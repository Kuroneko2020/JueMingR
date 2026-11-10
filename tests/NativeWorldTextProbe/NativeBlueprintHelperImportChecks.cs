using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintHelperImportChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            var host = NativeInformationChecks.Get(context, "Blueprint");
            var profile = (IBlueprintImportProfile)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver").GetMethod("Create", F).Invoke(null, null);
            RunWithProfile(profile, Path.Combine(Program.Repository, "references/JMR_Blueprint_Hoik_Zoom_20261110_v1/attachments/岛牌刷怪场4.0.json"), Path.Combine(Program.OutputDirectory, "helper-import-" + Guid.NewGuid().ToString("N")));
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
                var containers = new XElement(source); containers.Element("containers").Add(new XElement("item", new XAttribute("type", "object"), new XElement("items", new XAttribute("type", "array"))));
                var containerResult = Import(state, profile, WriteJson(area, "containers", containers));
                Need(containerResult.Status == BlueprintExchangeStatus.AlreadyExists && containerResult.Message.Contains("容器附加内容未导入"), "nonempty container payload warns once without changing building identity");
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
                { CaptureCell target; string reason; Need(mirror.TryMirrorCell(cell, 195, out target, out reason) && target.X == 194 - cell.X && target.Y == cell.Y, "ordinary cell mirror coordinates"); }
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
        private static LibraryState State(string path, string counter)
        { var state = new LibraryState(new FileBlueprintLibrary(path)); state.ConfigureExchange(new FileBlueprintExchange(), new AtomicFileDocument(counter, 65536, true)); return state; }
        private static IBlueprintExchangeTicket Ticket(string method, Guid request, string path)
        { return (IBlueprintExchangeTicket)typeof(WindowsBlueprintFileTicket).GetMethod(method, F).Invoke(null, new object[] { request, path }); }
        private static BlueprintExchangeReceipt Import(LibraryState state, IBlueprintImportProfile profile, string source)
        { Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.Import, RequestId = request, Ticket = Ticket("Source", request, source), SupportedTargetVersion = "1.4.5.8", ImportProfile = profile }), "import admitted"); return Await(state); }
        private static BlueprintExchangeReceipt Await(LibraryState state)
        { var watch = Stopwatch.StartNew(); BlueprintExchangeReceipt result; while (watch.ElapsedMilliseconds < 30000) { if (state.TryTakeExchangeReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import exchange"); }
        private static LibraryReceipt Command(LibraryState state, bool admitted)
        { Need(admitted, "ordinary command admitted"); var watch = Stopwatch.StartNew(); LibraryReceipt result; while (watch.ElapsedMilliseconds < 30000) { state.Pump(); if (state.TryTakeReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import ordinary command"); }
        private static LibraryPage List(LibraryState state) { return Command(state, state.TryList(10, 0)).Page; }
        private static BlueprintProjectionReceipt Projection(LibraryState state)
        { var watch = Stopwatch.StartNew(); BlueprintProjectionReceipt result; while (watch.ElapsedMilliseconds < 30000) { if (state.TryTakeProjectionReceipt(out result)) return result; Thread.Sleep(1); } throw new TimeoutException("helper import projection"); }
        private static void Export(LibraryState state, LibraryEntry entry, string target)
        {
            Guid id = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = id, Kind = BlueprintExchangeKind.PrepareExport }), "prepare export"); var proposal = Await(state).Proposal;
            Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = id, Kind = BlueprintExchangeKind.Export, Proposal = proposal, CounterCandidate = proposal.CounterCandidate, Ticket = Ticket("NewTarget", id, target), SupportedTargetVersion = "1.4.5.8", Name = entry.Name, TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash) }), "normal export admitted");
            Need(Await(state).Status == BlueprintExchangeStatus.Completed, "normal export reliably publishes");
        }
    }
}
