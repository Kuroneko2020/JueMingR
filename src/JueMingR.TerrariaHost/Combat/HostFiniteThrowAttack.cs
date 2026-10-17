using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI002's flame pair has a finite ai0 clock, distinct from timeLeft.
    // Every real preceding NPC hit moves ai0 halfway toward sixty. Replaying
    // only the newborn parabola can advertise a contact after native AI killed
    // this projectile. All state here is private; no NPC damage/buff or RNG.
    internal static class HostFiniteThrowAttack
    {
        internal static bool Handles(int type){return type==93 || type==166 || type==306 || type==954 || type==979 || HostSeedThrowAttack.Handles(type);}
        internal struct State
        {
            internal Vector2 Center,Velocity;internal float Counter;internal int Left;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Representative;internal int GravityThreshold;
            internal State(Projectile sample,Vector2 center,Vector2 velocity)
            {Center=center;Velocity=velocity;Counter=sample.ai[0];Left=sample.timeLeft;Alive=true;Wet=sample.wet;Honey=sample.honeyWet;Lava=sample.lavaWet;Shimmer=sample.shimmerWet;Representative=false;GravityThreshold=28;}
        }
        internal static void OnOtherHits(ref State state,int hits)
        {for(int i=0;i<hits;i++)state.Counter+=(60-state.Counter)/2;}
        // Step leaves the final movement available to Damage. Native decrements
        // timeLeft and calls Kill only after that Damage pass, so a last-step
        // receiver must not disappear merely because the private clock is zero.
        internal static void Tail(ref State state)
        {if(state.Alive && state.Left<=0){state.Alive=false;state.Left=0;}}
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State state)
        {
            if(HostSeedThrowAttack.Handles(sample.type))return HostSeedThrowAttack.Step(sample,terrain,ref state);
            if(!state.Alive || state.Left<=0){state.Alive=false;return true;}
            Vector2 wetVelocity=state.Velocity;
            if(state.Shimmer && sample.type!=166)return false;
            if(Main.windPhysics)state.Velocity.X+=Main.windSpeedCurrent*Main.windPhysicsStrength;
            if(sample.type==93)
            {
                state.Counter++;
                // Native resamples 28..33 each AI. Its possible first gravity
                // steps are exactly these six finite outcomes, then ai0=40
                // makes every later step deterministic. Never run its RNG.
                if(state.Counter>=28 && state.Counter<=33)state.Representative=true;
                if(state.Counter>=state.GravityThreshold){state.Counter=40;state.Velocity.Y+=.4f;state.Velocity.X*=.97f;}
            }
            else if(sample.type==166)
            {state.Counter++;if(state.Counter>=20){state.Velocity.Y+=.3f;state.Velocity.X*=.98f;}}
            else if(sample.type!=306)
            {
                state.Counter++;if(state.Counter>=60){state.Alive=false;return true;}
                if(state.Counter>=20){state.Velocity.X*=.99f;state.Velocity.Y+=.1f;}
            }
            // 306's own AI002 branch only fades/rotates. Its ai0 and velocity
            // have no ordinary throw gravity, even after twenty steps.
            if(state.Velocity.Y>32)state.Velocity.Y=32;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out wind))return false;state.Velocity.X+=wind;
            if(!sample.ignoreWater)
            {bool wet,lava;byte kind;if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind,sample.type==166) || wet && kind==3 && sample.type!=166 || !terrain.ProjectileLava(state.Center.X,state.Center.Y,sample.width,sample.height,out lava,sample.type==166))return false;if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;if(wet && kind==3)state.Shimmer=true;state.Wet=wet;if(!wet)state.Honey=state.Lava=state.Shimmer=false;}
            // Shimmer's AI002 branch adjusts velocity and lifetime before
            // movement; the ordinary one-step lifetime tail still follows.
            if(sample.type==166 && state.Shimmer){state.Velocity.Y-=.7f;state.Left-=20;}
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=state.Center-half,velocity=state.Velocity;bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,state.Wet,state.Honey,out wetVelocity,out hit,true,state.Shimmer,sample.type==166))return false;
            state.Center=position+half;state.Velocity=velocity;if(hit){state.Alive=false;return true;}
            state.Center+=state.Wet?wetVelocity:velocity;state.Left--;return true;
        }
        internal static bool StepSnowball(Projectile sample,HostProjectileTargets towns,PredictionTerrain terrain,ref State state)
        {
            Vector2 position=state.Center-new Vector2(sample.width,sample.height)*.5f;
            if(position.X<=Main.leftWorld || position.X+sample.width>=Main.rightWorld || position.Y<=Main.topWorld || position.Y+sample.height>=Main.bottomWorld)
            {state.Alive=false;return true;}
            // Native item949 writes ai1=1; SnowballCannon1319 leaves it zero.
            // The friendly town-body query precedes motor/movement/Damage and
            // cannot be inferred from the hostile receiver list. Other town
            // bodies are frozen observations, leased at Prepare/Consume.
            if(towns!=null)
            {
                if(Main.netMode==1)return false; // Allied-player futures need their own multiplayer lease.
                var box=new Rectangle((int)position.X,(int)position.Y,sample.width,sample.height);
                for(int i=0;i<towns.Count;i++)
                {var b=towns.Bounds(i);if(box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))){state.Alive=false;state.Left=0;return true;}}
            }
            return Step(sample,terrain,ref state);
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3 && sample.type!=166)return null;
            var towns=sample.type==166 && ammo.WeaponType==949?obstacles.CaptureTownBodies(sample):null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);AttackMotion motion;if(!HostAttackModels.TryRead(ammo,out motion))return null;
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            for(int threshold=28;threshold<=(sample.type==93?33:28);threshold++)
            {
            var proposals=HostMotionAttackInputs.Generate(origin,sample.type==93?motion.WithGravityStart(threshold):motion,timeline,age);var directions=new Vector2[proposals.Length+41];int count=0;
            for(int input=0;input<directions.Length;input++)
            {
                int seed=input-proposals.Length;
                Vector2 direction;if(seed<0)direction=proposals[input];else if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var state=new State(sample,origin,direction*ammo.Speed){Wet=!sample.ignoreWater && wet,Honey=wet && kind==2,Shimmer=wet && kind==3,GravityThreshold=threshold};var aim=origin+direction*10000;obstacles.BeginCandidate();int k=0;
                for(int outer=1;outer<=limit && state.Alive && state.Left>0;outer++)for(int sub=0;sub<sample.extraUpdates+1 && state.Alive && state.Left>0;sub++)
                {
                    if(sub==0 && !obstacles.BeforeNpc(age+outer,delay+outer,state.Center.X,state.Center.Y)){state.Alive=false;break;}
                    k++;if(!(sample.type==166?StepSnowball(sample,towns,terrain,ref state):Step(sample,terrain,ref state)) || !state.Alive){state.Alive=false;break;}int tick=age+outer,hits=obstacles.OtherHits;if(!obstacles.PassOuter(k+1,tick,delay+outer,state.Center.X,state.Center.Y)){state.Alive=false;break;}if(sample.type==954 || sample.type==979)OnOtherHits(ref state,obstacles.OtherHits-hits);if(receive.Allows(delay+outer)){var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,sample.width,sample.height,state.Representative?AttackConfidence.Representative:motion.Confidence);if(contact!=null)return contact;}Tail(ref state);
                }
            }
            }
            return null;
        }
    }
}
