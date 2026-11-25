using System;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    [Flags]
    internal enum NavigationModes
    {
        None = 0, Wing = 1, Rocket = 2, Carpet = 4, Hover = 8, Rope = 16,
        Track = 32, Swim = 64, Hook = 128, Mount = 256, Dash = 512,
        Gravity = 1024, Facility = 2048, Hoik = 4096, WaterWalk = 8192,
        LavaImmune = 16384, GroundMount = 32768, FlightMount = 65536, WaterMount = 131072, NarrowMount = 262144
    }

    internal enum NavigationAction
    {
        Walk, Drop, PlatformDrop, Jump, Flight, Rope, Track, Swim, Hook, Mount,
        Dismount, Door, Hoik, Dash, Gravity, Facility
    }

    internal enum NavigationTraversalDetail
    {
        None, MountGround, MountFlight, MountWater, Teleporter, Portal, Pylon, Rod, GravitySwitch, MountNarrow,
        TrackBoard, TrackRide, CarpetGlide, RunningJump, FlightDash
    }

    internal enum NavigationQueryStatus
    {
        Idle, ReadyPrefix, PendingBudget, NeedsMoreRegion, AwaitingWorldData, NoRouteInKnownGraph
    }

    internal readonly struct NavigationActorGeometry
    {
        internal readonly int BaseWidth, BaseHeight, GroundMountHeight, FlightMountHeight, WaterMountHeight,
            NarrowMountHeight, NarrowMountWidth, CartHeight, Life, FallProtectionKey;
        internal readonly Vector2 CartMagnetFromFeet;
        internal readonly bool RidingTrack, CarpetAvailable;

        internal NavigationActorGeometry(int baseWidth, int baseHeight, int groundMountHeight, int flightMountHeight,
            int waterMountHeight, int narrowMountHeight, int cartHeight, Vector2 cartMagnetFromFeet,
            int life, int fallProtectionKey, bool ridingTrack, bool carpetAvailable)
            : this(baseWidth, baseHeight, groundMountHeight, flightMountHeight, waterMountHeight, narrowMountHeight,
                cartHeight, cartMagnetFromFeet, life, fallProtectionKey, ridingTrack, carpetAvailable, 0)
        {
        }

        internal NavigationActorGeometry(int baseWidth, int baseHeight, int groundMountHeight, int flightMountHeight,
            int waterMountHeight, int narrowMountHeight, int cartHeight, Vector2 cartMagnetFromFeet,
            int life, int fallProtectionKey, bool ridingTrack, bool carpetAvailable, int narrowMountWidth)
        {
            BaseWidth = baseWidth;
            BaseHeight = baseHeight;
            GroundMountHeight = groundMountHeight;
            FlightMountHeight = flightMountHeight;
            WaterMountHeight = waterMountHeight;
            NarrowMountHeight = narrowMountHeight;
            NarrowMountWidth = narrowMountWidth;
            CartHeight = cartHeight;
            CartMagnetFromFeet = cartMagnetFromFeet;
            Life = life;
            FallProtectionKey = fallProtectionKey;
            RidingTrack = ridingTrack;
            CarpetAvailable = carpetAvailable;
        }

        internal int MountHeight(NavigationTraversalDetail mode)
        {
            switch (mode)
            {
                case NavigationTraversalDetail.MountGround: return GroundMountHeight;
                case NavigationTraversalDetail.MountFlight: return FlightMountHeight;
                case NavigationTraversalDetail.MountWater: return WaterMountHeight;
                case NavigationTraversalDetail.MountNarrow: return NarrowMountHeight;
                case NavigationTraversalDetail.TrackRide: return CartHeight;
                default: return 0;
            }
        }

        internal bool SameShape(NavigationActorGeometry other) => BaseWidth == other.BaseWidth &&
            BaseHeight == other.BaseHeight && GroundMountHeight == other.GroundMountHeight &&
            FlightMountHeight == other.FlightMountHeight && WaterMountHeight == other.WaterMountHeight &&
            NarrowMountHeight == other.NarrowMountHeight && NarrowMountWidth == other.NarrowMountWidth && CartHeight == other.CartHeight &&
            CartMagnetFromFeet == other.CartMagnetFromFeet;
    }

    internal readonly struct NavigationCapabilities
    {
        internal readonly int Width, Height, Gravity, FlightTicks, MaxFlightTicks;
        internal readonly float JumpHeight, JumpRange, FlightSpeed, HookRange;
        internal readonly NavigationModes Modes;
        internal readonly bool Supported, Mounted, MountCanFly;
        internal readonly NavigationTraversalDetail ActiveMount;
        internal readonly int AvailableMountFlightTicks;
        internal readonly NavigationActorGeometry Geometry;
        internal readonly CompanionMountChoice[] MountChoices;
        internal readonly int ActiveMountType;

        internal NavigationCapabilities(int width, int height, int gravity, float jumpHeight, float jumpRange,
            int flightTicks, int maxFlightTicks, float flightSpeed, float hookRange, NavigationModes modes,
            bool supported, bool mounted = false)
            : this(width, height, gravity, jumpHeight, jumpRange, flightTicks, maxFlightTicks, flightSpeed,
                hookRange, modes, supported, mounted, NavigationTraversalDetail.None, false, 0)
        {
        }

        internal NavigationCapabilities(int width, int height, int gravity, float jumpHeight, float jumpRange,
            int flightTicks, int maxFlightTicks, float flightSpeed, float hookRange, NavigationModes modes,
            bool supported, bool mounted, NavigationTraversalDetail activeMount, bool mountCanFly, int availableMountFlightTicks)
            : this(width, height, gravity, jumpHeight, jumpRange, flightTicks, maxFlightTicks, flightSpeed, hookRange,
                modes, supported, mounted, activeMount, mountCanFly, availableMountFlightTicks, default)
        {
        }

        private NavigationCapabilities(int width, int height, int gravity, float jumpHeight, float jumpRange,
            int flightTicks, int maxFlightTicks, float flightSpeed, float hookRange, NavigationModes modes,
            bool supported, bool mounted, NavigationTraversalDetail activeMount, bool mountCanFly,
            int availableMountFlightTicks, NavigationActorGeometry geometry,
            CompanionMountChoice[] mountChoices = null, int activeMountType = -1)
        {
            Width = width;
            Height = height;
            Gravity = gravity < 0 ? -1 : 1;
            JumpHeight = jumpHeight;
            JumpRange = jumpRange;
            FlightTicks = Math.Max(0, flightTicks);
            MaxFlightTicks = Math.Max(0, maxFlightTicks);
            FlightSpeed = flightSpeed;
            HookRange = hookRange;
            Modes = modes;
            Supported = supported;
            Mounted = mounted;
            ActiveMount = mounted ? activeMount : NavigationTraversalDetail.None;
            MountCanFly = mounted && mountCanFly;
            AvailableMountFlightTicks = Math.Max(0, availableMountFlightTicks);
            Geometry = geometry;
            MountChoices = mountChoices;
            ActiveMountType = activeMountType;
        }

        internal bool Has(NavigationModes modes) => (Modes & modes) != 0;
        internal NavigationCapabilities WithCondition(NavigationActorGeometry geometry) =>
            new NavigationCapabilities(Width, Height, Gravity, JumpHeight, JumpRange, FlightTicks, MaxFlightTicks,
                FlightSpeed, HookRange, Modes, Supported, Mounted, ActiveMount, MountCanFly, AvailableMountFlightTicks, geometry,
                MountChoices, ActiveMountType);

        internal NavigationCapabilities WithMountChoices(CompanionMountChoice[] choices, int activeMountType) =>
            new NavigationCapabilities(Width, Height, Gravity, JumpHeight, JumpRange, FlightTicks, MaxFlightTicks,
                FlightSpeed, HookRange, Modes, Supported, Mounted, ActiveMount, MountCanFly, AvailableMountFlightTicks,
                Geometry, choices, activeMountType);

        internal bool SameFlightConditions(NavigationCapabilities other) =>
            Geometry.CarpetAvailable == other.Geometry.CarpetAvailable && FlightTicks / 8 == other.FlightTicks / 8;

        internal bool SameFailureConditions(NavigationCapabilities other, NavigationAction action)
        {
            if (!SameShape(other)) return false;
            if (action == NavigationAction.Flight && !SameFlightConditions(other)) return false;
            if (action == NavigationAction.Drop || action == NavigationAction.PlatformDrop || action == NavigationAction.Flight)
                return Geometry.Life == other.Geometry.Life && Geometry.FallProtectionKey == other.Geometry.FallProtectionKey;
            return true;
        }

        internal bool SameShape(NavigationCapabilities other) =>
            Width == other.Width && Height == other.Height && Gravity == other.Gravity &&
            Mounted == other.Mounted && ActiveMount == other.ActiveMount && MountCanFly == other.MountCanFly &&
            Modes == other.Modes && Math.Abs(JumpHeight - other.JumpHeight) < 8f &&
            Math.Abs(JumpRange - other.JumpRange) < 8f && MaxFlightTicks == other.MaxFlightTicks &&
            Math.Abs(FlightSpeed - other.FlightSpeed) < .35f && HookRange == other.HookRange &&
            AvailableMountFlightTicks == other.AvailableMountFlightTicks && Geometry.SameShape(other.Geometry) &&
            ActiveMountType == other.ActiveMountType && ReferenceEquals(MountChoices, other.MountChoices);
    }

    // Coarse offers name a direction and a geometric dependency, never a proof
    // that the active Player can reach the predicted exit.
    internal readonly struct NavigationTransition
    {
        internal readonly NavigationAction Kind;
        internal readonly Vector2 From, To;
        internal readonly Point EntryTile, ExitTile;
        internal readonly Vector2 InteractionPosition;
        internal readonly int TerrainVersion;
        internal readonly bool Irreversible;
        internal readonly NavigationTraversalDetail Detail;
        internal readonly int RailDirection, RailFrame, RailEntryFrame;
        internal readonly CompanionMountChoice MountChoice;

        internal NavigationTransition(NavigationAction kind, Vector2 from, Vector2 to, Point entryTile = default,
            Point exitTile = default, int terrainVersion = 0, bool irreversible = false,
            NavigationTraversalDetail detail = NavigationTraversalDetail.None, Vector2 interactionPosition = default)
            : this(kind, from, to, entryTile, exitTile, terrainVersion, irreversible, detail, interactionPosition, 0, -1)
        {
        }

        internal NavigationTransition(NavigationAction kind, Vector2 from, Vector2 to, CompanionMountChoice mountChoice,
            Point entryTile = default, Point exitTile = default, int terrainVersion = 0, bool irreversible = false,
            NavigationTraversalDetail detail = NavigationTraversalDetail.None, Vector2 interactionPosition = default)
            : this(kind, from, to, entryTile, exitTile, terrainVersion, irreversible, detail, interactionPosition, 0, -1,
                mountChoice: mountChoice)
        {
        }

        internal NavigationTransition(NavigationAction kind, Vector2 from, Vector2 to, Point entryTile,
            Point exitTile, int terrainVersion, bool irreversible, NavigationTraversalDetail detail,
            Vector2 interactionPosition, int railDirection, int railFrame, int railEntryFrame = -1,
            CompanionMountChoice mountChoice = default)
        {
            Kind = kind;
            From = from;
            To = to;
            EntryTile = entryTile;
            ExitTile = exitTile;
            TerrainVersion = terrainVersion;
            Irreversible = irreversible;
            Detail = detail;
            InteractionPosition = interactionPosition;
            RailDirection = railDirection;
            RailFrame = railFrame;
            RailEntryFrame = railEntryFrame;
            MountChoice = mountChoice;
        }

        internal bool SameEdge(NavigationTransition other) =>
            Kind == other.Kind && From == other.From && To == other.To && EntryTile == other.EntryTile &&
            ExitTile == other.ExitTile && Detail == other.Detail && InteractionPosition == other.InteractionPosition &&
            RailDirection == other.RailDirection && RailFrame == other.RailFrame && RailEntryFrame == other.RailEntryFrame &&
            MountChoice.Equals(other.MountChoice);
    }

    // Shared across bodies for the runtime slice. Chunk extraction, candidate
    // discovery and native refinement each have a real, separately counted cap.
    internal sealed class NavigationBudget
    {
        private readonly int tileLimit, expansionLimit, predictionLimit;
        private readonly int shapeLimit;
        internal int Tiles { get; private set; }
        internal int Expansions { get; private set; }
        internal int Predictions { get; private set; }
        internal int ShapeReads { get; private set; }
        internal int AStarExpansions { get; private set; }
        internal int Candidates { get; private set; }
        internal int TilesRemaining => tileLimit - Tiles;
        internal int ExpansionsRemaining => expansionLimit - Expansions;
        internal int PredictionsRemaining => predictionLimit - Predictions;

        internal NavigationBudget(int tiles, int expansions, int predictions)
        {
            tileLimit = tiles;
            expansionLimit = expansions;
            predictionLimit = predictions;
            shapeLimit = Math.Max(2048, tiles + expansions * 64);
        }

        internal bool TryTiles(int count = 1)
        {
            if (count > TilesRemaining) return false;
            Tiles += count;
            return true;
        }

        internal bool TryExpand(int count = 1)
        {
            if (count > ExpansionsRemaining) return false;
            Expansions += count;
            return true;
        }

        internal bool TryPredict(int count = 1)
        {
            if (count > PredictionsRemaining) return false;
            Predictions += count;
            return true;
        }

        internal void Reset()
        {
            Tiles = Expansions = Predictions = ShapeReads = AStarExpansions = Candidates = 0;
        }

        internal bool TryShape()
        {
            if (ShapeReads >= shapeLimit) return false;
            ShapeReads++;
            return true;
        }

        internal void RecordExpansion() => AStarExpansions++;
        internal void RecordCandidate() => Candidates++;
    }
}
