using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionMotionProfile
    {
        internal static bool Wings(CompanionMobilitySnapshot state) =>
            state.WingAbilities && (state.WingTime > 0 || state.UnlimitedWings);

        internal static bool MountFlight(CompanionMobilitySnapshot state) =>
            state.MountFly && state.MountFlightTime > 0 ||
            state.MountHover && (state.UnlimitedMountHover || state.MountFatigue < state.MountFatigueMax);

        internal static bool CanFly(CompanionMobilitySnapshot state) => Wings(state) || state.RocketAvailable ||
            state.CarpetReady || MountFlight(state);

        internal static NavigationCapabilities Read(Player body, CompanionMobilitySnapshot state, bool supported)
        {
            float gravity = Math.Max(.05f, body.gravity);
            float speed = Math.Max(1f, body.maxRunSpeed);
            float jump = Math.Max(3f, state.JumpSpeed);
            // Nominal envelopes only. The runtime refiner uses actual velocity,
            // the released input edge and native contacts before launch.
            float height = jump * state.JumpHeight + jump * jump / (2f * gravity);
            // During the held portion the original jump gains extra height;
            // its return from that apex takes longer than the rise after key
            // release. A landing may also begin with positive toe overlap.
            float range = speed * (state.JumpHeight + jump / gravity + (float)Math.Sqrt(2f * height / gravity)) + body.width * .5f;
            if (state.ExtraJumps > 0) height += jump * jump / gravity;
            var modes = NavigationModes.Rope | NavigationModes.Swim | NavigationModes.Facility;
            if (!body.mount.Active && !state.Wet && body.gravDir > 0 &&
                body.width == Player.defaultWidth && body.height == Player.defaultHeight && !state.GrappleAttached)
                modes |= NavigationModes.Hoik;
            if (state.CanUseCart || state.MountCart) modes |= NavigationModes.Track;
            if (state.WingAbilities) modes |= NavigationModes.Wing;
            if (state.RocketBoots > 0) modes |= NavigationModes.Rocket;
            if (state.Carpet) modes |= NavigationModes.Carpet;
            if (state.WingHover || state.MountHover) modes |= NavigationModes.Hover;
            if (state.HookRange > 0 || state.GrappleAttached) modes |= NavigationModes.Hook;
            if (state.DashType > 0) modes |= NavigationModes.Dash;
            if (state.CanChangeGravity) modes |= NavigationModes.Gravity;
            if (state.LavaImmune || state.LavaTime > 24) modes |= NavigationModes.LavaImmune;
            if (body.waterWalk || body.waterWalk2) modes |= NavigationModes.WaterWalk;
            if (state.MountType >= 0) modes |= NavigationModes.Mount | NavigationModes.GroundMount;
            if (MountFlight(state)) modes |= NavigationModes.FlightMount;
            if (state.MountSwim) modes |= NavigationModes.WaterMount;
            if (state.MountChoices != null)
                foreach (var choice in state.MountChoices)
                {
                    modes |= NavigationModes.Mount | NavigationModes.GroundMount;
                    if (choice.CanFly) modes |= NavigationModes.FlightMount;
                    if (choice.WaterSpeed > 0) modes |= NavigationModes.WaterMount;
                    if (choice.Compact) modes |= NavigationModes.NarrowMount;
                }
            int bootTicks = state.RocketAvailable ? (state.RocketTime + 1) * Math.Max(8, state.RocketDelay) : 0;
            int bootCapacity = state.RocketBoots > 0 ? Math.Max(96, bootTicks) : 0;
            const int unboundedEnvelope = 10000;
            int wing = state.UnlimitedWings ? unboundedEnvelope : Wings(state) ? (int)state.WingTime : 0;
            int wingCapacity = state.UnlimitedWings ? unboundedEnvelope : state.WingTimeMax;
            int mount = state.UnlimitedMountHover ? unboundedEnvelope : state.MountFlightTime +
                (int)Math.Max(0, state.MountFatigueMax - state.MountFatigue);
            int mountCapacity = state.UnlimitedMountHover ? unboundedEnvelope : (int)Math.Max(
                Math.Max(mount, state.MountFatigueMax), CompanionMobilityState.MountData(state.MountType)?.flightTimeMax ?? 0);
            int carpet = state.CarpetAvailable && (supported || state.CarpetReady) ? 300 : Math.Max(0, state.CarpetTime);
            int available = Math.Max(wing + bootTicks + carpet, mount);
            int maximum = Math.Max(wingCapacity + bootCapacity + (state.Carpet ? 300 : 0), mountCapacity);
            float flightSpeed = Math.Max(2f, Math.Max(Math.Max(state.WingSpeed, state.WingHoverSpeed), body.accRunSpeed));
            var activeMount = !body.mount.Active ? NavigationTraversalDetail.None :
                body.height < body.DefaultSize.Y && body.width <= body.DefaultSize.X ? NavigationTraversalDetail.MountNarrow :
                state.MountFly || state.MountHover ? NavigationTraversalDetail.MountFlight :
                state.MountSwim ? NavigationTraversalDetail.MountWater : NavigationTraversalDetail.MountGround;
            Vector2 normal = body.DefaultSize;
            int normalHeight = (int)normal.Y;
            int cartHeight = state.MountCart ? body.height : state.CanUseCart ? MountHeight(body, normalHeight, CartType(body)) : 0;
            var geometry = new NavigationActorGeometry((int)normal.X, normalHeight, 0, 0, 0,
                0, cartHeight, RailMagnetFromFeet(body, cartHeight), body.statLife,
                CompanionFallRisk.ProtectionKey(body), state.Track, state.CarpetAvailable || state.CarpetReady);
            return new NavigationCapabilities(body.width, body.height, (int)body.gravDir, height, range,
                available, maximum, flightSpeed, state.HookRange, modes, supported, body.mount.Active,
                activeMount, state.MountFly || state.MountHover, 0).WithCondition(geometry)
                .WithMountChoices(state.MountChoices, body.mount.Active ? state.MountType : -1);
        }

        private static int MountHeight(Player body, int normal, int type)
        {
            var data = CompanionMobilityState.MountData(type);
            if (data == null) return 0;
            return (int)CompanionMobilityState.MountSize(body, new Vector2(body.DefaultSize.X, normal), data).Y;
        }

        private static int CartType(Player body)
        {
            var item = body.miscEquips[2];
            return item != null && item.stack > 0 && item.mountType >= 0 && item.mountType < MountID.Sets.Cart.Length &&
                MountID.Sets.Cart[item.mountType] && (!item.expertOnly || Main.expertMode) ? item.mountType : 13;
        }

        internal static Vector2 RailMagnetFromFeet(Player body, int height)
        {
            var settings = body.MinecartSettings;
            return new Vector2(-settings.MinecartTextureWidth * .5f, -height * .5f) + settings.MagnetOffset;
        }

        internal static float HorizontalSpeed(Player body, CompanionMobilitySnapshot state, bool airborne, bool hover)
        {
            float speed = airborne && Wings(state) ? hover && state.WingHoverRun ? state.WingHoverSpeed : state.WingSpeed :
                state.WingAbilities && state.WingBaseSpeed > 0 && !state.MountHover ? state.WingBaseSpeed : body.accRunSpeed;
            return Math.Max(body.maxRunSpeed, speed);
        }

        internal static float HorizontalAcceleration(Player body, CompanionMobilitySnapshot state, bool airborne, bool hover)
        {
            if (airborne && Wings(state)) return Math.Max(.04f, hover && state.WingHoverRun ?
                state.WingHoverAcceleration : state.WingAcceleration);
            return Math.Max(.04f, body.runAcceleration);
        }

        internal static int Steer(Player body, CompanionMobilitySnapshot state, float targetX, float targetSpeed, float tolerance,
            bool airborne, bool hover = false)
        {
            float offset = targetX - body.Center.X;
            float error = Math.Sign(offset) * Math.Max(0f, Math.Abs(offset) - tolerance);
            float velocity = body.velocity.X;
            float maximum = HorizontalSpeed(body, state, airborne, hover);
            float desired = MathHelper.Clamp(targetSpeed + error * .2f, -maximum, maximum);
            int current = Math.Sign(velocity);
            int wanted = Math.Abs(desired) >= .4f ? Math.Sign(desired) : 0;
            float slowdown = airborne && Wings(state) ? state.WingSlowdown : body.runSlowdown;
            float drag = Math.Max(.035f, airborne ? slowdown * .5f : slowdown);
            float closing = (velocity - targetSpeed) * current;
            // A moving owner can slow sooner than the follower's inertia.
            // Inside the stopping envelope give the live brake precedence
            // over the still-positive distance term. Planned connectors
            // with zero spacing retain their exact entrance controller.
            if (!airborne && tolerance > 0f && current != 0 && closing > .8f &&
                closing * closing > 2f * drag * (Math.Max(0f, offset * current - tolerance) + 5f) &&
                (targetSpeed * current <= 0f || Math.Abs(offset) <= tolerance + 24f))
                return -current;
            // A body just outside its arrival band still needs an initial
            // native stride. The moving-speed dead zone must not create a
            // second, larger band that the action can never report reached.
            if (wanted == 0 && Math.Abs(velocity) < .4f && error != 0f &&
                Math.Sign(error) == Math.Sign(desired)) return Math.Sign(error);
            if (wanted != 0 && (current != wanted || velocity * wanted < desired * wanted)) return wanted;
            float room = Math.Max(0f, offset * current) + 5f;
            return closing > .8f && closing * closing > 2f * drag * room ? -current : 0;
        }
    }
}
