using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI004 grows by native child births; UpdatePosition explicitly skips it.
    // Alpha controls retirement, not Damage permission. All local segments
    // share owner immunity, and children born after the current native slot
    // receive their first update in this same outer action. No live AI, spawn,
    // input, or RNG is invoked by this finite private replay.
    internal static class HostThornAttack
    {
        internal static bool Handles(int type){return type==7 || type==150 || type==494;}
        internal static bool MemberType(int type){return type==7 || type==8 || type>=150 && type<=152 || type==493 || type==494;}
        private const int MaximumMembers=128;
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Reflected;}
        internal static Projectile Copy(Projectile p)
        {
            // AI004 has no trail collider. Copy only its motor/receive values;
            // do not allocate and copy the unrelated oldPos drawing history
            // once per direction, random representative and spawned member.
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.direction=p.direction;q.spriteDirection=p.spriteDirection;q.aiStyle=p.aiStyle;
            q.damage=p.damage;q.knockBack=p.knockBack;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.alpha=p.alpha;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;
            q.friendly=p.friendly;q.hostile=p.hostile;q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.manualDirectionChange=p.manualDirectionChange;q.ownerHitCheck=p.ownerHitCheck;
            q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;q.wetCount=p.wetCount;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;q.usesOwnerMeleeHitCD=p.usesOwnerMeleeHitCD;
            q.armorPenetration=p.armorPenetration;q.bonusTagDamage=p.bonusTagDamage;q.bonusCritChance=p.bonusCritChance;q.tagEffectType=p.tagEffectType;Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static void Kill(Projectile p){if(p.active){p.active=false;p.timeLeft=0;}}
        // Two independent representative bits: Nettle's initial side, and
        // Crystal's seven/eight-depth tip. This is a finite random representative,
        // never a promise of the actual random branch or its first Strike tick.
        // Below depth seven Crystal's random call has no state effect.
        internal static bool Ai(Projectile p,int representative,out Projectile child)
        {
            child=null;if(!p.active)return true;if(!MemberType(p.type) || p.aiStyle!=4)return false;
            if(Main.netMode!=2 && p.ai[1]==0 && p.localAI[0]==0)p.localAI[0]=1;
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;
            if(p.ai[0]==0)
            {
                if(p.type>=150 && p.type<=152 && p.ai[1]==0 && p.alpha==255 && (representative&1)!=0)
                {p.type++;if(!MemberType(p.type))return false;}
                p.alpha-=50;if(p.type>=150 && p.type<=152)p.alpha-=25;else if(p.type==493 || p.type==494)p.alpha-=50;
                if(p.alpha>0)return true;p.alpha=0;p.ai[0]=1;
                if(p.ai[1]==0){p.ai[1]++;p.position+=p.velocity*1f;}
                if(p.owner!=Main.myPlayer || p.type==8 || p.type==152 || p.type==493)return true;
                int type=p.type,damage=p.damage;float knock=p.knockBack;
                if(type==7){if(p.ai[1]>=6)type=8;}
                else if(type==494)
                {if(p.ai[1]>=7+((representative>>1)&1)){type=493;damage=(int)((double)damage*1.25);knock*=1.25f;}}
                else if(type==150 || type==151){type=type==150?151:150;if(p.ai[1]>=10 && p.type==151)type=152;}
                else return false;
                // NewProjectile receives float center parameters before its
                // own top-left store. Preserve that call boundary and the
                // child's defaults-plus-parent armor/tag/crit inheritance.
                var center=new Vector2(p.position.X+p.velocity.X+(float)(p.width/2),p.position.Y+p.velocity.Y+(float)(p.height/2));
                child=new Projectile();child.SetDefaults(type);child.active=true;child.owner=p.owner;child.Center=center;child.velocity=p.velocity;child.damage=damage;child.knockBack=knock;child.ai[1]=p.ai[1]+1;
                child.armorPenetration+=p.armorPenetration;child.bonusTagDamage+=p.bonusTagDamage;child.bonusCritChance+=p.bonusCritChance;child.tagEffectType=p.tagEffectType;return true;
            }
            p.alpha+=p.type>=150 && p.type<=152?3:p.type==493 || p.type==494?4:5;if(p.alpha>=255)Kill(p);return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,int representative,out Projectile child)
        {
            child=null;if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y))
            {p.active=false;return true;} // Native world exit is not Kill.
            if(p.tileCollide || !p.ignoreWater || p.shimmerWet || p.manualDirectionChange || p.ownerHitCheck || p.usesOwnerMeleeHitCD || p.extraUpdates<0 || p.extraUpdates>16)return false;
            if(!Ai(p,representative,out child))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner>=0 && p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(p.velocity.X<0)p.direction=-1;else p.direction=1;return true;
        }
        internal static void Finish(Projectile p)
        {if(!p.active)return;p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)Kill(p);}
        private static bool BirthLiquid(Projectile p,PredictionTerrain terrain)
        {
            bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;
            p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return !p.shimmerWet;
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {
            var all=new List<Projectile>();var vile=new List<Projectile>();var nettle=new List<Projectile>();var crystal=new List<Projectile>();
            foreach(var p in Main.projectile)if(p.active && MemberType(p.type)){all.Add(p);if(p.type<=8)vile.Add(p);else if(p.type<=152)nettle.Add(p);else crystal.Add(p);}
            // Three exact qualification intervals avoid leasing the hundreds
            // of unrelated types between these families. Empty sets still
            // detect a newly appearing same-owner immunity competitor.
            root.CaptureProjectileFamily(vile.ToArray(),7,8);root.CaptureProjectileFamily(nettle.ToArray(),150,152);root.CaptureProjectileFamily(crystal.ToArray(),493,494);return all.ToArray();
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {
            var rows=new List<Member>(scene.Length+12);foreach(var p in scene){bool source=false;if(sources!=null)foreach(var s in sources)if(ReferenceEquals(p,s)){source=true;break;}var q=Copy(p);rows.Add(new Member{Shot=q,Source=source,Receive=q.owner==Main.myPlayer?root.ForkForSpawn(q):null});}return rows;
        }
        private static bool Birth(Member parent,Projectile child,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain)
        {
            if(child==null)return true;int slot;if(rows.Count>=MaximumMembers || !slots.TryAllocate(out slot))return false;
            // This family's births AND retirements update the private pool.
            // Other captured occupants retain the existing Conditional pool
            // premise: their future retirement/new birth is not predicted.
            // The shared full-key pool lease revokes an actually changed
            // occupancy before consumption; it never grants a new live slot.
            if(!BirthLiquid(child,terrain))return false;child.whoAmI=slot;rows.Add(new Member{Shot=child,Source=parent.Source,Receive=parent.Receive?.ForkForSpawn(child)});return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,bool beforeNpc,Vector2 aim,int representative,out AttackContact contact)
        {
            contact=null;if(beforeNpc)foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            int after=-1;for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;after=m.Shot.whoAmI;
                for(int sub=0;sub<=m.Shot.extraUpdates;sub++)
                {
                    var p=m.Shot;Projectile child;if(!Step(p,terrain,representative,out child) || !Birth(m,child,rows,slots,terrain))return false;
                    if(p.active && p.friendly && !p.hostile && p.damage>0 && m.Receive!=null)
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits)
                        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Representative);if(contact!=null)return true;}
                        if(!passed){if(p.penetrate==0)Kill(p);else{p.active=false;m.Reflected=true;}}
                    }
                    Finish(p);if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                }
            }
            return false;
        }
        internal static Vector2 BirthVelocity(Player player,Vector2 origin,Vector2 aim,float speed)
        {bool directionDependent;return BirthVelocity(player,origin,aim,speed,out directionDependent);}
        internal static Vector2 BirthVelocity(Player player,Vector2 origin,Vector2 aim,float speed,out bool directionDependent)
        {
            var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            var cursor=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y);
            var v=cursor-origin;float factor=(float)Math.Sqrt(v.X*v.X+v.Y*v.Y);
            directionDependent=v==Vector2.Zero;
            if(v==Vector2.Zero){v=Vector2.UnitX*HostBetsyFlameAttack.Facing(player,origin,aim);factor=speed;}else factor=speed/factor;v.X*=factor;v.Y*=factor;return v;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=64 && ammo.WeaponType!=788 && ammo.WeaponType!=3051 || ammo.Damage<=0 || age+1>=timeline.Count || delay<0 || age-delay<0)return null;
            obstacles.CaptureShotFacing(player);var scene=Capture(obstacles);if(scene.Length>=MaximumMembers)return null;var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[17];int count=0;
            for(int seed=0;seed<17;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;var aim=origin+direction*10000;
                for(int variant=0;variant<(ammo.Projectile==7?1:2);variant++)
                {
                    int representative=ammo.Projectile==494?variant*2:variant;obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,timeline,terrain,age-delay+wait,wait,true,aim,representative,out ignored)){valid=false;break;}}if(!valid)continue;
                    int slot;if(!slots.TryAllocate(out slot))return null;var q=new Projectile();q.SetDefaults(ammo.Projectile);q.active=true;q.whoAmI=slot;q.owner=player.whoAmI;q.Center=origin;q.velocity=BirthVelocity(player,origin,aim,ammo.Speed);q.damage=ammo.Damage;q.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);q.armorPenetration+=player.HeldItem.armorPenetration;q.bonusTagDamage+=player.HeldItem.bonusTagDamage;
                    if(!BirthLiquid(q,terrain))continue;rows.Add(new Member{Shot=q,Source=true,Receive=obstacles.ForkForSpawn(q)});
                    for(int step=1;step<=Math.Min(120,timeline.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,timeline,terrain,age+step,delay+step,true,aim,representative,out contact))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
        // Actual-born family oracle replays both finite suffix representatives.
        // It has no input permission. A contact before their branch is exact;
        // a random-only tip contact keeps Representative confidence.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in source)if(p==null || !p.active || !MemberType(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(obstacles);if(scene.Length>=MaximumMembers)return null;var pool=obstacles.CaptureBirthSlots();var aim=source[0].Center+source[0].velocity*1000;
            for(int representative=0;representative<4;representative++)
            {
                obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var slots=pool.Copy();for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++)
                {AttackContact contact;if(!Outer(rows,slots,timeline,terrain,clock.FirstTick+step,step+1,true,aim,representative,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}
