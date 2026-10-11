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
        internal static bool RawCursor(int weaponType){return weaponType==4956 || weaponType==5669;}
        internal static bool Handles(Projectile sample){return HostArcSwingAttack.Handles(sample.type) || sample.type>=938 && sample.type<=945 || sample.aiStyle==19 || ProjectileID.Sets.IsAWhip[sample.type] && sample.type!=1035 || HostBoomerangAttack.Handles(sample.type) || sample.type==611 || sample.type==933 || sample.type==1100;}
        internal static int AnimationDuration(Player player,Item item)
        {
            // ApplyItemAnimation multiplies by native speed factors. Faster
            // melee has a smaller factor; reciprocal duration is incorrect.
            if(item.melee && !ItemID.Sets.NoMeleeSpeedBonus[item.type])return Math.Max(1,(int)(item.useAnimation*player.meleeSpeed));
            if(item.summon && ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[item.type])return Math.Max(1,(int)(item.useAnimation*player.summonerWeaponSpeedBonus*player.whipUseTimeMultiplier));
            int extra=item.autoReuse && item.reuseDelay==0 && item.useTime<=item.useAnimation && item.shoot>0 && item.useStyle==5?1:0;
            return Math.Max(1,item.useAnimation+extra);
        }
        internal static AttackContact Solve(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int delay=0,HostAttackObstacles obstacles=null)
        {return SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample) || !Handles(sample))return null;
            if(HostArcSwingAttack.Weapon(item.type))return HostArcSwingAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            int retargetDamage=sample.type==866?player.GetWeaponDamage(item):0;
            if(sample.type==866 && retargetDamage<=0)return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            int animationMax=player.itemAnimation>0 && player.itemAnimationMax>0?player.itemAnimationMax:AnimationDuration(player,item);
            int animation=player.itemAnimation>0?player.itemAnimation:animationMax;
            var chase=(sample.type==182 || sample.type==866) && obstacles!=null?obstacles.CaptureChaseTargets(sample):null;
            Vector2 origin=player.RotatedRelativePoint(player.MountedCenter);
            var initialMotion=frame.Motion;var environment=frame.Environment;
            var directions=new Vector2[9];int directionCount=0;
            // Nine future direction seeds share the one published NPC timeline.
            // Native pose helpers may write a projectile, so they receive only
            // an independent shallow copy with independent AI arrays. Players,
            // ContentSamples and actual projectile state are never mutated.
            for(int seed=0;seed<9;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;
                Vector2 unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction),aim=RawCursor(item.type)?new Vector2(b.CenterX,b.CenterY):origin+unit*10000;
                // Identical direction seeds (the common stationary receiver)
                // have identical native pose and terrain work. Raw-cursor
                // weapons encode distance too, so compare that full control.
                Vector2 key=RawCursor(item.type)?aim:unit;bool duplicate=false;
                for(int i=0;i<directionCount;i++)if(directions[i]==key){duplicate=true;break;}
                if(duplicate)continue;directions[directionCount++]=key;
                if(obstacles!=null)obstacles.BeginCandidate();
                var shadow=(Projectile)copy.Invoke(sample,null);shadow.ai=(float[])sample.ai.Clone();shadow.localAI=(float[])sample.localAI.Clone();shadow.owner=player.whoAmI;shadow.velocity=unit*ammo.Speed;shadow.Center=origin;shadow.ai[0]=shadow.ai[1]=0;
                shadow.oldPos=(Vector2[])sample.oldPos.Clone();shadow.oldRot=(float[])sample.oldRot.Clone();
                if(sample.type==866)shadow.damage=retargetDamage;
                int receiveTick=0,receiveOuter=0;Func<int,int,bool> retarget=null;
                // One bounded delegate per 866 direction candidate. OnHit
                // runs within the native slot order, after that slot's local
                // immunity write and before spending its penetration.
                if(sample.type==866 && obstacles!=null)retarget=(slot,penetration)=>HostBoomerangVariants.OnRetargetHit(shadow,slot,penetration,terrain,chase,timeline,receiveTick,obstacles,receiveOuter);
                if(RawCursor(item.type))shadow.velocity=(aim-player.MountedCenter)/2;
                if(sample.type==611)shadow.direction=shadow.velocity.X<0?-1:1;
                var body=(Player)copy.Invoke(player,null);body.itemAnimationMax=animationMax;
                // GetWhipSettings reads the real owner's animation maximum.
                // Encode the identical computed duration in this private
                // preview's existing duration field for a first-click model.
                if(ProjectileID.Sets.IsAWhip[sample.type])shadow.ai[2]=1000+animationMax*(sample.extraUpdates+1);
                var motion=initialMotion;
                var shape=new CombatShapeSample();var now=new List<Vector2>();var prior=new List<Vector2>();
                int updates=sample.extraUpdates+1,limit=Math.Min(120,timeline.Count-age-1);bool active=true;
                for(int step=0;step<limit && active;step++)
                {
                    if(sample.aiStyle==19 && animation-step<=0 || sample.type==611 && step*updates+1>=30)break;
                    if(!beforeNpc || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))break;}
                    HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=animation-step;
                    for(int sub=0;sub<updates && active;sub++)
                    {
                        int k=step*updates+sub+1;Vector2 old=shadow.Center;
                        if(sample.aiStyle==19)
                        {
                            if(body.itemAnimation<=0){active=false;break;}
                            // AI_019 resets to the predicted mounted centre
                            // on EVERY AI substep before applying the offset;
                            // normal native movement then adds velocity once.
                            float offset=(float)spearOffset.Invoke(shadow,new object[]{body,body.itemAnimation,animationMax});shadow.Center=body.RotatedRelativePoint(body.MountedCenter)+shadow.velocity*(offset+1);
                        }
                        else if(sample.type>=938 && sample.type<=945)
                        {
                            // AI_161 ordinary shortswords die before damage
                            // at substep16; native movement adds velocity once.
                            if(k>=16){active=false;break;}
                            shadow.rotation=shadow.velocity.ToRotation()+MathHelper.PiOver2;shadow.Center=body.RotatedRelativePoint(body.MountedCenter)+shadow.velocity*k;
                        }
                        else if(ProjectileID.Sets.IsAWhip[sample.type])
                        {
                            shadow.ai[0]=k;shadow.rotation=shadow.velocity.ToRotation()+MathHelper.PiOver2;shadow.spriteDirection=shadow.velocity.X<0?-1:1;
                            float duration;int segments;float multiplier;Projectile.GetWhipSettings(shadow,out duration,out segments,out multiplier);if(k>=duration){active=false;break;}
                            shadow.Center=body.GetArmPosition()+shadow.velocity*k;
                        }
                        else if(sample.type==611)
                        {
                            if(k>=30){active=false;break;}
                            // Birth random arc has no exact pre-shot value.
                            // Zero lateral arc is an explicit representative.
                            SolarPose(shadow,body,unit,k);
                        }
                        else if(RawCursor(item.type))
                        {
                            for(int j=shadow.oldPos.Length-1;j>0;j--){shadow.oldPos[j]=shadow.oldPos[j-1];shadow.oldRot[j]=shadow.oldRot[j-1];}
                            if(shadow.oldPos.Length>0){shadow.oldPos[0]=shadow.position;shadow.oldRot[0]=shadow.rotation;}
                            if(!FractalPose(shadow,body)){active=false;break;}
                        }
                        else
                        {
                            bool known=HostBoomerangVariants.Handles(shadow.type)?HostBoomerangVariants.Advance(shadow,body,terrain,chase,timeline,age+step+1,out active,obstacles,delay+step+1):HostBoomerangAttack.Advance(shadow,body,terrain,out active);
                            if(!known || !active){active=false;break;}
                        }
                        if(!HostBoomerangAttack.Handles(sample.type) && (!HostProjectileEnvironment.Dry(sample,terrain,old.X,old.Y,shadow.velocity.X) || shadow.tileCollide && !terrain.ProjectilePassage(old.X,old.Y,shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height))){step=limit;break;}
                        int tick=age+step+1;
                        if(shadow.type==866 && shadow.damage<=0)continue;
                        var box=HostBoomerangVariants.Handles(shadow.type)?HostBoomerangVariants.DamageBox(shadow):new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height);
                        if(obstacles!=null)
                        {
                            int priorHits=obstacles.OtherHits;
                            receiveTick=tick;receiveOuter=delay+step+1;
                            bool passes=HostBoomerangVariants.Handles(shadow.type)?obstacles.PassShapeOuter(k,tick,receiveOuter,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height,false,0,retarget):obstacles.PassOuter(k,tick,receiveOuter,shadow.Center.X,shadow.Center.Y);
                            if(!passes){if(shadow.type==866 && shadow.damage==0 && obstacles.Remaining==0)continue;active=false;break;}
                            // Consume only actual qualified receiver events,
                            // including common AI003's first-hit reversal.
                            // Geometry through immunity is not an OnHit.
                            HostBoomerangAttack.OnOtherHits(shadow,obstacles.OtherHits-priorHits);
                        }
                        if(!receive.Allows(step+delay+1) || !OwnerAllows(body,shadow,timeline[tick].OwnerBounds,timeline[tick].NoTileCollide,terrain))continue;
                        shape.Count=0;
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
                            var receiver=timeline[tick].ProjectileReceiveBounds;
                            if(s.HasBounds && !s.Bounds.Intersects(new Rectangle((int)receiver.X,(int)receiver.Y,(int)receiver.Width,(int)receiver.Height)))continue;
                            var confidence=sample.type==611 || RawCursor(item.type)?AttackConfidence.Representative:AttackConfidence.Conditional;
                            var contact=s.Line?AttackIntercept.LineContact(timeline,tick,aim.X,aim.Y,s.A.X,s.A.Y,s.B.X,s.B.Y,s.Width,confidence):AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,(s.A.X+s.B.X)/2,(s.A.Y+s.B.Y)/2,s.B.X-s.A.X,s.B.Y-s.A.Y,confidence);
                            if(contact!=null)return contact;
                        }
                    }
                }
                if(sample.aiStyle==19)
                {
                    var derived=SpearDerived(player,sample,unit,aim,ammo.Speed,timeline,age,beforeNpc,terrain,animation,animationMax,delay,frame);
                    if(derived!=null)return derived;
                }
            }
            return null;
        }
        private static void SolarPose(Projectile shot,Player body,Vector2 unit,int subupdate)
        {
            if(shot.localAI[0]==0)shot.localAI[0]=shot.velocity.ToRotation();
            float sign=shot.localAI[0].ToRotationVector2().X>=0?1:-1;if(shot.ai[1]<=0)sign*=-1;
            var arc=(sign*(shot.ai[0]/30f*MathHelper.TwoPi-MathHelper.PiOver2)).ToRotationVector2();arc.Y*=(float)Math.Sin(shot.ai[1]);if(shot.ai[1]<=0)arc.Y*=-1;
            shot.ai[0]++;shot.velocity+=48*arc.RotatedBy(shot.localAI[0]);
            body.direction=shot.direction;shot.rotation=shot.velocity.ToRotation()+MathHelper.PiOver2;shot.Center=body.GetArmPosition();
            // Native Update changes direction after movement. The following
            // AI_075 uses this value to turn the owner before its arm anchor.
            if(!shot.manualDirectionChange)shot.direction=shot.velocity.X<0?-1:1;
        }
        internal static AttackContact KnownSolar(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain)
        {
            if(shot.type!=611 || !shot.active || player.dead || player.mount.Active && player.mount.Type>=0 && MountID.Sets.DontHoldItems[player.mount.Type])return null;
            var shadow=(Projectile)copy.Invoke(shot,null);shadow.ai=(float[])shot.ai.Clone();shadow.localAI=(float[])shot.localAI.Clone();
            var body=(Player)copy.Invoke(player,null);var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var motion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};var shape=new CombatShapeSample();
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            // An existing SolarEruption has its real random arc and elapsed
            // AI clock. Replay only its finite remaining body; do not solve a
            // fresh shot or steer it from subsequent cursor input.
            for(int step=0;step<30 && clock.FirstTick+step<timeline.Count;step++)
            {
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);
                for(int sub=0;sub<shot.extraUpdates+1;sub++)
                {
                    if(shadow.ai[0]+1>=30)return null;
                    Vector2 old=shadow.Center;SolarPose(shadow,body,Vector2.Zero,0);
                    if(!HostProjectileEnvironment.Dry(shot,terrain,old.X,old.Y,shadow.velocity.X) || shadow.tileCollide && !terrain.ProjectilePassage(old.X,old.Y,shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height))return null;
                    int tick=clock.FirstTick+step;if(!receive.Allows(clock.LaunchDelay+step+1) || !OwnerAllows(body,shadow,timeline[tick].OwnerBounds,timeline[tick].NoTileCollide,terrain))continue;
                    var box=new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height);shape.Count=0;
                    if(!ProjectileCollisionGeometry.TryCapture(shadow,box,0,shape))shape.Rectangle(box,0);
                    var receiver=timeline[tick].ProjectileReceiveBounds;
                    for(int i=0;i<shape.Count;i++)
                    {
                        var s=shape.Shapes[i];if(s.Condition!=0 || s.Kind!=0 || s.HasBounds && !s.Bounds.Intersects(new Rectangle((int)receiver.X,(int)receiver.Y,(int)receiver.Width,(int)receiver.Height)))continue;
                        var contact=s.Line?AttackIntercept.LineContact(timeline,tick,receiver.CenterX,receiver.CenterY,s.A.X,s.A.Y,s.B.X,s.B.Y,s.Width):AttackIntercept.BodyContact(timeline,tick,sub,receiver.CenterX,receiver.CenterY,(s.A.X+s.B.X)/2,(s.A.Y+s.B.Y)/2,s.B.X-s.A.X,s.B.Y-s.A.Y,AttackConfidence.Conditional);
                        if(contact!=null)return contact;
                    }
                }
            }
            return null;
        }
        private sealed class SpearChild
        {
            internal Projectile Shot;internal HostAttackReceive Receive;internal int Steps;
        }
        private static AttackContact SpearDerived(Player player,Projectile parent,Vector2 unit,Vector2 aim,float speed,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int animation,int animationMax,int delay,HostAttackPlayerFrame frame)
        {
            int childType=parent.type==222?228:parent.type==730?732:parent.type==130?131:parent.type==342?343:0;
            if(childType==0)return null;
            var sample=ContentSamples.ProjectilesByType[childType];var children=new List<SpearChild>(parent.type==130?7:1);
            var body=(Player)copy.Invoke(player,null);var spear=(Projectile)copy.Invoke(parent,null);spear.velocity=unit*speed;
            var motion=frame.Motion;var environment=frame.Environment;bool emitted=false;
            for(int step=0;step<Math.Min(120,timeline.Count-age-1);step++)
            {
                if(!beforeNpc || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=animation-step;
                bool birth=parent.type==130?step<=24 && step%4==0:parent.type==730?step==0:!emitted && body.itemAnimation>0 && body.itemAnimation<animationMax/3;
                if(birth && body.itemAnimation>0)
                {
                    float offset=(float)spearOffset.Invoke(spear,new object[]{body,body.itemAnimation,animationMax});Vector2 center=spear.Center+spear.velocity*offset;
                    bool clear=true;PredictionStop stop;
                    if(parent.type!=342 || terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),new MotionRect(center.X-parent.width/2f,center.Y-parent.height/2f,parent.width,parent.height),out clear,out stop) && clear)
                    {
                        var shot=(Projectile)copy.Invoke(sample,null);shot.owner=player.whoAmI;shot.Center=center;shot.velocity=spear.velocity*(parent.type==730?4:parent.type==342?2.4f:1);
                        children.Add(new SpearChild{Shot=shot,Receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc)});
                    }
                    emitted=true;
                }
                for(int i=children.Count-1;i>=0;i--)
                {
                    var child=children[i];var shot=child.Shot;int k=++child.Steps;bool active=true;
                    // These are separate native AI_044/030/057 damage roles,
                    // with their own wall, lifetime and immunity rules. Future
                    // pool allocation can defer birth to the next world loop;
                    // the same-loop replay is explicitly representative.
                    if(childType==228){shot.velocity*=.96f;active=k<=100;}
                    else if(childType==732)active=k<=21;
                    else if(childType==131){shot.velocity*=.8f;active=k<=63;}
                    else if(k>30){shot.velocity.Y=Math.Min(16,shot.velocity.Y+.25f);shot.velocity.X*=.995f;}
                    active&=k<=sample.timeLeft;Vector2 old=shot.Center;
                    if(!active || !HostProjectileEnvironment.Dry(sample,terrain,old.X,old.Y,shot.velocity.X) || shot.tileCollide && !terrain.ProjectilePassage(old.X,old.Y,old.X+shot.velocity.X,old.Y+shot.velocity.Y,shot.width,shot.height)){children.RemoveAt(i);continue;}
                    shot.Center+=shot.velocity;if(!child.Receive.Allows(step+delay+1))continue;
                    var contact=AttackIntercept.BodyContact(timeline,age+step+1,0,aim.X,aim.Y,shot.Center.X,shot.Center.Y,shot.width,shot.height,AttackConfidence.Representative);
                    if(contact!=null)return contact;
                }
                if(emitted && children.Count==0 && (parent.type!=130 || step>=24))break;
            }
            return null;
        }
        // AI_182's finite pose with ai0=0 is a stated representative of its
        // random lateral arc. It reads neither native AI nor RNG and never
        // takes the raw-cursor distance from the weapon's shootSpeed.
        private static bool FractalPose(Projectile shot,Player player)
        {
            float closeness=Utils.GetLerpValue(900,0,shot.velocity.Length()*2,true);
            shot.localAI[0]+=MathHelper.Lerp(.7f,2,closeness);if(shot.localAI[0]>=120)return false;
            float progress=Utils.GetLerpValue(0,1,shot.localAI[0]/60,true),sign=shot.velocity.X>0?1:-1;
            float turn=MathHelper.Pi+sign*progress*MathHelper.TwoPi,direction=shot.velocity.ToRotation();
            float radius=Math.Max(60,shot.velocity.Length()+Utils.GetLerpValue(.5f,1,progress,true)*40);
            Vector2 ellipse=turn.ToRotationVector2()*new Vector2(radius,shot.ai[0]*MathHelper.Lerp(2,1,closeness));
            Vector2 entry=(1-Utils.GetLerpValue(0,.5f,progress,true))*new Vector2(sign*-radius*.1f,-shot.ai[0]*.3f);
            shot.rotation=turn+direction+MathHelper.PiOver2;shot.Center=player.MountedCenter+shot.velocity+ellipse.RotatedBy(direction)+entry;
            return true;
        }
        internal static AttackContact Starlight(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 aim)
        {return StarlightPrepared(player,shot,timeline,clock,terrain,out aim,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact StarlightPrepared(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 aim,HostAttackPlayerFrame frame)
        {
            int age=clock.Age;aim=HostHeldAttack.DirectionPoint(player,timeline,clock.FirstTick);
            var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var body=(Player)copy.Invoke(player,null);var shadow=(Projectile)copy.Invoke(shot,null);
            var motion=frame.Motion;var environment=frame.Environment;var shape=new CombatShapeSample();
            // AI_075 resets this role to MountedCenter each update. Random
            // itemRotation is visual; its Damage shape uses velocity directly.
            // The native normal movement then adds that velocity once.
            for(int step=0;step<24 && age+step+1<timeline.Count;step++)
            {
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);Vector2 mounted=body.RotatedRelativePoint(body.MountedCenter);
                shadow.velocity=(aim-mounted).SafeNormalize(Vector2.UnitX*body.direction)*(player.HeldItem.shoot==927?player.HeldItem.shootSpeed:1);shadow.scale=shot.ai[1];shadow.Center=mounted+shadow.velocity;
                int tick=clock.FirstTick+step;if(!receive.Allows(clock.LaunchDelay+step+1) || !OwnerAllows(body,shadow,timeline[tick].OwnerBounds,timeline[tick].NoTileCollide,terrain))continue;
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
        internal static AttackContact WhipContact(Player player,Projectile shot,List<Vector2> now,List<Vector2> prior,NpcTrajectory timeline,int tick,int sub,Vector2 aim)
        {
            now.Clear();prior.Clear();Projectile.FillWhipControlPoints(shot,now,player,true,0);Projectile.FillWhipControlPoints(shot,prior,player,true,-1);
            for(int i=0;i<Math.Min(now.Count,prior.Count);i++)
            {
                var a=new Rectangle((int)now[i].X-shot.width/2,(int)now[i].Y-shot.height/2,shot.width,shot.height);var b=new Rectangle((int)prior[i].X-shot.width/2,(int)prior[i].Y-shot.height/2,shot.width,shot.height);var box=Rectangle.Union(a,b);
                var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,box.X+box.Width/2f,box.Y+box.Height/2f,box.Width,box.Height,AttackConfidence.Representative);if(contact!=null)return contact;
            }
            return null;
        }
    }
}
