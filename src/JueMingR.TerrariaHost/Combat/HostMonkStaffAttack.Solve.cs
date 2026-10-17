using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostMonkStaffAttack
    {
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count || ammo.Damage<=0)return null;root=root??new HostAttackObstacles(player,Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile],path,delay,beforeNpc,!beforeNpc);root.CaptureHomingInputs(player,false,true);root.CaptureShotFacing(player);var scene=Capture(player,root);root.CaptureMonkOwner(player,scene,false);var targets=root.CaptureMovingChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[699]);var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();var prefix=new HostAttackPlayerPrefix(frame,terrain,121);int maximum=player.itemAnimation>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,item),animation=player.itemAnimation>0?player.itemAnimation:maximum;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-player.MountedCenter).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=player.MountedCenter+unit*10000;root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();var body=HostMeleeAttack.PlayerCopy(player);bool valid=true;
                if(delay>0 && rows.Count>0)
                {
                    var live=Main.player[player.whoAmI];var prior=HostMeleeAttack.PlayerCopy(live);var oldPrefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(live,path),terrain,121);
                    for(int wait=1;wait<=delay;wait++){PredictionPlayerMotion motion;if(!oldPrefix.TryRead(wait-(beforeNpc?1:0),out motion)){valid=false;break;}HostAttackPlayerFrame.ApplyPose(prior,motion);prior.itemAnimation=Math.Max(0,live.itemAnimation-wait+(beforeNpc?1:0));AttackContact ignored;if(!Outer(prior,rows,slots,targets,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}
                }
                if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(ammo.Projectile);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.Center=player.RotatedRelativePoint(player.MountedCenter);shot.velocity=HostThornAttack.BirthVelocity(player,shot.Center,aim,ammo.Speed);shot.damage=ammo.Projectile==708?(int)(ammo.Damage*.5f):ammo.Damage;shot.ai[0]=ammo.Projectile==699?ammo.Speed*.375f*player.direction:(.3f+.7f*.5f)*(ammo.Speed/1.5f)*1.75f*player.direction;rows.Add(Make(shot,root,true,true));body.itemAnimationMax=maximum;
                for(int step=0;age+step+1<path.Count;step++)
                {
                    if(rows.Exists(row=>row.Shot.active && Handles(row.Shot.type))){PredictionPlayerMotion motion;if(!prefix.TryRead(step+(beforeNpc?0:1),out motion))break;HostAttackPlayerFrame.ApplyPose(body,motion);}body.itemAnimation=Math.Max(0,animation-step-(beforeNpc?0:1));AttackContact contact;if(!Outer(body,rows,slots,targets,path,age+step+1,delay+step+1,terrain,root,aim,out contact))break;if(contact!=null)return contact;
                }
            }return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;var scene=Capture(player,root);bool anchored=Array.Exists(scene,p=>Handles(p.type));if(anchored){root.CaptureHomingInputs(player,false,true);root.CaptureMonkOwner(player,scene,true);}var targets=root.CaptureMovingChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[699]);root.BeginCandidate();var rows=Copy(scene,source,root);var slots=root.CaptureBirthSlots().Copy();var body=HostMeleeAttack.PlayerCopy(player);var prefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++)
            {if(rows.Exists(row=>row.Shot.active && Handles(row.Shot.type))){PredictionPlayerMotion motion;if(!prefix.TryRead(step+(clock.NextWorld?1:0),out motion))return null;HostAttackPlayerFrame.ApplyPose(body,motion);}body.itemAnimation=Math.Max(0,player.itemAnimation-step-(clock.NextWorld?1:0));AttackContact contact;if(!Outer(body,rows,slots,targets,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
