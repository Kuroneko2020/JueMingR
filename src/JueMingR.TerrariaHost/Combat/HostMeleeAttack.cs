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
        internal static Player PlayerCopy(Player player){return (Player)copy.Invoke(player,null);}
        internal static float SpearOffset(Projectile shot,Player player){return (float)spearOffset.Invoke(shot,new object[]{player,player.itemAnimation,player.itemAnimationMax});}
        internal static int DerivedSpearChild(int parent){return parent==222?228:parent==730?732:parent==130?131:parent==342?343:0;}
        internal static bool KnownSpearParent(int type)
        {
            // AI019's ordinary parents share this sealed pose. 367 has its
            // dedicated first-AI shard family; 877-879 have a dedicated
            // channel/dummy clock. North Pole342/343/344 owns a dedicated
            // finite snowflake world. These cannot borrow this parent-only route.
            Projectile sample;return type!=342 && type!=367 && (type<877 || type>879) && ContentSamples.ProjectilesByType.TryGetValue(type,out sample) && sample.aiStyle==19;
        }
        internal static bool KnownWhipParent(int type)
        {return type!=1035 && type>=0 && type<ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[type];}
#if DEBUG
        internal static int SpearFlightPoolScans,SpearFlightMemberSteps;
#endif
        internal static bool Rapier(int type){return type==802 || type==842 || type>=938 && type<=945 || type==1127;}
        internal static bool RawCursor(int weaponType){return weaponType==4956 || weaponType==5669;}
        internal static bool Handles(Projectile sample){return HostMonkStaffAttack.Handles(sample.type) || HostArcSwingAttack.Handles(sample.type) || Rapier(sample.type) || sample.aiStyle==19 || ProjectileID.Sets.IsAWhip[sample.type] && sample.type!=1035 || HostBoomerangAttack.Handles(sample.type) || sample.type==611 || sample.type==933 || sample.type==1100;}
        internal static int AnimationDuration(Player player,Item item)
        {
            // ApplyItemAnimation multiplies by native speed factors. Faster
            // melee has a smaller factor; reciprocal duration is incorrect.
            if(item.melee && !ItemID.Sets.NoMeleeSpeedBonus[item.type])return Math.Max(1,(int)(item.useAnimation*player.meleeSpeed));
            if(item.summon && ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[item.type])return Math.Max(1,(int)(item.useAnimation*player.summonerWeaponSpeedBonus*player.whipUseTimeMultiplier));
            int extra=item.autoReuse && item.reuseDelay==0 && item.useTime<=item.useAnimation && item.shoot>0 && item.useStyle==5?1:0;
            return Math.Max(1,item.useAnimation+extra);
        }
        // SolvePrepared encodes this same complete native animation duration
        // in the private primary whip's ai[2]; extraUpdates divide back into
        // these owner/world actions. The extra boundary keeps the final phase
        // available without requesting a tail after the body has retired.
        internal static int WhipHandActions(Player player,Item item)
        {return (player.itemAnimation>0 && player.itemAnimationMax>0?player.itemAnimationMax:AnimationDuration(player,item))+1;}
        // Seed8 reads NPC action 1+8*4 even when the primary body is shorter.
        // Include all nine original full-range directions; a short body is not
        // permission to clamp that last direction to an earlier NPC point.
        internal static int WhipSearchRequired(Player player,Item item)
        {return Math.Max(33,WhipHandActions(player,item));}
        internal static int PreferredSpearRange(Player player,Item item,AttackAmmoSnapshot ammo,NPC target,int delay)
        {
            Projectile parent;if(ammo==null || !ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out parent) || parent.aiStyle!=19 || DerivedSpearChild(parent.type)==0)return 16;
            int maximum=player.itemAnimation>0 && player.itemAnimationMax>0?player.itemAnimationMax:AnimationDuration(player,item);
            int animation=player.itemAnimation>0?player.itemAnimation:maximum;
            // Exact native offset is only a scheduling hint. It writes this
            // private projectile, never the live owner/ContentSamples/RNG.
            var shadow=(Projectile)copy.Invoke(parent,null);
            float peak=(float)spearOffset.Invoke(shadow,new object[]{player,maximum/3,maximum});
            float distance=Vector2.Distance(player.MountedCenter,target.Center+target.netOffset);
            float parentReach=(peak+1)*ammo.Speed+(parent.width+target.width)*.5f;
            if(distance<=parentReach)return 16;
            int birth=parent.type==130 || parent.type==730?0:Math.Max(0,animation-maximum/3+1);
            float offset=(float)spearOffset.Invoke(shadow,new object[]{player,Math.Max(1,animation-birth),maximum});
            float remaining=Math.Max(0,distance-offset*ammo.Speed),travel=0,velocity=ammo.Speed*(parent.type==730?4:parent.type==342?2.4f:1);
            int child=DerivedSpearChild(parent.type),steps=0;float targetSpeed=target.velocity.Length();
            // Match the existing child's discrete damping-before-movement.
            // Unknown/nonlinear motion keeps progressive search. This never
            // proves a miss or removes any exact direction/range/Basic input.
            if(child!=228 && child!=131 && child!=732)return 16;
            while(steps<120 && travel<remaining+targetSpeed*(delay+birth+steps))
            {velocity*=child==228?.96f:child==131?.8f:1;travel+=velocity;steps++;}
            return Math.Min(120,Math.Max(16,delay+birth+steps+4));
        }
        internal static AttackContact Solve(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int delay=0,HostAttackObstacles obstacles=null)
        {return SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample) || !Handles(sample))return null;
            if(HostNorthPoleAttack.Handles(ammo.Projectile))return HostNorthPoleAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostMonkStaffAttack.Handles(ammo.Projectile))return HostMonkStaffAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostSolarEruptionAttack.Handles(ammo.Projectile))return HostSolarEruptionAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostSpearShardAttack.Handles(ammo.Projectile))return HostSpearShardAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostJoustingLanceAttack.Handles(ammo.Projectile))return HostJoustingLanceAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostWhipStarAttack.Handles(ammo.Projectile))return HostWhipStarAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            if(HostArcSwingAttack.Weapon(item.type))return HostArcSwingAttack.SolvePrepared(player,item,ammo,timeline,age,beforeNpc,terrain,delay,obstacles,frame);
            int retargetDamage=sample.type==866?player.GetWeaponDamage(item):0;
            if(sample.type==866 && retargetDamage<=0)return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            int animationMax=player.itemAnimation>0 && player.itemAnimationMax>0?player.itemAnimationMax:AnimationDuration(player,item);
            int animation=player.itemAnimation>0?player.itemAnimation:animationMax;
            bool whip=KnownWhipParent(sample.type);
            if(whip && ammo.Damage<=0)return null;
            if(whip && obstacles!=null && sample.usesOwnerMeleeHitCD){obstacles.CaptureArcOwner(player,item);if(player.itemAnimation==0)obstacles.ResetArcMeleeHistory();}
            var chase=(sample.type==182 || sample.type==866) && obstacles!=null?obstacles.CaptureChaseTargets(sample):null;
            Vector2 origin=player.RotatedRelativePoint(player.MountedCenter);
            var initialMotion=frame.Motion;
            // AI019 stops parent pose when the animation ends; detached
            // children never read owner motion after their final birth. Bound
            // storage by those actual owner actions, not the whole NPC tail.
            int ownerActions=Math.Min(120,timeline.Count-age-1);if(sample.aiStyle==19)ownerActions=Math.Min(ownerActions,animation);
            var ownerPrefix=new HostAttackPlayerPrefix(frame,terrain,ownerActions);
            var directions=new Vector2[9];int directionCount=0;
            // Nine future direction seeds share the one published NPC timeline.
            // Native pose helpers may write a projectile, so they receive only
            // an independent shallow copy with independent AI arrays. Players,
            // ContentSamples and actual projectile state are never mutated.
            for(int seed=0;seed<9;seed++)
            {
                CombatPredictionWork.Check();
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
                if(sample.type==1127)shadow.ai[1]=item.shootSpeed/ammo.Speed;
                shadow.oldPos=(Vector2[])sample.oldPos.Clone();shadow.oldRot=(float[])sample.oldRot.Clone();
                if(sample.type==866)shadow.damage=retargetDamage;
                if(whip)shadow.damage=ammo.Damage;
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
                var shape=new CombatShapeSample();var now=new List<Vector2>();var prior=new List<Vector2>();var spearBoxes=new List<Rectangle>();
                int updates=sample.extraUpdates+1,limit=Math.Min(120,timeline.Count-age-1);bool active=true;
                for(int step=0;step<limit && active;step++)
                {
                    CombatPredictionWork.Check();
                    if(sample.aiStyle==19 && animation-step<=0 || sample.type==611 && step*updates+1>=30)break;
                    if(!ownerPrefix.TryRead(step+(beforeNpc?0:1),out motion))break;
                    HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=animation-step-(whip && player.itemAnimation==0?1:0);
                    if(whip)WhipFrame(body);
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
                        else if(Rapier(sample.type))
                        {if(!RapierPose(shadow,body,k)){active=false;break;}}
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
                        if(whip && obstacles!=null)
                        {
                            if(shadow.damage<=0){active=false;break;}
                            WhipBoxes(shadow,body,now,prior,spearBoxes);int selectedHits=obstacles.SelectedHits;bool unknownHit=false;
                            bool pass=obstacles.PassArcOuter(k,tick,delay+step+1,shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height,
                                bounds=>spearBoxes.Exists(r=>r.Intersects(new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height))),
                                (bounds,noTile)=>OwnerAllows(body,shadow,bounds,noTile,terrain),0,true,(slot,remaining)=>{if(PlainWhipHit(shadow) || slot==timeline.Identity.Slot)return true;unknownHit=true;return false;});
                            if(obstacles.SelectedHits>selectedHits)
                            {
                                var selectedBounds=timeline[tick].ProjectileReceiveBounds;var targetBox=new Rectangle((int)selectedBounds.X,(int)selectedBounds.Y,(int)selectedBounds.Width,(int)selectedBounds.Height);var hit=spearBoxes.Find(r=>r.Intersects(targetBox));
                                return AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,hit.X+hit.Width*.5f,hit.Y+hit.Height*.5f,hit.Width,hit.Height,AttackConfidence.Conditional);
                            }
                            if(unknownHit || !pass){active=false;break;}
                            continue;
                        }
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
                        {SpearBoxes(shadow,body,spearBoxes);foreach(var spearBox in spearBoxes)shape.Rectangle(spearBox,0);}
                        else if(!ProjectileCollisionGeometry.TryCapture(shadow,box,0,shape))shape.Rectangle(box,0);
                        for(int i=0;i<shape.Count;i++)
                        {
                            var s=shape.Shapes[i];if(s.Condition!=0 || s.Kind!=0)continue;
                            var receiver=timeline[tick].ProjectileReceiveBounds;
                            if(s.HasBounds && !s.Bounds.Intersects(new Rectangle((int)receiver.X,(int)receiver.Y,(int)receiver.Width,(int)receiver.Height)))continue;
                            var confidence=sample.type==611 || sample.type==802 || sample.type==842 || sample.type==1127 || RawCursor(item.type)?AttackConfidence.Representative:AttackConfidence.Conditional;
                            var contact=s.Line?AttackIntercept.LineContact(timeline,tick,aim.X,aim.Y,s.A.X,s.A.Y,s.B.X,s.B.Y,s.Width,confidence):AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,(s.A.X+s.B.X)/2,(s.A.Y+s.B.Y)/2,s.B.X-s.A.X,s.B.Y-s.A.Y,confidence);
                            if(contact!=null)return contact;
                        }
                    }
                }
                if(sample.aiStyle==19)
                {
                    var derived=SpearDerived(player,sample,unit,aim,ammo.Speed,timeline,age,beforeNpc,terrain,animation,animationMax,delay,frame,ownerPrefix);
                    if(derived!=null)return derived;
                }
            }
            return null;
        }
        internal static bool RapierPose(Projectile shot,Player player,int subupdate)
        {
            // AI161 uses the same mounted pose for every member. The saber's
            // actual born ai1 changes BOTH lifetime and extension. Native
            // movement adds one velocity after AI resets to (extension-1).
            int lifetime=shot.type==1127?(int)(16*shot.ai[1])+5:16;
            if(subupdate>=lifetime)return false;
            float extension=shot.type==1127?Math.Min(subupdate,(int)(lifetime*shot.ai[1])):subupdate;
            shot.ai[0]=subupdate;shot.rotation=shot.velocity.ToRotation()+MathHelper.PiOver2;
            if(shot.type==1127 && shot.velocity.X!=0)player.direction=shot.velocity.X>0?1:-1;
            shot.Center=player.RotatedRelativePoint(player.MountedCenter)+shot.velocity*extension;
            if(shot.type==1127)shot.Center+=shot.velocity.SafeNormalize(Vector2.UnitX)*10;
            return true;
        }
        internal static void SolarPose(Projectile shot,Player body,Vector2 unit,int subupdate)
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
                CombatPredictionWork.Check();
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
        internal sealed class SpearFlightState
        {
            private readonly Player owner;private readonly int animation,maximum,direction;
            private readonly Projectile[] sources;private readonly Vector2 position,velocity,mounted,rotationOrigin,arm;
            private readonly float rotation,gravity,meleeSpeed,whipRange;private readonly int width,height;
            private readonly bool left,right,jump,down,up,wet,honey,lava,shimmer,whip,sandStorm,floatInWater,floatMount37;
            private readonly float[] scales,distances;private readonly bool[] ownerChecks;
            internal SpearFlightState(Player player,Projectile[] members)
            {owner=player;sandStorm=player.sandStorm;floatInWater=player.ShouldFloatInWater;floatMount37=player.mount.Active && player.mount.Type==37;whip=KnownWhipParent(player.HeldItem.shoot);animation=player.itemAnimation;maximum=player.itemAnimationMax;direction=player.direction;arm=player.GetArmPosition();whipRange=player.whipRangeMultiplier;position=player.position;velocity=player.velocity;mounted=player.RotatedRelativePoint(player.MountedCenter);rotationOrigin=player.fullRotationOrigin;rotation=player.fullRotation;gravity=player.gravDir;meleeSpeed=player.meleeSpeed;width=player.width;height=player.height;left=player.controlLeft;right=player.controlRight;jump=player.controlJump;down=player.controlDown;up=player.controlUp;wet=player.wet;honey=player.honeyWet;lava=player.lavaWet;shimmer=player.shimmerWet;sources=members;scales=new float[members.Length];distances=new float[members.Length];ownerChecks=new bool[members.Length];for(int i=0;i<members.Length;i++){scales[i]=members[i].scale;distances[i]=members[i].ownerHitCheckDistance;ownerChecks[i]=members[i].ownerHitCheck;}}
            internal bool Current
            {get{
                // Presentation validates only this captured scalar/member
                // lease. ReadPlayer's floating premise queries terrain and
                // belongs exclusively to preparation, never to Draw.
                if(owner.itemAnimation!=animation || owner.itemAnimationMax!=maximum || owner.direction!=direction || owner.position!=position || owner.velocity!=velocity || owner.RotatedRelativePoint(owner.MountedCenter)!=mounted || whip && (owner.GetArmPosition()!=arm || owner.whipRangeMultiplier!=whipRange || owner.sandStorm!=sandStorm) || owner.fullRotationOrigin!=rotationOrigin || owner.fullRotation!=rotation || owner.gravDir!=gravity || owner.meleeSpeed!=meleeSpeed || owner.width!=width || owner.height!=height || owner.controlLeft!=left || owner.controlRight!=right || owner.controlJump!=jump || owner.controlDown!=down || owner.controlUp!=up || owner.wet!=wet || owner.honeyWet!=honey || owner.lavaWet!=lava || owner.shimmerWet!=shimmer || owner.ShouldFloatInWater!=floatInWater || (owner.mount.Active && owner.mount.Type==37)!=floatMount37)return false;
                for(int i=0;i<sources.Length;i++)if(sources[i].scale!=scales[i] || sources[i].ownerHitCheckDistance!=distances[i] || sources[i].ownerHitCheck!=ownerChecks[i])return false;return true;}}
        }
        private sealed class KnownSpearMember
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal int Steps,BirthOuter;}
        private static Projectile SpearCopy(Projectile source)
        {var p=(Projectile)copy.Invoke(source,null);p.ai=(float[])source.ai.Clone();p.localAI=(float[])source.localAI.Clone();p.localNPCImmunity=(int[])source.localNPCImmunity.Clone();return p;}
        private static void WhipFrame(Player body)
        {
            if(body.itemAnimation>0 && body.HeldItem.useStyle==1 && !body.sandStorm)
                body.bodyFrame.Y=body.bodyFrame.Height*(body.itemAnimation<body.itemAnimationMax*.333?3:body.itemAnimation<body.itemAnimationMax*.666?2:1);
        }
        private static void WhipBoxes(Projectile shot,Player body,List<Vector2> now,List<Vector2> prior,List<Rectangle> boxes)
        {
            now.Clear();prior.Clear();boxes.Clear();Projectile.FillWhipControlPoints(shot,now,body,true,0);Projectile.FillWhipControlPoints(shot,prior,body,true,-1);
            for(int i=0;i<Math.Min(now.Count,prior.Count);i++){var a=shot.Hitbox;var b=a;a.X=(int)now[i].X-a.Width/2;a.Y=(int)now[i].Y-a.Height/2;b.X=(int)prior[i].X-b.Width/2;b.Y=(int)prior[i].Y-b.Height/2;boxes.Add(Rectangle.Union(a,b));}
        }
        internal static void SpearBoxes(Projectile shot,Player body,List<Rectangle> boxes)
        {
            boxes.Clear();boxes.Add(shot.Hitbox);Rectangle extension;
            if(!shot.AI_019_Spears_GetExtensionHitbox(body,out extension))return;
            // Native AI019 collision tiles the bridge to the extension for
            // 46/105/153. Both an observed body and a hypothetical hand use
            // the explicit private owner, never Main.player's future pose.
            var to=extension.Center.ToVector2();float length=Vector2.Distance(to,shot.Center),spacing=Math.Max(12,Math.Max(extension.Width,extension.Height));
            for(float offset=spacing;offset<length;offset+=spacing)boxes.Add(Utils.CenteredRectangle(Vector2.Lerp(shot.Center,to,offset/length),new Vector2(extension.Width,extension.Height)));
            boxes.Add(extension);
        }
        private static bool PlainWhipHit(Projectile shot)
        {
            // This sealed body's receive needs only these OnHit branches'
            // damage continuation. Ordinary tags and owner speed buffs for
            // later hands remain native responsibilities. Other whips can
            // spawn an independent attack or activate a proc: their later
            // receive is not proved by replaying the captured primary body.
            double factor;
            switch(shot.type)
            {
                case 841:factor=.5;break;
                case 1104:factor=.4;break;
                case 1029:case 1030:case 914:factor=.6;break;
                case 952:case 849:factor=.9;break;
                case 848:factor=.95;break;
                case 915:factor=.8;break;
                default:return false;
            }
            shot.damage=(int)(shot.damage*factor);return true;
        }
        // Existing sources retain native aim, AI clocks and ordered immunity.
        // This is observation only: private copies never invoke live AI,
        // borrow the cursor, spawn into Main.projectile or consume RNG.
        internal static AttackContact KnownSpearFamily(Player player,Projectile[] sources,int parentType,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,out SpearFlightState state)
        {
            state=new SpearFlightState(player,sources);int childType=DerivedSpearChild(parentType);
            bool whip=KnownWhipParent(parentType);
            if(sources.Length==0 || !KnownSpearParent(parentType) && !whip || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            root.CaptureOwnedProjectileFamily(sources,player.whoAmI,childType==0?new[]{parentType}:new[]{parentType,childType});var slots=root.CaptureBirthSlots().Copy();root.BeginCandidate();
            var rows=new List<KnownSpearMember>(sources.Length+7);
            foreach(var source in sources){if(source.usesOwnerMeleeHitCD)root.CaptureArcOwner(player,player.HeldItem);var p=SpearCopy(source);rows.Add(new KnownSpearMember{Shot=p,Receive=root.ForkForSpawn(p)});}
            rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
            var body=(Player)copy.Invoke(player,null);var frame=HostAttackPlayerFrame.Capture(player,path);var motion=frame.Motion;
            var now=whip?new List<Vector2>():null;var prior=whip?new List<Vector2>():null;var bodyBoxes=new List<Rectangle>();
            Vector2 aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                CombatPredictionWork.Check();
                // A zero-damage ordinary whip has no later Damage action.
                // It still occupies its native slot until retirement, but
                // cannot justify more player/terrain receive continuation.
                if(!rows.Exists(member=>member.Shot.active && (!whip || member.Shot.damage>0)))break;
                // A hand/whip still reads the owner's future arm geometry.
                // Once it retires, its detached children use their sealed
                // origins and velocities, plus the captured owner/receive
                // lease; unrelated future player motion cannot veto them.
                bool handAlive=rows.Exists(member=>member.Shot.active && member.Shot.type==parentType);
                if(handAlive && clock.MovePlayer(action)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop)){
                    return null;}}
                if(handAlive)HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=Math.Max(0,player.itemAnimation-action-(clock.NextWorld?1:0));
                // Player.Update frames the active useStyle=1 hand after
                // ItemCheck and before projectile damage. Whip geometry
                // reads that arm anchor, so the captured first frame cannot
                // remain frozen through the middle/end of the native hand.
                if(whip && handAlive && clock.MovePlayer(action))WhipFrame(body);
                int tick=clock.FirstTick+action,outer=action+1;
                for(int memberIndex=0;memberIndex<rows.Count;memberIndex++)
                {
                    // Birth can use a lower released slot. A monotonic native
                    // loop cursor then defers that new member to next action.
                    // Sort once, walk once; only a genuine birth inserts or
                    // replaces its allocated slot, rather than searching the
                    // entire family for every visited member on every action.
                    var member=rows[memberIndex];var p=member.Shot;if(!p.active || whip && p.damage<=0)continue;
                    // A high-slot child born in this projectile pass has
                    // already missed this action's NPC/AI87 scan. A lower
                    // slot waits for the next action and is then scanned.
                    if(outer!=member.BirthOuter && !member.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height)){p.active=false;slots.Release(p.whoAmI);continue;}
                    for(int sub=0;sub<p.extraUpdates+1 && p.active;sub++)
                    {
                        // The previous NPC slot loop is complete before this
                        // gate. Do not end that loop at the reducing OnHit or
                        // falsely release the still living zero-damage body.
                        if(whip && p.damage<=0)break;
#if DEBUG
                        SpearFlightMemberSteps++;
#endif
                        Vector2 old=p.Center;
                        if(whip)
                        {
                            // Existing primary AI165 retains its real AI age
                            // and sealed velocity. >=1000 secondary bodies do
                            // not match the primary hand clock, but their own
                            // native duration field remains observable here.
                            p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2;p.ai[0]++;
                            float duration;int segments;float multiplier;Projectile.GetWhipSettings(p,out duration,out segments,out multiplier);
                            if(p.ai[0]>=duration){p.active=false;slots.Release(p.whoAmI);break;}
                            p.Center=body.GetArmPosition()+p.velocity*(p.ai[0]-1);p.spriteDirection=p.velocity.X<0?-1:1;
                        }
                        else if(p.type==parentType)
                        {
                            float offset=(float)spearOffset.Invoke(p,new object[]{body,body.itemAnimation,body.itemAnimationMax});p.Center+=p.velocity*offset;
                            bool late=body.itemAnimation<body.itemAnimationMax/3;
                            bool birth=childType!=0 && (parentType==130?p.localAI[0]<=24 && p.localAI[0]%4==0:parentType==730?p.localAI[0]==0:late && p.localAI[0]==0);
                            if(parentType==130)p.localAI[0]++;else if(birth)p.localAI[0]=1;
                            if(birth)
                            {
                                bool clear=true;PredictionStop stop;
                                if(parentType==342 && (!terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),new MotionRect(p.position.X,p.position.Y,p.width,p.height),out clear,out stop)))return null;
                                if(clear)
                                {
                                    int slot;if(!slots.TryAllocate(out slot))return null;var child=SpearCopy(ContentSamples.ProjectilesByType[childType]);child.active=true;child.owner=player.whoAmI;child.whoAmI=slot;child.Center=p.Center;child.velocity=p.velocity*(parentType==730?4:parentType==342?2.4f:1);child.damage=parentType==130?p.damage/3:parentType==730?(int)(p.damage*1.6):p.damage;
                                    var born=new KnownSpearMember{Shot=child,Receive=root.ForkForSpawn(child),BirthOuter=outer};
                                    int lo=0,hi=rows.Count;while(lo<hi){int mid=lo+(hi-lo)/2;if(rows[mid].Shot.whoAmI<slot)lo=mid+1;else hi=mid;}
                                    if(lo<rows.Count && rows[lo].Shot.whoAmI==slot)rows[lo]=born;
                                    else{rows.Insert(lo,born);if(lo<=memberIndex)memberIndex++;}
                                }
                            }
                            p.ai[0]++;if(p.ai[0]>=body.itemAnimationMax){p.active=false;slots.Release(p.whoAmI);break;}
                            p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2+MathHelper.PiOver4;p.spriteDirection=-body.direction;if(p.spriteDirection==-1)p.rotation-=MathHelper.PiOver2;
                        }
                        else if(p.type==228){p.velocity*=.96f;p.alpha+=2;if(p.alpha>200)p.active=false;}
                        else if(p.type==732){p.alpha+=12;if(p.alpha>255)p.active=false;}
                        else if(p.type==131){p.velocity*=.8f;p.alpha+=4;if(p.alpha>=255)p.active=false;}
                        else
                        {
                            // AI057's random snowflake is a separate role.
                            // Stop before its first unmodeled emission instead
                            // of manufacturing RNG or ignoring an immunity write.
                            int nextSpawn=p.localAI[1]>0?8+(int)p.localAI[1]:8;
                            if(p.localAI[0]+1>=nextSpawn)return null;
                            p.localAI[0]++;p.ai[0]++;if(p.ai[0]>30){p.ai[0]=30;p.velocity.Y=Math.Min(16,p.velocity.Y+.25f);p.velocity.X*=.995f;}p.alpha=Math.Max(0,p.alpha-50);
                        }
                        if(!p.active){slots.Release(p.whoAmI);break;}
                        if(!HostProjectileEnvironment.Dry(p,terrain,old.X,old.Y,p.velocity.X) || p.tileCollide && !terrain.ProjectilePassage(old.X,old.Y,p.Center.X+p.velocity.X,p.Center.Y+p.velocity.Y,p.width,p.height)){
                            return null;}
                        p.Center+=p.velocity;int k=++member.Steps;var box=p.Hitbox;
                        if(whip)
                        {
                            WhipBoxes(p,body,now,prior,bodyBoxes);
                        }
                        else if(p.type==parentType)SpearBoxes(p,body,bodyBoxes);
                        if(p.damage>0 && p.friendly && !p.hostile)
                        {
                            int priorHits=member.Receive.SelectedHits;
                            Func<MotionRect,bool> collision=b=>p.type==parentType?bodyBoxes.Exists(r=>r.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))):box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height));
                            bool unknownWhipHit=false;
                            Func<int,int,bool> onHit=whip?(Func<int,int,bool>)((slot,remaining)=>
                            {
                                if(PlainWhipHit(p) || slot==path.Identity.Slot)return true;
                                unknownWhipHit=true;return false;
                            }):null;
                            // Damage_PVE checks damage>0 once before its NPC
                            // loop, not after each OnHit. Reaching zero here
                            // still visits later slots in this same damage
                            // action; the next sub/action has no damage stage.
                            bool pass=p.type==parentType?member.Receive.PassArcOuter(k,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,collision,(b,noTile)=>OwnerAllows(body,p,b,noTile,terrain),0,true,onHit):member.Receive.PassCustomOuter(k,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,collision,consumeSelected:true);
                            p.penetrate=member.Receive.Remaining;
                            if(member.Receive.SelectedHits>priorHits)
                            {var b=path[tick].ProjectileReceiveBounds;var targetBox=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);
                                if(p.type==parentType)box=bodyBoxes.Find(r=>r.Intersects(targetBox));
                                // Rectangle.Center truncates odd dimensions.
                                // BodyContact independently reconstructs the
                                // integer top-left: preserve the half pixel
                                // so a genuine one-pixel first overlap stays
                                // exactly the same rectangle after receiving.
                                return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.X+box.Width*.5f,box.Y+box.Height*.5f,box.Width,box.Height,AttackConfidence.Conditional);}
                            if(unknownWhipHit)return null;
                            if(!pass){p.active=false;slots.Release(p.whoAmI);break;}
                        }
                        if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
                    }
                }
            }
            return null;
        }
        private sealed class SpearChild
        {
            internal Projectile Shot;internal HostAttackReceive Receive;internal int Steps;
        }
        private static AttackContact SpearDerived(Player player,Projectile parent,Vector2 unit,Vector2 aim,float speed,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int animation,int animationMax,int delay,HostAttackPlayerFrame frame,HostAttackPlayerPrefix ownerPrefix)
        {
            int childType=DerivedSpearChild(parent.type);
            if(childType==0)return null;
            var sample=ContentSamples.ProjectilesByType[childType];var children=new List<SpearChild>(parent.type==130?7:1);
            var body=(Player)copy.Invoke(player,null);var spear=(Projectile)copy.Invoke(parent,null);spear.velocity=unit*speed;
            var motion=frame.Motion;bool emitted=false;
            for(int step=0;step<Math.Min(120,timeline.Count-age-1);step++)
            {
                CombatPredictionWork.Check();
                // Only a possible future birth still depends on the owner's
                // geometry. Detached children retain their own dry/terrain
                // and immunity continuation after that final hand action.
                bool birthPossible=animation-step>0 && (parent.type==130?step<=24:parent.type==730?step==0:!emitted);
                if(birthPossible && !ownerPrefix.TryRead(step+(beforeNpc?0:1),out motion))return null;
                if(birthPossible)HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=animation-step;
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
                CombatPredictionWork.Check();
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
