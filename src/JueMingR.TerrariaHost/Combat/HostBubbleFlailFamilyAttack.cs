using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // The original404 and the modern1058 have different owner/input motors.
    // Their405 descendants share a finite AI070 life, not a new mouse window.
    internal static class HostBubbleFlailFamilyAttack
    {
        internal static bool Handles(int type){return type==404 || type==1058;}
        internal static bool Family(int type){return Handles(type) || type==405;}
        internal sealed class Inputs
        {
            private readonly Player owner;private readonly bool channel,use,map,dead,active,noItems,cc,readsCursor;private readonly float speed;private readonly Vector2 cursor;
            private readonly List<Tuple<NPC,Vector2,int,int>> locked=new List<Tuple<NPC,Vector2,int,int>>();
            internal Inputs(Player p,Projectile[] scene)
            {
                owner=Main.player[p.whoAmI];channel=owner.channel;use=owner.controlUseItem;speed=owner.meleeSpeed;map=Main.mapFullscreen;dead=owner.dead;active=owner.active;noItems=owner.noItems;cc=owner.CCed;cursor=Main.MouseWorld;foreach(var q in scene)if(q.type==1058 && q.ai[0]==0)readsCursor=true;
                foreach(var q in scene)if(q.type==405 && q.ai[0]>0 && q.ai[0]<=Main.maxNPCs)
                {var n=Main.npc[(int)q.ai[0]-1];if(!locked.Exists(r=>ReferenceEquals(r.Item1,n)))locked.Add(Tuple.Create(n,n.position,n.width,n.height));}
            }
            internal bool MembersCurrent {get{return owner.channel==channel && owner.controlUseItem==use && owner.meleeSpeed==speed && Main.mapFullscreen==map && owner.dead==dead && owner.active==active && owner.noItems==noItems && owner.CCed==cc && (!readsCursor || Main.MouseWorld==cursor);}}
            internal bool Current {get{if(!MembersCurrent)return false;foreach(var r in locked){var n=r.Item1;if(!ReferenceEquals(Main.npc[n.whoAmI],n) || n.position!=r.Item2 || n.width!=r.Item3 || n.height!=r.Item4)return false;}return true;}}
            internal bool Locked(int slot,out MotionRect bounds)
            {foreach(var r in locked)if(r.Item1.whoAmI==slot){bounds=new MotionRect(r.Item2.X,r.Item2.Y,r.Item3,r.Item4);return true;}bounds=default(MotionRect);return false;}
        }
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var q in Main.projectile)if(q.active && q.owner==player.whoAmI && Family(q.type))list.Add(q);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{404,405,1058});root.CaptureBubbleFlailInputs(player,scene);root.CaptureHomingInputs(player,false,true);return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var q in scene){var p=HostSuperStarAttack.Copy(q);rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=sources!=null && Array.IndexOf(sources,q)>=0});}return rows;}
        private static bool Bounds(int slot,HostProjectileTargets targets,Inputs inputs,NpcTrajectory path,int tick,out MotionRect b)
        {if(slot==path.Identity.Slot){b=path[tick].Bounds;return true;}for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){b=targets.Bounds(i);return true;}return inputs.Locked(slot,out b);}
        internal static bool Bubble(Projectile p,HostProjectileTargets targets,Inputs inputs,NpcTrajectory path,int tick,PredictionTerrain terrain,out bool random)
        {
            random=false;if(p.ai[0]==0)
            {
                float distance=650;int slot=-1;MotionRect nearest=default(MotionRect);
                for(int i=0;i<targets.Count;i++){var b=targets.Bounds(i,path,tick);float d=Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY));if(d>=distance)continue;bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop))return false;if(!clear)continue;distance=d;slot=targets.Slot(i);nearest=b;}
                p.ai[0]=slot+1;if(slot<0)p.ai[0]=-15;else{p.velocity=(p.velocity*20+(new Vector2(nearest.CenterX,nearest.CenterY)-p.Center).SafeNormalize(Vector2.Zero)*(55f/30))/21;random=true;}
            }
            else if(p.ai[0]>0)
            {MotionRect b;if(!Bounds((int)p.ai[0]-1,targets,inputs,path,tick,out b))return false;p.velocity=(p.velocity*40+(new Vector2(b.CenterX,b.CenterY)-p.Center).SafeNormalize(Vector2.Zero)*12)/41;}
            else{p.ai[0]++;p.alpha=Math.Max(50,p.alpha-25);p.velocity*=.95f;}
            if(p.ai[1]==0){p.ai[1]=1;random=true;}p.scale=p.ai[1];return true;
        }
        internal static bool Parent(Projectile p,Player body,Vector2 cursor,out bool birth,out float knockback)
        {
            birth=false;knockback=p.knockBack;
            if(p.type==404)
            {
                var delta=body.Center-p.Center;p.rotation=delta.ToRotation()-1.57f;if(body.dead){Kill(p);return true;}body.SetDummyItemTime(10);p.direction=delta.X<0?1:-1;body.ChangeDir(p.direction);body.itemRotation=(-delta*p.direction).ToRotation();p.spriteDirection=delta.X>0?-1:1;
                float distance=delta.Length();if(p.ai[0]==0 && distance>400)p.ai[0]=1;if(p.ai[0]==1 || p.ai[0]==2){if(distance>1500){Kill(p);return true;}if(distance>600)p.ai[0]=2;p.tileCollide=false;float speed=p.ai[0]==2?40:20;p.velocity=Vector2.Normalize(delta)*speed;if(distance<speed){Kill(p);return true;}}
                p.ai[1]++;if(p.ai[1]>5)p.alpha=0;birth=(int)p.ai[1]%4==0 && p.owner==Main.myPlayer;return true;
            }
            float rate=1/body.meleeSpeed;if(!body.active || body.dead || body.noItems || body.CCed || Vector2.Distance(p.Center,body.Center)>900*rate || p.owner==Main.myPlayer && Main.mapFullscreen){Kill(p);return true;}
            Vector2 mounted=body.MountedCenter,deltaOwner=mounted-p.Center;float distanceOwner=deltaOwner.Length();p.localNPCHitCooldown=10;bool spin=false;
            switch((int)p.ai[0])
            {
                case 0:
                    spin=true;if(p.owner==Main.myPlayer){var unit=(cursor-mounted).SafeNormalize(Vector2.UnitX*body.direction);body.ChangeDir(unit.X>0?1:-1);if(!body.channel){p.ai[0]=1;p.ai[1]=0;p.velocity=unit*(23*rate)+body.velocity;p.Center=mounted;Array.Clear(p.localNPCImmunity,0,p.localNPCImmunity.Length);p.localNPCHitCooldown=10;break;}}
                    p.localAI[1]++;var orbit=new Vector2(body.direction).RotatedBy(MathHelper.Pi*10*(p.localAI[1]/60)*body.direction);orbit.Y*=.8f;if(orbit.Y*body.gravDir>0)orbit.Y*=.5f;p.Center=mounted+orbit*45;p.velocity=Vector2.Zero;p.localNPCHitCooldown=10;break;
                case 1:
                    bool turn=p.ai[1]++>=16 || distanceOwner>=800;if(body.controlUseItem){p.ai[0]=6;p.ai[1]=0;p.velocity*=.2f;break;}if(turn){p.ai[0]=2;p.ai[1]=0;p.velocity*=.3f;}body.ChangeDir(body.Center.X<p.Center.X?1:-1);p.localNPCHitCooldown=10;break;
                case 2:
                    if(distanceOwner<=16*rate){Kill(p);return true;}if(body.controlUseItem){p.ai[0]=6;p.ai[1]=0;p.velocity*=.2f;}else{p.velocity*=.98f;p.velocity=p.velocity.MoveTowards(deltaOwner.SafeNormalize(Vector2.Zero)*(16*rate),3*rate);body.ChangeDir(body.Center.X<p.Center.X?1:-1);}break;
                case 3:
                    if(!body.controlUseItem){p.ai[0]=4;p.ai[1]=0;break;}p.tileCollide=p.ai[1]==1;bool tiles=distanceOwner<=23*rate*16;if(tiles!=p.tileCollide){p.tileCollide=tiles;p.ai[1]=tiles?1:0;}if(distanceOwner>60){if(distanceOwner>=23*rate*16){p.velocity*=.5f;p.velocity=p.velocity.MoveTowards(deltaOwner.SafeNormalize(Vector2.Zero)*(14*rate),14*rate);}p.velocity*=.98f;p.velocity=p.velocity.MoveTowards(deltaOwner.SafeNormalize(Vector2.Zero)*(14*rate),rate);}else{if(p.velocity.Length()<6){p.velocity.X*=.96f;p.velocity.Y+=.2f;}if(body.velocity.X==0)p.velocity.X*=.96f;}body.ChangeDir(body.Center.X<p.Center.X?1:-1);break;
                case 4:
                    p.tileCollide=false;if(distanceOwner<=48*rate){Kill(p);return true;}var direction=deltaOwner.SafeNormalize(Vector2.Zero);p.velocity*=.98f;p.velocity=p.velocity.MoveTowards(direction*(48*rate),6*rate);if(Vector2.Dot(direction,(mounted-p.Center-p.velocity).SafeNormalize(Vector2.Zero))<0){Kill(p);return true;}body.ChangeDir(body.Center.X<p.Center.X?1:-1);break;
                case 5:
                    // Native num14 is initialized from the default10 BEFORE
                    // type1058 changes outbound to16; bounce wait stays15.
                    if(p.ai[1]++>=15){p.ai[0]=6;p.ai[1]=0;}else{p.localNPCHitCooldown=10;p.velocity.Y+=.6f;p.velocity.X*=.95f;body.ChangeDir(body.Center.X<p.Center.X?1:-1);}break;
                case 6:
                    if(!body.controlUseItem || distanceOwner>23*rate*16+160){p.ai[0]=4;p.ai[1]=0;break;}if(!p.shimmerWet)p.velocity.Y+=.8f;p.velocity.X*=.95f;body.ChangeDir(body.Center.X<p.Center.X?1:-1);break;
                default:return false;
            }
            p.alpha=Math.Max(0,p.alpha-50);var d=body.Center-p.Center;p.rotation=d.ToRotation()-1.57f;p.direction=d.X<0?1:-1;p.spriteDirection=p.direction;
            if(p.owner==Main.myPlayer){int interval=(int)p.ai[0]==0?4:(int)p.ai[0]==6 || (int)p.ai[0]==3?6:4;if(p.ai[0]==0)knockback*=.5f;p.localAI[2]++;if((int)p.localAI[2]>=interval){p.localAI[2]=0;birth=true;}}
            p.ownerHitCheck=spin;p.timeLeft=2;body.heldProj=p.whoAmI;body.SetDummyItemTime(2);body.itemRotation=(p.Center-mounted).ToRotation();if(p.Center.X<mounted.X)body.itemRotation+=MathHelper.Pi;body.itemRotation=MathHelper.WrapAngle(body.itemRotation);return true;
        }
        internal static Projectile Birth(Projectile parent,Player body,float knockback)
        {var q=HostSuperStarAttack.Copy(ContentSamples.ProjectilesByType[405]);q.active=true;q.owner=parent.owner;q.Center=parent.Center;q.velocity=(parent.Center-body.Center).SafeNormalize(Vector2.Zero)*5.4f;q.damage=parent.damage;q.knockBack=knockback;q.ai[0]=-10;return q;}
        private static void Kill(Projectile p){p.active=false;p.timeLeft=0;}
        internal static bool Move(Projectile p,PredictionTerrain terrain)
        {
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater){bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;p.wet=wet;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(lava)p.lavaWet=true;if(!wet)p.honeyWet=p.shimmerWet=p.lavaWet=false;}
            // Native Shimmer has no AI069/070 branch. AI015 return2/4
            // exits before its otherwise rising flail impulse.
            if(p.shimmerWet && p.type==1058 && p.ai[0]!=2 && p.ai[0]!=4){if(p.velocity.Y>0)p.velocity.Y=-p.velocity.Y;p.velocity.Y=Math.Max(-8,p.velocity.Y-.4f);}
            var position=p.position;var prior=p.velocity;var velocity=prior;Vector2 wetVelocity=prior;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.type==405){p.localAI[0]++;if(p.localAI[0]<2){if(velocity.X!=prior.X)p.velocity.X=-prior.X;if(velocity.Y!=prior.Y)p.velocity.Y=-prior.Y;}else Kill(p);}
                else if(p.type==404)p.ai[0]=1;
                else
                {
                    float bounce=p.ai[0]==1 || p.ai[0]==5?.4f:p.ai[0]==6?0:.2f;if(velocity.X!=prior.X){p.velocity.X=-prior.X*bounce;p.localAI[0]++;}if(velocity.Y!=prior.Y){p.velocity.Y=-prior.Y*bounce;p.localAI[0]++;}
                    if(p.ai[0]==1){p.ai[0]=5;p.localNPCHitCooldown=10;p.position-=velocity;}if(p.ai[0]!=3 && p.ai[0]!=0 && p.ai[0]!=5 && p.ai[0]!=6 && p.localAI[0]>=10)p.ai[0]=4;if(p.wet)wetVelocity=p.velocity;
                }
            }
            p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;if(!p.manualDirectionChange)p.direction=p.velocity.X<0?-1:1;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileTargets targets,Inputs inputs,HostProjectileBirthSlots slots,Player body,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var row in rows)if(row.Shot.active && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height))return false;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member member=null;foreach(var m in rows)if(m.Shot.active && m.Shot.whoAmI>after && (member==null || m.Shot.whoAmI<member.Shot.whoAmI))member=m;if(member==null)return true;var p=member.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;slots.Release(p.whoAmI);break;}
                    bool born=false,random=false;float knockback=p.knockBack;if(p.type==405){if(!Bubble(p,targets,inputs,path,tick,terrain,out random))return false;member.Random|=random;}else{float old=p.ai[0];if(!Parent(p,body,aim,out born,out knockback))return false;if(p.type==1058 && old==0 && p.ai[0]==1)member.Receive.ResetLocalHitHistory();}
                    if(born)
                    {int slot;if(slots.TryAllocate(out slot)){var q=Birth(p,body,knockback);q.whoAmI=slot;rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=member.Source,Random=true});}}
                    if(!Move(p,terrain))return false;if(!p.active){slots.Release(p.whoAmI);break;}int hits=member.Receive.SelectedHits;bool spin=p.type==1058 && p.ai[0]==0;
                    if(!(spin && p.localAI[1]<=12))
                    {
                        Func<MotionRect,bool> shape=null;if(spin)shape=b=>{var nearest=new Vector2(MathHelper.Clamp(body.MountedCenter.X,b.X,b.X+b.Width),MathHelper.Clamp(body.MountedCenter.Y,b.Y,b.Y+b.Height))-body.MountedCenter;nearest.Y/=.8f*(body.gravDir>0 && nearest.Y>0?.5f:1);return nearest.Length()<=55;};
                        Func<int,int,bool> onHit=(slot,left)=>{if(p.type==404)p.ai[0]=1;return true;};bool pass=p.type==1058?member.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,shape,(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true,onHit):member.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,shape,consumeSelected:true,onHit:onHit);p.penetrate=member.Receive.Remaining;
                        if(member.Source && member.Receive.SelectedHits>hits){var b=path[tick].ProjectileReceiveBounds;contact=AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,b.CenterX,b.CenterY,member.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass)Kill(p);
                    }
                    if(p.active && --p.timeLeft<=0)Kill(p);if(!p.active)slots.Release(p.whoAmI);
                }
            }
            return true;
        }
        private static AttackContact Replay(Player player,List<Member> rows,HostProjectileTargets targets,Inputs inputs,HostProjectileBirthSlots slots,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,HostAttackPlayerFrame frame,int offset=0,bool launchPlayerAdvanced=false)
        {
            var body=(Player)typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(player,null);var prefix=new HostAttackPlayerPrefix(frame,terrain,122);
            for(int i=0;i<Math.Min(120,path.Count-clock.FirstTick);i++){PredictionPlayerMotion motion;if(!prefix.TryRead(i+(clock.NextWorld && !launchPlayerAdvanced?1:0),out motion))return null;HostAttackPlayerFrame.ApplyPose(body,motion);AttackContact result;if(!Outer(rows,targets,inputs,slots,body,path,clock.FirstTick+i,offset+i+1,terrain,root,aim,out result))return null;if(result!=null)return result;}return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;var scene=Capture(player,root);var targets=root.CaptureChaseTargets(sources[0]);root.BeginCandidate();return Replay(player,Copy(scene,sources,root),targets,root.BubbleFlailInputs,root.CaptureBirthSlots().Copy(),path,clock,terrain,root,Main.MouseWorld,HostAttackPlayerFrame.Capture(player,path));}
        internal static AttackContact Held(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,out Vector2 aim,out HostAttackObstacles root)
        {
            aim=HostHeldAttack.DirectionPoint(player,path,clock.FirstTick);root=new HostAttackObstacles(player,source,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);var scene=Capture(player,root);var targets=root.CaptureChaseTargets(source);root.CaptureShotFacing(player);root.BeginCandidate();var contact=Replay(player,Copy(scene,new[]{source},root),targets,root.BubbleFlailInputs,root.CaptureBirthSlots().Copy(),path,clock,terrain,root,aim,frame);if(contact!=null)aim=new Vector2(contact.AimX,contact.AimY);return contact;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay,HostAttackPlayerFrame? preparedPlayer=null,bool launchPlayerAdvanced=false)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;var scene=Capture(player,root);var targets=root.CaptureChaseTargets(ContentSamples.ProjectilesByType[405]);root.CaptureShotFacing(player);var frame=preparedPlayer??HostAttackPlayerFrame.Capture(player,path);
            for(int seed=0;seed<9;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=new Vector2(b.CenterX,b.CenterY);root.BeginCandidate();var slots=root.CaptureBirthSlots().Copy();var rows=Copy(scene,null,root);bool available=true;var owner=Main.player[player.whoAmI];var waitBody=(Player)typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(owner,null);var waitPrefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(owner,path),terrain,delay+2);
                for(int wait=1;wait<=delay;wait++){PredictionPlayerMotion motion;if(!waitPrefix.TryRead(wait-(beforeNpc?1:0),out motion)){available=false;break;}HostAttackPlayerFrame.ApplyPose(waitBody,motion);AttackContact ignored;if(!Outer(rows,targets,root.BubbleFlailInputs,slots,waitBody,path,age-delay+wait,wait,terrain,root,aim,out ignored)){available=false;break;}}if(!available)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var q=HostSuperStarAttack.Copy(ContentSamples.ProjectilesByType[ammo.Projectile]);q.active=true;q.owner=player.whoAmI;q.whoAmI=slot;q.Center=origin;q.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);q.damage=player.GetWeaponDamage(player.HeldItem);q.knockBack=player.HeldItem.knockBack;rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=true});var clock=new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay);var result=Replay(player,rows,targets,root.BubbleFlailInputs,slots,path,clock,terrain,root,aim,frame,delay,launchPlayerAdvanced);if(result!=null)return result;
            }
            return null;
        }
    }
}
