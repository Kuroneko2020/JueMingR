using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The slow orb emits orbiters keyed to its actual parent. Kill first does
    // a local-immunity-preserving 176px Damage, retires keyed orbiters, then
    // births two finite AI029 fragment groups while the parent slot is held.
    internal static class HostNebulaArcanumAttack
    {
        internal static bool Handles(int type){return type==617;}
        internal static bool Family(int type){return type==617 || type==618 || type==620;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal Member Parent;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random=false)
        {return new Member{Shot=p,Receive=p.owner==Main.myPlayer?root.ForkForSpawn(p):null,Source=source,Random=random};}
        private static int Closest(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,out bool valid)
        {
            valid=true;int nearest=-1;float best=800;
            for(int i=0;i<targets.Count;i++){var b=targets.Bounds(i,path,tick);float distance=Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY));if(distance>=best)continue;bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.Center.X,p.Center.Y,1,1),b,out clear,out stop)){valid=false;return -1;}if(clear){best=distance;nearest=targets.Slot(i);}}
            return nearest;
        }
        internal static bool ParentAi(Projectile p,HostProjectileTargets find,HostProjectileTargets locked,NpcTrajectory path,int tick,PredictionTerrain terrain,out bool spawn,out bool killed)
        {
            spawn=killed=false;p.ai[0]++;bool slow=p.velocity.Length()<=4;p.alpha=Math.Max(0,p.alpha-15);p.rotation-=MathHelper.Pi/30;
            if(!slow)
            {
                if(p.ai[0]>=30){p.velocity*=.98f;p.scale=Math.Min(1.3f,p.scale+.0074468083f);p.rotation-=MathHelper.Pi/180;}
                if(p.velocity.Length()<4.1f){p.velocity.Normalize();p.velocity*=4;p.ai[0]=0;}
            }
            else
            {
                spawn=p.ai[0]%30==0 && p.ai[0]<241 && p.owner==Main.myPlayer;
                if(p.ai[1]==0){bool valid;int index=Closest(p,find,path,tick,terrain,out valid);if(!valid)return false;if(index>=0)p.ai[1]=index+1;}
                int slot=(int)(p.ai[1]-1);MotionRect? target=null;for(int i=0;i<locked.Count;i++)if(locked.Slot(i)==slot){var b=locked.Bounds(i,path,tick);if(Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY))<1000)target=b;break;}
                if(target.HasValue && p.friendly)
                {
                    float dx=target.Value.CenterX-p.Center.X,dy=target.Value.CenterY-p.Center.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);float factor=4/length;dx*=factor;dy*=factor;p.velocity.X=(p.velocity.X*7+dx)/8;p.velocity.Y=(p.velocity.Y*7+dy)/8;
                }
            }
            killed=p.ai[0]>=600;return true;
        }
        internal static bool ChildAi(Projectile p,Projectile parent)
        {
            p.ai[0]++;if(p.ai[0]>=420)return true;
            if(parent==null || !parent.active){p.ai[0]=420;p.active=false;p.timeLeft=0;return true;}
            if(parent.oldPos.Length>1 && parent.oldPos[1]!=Vector2.Zero)p.position+=parent.position-parent.oldPos[1];
            if(float.IsNaN(p.Center.X) || float.IsNaN(p.Center.Y)){p.active=false;p.timeLeft=0;return true;}
            p.velocity+=new Vector2(Math.Sign(parent.Center.X-p.Center.X),Math.Sign(parent.Center.Y-p.Center.Y))*.15f;
            if(p.velocity.Length()>6)p.velocity*=6/p.velocity.Length();p.alpha=255;return true;
        }
        private static bool Move(Projectile p,PredictionTerrain terrain)
        {
            var wetVelocity=p.velocity;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater){bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;}
            if(p.shimmerWet){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
            var prior=p.velocity;var position=p.position;var velocity=p.velocity;bool hit=false;

            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit && p.type==617){if(velocity.X!=prior.X)p.velocity.X=-prior.X*.35f;if(velocity.Y!=prior.Y)p.velocity.Y=-prior.Y*.35f;}
            p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;return true;
        }
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,int sub,HostAttackObstacles root,PredictionTerrain terrain,Vector2 aim,out AttackContact contact)
        {
            contact=null;var p=m.Shot;if(!p.active)return true;p.timeLeft=0;
            if(p.type==617)
            {
                if(m.Receive!=null)
                {
                    var burst=HostBetsyFlameAttack.Copy(p);burst.width=burst.height=176;burst.Center=p.Center;burst.maxPenetrate=burst.penetrate=-1;var receive=m.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;bool pass=receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,176,176,null,unlimited:true,consumeSelected:true);
                    if(m.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,176,176,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass)return false;
                }
                if(p.owner==Main.myPlayer)
                {
                    foreach(var child in rows)if(child.Shot.active && child.Shot.type==618 && ReferenceEquals(child.Parent,m)){child.Shot.active=false;child.Shot.timeLeft=0;slots.Release(child.Shot.whoAmI);}
                    // Each count5, zero offset and horizontal unit vector is a
                    // legal independent draw. Preserve both native groups.
                    for(int group=0;group<2;group++)for(int i=0;i<5;i++)
                    {
                        int slot;if(!slots.TryAllocate(out slot))return false;var q=new Projectile();q.SetDefaults(620);q.active=true;q.whoAmI=slot;q.owner=p.owner;q.Center=p.Center;q.velocity=Vector2.UnitX*(group==0?7:4);q.damage=(int)(p.damage*.65);q.knockBack=p.knockBack*.8f;q.ai[0]=group==0?86:90;bool wet;byte kind;if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true))return false;q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;rows.Add(Make(q,root,m.Source,true));
                    }
                    foreach(var row in rows)row.Random=true;
                }
            }
            p.active=false;slots.Release(p.whoAmI);return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets find,HostProjectileTargets locked,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;for(int visits=0;visits<1000;visits++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);break;}bool spawn=false,killed=false;
                    if(p.type==617){if(!ParentAi(p,find,locked,path,tick,terrain,out spawn,out killed))return false;}
                    else if(p.type==618)ChildAi(p,m.Parent?.Shot);
                    else if(!HostAi29SpellAttack.Step(p,terrain,false))return false;
                    if(killed){if(!Kill(m,rows,slots,path,tick,outer,sub,root,terrain,aim,out contact))return false;if(contact!=null)return true;}
                    if(spawn)
                    {
                        int slot;if(!slots.TryAllocate(out slot))return false;var q=new Projectile();q.SetDefaults(618);q.active=true;q.whoAmI=slot;q.owner=p.owner;q.Center=p.Center;q.velocity=Vector2.UnitY*12;q.damage=p.damage/2;q.ai[1]=(float)p.key;bool wet;byte kind;if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true))return false;q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;var row=Make(q,root,m.Source,true);row.Parent=m;rows.Add(row);foreach(var member in rows)member.Random=true;
                    }
                    if(!p.active){slots.Release(p.whoAmI);break;}if(p.type!=620 && !Move(p,terrain))return false;
                    if(m.Receive!=null && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,p.type==620?(Func<MotionRect,bool>)(b=>HostAi29SpellAttack.DamageBox(p).Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))):null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){var box=p.type==620?HostAi29SpellAttack.DamageBox(p):p.Hitbox;contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && p.penetrate!=0)return false;
                    }
                    if(p.type==617 && p.oldPos.Length>0){for(int index=p.oldPos.Length-1;index>0;index--)p.oldPos[index]=p.oldPos[index-1];p.oldPos[0]=p.position;}
                    if(--p.timeLeft<=0 || p.penetrate==0){if(!Kill(m,rows,slots,path,tick,outer,sub,root,terrain,aim,out contact))return false;if(contact!=null)return true;break;}
                }
            }return false;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var list=new List<Projectile>();var parents=new HashSet<float>();var needed=new HashSet<float>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type)){list.Add(p);if(p.type==617)parents.Add((float)p.key);else if(p.type==618)needed.Add(p.ai[1]);}
            foreach(var p in Main.projectile)if(p.active && p.type==617 && needed.Contains((float)p.key) && !list.Contains(p)){list.Add(p);parents.Add((float)p.key);}foreach(var p in Main.projectile)if(p.active && p.type==618 && parents.Contains(p.ai[1]) && !list.Contains(p))list.Add(p);list.Sort((a,b)=>a.whoAmI.CompareTo(b.whoAmI));var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{617,618,620});return scene;
        }
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){var q=HostBetsyFlameAttack.Copy(p);q.key=p.key;rows.Add(Make(q,root,source!=null && Array.IndexOf(source,p)>=0));}foreach(var m in rows)if(m.Shot.type==618)foreach(var p in rows)if(p.Shot.type==617 && (float)p.Shot.key==m.Shot.ai[1]){m.Parent=p;break;}return rows;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=617 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var sample=Terraria.ID.ContentSamples.ProjectilesByType[617];var find=root.CaptureChaseTargets(sample);var locked=root.CaptureSeedlerTargets(sample);var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool valid=true;for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,find,locked,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var q=new Projectile();q.SetDefaults(617);q.active=true;q.owner=player.whoAmI;q.whoAmI=slot;q.Center=origin;q.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);q.damage=ammo.Damage;q.knockBack=player.HeldItem.knockBack;rows.Add(Make(q,root,true));for(int step=1;age+step<path.Count;step++){AttackContact contact;if(!Outer(rows,slots,find,locked,path,age+step,delay+step,terrain,root,aim,out contact))break;if(contact!=null)return contact;}
            }return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=Capture(player,root);var find=root.CaptureChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[617]);var locked=root.CaptureSeedlerTargets(Terraria.ID.ContentSamples.ProjectilesByType[617]);root.BeginCandidate();var rows=Copy(scene,source,root);var slots=root.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,find,locked,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
