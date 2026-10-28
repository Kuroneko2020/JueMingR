using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
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
            Patch(typeof(Terraria.GameContent.SmartCursorHelper),"SmartCursorLookup",new[]{typeof(Player)},nameof(TargetBefore),nameof(TargetAfter),nameof(TargetFinal));
            Patch(typeof(Player),"Update",new[]{typeof(int)},null,nameof(PlayerUpdateAfter),nameof(PlayerUpdateFinal));
            Patch(typeof(Player),"LookForTileInteractions",Type.EmptyTypes,null,nameof(Interactions),nameof(InteractionsFinal));
            Patch(typeof(Player),"ItemCheck",Type.EmptyTypes,nameof(Before),nameof(After),nameof(Final));
            Patch(typeof(Player),"ItemCheck_Shoot",new[]{typeof(int),typeof(Item),typeof(int),typeof(bool)},nameof(ShotBefore),nameof(ShotAfter),nameof(ShotFinal));
            Patch(typeof(Player),"ItemCheck_CheckCanUse_Inner",new[]{typeof(Item),typeof(bool)},nameof(PlacementCheckBefore),nameof(PlacementCheckAfter),nameof(PlacementCheckFinal));
            Patch(typeof(Player),"ItemCheck_StartActualUse",new[]{typeof(Item)},null,nameof(Started));
            Patch(typeof(Player),"ItemCheck_AutoReuseLogic",new[]{typeof(Item)},null,nameof(AfterReuse));
            Patch(typeof(Player),"DropItems",new[]{typeof(bool)},nameof(Boundary));
            Patch(typeof(Player),"ToggleInv",Type.EmptyTypes,nameof(InventoryToggle));
            Patch(typeof(Player),"OpenInventory",new[]{typeof(bool)},nameof(InventoryOpen));
            Patch(typeof(Player),"PickTile",new[]{typeof(int),typeof(int),typeof(int),typeof(int)},nameof(BeforePick),nameof(AfterPick));
            Patch(typeof(Player),"PlaceThing_Tiles",new[]{typeof(bool)},nameof(BeforePlant),nameof(AfterPlant),nameof(FinalPlant));
            Patch(typeof(Player),"PlaceThing_Walls",Type.EmptyTypes,nameof(BeforeWall));
            Patch(typeof(Player),"PlaceThing_Tiles_PlaceIt",new[]{typeof(bool),typeof(TileObject),typeof(int)},nameof(BeforeTileChoice));
            Patch(typeof(Player),"PlaceThing_Tiles_BlockPlacementForAssortedThings",new[]{typeof(bool)},nameof(BeforeBuildPreparation),nameof(AfterBuildPreparation),nameof(FinalBuildPreparation));
            Patch(typeof(Player),"ItemCheck_CatchCritters",new[]{typeof(Item),typeof(Microsoft.Xna.Framework.Rectangle)},nameof(Catch));
            Patch(typeof(Player),"ItemCheck_UseMiningTools",new[]{typeof(Item)},nameof(Mine));
            Patch(typeof(Player),"ItemCheck_UseMiningTools_TryPoundingTile",new[]{typeof(Item),typeof(int),typeof(bool).MakeByRefType(),typeof(int),typeof(int)},nameof(BeforePound));
            Patch(typeof(Player),"ItemCheck_UseMiningTools_TryHittingWall",new[]{typeof(Item),typeof(int),typeof(int)},nameof(BeforeHitWall));
            Patch(typeof(Player),"PlaceThing_Paintbrush",Type.EmptyTypes,nameof(BeforeBrush));
            Patch(typeof(Player),"PlaceThing_PaintRoller",Type.EmptyTypes,nameof(BeforeRoller));
            Patch(typeof(Player),"PlaceThing_PaintScraper_TryScraping",new[]{typeof(int),typeof(int)},nameof(BeforeScrape));
            Patch(typeof(Player),"PlaceThing_PaintScraper_LongMoss",new[]{typeof(int),typeof(int)},nameof(BeforeScrapeMoss));
            Patch(typeof(Player),"TryPainting",new[]{typeof(int),typeof(int),typeof(bool),typeof(bool)},nameof(BeforePainting));
            Patch(typeof(Player),"ApplyPaint",new[]{typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(Item)},nameof(BeforeApplyPaint));
            Patch(typeof(Player),"ApplyCoating",new[]{typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(Item)},nameof(BeforeApplyCoating));
            Patch(typeof(Player),"ItemCheck_UseWiringTools",new[]{typeof(Item)},nameof(BeforeWiring),nameof(AfterWiring),nameof(FinalWiring));
            Patch(typeof(Player),"ItemCheck_UseMiningTools_ActuallyUseMiningTool",new[]{typeof(Item),typeof(bool).MakeByRefType(),typeof(int),typeof(int)},nameof(MiningEnter),nameof(MiningLeave),nameof(MiningFinal));
            Patch(typeof(Player),"TryUpdateChannel",new[]{typeof(Projectile)},null,nameof(ProjectileCreated));
            Patch(typeof(Projectile),"AI",Type.EmptyTypes,nameof(ProjectileBefore),nameof(ProjectileAfter),nameof(ProjectileFinal));
            Patch(typeof(Projectile),"Kill",Type.EmptyTypes,nameof(KillBefore),nameof(KillAfter),nameof(KillFinal));
            Patch(typeof(Projectile),"Damage",Type.EmptyTypes,nameof(DamageBefore),nameof(ShotAfter),nameof(ShotFinal));
            Patch(typeof(Main),"UpdateWorld_Projectiles",Type.EmptyTypes,nameof(BeforeProjectileWorld));
            harmony.Patch(AccessTools.Method(typeof(Main),"DoDraw",new[]{typeof(Microsoft.Xna.Framework.GameTime)}),
                transpiler:new HarmonyMethod(typeof(ToolHooks),nameof(ConstructionInputTail)),
                finalizer:new HarmonyMethod(typeof(ToolHooks),nameof(ConstructionDrawFinal)));
            var selection=typeof(Player).GetNestedType("SelectedItemState",BindingFlags.Public|BindingFlags.NonPublic);
            if(selection==null)throw new MissingMemberException("SelectedItemState");
            Patch(selection,"Select",new[]{typeof(int)},nameof(Select));
            Patch(selection,"Update",Type.EmptyTypes,nameof(SelectionUpdate),nameof(SelectionAfter));
        }
        private static void Patch(Type type,string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {
            var m=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
            if(m==null || m.GetMethodBody()==null)throw new MissingMethodException("tools-abi:"+name);
            harmony.Patch(m,Hook(prefix),Hook(postfix),null,Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(ToolHooks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        private static IEnumerable<CodeInstruction> ConstructionInputTail(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();int start,end;
            Items.ItemMaterialPreparation.LocateNativeInputTail(code,out start,out end);
            var before=new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Items.ItemMaterialPreparation),nameof(Items.ItemMaterialPreparation.BeforeNativeInputTail)));
            var after=new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Items.ItemMaterialPreparation),nameof(Items.ItemMaterialPreparation.AfterNativeInputTail)));
            before.labels.AddRange(code[start].labels);code[start].labels.Clear();
            after.labels.AddRange(code[end].labels);code[end].labels.Clear();
            code.Insert(end,after);code.Insert(start,before);return code;
        }
        private static Exception ConstructionDrawFinal(Exception __exception)
        {if(__exception!=null)Items.ItemMaterialPreparation.AfterNativeInputTail();return __exception;}
        private static void InventoryToggle(Player __instance){Items.ItemMaterialPreparation.BeforeNativeInventoryToggle(__instance);}
        private static void InventoryOpen(){Items.ItemMaterialPreparation.BeforeNativeInventoryOpen();}
        private static void ShotBefore(Player __instance,Item __1,bool __3,out Combat.HostAttackControl.ShotReceipt __state)
        {__state=null;var attack=host?.Combat?.Attack;if(attack==null)return;Combat.CombatCursorScope cursor=null;try{cursor=attack.BeginShot(__instance,__1,__3);__state=attack.Control.BeginShot(__instance,__1,cursor);}catch(Exception error){cursor?.End();attack.FailLocal(error);}}
        private static void ShotAfter(Combat.HostAttackControl.ShotReceipt __state){__state?.Owner.EndShot(__state);}
        private static Exception ShotFinal(Combat.HostAttackControl.ShotReceipt __state,Exception __exception){__state?.Owner.EndShot(__state);return __exception;}
        private static void PlacementCheckBefore(Player __instance,Item __0,out Combat.CombatCursorScope __state)
        {__state=null;try{__state=host?.Combat?.Attack?.BeginPlacement(__instance,__0,false);}catch(Exception error){host?.Combat?.Attack?.FailLocal(error);}}
        private static void PlacementCheckAfter(Combat.CombatCursorScope __state){__state?.End();}
        private static Exception PlacementCheckFinal(Combat.CombatCursorScope __state,Exception __exception){__state?.End();return __exception;}
        private static void KillBefore(Projectile __instance,out Combat.CombatCursorScope __state)
        {__state=null;try{__state=host?.Combat?.Attack?.Control.BeginKill(__instance);}catch(Exception error){host?.Combat?.Attack?.FailLocal(error);}}
        private static void KillAfter(Combat.CombatCursorScope __state){__state?.End();}
        private static Exception KillFinal(Combat.CombatCursorScope __state,Exception __exception){__state?.End();return __exception;}
        private static void DamageBefore(Projectile __instance,out Combat.HostAttackControl.ShotReceipt __state)
        {__state=null;try{__state=host?.Combat?.Attack?.Control.BeginDamage(__instance);}catch(Exception error){host?.Combat?.Attack?.FailLocal(error);}}
        internal static void Uninstall(){foreach(var m in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);host=null;deferredSync=null;}
        private static void Pick(Player __instance,ref int __0,ref bool __result){host?.Use.Pick(__instance,ref __0,ref __result);}
        private static void TargetBefore(Player __0){host?.Use.BeforeTargetResolution(__0);}
        private static void TargetAfter(Player __0){host?.Use.AfterTargetResolution(__0);}
        private static Exception TargetFinal(Player __0,Exception __exception){if(__exception!=null)host?.Use.TargetResolutionFailed(__0);return __exception;}
        private static void PlayerUpdateAfter(Player __instance){host?.Use.FinishPlayerUpdate(__instance,null);}
        private static Exception PlayerUpdateFinal(Player __instance,Exception __exception){if(__exception!=null)host?.Use.FinishPlayerUpdate(__instance,__exception);return __exception;}
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
        private static void Started(Player __instance,Item __0)
        {
            host?.Use.Started(__instance);host?.Fishing.ObserveCast(__instance,__0);host?.FishingStarted?.Invoke(__instance,__0);host?.Combat?.Use.Started(__instance,__0);
            try{host?.Combat?.Attack?.StartedHand(__instance,__0);}catch(Exception error){host?.Combat?.Attack?.FailLocal(error);}
        }
        private static void AfterReuse(Player __instance){host?.Combat?.Use.AfterReuse(__instance);}
        private static void ProjectileCreated(Player __instance,Projectile __0){host?.Use.ObserveProjectile(__instance,__0);host?.FishingProjectile?.Invoke(__instance,__0);host?.Combat?.Use.Created(__instance,__0);host?.Combat?.Attack?.Control.Created(__instance,__0);}
        // Vanilla has finished ALL NPC slots here, but has not advanced any
        // projectile. Finite held volleys must capture their full receiver and
        // chase scene at this boundary, once; individual AI only consumes it.
        private static void BeforeProjectileWorld(){host?.Combat?.Attack?.PrepareControlledProjectileWorld();}
        // This call's receipt chooses its owner, even if that action has since
        // stopped or another one began. A value receipt adds no idle allocation;
        // the out scope is published before Combat borrows any input.
        private struct ProjectileLease {internal long Token;internal ToolUse Tool;internal Combat.CombatUse Combat;internal Combat.CombatInputScope Scope;internal Combat.CombatCursorScope Attack;internal Combat.HostAttackControl.ShotReceipt Derived;}
        private static void ProjectileBefore(Projectile __instance,out ProjectileLease __state)
        {
            __state=default(ProjectileLease);var current=host;if(current==null)return;
            __state.Combat=current.Combat?.Use;
            __state.Combat?.BeginProjectile(__instance,out __state.Scope);
            try{__state.Attack=current.Combat?.Attack?.Control.BeginAI(__instance);__state.Derived=current.Combat?.Attack?.Control.BeginDerived(__instance);}catch(Exception error){current.Combat?.Attack?.FailLocal(error);}
            if(__state.Scope!=null || !current.Use.OwnsProjectile(__instance))return;
            __state.Tool=current.Use;__state.Token=current.Use.Operation;current.Use.BeginProjectile();
        }
        private static void EndProjectile(Projectile shot,ref ProjectileLease state,Exception error)
        {
            // Consume before dispatch: Postfix plus a later failing postfix's
            // Finalizer must not notify/return twice or touch a successor.
            var receipt=state;state=default(ProjectileLease);
            receipt.Attack?.End();
            receipt.Derived?.Owner.EndShot(receipt.Derived);
            if(receipt.Scope!=null)receipt.Combat.EndProjectile(shot,receipt.Scope,error);
            else if(receipt.Token>0)receipt.Tool.EndProjectile(receipt.Token,error);
        }
        private static void ProjectileAfter(Projectile __instance,ref ProjectileLease __state){EndProjectile(__instance,ref __state,null);}
        private static Exception ProjectileFinal(Projectile __instance,ref ProjectileLease __state,Exception __exception){if(__exception!=null)EndProjectile(__instance,ref __state,__exception);return __exception;}
        private static void Boundary(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Use.Retire();}
        // Reference state exists before Begin; even a Prefix exception midway
        // through temporary input borrowing reaches the same idempotent cleanup.
        private sealed class Lease {internal long Token,Generation;internal Combat.CombatInputScope Combat;internal Combat.HostAttackAim.PlacementUse Placement;internal Blueprint.BlueprintHooks.MouseLease Blueprint;}
        private static void SeedBoundary(Player player){if(ReferenceEquals(player,host?.Player))host.Herbs.InvalidateSeeds();}
        // Select from the captured physical sample before optional Facing and
        // before vanilla builds this action's pose; input leases follow it.
        private static void Before(Player __instance,out Lease __state)
        {
            __state=null;SeedBoundary(__instance);if(host==null)return;
            // Capture is a local world editor, not a UI owner. Deny new tool /
            // combat leases before they can inject fresh held-item input, and
            // mask raw mouse only inside this exact native world consumer.
            if(Blueprint.BlueprintHooks.Blocks(__instance) && !(host.Use.Building && host.Use.ActionValid))
            {__state=new Lease{Blueprint=Blueprint.BlueprintHooks.BeginHeld(__instance)};return;}
            if(ReferenceEquals(__instance,host.Player))
            {
                host.Combat?.Attack?.PrepareNatural();host.Combat?.Facing.Apply(__instance);
                if(Combat.HostPlacementAttack.Handles(__instance.HeldItem))
                {var placement=host.Combat?.Attack?.EnterPlacementUse(__instance);if(placement!=null)__state=new Lease{Placement=placement};}
            }
            if(host.Combat?.Use.Active??false){if(__state==null)__state=new Lease();__state.Combat=host.Combat.Use.Begin(__instance);return;}
            if(!host.Use.Active)return;if(__state==null)__state=new Lease();__state.Token=host.Use.Operation;__state.Generation=host.Use.NativeGeneration+1;host.Use.Begin(__instance);
        }
        private static void After(Player __instance,Lease __state)
        {try{SeedBoundary(__instance);host?.Use.End(__instance,__state?.Token??0,null,__state?.Generation??0);host?.Combat?.Use.End(__state?.Combat,null);if(ReferenceEquals(__instance,host?.Player))host.Combat?.Facing.Apply(__instance);}finally{__state?.Placement?.End();__state?.Blueprint?.End();}}
        private static Exception Final(Player __instance,Lease __state,Exception __exception)
        {try{if(__exception!=null){SeedBoundary(__instance);host?.Use.End(__instance,__state?.Token??0,__exception,__state?.Generation??0);host?.Combat?.Use.End(__state?.Combat,__exception);}}finally{__state?.Placement?.End();__state?.Blueprint?.End();}return __exception;}
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
        private struct PlantScope {internal HerbHarvest.HarvestReceipt Harvest;internal Action<Exception> Build;}
        [HarmonyPriority(Priority.First)]
        private static bool BeforePlant(Player __instance,out PlantScope __state)
        {
            __state=new PlantScope{Harvest=host?.Herbs.BeforePlant(__instance)??default(HerbHarvest.HarvestReceipt)};
            if(host==null || !ReferenceEquals(__instance,host.Player))return true;
            if(host.Use.Building){__state.Build=host.Use.BeginBuildConsumerScope(ToolBuildConsumer.Tiles);return host.Use.AllowBuildPlacement(false);}
            return !host.Use.InNativeUse || !host.Use.Is(ToolKind.Harvest) && !host.Use.Is(ToolKind.Seed) || host.Use.ActionValid;
        }
        private static bool BeforeWall(Player __instance)
        {return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Building || host.Use.AllowBuildPlacement(true);}
        private static bool BeforeTileChoice(Player __instance,bool __0,TileObject __1,int __2)
        {return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Building || host.Use.AllowBuildTileChoice(__0,__1,__2);}
        private static void BeforeBuildPreparation(Player __instance,out Action<bool> __state)
        {__state=ReferenceEquals(__instance,host?.Player)?host.Use.BeginBuildPreparation():null;}
        private static void AfterBuildPreparation(bool __result,Action<bool> __state){__state?.Invoke(__result);}
        private static Exception FinalBuildPreparation(Action<bool> __state,Exception __exception)
        {if(__exception!=null)__state?.Invoke(false);return __exception;}
        private static void AfterPlant(Player __instance,PlantScope __state){try{host?.Herbs.AfterPlant(__instance,__state.Harvest);}finally{__state.Build?.Invoke(null);}}
        private static Exception FinalPlant(PlantScope __state,Exception __exception){if(__exception!=null)__state.Build?.Invoke(__exception);return __exception;}
        private static bool BuildConsumer(Player p,ToolBuildConsumer kind,int x,int y,Item material=null,bool wall=false,bool animation=true)
        {return host==null || !ReferenceEquals(p,host.Player) || !host.Use.Building || host.Use.AllowBuildConsumerAt(kind,x,y,material,wall,animation);}
        private static bool BeforePound(Player __instance,int __3,int __4){return BuildConsumer(__instance,ToolBuildConsumer.Hammer,__3,__4);}
        private static bool BeforeHitWall(Player __instance,int __1,int __2){return BuildConsumer(__instance,ToolBuildConsumer.HittingWall,__1,__2);}
        private static bool BeforeBrush(Player __instance){return BuildConsumer(__instance,ToolBuildConsumer.Paintbrush,Player.tileTargetX,Player.tileTargetY);}
        private static bool BeforeRoller(Player __instance){return BuildConsumer(__instance,ToolBuildConsumer.PaintRoller,Player.tileTargetX,Player.tileTargetY);}
        private static bool BeforeScrape(Player __instance,int __0,int __1){return BuildConsumer(__instance,ToolBuildConsumer.PaintScraper,__0,__1);}
        private static bool BeforeScrapeMoss(Player __instance,int __0,int __1){return BuildConsumer(__instance,ToolBuildConsumer.LongMoss,__0,__1);}
        private static bool BeforePainting(Player __instance,int __0,int __1,bool __2,bool __3)
        {return BuildConsumer(__instance,__2?ToolBuildConsumer.PaintRoller:ToolBuildConsumer.Paintbrush,__0,__1,null,__2,__3);}
        private static bool BeforeApplyPaint(Player __instance,int __0,int __1,bool __2,bool __3,Item __4)
        {return BuildConsumer(__instance,ToolBuildConsumer.ApplyPaint,__0,__1,__4,__2,__3);}
        private static bool BeforeApplyCoating(Player __instance,int __0,int __1,bool __2,bool __3,Item __4)
        {return BuildConsumer(__instance,ToolBuildConsumer.ApplyCoating,__0,__1,__4,__2,__3);}
        [HarmonyPriority(Priority.First)]
        private static bool BeforeWiring(Player __instance,out Action<Exception> __state)
        {
            __state=null;if(host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Building)return true;
            __state=host.Use.BeginBuildConsumerScope(ToolBuildConsumer.Wiring);
            return host.Use.AllowBuildConsumer(ToolBuildConsumer.Wiring);
        }
        private static void AfterWiring(Action<Exception> __state){__state?.Invoke(null);}
        private static Exception FinalWiring(Action<Exception> __state,Exception __exception){if(__exception!=null)__state?.Invoke(__exception);return __exception;}
        private static bool Catch(Player __instance){return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Is(ToolKind.Capture) || host.Use.ActionValid;}
        private static bool Mine(Player __instance){return host==null || !ReferenceEquals(__instance,host.Player) || !host.Use.Is(ToolKind.Mining) || host.Use.ActionValid;}
    }
}
