using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;
namespace JueMingR.Features.Combat
{
    // AI123 owns its controller/home lifetime and probes three local foot boxes.
    // Projectile/dust/camera RNG never selects its own motor. The actual Slow
    // buff DOES affect the next player action: retain this known NPC action and
    // end before that later consumer, then recover from the observed new input.
    internal static class NpcDeerclopsMotion
    {
        private struct V{internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static V Store(float x,float y){return new V{X=x,Y=y};}
        private static V Center(NpcMotionState n){return Store(n.X+n.Width*.5f,n.Y+n.Height*.5f);}
        private static V Bottom(NpcMotionState n){return Store(n.X+n.Width*.5f,n.Y+n.Height);}
        private static float Distance(V a,V b){var d=Store(a.X-b.X,a.Y-b.Y);return (float)Math.Sqrt((double)d.X*d.X+(double)d.Y*d.Y);}
        private static float Clamp(float x,float min,float max){return Math.Max(min,Math.Min(max,x));}
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Lerp(float value1,float value2,float amount){return value1+(value2-value1)*amount;}
        internal static bool Known(NpcMotionState n){return n.EffectiveType==668 && n.Style==123;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {int phase=(int)n.A0;return phase==0 || phase==4 || phase==6 || (phase==3 || phase==5) && n.A1+1==30 || phase==7 && n.A1+1==40;}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost || Retargets(n,e) && n.HasClosestPlayer;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool face,bool confused){NpcTargeting.Face(ref n,ref e,face,confused);}
        private static bool Target(NpcMotionState n,PredictionEnvironment e,out MotionRect hitbox,out V center,out PredictionDeerclopsPlayer fact)
        {
            hitbox=default(MotionRect);center=default(V);fact=default(PredictionDeerclopsPlayer);
            if(n.DeerPlayers==null || !n.DeerPlayers.TryGet(n.Target,out fact) || !fact.Active || fact.Dead || fact.Ghost || n.Target<0 || n.Target>=255)return false;
            var area=n.PlayerPreciseAreaCaptured && n.PlayerIndex==e.PlayerIndex?n.PlayerPreciseArea:fact.Area;
            // Hitbox truncation is distinct from NPCAimedTarget.Center's floats.
            hitbox=new MotionRect((int)area.X,(int)area.Y,(int)area.Width,(int)area.Height);center=Store(area.X+area.Width*.5f,area.Y+area.Height*.5f);return true;
        }
        private static bool HasSlow(PredictionDeerclopsPlayer fact,NpcMotionState n,int elapsed)
        {
            if(!fact.HasSlow)return false;
            if(fact.Slot!=n.DeerPlayers.Local.Slot)return true;
            int advances=elapsed-(n.DeerFirstPlayerStepCompleted?1:0);
            return advances<=0 || fact.SlowTicks-advances>0;
        }
        internal static int SlowChangeAction(PredictionDeerclopsPlayer fact,bool firstCompleted,int localSlot)
        {
            if(fact.Token==null || !fact.Active || fact.Dead)return int.MaxValue;
            int next=firstCompleted?2:1;
            if(!fact.Slow && fact.HasSlow && fact.SlowTicks>0)return next;
            if(fact.Slow && (!fact.HasSlow || fact.SlowTicks<=0))return next;
            if(fact.Slow && fact.Slot==localSlot && fact.SlowTicks>0)return fact.SlowTicks+(firstCompleted?2:1);
            return int.MaxValue;
        }
        private static bool RunAway(NpcMotionState n,bool valid,V center,PredictionDeerclopsPlayer fact,bool chasing)
        {
            if(!valid)return true;
            bool snow=fact.Snow || Distance(center,Store((int)n.A2*16,(int)n.A3*16))<=480;
            return (fact.Dead || !chasing && !snow) | Distance(Center(n),center)>=2400;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.DeerFactsCaptured || n.DeerPlayers==null){stop=PredictionStop.MissingDependency;return false;}
            if(elapsed>=n.DeerSlowConsumerAt){stop=PredictionStop.BuffTransition;return false;}
            PredictionDeerclopsPlayer current;
            if(e.PlayerTimelineActive && n.DeerPlayers.TryGet(e.PlayerIndex,out current) && elapsed>=SlowChangeAction(current,n.DeerFirstPlayerStepCompleted,n.DeerPlayers.Local.Slot))
            {stop=PredictionStop.BuffTransition;return false;}
            MotionRect target;V targetCenter;PredictionDeerclopsPlayer fact;bool valid=Target(n,e,out target,out targetCenter,out fact);
            bool far=Distance(Center(n),targetCenter)>=450;
            n.L3=Clamp(n.L3+(far?1:-1),0,30);n.Health.DontTakeDamage=n.L3>=30;n.CanReceive=!n.Health.DontTakeDamage && !n.Health.Immortal;
            if(!n.DeerHomeInitialized)
            {var bottom=Bottom(n);n.A2=(int)bottom.X>>4;n.A3=(int)bottom.Y>>4;n.DeerHomeInitialized=n.A2!=-1 || n.A3!=-1;n.TimeLeft=86400;}
            n.TimeLeft=Math.Max(0,n.TimeLeft-n.DeerDayRate);n.HomeTileY=(int)n.A3;
            if(!e.Expert)n.L2=0;
            else if(n.DeerNetMode!=1)
            {float life=(float)n.Life/n.LifeMax;float fraction=Clamp((life-1f)/(0f-1f),0,1);int interval=(int)(80f+(40f-80f)*fraction);n.L2+=1;if(interval<=0){stop=PredictionStop.InvalidState;return false;}if((int)n.L2%interval==0 && (int)n.L2/interval>=3)n.L2=0;}
            bool halt=false,home=false;int phase=(int)n.A0;
            switch(phase)
            {
                case -1:n.L3=-10;break;
                case 6:
                    Query(ref n,ref e,false,confused);valid=Target(n,e,out target,out targetCenter,out fact);
                    if(n.DeerNetMode!=1 && !RunAway(n,valid,targetCenter,fact,false)){Reset(ref n,0);break;}
                    if(n.DeerNetMode!=1 && n.TimeLeft<=0){Reset(ref n,8);break;}
                    home=true;n.A1+=1;var origin=Store((int)n.A2*16,(int)n.A3*16);bool near=Distance(Center(n),origin)<1020;
                    if(near && n.A1%600<420)halt=true;
                    if(n.DeerNetMode!=1 && (n.Y>origin.Y+1600 && n.A1>=300 || !near && n.A1>=1500))Reset(ref n,7);
                    break;
                case 0:
                    Query(ref n,ref e,true,confused);valid=Target(n,e,out target,out targetCenter,out fact);
                    if(n.DeerNetMode!=1 && RunAway(n,valid,targetCenter,fact,true)){Reset(ref n,6);break;}
                    n.A1+=1;var point=Store(Bottom(n).X,Bottom(n).Y-32);var closest=Store(Clamp(point.X,target.X,target.X+target.Width),Clamp(point.Y,target.Y,target.Y+target.Height));var delta=Store(closest.X-point.X,closest.Y-point.Y);
                    bool aligned=Math.Abs(delta.X)>=Math.Abs(delta.Y)*.6f || Distance(delta,default(V))<48;
                    bool yRange=delta.Y<=100+target.Height && delta.Y>=-200;
                    if(n.DeerNetMode!=1 && Math.Abs(delta.X)<120 && yRange && n.Vy==0 && n.L1>=2){n.Vx=0;Reset(ref n,4);break;}
                    if(n.DeerNetMode!=1 && Math.Abs(delta.X)<120 && yRange && n.Vy==0 && aligned){n.Vx=0;n.A0=1;n.A1=0;n.L1+=1;break;}
                    if(n.DeerNetMode!=1 && n.Vy==0 && n.Vx!=0 && n.A1>=240){n.Vx=0;Reset(ref n,2);break;}
                    if(n.DeerNetMode!=1 && n.Vy==0 && n.Vx==0 && n.A1>=90){n.Vx=0;Reset(ref n,5);break;}
                    if(n.DeerNetMode!=1 && n.Vy==0 && n.A1>=120 && valid && !fact.SlowImmune && !HasSlow(fact,n,elapsed) && Math.Abs(delta.X)>100){n.Vx=0;Reset(ref n,3);}
                    break;
                case 1:n.A1+=1;halt=true;if(n.DeerNetMode!=1 && n.A1>=80){n.A0=n.A1=0;}break;
                case 4:n.A1+=1;halt=true;Query(ref n,ref e,true,confused);if(n.DeerNetMode!=1 && n.A1>=90){n.A0=n.A1=0;}break;
                case 2:n.A1+=1;halt=true;if(n.DeerNetMode!=1 && n.A1>=60){n.A0=n.A1=0;}break;
                case 3:
                    n.A1+=1;halt=true;
                    if(n.DeerNetMode!=2 && (int)n.A1%4==0 && n.A1>=28 && !SlowPulse(ref n,e,elapsed,out stop))return false;
                    if(n.A1==30)Query(ref n,ref e,true,confused);
                    if(n.DeerNetMode!=1 && n.A1>=60){n.A0=n.A1=0;}break;
                case 5:n.A1+=1;halt=true;if(n.A1==30)Query(ref n,ref e,true,confused);if(n.DeerNetMode!=1 && n.A1>=60){n.A0=n.A1=0;}break;
                case 7:
                    n.A1+=1;halt=true;if(n.A1==40){Query(ref n,ref e,true,confused);if(n.DeerNetMode!=1){n.X=(int)n.A2*16-n.Width*.5f;n.Y=(int)n.A3*16-n.Height;n.NewSegment=true;}}
                    if(n.DeerNetMode!=1 && n.A1>=60){n.A0=n.A1=0;}break;
                case 8:
                    n.A1+=1;halt=true;if(n.A1>=40){n.Life=-1;n.Active=false;stop=PredictionStop.Despawn;return false;}break;
                default:break;
            }
            return Movement(ref n,e,terrain,halt,home,out stop);
        }
        private static void Reset(ref NpcMotionState n,int phase){n.A0=phase;n.A1=n.L1=0;}
        private static bool SlowPulse(ref NpcMotionState n,PredictionEnvironment e,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;var local=n.DeerPlayers.Local;
            if(local.Token==null || !local.Active || local.Dead || local.God || local.SlowImmune || HasSlow(local,n,elapsed))return true;
            V center;
            if(local.Slot==e.PlayerIndex)center=Store(e.PlayerX,e.PlayerY);
            else if(elapsed==1)center=Store(local.Area.X+local.Area.Width*.5f,local.Area.Y+local.Area.Height*.5f);
            else{stop=PredictionStop.MissingDependency;return false;}
            if(Distance(center,Center(n))<800)n.DeerSlowConsumerAt=Math.Min(n.DeerSlowConsumerAt,elapsed+1);
            return true;
        }
        private static bool Movement(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool halt,bool home,out PredictionStop stop)
        {
            stop=PredictionStop.None;MotionRect target;V center;PredictionDeerclopsPlayer fact;Target(n,e,out target,out center,out fact);
            if(home){target=new MotionRect((int)n.A2*16,(int)n.A3*16,16,16);if(Distance(Center(n),Store(target.X+8,target.Y+8))<240)target=new MotionRect((int)(Center(n).X+160*n.Direction),target.Y,16,16);}
            float dx=target.X+(int)target.Width/2-Center(n).X;bool near=Math.Abs(dx)<80;float speed=3.5f+1f*(1f-(float)n.Life/n.LifeMax);bool brake=near || halt;
            if(home && dx!=0)n.Direction=n.SpriteDirection=Math.Sign(dx);
            if(n.A0==-1){dx=5;speed=5.35f;brake=false;}
            if(brake){n.Vx*=.9f;if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;}else n.Vx=Lerp(n.Vx,(float)Math.Sign(dx)*speed,.25f);
            var foot=Store(Center(n).X-20,n.Y+n.Height-20);bool overlaps=foot.X<target.X && foot.X+n.Width>target.X+target.Width;bool below=foot.Y+20<target.Y+target.Height-16;
            bool teleport=n.A0==7,accept=Bottom(n).Y>=target.Y && !teleport;
            bool full,upper,front;
            if(!Solid(terrain,e,new MotionRect(foot.X,foot.Y,40,20),accept,out full,out stop) || !Solid(terrain,e,new MotionRect(foot.X,foot.Y,40,16),accept,out upper,out stop) || !Solid(terrain,e,new MotionRect(Store(foot.X+40*n.Direction,foot.Y).X,foot.Y,16,80),accept,out front,out stop))return false;
            if(full || upper)n.L0=0;
            if(teleport)n.Vy=-.1f;
            if((overlaps || near) && below)n.Vy=Clamp(n.Vy+.4f*2,.001f,16);
            else if(full && !upper)n.Vy=0;
            else if(full)n.Vy=Clamp(n.Vy+(-.4f),-8,0);
            else if(n.Vy==0 && !front){n.Vy=-8;n.L0=1;}
            else n.Vy=Clamp(n.Vy+.4f,-8,16);
            return true;
        }
        // Original four-argument SolidCollision includes properly framed tops.
        // Do not change shared Solid's no-platform contract or ignore null cells.
        internal static bool Solid(IPredictionTerrain terrain,PredictionEnvironment e,MotionRect box,bool accept,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;
            int left=Math.Max(0,Math.Min(e.WorldWidth-1,(int)(box.X/16f)-1)),right=Math.Max(0,Math.Min(e.WorldWidth-1,(int)((box.X+box.Width)/16f)+2));
            int top=Math.Max(0,Math.Min(e.WorldHeight-40,(int)(box.Y/16f)-1)),bottom=Math.Max(0,Math.Min(e.WorldHeight-40,(int)((box.Y+box.Height)/16f)+2));
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {PredictionTile cell;if(!terrain.Tile(x,y,out cell,out stop))return false;if(!cell.Active)continue;bool value=cell.Solid && !cell.SolidTop;if(accept)value|=cell.Platform?cell.ProperPlatformFrame:cell.SolidTop && cell.SurfacePlatform;if(!value)continue;float surface=y*16+(cell.Half?8:0);if(box.X+box.Width>x*16 && box.X<x*16+16 && box.Y+box.Height>surface && box.Y<y*16+16){solid=true;return true;}}
            return true;
        }
    }
}
