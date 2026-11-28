using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.World
{
    internal sealed partial class WorldMemory
    {
        private const int Side = 50, ReadBudget = 768, BatchSize = 6;
        private const int LiquidBase = 0x20000, WallBase = 0x10000;

        private sealed class MaterialBook
        {
            internal readonly string Key;
            internal readonly Dictionary<int, MaterialPage> Pages;
            internal readonly HashSet<int> Verified = new HashSet<int>();
            internal string Stamp;
            internal MaterialBook(string key, Dictionary<int, MaterialPage> pages = null)
            { Key = key; Pages = pages ?? new Dictionary<int, MaterialPage>(); }
        }

        private sealed class MaterialPage
        {
            internal int Id, Revision, Count;
            internal long ConfirmedUtc;
            internal Dictionary<int, int> Counts = new Dictionary<int, int>();
            internal Dictionary<int, Point> Samples = new Dictionary<int, Point>();
            internal Point? Temple, Biome;
        }

        private sealed class CapturePage
        {
            internal int Id, Revision, X, Y, W, H, Position;
            internal Cell[] Cells;
        }

        private readonly struct Cell
        {
            internal readonly int Tile, Wall, Liquid;
            internal Cell(Tile tile)
            { Tile = tile?.active() == true ? tile.type : -1; Wall = tile?.wall ?? 0; Liquid = tile?.liquid > 0 ? tile.liquidType() : -1; }
        }

        private sealed partial class Session
        {
            internal int Columns, Rows, NextInitial, NextNetworkSeed, Revision;
            internal int KnownCells;
            internal long IndexCommand;
            internal bool IndexLoaded, IndexProtected, NeedsIndexSave;
            internal string IndexFailure;
            internal ulong NextIndexSave;
            internal readonly Dictionary<int, MaterialPage> Pages = new Dictionary<int, MaterialPage>();
            internal readonly HashSet<int> VerifiedPages = new HashSet<int>();
            internal readonly Dictionary<int, int> Versions = new Dictionary<int, int>();
            internal readonly Queue<int> Work = new Queue<int>();
            internal readonly HashSet<int> QueuedPages = new HashSet<int>();
            internal readonly List<CapturePage> Batch = new List<CapturePage>(BatchSize);
            internal readonly Dictionary<Guid, Place> Candidates = new Dictionary<Guid, Place>();
            internal readonly Dictionary<Guid, (long Time, int PageRevision)> Reviewed = new Dictionary<Guid, (long, int)>();
            internal readonly Dictionary<int, IReadOnlyList<Guid>> CandidatePages = new Dictionary<int, IReadOnlyList<Guid>>();
            internal readonly Dictionary<int, int> Totals = new Dictionary<int, int>();
            internal readonly HashSet<int> LiquidPages = new HashSet<int>();
            internal CapturePage Capture;
            internal Task<MaterialPage[]> Aggregate;
            internal Queue<MaterialPage> Restore;
            internal bool RestoreVerified;
            internal HashSet<int> RestoreStampedPages;
#if DEBUG
            internal long CellsRead;
#endif
        }

        private void StartIndex(Session session)
        {
            session.Columns = (session.Width + Side - 1) / Side;
            session.Rows = (session.Height + Side - 1) / Side;
            if (Inside(new Point(Main.dungeonX, Main.dungeonY), session.Width, session.Height) && (Main.dungeonX != 0 || Main.dungeonY != 0))
            {
                var dungeon = NewPlace(session, "dungeon", new Point(Main.dungeonX, Main.dungeonY), "原版世界元数据中的地牢参考点，入口待验证。");
                session.Candidates[dungeon.Id] = dungeon;
            }
        }

        private void PumpIndex(Session session, ulong tick)
        {
            if (session.DiskRead != null)
            {
                if (!session.DiskRead.IsCompleted) return;
                session.DiskStamp = session.DiskRead.Status == TaskStatus.RanToCompletion ? session.DiskRead.Result : null;
                session.DiskRead = null;
            }
            if (session.Index.TryTake(out var receipt))
            {
                if (!receipt.Success)
                {
                    session.IndexProtected = true;
                    session.IndexFailure = "物质索引磁盘读写未确认，保留原件；本次统计仍可查询。";
                }
                if (receipt.CommandId == 0)
                {
                    session.IndexLoaded = true;
                    if (receipt.Success)
                    {
                        session.RestoreVerified = !hooksUnavailable && session.CompleteSource && session.DiskStamp != null &&
                            session.DiskStamp == receipt.Value.Stamp;
                        session.RestoreStampedPages = receipt.Value.Verified;
                        session.Restore = new Queue<MaterialPage>(receipt.Value.Pages.Values);
                    }
                }
            }
            if (!session.IndexLoaded) return;
            if (session.Restore != null)
            {
                // Resuming a large disk index does not install every row and
                // every candidate in one game update.
                for (int n = 0; n < 16 && session.Restore.Count != 0; n++)
                {
                    var page = session.Restore.Dequeue();
                    InstallPage(session, page, session.RestoreVerified && session.RestoreStampedPages.Contains(page.Id) &&
                        Version(session, page.Id) == 0, witness: false);
                }
                if (session.Restore.Count != 0) return;
                session.Restore = null;
                session.RestoreStampedPages = null;
            }
            if (!session.CompleteSource && session.NextNetworkSeed < session.Columns * session.Rows)
                for (int n = 0; n < 48 && session.NextNetworkSeed < session.Columns * session.Rows; n++)
                {
                    int id = session.NextNetworkSeed++;
                    var rect = Bounds(session, id);
                    if (WorldTileKnowledge.RegionKnown(rect.Left, rect.Top, rect.Right, rect.Bottom)) QueuePage(session, id);
                }

            if (session.Aggregate?.IsCompleted == true)
            {
                var result = session.Aggregate;
                session.Aggregate = null;
                if (result.Status == TaskStatus.RanToCompletion)
                    foreach (var page in result.Result)
                    {
                        if (Version(session, page.Id) == page.Revision)
                        {
                            InstallPage(session, page, true);
                            session.NeedsIndexSave = true;
                        }
                        else QueuePage(session, page.Id);
                    }
                else session.IndexFailure = "有一批物质统计未完成。";
            }
            if (session.Work.Count != 0 || session.Capture != null || session.CompleteSource &&
                session.NextInitial < session.Columns * session.Rows)
                ReadCells(session);
            if (session.Batch.Count != 0 && session.Aggregate == null && (session.Batch.Count == BatchSize ||
                session.Capture == null && session.Work.Count == 0 &&
                (!session.CompleteSource || session.NextInitial >= session.Columns * session.Rows)))
            {
                var pages = session.Batch.ToArray();
                session.Batch.Clear();
                session.Aggregate = Task.Run(() => pages.Select(AggregatePage).ToArray());
            }
            if (session.NeedsIndexSave && !session.IndexProtected && tick >= session.NextIndexSave)
            {
                session.NextIndexSave = tick + 900;
                if (session.Index.TrySubmit(++session.IndexCommand, SnapshotIndex(session)))
                    session.NeedsIndexSave = false;
            }
        }

        private void ReadCells(Session session)
        {
            for (int budget = ReadBudget; budget > 0 && session.Batch.Count < BatchSize;)
            {
                if (session.Capture == null)
                {
                    int id = NextPage(session);
                    if (id < 0) break;
                    var bounds = Bounds(session, id);
                    if (!WorldTileKnowledge.RegionKnown(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom)) continue;
                    session.Capture = new CapturePage { Id = id, Revision = Version(session, id), X = bounds.X, Y = bounds.Y,
                        W = bounds.Width, H = bounds.Height, Cells = new Cell[bounds.Width * bounds.Height] };
                }
                var read = session.Capture;
                int end = Math.Min(read.Position + budget, read.Cells.Length);
                budget -= end - read.Position;
#if DEBUG
                session.CellsRead += end - read.Position;
#endif
                for (; read.Position < end; read.Position++)
                    read.Cells[read.Position] = new Cell(Main.tile[read.X + read.Position % read.W, read.Y + read.Position / read.W]);
                if (end != read.Cells.Length) break;
                if (Version(session, read.Id) == read.Revision) session.Batch.Add(read);
                else QueuePage(session, read.Id);
                session.Capture = null;
            }
        }

        private static int NextPage(Session session)
        {
            if (session.Work.Count != 0)
            {
                int id = session.Work.Dequeue(); session.QueuedPages.Remove(id); return id;
            }
            if (!session.CompleteSource) return -1;
            for (int checkedPages = 0; checkedPages < 64 && session.NextInitial < session.Columns * session.Rows; checkedPages++)
            {
                int id = session.NextInitial++;
                if (!session.VerifiedPages.Contains(id)) return id;
            }
            return -1;
        }

        private static MaterialPage AggregatePage(CapturePage read)
        {
            var page = new MaterialPage { Id = read.Id, Revision = read.Revision, Count = read.Cells.Length, ConfirmedUtc = DateTime.UtcNow.Ticks };
            for (int i = 0; i < read.Cells.Length; i++)
            {
                var cell = read.Cells[i];
                var point = new Point(read.X + i % read.W, read.Y + i / read.W);
                if (cell.Tile >= 0) CountCell(page, cell.Tile, point);
                if (cell.Wall != 0) CountCell(page, WallBase + cell.Wall, point);
                if (cell.Liquid >= 0) CountCell(page, LiquidBase + cell.Liquid, point);
                if (cell.Wall == WallID.LihzahrdBrickUnsafe) page.Temple = point;
                if (cell.Tile == TileID.JungleGrass || cell.Tile == TileID.MushroomGrass || cell.Tile == TileID.CorruptGrass ||
                    cell.Tile == TileID.CrimsonGrass || cell.Tile == TileID.HallowedGrass || cell.Tile == TileID.SnowBlock)
                    page.Biome = point;
            }
            return page;
        }

        private static void CountCell(MaterialPage page, int key, Point point)
        {
            page.Counts.TryGetValue(key, out int count);
            page.Counts[key] = count + 1;
            if (count == 0 && (key < WallBase && key < TileID.Sets.Ore.Length && TileID.Sets.Ore[key] || key == LiquidBase + 3))
                page.Samples[key] = point;
        }

        private void InstallPage(Session session, MaterialPage page, bool current, bool witness = true)
        {
            InvalidateVerifiedPage(session, page.Id);
            session.Pages[page.Id] = page;
            if (current)
            {
                session.VerifiedPages.Add(page.Id);
                ApplyCounts(session, page, 1);
            }
            if (session.CandidatePages.TryGetValue(page.Id, out var prior))
                foreach (var id in prior)
                {
                    session.Candidates.Remove(id);
                    if ((session.Pending ?? session.Saved)?.Entries.ContainsKey(id) != true) session.Reviewed.Remove(id);
                }
            var ids = new List<Guid>();
            foreach (var sample in page.Samples)
            {
                string category = sample.Key == LiquidBase + 3 ? "shimmer" : "ore";
                var fact = current && witness ? ObservePlace(category, sample.Value, automatic: true) : null;
                // The material counts and sparse coordinate still exist when
                // an unfinished shimmer component cannot identify a pool.
                if (category == "shimmer" && fact == null) continue;
                if (fact == null)
                {
                    var place = NewPlace(session,category,sample.Value,category == "ore" ? "物质索引中的矿物图格 " + sample.Key + "。" : "区域索引见过微光液体。");
                    if (category == "ore") place.OreTileType = sample.Key;
                    Add(place);
                }
                else Add(fact);
            }
            if (page.Temple.HasValue)
            {
                var temple = current && witness ? ObservePlace("temple", page.Temple.Value) :
                    NewPlace(session, "temple", page.Temple.Value, "区域索引曾见原版神庙墙，入口待验证。");
                if (temple != null) Add(temple);
            }
            if (page.Biome.HasValue)
            {
                var biome = current && witness ? ObservePlace("biome", page.Biome.Value) :
                    NewPlace(session, "biome", page.Biome.Value, "区域索引曾见代表群系方块。");
                if (biome != null) Add(biome);
            }
            if (ids.Count != 0) session.CandidatePages[page.Id] = ids;
            else session.CandidatePages.Remove(page.Id);
            void Add(Place place)
            {
                if (session.Candidates.ContainsKey(place.Id)) return;
                session.Candidates[place.Id] = place;
                ids.Add(place.Id);
            }
        }

        private static void ApplyCounts(Session session, MaterialPage page, int direction)
        {
            session.KnownCells += direction * page.Count;
            foreach (var entry in page.Counts)
            {
                session.Totals.TryGetValue(entry.Key, out int prior);
                int next = prior + direction * entry.Value;
                if (next == 0) session.Totals.Remove(entry.Key);
                else session.Totals[entry.Key] = next;
            }
        }

        private static void InvalidateVerifiedPage(Session session, int id)
        {
            if (session.VerifiedPages.Remove(id)) ApplyCounts(session, session.Pages[id], -1);
        }

        private static Rectangle Bounds(Session session, int id)
        {
            int x = id % session.Columns * Side, y = id / session.Columns * Side;
            return new Rectangle(x, y, Math.Min(Side, session.Width - x), Math.Min(Side, session.Height - y));
        }

        private static int Version(Session session, int id) => session.Versions.TryGetValue(id, out int value) ? value : 0;

        private static void QueuePage(Session session, int id)
        { if (session.QueuedPages.Add(id)) session.Work.Enqueue(id); }

        internal void Mark(Rectangle region, bool changed = true)
        {
            if (!Ready) return;
            var session = live;
            var area = Rectangle.Intersect(region, new Rectangle(0, 0, session.Width, session.Height));
            if (area.Width <= 0 || area.Height <= 0) return;
            for (int y = area.Top / Side; y <= (area.Bottom - 1) / Side; y++)
                for (int x = area.Left / Side; x <= (area.Right - 1) / Side; x++)
                {
                    int id = y * session.Columns + x;
                    if (!session.CompleteSource)
                    {
                        var rect = Bounds(session, id);
                        if (!WorldTileKnowledge.RegionKnown(rect.Left, rect.Top, rect.Right, rect.Bottom)) continue;
                    }
                    if (changed)
                    {
                        session.Versions[id] = ++session.Revision;
                        InvalidateVerifiedPage(session, id);
                    }
                    QueuePage(session, id);
                }
        }

        internal void MarkLiquidPages(BinaryReader reader, int count)
        {
            if (!Ready) return;
            var session = live;
            var affected = session.LiquidPages;
            affected.Clear();
            for (int i = 0; i < count; i++)
            {
                int address = reader.ReadInt32();
                reader.ReadUInt16(); // original amount and liquid kind are already applied
                int x = (address >> 16) & 0xffff, y = address & 0xffff;
                if (x < session.Width && y < session.Height && WorldTileKnowledge.CellKnown(x, y))
                    affected.Add(y / Side * session.Columns + x / Side);
            }
            foreach (int id in affected)
            {
                session.Versions[id] = ++session.Revision;
                InvalidateVerifiedPage(session, id);
                QueuePage(session, id);
            }
        }

        private MaterialBook SnapshotIndex(Session session)
        {
            // Only the confirmed original load fingerprint may promote a disk
            // snapshot. A changed world can still reuse historical rows while
            // the live scan replaces them.
            string stamp = session.Revision == 0 ? session.DiskStamp : null;
            var snapshot = new MaterialBook(session.Key, new Dictionary<int, MaterialPage>(session.Pages)) { Stamp = stamp };
            if (stamp != null) snapshot.Verified.UnionWith(session.VerifiedPages);
            return snapshot;
        }

        private void FinishIndex(Session session)
        {
            var final = session.IndexLoaded && !session.IndexProtected && session.NeedsIndexSave ? SnapshotIndex(session) : null;
            session.Index.BeginStop(final == null ? (Func<MaterialBook, MaterialBook>)null : _ => final);
        }

        internal CompanionTaskOutcome FindMaterials(string layer, int type, Point origin, int radius, bool report)
        {
            if (!Ready || !live.IndexLoaded) return WorldResult("waiting", "正在等待当前世界的物质索引。");
            int first;
            switch (layer)
            {
                case "wall": first = WallBase; break;
                case "liquid": first = LiquidBase; break;
                case "tile": first = 0; break;
                default: return WorldResult("failed", "物质层请选择 tile、wall 或 liquid。");
            }
            if (type < -1 || type >= WallBase || first == LiquidBase && type > 3)
                return WorldResult("failed", "这个图格物质类型无效。");
            var session = live;
            radius = Math.Max(0, Math.Min(400, radius));
            int wanted = first + type;
            var area = radius == 0 ? new Rectangle(0, 0, session.Width, session.Height) :
                Rectangle.Intersect(new Rectangle(origin.X - radius, origin.Y - radius, radius * 2 + 1, radius * 2 + 1),
                    new Rectangle(0, 0, session.Width, session.Height));
            if (type >= 0)
            {
                // A query may catch a native path without an invalidation
                // event. Drop its outdated count before reporting coverage.
                var witnesses = session.VerifiedPages.Select(id => session.Pages[id]).Where(page =>
                    area.Intersects(Bounds(session, page.Id)) && page.Samples.ContainsKey(wanted))
                    .OrderBy(page => DistanceSquared(page.Samples[wanted], origin)).Take(16).ToArray();
                foreach (var witness in witnesses)
                    if (!StillMaterial(witness.Samples[wanted], wanted)) Mark(Bounds(session, witness.Id));
            }
            var current = session.VerifiedPages.Select(id => session.Pages[id]).Where(page => area.Intersects(Bounds(session, page.Id))).ToArray();
            long areaCells = radius == 0 ? session.Width * session.Height : 0;
            long knownCells = radius == 0 ? session.KnownCells : current.Sum(page => page.Count);
            if (radius != 0 && !area.IsEmpty)
                for (int y = area.Top / Side; y <= (area.Bottom - 1) / Side; y++)
                    for (int x = area.Left / Side; x <= (area.Right - 1) / Side; x++)
                    { var rect = Bounds(session, y * session.Columns + x); areaCells += rect.Width * rect.Height; }
            var totals = new Dictionary<int, int>();
            if (radius == 0)
            {
                foreach (var entry in session.Totals)
                    if (entry.Key >= first && entry.Key < first + WallBase && (type < 0 || entry.Key == wanted))
                        totals[entry.Key] = entry.Value;
            }
            else
                foreach (var page in current)
                    foreach (var entry in page.Counts)
                        if (entry.Key >= first && entry.Key < first + WallBase && (type < 0 || entry.Key == wanted))
                        { totals.TryGetValue(entry.Key, out int count); totals[entry.Key] = count + entry.Value; }
            int actual = type >= 0 && totals.TryGetValue(wanted, out int quantity) ? quantity : 0;
            var regions = type < 0 ? Array.Empty<MaterialPage>() : current.Where(page => page.Counts.ContainsKey(wanted))
                .OrderByDescending(page => page.Counts[wanted]).ThenBy(page => page.Id).Take(4).ToArray();
            int historic = type < 0 ? 0 : session.Pages.Values.Where(page => !session.VerifiedPages.Contains(page.Id) && area.Intersects(Bounds(session, page.Id)))
                .Sum(page => page.Counts.TryGetValue(wanted, out int count) ? count : 0);
            var nearby = type >= 0 ? current.Where(page => page.Samples.ContainsKey(wanted)).Select(page => page.Samples[wanted])
                .OrderBy(point => DistanceSquared(point, origin)).Take(16).Where(point => StillMaterial(point, wanted)).ToArray() : Array.Empty<Point>();
            Point? near = nearby.Length == 0 ? (Point?)null : nearestKnown();
            Place closest = near.HasValue ? ObservePlace(first == LiquidBase ? "shimmer" : "ore", near.Value) : null;
            if (closest != null) session.Candidates[closest.Id] = closest;
            string detail = "已核实覆盖 " + knownCells + "/" + areaCells + " 格；" +
                (session.CompleteSource ? "本地地形。" : "联机仅含原版已同步地形，其他区域未知。") +
                "按相交的 50×50 分块计算图格数，非掉落数量。" +
                (type >= 0 ? " 类型 " + type + " 共 " + actual + " 格。" :
                    " 分布 " + String.Join("；", totals.OrderByDescending(entry => entry.Value).Take(12).Select(entry => (entry.Key - first) + " × " + entry.Value))) +
                (regions.Length == 0 ? "" : " 数量较多的分块（左上格）：" + String.Join("；", regions.Select(page =>
                    Bounds(session, page.Id).Location + " × " + page.Counts[wanted])) + "。") +
                (historic == 0 ? "" : " 另有上次见过 " + historic + " 格，未计入本次覆盖。") +
                (closest == null ? "" : " 最近已复核候选 " + closest.Reference + " (" + closest.Point.X + ", " + closest.Point.Y +
                    ")，距参考点约 " + (int)Math.Sqrt(DistanceSquared(closest.Point, origin)) + " 格。") +
                (wanted == LiquidBase + 3 && near.HasValue && closest == null ?
                    " 已知微光图格 (" + near.Value.X + ", " + near.Value.Y + ")，池界尚未核实。" : "") +
                (hooksUnavailable ? " 自动地形变化通知不可用，索引仅代表上次扫描。" : "") +
                (session.IndexFailure == null ? "" : " " + session.IndexFailure);
            if (!report) return WorldResult("succeeded", detail, actual, closest?.Reference);
            Point referencePoint = Inside(origin, session.Width, session.Height) ? origin : new Point(0, 0);
            var fact = NewPlace(session, "report", referencePoint, "由当前物质索引生成，包含实际覆盖、统计口径和参考点。");
            fact.Id = Guid.ParseExact(Digest(Utf8.GetBytes(fact.Reference + ":" + layer + ":" + type + ":" + radius)).Substring(0, 32), "N");
            WitnessPlace(fact);
            bool existing = TryGetPlace(fact.Reference, out var previous);
            return SavePlace(existing ? fact.Reference : null, existing ? previous.Revision : 0, "世界物质报告", detail, "report", null, fact);

            Point nearestKnown()
            {
                Point best = nearby[0];
                long distance = DistanceSquared(best, origin);
                var candidates = current.Where(page => page.Samples.ContainsKey(wanted)).Select(page => Bounds(session, page.Id))
                    .OrderBy(bounds => DistanceSquared(NearestInside(origin, bounds), origin)).Take(3);
                foreach (var bounds in candidates)
                {
                    Point center = NearestInside(origin, bounds);
                    if (DistanceSquared(center, origin) >= distance) break;
                    var target = Rectangle.Intersect(bounds, new Rectangle(center.X - 7, center.Y - 7, 15, 15));
                    for (int y = target.Top; y < target.Bottom; y++)
                        for (int x = target.Left; x < target.Right; x++)
                        {
                            var point = new Point(x, y);
                            long next = DistanceSquared(point, origin);
                            if (next >= distance || !StillMaterial(point, wanted)) continue;
                            best = point; distance = next;
                        }
                }
                return best;
            }
        }

        private static Point NearestInside(Point point, Rectangle bounds) => new Point(
            Math.Max(bounds.Left, Math.Min(bounds.Right - 1, point.X)), Math.Max(bounds.Top, Math.Min(bounds.Bottom - 1, point.Y)));

        private static bool StillMaterial(Point point, int key)
        {
            if (!WorldTileKnowledge.CellKnown(point.X, point.Y)) return false;
            var tile = Main.tile[point.X, point.Y];
            return tile != null && (key >= LiquidBase ? tile.liquid > 0 && LiquidBase + tile.liquidType() == key :
                key >= WallBase ? tile.wall + WallBase == key : tile.active() && tile.type == key);
        }

        private static byte[] EncodeMaterials(MaterialBook book)
        {
            using (var bytes = new MemoryStream())
            {
                using (var compressed = new DeflateStream(bytes, CompressionMode.Compress, true))
                using (var writer = new BinaryWriter(compressed, Utf8))
                {
                    writer.Write("JueMingR.WorldMaterials.2"); writer.Write(book.Key); writer.Write(book.Stamp ?? "");
                    writer.Write(book.Pages.Count);
                    foreach (var page in book.Pages.Values)
                    {
                        writer.Write(page.Id); writer.Write(page.Count); writer.Write(page.ConfirmedUtc);
                        writer.Write(book.Verified.Contains(page.Id));
                        writer.Write(page.Counts.Count);
                        foreach (var row in page.Counts) { writer.Write(row.Key); writer.Write(row.Value); }
                        writer.Write(page.Samples.Count);
                        foreach (var row in page.Samples) { writer.Write(row.Key); writer.Write(row.Value.X); writer.Write(row.Value.Y); }
                        WritePoint(writer, page.Temple); WritePoint(writer, page.Biome);
                    }
                }
                return bytes.ToArray();
            }
        }

        private static void WritePoint(BinaryWriter writer, Point? point)
        { writer.Write(point.HasValue); if (point.HasValue) { writer.Write(point.Value.X); writer.Write(point.Value.Y); } }

        private static MaterialBook DecodeMaterials(byte[] bytes, string key, int width, int height)
        {
            using (var compressed = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress))
            using (var reader = new BinaryReader(compressed, Utf8))
            {
                string format = reader.ReadString();
                bool perPageStamp = format == "JueMingR.WorldMaterials.2";
                if (!perPageStamp && format != "JueMingR.WorldMaterials.1" || reader.ReadString() != key) throw new InvalidDataException();
                var book = new MaterialBook(key) { Stamp = reader.ReadString() };
                int columns = (width + Side - 1) / Side, rows = (height + Side - 1) / Side, count = reader.ReadInt32();
                if (count < 0 || count > columns * rows || book.Stamp.Length != 0 && book.Stamp.Length != 64) throw new InvalidDataException();
                for (int i = 0; i < count; i++)
                {
                    var page = new MaterialPage { Id = reader.ReadInt32(), Count = reader.ReadInt32(), ConfirmedUtc = reader.ReadInt64() };
                    if (page.Id < 0 || page.Id >= columns * rows || page.Count <= 0 || page.Count > Side * Side) throw new InvalidDataException();
                    // Earlier snapshots had no trustworthy page-level proof.
                    // Load their counts as history even if the file stamp matches.
                    if (perPageStamp && reader.ReadBoolean())
                    {
                        if (book.Stamp.Length == 0) throw new InvalidDataException();
                        book.Verified.Add(page.Id);
                    }
                    int entries = reader.ReadInt32();
                    if (entries < 0 || entries > page.Count * 3) throw new InvalidDataException();
                    for (int j = 0; j < entries; j++)
                    {
                        int material = reader.ReadInt32(), quantity = reader.ReadInt32();
                        if (material < 0 || material > LiquidBase + 3 || quantity < 1 || quantity > page.Count) throw new InvalidDataException();
                        page.Counts.Add(material, quantity);
                    }
                    int samples = reader.ReadInt32();
                    if (samples < 0 || samples > entries) throw new InvalidDataException();
                    for (int j = 0; j < samples; j++)
                    {
                        int material = reader.ReadInt32(); var point = new Point(reader.ReadInt32(), reader.ReadInt32());
                        if (!page.Counts.ContainsKey(material) || !Inside(point, width, height)) throw new InvalidDataException();
                        page.Samples.Add(material, point);
                    }
                    page.Temple = ReadPoint(reader); page.Biome = ReadPoint(reader);
                    book.Pages.Add(page.Id, page);
                }
                if (compressed.ReadByte() != -1) throw new InvalidDataException();
                return book;
            }
        }

        private static Point? ReadPoint(BinaryReader reader) => reader.ReadBoolean() ? new Point(reader.ReadInt32(), reader.ReadInt32()) : (Point?)null;
    }
}
