using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The old AI9 knife keeps its near-cursor orbit, negative steering clock,
    // ten-pixel tile body, reflection and autonomous return. It is not the
    // newer magic missile's direct 32px cursor chase.
    internal static class HostFlyingKnifeAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal struct State
        {
            internal Vector2 Position,Velocity;private Vector2 halfSize;internal float Phase,Age;internal int Left;internal bool Alive,Tile,Wet,Honey;
            internal Vector2 Center {get{return Position+halfSize;}}
            internal State(Projectile p){Position=p.position;halfSize=p.Size*.5f;Velocity=p.velocity;Phase=p.ai[0];Age=p.localAI[0];Left=p.timeLeft;Alive=p.active;Tile=p.tileCollide;Wet=p.wet;Honey=p.honeyWet;}
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State s,Vector2 owner,Vector2 aim,bool channel)
        {
            if(!s.Alive || s.Left<=0){s.Alive=false;return true;}Vector2 wetMove=s.Velocity;
            if(s.Phase<=0)
            {
                if(channel)
                {
                    Vector2 delta=aim-s.Center;float distance=delta.Length();if(s.Phase<0)s.Phase++;
                    if(distance<100)
                    {if(s.Velocity.Length()<20){s.Velocity*=1.1f;if(s.Velocity.Length()>20)s.Velocity=s.Velocity.SafeNormalize(Vector2.Zero)*20;}if(s.Phase==0)s.Phase=-10;}
                    else if(distance>20)s.Velocity=(s.Velocity*4+delta*(20/distance))/5;
                    else s.Velocity=delta;
                }
                else s.Phase=1;
            }
            s.Age++;
            if(s.Phase>0 && s.Age>15)
            {s.Tile=false;Vector2 delta=owner-s.Center;if(delta.Length()<20){s.Alive=false;return true;}s.Velocity=(s.Velocity*5+Vector2.Normalize(delta)*25)/6;}
            if(Vector2.Distance(s.Center,owner)>2000){s.Alive=false;return true;}
            // AI009 applies this cap after both its orbit and return motor,
            // before Update's wind/liquid/tile phases (also while submerged).
            if(s.Velocity.Y>16)s.Velocity.Y=16;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,s.Center.X,s.Center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            bool wet;byte kind;if(!terrain.ProjectileWet(s.Center.X,s.Center.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3)return false;
            s.Wet=wet;if(wet && kind==2)s.Honey=true;if(!wet)s.Honey=false;
            if(s.Tile)
            {
                Vector2 previous=s.Velocity;bool hit;
                if(!terrain.ProjectileTileMotion(sample,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetMove,out hit))return false;
                if(hit)
                {if(s.Phase<=0)s.Phase=-10;if(s.Velocity.X!=previous.X && Math.Abs(previous.X)>0)s.Velocity.X=-previous.X;if(s.Velocity.Y!=previous.Y && Math.Abs(previous.Y)>0)s.Velocity.Y=-previous.Y;}
            }
            // Native initializes wetVelocity before AI. Once returning with
            // tileCollide=false, water uses that old displacement unchanged.
            s.Position+=s.Wet?wetMove:s.Velocity;s.Left--;return true;
        }
        internal static AttackContact Solve(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain)
        {return SolvePrepared(player,shot,timeline,clock,terrain,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame)
        {
            int limit=Math.Min(120,timeline.Count-clock.FirstTick);if(limit<=0)return null;
            var body=(Player)copy.Invoke(player,null);var motion=frame.Motion;var positions=new PredictionPlayerMotion[limit];var world=frame.Environment;
            for(int step=0;step<limit;step++){if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,world,terrain,out stop)){limit=step;break;}}positions[step]=motion;}
            var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);bool channel=player.channel && player.HeldItem.shoot==shot.type;
            int estimate=Math.Max(1,(int)(Vector2.Distance(shot.Center,new Vector2(timeline[clock.FirstTick].Bounds.CenterX,timeline[clock.FirstTick].Bounds.CenterY))/20));
            Vector2 previousAim=new Vector2(float.NaN);
            foreach(int offset in new[]{0,3,10,25})
            {
                var bounds=timeline[Math.Min(timeline.Count-1,clock.FirstTick+estimate+offset)].ProjectileReceiveBounds;Vector2 aim=new Vector2(bounds.CenterX,bounds.CenterY);if(aim==previousAim)continue;previousAim=aim;
                var state=new State(shot);
                for(int step=0;step<limit && state.Alive;step++)
                {
                    HostAttackPlayerFrame.ApplyPose(body,positions[step]);Vector2 point=aim;body.LimitPointToPlayerReachableArea(ref point);
                    if(!Step(shot,terrain,ref state,body.Center,point,channel) || !state.Alive)break;
                    if(receive.Allows(clock.LaunchDelay+step+1))
                    {var contact=AttackIntercept.BodyContact(timeline,clock.FirstTick+step,0,aim.X,aim.Y,state.Center.X,state.Center.Y,shot.width,shot.height,AttackConfidence.Conditional);if(contact!=null)return contact;}
                }
                if(!channel)break; // Return has one actual owner path, no cursor candidates.
            }
            return null;
        }
    }
}
