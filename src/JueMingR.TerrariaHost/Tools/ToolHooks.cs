using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Tools
{
    internal static class ToolHooks
    {
        private static HostTools host;
        private static readonly Harmony harmony=new Harmony("JueMingR.Tools");
        private struct NativeMiningScope {internal Player Player;internal Item Item;internal int X,Y;}
        private static NativeMiningScope miningScope;
        internal static void Install(HostTools value)
        {
            host=value;
            Patch(typeof(Player),"PickItemSelectionOverride",new[]{typeof(int).MakeByRefType()},null,nameof(Pick));
            Patch(typeof(Player),"TrySyncingInput",Type.EmptyTypes,nameof(Sync));
            Patch(typeof(Player),"ItemCheck",Type.EmptyTypes,nameof(Before),nameof(After),nameof(Final));
            Patch(typeof(Player),"ItemCheck_StartActualUse",new[]{typeof(Item)},null,nameof(Started));
            Patch(typeof(Player),"DropItems",new[]{typeof(bool)},nameof(Boundary));
            Patch(typeof(Player),"PickTile",new[]{typeof(int),typeof(int),typeof(int),typeof(int)},nameof(BeforePick),nameof(AfterPick));
            Patch(typeof(Player),"PlaceThing_Tiles",new[]{typeof(bool)},nameof(BeforePlant),nameof(AfterPlant));
            Patch(typeof(Player),"ItemCheck_CatchCritters",new[]{typeof(Item),typeof(Microsoft.Xna.Framework.Rectangle)},nameof(Catch));
            Patch(typeof(Player),"ItemCheck_UseMiningTools",new[]{typeof(Item)},nameof(Mine));
            Patch(typeof(Player),"ItemCheck_UseMiningTools_ActuallyUseMiningTool",new[]{typeof(Item),typeof(bool).MakeByRefType(),typeof(int),typeof(int)},nameof(MiningEnter),nameof(MiningLeave),nameof(MiningFinal));
            Patch(typeof(Player),"TryUpdateChannel",new[]{typeof(Projectile)},null,nameof(ProjectileCreated));
            Patch(typeof(Projectile),"AI",Type.EmptyTypes,nameof(ProjectileBefore),nameof(ProjectileAfter),nameof(ProjectileFinal));
            var selection=typeof(Player).GetNestedType("SelectedItemState",BindingFlags.Public|BindingFlags.NonPublic);
            if(selection==null)throw new MissingMemberException("SelectedItemState");
            Patch(selection,"Select",new[]{typeof(int)},nameof(Select));
            Patch(selection,"Update",Type.EmptyTypes,nameof(SelectionUpdate));
        }
        private static void Patch(Type type,string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {
            var m=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
            if(m==null || m.GetMethodBody()==null)throw new MissingMethodException("tools-abi:"+name);
            harmony.Patch(m,Hook(prefix),Hook(postfix),null,Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(ToolHooks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        internal static void Uninstall(){foreach(var m in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);host=null;}
        private static void Pick(Player __instance,ref int __0,ref bool __result){host?.Use.Pick(__instance,ref __0,ref __result);}
        private static void Sync(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Mining.ObserveManual();host?.Use.BeforeSync(__instance);}
        private static void Started(Player __instance,Item __0){host?.Use.Started(__instance);host?.Fishing.ObserveCast(__instance,__0);}
        private static void ProjectileCreated(Player __instance,Projectile __0){host?.Use.ObserveProjectile(__instance,__0);}
        private static void ProjectileBefore(Projectile __instance,out Lease __state)
        {__state=null;if(host==null || !host.Use.OwnsProjectile(__instance))return;__state=new Lease{Token=host.Use.Operation};host.Use.BeginProjectile();}
        private static void ProjectileAfter(Lease __state){if(__state!=null)host?.Use.EndProjectile(__state.Token,null);}
        private static Exception ProjectileFinal(Lease __state,Exception __exception){if(__state!=null && __exception!=null)host?.Use.EndProjectile(__state.Token,__exception);return __exception;}
        private static void Boundary(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Use.Retire();}
        // Reference state exists before Begin; even a Prefix exception midway
        // through temporary input borrowing reaches the same idempotent cleanup.
        private sealed class Lease {internal long Token;}
        private static void Before(Player __instance,out Lease __state){__state=null;if(host==null || !host.Use.Active)return;__state=new Lease{Token=host.Use.Operation};host.Use.Begin(__instance);}
        private static void After(Player __instance,Lease __state){host?.Use.End(__instance,__state?.Token??0,null);}
        private static Exception Final(Player __instance,Lease __state,Exception __exception){if(__exception!=null)host?.Use.End(__instance,__state?.Token??0,__exception);return __exception;}
        private static void Select(Player ___player){if(host!=null && host.Enabled && ReferenceEquals(___player,host.Player) && !host.Use.Returning && !host.Items.ReturningSelection)host.ManualSelection();}
        private static void SelectionUpdate(Player ___player){if(host!=null && host.Enabled && ReferenceEquals(___player,host.Player) && ___player.selectedItemState.HasBufferedChange && !host.Use.Returning)host.ManualSelectionFrame=host.Input.Frame;}
        // Pets and other projectile consumers also call Player.PickTile. Only
        // the exact native held-tool scope may establish a manual first hit.
        private static void MiningEnter(Player __instance,Item __0,int __2,int __3,out NativeMiningScope __state)
        {__state=miningScope;if(host!=null && host.Mode(2)!=0 && ReferenceEquals(__instance,host.Player))miningScope=new NativeMiningScope{Player=__instance,Item=__0,X=__2,Y=__3};}
        private static void MiningLeave(NativeMiningScope __state){miningScope=__state;}
        private static Exception MiningFinal(NativeMiningScope __state,Exception __exception){miningScope=__state;return __exception;}
        private static void BeforePick(Player __instance,int __0,int __1,out AutoMining.ManualHit __state){__state=ReferenceEquals(__instance,miningScope.Player) && ReferenceEquals(__instance.HeldItem,miningScope.Item) && __0==miningScope.X && __1==miningScope.Y?host.Mining.BeforePick(__instance,__0,__1):default(AutoMining.ManualHit);}
        private static void AfterPick(Player __instance,AutoMining.ManualHit __state){host?.Mining.AfterPick(__instance,__state);}
        private static bool BeforePlant(Player __instance,out HerbHarvest.HarvestReceipt __state)
        {__state=host?.Herbs.BeforePlant(__instance)??default(HerbHarvest.HarvestReceipt);return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.InNativeUse || !host.Use.Is(ToolKind.Harvest) && !host.Use.Is(ToolKind.Seed) || host.Use.ActionValid;}
        private static void AfterPlant(Player __instance,HerbHarvest.HarvestReceipt __state){host?.Herbs.AfterPlant(__instance,__state);}
        private static bool Catch(Player __instance){return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Is(ToolKind.Capture) || host.Use.ActionValid;}
        private static bool Mine(Player __instance){return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Is(ToolKind.Mining) || host.Use.ActionValid;}
    }
}
