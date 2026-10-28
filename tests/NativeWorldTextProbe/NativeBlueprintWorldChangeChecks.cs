using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.NetModules;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Watch registration is fixture setup. All primary invalidations below
    // come from actual .8 write/receive entries, never injected dirty bits.
    // The outer native scope owns its isolated world and muted audio policy.
    internal static class NativeBlueprintWorldChangeChecks
    {
        private const long Focus = -91001, Far = -91002;
        private const int X = 31, Y = 31;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static IDisposable ObserveFrameFaults() { return new FrameFaultScope(); }
        private sealed class FrameFaultScope : IDisposable
        {
            private readonly HarmonyLib.Harmony patch = new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.NativeFrameFault");
            internal FrameFaultScope()
            {
                foreach (var method in typeof(WorldGen).GetMethods(Flags).Where(m => m.GetMethodBody() != null &&
                    (m.Name.StartsWith("KillTile_", StringComparison.Ordinal) || m.Name.StartsWith("CheckTileBreakability", StringComparison.Ordinal) || m.Name == "AttemptFossilShattering")))
                    patch.Patch(method, finalizer: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldChangeChecks), nameof(FrameLeafFault)));
                patch.Patch(typeof(WorldGen).GetMethod("KillTile", Flags), finalizer:
                    new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldChangeChecks), nameof(FrameLeafFault)));
                patch.Patch(typeof(WorldGen).GetMethod("TileFrameImportant", Flags), finalizer:
                    new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldChangeChecks), nameof(FrameFault)));
            }
            public void Dispose()
            { foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyLib.HarmonyPatchType.All, patch.Id); }
        }
        private static Exception FrameLeafFault(MethodBase __originalMethod, Exception __exception)
        {
            if (__exception != null) Console.Error.WriteLine("BLUEPRINT ORIGINAL FRAME LEAF " + __originalMethod.Name + " " + __exception);
            return __exception;
        }
        private static Exception FrameFault(int __0, int __1, int __2, Exception __exception)
        {
            // TileFrame's original broad catch otherwise hides this exception
            // and any native recursive-framing flag it failed to return. The
            // narrow diagnostic observes the unmodified exception before that
            // catch; it neither clears the flag nor supplies a successful call.
            if (__exception != null) Console.Error.WriteLine("BLUEPRINT ORIGINAL FRAME FAULT xy=" + __0 + "," + __1 + " type=" + __2 +
                " destroyObject=" + WorldGen.destroyObject + " " + __exception);
            return __exception;
        }
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); Call(host, "ClearProjection");
            object observer = Get(host, "projectionChanges");
            // The CPU fixture initializes object metadata without Main's full
            // graphics startup. Native framing needs its own real lookup data.
            if (typeof(Framing).GetField("selfFrame8WayLookup", Flags).GetValue(null) == null) Framing.Initialize();
            if (Main.tileMerge[1] == null) Main.SetupTileMerge();
            var saved = new Tile[18, 14];
            for (int x = 0; x < saved.GetLength(0); x++) for (int y = 0; y < saved.GetLength(1); y++)
            { saved[x, y] = Main.tile[X - 2 + x, Y - 2 + y]; Main.tile[X - 2 + x, Y - 2 + y] = new Tile(); }
            int mode = Main.netMode; WorldSections sections = Main.sectionManager;
            bool solid = Main.tileSolid[1], top = Main.tileSolidTop[1];
            bool nativeFraming = WorldGen.destroyObject;
            var absentPlayers = Enumerable.Range(0, Main.player.Length).Where(i => Main.player[i] == null).ToArray();
            try
            {
                // Normal Main initializes every Player slot. A malformed lamp
                // square genuinely frames/destroys members here; its original
                // KillTile_DropBait -> FindClosest visits even inactive slots.
                // Null slots otherwise throw inside TileFrame's broad catch,
                // leaving destroyObject set and poisoning later chest replies.
                foreach (int i in absentPlayers) Main.player[i] = new Player();
                Main.netMode = 0; Main.tileSolid[1] = true; Main.tileSolidTop[1] = false;
                Main.sectionManager = new WorldSections(1, 1);
                Main.sectionManager.SetAllSectionsLoaded();
                Tile tile = Main.tile[X, Y]; tile.active(true); tile.type = 1; tile.wall = 1;
                Main.tile[X + 1, Y + 1].active(true); Main.tile[X + 1, Y + 1].type = 1;
                Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
                Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
                Changed(observer, () => Native(typeof(WorldGen), "paintTile", X, Y, (byte)11, false, false), "tile paint");
                Require(tile.color() == 11, "actual native paint field changed");
                long stable = Stamp(observer, Focus);
                Native(typeof(WorldGen), "paintTile", X, Y, (byte)11, false, false);
                Require(Current(observer, Focus, stable), "same native paint value does not keep stable proof dirty");
                Changed(observer, () => Native(typeof(WorldGen), "paintWall", X, Y, (byte)4, false, false), "wall paint");
                Changed(observer, () => Native(typeof(WorldGen), "paintCoatTile", X, Y, (byte)1, false, false), "tile fullbright");
                Changed(observer, () => Native(typeof(WorldGen), "paintCoatTile", X, Y, (byte)2, false, false), "tile invisible");
                Changed(observer, () => Native(typeof(WorldGen), "paintCoatWall", X, Y, (byte)1, false, false), "wall fullbright");
                Changed(observer, () => Native(typeof(WorldGen), "paintCoatWall", X, Y, (byte)2, false, false), "wall invisible");
                Require(tile.fullbrightBlock() && tile.invisibleBlock() && tile.fullbrightWall() && tile.invisibleWall(), "native coating families persist in true Tile");
                foreach (string method in new[] { "PlaceWire", "PlaceWire2", "PlaceWire3", "PlaceWire4", "PlaceActuator" })
                    Changed(observer, () => Native(typeof(WorldGen), method, X, Y), "true native " + method + " including tiny-setter inlining regression");
                Require(tile.wire() && tile.wire2() && tile.wire3() && tile.wire4() && tile.actuator(), "four independent native wire bits and actuator written");
                Changed(observer, () => Native(typeof(Wiring), "ActuateForced", X, Y), "inactive body");
                Require(tile.inActive(), "native actuation changes inactive, not active presence");
                Changed(observer, () => Native(typeof(WorldGen), "SlopeTile", X, Y, 1, true, true), "slope");
                Require(tile.slope() == 1, "native shape writer received real slope");
                Changed(observer, () => Native(typeof(WorldGen), "ReplaceWall", X, Y, (ushort)4), "wall type");
                Require(tile.wall == 4, "native wall replacement changes type");
                Changed(observer, () => new Terraria.WorldBuilding.Actions.PlaceWall(4, false).Apply(new Microsoft.Xna.Framework.Point(X, Y), X + 8, Y, Array.Empty<object>()), "hardmode polymorphic native wall-action leaf");
                Require(Main.tile[X + 8, Y].wall == 4, "actual Actions.PlaceWall writes wall through GenBase tile array");

                // A cached Tile parameter is the actual member, not an array
                // read in this tiny native helper; its i/j mapping is required.
                Tile torch = Main.tile[X + 5, Y]; torch.active(true); torch.type = 4; torch.frameX = torch.frameY = 0;
                Changed(observer, () => Native(typeof(Wiring), "ToggleTorch", X + 5, Y, torch, (bool?)false), "cached-member furniture state");
                Require(torch.frameX == 66, "actual native torch off-state frame written");

                ReceiveLiquid(observer);
                ReceiveSquare(observer);
                ReceiveSection(observer);
                PendingAndException(observer);
                BackgroundNoWatchBoundary(observer);
                SparseDependencies(observer);
                LayerDependencies(observer, host.GetType().Assembly);
                CoordinatePressure(observer, host.GetType().Assembly);
                RoutedDependencies(observer);
                CanonicalFrameTransactions(observer, host.GetType().Assembly);
                CanonicalDoorTransactions(observer, host.GetType().Assembly);
                StableCosts(observer, host.GetType().Assembly);
                Require(WorldGen.destroyObject == nativeFraming, "real frame/destruction consumers naturally return their original recursion flag");
                Console.WriteLine("PASS BlueprintWorldChanges: actual .8 paint/coating/shape/wall/four-wire/actuator/inactive/cached furniture/TileSquare/liquid-module/section receivers; relevant stamps, bounded merge, no-op/animation/remote stability, in-flight and exceptional receive revocation. Live network/FPS/owner gameplay NOT_RUN.");
            }
            finally
            {
                Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far);
                Main.netMode = mode; Main.sectionManager = sections; Main.tileSolid[1] = solid; Main.tileSolidTop[1] = top;
                foreach (int i in absentPlayers) Main.player[i] = null;
                // The assertion above observes natural return first. Cleanup
                // also owns the original fixture value on a failing path.
                WorldGen.destroyObject = nativeFraming;
                for (int x = 0; x < saved.GetLength(0); x++) for (int y = 0; y < saved.GetLength(1); y++) Main.tile[X - 2 + x, Y - 2 + y] = saved[x, y];
            }
        }

        private static void ReceiveLiquid(object observer)
        {
            byte[] payload = LiquidPayload(1, 47, 2);
            Changed(observer, () => { using (var reader = new BinaryReader(new MemoryStream(payload))) new NetLiquidModule().Deserialize(reader, 256); }, "batched liquid amount/kind receiver");
            Require(Main.tile[X + 2, Y].liquid == 47 && Main.tile[X + 2, Y].liquidType() == 2, "actual NetLiquidModule writes amount and kind");
        }

        private static void ReceiveSquare(object observer)
        {
            int mode = Main.netMode; Main.netMode = 1;
            try
            {
                var buffer = new MessageBuffer { whoAmI = 256 };
                using (var bytes = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                    {
                        writer.Write((byte)20); writer.Write((short)(X + 3)); writer.Write((short)Y); writer.Write((byte)1); writer.Write((byte)1); writer.Write((byte)0);
                        writer.Write((byte)221); writer.Write((byte)159); writer.Write((byte)15); writer.Write((byte)11); writer.Write((byte)4);
                        writer.Write((ushort)1); writer.Write((ushort)4); writer.Write((byte)77); writer.Write((byte)2);
                    }
                    byte[] payload = bytes.ToArray(); Array.Copy(payload, buffer.readBuffer, payload.Length); buffer.ResetReader();
                    Changed(observer, () => { int kind; buffer.GetData(0, payload.Length, out kind); Require(kind == 20, "actual TileSquare dispatch"); }, "actual MessageBuffer TileSquare application");
                }
                Tile tile = Main.tile[X + 3, Y];
                Require(tile.active() && tile.type == 1 && tile.wall == 4 && tile.liquid == 77 && tile.liquidType() == 2 && tile.wire2() && tile.wire3() && tile.wire4() && tile.actuator() && tile.inActive(), "real TileSquare field family receipt");
                Require(tile.color() == 11 && tile.wallColor() == 4 && tile.fullbrightBlock() && tile.fullbrightWall() && tile.invisibleBlock() && tile.invisibleWall(), "real TileSquare appearance receipt");
            }
            finally { Main.netMode = mode; }
        }

        private static void ReceiveSection(object observer)
        {
            Main.netMode = 1;
            Main.sectionManager = new WorldSections(1, 1);
            Require(!Main.sectionManager.TileLoaded(X, Y), "fresh native section genuinely unreadable");
            long before = Stamp(observer, Focus);
            Main.sectionManager.SetSectionLoaded(0, 0);
            Require(!Current(observer, Focus, before) && Main.sectionManager.TileLoaded(X, Y), "actual section-loaded application invalidates the old unreadable qualification");
            Drain(observer);
            // Real native serialization followed by real native application.
            // Setup is deliberately outside the claimed product observation.
            byte[] payload;
            Main.tile[X + 7, Y].active(true); Main.tile[X + 7, Y].type = 1; Main.tile[X + 7, Y].wire3(true);
            using (var stream = new MemoryStream())
            { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) NetMessage.CompressTileBlock_Inner(writer, X + 7, Y, 1, 1); payload = stream.ToArray(); }
            Main.tile[X + 7, Y] = new Tile();
            Call(observer, "Unwatch", Focus); Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
            Changed(observer, () => { using (var reader = new BinaryReader(new MemoryStream(payload))) NetMessage.DecompressTileBlock_Inner(reader, X + 7, Y, 1, 1); }, "section tile replacement on already loaded section");
            Require(Main.tile[X + 7, Y].active() && Main.tile[X + 7, Y].type == 1 && Main.tile[X + 7, Y].wire3(), "actual native block decoder restored source content");
            Main.netMode = 0;
        }

        private static void PendingAndException(object observer)
        {
            using (var stream = new GatedStream(LiquidPayload(2, 48, 1)))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } });
                worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000), "real receiver applied first record and reached the controlled second-record boundary");
                    Drain(observer); long stamp;
                    Require(!TryStamp(observer, Focus, out stamp), "draining dirty does not authorize reads while native receiver remains pending");
                    bool ran = false; Require(!(bool)Call(observer, "TryRunStable", Focus, stamp, (Action)(() => ran = true)) && !ran, "safe completion cannot pass in-flight native receiver");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "controlled native receiver finished"); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Drain(observer); Stamp(observer, Focus);
            byte[] partial = LiquidPayload(2, 49, 2).Take(8).ToArray();
            bool failed = false;
            try { using (var reader = new BinaryReader(new MemoryStream(partial))) new NetLiquidModule().Deserialize(reader, 256); }
            catch (EndOfStreamException) { failed = true; }
            Require(failed, "actual truncated receiver throws after applying a real earlier mutation");
            Drain(observer); Stamp(observer, Focus);
            Require(Main.tile[X + 2, Y].liquid == 49, "exceptional receive retains the actual partial write and releases pending without inventing rollback");
        }

        private static void StableCosts(object observer, Assembly assembly)
        {
            Native(typeof(WorldGen), "paintTile", X, Y, (byte)11, false, false); Drain(observer);
            long stamp = Stamp(observer, Focus), far = Stamp(observer, Far);
            int animation = Main.tileFrame[4];
            try
            {
                for (int i = 0; i < 200; i++) { Main.tileFrame[4] = i % 3; Native(typeof(WorldGen), "paintTile", X, Y, (byte)11, false, false); }
                Require(Current(observer, Focus, stamp) && Current(observer, Far, far), "continuous native animation arrays and repeated no-op paint converge without observer resets");
                for (int i = 0; i < 100; i++) Native(typeof(WorldGen), "paintTile", X, Y, (byte)(i % 2 == 0 ? 12 : 11), false, false);
                int dirty = Drain(observer); Require(dirty == 1, "100 actual field writes coalesce into one live-watch event, not event history");
                Require(Current(observer, Far, far), "remote watch never inherits an unrelated global mutation epoch");
                var hooks = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldChangeHooks", true);
                var targets = (IEnumerable<string>)hooks.GetField("DebugHookTargets", Flags).GetValue(null);
                Require(targets.Contains("Terraria.WorldGen.meteor") && targets.Contains("Terraria.WorldGen.GERunner") && targets.Contains("Terraria.WorldBuilding.Actions+PlaceWall.Apply"), "actual direct-time, background-hardmode and polymorphic wall-writer roots retained");
                Console.WriteLine("BlueprintWorldChanges hooks=" + hooks.GetField("DebugHookCount", Flags).GetValue(null) + " discoveryMs=" + hooks.GetField("DebugDiscoveryMilliseconds", Flags).GetValue(null) + " selectionMs=" + hooks.GetField("DebugSelectionMilliseconds", Flags).GetValue(null) + " patchMs=" + hooks.GetField("DebugPatchMilliseconds", Flags).GetValue(null));
                NativePaintBurst(20); AppDomain.MonitoringIsEnabled = true;
                long mappedBefore = (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null);
                var timer = System.Diagnostics.Stopwatch.StartNew(); long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                NativePaintBurst(1000); long watchedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated; timer.Stop(); long watchedTicks = timer.ElapsedTicks;
                long mappedAfter = (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null);
                Require(mappedAfter - mappedBefore == 1000, "stable finite watch performs one native-cell mapping per attempted paint and no template scan");
                Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far);
                Require((int)hooks.GetProperty("DebugScopeBuffers", Flags).GetValue(null, null) == 0 && (int)hooks.GetProperty("DebugGlobalScopeBuffers", Flags).GetValue(null, null) == 0,
                    "last watch releases retained coordinate storage on both the game thread and previously used native worker threads");
                NativePaintBurst(20); mappedBefore = (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null);
                timer.Restart(); allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                NativePaintBurst(1000); long idleBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated; timer.Stop();
                mappedAfter = (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null);
                Require(mappedAfter == mappedBefore && (int)hooks.GetProperty("DebugScopeBuffers", Flags).GetValue(null, null) == 0, "no-watch native writes return without coordinate reads, maps or pending state");
                Console.WriteLine("BlueprintWorldChanges 1000 no-op native paints: watchedTicks=" + watchedTicks + " watchedAllocated=" + watchedBytes + " noWatchTicks=" + timer.ElapsedTicks + " noWatchAllocated=" + idleBytes + "; structural microcheck, not frame/FPS evidence");
            }
            finally { Main.tileFrame[4] = animation; }
        }

        private static void SparseDependencies(object observer)
        {
            // Both actual dependency cells share a bbox with an unrelated
            // middle Tile. Fixture writes precede the new watch identity;
            // the proof-changing writes below use the original paint entry.
            Tile middle = Main.tile[X + 4, Y], end = Main.tile[X + 10, Y];
            middle.active(true); middle.type = 1; end.active(true); end.type = 1;
            var bounds = new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10);
            Func<BlueprintProjectionBounds, bool> intersects = changed => changed.Contains(X, Y) || changed.Contains(X + 10, Y);
            Watch(observer, Focus, bounds, intersects);
            long before = Stamp(observer, Focus), far = Stamp(observer, Far);
            Native(typeof(WorldGen), "paintTile", X + 4, Y, (byte)12, false, false);
            Require(middle.color() == 12 && Current(observer, Focus, before) && Current(observer, Far, far) && Drain(observer) == 0,
                "actual native content write in the dependency bbox's sparse hole preserves both current proofs");
            Changed(observer, () => Native(typeof(WorldGen), "paintTile", X + 10, Y, (byte)13, false, false), "sparse dependency endpoint native write");
            Require(end.color() == 13, "sparse endpoint change is an actual Tile write, not injected invalidation");
            Watch(observer, Focus, bounds);
        }

        private static void LayerDependencies(object observer, Assembly assembly)
        {
            Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far);
            Tile tile = Main.tile[X, Y] = new Tile(); tile.active(true); tile.type = 1; tile.wall = 1;
            Main.tile[X + 2, Y] = new Tile();
            Tile torch = Main.tile[X + 5, Y] = new Tile(); torch.active(true); torch.type = 4;
            var bounds = new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10);
            const long first = -92000;
            try
            {
                for (int i = 0; i < 8; i++)
                { ushort bit = (ushort)(1 << i); Watch(observer, first - i, bounds, (Func<BlueprintProjectionBounds, ushort, bool>)((changed, layers) => (layers & bit) != 0), bit); }
                ChangedLayers(observer, first, 1, () => Native(typeof(WorldGen), "paintTile", X, Y, (byte)5, false, false), "sHeader block paint");
                ChangedLayers(observer, first, 2, () => Native(typeof(WorldGen), "paintWall", X, Y, (byte)6, false, false), "bHeader wall paint");
                ChangedLayers(observer, first, 1, () => Native(typeof(WorldGen), "paintCoatTile", X, Y, (byte)1, false, false), "bHeader3 fullbright block");
                ChangedLayers(observer, first, 1, () => Native(typeof(WorldGen), "paintCoatTile", X, Y, (byte)2, false, false), "bHeader3 invisible block");
                ChangedLayers(observer, first, 2, () => Native(typeof(WorldGen), "paintCoatWall", X, Y, (byte)1, false, false), "sHeader fullbright wall");
                ChangedLayers(observer, first, 2, () => Native(typeof(WorldGen), "paintCoatWall", X, Y, (byte)2, false, false), "bHeader3 invisible wall");
                string[] place = { "PlaceWire", "PlaceWire2", "PlaceWire3", "PlaceWire4" };
                for (int i = 0; i < 4; i++) { string method = place[i]; ChangedLayers(observer, first, (ushort)(16 << i), () => Native(typeof(WorldGen), method, X, Y), "independent " + method); }
                ChangedLayers(observer, first, 8, () => Native(typeof(WorldGen), "PlaceActuator", X, Y), "installed actuator");
                ChangedLayers(observer, first, 1, () => Native(typeof(Wiring), "ActuateForced", X, Y), "inactive belongs to Tile rather than Actuator");
                ChangedLayers(observer, first, 1, () => Native(typeof(WorldGen), "SlopeTile", X, Y, 1, true, true), "shape belongs to Tile");
                ChangedLayers(observer, first, 1, () => Native(typeof(Wiring), "ToggleTorch", X + 5, Y, torch, (bool?)false), "frame-important furniture frame still matters");
                ChangedLayers(observer, first, 4, () => { using (var reader = new BinaryReader(new MemoryStream(LiquidPayload(1, 42, 3)))) new NetLiquidModule().Deserialize(reader, 256); }, "amount and kind belong to Liquid");
                Require(tile.color() == 5 && tile.wallColor() == 6 && tile.fullbrightBlock() && tile.invisibleBlock() && tile.fullbrightWall() && tile.invisibleWall() && tile.inActive() && tile.actuator(), "original packed fields actually changed in layer tests");
                Main.netMode = 1; // Native client removal does not create loot.
                string[] remove = { "KillWire", "KillWire2", "KillWire3", "KillWire4" };
                for (int i = 0; i < 4; i++) { string method = remove[i]; ChangedLayers(observer, first, (ushort)(16 << i), () => Native(typeof(WorldGen), method, X, Y), "independent " + method); }
                ChangedLayers(observer, first, 8, () => Native(typeof(WorldGen), "KillActuator", X, Y), "actuator removal");
                Main.netMode = 0;
            }
            finally { for (int i = 0; i < 8; i++) Call(observer, "Unwatch", first - i); }

            // The union contains BOTH layers. Exact per-point geometry must
            // reject an extra Tile at the wall point and vice versa.
            tile.inActive(false); tile.slope(0); tile.halfBrick(false);
            Tile end = Main.tile[X + 10, Y]; end.active(true); end.type = 1; end.wall = 1; end.color(0);
            Func<BlueprintProjectionBounds, ushort, bool> geometry = (changed, layers) =>
                (changed.Contains(X, Y) && (layers & 2) != 0) || (changed.Contains(X + 10, Y) && (layers & 1) != 0);
            Watch(observer, Focus, bounds, geometry, (ushort)3);
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
            long before = Stamp(observer, Focus), far = Stamp(observer, Far);
            Native(typeof(WorldGen), "paintTile", X, Y, (byte)12, false, false);
            Native(typeof(WorldGen), "paintWall", X + 10, Y, (byte)8, false, false);
            Require(tile.color() == 12 && end.wallColor() == 8 && Current(observer, Focus, before), "extra per-point layers preserve mixed-range proof despite the broad target union");
            bool frameChanged = false;
            for (int i = 0; i < 200; i++)
            {
                short oldX = end.frameX, oldY = end.frameY; end.frameNumber((byte)(i % 3));
                Native(typeof(Framing), "SelfFrame8Way", X + 10, Y, end, false);
                frameChanged |= oldX != end.frameX || oldY != end.frameY;
            }
            Require(frameChanged && !Main.tileFrameImportant[end.type] && Current(observer, Focus, before), "actual ordinary-terrain random framing changes raw frames without changing Tile semantics");
            var hooks = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldChangeHooks", true);
            NativeWireBurst(20); AppDomain.MonitoringIsEnabled = true;
            long writes = (long)hooks.GetField("DebugRecordedWrites", Flags).GetValue(null);
            var timer = new System.Diagnostics.Stopwatch(); long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            timer.Start(); NativeWireBurst(1000); timer.Stop();
            long extraBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated;
            Require(Current(observer, Focus, before) && Current(observer, Far, far) && Drain(observer) == 0 && (long)hooks.GetField("DebugRecordedWrites", Flags).GetValue(null) == writes,
                "1000 actual extra-wire changes do not allocate dirty/pending or revoke mixed range proof");
            Console.WriteLine("BlueprintWorldChanges extra-layer 1000 native wire changes: ticks=" + timer.ElapsedTicks + " allocated=" + extraBytes + "; range union Tile+Wall, per-point exact layers");
            Changed(observer, () => Native(typeof(WorldGen), "paintWall", X, Y, (byte)9, false, false), "true wall target change in mixed range");
            Changed(observer, () => Native(typeof(WorldGen), "paintTile", X + 10, Y, (byte)14, false, false), "true Tile target change in mixed range");
            Watch(observer, Focus, bounds);
        }

        private static void ChangedLayers(object observer, long first, ushort expected, Action write, string reason)
        {
            var stamps = new long[8]; for (int i = 0; i < 8; i++) stamps[i] = Stamp(observer, first - i);
            write();
            for (int i = 0; i < 8; i++) Require(Current(observer, first - i, stamps[i]) == ((expected & (1 << i)) == 0), reason + " revokes exactly its original field's semantic layers");
            ushort dirty = 0;
            Drain(observer, (Action<long, BlueprintProjectionBounds>)((id, bounds) => { int index = (int)(first - id); Require(index >= 0 && index < 8, "only actual layer watches notified"); dirty |= (ushort)(1 << index); }));
            Require(dirty == expected, reason + " does not create unrelated pending/dirty before filtering");
            for (int i = 0; i < 8; i++) Stamp(observer, first - i);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NativeWireBurst(int count)
        {
            int mode = Main.netMode; Main.netMode = 1;
            try { for (int i = 0; i < count; i++) { if ((i & 1) == 0) WorldGen.PlaceWire(X, Y); else WorldGen.KillWire(X, Y); } }
            finally { Main.netMode = mode; }
        }

        private static void CoordinatePressure(object observer, Assembly assembly)
        {
            var hooks = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldChangeHooks", true);
            int cap = (int)hooks.GetField("MaximumCoordinateAssociations", Flags).GetRawConstantValue();
            int side = (int)Math.Sqrt(cap) + 1, px = X + 20, py = Y + 20;
            Require(side * side > cap && px + side < Main.maxTilesX && py + side < Main.maxTilesY, "finite pressure receiver exceeds only the association cap, inside the owned world");
            var saved = new Tile[side, side];
            for (int x = 0; x < side; x++) for (int y = 0; y < side; y++) { saved[x, y] = Main.tile[px + x, py + y]; Main.tile[px + x, py + y] = new Tile { wall = 7 }; }
            var pressure = new BlueprintProjectionBounds(px, py, side, side);
            var total = new BlueprintProjectionBounds(X - 1, Y - 1, px + side - X + 1, py + side - Y + 1);
            Func<BlueprintProjectionBounds, ushort, bool> geometry = (changed, layers) =>
                ((layers & 2) != 0 && Hit(changed, pressure)) || ((layers & 4) != 0 && changed.Contains(px, py)) || ((layers & 1) != 0 && changed.Contains(X + 10, Y));
            try
            {
                Watch(observer, Focus, total, geometry, (ushort)7);
                long before = Stamp(observer, Focus), far = Stamp(observer, Far);
                long overflows = (long)hooks.GetField("DebugOverflowScopes", Flags).GetValue(null);
                using (var stream = new GatedStream(PressurePayload(px, py, side, 0, 0, false), 2 + 6 * (cap + 1)))
                using (var reader = new BinaryReader(stream))
                {
                    Exception failure = null;
                    var worker = new Thread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                    try
                    {
                        Require(stream.Entered.Wait(10000) && (long)hooks.GetField("DebugOverflowScopes", Flags).GetValue(null) > overflows, "actual receiver crossed the coordinate cap before its controlled read boundary");
                        Tile terrain = Main.tile[X + 10, Y]; bool changedFrame = false;
                        for (int i = 0; i < 30; i++) { short old = terrain.frameX; terrain.frameNumber((byte)(i % 3)); Native(typeof(Framing), "SelfFrame8Way", X + 10, Y, terrain, false); changedFrame |= old != terrain.frameX; }
                        Require(changedFrame && Current(observer, Focus, before) && TryStamp(observer, Focus, out before) && Drain(observer) == 0,
                            "real no-op read pressure plus changing ordinary terrain frames creates neither dirty nor pending");
                    }
                    finally { stream.Release.Set(); Require(worker.Join(10000), "no-op pressure receiver exited"); }
                    if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                }
                using (var reader = new BinaryReader(new MemoryStream(PressurePayload(px, py, side, 37, 37, false)))) new NetLiquidModule().Deserialize(reader, 256);
                Require(Main.tile[px + side - 1, py + side - 1].liquid == 37 && Current(observer, Focus, before) && Current(observer, Far, far) && Drain(observer) == 0,
                    "actual extra-liquid writes past the cap preserve a mixed range whose Liquid target is elsewhere");
                Func<BlueprintProjectionBounds, ushort, bool> tailGeometry = (changed, layers) => geometry(changed, layers) || ((layers & 4) != 0 && changed.Contains(px + side - 1, py + side - 1));
                Watch(observer, Focus, total, tailGeometry, (ushort)7);
                before = Stamp(observer, Focus);
                byte[] partial = PressurePayload(px, py, side, 37, 38, true);
                using (var stream = new GatedStream(partial, partial.Length))
                using (var reader = new BinaryReader(stream))
                {
                    Exception failure = null;
                    var worker = new Thread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                    try
                    {
                        Require(stream.Entered.Wait(10000) && Main.tile[px + side - 1, py + side - 1].liquid == 38 && !Current(observer, Focus, before), "tail write beyond the cap actually applies and immediately revokes old proof");
                        Drain(observer); long pending;
                        Require(!TryStamp(observer, Focus, out pending), "pressure tail write retains pending while the actual receiver is suspended");
                    }
                    finally { stream.Release.Set(); Require(worker.Join(10000), "exceptional pressure receiver exited"); }
                    Require(failure is EndOfStreamException, "truncated extra record throws only after the true tail mutation");
                }
                Drain(observer); Stamp(observer, Focus);
                Require((int)hooks.GetField("DebugMaxCoordinates", Flags).GetValue(null) <= cap && (int)hooks.GetProperty("DebugScopeBuffers", Flags).GetValue(null, null) <= (int)hooks.GetField("MaximumRetainedScopes", Flags).GetRawConstantValue(), "native pressure keeps exact associations and retained scope slots under their explicit caps");
                Console.WriteLine("BlueprintWorldChanges pressure records=" + side * side + " associationCap=" + cap + " maxCoordinates=" + hooks.GetField("DebugMaxCoordinates", Flags).GetValue(null) + " maxDepth=" + hooks.GetField("DebugMaxDepth", Flags).GetValue(null) + " overflowScopes=" + hooks.GetField("DebugOverflowScopes", Flags).GetValue(null) + " fallbackWrites=" + hooks.GetField("DebugFallbackWrites", Flags).GetValue(null));
            }
            finally
            {
                Call(observer, "Unwatch", Focus);
                for (int x = 0; x < side; x++) for (int y = 0; y < side; y++) Main.tile[px + x, py + y] = saved[x, y];
                Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
            }
        }

        private static bool Hit(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        { return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom; }
        private static byte[] PressurePayload(int px, int py, int side, byte amount, byte tail, bool extraRecord)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write((ushort)(side * side + (extraRecord ? 1 : 0)));
                    for (int i = 0; i < side * side; i++) { writer.Write(((px + i % side) << 16) | (py + i / side)); writer.Write(i == 0 ? (byte)0 : i == side * side - 1 ? tail : amount); writer.Write((byte)0); }
                }
                return bytes.ToArray();
            }
        }

        private static void RoutedDependencies(object observer)
        {
            var bounds = new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10);
            var points = new[] { new Point(X, Y), new Point(X + 4, Y), new Point(X + 8, Y) };
            Call(observer, "Unwatch", Focus);
            foreach (Point point in points) Main.tile[point.X, point.Y].liquid = 0;
            Func<BlueprintProjectionBounds, ushort, bool> geometry = (changed, layers) =>
                (layers & 4) != 0 && (changed.Contains(points[0].X, points[0].Y) || changed.Contains(points[1].X, points[1].Y) || changed.Contains(points[2].X, points[2].Y));
            Action<BlueprintProjectionBounds, ushort, Action<int>> route = (changed, layers, emit) =>
            { if ((layers & 4) != 0) for (int i = 0; i < points.Length; i++) if (changed.Contains(points[i].X, points[i].Y)) emit(i); };
            Watch(observer, Focus, bounds, geometry, 4, 3, route);
            Require((int)Get(observer, "WatchCount") == 2, "three logical proofs reuse one existing physical watch");
            long before = Stamp(observer, Focus), far = Stamp(observer, Far);
            using (var reader = new BinaryReader(new MemoryStream(PointLiquidPayload(61, points[0], points[2])))) new NetLiquidModule().Deserialize(reader, 256);
            Require(!Current(observer, Focus, before) && Current(observer, Far, far) && Main.tile[points[0].X, Y].liquid == 61 && Main.tile[points[2].X, Y].liquid == 61 && Main.tile[points[1].X, Y].liquid == 0,
                "one real native batch changed only its two distant endpoint members");
            Require(DrainRoutes(observer).SetEquals(new[] { 0, 2 }), "native batch exit cannot route its union bbox across the untouched middle proof");
            Stamp(observer, Focus);
            for (int i = 0; i < 100; i++) using (var reader = new BinaryReader(new MemoryStream(PointLiquidPayload((byte)(64 + (i & 1)), points[0], points[2])))) new NetLiquidModule().Deserialize(reader, 256);
            Require(DrainRoutes(observer).SetEquals(new[] { 0, 2 }), "200 actual member changes coalesce into fixed proof bits without an event history or cross-product");
            before = Stamp(observer, Focus);
            using (var stream = new GatedStream(PointLiquidPayload(66, points[0], points[2])))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000) && !Current(observer, Focus, before), "first real routed member change revoked native proof before the controlled receive boundary");
                    Require(DrainRoutes(observer).SetEquals(new[] { 0 }), "in-flight drain delivers only the already changed endpoint");
                    long stamp; Require(!TryStamp(observer, Focus, out stamp), "routed drain never clears the original native pending gate");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "routed native receiver exited"); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Require(DrainRoutes(observer).SetEquals(new[] { 0, 2 }), "native finally republishes exact pending proof bits rather than recomputing the union rectangle");
            Stamp(observer, Focus);

            // Two genuine endpoint writes, then the declared third record is
            // truncated. Drain while the actual receiver is still in flight;
            // its exceptional finalizer must re-emit exactly those endpoints.
            byte[] partial = PointLiquidPayload(68, points[0], points[2], points[1]).Take(14).ToArray();
            using (var stream = new GatedStream(partial, partial.Length))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000), "truncated routed receiver actually applied both endpoints before its missing record");
                    Require(DrainRoutes(observer).SetEquals(new[] { 0, 2 }), "in-flight partial receive routes only actual endpoint writes");
                    long stamp; Require(!TryStamp(observer, Focus, out stamp), "partial routed receive remains pending after drain");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "truncated routed receiver exited through original exception handling"); }
                Require(failure is EndOfStreamException, "actual truncated routed receiver raises its original stream exception");
            }
            Require(DrainRoutes(observer).SetEquals(new[] { 0, 2 }) && Main.tile[points[0].X, Y].liquid == 68 && Main.tile[points[2].X, Y].liquid == 68 && Main.tile[points[1].X, Y].liquid == 0,
                "exceptional native finally retains partial writes and exact proof bits without inventing middle writes or rollback");
            Stamp(observer, Focus);

            // Exercise several fixed bitset words with real source points,
            // then the actual unknown-background boundary including padding.
            var many = new Point[130];
            Call(observer, "Unwatch", Focus);
            for (int i = 0; i < many.Length; i++) { many[i] = new Point(X - 1 + i % 13, Y - 1 + i / 13); Main.tile[many[i].X, many[i].Y].liquid = 0; }
            Func<BlueprintProjectionBounds, ushort, bool> manyGeometry = (changed, layers) =>
            { if ((layers & 4) == 0) return false; for (int i = 0; i < many.Length; i++) if (changed.Contains(many[i].X, many[i].Y)) return true; return false; };
            Action<BlueprintProjectionBounds, ushort, Action<int>> manyRoute = (changed, layers, emit) =>
            { if ((layers & 4) != 0) for (int i = 0; i < many.Length; i++) if (changed.Contains(many[i].X, many[i].Y)) emit(i); };
            Watch(observer, Focus, bounds, manyGeometry, 4, many.Length, manyRoute);
            Require((int)Get(observer, "WatchCount") == 2, "130 logical source proofs still reuse the same two physical watches");
            using (var reader = new BinaryReader(new MemoryStream(PointLiquidPayload(67, many[129])))) new NetLiquidModule().Deserialize(reader, 256);
            Require(DrainRoutes(observer).SetEquals(new[] { 129 }) && Main.tile[many[129].X, many[129].Y].liquid == 67, "actual last-word target change reaches its exact proof ordinal");
            Call(observer, "Unwatch", Far);
            using (var done = new ManualResetEventSlim(false))
            {
                WorldGen.TransformWorldOnBackgroundThread(() => done.Set(), null);
                Require(done.Wait(10000) && SpinWait.SpinUntil(() => !WorldGen.TransformingWorld, 10000), "actual unknown background boundary completed");
            }
            HashSet<int> all = DrainRoutes(observer);
            Require(all.Count == many.Length && all.All(i => i >= 0 && i < many.Length), "unknown background invalidates every declared proof without emitting padded bit ordinals");
            Stamp(observer, Focus);

            Call(observer, "Unwatch", Focus);
            Tile tile = Main.tile[X, Y] = new Tile { type = 1, wall = 1 }; tile.active(true);
            Watch(observer, Focus, bounds); Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
            Native(typeof(WorldGen), "paintTile", X, Y, (byte)15, false, false);
            Native(typeof(WorldGen), "paintWall", X, Y, (byte)10, false, false);
            Native(typeof(WorldGen), "PlaceWire", X, Y);
            int packets = 0; ushort changedLayers = 0;
            Drain(observer, (id, changed, layers) => { Require(id == Focus && changed.Contains(X, Y), "coarse diagnostic packet belongs to the real changed point"); packets++; changedLayers |= layers; });
            Require(packets == 1 && changedLayers == (1 | 2 | 16) && tile.color() == 15 && tile.wallColor() == 10 && tile.wire(), "coarse packet preserves the OR of actual Tile/Wall/Wire changes");
            Stamp(observer, Focus); Stamp(observer, Far);
            Console.WriteLine("PASS BlueprintWorldChanges routed proofs: native two-endpoint batch excludes middle, drain cannot clear pending, normal/exceptional finally exact re-emission, 200-write bounded merge, 130 ordinals/constant physical watches, unknown-background padding, actual layer OR.");
        }

        private static byte[] PointLiquidPayload(byte amount, params Point[] points)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                { writer.Write((ushort)points.Length); foreach (Point point in points) { writer.Write((point.X << 16) | point.Y); writer.Write(amount); writer.Write((byte)0); } }
                return bytes.ToArray();
            }
        }

        private static void CanonicalFrameTransactions(object observer, Assembly assembly)
        {
            PrepareLampWitness(observer, assembly, true);
            long far = Stamp(observer, Far);
            for (int i = 0; i < 20; i++)
            {
                long raw = Stamp(observer, Focus);
                Native(typeof(Wiring), "ToggleLamp", X, Y, Main.tile[X, Y], null, false);
                Require(!Current(observer, Focus, raw) && Current(observer, Far, far), "every genuine equivalent operating change revokes its raw token and preserves remote native proof");
                Require(!DrainFrameRoute(observer), "complete legal native frame transaction preserves an existing canonical witness without suppressing the raw route");
                Stamp(observer, Focus);
                for (int row = 0; row < 3; row++) Require(Main.tile[X, Y + row].frameX == ((i & 1) == 0 ? 18 : 0), "real native lamp changed all three members to the expected coherent phase");
            }
            NativeLampBurst(10); Require(!DrainFrameRoute(observer), "canonical native warmup keeps a bounded raw route"); Stamp(observer, Focus);
            AppDomain.MonitoringIsEnabled = true;
            var timer = System.Diagnostics.Stopwatch.StartNew(); long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            NativeLampBurst(1000); timer.Stop(); long witnessTicks = timer.ElapsedTicks, witnessBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated;
            Require(!DrainFrameRoute(observer), "1000 actual equivalent frame transactions coalesce without losing the complete witness"); Stamp(observer, Focus);

            Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far); NativeLampBurst(10);
            timer.Restart(); allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            NativeLampBurst(1000); timer.Stop(); long idleTicks = timer.ElapsedTicks, idleBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated;
            Require((int)Get(observer, "WatchCount") == 0, "no-watch frame mutation burst retains no observer proof");
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
            Console.WriteLine("BlueprintWorldChanges 1000 actual lamp transitions: witnessedTicks=" + witnessTicks + " witnessedAllocated=" + witnessBytes + " noWatchTicks=" + idleTicks + " noWatchAllocated=" + idleBytes + "; one finite three-member phase witness; structural microcheck, not frame/FPS evidence");

            PrepareLampWitness(observer, assembly, false);
            Native(typeof(Wiring), "ToggleLamp", X, Y, Main.tile[X, Y], null, false);
            Require(DrainFrameRoute(observer), "a legal native frame operation without a previously complete Match witness cannot manufacture canonical proof"); Stamp(observer, Focus);

            PrepareLampWitness(observer, assembly, true);
            Native(typeof(WorldGen), "paintTile", X, Y, (byte)3, false, false);
            Require(Main.tile[X, Y].color() == 3 && DrainFrameRoute(observer), "real material appearance change destroys the frame witness synchronously"); Stamp(observer, Focus);
            Native(typeof(Wiring), "ToggleLamp", X, Y, Main.tile[X, Y], null, false);
            Require(DrainFrameRoute(observer), "later legal frames cannot revive a witness invalidated by another semantic field"); Stamp(observer, Focus);

            BlueprintWorldObjectFrameWitness certificate = PrepareLampWitness(observer, assembly, true);
            long publishStamp = Stamp(observer, Focus);
            byte[] partial = LampSquarePayload(4, 18, 18, 18);
            using (var stream = new GatedStream(partial, partial.Length))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { ApplyLampSquare(partial, reader); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000), "real frame receiver completed all witnessed members before reaching a declared missing record");
                    Require(!DrainFrameRoute(observer), "in-flight complete member coverage remains only a provisional canonical candidate");
                    long stamp; Require(!TryStamp(observer, Focus, out stamp), "provisional canonical candidate never bypasses raw native pending");
                    Require(!(bool)Call(observer, "TrySetWitness", Focus, publishStamp, 0, certificate), "a stale complete certificate cannot be republished through an in-flight raw transaction");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "exceptional real frame receiver ended"); }
                Require(failure is EndOfStreamException && DrainFrameRoute(observer), "original receiver exception hard-invalidates even complete coherent frame coverage in its finally");
            }
            Stamp(observer, Focus);

            PrepareLampWitness(observer, assembly, true);
            byte[] payload = LampSquarePayload(3, 18, 18, 18);
            using (var stream = new GatedStream(payload, 17))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { ApplyLampSquare(payload, reader); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000) && !DrainFrameRoute(observer), "first original writer owns only its first witnessed member while pending");
                    using (var secondReader = new BinaryReader(new MemoryStream(payload))) ApplyLampSquare(payload, secondReader);
                    Require(DrainFrameRoute(observer), "a distinct real native root cannot borrow an in-flight writer's member coverage");
                    long stamp; Require(!TryStamp(observer, Focus, out stamp), "overlapping frame roots retain the original pending gate");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "overlapping real frame receiver ended"); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Require(DrainFrameRoute(observer), "normal final native state cannot certify a witness already destroyed by root/thread interleaving"); Stamp(observer, Focus);

            PrepareLampWitness(observer, assembly, true);
            payload = LampSquarePayload(3, 18, 0, 0);
            using (var reader = new BinaryReader(new MemoryStream(payload))) ApplyLampSquare(payload, reader);
            Require(DrainFrameRoute(observer), "partial/mixed member phase cannot preserve a completed canonical object proof"); Stamp(observer, Focus);

            PrepareLampWitness(observer, assembly, true);
            Call(observer, "Unwatch", Far);
            using (var done = new ManualResetEventSlim(false))
            {
                WorldGen.TransformWorldOnBackgroundThread(() => done.Set(), null);
                Require(done.Wait(10000) && SpinWait.SpinUntil(() => !WorldGen.TransformingWorld, 10000), "unknown original background transform completed");
            }
            Require(DrainFrameRoute(observer), "unknown actual background boundary destroys a published frame witness"); Stamp(observer, Focus);
            Native(typeof(Wiring), "ToggleLamp", X, Y, Main.tile[X, Y], null, false);
            Require(DrainFrameRoute(observer), "unknown invalidation cannot be repaired by a later operating transition without a fresh complete Match"); Stamp(observer, Focus);
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));

            Call(observer, "Unwatch", Focus);
            Tile tile = Main.tile[X, Y] = new Tile { type = 1, wall = 1 }; tile.active(true);
            Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
            Console.WriteLine("PASS BlueprintWorldChanges canonical frame transactions: 20 real ToggleLamp changes retain existing witness/raw revoke; unwitnessed/material/complete-but-exceptional/overlapping-root/partial-mixed/unknown changes hard invalidate; witness evaluation adds no metadata/world-source reads.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NativeLampBurst(int count)
        { for (int i = 0; i < count; i++) Wiring.ToggleLamp(X, Y, Main.tile[X, Y], null, false); }

        private static BlueprintWorldObjectFrameWitness PrepareLampWitness(object observer, Assembly assembly, bool install)
        {
            Call(observer, "Unwatch", Focus);
            for (int row = 0; row < 3; row++) { var tile = new Tile { type = 93, frameX = 0, frameY = (short)(row * 18) }; tile.active(true); Main.tile[X, Y + row] = tile; }
            var floor = new Tile { type = 1 }; floor.active(true); Main.tile[X, Y + 3] = floor;
            var bounds = new BlueprintProjectionBounds(X, Y, 1, 3);
            Func<BlueprintProjectionBounds, ushort, bool> geometry = (changed, layers) => (layers & 1) != 0 && changed.Left < X + 1 && X < changed.Right && changed.Top < Y + 3 && Y < changed.Bottom;
            Watch(observer, Focus, bounds, geometry, 1, 1, (changed, layers, emit) => emit(0));
            if (!install) return null;
            long stamp = Stamp(observer, Focus);
            Type type = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(type, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            var samples = new BlueprintWorldMatchSample[3]; var targets = new CaptureCell[3];
            for (int row = 0; row < 3; row++)
            {
                object[] read = { X, Y + row, null };
                Require((bool)type.GetMethod("TryRead", Flags).Invoke(source, read), "game-thread source read actual complete lamp member");
                samples[row] = (BlueprintWorldMatchSample)read[2]; targets[row] = samples[row].Cell; targets[row].ObjectId = 1;
            }
            object[] args = { samples[0].Object, targets, samples, true, null };
            Require((bool)type.GetMethod("TryCreateFrameWitness", Flags).Invoke(source, args) && args[4] != null, "real game-thread complete Match compiles the immutable frame witness");
            Require((bool)Call(observer, "TrySetWitness", Focus, stamp, 0, args[4]), "witness publication succeeds only at the exact current native stamp");
            return (BlueprintWorldObjectFrameWitness)args[4];
        }

        private static byte[] LampSquarePayload(int height, params short[] frames)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write((byte)20); writer.Write((short)X); writer.Write((short)Y); writer.Write((byte)1); writer.Write((byte)height); writer.Write((byte)0);
                    for (int row = 0; row < frames.Length; row++) { writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)93); writer.Write(frames[row]); writer.Write((short)(row * 18)); }
                }
                return stream.ToArray();
            }
        }
        private static void ApplyLampSquare(byte[] payload, BinaryReader reader)
        {
            var buffer = new MessageBuffer { whoAmI = 256, reader = reader };
            Array.Copy(payload, buffer.readBuffer, payload.Length); int kind; buffer.GetData(0, payload.Length, out kind); Require(kind == 20, "actual native frame TileSquare receiver dispatch");
        }

        private static void CanonicalDoorTransactions(object observer, Assembly assembly)
        {
            PrepareDoorWitness(observer, assembly, false, true, false, 0);
            long extra = Stamp(observer, Focus);
            Native(typeof(WorldGen), "PlaceWire2", X - 1, Y);
            Require(Main.tile[X - 1, Y].wire2() && Current(observer, Focus, extra), "an extra native wing wire is outside the door Tile certificate and does not stale its masked header seed");
            for (int cycle = 0; cycle < 12; cycle++)
            {
                int direction = (cycle & 1) == 0 ? -1 : 1; long raw = Stamp(observer, Focus);
                Require((bool)Native(typeof(WorldGen), "OpenDoor", X, Y, direction), "actual original door opened in the chosen legal direction");
                Require(!Current(observer, Focus, raw) && !DrainFrameRoute(observer), "complete original 1x3-to-2x3 occupancy transaction preserves only the already matched canonical door"); Stamp(observer, Focus);
                int left = direction < 0 ? X - 1 : X;
                for (int row = 0; row < 3; row++) for (int column = 0; column < 2; column++)
                {
                    Tile tile = Main.tile[left + column, Y + row];
                    Require(tile.active() && tile.type == 11 && tile.color() == row + 2 && tile.invisibleBlock() == (row != 1) && tile.fullbrightBlock() == (row == 1), "native open door copies each source row paint/coating into both actual members");
                }
                raw = Stamp(observer, Focus);
                Require((bool)Native(typeof(WorldGen), "CloseDoor", X, Y, true), "actual original door closed after its open footprint");
                Require(!Current(observer, Focus, raw) && !DrainFrameRoute(observer), "independent native closed-row random frames and inactive wing residuals remain a complete certified door pose"); Stamp(observer, Focus);
                for (int row = 0; row < 3; row++) Require(Main.tile[X, Y + row].active() && Main.tile[X, Y + row].type == 10 && !Main.tile[X + direction, Y + row].active(), "native closing restores the hinge and actually deactivates the selected wing");
            }
            NativeDoorBurst(10); Require(!DrainFrameRoute(observer), "native occupancy warmup still emits one bounded raw route"); Stamp(observer, Focus);
            var timer = System.Diagnostics.Stopwatch.StartNew(); long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            NativeDoorBurst(500); timer.Stop(); long witnessTicks = timer.ElapsedTicks, witnessBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated;
            Require(!DrainFrameRoute(observer), "1000 real alternating open/close transactions reuse a fixed nine-slot certificate without event history"); Stamp(observer, Focus);
            Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far); NativeDoorBurst(10);
            timer.Restart(); allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            NativeDoorBurst(500); timer.Stop(); long idleTicks = timer.ElapsedTicks, idleBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated;
            Require((int)Get(observer, "WatchCount") == 0, "no-watch original door operations own no certificate or pending state");
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
            Console.WriteLine("BlueprintWorldChanges 1000 actual door transitions: witnessedTicks=" + witnessTicks + " witnessedAllocated=" + witnessBytes + " noWatchTicks=" + idleTicks + " noWatchAllocated=" + idleBytes + "; one finite nine-slot occupancy witness; structural microcheck, not frame/FPS evidence");

            PrepareDoorWitness(observer, assembly, true, false, false, 36);
            for (int cycle = 0; cycle < 4; cycle++)
            {
                Require((bool)Native(typeof(WorldGen), "OpenDoor", X, Y, 1) && !DrainFrameRoute(observer), "one blocked wing does not prevent witnessed legal opening to the other side across a material page"); Stamp(observer, Focus);
                Require((bool)Native(typeof(WorldGen), "CloseDoor", X, Y, true) && !DrainFrameRoute(observer), "unused true solid wing remains extra world content while the opposite legal footprint closes"); Stamp(observer, Focus);
            }
            long before = Stamp(observer, Focus);
            Require(!(bool)Native(typeof(WorldGen), "OpenDoor", X, Y, -1) && Current(observer, Focus, before), "genuinely blocked original open attempt remains a no-op without fake occupancy or invalidation");

            PrepareDoorWitness(observer, assembly, false, false, false, 0);
            byte[] partial = DoorSquarePayload(3, 2, 0);
            using (var stream = new GatedStream(partial, partial.Length))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                var worker = new Thread(() => { try { ApplyLampSquare(partial, reader); } catch (Exception error) { failure = error; } }); worker.IsBackground = true; worker.Start();
                try
                {
                    Require(stream.Entered.Wait(10000) && !DrainFrameRoute(observer), "actual receiver completed a legal open door pose before its declared missing extra column");
                    for (int row = 0; row < 3; row++) for (int column = 0; column < 2; column++) Require(Main.tile[X - 1 + column, Y + row].active() && Main.tile[X - 1 + column, Y + row].type == 11 && Main.tile[X - 1 + column, Y + row].frameX == 36 + column * 18 && Main.tile[X - 1 + column, Y + row].color() == 0, "the actual pre-exception door footprint is complete and coherent");
                    long stamp; Require(!TryStamp(observer, Focus, out stamp), "a complete provisional door pose cannot bypass the raw pending gate");
                }
                finally { stream.Release.Set(); Require(worker.Join(10000), "exceptional occupancy receiver exited"); }
                Require(failure is EndOfStreamException && DrainFrameRoute(observer), "actual network exception hard-invalidates a complete legal door candidate in root finally");
            }
            Stamp(observer, Focus);

            PrepareDoorWitness(observer, assembly, false, false, false, 0);
            byte[] changedStyle = DoorSquarePayload(2, 2, 1);
            using (var reader = new BinaryReader(new MemoryStream(changedStyle))) ApplyLampSquare(changedStyle, reader);
            Require(DrainFrameRoute(observer), "a real complete network door with different material style cannot borrow the prior canonical certificate"); Stamp(observer, Focus);

            PrepareDoorWitness(observer, assembly, false, true, true, 0);
            Require((bool)Native(typeof(WorldGen), "OpenDoor", X, Y, -1), "real occupancy change also reaches another effective Tile target at its wing");
            var notices = new Dictionary<int, bool>();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, ordinal, hard) => { Require(id == Focus && !notices.ContainsKey(ordinal), "door and sibling notifications remain distinct actual ordinals"); notices.Add(ordinal, hard); }) });
            Require(notices.Count == 2 && !notices[0] && notices[1], "door equivalence cannot swallow a genuine write to a different effective same-layer proof"); Stamp(observer, Focus);

            // Regression: an ordinary foreign Tile can be framed while a
            // closed door remains matched. Its raw frame is still part of the
            // later occupancy certificate. A no-op network frame assignment
            // must not let an old raw seed certify a malformed open footprint.
            PrepareDoorWitness(observer, assembly, true, false, false, 0, true);
            Type sourceType = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            var targetCells = new CaptureCell[3]; BlueprintWorldObject target = null;
            for (int row = 0; row < 3; row++)
            {
                object[] read = { X, Y + row, null }; Require((bool)sourceType.GetMethod("TryRead", Flags).Invoke(source, read), "current closed target sampled before the genuine ordinary frame change");
                var sample = (BlueprintWorldMatchSample)read[2]; if (target == null) target = sample.Object; targetCells[row] = sample.Cell; targetCells[row].ObjectId = 1;
            }
            Tile foreign = Main.tile[X - 1, Y]; short seedFrame = foreign.frameX;
            Framing.SelfFrame8Way(X - 1, Y, foreign, false);
            Require(seedFrame == 36 && foreign.frameX == 0, "actual original ordinary framing changes a future left-wing raw seed from 36 to 0");
            notices.Clear();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, ordinal, hard) => { Require(id == Focus, "ordinary seed propagation stays in its physical watch"); notices[ordinal] = hard; }) });
            byte[] malformed = DoorSquarePayload(2, 2, 0, foreign.frameX);
            using (var reader = new BinaryReader(new MemoryStream(malformed))) ApplyLampSquare(malformed, reader);
            var actual = new BlueprintWorldMatchSample[6];
            for (int row = 0; row < 3; row++) for (int column = 0; column < 2; column++)
            {
                object[] read = { X - 1 + column, Y + row, null }; Require((bool)sourceType.GetMethod("TryRead", Flags).Invoke(source, read), "actual post-network malformed door samples read through the production gate"); actual[row * 2 + column] = (BlueprintWorldMatchSample)read[2];
            }
            BlueprintWorldMatchState actualState = BlueprintWorldMatchComparer.CompareObject(target, targetCells, actual, true);
            bool damagedHard = DrainFrameRoute(observer);
            Console.WriteLine("BlueprintWorldChanges ordinary seed malformed pose: frame=" + seedFrame + "->" + foreign.frameX + " earlierRoutes=" + notices.Count + " actual=" + actualState + " hard=" + damagedHard);
            Require(actualState == BlueprintWorldMatchState.Conflict && damagedHard, "stale ordinary raw frame seeds cannot certify a genuinely conflicting open footprint after a no-op frame assignment"); Stamp(observer, Focus);

            PrepareDoorWitness(observer, assembly, true, false, true, 0);
            Tile ordinaryWing = Main.tile[X - 1, Y]; before = Stamp(observer, Focus);
            short oldX = ordinaryWing.frameX, oldY = ordinaryWing.frameY;
            Framing.SelfFrame8Way(X - 1, Y, ordinaryWing, false);
            Console.WriteLine("BlueprintWorldChanges ordinary wing frame native old=" + oldX + "," + oldY + " new=" + ordinaryWing.frameX + "," + ordinaryWing.frameY + " rawCurrent=" + Current(observer, Focus, before));
            Require((ordinaryWing.frameX != oldX || ordinaryWing.frameY != oldY) && !Current(observer, Focus, before), "real ordinary framing still updates the raw seed of an existing door union witness");
            notices.Clear();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, ordinal, hard) => { Require(id == Focus && !notices.ContainsKey(ordinal), "ordinary raw frames notify only actual certificate dependencies"); notices.Add(ordinal, hard); }) });
            Require(notices.Count == 1 && !notices[0], "door raw-seed maintenance preserves both its canonical pose and the ordinary sibling whose matcher ignores raw frames");
            before = Stamp(observer, Focus);
            for (int i = 0; i < 100; i++) Framing.SelfFrame8Way(X - 1, Y, ordinaryWing, false);
            Require(Current(observer, Focus, before), "repeated no-op ordinary framing creates neither raw change nor pending/dirty just because a door witness exists");
            Require((bool)Native(typeof(WorldGen), "OpenDoor", X, Y, 1) && !DrainFrameRoute(observer), "a door witness with an updated ordinary foreign-wing raw seed still proves its next legal operation"); Stamp(observer, Focus);

            // No certificate exists yet. The Host acquires this one finite
            // lease before its first occupied/union read, not merely when it
            // eventually publishes a complete witness. Real ignored drawing
            // frames must invalidate that in-flight raw seed as well.
            BlueprintWorldDoorWitness unpublished = PrepareDoorWitness(observer, assembly, true, false, true, 0, false, false);
            Require(!(bool)Get(observer, "HasRawSeedInterests"), "unpublished Door factory values alone register no writer interest");
            before = Stamp(observer, Focus);
            var seedBounds = new BlueprintProjectionBounds(X - 1, Y, 3, 3);
            var seedLease = (IDisposable)Call(observer, "BeginDoorSeed", Focus, before, 0, seedBounds);
            Require(seedLease != null && Current(observer, Focus, before) && (bool)Get(observer, "HasRawSeedInterests"), "one first-read seed lease adds geometry only without dirtying a stable range");
            Require(Call(observer, "BeginDoorSeed", Focus, before, 0, seedBounds) == null, "a second raw seed lease cannot grow the single Host read workset");
            ordinaryWing = Main.tile[X - 1, Y];
            object[] firstSeed = { X - 1, Y, null };
            source = Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            Require((bool)sourceType.GetMethod("TryRead", Flags).Invoke(source, firstSeed), "production source takes the real first raw seed while the lease already owns it");
            // Another physical source can care about wire at the same point.
            // Its actual store must not enter a Door's Tile-only seed route.
            Watch(observer, Far, new BlueprintProjectionBounds(X - 1, Y, 1, 1),
                (Func<BlueprintProjectionBounds, ushort, bool>)((changed, layers) => (layers & 16) != 0), 16);
            long wireStamp = Stamp(observer, Far);
            Native(typeof(WorldGen), "PlaceWire", X - 1, Y);
            Require(ordinaryWing.wire() && Current(observer, Focus, before) && !Current(observer, Far, wireStamp), "a real sibling-watch wire store cannot make the Tile-only first Door seed pending or dirty");
            notices.Clear();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, ordinal, hard) => { Require(id == Far && ordinal == 0 && hard, "only the independent wire target receives its actual raw delta"); notices.Add(ordinal, hard); }) });
            Require(notices.Count == 1, "the active Door seed lease preserves exact changed-layer filtering");
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
            oldX = ordinaryWing.frameX; oldY = ordinaryWing.frameY;
            Framing.SelfFrame8Way(X - 1, Y, ordinaryWing, false);
            Require((oldX != ordinaryWing.frameX || oldY != ordinaryWing.frameY) && !Current(observer, Focus, before), "ordinary raw delta before the first certificate revokes its native stamp");
            Require(!(bool)Call(observer, "TrySetWitness", Focus, before, 0, unpublished), "the genuine stale first-read seed cannot publish a new occupancy certificate");
            notices.Clear();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, ordinal, hard) => { Require(id == Focus, "first-read lease is tied to its actual watch"); notices.Add(ordinal, hard); }) });
            Require(notices.Count == 1 && notices[0], "an uncertified raw seed is hard-dirty only at its own ordinal; ordinary sibling still ignores drawing frames");
            before = Stamp(observer, Focus);
            for (int i = 0; i < 100; i++) Framing.SelfFrame8Way(X - 1, Y, ordinaryWing, false);
            Require(Current(observer, Focus, before), "an active first-read lease does not dirty repeated actual no-op framing");
            seedLease.Dispose(); seedLease.Dispose();
            Require(!(bool)Get(observer, "HasRawSeedInterests") && Current(observer, Focus, before), "lease disposal is idempotent and releases its sole raw interest without dirtying");
            seedLease = (IDisposable)Call(observer, "BeginDoorSeed", Focus, before, 0, seedBounds);
            Require(seedLease != null, "a released bounded first-read lease can be reused");
            Call(observer, "Unwatch", Focus); seedLease.Dispose();
            Require(!(bool)Get(observer, "HasRawSeedInterests"), "retiring the native watch also releases an outstanding read lease");
            PrepareDoorWitness(observer, assembly, true, false, false, 0);
            before = Stamp(observer, Focus);
            seedLease = (IDisposable)Call(observer, "BeginDoorSeed", Focus, before, 0, seedBounds);
            Require(seedLease != null && (bool)Get(observer, "HasRawSeedInterests"), "published certificate and one in-flight seed share the same bounded ownership lifecycle");
            Call(observer, "Clear"); seedLease.Dispose();
            Require((int)Get(observer, "WatchCount") == 0 && !(bool)Get(observer, "HasRawSeedInterests"), "clearing the scene releases both published Door seeds and the current lease; late Dispose is harmless");
            PrepareDoorWitness(observer, assembly, false, false, false, 0);
            before = Stamp(observer, Focus);
            seedLease = (IDisposable)Call(observer, "BeginDoorSeed", Focus, before, 0, seedBounds);
            Require(seedLease != null, "one seed lease can coexist with the actual published Door at a stable native stamp");
            using (var stream = new GatedStream(LiquidPayload(1, 73, 2), 0))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                WorldGen.TransformWorldOnBackgroundThread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }, null);
                try
                {
                    long pendingStamp;
                    Require(stream.Entered.Wait(10000) && !TryStamp(observer, Focus, out pendingStamp) && DrainFrameRoute(observer), "the actual unknown background transformation revokes a published Door and gates its still-active raw seed before delegate writes");
                }
                finally { stream.Release.Set(); Require(SpinWait.SpinUntil(() => !WorldGen.TransformingWorld, 10000), "original background Door boundary releases its pending transformation"); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Require(DrainFrameRoute(observer), "background completion republishes the hard Door route rather than restoring its discarded certificate");
            seedLease.Dispose();
            Require(!(bool)Get(observer, "HasRawSeedInterests"), "unknown boundary discards the published raw seed, and lease disposal releases the final bounded interest");
            Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));

            Call(observer, "Unwatch", Focus);
            Tile normal = Main.tile[X, Y] = new Tile { type = 1, wall = 1 }; normal.active(true);
            Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
            Console.WriteLine("PASS BlueprintWorldChanges canonical door transactions: both original directions and random closed rows; true inactive residuals/row coatings/extra wire; blocked unused wing/material page; complete-but-exceptional occupancy/material mismatch/sibling effective Tile hard gate; real ordinary raw-frame seed propagation without sibling/no-op invalidation; first-read lease rejects stale factory publication and retires with its watch.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NativeDoorBurst(int cycles)
        {
            for (int i = 0; i < cycles; i++)
                if (!WorldGen.OpenDoor(X, Y, (i & 1) == 0 ? -1 : 1) || !WorldGen.CloseDoor(X, Y, true)) throw new InvalidOperationException("actual native door burst failed its legal open/close operation");
        }

        private static BlueprintWorldDoorWitness PrepareDoorWitness(object observer, Assembly assembly, bool blockLeft, bool paint, bool sibling, int style, bool ordinaryDoorSeed = false, bool publish = true)
        {
            Call(observer, "Unwatch", Focus);
            for (int row = 0; row < 3; row++) for (int column = -1; column <= 1; column++)
            {
                var tile = new Tile { type = 1, frameX = -1, frameY = -1 }; tile.color(12); tile.invisibleBlock(true); tile.fullbrightBlock(true);
                if (column == -1 && blockLeft) tile.active(true);
                if (column == -1 && ordinaryDoorSeed) { tile.frameX = 36; tile.frameY = (short)(row * 18); tile.color(0); tile.invisibleBlock(false); tile.fullbrightBlock(false); }
                if (column == 0)
                {
                    tile.type = 10; tile.frameX = (short)(style / 36 * 54 + row * 18); tile.frameY = (short)(style % 36 * 54 + row * 18); tile.active(true);
                    tile.color((byte)(paint ? row + 2 : 0)); tile.invisibleBlock(paint && row != 1); tile.fullbrightBlock(paint && row == 1);
                }
                Main.tile[X + column, Y + row] = tile;
            }
            for (int column = -1; column <= 1; column++) foreach (int row in new[] { -1, 3 }) { var solid = new Tile { type = 1 }; solid.active(true); Main.tile[X + column, Y + row] = solid; }
            var bounds = new BlueprintProjectionBounds(X - 1, Y, 3, 3);
            Func<BlueprintProjectionBounds, ushort, bool> geometry = (changed, layers) => (layers & 1) != 0 && changed.Left < X + 2 && X - 1 < changed.Right && changed.Top < Y + 3 && Y < changed.Bottom;
            Watch(observer, Focus, bounds, geometry, 1, sibling ? 2 : 1, (changed, layers, emit) => { emit(0); if (sibling && changed.Contains(X - 1, Y)) emit(1); });
            long stamp = Stamp(observer, Focus);
            Type type = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(type, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            var occupied = new BlueprintWorldMatchSample[3]; var union = new BlueprintWorldMatchSample[9]; var targets = new CaptureCell[3];
            for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++)
            {
                object[] read = { X - 1 + column, Y + row, null };
                Require((bool)type.GetMethod("TryRead", Flags).Invoke(source, read), "same game-thread gate reads all nine actual occupancy values, including inactive residuals");
                union[row * 3 + column] = (BlueprintWorldMatchSample)read[2];
                if (column == 1) { occupied[row] = union[row * 3 + column]; targets[row] = occupied[row].Cell; targets[row].ObjectId = 1; }
            }
            object[] args = { occupied[0].Object, targets, occupied, union, true, null };
            Require((bool)type.GetMethod("TryCreateDoorWitness", Flags).Invoke(source, args) && args[5] != null, "actual complete door Match and true union seed compile an occupancy witness");
            if (publish) Require((bool)Call(observer, "TrySetWitness", Focus, stamp, 0, args[5]), "door witness publishes at the current native gate only");
            return (BlueprintWorldDoorWitness)args[5];
        }

        private static byte[] DoorSquarePayload(int declaredWidth, int columns, int style, short? firstWingFrame = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write((byte)20); writer.Write((short)(X - 1)); writer.Write((short)Y); writer.Write((byte)declaredWidth); writer.Write((byte)3); writer.Write((byte)0);
                    for (int column = 0; column < columns; column++) for (int row = 0; row < 3; row++)
                    { writer.Write((byte)1); writer.Write((byte)4); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)11); writer.Write(column == 0 && row == 0 && firstWingFrame.HasValue ? firstWingFrame.Value : (short)(style / 36 * 72 + 36 + column * 18)); writer.Write((short)(style % 36 * 54 + row * 18)); }
                }
                return stream.ToArray();
            }
        }

        private static void BackgroundNoWatchBoundary(object observer)
        {
            Call(observer, "Unwatch", Focus); Call(observer, "Unwatch", Far);
            // This real receiver has ALREADY entered its native method with
            // zero watches. Installing a watch while it is blocked must not
            // make that old early-return scope safe to sample retroactively.
            using (var stream = new GatedStream(LiquidPayload(1, 51, 2), 0))
            using (var reader = new BinaryReader(stream))
            {
                Exception failure = null;
                WorldGen.TransformWorldOnBackgroundThread(() => { try { new NetLiquidModule().Deserialize(reader, 256); } catch (Exception error) { failure = error; } }, null);
                try
                {
                    Require(stream.Entered.Wait(10000) && WorldGen.TransformingWorld, "actual background-transform delegate entered its native receiver with zero watches");
                    Watch(observer, Focus, new BlueprintProjectionBounds(X - 1, Y - 1, 14, 10));
                    Watch(observer, Far, new BlueprintProjectionBounds(90, 90, 1, 1));
                    long stamp;
                    Require(!TryStamp(observer, Focus, out stamp), "new watch cannot sample through the original pending-transform counter");
                    bool ran = false; Require(!(bool)Call(observer, "TryRunStable", Focus, stamp, (Action)(() => ran = true)) && !ran, "new-watch completion is gated even though the writer entered before it existed");
                }
                finally { stream.Release.Set(); Require(SpinWait.SpinUntil(() => !WorldGen.TransformingWorld, 10000), "actual background delegate and original pending counter retired"); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Drain(observer); Stamp(observer, Focus); Stamp(observer, Far);
            Require(Main.tile[X + 2, Y].liquid == 51 && Main.tile[X + 2, Y].liquidType() == 2, "new watch becomes eligible for its initial fresh read only after the actual background write");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NativePaintBurst(int count)
        { for (int i = 0; i < count; i++) WorldGen.paintTile(X, Y, 11, false, false); }

        private static void Changed(object observer, Action action, string reason)
        {
            long before = Stamp(observer, Focus), far = Stamp(observer, Far); action();
            Require(!Current(observer, Focus, before), reason + " revokes old proof at the real native writer");
            Require(Current(observer, Far, far), reason + " preserves unrelated range epoch");
            Require(Drain(observer) == 1, reason + " merges related invalidation into one bounded watch event"); Stamp(observer, Focus);
        }
        private static int Drain(object observer)
        { int count = 0; Drain(observer, (Action<long, BlueprintProjectionBounds>)((id, bounds) => { Require(id == Focus || id == Far, "only live fixture dependencies notified"); count++; })); return count; }
        private static void Drain(object observer, Action<long, BlueprintProjectionBounds> consume)
        { observer.GetType().GetMethod("Drain", Flags, null, new[] { typeof(Action<long, BlueprintProjectionBounds>) }, null).Invoke(observer, new object[] { consume }); }
        private static void Drain(object observer, Action<long, BlueprintProjectionBounds, ushort> consume)
        { observer.GetType().GetMethod("Drain", Flags, null, new[] { typeof(Action<long, BlueprintProjectionBounds, ushort>) }, null).Invoke(observer, new object[] { consume }); }
        private static HashSet<int> DrainRoutes(object observer)
        {
            var routes = new HashSet<int>();
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int>) }, null).Invoke(observer,
                new object[] { (Action<long, int>)((id, route) => { Require(id == Focus && routes.Add(route), "only one notification per current physical watch/proof ordinal"); }) });
            return routes;
        }
        private static bool DrainFrameRoute(object observer)
        {
            int count = 0; bool hard = false;
            observer.GetType().GetMethod("DrainRoutes", Flags, null, new[] { typeof(Action<long, int, bool>) }, null).Invoke(observer,
                new object[] { (Action<long, int, bool>)((id, route, dirty) => { Require(id == Focus && route == 0, "frame route belongs only to its declared proof"); count++; hard |= dirty; }) });
            Require(count == 1, "actual frame transaction always emits its raw route, including canonical-equivalent frames"); return hard;
        }
        // The shared native Call helper deliberately resolves by name only.
        // Watch has distinct geometry/layer contracts, so this fixture must
        // select their real signatures rather than rely on a local adapter's
        // more permissive overload resolution.
        private static void Watch(object observer, long id, BlueprintProjectionBounds bounds)
        { InvokeWatch(observer, new[] { typeof(long), typeof(BlueprintProjectionBounds) }, new object[] { id, bounds }); }
        private static void Watch(object observer, long id, BlueprintProjectionBounds bounds, Func<BlueprintProjectionBounds, bool> geometry)
        { InvokeWatch(observer, new[] { typeof(long), typeof(BlueprintProjectionBounds), typeof(Func<BlueprintProjectionBounds, bool>) }, new object[] { id, bounds, geometry }); }
        private static void Watch(object observer, long id, BlueprintProjectionBounds bounds, Func<BlueprintProjectionBounds, ushort, bool> geometry, ushort layers)
        { InvokeWatch(observer, new[] { typeof(long), typeof(BlueprintProjectionBounds), typeof(Func<BlueprintProjectionBounds, ushort, bool>), typeof(ushort) }, new object[] { id, bounds, geometry, layers }); }
        private static void Watch(object observer, long id, BlueprintProjectionBounds bounds, Func<BlueprintProjectionBounds, ushort, bool> geometry, ushort layers,
            int routeCount, Action<BlueprintProjectionBounds, ushort, Action<int>> route)
        { InvokeWatch(observer, new[] { typeof(long), typeof(BlueprintProjectionBounds), typeof(Func<BlueprintProjectionBounds, ushort, bool>), typeof(ushort), typeof(int), typeof(Action<BlueprintProjectionBounds, ushort, Action<int>>) }, new object[] { id, bounds, geometry, layers, routeCount, route }); }
        private static void InvokeWatch(object observer, Type[] signature, object[] arguments)
        {
            MethodInfo method = observer.GetType().GetMethod("Watch", Flags, null, signature, null);
            Require(method != null, "actual observer exposes the exact watch geometry/layer contract");
            method.Invoke(observer, arguments);
        }
        private static bool TryStamp(object observer, long id, out long stamp)
        { object[] args = { id, 0L }; bool result = (bool)observer.GetType().GetMethod("TryGetStamp", Flags).Invoke(observer, args); stamp = (long)args[1]; return result; }
        private static long Stamp(object observer, long id)
        { long value; Require(TryStamp(observer, id, out value), "fixture range ready after native write/finalizer and drain"); return value; }
        private static bool Current(object observer, long id, long stamp) { return (bool)Call(observer, "IsCurrent", id, stamp); }
        private static object Native(Type type, string name, params object[] args)
        {
            MethodInfo method = type.GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { if (error.InnerException != null) ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static byte[] LiquidPayload(ushort count, byte amount, byte kind)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                { writer.Write(count); writer.Write(((X + 2) << 16) | Y); writer.Write(amount); writer.Write(kind); if (count == 2) { writer.Write(((X + 3) << 16) | Y); writer.Write((byte)24); writer.Write((byte)0); } }
                return stream.ToArray();
            }
        }
        private sealed class GatedStream : MemoryStream
        {
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false), Release = new ManualResetEventSlim(false);
            private readonly long gateAt;
            internal GatedStream(byte[] bytes, long gateAt = 8) : base(bytes) { this.gateAt = gateAt; }
            private void Gate() { if (Position < gateAt || Release.IsSet) return; Entered.Set(); if (!Release.Wait(10000)) throw new TimeoutException("controlled native receive release"); }
            public override int Read(byte[] buffer, int offset, int count) { Gate(); return base.Read(buffer, offset, count); }
            public override int ReadByte() { Gate(); return base.ReadByte(); }
            protected override void Dispose(bool disposing) { if (disposing) { Entered.Dispose(); Release.Dispose(); } base.Dispose(disposing); }
        }
    }
}
