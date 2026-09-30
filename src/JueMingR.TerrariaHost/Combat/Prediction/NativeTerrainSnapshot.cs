using System;
using System.Collections.Generic;
using System.IO;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Immutable, value-only chunks. Capture belongs to the game thread;
    // encoding/cache comparison may run on the transport owner afterwards.
    // Unknown cells have no chunk and must never acquire an implicit air value.
    internal sealed class NativeTerrainSnapshot
    {
        internal const int ChunkSize=32,MaximumChunks=128,MaximumWidth=8401,MaximumHeight=2401;
        internal sealed class Chunk
        {
            internal readonly int X,Y;
            internal readonly long Version;
            internal readonly byte[] Values;
            internal Chunk(int x,int y,byte[] values){X=x;Y=y;Values=values;Version=Fingerprint(values);}
        }
        internal readonly long World;
        internal readonly int Width,Height;
        internal readonly Chunk[] Chunks;
        // A proof cache for ONE synchronous game-thread Prepare, never for a
        // tick number shared with arbitrary callers. Only successful complete
        // comparisons enter it; failure cannot certify another snapshot.
        internal sealed class Comparison
        {
            internal readonly Dictionary<int,Chunk> Matched=new Dictionary<int,Chunk>();
            internal long World,TilesRead,ChunkHits,TilesCaptured;internal int Width,Height;
            internal void Clear(){Matched.Clear();World=TilesRead=ChunkHits=TilesCaptured=0;Width=Height=0;}
        }
        private NativeTerrainSnapshot(long world,int width,int height,Chunk[] chunks){World=world;Width=width;Height=height;Chunks=chunks;}
        internal static NativeTerrainSnapshot Capture(long world,int left,int top,int right,int bottom)
        {
            var keys=new SortedSet<int>();AddRegion(keys,left,top,right,bottom);return CaptureChunks(world,keys);
        }
        internal static void AddRegion(SortedSet<int> keys,int left,int top,int right,int bottom)
        {
            int width=Main.maxTilesX,height=Main.maxTilesY;
            if(left<0 || top<0 || right<left || bottom<top || right>=width || bottom>=height)throw new InvalidDataException("Invalid local terrain rectangle.");
            int x0=left/ChunkSize,y0=top/ChunkSize,x1=right/ChunkSize,y1=bottom/ChunkSize;
            for(int cx=x0;cx<=x1;cx++)for(int cy=y0;cy<=y1;cy++)
            {keys.Add(cx*128+cy);if(keys.Count>MaximumChunks)throw new InvalidDataException("Local terrain capacity exceeded.");}
        }
        internal static NativeTerrainSnapshot CaptureChunks(long world,SortedSet<int> keys)
        {return CaptureChunksObserved(world,keys,null);}
        internal static NativeTerrainSnapshot CaptureChunksObserved(long world,SortedSet<int> keys,Comparison pass)
        {
            int width=Main.maxTilesX,height=Main.maxTilesY;ValidateExtent(world,width,height);
            if(keys==null || keys.Count<1 || keys.Count>MaximumChunks)throw new InvalidDataException("Local terrain capacity exceeded.");
            var chunks=new Chunk[keys.Count];int index=0;
            foreach(int key in keys)
            {
                int cx=key/128,cy=key%128;
                if(cx<0 || cy<0 || cx>(width-1)/32 || cy>(height-1)/32)throw new InvalidDataException("Local terrain chunk coordinates.");
                // Region growth does not invalidate this call's completed
                // live comparisons. Reuse those immutable bytes; only new or
                // unverified chunks need another tile read/allocation/hash.
                Chunk verified;
                if(pass!=null && pass.World==world && pass.Width==width && pass.Height==height && pass.Matched.TryGetValue(key,out verified)){chunks[index++]=verified;continue;}
                var data=new byte[Math.Min(32,width-cx*32)*Math.Min(32,height-cy*32)*14];int at=0;
                for(int x=cx*ChunkSize;x<Math.Min(width,(cx+1)*ChunkSize);x++)
                for(int y=cy*ChunkSize;y<Math.Min(height,(cy+1)*ChunkSize);y++,at+=14)
                {
                    Tile tile=Main.tile[x,y];if(tile==null)throw new InvalidDataException("Unobserved live terrain.");
                    Put(data,at,tile.type);Put(data,at+2,tile.wall);data[at+4]=tile.liquid;Put(data,at+5,tile.sTileHeader);
                    data[at+7]=tile.bTileHeader;data[at+8]=tile.bTileHeader2;data[at+9]=tile.bTileHeader3;Put(data,at+10,unchecked((ushort)tile.frameX));Put(data,at+12,unchecked((ushort)tile.frameY));
                }
                chunks[index++]=new Chunk(cx,cy,data);
                if(pass!=null && PredictionPipeProtocol.Measure)pass.TilesCaptured+=data.Length/14;
            }
            return new NativeTerrainSnapshot(world,width,height,chunks);
        }
        private static void Put(byte[] values,int at,ushort value){values[at]=(byte)value;values[at+1]=(byte)(value>>8);}
        internal void Write(BinaryWriter writer,bool referencesOnly)
        {
            writer.Write(World);writer.Write(Chunks.Length);
            foreach(var chunk in Chunks)
            {
                writer.Write(chunk.X);writer.Write(chunk.Y);writer.Write(chunk.Version);writer.Write(!referencesOnly);
                if(!referencesOnly)writer.Write(chunk.Values);
            }
        }
        internal bool IsCurrent(long world)
        {return IsCurrentObserved(world,null);}
        internal bool IsCurrentObserved(long world,Comparison pass)
        {
            if(World!=world || Main.maxTilesX!=Width || Main.maxTilesY!=Height)return false;
            if(pass!=null && (pass.World!=world || pass.Width!=Width || pass.Height!=Height)){pass.Clear();pass.World=world;pass.Width=Width;pass.Height=Height;}
            foreach(var chunk in Chunks)
            {
                int key=chunk.X*128+chunk.Y;Chunk matched;
                if(pass!=null && pass.Matched.TryGetValue(key,out matched))
                {
                    if(matched.Version!=chunk.Version || !SameBytes(matched.Values,chunk.Values))return false;
                    if(PredictionPipeProtocol.Measure)pass.ChunkHits++;continue;
                }
                byte[] b=chunk.Values;int at=0;
                for(int x=chunk.X*32;x<Math.Min(Width,chunk.X*32+32);x++)for(int y=chunk.Y*32;y<Math.Min(Height,chunk.Y*32+32);y++,at+=14)
                {
                    Tile t=Main.tile[x,y];if(t==null || t.type!=U16(b,at) || t.wall!=U16(b,at+2) || t.liquid!=b[at+4] || t.sTileHeader!=U16(b,at+5) || t.bTileHeader!=b[at+7] || t.bTileHeader2!=b[at+8] || t.bTileHeader3!=b[at+9] || t.frameX!=(short)U16(b,at+10) || t.frameY!=(short)U16(b,at+12))
                    {if(pass!=null && PredictionPipeProtocol.Measure)pass.TilesRead+=at/14+1;return false;}
                }
                if(pass!=null){pass.Matched.Add(key,chunk);if(PredictionPipeProtocol.Measure)pass.TilesRead+=at/14;}
            }
            return true;
        }
        private static bool SameBytes(byte[] a,byte[] b)
        {if(ReferenceEquals(a,b))return true;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private static ushort U16(byte[] value,int offset){return (ushort)(value[offset]|value[offset+1]<<8);}
        internal static long Fingerprint(byte[] values)
        {
            unchecked{ulong hash=14695981039346656037UL;foreach(byte value in values){hash^=value;hash*=1099511628211UL;}return (long)hash;}
        }
        internal static void ValidateExtent(long world,int width,int height)
        {if(world<=0 || width<20 || width>MaximumWidth || height<20 || height>MaximumHeight)throw new InvalidDataException("Invalid native world extent.");}
    }

    // The worker retains immutable baseline bytes, never mutated Tile objects.
    // Every forecast restores exactly its declared chunks. Prior request tiles
    // outside that set become unknown, including after a failed simulation.
    internal sealed class NativeTerrainStore
    {
        private long world;
        private int width,height;
        private Dictionary<int,NativeTerrainSnapshot.Chunk> chunks=new Dictionary<int,NativeTerrainSnapshot.Chunk>();
        internal void Clear()
        {world=0;width=height=0;chunks.Clear();Main.tile=new Tile[120,120];}
        internal void Read(BinaryReader reader,int requestedWidth,int requestedHeight)
        {
            long requestedWorld=reader.ReadInt64();NativeTerrainSnapshot.ValidateExtent(requestedWorld,requestedWidth,requestedHeight);
            int count=reader.ReadInt32();if(count<1 || count>NativeTerrainSnapshot.MaximumChunks)throw new InvalidDataException("Terrain chunk count.");
            bool same=world==requestedWorld && width==requestedWidth && height==requestedHeight;
            var incoming=new Dictionary<int,NativeTerrainSnapshot.Chunk>();
            for(int i=0;i<count;i++)
            {
                int cx=reader.ReadInt32(),cy=reader.ReadInt32();long version=reader.ReadInt64();bool hasValues=reader.ReadBoolean();
                if(cx<0 || cy<0 || cx>(requestedWidth-1)/32 || cy>(requestedHeight-1)/32)throw new InvalidDataException("Terrain chunk coordinates.");
                int key=cx*128+cy;if(incoming.ContainsKey(key))throw new InvalidDataException("Duplicate terrain chunk.");
                NativeTerrainSnapshot.Chunk chunk;
                if(hasValues)
                {
                    int size=Math.Min(32,requestedWidth-cx*32)*Math.Min(32,requestedHeight-cy*32)*14;
                    byte[] data=reader.ReadBytes(size);if(data.Length!=size)throw new EndOfStreamException("Truncated terrain chunk.");
                    chunk=new NativeTerrainSnapshot.Chunk(cx,cy,data);if(chunk.Version!=version)throw new InvalidDataException("Terrain chunk fingerprint mismatch.");
                }
                else if(!same || !chunks.TryGetValue(key,out chunk) || chunk.Version!=version)throw new InvalidDataException("Terrain cache reference is unavailable.");
                incoming.Add(key,chunk);
            }
            if(!same || Main.tile==null || Main.tile.GetLength(0)!=requestedWidth || Main.tile.GetLength(1)!=requestedHeight)
                Main.tile=new Tile[requestedWidth,requestedHeight];
            else foreach(var previous in chunks.Values)if(!incoming.ContainsKey(previous.X*128+previous.Y))
                for(int x=previous.X*32;x<Math.Min(width,(previous.X+1)*32);x++)for(int y=previous.Y*32;y<Math.Min(height,(previous.Y+1)*32);y++)Main.tile[x,y]=null;
            world=requestedWorld;width=requestedWidth;height=requestedHeight;chunks=incoming;
            foreach(var chunk in chunks.Values)using(var input=new MemoryStream(chunk.Values,false))using(var values=new BinaryReader(input))
                for(int x=chunk.X*32;x<Math.Min(width,(chunk.X+1)*32);x++)for(int y=chunk.Y*32;y<Math.Min(height,(chunk.Y+1)*32);y++)
                {
                    // Simulation may modify every one of Tile's nine fields.
                    // Reuse storage only; always restore the complete baseline
                    // even when this request references unchanged chunk bytes.
                    var tile=Main.tile[x,y]??new Tile();
                    tile.type=values.ReadUInt16();tile.wall=values.ReadUInt16();tile.liquid=values.ReadByte();tile.sTileHeader=values.ReadUInt16();
                    tile.bTileHeader=values.ReadByte();tile.bTileHeader2=values.ReadByte();tile.bTileHeader3=values.ReadByte();tile.frameX=values.ReadInt16();tile.frameY=values.ReadInt16();
                    if(tile.type>=Main.tileSolid.Length)throw new InvalidDataException("Unknown terrain type.");Main.tile[x,y]=tile;
                }
        }
    }
}
