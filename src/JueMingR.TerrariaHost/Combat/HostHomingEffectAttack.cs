using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These are free projectiles after their hand birth. In particular neither
    // the Empress streak nor ArcSurge1122 reads a later mouse position. Random
    // future births below are legal representatives, never RNG observations.
    internal static partial class HostHomingEffectAttack
    {
        internal static bool Handles(int type){return type==931 || type==932 || type==1116 || type==1122 || type==1039;}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;
        }
        private static bool Center(HostProjectileTargets targets,NpcTrajectory path,int tick,int index,out Vector2 center)
        {var b=targets.Bounds(index,path,tick);center=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);return targets.Slot(index)!=path.Identity.Slot || path[tick].CanReceive;}
        private static bool FindStreakTarget(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,out int slot,out Vector2 center)
        {
            slot=-1;center=Vector2.Zero;float nearest=p.type==1039?1600:800;
            for(int i=0;i<targets.Count;i++)
            {
                Vector2 c;if(!Center(targets,path,tick,i,out c) || p.localNPCImmunity[targets.Slot(i)]!=0)continue;float distance=Vector2.Distance(c,p.Center);if(distance>=nearest)continue;
                bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),targets.Bounds(i,path,tick),out clear,out stop))return false;
                if(clear){nearest=distance;slot=targets.Slot(i);center=c;}
            }
            return true;
        }
        // One native AI action, before wind/tile movement and Damage. This
        // method is also compared directly with original AI by native tests.
        internal static bool Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(p.type==932){p.ai[0]++;p.alpha=(int)MathHelper.Lerp(255,0,Utils.GetLerpValue(0,10,p.ai[0],true));p.rotation=p.velocity.ToRotation();return true;}
            if(p.type==1116)
            {
                bool initial=p.ai[0]<20;p.localAI[0]=1;p.ai[0]++;
                if(initial){p.velocity*=.95f;return true;}
                float nearest=400;bool found=false;Vector2 target=Vector2.Zero;
                for(int i=0;i<targets.Count;i++)
                {
                    Vector2 c;if(!Center(targets,path,tick,i,out c))continue;float distance=Vector2.Distance(c,p.Center);if(distance>=nearest)continue;
                    bool clear=false;PredictionStop stop;if(distance>0 && !terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),targets.Bounds(i,path,tick),out clear,out stop))return false;
                    if(distance>0 && !clear)continue;nearest=distance;target=c;found=true;
                }
                if(found)p.velocity=(p.velocity*30+(target-p.Center).SafeNormalize(Vector2.Zero)*6)/31;
                else{p.velocity.X=MathHelper.Lerp(p.velocity.X,(float)Math.Sin(p.ai[0]*.1f)*1.5f,.1f);p.velocity.Y=MathHelper.Lerp(p.velocity.Y,.3f,.1f);}return true;
            }
            if(p.type!=931 && p.type!=1039)return false;
            if(p.timeLeft>180)
            {float turn=(float)Math.Cos((float)p.whoAmI%6/6+p.position.X/320+p.position.Y/160);p.velocity*=.97f;p.velocity=p.velocity.RotatedBy(turn*MathHelper.TwoPi*.125f/30);}
            int locked=(int)p.ai[0];Vector2 selected=Vector2.Zero;bool exists=false;
            for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==locked){exists=Center(targets,path,tick,i,out selected);break;}
            if(locked>=0 && !exists){locked=-1;p.ai[0]=-1;}
            if(locked==-1)
            {
                if(!FindStreakTarget(p,targets,path,tick,terrain,out locked,out selected))return false;exists=locked>=0;
                p.ai[0]=locked;
            }
            if(p.timeLeft<=180 && p.timeLeft>20)
            {
                Vector2 desired=p.velocity;if(exists)desired=(selected-p.Center).SafeNormalize(Vector2.Zero)*30;else p.timeLeft--;
                float amount=MathHelper.Lerp(.075f,.125f,Utils.GetLerpValue(180,30,p.timeLeft,true));
                p.velocity=Vector2.SmoothStep(p.velocity,desired,amount);p.velocity*=MathHelper.Lerp(.85f,1,Utils.GetLerpValue(0,90,p.timeLeft,true));
            }
            p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2;return true;
        }
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;return true;}
            if(!Ai(p,targets,path,tick,terrain))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active){p.active=false;return true;}
            var position=p.position;var prior=p.velocity;var velocity=prior;Vector2 wetVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,false,false,out wetVelocity,out hit,nativeClip:true))return false;
            p.position=position;
            if(hit)
            {
                if(p.type==931 || p.type==1039){if(velocity.X!=prior.X)velocity.X=-prior.X;if(velocity.Y!=prior.Y)velocity.Y=-prior.Y;}
                else{p.active=false;return true;}
            }
            p.velocity=velocity;p.position+=velocity;p.direction=velocity.X<0?-1:1;return true;
        }
        internal static bool EventideCollides(Projectile p,MotionRect target)
        {
            var box=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
            var broad=new Rectangle((int)p.position.X-150,(int)p.position.Y-150,300,300);
            if(!broad.Intersects(box))return false;float point=0;var axis=p.rotation.ToRotationVector2()*40;
            return Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),p.Center-axis,p.Center+axis,8,ref point);
        }
        internal static bool StreakOnHit(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {if(p.type==931)p.damage=(int)(p.damage*.8);int next;Vector2 center;if(!FindStreakTarget(p,targets,path,tick,terrain,out next,out center))return false;if(next!=-1)p.ai[0]=next;return true;}
        private static Projectile[] Capture(HostAttackObstacles root,int type)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==type)rows.Add(p);var scene=rows.ToArray();root.CaptureProjectileFamily(scene,type,type);return scene;}
        private static List<Member> Copy(Projectile[] scene,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){var q=HostNebulaBlazeAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=q.owner==Main.myPlayer?root.ForkForSpawn(q):null});}return rows;}
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                // Original Update decrements these once before its extra
                // update loop, not once per AI/movement/Damage subupdate.
                for(int i=0;i<p.localNPCImmunity.Length;i++)if(p.localNPCImmunity[i]>0)p.localNPCImmunity[i]--;
                if(m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height)){p.active=false;continue;}
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(p,targets,path,tick,terrain))return false;
                    if(!p.active){slots.Release(p.whoAmI);break;}
                    if(m.Receive!=null && p.friendly && !p.hostile && p.damage>0)
                    {
                        int width=p.width+(p.type==1116?10:0),height=p.height+(p.type==1116?10:0);
                        bool queryFailed=false;int before=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,width,height,p.type==932?(Func<MotionRect,bool>)(b=>EventideCollides(p,b)):null,consumeSelected:true,onHit:(slot,remaining)=>
                        {
                            if(p.usesLocalNPCImmunity)p.localNPCImmunity[slot]=p.localNPCHitCooldown;
                            if((p.type==931 || p.type==1039) && !StreakOnHit(p,targets,path,tick,terrain)){queryFailed=true;return false;}
                            return true;
                        });p.penetrate=m.Receive.Remaining;if(queryFailed)return false;
                        if(m.Source && m.Receive.SelectedHits>before){contact=p.type==932?AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,p.Center.X,p.Center.Y,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional):AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,width,height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return true;}
                        if(!pass){p.active=false;if(p.penetrate==0)slots.Release(p.whoAmI);break;}
                    }
                    if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return true;
        }
        internal static Vector2 StaffBirth(Player player,int direction,PredictionTerrain terrain,out bool available)
        {
            var point=player.MountedCenter+new Vector2(direction*15,player.gravDir*3);player.ApplyItemPositionOffsetFromMount(ref point);
            PredictionTile tile;PredictionStop stop;available=terrain.Tile((int)point.X/16,(int)point.Y/16,out tile,out stop);
            return available && tile.Active && tile.Solid && !tile.SolidTop && !tile.Platform?player.MountedCenter:point;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(age-delay<0 || age+1>=path.Count || ammo.Damage<=0)return null;
            if(ammo.WeaponType==4953)return Eventide(player,ammo,path,origin,age,beforeNpc,terrain,root,delay);
            if(ammo.Projectile==1122)return ArcSurge(player,ammo,path,origin,age,terrain,root,delay);
            if(ammo.Projectile!=931 && ammo.Projectile!=1116)return null;
            root.CaptureHomingInputs(player,false);root.CaptureShotFacing(player);
            var sample=new Projectile();sample.SetDefaults(ammo.Projectile);sample.owner=player.whoAmI;
            var targets=root.CaptureChaseTargets(sample);var scene=Capture(root,ammo.Projectile);var pool=root.CaptureBirthSlots();
            for(int seed=0;seed<(ammo.Projectile==931?1:9);seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=HostDirectionalControl.Cursor(player,new Vector2(b.CenterX,b.CenterY));var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);
                root.BeginCandidate();var rows=Copy(scene,root);var slots=pool.Copy();bool available=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,targets,path,terrain,age-delay+wait,wait,aim,out ignored)){available=false;break;}}if(!available)continue;
                Vector2 birth=StaffBirth(player,HostBetsyFlameAttack.Facing(player,origin,aim),terrain,out available);if(!available)return null;
                // (0,-3) and (0,-2) are native edge-circle representatives;
                // the zero circular addend is legal. Three petals are real
                // siblings from one hand, sharing native slot/immunity order.
                for(int sibling=0;sibling<(ammo.Projectile==1116?3:1);sibling++)
                {
                    int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=birth;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;p.ai[0]=-1;
                    p.ai[1]=((player.itemAnimationMax>0?(float)player.itemAnimation/player.itemAnimationMax*.66f:0)+player.miscCounterNormalized)%1;
                    p.velocity=ammo.Projectile==931?new Vector2(0,-3):new Vector2(0,-4)+velocity*2;
                    rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true,Representative=true});
                }
                rows.Sort((a,b2)=>a.Shot.whoAmI.CompareTo(b2.Shot.whoAmI));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,targets,path,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile source,NpcTrajectory path,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || !source.active || !Handles(source.type))return null;
            var targets=source.type==1122?root.CaptureAttachmentTargets(source):root.CaptureChaseTargets(source);
            var scene=Capture(root,source.type);root.BeginCandidate();var rows=Copy(scene,root);var slots=root.CaptureBirthSlots().Copy();
            foreach(var m in rows)if(m.Shot.whoAmI==source.whoAmI)m.Source=true;
            var aim=source.Center+source.velocity*1000;
            for(int step=1;step<Math.Min(121,path.Count);step++)
            {
                AttackContact contact;bool valid=source.type==1122?ArcOuter(rows,slots,player,targets,path,terrain,step,step,aim,out contact):Outer(rows,slots,targets,path,terrain,step,step,aim,out contact);
                if(!valid)return null;if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Age<0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            var primary=sources[0];foreach(var source in sources)if(source==null || !source.active || source.owner!=player.whoAmI || source.type!=primary.type || !Handles(source.type))return null;
            if(primary.type==1122)root.CaptureHomingInputs(player,true);
            var targets=primary.type==1122?root.CaptureAttachmentTargets(primary):root.CaptureChaseTargets(primary);
            var scene=Capture(root,primary.type);root.BeginCandidate();var rows=Copy(scene,root);var slots=root.CaptureBirthSlots().Copy();
            foreach(var m in rows)foreach(var source in sources)if(m.Shot.whoAmI==source.whoAmI)m.Source=true;
            var aim=primary.Center+primary.velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++)
            {
                AttackContact contact;bool valid=primary.type==1122?ArcOuter(rows,slots,player,targets,path,terrain,clock.FirstTick+step,step+1,aim,out contact):Outer(rows,slots,targets,path,terrain,clock.FirstTick+step,step+1,aim,out contact);
                if(!valid)return null;if(contact!=null)return contact;
            }
            return null;
        }
    }
}
