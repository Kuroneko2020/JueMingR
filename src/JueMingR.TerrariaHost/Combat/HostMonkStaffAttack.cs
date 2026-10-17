using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The two AI142 hand motors use finite animation poses. Ghastly's OnHit
    // ghost and Fury's three timed bolts/merging circles share native pool and
    // immunity order, with explicit lawful future random representatives.
    internal static partial class HostMonkStaffAttack
    {
        internal static bool Handles(int type){return type==699 || type==708;}
        internal static bool Family(int type){return Handles(type) || type==700 || type==709 || type==1110;}
        internal sealed class OwnerLease
        {
            private readonly Player owner;private readonly bool dead,frozen;private readonly HostMeleeAttack.SpearFlightState pose;
            internal OwnerLease(Player p,Projectile[] sources,bool observed){owner=p;dead=p.dead;frozen=p.frozen;if(observed)pose=new HostMeleeAttack.SpearFlightState(p,sources);}
            internal bool Current {get{return owner.dead==dead && owner.frozen==frozen && (pose==null || pose.Current);}}
        }
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random=false){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Random=random};}
        internal static bool ParentAi(Projectile p,Player owner,out bool bolt)
        {
            bolt=false;p.direction=owner.direction;p.Center=owner.RotatedRelativePoint(owner.MountedCenter);if(owner.dead){p.active=false;p.timeLeft=0;return true;}
            if(!owner.frozen)
            {
                p.spriteDirection=p.direction=owner.direction;p.alpha=Math.Max(0,p.alpha-127);if(p.type==699 && p.localAI[0]>0)p.localAI[0]--;
                float progress=1-(float)owner.itemAnimation/owner.itemAnimationMax,angle=p.velocity.ToRotation(),radius=p.velocity.Length();var swing=new Vector2(1,0).RotatedBy(MathHelper.Pi+progress*MathHelper.TwoPi)*new Vector2(radius,p.ai[0]);var endpoint=owner.RotatedRelativePoint(owner.MountedCenter)+swing.RotatedBy(angle)+new Vector2(radius+62,0).RotatedBy(angle);p.position+=swing.RotatedBy(angle)+new Vector2(radius+22,0).RotatedBy(angle);
                p.rotation=p.type==699?owner.RotatedRelativePoint(owner.MountedCenter).AngleTo(endpoint)+MathHelper.PiOver4*owner.direction:(endpoint-owner.RotatedRelativePoint(owner.MountedCenter)).SafeNormalize(Vector2.UnitX).ToRotation()+MathHelper.PiOver4*owner.direction;if(p.spriteDirection==-1)p.rotation+=MathHelper.Pi;
                bolt=p.type==708 && p.owner==Main.myPlayer && (owner.itemAnimation==2 || owner.itemAnimation==6 || owner.itemAnimation==10);
            }
            if(owner.whoAmI==Main.myPlayer && owner.itemAnimation<=2){p.active=false;p.timeLeft=0;}return true;
        }
        internal static bool ChildAi(Projectile p)
        {
            if(p.type==700){p.ai[0]++;p.alpha=p.ai[0]>50?Math.Min(255,p.alpha+25):Math.Max(100,p.alpha-25);p.velocity*=.98f;if(p.ai[0]>=60){p.active=false;p.timeLeft=0;}}
            else if(p.type==709){p.alpha=Math.Max(0,p.alpha-30);p.ai[1]++;if(p.ai[1]>=30)return false;if(p.ai[0]>=15){p.ai[0]=15;p.velocity.Y+=.1f;}if(p.velocity.Y>16)p.velocity.Y=16;}
            else
            {
                if(p.ai[1]==1){p.friendly=false;p.alpha=Math.Min(255,p.alpha+51);if(p.alpha>=255){p.active=false;p.timeLeft=0;return true;}}else p.alpha=Math.Max(0,p.alpha-50);
                p.ai[0]++;if(p.ai[0]>120)p.ai[0]=0;var wave=-Vector2.UnitY.RotatedBy(MathHelper.TwoPi*p.ai[0]/30);float scale=Math.Max(.75f+wave.Y*.25f,.8f-wave.Y*.2f);var center=p.Center;p.width=p.height=(int)(80*scale);p.Center=center;
            }
            return true;
        }
        internal static bool Collides(Projectile p,MotionRect target)
        {
            // Native Colliding accepts the raw body before its extra699 line.
            // The shaft supplements this rectangle; it does not replace it.
            if(p.Hitbox.Intersects(new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height)))return true;
            if(p.type!=699)return false;
            float angle=p.rotation-MathHelper.PiOver4*Math.Sign(p.velocity.X)+(p.spriteDirection==-1?MathHelper.Pi:0),at=0;var end=p.Center+angle.ToRotationVector2()*-95;return Collision.CheckAABBvLineCollision(new Vector2(target.X,target.Y),new Vector2(target.Width,target.Height),p.Center,end,23*p.scale,ref at);
        }
        private static bool Move(Projectile p,PredictionTerrain terrain,out bool killed,out Vector2 death)
        {
            killed=false;death=p.Center;if(!p.active)return true;var wetVelocity=p.velocity;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater){bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;}
            if(p.shimmerWet){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}var position=p.position;var velocity=p.velocity;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit){p.position+=velocity;death=p.Center;killed=true;}p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;return true;
        }
        private static bool Wet(Projectile p,PredictionTerrain terrain)
        {bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return true;}
        private static bool Circle(Member parent,List<Member> rows,HostProjectileBirthSlots slots,HostAttackObstacles root,PredictionTerrain terrain,Vector2 center)
        {
            foreach(var old in rows)if(old.Shot.active && old.Shot.type==1110 && old.Shot.Hitbox.Intersects(new Rectangle((int)center.X-40,(int)center.Y-40,80,80))){old.Shot.ai[1]=1;old.Shot.velocity=(center-old.Shot.Center)/5;}
            int slot;if(!slots.TryAllocate(out slot))return false;var q=new Projectile();q.SetDefaults(1110);q.active=true;q.owner=parent.Shot.owner;q.whoAmI=slot;q.Center=center;q.damage=parent.Shot.damage;q.timeLeft=60;rows.Add(Make(q,root,parent.Source,true));foreach(var row in rows)row.Random=true;return true;
        }
        private static bool Ghast(Member parent,List<Member> rows,HostProjectileBirthSlots slots,HostAttackObstacles root,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory path,int tick)
        {
            var p=parent.Shot;if(p.localAI[0]>0)return true;p.localAI[0]=1000;Vector2 center=p.Center,velocity=Vector2.Zero;int selected=-1;
            for(int i=0;i<targets.Count;i++){var b=targets.Bounds(i,path,tick);if(Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY))>=800)continue;if(selected<0 || targets.Slot(i)==path.Identity.Slot){selected=i;if(targets.Slot(i)==path.Identity.Slot)break;}}
            if(selected>=0){var b=targets.Bounds(selected,path,tick);center=new Vector2(b.CenterX,b.CenterY);velocity=targets.Velocity(selected,path,tick);}var origin=center-new Vector2(120,0);int slot;if(!slots.TryAllocate(out slot))return false;var q=new Projectile();q.SetDefaults(700);q.active=true;q.whoAmI=slot;q.owner=p.owner;q.Center=origin;q.velocity=new Vector2(4,0)+(center+velocity*15-origin).SafeNormalize(Vector2.Zero)*2;q.damage=p.damage;if(!Wet(q,terrain))return false;rows.Add(Make(q,root,parent.Source,true));foreach(var row in rows)row.Random=true;return true;
        }
        private static bool Bolt(Member parent,Player owner,List<Member> rows,HostProjectileBirthSlots slots,HostAttackObstacles root,PredictionTerrain terrain)
        {
            var p=parent.Shot;int slot;if(!slots.TryAllocate(out slot))return false;var q=new Projectile();q.SetDefaults(709);q.active=true;q.whoAmI=slot;q.owner=p.owner;q.Center=p.Center;double angle=owner.itemAnimation==2?.3839724659919739:owner.itemAnimation==6?-.3839724659919739:0;q.velocity=p.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(angle)*11;q.damage=p.damage;if(!Wet(q,terrain))return false;rows.Add(Make(q,root,parent.Source,true));foreach(var row in rows)row.Random=true;return true;
        }
        private static bool Outer(Player owner,List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;int after=-1;
            for(int visits=0;visits<1000;visits++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);break;}bool parent=Handles(p.type),killed=false;Vector2 death=p.Center;
                    if(parent){bool bolt;ParentAi(p,owner,out bolt);if(bolt && !Bolt(m,owner,rows,slots,root,terrain))return false;}
                    else if(!ChildAi(p)){killed=true;death=p.Center;}
                    if(killed && p.type==709){if(!Circle(m,rows,slots,root,terrain,death))return false;p.active=false;p.timeLeft=0;}if(!p.active){slots.Release(p.whoAmI);break;}
                    if(!Move(p,terrain,out killed,out death))return false;if(killed){if(p.type==709 && !Circle(m,rows,slots,root,terrain,death))return false;p.active=false;p.timeLeft=0;slots.Release(p.whoAmI);break;}if(p.friendly && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool failed=false;Func<int,int,bool> onHit=(slot,left)=>{if(p.type==699 && !Ghast(m,rows,slots,root,terrain,targets,path,tick)){failed=true;return false;}return true;};
                        bool pass=parent?m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(owner,p,b,noTile,terrain),0,true,onHit):m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){var box=p.Hitbox;contact=p.type==699?AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,p.Center.X,p.Center.Y,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional):AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(failed || !pass && p.penetrate!=0)return false;killed=p.penetrate==0;
                    }
                    if(!killed)killed=--p.timeLeft<=0;if(killed){if(p.type==709 && !Circle(m,rows,slots,root,terrain,p.Center))return false;p.active=false;p.timeLeft=0;slots.Release(p.whoAmI);break;}
                }
            }return false;
        }
        private static Projectile[] Capture(Player owner,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==owner.whoAmI && Family(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,owner.whoAmI,new[]{699,700,708,709,1110});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var list=new List<Member>();foreach(var p in scene)list.Add(Make(HostBetsyFlameAttack.Copy(p),root,source!=null && Array.IndexOf(source,p)>=0));return list;}
    }
}
