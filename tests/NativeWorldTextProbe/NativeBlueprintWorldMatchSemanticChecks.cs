using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintWorldMatchSemanticChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var previous = Main.tile; int width = Main.maxTilesX, height = Main.maxTilesY, mode = Main.netMode;
            var sections = Main.sectionManager;
            try
            {
                Main.netMode = 0; Main.maxTilesX = Main.maxTilesY = 120; Main.tile = new Tile[120, 120];
                for (int x = 0; x < 120; x++) for (int y = 0; y < 120; y++) Main.tile[x, y] = new Tile();
                // Original metadata locks after startup. Share the native
                // fixture's ready probe: reuse prior groups' complete table,
                // or perform normal startup once for a standalone invocation.
                NativeBlueprintWorldMatchChecks.InitializeMetadata();
                var assembly = Get(context, "Blueprint").GetType().Assembly;
                Type captureType = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
                Type matchType = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
                object source = Source(matchType);
                Fields(source);

                // Fixed literals from .8 WorldGen.Place4x2/Place1x2 and the
                // capture regression witnesses; no resolver math builds them.
                CheckPattern(captureType, source, Pattern(79, 4, new[] { 108, 126 }, 0), "bed negative PaddingFix");
                CheckPattern(captureType, source, Pattern(15, 1, new[] { 40, 58 }, 18), "right-facing chair positive PaddingFix");
                CheckPattern(captureType, source, Pattern(497, 1, new[] { 40, 58 }, 18), "right-facing toilet");
                CheckPattern(captureType, source, Pattern(185, 1, new[] { 0 }, 90), "small-pile selector 1x1");
                CheckPattern(captureType, source, Pattern(185, 2, new[] { 18 }, 108), "small-pile selector 2x1");
                CheckPattern(captureType, source, Pattern(233, 3, new[] { 0, 18 }, 108), "detritus selector 3x2");
                CheckPattern(captureType, source, Pattern(233, 2, new[] { 36, 54 }, 108), "detritus selector 2x2");
                foreach (int region in new[] { 0, 36, 72, 90 })
                    CheckPattern(captureType, source, Pattern(165, 1, region < 72 ? new[] { region, region + 18 } : new[] { region }, 54), "stalactite attachment " + region);
                DirectionAndOrigin(captureType, source);
                Door(captureType, source);
                DoorOccupancy(captureType, source);
                OperatingStates(captureType, source);
                RandomPlacement(captureType, source);

                Main.tile[20, 20] = new Tile { type = 5, frameX = 22, frameY = 198 }; Main.tile[20, 20].active(true);
                var natural = Read(source, 20, 20);
                Require(natural.Available && natural.Object == null && !natural.ObjectUnknown && BlueprintWorldMatchComparer.CompareCell(natural.Cell, natural).MatchBits == 1,
                    "real tree fragment with ObjectId=0 remains a reliable selected-cell target");
                Main.tile[20, 20].frameY = 242;
                Require(BlueprintWorldMatchComparer.CompareCell(natural.Cell, Read(source, 20, 20)).MatchBits == 1, "original random tree decoration within one top/branch form is equivalent");
                Main.tile[20, 20].frameX = 44;
                Require(BlueprintWorldMatchComparer.CompareCell(natural.Cell, Read(source, 20, 20)).ConflictBits == 1, "different natural top/branch structural frame cannot be discarded with random decoration");
                Main.netMode = 1; Main.sectionManager = null;
                Require(!Read(source, 20, 20).Available, "allocated tile in unreceived client section is unknown");
                Main.netMode = 0; Main.sectionManager = sections;
                Main.tile[20, 20] = null; Require(!Read(source, 20, 20).Available, "null original Tile remains unknown");
                Main.tile = new Tile[120, 120]; Require(!Read(source, 20, 20).Available, "array/session identity replacement revokes an old reader");
                Require(!Read(Source(matchType, () => false), 20, 20).Available, "session predicate revokes the entire sample");
                Console.WriteLine("PASS Blueprint world-match semantics: independent real Tile layers, native directions and +/-PaddingFix, all three selector mechanisms, complete object/member origin proof, door random rows/occupancy/raw residuals and unequal open-member appearance, original toggles and declared random placement, natural snapshots, section/null/session Unknown. This is a finite original-ABI component witness, not complete game/FPS/multiplayer/owner acceptance.");
            }
            finally { Main.tile = previous; Main.maxTilesX = width; Main.maxTilesY = height; Main.netMode = mode; Main.sectionManager = sections; }
        }
        private static object Source(Type type, Func<bool> session = null)
        { return Activator.CreateInstance(type, F, null, new object[] { session ?? (Func<bool>)(() => true) }, null); }
        private static BlueprintWorldMatchSample Read(object source, int x, int y)
        {
            object[] args = { x, y, null }; source.GetType().GetMethod("TryRead", F).Invoke(source, args); return (BlueprintWorldMatchSample)args[2];
        }
        private sealed class Target
        { internal BlueprintWorldObject Object; internal CaptureCell[] Cells; }
        private static Target TargetFor(Type captureType, object source, CaptureCell[] pattern)
        {
            var capture = (IBlueprintCaptureSource)Source(captureType); CaptureCell first;
            Require(capture.Read(20, 20, out first), "native target sample is readable");
            BlueprintObjectShape shape = capture.Describe(first);
            Require(shape != null && shape.Object.Reliable, "native target metadata is reliable; type=" + first.TileType + " frame=" + first.FrameX + "," + first.FrameY +
                " important=" + Main.tileFrameImportant[first.TileType] + " reason=" + (shape == null ? "no rectangular object" : shape.Object.Reason));
            CaptureObject metadata = shape.Object; metadata.Id = 19; metadata.X -= 20; metadata.Y -= 20;
            var members = new CaptureCell[pattern.Length];
            for (int i = 0; i < members.Length; i++)
            {
                Require(capture.Read(pattern[i].X, pattern[i].Y, out members[i]), "every target member is read through the original gate");
                members[i].X -= 20; members[i].Y -= 20; members[i].ObjectId = 19;
            }
            object[] args = { metadata, members[0], 20, 20, null };
            Require((bool)source.GetType().GetMethod("TryDescribeTarget", F).Invoke(source, args), "canonical target verifies captured raw fields without reading world");
            return new Target { Object = (BlueprintWorldObject)args[4], Cells = members };
        }
        private static BlueprintWorldMatchSample[] Members(object source, BlueprintWorldObject obj)
        {
            var values = new BlueprintWorldMatchSample[obj.MemberCount];
            for (int i = 0; i < values.Length; i++) values[i] = Read(source, obj.WorldX + i % obj.Width, obj.WorldY + i / obj.Width);
            return values;
        }
        private static BlueprintWorldMatchState State(object source, Target target)
        {
            var hinge = Read(source, target.Object.AnchorX, target.Object.AnchorY);
            return BlueprintWorldMatchComparer.CompareObject(target.Object, target.Cells, Members(source, hinge.Object ?? target.Object), true);
        }
        private static BlueprintWorldObjectFrameWitness FrameWitness(object source, Target target, BlueprintWorldMatchSample[] world, bool current = true)
        {
            object[] args = { target.Object, target.Cells, world, current, null };
            bool created = (bool)source.GetType().GetMethod("TryCreateFrameWitness", F).Invoke(source, args);
            Require(created == (args[4] != null), "frame witness factory has one explicit success result");
            return (BlueprintWorldObjectFrameWitness)args[4];
        }
        private static BlueprintWorldMatchSample[] DoorUnion(object source, BlueprintWorldObject door)
        {
            var values = new BlueprintWorldMatchSample[9];
            for (int i = 0; i < values.Length; i++) values[i] = Read(source, door.AnchorX - 1 + i % 3, door.AnchorY + i / 3);
            return values;
        }
        private static BlueprintWorldDoorWitness DoorWitness(object source, Target target, BlueprintWorldMatchSample[] union, bool current = true)
        {
            var hinge = Read(source, target.Object.AnchorX, target.Object.AnchorY);
            object[] args = { target.Object, target.Cells, Members(source, hinge.Object ?? target.Object), union, current, null };
            bool created = (bool)source.GetType().GetMethod("TryCreateDoorWitness", F).Invoke(source, args);
            Require(created == (args[5] != null), "door witness factory has one explicit success result");
            return (BlueprintWorldDoorWitness)args[5];
        }
        private static BlueprintWorldTileWriteState[] DoorStates(BlueprintWorldMatchSample[] union)
        {
            var states = new BlueprintWorldTileWriteState[union.Length];
            for (int i = 0; i < states.Length; i++)
            {
                Require(union[i].Available && union[i].WriteState.HasValue && union[i].WriteState.Value.MatchesCell(union[i].Cell),
                    "same gated native read supplies exact raw seed for each occupied or inactive door-union slot");
                states[i] = union[i].WriteState.Value;
            }
            return states;
        }
        private static void FrameChange(BlueprintWorldObjectFrameWitness witness, BlueprintWorldMatchSample[] before, BlueprintWorldMatchSample[] after)
        {
            Require(witness != null && before.Length == witness.MemberCount && after.Length == before.Length, "real complete native members supply a fixed-layout certificate");
            for (int i = 0; i < before.Length; i++)
            {
                CaptureCell a = before[i].Cell, b = after[i].Cell;
                bool horizontal = a.FrameX != b.FrameX;
                Require(horizontal ? a.FrameY == b.FrameY : a.FrameY != b.FrameY, "locked operating mechanism changes the certified frame axis");
                int member; long phase;
                Require(witness.TryTransition(a.X, a.Y, horizontal, horizontal ? a.FrameX : a.FrameY, horizontal ? b.FrameX : b.FrameY,
                    before[i].Object.FrameGroup, out member, out phase) && member == i && phase == after[i].Object.FrameGroup,
                    "actual original frame store maps to the same canonical member and its exact new raw phase");
            }
        }
        private static void Fields(object source)
        {
            Tile tile = Main.tile[10, 10]; tile.active(true); tile.type = 1; tile.inActive(true); tile.halfBrick(true); tile.color(9); tile.invisibleBlock(true); tile.fullbrightBlock(true);
            tile.wall = 7; tile.wallColor(4); tile.invisibleWall(true); tile.fullbrightWall(true); tile.wire(true); tile.wire2(true); tile.wire3(true); tile.wire4(true); tile.actuator(true); tile.liquid = 201; tile.liquidType(3);
            var actual = Read(source, 10, 10); CaptureCell target = actual.Cell;
            Require(BlueprintWorldMatchComparer.CompareCell(target, actual).MatchBits == 255, "one actual Tile read provides all eight independently counted layers");
            tile.active(false); tile.wallColor(5); tile.wire2(false); tile.liquid = 0;
            var result = BlueprintWorldMatchComparer.CompareCell(target, Read(source, 10, 10));
            Require(result.MatchBits == (8 | 16 | 64 | 128) && result.ConflictBits == 2 && result.MissingBits == (1 | 4 | 32), "actual inactive=false, wall paint, wire color and amount=0 are independent");
        }
        private static CaptureCell[] Pattern(ushort type, int width, int[] rows, int startX)
        {
            var result = new CaptureCell[width * rows.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new CaptureCell { X = 20 + i % width, Y = 20 + i / width, Layers = CaptureLayer.Tile, TileType = type, FrameX = (short)(startX + i % width * 18), FrameY = (short)rows[i / width] };
            return result;
        }
        private static void Clear()
        { for (int x = 18; x < 30; x++) for (int y = 18; y < 30; y++) Main.tile[x, y] = new Tile(); }
        private static void Set(CaptureCell[] values)
        {
            foreach (CaptureCell value in values)
            { var tile = Main.tile[value.X, value.Y]; tile.active(true); tile.type = value.TileType; tile.frameX = value.FrameX; tile.frameY = value.FrameY; }
        }
        private static void CheckPattern(Type captureType, object source, CaptureCell[] pattern, string label)
        {
            Clear(); Set(pattern); Target target = TargetFor(captureType, source, pattern);
            Require(State(source, target) == BlueprintWorldMatchState.Match, "same original encoding matches complete canonical building: " + label);
            Main.tile[pattern[pattern.Length - 1].X, pattern[pattern.Length - 1].Y].active(false);
            Require(State(source, target) == BlueprintWorldMatchState.Missing, "one missing original member grants no complete object: " + label);
            Set(pattern); Main.tile[20, 20].color(12);
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "real member paint blocks object completion: " + label);
        }
        private static void DirectionAndOrigin(Type captureType, object source)
        {
            Clear(); var pattern = Pattern(15, 1, new[] { 40, 58 }, 0); Set(pattern); var target = TargetFor(captureType, source, pattern);
            Set(Pattern(15, 1, new[] { 40, 58 }, 18));
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "same style right-facing alternate cannot satisfy left-facing chair");
            Set(pattern); Main.tile[20, 21].frameY = 40;
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "same tile type from two different real object origins cannot form a chair");
            Set(pattern); short original = target.Cells[1].FrameY; target.Cells[1].FrameY = 40;
            Require(State(source, target) == BlueprintWorldMatchState.Unknown, "mechanically readable template with another member origin cannot silently become a completed canonical target");
            target.Cells[1].FrameY = original;
            Clear(); pattern = Pattern(165, 1, new[] { 72 }, 54); Set(pattern); target = TargetFor(captureType, source, pattern);
            Set(Pattern(165, 1, new[] { 90 }, 54));
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "same-sized stalactite floor/ceiling selector regions remain distinct");
        }
        private static void Door(Type captureType, object source)
        {
            Clear(); var closed = Pattern(10, 1, new[] { 54, 72, 90 }, 54); closed[1].FrameX = 72; closed[2].FrameX = 90; Set(closed);
            var target = TargetFor(captureType, source, closed);
            Main.tile[20, 20].frameX = 90; Main.tile[20, 21].frameX = 54; Main.tile[20, 22].frameX = 72;
            Require(State(source, target) == BlueprintWorldMatchState.Match, "closed-door style 37 permits independent legal X decoration in all three rows");
            Require(FrameWitness(source, target, Members(source, Read(source, 20, 20).Object)) == null,
                "door type/presence/footprint transformations cannot borrow a fixed-layout frame certificate");
            foreach (int direction in new[] { 72, 108 })
            {
                Clear(); var open = Pattern(11, 2, new[] { 54, 72, 90 }, direction);
                // left opening's two-column rectangle starts at hinge-1.
                if (direction == 108) for (int i = 0; i < open.Length; i++) open[i].X--;
                Set(open);
                Require(State(source, target) == BlueprintWorldMatchState.Match, "legal open door retains closed canonical hinge and material style for direction " + direction);
                Main.tile[direction == 108 ? 19 : 21, 21].active(false);
                Require(State(source, target) == BlueprintWorldMatchState.Missing, "opened door needs all six actual members, including outside target closed footprint");
            }
            Clear(); Set(Pattern(10, 1, new[] { 108, 126, 144 }, 54));
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "next door material style is not another random decoration");
        }
        private static void DoorOccupancy(Type captureType, object source)
        {
            var random = Main.rand; bool wiring = Wiring.running;
            try
            {
                Main.rand = new Terraria.Utilities.UnifiedRandom(138); Wiring.running = false;
                Clear(); Set(Pattern(10, 1, new[] { 0, 18, 36 }, 54));
                // Original framing requires both solid hinge anchors. Inactive
                // wings intentionally contain nonzero residual fields: absence
                // in the mechanical CaptureCell is not an all-zero raw seed.
                foreach (int y in new[] { 19, 23 }) { Main.tile[20, y].active(true); Main.tile[20, y].type = 1; }
                for (int row = 0; row < 3; row++)
                {
                    Tile tile = Main.tile[20, 20 + row]; tile.color((byte)(row + 7)); tile.invisibleBlock(row == 1); tile.fullbrightBlock(row == 2);
                    foreach (int x in new[] { 19, 21 })
                    {
                        tile = Main.tile[x, 20 + row]; tile.type = 222; tile.frameX = 72; tile.frameY = 14;
                        tile.color(3); tile.fullbrightBlock(true);
                    }
                }
                var target = TargetFor(captureType, source, Pattern(10, 1, new[] { 0, 18, 36 }, 54));
                var union = DoorUnion(source, target.Object); var witness = DoorWitness(source, target, union);
                Require(witness != null && DoorWitness(source, target, union, false) == null,
                    "only complete current native Match can seed the nine-slot occupancy witness");
                Require(union[0].Cell.Layers == CaptureLayer.None && union[0].Cell.TileType == 0 &&
                    witness.InitialState(0).Type == 222 && witness.InitialState(0).FrameY == 14,
                    "inactive residual seed is copied without changing mechanical capture output");
                var missing = (BlueprintWorldMatchSample[])union.Clone(); missing[0] = new BlueprintWorldMatchSample(false, union[0].Cell);
                Require(DoorWitness(source, target, missing) == null, "unknown optional wing cannot seed an occupancy witness");
                missing[0] = new BlueprintWorldMatchSample(true, union[0].Cell);
                Require(DoorWitness(source, target, missing) == null, "readable inactive CaptureCell without real raw seed is insufficient");
                foreach (int direction in new[] { -1, 1 })
                {
                    Require(WorldGen.OpenDoor(20, 20, direction), "actual supported material-36 door opens in the requested direction");
                    Require(State(source, target) == BlueprintWorldMatchState.Match, "actual expanded door preserves every saved row appearance");
                    BlueprintWorldDoorWitness next;
                    Require(witness.TryComplete(DoorStates(DoorUnion(source, target.Object)), out next),
                        "actual original expanded footprint and copied per-row paint/coatings satisfy the finite occupancy states");
                    witness = next;
                    Require(WorldGen.CloseDoor(20, 20, true), "actual original wired-door close succeeds");
                    Require(State(source, target) == BlueprintWorldMatchState.Match &&
                        witness.TryComplete(DoorStates(DoorUnion(source, target.Object)), out next),
                        "original independently randomized closed rows and inactive wing residues remain complete");
                    witness = next;
                    int wing = direction < 0 ? 19 : 21;
                    Require(!Main.tile[wing, 21].active() && Main.tile[wing, 21].type == 11 &&
                        witness.InitialState(3 + wing - 19).Type == 11,
                        "closed native wing retains its actual open-door raw type rather than an invented empty snapshot");
                }
                Require(WorldGen.OpenDoor(20, 20, -1), "actual native door opens before unequal-member target capture");
                Main.tile[19, 21].color(27);
                var open = Pattern(11, 2, new[] { 0, 18, 36 }, 108); for (int i = 0; i < open.Length; i++) open[i].X--;
                var unequal = TargetFor(captureType, source, open);
                Require(State(source, unequal) == BlueprintWorldMatchState.Match,
                    "saved open door with unequal wing/hinge paint matches its own complete two-column appearance");
                Require(DoorWitness(source, unequal, DoorUnion(source, unequal.Object)) == null,
                    "unequal source columns cannot certify a later one-column closed pose");
                Require(WorldGen.CloseDoor(20, 20, true) && State(source, unequal) == BlueprintWorldMatchState.Conflict,
                    "legal closed door cannot satisfy two different saved target-member appearances");
            }
            finally { Main.rand = random; Wiring.running = wiring; }
        }
        private static void OperatingStates(Type captureType, object source)
        {
            Clear(); var lamp = Pattern(93, 1, new[] { 0, 18, 36 }, 0); Set(lamp); var target = TargetFor(captureType, source, lamp);
            var before = Members(source, Read(source, 20, 20).Object); var witness = FrameWitness(source, target, before);
            Require(witness != null && FrameWitness(source, target, before, false) == null, "only a current complete Match can seed a native operating certificate");
            Require(System.Threading.Tasks.Task.Run(() => FrameWitness(source, target, before)).GetAwaiter().GetResult() == null,
                "background consumers cannot compile original metadata or manufacture a write certificate");
            var unavailable = (BlueprintWorldMatchSample[])before.Clone(); unavailable[1] = new BlueprintWorldMatchSample(false, before[1].Cell);
            Require(FrameWitness(source, target, unavailable) == null, "one unknown member cannot seed an operating certificate");
            Wiring.ToggleLamp(20, 21, Main.tile[20, 21], null, false);
            Require(Main.tile[20, 20].frameX == 18 && State(source, target) == BlueprintWorldMatchState.Match, "actual original Wiring toggle preserves lamp building identity");
            FrameChange(witness, before, Members(source, Read(source, 20, 20).Object));
            Main.tile[20, 21].frameX = 0;
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "members from separate legal light states cannot be assembled into one complete object");
            Require(FrameWitness(source, target, Members(source, Read(source, 20, 20).Object)) == null, "mixed raw phases cannot seed an equivalence certificate");
            Main.tile[20, 21].frameX = 18;
            Main.tile[20, 21].fullbrightBlock(true);
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "legal light state does not erase a saved coating requirement");
            Require(FrameWitness(source, target, Members(source, Read(source, 20, 20).Object)) == null, "paint/coating mismatch cannot seed an equivalence certificate");
            Clear(); var chandelier = Pattern(34, 3, new[] { 0, 18, 36 }, 108); Set(chandelier); target = TargetFor(captureType, source, chandelier);
            before = Members(source, Read(source, 20, 20).Object); witness = FrameWitness(source, target, before);
            Wiring.ToggleChandelier(21, 21, Main.tile[21, 21], null, false);
            Require(State(source, target) == BlueprintWorldMatchState.Match, "actual original chandelier toggle retains material style above zero");
            FrameChange(witness, before, Members(source, Read(source, 20, 20).Object));
            Main.tile[20, 20].frameX += 108;
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "another chandelier material style is not a light state");
            Clear(); var monolith = Pattern(658, 2, new[] { 0, 18, 36 }, 0); Set(monolith); target = TargetFor(captureType, source, monolith);
            before = Members(source, Read(source, 20, 20).Object); witness = FrameWitness(source, target, before);
            WorldGen.SwitchMonolith(20, 20); Require(State(source, target) == BlueprintWorldMatchState.Match, "original three-state monolith first operating state");
            var after = Members(source, Read(source, 20, 20).Object); FrameChange(witness, before, after); before = after;
            WorldGen.SwitchMonolith(20, 20); Require(State(source, target) == BlueprintWorldMatchState.Match, "original three-state monolith second operating state");
            after = Members(source, Read(source, 20, 20).Object); FrameChange(witness, before, after); before = after;
            WorldGen.SwitchMonolith(20, 20); Require(State(source, target) == BlueprintWorldMatchState.Match, "original three-state monolith returns to initial state");
            FrameChange(witness, before, Members(source, Read(source, 20, 20).Object));
        }
        private static void RandomPlacement(Type captureType, object source)
        {
            Clear(); var pumpkin = Pattern(254, 2, new[] { 0, 18 }, 0); Set(pumpkin); var target = TargetFor(captureType, source, pumpkin);
            Set(Pattern(254, 2, new[] { 180, 198 }, 0));
            Require(State(source, target) == BlueprintWorldMatchState.Match, "declared pumpkin random row range is decoration");
            Set(Pattern(254, 2, new[] { 180, 198 }, 36));
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "pumpkin growth frame remains meaningful despite RandomVariant field");
            Clear(); var larva = Pattern(485, 2, new[] { 0, 18 }, 0); Set(larva); target = TargetFor(captureType, source, larva);
            Set(Pattern(485, 2, new[] { 0, 18 }, 108));
            Require(State(source, target) == BlueprintWorldMatchState.Match, "original range-overflow random placement does not invent another larva building style");
            Clear(); var sunflower = Pattern(27, 2, new[] { 0, 18, 36, 54 }, 0); Set(sunflower); target = TargetFor(captureType, source, sunflower);
            Set(Pattern(27, 2, new[] { 0, 18, 36, 54 }, 72));
            Require(State(source, target) == BlueprintWorldMatchState.Match, "same original overflow mechanism also folds sunflower random slots");
            Clear(); var sapling = Pattern(20, 1, new[] { 0, 18 }, 0); Set(sapling); target = TargetFor(captureType, source, sapling);
            Set(Pattern(20, 1, new[] { 0, 18 }, 18));
            Require(State(source, target) == BlueprintWorldMatchState.Match, "same sapling biome keeps its declared random appearance range");
            Set(Pattern(20, 1, new[] { 0, 18 }, 54));
            Require(State(source, target) == BlueprintWorldMatchState.Conflict, "next sapling alternate range is a different biome/form rather than decoration");
        }
    }
}
