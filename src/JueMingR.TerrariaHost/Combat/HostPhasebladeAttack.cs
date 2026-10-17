using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Release input is owned by Source, not this motor. Idle previews describe
    // one complete press update followed by release; a held use has no chosen
    // future release instant. Copies below never execute native AI or input.
    internal static class HostPhasebladeAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type)
        {return type>=1059 && type<=1072 || type>=1074 && type<=1076 || type==1101 || type==1102;}
        internal static bool SaberDamage(int type){return type>=1065 && type<=1070 || type==1072 || type==1076;}
#if DEBUG
        internal static int Candidates,MotorSteps;
        internal static void ResetCounters(){Candidates=MotorSteps=0;}
#endif
        private static Projectile Copy(Projectile shot)
        {var result=(Projectile)copy.Invoke(shot,null);result.ai=(float[])shot.ai.Clone();result.localAI=(float[])shot.localAI.Clone();result.localNPCImmunity=(int[])shot.localNPCImmunity.Clone();return result;}
        private static void Kill(Projectile shot){shot.active=false;shot.timeLeft=0;}
        private static float Approach(float value,float target)
        {if(value<target){value+=3;if(value<0 && target>0)value+=3;}else if(value>target){value-=3;if(value>0 && target<0)value-=3;}return value;}
        // One subupdate through AI and HandleMovement. Tail remains separate
        // because real Damage lies between movement and timeLeft decrement.
        internal static bool Advance(Projectile shot,Player owner,PredictionTerrain terrain,out bool alive)
        {
#if DEBUG
            MotorSteps++;
#endif
            alive=shot.active;Vector2 wetVelocity=shot.velocity;
            int tx=(int)(shot.Center.X/16),ty=(int)(shot.Center.Y/16);
            if(shot.owner==Main.myPlayer && shot.ai[0]!=1 && (tx<10 || ty<10 || tx>=Main.maxTilesX-10 || ty>=Main.maxTilesY-10))shot.ai[0]=1;
            if(shot.timeLeft<5 && owner.active && !owner.dead){shot.timeLeft=5;if(shot.owner==Main.myPlayer)shot.ai[0]=1;}
            if(Handles(owner.HeldItem.shoot) && shot.owner==Main.myPlayer && shot.ai[0]!=1 && owner.active && !owner.dead && owner.controlUseItem)shot.ai[0]=1;
            if(shot.ai[0]==0)
            {shot.ai[1]++;if(shot.ai[1]>=30){shot.velocity.Y+=1;shot.velocity.X*=.96f;if(shot.velocity.Y>16)shot.velocity.Y=16;}}
            else if(shot.ai[0]==2)
            {if(shot.velocity.Y<0)shot.velocity.Y*=.5f;shot.velocity.X=0;shot.velocity.Y+=1;}
            else
            {
                shot.tileCollide=false;
                Vector2 delta=owner.position+new Vector2(owner.width/2,owner.height/2)-shot.Center;
                float distance=(float)Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y);
                shot.extraUpdates=Math.Min(20,(int)(distance/3000)+(shot.type==1074?1:0));
                float scale=40f/distance;delta.X*=scale;delta.Y*=scale;
                shot.velocity.X=Approach(shot.velocity.X,delta.X);shot.velocity.Y=Approach(shot.velocity.Y,delta.Y);
                if(shot.owner==Main.myPlayer && shot.Hitbox.Intersects(owner.Hitbox))Kill(shot);
            }
            float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X,out wind))return false;shot.velocity.X+=wind;
            if(!owner.active)Kill(shot);
            if(shot.ignoreWater)shot.wet=false;
            else
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(shot.Center.X,shot.Center.Y,shot.width,shot.height,out wet,out kind,true) || !terrain.ProjectileLava(shot.Center.X,shot.Center.Y,shot.width,shot.height,out lava,true))return false;
                if(lava)shot.lavaWet=true;if(wet && kind==2)shot.honeyWet=true;if(wet && kind==3)shot.shimmerWet=true;
                shot.wet=wet;if(!wet)shot.lavaWet=shot.honeyWet=shot.shimmerWet=false;
            }
            if(shot.shimmerWet && shot.ai[0]!=1)
            {shot.ai[0]=shot.ai[1]=0;if(shot.velocity.Y> -11){if(shot.velocity.Y>16)shot.velocity.Y*=.9f;shot.velocity.Y-=.5f;}shot.velocity.X=MathHelper.Clamp(shot.velocity.X*1.1f,-14,14);}
            Vector2 position=shot.position,velocity=shot.velocity;bool hit=false;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref position,ref velocity,shot.wet,shot.honeyWet,out wetVelocity,out hit,true,shot.shimmerWet,true))return false;
            if(hit)shot.ai[0]=2;
            shot.velocity=velocity;shot.position=position+(shot.wet?wetVelocity:velocity);alive=shot.active;return true;
        }
        internal static void Tail(Projectile shot)
        {if(!shot.active)return;shot.timeLeft--;if(shot.timeLeft<=0)Kill(shot);}
        internal static Rectangle DamageBox(Projectile shot)
        {var box=shot.Hitbox;if(shot.ai[0]==2)box.Inflate(-14,0);return box;}
        private static bool Move(ref PredictionPlayerMotion motion,Player body,PredictionEnvironment environment,PredictionTerrain terrain)
        {PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return false;HostAttackPlayerFrame.ApplyPose(body,motion);return true;}
        private static AttackContact Replay(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles,Vector2 aim,bool released,PredictionPlayerMotion initialMotion,PredictionEnvironment world)
        {
            var body=(Player)copy.Invoke(player,null);if(released)body.controlUseItem=false;
            // Every bounded candidate copies the same captured owner scalar
            // state. Do not rescan unchanged player inputs for each angle.
            var motion=initialMotion;
            // ReleaseSolve has prepared this independent launch body once.
            // Known instead starts at its actual observed phase and advances
            // the first owner step only when clock.MovePlayer requires it.
            obstacles.BeginCandidate();int limit=Math.Min(120,timeline.Count-clock.FirstTick);
            for(int step=0;step<limit && shot.active;step++)
            {
                if((released?step>0:clock.MovePlayer(step)) && !Move(ref motion,body,world,terrain))return null;
                int tick=clock.FirstTick+step,outer=clock.LaunchDelay+step+1,updates=shot.extraUpdates+1;
                // Native snapshots numUpdates at entry. AI's return distance
                // may change extraUpdates, but only the next outer uses it.
                for(int sub=0;sub<updates;sub++)
                {
                    bool alive;if(!Advance(shot,body,terrain,out alive))return null;if(!alive)break;
                    if(shot.damage>0)
                    {
                        var box=DamageBox(shot);
                        if(!obstacles.PassShapeOuter(2,tick,outer,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height))return null;
                        if(obstacles.SelectedAllows(outer))
                        {var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height,AttackConfidence.Conditional);if(contact!=null)return contact;}
                    }
                    Tail(shot);
                }
            }
            return null;
        }
        internal static AttackContact ReleaseSolve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(ammo==null || !Handles(ammo.Projectile) || obstacles==null || clock.FirstTick<0 || clock.FirstTick>=timeline.Count || clock.LaunchDelay<0)return null;
            var sample=new Projectile();sample.SetDefaults(ammo.Projectile);sample.owner=player.whoAmI;sample.active=true;sample.damage=player.GetWeaponDamage(player.HeldItem);
            if(sample.damage<=0)return null;
            // Origin is the current baseline, not an already advanced body.
            // Adjust direction for the actual private launch position below.
            var body=(Player)copy.Invoke(player,null);var motion=NpcPredictionSource.ReadPlayer(player);var world=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            for(int wait=0;wait<clock.LaunchDelay+(clock.NextWorld?1:0);wait++)if(!Move(ref motion,body,world,terrain))return null;
            origin=HostAttackModels.LaunchOrigin(body,player.HeldItem);
            var used=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var box=timeline[Math.Min(timeline.Count-1,clock.FirstTick+seed%9*4)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(used[i]==direction){duplicate=true;break;}if(duplicate)continue;used[count++]=direction;
#if DEBUG
                Candidates++;
#endif
                var shot=Copy(sample);shot.Center=origin;shot.velocity=direction*ammo.Speed;
                var contact=Replay(body,shot,timeline,clock,terrain,obstacles,origin+direction*10000,true,motion,world);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile actual,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(actual==null || !actual.active || !Handles(actual.type) || actual.owner!=player.whoAmI || clock.LaunchDelay!=0 || obstacles==null || clock.FirstTick<0 || clock.FirstTick>=timeline.Count)return null;
            var motion=NpcPredictionSource.ReadPlayer(player);var world=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            return Replay(player,Copy(actual),timeline,clock,terrain,obstacles,actual.Center+actual.velocity*1000,false,motion,world);
        }
    }
}
