using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These AI019 parents keep their original launch direction. Channel only
    // extends the native owner animation; it is never a later mouse consumer.
    internal static class HostJoustingLanceAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type>=877 && type<=879;}
        internal sealed class State
        {
            private readonly Player player;private readonly bool channel;private readonly HostMeleeAttack.SpearFlightState pose;
            internal State(Player player,Projectile[] sources){this.player=player;channel=player.channel;if(sources.Length>0)pose=new HostMeleeAttack.SpearFlightState(player,sources);}
            // An unborn hand already has the enclosing plan's owner/animation
            // lease and its explicit native hand-start transition. Only live
            // sources need the additional captured pose here.
            internal bool Current {get{return player.channel==channel && (pose==null || pose.Current);}}
        }
        internal static float DamageMultiplier(Vector2 ownerVelocity){return .1f+ownerVelocity.Length()/7*.9f;}
        // One original AI019 action, including the post-pose dummy clamp and
        // ai0 kill gate. Movement/Damage happen only if the body survives it.
        internal static bool Ai(Projectile p,Player body)
        {
            p.direction=body.direction;p.spriteDirection=-p.direction;p.alpha=Math.Max(0,p.alpha-40);
            int maximum=body.itemAnimationMax,animation=body.itemAnimation,third=maximum/3;
            float first=Math.Min(animation,third),retraction=third-first,extension=maximum-third-(animation-first);
            float offset=28+.4f*extension-.4f*retraction;
            p.Center=body.RotatedRelativePoint(body.MountedCenter)+p.velocity*offset;
            if(body.channel && animation<third){body.itemAnimation=third;body.itemTime=third;body.itemTimeMax=third+1;}
            p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2+MathHelper.PiOver4;if(p.spriteDirection==-1)p.rotation-=MathHelper.PiOver2;
            p.ai[0]++;return !(p.ai[0]>=maximum && !body.channel);
        }
        private static bool Collides(Projectile p,MotionRect bounds)
        {
            var target=new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height);var limit=new Rectangle((int)p.position.X-150,(int)p.position.Y-150,300,300);if(!limit.Intersects(target))return false;
            float angle=p.rotation-MathHelper.PiOver4-MathHelper.PiOver2-(p.spriteDirection==1?MathHelper.Pi:MathHelper.PiOver2),point=0;
            return Collision.CheckAABBvLineCollision(target.TopLeft(),target.Size(),p.Center,p.Center+angle.ToRotationVector2()*95,23*p.scale,ref point);
        }
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;}
        private static AttackContact Replay(Player player,List<Member> members,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,HostAttackPlayerFrame frame,Vector2 aim,bool newHand)
        {
            int limit=Math.Min(120,path.Count-clock.FirstTick);if(limit<=0)return null;var prefix=new HostAttackPlayerPrefix(frame,terrain,limit+1);var body=(Player)clone.Invoke(player,null);
            if(newHand && body.itemAnimation<=0){body.itemAnimationMax=HostMeleeAttack.AnimationDuration(player,player.HeldItem);body.itemAnimation=body.itemAnimationMax;}
            if(newHand)body.direction=HostBetsyFlameAttack.Facing(player,player.RotatedRelativePoint(player.MountedCenter),aim);
            int animation=body.itemAnimation;
            root.CaptureJoustingOwner(player,newHand?new Projectile[0]:members.ConvertAll(m=>m.Shot).ToArray());
            for(int step=0;step<limit;step++)
            {
                CombatPredictionWork.Check();PredictionPlayerMotion motion;if(!prefix.TryRead(step+(clock.NextWorld?1:0),out motion))return null;HostAttackPlayerFrame.ApplyPose(body,motion);
                if(clock.MovePlayer(step))animation=Math.Max(0,animation-1);body.itemAnimation=animation;
                foreach(var member in members)
                {
                    var p=member.Shot;if(!p.active)continue;
                    if(!member.Receive.BeforeNpc(clock.FirstTick+step,step+1,p.Center.X,p.Center.Y,p.width,p.height))return null;
                    for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                    {
                        var old=p.Center;if(!Ai(p,body)){p.active=false;continue;}animation=body.itemAnimation;
                        if(!HostProjectileEnvironment.Dry(p,terrain,old.X,old.Y,p.velocity.X))return null;p.Center+=p.velocity;
                        int hits=member.Receive.SelectedHits;
                        bool passed=member.Receive.PassArcOuter(2,clock.FirstTick+step,step+1,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true);
                        p.penetrate=member.Receive.Remaining;
                        if(member.Receive.SelectedHits>hits)
                        {
                            var b=path[clock.FirstTick+step].ProjectileReceiveBounds;
                            // The full custom line and native owner gate have
                            // already received this exact target. Publish that
                            // target shape without reinterpreting the shaft as
                            // the projectile's small25px endpoint rectangle.
                            return AttackIntercept.ProjectileShapeContact(path,clock.FirstTick+step,sub,true,aim.X,aim.Y,b.CenterX,b.CenterY,AttackConfidence.Conditional);
                        }
                        if(!passed || --p.timeLeft<=0)p.active=false;
                    }
                }
            }
            return null;
        }
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(!Handles(ammo.Projectile) || root==null || age+1>=path.Count)return null;root.CaptureShotFacing(player);
            var origin=player.RotatedRelativePoint(player.MountedCenter);var directions=new List<Vector2>();
            for(int seed=0;seed<9;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);if(directions.Contains(unit))continue;directions.Add(unit);
                var aim=origin+unit*10000;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.owner=player.whoAmI;p.damage=player.GetWeaponDamage(item);p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.Center=origin;
                root.BeginCandidate();if(p.usesOwnerMeleeHitCD){root.CaptureArcOwner(player,item);if(player.itemAnimation<=0)root.ResetArcMeleeHistory();}
                var clock=new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay);var contact=Replay(player,new List<Member>{new Member{Shot=p,Receive=root.ForkForSpawn(p)}},path,clock,terrain,root,frame,aim,true);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            root.CaptureProjectileFamily(sources);root.CaptureJoustingOwner(player,sources);root.BeginCandidate();var rows=new List<Member>();
            foreach(var source in sources)
            {if(!source.active || !Handles(source.type))return null;var p=HostThornAttack.Copy(source);if(p.usesOwnerMeleeHitCD)root.CaptureArcOwner(player,player.HeldItem);rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p)});}
            return Replay(player,rows,path,clock,terrain,root,HostAttackPlayerFrame.Capture(player,path),sources[0].Center+sources[0].velocity*1000,false);
        }
    }
}
