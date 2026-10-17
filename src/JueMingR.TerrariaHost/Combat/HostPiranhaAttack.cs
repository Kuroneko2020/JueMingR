using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI039 never reads the mouse after birth. Three ordinary launch bodies
    // subsequently share ordered separation/attachment/return behavior. Held
    // channel is a condition of that continuation, not a new input consumer.
    internal static class HostPiranhaAttack
    {
        internal static bool Handles(int type){return type==190;}
        internal static bool Family(int type){return Handles(type);}
        internal sealed class OwnerLease
        {
            private readonly Player owner;private readonly bool dead,channel,observed;
            internal OwnerLease(Player p,bool observed){owner=p;dead=p.dead;channel=p.channel;this.observed=observed;}
            internal bool Current {get{return owner.dead==dead && (!observed || owner.channel==channel);}}
        }
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static int Find(HostProjectileTargets targets,int slot)
        {for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot)return i;return -1;}
        private static void Face(Projectile p)
        {p.spriteDirection=p.velocity.X<0?-1:1;p.rotation=p.velocity.X<0?(float)Math.Atan2(-p.velocity.Y,-p.velocity.X):(float)Math.Atan2(p.velocity.Y,p.velocity.X);}
        internal static bool Ai(Projectile p,Player owner,Projectile[] family,HostProjectileTargets bodies,HostProjectileTargets chase,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            p.alpha=Math.Max(0,p.alpha-50);
            if(!p.active || !owner.active || owner.dead || Vector2.Distance(owner.Center,p.Center)>2000){p.active=false;p.timeLeft=0;return true;}
            var delta=owner.Center-p.Center;float distance=delta.Length();if(distance==0)distance=.001f;
            if(!owner.channel && p.alpha==0){p.ai[0]=1;p.ai[1]=-1;}
            if(p.ai[1]>0 && distance>1500){p.ai[1]=0;p.ai[0]=1;}
            if(p.ai[1]>0)
            {
                p.tileCollide=false;int n=Find(bodies,(int)p.ai[1]-1);
                if(n>=0 && bodies.Alive(n))
                {
                    var b=bodies.Bounds(n,path,tick);delta=new Vector2(b.CenterX,b.CenterY)-p.Center;distance=delta.Length();
                    if(distance<16){p.velocity=delta;if(distance>16f/3){Face(p);p.velocity=Vector2.Zero;}}
                    else{if(distance==0)distance=.0001f;float scale=16/distance;p.velocity=new Vector2(delta.X*scale,delta.Y*scale);Face(p);}
                    p.position+=bodies.Velocity(n,path,tick);
                    foreach(var other in family)if(other.active && other.whoAmI!=p.whoAmI && other.owner==p.owner && Vector2.Distance(p.Center,other.Center)<8){p.velocity.X+=p.position.X<other.position.X?-4:4;p.velocity.Y+=p.position.Y<other.position.Y?-4:4;}
                    if(p.owner==Main.myPlayer)p.ai[0]=1;
                }
                else if(p.owner==Main.myPlayer)
                {
                    p.ai[1]=0;int target=-1;float nearest=3000;
                    for(int i=0;i<chase.Count;i++)
                    {
                        var b=chase.Bounds(i,path,tick);float manhattan=Math.Abs(p.Center.X-b.CenterX)+Math.Abs(p.Center.Y-b.CenterY);if(manhattan>=nearest)continue;bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop))return false;if(clear){nearest=manhattan;target=i;}
                    }
                    if(target>=0){var b=chase.Bounds(target,path,tick);delta=new Vector2(b.CenterX,b.CenterY)-p.Center;distance=delta.Length();if(distance==0)distance=.0001f;float scale=16/distance;p.velocity=new Vector2(delta.X*scale,delta.Y*scale);p.ai[0]=0;p.ai[1]=chase.Slot(target)+1;}
                }
            }
            else if(p.ai[0]==0){if(p.owner==Main.myPlayer && distance>700)p.ai[0]=1;Face(p);}
            else if(p.ai[0]==1)
            {
                p.tileCollide=false;Face(p);if(p.owner==Main.myPlayer && distance<70){p.active=false;p.timeLeft=0;}
                float scale=20/distance;p.velocity=new Vector2(delta.X*scale,delta.Y*scale);p.position+=owner.velocity;
            }
            return true;
        }
        internal static bool Move(Projectile p,PredictionTerrain terrain,Vector2? beforeAi=null)
        {
            if(!p.active)return true;var wetVelocity=beforeAi??p.velocity;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater){bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;p.wet=wet;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(lava)p.lavaWet=true;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;}
            // Shimmer has no AI039 velocity branch. It still selects the
            // native .375 movement factor when tile collision is enabled.
            var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;if(hit)p.ai[0]=1;p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;return true;
        }
        private static bool Outer(Player owner,List<Member> rows,HostProjectileTargets bodies,HostProjectileTargets chase,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;var family=rows.ConvertAll(m=>m.Shot).ToArray();
            foreach(var m in rows){var p=m.Shot;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
            {
                CombatPredictionWork.Check();if(HostRaySpellAttack.WorldRetired(p)){p.active=false;break;}var beforeAi=p.velocity;if(!Ai(p,owner,family,bodies,chase,path,tick,terrain) || !Move(p,terrain,beforeAi))return false;if(!p.active)break;
                int before=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,left)=>{if(p.ai[1]==0)p.ai[1]=slot+1;return true;});p.penetrate=m.Receive.Remaining;
                if(m.Source && m.Receive.SelectedHits>before)return Contact(p,path,tick,sub,aim,m.Random,out contact);if(!pass && p.penetrate!=0)return false;if(!pass || --p.timeLeft<=0){p.active=false;p.timeLeft=0;}
            }}return true;
        }
        private static bool Contact(Projectile p,NpcTrajectory path,int tick,int sub,Vector2 aim,bool random,out AttackContact contact)
        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,random?AttackConfidence.Representative:AttackConfidence.Conditional);return contact!=null;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))scene.Add(p);root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{190});return scene.ToArray();}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){var q=HostNoteAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=source!=null && Array.IndexOf(source,p)>=0});}return rows;}
        private static bool Owner(HostAttackPlayerPrefix prefix,Player body,int index,out PredictionPlayerMotion motion)
        {if(!prefix.TryRead(index,out motion))return false;HostAttackPlayerFrame.ApplyPose(body,motion);return true;}
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;var scene=Capture(player,root);root.CaptureHomingInputs(player,false,true);root.CapturePiranhaOwner(player,true);var bodies=root.CapturePiranhaTargets(source[0]);var chase=root.CaptureMovingChaseTargets(source[0]);root.BeginCandidate();var rows=Copy(scene,source,root);var body=HostMeleeAttack.PlayerCopy(player);var prefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){PredictionPlayerMotion motion;if(!Owner(prefix,body,step+(clock.NextWorld?1:0),out motion))return null;AttackContact contact;if(!Outer(body,rows,bodies,chase,path,clock.FirstTick+step,step+1,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay,HostAttackPlayerFrame? preparedPlayer=null,bool launchPlayerAdvanced=false)
        {
            if(ammo.Projectile!=190 || ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);root.CaptureHomingInputs(player,false,true);root.CapturePiranhaOwner(player,false);var scene=Capture(player,root);var bodies=root.CapturePiranhaTargets(Terraria.ID.ContentSamples.ProjectilesByType[190]);var chase=root.CaptureMovingChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[190]);var pool=root.CaptureBirthSlots();var prefix=new HostAttackPlayerPrefix(preparedPlayer??HostAttackPlayerFrame.Capture(player,path),terrain,121);var inputs=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(inputs.Contains(direction))continue;inputs.Add(direction);var aim=origin+direction*10000;root.BeginCandidate();var rows=Copy(scene,null,root);var body=HostMeleeAttack.PlayerCopy(player);var slots=pool.Copy();bool valid=true;
                // Preview already supplies the launch pose after cooldown.
                // Existing members alone must first traverse the real phase
                // prefix; advancing the launch pose through delay again would
                // shift both their return and the newly born hand twice.
                if(delay>0 && rows.Count>0)
                {var live=Main.player[player.whoAmI];var prior=HostMeleeAttack.PlayerCopy(live);var waitPrefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(live,path),terrain,delay+2);for(int wait=1;wait<=delay;wait++){PredictionPlayerMotion motion;if(!Owner(waitPrefix,prior,wait-(beforeNpc?1:0),out motion)){valid=false;break;}AttackContact ignored;if(!Outer(prior,rows,bodies,chase,path,age-delay+wait,wait,terrain,aim,out ignored)){valid=false;break;}}}if(!valid)continue;foreach(var old in rows)if(!old.Shot.active)slots.Release(old.Shot.whoAmI);
                int count=rows.FindAll(row=>row.Shot.active).Count;for(int born=count;born<3;born++){int slot;if(!slots.TryAllocate(out slot)){valid=false;break;}var p=new Projectile();p.SetDefaults(190);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true,Random=true});}if(!valid)return null;rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));body.channel=true;
                for(int step=1;age+step<path.Count;step++){PredictionPlayerMotion motion;if(!Owner(prefix,body,step-(beforeNpc || launchPlayerAdvanced?1:0),out motion))break;AttackContact contact;if(!Outer(body,rows,bodies,chase,path,age+step,delay+step,terrain,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}
