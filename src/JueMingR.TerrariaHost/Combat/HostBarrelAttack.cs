using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The barrel's own Kill calls Damage again on the SAME projectile. Its
    // local immunity and positive/nonpositive budget survive that transition.
    // Drawing alpha does not gate damage, and world retirement is not Kill.
    internal static class HostBarrelAttack
    {
        internal static bool Handles(int type){return type==1023;}
        internal sealed class State
        {
            internal Vector2 Position,Velocity,BurstCenter;
            internal float Counter,Started,Bounced,Rotation;
            internal int Left,Width,Height,Alpha;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Burst,Retired;
            private readonly Projectile movement;
            internal Vector2 Center{get{return Position+new Vector2(Width,Height)*.5f;}}
            internal State(Projectile q)
            {
                Position=q.position;Velocity=q.velocity;Counter=q.ai[0];Started=q.ai[1];Bounced=q.localAI[1];Rotation=q.rotation;
                Left=q.timeLeft;Width=q.width;Height=q.height;Alpha=q.alpha;Alive=q.active;Wet=q.wet;Honey=q.honeyWet;Lava=q.lavaWet;Shimmer=q.shimmerWet;
                movement=Copy(q);
            }
            internal Projectile Movement{get{movement.width=Width;movement.height=Height;movement.position=Position;movement.velocity=Velocity;return movement;}}
        }
        private sealed class Shape
        {
            internal Rectangle Damage;
            internal bool Overlaps(MotionRect b){return Damage.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height));}
            internal void At(Vector2 center,int nativeSide){Damage=new Rectangle((int)(center.X-nativeSide*.5f),(int)(center.Y-nativeSide*.5f),nativeSide,nativeSide);Damage.Inflate(8,8);}
        }
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.owner=p.owner;q.whoAmI=p.whoAmI;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;
            q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.timeLeft=p.timeLeft;q.alpha=p.alpha;q.extraUpdates=p.extraUpdates;
            q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.friendly=p.friendly;q.hostile=p.hostile;q.ranged=p.ranged;q.magic=p.magic;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;
            q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        internal static void Kill(State s)
        {
            if(!s.Alive)return;s.BurstCenter=s.Center;s.Burst=true;s.Alive=false;s.Left=0;
            // Native resizes before Damage and retains this body after Kill.
            s.Width=s.Height=192;s.Position=s.BurstCenter-new Vector2(96);
        }
        internal static bool Step(State s,PredictionTerrain terrain)
        {
            s.Burst=false;if(!s.Alive)return true;
            if(s.Position.X<=Main.leftWorld || s.Position.X+s.Width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+s.Height>=Main.bottomWorld || float.IsNaN(s.Position.X) || float.IsNaN(s.Position.Y))
            {s.Alive=false;s.Retired=true;return true;}
            Vector2 wetVelocity=s.Velocity;
            if(s.Started==0)s.Started=1;
            s.Counter++;if(s.Counter>=6)s.Alpha=0;if(s.Counter>=20){s.Counter=30;s.Velocity.Y+=.25f;}if(s.Velocity.Y>16)s.Velocity.Y=16;
            s.Rotation+=MathHelper.Clamp(s.Velocity.X*.025f,MathHelper.Pi/16,MathHelper.Pi/6);
            var shot=s.Movement;float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,s.Center.X,s.Center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            if(shot.owner<255 && !Main.player[shot.owner].active)Kill(s);
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;var center=s.Center;
                if(!terrain.ProjectileWet(center.X,center.Y,s.Width,s.Height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,s.Width,s.Height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;s.Wet=wet;if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            if(s.Shimmer)
            {
                var center=s.Center;int x=(int)(center.X/16),y=(int)(s.Position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {
                    PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}
                }
                if(full)Kill(s);else if(s.Velocity.Y>0){s.Velocity.Y=-s.Velocity.Y;if(s.Left>600)s.Left=600;s.Left-=60;s.Wet=s.Shimmer=false;}
            }
            var prior=s.Velocity;bool hit=false;shot=s.Movement;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,true,s.Shimmer,true))return false;
            if(hit)
            {
                if(s.Velocity.X!=prior.X){bool minimum=Math.Abs(s.Velocity.X-prior.X)<5;s.Velocity.X=-prior.X;if(minimum)s.Velocity.X=s.Velocity.X<0?-5:5;s.Bounced=1;}
                if(s.Velocity.Y!=prior.Y){s.Velocity.Y=prior.Y>0 && prior.Y<5?-5:-prior.Y;s.Bounced=1;}
            }
            s.Position+=s.Wet?wetVelocity:s.Velocity;return true;
        }
        internal static void Tail(State s,int remaining)
        {if(s.Alive && (--s.Left<=0 || remaining==0))Kill(s);}
        private static AttackContact Receive(State s,Vector2 center,int nativeSide,bool burst,NpcTrajectory timeline,int tick,int outer,int sub,Vector2 aim,HostAttackObstacles receive,Shape shape,Func<MotionRect,bool> collision)
        {
            shape.At(center,nativeSide);int hits=receive.SelectedHits;
            bool passed=receive.PassCustomOuter(2,tick,outer,center.X,center.Y,nativeSide,nativeSide,collision,burst && receive.Remaining<=0,0,true);
            if(receive.SelectedHits>hits)return AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,center.X,center.Y,nativeSide+16,nativeSide+16,AttackConfidence.Conditional);
            if(!passed && receive.Remaining!=0){s.Alive=false;s.Retired=true;s.Burst=false;}return null;
        }
        private static AttackContact Replay(Projectile shot,NpcTrajectory timeline,int age,int delay,PredictionTerrain terrain,HostAttackObstacles receive,Vector2 aim,Shape shape,Func<MotionRect,bool> collision)
        {
            var s=new State(shot);int limit=Math.Min(120,timeline.Count-age-1);
            for(int step=0;step<limit && s.Alive;step++)
            {
                int tick=age+step+1,outer=delay+step+1;
                if(!receive.BeforeNpc(tick,outer,s.Center.X,s.Center.Y,s.Width,s.Height))break;
                for(int sub=0;sub<=shot.extraUpdates && s.Alive;sub++)
                {
                    if(!Step(s,terrain))return null;AttackContact contact;
                    if(s.Burst){contact=Receive(s,s.BurstCenter,192,true,timeline,tick,outer,sub,aim,receive,shape,collision);if(contact!=null)return contact;break;}
                    if(!s.Alive)break;
                    contact=Receive(s,s.Center,26,false,timeline,tick,outer,sub,aim,receive,shape,collision);if(contact!=null)return contact;if(s.Retired)break;
                    Tail(s,receive.Remaining);
                    if(s.Burst){contact=Receive(s,s.BurstCenter,192,true,timeline,tick,outer,sub,aim,receive,shape,collision);if(contact!=null)return contact;break;}
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=1023 || ammo.WeaponType!=5460 || ammo.Damage<=0 || delay<0 || age+1>=timeline.Count)return null;
            var shot=new Projectile();shot.SetDefaults(1023);shot.active=true;shot.owner=player.whoAmI;shot.damage=ammo.Damage;shot.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
            var shape=new Shape();Func<MotionRect,bool> collision=shape.Overlaps;var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                shot.Center=origin;shot.velocity=direction*ammo.Speed;while(shot.velocity.X>=16 || shot.velocity.X<=-16 || shot.velocity.Y>=16 || shot.velocity.Y< -16)shot.velocity*=.97f;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,26,26,out wet,out kind,true))return null;shot.wet=wet;
                obstacles.BeginCandidate();var contact=Replay(shot,timeline,age,delay,terrain,obstacles,origin+direction*10000,shape,collision);if(contact!=null)return contact;
            }
            return null;
        }
        // Actual-born oracle only; an autonomous barrel gains no input lease.
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(shot==null || !shot.active || shot.type!=1023 || shot.owner!=player.whoAmI || !shot.friendly || shot.hostile || shot.damage<=0 || clock.LaunchDelay!=0)return null;
            var shape=new Shape();obstacles.CaptureProjectileFamily(new[]{shot});obstacles.BeginCandidate();
            return Replay(Copy(shot),timeline,clock.FirstTick-1,0,terrain,obstacles,shot.Center+shot.velocity*1000,shape,shape.Overlaps);
        }
    }
}
