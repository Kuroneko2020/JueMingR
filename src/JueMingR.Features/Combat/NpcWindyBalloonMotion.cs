using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI113 parent motor. Caller retains normal NPC gravity,
    // movement/collision and slot order. The controlled slime has a genuine
    // AI001 item prelude before its -999 early return; it is not inert.
    internal static class NpcWindyBalloonMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.EffectiveType==594 && n.Style==113;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return Known(n) && n.WindySlaveValid && n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.L0==0 && n.WindyNetMode!=1 || n.CollideX || n.CollideY || n.Wet;}
        internal static bool NeededWriter(NpcMotionState n,NpcMotionState[] group,int count)
        {
            // Caller uses this only for nonselected incoming balloon rows.
            // Once its last controlled slave leaves -999, that writer no
            // longer changes the selected slime. An unrelated later wall or
            // player choice must not shorten the released slime's horizon.
            // A real shared-life consumer still owns its health dependency.
            if(!Known(n) || !n.WindyRelationsCaptured)return true;
            for(int i=0;i<count;i++)
            {
                var row=group[i];
                if(row.Active && row.Health.RealLife==n.Identity.Slot && row.Identity.Slot!=n.Identity.Slot)return true;
                if(n.WindySlaveValid && row.Identity.Equals(n.WindySlaveIdentity) && row.Active && row.EffectiveType==1 && row.A0==-999)return true;
            }
            return false;
        }
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0)
        {
            if(Known(n))return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;
            // Selected held slime's own255 target is not a numbered-player
            // consumer. The needed balloon motor owns the player premise.
            if(n.EffectiveType==1 && n.WindyRelationsCaptured && NpcWindySlimePrelude.HeldAfterPrelude(n))
            {
                if(NpcWindySlimePrelude.HeldNeedsPlayer(n))return n.PlayerIndex;
                for(int i=0;i<count;i++)
                {
                    var parent=group[i];
                    if(!parent.Active || !Known(parent) || !parent.WindySlaveValid || !parent.WindySlaveIdentity.Equals(n.Identity))continue;
                    if(Retargets(parent,e) && parent.HasClosestPlayer)return parent.ClosestPlayerIndex;
                    if(parent.HasPlayer && parent.PlayerActive && !parent.PlayerNativeDead && !parent.PlayerGhost)return parent.PlayerIndex;
                    if(parent.HasClosestPlayer)return parent.ClosestPlayerIndex;
                }
                return n.PlayerIndex;
            }
            return NpcSlimeControl.PlayerPremiseTarget(n,e);
        }

        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.WindyRelationsCaptured || !n.WindyRelationsComplete || !n.WindySlaveCaptured)
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.L0==0 && n.WindyNetMode!=1)
            {
                // Native creates an unobserved full identity and chooses its
                // actual variant/scale before resizing or consuming it.
                stop=PredictionStop.PhaseBoundary;return false;
            }
            float cx=n.Bounds.CenterX,cy=n.Bounds.CenterY;
            // Native Vector2 multiplication stores the dimension in a float
            // FIELD before Entity.Size truncates it to int. A direct x86
            // (int)(20f*AI1) can truncate an extended product just below the
            // integer, shrinking both dimensions by1 at ordinary slime scales.
            // This copied field is scratch only until the original scale write.
            n.Scale=(float)(20d*n.A1);n.Width=n.Height=(int)n.Scale;
            n.Scale=n.A1;n.X=cx-n.Width*.5f;n.Y=cy-n.Height*.5f;
            int index=Slave(ref n,group,count,out stop);if(stop!=PredictionStop.None)return false;
            if(index<0)
            {
                n.Vy=Clamp(n.Vy-.2f,-8,8);n.Vx=Clamp(n.Vx+e.WindTarget*.3f,-4,4);
                if(n.CollideX || n.CollideY)
                {
                    n.X-=n.OldVx+n.Direction*8;
                    NpcTargeting.Face(ref n,ref e,false,confused);n.Direction*=-1;n.Vx=n.Direction*2;
                    n.Life=-1;n.Active=false;stop=PredictionStop.Despawn;return false;
                }
                return true;
            }
            var child=group[index];
            bool target=n.TargetCaptured && n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost;
            var area=NpcTargeting.Area(n,e);float dx=0,dy=0,distance=float.PositiveInfinity;int rise=0;
            if(target)
            {
                dx=area.CenterX-n.Bounds.CenterX;dy=area.CenterY-(n.Y+n.Height);
                if(dy<0)rise=(int)dy/-16;
                // XNA Length stores a float result after the sqrt. The motor
                // does not replace it with an integer or squared-distance gate.
                distance=(float)Math.Sqrt((double)dx*dx+(double)dy*dy);
                if(n.Direction!=Math.Sign(dx))rise=0;
            }
            if(n.Wet || child.Wet || n.CollideX || n.CollideY && n.OldVy<0)
            {
                float correction=n.OldVx+n.Direction*8;n.X-=correction;child.X-=correction;group[index]=child;
                NpcTargeting.Face(ref n,ref e,false,confused);n.Direction*=-1;n.Vx=n.Direction*2;n.Life=-1;
                if(!HealthDeath(ref n,group,count,out stop))return false;
                n.Active=false;stop=PredictionStop.Despawn;return false;
            }
            if(n.CollideY)
            {
                n.Vy=n.OldVy>0?1:-1;NpcTargeting.Face(ref n,ref e,false,confused);
                if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
                // Native distance/rise above uses the old target. Only the
                // subsequent CanHit and quarter-height guidance use this query.
                area=NpcTargeting.Area(n,e);
            }
            float limit=2f+Math.Abs(e.WindTarget)*2f;
            if(Math.Sign(n.Vx)!=n.Direction || Math.Abs(n.Vx)<limit)
            {
                n.Vx+=n.Direction*.01f;
                if(n.Vx*n.Direction<0)n.Vx+=n.Direction*(Math.Abs(n.Vx)>limit?.1f:.05f);
                else if(Math.Abs(n.Vx)>limit)n.Vx=n.Direction*limit;
            }
            if(t==null){stop=PredictionStop.MissingDependency;return false;}
            int tx=(int)((n.X+n.Width/2)/16f)+n.Direction,ty=(int)((n.Y+n.Height)/16f);bool empty=true,near=false;
            for(int y=ty;y<ty+8+rise;y++)
            {
                // Unlike safe framing, native AI113 directly indexes this
                // column. Do not silently invent out-of-world air.
                if(tx<0 || tx>=e.WorldWidth || y<0 || y>=e.WorldHeight){stop=PredictionStop.TerrainUnavailable;return false;}
                PredictionTile cell;if(!t.Tile(tx,y,out cell,out stop))return false;
                if(cell.Active && cell.RawSolid || cell.Liquid>0){near=y<ty+5+rise;empty=false;break;}
            }
            bool clear=false;if(distance<400 && !t.CanHit(n.Bounds,area,out clear,out stop))return false;
            if(distance<400 && clear)
            {
                double bodyQuarter=(double)n.Bounds.CenterY+n.Height/4;
                double playerQuarter=(double)area.Y+(int)area.Height/4;
                if(bodyQuarter>playerQuarter && n.Vy>-2){n.Vy-=.035f;if(n.Vy>0)n.Vy-=.035f;}
                else if(bodyQuarter<playerQuarter && n.Vy<2){n.Vy+=.035f;if(n.Vy<0)n.Vy+=.035f;}
            }
            else
            {
                n.Vy+=empty?.05f:-.1f;if(near)n.Vy-=.2f;n.Vy=Clamp(n.Vy,-4,2);
            }
            child=group[index];
            float anchorY=n.Y+n.Height;anchorY-=8;float offset=56f*n.A1;anchorY+=offset;
            child.X=n.Bounds.CenterX-child.Width*.5f;child.Y=anchorY-child.Height*.5f;child.Vx=n.Vx;child.Vy=n.Vy;
            group[index]=child;return true;
        }

        // Shared caller invokes this only after actual NpcHealth death on594
        // or this kernel's collision death, never ordinary inactivity retire.
        internal static bool HealthDeath(ref NpcMotionState n,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            // NpcHealth's committed death path currently leaves its copied
            // Life=1 while returning Despawn. The caller owns the real death
            // condition; do not suppress the release by rechecking that copy.
            stop=PredictionStop.None;if(!Known(n))return true;
            int index=Slave(ref n,group,count,out stop);if(stop!=PredictionStop.None)return false;
            if(index>=0){var child=group[index];child.A0=0;child.Y-=10;group[index]=child;}
            return true;
        }
        private static int Slave(ref NpcMotionState n,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.WindySlaveCaptured){stop=PredictionStop.MissingDependency;return -1;}
            if(!n.WindySlaveValid)
            {
                // A known in-range non-slave still has a raw object identity;
                // native clears AI3 only for that case, not an invalid index.
                if(n.WindySlaveIdentity.Token!=null)n.A3=-1;
                return -1;
            }
            int index=-1;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.WindySlaveIdentity)){index=i;break;}
            if(index<0){stop=PredictionStop.MissingDependency;return -1;}
            var row=group[index];
            if(!row.Active || row.EffectiveType!=1 || row.A0!=-999){n.A3=-1;return -1;}
            return index;
        }
        private static float Clamp(float value,float minimum,float maximum)
        {return Math.Max(minimum,Math.Min(maximum,value));}
    }
}
