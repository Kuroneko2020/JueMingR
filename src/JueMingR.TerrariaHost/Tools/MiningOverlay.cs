using System;
using JueMingR.Features.Tools;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Tools
{
    // Cached negatives bound idle discovery; positives never authorize a hit.
    // Every actual mining consumer still reads full native eligibility.
    // Known local/global changes refresh now;
    // external dependencies without hooks have a <=22-update fallback at 512.
    internal sealed class MiningOverlay
    {
        private struct Entry {internal MiningPoint Point;internal ulong Witness;internal bool Known,Green;}
        private readonly Entry[] entries=new Entry[MiningRegion.Capacity];
        private Rectangle reach;
        private object tiles,sections;
        private int count,cursor,power,tileWidth,tileHeight,ready;
        private bool held,blocked,hardMode,initialized;
        private double surface;
#if DEBUG
        internal long Checks {get;private set;}
        internal long WitnessReads {get;private set;}
#endif
        internal bool Green(int index){return held && entries[index].Green;}
        internal bool Candidate(int index){return index<count && entries[index].Green;}
        internal bool Any {get{return ready>0;}}
        internal Vector2 Center(int index){var p=entries[index].Point;return new Vector2(p.X*16+8,p.Y*16+8);}
        internal int Count {get{return count;}}
        internal void RemoveAt(int index)
        {if(index>=count)return;if(entries[index].Green)ready--;Array.Copy(entries,index+1,entries,index,count-index-1);entries[--count]=default(Entry);if(cursor>index)cursor--;}
        internal void Reject(int index){if(index<count && entries[index].Green){entries[index].Green=false;ready--;}}
        internal void Clear(){Array.Clear(entries,0,entries.Length);count=cursor=ready=0;initialized=false;tiles=sections=null;}
        private bool Changed(Player p,Item tool,Rectangle current)
        {return !initialized || reach!=current || blocked!=p.noBuilding || power!=tool.pick || hardMode!=Main.hardMode || surface!=Main.worldSurface ||
            !ReferenceEquals(tiles,Main.tile) || !ReferenceEquals(sections,Main.sectionManager) || tileWidth!=Main.maxTilesX || tileHeight!=Main.maxTilesY;}
        internal void Prepare(MiningRegion region,Player p,Item tool,bool holding)
        {
            held=holding;
            // Selection precedes the normal runtime update. Fresh capability,
            // reach and new-region facts must already apply at that entrance.
            if(count!=region.Count || Changed(p,tool,TileReachCheckSettings.Simple.GetTileRegion(p,tool.tileBoost)))Update(region,p,tool,holding);
        }
        internal void Update(MiningRegion region,Player p,Item tool,bool holding)
        {
            Rectangle current=TileReachCheckSettings.Simple.GetTileRegion(p,tool.tileBoost);
            bool changed=Changed(p,tool,current);
            reach=current;held=holding;blocked=p.noBuilding;power=tool.pick;hardMode=Main.hardMode;surface=Main.worldSurface;
            tiles=Main.tile;sections=Main.sectionManager;tileWidth=Main.maxTilesX;tileHeight=Main.maxTilesY;initialized=true;
            count=region.Count;
            for(int i=0;i<count;i++)
            {
                var point=region[i];var entry=entries[i];
                bool fresh=!entry.Known || entry.Point.X!=point.X || entry.Point.Y!=point.Y || entry.Point.Type!=point.Type;
                entry.Point=point;
                if(blocked || point.X<reach.Left || point.X>reach.Right || point.Y<reach.Top || point.Y>reach.Bottom)
                {entry.Green=false;entry.Known=true;entries[i]=entry;continue;}
                ulong witness=Witness(point.X,point.Y);
                if(changed || fresh || witness!=entry.Witness){entry.Green=Check(p,tool,point);entry.Witness=witness;}
                entry.Known=true;entries[i]=entry;
            }
            // Membership/own-hit/support witnesses do not depend on this
            // polling cursor. It covers unhooked section/neighbour changes.
            for(int n=0;n<Math.Min(24,count);n++)
            {
                int i=cursor%count;cursor=(i+1)%count;var entry=entries[i];var point=entry.Point;
                if(!blocked && point.X>=reach.Left && point.X<=reach.Right && point.Y>=reach.Top && point.Y<=reach.Bottom)
                {entry.Green=Check(p,tool,point);entries[i]=entry;}
            }
            ready=0;for(int i=0;i<count;i++)if(entries[i].Green)ready++;
        }
        private bool Check(Player p,Item tool,MiningPoint point)
        {
#if DEBUG
            Checks++;
#endif
            return MiningEligibility.CanProgress(p,tool,point.X,point.Y,point.Type);
        }
        private ulong Witness(int x,int y)
        {
            ulong value=1469598103934665603UL;
            for(int dy=-1;dy<=1;dy++)
            {
                var t=WorldTileObservation.ReadCurrent(x,y+dy);
#if DEBUG
                WitnessReads++;
#endif
                ulong bits=t.Readable?1UL:0;if(t.Active)bits|=2;if(t.Inactive)bits|=4;
                bits|=(ulong)(ushort)t.Type<<3;bits|=(ulong)(ushort)t.FrameX<<19;bits|=(ulong)(ushort)t.FrameY<<35;
                unchecked{value=(value^bits)*1099511628211UL;if(t.Readable)value=(value^Main.tile[x,y+dy].wall)*1099511628211UL;}
            }
            return value^(Terraria.GameContent.FixExploitManEaters.SpotProtected(x,y)?1UL:0);
        }
    }
}
