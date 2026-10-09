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
    internal static class HostMeleeAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly MethodInfo spearOffset=typeof(Projectile).GetMethod("AI_019_Spears_GetSpearOffsetRelativeToPlayer",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(Projectile sample){return sample.aiStyle==19 || ProjectileID.Sets.IsAWhip[sample.type] || sample.type==13 || sample.type==19 || sample.type==33 || sample.type==52 || sample.type==106 || sample.type==611;}
        internal static AttackContact Solve(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample) || !Handles(sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            int animationMax=player.itemAnimationMax>0?player.itemAnimationMax:Math.Max(1,(int)(item.useAnimation/player.meleeSpeed));
            int animation=player.itemAnimation>0?player.itemAnimation:animationMax;
            // Nine future direction seeds share the one published NPC timeline.
            // Native pose helpers may write a projectile, so they receive only
            // an independent shallow copy with independent AI arrays. Players,
            // ContentSamples and actual projectile state are never mutated.
            for(int seed=0;seed<9;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;
                Vector2 origin=player.RotatedRelativePoint(player.MountedCenter),unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction),aim=origin+unit*10000;
                var shadow=(Projectile)copy.Invoke(sample,null);shadow.ai=(float[])sample.ai.Clone();shadow.localAI=(float[])sample.localAI.Clone();shadow.owner=player.whoAmI;shadow.velocity=unit*ammo.Speed;shadow.Center=origin;shadow.ai[0]=shadow.ai[1]=0;
                var body=(Player)copy.Invoke(player,null);body.itemAnimationMax=animationMax;
                // GetWhipSettings reads the real owner's animation maximum.
                // Encode the identical computed duration in this private
                // preview's existing duration field for a first-click model.
                if(ProjectileID.Sets.IsAWhip[sample.type])shadow.ai[2]=1000+animationMax*(sample.extraUpdates+1);
                var motion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
                var shape=new CombatShapeSample();var now=new List<Vector2>();var prior=new List<Vector2>();
                int updates=sample.extraUpdates+1,limit=Math.Min(120,timeline.Count-age-1);
                for(int step=0;step<limit;step++)
                {
                    if(!beforeNpc || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))break;}
                    body.position=player.position+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);body.itemAnimation=animation-step;
                    for(int sub=0;sub<updates;sub++)
                    {
                        int k=step*updates+sub+1;Vector2 old=shadow.Center;
                        if(sample.aiStyle==19)
                        {
                            if(body.itemAnimation<=0)break;
                            float offset=(float)spearOffset.Invoke(shadow,new object[]{body,body.itemAnimation,animationMax});shadow.position+=shadow.velocity*(offset+1);
                        }
                        else if(ProjectileID.Sets.IsAWhip[sample.type])
                        {
                            shadow.ai[0]=k;shadow.rotation=shadow.velocity.ToRotation()+MathHelper.PiOver2;shadow.spriteDirection=shadow.velocity.X<0?-1:1;
                            float duration;int segments;float multiplier;Projectile.GetWhipSettings(shadow,out duration,out segments,out multiplier);if(k>=duration)break;
                            shadow.Center=body.GetArmPosition()+shadow.velocity*k;
                        }
                        else if(sample.type==611)
                        {
                            if(k>=30)break;
                            // Birth random arc has no exact pre-shot value.
                            // Zero lateral arc is an explicit representative.
                            float sign=unit.X>=0?-1:1;var arc=(sign*((k-1)/30f*MathHelper.TwoPi-MathHelper.PiOver2)).ToRotationVector2();arc.Y=0;shadow.velocity+=48*arc.RotatedBy(unit.ToRotation());shadow.Center=body.GetArmPosition();
                        }
                        else
                        {
                            // Ordinary boomerang's outbound phase ends at its
                            // native return clock. Return/hit/wall effects stay
                            // native; no infinite straight-flight claim.
                            if(k>=(sample.type==106?45:30))break;shadow.Center+=shadow.velocity;
                        }
                        if(shadow.tileCollide && !terrain.ProjectilePassage(old.X,old.Y,shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height))break;
                        int tick=age+step+1;if(!receive.Allows(step+1) || !OwnerAllows(body,shadow,timeline[tick].Bounds,Main.npc[timeline.Identity.Slot].noTileCollide,terrain))continue;
                        var box=new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height);shape.Count=0;
                        if(ProjectileID.Sets.IsAWhip[sample.type])
                        {
                            now.Clear();prior.Clear();Projectile.FillWhipControlPoints(shadow,now,body,true,0);Projectile.FillWhipControlPoints(shadow,prior,body,true,-1);
                            for(int i=0;i<Math.Min(now.Count,prior.Count);i++){var a=box;var c=box;a.X=(int)now[i].X-a.Width/2;a.Y=(int)now[i].Y-a.Height/2;c.X=(int)prior[i].X-c.Width/2;c.Y=(int)prior[i].Y-c.Height/2;shape.Rectangle(Rectangle.Union(a,c),0);}
                        }
                        else if(sample.aiStyle==19)
                        {shape.Rectangle(box,0);Rectangle extension;if(shadow.AI_019_Spears_GetExtensionHitbox(body,out extension))shape.Rectangle(extension,0);}
                        else if(!ProjectileCollisionGeometry.TryCapture(shadow,box,0,shape))shape.Rectangle(box,0);
                        for(int i=0;i<shape.Count;i++)
                        {
                            var s=shape.Shapes[i];if(s.Condition!=0 || s.Kind!=0)continue;
                            var confidence=sample.type==611?AttackConfidence.Representative:AttackConfidence.Conditional;
                            var contact=s.Line?AttackIntercept.LineContact(timeline,tick,aim.X,aim.Y,s.A.X,s.A.Y,s.B.X,s.B.Y,s.Width,confidence):AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,(s.A.X+s.B.X)/2,(s.A.Y+s.B.Y)/2,s.B.X-s.A.X,s.B.Y-s.A.Y,confidence);
                            if(contact!=null)return contact;
                        }
                    }
                }
            }
            return null;
        }
        internal static AttackContact Starlight(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 aim)
        {
            int age=clock.Age;aim=HostHeldAttack.DirectionPoint(player,timeline,clock.FirstTick);
            var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var body=(Player)copy.Invoke(player,null);var shadow=(Projectile)copy.Invoke(shot,null);
            var motion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};var shape=new CombatShapeSample();
            // AI_075 resets this role to MountedCenter each update. Random
            // itemRotation is visual; its Damage shape uses velocity directly.
            // The native normal movement then adds that velocity once.
            for(int step=0;step<24 && age+step+1<timeline.Count;step++)
            {
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                body.position=new Vector2(motion.X,motion.Y);Vector2 mounted=body.RotatedRelativePoint(body.MountedCenter);
                shadow.velocity=(aim-mounted).SafeNormalize(Vector2.UnitX*body.direction)*(player.HeldItem.shoot==927?player.HeldItem.shootSpeed:1);shadow.scale=shot.ai[1];shadow.Center=mounted+shadow.velocity;
                int tick=clock.FirstTick+step;if(!receive.Allows(step+1) || !OwnerAllows(body,shadow,timeline[tick].Bounds,Main.npc[timeline.Identity.Slot].noTileCollide,terrain))continue;
                var box=new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height);shape.Count=0;ProjectileCollisionGeometry.TryCapture(shadow,box,0,shape);
                for(int i=0;i<shape.Count;i++){var s=shape.Shapes[i];var contact=AttackIntercept.BodyContact(timeline,tick,0,aim.X,aim.Y,(s.A.X+s.B.X)/2,(s.A.Y+s.B.Y)/2,s.B.X-s.A.X,s.B.Y-s.A.Y,AttackConfidence.Conditional);if(contact!=null)return contact;}
            }
            return null;
        }
        internal static bool OwnerAllows(Player player,Projectile shot,MotionRect target,bool noTileCollide,PredictionTerrain terrain)
        {
            if(!shot.ownerHitCheck || noTileCollide)return true;
            var center=new Vector2(target.CenterX,target.CenterY);if(Vector2.Distance(shot.Center,center)>shot.ownerHitCheckDistance)return false;
            bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(player.position.X,player.position.Y,player.width,player.height),target,out clear,out stop))return false;if(clear)return true;
            Vector2 upper=player.Center+new Vector2(player.direction*player.width/2,player.gravDir*-player.height/3f),side=player.Center+new Vector2(player.direction*player.width/2,0);
            return terrain.MeleeLine(upper.X,upper.Y,center.X,center.Y-(int)target.Height/3) || terrain.MeleeLine(upper.X,upper.Y,center.X,center.Y) || terrain.MeleeLine(side.X,side.Y,center.X,center.Y+(int)target.Height/3);
        }
    }
}
