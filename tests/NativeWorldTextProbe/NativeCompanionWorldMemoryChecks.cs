using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.IO;
using Terraria.Net;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionWorldMemoryChecks
    {
        internal static void Run(object host, Player human, Player first, Player second, Action tick)
        {
            var memory = Get(host, "SharedWorldMemory");
            var originalWorld = Main.ActiveWorldFileData;
            var originalTiles = Main.tile;
            var firstFeet = first.Bottom;
            var secondFeet = second.Bottom;
            var humanFeet = human.Bottom;
            var firstVelocity = first.velocity;
            var secondVelocity = second.velocity;
            var oldSource = second.inventory[21];
            var firstActor = Call(host, "Owned", first);
            var secondActor = Call(host, "Owned", second);
            var firstProfile = (CompanionProfile)Get(firstActor, "Profile");
            var secondProfile = (CompanionProfile)Get(secondActor, "Profile");
            var oldDrops = new HashSet<WorldItem>(Main.item.Where(item => item?.active == true));
            uint tickCount = 0;
            void Pump() => Call(memory, "Update", (ulong)++tickCount);
            void Until(Func<bool> completed) => NativeQuickItemChecks.Until(() => { Pump(); return completed(); });
            CompanionTaskOutcome Read(string reference) => (CompanionTaskOutcome)Call(host, "ReadWorldMemory", reference);
            CompanionTaskOutcome Material(string layer, int item, bool report = false) =>
                (CompanionTaskOutcome)Call(host, "QueryWorldMaterials", layer, item, new Point(77, 54), 0, report);
            void Rotate() { Call(memory, "OnSessionEnded"); Call(memory, "OnSessionStarted"); Pump(); }
            CompanionTaskIdentity Identity(object actor, CompanionProfile profile) => new CompanionTaskIdentity(profile.OwnerId, profile.Id,
                (long)Get(host, "session"), (long)Get(actor, "Generation"), profile.DataEpoch);
            void SetPool(byte liquid)
            {
                for (int y = 47; y < 55; y++)
                    for (int x = 79; x <= 84; x++)
                    {
                        var tile = new Tile { liquid = liquid };
                        tile.shimmer(true);
                        Main.tile[x, y] = tile;
                    }
            }
            string ObservePool()
            {
                var observation = (CompanionTaskOutcome)Call(host, "TryGetTaskObservation", Identity(firstActor, firstProfile),
                    "observe_tiles", new Dictionary<string, object> { ["around"] = "companion", ["radius"] = 4, ["liquid"] = "shimmer" },
                    Call(host, "CaptureTaskSendAnchor", human));
                string row = observation.Detail.Split('；').FirstOrDefault(text => text.StartsWith("(79,"));
                Require(row != null, "first actor saw original pool: " + observation.Detail);
                return row.Substring(row.LastIndexOf("· ", StringComparison.Ordinal) + 2).Split(' ')[0];
            }
            CompanionTaskSnapshot Shimmer(string reference, bool drainOnDeparture)
            {
                var step = new CompanionTaskStep("shimmer_item", new Dictionary<string, object> {
                    ["target"] = reference, ["item_type"] = (int)ItemID.AngelStatue, ["amount"] = 1 });
                var job = (CompanionTaskSnapshot)Call(host, "SubmitTask", Identity(secondActor, secondProfile),
                    "去上次那个微光池，把这件物品丢进去", new[] { step }, Call(host, "CaptureTaskSendAnchor", human), Guid.NewGuid());
                Require(job.State == "accepted", "second actor accepts a shared location as an executable native destination");
                for (int frame = 0; frame < 210 && job.IsActive; frame++)
                {
                    tick();
                    if (drainOnDeparture && frame == 0) SetPool(0);
                    for (int i = 0; i < Main.maxItems; i++)
                        if (Main.item[i]?.active == true && !oldDrops.Contains(Main.item[i])) Main.item[i].UpdateItem(i);
                    Call(host, "AdvanceTaskShimmerDrops");
                    job = (CompanionTaskSnapshot)Call(host, "GetTask", secondProfile.OwnerId, secondProfile.Id, job.TaskId);
                }
                return job;
            }

            try
            {
                Main.tile = (Tile[,])originalTiles.Clone();
                for (int x = 51; x <= 86; x++)
                    for (int y = 46; y <= 56; y++) Main.tile[x, y] = y == 55 ? Solid(TileID.Stone) : new Tile();
                SetPool(255);
                Main.tile[105, 30] = Solid(TileID.Iron);
                Main.tile[106, 30] = new Tile { wall = WallID.Stone };
                first.Bottom = second.Bottom = new Vector2(77 * 16 + 8, 55 * 16);
                first.velocity = second.velocity = Vector2.Zero;
                Call(Get(firstActor, "Behavior"), "PositionChanged", first);
                Call(Get(secondActor, "Behavior"), "PositionChanged", second);
                Rotate();
                int cells = Main.maxTilesX * Main.maxTilesY;
                long maximumRead = 0;
                NativeQuickItemChecks.Until(() =>
                {
                    var current = GetOptional(memory, "live");
                    long beforeRead = current == null ? 0 : (long?)GetOptional(current, "CellsRead") ?? 0;
                    Pump();
                    current = GetOptional(memory, "live");
                    long afterRead = current == null ? 0 : (long?)GetOptional(current, "CellsRead") ?? 0;
                    maximumRead = Math.Max(maximumRead, afterRead - beforeRead);
                    return Material("tile", TileID.Iron).Detail.Contains(cells + "/" + cells);
                });
                Require(Material("tile", TileID.Iron).Completed == 1 && Material("wall", WallID.Stone).Completed == 1 &&
                    Material("liquid", 3).Completed == 48, "automatic no-chat full scan reports actual unrevealed material layers");
                var session = Get(memory, "live");
                int revision = (int)Get(session, "Revision");
                long? initialReads = (long?)GetOptional(session, "CellsRead");
                for (int i = 0; i < 60; i++) { Material("tile", TileID.Iron); Pump(); }
                Require((int)Get(session, "Revision") == revision && ((ICollection)Get(session, "Work")).Count == 0 &&
                    GetOptional(session, "Aggregate") == null && GetOptional(session, "Capture") == null &&
                    (!initialReads.HasValue || (long?)GetOptional(session, "CellsRead") == initialReads && maximumRead <= 768 && initialReads == cells),
                    "idle queries reuse one index and original-cell reads obey the per-update production budget");
                if (initialReads.HasValue)
                    Console.WriteLine("WORKLOAD world-memory full cells=" + initialReads + " maxReadsPerUpdate=" + maximumRead +
                        " idleUpdates=60 extraReads=0 (isolated original tile array, no FPS claim).");
                CheckLocalizedMaterialName(host, tick);
                var report = Material("tile", TileID.Iron, report: true);
                Require(report.State == "waiting" && report.Value?.StartsWith("memory:", StringComparison.Ordinal) == true,
                    "a material report enters the real world document commit");
                Until(() => Read(report.Value).State == "succeeded");
                Require(Read(report.Value).Detail.Contains("类型 " + TileID.Iron + " 共 1 格"), "readable report preserves the exact original mineral count");
                WorldGen.KillTile(105, 30, noItem: true);
                Require(!Main.tile[105, 30].active(), "fixture uses the original mine mutation");
                Until(() => Material("tile", TileID.Iron).Detail.Contains(cells + "/" + cells));
                Require(Material("tile", TileID.Iron).Completed == 0, "native mutation replaces the old page contribution");

                first.Bottom = new Vector2(77 * 16 + 8, 55 * 16);
                first.velocity = Vector2.Zero;
                Call(Get(firstActor, "Behavior"), "PositionChanged", first);
                string witnessed = ObservePool();
                var write = (CompanionTaskOutcome)Call(host, "WriteWorldMemory", firstProfile.Id, null, 0,
                    "西侧矿道微光池", "从东边下去方便", witnessed, "shimmer", null);
                Require(write.State == "waiting" && write.Value?.StartsWith("memory:", StringComparison.Ordinal) == true,
                    "first actor submits an actual observed pool, not its own temporary reference");
                Until(() => Read(write.Value).State == "succeeded");
                var raw = (string)Get(memory, "root");
                string doc = Directory.GetFiles(raw, "world.md", SearchOption.AllDirectories)
                    .Single(path => File.ReadAllText(path).Contains("西侧矿道微光池"));
                Require(File.ReadAllText(doc).Contains("从东边下去方便"), "fact and human-readable note appear in the real archive");
                int savedRevision = Read(write.Value).Completed;
                var renamed = (CompanionTaskOutcome)Call(host, "WriteWorldMemoryWithAliases", secondProfile.Id, write.Value, savedRevision,
                    "矿道东口的微光池", null, null, null, null, "西池、东侧小路");
                Require(renamed.State == "waiting", "second actor can rename and attach aliases to an existing shared fact");
                var addedRoute = (CompanionTaskOutcome)Call(host, "WriteWorldMemory", firstProfile.Id, write.Value, savedRevision,
                    null, "从东边下去方便，记得沿阶梯走", null, null, null);
                Require(addedRoute.State == "waiting" && addedRoute.Completed > renamed.Completed,
                    "different fields from two actors merge even while the first file write is still pending");
                Until(() => Read(write.Value).Completed == addedRoute.Completed && Read(write.Value).State == "succeeded");
                var byAlias = (CompanionTaskOutcome)Call(host, "FindWorldMemory", "东侧小路", "shimmer", new Point(77, 54), 4);
                var staleEdit = (CompanionTaskOutcome)Call(host, "WriteWorldMemory", firstProfile.Id, write.Value, savedRevision,
                    "旧模型的冲突名字", null, null, null, null);
                Require(byAlias.Value == write.Value && staleEdit.State == "conflict" && Read(write.Value).Detail.Contains("沿阶梯走"),
                    "alias lookup keeps the original stable id, nonconflicting route survives, and stale title conflicts");
                long before = (long)Get(memory, "Epoch");
                Rotate();
                Until(() => Read(write.Value).State == "succeeded");
                Require((long)Get(memory, "Epoch") > before && Read(write.Value).Detail.Contains("从东边下去方便"),
                    "reopening the same world reloads the same note and durable identifier");

                first.Bottom = human.Bottom = new Vector2(63 * 16 + 8, 55 * 16);
                second.Bottom = new Vector2(77 * 16 + 8, 55 * 16);
                first.velocity = Vector2.Zero;
                second.velocity = Vector2.Zero;
                Call(Get(firstActor, "Behavior"), "PositionChanged", first);
                Call(Get(secondActor, "Behavior"), "PositionChanged", second);
                second.inventory[21] = new Item(ItemID.AngelStatue) { stack = 2 };
                int product = ShimmerTransforms.GetTransformToItem(ItemID.AngelStatue);
                var completed = Shimmer(write.Value, false);
                Require(completed.State == "succeeded" && completed.Attempts.Last().Completed == 1 && second.inventory[21].stack == 1 &&
                    Main.item.Any(item => item?.active == true && !oldDrops.Contains(item) && item.shimmered && item.type == product),
                    "second actor converts exactly one real item at the first actor's persisted pool: " + completed.Detail +
                    " · body " + second.Bottom + " · drops " + String.Join(";", Main.item.Where(item => item?.active == true &&
                        !oldDrops.Contains(item)).Take(12).Select(item => item.type + "@" + item.position + "/" + item.velocity + "/wet=" + item.shimmerWet)));

                // Depart from outside the eventual dry check radius; the
                // second actor must physically approach before correcting it.
                second.Bottom = new Vector2(65 * 16 + 8, 55 * 16);
                second.velocity = Vector2.Zero;
                Call(Get(secondActor, "Behavior"), "PositionChanged", second);
                var dry = Shimmer(write.Value, true);
                Require(dry.State == "failed" && second.inventory[21].stack == 1 && dry.Detail.Contains("干涸"),
                    "drain during approach never debits the second actor and reports the original pool: " + dry.Detail);
                Until(() => Read(write.Value).State == "succeeded" && Read(write.Value).Detail.Contains("已失效"));
                Require(File.ReadAllText(doc).Contains("已失效"), "the archive's verified dry correction is present in readable Markdown");
                File.AppendAllText(doc, "外部内容，隔离夹具\n");
                Rotate();
                Until(() => Read(write.Value).State == "failed");
                Require(File.ReadAllText(doc).EndsWith("外部内容，隔离夹具\n", StringComparison.Ordinal),
                    "externally changed world document is protected without erasing human bytes");
                Main.ActiveWorldFileData = new WorldFileData(Path.Combine(Path.GetDirectoryName(originalWorld.Path), "fixture-branch.wld"), false)
                    { UniqueId = originalWorld.UniqueId };
                Rotate();
                Until(() => Read(write.Value).State == "failed");
                Require(Read(write.Value).Detail.Contains("找不到"), "same GUID at a different local source does not inherit the other archive");
                var otherWorld = new WorldFileData(originalWorld.Path, false) { UniqueId = Guid.NewGuid() };
                Main.ActiveWorldFileData = otherWorld;
                Rotate();
                Until(() => Read(write.Value).State == "failed");
                Require(Read(write.Value).State == "failed", "same world filename and title with a different GUID cannot find the old note");
                Console.WriteLine("PASS world memory: 3 material layers, unrevealed iron, native mine invalidation, stable no-chat reuse, alias/revision conflict, " +
                    first.name + " recorded " + write.Value + ", " + second.name + " reopened and converted Angel Statue to " +
                    product + ", then dry arrival kept one and saved invalidation; external file retained; different GUID isolated.");

                GuestLiquid(memory, Pump, Until, Material);
                Call(memory, "OnSessionStarted");
                VerifyConnectedPool(memory, Main.ActiveWorldFileData, Rotate, Until);
                VerifyPartialCacheRestart(memory, originalWorld, Rotate, Until, Material);
            }
            finally
            {
                Call(host, "StopTask", secondProfile.OwnerId, secondProfile.Id, null);
                Call(memory, "OnSessionEnded");
                Main.ActiveWorldFileData = originalWorld;
                Main.tile = originalTiles;
                first.Bottom = firstFeet; second.Bottom = secondFeet; human.Bottom = humanFeet;
                first.velocity = firstVelocity; second.velocity = secondVelocity;
                Call(Get(firstActor, "Behavior"), "PositionChanged", first);
                Call(Get(secondActor, "Behavior"), "PositionChanged", second);
                second.inventory[21] = oldSource;
                for (int i = 0; i < Main.maxItems; i++)
                    if (Main.item[i]?.active == true && !oldDrops.Contains(Main.item[i])) Main.item[i].TurnToAir();
                Call(memory, "OnSessionStarted");
            }
        }

        private static Tile Solid(int type)
        {
            var tile = new Tile { type = (ushort)type };
            tile.active(true);
            return tile;
        }

        private static void CheckLocalizedMaterialName(object host, Action tick)
        {
            // The later chat case deliberately starts its own original shared
            // catalog cold. This case uses the same production catalog owner
            // with a separate snapshot and real native sample preparation.
            var original = Get(host, "ChatKnowledge");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var clock = (Func<long>)Get(Get(original, "Native"), "placementClock");
            var isolated = Activator.CreateInstance(original.GetType(), flags, null, new object[] { clock }, null);
            try
            {
                Call(Get(isolated, "Native"), "BindPlacementClock", clock);
                Set(host, "ChatKnowledge", isolated);
                object[] named = { "tile", "铁矿", -1 };
                NativeQuickItemChecks.Until(() =>
                {
                    string state = (string)Call(host, "ResolveWorldMaterialType", named);
                    if (state == "waiting") { tick(); Call(isolated, "StepChat"); return false; }
                    Require(state == "ready" && (int)named[2] == TileID.Iron, "Chinese Iron Ore resolves to the native tile ID: " + state);
                    return true;
                });
            }
            finally
            {
                Set(host, "ChatKnowledge", original);
                ((IDisposable)isolated).Dispose();
            }
        }

        private static void VerifyConnectedPool(object memory, WorldFileData world, Action rotate, Action<Func<bool>> until)
        {
            Main.ActiveWorldFileData = world;
            rotate();
            until(() => GetOptional(memory, "live") is object session && GetOptional(session, "Saved") != null);
            void Fill(Rectangle bounds, bool wet)
            {
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                    for (int x = bounds.Left; x < bounds.Right; x++)
                    {
                        var tile = new Tile { liquid = wet ? (byte)255 : (byte)0 };
                        tile.shimmer(wet);
                        Main.tile[x, y] = tile;
                    }
            }
            var pool = new Rectangle(20, 68, 30, 30);
            Fill(pool, true);
            var left = new Point(20, 68);
            var right = new Point(49, 97);
            Require(Call(memory, "ObservePlace", "shimmer", left, true) == null &&
                Call(memory, "ObservePlace", "shimmer", right, true) == null,
                "automatic 512-cell work leaves a 900-cell component ungrouped");
            var first = Call(memory, "ObservePlace", "shimmer", left, false);
            var second = Call(memory, "ObservePlace", "shimmer", right, false);
            Require(first != null && second != null && (string)Get(first, "Reference") == (string)Get(second, "Reference"),
                "two distant original cells of the 30 by 30 artificial pool resolve to one trustworthy ID");
            var saved = (CompanionTaskOutcome)Call(memory, "SavePlace", null, 0, "独审大池", "", "shimmer", null, first, null);
            Require(saved.State == "waiting", "pond geometry enters the actual archive");
            until(() => ((CompanionTaskOutcome)Call(memory, "ReadPlace", saved.Value)).State == "succeeded");
            rotate();
            until(() => ((CompanionTaskOutcome)Call(memory, "ReadPlace", saved.Value)).State == "succeeded");
            object[] args = { saved.Value, null };
            Require((bool)Call(memory, "TryGetPlace", args), "the exact old pond is recovered from a new session");
            var reopened = args[1];
            var close = new Rectangle(20, 68, 11, 11);
            Fill(close, false);
            object[] recheck = { reopened, left.ToWorldCoordinates(), null, false };
            Require(!(bool)Call(memory, "RecheckShimmer", recheck) && !(bool)recheck[3] && Main.tile[right.X, right.Y].liquid > 0,
                "full old footprint prevents declaring a locally dry edge an exhausted pond");
            var remaining = Call(memory, "ObservePlace", "shimmer", right, false);
            Require(remaining != null && (string)Get(remaining, "Reference") == saved.Value,
                "distant remaining cells retain the original pond identity");
            Fill(pool, false);
            Fill(new Rectangle(52, 68, 2, 2), true); // An unrelated nearby pond does not settle this one.
            Require(!(bool)Call(memory, "RecheckShimmer", recheck) && (bool)recheck[3] && Main.tile[52, 68].liquid == 255,
                "complete old pond dryness is recorded without selecting the disconnected wet neighbor");
            until(() =>
            {
                var read = (CompanionTaskOutcome)Call(memory, "ReadPlace", saved.Value);
                return read.State == "succeeded" && read.Detail.Contains("已失效");
            });
            Console.WriteLine("PASS actual shimmer connected footprint 900 cells, same ID on opposite edges, partial drain unknown, full drain persisted without switching pools.");
        }

        private static void VerifyPartialCacheRestart(object memory, WorldFileData originalWorld, Action rotate, Action<Func<bool>> until,
            Func<string, int, bool, CompanionTaskOutcome> material)
        {
            string path = Path.Combine(Path.GetDirectoryName(originalWorld.Path), "partial-index-fixture.wld");
            Main.ActiveWorldFileData = new WorldFileData(path, false) { UniqueId = Guid.NewGuid() };
            var privateHooks = memory.GetType().Assembly.GetType("JueMingR.TerrariaHost.World.WorldMemoryHooks", true);
            var witness = privateHooks.GetMethod("ReadWorldBytes", BindingFlags.Static | BindingFlags.NonPublic);
            var metadata = Main.WorldFileMetadata;
            void WriteNativeTerrain()
            {
                Main.WorldFileMetadata = FileMetadata.FromCurrentSettings(FileType.World);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    // The native header, pointer and tile writers produce the
                    // same terrain section consumed by the source witness.
                    int header = WorldFile.SaveFileFormatHeader(writer);
                    writer.Write((byte)0);
                    int start = (int)writer.BaseStream.Position;
                    int end = WorldFile.SaveWorldTiles(writer);
                    var pointers = new int[11];
                    pointers[0] = header; pointers[1] = start; pointers[2] = end;
                    WorldFile.SaveHeaderPointers(writer, pointers);
                }
                witness.Invoke(null, new object[] { path, false });
            }
            try
            {
                var ore = new Point(109, 109);
                int columns = (Main.maxTilesX + 49) / 50;
                int last = ore.Y / 50 * columns + ore.X / 50;
                string full = Main.maxTilesX * Main.maxTilesY + "/" + Main.maxTilesX * Main.maxTilesY;
                Main.tile[ore.X, ore.Y] = Solid(TileID.Tin);
                WriteNativeTerrain();
                rotate();
                until(() => material("tile", TileID.Tin, false).Detail.Contains(full));
                Require(material("tile", TileID.Tin, false).Completed >= 1, "A contains the original material in its last page");
                string stampA = (string)Get(Get(memory, "live"), "DiskStamp");
                Call(memory, "OnSessionEnded");

                Main.tile[ore.X, ore.Y] = new Tile();
                WriteNativeTerrain();
                rotate();
                until(() =>
                {
                    var active = GetOptional(memory, "live");
                    return active != null && ((HashSet<int>)Get(active, "VerifiedPages")).Count >= 6;
                });
                var partial = Get(memory, "live");
                var verified = (HashSet<int>)Get(partial, "VerifiedPages");
                string stampB = (string)Get(partial, "DiskStamp");
                Require(stampA != stampB && verified.Count == 6 && !verified.Contains(last) && ((IDictionary)Get(partial, "Pages")).Contains(last),
                    "B loaded historical A and independently verified only its first batch");
                rotate();
                until(() =>
                {
                    var active = GetOptional(memory, "live");
                    return active != null && GetOptional(active, "DiskStamp") as string == stampB &&
                        ((HashSet<int>)Get(active, "VerifiedPages")).Count >= 6;
                });
                var restarted = Get(memory, "live");
                Require(((HashSet<int>)Get(restarted, "VerifiedPages")).Count == 6 &&
                    !((HashSet<int>)Get(restarted, "VerifiedPages")).Contains(last) &&
                    ((IDictionary)Get(restarted, "Pages")).Contains(last),
                    "same B reentry trusts the persisted verified subset, not A's unscanned last page");
                var pending = material("tile", TileID.Tin, false);
                Require(pending.Completed == 0 && pending.Detail.Contains("上次见过"), "the unverified A ore is historical only");
                until(() => material("tile", TileID.Tin, false).Detail.Contains(full));
                Require(material("tile", TileID.Tin, false).Completed == 0, "completed B rebuild confirms the removed ore");
                Console.WriteLine("PASS actual atomic index A to changed B, six verified pages persisted, same-stamp B reentry keeps remaining A pages historical.");
            }
            finally { Main.WorldFileMetadata = metadata; }
        }

        private static void GuestLiquid(object memory, Action pump, Action<Func<bool>> until, Func<string, int, bool, CompanionTaskOutcome> material)
        {
            var now = Main.ActiveWorldFileData;
            var tiles = Main.tile;
            int width = Main.maxTilesX, height = Main.maxTilesY;
            int mode = Main.netMode, connected = Netplay.Connection.State;
            bool ending = Netplay.Connection.PendingTermination;
            var oldSocket = Netplay.Connection.Socket;
            var sections = Main.sectionManager;
            var previousDecoder = NetMessage.buffer[256];
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect((IPEndPoint)listener.LocalEndpoint);
                    using (var server = listener.AcceptTcpClient())
                    {
                        Main.maxTilesX = 220; Main.maxTilesY = 100;
                        Main.tile = new Tile[220, 100];
                        for (int y = 0; y < 100; y++)
                            for (int x = 0; x < 220; x++) Main.tile[x, y] = new Tile();
                        Main.tile[90, 60].liquid = Main.tile[210, 60].liquid = 255;
                        Main.tile[90, 60].shimmer(true); Main.tile[210, 60].shimmer(true);
                        Main.netMode = 1;
                        Main.sectionManager = new WorldSections(2, 1);
                        Main.sectionManager.SetSectionLoaded(0, 0);
                        Netplay.Connection.Socket = new TcpSocket(client);
                        Netplay.Connection.State = 10; Netplay.Connection.PendingTermination = false;
                        var decoder = new MessageBuffer { whoAmI = 256 };
                        NetMessage.buffer[256] = decoder;
                        Call(memory, "OnSessionEnded"); Call(memory, "OnSessionStarted"); pump();
                        until(() => material("liquid", 3, false).Detail.Contains("20000/22000"));
                        Require(material("liquid", 3, false).Completed == 1, "only the original synchronized section contributes its shimmer");
                        if (NetManager.Instance.GetModule<NetLiquidModule>() == null) Terraria.Initializers.NetworkInitializer.Load();
                        Main.tile[90, 60].liquid = 0;
                        var packet = NetLiquidModule.Serialize(new HashSet<int> { (90 << 16) | 60 });
                        try
                        {
                            Main.tile[90, 60].liquid = 255;
                            packet.Writer.Flush();
                            packet.Reader.BaseStream.Position = 2;
                            byte[] frame = packet.Reader.ReadBytes(packet.Length - 2);
                            Buffer.BlockCopy(frame, 0, decoder.readBuffer, 0, frame.Length);
                            decoder.ResetReader();
                            decoder.GetData(0, frame.Length, out _);
                        }
                        finally { packet.Recycle(); }
                        Require(Main.tile[90, 60].liquid == 0, "the original primary network module consumed its actual liquid delta");
                        until(() => material("liquid", 3, false).Detail.Contains("20000/22000"));
                        Require(material("liquid", 3, false).Completed == 0 && Main.tile[210, 60].liquid == 255,
                            "an actual network liquid packet invalidates only the synchronized indexed page");
                        Main.tile[199, 48].liquid = Main.tile[200, 48].liquid = 255;
                        Main.tile[199, 48].shimmer(true); Main.tile[200, 48].shimmer(true);
                        Require(Call(memory, "ObservePlace", "shimmer", new Point(199, 48), false) == null &&
                            Call(memory, "ObservePlace", "shimmer", new Point(200, 48), false) == null,
                            "a pond crossing into an unsynchronized section cannot create a trusted ID from either edge");
                        Main.sectionManager.SetSectionLoaded(1, 0);
                        var knownLeft = Call(memory, "ObservePlace", "shimmer", new Point(199, 48), false);
                        var knownRight = Call(memory, "ObservePlace", "shimmer", new Point(200, 48), false);
                        Require(knownLeft != null && knownRight != null &&
                            (string)Get(knownLeft, "Reference") == (string)Get(knownRight, "Reference"),
                            "when both original sections are known, both sources resolve the same verified pond");
                    }
                }
                Console.WriteLine("PASS guest world memory: original section admission, 20,000/22,000 coverage, live NetLiquidModule 82 drain, hidden pond continuation withheld until synced.");
            }
            finally
            {
                listener.Stop();
                Call(memory, "OnSessionEnded");
                Main.ActiveWorldFileData = now; Main.tile = tiles;
                Main.maxTilesX = width; Main.maxTilesY = height; Main.netMode = mode; Main.sectionManager = sections;
                Netplay.Connection.Socket = oldSocket; Netplay.Connection.State = connected; Netplay.Connection.PendingTermination = ending;
                NetMessage.buffer[256] = previousDecoder;
            }
        }
    }
}
