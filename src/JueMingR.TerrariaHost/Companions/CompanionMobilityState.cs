using System;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionMobilityResult { Idle, Deferred, Started, Completed, Rejected }

    // Immutable after observation. A query and its published edges keep the
    // exact shape and native source that were compared, even after inventory changes.
    internal readonly struct CompanionMountChoice : IEquatable<CompanionMountChoice>
    {
        internal const int UnboundedFlight = 10000;
        internal readonly int ItemSlot, Type, Width, Height, FlightTicks;
        internal readonly float RunSpeed, Acceleration, WaterSpeed;
        internal readonly bool Hover, Compact;
        internal bool Valid => Width > 0;
        internal bool CanFly => FlightTicks > 0;

        internal CompanionMountChoice(int slot, int type, int width, int height, int flightTicks,
            float runSpeed, float acceleration, float waterSpeed, bool hover, bool compact)
        {
            ItemSlot = slot;
            Type = type;
            Width = width;
            Height = height;
            FlightTicks = flightTicks;
            RunSpeed = runSpeed;
            Acceleration = acceleration;
            WaterSpeed = waterSpeed;
            Hover = hover;
            Compact = compact;
        }

        public bool Equals(CompanionMountChoice other) => Type == other.Type && ItemSlot == other.ItemSlot &&
            Width == other.Width && Height == other.Height && FlightTicks == other.FlightTicks &&
            RunSpeed == other.RunSpeed && Acceleration == other.Acceleration && WaterSpeed == other.WaterSpeed &&
            Hover == other.Hover && Compact == other.Compact;

        public override bool Equals(object obj) => obj is CompanionMountChoice other && Equals(other);
        public override int GetHashCode() => Type * 59 + ItemSlot;

        internal bool SourceCurrent(Player body)
        {
            if (!Valid || ItemSlot < -1 || ItemSlot >= body.inventory.Length) return false;
            var item = ItemSlot < 0 ? body.miscEquips[3] : body.inventory[ItemSlot];
            return item != null && !item.IsAir && item.mountType == Type && (!item.expertOnly || Main.expertMode);
        }
    }

    internal struct CompanionMobilitySnapshot
    {
        internal CompanionMountChoice[] MountChoices;
        internal bool Ready, ReleaseJump, Sliding, DashReady, DashActive, UnlimitedWings, WingHover, WingHovering, WingHoverRun, WingFastAscent,
            WingAbilities, RocketReady, RocketAvailable,
            Carpet, CarpetAvailable, CarpetReady, MountFly, MountHover, MountUseWings, MountCart, Track, CanChangeGravity,
            Pulley, GrappleAttached, GrappleInFlight, Wet, Lava, Honey, Shimmer, CanSwim, MountSwim, BreathProtected, LavaImmune,
            Controlled, HandBusy, CanUseRod, CanUseCart, CanDismount, UnlimitedMountHover;
        internal int Width, Height, JumpHeight, JumpTime, ExtraJumps, WallSide, WallGrip, DashType, DashDelay, DashKind,
            Wings, WingTimeMax, RocketBoots, RocketTime, RocketDelay, CarpetTime, MountType, MountFlightTime,
            HookType, Breath, BreathMax, RodSlot, LavaTime;
        internal float GravityDirection, Gravity, Speed, RunAcceleration, RunSlowdown, MaxFallSpeed,
            JumpSpeed, WingTime, MountFatigue, MountFatigueMax, HookRange;
        internal float WingSpeed, WingAcceleration, WingHoverSpeed, WingHoverAcceleration, WingSlowdown;
        internal float WingBaseSpeed, WingBaseAcceleration, WingBaseSlowdown;
    }

    internal sealed class CompanionMobilityState
    {
        private static readonly AccessTools.FieldRef<Mount, int> flightTime = AccessTools.FieldRefAccess<Mount, int>("_flyTime");
        private static readonly AccessTools.FieldRef<Mount, float> fatigue = AccessTools.FieldRefAccess<Mount, float>("_fatigue");
        private static readonly AccessTools.FieldRef<Mount, float> fatigueMax = AccessTools.FieldRefAccess<Mount, float>("_fatigueMax");
        private static readonly AccessTools.FieldRef<Mount.MountData[]> mounts = AccessTools.StaticFieldRefAccess<Mount.MountData[]>(AccessTools.Field(typeof(Mount), "mounts"));
        private static readonly System.Func<Mount, bool> unlimitedHover = AccessTools.MethodDelegate<System.Func<Mount, bool>>(
            AccessTools.Method(typeof(Mount), "DoesHoverIgnoresFatigue"));

        internal CompanionMobilitySnapshot Observed;
        internal CompanionMobilityResult Result;
        internal int Sequence, PositionRevision;
        internal long Generation;
        internal CompanionMovementActivation Action;
        internal Vector2 Target, Aim, ExpectedExit;
        internal CompanionDoor DoorOpened;
        internal CompanionDoorFamily DoorFamily;
        internal int DoorPass, DoorOpenedPass;
        internal uint DoorOpenedAt;
        internal uint Started, NextSourceScan;
        internal long SourceGestureRevision;
        internal bool SourceChaos;
        internal bool DoorNeedsClose, Prepared, HandPressed, ShapeHandExit, AwaitingEquipmentObservation;
        internal Item Source;
        internal Item[] SourceItems;
        internal int SourceSlot = -1, PreviousSelection = -1, SourceType, SourceStack;
        internal byte SourcePrefix;
        internal Projectile Hook;
        internal int HookKey;
        internal bool WingRunSampled;
        internal readonly List<CompanionMountChoice> MountChoiceScratch = new List<CompanionMountChoice>(4);
        private float wingBaseSpeed, wingBaseAcceleration;
        private bool wingSampledHover;
        private struct HookLease
        {
            internal Projectile Body;
            internal int Key;
        }
        private List<HookLease> hooks;

        internal void BindSource(Item[] items, int slot)
        {
            SourceItems = items;
            SourceSlot = slot;
            Source = items[slot];
            SourceType = Source.type;
            SourceStack = Source.stack;
            SourcePrefix = Source.prefix;
        }

        internal bool SourceCurrent(Player body) => Source != null &&
            (ReferenceEquals(SourceItems, body.inventory) || ReferenceEquals(SourceItems, body.miscEquips)) &&
            ReferenceEquals(Source, SourceItems[SourceSlot]) && Source.type == SourceType &&
            Source.stack == SourceStack && Source.prefix == SourcePrefix;

        internal void SetHook(Projectile hook)
        {
            Hook = hook;
            HookKey = (int)hook.key;
            (hooks ?? (hooks = new List<HookLease>(4))).Add(new HookLease { Body = hook, Key = HookKey });
        }

        internal bool LiveHook(Player player) => Hook != null && Hook.active && (int)Hook.key == HookKey && Hook.owner == player.whoAmI;

        internal bool ReachedHookAnchor(Player player) => LiveHook(player) && Hook.ai[0] == 2f &&
            Vector2.DistanceSquared(Hook.Center, Target) <= 24f * 24f;

        internal void ObserveGrapple(Player player)
        {
            // Player.Update clears grapCount and grappling[] at its tail so
            // the next projectile pass can rebuild them. Lease/key + actual
            // attached projectile state survives that native scratch reset.
            Observed.GrappleAttached = false;
            Observed.GrappleInFlight = false;
            if (hooks == null) return;
            for (int i = hooks.Count - 1; i >= 0; i--)
            {
                var lease = hooks[i];
                var hook = lease.Body;
                if (!hook.active || hook.owner != player.whoAmI || (int)hook.key != lease.Key) { hooks.RemoveAt(i); continue; }
                Observed.GrappleAttached |= hook.ai[0] == 2f;
                Observed.GrappleInFlight |= hook.ai[0] == 0f;
            }
        }

        internal void Reset(long generation)
        {
            Observed = default(CompanionMobilitySnapshot);
            Result = CompanionMobilityResult.Idle;
            Action = CompanionMovementActivation.None;
            Generation = generation;
            Sequence = 0;
            PositionRevision++;
            NextSourceScan = 0;
            Source = null;
            SourceItems = null;
            SourceSlot = PreviousSelection = -1;
            DoorNeedsClose = Prepared = HandPressed = ShapeHandExit = false;
            Hook = null;
            hooks?.Clear();
        }

        internal static Mount.MountData MountData(int type)
        {
            var data = mounts();
            return data != null && type >= 0 && type < data.Length ? data[type] : null;
        }

        internal static Vector2 MountSize(Player body, Vector2 baseSize, Mount.MountData data)
        {
            var projection = data.delegations.PlayerSize;
            return projection != null && projection(body, out var size) && size.HasValue ?
                size.Value : baseSize + new Vector2(0, data.heightBoost);
        }

        internal void ObserveWingRunInputs(Player body)
        {
            // These are this actor's native inputs before WingAirLogicTweaks
            // applies the selected mode. The tail alone only exposes the mode
            // that ran and would give the wrong acceleration on a transition.
            WingRunSampled = true;
            wingBaseSpeed = body.accRunSpeed;
            wingBaseAcceleration = body.runAcceleration;
            wingSampledHover = body.TryingToHoverDown && body.controlJump && body.wingTime > 0 && !body.isPerformingJump_DownDash;
        }

        private void ObserveWingModes(Player body)
        {
            int wings = body.wingsLogic;
            Observed.Wings = wings;
            Observed.WingTime = body.wingTime;
            Observed.WingTimeMax = body.wingTimeMax;
            Observed.UnlimitedWings = body.empressBrooch && wings > 0;
            Observed.WingAbilities = wings > 0 && body.CanUseWingAbilities && body.mount.CanUseWings;
            // The jump-reset list also mentions 29 and 32. Only the actual
            // native vertical clamp grants hover, independent of WingStats.
            Observed.WingHover = wings == 22 || wings == 28 || wings == 30 || wings == 31 ||
                wings == 33 || wings == 35 || wings == 37 || wings == 45;
            Observed.WingHovering = Observed.WingHover && Observed.WingAbilities && WingRunSampled && wingSampledHover;
            Observed.WingFastAscent = wings == 4 || wings == 44 || wings == 45;
            Observed.WingHoverRun = false;
            if (!Observed.WingAbilities || ArmorIDs.Wing.Sets.Stats == null) { Observed.WingSpeed = 0; return; }

            var stats = body.GetWingStats(wings);
            float ordinary = stats.AccRunAccelerationMult;
            float active = wingSampledHover && stats.HasDownHoverStats ? stats.DownHoverAccelerationMult : ordinary;
            float acceleration = WingRunSampled && active > 0 ? body.runAcceleration / active :
                WingRunSampled ? wingBaseAcceleration : body.runAcceleration;
            float speed = WingRunSampled ? wingBaseSpeed : body.accRunSpeed;
            Observed.WingBaseSpeed = speed;
            Observed.WingBaseAcceleration = acceleration;
            var wingBrake = wings == 45 && body.timeSinceLastDashStarted >= 60 ? 6f : 1f;
            Observed.WingBaseSlowdown = WingRunSampled ? body.runSlowdown / wingBrake : body.runSlowdown;
            Observed.WingSpeed = System.Math.Max(speed, stats.AccRunSpeedOverride);
            Observed.WingAcceleration = acceleration * ordinary;
            Observed.WingHoverRun = stats.HasDownHoverStats;
            Observed.WingHoverSpeed = stats.DownHoverSpeedOverride >= 0 ? stats.DownHoverSpeedOverride : speed;
            Observed.WingHoverAcceleration = acceleration * stats.DownHoverAccelerationMult;
            // 45's fast brake is native state, not a property of Down-hover.
            // When grounded the wing modifier has not run on this observation.
            Observed.WingSlowdown = Observed.WingBaseSlowdown * wingBrake;
        }

        internal void ObserveBody(Player body, bool busy)
        {
            // Player.jumpSpeed/Height belong to the body whose Update just ran.
            // They are native scratch cells shared by the next player update.
            var mount = body.mount;
            int type = mount.Active ? mount.Type : -1;
            Observed.Ready = true;
            Observed.Width = body.width;
            Observed.Height = body.height;
            Observed.GravityDirection = body.gravDir;
            Observed.Gravity = body.gravity;
            Observed.Speed = body.accRunSpeed;
            Observed.RunAcceleration = body.runAcceleration;
            Observed.RunSlowdown = body.runSlowdown;
            Observed.MaxFallSpeed = body.maxFallSpeed;
            Observed.JumpSpeed = Player.jumpSpeed;
            Observed.JumpHeight = Player.jumpHeight;
            Observed.JumpTime = body.jump;
            Observed.ReleaseJump = body.releaseJump;
            Observed.ExtraJumps = mount.Active && mount.BlockExtraJumps ? 0 : SmartFallCapabilities.UnusedJumps(body);
            Observed.WallSide = body.slideDir;
            Observed.WallGrip = body.spikedBoots;
            Observed.Sliding = body.sliding;
            Observed.DashType = CompanionDashMotion.Source(body);
            Observed.DashDelay = body.dashDelay;
            Observed.DashReady = Observed.DashType > 0 && body.dashDelay == 0 && !body.controlDash && !body.CCed;
            Observed.DashKind = body.dash;
            Observed.DashActive = body.dashDelay < 0 || body.dash == 2 && body.eocDash > 0;
            ObserveWingModes(body);
            Observed.RocketBoots = body.rocketBoots;
            Observed.RocketTime = body.rocketTime;
            Observed.RocketDelay = body.rocketDelay;
            Observed.RocketAvailable = body.rocketBoots > 0 && body.CanUseBootFlyingAbilities &&
                (body.rocketDelay > 0 || body.rocketTime > 0 || body.empressBrooch);
            Observed.RocketReady = Observed.RocketAvailable && (body.rocketDelay > 0 || body.canRocket && body.rocketRelease);
            ObserveGrapple(body);
            Observed.Carpet = body.carpet;
            Observed.CarpetAvailable = body.carpet && body.canCarpet;
            Observed.CarpetTime = body.carpetTime;
            // Grounded readiness only promises a future start. Consumption
            // also waits for earlier native jump and flight phases to finish.
            Observed.CarpetReady = body.carpet && !mount.Active && !Observed.GrappleAttached &&
                Observed.ExtraJumps == 0 && body.jump == 0 && body.velocity.Y != 0f && body.rocketTime == 0 &&
                body.wingTime == 0f && (Observed.CarpetAvailable || body.carpetTime > 0);
            Observed.MountType = type;
            Observed.MountFly = mount.CanFly(body);
            Observed.MountHover = mount.CanHover();
            Observed.UnlimitedMountHover = Observed.MountHover && unlimitedHover(mount);
            Observed.MountUseWings = !mount.Active || mount.CanUseWings;
            Observed.MountCart = mount.Cart;
            Observed.MountFlightTime = mount.Active ? flightTime(mount) : 0;
            Observed.MountFatigue = mount.Active ? fatigue(mount) : 0f;
            Observed.MountFatigueMax = mount.Active ? fatigueMax(mount) : 0f;
            Observed.Track = body.onTrack;
            Observed.CanChangeGravity = !mount.Active && body.forcedGravity == 0 && (body.gravControl || body.gravControl2);
            Observed.Pulley = body.pulley;
            Observed.Wet = body.wet;
            Observed.Lava = body.lavaWet;
            Observed.Honey = body.honeyWet;
            Observed.Shimmer = body.shimmerWet || body.shimmering;
            Observed.CanSwim = body.accFlipper || body.merman;
            Observed.MountSwim = body.wet && Observed.MountHover;
            Observed.BreathProtected = body.gills || body.merman || type == MountID.Turtle;
            Observed.LavaImmune = body.lavaImmune;
            Observed.LavaTime = body.lavaTime;
            Observed.Breath = body.breath;
            Observed.BreathMax = body.breathMax;
            Observed.Controlled = body.CCed || body.tongued || body.shimmering || body.isLockedToATile;
            Observed.HandBusy = busy || body.noItems || body.cursed;
        }
    }
}
