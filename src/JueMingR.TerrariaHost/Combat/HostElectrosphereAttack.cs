using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // A 442 receive is a pre-Strike Kill trigger, never projectile damage.
    // Only its 443 child contributes; other local spheres still write the
    // same owner's eight-update immunity. Kill allocates before freeing the
    // parent slot, and can migrate an earlier/later existing sphere in place.
    internal static class HostElectrosphereAttack
    {
        internal static bool Handles(int type){return type==442;}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receivers;internal bool Source,Reflected,Triggered;
            internal readonly Func<int,int,bool> Trigger;
            internal Member(){Trigger=OnTrigger;}
            private bool OnTrigger(int slot,int remaining){Triggered=true;return false;}
        }
        internal static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.direction=p.direction;q.spriteDirection=p.spriteDirection;q.alpha=p.alpha;q.frame=p.frame;q.frameCounter=p.frameCounter;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;
            q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.active=p.active;q.friendly=p.friendly;q.hostile=p.hostile;q.ranged=p.ranged;q.magic=p.magic;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static bool WorldRetired(Projectile p)
        {return p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y);}
        private static void Kill(Projectile p,ref bool killed,ref Vector2 death)
        {if(!p.active)return;death=p.Center;p.active=false;p.timeLeft=0;killed=true;}
        private static bool Liquid(Projectile p,PredictionTerrain terrain)
        {
            if(p.ignoreWater)return true;bool wet,lava;byte kind;
            if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;return true;
        }
        // Exposes one native AI/movement interval for the independent oracle.
        // death is BEFORE the final UpdatePosition, which may run after Kill.
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed,out Vector2 death)
        {
            killed=false;death=Vector2.Zero;if(!p.active)return true;if(WorldRetired(p)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;
            if(p.type==442)
            {
                p.frame=0;if(p.alpha!=0){p.localAI[0]++;if(p.localAI[0]>=4){p.alpha-=90;if(p.alpha<0){p.alpha=0;p.localAI[0]=2;}}}
                if(Vector2.Distance(p.Center,new Vector2(p.ai[0],p.ai[1])*16+Vector2.One*8)<=16)Kill(p,ref killed,ref death);
                else
                {
                    if(p.alpha==0){p.localAI[1]++;if(p.localAI[1]>=120)Kill(p,ref killed,ref death);else {p.localAI[0]++;if(p.localAI[0]==3)p.localAI[0]=0;}}
                    if(p.active){if(p.velocity.Y>16)p.velocity.Y=16;p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;}
                }
            }
            else if(p.type==443)
            {
                // Invalid sound handles are visual, but the native AI stores
                // their sentinel in localAI[0]; preserve it for scalar replay.
                p.localAI[0]=ReLogic.Utilities.SlotId.Invalid.ToFloat();
                if(p.ai[1]==1){p.friendly=false;if(p.alpha<255)p.alpha+=51;if(p.alpha>=255){p.alpha=255;Kill(p,ref killed,ref death);}}
                else {if(p.alpha>0)p.alpha-=50;if(p.alpha<0)p.alpha=0;}
                if(p.active)
                {
                    p.ai[0]++;if(p.ai[0]>120)p.ai[0]=0;Vector2 unit=-Vector2.UnitY.RotatedBy((float)Math.PI*2*p.ai[0]/30f);
                    float scale=Math.Max(.75f+unit.Y*.25f,.8f-unit.Y*.2f);
                    // Native's two position operations are intentional: a
                    // reconstructed Center changes ULPs near integer bounds.
                    p.position+=new Vector2(p.width,p.height)/2;p.width=p.height=(int)(80*scale);p.position-=new Vector2(p.width,p.height)/2;
                    p.frameCounter++;if(p.frameCounter>=3){p.frameCounter=0;p.frame++;if(p.frame>=4)p.frame=0;}
                }
            }
            else return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p,ref killed,ref death);if(!Liquid(p,terrain))return false;
            if(p.shimmerWet && p.type==442)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {PredictionTile t,a;PredictionStop stop;if(!terrain.Tile(x,y,out t,out stop))return false;if(t.Liquid==255 && t.ShimmerLiquid){if(!terrain.Tile(x,y-1,out a,out stop))return false;full=a.Liquid>0 && a.ShimmerLiquid;}}
                if(full)Kill(p,ref killed,ref death);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;Kill(p,ref killed,ref death);}p.position+=p.wet?wetVelocity:velocity;return true;
        }
        internal static void Tail(Projectile p,out bool killed,out Vector2 death)
        {killed=false;death=Vector2.Zero;if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p,ref killed,ref death);}
        private static Projectile[] CaptureScene(HostAttackObstacles obstacles)
        {
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && (p.type==442 || p.type==443))scene.Add(p);
            var bodies=scene.ToArray();obstacles.CaptureProjectileFamily(bodies,442,443);return bodies;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles obstacles)
        {
            var members=new List<Member>(scene.Length+8);foreach(var p in scene)
            {bool contributes=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){contributes=true;break;}var copy=Copy(p);members.Add(new Member{Shot=copy,Source=contributes,Receivers=copy.owner==Main.myPlayer?obstacles.ForkForSpawn(copy):null});}return members;
        }
        private static List<Member> ReplayMembers(List<Member> prepared)
        {var members=new List<Member>(prepared.Count+8);foreach(var m in prepared){m.Receivers?.BeginCandidate();members.Add(new Member{Shot=Copy(m.Shot),Source=m.Source,Receivers=m.Receivers});}return members;}
        internal static void Migrate(Projectile parent,Vector2 death,Projectile[] scene)
        {
            var area=new Rectangle((int)death.X-40,(int)death.Y-40,80,80);
            foreach(var p in scene)if(p.whoAmI!=parent.whoAmI && p.active && p.owner==parent.owner && p.type==443 && p.getRect().Intersects(area)){p.ai[1]=1;p.velocity=(death-p.Center)/5;}
        }
        private static bool Birth(Member parent,Vector2 death,List<Member> members,HostProjectileBirthSlots slots,PredictionTerrain terrain)
        {
            if(parent.Shot.owner!=Main.myPlayer)return true;var scene=new Projectile[members.Count];for(int i=0;i<scene.Length;i++)scene[i]=members[i].Shot;Migrate(parent.Shot,death,scene);
            int slot;if(!slots.TryAllocate(out slot))return false;var p=new Projectile();p.SetDefaults(443);p.whoAmI=slot;p.owner=parent.Shot.owner;p.active=true;p.Center=death;p.damage=parent.Shot.damage;p.knockBack=0;p.velocity=Vector2.Zero;
            bool wet;byte kind;if(!terrain.ProjectileWet(death.X,death.Y,p.width,p.height,out wet,out kind,true))return false;p.wet=wet;
            members.Add(new Member{Shot=p,Source=parent.Source,Receivers=parent.Receivers.ForkForSpawn(p)});return true;
        }
        private static bool Outer(List<Member> members,HostProjectileBirthSlots slots,NpcTrajectory timeline,PredictionTerrain terrain,int tick,int outer,bool beforeNpc,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            // Only members already present at the NPC phase see its scan.
            // Reflection retires useful observation without Kill or slot free.
            if(beforeNpc)foreach(var m in members)if(m.Shot.active && m.Receivers!=null && !m.Receivers.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            int after=-1;for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in members)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;after=m.Shot.whoAmI;
                for(int sub=0;sub<=m.Shot.extraUpdates;sub++)
                {
                    var p=m.Shot;bool killed;Vector2 death;if(!Step(p,terrain,out killed,out death))return false;
                    if(killed && p.type==442 && !Birth(m,death,members,slots,terrain))return false;
                    if(p.active && p.friendly && !p.hostile && p.damage>0 && m.Receivers!=null)
                    {
                        m.Triggered=false;int hits=m.Receivers.SelectedHits;
                        bool passed=m.Receivers.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:p.type==442?m.Trigger:null);
                        if(p.type==443 && m.Source && m.Receivers.SelectedHits>hits)
                        {contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);if(contact!=null)return true;}
                        if(!passed)
                        {
                            if(m.Triggered){Kill(p,ref killed,ref death);if(!Birth(m,death,members,slots,terrain))return false;}
                            else {p.active=false;m.Reflected=true;}
                        }
                    }
                    if(p.active){Tail(p,out killed,out death);if(killed && p.type==442 && !Birth(m,death,members,slots,terrain))return false;}
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                }
            }
            return false;
        }
        internal static bool Cursor(Player player,Vector2 aim,out Vector2 consumed,out Vector2 tile)
        {
            consumed=tile=Vector2.Zero;var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            if(float.IsNaN(screen.X) || float.IsInfinity(screen.X) || float.IsNaN(screen.Y) || float.IsInfinity(screen.Y) || screen.X<int.MinValue || screen.X>int.MaxValue || screen.Y<int.MinValue || screen.Y>int.MaxValue)return false;
            int x=(int)screen.X,y=(int)screen.Y;consumed=new Vector2((float)x+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-y:(float)y+Main.screenPosition.Y);
            tile=new Vector2(Math.Max(5,Math.Min(Main.maxTilesX-5,(int)(consumed.X/16))),Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(consumed.Y/16))));return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=442 || ammo.WeaponType!=2796 || ammo.Damage<=0 || age+1>=timeline.Count || delay<0 || age-delay<0)return null;
            // Root's 442-only cursor scope leases mouse AND tileTarget from
            // the same integer screen mapping. No live input is written here.
            obstacles.CaptureCursorMapping(player);var scene=CaptureScene(obstacles);var prepared=Prepare(scene,null,obstacles);var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[41];var tiles=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;Vector2 aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;Vector2 cursor,tile;if(!Cursor(player,aim,out cursor,out tile))continue;
                Vector2 direction=(cursor-origin).SafeNormalize(Vector2.UnitX*player.direction);bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction && tiles[i]==tile){duplicate=true;break;}if(duplicate)continue;directions[count]=direction;tiles[count++]=tile;
                obstacles.BeginCandidate();var members=ReplayMembers(prepared);var slots=pool.Copy();bool valid=true;
                for(int step=1;step<=delay;step++){AttackContact ignored;if(!Outer(members,slots,timeline,terrain,age-delay+step,step,true,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(442);p.whoAmI=slot;p.owner=player.whoAmI;p.active=true;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;p.Center=origin;p.velocity=direction*ammo.Speed;p.ai[0]=tile.X;p.ai[1]=tile.Y;
                while(p.velocity.X>=16 || p.velocity.X<=-16 || p.velocity.Y>=16 || p.velocity.Y< -16)p.velocity*=.97f;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;members.Add(new Member{Shot=p,Source=true,Receivers=obstacles});
                for(int step=1;step<=Math.Min(120,timeline.Count-age-1);step++){AttackContact contact;if(!Outer(members,slots,timeline,terrain,age+step,delay+step,true,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        // Oracle for explicitly observed parents/children, not input authority
        // or a production observed-flight registration of all local spheres.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0)return null;foreach(var p in source)if(p==null || !p.active || (p.type!=442 && p.type!=443) || p.owner!=player.whoAmI)return null;
            var scene=CaptureScene(obstacles);obstacles.BeginCandidate();var members=Prepare(scene,source,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(members,slots,timeline,terrain,clock.FirstTick+step,step+1,clock.Phase!=HostAttackPhase.Projectiles || step>0,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
