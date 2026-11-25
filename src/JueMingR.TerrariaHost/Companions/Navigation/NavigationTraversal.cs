using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum NavigationMedium : byte { Ground, Air, Rope, Track, Liquid }

    internal readonly struct NavigationNode : IEquatable<NavigationNode>
    {
        internal readonly Point Cell;
        internal readonly NavigationMedium Medium;
        internal readonly int Gravity;
        internal readonly bool Mounted;
        internal readonly byte Energy;
        internal readonly NavigationTraversalDetail MountMode;
        internal readonly CompanionMountChoice MountChoice;
        internal readonly int RailFrame, OffsetX;

        internal NavigationNode(Point cell, NavigationMedium medium, int gravity, bool mounted, int energy,
            NavigationTraversalDetail mountMode = NavigationTraversalDetail.None, int railFrame = -1, int offsetX = 0,
            CompanionMountChoice mountChoice = default)
        {
            Cell = cell;
            Medium = medium;
            Gravity = gravity;
            Mounted = mounted;
            MountMode = mounted ? mountMode : NavigationTraversalDetail.None;
            MountChoice = mounted ? mountChoice : default;
            RailFrame = railFrame;
            OffsetX = offsetX;
            Energy = (byte)Math.Min(byte.MaxValue, Math.Max(0, energy));
        }

        internal bool Grounded => Medium == NavigationMedium.Ground;
        internal bool EqualsRegion(NavigationNode other) =>
            Cell == other.Cell && Medium == other.Medium && Gravity == other.Gravity &&
            Mounted == other.Mounted && MountMode == other.MountMode && RailFrame == other.RailFrame && OffsetX == other.OffsetX &&
            MountChoice.Equals(other.MountChoice);
        public bool Equals(NavigationNode other) => EqualsRegion(other) && Energy == other.Energy;
        public override bool Equals(object obj) => obj is NavigationNode other && Equals(other);
        public override int GetHashCode() => (((Cell.X * 397) ^ Cell.Y) * 397 ^ (int)Medium) * 397 ^
            (Gravity * 521 + Energy * 2 + (Mounted ? 1 : 0) + (int)MountMode * 997 + RailFrame * 3299 + OffsetX * 571) ^
            MountChoice.GetHashCode();
    }

    // Expands one offer at a time. Spatial branches use a local surface
    // neighborhood; only a chosen prefix goes to native-body refinement.
    internal sealed class NavigationTraversal
    {
        private static readonly Point[] AirOffsets =
        {
            new Point(-2, 0), new Point(2, 0), new Point(0, -2), new Point(0, 2),
            new Point(-2, -2), new Point(2, -2), new Point(-2, 2), new Point(2, 2),
            new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1)
        };
        private static readonly int[] DropOffsets = { 0, -1, 1, -2, 2, -3, 3, -5, 5 };
        private static readonly int[] JumpOffsets =
            { 0, -2, 2, -4, 4, -6, 6, -1, 1, -3, 3, -5, 5, -8, 8, -10, 10, -7, 7, -9, 9 };
        private static readonly int[] HookOffsets = { 0, -2, 2, -4, 4, -7, 7, -11, 11 };
        private const int BasicOptions = 34;
        private static readonly int JumpOptions = JumpOffsets.Length;
        private const int HookOptions = 9;
        private static readonly int SpecialOptions = BasicOptions + JumpOptions + HookOptions;
        private const int TrackOptions = 10;
        private static readonly int AdjacentWalk = SpecialOptions + TrackOptions + 4;
        internal static readonly int OptionCount = AdjacentWalk + 2;
        internal const int FirstBranch = int.MinValue;
        private const int MountRoles = 4;
        private readonly NavigationWorld world;
        private NavigationBudget budget;
        private NavigationCapabilities caps;
        private NavigationReadSet readSet;
        internal bool Pending { get; private set; }
        internal bool Unknown { get; private set; }
        private NavigationTraversalDetail CurrentMount => caps.Geometry.RidingTrack ? NavigationTraversalDetail.TrackRide :
            caps.MountCanFly ? NavigationTraversalDetail.MountFlight : caps.ActiveMount;

        internal static int CandidateCount(NavigationCapabilities capabilities) =>
            OptionCount + (capabilities.MountChoices?.Length ?? 0) * MountRoles;

        private static NavigationTraversalDetail MountRole(int option)
        {
            switch ((option - OptionCount) % MountRoles)
            {
                case 1: return NavigationTraversalDetail.MountFlight;
                case 2: return NavigationTraversalDetail.MountWater;
                case 3: return NavigationTraversalDetail.MountNarrow;
                default: return NavigationTraversalDetail.MountGround;
            }
        }

        private CompanionMountChoice CurrentMountChoice()
        {
            if (caps.Mounted && !caps.Geometry.RidingTrack && caps.MountChoices != null)
                foreach (var choice in caps.MountChoices)
                    if (choice.Type == caps.ActiveMountType) return choice;
            return default;
        }

        internal NavigationTraversal(NavigationWorld world) { this.world = world; }

        internal void Begin(NavigationCapabilities capabilities, NavigationBudget slice, NavigationReadSet reads = null)
        {
            caps = capabilities;
            budget = slice;
            readSet = reads;
            Pending = Unknown = false;
        }

        internal NavigationNode Anchor(Vector2 feet, bool supported, bool actual = false)
        {
            var point = CellAt(feet);
            int energy = supported ? Energy(caps.MaxFlightTicks) : Energy(caps.FlightTicks);
            var mount = caps.MountCanFly ? NavigationTraversalDetail.MountFlight : caps.ActiveMount;
            if (actual && caps.Geometry.RidingTrack)
            {
                Vector2 magnet = feet + caps.Geometry.CartMagnetFromFeet;
                var rail = CellAt(magnet);
                var track = Read(rail);
                int frame = track.Kind == NavigationCellKind.Track ? NavigationRail.FrameAt(track, rail, magnet) : -1;
                if (frame >= 0)
                    return new NavigationNode(rail, NavigationMedium.Track, 1, true, energy, NavigationTraversalDetail.TrackRide, frame);
            }
            var medium = MediumAt(point, caps.Gravity);
            if (supported)
            {
                if (TryGroundAnchor(point, feet.Y, out Point floor))
                    return SupportedAnchor(feet, floor, mount, energy, actual);
                // Native support can use any positive horizontal overlap.
                // The shrink used for clearance would erase real contact
                // on a lip. Exact edge contact alone still spans no tile.
                float halfWidth = caps.Width * .5f;
                int left = (int)Math.Floor((feet.X - halfWidth) / 16f);
                int right = (int)Math.Ceiling((feet.X + halfWidth) / 16f) - 1;
                for (int distance = 1; point.X - distance >= left || point.X + distance <= right; distance++)
                {
                    if (point.X - distance >= left && TryGroundAnchor(new Point(point.X - distance, point.Y), feet.Y, out floor) ||
                        point.X + distance <= right && TryGroundAnchor(new Point(point.X + distance, point.Y), feet.Y, out floor))
                        return SupportedAnchor(feet, floor, mount, energy, actual);
                }
            }
            return new NavigationNode(point, medium == NavigationMedium.Track ? NavigationMedium.Air : medium,
                caps.Gravity, caps.Mounted, energy, mount, mountChoice: CurrentMountChoice());
        }

        private NavigationNode SupportedAnchor(Vector2 feet, Point floor, NavigationTraversalDetail mount, int energy, bool actual)
        {
            var choice = CurrentMountChoice();
            var anchor = new NavigationNode(floor, NavigationMedium.Ground, caps.Gravity, caps.Mounted, energy, mount,
                mountChoice: choice);
            float delta = feet.X - (floor.X * 16f + 8);
            // A legal body against the sidewall can still have positive
            // support in this column. Keep that support and a clear inlet.
            if (!actual || Math.Abs(delta) < 1f || Math.Abs(delta) > 8f || !DepartureShoulderBlocked(anchor, Math.Sign(delta)))
                return anchor;
            int offset = delta < 0 ? (int)Math.Floor(delta) : (int)Math.Ceiling(delta);
            if (!Clear(new Vector2(floor.X * 16f + 8 + offset, feet.Y), caps.Gravity, mount, 2, default, false,
                choice: choice)) return anchor;
            return new NavigationNode(floor, NavigationMedium.Ground, caps.Gravity, caps.Mounted, energy, mount, offsetX: offset,
                mountChoice: choice);
        }

        private bool TryGroundAnchor(Point center, float footY, out Point floor)
        {
            for (int i = 0; i < 4; i++)
            {
                int offset = i == 0 ? 0 : i == 1 ? 1 : i == 2 ? -1 : 2;
                floor = new Point(center.X, center.Y + offset * caps.Gravity);
                var cell = Read(floor);
                if (Supports(cell, caps.Gravity) &&
                    Math.Abs(SupportFeet(floor, cell, caps.Gravity, caps.Width).Y - footY) <= 18f) return true;
            }
            floor = default;
            return false;
        }

        internal NavigationNode FallingGoal(Vector2 target, Vector2 velocity, NavigationNode start, NavigationNode observed)
        {
            float fall = velocity.Y * caps.Gravity;
            if (observed.Medium != NavigationMedium.Air || fall < 2f) return observed;
            // A falling owner may already be leaving the current platform
            // laterally. Keep observed air as an alternate query goal and
            // look only as far ahead as a short, known body sweep can reach.
            bool pending = Pending, unknown = Unknown;
            var landing = observed;
            int limit = Math.Min(32, Math.Max(8, (int)Math.Ceiling(fall * 6f)));
            for (int row = 1; row <= limit && !Pending; row++)
            {
                int y = observed.Cell.Y + row * caps.Gravity;
                float lead = MathHelper.Clamp(velocity.X * Math.Min(12f, row * 16f / Math.Max(4f, fall)), -64f, 64f);
                int x = CellAt(target + new Vector2(lead, 0)).X;
                for (int sample = 0; sample < (x == observed.Cell.X ? 1 : 2); sample++)
                {
                    var point = new Point(sample == 0 ? x : observed.Cell.X, y);
                    var cell = Read(point);
                    if (!Supports(cell, caps.Gravity) ||
                        (SupportFeet(point, cell, caps.Gravity, caps.Width).Y - target.Y) * caps.Gravity < 0f) continue;
                    var destination = Ground(point, start);
                    if (KnownClear(target, Feet(destination), start.Gravity, start.MountMode, 2, start.MountChoice))
                    {
                        landing = destination;
                        break;
                    }
                }
                if (landing.Grounded) break;
            }
            // Projection is optional. Its unresolved samples cannot stall
            // the actual observed goal or mark known terrain as unknown.
            Pending = pending;
            Unknown = unknown;
            return landing;
        }

        internal Vector2 Feet(NavigationNode node)
        {
            if (node.Grounded)
                return SupportFeet(node.Cell, Read(node.Cell), node.Gravity, BodyWidth(node.MountMode, node.MountChoice)) + new Vector2(node.OffsetX, 0);
            if (node.Medium == NavigationMedium.Track && NavigationRail.TryHeight(node.RailFrame, 4, out int height))
                return new Vector2(node.Cell.X * 16f + 8, node.Cell.Y * 16f + height) - caps.Geometry.CartMagnetFromFeet;
            return new Vector2(node.Cell.X * 16f + 8 + node.OffsetX, node.Cell.Y * 16f + 8);
        }

        internal bool IsGoal(NavigationNode node, NavigationNode goal)
        {
            if (node.Gravity != goal.Gravity || node.Medium != goal.Medium) return false;
            if (node.Cell == goal.Cell) return true;
            if (!node.Grounded || Math.Abs(node.Cell.X - goal.Cell.X) > 2) return false;
            return ConnectGround(node, goal.Cell.X, out var end) && end.Cell == goal.Cell;
        }

        internal bool SameGroundRegion(NavigationNode node, NavigationNode goal)
        {
            if (!node.Grounded || !goal.Grounded || node.Gravity != goal.Gravity ||
                Math.Abs(node.Cell.X - goal.Cell.X) > 18) return false;
            return ConnectGround(node, goal.Cell.X, out var end) && end.Cell == goal.Cell;
        }

        internal bool SameAirRegion(NavigationNode node, NavigationNode goal)
        {
            if (node.Medium != NavigationMedium.Air || goal.Medium != NavigationMedium.Air ||
                node.Gravity != goal.Gravity || node.MountMode != goal.MountMode ||
                Math.Abs(node.Cell.X - goal.Cell.X) > 4 || Math.Abs(node.Cell.Y - goal.Cell.Y) > 6) return false;
            var from = Feet(node);
            var to = Feet(goal);
            if (!KnownClear(from, to, node.Gravity, node.MountMode, choice: node.MountChoice)) return false;
            if (from.Y == to.Y) return true;
            // Whole-body clearance treats platforms as one-way. Crossing
            // their support plane changes the target region in either direction.
            float halfWidth = BodyWidth(node.MountMode, node.MountChoice) * .5f;
            var first = Vector2.Min(from, to);
            var last = Vector2.Max(from, to);
            for (int y = (int)Math.Ceiling(first.Y / 16f); y * 16f <= last.Y; y++)
            {
                float x = MathHelper.Lerp(from.X, to.X, (y * 16f - from.Y) / (to.Y - from.Y));
                int right = (int)Math.Ceiling((x + halfWidth) / 16f) - 1;
                for (int column = (int)Math.Floor((x - halfWidth) / 16f); column <= right; column++)
                {
                    var cell = Read(new Point(column, y));
                    if (!cell.Known || cell.Support) return false;
                }
            }
            return true;
        }

        internal bool GroundGoalLink(NavigationNode from, NavigationNode goal, out NavigationNode to,
            out NavigationTransition edge, out float cost)
        {
            to = default;
            edge = default;
            cost = 0f;
            int distance = Math.Abs(from.Cell.X - goal.Cell.X);
            if (distance < 3 || distance > 12 || from.Gravity != goal.Gravity || !from.Grounded || !goal.Grounded ||
                !ConnectGround(from, goal.Cell.X, out to) || to.Cell != goal.Cell) return false;
            edge = new NavigationTransition(NavigationAction.Walk, Feet(from), Feet(to), from.MountChoice, from.Cell, to.Cell,
                world.Revision);
            cost = Vector2.Distance(edge.From, edge.To) / 16f;
            return true;
        }

        internal bool LocalDoor(Vector2 feet, Vector2 target, NavigationNode start, out NavigationTransition edge)
        {
            edge = default;
            float horizontal = target.X - feet.X;
            if (!start.Grounded ||
                Math.Abs(horizontal) < 28 || budget.ExpansionsRemaining < 2 || !budget.TryExpand()) return false;
            int direction = Math.Sign(horizontal);
            if (!Offer(start, direction < 0 ? 2 : 3, out _, out var candidate, out _) ||
                Math.Abs(candidate.To.X - feet.X) > Math.Abs(horizontal) + 16 ||
                Math.Abs(candidate.EntryTile.X * 16 + 8 - feet.X) > 36) return false;
            return CanAttach(feet, candidate, out edge);
        }

        internal bool LocalLevel(Vector2 feet, NavigationNode start, NavigationNode goal, out NavigationTransition edge)
        {
            edge = default;
            if (!start.Grounded || !goal.Grounded) return false;
            int levels = (goal.Cell.Y - start.Cell.Y) * start.Gravity;
            int columns = Math.Abs(start.Cell.X - goal.Cell.X);
            var support = Read(start.Cell).Kind;
            bool acrossLowerBand = levels > 0 && columns <= 18 && support == NavigationCellKind.Platform;
            bool nearLedgeBank = levels > 0 && columns >= 3 && columns <= 8 && support == NavigationCellKind.Solid;
            if (levels >= -9 && levels <= -2 && columns > 3 && columns <= 20 &&
                TryUpperBandApproach(feet, start, goal, out edge)) return true;
            if (columns > 3 && !acrossLowerBand && !nearLedgeBank) return false;
            if (Math.Abs(levels) < 2 || (acrossLowerBand ? levels > 48 : Math.Abs(levels) > 9) ||
                budget.ExpansionsRemaining < 2 ||
                !budget.TryExpand()) return false;
            budget.RecordCandidate();
            if (nearLedgeBank)
            {
                int direction = Math.Sign(goal.Cell.X - start.Cell.X);
                // Try the nearer and farther known landing columns; the
                // drop provider requires the adjacent support to end.
                for (int reach = 3; reach <= 5; reach += 2)
                    if (Offer(start, Array.IndexOf(DropOffsets, reach * direction) + 4, out var landing, out var ledge, out _) &&
                        SameGroundRegion(landing, goal) && CanAttach(feet, ledge, out edge)) return true;
                return false;
            }
            int offset = Math.Max(-2, Math.Min(2, goal.Cell.X - start.Cell.X));
            int option = levels > 0 ? Array.IndexOf(DropOffsets, offset) + 4 : Array.IndexOf(JumpOffsets, offset) + BasicOptions;
            return Offer(start, option, out var destination, out var candidate, out _) &&
                (IsGoal(destination, goal) || acrossLowerBand && SameGroundRegion(destination, goal)) &&
                CanAttach(feet, candidate, out edge);
        }

        private bool TryUpperBandApproach(Vector2 feet, NavigationNode start, NavigationNode goal, out NavigationTransition edge)
        {
            edge = default;
            // This shortcut may miss an inlet. Leave a search slice for the
            // resumable ordinary graph rather than spending every small
            // slice retrying the same local launch sample.
            if (budget.ExpansionsRemaining < 8) return false;
            int direction = Math.Sign(goal.Cell.X - start.Cell.X);
            for (int stride = 0; stride <= 4 && budget.ExpansionsRemaining > 3 && budget.TryExpand(); stride += 4)
            {
                if (!ConnectGround(start, start.Cell.X + stride * direction, out var launch)) continue;
                for (int run = 0; run <= 4 && budget.ExpansionsRemaining > 3 && budget.TryExpand(); run += 2)
                {
                    budget.RecordCandidate();
                    int option = BasicOptions + Array.IndexOf(JumpOffsets, run * direction);
                    if (!Offer(launch, option, out var landing, out var jump, out _) || !SameGroundRegion(landing, goal))
                        continue;
                    if (stride == 0) return CanAttach(feet, jump, out edge);
                    // Publish the checked floor approach when the lane
                    // genuinely feeds a launch onto the live upper band.
                    // The actual jump is refined after arriving at its inlet.
                    edge = new NavigationTransition(NavigationAction.Walk, feet, Feet(launch), start.Cell, launch.Cell, world.Revision);
                    return true;
                }
                if (Pending) return false;
            }
            return false;
        }

        internal bool LocalPlatformExit(Vector2 feet, Vector2 target, NavigationNode from,
            Predicate<NavigationTransition> rejected, out NavigationTransition edge)
        {
            edge = default;
            if (!from.Grounded || from.Gravity < 0 || (target.Y - feet.Y) * from.Gravity < 16f ||
                Read(from.Cell).Kind != NavigationCellKind.Platform ||
                budget.ExpansionsRemaining < 2 || !budget.TryExpand()) return false;
            // Prefer one column toward a distant owner when the release can
            // feed that landing. Keep the aligned fallback in the same
            // bounded set and leave short isolated steps to the real route.
            float horizontal = target.X - feet.X;
            int preferred = Math.Abs(horizontal) < 24f ? 0 : horizontal < 0f ? 1 : 2;
            Vector2? inlet = LocalPlatformInlet(from, feet);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int offset = attempt == 0 ? preferred : attempt <= preferred ? attempt - 1 : attempt;
                // An opposite-side release is a detour, not an emergency
                // fallback when all of the forward exits are obstructed.
                if (preferred != 0 && DropOffsets[offset] * horizontal < 0f) continue;
                budget.RecordCandidate();
                if (PlatformDropOffer(from, feet, offset, inlet, out var drop) &&
                    (drop.To.Y - feet.Y >= 24f || Math.Abs(target.Y - drop.To.Y) <= 8f) &&
                    Math.Abs(target.Y - drop.To.Y) < Math.Abs(target.Y - feet.Y) &&
                    !rejected(drop) && ClearsLocalOutlet(from, drop.To, target.X) && CanAttach(feet, drop, out edge)) return true;
                if (Pending || budget.ExpansionsRemaining < 2 || !budget.TryExpand()) break;
            }
            return false;
        }

        private Vector2? LocalPlatformInlet(NavigationNode from, Vector2 feet)
        {
            Vector2 nominal = Feet(from);
            bool toe = CellAt(feet).X != from.Cell.X;
            if (!toe && PlatformInlet(from, nominal)) return null;
            // Native support can persist beyond the platform's column. If
            // an adjacent solid still holds a foot, there may instead be a
            // short supported approach into the platform-only inlet.
            if (PlatformInlet(from, feet)) return feet;
            if (Pending || Unknown) return null;
            float supportReach = BodyWidth(from.MountMode, from.MountChoice) * .5f + 8f - .125f;
            for (int distance = 2; distance <= 8; distance += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 candidate = feet + new Vector2(side * distance, 0f);
                    if (Math.Abs(candidate.X - (from.Cell.X * 16f + 8f)) >= supportReach) continue;
                    if (PlatformInlet(from, candidate) && KnownClear(feet, candidate, from.Gravity, from.MountMode, 2,
                        from.MountChoice)) return candidate;
                    if (Pending || Unknown) return null;
                }
            return null;
        }

        private bool PlatformDropOffer(NavigationNode from, Vector2 feet, int option, Vector2? inlet, out NavigationTransition edge)
        {
            if (!inlet.HasValue) return Offer(from, 4 + option, out _, out edge, out _);
            edge = default;
            if (!Drop(from, DropOffsets[option], out var to, out var kind, inlet) || Pending) return false;
            Vector2 landing = Feet(to);
            // Refinement starts from the measured body. Its own live release
            // check records the shifted inlet and the actual landing proof.
            edge = new NavigationTransition(kind, feet, landing, from.MountChoice, from.Cell, to.Cell,
                world.Revision, (landing.Y - feet.Y) * from.Gravity > Math.Max(80f, caps.JumpHeight));
            return true;
        }

        private bool ClearsLocalOutlet(NavigationNode from, Vector2 landing, float targetX)
        {
            // Don't take the quick release into the near side of a known
            // wall while the upper floor can still feed its actual exit.
            // Only the short onward volume is needed here, even at a ledge.
            float offset = MathHelper.Clamp(targetX - landing.X, -32f, 32f);
            return Math.Abs(offset) < 16f || KnownClear(landing, landing + new Vector2(offset, 0f), from.Gravity,
                from.MountMode, 2, from.MountChoice);
        }

        internal bool LocalTrack(Vector2 feet, Vector2 target, NavigationNode from, out NavigationTransition edge)
        {
            edge = default;
            float dx = target.X - feet.X;
            if (!from.Grounded || from.Mounted || from.Gravity < 0 || !caps.Has(NavigationModes.Track) ||
                caps.Geometry.CartHeight <= 0 || Math.Abs(dx) < 112f || Math.Abs(target.Y - feet.Y) > 32f ||
                budget.ExpansionsRemaining < 2) return false;
            int direction = Math.Sign(dx);
            int side = direction > 0 ? 1 : 0;
            for (int i = 0; i < TrackOptions; i += 2)
            {
                if (!Offer(from, SpecialOptions + i + side, out var start, out var board, out _)) continue;
                var next = start;
                for (int tiles = 0; tiles < 4; tiles++)
                {
                    if (!Track(next, side, out var following, out _, out _, out _, out _) &&
                        !Track(next, 2 + side, out following, out _, out _, out _, out _)) break;
                    next = following;
                }
                // Boarding has a fixed activation and settling cost. Use a
                // contact shortcut only when a useful run of reciprocal
                // nearby rail already exists toward the observed goal.
                if (next.Cell == start.Cell || (next.Cell.X - start.Cell.X) * direction < 4) continue;
                if (!budget.TryExpand()) return false;
                budget.RecordCandidate();
                return CanAttach(feet, board, out edge);
            }
            return false;
        }

        internal bool LocalFlightMount(Vector2 feet, NavigationNode from, Predicate<NavigationTransition> rejected,
            out NavigationTransition edge)
        {
            edge = default;
            if (from.Medium != NavigationMedium.Air || from.Mounted || !caps.Has(NavigationModes.FlightMount) ||
                !budget.TryExpand()) return false;
            budget.RecordCandidate();
            var selected = default(CompanionMountChoice);
            const NavigationTraversalDetail flight = NavigationTraversalDetail.MountFlight;
            if (caps.MountChoices != null)
            {
                float range = 0f;
                foreach (var choice in caps.MountChoices)
                {
                    if (!MountEligible(from, flight, choice)) continue;
                    var alternative = new NavigationTransition(NavigationAction.Mount, feet, feet, choice, from.Cell, from.Cell,
                        world.Revision, detail: flight);
                    float reach = choice.FlightTicks * Math.Min(7f, choice.RunSpeed);
                    if (reach <= range || rejected(alternative) || !Clear(feet, from.Gravity, flight, 1, default, false, choice: choice) ||
                        !KnownClear(feet, feet - new Vector2(0, 20f * from.Gravity), from.Gravity, flight, 1, choice)) continue;
                    range = reach;
                    selected = choice;
                }
                if (!selected.Valid) return false;
            }
            else if (caps.AvailableMountFlightTicks <= 0 || !Clear(feet, from.Gravity, flight, 1, default, false)) return false;
            edge = new NavigationTransition(NavigationAction.Mount, feet, feet, selected, from.Cell, from.Cell,
                world.Revision, detail: flight);
            return !rejected(edge);
        }

        internal bool IsApplicable(NavigationNode from, int option)
        {
            if (from.Medium == NavigationMedium.Track) return option >= SpecialOptions && option < SpecialOptions + 6;
            if (option >= OptionCount)
                return !from.Mounted && MountEligible(from, MountRole(option), caps.MountChoices[(option - OptionCount) / MountRoles]);
            if (option < 4) return from.Grounded;
            if (option < 13) return from.Grounded || from.Medium == NavigationMedium.Air && option < 9;
            if (option < 25) return caps.Has(NavigationModes.Wing | NavigationModes.Rocket | NavigationModes.Carpet |
                NavigationModes.Rope | NavigationModes.Track | NavigationModes.Swim | NavigationModes.FlightMount |
                NavigationModes.WaterMount);
            if (option == 25) return from.Mounted || caps.MountChoices == null &&
                caps.Has(NavigationModes.Mount | NavigationModes.GroundMount | NavigationModes.FlightMount | NavigationModes.WaterMount);
            if (option == 26) return from.Grounded && caps.Has(NavigationModes.Gravity);
            if (option == 27) return from.Grounded && caps.Has(NavigationModes.Hoik);
            if (option == 28) return caps.MountChoices == null && !from.Mounted && caps.Has(NavigationModes.FlightMount);
            if (option == 29) return caps.MountChoices == null && !from.Mounted &&
                from.Medium == NavigationMedium.Liquid && caps.Has(NavigationModes.WaterMount);
            if (option == 30) return caps.MountChoices == null && !from.Mounted && from.Grounded && caps.Has(NavigationModes.NarrowMount);
            if (option < 33) return from.Grounded && caps.Has(NavigationModes.Dash);
            if (option == 33) return from.Gravity > 0 && (from.Grounded || from.Energy > 0 && caps.Has(NavigationModes.Wing |
                NavigationModes.Rocket | NavigationModes.FlightMount));
            if (option < BasicOptions + JumpOptions)
                return (from.Grounded || from.Medium == NavigationMedium.Rope || from.Medium == NavigationMedium.Liquid) &&
                    Math.Abs(JumpOffsets[option - BasicOptions] * 16) <= caps.JumpRange +
                        Math.Abs(from.OffsetX) + JumpBankInset(from, JumpOffsets[option - BasicOptions]);
            if (option < SpecialOptions)
                return caps.Has(NavigationModes.Hook) && !from.Mounted &&
                    Math.Abs(HookOffsets[option - BasicOptions - JumpOptions] * 16) < caps.HookRange;
            if (option < SpecialOptions + TrackOptions)
                return caps.Has(NavigationModes.Track) && caps.Geometry.CartHeight > 0 && from.Gravity > 0 && !from.Mounted &&
                    (from.Grounded || from.Medium == NavigationMedium.Air);
            if (option < AdjacentWalk)
                return from.Grounded && !from.Mounted && caps.Geometry.CarpetAvailable && caps.Has(NavigationModes.Carpet);
            return from.Grounded;
        }

        internal bool CanAttach(Vector2 feet, NavigationTransition action, out NavigationTransition attached)
        {
            attached = action;
            if (Math.Abs(feet.X - action.From.X) > 40f || Math.Abs(feet.Y - action.From.Y) > 22f) return false;
            // Nearby pose is insufficient to skip a mount edge. The later
            // swept shape and reserve belong to the selected native body mode.
            if (action.Kind != NavigationAction.Mount && action.MountChoice.Valid &&
                (!caps.Mounted || caps.ActiveMountType != action.MountChoice.Type)) return false;
            if (action.Detail == NavigationTraversalDetail.TrackRide)
            {
                var railStart = Anchor(feet, caps.Supported, actual: true);
                return railStart.Medium == NavigationMedium.Track && railStart.Cell == action.EntryTile &&
                    railStart.RailFrame == action.RailEntryFrame;
            }
            if (action.Kind == NavigationAction.Door && Math.Abs(action.To.X - action.From.X) > 12f &&
                Math.Abs(feet.X - action.From.X) <= 24f && Math.Abs(feet.Y - action.From.Y) <= 18f &&
                Clear(feet, caps.Gravity, CurrentMount, 4, action.EntryTile, true) &&
                Clear((feet + action.From) * .5f, caps.Gravity, CurrentMount, 4, action.EntryTile, true))
            {
                // The native body stops with its edge exactly against the
                // closed leaf. A nominal center two pixels farther may be
                // inside that same leaf; this is already a valid door inlet.
                attached = new NavigationTransition(action.Kind, feet, action.To, action.MountChoice, action.EntryTile,
                    action.ExitTile, action.TerrainVersion, action.Irreversible, action.Detail);
                return true;
            }
            if (action.Kind == NavigationAction.Walk || action.Kind == NavigationAction.Dash)
            {
                var start = Anchor(feet, caps.Supported);
                if (!start.Grounded || !ConnectGround(start, CellAt(action.To).X, out var end) ||
                    Math.Abs(Feet(end).Y - action.To.Y) > 9f) return false;
                attached = new NavigationTransition(action.Kind, feet, action.To, action.MountChoice, action.EntryTile,
                    action.ExitTile, action.TerrainVersion, action.Irreversible, action.Detail, action.InteractionPosition);
                return true;
            }
            if (action.Kind == NavigationAction.Drop && caps.Supported && Math.Abs(feet.Y - action.From.Y) <= 8f &&
                (feet.X - action.From.X) * Math.Sign(action.To.X - action.From.X) > 0f &&
                Anchor(feet, true, actual: true).Cell == action.EntryTile &&
                KnownClear(feet, action.From, caps.Gravity, CurrentMount, 4))
            {
                // Native toe support can outlast the tile center at a lip.
                // Start refinement at the body still on that same support.
                attached = new NavigationTransition(action.Kind, feet, action.To, action.MountChoice, action.EntryTile,
                    action.ExitTile, action.TerrainVersion, action.Irreversible, action.Detail);
                return true;
            }
            // A nearby elevated jump inlet can still sit behind the body.
            // Walk back before launching so proximity cannot bypass runway.
            bool behindJumpInlet = action.Kind == NavigationAction.Jump &&
                (action.From.Y - action.To.Y) * caps.Gravity >= 32f &&
                (feet.X - action.From.X) * Math.Sign(action.To.X - action.From.X) > 4f;
            if (!behindJumpInlet && Vector2.DistanceSquared(feet, action.From) <= 16f * 16f &&
                KnownClear(feet, action.From, caps.Gravity, CurrentMount, 4)) return true;
            // A connector below the native arrival tolerance could report
            // success without moving. It cannot repair a rejected inlet.
            if (Math.Abs(feet.X - action.From.X) <= 4f) return false;
            var anchor = Anchor(feet, caps.Supported);
            if (!anchor.Grounded || !ConnectGround(anchor, CellAt(action.From).X, out var approach) ||
                Math.Abs(Feet(approach).Y - action.From.Y) > 6f) return false;
            // A valid ledge route can own its final supported approach.
            // The native action keeps its release gate while walking to
            // the proved inlet, rather than discovering the drop at the lip.
            if (action.Kind == NavigationAction.Drop && Math.Abs(feet.X - action.From.X) <= 32f) return true;
            attached = new NavigationTransition(NavigationAction.Walk, feet, action.From, anchor.Cell,
                approach.Cell, action.TerrainVersion);
            return true;
        }

        internal bool Offer(NavigationNode from, int option, out NavigationNode to, out NavigationTransition action, out float cost)
        {
            int branch = FirstBranch;
            return OfferNext(from, option, ref branch, out to, out action, out cost);
        }

        // The query owns the next branch. A paused row stays eligible until
        // its entire envelope is known, even when earlier rows were rejected.
        internal bool OfferNext(NavigationNode from, int option, ref int branch, out NavigationNode to,
            out NavigationTransition action, out float cost)
        {
            to = default;
            action = default;
            cost = 0;
            var kind = NavigationAction.Walk;
            var detail = NavigationTraversalDetail.None;
            var entry = from.Cell;
            Point exitOverride = default;
            Vector2? preciseStart = null;
            int railDirection = 0, railFrame = -1;
            if (option < 2)
            {
                if (!from.Grounded || !Walk(from, option == 0 ? -1 : 1, out to)) return false;
            }
            else if (option < 4)
            {
                kind = NavigationAction.Door;
                if (!from.Grounded || !Door(from, option == 2 ? -1 : 1, out to, out entry)) return false;
            }
            else if (option < 13)
            {
                int dx = DropOffsets[option - 4];
                if (from.Grounded && Read(from.Cell).Kind == NavigationCellKind.Trapdoor)
                {
                    kind = NavigationAction.Door;
                    if (dx != 0 || !TraverseTrapdoor(from, from.Cell, true, out to, out entry, out var inlet)) return false;
                    preciseStart = inlet;
                }
                else if (!Drop(from, dx, out to, out kind)) return false;
            }
            else if (option < 25)
            {
                if (!Air(from, AirOffsets[option - 13], out to, out kind, out entry)) return false;
            }
            else if (option == 25)
            {
                detail = NavigationTraversalDetail.MountGround;
                if (!Mount(from, detail, out to, out kind)) return false;
                if (kind == NavigationAction.Dismount) detail = NavigationTraversalDetail.None;
            }
            else if (option == 26)
            {
                kind = NavigationAction.Gravity;
                detail = NavigationTraversalDetail.GravitySwitch;
                if (!caps.Has(NavigationModes.Gravity) || !from.Grounded || !ReverseGravity(from, out to)) return false;
            }
            else if (option == 27)
            {
                if (!SpecialGround(from, out to, out kind, out entry)) return false;
            }
            else if (option < 31)
            {
                detail = option == 28 ? NavigationTraversalDetail.MountFlight :
                    option == 29 ? NavigationTraversalDetail.MountWater : NavigationTraversalDetail.MountNarrow;
                if (!Mount(from, detail, out to, out kind)) return false;
            }
            else if (option < 33)
            {
                kind = NavigationAction.Dash;
                if (!ConnectGround(from, from.Cell.X + (option == 31 ? -8 : 8), out to)) return false;
            }
            else if (option == 33)
            {
                kind = NavigationAction.Door;
                if (!OpenUpward(from, out to, out entry, out var inlet)) return false;
                preciseStart = inlet;
            }
            else if (option < BasicOptions + JumpOptions)
            {
                kind = NavigationAction.Jump;
                if (!Jump(from, JumpOffsets[option - BasicOptions], ref branch, out to, out bool running)) return false;
                if (running) detail = NavigationTraversalDetail.RunningJump;
            }
            else if (option < SpecialOptions)
            {
                kind = NavigationAction.Hook;
                if (!Hook(from, HookOffsets[option - BasicOptions - JumpOptions], out to, out exitOverride, out var inlet)) return false;
                preciseStart = inlet;
            }
            else if (option < SpecialOptions + TrackOptions)
            {
                int trackBranch = option - SpecialOptions;
                if (!Track(from, trackBranch, out to, out kind, out detail, out entry, out railDirection)) return false;
                railFrame = to.RailFrame;
            }
            else if (option < AdjacentWalk)
            {
                kind = NavigationAction.Flight;
                detail = NavigationTraversalDetail.CarpetGlide;
                if (!Carpet(from, option - SpecialOptions - TrackOptions, out to)) return false;
            }
            else if (option < OptionCount)
            {
                // Long surface links are shortcuts. Keep every intervening
                // contact reachable as a regular node for launch and drop.
                int direction = option == AdjacentWalk ? -1 : 1;
                if (!from.Grounded || !ConnectGround(from, from.Cell.X + direction, out to)) return false;
            }
            else
            {
                detail = MountRole(option);
                var choice = caps.MountChoices[(option - OptionCount) / MountRoles];
                if (!Mount(from, detail, out to, out kind, choice)) return false;
            }
            Vector2 start = preciseStart ?? Feet(from), finish = Feet(to);
            bool irreversible = (kind == NavigationAction.Drop || kind == NavigationAction.PlatformDrop ||
                kind == NavigationAction.Door && Read(entry).Kind == NavigationCellKind.Trapdoor) &&
                (finish.Y - start.Y) * from.Gravity > Math.Max(80f, caps.JumpHeight) ||
                kind == NavigationAction.Flight && !to.Grounded && from.Energy > to.Energy && to.Energy <= 1;
            action = new NavigationTransition(kind, start, finish, entry,
                exitOverride == default ? to.Cell : exitOverride, world.Revision, irreversible, detail, default,
                railDirection, railFrame, from.RailFrame, kind == NavigationAction.Mount ? to.MountChoice : from.MountChoice);
            if (kind == NavigationAction.Jump && !Pending) branch--;
            float distance = Math.Max(1f, Vector2.Distance(start, finish) / 16f);
            cost = detail == NavigationTraversalDetail.TrackRide ? distance : distance + ActionCost(kind);
            // Ordinary shortcuts avoid unnecessary command handoffs. Short
            // links still keep every real intermediate inlet in OPEN.
            if (option >= AdjacentWalk && option < OptionCount) cost += .125f;
            // A long jump's first bank contact is a better place to hand
            // off to ground control than the far interior of the same bank.
            if (detail == NavigationTraversalDetail.RunningJump && to.OffsetX == 0) cost += 3f;
            if (kind == NavigationAction.Jump)
            {
                // Steep launches have less time at the receiving height to
                // build lateral speed. Prefer the nearer part of the bank
                // plus ground travel over a single optimistic diagonal.
                float rise = (start.Y - finish.Y) * from.Gravity;
                cost += Math.Abs(finish.X - start.X) * Math.Max(0f, rise - 24f) / 1024f;
            }
            // A ledge's near toe can still overlap the old support during a
            // vertical projection. Prefer a landing with room to release the
            // whole body before turning back toward the goal.
            if (kind == NavigationAction.Drop && from.Grounded &&
                Math.Abs(finish.X - start.X) < BodyWidth(from.MountMode, from.MountChoice) + 2f) cost += 3f;
            return !from.Equals(to);
        }

        internal bool KnownClear(Vector2 from, Vector2 to, int gravity, NavigationTraversalDetail mount, int allowance = 0,
            CompanionMountChoice choice = default)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / 12f));
            for (int i = 0; i <= steps; i++)
                if (!Clear(Vector2.Lerp(from, to, i / (float)steps), gravity, mount, allowance, default, false,
                    choice: choice)) return false;
            return true;
        }

        private bool Walk(NavigationNode from, int dir, out NavigationNode to)
        {
            to = from;
            var start = Read(from.Cell);
            int length = 3;
            if (world.TrySpan(from.Cell, false, budget, out var span, out bool pending))
            {
                int boundary = dir < 0 ? span.Left : span.Right - 1;
                // A shared contour's endpoint is a portal. Adjacent chunk
                // endpoints stitch via the same physical step tests as
                // internal contour changes; they are never world walls.
                length = Math.Max(1, Math.Min(5, Math.Abs(from.Cell.X - boundary)));
            }
            else if (pending) Pending = true;
            bool slopeRun = start.Slope == 1 || start.Slope == 2;
            if (slopeRun) length = 4;
            else if (length > 1 && DepartureShoulderBlocked(from, dir)) length = 1;
            for (int i = 1; i <= length; i++)
            {
                if (!ConnectGround(to, from.Cell.X + i * dir, out var step)) break;
                to = step;
                var support = Read(to.Cell);
                if (support.Kind != start.Kind || support.Slope != start.Slope || !slopeRun && to.Cell.Y != from.Cell.Y)
                    break;
            }
            return !to.Equals(from);
        }

        private bool DepartureShoulderBlocked(NavigationNode from, int direction)
        {
            var feet = Feet(from);
            int shoulder = (int)Math.Floor((feet.X - direction * (BodyWidth(from.MountMode, from.MountChoice) * .5f - .125f)) / 16f);
            if (shoulder == from.Cell.X) return false;
            int height = from.MountChoice.Valid ? from.MountChoice.Height : from.Mounted ? caps.Geometry.MountHeight(from.MountMode) :
                caps.Geometry.BaseHeight > 0 ? caps.Geometry.BaseHeight : caps.Height;
            int rows = Math.Max(1, (int)Math.Ceiling(height / 16f));
            for (int offset = 1; offset <= rows; offset++)
            {
                var side = Read(new Point(shoulder, from.Cell.Y - offset * from.Gravity));
                // Keep the first free support as a jump or flight inlet
                // when the midpoint of the departing support clips a wall.
                if (side.Kind == NavigationCellKind.Solid) return true;
            }
            return false;
        }

        private bool ConnectGround(NavigationNode from, int x, out NavigationNode to)
        {
            to = from;
            int dir = Math.Sign(x - from.Cell.X);
            if (dir == 0) return true;
            while (to.Cell.X != x)
            {
                var start = Feet(to);
                bool found = false;
                for (int i = 0; i < 3; i++)
                {
                    // A filled ridge can have both its slope and a lower
                    // floor in this column. Follow the exposed contour
                    // before considering support buried under that slope.
                    int dy = (i - 1) * to.Gravity;
                    Point next = new Point(to.Cell.X + dir, to.Cell.Y + dy);
                    var support = Read(next);
                    if (!Supports(support, to.Gravity)) continue;
                    if (i == 0 && support.Kind == NavigationCellKind.Platform) continue;
                    Vector2 finish = SupportFeet(next, support, to.Gravity, BodyWidth(to.MountMode, to.MountChoice));
                    if (Math.Abs(finish.Y - start.Y) > 18f) continue;
                    int offsetX = 0;
                    int allowance = dy != 0 ? 8 : 2;
                    if (!Clear(finish, to.Gravity, to.MountMode, allowance, default, false, dir, to.MountChoice))
                    {
                        if (Pending || dy != 0 || support.Kind != NavigationCellKind.Solid) continue;
                        // The body can stand on this last support next to a
                        // wall even when its midpoint would intersect it.
                        // Prove a whole-body stop on the approaching side.
                        offsetX = -3 * dir;
                        finish.X += offsetX;
                        if (!Clear(finish, to.Gravity, to.MountMode, allowance, default, false, dir, to.MountChoice)) continue;
                    }
                    Vector2 highMid = new Vector2((start.X + finish.X) * .5f,
                        to.Gravity > 0 ? Math.Min(start.Y, finish.Y) : Math.Max(start.Y, finish.Y));
                    if (!Clear(highMid, to.Gravity, to.MountMode, 5, default, false, dir, to.MountChoice)) continue;
                    to = Ground(next, to, offsetX);
                    found = true;
                    break;
                }
                if (!found)
                {
                    // A one-way step is a possible inlet when ordinary
                    // forward support ends; it does not replace a lane below it.
                    Point next = new Point(to.Cell.X + dir, to.Cell.Y - to.Gravity);
                    var support = Read(next);
                    if (support.Kind == NavigationCellKind.Platform &&
                        Clear(SupportFeet(next, support, to.Gravity, BodyWidth(to.MountMode, to.MountChoice)), to.Gravity, to.MountMode, 4,
                            default, false, dir, to.MountChoice))
                    {
                        to = Ground(next, to);
                        found = true;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        private bool Door(NavigationNode from, int dir, out NavigationNode to, out Point door)
        {
            to = default;
            door = default;
            Vector2 begin = Feet(from);
            for (int dist = 1; dist <= 2; dist++)
            {
                int x = from.Cell.X + dir * dist;
                for (int i = 1; i <= 4; i++)
                {
                    var point = new Point(x, from.Cell.Y - i * from.Gravity);
                    if (Read(point).Kind != NavigationCellKind.Door) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var end = new Point(x + dir, from.Cell.Y + dy);
                        var floor = Read(end);
                        if (!Supports(floor, from.Gravity)) continue;
                        Vector2 feet = SupportFeet(end, floor, from.Gravity, BodyWidth(from.MountMode, from.MountChoice));
                        if (!Clear(feet, from.Gravity, from.MountMode, 8, point, true, choice: from.MountChoice) ||
                            !Clear((begin + feet) * .5f, from.Gravity, from.MountMode, 9, point, true, choice: from.MountChoice)) continue;
                        door = point;
                        to = Ground(end, from);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Drop(NavigationNode from, int dx, out NavigationNode to, out NavigationAction kind, Vector2? inlet = null)
        {
            to = default;
            kind = NavigationAction.Drop;
            if (!from.Grounded && (dx < -2 || dx > 2 || from.Medium != NavigationMedium.Air)) return false;
            var source = Read(from.Cell);
            bool platform = from.Grounded && source.Kind == NavigationCellKind.Platform;
            if (platform && Math.Abs(dx) > 2) return false;
            if (platform && !PlatformInlet(from, inlet ?? Feet(from))) return false;
            if (from.Grounded && !platform)
            {
                if (dx == 0) return false;
                if (ConnectGround(from, from.Cell.X + Math.Sign(dx), out _)) return false;
            }
            if (platform) kind = NavigationAction.PlatformDrop;
            int x = (inlet.HasValue ? CellAt(inlet.Value).X : from.Cell.X) + dx;
            // Choose the first usable support in the receiving column. If
            // its upper face is buried in a side wall, the body's descent
            // must stay outside that wall until there is full clearance.
            int maxDepth = dx == 0 ? 48 : 24;
            bool underWall = false;
            for (int i = 1; i <= maxDepth; i++)
            {
                var point = new Point(x, from.Cell.Y + i * from.Gravity);
                var below = Read(point);
                if (Pending || !below.Known) return false;
                if (!Supports(below, from.Gravity)) continue;
                var landing = SupportFeet(point, below, from.Gravity, BodyWidth(from.MountMode, from.MountChoice));
                int offsetX = dx == 0 ? from.OffsetX : 0;
                if (dx == 0 && inlet.HasValue) offsetX = (int)Math.Round(inlet.Value.X - landing.X);
                if (underWall) offsetX = -Math.Sign(dx) * (BodyWidth(from.MountMode, from.MountChoice) / 2 + 7);
                // A vertical release stays in the checked body lane. Moving
                // back to the nominal center would overlap the wall again.
                landing.X += offsetX;
                if (!Clear(landing, from.Gravity, from.MountMode, 3, default, false, choice: from.MountChoice))
                {
                    if (Pending || Unknown || !from.Grounded || platform || below.Kind != NavigationCellKind.Solid ||
                        Read(new Point(point.X, point.Y - from.Gravity)).Kind != NavigationCellKind.Solid) return false;
                    underWall = true;
                    continue;
                }
                if (!DescentLane(from, inlet ?? Feet(from), landing, dx, platform)) return false;
                to = Ground(point, from, offsetX);
                return true;
            }
            return false;
        }

        private bool DescentLane(NavigationNode from, Vector2 start, Vector2 landing, int dx, bool platform)
        {
            var lip = new Vector2(landing.X, start.Y + (from.Grounded ? from.Gravity : 0));
            if ((dx == 0 || KnownClear(start, lip, from.Gravity, from.MountMode, 2, choice: from.MountChoice)) &&
                KnownClear(lip, landing, from.Gravity, from.MountMode, 2, choice: from.MountChoice)) return true;
            if (Pending || dx == 0 || !from.Grounded) return false;

            Vector2 release = start;
            if (!platform)
            {
                // A solid ledge supplies no Down release. Start the fall
                // where the body's trailing edge has actually cleared it.
                int direction = Math.Sign(dx);
                int edge = (from.Cell.X + (direction > 0 ? 1 : 0)) * 16;
                release.X = edge + direction * (BodyWidth(from.MountMode, from.MountChoice) * .5f + .125f);
                if (!KnownClear(start, release, from.Gravity, from.MountMode, 2, choice: from.MountChoice)) return false;
            }
            float depth = (landing.Y - release.Y) * from.Gravity;
            Vector2 previous = release;
            for (float drop = Math.Min(16f, depth); drop > 0; drop = Math.Min(drop + 16f, depth))
            {
                var below = release + new Vector2(0, drop * from.Gravity);
                if (!KnownClear(previous, below, from.Gravity, from.MountMode, 2, choice: from.MountChoice)) return false;
                var across = new Vector2(landing.X, below.Y);
                if (KnownClear(below, across, from.Gravity, from.MountMode, 2, choice: from.MountChoice) &&
                    KnownClear(across, landing, from.Gravity, from.MountMode, 2, choice: from.MountChoice))
                    // The graph established only a clear bent envelope.
                    // The local native refinement must still meet its timing.
                    return true;
                if (Pending || drop >= depth) break;
                previous = below;
            }
            return false;
        }

        private bool PlatformInlet(NavigationNode from, Vector2 feet)
        {
            float halfWidth = BodyWidth(from.MountMode, from.MountChoice) * .5f;
            int left = CellAt(feet - new Vector2(halfWidth - .125f, 0)).X;
            int right = CellAt(feet + new Vector2(halfWidth - .125f, 0)).X;
            for (int x = left; x <= right; x++)
                for (int dy = -1; dy <= 0; dy++)
                {
                    var cell = Read(new Point(x, from.Cell.Y + dy * from.Gravity));
                    if (!cell.Known || cell.Support && cell.Kind != NavigationCellKind.Platform) return false;
                }
            // A platform beside a solid slope can share its tile-center
            // column with the observed feet while the body is still held by
            // the slope. Find an inlet through the full release footprint.
            return true;
        }

        private bool Jump(NavigationNode from, int dx, ref int branch, out NavigationNode to, out bool running)
        {
            to = default;
            running = false;
            if (from.Medium != NavigationMedium.Ground && from.Medium != NavigationMedium.Rope &&
                from.Medium != NavigationMedium.Liquid) return false;
            if (caps.JumpHeight < 12f) return false;
            var start = Feet(from);
            int direction = Math.Sign(dx);
            int bankInset = JumpBankInset(from, dx);
            int maxRise = branch == FirstBranch ? Math.Min(20, Math.Max(1, (int)(caps.JumpHeight / 16f))) : branch;
            // Scan independently developed support. The coarse arc admits
            // slopes and platforms; local native control proves entry and landing.
            for (int i = maxRise; i >= (dx == 0 ? 1 : -1); i--)
            {
                if (Pending) return false;
                branch = i;
                var point = new Point(from.Cell.X + dx, from.Cell.Y - i * from.Gravity);
                var cell = Read(point);
                if (Pending) return false;
                if (!Supports(cell, from.Gravity)) continue;
                Vector2 finish = SupportFeet(point, cell, from.Gravity, BodyWidth(from.MountMode, from.MountChoice));
                int offsetX = 0;
                if (bankInset != 0 && cell.Kind == NavigationCellKind.Solid)
                {
                    // The outside edge of a bank can catch the approaching
                    // body with positive toe overlap before its center is on
                    // that tile. Keep the real support index and edge pose.
                    var nearBank = Read(new Point(point.X - direction, point.Y));
                    if (nearBank.Known && !Supports(nearBank, from.Gravity))
                    {
                        offsetX = -direction * bankInset;
                        finish.X += offsetX;
                    }
                }
                if (Math.Abs(finish.X - start.X) > caps.JumpRange) continue;
                if (!Clear(finish, from.Gravity, from.MountMode, 2, default, false, choice: from.MountChoice)) continue;
                if (from.Grounded && point.X != from.Cell.X && ConnectGround(from, point.X, out var same) &&
                    same.Cell == point) continue;
                float rise = Math.Min(caps.JumpHeight, Math.Max(32f, (i + 1) * 16f));
                var top = new Vector2(MathHelper.Lerp(start.X, finish.X, .43f), start.Y - rise * from.Gravity);
                if (!KnownClear(start, top, from.Gravity, from.MountMode, 6, choice: from.MountChoice) ||
                    !KnownClear(top, finish, from.Gravity, from.MountMode, 6, choice: from.MountChoice)) continue;
                bool runup = i >= 2 && Math.Abs(finish.X - start.X) >= 96f && HasLaunchGap(from, direction);
                if (runup && ConnectGround(from, from.Cell.X + direction, out var runway) && runway.Cell.Y == from.Cell.Y)
                    continue;
                if (Pending) return false;
                to = Ground(point, from, offsetX);
                running = runup;
                return true;
            }
            if (!Pending) branch = FirstBranch;
            return false;
        }

        private int JumpBankInset(NavigationNode from, int offset) =>
            from.Grounded && Math.Abs(offset) >= 6 ? BodyWidth(from.MountMode, from.MountChoice) / 2 + 4 : 0;

        private bool HasLaunchGap(NavigationNode from, int direction)
        {
            for (int i = 1; i <= 2; i++)
            {
                var cell = Read(new Point(from.Cell.X + i * direction, from.Cell.Y));
                if (!cell.Known) return false;
                if (!Supports(cell, from.Gravity)) return true;
            }
            return false;
        }

        private bool Air(NavigationNode from, Point offset, out NavigationNode to, out NavigationAction kind, out Point entry)
        {
            to = default;
            kind = NavigationAction.Flight;
            entry = from.Cell;
            if (!caps.Has(NavigationModes.Wing | NavigationModes.Rocket | NavigationModes.Carpet | NavigationModes.Rope |
                NavigationModes.Swim | NavigationModes.Track | NavigationModes.FlightMount | NavigationModes.WaterMount)) return false;
            // Leave one-way support by the edge or by an explicit Down
            // transition, before generic flight can descend underneath it.
            if (from.Grounded && offset.Y * from.Gravity > 0 && Read(from.Cell).Kind == NavigationCellKind.Platform) return false;
            Point point = new Point(from.Cell.X + offset.X, from.Cell.Y + offset.Y);
            if (offset.Y == 0 && Math.Abs(offset.X) == 2 && !from.Grounded &&
                world.TrySpan(from.Cell, true, budget, out var fromSpan, out _))
            {
                int direction = Math.Sign(offset.X);
                int boundary = direction < 0 ? fromSpan.Left : fromSpan.Right - 1;
                int distance = Math.Abs(boundary - from.Cell.X);
                if (distance >= 2)
                    point.X = from.Cell.X + direction * Math.Min(3, distance);
            }
            var medium = MediumAt(point, from.Gravity);
            int energy = from.Energy;
            var landing = Read(point);
            bool mountedFlight = from.MountMode == NavigationTraversalDetail.MountFlight;
            bool canFly = energy > 0 && (caps.Has(NavigationModes.Wing | NavigationModes.Rocket | NavigationModes.Carpet) ||
                mountedFlight);
            if (medium == NavigationMedium.Rope)
            {
                if (!caps.Has(NavigationModes.Rope)) return false;
                kind = NavigationAction.Rope;
                entry = point;
            }
            else if (medium == NavigationMedium.Track)
            {
                // Physical adjacency and mount mode belong to the rail
                // transitions, even if the track tile is open air geometry.
                return false;
            }
            else if (Supports(landing, from.Gravity) && offset.Y * from.Gravity >= 0)
            {
                if (from.Grounded && from.OffsetX != 0 && offset.Y == 0 &&
                    ConnectGround(from, point.X, out var supportStep) && supportStep.Cell == point)
                {
                    kind = NavigationAction.Walk;
                    to = supportStep;
                    return true;
                }
                if (!canFly && from.Medium != NavigationMedium.Rope && from.Medium != NavigationMedium.Track &&
                    from.Medium != NavigationMedium.Liquid) return false;
                if (from.Medium == NavigationMedium.Rope)
                {
                    // Leaving the rope sideways belongs to the native jump
                    // input, with its own separate landing candidate.
                    if (offset.X != 0) return false;
                    kind = NavigationAction.Rope;
                }
                if (from.Medium == NavigationMedium.Track) kind = NavigationAction.Dismount;
                if (from.Medium == NavigationMedium.Liquid) kind = NavigationAction.Swim;
                to = Ground(point, from);
                return KnownClear(Feet(from), Feet(to), from.Gravity, from.MountMode, 4, choice: from.MountChoice);
            }
            else if (medium == NavigationMedium.Liquid || from.Medium == NavigationMedium.Liquid)
            {
                if (!caps.Has(NavigationModes.Swim | NavigationModes.WaterMount)) return false;
                kind = NavigationAction.Swim;
            }
            else
            {
                if (!canFly || caps.Has(NavigationModes.Carpet) && !caps.Has(NavigationModes.Wing | NavigationModes.Rocket) &&
                    !from.Mounted && (from.Grounded || offset.Y * from.Gravity < 0)) return false;
                float speed = from.MountChoice.Valid ? Math.Min(7f, from.MountChoice.RunSpeed) : caps.FlightSpeed;
                int needed = Math.Max(1, (int)Math.Ceiling(new Vector2(point.X - from.Cell.X, point.Y - from.Cell.Y).Length() *
                    2f / Math.Max(1f, speed)));
                if (energy < needed) return false;
                energy -= needed;
            }
            to = new NavigationNode(point, medium, from.Gravity, from.Mounted, energy, from.MountMode,
                offsetX: offset.X == 0 ? from.OffsetX : 0, mountChoice: from.MountChoice);
            if (kind == NavigationAction.Flight && from.Grounded && offset.Y * from.Gravity < 0)
            {
                var feet = Feet(from);
                var rise = new Vector2(0f, -Math.Max(24f, Math.Abs(Feet(to).Y - feet.Y)) * from.Gravity);
                // If the launch shoulder is under an overhang, a connected
                // clear floor can supply the actual lateral takeoff inlet.
                if (!KnownClear(feet, feet + rise, from.Gravity, from.MountMode, 4, choice: from.MountChoice) && !Pending && !Unknown)
                {
                    if (offset.X != 0 && ConnectGround(from, point.X, out var takeoff) && takeoff.Cell.Y == from.Cell.Y)
                    {
                        kind = NavigationAction.Walk;
                        to = takeoff;
                        return true;
                    }
                    if (offset.X == 0)
                        for (int side = -2; side <= 2; side += 4)
                            if (ConnectGround(from, from.Cell.X + side, out var floor) && floor.Cell.Y == from.Cell.Y &&
                                KnownClear(Feet(floor), Feet(floor) + rise, from.Gravity, from.MountMode, 4, choice: from.MountChoice)) return false;
                }
            }
            if (medium == NavigationMedium.Air && !world.TrySpan(point, true, budget, out _, out bool pending))
            {
                Pending |= pending;
                return false;
            }
            return KnownClear(Feet(from), Feet(to), from.Gravity, from.MountMode, 4, choice: from.MountChoice);
        }

        private bool Hook(NavigationNode from, int dx, out NavigationNode to, out Point anchor, out Vector2 inlet)
        {
            to = default;
            anchor = default;
            inlet = default;
            if (!caps.Has(NavigationModes.Hook) || from.Mounted || caps.HookRange < 32 || Math.Abs(dx * 16) >= caps.HookRange)
                return false;
            var start = Feet(from);
            int maxRise = Math.Min(26, (int)(caps.HookRange / 16f));
            for (int i = 2; i <= maxRise; i++)
            {
                var point = new Point(from.Cell.X + dx, from.Cell.Y - i * from.Gravity);
                var cell = Read(point);
                if (Pending || !cell.Known) return false;
                if (cell.Kind != NavigationCellKind.Solid && cell.Kind != NavigationCellKind.Track &&
                    cell.Kind != NavigationCellKind.Slope) continue;
                // The anchor is sampled at its tile center. The hanging
                // body must clear the rest of that tile beyond its own head.
                var hanging = new Point(point.X, point.Y + (int)Math.Ceiling((caps.Height + 10) / 16f) * from.Gravity);
                if (hanging == from.Cell) return false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    // A solid anchor can be used from the middle of a
                    // nearby shaft, where the tile-centered body clips a
                    // wall. Keep the ray target fixed while finding a body lane.
                    int offset = attempt == 0 ? 0 : attempt == 1 ? -8 : 8;
                    var adjusted = start + new Vector2(offset, 0);
                    var candidate = new NavigationNode(hanging, NavigationMedium.Air, from.Gravity, false,
                        from.Energy, offsetX: offset);
                    Vector2 end = Feet(candidate);
                    var target = new Vector2(point.X * 16f + 8, point.Y * 16f + 8);
                    var center = adjusted - new Vector2(0, caps.Height * .5f * from.Gravity);
                    if (Vector2.DistanceSquared(center, target) > caps.HookRange * caps.HookRange ||
                        !KnownClear(adjusted, end, from.Gravity, NavigationTraversalDetail.None, 2)) continue;
                    inlet = adjusted;
                    to = candidate;
                    anchor = point;
                    return true;
                }
                return false;
            }
            return false;
        }

        private bool Track(NavigationNode from, int option, out NavigationNode to, out NavigationAction kind,
            out NavigationTraversalDetail detail, out Point entry, out int branch)
        {
            to = default;
            kind = NavigationAction.Track;
            detail = NavigationTraversalDetail.TrackBoard;
            entry = from.Cell;
            branch = 0;
            int dir = option % 2 == 0 ? -1 : 1;
            if (from.Medium == NavigationMedium.Track)
            {
                if (option >= 4)
                {
                    kind = NavigationAction.Dismount;
                    detail = NavigationTraversalDetail.None;
                    return LeaveRail(from, dir, out to);
                }
                detail = NavigationTraversalDetail.TrackRide;
                var start = Read(from.Cell);
                if (start.Kind != NavigationCellKind.Track ||
                    from.RailFrame != start.FrameX && from.RailFrame != start.FrameY ||
                    !NavigationRail.Exit(from.RailFrame, dir, out int grade)) return false;
                Point next = new Point(from.Cell.X + dir, from.Cell.Y + grade);
                var track = Read(next);
                if (track.Kind != NavigationCellKind.Track) return false;
                int end = option < 2 ? track.FrameX : track.FrameY;
                if (!NavigationRail.Enters(end, dir, grade)) return false;
                branch = NavigationRail.BranchInput(track, end, grade);
                to = new NavigationNode(next, NavigationMedium.Track, 1, true, from.Energy, detail, end);
                return KnownClear(Feet(from), Feet(to), 1, detail, 3);
            }

            // Board only from a nearby proved stand/air entry; the actual
            // reach and QuickCart activation are owned by native delivery.
            Vector2 feet = Feet(from);
            Point magnet = CellAt(feet + caps.Geometry.CartMagnetFromFeet);
            int distance = (option + 2) / 4;
            int rowOffset = distance == 0 ? 0 : (option / 2 % 2 == 0 ? distance : -distance);
            entry = new Point(magnet.X + dir, magnet.Y + rowOffset);
            var tile = Read(entry);
            if (tile.Kind != NavigationCellKind.Track || !NavigationRail.TryHeight(tile.FrameX, 4, out _)) return false;
            to = new NavigationNode(entry, NavigationMedium.Track, 1, true, from.Energy, NavigationTraversalDetail.TrackRide, tile.FrameX);
            Vector2 exit = Feet(to);
            return Vector2.DistanceSquared(feet, exit) <= 38f * 38f &&
                KnownClear(feet, exit, 1, NavigationTraversalDetail.TrackRide, 3);
        }

        private bool LeaveRail(NavigationNode from, int dir, out NavigationNode to)
        {
            to = default;
            Vector2 start = Feet(from);
            var next = CellAt(start);
            next.X += dir;
            for (int down = 0; down <= 3; down++, next.Y++)
            {
                if (!Supports(Read(next), from.Gravity)) continue;
                to = new NavigationNode(next, NavigationMedium.Ground, from.Gravity, false, Energy(caps.MaxFlightTicks));
                return KnownClear(start, Feet(to), from.Gravity, NavigationTraversalDetail.None, 3);
            }
            return false;
        }

        private bool Carpet(NavigationNode from, int option, out NavigationNode to)
        {
            to = default;
            if (from.Energy == 0 || caps.JumpHeight < 24f || caps.FlightSpeed <= 0) return false;
            int dir = option % 2 == 0 ? -1 : 1;
            int gap = 0;
            int limit = Math.Min(28, (int)(from.Energy * 8 * caps.FlightSpeed * .5f / 16f));
            Vector2 start = Feet(from);
            float rise = Math.Min(44f, caps.JumpHeight * .55f);
            for (int i = 1; i <= limit; i++)
            {
                int x = from.Cell.X + i * dir;
                bool supported = false;
                for (int down = 0; down <= (option < 2 ? 1 : 3); down++)
                {
                    var bank = new Point(x, from.Cell.Y + down * from.Gravity);
                    var cell = Read(bank);
                    if (!cell.Known) return false;
                    if (!Supports(cell, from.Gravity)) continue;
                    supported = true;
                    if (gap < 2) break;
                    var end = Ground(bank, from);
                    Vector2 landing = Feet(end);
                    var takeoff = new Vector2(start.X + dir * Math.Min(16f, i * 5f), start.Y - rise * from.Gravity);
                    var coast = new Vector2(landing.X - dir * 8f, takeoff.Y);
                    if (!KnownClear(start, takeoff, from.Gravity, NavigationTraversalDetail.None, 2) ||
                        !KnownClear(takeoff, coast, from.Gravity, NavigationTraversalDetail.None) ||
                        !KnownClear(coast, landing, from.Gravity, NavigationTraversalDetail.None, 2)) return false;
                    to = end;
                    return true;
                }
                if (!supported) gap++;
                else gap = 0;
            }
            return false;
        }

        private static bool MountEligible(NavigationNode from, NavigationTraversalDetail detail, CompanionMountChoice choice)
        {
            if (!choice.Valid) return false;
            switch (detail)
            {
                case NavigationTraversalDetail.MountFlight: return choice.CanFly && choice.FlightTicks > 0;
                case NavigationTraversalDetail.MountWater: return from.Medium == NavigationMedium.Liquid && choice.WaterSpeed > 0f;
                case NavigationTraversalDetail.MountNarrow: return from.Grounded && choice.Compact;
                default: return from.Grounded && !choice.Compact && choice.RunSpeed > 0f;
            }
        }

        private bool Mount(NavigationNode from, NavigationTraversalDetail detail, out NavigationNode to, out NavigationAction action,
            CompanionMountChoice choice = default)
        {
            to = default;
            action = NavigationAction.Mount;
            if (from.Mounted)
            {
                action = NavigationAction.Dismount;
                to = new NavigationNode(from.Cell, from.Medium, from.Gravity, false, from.Energy);
                return Clear(Feet(to), to.Gravity, NavigationTraversalDetail.None, 2, default, false);
            }
            if (choice.Valid ? !MountEligible(from, detail, choice) :
                detail == NavigationTraversalDetail.MountGround && (!from.Grounded || !caps.Has(NavigationModes.GroundMount)))
                return false;
            int energy = detail == NavigationTraversalDetail.MountFlight
                ? Energy(choice.Valid ? choice.FlightTicks : caps.AvailableMountFlightTicks) : from.Energy;
            to = new NavigationNode(from.Cell, from.Medium, from.Gravity, true, energy, detail, mountChoice: choice);
            return Clear(Feet(to), to.Gravity, detail, 1, default, false, choice: choice);
        }

        private bool OpenUpward(NavigationNode from, out NavigationNode to, out Point entry, out Vector2 inlet)
        {
            to = default;
            entry = default;
            inlet = default;
            int limit = Math.Min(14, Math.Max(3, (int)(caps.JumpHeight / 16f) + from.Energy));
            for (int i = 2; i <= limit; i++)
            {
                var tile = new Point(from.Cell.X, from.Cell.Y - i);
                var cell = Read(tile);
                if (Pending || !cell.Known || cell.FullBarrier) return false;
                if (cell.Kind == NavigationCellKind.Trapdoor)
                    return TraverseTrapdoor(from, tile, false, out to, out entry, out inlet);
            }
            return false;
        }

        private bool TraverseTrapdoor(NavigationNode from, Point tile, bool down, out NavigationNode to,
            out Point entry, out Vector2 inlet)
        {
            to = default;
            entry = default;
            inlet = default;
            if (from.Gravity < 0 || !CompanionDoor.TryRead(tile, out var hatch) ||
                hatch.Family != CompanionDoorFamily.Trapdoor || hatch.Open) return false;
            var opening = hatch.Aperture;
            int width = BodyWidth(from.MountMode, from.MountChoice);
            if (width <= 0 || width > opening.Width) return false;
            entry = hatch.Hinge;
            if (!Read(entry).Known || !Read(new Point(entry.X + 1, entry.Y)).Known) return false;

            // A two-cell hatch can admit the body between the tile centers
            // even when each cell-centered body overlaps a shaft wall.
            int center = opening.Center.X;
            int column = center / 16;
            int offset = center - (column * 16 + 8);
            inlet = new Vector2(center, Feet(from).Y);
            if (down)
            {
                for (int i = 1; i <= 48; i++)
                {
                    int row = entry.Y + i;
                    var right = new Point(column, row);
                    var left = new Point(column - 1, row);
                    var rightCell = Read(right);
                    var leftCell = Read(left);
                    if (Pending || !rightCell.Known || !leftCell.Known) return false;
                    bool rightSupport = Supports(rightCell, from.Gravity);
                    bool leftSupport = Supports(leftCell, from.Gravity);
                    if (!rightSupport && !leftSupport) continue;
                    bool useRight = rightSupport && (!leftSupport ||
                        SupportFeet(right, rightCell, from.Gravity, width).Y <= SupportFeet(left, leftCell, from.Gravity, width).Y);
                    Point floor = useRight ? right : left;
                    to = Ground(floor, from, center - (floor.X * 16 + 8));
                    break;
                }
                if (!to.Grounded) return false;
            }
            else
            {
                // Keep a reachable inlet above the moving leaf, so an
                // unpowered jump can terminate the crossing at its actual
                // upper approach band without a fictitious extra air edge.
                var exit = new Point(column, entry.Y - 2);
                to = new NavigationNode(exit, NavigationMedium.Air, from.Gravity, from.Mounted,
                    Math.Max(0, from.Energy - 1), from.MountMode, offsetX: offset, mountChoice: from.MountChoice);
            }
            Vector2 finish = Feet(to);
            int steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(inlet, finish) / 8f));
            for (int sample = 0; sample <= steps; sample++)
                if (!Clear(Vector2.Lerp(inlet, finish, sample / (float)steps), from.Gravity,
                    from.MountMode, 2, entry, true, choice: from.MountChoice)) return false;
            return true;
        }

        private bool ReverseGravity(NavigationNode from, out NavigationNode to)
        {
            to = default;
            for (int i = 3; i <= 16; i++)
            {
                var point = new Point(from.Cell.X, from.Cell.Y - i * from.Gravity);
                var cell = Read(point);
                if (Pending || !cell.Known) return false;
                if (!Supports(cell, -from.Gravity)) continue;
                to = new NavigationNode(point, NavigationMedium.Ground, -from.Gravity, false, Energy(caps.MaxFlightTicks));
                // The reference foot changes to the previous head at the
                // gravity flip; the old floor itself must not enter the sweep.
                var switched = Feet(from) - new Vector2(0, caps.Height * from.Gravity);
                return KnownClear(switched, Feet(to), to.Gravity, from.MountMode, 2, choice: from.MountChoice);
            }
            return false;
        }

        private bool SpecialGround(NavigationNode from, out NavigationNode to, out NavigationAction kind, out Point entry)
        {
            to = default;
            kind = NavigationAction.Hoik;
            entry = default;
            if (!from.Grounded || !HoikBody(from.Gravity, from.MountMode) ||
                Read(from.Cell).Kind != NavigationCellKind.Solid) return false;
            Vector2 start = Feet(from);
            int head = (int)Math.Floor((start.Y - caps.Height) / 16f);
            for (int dir = -1; dir <= 1; dir += 2)
            {
                var tooth = new Point(from.Cell.X + dir, head);
                if (!HoikTooth(tooth, Read(tooth), dir))
                {
                    tooth.X += dir;
                    if (!HoikTooth(tooth, Read(tooth), dir)) continue;
                }
                var endTooth = tooth;
                var next = new Point(endTooth.X + dir * 2, head);
                while (Math.Abs(next.X * 16f + 8f - start.X) < 84f && HoikTooth(next, Read(next), dir))
                {
                    endTooth = next;
                    next.X += dir * 2;
                }
                // Return a whole-body exit past the final tooth. A tile
                // center alone can leave the trailing two pixels inside it.
                int exitX = endTooth.X * 16 + (dir > 0 ? 16 : 0) + dir * (caps.Width / 2 + 1);
                if (Math.Abs(exitX - start.X) > 84f) continue;
                var point = new Point((int)Math.Floor(exitX / 16f), from.Cell.Y);
                if (Read(point).Kind != NavigationCellKind.Solid) continue;
                var end = Ground(point, from, exitX - (point.X * 16 + 8));
                if (!Clear(Feet(end), from.Gravity, from.MountMode, 0, default, false, dir, choice: from.MountChoice)) continue;
                to = end;
                entry = tooth;
                return true;
            }
            return false;
        }

        private bool HoikBody(int gravity, NavigationTraversalDetail mount) => gravity > 0 &&
            mount == NavigationTraversalDetail.None && caps.Has(NavigationModes.Hoik) &&
            BodyWidth(mount) == Player.defaultWidth &&
            (caps.Geometry.BaseHeight > 0 ? caps.Geometry.BaseHeight : caps.Height) == Player.defaultHeight;

        private bool HoikTooth(Point point, NavigationCell tooth, int direction)
        {
            if (tooth.Kind != NavigationCellKind.Slope || tooth.Slope != (direction > 0 ? 3 : 4)) return false;
            var mouth = Read(new Point(point.X - direction, point.Y));
            return mouth.Active && mouth.Actuated && mouth.Slope == (direction > 0 ? 4 : 3);
        }

        private NavigationNode Ground(Point point, NavigationNode from, int offsetX = 0) =>
            new NavigationNode(point, NavigationMedium.Ground, from.Gravity, from.Mounted,
                Energy(from.MountMode == NavigationTraversalDetail.MountFlight && from.MountChoice.Valid
                    ? from.MountChoice.FlightTicks : from.MountMode == NavigationTraversalDetail.MountFlight && !caps.MountCanFly
                        ? caps.AvailableMountFlightTicks : caps.MaxFlightTicks),
                from.MountMode, offsetX: offsetX, mountChoice: from.MountChoice);

        private NavigationMedium MediumAt(Point point, int gravity)
        {
            var cell = Read(point);
            var head = Read(new Point(point.X, point.Y - gravity));
            if (HasRope(point, cell) || HasRope(new Point(point.X, point.Y - gravity), head)) return NavigationMedium.Rope;
            if (cell.Kind == NavigationCellKind.Track) return NavigationMedium.Track;
            if (cell.Amount >= 96 && (!cell.Hazard || caps.Has(NavigationModes.LavaImmune) && cell.Liquid == LiquidID.Lava))
                return NavigationMedium.Liquid;
            return NavigationMedium.Air;
        }

        private bool HasRope(Point point, NavigationCell cell)
        {
            if (cell.Kind == NavigationCellKind.Rope) return true;
            if (!cell.RopeOverlap) return false;
            // GetRopeEnds crosses active tiles and requires an actual rope at
            // both ends of the same short run; geometry is checked separately.
            int length = 0;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                bool reachesRope = false;
                for (int i = 1; i <= 5; i++)
                {
                    var next = Read(new Point(point.X, point.Y + i * direction));
                    if (next.Kind == NavigationCellKind.Rope)
                    {
                        reachesRope = true;
                        length += i;
                        break;
                    }
                    if (!next.Active) return false;
                }
                if (!reachesRope) return false;
            }
            return length <= 6;
        }

        private NavigationCell Read(Point point)
        {
            NavigationCell cell = default;
            if (!budget.TryShape() || !world.Read(point, budget, out cell)) Pending = true;
            else if (!cell.Known) Unknown = true;
            readSet?.Record(point);
            return cell;
        }

        private bool Clear(Vector2 feet, int gravity, NavigationTraversalDetail mount, int allowance, Point door, bool allowDoor,
            int walkDirection = 0, CompanionMountChoice choice = default)
        {
            // Alternate bodies use the selected native mount's dimensions.
            // Dismount always tests the ordinary body, including from a Rat.
            int width = BodyWidth(mount, choice);
            int height = choice.Valid ? caps.Mounted && choice.Type == caps.ActiveMountType ? caps.Height : choice.Height
                : mount == NavigationTraversalDetail.None
                ? caps.Geometry.BaseHeight > 0 ? caps.Geometry.BaseHeight : !caps.Mounted ? caps.Height : 0
                : mount == CurrentMount && caps.Mounted ? caps.Height : caps.Geometry.MountHeight(mount);
            if (width <= 0 || height <= 0) return false;
            int left = CellAt(feet - new Vector2(width * .5f - .125f, 0)).X;
            int right = CellAt(feet + new Vector2(width * .5f - .125f, 0)).X;
            var top = gravity > 0 ? feet.Y - height : feet.Y + allowance + .125f;
            var bottom = gravity > 0 ? feet.Y - allowance - .125f : feet.Y + height;
            int first = (int)Math.Floor(top / 16f), last = (int)Math.Floor(bottom / 16f);
            for (int y = first; y <= last; y++)
                for (int x = left; x <= right; x++)
                {
                    var point = new Point(x, y);
                    var cell = Read(point);
                    if (!cell.Known) return false;
                    if (cell.FullBarrier && !(allowDoor && cell.Kind == NavigationCellKind.Door && x == door.X &&
                        Math.Abs(y - door.Y) <= 4)) return false;
                    if (cell.Kind == NavigationCellKind.Trapdoor && !(allowDoor && y == door.Y &&
                        Math.Abs(x - door.X) <= 1)) return false;
                    // This original contact is a directional impulse. Stop
                    // walking at its mouth so native refinement owns passage.
                    if (walkDirection != 0 && y == first && cell.Kind == NavigationCellKind.Slope &&
                        (first + 1) * 16f - top >= 4f && (first + 1) * 16f - top <= 12f &&
                        HoikBody(gravity, mount) && HoikTooth(point, cell, walkDirection)) return false;
                    if (cell.Hazard && !(caps.Has(NavigationModes.LavaImmune) && cell.Liquid == LiquidID.Lava)) return false;
                }
            return true;
        }

        private int BodyWidth(NavigationTraversalDetail mount, CompanionMountChoice choice = default)
        {
            if (choice.Valid) return caps.Mounted && choice.Type == caps.ActiveMountType ? caps.Width : choice.Width;
            if (mount != NavigationTraversalDetail.None && mount == CurrentMount && caps.Mounted) return caps.Width;
            if (mount == NavigationTraversalDetail.MountNarrow) return caps.Geometry.NarrowMountWidth;
            return caps.Geometry.BaseWidth > 0 ? caps.Geometry.BaseWidth : caps.Width;
        }

        private bool Supports(NavigationCell cell, int gravity) => cell.Known &&
            (cell.Support && (gravity > 0 || cell.Kind != NavigationCellKind.Platform) ||
            gravity > 0 && caps.Has(NavigationModes.WaterWalk) && cell.Amount >= 32 && !cell.Hazard);

        internal static Point CellAt(Vector2 point) =>
            new Point((int)Math.Floor(point.X / 16f), (int)Math.Floor(point.Y / 16f));

        private static Vector2 SupportFeet(Point point, NavigationCell cell, int gravity, int width)
        {
            float height = point.Y * 16f + (gravity < 0 ? 16f : 0f);
            if (cell.Kind == NavigationCellKind.Half && gravity > 0) height += 8;
            if (gravity > 0 && (cell.Slope == 1 || cell.Slope == 2))
                height += Math.Max(0f, 8 - width * .5f);
            if (gravity < 0 && (cell.Slope == 3 || cell.Slope == 4))
                height -= Math.Max(0f, 8 - width * .5f);
            if (!cell.Support && cell.Amount > 0 && gravity > 0) height += 16f * (255 - cell.Amount) / 255f;
            return new Vector2(point.X * 16f + 8, height);
        }

        private static int Energy(int flightTicks) => Math.Min(byte.MaxValue, flightTicks / 8);

        private static float ActionCost(NavigationAction kind)
        {
            switch (kind)
            {
                case NavigationAction.Walk: return 0;
                case NavigationAction.PlatformDrop: return 1;
                case NavigationAction.Drop: return 2;
                case NavigationAction.Jump: return 3;
                case NavigationAction.Flight: return 2;
                case NavigationAction.Gravity: return 18;
                default: return 4;
            }
        }
    }
}
