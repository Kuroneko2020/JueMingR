using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Heat Ray260 and Shadowbeam294 use AI048, not the AI001 Heat Ray606.
    // Their101 substeps are one world update: owner immunity never ticks
    //101 times. Only294 expands Damage's integer rectangle and ricochets.
    internal static class HostRaySpellAttack
    {
        internal static bool Handles(int type){return type==260 || type==294;}
        internal static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);q.oldPosition=p.oldPosition;q.oldVelocity=p.oldVelocity;q.numHits=p.numHits;return q;}
        internal static Vector2 BirthVelocity(Vector2 origin,Vector2 cursor,float speed,int facing)
        {
            float x=cursor.X-origin.X,y=cursor.Y-origin.Y;
            if(x==0 && y==0)x=facing;
            float scale=speed/(float)Math.Sqrt(x*x+y*y);
            // NewProjectile's .97 component clamp belongs only to aiStyle1.
            return new Vector2(x*scale,y*scale);
        }
        internal static Rectangle DamageBox(Projectile p)
        {var box=p.Hitbox;if(p.type==294)box.Inflate(10,10);return box;}
        internal static bool WorldRetired(Projectile p)
        {return p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y);}
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        internal static bool Step(Projectile p,PredictionTerrain terrain){return Motor(p,terrain);}
        private static bool Motor(Projectile p,PredictionTerrain terrain)
        {
            if(WorldRetired(p)){p.active=false;return true;}
            p.oldVelocity=p.velocity;
            if(p.active)
            {
                if(!WorldGen.InWorld(p.Center.ToTileCoordinates(),5))Kill(p);
                else
                {
                    int start=p.type==260?3:9;
                    if(p.localAI[0]==start){p.localAI[1]=p.Center.X;p.localAI[2]=p.Center.Y;}
                    p.localAI[0]++;p.alpha=255;
                }
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            // Shimmer() has no AI048 branch. Its only effect here is the
            // ordinary wet displacement (.375), with no invented AI001 Kill.
            p.oldPosition=p.position;var position=p.position;var velocity=p.velocity;Vector2 wetVelocity=p.velocity;bool hit=false;
            var last=p.velocity;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.type==294)
                {
                    if(velocity.X!=last.X){p.position.X+=velocity.X;p.velocity.X=-last.X;}
                    if(velocity.Y!=last.Y){p.position.Y+=velocity.Y;p.velocity.Y=-last.Y;}
                    if(p.owner==Main.myPlayer && p.localAI[0]>=9){p.localAI[1]=p.Center.X;p.localAI[2]=p.Center.Y;}
                }
                else{p.position+=velocity;Kill(p);}
            }
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p)
        {p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)Kill(p);}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))rows.Add(p);
            var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{260,294});return scene;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){own=true;break;}var copy=Copy(p);rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=own});}
            rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;
        }
        private static bool Outer(List<Member> rows,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                for(int sub=0;sub<=p.extraUpdates;sub++)
                {
                    if(!Motor(p,terrain))return false;if(!p.active)break;
                    if(p.friendly && !p.hostile && p.damage>0)
                    {
                        var box=DamageBox(p);int hits=m.Receive.SelectedHits;
                        // SharedReceiver owns slot order and real owner writes.
                        // A294 OnHit may reach damage0; PVE checks damage only
                        // before its NPC loop, so finish the current loop.
                        bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,
                            b=>box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)),consumeSelected:true,
                            onHit:(slot,time)=>{if(p.type==294)p.damage=(int)((double)p.damage*.9);p.numHits++;return true;});
                        p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits)
                        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.X+box.Width/2,box.Y+box.Height/2,box.Width,box.Height,AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0){p.active=false;break;}
                    }
                    Finish(p);if(!p.active)break;
                }
            }
            return true;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            // Origin is supplied by the existing prepared hand pose. AI048
            // never reads a future owner position: do not advance it twice.
            obstacles.CaptureShotFacing(player);var scene=Capture(player,obstacles);var pool=obstacles.CaptureBirthSlots();var inputs=new List<Vector2>();
            int seeds=ammo.Projectile==294?41:9;
            for(int seed=0;seed<seeds;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8))].ProjectileReceiveBounds;
                var aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;
                var cursor=Cursor(player,aim);if(inputs.Contains(cursor))continue;inputs.Add(cursor);obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++)
                {
                    AttackContact ignored;if(!Outer(rows,path,terrain,age-delay+wait,wait,cursor,out ignored)){valid=false;break;}
                    // AI048 Kill never spawns. A completed private world
                    // update can free its dead slot before the next hand.
                    foreach(var member in rows)if(!member.Shot.active)slots.Release(member.Shot.whoAmI);
                }
                if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;
                var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.velocity=BirthVelocity(origin,cursor,ammo.Speed,player.direction);p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                rows.Add(new Member{Shot=p,Receive=obstacles.ForkForSpawn(p),Source=true});rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                for(int step=1;step<=Math.Min(3,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,path,terrain,age+step,delay+step,cursor,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        // Whole-array observation/oracle, not permission to control an
        // autonomous beam. Partial Projectiles needs an explicit slot epoch.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in source)if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,obstacles);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var aim=source[0].Center+source[0].velocity*1000;
            int count=0;foreach(var p in scene)count=Math.Max(count,(p.timeLeft+p.extraUpdates)/(p.extraUpdates+1));
            for(int step=0;step<Math.Min(count,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,path,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
