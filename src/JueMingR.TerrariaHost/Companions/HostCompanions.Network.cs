using System;
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
        internal Actor Decoding {get;private set;}
        private void PumpConnection(Actor a)
        {
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
                            byte[] old;if(a.InitialNpc.TryGetValue(slot,out old) && old[2]!=packet[2])a.InitialNpcBuffs.Remove(slot);
                            a.InitialNpc[slot]=packet;a.InitialNpcTicks[slot]=Main.GameUpdateCount;
                        }
                        // 54 has no generation field. Its identity is the last
                        // preceding 23 on this same FIFO stream; a later new
                        // generation cannot inherit that earlier buff frame.
                        else if(a.InitialNpc.ContainsKey(slot))a.InitialNpcBuffs[slot]=packet;
                        continue;
                    }
                    if(id==23)ObservePredictedNpc(a,packet,0,packet.Length);
                    if(id==154){ActorPingReceived(a);continue;}
                    if(id==162){StrikeAcknowledged(a);continue;}
                    if(id==49 && a.Ready){CompleteNpcSnapshot(a);continue;}
                    if(id==2)throw new IOException(NetworkText.Deserialize(r).ToString());
                    if(id==37)
                    {
                        if(String.IsNullOrEmpty(password)){a.WaitingPassword=true;a.Status="需要当前服务器密码";continue;}
                        a.Connection.Send(CompanionConnection.Packet(38,w=>w.Write(password)));continue;
                    }
                    if(id==3)
                    {
                        Attach(a,r.ReadByte());
                        using(CompanionHooks.Enter(a,a.Body.Center))
                        {
                            NetMessage.SendData(4,-1,-1,null,a.Body.whoAmI);NetMessage.SendData(16,-1,-1,null,a.Body.whoAmI);NetMessage.SendData(42,-1,-1,null,a.Body.whoAmI);
                            foreach(int slot in a.NetworkSlotIds)NetMessage.SendData(5,-1,-1,null,a.Body.whoAmI,slot);
                            NetMessage.SendData(6);
                        }
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
                        a.InitialNpcComplete=true;
                        Spawn(a,PlayerSpawnContext.SpawningIntoWorld);
                        using(CompanionHooks.Enter(a,a.Body.Center))NetMessage.SendData(12,-1,-1,null,a.Body.whoAmI,(byte)PlayerSpawnContext.SpawningIntoWorld);
                        continue;
                    }
                    if(id==129)
                    {
                        if(!a.InitialNpcComplete || !a.SpawnSent)throw new InvalidDataException("队友初始世界基线未完成。");
                        if(!a.Ready)
                        {
                            a.Ready=true;profiles.BeginPlay(a.Body);a.Status="跟随中";
                            foreach(var pair in a.InitialNpc){var state=pair.Value;if(RouteNpc(a,state,0,state.Length))DecodeNpcObservationAt(a,state,0,state.Length,a.InitialNpcTicks[pair.Key]);}
                            foreach(var buffs in a.InitialNpcBuffs.Values)
                            {WorldLease lease;int slot=BitConverter.ToInt16(buffs,1);if(npcSources.TryGetValue(slot,out lease) && lease.NpcBaseline && ReferenceEquals(lease.Source,a) && !lease.Waiting && !lease.AwaitBaseline)DecodeNpcObservation(a,buffs,0,buffs.Length);}
                            a.InitialNpc.Clear();a.InitialNpcBuffs.Clear();a.InitialNpcTicks.Clear();
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
                    try{int type;if(id==39)using(CompanionHooks.Enter(a,a.Aim))decoder.GetData(0,packet.Length,out type);else decoder.GetData(0,packet.Length,out type);}finally{Decoding=null;}
                }
            }
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
            return false;
        }
        internal bool AcceptPrimary(byte[] data,int start,int length)
        {
            if(ReadingObservation || Decoding!=null || length<1)return true;int id=data[start];if(id==49)CompleteNpcSnapshot(null);
            if(length<2)return true;var a=OwnedSlot(data[start+1]);
            // Own team echoes arrive on the human stream, excluding the AI's
            // sender connection. They cannot overwrite a later local team intent.
            if(a!=null && (id==4 || id==5 || id==12 || id==13 || id==16 || id==42 || id==45 || id==157 || id==50 || id==117))return false;
            if((id==27 || id==29) && length>=5)
            {var key=(ProjectileKey)BitConverter.ToInt32(data,start+1);Projectile projectile;if(key.TryGetActive(out projectile) && ProjectileActor(projectile)!=null)return false;if(key.Spawner!=owner?.whoAmI && (HasActors || projectileSources.Count!=0))return RouteProjectile(null,data,start,length);}
            // Primary NPC 23/28/162 always retain their original native order.
            // A second shared AI table is updated after successful consumption.
            return true;
        }
        private void Spawn(Actor a,PlayerSpawnContext context)
        {
            var p=a.Body;p.FindSpawn();if(!Player.CheckSpawn(p.SpawnX,p.SpawnY)){p.SpawnX=p.SpawnY=-1;}
            CompanionHooks.Spawning=p;try{p.Spawn(context);}finally{CompanionHooks.Spawning=null;}
            RestoreManualWeapon(a);
            // Native Spawn chooses the real position for joins and revivals.
            // The deserialized body's pre-spawn center is not a world cursor.
            a.Target=null;IdleAim(a);
            // Only the native restoration consumer borrows this body's owner
            // context. Spawn's human UI/refund/HostPlay branches stay closed.
            using(CompanionHooks.Enter(a,a.Aim))a.Minions.RestoreMinionsFor(p);
        }
        internal void Publish(Actor a)
        {
            if(a.Connection==null || !a.Ready || a.Closing)return;
            if(Main.GameUpdateCount<a.NextPublish)return;a.NextPublish=Main.GameUpdateCount+6;
            if(a.SyncType==null){a.SyncType=new int[a.NetworkSlotIds.Length];a.SyncStack=new int[a.SyncType.Length];a.SyncPrefix=new int[a.SyncType.Length];a.SyncBuffType=new int[a.Body.buffType.Length];a.SyncBuffTime=new int[a.Body.buffTime.Length];}
            using(CompanionHooks.Enter(a,a.Aim))
            {
                NetMessage.SendData(13,-1,-1,null,a.Body.whoAmI);NetMessage.SendData(16,-1,-1,null,a.Body.whoAmI);
                // Compare this role's native slots, not Main.clientPlayer. Only
                // actual differences emit inventory data on its own stream.
                for(int index=0;index<a.SyncType.Length;index++)
                {
                    int slot=a.NetworkSlotIds[index];
                    var item=new PlayerItemSlotID.SlotReference(a.Body,slot).Item;
                    if(a.SyncType[index]==item.type && a.SyncStack[index]==item.stack && a.SyncPrefix[index]==item.prefix)continue;
                    a.SyncType[index]=item.type;a.SyncStack[index]=item.stack;a.SyncPrefix[index]=item.prefix;NetMessage.SendData(5,-1,-1,null,a.Body.whoAmI,slot);a.Dirty=true;
                }
                if(a.SyncMana!=a.Body.statMana){a.SyncMana=a.Body.statMana;NetMessage.SendData(42,-1,-1,null,a.Body.whoAmI);}
                bool buffs=false;for(int i=0;i<a.SyncBuffType.Length;i++){buffs|=a.SyncBuffType[i]!=a.Body.buffType[i] || a.Body.buffTime[i]>a.SyncBuffTime[i];a.SyncBuffType[i]=a.Body.buffType[i];a.SyncBuffTime[i]=a.Body.buffTime[i];}
                if(buffs)NetMessage.SendData(50,-1,-1,null,a.Body.whoAmI);
            }
        }
    }
}
