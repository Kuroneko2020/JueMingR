using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI94 owns its height correction and sinusoidal motor. Its attacks spawn
    // other actors; their random emitter clocks do not steer this body. Shield
    // souls are different: already observed 629s consume the shared counter
    // AFTER the native NPC loop and expose the tower on its next AI action.
    internal static class NpcCelestialTowerMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.Style==94 && (n.EffectiveType==422 || n.EffectiveType==493 || n.EffectiveType==507 || n.EffectiveType==517);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return Known(a) && a.EffectiveType==b.EffectiveType && (a.A2==1)==(b.A2==1);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return Known(n) && n.A2!=1 && !e.Multiplayer && n.L0>=59 && n.L0<60 && n.Life<n.LifeMax;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return NeedsPlayer(n,e);}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.A2==1)
            {
                double x=n.Vx,y=n.Vy;n.Vx=0;n.Vy=(float)Math.Sqrt(x*x+y*y);
                // These are independent tests: a value just below .25 can
                // cross it and be reduced again in this same native action.
                if(n.Vy<.25f)n.Vy+=.02f;if(n.Vy>.25f)n.Vy-=.02f;
                n.Health.DontTakeDamage=true;n.CanReceive=false;n.A1++;
                if(n.A1>120)
                {
                    float opacity=(float)(1d-((double)n.A1-120d)/60d);
                    float alpha=(float)((1d-opacity)*255d);n.Alpha=(int)Math.Max(0,Math.Min(255,alpha));
                }
                if(n.A1>=180){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
                return true;
            }
            if(!n.TowerRelationsCaptured || !n.TowerRelationsComplete || !n.TowerShieldCaptured || e.TowerConsumers==null)
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.TowerShieldStrength!=0 && elapsed>=n.TowerNewConsumerAt)
            {stop=PredictionStop.PhaseBoundary;return false;}
            if(n.A3>0){n.A3++;if(n.A3>120)n.A3=0;}
            n.Health.DontTakeDamage=n.TowerShieldStrength!=0;
            n.CanReceive=!n.Health.DontTakeDamage && !n.Health.Immortal;
            bool heal=n.L0>=60 && !e.Multiplayer;
            if(NeedsPlayer(n,e))
            {
                if(elapsed>1 && e.Players!=null && e.Players.Count>1){stop=PredictionStop.MissingDependency;return false;}
                NpcTargeting.Face(ref n,ref e,false,confused);
                if(!n.TargetCaptured || !n.HasPlayer || n.TargetChoiceUnavailable || n.PlayerGhost && elapsed>1)
                {stop=PredictionStop.MissingDependency;return false;}
                double dx=(double)e.PlayerX-n.Bounds.CenterX,dy=(double)e.PlayerY-n.Bounds.CenterY;
                heal=(float)Math.Sqrt(dx*dx+dy*dy)>2000;
            }
            else if(confused)n.Direction=-n.Direction;
            if(heal)n.Life=(int)Math.Max(0,Math.Min(n.LifeMax,n.Life+200));
            // Native resets this clock in BOTH branches; distance alone does
            // not accumulate sixty consecutive actions from an observed zero.
            n.L0=0;n.Vx=0;
            n.Vy=(float)Math.Sin((double)((float)Math.PI*2f)*n.A0/300d)*.5f;
            int ox=(int)(n.X+n.Width*.5f)>>4,oy=(int)(n.Y+n.Height)>>4;
            if(InWorld(ox,oy,e,60))
            {
                PredictionTile origin;if(terrain==null || !terrain.Tile(ox,oy,out origin,out stop))return false;
                int distance;if(!Down(ox,oy,30,e,terrain,out distance,out stop))return false;
                if(distance>=0 && distance<10)n.Y-=1.5f*(1f-distance/10f);
                else if(distance<0 || distance>=20)n.Y+=1.5f*(distance<0?1f:(distance-20)/10f);
            }
            float max=e.WorldWidth*16,edge=960;
            if(n.Bounds.CenterX<edge)n.X=1040-n.Width*.5f;
            if(n.Bounds.CenterX>max-edge)n.X=max-1040-n.Width*.5f;
            if(n.Bounds.CenterY<edge)n.Y=1040-n.Height*.5f;
            // The locked source uses maxTilesX for this Y clamp too.
            if(n.Bounds.CenterY>max-edge)n.Y=max-1040-n.Height*.5f;
            if(e.GravityWorldSurface<=50){if(n.Y<640)n.Y=e.RockLayer*16;}
            else if(!e.Remix && !e.GoodWorld && (double)(n.Y+n.Height)>e.GravityWorldSurface*16d-100d)
                n.Y=(float)e.GravityWorldSurface*16-n.Height-100;
            n.A0++;if(n.A0>=300)n.A0=0;
            // Future A1 resets are emitter effects, not a motion-state claim.
            if(n.EffectiveType!=507 && n.A1>0)n.A1--;
            return true;
        }
        private static bool InWorld(int x,int y,PredictionEnvironment e,int margin)
        {return x>=margin && y>=margin && x<e.WorldWidth-margin && y<e.WorldHeight-margin;}

        // This is an EARLIEST possible new soul consumer, not a certified
        // health timeline. Future water/AI invulnerability/healing may defer
        // death; they must never defer this conservative dependency boundary.
        // A birth during NPC action f first changes an NPC shield premise at
        // action f+1. Existing souls have their separate exact EndFrame motor.
        internal static int FirstNewConsumerAction(NpcMotionState n,PredictionEnvironment e,int maximum,NpcMotionState[] group,int count)
        {
            if(!n.Active || e.Multiplayer || maximum<2)return int.MaxValue;
            // Solar worm invalid links call checkDead even at full health.
            // Pure health rows do not certify future chain survival. Keeping
            // this explicit avoids freezing that AI death behind NoNewHits.
            if(n.EffectiveType==412 || n.EffectiveType==413 || n.EffectiveType==414)return 2;
            var h=n.Health;
            bool dot=h.Poison>0 || h.Fire>0 || h.Cursed>0 || h.Venom>0 || h.Frost>0 || h.Fire3>0 || h.Frost2>0 || h.Shadow>0;
            if(n.UnmodeledDamageTicks>0 || h.Regen<0 && !dot)return 2;
            if(!dot && h.RegenCount>=0)return int.MaxValue;
            int root=-1;
            if(h.RealLife>=0 && h.RealLife!=n.Identity.Slot)
            {
                for(int i=0;i<count;i++)if(group[i].Identity.Slot==h.RealLife && group[i].Active){root=i;break;}
                if(root<0)return 2;
            }
            // Value copies only: Source's observed rows remain immutable.
            var projected=root<0?null:new[]{group[root]};
            if(root>=0){n.Health.RealLife=projected[0].Identity.Slot;projected[0].Health.Immortal=false;}
            n.Health.DontTakeDamage=false;n.Health.Immortal=false;n.Health.WetBuff=0;n.Health.Buffs=default(NpcBuffLayout);
            for(int action=1;action<maximum;action++)
            {
                PredictionStop stop;
                if(!NpcHealth.Step(ref n,projected,projected==null?0:1,e,out stop))
                    return action+1;
                if(root>=0 && (!projected[0].Active || projected[0].Life<=0))return action+1;
                if(n.Life<=0 || !n.Active)return action+1;
                // No remaining timer or residual negative settlement can
                // reduce this actor further without a genuinely new input.
                if(n.Health.Poison<=0 && n.Health.Fire<=0 && n.Health.Cursed<=0 && n.Health.Venom<=0 && n.Health.Frost<=0 && n.Health.Fire3<=0 && n.Health.Frost2<=0 && n.Health.Shadow<=0 && n.Health.RegenCount>=0)return int.MaxValue;
            }
            return int.MaxValue;
        }
        private static bool Down(int x,int y,int maximum,PredictionEnvironment e,IPredictionTerrain t,out int distance,out PredictionStop stop)
        {
            distance=-1;stop=PredictionStop.None;
            for(int offset=0;offset<maximum;offset++)
            {
                if(!InWorld(x,y+offset,e,10))continue;
                PredictionTile tile;if(!t.Tile(x,y+offset,out tile,out stop))return false;
                // Conditions.IsSolid reads active(), including actuated tiles;
                // it ignores slope/half/platform collision shape completely.
                if(tile.RawActive && tile.RawSolid){distance=offset;return true;}
            }
            return true;
        }

        internal struct ConsumerWork
        {
            internal PredictionTowerShieldConsumer Fact;
            internal bool Active;
            internal float X,Y,Vx,Vy;
            internal int TimeLeft;
        }
        internal static ConsumerWork[] CreateConsumers(PredictionEnvironment e)
        {
            if(e.TowerConsumers==null)return null;
            var work=new ConsumerWork[e.TowerConsumers.Count];
            for(int i=0;i<work.Length;i++)
            {var f=e.TowerConsumers[i];work[i]=new ConsumerWork{Fact=f,Active=true,X=f.Area.X,Y=f.Area.Y,Vx=f.Vx,Vy=f.Vy,TimeLeft=f.TimeLeft};}
            return work;
        }
        internal static bool EndFrame(ConsumerWork[] consumers,NpcMotionState[] group,int count,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(consumers==null)return true;
            for(int i=0;i<consumers.Length;i++)
            {
                var p=consumers[i];if(!p.Active)continue;var f=p.Fact;
                if(!f.SettingsKnown || f.ExtraUpdates<0 || f.ExtraUpdates>16){stop=PredictionStop.MissingDependency;return false;}
                for(int action=0;action<=f.ExtraUpdates && p.Active;action++)
                {
                    // World rejection does not call Kill and therefore does
                    // not consume a shield. It precedes even a bad AI0 owner.
                    var bounds=e.TowerConsumers;
                    if(p.X<=bounds.Left || p.X+f.Area.Width>=bounds.Right || p.Y<=bounds.Top || p.Y+f.Area.Height>=bounds.Bottom || !Finite(p.X) || !Finite(p.Y))
                    {p.Active=false;break;}
                    if(!f.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
                    int owner=Find(group,count,f.TargetIdentity);
                    if(owner<0){stop=PredictionStop.MissingDependency;return false;}var target=group[owner];
                    if(TowerType(target.EffectiveType) && !e.Multiplayer && !target.TowerShieldCaptured){stop=PredictionStop.MissingDependency;return false;}
                    float oldVx=p.Vx,oldVy=p.Vy;
                    if(!target.Active || Intersects(p.X,p.Y,(int)f.Area.Width,(int)f.Area.Height,target.Bounds))
                    {Kill(ref p,owner,group,count,e);break;}
                    float dx=target.Bounds.CenterX-(p.X+f.Area.Width*.5f),dy=target.Bounds.CenterY-(p.Y+f.Area.Height*.5f);
                    Normalize(ref dx,ref dy);p.Vx=dx*5;p.Vy=dy*5;
                    if(f.Owner<255 && !f.OwnerActive){Kill(ref p,owner,group,count,e);break;}
                    p.X+=f.Wet?oldVx:p.Vx;p.Y+=f.Wet?oldVy:p.Vy;
                    if(--p.TimeLeft<=0 || f.Penetrate==0){Kill(ref p,owner,group,count,e);break;}
                }
                consumers[i]=p;
            }
            return true;
        }
        private static void Kill(ref ConsumerWork p,int owner,NpcMotionState[] group,int count,PredictionEnvironment e)
        {
            p.Active=false;if(e.Multiplayer)return;var target=group[owner];if(!TowerType(target.EffectiveType))return;
            int shield=target.TowerShieldStrength;
            if(shield!=0){target.A3=1;group[owner]=target;}
            int next=(int)Math.Max(0,Math.Min(target.TowerShieldMaximum,shield-1));
            for(int i=0;i<count;i++)if(group[i].TowerShieldCaptured && group[i].EffectiveType==target.EffectiveType)group[i].TowerShieldStrength=next;
        }
        private static int Find(NpcMotionState[] group,int count,NpcIdentity id)
        {for(int i=0;i<count;i++)if(group[i].Identity.Equals(id))return i;return -1;}
        private static bool TowerType(int type){return type==422 || type==493 || type==507 || type==517;}
        private static bool Intersects(float x,float y,int width,int height,MotionRect b)
        {return (int)x+width>(int)b.X && (int)x<(int)b.X+(int)b.Width && (int)y+height>(int)b.Y && (int)y<(int)b.Y+(int)b.Height;}
        private static void Normalize(ref float x,ref float y)
        {double squared=(double)x*x+(double)y*y;float length=(float)Math.Sqrt(squared);double inverse=1d/length;x=(float)(x*inverse);y=(float)(y*inverse);}
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
    }
}
