using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool movingChairWatch,movingChairLate,movingChairLookupFault,movingChairFaultBorrowed,movingChairFaultMovement,movingChairFaultMovementSeen;
        private static int movingChairMovement,movingChairMovementTurns,movingChairLateEntries,movingChairLateBaselines,movingChairLateWrites,movingChairLookupFailures;
        private static Exception movingChairLookupError,movingChairObservedError;
        private struct ChairGrappleState {internal bool Active,Left,Right;internal int Grapple;}
        private static void BeforeChairMovement(Player __instance,out int __state)
        {
            __state=__instance.direction;
            if(movingChairWatch && movingChairLookupFailures>0 && movingChairObservedError!=null && !movingChairFaultMovementSeen)
            {
                movingChairFaultMovementSeen=true;
                object use=Get(faultTools,"Use");
                movingChairFaultMovement=__instance.direction==-1 && !(bool)Get(use,"directionBorrowed") &&
                    (bool)Get(use,"borrowed") && (bool)Get(use,"cursorBorrowed");
            }
        }
        private static void ObserveChairMovement(Player __instance,int __state)
        {
            if(!movingChairWatch || !__instance.controlRight || __instance.direction!=1 || !(bool)Get(Get(faultTools,"Use"),"Building"))return;
            movingChairMovement++;if(__state==-1)movingChairMovementTurns++;
        }
        private static void EnterChairGrappleBranch(Player __instance,out ChairGrappleState __state)
        {
            __state=default(ChairGrappleState);
            if(!movingChairWatch || !movingChairLate || !(bool)Get(Get(faultTools,"Use"),"InNativeUse"))return;
            // Enter only the original StartActualUse grapple-facing branch at
            // this narrow seam, after neutral movement. The fixture never writes
            // facing; the original code must turn left to right before placing.
            __state=new ChairGrappleState{Active=true,Grapple=__instance.grappling[0],Left=__instance.controlLeft,Right=__instance.controlRight};
            movingChairLateEntries++;if(__instance.direction==-1)movingChairLateBaselines++;
            __instance.grappling[0]=0;__instance.controlRight=true;__instance.controlLeft=false;
        }
        private static Exception LeaveChairGrappleBranch(Player __instance,ChairGrappleState __state,Exception __exception)
        {
            if(__state.Active)
            {
                if(__exception==null && __instance.direction==1)movingChairLateWrites++;
                __instance.grappling[0]=__state.Grapple;__instance.controlLeft=__state.Left;__instance.controlRight=__state.Right;
            }
            return __exception;
        }
        [HarmonyAfter("JueMingR.Tools"),HarmonyPriority(Priority.Last)]
        private static void FailChairTargetLookup(Player __0)
        {
            if(!movingChairWatch || !movingChairLookupFault || !(bool)Get(Get(faultTools,"Use"),"Building"))return;
            object use=Get(faultTools,"Use");
            movingChairLookupFault=false;movingChairLookupFailures++;
            movingChairFaultBorrowed=__0.direction==1 && (bool)Get(use,"directionBorrowed") &&
                (bool)Get(use,"borrowed") && (bool)Get(use,"cursorBorrowed");
            // Throw after the production prefix borrowed direction. Its postfix
            // cannot run; only the production finalizer can release before the
            // original Update catch continues into HorizontalMovement.
            throw movingChairLookupError;
        }
        [HarmonyAfter("JueMingR.Tools"),HarmonyPriority(Priority.Last)]
        private static Exception ObserveChairLookupFault(Exception __exception)
        {
            if(movingChairWatch && __exception!=null)movingChairObservedError=__exception;
            return __exception;
        }
        private static void MovingFurnitureDirection(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.BlueprintMovingChair");
            patch.Patch(typeof(Player).GetMethod("HorizontalMovement",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(BeforeChairMovement)),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ObserveChairMovement)));
            patch.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(EnterChairGrappleBranch)),
                finalizer:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(LeaveChairGrappleBranch)));
            patch.Patch(typeof(Terraria.GameContent.SmartCursorHelper).GetMethod("SmartCursorLookup",F),
                prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FailChairTargetLookup)),
                finalizer:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ObserveChairLookupFault)));
            try
            {
                foreach(string scenario in new[]{"opposite-movement","same-movement","same-late-start","same-lookup-fault"})
                {
                    bool late=scenario=="same-late-start",lookupFault=scenario=="same-lookup-fault";
                    int capturedDirection=scenario=="opposite-movement"?-1:1;
                    var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.WoodenChair);p.inventory[12].stack=2;
                    Require(p.inventory[12].useTurn && WorldGen.PlaceObject(60,42,p.inventory[12].createTile,true,p.inventory[12].placeStyle,direction:capturedDirection),
                        "the directional source uses the actual vanilla useTurn chair and requested alternate");
                    var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),
                        ()=>Step(context,input),60,41,1,2,"移动定向椅子"+scenario);
                    Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);AwaitPrepared(context,host,input);
                    movingChairMovement=movingChairMovementTurns=movingChairLateEntries=movingChairLateBaselines=movingChairLateWrites=movingChairLookupFailures=0;
                    movingChairFaultBorrowed=movingChairFaultMovement=movingChairFaultMovementSeen=false;movingChairObservedError=null;
                    movingChairLookupError=new InvalidOperationException("fixture-smartcursor-after-direction-borrow");
                    movingChairLate=late;movingChairLookupFault=lookupFault;movingChairWatch=true;p.direction=-1;
                    float start=p.position.X;int firstDirection=0;long choices=(long)Get(host,"BuildNativeChoices");
                    NativeQuickItemChecks.Until(()=>
                    {
                        NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                        p.controlRight=!late;p.controlLeft=false;PlayerInput.Triggers.Current.Right=!late;PlayerInput.Triggers.Current.Left=false;
                        NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                        if(p.inventory[12].stack<2)
                        {
                            // Observe the first consuming Player.Update itself.
                            // Later idle movement must not repair a bad return
                            // direction before this assertion can see it.
                            firstDirection=p.direction;
                            Require(firstDirection==1 && p.inventory[12].stack==1 && (long)Get(host,"BuildNativeChoices")>choices,
                                "first actual chair consumption immediately returns the latest original facing: "+scenario+" direction="+firstDirection);
                            return true;
                        }
                        Call(context,"UpdateRuntime");NativeToolExecutionChecks.Outer(context,input,1);return false;
                    });
                    Require(Enumerable.Range(0,2).All(y=>Main.tile[44,41+y].active() && Main.tile[44,41+y].type==TileID.Chairs &&
                        Main.tile[44,41+y].frameX==Main.tile[60,41+y].frameX && Main.tile[44,41+y].frameY==Main.tile[60,41+y].frameY),
                        "the real consumer wrote both members in the captured direction and style: "+scenario);
                    Require(late?movingChairLateEntries>0 && movingChairLateBaselines==movingChairLateEntries && movingChairLateWrites==movingChairLateEntries:
                        p.position.X>start && movingChairMovement>0 && movingChairMovementTurns>0,
                        "the original movement or StartActualUse actually turns from its unborrowed left baseline: "+scenario);
                    if(lookupFault)Require(movingChairLookupFailures==1 && movingChairFaultBorrowed && movingChairFaultMovement &&
                        ReferenceEquals(movingChairObservedError,movingChairLookupError),
                        "production lookup finalizer returns the original fault and only direction before vanilla catches and continues moving");
                    movingChairWatch=false;p.controlRight=false;
                    Call(context,"UpdateRuntime");Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                    Require(p.inventory[12].stack==1,"full current furniture match retires without a second consumption: "+scenario);
                    Console.WriteLine("PASS BlueprintMovingChair scenario="+scenario+" nativeMovementTurns="+movingChairMovementTurns+
                        " nativeLateWrites="+movingChairLateWrites+" lookupFaults="+movingChairLookupFailures+
                        " completeCapturedDirection=true consumed=1 firstConsumptionReturnDirection="+firstDirection);
                }
            }
            finally
            {
                movingChairWatch=movingChairLate=movingChairLookupFault=false;movingChairLookupError=movingChairObservedError=null;
                Main.LocalPlayer.controlLeft=Main.LocalPlayer.controlRight=false;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
                SetAutomatic(host,input,false);
            }
        }
        private static int wrongNativeChoice, observedWrongChoice;
        private static TileObject wrongNativeResult;
        private static void ChangeNativeChoice(ref int __3, ref int __4)
        {
            if (wrongNativeChoice == 0 || faultTools == null || !(bool)Get(Get(faultTools, "Use"), "Building")) return;
            // Fault injection changes an input of the real original selector.
            // It does not synthesize a successful CanPlace result or write a
            // target. The production before-PlaceIt gate must reject the
            // independently selected, legal but wrong body.
            if (wrongNativeChoice == 1) __4 = -__4;
            else if (wrongNativeChoice == 2) __3++;
        }
        private static void ObserveNativeChoice(bool __result, TileObject __5)
        {
            if (wrongNativeChoice == 0 || faultTools == null || !(bool)Get(Get(faultTools, "Use"), "Building")) return;
            if (__result) { wrongNativeResult = __5; observedWrongChoice++; }
        }
        private static void WrongNativeFurnitureChoice(object context, object host, object tools, object input)
        {
            var method = typeof(TileObject).GetMethods(F).Single(m => m.Name == "CanPlace" && m.GetParameters().Length == 8);
            var patch = new Harmony("JueMingR.Tests.BlueprintWrongNativeChoice");
            patch.Patch(method, prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ChangeNativeChoice)),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ObserveNativeChoice)));
            try
            {
                foreach (int change in new[] { 1, 2 })
                {
                    var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
                    Require(WorldGen.PlaceObject(60, 42, p.inventory[12].createTile, true, p.inventory[12].placeStyle, direction: -1), "wrong-choice source uses the original left chair writer");
                    var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                        () => Step(context, input), 60, 41, 1, 2, "真实选择拒绝" + change);
                    Place(context, host, input, entry, 44, 41);
                    observedWrongChoice = 0; wrongNativeChoice = change;
                    long choices = (long)Get(host, "BuildNativeChoices");
                    SetAutomatic(host, input, true);
                    Until(context, input, () => observedWrongChoice > 0);
                    Require(wrongNativeResult.xCoord == 44 && wrongNativeResult.yCoord == 41 &&
                        (change == 1 ? wrongNativeResult.alternate != 0 : wrongNativeResult.style != p.inventory[12].placeStyle),
                        "original CanPlace independently returned the wrong alternate/direction or style at the same footprint: " + change);
                    Require(!Main.tile[44, 41].active() && !Main.tile[44, 42].active() && p.inventory[12].stack == 2 && Scene(host).Length == 1 &&
                        (long)Get(host, "BuildNativeChoices") == choices,
                        "real before-PlaceIt production gate rejects the wrong original choice without spending or falsely completing: " + change);
                    wrongNativeChoice = 0;
                    SetAutomatic(host, input, false); SetAutomatic(host, input, true);
                    Until(context, input, () => Scene(host).Length == 0);
                    Require(Main.tile[44, 41].active() && p.inventory[12].stack == 1 && (long)Get(host, "BuildNativeChoices") > choices,
                        "the same captured target succeeds from the actual correct native choice after the fault is removed: " + change);
                }
                EquivalentNativeAlternate(context, host, tools, input);
            }
            finally
            {
                wrongNativeChoice = 0;
                foreach (var target in patch.GetPatchedMethods().ToArray()) patch.Unpatch(target, HarmonyPatchType.All, patch.Id);
            }
        }
        private static void EquivalentNativeAlternate(object context, object host, object tools, object input)
        {
            wrongNativeChoice = 0;
            try
            {
                // 1993 is a directly consumed hanging bottle with the same
                // solid/platform frame alias mechanism. 5472's original
                // flexible selector is deliberately outside basic execution;
                // its alias is covered by the material writer oracle instead.
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(1993); p.inventory[12].stack = 2;
                Require(p.inventory[12].GetFlexibleTileWand() == null && p.inventory[12].tileWand <= 0,
                    "the alias execution positive uses an ordinary directly consumed item");
                Require(WorldGen.PlaceTile(60, 38, TileID.Platforms, true, true) && WorldGen.PlaceTile(44, 38, TileID.Platforms, true, true),
                    "equivalent-alternate hanging object uses original platform anchors");
                Require(WorldGen.PlaceObject(60, 39, p.inventory[12].createTile, true, p.inventory[12].placeStyle),
                    "equivalent-alternate source uses the original supported object selector and writer");
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                    () => Step(context, input), 60, 39, 1, 2, "原版同义支撑选择");
                Place(context, host, input, entry, 44, 39);
                observedWrongChoice = 0; wrongNativeChoice = 3; // Observe only; native inputs remain unchanged.
                long choices = (long)Get(host, "BuildNativeChoices");
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(observedWrongChoice > 0 && wrongNativeResult.alternate == 1 && Main.tile[44, 39].type == p.inventory[12].createTile &&
                    Main.tile[44, 39].active() && Main.tile[44, 40].active() && p.inventory[12].stack == 1 &&
                    (long)Get(host, "BuildNativeChoices") > choices,
                    "actual CanPlace platform alternate 1 is accepted as the same complete captured body as its solid-anchor frame alias");
            }
            finally
            {
                wrongNativeChoice = 0;
            }
        }
    }
}
