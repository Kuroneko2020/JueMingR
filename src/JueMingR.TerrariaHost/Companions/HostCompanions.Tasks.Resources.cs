using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.ChestLocator;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const string ActorInventoryEndpoint = "self_inventory";
        private const string OwnerInventoryEndpoint = "owner_inventory";
        private static readonly (string Id, CompanionInventoryArea Area)[] BankTaskEndpoints =
        {
            ("self_piggy", CompanionInventoryArea.PiggyBank), ("self_safe", CompanionInventoryArea.Safe),
            ("self_forge", CompanionInventoryArea.DefenderForge), ("self_void", CompanionInventoryArea.VoidVault)
        };

        private sealed class TaskResourceEnd
        {
            internal bool Owner;
            internal CompanionInventoryArea Area;
            internal Item[] Items;
            internal int Count;
            internal BankAccess Bank;
            internal Chest Chest;
        }

        private sealed class TaskChestReference
        {
            internal Actor Actor;
            internal Chest Chest;
            internal int X, Y;
            internal long Session, Activation;
        }

        private readonly Dictionary<Guid, TaskChestReference> taskChestReferences = new Dictionary<Guid, TaskChestReference>();
        private readonly Queue<Guid> taskChestReferenceOrder = new Queue<Guid>();

        partial void RegisterResourceTaskOperations()
        {
            RegisterTaskObservation("resource_sources", "查当前队友、主人、可访问个人储存和附近世界箱的资源来源。Value 为首个可确认的有货端点。",
                QueryTaskResources, new TaskField("item_type", "integer", min: 1, max: ItemID.Count - 1));
            RegisterTaskOperation("transfer_item", "按数量和最低保留数移动物品。端点由 resource_sources 提供。联机世界箱和主人、个人银行或另一世界箱之间的转移，拆成两步并使用 self_inventory（队友背包）中转；第二步数量用第一步已确认数量。",
                TransferTaskItems, CancelTaskChestTransfer,
                new TaskField("item_type", "integer", true, 1, ItemID.Count - 1), new TaskField("amount", "integer", true, 1, 99999),
                new TaskField("source", required: true, min: 1, max: 64), new TaskField("destination", required: true, min: 1, max: 64),
                new TaskField("keep_amount", "integer", min: 0, max: 99999));
            RegisterTaskOperation("equip_item", "装备队友背包中的物品。可指定 armor、misc、dye 或 misc_dye，槽号和配装号从 0 开始。换下的物品回队友背包。",
                EquipTaskItem, null, new TaskField("item_type", "integer", true, 1, ItemID.Count - 1),
                new TaskField("amount", "integer", min: 1, max: 1), new TaskField("loadout", "integer", min: 0, max: 2),
                new TaskField("area", "string", false, 1, 12, "armor", "misc", "dye", "misc_dye"), new TaskField("slot", "integer", min: 0, max: 19));
            RegisterTaskOperation("unequip_item", "按槽把队友装备取回自己的背包。",
                UnequipTaskItem, null, new TaskField("slot", "integer", true, 0, 19),
                new TaskField("area", "string", false, 1, 12, "armor", "misc", "dye", "misc_dye"));
            RegisterTaskOperation("switch_loadout", "切换队友的原版配装组并核对实际组。",
                SwitchTaskLoadout, null, new TaskField("loadout", "integer", true, 0, 2));
        }

        private CompanionTaskOutcome QueryTaskResources(TaskExecution execution)
        {
            var actor = execution.Actor;
            int type = execution.Number("item_type");
            var details = new List<string>();
            string first = null;
            int firstQuantity = 0;
            foreach (string id in new[] { ActorInventoryEndpoint, OwnerInventoryEndpoint }.Concat(BankTaskEndpoints.Select(candidate => candidate.Id)))
            {
                if (!TryTaskResourceEnd(actor, id, out var endpoint, out _, observation: true)) continue;
                int count = CountResourceEndpoint(actor, endpoint, type);
                if (count > 0 && first == null) { first = id; firstQuantity = count; }
                details.Add(id + " " + count);
            }
            foreach (var native in NearbyChests.GetChestsInRangeOf(actor.Body.position))
            {
                var chest = native.chest;
                if (!TaskChestReachable(actor, chest, requireReach: false)) continue;
                bool inRange = TaskChestReachable(actor, chest);
                string id = DescribeTaskChest(actor, chest);
                if (Main.netMode == 1 && !ChestReceiveObserver.TaskContentsCurrent(chest))
                {
                    details.Add(id + (inRange ? " 待向服务器打开并确认内容" : " 需走近并向服务器确认内容"));
                    if (details.Count >= 20) break;
                    continue;
                }
                int count = CountResourceEndpoint(actor, new TaskResourceEnd { Chest = chest, Items = chest.item, Count = chest.maxItems }, type);
                if (inRange && count > 0 && first == null) { first = id; firstQuantity = count; }
                details.Add(id + " " + count + (inRange ? "" : "（需走近）"));
                if (details.Count >= 20) break;
            }
            return TaskOutcome("succeeded", details.Count == 0 ? "当前没有可确认的来源。" : String.Join("，", details), firstQuantity, first);
        }

        private string DescribeTaskChest(Actor actor, Chest chest)
        {
            foreach (var pair in taskChestReferences)
                if (ReferenceEquals(pair.Value.Actor, actor) && ReferenceEquals(pair.Value.Chest, chest) &&
                    pair.Value.Session == session && pair.Value.Activation == actor.Generation)
                    return FormattableString.Invariant($"chest:{chest.index}:{chest.x}:{chest.y}:{pair.Key:N}");
            var id = Guid.NewGuid();
            taskChestReferences.Add(id, new TaskChestReference { Actor = actor, Chest = chest, X = chest.x, Y = chest.y,
                Session = session, Activation = actor.Generation });
            taskChestReferenceOrder.Enqueue(id);
            if (taskChestReferenceOrder.Count > 256) taskChestReferences.Remove(taskChestReferenceOrder.Dequeue());
            return FormattableString.Invariant($"chest:{chest.index}:{chest.x}:{chest.y}:{id:N}");
        }

        private int CountResourceEndpoint(Actor actor, TaskResourceEnd endpoint, int type)
        {
            int count = 0;
            for (int slot = 0; slot < endpoint.Count; slot++)
            {
                var item = endpoint.Items[slot];
                if (item != null && !item.IsAir && !item.favorited && (type == 0 || item.type == type) && !ResourceBlocked(actor, endpoint, slot)) count += item.stack;
            }
            return count;
        }

        private bool TryTaskResourceEnd(Actor actor, string id, out TaskResourceEnd endpoint, out string reason, bool observation = false,
            bool requireReach = true)
        {
            endpoint = null;
            reason = "来源或目的地无效。";
            if (id == ActorInventoryEndpoint || id == OwnerInventoryEndpoint)
            {
                bool human = id == OwnerInventoryEndpoint;
                if (human && (observation ? !ItemCommandOwnerCurrent() || owner.dead || owner.ghost : !ItemOwnerCurrent()))
                { reason = "主人背包当前不可操作。"; return false; }
                endpoint = new TaskResourceEnd { Owner = human, Area = CompanionInventoryArea.Inventory,
                    Items = (human ? owner : actor.Body).inventory, Count = 58 };
                return true;
            }
            foreach (var candidate in BankTaskEndpoints)
            {
                if (id != candidate.Id) continue;
                var access = ActorTileReachReady(actor.Body) ? SearchBank(actor, candidate.Area) : null;
                if (!BankCurrent(access, candidate.Area)) { reason = MissingBank(candidate.Area); return false; }
                var items = CompanionResources.Items(actor.Body, candidate.Area);
                endpoint = new TaskResourceEnd { Area = candidate.Area, Bank = access, Items = items, Count = items.Length };
                return true;
            }
            string[] parts = id.Split(':');
            if (parts.Length != 5 || parts[0] != "chest" || !TryEndpointCoordinate(parts[1], out int index) ||
                !TryEndpointCoordinate(parts[2], out int x) || !TryEndpointCoordinate(parts[3], out int y) || index >= Main.chest.Length ||
                !Guid.TryParseExact(parts[4], "N", out Guid marker) || !taskChestReferences.TryGetValue(marker, out var found) ||
                found.X != x || found.Y != y || !ReferenceEquals(found.Actor, actor) || found.Activation != actor.Generation || found.Session != session) return false;
            var chest = Main.chest[index];
            if (chest == null || !ReferenceEquals(found.Chest, chest) || chest.x != x || chest.y != y || !TaskChestReachable(actor, chest, requireReach))
            { reason = "指定箱子已变化、锁住、有人使用或超出交互范围。"; return false; }
            endpoint = new TaskResourceEnd { Chest = chest, Items = chest.item, Count = chest.maxItems };
            return true;
        }

        private static bool TryEndpointCoordinate(string text, out int value) =>
            Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

        private bool TaskChestReachable(Actor actor, Chest chest, bool requireReach = true)
        {
            if (chest == null || chest.bankChest || chest.index < 0 || chest.index >= Main.chest.Length ||
                !ReferenceEquals(Main.chest[chest.index], chest) || requireReach && !ActorTileReachReady(actor.Body) ||
                !WorldTileKnowledge.CellKnown(chest.x, chest.y)) return false;
            var tile = Main.tile[chest.x, chest.y];
            return tile.active() && TileID.Sets.BasicChest[tile.type] &&
                TileObjectData.TryGetTileBounds(chest.x, chest.y, out Rectangle bounds) && bounds.X == chest.x && bounds.Y == chest.y &&
                WorldTileKnowledge.RegionKnown(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom) &&
                (!requireReach || HitboxInReach(actor.Body, ActorTileReach(actor.Body), new Rectangle(bounds.X * 16, bounds.Y * 16, bounds.Width * 16, bounds.Height * 16))) &&
                CraftingRequests.CanCraftFromChest(chest, actor.Body.whoAmI);
        }

        private CompanionItemDraft TaskItemDraft(Actor actor, Func<CompanionInventoryArea, bool> bankCurrent = null) =>
            new CompanionItemDraft(owner, actor.Body, actor.Profile.Id, actor.Generation, session, world,
                (human, area, slot) => TaskResourceSlotBlocked(actor, human, area, slot), bankCurrent: bankCurrent);

        private bool TaskResourceSlotBlocked(Actor actor, bool human, CompanionInventoryArea area, int slot)
        {
            var body = human ? owner : actor.Body;
            var items = CompanionResources.Items(body, area);
            return !human && area == CompanionInventoryArea.Inventory && TaskChestSourceHeld(actor, slot) ||
                itemDraft?.DependsOn(items, slot) == true || ItemSlotBlocked(human, body, area, slot);
        }

        private CompanionTaskOutcome TransferTaskItems(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (execution.State is TaskChestTransfer pending) return AdvanceTaskChestTransfer(execution, pending);
            if (!TryTaskResourceEnd(actor, execution.Text("source"), out var source, out var reason, requireReach: false) ||
                !TryTaskResourceEnd(actor, execution.Text("destination"), out var destination, out reason, requireReach: false)) return TaskOutcome("failed", reason);
            if (ReferenceEquals(source.Items, destination.Items)) return TaskOutcome("failed", "来源和目的地是同一个容器。");
            if (source.Chest != null && destination.Chest != null) return TaskOutcome("failed", "两个世界箱之间需要以队友背包为中转。");
            var chest = source.Chest ?? destination.Chest;
            if (Main.netMode == 1 && chest != null)
            {
                var backpack = source.Chest != null ? destination : source;
                if (backpack.Owner || backpack.Area != CompanionInventoryArea.Inventory)
                    return TaskOutcome("failed", "联机世界箱请用队友背包中转，再按已确认数量移动到主人或个人储存。");
            }
            if (chest != null && !TaskChestReachable(actor, chest)) return ApproachTaskChest(execution, chest);
            if (Main.netMode == 1 && chest != null)
                return BeginTaskChestTransfer(execution, source, destination);
            return CommitTaskTransfer(execution, source, destination);
        }

        private CompanionTaskOutcome ApproachTaskChest(TaskExecution execution, Chest chest)
        {
            var actor = execution.Actor;
            if (Main.GameUpdateCount - execution.StartedAt > 3600)
                return TaskOutcome("failed", "在任务期限内未进入世界箱的原版交互范围。");
            if (!(execution.State is ArrivalSearch near))
            {
                if (actor.Fishing != null) EndFishingActivity(actor);
                SetTaskTravel(actor, null);
                execution.State = near = new ArrivalSearch { X = chest.x, Y = chest.y + 2, HorizontalRadius = 5,
                    VerticalRadius = 5, Name = "指定世界箱" };
            }
            if (actor.TaskTravel == null || actor.TaskTravel.Temporary)
            {
                if (!FindTaskFooting(actor, near, out Vector2 feet, out bool exhausted))
                    return TaskOutcome(exhausted ? "failed" : "waiting", exhausted ? "箱子周围没有可站立的互动位置。" : "正在核对世界箱附近地形。");
                actor.TaskControlId = execution.Job.Id;
                SetTaskTravel(actor, new TaskTravel { Feet = feet, Name = near.Name });
            }
            return actor.TaskTravel.Arrived && ActorTileReachReady(actor.Body) ? TaskOutcome("failed", "已到邻近位置，箱子仍超出原版交互范围。") :
                TaskOutcome("running", "正在走向指定世界箱。");
        }

        private CompanionTaskOutcome CommitTaskTransfer(TaskExecution execution, TaskResourceEnd source, TaskResourceEnd destination,
            TaskChestTransfer remote = null)
        {
            var actor = execution.Actor;
            bool BanksCurrent(CompanionInventoryArea area) => BankCurrent(source.Area == area ? source.Bank : destination.Bank, area);
            var draft = TaskItemDraft(actor, BanksCurrent);
            Item[] from = ProjectResource(draft, source);
            Item[] to = ProjectResource(draft, destination);
            int type = execution.Number("item_type"), amount = execution.Number("amount") - (remote?.Completed ?? 0);
            int present = from.Where(item => !item.IsAir && item.type == type).Sum(item => item.stack);
            int target = Math.Min(amount, Math.Max(0, present - execution.Number("keep_amount")));
            int moved = 0;
            for (int s = 0; s < from.Length && moved < target; s++)
            {
                Item item = from[s];
                if (item.IsAir || item.type != type || item.favorited || ResourceBlocked(actor, source, s)) continue;
                int request = Math.Min(item.stack, target - moved);
                int placed = PlaceResource(actor, destination, to, item, request, remote != null && destination.Chest != null);
                if (placed == 0) continue;
                item.stack -= placed;
                moved += placed;
                if (item.stack == 0) from[s] = new Item();
                if (remote != null) break; // One original chest slot per acknowledged update.
            }
            if (moved == 0) return TaskOutcome("failed", "没有可移动的数量。请核对保留数、锁定槽与目的地空间。");
            if (source.Chest == null) draft.Stage(source.Owner, source.Area, from);
            if (destination.Chest == null) draft.Stage(destination.Owner, destination.Area, to);
            if (!draft.TryPrepare(out var changes) || source.Chest != null && !TaskChestReachable(actor, source.Chest) ||
                destination.Chest != null && !TaskChestReachable(actor, destination.Chest)) return TaskOutcome("waiting", "槽位或容器已变化，即将重新核对。");
            if (remote != null) return PrepareTaskChestSlot(remote, source.Chest != null ? from : to, moved, changes);
            execution.MarkSubmitted();
            if (source.Chest != null) ApplyTaskChestSlots(source, from);
            if (destination.Chest != null) ApplyTaskChestSlots(destination, to);
            foreach (var change in changes) change.Apply();
            FinishItemCommit(actor, changes, "");
            return TaskOutcome(moved == amount ? "succeeded" : "partial", "实际移动 " + moved + " 件。" +
                (moved < amount ? "剩余数量受保留数、可用物品或目标空间限制。" : ""), moved);
        }

        private static Item[] ProjectResource(CompanionItemDraft draft, TaskResourceEnd endpoint) => endpoint.Chest == null ?
            draft.Project(endpoint.Owner, endpoint.Area, endpoint.Count) : endpoint.Items.Take(endpoint.Count).Select(item => item?.Clone() ?? new Item()).ToArray();

        private static void ApplyTaskChestSlots(TaskResourceEnd endpoint, Item[] projection)
        {
            for (int slot = 0; slot < projection.Length; slot++)
                if (endpoint.Items[slot] == null || projection[slot].IsNetStateDifferent(endpoint.Items[slot])) endpoint.Items[slot] = projection[slot];
        }

        private bool ResourceBlocked(Actor actor, TaskResourceEnd endpoint, int slot) => endpoint.Chest == null &&
            TaskResourceSlotBlocked(actor, endpoint.Owner, endpoint.Area, slot);

        private int PlaceResource(Actor actor, TaskResourceEnd destination, Item[] projected, Item item, int amount, bool oneSlot = false)
        {
            int placed = 0;
            for (int pass = 0; pass < 2 && placed < amount; pass++)
                for (int slot = 0; slot < projected.Length && placed < amount; slot++)
                {
                    var current = projected[slot];
                    if ((pass == 1) != current.IsAir || ResourceBlocked(actor, destination, slot) ||
                        !current.IsAir && (current.favorited || !Item.CanStack(current, item))) continue;
                    if (destination.Chest == null && !CompanionItemRules.Accepts(destination.Owner ? owner : actor.Body,
                        destination.Area, slot, item, (area, index) => projected[index])) continue;
                    int added = Math.Min(amount - placed, current.IsAir ? item.maxStack : current.maxStack - current.stack);
                    if (added <= 0) continue;
                    if (current.IsAir)
                    {
                        current = projected[slot] = item.Clone();
                        current.stack = 0;
                    }
                    current.stack += added;
                    placed += added;
                    if (oneSlot) return placed;
                }
            return placed;
        }

        private static CompanionInventoryArea TaskEquipmentArea(string area)
        {
            switch (area)
            {
                case "misc": return CompanionInventoryArea.Misc;
                case "dye": return CompanionInventoryArea.Dye;
                case "misc_dye": return CompanionInventoryArea.MiscDye;
                default: return CompanionInventoryArea.Armor;
            }
        }

        private CompanionTaskOutcome SwitchTaskLoadout(TaskExecution execution)
        {
            int requested = execution.Number("loadout");
            var actor = execution.Actor;
            if (actor.Body.CurrentLoadoutIndex == requested) return TaskOutcome("succeeded", "当前配装已切到第 " + (requested + 1) + " 组。", 1);
            if (!FishingHandFree(actor.Body) || actor.Body.IsStackingItems()) return TaskOutcome("waiting", "等队友结束手中动作后切换配装。");
            if (!TrySwitchCompanionLoadout(actor.Profile.Id, requested) || actor.Body.CurrentLoadoutIndex != requested)
            {
                // A task waits and retries its own request. Do not leave an
                // independent manual switch behind when the task is cancelled.
                if (actor.FishingLine.Gear.ManualLoadout == requested) actor.FishingLine.Gear.ManualLoadout = -1;
                return TaskOutcome("failed", "当前无法切换到指定配装。");
            }
            execution.MarkSubmitted();
            return TaskOutcome("succeeded", "已切换到第 " + (requested + 1) + " 组配装。", 1);
        }

        private CompanionTaskOutcome EquipTaskItem(TaskExecution execution)
        {
            var actor = execution.Actor;
            int loadout = execution.Number("loadout", actor.Body.CurrentLoadoutIndex);
            if (loadout != actor.Body.CurrentLoadoutIndex)
                return TaskOutcome("failed", "请先用 switch_loadout 切到指定配装，再装备物品。");
            return ChangeTaskEquipment(execution, equip: true);
        }

        private CompanionTaskOutcome UnequipTaskItem(TaskExecution execution) => ChangeTaskEquipment(execution, equip: false);

        private CompanionTaskOutcome ChangeTaskEquipment(TaskExecution execution, bool equip)
        {
            var actor = execution.Actor;
            if (!FishingHandFree(actor.Body)) return TaskOutcome("waiting", "等队友结束手中动作后调整装备。");
            string requestedArea = execution.Text("area");
            var draft = TaskItemDraft(actor);
            var inventory = new TaskResourceEnd { Area = CompanionInventoryArea.Inventory, Items = actor.Body.inventory, Count = 58 };
            Item[] bag = ProjectResource(draft, inventory);
            int slot = execution.Number("slot", -1), type = execution.Number("item_type");
            int source = equip ? Array.FindIndex(bag, item => !item.IsAir && !item.favorited && item.type == type) : -1;
            if (equip && (source < 0 || ResourceBlocked(actor, inventory, source))) return TaskOutcome("failed", "队友背包没有可装备的指定物品。");
            var areas = equip && requestedArea == null ? new[] { CompanionInventoryArea.Armor, CompanionInventoryArea.Misc, CompanionInventoryArea.Dye, CompanionInventoryArea.MiscDye } :
                new[] { TaskEquipmentArea(requestedArea) };
            foreach (var area in areas)
            {
                var equipped = draft.Project(false, area, CompanionResources.Items(actor.Body, area).Length);
                int chosen = slot >= 0 ? slot : Array.FindIndex(equipped, item => item.IsAir);
                if (slot < 0)
                    chosen = Enumerable.Range(0, equipped.Length).Where(index => index < 10 &&
                        !TaskResourceSlotBlocked(actor, false, area, index) && !equipped[index].favorited &&
                        CompanionItemRules.Accepts(actor.Body, area, index, One(bag[source]), (account, i) => equipped[i]))
                        .OrderBy(index => equipped[index].IsAir ? 0 : 1).DefaultIfEmpty(-1).First();
                if (chosen < 0 || chosen >= equipped.Length || TaskResourceSlotBlocked(actor, false, area, chosen) || equipped[chosen].favorited) continue;
                var old = equipped[chosen];
                var incoming = equip ? One(bag[source]) : new Item();
                if (equip && !CompanionItemRules.Accepts(actor.Body, area, chosen, incoming, (account, index) => equipped[index]) || !equip && old.IsAir) continue;
                if (equip && --bag[source].stack == 0) bag[source] = new Item();
                if (!old.IsAir && PlaceResource(actor, inventory, bag, old, old.stack) != old.stack)
                    return TaskOutcome("failed", "队友背包空间不足，装备没有改变。");
                equipped[chosen] = incoming;
                draft.Stage(false, area, equipped);
                draft.Stage(false, CompanionInventoryArea.Inventory, bag);
                if (!draft.TryPrepare(out var changes) || changes.Count == 0) return TaskOutcome("failed", "装备或背包槽已变化。");
                execution.MarkSubmitted();
                foreach (var change in changes) change.Apply();
                FinishItemCommit(actor, changes, "");
                return TaskOutcome("succeeded", equip ? "物品已装备到指定槽。" : "装备已放回队友背包。", 1);
            }
            return TaskOutcome("failed", "找不到可用的装备槽，或原版装备限制不满足。");
        }

        private static Item One(Item item)
        {
            var single = item.Clone();
            single.stack = 1;
            return single;
        }
    }
}
