using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The native composition supplies real Host scene commits and exact library
    // opens; real atomic files are then read by new semantic owners. No game is
    // launched and no OS input, multiplayer or FPS result is implied.
    internal static class NativeBlueprintPersistenceChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input");
            var originalLibrary = (LibraryState)Get(host, "Library"); var originalHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var originalWorld = Main.ActiveWorldFileData; bool originalMenu = Main.gameMenu; bool originalAutomatic = (bool)Get(host, "AutoBuildEnabled");
            string root = Path.Combine(Terraria.Program.SavePath, "blueprint-scene-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new JueMingR.Infrastructure.Blueprint.FileBlueprintLibrary(Path.Combine(root, "templates")));
            BlueprintSceneHistory history = null;
            Action step = () => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); };
            Action menu = () => { Main.gameMenu = true; Call(context, "UpdateRuntime"); };
            Action enter = () => { Main.gameMenu = false; step(); NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ProjectionPersistenceReady"); }); };
            Func<BlueprintSceneHistory> create = () => new BlueprintSceneHistory(scope => new AtomicFileDocument(ScenePath(root, scope), Int32.MaxValue, true));
            menu();
            var finiteWorld = NativeBlueprintWorldMatchChecks.EmptyWorld(host, 512, 160);
            try
            {
                Set(host, "<Library>k__BackingField", library); history = create(); Set(host, "projectionHistory", history);
                var worldA = new Terraria.IO.WorldFileData(Path.Combine(root, "a.wld"), false) { UniqueId = Guid.NewGuid() };
                var worldB = new Terraria.IO.WorldFileData(Path.Combine(root, "b.wld"), false) { UniqueId = Guid.NewGuid() };
                Main.ActiveWorldFileData = worldA; enter(); Call(host, "SetAutoBuild", false);
                NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                var mixed = NativeBlueprintProjectionVisualChecks.Save(library, NativeBlueprintProjectionChecks.MixedWindow());
                object first = Place(host, input, mixed, 40, 40, step);
                var cover = Save(library, new CaptureCell { Layers = CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator, WallType = 7, Wires = 1, Actuator = true },
                    new CaptureCell { X = 3, Layers = CaptureLayer.Tile, TileType = 1 });
                object covering = Place(host, input, cover, 45, 42, step);
                var mask = (BlueprintProjectionMask)Get(first, "Mask");
                Require(mask.CellCount == 1 && mask.ObjectCount == 1 && mask.RemovedLayerCount == 15, "real overlap commits a sparse layer mask and the complete twelve-member object mask");
                Remove(host, input, covering, 45, 42, step);
                Require(((BlueprintProjectionMask)Get(first, "Mask")).RemovedLayerCount == 15, "removing the covering instance does not undo historical masks");

                var display = Save(library, new CaptureCell { Layers = CaptureLayer.Liquid, LiquidAmount = 120 });
                Place(host, input, display, 110, 40, step);
                var one = Save(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 });
                object completed = Place(host, input, one, 130, 40, step);
                Require(WorldGen.PlaceTile(130, 40, 1, true, true), "original finite-world writer completes the single target");
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(completed); });
                WorldGen.KillTile(130, 40, false, false, true);
                object removed = Place(host, input, one, 140, 40, step); Remove(host, input, removed, 140, 40, step);

                const int denseCount = 8192;
                var dense = Enumerable.Range(0, denseCount).Select(i => new CaptureCell { X = i % 128, Y = i / 128, Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = 1, WallType = 7 }).ToArray();
                var denseEntry = NativeBlueprintBudgetChecks.Save(library, dense, 128, 64);
                object large = Place(host, input, denseEntry, 200, 60, step);
                var walls = dense.Select(cell => cell.WithoutTile()).ToArray();
                var wallEntry = NativeBlueprintBudgetChecks.Save(library, walls, 128, 64);
                covering = Place(host, input, wallEntry, 200, 60, step); Remove(host, input, covering, 200, 60, step);
                Require(((BlueprintProjectionMask)Get(large, "Mask")).CellCount == denseCount, "real large overlap retains every sparse removed layer without a preference-size quota");

                Move(host, input, first, 40, 45, 60, 45, true, step);
                Require((int)Get(first = Scene(host).Single(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == mixed.AssetId), "OriginX") == 60, "confirmed move publishes its new origin");
                Move(host, input, first, 60, 45, 70, 45, false, step);
                Require(Get(host, "ProjectionMode").ToString() == "Moving", "a second movement remains deliberately unconfirmed");
                step(); NativeQuickItemChecks.Until(() => { step(); return history.Saved; });
                string scopeA = (string)Get(history, "requestedScope"), pathA = ScenePath(root, scopeA);
                byte[] savedA = File.ReadAllBytes(pathA); Require(savedA.Length > 65536, "real atomic scene document exceeds 64 KiB with no dropped masks");
                var document = BlueprintSceneCodec.Decode(savedA, scopeA, 512, 160);
                Require(document.Records.Count == 3 && document.Records.Single(record => record.Template.AssetId == mixed.AssetId).OriginX == 60, "archive contains only the last committed scene, excluding completed/removed/covering and unconfirmed movement");
                long oldGeneration = ((BlueprintProjectionIdentity)Get(first, "Identity")).SessionGeneration;
                menu(); Require(history.Stop(3000), "retired old scene owner really ends before replacement");
                history = create(); Set(host, "projectionHistory", history); enter();
                Require(Scene(host).Length == 3 && Scene(host).Select(item => (long)Get(item, "CommitOrder")).SequenceEqual(document.Records.Select(record => record.Order)), "a new owner restores the complete ordered scene through actual Host/template admissions");
                first = Scene(host).Single(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == mixed.AssetId);
                Require((int)Get(first, "OriginX") == 60 && ((BlueprintProjectionMask)Get(first, "Mask")).RemovedLayerCount == 15 &&
                    ((BlueprintProjectionIdentity)Get(first, "Identity")).SessionGeneration != oldGeneration, "new runtime generation restores committed origin and permanent whole-object/layer history");
                Require(GetOptional(host, "preparedBuild") == null && !(bool)Get(Get(Get(context, "Tools"), "Use"), "Active"), "recovery itself has no old prepared target or native action");
                Require(Scene(host).Any(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == display.AssetId) && !Scene(host).Any(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == one.AssetId), "pure-display survives while genuine completion and manual removal never resurrect");
                Require(((BlueprintProjectionMask)Get(Scene(host).Single(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == denseEntry.AssetId), "Mask")).CellCount == denseCount,
                    "new-owner source validation retains the entire large permanent mask");
                // Rebuild actual Match from the current world after recovery;
                // no persisted completion map is supplied by the fixture.
                Require(WorldGen.PlaceTile(60, 45, 1, true, true), "current destination now receives a native tile");
                WorldGen.paintTile(60, 45, 11, false, false);
                NativeQuickItemChecks.Until(() => { step(); var state = GetOptional(first, "Match"); return state != null && (long)Get(state, "Matches") > 0; });
                Require(Call(host, "ProjectionMoveEligibility", first).ToString() == "Matched", "current world re-observation restores the existing movement lock");

                menu(); Main.ActiveWorldFileData = worldB; enter(); Require(Scene(host).Length == 0, "another reliable world scope starts without A's instances");
                Place(host, input, display, 310, 40, step);
                Require(library.TryRename(denseEntry.AssetId, denseEntry.MetadataIdentity, "跨世界新名字"), "reliable metadata rename is accepted outside A's world");
                NativeQuickItemChecks.Until(() => { step(); return !library.Snapshot.Busy; });
                menu(); Main.ActiveWorldFileData = worldA; enter();
                Require(Scene(host).Length == 3 && Scene(host).All(item => (int)Get(item, "OriginX") != 310), "fast B-to-A transition waits for its writer and reloads only A");
                Require((string)Get(Scene(host).Single(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId == denseEntry.AssetId), "Name") == "跨世界新名字", "restore obtains the reliable current metadata name instead of a saved display label");
                menu(); Main.ActiveWorldFileData = worldB; enter(); Require(Scene(host).Length == 1 && (int)Get(Scene(host)[0], "OriginX") == 310, "B remains independently saved after returning to A");
                byte[] beforeDelete = File.ReadAllBytes(pathA);
                Require((bool)Call(host, "DeleteAsset", mixed), "another world reliably deletes a template still referenced by A's archive");
                NativeQuickItemChecks.Until(() => { step(); return !library.Snapshot.Busy; });
                menu(); Main.ActiveWorldFileData = worldA; enter();
                Require(history.Protected && Scene(host).Length == 2 && Scene(host).All(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).AssetId != mixed.AssetId),
                    "a deleted template is rejected while independent legal instances still restore");
                for (int i = 0; i < 8; i++) step();
                Require(File.ReadAllBytes(pathA).SequenceEqual(beforeDelete), "a rejected archive reference does not cause a filtered or empty rewrite");
                menu(); enter(); Require(Scene(host).Length == 2 && history.Protected, "same-process protected reentry retains independently verified records without reopening a writer");
                OwnerFailures(Path.Combine(root, "mechanical"), document);
                Console.WriteLine("PASS BlueprintPersistence production Host commits/new-owner atomic-file recovery: order/origin/permanent raw and whole-object masks, >64KiB scene, last committed move, removal/completion/display, current Match and independent worlds; delayed read/write retirement and failure protect original. Real process/gameplay exit, OS input, multiplayer and FPS NOT_RUN.");
            }
            finally
            {
                menu(); if (history != null) Require(history.Stop(3000), "isolated scene worker stopped before fixture cleanup");
                Set(host, "projectionHistory", originalHistory); Set(host, "<Library>k__BackingField", originalLibrary); Main.ActiveWorldFileData = originalWorld;
                Require(library.Stop(3000), "isolated template worker stopped before cleanup");
                finiteWorld.Dispose();
                Main.gameMenu = false; step(); NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ProjectionPersistenceReady"); });
                Call(host, "SetAutoBuild", originalAutomatic); if (originalMenu) menu();
            }
        }
        private static string ScenePath(string root, string scope) { return Path.Combine(root, "instances", scope, "scene.bin"); }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static void Set(object target, string field, object value) { target.GetType().GetField(field, F).SetValue(target, value); }
        private static object Place(object host, object input, LibraryEntry entry, int x, int y, Action step)
        { NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "CanPlaceProjection"); }); return NativeBlueprintWorldMatchChecks.Place(host, input, entry, x, y, step); }
        private static LibraryEntry Save(LibraryState library, params CaptureCell[] cells)
        { return NativeBlueprintBudgetChecks.Save(library, cells, cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1); }
        private static void Pointer(object host, bool down, int x, int y)
        { Call(host, "AcceptWorldPointer", true, down, false, x, y, Main.maxTilesX, Main.maxTilesY); }
        private static void Window(object host, object item, Action step)
        {
            NativeQuickItemChecks.Until(() =>
            { Call(host, "UpdateProjectionWindow", new WorldTargetView((int)Get(item, "OriginX"), (int)Get(item, "OriginY"), 16, 16, 0)); step(); return GetOptional(item, "Window") != null; });
        }
        private static void Remove(object host, object input, object item, int x, int y, Action step)
        {
            Window(host, item, step); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection"); Pointer(host, false, x, y); Pointer(host, true, x, y);
            Require(!Scene(host).Contains(item), "trusted remove commits only the selected instance"); step();
        }
        private static void Move(object host, object input, object item, int x, int y, int toX, int toY, bool commit, Action step)
        {
            Window(host, item, step); NativeQuickItemChecks.Until(() => { step(); return Call(host, "ProjectionMoveEligibility", item).ToString() == "Movable"; });
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MoveProjection"); Pointer(host, false, x, y); Pointer(host, true, x, y);
            Require(Get(host, "ProjectionMode").ToString() == "Moving", "trusted selection begins actual moving rollback ownership");
            Pointer(host, false, toX, toY);
            if (commit) { Pointer(host, true, toX, toY); NativeQuickItemChecks.Until(() => { step(); return Get(host, "ProjectionMode").ToString() == "Fixed"; }); }
        }
        private static void OwnerFailures(string root, BlueprintSceneDocument source)
        {
            const string scope = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            string path = ScenePath(root, scope); Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] original = BlueprintSceneCodec.Encode(new BlueprintSceneDocument(scope, source.Width, source.Height, 1, source.Records)); File.WriteAllBytes(path, original);
            int factories = 0, active = 0, maximumActive = 0;
            using (var readGate = new ManualResetEvent(false))
            using (var writeGate = new ManualResetEvent(false))
            using (var readEntered = new ManualResetEvent(false))
            using (var writeEntered = new ManualResetEvent(false))
            {
                var owner = new BlueprintSceneHistory(key =>
                {
                    int number = ++factories; maximumActive = Math.Max(maximumActive, ++active);
                    return new ControlledStorage(new AtomicFileDocument(ScenePath(root, key), Int32.MaxValue, true), () => active--,
                        number == 1 ? readGate : null, number == 1 ? readEntered : null, number == 1 ? writeGate : null, number == 1 ? writeEntered : null, false);
                });
                try
                {
                    owner.BeginSession(1, source.Width, source.Height); owner.UseScope(scope);
                    Require(readEntered.WaitOne(1000), "actual document read entered the finite fixture gate");
                    Require(!owner.Offer(Array.Empty<BlueprintSceneRecord>()), "initial load refuses new scene edits rather than allowing late data to overwrite them");
                    readGate.Set(); NativeQuickItemChecks.Until(() => { owner.Poll(); return owner.Loaded; });
                    Require(owner.Offer(source.Records.Take(1).ToArray()), "loaded owner accepts an immutable committed successor");
                    NativeQuickItemChecks.Until(() => { owner.Poll(); return writeEntered.WaitOne(0); });
                    owner.EndSession(); owner.BeginSession(2, source.Width, source.Height); owner.UseScope(scope);
                    for (int i = 0; i < 8; i++) owner.Poll();
                    Require(factories == 1 && !owner.Loaded, "fast same-scope reentry cannot create a second writer before old accepted write finishes");
                    writeGate.Set(); NativeQuickItemChecks.Until(() => { owner.Poll(); return owner.Loaded; });
                    Require(maximumActive == 1 && factories == 2 && owner.Restored.Records.Count == 1 && owner.Restored.Revision == 2,
                        "old final accepted revision is read by a new owner only after the real file lease has ended");
                }
                finally { readGate.Set(); writeGate.Set(); Require(owner.Stop(3000), "gated owner actually stopped"); }
            }
            byte[] beforeFailure = File.ReadAllBytes(path);
            var failed = new BlueprintSceneHistory(key => new ControlledStorage(new AtomicFileDocument(ScenePath(root, key), Int32.MaxValue, true), null, null, null, null, null, true));
            try
            {
                failed.BeginSession(3, source.Width, source.Height); failed.UseScope(scope); NativeQuickItemChecks.Until(() => { failed.Poll(); return failed.Loaded; });
                Require(failed.Offer(Array.Empty<BlueprintSceneRecord>()), "reliable initial data accepts the attempted explicit removal");
                NativeQuickItemChecks.Until(() => { failed.Poll(); return failed.Protected; });
                Require(!failed.Offer(Array.Empty<BlueprintSceneRecord>()) && File.ReadAllBytes(path).SequenceEqual(beforeFailure), "one failed save protects source bytes and cannot later overwrite the archive as empty");
            }
            finally { Require(failed.Stop(3000), "failed worker stopped without an automatic retry"); }
            // A valid checksum with a wrong scope is still rejected by the
            // semantic owner. Neither initial failure nor Stop writes defaults.
            var wrong = new BlueprintSceneHistory(key => new AtomicFileDocument(path, Int32.MaxValue, true));
            try
            {
                wrong.BeginSession(4, source.Width, source.Height); wrong.UseScope(new string('b', 64)); NativeQuickItemChecks.Until(() => { wrong.Poll(); return wrong.Loaded; });
                Require(wrong.Protected && wrong.Restored == null && wrong.Error == PreferenceStatus.Conflict.ToString() && !wrong.Offer(Array.Empty<BlueprintSceneRecord>()), "wrong-scope bytes remain an identity conflict, never a reliable empty document");
            }
            finally { Require(wrong.Stop(3000), "identity-failed owner stopped"); }
            Require(File.ReadAllBytes(path).SequenceEqual(beforeFailure), "all rejected recovery attempts preserve the original document");
            var future = (byte[])original.Clone(); future[8] = 2;
            using (var sha = System.Security.Cryptography.SHA256.Create()) Array.Copy(sha.ComputeHash(future, 0, future.Length - 32), 0, future, future.Length - 32, 32);
            ProtectedRead(Path.Combine(root, "future"), scope, source.Width, source.Height, future, PreferenceStatus.UnsupportedVersion);
            var corrupt = (byte[])original.Clone(); corrupt[60] ^= 1;
            ProtectedRead(Path.Combine(root, "corrupt"), scope, source.Width, source.Height, corrupt, PreferenceStatus.Invalid);
        }
        private static void ProtectedRead(string root, string scope, int width, int height, byte[] bytes, PreferenceStatus expected)
        {
            string path = ScenePath(root, scope); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes);
            var owner = new BlueprintSceneHistory(key => new AtomicFileDocument(ScenePath(root, key), Int32.MaxValue, true));
            try
            {
                owner.BeginSession(1, width, height); owner.UseScope(scope); NativeQuickItemChecks.Until(() => { owner.Poll(); return owner.Loaded; });
                Require(owner.Protected && owner.Error == expected.ToString() && !owner.Offer(Array.Empty<BlueprintSceneRecord>()), "scene reader preserves the distinct " + expected + " state");
            }
            finally { Require(owner.Stop(3000), "protected format owner ended"); }
            Require(File.ReadAllBytes(path).SequenceEqual(bytes), "protected " + expected + " bytes survive load/stop unchanged");
        }
        private sealed class ControlledStorage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly Action disposed;
            private readonly ManualResetEvent readGate, readEntered, writeGate, writeEntered; private readonly bool failWrite;
            internal ControlledStorage(IPreferenceStorage inner, Action disposed, ManualResetEvent readGate, ManualResetEvent readEntered, ManualResetEvent writeGate, ManualResetEvent writeEntered, bool failWrite)
            { this.inner = inner; this.disposed = disposed; this.readGate = readGate; this.readEntered = readEntered; this.writeGate = writeGate; this.writeEntered = writeEntered; this.failWrite = failWrite; }
            public PreferenceReadResult Read() { readEntered?.Set(); readGate?.WaitOne(); return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            { writeEntered?.Set(); writeGate?.WaitOne(); return failWrite ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "injected-before-commit", false, true) : inner.Write(identity, bytes); }
            public void Dispose() { inner.Dispose(); disposed?.Invoke(); }
        }
    }
}
