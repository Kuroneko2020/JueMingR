using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Finite free-flight roles with no controller or damaging Kill children.
    // The explicit motors retain their native clocks/body changes; random
    // future births use legal representatives, without consuming live RNG.
    internal static class HostFreeFlightAttack
    {
        internal static bool Handles(int type){return type==206 || type==410 || type==523 || type==876 || type==496 || type==1082 || type==1083 || type==409;}
        internal static bool Family(int type){return Handles(type);}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;
            internal bool Source,Representative,Selected;
            internal int SelectedSlot;
            internal bool OnHit(int slot,int remaining)
            {
                if(slot==SelectedSlot)Selected=true;
                // A Zapinator's first hit can randomly move/change the shot
                // before later targets. Never continue its old straight motor
                // through that unknown event; an already qualified selected
                // receive remains valid and is returned by the caller.
                return Shot.type!=876;
            }
        }
        internal static bool Step(Projectile shot,PredictionTerrain terrain,ref bool representative)
        {
            if(!shot.active)return true;
            if(shot.position.X<=Main.leftWorld || shot.position.X+shot.width>=Main.rightWorld || shot.position.Y<=Main.topWorld || shot.position.Y+shot.height>=Main.bottomWorld)
            {shot.active=false;return true;}
            var wetVelocity=shot.velocity;
            if(shot.type==206)
            {
                shot.localAI[0]++;
                if(shot.localAI[0]>50){shot.alpha+=2;if(shot.alpha>=255){shot.active=false;return true;}}
                else if(shot.localAI[0]>3)shot.alpha=Math.Max(0,shot.alpha-50);
                shot.velocity+=new Vector2(shot.ai[0],shot.ai[1]);
                if(shot.localAI[1]==0)shot.localAI[2]=shot.velocity.X>0?-1:1;
                shot.localAI[1]++;
                if(shot.localAI[1]==1){shot.ai[0]=shot.ai[1]=0;representative=true;}
                if(shot.localAI[1]>10){shot.velocity=shot.velocity.RotatedBy(shot.localAI[2]*Math.Abs(shot.ai[0])*shot.localAI[1]*.35f);shot.velocity*=.97f;}
                float manhattan=Math.Abs(shot.velocity.X)+Math.Abs(shot.velocity.Y);
                if(manhattan>16)shot.velocity*=.95f;
                if(Math.Abs(shot.velocity.X)+Math.Abs(shot.velocity.Y)<12)shot.velocity*=1.05f;
            }
            else if(shot.type==410)
            {
                shot.localAI[0]++;if(shot.localAI[0]>3)shot.alpha=Math.Max(50,shot.alpha-25);
                shot.velocity*=.96f;
                if(shot.ai[1]==0){shot.ai[1]=1;representative=true;}
                var center=shot.Center;shot.scale=shot.ai[1];shot.width=shot.height=(int)(14*shot.ai[1]);
                // Native subtracts integer width/2 from the old centre. Odd
                // representative/observed sizes retain that half-pixel shift.
                shot.position=center-new Vector2(shot.width/2,shot.height/2);
            }
            else if(shot.type==409){} // Its captured autonomous AI runs immediately before this shared environment stage.
            else if(shot.type==1082 || shot.type==1083)
            {
                if(shot.ai[1]==0)
                {if(shot.ai[0]>15){shot.velocity.X*=.95f;shot.velocity.Y+=.4f;}else shot.ai[0]++;}
                else{shot.tileCollide=false;shot.damage=0;shot.velocity*=.6f;shot.alpha+=7;if(shot.alpha>=255){shot.active=false;return true;}}
            }
            else if(shot.type==496)
            {
                var center=shot.Center;shot.scale=1-shot.localAI[0];shot.width=shot.height=(int)(20*shot.scale);
                shot.position=center-new Vector2(shot.width/2,shot.height/2);
                shot.localAI[0]+=shot.localAI[0]<.1f?.01f:.025f;
                if(shot.localAI[0]>=.95f){shot.active=false;return true;}
                shot.velocity+=new Vector2(shot.ai[0],shot.ai[1])*1.5f;
                if(shot.velocity.Length()>16)shot.velocity=shot.velocity.SafeNormalize(Vector2.UnitX)*16;
                shot.ai[0]*=1.05f;shot.ai[1]*=1.05f;
            }
            else if(shot.type==523)
            {
                if(shot.localAI[0]>2)shot.alpha=Math.Max(100,shot.alpha-20);else shot.localAI[0]++;
                if(shot.ai[0]>30){if(shot.velocity.Y> -8)shot.velocity.Y-=.05f;shot.velocity.X*=.98f;}else shot.ai[0]++;
                if(shot.wet){if(shot.velocity.Y>0)shot.velocity.Y*=.98f;if(shot.velocity.Y> -8)shot.velocity.Y-=.2f;shot.velocity.X*=.94f;}
            }
            // 876 has no pre-hit gravity clock. Only its ordinary native
            // AI001 birth/component limit and sixteen-pixel falling cap apply.
            else if(shot.type==876){shot.tileCollide=shot.ai[1]==0;if(shot.velocity.Y>16)shot.velocity.Y=16;}
            else return false;
            float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X,out wind))return false;shot.velocity.X+=wind;
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(shot.Center.X,shot.Center.Y,shot.width,shot.height,out wet,out kind,true) || wet && kind==3 || !terrain.ProjectileLava(shot.Center.X,shot.Center.Y,shot.width,shot.height,out lava,true))return false;
                shot.wet=wet;if(lava)shot.lavaWet=true;if(wet && kind==2)shot.honeyWet=true;if(!wet)shot.honeyWet=shot.lavaWet=false;
            }
            Vector2 position=shot.position,velocity=shot.velocity,priorVelocity=shot.velocity;bool hit=false;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref position,ref velocity,shot.wet,shot.honeyWet,out wetVelocity,out hit,nativeClip:true))return false;
            shot.position=position;shot.velocity=velocity;
            if(hit)
            {
                if(shot.type==409){if(shot.velocity.X!=priorVelocity.X)shot.velocity.X=-priorVelocity.X;if(shot.velocity.Y!=priorVelocity.Y)shot.velocity.Y=-priorVelocity.Y;}
                else if(shot.type==1082 || shot.type==1083){shot.ai[1]=1;shot.velocity=priorVelocity;}
                else if(shot.type!=496){shot.active=false;return true;}
                // AI091 reflects a component only when tile clipping reduced
                // it below one pixel; a partial nonzero clip retires instead.
                if(shot.type==496 && shot.velocity.X!=priorVelocity.X){if(Math.Abs(shot.velocity.X)<1)shot.velocity.X=-priorVelocity.X;else{shot.active=false;return true;}}
                if(shot.type==496 && shot.velocity.Y!=priorVelocity.Y){if(Math.Abs(shot.velocity.Y)<1)shot.velocity.Y=-priorVelocity.Y;else{shot.active=false;return true;}}
            }
            shot.position+=shot.wet?wetVelocity:shot.velocity;return true;
        }
        private static List<Member> Copy(Projectile[] scene,HostAttackObstacles root,int selected)
        {
            var rows=new List<Member>(scene.Length+1);
            foreach(var shot in scene){var copy=HostBetsyFlameAttack.Copy(shot);rows.Add(new Member{Shot=copy,Receive=copy.owner==Main.myPlayer?root.ForkForSpawn(copy):null,SelectedSlot=selected});}
            return rows;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact,HostProjectileTargets targets=null)
        {
            contact=null;
            foreach(var member in rows)
            {
                var shot=member.Shot;if(!shot.active)continue;
                if(member.Receive!=null && !member.Receive.BeforeNpc(tick,outer,shot.Center.X,shot.Center.Y,shot.width,shot.height)){shot.active=false;continue;}
                for(int sub=0;sub<=shot.extraUpdates && shot.active;sub++)
                {
                    if(shot.type==409 && !HostTyphoonAttack.Ai(shot,terrain,targets,path,tick,outer,member.Receive))return false;
                    if(!Step(shot,terrain,ref member.Representative))return false;
                    if(!shot.active){slots.Release(shot.whoAmI);break;}
                    member.Selected=false;
                    if(member.Receive!=null && shot.friendly && !shot.hostile && shot.damage>0)
                    {
                        bool passed=member.Receive.PassCustomOuter(2,tick,outer,shot.Center.X,shot.Center.Y,shot.width,shot.height,null,consumeSelected:true,onHit:member.OnHit);
                        shot.penetrate=member.Receive.Remaining;
                        if(member.Source && member.Selected)
                        {
                            contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,shot.Center.X,shot.Center.Y,shot.width,shot.height,member.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                            if(contact!=null)return true;
                        }
                        if(!passed){if(shot.type==876 && member.Receive.OtherHits>0)return false;shot.active=false;if(shot.penetrate==0)slots.Release(shot.whoAmI);break;}
                    }
                    if(--shot.timeLeft<=0 || shot.penetrate==0){shot.active=false;slots.Release(shot.whoAmI);}
                }
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;
            root.CaptureShotFacing(player);var sceneList=new List<Projectile>();foreach(var shot in Main.projectile)if(shot.active && shot.type==ammo.Projectile && (ammo.Projectile!=409 || shot.owner==player.whoAmI))sceneList.Add(shot);
            var scene=sceneList.ToArray();if(ammo.Projectile==409)root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{409});else root.CaptureProjectileFamily(scene,ammo.Projectile,ammo.Projectile);var targetingSample=new Projectile();targetingSample.SetDefaults(ammo.Projectile);targetingSample.owner=player.whoAmI;var targets=ammo.Projectile==409?root.CaptureOwnerChaseTargets(targetingSample):null;var pool=root.CaptureBirthSlots();
            AttackMotion model;var proposals=HostAttackModels.TryRead(ammo,out model)?HostMotionAttackInputs.Generate(origin,model,path,age):new Vector2[0];
            var directions=new Vector2[proposals.Length+41];int count=0;
            for(int seed=0;seed<directions.Length;seed++)
            {
                CombatPredictionWork.Check();var box=path[Math.Min(path.Count-1,age+1+Math.Min(Math.Max(0,seed-proposals.Length),8)*4)].ProjectileReceiveBounds;
                var direction=seed<proposals.Length?proposals[seed]:seed-proposals.Length<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-proposals.Length-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                root.BeginCandidate();var rows=Copy(scene,root,path.Identity.Slot);var slots=pool.Copy();var aim=origin+direction*10000;bool available=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,aim,out ignored,targets)){available=false;break;}}
                if(!available)continue;int slot;if(!slots.TryAllocate(out slot))return null;
                var born=new Projectile();born.SetDefaults(ammo.Projectile);born.whoAmI=slot;born.active=true;born.owner=player.whoAmI;born.Center=origin;born.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);born.damage=ammo.Damage;
                if(born.type==876){var motion=new AttackMotion(ammo.Speed,0,0,1,born.width,born.height,born.timeLeft,componentBirthLimit:true);var velocity=born.velocity;motion.Launch(ref velocity.X,ref velocity.Y);born.velocity=velocity;}
                if(born.type==523)born.ai[0]=10;
                if(born.type==496)
                {
                    var item=player.HeldItem;int animation=player.itemAnimation>0?Math.Max(0,player.itemAnimation-delay):HostMeleeAttack.AnimationDuration(player,item);
                    bool final=animation<=item.useTime+1;
                    // The final actual Shoot is exact. Earlier shots choose
                    // legal native random values (100,0), +.010,+.010; zero
                    // acceleration is not a legal early random representative.
                    born.velocity=(final?direction:(direction*4+Vector2.UnitX).SafeNormalize(direction))*item.shootSpeed;
                    born.ai[0]=born.ai[1]=final?0:.01f;
                }
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,born.width,born.height,out wet,out kind,true))return null;born.wet=!born.ignoreWater && wet;born.honeyWet=wet && kind==2;
                // 1082/1083 Shoot chooses ai0 in [-5,0]. Zero is a legal
                // representative; only an observed born clock is exact.
                rows.Add(new Member{Shot=born,Receive=root.ForkForSpawn(born),SelectedSlot=path.Identity.Slot,Source=true,Representative=born.type!=876 && born.type!=409 && (born.type!=496 || born.ai[0]!=0)});
                rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
                for(int outer=1;outer<=Math.Min(120,path.Count-age-1);outer++)
                {
                    CombatPredictionWork.Check();AttackContact contact;if(!Outer(rows,slots,path,terrain,age+outer,delay+outer,aim,out contact,targets))break;if(contact!=null)return contact;
                    if(!born.active)break;
                }
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            var live=new List<Projectile>();Projectile typhoon=null;foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type)){live.Add(p);if(p.type==409)typhoon=p;}
            var scene=live.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{206,410,523,876,496,1082,1083,409});var targets=typhoon==null?null:root.CaptureOwnerChaseTargets(typhoon);root.BeginCandidate();var rows=Copy(scene,root,path.Identity.Slot);
            for(int index=0;index<rows.Count;index++)rows[index].Source=Array.IndexOf(source,scene[index])>=0;
            var slots=root.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,out contact,targets))return null;if(contact!=null)return contact;}return null;
        }
    }
}
