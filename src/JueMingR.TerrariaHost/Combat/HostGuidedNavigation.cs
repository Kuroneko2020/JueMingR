using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // One registered controller owns one geometry cache. A preview gets a
    // different instance; neither candidates nor Draw advance a live cursor.
    internal sealed class HostGuidedNavigation
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly HostGuidedRoute route=new HostGuidedRoute();
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private PredictionTerrain.CellLease motorLease,routeLease;
        private PredictionPlayerMotion input;
        private PredictionPlayerMotion birthPose;
        private bool usable;
        private bool hasSafePrefix;
        private Vector2 safePoint;
        private PredictionTerrain.CellLease safeMotor,safeRoute;
        private uint preparedTick;
        // The meet of this call's required proposals. Native destruction or
        // a definitely rejected proposal closes that proposal's remainder;
        // unknown dependencies never do. A later required route can still
        // invalidate completion of the whole candidate.
        private int through,attemptThrough;
        internal bool BudgetPaused {get;private set;}
        internal HostGuidedWindow Window {get;private set;}
        internal int Searches {get{return route.Searches;}}
        internal int Reuses {get{return route.Reuses;}}
        internal int Replays {get;private set;}
        internal bool PresentationCurrent(Player player)
        {return usable && input.SameSample(NpcPredictionSource.ReadPlayer(player));}
        internal bool Current(Player player)
        {return PresentationCurrent(player) && motorLease!=null && motorLease.DisplayCurrent(preparedTick==Main.GameUpdateCount) && (routeLease==null || routeLease.DisplayCurrent(preparedTick==Main.GameUpdateCount));}
        internal sealed class Dependencies
        {
            private readonly PredictionTerrain.CellLease motor,geometry;
            private readonly PredictionPlayerMotion input;
            internal Dependencies(HostGuidedNavigation source,Player openingInput)
            {motor=source.motorLease;geometry=source.routeLease;input=openingInput==null?source.input:NpcPredictionSource.ReadPlayer(openingInput);}
            internal bool Current(Player player)
            {return player!=null && input.SameSample(NpcPredictionSource.ReadPlayer(player)) && motor!=null && motor.Current && (geometry==null || geometry.Current);}
        }
        internal Dependencies Freeze(Player openingInput=null){return new Dependencies(this,openingInput);}
        internal AttackContact Prepare(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,HostAttackPlayerFrame frame,out Vector2 point,out bool hasPoint,AttackAmmoSnapshot birthAmmo=null)
        {
            hasSafePrefix=false;safeMotor=safeRoute=null;BudgetPaused=false;preparedTick=Main.GameUpdateCount;
            Window=new HostGuidedWindow(timeline,clock.Phase);through=int.MaxValue;attemptThrough=-1;
            try{return PrepareCore(player,shot,timeline,clock,frame,out point,out hasPoint,birthAmmo);}
            catch(CombatPredictionBudgetExceededException)
            {
                // A completed three-world-step prefix already owns its exact
                // read set. Optional tail exhaustion cannot erase that input
                // or turn it into a damage Contact. No allowance is restarted.
                BudgetPaused=true;Window=new HostGuidedWindow(timeline,clock.Phase,through:Math.Min(through,attemptThrough),budgetPaused:true);point=safePoint;hasPoint=usable=hasSafePrefix;motorLease=safeMotor;routeLease=safeRoute;return null;
            }
        }
        private AttackContact PrepareCore(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,HostAttackPlayerFrame frame,out Vector2 point,out bool hasPoint,AttackAmmoSnapshot birthAmmo)
        {
            usable=hasPoint=false;point=default(Vector2);motorLease=routeLease=null;terrain.Reset();input=NpcPredictionSource.ReadPlayer(player);
            if(!HostGuidedAttack.Weapon(player.HeldItem,shot.type) || !HostGuidedAttack.Consumes(player,shot))return null;
            int limit=Math.Min(120,timeline.Count-clock.FirstTick);if(limit<=0)return null;
            // Extend owner poses only as the motor needs them. Computing the
            // whole optional tail before step zero would spend the allowance
            // before an otherwise executable prefix could even be verified.
            var body=(Player)copy.Invoke(player,null);var positions=new PlayerPoses(frame,clock,terrain,limit);
            if(birthAmmo!=null)
            {
                if(!positions.Try(0,out birthPose))return null;
                HostAttackPlayerFrame.ApplyPose(body,birthPose);int facing;
                shot.Center=HostAttackModels.NativeLaunchOrigin(body,body.HeldItem,HostAttackModels.LaunchOrigin(body,body.HeldItem),shot.Center,out facing);
                bool wet;byte kind;if(!terrain.ProjectileWet(shot.Center.X,shot.Center.Y,shot.width,shot.height,out wet,out kind))return null;
                // NewProjectile samples liquid before the first AI; e.g. a
                // flame born already in water cannot borrow an extra dry step.
                shot.wet=wet && !shot.ignoreWater;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;
            }
            var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            int updates=shot.extraUpdates+1;
            int estimate=Math.Max(1,(int)(Vector2.Distance(shot.Center,new Vector2(timeline[clock.FirstTick].Bounds.CenterX,timeline[clock.FirstTick].Bounds.CenterY))/(shot.type==491?20:32)/updates));
            bool blocked=false,complete=true;Vector2 directPoint=default(Vector2),previousAim=new Vector2(float.NaN);bool directUsable=false;
            foreach(int offset in shot.type==491?KnifeOffsets:MissileOffsets)
            {
                var box=timeline[Math.Min(timeline.Count-1,clock.FirstTick+estimate+offset)].ProjectileReceiveBounds;
                Vector2 candidate=new Vector2(box.CenterX,box.CenterY),first;bool safe,hit,finished;
                if(candidate==previousAim)continue;previousAim=candidate;
                var contact=Replay(player,body,shot,timeline,clock,positions,limit,receive,new[]{candidate},false,out first,out safe,out hit,out finished,birthAmmo);
                through=Math.Min(through,attemptThrough);complete&=finished;blocked|=hit;
                if(safe && !directUsable){directPoint=first;directUsable=true;}
                if(contact!=null){point=first;hasPoint=usable=true;motorLease=terrain.FreezeCells();return contact;}
            }
            // No Contact can mean horizon/immune, not a wall. Keep the cheap
            // native control prefix unless actual motor collision needs a route.
            if(!blocked)
            {Window=new HostGuidedWindow(timeline,clock.Phase,complete,through);point=directPoint;hasPoint=usable=directUsable;motorLease=terrain.FreezeCells();return null;}
            var receiver=timeline[Math.Min(timeline.Count-1,clock.FirstTick+estimate)].ProjectileReceiveBounds;
            attemptThrough=clock.FirstTick-1;
            if(route.Prepare(player,shot,timeline.Identity,receiver))
            {
                bool safe,hit,finished;Vector2 first;
                var contact=Replay(player,body,shot,timeline,clock,positions,limit,receive,route.Points,true,out first,out safe,out hit,out finished,birthAmmo);
                through=Math.Min(through,attemptThrough);complete&=finished;Window=new HostGuidedWindow(timeline,clock.Phase,contact==null && complete,through);
                route.Freeze();routeLease=route.Lease;
                point=first;hasPoint=usable=safe;motorLease=terrain.FreezeCells();return contact;
            }
            // A failed geometric proposal cannot forbid native Shoot. Basic
            // remains the existing fallback; no synthetic Contact is emitted.
            complete&=route.Status==HostGuidedRouteStatus.Blocked;
            if(!complete)through=Math.Min(through,attemptThrough);
            Window=new HostGuidedWindow(timeline,clock.Phase,complete,through);
            motorLease=terrain.FreezeCells();return null;
        }
        private static readonly int[] MissileOffsets={0,1,3,8},KnifeOffsets={0,3,10,25};
        private AttackContact Replay(Player player,Player body,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PlayerPoses positions,int limit,HostAttackReceive receive,IReadOnlyList<Vector2> path,bool navigation,out Vector2 first,out bool safe,out bool blocked,out bool complete,AttackAmmoSnapshot birthAmmo)
        {
            Replays++;attemptThrough=clock.FirstTick-1;first=default(Vector2);safe=blocked=complete=false;if(path.Count==0)return null;
            int at=0,updates=shot.extraUpdates+1;var knife=new HostFlyingKnifeAttack.State(shot);
            Vector2 center=shot.Center,velocity=shot.velocity,position=shot.position;bool wet=shot.wet,honey=shot.honeyWet,lava=shot.lavaWet,shimmer=shot.shimmerWet;
            for(int step=0;step<limit;step++)
            {
                CombatPredictionWork.Check();PredictionPlayerMotion pose;if(!positions.Try(step,out pose))return null;HostAttackPlayerFrame.ApplyPose(body,pose);
                for(int sub=0;sub<updates;sub++)
                {
                    // A future corner may be selected only through a full-body
                    // clear connection. The native motor still checks every
                    // movement substep, including knife inertia/reflections.
                    if(navigation)while(at<path.Count-1)
                    {
                        bool clear=route.Clear(center,path[at+1]);if(route.LastClearUnknown)return null;
                        if(!clear)break;at++;
                    }
                    Vector2 aim=path[at];
                    if(navigation && shot.type==491 && at<path.Count-1 && Vector2.Distance(center,aim)<120)
                        aim=center+(aim-center).SafeNormalize(velocity.SafeNormalize(Vector2.UnitX))*120;
                    aim=HostDirectionalControl.Cursor(body,aim);body.LimitPointToPlayerReachableArea(ref aim);
                    if(step==0 && sub==0)
                    {
                        first=aim;
                        if(birthAmmo!=null)
                        {
                            // These four weapons have direction-independent
                            // mounted origins. Their candidate's FIRST cursor
                            // still determines native birth inertia, so bind it
                            // before verifying any prefix, not in a second solve.
                            int facing;center=HostAttackModels.NativeLaunchOrigin(body,body.HeldItem,HostAttackModels.LaunchOrigin(body,body.HeldItem),aim,out facing);
                            position=center-shot.Size*.5f;velocity=HostAttackModels.NativeLaunchVelocity(body,center,aim,birthAmmo.Speed,facing);
                            knife.Position=position;knife.Velocity=velocity;
                        }
                    }
                    if(shot.type==491)
                    {
                        var previous=knife;
                        if(!HostFlyingKnifeAttack.Step(shot,terrain,ref knife,body.Center,aim,true))return null;
                        if(!knife.Alive){attemptThrough=clock.FirstTick+limit-1;complete=true;return null;}
                        center=knife.Center;velocity=knife.Velocity;
                        // A reflection is real knife behavior, but a straight
                        // proposal which bounces is a reason to query a route.
                        if(knife.Collided)blocked=true;
                        if(navigation && !route.Clear(previous.Center,center)){blocked=!route.LastClearUnknown;complete=blocked;if(complete)attemptThrough=clock.FirstTick+limit-1;return null;}
                    }
                    else
                    {
                        var delta=aim-center;float distance=delta.Length();
                        velocity=distance>=64?delta.SafeNormalize(Vector2.Zero)*Math.Min(32,distance):velocity*.3f+delta*.3f;
                        float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,center.X,center.Y,velocity.X,out wind))return null;velocity.X+=wind;
                        if(!shot.ignoreWater)
                        {
                            bool touching,touchingLava;byte kind;
                            if(!terrain.ProjectileLava(center.X,center.Y,shot.width,shot.height,out touchingLava) || !terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out touching,out kind))return null;
                            if(touchingLava)lava=true;if(touching && kind==2)honey=true;if(touching && kind==3)shimmer=true;
                            // Update checks the previous wet flag before
                            // assigning this action's WetCollision result.
                            // Flame lash dies in water on that native phase;
                            // touching lava instead clears its wet/lava flags.
                            if(wet && !lava && Terraria.ID.ProjectileID.Sets.IsDestroyedByWater[shot.type]){attemptThrough=clock.FirstTick+limit-1;complete=true;return null;}
                            if(shot.type==34 && lava){touching=wet=lava=false;}
                            wet=touching;if(!wet)lava=honey=shimmer=false;
                        }
                        if(shimmer)return null;
                        Vector2 displacement=velocity;bool hit=false;
                        if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref position,ref velocity,wet,honey,out displacement,out hit))return null;
                        if(hit){attemptThrough=clock.FirstTick+limit-1;blocked=complete=true;return null;}
                        position+=wet?displacement:velocity;center=position+shot.Size*.5f;
                    }
                    // A prefix authorizes only this frame's first input. It
                    // does not promise that an uncomputed tail will hit.
                    if(sub==updates-1)attemptThrough=clock.FirstTick+step;
                    if(step>=2 && sub==updates-1 && !safe){safe=true;CapturePrefix(first,navigation);}
                    if(receive.Allows(clock.LaunchDelay+step+1))
                    {
                        var contact=AttackIntercept.BodyContact(timeline,clock.FirstTick+step,sub,first.X,first.Y,center.X,center.Y,shot.width,shot.height,AttackConfidence.Conditional);
                        if(contact!=null){safe=true;return contact;}
                    }
                }
            }
            complete=true;return null;
        }
        private void CapturePrefix(Vector2 point,bool navigation)
        {
            safePoint=point;safeMotor=terrain.FreezeCells();safeRoute=null;
            if(navigation){route.Freeze();safeRoute=route.Lease;}
            hasSafePrefix=true;
        }
        private sealed class PlayerPoses
        {
            private readonly PredictionPlayerMotion[] values;
            private readonly PredictionTerrain terrain;
            private readonly PredictionEnvironment environment;
            private readonly HostAttackClock clock;
            private PredictionPlayerMotion motion;
            private int count;private bool stopped;
            internal PlayerPoses(HostAttackPlayerFrame frame,HostAttackClock clock,PredictionTerrain terrain,int limit)
            {motion=frame.Motion;environment=frame.Environment;this.clock=clock;this.terrain=terrain;values=new PredictionPlayerMotion[limit];}
            internal bool Try(int step,out PredictionPlayerMotion value)
            {
                while(count<=step && !stopped)
                {
                    if(clock.MovePlayer(count)){
                        PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop)){stopped=true;break;}}
                    values[count++]=motion;
                }
                value=step<count?values[step]:default(PredictionPlayerMotion);return step<count;
            }
        }
        internal static Projectile OpeningShot(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,HostAttackClock clock,HostAttackPlayerFrame frame,out Vector2 point,out bool usable,out HostGuidedNavigation navigation,out AttackContact contact)
        {
            point=default(Vector2);usable=false;navigation=null;contact=null;if(ammo==null || !HostGuidedAttack.Weapon(item,ammo.Projectile))return null;
            var shot=HostAttackControl.CopyGuidedProjectile(ammo.Projectile,player.whoAmI);if(shot==null)return null;
            var initial=HostHeldAttack.OpeningPoint(player,item,ammo,timeline);int facing;
            shot.Center=HostAttackModels.NativeLaunchOrigin(player,item,HostAttackModels.LaunchOrigin(player,item),initial,out facing);
            shot.velocity=HostAttackModels.NativeLaunchVelocity(player,shot.Center,initial,ammo.Speed,facing);
            var body=(Player)copy.Invoke(player,null);body.channel=true;
            navigation=new HostGuidedNavigation();contact=navigation.Prepare(body,shot,timeline,clock,frame,out point,out usable,ammo);
            if(usable)
            {
                // Every replay above already used this cursor's actual native
                // birth. Preserve its completed prefix even if the tail paused.
                HostAttackPlayerFrame.ApplyPose(body,navigation.birthPose);
                shot.Center=HostAttackModels.NativeLaunchOrigin(body,item,HostAttackModels.LaunchOrigin(body,item),point,out facing);
                shot.velocity=HostAttackModels.NativeLaunchVelocity(body,shot.Center,point,ammo.Speed,facing);
            }
            navigation.input=NpcPredictionSource.ReadPlayer(player);return shot;
        }
    }
}
