using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI99 release copies the parent's velocity and does not read a cursor.
    // Prepare the last legal held direction for next-action free flight; the
    // detached child is never lent input. Frequency does not change gravity.
    internal static class HostYoyoReleaseAttack
    {
        private static readonly object preparationKey=new object();
        private sealed class DirectionPreparation
        {
            internal Player Player;internal Item Item;internal int Type,Prefix,Direction;
            internal PredictionPlayerMotion Motion;internal Vector2 Start,Point;
            internal float LaunchSpeed,MeleeSpeed,Range,Speed;internal bool String;
            internal HostAttackClock Clock;internal NpcTrajectory Future;
            internal bool Matches(Player player,Item item,float launchSpeed,Vector2 start,float range,float speed,NpcTrajectory future,HostAttackClock clock)
            {return ReferenceEquals(Player,player) && ReferenceEquals(Item,item) && Type==item.type && Prefix==item.prefix && Direction==player.direction && Motion.SameSample(NpcPredictionSource.ReadPlayer(player)) && Start==start && LaunchSpeed==launchSpeed && MeleeSpeed==player.meleeSpeed && String==player.yoyoString && Range==range && Speed==speed && Clock.Phase==clock.Phase && Clock.Age==clock.Age && Clock.LaunchDelay==clock.LaunchDelay && HostAttackAim.SolvePreparation.Extends(Future,future);}
        }
        internal static bool TryPoint(Player player,Item item,float launchSpeed,NpcTrajectory future,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point)
        {
            point=Vector2.Zero;int first=clock.FirstTick;
            if(first<0 || first+2>=future.Count || launchSpeed<=0)return false;
            var start=player.RotatedRelativePoint(player.MountedCenter);
            float multiplier=(1+player.meleeSpeed*3)/4;
            float speed=ProjectileID.Sets.YoyosTopSpeed[item.shoot]/multiplier;
            float range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];
            if(player.yoyoString)range=range*1.25f+30;range/=multiplier;
            if(range<=1 || speed<=0)return false;
            float inertia=Math.Max(1,14-speed/2),heldSpeed=launchSpeed>speed?launchSpeed*.98f:launchSpeed;
            float inherited=(heldSpeed*(inertia-1)+Math.Min((range-1)/2,speed))/inertia;
            Projectile sample=ContentSamples.ProjectilesByType[item.shoot];
            // This is a pure direction proposal retained only on the current
            // synchronous preparation stack. Recheck complete input/phase and
            // old receive prefix; terrain and Preview permission remain fresh.
            var scope=HostAttackAim.SolvePreparation.Current;
            var previous=scope?.Read(preparationKey) as DirectionPreparation;
            if(previous!=null && previous.Matches(player,item,launchSpeed,start,range,speed,future,clock))
            {
#if DEBUG
                ResponseReuses++;
#endif
                point=previous.Point;var unit=(point-start).SafeNormalize(Vector2.UnitX*player.direction);
                return terrain.ProjectilePassage(start.X,start.Y,start.X+unit.X*inherited,start.Y+unit.Y*inherited,sample.width,sample.height);
            }
            int horizon=future.Count-first-1;
            // Coarse windows keep both early/late arcs. Cheap gravity response
            // chooses a time in each window; only those directions receive a
            // full discrete scan. Each scan compares ALL absolute receive times,
            // so increasing H costs O(K*H), not five restarts at every time.
            var times=new int[8];var windows=new int[8];var ranks=new float[8];int count=0;
            for(int window=0;window<8;window++)
            {
                if(!CombatPredictionWork.TryContinue())break;
                int low=2+(horizon-1)*window/8,high=2+(horizon-1)*(window+1)/8;
                float rank=float.MaxValue;int chosen=-1;
                for(int time=low;time<high && time<=horizon;time++)
                {
                    var target=future[first+time].ProjectileReceiveBounds;
                    var delta=new Vector2(target.CenterX,target.CenterY)-start;
                    float vy=(delta.Y-.15f*time*(time+1))/(time+1);
                    int accelerating=Math.Min(time,Math.Max(0,(int)Math.Floor((3-vy)/.3f)));
                    float horizontal=.1f*(accelerating*(time+1)-accelerating*(accelerating+1)*.5f);
                    // Include the below-cap horizontal response in the cheap
                    // rank. Both speed caps and branch changes still require
                    // the discrete candidate replay.
                    var required=new Vector2((delta.X-Math.Sign(delta.X)*horizontal)/(time+1),vy);
                    float error=Math.Abs(required.LengthSquared()-inherited*inherited);
                    if(error<rank){rank=error;chosen=time;}
                }
                if(chosen>=0){int at=count++;while(at>0 && ranks[at-1]>rank){times[at]=times[at-1];windows[at]=windows[at-1];ranks[at]=ranks[at-1];at--;}times[at]=chosen;windows[at]=window;ranks[at]=rank;}
            }
            var response=new ResponseSearch{Start=start,Inherited=inherited,Speed=speed,Range=range,Future=future,First=first,Horizon=horizon,Direction=player.direction,Width=sample.width,Height=sample.height,Best=ResponseCandidate.Empty,Canonical=new CanonicalDirection[horizon+1]};
            bool more=true;
            // A direction passing near receive time T is not the canonical
            // direction aimed at T. Follow that nomination before neighbours;
            // otherwise a migrating nearest time is skipped while an already
            // evaluated neighbour consumes its slot. Each band keeps the same
            // five-probe allowance, with cycles/repeated times removed.
            var tried=new int[5];
            for(int i=0;i<count && more;i++)
            {
                if(response.Best.Error<16 && response.Best.Time==2)break;
                int low=2+(horizon-1)*windows[i]/8,high=2+(horizon-1)*(windows[i]+1)/8;
                int used=0,time=times[i];ResponseCandidate nearest=ResponseCandidate.Empty;
                while(more && used<tried.Length && time>=2 && time<=horizon && Array.IndexOf(tried,time,0,used)<0)
                {
                    tried[used++]=time;more=response.Probe(time,true,low,high,out nearest);
                    if(nearest.Error<16)break;
                    time=nearest.Time;
                }
                if(more && nearest.Error>=16 && nearest.Time>=2)
                {
                    // Once the nomination settles or cycles, inspect both
                    // sides of that fixed anchor using remaining unique slots.
                    // A difficult band may exhaust its allowance; it cannot
                    // turn an unbounded local search into a budget workaround.
                    int anchor=nearest.Time;
                    for(int side=-1;side<=1 && more && used<tried.Length && nearest.Error>=16;side+=2)
                    {time=anchor+side;if(time<2 || time>horizon || Array.IndexOf(tried,time,0,used)>=0)continue;tried[used++]=time;more=response.Probe(time,true,low,high,out nearest);}
                }
            }
            // A band's direction can win at a receive OUTSIDE that band's
            // window. Refine that global boundary on both sides as well.
            // When no legal proposal exists yet, retain the original two
            // same-time cap corrections instead of probing an unproved hit.
            bool contact=response.Best.Bound<=4;int finalAnchor=response.Best.Time;
            for(int correction=0;correction<2 && more && response.Best.Error>=16 && response.Best.Time>=2;correction++)
            {ResponseCandidate nearest;more=response.Probe(contact?finalAnchor+(correction==0?-1:1):response.Best.Time,contact,2,horizon+1,out nearest);}
            var best=response.Best;
            if(best.Bound>4)return false;
            point=best.Point;
            scope?.Store(preparationKey,new DirectionPreparation{Player=player,Item=item,Type=item.type,Prefix=item.prefix,Direction=player.direction,Motion=NpcPredictionSource.ReadPlayer(player),Start=start,Point=point,LaunchSpeed=launchSpeed,MeleeSpeed=player.meleeSpeed,String=player.yoyoString,Range=range,Speed=speed,Clock=clock,Future=future});
            return terrain.ProjectilePassage(start.X,start.Y,start.X+best.Unit.X*inherited,start.Y+best.Unit.Y*inherited,sample.width,sample.height);
        }
        private struct ResponseCandidate
        {
            internal float Error,Bound;internal int Time;internal Vector2 Unit,Point;
            internal static ResponseCandidate Empty {get{return new ResponseCandidate{Error=float.MaxValue,Bound=float.MaxValue};}}
        }
        private struct CanonicalDirection {internal bool Ready;internal Vector2 Unit;}
        // Stack-owned scratch for at most 8*(coarse + two nearest + two grazing
        // neighbours) + two global corrections. Every probe is linear in H;
        // no receive-time loop restarts another whole prefix solve.
        private struct ResponseSearch
        {
            internal Vector2 Start;internal float Inherited,Speed,Range;
            internal NpcTrajectory Future;internal int First,Horizon,Direction,Width,Height;
            internal ResponseCandidate Best;
            // Different coarse bands/neighbours can nominate the same time.
            // Its canonical direction is pure in this one fixed input; retain
            // only the completed four-refinement result, never a permission.
            internal CanonicalDirection[] Canonical;
            internal bool Probe(int time,bool canonical,int low,int high,out ResponseCandidate nearest)
            {
                nearest=ResponseCandidate.Empty;
                if(time<2 || time>Horizon)return true;
                if(!CombatPredictionWork.TryContinue())return false;
                var target=Future[First+time].ProjectileReceiveBounds;
                var goal=new Vector2(target.CenterX,target.CenterY);
                var cached=canonical?Canonical[time]:default(CanonicalDirection);
                var unit=cached.Ready?cached.Unit:canonical?(goal-Start-new Vector2(0,.15f*time*(time+1))).SafeNormalize(Vector2.UnitX*Direction):Best.Unit;
                for(int refine=0;!cached.Ready && refine<4;refine++)
                {
                    Vector2 end;if(!End(Start,unit,Inherited,Speed,time,out end))return false;
                    unit=(unit+(goal-end)/Math.Max(1,Inherited*(time+1))).SafeNormalize(unit);
                }
                if(canonical && !cached.Ready)Canonical[time]=new CanonicalDirection{Ready=true,Unit=unit};
                // The native hand consumes this world-space cursor, whose
                // float quantization matters at large world coordinates.
                // Score its actual direction, and return that exact point;
                // an unrepresentable improvement is not a better launch.
                var proposal=Start+unit*(Range-1);
                unit=(proposal-Start).SafeNormalize(unit);
                var velocity=unit*Inherited;var actual=Start+velocity;
                for(int step=1;step<=Horizon;step++)
                {
                    // Once a precise direction exists, later precise receives
                    // cannot win the native earliest-receive preference.
                    if(Best.Error<16 && step>Best.Time)break;
                    if((step&15)==1 && !CombatPredictionWork.TryContinue())return false;
                    Advance(ref velocity,unit,Speed);actual+=velocity;
                    if(step<2)continue;
                    var receiver=Future[First+step].ProjectileReceiveBounds;var center=new Vector2(receiver.CenterX,receiver.CenterY);
                    float error=Vector2.DistanceSquared(actual,center);
                    float dx=Math.Max(0,Math.Abs(actual.X-center.X)-(receiver.Width+Width)/2),dy=Math.Max(0,Math.Abs(actual.Y-center.Y)-(receiver.Height+Height)/2);
                    var current=new ResponseCandidate{Error=error,Bound=dx*dx+dy*dy,Time=step,Unit=unit,Point=proposal};
                    if(step>=low && step<high && error<nearest.Error)nearest=current;
                    if(Best.Error<16 ? error>=16 || step>Best.Time || step==Best.Time && error>=Best.Error : error>=Best.Error)continue;
                    Best=current;
                    if(error<16)break;
                }
                return true;
            }
        }
