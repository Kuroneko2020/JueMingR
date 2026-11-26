using System;
using System.Diagnostics;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionAutoTeleportChecks
    {
        private const int Slot = 44, Floor = 60, Wall = 56;

        internal static CompanionBehavior Preferences(bool enabled = true, bool blocked = true, bool distance = false) =>
            new CompanionBehavior
            {
                Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false,
                AutoTeleportEnabled = enabled, AutoTeleportBlockedEnabled = blocked, AutoTeleportBlockedSeconds = 1,
                AutoTeleportDistanceEnabled = distance, AutoTeleportDistanceTiles = 5
            };

        internal static void Run(object host, Guid first, Guid second, Player human, Player body, Player other)
        {
            var actor = Call(host, "Owned", body);
            var otherActor = Call(host, "Owned", other);
            var saved = ((CompanionProfile)Get(actor, "Profile")).Behavior;
            var savedOther = ((CompanionProfile)Get(otherActor, "Profile")).Behavior;
            var originalTiles = Main.tile;
            var previousPause = Main.gamePaused;
            try
            {
                Main.gamePaused = false;
                // Local() ended after a paid, native partial failure. Advance
                // the real simulation clock through its full known delay.
                for (int i = 0; i < 901; i++) NativeQuickItemChecks.BeginWorldStep();
                LocalPassages(host);
                Main.tile = (Tile[,])originalTiles.Clone();
                for (int x = 42; x < 118; x++) Tile(x, Floor, true);
                for (int x = 48; x < Wall; x++) Tile(x, Floor - 4, true);
                for (int y = Floor - 3; y < Floor; y++) Tile(48, y, true);
                WallTiles(true);
                human.Center = new Vector2(68 * 16, Floor * 16 - human.height / 2);
                Place(body, actor, Wall * 16 - 11);
                Place(other, otherActor, 51 * 16);
                body.inventory[Slot] = new Item();
                other.inventory[Slot] = new Item();
                human.inventory[Slot] = Bottle(2);

                int admissions = Counter(host, "AutoTeleportAdmissions");
                SetBehavior(host, first, Preferences(enabled: false));
                for (int i = 0; i < 75; i++) Step(host, human, body, i);
                Require(GetOptional(actor, "AutoTeleport") == null && Counter(host, "AutoTeleportAdmissions") == admissions &&
                    human.inventory[Slot].stack == 2, "closed automatic setting has no observer or payer visit");

                SetBehavior(host, first, Preferences(blocked: false));
                for (int i = 0; i < 135; i++) Step(host, human, body, i);
                Require(GetOptional(actor, "AutoTeleport") == null && human.inventory[Slot].stack == 2,
                    "both conditions can be unticked without attempting the original action");

                human.inventory[Slot] = new Item();
                SetBehavior(host, first, Preferences());
                var nav = Get(Get(actor, "Behavior"), "Navigator");
                long observedCpu = 0;
                int emptyVisits = Counter(host, "AutoTeleportInventoryVisits");
                int loops = 0;
                for (; loops < 185; loops++)
                {
                    long now = Stopwatch.GetTimestamp();
                    Step(host, human, body, loops);
                    observedCpu += Stopwatch.GetTimestamp() - now;
                    if (GetOptional(host, "AutoTeleportAdmissions") != null && Counter(host, "AutoTeleportAdmissions") >= admissions + 2)
                        break;
                }
                int emptyAdmissions = Counter(host, "AutoTeleportAdmissions") - admissions;
                int inventoryVisits = Counter(host, "AutoTeleportInventoryVisits") - emptyVisits;
                Require((bool)Get(Get(actor, "AutoTeleport"), "HasCandidate") && body.Right.X <= Wall * 16 + 1 &&
                    !(bool)Get(actor, "Faulted") && (GetOptional(host, "AutoTeleportAdmissions") == null ||
                        loops >= 119 && emptyAdmissions == 2 && inventoryVisits == 2 * CompanionBehavior.MainInventorySlots * 2),
                    $"native wall stop meets one simulated second while a moving owner stays outside, with bounded empty-slot probes: ticks={loops} tries={emptyAdmissions} slots={inventoryVisits} body={body.Bottom}");
                Console.WriteLine($"AUTO TELEPORT empty ticks={loops + 1} admissions={emptyAdmissions} inventoryVisits={inventoryVisits} " +
                    $"samples={Counter(Get(actor, "AutoTeleport"), "Samples")} passageComparisons={Counter(Get(actor, "AutoTeleport"), "PassageComparisons")} query={Get(nav, "QueryStatus")}/{Get(nav, "QueryGeneration")} bodyAndHostMs={1000d * observedCpu / Stopwatch.Frequency:F2}");

                WallTiles(false);
                // Put the moving owner farther along the genuinely opened
                // floor, so the AI starts walking well before it can settle.
                human.Bottom = new Vector2(77 * 16, Floor * 16);
                var start = body.Bottom;
                human.inventory[Slot] = Bottle(2);
                for (int i = 0; i < 75; i++) Step(host, human, body, i);
                Require(body.Bottom.X > start.X + 52 && human.inventory[Slot].stack == 2 &&
                    !(bool)Get(Get(actor, "AutoTeleport"), "HasCandidate"),
                    $"after a due empty attempt, actual restored floor travel retires the candidate before the restocked bottle can be charged: body={body.Bottom} start={start}");

                SetBehavior(host, first, Preferences(enabled: false));
                WallTiles(true);
                Place(body, actor, Wall * 16 - 11);
                Place(other, otherActor, Wall * 16 - 11);
                SetBehavior(host, first, Preferences(distance: true));
                SetBehavior(host, second, Preferences(distance: true));
                human.inventory[Slot] = Bottle(1);
                for (int i = 0; i < 58; i++) Step(host, human, body, i, other);
                Require(human.inventory[Slot].stack == 1 && body.Right.X <= Wall * 16 + 1,
                    "continuous blocked time cannot spend before the configured 60 updates");
                Main.gamePaused = true;
                var paused = Main.GameUpdateCount;
                for (int i = 0; i < 80; i++) Call(host, "Update", (ulong)Main.GameUpdateCount);
                Require(Main.GameUpdateCount == paused && human.inventory[Slot].stack == 1,
                    "outer updates on a genuinely unadvanced paused clock leave the candidate unpaid");
                Main.gamePaused = false;
                Step(host, human, body, 58, other);
                NativeQuickItemChecks.BeginWorldStep();
                body.Update(body.whoAmI);
                other.Update(other.whoAmI);
                Require((bool)Get(Get(actor, "AutoTeleport"), "HasCandidate") &&
                    (bool)Get(Get(otherActor, "AutoTeleport"), "HasCandidate") && human.inventory[Slot].stack == 1,
                    "both actual bodies hand off an unpaid candidate when the shorter blocked branch of OR reaches 60 updates");
                Call(host, "Update", (ulong)Main.GameUpdateCount);
                bool firstArrived = body.teleportStyle == 3 && body.Bottom == human.Bottom;
                bool secondArrived = other.teleportStyle == 3 && other.Bottom == human.Bottom;
                Require(human.inventory[Slot].IsAir && firstArrived != secondArrived,
                    "one blocked trigger wins the OR before the two-second distance edge and only one AI gets the shared last bottle");
                Guid arrived = firstArrived ? first : second;
                var landed = firstArrived ? body : other;
                var landedActor = firstArrived ? actor : otherActor;
                human.position.X += 48f;
                human.inventory[Slot] = Bottle(2);
                Require(!(bool)Call(host, "TryManualRecall", arrived) && human.inventory[Slot].stack == 2 &&
                    (uint)Get(landedActor, "AutoTeleportDelay") >= 600 && landed.Bottom != human.Bottom,
                    "manual activation later in the same update cannot consume again after synchronous automatic arrival");
                NativeQuickItemChecks.BeginWorldStep();
                Require((bool)Call(host, "TryManualRecall", arrived) && human.inventory[Slot].stack == 1 && landed.Bottom == human.Bottom,
                    "a new explicit manual activation remains allowed during the automatic cooldown");

                SetBehavior(host, first, Preferences(enabled: false));
                SetBehavior(host, second, Preferences(blocked: false, distance: true));
                for (int i = 0; i < 601; i++) NativeQuickItemChecks.BeginWorldStep();
                Place(other, otherActor, 51 * 16);
                var otherProfile = (CompanionProfile)Get(otherActor, "Profile");
                Require((bool)Call(host, "SetFishing", otherProfile.OwnerId, second, true), "actual fishing activity starts while Follow remains persisted");
                for (int i = 0; i < 130; i++) Step(host, human, other, i);
                Require(human.inventory[Slot].stack == 1 && (bool)Call(host, "IsFishing", otherProfile.OwnerId, second),
                    "a distance-only timer cannot run or teleport a body actively fishing");
                Require((bool)Call(host, "SetFishing", otherProfile.OwnerId, second, false), "actual fishing activity exits back to follow");
                Place(other, otherActor, 51 * 16);
                for (int i = 0; i < 119; i++) Step(host, human, other, i);
                Require(human.inventory[Slot].stack == 1, "distance-only requires a fresh full two seconds after fishing");
                Step(host, human, other, 119);
                Require(human.inventory[Slot].IsAir && other.Bottom == human.Bottom, "distance-only performs the same original arrival and debit at 120 completed updates");

                for (int i = 0; i < 601; i++) NativeQuickItemChecks.BeginWorldStep();
                SetBehavior(host, first, Preferences(blocked: false, distance: true));
                Place(body, actor, Wall * 16 - 11);
                body.inventory[Slot] = Bottle(2);
                for (int i = 0; i < 119; i++) Step(host, human, body, i);
                NativeQuickItemChecks.BeginWorldStep();
                body.Update(body.whoAmI);
                Require((bool)Get(Get(actor, "AutoTeleport"), "HasCandidate") && (bool)Call(host, "TryManualRecall", first),
                    "a manual entry can consume an unpaid due candidate before outer Host submission");
                human.position.X += 48f;
                Call(host, "Update", (ulong)Main.GameUpdateCount);
                Require(body.inventory[Slot].stack == 1 && body.Bottom != human.Bottom, "an earlier manual submission prevents automatic second payment");
                Console.WriteLine("PASS automatic original movement and Host: total off, no conditions, physical wall stall, restocked recovery, pause, OR, final bottle race, both same-update submission orders, and active fishing versus distance-only follow.");
            }
            finally
            {
                SetBehavior(host, first, saved);
                SetBehavior(host, second, savedOther);
                Main.tile = originalTiles;
                Main.gamePaused = previousPause;
                NativeQuickItemChecks.BeginWorldStep();
            }
        }

        private static void LocalPassages(object host)
        {
            var stateType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionAutoTeleport", true);
            var trapped = Activator.CreateInstance(stateType, true);
            var returning = Activator.CreateInstance(stateType, true);
            var looping = Activator.CreateInstance(stateType, true);
            foreach (var state in new[] { trapped, returning, looping }) Call(state, "Configure", Preferences());
            Vector2 previous = new Vector2(800, 700), previousReturn = previous;
            for (uint tick = 1; tick <= 120; tick++)
            {
                // Same small piece of real space revisited, with every
                // observation nominally delivered a replacement local leg.
                var current = new Vector2(tick % 2 == 0 ? 800 : 816, 700);
                var direction = Vector2.Normalize(current - previous);
                Call(trapped, "CaptureMovement", previous, direction, false);
                Call(trapped, "Observe", tick, new Vector2(1040 + tick, 690), current, current, false, false);
                float phase = tick % 12;
                var retraced = new Vector2(800 + Math.Min(phase, 12 - phase) * 16, 700);
                var returnDirection = Vector2.Normalize(retraced - previousReturn);
                Call(returning, "CaptureMovement", previousReturn, returnDirection, false);
                Call(returning, "Observe", tick, retraced + returnDirection * 50, retraced, retraced, false, false);
                previous = current;
                previousReturn = retraced;
            }
            Require((bool)Get(trapped, "HasCandidate") && !(bool)Get(returning, "HasCandidate"),
                "reissuing two nearby legs with a receding owner doesn't reset a wall cycle, while following a real reversing target can reuse it");

            previous = Orbit(0);
            for (uint tick = 1; tick <= 240; tick++)
            {
                var current = Orbit(tick);
                var leadingOwner = Orbit(tick + 4);
                Call(looping, "CaptureMovement", previous, Vector2.Normalize(current - previous), false);
                Call(looping, "Observe", tick, leadingOwner, current, current, false, false);
                Require(!(bool)Get(looping, "HasCandidate"), "following the same moving owner around a genuinely traversed loop stays useful outside the settle band");
                previous = current;
            }
            Require((int)Get(looping, "count") < 20, "the moving loop actually reuses recent physical marks instead of continually evicting them");

            Vector2 Orbit(uint tick)
            {
                double angle = (tick % 16) * Math.PI / 8;
                return new Vector2(900 + (float)Math.Cos(angle) * 32, 750 + (float)Math.Sin(angle) * 32);
            }
        }

        private static void SetBehavior(object host, Guid id, CompanionBehavior preferences) =>
            Require((bool)Call(host, "SetBehavior", id, preferences), "set real teleport preferences on current saved actor");

        private static Item Bottle(int count) => new Item(ItemID.WormholePotion) { stack = count };
        private static int Counter(object owner, string name) => (int)(GetOptional(owner, name) ?? 0);

        private static void Step(object host, Player human, Player body, int i, Player other = null)
        {
            NativeQuickItemChecks.BeginWorldStep();
            human.velocity.X = i % 80 < 40 ? .75f : -.75f;
            human.position.X += human.velocity.X;
            body.Update(body.whoAmI);
            if (other != null) other.Update(other.whoAmI);
            Call(host, "Update", (ulong)Main.GameUpdateCount);
        }

        private static void Place(Player body, object actor, float x)
        {
            body.Bottom = new Vector2(x, Floor * 16);
            body.velocity = Vector2.Zero;
            body.ResetFallDamage();
            Call(Get(actor, "Behavior"), "PositionChanged", body);
        }

        private static void WallTiles(bool solid)
        {
            for (int y = Floor - 3; y < Floor; y++) Tile(Wall, y, solid);
        }

        private static void Tile(int x, int y, bool solid)
        {
            var tile = new Tile { type = TileID.Stone };
            tile.active(solid);
            Main.tile[x, y] = tile;
        }
    }
}
