using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

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
        internal static int Candidates,MotorSteps,TargetQueries,FlightCaptures,FlightPoolScans;
        internal static void ResetCounters(){Candidates=MotorSteps=TargetQueries=FlightCaptures=FlightPoolScans=0;}
#endif
        // Observation of already born gems has no input receipt. All local
        // gem roles can clear shared owner immunity; only the held role may
        // contribute a Contact. Full scene/terrain checks belong to Prepare
        // or Consume, while Draw uses the captured scalar member lease.
        internal sealed class FlightState
        {
            private sealed class Row
            {
                private readonly Projectile shot;private readonly ProjectileKey key;
                private readonly Vector2 position,velocity;private readonly int slot,type,owner,left,extra,damage,penetration,maximum,width,height,localCooldown,staticCooldown,immunityIdentity;
                private readonly float bits,twin,target,swirl,scale;private readonly bool wet,honey,lava,shimmer,tile,friendly,hostile,ignoreWater,correctSlope,localImmunity,staticImmunity,singleImmunity;
                private readonly int[] local;private readonly Vector2[] old;
                internal Row(Projectile p)
                {shot=p;key=p.key;slot=p.whoAmI;type=p.type;owner=p.owner;position=p.position;velocity=p.velocity;left=p.timeLeft;extra=p.extraUpdates;damage=p.damage;penetration=p.penetrate;maximum=p.maxPenetrate;width=p.width;height=p.height;bits=p.ai[0];twin=p.ai[1];target=p.ai[2];swirl=p.localAI[0];scale=p.scale;wet=p.wet;honey=p.honeyWet;lava=p.lavaWet;shimmer=p.shimmerWet;tile=p.tileCollide;friendly=p.friendly;hostile=p.hostile;ignoreWater=p.ignoreWater;correctSlope=p.correctSlopeCollision;localImmunity=p.usesLocalNPCImmunity;staticImmunity=p.usesIDStaticNPCImmunity;singleImmunity=p.appliesImmunityTimeOnSingleHits;localCooldown=p.localNPCHitCooldown;staticCooldown=p.idStaticNPCHitCooldown;immunityIdentity=p.immunityIdentity;local=(int[])p.localNPCImmunity.Clone();old=(Vector2[])p.oldPos.Clone();}
                internal bool MembersCurrent
                {get{return shot.active && ReferenceEquals(shot,Main.projectile[slot]) && key.Equals(shot.key) && shot.type==type && shot.owner==owner && shot.position==position && shot.velocity==velocity && shot.timeLeft==left && shot.extraUpdates==extra && shot.damage==damage && shot.penetrate==penetration && shot.maxPenetrate==maximum && shot.width==width && shot.height==height && shot.ai[0]==bits && shot.ai[1]==twin && shot.ai[2]==target && shot.localAI[0]==swirl && shot.scale==scale && shot.wet==wet && shot.honeyWet==honey && shot.lavaWet==lava && shot.shimmerWet==shimmer && shot.tileCollide==tile && shot.friendly==friendly && shot.hostile==hostile && shot.ignoreWater==ignoreWater && shot.correctSlopeCollision==correctSlope && shot.usesLocalNPCImmunity==localImmunity && shot.usesIDStaticNPCImmunity==staticImmunity && shot.appliesImmunityTimeOnSingleHits==singleImmunity && shot.localNPCHitCooldown==localCooldown && shot.idStaticNPCHitCooldown==staticCooldown && shot.immunityIdentity==immunityIdentity;}}
                internal bool Current
                {get{if(!MembersCurrent || shot.localNPCImmunity.Length!=local.Length || shot.oldPos.Length!=old.Length)return false;for(int i=0;i<local.Length;i++)if(shot.localNPCImmunity[i]!=local[i])return false;for(int i=0;i<old.Length;i++)if(shot.oldPos[i]!=old[i])return false;return true;}}
            }
            private readonly Player player;private readonly int heldType;private readonly Row[] rows;private readonly bool[] captured;
            private readonly HostAttackObstacles receivers;private readonly PredictionTerrain terrain;private readonly HostProjectileEnvironment.State environment;
            internal FlightState(Player player,Projectile[] members,HostAttackObstacles receivers,PredictionTerrain terrain)
            {
                this.player=player;heldType=player.HeldItem.shoot;this.receivers=receivers;this.terrain=terrain;environment=new HostProjectileEnvironment.State(true);rows=new Row[members.Length];captured=new bool[Main.maxProjectiles];
                for(int i=0;i<members.Length;i++){rows[i]=new Row(members[i]);captured[members[i].whoAmI]=true;}
#if DEBUG
                FlightCaptures++;
#endif
            }
            internal bool MembersCurrent
            {get{if(player.HeldItem.shoot!=heldType)return false;foreach(var row in rows)if(!row.MembersCurrent)return false;return true;}}
            internal bool Current
            {get{return MembersCurrent && terrain.Unchanged && DependenciesCurrent;}}
            // The presentation owner freezes this solve's terrain separately.
            // Keep all other original full-family/receive dependencies here;
            // this preparation-only check must never run from Draw.
            internal bool DependenciesCurrent
            {
                get
                {
                    if(!MembersCurrent || !environment.Current || !receivers.Unchanged)return false;foreach(var row in rows)if(!row.Current)return false;
#if DEBUG
                    FlightPoolScans++;
#endif
                    for(int slot=0;slot<Main.maxProjectiles;slot++){var p=Main.projectile[slot];if(p!=null && p.active && p.owner==player.whoAmI && Handles(p.type) && !captured[slot])return false;}return true;
                }
            }
        }
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
            internal PredictionTerrain Terrain;internal Projectile.GemStaffFeatures Flags;internal bool Burst,GeometryKnown=true,Primary=true;
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
        private static AttackContact Contact(Member m,NpcTrajectory timeline,int tick,int outer,int sub,Vector2 aim,AttackConfidence confidence)
        {
            int side=m.Burst?160:m.Shot.width;Vector2 center=m.Burst?m.State.DeathCenter:m.State.Position+new Vector2(m.Shot.width,m.Shot.height)*.5f;
            AttackContact contact=null;
            bool allowed=m.Receivers.PassCustomOuter(2,tick,outer,center.X,center.Y,side,m.Burst?160:m.Shot.height,m.Collision,m.Burst,m.State.Penetration,true,(slot,remaining)=>
            {
                if(m.Primary && slot==timeline.Identity.Slot && m.Collides(timeline[tick].ProjectileReceiveBounds))
                {var b=m.Witness;contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,b.X+b.Width*.5f,b.Y+b.Height*.5f,b.Width,b.Height,confidence);}return true;
            });
            // A later slot's dependency cannot undo the already received
            // selected source event. Ambient selected receives still write
            // shared history and consume penetration without making a mark.
            if(contact!=null)return contact;
            if(!m.GeometryKnown)return null;
            if(!allowed){if(m.Receivers.Remaining==0)Kill(m.Shot,m.State);else m.State.Alive=false;return null;}
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
                    int updates=m.Shot.extraUpdates+1;
                    for(int sub=0;sub<updates && m.State.Alive;sub++)
                    {
                        if(!Step(m.Shot,m.State,targets,m.Receivers,timeline,tick,delay+outer,m.Terrain))return null;
                        if(m.State.Alive && m.Shot.friendly && !m.Shot.hostile && m.Shot.damage>0)
                        {var body=Contact(m,timeline,tick,delay+outer,sub,aim,confidence);if(body!=null)return body;if(!m.GeometryKnown)return null;}
                        Tail(m.Shot,m.State);
                        if(m.State.Killed && m.Shot.friendly && !m.Shot.hostile && m.Flags.AoeExplosion && (int)(m.Shot.damage*(.35f/m.Flags.GetDamageMultiplier()))>0)
                        {
                            m.Burst=true;m.Receivers.MakeUnlimited();m.Receivers.ClearLocalExceptFresh(delay+outer);
                            var burst=Contact(m,timeline,tick,delay+outer,sub,aim,confidence);if(burst!=null)return burst;if(!m.GeometryKnown)return null;
                        }
                    }
                }
                if(!live)break;
            }
            return null;
        }
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=true;q.owner=p.owner;q.whoAmI=p.whoAmI;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;q.width=p.width;q.height=p.height;
            q.ai[0]=p.ai[0];q.ai[1]=p.ai[1];q.ai[2]=p.ai[2];q.localAI[0]=p.localAI[0];q.scale=p.scale;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.friendly=p.friendly;q.hostile=p.hostile;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;q.trap=p.trap;q.npcProj=p.npcProj;q.usesOwnerMeleeHitCD=p.usesOwnerMeleeHitCD;
            Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] observed,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out FlightState state)
        {
            state=null;if(observed==null || observed.Length==0 || !Handles(player.HeldItem.shoot) || clock.LaunchDelay!=0 || clock.FirstTick<0 || clock.FirstTick>=timeline.Count)return null;
            var members=new Member[observed.Length];HostAttackObstacles receivers=null;bool source=false;int after=-1;
            for(int i=0;i<observed.Length;i++)
            {
                var p=observed[i];if(p==null || !p.active || p.owner!=player.whoAmI || !Handles(p.type) || p.whoAmI<=after || p.extraUpdates<0 || p.extraUpdates>15)return null;after=p.whoAmI;
                var copy=Copy(p);if(receivers==null){receivers=new HostAttackObstacles(player,copy,timeline,0,clock.BeforeNpc,clock.NextWorld);receivers.BeginCandidate();}
                var m=new Member{Shot=copy,State=new State(p),Receivers=receivers.ForkForSpawn(copy),Terrain=terrain,Flags=new Projectile.GemStaffFeatures(p.ai[0]),Primary=p.type==player.HeldItem.shoot && p.friendly && !p.hostile};if(!p.friendly || p.hostile)m.State.Alive=false;m.Collision=m.Collides;members[i]=m;source|=m.Primary;
            }
            if(!source)return null;var targets=receivers.CaptureChaseTargets(members[0].Shot);state=new FlightState(player,observed,receivers,terrain);
            var aim=observed[0].Center+observed[0].velocity*1000;
            return Family(members,targets,timeline,clock.FirstTick-1,0,aim,AttackConfidence.Conditional);
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
