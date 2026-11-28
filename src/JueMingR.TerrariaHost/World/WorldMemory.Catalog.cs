using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.World
{
    internal sealed partial class WorldMemory
    {
        private const int MaxNotes = 256;
        private const string MetadataStart = "<!-- JueMingR.WorldMemory v1 ";
        private const string DocumentName = "world.md";

        internal sealed class Place
        {
            internal Guid Id;
            internal Point Point;
            internal Point? Footing;
            internal string Category, Title, Note, Evidence, DiskStamp;
            internal string[] Aliases = Array.Empty<string>();
            internal int Revision, TitleRevision, NoteRevision, FactRevision;
            internal long ConfirmedUtc;
            internal bool Invalid, Reported;
            internal string Reference => "memory:" + Id.ToString("N");
            internal Place Copy() => (Place)MemberwiseClone();
        }

        private sealed class PlaceBook
        {
            internal readonly string Key;
            internal readonly Dictionary<Guid, Place> Entries;
            internal PlaceBook(string key, Dictionary<Guid, Place> entries = null)
            { Key = key; Entries = entries ?? new Dictionary<Guid, Place>(); }
        }

        private sealed partial class Session
        {
            internal PlaceBook Saved, Pending, Queued;
            internal string PlaceFailure;
            internal long PlaceCommand;
            internal readonly Dictionary<Guid, Place> Corrections = new Dictionary<Guid, Place>();
            internal readonly List<(string Reference, int Revision, Action<CompanionTaskOutcome> Complete)> PlaceReceipts =
                new List<(string, int, Action<CompanionTaskOutcome>)>();
        }

        private void PumpPlaces(Session session)
        {
            if (session.Places.TryTake(out var receipt))
            {
                session.Pending = null;
                if (receipt.Success) { session.Saved = receipt.Value; session.PlaceFailure = null; }
                else session.PlaceFailure = receipt.CommitUnconfirmed ? "世界文档写入未确认，已暂停写入。" :
                    "无法读写世界文档，原文件已保留。";
                for (int i = session.PlaceReceipts.Count - 1; i >= 0; i--)
                {
                    var waiter = session.PlaceReceipts[i];
                    if (receipt.Success && MemoryId(waiter.Reference, out var id) && session.Saved.Entries.TryGetValue(id, out var place) &&
                        place.Revision == waiter.Revision)
                        waiter.Complete(WorldResult("succeeded", Receipt(place), place.Revision, place.Reference));
                    else if (receipt.Success && session.Queued != null) continue;
                    else waiter.Complete(WorldResult("failed", session.PlaceFailure ?? "未确认这一次世界文档修订。"));
                    session.PlaceReceipts.RemoveAt(i);
                }
                if (!receipt.Success) session.Queued = null;
                if (session.Queued != null)
                {
                    var next = session.Queued;
                    session.Queued = null;
                    SubmitPlaces(session, next);
                }
            }
            if (session.Saved == null || session.Pending != null || session.PlaceFailure != null || session.Corrections.Count == 0) return;
            var entries = new Dictionary<Guid, Place>(session.Saved.Entries);
            foreach (var correction in session.Corrections.Values)
                if (entries.TryGetValue(correction.Id, out var current)) entries[correction.Id] = WithFact(current, correction);
            if (SubmitPlaces(session, new PlaceBook(session.Key, entries))) session.Corrections.Clear();
        }

        private static Place WithFact(Place previous, Place observation)
        {
            if (observation.ConfirmedUtc < previous.ConfirmedUtc) return previous;
            var updated = previous.Copy();
            updated.Point = observation.Point;
            updated.Footing = observation.Footing;
            updated.Evidence = observation.Evidence;
            updated.DiskStamp = observation.DiskStamp;
            updated.Invalid = observation.Invalid;
            updated.Reported = observation.Reported;
            updated.ConfirmedUtc = observation.ConfirmedUtc;
            updated.Revision++;
            updated.FactRevision = updated.Revision;
            return updated;
        }

        private static bool SubmitPlaces(Session session, PlaceBook book)
        {
            if (session.Pending != null)
            {
                if (session.Queued != null) return false;
                session.Queued = book;
                return true;
            }
            if (!session.Places.TrySubmit(++session.PlaceCommand, book)) return false;
            session.Pending = book;
            return true;
        }

        internal bool WaitForPlace(string reference, int revision, Action<CompanionTaskOutcome> complete)
        {
            if (!Ready || live.Pending == null || !MemoryId(reference, out Guid id) ||
                !((live.Pending.Entries.TryGetValue(id, out var first) && first.Revision == revision) ||
                 (live.Queued?.Entries.TryGetValue(id, out var second) == true && second.Revision == revision)) ||
                live.PlaceReceipts.Count >= 32) return false;
            live.PlaceReceipts.Add((reference, revision, complete));
            return true;
        }

        private static bool MemoryId(string reference, out Guid id)
        {
            id = Guid.Empty;
            return reference != null && reference.Length == 39 && reference.StartsWith("memory:", StringComparison.Ordinal) &&
                Guid.TryParseExact(reference.Substring(7), "N", out id);
        }

        private static PlaceBook VisiblePlaces(Session session) => session.Queued ?? session.Pending ?? session.Saved;

        internal bool TryGetPlace(string reference, out Place place)
        {
            place = null;
            if (!Ready || !MemoryId(reference, out Guid id)) return false;
            if (live.Corrections.TryGetValue(id, out var correction) &&
                (VisiblePlaces(live)?.Entries.TryGetValue(id, out var revised) ?? false))
            { place = WithFact(revised, correction); return true; }
            return (VisiblePlaces(live)?.Entries.TryGetValue(id, out place) ?? false) ||
                live.Candidates.TryGetValue(id, out place);
        }

        internal IReadOnlyList<Place> FindPlaces(string filter, string category, Point origin, int limit, int offset = 0)
        {
            if (!Ready) return Array.Empty<Place>();
            var entries = VisiblePlaces(live)?.Entries;
            var saved = entries?.Values.AsEnumerable() ?? Enumerable.Empty<Place>();
            return saved.Select(place => live.Corrections.TryGetValue(place.Id, out var correction) ? WithFact(place, correction) : place)
                .Concat(live.Candidates.Values.Where(place => entries?.ContainsKey(place.Id) != true))
                .Where(place => (String.IsNullOrEmpty(category) || place.Category == category) &&
                    (String.IsNullOrWhiteSpace(filter) || SearchMatch(place, filter)))
                .OrderBy(place => place.Invalid).ThenBy(place => DistanceSquared(place.Point, origin))
                .ThenByDescending(place => place.ConfirmedUtc).ThenBy(place => place.Id).Skip(offset)
                .Take(Math.Max(1, Math.Min(16, limit))).ToArray();
        }

        internal string LastSavedShimmer()
        {
            if (!Ready || live.Saved == null) return null;
            var places = live.Saved.Entries.Values.Where(place => place.Category == "shimmer")
                .OrderByDescending(place => place.ConfirmedUtc).Take(2).ToArray();
            return places.Length == 0 || places.Length == 2 && places[0].ConfirmedUtc == places[1].ConfirmedUtc ? null : places[0].Reference;
        }

        private static bool SearchMatch(Place place, string filter) =>
            place.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
            place.Aliases.Any(alias => alias.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
            place.Note.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
            place.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private static long DistanceSquared(Point left, Point right)
        { long x = left.X - right.X, y = left.Y - right.Y; return x * x + y * y; }

        internal string PlaceStatus(Place place)
        {
            if (place.Invalid) return "上次复核确认已失效";
            if (place.Reported) return "转述地点，未现场核实";
            if (!Ready) return "仅有历史记录";
            if (!WorldTileKnowledge.CellKnown(place.Point.X, place.Point.Y)) return "当前未同步，上次见过";
            if (PlaceWitnessCurrent(place)) return "本次世界已实测";
            return "仅有上次观察，使用前重新核实";
        }

        private void WitnessPlace(Place place)
        {
            int page = place.Point.Y / Side * live.Columns + place.Point.X / Side;
            live.Reviewed[place.Id] = (place.ConfirmedUtc, Version(live, page));
        }

        private bool PlaceWitnessCurrent(Place place)
        {
            int page = place.Point.Y / Side * live.Columns + place.Point.X / Side;
            return live.Reviewed.TryGetValue(place.Id, out var witness) && witness.Time == place.ConfirmedUtc &&
                witness.PageRevision == Version(live, page);
        }

        internal CompanionTaskOutcome SavePlace(string reference, int revision, string title, string note,
            string category, Point? reported, Place evidence, string aliases = null)
        {
            if (!Ready || live.Saved == null)
                return WorldResult(live?.PlaceFailure == null ? "waiting" : "failed",
                    live?.PlaceFailure ?? "正在载入当前世界的文档。");
            if (live.PlaceFailure != null) return WorldResult("failed", live.PlaceFailure);
            if (title?.Length > 100 || note?.Length > 3000 || aliases?.Length > 400) return WorldResult("failed", "标题、别名或备注太长。");
            var additions = (aliases ?? "").Split(new[] { '、', '，', ',', '；', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim()).Where(value => value.Length != 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (additions.Length > 12 || additions.Any(alias => alias.Length > 64))
                return WorldResult("failed", "同一地点最多保留 12 个别名，每个最多 64 字。");
            var session = live;
            bool hasReference = !String.IsNullOrEmpty(reference);
            Place old = null;
            if (hasReference && !TryGetPlace(reference, out old)) return WorldResult("failed", "这个地点不属于当前世界或已不可用。");
            if (old != null && VisiblePlaces(session).Entries.ContainsKey(old.Id) &&
                SameRequestedValues(old, title, note, reported, evidence) &&
                additions.All(alias => old.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase)))
            {
                bool saved = session.Saved.Entries.TryGetValue(old.Id, out var row) && row.Revision == old.Revision;
                return WorldResult(saved ? "succeeded" : "waiting", saved ? Receipt(old) : "已受理，等待世界文档保存。", old.Revision, old.Reference);
            }
            if (session.Queued != null) return WorldResult("waiting", "正在保存两条世界文档修订，请稍后再提交。");
            if (old == null && evidence == null && (reported == null || !Inside(reported.Value, session.Width, session.Height)))
                return WorldResult("failed", "需要实际观察引用，或位于当前世界内且注明为转述的坐标。");
            if (old == null && VisiblePlaces(session).Entries.Count >= MaxNotes) return WorldResult("failed", "这个世界的手工地点文档已满。");
            if (old != null && (revision > old.Revision || revision < old.TitleRevision && title != null && title != old.Title ||
                revision < old.NoteRevision && note != null && note != old.Note ||
                revision < old.FactRevision && (reported.HasValue || evidence != null)))
                return WorldResult("conflict", "这个字段已有更新，请重新读取当前修订。", old.Revision, old.Reference);
            var updated = old?.Copy() ?? new Place { Id = evidence?.Id ?? Guid.NewGuid(), Category = NormalizeCategory(category), Title = "", Note = "" };
            if (old == null && VisiblePlaces(session).Entries.TryGetValue(updated.Id, out old))
                return WorldResult("conflict", "此地点已有文档，请用其引用及当前修订补充。", old.Revision, old.Reference);
            if (evidence != null)
            {
                if (old != null && evidence.ConfirmedUtc < old.ConfirmedUtc)
                    return WorldResult("conflict", "现场证据早于已有修订，未覆盖较新的勘验。", old.Revision, old.Reference);
                updated = WithFact(updated, evidence);
                updated.Category = evidence.Category;
            }
            else
            {
                updated.Revision++;
                if (old == null || reported.HasValue)
                {
                    updated.Point = reported.Value;
                    updated.Reported = true; updated.Invalid = false; updated.DiskStamp = null;
                    updated.Evidence = "对话转述的坐标，未核实原版地形。";
                    updated.ConfirmedUtc = DateTime.UtcNow.Ticks;
                    updated.FactRevision = updated.Revision;
                }
            }
            if (title != null) { updated.Title = title.Trim(); updated.TitleRevision = updated.Revision; }
            if (String.IsNullOrEmpty(updated.Title)) updated.Title = DisplayCategory(updated.Category);
            if (note != null) { updated.Note = note; updated.NoteRevision = updated.Revision; }
            // Additions from concurrent actors commute. Renaming keeps the
            // previous lookup name, while a stale conflicting title is still rejected.
            var names = updated.Aliases.Concat(additions);
            if (old != null && old.Title != updated.Title && old.Title.Length <= 64) names = names.Append(old.Title);
            updated.Aliases = names.Where(alias => !String.Equals(alias, updated.Title, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (updated.Aliases.Length > 12) return WorldResult("failed", "这个地点已经达到 12 个别名。");
            var entries = new Dictionary<Guid, Place>(VisiblePlaces(session).Entries) { [updated.Id] = updated };
            return SubmitPlaces(session, new PlaceBook(session.Key, entries)) ?
                WorldResult("waiting", "已受理，等待世界文档保存。", updated.Revision, updated.Reference) :
                WorldResult("failed", "世界文档暂不能写入。");
        }

        private static bool SameRequestedValues(Place old, string title, string note, Point? reported, Place evidence) =>
            (title == null || title.Trim() == old.Title) && (note == null || note == old.Note) &&
            (!reported.HasValue || old.Reported && reported.Value == old.Point) &&
            (evidence == null || !old.Invalid && !old.Reported && evidence.Point == old.Point && evidence.Category == old.Category);

        internal string Receipt(Place place)
        {
            string path = "world-memory/" + live.Key + "/" + DocumentName;
            return DescribePlace(place) + " · " + path + " · " + place.Evidence +
                (String.IsNullOrWhiteSpace(place.Note) ? "" : " · 备注（转述或路线建议）：" + place.Note);
        }

        internal string DescribePlace(Place place) => place.Title + " · " + DisplayCategory(place.Category) + " · 参考格 (" +
            place.Point.X + ", " + place.Point.Y + ") · " + PlaceStatus(place) + " · 修订 " + place.Revision +
            (place.Footing.HasValue ? " · 上次实际落脚 (" + place.Footing.Value.X + ", " + place.Footing.Value.Y + ")" : " · 尚无安全落脚实测") +
            (place.Aliases.Length == 0 ? "" : " · 别名 " + String.Join("、", place.Aliases));

        internal CompanionTaskOutcome ReadPlace(string reference)
        {
            if (!TryGetPlace(reference, out var place)) return live?.Saved == null && live?.PlaceFailure == null ?
                WorldResult("waiting", "正在载入当前世界文档。") :
                WorldResult("failed", live?.PlaceFailure ?? "在当前世界找不到此地点。");
            var durable = live.Saved?.Entries.TryGetValue(place.Id, out var saved) == true && saved.Revision == place.Revision;
            return WorldResult(live.PlaceFailure != null ? "failed" : durable || place.Revision == 0 ? "succeeded" : "waiting",
                Receipt(place) + (live.PlaceFailure != null ? " · " + live.PlaceFailure : ""), place.Revision, place.Reference);
        }

        internal void RecordDryPool(Place place)
        {
            if (!Ready || place.Category != "shimmer" || !WorldTileKnowledge.CellKnown(place.Point.X, place.Point.Y)) return;
            var verified = place.Copy();
            verified.Invalid = true; verified.Reported = false; verified.Evidence = "到场复核，同一池范围内未发现剩余微光。";
            verified.ConfirmedUtc = DateTime.UtcNow.Ticks;
            verified.DiskStamp = live.DiskStamp;
            WitnessPlace(verified);
            if (place.Revision > 0) live.Corrections[place.Id] = verified;
            else live.Candidates[place.Id] = verified;
        }

        internal void RecordShimmerFooting(string reference, Point feet)
        {
            if (!TryGetPlace(reference, out var place) || place.Invalid || place.Revision <= 0 || place.Footing == feet ||
                !WorldTileKnowledge.CellKnown(feet.X, feet.Y)) return;
            var sight = place.Copy();
            sight.Footing = feet;
            sight.Reported = false;
            sight.ConfirmedUtc = DateTime.UtcNow.Ticks;
            sight.Evidence = "到场复核：代表格有原版微光，记录队友实际落脚格。";
            WitnessPlace(sight);
            live.Corrections[place.Id] = sight;
        }

        internal Place ObservePlace(string category, Point point)
        {
            if (!Ready || !WorldTileKnowledge.CellKnown(point.X, point.Y)) return null;
            category = NormalizeCategory(category);
            var tile = Main.tile[point.X, point.Y];
            if (tile == null) return null;
            string evidence;
            switch (category)
            {
                case "shimmer":
                    if (tile.liquid == 0 || !tile.shimmer()) return null;
                    var pool = ShimmerRepresentative(point);
                    point = pool.point;
                    evidence = "原版微光液体，代表格余量 " + Main.tile[point.X, point.Y].liquid + "/255。";
                    break;
                case "ore":
                    if (!tile.active() || !TileID.Sets.Ore[tile.type]) return null;
                    evidence = "现场确认矿物图格类型 " + tile.type + "。";
                    break;
                case "temple":
                    if (!TempleWitness(point)) return null;
                    evidence = "原版未安全神庙墙与神庙建筑相邻，当前通路待到场复核。";
                    break;
                case "dungeon":
                    if (Math.Abs(point.X - Main.dungeonX) > 18 || Math.Abs(point.Y - Main.dungeonY) > 18) return null;
                    evidence = "原版地牢参考点附近的实测位置，入口通路待复核。";
                    break;
                case "biome":
                    if (!BiomeTerrain(tile)) return null;
                    evidence = "现场群系代表图格类型 " + tile.type + "，范围和传播以当前区域为准。";
                    break;
                default:
                    evidence = "已同步实际地形，方块 " + (tile.active() ? tile.type : 0) + "，墙 " + tile.wall + "。";
                    break;
            }
            var place = NewPlace(live, category, point, evidence);
            WitnessPlace(place);
            return place;
        }

        private static bool TempleWitness(Point point)
        {
            int left = Math.Max(0, point.X - 3), top = Math.Max(0, point.Y - 3);
            int right = Math.Min(Main.maxTilesX, point.X + 4), bottom = Math.Min(Main.maxTilesY, point.Y + 4);
            if (!WorldTileKnowledge.RegionKnown(left, top, right, bottom)) return false;
            int walls = 0, bricks = 0;
            for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++)
                {
                    var tile = Main.tile[x, y];
                    if (tile?.wall == WallID.LihzahrdBrickUnsafe) walls++;
                    if (tile?.active() == true && tile.type == TileID.LihzahrdBrick) bricks++;
                }
            return walls >= 6 && bricks >= 4;
        }

        private static bool BiomeTerrain(Tile tile) => tile.active() && (tile.type == TileID.JungleGrass || tile.type == TileID.MushroomGrass ||
            tile.type == TileID.CorruptGrass || tile.type == TileID.CrimsonGrass || tile.type == TileID.HallowedGrass || tile.type == TileID.SnowBlock);

        private static Place NewPlace(Session session, string category, Point point, string evidence)
        {
            var id = Digest(Utf8.GetBytes(session.Key + ":" + category + ":" + point.X + ":" + point.Y));
            return new Place { Id = Guid.ParseExact(id.Substring(0, 32), "N"), Point = point, Title = DisplayCategory(category), Note = "",
                Category = category, Evidence = evidence, ConfirmedUtc = DateTime.UtcNow.Ticks, DiskStamp = session.DiskStamp };
        }

        private static string DisplayCategory(string category)
        {
            switch (category)
            {
                case "shimmer": return "微光池";
                case "ore": return "矿点";
                case "temple": return "丛林神庙";
                case "dungeon": return "地牢";
                case "biome": return "特殊群系";
                case "report": return "世界物质报告";
                default: return "地点";
            }
        }

        private static string NormalizeCategory(string category) =>
            category == "shimmer" || category == "ore" || category == "temple" || category == "dungeon" ||
            category == "biome" || category == "report" ? category : "place";

        internal static CompanionTaskOutcome WorldResult(string state, string detail, int number = 0, string value = null) =>
            new CompanionTaskOutcome(state, detail, number, value);

        private static bool Inside(Point point, int width, int height) => point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;

        private static byte[] EncodePlaces(PlaceBook book)
        {
            // Structured facts and their human-readable projection replace
            // one document atomically. Titles never become filesystem paths.
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes, Utf8, true))
            {
                writer.Write(book.Key); writer.Write(book.Entries.Count);
                var text = new StringBuilder("# 世界记忆\n\n");
                foreach (var place in book.Entries.Values.OrderBy(row => row.Id))
                {
                    writer.Write(place.Id.ToByteArray()); writer.Write(place.Point.X); writer.Write(place.Point.Y);
                    writer.Write(place.Category); writer.Write(place.Title); writer.Write(place.Note); writer.Write(place.Evidence);
                    writer.Write(place.DiskStamp ?? ""); writer.Write(place.Revision); writer.Write(place.TitleRevision);
                    writer.Write(place.NoteRevision); writer.Write(place.FactRevision); writer.Write(place.ConfirmedUtc);
                    writer.Write(place.Invalid); writer.Write(place.Reported); WritePoint(writer, place.Footing);
                    writer.Write(place.Aliases.Length); foreach (var alias in place.Aliases) writer.Write(alias);
                    text.Append("## ").AppendLine(place.Title.Replace('\r', ' ').Replace('\n', ' ')).AppendLine()
                        .Append("图格 ").Append(place.Point.X).Append(", ").Append(place.Point.Y).Append(" · 修订 ").AppendLine(Decimal(place.Revision))
                        .AppendLine(place.Reference).AppendLine(place.Invalid ? "已失效" : place.Reported ? "转述，未实测" : "最后核实记录")
                        .AppendLine(place.Evidence).AppendLine(new DateTime(place.ConfirmedUtc, DateTimeKind.Utc).ToString("u"))
                        .AppendLine(place.Footing.HasValue ? "上次实际落脚 " + place.Footing.Value : "尚无安全落脚实测")
                        .AppendLine(place.Aliases.Length != 0 ? "别名 " + String.Join("、", place.Aliases) : "")
                        .AppendLine().AppendLine(place.Note).AppendLine();
                }
                writer.Flush();
                return Utf8.GetBytes(MetadataStart + Convert.ToBase64String(bytes.ToArray()) + " -->\n" + text);
            }
        }

        private static PlaceBook DecodePlaces(byte[] bytes, string key, int width, int height)
        {
            string text = Utf8.GetString(bytes);
            int end = text.IndexOf(" -->\n", StringComparison.Ordinal);
            if (!text.StartsWith(MetadataStart, StringComparison.Ordinal) || end <= MetadataStart.Length) throw new InvalidDataException();
            using (var reader = new BinaryReader(new MemoryStream(Convert.FromBase64String(text.Substring(MetadataStart.Length, end - MetadataStart.Length))), Utf8))
            {
                if (reader.ReadString() != key) throw new InvalidDataException();
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxNotes) throw new InvalidDataException();
                var book = new PlaceBook(key);
                for (int i = 0; i < count; i++)
                {
                    var place = new Place { Id = new Guid(reader.ReadBytes(16)), Point = new Point(reader.ReadInt32(), reader.ReadInt32()),
                        Category = reader.ReadString(), Title = reader.ReadString(), Note = reader.ReadString(), Evidence = reader.ReadString(),
                        DiskStamp = reader.ReadString(), Revision = reader.ReadInt32(), TitleRevision = reader.ReadInt32(),
                        NoteRevision = reader.ReadInt32(), FactRevision = reader.ReadInt32(), ConfirmedUtc = reader.ReadInt64(),
                        Invalid = reader.ReadBoolean(), Reported = reader.ReadBoolean(), Footing = ReadPoint(reader) };
                    int aliases = reader.ReadInt32();
                    if (aliases < 0 || aliases > 12) throw new InvalidDataException();
                    place.Aliases = Enumerable.Range(0, aliases).Select(_ => reader.ReadString()).ToArray();
                    if (!Inside(place.Point, width, height) || place.Id == Guid.Empty || place.Category != NormalizeCategory(place.Category) ||
                        place.Revision <= 0 || place.Title.Length > 100 || place.Note.Length > 3000 ||
                        place.ConfirmedUtc <= 0 || place.ConfirmedUtc > DateTime.MaxValue.Ticks ||
                        place.Footing.HasValue && !Inside(place.Footing.Value, width, height) ||
                        place.Aliases.Any(alias => alias.Length == 0 || alias.Length > 64) ||
                        new[] { place.TitleRevision, place.NoteRevision, place.FactRevision }.Any(revision => revision < 0 || revision > place.Revision))
                        throw new InvalidDataException();
                    book.Entries.Add(place.Id, place);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length || !bytes.SequenceEqual(EncodePlaces(book)))
                    throw new InvalidDataException("世界文档外部改动，原件已保留。");
                return book;
            }
        }
    }
}
