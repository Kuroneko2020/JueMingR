using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked .8 AI030: the star emits four ornaments during AI, before its
    // current movement. Slot parity changes their side; Kill emits no child.
    // Legal private random representatives never consume the live generator.
    internal static class HostOrnamentAttack
    {
        private const int MaximumMembers=128;
        internal static bool Handles(int type){return type==907;}
        internal static bool Family(int type){return type==907 || type==335;}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        internal static Projectile Copy(Projectile p){return HostNoteAttack.Copy(p);}
        private static void Kill(Projectile p){if(p.active){p.active=false;p.timeLeft=0;}}
        internal static bool Ai(Projectile p,int angleChoice,int speedChoice,out Projectile child)
        {
            child=null;if(!Family(p.type) || p.aiStyle!=30 || angleChoice< -5 || angleChoice>=5 || speedChoice<0 || speedChoice>=2)return false;
            if(p.type==907)
            {
                if(p.ai[0]>50){p.velocity*=.9f;p.rotation*=.9f;}
                else{p.rotation+=.2f;if(p.rotation>MathHelper.TwoPi)p.rotation-=MathHelper.TwoPi;}
                p.ai[0]++;
                if(p.owner==Main.myPlayer && p.ai[0]<50 && p.ai[0]%10==0)
                {
                    float angle=(float)Math.PI/2*(p.ai[0]%20!=0?1:-1);angle*=p.whoAmI%2!=0?1:-1;
                    angle+=angleChoice*MathHelper.Lerp(.2f,.03f,p.ai[0]/50);
                    var velocity=p.velocity.RotatedBy(angle).SafeNormalize(Vector2.Zero);
                    velocity*=Math.Max(2.5f,(50-p.ai[0])/50*(7+(-2+speedChoice*2)));
                    child=new Projectile();child.SetDefaults(335);child.active=true;child.owner=p.owner;child.Center=p.Center;child.velocity=velocity;child.damage=p.damage;child.knockBack=p.knockBack*.25f;
                    // Native335 replaces ai1 with an independent color draw.
                    // Color does not feed movement, shape or receiving.
                    child.ai[1]=0;child.armorPenetration+=p.armorPenetration;child.bonusCritChance+=p.bonusCritChance;child.bonusTagDamage+=p.bonusTagDamage;child.tagEffectType=p.tagEffectType;
                }
                if(p.ai[0]>100)Kill(p);
            }
            else
            {
                p.frame=(int)p.ai[1];
                if(p.ai[0]<0){p.velocity.Y+=.25f;if(p.velocity.Y>14)p.velocity.Y=14;}
                else p.velocity*=.95f;
                if(p.ai[0]>=0 && p.velocity.Length()<.25f)
                {
                    if(p.velocity!=Vector2.Zero){p.velocity=Vector2.Zero;if(Main.netMode!=1)p.ai[0]=50;}
                    p.ai[0]--;
                }
                p.localAI[0]++;p.rotation=(float)Math.Sin(p.localAI[0]/10);
            }
            return true;
        }
        private static bool BirthLiquid(Projectile p,PredictionTerrain terrain)
        {
            bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;
            p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,int angleChoice,int speedChoice,out Projectile child)
        {
            child=null;if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            if(p.owner<0 || p.owner>=Main.player.Length || p.correctSlopeCollision || p.ownerHitCheck || p.usesOwnerMeleeHitCD || p.manualDirectionChange || p.extraUpdates<0 || p.extraUpdates>16)return false;
            p.oldVelocity=p.velocity;if(!Ai(p,angleChoice,speedChoice,out child))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            // AI030 has no Shimmer branch. Preserve ordinary wet movement,
            // including HandleMovement's suffix after an AI/owner Kill.
            p.oldPosition=p.position;p.oldDirection=p.direction;var position=p.position;var velocity=p.velocity;var wetVelocity=p.oldVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit){p.position+=p.velocity;Kill(p);}
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        internal static Projectile Birth(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 aim,int slot,PredictionTerrain terrain)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=1928 || ammo.Damage<=0)return null;
            var p=new Projectile();p.SetDefaults(907);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);p.armorPenetration+=player.HeldItem.armorPenetration;p.bonusTagDamage+=player.HeldItem.bonusTagDamage;
            return BirthLiquid(p,terrain)?p:null;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))rows.Add(p);
            var result=rows.ToArray();root.CaptureOwnedProjectileFamily(result,player.whoAmI,new[]{907,335});return result;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool selected=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){selected=true;break;}var copy=Copy(p);rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=selected});}return rows;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,int representative,out AttackContact contact)
        {
            contact=null;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var candidate in rows)if(candidate.Shot.active && candidate.Shot.whoAmI>after && (m==null || candidate.Shot.whoAmI<m.Shot.whoAmI))m=candidate;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y))return false;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    Projectile child;if(!Step(p,terrain,representative==0?0:representative==1?-5:4,representative==0?0:1,out child))return false;
                    if(child!=null)
                    {
                        int slot;if(rows.Count>=MaximumMembers || !slots.TryAllocate(out slot) || !BirthLiquid(child,terrain))return false;
                        child.whoAmI=slot;rows.Add(new Member{Shot=child,Receive=m.Receive.ForkForSpawn(child),Source=m.Source,Random=true});
                    }
                    if(p.active && p.friendly && !p.hostile && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits)
                        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0)return false;
                    }
                    Finish(p);
                    if(!p.active)slots.Release(p.whoAmI);
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=1928 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            root.CaptureShotFacing(player);var scene=Capture(player,root);if(scene.Length>=MaximumMembers)return null;var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(direction))continue;directions.Add(direction);var aim=origin+direction*10000;
                for(int representative=0;representative<3;representative++)
                {
                    root.BeginCandidate();var rows=Prepare(scene,null,root);var slots=pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,aim,representative,out ignored)){valid=false;break;}}if(!valid)continue;
                    int slot;if(!slots.TryAllocate(out slot))return null;var p=Birth(player,ammo,origin,aim,slot,terrain);
                    if(p==null)continue;rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true});
                    for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,slots,path,terrain,age+step,delay+step,aim,representative,out result))break;if(result!=null)return result;}
                }
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in source)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,root);if(scene.Length>=MaximumMembers)return null;var pool=root.CaptureBirthSlots();var aim=source[0].Center+source[0].velocity*1000;
            for(int representative=0;representative<3;representative++)
            {
                root.BeginCandidate();var rows=Prepare(scene,source,root);var slots=pool.Copy();
                for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact result;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,representative,out result))break;if(result!=null)return result;}
            }
            return null;
        }
    }
}
