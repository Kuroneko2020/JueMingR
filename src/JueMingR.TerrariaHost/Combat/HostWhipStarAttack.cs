using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Items;

namespace JueMingR.TerrariaHost.Combat
{
    // Constellation's own observed proc produces a bounded star family. This
    // does not predict another minion's future tag/proc or modify live tags.
    internal static class HostWhipStarAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo effects=typeof(TagEffectStack).GetField("_effectStates",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo activeEffects=typeof(TagEffectStack).GetField("_activeEffectCount",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo timers=typeof(TagEffectState).GetField("ProcTimeLeftOnNPC",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==1034;}
        internal static bool Family(int type){return type==1034 || type==1039;}
        internal sealed class State
        {
            private readonly Player player;private readonly TagEffectStack stack;private readonly int tag;private readonly int[] source;
            private readonly HostMeleeAttack.SpearFlightState pose;
            internal readonly int[] Remaining;
            internal State(Player player,Projectile[] sources,int tag)
            {this.player=player;stack=player.TagEffectStack;this.tag=tag;source=Read(stack,tag);Remaining=source==null?new int[Main.maxNPCs]:(int[])source.Clone();if(sources.Length>0)pose=new HostMeleeAttack.SpearFlightState(player,sources);}
            private static int[] Read(TagEffectStack stack,int tag)
            {var states=(TagEffectState[])effects.GetValue(stack);int count=(int)activeEffects.GetValue(stack);for(int i=0;i<count;i++)if(states[i]!=null && states[i].Type==tag)return (int[])timers.GetValue(states[i]);return null;}
            // Draw follows the captured family/owner scalars. The exact200
            // proc-clock comparison belongs to Prepare and real Consume.
            internal bool MembersCurrent {get{return ReferenceEquals(player.TagEffectStack,stack) && (pose==null || pose.Current);}}
            internal bool Current
            {get{if(!MembersCurrent || !ReferenceEquals(Read(stack,tag),source))return false;if(source!=null)for(int i=0;i<Remaining.Length;i++)if(source[i]!=Remaining[i])return false;return true;}}
        }
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private sealed class Candidate{internal bool ProcConsumed,Failed;}
        private static AttackContact Unknown(Candidate candidate){candidate.Failed=true;return null;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var shots=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))shots.Add(p);var scene=shots.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{1034,1039});return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){var q=HostThornAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=sources!=null && Array.IndexOf(sources,p)>=0});}return rows;}
        internal static bool Births(Projectile whip,Player body,PredictionTerrain terrain,int target,out Projectile[] children)
        {
            var points=new List<Vector2>();Projectile.FillWhipControlPoints(whip,points,body);var stars=new List<Projectile>();
            for(int i=2;i<points.Count-1;i+=3)
            {
                var birth=points[i];var velocity=(points[i+1]-points[i]).RotatedBy(-MathHelper.PiOver2*body.direction*body.gravDir).SafeNormalize(Vector2.Zero);
                var cell=birth.ToTileCoordinates();if(WorldGen.InWorld(cell,5))
                {PredictionTile tile;PredictionStop stop;if(!terrain.Tile(cell.X,cell.Y,out tile,out stop)){children=null;return false;}if(tile.Active && tile.Solid && !tile.SolidTop)birth=body.Center;}
                var star=new Projectile();star.SetDefaults(1039);star.active=true;star.owner=whip.owner;star.Center=birth;star.velocity=velocity;star.damage=(int)((whip.damage*2)*.22f);star.knockBack=whip.knockBack;star.tagEffectType=whip.tagEffectType;star.bonusTagDamage=whip.bonusTagDamage;star.armorPenetration=whip.armorPenetration;star.bonusCritChance=whip.bonusCritChance;star.ai[0]=target;star.ai[1]=.065f;stars.Add(star);
            }
            children=stars.ToArray();return true;
        }
        private static void WhipBoxes(Projectile whip,Player body,List<Vector2> now,List<Vector2> prior,List<Rectangle> boxes)
        {
            now.Clear();prior.Clear();boxes.Clear();Projectile.FillWhipControlPoints(whip,now,body,true,0);Projectile.FillWhipControlPoints(whip,prior,body,true,-1);
            for(int i=0;i<Math.Min(now.Count,prior.Count);i++){var a=whip.Hitbox;var b=a;a.X=(int)now[i].X-a.Width/2;a.Y=(int)now[i].Y-a.Height/2;b.X=(int)prior[i].X-b.Width/2;b.Y=(int)prior[i].Y-b.Height/2;boxes.Add(Rectangle.Union(a,b));}
        }
        private static AttackContact Replay(Player player,List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,State lease,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,HostAttackPlayerFrame frame,Vector2 aim,Candidate candidate,int maximumActions=120)
        {
            var body=(Player)clone.Invoke(player,null);var prefix=new HostAttackPlayerPrefix(frame,terrain,Math.Min(maximumActions,path.Count-clock.FirstTick)+1);var now=new List<Vector2>();var prior=new List<Vector2>();var boxes=new List<Rectangle>();
            for(int action=0;action<Math.Min(maximumActions,path.Count-clock.FirstTick);action++)
            {
                CombatPredictionWork.Check();int tick=clock.FirstTick+action,outer=clock.LaunchDelay+action+1;
                if(rows.Exists(m=>m.Shot.active && m.Shot.type==1034))
                {
                    PredictionPlayerMotion motion;if(!prefix.TryRead(action+(clock.NextWorld?1:0),out motion))return Unknown(candidate);HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=Math.Max(0,player.itemAnimation-action-(clock.NextWorld?1:0));
                    if(body.itemAnimation>0 && body.HeldItem.useStyle==1 && !body.sandStorm)body.bodyFrame.Y=body.bodyFrame.Height*(body.itemAnimation<body.itemAnimationMax*.333?3:body.itemAnimation<body.itemAnimationMax*.666?2:1);
                }
                foreach(var existing in rows)if(existing.Shot.active && !existing.Receive.BeforeNpc(tick,outer,existing.Shot.Center.X,existing.Shot.Center.Y,existing.Shot.width,existing.Shot.height))return Unknown(candidate);
                int after=-1;
                for(int visit=0;visit<1000;visit++)
                {
                    Member member=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (member==null || row.Shot.whoAmI<member.Shot.whoAmI))member=row;if(member==null)break;var p=member.Shot;after=p.whoAmI;
                    for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                    {
                        CombatPredictionWork.Check();bool whip=p.type==1034;
                        if(whip)
                        {
                            if(p.damage<=0)break;p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2;p.ai[0]++;float duration;int count;float range;Projectile.GetWhipSettings(p,out duration,out count,out range);if(p.ai[0]>=duration){p.active=false;slots.Release(p.whoAmI);break;}
                            p.Center=body.GetArmPosition()+p.velocity*(p.ai[0]-1);p.spriteDirection=p.velocity.X<0?-1:1;if(!HostProjectileEnvironment.Dry(p,terrain,p.Center.X,p.Center.Y,p.velocity.X))return Unknown(candidate);p.Center+=p.velocity;WhipBoxes(p,body,now,prior,boxes);
                        }
                        else if(!HostHomingEffectAttack.Step(p,targets,path,tick,terrain))return Unknown(candidate);
                        if(!p.active){slots.Release(p.whoAmI);break;}
                        int hits=member.Receive.SelectedHits;bool valid=true,representative=member.Representative;
                        Func<int,int,bool> onHit=(slot,remaining)=>
                        {
                            if(!whip){p.localNPCImmunity[slot]=p.localNPCHitCooldown;return HostHomingEffectAttack.StreakOnHit(p,targets,path,tick,terrain);}
                            p.damage=(int)(p.damage*.9);
                            if(p.localAI[0]!=0 || candidate.ProcConsumed || lease.Remaining[slot]<=action+clock.LaunchDelay+(clock.NextWorld?1:0))return true;
                            p.localAI[0]=1;candidate.ProcConsumed=true;Projectile[] children;if(!Births(p,body,terrain,slot,out children)){valid=false;return false;}
                            foreach(var existing in rows)existing.Representative=true;
                            foreach(var child in children)
                            {int born;if(!slots.TryAllocate(out born)){valid=false;return false;}child.whoAmI=born;rows.Add(new Member{Shot=child,Receive=root.ForkForSpawn(child),Source=member.Source,Representative=true});}
                            return true;
                        };
                        bool passed=whip?member.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>boxes.Exists(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true,onHit):member.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:onHit);p.penetrate=member.Receive.Remaining;
                        if(member.Source && member.Receive.SelectedHits>hits)
                        {
                            var b=path[tick].ProjectileReceiveBounds;return AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,b.CenterX,b.CenterY,representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                        }
                        // Strike and its immunity writes precede OnHit's
                        // optional births. A failed later descendant cannot
                        // erase this source's already established receive.
                        if(!valid)return Unknown(candidate);
                        if(!passed && p.penetrate!=0)return Unknown(candidate);if(!passed || --p.timeLeft<=0){p.active=false;slots.Release(p.whoAmI);}
                    }
                }
            }
            return null;
        }
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(ammo.Projectile!=1034 || root==null || age+1>=path.Count)return null;var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var sample=new Projectile();sample.SetDefaults(1034);var lease=root.CaptureWhipStarOwner(player,new Projectile[0],item.type);var targets=root.CaptureChaseTargets(sample);var origin=player.RotatedRelativePoint(player.MountedCenter);var directions=new List<Vector2>();int maximum=player.itemAnimation>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,item);
            var privatePlayer=(Player)clone.Invoke(player,null);if(privatePlayer.itemAnimation<=0){privatePlayer.itemAnimationMax=maximum;privatePlayer.itemAnimation=maximum;}
            for(int seed=0;seed<9;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;
                root.BeginCandidate();root.CaptureArcOwner(player,item);if(player.itemAnimation<=0)root.ResetArcMeleeHistory();var rows=Prepare(scene,null,root);var slots=pool.Copy();var candidate=new Candidate();
                if(delay>0){var actual=Main.player[player.whoAmI];Replay(actual,rows,slots,targets,lease,path,new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld),terrain,root,HostAttackPlayerFrame.Capture(actual,path),aim,candidate,delay);if(candidate.Failed)continue;}
                int slot;if(!slots.TryAllocate(out slot))return null;
                var p=new Projectile();p.SetDefaults(1034);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.tagEffectType=item.type;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.GetWeaponKnockback(item,item.knockBack);p.ai[2]=1000+maximum*(p.extraUpdates+1);rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true});
                privatePlayer.direction=HostBetsyFlameAttack.Facing(player,origin,aim);var clock=new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay);var contact=Replay(privatePlayer,rows,slots,targets,lease,path,clock,terrain,root,frame,aim,candidate);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=Capture(player,root);var sample=new Projectile();sample.SetDefaults(1034);var lease=root.CaptureWhipStarOwner(player,sources,sources[0].tagEffectType);var targets=root.CaptureChaseTargets(sample);root.CaptureArcOwner(player,player.HeldItem);root.BeginCandidate();
            return Replay(player,Prepare(scene,sources,root),root.CaptureBirthSlots().Copy(),targets,lease,path,clock,terrain,root,HostAttackPlayerFrame.Capture(player,path),sources[0].Center+sources[0].velocity*1000,new Candidate());
        }
    }
}
