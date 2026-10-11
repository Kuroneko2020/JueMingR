using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI120's projectiles and dust do not feed its own motor. Its numbered
    // target, native phase clock and Remix's first raw636 do. No live world,
    // original AI, projectile birth or RNG is advanced by this value model.
    internal static class NpcEmpressMotion
    {
        private struct V {internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static V Store(float x,float y){return new V{X=x,Y=y};}
        private static V Center(NpcMotionState n){return Store(n.X+n.Width*.5f,n.Y+n.Height*.5f);}
        private static V Sub(V a,V b){return Store(a.X-b.X,a.Y-b.Y);}
        private static V Add(V a,V b){return Store(a.X+b.X,a.Y+b.Y);}
        private static V Scale(V v,float scale){return Store(v.X*scale,v.Y*scale);}
        private static float Length(V v){return (float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y);}
        private static V Unit(V v)
        {float length=Length(v);if(length==0 || float.IsNaN(length))return default(V);double inverse=1d/length;return Store((float)(v.X*inverse),(float)(v.Y*inverse));}
        private static V Lerp(V a,V b,float amount){return Store((float)((double)a.X+((double)b.X-a.X)*amount),(float)((double)a.Y+((double)b.Y-a.Y)*amount));}
        internal static bool Known(NpcMotionState n){return n.EffectiveType==636 && n.Style==120;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        private static bool Phase2(NpcMotionState n){return n.A3==1 || n.A3==3;}
        private static bool Genuine(NpcMotionState n){return n.A3==2 || n.A3==3;}
        private static int IntroAlpha(float clock)
        {
            // Native Clamp and the Opacity setter each receive a stored float.
            // A direct x86 float-to-int expression can retain extended precision
            // and truncate an otherwise integral alpha one unit too early.
            float opacity=Store(clock/180f,0).X;
            float alpha=Store((1f-Math.Max(0f,Math.Min(1f,opacity)))*255f,0).X;
            return (int)Math.Max(0f,Math.Min(255f,alpha));
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.A0==13 || n.A0==0 && n.A1+1>=180 || n.A0==1;}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost || n.HasClosestPlayer;}
        private static bool Target(NpcMotionState n,PredictionEnvironment e,out V center)
        {
            bool valid=n.TargetCaptured && n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost;
            center=valid?Store(n.PlayerCenterX,n.PlayerCenterY):default(V);return valid;
        }
        private static bool Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,out PredictionStop stop)
        {NpcTargeting.Face(ref n,ref e,true,confused);stop=n.TargetChoiceUnavailable?PredictionStop.MissingDependency:PredictionStop.None;return stop==PredictionStop.None;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.EmpressFactsCaptured || !n.EmpressRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
            // Main's time update follows NPCs. Both fresh BeforeNpc and a
            // CompletedWorld sample use their captured time for action one.
            // Later actions each consume exactly one observed dayRate step.
            if(n.EmpressClockStarted)
            {n.EmpressClock+=n.EmpressDayRate;if(n.EmpressClock>(n.EmpressDay?54000d:32400d)){n.EmpressDay=!n.EmpressDay;n.EmpressClock=0;}}
            n.EmpressClockStarted=true;
            bool enraged=n.EmpressDay;
            if(e.Remix)
            {
                enraged=n.EmpressRageLatched;
                if(!enraged && n.EmpressFirstIdentity.Token!=null)
                {
                    float firstY=n.EmpressFirstCenterY;
                    if(n.EmpressFirstIdentity.Equals(n.Identity))firstY=Center(n).Y;
                    else if(n.EmpressFirstActive)
                    {
                        bool found=false;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.EmpressFirstIdentity)){firstY=Center(group[i]).Y;found=true;break;}
                        if(!found){stop=PredictionStop.MissingDependency;return false;}
                    }
                    enraged=(double)firstY<e.GravityWorldSurface*16d;
                }
                if(enraged)
                {
                    n.EmpressRageLatched=true;
                    for(int i=0;i<count;i++)if(Known(group[i]))group[i].EmpressRageLatched=true;
                }
            }
            bool second=Phase2(n),expert=e.Expert || enraged,receives=true,fade=true;
            if(n.Life==n.LifeMax && enraged && !Genuine(n))n.A3+=2;
            int faster=(second?15:0)+(expert?5:0);V target;bool valid;
            switch((int)n.A0)
            {
                case 0:
                    if(n.A1==0){n.Vx=0;n.Vy=5;}n.Vx*=.95f;n.Vy*=.95f;n.A1++;
                    fade=receives=false;n.Alpha=IntroAlpha(n.A1);
                    if(n.A1>=180){if(enraged && !Genuine(n))n.A3+=2;n.A0=1;n.A1=0;if(!Query(ref n,ref e,confused,out stop))return false;}
                    break;
                case 1:
                    float wait=second?20:45;if(e.GoodWorld)wait/=2;
                    if(n.A1<=10)
                    {
                        if(n.A1==0 && !Query(ref n,ref e,confused,out stop))return false;
                        if(!Target(n,e,out target)){n.A0=13;n.A1=0;n.A2++;n.Vx/=4;n.Vy/=4;break;}
                        Dash(ref n,target);
                    }
                    if(Length(Store(n.Vx,n.Vy))>16 && n.A1>10){n.Vx/=2;n.Vy/=2;}
                    n.Vx*=.92f;n.Vy*=.92f;n.A1++;
                    if(n.A1<wait)break;
                    int next=Next((int)n.A2,second,expert);if(!second && (float)n.Life/n.LifeMax<=.5f)next=10;
                    if(!Query(ref n,ref e,confused,out stop))return false;
                    valid=Target(n,e,out target);
                    if(!valid || Length(Sub(target,Center(n)))>6400 || Expired(n))next=13;
                    if(next==8 && target.X>Center(n).X)next=9;
                    if(expert && next!=5 && next!=12)
                    {
                        V v=Unit(Unit(Sub(Center(n),target)));float angle=(float)Math.PI/2f*(target.X>Center(n).X?1:-1);
                        double cosine=Math.Cos(angle),sine=Math.Sin(angle);v=Store((float)(v.X*cosine-v.Y*sine),(float)(v.X*sine+v.Y*cosine));v=Scale(v,20);n.Vx=v.X;n.Vy=v.Y;
                    }
                    n.A0=next;n.A1=0;n.A2++;break;
                case 2:Hover(ref n,e,-150,-250,12,.5f);Advance(ref n,150-faster);break;
                case 3:n.A1++;Hover(ref n,e,150,-250,12,.5f);if(n.A1>=120){n.A0=1;n.A1=0;}break;
                case 4:case 11:Hover(ref n,e,0,-350,12,.5f);Advance(ref n,120-faster);break;
                case 5:Hover(ref n,e,0,-350,12,.5f);Advance(ref n,72-faster);break;
                case 6:Hover(ref n,e,-80,-500,12*.3f,.5f*.7f);Advance(ref n,300-faster);break;
                case 7:Hover(ref n,e,0,-350,12*.4f,.5f);Advance(ref n,240+(expert?40:20)-faster);break;
                case 8:case 9:
                    int direction=n.A0==8?-1:1;receives=!(n.A1>=6 && n.A1<=40);
                    if(n.A1<=40){Hover(ref n,e,direction*-550,0,12,1,false);if(n.A1==40){n.Vx*=.3f;n.Vy*=.3f;}}
                    else if(n.A1<=90){V v=Lerp(Store(n.Vx,n.Vy),Store(direction*50,0),.05f);n.Vx=v.X;n.Vy=v.Y;if(n.A1==90){n.Vx*=.7f;n.Vy*=.7f;}}
                    else{n.Vx*=.92f;n.Vy*=.92f;}
                    Advance(ref n,110-faster);break;
                case 10:
                    receives=!(n.A1>=30 && n.A1<=170);n.Vx*=.95f;n.Vy*=.95f;
                    if(n.A1==90){if(n.A3==0)n.A3=1;else if(n.A3==2)n.A3=3;Target(n,e,out target);n.X=target.X-n.Width*.5f;n.Y=target.Y-250-n.Height*.5f;n.NewSegment=true;}
                    n.A1++;if(n.A1>=200-faster){n.A0=1;n.A1=n.A2=0;}break;
                case 12:
                    if(n.A1==0){n.Vx=0;n.Vy=-12;}n.Vx*=.95f;n.Vy*=.95f;Advance(ref n,150-faster);break;
                case 13:
                    if(n.A1==0){n.Vx=0;n.Vy=-7;}n.Vx*=.95f;n.Vy*=.95f;
                    if(!Query(ref n,ref e,confused,out stop))return false;valid=Target(n,e,out target);fade=false;
                    bool leaving=Expired(n) || !valid || Length(Sub(target,Center(n)))>6400;
                    n.Alpha=Math.Max(0,Math.Min(255,n.Alpha+(leaving?5:-5)));n.A1++;
                    if(n.A1>=20 && (n.Alpha==0 || n.Alpha==255))
                    {if(n.Alpha==255){n.Active=false;stop=PredictionStop.Despawn;return false;}n.A0=1;n.A1=0;}
                    break;
                default:stop=PredictionStop.UnsupportedMechanism;return false;
            }
            n.Health.Defense=second?(int)(n.Health.DefaultDefense*1.2f):n.Health.DefaultDefense;n.Health.DontTakeDamage=!receives;n.CanReceive=receives && !n.Health.Immortal;n.CanHarm=!n.Friendly;
            n.L0++;if(n.L0>=44)n.L0=0;if(fade)n.Alpha=Math.Max(0,n.Alpha-5);return true;
        }
        private static bool Expired(NpcMotionState n){return Genuine(n) && (!n.EmpressDay || n.EmpressClock>=53400d);}
        private static int Next(int ordinal,bool second,bool expert)
        {
            if(!second){switch(ordinal%10){case 0:case 5:return 2;case 1:case 3:case 6:case 8:return 8;case 2:return 6;case 7:return 4;default:return 5;}}
            int i=ordinal%(expert?10:9);if(i==0)return 7;if(i==1)return 2;if(i==2)return 8;if(expert){if(i==3)return 11;i--;}
            switch(i){case 3:return 5;case 4:return 2;case 5:return 6;case 6:return 4;case 7:return 8;default:return 12;}
        }
        private static void Advance(ref NpcMotionState n,float end){n.A1++;if(n.A1>=end){n.A0=1;n.A1=0;}}
        private static void Hover(ref NpcMotionState n,PredictionEnvironment e,float x,float y,float speed,float acceleration,bool distanceGate=true)
        {
            V target;if(!Target(n,e,out target))target=Center(n);V delta=Sub(Add(target,Store(x,y)),Center(n));
            if(distanceGate && Length(delta)<=40)return;V v=Scale(Unit(Unit(delta)),speed);
            if(n.Vx<v.X){n.Vx+=acceleration;if(n.Vx<0 && v.X>0)n.Vx+=acceleration;}else if(n.Vx>v.X){n.Vx-=acceleration;if(n.Vx>0 && v.X<0)n.Vx-=acceleration;}
            if(n.Vy<v.Y){n.Vy+=acceleration;if(n.Vy<0 && v.Y>0)n.Vy+=acceleration;}else if(n.Vy>v.Y){n.Vy-=acceleration;if(n.Vy>0 && v.Y<0)n.Vy-=acceleration;}
        }
        private static void Dash(ref NpcMotionState n,V target)
        {
            target=Add(target,Store(0,-300));V delta=Sub(target,Center(n));
            if(Length(delta)>200)target=Sub(target,Scale(Unit(delta),100));
            delta=Sub(target,Center(n));float length=Length(delta),amount=Math.Max(0,Math.Min(1,(length-100)/500));
            V first=Scale(Unit(delta),Math.Min(length,18));V second=Store(delta.X/6,delta.Y/6),v=Lerp(first,second,amount);n.Vx=v.X;n.Vy=v.Y;
        }
    }
}
