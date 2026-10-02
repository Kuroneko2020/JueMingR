using System;
using System.Collections.Generic;
using System.IO;
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
            internal long Tick;
            internal int SourceSlot,SourceType,SourceOwner,TargetSlot,SharedSlot;
            internal uint SourceKey;
            internal ulong Signature;
            internal Projectile Source;
            internal bool Known;
        }
        private struct SourceIdentity
        {
            internal Projectile Token;
            internal int Slot,Type,Owner;
            internal uint Key;
            internal bool Active;
            internal bool Matches(Hit hit)
            {return Active && hit.Known && ReferenceEquals(Token,hit.Source) && Slot==hit.SourceSlot && Type==hit.SourceType && Owner==hit.SourceOwner && Key==hit.SourceKey;}
        }
        private readonly SourceIdentity[] sources;
        private readonly List<Hit> hits=new List<Hit>();
        internal NativeImpactProof(int[] projectiles)
        {
            sources=new SourceIdentity[projectiles.Length];
            for(int i=0;i<sources.Length;i++)
            {int slot=projectiles[i];var p=Main.projectile[slot];sources[i]=new SourceIdentity{Token=p,Slot=slot,Type=p.type,Owner=p.owner,Key=(uint)p.key,Active=p.active};}
        }
        internal bool Owns(Projectile value)
        {foreach(var source in sources)if(source.Active && ReferenceEquals(source.Token,value))return true;return false;}
        internal bool Record(Hit hit,long capture)
        {
            if(hit.Tick<=capture || hit.Tick-capture>PredictionWire.MaximumAlignmentAge || hits.Count>=MaximumHits)return false;
            foreach(var source in sources)if(source.Matches(hit)){hit.Source=null;hits.Add(hit);return true;}
            return false;
        }
        internal string Difference(Hit[] expected,long tick,int[] npcs)
        {
            int index=0;
            foreach(var hit in expected)
            {
                if(hit.Tick>tick)break;
                if(!Touches(npcs,hit))continue;
                if(index>=hits.Count || !Same(hit,hits[index++]))return "NPC impact history";
            }
            return index==hits.Count?null:"extra NPC impact";
        }
        private static bool Same(Hit a,Hit b)
        {return a.Tick==b.Tick && a.Known==b.Known && a.SourceSlot==b.SourceSlot && a.SourceType==b.SourceType && a.SourceOwner==b.SourceOwner && a.SourceKey==b.SourceKey && a.TargetSlot==b.TargetSlot && a.SharedSlot==b.SharedSlot && a.Signature==b.Signature;}
        internal static bool Touches(int[] npcs,Hit hit)
        {return Array.IndexOf(npcs,hit.TargetSlot)>=0 || hit.SharedSlot>=0 && Array.IndexOf(npcs,hit.SharedSlot)>=0;}
        internal static void Write(BinaryWriter writer,List<Hit> hits)
        {
            writer.Write(hits.Count);
            foreach(var hit in hits)
            {writer.Write(hit.Tick);writer.Write(hit.Known);writer.Write(hit.SourceSlot);writer.Write(hit.SourceType);writer.Write(hit.SourceOwner);writer.Write(hit.SourceKey);writer.Write(hit.TargetSlot);writer.Write(hit.SharedSlot);writer.Write(hit.Signature);}
        }
        internal static Hit[] Read(BinaryReader reader,long capture,int frames)
        {
            int count=reader.ReadInt32();if(count<0 || count>MaximumHits)throw new InvalidDataException("NPC impact proof capacity.");
            var hits=new Hit[count];long prior=capture;
            for(int i=0;i<count;i++)
            {
                var hit=new Hit{Tick=reader.ReadInt64(),Known=reader.ReadBoolean(),SourceSlot=reader.ReadInt32(),SourceType=reader.ReadInt32(),SourceOwner=reader.ReadInt32(),SourceKey=reader.ReadUInt32(),TargetSlot=reader.ReadInt32(),SharedSlot=reader.ReadInt32(),Signature=reader.ReadUInt64()};
                if(hit.Tick<=capture || hit.Tick<prior || hit.Tick-capture>PredictionWire.MaximumAlignmentAge || hit.Tick-capture>=frames || hit.TargetSlot<0 || hit.TargetSlot>=Main.maxNPCs || hit.SharedSlot<-1 || hit.SharedSlot>=Main.maxNPCs
                    || (hit.Known?(hit.SourceSlot<0 || hit.SourceSlot>=Main.maxProjectiles || hit.SourceType<=0 || hit.SourceType>=Terraria.ID.ProjectileID.Count || hit.SourceOwner<0 || hit.SourceOwner>Main.maxPlayers):hit.SourceSlot!=-1))
                    throw new InvalidDataException("NPC impact proof identity or time.");
                hits[i]=hit;prior=hit.Tick;
            }
            return hits;
        }
    }
}
