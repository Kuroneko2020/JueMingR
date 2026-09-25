using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ObjectData;
using JueMingR.TerrariaHost.World;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class FurnitureRecovery
    {
        private static readonly int[] Types={125,287,354,377,464,621,699},Buffs={29,93,150,159,348,192,366};
        private static readonly int[] Widths={2,2,3,3,5,2,4},Heights={2,2,3,2,4,2,4};
        private readonly HostRecovery host;
        private readonly WorldTileObservation tiles;
        private readonly Action<Player,int,int> use;
        private readonly Candidate[] failed=new Candidate[7];
        private readonly Candidate[] nearest=new Candidate[7];
        private Rectangle region;
        private long revision=-1;
        private ulong nextScan;
        internal bool Executing {get;private set;}
#if DEBUG
        internal long TileReads,NativeCalls,ValidationReads;
#endif
        private struct Candidate{internal Tile Tile;internal int X,Y,Left,Top,FrameX,FrameY;internal float Distance;}
        internal FurnitureRecovery(HostRecovery host,WorldTileObservation tiles)
        {this.host=host;this.tiles=tiles;use=(Action<Player,int,int>)Delegate.CreateDelegate(typeof(Action<Player,int,int>),typeof(Player).GetMethod("TileInteractionsUse",BindingFlags.Instance|BindingFlags.NonPublic));}
        internal void Reset(){Array.Clear(failed,0,7);nextScan=0;revision=-1;}
        internal void Update(Player p,ulong tick)
        {
            if(tiles==null || !p.releaseUseTile || p.controlUseTile || p.tileInteractionHappened || WiresUI.Open || p.ownedProjectileCounts[651]>0 || Main.HasInteractableObjectThatIsNotATile)return;
            bool need=false;for(int i=0;i<7;i++)if(BuffRecovery.Missing(p,Buffs[i])){need=true;break;}if(!need)return;
            Rectangle current=TileReachCheckSettings.Simple.GetTileRegion(p);
            if(region!=current || revision!=host.Services.Revision){region=current;revision=host.Services.Revision;nextScan=0;Array.Clear(failed,0,7);}
            if(tick<nextScan)return;nextScan=tick+30;Array.Clear(nearest,0,7);
            // Discovery is finite and shared. Every write below re-reads the
            // complete object, bypassing this observation cache.
            int left=Math.Max(0,current.Left),top=Math.Max(0,current.Top),right=Math.Min(Main.maxTilesX,current.Right),bottom=Math.Min(Main.maxTilesY,current.Bottom);
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++)
            {
#if DEBUG
                TileReads++;
#endif
                var observed=tiles.Read(x,y);if(!observed.Readable || !observed.Active || observed.Inactive)continue;
                int kind=Array.IndexOf(Types,(int)observed.Type);if(kind<0 || !BuffRecovery.Missing(p,Buffs[kind]))continue;
                Candidate c;if(!Read(x,y,kind,out c))continue;c.Distance=Vector2.DistanceSquared(p.Center,new Vector2(x*16+8,y*16+8));
                if(failed[kind].Tile!=null && Same(c,failed[kind]))continue;
                if(nearest[kind].Tile==null || c.Distance<nearest[kind].Distance)nearest[kind]=c;
            }
            for(int kind=0;kind<7;kind++)if(nearest[kind].Tile!=null)
            {Apply(p,kind,nearest[kind]);nextScan=tick+1;break;}
        }
        private static bool Same(Candidate a,Candidate b){return ReferenceEquals(a.Tile,b.Tile) && a.Left==b.Left && a.Top==b.Top && a.FrameX==b.FrameX && a.FrameY==b.FrameY;}
        private bool Read(int x,int y,int kind,out Candidate result)
        {
            result=default(Candidate);var t=Current(x,y);
            if(!t.Readable || !t.Active || t.Inactive || t.Type!=Types[kind] || t.FrameX<0 || t.FrameY<0)return false;
            Tile tile=Main.tile[x,y];TileObjectData data=TileObjectData.GetTileData(tile);
            if(data==null || data.Width!=Widths[kind] || data.Height!=Heights[kind] || data.CoordinateFullWidth<=0 || data.CoordinateFullHeight<=0)return false;
            int stride=data.CoordinateWidth+data.CoordinatePadding,dx=t.FrameX%data.CoordinateFullWidth,dy=t.FrameY%data.CoordinateFullHeight;
            if(stride<=0 || dx%stride!=0 || dx/stride>=data.Width)return false;
            int row=0,offset=0;while(row<data.Height && offset<dy){offset+=data.CoordinateHeights[row]+data.CoordinatePadding;row++;}
            if(row>=data.Height || offset!=dy)return false;
            int left=x-dx/stride,top=y-row,baseX=t.FrameX-dx,baseY=t.FrameY-dy;offset=0;
            for(int yy=0;yy<data.Height;yy++)
            {
                for(int xx=0;xx<data.Width;xx++){var part=Current(left+xx,top+yy);if(!part.Readable || !part.Active || part.Inactive || part.Type!=t.Type || part.FrameX!=baseX+xx*stride || part.FrameY!=baseY+offset)return false;}
                offset+=data.CoordinateHeights[yy]+data.CoordinatePadding;
            }
            result=new Candidate{Tile=Main.tile[left,top],X=x,Y=y,Left=left,Top=top,FrameX=baseX,FrameY=baseY};return true;
        }
        private JueMingR.Platform.WorldTargets.WorldTargetTile Current(int x,int y)
        {
#if DEBUG
            ValidationReads++;
#endif
            return WorldTileObservation.ReadCurrent(x,y);
        }
        private void Apply(Player p,int kind,Candidate candidate)
        {
            Candidate fresh;if(!host.Admit(p) || host.Value(3)==0 || !BuffRecovery.Missing(p,Buffs[kind]) || !p.IsInTileInteractionRange(candidate.X,candidate.Y,TileReachCheckSettings.Simple) || !Read(candidate.X,candidate.Y,kind,out fresh) || !Same(candidate,fresh))return;
            bool attempted=p.tileInteractAttempted,happened=p.tileInteractionHappened;Executing=true;
            try
            {
                p.tileInteractAttempted=true;
#if DEBUG
                NativeCalls++;
#endif
                use(p,candidate.X,candidate.Y);
                if(BuffRecovery.Missing(p,Buffs[kind]))failed[kind]=candidate;
            }
            catch{failed[kind]=candidate;host.Report("家具交互未完成；已停止重复点击该对象。");}
            finally{Executing=false;p.tileInteractAttempted=attempted;p.tileInteractionHappened=happened;}
        }
    }
}
