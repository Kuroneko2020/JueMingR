using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
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
