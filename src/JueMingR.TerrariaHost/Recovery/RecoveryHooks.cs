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
            RecoveryBankGuards.Install(value,harmony);
            Patch(typeof(Player),"QuickHeal_GetItemToUse",Type.EmptyTypes,nameof(HealSelection),nameof(LearnSelection));
            Patch(typeof(Player),"QuickMana_GetItemToUse",Type.EmptyTypes,nameof(ManaSelection));
            Patch(typeof(Player),"QuickMana",Type.EmptyTypes,nameof(ManaStart));
            Patch(typeof(Player),"ApplyLifeAndOrMana",new[]{typeof(Item)},nameof(BeforeEffect),nameof(AfterEffect));
            Patch(typeof(Player),"QuickBuff_PickBestFoodItem",Type.EmptyTypes,nameof(BuffFood),nameof(LearnSelection));
            Patch(typeof(Player),"QuickBuff_ShouldUseItem",new[]{typeof(Item),typeof(int).MakeByRefType()},nameof(BuffChoice));
            Patch(typeof(Player),"ItemCheck",Type.EmptyTypes,nameof(ManualItem),null,nameof(ManualEnd));
            Patch(typeof(Player),"QuickBuff",Type.EmptyTypes,nameof(ManualBuff),null,nameof(ManualEnd));
            Patch(typeof(Player),"QuickHeal",Type.EmptyTypes,nameof(ManualHeal),null,nameof(ManualEnd));
            Patch(typeof(Player),"QuickBuff_UseItemForBuff",new[]{typeof(Item),typeof(int)},nameof(LearnBuffSource));
            Patch(typeof(Player),"AddBuff",new[]{typeof(int),typeof(int),typeof(bool)},nameof(LearnBeforeAdd),nameof(LearnAfterAdd));
            Patch(typeof(Main),"DrawBuffIcon",new[]{typeof(int),typeof(int),typeof(int),typeof(int)},nameof(IconStart),null,nameof(IconEnd));
            Patch(typeof(Main),"TryRemovingBuff",new[]{typeof(int),typeof(int)},nameof(BeforeRemove),nameof(AfterRemove));
            Patch(typeof(Player),"InteractWithCraftingStation",new[]{typeof(Terraria.GameContent.UI.NewCraftingUI.RecipeFilter)},nameof(Crafting));
            Patch(typeof(Player),"SetTalkNPC",new[]{typeof(int)},nameof(TalkChanged));
            Patch(typeof(Player),"BuyItem",new[]{typeof(long),typeof(int)},nameof(PaymentStart),nameof(PaymentEnd));
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
        private static bool BuffFood(Player __instance,ref Item __result)
        {if(host==null || !host.BuffUse.Executing)return true;__result=host.BuffUse.Food(__instance);return false;}
        private static bool BuffChoice(Player __instance,Item __0,ref int __1,ref bool __result)
        {if(host==null || !host.BuffUse.Executing)return true;if(host.BuffUse.Permit(__instance,__0))return true;__1=0;__result=false;return false;}
        private struct EffectState{internal int Life,Mana;internal bool Observe;}
        private static void BeforeEffect(Player __instance,out EffectState __state)
        {__state=default(EffectState);if(host!=null && (host.Value(0)!=0 || host.Value(1)!=0) && ReferenceEquals(__instance,host.Player))__state=new EffectState{Life=__instance.statLife,Mana=__instance.statMana,Observe=true};}
        private static void AfterEffect(Player __instance,Item __0,EffectState __state)
        {if(__state.Observe)host.PotionsUse.Applied(__instance,__0,__state.Life,__state.Mana);}
        private static void ManualItem(Player __instance,out int __state){__state=host?.Learning.Begin(__instance,1)??0;}
        private static void ManualBuff(Player __instance,out int __state){__state=host?.Learning.Begin(__instance,2)??0;}
        private static void ManualHeal(Player __instance,out int __state){__state=host?.Learning.Begin(__instance,3)??0;}
        private static Exception ManualEnd(int __state,Exception __exception){host?.Learning.End(__state);return __exception;}
        private static void LearnSelection(Item __result){host?.Learning.CaptureQuick(__result);}
        private static void LearnBuffSource(Item __0){host?.Learning.CaptureQuick(__0);}
        private static void LearnBeforeAdd(Player __instance,int __0,out int __state){__state=host?.Learning.BeforeAdd(__instance,__0)??-1;}
        private static void LearnAfterAdd(Player __instance,int __0,int __state){host?.Learning.AfterAdd(__instance,__0,__state);}
        private static void IconStart(int __1,out int __state){__state=host?.Learning.BeginIcon(__1)??-1;}
        private static Exception IconEnd(int __state,Exception __exception){host?.Learning.EndIcon(__state);return __exception;}
        private static void BeforeRemove(int __0,int __1,out bool __state){__state=host?.Learning.BeforeRemove(__0,__1)??false;}
        private static void AfterRemove(int __1,bool __state){host?.Learning.AfterRemove(__1,__state);}
        private static bool Crafting(){return host==null || !host.Furniture.Executing || host.Player==null;}
        private static void TalkChanged(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Dialog.Changed();}
        private static bool PaymentStart(Player __instance,long __0,int __1,ref bool __result,out bool __state)
        {__state=host!=null && host.Nurse.Executing;if(!__state || host.Nurse.BeforePayment(__instance,__0,__1))return true;__result=false;return false;}
        private static void PaymentEnd(bool __result,bool __state){if(__state)host.Nurse.Paid(__result);}
    }
}
