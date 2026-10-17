using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI001 pulses stop at their first tile contact and retain a stationary
    // damage body until their already-grown trail has faded. A wall hit is
    // neither immediate death nor permission to continue through the wall.
    internal static class HostPulseAttack
    {
        internal static bool Handles(int type){return type==440 || type==449 || type==606;}
        internal struct State
        {
            internal Vector2 Position,Velocity;private Vector2 halfSize;
            internal float Phase,Length;internal int Left;internal bool Alive;
            internal Vector2 Center {get{return Position+halfSize;}}
            internal State(Projectile p){Position=p.position;Velocity=p.velocity;halfSize=p.Size*.5f;Phase=p.ai[1];Length=p.localAI[0];Left=p.timeLeft;Alive=p.active;}
            internal State(Projectile p,Vector2 origin,Vector2 velocity):this(p)
            {Position=origin-halfSize;Velocity=velocity;Phase=Length=0;Alive=true;while(Velocity.X>=16 || Velocity.X<=-16 || Velocity.Y>=16 || Velocity.Y< -16)Velocity*=.97f;}
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State state)
        {
            if(!state.Alive || state.Left<=0){state.Alive=false;return true;}
            float speed=sample.type==606?5:3,maximum=sample.type==606?150:100;
            if(state.Phase==0)state.Length=Math.Min(maximum,state.Length+speed);
            else {state.Length-=speed;if(state.Length<=0){state.Alive=false;return true;}}
            if(!HostProjectileEnvironment.Dry(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X))return false;
            if(state.Phase!=1)
            {
                Vector2 previous=state.Velocity,wet;bool hit=false;
                if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref state.Position,ref state.Velocity,false,false,out wet,out hit))return false;
                if(hit){state.Phase=1;state.Position+=state.Velocity;state.Velocity=previous;}
                else state.Position+=state.Velocity;
            }
            state.Left--;return true;
        }
        internal static AttackContact Replay(Player player,Projectile sample,State state,NpcTrajectory timeline,int firstTick,int elapsed,PredictionTerrain terrain,HostAttackObstacles obstacles,HostAttackReceive receive,Vector2 aim,AttackConfidence confidence)
        {
            obstacles.BeginCandidate();int k=0;
            for(int outer=0;outer<120 && firstTick+outer<timeline.Count && state.Alive && state.Left>0;outer++)
            for(int sub=0;sub<=sample.extraUpdates && state.Alive && state.Left>0;sub++)
            {
                if(sub==0 && !obstacles.BeforeNpc(firstTick+outer,elapsed+outer,state.Center.X,state.Center.Y))return null;
                if(!Step(sample,terrain,ref state) || !state.Alive)return null;
                k++;int tick=firstTick+outer;
                if(!obstacles.PassOuter(k,tick,elapsed+outer,state.Center.X,state.Center.Y))return null;
                if(!receive.Allows(elapsed+outer))continue;
                var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,sample.width,sample.height,confidence);
                if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out HostAttackObstacles obstacles)
        {
            obstacles=new HostAttackObstacles(player,shot,timeline,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            // A sealed pulse has no cursor consumer. The point is only an
            // observation carrier; Control never borrows input for this role.
            return Replay(player,shot,new State(shot),timeline,clock.FirstTick,clock.LaunchDelay+1,terrain,obstacles,receive,shot.Center+shot.velocity,AttackConfidence.Conditional);
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            var motor=new AttackMotion(ammo.Speed,0,0,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,componentBirthLimit:true);
            var direct=AttackIntercept.Solve(origin.X,origin.Y,motor,timeline,age,0);Vector2 prior=new Vector2(float.NaN);
            for(int seed=-1;seed<9;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+Math.Max(0,seed)*4)].ProjectileReceiveBounds;if(seed<0 && direct==null)continue;
                Vector2 target=seed<0?new Vector2(direct.AimX,direct.AimY):new Vector2(b.CenterX,b.CenterY);
                Vector2 unit=(target-origin).SafeNormalize(Vector2.UnitX*player.direction);if(unit==prior)continue;prior=unit;
                var contact=Replay(player,sample,new State(sample,origin,unit*ammo.Speed),timeline,age+1,delay+1,terrain,obstacles,receive,origin+unit*10000,AttackConfidence.Conditional);
                if(contact!=null)return contact;
            }
            return null;
        }
    }
}
