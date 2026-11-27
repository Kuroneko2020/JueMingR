using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintHelperImportChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            // The real Main startup initializes Minecart before a ready
            // immutable blueprint profile can capture its physical frame map.
            // This CPU receiver must provide that same native metadata first.
            typeof(NativeLegacyPlacementIdentityChecks).GetMethod("PrepareTrackMetadata",F).Invoke(null,null);
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            Framing.Initialize(); Main.SetupTileMerge();
            var host = NativeInformationChecks.Get(context, "Blueprint");
            var profile = (IBlueprintImportProfile)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver").GetMethod("Create", F).Invoke(null, null);
            Need(profile.Ready, "actual original metadata profile is Ready");
            string repair = Path.Combine(Program.OutputDirectory, "helper-repair-" + Guid.NewGuid().ToString("N"));
            FieldAndNameContracts(profile, Path.Combine(repair, "fields"));
            ContainerNames(profile, host.GetType().Assembly, Path.Combine(repair, "containers"));
            string inputPath = Environment.GetEnvironmentVariable("JUEMINGR_BLUEPRINT_HELPER_INPUT");
            if (!String.IsNullOrEmpty(inputPath)) OriginalRepairInput(profile, host.GetType().Assembly, Path.GetFullPath(inputPath), Path.Combine(repair, "original"));
            RunWithProfile(profile, LargeSamplePath(), Path.Combine(Program.OutputDirectory, "helper-import-" + Guid.NewGuid().ToString("N")));
        }
        internal static string LargeSamplePath()
        { return Path.Combine(Program.Repository, "references/归档文件/JMR_Blueprint_Hoik_Zoom_20261110_v1/attachments/岛牌刷怪场4.0.json"); }
        private const string TileRecord = "{\"x\":0,\"y\":0,\"tileType\":1,\"frameX\":0,\"frameY\":0";
        private static string WriteInput(string area, string name, string json)
        { string path = Path.Combine(area, name + ".json"); File.WriteAllText(path, json, new UTF8Encoding(false)); return path; }
        private static string NamedTile(string name, int width)
        { return "{\"name\":" + name + ",\"width\":" + width + ",\"height\":2,\"tiles\":[" + TileRecord + "}]}"; }
        private static void FieldAndNameContracts(IBlueprintImportProfile profile, string area)
        {
            Directory.CreateDirectory(area);
            string templates = Path.Combine(area, "templates"), counter = Path.Combine(area, "counter.json");
            using (var state = State(templates, counter))
            {
                string core = "{\"name\":\"schema\",\"width\":4,\"height\":2,\"tiles\":[";
                string defaults = ",\"color\":0,\"slope\":0,\"isHalfBrick\":false,\"inActive\":false,\"fullbright\":false,\"invisible\":false";
                string explicitFields = core + TileRecord + defaults + "}],\"walls\":[],\"wires\":[{\"x\":1,\"y\":0,\"red\":false,\"blue\":false,\"green\":false,\"yellow\":false,\"actuator\":false}],\"containers\":[],\"entities\":[],\"createdAt\":\"2026-11-27T12:00:00+08:00\"}";
                var published = Import(state, profile, WriteInput(area, "explicit", explicitFields));
                Need(published.Status == BlueprintExchangeStatus.Completed, published.Message);
                var entry = List(state).Entries.Single();
                foreach (var json in new[] { core + TileRecord + "}]}", core + "null," + TileRecord + "},null],\"walls\":null,\"wires\":[null,{\"x\":1,\"y\":0}],\"containers\":null,\"entities\":[null]}" })
                {
                    var same = Import(state, profile, WriteInput(area, "equivalent", json));
                    Need(same.Status == BlueprintExchangeStatus.AlreadyExists && same.AssetId == entry.AssetId, "normalized fields deduplicate to the identical healthy standard body");
                }
                string wallWire = "{\"name\":\"layer defaults\",\"width\":4,\"height\":2,\"tiles\":null,\"walls\":[{\"x\":0,\"y\":0,\"wallType\":1";
                var layers = Import(state, profile, WriteInput(area, "wall-wire", wallWire + "}],\"wires\":[{\"x\":0,\"y\":0,\"red\":true}]}"));
                Need(layers.Status == BlueprintExchangeStatus.Completed, layers.Message);
                var explicitLayers = Import(state, profile, WriteInput(area, "wall-wire-explicit", wallWire + ",\"color\":0,\"fullbright\":false,\"invisible\":false}],\"wires\":[{\"x\":0,\"y\":0,\"red\":true,\"blue\":false,\"green\":false,\"yellow\":false,\"actuator\":false}]}"));
                Need(explicitLayers.Status == BlueprintExchangeStatus.AlreadyExists && explicitLayers.AssetId == layers.AssetId, "wall and wire defaults preserve identity too");
                var invalids = new[] {
                    "{\"width\":1,\"height\":1,\"tiles\":[null],\"wires\":[{\"x\":0,\"y\":0}]}", explicitFields.Replace("\"width\":4", "\"width\":0"),
                    explicitFields.Replace("\"frameX\":0,", ""), explicitFields.Replace("\"tileType\":1", "\"tileType\":65535"),
                    explicitFields.Replace("\"walls\":[]", "\"walls\":{}"), explicitFields.Replace("\"walls\":[]", "\"walls\":[1]"),
                    explicitFields.Replace("\"x\":0", "\"x\":0,\"x\":0"), explicitFields.Replace("\"x\":0", "\"x\":4"),
                    explicitFields.Replace("\"name\":\"schema\",", "\"name\":\"schema\",\"unknown\":0,"), explicitFields.Replace("\"color\":0", "\"unknown\":0"),
                    explicitFields.Replace("\"color\":0", "\"color\":null"), explicitFields.Replace("\"inActive\":false", "\"inActive\":\"false\""),
                    explicitFields.Replace("\"slope\":0,\"isHalfBrick\":false", "\"slope\":3,\"isHalfBrick\":true"),
                    explicitFields.Replace("2026-11-27T12:00:00+08:00", "not a date"), explicitFields.Replace("\"wires\":[{\"x\":1", "\"wires\":[{\"x\":4"),
                    explicitFields.Replace("\"containers\":[]", "\"containers\":[{\"x\":4,\"y\":0,\"name\":null}]"),
                    explicitFields.Replace("\"containers\":[]", "\"containers\":[{\"x\":0,\"y\":0},{\"x\":0,\"y\":0,\"name\":\"重复\"}]"),
                };
                foreach (string json in invalids)
                {
                    var bad = Import(state, profile, WriteInput(area, "invalid", json));
                    Need(bad.Status == BlueprintExchangeStatus.Failed && !state.ExchangeBusy && !Directory.EnumerateDirectories(Path.Combine(templates, ".staging")).Any(), "invalid input retires ticket/stage: " + json);
                }
                Need(List(state).TotalCount == 2, "bad fields never publish a visible row");
                Need(Import(state, profile, WriteInput(area, "fixed-after-error", explicitFields)).Status == BlueprintExchangeStatus.AlreadyExists, "following valid request still runs");
                string shortTitle = new string('中', 48), combined = new string('中', 47) + "e\u0301";
                Need(Import(state, profile, WriteInput(area, "48", NamedTile("\"" + shortTitle + "\"", 48))).Status == BlueprintExchangeStatus.Completed, "48 Chinese title stays direct");
                Need(Import(state, profile, WriteInput(area, "combined", NamedTile("\"" + combined + "\"", 50))).Status == BlueprintExchangeStatus.Completed, "count composed text elements instead of UTF-16 title length");
                foreach (int length in new[] { 49, 64 })
                {
                    string sourceName = new string('长', length), json = NamedTile("\"" + sourceName + "\"", length);
                    string path = WriteInput(area, "rename-" + length, json);
                    var need = Import(state, profile, path);
                    Need(need.Status == BlueprintExchangeStatus.NeedsName && need.SourceName == sourceName && need.AssetId == Guid.Empty && state.ExchangePhase == BlueprintExchangePhase.AwaitingName,
                        "source title reaches same reserved private stage with exact failure reason");
                    string stage = Directory.EnumerateDirectories(Path.Combine(templates, ".staging")).Single();
                    byte[] sealedBody = File.ReadAllBytes(Path.Combine(stage, "root.bin"));
                    Need(List(state).TotalCount == (length == 49 ? 4 : 5), "waiting exchange leaves read worker lane free and publishes nothing");
                    Need(!state.ResumeImportName(Guid.NewGuid(), "另一请求") && !state.ResumeImportName(need.RequestId, sourceName), "only this state may accept a valid local title");
                    if (length == 64)
                    {
                        state.CancelExchange(need.RequestId);
                        Need(Await(state).Status == BlueprintExchangeStatus.Cancelled && !state.ExchangeBusy && !Directory.Exists(stage), "cancel wakes idle sealed-stage cleanup");
                        need = Import(state, profile, path);
                        Need(need.Status == BlueprintExchangeStatus.NeedsName, "fresh import can ask again after cancellation");
                    }
                    // Replacing this test-only path while the same import waits
                    // proves the external handle is closed and no reread occurs.
                    File.Delete(path); File.WriteAllText(path, NamedTile("\"替换文件\"", 99));
                    Need(state.ResumeImportName(need.RequestId, "本地名称" + length), "resume exact pending name once");
                    Need(!state.ResumeImportName(need.RequestId, "第二次确认"), "replayed name cannot resubmit");
                    var done = Await(state);
                    var saved = List(state).Entries.Single(e => e.AssetId == done.AssetId);
                    Need(done.Status == BlueprintExchangeStatus.Completed && saved.Width == length && saved.Name == "本地名称" + length &&
                        File.ReadAllBytes(Path.Combine(templates, done.AssetId.ToString("N"), "content", "root.bin")).SequenceEqual(sealedBody), "confirmation publishes exact previously sealed root/body with only local metadata title changed");
                    var duplicate = Import(state, profile, WriteInput(area, "again-" + length, json));
                    Need(duplicate.Status == BlueprintExchangeStatus.AlreadyExists && duplicate.AssetId == done.AssetId, "healthy duplicate bypasses naming and retains saved title");
                    string export = Path.Combine(area, "rename-" + length + ".jmbp");
                    Export(state, saved, export);
                    Need(Import(state, profile, export).AssetId == saved.AssetId, "explicit local title survives .jmbp roundtrip");
                }
                foreach (string name in new[] { "null", "\"\"" })
                {
                    var need = Import(state, profile, WriteInput(area, "no-title", NamedTile(name, 73)));
                    Need(need.Status == BlueprintExchangeStatus.NeedsName, "missing or empty source name must be explicitly replaced");
                    state.CancelExchange(need.RequestId); Need(Await(state).Status == BlueprintExchangeStatus.Cancelled, "no-name draft cancels cleanly");
                }
                var abandoned = Import(state, profile, WriteInput(area, "omitted-title", NamedTile("null", 78).Replace("\"name\":null,", "")));
                Need(abandoned.Status == BlueprintExchangeStatus.NeedsName, "missing source title reaches awaiting name");
            }
            Need(!Directory.EnumerateDirectories(Path.Combine(templates, ".staging")).Any(), "whole worker stop releases its idle sealed stage");
            Console.WriteLine("HelperImport schema/name PASS: semantic dedup, strict negatives, private sealed body, 48/49/64 and composed title, closed ticket, exact resume, cancel/stop cleanup");
        }
        private static BlueprintProjectionWindow OpenWindow(LibraryState state, LibraryEntry entry)
        {
            var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 812, 1);
            Need(state.RequestProjectionOpen(identity) && Projection(state).Kind == BlueprintProjectionReceiptKind.Opened, "cold worker opens exact published root/index");
            Need(state.RequestProjectionWindow(identity, 6, new BlueprintProjectionBounds(0, 0, entry.Width, entry.Height)), "projection window admits full declared bounds");
            var receipt = Projection(state);
            Need(receipt.Window != null, "normal projection returns sparse source body");
            return receipt.Window;
        }
        private static void OriginalRepairInput(IBlueprintImportProfile profile, Assembly host, string inputPath, string area)
        {
            const string expected = "13064364E6CD6A6E107DF7747E4AEE2656CFF265A14A032ED8878825253D005C";
            using (var stream = File.OpenRead(inputPath)) using (var sha = SHA256.Create())
                Need(stream.Length == 3397 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected, "opt-in InputPath is the untouched owner original");
            Directory.CreateDirectory(area);
            string templates = Path.Combine(area, "templates"), counter = Path.Combine(area, "counter.json"), exported = Path.Combine(area, "owner-original.jmbp");
            string version = typeof(Main).Assembly.GetName().Version.ToString();
            LibraryEntry entry;
            using (var state = State(templates, counter))
            {
                var receipt = ImportVersion(state, profile, inputPath, version);
                Need(receipt.Status == BlueprintExchangeStatus.Completed, "normal Ready-profile publication of exact owner InputPath: " + receipt.Message);
                entry = List(state).Entries.Single();
                Need(entry.Name == "肉前血月钓鱼" && entry.Width == 8 && entry.Height == 5 && entry.ContentCount == 21, "ordinary visible list metadata keeps declared dimensions and count");
                Export(state, entry, exported, version);
            }
            using (var reopened = State(templates, counter))
                CheckOriginalShape(OpenWindow(reopened, List(reopened).Entries.Single()), false);
            string clone = Path.Combine(area, "roundtrip");
            LibraryEntry roundtrip;
            using (var state = State(clone, counter))
            {
                var receipt = ImportVersion(state, profile, exported, version);
                Need(receipt.Status == BlueprintExchangeStatus.Completed, "export publishes as standard original asset into fresh library");
                roundtrip = List(state).Entries.Single();
                Need(roundtrip.ContentHash == entry.ContentHash && roundtrip.ContentRevision == entry.ContentRevision && roundtrip.Name == entry.Name,
                    "jmbp roundtrip retains exact root and local title");
            }
            string scenePath = Path.Combine(area, "scene.json"), scope = new string('6', 64);
            var scene = new BlueprintSceneHistory(_ => new AtomicFileDocument(scenePath, 65536, true));
            scene.BeginSession(1, Main.maxTilesX, Main.maxTilesY); scene.UseScope(scope);
            NativeQuickItemChecks.Until(() => { scene.Poll(); return scene.Loaded; });
            var key = new BlueprintTemplateKey(roundtrip.AssetId, Guid.Parse(roundtrip.ContentRevision), roundtrip.ContentHash);
            Need(scene.Offer(new[] { new BlueprintSceneRecord(key, 70, 68, 7, BlueprintProjectionMask.Empty, true) }), "committed mirrored scene snapshot is accepted");
            NativeQuickItemChecks.Until(() => { scene.Poll(); return scene.Saved; });
            Need(scene.Stop(3000), "mirrored snapshot reliably saved and scene owner stopped");
            scene = new BlueprintSceneHistory(_ => new AtomicFileDocument(scenePath, 65536, true));
            scene.BeginSession(2, Main.maxTilesX, Main.maxTilesY); scene.UseScope(scope);
            NativeQuickItemChecks.Until(() => { scene.Poll(); return scene.Loaded; });
            var record = scene.Restored?.Records.Single();
            Need(!scene.Protected && record != null && record.MirroredHorizontally && record.OriginX == 70 && record.OriginY == 68 && record.Order == 7 &&
                record.Template.AssetId == key.AssetId && record.Template.ContentHash == key.ContentHash, "cold scene reentry retains committed pose, origin, order and asset identity");
            try
            {
                using (var reopened = State(clone, counter))
                {
                    var raw = OpenWindow(reopened, List(reopened).Entries.Single());
                    CheckOriginalShape(raw, false);
                    var mirror = (IBlueprintHorizontalMirrorProfile)profile;
                    var view = new BlueprintProjectionView(raw.Summary, record.MirroredHorizontally, record.Mask, mirror);
                    Need(view.TryApply(raw, true, out BlueprintProjectionWindow mirrored, out string why), "ordinary mirror of reopened source: " + why);
                    CheckOriginalShape(mirrored, true);
                    // Re-ingest the mirrored body as source values so this really
                    // reflects twice, rather than recomputing one view of raw.
                    var mirrorBody = new BlueprintProjectionWindow(raw.Identity, 7, raw.Summary, raw.Bounds, mirrored.Cells.ToArray(), mirrored.Objects.ToArray());
                    Need(view.TryApply(mirrorBody, true, out BlueprintProjectionWindow twice, out why), "second horizontal reflection: " + why);
                    CheckOriginalShape(twice, false);
                    Need(raw.Cells.Select(c => new { c.X, c.Y, c.TileType, c.Slope, c.HalfBrick }).SequenceEqual(twice.Cells.Select(c => new { c.X, c.Y, c.TileType, c.Slope, c.HalfBrick })), "double mirror restores all captured geometry");
                    MatchOriginalHammer(host, mirrored.Cells.Single(c => c.X == 3 && c.Y == 2), record.OriginX + 3, record.OriginY + 2);
                }
            }
            finally { scene.Stop(3000); }
            using (var stream = File.OpenRead(inputPath)) using (var sha = SHA256.Create())
                Need(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected, "desktop original remains byte-for-byte intact");
            Console.WriteLine("HelperImport owner vector PASS: exact untouched InputPath SHA256=" + expected + "; 8x5/21; cold library and jmbp; mirror twice; original placement/hammer and world comparer; scene cold persistence. Interactive Steam NOT_EXECUTED.");
        }
        private static void CheckOriginalShape(BlueprintProjectionWindow window, bool mirrored)
        {
            Need(window.Summary.Width == 8 && window.Summary.Height == 5 && window.Cells.Count == 21 && window.Cells.All(c => c.Layers == CaptureLayer.Tile), "boundaries and 21 foreground cells retained");
            Need(window.Cells.Count(c => c.TileType == 273) == 17 && window.Cells.Count(c => c.TileType == 19) == 4 && window.Cells.Count(c => c.HalfBrick) == 6, "17 stone slab, four platforms, six half bricks");
            var slope = window.Cells.Single(c => c.Slope != 0);
            Need(slope.X == (mirrored ? 3 : 4) && slope.Y == 2 && slope.TileType == 273 && slope.Slope == (mirrored ? 4 : 3) && !slope.HalfBrick, "horizontal reflection swaps the two original upper-slope directions");
            // Terraria's platform framing pairs 18/36 and leaves 0/90
            // symmetric. Compare independent coordinates for the four slots.
            int[] y = { 4, 0, 4, 4 };
            int[] sourceX = { 1, 2, 2, 3 }, sourceFrameX = { 36, 90, 0, 18 }, reflectedFrameX = { 18, 90, 0, 36 };
            for (int i = 0; i < y.Length; i++)
            {
                var cell = window.Cells.Single(c => c.X == (mirrored ? 7 - sourceX[i] : sourceX[i]) && c.Y == y[i]);
                Need(cell.TileType == 19 && cell.HalfBrick && cell.FrameY == 774 && cell.FrameX == (mirrored ? reflectedFrameX[i] : sourceFrameX[i]), "original platform half-brick style/slot " + i);
            }
        }
        private static void MatchOriginalHammer(Assembly host, CaptureCell target, int x, int y)
        {
            var reader = CaptureReader(host); var previous = Main.tile[x, y]; Main.tile[x, y] = new Tile();
            try
            {
                Need(WorldGen.PlaceTile(x, y, target.TileType, mute: true, forced: true), "original tile placement installs the target block");
                Need(reader.Read(x, y, out CaptureCell unhammered) && BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, unhammered)).ConflictBits == 1,
                    "pre-hammer ordinary flat block conflicts with the mirrored slope");
                Need(WorldGen.SlopeTile(x, y, target.Slope, true, true) && reader.Read(x, y, out CaptureCell hammered) &&
                    BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, hammered)).MatchBits == 1,
                    "actual original hammer outcome matches required mirrored appearance even after reframe");
            }
            finally { Main.tile[x, y] = previous; }
        }
        private static IBlueprintCaptureSource CaptureReader(Assembly host)
        { return (IBlueprintCaptureSource)Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource"), F, null, new object[] { (Func<bool>)(() => true) }, null); }
        private static void ContainerNames(IBlueprintImportProfile profile, Assembly host, string area)
        {
            Directory.CreateDirectory(area);
            const int x = 86, y = 86;
            var savedTiles = new Tile[12, 7]; var savedChests = (Chest[])Main.chest.Clone();
            for (int dx = 0; dx < 12; dx++) for (int dy = 0; dy < 7; dy++)
            { savedTiles[dx, dy] = Main.tile[x - 2 + dx, y - 3 + dy]; Main.tile[x - 2 + dx, y - 3 + dy] = new Tile(); }
            string body;
            BlueprintObjectPayload named, knownEmpty;
            try
            {
                for (int dx = 0; dx < 6; dx++) Need(WorldGen.PlaceTile(x + dx, y + 1, 1, true, true), "native chest fixture support");
                int left = WorldGen.PlaceChest(x, y, 21, false, 0), right = WorldGen.PlaceChest(x + 4, y, 21, false, 0);
                Need(left >= 0 && right >= 0, "two actual vanilla wooden chest bodies");
                Main.chest[left].name = "已有名字";
                var reader = CaptureReader(host); var records = new List<string>();
                var first = Main.chest[left];
                Need(reader.Read(first.x, first.y, out CaptureCell actualNamed), "native named sample available");
                Need(reader.Read(Main.chest[right].x, Main.chest[right].y, out CaptureCell actualEmpty), "native default name can be sampled");
                named = actualNamed.Payload; knownEmpty = actualEmpty.Payload;
                foreach (int id in new[] { left, right })
                {
                    var chest = Main.chest[id];
                    for (int row = 0; row < 2; row++) for (int column = 0; column < 2; column++)
                    {
                        Need(reader.Read(chest.x + column, chest.y + row, out CaptureCell member) && member.TileType == 21, "native chest member frames");
                        records.Add(FormattableString.Invariant($"{{\"x\":{member.X - first.x},\"y\":{row},\"tileType\":21,\"frameX\":{member.FrameX},\"frameY\":{member.FrameY}}}"));
                    }
                }
                body = "{\"name\":\"双箱蓝图\",\"width\":8,\"height\":3,\"tiles\":[" + String.Join(",", records) + "]";
            }
            finally
            {
                Array.Copy(savedChests, Main.chest, savedChests.Length);
                for (int dx = 0; dx < 12; dx++) for (int dy = 0; dy < 7; dy++) Main.tile[x - 2 + dx, y - 3 + dy] = savedTiles[dx, dy];
            }
            Need(named.NameState == BlueprintPayloadState.Known && knownEmpty.NameState == BlueprintPayloadState.Known && knownEmpty.Name == "" &&
                knownEmpty.Compare(named) == BlueprintWorldMatchState.Conflict && knownEmpty.Compare(knownEmpty) == BlueprintWorldMatchState.Match,
                "native explicitly empty asks for the original default name");
            Need(new BlueprintObjectPayload { NameState = BlueprintPayloadState.Unknown }.Compare(named) == BlueprintWorldMatchState.Unknown,
                "unknown payload is never a cleared or completed name");
            string firstName = new string('长', 19) + "😀余";
            string firstNormalized = Utils.TrimUserString(firstName, 20);
            Need(firstNormalized == new string('长', 19), "original trim excludes a surrogate pair straddling the unit limit");
            string templates = Path.Combine(area, "templates"), counter = Path.Combine(area, "counter.json");
            Guid namedAsset;
            using (var state = State(templates, counter))
            {
                var absent = Import(state, profile, WriteInput(area, "container-absent", body + "}"));
                Need(absent.Status == BlueprintExchangeStatus.Completed, absent.Message);
                foreach (string record in new[] { "", ",\"name\":null", ",\"name\":\"\"" })
                {
                    var empty = Import(state, profile, WriteInput(area, "container-empty", body + ",\"containers\":[{\"x\":0,\"y\":0" + record + "}]}"));
                    Need(empty.Status == BlueprintExchangeStatus.AlreadyExists && empty.AssetId == absent.AssetId, "Helper missing/null/empty container name creates no requirement and has same body hash");
                }
                var first = OpenWindow(state, List(state).Entries.Single());
                Need(first.Cells.All(c => c.Payload.NameState == BlueprintPayloadState.Absent) && first.Cells[0].Payload.Compare(named) == BlueprintWorldMatchState.Match,
                    "Helper body does not ask to clear any existing name");
                var differentNames = Import(state, profile, WriteInput(area, "container-named", body + ",\"containers\":[{\"x\":0,\"y\":0,\"name\":\"" + firstName + "\"},{\"x\":4,\"y\":0,\"name\":\"右箱\"}]}"));
                Need(differentNames.Status == BlueprintExchangeStatus.Completed && differentNames.AssetId != absent.AssetId, "named anchors affect content identity");
                namedAsset = differentNames.AssetId;
            }
            using (var reopened = State(templates, counter))
            {
                var raw = OpenWindow(reopened, List(reopened).Entries.Single(e => e.AssetId == namedAsset));
                var mirror = new BlueprintProjectionView(raw.Summary, true, BlueprintProjectionMask.Empty, (IBlueprintHorizontalMirrorProfile)profile);
                Need(mirror.TryApply(raw, true, out BlueprintProjectionWindow reflected, out string why), "two real named boxes mirror: " + why);
                var anchors = reflected.Cells.Where(c => c.Payload.NameState == BlueprintPayloadState.Known).ToDictionary(c => c.X);
                Need(anchors.Count == 2 && anchors[6].Y == 0 && anchors[6].Payload.Name == firstNormalized && anchors[2].Y == 0 && anchors[2].Payload.Name == "右箱",
                    "names move to each reflected object's new left-top, without old anchor duplicates");
                var reflectedBody = new BlueprintProjectionWindow(raw.Identity, 6, raw.Summary, raw.Bounds, reflected.Cells.ToArray(), reflected.Objects.ToArray());
                Need(mirror.TryApply(reflectedBody, true, out BlueprintProjectionWindow restored, out why), "cold named body can reflect twice: " + why);
                Need(restored.Cells.Where(c => c.Payload.NameState == BlueprintPayloadState.Known).Select(c => new { c.X, c.Y, c.Payload.Name })
                    .SequenceEqual(raw.Cells.Where(c => c.Payload.NameState == BlueprintPayloadState.Known).Select(c => new { c.X, c.Y, c.Payload.Name })), "both original names return to their original anchors");
            }
            Console.WriteLine("HelperImport containers PASS: real native bodies, absent/empty/default/unknown meanings, UTF-16 trim parity and exact cold mirrored anchors");
        }
        internal static void RunWithProfile(IBlueprintImportProfile profile, string sample, string area)
        {
            Directory.CreateDirectory(area);
            byte[] original = File.ReadAllBytes(sample);
            Need(original.Length == 1283762 && original[0] == 239 && original[1] == 187 && original[2] == 191, "literal supplied BOM vector");
            XElement source;
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(original, 3, original.Length - 3, Encoding.UTF8, new XmlDictionaryReaderQuotas { MaxDepth = 64, MaxArrayLength = Int32.MaxValue, MaxStringContentLength = Int32.MaxValue, MaxNameTableCharCount = Int32.MaxValue, MaxBytesPerRead = 65536 }, null)) source = XElement.Load(reader);
            string templates = Path.Combine(area, "templates"), counter = Path.Combine(area, "counter.json"), exported = Path.Combine(area, "normal.jmbp");
            Guid asset; LibraryEntry entry;
            using (var state = State(templates, counter))
            {
                var watch = Stopwatch.StartNew(); var receipt = Import(state, profile, sample);
                Need(receipt.Status == BlueprintExchangeStatus.Completed, "actual JSON publication: " + receipt.Message); asset = receipt.AssetId;
                entry = List(state).Entries.Single(); Need(entry.Name == "岛牌刷怪场4.0" && entry.Width == 195 && entry.Height == 148 && entry.ContentCount == 8060, "formal metadata bounds/name/count");
                var preview = Command(state, state.TryPreview(11, asset, entry.ContentHash, 256, 58));
                Need(preview.Status == LibraryStatus.Completed && preview.Summary.Top == 0 && preview.Summary.Left == 0 && preview.Summary.Width == 195 && preview.Summary.Height == 148, "formal preview preserves blank padding");
                Console.WriteLine("HelperImport publicationMs=" + watch.ElapsedMilliseconds);
                Need(Import(state, profile, sample).Status == BlueprintExchangeStatus.AlreadyExists, "repeat import deduplicates");
                // Same semantic content with reordered root properties and compact JSON.
                string reordered = Path.Combine(area, "reordered.json");
                using (var stream = new MemoryStream())
                {
                    using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false))
                    { new XElement("root", new XAttribute("type", "object"), source.Elements().Reverse()).WriteTo(writer); writer.Flush(); }
                    File.WriteAllBytes(reordered, stream.ToArray());
                }
                Need(Import(state, profile, reordered).Status == BlueprintExchangeStatus.AlreadyExists && List(state).TotalCount == 1, "format/property order independent identity");
                var newer = new XElement(source); newer.AddFirst(new XElement("entities",new XAttribute("type","array")));
                Need(Import(state,profile,WriteJson(area,"new-empty-entities",newer)).Status==BlueprintExchangeStatus.AlreadyExists,
                    "R0.4.9 empty entities before tiles retains the old immutable content identity");
                var containers = new XElement(source); containers.Element("containers").Add(new XElement("item", new XAttribute("type", "object"), new XElement("items", new XAttribute("type", "array"))));
                var containerResult = Import(state, profile, WriteJson(area, "containers", containers));
                Need(containerResult.Status == BlueprintExchangeStatus.Failed, "malformed known container payload is rejected rather than silently discarded");
                Export(state, entry, exported);
                Need(Import(state, null, exported).Status == BlueprintExchangeStatus.AlreadyExists, "old jmbp decoder shares original content identity");
                BadInputs(state, profile, source, area);
                Need(List(state).TotalCount == 1, "failed imports publish no visible assets");
                // A true semantic change creates another asset even with the same name.
                XElement changed = new XElement(source); changed.Element("tiles").Elements().First().Element("invisible").Value = "true";
                string changedPath = WriteJson(area, "changed", changed);
                Need(Import(state, profile, changedPath).Status == BlueprintExchangeStatus.Completed && List(state).TotalCount == 2, "same-name different content coexists");
            }
            using (var state = State(templates, counter))
            {
                entry = List(state).Entries.Single(e => e.AssetId == asset);
                Need(entry.Width == 195 && entry.Height == 148 && entry.ContentCount == 8060, "normal close/reopen library retains exact asset");
                var identity = new BlueprintProjectionIdentity(asset, Guid.Parse(entry.ContentRevision), entry.ContentHash, 701, 1);
                Need(state.RequestProjectionOpen(identity), "formal projection admission");
                var opened = Projection(state); Need(opened.Kind == BlueprintProjectionReceiptKind.Opened, "formal projection opens after restart");
                Need(state.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(0, 0, 195, 148)), "formal full projection window");
                var window = Projection(state).Window;
                Need(window != null && window.Summary.Top == 0 && window.Summary.Height == 148 && window.Cells.Count == 8060 && window.Cells.Min(c => c.Y) == 2, "projection blank top rows and source coordinates");
                Need(window.Objects.Count(o => o.Type == 21 && o.Width == 2 && o.Height == 2) == 39, "original metadata decoded all 39 complete chests");
                Need(window.Objects.Count(o => o.Type == 10 && o.Width == 1 && o.Height == 3) == 1, "original independent-frame closed door");
                Need(window.Cells.Count(c => c.TileType == 19 && c.Slope >= 3) == 25 && window.Cells.Count(c => c.TileType == 314 && c.FrameY == -1) == 2, "Hoik shapes and signed track frame retained");
                Console.WriteLine("HelperImport objects=" + String.Join(",", window.Objects.GroupBy(o => o.Type).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())));
                Billing(window);
                var saved = window.Cells.ToDictionary(c => ((long)c.Y << 32) | (uint)c.X);
                foreach (XElement tile in source.Element("tiles").Elements())
                {
                    CaptureCell c = saved[((long)N(tile, "y") << 32) | (uint)N(tile, "x")];
                    Need((c.Layers & CaptureLayer.Tile) != 0 && c.TileType == N(tile, "tileType") && c.FrameX == N(tile, "frameX") && c.FrameY == N(tile, "frameY") && c.TilePaint == N(tile, "color") && c.Slope == N(tile, "slope") && c.HalfBrick == B(tile, "isHalfBrick") && c.Inactive == B(tile, "inActive") && c.FullbrightTile == B(tile, "fullbright") && c.InvisibleTile == B(tile, "invisible"), "saved tile field fidelity");
                }
                foreach (XElement wall in source.Element("walls").Elements())
                {
                    CaptureCell c = saved[((long)N(wall, "y") << 32) | (uint)N(wall, "x")];
                    Need((c.Layers & CaptureLayer.Wall) != 0 && c.WallType == N(wall, "wallType") && c.WallPaint == N(wall, "color") && c.FullbrightWall == B(wall, "fullbright") && c.InvisibleWall == B(wall, "invisible"), "saved wall field fidelity");
                }
                foreach (XElement wire in source.Element("wires").Elements())
                {
                    CaptureCell c = saved[((long)N(wire, "y") << 32) | (uint)N(wire, "x")];
                    Need(c.Wires == ((B(wire, "red") ? 1 : 0) | (B(wire, "blue") ? 2 : 0) | (B(wire, "green") ? 4 : 0) | (B(wire, "yellow") ? 8 : 0)) && c.Actuator == B(wire, "actuator"), "saved wire/actuator fidelity");
                }
                var mirror = (IBlueprintHorizontalMirrorProfile)profile;
                foreach (CaptureObject obj in window.Objects)
                {
                    var members = window.Cells.Where(c => c.ObjectId == obj.Id).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray(); CaptureObject target; CaptureCell[] transformed; string reason;
                    Need(mirror.TryMirrorObject(obj, members, 195, out target, out transformed, out reason), "imported native object mirrors: " + obj.Type + ":" + reason);
                    Need(target.X == 195 - obj.X - obj.Width && target.Y == obj.Y, "full declared-width mirror and original Y");
                }
                foreach (CaptureCell cell in window.Cells.Where(c => c.ObjectId == 0))
                { CaptureCell target; string reason; Need(mirror.TryMirrorCell(cell, 195, out target, out reason) && target.X == 194 - cell.X && target.Y == cell.Y, "ordinary cell mirror coordinates: type="+cell.TileType+" frame="+cell.FrameX+","+cell.FrameY+" at="+cell.X+","+cell.Y+" reason="+reason); }
                Cooperation(state, profile, sample, entry, identity);
            }
            Need(original.SequenceEqual(File.ReadAllBytes(sample)), "external original never changed");
            Console.WriteLine("HelperImport PASS: real BOM sample 195x148/8060; 6131 tiles,2072 walls,1811 wires; 216 native objects,39 chests; full-field persisted projection; mirrors; stable identity; jmbp export/reimport; bad data; normal reopen. Gameplay/COM/multiplayer/FPS NOT_EVALUATED.");
        }
        private static void BadInputs(LibraryState state, IBlueprintImportProfile profile, XElement source, string area)
        {
            string truncated = Path.Combine(area, "truncated.json"); File.WriteAllText(truncated, "{\"name\":", Encoding.UTF8);
            Need(Import(state, profile, truncated).Status == BlueprintExchangeStatus.Failed, "truncated input rejects");
            var outside = new XElement(source); outside.Element("tiles").Elements().First().Element("x").Value = "195";
            Need(Import(state, profile, WriteJson(area, "outside", outside)).Status == BlueprintExchangeStatus.Failed, "out-of-bounds input rejects");
            var duplicate = new XElement(source); duplicate.Element("tiles").Add(new XElement(duplicate.Element("tiles").Elements().First()));
            Need(Import(state, profile, WriteJson(area, "duplicate", duplicate)).Status == BlueprintExchangeStatus.Failed, "same-layer duplicate rejects");
            var incomplete = new XElement(source); incomplete.Element("tiles").Elements().First(t => N(t, "tileType") == 21).Remove();
            Need(Import(state, profile, WriteJson(area, "incomplete", incomplete)).Status == BlueprintExchangeStatus.Failed, "incomplete native furniture rejects");
            var unknown = new XElement(source); unknown.Element("tiles").Elements().First().Element("tileType").Value = "65535";
            Need(Import(state, profile, WriteJson(area, "unknown", unknown)).Status == BlueprintExchangeStatus.Failed, "unknown native type rejects");
            var numeric = new XElement(source); numeric.Element("tiles").Elements().First().Element("slope").Value = "5";
            Need(Import(state, profile, WriteJson(area, "numeric", numeric)).Status == BlueprintExchangeStatus.Failed, "illegal shape rejects");
            string duplicateProperty = Path.Combine(area, "duplicate-property.json"); File.WriteAllText(duplicateProperty, "{\"name\":\"a\",\"name\":\"b\"}", new UTF8Encoding(false));
            Need(Import(state, profile, duplicateProperty).Status == BlueprintExchangeStatus.Failed, "duplicate JSON properties reject");
            Guid cancelled = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.Import, RequestId = cancelled, Ticket = Ticket("Source", cancelled, duplicateProperty), SupportedTargetVersion = "1.4.5.8", ImportProfile = profile }), "cancellable import admitted");
            state.CancelExchange(cancelled); Need(Await(state).Status == BlueprintExchangeStatus.Cancelled, "cancel before reliable publication retires import");
        }
        private static void Cooperation(LibraryState state, IBlueprintImportProfile profile, string sample, LibraryEntry entry, BlueprintProjectionIdentity identity)
        {
            Guid request = Guid.NewGuid();
            Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.Import, RequestId = request, Ticket = Ticket("Source", request, sample), SupportedTargetVersion = "1.4.5.8", ImportProfile = profile }), "restart import concurrent with existing projection");
            // An intentionally empty catalog proves lane progress, not material
            // support. The existing asset is already fully validated above.
            var catalog = new BlueprintPlacementCatalog(798, Array.Empty<BlueprintPlacementRule>(), new Dictionary<int, BlueprintPlacementDescriptor>(), new Dictionary<int, int>(), new Dictionary<int, int>(), 530, 849);
            var key = new BlueprintMaterialKey(entry.AssetId, false, Guid.Parse(entry.ContentRevision), entry.ContentHash, false, null, catalog.Revision);
            Need(state.RequestMaterials(key, catalog) && state.RequestProjectionWindow(identity, 2, new BlueprintProjectionBounds(0, 0, 20, 20)), "existing material and projection lanes admit concurrent demand");
            bool projected = false, materials = false; BlueprintExchangeReceipt imported = null; var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 30000)
            {
                BlueprintProjectionReceipt next;
                if (state.TryTakeProjectionReceipt(out next)) projected |= next.Kind == BlueprintProjectionReceiptKind.Window;
                materials |= state.Materials(key).Status != BlueprintMaterialStatus.Pending;
                if (state.TryTakeExchangeReceipt(out imported)) break;
                if (projected && materials) break;
                Thread.Sleep(1);
            }
            Need(imported == null && projected && materials, "actual shared projection/material work progresses before sample conversion finishes");
            imported = Await(state); Need(imported.Status == BlueprintExchangeStatus.AlreadyExists, "restart duplicate still settles reliably");
            Console.WriteLine("HelperImport cooperation: existing projection window and material result completed during actual sample import; elapsedMs=" + watch.ElapsedMilliseconds);
        }
        private static void Billing(BlueprintProjectionWindow window)
        {
            // This explicit consumption declaration isolates aggregation from
            // the native placement catalog (which has separate producer tests).
            // The objects and all member coatings are real persisted sample data.
            const int bodyItem = 70001, coatingItem = 70002;
            var catalog = new BlueprintPlacementCatalog(799, new[] { new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, bodyItem, bodyItem, 1, 21, 0, width: 2, height: 2, requiresObject: true), true) },
                new Dictionary<int, BlueprintPlacementDescriptor>(), new Dictionary<int, int>(), new Dictionary<int, int> { { 1, coatingItem } }, 530, 849);
            var total = new BlueprintMaterialAccumulator();
            foreach (CaptureObject obj in window.Objects.Where(o => o.Type == 21))
            {
                CaptureCell first = window.Cells.First(c => c.ObjectId == obj.Id);
                BlueprintMaterialSteps.StaticObject(obj, first, catalog, total);
                foreach (CaptureCell member in window.Cells.Where(c => c.ObjectId == obj.Id))
                { CaptureCell tile = member; tile.Layers = CaptureLayer.Tile; BlueprintMaterialSteps.StaticCell(tile, catalog, total, obj); }
            }
            var result = total.Snapshot(1);
            Need(result.Status == BlueprintMaterialStatus.Exact && result.Rows.Single(r => r.ItemType == bodyItem).Required == 39 && result.Rows.Single(r => r.ItemType == coatingItem).Required == 156,
                "shared material consumers count 39 native chest bodies once and all 156 member fullbright coatings");
        }
        private static string WriteJson(string area, string name, XElement value)
        { string path = Path.Combine(area, name + ".json"); using (var stream = File.Create(path)) using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8)) { value.WriteTo(writer); writer.Flush(); } return path; }
        private static int N(XElement item, string field) { return Int32.Parse(item.Element(field).Value, CultureInfo.InvariantCulture); }
        private static bool B(XElement item, string field) { return item.Element(field).Value == "true"; }
        private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException("HelperImport: " + message); }
        internal static LibraryState State(string path, string counter)
        { var state = new LibraryState(new FileBlueprintLibrary(path)); state.ConfigureExchange(new FileBlueprintExchange(), new AtomicFileDocument(counter, 65536, true)); return state; }
        internal static IBlueprintExchangeTicket Ticket(string method, Guid request, string path)
        { return (IBlueprintExchangeTicket)typeof(WindowsBlueprintFileTicket).GetMethod(method, F).Invoke(null, new object[] { request, path }); }
        internal static BlueprintExchangeReceipt Import(LibraryState state, IBlueprintImportProfile profile, string source)
        { return ImportVersion(state, profile, source, "1.4.5.8"); }
        private static BlueprintExchangeReceipt ImportVersion(LibraryState state, IBlueprintImportProfile profile, string source, string version)
        { Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.Import, RequestId = request, Ticket = Ticket("Source", request, source), SupportedTargetVersion = version, ImportProfile = profile }), "import admitted"); return Await(state); }
        private static BlueprintExchangeReceipt Await(LibraryState state)
        { var watch = Stopwatch.StartNew(); BlueprintExchangeReceipt result; while (watch.ElapsedMilliseconds < 30000) { if (state.TryTakeExchangeReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import exchange"); }
        internal static LibraryReceipt Command(LibraryState state, bool admitted)
        { Need(admitted, "ordinary command admitted"); var watch = Stopwatch.StartNew(); LibraryReceipt result; while (watch.ElapsedMilliseconds < 30000) { state.Pump(); if (state.TryTakeReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import ordinary command"); }
        private static LibraryPage List(LibraryState state) { return Command(state, state.TryList(10, 0)).Page; }
        private static BlueprintProjectionReceipt Projection(LibraryState state)
        { var watch = Stopwatch.StartNew(); BlueprintProjectionReceipt result; while (watch.ElapsedMilliseconds < 30000) { if (state.TryTakeProjectionReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import projection"); }
        internal static void Export(LibraryState state, LibraryEntry entry, string target, string version = "1.4.5.8")
        {
            Guid id = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = id, Kind = BlueprintExchangeKind.PrepareExport }), "prepare export"); var proposal = Await(state).Proposal;
            Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = id, Kind = BlueprintExchangeKind.Export, Proposal = proposal, CounterCandidate = proposal.CounterCandidate, Ticket = Ticket("NewTarget", id, target), SupportedTargetVersion = version, Name = entry.Name, TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash) }), "normal export admitted");
            Need(Await(state).Status == BlueprintExchangeStatus.Completed, "normal export reliably publishes");
        }
    }
}
