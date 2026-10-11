using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked Terraria1.4.5.8 AI024 motor. Original owl transform remains
    // inside this AI024 action; only the next action dispatches AI002.
    // Poop1080's lottery writes L3/an outside actor, never this motor.
    internal static class NpcBirdMotion
    {
        internal static bool Type(int type)
        {return type==74 || type==297 || type==298 || type==442 || type==611 || type>=671 && type<=675 || type==689;}
        internal static bool Known(NpcMotionState n){return n.Style==24 && Type(n.EffectiveType);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return Known(a) && Known(b) || a.Identity.Type==689 && b.Identity.Type==689 && (Known(a) || a.EffectiveType==317 && a.Style==2) && (Known(b) || b.EffectiveType==317 && b.Style==2);}
        private static bool Owl(int type){return type==611 || type==689;}
        internal static bool Census(NpcMotionState n,PredictionEnvironment e)
        {return Owl(n.EffectiveType) && n.L0!=0 && n.A0==1 && !e.Day && e.BirdCloudAlpha==0;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {
            // The flying body reads only raw target.dead, not target position.
            // Actual wake/retarget, wet and689's nearest-transform query are
            // separate numbered-player geometry consumers.
            bool query=n.EffectiveType==689 && n.BirdNetMode!=1 && !e.Day || n.A0==0 || Owl(n.EffectiveType) && n.L0==0 || n.Wet;
            bool independent=n.EffectiveType==689 && n.BirdNetMode!=1 && !e.Day && e.Players!=null && e.Players.Count>0;
            bool rectangle=n.A0==0 && !Owl(n.EffectiveType) && n.BirdNetMode!=1 && n.BirdReleaseOwner==255 && n.Vx==0 && n.Vy>=0 && (double)n.Vy<=.3;
            bool rawFacing=n.PlayerActive && !n.PlayerNativeDead && !(n.TargetNoAggro && n.Direction!=0) && !(n.PlayerIdle && n.Target>=0 && n.Target<255 && !n.Boss);
            return independent || query && (n.HasClosestPlayer || rawFacing || rectangle && n.PlayerActive);
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.A0==0 || Owl(n.EffectiveType) && n.L0==0 || n.Wet;}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0)
        {
            if(n.EffectiveType==689 && n.BirdNetMode!=1 && !e.Day)
            {MotionRect area;int slot;PredictionStop stop;if(Nearest(n,e,0,out slot,out area,out stop))return slot;}
            return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.BirdFactsCaptured || !e.BirdWeatherCaptured){stop=PredictionStop.MissingDependency;return false;}
            bool originalOwl=Owl(n.EffectiveType);n.NoGravity=true;
            if(originalOwl)
            {
                if(n.EffectiveType==689 && n.BirdNetMode!=1 && !e.Day)
                {
                    MotionRect nearest;int slot;if(!Nearest(n,e,elapsed,out slot,out nearest,out stop))return false;
                    float dx=n.Bounds.CenterX-nearest.CenterX,dy=n.Bounds.CenterY-nearest.CenterY;
                    float distance=(float)Math.Sqrt((double)dx*dx+(double)dy*dy);
                    if(distance<80)
                    {
                        if(t==null){stop=PredictionStop.MissingDependency;return false;}
                        bool clear;if(!t.CanHit(n.Bounds,nearest,out clear,out stop))return false;
                        if(clear)
                        {
                            if(!NpcPhasedFlightMotion.TransformKnown(ref n,ref e,317,30,32,2,6,false,out stop))return false;
                            if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
                            // SetDefaults clears the current derived flag;
                            // retained buff clocks reapply only next action.
                            confused=false;
                        }
                    }
                }
                if(n.L0==0)
                {if(!Query(ref n,ref e,confused,out stop))return false;n.A0=1;n.L0=1;}
                else if(n.A0==1 && e.BirdCloudAlpha==0 && !e.Day)
                {
                    if(!n.BirdRelationsCaptured || !n.BirdRelationsComplete || group==null){stop=PredictionStop.MissingDependency;return false;}
                    // Source sorts actual full identities by native slot.
                    // A later same-type awake bird can undo an earlier town
                    // landing choice and terminate this original scan.
                    for(int i=0;i<count;i++)
                    {
                        var row=group[i];if(!row.Active || row.Identity.Slot==n.Identity.Slot)continue;
                        if(row.Town)
                        {
                            if(!row.BirdRelationsCaptured || !row.BirdRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                            if(Math.Abs(n.Bounds.CenterX-row.Bounds.CenterX)<96)
                            {
                                float dy=row.Bounds.CenterY-n.Bounds.CenterY;
                                if(dy>32 && dy<320)
                                {if(t==null){stop=PredictionStop.MissingDependency;return false;}bool clear;if(!t.CanHit(n.Bounds,row.Bounds,out clear,out stop))return false;if(!clear)n.A0=2;}
                            }
                        }
                        else if(row.EffectiveType==n.EffectiveType)
                        {
                            if(!row.BirdRelationsCaptured || !row.BirdRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                            if(row.A0!=1 && Math.Abs(n.Bounds.CenterX-row.Bounds.CenterX)<320){n.A0=1;break;}
                        }
                    }
                }
            }
            if(n.A0==0)
            {
                n.NoGravity=false;
                if(Owl(n.EffectiveType) && (e.BirdCloudAlpha>0 || e.Day))n.A0=1;
                if(!Query(ref n,ref e,confused,out stop))return false;
                if(n.BirdNetMode!=1)
                {
                    if(n.BirdReleaseOwner!=255 || n.Vx!=0 || n.Vy<0 || (double)n.Vy>.3)
                    {n.A0=1;n.Direction=-n.Direction;}
                    else if(!Owl(n.EffectiveType))
                    {
                        if(!n.TargetCaptured || !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
                        // Native TargetClosest materializes an integer box;
                        // its wake rectangle also truncates raw top-left.
                        var p=n.PlayerArea;
                        float left=(int)n.X-100,top=(int)n.Y-100;
                        if(left<p.X+p.Width && left+n.Width+200>p.X && top<p.Y+p.Height && top+n.Height+200>p.Y || n.Life<n.LifeMax)
                        {n.A0=1;n.Vy-=6;n.Direction=-n.Direction;}
                    }
                }
            }
            else if(n.A0==2)
            {n.Vx*=.98f;if(n.Vy==0){n.A0=0;n.Vx=0;}n.Vy+=.05f;if(n.Vy>2)n.Vy=2;}
            else
            {
                bool dead;if(n.BirdRawTargetCaptured)dead=n.BirdRawTargetDead;else{stop=PredictionStop.MissingDependency;return false;}
                if(!dead)
                {
                    float limit=n.EffectiveType>=671 && n.EffectiveType<=675?4:3;
                    if(n.CollideX)
                    {n.Direction*=-1;n.Vx=n.OldVx*-.5f;if(n.Direction==-1 && n.Vx>0 && n.Vx<limit-1)n.Vx=limit-1;if(n.Direction==1 && n.Vx<0 && n.Vx>-limit+1)n.Vx=-limit+1;}
                    if(n.CollideY)
                    {n.Vy=n.OldVy*-.5f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy>-1)n.Vy=-1;}
                    if(n.Direction==-1 && n.Vx>-limit)
                    {n.Vx-=.1f;if(n.Vx>limit)n.Vx-=.1f;else if(n.Vx>0)n.Vx-=.05f;if(n.Vx<-limit)n.Vx=-limit;}
                    else if(n.Direction==1 && n.Vx<limit)
                    {n.Vx+=.1f;if(n.Vx<-limit)n.Vx+=.1f;else if(n.Vx<0)n.Vx+=.05f;if(n.Vx>limit)n.Vx=limit;}
                    if(t==null){stop=PredictionStop.MissingDependency;return false;}
                    int x=(int)((n.X+n.Width/2)/16f)+n.Direction,y=(int)((n.Y+n.Height)/16f);bool empty=true,near=false;
                    for(int row=y;row<y+15;row++)
                    {
                        // Native WorldGen.InWorld skips outside cells, unlike
                        // AI113's unbounded column index. Do not reuse that
                        // family's out-of-world rejection here.
                        if(x<0 || x>=e.WorldWidth || row<0 || row>=e.WorldHeight)continue;
                        PredictionTile tile;if(!t.Tile(x,row,out tile,out stop))return false;
                        if(tile.Active && tile.RawSolid || tile.Liquid>0){near=row<y+5;empty=false;break;}
                    }
                    n.Vy+=empty?.05f:-.1f;if(near)n.Vy-=.2f;n.Vy=Math.Max(-4,Math.Min(2,n.Vy));
                }
            }
            if(n.Wet)
            {n.A1=0;if(n.Vy>0)n.Vy*=.95f;n.Vy-=.5f;if(n.Vy<-4)n.Vy=-4;if(!Query(ref n,ref e,confused,out stop))return false;}
            return true;
        }
        private static bool Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.HasClosestPlayer)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
            }
            else
            {
                // No winner retains a legal raw target; an invalid target
                // becomes0. Only a genuinely inactive fallback has no future
                // player action. Active dead/ghost is not a frozen dummy.
                if(!n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
                int old=n.Target;n.Target=n.PlayerIndex;var p=n.PlayerArea;
                if(!n.PlayerNativeDead && !(n.TargetNoAggro && n.Direction!=0) && !(n.PlayerIdle && old>=0 && old<255 && !n.Boss))
                {n.Direction=p.X+(int)p.Width/2<n.X+n.Width/2?-1:1;n.DirectionY=p.Y+(int)p.Height/2<n.Y+n.Height/2?-1:1;}
                if(confused)n.Direction=-n.Direction;
                e=NpcTargeting.Player(n,e);
            }
            n.BirdRawTargetCaptured=true;n.BirdRawTargetDead=n.PlayerNativeDead;return true;
        }
        internal static bool Nearest(NpcMotionState n,PredictionEnvironment e,int elapsed,out int slot,out MotionRect area,out PredictionStop stop)
        {
            stop=PredictionStop.None;slot=0;area=default(MotionRect);
            if(e.Players==null || !e.Players.SearchCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(e.Players.Count==0)
            {
                if(!e.BirdInactiveZeroCaptured){stop=PredictionStop.MissingDependency;return false;}
                area=e.BirdInactiveZero;return true;
            }
            int chosen=0;float best=-1;area=e.Players.SearchAreaAt(0);
            for(int i=0;i<e.Players.Count;i++)
            {
                if(e.Players.SearchDeadAt(i))continue;
                if(elapsed>0 && e.Players.SlotAt(i)!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}
                var candidate=e.Players.SearchAreaAt(i);
                if(e.Players.SlotAt(i)==e.PlayerIndex && e.PlayerTimelineActive)candidate=new MotionRect(e.PlayerX-candidate.Width*.5f,e.PlayerY-candidate.Height*.5f,candidate.Width,candidate.Height);
                float distance=Math.Abs(candidate.X+(int)candidate.Width/2-(n.X+n.Width/2))+Math.Abs(candidate.Y+(int)candidate.Height/2-(n.Y+n.Height/2));
                if(best==-1 || distance<best){best=distance;chosen=i;area=candidate;}
            }
            slot=e.Players.SlotAt(chosen);
            // Active dead fallback still has a player action and cannot be
            // certified as a frozen box. Ghost is not removed from the search.
            if(elapsed>0 && (slot!=e.PlayerIndex || !e.PlayerTimelineActive)){stop=PredictionStop.MissingDependency;return false;}
            return true;
        }
    }
}
