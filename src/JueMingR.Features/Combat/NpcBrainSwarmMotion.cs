using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI54's global boss write precedes every AI55 slot. A Brain only counts
    // active Creepers; their orbit geometry is not a dependency of its fade.
    internal static class NpcBrainSwarmMotion
    {
        internal static bool Known(NpcMotionState n){return n.EffectiveType==266 && n.Style==54 || Creeper(n);}
        internal static bool Creeper(NpcMotionState n){return n.EffectiveType==267 && n.Style==55;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return n.Style==54 || !(n.BrainBossCaptured && !n.BrainBossValid) && e.Expert && n.A0!=0;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return n.Style==54;}
        internal static void BeginFrame(NpcMotionState[] group,int count)
        {
            if(group==null)return;
            for(int i=0;i<count;i++)
            {
                var n=group[i];if(!Known(n) || !n.BrainBossCaptured || !n.BrainBossValid)continue;
                int owner=Find(group,count,n.BrainBossIdentity);
                if(owner>=0 && (!group[owner].Active || group[owner].EffectiveType!=266))
                {n.BrainBossValid=false;n.BrainBossIdentity=default(NpcIdentity);group[i]=n;}
            }
        }
        // Health-only Creepers have exactly one AI lifecycle consumer. Their
        // velocity/target choices cannot change the Brain's active count.
        internal static bool LifeOnly(ref NpcMotionState n,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.BrainBossCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(!n.BrainBossValid){n.Active=false;stop=PredictionStop.Despawn;return false;}return true;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.BrainRelationsCaptured || !n.BrainRelationsComplete || !n.BrainBossCaptured || group==null || count<1 || count>group.Length)
            {stop=PredictionStop.MissingDependency;return false;}
            if(Creeper(n))return Swarm(ref n,e,group,count,out stop);
            WriteBoss(ref n,group,count);
            if(n.BrainNetMode!=1 && n.L0==0){stop=PredictionStop.PhaseBoundary;return false;}
            if(n.BrainNetMode!=1)
            {
                NpcTargeting.Face(ref n,ref e,confused:confused);
                if(Math.Abs(n.Bounds.CenterX-e.PlayerX)+Math.Abs(n.Bounds.CenterY-e.PlayerY)>6000)
                {n.Active=false;n.Life=0;stop=PredictionStop.Despawn;return false;}
            }
            if(n.A0<0 && e.GoodWorld)
            {
                // The rolling owner schedules this write after the native NPC
                // slot and supplies its center to the NEXT player action.
                if(!e.GravitySourceCaptured){stop=PredictionStop.MissingDependency;return false;}
            }
            NpcTargeting.Face(ref n,ref e,confused:confused);
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,length=ManualLength(dx,dy);
            if(n.A0<0)
            {
                if(length==0){stop=PredictionStop.InvalidState;return false;}
                n.L2=1;n.Health.DontTakeDamage=false;n.CanReceive=!n.Health.Immortal;
                float factor=8/length;dx*=factor;dy*=factor;n.Vx=(n.Vx*50+dx)/51;n.Vy=(n.Vy*50+dy)/51;
                if(n.A0==-1 && n.BrainNetMode!=1)
                {
                    // Server adds at least30 to the random60..179 threshold.
                    // A hit subtracts an unknown clock amount even before it.
                    if(n.JustHit){stop=PredictionStop.RandomDecision;return false;}
                    if(n.L1+1>=(n.BrainNetMode==0?60:90)){stop=PredictionStop.RandomDestination;return false;}n.L1++;
                }
                else if(n.A0==-2)
                {
                    n.Vx*=.9f;n.Vy*=.9f;n.A3+=n.BrainNetMode==0?25:15;
                    if(n.A3>=255){n.A3=255;n.X=n.A1*16-(int)n.Width/2;n.Y=n.A2*16-(int)n.Height/2;n.A0=-3;n.NewSegment=true;}n.Alpha=(int)n.A3;
                }
                else if(n.A0==-3)
                {n.A3-=n.BrainNetMode==0?25:15;if(n.A3<=0){n.A3=0;n.A0=-1;}n.Alpha=(int)n.A3;}
                else if(n.A0!=-1){stop=PredictionStop.UnsupportedMechanism;return false;}
            }
            else
            {
                float speed=e.GoodWorld?3:1;
                if(length<speed){n.Vx=dx;n.Vy=dy;}else{float factor=speed/length;n.Vx=dx*factor;n.Vy=dy*factor;}
                if(n.A0==0 && n.BrainNetMode!=1)
                {
                    int creepers=0;for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==267)creepers++;
                    if(creepers==0){n.A0=-1;n.L1=0;n.Alpha=0;}
                    if(n.L1+1>=120){stop=PredictionStop.RandomDestination;return false;}n.L1++;
                }
                else if(n.A0==1)
                {n.Alpha+=5;if(n.Alpha>=255){n.Alpha=255;n.X=n.A1*16-(int)n.Width/2;n.Y=n.A2*16-(int)n.Height/2;n.A0=2;n.NewSegment=true;}}
                else if(n.A0==2){n.Alpha-=5;if(n.Alpha<=0){n.Alpha=0;n.A0=0;}}
                else if(n.A0!=0){stop=PredictionStop.UnsupportedMechanism;return false;}
            }
            if(n.PlayerNativeDead || !e.Crimson)
            {if(n.L3<120)n.L3++;if(n.L3>60)n.Vy+=(n.L3-60)*.25f;n.A0=2;n.Alpha=10;}
            else if(n.L3>0)n.L3--;
            if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}return true;
        }
        // Native x86 XNA stores float vector fields after each value operator,
        // but retains the reciprocal between divide/normalize and those final
        // stores. A Debug float reciprocal adds an earlier rounding boundary.
        // Keep the native value-call boundaries and wider reciprocal together;
        // no XNA/live actor dependency is introduced into Features.
        private struct StoredVector {internal float X,Y;}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static StoredVector Multiply(StoredVector v,float amount)
        {return new StoredVector{X=v.X*amount,Y=v.Y*amount};}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static StoredVector Add(StoredVector v,StoredVector other)
        {return new StoredVector{X=v.X+other.X,Y=v.Y+other.Y};}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static StoredVector Divide(StoredVector v,float amount)
        {double reciprocal=1d/amount;return new StoredVector{X=(float)(v.X*reciprocal),Y=(float)(v.Y*reciprocal)};}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Normalize(ref StoredVector v)
        {float root=(float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y);double reciprocal=1d/root;v.X=(float)(v.X*reciprocal);v.Y=(float)(v.Y*reciprocal);}
        private static bool Swarm(ref NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            if(!LifeOnly(ref n,out stop))return false;
            int owner=Find(group,count,n.BrainBossIdentity);if(owner<0){stop=PredictionStop.MissingDependency;return false;}
            var boss=group[owner];float dx=boss.Bounds.CenterX-n.Bounds.CenterX,dy=boss.Bounds.CenterY-n.Bounds.CenterY,length=ManualLength(dx,dy);
            if(n.A0==0)
            {
                n.A1=0;
                if(length>90){float factor=8/length;dx*=factor;dy*=factor;n.Vx=(n.Vx*15+dx)/16;n.Vy=(n.Vy*15+dy)/16;return true;}
                if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<8){n.Vy*=1.05f;n.Vx*=1.05f;}
                if(n.BrainNetMode!=1){stop=PredictionStop.RandomDecision;return false;}return true;
            }
            if(e.Expert)
            {
                var aim=new StoredVector{X=e.PlayerX-n.Bounds.CenterX,Y=e.PlayerY-n.Bounds.CenterY};
                if(aim.X==0 && aim.Y==0){stop=PredictionStop.InvalidState;return false;}
                Normalize(ref aim);aim=Multiply(aim,e.GoodWorld?12f:9f);
                var inertia=Multiply(new StoredVector{X=n.Vx,Y=n.Vy},e.GoodWorld?49f:99f);
                inertia=Divide(Add(inertia,aim),e.GoodWorld?50f:100f);n.Vx=inertia.X;n.Vy=inertia.Y;
            }
            if(length>700)n.A0=0;
            else if(n.JustHit){if(n.BrainKnockBackZero){n.A1++;if(n.A1>5)n.A0=0;}else n.A0=0;}
            return true;
        }
        private static void WriteBoss(ref NpcMotionState n,NpcMotionState[] group,int count)
        {n.BrainBossValid=true;n.BrainBossIdentity=n.Identity;for(int i=0;i<count;i++)if(Known(group[i]) && !group[i].Identity.Equals(n.Identity)){var row=group[i];row.BrainBossCaptured=true;row.BrainBossValid=true;row.BrainBossIdentity=n.Identity;group[i]=row;}}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity identity){for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        private static float ManualLength(float x,float y){float squared=x*x+y*y;return (float)Math.Sqrt(squared);}
    }
}
