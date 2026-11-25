using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private enum RecallSend { None, Uncertain, Accepted }
        private sealed class WormholeRecall
        {
            internal Actor Actor;
            internal Player Body, Owner;
            internal CompanionConnection Connection;
            internal object World;
            internal Guid Profile, OwnerId;
            internal long Session, Generation, Epoch, Before, After, Deadline, RetryFenceAt;
            internal int PayerSlot;
            internal Vector2 Destination;
            internal RecallSend Send;
            internal bool Paid, OwnerPaid, Waiting = true, Open, EndQueued, ReplySeen, LocalEntered, Moved, AckQueued;
        }

        // At most one live command per activation. Timed-out windows keep only
        // wire attribution until their original closing echo arrives. An old
        // window never acquires a newer body, charge, or feedback lifetime.
        private const int MaxRecallWindows = 4;
        private readonly Dictionary<Actor, List<WormholeRecall>> wormholeWindows = new Dictionary<Actor, List<WormholeRecall>>();
        private long wormholeSequence;
        private WormholeRecall submittingWormhole, decodingWormhole;
        internal Action<Guid, string, bool> ManualRecallFeedback;

        internal bool QueryManualRecall(Guid profileId, out string reason) =>
            PrepareWormhole(profileId, out _, out _, out _, out reason);

        private bool PrepareWormhole(Guid id, out Actor actor, out Vector2 destination, out RecoverySource source, out string reason)
        {
            actor = null;
            source = default(RecoverySource);
            destination = Vector2.Zero;
            if (Thread.CurrentThread.ManagedThreadId != itemThread || CompanionHooks.CurrentActor != null ||
                Find(id) == null || !actors.TryGetValue(id, out actor) || !SupplyActorReady(actor) ||
                actor.Profile.PermanentlyDead || actor.PermanentDeathPending || actor.Body.ghost)
                reason = "需要已经加入且存活的队友。";
            else if (owner == null || !ReferenceEquals(owner, Main.LocalPlayer) || !owner.active || owner.dead || owner.ghost)
                reason = "需要存活的当前玩家。";
            else if (Main.netMode != 0 && (Main.netMode != 1 || actor.Connection == null || actor.Connection.Finished))
                reason = "队友连接不可用。";
            else if (ReferenceEquals(submittingWormhole?.Actor, actor) || HasWaitingWormhole(actor))
                reason = "正在等待传送结果。";
            else if (actor.Body.Bottom == owner.Bottom)
                reason = "队友已在目标位置。";
            else if (!FindWormholeSource(actor, out source))
                reason = "需要消耗虫洞药水";
            else if (!WormholeDestination(actor.Body, owner, out destination))
                reason = "玩家身边没有安全的传送位置。";
            else if (actor.Body.position == destination)
                reason = "队友已在目标位置。";
            else if (wormholeWindows.TryGetValue(actor, out var windows) &&
                (windows.Count >= MaxRecallWindows || !windows[windows.Count - 1].EndQueued))
                reason = "队友连接尚未同步，可收回队友后重新加入。";
            else reason = null;
            return reason == null;
        }

        private bool HasWaitingWormhole(Actor actor)
        {
            if (!wormholeWindows.TryGetValue(actor, out var windows)) return false;
            var latest = windows[windows.Count - 1];
            return latest.Waiting && latest.Generation == actor.Generation;
        }

        private static bool WormholeDestination(Player body, Player target, out Vector2 destination)
        {
            destination = target.Bottom - new Vector2(body.width * .5f, body.height);
            if (float.IsNaN(destination.X) || float.IsNaN(destination.Y) ||
                float.IsInfinity(destination.X) || float.IsInfinity(destination.Y)) return false;
            if (CompanionMobilityGeometry.ClearBody(destination, body.width, body.height)) return true;
            // A different equipped body can be wider or taller than the owner.
            // A short local search is enough; flight does not require support.
            Vector2 origin = destination;
            for (int i = 0; i < 3; i++)
            {
                destination = origin + (i == 0 ? new Vector2(0, -16) : new Vector2(i == 1 ? -16 : 16, 0));
                if (CompanionMobilityGeometry.ClearBody(destination, body.width, body.height)) return true;
            }
            return false;
        }

        private bool FindWormholeSource(Actor actor, out RecoverySource source)
        {
            var inventory = actor.Body.inventory;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
                if (WormholePotion(inventory[slot]) && SupplyAdmitted(actor, inventory, 0, slot))
                { source = SupplySource(actor, inventory, 0, slot); return true; }
            inventory = owner.inventory;
            var operations = OwnerOperations;
            if (operations != null && operations.Session > 0)
                for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
                    if (WormholePotion(inventory[slot]) && OwnerWormholeSlot(owner, inventory, slot))
                    {
                        var item = inventory[slot];
                        source = new RecoverySource { Player = owner, Items = inventory, Item = item, Slot = slot,
                            Account = 0, Type = item.type, Stack = item.stack, Session = operations.Session };
                        return true;
                    }
            source = default(RecoverySource);
            return false;
        }

        private static bool WormholePotion(Item item) => item?.type == ItemID.WormholePotion && item.stack > 0;

        private bool OwnerWormholeSlot(Player player, Item[] items, int slot) =>
            !ReferenceEquals(items[slot], Main.mouseItem) && itemDraft?.DependsOn(items, slot) != true &&
            !CompanionItemRules.Blocked(player, CompanionInventoryArea.Inventory, slot, OwnerOperations) &&
            OwnerItems?.CompanionPaymentSlotProtected(player, items, slot) != true;

        internal bool TryManualRecall(Guid profileId)
        {
            if (!PrepareWormhole(profileId, out var actor, out var destination, out var source, out string reason))
            { ShowWormholeResult(profileId, reason, false); return false; }

            bool fromOwner = ReferenceEquals(source.Player, owner);
            var operations = OwnerOperations;
            ulong[] ownerSlots = null;
            long lease = 0;
            ulong sourceBit = 1UL << source.Slot;
            if (fromOwner)
            {
                if (!ReferenceEquals(source.Items, owner.inventory) || !ReferenceEquals(source.Item, source.Items[source.Slot]) ||
                    source.Item.type != source.Type || source.Item.stack != source.Stack || !OwnerWormholeSlot(owner, source.Items, source.Slot))
                { ShowWormholeResult(profileId, "虫洞药水已变动，请重试。", false); return false; }
                ownerSlots = new[] { sourceBit, 0UL, 0UL, 0UL, 0UL };
                lease = operations.NewUseToken();
                if (!operations.TryBeginRecovery(source.Session, ownerSlots, lease))
                { ShowWormholeResult(profileId, "玩家的物品正在使用。", false); return false; }
            }
            else
            {
                if (!SourceCurrent(actor, source) || !SupplyAdmitted(actor, source.Items, 0, source.Slot))
                { ShowWormholeResult(profileId, "虫洞药水已变动，请重试。", false); return false; }
                actor.Supplies.PaymentSlots[0] |= sourceBit;
            }

            var attempt = new WormholeRecall
            {
                Actor = actor, Body = actor.Body, Owner = owner, Connection = actor.Connection, World = world,
                Profile = profileId, OwnerId = actor.Profile.OwnerId, Session = session, Generation = actor.Generation,
                Epoch = actor.Profile.DataEpoch, Destination = destination, OwnerPaid = fromOwner, PayerSlot = source.Slot,
                Deadline = Stopwatch.GetTimestamp() + 5 * Stopwatch.Frequency
            };
            Item debit = null;
            submittingWormhole = attempt;
            try
            {
                // The local write and native submission are synchronous and
                // protected by the exact payer's slot. Record an uncertain
                // wire write in the send hook before it touches the transport.
                if (attempt.Connection != null && !OpenWormhole(attempt)) return false;
                debit = source.Stack == 1 ? new Item() : source.Item.Clone();
                if (source.Stack > 1) debit.stack--;
                source.Items[source.Slot] = debit;
                attempt.Paid = true;
                using (var scope = CompanionHooks.Enter(actor, actor.Aim))
                {
                    actor.Body.UnityTeleport(destination);
                    if (scope.PacketAccepted && attempt.Send != RecallSend.None) attempt.Send = RecallSend.Accepted;
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Companion wormhole submission {0}/{1}: {2}", attempt.Profile, attempt.Generation, e.Message);
            }
            finally
            {
                submittingWormhole = null;
                if (!attempt.LocalEntered && !attempt.Moved && attempt.Send == RecallSend.None && attempt.Paid &&
                    ReferenceEquals(source.Items, source.Player.inventory) && ReferenceEquals(source.Items[source.Slot], debit))
                {
                    source.Items[source.Slot] = source.Item;
                    attempt.Paid = false;
                }
                if (fromOwner) operations.EndRecovery(source.Session, ownerSlots, lease, false);
                else actor.Supplies.PaymentSlots[0] &= ~sourceBit;
                if (attempt.Connection != null && attempt.Before != 0) QueueWormholeEnd(attempt);
                if (attempt.Paid) RecordWormholePayment(attempt, source, debit);
                if (attempt.Moved) CompleteWormhole(attempt, true);
                else if (attempt.LocalEntered) CompleteWormhole(attempt, false, "传送结果未确认，已消耗 1 瓶虫洞药水。");
                else if (attempt.Send == RecallSend.None) CompleteWormhole(attempt, false, "未能提交传送。");
                else ShowWormholeResult(profileId, "正在等待传送结果。", true);
            }
            return attempt.LocalEntered || attempt.Moved || attempt.Send != RecallSend.None;
        }

        private void RecordWormholePayment(WormholeRecall attempt, RecoverySource source, Item debit)
        {
            if (!attempt.OwnerPaid)
            {
                attempt.Actor.Dirty = true;
                InvalidateItemSources(attempt.Actor);
            }
            if (attempt.Connection != null)
                QueueItemSync(source.Player, attempt.OwnerPaid ? null : attempt.Actor, PlayerItemSlotID.Inventory0 + source.Slot);
            if (attempt.OwnerPaid)
            {
                try { OwnerItems?.NotifyManualItemReplacement(source.Player, source.Items, source.Slot, source.Item, debit); }
                catch (Exception e) { Trace.TraceWarning("Companion wormhole payer notification: {0}", e.Message); }
            }
            FlushItemSync(true);
        }

        private bool OpenWormhole(WormholeRecall attempt)
        {
            long marker = -(++wormholeSequence * 2);
            if (!SendWormholeFence(attempt.Actor, marker)) return false;
            attempt.Before = marker;
            attempt.After = marker - 1;
            if (!wormholeWindows.TryGetValue(attempt.Actor, out var windows))
                wormholeWindows.Add(attempt.Actor, windows = new List<WormholeRecall>(2));
            windows.Add(attempt);
            return true;
        }

        private static bool SendWormholeFence(Actor actor, long marker)
        {
            if (!actor.Connection.TrySend(CompanionConnection.Packet(154))) return false;
            // World snapshots, death and recall share this original FIFO.
            // A rejected native ping must never shift a later marker's echo.
            actor.WorldSource.Pings.Enqueue(marker);
            return true;
        }

        private static void QueueWormholeEnd(WormholeRecall attempt)
        {
            if (attempt.EndQueued || !ReferenceEquals(attempt.Connection, attempt.Actor.Connection) || attempt.Connection.Finished) return;
            long now = Stopwatch.GetTimestamp();
            if (now < attempt.RetryFenceAt) return;
            bool first = attempt.RetryFenceAt == 0;
            // Only the original ordering probe is retried, at a bounded rate.
            // A slow queue never reruns the teleport or the inventory debit.
            attempt.RetryFenceAt = now + Stopwatch.Frequency;
            try { attempt.EndQueued = SendWormholeFence(attempt.Actor, attempt.After); }
            catch (Exception e) when (e is System.IO.IOException || attempt.Connection.Finished)
            { if (first) Trace.TraceWarning("Companion wormhole fence: {0}", e.Message); }
        }

        internal void ObserveWormholeSend(Actor actor, byte[] frame, bool? accepted)
        {
            if (frame.Length != 15 || frame[2] != 65 || BitConverter.ToInt16(frame, 4) != actor.Body.whoAmI) return;
            var attempt = submittingWormhole;
            if (ReferenceEquals(attempt?.Actor, actor) && frame[3] == 2 && frame[14] == 3 &&
                BitConverter.ToSingle(frame, 6) == attempt.Destination.X && BitConverter.ToSingle(frame, 10) == attempt.Destination.Y)
                attempt.Send = accepted.HasValue ? accepted.Value ? RecallSend.Accepted : RecallSend.None : RecallSend.Uncertain;
            if (frame[3] == 3 && accepted == true && ReferenceEquals(decodingWormhole?.Actor, actor))
                decodingWormhole.AckQueued = true;
        }

        private void WormholeFenceReceived(Actor actor, long marker)
        {
            if (!wormholeWindows.TryGetValue(actor, out var windows)) return;
            var attempt = windows[0];
            if (attempt.Before == marker) { attempt.Open = true; return; }
            if (attempt.After != marker) return;
            windows.RemoveAt(0);
            if (windows.Count == 0) wormholeWindows.Remove(actor);
            CompleteWormhole(attempt, attempt.Moved && attempt.AckQueued, "传送结果未确认。");
        }

        private WormholeRecall MatchWormholeReply(Actor actor, byte[] packet)
        {
            // Native mode 0 must supply the exact style and transmitted
            // position while our two ordered native echoes enclose it. Other
            // native teleports still reach the same decoder unchanged.
            if (packet.Length != 13 || packet[1] != 0 || packet[12] != 3 ||
                !wormholeWindows.TryGetValue(actor, out var windows)) return null;
            var attempt = windows[0];
            if (!attempt.Open || attempt.ReplySeen || attempt.Send == RecallSend.None ||
                !ReferenceEquals(attempt.Connection, actor.Connection) || !ReferenceEquals(attempt.Body, actor.Body) ||
                BitConverter.ToSingle(packet, 4) != attempt.Destination.X ||
                BitConverter.ToSingle(packet, 8) != attempt.Destination.Y) return null;
            attempt.ReplySeen = true;
            return attempt;
        }

        private bool WormholeActivationCurrent(WormholeRecall attempt) => attempt.Session == session &&
            ReferenceEquals(attempt.Owner, owner) && ReferenceEquals(attempt.World, world) &&
            ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(owner, Main.ActivePlayerFileData?.Player) &&
            ReferenceEquals(attempt.Body, attempt.Actor.Body) && ReferenceEquals(attempt.Connection, attempt.Actor.Connection) &&
            attempt.Actor.Generation == attempt.Generation && attempt.Actor.Profile.OwnerId == attempt.OwnerId &&
            attempt.Actor.Profile.Id == attempt.Profile && attempt.Actor.Profile.DataEpoch == attempt.Epoch &&
            !attempt.Actor.Closing && !attempt.Actor.Faulted && !attempt.Body.dead &&
            ReferenceEquals(Owned(attempt.Body), attempt.Actor);

        internal bool AllowWormholeDisplacement(Player body, int style, Vector2 destination)
        {
            var attempt = decodingWormhole;
            // Suppress only this retired command's original displacement. The
            // original GetData continues through its mandatory native mode-3 ACK.
            return attempt == null || !ReferenceEquals(attempt.Body, body) || style != 3 || destination != attempt.Destination ||
                WormholeActivationCurrent(attempt);
        }

        internal void ObserveLocalWormholeEntry(Player body, Vector2 destination)
        {
            var attempt = submittingWormhole;
            // Vanilla can change buffs, grapple or pressure-plate state and
            // catch an error before writing position. Entry and arrival are
            // separate facts; an entered call cannot safely refund the debit.
            if (attempt != null && attempt.Connection == null && ReferenceEquals(attempt.Body, body) &&
                ReferenceEquals(attempt.Actor, CompanionHooks.CurrentActor) && attempt.Destination == destination) attempt.LocalEntered = true;
        }

        internal void ObserveWormholeArrival(Actor actor, Vector2 destination)
        {
            var attempt = decodingWormhole ?? submittingWormhole;
            if (!ReferenceEquals(attempt?.Actor, actor) || destination != attempt.Destination || !WormholeActivationCurrent(attempt)) return;
            attempt.Moved = true;
            bool hadMobilityHand = actor.Mobility.PreviousSelection >= 0;
            RetireMobilityHand(actor);
            if (hadMobilityHand) actor.Body.controlUseItem = false;
            var mobility = actor.Mobility;
            mobility.Action = CompanionMovementActivation.None;
            mobility.Result = CompanionMobilityResult.Idle;
            mobility.DoorNeedsClose = mobility.ShapeHandExit = false;
            mobility.Source = null;
            mobility.SourceItems = null;
            mobility.SourceSlot = -1;
            mobility.Hook = null;
            actor.Body.controlLeft = actor.Body.controlRight = actor.Body.controlJump = actor.Body.controlUp =
                actor.Body.controlDown = actor.Body.controlHook = actor.Body.controlUseTile = false;
            actor.Combat.ResetHand();
            actor.Combat.Choice = actor.Combat.ReadyChoice = actor.Combat.Pursuit = actor.Combat.Probe = default(CompanionAttackChoice);
            actor.Behavior.PositionChanged(actor.Body);
            IdleAim(actor);
        }

        private void PumpWormholes(Actor actor)
        {
            if (!wormholeWindows.TryGetValue(actor, out var windows)) return;
            var latest = windows[windows.Count - 1];
            if (!latest.EndQueued) QueueWormholeEnd(latest);
            if (!latest.Waiting) return;
            if (!WormholeActivationCurrent(latest)) latest.Waiting = false;
            else if (Stopwatch.GetTimestamp() >= latest.Deadline)
                CompleteWormhole(latest, false, "等待传送结果超时，已消耗 1 瓶虫洞药水。");
        }

        private void RetireWormholes(Actor actor)
        {
            if (!wormholeWindows.TryGetValue(actor, out var windows)) return;
            wormholeWindows.Remove(actor);
            foreach (var attempt in windows)
            {
                if (attempt.Waiting && !Main.gameMenu && ReferenceEquals(owner, Main.ActivePlayerFileData?.Player) &&
                    ReferenceEquals(world, Main.ActiveWorldFileData) && attempt.Connection.Finished)
                    CompleteWormhole(attempt, false, "队友连接已结束，传送结果未确认。");
                attempt.Waiting = false;
            }
        }

        private void CompleteWormhole(WormholeRecall attempt, bool moved, string failure = null)
        {
            if (!attempt.Waiting) return;
            attempt.Waiting = false;
            if (moved && !WormholeActivationCurrent(attempt)) return;
            if (!moved && attempt.Actor.Generation != attempt.Generation) return;
            string payer = attempt.OwnerPaid ? "玩家" : "队友";
            if (!moved && attempt.Paid)
                Trace.TraceInformation("Companion wormhole {0}/{1} paid from {2} inventory slot {3}; local entry {4}, wire submission {5}, moved {6}, ACK queued {7}.",
                    attempt.Profile, attempt.Generation, attempt.OwnerPaid ? "owner" : "actor", attempt.PayerSlot,
                    attempt.LocalEntered, attempt.Send, attempt.Moved, attempt.AckQueued);
            ShowWormholeResult(attempt.Profile, moved ? attempt.Body.name + " · 已传送，消耗" + payer + "虫洞药水 1 瓶" : failure, moved);
        }

        private void ShowWormholeResult(Guid profile, string text, bool succeeded)
        {
            try { ManualRecallFeedback?.Invoke(profile, text, succeeded); }
            catch (Exception e) { Trace.TraceWarning("Companion wormhole feedback: {0}", e.Message); }
        }
    }
}
