using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The seven hand staves share native feature bits, including the robe
    // combination. AI029 alone also includes unrelated autonomous roles.
    // These copies never call live AI, Damage, Kill, birth or RNG.
    internal static class HostGemStaffAttack
    {
        private static readonly Func<Player,int,float> pack=(Func<Player,int,float>)Delegate.CreateDelegate(typeof(Func<Player,int,float>),typeof(Player).GetMethod("PackGemStaffFeatures",BindingFlags.Instance|BindingFlags.NonPublic));
        internal static bool Handles(int type){return type>=121 && type<=126 || type==597;}
        internal static float Features(Player player,int type)
        {
            var flags=new Projectile.GemStaffFeatures(pack(player,type));
            if(flags.AllGems)flags.Homing=flags.CanBounce=flags.AoeExplosion=flags.FastThenSlow=true;
            return flags.Bits;
        }
        internal static float Speed(float speed,float bits)
        {var f=new Projectile.GemStaffFeatures(bits);if(f.FastThenSlow)speed*=5;if(f.CanBounce)speed*=1.25f;return speed;}
        internal static int Damage(Player player,Item item,float bits)
        {var f=new Projectile.GemStaffFeatures(bits);return (int)(player.GetWeaponDamage(item)*f.GetDamageMultiplier())+(f.RepeatsGem?4:0);}
#if DEBUG
        internal static int Candidates,MotorSteps,TargetQueries;
        internal static void ResetCounters(){Candidates=MotorSteps=TargetQueries=0;}
#endif
        internal sealed class State
        {
            internal Vector2 Position,Velocity,DeathCenter;internal readonly Vector2[] Old;
            internal float SwirlClock,Scale,Target;internal int Left,Penetration;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Killed;
            internal State(Projectile shot)
            {Position=shot.position;Velocity=shot.velocity;Old=(Vector2[])shot.oldPos.Clone();SwirlClock=shot.localAI[0];Scale=shot.scale;Target=shot.ai[2];Left=shot.timeLeft;Penetration=shot.penetrate;Alive=shot.active;Wet=shot.wet;Honey=shot.honeyWet;Lava=shot.lavaWet;Shimmer=shot.shimmerWet;}
        }
        private sealed class Member
        {
            internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;
            internal PredictionTerrain Terrain;internal Projectile.GemStaffFeatures Flags;internal bool Burst,GeometryKnown=true;
            internal Rectangle Witness;internal Func<MotionRect,bool> Collision;
            internal bool Collides(MotionRect target)
            {
                Rectangle witness;bool hit;
                if(!CollisionShape(Shot,State,Terrain,target,Burst,out witness,out hit)){GeometryKnown=false;return false;}
                Witness=witness;return hit;
            }
        }
        internal static Projectile PrivateBirth(int type,int owner,float bits,int twin,Vector2 center,Vector2 velocity,int damage,PredictionTerrain terrain)
        {
            var shot=new Projectile();shot.SetDefaults(type);shot.active=true;shot.owner=owner;shot.Center=center;shot.velocity=velocity;shot.damage=damage;shot.ai[0]=bits;shot.ai[1]=twin;
            // This exact type's Finalize only adjusts penetration, lifetime
            // and armor penetration. It has no game or random side effect.
            shot.FinalizeProjectile();bool wet;byte kind;
            if(!terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out wet,out kind,true))return null;
            shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;
            return shot;
        }
        private static void Kill(Projectile shot,State s)
        {if(s.Alive){s.DeathCenter=s.Position+new Vector2(shot.width,shot.height)*.5f;s.Alive=false;s.Killed=true;s.Left=0;}}
        private static bool Find(Projectile shot,State s,HostProjectileTargets targets,HostAttackObstacles receivers,NpcTrajectory timeline,int tick,int outer,PredictionTerrain terrain,float range,out Vector2 target,out int slot)
        {
            target=Vector2.Zero;slot=-1;var center=s.Position+new Vector2(shot.width,shot.height)*.5f;
            for(int i=0;i<targets.Count;i++)
            {
#if DEBUG
                TargetQueries++;
#endif
                int candidate=targets.Slot(i);if(!receivers.LocalTargetAllowed(candidate,outer) || candidate==timeline.Identity.Slot && !timeline[tick].CanReceive)continue;
                var box=targets.Bounds(i,timeline,tick);var point=new Vector2(box.CenterX,box.CenterY);float distance=Vector2.Distance(center,point);
                if(distance>=range)continue;bool clear;PredictionStop stop;
                if(!terrain.CanHit(new MotionRect(s.Position.X,s.Position.Y,shot.width,shot.height),box,out clear,out stop))return false;
                if(clear){range=distance;target=point;slot=candidate;}
            }
            return true;
        }
        internal static bool Step(Projectile shot,State s,HostProjectileTargets targets,HostAttackObstacles receivers,NpcTrajectory timeline,int tick,int outer,PredictionTerrain terrain)
        {
#if DEBUG
            MotorSteps++;
#endif
            if(!s.Alive)return true;
            if(s.Position.X<=Main.leftWorld || s.Position.X+shot.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+shot.height>=Main.bottomWorld)
            {s.Alive=false;return true;} // Native active=false does not call Kill.
            var flags=new Projectile.GemStaffFeatures(shot.ai[0]);Vector2 half=new Vector2(shot.width,shot.height)*.5f,wetVelocity=s.Velocity;
            if(flags.Homing)
            {
                float range=flags.FastThenSlow || flags.CanBounce?1000:500;Vector2 target;int slot;
                if(!Find(shot,s,targets,receivers,timeline,tick,outer,terrain,range,out target,out slot))return false;s.Target=slot+1;
                if(slot>=0)
                {
                    float speed=flags.FastThenSlow?16:8,length=s.Velocity.Length();
                    if(length<speed){s.Velocity+=s.Velocity.SafeNormalize(Vector2.Zero)*.1f;s.Velocity*=1.1f;}
                    Vector2 center=s.Position+half;float distance=Vector2.Distance(center,target),turn=Utils.Remap(distance,range,0,0,1)*.1f+.01f;
                    if(length<speed){turn+=.05f;turn*=Utils.Remap(length,1,3,10,1);}
                    if(flags.FastThenSlow)turn*=Utils.Remap(length,30,40,1,0);
                    s.Velocity=Utils.rotateTowards(center,s.Velocity,target,turn);
                    s.Velocity=Vector2.Lerp(s.Velocity,Vector2.Zero.MoveTowards(target-center,speed),Utils.Remap(distance,150,0,0,.2f));
                }
            }
            if(flags.SwirlTwins)
            {
                float phase=(int)shot.ai[1]*(float)Math.PI;
                float from=(float)Math.Sin(s.SwirlClock*((float)Math.PI*2)*.05f+phase),to=(float)Math.Sin((s.SwirlClock+1)*((float)Math.PI*2)*.05f+phase);s.SwirlClock++;
                s.Position+=(s.Velocity.ToRotation()+(float)Math.PI/2).ToRotationVector2()*(to-from)*6;
            }
            if(flags.FastThenSlow)
            {if(s.Velocity.Length()>.1f)s.Velocity*=.97f;else{s.Scale-=.005f;if(s.Scale<.1f){Kill(shot,s);return true;}}}
            float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,s.Position.X+half.X,s.Position.Y+half.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            bool wet,lava;byte kind;
            if(!terrain.ProjectileWet(s.Position.X+half.X,s.Position.Y+half.Y,shot.width,shot.height,out wet,out kind,true) || !terrain.ProjectileLava(s.Position.X+half.X,s.Position.Y+half.Y,shot.width,shot.height,out lava,true))return false;
            if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;s.Wet=wet;if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            if(s.Shimmer)
            {
                int x=(int)((s.Position.X+half.X)/16),y=(int)(s.Position.Y/16);PredictionTile tile,above;PredictionStop stop;
                if(!terrain.Tile(x,y,out tile,out stop))return false;
                if(tile.Liquid==255 && tile.ShimmerLiquid && y>0)
                {if(!terrain.Tile(x,y-1,out above,out stop))return false;if(above.Liquid>0 && above.ShimmerLiquid){Kill(shot,s);return true;}}
                if(s.Velocity.Y>0){s.Velocity.Y=-s.Velocity.Y;s.Shimmer=s.Wet=false;}
            }
            Vector2 prior=s.Velocity;bool hit;
            if(!terrain.ProjectileTileMotion(shot,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,shimmer:s.Shimmer,nativeClip:true))return false;
            if(hit)
            {
                if(flags.CanBounce)
                {
                    if(s.Velocity.X!=prior.X)s.Velocity.X=-prior.X*.8f;if(s.Velocity.Y!=prior.Y)s.Velocity.Y=-prior.Y*.8f;s.Penetration--;
                    if(flags.Homing)
                    {Vector2 target;int slot;if(!Find(shot,s,targets,receivers,timeline,tick,outer,terrain,2400,out target,out slot))return false;s.Target=slot+1;Vector2 delta=target-(s.Position+half);if(slot>=0 && delta.Length()>0)s.Velocity=delta.SafeNormalize(Vector2.UnitY)*prior.Length()*.8f;}
                    Array.Clear(s.Old,0,s.Old.Length);if(s.Penetration<=0){Kill(shot,s);return true;}
                }
                else if(flags.FastThenSlow){s.Position+=s.Velocity*.99f;s.Velocity=prior.SafeNormalize(Vector2.Zero)*.0001f;}
                else{Kill(shot,s);return true;}
            }
            s.Position+=s.Wet?wetVelocity:s.Velocity;return true;
        }
        internal static void Tail(Projectile shot,State s)
        {
            if(!s.Alive)return;
            for(int i=s.Old.Length-1;i>0;i--)s.Old[i]=s.Old[i-1];if(s.Old.Length>0)s.Old[0]=s.Position;
            if(--s.Left<=0 || s.Penetration==0)Kill(shot,s);
        }
        internal static bool CollisionShape(Projectile shot,State s,PredictionTerrain terrain,MotionRect target,bool burst,out Rectangle witness,out bool hit)
        {
            var flags=new Projectile.GemStaffFeatures(shot.ai[0]);int width=burst?160:shot.width,height=burst?160:shot.height;
            Vector2 center=burst?s.DeathCenter:s.Position+new Vector2(shot.width,shot.height)*.5f;
            var current=new Rectangle((int)(center.X-width*.5f),(int)(center.Y-height*.5f),width,height);var other=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
            // Damage_GetHitbox enlarges every AI029 body before Colliding.
            // Trails inherit that enlarged size but replace its top-left with
            // oldPos; a Kill burst follows the same 160 -> 168 damage shape.
            current.Inflate(4,4);
            hit=false;witness=Rectangle.Empty;bool clear;PredictionStop stop;
            if(flags.FastThenSlow)
            {
                for(int i=1;i<=5 && i<s.Old.Length;i++)
                {
                    if(s.Old[i]==Vector2.Zero)break;var trail=current;trail.X=(int)s.Old[i].X;trail.Y=(int)s.Old[i].Y;if(flags.BiggerHitbox)trail.Inflate(30,30);
                    if(!trail.Intersects(other))continue;
                    if(!terrain.CanHit(new MotionRect(trail.X,trail.Y,trail.Width,trail.Height),new MotionRect(other.X,other.Y,other.Width,other.Height),out clear,out stop))return false;
                    if(clear){hit=true;witness=trail;return true;}
                }
            }
            if(flags.BiggerHitbox)current.Inflate(30,30);if(!current.Intersects(other))return true;
            if(!terrain.CanHit(new MotionRect(center.X,center.Y,0,0),new MotionRect(other.Center.X,other.Center.Y,0,0),out clear,out stop))return false;
            hit=clear;witness=current;return true;
        }
        private static AttackContact Contact(Member m,NpcTrajectory timeline,int tick,int outer,Vector2 aim,AttackConfidence confidence)
        {
            int side=m.Burst?160:m.Shot.width;Vector2 center=m.Burst?m.State.DeathCenter:m.State.Position+new Vector2(m.Shot.width,m.Shot.height)*.5f;
            bool allowed=m.Receivers.PassCustomOuter(2,tick,outer,center.X,center.Y,side,m.Burst?160:m.Shot.height,m.Collision,m.Burst,m.State.Penetration);
            if(!m.GeometryKnown)return null;
            if(!allowed){if(m.Receivers.Remaining==0)Kill(m.Shot,m.State);else m.State.Alive=false;return null;}
            if(m.Receivers.SelectedAllows(outer) && m.Collides(timeline[tick].ProjectileReceiveBounds))
            {var b=m.Witness;return AttackIntercept.BodyContact(timeline,tick,0,aim.X,aim.Y,b.X+b.Width*.5f,b.Y+b.Height*.5f,b.Width,b.Height,confidence);}
            if(!m.Burst)m.State.Penetration=m.Receivers.Remaining;return null;
        }
        private static AttackContact Family(Member[] members,HostProjectileTargets targets,NpcTrajectory timeline,int age,int delay,Vector2 aim,AttackConfidence confidence)
        {
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int outer=1;outer<=limit;outer++)
            {
                bool live=false;int tick=age+outer;
                foreach(var m in members)
                {
                    if(!m.State.Alive)continue;live=true;
                    Vector2 before=m.State.Position+new Vector2(m.Shot.width,m.Shot.height)*.5f;
                    if(!m.Receivers.BeforeNpc(tick,delay+outer,before.X,before.Y)){m.State.Alive=false;continue;}
                    if(!Step(m.Shot,m.State,targets,m.Receivers,timeline,tick,delay+outer,m.Terrain))return null;
                    if(m.State.Alive)
                    {var body=Contact(m,timeline,tick,delay+outer,aim,confidence);if(!m.GeometryKnown)return null;if(body!=null)return body;}
                    Tail(m.Shot,m.State);
                    if(m.State.Killed && m.Flags.AoeExplosion && (int)(m.Shot.damage*(.35f/m.Flags.GetDamageMultiplier()))>0)
                    {
                        m.Burst=true;m.Receivers.MakeUnlimited();m.Receivers.ClearLocalExceptFresh(delay+outer);
                        var burst=Contact(m,timeline,tick,delay+outer,aim,confidence);if(!m.GeometryKnown)return null;if(burst!=null)return burst;
                    }
                }
                if(!live)break;
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay=0)
        {
            if(age<0 || age>=timeline.Count-1)return null;var flags=new Projectile.GemStaffFeatures(ammo.GemFeatures);
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];var targets=obstacles.CaptureChaseTargets(sample);
            bool representative=flags.ArPenSpread || flags.AllGems;int memberCount=flags.AllGems?3:flags.SwirlTwins?2:1;
            var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var box=timeline[Math.Min(timeline.Count-1,age+1+Math.Min(seed,8)*8)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
#if DEBUG
                Candidates++;
#endif
                obstacles.BeginCandidate();var members=new Member[memberCount];Vector2 birth=flags.ArPenSpread?origin+direction.RotatedBy(MathHelper.PiOver2)*12:origin;
                for(int i=0;i<memberCount;i++)
                {
                    Vector2 velocity=direction*ammo.Speed;if(flags.AllGems){velocity=velocity.RotatedBy((i-1)*(MathHelper.PiOver4/10));if(i==1)velocity*=1.1f;}
                    var shot=PrivateBirth(ammo.Projectile,player.whoAmI,ammo.GemFeatures,flags.AllGems?i+1:i,birth,velocity,ammo.Damage,terrain);if(shot==null)return null;
                    var m=new Member{Shot=shot,State=new State(shot),Receivers=obstacles.ForkForSpawn(shot),Terrain=terrain,Flags=flags};m.Collision=m.Collides;members[i]=m;
                }
                var contact=Family(members,targets,timeline,age,delay,origin+direction*10000,representative?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return contact;
            }
            return null;
        }
    }
}
