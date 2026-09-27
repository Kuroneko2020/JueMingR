using JueMingR.TerrariaHost.World;
using Terraria;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Tools
{
    // Cache ready candidates, not every immature plant. Consumption still
    // checks live eligibility; missing world notifications have a 19-update
    // discovery bound. Environment/capability changes invalidate immediately.
    internal sealed class HerbField
    {
        private readonly bool[] plants=new bool[441];
        private int x,y,cursor;
        private bool initialized;
        private Rectangle reach;
        private object tiles,sections;
        private int toolType,boost,environment,height,width;
        private double surface;
        private bool blocked;
        internal int Count {get;private set;}
        private long frame=-1;
#if DEBUG
        internal long TileReads {get;private set;}
#endif
        internal void Observe(Player p,Item tool,long update)
        {
            int cx=(int)(p.Center.X/16)-10,cy=(int)(p.Center.Y/16)-10;
            Rectangle current=TileReachCheckSettings.Simple.GetTileRegion(p,tool.tileBoost+p.blockRange);
            int env=(Main.dayTime?1:0)|(Main.bloodMoon?2:0)|(Main.moonPhase==0?4:0)|(Main.raining?8:0)|(Main.cloudAlpha>0?16:0)|(Main.time>40500?32:0)|(Main.remixWorld?64:0);
            bool changed=!initialized || cx!=x || cy!=y || reach!=current || toolType!=tool.type || boost!=tool.tileBoost+p.blockRange || blocked!=p.noBuilding || environment!=env ||
                surface!=Main.worldSurface || height!=Main.maxTilesY || width!=Main.maxTilesX || !ReferenceEquals(tiles,Main.tile) || !ReferenceEquals(sections,Main.sectionManager);
            if(!changed && frame==update)return;frame=update;
            if(changed){initialized=true;x=cx;y=cy;cursor=0;reach=current;toolType=tool.type;boost=tool.tileBoost+p.blockRange;blocked=p.noBuilding;environment=env;
                surface=Main.worldSurface;height=Main.maxTilesY;width=Main.maxTilesX;tiles=Main.tile;sections=Main.sectionManager;}
            int budget=changed?plants.Length:24;
            for(int n=0;n<budget;n++)
            {
                int i=cursor;cursor=(cursor+1)%plants.Length;
                var t=WorldTileObservation.ReadCurrent(x+i/21,y+i%21);
#if DEBUG
                TileReads++;
#endif
                bool found=t.Readable && t.Active && (t.Type==83 || t.Type==84) && HerbHarvest.Harvestable(p,tool,x+i/21,y+i%21,t.FrameX/18);
                if(found!=plants[i])Count+=found?1:-1;plants[i]=found;
            }
        }
        internal bool TryPoint(int index,out int px,out int py)
        {px=x+index/21;py=y+index%21;return initialized && plants[index];}
        internal void Reject(int px,int py)
        {int dx=px-x,dy=py-y;if(dx<0 || dx>=21 || dy<0 || dy>=21)return;int i=dx*21+dy;if(plants[i]){plants[i]=false;Count--;}}
        internal void Clear(){initialized=false;frame=-1;Count=0;tiles=sections=null;System.Array.Clear(plants,0,plants.Length);}
    }
}
