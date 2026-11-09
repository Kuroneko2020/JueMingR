using System;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object emptyReceiptTools, emptyReceiptPreparation;
        private static bool emptyReceiptWatching;
        private static int emptyReceiptRetired, emptyReceiptRejected, emptyReceiptCompleted;
        private static long emptyReceiptToken, emptyReceiptClicks;
        private static uint emptyReceiptFirstLegal, emptyReceiptSuccessor;
        private static uint emptyReceiptFirstPaint;
        private static bool emptyReceiptReferenceWatching;
        private static byte emptyReceiptRed;
        private static byte emptyReceiptSuccessorColor;
        private static bool emptyReceiptTokenControl;
        private static int emptyReceiptInsideRejected, emptyReceiptWrongTokenRejected;
        private static long emptyReceiptHandoffLogged;
        private static void EmptyReceiptRefresh(object __instance)
        {
            if (!emptyReceiptWatching) return;
            var current = GetOptional(__instance, "activeBuild"); var next = GetOptional(__instance, "preparedBuild");
            if (current == null || next == null || !(bool)Get(current, "Checked") || (long)Get(next, "Generation") == emptyReceiptHandoffLogged) return;
            emptyReceiptHandoffLogged = (long)Get(next, "Generation");
            var a = Get(current, "Source"); var b = Get(next, "Source");
            var intent = Get(Get(emptyReceiptTools, "Use"), "Intent");
            Console.WriteLine("EmptyReceipt handoff tick=" + Main.GameUpdateCount + " settled=" + Get(current, "SettledConsumption") +
                " receipt=" + Get(emptyReceiptPreparation, "phase") + " sameSource=" + ReferenceEquals(Get(a, "Item"), Get(b, "Item")) +
                " sourceSlots=" + Get(a, "Slot") + "/" + Get(b, "Slot") + " dependencies/currentIntent=" + Get(current, "DependencySlots") + "/" +
                Get(next, "DependencySlots") + "/" + Get(intent, "MaterialSlots") + " supplyTypes=" + Get(Get(current, "Operation"), "SupplyType") + "/" +
                Get(Get(next, "Operation"), "SupplyType") + " sameBuildSource=" +
                __instance.GetType().GetMethod("SameBuildSource", F).Invoke(null, new[] { current, next, Get(intent, "MaterialSlots") }) +
                " sameOperation=" + __instance.GetType().GetMethod("SameBuildContinuationOperation", F).Invoke(null, new[] { Get(current, "Operation"), Get(next, "Operation") }) +
                " sameDependencies=" + __instance.GetType().GetMethod("SameBuildDependencies", F, null, new[] { current.GetType(), next.GetType() }, null).Invoke(null, new[] { current, next }));
        }
        private static void EmptyReceiptBeforeRetirement(object __instance, long __0)
        {
            if (!emptyReceiptWatching || emptyReceiptTokenControl || !(bool)Get(__instance, "Using")) return;
            emptyReceiptTokenControl = true;
            try
            {
                Require(!(bool)Call(__instance, "TryCompleteSettledEmptyUse", __0 + 1) && (bool)Get(__instance, "Using"),
                    "a different exact token is refused on the real Using receipt before positive retirement");
                emptyReceiptWrongTokenRejected++;
            }
            finally { emptyReceiptTokenControl = false; }
        }
        private static void EmptyReceiptInsidePaint(Player __instance, int __0, int __1, bool __2, bool __3, Item __4)
        {
            if (emptyReceiptWatching || emptyReceiptReferenceWatching)
                Console.WriteLine("EmptyReceipt native-before trajectory=" + (emptyReceiptReferenceWatching ? "original" : "automatic") +
                    " tick=" + Main.GameUpdateCount + " xy=" + __0 + "," + __1 + " wall=" + __2 + " applyAnimation=" + __3 +
                    " supply=" + __4.type + ":" + __4.stack + " time/animation=" + __instance.itemTime + "/" + __instance.itemAnimation +
                    " release/held=" + __instance.releaseUseItem + "/" + __instance.controlUseItem + " selected=" + __instance.selectedItem +
                    " override/buffer=" + __instance.selectedItemState.HasActiveOverride + "/" + __instance.selectedItemState.HasBufferedChange +
                    " 54=" + __instance.inventory[54].type + ":" + __instance.inventory[54].stack + " receipt=" + Get(emptyReceiptPreparation, "phase") +
                    " active/token=" + Get(Get(emptyReceiptTools, "Use"), "Active") + "/" + Get(Get(emptyReceiptTools, "Use"), "Operation"));
            if (!emptyReceiptWatching) return;
            var use = Get(emptyReceiptTools, "Use");
            if (!(bool)Get(use, "Building") || !(bool)Get(use, "InNativeUse")) return;
            var preparation = emptyReceiptPreparation;
            // Actual native call, before paint result/debit: no fabricated
            // phase, endpoints or completion flags are installed by this probe.
            Require(!(bool)Call(preparation, "TryCompleteSettledEmptyUse", (long)Get(use, "Operation")),
                "an in-progress original paint consumer cannot retire its unconfirmed receipt");
            emptyReceiptInsideRejected++;
        }
        private static void EmptyReceiptPaint(Player __instance, int __0, int __1)
        {
            if (!emptyReceiptWatching && !emptyReceiptReferenceWatching) return;
            Console.WriteLine("EmptyReceipt native-after trajectory=" + (emptyReceiptReferenceWatching ? "original" : "automatic") +
                " tick=" + Main.GameUpdateCount + " xy=" + __0 + "," + __1 + " color=" + Main.tile[__0, __1].color() +
                " time/animation=" + __instance.itemTime + "/" + __instance.itemAnimation + " source20/12/54=" +
                __instance.inventory[20].type + ":" + __instance.inventory[20].stack + "/" + __instance.inventory[12].type + ":" +
                __instance.inventory[12].stack + "/" + __instance.inventory[54].type + ":" + __instance.inventory[54].stack);
            if (__0 == 44 && __1 == 42 && Main.tile[44, 42].color() == emptyReceiptRed && emptyReceiptFirstPaint == 0)
                emptyReceiptFirstPaint = Main.GameUpdateCount;
            if (__0 == 46 && __1 == 42 &&
                Main.tile[46, 42].color() == emptyReceiptSuccessorColor && emptyReceiptSuccessor == 0)
                emptyReceiptSuccessor = Main.GameUpdateCount;
        }
        private static uint EmptyReceiptOriginalGap(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 1; var red = p.inventory[20];
            p.inventory[12].SetDefaults(ItemID.BluePaint); p.inventory[12].stack = 1;
            p.inventory[14].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[14];
            NativeToolsChecks.Tile(44, 42, TileID.Stone); NativeToolsChecks.Tile(46, 42, TileID.Stone);
            WorldGen.paintTile(44, 42, 0, false, false); WorldGen.paintTile(46, 42, 0, false, false);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = emptyReceiptRed },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = emptyReceiptSuccessorColor });
            Place(context, host, input, entry, 44, 42);
            var preparation = Get(host, "buildMaterialPreparation");
            var identity = new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "native-empty-receipt-reference",
                (long)Get(Get(tools, "Runtime"), "Generation"), 1);
            Call(preparation, "TryPrepare", new object[] { p, 20, 54, identity, -1 });
            Until(context, input, () => (bool)Get(preparation, "Ready"));
            // Same original preparation layout, handed to the native player
            // through real UI takeover. No automatic token or Ready predicate
            // defines the reference's earliest consumer opportunity.
            p.ToggleInv(); p.ToggleInv();
            Require(!(bool)Get(preparation, "HasWork") && ReferenceEquals(p.inventory[54], red), "native replay starts from the same real prepared red layout");
            emptyReceiptFirstPaint = emptyReceiptSuccessor = 0; emptyReceiptReferenceWatching = true;
            try
            {
                p.selectedItemState.Select(14);
                NativeQuickItemChecks.Until(() =>
                {
                    int x = Main.tile[44, 42].color() == emptyReceiptRed ? 46 : 44;
                    NativeToolExecutionChecks.Sample(context, input, new Microsoft.Xna.Framework.Vector2(x * 16 + 8, 42 * 16 + 8),
                        p.itemAnimation > 0 || p.releaseUseItem || brush.autoReuse);
                    NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime"); constructionInputTail();
                    return emptyReceiptSuccessor != 0;
                });
                Require(emptyReceiptFirstPaint != 0 && p.inventory[20].IsAir && p.inventory[12].IsAir && p.inventory[54].IsAir,
                    "original reference performs both genuine paint/debit consumers from the same single stacks");
                return unchecked(emptyReceiptSuccessor - emptyReceiptFirstPaint);
            }
            finally { emptyReceiptReferenceWatching = false; }
        }
        private static void EmptyReceiptRetired(object __instance, long __0, bool __result)
        {
            if (!emptyReceiptWatching || emptyReceiptTokenControl) return;
            if (!__result) { emptyReceiptRejected++; return; }
            emptyReceiptRetired++; if (emptyReceiptToken == 0) emptyReceiptToken = __0;
            var use = Get(emptyReceiptTools, "Use");
            Require(!(bool)Get(__instance, "HasWork") && (bool)Get(use, "Building") &&
                (long)Get(use, "Operation") == __0 && !(bool)Get(use, "InNativeUse"),
                "real settled-empty receipt retires after native return while its exact ToolUse still owns the natural tail");
            Require(Main.LocalPlayer.inventory[20].IsAir && Main.LocalPlayer.inventory[54].IsAir && Main.mouseItem.IsAir,
                "native last debit leaves actual source, priority slot and cursor Air");
            if (emptyReceiptRetired == 1) emptyReceiptClicks = (long)Get(__instance, "NativeClicks");
        }
        private static void EmptyReceiptCompleted(object __0)
        {
            if (emptyReceiptWatching && __0 != null && (long)Get(__0, "Token") == emptyReceiptToken) emptyReceiptCompleted++;
        }
        private static void ConstructionEmptyPaintReceipt(object context, object host, object tools, object input)
        {
            var preparation = Get(host, "buildMaterialPreparation");
            var patch = new HarmonyLib.Harmony("JueMingR.Tests.EmptyPaintReceipt");
            patch.Patch(preparation.GetType().GetMethod("TryCompleteSettledEmptyUse", F),
                prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptBeforeRetirement)),
                postfix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptRetired)));
            patch.Patch(host.GetType().GetMethod("EndAutomaticBuild", F),
                prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptCompleted)));
            patch.Patch(host.GetType().GetMethod("RefreshAutomaticBuild", F),
                prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptRefresh)));
            patch.Patch(typeof(Player).GetMethod("ApplyPaint", F),
                prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptInsidePaint)),
                postfix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(EmptyReceiptPaint)));
            emptyReceiptTools = tools; emptyReceiptPreparation = preparation;
            try
            {
                var p = Reset(context, host, tools, input);
                Require(p.inventory[54].IsAir && Main.mouseItem.IsAir && !Main.playerInventory, "empty receipt fixture begins with real empty priority/cursor");
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 1; byte color = p.inventory[20].paint;
                emptyReceiptRed = color;
                p.inventory[14].SetDefaults(ItemID.Paintbrush);
                // A lower-slot different paint makes real preparation of red
                // necessary; empty54 alone otherwise allows a direct bag use
                // with no receipt at all. The successor can then use that bag.
                p.inventory[12].SetDefaults(ItemID.BluePaint); p.inventory[12].stack = 1; emptyReceiptSuccessorColor = p.inventory[12].paint;
                NativeToolsChecks.Tile(44, 42, TileID.Stone);
                NativeToolsChecks.Tile(46, 42, TileID.Stone);
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = color },
                    new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = emptyReceiptSuccessorColor });
                Place(context, host, input, entry, 44, 42);
                emptyReceiptRetired = emptyReceiptRejected = emptyReceiptCompleted = 0; emptyReceiptToken = 0;
                emptyReceiptInsideRejected = emptyReceiptWrongTokenRejected = 0;
                emptyReceiptHandoffLogged = 0;
                emptyReceiptFirstLegal = emptyReceiptSuccessor = emptyReceiptFirstPaint = 0;
                emptyReceiptWatching = true; SetAutomatic(host, input, true);
                Until(context, input, () => emptyReceiptRetired == 1);
                Require(Main.tile[44, 42].color() == color && emptyReceiptCompleted == 0,
                    "real world paint and separate inventory debit reach fast retirement before Completed");
                Require(!(bool)Call(preparation, "TryCompleteSettledEmptyUse", emptyReceiptToken + 1),
                    "a different exact token cannot retire an already settled receipt");
                Until(context, input, () => emptyReceiptSuccessor != 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(emptyReceiptCompleted == 1 && emptyReceiptRetired == 1 && (long)Get(preparation, "NativeClicks") == emptyReceiptClicks &&
                    p.inventory[20].IsAir && p.inventory[12].IsAir && p.inventory[54].IsAir && Main.mouseItem.IsAir,
                    "one completion per old token; the native bag successor needs no receipt clicks or revived paint");
                Require(emptyReceiptInsideRejected >= 2 && emptyReceiptWrongTokenRejected >= 1,
                    "real native-in-progress and exact foreign-token boundaries both remain rejected");
                uint actualFirst = emptyReceiptFirstPaint, actualSuccessor = emptyReceiptSuccessor;
                emptyReceiptWatching = false;
                uint originalGap = EmptyReceiptOriginalGap(context, host, tools, input);
                emptyReceiptFirstLegal = unchecked(actualFirst + originalGap);
                Console.WriteLine("EmptyReceipt comparison first=" + actualFirst + " successor=" + actualSuccessor +
                    " actualGap=" + unchecked(actualSuccessor - actualFirst) + " originalGap=" + originalGap +
                    " extraGap=" + ((long)unchecked(actualSuccessor - actualFirst) - originalGap));
                Require(actualSuccessor == emptyReceiptFirstLegal,
                    "automatic successor gap equals the independent original native replay from the same real prepared supply");
                Console.WriteLine("PASS EmptyPaintReceipt retired=1 oldCompleted=1 reverseClicks=0 token=" + emptyReceiptToken +
                    " firstLegal=" + emptyReceiptFirstLegal + " successor=" + actualSuccessor + " originalGap=" + originalGap + " extraGap=0");
                // The existing real nonempty-priority trajectory must refuse
                // fast retirement and execute its original inverse return.
                emptyReceiptRetired = emptyReceiptRejected = 0;
                emptyReceiptWatching = true;
                ConstructionLastPaint(context, host, tools, input);
                Require(emptyReceiptRetired == 0 && emptyReceiptRejected > 0,
                    "nonempty original54 refuses fast retirement and retains real inverse return");
                p = Reset(context, host, tools, input);
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 2; var remaining = p.inventory[20];
                p.inventory[12].SetDefaults(ItemID.BluePaint); p.inventory[12].stack = 1; var untouched = p.inventory[12];
                p.inventory[14].SetDefaults(ItemID.Paintbrush); NativeToolsChecks.Tile(44, 42, TileID.Stone);
                entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = color });
                Place(context, host, input, entry, 44, 42);
                emptyReceiptRetired = emptyReceiptRejected = 0;
                long remainderClicks = (long)Get(preparation, "NativeClicks");
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(preparation, "HasWork") && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(emptyReceiptRetired == 0 && emptyReceiptRejected > 0 && ReferenceEquals(p.inventory[20], remaining) &&
                    remaining.stack == 1 && ReferenceEquals(p.inventory[12], untouched) && untouched.stack == 1 &&
                    p.inventory[54].IsAir && Main.mouseItem.IsAir && (long)Get(preparation, "NativeClicks") == remainderClicks + 4,
                    "a real remaining same-color stack refuses empty retirement and executes exactly two forward plus two inverse clicks");
                Console.WriteLine("PASS EmptyPaintReceipt remainder-control remaining=1 forwardClicks=2 inverseClicks=2 retired=0");
            }
            finally
            {
                emptyReceiptWatching = false; emptyReceiptTools = emptyReceiptPreparation = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyLib.HarmonyPatchType.All, patch.Id);
            }
        }
    }
}
