using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // One proposed main channel, never a copy of the ambient projectile
    // world. Reachable means this representative crossed the selected future
    // receive shape; it is deliberately not a future Damage/OnHit proof.
    internal static class HostRepresentativeAttack
    {
        internal static bool TryMotion(AttackAmmoSnapshot ammo,out AttackMotion motion)
        {return TryMotion(ammo.Projectile,ammo.Projectile==710 && ammo.WeaponType==3859?ammo.Speed*.8f-.25f:ammo.Speed,ammo.WeaponType,ammo.ExtraArrowUpdates,out motion);}
        private static bool TryMotion(int type,float speed,int weapon,bool quiver,out AttackMotion motion)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(type,out sample)){motion=default(AttackMotion);return false;}
            if(!HostAttackModels.TryResolved(type,speed,weapon,out motion) && !HostSpecialArrowAttack.TrySeedMotion(type,speed,weapon,out motion))
            {
                if(type!=932 && type!=706 && type!=661 && type!=36 && type!=207){motion=default(AttackMotion);return false;}
                motion=new AttackMotion(speed,0,0,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,AttackConfidence.Representative,componentBirthLimit:sample.aiStyle==1,maxFallSpeed:sample.aiStyle==1 || type==706?16:0);
            }
            if(quiver && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
            if(type==485 && weapon==3019)motion=new AttackMotion(speed,0,0,motion.Updates,motion.Width,motion.Height,motion.Lifetime,AttackConfidence.Representative,componentBirthLimit:true,gravityClockUsesLaunchX:true,firstUpdates:motion.FirstUpdates);
            return true;
        }
        internal static HostAttackSolution Prepare(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain)
        {
            return Prepare(player,ammo,path,origin,clock,terrain,aim=>
            {
                var p=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[ammo.Projectile]);p.active=true;p.owner=player.whoAmI;p.Center=origin;
                p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.velocity=HostSpecialArrowAttack.BirthVelocity(ammo,p.velocity,player.direction);p.damage=ammo.Damage;p.knockBack=ammo.NativeKnockBack;
                if(ammo.WeaponType==3019){p.ai[0]=p.velocity.X;p.ai[1]=p.velocity.Y;}
                if(p.aiStyle==1)while(p.velocity.X>=16 || p.velocity.X<=-16 || p.velocity.Y>=16 || p.velocity.Y< -16)p.velocity*=.97f;
                return p;
            });
        }
        internal static HostAttackSolution Prepare(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,Func<Vector2,Projectile> birth,int fireDelay=0)
        {
            // Ordinary snapshots do not populate Damage for every ammo role.
            // Current target/input qualification belongs to the consumer; a
            // zero optional damage field must not disable its motion solution.
            if(ammo==null || path==null || clock.FirstTick+fireDelay<0 || clock.FirstTick+fireDelay>=path.Count)return default(HostAttackSolution);
            var b=path[clock.FirstTick+fireDelay].ProjectileReceiveBounds;var point=new Vector2(b.CenterX,b.CenterY);var prototype=birth(point);AttackMotion motion;
            float speed=prototype!=null && prototype.type==706?16:prototype!=null && prototype.type==932?ammo.Speed*2:prototype!=null && prototype.type==710 && ammo.WeaponType==3859?ammo.Speed*.8f-.25f:ammo.Speed;
            if(prototype==null || !TryMotion(prototype.type,speed,ammo.WeaponType,ammo.ExtraArrowUpdates,out motion))return default(HostAttackSolution);
            return PrepareMotion(player,path,origin,clock,terrain,motion,birth,fireDelay);
        }
        internal static HostAttackSolution PrepareMotion(Player player,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,AttackMotion motion,Func<Vector2,Projectile> birth,int fireDelay=0)
        {
            int first=clock.FirstTick+fireDelay;if(path==null || first<0 || first>=path.Count)return default(HostAttackSolution);
            var b=path[first].ProjectileReceiveBounds;var direct=new Vector2(b.CenterX,b.CenterY);var probe=birth(direct);
            var proposals=probe!=null && (probe.type==710 || probe.type==485 && motion.GravityClockUsesLaunchX)?SpecialDirections(origin,motion,path,first,probe.type):HostMotionAttackInputs.Generate(origin,motion,path,first-1);
            var inputs=new List<Vector2>(proposals.Length+1);
            // Held/fan births can rotate their main axis. Undo the measured
            // legal representative rotation before handing the point to the
            // real cursor consumer; ordinary direction births have zero turn.
            float rotation=probe==null?0:MathHelper.WrapAngle(probe.velocity.ToRotation()-(direct-origin).ToRotation());
            foreach(var direction in proposals)inputs.Add(origin+direction.RotatedBy(-rotation)*10000);
            if(inputs.Count==0)inputs.Add(direct);
            var targets=probe!=null && HostAmmoSteeringAttack.Handles(probe.type)?HostProjectileTargets.Capture(probe,path.Identity.Session):null;
            Vector2 best=inputs[0];float bestError=float.MaxValue;bool blocked=false,unknown=false;
            for(int i=0;i<inputs.Count;i++)
            {
                CombatPredictionWork.Check();var aim=inputs[i];var p=birth(aim);if(p==null){unknown=true;continue;}
                // Displaced muzzles are still controlled by the same input.
                // One bounded correction inverses from the REAL birth point,
                // rather than paying for an entire angular/world replay.
                if(i<proposals.Length && Vector2.DistanceSquared(p.Center,origin)>1)
                {
                    var adjusted=HostMotionAttackInputs.Generate(p.Center,motion.WithSpeed(p.velocity.Length()),path,first-1);
                    if(adjusted.Length>0)
                    {
                        var actual=p.velocity.SafeNormalize(Vector2.UnitX);int closest=0;float dot=-2;
                        for(int n=0;n<adjusted.Length;n++){float value=Vector2.Dot(actual,adjusted[n]);if(value>dot){dot=value;closest=n;}}
                        aim=origin+adjusted[closest]*10000;p=birth(aim);if(p==null){unknown=true;continue;}
                    }
                }
                float error;int receive;var result=Replay(player,p,path,first,terrain,motion,out receive,out error,targets);
                if(error<bestError){bestError=error;best=aim;}
                if(result==HostAttackReachability.Reachable)return new HostAttackSolution(path,aim,true,result,receive);
                blocked|=result==HostAttackReachability.Blocked;unknown|=result==HostAttackReachability.Unknown;
            }
            return new HostAttackSolution(path,best,proposals.Length>0,blocked && !unknown?HostAttackReachability.Blocked:HostAttackReachability.Unknown);
        }
        private sealed class SpecialProposal {internal Vector2 Direction;internal int Tick;internal float Error;}
        private static Vector2[] SpecialDirections(Vector2 origin,AttackMotion motion,NpcTrajectory path,int first,int type)
        {
            var rows=new List<SpecialProposal>();float speed=motion.Speed;int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(path.Count-first));
            do
            {
                for(int step=1;step<=limit;step++)
                {
                    if((step&15)==1)CombatPredictionWork.Check();int tick=first+motion.OuterAtSubupdate(step)-1;var sample=path[tick];if(!sample.CanReceive)continue;var b=sample.ProjectileReceiveBounds;float dx=b.CenterX-origin.X,dy=b.CenterY-origin.Y;
                    if(type==710)
                    {
                        double low=-speed,high=speed;
                        if(dy<PhoenixY(low,step) || dy>PhoenixY(high,step))continue;
                        for(int n=0;n<20;n++){double mid=(low+high)*.5;if(PhoenixY(mid,step)<dy)low=mid;else high=mid;}
                        double vy=(low+high)*.5,vx=Math.Sqrt(Math.Max(0,speed*speed-vy*vy))*(dx<0?-1:1);
                        int start=19+Math.Max(1,(int)Math.Floor(-vy/.2)+1),dragged=Math.Max(0,step-start+1);double response=dragged==0?step:start-1+.98*(1-Math.Pow(.98,dragged))/.02;
                        float error=(float)Math.Abs(vx*response-dx);if(error>(b.Width+motion.Width)*.5f)continue;
                        AddSpecial(rows,new Vector2((float)(vx/speed),(float)(vy/speed)),tick,error,motion,speed);
                    }
                    else
                    {
                        // Bat's 39/40 pull is toward the UNLIMITED launch AI,
                        // while its current velocity had component limiting.
                        // The >=15 launch-X gravity arm has only two classes.
                        double sum=.975*(1-Math.Pow(.975,step))/.025,response=motion.Speed*step+(speed-motion.Speed)*sum;
                        for(int gravity=0;gravity<2;gravity++)
                        {
                            double drop=gravity==0?0:.1*.975/.025*(step-sum),x=dx/response,y=(dy-drop)/response,length=Math.Sqrt(x*x+y*y);if(length<=0)continue;
                            var direction=new Vector2((float)(x/length),(float)(y/length));if((direction.X*motion.Speed>=15)!=(gravity==1))continue;
                            float error=(float)(Math.Abs(length-1)*response);if(error>(b.Width+b.Height+motion.Width+motion.Height)*.5f)continue;AddSpecial(rows,direction,tick,error,motion,speed);
                        }
                    }
                }
                if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
            }while(true);
            rows.Sort((a,b)=>a.Error.CompareTo(b.Error));var result=new List<Vector2>(8);
            // Centre error is the primary ranking, with one late arc reserved
            // for a real obstruction rather than all early edge grazes.
            foreach(var row in rows){bool duplicate=false;foreach(var prior in result)if(Vector2.Dot(prior,row.Direction)>.9998f){duplicate=true;break;}if(!duplicate)result.Add(row.Direction);if(result.Count>=7)break;}
            SpecialProposal late=null;foreach(var row in rows)if(late==null || row.Tick>late.Tick)late=row;if(late!=null && !result.Contains(late.Direction))result.Add(late.Direction);
            return result.ToArray();
        }
        private static void AddSpecial(List<SpecialProposal> rows,Vector2 direction,int tick,float error,AttackMotion motion,float branchSpeed)
        {
            float vx=direction.X*motion.Speed,vy=direction.Y*motion.Speed;motion.Launch(ref vx,ref vy);float speed=(float)Math.Sqrt(vx*vx+vy*vy);
            // Keep the candidate's class consistent with its actual component
            // birth. Final replay still owns float rounding and native boxes.
            if(speed<=0 || Math.Abs(speed-branchSpeed)>.002f)return;rows.Add(new SpecialProposal{Direction=direction,Tick=tick,Error=error});
        }
        private static double PhoenixY(double velocity,int steps)
        {
            if(steps<20)return velocity*steps;int gravitySteps=steps-19,falling=Math.Min(gravitySteps,Math.Max(0,(int)Math.Floor((12-velocity)/.2)));
            return velocity*(19+falling)+.1*falling*(falling+1)+12*(gravitySteps-falling);
        }
        internal static HostAttackSolution PrepareSpear(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,bool northPole)
        {
            if(ammo==null || ammo.Damage<=0 || clock.FirstTick<0 || clock.FirstTick>=path.Count)return default(HostAttackSolution);
            int maximum=player.itemAnimation>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,item),animation=player.itemAnimation>0?player.itemAnimation:maximum;
            int phase=clock.NextWorld?1:0,emission=northPole?Math.Max(0,animation-phase-maximum/3+1):0;
            var prefix=new HostAttackPlayerPrefix(frame,terrain,Math.Min(120,maximum+phase+1));var origin=player.RotatedRelativePoint(player.MountedCenter);var selected=path[clock.FirstTick].ProjectileReceiveBounds;
            var direct=new Vector2(selected.CenterX,selected.CenterY);
            // The anchored body remains a real close-range channel. Test its
            // native boxes with the same pose clock before solving the distant
            // child; no snowflakes or shared future immunity are prerequisites.
            if(Vector2.Distance(origin,direct)<ammo.Speed*(maximum*2+3)+128)
            {
                var body=HostMeleeAttack.PlayerCopy(player);var parent=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[ammo.Projectile]);parent.active=true;parent.owner=player.whoAmI;parent.velocity=HostThornAttack.BirthVelocity(player,origin,direct,ammo.Speed);var boxes=new List<Rectangle>();
                for(int step=0;step<animation-phase && clock.FirstTick+step<path.Count;step++)
                {
                    PredictionPlayerMotion pose;if(!prefix.TryRead(step+phase,out pose))break;HostAttackPlayerFrame.ApplyPose(body,pose);body.itemAnimationMax=maximum;body.itemAnimation=Math.Max(0,animation-step-phase);body.direction=parent.velocity.X<0?-1:1;
                    parent.Center=body.RotatedRelativePoint(body.MountedCenter);float offset=HostMeleeAttack.SpearOffset(parent,body);parent.Center+=parent.velocity*(offset+1);HostMeleeAttack.SpearBoxes(parent,body,boxes);
                    int tick=clock.FirstTick+step;var b=path[tick].ProjectileReceiveBounds;var target=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);
                    if(path[tick].CanReceive && boxes.Exists(r=>r.Intersects(target)) && HostMeleeAttack.OwnerAllows(body,parent,b,false,terrain))return new HostAttackSolution(path,direct,true,HostAttackReachability.Reachable,tick);
                }
            }
            if(clock.FirstTick+emission>=path.Count)return new HostAttackSolution(path,direct,false,HostAttackReachability.Unknown);
            PredictionPlayerMotion launchMotion;if(!prefix.TryRead(emission+phase,out launchMotion))return new HostAttackSolution(path,direct,false,HostAttackReachability.Unknown);
            var launch=HostMeleeAttack.PlayerCopy(player);HostAttackPlayerFrame.ApplyPose(launch,launchMotion);launch.itemAnimationMax=maximum;launch.itemAnimation=Math.Max(0,animation-emission-phase);
            int type=northPole?343:1107;var sample=ContentSamples.ProjectilesByType[type];HostAttackSolution result=default(HostAttackSolution);
            // Obsidian multiplies X/Y by independent .7..1.3 draws, then
            // both by .8..1.2. The middle speed is the normal representative;
            // a single legal 1.2 component branch preserves its useful outer
            // range instead of declaring the whole weapon limited to that
            // middle draw's shorter ballistic range. Native RNG is untouched.
            for(int branch=0;branch<(northPole?1:2);branch++)
            {
                float multiplier=northPole?2.4f:branch==0?2:2.4f;
                var motion=new AttackMotion(ammo.Speed*multiplier,northPole?.25f:.2f,northPole?31:6,sample.extraUpdates+1,sample.width,sample.height,northPole?sample.timeLeft:90,AttackConfidence.Representative,acceleration:northPole?.995f:1,maxFallSpeed:16,accelerationStart:northPole?31:0,horizontalAccelerationOnly:northPole);
                var candidate=PrepareMotion(launch,path,origin,clock,terrain,motion,aim=>
                {
                var parent=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[ammo.Projectile]);parent.owner=player.whoAmI;parent.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);launch.direction=parent.velocity.X<0?-1:1;
                parent.Center=launch.RotatedRelativePoint(launch.MountedCenter);float offset=HostMeleeAttack.SpearOffset(parent,launch);parent.Center+=parent.velocity*offset;
                if(northPole)
                {
                    bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(launch.position.X,launch.position.Y,launch.width,launch.height),new MotionRect(parent.position.X,parent.position.Y,parent.width,parent.height),out clear,out stop) || !clear)return null;
                }
                var child=HostNoteAttack.Copy(sample);child.active=true;child.owner=player.whoAmI;child.Center=parent.Center;child.velocity=parent.velocity*multiplier;child.damage=northPole?ammo.Damage:(int)(ammo.Damage*.5f);if(!northPole)child.timeLeft=90;
                return child;
                },emission);
                if(candidate.Reachability==HostAttackReachability.Reachable)return candidate;
                if(branch==0 || !result.HasPoint)result=candidate;
            }
            return result;
        }
        internal static HostAttackReachability Replay(Player player,Projectile p,NpcTrajectory path,int first,PredictionTerrain terrain,AttackMotion motion,out int receive,out float nearest,HostProjectileTargets targets=null)
        {
            receive=-1;nearest=float.MaxValue;bool wet;byte kind;
            if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return HostAttackReachability.Unknown;
            p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
            var special=new HostSpecialArrowAttack.State(p);var phoenix=p.type==706?HostPhoenixAttack.Bind(p,player.setApprenticeT3):null;
            motion=motion.ForLaunch(p.velocity.X);
            int substep=0;
            for(int tick=first;tick<path.Count && p.active && substep<motion.Lifetime;tick++)
            {
                int updates=tick==first?(motion.FirstUpdates==0?motion.Updates:motion.FirstUpdates):Math.Max(motion.Updates,p.extraUpdates+1);
                for(int sub=0;sub<updates && p.active && substep<motion.Lifetime;sub++)
                {
                    CombatPredictionWork.Check();substep++;bool known=true,blocked=false,burst=false;
                    if(p.type==343){bool emit,representative;known=HostNorthPoleAttack.ChildStep(p,Array.Empty<Projectile>(),terrain,out emit,out representative);}
                    else if(p.type>=1107 && p.type<=1109)known=HostSpearShardAttack.ChildStep(p,terrain);
                    else if(p.type==932)known=HostHomingEffectAttack.Step(p,null,path,tick,terrain);
                    else if(phoenix!=null)known=HostPhoenixAttack.Step(phoenix,terrain);
                    else if(HostSpecialArrowAttack.Handles(p.type))
                    {known=HostSpecialArrowAttack.Step(p,terrain,ref special);p.Center=special.Center;p.velocity=special.Velocity;p.active=special.Alive;}
                    else
                    {
                        bool killed,skipped;
                        if(HostAmmoSteeringAttack.Handles(p.type))known=HostAmmoSteeringAttack.Step(p,targets,path,tick,terrain,out killed,out skipped);
                        else if(p.type==661)known=HostOnyxBlastAttack.Step(p,terrain,out killed,out skipped);
                        else if(HostArrowFamilyAttack.Family(p.type) || HostAmmoImpactAttack.Family(p.type))known=HostArrowFamilyAttack.Step(p,terrain,out killed,out skipped);
                        else known=HostArrowFamilyAttack.StepMotor(p,terrain,out killed,out skipped,q=>
                        {float x=0,y=0,vx=q.velocity.X,vy=q.velocity.Y;motion.Advance(ref x,ref y,ref vx,ref vy,substep);q.velocity=new Vector2(vx,vy);return 0;});
                        if(skipped)continue;
                        if(killed){blocked=true;burst=p.type==661?HostOnyxBlastAttack.PrepareKillBurst(p):HostAmmoImpactAttack.PrepareKillBurst(p);p.active=false;}
                    }
                    if(!known)return HostAttackReachability.Unknown;
                    var box=path[tick].ProjectileReceiveBounds;float dx=Math.Max(box.X-p.Center.X,Math.Max(0,p.Center.X-box.X-box.Width)),dy=Math.Max(box.Y-p.Center.Y,Math.Max(0,p.Center.Y-box.Y-box.Height));nearest=Math.Min(nearest,dx*dx+dy*dy);
                    bool colliderKnown=true;bool hitBox=p.type==932?HostHomingEffectAttack.EventideCollides(p,box):p.type==661?HostOnyxBlastAttack.Collides(p,box,terrain,out colliderKnown):p.Hitbox.Intersects(new Rectangle((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height));
                    if(!colliderKnown)return HostAttackReachability.Unknown;
                    if(path[tick].CanReceive && (p.active || burst || phoenix!=null && phoenix.Burst) && hitBox){receive=tick;return HostAttackReachability.Reachable;}
                    if(blocked || !p.active)return HostAttackReachability.Blocked;
                    if(phoenix!=null)HostPhoenixAttack.Finish(phoenix);else if(--p.timeLeft<=0)p.active=false;
                }
            }
            return HostAttackReachability.Unknown;
        }
    }
}
