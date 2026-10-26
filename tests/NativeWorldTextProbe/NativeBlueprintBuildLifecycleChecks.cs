using System;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void AwaitPrepared(object context, object host, object input)
        {
            NativeQuickItemChecks.Until(() =>
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                return GetOptional(host, "preparedBuild") != null;
            });
        }
        private static void ProjectionLifecycle(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
            NativeQuickItemChecks.Until(() =>
            {
                Call(host, "UpdateProjectionWindow", new JueMingR.Platform.WorldTargets.WorldTargetView(38, 38, 16, 8, 0)); Call(host, "Poll");
                return GetOptional(Scene(host)[0], "Window") != null;
            });
            Call(host, "MoveProjection");
            Action<bool, int> point = (down, x) => Call(host, "AcceptWorldPointer", true, down, false, x, 42, Main.maxTilesX, Main.maxTilesY);
            point(false, 44); point(true, 44);
            Require(Get(host, "ProjectionMode").ToString() == "Moving", "real move selection takes the prepared original target");
            point(false, 45); point(true, 45);
            Until(context, input, () => Scene(host).Length == 0);
            Require(!Main.tile[44, 42].active() && Main.tile[45, 42].active() && p.inventory[12].stack == 1,
                "a committed real move invalidates the old prepared origin and spends once only at the new origin");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
            var dirt = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
            Place(context, host, input, dirt, 44, 42);
            for (int i = 0; i < 80; i++) Step(context, input);
            Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && Scene(host).Length == 1,
                "real newer permanent mask retires the covered old target without spending its prepared StoneBlock");
            p.inventory[13].SetDefaults(ItemID.DirtBlock); Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].type == TileID.Dirt && p.inventory[13].IsAir && p.inventory[12].stack == 2,
                "the surviving new instance independently resumes with its own actual material");

            foreach (string exit in new[] { "remove", "delete", "session" })
            {
                p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
                var removable = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(context, host, input, removable, 44, 42); SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
                if (exit == "remove") { Call(host, "RemoveProjection"); Call(host, "RemoveAllProjections"); }
                else if (exit == "delete") Require((bool)Call(host, "DeleteAsset", removable), "real reliable deletion is accepted after preparation");
                else { Main.gameMenu = true; Call(context, "UpdateRuntime"); Main.gameMenu = false; Call(context, "UpdateRuntime"); }
                for (int i = 0; i < 80; i++) Step(context, input);
                Require(Scene(host).Length == 0 && !Main.tile[44, 42].active() && p.inventory[12].stack == 2 &&
                    !(bool)Get(Get(tools, "Use"), "Active") && !(bool)Get(Get(Get(tools, "Items"), "Ownership"), "AnyProtected"),
                    "prepared " + exit + " owner cannot consume or keep source/watch/use references alive");
                Console.WriteLine("PASS BlueprintBuild prepared target exit=" + exit);
            }
            Console.WriteLine("PASS BlueprintBuild actual move, permanent overlapping mask, world removal, reliable template delete and session exit invalidate prepared targets before native consumption.");
        }
        private static void StaleSourcesAndTargets(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            // Publish a current observation without executing another player
            // use. The following native step must recheck this exact source.
            NativeQuickItemChecks.Until(() => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); return GetOptional(host, "preparedBuild") != null; });
            var original = p.inventory[12]; p.inventory[13] = original; p.inventory[12] = new Item(); p.inventory[12].SetDefaults(ItemID.DirtBlock);
            Step(context, input);
            Require(!Main.tile[44, 42].active() && original.stack == 2 && p.inventory[12].type == ItemID.DirtBlock && p.inventory[12].stack == 1,
                "source moved after frozen inventory cannot consume the old slot or a substituted item in the next real native use");
            Until(context, input, () => Scene(host).Length == 0);
            Require(original.stack == 1 && p.inventory[12].stack == 1, "new real observation resumes from the moved StoneBlock reference only");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            NativeQuickItemChecks.Until(() => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); return GetOptional(host, "preparedBuild") != null; });
            long uses = (long)Get(host, "BuildUsesStarted");
            Require(WorldGen.PlaceTile(44, 42, TileID.Stone, true, true), "independent manual native writer completes the target after planning");
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.inventory[12].stack == 2 && uses == (long)Get(host, "BuildUsesStarted"), "actual world change invalidates old candidate before consumption and naturally retires without a duplicate block");
        }
        private static void BackgroundAndManualProtection(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 80; i++) { Call(input, "BeginUpdate"); Call(context, "UpdateRuntime"); }
            Require(uses == (long)Get(host, "BuildUsesStarted") && !Main.tile[44, 42].active() && p.inventory[12].stack == 2,
                "outer updates without an actual gameplay entry never create a native build press");
            var foreground = Get(input, "foregroundWindow"); long generation = (long)Get(Get(tools, "Runtime"), "Generation");
            try
            {
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(2))); FocusHelper.IsSelectedApplication = false; p.mouseInterface = true;
                Until(context, input, () => Scene(host).Length == 0);
                Require(!(bool)Get(input, "IsFocused") && Main.tile[44, 42].active() && p.inventory[12].stack == 1 && generation == (long)Get(Get(tools, "Runtime"), "Generation"),
                    "real OS focus sampler permits original automatic build only on background gameplay steps, ignores stale Draw hover and preserves session identity");
            }
            finally { Set(input, "foregroundWindow", foreground); FocusHelper.IsSelectedApplication = true; p.mouseInterface = false; NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); }

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            p.inventory[11].SetDefaults(ItemID.Wood); p.inventory[11].stack = 3;
            p.selectedItemState.Select(12); p.selectedItemState.Update();
            NativeToolExecutionChecks.Sample(context, input, new Vector2(44 * 16 + 8, 42 * 16 + 8), true); NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
            Require(Main.tile[44, 42].active() && p.inventory[12].stack == 1 && p.itemAnimation > 0 && !(bool)Call(Get(tools, "Use"), "OwnsBuildMaterial", p.inventory[12]) &&
                !(bool)Call(tools, "BuildInventoryEligible", 12, p.inventory[12]) && (bool)Call(tools, "BuildInventoryEligible", 11, p.inventory[11]),
                "a real manual held block keeps its original active-material protection; only exact automatic lease sources receive the narrow exception");
            Console.WriteLine("PASS BlueprintBuild lifecycle: stale source reference, independent manual completion, unsampled outer gate, actual background native use and retained manual-source protection.");
        }
    }
}
