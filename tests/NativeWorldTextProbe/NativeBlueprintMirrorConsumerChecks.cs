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
                Console.WriteLine("PASS BlueprintMirrorConsumers asset=" + entry.AssetId + " revision=" + entry.ContentRevision +
                    " actualCaptureSaveOpen=true HostMirror=1 materials=one-chair+one-stone naturalItemChecks=" + nativeItemChecks +
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
            NativeQuickItemChecks.Until(() => { observe(); return !Scene(host).Contains(target); });
            Require(((BlueprintProjectionMask)Get(target, "Mask")).RemovedLayerCount == 1 && (bool)Get(host, "AutoBuildEnabled"),
                "real confirm trims the one ordinary source layer and remaining full chair Match naturally retires with preference enabled");
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 20; i++) step();
            Require(!Main.tile[45, 42].active() && Main.tile[42, 41].frameX == 18 && Main.tile[42, 42].frameX == 18 &&
                player.inventory[12].stack == 4 && player.inventory[13].stack == 5 && (long)Get(host, "BuildUsesStarted") == uses,
                "trimmed old stone candidate cannot consume after retirement; the independent built chair and inventory remain exact");
        }

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
