using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class ObjectRulesChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { CompleteObjects(); Names(); Selection(); }
            catch (Exception e) { failures.Add("World object rules: " + e.Message); }
        }
        private static readonly Dictionary<long, WorldTargetTile> Tiles = new Dictionary<long, WorldTargetTile>();
        private static WorldTargetTile Read(int x, int y)
        { WorldTargetTile value; return Tiles.TryGetValue(WorldObject.PositionKey(x, y), out value) ? value : default(WorldTargetTile); }
        private static void Put(int x, int y, int type, int style, int width)
        {
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < width; dx++)
                Tiles[WorldObject.PositionKey(x + dx, y + dy)] = new WorldTargetTile { Readable = true, Active = true, Type = type, FrameX = style * width * 18 + dx * 18, FrameY = dy * 18 };
        }
        private static void CompleteObjects()
        {
            Tiles.Clear(); Put(10, 20, 88, 10, 3); Put(13, 20, 88, 10, 3);
            var origins = new HashSet<long>();
            for (int x = 10; x < 16; x++) for (int y = 20; y < 22; y++)
            {
                WorldObject value;
                Require(WorldObjectResolver.TryResolve(x, y, Read(x, y), Read, out value), "every dresser subcell resolves");
                Require(value.Width == 3 && value.Type == 88 && value.Style == 10 && value.TileY == 20, "dresser has exact 3x2 geometry");
                origins.Add(value.Key);
            }
            Require(origins.Count == 2, "two adjacent 3x2 dressers yield exactly two origins, not four 2x2 labels");
            WorldObject found;
            Tiles.Remove(WorldObject.PositionKey(12, 21));
            Require(!WorldObjectResolver.TryResolve(10, 20, Read(10, 20), Read, out found), "unreadable sixth cell suppresses whole dresser");
            Put(10, 20, 88, 10, 3); var bad = Read(12, 21); bad.FrameX -= 18; Tiles[WorldObject.PositionKey(12, 21)] = bad;
            Require(!WorldObjectResolver.TryResolve(10, 20, Read(10, 20), Read, out found), "wrong sixth frame cannot be accepted");
            foreach (int type in new[] { 21, 467, 441, 468, 55, 425, 573, 85 })
            {
                Tiles.Clear(); Put(4, 6, type, 4, 2);
                Require(WorldObjectResolver.TryResolve(5, 7, Read(5, 7), Read, out found) && found.TileX == 4 && found.TileY == 6,
                    "only visible bottom-right cell still resolves full object");
                Require(found.Kind == (type == 85 ? WorldObjectKind.Tombstone : type == 55 || type == 425 || type == 573 ? WorldObjectKind.Sign : WorldObjectKind.Chest), "classification matches actual type");
            }
            foreach (var item in new[] { new[] { 21, 52, 2 }, new[] { 467, 38, 2 }, new[] { 88, 65, 3 }, new[] { 55, 5, 2 }, new[] { 85, 11, 2 }, new[] { 29, 0, 2 }, new[] { 97, 0, 2 } })
            { Tiles.Clear(); Put(4, 6, item[0], item[1], item[2]); Require(!WorldObjectResolver.TryResolve(4, 6, Read(4, 6), Read, out found), "unsupported style or bank is excluded"); }
        }
        private static void Names()
        {
            // These expected strings/indices are independent .8 embedded-language
            // evidence. The Host test additionally exercises the actual native tables.
            Func<ContainerNameFamily, int, string> native = (family, index) =>
                family == ContainerNameFamily.Chest && index == 10 ? "Ivy Chest" :
                family == ContainerNameFamily.Item && index == 3988 ? "Dead Man's Chest" :
                family == ContainerNameFamily.Dresser && index == 10 ? "dresser-style-10" : "WRONG NATIVE ENTRY";
            foreach (int type in new[] { 21, 441 })
                Require(WorldObjectResolver.ContainerName(new WorldObject { Type = type, Style = 10 }, null, native) == "Ivy Chest", "Ivy chest must select exact native class/style, including fake tile");
            Require(WorldObjectResolver.ContainerName(new WorldObject { Type = 467, Style = 4 }, null, native) == "Dead Man's Chest", ".8 467 style 4 follows native ChestUI item3988");
            Require(WorldObjectResolver.ContainerName(new WorldObject { Type = 88, Style = 10 }, null, native) == "dresser-style-10", "dresser uses native dresser family and style index");
            Require(WorldObjectResolver.ContainerName(new WorldObject { Type = 21, Style = 10 }, "My tools", native) == "My tools", "current custom name has priority");
        }
        private static void Selection()
        {
            var selection = new WorldObjectSelection(3);
            selection.SetView(new WorldTargetView(0, 0, 100, 100, 1), 16, 16);
            for (int i = 50; i >= 1; i--) selection.Offer(new WorldObject { TileX = i, TileY = 0, Width = 2 });
            Require(selection.Ordered.Select(v => v.TileX).SequenceEqual(new[] { 1, 2, 3 }), "later nearby objects replace earlier far objects at K");
            selection.SetView(new WorldTargetView(0, 0, 100, 100, 1), 50 * 16, 16);
            for (int i = 1; i <= 50; i++) selection.Offer(new WorldObject { TileX = i, TileY = 0, Width = 2 });
            Require(selection.Ordered.First().TileX == 49, "player movement ranks current centers, not screen center or incumbent order");
            selection.Clear(); selection.SetView(new WorldTargetView(20, 0, 20, 20, 1), 0, 16);
            selection.Offer(new WorldObject { TileX = 0, Width = 2 }); selection.Offer(new WorldObject { TileX = 20, Width = 2 });
            Require(selection.Ordered.First().TileX == 20, "visible first outranks closer offscreen object");
            selection.Clear(); selection.SetView(new WorldTargetView(0, 0, 20, 20, 1), 10 * 16 + 16, 16);
            selection.Offer(new WorldObject { TileX = 11, Width = 2 }); selection.Offer(new WorldObject { TileX = 9, Width = 2 });
            Require(selection.Ordered.First().TileX == 9, "equal distance uses stable coordinates regardless of arrival");
            selection.Offer(new WorldObject { TileX = 9, Width = 3 }); Require(selection.Count == 2, "same origin deduplicates style/type updates");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