#if DEBUG
        internal static int ResponseSteps,ResponseReuses;
#endif
        private static void Advance(ref Vector2 velocity,Vector2 unit,float speed)
        {
#if DEBUG
            ResponseSteps++;
#endif
            velocity.Y+=.3f;
            if(velocity.LengthSquared()>256)velocity=velocity.SafeNormalize(Vector2.Zero)*16;
            else if(velocity.Y<=3)
            {
                float top=Math.Min(16,speed);
                if(Math.Abs(velocity.X)<.1f)velocity.X+=unit.X<0?-.1f:.1f;
                else if(velocity.X>0 && velocity.X<top)velocity.X+=.1f;
                else if(velocity.X<0 && velocity.X>-top)velocity.X-=.1f;
            }
        }
        private static bool End(Vector2 start,Vector2 unit,float inherited,float speed,int time,out Vector2 position)
        {
            var velocity=unit*inherited;position=start+velocity;
            for(int i=0;i<time;i++)
            {
                if((i&15)==0 && !CombatPredictionWork.TryContinue())return false;
                Advance(ref velocity,unit,speed);position+=velocity;
            }
            return true;
        }
        // Clear-line opening uses the same release seed, never the held rope
        // boundary. Only a dry, unobstructed representative child is proved;
        // native bounce/liquid/family uncertainty remains an unknown preview.
        internal static AttackContact Preview(Player player,Item item,float launchSpeed,Projectile child,NpcTrajectory future,HostAttackClock clock,PredictionTerrain terrain,out HostAttackObstacles obstacles)
        {
            obstacles=null;Vector2 point;
            if(!TryPoint(player,item,launchSpeed,future,clock,terrain,out point))return null;
            float multiplier=(1+player.meleeSpeed*3)/4,speed=ProjectileID.Sets.YoyosTopSpeed[item.shoot]/multiplier;
            float range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];if(player.yoyoString)range=range*1.25f+30;range/=multiplier;
            float inertia=Math.Max(1,14-speed/2),heldSpeed=launchSpeed>speed?launchSpeed*.98f:launchSpeed;
            float inherited=(heldSpeed*(inertia-1)+Math.Min((range-1)/2,speed))/inertia;
            var start=player.RotatedRelativePoint(player.MountedCenter);var unit=(point-start).SafeNormalize(Vector2.UnitX*player.direction);
            if(!HostProjectileEnvironment.Dry(child,terrain,start.X,start.Y,unit.X*inherited))return null;
            var position=start+unit*inherited;var velocity=unit*inherited;child.ai[0]=-2;
            if(child.penetrate<0)child.penetrate=3; // Native 3..6 RNG: prove only its minimum.
            var receive=HostAttackReceive.Capture(player,child,future.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            obstacles=new HostAttackObstacles(player,child,future,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            // A first held impact can change immunity before the split. This
            // finite opening does not promise a second hit through that stage.
            if(!obstacles.PassCustomOuter(1,clock.FirstTick,clock.LaunchDelay+1,position.X,position.Y,child.width,child.height,null,false,0,true) || obstacles.SelectedHits>0 || obstacles.OtherHits>0)return null;
            for(int time=1;clock.FirstTick+time<future.Count && time<child.timeLeft;time++)
            {
                int tick=clock.FirstTick+time,outer=clock.LaunchDelay+time+1;
                if(time>1 && !obstacles.BeforeNpc(tick,outer,position.X,position.Y))return null;
                // The near-vertical branch reads the future owner's side;
                // this opening has no continuing owner-side proof for it.
                if(Math.Abs(velocity.X)<.1f && velocity.Y+.3f<=3)return null;
                velocity.Y+=.3f;
                if(velocity.LengthSquared()>256)velocity=velocity.SafeNormalize(Vector2.Zero)*16;
                else if(velocity.Y<=3)
                {
                    float top=Math.Min(16,speed);
                    if(Math.Abs(velocity.X)<.1f)velocity.X+=unit.X<0?-.1f:.1f;
                    else if(velocity.X>0 && velocity.X<top)velocity.X+=.1f;
                    else if(velocity.X<0 && velocity.X>-top)velocity.X-=.1f;
                }
                var next=position+velocity;
                if(!HostProjectileEnvironment.Dry(child,terrain,position.X,position.Y,velocity.X) || !terrain.ProjectilePassage(position.X,position.Y,next.X,next.Y,child.width,child.height))return null;
                position=next;
                if(!obstacles.PassOuter(time+1,tick,outer,position.X,position.Y))return null;
                var contact=receive.Allows(outer) && obstacles.SelectedAllows(outer)?AttackIntercept.BodyContact(future,tick,0,point.X,point.Y,position.X,position.Y,child.width,child.height,AttackConfidence.Representative):null;
                if(contact!=null)return contact;
            }
            return null;
        }
    }
}
