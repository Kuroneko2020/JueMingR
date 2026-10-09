using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostWhipAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Weapon(Item item){return item!=null && item.shoot==1035;}
        internal static bool Primary(Projectile shot){return shot.type==1035 && shot.ai[2]<10;}
        internal static bool Pending(Projectile shot){return Primary(shot) && shot.ai[2]<3;}
        internal static bool Consumes(Projectile shot)
        {float duration;int segments;float range;Projectile.GetWhipSettings(shot,out duration,out segments,out range);float next=shot.ai[0]+1;return Pending(shot) && next<duration && shot.ai[2]<next/duration*3;}
        internal static bool TryPoint(Player player,Projectile primary,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point)
        {
            point=Vector2.Zero;var ammo=AttackAmmoSnapshot.Capture(player,player.HeldItem);if(ammo==null)return false;
            int delay=0;float speed=ammo.Speed;
            if(primary!=null)
            {
                float duration;int segments;float range;Projectile.GetWhipSettings(primary,out duration,out segments,out range);
                int until=Math.Max(1,(int)Math.Floor(primary.ai[2]*duration/3-primary.ai[0])+1);delay=(until-1)/primary.MaxUpdates;speed=primary.velocity.Length();
            }
            return PreviewInput(player,1035,11,speed,delay,timeline,clock,terrain,out point);
        }
        internal static bool TryExtraPoint(Player player,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point)
        {
            var ammo=AttackAmmoSnapshot.Capture(player,player.HeldItem);point=Vector2.Zero;if(ammo==null)return false;
            return PreviewInput(player,ammo.Projectile,ammo.Projectile==1035?11:1000+player.itemAnimationMax*2,ammo.Speed,0,timeline,clock,terrain,out point);
        }
        // The next child/extra is previewed with its actual native duration,
        // speed, range and current/previous whip rectangles. Nine directions
        // share the NPC timeline; no fixed lead ticks or game RNG are used.
        // Player pose, later scatter and world changes remain conditional: this
        // returns a representative input packet, deliberately no displayed box.
        private static bool PreviewInput(Player player,int type,float role,float speed,int delay,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point)
        {
            point=Vector2.Zero;Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(type,out sample) || clock.FirstTick+delay>=timeline.Count)return false;
            var shadow=(Projectile)copy.Invoke(sample,null);shadow.ai=(float[])sample.ai.Clone();shadow.localAI=(float[])sample.localAI.Clone();shadow.owner=player.whoAmI;shadow.ai[2]=role;
            float duration;int segments;float range;Projectile.GetWhipSettings(shadow,out duration,out segments,out range);int steps=Math.Min(120,(int)Math.Ceiling(duration/shadow.MaxUpdates));
            var receive=HostAttackReceive.Capture(player,shadow,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};var now=new List<Vector2>();var prior=new List<Vector2>();
            for(int seed=0;seed<9;seed++)
            {
                var target=timeline[Math.Min(timeline.Count-1,clock.FirstTick+delay+seed*(steps-1)/8)].ProjectileReceiveBounds;var aim=new Vector2(target.CenterX,target.CenterY);var unit=(aim-player.Center).SafeNormalize(Vector2.UnitX*player.direction);var packet=player.Center+unit*10000;
                var body=(Player)copy.Invoke(player,null);var motion=NpcPredictionSource.ReadPlayer(player);shadow.velocity=unit*speed;shadow.rotation=shadow.velocity.ToRotation()+MathHelper.PiOver2;shadow.spriteDirection=unit.X<0?-1:1;
                for(int step=0;step<delay+steps && clock.FirstTick+step<timeline.Count;step++)
                {
                    if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))break;}body.position=new Vector2(motion.X,motion.Y);if(step<delay)continue;
                    for(int sub=0;sub<shadow.MaxUpdates;sub++)
                    {
                        shadow.ai[0]=(step-delay)*shadow.MaxUpdates+sub+1;if(shadow.ai[0]>=duration)break;shadow.Center=body.GetArmPosition()+shadow.velocity*shadow.ai[0];int tick=clock.FirstTick+step;
                        if(!receive.Allows(step+1) || !HostMeleeAttack.OwnerAllows(body,shadow,timeline[tick].OwnerBounds,Main.npc[timeline.Identity.Slot].noTileCollide,terrain))continue;
                        if(HostMeleeAttack.WhipContact(body,shadow,now,prior,timeline,tick,sub,packet)!=null){point=packet;return true;}
                    }
                }
            }
            return false;
        }
    }
}
