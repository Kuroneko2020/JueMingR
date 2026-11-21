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

    internal struct CompanionMobilitySnapshot
    {
        internal bool Ready, ReleaseJump, Sliding, DashReady, UnlimitedWings, WingHover, RocketReady, RocketAvailable,
            Carpet, CarpetReady, MountFly, MountHover, MountUseWings, MountCart, Track, CanChangeGravity,
            Pulley, GrappleAttached, GrappleInFlight, Wet, Lava, Honey, Shimmer, CanSwim, MountSwim, BreathProtected, LavaImmune,
            Controlled, HandBusy, CanUseRod, CanUseCart, DefaultMountFly, DefaultMountHover, CanDismount, UnlimitedMountHover;
        internal int Width, Height, JumpHeight, JumpTime, ExtraJumps, WallSide, WallGrip, DashType, DashDelay,
            Wings, WingTimeMax, RocketBoots, RocketTime, RocketDelay, CarpetTime, MountType, MountFlightTime,
            HookType, Breath, BreathMax, DefaultMountType, DefaultMountSlot, FlightMountType, FlightMountSlot,
            WaterMountType, WaterMountSlot, NarrowMountType, NarrowMountSlot, NarrowMountWidth, NarrowMountHeight, RodSlot, LavaTime;
        internal float GravityDirection, Gravity, Speed, RunAcceleration, RunSlowdown, MaxFallSpeed,
            JumpSpeed, WingTime, MountFatigue, MountFatigueMax, HookRange, FlightMountBudget, WaterMountSpeed;
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
        internal Vector2 Target, Aim, DoorPoint, ExpectedExit;
        internal uint Started, NextSourceScan;
        internal long SourceGestureRevision;
        internal bool SourceChaos;
        internal bool DoorNeedsClose, Prepared, HandPressed;
        internal Item Source;
        internal Item[] SourceItems;
        internal int SourceSlot = -1, PreviousSelection = -1, SourceType, SourceStack;
        internal byte SourcePrefix;
        internal Projectile Hook;
        internal int HookKey;
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
            DoorNeedsClose = Prepared = HandPressed = false;
            Hook = null;
            hooks?.Clear();
        }

        internal static Mount.MountData MountData(int type)
        {
            var data = mounts();
            return data != null && type >= 0 && type < data.Length ? data[type] : null;
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
            Observed.DashType = type >= 0 && MountID.Sets.CanDash[type] ? 6 : body.dashType;
            Observed.DashDelay = body.dashDelay;
            Observed.DashReady = Observed.DashType > 0 && body.dashDelay == 0 && !body.controlDash;
            Observed.Wings = body.wingsLogic;
            Observed.WingTime = body.wingTime;
            Observed.WingTimeMax = body.wingTimeMax;
            Observed.UnlimitedWings = body.empressBrooch && body.wingsLogic > 0;
            switch (body.wingsLogic)
            {
                case 22: case 28: case 29: case 30: case 31: case 32: case 33: case 35: case 37: case 45:
                    Observed.WingHover = true;
                    break;
                default:
                    Observed.WingHover = false;
                    break;
            }
            Observed.RocketBoots = body.rocketBoots;
            Observed.RocketTime = body.rocketTime;
            Observed.RocketDelay = body.rocketDelay;
            Observed.RocketAvailable = body.rocketBoots > 0 && body.CanUseBootFlyingAbilities &&
                (body.rocketDelay > 0 || body.rocketTime > 0 || body.empressBrooch);
            Observed.RocketReady = Observed.RocketAvailable && (body.rocketDelay > 0 || body.canRocket && body.rocketRelease);
            Observed.Carpet = body.carpet;
            Observed.CarpetTime = body.carpetTime;
            Observed.CarpetReady = body.carpet && (body.gravDir > 0 && body.canCarpet || body.carpetTime > 0);
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
            ObserveGrapple(body);
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
