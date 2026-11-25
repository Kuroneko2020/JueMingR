using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostAttachedShotAttack
    {
        internal static AttackMotion FlightMotion(Projectile sample,float speed)
        {
            int type=sample.type,damageSize=type==1024?12:0;
            return new AttackMotion(speed,Gravity(type),GravityStart(type),sample.MaxUpdates,sample.width+damageSize,sample.height+damageSize,
                sample.timeLeft,AttackConfidence.Representative,acceleration:Drag(type),accelerationStart:GravityStart(type),horizontalAccelerationOnly:true,maxFallSpeed:type==1024?12:0);
        }

        // Native mode 3 checks retained slots too. A mode-0 fallback can
        // still be consumed only while the real hand will emit a new card.
        internal static bool HasOutstandingCard(Projectile[] shots,int owner)
        {
            int count=Math.Min(shots.Length,Main.maxProjectiles);
            for(int i=0;i<count;i++)
            {
                var shot=shots[i];
                if(shot.type==1024 && shot.owner==owner && shot.ai[0]!=2)return true;
            }
            return false;
        }

        // A new hand owns one legal first-flight channel. Attachment, slot
        // ordering, recall, derived Damage and observed siblings stay with
        // Solve/KnownFamily. No copied projectile enters the live world.
        internal static HostAttackSolution PrepareHand(Player player,Player liveOwner,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,out HostAttackObstacles root,HostAttackPlayerFrame? preparedPlayer)
        {
            root=null;
            int first=clock.FirstTick;
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];
            // Native recall can deal immediate damage from an already born
            // card even if there is no later first-flight sample or new damage.
            if(sample.type==1024 && ammo.CardFireType==3 && clock.Age>=0 && clock.Age<path.Count)
            {
                var family=Observe(liveOwner);
                // Native Shoot checks retained slots without testing active.
                // If all are already returning (or absent), it fires mode 0.
                if(HasOutstandingCard(family,liveOwner.whoAmI))
                {
                    root=new HostAttackObstacles(liveOwner,sample,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
                    root.CaptureOwnedProjectileFamily(family,liveOwner.whoAmI,sceneTypes);
                    var recallSample=Copy(sample);recallSample.owner=liveOwner.whoAmI;recallSample.damage=ammo.Damage;
                    var group=Capture(liveOwner,path,terrain,root,origin,family,root.CaptureAttachmentTargets(recallSample),root.CaptureBirthSlots());
                    AttackContact recall;
                    // The real owner holds the resource/receive leases. After
                    // cooldown, return flight reads the already advanced pose,
                    // including its motion token and native gravity ordinal.
                    if(TryRecall(group,player,family,preparedPlayer,clock.Age,clock.BeforeNpc,clock.LaunchDelay,out recall))
                        return HostAttackSolution.FromContact(AttackIntercept.WithLaunchDelay(recall,clock.LaunchDelay));
                }
            }
            if(first<0 || first>=path.Count || ammo.Damage<=0)return default(HostAttackSolution);
            var motion=FlightMotion(sample,ammo.Speed);float scale;
            if(HostProjectileEnvironment.Factor(sample,terrain,origin.X,origin.Y,out scale))motion=motion.WithEnvironment(scale,0);
            bool card=sample.type==1024,blocked=false,unknown=false;
            // Forward is legal in both single and reverse modes. In the fan
            // the near-axis third card can legally have either native sign.
            // Only that representative is sought, never a fabricated fan.
            float lane=card && ammo.CardFireType==2?-(float)Math.PI/40:0;
            for(int pattern=0;pattern<(card?2:1);pattern++)
            {
                float curve=card?(pattern==0?-.4f:.4f):0;
                var proposals=card?CardDirections(path,first,origin,motion,curve):HostMotionAttackInputs.Generate(origin,motion,path,first-1);
                if(proposals.Length==0)unknown=true;
                foreach(var direction in proposals)
                {
                    CombatPredictionWork.Check();
                    var aim=origin+direction.RotatedBy(-lane)*10000;
                    int facing;
                    var birth=HostAttackModels.NativeLaunchOrigin(player,item,origin,aim,out facing);
                    var velocity=HostAttackModels.NativeLaunchVelocity(player,birth,aim,ammo.Speed,facing).RotatedBy(lane);
                    int receive;
                    var result=ReplayHand(sample,path,first,terrain,birth,velocity,curve,out receive);
                    if(result==HostAttackReachability.Reachable)
                        return new HostAttackSolution(path,aim,true,result,receive,directionIndependent:true,pointSemantics:AttackPointSemantics.Direction);
                    blocked|=result==HostAttackReachability.Blocked;
                    unknown|=result==HostAttackReachability.Unknown;
                }
            }
            // Only a checked path can lend this point. A finite search which
            // yielded no sufficiently supported route keeps the honest Basic
            // fallback. AI's existing blocked path can withhold native use.
            return new HostAttackSolution(path,Vector2.Zero,false,blocked && !unknown?HostAttackReachability.Blocked:HostAttackReachability.Unknown);
        }

        private static HostAttackReachability ReplayHand(Projectile sample,NpcTrajectory path,int first,PredictionTerrain terrain,Vector2 birth,Vector2 velocity,float curve,out int receive)
        {
            receive=-1;
            var state=new State(sample,birth,velocity,curve);
            for(int tick=first;tick<path.Count && state.Alive;tick++)
                for(int sub=0;sub<sample.MaxUpdates && state.Alive;sub++)
                {
                    CombatPredictionWork.Check();
                    if(!Step(sample,terrain,ref state,null,null,true,false))return HostAttackReachability.Unknown;
                    // A settled card and Daybreak's death burst have a real
                    // lifecycle, but neither is this fresh airborne channel.
                    if(!state.Alive || state.Phase!=0)return HostAttackReachability.Blocked;
                    var box=path[tick].ProjectileReceiveBounds;
                    if(path[tick].CanReceive && Collides(sample.type,DamageBox(sample.type,state.Position,sample.width,sample.height),box))
                    {receive=tick;return HostAttackReachability.Reachable;}
                    Finish(ref state);
                }
            return HostAttackReachability.Unknown;
        }

        private struct CardProposal
        {
            internal float Error;
            internal Vector2 Direction;
        }

        // A card's bend is perpendicular to its current velocity. Run the
        // same air motor at two nearby angles once across the finite prefix.
        // Their angular derivative proposes center directions without a full
        // terrain replay at every tick. Exact integer input and native tile
        // and receive geometry are checked only on distinct best directions.
        private static Vector2[] CardDirections(NpcTrajectory path,int first,Vector2 origin,AttackMotion motion,float curve)
        {
            var initial=path[first].ProjectileReceiveBounds;
            var axis=(new Vector2(initial.CenterX,initial.CenterY)-origin).SafeNormalize(Vector2.UnitX);
            const float angleStep=.02f;
            var forward=axis*motion.Speed;var side=forward.RotatedBy(angleStep);
            Vector2 a=Vector2.Zero,b=Vector2.Zero;
            var ranked=new List<CardProposal>();
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(path.Count-first));
            for(int k=1;k<=limit;k++)
            {
                if((k&15)==1)CombatPredictionWork.Check();
                float rotation;
                AdvanceFlight(1024,k,curve,ref forward,out rotation);
                AdvanceFlight(1024,k,curve,ref side,out rotation);
                a+=forward*motion.MoveScale;b+=side*motion.MoveScale;
                var point=path[first+motion.OuterAtSubupdate(k)-1];
                if(!point.CanReceive)continue;
                var box=point.ProjectileReceiveBounds;
                var wanted=new Vector2(box.CenterX,box.CenterY)-origin-a;
                var tangent=(b-a)/angleStep;
                float denominator=tangent.LengthSquared();
                if(denominator<1)continue;
                float offset=MathHelper.Clamp(Vector2.Dot(wanted,tangent)/denominator,-.35f,.35f);
                float error=(wanted-offset*tangent).LengthSquared();
                float margin=Math.Max(box.Width+motion.Width,box.Height+motion.Height);
                if(error<=margin*margin*.25f)
                    ranked.Add(new CardProposal{Error=error,Direction=axis.RotatedBy(offset)});
            }
            ranked.Sort((left,right)=>left.Error.CompareTo(right.Error));
            var result=new List<Vector2>(4);
            foreach(var row in ranked)
            {
                bool duplicate=false;
                foreach(var prior in result)if(Vector2.Dot(prior,row.Direction)>.99998f){duplicate=true;break;}
                if(!duplicate)result.Add(row.Direction);
                if(result.Count==4)break;
            }
            return result.ToArray();
        }
    }
}
