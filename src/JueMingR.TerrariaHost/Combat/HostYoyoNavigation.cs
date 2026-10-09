using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Owns only a bounded navigation path and its terrain/progress lease.
    // Every accepted route is replayed with the native controlled motor;
    // neither a geometric polyline nor a navigation corner is a red impact.
    internal sealed class HostYoyoNavigation
    {
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private readonly List<Vector2> path=new List<Vector2>();
        private Vector2 playerAnchor,targetAnchor,progressAnchor;
        private uint progressStep,retryStep;
        internal int Searches {get;private set;}
#if DEBUG
        internal string ReplayStop {get;private set;}
        internal int ReplayStep {get;private set;}
        internal Vector2 ReplayPlayer {get;private set;}
#endif
        internal static bool Handles(Projectile shot){return shot.aiStyle==99 && !shot.counterweight && !(shot.type>=556 && shot.type<=561) && shot.type!=1079;}
        internal static bool Weapon(Item item){return item!=null && item.type>0 && ItemID.Sets.Yoyo[item.type];}
        internal static bool Secondary(Projectile shot)
        {for(int i=0;i<shot.whoAmI;i++){var other=Main.projectile[i];if(other.active && other.owner==shot.owner && other.type==shot.type && other.ai[0]>=-1)return true;}return false;}
        internal static int Remaining(Player player,Projectile shot,bool secondary)
        {
            float lifetime=ProjectileID.Sets.YoyosLifeTimeMultiplier[shot.type];if(lifetime==-1)return 120;
            if(player.yoyoString && lifetime>0)lifetime*=1.5f;
            float clock=shot.localAI[0],rate=(1+player.meleeSpeed)/2;
            if(float.IsNaN(clock) || float.IsInfinity(clock) || rate<=0)return 0;
            // Native second-ball clock adds a random 1..3 on top of +1.
            // Use its maximum +4 to promise only the known safe prefix;
            // preparation never advances vanilla RNG to pick a nicer lifetime.
            for(int i=1;i<=120;i++){clock+=secondary?4:1;if(clock/60/rate>lifetime)return i-1;}
            return 120;
        }
        private static void Parameters(Player player,Projectile shot,bool secondary,out float range,out float speed)
        {Parameters(player,shot.type,secondary,out range,out speed);}
        private static void Parameters(Player player,int type,bool secondary,out float range,out float speed)
        {
            range=ProjectileID.Sets.YoyosMaximumRange[type];speed=ProjectileID.Sets.YoyosTopSpeed[type];
            if(type==554 && secondary)speed*=.75f;
            if(player.yoyoString)range=range*1.25f+30;
            float multiplier=(1+player.meleeSpeed*3)/4;range/=multiplier;speed/=multiplier;
        }
        internal static bool OpeningPoint(Player player,Item item,NpcTrajectory timeline,out Vector2 point)
        {
            var target=timeline[0].ProjectileReceiveBounds;Vector2 goal;Projectile sample;point=Vector2.Zero;
            if(!ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out sample))return false;
            float range,speed;Parameters(player,item.shoot,false,out range,out speed);var navigation=new HostYoyoNavigation();navigation.terrain.Reset();
            // The real launch must face the first gap, otherwise its native
            // initial 16px velocity can hit the wall before steering can turn.
            // This opening point carries no Contact or damage guarantee.
            if(Goal(target,player.Center,range,out goal) && navigation.Find(player.RotatedRelativePoint(player.MountedCenter),goal,player.Center,range,sample.width,sample.height)){point=navigation.path[0];return true;}
            return false;
        }
        private static bool Goal(MotionRect box,Vector2 player,float range,out Vector2 point)
        {
            point=new Vector2(box.CenterX,box.CenterY);if(Vector2.Distance(point,player)<=range-1)return true;
            // A large receive shape can have a reachable edge while its centre
            // is outside. Choose a point strictly inside that existing shape;
            // do not enlarge the input radius or the native damage rectangle.
            float insetX=Math.Min(1,box.Width/4),insetY=Math.Min(1,box.Height/4);
            point=new Vector2(MathHelper.Clamp(player.X,box.X+insetX,box.X+box.Width-insetX),MathHelper.Clamp(player.Y,box.Y+insetY,box.Y+box.Height-insetY));
            return Vector2.Distance(point,player)<=range-1;
        }
        internal AttackContact Prepare(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,out Vector2 point,out bool usable)
        {
            int age=clock.Age;point=Vector2.Zero;usable=false;if(!player.channel || player.CCed || shot.ai[0]<0)return null;
            bool secondary=Secondary(shot);float range,speed;Parameters(player,shot,secondary,out range,out speed);
            if(Remaining(player,shot,secondary)==0){path.Clear();return null;}
            if(range<=1 || speed<=0 || Vector2.Distance(shot.Center,player.Center)>range*1.3f)return null;
            var bounds=timeline[Math.Min(timeline.Count-1,age+Math.Max(1,(int)(Vector2.Distance(shot.Center,new Vector2(timeline[age].Bounds.CenterX,timeline[age].Bounds.CenterY))/speed)))].ProjectileReceiveBounds;
            Vector2 goal;if(!Goal(bounds,player.Center,range,out goal))return null;
            bool reuse=path.Count>0 && terrain.Unchanged && Vector2.DistanceSquared(playerAnchor,player.Center)<64 && Vector2.DistanceSquared(targetAnchor,goal)<144;
            if(reuse && Vector2.DistanceSquared(progressAnchor,shot.Center)>4){progressAnchor=shot.Center;progressStep=Main.GameUpdateCount;}
            if(reuse && Main.GameUpdateCount-progressStep>14){reuse=false;path.Clear();retryStep=Main.GameUpdateCount+5;}
            if(!reuse)
            {
                path.Clear();if(Main.GameUpdateCount<retryStep)return null;
                terrain.Reset();Searches++;if(!Find(shot.Center,goal,player.Center,range,shot.width,shot.height)){retryStep=Main.GameUpdateCount+5;return null;}
                playerAnchor=player.Center;targetAnchor=goal;progressAnchor=shot.Center;progressStep=Main.GameUpdateCount;
            }
            // Replayed steering may fail a geometric route because a fast ball
            // cannot make its corner. Rejection restores the physical cursor.
            float dead=5+speed/2+(secondary?20:0);
            while(path.Count>1 && Vector2.Distance(shot.Center,path[0])<=dead)path.RemoveAt(0);
            var contact=Replay(player,shot,timeline,clock,secondary,range,speed,out point);
            usable=contact!=null;
            if(!usable){path.Clear();retryStep=Main.GameUpdateCount+5;}
            return contact;
        }
        private bool Clear(Vector2 a,Vector2 b,int width,int height)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(a,b)/8));Vector2 old=a;
            for(int i=1;i<=steps;i++){var next=Vector2.Lerp(a,b,(float)i/steps);if(!terrain.ProjectilePassage(old.X,old.Y,next.X,next.Y,width,height))return false;old=next;}return true;
        }
        private bool Find(Vector2 start,Vector2 goal,Vector2 player,float range,int width,int height)
        {
            if(Clear(start,goal,width,height)){path.Add(goal);return true;}
            // 16px local grid, at most 1024 visited cells and 512 expansions.
            // Grid vertices use the actual ball's origin; no snapped origin
            // is silently assumed clear. Whole ball dimensions join each edge.
            var nodes=new List<Vector2>{start};var previous=new List<int>{-1};var xs=new List<int>{0};var ys=new List<int>{0};var seen=new HashSet<long>{0};
            for(int at=0;at<nodes.Count && at<512;at++)
            {
                if(Clear(nodes[at],goal,width,height))
                {path.Add(goal);for(int i=at;i>0;i=previous[i])path.Add(nodes[i]);path.Reverse();return true;}
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                {
                    if(dx==0 && dy==0)continue;int x=xs[at]+dx,y=ys[at]+dy;long key=((long)x<<32)|(uint)y;
                    if(seen.Contains(key) || seen.Count>=1024)continue;seen.Add(key);var next=start+new Vector2(x*16,y*16);
                    if(Vector2.Distance(next,player)>range-1 || !Clear(nodes[at],next,width,height))continue;
                    nodes.Add(next);previous.Add(at);xs.Add(x);ys.Add(y);
                }
            }
            return false;
        }
        private AttackContact Replay(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,bool secondary,float range,float speed,out Vector2 input)
        {
#if DEBUG
            ReplayStop="NoContact";ReplayStep=0;
#endif
            int age=clock.Age,at=0;float dead=5+speed/2+(secondary?20:0);Vector2 center=shot.Center,velocity=shot.velocity;input=path[0];var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var playerMotion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
            for(int step=0;step<Math.Min(Remaining(player,shot,secondary),timeline.Count-age-1);step++)
            {
#if DEBUG
                ReplayStep=step;
#endif
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref playerMotion,environment,terrain,out stop)){
#if DEBUG
                    ReplayStop="Player:"+stop;
#endif
                    return null;}}
                Vector2 playerCenter=player.Center+new Vector2(playerMotion.X-player.position.X,playerMotion.Y-player.position.Y);
