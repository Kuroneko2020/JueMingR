using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Decode only value records produced by the authenticated worker. The
    // owning session supplies the original identity; wire slots cannot mint it.
    internal sealed class NativePredictionResult
    {
        internal NativePredictionAlignment.Frame[] Frames;
        internal NpcTrajectory Trajectory;
        internal int Kind,Slot,TileX,TileY;
        internal string Error;
        internal readonly SortedSet<int> Npcs=new SortedSet<int>(),Projectiles=new SortedSet<int>();
        internal double TotalMs,ResetMs,RestoreMs,AdvanceMs;
        internal static NativePredictionResult Read(byte[] core,byte[] alignment,NpcIdentity identity,long tick,long version,bool networkObservation)
        {
            var value=new NativePredictionResult();
            using(var stream=new MemoryStream(core,false))using(var r=new BinaryReader(stream))
            {
                int protocol=r.ReadInt32();
                if(protocol==-PredictionWire.Protocol)
                {value.Error=r.ReadString()+": "+r.ReadString();value.TileX=r.ReadInt32();value.TileY=r.ReadInt32();value.Kind=r.ReadInt32();value.Slot=r.ReadInt32();r.ReadInt32();End(stream);return value;}
                if(protocol!=PredictionWire.Protocol || r.ReadInt64()!=tick || r.ReadInt32()!=identity.Slot)throw new InvalidDataException("Native result identity.");
                int count=Count(r,181);if(count<2)throw new InvalidDataException("Native result horizon.");
                if(alignment==null)throw new InvalidDataException("Missing native alignment proof.");
                long advance;
                using(var data=new MemoryStream(alignment,false))using(var proof=new BinaryReader(data))
                {
                    if(proof.ReadInt32()!=count)throw new InvalidDataException("Alignment horizon.");value.Frames=new NativePredictionAlignment.Frame[count];
                    for(int i=0;i<count;i++)
                    {
                        var frame=NativePredictionAlignment.Read(proof);value.Frames[i]=frame;
                        if(frame.Tick!=tick+i || frame.HasState!=(i<=PredictionWire.MaximumAlignmentAge))throw new InvalidDataException("Alignment proof extent.");
                        if(frame.HasState && Array.IndexOf(frame.Npcs,identity.Slot)<0)throw new InvalidDataException("Alignment target absent.");
                    }
                    advance=proof.ReadInt64();End(data);
                }
                var points=new NpcTrajectoryPoint[count];int length=count,quality=0;bool ended=false;
                for(int i=0;i<count;i++)
                {
                    if(r.ReadInt32()!=i)throw new InvalidDataException("Native point order.");
                    int type=r.ReadInt32();bool active=r.ReadBoolean();
                    var state=new NpcMotionState{X=Float(r),Y=Float(r),Vx=Float(r),Vy=Float(r),Width=r.ReadInt32(),Height=r.ReadInt32(),A0=r.ReadSingle(),Life=r.ReadInt32()};
                    for(int j=0;j<4;j++)r.ReadInt32();double frame=r.ReadDouble();quality|=r.ReadInt32();
                    int netId=r.ReadInt32();int generation=r.ReadByte(),cause=r.ReadByte();
                    if(type<0 || type>=Terraria.ID.NPCID.Count || state.Width<1 || state.Height<1 || state.Width>8192 || state.Height>8192 || generation!=identity.Generation || cause>3 || double.IsNaN(frame) || double.IsInfinity(frame))throw new InvalidDataException("Native point geometry or birth.");
                    if(i==0 && (type!=identity.Type || netId!=identity.NetId || !active || cause!=0))throw new InvalidDataException("Native initial form.");
                    if(ended && cause==0 || active!=(cause==0))throw new InvalidDataException("Native lifetime continuity.");
                    if(cause!=0 && !ended){ended=true;length=Math.Max(1,i);}
                    var proof=value.Frames[i];state.Identity=new NpcIdentity(identity.Session,identity.Token,identity.Slot,identity.Generation,type,netId);
                    state.NetOffsetX=proof.NetOffset.X;state.NetOffsetY=proof.NetOffset.Y;state.CanReceive=proof.CanReceive;state.CanHarm=proof.CanHarm;state.NewSegment=proof.NewSegment;
                    points[i]=new NpcTrajectoryPoint(i,state);
                }
                int players=Count(r,255),prior=-1;for(int i=0;i<players;i++){int slot=r.ReadInt32();if(slot<=prior || slot>=255)throw new InvalidDataException("Native player order.");prior=slot;}
                for(int i=0;i<count*players*4;i++)Float(r);
                int size=Count(r,PredictionWire.MaximumBytes);long until=stream.Position+size;if(until>stream.Length-32)throw new InvalidDataException("Native dependency size.");
                for(int step=0;step<count;step++)
                {
                    if(r.ReadInt32()!=step)throw new InvalidDataException("Native dependency step.");
                    int npcs=Count(r,201);prior=-1;
                    for(int i=0;i<npcs;i++){int slot=r.ReadInt32();if(slot<=prior || slot>200)throw new InvalidDataException("Native NPC order.");prior=slot;value.Npcs.Add(slot);r.ReadByte();r.ReadInt32();r.ReadInt32();for(int j=0;j<4;j++)Float(r);r.ReadInt32();for(int j=0;j<4;j++)r.ReadSingle();}
                    int shots=Count(r,1001);prior=-1;
                    // AI slots can contain raw ProjectileKey bits, including
                    // IEEE NaN patterns. Only physical motion must be finite.
                    for(int i=0;i<shots;i++){int slot=r.ReadInt32();if(slot<=prior || slot>1000)throw new InvalidDataException("Native projectile order.");prior=slot;value.Projectiles.Add(slot);r.ReadUInt32();r.ReadInt32();for(int j=0;j<4;j++)Float(r);r.ReadInt32();for(int j=0;j<3;j++)r.ReadSingle();}
                }
                if(stream.Position!=until)throw new InvalidDataException("Native dependency extent.");
                long total=r.ReadInt64(),frequency=r.ReadInt64(),reset=r.ReadInt64(),restore=r.ReadInt64();if(frequency<=0 || reset<0 || restore<0 || total<reset+restore)throw new InvalidDataException("Native timing.");
                if(advance<0 || advance>total)throw new InvalidDataException("Native advance timing.");
                value.TotalMs=1000.0*total/frequency;value.ResetMs=1000.0*reset/frequency;value.RestoreMs=1000.0*restore/frequency;value.AdvanceMs=1000.0*advance/frequency;End(stream);
                var assumptions=PredictionAssumption.NoNewHits|PredictionAssumption.RandomRepresentative|PredictionAssumption.LocalTerrain|PredictionAssumption.HeldPlayerControls;
#if JMR_CONDITIONAL_RESEARCH
                assumptions|=PredictionAssumption.ApproximateMechanism;
#endif
                // Request-owned host observation, never the private world's
                // forced offline netMode or a later live game-thread read.
                if(networkObservation)assumptions|=PredictionAssumption.NetworkObservation;
                if((quality&6)!=0)assumptions|=PredictionAssumption.ApproximateMechanism;
                if((quality&8)!=0)assumptions|=PredictionAssumption.ObservedLighting;
                value.Trajectory=new NpcTrajectory(identity,tick,version,assumptions,ended?PredictionStop.Despawn:PredictionStop.None,points,length,PredictionStrategy.NativeIsolated);return value;
            }
        }
        private static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0 || n>max)throw new InvalidDataException("Native result count.");return n;}
        private static float Float(BinaryReader r){float n=r.ReadSingle();if(float.IsNaN(n)||float.IsInfinity(n))throw new InvalidDataException("Native result nonfinite.");return n;}
        private static void End(Stream stream){if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing native result bytes.");}
    }
}
