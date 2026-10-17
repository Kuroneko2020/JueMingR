using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Stellar Tune seals one native random target/upper arc at Shoot. Its
    // AI167 curve subsequently follows the owner's center, not the mouse.
    internal static class HostGuitarCurveAttack
    {
        internal static bool Handles(int type){return type==856;}
        internal sealed class BirthState
        {
            internal readonly NPC[] Targets;internal readonly Vector2[] Velocities;
            private readonly Player player;private readonly Rectangle area;private readonly Vector2[] positions;private readonly Rectangle[] boxes;private readonly NpcIdentity[] identities;private readonly long session;
            internal BirthState(Player p,long session)
            {player=p;this.session=session;area=Utils.CenteredRectangle(p.Center,new Vector2(1000,800));var list=new List<NPC>();foreach(var n in Main.npc)if(n.CanBeChasedBy(p) && n.Hitbox.Intersects(area))list.Add(n);Targets=list.ToArray();Velocities=Array.ConvertAll(Targets,n=>n.velocity);positions=Array.ConvertAll(Targets,n=>n.position);boxes=Array.ConvertAll(Targets,n=>n.Hitbox);identities=Array.ConvertAll(Targets,n=>CombatSelection.Identity(n,session));}
            internal bool Current
            {get{if(area!=Utils.CenteredRectangle(player.Center,new Vector2(1000,800)))return false;int at=0;foreach(var n in Main.npc)if(n.CanBeChasedBy(player) && n.Hitbox.Intersects(area)){if(at>=Targets.Length || !ReferenceEquals(Targets[at],n) || !CombatSelection.Valid(identities[at],session) || positions[at]!=n.position || boxes[at]!=n.Hitbox || Velocities[at]!=n.velocity)return false;at++;}return at==Targets.Length;}}
        }
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        internal static void Kill(Projectile p)
        {var center=p.Center;p.width=p.height=128;p.Center=center;p.maxPenetrate=p.penetrate=-1;p.timeLeft=0;p.active=false;}
        internal static bool Ai(Projectile p,Vector2 ownerCenter)
        {if(++p.localAI[0]>=89){Kill(p);return false;}float t=p.localAI[0]/90;var end=new Vector2(p.ai[0],p.ai[1]);var tangent=-p.velocity;p.Center=Vector2.CatmullRom(ownerCenter+tangent*2,ownerCenter,end,end+tangent*(1-t*3),t);p.rotation=MathHelper.TwoPi*t;return true;}
        internal static Projectile Birth(Player player,Vector2 origin,Vector2 aim,Vector2? nativeTarget,Vector2 targetVelocity,int damage)
        {
            var end=nativeTarget.HasValue?nativeTarget.Value+targetVelocity*20:aim;var delta=end-player.Center;
            if(!nativeTarget.HasValue && delta.Length()>700){delta*=700/delta.Length();end=player.Center+delta;}
            // UnitY up is a legal edge-circle draw, with no extra circular
            // endpoint jitter. This is explicitly a random representative.
            var p=new Projectile();p.SetDefaults(856);p.active=true;p.owner=player.whoAmI;p.Center=origin;p.velocity=-Vector2.UnitY*delta.Length()*2;p.damage=damage;p.knockBack=player.HeldItem.knockBack;p.ai[0]=end.X;p.ai[1]=end.Y;return p;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && p.type==856)list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{856});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var list=new List<Member>();foreach(var p in scene){var q=HostNebulaBlazeAttack.Copy(p);list.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=sources!=null && Array.IndexOf(sources,p)>=0});}return list;}
        private static bool Outer(List<Member> rows,NpcTrajectory path,int tick,int outer,Vector2 ownerCenter,int ownerWidth,int ownerHeight,PredictionTerrain terrain,Vector2 aim,out AttackContact contact,HostProjectileBirthSlots slots=null)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return false;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool burst=!Ai(p,ownerCenter);if(!HostProjectileEnvironment.Dry(p,terrain,p.Center.X,p.Center.Y,p.velocity.X))return false;if(!Main.player[p.owner].active && !burst){Kill(p);burst=true;}
                    bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),new MotionRect(ownerCenter.X-ownerWidth*.5f,ownerCenter.Y-ownerHeight*.5f,ownerWidth,ownerHeight),out clear,out stop))return false;
                    if(p.damage>0 && (clear || (int)(p.damage*.75)>0))
                    {
                        int before=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,unlimited:burst,consumeSelected:true);if(!burst)p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>before){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!pass && !burst && p.penetrate!=0)return false;
                        if(!burst && (!pass || p.timeLeft<=1))
                        {
                            // A first body hit or expiry invokes the SAME
                            // body's native128px Damage after local immunity
                            // was written; do not manufacture a fresh child.
                            Kill(p);before=m.Receive.SelectedHits;
                            pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,unlimited:true,consumeSelected:true);
                            if(m.Source && m.Receive.SelectedHits>before){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass)return false;burst=true;
                        }
                    }
                    if(burst)break;if(--p.timeLeft<=0){Kill(p);break;}
                }
                if(!p.active && slots!=null)slots.Release(p.whoAmI);
            }
            return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.Age<0 || clock.LaunchDelay!=0)return null;root.CaptureHomingInputs(player,false,true);var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,sources,root);var prefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);var aim=new Vector2(sources[0].ai[0],sources[0].ai[1]);
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){PredictionPlayerMotion owner;if(!prefix.TryRead(action+(clock.NextWorld?1:0),out owner))return null;AttackContact contact;if(!Outer(rows,path,clock.FirstTick+action,action+1,new Vector2(owner.X+player.width*.5f,owner.Y+player.height*.5f),player.width,player.height,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay,HostAttackPlayerFrame? preparedPlayer=null,bool launchPlayerAdvanced=false)
        {
            if(ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;root.CaptureHomingInputs(player,false,true);var inputs=root.CaptureGuitarBirth(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var prefix=new HostAttackPlayerPrefix(preparedPlayer??HostAttackPlayerFrame.Capture(player,path),terrain,121);
            // Public preview already moved its private holder to launch.
            // Replay old bodies from the real owner's original phase, then
            // continue the supplied launch frame without advancing delay twice.
            var waitPrefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(Main.player[player.whoAmI],path),terrain,delay+1);int first=!beforeNpc && !launchPlayerAdvanced?1:0;
            int candidates=inputs.Targets.Length>0?inputs.Targets.Length:9;
            for(int seed=0;seed<candidates;seed++)
            {
                int target=inputs.Targets.Length>0?inputs.Targets[seed].whoAmI:-1;var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=HostDirectionalControl.Cursor(player,new Vector2(b.CenterX,b.CenterY));Vector2? center=target<0?(Vector2?)null:inputs.Targets[seed].Center;var velocity=target<0?Vector2.Zero:inputs.Velocities[seed];if(target==path.Identity.Slot){var point=path[age];center=new Vector2(point.Bounds.CenterX,point.Bounds.CenterY);velocity=new Vector2(point.Vx,point.Vy);}
                root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool available=true;
                for(int action=0;action<delay;action++){PredictionPlayerMotion owner;if(!waitPrefix.TryRead(action+(beforeNpc?0:1),out owner)){available=false;break;}AttackContact ignored;if(!Outer(rows,path,age-delay+action+1,action+1,new Vector2(owner.X+player.width*.5f,owner.Y+player.height*.5f),player.width,player.height,terrain,aim,out ignored,slots)){available=false;break;}}if(!available)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var p=Birth(player,origin,aim,center,velocity,ammo.Damage);p.whoAmI=slot;rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true,Representative=true});rows.Sort((a,b2)=>a.Shot.whoAmI.CompareTo(b2.Shot.whoAmI));
                for(int action=0;action<Math.Min(120,path.Count-age-1);action++){PredictionPlayerMotion owner;if(!prefix.TryRead(action+first,out owner))break;AttackContact contact;if(!Outer(rows,path,age+action+1,delay+action+1,new Vector2(owner.X+player.width*.5f,owner.Y+player.height*.5f),player.width,player.height,terrain,aim,out contact,slots))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}