#if DEBUG
                ReplayPlayer=playerCenter;
#endif
                if(Vector2.Distance(center,playerCenter)>range*1.3f || Vector2.Distance(path[at],playerCenter)>range-1){
#if DEBUG
                    ReplayStop="Radius";
#endif
                    return null;}
                while(at<path.Count-1 && Vector2.Distance(center,path[at])<=dead)at++;
                if(step==0)input=path[at];velocity=Step(center,velocity,path[at],playerCenter,range,speed,secondary);
                Vector2 old=center;center+=velocity;if(!terrain.ProjectilePassage(old.X,old.Y,center.X,center.Y,shot.width,shot.height)){
#if DEBUG
                    ReplayStop="BallTerrain";
#endif
                    return null;}
                var contact=receive.Allows(step+1)?AttackIntercept.BodyContact(timeline,clock.FirstTick+step,0,input.X,input.Y,center.X,center.Y,shot.width,shot.height,AttackConfidence.Conditional):null;
                if(contact!=null){
#if DEBUG
                    ReplayStop="Contact";
#endif
                    return contact;}
            }
            return null;
        }
        internal static Vector2 Step(Vector2 center,Vector2 velocity,Vector2 point,Vector2 player,float range,float speed,bool secondary)
        {
            float dead=5+speed/2+(secondary?20:0),inertia=Math.Max(1,14-speed/2);
            if(velocity.Length()>speed)velocity*=.98f;
            bool outside=Vector2.Distance(center,player)>range;
            // Native 1.0R..1.3R is a pull-back phase, not automatic return.
            // It doubles available speed and removes inertia, caps desired
            // pull speed at the original top speed, then resumes normal motor.
            if(outside)
            {speed*=2;inertia=1;if((center.X>player.X && velocity.X>0) || (center.X<player.X && velocity.X<0))velocity.X*=.5f;if((center.Y>player.Y && velocity.Y>0) || (center.Y<player.Y && velocity.Y<0))velocity.Y*=.5f;}
            Vector2 delta=point-center;
            if(delta.Length()>dead){float desired=Math.Min(delta.Length()/2,speed);if(outside)desired=Math.Min(desired,speed/2);velocity=(velocity*(inertia-1)+delta.SafeNormalize(Vector2.Zero)*desired)/inertia;}
            else if(!secondary)velocity*=.8f;
            else if(velocity.Length()<speed*.6f)velocity=(velocity*(inertia-1)+velocity.SafeNormalize(Vector2.Zero)*speed*.6f)/inertia;
            if(secondary && !outside && velocity.Length()<speed*.6f)velocity=velocity.SafeNormalize(Vector2.Zero)*speed*.6f;
            return velocity;
        }
    }
}
