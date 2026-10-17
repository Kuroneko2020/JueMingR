using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Three deterministic hand births, each with independent local immunity.
    // Native Damage tests the current body then its pre-Damage oldPos trail;
    // drawing scale/alpha do not replace those integer bodies. Native Kill
    // damages a 140px body with the SAME local immunity after its first hit.
    internal static class HostBetsyFlameAttack
    {
        internal static bool Handles(int type){return type==711;}
        private sealed class Member
        {
            internal Projectile Shot;internal bool Source,Reflected;
            internal HostAttackObstacles Receive;internal Rectangle Witness,LastOverlap;
            internal int Selected;
            internal AttackContact KillContact;internal NpcTrajectory Path;internal int Tick,Outer,Sub;
            internal Vector2 Aim;internal readonly Action<Vector2> OnKill;
            internal bool Collides(MotionRect box)
            {
                var target=new Rectangle((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height);
                var body=Shot.getRect();if(body.Intersects(target)){LastOverlap=body;return true;}
                if(Shot.penetrate!=-1)foreach(var p in Shot.oldPos)
                {if(p==Vector2.Zero)break;body.X=(int)p.X;body.Y=(int)p.Y;if(body.Intersects(target)){LastOverlap=body;return true;}}
                return false;
            }
            internal bool Hit(int slot,int remaining){if(slot==Selected)Witness=LastOverlap;return true;}
            internal Func<MotionRect,bool> Collision;internal Func<int,int,bool> OnHit;
            private void Killed(Vector2 position)
            {
                // Native Damage is synchronous inside Kill, before any
                // remaining movement. Its prior local hits stay immune.
                Receive?.MakeUnlimited();KillContact=HostBetsyFlameAttack.Receive(this,Path,Tick,Outer,Sub,Aim);
                // Kill always retires its native slot, including when that
                // inner Damage reflects. A normal flight reflection does not.
                Reflected=false;
            }
            internal Member(){Collision=Collides;OnHit=Hit;OnKill=Killed;}
        }
        internal static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.direction=p.direction;q.spriteDirection=p.spriteDirection;
            q.damage=p.damage;q.knockBack=p.knockBack;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.alpha=p.alpha;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;
            q.friendly=p.friendly;q.hostile=p.hostile;q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;
            q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;q.wetCount=p.wetCount;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);
            Array.Copy(p.oldPos,q.oldPos,p.oldPos.Length);Array.Copy(p.oldRot,q.oldRot,p.oldRot.Length);Array.Copy(p.oldSpriteDirection,q.oldSpriteDirection,p.oldSpriteDirection.Length);return q;
        }
        internal static void Kill(Projectile p){KillCore(p,null);}
        private static void KillCore(Projectile p,Action<Vector2> onKill)
        {
            if(!p.active)return;p.timeLeft=0;
            if(p.owner==Main.myPlayer)
            {
                // Full Kill711 tail, after its visual trail: resize first,
                // retain every local hit, then native Damage with pen=-1.
                p.position=p.Center;p.Size=new Vector2(140);p.Center=p.position;
                p.penetrate=-1;p.usesLocalNPCImmunity=true;p.localNPCHitCooldown=-1;
                if(onKill!=null)onKill(p.position);
            }
            p.active=false;
        }
        internal static bool BirthOrigin(PredictionTerrain terrain,Vector2 origin,Vector2 direction,float scale,out Vector2 birth)
        {
            birth=origin;Vector2 offset=Vector2.Normalize(direction)*40f*scale;bool clear;PredictionStop stop;
            if(!terrain.CanHit(new MotionRect(origin.X,origin.Y,0,0),new MotionRect(origin.X+offset.X,origin.Y+offset.Y,0,0),out clear,out stop))return false;
            if(clear)birth+=offset;return true;
        }
        internal static Vector2 BirthVelocity(Vector2 velocity,int direction,int member)
        {
            var scaled=velocity*.8f;var unit=scaled.SafeNormalize(-Vector2.UnitY);
            var v=(scaled+unit*member).RotatedBy(member*((float)Math.PI/180f*(-direction)));
            while(v.X>=16 || v.X<=-16 || v.Y>=16 || v.Y< -16)v*=.97f;return v;
        }
        internal static int Facing(Player player,Vector2 origin,Vector2 aim)
        {
            // Shoot chooses facing BEFORE converting the Y velocity for
            // reverse gravity. Use the borrowed raw Main.MouseWorld here.
            var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            aim=new Vector2((int)screen.X+Main.screenPosition.X,(int)screen.Y+Main.screenPosition.Y);
            var v=aim-origin;if(v!=Vector2.Zero)v.Normalize();
            return Vector2.Dot(Vector2.UnitX.RotatedBy(player.fullRotation),v)>0?1:-1;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {return !p.active || Motor(p,terrain,null);}
        // A tail Kill continues Update's while-loop if another subupdate
        // remains. That one inactive interval skips AI, still moves, then
        // returns before Damage/trail/timeLeft. Pre-Damage Kill has already
        // reached that return in its own interval and cannot use this path.
        internal static bool RetiredStep(Projectile p,PredictionTerrain terrain)
        {return Motor(p,terrain,null);}
        private static bool Motor(Projectile p,PredictionTerrain terrain,Action<Vector2> onKill)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y))
            {p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;
            // Native AI001 increments its common clock before the 711 alpha
            // branch. The second subupdate already fades the starting body.
            if(p.active)
            {
                if(p.localAI[0]==0)p.localAI[0]=1;
                p.ai[0]++;if(p.ai[0]>=2){p.alpha-=25;if(p.alpha<0)p.alpha=0;}
                if(p.ai[0]>=10)p.velocity.Y+=.1f;if(p.ai[0]>=20)p.velocity.Y+=.1f;if(p.ai[0]>20)p.ai[0]=20;p.velocity.X*=.99f;
                if(p.velocity.Y>32)p.velocity.Y=32;p.rotation=p.velocity.ToRotation()+(float)Math.PI/2f;if(p.velocity.Y>16)p.velocity.Y=16;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)KillCore(p,onKill);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)KillCore(p,onKill);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;KillCore(p,onKill);}p.position+=p.wet?wetVelocity:velocity;
            if(p.velocity.X<0)p.direction=-1;else p.direction=1;return true;
        }
        // This runs after native Damage, even if that Damage spent the last
        // penetration. A pre-Damage wall Kill exits
        // Update earlier and must not shift the trail or decrement its clock.
        internal static void Finish(Projectile p)
        {Tail(p,null);}
        private static void Tail(Projectile p,Action<Vector2> onKill)
        {
            for(int i=p.oldPos.Length-1;i>0;i--){p.oldPos[i]=p.oldPos[i-1];p.oldRot[i]=p.oldRot[i-1];p.oldSpriteDirection[i]=p.oldSpriteDirection[i-1];}
            p.oldPos[0]=p.position;p.oldRot[0]=p.rotation;p.oldSpriteDirection[0]=p.spriteDirection;
            p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)KillCore(p,onKill);
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==711)rows.Add(p);var scene=rows.ToArray();root.CaptureProjectileFamily(scene,711,711);return scene;}
        private static Projectile[] RelevantScene(Projectile[] scene,Projectile[] sources,HostAttackObstacles root,int delay)
        {
            // 711 has no children or projectile/projectile collision. Its
            // body, trail and Kill all write only its own local history and
            // owner immunity zero. With already-zero owner timers, ambient
            // members cannot influence another member's receive result.
            // Still capture the FULL family and occupied-slot lease first:
            // real mutations/new births must retire the prepared result.
            // A delayed hand must replay ambient retirement before allocating
            // its three slots, even when the retiring shot is very far away.
            if(delay!=0 || !root.OwnerImmunityAlreadyClear)return scene;
            foreach(var shot in scene)
                if(shot.owner==Main.myPlayer && (!shot.usesLocalNPCImmunity || shot.usesIDStaticNPCImmunity || shot.localNPCHitCooldown==-2))return scene;
            return sources??new Projectile[0];
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root,int selected)
        {
            var rows=new List<Member>();foreach(var p in scene){bool contributes=false;if(source!=null)foreach(var s in source)if(ReferenceEquals(p,s)){contributes=true;break;}var q=Copy(p);rows.Add(new Member{Shot=q,Source=contributes,Selected=selected,Receive=q.owner==Main.myPlayer?root.ForkForSpawn(q):null});}rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;
        }
        private static AttackContact Receive(Member m,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim)
        {
            var p=m.Shot;if(!p.friendly || p.hostile || p.damage<=0 || m.Receive==null)return null;
            int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,m.Collision,consumeSelected:true,onHit:m.OnHit);
            p.penetrate=m.Receive.Remaining;
            if(m.Source && m.Receive.SelectedHits>hits)
            {
                // Pass visited lower/higher slots too. Re-read only this
                // selected shape so another receiver cannot replace its
                // successful trail/body witness; no receive gates rerun.
                m.Collides(path[tick].ProjectileReceiveBounds);var b=m.LastOverlap;
                return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,b.X+b.Width*.5f,b.Y+b.Height*.5f,b.Width,b.Height,AttackConfidence.Conditional);
            }
            // Spending the last penetration stops this Damage pass. Native
            // still shifts the trail/timeLeft before calling its Kill burst.
            if(!passed && p.penetrate!=0){p.active=false;m.Reflected=true;}return null;
        }
        private static bool Outer(List<Member> members,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,bool beforeNpc,Vector2 aim,out AttackContact contact)
        {
            contact=null;if(beforeNpc)foreach(var m in members)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            foreach(var m in members)
            {
                var p=m.Shot;if(!p.active)continue;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    m.Path=path;m.Tick=tick;m.Outer=outer;m.Sub=sub;m.Aim=aim;m.KillContact=null;
                    bool available=Motor(p,terrain,m.OnKill);contact=m.KillContact;if(contact!=null)return true;if(!available)return false;
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                    contact=Receive(m,path,tick,outer,sub,aim);if(contact!=null)return true;if(m.Reflected)break;
                    Tail(p,m.OnKill);contact=m.KillContact;if(contact!=null)return true;
                    if(!p.active && sub+1<updates && !RetiredStep(p,terrain))return false;
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                }
            }
            return true;
        }
        private static readonly object PreparationKey=new object();
        private sealed class Preparation
        {
            internal Player Player;internal Item Item;internal AttackAmmoSnapshot Ammo;internal Vector2 Origin;internal int Age,Delay,Direction;internal byte Prefix;internal bool BeforeNpc;internal float Scale,KnockBack;
            internal NpcTrajectory Timeline;internal PredictionTerrain Terrain;internal HostAttackObstacles Root;internal Projectile[] Scene;internal HostProjectileBirthSlots Pool;
            internal AttackBallisticCandidates.Response Response;internal readonly List<Branch> Branches=new List<Branch>();
            internal bool Matches(Player p,AttackAmmoSnapshot a,NpcTrajectory path,Vector2 start,int age,bool before,PredictionTerrain terrain,int delay)
            {return ReferenceEquals(Player,p) && ReferenceEquals(Item,p.HeldItem) && Ammo.WeaponType==a.WeaponType && Prefix==p.HeldItem.prefix && Direction==p.direction && Scale==p.HeldItem.scale && KnockBack==p.HeldItem.knockBack && HostAttackAim.SameAmmo(Ammo,a) && Origin==start && Age==age && Delay==delay && BeforeNpc==before && ReferenceEquals(Terrain,terrain) && terrain.Unchanged && Root.Unchanged && HostAttackAim.SolvePreparation.Extends(Timeline,path);}
        }
        private readonly struct Launch
        {
            private readonly Vector2 birth,v0,v1,v2;
            internal Launch(Vector2 birth,Vector2 v0,Vector2 v1,Vector2 v2){this.birth=birth;this.v0=v0;this.v1=v1;this.v2=v2;}
            internal bool Same(Launch other){return birth==other.birth && v0==other.v0 && v1==other.v1 && v2==other.v2;}
        }
        private sealed class Branch
        {
            internal Launch Launch;internal Vector2 Aim;internal HostAttackObstacles Root;internal List<Member> Rows;internal HostProjectileBirthSlots Slots;internal int Completed;internal bool Terminal;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=711 || ammo.WeaponType!=3870 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=timeline.Count)return null;
            var scope=HostAttackAim.SolvePreparation.Current;var prepared=scope?.Read(PreparationKey) as Preparation;var body=Terraria.ID.ContentSamples.ProjectilesByType[711];
            if(prepared==null || !prepared.Matches(player,ammo,timeline,origin,age,beforeNpc,terrain,delay))
            {
                obstacles.CaptureShotFacing(player);
                prepared=new Preparation{Player=player,Item=player.HeldItem,Ammo=ammo,Origin=origin,Age=age,Delay=delay,BeforeNpc=beforeNpc,Prefix=player.HeldItem.prefix,Direction=player.direction,Scale=player.HeldItem.scale,KnockBack=player.HeldItem.knockBack,Timeline=timeline,Terrain=terrain,Root=obstacles,Scene=Capture(obstacles),Pool=obstacles.CaptureBirthSlots(),Response=new AttackBallisticCandidates.Response(body.extraUpdates+1,true)};
                prepared.Scene=RelevantScene(prepared.Scene,null,obstacles,delay);
                scope?.Store(PreparationKey,prepared);
            }
            else{obstacles.IncludePreparation(prepared.Root);prepared.Root.ExtendPreparation(timeline);prepared.Timeline=timeline;}
            var ballistic=AttackBallisticCandidates.GeneratePrepared(timeline,age,origin.X,origin.Y,ammo.Speed,body.extraUpdates+1,true,40*player.HeldItem.scale,Math.Max(body.width,body.height)*.5f,prepared.Response);
            // Deduplication retains launch values only. Keeping Branch here
            // would unnecessarily keep every retired scene/history alive until
            // the whole search ends, including directions not checkpointed.
            var directions=new Launch[ballistic.Length+33];int count=0;
            for(int seed=0;seed<directions.Length;seed++)
            {
                CombatPredictionWork.Check();
                var b=timeline[age+1].ProjectileReceiveBounds;
                // Discrete inverse candidates precede the sparse emergency
                // directions. A dry proposal never substitutes for the full
                // three-member receive/trail/Kill replay below.
                int fallback=seed-ballistic.Length;
                Vector2 direction=seed<ballistic.Length?new Vector2(ballistic[seed].X,ballistic[seed].Y):fallback==0?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((fallback-1)*MathHelper.TwoPi/32).ToRotationVector2();
                var aim=origin+direction*10000;
                var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
                var point=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y);
                var velocity=point-origin;float length=(float)Math.Sqrt(velocity.X*velocity.X+velocity.Y*velocity.Y);
                if(length==0)velocity=Vector2.UnitX*player.direction*ammo.Speed;else velocity*=ammo.Speed/length;
                Vector2 birth;if(!BirthOrigin(terrain,origin,velocity,player.HeldItem.scale,out birth))return null;int ownerDirection=Facing(player,origin,aim);
                Vector2 v0=BirthVelocity(velocity,ownerDirection,0),v1=BirthVelocity(velocity,ownerDirection,1),v2=BirthVelocity(velocity,ownerDirection,2);
                // Deduplicate the actual cursor-quantized three native births,
                // including muzzle obstruction and facing, not nearby seeds.
                var launch=new Launch(birth,v0,v1,v2);
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i].Same(launch)){duplicate=true;break;}if(duplicate)continue;
                Branch branch=null;foreach(var prior in prepared.Branches)if(prior.Launch.Same(launch)){branch=prior;break;}
                if(branch==null)
                {
                    // Only a live extension scope with a shorter prefix needs
                    // a distinct retained history. Unretained directions are
                    // serial scratch: reset the root without touching the
                    // independent histories of saved checkpoints.
                    bool retain=scope!=null && timeline.Stop==PredictionStop.None && timeline.Count-age-1<120 && prepared.Branches.Count<4;
                    var root=retain?prepared.Root.ForkCandidate():prepared.Root;if(!retain)root.BeginCandidate();
                    var rows=Prepare(prepared.Scene,null,root,timeline.Identity.Slot);var slots=prepared.Pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){CombatPredictionWork.Check();AttackContact ignored;if(!Outer(rows,slots,timeline,terrain,age-delay+wait,wait,true,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                    for(int i=0;i<3;i++)
                    {
                        int slot;if(!slots.TryAllocate(out slot))return null;var q=new Projectile();q.SetDefaults(711);q.active=true;q.whoAmI=slot;q.owner=player.whoAmI;q.Center=birth;q.velocity=i==0?v0:i==1?v1:v2;q.damage=ammo.Damage;q.knockBack=player.HeldItem.knockBack;
                        bool wet;byte kind;if(!terrain.ProjectileWet(birth.X,birth.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;rows.Add(new Member{Shot=q,Source=true,Selected=timeline.Identity.Slot,Receive=root.ForkForSpawn(q)});
                    }
                    rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                    branch=new Branch{Launch=launch,Aim=aim,Root=root,Rows=rows,Slots=slots};
                    // Only a few complete direction workspaces survive an
                    // extension; a dense live scene must not be multiplied by
                    // every fallback direction. Other directions still replay.
                    if(retain)prepared.Branches.Add(branch);
                }
                else branch.Root.ExtendPreparation(timeline);
                directions[count++]=launch;if(branch.Terminal)continue;
                for(int step=branch.Completed+1;step<=Math.Min(120,timeline.Count-age-1);step++)
                {
                    CombatPredictionWork.Check();AttackContact contact;
                    if(!Outer(branch.Rows,branch.Slots,timeline,terrain,age+step,delay+step,true,branch.Aim,out contact)){prepared.Branches.Remove(branch);break;}
                    if(contact!=null)return contact;branch.Completed=step;
                    bool alive=false;foreach(var m in branch.Rows)if(m.Source && m.Shot.active){alive=true;break;}
                    if(!alive){branch.Terminal=true;branch.Rows=null;branch.Slots=null;break;}
                }
            }
            return null;
        }
        internal static int PreferredRange(Player player,AttackAmmoSnapshot ammo,NPC target)
        {
            // Estimate the current low arc from the same dry discrete clock.
            // Unknown terrain/target motion still uses progressive expansion;
            // this is a scheduling hint, not a contact or reachability claim.
            var delta=target.Center-player.RotatedRelativePoint(player.MountedCenter);int updates=Terraria.ID.ContentSamples.ProjectilesByType[711].extraUpdates+1;
            return AttackBallisticCandidates.PreferredWindow(delta.X,delta.Y,ammo.Speed,updates,true,40*player.HeldItem.scale,(float)Math.Sqrt(target.width*target.width+target.height*target.height)*.5f+10);
        }
        // Actual-born finite oracle; no control rights for autonomous flames.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0)return null;foreach(var p in source)if(p==null || !p.active || p.type!=711 || p.owner!=player.whoAmI)return null;
            var scene=RelevantScene(Capture(obstacles),source,obstacles,0);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles,timeline.Identity.Slot);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,timeline,terrain,clock.FirstTick+step,step+1,clock.Phase!=HostAttackPhase.Projectiles || step>0,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
