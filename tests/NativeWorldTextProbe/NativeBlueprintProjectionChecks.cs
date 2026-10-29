using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Expected geometry below is literal locked-.8 native geometry/raw frame
    // evidence. It never asks the production shape helper to form an oracle.
    internal static class NativeBlueprintProjectionChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var assembly = host.GetType().Assembly;
            var builder = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionRendererCommands", true);
            var rendererType = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionRenderer", true);
            // Registered after CaptureContent in the same native composition.
            // Original object metadata locks after startup; reuse it instead of
            // rerunning Initialize or bypassing that lock for this specialist.
            Require(TileObjectData.GetTileData(79, 3) != null, "native capture specialist initialized locked object metadata before projection checks");
            var world = Main.tile; var player = Main.LocalPlayer;
            var inventory = player.inventory; int selected = player.selectedItem;
            var cell = Tile(1, 2, 3, 36, 54);
            var normal = Build(builder, new[] { cell });
            Require(normal.Count == 1 && Rect(normal[0]) == new Rectangle(36, 54, 16, 16) && Pos(normal[0]) == new Vector2(32, 48), "full brick keeps raw source and 16px local position");
            cell.HalfBrick = true;
            var half = Build(builder, new[] { cell, Tile(1, 2, 4, 0, 0) });
            Require(half.Count == 2 && Rect(half[0]) == new Rectangle(36, 54, 16, 8) && Pos(half[0]) == new Vector2(32, 56), "half brick over solid crops eight source rows and moves down eight without rescaling");
            var exposed = Build(builder, new[] { cell });
            Require(exposed.Count == 2 && Rect(exposed[0]) == new Rectangle(36, 54, 16, 4) && Rect(exposed[1]) == new Rectangle(144, 66, 16, 4) &&
                Pos(exposed[0]) == new Vector2(32, 56) && Pos(exposed[1]) == new Vector2(32, 60), "exposed native half-brick keeps independent four-pixel bottom edge");
            cell.HalfBrick = false;
            // Native eight 2px columns and cap: the upper pair's final column
            // has zero height; lower pair keeps all eight positive columns.
            int[][] expectedX = { new[] { 0, 2, 4, 6, 8, 10, 12 }, new[] { 14, 12, 10, 8, 6, 4, 2 },
                new[] { 0, 2, 4, 6, 8, 10, 12, 14 }, new[] { 14, 12, 10, 8, 6, 4, 2, 0 } };
            int[][] expectedH = { new[] { 14, 12, 10, 8, 6, 4, 2 }, new[] { 14, 12, 10, 8, 6, 4, 2 },
                new[] { 16, 14, 12, 10, 8, 6, 4, 2 }, new[] { 16, 14, 12, 10, 8, 6, 4, 2 } };
            for (byte slope = 1; slope <= 4; slope++)
            {
                cell.Slope = slope; var values = Build(builder, new[] { cell }); int count = expectedX[slope - 1].Length;
                Require(values.Count == count + 1, "slope strip/cap count " + slope);
                for (int i = 0; i < count; i++)
                {
                    int x = expectedX[slope - 1][i], h = expectedH[slope - 1][i];
                    Require(Rect(values[i]) == new Rectangle(36 + x, 54 + (slope <= 2 ? 0 : i * 2), 2, h) &&
                        Pos(values[i]) == new Vector2(32 + x, 48 + (slope <= 2 ? i * 2 : 0)), "literal native slope column " + slope + "/" + i);
                }
                Require(Rect(values[count]) == new Rectangle(36, slope <= 2 ? 68 : 54, 16, 2) &&
                    Pos(values[count]) == new Vector2(32, slope <= 2 ? 62 : 48), "literal native slope cap " + slope);
            }
            var stair = Tile(19, 2, 3, 144, 0); stair.Slope = 1;
            var east = Tile(1, 3, 4, 0, 0);
            var platform = Build(builder, new[] { stair, east });
            Require(platform.Count == 3 && Rect(platform[0]) == new Rectangle(144, 0, 16, 16) && Rect(platform[1]) == new Rectangle(198, 0, 16, 16) &&
                Pos(platform[1]) == new Vector2(32, 64), "sloped platform preserves whole stair frame plus native solid-neighbor join; no solid triangle slicing");
            stair.Slope = 2; east.X = 1;
            var westPlatform = Build(builder, new[] { stair, east });
            Require(Rect(westPlatform[1]).X == 162, "opposite platform native join uses its independent source X");
            stair.Slope = 0;
            Require(Build(builder, new[] { stair }).Count == 1, "flat platform emits one raw frame");
            PlatformJoinBoundaries(builder);

            Furniture(builder);
            NaturalAndLayers(builder);
            WorldClip(builder);
            WindowAndCache(builder, rendererType);
            Require(ReferenceEquals(Main.tile, world) && ReferenceEquals(player.inventory, inventory) && player.selectedItem == selected,
                "pure command/cache path leaves world and inventory owners unchanged");
            Console.WriteLine("PASS Blueprint projection CPU: literal half/four slopes/platform joins, raw door/furniture members, natural extents/source flip, walls/liquid/circuits/coatings, pylon separate body, halo publication and origin-independent command cache. GPU/native-Main drawing remains separate evidence.");
        }

        private static void PlatformJoinBoundaries(Type builder)
        {
            // Locked .8 DrawBasicTile: slope 1 blocks below on blockType 0/5,
            // slope 2 on 0/4. Tile.blockType gives half priority over slope.
            // These literal cases also keep active distinct from inActive.
            Require(TileID.Sets.BlocksStairs[TileID.Glass] && Main.tileSolid[TileID.Glass] &&
                TileID.Sets.BlocksStairs[TileID.TrapdoorOpen] && !Main.tileSolid[TileID.TrapdoorOpen],
                "locked native glass and open-trapdoor stair-blocking metadata");
            var stair = Tile(19, 2, 3, 144, 36); stair.Slope = 1; stair.TilePaint = 11;
            var below = Tile(TileID.Glass, 2, 4, 18, 18); below.TilePaint = 4;
            var diagonal = Tile(1, 3, 4, 36, 54); diagonal.TilePaint = 2;
            below.Inactive = true;
            RequirePlatformJoin(builder, stair, below, diagonal, -1, "inactive full glass still blocks the join below");
            below.Inactive = false; below.HalfBrick = true;
            RequirePlatformJoin(builder, stair, below, diagonal, 198, "half glass is blockType 1, not full blockType 0");
            below.Slope = 4;
            RequirePlatformJoin(builder, stair, below, diagonal, 198, "half flag takes precedence over a stored slope");
            below.HalfBrick = false; below.Slope = 3;
            RequirePlatformJoin(builder, stair, below, diagonal, 198, "slope 1 permits blockType 4 below");
            stair.Slope = 2; diagonal.X = 1;
            RequirePlatformJoin(builder, stair, below, diagonal, -1, "slope 2 is blocked by blockType 4 below");
            below.Slope = 4;
            RequirePlatformJoin(builder, stair, below, diagonal, 162, "slope 2 permits blockType 5 below");
            stair.Slope = 1; diagonal.X = 3;
            RequirePlatformJoin(builder, stair, below, diagonal, -1, "slope 1 is blocked by blockType 5 below");
            below = Tile(TileID.TrapdoorOpen, 2, 4, 0, 0);
            RequirePlatformJoin(builder, stair, below, diagonal, -1, "non-solid active open trapdoor blocks directly below");

            // A differently painted flat platform supplies only geometry.
            // Projection visibility keeps actuated/invisible source content.
            below = default(CaptureCell);
            diagonal = Tile(19, 3, 4, 0, 90); diagonal.TilePaint = 4;
            diagonal.Inactive = diagonal.InvisibleTile = true;
            stair.Inactive = stair.InvisibleTile = true;
            RequirePlatformJoin(builder, stair, below, diagonal, 324, "inactive invisible right diagonal flat platform supports join");
            stair.Slope = 2; diagonal.X = 1;
            RequirePlatformJoin(builder, stair, below, diagonal, 306, "inactive invisible left diagonal flat platform supports join");
            diagonal = Tile(TileID.TrapdoorOpen, 1, 4, 0, 0);
            RequirePlatformJoin(builder, stair, below, diagonal, -1, "non-solid open trapdoor cannot support the diagonal join");
        }
        private static void RequirePlatformJoin(Type builder, CaptureCell stair, CaptureCell below, CaptureCell diagonal, int sourceX, string label)
        {
            var commands = Build(builder, new[] { stair, below, diagonal });
            var owned = commands.Where(c => (int)Get(c, "CellX") == 2 && (int)Get(c, "CellY") == 3 && (ushort)Get(c, "SemanticBits") == 1).ToArray();
            Require(owned.Length == (sourceX < 0 ? 1 : 2), label + ": literal join presence and upper-cell semantic ownership");
            if (sourceX < 0) return;
            var body = owned[0]; var join = owned[1];
            Require(Rect(body) == new Rectangle(144, 36, 16, 16) && Rect(join) == new Rectangle(sourceX, 36, 16, 16) &&
                Pos(join) == new Vector2(32, 64) && Get(join, "Texture").ToString() == "Tile" &&
                (int)Get(join, "Type") == 19 && (int)Get(join, "Paint") == 11 &&
                (Color)Get(join, "Color") == (Color)Get(body, "Color") && ((Color)Get(join, "Color")).A > 0 &&
                (SpriteEffects)Get(join, "Effects") == (SpriteEffects)Get(body, "Effects"),
                label + ": native source X/Y and position retain upper platform texture, paint, ink and effects");
        }

        private static void Furniture(Type builder)
        {
            // Original WorldGen closed-door raw samples: random frame X differs
            // per row, including a style page crossing. No normalized frame/18.
            var door = new CaptureObject { Id = 1, X = 2, Y = 2, Width = 1, Height = 3, Type = 10, Style = 37, Reliable = true };
            var members = new[] { Tile(10, 2, 2, 54, 54, 1), Tile(10, 2, 3, 72, 72, 1), Tile(10, 2, 4, 90, 90, 1) };
            var drawn = Build(builder, members, new[] { door });
            Require(drawn.Count == 3 && Rect(drawn[0]).X == 54 && Rect(drawn[1]).X == 72 && Rect(drawn[2]).X == 90 &&
                Rect(drawn[0]).Y == 54 && Rect(drawn[2]).Y == 90, "closed-door independently randomized raw rows survive intact");
            foreach (int facing in new[] { 72, 108 })
            {
                var open = new CaptureObject { Id = 2, X = 2, Y = 2, Width = 2, Height = 3, Type = 11, Style = 37, Alternate = facing == 72 ? 0 : 1, Reliable = true };
                var two = new[] { Tile(11, 2, 2, facing, 54, 2), Tile(11, 3, 2, facing + 18, 54, 2) };
                var values = Build(builder, two, new[] { open });
                Require(values.Count == 2 && Rect(values[0]).X == facing && Rect(values[1]).X == facing + 18 && Pos(values[1]).X - Pos(values[0]).X == 16,
                    "open-door raw left/right direction page " + facing);
            }
            var chair = new CaptureObject { Id = 3, X = 2, Y = 2, Width = 1, Height = 2, Type = 15, Style = 1, Reliable = true };
            drawn = Build(builder, new[] { Tile(15, 2, 2, 18, 40, 3), Tile(15, 2, 3, 18, 58, 3) }, new[] { chair });
            Require(Rect(drawn[0]) == new Rectangle(18, 40, 16, 16) && Rect(drawn[1]) == new Rectangle(18, 58, 16, 18),
                "positive PaddingFix controls style stride while member last-row height remains 18");
            var bed = new CaptureObject { Id = 4, X = 1, Y = 2, Width = 4, Height = 2, Type = 79, Style = 3, Reliable = true };
            drawn = Build(builder, new[] { Tile(79, 3, 3, 108, 126, 4) }, new[] { bed });
            Require(drawn.Count == 1 && Rect(drawn[0]) == new Rectangle(108, 126, 16, 18) && Pos(drawn[0]).X == 48,
                "negative PaddingFix/raw reversed bed member survives when origin/member peers are off-window");
            bed.Style = 56; bed.PlacementStyle = 112;
            drawn = Build(builder, new[] { Tile(79, 3, 3, 36, 2034, 4) }, new[] { bed });
            Require(drawn.Count == 1 && Rect(drawn[0]) == new Rectangle(180, 18, 16, 18),
                "original furniture page raw Y2034 maps to X+144/Y18 through visual style bank and DrawFrameOffsets without rebuilding raw member frames");
        }
        private static void NaturalAndLayers(Type builder)
        {
            var summary = new CaptureSummary { Left = 101, Top = 200, Width = 20, Height = 20 };
            var grass = Tile(3, 1, 2, 54, 0); var herb = Tile(82, 2, 2, 18, 0); var tall = Tile(73, 3, 2, 0, 0);
            var drawn = Build(builder, new[] { grass, herb, tall }, null, summary);
            Require(Rect(drawn[0]).Height == 20 && Pos(drawn[0]).Y == 32 && (SpriteEffects)Get(drawn[0], "Effects") == SpriteEffects.FlipHorizontally,
                "ObjectId zero grass extends to 20px and flips by source X=102");
            Require(Rect(drawn[1]).Height == 20 && Pos(drawn[1]).Y == 30 && (SpriteEffects)Get(drawn[1], "Effects") == SpriteEffects.None,
                "20px herb is raised two pixels and source X=103 stays unflipped");
            Require(Rect(drawn[2]).Height == 32 && Pos(drawn[2]).Y == 20, "32px long grass is raised twelve pixels");
            var layer = Tile(1, 2, 2, 0, 0); layer.Layers = (CaptureLayer)31; layer.WallType = 7; layer.TilePaint = 11; layer.WallPaint = 4;
            layer.Wires = 15; layer.Actuator = true; layer.LiquidAmount = 128; layer.LiquidKind = 3; layer.Inactive = true;
            layer.InvisibleTile = layer.InvisibleWall = layer.FullbrightTile = layer.FullbrightWall = true;
            drawn = Build(builder, new[] { layer });
            Require((int)Get(drawn[0], "Type") == 7 && (int)Get(drawn[0], "Paint") == 4 && Rect(drawn[0]) == new Rectangle(324, 108, 32, 32) && Pos(drawn[0]) == new Vector2(24, 24),
                "isolated wall uses native 32px frame/8px overhang and preserves paint");
            Require(Get(drawn[1], "Texture").ToString() == "Pixel" && Rect(drawn[1]).Height == 9,
                "liquid-only volume derives static height from saved amount");
            var tile = drawn.Single(c => Get(c, "Texture").ToString() == "Tile");
            Require((int)Get(tile, "Paint") == 11 && ((Color)Get(tile, "Color")).A > 0 && drawn.Count >= 15,
                "inactive/invisible/fullbright remain readable; all four wire colors and actuator are present independent of wiring tool");
            var neighbor = layer; neighbor.X = 3; neighbor.Layers = CaptureLayer.Wall;
            drawn = Build(builder, new[] { layer, neighbor });
            Require(Rect(drawn[0]) == new Rectangle(324, 0, 32, 32), "east-only wall neighbor selects native mask4 static frame");
            var pylon = new CaptureObject { Id = 11, X = 0, Y = 2, Width = 3, Height = 4, Type = 597, Style = 4, Reliable = true };
            drawn = Build(builder, new[] { Tile(597, 2, 3, 252, 18, 11), Tile(597, 2, 4, 252, 36, 11) }, new[] { pylon });
            var body = drawn.Where(c => Get(c, "Texture").ToString() == "Extra").ToArray();
            Require(drawn.Count == 3 && body.Length == 1 && (int)Get(body[0], "Type") == 181 && Rect(body[0]).X == 7 &&
                (int)Get(body[0], "Columns") == 14 && (int)Get(body[0], "Rows") == 8 && Pos(body[0]) == new Vector2(24, 56),
                "pylon has one native Extra181 full crystal body beyond two raw base fragments with off-window origin; no entity created");
        }
        private static void WindowAndCache(Type builder, Type renderer)
        {
            var summary = new CaptureSummary { Width = 20, Height = 20 };
            var c = new CaptureCell { X = 4, Y = 4, Layers = CaptureLayer.Wall, WallType = 7 };
            var values = Build(builder, new[] { c }, null, summary, new BlueprintProjectionBounds(4, 3, 5, 5));
            Require(values.Count == 0, "unknown outer halo ring is not permanently interpreted as air");
            var neighbor = c; neighbor.X = 3;
            values = Build(builder, new[] { c, neighbor }, null, summary, new BlueprintProjectionBounds(3, 3, 6, 5));
            Require(values.Count == 1 && Rect(values[0]) == new Rectangle(432, 0, 32, 32), "completed expanded halo rebuilds west adjacency from newly arrived source record");
            var identity = new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "projection-mechanism", 1, 1);
            var window = new BlueprintProjectionWindow(identity, 1, summary, new BlueprintProjectionBounds(0, 0, 20, 20), new[] { c }, Array.Empty<CaptureObject>());
            var instance = Activator.CreateInstance(renderer, F, null, Array.Empty<object>(), null);
            try
            {
                Call(instance, "SetWindow", window); long builds = (long)Get(instance, "BuildCount");
                for (int i = 0; i < 100; i++) Call(instance, "SetWindow", window);
                Require((long)Get(instance, "BuildCount") == builds && (int)Get(instance, "CommandCount") == 1,
                    "same immutable local window is interpreted once regardless of origin/frame updates");
                Call(instance, "Clear"); Require((int)Get(instance, "CommandCount") == 0, "remove/session retirement releases derived commands and borrowed references");
                var dense = Enumerable.Range(0,64*16).Select(n => new CaptureCell { X=n%64,Y=n/64,Layers=CaptureLayer.Tile,TileType=1 }).ToArray();
                var denseSummary = new CaptureSummary { Width=64,Height=16 };
                Call(instance,"SetWindow",new BlueprintProjectionWindow(identity,3,denseSummary,new BlueprintProjectionBounds(-1,-1,34,18),dense.Where(v=>v.X<=32).ToArray(),Array.Empty<CaptureObject>()));
                long segments=(long)Get(instance,"SegmentBuildCount");
                Call(instance,"SetWindow",new BlueprintProjectionWindow(identity,4,denseSummary,new BlueprintProjectionBounds(15,-1,34,18),dense.Where(v=>v.X>=15 && v.X<=48).ToArray(),Array.Empty<CaptureObject>()));
                Require((long)Get(instance,"SegmentBuildCount")==segments+1 && (int)Get(instance,"CommandCount")==512,"shifted source window reuses unchanged interior segment and builds only entering 16-column segment");
                Call(instance,"Clear");
                var unsupported = c; unsupported.Layers = CaptureLayer.Tile; unsupported.WallType = 0; unsupported.TileType = ushort.MaxValue;
                Call(instance, "SetWindow", new BlueprintProjectionWindow(identity, 2, summary, new BlueprintProjectionBounds(0, 0, 20, 20), new[] { unsupported }, Array.Empty<CaptureObject>()));
                object[] error = { null };
                Require((bool)renderer.GetMethod("TryTakeError", F).Invoke(instance, error) && !String.IsNullOrEmpty((string)error[0]) &&
                    (int)Get(instance, "CommandCount") == 0 && !(bool)renderer.GetMethod("TryTakeError", F).Invoke(instance, new object[] { null }),
                    "unsupported native type retires only projection commands and emits one explicit local failure, never a silently missing layer");
            }
            finally { ((IDisposable)instance).Dispose(); }
        }
        private static void WorldClip(Type builder)
        {
            var clip = builder.GetMethod("Clip", F);
            object[] normal = { new Vector2(-4, -3), new Rectangle(18, 36, 16, 16), Vector2.One, SpriteEffects.None, false, 100f, 100f };
            Require((bool)clip.Invoke(null, normal) && (Vector2)normal[0] == Vector2.Zero && (Rectangle)normal[1] == new Rectangle(22, 39, 12, 13) && (Vector2)normal[2] == Vector2.One,
                "world intersection clips source/destination 1:1 without shifting blueprint origin");
            object[] flipped = { new Vector2(-4, -3), new Rectangle(18, 36, 16, 16), Vector2.One, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, false, 100f, 100f };
            Require((bool)clip.Invoke(null, flipped) && (Rectangle)flipped[1] == new Rectangle(18, 36, 12, 13), "flipped clipping removes opposite source edges before gravity conversion");
            object[] pixel = { new Vector2(-2, -3), new Rectangle(0, 0, 1, 1), new Vector2(8, 6), SpriteEffects.None, true, 100f, 100f };
            Require((bool)clip.Invoke(null, pixel) && (Vector2)pixel[0] == Vector2.Zero && (Vector2)pixel[2] == new Vector2(6, 3) && (Rectangle)pixel[1] == new Rectangle(0, 0, 1, 1),
                "wire/liquid/actuator pixel geometry clips extent while keeping borrowed pixel source");
            object[] outside = { new Vector2(-20, 0), new Rectangle(18, 36, 16, 16), Vector2.One, SpriteEffects.None, false, 100f, 100f };
            Require(!(bool)clip.Invoke(null, outside), "completely outside world preview produces no draw command");
        }
        internal static BlueprintProjectionWindow MixedWindow()
        {
            var cells = new List<CaptureCell>();
            for (int x = 0; x < 8; x++) { var c = Tile(1, x, 5, 18, 18); c.HalfBrick = x == 1; c.Slope = (byte)(x >= 2 && x <= 5 ? x - 1 : 0); c.TilePaint = (byte)(x == 0 ? 11 : 0); cells.Add(c); }
            cells.Add(Tile(3, 1, 4, 54, 0)); cells.Add(Tile(82, 2, 4, 18, 0)); cells.Add(Tile(73, 3, 4, 0, 0));
            var wall = new CaptureCell { X = 5, Y = 2, Layers = CaptureLayer.Wall | CaptureLayer.Liquid | CaptureLayer.Wire | CaptureLayer.Actuator,
                WallType = 7, WallPaint = 4, LiquidKind = 0, LiquidAmount = 180, Wires = 15, Actuator = true }; cells.Add(wall);
            var obj = new CaptureObject { Id = 1, X = 8, Y = 2, Width = 3, Height = 4, Type = 597, Style = 0, Reliable = true };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) cells.Add(Tile(597, 8 + x, 2 + y, x * 18, y * 18, 1));
            var door = new CaptureObject { Id = 2, X = 6, Y = 1, Width = 1, Height = 3, Type = 10, Style = 37, Reliable = true };
            cells.Add(Tile(10, 6, 1, 54, 54, 2)); cells.Add(Tile(10, 6, 2, 72, 72, 2)); cells.Add(Tile(10, 6, 3, 90, 90, 2));
            var bed = new CaptureObject { Id = 3, X = 1, Y = 7, Width = 4, Height = 2, Type = 79, Style = 3, Reliable = true };
            for (int y = 0; y < 2; y++) for (int x = 0; x < 4; x++) cells.Add(Tile(79, 1 + x, 7 + y, 72 + x * 18, 108 + y * 18, 3));
            cells.Add(new CaptureCell { X = 0, Y = 0, Layers = CaptureLayer.Wire, Wires = 1 });
            cells.Add(Tile(1, 11, 9, 18, 18));
            return new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "mixed-projection-raw-samples", 1, 1), 1,
                new CaptureSummary { Left = 100, Top = 200, Width = 12, Height = 10 }, new BlueprintProjectionBounds(0, 0, 12, 10), cells.ToArray(), new[] { obj, door, bed });
        }
        private static List<object> Build(Type builder, CaptureCell[] cells, CaptureObject[] objects = null, CaptureSummary summary = null, BlueprintProjectionBounds? bounds = null)
        {
            summary = summary ?? new CaptureSummary { Width = 20, Height = 20 }; var b = bounds ?? new BlueprintProjectionBounds(0, 0, summary.Width, summary.Height);
            return ((IEnumerable)builder.GetMethod("Build", F).Invoke(null, new object[] { summary, cells, objects ?? Array.Empty<CaptureObject>(), b.Left, b.Top, b.Right, b.Bottom })).Cast<object>().ToList();
        }
        private static CaptureCell Tile(ushort type, int x, int y, int fx, int fy, int id = 0)
        { return new CaptureCell { X = x, Y = y, TileType = type, FrameX = (short)fx, FrameY = (short)fy, Layers = CaptureLayer.Tile, ObjectId = id }; }
        private static Rectangle Rect(object command) { return (Rectangle)Get(command, "Source"); }
        private static Vector2 Pos(object command) { return (Vector2)Get(command, "Position"); }
    }
}
