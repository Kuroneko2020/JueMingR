using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionResourceTaskChecks
    {
        internal static void Run(object host, Player human, Player body, Action tick)
        {
            var actor = Call(host, "Owned", body);
            var profile = (CompanionProfile)Get(actor, "Profile");
            var identity = new CompanionTaskIdentity(profile.OwnerId, profile.Id, (long)Get(host, "session"),
                (long)Get(actor, "Generation"), profile.DataEpoch);
            var anchor = host.GetType().GetMethod("CaptureFishingAnchor", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { human });
            CompanionTaskOutcome Query(string name, params (string Key, object Value)[] arguments) =>
                (CompanionTaskOutcome)Call(host, "TryGetTaskObservation", identity, name, Map(arguments), anchor);
            CompanionTaskStep Step(string operation, params (string Key, object Value)[] arguments) => new CompanionTaskStep(operation, Map(arguments));
            CompanionTaskSnapshot RunStep(string operation, params (string Key, object Value)[] arguments) => RunPlan(Step(operation, arguments));
            CompanionTaskSnapshot RunPlan(params CompanionTaskStep[] steps)
            {
                var result = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "资源操作原版验证",
                    steps, anchor, Guid.NewGuid());
                Require(result.State == "accepted", steps[0].Operation + " schema admitted: " + result.Detail);
                for (int i = 0; i < 240 && result.IsActive; i++)
                {
                    tick();
                    result = (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, result.TaskId);
                }
                Require(!result.IsActive, steps[0].Operation + " reached a terminal receipt: " + result.Detail);
                return result;
            }

            var humanInventory = human.inventory.ToArray();
            var actorInventory = body.inventory.ToArray();
            var oldHelmet = body.armor[0];
            Vector2 previousFeet = body.Bottom, previousVelocity = body.velocity;
            bool nearby = Player.Settings.CraftFromNearbyChests;
            var mouse = Main.mouseItem;
            int index = Array.FindIndex(Main.chest, entry => entry == null);
            Point feet = body.Bottom.ToTileCoordinates();
            var position = new Point(feet.X + 8, feet.Y - 2);
            var previousTiles = new Tile[2, 2];
            try
            {
                Player.Settings.CraftFromNearbyChests = true;
                for (int slot = 0; slot < 58; slot++) human.inventory[slot] = body.inventory[slot] = new Item();
                body.armor[0] = new Item();
                human.inventory[10] = new Item(ItemID.Wood) { stack = 9 };
                var transfer = RunStep("transfer_item", ("item_type", ItemID.Wood), ("amount", 4),
                    ("source", "owner_inventory"), ("destination", "self_inventory"), ("keep_amount", 7));
                Require(transfer.State == "partial" && transfer.Attempts.Last().Completed == 2 &&
                    Quantity(body.inventory, ItemID.Wood) == 2 && Quantity(human.inventory, ItemID.Wood) == 7,
                    "specified reserve and true partial conserve owner/AI quantities: " + transfer.Detail);

                body.inventory[15] = new Item(ItemID.CopperHelmet);
                var equip = RunStep("equip_item", ("item_type", ItemID.CopperHelmet));
                Require(equip.State == "succeeded" && body.armor[0].type == ItemID.CopperHelmet &&
                    Quantity(body.inventory, ItemID.CopperHelmet) == 0, "actual legal body slot receives the selected armor");
                var unequip = RunStep("unequip_item", ("slot", 0));
                Require(unequip.State == "succeeded" && body.armor[0].IsAir && Quantity(body.inventory, ItemID.CopperHelmet) == 1,
                    "unequip returns exact armor to AI bag");

                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        previousTiles[x, y] = Main.tile[position.X + x, position.Y + y];
                        var cell = new Tile { type = TileID.Containers, frameX = (short)(18 * x), frameY = (short)(18 * y) };
                        cell.active(true);
                        Main.tile[position.X + x, position.Y + y] = cell;
                    }
                var chest = Chest.CreateWorldChest(index, position.X, position.Y);
                chest.item[0] = new Item(ItemID.Gel) { stack = 10 };
                chest.item[1] = new Item(ItemID.StoneBlock) { stack = 10 };
                var estimate = Query("craft_availability", ("item_type", ItemID.Torch), ("amount", 3), ("allow_nearby", true));
                Require(estimate.Completed >= 3 && estimate.Value.StartsWith("recipe:", StringComparison.Ordinal),
                    "real native current recipe sees permitted nearby Gel: " + estimate.Detail);
                Require(Query("craft_availability", ("item_type", ItemID.Torch), ("amount", 3)).Completed == 0,
                    "same recipe lacks material without nearby source permission");
                var passTorch = Step("transfer_item", ("item_type", ItemID.Torch), ("amount", "$previous"),
                    ("source", "self_inventory"), ("destination", "owner_inventory"));
                var deniedCraft = RunPlan(Step("craft_item", ("item_type", ItemID.Torch), ("amount", 3)), passTorch);
                Require(deniedCraft.State == "failed" && deniedCraft.Attempts.Count == 1 && chest.item[0].stack == 10 &&
                    Quantity(human.inventory, ItemID.Torch) == 0, "the later transfer does not run when the unauthorized craft cannot start");
                var craft = RunPlan(Step("craft_item", ("item_type", ItemID.Torch), ("amount", 3), ("allow_nearby", true)), passTorch);
                Require(craft.State == "succeeded" && craft.Attempts.Count == 2 && craft.Attempts[0].Completed == 3 &&
                    craft.Attempts[1].Completed == 3 && Quantity(body.inventory, ItemID.Torch) == 0 && Quantity(human.inventory, ItemID.Torch) == 3 &&
                    Quantity(body.inventory, ItemID.Wood) == 1 && chest.item[0].stack == 9 && ReferenceEquals(Main.mouseItem, mouse),
                    "real CraftItem consumes backpack wood and nearby chest Gel, then hands the verified output to the owner: " + craft.Detail);

                var sources = Query("resource_sources", ("item_type", ItemID.StoneBlock));
                string prefix = $"chest:{index}:{position.X}:{position.Y}:";
                string reference = sources.Detail.Split('，').FirstOrDefault(row => row.StartsWith(prefix, StringComparison.Ordinal))?.Split(' ')[0];
                Require(reference != null && reference.StartsWith("chest:", StringComparison.Ordinal),
                    "loaded chest has an opaque source reference: " + sources.State + " / " + sources.Detail + " / " + sources.Value +
                    " chest=" + position + " body=" + body.Bottom + " reachable=" + Call(host, "TaskChestReachable", actor, chest, true));
                bool startedOutside = !(bool)Call(host, "TaskChestReachable", actor, chest, true);
                Vector2 beforePull = body.Bottom;
                var pull = RunStep("transfer_item", ("item_type", ItemID.StoneBlock), ("amount", 4), ("source", reference),
                    ("destination", "self_inventory"));
                Require(pull.State == "succeeded" && chest.item[1].stack == 6 && Quantity(body.inventory, ItemID.StoneBlock) == 4 &&
                    (!startedOutside || Vector2.DistanceSquared(body.Bottom, beforePull) > 16 * 16),
                    "the real chest and player resource move together");
                VerifyPersonalBank(body, RunStep);
                NativeCompanionShimmerTaskChecks.Run(host, actor, body, anchor, tick);
                NativeCompanionCraftWireChecks.Run(host, actor, body, human, chest, anchor);
                Chest.RemoveChest(index);
                var replacement = Chest.CreateWorldChest(index, position.X, position.Y);
                replacement.item[1] = new Item(ItemID.StoneBlock) { stack = 99 };
                var stale = RunStep("transfer_item", ("item_type", ItemID.StoneBlock), ("amount", 1), ("source", reference),
                    ("destination", "self_inventory"));
                Require(stale.State == "failed" && replacement.item[1].stack == 99 && Quantity(body.inventory, ItemID.StoneBlock) == 4,
                    "same-coordinate/same-slot replacement cannot consume a stale source reference");

                body.inventory[20] = new Item(ItemID.HerbBag) { stack = 2 };
                var opened = RunStep("open_container", ("item_type", ItemID.HerbBag), ("amount", 1));
                Require(opened.State == "succeeded" && body.inventory[20].stack == 1 && ReferenceEquals(Main.mouseItem, mouse),
                    "the original ItemSlot consumes exactly one chosen bag and restores the human cursor");
                Console.WriteLine("PASS resource tasks: count/reserve, equipment return, native nearby craft, chest identity and original open bag.");
            }
            finally
            {
                Call(host, "StopTask", profile.OwnerId, profile.Id, null);
                if (index >= 0 && Main.chest[index] != null) Chest.RemoveChest(index);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        if (previousTiles[x, y] != null) Main.tile[position.X + x, position.Y + y] = previousTiles[x, y];
                Array.Copy(humanInventory, human.inventory, humanInventory.Length);
                Array.Copy(actorInventory, body.inventory, actorInventory.Length);
                body.armor[0] = oldHelmet;
                body.Bottom = previousFeet;
                body.velocity = previousVelocity;
                Call(Get(actor, "Behavior"), "PositionChanged", body);
                Player.Settings.CraftFromNearbyChests = nearby;
            }
        }

        private static void VerifyPersonalBank(Player body, Func<string, (string Key, object Value)[], CompanionTaskSnapshot> run)
        {
            Point feet = body.Bottom.ToTileCoordinates();
            var cells = new Tile[2, 2];
            var oldSafe = body.bank2.item.ToArray();
            int x0 = feet.X - 3, y0 = feet.Y - 2;
            try
            {
                for (int i = 0; i < body.bank2.item.Length; i++) body.bank2.item[i] = new Item();
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        cells[x, y] = Main.tile[x0 + x, y0 + y];
                        var safe = new Tile { type = TileID.Safes, frameX = (short)(18 * x), frameY = (short)(18 * y) };
                        safe.active(true);
                        Main.tile[x0 + x, y0 + y] = safe;
                    }
                var result = run("transfer_item", new (string Key, object Value)[] { ("item_type", ItemID.StoneBlock), ("amount", 2),
                    ("source", "self_inventory"), ("destination", "self_safe") });
                Require(result.State == "succeeded" && Quantity(body.bank2.item, ItemID.StoneBlock) == 2 && Quantity(body.inventory, ItemID.StoneBlock) == 2,
                    "the real safe entry admits only the personal actor bank: " + result.Detail);
                result = run("transfer_item", new (string Key, object Value)[] { ("item_type", ItemID.StoneBlock), ("amount", 2),
                    ("source", "self_safe"), ("destination", "self_inventory") });
                Require(result.State == "succeeded" && Quantity(body.bank2.item, ItemID.StoneBlock) == 0 && Quantity(body.inventory, ItemID.StoneBlock) == 4,
                    "bank withdrawal restores exact actor inventory and empty safe: " + result.Detail);
            }
            finally
            {
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        if (cells[x, y] != null) Main.tile[x0 + x, y0 + y] = cells[x, y];
                Array.Copy(oldSafe, body.bank2.item, oldSafe.Length);
            }
        }

        private static Dictionary<string, object> Map((string Key, object Value)[] arguments) =>
            arguments.ToDictionary(entry => entry.Key, entry => entry.Value is short value ? (object)(int)value : entry.Value, StringComparer.Ordinal);

        private static int Quantity(Item[] inventory, int type) => inventory.Where(item => item.type == type).Sum(item => item.stack);
    }
}
