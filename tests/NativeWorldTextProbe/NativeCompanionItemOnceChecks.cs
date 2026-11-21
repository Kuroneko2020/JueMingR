using System;
using System.Linq;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionItemOnceChecks
    {
        internal static void Run(object host, object actor, Player human, Action tick)
        {
            var body = (Player)Get(actor, "Body");
            Guid id = ((CompanionProfile)Get(actor, "Profile")).Id;
            int originalSlot = body.selectedItem;
            var oldPosition = body.position;
            var oldHuman = human.position;
            bool oldInventory = Main.playerInventory, oldCreative = human.creativeInterface;
            int oldMouseX = Main.mouseX, oldMouseY = Main.mouseY;
            float oldCameraX = Main.cameraX, oldCameraY = Main.cameraY;
            int oldMap = Main.mapTime;
            int oldSpawnX = Main.spawnTileX;
            var targetBefore = Main.npc[0];
            var oldAmmo = body.inventory[54];
            var oldHumanAmmo = human.inventory[54].Clone();
            var portBuffer = NetMessage.buffer[256];
            var itemsBefore = body.inventory.Skip(32).Take(6).ToArray();
            try
            {
                NetMessage.buffer[256] = new MessageBuffer { whoAmI = 256 };
                body.inventory[32] = new Item(ItemID.WoodenBow);
                body.inventory[33] = new Item(ItemID.MoneyTrough);
                body.inventory[34] = new Item(ItemID.CellPhone);
                body.inventory[35] = new Item(ItemID.ShellphoneSpawn);
                body.inventory[36] = new Item(ItemID.PiggyBank) { stack = 2 };
                body.inventory[37] = new Item(ItemID.RodofDiscord);
                body.inventory[54] = new Item(ItemID.WoodenArrow) { stack = 11 };
                var selected = new[] { 32, 33, 34, 35 };
                foreach (int slot in selected)
                    Require((bool)Call(host, "CanUseItemOnce", new object[] { id, CompanionInventoryArea.Inventory, slot, null }),
                        "manual selection admits the actual " + body.inventory[slot].Name);
                foreach (var pair in new[] { Tuple.Create(36, "放置"), Tuple.Create(37, "指定") })
                {
                    var args = new object[] { id, CompanionInventoryArea.Inventory, pair.Item1, null };
                    Require(!(bool)Call(host, "CanUseItemOnce", args) && ((string)args[3]).Contains(pair.Item2) &&
                        !(bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, pair.Item1),
                        "unavailable action explains its actual location/placement constraint: " + pair.Item2);
                }
                foreach (int type in new[] { ItemID.LawnMower, ItemID.GravediggerShovel, ItemID.Firefly, ItemID.EmptyBucket,
                    ItemID.YellowWrench, ItemID.RubblemakerSmall, ItemID.QueenSpiderStaff, ItemID.ChestLock, ItemID.StarTopper1,
                    ItemID.BlueAndYellowLights, ItemID.OldShoe, ItemID.Paintbrush, ItemID.SpectrePaintScraper, ItemID.FreezeBomb,
                    ItemID.MysticCoilSnake, ItemID.MudBallPlayer, ItemID.AcornSlingshot, ItemID.Sandgun, ItemID.IceRod })
                {
                    var restricted = body.inventory[37] = new Item(type) { stack = 2 };
                    var args = new object[] { id, CompanionInventoryArea.Inventory, 37, null };
                    Require(!(bool)Call(host, "CanUseItemOnce", args) &&
                        (((string)args[3]).Contains("指定") || ((string)args[3]).Contains("放置")) &&
                        !(bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 37) &&
                        ReferenceEquals(body.inventory[37], restricted) && restricted.stack == 2,
                        "manual use requires a location or places in the world for original item " + type);
                }
                foreach (int type in new[] { ItemID.PalworldDigtoise, ItemID.PalworldTrustyDigtoise })
                {
                    var digger = body.inventory[37] = new Item(type);
                    int stack = digger.stack;
                    var args = new object[] { id, CompanionInventoryArea.Inventory, 37, null };
                    Require(!(bool)Call(host, "CanUseItemOnce", args) && ((string)args[3]).Contains("指定世界中的地点") &&
                        !(bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 37) &&
                        ReferenceEquals(body.inventory[37], digger) && digger.stack == stack,
                        "the original zero-damage Digtoise requires a world column through both normal manual entry points: " + type);
                }
                body.inventory[37] = new Item(ItemID.RocketLauncher);
                body.inventory[54] = new Item(ItemID.WetRocket) { stack = 3 };
                var launcher = new object[] { id, CompanionInventoryArea.Inventory, 37, null };
                Require(!(bool)Call(host, "CanUseItemOnce", launcher) && ((string)launcher[3]).Contains("液体") &&
                    !(bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 37) && body.inventory[54].stack == 3,
                    "the admitted launcher's real liquid ammunition cannot place water");
                body.inventory[37] = new Item(ItemID.Celeb2);
                Require((bool)Call(host, "CanUseItemOnce", launcher), "Celebration Mk2 converts that very same ammo to a non-placing native projectile");
                body.inventory[54] = new Item(ItemID.WoodenArrow) { stack = 11 };
                body.position = new Vector2(42 * 16, 60 * 16 - body.height);
                body.velocity = Vector2.Zero;
                var enemy = new NPC();
                enemy.SetDefaults(NPCID.Zombie);
                enemy.whoAmI = 0;
                enemy.active = true;
                enemy.position = body.position + new Vector2(96, 0);
                Main.npc[0] = enemy;
                body.ChangeDir(-1);
                Main.mouseX = (int)(body.Center.X - Main.screenPosition.X) - 240;
                Main.mouseY = (int)(body.Center.Y - Main.screenPosition.Y);
                var arrowsBefore = Main.projectile.Where(p => p.active && p.type == ProjectileID.WoodenArrowFriendly && p.owner == body.whoAmI).ToArray();
                Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 32), "queued selected real bow");
                for (int i = 0; i < 65; i++) tick();
                var shots = Main.projectile.Where(p => p.active && p.type == ProjectileID.WoodenArrowFriendly &&
                    p.owner == body.whoAmI && !arrowsBefore.Contains(p)).ToArray();
                Require(shots.Length == 1 && shots[0].velocity.X > 0 && body.inventory[54].stack == 10 &&
                    body.selectedItem == originalSlot && !human.inventory[54].IsNetStateDifferent(oldHumanAmmo),
                    "one native bow aims at its own target opposite the human cursor, costs its own arrow and returns the hand: " +
                    $"shots={shots.Length} ammo={body.inventory[54].stack} selected={body.selectedItem} feedback={Get(host, "Feedback")}");
                body.inventory[54].TurnToAir();
                Require(!(bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 32) &&
                    ((string)Get(host, "Feedback")).Contains("弹药"), "a bow cannot borrow arrows or start a second action without its own ammo");
                Main.npc[0] = targetBefore;
                Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 33), "ordinary single-use admits the portable pig");
                Projectile pig = null;
                for (int i = 0; i < 62 && pig == null; i++)
                {
                    tick();
                    pig = Main.projectile.FirstOrDefault(p => p.active && p.owner == body.whoAmI && p.type == ProjectileID.FlyingPiggyBank);
                }
                var bankArgs = new object[] { id, CompanionInventoryArea.PiggyBank, null };
                bool bankOpen = (bool)Call(host, "StorageAvailable", bankArgs);
                Require(pig != null && bankOpen && body.inventory[36].stack == 2,
                    $"Money Trough creates an available native flying pig while actual furniture remains: pig={pig?.Center} body={body.Center} slot={body.selectedItem}/{originalSlot} bank={bankArgs[2]} feedback={Get(host, "Feedback")}");
                for (int i = 0; i < 62 && body.selectedItem != originalSlot; i++) tick();
                Require(body.selectedItem == originalSlot, "the native Money Trough hand returns its selection");
                // The active body's real stand order can walk back towards
                // its old anchor. Use a distinct, reachable spawn beyond it
                // to distinguish native discontinuous movement from running.
                Main.spawnTileX = 75;
                foreach (int slot in new[] { 34, 35 })
                {
                    body.position = new Vector2(46 * 16, 60 * 16 - body.height);
                    body.velocity = Vector2.Zero;
                    Main.playerInventory = human.creativeInterface = true;
                    Main.cameraX = 9; Main.cameraY = 13;
                    Main.mapTime = 1;
                    int type = body.inventory[slot].type;
                    Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, slot), "queued actual phone mode");
                    for (int i = 0; i < 12; i++) tick();
                    Require(body.selectedItem == slot && body.itemTime > body.inventory[slot].useTime / 2 &&
                        Math.Abs(body.Center.X - Main.spawnTileX * 16) > 120,
                        $"native phone still owns its hand before halfway: type={type} selected={body.selectedItem}/{slot} animation={body.itemAnimation} time={body.itemTime}/{body.inventory[slot].useTime} actor={body.Center} spawn={Main.spawnTileX * 16} feedback={Get(host, "Feedback")}");
                    bool jumpedToSpawn = false;
                    float jumpStart = 0, jumpEnd = 0;
                    for (int i = 0; i < 100; i++)
                    {
                        float beforeX = body.Center.X;
                        tick();
                        if (Math.Abs(body.Center.X - beforeX) > Math.Abs(jumpEnd - jumpStart))
                        { jumpStart = beforeX; jumpEnd = body.Center.X; }
                        if (Math.Abs(body.Center.X - beforeX) > 120 && Math.Abs(body.Center.X - Main.spawnTileX * 16) < 80) jumpedToSpawn = true;
                    }
                    Require(jumpedToSpawn && body.selectedItem == originalSlot &&
                        body.inventory[slot].type == type && human.position == oldHuman && Main.playerInventory &&
                        human.creativeInterface && Main.cameraX == 9 && Main.cameraY == 13 && Main.mapTime == 1,
                        $"native phone {type} moves the actor and preserves the human: largest={jumpStart}->{jumpEnd} spawn={Main.spawnTileX * 16} selected={body.selectedItem}/{originalSlot} type={body.inventory[slot].type} human={human.position}/{oldHuman} inventory={Main.playerInventory} creative={human.creativeInterface} camera={Main.cameraX},{Main.cameraY} map={Main.mapTime} feedback={Get(host, "Feedback")}");
                }
                Console.WriteLine("PASS single native companion hand: actor-aimed bow/one arrow; flying pig versus furniture; location-required refusal; Cell Phone and world-spawn Shellphone delayed teleports with human UI intact.");
            }
            finally
            {
                Main.npc[0] = targetBefore;
                Main.playerInventory = oldInventory; human.creativeInterface = oldCreative;
                Main.mouseX = oldMouseX; Main.mouseY = oldMouseY;
                Main.cameraX = oldCameraX; Main.cameraY = oldCameraY; Main.mapTime = oldMap;
                Main.spawnTileX = oldSpawnX;
                body.position = oldPosition;
                body.inventory[54] = oldAmmo;
                Array.Copy(itemsBefore, 0, body.inventory, 32, itemsBefore.Length);
                NetMessage.buffer[256] = portBuffer;
            }
        }
    }
}
