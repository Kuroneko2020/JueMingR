using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The source and the later gate fixtures use original world writers. The
    // mirrored destination is written only by production Build -> ToolUse ->
    // natural Player.Update/ItemCheck, including the hammer's actual cycle.
    // Trusted CPU pointer samples exercise Host transactions; no GPU, desktop
    // input, ordinary game startup or multiplayer result is implied.
    internal static class NativeBlueprintMirrorConsumerChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool watchNative;
        private static int nativeItemChecks, nativePounds;

        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var input = Get(context, "Input");
            var oldLibrary = (LibraryState)Get(host, "Library");
            var oldHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var oldWorld = Main.ActiveWorldFileData; bool oldMenu = Main.gameMenu, oldAuto = (bool)Get(host, "AutoBuildEnabled");
            string root = Path.Combine(Terraria.Program.SavePath, "blueprint-mirror-consumers-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(root, "templates")));
            var history = new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(root, "instances", scope, "scene.bin"), int.MaxValue, true, ".schema1-original"),
                (file, value) => { if (value.SourceVersion == 1) ((AtomicFileDocument)file).RetainLoadedSource(); });
            var patch = new Harmony("JueMingR.Tests.BlueprintMirrorConsumers");
            var lighting = Lighting.Mode;
            IDisposable world = null;
            Action observe = () => Observe(context, host, input);
            Action step = () => Build("Step", context, input);
            try
            {
                NativeToolExecutionChecks.Initialize(context, null);
                Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
                Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
                foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning", "NotifyProgressionEvent" })
                    patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintMirrorConsumerChecks), nameof(SkipOutlet)));
                patch.Patch(typeof(Player).GetMethod("ItemCheck", F),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintMirrorConsumerChecks), nameof(ItemChecked)));
                patch.Patch(typeof(Player).GetMethod("ItemCheck_UseMiningTools_TryPoundingTile", F),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintMirrorConsumerChecks), nameof(Pounded)));

                Main.gameMenu = true; Call(context, "UpdateRuntime");
                world = NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160);
                Set(host, "<Library>k__BackingField", library); Set(host, "projectionHistory", history);
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "world.wld"), false) { UniqueId = Guid.NewGuid() };
                Main.gameMenu = false;
                NativeQuickItemChecks.Until(() => { observe(); return (bool)Get(host, "ControlsEnabled") && (bool)Get(host, "ProjectionPersistenceReady"); });

                var player = Reset(context, host, tools, input);
                Stock(player);
                Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true, style: 0, direction: 1), "actual right wooden chair has the original floor anchor");
                Require(WorldGen.PlaceTile(63, 42, TileID.Stone, mute: true, forced: true) && WorldGen.SlopeTile(63, 42, 1, true, true),
                    "asymmetric source stone has the actual original slope-one state");
                Require(Main.tile[60, 41].active() && Main.tile[60, 42].active() && Main.tile[60, 41].frameX == 18 &&
                    Main.tile[60, 42].frameX == 18 && Main.tile[60, 41].frameY == 0 && Main.tile[60, 42].frameY == 18 && Main.tile[63, 42].slope() == 1,
                    "independent original source facts are a complete right chair and a separated slope, before capture");
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, step, 60, 41, 4, 2, "镜像真实施工消费者");
                RequireSavedSource(Open(library, entry));
                MirrorAndBuild(context, host, tools, input, entry, player, observe, step);
                UnknownGate(context, host, tools, input, entry, observe, step);
                MatchedTrim(context, host, tools, input, entry, observe, step);
                RequireSavedSource(Open(library, entry));
                AsyncMirrorOverlap(context, host, tools, input, library, observe);
                Console.WriteLine("PASS BlueprintMirrorConsumers asset=" + entry.AssetId + " revision=" + entry.ContentRevision +
                    " actualCaptureSaveOpen=true constructionHostMirror=1 materials=one-chair+one-stone naturalItemChecks=" + nativeItemChecks +
                    " naturalPounds=" + nativePounds + "; opposite native chair frames, asymmetric slope-two, exact consumption/retirement, old prepared target refusal, Unknown/Match mirror gates and partial-Match trim keep AutoBuild preference. Controlled CPU original consumers; GPU/OS/ordinary load/multiplayer/FPS NOT_RUN.");
            }
            finally
            {
                watchNative = false;
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                Main.gameMenu = true; Call(context, "UpdateRuntime"); history.Stop(3000); library.Stop(3000);
                Set(host, "<Library>k__BackingField", oldLibrary); Set(host, "projectionHistory", oldHistory);
                world?.Dispose(); Main.ActiveWorldFileData = oldWorld; Main.gameMenu = oldMenu;
                if (!oldMenu) observe();
                Call(host, "SetAutoBuild", oldAuto); Call(host, "SetMaterialViewDemand", false);
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                Lighting.Clear(); Lighting.Mode = lighting;
            }
        }

        private static void MirrorAndBuild(object context, object host, object tools, object input, LibraryEntry entry,
            Player player, Action observe, Action step)
        {
            var original = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 42, 41, step);
            long generation = ((BlueprintProjectionIdentity)Get(original, "Identity")).ProjectionGeneration;
            Load(host, original, observe);
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", true);
            Build("AwaitPrepared", context, host, input);
            var stale = Get(host, "preparedBuild");
            Require(!Main.tile[42, 41].active() && !Main.tile[42, 42].active() && !Main.tile[45, 42].active(),
                "real original-view planning has not yet entered a native destination writer");
            Request(host, input, "MirrorProjection");
            Require(GetOptional(host, "preparedBuild") == null && (bool)Get(host, "AutoBuildEnabled"),
                "entering Mirror cancels the actual old prepared target and preserves the enabled preference");
            Pointer(host, false, 42, 41); Pointer(host, true, 42, 41);
            NativeQuickItemChecks.Until(() => { observe(); return !(bool)Get(host, "ProjectionEditing"); });
            var mirrored = Scene(host).Single(p => ((BlueprintProjectionIdentity)Get(p, "Identity")).ProjectionGeneration == generation);
            Require((bool)Get(mirrored, "MirroredHorizontally") && (int)Get(mirrored, "OriginX") == 42 && (int)Get(mirrored, "OriginY") == 41 &&
                (bool)Get(host, "AutoBuildEnabled") && !ReferenceEquals(stale, GetOptional(host, "preparedBuild")),
                "one real Host Mirror publishes the same fixed instance and drops its old prepared identity");
            var materials = Materials(host, generation, observe);
            Require(materials.Total == 2 && materials.Rows.Count == 2 &&
                materials.Rows.Single(r => r.ItemType == ItemID.WoodenChair).Required == 1 &&
                materials.Rows.Single(r => r.ItemType == ItemID.WoodenChair).Held == 4 &&
                materials.Rows.Single(r => r.ItemType == ItemID.StoneBlock).Required == 1 &&
                materials.Rows.Single(r => r.ItemType == ItemID.StoneBlock).Held == 5 &&
                !materials.Rows.Any(r => r.ItemType == ItemID.WoodenHammer),
                "real mirrored-world materials count one complete original chair and one stone, with no hammer consumption");
            Require(player.inventory[12].stack == 4 && player.inventory[13].stack == 5,
                "mirror and real material observation alone consume no original inventory");
            // Keep facing the original right direction. Only the production
            // effective target may lend left-facing to the actual chair use.
            player.direction = 1;
            long unknown = (long)Get(host, "BuildUnknownResults"), choices = (long)Get(host, "BuildNativeChoices");
            nativeItemChecks = nativePounds = 0; watchNative = true;
            try
            {
                step();
                Require((!Main.tile[42, 42].active() || Main.tile[42, 42].type != TileID.Chairs) &&
                    (!Main.tile[45, 42].active() || Main.tile[45, 42].type != TileID.Stone),
                    "the first actual Player.Update cannot consume either old unmirrored destination");
                Build("Until", context, input, (Func<bool>)(() => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active")));
            }
            finally { watchNative = false; }
            Require(Main.tile[42, 42].active() && Main.tile[42, 42].type == TileID.Stone && Main.tile[42, 42].slope() == 2 && !Main.tile[42, 42].halfBrick() &&
                Main.tile[45, 41].active() && Main.tile[45, 42].active() && Main.tile[45, 41].type == TileID.Chairs && Main.tile[45, 42].type == TileID.Chairs &&
                Main.tile[45, 41].frameX == 0 && Main.tile[45, 42].frameX == 0 && Main.tile[45, 41].frameY == 0 && Main.tile[45, 42].frameY == 18,
                "original native consumers independently produce left-chair frames 0/0,18 at the opposite end and slope-two stone");
            Require(!Main.tile[42, 41].active() && player.inventory[12].type == ItemID.WoodenChair && player.inventory[12].stack == 3 &&
                player.inventory[13].type == ItemID.StoneBlock && player.inventory[13].stack == 4 &&
                player.inventory[14].type == ItemID.WoodenHammer && player.inventory[14].stack == 1 && nativeItemChecks > 0 && nativePounds > 0 &&
                (long)Get(host, "BuildNativeChoices") > choices && (long)Get(host, "BuildUnknownResults") == unknown && (bool)Get(host, "AutoBuildEnabled"),
                "natural ItemCheck and pounding spend exactly one chair/stone, preserve the hammer/preference and retire by complete Match without Unknown");
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 20; i++) step();
            Require((long)Get(host, "BuildUsesStarted") == uses && player.inventory[12].stack == 3 && player.inventory[13].stack == 4,
                "retired mirrored work cannot replay either body or the former prepared candidate");
        }

        private static void UnknownGate(object context, object host, object tools, object input, LibraryEntry entry, Action observe, Action step)
        {
            var player = Reset(context, host, tools, input); Stock(player);
            // Readable raw Tile with an invalid native furniture identity is
            // the established original-source Unknown mechanism, not a bit
            // written into the match ledger or a fabricated profile failure.
            var invalid = Main.tile[45, 42]; invalid.active(true); invalid.type = 658; invalid.frameX = invalid.frameY = Int16.MaxValue;
            var target = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 42, 41, step);
            AwaitProof(target, observe, false, true); Load(host, target, observe);
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", true);
            long uses = (long)Get(host, "BuildUsesStarted");
            Request(host, input, "MirrorProjection"); Pointer(host, false, 42, 41); Pointer(host, true, 42, 41);
            for (int i = 0; i < 12; i++) observe();
            Require((bool)Get(host, "ProjectionEditing") && !(bool)Get(target, "MirroredHorizontally") && Scene(host).Contains(target) &&
                GetOptional(host, "ProjectionCandidate") == null && (bool)Get(host, "AutoBuildEnabled") &&
                player.inventory[12].stack == 4 && player.inventory[13].stack == 5 && (long)Get(host, "BuildUsesStarted") == uses,
                "actual Unknown proof cannot publish Mirror or consume inventory while the exact request remains checking");
            Call(host, "CancelProjection");
        }

        private static void MatchedTrim(object context, object host, object tools, object input, LibraryEntry entry, Action observe, Action step)
        {
            var player = Reset(context, host, tools, input); Stock(player);
            Require(WorldGen.PlaceObject(42, 42, TileID.Chairs, true, style: 0, direction: 1),
                "independent original writer establishes only the complete right chair before the second instance");
            var target = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 42, 41, step);
            AwaitProof(target, observe, true, false); Load(host, target, observe);
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", true);
            var remaining = Materials(host, ((BlueprintProjectionIdentity)Get(target, "Identity")).ProjectionGeneration, observe);
            Require(remaining.Total == 1 && remaining.Rows.Single(r => r.Required > 0).ItemType == ItemID.StoneBlock &&
                remaining.Rows.Single(r => r.Required > 0).Required == 1,
                "real partial-Match material contribution removes the complete chair and retains exactly one missing stone");
            Request(host, input, "MirrorProjection"); Pointer(host, false, 42, 41); Pointer(host, true, 42, 41);
            NativeQuickItemChecks.Until(() => { observe(); return !(bool)Get(host, "ProjectionEditing"); });
            Require(Scene(host).Contains(target) && !(bool)Get(target, "MirroredHorizontally") && (bool)Get(host, "AutoBuildEnabled"),
                "current partial Match rejects actual Mirror and preserves the original instance and automatic preference");
            Build("AwaitPrepared", context, host, input);
            Request(host, input, "TrimProjection"); Pointer(host, false, 45, 42); Pointer(host, true, 45, 42);
            NativeQuickItemChecks.Until(() => { observe(); return Get(host, "ProjectionMode").ToString() == "TrimReady"; });
            Require((bool)Get(host, "AutoBuildEnabled") && GetOptional(host, "preparedBuild") == null,
                "partial Match still admits real Trim while cancelling the missing-stone candidate and retaining the preference");
            Pointer(host, false, 45, 42); Pointer(host, true, 45, 42); Pointer(host, false, 45, 42);
            NativeQuickItemChecks.Until(() => { observe(); return (bool)Get(host, "CanInvokeConfirmTrim"); });
            Require(((BlueprintProjectionMask)Get(target, "Mask")).RemovedLayerCount == 0, "released real trim is still only a preview");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "ConfirmTrimProjection");
            // A control release belongs to this mapping. Observe samples the
            // next mapping, so first finish the real Host input arbitration.
            Call(host, "ProcessInput");
            Require((bool)Get(host, "trimFinalizing") && ((BlueprintProjectionMask)Get(target, "Mask")).RemovedLayerCount == 0,
                "one current-mapping confirmation accepts Freeze without publishing from the command itself");
            NativeQuickItemChecks.Until(() => { observe(); Call(host,"ProcessInput"); return !Scene(host).Contains(target); });
            Require(((BlueprintProjectionMask)Get(target, "Mask")).RemovedLayerCount == 1 && (bool)Get(host, "AutoBuildEnabled"),
                "real confirm trims the one ordinary source layer and remaining full chair Match naturally retires with preference enabled");
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 20; i++) step();
            Require(!Main.tile[45, 42].active() && Main.tile[42, 41].frameX == 18 && Main.tile[42, 42].frameX == 18 &&
                player.inventory[12].stack == 4 && player.inventory[13].stack == 5 && (long)Get(host, "BuildUsesStarted") == uses,
                "trimmed old stone candidate cannot consume after retirement; the independent built chair and inventory remain exact");
        }

        private static void AsyncMirrorOverlap(object context, object host, object tools, object input, LibraryState library, Action observe)
        {
            var player = Reset(context, host, tools, input);
            Require(!(bool)Get(host, "AutoBuildEnabled"), "asynchronous overlap fixture keeps automatic construction OFF");
            Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true, style: 0, direction: 1), "overlap source contains a complete original right chair");
            foreach (int y in new[] { 41, 42 })
            {
                WorldGen.PlaceWall(60, y, 7, true); WorldGen.PlaceWire(60, y); WorldGen.PlaceWire2(60, y); WorldGen.PlaceActuator(60, y);
            }
            Liquid(60, 41, 67, 0); Liquid(60, 42, 73, 2);
            Require(WorldGen.PlaceTile(62, 42, TileID.Stone, mute: true, forced: true), "transparent-hole witness is an original stone placement");
            WorldGen.PlaceWall(62, 42, 7, true); WorldGen.PlaceWire2(64, 42);
            var oldEntry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, observe, 60, 41, 5, 2, "镜像重叠旧对象");
            var oldRaw = Open(library, oldEntry);
            Require(oldRaw.Summary.Width == 5 && oldRaw.Summary.Height == 2 && oldRaw.Cells.Count == 4 && oldRaw.Objects.Count == 1 && oldRaw.Objects[0].Reliable &&
                oldRaw.Cells.Single(c => c.X == 0 && c.Y == 0).Layers == (CaptureLayer)31 && oldRaw.Cells.Single(c => c.X == 0 && c.Y == 1).Layers == (CaptureLayer)31 &&
                oldRaw.Cells.Single(c => c.X == 2 && c.Y == 1).TileType == TileID.Stone && oldRaw.Cells.Single(c => c.X == 2 && c.Y == 1).WallType == 7 &&
                oldRaw.Cells.Single(c => c.X == 4 && c.Y == 1).Layers == CaptureLayer.Wire && oldRaw.Cells.Single(c => c.X == 4 && c.Y == 1).Wires == 2,
                "real old save/open supplies the literal 5x2 chair/layers, center stone-wall and blue-wire endpoint");
            Require(WorldGen.PlaceTile(70, 42, TileID.Dirt, mute: true, forced: true), "incoming asymmetric body is an original dirt placement");
            WorldGen.PlaceWall(70, 42, 7, true); WorldGen.PlaceWire(70, 42); WorldGen.PlaceWire2(71, 42); WorldGen.PlaceWire3(74, 42);
            var newEntry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, observe, 70, 42, 5, 1, "镜像重叠新内容");
            var newRaw = Open(library, newEntry);
            Require(newRaw.Summary.Width == 5 && newRaw.Summary.Height == 1 && newRaw.Cells.Count == 3 && newRaw.Objects.Count == 0 &&
                newRaw.Cells.Single(c => c.X == 0).Layers == (CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Wire) &&
                newRaw.Cells.Single(c => c.X == 0).TileType == TileID.Dirt && newRaw.Cells.Single(c => c.X == 0).WallType == 7 && newRaw.Cells.Single(c => c.X == 0).Wires == 1 &&
                newRaw.Cells.Single(c => c.X == 1).Layers == CaptureLayer.Wire && newRaw.Cells.Single(c => c.X == 1).Wires == 2 &&
                newRaw.Cells.Single(c => c.X == 4).Layers == CaptureLayer.Wire && newRaw.Cells.Single(c => c.X == 4).Wires == 4,
                "real incoming save/open is the literal asymmetric Dirt-wall-red, blue, hole, hole, green source row");

            string worldBefore = NativeBlueprintProjectionVisualChecks.Snapshot(100, 50, 35, 2);
            string inventoryBefore = String.Join(",", player.inventory.Select(i => i.type + ":" + i.stack));
            long uses = (long)Get(host, "BuildUsesStarted");
            var old = NativeBlueprintWorldMatchChecks.Place(host, input, oldEntry, 100, 50, observe);
            var third = NativeBlueprintWorldMatchChecks.Place(host, input, oldEntry, 130, 50, observe);
            var thirdMask = Get(third, "Mask"); long thirdOrder = (long)Get(third, "CommitOrder");
            old = MirrorReceipt(context, host, input, library, old, 100, 50, observe, receipt =>
                Require(receipt.Masks.Count == 0, "the opponent first enters its real mirrored pose without an unrelated overlap"));
            long oldId = ((BlueprintProjectionIdentity)Get(old, "Identity")).ProjectionGeneration;
            var incoming = NativeBlueprintWorldMatchChecks.Place(host, input, newEntry, 100, 51, observe);
            long incomingId = ((BlueprintProjectionIdentity)Get(incoming, "Identity")).ProjectionGeneration;
            Require(((BlueprintProjectionMask)Get(old, "Mask")).RemovedLayerCount == 0 && ((BlueprintProjectionMask)Get(incoming, "Mask")).RemovedLayerCount == 0,
                "the incoming normal pose meets only different layers, so neither initial instance is erased");

            // Hand-listed world/source oracle, not a view-derived answer:
            // mirrored old source (0,0)/(0,1) -> (104,50)/(104,51);
            // old (2,1) -> (102,51), old (4,1) -> (100,51).
            // mirrored incoming source 0 -> (104,51), 1 -> (103,51),
            // 4 -> (100,51); (102,51) is an actual transparent source hole.
            incoming = MirrorReceipt(context, host, input, library, incoming, 100, 51, observe, receipt =>
            {
                Require(receipt.Request.Candidate.MirroredHorizontally && receipt.Request.Previous.Count == 1 &&
                    receipt.Request.Previous[0].Identity.ProjectionGeneration == oldId && receipt.Request.Previous[0].MirroredHorizontally &&
                    receipt.Request.Previous.All(p => p.Identity.ProjectionGeneration != incomingId) && receipt.Masks.Count == 1 &&
                    receipt.Masks.ContainsKey(oldId) && !receipt.Masks.ContainsKey(incomingId) && receipt.RetiredInstanceIds.Count == 0,
                    "complete real worker receipt uses both mirrored poses, excludes self and preserves the unrelated third instance");
                Require(((BlueprintProjectionMask)Get(old, "Mask")).RemovedLayerCount == 0 && !(bool)Get(Scene(host).Single(p =>
                    ((BlueprintProjectionIdentity)Get(p, "Identity")).ProjectionGeneration == incomingId), "MirroredHorizontally"),
                    "completed worker reply has not prematurely changed either old mask or incoming scene pose");
                RequireOverlapMask(receipt.Masks[oldId], oldRaw);
            });
            var permanent = (BlueprintProjectionMask)Get(old, "Mask"); RequireOverlapMask(permanent, oldRaw);
            Require(((BlueprintProjectionMask)Get(incoming, "Mask")).RemovedLayerCount == 0 && (long)Get(incoming, "TotalLayerCount") == 5 &&
                ReferenceEquals(Scene(host).Last(), incoming) && (long)Get(incoming, "CommitOrder") > (long)Get(old, "CommitOrder") &&
                ReferenceEquals(Get(third, "Mask"), thirdMask) && (long)Get(third, "CommitOrder") == thirdOrder && !(bool)Get(third, "MirroredHorizontally"),
                "Host atomic application keeps all incoming layers, gives it latest order and leaves the same-template third reference exact");

            incoming = MirrorReceipt(context, host, input, library, incoming, 104, 51, observe, receipt =>
                Require(receipt.Masks.Count == 0, "cover's second real mirror adds no same-layer overlap and supplies no undo"));
            Require(ReferenceEquals(Get(old, "Mask"), permanent), "mirroring the cover away retains the exact committed permanent mask");
            Request(host, input, "RemoveProjection"); Pointer(host, false, 100, 51); Pointer(host, true, 100, 51);
            Require(!Scene(host).Contains(incoming) && Scene(host).Contains(old) && Scene(host).Contains(third) && ReferenceEquals(Get(old, "Mask"), permanent),
                "real remove of the latest cover cannot restore the old chair or erase another instance");
            old = MirrorReceipt(context, host, input, library, old, 104, 50, observe, receipt =>
                Require(receipt.Masks.Count == 0, "old instance's second mirror uses its remaining layers without restoring object Tile"));
            Require(!(bool)Get(old, "MirroredHorizontally") && ReferenceEquals(Get(old, "Mask"), permanent),
                "the old source mask remains identical when its own pose is mirrored again");
            RequireOverlapMask(permanent, oldRaw);
            Require(Scene(host).Length == 2 && Scene(host).Contains(third) && ReferenceEquals(Get(third, "Mask"), thirdMask) &&
                ((BlueprintProjectionMask)thirdMask).RemovedLayerCount == 0 && (long)Get(third, "CommitOrder") == thirdOrder &&
                NativeBlueprintProjectionVisualChecks.Snapshot(100, 50, 35, 2) == worldBefore &&
                String.Join(",", player.inventory.Select(i => i.type + ":" + i.stack)) == inventoryBefore &&
                !(bool)Get(host, "AutoBuildEnabled") && (long)Get(host, "BuildUsesStarted") == uses,
                "all asynchronous mirror/remove operations preserve the third instance, real finite world, inventory and OFF construction preference");
            Call(host, "ClearProjection");
            Console.WriteLine("PASS BlueprintMirrorOverlap actual capture/save/open assets=" + oldEntry.AssetId + "," + newEntry.AssetId +
                "; real mirrored opponent + incoming Mirror -> complete worker receipt before Host atomic publication; source(0,0)/(0,1) Tile removal plus source(0,1) wall/red only=4, world(104,50) outer mixed layers and world(102,51) transparent hole preserved; self/order/third exact; cover second mirror/remove and old second mirror never revive permanent source mask. AutoBuild OFF; CPU original Host/worker evidence only.");
        }

        private static object MirrorReceipt(object context, object host, object input, LibraryState library, object target,
            int pickX, int pickY, Action observe, Action<BlueprintProjectionCommitReceipt> beforeApply)
        {
            NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(96, 48, 44, 8, 0)); observe(); return GetOptional(target, "Window") != null; });
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, target);
            long id = ((BlueprintProjectionIdentity)Get(target, "Identity")).ProjectionGeneration;
            long revision = (long)Get(host, "projectionSceneRevision"), latest = Scene(host).Max(p => (long)Get(p, "CommitOrder"));
            var mask = Get(target, "Mask"); bool mirrored = (bool)Get(target, "MirroredHorizontally");
            Request(host, input, "MirrorProjection"); Pointer(host, false, pickX, pickY); Pointer(host, true, pickX, pickY);
            Require(Get(host, "ProjectionMode").ToString() == "Committing", "warm real mirror click queues its actual asynchronous worker transaction; actual=" + Get(host, "ProjectionMode"));
            var worker = Get(library, "worker"); var gate = Get(worker, "gate"); BlueprintProjectionCommitReceipt complete = null;
            // Do not Poll/update Host during this wait. Read, but never take or
            // replace, its real fully published worker reply under its owner lock.
            NativeQuickItemChecks.Until(() => { lock (gate) complete = GetOptional(worker, "projectionCommitReceipt") as BlueprintProjectionCommitReceipt; return complete != null; });
            Require(complete.Succeeded && complete.Request.Kind == BlueprintProjectionEditKind.Mirror && complete.Request.Candidate.Identity.ProjectionGeneration == id &&
                complete.Request.Candidate.MirroredHorizontally != mirrored && Scene(host).Contains(target) && ReferenceEquals(Get(target, "Mask"), mask) &&
                (bool)Get(target, "MirroredHorizontally") == mirrored && (long)Get(host, "projectionSceneRevision") == revision && Scene(host).Max(p => (long)Get(p, "CommitOrder")) == latest,
                "real complete receipt leaves scene masks, pose, order and revision unchanged until the Host consumes it: " + complete.Error);
            beforeApply(complete);
            NativeQuickItemChecks.Until(() => { observe(); return !(bool)Get(host, "ProjectionEditing"); });
            var result = Scene(host).Single(p => ((BlueprintProjectionIdentity)Get(p, "Identity")).ProjectionGeneration == id);
            Require((bool)Get(result, "MirroredHorizontally") != mirrored && (long)Get(host, "projectionSceneRevision") == revision + 1 &&
                (long)Get(result, "CommitOrder") > latest && ReferenceEquals(Scene(host).Last(), result),
                "the same Host consumes the retained real receipt and atomically publishes one new scene/order");
            return result;
        }

        private static void RequireOverlapMask(BlueprintProjectionMask mask, BlueprintProjectionWindow raw)
        {
            // These are actual committed source cells under inspection. All
            // expected bits and their world positions above are explicit literals.
            var outer = mask.Apply(raw.Cells.Single(c => c.X == 0 && c.Y == 0));
            var touched = mask.Apply(raw.Cells.Single(c => c.X == 0 && c.Y == 1));
            var hole = mask.Apply(raw.Cells.Single(c => c.X == 2 && c.Y == 1));
            var endpoint = mask.Apply(raw.Cells.Single(c => c.X == 4 && c.Y == 1));
            Require(mask.RemovedLayerCount == 4 && mask.RemovedConstructionLayerCount == 4 && mask.ObjectCount == 1 && mask.CellCount == 1 &&
                outer.Layers == (CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator | CaptureLayer.Liquid) && outer.ObjectId == 0 &&
                outer.WallType == 7 && outer.Wires == 3 && outer.Actuator && outer.LiquidAmount == 67 && outer.LiquidKind == 0 &&
                touched.Layers == (CaptureLayer.Wire | CaptureLayer.Actuator | CaptureLayer.Liquid) && touched.ObjectId == 0 && touched.Wires == 2 &&
                touched.Actuator && touched.LiquidAmount == 73 && touched.LiquidKind == 2 &&
                hole.Layers == (CaptureLayer.Tile | CaptureLayer.Wall) && hole.TileType == TileID.Stone && hole.WallType == 7 &&
                endpoint.Layers == CaptureLayer.Wire && endpoint.Wires == 2,
                "literal overlap oracle: whole two-member chair Tile plus touched wall/red removed; outer wall/two wires/liquid/actuator and transparent stone-wall/blue endpoint stay; removed=" + mask.RemovedLayerCount +
                " outer=" + outer.Layers + "/wall" + outer.WallType + "/wire" + outer.Wires + "/act" + outer.Actuator + "/liquid" + outer.LiquidAmount + "," + outer.LiquidKind +
                " touched=" + touched.Layers + "/wire" + touched.Wires + "/act" + touched.Actuator + "/liquid" + touched.LiquidAmount + "," + touched.LiquidKind +
                " hole=" + hole.Layers + "/tile" + hole.TileType + "/wall" + hole.WallType + " endpoint=" + endpoint.Layers + "/wire" + endpoint.Wires);
        }
        private static void Liquid(int x, int y, byte amount, byte kind)
        { typeof(NativeBlueprintWorldMatchChecks).GetMethod("NativeLiquidStore", F).Invoke(null, new object[] { x, y, amount, kind }); }

        private static void RequireSavedSource(BlueprintProjectionWindow saved)
        {
            string detail = "sourceSummary=" + saved.Summary.Left + "," + saved.Summary.Top + "/" + saved.Summary.Width + "x" + saved.Summary.Height +
                " objects=[" + String.Join("; ", saved.Objects.Select(o => "id=" + o.Id + " type=" + o.Type + " xy=" + o.X + "," + o.Y + " size=" + o.Width + "x" + o.Height +
                    " style=" + o.Style + " alternate=" + o.Alternate + " placement=" + o.PlacementStyle + " random=" + o.RandomVariant + " direction=" + o.Direction +
                    " origin=" + o.OriginX + "," + o.OriginY + " reliable=" + o.Reliable + " reason=" + o.Reason + " missing=" + o.MissingPayload)) +
                "] cells=[" + String.Join("; ", saved.Cells.Select(c => "xy=" + c.X + "," + c.Y + " object=" + c.ObjectId + " layers=" + c.Layers + " tile=" + c.TileType +
                    " wall=" + c.WallType + " frame=" + c.FrameX + "," + c.FrameY + " paint=" + c.TilePaint + "," + c.WallPaint + " slope=" + c.Slope + " half=" + c.HalfBrick +
                    " inactive=" + c.Inactive + " wires=" + c.Wires + " actuator=" + c.Actuator + " liquid=" + c.LiquidAmount + "," + c.LiquidKind +
                    " invisible=" + c.InvisibleTile + "," + c.InvisibleWall + " fullbright=" + c.FullbrightTile + "," + c.FullbrightWall + " unreliable=" + c.UnreliableObject)) + "]";
            Require(saved.Summary.Width == 4 && saved.Summary.Height == 2 && saved.Cells.Count == 3 && saved.Objects.Count == 1,
                "real authenticated save/open contains the finite source body and the separated ordinary cell; " + detail);
            var obj = saved.Objects.Single(); var chair = saved.Cells.Where(c => c.ObjectId == obj.Id).ToArray();
            // Native .8 chairs use multiplier two and right alternate Style=1:
            // material style zero therefore occupies complete placement one.
            Require(obj.Reliable && obj.Type == TileID.Chairs && obj.Width == 1 && obj.Height == 2 && obj.X == 0 && obj.Y == 0 &&
                obj.Style == 0 && obj.PlacementStyle == 1 && obj.Alternate == 1 && obj.Direction == 2 && chair.Length == 2 &&
                chair[0].X == 0 && chair[0].Y == 0 && chair[0].FrameX == 18 && chair[0].FrameY == 0 &&
                chair[1].X == 0 && chair[1].Y == 1 && chair[1].FrameX == 18 && chair[1].FrameY == 18 &&
                saved.Cells.Single(c => c.ObjectId == 0).X == 3 && saved.Cells.Single(c => c.ObjectId == 0).Y == 1 &&
                saved.Cells.Single(c => c.ObjectId == 0).TileType == TileID.Stone && saved.Cells.Single(c => c.ObjectId == 0).Slope == 1,
                "complete source metadata, member transport order and literal raw frames/slope survive save/open and world edits unchanged; " + detail);
        }
        private static BlueprintProjectionWindow Open(LibraryState library, LibraryEntry entry)
        { return (BlueprintProjectionWindow)Build("OpenCapturedAppearance", library, entry); }
        private static Player Reset(object context, object host, object tools, object input)
        { return (Player)Build("Reset", context, host, tools, input); }
        private static void Stock(Player player)
        {
            player.inventory[12].SetDefaults(ItemID.WoodenChair); player.inventory[12].stack = 4;
            player.inventory[13].SetDefaults(ItemID.StoneBlock); player.inventory[13].stack = 5;
            player.inventory[14].SetDefaults(ItemID.WoodenHammer);
        }
        private static BlueprintMaterialSnapshot Materials(object host, long generation, Action observe)
        {
            BlueprintMaterialSnapshot result = null;
            NativeQuickItemChecks.Until(() =>
            {
                observe(); result = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", generation);
                return result.Status != BlueprintMaterialStatus.Pending && (result.Status != BlueprintMaterialStatus.Exact || result.InventoryReady);
            });
            Require(result.Status == BlueprintMaterialStatus.Exact && result.InventoryReady, "actual requested material consumer becomes Exact: " + result.Status + " " + result.Error);
            return result;
        }
        private static void AwaitProof(object target, Action observe, bool matched, bool unknown)
        {
            NativeQuickItemChecks.Until(() =>
            {
                observe(); var proof = GetOptional(target, "Match");
                return proof != null && (int)Get(proof, "Current") == ((Array)Get(proof, "Ranges")).Length &&
                    ((long)Get(proof, "Matches") > 0) == matched && ((long)Get(proof, "Unknown") > 0) == unknown;
            });
        }
        private static void Load(object host, object target, Action observe)
        { NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(38, 38, 16, 8, 0)); observe(); return GetOptional(target, "Window") != null; }); }
        private static void Observe(object context, object host, object input)
        { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); }
        private static void Request(object host, object input, string action)
        { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, action); Require((bool)Get(host, "ProjectionEditing"), "actual Host accepts " + action); }
        private static void Pointer(object host, bool down, int x, int y)
        { Call(host, "AcceptWorldPointer", true, down, false, x, y, Main.maxTilesX, Main.maxTilesY); }
        private static object[] Scene(object host)
        { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static object Build(string name, params object[] arguments)
        {
            try { return typeof(NativeBlueprintBuildChecks).GetMethod(name, F).Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static void Set(object target, string name, object value)
        { target.GetType().GetField(name, F).SetValue(target, value); }
        private static bool SkipOutlet() { return false; }
        private static void ItemChecked(Player __instance, bool __runOriginal)
        { if (watchNative && __runOriginal && ReferenceEquals(__instance, Main.LocalPlayer)) nativeItemChecks++; }
        private static void Pounded(Player __instance, bool __runOriginal)
        { if (watchNative && __runOriginal && ReferenceEquals(__instance, Main.LocalPlayer)) nativePounds++; }
    }
}
