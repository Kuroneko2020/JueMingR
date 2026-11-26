using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFishingOpeningChecks
    {
        internal static void Run(object context, object host, object input, Player actor, Player other,
            CompanionProfile profile, CompanionProfile otherProfile)
        {
            var owner = Main.LocalPlayer;
            var savedSeats = Main.sittingManager;
            var savedOwnerPosition = owner.position;
            var savedOwnerDirection = owner.direction;
            var savedTiles = Main.tile;
            var savedMap = Main.Map;
            int savedWidth = Main.maxTilesX;
            float savedLeft = Main.leftWorld, savedTop = Main.topWorld, savedRight = Main.rightWorld, savedBottom = Main.bottomWorld;
            Main.sittingManager = new AnchoredEntitiesCollection();
            bool Toggle(Guid ownerId, Guid id, bool active) => (bool)Call(host, "SetFishing", ownerId, id, active);
            void Step()
            {
                Main.sittingManager.ClearPlayerAnchors();
                owner.sitting.UpdateSitting(owner);
                NativeCompanionFishingLineChecks.Step(context, input, actor, other);
            }

            try
            {
                Require(Toggle(otherProfile.OwnerId, otherProfile.Id, false), "finish other role's original test cast");
                for (int frame = 0; frame < 120 && NativeCompanionFishingLineChecks.OwnedBobber(other) != null; frame++) Step();
                Require(NativeCompanionFishingLineChecks.OwnedBobber(other) == null, "prior native line retires before the terrain fixture changes");
                // Original BordersMovement reserves 640 pixels on both
                // sides. A real landing at X=1480 needs a wider tile world
                // than the earlier 120-tile inventory/rod fixture.
                Main.maxTilesX = 160;
                Main.tile = new Tile[Main.maxTilesX, Main.maxTilesY];
                Main.Map = new Terraria.Map.WorldMap(Main.maxTilesX, Main.maxTilesY);
                for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < Main.maxTilesY; y++) Main.tile[x, y] = new Tile();
                Main.leftWorld = Main.topWorld = 0;
                Main.rightWorld = Main.maxTilesX * 16;
                Main.bottomWorld = Main.maxTilesY * 16;
                for (int x = 37; x < 55; x++) NativeToolsChecks.Tile(x, 48, TileID.Stone);
                for (int y = 46; y <= 74; y++) NativeToolsChecks.Tile(55, y, TileID.Stone);
                for (int x = 59; x <= 61; x++) for (int y = 52; y < 79; y++) Main.tile[x, y].liquid = 255;
                for (int x = 64; x <= 69; x++) for (int y = 53; y < 71; y++) Main.tile[x, y].liquid = 255;
                for (int x = 87; x <= 94; x++) NativeToolsChecks.Tile(x, 48, TileID.Stone);
                for (int x = 96; x <= 103; x++) for (int y = 51; y < 65; y++) Main.tile[x, y].liquid = 255;

                var top = Main.tile[50, 46];
                var bottom = Main.tile[50, 47];
                top.active(true); top.type = TileID.Chairs; top.frameX = 18;
                bottom.active(true); bottom.type = TileID.Chairs; bottom.frameX = 18; bottom.frameY = 18;
                Require(PlayerSittingHelper.GetSittingTargetInfo(owner, 50, 47, out _, out Vector2 seat, out _, out _),
                    "fixture uses a real east-facing chair at the edge of the bank");
                owner.Bottom = seat + new Vector2(-8, 0);
                owner.velocity = Vector2.Zero;
                owner.controlLeft = owner.controlRight = owner.controlUp = owner.controlDown = owner.controlJump = false;
                owner.sitting.SitDown(owner, 50, 47);
                Require(owner.sitting.isSitting, "owner really occupies the chair before the mode is enabled");
                actor.Bottom = seat + new Vector2(-72, 0);
                actor.velocity = Vector2.Zero;
                other.Bottom = seat + new Vector2(-130, 0);
                other.velocity = Vector2.Zero;
                var role = Call(host, "Owned", actor);
                Require(Toggle(profile.OwnerId, profile.Id, true), "normal fishing command captures the actual occupied chair");
                object activity = Get(role, "Fishing");
                for (int frame = 0; frame < 180 && actor.chatOverhead.chatText != "椅子让一下，我要坐"; frame++) Step();
                Require(actor.chatOverhead.chatText == "椅子让一下，我要坐" && owner.sitting.isSitting && !actor.sitting.isSitting &&
                    NativeCompanionFishingLineChecks.OwnedBobber(actor) == null,
                    "fishing actor navigates to an occupied chair, asks from its own identity, and waits: " + Get(role, "Status"));
                owner.sitting.SitUp(owner, multiplayerBroadcast: false);
                owner.Bottom = new Vector2(92 * 16 + 8, 48 * 16);

                Projectile first = null;
                for (int frame = 0; frame < 320 && (first == null || !(bool)Get(activity, "Reported")); frame++)
                {
                    Step();
                    first = NativeCompanionFishingLineChecks.WetBobber(actor);
                }
                Require(actor.sitting.isSitting && actor.Bottom == seat && ReferenceEquals(Get(role, "Fishing"), activity),
                    "the actor takes the original native chair after the owner walks away: " + Get(role, "Status"));
                Require(first != null && first.Center.X >= 59 * 16 && first.Center.X < 62 * 16 && (bool)Get(activity, "Reported") &&
                    actor.chatOverhead.chatText.EndsWith("正在水中钓鱼。", StringComparison.Ordinal),
                    "real rod use crosses above the blocking wall, enters its selected pool and reports from the seated actor: " + Get(role, "Status"));

                int firstKey = (int)first.key;
                NativeCompanionFishingLineChecks.DefineBite(first, ItemID.Bass);
                Projectile next = null;
                for (int frame = 0; frame < 220; frame++)
                {
                    Step();
                    next = NativeCompanionFishingLineChecks.WetBobber(actor);
                    if (next != null && (int)next.key != firstKey) break;
                }
                Require(next != null && (int)next.key != firstKey && actor.sitting.isSitting && actor.Bottom == seat,
                    "native pull and replacement cast leave the seated actor on the fixed chair");
                Console.WriteLine("PASS AI fishing occupied chair, actor chat, original seat after owner moves, actual cast over stone into water, and seated recast.");

                Vector2 originalCircle = (Vector2)Get(Get(activity, "Water"), "Center");
                int previousKey = (int)next.key;
                var cast = Get(activity, "Cast");
                for (int x = 59; x <= 61; x++) for (int y = 75; y < 79; y++) Main.tile[x, y].liquid = 0;
                Func<int, int, Tile> read = (x, y) => Main.tile[x, y];
                Require((bool)Call(cast, "TerrainCurrent", read), "the edit is below the unchanged waterline and outside the successful cast's terrain reads");
                Projectile movedPool = null;
                for (int frame = 0; frame < 480; frame++)
                {
                    Step();
                    movedPool = NativeCompanionFishingLineChecks.WetBobber(actor);
                    if (movedPool != null && (int)movedPool.key != previousKey && movedPool.Center.X >= 64 * 16) break;
                }
                Require(movedPool != null && (int)movedPool.key != previousKey && movedPool.Center.X >= 64 * 16 && movedPool.Center.X < 70 * 16 &&
                    (Vector2)Get(Get(activity, "Water"), "Center") == originalCircle && actor.sitting.isSitting && actor.Bottom == seat,
                    "pure deep drainage retires the old line and reaches the next real pond from the same chair and circle: " + Get(role, "Status"));
                Console.WriteLine("PASS deep drain outside the 20-tile circle forces native re-cast into the next qualified water body.");

                int removedSeatKey = (int)movedPool.key;
                top.ClearEverything();
                bottom.ClearEverything();
                for (int frame = 0; frame < 180 && movedPool.active && (int)movedPool.key == removedSeatKey; frame++) Step();
                Require((!movedPool.active || (int)movedPool.key != removedSeatKey) && !actor.sitting.isSitting &&
                    GetOptional(activity, "Seat") == null && ReferenceEquals(Get(role, "Fishing"), activity),
                    "removing the occupied original chair retires its real bobber and keeps the same fishing activity: " + Get(role, "Status"));
                Console.WriteLine("PASS actual chair removal retires the old native bobber without ending fishing.");

                var settings = ((CompanionProfile)Get(role, "Profile")).Behavior;
                settings.TeleportStopsFishing = false;
                Require((bool)Call(host, "SetBehavior", profile.Id, settings), "retain-fishing recall preference saved through its normal entry");
                actor.inventory[38].SetDefaults(ItemID.WormholePotion); actor.inventory[38].stack = 2;
                Require((bool)Call(host, "TryManualRecall", profile.Id), "real paid recall moves the fishing actor");
                object relocated = Get(role, "Fishing");
                Require(!ReferenceEquals(relocated, activity) && (Vector2)Get(relocated, "RequestedFeet") == actor.Bottom &&
                    GetOptional(relocated, "Seat") == null && actor.inventory[38].stack == 1,
                    "retaining the mode starts new fishing from the real wormhole arrival and drops the old chair");
                Projectile atLanding = null;
                for (int frame = 0; frame < 300; frame++)
                {
                    Step();
                    atLanding = NativeCompanionFishingLineChecks.WetBobber(actor);
                    if (atLanding != null && atLanding.Center.X >= 96 * 16 && (bool)Get(relocated, "Reported")) break;
                }
                Require(atLanding != null && atLanding.Center.X >= 96 * 16 && atLanding.Center.X < 104 * 16 && (bool)Get(relocated, "Reported"),
                    "retained mode searches and actually casts into the new pool outside the old fishing circle: " + Get(role, "Status") +
                    " feet=" + actor.Bottom + " velocity=" + actor.velocity + " target=" + Get(relocated, "FeetGoal") +
                    " supported=" + Get(Get(Get(role, "Behavior"), "Navigator"), "Supported") + " hold=" + Get(relocated, "Hold") +
                    " rightBoundary=" + Main.rightWorld + " wet=" + atLanding?.Center);
                VerifyStandingReturn(Step, role, actor, relocated);
                VerifyStandingDefense(Step, host, role, profile, actor, other, relocated);

                settings = ((CompanionProfile)Get(role, "Profile")).Behavior;
                settings.TeleportStopsFishing = true;
                Require((bool)Call(host, "SetBehavior", profile.Id, settings), "stop-fishing recall preference saved through its normal entry");
                owner.Bottom -= new Vector2(64, 0);
                Require((bool)Call(host, "TryManualRecall", profile.Id) && GetOptional(role, "Fishing") == null &&
                    ((CompanionProfile)Get(role, "Profile")).Behavior.Follow && actor.inventory[38].IsAir,
                    "second real paid recall stops fishing and resumes owner follow");
                Console.WriteLine("PASS AI fishing both real paid wormhole arrivals, new landing request and stop-to-follow.");

                Require(Toggle(profile.OwnerId, profile.Id, true), "start another ordinary standing activity for the death boundary");
                Projectile beforeDeath = null;
                for (int frame = 0; frame < 360 && beforeDeath == null; frame++)
                {
                    Step();
                    beforeDeath = NativeCompanionFishingLineChecks.WetBobber(actor);
                }
                Require(beforeDeath != null && GetOptional(role, "Fishing") != null, "actor has a real line before native death");
                actor.KillMe(PlayerDeathReason.ByCustomReason("fixture active AI fishing death"), 999, 0);
                Require(actor.dead && GetOptional(role, "Fishing") == null && NativeCompanionFishingLineChecks.OwnedBobber(actor) == null,
                    "real death ends the fishing activity and retires that actor's original bobber");
                for (int frame = 0; frame < 240 && actor.dead; frame++) Step();
                Require(!actor.dead && !actor.sitting.isSitting && GetOptional(role, "Fishing") == null &&
                    ((CompanionProfile)Get(role, "Profile")).Behavior.Follow,
                    "normal native revival resumes following without recreating the previous fishing activity");
                Console.WriteLine("PASS native death with wet line and native revive to follow.");
            }
            finally
            {
                if (owner.sitting.isSitting) owner.sitting.SitUp(owner, multiplayerBroadcast: false);
                Toggle(profile.OwnerId, profile.Id, false);
                owner.position = savedOwnerPosition;
                owner.direction = savedOwnerDirection;
                Main.sittingManager = savedSeats;
                Main.maxTilesX = savedWidth;
                Main.tile = savedTiles;
                Main.Map = savedMap;
                Main.leftWorld = savedLeft; Main.topWorld = savedTop; Main.rightWorld = savedRight; Main.bottomWorld = savedBottom;
            }
        }

        private static void VerifyStandingReturn(Action step, object role, Player actor, object activity)
        {
            Vector2 feet = actor.Bottom;
            Vector2 center = (Vector2)Get(Get(activity, "Water"), "Center");
            const int slot = 115;
            var previous = Main.npc[slot];
            bool moved = false;
            try
            {
                Main.npc[slot] = new NPC { whoAmI = slot, active = true, position = actor.position + new Vector2(7, 0),
                    width = 28, height = 42, damage = 1, life = 20, lifeMax = 20 };
                for (int frame = 0; frame < 24 && !moved; frame++)
                {
                    step();
                    moved = Vector2.DistanceSquared(actor.Bottom, feet) > 8 * 8;
                }
            }
            finally { Main.npc[slot] = previous; }
            Require(moved && ReferenceEquals(Get(role, "Fishing"), activity), "real nearby danger can displace a standing fishing body without cancelling the request");
            for (int frame = 0; frame < 240; frame++)
            {
                step();
                var bobber = NativeCompanionFishingLineChecks.WetBobber(actor);
                if ((bool)Get(activity, "Hold") && bobber != null && bobber.Center.X >= 96 * 16) break;
            }
            Require(ReferenceEquals(Get(role, "Fishing"), activity) && (bool)Get(activity, "Hold") &&
                (Vector2)Get(Get(activity, "Water"), "Center") == center && NativeCompanionFishingLineChecks.WetBobber(actor) != null,
                "the standing body returns to its fixed area and resumes a real line once danger clears");
            Console.WriteLine("PASS standing fishing body's danger avoidance and return to the fixed circle.");
        }

        private static void VerifyStandingDefense(Action step, object host, object role, CompanionProfile profile,
            Player actor, Player other, object activity)
        {
            const int wallX = 90, evadeWallX = 94, nearSlot = 115, distantSlot = 116, bowSlot = 41, arrowSlot = 56;
            var settings = ((CompanionProfile)Get(role, "Profile")).Behavior;
            var oldSettings = settings.Clone();
            settings.AttackEnabled = false;
            settings.Fishing = new FishingOptions(loadout: true);
            settings.MainWeapons = new[] { new CompanionWeaponPreference(ItemID.WoodenBow, 0, bowSlot) };
            int daily = actor.CurrentLoadoutIndex;
            int fishing = (daily + 2) % actor.Loadouts.Length;
            Item dailyArmor = new Item(ItemID.CopperHelmet);
            Item fishingAccessory = new Item(ItemID.AnglerEarring);
            actor.armor[0] = dailyArmor;
            actor.Loadouts[fishing].Armor[0] = new Item(ItemID.AnglerHat);
            actor.Loadouts[fishing].Armor[3] = fishingAccessory;
            actor.inventory[bowSlot] = new Item(ItemID.WoodenBow);
            actor.inventory[arrowSlot] = new Item(ItemID.WoodenArrow) { stack = 30 };
            Require((bool)Call(host, "SetBehavior", profile.Id, settings), "start the ordinary main weapon and automatic fishing loadout options");
            Projectile bobber = null;
            for (int frame = 0; frame < 400; frame++)
            {
                step();
                bobber = NativeCompanionFishingLineChecks.WetBobber(actor);
                if (bobber != null && actor.CurrentLoadoutIndex == fishing) break;
            }
            var gear = Get(Get(role, "FishingLine"), "Gear");
            Require(bobber != null && actor.CurrentLoadoutIndex == fishing && ReferenceEquals(actor.armor[3], fishingAccessory),
                "the fishing group keeps its actual Angler Earring and native bobber before threats are introduced: " +
                $"actual={actor.CurrentLoadoutIndex} desired={fishing} armor={actor.armor[3].type} staged={actor.Loadouts[fishing].Armor[3].type} " +
                $"strategy={Get(gear, "Strategy")} next={Get(gear, "NextPlan")} tick={Main.GameUpdateCount} " +
                $"hold={Get(activity, "Hold")} wet={bobber?.key} held={actor.selectedItem}/{actor.itemAnimation}/{actor.itemTime} " +
                $"status={Get(role, "Status")} feet={actor.Bottom}");
            settings.AttackEnabled = true;
            Require((bool)Call(host, "SetBehavior", profile.Id, settings), "turn on the ordinary Attack setting after native fishing is established");
            var center = (Vector2)Get(Get(activity, "Water"), "Center");
            var feet = actor.Bottom;
            int peerLoadout = other.CurrentLoadoutIndex;
            for (int y = 38; y < 48; y++) NativeToolsChecks.Tile(wallX, y, TileID.Stone);
            var priorNear = Main.npc[nearSlot];
            var priorDistant = Main.npc[distantSlot];
            var nearby = Main.npc[nearSlot] = new NPC();
            var distant = Main.npc[distantSlot] = new NPC();
            int ammo = actor.inventory[arrowSlot].stack;
            int bobberKey = (int)bobber.key;
            var groups = new List<int> { fishing };
            void RecordGroup()
            {
                if (groups[groups.Count - 1] != actor.CurrentLoadoutIndex) groups.Add(actor.CurrentLoadoutIndex);
            }
            string Trace() => $"groups={String.Join("->", groups)} feet={actor.Bottom}/{feet} " +
                $"ammo={actor.inventory[arrowSlot].stack}/{ammo} near={nearby.life} other={distant.life} " +
                $"wet={NativeCompanionFishingLineChecks.WetBobber(actor)?.key} status={Get(role, "Status")}";
            Projectile Incoming(IEntitySource source) => Main.projectile[Projectile.NewProjectile(source,
                new Vector2((evadeWallX + 1) * 16 + 8, actor.Center.Y), new Vector2(-8, 0), ProjectileID.FlamingWood, 8, 0, Main.myPlayer)];
            void CheckEvasion(IEntitySource source, string reason, Action afterBirth = null, bool keepBarrierOnReturn = false)
            {
                // The added wall is east so the body can evade left without
                // leaving the bank. Holding it through the first return
                // also exercises a real terrain-driven line change.
                for (int y = 44; y < 48; y++) NativeToolsChecks.Tile(evadeWallX, y, TileID.Stone);
                Projectile shot = null;
                try
                {
                    bool displaced = false, crossed = false;
                    shot = Incoming(source);
                    Require(shot.active && shot.hostile && !shot.friendly && !shot.tileCollide &&
                        !Collision.CanHitLine(actor.position, actor.width, actor.height, shot.position, shot.width, shot.height),
                        "native projectile passes a real shot barrier: " + reason);
                    afterBirth?.Invoke();
                    for (int frame = 0; frame < 20 && (!displaced || !crossed); frame++)
                    {
                        step();
                        RecordGroup();
                        displaced |= Vector2.DistanceSquared(actor.Bottom, feet) > 8 * 8;
                        crossed |= shot.Center.X < evadeWallX * 16;
                    }
                    if (shot.active) shot.Kill();
                    if (!keepBarrierOnReturn) for (int y = 44; y < 48; y++) Main.tile[evadeWallX, y].ClearEverything();
                    Require(crossed && displaced && groups.Count == 1 && actor.inventory[arrowSlot].stack == ammo &&
                        nearby.life == nearby.lifeMax && distant.life == distant.lifeMax && other.CurrentLoadoutIndex == peerLoadout,
                        reason + " causes real avoidance without blind attacks or switching armor: " + Trace());
                    for (int frame = 0; frame < 300; frame++)
                    {
                        step();
                        RecordGroup();
                        if ((bool)Get(activity, "Hold") && NativeCompanionFishingLineChecks.WetBobber(actor) != null) break;
                    }
                    Require((bool)Get(activity, "Hold") && NativeCompanionFishingLineChecks.WetBobber(actor) != null && groups.Count == 1,
                        "the fishing body returns to the same wet activity without switching gear after " + reason + ": " + Trace());
                    Console.WriteLine("PASS AI fishing projectile avoidance only: " + reason + ". " + Trace());
                }
                finally
                {
                    if (shot?.active == true) shot.Kill();
                    for (int y = 44; y < 48; y++) Main.tile[evadeWallX, y].ClearEverything();
                }
            }
            try
            {
                NativeCompanionNavigationChecks.InitializeTarget(nearby, nearSlot, new Vector2(wallX * 16 - 14, actor.Center.Y));
                nearby.noTileCollide = false;
                // The native body is on the other side of the stone. Its projected contact is only a threat if it can cross.
                nearby.velocity = new Vector2(8, 0);
                NativeCompanionNavigationChecks.InitializeTarget(distant, distantSlot, actor.Center + new Vector2(176, 0));
                Require(!Collision.CanHitLine(actor.position, actor.width, actor.height, nearby.position, nearby.width, nearby.height) &&
                    Collision.CanHitLine(actor.position, actor.width, actor.height, distant.position, distant.width, distant.height),
                    "fixture has a real full stone wall between the nearby NPC and the angler, and a separate open NPC");
                var allocated = NativeCompanionFishingLineChecks.GetAllocationSampler();
                long bytes = allocated?.Invoke() ?? 0;
                long started = Stopwatch.GetTimestamp();
                bool sameLine = true;
                for (int frame = 0; frame < 48; frame++)
                {
                    step();
                    RecordGroup();
                    sameLine &= (int?)NativeCompanionFishingLineChecks.WetBobber(actor)?.key == bobberKey;
                }
                double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                Console.WriteLine($"WORKLOAD AI fishing defense wall: frames=48 elapsedMS={milliseconds:F3} " +
                    $"allocatedBytes={(allocated == null ? "unavailable" : (allocated() - bytes).ToString())} {Trace()}");
                Require(sameLine && groups.Count == 1 && actor.inventory[arrowSlot].stack == ammo &&
                    nearby.life == nearby.lifeMax && distant.life == distant.lifeMax && other.CurrentLoadoutIndex == peerLoadout,
                    "a blocked projected-contact NPC and an unrelated visible NPC leave the exact wet line and fishing group in place without firing: " + Trace());

                nearby.velocity = Vector2.Zero;
                var blocked = Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(distant),
                    new Vector2(wallX * 16 - 12, actor.Center.Y), new Vector2(7, 0), ProjectileID.DeathLaser, 8, 0, Main.myPlayer)];
                Require(blocked.active && blocked.hostile && blocked.tileCollide, "known-parent hostile shot uses real tile collision");
                for (int frame = 0; frame < 16 && blocked.active; frame++) { step(); RecordGroup(); }
                Require(!blocked.active && (int?)NativeCompanionFishingLineChecks.WetBobber(actor)?.key == bobberKey &&
                    groups.Count == 1 && actor.inventory[arrowSlot].stack == ammo && distant.life == distant.lifeMax,
                    "the actual wall stops a known-parent projectile before it can justify switching or firing: " + Trace());
                CheckEvasion(new EntitySource_WorldEvent(), "a through-wall hostile projectile with unknown source", keepBarrierOnReturn: true);
                CheckEvasion(new EntitySource_Parent(distant), "a through-wall shot whose original source disappeared and its NPC slot was reused", () =>
                {
                    var emitterCenter = distant.Center;
                    distant.active = false;
                    distant = Main.npc[distantSlot] = new NPC();
                    NativeCompanionNavigationChecks.InitializeTarget(distant, distantSlot, emitterCenter);
                });

                for (int y = 43; y < 48; y++) Main.tile[wallX, y].ClearEverything();
                Require(Collision.CanHitLine(actor.position, actor.width, actor.height, nearby.position, nearby.width, nearby.height),
                    "removing five actual wall tiles exposes the nearby NPC");
                bool shotInDaily = false;
                Func<Projectile, bool> actorArrow = p => p.active && p.owner == actor.whoAmI && p.type == ProjectileID.WoodenArrowFriendly;
                nearby.velocity = new Vector2(8, 0);
                bytes = allocated?.Invoke() ?? 0;
                started = Stopwatch.GetTimestamp();
                int attackFrames = 0;
                for (; attackFrames < 160 && nearby.life == nearby.lifeMax; attackFrames++)
                {
                    step();
                    RecordGroup();
                    if (Main.projectile.Any(actorArrow))
                        shotInDaily |= actor.CurrentLoadoutIndex == daily && ReferenceEquals(actor.armor[0], dailyArmor);
                }
                milliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                Console.WriteLine($"WORKLOAD AI fishing defense opening: frames={attackFrames} elapsedMS={milliseconds:F3} " +
                    $"allocatedBytes={(allocated == null ? "unavailable" : (allocated() - bytes).ToString())} {Trace()}");
                Require(shotInDaily && nearby.life < nearby.lifeMax && distant.life == distant.lifeMax &&
                    actor.inventory[arrowSlot].stack < ammo && other.CurrentLoadoutIndex == peerLoadout,
                    "opening commits the actor's real bow, daily native armor, arrow debit and nearby NPC hit only: " + Trace());

                nearby.active = false;
                for (int frame = 0; frame < 320; frame++)
                {
                    step();
                    RecordGroup();
                    if (actor.CurrentLoadoutIndex == fishing && NativeCompanionFishingLineChecks.WetBobber(actor) != null &&
                        (bool)Get(activity, "Hold")) break;
                }
                Require(ReferenceEquals(Get(role, "Fishing"), activity) && (bool)Get(activity, "Hold") &&
                    Vector2.DistanceSquared(actor.Bottom, feet) <= 12 * 12 &&
                    (Vector2)Get(Get(activity, "Water"), "Center") == center && ReferenceEquals(actor.armor[3], fishingAccessory) &&
                    NativeCompanionFishingLineChecks.WetBobber(actor) != null && distant.life == distant.lifeMax &&
                    groups.SequenceEqual(new[] { fishing, daily, fishing }),
                    "ending real danger returns once to the original fishing group and resumes a native line at the same anchor with a distant foe still present: " + Trace());
                Console.WriteLine("PASS AI fishing self-defense: original weapon and damage only after an actual opening, then native armor and line resume. " + Trace());

                ammo = actor.inventory[arrowSlot].stack;
                int beforeSource = distant.life;
                var emitter = Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(distant), distant.Center,
                    new Vector2(-18, 0), ProjectileID.FlamingWood, 8, 0, Main.myPlayer)];
                Require(emitter.active && emitter.hostile, "native factory registers a live projectile with its actual parent NPC");
                bool sourcedAttack = false;
                for (int frame = 0; frame < 120 && distant.life == beforeSource; frame++)
                {
                    step();
                    RecordGroup();
                    if (actor.inventory[arrowSlot].stack < ammo)
                        sourcedAttack |= actor.CurrentLoadoutIndex == daily && ReferenceEquals(actor.armor[0], dailyArmor);
                }
                if (emitter.active) emitter.Kill();
                Require(sourcedAttack && distant.life < beforeSource && groups.SequenceEqual(new[] { fishing, daily, fishing, daily }),
                    "the same otherwise harmless NPC becomes a valid target when its actual native projectile threatens this actor: " + Trace());
                Console.WriteLine("PASS AI fishing actual parent projectile authorizes native retaliation exclusively against its source. " + Trace());
            }
            finally
            {
                Main.npc[nearSlot] = priorNear;
                Main.npc[distantSlot] = priorDistant;
                Require((bool)Call(host, "SetBehavior", profile.Id, oldSettings), "restore the neighboring stop and death scenarios' settings");
            }
        }
    }
}
