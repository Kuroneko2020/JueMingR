using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI013 has a sealed launch followed by release/range/hit-driven return.
    // A flying fist is not a steerable controller. This pure continuation
    // advances independent projectile state and the existing player forecast.
    internal static class HostHarpoonAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==23 || type==262 || type==271 || type==273 || type==481;}
        internal static Projectile Copy(Projectile source)
        {
            var result=(Projectile)copy.Invoke(source,null);result.ai=(float[])source.ai.Clone();result.localAI=(float[])source.localAI.Clone();
            result.localNPCImmunity=(int[])source.localNPCImmunity.Clone();return result;
        }
        internal static bool Advance(Projectile shot,Player owner,PredictionTerrain terrain,out bool alive)
        {
            alive=false;
            if(owner.dead || !owner.active || Vector2.Distance(owner.Center,shot.Center)>2000 || shot.position.X<=Main.leftWorld || shot.position.Y<=Main.topWorld || shot.position.X+shot.width>=Main.rightWorld || shot.position.Y+shot.height>=Main.bottomWorld)return true;
            // Native captures this before AI. With tileCollide=false it is
            // the full old velocity, not the usual wet tile-motion multiplier.
            Vector2 wetVelocity=shot.velocity;
            if(shot.type==481)shot.extraUpdates=shot.ai[0]==0?1:2;
            Vector2 delta=owner.position+new Vector2(owner.width/2,owner.height/2)-shot.Center;
            float distance=delta.Length();
            if(shot.type==23 && shot.ai[1]>10 && !owner.controlUseItem)shot.ai[0]=1;
            if(shot.ai[0]==0)
            {
                float range=shot.type==23?1000:shot.type==262?500:shot.type==271?200:shot.type==273?(Main.remixWorld?300:150):525;
                if(distance>range)shot.ai[0]=1;
                shot.ai[1]++;
                if(shot.ai[1]>5)shot.alpha=0;
                if(shot.type!=23 && shot.ai[1]>8)shot.ai[1]=8;
                if(shot.type==23 && shot.ai[1]>=17){shot.ai[1]=17;shot.velocity.Y+=.6f;shot.velocity.X*=.995f;}
                // KO Cannon's range return expressly suppresses the 200px
                // one-action hitbox used by its collision/hit return.
                if(shot.type==262 && shot.ai[0]!=0)shot.localAI[0]=-1;
            }
            else if(shot.ai[0]==1)
            {
                if(shot.type==262 && shot.localAI[0]==0)shot.localAI[0]=Vector2.Distance(shot.Center,owner.Center)<150?-1:200;
                shot.tileCollide=false;
                if(distance<50)return true;
                shot.velocity=delta*((shot.type==262?30:20)/distance);
            }
            float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X,out wind))return false;shot.velocity.X+=wind;
            if(shot.shimmerWet)return false;
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(shot.Center.X,shot.Center.Y,shot.width,shot.height,out wet,out kind) || wet && kind==3 || !terrain.ProjectileLava(shot.Center.X,shot.Center.Y,shot.width,shot.height,out lava))return false;
                if(lava)shot.lavaWet=true;if(wet && kind==2)shot.honeyWet=true;shot.wet=wet;if(!wet)shot.lavaWet=shot.honeyWet=false;
            }
            Vector2 position=shot.position,velocity=shot.velocity;bool hit;
            if(shot.tileCollide)
            {
                if(!terrain.ProjectileTileMotion(shot,ref position,ref velocity,shot.wet,shot.honeyWet,out wetVelocity,out hit))return false;
                if(hit){if(shot.type==23)velocity*=0;shot.ai[0]=1;}
            }
            shot.velocity=velocity;shot.position=position+(shot.wet?wetVelocity:velocity);alive=true;return true;
        }
        internal static Rectangle DamageBody(Projectile shot)
        {
            var box=new Rectangle((int)shot.position.X,(int)shot.position.Y,shot.width,shot.height);
            if(shot.type==262 && shot.localAI[0]>0){box.Inflate(-shot.width/2,-shot.height/2);box.Inflate((int)(shot.localAI[0]/2),(int)shot.localAI[0]/2);shot.localAI[0]=-1;}
            return box;
        }
        private static void OnOtherHit(Projectile shot,HostAttackObstacles obstacles,int prior)
        {
            if(obstacles.OtherHits==prior)return;
            if(shot.type!=23){shot.ai[0]=1;return;}
            shot.penetrate=obstacles.Remaining;
            if(shot.penetrate==0){if(shot.ai[0]==0)shot.velocity*=0;shot.penetrate=-1;shot.ai[0]=1;obstacles.MakeUnlimited();}
        }
        private static AttackContact Replay(Player player,Projectile shot,NpcTrajectory timeline,int first,int firstOuter,bool moveFirst,Vector2 aim,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            var body=(Player)copy.Invoke(player,null);var motion=NpcPredictionSource.ReadPlayer(player);
            var world=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            obstacles.BeginCandidate();int actions=0;
            for(int step=0;step<120 && first+step<timeline.Count;step++)
            {
                if(moveFirst || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,world,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);
                // Update latches extraUpdates once BEFORE its while loop.
                // GolemFist changes that field inside AI for the next frame.
                int updates=shot.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    bool alive;if(!Advance(shot,body,terrain,out alive) || !alive)return null;
                    var box=DamageBody(shot);int previous=obstacles.OtherHits;
                    bool pass=obstacles.PassShapeOuter(++actions,first+step,firstOuter+step,box.Center.X,box.Center.Y,box.Width,box.Height);
                    bool spentHarpoon=shot.type==23 && obstacles.Remaining==0 && obstacles.OtherHits>previous;
                    OnOtherHit(shot,obstacles,previous);
                    if(pass && obstacles.SelectedAllows(firstOuter+step))
                    {
                        var contact=AttackIntercept.BodyContact(timeline,first+step,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,AttackConfidence.Conditional);
                        if(contact!=null)return contact;
                    }
                    if(!pass && !spentHarpoon)return null;
                    if(--shot.timeLeft<=0)return null;
                }
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out HostAttackObstacles obstacles)
        {
            obstacles=new HostAttackObstacles(player,shot,timeline,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            return Replay(player,Copy(shot),timeline,clock.FirstTick,clock.LaunchDelay+1,clock.MovePlayer(0),shot.Center+shot.velocity*1000,terrain,obstacles);
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];var directions=new List<Vector2>(12);
            for(int seed=0;seed<9;seed++)
            {var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;Add(directions,(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction));}
            if(sample.type==23)
            {
                // Invert the real dry outgoing recurrence at each visible
                // target time. Only the best speed-consistent seed is replayed;
                // terrain/liquid/return still decide actual contact below.
                float best=float.MaxValue,damping=1,xWeight=0;Vector2 chosen=Vector2.Zero;
                for(int tick=1;tick<=120 && age+tick<timeline.Count;tick++)
                {
                    int falling=Math.Max(0,tick-16);if(falling>0)damping*=.995f;xWeight+=damping;
                    var b=timeline[age+tick].ProjectileReceiveBounds;
                    var direction=new Vector2((b.CenterX-origin.X)/(ammo.Speed*xWeight),(b.CenterY-origin.Y-.3f*falling*(falling+1))/(ammo.Speed*tick));
                    float error=Math.Abs(direction.LengthSquared()-1);if(error<best){best=error;chosen=direction.SafeNormalize(Vector2.UnitX*player.direction);}
                }
                Add(directions,chosen);
            }
            foreach(var unit in directions)
            {
                var shot=Copy(sample);shot.active=true;shot.owner=player.whoAmI;shot.Center=origin;shot.velocity=unit*ammo.Speed;
                var body=(Player)copy.Invoke(player,null);body.controlUseItem=true;
                // The caller has already advanced a CompletedWorld launch to
                // its next player action. Only later owner actions advance.
                var result=Replay(body,shot,timeline,age+1,delay+1,false,origin+unit*10000,terrain,obstacles);if(result!=null)return result;
            }
            return null;
        }
        private static void Add(List<Vector2> directions,Vector2 unit){if(unit!=Vector2.Zero && !directions.Contains(unit))directions.Add(unit);}
    }
}
