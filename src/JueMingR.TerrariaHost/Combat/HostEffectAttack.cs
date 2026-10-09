using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostEffectAttack
    {
        internal static bool Handles(int type){return type==207 || type==36 || type==134 || type==137 || type==140 || type==143;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(ammo.Projectile==36)return Bounce(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles);
            if(ammo.Projectile!=207)return Rocket(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles);
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            // A player bullet may directly hit an admitted target that native
            // homing does not chase (notably an immortal training dummy).
            var target=Main.npc[timeline.Identity.Slot];if(target.dontTakeDamage)return null;
            int updates=sample.extraUpdates+1,limit=Math.Min(120,timeline.Count-age-1);
            // Native autonomous selection is an independent stage. Read its
            // currently known candidates; do not steer a born bullet or claim
            // that a different native target must remain the selected NPC.
            for(int seed=0;seed<9;seed++)
            {
                var seedBox=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].Bounds;Vector2 aim=origin+(new Vector2(seedBox.CenterX,seedBox.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction)*10000;
                Vector2 velocity=(aim-origin).SafeNormalize(Vector2.UnitX)*ammo.Speed;while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
                float initialSpeed=velocity.Length();Vector2 center=origin;int locked=-1;
                for(int step=0;step<limit;step++)for(int sub=0;sub<updates;sub++)
                {
                    int tick=age+step+1;var body=timeline[tick].Bounds;
                    if(locked<0)
                    {
                        float nearest=300;
                        for(int slot=0;slot<Main.maxNPCs;slot++)
                        {
                            var npc=Main.npc[slot];if(!npc.active || !npc.CanBeChasedBy(null))continue;
                            var bounds=slot==timeline.Identity.Slot?body:new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height);float distance=Math.Abs(center.X-bounds.CenterX)+Math.Abs(center.Y-bounds.CenterY);
                            bool clear;PredictionStop stop;if(distance<nearest && terrain.CanHit(new MotionRect(center.X,center.Y,1,1),bounds,out clear,out stop) && clear){locked=slot;nearest=distance;}
                        }
                    }
                    if(locked>=0)
                    {
                        var npc=Main.npc[locked];var bounds=locked==timeline.Identity.Slot?body:new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height);var delta=new Vector2(bounds.CenterX,bounds.CenterY)-center;
                        if(!npc.active || !npc.CanBeChasedBy(null,true) || npc.dontTakeDamage)locked=-1;
                        else if(Math.Abs(delta.X)+Math.Abs(delta.Y)<1000)velocity=(velocity*7+delta.SafeNormalize(Vector2.UnitX)*initialSpeed)/8;
                    }
                    Vector2 old=center;if(!HostProjectileEnvironment.Dry(sample,terrain,old.X,old.Y,velocity.X)){step=limit;break;}
                    center+=velocity;if(!terrain.ProjectilePassage(old.X,old.Y,center.X,center.Y,sample.width,sample.height)){step=limit;break;}
                    if(!obstacles.Pass(step*updates+sub+1,tick,center.X,center.Y)){step=limit;break;}
                    if(receive.Allows(step+1))
                    {
                        var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,center.X,center.Y,sample.width,sample.height,AttackConfidence.Representative);if(contact!=null)return contact;
                    }
                }
            }
            return null;
        }
        private static AttackContact Rocket(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);AttackMotion motion;if(!HostAttackModels.TryRead(ammo,out motion))return null;
            AttackPassage passage=(x,y,nx,ny,w,h)=>HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x) && terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h);
            var ordinary=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0,passage,1,receive.Allows,obstacles.Pass);if(ordinary!=null)return ordinary;
            int limit=Math.Min(motion.Lifetime,Math.Min(120*motion.Updates,(timeline.Count-age-1)*motion.Updates));
            // Solid contact stops these rockets and sets timeLeft=3. Their
            // next AI_016 sees <=3 and Kill/Resize/Damage occurs at that stopped
            // centre, before its later visual reset. Read the finite rectangle;
            // never run Kill/PrepareBomb/Damage as a prediction getter.
            for(int seed=0;seed<9;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;Vector2 aim=origin+(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction)*10000;
                Vector2 velocity=(aim-origin).SafeNormalize(Vector2.UnitX)*ammo.Speed;float x=origin.X,y=origin.Y,vx=velocity.X,vy=velocity.Y;
                for(int k=1;k<=limit;k++)
                {
                    float oldX=x,oldY=y;motion.Advance(ref x,ref y,ref vx,ref vy,k);int step=(k-1)/motion.Updates,tick=age+step+1;
                    float rx,ry;if(!HostProjectileEnvironment.Dry(sample,terrain,oldX,oldY,vx) || !terrain.ProjectileCollision(oldX,oldY,vx,vy,(int)motion.Width,(int)motion.Height,out rx,out ry))break;
                    if(rx!=vx || ry!=vy)
                    {
                        int killStep=k/motion.Updates,killTick=age+killStep+1;
                        if(terrain.Unchanged && receive.Allows(killStep+1) && killTick<timeline.Count)
                        {int side=ammo.Projectile==134 || ammo.Projectile==137?128:200;var contact=AttackIntercept.BodyContact(timeline,killTick,k%motion.Updates,aim.X,aim.Y,oldX,oldY,side,side,AttackConfidence.Conditional);if(contact!=null)return contact;}
                        break;
                    }
                    if(!obstacles.Pass(k,tick,x,y))break;
                }
            }
            return null;
        }
        private static AttackContact Bounce(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[36];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);int updates=sample.extraUpdates+1,limit=Math.Min(sample.timeLeft,(timeline.Count-age-1)*updates);
            // One ordinary MeteorShot reflection consumes one penetration.
            // Finite angular representatives can find a first reflected route;
            // its body still damages only at discrete native movement endpoints.
            for(int seed=-1;seed<32;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1)].ProjectileReceiveBounds;Vector2 unit=seed<0?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):(seed*MathHelper.TwoPi/32).ToRotationVector2();Vector2 aim=origin+unit*10000,velocity=unit*ammo.Speed,center=origin;
                while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;int remaining=sample.penetrate;
                for(int k=1;k<=limit;k++)
                {
                    float rx,ry;if(!HostProjectileEnvironment.Dry(sample,terrain,center.X,center.Y,velocity.X) || !terrain.ProjectileCollision(center.X,center.Y,velocity.X,velocity.Y,sample.width,sample.height,out rx,out ry))break;
                    bool horizontal=Math.Abs(rx-velocity.X)>.0001f,vertical=Math.Abs(ry-velocity.Y)>.0001f;
                    if(horizontal || vertical){if(remaining<=1)break;remaining--;if(horizontal)velocity.X=-velocity.X;if(vertical)velocity.Y=-velocity.Y;}
                    center+=velocity;int step=(k-1)/updates,tick=age+step+1;
                    if(!obstacles.Pass(k,tick,center.X,center.Y,remaining))break;remaining=obstacles.Remaining;
                    if(!receive.Allows(step+1))continue;
                    var contact=AttackIntercept.BodyContact(timeline,tick,(k-1)%updates,aim.X,aim.Y,center.X,center.Y,sample.width,sample.height,AttackConfidence.Conditional);if(contact!=null)return contact;
                }
            }
            return null;
        }
    }
}
