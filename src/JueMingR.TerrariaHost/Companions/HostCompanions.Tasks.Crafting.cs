using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.ChestLocator;
using JueMingR.TerrariaHost.World;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Net;
using Terraria.WorldBuilding;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private readonly Dictionary<Actor, NativeTaskCraft> taskCrafts = new Dictionary<Actor, NativeTaskCraft>();
        private static readonly Harmony TaskCraftHarmony = new Harmony("JueMingR.Companions.TaskCrafting");

        private static readonly AccessTools.FieldRef<Dictionary<int, int>> CraftOwned = AccessTools.StaticFieldRefAccess<Dictionary<int, int>>(
            AccessTools.Field(typeof(Recipe), "_ownedItems"));
        private static readonly AccessTools.FieldRef<List<Chest>> CraftChests = AccessTools.StaticFieldRefAccess<List<Chest>>(
            AccessTools.Field(typeof(Recipe), "_recipeChests"));
        private static readonly AccessTools.FieldRef<Queue<CraftingRequests.RemoteCraftRequest>> CraftPending =
            AccessTools.StaticFieldRefAccess<Queue<CraftingRequests.RemoteCraftRequest>>(AccessTools.Field(typeof(CraftingRequests), "_pendingCrafts"));
        private static readonly AccessTools.FieldRef<int> CraftFakeCursorType = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(FakeCursorItem), "_type"));
        private static readonly AccessTools.FieldRef<int> CraftFakeCursorStack = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(FakeCursorItem), "_stack"));
        private static readonly AccessTools.FieldRef<int> CraftFakeCursorPrefix = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(FakeCursorItem), "_prefix"));
        private static readonly AccessTools.FieldRef<int> CraftEffectType = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(CraftingEffects), "_justCraftedItemType"));
        private static readonly AccessTools.FieldRef<float> CraftEffectGlow = AccessTools.StaticFieldRefAccess<float>(AccessTools.Field(typeof(CraftingEffects), "_mouseItemGlow"));
        private static readonly Action<Player> CollectCraftMaterials = (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),
            AccessTools.Method(typeof(Recipe), "CollectItemsToCraftWithFrom"));
        private static readonly Action<Item[], int> CountCraftMaterials = (Action<Item[], int>)Delegate.CreateDelegate(typeof(Action<Item[], int>),
            AccessTools.Method(typeof(Recipe), "CollectItems", new[] { typeof(Item[]), typeof(int) }));
        private static readonly Action CountCraftGroups = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(typeof(Recipe), "AddFakeCountsForItemGroups"));
        private static readonly Action<bool> SettleCraftWithoutSend = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>),
            AccessTools.Method(typeof(CraftingRequests), "HandleResponse"));
        private static readonly AccessTools.FieldRef<Recipe, Recipe.RequiredItemEntry[]> CraftRequirements =
            AccessTools.FieldRefAccess<Recipe, Recipe.RequiredItemEntry[]>("requiredItemQuickLookup");

        [ThreadStatic] private static NativeTaskCraft craftingTask;
        private static bool craftHooksInstalled;
        partial void RegisterTaskProcessing();

        private sealed class NativeTaskCraft
        {
            internal TaskExecution Execution;
            internal readonly Queue<CraftingRequests.RemoteCraftRequest> Pending = new Queue<CraftingRequests.RemoteCraftRequest>();
            internal CompanionConnection Connection;
            internal Recipe Recipe;
            internal byte[] Frame;
            internal int Completed, Granted;
            internal uint Deadline;
            internal bool Cancelled;
        }

        private sealed class NativeTaskCraftScope : IDisposable
        {
            private readonly Dictionary<int, int> previousCounts;
            private readonly List<Chest> previousChests;
            private readonly Queue<CraftingRequests.RemoteCraftRequest> previousPending;
            private readonly Item previousCursor;
            private readonly int cursorType, cursorStack, cursorPrefix, effectType;
            private readonly float effectGlow;
            private readonly NativeTaskCraft previousCraft;
            private readonly CompanionHooks.Scope ownerScope;

            internal NativeTaskCraftScope(Actor actor, NativeTaskCraft work = null)
            {
                ownerScope = work == null ? null : CompanionHooks.Enter(actor, actor.Body.Center, inventory: true);
                previousCounts = CraftOwned();
                previousChests = CraftChests();
                previousPending = CraftPending();
                previousCursor = Main.mouseItem;
                cursorType = CraftFakeCursorType();
                cursorStack = CraftFakeCursorStack();
                cursorPrefix = CraftFakeCursorPrefix();
                effectType = CraftEffectType();
                effectGlow = CraftEffectGlow();
                previousCraft = craftingTask;
                CraftOwned() = new Dictionary<int, int>();
                CraftChests() = new List<Chest>();
                CraftPending() = work?.Pending ?? new Queue<CraftingRequests.RemoteCraftRequest>();
                if (work != null)
                {
                    craftingTask = work;
                    Main.mouseItem = new Item();
                    FakeCursorItem.Reset();
                }
            }

            public void Dispose()
            {
                CraftOwned() = previousCounts;
                CraftChests() = previousChests;
                CraftPending() = previousPending;
                Main.mouseItem = previousCursor;
                CraftFakeCursorType() = cursorType;
                CraftFakeCursorStack() = cursorStack;
                CraftFakeCursorPrefix() = cursorPrefix;
                CraftEffectType() = effectType;
                CraftEffectGlow() = effectGlow;
                craftingTask = previousCraft;
                ownerScope?.Dispose();
            }
        }

        partial void RegisterCraftTaskOperations()
        {
            var output = new TaskField("item_type", "integer", true, 1, ItemID.Count - 1);
            var recipe = new TaskField("recipe_id", "integer", min: 0, max: 65535);
            var nearby = new TaskField("allow_nearby", "boolean");
            RegisterTaskObservation("craft_availability", "查指定成品的当前原版配方条件、实际材料与可制作数量。附近储存须显式 allow_nearby。",
                ObserveTaskCraft, output, new TaskField("amount", "integer", min: 1, max: 99999), recipe, nearby);
            RegisterTaskOperation("craft_item", "使用原版配方和队友的实际材料制作至少指定成品数量。允许附近储存须显式 allow_nearby。",
                CraftTaskItem, CancelTaskCraft, output, new TaskField("amount", "integer", true, 1, 99999), recipe, nearby);
            RegisterTaskProcessing();
        }

        private CompanionTaskOutcome ObserveTaskCraft(TaskExecution execution)
        {
            using (new NativeTaskCraftScope(execution.Actor))
            {
                if (!PrepareCraftScene(execution, out var missing)) return TaskOutcome("waiting", missing);
                var pick = ChooseCraftRecipe(execution, execution.Number("amount", 1), out int count, out string reason);
                return TaskOutcome("succeeded", reason, count, pick >= 0 ? "recipe:" + pick : null);
            }
        }

        private bool PrepareCraftScene(TaskExecution execution, out string reason)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            reason = "等待队友周围制作站和环境地形加载。";
            if (!RefreshActorEnvironment(actor)) return false;
            var reach = WorldUtils.ClampToWorld(TileReachCheckSettings.Simple.GetTileRegion(body, body.ateArtisanBread ? 4 : 0));
            if (!WorldTileKnowledge.RegionKnown(reach.Left, reach.Top, reach.Right + 1, reach.Bottom + 1)) return false;
            body.AdjTiles();
            bool allowed = execution.Flag("allow_nearby") && Player.Settings.CraftFromNearbyChests;
            // The original nearby-bank scan indexes every tile in its range.
            // Let main inventory recipes progress while that region is unknown.
            var center = body.Center.ToTileCoordinates();
            var banks = WorldUtils.ClampToWorld(new Microsoft.Xna.Framework.Rectangle(center.X - 40, center.Y - 40, 81, 81));
            bool enableNear = allowed && WorldTileKnowledge.RegionKnown(banks.Left, banks.Top, banks.Right, banks.Bottom);
            bool original = Player.Settings.CraftFromNearbyChests;
            try
            {
                Player.Settings.CraftFromNearbyChests = enableNear;
                CollectCraftMaterials(body);
            }
            finally { Player.Settings.CraftFromNearbyChests = original; }

            var chests = CraftChests();
            if (chests.RemoveAll(chest => !CraftSourceCurrent(actor, chest, enableNear)) > 0)
            {
                // Original collection included the candidate list. Rebuild
                // the native counts and recipe groups from admitted sources.
                CraftOwned().Clear();
                CountCraftMaterials(body.inventory, 58);
                foreach (var chest in chests) CountCraftMaterials(chest.item, chest.maxItems);
                CountCraftGroups();
                CraftingRequests.SubtractPendingRequests();
            }
            reason = null;
            return true;
        }

        private bool CraftSourceCurrent(Actor actor, Chest chest, bool allowNearby)
        {
            if (chest == null) return false;
            if (chest.bankChest) return ReferenceEquals(chest, actor.Body.bank4) ? actor.Body.disableVoidBag < 0 : allowNearby;
            if (!allowNearby || !WorldTileKnowledge.CellKnown(chest.x, chest.y) || chest.index < 0 || chest.index >= Main.chest.Length ||
                !ReferenceEquals(Main.chest[chest.index], chest) || !CraftingRequests.CanCraftFromChest(chest, actor.Body.whoAmI)) return false;
            var origin = Main.tile[chest.x, chest.y];
            return origin.active() && TileID.Sets.BasicChest[origin.type] &&
                (Main.netMode != 1 || ChestReceiveObserver.TaskContentsCurrent(chest));
        }

        private int ChooseCraftRecipe(TaskExecution execution, int amount, out int count, out string reason)
        {
            int recipe = execution.Number("recipe_id", -1), type = execution.Number("item_type"), best = -1;
            count = 0;
            reason = "当前版本未找到指定产物的配方。";
            var body = execution.Actor.Body;
            int end = recipe >= 0 ? Math.Min(Recipe.numRecipes, recipe + 1) : Recipe.numRecipes;
            for (int i = Math.Max(0, recipe); i < end; i++)
            {
                var candidate = Main.recipe[i];
                if (candidate?.createItem?.type != type || candidate.createItem.stack <= 0) continue;
                var missing = new List<string>();
                if (!candidate.PlayerMeetsEnvironmentConditions(body, missing))
                {
                    if (best == -1) reason = missing.Count > 0 ? "缺少当前制作条件：" + String.Join("、", missing) : "当前世界或人物条件不允许此配方。";
                    continue;
                }
                if (!Recipe.CollectedEnoughItemsToCraft(candidate))
                {
                    if (best == -1) reason = execution.Flag("allow_nearby") ? "当前已确认可用材料不足，附近箱子须已同步、解锁且空闲。" :
                        "队友背包和原版可用虚空袋的材料不足。";
                    continue;
                }
                int batches = CountTaskCraftBatches(execution.Actor, candidate, Math.Min(128, (amount + candidate.createItem.stack - 1) / candidate.createItem.stack));
                int available = batches * candidate.createItem.stack;
                if (available <= count)
                {
                    if (count == 0) reason = "原版扣料次序会触及收藏、占用槽或不够分配的配方组。";
                    continue;
                }
                count = available;
                best = i;
                reason = "可按原版配方制作 " + count + " 件。";
                if (count >= amount) break;
            }
            return best;
        }

        private int CountTaskCraftBatches(Actor actor, Recipe recipe, int limit)
        {
            var sources = new List<(Item Item, bool Protected, int Left)>();
            void Add(Item[] items, int count, CompanionInventoryArea? area)
            {
                for (int index = 0; index < count; index++)
                {
                    var item = items[index];
                    if (item == null || item.IsAir) continue;
                    sources.Add((item, item.favorited || area.HasValue && TaskResourceSlotBlocked(actor, false, area.Value, index), item.stack));
                }
            }
            Add(actor.Body.inventory, 58, CompanionInventoryArea.Inventory);
            foreach (var chest in CraftChests())
            {
                CompanionInventoryArea? area = null;
                foreach (var entry in BankTaskEndpoints)
                    if (ReferenceEquals(CompanionResources.Items(actor.Body, entry.Area), chest.item)) { area = entry.Area; break; }
                Add(chest.item, chest.maxItems, area);
            }
            // Forecast only. Vanilla still chooses and debits the materials,
            // including its alchemy discount RNG, during the real craft.
            var requirements = CraftRequirements(recipe);
            for (int batch = 0; batch < limit; batch++)
                foreach (var ingredient in requirements)
                {
                    if (ingredient.itemIdOrRecipeGroup == 0) break;
                    int left = ingredient.stack;
                    for (int s = 0; s < sources.Count && left > 0; s++)
                    {
                        var source = sources[s];
                        if (source.Left <= 0 || !ingredient.Matches(source.Item.type)) continue;
                        if (source.Protected) return batch;
                        int used = Math.Min(left, source.Left);
                        left -= used;
                        sources[s] = (source.Item, source.Protected, source.Left - used);
                    }
                    if (left != 0) return batch;
                }
            return limit;
        }

        private CompanionTaskOutcome CraftTaskItem(TaskExecution execution)
        {
            var actor = execution.Actor;
            int amount = execution.Number("amount");
            if (!(execution.State is NativeTaskCraft work))
            {
                if (taskCrafts.ContainsKey(actor)) return TaskOutcome("waiting", "正在等待上次制作的原版回执。");
                execution.State = work = new NativeTaskCraft { Execution = execution, Connection = actor.Connection };
            }
            if (work.Pending.Count != 0)
                return TaskOutcome(Main.GameUpdateCount >= work.Deadline ? "unconfirmed" : "waiting", "正在等待服务器原版制作回执。", work.Completed);
            if (work.Completed >= amount) return CompletedTaskCraft(work);
            if (!FishingHandFree(actor.Body) || actor.Body.IsStackingItems() || actor.Body.chest != -1)
                return TaskOutcome("waiting", "等待队友结束手中动作和容器操作。", work.Completed);
            if (Main.netMode == 1 && (actor.Connection == null || actor.Connection.Finished || !EnsureCraftHooks()))
                return TaskOutcome("failed", "队友的原版制作连接未就绪。", work.Completed);
            if (!EnsureCraftHooks()) return TaskOutcome("failed", "原版制作结果接口未就绪。", work.Completed);
            string failure = null;
            using (new NativeTaskCraftScope(actor, work))
            {
                if (!PrepareCraftScene(execution, out failure)) return TaskOutcome("waiting", failure, work.Completed);
                int index = ChooseCraftRecipe(execution, 1, out _, out failure);
                if (index < 0) return TaskOutcome(work.Completed > 0 ? "partial" : "failed", failure, work.Completed);
                work.Recipe = Main.recipe[index];
                work.Frame = null;
                work.Granted = 0;
                execution.MarkSubmitted();
                try
                {
                    CraftingRequests.CraftItem(work.Recipe, 1, quickCraft: true);
                    FinishNativeCraftCursor(actor);
                    if (work.Pending.Count != 0)
                    {
                        if (work.Frame == null) SettleCraftWithoutSend(false);
                        else
                        {
                            // Record before relaying. An ambiguous relay failure
                            // must retain the debit and wait instead of retrying.
                            taskCrafts[actor] = work;
                            work.Deadline = Main.GameUpdateCount + 600;
                            bool queued;
                            try { queued = work.Connection.TrySend(work.Frame); }
                            catch (CompanionConnection.SendQueueFullException) when (!work.Connection.IsRelay) { queued = false; }
                            if (!queued) { taskCrafts.Remove(actor); SettleCraftWithoutSend(false); }
                            else return TaskOutcome("waiting", "制作已交给队友连接，等待服务器确认材料。", work.Completed);
                        }
                        failure = "服务器制作请求未发出，原版已退回本次扣料。";
                    }
                }
                finally { actor.Dirty = true; }
            }
            work.Completed += work.Granted;
            InvalidateItemSources(actor);
            if (work.Granted == 0) return TaskOutcome(work.Completed > 0 ? "partial" : "failed", failure ?? "原版未发放指定产物。", work.Completed);
            return work.Completed >= amount ? CompletedTaskCraft(work) :
                TaskOutcome("running", "已制作 " + work.Completed + " 件。", work.Completed);
        }

        private static CompanionTaskOutcome CompletedTaskCraft(NativeTaskCraft work) => TaskOutcome("succeeded",
            "实际制作 " + work.Completed + " 件。", work.Completed, work.Completed.ToString(CultureInfo.InvariantCulture));

        private static bool EnsureCraftHooks()
        {
            if (craftHooksInstalled) return true;
            try
            {
                TaskCraftHarmony.Patch(AccessTools.Method(typeof(NetManager), nameof(NetManager.SendToServer)),
                    prefix: new HarmonyMethod(typeof(HostCompanions), nameof(CaptureNativeCraftFrame)));
                TaskCraftHarmony.Patch(AccessTools.Method(typeof(Main), nameof(Main.CraftItem_GrantItem)),
                    postfix: new HarmonyMethod(typeof(HostCompanions), nameof(ObserveNativeCraftGrant)));
                return craftHooksInstalled = true;
            }
            catch (Exception) { TaskCraftHarmony.UnpatchAll(TaskCraftHarmony.Id); return false; }
        }

        private static bool CaptureNativeCraftFrame(NetPacket __0)
        {
            var work = craftingTask;
            if (work == null || __0.Id != NetManager.Instance.GetId<CraftingRequests.NetCraftingRequestsModule>()) return true;
            try
            {
                __0.ShrinkToFit();
                work.Frame = new byte[__0.Length];
                Buffer.BlockCopy(__0.Buffer.Data, 0, work.Frame, 0, __0.Length);
            }
            finally { __0.Recycle(); }
            return false;
        }

        private static void ObserveNativeCraftGrant(Recipe __0, bool __runOriginal)
        {
            if (__runOriginal && craftingTask != null && ReferenceEquals(craftingTask.Recipe, __0)) craftingTask.Granted += __0.createItem.stack;
        }

        private static void FinishNativeCraftCursor(Actor actor)
        {
            // The native grant may retain a remainder in the actor's borrowed
            // cursor. Keep its actual Item and let vanilla return or drop it.
            if (!Main.mouseItem.IsAir) actor.Body.GetOrDropItem(Main.mouseItem, GetItemSettings.ReturnItemShowAsNewNoCoinMerge);
            Main.mouseItem = new Item();
        }

        private static void SettleNativeCraftReceipt(bool accepted)
        {
            var module = (CraftingRequests.NetCraftingRequestsModule)NetManager.Instance.GetModule<CraftingRequests.NetCraftingRequestsModule>();
            using (var stream = new MemoryStream(new[] { accepted ? (byte)1 : (byte)0 }))
            using (var reader = new BinaryReader(stream)) module.DeserializeResponse(reader);
        }

        private bool ConsumeTaskCraftResponse(Actor actor, byte[] packet)
        {
            if (packet.Length != 4 || packet[0] != MessageID.NetModules ||
                BitConverter.ToUInt16(packet, 1) != NetManager.Instance.GetId<CraftingRequests.NetCraftingRequestsModule>()) return false;
            if (!taskCrafts.TryGetValue(actor, out var work) || work.Pending.Count != 1 || !ReferenceEquals(actor.Connection, work.Connection)) return true;
            taskCrafts.Remove(actor);
            if (actor.Closing || work.Execution.Job.Identity.DataEpoch != actor.Profile.DataEpoch || work.Execution.Job.Identity.WorldSession != session)
            {
                work.Execution.Settle(TaskOutcome("unconfirmed", "制作回执到达时身体已退役。", work.Completed));
                return true;
            }
            using (new NativeTaskCraftScope(actor, work))
            {
                work.Granted = 0;
                SettleNativeCraftReceipt(packet[3] != 0);
                FinishNativeCraftCursor(actor);
            }
            actor.Dirty = true;
            InvalidateItemSources(actor);
            work.Completed += work.Granted;
            int amount = work.Execution.Number("amount");
            if (work.Completed >= amount) work.Execution.Settle(CompletedTaskCraft(work));
            else if (work.Cancelled || work.Granted == 0)
                work.Execution.Settle(TaskOutcome(work.Completed > 0 ? "partial" : "failed",
                    packet[3] != 0 ? "服务器已确认，实际制作 " + work.Completed + " 件。" : "服务器未批准扣料，原版已退回本次本地材料。", work.Completed));
            return true;
        }

        private static void CancelTaskCraft(TaskExecution execution)
        {
            if (execution.State is NativeTaskCraft work) work.Cancelled = true;
        }

        private void RetireTaskCraftConnection(Actor actor)
        {
            if (!taskCrafts.TryGetValue(actor, out var work)) return;
            taskCrafts.Remove(actor);
            work.Execution.Settle(TaskOutcome("unconfirmed", "队友连接中断，已提交的原版制作结果未确认。", work.Completed));
        }
    }
}
