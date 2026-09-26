using System;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.TerrariaHost.World;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class HerbHarvest
    {
        internal struct HarvestReceipt {internal bool Valid;internal int X,Y,Style;internal long Token;}
        private readonly HostTools host;
        internal readonly ReplantQueue Pending=new ReplantQueue();
        private static readonly int[] seeds={307,308,309,310,311,312,2357};
        private long nextProbe;
        private bool yieldHarvest;
#if DEBUG
        internal long Probes {get;private set;}
#endif
        private readonly long[] unknownPlots=new long[50];
        private int unknownCount;
        private static long Plot(int x,int y){return ((long)x<<32)|(uint)y;}
        private bool Unknown(int x,int y){for(int i=0;i<unknownCount;i++)if(unknownPlots[i]==Plot(x,y))return true;return unknownCount==unknownPlots.Length;}
        private void HoldUnknown(int x,int y){if(!Unknown(x,y))unknownPlots[unknownCount++]=Plot(x,y);}
        internal void ClearUnknown(){unknownCount=0;}
        internal HerbHarvest(HostTools host){this.host=host;}
        internal static bool IsTool(Item item){return item!=null && item.stack>0 && (item.type==213 || item.type==5295);}
        private static bool Support(int x,int y)
        {var t=WorldTileObservation.ReadCurrent(x,y+1);return t.Readable && t.Active && !t.Inactive && (t.Type==78 || t.Type==380);}
        internal static bool Harvestable(Player p,Item tool,int x,int y,int style)
        {
            var t=WorldTileObservation.ReadCurrent(x,y);
            return !p.noBuilding && IsTool(tool) && t.Readable && t.Active && !t.Inactive && (t.Type==83 || t.Type==84) && t.FrameX/18==style && style>=0 && style<=6 &&
                Support(x,y) && p.IsInTileInteractionRange(x,y,TileReachCheckSettings.Simple,tool.tileBoost+p.blockRange) && WorldGen.IsHarvestableHerbWithSeed(t.Type,style,y);
        }
        internal bool ProtectSeed(Item item)
        {
            if(!host.KeepsIntent(1) || item==null || item.stack<=0)return false;
            int needed=0;for(int i=0;i<Pending.Count;i++)if(item.type==seeds[Pending[i].Style])needed++;
            var player=host.Player;if(needed==0 || player==null)return false;
            // Existing item guards operate on exact sources, not fractions of
            // a stack. Reserve only enough first real stacks for live plots.
            for(int i=0;i<50 && needed>0;i++){var source=player.inventory[i];if(source.type!=item.type || source.stack<=0)continue;if(ReferenceEquals(source,item))return true;needed-=source.stack;}return false;
        }
        internal void Update()
        {
            if(!host.KeepsIntent(1) || host.Player==null || host.Player.dead){Pending.Clear();return;}
            Pending.Expire(host.Tick);
            for(int i=Pending.Count-1;i>=0;i--)
            {
                var p=Pending[i];var t=WorldTileObservation.ReadCurrent(p.X,p.Y);
                var bottom=WorldTileObservation.ReadCurrent(p.X,p.Y+1);
                // Unloaded or actuated support is temporary; only a confirmed
                // replacement retires the plot before its fixed deadline.
                if(t.Readable && t.Active || bottom.Readable && (!bottom.Active || bottom.Type!=78 && bottom.Type!=380))Pending.RemoveAt(i);
            }
        }
        internal ToolIntent Choose(Player p)
        {
            if(host.Mode(1)==0 || !host.Admit(p,false) || host.Input.Frame<nextProbe)return null;
            // Only capture owns the explicit rod-borrow/recast contract.
            // Harvest and seed uses must not silently end an ordinary cast.
            if(p.HeldItem.fishingPole>0 && FishingBorrow.HasBobber(p))return null;
            nextProbe=host.Input.Frame+4;
#if DEBUG
            Probes++;
#endif
            ToolIntent fallback=ChooseSeed(p);
            if(fallback!=null && yieldHarvest){yieldHarvest=false;return fallback;}
            int tool=-1;
            if(p.selectedItem>=0 && p.selectedItem<50 && IsTool(p.HeldItem) && host.Candidate(p,p.selectedItem))tool=p.selectedItem;
            for(int i=0;i<50;i++)if(tool<0 && p.inventory[i].type==5295 && host.Candidate(p,i))tool=i;
            for(int i=0;i<50;i++)if(tool<0 && IsTool(p.inventory[i]) && host.Candidate(p,i))tool=i;
            if(tool<0)return fallback;
            int cx=(int)(p.Center.X/16),cy=(int)(p.Center.Y/16);Item source=p.inventory[tool];
            int bx=-1,by=-1,style=-1;float best=float.MaxValue;
            for(int x=cx-10;x<=cx+10;x++)for(int y=cy-10;y<=cy+10;y++)
            {
                var t=WorldTileObservation.ReadCurrent(x,y);if(!t.Readable || !t.Active || t.Type!=83 && t.Type!=84)continue;
                int s=t.FrameX/18;if(Unknown(x,y) || !Harvestable(p,source,x,y,s))continue;
                float distance=Vector2.DistanceSquared(p.Center,new Vector2(x*16+8,y*16+8));if(distance>=best)continue;best=distance;bx=x;by=y;style=s;
            }
            if(bx<0)return fallback;
            yieldHarvest=true;
            return new ToolIntent{Kind=ToolKind.Harvest,Slot=tool,Target=new Vector2(bx*16+8,by*16+8),Valid=()=>host.Mode(1)!=0 && !Unknown(bx,by) && Harvestable(p,source,bx,by,style),Completed=(started,unknown)=>{if(unknown)HoldUnknown(bx,by);}};
        }
        private ToolIntent ChooseSeed(Player p)
        {
            int remaining=Pending.Count;
            while(remaining-->0)
            {
                int index=Pending.Next();if(index<0)break;var entry=Pending[index];if(Unknown(entry.X,entry.Y))continue;
                for(int slot=0;slot<50;slot++)
                {
                    Item source=p.inventory[slot];
                    if(source==null || source.type!=seeds[entry.Style] || source.stack<=0 || !host.Candidate(p,slot,true) || !CanPlant(p,source,entry))continue;
                    return new ToolIntent{Kind=ToolKind.Seed,Slot=slot,Target=new Vector2(entry.X*16+8,entry.Y*16+8),
                        Valid=()=>host.Mode(1)!=0 && !Unknown(entry.X,entry.Y) && host.Tick-entry.Created<ReplantQueue.Lifetime && source.type==seeds[entry.Style] && CanPlant(p,source,entry),Completed=(started,unknown)=>{if(unknown)HoldUnknown(entry.X,entry.Y);}};
                }
            }
            return null;
        }
        private static bool CanPlant(Player p,Item seed,ReplantPoint point)
        {
            var t=WorldTileObservation.ReadCurrent(point.X,point.Y);
            if(p.noBuilding || !t.Readable || t.Active || !Support(point.X,point.Y) || !p.IsInTileInteractionRange(point.X,point.Y,TileReachCheckSettings.Simple,seed.tileBoost+p.blockRange))return false;
            Tile bottom=Main.tile[point.X,point.Y+1],cell=Main.tile[point.X,point.Y];
            if(bottom.halfBrick() || bottom.slope()!=0)return false;
            return cell.liquid==0 || (point.Style==5?cell.lava():point.Style>=4 && !cell.lava());
        }
        internal HarvestReceipt BeforePlant(Player p)
        {
            if(!ReferenceEquals(p,host.Player) || !host.Use.Is(ToolKind.Harvest) || !host.Use.ActionValid)return default(HarvestReceipt);
            int x=Player.tileTargetX,y=Player.tileTargetY;var t=WorldTileObservation.ReadCurrent(x,y);
            return new HarvestReceipt{Valid=t.Readable && t.Active && (t.Type==83 || t.Type==84),X=x,Y=y,Style=t.FrameX/18,Token=host.Use.Operation};
        }
        internal void AfterPlant(Player p,HarvestReceipt receipt)
        {
            if(!receipt.Valid || !ReferenceEquals(p,host.Player) || receipt.Token!=host.Use.Operation)return;
            var after=WorldTileObservation.ReadCurrent(receipt.X,receipt.Y);
            var bottom=WorldTileObservation.ReadCurrent(receipt.X,receipt.Y+1);
            if(after.Readable && !after.Active && bottom.Readable && bottom.Active && (bottom.Type==78 || bottom.Type==380))Pending.Add(receipt.X,receipt.Y,receipt.Style,host.Tick);
        }
        internal void Reset(){Pending.Clear();nextProbe=0;yieldHarvest=false;}
    }
}
