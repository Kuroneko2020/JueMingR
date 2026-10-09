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
        private static bool manualBuffered;
        private static Player deferredSync;
        private static uint deferredSyncStep;
        private static long deferredSyncSession;
        internal static void EndCombatFrame(){deferredSync=null;}
        internal static void Install(HostTools value)
        {
            host=value;manualBuffered=false;deferredSync=null;
            harmony.CreateReversePatcher(typeof(Player).GetMethod("TrySyncingInput",BindingFlags.Instance|BindingFlags.NonPublic),new HarmonyMethod(typeof(ToolHooks).GetMethod(nameof(NativeSync),BindingFlags.Static|BindingFlags.NonPublic))).Patch();
            Patch(typeof(Player),"PickItemSelectionOverride",new[]{typeof(int).MakeByRefType()},null,nameof(Pick));
            Patch(typeof(Player),"TrySyncingInput",Type.EmptyTypes,nameof(Sync));
            Patch(typeof(Player),"LookForTileInteractions",Type.EmptyTypes,null,nameof(Interactions),nameof(InteractionsFinal));
            Patch(typeof(Player),"ItemCheck",Type.EmptyTypes,nameof(Before),nameof(After),nameof(Final));
            Patch(typeof(Player),"ItemCheck_Shoot",new[]{typeof(int),typeof(Item),typeof(int),typeof(bool)},nameof(ShotBefore),nameof(ShotAfter),nameof(ShotFinal));
            Patch(typeof(Player),"ItemCheck_StartActualUse",new[]{typeof(Item)},null,nameof(Started));
            Patch(typeof(Player),"ItemCheck_AutoReuseLogic",new[]{typeof(Item)},null,nameof(AfterReuse));
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
            Patch(selection,"Update",Type.EmptyTypes,nameof(SelectionUpdate),nameof(SelectionAfter));
        }
        private static void Patch(Type type,string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {
            var m=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
            if(m==null || m.GetMethodBody()==null)throw new MissingMethodException("tools-abi:"+name);
            harmony.Patch(m,Hook(prefix),Hook(postfix),null,Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(ToolHooks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        private static void ShotBefore(Player __instance,Item __1,out Combat.CombatCursorScope __state)
        {__state=null;try{__state=host?.Combat?.Attack?.BeginShot(__instance,__1);}catch{host?.Combat?.Attack?.Clear();}}
        private static void ShotAfter(Combat.CombatCursorScope __state){__state?.End();}
        private static Exception ShotFinal(Combat.CombatCursorScope __state,Exception __exception){__state?.End();return __exception;}
        internal static void Uninstall(){foreach(var m in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);host=null;deferredSync=null;}
        private static void Pick(Player __instance,ref int __0,ref bool __result){host?.Use.Pick(__instance,ref __0,ref __result);}
        [HarmonyPriority(Priority.First)]
        private static bool Sync(Player __instance)
        {
            bool defer=false;
            if(ReferenceEquals(__instance,host?.Player))
            {deferredSync=null;host.Mining.ObserveManual();host.Combat?.Use.BeforeSync(__instance);defer=Main.netMode==1 && (host.Combat?.Use.DeferSync??false);}
            host?.Use.BeforeSync(__instance);
            if(defer){deferredSync=__instance;deferredSyncStep=Main.GameUpdateCount;deferredSyncSession=host.Combat.Runtime.Generation;}
            return !defer;
        }
        // The original diff-based packet outlet runs once after right-click
        // interaction/usage arbitration. Reverse patch avoids running another
        // owner's Sync prefix twice or reproducing vanilla's network rules.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void NativeSync(Player player){throw new InvalidOperationException("Original input sync not installed.");}
        private static void Interactions(Player __instance)
        {
            if(!ReferenceEquals(__instance,host?.Player))return;
            host.Combat?.Use.AfterInteractions(__instance);host.Combat?.Handoff.AfterInteractions(__instance);
            if(ReferenceEquals(deferredSync,__instance))
            {deferredSync=null;if(deferredSyncStep==Main.GameUpdateCount && deferredSyncSession==host.Combat.Runtime.Generation && host.Combat.Runtime.IsSessionActive)NativeSync(__instance);}
        }
        private static Exception InteractionsFinal(Player __instance,Exception __exception)
        {if(__exception!=null && ReferenceEquals(__instance,host?.Player)){deferredSync=null;host.Combat?.Handoff.Interrupted();host.Combat?.Use.Stop();}return __exception;}
        private static void Started(Player __instance,Item __0){host?.Use.Started(__instance);host?.Fishing.ObserveCast(__instance,__0);host?.FishingStarted?.Invoke(__instance,__0);host?.Combat?.Use.Started(__instance,__0);}
        private static void AfterReuse(Player __instance){host?.Combat?.Use.AfterReuse(__instance);}
        private static void ProjectileCreated(Player __instance,Projectile __0){host?.Use.ObserveProjectile(__instance,__0);host?.FishingProjectile?.Invoke(__instance,__0);host?.Combat?.Use.Created(__instance,__0);}
        // This call's receipt chooses its owner, even if that action has since
        // stopped or another one began. A value receipt adds no idle allocation;
        // the out scope is published before Combat borrows any input.
        private struct ProjectileLease {internal long Token;internal ToolUse Tool;internal Combat.CombatUse Combat;internal Combat.CombatInputScope Scope;}
        private static void ProjectileBefore(Projectile __instance,out ProjectileLease __state)
        {
            __state=default(ProjectileLease);var current=host;if(current==null)return;
            __state.Combat=current.Combat?.Use;
            __state.Combat?.BeginProjectile(__instance,out __state.Scope);
            if(__state.Scope!=null || !current.Use.OwnsProjectile(__instance))return;
            __state.Tool=current.Use;__state.Token=current.Use.Operation;current.Use.BeginProjectile();
        }
        private static void EndProjectile(Projectile shot,ref ProjectileLease state,Exception error)
        {
            // Consume before dispatch: Postfix plus a later failing postfix's
            // Finalizer must not notify/return twice or touch a successor.
            var receipt=state;state=default(ProjectileLease);
            if(receipt.Scope!=null)receipt.Combat.EndProjectile(shot,receipt.Scope,error);
            else if(receipt.Token>0)receipt.Tool.EndProjectile(receipt.Token,error);
        }
        private static void ProjectileAfter(Projectile __instance,ref ProjectileLease __state){EndProjectile(__instance,ref __state,null);}
        private static Exception ProjectileFinal(Projectile __instance,ref ProjectileLease __state,Exception __exception){if(__exception!=null)EndProjectile(__instance,ref __state,__exception);return __exception;}
        private static void Boundary(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Use.Retire();}
        // Reference state exists before Begin; even a Prefix exception midway
        // through temporary input borrowing reaches the same idempotent cleanup.
        private sealed class Lease {internal long Token;internal Combat.CombatInputScope Combat;}
        private static void SeedBoundary(Player player){if(ReferenceEquals(player,host?.Player))host.Herbs.InvalidateSeeds();}
        private static void Before(Player __instance,out Lease __state){__state=null;SeedBoundary(__instance);if(host==null)return;if(ReferenceEquals(__instance,host.Player)){host.Combat?.Facing.Apply(__instance);host.Combat?.Attack?.PrepareNatural();}if(host.Combat?.Use.Active??false){__state=new Lease();__state.Combat=host.Combat.Use.Begin(__instance);return;}if(!host.Use.Active)return;__state=new Lease{Token=host.Use.Operation};host.Use.Begin(__instance);}
        private static void After(Player __instance,Lease __state){SeedBoundary(__instance);host?.Use.End(__instance,__state?.Token??0,null);host?.Combat?.Use.End(__state?.Combat,null);if(ReferenceEquals(__instance,host?.Player))host.Combat?.Facing.Apply(__instance);}
        private static Exception Final(Player __instance,Lease __state,Exception __exception){if(__exception!=null){SeedBoundary(__instance);host?.Use.End(__instance,__state?.Token??0,__exception);host?.Combat?.Use.End(__state?.Combat,__exception);}return __exception;}
        private static void Select(Player ___player){if(host!=null && (host.Enabled || host.Combat?.Enabled==true) && ReferenceEquals(___player,host.Player) && !host.Use.Returning && !host.Items.ReturningSelection && !(host.Combat?.Use.Selecting??false)){manualBuffered=true;host.ManualSelection();}}
        private static void SelectionUpdate(Player ___player)
        {
            host?.Combat?.Handoff.BeforeSelection(___player);
            if(host==null || !host.Enabled || !ReferenceEquals(___player,host.Player))return;
            host.Npcs.BeginActions(host.Input.Frame);
            // Only a genuine Select owns a buffered manual handoff. Another
            // automatic owner returning its lease is not a new player intent.
            if(!___player.selectedItemState.HasBufferedChange)manualBuffered=false;
            else if(manualBuffered)host.ManualSelectionFrame=host.Input.Frame;
        }
        private static void SelectionAfter(Player ___player){host?.Combat?.Handoff.AfterSelection(___player);}
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
