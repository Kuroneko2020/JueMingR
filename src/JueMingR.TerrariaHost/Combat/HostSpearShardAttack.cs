using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Obsidian's first native spear AI emits finite AI014 bodies. They share
    // receive history and pool order with their anchored parent; the random
    // unborn count/type/scale/lifetime is an explicit legal representative.
    internal static class HostSpearShardAttack
    {
        internal static bool Handles(int type){return type==367;}
        internal static bool Family(int type){return type==367 || type>=1107 && type<=1109;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Random=random};}
        internal static bool ChildStep(Projectile p,PredictionTerrain terrain)
        {
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}var wetVelocity=p.velocity;p.ai[0]++;
            if(p.ai[0]>5){p.ai[0]=5;if(p.velocity.Y==0 && p.velocity.X!=0){p.velocity.X*=.97f;if((double)p.velocity.X>-.01 && (double)p.velocity.X<.01)p.velocity.X=0;}p.velocity.Y+=.2f;}
            if(p.wet){p.active=false;return true;}if(p.velocity.Y<.25f && p.velocity.Y>.15f)p.velocity.X*=.8f;if(p.velocity.Y>16)p.velocity.Y=16;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;
            if(p.shimmerWet){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
            var prior=p.velocity;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            // This explicit AI014 subfamily only reflects X by .1. The
            // ordinary .5 Y bounce is in the following native else branch.
            if(hit && p.velocity.X!=prior.X)p.velocity.X=prior.X*-.1f;p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,Player body,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;var boxes=new List<Rectangle>();for(int visited=0;visited<1000;visited++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool parent=p.type==367;
                    if(parent)
                    {
                        if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);break;}
                        p.direction=body.direction;p.spriteDirection=-p.direction;p.Center=body.RotatedRelativePoint(body.MountedCenter);float offset=HostMeleeAttack.SpearOffset(p,body);p.Center+=p.velocity*offset;
                        if(p.localAI[0]==0 && p.owner==Main.myPlayer)
                        {
                            p.localAI[0]=1;int slot;if(!slots.TryAllocate(out slot))return false;var child=new Projectile();child.SetDefaults(1107);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.Center=p.Center;child.velocity=p.velocity*2;child.damage=(int)(p.damage*.5);child.knockBack=0;child.timeLeft=90;
                            bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind,true))return false;child.wet=wet;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;rows.Add(Make(child,root,m.Source,true));foreach(var row in rows)row.Random=true;
                        }
                        p.ai[0]++;if(p.ai[0]>=body.itemAnimationMax){p.active=false;slots.Release(p.whoAmI);break;}
                        var old=p.Center;if(!HostProjectileEnvironment.Dry(p,terrain,old.X,old.Y,p.velocity.X))return false;p.Center+=p.velocity;HostMeleeAttack.SpearBoxes(p,body,boxes);
                    }
                    else if(!ChildStep(p,terrain))return false;
                    if(!p.active){slots.Release(p.whoAmI);break;}
                    int hits=m.Receive.SelectedHits;bool pass;
                    if(parent)pass=m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>boxes.Exists(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true);
                    else pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);
                    p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits)
                    {
                        var box=p.Hitbox;if(parent){var b=path[tick].ProjectileReceiveBounds;box=boxes.Find(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)));}
                        contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return true;
                    }
                    if(!pass && p.penetrate!=0)return false;if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return false;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))rows.Add(p);var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{367,1107,1108,1109});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,sources!=null && Array.IndexOf(sources,p)>=0,false));return rows;}
        private static bool Pose(Player body,HostAttackPlayerFrame frame,ref PredictionPlayerMotion motion,PredictionTerrain terrain,int step,bool advance)
        {if(advance){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return false;}HostAttackPlayerFrame.ApplyPose(body,motion);return true;}
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(ammo.Projectile!=367 || age-delay<0 || age+1>=path.Count)return null;root=root??new HostAttackObstacles(player,Terraria.ID.ContentSamples.ProjectilesByType[367],path,delay,beforeNpc,!beforeNpc);root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var inputs=new List<Vector2>();
            int maximum=player.itemAnimation>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,item),animation=player.itemAnimation>0?player.itemAnimation:maximum;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-player.MountedCenter).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(inputs.Contains(unit))continue;inputs.Add(unit);var aim=player.MountedCenter+unit*10000;
                root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();var body=HostMeleeAttack.PlayerCopy(player);var motion=frame.Motion;body.itemAnimationMax=maximum;bool valid=true;
                // Preview passes its already advanced future hand. Existing
                // members' pre-launch frames instead start at the real owner.
                if(delay>0)
                {
                    var live=Main.player[player.whoAmI];var prior=HostAttackPlayerFrame.Capture(live,path);var priorMotion=prior.Motion;var priorBody=HostMeleeAttack.PlayerCopy(live);
                    for(int wait=1;wait<=delay;wait++){if(!Pose(priorBody,prior,ref priorMotion,terrain,wait,!beforeNpc || wait>1)){valid=false;break;}priorBody.itemAnimation=Math.Max(0,live.itemAnimation-wait+1-(!beforeNpc?1:0));AttackContact ignored;if(!Outer(rows,slots,priorBody,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}
                }
                if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;var parent=new Projectile();parent.SetDefaults(367);parent.active=true;parent.owner=player.whoAmI;parent.whoAmI=slot;parent.Center=player.RotatedRelativePoint(player.MountedCenter);parent.velocity=HostThornAttack.BirthVelocity(player,parent.Center,aim,ammo.Speed);parent.damage=ammo.Damage;parent.knockBack=item.knockBack;rows.Add(Make(parent,root,true,false));
                for(int step=0;age+step+1<path.Count;step++)
                {
                    bool hand=rows.Exists(row=>row.Shot.active && row.Shot.type==367);if(hand && !Pose(body,frame,ref motion,terrain,step,!beforeNpc || step>0))break;body.itemAnimation=Math.Max(0,animation-step-(!beforeNpc?1:0));AttackContact contact;if(!Outer(rows,slots,body,path,age+step+1,delay+step+1,terrain,root,aim,out contact))break;if(contact!=null)return contact;
                }
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,sources,root);var slots=root.CaptureBirthSlots().Copy();var body=HostMeleeAttack.PlayerCopy(player);var frame=HostAttackPlayerFrame.Capture(player,path);var motion=frame.Motion;var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){bool hand=rows.Exists(row=>row.Shot.active && row.Shot.type==367);if(hand && !Pose(body,frame,ref motion,terrain,step,clock.MovePlayer(step)))return null;body.itemAnimation=Math.Max(0,player.itemAnimation-step-(clock.NextWorld?1:0));AttackContact contact;if(!Outer(rows,slots,body,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
