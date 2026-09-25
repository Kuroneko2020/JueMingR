using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Recovery
{
    internal static class RecoveryHooks
    {
        private static HostRecovery host;
        private static readonly Harmony harmony=new Harmony("JueMingR.Recovery");
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Install(HostRecovery value)
        {
            host=value;
            Patch(typeof(Player),"QuickHeal_GetItemToUse",Type.EmptyTypes,nameof(HealSelection));
            Patch(typeof(Player),"QuickMana_GetItemToUse",Type.EmptyTypes,nameof(ManaSelection));
            Patch(typeof(Player),"QuickMana",Type.EmptyTypes,nameof(ManaStart));
            Patch(typeof(Player),"ApplyLifeAndOrMana",new[]{typeof(Item)},nameof(BeforeEffect),nameof(AfterEffect));
        }
        internal static void Patch(Type type,string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {
            MethodInfo method=type.GetMethod(name,Flags,null,args,null);if(method==null)throw new MissingMethodException(type.FullName,name);
            harmony.Patch(method,HM(prefix),HM(postfix),null,HM(finalizer));
        }
        private static HarmonyMethod HM(string name){return name==null?null:new HarmonyMethod(typeof(RecoveryHooks).GetMethod(name,Flags));}
        internal static void Uninstall(){foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);host=null;}
        private static bool HealSelection(Player __instance,ref Item __result)
        {if(host==null || host.PotionsUse.Kind!=1)return true;__result=host.PotionsUse.Selection(__instance,1);return false;}
        private static bool ManaSelection(Player __instance,ref Item __result)
        {if(host==null || host.PotionsUse.Kind!=2)return true;__result=host.PotionsUse.Selection(__instance,2);return false;}
        private static bool ManaStart(Player __instance)
        {return host==null || host.Value(1)==0 || !ReferenceEquals(__instance,host.Player) || host.PotionsUse.ManaFrame!=host.Input.Frame;}
        private struct EffectState{internal int Life,Mana;internal bool Observe;}
        private static void BeforeEffect(Player __instance,out EffectState __state)
        {__state=default(EffectState);if(host!=null && (host.Value(0)!=0 || host.Value(1)!=0) && ReferenceEquals(__instance,host.Player))__state=new EffectState{Life=__instance.statLife,Mana=__instance.statMana,Observe=true};}
        private static void AfterEffect(Player __instance,Item __0,EffectState __state)
        {if(__state.Observe)host.PotionsUse.Applied(__instance,__0,__state.Life,__state.Mana);}
    }
}
