using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private readonly MessageBuffer decoder=new MessageBuffer{whoAmI=256};
        private readonly List<CompanionConnection> closingDeathFlows = new List<CompanionConnection>();
        private readonly Dictionary<int,Actor> joiningSlots = new Dictionary<int,Actor>();
        private long playerSlotFenceSequence, sendingPlayerSlotFence;
        internal Actor Decoding {get;private set;}

        internal void RecordActorPacket(Actor a, byte[] frame)
        {
            if (frame.Length >= 8 && frame[2] == 16 && frame[3] == a.Body.whoAmI)
            {
                a.SyncLife = BitConverter.ToInt16(frame, 4);
                a.SyncLifeMax = BitConverter.ToInt16(frame, 6);
                return;
            }
            if (frame.Length < 12 || frame[2] != 5 || frame[3] != a.Body.whoAmI) return;
            int slot = BitConverter.ToInt16(frame, 4);
            // NetworkSlots is ordered by the native slot IDs including its
            // real bank extents. Immediate writers (pickup, transfer, supplies)
            // advance the same FIFO baseline as the periodic scan.
            int index = Array.BinarySearch(a.NetworkSlotIds, slot);
            if (index < 0) return;
            if (a.SyncType == null)
            {
                a.SyncType = new int[a.NetworkSlotIds.Length];
                a.SyncStack = new int[a.SyncType.Length];
                a.SyncPrefix = new int[a.SyncType.Length];
                a.SyncFavorite = new bool[a.SyncType.Length];
            }
            a.SyncType[index] = BitConverter.ToInt16(frame, 9);
            a.SyncStack[index] = BitConverter.ToInt16(frame, 6);
            a.SyncPrefix[index] = frame[8];
            a.SyncFavorite[index] = (frame[11] & 1) != 0;
        }

        internal void SharedHealApplied(int slot)
        {
            var a = OwnedSlot(slot);
            if (a != null) a.Dirty = true;
        }

        private void RetireConnection(Actor a)
        {
            var connection = a.Connection;
            if (connection == null) return;
            RetireWormholes(a);
            string deathFailure = null;
            bool unconfirmedDeath = connection.HasUnconfirmedDeath;
            if (unconfirmedDeath && !connection.Finished)
            {
                // The native server may discard unread data as soon as the
                // socket closes. Its ordered 154 response proves it consumed
                // the preceding death frame and native settlement, including
                // through the Steam relay. The detached body cannot send again.
                try
                {
                    if (connection.CloseAfterDeath())
                    {
                        closingDeathFlows.Add(connection);
                        return;
                    }
                }
                catch (Exception e) { deathFailure = e.Message; }
            }
            if (unconfirmedDeath)
                Feedback = deathFailure == null ? "无法确认服务器已处理队友死亡。" : "无法确认服务器已处理队友死亡：" + deathFailure;
            connection.Dispose();
        }

        private void PumpRetiringConnections()
        {
            for (int i = closingDeathFlows.Count - 1; i >= 0; i--)
            {
                var flow = closingDeathFlows[i];
                // The TCP byte worker can finish even when a world unloads.
                // Steam callbacks and its relay close stay on the game thread.
                if (!(flow.IsRelay && flow.DeathCloseReady()) && !flow.Finished) continue;
                if (!flow.DeathConfirmed) Feedback = "无法确认服务器已处理队友死亡。";
                flow.Dispose();
                closingDeathFlows.RemoveAt(i);
            }
        }

        private void ClearRetiringConnections()
        {
            foreach (var flow in closingDeathFlows) flow.Dispose();
            closingDeathFlows.Clear();
        }

        private void EndRetiringConnectionSession()
        {
            PumpRetiringConnections();
            for (int i = closingDeathFlows.Count - 1; i >= 0; i--)
            {
                if (!closingDeathFlows[i].IsRelay) continue;
                closingDeathFlows[i].Dispose();
                closingDeathFlows.RemoveAt(i);
                Feedback = "Steam 会话已结束，无法确认服务器已处理队友死亡。";
            }
        }
        private void PumpConnection(Actor a)
        {
            AdvanceReservedSpawn(a);
            // Bound native work per outer update, while retaining the byte
            // transport's own overflow failure instead of dropping resource data.
            for(int n=0;n<96;n++)
            {
                var packet=a.Connection.Receive();if(packet==null)break;
                using(var stream=new MemoryStream(packet,false))using(var r=new BinaryReader(stream))
                {
                    byte id=r.ReadByte();
                    if(id==90){DecodePersonalItem(a,packet);continue;}
                    // Server instance expiry/reuse is FIFO with this role's
                    // 90 births. Retire its sparse copy only; the primary
                    // connection still owns the one shared native151 consumer.
                    if(id==151){if(packet.Length<3)throw new InvalidDataException("物品结束帧不完整。");a.PersonalItems.Remove(BitConverter.ToInt16(packet,1));continue;}
                    if(!a.Ready && (id==23 || id==54))
                    {
                        int slot=id==23?packet[1]:BitConverter.ToInt16(packet,1);
                        if(slot<0 || slot>=Main.maxNPCs)throw new InvalidDataException("初始人物观察槽位无效。");
                        // Native initial NPCs precede 49/129. Retain at most
                        // one full state/buff frame per actual slot until the
                        // validated world has a spawned, consumable body.
                        if(id==23)
                        {
                            byte[] old;if(a.InitialNpc.TryGetValue(slot,out old) && old[2]!=packet[2]){a.InitialNpcBuffs.Remove(slot);a.InitialNpcHousing.Remove(slot);}
                            a.InitialNpc[slot]=packet;a.InitialNpcTicks[slot]=Main.GameUpdateCount;
                        }
                        // 54 has no generation field. Its identity is the last
                        // preceding 23 on this same FIFO stream; a later new
                        // generation cannot inherit that earlier buff frame.
                        else if(a.InitialNpc.ContainsKey(slot))a.InitialNpcBuffs[slot]=packet;
                        continue;
                    }
                    if(id==60)
                    {
                        if(a.Ready)ObserveNpcHousing(a,packet,0,packet.Length);
                        else if(packet.Length>=3)
                        {
                            int slot=BitConverter.ToInt16(packet,1);
                            if(a.InitialNpc.ContainsKey(slot))a.InitialNpcHousing[slot]=packet;
                        }
                        continue;
                    }
                    if(id==23)ObservePredictedNpc(a,packet,0,packet.Length);
                    if(id==154){ActorPingReceived(a);continue;}
                    if(id==162){StrikeAcknowledged(a);continue;}
                    if(id==49 && a.Ready){CompleteNpcSnapshot(a);continue;}
                    if(id==2)
                    {
                        if (RejectPassword(a, r)) return;
                        throw new IOException(NetworkText.Deserialize(r).ToString());
                    }
                    if(id==37)
                    {
                        AskPassword(a);
                        continue;
                    }
                    if(id==3)
                    {
                        ValidatePasswordAcceptance(a);
                        ReserveSlot(a,r.ReadByte());
                        AcceptPassword(a);
                        var previous = Main.player[a.ReservedSlot];
                        Main.player[a.ReservedSlot] = a.Body;
                        try
                        {
                            // Native writers read Main.player[slot]. Borrow
                            // just that serializer input, then restore the
                            // primary's still-authoritative remote body.
                            using(CompanionHooks.Enter(a,a.Body.Center))
                            {
                                NetMessage.SendData(4,-1,-1,null,a.Body.whoAmI);NetMessage.SendData(16,-1,-1,null,a.Body.whoAmI);NetMessage.SendData(42,-1,-1,null,a.Body.whoAmI);
                                PublishSlots(a, true);
                                NetMessage.SendData(6);
                            }
                        }
                        finally { Main.player[a.ReservedSlot] = previous; }
                        continue;
                    }
                    if(id==7 && !a.SpawnSent)
                    {
                        r.ReadInt32();r.ReadByte();r.ReadByte();r.ReadInt16();r.ReadInt16();r.ReadInt16();r.ReadInt16();r.ReadInt16();r.ReadInt16();r.ReadInt32();r.ReadString();byte mode=r.ReadByte();var guid=new Guid(r.ReadBytes(16));
                        if(Main.ActiveWorldFileData==null || guid!=Main.ActiveWorldFileData.UniqueId || (mode==3)!=(a.Body.difficulty==3))throw new IOException("队友连接的世界与当前世界不一致。");
                        a.Connection.Send(CompanionConnection.Packet(8,w=>{w.Write(-1);w.Write(-1);w.Write((byte)a.Body.team);}));a.SpawnSent=true;continue;
                    }
                    if(id==49 && !a.Ready)
                    {
                        if(!a.SpawnSent || a.ReservedSlot<0)throw new InvalidDataException("队友初始世界基线未完成。");
                        if(!a.InitialNpcComplete){a.InitialNpcComplete=true;FencePrimarySlot(a);}
                        continue;
                    }
                    if(id==129)
                    {
                        if(!a.BirthSubmitted)throw new InvalidDataException("队友初始世界基线未完成。");
                        if(!a.Ready)
                        {
                            if(a.Connection.Finished)continue;
                            Attach(a,a.ReservedSlot);
                            ReleaseReservedSlot(a);
                            RestoreMinions(a);
                            a.Ready=true;profiles.BeginPlay(a.Body);a.Status="跟随中";RetireJoinFeedback(a);
                            foreach(var pair in a.InitialNpc){var state=pair.Value;if(RouteNpc(a,state,0,state.Length))DecodeNpcObservationAt(a,state,0,state.Length,a.InitialNpcTicks[pair.Key]);}
                            foreach(var buffs in a.InitialNpcBuffs.Values)
                            {WorldLease lease;int slot=BitConverter.ToInt16(buffs,1);if(npcSources.TryGetValue(slot,out lease) && lease.NpcBaseline && ReferenceEquals(lease.Source,a) && !lease.Waiting && !lease.AwaitBaseline)DecodeNpcObservation(a,buffs,0,buffs.Length);}
                            foreach(var housing in a.InitialNpcHousing.Values)ObserveNpcHousing(a,housing,0,housing.Length);
                            a.InitialNpc.Clear();a.InitialNpcBuffs.Clear();a.InitialNpcHousing.Clear();a.InitialNpcTicks.Clear();
                        }
                        continue;
                    }
                    if(id==5 || id==13 || id==16 || id==117 || id==4 || id==12 || id==14)continue;
                    if(id==23 || id==28){if(RouteNpc(a,packet,0,packet.Length))DecodeNpcObservation(a,packet,0,packet.Length);continue;}
                    if(id==54){WorldLease lease;int slot=packet.Length>=3?BitConverter.ToInt16(packet,1):-1;if(slot>=0 && npcSources.TryGetValue(slot,out lease) && lease.NpcBaseline && !lease.Waiting && !lease.AwaitBaseline && ReferenceEquals(lease.Source,a))DecodeNpcObservation(a,packet,0,packet.Length);continue;}
                    // World facts have one stream at a time. Source changes
                    // wait for an ordered native ping barrier, not arrival time.
                    if(!WorldPacket(a,packet))continue;
                    if(packet.Length>decoder.readBuffer.Length)throw new InvalidDataException("队友世界数据帧过大。");
                    Buffer.BlockCopy(packet,0,decoder.readBuffer,0,packet.Length);decoder.ResetReader();Decoding=a;
                    try
                    {
                        if (id == 65) decodingWormhole = MatchWormholeReply(a, packet);
                        int type;
                        if (id == 39 || id == 65)
                            using (CompanionHooks.Enter(a, a.Aim)) decoder.GetData(0, packet.Length, out type);
                        else decoder.GetData(0, packet.Length, out type);
                    }
                    finally { Decoding = null; decodingWormhole = null; }
                }
            }
            PumpWormholes(a);
        }
        private Actor Observer(Vector2 position)
        {
            float best=owner==null?float.MaxValue:Vector2.DistanceSquared(owner.Center,position);Actor chosen=null;
            foreach(var a in actors.Values)if(a.Ready && !a.Closing && a.Connection!=null && a.Body.active)
            {float d=Vector2.DistanceSquared(a.Body.Center,position);if(d<best){best=d;chosen=a;}}
            return chosen;
        }
        private bool WorldPacket(Actor a,byte[] p)
        {
            if(p.Length<2)return false;int id=p[0];
            if(id==23 || id==28)return RouteNpc(a,p,0,p.Length);
            if(id==27 || id==29)
            {var key=(ProjectileKey)BitConverter.ToInt32(p,1);Projectile owned;if(key.Spawner==owner?.whoAmI || key.TryGetActive(out owned) && ProjectileActor(owned)!=null)return false;return RouteProjectile(a,p,0,p.Length);}
            // Terrain has one permanent decoder: the human primary stream.
            // It explicitly loads every active body's needed region below.
            // Secondary initial sections must never overwrite that shared map.
            if(id==10 || id==17 || id==19 || id==20 || id==48)return false;
            // 21/22/151 are already broadcast to the primary connection even
            // in distant regions. 39 is directed to the reserved player only.
            if(id==39 && p.Length>=3){int slot=BitConverter.ToInt16(p,1);return slot>=0 && slot<Main.maxItems && Main.item[slot].playerIndexTheItemIsReservedFor==a.Body.whoAmI;}
            if(id==65)return DirectedPlayerTeleport(p,0,p.Length)==a.Body.whoAmI && a.Ready && !a.Closing && a.Body.active && ReferenceEquals(Owned(a.Body),a);
            return false;
        }
        // Server player-teleport (mode 0) carries its slot after the flags,
        // unlike the team/control messages. Consume it once on its own FIFO
        // so native GetData applies the position and replies with mode 3.
        // The server protects that position against stale 13 until this ACK.
        private static int DirectedPlayerTeleport(byte[] data,int start,int length)
        {return length>=13 && (data[start+1]&3)==0 ? BitConverter.ToInt16(data,start+2) : -1;}
        internal bool AcceptPrimary(byte[] data,int start,int length)
        {
            if(ReadingObservation || Decoding!=null || length<1)return true;int id=data[start];if(id==49)CompleteNpcSnapshot(null);
            if(length<2)return true;int slot=data[start+1];var a=OwnedSlot(slot);
            if(id==65 && OwnedSlot(DirectedPlayerTeleport(data,start,length)) is Actor receiver && receiver.Ready &&
                !receiver.Closing && receiver.Body.active && receiver.Connection!=null)return false;
            if(id==14 && length>=3)return AcceptPrimarySlotChange(a,slot,data[start+2]==1);
            // Own team echoes arrive on the human stream, excluding the AI's
            // sender connection. They cannot overwrite a later local team intent.
            // Portal 96 is also a client-owned echo: the actual player's local
            // portal consumer already applied it and submitted its movement.
            if(a!=null && (id==4 || id==5 || id==12 || id==13 || id==16 || id==42 || id==45 || id==96 || id==157 || id==50 || id==117 || id==118 || id==135))return false;
            if((id==27 || id==29) && length>=5)
            {var key=(ProjectileKey)BitConverter.ToInt32(data,start+1);Projectile projectile;if(key.TryGetActive(out projectile) && ProjectileActor(projectile)!=null)return false;if(key.Spawner!=owner?.whoAmI && (HasActors || projectileSources.Count!=0))return RouteProjectile(null,data,start,length);}
            // Primary NPC 23/28/162 always retain their original native order.
            // A second shared AI table is updated after successful consumption.
            return true;
        }
        private bool AcceptPrimarySlotChange(Actor installed, int slot, bool active)
        {
            var a = installed;
            if (a == null) joiningSlots.TryGetValue(slot, out a);
            if (a == null || !a.PrimarySlotConfirmed) return true;

            // Reset and SyncDisconnectedPlayer can straddle the marker. An
            // off before our first on cannot end this activation. Until 12,
            // no on can belong to it. The independently reserved slot never
            // hides either event from the original remote player.
            if (!a.Connection.Finished && (!a.BirthSubmitted || !active && !a.PrimarySlotSawJoin))
                return installed == null;

            // This stream has at most one join for the submitted 12. Either
            // its following off or a second on marks a real loss; vanilla can
            // reuse a lost socket before broadcasting a separate off.
            if (a.Connection.Finished || !active || a.PrimarySlotSawJoin)
            {
                CloseActor(a, a.Connection.Error ?? "队友连接已断开。");
                if (active) Main.player[slot].active = false;
                return true;
            }
            a.PrimarySlotSawJoin = true;
            // Until 129 arrives the real remote slot keeps consuming native
            // packets, even if this 14 later turns out to be another player.
            return installed == null;
        }
        private void ReserveSlot(Actor a, int slot)
        {
            if (slot < 0 || slot >= Main.maxPlayers || slot == Main.myPlayer || a.ReservedSlot >= 0)
                throw new InvalidOperationException("服务器分配了冲突的玩家槽位。");
            var previous = OwnedSlot(slot);
            if (previous == null) joiningSlots.TryGetValue(slot, out previous);
            // A fresh native 3 on another socket is already proof that the
            // server reassigned this numeric slot.
            if (previous != null) CloseActor(previous, "队友连接已断开。");
            a.ReservedSlot=slot;a.Body.whoAmI=slot;a.NetworkSlotIds=NetworkSlots(a.Body);
            joiningSlots.Add(slot,a);
        }
        private void ReleaseReservedSlot(Actor a)
        {
            if(a.ReservedSlot<0)return;
            if(joiningSlots.TryGetValue(a.ReservedSlot,out var holder) && ReferenceEquals(holder,a))
                joiningSlots.Remove(a.ReservedSlot);
            a.ReservedSlot=-1;
        }
        private void FencePrimarySlot(Actor a)
        {
            // The secondary has reserved its slot, but has not submitted 12.
            // Drain previous generations on the primary into its original
            // body before admitting any of this secondary's birth traffic.
            a.PrimarySlotFence = (++playerSlotFenceSequence << 8) | (byte)a.ReservedSlot;
            a.PrimarySlotDeadline = Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
            sendingPlayerSlotFence = a.PrimarySlotFence;
            try { NetMessage.SendData(154); }
            finally { sendingPlayerSlotFence = 0; }
        }
        private void CompletePrimarySlotFence(long marker)
        {
            if (joiningSlots.TryGetValue((byte)marker,out var a) && a.PrimarySlotFence == marker) a.PrimarySlotConfirmed = true;
        }
        private void AdvanceReservedSpawn(Actor a)
        {
            if(a.ReservedSlot<0 || !a.InitialNpcComplete || a.BirthSubmitted || a.Connection.Finished)return;
            if(!a.PrimarySlotConfirmed)
            {
                if(Stopwatch.GetTimestamp()>=a.PrimarySlotDeadline)throw new IOException("等待服务器确认队友出生槽位超时。");
                return;
            }
            int slot=a.ReservedSlot;
            var previous=Main.player[slot];
            Main.player[slot]=a.Body;
            try
            {
                a.Body.active=true;a.Body.ghost=false;
                SpawnBody(a,PlayerSpawnContext.SpawningIntoWorld);
                using(var scope=CompanionHooks.Enter(a,a.Body.Center))
                {
                    NetMessage.SendData(12,-1,-1,null,slot,(byte)PlayerSpawnContext.SpawningIntoWorld);
                    if(!scope.PacketAccepted)throw new IOException("队友连接已结束。");
                    a.BirthSubmitted=true;
                }
            }
            finally{Main.player[slot]=previous;}
        }
        private void Spawn(Actor a,PlayerSpawnContext context)
        {
            SpawnBody(a,context);
            RestoreMinions(a);
        }
        internal void RecallFromItem(Actor actor,int humanIndex)
        {
            // Native mirror use enters Spawn from inside the item's actor
            // scope. Reuse the genuine body/bed spawn without local-human
            // camera, UI or static minion ownership. Send the native recall
            // on the item's connection after restoring its owner scope.
            int previous=Main.myPlayer;
            Main.myPlayer=humanIndex;
            try { SpawnBody(actor,PlayerSpawnContext.RecallFromItem); }
            finally { Main.myPlayer=previous; }
            if(actor.Connection!=null)NetMessage.SendData(12,-1,-1,null,actor.Body.whoAmI,(byte)PlayerSpawnContext.RecallFromItem);
            RestoreMinions(actor);
        }
        private void SpawnBody(Actor a,PlayerSpawnContext context)
        {
            var p=a.Body;p.FindSpawn();if(!Player.CheckSpawn(p.SpawnX,p.SpawnY)){p.SpawnX=p.SpawnY=-1;}
            CompanionHooks.Spawning=p;try{p.Spawn(context);}finally{CompanionHooks.Spawning=null;}
            // Native Spawn chooses the real position for joins and revivals.
            // The deserialized body's pre-spawn center is not a world cursor.
            a.Mobility.Reset(a.Generation);
            a.Behavior=new CompanionBehaviorHost();a.Combat.Reset();SetAttackTarget(a,null);IdleAim(a);
        }
        private void RestoreMinions(Actor a)
        {
            // Only the native restoration consumer borrows this body's owner
            // context, after the body belongs to the confirmed activation.
            using(CompanionHooks.Enter(a,a.Aim))a.Minions.RestoreMinionsFor(a.Body);
        }
        internal void Publish(Actor a)
        {
            if(a.Connection==null || !a.Ready || a.Closing)return;
            if(Main.GameUpdateCount<a.NextPublish)return;a.NextPublish=Main.GameUpdateCount+6;
            if(a.SyncBuffType==null){a.SyncBuffType=new int[a.Body.buffType.Length];a.SyncBuffTime=new int[a.Body.buffTime.Length];}
            using(CompanionHooks.Enter(a,a.Aim))
            {
                NetMessage.SendData(13,-1,-1,null,a.Body.whoAmI);
                // A periodic unchanged absolute 16 would roll back the
                // server's relative heal before its primary 66 reaches us.
                if (a.Body.statLife != a.SyncLife || a.Body.statLifeMax != a.SyncLifeMax)
                    NetMessage.SendData(16,-1,-1,null,a.Body.whoAmI);
                PublishSlots(a, false);
                if(a.SyncMana!=a.Body.statMana){a.SyncMana=a.Body.statMana;NetMessage.SendData(42,-1,-1,null,a.Body.whoAmI);}
                bool buffs=false;for(int i=0;i<a.SyncBuffType.Length;i++){buffs|=a.SyncBuffType[i]!=a.Body.buffType[i] || a.Body.buffTime[i]>a.SyncBuffTime[i];a.SyncBuffType[i]=a.Body.buffType[i];a.SyncBuffTime[i]=a.Body.buffTime[i];}
                if(buffs)NetMessage.SendData(50,-1,-1,null,a.Body.whoAmI);
            }
        }
        internal void PublishSlotsBeforeDeath(Actor a)
        {
            if (a.Connection == null || !a.Ready || a.Closing) return;
            // The server's KillMe drops the inventory it already knows.
            // Capture pre-DropItems state here, before native 118 on this FIFO.
            PublishSlots(a, false);
        }
        private static void PublishSlots(Actor a, bool full)
        {
            full |= a.SyncType == null;
            for (int i = 0; i < a.NetworkSlotIds.Length; i++)
            {
                int slot = a.NetworkSlotIds[i];
                var item = new PlayerItemSlotID.SlotReference(a.Body, slot).Item;
                if (!full && (item.IsAir
                    ? a.SyncType[i] == 0
                    : a.SyncType[i] == item.type && a.SyncStack[i] == item.stack && a.SyncPrefix[i] == item.prefix && a.SyncFavorite[i] == item.favorited)) continue;
                NetMessage.SendData(5, -1, -1, null, a.Body.whoAmI, slot);
                if (!full) a.Dirty = true;
            }
        }
    }
}
