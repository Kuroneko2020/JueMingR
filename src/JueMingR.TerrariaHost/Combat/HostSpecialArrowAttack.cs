using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI001 has three arrow motors that cannot be represented by its ordinary
    // advancing gravity clock. Hellwing launch AI is kept
    // separately from current velocity, including its native ai0>=15 clamp
    // before the 39/40 pull and after Harpy turns the current velocity.
    // 710 retains its finite native fan, OnHit children and Kill Damage.
    // Random death bees remain a
    // autonomous role, never an invented primary contact or private RNG run.
    internal static class HostSpecialArrowAttack
    {
        internal static bool Handles(int type){return type==469 || type==485 || type==710;}
        internal struct State
        {
            internal Vector2 Center,Velocity;internal float Counter,Second;internal int Left;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Burst,Killed;
            internal Vector2 DeathPosition,OldVelocity;
            internal State(Projectile shot)
            {Center=shot.Center;Velocity=shot.velocity;Counter=shot.ai[0];Second=shot.ai[1];Left=shot.timeLeft;Alive=true;Wet=shot.wet;Honey=shot.honeyWet;Lava=shot.lavaWet;Shimmer=shot.shimmerWet;Burst=Killed=false;DeathPosition=Vector2.Zero;OldVelocity=shot.oldVelocity;}
        }
        internal static bool TrySeedMotion(int type,float speed,int weapon,out AttackMotion motion)
        {
            motion=default(AttackMotion);if(!Handles(type))return false;var p=ContentSamples.ProjectilesByType[type];
            // Direction candidates are seeds only. Every accepted contact is
            // replayed with the complete motor below, not this straight basis.
            motion=new AttackMotion(speed,0,0,p.extraUpdates+1,p.width,p.height,p.timeLeft,weapon==3019 || weapon==3859 || type==485?AttackConfidence.Representative:AttackConfidence.Conditional,componentBirthLimit:true);return true;
        }
        internal static bool TrySeedMotion(AttackAmmoSnapshot ammo,out AttackMotion motion)
        {if(!TrySeedMotion(ammo.Projectile,ammo.Speed,ammo.WeaponType,out motion))return false;var p=ContentSamples.ProjectilesByType[ammo.Projectile];if(ammo.ExtraArrowUpdates && p.extraUpdates<1)motion=motion.WithUpdates(2,1);return true;}
        internal static Vector2 BirthVelocity(AttackAmmoSnapshot ammo,Vector2 raw,int direction,float fanMember=-.5f)
        {if(ammo.WeaponType!=3859)return raw;Vector2 reduced=raw*.8f,unit=reduced.SafeNormalize(-Vector2.UnitY);return (reduced+unit*fanMember*.5f).RotatedBy(fanMember*MathHelper.Pi/180*-direction);}
        private static float VerticalSeedDistance(float vy,int steps)
        {
            if(steps<20)return vy*steps;int gravitySteps=steps-19;
            int falling=Math.Min(gravitySteps,Math.Max(0,(int)Math.Floor((12-vy)/.2f)));
            return vy*(19+falling)+.1f*falling*(falling+1)+12*(gravitySteps-falling);
        }
        internal static bool PhoenixSeed(AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int tick,int steps,int direction,out Vector2 unit)
        {
            unit=Vector2.Zero;if(ammo.Projectile!=710)return false;
            float speed=ammo.WeaponType==3859?ammo.Speed*.8f-.25f:ammo.Speed;if(!(speed>0) || speed>=16)return false;
            var b=timeline[tick].ProjectileReceiveBounds;float dy=b.CenterY-origin.Y,dx=b.CenterX-origin.X,low=-speed,high=speed;
            if(dy<VerticalSeedDistance(low,steps) || dy>VerticalSeedDistance(high,steps))return false;
            // This bounded inverse is only a seed. Positive-Y-only X drag
            // changes the reachable curve, so a straight angular grid can
            // miss a legal shot between its representatives. Replay below
            // still verifies every AI, tile and receive endpoint exactly.
            for(int i=0;i<16;i++){float mid=(low+high)*.5f;if(VerticalSeedDistance(mid,steps)<dy)low=mid;else high=mid;}
            float vy=(low+high)*.5f,vx=(float)Math.Sqrt(Math.Max(0,speed*speed-vy*vy))*(dx<0?-1:1);
            int start=19+Math.Max(1,(int)Math.Floor(-vy/.2f)+1),dragged=Math.Max(0,steps-start+1);
            float response=dragged==0?steps:start-1+.98f*(1-(float)Math.Pow(.98,dragged))/.02f;
            if(Math.Abs(vx*response-dx)>b.Width*.5f+ContentSamples.ProjectilesByType[710].width*.5f+2)return false;
            unit=new Vector2(vx,vy)/speed;if(ammo.WeaponType==3859)unit=unit.RotatedBy(-.5f*MathHelper.Pi/180*direction);return true;
        }
        internal static bool Motor(int type,ref float counter,float second,bool wet,bool honey,bool shimmer,ref Vector2 velocity,out bool alive)
        {
            alive=true;if(shimmer || type==710 && wet)return false;
            if(type==469 && wet && !honey){alive=false;return true;}
            if((type==469 || type==485) && counter>=15){counter=15;velocity.Y+=.1f;}
            if(type==485)velocity=(velocity*39+new Vector2(counter,second))/40;
            else if(type==710 && counter>=0)
            {counter++;if(counter>=20){velocity.Y+=.2f;if(velocity.Y>0)velocity.X*=.98f;if(velocity.Y>12)velocity.Y=12;}}
            if(velocity.Y>16)velocity.Y=16;return true;
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State state)
        {
            if(sample.type==469)return BeeArrowStep(sample,terrain,ref state);
            if(!state.Alive || state.Left<=0){state.Alive=false;return true;}
            Vector2 wetVelocity=state.Velocity;
            bool alive;if(!Motor(sample.type,ref state.Counter,state.Second,state.Wet,state.Honey,state.Shimmer,ref state.Velocity,out alive))return false;if(!alive){state.Alive=false;return true;}
            float impulse;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out impulse))return false;state.Velocity.X+=impulse;
            if(!sample.ignoreWater)
            {bool wet,lava;byte kind;if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3 || !terrain.ProjectileLava(state.Center.X,state.Center.Y,sample.width,sample.height,out lava))return false;if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;state.Wet=wet;if(!wet)state.Lava=state.Honey=false;}
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=state.Center-half,velocity=state.Velocity;bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,state.Wet,state.Honey,out wetVelocity,out hit))return false;
            state.Center=position+half;state.Velocity=velocity;if(hit){if(sample.type==710){state.Center+=velocity;state.Burst=true;}state.Alive=false;return true;}state.Center+=state.Wet?wetVelocity:velocity;state.Left--;return true;
        }
        private static void BeeArrowKill(Projectile sample,ref State state)
        {if(!state.Alive)return;state.DeathPosition=state.Center-new Vector2(sample.width,sample.height)*.5f;state.Alive=false;state.Left=0;state.Killed=true;}
        private static bool BeeArrowStep(Projectile sample,PredictionTerrain terrain,ref State state)
        {
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=state.Center-half;
            if(position.X<=Main.leftWorld || position.X+sample.width>=Main.rightWorld || position.Y<=Main.topWorld || position.Y+sample.height>=Main.bottomWorld || float.IsNaN(position.X) || float.IsNaN(position.Y))
            {state.Alive=false;return true;} // Native pre-AI retirement creates no Kill bees.
            state.OldVelocity=state.Velocity;Vector2 wetVelocity=state.Velocity;
            if(state.Wet && !state.Honey && !state.Shimmer)BeeArrowKill(sample,ref state);
            if(state.Counter>=15){state.Counter=15;state.Velocity.Y+=.1f;}if(state.Velocity.Y>16)state.Velocity.Y=16;
            float impulse;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out impulse))return false;state.Velocity.X+=impulse;
            if(sample.owner<255 && !Main.player[sample.owner].active)BeeArrowKill(sample,ref state);
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind,true) || !terrain.ProjectileLava(state.Center.X,state.Center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;if(wet && kind==3)state.Shimmer=true;state.Wet=wet;if(!wet)state.Honey=state.Lava=state.Shimmer=false;
            }
            if(state.Shimmer){state.Velocity.Y-=.2f;if(state.Velocity.Y<0)BeeArrowKill(sample,ref state);}
            Vector2 velocity=state.Velocity;bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,state.Wet,state.Honey,out wetVelocity,out hit,true,state.Shimmer,true))return false;
            state.Center=position+half;
            if(hit){state.Center+=velocity;BeeArrowKill(sample,ref state);}
            state.Velocity=velocity;state.Center+=state.Wet?wetVelocity:velocity;if(state.Alive)state.Left--;return true;
        }
        private static AttackContact BeeArrowCandidate(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,PredictionTerrain terrain,HostAttackObstacles obstacles,HostProjectileBirthSlots captured,HostProjectileTargets beeTargets,Vector2 origin,Vector2 velocity,Vector2 aim,int age,int delay,AttackConfidence confidence)
        {
            var slots=captured.Copy();int slot;if(!slots.TryAllocate(out slot))return null;
            var shot=new Projectile();shot.SetDefaults(469);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.damage=ammo.Damage;shot.Center=origin;shot.velocity=velocity;
            bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;
            var state=new State(shot);int extra=shot.extraUpdates,limit=Math.Min(120,timeline.Count-age-1),k=0;
            for(int outer=1;outer<=limit && state.Alive;outer++)
            {
                if(!obstacles.BeforeNpc(age+outer,delay+outer,state.Center.X,state.Center.Y))return null;
                int updates=extra+1;
                for(int sub=0;sub<updates && state.Alive;sub++)
                {
                    if(!BeeArrowStep(shot,terrain,ref state))return null;if(ammo.ExtraArrowUpdates && extra<1)extra=1;
                    int tick=age+outer;k++;
                    if(state.Alive)
                    {
                        if(!obstacles.PassOuter(k+1,tick,delay+outer,state.Center.X,state.Center.Y))
                        {if(obstacles.Remaining>0)return null;BeeArrowKill(shot,ref state);}
                        else if(obstacles.SelectedAllows(delay+outer))
                        {var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,shot.width,shot.height,confidence);if(contact!=null)return contact;}
                        if(state.Alive && state.Left<=0)BeeArrowKill(shot,ref state);
                    }
                    if(state.Killed)
                    {
                        // Parent history includes the real preceding OnHit;
                        // children inherit only the common static181 rules.
                        var contact=HostBeeAttack.BirthRepresentative(shot,state.DeathPosition,state.OldVelocity,false,slots,timeline,tick,delay+outer,terrain,obstacles,beeTargets);
                        if(contact!=null)return AttackIntercept.WithAimInput(contact,aim.X,aim.Y);
                    }
                }
            }
            return null;
        }
        private sealed class PhoenixMember
        {
            internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;internal int Slot,Extra;
        }
        private static Projectile PhoenixShot(Player player,int damage)
        {var p=new Projectile();p.SetDefaults(710);p.owner=player.whoAmI;p.damage=damage;return p;}
        internal static Vector2 PhoenixShardVelocity(int index)
        {float angle=(float)Math.PI/4,step=(0-angle*2)/4;return new Vector2(0,6).RotatedBy(angle+step*index);}
        private static bool BirthState(Projectile shot,PredictionTerrain terrain,Vector2 center,Vector2 velocity,float second,out State state)
        {
            state=new State(shot){Center=center,Velocity=velocity,Counter=0,Second=second};bool wet;byte kind;
            if(!terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out wet,out kind) || wet && kind==3)return false;
            state.Wet=!shot.ignoreWater && wet;state.Honey=wet && kind==2;state.Lava=false;return true;
        }
        private static bool SpawnPhoenixChildren(Player player,PhoenixMember parent,PredictionTerrain terrain,HostProjectileBirthSlots slots,List<PhoenixMember> members)
        {
            if(parent.State.Second==-1 || parent.Shot.owner!=Main.myPlayer)return true;
            for(int i=0;i<5;i++)
            {
                int slot;if(!slots.TryAllocate(out slot))return false;var shot=PhoenixShot(player,parent.Shot.damage);State child;
                if(!BirthState(shot,terrain,parent.State.Center,PhoenixShardVelocity(i),-1,out child))return false;
                members.Add(new PhoenixMember{Shot=shot,State=child,Slot=slot,Extra=shot.extraUpdates,Receivers=parent.Receivers.ForkForSpawn(shot,true)});
            }
            return true;
        }
        private static AttackContact PhoenixDamage(Player player,PhoenixMember member,PredictionTerrain terrain,HostProjectileBirthSlots slots,List<PhoenixMember> members,NpcTrajectory timeline,Vector2 aim,int tick,int outer,int sub,bool burst,AttackConfidence confidence,out bool known)
        {
            known=true;if(member.Shot.damage<=0)return null;int before=member.Receivers.OtherHits,side=burst?40:member.Shot.width;
            // Kill does not replace 710's penetration. Tile/time death still
            // has its positive one-hit budget; a body-killed penetrate=0
            // performs Damage without further positive consumption.
            bool pass=member.Receivers.PassShapeOuter(2,tick,outer,member.State.Center.X,member.State.Center.Y,side,burst?40:member.Shot.height,burst && member.Receivers.Remaining<=0);
            // Pass publishes only qualified native receives, including the
            // last finite hit. Spawn sees that hit's copied local=-1 before
            // penetration reaches zero and Kill's 40x40 Damage starts.
            if(!burst)for(int i=before;i<member.Receivers.OtherHits;i++)
                if(!SpawnPhoenixChildren(player,member,terrain,slots,members)){known=false;return null;}
            if(!pass && member.Receivers.Remaining!=0){known=false;return null;}
            if(pass && member.Receivers.SelectedAllows(outer))
            {var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,member.State.Center.X,member.State.Center.Y,side,burst?40:member.Shot.height,confidence);if(contact!=null)return contact;}
            return null;
        }
        private static AttackContact PhoenixFamily(Player player,List<PhoenixMember> members,PredictionTerrain terrain,HostProjectileBirthSlots slots,NpcTrajectory timeline,Vector2 aim,int age,int delay,bool quiver,AttackConfidence confidence)
        {
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int outer=1;outer<=limit;outer++)
            {
                // Only members already alive before this NPC action existed
                // for its reflection scan. Children born below wait until the
                // next outer; higher slots do not replay an earlier NPC phase.
                foreach(var existing in members)if(existing.State.Alive && !existing.Receivers.BeforeNpc(age+outer,delay+outer,existing.State.Center.X,existing.State.Center.Y))existing.State.Alive=false;
                int lastSlot=-1,visited=0;
                // Native Main updates ascending projectile slots. A child
                // born above its parent updates this same outer frame; a
                // lower reused slot waits until the following frame. Scan
                // this bounded family (<=36), not the live 1000-slot scene.
                while(visited++<36)
                {
                    PhoenixMember member=null;foreach(var item in members)if(item.State.Alive && item.Slot>lastSlot && (member==null || item.Slot<member.Slot))member=item;
                    if(member==null)break;lastSlot=member.Slot;int updates=member.Extra+1;
                    for(int sub=0;sub<updates && member.State.Alive;sub++)
                    {
                        if(!Step(member.Shot,terrain,ref member.State))return null;int tick=age+outer;bool known;AttackContact contact;
                        if(member.State.Alive)
                        {
                            if(quiver && member.Extra<1)member.Extra=1;
                            contact=PhoenixDamage(player,member,terrain,slots,members,timeline,aim,tick,delay+outer,sub,false,confidence,out known);if(!known || contact!=null)return contact;
                            if(member.Receivers.Remaining==0 || member.State.Left<=0){member.State.Alive=false;member.State.Burst=true;}
                        }
                        if(member.State.Burst)
                        {
                            member.State.Second=-1;
                            contact=PhoenixDamage(player,member,terrain,slots,members,timeline,aim,tick,delay+outer,sub,true,confidence,out known);if(!known || contact!=null)return contact;
                            member.State.Burst=false;
                        }
                        if(!member.State.Alive)slots.Release(member.Slot);
                    }
                }
            }
            return null;
        }
        internal static AttackContact ReplayFamily(Player player,Projectile[] shots,PredictionTerrain terrain,NpcTrajectory timeline,Vector2 aim,int age,bool beforeNpc,bool quiver)
        {
            if(shots.Length==0)return null;var root=new HostAttackObstacles(player,shots[0],timeline,0,beforeNpc,!beforeNpc);root.BeginCandidate();var slots=root.CaptureBirthSlots().Copy();var members=new List<PhoenixMember>();
            foreach(var shot in shots){if(shot.type!=710 || !shot.active)return null;members.Add(new PhoenixMember{Shot=shot,State=new State(shot),Slot=shot.whoAmI,Extra=shot.extraUpdates,Receivers=root.ForkForSpawn(shot)});}
            return PhoenixFamily(player,members,terrain,slots,timeline,aim,age,0,quiver,AttackConfidence.Conditional);
        }
        internal static AttackContact Replay(Projectile shot,PredictionTerrain terrain,NpcTrajectory timeline,Vector2 aim,int age,bool quiver)
        {
            var state=new State(shot);int extra=shot.extraUpdates,limit=Math.Min(180,timeline.Count-age-1);
            for(int outer=1;outer<=limit && state.Alive && state.Left>0;outer++)
            {int updates=extra+1;for(int sub=0;sub<updates && state.Alive && state.Left>0;sub++)
                {if(!Step(shot,terrain,ref state) || !state.Alive)return null;if(quiver && extra<1)extra=1;var contact=AttackIntercept.BodyContact(timeline,age+outer,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,shot.width,shot.height,AttackConfidence.Conditional);if(contact!=null)return contact;}}
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];AttackMotion motion;if(!TrySeedMotion(ammo,out motion))return null;
            bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3 && sample.type!=469)return null;
            var beeSlots=sample.type==469?obstacles.CaptureBirthSlots():null;
            HostProjectileTargets beeTargets=null;
            if(sample.type==469){var parent=new Projectile();parent.SetDefaults(469);parent.owner=player.whoAmI;parent.damage=ammo.Damage;beeTargets=HostBeeAttack.CaptureTargets(parent,obstacles);obstacles.CaptureBeeOwner();}
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;var direct=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0);var directions=new Vector2[162];int count=0,damage=ammo.Projectile==710?player.GetWeaponDamage(player.HeldItem):0;
            for(int seed=ammo.Projectile==710?-limit-1:-1;seed<41;seed++)
            {
                Vector2 direction;if(seed< -1){int outer=limit+seed+2;if(!PhoenixSeed(ammo,timeline,origin,age+outer,motion.SubupdatesForOuters(outer),player.direction,out direction))continue;}else if(seed<0){if(direct==null)continue;direction=new Vector2(direct.AimX-origin.X,direct.AimY-origin.Y).SafeNormalize(Vector2.UnitX*player.direction);}else if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                Vector2 raw=direction*ammo.Speed;var aim=origin+direction*10000;obstacles.BeginCandidate();
                if(ammo.Projectile==469)
                {Vector2 launch=raw;motion.Launch(ref launch.X,ref launch.Y);var bee=BeeArrowCandidate(player,ammo,timeline,terrain,obstacles,beeSlots,beeTargets,origin,launch,aim,age,delay,motion.Confidence);if(bee!=null)return bee;continue;}
                if(ammo.Projectile==710)
                {
                    var slots=obstacles.CaptureBirthSlots().Copy();var members=new List<PhoenixMember>();bool born=true;int births=ammo.WeaponType==3859?6:1;
                    for(int i=0;i<births;i++){int slot;if(!slots.TryAllocate(out slot)){born=false;break;}var shot=PhoenixShot(player,damage);Vector2 fan=BirthVelocity(ammo,raw,player.direction,i-2.5f);motion.Launch(ref fan.X,ref fan.Y);State start;if(!BirthState(shot,terrain,origin,fan,0,out start)){born=false;break;}members.Add(new PhoenixMember{Shot=shot,State=start,Slot=slot,Extra=shot.extraUpdates,Receivers=obstacles.ForkForSpawn(shot)});}
                    if(!born)continue;var family=PhoenixFamily(player,members,terrain,slots,timeline,aim,age,delay,ammo.ExtraArrowUpdates,motion.Confidence);if(family!=null)return family;continue;
                }
                Vector2 velocity=BirthVelocity(ammo,raw,player.direction);motion.Launch(ref velocity.X,ref velocity.Y);var state=new State(sample){Center=origin,Velocity=velocity,Counter=ammo.WeaponType==3019?raw.X:0,Second=ammo.WeaponType==3019?raw.Y:0,Wet=!sample.ignoreWater && wet,Honey=wet && kind==2,Lava=false};int k=0,extra=sample.extraUpdates;
                for(int outer=1;outer<=limit && state.Alive && state.Left>0;outer++)
                {if(!obstacles.BeforeNpc(age+outer,delay+outer,state.Center.X,state.Center.Y)){state.Alive=false;break;}int updates=extra+1;for(int sub=0;sub<updates && state.Alive && state.Left>0;sub++)
                    {k++;if(!Step(sample,terrain,ref state) || !state.Alive){state.Alive=false;break;}if(ammo.ExtraArrowUpdates && extra<1)extra=1;int tick=age+outer;if(!obstacles.PassOuter(k+1,tick,delay+outer,state.Center.X,state.Center.Y)){state.Alive=false;break;}if(receive.Allows(delay+outer)){var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,sample.width,sample.height,motion.Confidence);if(contact!=null)return contact;}}
                }
            }
            return null;
        }
    }
}
