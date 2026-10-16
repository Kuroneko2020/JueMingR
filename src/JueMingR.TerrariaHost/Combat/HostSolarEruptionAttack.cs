using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Solar Eruption is AI075, not an AI019 glaive. Its real OnHit can
    // create a finite AI117 burst at another receiver. Both types write
    // owner4 AND local6 history; descendants share those native writes.
    internal static class HostSolarEruptionAttack
    {
        internal static bool Handles(int type){return type==611;}
        internal static bool Family(int type){return type==611 || type==612;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random=false)
        {return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=random};}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && (p.type==611 || p.type==612))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{611,612});return scene;}
        internal static bool Parent(Projectile p,Player body)
        {
            if(body.dead || body.mount.Active && body.mount.Type>=0 && MountID.Sets.DontHoldItems[body.mount.Type] || p.ai[0]+1>=30){p.active=false;p.timeLeft=0;return false;}
            if(p.localAI[1]>0)p.localAI[1]--;p.alpha=Math.Max(0,p.alpha-42);HostMeleeAttack.SolarPose(p,body,Vector2.Zero,0);p.timeLeft=2;return true;
        }
        internal static bool Child(Projectile p)
        {
            p.ai[1]+=.01f;p.scale=p.ai[1];if(++p.ai[0]>=3*Main.projFrames[p.type]){p.active=false;p.timeLeft=0;return false;}
            if(++p.frameCounter>=3){p.frameCounter=0;if(++p.frame>=Main.projFrames[p.type])p.hide=true;}p.alpha=Math.Max(0,p.alpha-63);
            if(p.ai[0]==1){var center=p.Center;p.width=p.height=(int)(52*p.scale);p.Center=center;}return true;
        }
        internal static Projectile Birth(Projectile parent,Vector2 center)
        {var p=new Projectile();p.SetDefaults(612);p.active=true;p.owner=parent.owner;p.Center=center;p.velocity=Vector2.Zero;p.damage=parent.damage;p.knockBack=10;p.ai[1]=1.425f;return p;}
        private static bool Collides(Projectile p,MotionRect b)
        {float point=0;return Collision.CheckAABBvLineCollision(new Vector2(b.X,b.Y),new Vector2(b.Width,b.Height),p.Center,p.Center+p.velocity+p.velocity.SafeNormalize(Vector2.Zero)*48,16*p.scale,ref point);}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostNebulaBlazeAttack.Copy(p),root,sources!=null && Array.IndexOf(sources,p)>=0));return rows;}
        private static bool Outer(Player body,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var row in rows)if(row.Shot.active && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height))return false;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!(p.type==611?Parent(p,body):Child(p))){slots.Release(p.whoAmI);break;}
                    if(!HostProjectileEnvironment.Dry(p,terrain,p.Center.X,p.Center.Y,p.velocity.X))return false;
                    bool failed=false;int hits=m.Receive.SelectedHits;
                    Func<int,int,bool> onHit=(slot,remaining)=>
                    {
                        if(p.type!=611)return true;
                        if(p.localAI[1]<=0)
                        {
                            int born;if(!slots.TryAllocate(out born)){failed=true;return false;}var box=slot==path.Identity.Slot?path[tick].Bounds:new MotionRect(Main.npc[slot].position.X,Main.npc[slot].position.Y,Main.npc[slot].width,Main.npc[slot].height);var q=Birth(p,new Vector2(box.CenterX,box.CenterY));q.whoAmI=born;rows.Add(Make(q,root,m.Source,true));
                        }
                        p.localAI[1]=4;return true;
                    };
                    bool pass=p.type==611?m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true,onHit):m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);
                    p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits)
                    {
                        var box=path[tick].ProjectileReceiveBounds;contact=p.type==611?AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,box.CenterX,box.CenterY,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional):AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;
                    }
                    // A pool/descendant failure is after the parent's Strike.
                    // Only an unresolved later receiver depends on that tail.
                    if(failed)return false;
                    if(!pass || --p.timeLeft<=0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return true;
        }
        private static AttackContact Replay(Player player,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,HostAttackPlayerFrame frame,Vector2 aim)
        {
            var body=HostMeleeAttack.PlayerCopy(player);var prefix=new HostAttackPlayerPrefix(frame,terrain,121);
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {PredictionPlayerMotion owner;if(!prefix.TryRead(action+(clock.NextWorld?1:0),out owner))return null;HostAttackPlayerFrame.ApplyPose(body,owner);AttackContact contact;if(!Outer(body,rows,slots,path,clock.FirstTick+action,clock.LaunchDelay+action+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.Age<0 || clock.LaunchDelay!=0)return null;root.CaptureHomingInputs(player,false,true);root.CaptureSolarOwner(player,sources);var scene=Capture(player,root);root.BeginCandidate();return Replay(player,Copy(scene,sources,root),root.CaptureBirthSlots().Copy(),path,clock,terrain,root,HostAttackPlayerFrame.Capture(player,path),sources[0].Center+sources[0].velocity*1000);
        }
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;root.CaptureHomingInputs(player,false,true);root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var origin=player.RotatedRelativePoint(player.MountedCenter);
            for(int seed=0;seed<9;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+seed*3)].ProjectileReceiveBounds;var aim=origin+(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction)*10000;root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();
                // The prepared owner is already at the hand's future frame.
                // Older family members still act during its pre-launch wait.
                bool valid=true;if(delay>0){var live=Main.player[player.whoAmI];var prior=HostAttackPlayerFrame.Capture(live,path);var prefix=new HostAttackPlayerPrefix(prior,terrain,delay+1);var body=HostMeleeAttack.PlayerCopy(live);for(int wait=0;wait<delay;wait++){PredictionPlayerMotion motion;if(!prefix.TryRead(wait+(beforeNpc?0:1),out motion)){valid=false;break;}HostAttackPlayerFrame.ApplyPose(body,motion);AttackContact ignored;if(!Outer(body,rows,slots,path,age-delay+wait+1,wait+1,terrain,root,aim,out ignored)){valid=false;break;}}}if(!valid)continue;
                int born;if(!slots.TryAllocate(out born))return null;var p=new Projectile();p.SetDefaults(611);p.active=true;p.owner=player.whoAmI;p.whoAmI=born;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.direction=p.velocity.X<0?-1:1;p.damage=ammo.Damage;p.knockBack=item.knockBack;p.ai[1]=0;rows.Add(Make(p,root,true,true));var clock=new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay);var contact=Replay(player,rows,slots,path,clock,terrain,root,frame,aim);if(contact!=null)return contact;
            }
            return null;
        }
    }
}
