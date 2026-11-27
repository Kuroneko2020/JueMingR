using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.Net.Sockets;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private readonly Dictionary<Actor, TaskChestTransfer> taskChestTransfers = new Dictionary<Actor, TaskChestTransfer>();

        private sealed class TaskChestTransfer
        {
            internal TaskExecution Execution;
            internal Chest Chest;
            internal CompanionConnection Connection;
            internal RemoteServer Primary;
            internal ISocket PrimarySocket;
            internal int ActorSlot;
            internal long Fence;
            internal uint Deadline;
            internal bool Opened, FenceSeen, Cancelled, Released, Sent, Deposit;
            internal Item[] Snapshot;
            internal int Completed, InFlight, Slot;
            internal Item Expected;
            internal List<CompanionItemChange> Changes;
            internal Item[] Before;
            internal ulong HeldSlots;
        }

        private CompanionTaskOutcome BeginTaskChestTransfer(TaskExecution execution, TaskResourceEnd source, TaskResourceEnd destination)
        {
            var actor = execution.Actor;
            var chest = source.Chest ?? destination.Chest;
            if (taskChestTransfers.ContainsKey(actor) || taskChestTransfers.Values.Any(flow => ReferenceEquals(flow.Chest, chest)))
                return TaskOutcome("waiting", "正在结算上一次世界箱操作。");
            if (actor.Connection == null || actor.Body.chest != -1 || !TaskChestReachable(actor, chest))
                return TaskOutcome("failed", "队友暂时无法打开指定箱子。");
            var pending = new TaskChestTransfer { Execution = execution, Chest = chest, Connection = actor.Connection,
                Primary = Netplay.Connection, PrimarySocket = Netplay.Connection?.Socket, ActorSlot = actor.Body.whoAmI,
                Fence = -(++wormholeSequence * 2), Deadline = Main.GameUpdateCount + 300, Deposit = destination.Chest != null };
            // Order this open after prior requests on the same role. An old
            // 33 from a cancelled visit may arrive on this same connection.
            if (!SendWormholeFence(actor, pending.Fence)) return TaskOutcome("failed", "队友连接未准备好打开箱子。");
            using (var scope = CompanionHooks.Enter(actor, actor.Aim))
            {
                NetMessage.SendData(31, number: chest.x, number2: chest.y);
                if (!scope.PacketAccepted) return TaskOutcome("failed", "队友连接未接受打开箱子的请求。");
            }
            taskChestTransfers.Add(actor, pending);
            execution.State = pending;
            return TaskOutcome("waiting", "正在等待服务器确认打开指定箱子。");
        }

        private CompanionTaskOutcome AdvanceTaskChestTransfer(TaskExecution execution, TaskChestTransfer pending)
        {
            if (!TaskChestCurrent(pending))
                return TaskOutcome(pending.Sent ? "unconfirmed" : "failed", "箱子连接或实例已改变。", pending.Completed);
            if (Main.GameUpdateCount >= pending.Deadline)
                return TaskOutcome(pending.Sent ? "unconfirmed" : pending.Completed > 0 ? "partial" : "failed",
                    pending.Sent ? TaskChestPendingDetail(pending) : "等待箱子内容、打开资格或物品保存超时。", pending.Completed);
            if (pending.Sent || !pending.Opened) return TaskOutcome("waiting", "正在等待原版箱子结果。", pending.Completed);
            if (pending.Expected != null) return SendTaskChestSlot(pending);
            if (pending.Completed >= execution.Number("amount"))
            {
                CloseTaskChest(pending);
                return TaskOutcome("succeeded", "服务器已确认移动 " + pending.Completed + " 件。", pending.Completed);
            }
            if (!TryTaskResourceEnd(execution.Actor, execution.Text("source"), out var source, out var reason) ||
                !TryTaskResourceEnd(execution.Actor, execution.Text("destination"), out var destination, out reason))
                return TaskOutcome(pending.Completed > 0 ? "partial" : "failed", reason, pending.Completed);
            var worldEnd = source.Chest != null ? source : destination;
            worldEnd.Items = pending.Snapshot;
            worldEnd.Count = pending.Snapshot.Length;
            (pending.Deposit ? source : destination).Count = CompanionBehavior.MainInventorySlots;
            var outcome = CommitTaskTransfer(execution, source, destination, pending);
            return outcome.State == "failed" && pending.Completed > 0 ? TaskOutcome("partial", outcome.Detail, pending.Completed) : outcome;
        }

        private bool TaskChestCurrent(TaskChestTransfer flow)
        {
            var actor = flow.Execution.Actor;
            return TaskChestReceiptCurrent(flow) && flow.Primary.IsConnected() && !actor.Closing && actor.Ready &&
                ReferenceEquals(actor.Connection, flow.Connection) && !flow.Connection.Finished;
        }

        private bool TaskChestReceiptCurrent(TaskChestTransfer flow)
        {
            var actor = flow.Execution.Actor;
            var identity = flow.Execution.Job.Identity;
            var chest = flow.Chest;
            return Main.netMode == 1 && identity.WorldSession == session && ReferenceEquals(world, Main.ActiveWorldFileData) &&
                ReferenceEquals(flow.Primary, Netplay.Connection) && flow.PrimarySocket != null &&
                ReferenceEquals(flow.PrimarySocket, Netplay.Connection?.Socket) && actor.Generation == identity.Activation &&
                profiles.OwnsActivation(actor.Profile.Id, identity.Activation) && ReferenceEquals(Main.chest[chest.index], chest);
        }

        private CompanionTaskOutcome PrepareTaskChestSlot(TaskChestTransfer pending, Item[] projected, int moved, List<CompanionItemChange> changes)
        {
            int slot = -1;
            // One original 32 and one exact backpack write set are kept per slot.
            for (int i = 0; i < projected.Length; i++)
                if (projected[i].IsNetStateDifferent(pending.Snapshot[i])) { slot = i; break; }
            var actor = pending.Execution.Actor;
            var held = actor.Body.inventoryChestStack;
            if (slot < 0 || changes.Count == 0 || changes.Any(change =>
                change.Owner || !ReferenceEquals(change.Array, actor.Body.inventory) || change.Slot >= held.Length ||
                held[change.Slot] || TaskChestSourceHeld(actor, change.Slot)))
                return TaskOutcome("failed", "队友背包与箱子的待移动槽位已变化。", pending.Completed);
            if (pending.Deposit && changes.Any(change => TaskChestDropsOnDeath(actor.Body, change.Before)))
                return TaskOutcome("failed", "这些物品可能在原版死亡时掉落，无法安全等待服务器收存。", pending.Completed);

            pending.Slot = slot;
            pending.Expected = projected[slot];
            pending.InFlight = moved;
            pending.Changes = changes;
            pending.Before = changes.Select(change => change.Before.Clone()).ToArray();
            pending.Deadline = Main.GameUpdateCount + 300;
            foreach (var change in changes)
            {
                held[change.Slot] = true;
                pending.HeldSlots |= 1UL << change.Slot;
            }
            // A crash after the world is credited must not reopen a saved
            // copy of those same items. The existing storage quarantine is
            // durable before the actor connection can send a deposit.
            if (pending.Deposit)
                CaptureActorState(actor, fishingDepositUnconfirmedSlots: pending.HeldSlots);
            return SendTaskChestSlot(pending);
        }

        private static bool TaskChestDropsOnDeath(Player actor, Item item) =>
            actor.difficulty == 1 || actor.difficulty == 2 || item.IsACoin ||
            item.type >= 1522 && item.type <= 1527 || item.type == 3643;

        private CompanionTaskOutcome SendTaskChestSlot(TaskChestTransfer pending)
        {
            var actor = pending.Execution.Actor;
            var chest = pending.Chest;
            int slot = pending.Slot;
            if (!TaskChestCurrent(pending) || !TaskChestReachable(actor, chest) || actor.Body.chest != chest.index ||
                slot >= chest.item.Length || !TaskChestBackpackCurrent(pending))
                return TaskOutcome("failed", "发送前，箱子或队友背包槽位已变化。", pending.Completed);
            if (pending.Deposit && !profiles.IsSaved(actor.Profile.Id))
                return TaskOutcome("waiting", "正在保存队友的待确认存入记录。", pending.Completed);
            var original = chest.item[slot];
            try
            {
                // Native 32 serializes from the real indexed chest. The
                // primary stream remains the owner of its local world copy.
                chest.item[slot] = pending.Expected;
                using (var scope = CompanionHooks.Enter(actor, actor.Aim))
                {
                    NetMessage.SendData(32, number: chest.index, number2: slot);
                    if (!scope.PacketAccepted) return TaskOutcome("failed", "队友连接未发送箱子操作。", pending.Completed);
                }
            }
            catch (CompanionConnection.SendQueueFullException) when (!pending.Connection.IsRelay)
            { return TaskOutcome("failed", "队友连接的发送队列未接收箱子操作。", pending.Completed); }
            catch (Exception)
            {
                // A relay may have published bytes before throwing. Keep
                // the submitted slot protected and await original evidence.
            }
            finally { chest.item[slot] = original; }
            pending.Sent = true;
            pending.Execution.MarkSubmitted();
            pending.Deadline = Main.GameUpdateCount + 300;
            return TaskOutcome("waiting", "箱子操作已发送，等待服务器回传实际槽位。", pending.Completed);
        }

        private bool ConsumeTaskChestActorFrame(Actor actor, byte[] frame)
        {
            int id = frame[0];
            if (id != 32 && id != 33 && id != 155 || !taskChestTransfers.TryGetValue(actor, out var flow) || !TaskChestCurrent(flow)) return false;
            if (!flow.FenceSeen) return true;
            if (frame.Length < 3 || BitConverter.ToInt16(frame, 1) != flow.Chest.index) return true;
            // The open response is ordered after a native size frame and all
            // actual slots on this connection. Section broadcasts alone
            // cannot grant a private chest interaction.
            if (!flow.Opened && id == 155 && frame.Length == 5)
            {
                int count = BitConverter.ToInt16(frame, 3);
                flow.Snapshot = count > 0 && count <= Chest.AbsoluteMaxItemsWeCanEverReachInAChestForNow ? new Item[count] : null;
            }
            if (!flow.Opened && id == 32 && flow.Snapshot != null && frame.Length == 9 && frame[3] < flow.Snapshot.Length)
            {
                var item = new Item();
                item.SetDefaults(BitConverter.ToInt16(frame, 7));
                item.Prefix(frame[6]);
                item.stack = BitConverter.ToInt16(frame, 4);
                flow.Snapshot[frame[3]] = item;
            }
            if (id == 33 && frame.Length >= 8 && BitConverter.ToInt16(frame, 3) == flow.Chest.x &&
                BitConverter.ToInt16(frame, 5) == flow.Chest.y && flow.Snapshot != null && flow.Snapshot.All(item => item != null))
            {
                flow.Opened = true;
                actor.Body.chest = flow.Chest.index;
                actor.Body.chestX = flow.Chest.x;
                actor.Body.chestY = flow.Chest.y;
            }
            return true;
        }

        private void ObserveTaskChestFence(Actor actor)
        {
            if (actor.WorldSource.Pings.Count != 0 && taskChestTransfers.TryGetValue(actor, out var flow) &&
                TaskChestCurrent(flow) && actor.WorldSource.Pings.Peek() == flow.Fence) flow.FenceSeen = true;
        }

        internal void ObserveTaskChestPrimary(byte[] data, int start, int length)
        {
            if (taskChestTransfers.Count == 0 || length != 9) return;
            int index = BitConverter.ToInt16(data, start + 1);
            var flow = taskChestTransfers.Values.FirstOrDefault(candidate => candidate.Sent && candidate.Chest.index == index);
            if (flow == null || !TaskChestReceiptCurrent(flow) || flow.Slot != data[start + 3]) return;
            var expected = flow.Expected;
            var actual = flow.Chest.item[flow.Slot];
            if (expected.stack != BitConverter.ToInt16(data, start + 4) || expected.prefix != data[start + 6] ||
                expected.type != BitConverter.ToInt16(data, start + 7) || actual == null || expected.IsNetStateDifferent(actual)) return;
            var actor = flow.Execution.Actor;
            if (!TaskChestBackpackCurrent(flow))
            {
                EndUnconfirmedTaskChest(flow, "服务器已更新箱子，但队友背包的预留槽位已变化，未结算背包。");
                return;
            }
            flow.Snapshot[flow.Slot] = expected;
            flow.Completed += flow.InFlight;
            flow.Expected = null; flow.Sent = false;
            flow.Deadline = Main.GameUpdateCount + 300;
            var changes = flow.Changes;
            foreach (var change in changes) change.Apply();
            ReleaseTaskChestBackpack(flow, clearDeposit: true);
            FinishItemCommit(actor, changes, "");
            bool complete = flow.Completed == flow.Execution.Number("amount");
            if (flow.Cancelled || complete)
            {
                CloseTaskChest(flow);
                flow.Execution.Settle(TaskOutcome(complete ? "succeeded" : "partial",
                    (flow.Cancelled ? "停止后确认实际移动 " : "服务器已确认移动 ") + flow.Completed + " 件。", flow.Completed));
            }
            else if (flow.Execution.Job.Paused)
                flow.Execution.Settle(TaskOutcome("waiting", "已确认移动 " + flow.Completed + " 件，恢复后继续。", flow.Completed));
        }

        private static bool TaskChestBackpackCurrent(TaskChestTransfer flow)
        {
            var inventory = flow.Execution.Actor.Body.inventory;
            var held = flow.Execution.Actor.Body.inventoryChestStack;
            for (int i = 0; i < flow.Changes.Count; i++)
            {
                var change = flow.Changes[i];
                if (!ReferenceEquals(change.Array, inventory) || !held[change.Slot] ||
                    !ReferenceEquals(inventory[change.Slot], change.Before) || flow.Before[i].IsNetStateDifferent(change.Before))
                    return false;
            }
            return true;
        }

        internal bool TaskChestSourceHeld(Actor actor, int slot) =>
            (actor.Profile.FishingDepositUnconfirmedSlots & (1UL << slot)) != 0 ||
            taskChestTransfers.TryGetValue(actor, out var flow) && (flow.HeldSlots & (1UL << slot)) != 0;

        private static void ReleaseTaskChestBackpack(TaskChestTransfer flow, bool clearDeposit)
        {
            if (flow.HeldSlots == 0) return;
            var actor = flow.Execution.Actor;
            var held = actor.Body.inventoryChestStack;
            ulong mask = flow.HeldSlots;
            flow.HeldSlots = 0; flow.Changes = null; flow.Before = null;
            // Keep only an unconfirmed deposit's held slots inaccessible.
            // Withdrawals never credit a bag until the real receipt.
            if (flow.Deposit && !clearDeposit) return;
            for (int i = 0; i < held.Length; i++)
                if ((mask & (1UL << i)) != 0) held[i] = false;
            if (flow.Deposit) actor.TaskChestResolvedDepositSlots |= actor.Profile.FishingDepositUnconfirmedSlots & mask;
        }

        private static string TaskChestPendingDetail(TaskChestTransfer flow) => flow.Deposit ?
            "服务器尚未确认世界箱存入，背包物品已保留并锁定。" : "服务器尚未确认世界箱取出，队友背包未增加物品。";

        private void EndUnconfirmedTaskChest(TaskChestTransfer flow, string reason)
        {
            ReleaseTaskChestBackpack(flow, clearDeposit: false);
            CloseTaskChest(flow);
            flow.Execution.Settle(TaskOutcome("unconfirmed", reason, flow.Completed));
        }

        private void CancelTaskChestTransfer(TaskExecution execution)
        {
            if (!(execution.State is TaskChestTransfer flow)) return;
            flow.Cancelled = true;
            if (flow.Sent) return; // Keep this lease and original attempt until authoritative settlement.
            CloseUnsentTaskChest(flow);
            if (flow.Completed > 0 && execution.Job.State == "cancelled")
                execution.Settle(TaskOutcome("partial", "停止前已确认移动 " + flow.Completed + " 件。", flow.Completed));
        }

        private void CloseUnsentTaskChest(TaskChestTransfer flow)
        {
            ReleaseTaskChestBackpack(flow, clearDeposit: true);
            CloseTaskChest(flow);
            var actor = flow.Execution.Actor;
            if (actor.TaskChestResolvedDepositSlots != 0) CaptureActorState(actor);
        }

        private void CloseTaskChest(TaskChestTransfer flow)
        {
            if (!taskChestTransfers.TryGetValue(flow.Execution.Actor, out var current) || !ReferenceEquals(current, flow)) return;
            taskChestTransfers.Remove(flow.Execution.Actor);
            ReleaseTaskChestOpen(flow);
        }

        private static void ReleaseTaskChestOpen(TaskChestTransfer flow)
        {
            if (flow.Released) return;
            flow.Released = true;
            var actor = flow.Execution.Actor;
            if (actor.Body.chest == flow.Chest.index) actor.Body.chest = -1;
            if (!ReferenceEquals(actor.Connection, flow.Connection) || flow.Connection.Finished) return;
            try { using (CompanionHooks.Enter(actor, actor.Aim)) NetMessage.SendData(33, number: -1); }
            catch (Exception)
            {
                // A failed release cannot keep a private chest lease behind.
                // The original server releases it when this actor disconnects.
                flow.Connection.Disconnect("无法关闭队友的世界箱操作。");
            }
        }

        private void RetireTaskChestConnection(Actor actor)
        {
            if (!taskChestTransfers.TryGetValue(actor, out var flow)) return;
            if (!flow.Sent) { CloseUnsentTaskChest(flow); return; }
            flow.Cancelled = true;
            flow.Execution.Settle(TaskOutcome("unconfirmed", "队友连接中断。" + TaskChestPendingDetail(flow), flow.Completed));
        }

        private void PumpTaskChestReceipts()
        {
            if (taskChestTransfers.Count == 0) return;
            foreach (var flow in taskChestTransfers.Values.Where(candidate => candidate.Sent &&
                (!TaskChestReceiptCurrent(candidate) || Main.GameUpdateCount >= candidate.Deadline)).ToArray())
                EndUnconfirmedTaskChest(flow, TaskChestPendingDetail(flow));
        }

        private bool WaitingForTaskChestActor(Actor actor) =>
            taskChestTransfers.TryGetValue(actor, out var flow) && flow.Sent;

        internal void ObserveTaskChestLease(int actorSlot, int chestIndex)
        {
            if (taskChestTransfers.Count == 0) return;
            var flow = taskChestTransfers.Values.FirstOrDefault(candidate => candidate.Sent &&
                (candidate.ActorSlot == actorSlot && candidate.Chest.index != chestIndex ||
                 candidate.ActorSlot != actorSlot && candidate.Chest.index == chestIndex));
            if (flow != null && TaskChestReceiptCurrent(flow)) EndUnconfirmedTaskChest(flow, TaskChestPendingDetail(flow));
        }
    }
}
