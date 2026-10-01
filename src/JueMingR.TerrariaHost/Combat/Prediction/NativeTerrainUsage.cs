using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // One request's bounded cell facts, independent of exact snapshot bytes.
    // Reads of air count too. At most 128 observed chunks, 128 bytes per chunk;
    // repeated native reads set a bit, never append per-step/field records.
    internal sealed class NativeTerrainUsage
    {
        internal readonly Dictionary<int,byte[]> Cells=new Dictionary<int,byte[]>();
        private int lastKey=-1;private byte[] lastBits;
        internal void Add(int x,int y)
        {
            int key=x/32*128+y/32;byte[] bits=lastBits;
            if(key!=lastKey)
            {
                if(!Cells.TryGetValue(key,out bits))
                {if(Cells.Count>=NativeTerrainSnapshot.MaximumChunks)throw new InvalidDataException("Terrain usage capacity.");Cells.Add(key,bits=new byte[128]);}
                lastKey=key;lastBits=bits;
            }
            int at=x%32*32+y%32;bits[at/8]|=(byte)(1<<(at%8));
        }
        internal bool Contains(int key,int x,int y)
        {byte[] bits;int at=x%32*32+y%32;return Cells.TryGetValue(key,out bits) && (bits[at/8]&(1<<(at%8)))!=0;}
        internal bool Intersects(NativeTerrainUsage other)
        {
            foreach(var pair in Cells){byte[] bits;if(other.Cells.TryGetValue(pair.Key,out bits))for(int i=0;i<128;i++)if((pair.Value[i]&bits[i])!=0)return true;}
            return false;
        }
        internal void Write(BinaryWriter writer)
        {writer.Write(Cells.Count);var keys=new List<int>(Cells.Keys);keys.Sort();foreach(int key in keys){writer.Write(key);writer.Write(Cells[key]);}}
        internal static NativeTerrainUsage Read(BinaryReader reader)
        {
            int count=reader.ReadInt32(),prior=-1;if(count<0 || count>NativeTerrainSnapshot.MaximumChunks)throw new InvalidDataException("Terrain usage extent.");
            var value=new NativeTerrainUsage();
            for(int i=0;i<count;i++)
            {
                int key=reader.ReadInt32();if(key<=prior || key/128>NativeTerrainSnapshot.MaximumWidth/32 || key%128>NativeTerrainSnapshot.MaximumHeight/32)throw new InvalidDataException("Terrain usage order.");prior=key;
                byte[] bits=reader.ReadBytes(128);if(bits.Length!=128)throw new EndOfStreamException("Terrain usage truncated.");
                bool any=false;foreach(byte b in bits)any|=b!=0;if(!any)throw new InvalidDataException("Empty terrain usage page.");value.Cells.Add(key,bits);
            }
            return value;
        }
        internal void Validate(NativeTerrainSnapshot snapshot)
        {
            foreach(var pair in Cells)
            {
                bool supplied=false;foreach(var chunk in snapshot.Chunks)if(chunk.X*128+chunk.Y==pair.Key){supplied=true;break;}
                if(!supplied)throw new InvalidDataException("Terrain usage outside captured pages.");
                int cx=pair.Key/128,cy=pair.Key%128;
                for(int x=0;x<32;x++)for(int y=0;y<32;y++)if((cx*32+x>=snapshot.Width || cy*32+y>=snapshot.Height) && Contains(pair.Key,x,y))throw new InvalidDataException("Terrain usage outside captured extent.");
            }
        }
    }
}
