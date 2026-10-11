using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        private IceReadSet icePathReads;
        private HashSet<int> icePlayerGuard;
        // This opt-in read set spans resets/restarts in one NPC preparation.
        // A final cell cache alone omits queries made before rolling.Reset.
        // Only Ice Rod needs this proof; other weapons allocate no extra set.
        internal sealed class IceReadSet
        {
            private const int Maximum=8192;
            private readonly int width=Main.maxTilesX,height=Main.maxTilesY;
            private readonly HashSet<int> keys=new HashSet<int>();
            private bool complete=true;
            internal void Record(int x,int y)
            {if(width!=Main.maxTilesX || height!=Main.maxTilesY || keys.Count>=Maximum && !keys.Contains(y*width+x)){complete=false;return;}keys.Add(y*width+x);}
            internal void Include(IceReadSet prior)
            {
                if(prior==null || !prior.complete || width!=prior.width || height!=prior.height){complete=false;return;}
                foreach(int key in prior.keys)Record(key%width,key/width);
            }
            internal bool Unaffected(IEnumerable<Point> changed)
            {
                if(!complete || changed==null || width<=0 || height<=0 || width!=Main.maxTilesX || height!=Main.maxTilesY)return false;
                int count=0;foreach(var cell in changed)
                    if(++count>64 || cell.X<0 || cell.Y<0 || cell.X>=width || cell.Y>=height || keys.Contains(cell.Y*width+cell.X))return false;
                return true;
            }
        }
        internal void BeginIcePathReadCapture()
        {if(icePathReads!=null)throw new InvalidOperationException("Ice path read capture cannot nest.");icePathReads=new IceReadSet();}
        internal IceReadSet EndIcePathReadCapture()
        {var value=icePathReads;icePathReads=null;return value;}
        internal bool IncludeIcePathReads(IceReadSet prior)
        {if(icePathReads==null)return true;if(prior==null)return false;icePathReads.Include(prior);return true;}
        internal bool BeginIcePlayerReadGuard(IEnumerable<Point> changedCells)
        {
            if(icePlayerGuard!=null || changedCells==null)return false;
            var keys=new HashSet<int>();int count=0;
            foreach(var cell in changedCells)
            {if(++count>64 || cell.X<0 || cell.Y<0 || cell.X>=Main.maxTilesX || cell.Y>=Main.maxTilesY)return false;keys.Add(cell.Y*Main.maxTilesX+cell.X);}
            icePlayerGuard=keys;return true;
        }
        internal void EndIcePlayerReadGuard(){icePlayerGuard=null;}

        // AI22 scans its actual envelope but tests a fixed integer 8x8 core
        // at position + 4. Its failed-placement tail uses raw active tiles;
        // normal flight uses nactive. Both ignore ice blocks and platforms.
        internal bool IceCoreClear(float x,float y,int bodyWidth,int bodyHeight,bool rawActive,out bool clear)
        {
            clear=false;if(float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))return false;
            int left=Math.Max(0,(int)(x/16f)-1),right=Math.Min(Terraria.Main.maxTilesX,(int)((x+bodyWidth)/16f)+2);
            int top=Math.Max(0,(int)(y/16f)-1),bottom=Math.Min(Terraria.Main.maxTilesY,(int)((y+bodyHeight)/16f)+2);
            int coreX=(int)x+4,coreY=(int)y+4;
            for(int tx=left;tx<right;tx++)for(int ty=top;ty<bottom;ty++)
            {
                Cell cell;PredictionStop stop;if(!CellAt(tx,ty,out cell,out stop))return false;
                if(!(rawActive?cell.RawActive:cell.Active) || cell.Type==127 || !cell.RawSolid || cell.RawPlatform)continue;
                if(coreX+8>tx*16 && coreX<tx*16+16 && coreY+8>ty*16 && coreY<ty*16+16)return true;
            }
            clear=true;return true;
        }
    }
}
