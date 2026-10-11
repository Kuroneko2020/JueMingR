using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI77/78/79, locked Terraria 1.4.5.8. Source owns necessary full identities
    // and effect deadlines; all changes below are private forecast state.
    // Motion and aperture are authoritative. Pupil angle/rotation, dust and
    // shot RNG are deliberately not promoted to future motor prerequisites.
    internal static class NpcMoonLordMotion
    {
        private struct V {internal float X,Y;}
        private static readonly int[][] Phases={new[]{0,1,2,0,3},new[]{1,0,3,0,2},new[]{3,0,2,3,1}};
        private static readonly int[][] Clocks={new[]{50,70,330,60,90},new[]{70,50,90,60,330},new[]{180,30,435,180,375}};
        [MethodImpl(MethodImplOptions.NoInlining)] private static V Store(float x,float y){return new V{X=x,Y=y};}
        private static V Center(NpcMotionState n){return Store(n.X+n.Width*.5f,n.Y+n.Height*.5f);}
        private static V Add(V a,V b){return Store(a.X+b.X,a.Y+b.Y);}
        private static V Sub(V a,V b){return Store(a.X-b.X,a.Y-b.Y);}
        private static V Mul(V a,float k){return Store(a.X*k,a.Y*k);}
        private static float Length(V v){return (float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y);}
        private static bool Unit(V v,out V u)
        {float length=Length(v);if(length==0 || float.IsNaN(length) || float.IsInfinity(length)){u=default(V);return false;}double k=1d/length;u=Store((float)(v.X*k),(float)(v.Y*k));return true;}
        private static V Lerp(V a,V b,float k){return Store(a.X+(b.X-a.X)*k,a.Y+(b.Y-a.Y)*k);}
        // Keep XNA's double Hermite polynomial before the final component
        // store. The original x86 scalar oracle protects this arithmetic.
        private static float Hermite(float a,float b,float t)
        {double x=t,x2=x*x,x3=x2*x;return (float)((2d*a-2d*b)*x3+(3d*b-3d*a)*x2+a);}
        private static V Smooth(V a,V b,float t)
        {t=Math.Max(0,Math.Min(1,t));return Store(Hermite(a.X,b.X,t),Hermite(a.Y,b.Y,t));}
        internal static bool Known(NpcMotionState n)
        {return n.EffectiveType==398 && n.Style==77 || n.EffectiveType==397 && n.Style==78 || n.EffectiveType==396 && n.Style==79;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.EffectiveType==398 && n.L3!=0 && (n.A0==0 || n.A0==1 || (n.A0==-2 || n.A0==-1) && n.A1==59);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return Retargets(n,e);}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.MoonRelationsCaptured || !n.MoonRelationsComplete || !n.MoonEffectsCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(elapsed>=n.MoonExternalEffectAt || elapsed>=n.MoonGlobalRetireAt){stop=PredictionStop.PhaseBoundary;return false;}
            if(n.EffectiveType==398)return Core(ref n,e,group,count,confused,out stop);
            if(!n.PositionOwnerCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(!n.PositionOwnerValid){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            int index=Find(group,count,n.PositionOwner);if(index<0){stop=PredictionStop.MissingDependency;return false;}
            var owner=group[index];if(!owner.Active || owner.EffectiveType!=398){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            return n.EffectiveType==397?Hand(ref n,owner,out stop):Head(ref n,owner,e,elapsed,group,count,out stop);
        }
        private static bool Core(ref NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L3==0){n.L3=1;n.A0=-1;}
            if(n.A0==-2)
            {Receive(ref n,false);if(++n.A1==60){n.A1=0;n.A0=0;if(!e.Multiplayer)n.A2=0;}}
            if(n.A0==-1)
            {
                Receive(ref n,false);if(++n.A1==60)
                {
                    // Server creates three actual new identities then consumes
                    // them in phase0 THIS action. Never assign guessed slots.
                    if(!e.Multiplayer){stop=PredictionStop.MissingDependency;return false;}
                    n.A1=0;n.A0=0;
                }
            }
            if(n.A0==0 || n.A0==1)
            {
                Receive(ref n,n.A0==1);NpcTargeting.Face(ref n,ref e,false,confused);
                if(!n.TargetCaptured || !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
                V center=Center(n),player=Store(e.PlayerX,e.PlayerY),delta=Add(Sub(player,center),Store(0,130));
                if(Length(delta)>20)
                {
                    V desired,adjusted=Sub(delta,Store(n.Vx,n.Vy));
                    // Native Normalize(Zero) produces NaN desired components;
                    // SimpleFlyMovement's comparisons then perform NO writes.
                    // Its final Lerp of the unchanged finite velocity is legal.
                    if(adjusted.X==0 && adjusted.Y==0)desired=Store(float.NaN,float.NaN);
                    else if(!Unit(adjusted,out desired)){stop=PredictionStop.InvalidState;return false;}
                    desired=Mul(desired,8);V old=Store(n.Vx,n.Vy);Fly(ref n.Vx,desired.X,.5f);Fly(ref n.Vy,desired.Y,.5f);V mixed=Lerp(Store(n.Vx,n.Vy),old,.5f);n.Vx=mixed.X;n.Vy=mixed.Y;
                }
                if(n.A0==0 && !e.Multiplayer)
                {
                    int a=Find(group,count,n.MoonPart0),b=Find(group,count,n.MoonPart1),c=Find(group,count,n.MoonPart2);
                    if(a<0 || b<0 || c<0){stop=PredictionStop.MissingDependency;return false;}
                    if(n.L0<0 || n.L1<0 || n.L2<0 || !group[a].Active || group[a].EffectiveType!=397 || !group[b].Active || group[b].EffectiveType!=397 || !group[c].Active || group[c].EffectiveType!=396)
                    {n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
                    if(group[a].A0==-2 && group[b].A0==-2 && group[c].A0==-2){n.A0=1;Receive(ref n,true);}
                }
            }
            else if(n.A0==2 || n.A0==3)
            {
                Receive(ref n,false);V mixed=Lerp(Store(n.Vx,n.Vy),Store(n.A0==2?0:n.Direction,-.5f),.98f);n.Vx=mixed.X;n.Vy=mixed.Y;n.A1++;
                // Native clears every existing type456 at these exact stored
                // clocks, before later NPC slots read the packed anchor key.
                // Earlier clots retain this action's pose; the projectile tail
                // sees the same invalidation and cannot revive that key.
                if(n.A0==2 && n.A1==60 || n.A0==3 && n.A1==40)
                    NpcMoonLeechMotion.ClearAnchors(group,count);
                if(n.A0==2 && n.A1>=600 || n.A0==3 && n.A1>=60)
                {if(n.A0==3)RetireParts(group,count);n.Life=n.A0==2?0:n.Life;n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            else if(n.A0!=-1 && n.A0!=-2){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.A0>=0 && n.A0<2)
            {
                bool alive=n.TargetCaptured && n.PlayerActive && !n.PlayerNativeDead;
                if(!alive)
                {
                    if(e.Players==null || !e.Players.SearchCaptured){stop=PredictionStop.MissingDependency;return false;}
                    for(int i=0;i<e.Players.Count;i++)if(!e.Players.SearchDeadAt(i)){alive=true;break;}
                }
                if(!alive){n.A0=3;n.A1=0;}
            }
            if(!e.Multiplayer && n.A0>=0 && n.A0<2 && Length(Sub(Store(e.PlayerX,e.PlayerY),Center(n)))>2400)
            {
                // Whole necessary group already advances in original slot order.
                // Parts' teleport operands are raw local slots, not AI3 ownership.
                V shift=Sub(Sub(Store(e.PlayerX,e.PlayerY),Store(0,150)),Center(n));n.A0=-2;n.X+=shift.X;n.Y+=shift.Y;
                NpcIdentity[] parts={n.MoonPart0,n.MoonPart1,n.MoonPart2};for(int k=0;k<3;k++){int i=Find(group,count,parts[k]);if(i<0){stop=PredictionStop.MissingDependency;return false;}if(group[i].Active){var p=group[i];p.X+=shift.X;p.Y+=shift.Y;group[i]=p;}}
                // Type400 is permanently nonreceiving and feeds no 77/78/79
                // motor. Its own optional future is not certified by this class.
            }
            return true;
        }
        private static bool Hand(ref NpcMotionState n,NpcMotionState owner,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.MoonHandApertureCaptured || double.IsNaN(n.MoonHandAperture) || double.IsInfinity(n.MoonHandAperture)){stop=PredictionStop.MissingDependency;return false;}
            float side=n.A2==0?-1:1;n.SpriteDirection=(int)side;Receive(ref n,n.MoonHandAperture<21);
            float time=0,duration=0;if(n.A0!=-2)Loop(ref n,n.A2==0?0:1,true,out time,out duration);
            int aperture=0;V body=Center(owner),v=Store(n.Vx,n.Vy),center=Center(n);
            if(n.A0==-2 || n.A0==0)
            {
                if(n.A0==-2){NoContact(ref n);Receive(ref n,false);if(++n.A1>=32)n.A1=0;if(n.A1<0)n.A1=0;}else aperture=3;
                V delta=Sub(Add(body,Store(350*side,-100)),center);if(Length(delta)>20){V unit;if(!Unit(delta,out unit)){stop=PredictionStop.InvalidState;return false;}V old=v,desired=Mul(unit,6);Fly(ref n.Vx,desired.X,.3f);Fly(ref n.Vy,desired.Y,.3f);v=Lerp(old,Store(n.Vx,n.Vy),.5f);}
            }
            else if(n.A0==2)
            {
                V start=Add(Add(Store(220*side,-60),body),Store(side*100,-50)),span=Store(400*side,-60),delta;
                if(time<30){delta=Sub(start,center);if(!Aim(delta,v,8,.2f,true,out v,out stop))return false;}
                else if(time<210){aperture=1;delta=Sub(Smooth(start,Add(start,span),(time-30)/180),center);if(!Aim(delta,v,20,.5f,false,out v,out stop))return false;}
                else if(time<282){v=Mul(v,.9f);}
                else if(time<287){aperture=1;v=Mul(v,.9f);}
                else if(time<292){aperture=2;v=Mul(v,.9f);}
                else if(time<300){aperture=3;delta=Sub(Smooth(start,Add(start,span),1-(time-270)/30),center);if(!Aim(delta,v,14,.1f,false,out v,out stop))return false;}
                else{aperture=3;delta=Sub(start,center);if(!Aim(delta,v,8,.2f,true,out v,out stop))return false;}
            }
            else if(n.A0!=1 && n.A0!=3){stop=PredictionStop.UnsupportedMechanism;return false;}
            // Native clamps predicted CENTER then subtracts velocity before
            // common integration. Preserve each Vector2 component field store.
            n.Vx=v.X;n.Vy=v.Y;V min=Add(Add(Store(220*side,-60),body),Store(side*110,-150)),max=Add(min,Store(side*370,150));if(min.X>max.X){float t=min.X;min.X=max.X;max.X=t;}
            V next=Add(center,v),clamped=Store(Math.Max(min.X,Math.Min(max.X,next.X)),Math.Max(min.Y,Math.Min(max.Y,next.Y)));
            if(next.X!=clamped.X || next.Y!=clamped.Y){V at=Sub(clamped,v);n.X=at.X-n.Width*.5f;n.Y=at.Y-n.Height*.5f;}
            double target=aperture*7;if(target>n.MoonHandAperture)n.MoonHandAperture++;if(target<n.MoonHandAperture)n.MoonHandAperture--;n.MoonHandAperture=Math.Max(0,Math.Min(21,n.MoonHandAperture));return true;
        }
        private static bool Head(ref NpcMotionState n,NpcMotionState owner,PredictionEnvironment e,int elapsed,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;Receive(ref n,n.L3<15);n.Vx=n.Vy=0;V center=Add(Center(owner),Store(0,-400));n.X=center.X-n.Width*.5f;n.Y=center.Y-n.Height*.5f;
            float time,duration;Loop(ref n,2,false,out time,out duration);int aperture=0;
            if(n.A0==-3){NoContact(ref n);Receive(ref n,false);if(++n.A2>=32)n.A2=0;if(n.A2<0)n.A2=0;}
            else if(n.A0==-2)
            {if(owner.A0==2){n.A0=-3;return true;}NoContact(ref n);Receive(ref n,false);if(++n.A2>=32)n.A2=0;if(n.A2<0)n.A2=0;}
            else if(n.A0==0 || n.A0==2)aperture=3;
            else if(n.A0==1){if(time>=duration-15)aperture=3;}
            else if(n.A0!=3){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.A0==2 && !e.Multiplayer && (time==120 || time==180 || time==240))
            {
                // Unknown newly born 401 identity can first run in this same
                // NPC frame; its 90th AI heal is no earlier than f+89. This
                // preserves the known cast prefix without claiming that 401's
                // ProjectileKey(456) interpolation/effects have been modeled.
                int deadline=elapsed+89;n.MoonExternalEffectAt=Math.Min(n.MoonExternalEffectAt,deadline);
                for(int i=0;i<count;i++)if(!group[i].Identity.Equals(n.Identity) && group[i].MoonRelationsCaptured){var p=group[i];p.MoonExternalEffectAt=Math.Min(p.MoonExternalEffectAt,deadline);group[i]=p;}
            }
            // L3 is a real receive aperture. L0/L1 and rotation remain cosmetic
            // and projectile-only. Do not import those into authoritative proof.
            float target=aperture*5;if(target>n.L3)n.L3++;if(target<n.L3)n.L3--;return true;
        }
        private static void Loop(ref NpcMotionState n,int row,bool forcePhase,out float time,out float duration)
        {
            n.A1++;int phase=0,start=0;while(phase<5 && Clocks[row][phase]+start<=n.A1){start+=Clocks[row][phase];phase++;}
            if(phase==5){phase=0;start=0;n.A1=0;}duration=Clocks[row][phase];time=(int)n.A1-start;if(forcePhase || n.A0>=0)n.A0=Phases[row][phase];
        }
        private static bool Aim(V delta,V old,float maximum,float amount,bool smooth,out V result,out PredictionStop stop)
        {stop=PredictionStop.None;result=old;if(delta.X==0 && delta.Y==0)return true;V unit;if(!Unit(delta,out unit)){stop=PredictionStop.InvalidState;return false;}V desired=Mul(unit,Math.Min(maximum,Length(delta)));result=smooth?Smooth(old,desired,amount):Lerp(old,desired,amount);return true;}
        private static void Fly(ref float v,float desired,float acceleration)
        {if(v<desired){v+=acceleration;if(v<0 && desired>0)v+=acceleration;}else if(v>desired){v-=acceleration;if(v>0 && desired<0)v-=acceleration;}}
        private static void Receive(ref NpcMotionState n,bool value){n.CanReceive=value && !n.Health.Immortal;n.Health.DontTakeDamage=!value;}
        private static void NoContact(ref NpcMotionState n){n.CanHarm=false;n.NoContactDamage=true;}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity id)
        {if(group==null || count<0 || count>group.Length || id.Token==null)return -1;for(int i=0;i<count;i++)if(group[i].Identity.Equals(id))return i;return -1;}
        private static void RetireParts(NpcMotionState[] group,int count)
        {for(int i=0;i<count;i++)if(group[i].EffectiveType==396 || group[i].EffectiveType==397 || group[i].EffectiveType==400){var p=group[i];p.Active=false;group[i]=p;}}
    }
}
