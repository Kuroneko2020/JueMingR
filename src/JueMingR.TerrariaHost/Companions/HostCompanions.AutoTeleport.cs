using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const int UnpaidAutoTeleportDelay = 60;
        private const int ArrivedAutoTeleportDelay = 600;
        private const int PaidFailureAutoTeleportDelay = 900;

#if DEBUG
        internal int AutoTeleportAdmissions { get; private set; }
        internal int AutoTeleportInventoryVisits { get; private set; }
#endif

        private bool AutoTeleportFollowing(Actor a) =>
            a.Preferences.AutoTeleportEnabled && FishingFollowsOwner(a) &&
            a.Ready && !a.Closing && !a.Faulted && a.Body.active && !a.Body.dead && !a.Body.ghost &&
            !a.Profile.PermanentlyDead && !a.PermanentDeathPending && owner?.active == true && !owner.dead && !owner.ghost &&
            ReferenceEquals(owner, Main.ActivePlayerFileData?.Player) && ReferenceEquals(world, Main.ActiveWorldFileData) &&
            (Main.netMode == 0 || Main.netMode == 1 && a.Connection?.Finished == false);

        private CompanionAutoTeleport AutoTeleportObservation(Actor a)
        {
            var options = a.Preferences;
            if (!AutoTeleportFollowing(a) || !options.AutoTeleportBlockedEnabled && !options.AutoTeleportDistanceEnabled)
            {
                a.AutoTeleport?.ResetObservation();
                return null;
            }
            var observer = a.AutoTeleport ?? (a.AutoTeleport = new CompanionAutoTeleport());
            observer.Configure(options);
            return observer;
        }

        private void CaptureAutoTeleportMovement(Actor a, CompanionMovementDelivery delivery, bool temporary)
        {
            if (Main.gamePaused) return;
            var observer = AutoTeleportObservation(a);
            if (observer == null) return;
            var feet = CompanionNativeContact.Feet(a.Body);
            observer.CaptureMovement(feet, a.Behavior.Navigator.DeliveredTravelAxis(delivery, feet), temporary);
        }

        private void ObserveAutoTeleport(Actor a)
        {
            if (Main.gamePaused) return;
            var observer = AutoTeleportObservation(a);
            if (observer == null) return;
            var body = a.Body;
            bool settled = a.Preferences.AutoTeleportBlockedEnabled && a.Behavior.Navigator.WithinOwnerFollowBand(body, owner);
            observer.Observe(Main.GameUpdateCount, owner.Center, body.Center, CompanionNativeContact.Feet(body), settled,
                body.CCed || a.Mobility.Observed.Controlled);
        }

        private void ConsumeAutoTeleport(Actor a)
        {
            if (Main.gamePaused || !Main.CanUpdateGameplay || Main.GameUpdateCount - a.AutoTeleportDelayedAt < a.AutoTeleportDelay) return;
            var observer = AutoTeleportObservation(a);
            // The handoff can cross the normal update boundary, but a
            // stopped native body or stale candidate can't spend inventory.
            if (observer?.HasCandidate != true || Main.GameUpdateCount - observer.ObservedAt > 1) return;
            if (wormholeWindows.ContainsKey(a) || RecalledThisUpdate(a)) return;
            bool settled = a.Preferences.AutoTeleportBlockedEnabled && a.Behavior.Navigator.WithinOwnerFollowBand(a.Body, owner);
            if (!observer.StillWants(owner.Center, a.Body.Center, settled)) return;
#if DEBUG
            AutoTeleportAdmissions++;
#endif
            // Prepare/commit share the real wormhole slot lease and late
            // activation checks. A failed admission earns only a short retry.
            DelayAutoTeleport(a, UnpaidAutoTeleportDelay);
            TryRecall(a.Profile.Id, automatic: true);
        }

        private static void DelayAutoTeleport(Actor actor, uint ticks)
        {
            // Terraria advances this inside DoUpdateInWorld_Inner. A pause
            // spends no time here, while a real world still ages the delay
            // if the player disables follow or starts fishing.
            uint now = Main.GameUpdateCount;
            uint elapsed = now - actor.AutoTeleportDelayedAt;
            if (elapsed < actor.AutoTeleportDelay && actor.AutoTeleportDelay - elapsed >= ticks) return;
            actor.AutoTeleportDelayedAt = now;
            actor.AutoTeleportDelay = ticks;
        }

        private bool RecalledThisUpdate(Actor actor) => actor.LastRecallGeneration == actor.Generation &&
            actor.LastRecallSubmission == Main.GameUpdateCount;
    }
}
