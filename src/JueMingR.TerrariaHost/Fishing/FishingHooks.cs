using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Companions;

namespace JueMingR.TerrariaHost.Fishing
{
    internal static class FishingHooks
    {
        private static HostFishing host;
        private static readonly Harmony harmony=new Harmony("JueMingR.Fishing");
        private static bool installed, companions;
        [ThreadStatic] private static HostCompanions.FishingGrant actorGrant;
        internal static void Install(HostFishing value)
        {
            InstallNative();
            host=value;
        }
        internal static void InstallForCompanions() { InstallNative(); companions=true; }
        internal static void UninstallForCompanions() { companions=false; UninstallIfUnused(); }
        private static void InstallNative()
        {
            if(installed)return;
            var pull=typeof(Player).GetMethod("ItemCheck_PullFishingBobbers",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Item)},null);
            var give=typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Player),typeof(int)},null);
            if(pull==null || pull.ReturnType!=typeof(bool) || pull.GetMethodBody()==null || give==null || give.ReturnType!=typeof(void) || give.GetMethodBody()==null)throw new MissingMethodException("fishing native ABI");
            try
            {
                harmony.Patch(pull,prefix:new HarmonyMethod(typeof(FishingHooks),nameof(BeforePull)),postfix:new HarmonyMethod(typeof(FishingHooks),nameof(Pulled)));
                harmony.Patch(give,prefix:new HarmonyMethod(typeof(FishingHooks),nameof(BeforeGive)),finalizer:new HarmonyMethod(typeof(FishingHooks),nameof(Given)));
                installed=true;
            }
            catch { UninstallIfUnused(); throw; }
        }
        private struct PullState
        {
            internal HostCompanions.Actor Actor;
            internal long HumanToken;
        }
        private static void BeforePull(Player __instance,Item __0,out PullState __state)
        {
            __state=default;
            var ai=companions?CompanionHooks.FishingHost:null;
            var a=ai?.Owned(__instance);
            if(a!=null) { if(ai.BeforeCompanionPull(a,__0))__state.Actor=a; return; }
            __state.HumanToken=host?.Session.BeforeNativePull(__instance,__0)??0;
        }
        private static void Pulled(Player __instance,PullState __state,bool __runOriginal)
        {
            if(!__runOriginal)return;
            if(__state.Actor!=null)CompanionHooks.FishingHost?.AfterCompanionPull(__state.Actor);
            else host?.Session.NativePulled(__instance,__state.HumanToken);
        }
        private struct GrantState
        {
            internal ItemSourceHooks.Origin Human;
            internal HostCompanions.FishingGrant Actor, Previous;
        }
        private static void BeforeGive(Projectile __instance,Player __0,int __1,out GrantState __state)
        {
            __state=default;
            __state.Previous=actorGrant;
            actorGrant=null;
            var ai=companions?CompanionHooks.FishingHost:null;
            var a=ai?.Owned(__0);
            if(a!=null) { __state.Actor=actorGrant=ai.BeforeFishingGrant(a,__instance,__1); return; }
            if(host==null || host.Settings.Value.StoreMode!=1 || !host.Session.OwnsProduct(__0,__instance))return;
            host.SyncStorage();__state.Human=ItemSourceHooks.BeginFishing(__0,host.Session.Rod,__1,host.Session.Token);
        }
        internal static HostCompanions.FishingGrant BeforeNativeProduct(Player player,Item item)
        {
            var grant=actorGrant;
            // Native Give owns this scope and its identified bobber. Observe
            // the actual GetItem entrance, including an entirely overflowing
            // item; the small QuickSpawnItem wrappers can be inlined.
            if(grant==null || !ReferenceEquals(grant.Actor.Body,player) || item==null ||
                item.type!=grant.Type || item.stack<=0)return null;
            grant.Prefix=item.prefix;
            return grant;
        }
        private static Exception Given(Exception __exception,bool __runOriginal,GrantState __state)
        {
            actorGrant=__state.Previous;
            if(__state.Actor!=null && __runOriginal && __exception==null)
                CompanionHooks.FishingHost?.FinishFishingGrant(__state.Actor);
            if(__state.Human!=null)
            {
                if(!__runOriginal && __exception==null)__state.Human.Cancelled=true;
                ItemSourceHooks.FinishFishing(__state.Human,__exception==null);
            }
            return __exception;
        }
        internal static void Uninstall(){host=null;UninstallIfUnused();}
        private static void UninstallIfUnused()
        {
            if(host!=null || companions)return;
            foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);
            installed=false;
        }
    }
}
