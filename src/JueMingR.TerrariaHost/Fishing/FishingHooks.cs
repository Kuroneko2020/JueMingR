using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.Fishing
{
    internal static class FishingHooks
    {
        private static HostFishing host;
        private static readonly Harmony harmony=new Harmony("JueMingR.Fishing");
        internal static void Install(HostFishing value)
        {
            host=value;
            var pull=typeof(Player).GetMethod("ItemCheck_PullFishingBobbers",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Item)},null);
            var give=typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Player),typeof(int)},null);
            if(pull==null || pull.ReturnType!=typeof(bool) || pull.GetMethodBody()==null || give==null || give.ReturnType!=typeof(void) || give.GetMethodBody()==null)throw new MissingMethodException("fishing native ABI");
            harmony.Patch(pull,prefix:new HarmonyMethod(typeof(FishingHooks),nameof(BeforePull)),postfix:new HarmonyMethod(typeof(FishingHooks),nameof(Pulled)));
            harmony.Patch(give,prefix:new HarmonyMethod(typeof(FishingHooks),nameof(BeforeGive)),finalizer:new HarmonyMethod(typeof(FishingHooks),nameof(Given)));
        }
        private static void BeforePull(Player __instance,Item __0,out long __state){__state=host?.Session.BeforeNativePull(__instance,__0)??0;}
        private static void Pulled(Player __instance,long __state,bool __runOriginal){if(__runOriginal)host?.Session.NativePulled(__instance,__state);}
        private static void BeforeGive(Projectile __instance,Player __0,int __1,out ItemSourceHooks.Origin __state)
        {
            __state=null;if(host==null || host.Settings.Value.StoreMode!=1 || !host.Session.OwnsProduct(__0,__instance))return;
            host.SyncStorage();__state=ItemSourceHooks.BeginFishing(__0,host.Session.Rod,__1,host.Session.Token);
        }
        private static Exception Given(Exception __exception,bool __runOriginal,ItemSourceHooks.Origin __state)
        {
            if(__state!=null){if(!__runOriginal && __exception==null)__state.Cancelled=true;ItemSourceHooks.FinishFishing(__state,__exception==null);}
            return __exception;
        }
        internal static void Uninstall(){foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);host=null;}
    }
}
