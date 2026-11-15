using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class SourceFlow
        {
            internal long Sent,Completed;internal bool Requested,Outstanding,Snapshot;
            internal readonly HashSet<int> SeenNpc=new HashSet<int>();
            internal readonly Queue<long> Pings=new Queue<long>();
        }
        private sealed class WorldLease
        {
            internal Actor Source,Pending;internal bool Waiting,AwaitBaseline,Terminated,PrimaryProjectile;internal long Barrier;
            internal int Key;

        }
        private readonly SourceFlow primarySource=new SourceFlow();

        private bool issuingPrimaryBarrier;
        private readonly Dictionary<int,WorldLease> npcSources=new Dictionary<int,WorldLease>();
        private readonly HashSet<int> terrainRequested=new HashSet<int>();
        private readonly Dictionary<int,WorldLease> projectileSources=new Dictionary<int,WorldLease>();
        private SourceFlow Flow(Actor a){return a==null?primarySource:a.WorldSource;}
        private void RetireObserver(Actor actor)
        {
            if(!HasActors)
            {
                // No released role remains retained by an idle world lease.
                // Keep only ping markers already on the primary wire so their
                // replies can drain without becoming vanilla user pings.
                npcSources.Clear();projectileSources.Clear();
                observedNpcs=null;observationStep=UInt32.MaxValue;
                primarySource.Snapshot=primarySource.Requested=false;primarySource.SeenNpc.Clear();return;
            }
            foreach(var lease in npcSources.Values)RetireLease(lease,actor);
            foreach(var lease in projectileSources.Values)RetireLease(lease,actor);
        }
        private void RetireLease(WorldLease lease,Actor actor)
        {
            if(!ReferenceEquals(lease.Source,actor) && (!lease.Waiting || !ReferenceEquals(lease.Pending,actor)))return;
            lease.Source=lease.Pending=null;lease.Waiting=lease.AwaitBaseline=true;
            lease.Barrier=primarySource.Sent+1;primarySource.Requested=true;
        }
        private void RequestSharedTerrain()
        {
            if(Main.netMode!=1 || Main.sectionManager==null)return;
            // The vanilla request loads a neighbourhood, including its framing.
            // Coalesce all roles by section and issue at most one new region per
            // outer update. Already decoded primary sections need no refresh.
            foreach(var a in actors.Values)
            {
                if(!a.Ready || a.Closing || a.Connection==null)continue;
                int x=Math.Max(10,Math.Min(Main.maxTilesX-11,(int)(a.Body.Center.X/16)));
                int y=Math.Max(10,Math.Min(Main.maxTilesY-11,(int)(a.Body.Center.Y/16)));
                int sx=Netplay.GetSectionX(x),sy=Netplay.GetSectionY(y),key=sx*65536+sy;
                if(Main.sectionManager.SectionLoaded(sx,sy) || !terrainRequested.Add(key))continue;
                NetMessage.SendData(8,-1,-1,null,x,y,a.Body.team);break;
            }
        }
        private bool SourceAlive(Actor a){return a==null?owner!=null:a.Ready && !a.Closing && a.Connection!=null;}
        private bool Route(WorldLease lease,Actor received,Vector2 point,bool baseline=false)
        {
            bool restart=false;
            if(lease.Waiting)
            {
                // A recalled stream cannot answer its outstanding barrier.
                // Retire only this object's pending choice and select afresh.
                if(!SourceAlive(lease.Pending)){lease.Waiting=false;lease.AwaitBaseline=true;restart=true;}
                else if(Flow(lease.Pending).Completed<lease.Barrier)return false;
                else {lease.Source=lease.Pending;lease.Waiting=false;lease.AwaitBaseline=true;}
            }
            Actor desired=Observer(point);
            bool sourceAlive=SourceAlive(lease.Source);
            var oldPosition=lease.Source==null?owner?.Center??point:lease.Source.Body.Center;
            var desiredPosition=desired==null?owner?.Center??point:desired.Body.Center;
            if(restart || !ReferenceEquals(desired,lease.Source) && (!sourceAlive || Vector2.DistanceSquared(oldPosition,point)>Vector2.DistanceSquared(desiredPosition,point)+256*256))
            {
                // Freeze only this object's old source BEFORE sending the new
                // source's ping. Its later reply is causally after every old
                // state already applied here; wall-clock arrival is not a stamp.
                var flow=Flow(desired);lease.Pending=desired;lease.Waiting=true;lease.Barrier=flow.Sent+1;flow.Requested=true;return false;
            }
            if(!ReferenceEquals(lease.Source,received))return false;
            if(baseline)lease.AwaitBaseline=false;
            return !lease.AwaitBaseline;
        }
        private bool RouteNpc(Actor source,byte[] data,int start,int length)
        {
            if(length<3)return false;int slot=data[start+1];WorldLease lease;
            if(data[start]==23 && Flow(source).Snapshot)Flow(source).SeenNpc.Add(slot);
            if(!npcSources.TryGetValue(slot,out lease)){lease=new WorldLease{Source=source,AwaitBaseline=data[start]!=23};npcSources.Add(slot,lease);if(data[start]==23)return true;}
            var npc=observedNpcs==null?Main.npc[slot]:observedNpcs[slot];
            var point=data[start]==23 && length>=19?new Vector2(BitConverter.ToSingle(data,start+3),BitConverter.ToSingle(data,start+7)):npc.Center;
            bool accepted=Route(lease,source,point,data[start]==23);
            return accepted && (data[start]==23 || npc.generation==data[start+2]);
        }
        private bool RouteProjectile(Actor source,byte[] data,int start,int length)
        {
            if(length<13)return false;var key=(ProjectileKey)BitConverter.ToInt32(data,start+1);int slot=key.Spawner*1024+key.Index;WorldLease lease;
            var point=new Vector2(BitConverter.ToSingle(data,start+5),BitConverter.ToSingle(data,start+9));
            // Native 29 is broadcast globally. The primary stream is the sole
            // terminal consumer, even while an AI source barrier is pending.
            // Retain its exact key so a late 27 cannot resurrect that identity.
            if(data[start]==29)
            {
                if(source!=null)return false;
                if(projectileSources.TryGetValue(slot,out lease))
                {int age=(key.Generation-((ProjectileKey)lease.Key).Generation)&16383;if(lease.Key!=(int)key && (age==0 || age>=8192))return false;}
                else {lease=new WorldLease();projectileSources.Add(slot,lease);}
                lease.Key=(int)key;lease.Terminated=true;return true;
            }
            if(!projectileSources.TryGetValue(slot,out lease))
            {
                Projectile old;bool known=key.TryGet(out old);
                lease=new WorldLease{Source=source,Key=(int)key,PrimaryProjectile=source==null};projectileSources.Add(slot,lease);
                if(data[start]==27 && !known)return true;
                lease.Source=null;
            }
            if(data[start]==27 && lease.Key!=(int)key)
            {
                var previous=(ProjectileKey)lease.Key;
                // Native generation is a 14-bit serial number. A late packet
                // from an older stream cannot resurrect the retired generation.
                int distance=(key.Generation-previous.Generation)&16383;
                if(distance==0 || distance>=8192 || !SourceAlive(source))return false;
                // The sender reused this native pool index, so its preceding
                // generation is retired. End a still-visible old replica
                // before NewProjectileSetup replaces the key lookup; a later
                // primary 29 can no longer look up that old object afterward.
                Projectile retired;if(previous.TryGetActive(out retired))retired.Kill();
                // This is a full birth of a DIFFERENT native identity, not a
                // delta handoff for the old object. Ordinary projectiles may
                // send this 27 only once, so admit it now from its live source;
                // never discard it behind the old generation's ping barrier.
                lease.Key=(int)key;lease.Source=source;lease.Pending=null;lease.Waiting=lease.AwaitBaseline=lease.Terminated=false;lease.PrimaryProjectile=source==null;return true;
            }
            if(lease.Key!=(int)key)return false;
            if(lease.Terminated)return false;
            // A legal primary 27 is the human native consumer's update, not
            // an optional AI observation. Once this identity is on that stream,
            // only its FIFO updates may write it; late secondary packets cannot
            // overwrite it or freeze its sole update behind an AI pong barrier.
            if(source==null){lease.PrimaryProjectile=true;lease.Source=lease.Pending=null;lease.Waiting=lease.AwaitBaseline=false;return true;}
            if(lease.PrimaryProjectile)return false;
            return Route(lease,source,point,true);
        }
        internal void PrimaryPingSent(){primarySource.Pings.Enqueue(issuingPrimaryBarrier?primarySource.Sent:0);}
        internal bool PrimaryPingReceived()
        {
            if(primarySource.Pings.Count==0)return false;long marker=primarySource.Pings.Dequeue();if(marker==0)return false;
            CompleteSource(null,marker);return true;
        }
        private void ActorPingReceived(Actor a)
        {if(a.WorldSource.Pings.Count!=0)CompleteSource(a,a.WorldSource.Pings.Dequeue());}
        private void CompleteSource(Actor source,long marker)
        {
            var flow=Flow(source);flow.Completed=marker;flow.Outstanding=false;
            bool refresh=false;
            foreach(var lease in npcSources.Values)if(lease.Waiting && ReferenceEquals(lease.Pending,source) && lease.Barrier<=marker)
            {lease.Source=source;lease.Waiting=false;lease.AwaitBaseline=true;refresh=true;}
            foreach(var lease in projectileSources.Values)if(lease.Waiting && ReferenceEquals(lease.Pending,source) && lease.Barrier<=marker)
            {lease.Source=source;lease.Waiting=false;lease.AwaitBaseline=true;}
            if(!refresh)return;
            // 154 establishes order only. Native 8 enumerates fresh active NPC
            // snapshots even when terrain sections were already sent; 49 ends
            // that enumeration. Missing slots then represent inactive bodies.
            flow.Snapshot=true;flow.SeenNpc.Clear();var p=source==null?owner:source.Body;
            int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);
            if(source==null)NetMessage.SendData(8,-1,-1,null,x,y,p.team);
            else source.Connection.Send(CompanionConnection.Packet(8,w=>{w.Write(x);w.Write(y);w.Write((byte)p.team);}));
        }
        internal void CompleteNpcSnapshot(Actor source)
        {
            var flow=Flow(source);if(!flow.Snapshot)return;flow.Snapshot=false;
            foreach(var pair in npcSources)
            {
                var lease=pair.Value;if(lease.Waiting || !lease.AwaitBaseline || !ReferenceEquals(lease.Source,source))continue;
                if(!flow.SeenNpc.Contains(pair.Key) && observedNpcs!=null)observedNpcs[pair.Key].active=false;
                lease.AwaitBaseline=false;
            }
            flow.SeenNpc.Clear();
        }
        private void FlushSourceRequests()
        {
            FlushSource(null);foreach(var a in actors.Values)if(a.Ready && !a.Closing && a.Connection!=null)FlushSource(a);
        }
        private void FlushSource(Actor a)
        {
            var flow=Flow(a);if(!flow.Requested || flow.Outstanding || flow.Snapshot)return;flow.Requested=false;flow.Outstanding=true;flow.Sent++;
            if(a!=null){flow.Pings.Enqueue(flow.Sent);a.Connection.Send(CompanionConnection.Packet(154));return;}
            issuingPrimaryBarrier=true;try{NetMessage.SendData(154);}finally{issuingPrimaryBarrier=false;}
        }
        private void ClearWorldSources()
        {npcSources.Clear();terrainRequested.Clear();projectileSources.Clear();observedNpcs=null;observationStep=UInt32.MaxValue;primarySource.Pings.Clear();primarySource.SeenNpc.Clear();primarySource.Sent=primarySource.Completed=0;primarySource.Snapshot=primarySource.Requested=primarySource.Outstanding=false;}
    }
}
