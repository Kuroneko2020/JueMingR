using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionShapeHandChecks
    {
        private const int Floor = 73, RoofStart = 81, RoofEnd = 89, BowSlot = 45, ThrowSlot = 46, HealSlot = 47;

        internal static void Run(object host, object actor, Player body, Player human)
        {
            NativeCombatLiveContextChecks.InitializeMount();
            var id = ((CompanionProfile)Get(actor, "Profile")).Id;
            var preferences = ((CompanionProfile)Get(actor, "Profile")).Behavior.Clone();
            var inventory = (Item[])body.inventory.Clone();
            var savedNpc = Main.npc[11];
            var savedCursor = Main.mouseItem;
            var bodyPosition = body.position;
            var bodyVelocity = body.velocity;
            var humanPosition = human.position;
            int selected = body.selectedItem, life = body.statLife, potionDelay = body.potionDelay;
            var buffTypes = (Array)body.buffType.Clone();
            var buffTimes = (Array)body.buffTime.Clone();
            var tiles = new Dictionary<Point, Tile>();
            try
            {
                Require(!body.mount.Active && body.height == 42, "shape hand fixture starts with an ordinary owned actor");
                Main.mouseItem = new Item();
                for (int x = 77; x <= 114; x++)
                    for (int y = Floor - 5; y <= Floor + 1; y++)
                    {
                        var point = new Point(x, y);
                        tiles.Add(point, Main.tile[x, y]);
                        var tile = new Tile();
                        if (y == Floor || x >= RoofStart && x <= RoofEnd && (y == Floor - 2 || y == Floor - 3))
                        { tile.active(true); tile.type = TileID.Stone; }
                        Main.tile[x, y] = tile;
                    }

                body.inventory[BowSlot] = new Item(ItemID.WoodenBow);
                body.inventory[54] = new Item(ItemID.WoodenArrow) { stack = 7 };
                body.inventory[ThrowSlot] = new Item(ItemID.Shuriken) { stack = 4 };
                body.inventory[HealSlot] = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                var behavior = new CompanionBehavior { Follow = true, AttackEnabled = true, HealthRecoveryEnabled = false,
                    MainWeapons = new[] { new CompanionWeaponPreference(ItemID.WoodenBow, 0, BowSlot) } };
                Call(host, "SetBehavior", id, behavior);

                PlaceShape(host, actor, body, human, MountID.Rat, 84);
                var target = new NPC();
                target.SetDefaults(NPCID.Zombie);
                target.whoAmI = 11;
                target.active = target.noGravity = target.noTileCollide = true;
                target.life = target.lifeMax = 1000;
                target.damage = 0;
                target.aiStyle = -1;
                target.Bottom = new Vector2(105 * 16f, Floor * 16f);
                Main.npc[11] = target;
                var arrowSlots = CurrentShots(body, ProjectileID.WoodenArrowFriendly);
                human.Center = new Vector2(108 * 16f, Floor * 16f - 21);
                bool movedAsRat = false, fired = false;
                for (int i = 0; i < 140 && !fired; i++)
                {
                    Step(host, body);
                    if (body.Center.X > 85 * 16 && body.Center.X < (RoofEnd + 1) * 16 && body.mount.Active) movedAsRat = true;
                    if (body.Center.X < (RoofEnd + 1) * 16)
                        Require(body.mount.Active && body.noItems && body.inventory[54].stack == 7, "Rat keeps the native compact body and does not pay ammo under the low roof");
                    fired = NewShot(body, ProjectileID.WoodenArrowFriendly, arrowSlots);
                }
                Require(movedAsRat && fired && !body.mount.Active && !body.noItems && body.height == 42 && body.inventory[54].stack == 6,
                    $"live Rat path leaves the tunnel before a native bow shot: moved={movedAsRat} fired={fired} body={body.position} mount={body.mount.Active} ammo={body.inventory[54].stack} status={Get(actor, "Status")}");
                target.active = false;

                behavior.AttackEnabled = false;
                Call(host, "SetBehavior", id, behavior);
                for (int i = 0; i < 35; i++) Step(host, body);

                PlaceShape(host, actor, body, human, MountID.Pixie, 84);
                Require(Queue(host, id, ThrowSlot) && !Queue(host, id, ThrowSlot), "menu's manual item entry takes one exact source while transformed");
                for (int i = 0; i < 12; i++) Step(host, body);
                Require(body.mount.Active && body.noItems && body.inventory[ThrowSlot].stack == 4,
                    "queued item and held animation wait while normal size is obstructed");
                var throwSlots = CurrentShots(body, ProjectileID.Shuriken);
                human.Center = new Vector2(108 * 16f, Floor * 16f - 21);
                for (int i = 0; i < 130 && body.inventory[ThrowSlot].stack == 4; i++) Step(host, body);
                Require(!body.mount.Active && !body.noItems && body.height == 42 && body.inventory[ThrowSlot].stack == 3 &&
                    NewShot(body, ProjectileID.Shuriken, throwSlots),
                    $"Pixie leaves safely before the exact manual item starts: body={body.position} velocity={body.velocity} mount={body.mount.Active} jump={body.controlJump} stack={body.inventory[ThrowSlot].stack} action={Get(Get(actor, "Mobility"), "Action")} result={Get(Get(actor, "Mobility"), "Result")} feedback={Get(host, "Feedback")}");
                for (int i = 0; i < 42; i++) Step(host, body);
                Require(body.inventory[ThrowSlot].stack == 3 && body.selectedItem != ThrowSlot, "manual throw does not repeat and releases its native temporary selection");

                PlaceShape(host, actor, body, human, MountID.Bat, 100);
                body.AddBuff(BuffID.Frozen, 90);
                Step(host, body);
                Require(body.CCed && Queue(host, id, ThrowSlot), "a controlled Bat retains an exact manual request");
                for (int i = 0; i < 9; i++) Step(host, body);
                Require(body.CCed && body.mount.Active && body.inventory[ThrowSlot].stack == 3, "native crowd control forbids the shape exit and item start");
                body.ClearBuff(BuffID.Frozen);
                for (int i = 0; i < 58; i++) Step(host, body);
                Require(!body.CCed && !body.mount.Active && body.inventory[ThrowSlot].stack == 2 && !(bool)Get(actor, "Faulted"),
                    "clearing actual crowd control lets Bat exit, use once and finish its native hand");

                PlaceShape(host, actor, body, human, MountID.Pixie, 100);
                body.statLife = Math.Max(1, body.statLifeMax2 - 45);
                body.potionDelay = 0;
                int damagedLife = body.statLife;
                behavior.HealthRecoveryEnabled = true;
                behavior.HealthRecoveryMode = CompanionHealthRecoveryMode.Fast;
                Call(host, "SetBehavior", id, behavior);
                for (int i = 0; i < 20 && body.inventory[HealSlot].stack == 2; i++) Step(host, body);
                Require(!body.mount.Active && body.inventory[HealSlot].stack == 1 && body.statLife > damagedLife && body.potionDelay > 0,
                    $"existing native QuickHeal consumes in open space and actually dismounts Pixie: mount={body.mount.Active} stack={body.inventory[HealSlot].stack} life={body.statLife}/{damagedLife} delay={body.potionDelay}");
            }
            finally
            {
                if (body.mount.Active) body.mount.Dismount(body, true);
                foreach (var tile in tiles) Main.tile[tile.Key.X, tile.Key.Y] = tile.Value;
                Array.Copy(inventory, body.inventory, inventory.Length);
                Array.Copy(buffTypes, body.buffType, buffTypes.Length);
                Array.Copy(buffTimes, body.buffTime, buffTimes.Length);
                body.selectedItemState.Select(selected);
                body.statLife = life;
                body.potionDelay = potionDelay;
                Main.npc[11] = savedNpc;
                Main.mouseItem = savedCursor;
                body.position = bodyPosition;
                body.velocity = bodyVelocity;
                human.position = humanPosition;
                Call(host, "SetBehavior", id, preferences);
            }
            Console.WriteLine("PASS native companion handless forms: Rat crossed a low tunnel before firing, Pixie kept one manual use until clear, Bat waited for crowd control to clear, open-space QuickHeal used its original native path.");
        }

        private static void PlaceShape(object host, object actor, Player body, Player human, int type, int x)
        {
            if (body.mount.Active) body.mount.Dismount(body, true);
            body.position = new Vector2(100 * 16, Floor * 16 - body.height);
            body.velocity = Vector2.Zero;
            body.mount.SetMount(type, body);
            body.position = new Vector2(x * 16, Floor * 16 - body.height);
            human.Center = body.Center;
            Set(actor, "Behavior", Activator.CreateInstance(Get(actor, "Behavior").GetType(), true));
            Step(host, body);
            Require(body.mount.Active && body.mount.Type == type && body.noItems && body.height < 42,
                $"native mount updates own the actual handless body: type={type} active={body.mount.Active} noItems={body.noItems} height={body.height}");
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            body.Update(body.whoAmI);
        }

        private static bool Queue(object host, Guid id, int slot) =>
            (bool)Call(host, "CanUseItemOnce", new object[] { id, CompanionInventoryArea.Inventory, slot, null }) &&
            (bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, slot);

        private static HashSet<Projectile> CurrentShots(Player body, int type) => new HashSet<Projectile>(Main.projectile.Where(shot =>
            shot.active && shot.owner == body.whoAmI && shot.type == type));

        private static bool NewShot(Player body, int type, HashSet<Projectile> old) => Main.projectile.Any(shot =>
            shot.active && shot.owner == body.whoAmI && shot.type == type && !old.Contains(shot));
    }
}
