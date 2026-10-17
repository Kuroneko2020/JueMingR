using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostDirectionalStaffAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==697 || type==707 || type==698;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;internal int Ready;}
        // Complete damaging state of AI140. Dust, sound, dummy timers and
        // player presentation writes are not replayed into the real owner.
        internal static Projectile Ai(Projectile p,Player player,Vector2 aim)
        {
            if(player.dead){p.active=false;return null;}int sign=Math.Sign(p.velocity.X);p.velocity=new Vector2(sign,0);
            if(p.ai[0]==0){p.rotation=new Vector2(sign,-player.gravDir).ToRotation()-MathHelper.PiOver4+MathHelper.Pi;if(sign<0)p.rotation-=MathHelper.PiOver2;}
            p.alpha=Math.Max(0,p.alpha-128);float increment=p.type==697?1+p.ai[0]/50*.5f:1;bool midpoint=p.ai[0]<25;p.ai[0]+=increment;p.rotation+=MathHelper.TwoPi*2/50*sign*increment;midpoint&=p.ai[0]>=25;
            if(p.ai[0]>=50 || midpoint && !player.controlUseItem)p.active=false;
            else if(midpoint)
            {int wanted=(aim-player.Center).X>0?1:-1;if(wanted!=p.velocity.X){p.velocity=new Vector2(wanted,0);p.rotation-=MathHelper.Pi;}}
            float angle=p.rotation-MathHelper.PiOver4*sign;var axis=(angle+(sign<0?MathHelper.Pi:0)).ToRotationVector2();Projectile born=null;
            if(p.type==697 && p.ai[0]>=47 && p.ai[0]-increment<47 && p.owner==Main.myPlayer)
            {
                born=new Projectile();born.SetDefaults(698);born.active=true;born.owner=p.owner;born.Center=p.Center+axis*30+new Vector2(sign*-15,-60);born.velocity=new Vector2(sign,0);born.damage=p.damage*3;born.knockBack=0;born.bonusCritChance=25;
            }
            p.Center=player.RotatedRelativePoint(player.MountedCenter)+axis*(p.ai[0]/50)*20;p.spriteDirection=p.direction;p.timeLeft=2;return born;
        }
        private static bool Collision(Projectile p,Player player,MotionRect b)
        {
            var target=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);
            if(p.type==698)
            {
                var first=p.Hitbox;first.Inflate(70,0);first.X+=(int)p.velocity.X*30;if(first.Intersects(target))return true;
                first.Y-=70;first.X+=(int)p.velocity.X*20;first.Inflate(-30,0);return first.Intersects(target);
            }
            var center=player.MountedCenter;var closest=target.ClosestPointInRect(center);return Vector2.Distance(closest,center)<=(p.type==697?73:110);
        }
        internal static AttackContact Solve(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,Vector2 aim,out HostAttackObstacles root)
        {
            root=new HostAttackObstacles(player,source,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);root.CaptureArcOwner(player,player.HeldItem);
            aim=HostDirectionalControl.Cursor(player,aim);
            root.CaptureHomingInputs(player,false,true);
            var scene=new List<Projectile>();
            foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))
            {
                // A separate unobserved staff can consume a different physical
                // cursor at its midpoint. Do not borrow this source's future
                // input for that external actor or claim its hit as ours.
                if(p.type!=698 && p.ai[0]<25 && !ReferenceEquals(p,source)){root=null;return null;}scene.Add(p);
            }
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{697,698,707});var pool=root.CaptureBirthSlots().Copy();root.BeginCandidate();
            var rows=new List<Member>();bool included=false;foreach(var p in scene){var copy=HostThornAttack.Copy(p);copy.ownerHitCheckDistance=p.ownerHitCheckDistance;rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=ReferenceEquals(p,source)});included|=ReferenceEquals(p,source);}
            if(!included){int slot;if(!pool.TryAllocate(out slot))return null;var copy=HostThornAttack.Copy(source);copy.ownerHitCheckDistance=source.ownerHitCheckDistance;copy.whoAmI=slot;rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=true});}
            var body=(Player)clone.Invoke(player,null);var motion=frame.Motion;
            for(int step=0;step<120 && clock.FirstTick+step<path.Count;step++)
            {
                CombatPredictionWork.Check();PredictionStop stop;if(clock.MovePlayer(step) && !PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return null;HostAttackPlayerFrame.ApplyPose(body,motion);
                rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));int tick=clock.FirstTick+step,outer=clock.LaunchDelay+step+1;
                // New children in a later native slot act this same pass. An
                // earlier free slot waits until the next pass; no whole-array
                // replay is started from inside the parent's partial action.
                int previousSlot=-1;
                while(true)
                {
                    Member m=null;foreach(var row in rows)if(row.Shot.active && row.Ready<=outer && row.Shot.whoAmI>previousSlot && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;if(m==null)break;var p=m.Shot;int slot=previousSlot=p.whoAmI;
                    if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                    if(p.type==698){p.ai[0]++;if(p.ai[0]>20)p.active=false;}
                    else
                    {
                        var born=Ai(p,body,aim);if(born!=null){int childSlot;if(!pool.TryAllocate(out childSlot))return null;born.whoAmI=childSlot;rows.Add(new Member{Shot=born,Receive=root.ForkForSpawn(born),Source=m.Source,Ready=childSlot>slot?outer:outer+1});}
                    }
                    if(!p.active){pool.Release(slot);continue;}float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return null;p.velocity.X+=wind;p.position+=p.velocity;
                    int before=m.Receive.SelectedHits;
                    bool passed=m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collision(p,body,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0);
                    if(m.Source && m.Receive.SelectedHits>before)return AttackIntercept.ProjectileShapeContact(path,tick,0,true,aim.X,aim.Y,p.Center.X,p.Center.Y,AttackConfidence.Conditional);
                    if(!passed){p.active=false;continue;}if(--p.timeLeft<=0 || m.Receive.Remaining==0){p.active=false;pool.Release(slot);}
                }
            }
            return null;
        }
    }
}
