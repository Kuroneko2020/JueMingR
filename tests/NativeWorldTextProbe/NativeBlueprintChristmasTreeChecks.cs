using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintMirrorProfileChecks
    {
        internal static void ChristmasTreeRoundTrip(object context)
        {
            Program.RequireOriginalGame(typeof(Main).Assembly);
            typeof(NativeLegacyPlacementIdentityChecks).GetMethod("PrepareTrackMetadata", F).Invoke(null, null);
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            object host = Get(context, "Blueprint");
            Assembly assembly = host.GetType().Assembly;
            object resolver = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver", true).GetMethod("Create", F).Invoke(null, null);
            var profile = (IBlueprintHorizontalMirrorProfile)resolver;
            var import = (IBlueprintImportProfile)resolver;
            var captureType = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            string directory = Path.Combine(Terraria.Program.SavePath, "tree-library");
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160))
            {
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(captureType, F, null, new object[] { (Func<bool>)(() => true) }, null);
                NativeTree(31, 26, false);
                var bare = NativeTreeCells(31, 26, source);
                NativeTree(42, 26, true);
                var decorated = NativeTreeCells(42, 26, source);
                Require(bare[0].FrameY == 0 && decorated[0].FrameY == (4 | 6 << 3 | 11 << 6 | 11 << 10) &&
                    decorated.Skip(1).Zip(bare.Skip(1), (a, b) => a.FrameX == b.FrameX && a.FrameY == b.FrameY).All(x => x),
                    "original native placement/decoration modifies only the packed anchor");
                for (int i = 0; i < decorated.Length; i++)
                {
                    var shape = import.DescribeImport(decorated[i]);
                    Require(shape?.Object.Reliable == true && shape.Object.X == 42 && shape.Object.Y == 26 &&
                        shape.Object.Width == 4 && shape.Object.Height == 8 && shape.Object.OriginX == 1 && shape.Object.OriginY == 7 &&
                        shape.MatchesMemberFrame(decorated[i], i), "every original member proves the shared tree origin and click");
                }
                var selection = new BlueprintSelection();
                selection.ToggleRectangle(45, 33, 45, 33, 200, 160);
                var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1);
                var handle = new ContentHandle(token.OperationId);
                LibraryEntry saved;
                using (var library = new LibraryState(new FileBlueprintLibrary(directory)))
                {
                    Finish(library, library.TryBeginStage(token));
                    var capture = new CaptureJob(source, selection.Freeze(), 0);
                    for (int tick = 1; tick <= 128 && !capture.Complete && capture.Error == null; tick++)
                    {
                        capture.Advance(16, 20);
                        if (capture.PendingChunk == null) continue;
                        Finish(library, library.TryAppendChunk(handle, capture.PendingChunk));
                        capture.AcceptChunk();
                    }
                    Require(capture.Complete && capture.Error == null && capture.TotalReads <= 66, "one selected member reads only its finite tree footprint and verification");
                    var summary = capture.Summary(1);
                    Require(summary.UnreliableObjectCount == 0 && summary.ContentCount == 32, "native partial selection publishes the whole original tree");
                    Finish(library, library.TrySealDraft(handle, summary));
                    var receipt = Finish(library, library.TryCreate(handle, "原版圣诞树", token));
                    saved = Finish(library, library.TryList(1, 0, receipt.AssetId)).Page.Entries.Single();
                }
                using (var library = new LibraryState(new FileBlueprintLibrary(directory)))
                {
                    var window = Open(library, saved, 1);
                    var obj = window.Objects.Single(); var members = window.Cells.ToArray();
                    Require(members.Length == 32 && members[0].FrameY == decorated[0].FrameY && members.All(c => c.ObjectId == obj.Id),
                        "cold immutable reopen preserves all raw members, origin state and object identity");
                    Require(profile.TryMirrorObject(obj, members, 13, out var reflected, out var mirror, out string reason) &&
                        reflected.X == 9 && mirror[0].FrameY == members[0].FrameY &&
                        mirror.Zip(members, (a, b) => a.FrameX == b.FrameX && a.FrameY == b.FrameY).All(x => x),
                        "valid opposite tree uses the same native state and legal ordinal footprint: " + reason);
                    Require(profile.TryMirrorObject(reflected, mirror, 13, out var restored, out var twice, out reason) &&
                        restored.X == obj.X && twice.SequenceEqual(members), "reflecting twice returns exact tree records");
                    Require(library.TryList(2, 0, saved.AssetId), "original template can still be listed");
                    LibraryReceipt reread = null;
                    NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out reread); });
                    Require(reread.Page.Entries.Single().ContentHash == saved.ContentHash, "view leaves original hash intact");
                    TreeDrawCommands(assembly, window);
                    TreeWorldProof(assembly, obj, members, 61, 49);
                }
                TreeImports(import, bare, decorated);
                UnreliableTreeDraft(decorated);
            }
            Console.WriteLine("PASS Christmas tree native ordinal and ornament oracle, bounded native capture, cold reopen, mirror, Match, Helper inputs and old unreliable publication gate.");
        }

        private static void TreeDrawCommands(Assembly assembly, BlueprintProjectionWindow window)
        {
            var commands = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionRendererCommands", true)
                .GetMethod("Build", F).Invoke(null, new object[] { window.Summary, window.Cells, window.Objects, 0, 0, 4, 8 });
            var slices = ((System.Collections.IEnumerable)commands).Cast<object>().ToArray();
            int[] textureOrder = { 0, 3, 1, 2, 4 }, atlasX = { 0, 198, 330, 660, 660 };
            Require(slices.Length == 5, "one native tree body and four decoration textures replace 32 incorrect tile slices");
            for (int i = 0; i < slices.Length; i++)
            {
                var source = (Rectangle)Get(slices[i], "Source");
                Require(Get(slices[i], "Texture").ToString() == "ChristmasTree" && (int)Get(slices[i], "Type") == textureOrder[i] &&
                    source == new Rectangle(atlasX[i], 0, 64, 128), "original 66-pixel atlas stride and layer order");
            }
        }

        internal static void NativeTree(int x, int y, bool ornamented)
        {
            // The original placement call reads only the fixture's two middle
            // foundation cells. Ornament writes deliberately target non-anchors.
            foreach (int support in new[] { x + 1, x + 2 })
            {
                Main.tile[support, y + 8].type = TileID.Stone;
                Main.tile[support, y + 8].active(true);
            }
            WorldGen.PlaceXmasTree(x + 1, y + 7, 171);
            Require(Main.tile[x, y].active() && Main.tile[x, y].type == 171 && Main.tile[x + 3, y + 7].type == 171,
                "independent original tree placement");
            if (ornamented)
            {
                WorldGen.setXmasTree(x + 3, y + 6, 0, 4);
                WorldGen.setXmasTree(x + 2, y + 4, 1, 6);
                WorldGen.setXmasTree(x + 1, y + 7, 2, 11);
                WorldGen.setXmasTree(x, y + 1, 3, 11);
            }
            WorldGen.CheckXmasTree(x + 2, y + 7);
        }

        private static CaptureCell[] NativeTreeCells(int x, int y, IBlueprintCaptureSource source)
        {
            var members = new CaptureCell[32];
            for (int i = 0; i < members.Length; i++)
                Require(source.Read(x + i % 4, y + i / 4, out members[i]) && members[i].TileType == 171, "read original world tree cell");
            return members;
        }

        private static void TreeWorldProof(Assembly assembly, CaptureObject obj, CaptureCell[] targets, int x, int y)
        {
            var type = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(type, F, null, new object[] { (Func<bool>)(() => true) }, null);
            object[] args = { obj, targets[0], x, y, null };
            Require((bool)type.GetMethod("TryDescribeTarget", F).Invoke(source, args), "saved decorated tree has a canonical world target");
            var body = (BlueprintWorldObject)args[4];
            var world = new BlueprintWorldMatchSample[32];
            var reader = type.GetMethod("TryRead", F, null,
                new[] { typeof(int), typeof(int), typeof(BlueprintWorldMatchSample).MakeByRefType() }, null);
            NativeTree(x, y, false);
            Action read = () =>
            {
                for (int i = 0; i < world.Length; i++)
                {
                    var call = new object[] { x + i % 4, y + i / 4, null };
                    Require((bool)reader.Invoke(source, call), "production world observation");
                    world[i] = (BlueprintWorldMatchSample)call[2];
                }
            };
            read();
            Require(BlueprintMaterialSteps.SameBody(body, targets, world, true) == BlueprintBodyState.Same &&
                BlueprintWorldMatchComparer.CompareObject(body, targets, world, true) == BlueprintWorldMatchState.Conflict,
                "bare tree has the same body but does not complete a decorated target");
            WorldGen.setXmasTree(x, y, 0, 4); WorldGen.setXmasTree(x, y, 1, 6);
            WorldGen.setXmasTree(x, y, 2, 11); WorldGen.setXmasTree(x, y, 3, 11);
            read();
            Require(BlueprintWorldMatchComparer.CompareObject(body, targets, world, true) == BlueprintWorldMatchState.Match,
                "independent native decoration matches the complete saved target");
            Main.tile[x + 3, y + 7].active(false);
            read();
            Require(BlueprintWorldMatchComparer.CompareObject(body, targets, world, true) != BlueprintWorldMatchState.Match,
                "a missing observed member cannot complete a tree");
        }

        private static void TreeImports(IBlueprintImportProfile profile, CaptureCell[] bare, CaptureCell[] decorated)
        {
            string root = Path.Combine(Terraria.Program.SavePath, "tree-exchange");
            Directory.CreateDirectory(root);
            using (var library = NativeBlueprintHelperImportChecks.State(Path.Combine(root, "templates"), Path.Combine(root, "counter.json")))
            {
                Require(ImportTree(library, profile, root, "裸树", bare) == BlueprintExchangeStatus.Completed, "Helper imports native bare tree");
                Require(ImportTree(library, profile, root, "装饰树", decorated) == BlueprintExchangeStatus.Completed, "Helper imports native decorated tree");
                Require(ImportTree(library, profile, root, "缺格", decorated.Take(31).ToArray()) == BlueprintExchangeStatus.Failed, "Helper rejects incomplete native geometry");
                var mixed = (CaptureCell[])decorated.Clone();
                mixed[31].FrameX = bare[30].FrameX;
                Require(ImportTree(library, profile, root, "邻树混格", mixed) == BlueprintExchangeStatus.Failed, "Helper rejects a raw member that points at another origin");
                mixed[31] = decorated[31]; mixed[0].FrameY = 7;
                Require(ImportTree(library, profile, root, "装饰伪造", mixed) == BlueprintExchangeStatus.Failed, "Helper rejects unreachable packed ornament");
                mixed[0] = decorated[0]; mixed[31].Y++;
                Require(ImportTree(library, profile, root, "越界", mixed) == BlueprintExchangeStatus.Failed, "Helper rejects object member outside template");
            }
        }

        private static BlueprintExchangeStatus ImportTree(LibraryState library, IBlueprintImportProfile profile, string root, string name, CaptureCell[] input)
        {
            string source = Path.Combine(root, name + ".json");
            int left = input[0].X, top = input[0].Y;
            string members = String.Join(",", input.Select(c => String.Format(CultureInfo.InvariantCulture,
                "{{\"x\":{0},\"y\":{1},\"tileType\":171,\"frameX\":{2},\"frameY\":{3}}}", c.X - left, c.Y - top, c.FrameX, c.FrameY)));
            File.WriteAllText(source, "{\"name\":\"" + name + "\",\"width\":4,\"height\":8,\"tiles\":[" + members + "]}");
            return NativeBlueprintHelperImportChecks.Import(library, profile, source).Status;
        }

        private static void UnreliableTreeDraft(CaptureCell[] source)
        {
            string root = Path.Combine(Terraria.Program.SavePath, "tree-old-draft");
            using (var library = new LibraryState(new FileBlueprintLibrary(root)))
            {
                var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
                var summary = new CaptureSummary { Left = source[0].X, Top = source[0].Y, Width = 5, Height = 8,
                    ContentCount = 33, UnreliableObjectCount = 32, TargetVersion = "1.4.6.1" };
                var old = (CaptureCell[])source.Clone();
                Finish(library, library.TryBeginStage(token));
                for (int at = 0; at < 32; at += 16)
                {
                    var objects = new CaptureObject[16];
                    for (int i = 0; i < objects.Length; i++)
                    {
                        int n = at + i;
                        old[n].ObjectId = n + 1; old[n].UnreliableObject = true;
                        objects[i] = new CaptureObject { Id = n + 1, Type = 171, X = old[n].X, Y = old[n].Y, Width = 1, Height = 1, Reason = "旧版未知对象" };
                    }
                    Finish(library, library.TryAppendChunk(handle, new CaptureChunk(old.Skip(at).Take(16).ToArray(), 16, objects, 16)));
                }
                var stone = new CaptureCell { X = summary.Left + 4, Y = summary.Top + 7, Layers = CaptureLayer.Tile, TileType = 1 };
                Finish(library, library.TryAppendChunk(handle, new CaptureChunk(new[] { stone }, 1)));
                Finish(library, library.TrySealDraft(handle, summary));
                var partial = NativeBlueprintHelperImportChecks.Command(library, library.TryCreate(handle, "旧成员", token, excludedObjectIds: new[] { 1 }));
                Require(partial.Status == LibraryStatus.Failed && partial.Error.Contains("排除所有未确认对象"),
                    "a complete raw draft across chunks cannot publish with only one historical member excluded");
                var saved = Finish(library, library.TryCreate(handle, "排除未确认成员", token, excludeUnreliable: true));
                var entry = Finish(library, library.TryList(1, 0, saved.AssetId)).Page.Entries.Single();
                var opened = Open(library, entry, 1);
                Require(opened.Cells.Count == 1 && opened.Cells[0].TileType == 1 && opened.Objects.Count == 0,
                    "publication actually excludes disproved members without resurrecting their raw tree or losing another tile");
            }
        }
    }

    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionChristmasTree(object context, object host, object tools, object input)
        {
            var player = Reset(context, host, tools, input);
            var worldHooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldChangeHooks", true);
            var hooks = (System.Collections.Generic.List<string>)worldHooks.GetField("DebugHookTargets", F).GetValue(null);
            Require(hooks.Contains("Terraria.WorldGen.setXmasTree") && hooks.Contains("Terraria.WorldGen.PlaceXmasTree_Direct"),
                "the direct native body and ornament writers are instrumented by the ordinary world observer");
            player.position = new Vector2(46 * 16, 646); player.velocity = Vector2.Zero;
            NativeBlueprintMirrorProfileChecks.NativeTree(90, 26, true);
            const int x = 44, y = 34;
            for (int col = 0; col < 4; col++)
            {
                Main.tile[x + col, y + 8].type = TileID.Stone;
                Main.tile[x + col, y + 8].active(true);
            }
            int[] items = { 1873, 1877, 1883, 1894, 1905 };
            for (int i = 0; i < items.Length; i++) { player.inventory[12 + i].SetDefaults(items[i]); player.inventory[12 + i].stack = 2; }
            var members = Enumerable.Range(0, 32).Select(i =>
            {
                int col = i % 4, row = i / 4;
                Tile tile = Main.tile[90 + col, 26 + row];
                return new CaptureCell { X = col, Y = row, ObjectId = 1, Layers = CaptureLayer.Tile, TileType = 171,
                    FrameX = tile.frameX, FrameY = tile.frameY };
            }).ToArray();
            var metadata = new CaptureObject { Id = 1, Type = 171, Width = 4, Height = 8, OriginX = 1, OriginY = 7, Reliable = true };
            var summary = new CaptureSummary { Width = 4, Height = 8, ContentCount = 32, TargetVersion = "1.4.6.1" };
            var window = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "native-tree-fixture", 1, 1),
                1, summary, new BlueprintProjectionBounds(0, 0, 4, 8), members, new[] { metadata });
            var entry = NativeBlueprintProjectionVisualChecks.Save((LibraryState)Get(host, "Library"), window);
            Place(context, host, input, entry, x, y);
            Call(host, "SetMaterialViewDemand", true);
            try
            {
                Until(context, input, () => GetOptional(host, "MaterialCatalog") != null);
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                var bill = new BlueprintMaterialAccumulator();
                BlueprintMaterialSteps.StaticObject(metadata, members[0], catalog, bill);
                Require(bill.Status == BlueprintMaterialStatus.Exact && bill.Counts.Count == items.Length &&
                    items.All(id => bill.Counts.TryGetValue(id, out long count) && count == 1), "one tree and one of each desired native ornament, independent of 32 members");
                SetAutomatic(host, input, true);
                try
                {
                    Until(context, input, () => Main.tile[x, y].type == 171 && WorldGen.checkXmasTreeDrop(x, y, 3) == 11 &&
                        player.inventory.Skip(12).Take(items.Length).All(item => item.stack == 1));
                }
                catch
                {
                    Console.Error.WriteLine("Tree native receipt: members=" + members.Count(c => Main.tile[x + c.X, y + c.Y].type == 171) +
                        ", ornaments=" + Main.tile[x, y].frameY + ", stacks=" + String.Join(",", player.inventory.Skip(12).Take(items.Length).Select(item => item.stack)));
                    var scene = Scene(host).SingleOrDefault();
                    if (scene != null)
                    {
                        foreach (var range in ((System.Collections.IEnumerable)Get(Get(scene, "Match"), "Ranges")).Cast<object>())
                        {
                            var material = GetOptional(range, "Materials");
                            var bodies = material == null ? null : GetOptional(material, "BuildObjectBodies") as System.Collections.IDictionary;
                            if (bodies != null) foreach (System.Collections.DictionaryEntry body in bodies)
                                Console.Error.WriteLine("Tree body proof: " + body.Key + "=" + body.Value + ", rangeVersion=" + Get(range, "Version") +
                                    ", bodyVersion=" + Get(material, "BuildBodyVersion") + ", rangeIndex=" + Get(range, "Index"));
                        }
                        var discovered = (System.Collections.IDictionary)Get(host, "nearBuildRanges");
                        foreach (System.Collections.DictionaryEntry entryFact in discovered)
                        {
                            var facts = (System.Collections.IDictionary)Get(entryFact.Value, "Facts");
                            if (!facts.Contains(2)) continue;
                            var shape = facts[2];
                            Console.Error.WriteLine("Tree near shape: ready=" + Get(shape, "Ready") + ",pending=" + Get(shape, "Pending") +
                                ",wait=" + Get(shape, "Wait") + ",type=" + Get(shape, "Type"));
                        }
                    }
                    throw;
                }
                Require(members.All(c => Main.tile[x + c.X, y + c.Y].active() && Main.tile[x + c.X, y + c.Y].type == 171 &&
                    Main.tile[x + c.X, y + c.Y].frameX == c.FrameX && Main.tile[x + c.X, y + c.Y].frameY == c.FrameY),
                    "production held-tool operations recreate exact original frame state");
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            }
            finally { SetAutomatic(host, input, false); Call(host, "SetMaterialViewDemand", false); }
            ChristmasTreeRestock(context, host, tools, input, entry, 1, 62);
            ChristmasTreeRestock(context, host, tools, input, entry, 3, 86);
            ChristmasTreeShapeContinuation(context, host, tools, input, entry);
        }

        private static Player PrepareTreeConstruction(object context, object host, object tools, object input, int x, int y)
        {
            var player = Reset(context, host, tools, input);
            for (int column = x - 3; column <= x + 12; column++)
            {
                Main.tile[column, y + 8].type = TileID.Stone;
                Main.tile[column, y + 8].active(true);
            }
            player.position = new Vector2(x * 16, (y + 8) * 16 - player.height);
            player.velocity = Vector2.Zero;
            return player;
        }

        private static void ChristmasTreeRestock(object context, object host, object tools, object input, LibraryEntry entry, int first, int y)
        {
            const int x = 44;
            int[] types = { 1877, 1883, 1894, 1905 };
            int[] variants = { 4, 6, 11, 11 };
            int[] shifts = { 0, 3, 6, 10 };
            var player = PrepareTreeConstruction(context, host, tools, input, x, y);
            NativeBlueprintMirrorProfileChecks.NativeTree(x, y, false);
            Place(context, host, input, entry, x, y);
            SetAutomatic(host, input, true);
            try
            {
                WaitForQuietTree(context, host, tools, input);
                var position = player.position;
                long selection = (long)Get(tools, "SelectionIntent");
                long uses = (long)Get(host, "BuildUsesStarted");
                long classifications = (long)Get(host, "NearBuildClassifications");
                player.inventory[30].SetDefaults(ItemID.DirtBlock);
                for (int i = 0; i < 24; i++) Step(context, input);
                Require((long)Get(host, "NearBuildClassifications") == classifications && Main.tile[x, y].frameY == 0,
                    "stable bare tree ignores unrelated inventory publication without reclassification");

                int packed = 0;
                foreach (int slot in new[] { first }.Concat(Enumerable.Range(0, 4).Where(slot => slot != first)))
                {
                    player.inventory[12 + slot].SetDefaults(types[slot]);
                    Console.WriteLine("Ornament recovery: refill slot=" + (slot + 1) + ", preceding classifications=" + Get(host, "NearBuildClassifications"));
                    packed |= variants[slot] << shifts[slot];
                    for (int i = 0; i < 64 && !(Main.tile[x, y].frameY == packed && player.inventory[12 + slot].IsAir); i++)
                        Step(context, input);
                    Require(Main.tile[x, y].frameY == packed && player.inventory[12 + slot].IsAir,
                        "restock of ornament slot " + (slot + 1) + " must update the native packed frame and debit its one item: actual frame=" +
                        Main.tile[x, y].frameY + ", stack=" + player.inventory[12 + slot].stack + ", classifications=" + Get(host, "NearBuildClassifications"));
                    if (packed == (4 | 6 << 3 | 11 << 6 | 11 << 10)) break;
                    WaitForQuietTree(context, host, tools, input);
                    // Refill an already fulfilled slot while the partial tree
                    // still waits. It must neither wake it nor consume again.
                    classifications = (long)Get(host, "NearBuildClassifications");
                    player.inventory[12 + slot].SetDefaults(types[slot]);
                    for (int i = 0; i < 24; i++) Step(context, input);
                    Require((long)Get(host, "NearBuildClassifications") == classifications && player.inventory[12 + slot].stack == 1,
                        "completed ornament retires its wait from the partial tree");
                }
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                long consumedUses = (long)Get(host, "BuildUsesStarted") - uses;
                Require(consumedUses == 4 && player.position == position && (long)Get(tools, "SelectionIntent") == selection &&
                    (bool)Get(host, "AutoBuildEnabled"), "restock alone advances all four native uses without moving or a manual selection gesture");
                Console.WriteLine("PASS ornament restock, first slot=" + (first + 1) + ", packed frame=" + packed + ", uses=" + consumedUses);
            }
            finally { SetAutomatic(host, input, false); }
        }

        private static void WaitForQuietTree(object context, object host, object tools, object input)
        {
            long classifications = -1;
            int quietTicks = 0;
            Until(context, input, () =>
            {
                long now = (long)Get(host, "NearBuildClassifications");
                quietTicks = now > 0 && now == classifications && !(bool)Get(Get(tools, "Use"), "Active") &&
                    !(bool)Get(host, "nearVisitNeeded") ? quietTicks + 1 : 0;
                classifications = now;
                return quietTicks == 24;
            });
        }

        private static void ChristmasTreeShapeContinuation(object context, object host, object tools, object input, LibraryEntry entry)
        {
            const int x = 44, y = 110, second = x + 5;
            var player = PrepareTreeConstruction(context, host, tools, input, x + 4, y);
            NativeBlueprintMirrorProfileChecks.NativeTree(x, y, false);
            NativeBlueprintMirrorProfileChecks.NativeTree(second, y, false);
            Place(context, host, input, entry, x, y);
            Place(context, host, input, entry, second, y);
            player.inventory[12].SetDefaults(1905);
            player.inventory[12].stack = 2;
            long initialUses = (long)Get(host, "BuildUsesStarted");
            SetAutomatic(host, input, true);
            try
            {
                Until(context, input, () => player.inventory[12].stack == 1 &&
                    (WorldGen.checkXmasTreeDrop(x, y, 3) == 11 || WorldGen.checkXmasTreeDrop(second, y, 3) == 11));
                var group = Get(host, "buildContinuation");
                // A lower slot on both trees is now runnable. The current
                // ornament still has one real use on the second Shape fact.
                player.inventory[13].SetDefaults(1877);
                Until(context, input, () => player.inventory[12].IsAir && WorldGen.checkXmasTreeDrop(x, y, 3) == 11 &&
                    WorldGen.checkXmasTreeDrop(second, y, 3) == 11);
                bool sameGroup = ReferenceEquals(Get(host, "buildContinuation"), group);
                long starts = (long)Get(host, "BuildUsesStarted") - initialUses;
                // The two native debits above belong to one continuous use.
                // Count independent starts separately from world receipts.
                Require(sameGroup && player.inventory[13].stack == 1 && starts == 1,
                    "same ornament continues across two tree Shape facts before a different runnable ornament: continued=" + sameGroup +
                    ", other stack=" + player.inventory[13].stack + ", starts=" + starts);
                Until(context, input, () => player.inventory[13].IsAir);
                WaitForQuietTree(context, host, tools, input);
                player.inventory[14].SetDefaults(1883);
                Until(context, input, () => player.inventory[14].IsAir &&
                    (WorldGen.checkXmasTreeDrop(x, y, 1) == 6 || WorldGen.checkXmasTreeDrop(second, y, 1) == 6));
                Require((long)Get(host, "BuildUsesStarted") == initialUses + 3, "depleted partial trees keep their remaining real restock eligibility");
                Console.WriteLine("PASS ornament Shape continuation: two native debits in one use, stable shortage and another exact refill.");
            }
            finally { SetAutomatic(host, input, false); }
        }
    }
}
