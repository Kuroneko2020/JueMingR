using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Owned by one captured request. Events cannot be cancelled by healing or
    // reduced to a set of source slots. The limit bounds both storage and wire
    // work; overflow invalidates the proof instead of dropping old events.
    internal sealed class NativeImpactProof
    {
        internal const int MaximumHits=4096;
        internal struct Hit
        {
            internal byte Kind; // 0: successful NPC hit; 1: completed native birth.
            internal long Tick;
            internal int SourceSlot,SourceType,SourceOwner,TargetSlot,SharedSlot,SourceGeneration,SourceEpoch;
            internal uint SourceKey;
            internal ulong Signature;
            internal Projectile Source;
            internal bool Known;
            internal Entity Parent;
            internal int ParentKind,ParentSlot,ParentType,ParentGeneration,ParentOwner,ParentEpoch;
            internal uint ParentKey;
            internal ulong Origin;
        }
        private struct SourceIdentity
        {
            internal Projectile Token;
            internal int Slot,Type,Owner,Generation,Epoch;
            internal uint Key;
            internal bool Active;
            internal bool Matches(Hit hit)
            {return Active && hit.Known && ReferenceEquals(Token,hit.Source) && Slot==hit.SourceSlot && Type==hit.SourceType && Owner==hit.SourceOwner && Key==hit.SourceKey && Generation==hit.SourceGeneration;}
        }
        private struct ParentIdentity {internal NPC Token;internal int Slot,Type,Generation;}
        private static readonly FieldInfo Generations=typeof(Projectile).GetField("slotGenerations",BindingFlags.Static|BindingFlags.NonPublic);
        internal static int Generation(int slot)=>((int[])Generations.GetValue(null))[slot];
        private readonly SourceIdentity[] sources;
        private readonly SourceIdentity[] initialSources;
        private readonly ParentIdentity[] parents;
        private readonly List<Hit> hits=new List<Hit>();
        internal NativeImpactProof(int[] projectiles):this(projectiles,new int[0]){}
        internal NativeImpactProof(int[] projectiles,int[] npcs)
        {
            sources=new SourceIdentity[projectiles.Length];
            for(int i=0;i<sources.Length;i++)
            {int slot=projectiles[i];var p=Main.projectile[slot];sources[i]=new SourceIdentity{Token=p,Slot=slot,Type=p.type,Owner=p.owner,Key=(uint)p.key,Active=p.active,Generation=Generation(slot),Epoch=-1};}
            initialSources=(SourceIdentity[])sources.Clone();
            parents=new ParentIdentity[npcs.Length];
            for(int i=0;i<parents.Length;i++){int slot=npcs[i];var n=Main.npc[slot];parents[i]=new ParentIdentity{Token=n,Slot=slot,Type=n.type,Generation=n.generation};}
        }
        internal bool Owns(Projectile value)
        {foreach(var source in sources)if(source.Active && ReferenceEquals(source.Token,value))return true;return false;}
        internal bool HasPage(Projectile value)
        {foreach(var source in sources)if(ReferenceEquals(source.Token,value))return true;return false;}
        internal bool Record(Hit hit,long capture)
        {
            if(hit.Tick<=capture || hit.Tick-capture>PredictionWire.MaximumAlignmentAge || hits.Count>=MaximumHits)return false;
            foreach(var source in sources)if(source.Matches(hit)){hit.SourceEpoch=source.Epoch;hit.Source=null;hits.Add(hit);return true;}
            return false;
        }
        // A binding advances only after the original factory returns normally.
        // Full generation and sequence ordinal prevent key wrap or another
        // same-slot lifetime from borrowing a captured source's hit authority.
        internal bool RecordBirth(Hit value,long capture)
        {
            if(!value.Known || value.Kind!=1 || value.Tick<=capture || value.Tick-capture>PredictionWire.MaximumAlignmentAge || hits.Count>=MaximumHits)return false;
            bool parent=false;value.ParentEpoch=-1;
            if(value.ParentKind==1)foreach(var p in parents)
                if(ReferenceEquals(p.Token,value.Parent) && p.Slot==value.ParentSlot && p.Type==value.ParentType && p.Generation==value.ParentGeneration && ReferenceEquals(Main.npc[p.Slot],p.Token) && p.Token.type==p.Type && p.Token.generation==p.Generation){parent=true;break;}
            if(value.ParentKind==2)foreach(var p in sources)
                if(p.Active && ReferenceEquals(p.Token,value.Parent) && p.Slot==value.ParentSlot && p.Type==value.ParentType && p.Generation==value.ParentGeneration && p.Key==value.ParentKey && p.Owner==value.ParentOwner && ReferenceEquals(Main.projectile[p.Slot],p.Token)
                    && p.Token.type==p.Type && p.Token.owner==p.Owner && (uint)p.Token.key==p.Key && Generation(p.Slot)==p.Generation)
                {parent=true;value.ParentEpoch=p.Epoch;break;}
            if(!parent)return false;
            for(int i=0;i<sources.Length;i++)
            {
                var source=sources[i];if(!ReferenceEquals(source.Token,value.Source) || source.Slot!=value.SourceSlot)continue;
                if(!ReferenceEquals(Main.projectile[source.Slot],source.Token) || value.SourceGeneration!=unchecked(source.Generation+1))return false;
                value.SourceEpoch=hits.Count;source.Epoch=value.SourceEpoch;source.Generation=value.SourceGeneration;
                source.Key=value.SourceKey;source.Type=value.SourceType;source.Owner=value.SourceOwner;source.Active=true;sources[i]=source;
                value.Source=null;value.Parent=null;hits.Add(value);return true;
            }
            return false;
        }
        internal string Difference(Hit[] expected,long tick,int[] npcs)
        {
            int index=0;
            foreach(var hit in expected)
            {
                if(hit.Tick>tick)break;
                if(hit.Kind==0 && !Touches(npcs,hit))continue;
                if(index>=hits.Count || !Same(hit,hits[index++]))return "NPC impact history";
            }
            return index==hits.Count?null:"extra NPC impact";
        }
        // Validate the whole returned journal before using any of its future
        // trajectory. Matching the elapsed prefix alone cannot authenticate a
        // malformed or uncaptured future lifetime/parent in the same packet.
        internal bool ValidTimeline(Hit[] events)
        {
            var predicted=(SourceIdentity[])initialSources.Clone();
            for(int index=0;index<events.Length;index++)
            {
                var e=events[index];int source=Array.FindIndex(predicted,s=>s.Slot==e.SourceSlot);
                if(!e.Known || source<0)return false;
                var p=predicted[source];
                if(e.Kind==0)
                {if(!p.Active || p.Type!=e.SourceType || p.Owner!=e.SourceOwner || p.Key!=e.SourceKey || p.Generation!=e.SourceGeneration || p.Epoch!=e.SourceEpoch)return false;continue;}
                bool parent=false;
                if(e.ParentKind==1)foreach(var n in parents)if(n.Slot==e.ParentSlot && n.Type==e.ParentType && n.Generation==e.ParentGeneration && e.ParentEpoch==-1){parent=true;break;}
                if(e.ParentKind==2)foreach(var shot in predicted)if(shot.Active && shot.Slot==e.ParentSlot && shot.Type==e.ParentType && shot.Generation==e.ParentGeneration && shot.Key==e.ParentKey && shot.Owner==e.ParentOwner && shot.Epoch==e.ParentEpoch){parent=true;break;}
                if(!parent || e.SourceGeneration!=unchecked(p.Generation+1) || e.SourceEpoch!=index)return false;
                p.Active=true;p.Type=e.SourceType;p.Owner=e.SourceOwner;p.Key=e.SourceKey;p.Generation=e.SourceGeneration;p.Epoch=index;predicted[source]=p;
            }
            return true;
        }
        private static bool Same(Hit a,Hit b)
        {return a.Kind==b.Kind && a.Tick==b.Tick && a.Known==b.Known && a.SourceSlot==b.SourceSlot && a.SourceType==b.SourceType && a.SourceOwner==b.SourceOwner && a.SourceKey==b.SourceKey && a.SourceGeneration==b.SourceGeneration && a.SourceEpoch==b.SourceEpoch && a.TargetSlot==b.TargetSlot && a.SharedSlot==b.SharedSlot && a.Signature==b.Signature
            && (a.Kind==0 || a.ParentKind==b.ParentKind && a.ParentSlot==b.ParentSlot && a.ParentType==b.ParentType && a.ParentGeneration==b.ParentGeneration && a.ParentKey==b.ParentKey && a.ParentOwner==b.ParentOwner && a.ParentEpoch==b.ParentEpoch && a.Origin==b.Origin);}
        internal static bool Touches(int[] npcs,Hit hit)
        {return Array.IndexOf(npcs,hit.TargetSlot)>=0 || hit.SharedSlot>=0 && Array.IndexOf(npcs,hit.SharedSlot)>=0;}
        internal static void Write(BinaryWriter writer,List<Hit> hits)
        {
            writer.Write(hits.Count);
            foreach(var hit in hits)
            {
                writer.Write(hit.Kind);writer.Write(hit.Tick);writer.Write(hit.Known);writer.Write(hit.SourceSlot);writer.Write(hit.SourceType);writer.Write(hit.SourceOwner);writer.Write(hit.SourceKey);writer.Write(hit.SourceGeneration);writer.Write(hit.SourceEpoch);writer.Write(hit.TargetSlot);writer.Write(hit.SharedSlot);writer.Write(hit.Signature);
                if(hit.Kind==1){writer.Write(hit.ParentKind);writer.Write(hit.ParentSlot);writer.Write(hit.ParentType);writer.Write(hit.ParentGeneration);writer.Write(hit.ParentKey);writer.Write(hit.ParentOwner);writer.Write(hit.ParentEpoch);writer.Write(hit.Origin);}
            }
        }
        internal void WriteEvents(BinaryWriter writer){Write(writer,hits);}
        internal static Hit[] Read(BinaryReader reader,long capture,int frames)
        {
            int count=reader.ReadInt32();if(count<0 || count>MaximumHits)throw new InvalidDataException("NPC impact proof capacity.");
            var hits=new Hit[count];long prior=capture;
            for(int i=0;i<count;i++)
            {
                var hit=new Hit{Kind=reader.ReadByte(),Tick=reader.ReadInt64(),Known=reader.ReadBoolean(),SourceSlot=reader.ReadInt32(),SourceType=reader.ReadInt32(),SourceOwner=reader.ReadInt32(),SourceKey=reader.ReadUInt32(),SourceGeneration=reader.ReadInt32(),SourceEpoch=reader.ReadInt32(),TargetSlot=reader.ReadInt32(),SharedSlot=reader.ReadInt32(),Signature=reader.ReadUInt64()};
                if(hit.Kind==1){hit.ParentKind=reader.ReadInt32();hit.ParentSlot=reader.ReadInt32();hit.ParentType=reader.ReadInt32();hit.ParentGeneration=reader.ReadInt32();hit.ParentKey=reader.ReadUInt32();hit.ParentOwner=reader.ReadInt32();hit.ParentEpoch=reader.ReadInt32();hit.Origin=reader.ReadUInt64();}
                if(hit.Kind>1 || hit.Tick<=capture || hit.Tick<prior || hit.Tick-capture>PredictionWire.MaximumAlignmentAge || hit.Tick-capture>=frames || hit.SourceEpoch<-1 || hit.SourceEpoch>i
                    || (hit.Kind==0?(hit.SourceEpoch>=i || hit.TargetSlot<0 || hit.TargetSlot>=Main.maxNPCs || hit.SharedSlot<-1 || hit.SharedSlot>=Main.maxNPCs):(!hit.Known || hit.TargetSlot!=-1 || hit.SharedSlot!=-1 || hit.SourceEpoch!=i || hit.Origin==0 || hit.ParentEpoch<-1 || hit.ParentEpoch>=i || hit.ParentSlot<0 || hit.ParentType<=0
                        || (hit.ParentKind==1?hit.ParentSlot>=Main.maxNPCs || hit.ParentType>=Terraria.ID.NPCID.Count || hit.ParentEpoch!=-1:hit.ParentKind!=2 || hit.ParentSlot>=Main.maxProjectiles || hit.ParentType>=Terraria.ID.ProjectileID.Count || hit.ParentOwner<0 || hit.ParentOwner>Main.maxPlayers)))
                    || (hit.Known?(hit.SourceSlot<0 || hit.SourceSlot>=Main.maxProjectiles || hit.SourceType<=0 || hit.SourceType>=Terraria.ID.ProjectileID.Count || hit.SourceOwner<0 || hit.SourceOwner>Main.maxPlayers):hit.SourceSlot!=-1))
                    throw new InvalidDataException("NPC impact proof identity or time.");
                if(hit.Kind==0 && hit.SourceEpoch>=0 && !Epoch(hits[hit.SourceEpoch],hit.SourceSlot,hit.SourceType,hit.SourceOwner,hit.SourceKey,hit.SourceGeneration)
                    || hit.Kind==1 && hit.ParentEpoch>=0 && !Epoch(hits[hit.ParentEpoch],hit.ParentSlot,hit.ParentType,hit.ParentOwner,hit.ParentKey,hit.ParentGeneration)
                    || hit.Kind==1 && ((int)(hit.SourceKey>>8&0x3ff)!=hit.SourceSlot || (int)(hit.SourceKey>>18)!= (hit.SourceGeneration&0x3fff)))
                    throw new InvalidDataException("Projectile lifetime epoch.");
                hits[i]=hit;prior=hit.Tick;
            }
            return hits;
        }
        private static bool Epoch(Hit birth,int slot,int type,int owner,uint key,int generation)
        {return birth.Kind==1 && birth.SourceSlot==slot && birth.SourceType==type && birth.SourceOwner==owner && birth.SourceKey==key && birth.SourceGeneration==generation;}
    }
}
