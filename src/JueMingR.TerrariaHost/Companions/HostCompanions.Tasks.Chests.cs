using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using Terraria;

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
            internal long Fence;
            internal uint Deadline;
            internal bool Opened, FenceSeen, Cancelled, Released;
            internal Item[] Snapshot;
            internal int Completed, InFlight, Slot;
            internal Item Expected;
        }

        private CompanionTaskOutcome BeginTaskChestTransfer(TaskExecution execution, TaskResourceEnd source, TaskResourceEnd destination)
        {
            var actor = execution.Actor;
            var chest = source.Chest ?? destination.Chest;
            if (taskChestTransfers.ContainsKey(actor)) return TaskOutcome("waiting", "正在结算上一次世界箱操作。");
            if (actor.Connection == null || actor.Body.chest != -1 || !TaskChestReachable(actor, chest))
                return TaskOutcome("failed", "队友暂时无法打开指定箱子。");
            var pending = new TaskChestTransfer { Execution = execution, Chest = chest, Connection = actor.Connection,
                Fence = -(++wormholeSequence * 2), Deadline = Main.GameUpdateCount + 300 };
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
                return TaskOutcome(pending.Expected == null ? "failed" : "unconfirmed", "箱子连接或实例已改变。", pending.Completed);
            if (Main.GameUpdateCount >= pending.Deadline)
                return TaskOutcome(pending.Expected == null ? (pending.Completed > 0 ? "partial" : "failed") : "unconfirmed",
                    pending.Expected == null ? "等待箱子内容或打开资格超时。" : "服务器尚未确认箱子槽位，已暂停后续移动。", pending.Completed);
            if (!pending.Opened || pending.Expected != null) return TaskOutcome("waiting", "正在等待原版箱子结果。", pending.Completed);
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
            var outcome = CommitTaskTransfer(execution, source, destination, pending);
            return outcome.State == "failed" && pending.Completed > 0 ? TaskOutcome("partial", outcome.Detail, pending.Completed) : outcome;
        }

        private bool TaskChestCurrent(TaskChestTransfer flow)
        {
            var actor = flow.Execution.Actor;
            var chest = flow.Chest;
            return !actor.Closing && actor.Ready && actor.Generation == flow.Execution.Job.Identity.Activation &&
                ReferenceEquals(actor.Connection, flow.Connection) && flow.Connection?.Finished == false &&
                ReferenceEquals(Main.chest[chest.index], chest) && flow.Execution.Job.Identity.WorldSession == session;
        }

        private bool SubmitTaskChestSlot(TaskExecution execution, TaskChestTransfer pending, Item[] projected, int moved)
        {
            int slot = -1;
            for (int i = 0; i < projected.Length; i++)
                if (projected[i].IsNetStateDifferent(pending.Snapshot[i])) { slot = i; break; }
            if (slot < 0) return false;
            var actor = execution.Actor;
            var chest = pending.Chest;
            if (!TaskChestCurrent(pending) || !TaskChestReachable(actor, chest) || actor.Body.chest != chest.index ||
                slot >= chest.item.Length) return false;
            var original = chest.item[slot];
            try
            {
                // Native 32 serializes from the real indexed chest. The
                // primary stream remains the owner of its local world copy.
                chest.item[slot] = projected[slot];
                using (var scope = CompanionHooks.Enter(actor, actor.Aim))
                {
                    NetMessage.SendData(32, number: chest.index, number2: slot);
                    if (!scope.PacketAccepted) return false;
                }
            }
            catch (CompanionConnection.SendQueueFullException) when (!pending.Connection.IsRelay) { return false; }
            finally { chest.item[slot] = original; }
            pending.Slot = slot;
            pending.Expected = projected[slot];
            pending.InFlight = moved;
            pending.Deadline = Main.GameUpdateCount + 300;
            return true;
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

        private void ObserveTaskChestPrimary(byte[] data, int start, int length)
        {
            if (length != 9 || data[start] != 32 || taskChestTransfers.Count == 0) return;
            int index = BitConverter.ToInt16(data, start + 1);
            TaskChestTransfer cancelled = null;
            foreach (var flow in taskChestTransfers.Values)
            {
                var expected = flow.Expected;
                if (expected == null || flow.Chest.index != index || !TaskChestCurrent(flow) || flow.Slot != data[start + 3] ||
                    expected.stack != BitConverter.ToInt16(data, start + 4) || expected.prefix != data[start + 6] ||
                    expected.type != BitConverter.ToInt16(data, start + 7)) continue;
                flow.Snapshot[flow.Slot] = expected;
                flow.Completed += flow.InFlight;
                flow.Expected = null;
                flow.Deadline = Main.GameUpdateCount + 300;
                if (flow.Cancelled) cancelled = flow;
                break;
            }
            if (cancelled != null)
            {
                CloseTaskChest(cancelled);
                cancelled.Execution.Settle(TaskOutcome("partial", "停止后确认实际移动 " + cancelled.Completed + " 件。", cancelled.Completed));
            }
        }

        private void CancelTaskChestTransfer(TaskExecution execution)
        {
            if (!(execution.State is TaskChestTransfer flow)) return;
            flow.Cancelled = true;
            if (flow.Expected == null) CloseTaskChest(flow);
            else ReleaseTaskChestOpen(flow); // Already transmitted writes still own their receipts.
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
            CloseTaskChest(flow);
            if (flow.Expected != null)
                flow.Execution.Settle(TaskOutcome("unconfirmed", "队友连接中断，最后一个箱子槽位未确认。", flow.Completed));
        }
    }
}
