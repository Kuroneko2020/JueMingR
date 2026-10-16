using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These ordinary ammo bodies keep their AI001/environment motor. Kill
    // adds a finite receiving stage or two crystal children, before freeing
    // the parent slot. Venom's inactive Damage passes apply status only;
    // they cannot create a second Strike/Contact.
    internal static class HostAmmoImpactAttack
    {
        internal static bool Handles(int type){return type==41 || type==89 || type==283 || type==286;}
        internal static bool Family(int type){return Handles(type) || type==90;}
        private static readonly int[] Types={41,89,90,283,286};
        internal sealed class Snapshot{internal Projectile[] Scene;internal HostProjectileBirthSlots Slots;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool representative=false)
        {return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=representative};}
        internal static bool Ai(Projectile p)
        {
            if(p.type==90)
            {
                p.light=p.scale*.5f;p.rotation+=p.velocity.X*.2f;p.ai[1]++;p.velocity*=.96f;
                if(p.ai[1]>15){p.scale-=.05f;if((double)p.scale<=.2){p.scale=.2f;return false;}}return true;
            }
            if(!Handles(p.type))return false;
            if(p.type==41)p.ai[0]++;else p.alpha=Math.Max(0,p.alpha-15);
            // Bullet births start at zero and do not advance this clock,
            // but a sealed observed nonzero AI001 state still takes the
            // original common gravity branch. Do not infer it is an arrow.
            if(p.ai[0]>=15){p.ai[0]=15;p.velocity.Y+=.1f;}
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;if(p.velocity.Y>16)p.velocity.Y=16;return true;
        }
        internal static bool Shimmer(Projectile p,PredictionTerrain terrain,out bool killed)
        {
            killed=false;if(p.aiStyle!=1)return true;
            int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);
            if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
            {
                PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;if(above.Liquid>0 && above.ShimmerLiquid){killed=true;return true;}}
            }
            if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.timeLeft=Math.Min(600,p.timeLeft)-60;p.shimmerWet=p.wet=false;}return true;
        }
        // Shared arrow Update preserves wet-before-AI displacement, wind,
        // native tile clipping and the optional Harpy tail between AI/motion.
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,bool> afterAi=null)
        {return HostArrowFamilyAttack.Step(p,terrain,out killed,out skipped,afterAi);}
        internal static bool PrepareKillBurst(Projectile p)
        {
            if(p.owner!=Main.myPlayer || p.type!=41 && p.type!=286)return false;
            // Native Kill adds integer width/2 BEFORE resizing. Scaled 4px
            // bullets have a five-pixel body, so replacing this with Center
            // shifts the actual burst by half a pixel on both axes.
            var anchor=p.position+new Vector2(p.width/2,p.height/2);p.width=p.height=p.type==41?64:80;p.position=anchor-new Vector2(p.width/2,p.height/2);p.penetrate=p.type==41?-1:1;
            if(p.type==286){p.localAI[1]=-1;p.idStaticNPCHitCooldown=5;}return true;
        }
        internal static Projectile[] KillChildren(Projectile parent)
        {
            if(parent.type!=89 || parent.owner!=Main.myPlayer)return new Projectile[0];
            var result=new Projectile[2];for(int i=0;i<2;i++)
            {
                // Independent middle draws of Next(40,70), Next(-20,21)
                // are legal representatives. Native NewProjectile receives
                // TOP-LEFT + velocity as its CENTER, not parent.Center.
                var child=new Projectile();child.SetDefaults(90);child.owner=parent.owner;child.active=true;child.velocity=new Vector2(-parent.velocity.X*55*.01f+0f,-parent.velocity.Y*55*.01f+0f);child.Center=parent.position+child.velocity;child.damage=(int)(parent.damage*.5);child.knockBack=0;result[i]=child;
            }
            return result;
        }
        internal static Rectangle[] VenomStatusAreas(Projectile p)
        {
            if(p.type!=283 || p.owner!=Main.myPlayer)return new Rectangle[0];
            var position=p.position+new Vector2(p.width/2,p.height/2)-new Vector2(40);var delta=p.oldVelocity.ToRotation().ToRotationVector2()*80*.75f;
            var result=new Rectangle[3];for(int i=0;i<3;i++){result[i]=new Rectangle((int)position.X,(int)position.Y,80,80);position+=delta;}return result;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type)){list.Add(p);root.CaptureAmmoImpactStatic(p);}
            var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,Types);return scene;
        }
        internal static Snapshot CaptureForSolve(Player player,HostAttackObstacles root,int birthType=0)
        {if(birthType==286){var sample=new Projectile();sample.SetDefaults(286);root.CaptureAmmoImpactStatic(sample);}return new Snapshot{Scene=Capture(player,root),Slots=root.CaptureBirthSlots()};}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostNebulaBlazeAttack.Copy(p),root,sources!=null && Array.IndexOf(sources,p)>=0));return rows;}
        private static bool Wet(Projectile p,PredictionTerrain terrain)
        {bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return true;}
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,int sub,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;var p=m.Shot;if(!p.active)return true;int oldCooldown=p.idStaticNPCHitCooldown;
            // Fork a private burst policy while retaining the SAME owner and
            // static history. A final direct hit's sixty-tick assignment still
            // blocks its own five-tick burst until ResetStatic runs AFTER it.
            var burst=HostNebulaBlazeAttack.Copy(p);
            if(PrepareKillBurst(burst) && burst.damage>0)
            {
                var receive=m.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;bool pass=receive.PassCustomOuter(2,tick,outer,burst.Center.X,burst.Center.Y,burst.width,burst.height,null,unlimited:burst.penetrate<0,consumeSelected:true);
                if(m.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,burst.Center.X,burst.Center.Y,burst.width,burst.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                if(!pass && receive.Remaining!=0)return false;
                if(p.type==286)root.ResetAmmoImpactStaticMatching(outer,oldCooldown);
            }
            foreach(var child in KillChildren(p))
            {
                int slot;if(!slots.TryAllocate(out slot))return false;child.whoAmI=slot;if(!Wet(child,terrain))return false;rows.Add(Make(child,root,m.Source,true));
            }
            p.timeLeft=0;p.active=false;slots.Release(p.whoAmI);return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    CombatPredictionWork.Check();p.oldVelocity=p.velocity;bool killed,skipped;if(!Step(p,terrain,out killed,out skipped))return false;
                    if(!p.active){slots.Release(p.whoAmI);break;}if(skipped)continue;
                    if(!killed && p.damage>0 && p.friendly && !p.hostile && p.owner==Main.myPlayer)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,remaining)=>{if(p.type==41 && p.timeLeft>1)p.timeLeft=1;return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!pass && p.penetrate!=0)return false;
                    }
                    if(!killed)killed=--p.timeLeft<=0 || p.penetrate==0;
                    if(killed){if(!Kill(m,rows,slots,path,tick,outer,sub,terrain,root,aim,out contact))return false;if(contact!=null)return true;}
                }
            }
            return false;
        }
        internal static AttackContact Explicit(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 aim,Vector2 origin,Vector2 velocity,int age,PredictionTerrain terrain,HostAttackObstacles root,int delay,Snapshot captured=null)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;
            captured=captured??CaptureForSolve(player,root,ammo.Projectile);var slots=captured.Slots.Copy();root.ResetAmmoImpactCandidate();root.BeginCandidate();var rows=Copy(captured.Scene,null,root);
            for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,age-delay+wait,wait,terrain,root,aim,out ignored))return null;}
            int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=origin;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
            if(!Wet(p,terrain))return null;rows.Add(Make(p,root,true));
            for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,age+step,delay+step,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            AttackMotion motion;if(!HostAttackModels.TryRead(ammo,out motion))return null;var proposals=HostMotionAttackInputs.Generate(origin,motion,path,age);var directions=new List<Vector2>();var captured=CaptureForSolve(player,root,ammo.Projectile);root.CaptureShotFacing(player);
            for(int input=0;input<proposals.Length+41;input++)
            {
                int seed=input-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Max(0,Math.Min(seed,8))*4)].ProjectileReceiveBounds;
                var unit=seed<0?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);
                var aim=origin+unit*10000;var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);motion.Launch(ref velocity.X,ref velocity.Y);var contact=Explicit(player,ammo,path,aim,origin,velocity,age,terrain,root,delay,captured);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Age<0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            var scene=CaptureForSolve(player,root);root.ResetAmmoImpactCandidate();root.BeginCandidate();var rows=Copy(scene.Scene,sources,root);var slots=scene.Slots.Copy();var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
