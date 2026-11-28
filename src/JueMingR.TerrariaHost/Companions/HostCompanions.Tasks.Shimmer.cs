using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static readonly Harmony TaskShimmerHarmony = new Harmony("JueMingR.Companions.TaskShimmer");
        private static bool taskShimmerInstalled;
        private static HostCompanions taskShimmerOwner;
        [ThreadStatic] private static TaskShimmerDrop droppingForShimmer;
        [ThreadStatic] private static TaskShimmerDrop convertingShimmer;
        private readonly Dictionary<Actor, TaskShimmerDrop> taskShimmerDrops = new Dictionary<Actor, TaskShimmerDrop>();

        private sealed class TaskShimmerDrop
        {
            internal TaskExecution Execution;
            internal TargetTile Pool;
            internal string MemoryReference;
            internal Point RememberedPoint;
            internal Point? RememberedFooting;
            internal long MemoryEpoch;
            internal ArrivalSearch Arrival;
            internal Item Item;
            internal int Slot, Type, Batch, ResultType, ResultStack, Confirmed;
            internal uint Deadline, NextSearch;
            internal Vector2 Origin;
            internal WorldItem Dropped;
            internal HashSet<WorldItem> OlderItems;
            internal readonly Dictionary<int, int> Products = new Dictionary<int, int>();
            internal bool Submitted, Cancelled, Converted, TimedOut, NativeDropAttempted, NativeDropAccepted, PoolReady;
        }

        private void RegisterTaskShimmer()
        {
            RegisterTaskOperation("shimmer_item", "让队友走到已观察的微光池或当前世界共享的 memory: 地标，到场复核后投入指定数量并观察原版结果。",
                ShimmerTaskItem, CancelTaskShimmer, new TaskField("target", required: true, min: 1, max: 64),
                new TaskField("item_type", "integer", true, 1, ItemID.Count - 1), new TaskField("amount", "integer", true, 1, 99999),
                new TaskField("held_at_send", "boolean"));
        }

        private CompanionTaskOutcome ShimmerTaskItem(TaskExecution execution)
        {
            var actor = execution.Actor;
            int type = execution.Number("item_type"), amount = execution.Number("amount");
            if (!(execution.State is TaskShimmerDrop work))
            {
                if (taskShimmerDrops.ContainsKey(actor)) return TaskOutcome("waiting", "正在确认队友先前投入微光的物品。");
                string target = execution.Text("target");
                bool remembered = target?.StartsWith("memory:", StringComparison.Ordinal) == true;
                TargetTile pool = default;
                WorldMemory.Place memory = null;
                if (remembered ? SharedWorldMemory?.TryGetPlace(target, out memory) != true || memory.Invalid || memory.Category != "shimmer" :
                    actor.TaskTiles == null || !actor.TaskTiles.TryGetValue(target, out pool) || !KnownShimmerPool(pool.Point))
                    return TaskOutcome("failed", "需要队友观察到的实际微光池，或本世界可到场复核的微光地标。");
                if (remembered && execution.Anchor.WorkPoint is Point spoken &&
                    (target != execution.Anchor.WorkReference || memory.Point != spoken))
                    return TaskOutcome("failed", "发话时指定的微光池已变化，请重新确认。");
                if (actor.Fishing != null) EndFishingActivity(actor);
                execution.State = work = new TaskShimmerDrop { Execution = execution, Pool = pool, Type = type,
                    MemoryReference = remembered ? target : null, RememberedPoint = remembered ? memory.Point : pool.Point,
                    RememberedFooting = memory?.Footing, MemoryEpoch = remembered ? SharedWorldMemory.Epoch : 0 };
            }
            if (work.Converted)
            {
                work.Confirmed += work.Batch;
                work.Submitted = work.Converted = false;
                work.Dropped = null;
                if (work.Confirmed >= amount) return TaskShimmerResult(work, "succeeded", "已观察到原版微光结果。");
            }
            if (work.Submitted)
            {
                ObserveTaskShimmerDrop(actor);
                return TaskOutcome("waiting", "已投入该批物品，正在等原版微光结果。", work.Confirmed);
            }
            if (Main.GameUpdateCount - execution.StartedAt > 3600)
                return TaskShimmerResult(work, work.Confirmed > 0 ? "partial" : "failed", "未在任务期限内完成微光处理。");
            if (work.MemoryReference != null)
            {
                var outcome = RefreshTaskShimmerMemory(execution, work);
                if (outcome != null) return outcome;
            }
            else if (!KnownShimmerPool(work.Pool.Point))
                return TaskShimmerResult(work, work.Confirmed > 0 ? "partial" : "failed", "原目标已无可确认的微光。");
            if (!TryTaskShimmerPosition(execution, work, out var travel)) return travel;
            if (work.MemoryReference != null && CompanionNativeContact.ObservedSupport(actor.Body))
                SharedWorldMemory.RecordShimmerFooting(work.MemoryReference, CompanionNativeContact.Feet(actor.Body).ToTileCoordinates());
            if (!FishingHandFree(actor.Body) || actor.Body.IsStackingItems()) return TaskOutcome("waiting", "等队友结束手中操作再投入。", work.Confirmed);
            var body = actor.Body;
            int slot;
            if (execution.Flag("held_at_send"))
            {
                if (amount != 1 || !TryTaskTalkItem(actor, execution.Anchor, out slot, out var spokenItem) || spokenItem.type != type ||
                    TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, slot))
                    return TaskShimmerResult(work, "failed", "发话时指向的那件队友物品已变化，未改用其他背包物品。");
            }
            else slot = Enumerable.Range(0, CompanionBehavior.MainInventorySlots).Where(i => body.inventory[i].type == type && !body.inventory[i].IsAir &&
                !body.inventory[i].favorited && !TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, i)).DefaultIfEmpty(-1).First();
            if (slot < 0) return TaskShimmerResult(work, work.Confirmed > 0 ? "partial" : "failed", "队友没有更多可投入的指定物品。");
            var item = body.inventory[slot];
            var aliquot = item.Clone();
            aliquot.stack = Math.Min(amount - work.Confirmed, item.stack);
            work.ResultType = ShimmerTransforms.GetTransformToItem(item.GetShimmerEquivalentType());
            int recipe = ShimmerTransforms.GetDecraftingRecipeIndex(item.GetShimmerEquivalentType(forDecrafting: true));
            if (!aliquot.CanShimmer() || work.ResultType <= 0 && (recipe < 0 || aliquot.FindDecraftAmount() <= 0))
                return TaskShimmerResult(work, work.Confirmed > 0 ? "partial" : "failed", "该物品当前不能合法转换或分解。");
            if (work.ResultType <= 0) aliquot.stack = aliquot.FindDecraftAmount() * Main.recipe[recipe].createItem.stack;
            // Multiplayer applies the standard drop grace to direct drops.
            // Without an individual owner on that grace no unique native
            // world-item birth can be matched to the controlled body.
            if (Main.netMode == 1 && (actor.Connection == null || actor.Connection.Finished || ItemID.Sets.DropWithGrabDelayForAllPlayers[type]))
                return TaskShimmerResult(work, work.Confirmed > 0 ? "partial" : "failed", "该物品的多人投放无法从原版回包确认归属。");
            if (!EnsureTaskShimmerHooks()) return TaskShimmerResult(work, "failed", "原版微光观察接口不可用。");
            taskShimmerOwner = this;
            work.Item = item;
            work.Slot = slot;
            work.Batch = aliquot.stack;
            work.ResultStack = 0;
            work.TimedOut = false;
            work.NativeDropAttempted = work.NativeDropAccepted = false;
            work.Origin = body.Center;
            work.Deadline = Main.GameUpdateCount + 600;
            work.OlderItems = Main.netMode == 1 ? new HashSet<WorldItem>(Main.item.Where(entry => entry != null && entry.active)) : null;
            return SubmitTaskShimmerDrop(execution, work, aliquot);
        }

        private CompanionTaskOutcome SubmitTaskShimmerDrop(TaskExecution execution, TaskShimmerDrop work, Item aliquot)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            var stash = body.inventory[work.Slot];
            bool debit = false;
            try
            {
                using (var scope = CompanionHooks.Enter(actor, work.Pool.Point.ToWorldCoordinates(), inventory: true))
                {
                    if (!ReferenceEquals(stash, work.Item) || stash.stack < work.Batch || !KnownShimmerPool(work.Pool.Point))
                        return TaskShimmerResult(work, "failed", "投放前物品或微光池已变化。");
                    // The real manual drop determines initial velocity, item
                    // ownership and server serialization. Split only by the
                    // authorized count, before giving vanilla the actual slot.
                    body.ChangeDir(work.Pool.Point.X * 16 + 8 >= body.Center.X ? 1 : -1);
                    Main.mouseItem = new Item();
                    body.inventory[work.Slot] = aliquot;
                    droppingForShimmer = work;
                    body.DropSelectedItem(work.Slot, ref body.inventory[work.Slot]);
                    debit = Main.netMode == 1 ? work.NativeDropAccepted : work.Dropped != null;
                    if (!debit) return TaskShimmerResult(work, "failed", "原版未接受此次投放。");
                    execution.MarkSubmitted();
                    taskShimmerDrops[actor] = work;
                    work.Submitted = true;
                    return TaskOutcome("waiting", "已实际投入 " + work.Batch + " 件，等待原版微光结果。", work.Confirmed);
                }
            }
            catch (CompanionConnection.SendQueueFullException) when (actor.Connection?.IsRelay == false)
            { return TaskShimmerResult(work, "failed", "队友连接未接受本次投放。"); }
            catch (Exception) when (work.NativeDropAttempted && actor.Connection?.IsRelay == true)
            {
                debit = true;
                execution.MarkSubmitted();
                taskShimmerDrops[actor] = work;
                work.Submitted = true;
                return TaskOutcome("waiting", "原版微光投放发送结果暂未确认，等待实际掉落实例。", work.Confirmed);
            }
            finally
            {
                droppingForShimmer = null;
                if (debit) stash.stack -= work.Batch;
                body.inventory[work.Slot] = stash.stack > 0 ? stash : new Item();
                actor.Dirty |= debit;
                if (debit) InvalidateItemSources(actor);
            }
        }

        internal void PrepareTaskShimmerDrop(Actor actor)
        {
            if (ReferenceEquals(droppingForShimmer?.Execution.Actor, actor)) droppingForShimmer.NativeDropAttempted = true;
        }

        private static void RecordTaskShimmerDrop(Actor actor)
        {
            if (ReferenceEquals(droppingForShimmer?.Execution.Actor, actor)) droppingForShimmer.NativeDropAccepted = true;
        }

        private bool TryTaskShimmerPosition(TaskExecution execution, TaskShimmerDrop work, out CompanionTaskOutcome outcome)
        {
            var actor = execution.Actor;
            var point = work.MemoryReference == null || work.PoolReady ? work.Pool.Point : work.RememberedPoint;
            Vector2 destination = point.ToWorldCoordinates();
            Vector2 center = actor.Body.Center;
            outcome = null;
            if (Vector2.DistanceSquared(center, destination) <= 84 * 84 && center.Y <= destination.Y + 8 &&
                Collision.CanHitLine(center, 1, 1, destination, 1, 1)) return true;
            if (work.Arrival == null)
            {
                SetTaskTravel(actor, null);
                int side = center.X < destination.X ? -1 : 1;
                bool findingRememberedPool = work.MemoryReference != null && !work.PoolReady;
                var hint = findingRememberedPool ? work.RememberedFooting : null;
                work.Arrival = new ArrivalSearch { X = hint?.X ?? point.X + side * 3, Y = hint?.Y ?? point.Y + (findingRememberedPool ? 4 : 0),
                    HorizontalRadius = 4, VerticalRadius = 4, Name = "微光池边" };
            }
            if (actor.TaskTravel == null)
            {
                if (!FindTaskFooting(actor, work.Arrival, out var feet, out bool exhausted))
                {
                    outcome = TaskOutcome(exhausted ? "failed" : "waiting", "正在核对微光池边的安全落脚点。", work.Confirmed);
                    return false;
                }
                actor.TaskControlId = execution.Job.Id;
                SetTaskTravel(actor, new TaskTravel { Feet = feet, Name = work.Arrival.Name });
            }
            outcome = TaskOutcome(actor.TaskTravel.Arrived ? "failed" : "running",
                actor.TaskTravel.Arrived ? "当前站位不能把物品合法投入该池。" : "正在接近微光池。", work.Confirmed);
            return false;
        }

        private CompanionTaskOutcome RefreshTaskShimmerMemory(TaskExecution execution, TaskShimmerDrop work)
        {
            string failure = work.Confirmed > 0 ? "partial" : "failed";
            if (SharedWorldMemory?.Epoch != work.MemoryEpoch || SharedWorldMemory.TryGetPlace(work.MemoryReference, out var place) != true ||
                place.Point != work.RememberedPoint)
                return TaskShimmerResult(work, failure, "任务中的原微光地标已变化，未改用别处投放。");
            var actor = execution.Actor;
            bool reachedBank = work.Arrival != null && actor.TaskTravel?.Arrived == true && !actor.TaskTravel.Temporary;
            // The saved point represents the whole pool, often at the top of
            // a tall water column. Arriving at the real bank admits rechecking
            // the pool even when that display point is above throw range.
            if (!place.Invalid && !work.PoolReady && !reachedBank &&
                Vector2.DistanceSquared(actor.Body.Center, place.Point.ToWorldCoordinates()) > 112 * 112)
            {
                if (!TryTaskShimmerPosition(execution, work, out var travel)) return travel;
            }
            bool invalid = place.Invalid;
            if (place.Invalid || !SharedWorldMemory.RecheckShimmer(place, actor.Body.Center, out Point wet, out invalid))
                return TaskShimmerResult(work, failure, place.Invalid || invalid ?
                    "到场复核：原微光池已干涸，已更新共享地点，未投放这批物品。" :
                    "队友尚无法在原地标核实同一微光池，未投放这批物品。");
            // Persistence carries a destination and evidence only. This
            // actor witnesses an actual tile in this session before DropItem.
            work.Pool = new TargetTile(wet, new TileFact(Main.tile[wet.X, wet.Y]));
            if (!work.PoolReady && work.Arrival != null)
            {
                work.Arrival = null;
                SetTaskTravel(actor, null);
            }
            work.PoolReady = true;
            return null;
        }

        private static bool KnownShimmerPool(Point point)
        {
            if (!CompanionNativeContact.Loaded(point.X, point.Y)) return false;
            var tile = Main.tile[point.X, point.Y];
            return tile.liquid > 0 && tile.shimmer();
        }

        private void ObserveTaskShimmerDrop(Actor actor)
        {
            if (!taskShimmerDrops.TryGetValue(actor, out var work) || Main.GameUpdateCount < work.NextSearch) return;
            work.NextSearch = Main.GameUpdateCount + 6;
            if (Main.netMode == 1 && work.Dropped == null)
            {
                var candidates = Main.item.Where(item => item != null && item.active && item.type == work.Type && item.stack == work.Batch &&
                    item.prefix == work.Item.prefix && item.grabDelayPlayer == actor.Body.whoAmI && !work.OlderItems.Contains(item) &&
                    Vector2.DistanceSquared(item.Center, work.Origin) < 128 * 128).Take(2).ToArray();
                if (candidates.Length == 1) { work.Dropped = candidates[0]; work.OlderItems = null; }
            }
            var dropped = work.Dropped;
            bool transformed = work.Converted || dropped != null && dropped.active && ReferenceEquals(Main.item[dropped.whoAmI], dropped) &&
                dropped.shimmered && work.ResultType > 0 && dropped.type == work.ResultType && dropped.stack == work.Batch;
            if (transformed)
            {
                work.ResultStack = work.ResultType > 0 ? work.Batch : 0;
                if (work.ResultStack > 0) AddShimmerProduct(work, work.ResultType, work.ResultStack);
                work.Converted = true;
                taskShimmerDrops.Remove(actor);
                if (work.Cancelled)
                {
                    work.Confirmed += work.Batch;
                    work.Execution.Settle(TaskShimmerResult(work, "partial", "停止后确认了原版微光结果。"));
                }
            }
            else if (Main.GameUpdateCount >= work.Deadline)
            {
                if (work.TimedOut) taskShimmerDrops.Remove(actor);
                else
                {
                    // Keep the exact in-flight drop through one observation
                    // window so a delayed world reply can settle this attempt.
                    work.TimedOut = true;
                    work.Deadline = Main.GameUpdateCount + 600;
                    work.Execution.Settle(TaskShimmerResult(work, "unconfirmed", "已投放的物品未取得可匹配的原版微光结果。"));
                }
            }
        }

        internal void AdvanceTaskShimmerDrops()
        {
            if (taskShimmerDrops.Count == 0 || (Main.GameUpdateCount & 3) != 0) return;
            foreach (var actor in taskShimmerDrops.Keys.ToArray()) ObserveTaskShimmerDrop(actor);
        }

        private static void AddShimmerProduct(TaskShimmerDrop work, int type, int count)
        {
            work.Products.TryGetValue(type, out int previous);
            work.Products[type] = previous + count;
        }

        private static CompanionTaskOutcome TaskShimmerResult(TaskShimmerDrop work, string state, string detail) =>
            TaskOutcome(state, detail + (work == null ? "" : " 已确认处理 " + work.Confirmed + " 件。" +
                (work.Products.Count != 0 ? "观察到原版产物 " + String.Join("、", work.Products.Take(12).Select(entry =>
                    Lang.GetItemNameValue(entry.Key) + "×" + entry.Value)) + "。" : "")), work?.Confirmed ?? 0);

        private static void CancelTaskShimmer(TaskExecution execution)
        {
            if (execution.State is TaskShimmerDrop work) work.Cancelled = true;
        }

        private void RetireTaskShimmerDrop(Actor actor)
        {
            if (!taskShimmerDrops.TryGetValue(actor, out var work)) return;
            taskShimmerDrops.Remove(actor);
            work.Execution.Settle(TaskShimmerResult(work, "unconfirmed", "连接或身体退出，最后一次微光结果未确认。"));
        }

        private static bool EnsureTaskShimmerHooks()
        {
            if (taskShimmerInstalled) return true;
            try
            {
                var nativeBirth = AccessTools.Method(typeof(Item), nameof(Item.NewItem), new[] { typeof(IEntitySource), typeof(Vector2),
                    typeof(int), typeof(int), typeof(int), typeof(NewItemOwnership), typeof(Vector2?), typeof(Item.NewItemModifier), typeof(bool) });
                TaskShimmerHarmony.Patch(nativeBirth, postfix: new HarmonyMethod(typeof(HostCompanions), nameof(ObserveTaskShimmerBirth)));
                TaskShimmerHarmony.Patch(AccessTools.Method(typeof(WorldItem), nameof(WorldItem.GetShimmered)),
                    prefix: new HarmonyMethod(typeof(HostCompanions), nameof(BeginTaskShimmerConversion)),
                    postfix: new HarmonyMethod(typeof(HostCompanions), nameof(ObserveTaskShimmerConversion)),
                    finalizer: new HarmonyMethod(typeof(HostCompanions), nameof(EndTaskShimmerConversion)));
                TaskShimmerHarmony.Patch(AccessTools.Method(typeof(WorldItem), nameof(WorldItem.UpdateItem)),
                    prefix: new HarmonyMethod(typeof(HostCompanions), nameof(EnterTaskShimmerItem)),
                    finalizer: new HarmonyMethod(typeof(HostCompanions), nameof(LeaveTaskShimmerItem)));
                return taskShimmerInstalled = true;
            }
            catch (Exception) { TaskShimmerHarmony.UnpatchAll(TaskShimmerHarmony.Id); return false; }
        }

        private static void ObserveTaskShimmerBirth(int __result, int __3)
        {
            var work = droppingForShimmer;
            if (Main.netMode != 0 || __result < 0 || __result >= Main.maxItems || !Main.item[__result].active) return;
            if (work != null) work.Dropped = Main.item[__result];
            if (convertingShimmer != null) AddShimmerProduct(convertingShimmer, Main.item[__result].type, Math.Min(__3, Main.item[__result].stack));
        }

        private static void EnterTaskShimmerItem(WorldItem __instance, out CompanionHooks.Scope __state)
        {
            __state = null;
            var host = taskShimmerOwner;
            if (host == null || !__instance.shimmerWet || __instance.playerIndexTheItemIsReservedFor == Main.myPlayer) return;
            foreach (var entry in host.taskShimmerDrops)
            {
                var actor = entry.Key;
                if (actor.Ready && !actor.Closing && !actor.Body.dead && actor.Body.whoAmI == __instance.playerIndexTheItemIsReservedFor &&
                    ReferenceEquals(entry.Value.Dropped, __instance))
                { __state = CompanionHooks.Enter(actor, __instance.Center); break; }
            }
        }

        private static Exception LeaveTaskShimmerItem(CompanionHooks.Scope __state, Exception __exception)
        {
            __state?.Dispose();
            return __exception;
        }

        private static void BeginTaskShimmerConversion(WorldItem __instance, out TaskShimmerDrop __state)
        {
            __state = convertingShimmer;
            convertingShimmer = taskShimmerOwner?.taskShimmerDrops.Values.FirstOrDefault(work => ReferenceEquals(work.Dropped, __instance));
        }

        private static Exception EndTaskShimmerConversion(TaskShimmerDrop __state, Exception __exception)
        {
            convertingShimmer = __state;
            return __exception;
        }

        private static void ObserveTaskShimmerConversion(WorldItem __instance)
        {
            var host = taskShimmerOwner;
            if (host == null) return;
            Actor found = null;
            foreach (var entry in host.taskShimmerDrops)
                if (ReferenceEquals(entry.Value.Dropped, __instance) &&
                    (__instance.type == entry.Value.ResultType && __instance.shimmered || __instance.IsAir))
                { entry.Value.Converted = true; found = entry.Key; break; }
            if (found != null)
            {
                host.taskShimmerDrops[found].NextSearch = 0;
                host.ObserveTaskShimmerDrop(found);
            }
        }
    }
}
