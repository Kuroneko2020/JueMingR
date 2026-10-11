using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // LaserRifle grows its native budget; PaintballGun has a random launch.
    // Legal future representatives never draw RNG or recolor native slimes.
    // The body is the fixed native hitbox, independent of drawing scale.
    internal static class HostLaserPaintAttack
    {
        internal static bool Handles(int type){return type==88 || type==587;}
        internal sealed class State
        {
            internal Vector2 Position,Velocity;
            internal float Counter,Visual,Scale,KnockBack;
            internal int Left,Extra,Damage,Penetration,MaximumPenetration,Alpha;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Stopped,Representative;
            internal State(Projectile p)
            {Position=p.position;Velocity=p.velocity;Counter=p.ai[0];Visual=p.localAI[0];Scale=p.scale;KnockBack=p.knockBack;Left=p.timeLeft;Extra=p.extraUpdates;Damage=p.damage;Penetration=p.penetrate;MaximumPenetration=p.maxPenetrate;Alpha=p.alpha;Alive=p.active;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;}
        }
        internal static Vector2 LimitLaunch(Vector2 v)
        {
            if(float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.X) || float.IsInfinity(v.Y))return new Vector2(float.NaN,float.NaN);
            while(v.X>=16 || v.X<=-16 || v.Y>=16 || v.Y< -16)v*=.97f;return v;
        }
        internal static bool PaintBirth(PredictionTerrain terrain,Vector2 playerCenter,Vector2 origin,Vector2 velocity,float gravDir,out Vector2 birth)
        {
            birth=origin;bool clear;PredictionStop stop;
            if(!terrain.CanHitLine(new MotionRect(playerCenter.X,playerCenter.Y,0,0),new MotionRect(origin.X+velocity.X*2,origin.Y+velocity.Y*2,0,0),out clear,out stop))return false;
            if(clear)birth+=velocity;birth.Y-=gravDir*4;return true;
        }
        internal static void Kill(State s){if(s.Alive){s.Alive=false;s.Left=0;}}
        // Observed native growth deltas are accepted only by the oracle's
        // motor comparison. Production defaults to the minimum legal draws.
        internal static bool Step(Projectile shot,State s,PredictionTerrain terrain,int growthDamage=1,int growthPenetration=0)
        {
            if(growthDamage<1 || growthDamage>3 || growthPenetration<0 || growthPenetration>1)return false;
            s.Stopped=false;
            if(s.Position.X<=Main.leftWorld || s.Position.X+shot.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+shot.height>=Main.bottomWorld || float.IsNaN(s.Position.X) || float.IsNaN(s.Position.Y))
            {s.Alive=false;s.Stopped=true;return true;}
            Vector2 wetVelocity=s.Velocity,half=new Vector2(shot.width,shot.height)*.5f;
            if(s.Alive)
            {
                if(shot.type==88)
                {
                    if(s.Counter!=0)s.Extra=(int)s.Counter;
                    if(s.Scale<4)
                    {
                        s.Scale+=.053f;if(s.Scale>4)s.Scale=4;
                        if(shot.owner==Main.myPlayer)
                        {
                            s.KnockBack*=1.0045f;
                            if(++s.Visual>=8){s.Visual=0;s.Damage+=growthDamage;s.Penetration+=growthPenetration;s.Representative=true;}
                        }
                    }
                    if(s.Alpha>0)s.Alpha-=10;if(s.Alpha<0)s.Alpha=0;
                }
                else
                {
                    s.Visual++;s.Counter++;if(s.Counter>=15){s.Counter=15;s.Velocity.Y+=.1f;}
                }
                if(s.Velocity.Y>16)s.Velocity.Y=16;
            }
            Vector2 center=s.Position+half;float wind;
            if(!HostProjectileEnvironment.Impulse(shot,terrain,center.X,center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            if(shot.owner<255 && !Main.player[shot.owner].active)Kill(s);
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,shot.width,shot.height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;s.Wet=wet;
                if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            if(s.Shimmer)
            {
                int x=(int)(center.X/16),y=(int)(s.Position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {
                    PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}
                }
                if(full)Kill(s);else if(s.Velocity.Y>0){s.Velocity.Y=-s.Velocity.Y;if(s.Left>600)s.Left=600;s.Left-=60;s.Wet=s.Shimmer=false;}
            }
            bool hit=false;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,true,s.Shimmer,true))return false;
            if(hit){s.Position+=s.Velocity;Kill(s);}
            s.Position+=s.Wet?wetVelocity:s.Velocity;s.Stopped=!s.Alive;return true;
        }
        internal static void Tail(State s)
        {if(!s.Stopped && (--s.Left<=0 || s.Penetration==0))Kill(s);}
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.owner=p.owner;q.whoAmI=p.whoAmI;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;q.width=p.width;q.height=p.height;q.scale=p.scale;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.alpha=p.alpha;
            q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.friendly=p.friendly;q.hostile=p.hostile;q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;
            q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static HostAttackObstacles Policy(Projectile shot,State s,HostAttackObstacles root)
        {
            // Growth changes penetrate but never maxPenetrate. Keep the real
            // max==1 owner-immunity gate, and the same candidate hit history.
            var q=Copy(shot);q.penetrate=s.Penetration;q.maxPenetrate=s.MaximumPenetration;q.damage=s.Damage;return root.ForkForSpawn(q);
        }
        private static AttackContact Replay(Projectile shot,NpcTrajectory timeline,int age,int delay,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,bool representative)
        {
            var s=new State(shot);s.Representative=representative;var receive=Policy(shot,s,root);
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int step=0;step<limit;step++)
            {
                int tick=age+step+1,outer=delay+step+1;
                if(!s.Alive)break;
                if(!receive.BeforeNpc(tick,outer,s.Position.X+shot.width*.5f,s.Position.Y+shot.height*.5f,shot.width,shot.height))break;
                // Native takes extraUpdates once at Update entry. An AI001
                // override takes effect for the next outer, not mid-subcycle.
                int updates=s.Extra+1;
                for(int sub=0;sub<updates;sub++)
                {
                    int damage=s.Damage,penetration=s.Penetration;
                    if(!Step(shot,s,terrain))return null;
                    if(damage!=s.Damage || penetration!=s.Penetration)receive=Policy(shot,s,root);
                    if(s.Alive && s.Damage>0 && shot.friendly && !shot.hostile && shot.owner==Main.myPlayer)
                    {
                        int selected=receive.SelectedHits;
                        bool passed=receive.PassCustomOuter(2,tick,outer,s.Position.X+shot.width*.5f,s.Position.Y+shot.height*.5f,shot.width,shot.height,null,false,s.Penetration,true);
                        if(receive.SelectedHits>selected)return AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,s.Position.X+shot.width*.5f,s.Position.Y+shot.height*.5f,shot.width,shot.height,s.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                        s.Penetration=receive.Remaining;if(!passed && s.Penetration!=0){s.Alive=false;break;}
                    }
                    if(s.Stopped)break;Tail(s);
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=(ammo.Projectile==88?514:3350) || ammo.Damage<=0 || delay<0 || age+1>=timeline.Count)return null;
            var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var q=new Projectile();q.SetDefaults(ammo.Projectile);q.active=true;q.owner=player.whoAmI;q.damage=ammo.Damage;q.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);q.velocity=LimitLaunch(direction*ammo.Speed);
                Vector2 center=origin;if(q.type==587 && !PaintBirth(terrain,player.Center,origin,q.velocity,player.gravDir,out center))return null;q.Center=center;
                bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;
                obstacles.BeginCandidate();var contact=Replay(q,timeline,age,delay,terrain,obstacles,origin+direction*10000,q.type==587);
                if(contact!=null)return contact;
            }
            return null;
        }
        // Native born oracle only. Existing autonomous shots gain no input
        // permission and do not suppress a still-usable hand's next launch.
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(shot==null || !shot.active || !Handles(shot.type) || shot.owner!=player.whoAmI || clock.LaunchDelay!=0)return null;
            var privateShot=Copy(shot);obstacles.CaptureProjectileFamily(new[]{shot});obstacles.BeginCandidate();
            return Replay(privateShot,timeline,clock.FirstTick-1,0,terrain,obstacles,shot.Center+shot.velocity*1000,false);
        }
    }
}
