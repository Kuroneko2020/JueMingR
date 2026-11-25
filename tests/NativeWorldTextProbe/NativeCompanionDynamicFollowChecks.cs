using System;
using System.Diagnostics;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionDynamicFollowChecks
    {
        internal static void Run(object host, object actor, Player body, Player human, bool comparison)
        {
            var previousTiles = Main.tile;
            var bodyArmor = (Item[])body.armor.Clone();
            var previousMount = body.inventory[32];
            var previousPotion = body.inventory[33];
            var previousWeapon = body.inventory[41];
            int life = body.statLife;
            int potionDelay = body.potionDelay;
            var buffs = (Array)body.buffType.Clone();
            var buffTimes = (Array)body.buffTime.Clone();
            var bodyPosition = body.position;
            var humanPosition = human.position;
            var bodyVelocity = body.velocity;
            var humanVelocity = human.velocity;
            var previousBehavior = Get(actor, "Behavior");
            var profile = (CompanionProfile)Get(actor, "Profile");
            var wingStats = ArmorIDs.Wing.Sets.Stats;
            try
            {
                Terraria.Initializers.WingStatsInitializer.Load();
                NativeCombatLiveContextChecks.InitializeMount();
                Main.tile = new Tile[Main.maxTilesX, Main.maxTilesY];
                for (int x = 0; x < Main.maxTilesX; x++)
                    for (int y = 0; y < Main.maxTilesY; y++) Main.tile[x, y] = new Tile();
                for (int x = 42; x < 118; x++) Stone(x, 106);
                for (int y = 82; y < 106; y++) Stone(70, y);
                for (int i = 0; i < body.armor.Length; i++) body.armor[i] = new Item();
                body.armor[3] = new Item(ItemID.CreativeWings);
                body.inventory[32] = new Item(ItemID.CosmicCarKey);
                body.inventory[41] = new Item(ItemID.VenusMagnum);
                int sickness = body.FindBuffIndex(BuffID.PotionSickness);
                if (sickness >= 0) body.DelBuff(sickness);
                body.potionDelay = 0;
                body.gravDir = 1;
                body.RemoveAllGrapplingHooks();
                body.position = new Vector2(65 * 16, 106 * 16 - body.height);
                if (body.mount.Active) body.mount.Dismount(body);
                body.position = human.position = new Vector2(65 * 16, 106 * 16 - body.height);
                body.velocity = human.velocity = Vector2.Zero;
                body.ResetFallDamage();
                Set(actor, "Behavior", Activator.CreateInstance(previousBehavior.GetType(), true));
                Require((bool)Call(host, "SetBehavior", profile.Id, new CompanionBehavior
                {
                    Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false,
                    MainWeapons = new[] { new CompanionWeaponPreference(ItemID.VenusMagnum, 0, 41) }
                }),
                    "dynamic fixture uses the public follow behavior");
                Call(Get(actor, "Mobility"), "Reset", Get(actor, "Generation"));
                for (int t = 0; t < 6; t++) Step(host, body);
                float full = body.wingTime;
                Require(full > 0 && body.wingsLogic != 0 && !body.mount.Active, "finite wings warmed by native gear before commencing follow: " +
                    $"budget={full} logic={body.wingsLogic} mount={body.mount.Type} position={body.position} velocity={body.velocity} status={Get(actor,"Status")}");
                human.position = new Vector2(74 * 16, 106 * 16 - human.height);
                var origin = body.position;
                var ownerOrigin = human.position;
                int ownerMoved = 0, bodyMoved = 0, firstUp = -1, firstWing = -1, mounted = -1, firstDown = -1,
                    firstReverse = -1, turnUseful = -1, longestIdle = 0, idle = 0, directBlocked = 0,
                    firstSettled = -1, stable = 0, flyingHeal = -1;
                float ownerTop = human.position.Y, bodyTop = body.position.Y;
                bool mountedInAir = false, crossedWall = false;
                var navigation = Get(Get(actor, "Behavior"), "Navigator");
                var mobility = Get(actor, "Mobility");
                int firstGeneration = (int)Get(navigation, "QueryGeneration");
                string previousNativeAction = null;
                long cost = 0;
                for (int t = 0; t < 340; t++)
                {
                    var ownerBefore = human.position;
                    var before = body.position;
                    if (!comparison && t == firstWing + 2 && firstWing >= 0)
                    {
                        body.statLife = Math.Max(1, body.statLifeMax2 - 65);
                        body.inventory[33] = new Item(ItemID.LesserHealingPotion);
                        Require((bool)Call(host, "UseItemOnce", profile.Id, CompanionInventoryArea.Inventory, 33),
                            "one accepted manual use retains its actual owned equipment in flight");
                    }
                    // Only the observed owner trajectory is driven by the
                    // fixture. The actual AI runs its native Update once.
                    human.velocity = OwnerMotion(t);
                    human.position += human.velocity;
                    cost += Step(host, body);
                    if (Vector2.DistanceSquared(ownerBefore, human.position) > 1) ownerMoved++;
                    if (Vector2.DistanceSquared(before, body.position) > .25f) { bodyMoved++; idle = 0; }
                    else if (t < 235) longestIdle = Math.Max(longestIdle, ++idle);
                    if (firstUp < 0 && body.position.Y < origin.Y - 8 && body.velocity.Y < 0) firstUp = t;
                    if (firstWing < 0 && !body.mount.Active && body.wingTime < full && body.velocity.Y < 0) firstWing = t;
                    if (!comparison && flyingHeal < 0 && firstWing >= 0 && t > firstWing + 1 && body.inventory[33].IsAir &&
                        body.statLife > Math.Max(1, body.statLifeMax2 - 65) && body.controlJump && body.position.Y < before.Y) flyingHeal = t;
                    if (mounted < 0 && body.mount.Active) { mounted = t; mountedInAir = body.Bottom.Y < 106 * 16 - 24; }
                    if (firstDown < 0 && t >= 78 && body.position.Y > before.Y + 1 && body.velocity.Y > 0) firstDown = t;
                    if (turnUseful < 0 && t >= 99 && human.Center.X - body.Center.X < -36) turnUseful = t;
                    if (firstReverse < 0 && turnUseful >= 0 && body.position.X < before.X - .5f) firstReverse = t;
                    if (body.controlRight && before.X + body.width >= 70 * 16 - 2 && body.position.Y >= 82 * 16 && body.position.X <= before.X) directBlocked++;
                    crossedWall |= body.position.X >= 71 * 16 && body.Bottom.Y <= 82 * 16 + 4;
                    ownerTop = Math.Min(ownerTop, human.position.Y);
                    bodyTop = Math.Min(bodyTop, body.position.Y);
                    if (t >= 235 && SettledNearOwner(body, human))
                    { if (stable == 0) firstSettled = t; stable = SettledStep(stable, true); }
                    else { stable = SettledStep(stable, false); firstSettled = -1; }
                    string nativeAction = $"{Get(mobility, "Action")}/{Get(mobility, "Result")}";
                    if (t % 24 == 0 || t == 339 || t == firstWing || t == mounted || nativeAction != previousNativeAction)
                    {
                        var observed = Get(mobility, "Observed");
                        var delivery = GetOptional(navigation, "LastMovementDelivery");
                        var transition = Get(navigation, "ActionTransition");
                        var choices = GetOptional(observed, "MountChoices") as Array;
                        Console.WriteLine($"FOLLOW TRACE t={t} owner={human.Bottom}/{human.velocity} actor={body.Bottom}/{body.velocity} jump={body.controlJump} wing={body.wingTime:F1}/{full:F1} " +
                            $"mount={body.mount.Type} ownedMountChoices={choices?.Length} native={nativeAction} " +
                            $"route={Get(navigation, "ActionId")}/{Get(transition, "Kind")}/{Get(transition, "Detail")}:{Get(transition, "From")}→{Get(transition, "To")} " +
                            $"delivery={(delivery == null ? null : Get(delivery, "Activation"))} query={Get(navigation, "QueryStatus")}/{Get(navigation, "QueryGeneration")} reason={Get(navigation, "LastDeferral")}");
                    }
                    previousNativeAction = nativeAction;
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW DYNAMIC ownerMoved={ownerMoved} ownerRise={ownerOrigin.Y-ownerTop:F1} actorMoved={bodyMoved} rise={origin.Y-bodyTop:F1} firstUp={firstUp} wing={firstWing} mount={mounted} airborneMount={mountedInAir} down={firstDown} turnUseful={turnUseful} reverse={firstReverse} crossed={crossedWall} starts={(int)Get(navigation, "QueryGeneration") - firstGeneration} blockedHold={directBlocked} longestMovingIdle={longestIdle} settled={firstSettled} stable={stable} healingInFlight={flyingHeal} actorMs={1000d*cost/Stopwatch.Frequency:F3}"));
                Require(ownerMoved > 90 && ownerOrigin.Y - ownerTop > 300 && human.position.X < 88 * 16,
                    "the scripted owner continuously flies above root planning range, releases, and reverses");
                Require(!(bool)Get(actor, "Faulted"), "dynamic production player does not fault: " + Get(actor, "Status"));
                if (!comparison)
                {
                    Require(firstUp >= 0 && firstUp < 55 && firstWing > firstUp && bodyMoved > 95 && crossedWall,
                        "production follows a moving human by taking off and passing the real wall using its own capabilities");
                    Require(mounted > firstWing && mountedInAir && firstDown > mounted && firstReverse >= 99,
                        "native finite wing flight continues through legal airborne mount, descent and turn");
                    Require(SettledStep(SettledStep(19, false), true) == 1,
                        "one nonqualifying update interrupts the continuous settled window");
                    Require(firstSettled >= 235 && stable >= 20 && SettledNearOwner(body, human),
                        "after the moving owner stops, the native body brakes into the latest reachable region and stays close");
                    Require(flyingHeal > firstWing && flyingHeal < mounted, "a real manual potion consumes and heals while the production follow input remains in ascending wing flight");
                    var current = ((CompanionProfile)Get(actor, "Profile")).Behavior.MainWeapons;
                    Require(current.Length == 1 && current[0].Type == ItemID.VenusMagnum, "the accepted mobility item does not alter the actual weapon preference");
                    RequireNativeMountClearance(host, actor, body, human);
                }
            }
            finally
            {
                if (body.mount.Active) body.mount.Dismount(body);
                Main.tile = previousTiles;
                ArmorIDs.Wing.Sets.Stats = wingStats;
                Array.Copy(bodyArmor, body.armor, bodyArmor.Length);
                body.inventory[32] = previousMount;
                body.inventory[33] = previousPotion;
                body.inventory[41] = previousWeapon;
                body.statLife = life;
                body.potionDelay = potionDelay;
                Array.Copy(buffs, body.buffType, buffs.Length);
                Array.Copy(buffTimes, body.buffTime, buffTimes.Length);
                body.position = bodyPosition; body.velocity = bodyVelocity;
                human.position = humanPosition; human.velocity = humanVelocity;
                Call(host, "SetBehavior", profile.Id, profile.Behavior);
                Set(actor, "Behavior", previousBehavior);
            }
        }

        private static int SettledStep(int consecutive, bool inRegion) => inRegion ? consecutive + 1 : 0;

        private static bool SettledNearOwner(Player body, Player human)
        {
            if (!body.active || body.dead || Math.Abs(human.Center.X - body.Center.X) >= 32 ||
                Math.Abs(human.Center.Y - body.Center.Y) >= 34 || Math.Abs(body.velocity.X) >= .65f || Math.Abs(body.velocity.Y) >= .65f)
                return false;
            // The scripted endpoint is in open air; the matching actor must
            // still have a native means to hold there at the end of the run.
            return body.mount.Active && body.mount.CanHover();
        }


        private static void RequireNativeMountClearance(object host, object actor, Player body, Player human)
        {
            const int slot = 32, floor = 106, roof = 102;
            body.mount.Dismount(body);
            Require(!body.mount.Active, "the previous actual mount can legally dismount in its open room");
            body.armor[3] = new Item();
            for (int x = 50; x <= 63; x++) Stone(x, roof);
            var item = body.inventory[slot];
            int itemStack = item.stack;
            var feet = new Vector2(56 * 16 + body.width * .5f, floor * 16);
            var state = Get(actor, "Mobility");
            ResetTrial();
            Require(body.wingsLogic == 0 && item.type == ItemID.CosmicCarKey && item.mountType >= 0 &&
                ReferenceEquals(item, body.QuickMount_GetItemToUse()), "only the actual inventory flight mount is available in this room");
            int expandedHeight = body.height + Mount.GetHeightBoost(item.mountType);
            var mountedTop = feet - new Vector2(body.width * .5f, expandedHeight);
            bool closedCollision = Collision.SolidCollision(mountedTop, body.width, expandedHeight);
            Require(expandedHeight > body.height && closedCollision && !body.mount.CanMount(item.mountType, body) &&
                !Collision.SolidCollision(body.position, body.width, body.height),
                "native collision admits the ordinary body but rejects this actual mount's expanded size at the same feet");
            bool attempted = false;
            human.Bottom = feet - new Vector2(0, 176);
            for (int t = 0; t < 50; t++)
            {
                Step(host, body);
                attempted |= Get(state, "Action").ToString() == "QuickMount";
                Require(!body.mount.Active && !body.dead && body.Top.Y >= (roof + 1) * 16 && body.Bottom.Y <= floor * 16 + .1f &&
                    body.Left.X >= 50 * 16 && body.Right.X <= 64 * 16 && !Collision.SolidCollision(body.position, body.width, body.height) &&
                    ReferenceEquals(item, body.inventory[slot]) && item.type == ItemID.CosmicCarKey && item.stack == itemStack,
                    "the same body stays safe and retains its own real mount below the low ceiling");
            }
            var closedFeet = body.Bottom;
            for (int x = 50; x <= 63; x++) Main.tile[x, roof].active(false);
            ResetTrial();
            bool openCollision = Collision.SolidCollision(mountedTop, body.width, expandedHeight);
            Require(!openCollision && body.mount.CanMount(item.mountType, body) && body.Bottom == feet,
                "only removing the roof admits the same expanded native body at its original feet");
            // The continuous high-wall case already proves automatic Follow activation.
            // This native control changes only headroom between the two room poses.
            body.QuickMount();
            Console.WriteLine($"FOLLOW MOUNT ROOM height={expandedHeight} closedCollision={closedCollision} attempted={attempted} closedFeet={closedFeet} openCollision={openCollision} nativeOpenMount={body.mount.Type}/{body.mount.Active}");
            Require(body.mount.Active && body.mount.Type == item.mountType && body.height == expandedHeight &&
                ReferenceEquals(item, body.inventory[slot]) && item.stack == itemStack && !body.dead,
                "the same inventory mount actually starts through the native command when only the roof is removed");

            void ResetTrial()
            {
                body.Bottom = human.Bottom = feet;
                body.velocity = human.velocity = Vector2.Zero;
                body.ResetFallDamage();
                Call(Get(Get(actor, "Behavior"), "Navigator"), "ResetPosition");
                Call(state, "Reset", Get(actor, "Generation"));
                for (int t = 0; t < 4; t++) Step(host, body);
            }
        }

        private static void Stone(int x, int y)
        {
            var tile = Main.tile[x, y];
            tile.active(true);
            tile.type = TileID.Stone;
        }

        private static Vector2 OwnerMotion(int tick)
        {
            float direction = tick < 54 ? 4.2f : tick < 82 ? 1f : tick < 99 ? 0 : tick < 193 ? -3.8f : 0;
            float rise = tick < 12 ? -3 - tick * .35f : tick < 74 ? -6 : tick < 88 ? -2 :
                tick < 118 ? 3.5f : tick < 147 ? -2.8f : tick < 195 ? 0 : tick < 235 ? 4f : 0;
            return new Vector2(direction, rise);
        }

        private static long Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            long start = Stopwatch.GetTimestamp();
            body.Update(body.whoAmI);
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            return Stopwatch.GetTimestamp() - start;
        }
    }
}
