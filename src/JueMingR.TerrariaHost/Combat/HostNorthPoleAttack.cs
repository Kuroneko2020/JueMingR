using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // North Pole keeps its existing native spear pose, then AI057 emits
    // finite AI001 snowflakes. Captured clocks are exact; an as-yet-undrawn
    // first emission delay/frame is a legal representative, never live RNG.
    internal static class HostNorthPoleAttack
    {
        internal static bool Handles(int type){return type==342;}
        internal static bool Family(int type){return type==342 || type==343 || type==344;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Random=random};}
        internal static bool ChildStep(Projectile p,System.Collections.Generic.IList<Projectile> scene,PredictionTerrain terrain,out bool emit,out bool representative)
        {
            emit=representative=false;if(HostRaySpellAttack.WorldRetired(p)){p.active=false;p.timeLeft=0;return true;}
            var wetVelocity=p.velocity;
            if(p.type==343)
            {
                p.ai[0]++;if(p.ai[0]>30){p.ai[0]=30;p.velocity.Y=Math.Min(16,p.velocity.Y+.25f);p.velocity.X*=.995f;}
                p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;p.alpha=Math.Max(0,p.alpha-50);
                if(p.owner==Main.myPlayer){if(p.localAI[1]==0){p.localAI[1]=3;representative=true;}p.localAI[0]++;if(p.localAI[0]>=8+Math.Max(0,(int)p.localAI[1])){p.localAI[0]=0;p.localAI[1]=-1;emit=true;}}
            }
            else if(p.type==344)
            {
                p.ai[0]++;
                int y=(int)(p.position.Y+p.velocity.Y)/16+1;
                foreach(int x in new[]{(int)p.position.X/16,(int)(p.position.X+p.width)/16})
                {
                    PredictionTile t;PredictionStop stop;if(!terrain.Tile(x,y,out t,out stop))return false;
                    if(t.Active && t.RawSolid && !t.SolidTop && !t.Half && t.Slope==0){p.active=false;p.timeLeft=0;return true;}
                }
                p.localAI[1]++;if(p.localAI[1]>5)p.alpha=Math.Max(0,p.alpha-50);p.frame=(int)p.ai[1];
                if(p.localAI[1]>=20){p.velocity.Y+=.15f;p.velocity.X*=.96f;}
                if(p.localAI[1]>=25)
                {
                    p.localAI[1]=20;float count=0,sum=0;
                    // Native repulsion includes other owners and follows the
                    // current ordered private scene, including same-pass births.
                    foreach(var other in scene)if(other.active && other.type==344 && other.whoAmI!=p.whoAmI){var delta=other.position-p.position;float distance=delta.Length();if(distance<=60){count++;sum+=-Math.Sign(delta.X)*Utils.Remap(distance,0,60,1,0);}}
                    if(count>0)p.velocity.X+=(sum==0?1:sum)*.25f;
                }
                if(p.velocity.Y>16)p.velocity.Y=16;
            }
            else return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            // Update retires an absent owner after AI and before movement.
            // That retired snowflake must not repel a later-slot live one.
            if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;return true;}
            if(!HostArcSwingAttack.Wet(p,terrain))return false;
            var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,nativeClip:true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.active=false;p.timeLeft=0;return true;}p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Outer(List<Member> rows,List<Projectile> bodies,HostProjectileBirthSlots slots,Player body,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;var boxes=new List<Rectangle>();for(int visited=0;visited<1000;visited++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool parent=p.type==342;
                    if(parent)
                    {
                        if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);break;}
                        p.direction=body.direction;p.spriteDirection=-p.direction;p.Center=body.RotatedRelativePoint(body.MountedCenter);float offset=HostMeleeAttack.SpearOffset(p,body);p.Center+=p.velocity*offset;
                        if(p.localAI[0]==0 && p.owner==Main.myPlayer && body.itemAnimation<body.itemAnimationMax/3)
                        {
                            p.localAI[0]=1;bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),new MotionRect(p.position.X,p.position.Y,p.width,p.height),out clear,out stop))return false;
                            int slot=-1;if(clear && !slots.TryAllocate(out slot))return false;
                            if(clear)
                            {
                                var child=HostNoteAttack.Copy(Terraria.ID.ContentSamples.ProjectilesByType[343]);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.Center=p.Center;child.velocity=p.velocity*2.4f;child.damage=p.damage;child.knockBack=p.knockBack*.85f;rows.Add(Make(child,root,m.Source,m.Random));bodies.Add(child);
                            }
                        }
                        p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2+MathHelper.PiOver4;if(p.spriteDirection==-1)p.rotation-=MathHelper.PiOver2;
                        p.ai[0]++;if(p.ai[0]>=body.itemAnimationMax){p.active=false;slots.Release(p.whoAmI);break;}
                        var old=p.Center;if(!HostProjectileEnvironment.Dry(p,terrain,old.X,old.Y,p.velocity.X))return false;p.Center+=p.velocity;HostMeleeAttack.SpearBoxes(p,body,boxes);
                    }
                    else
                    {
                        var spawn=p.Center;bool emit,random;if(!ChildStep(p,bodies,terrain,out emit,out random))return false;if(random)foreach(var row in rows)row.Random=true;
                        int slot=-1;if(emit && !slots.TryAllocate(out slot))return false;if(emit){var child=HostNoteAttack.Copy(Terraria.ID.ContentSamples.ProjectilesByType[344]);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.Center=spawn;child.velocity=Vector2.Zero;child.damage=(int)(p.damage*.7f);child.knockBack=p.knockBack*.55f;child.ai[1]=1;rows.Add(Make(child,root,m.Source,true));bodies.Add(child);}
                    }
                    if(!p.active){slots.Release(p.whoAmI);break;}
                    int hits=m.Receive.SelectedHits;bool pass;
                    if(parent)pass=m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>boxes.Exists(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true);
                    else pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);
                    p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits)
                    {
                        var box=p.Hitbox;if(parent){var b=path[tick].ProjectileReceiveBounds;box=boxes.Find(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)));}
                        contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return true;
                    }
                    if(!pass && p.penetrate!=0)return false;if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return false;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var rows=new List<Projectile>();var own=new List<Projectile>();var snow=new List<Projectile>();
            foreach(var p in Main.projectile)if(p.active && (p.owner==player.whoAmI && Family(p.type) || p.type==344)){rows.Add(p);if(p.owner==player.whoAmI)own.Add(p);if(p.type==344)snow.Add(p);}
            root.CaptureOwnedProjectileFamily(own.ToArray(),player.whoAmI,new[]{342,343,344});root.CaptureProjectileFamily(snow.ToArray(),344,344);return rows.ToArray();
        }
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root,NpcTrajectory path)
        {
            var rows=new List<Member>();var owners=new Dictionary<int,HostAttackObstacles>();
            foreach(var p in scene){var q=HostNoteAttack.Copy(p);var receive=root;if(p.owner!=Main.myPlayer){if(p.owner>=255)return null;if(!owners.TryGetValue(p.owner,out receive)){receive=new HostAttackObstacles(Main.player[p.owner],p,path,0,false,true);owners[p.owner]=receive;root.IncludePreparation(receive);} }rows.Add(Make(q,receive,sources!=null && Array.IndexOf(sources,p)>=0,false));}return rows;
        }
        private static bool Pose(Player body,HostAttackPlayerFrame frame,ref PredictionPlayerMotion motion,PredictionTerrain terrain,int step,bool advance)
        {if(advance){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return false;}HostAttackPlayerFrame.ApplyPose(body,motion);return true;}
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {return Prepare(player,item,ammo,path,new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay),terrain,frame).Contact;}
        internal static HostAttackSolution Prepare(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame)
        {return HostRepresentativeAttack.PrepareSpear(player,item,ammo,path,clock,terrain,frame,true);}
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;root.CaptureArcOwner(player,player.HeldItem);root.CaptureHomingInputs(player,false,true);var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,sources,root,path);if(rows==null)return null;var bodies=rows.ConvertAll(row=>row.Shot);var slots=root.CaptureBirthSlots().Copy();var body=HostMeleeAttack.PlayerCopy(player);var frame=HostAttackPlayerFrame.Capture(player,path);var motion=frame.Motion;var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){bool hand=rows.Exists(row=>row.Shot.active && row.Shot.type==342);if(hand && !Pose(body,frame,ref motion,terrain,step,clock.MovePlayer(step)))return null;body.itemAnimation=Math.Max(0,player.itemAnimation-step-(clock.NextWorld?1:0));AttackContact contact;if(!Outer(rows,bodies,slots,body,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
