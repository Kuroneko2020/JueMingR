using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // One private slot-ordered world for an AI99 parent's direct damage,
    // its own finite OnHit children and Terrarian's periodic shot. Children
    // remain autonomous: no source registration or cursor grant occurs here.
    internal sealed class HostYoyoFamilyAttack
    {
        internal static bool Parent(int type){return type==545 || type==546 || type==554 || type==555 || type==999 || type==603;}
        internal static bool Child(int type){return type==1125 || type==1134 || type==1135 || type==1130 || type==604 || HostBeeAttack.Handles(type);}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal HostBeeAttack.State Bee;internal bool Source,Representative,Secondary,ReleaseNext;internal int Remaining=120;}
        private readonly List<Member> rows=new List<Member>();
        private readonly HostAttackObstacles root;private readonly HostProjectileBirthSlots slots;
        private readonly HostProjectileTargets targets,beeTargets;
        private readonly Player player;private readonly NpcTrajectory future;private readonly PredictionTerrain terrain;
        private readonly int sourceSlot;private int counter;private bool failed;private readonly bool[] noTile;
        internal HostAttackObstacles Obstacles {get{return root;}}
        internal bool Failed {get{return failed;}}
        internal HostYoyoFamilyAttack(Player player,Projectile source,NpcTrajectory future,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles supplied=null,bool unborn=false)
        {
            this.player=player;this.future=future;this.terrain=terrain;counter=player.yoyoHitCounter;
            root=supplied??new HostAttackObstacles(player,source,future,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);root.CaptureYoyoOwner(player);root.CaptureBeeOwner();
            var scene=new List<Projectile>();var types=new List<int>{545,546,554,555,999,603,1125,1134,1135,1130,604,181,566};
            foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && (Parent(p.type) || Child(p.type)))scene.Add(p);
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,types.ToArray());slots=root.CaptureBirthSlots().Copy();root.BeginCandidate();
            targets=root.CaptureChaseTargets(source);beeTargets=HostBeeAttack.CaptureTargets(source,root);noTile=new bool[Main.maxNPCs];for(int i=0;i<noTile.Length;i++)noTile[i]=Main.npc[i].noTileCollide;
            foreach(var p in scene)Add(HostRaySpellAttack.Copy(p),ReferenceEquals(p,source),false);
            sourceSlot=source.whoAmI;
            if(unborn || !scene.Contains(source))
            {int slot;if(!slots.TryAllocate(out slot)){failed=true;return;}sourceSlot=slot;var copy=HostRaySpellAttack.Copy(source);copy.whoAmI=slot;if(copy.damage<=0)copy.damage=player.GetWeaponDamage(player.HeldItem);Add(copy,true,unborn);}
        }
        private void Add(Projectile p,bool source,bool representative)
        {
            bool secondary=false;if(Parent(p.type))foreach(var row in rows)if(row.Shot.active && row.Shot.type==p.type && row.Shot.whoAmI<p.whoAmI && row.Shot.ai[0]>=-1)secondary=true;
            rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=representative,Secondary=secondary,Remaining=Parent(p.type) && p.ai[0]>=0?HostYoyoNavigation.Remaining(player,p,secondary):120,Bee=HostBeeAttack.Handles(p.type)?new HostBeeAttack.State(p):default(HostBeeAttack.State)});
        }
        private bool Spawn(Projectile child,bool source,bool representative)
        {int slot;if(!slots.TryAllocate(out slot))return false;child.whoAmI=slot;Add(child,source,representative);return true;}
        private Projectile NewChild(Projectile parent,int type,Vector2 velocity,float damage,float a0=0,float a1=0)
        {var q=new Projectile();q.SetDefaults(type);q.active=true;q.owner=parent.owner;q.Center=parent.Center;q.velocity=velocity;q.damage=(int)(parent.damage*damage);q.knockBack=parent.knockBack;q.ai[0]=a0;q.ai[1]=a1;return q;}
        private MotionRect Bounds(int slot,int tick)
        {return slot==future.Identity.Slot?future[tick].Bounds:new MotionRect(Main.npc[slot].position.X,Main.npc[slot].position.Y,Main.npc[slot].width,Main.npc[slot].height);}
        private static Vector2 Closest(MotionRect b,Vector2 p)
        {return new Vector2(MathHelper.Clamp(p.X,b.X,b.X+b.Width),MathHelper.Clamp(p.Y,b.Y,b.Y+b.Height));}
        private bool Nearest(Projectile p,int victim,int tick,float radius,bool manhattan,bool canHit,out Vector2 point,out float distance)
        {
            point=p.Center;distance=radius;bool found=false;
            for(int i=0;i<targets.Count;i++)
            {
                int slot=targets.Slot(i);if(slot==victim)continue;var b=targets.Bounds(i,future,tick);var candidate=manhattan?new Vector2(b.CenterX,b.CenterY):Closest(b,p.Center);
                float length=manhattan?Math.Abs(candidate.X-p.Center.X)+Math.Abs(candidate.Y-p.Center.Y):Vector2.Distance(candidate,p.Center);if(length>=distance)continue;
                bool clear=true;PredictionStop stop;
                if(canHit || !noTile[slot])
                {var from=new MotionRect(p.position.X,p.position.Y,p.width,p.height);if(!(canHit?terrain.CanHit(from,b,out clear,out stop):terrain.CanHitLine(from,b,out clear,out stop)))return false;}
                if(!clear)continue;found=true;point=candidate;distance=length;
            }
            if(!found)distance=0;return true;
        }
        private bool OnHit(Member member,int victim,int tick,int outer)
        {
            var p=member.Shot;if(!Parent(p.type))return true;
            int amount=p.type==545?2:p.type==546?3:p.type==554?4:p.type==555?7:0;
            bool proc=amount>0 && counter%amount==0;if(amount>0 && (p.type!=545 || p.ai[0]!=-2))counter++;
            if(p.type==545 && p.ai[0]==-2)return true;
            if(p.type==999)
            {
                // One legal success representative of the original one-in-three
                // branch. The observed actual child later uses its exact state.
                int type=181;var velocity=(p.Center-new Vector2(Bounds(victim,tick).CenterX,Bounds(victim,tick).CenterY)).SafeNormalize(Vector2.Zero)*.25f;
                var bee=NewChild(p,type,velocity,.5f);bee.damage=p.damage/2;bee.knockBack=p.knockBack;bee.melee=true;return Spawn(bee,member.Source,true);
            }
            if(!proc)return true;
            if(p.type==545 || p.type==555)
            {
                Vector2 target;float distance;if(!Nearest(p,victim,tick,p.type==545?208:80,false,false,out target,out distance))return false;
                var unit=(target-p.Center).SafeNormalize(Vector2.UnitX);float scale=Math.Min(1.5f,Math.Max(.33f,distance/200));
                var child=NewChild(p,p.type==545?1125:1130,unit*(p.type==545?1.1f:14),2,0,scale);child.localNPCImmunity[victim]=-1;return Spawn(child,member.Source,true);
            }
            if(p.type==554)
            {
                var b=Bounds(victim,tick);var direction=new Vector2(b.CenterX,b.CenterY)-p.Center;
                foreach(int side in new[]{-2,0,2})if(!Spawn(NewChild(p,1135,direction.RotatedBy(side*MathHelper.TwoPi*.05f).SafeNormalize(Vector2.UnitX)*22,.5f),member.Source,true))return false;return true;
            }
            if(p.type==546)
            {
                for(int i=0;i<5;i++)
                {var child=NewChild(p,1134,(i*MathHelper.TwoPi/5).ToRotationVector2()*4,1,i==0?0:1);if(!Spawn(child,member.Source,true))return false;root.BlockStaticUntil(child,victim,outer+child.idStaticNPCHitCooldown);}return true;
            }
            return true;
        }
        private bool Periodic(Member member,int tick)
        {
            var p=member.Shot;if(p.type!=603 || p.owner!=Main.myPlayer || p.ai[0]==3)return true;p.localAI[1]++;
            if(p.localAI[1]<6)return true;p.localAI[1]=0;Vector2 target;float distance;
            if(!Nearest(p,-1,tick,400,true,true,out target,out distance))return false;
            var velocity=distance>0?(target-p.Center).SafeNormalize(Vector2.Zero)*6.4f:(p.velocity*.25f+Vector2.UnitX*2.5f)*.8f;
            var child=NewChild(p,604,velocity,1);child.Center=p.Center-velocity;return Spawn(child,member.Source,true);
        }
        internal static bool ChildStep(Projectile p,PredictionTerrain terrain)
        {
            var wetVelocity=p.velocity;
            if(p.type==1125)
            {p.scale=p.ai[1];if(++p.frameCounter>=2){p.frameCounter=0;if(++p.frame>=5){p.active=false;return true;}}p.ai[0]++;}
            else if(p.type==1130 || p.type==1135)
            {p.velocity*=.92f;p.localAI[0]++;if(p.localAI[0]>=20 && p.owner==Main.myPlayer){p.active=false;return true;}}
            else if(p.type==1134){p.localAI[0]=1;p.ai[1]++;p.velocity*=.96f;if(p.ai[1]>15){p.scale-=.05f;if((double)p.scale<=.2){p.scale=.2f;p.active=false;return true;}}}
            else if(p.type==604){p.velocity*=.985f;p.ai[1]++;if(p.ai[1]>30){p.alpha+=10;if(p.alpha>=255){p.alpha=255;p.active=false;return true;}}}
            else return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || wet && kind==3 || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                p.wet=wet;if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(!wet)p.honeyWet=p.lavaWet=false;
            }
            else p.wet=false;
            if(p.type==1125)return true; // Native UpdatePosition explicitly excludes this stationary line.
            var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,nativeClip:true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.active=false;return true;}
            p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Collision(Projectile p,MotionRect b)
        {
            var rectangle=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);
            if(p.type!=1125)return p.Hitbox.Intersects(rectangle);
            float hit=0;return p.ai[0]>=0 && CollisionLine(p,b,ref hit);
        }
        private static bool CollisionLine(Projectile p,MotionRect b,ref float hit)
        {return Terraria.Collision.CheckAABBvLineCollision(new Vector2(b.X,b.Y),new Vector2(b.Width,b.Height),p.Center,p.Center+p.velocity.SafeNormalize(-Vector2.UnitY)*200*p.scale,22*p.scale,ref hit);}
        internal AttackContact Advance(int tick,int outer,Vector2 aim,Vector2 playerCenter,Vector2 sourceCenter,Vector2 sourceVelocity,bool driveSource=true)
        {
            if(failed)return null;
            foreach(var row in rows)if(row.Shot.active && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height)){failed=true;return null;}
            int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member member=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (member==null || row.Shot.whoAmI<member.Shot.whoAmI))member=row;if(member==null)break;
                var p=member.Shot;after=p.whoAmI;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool parent=Parent(p.type),bee=HostBeeAttack.Handles(p.type);
                    if(parent)
                    {
                        if(outer>member.Remaining){failed=true;return null;}
                        if(!Periodic(member,tick)){failed=true;return null;}
                        if(member.ReleaseNext)
                        {
                            member.ReleaseNext=false;var child=NewChild(p,p.type,p.velocity,.75f,-2);child.knockBack=(int)(p.knockBack*.75);child.penetrate=3;
                            if(!Spawn(child,member.Source,true)){failed=true;return null;}p.ai[0]=-3;p.damage=0;
                        }
                        if(driveSource && p.whoAmI==sourceSlot){p.Center=sourceCenter;p.velocity=sourceVelocity;}
                        else if(!ParentStep(p,player,playerCenter,aim,member.Secondary,terrain)){failed=true;return null;}
                        p.timeLeft=6;
                    }
                    else if(bee)
                    {
                        if(!HostBeeAttack.Step(p,beeTargets,future,tick,terrain,ref member.Bee)){failed=true;return null;}
                        p.position=member.Bee.Position;p.velocity=member.Bee.Velocity;p.active=member.Bee.Alive;p.penetrate=member.Bee.Penetration;
                    }
                    else if(!ChildStep(p,terrain)){failed=true;return null;}
                    if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.damage<=0){if(--p.timeLeft<=0)p.active=false;continue;}
                    int hits=member.Receive.SelectedHits;bool valid=true,zero=bee && p.penetrate==0;
                    bool passed=member.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collision(p,b),unlimited:zero,maximumRemaining:bee?p.penetrate:0,consumeSelected:true,onHit:(victim,remaining)=>{valid=OnHit(member,victim,tick,outer);return valid;});if(!zero)p.penetrate=member.Receive.Remaining;
                    if(member.Source && member.Receive.SelectedHits>hits)
                    {var b=future[tick].ProjectileReceiveBounds;return AttackIntercept.ProjectileShapeContact(future,tick,sub,true,aim.X,aim.Y,b.CenterX,b.CenterY,member.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);}
                    // OnHit runs after the recorded Strike. An unknown later
                    // birth retires only continuation, not that first receive.
                    if(!valid || !passed && p.penetrate!=0){failed=true;return null;}
                    if(bee){member.Bee.Penetration=p.penetrate;HostBeeAttack.Tail(ref member.Bee);p.active=member.Bee.Alive;p.timeLeft=member.Bee.Left;}
                    else if(--p.timeLeft<=0 || p.penetrate==0)p.active=false;
                    if(!p.active)slots.Release(p.whoAmI);
                }
            }
            return null;
        }
        private static bool ParentStep(Projectile p,Player player,Vector2 owner,Vector2 aim,bool secondary,PredictionTerrain terrain)
        {
            float range,speed;HostYoyoNavigation.Parameters(player,p.type,secondary,out range,out speed);
            if(p.ai[0]>=0)
            {if(Vector2.Distance(p.Center,owner)>range*1.3f)return false;p.velocity=HostYoyoNavigation.Step(p.Center,p.velocity,aim,owner,range,speed,secondary);}
            else if(p.ai[0]==-2)
            {
                if(p.penetrate<0)p.penetrate=3;p.velocity.Y+=.3f;if(p.velocity.Length()>16)p.velocity=p.velocity.SafeNormalize(Vector2.Zero)*16;
                else if(p.velocity.Y<=3){speed=Math.Min(16,speed);if(Math.Abs(p.velocity.X)<.1f)p.velocity.X+=p.Center.X<owner.X?-.1f:.1f;else if(p.velocity.X>0 && p.velocity.X<speed)p.velocity.X+=.1f;else if(p.velocity.X<0 && p.velocity.X>-speed)p.velocity.X-=.1f;}
            }
            else
            {p.tileCollide=false;var delta=owner-p.Center;float inertia=Math.Max(1,14-speed/2)*.8f;speed*=1.5f;if(delta.Length()<speed+10 || delta.Length()>2000){p.active=false;return true;}p.velocity=(p.velocity*(inertia-1)+delta.SafeNormalize(Vector2.Zero)*speed)/inertia;}
            if(!HostProjectileEnvironment.Dry(p,terrain,p.Center.X,p.Center.Y,p.velocity.X))return false;float vx,vy;
            if(p.tileCollide)
            {
                var original=p.velocity;
                if(!terrain.ProjectileCollision(p.Center.X,p.Center.Y,original.X,original.Y,p.width,p.height,out vx,out vy,fallThrough:p.ai[0]!=-2))return false;
                p.velocity=new Vector2(vx,vy);
                // Detached AI99 lands on platforms, bounces only a downward
                // impact above4, and dies at a side collision. Held balls
                // retain their existing clipped slide at a navigation corner.
                if(p.ai[0]==-2)
                {if(vy!=original.Y && original.Y>4)p.velocity.Y=-original.Y*.6f;if(vx!=original.X){p.active=false;p.timeLeft=0;return true;}}
            }
            p.position+=p.velocity;return true;
        }
        internal static AttackContact ReleasePreview(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,Vector2 firstCenter,Vector2 velocity,Vector2 aim,out HostAttackObstacles root)
        {
            var parent=HostRaySpellAttack.Copy(source);parent.ai[0]=0;parent.Center=firstCenter-velocity;parent.velocity=velocity;
            var scene=new HostYoyoFamilyAttack(player,parent,path,clock,terrain,null,true);root=scene.root;
            var contact=scene.Advance(clock.FirstTick,clock.LaunchDelay+1,aim,player.Center,firstCenter,velocity);if(scene.failed || contact!=null)return contact;
            foreach(var row in scene.rows)if(row.Shot.whoAmI==scene.sourceSlot){row.ReleaseNext=true;break;}
            var frame=HostAttackPlayerFrame.Capture(player,path);var motion=frame.Motion;
            for(int step=1;step<Math.Min(120,path.Count-clock.FirstTick);step++)
            {
                PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return null;
                var owner=player.Center+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);
                contact=scene.Advance(clock.FirstTick+step,clock.LaunchDelay+step+1,aim,owner,Vector2.Zero,Vector2.Zero,false);if(scene.failed)return null;if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            // Live held parents belong to the registered navigation consumer.
            // Detached parents and observed finite children need no new input.
            foreach(var p in sources)if(p==null || p.owner!=player.whoAmI || Parent(p.type) && p.ai[0]>=0)return null;
            var scene=new HostYoyoFamilyAttack(player,sources[0],path,clock,terrain,root);foreach(var row in scene.rows)row.Source=Array.Exists(sources,p=>p.whoAmI==row.Shot.whoAmI);
            var frame=HostAttackPlayerFrame.Capture(player,path);var motion=frame.Motion;var aim=sources[0].Center;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++)
            {PredictionStop stop;if(clock.MovePlayer(step) && !PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return null;var owner=player.Center+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);var contact=scene.Advance(clock.FirstTick+step,step+1,aim,owner,Vector2.Zero,Vector2.Zero,false);if(scene.failed)return null;if(contact!=null)return contact;}return null;
        }
    }
}
