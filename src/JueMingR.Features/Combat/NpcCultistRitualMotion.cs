using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI83 tablet registry and encoded-owner motor. Four tablet
    // registry slots are distinct from the selected devotee's encoded owner.
    // Raw owner geometry has no active/type/life gate in the native motor.
    internal static class NpcCultistRitualMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.Style==83 && (n.EffectiveType==437 || n.EffectiveType==438) || n.Style==3 && (n.EffectiveType==379 || n.EffectiveType==380);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.Style==3 && n.A3>=0 || n.EffectiveType==437 && n.CultistNetMode!=1 && n.L3!=0 && n.L0==1;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return NeedsPlayer(n,e);}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0)
        {
            if(n.EffectiveType==437 && NeedsPlayer(n,e))
            {
                // The tablet's own numbered target is not the target query.
                // Native release calls TargetClosest on each raw outer row.
                int a=Find(group,count,n.RitualPart0),d=Find(group,count,n.RitualPart3);
                if(a>=0 && group[a].Active)return group[a].HasClosestPlayer?group[a].ClosestPlayerIndex:group[a].PlayerIndex;
                if(d>=0 && group[d].Active)return group[d].HasClosestPlayer?group[d].ClosestPlayerIndex:group[d].PlayerIndex;
                return n.PlayerIndex;
            }
            if(!NeedsPlayer(n,e))return n.PlayerIndex;if(n.A0>=1000)n.A0=0;return NpcGroundMotion.PlayerPremiseTarget(n,e);
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.EffectiveType==437)return Tablet(ref n,e,terrain,group,count,out stop);
            if(n.Style==3 && n.A3>=0)
            {
                // Native56780 resets the tether animation counter before the
                // ordinary shooter/ground action; keep its existing solver.
                if(n.A0>=1000)n.A0=0;n.CanHarm=n.RitualReleasedCanHarm;n.NoContactDamage=!n.RitualReleasedCanHarm;
                return NpcGroundMotion.Fallback(ref n,e,terrain,confused,elapsed,out stop);
            }
            n.Vx*=.93f;if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;
            int raw=(int)(0f-n.A3-1f);
            if(n.EffectiveType==438 && raw==-1)return Retire(ref n,out stop);
            if(raw<0 || !n.PositionOwnerCaptured || !n.PositionOwnerValid){stop=PredictionStop.MissingDependency;return false;}
            int parent=Find(group,count,n.PositionOwner);
            if(parent<0){stop=PredictionStop.MissingDependency;return false;}
            var owner=group[parent];int direction=Math.Sign(owner.Bounds.CenterX-n.Bounds.CenterX);
            if(direction!=n.Direction){n.Vx=0;n.Direction=direction;if(n.EffectiveType==438)n.SpriteDirection=direction;}
            if(n.JustHit && n.CultistNetMode!=1 && owner.L0==0)
            {if(owner.Identity.Equals(n.Identity))n.L0=1;else{owner.L0=1;group[parent]=owner;}}
            if(n.Style==3)
            {
                n.DirectionY=-1;n.CanHarm=false;n.NoContactDamage=true;
                if(n.A0<1000)n.A0=1000;if(++n.A0>=1300)n.A0=1000;
            }
            else if(++n.A0>=300)n.A0=0;
            return true;
        }
        private static bool Tablet(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L3==0 && n.CultistNetMode!=1)
            {
                n.L3=1;
                bool floor;if(!CheckFloor(n.Bounds.CenterX,n.Bounds.CenterY,e,terrain,out floor,out stop))return false;
                if(!floor)return Retire(ref n,out stop);
                // Successful first native action creates four not-yet-seen
                // full identities/registry indices. Do not invent those slots.
                stop=PredictionStop.PhaseBoundary;return false;
            }
            if(n.CultistNetMode!=1 && (n.A0!=-1 || n.L0==1))
            {
                if(!n.RitualRelationsCaptured || !n.RitualRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                int a=Find(group,count,n.RitualPart0),b=Find(group,count,n.RitualPart1),c=Find(group,count,n.RitualPart2),d=Find(group,count,n.RitualPart3);
                if(a<0 || b<0 || c<0 || d<0){stop=PredictionStop.MissingDependency;return false;}
                if(n.L0==1)
                {
                    n.L0=2;var first=group[b];first.A1=1;group[b]=first;
                    if(!Release(a,group,e,out stop))return false;
                    var second=group[c];second.A1=1;group[c]=second;
                    if(!Release(d,group,e,out stop))return false;
                }
                bool alive=Matches(group[a],379) || Matches(group[d],379) || Matches(group[b],438) || Matches(group[c],438);
                if(n.A0!=-1 && !alive)
                {
                    // Random new439 X/terrain placement feeds only AI2 and
                    // emitted526 aim. Neither feeds this tablet's geometry,
                    // receiving or deterministic300-action retirement.
                    n.A0=-1;n.A1=n.A3=0;
                }
            }
            if(n.A0==-1 && ++n.A3>300)return Retire(ref n,out stop);
            return true;
        }
        private static bool Release(int index,NpcMotionState[] group,PredictionEnvironment e,out PredictionStop stop)
        {
            int timeline=e.PlayerIndex;bool timelineActive=e.PlayerTimelineActive;
            // A lower raw slot may have just consumed its last buff action:
            // native confused remains true although remaining buffTime is0.
            var row=group[index];row.A3=0;NpcTargeting.Face(ref row,ref e,true,row.RitualConfused);
            if(row.TargetChoiceUnavailable || row.Active && (!timelineActive || row.PlayerIndex!=timeline))
            {stop=PredictionStop.MissingDependency;return false;}group[index]=row;stop=PredictionStop.None;return true;
        }
        // Original CultistRitual.CheckFloor uses exactly four X columns and
        // the first passing Y in each. Platform eligibility is by raw Type,
        // even for an inactive/actuated tile, unlike ordinary solid collision.
        internal static bool CheckFloor(float centerX,float centerY,PredictionEnvironment e,IPredictionTerrain t,out bool valid,out PredictionStop stop)
        {
            valid=false;stop=PredictionStop.None;if(t==null){stop=PredictionStop.MissingDependency;return false;}
            int x=(int)(centerX/16f),y=(int)(centerY/16f),found=0;
            foreach(int offset in new[]{-10,-6,6,10})
            {
                bool column=false;
                for(int dy=-5;dy<12;dy++)
                {
                    int tx=x+offset,ty=y+dy;
                    // SolidTile catches an out-of-world index and returns
                    // false; GetTileSafely returns an empty type0 tile there.
                    // This is a known bound, not missing in-world terrain.
                    if(tx<0 || ty<0 || tx>=e.WorldWidth || ty>=e.WorldHeight)continue;
                    PredictionTile tile;if(!t.Tile(tx,ty,out tile,out stop))return false;
                    if(!(tile.Active && tile.RawSolid && !tile.SolidTop && !tile.Half && tile.Slope==0 || tile.Platform))continue;
                    bool full,left,right,middle;
                    if(!SolidTiles(tx-1,tx+1,ty-3,ty-1,e,t,out full,out stop))return false;
                    if(full)
                    {
                        if(!SolidTiles(tx,tx,ty-3,ty-1,e,t,out middle,out stop))return false;if(middle)continue;
                        if(!SolidTiles(tx+1,tx+1,ty-3,ty-2,e,t,out right,out stop))return false;if(right)continue;
                        if(!SolidTiles(tx-1,tx-1,ty-3,ty-2,e,t,out left,out stop))return false;if(left)continue;
                    }
                    column=true;break;
                }
                if(column)found++;
            }
            valid=found==4;return true;
        }
        private static bool SolidTiles(int x1,int x2,int y1,int y2,PredictionEnvironment e,IPredictionTerrain t,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;
            if(x1<0 || x2>=e.WorldWidth || y1<0 || y2>=e.WorldHeight-40){solid=true;return true;}
            for(int x=x1;x<=x2;x++)for(int y=y1;y<=y2;y++)
            {PredictionTile tile;if(!t.Tile(x,y,out tile,out stop))return false;if(tile.Active && tile.RawSolid && !tile.SolidTop){solid=true;return true;}}
            return true;
        }
        private static bool Matches(NpcMotionState n,int type){return n.Active && n.EffectiveType==type;}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity identity)
        {if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        private static bool Retire(ref NpcMotionState n,out PredictionStop stop){n.Active=false;n.Life=0;stop=PredictionStop.Despawn;return false;}
    }
}
