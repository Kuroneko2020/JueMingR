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
        private PredictionPlayerMotion preparedPlayer;
        private MotionRect? contactGoal;
        private readonly List<Vector2> contactPoints=new List<Vector2>(6);
        private bool motorRoute;
        private float preparedMeleeSpeed;private bool preparedString,preparedUsable;
        private uint progressStep,retryStep;
        internal int Searches {get;private set;}
        // This navigation owns terrain/player dependencies separate from the
        // controller's launch/beam terrain. Consumption only validates them;
        // it must not turn Draw or an AI callback into another route search.
        internal bool Current(Player player)
        {return terrain.Unchanged && PresentationCurrent(player);}
        internal sealed class PresentationDependencies
        {
            private readonly PredictionTerrain.CellLease terrain;
            private readonly PredictionPlayerMotion player;
            private readonly float meleeSpeed;private readonly bool yoyoString,usable;
            internal PresentationDependencies(HostYoyoNavigation source,Player openingInput)
            {terrain=source.terrain.FreezeCells();player=openingInput==null?source.preparedPlayer:NpcPredictionSource.ReadPlayer(openingInput);meleeSpeed=source.preparedMeleeSpeed;yoyoString=source.preparedString;usable=source.preparedUsable;}
            // These scalars belong to this replay, not to a navigation owner
            // which another preparation may overwrite later in the same tick.
            internal bool Current(Player current)
            {return usable && meleeSpeed==current.meleeSpeed && yoyoString==current.yoyoString && player.SameSample(NpcPredictionSource.ReadPlayer(current)) && terrain.Current;}
        }
        // A hypothetical opening replays a private future Player. Its token
        // and future pose are not the live input premise; bind the original
        // holder sample separately, while retaining the actual replay terrain.
        internal PresentationDependencies FreezePresentation(Player openingInput=null){return new PresentationDependencies(this,openingInput);}
        internal bool PresentationCurrent(Player player)
        {return preparedUsable && preparedMeleeSpeed==player.meleeSpeed && preparedString==player.yoyoString && preparedPlayer.SameSample(NpcPredictionSource.ReadPlayer(player));}
#if DEBUG
        internal static int GroupScans;
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
            int safe=OwnRemaining(player,shot,secondary);
            // Any non-detached same-owner AI99 ball can trigger native recall
            // of the whole owner group, including a ball not registered here.
            // Read that existing group once per preparation, not per replay step.
            for(int slot=0;slot<Main.maxProjectiles && safe>0;slot++)
            {var other=Main.projectile[slot];if(other!=null && other.active && !ReferenceEquals(other,shot) && other.owner==shot.owner && Handles(other) && other.ai[0]>=-1)safe=Math.Min(safe,OwnRemaining(player,other,Secondary(other)));}
            return safe;
        }
        // One definite controller preparation owns these facts. No GUC cache:
        // a native birth/recall in the same update must capture a new group.
        internal sealed class GroupFacts
        {
            private readonly Dictionary<Projectile,bool> secondaries=new Dictionary<Projectile,bool>();
            private readonly Dictionary<Projectile,int> remaining=new Dictionary<Projectile,int>();
            internal GroupFacts(Player player)
            {
#if DEBUG
                GroupScans++;
#endif
                var prior=new HashSet<int>();var members=new List<Projectile>();int safe=120;
                for(int i=0;i<Main.maxProjectiles;i++)
                {
                    var shot=Main.projectile[i];if(shot==null || !shot.active || shot.owner!=player.whoAmI)continue;
                    bool secondary=prior.Contains(shot.type);
                    if(Handles(shot)){secondaries[shot]=secondary;remaining[shot]=OwnRemaining(player,shot,secondary);if(shot.ai[0]>=-1){members.Add(shot);safe=Math.Min(safe,remaining[shot]);}}
                    // Secondary includes same-type earlier AI>=-1 even when
                    // that projectile is not an observed navigation member.
                    if(shot.ai[0]>=-1)prior.Add(shot.type);
                }
                foreach(var shot in members)remaining[shot]=Math.Min(remaining[shot],safe);
            }
            internal bool Secondary(Projectile shot){bool value;return secondaries.TryGetValue(shot,out value)?value:HostYoyoNavigation.Secondary(shot);}
            internal int Remaining(Player player,Projectile shot,bool secondary){int value;return remaining.TryGetValue(shot,out value)?value:HostYoyoNavigation.Remaining(player,shot,secondary);}
        }
        private static int OwnRemaining(Player player,Projectile shot,bool secondary)
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
        internal static bool BasicPoint(Player player,Item item,Projectile shot,Vector2 target,PredictionTerrain terrain,out Vector2 point)
        {
            point=Vector2.Zero;
            bool live=shot!=null;
            if(live && (!Handles(shot) || !player.channel || shot.ai[0]<0 || OwnRemaining(player,shot,Secondary(shot))==0))return false;
            float range,speed;Parameters(player,live?shot.type:item.shoot,false,out range,out speed);
            if(range<=1 || speed<=0 || live && Vector2.Distance(shot.Center,player.Center)>range*1.3f)return false;
            var offset=target-player.Center;
            if(offset.Length()>range-1)target=player.Center+offset.SafeNormalize(Vector2.UnitX*player.direction)*(range-1);
            // No search or future replay belongs in a cursor consumer. Native
            // TileCollision examines the start's neighbours, not a long ray,
            // and mutates shared collision flags. Use existing pure body
            // passage in short adjacent segments, bounded by native line
            // length (and 128 segments), stopping at its first blocked step.
            // Existing valid navigation prefixes are chosen before fallback.
            var center=live?shot.Center:player.RotatedRelativePoint(player.MountedCenter);
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(live?shot.type:item.shoot,out sample))return false;
            int width=live?shot.width:sample.width,height=live?shot.height:sample.height;
            var displacement=target-center;float length=displacement.Length();var unit=displacement.SafeNormalize(Vector2.Zero);
            terrain.Reset();point=center;
            for(int step=0;step<128 && length>0;step++)
            {
                float distance=Math.Min(8,length);var next=point+unit*distance;
                if(!terrain.ProjectilePassage(point.X,point.Y,next.X,next.Y,width,height))break;
                point=next;length-=distance;
            }
            return true;
        }
        internal static bool OpeningPoint(Player player,Item item,NpcTrajectory timeline,out Vector2 point,out HostYoyoNavigation navigation)
        {
            navigation=new HostYoyoNavigation();var target=timeline[0].ProjectileReceiveBounds;Vector2 goal;Projectile sample;point=Vector2.Zero;
            if(!ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out sample))return false;
            float range,speed;Parameters(player,item.shoot,false,out range,out speed);navigation.terrain.Reset();
            // The real launch must face the first gap, otherwise its native
            // initial 16px velocity can hit the wall before steering can turn.
            // This opening point carries no Contact or damage guarantee.
            if(Goal(target,player.Center,range,out goal) && navigation.FindContact(player.RotatedRelativePoint(player.MountedCenter),target,player.Center,range,sample.width,sample.height))
            {
                var start=player.RotatedRelativePoint(player.MountedCenter);float launch=item.shootSpeed;
                if(item.melee && !ProjectileID.Sets.NoMeleeSpeedVelocityScaling[item.shoot])launch/=player.meleeSpeed;
                navigation.MotorShortcut(start,(navigation.path[0]-start).SafeNormalize(Vector2.UnitX*player.direction)*launch,player.Center,range,speed,false,sample.width,sample.height,120,true,launch);
                point=navigation.path[0];navigation.preparedPlayer=NpcPredictionSource.ReadPlayer(player);navigation.preparedMeleeSpeed=player.meleeSpeed;navigation.preparedString=player.yoyoString;navigation.preparedUsable=true;return true;
            }
            return false;
        }
        private static bool Goal(MotionRect box,Vector2 player,float range,out Vector2 point)
        {
            point=new Vector2(box.CenterX,box.CenterY);if(Vector2.Distance(point,player)<=range)return true;
            // A large receive shape can have a reachable edge while its centre
            // is outside. Choose a point strictly inside that existing shape;
            // do not enlarge the input radius or the native damage rectangle.
            float insetX=Math.Min(1,box.Width/4),insetY=Math.Min(1,box.Height/4);
            point=new Vector2(MathHelper.Clamp(player.X,box.X+insetX,box.X+box.Width-insetX),MathHelper.Clamp(player.Y,box.Y+insetY,box.Y+box.Height-insetY));
            // This is only a stable reuse anchor. Body-contact reachability is
            // decided by FindContact with the actual ball dimensions below.
            return true;
        }
        internal AttackContact Prepare(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,out Vector2 point,out bool usable)
        {return PrepareWithPlayer(player,shot,timeline,clock,out point,out usable,HostAttackPlayerFrame.Capture(player,timeline));}
        internal AttackContact PrepareWithPlayer(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,out Vector2 point,out bool usable,HostAttackPlayerFrame playerFrame,GroupFacts group=null)
        {
            preparedUsable=false;int age=clock.Age;point=Vector2.Zero;usable=false;if(!player.channel || player.CCed || shot.ai[0]<0)return null;
            bool secondary=group==null?Secondary(shot):group.Secondary(shot);float range,speed;Parameters(player,shot,secondary,out range,out speed);
            int remaining=group==null?Remaining(player,shot,secondary):group.Remaining(player,shot,secondary);if(remaining==0){path.Clear();return null;}
            if(range<=1 || speed<=0 || Vector2.Distance(shot.Center,player.Center)>range*1.3f)return null;
            var bounds=timeline[Math.Min(timeline.Count-1,age+Math.Max(1,(int)(Vector2.Distance(shot.Center,new Vector2(timeline[age].Bounds.CenterX,timeline[age].Bounds.CenterY))/speed)))].ProjectileReceiveBounds;
            Vector2 goal;if(!Goal(bounds,player.Center,range,out goal))return null;
            bool reuse=path.Count>0 && terrain.Unchanged && Vector2.DistanceSquared(playerAnchor,player.Center)<64 && Vector2.DistanceSquared(targetAnchor,goal)<144;
            if(reuse && Vector2.DistanceSquared(progressAnchor,shot.Center)>4){progressAnchor=shot.Center;progressStep=Main.GameUpdateCount;}
            if(reuse && Main.GameUpdateCount-progressStep>14){reuse=false;path.Clear();retryStep=Main.GameUpdateCount+5;}
            if(!reuse)
            {
                path.Clear();if(Main.GameUpdateCount<retryStep)return null;
                terrain.Reset();Searches++;if(!FindContact(shot.Center,bounds,player.Center,range,shot.width,shot.height)){retryStep=Main.GameUpdateCount+5;return null;}
                MotorShortcut(shot.Center,shot.velocity,player.Center,range,speed,secondary,shot.width,shot.height,remaining);
                playerAnchor=player.Center;targetAnchor=goal;progressAnchor=shot.Center;progressStep=Main.GameUpdateCount;
            }
            // Replayed steering may fail a geometric route because a fast ball
            // cannot make its corner. Rejection restores the physical cursor.
            float dead=5+speed/2+(secondary?20:0);
            while(path.Count>1 && Vector2.Distance(shot.Center,path[0])<=dead && (!motorRoute || Clear(shot.Center,path[1],shot.width,shot.height)))path.RemoveAt(0);
            var contact=Replay(player,shot,timeline,clock,secondary,range,speed,remaining,out point,out usable,playerFrame);
            if(usable){preparedPlayer=NpcPredictionSource.ReadPlayer(player);preparedMeleeSpeed=player.meleeSpeed;preparedString=player.yoyoString;preparedUsable=true;}
            if(!usable){path.Clear();retryStep=Main.GameUpdateCount+5;}
            return contact;
        }
        private bool Clear(Vector2 a,Vector2 b,int width,int height)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(a,b)/8));Vector2 old=a;
            for(int i=1;i<=steps;i++){var next=Vector2.Lerp(a,b,(float)i/steps);if(!terrain.ProjectilePassage(old.X,old.Y,next.X,next.Y,width,height))return false;old=next;}return true;
        }
        private bool RouteEdge(Vector2 a,Vector2 b,int width,int height)
        {
            bool bodyClear=Clear(a,b,width,height);
            if(!contactGoal.HasValue)return bodyClear;
            if(!bodyClear && Math.Abs(a.X-b.X)>.001f && Math.Abs(a.Y-b.Y)>.001f)return false;
            // A corridor is a steering proposal, never a smaller projectile or
            // a body-clear proof. Native AI99 can clip X into a corner while
            // retaining Y, then leave that temporary edge overlap next action.
            // The centre must still stay in air: a solid sealed wall does not
            // become a corridor because an embedded contact returned zero.
            int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(a,b)/8));
            for(int i=0;i<=steps;i++)
            {
                var p=Vector2.Lerp(a,b,(float)i/steps);PredictionTile tile;PredictionStop stop;
                if(!terrain.Tile((int)Math.Floor(p.X/16),(int)Math.Floor(p.Y/16),out tile,out stop))return false;
                if(!tile.Active || !tile.Solid || tile.SolidTop)continue;
                float x=p.X%16,y=p.Y%16;
                if(tile.Half && y<8 || tile.Slope==1 && y<x || tile.Slope==2 && y<16-x || tile.Slope==3 && y>16-x || tile.Slope==4 && y>x)continue;
                return false;
            }
            return true;
        }
        private void MotorShortcut(Vector2 start,Vector2 velocity,Vector2 player,float range,float speed,bool secondary,int width,int height,int remaining,bool opening=false,float launch=0)
        {
            if(!motorRoute || path.Count<2)return;
            int last=path.Count-1;
            for(int trial=0;trial<4;trial++)
            {
                int candidate=trial==0?last:Math.Min(last-1,3-trial);
                if(candidate<=0)continue;
                var center=start;var v=opening?(path[candidate]-start).SafeNormalize(Vector2.UnitX)*launch:velocity;
                for(int step=0;step<remaining;step++)
                {
                    if((step&15)==0)CombatPredictionWork.Check();
                    v=Step(center,v,path[candidate],player,range,speed,secondary);
                    float x,y;if(!terrain.ProjectileCollision(center.X,center.Y,v.X,v.Y,width,height,out x,out y))break;
                    v=new Vector2(x,y);center+=v;
                    if(Vector2.DistanceSquared(center,player)>range*range*1.69f)break;
                    if(Vector2.Distance(center,path[candidate])<=5+speed/2+(secondary?20:0) && (candidate==last || Clear(center,path[candidate+1],width,height)))
                    {path.RemoveRange(0,candidate);return;}
                }
            }
        }
        private bool FindContact(Vector2 start,MotionRect box,Vector2 player,float range,int width,int height)
        {
            // Preserve an already legal native centre-directed input. Region
            // alternatives are needed only when that old endpoint/ray fails;
            // choosing a nearer grazing point on every ordinary open route
            // changes the native motor and needlessly captures six endpoints.
            var direct=new Vector2(box.CenterX,box.CenterY);bool occupied;PredictionStop directStop;
            if(Vector2.DistanceSquared(direct,player)<=range*range && terrain.Solid(new MotionRect(direct.X-width*.5f,direct.Y-height*.5f,width,height),out occupied,out directStop) && !occupied && Clear(start,direct,width,height))
            {path.Add(direct);motorRoute=false;return true;}
            float left=box.X-width*.5f+1,right=box.X+box.Width+width*.5f-1;
            float top=box.Y-height*.5f+1,bottom=box.Y+box.Height+height*.5f-1;
            var nearest=new Vector2(MathHelper.Clamp(player.X,left,right),MathHelper.Clamp(player.Y,top,bottom));
            if(Vector2.DistanceSquared(nearest,player)>range*range)return false;
            // Candidate occupancy is independent of the BFS vertex. Capture
            // it once, then try only the closest valid endpoint per expansion.
            // Nodes which already contact the receiver remain goals themselves;
            // a blocked endpoint never launches a second whole search.
            contactPoints.Clear();
            for(int i=0;i<6;i++)
            {
                var near=i==0?new Vector2(box.CenterX,box.CenterY):i==5?player:new Vector2((float)Math.Floor((box.CenterX-8)/16)*16+8+((i-1)&1)*16,(float)Math.Floor((box.CenterY-8)/16)*16+8+((i-1)>>1)*16);
                var candidate=new Vector2(MathHelper.Clamp(near.X,left,right),MathHelper.Clamp(near.Y,top,bottom));
                if(Vector2.DistanceSquared(candidate,player)>range*range || contactPoints.Contains(candidate))continue;
                bool solid;PredictionStop stop;
                // Zero-velocity TileCollision does not prove occupancy. The
                // existing pure Solid query leases the complete actual body.
                if(terrain.Solid(new MotionRect(candidate.X-width*.5f,candidate.Y-height*.5f,width,height),out solid,out stop) && !solid)contactPoints.Add(candidate);
            }
            contactGoal=box;
            try
            {
                bool found=Find(start,new Vector2(box.CenterX,box.CenterY),player,range,width,height);motorRoute=false;
                var previous=start;foreach(var next in path){if(!Clear(previous,next,width,height))motorRoute=true;previous=next;}
                return found;
            }
            finally{contactGoal=null;contactPoints.Clear();}
        }
        private bool ContactGoal(Vector2 from,MotionRect box,Vector2 player,float range,int width,int height,out Vector2 goal)
        {
            // Expanded centres select geometric proposals only. Native Damage
            // still uses the untouched integer receiver/projectile rectangles.
            if(from.X>=box.X-width*.5f+1 && from.X<=box.X+box.Width+width*.5f-1 && from.Y>=box.Y-height*.5f+1 && from.Y<=box.Y+box.Height+height*.5f-1 && Vector2.DistanceSquared(from,player)<=range*range)
            {
                bool solid;PredictionStop stop;
                if(terrain.Solid(new MotionRect(from.X-width*.5f,from.Y-height*.5f,width,height),out solid,out stop) && !solid){goal=from;return true;}
            }
            int nearest=-1;float distance=float.MaxValue;
            for(int i=0;i<contactPoints.Count;i++){float candidate=Vector2.DistanceSquared(from,contactPoints[i]);if(candidate<distance){distance=candidate;nearest=i;}}
            goal=nearest<0?Vector2.Zero:contactPoints[nearest];
            return nearest>=0 && Clear(from,goal,width,height) && RouteEdge(from,goal,width,height);
        }
        private bool Find(Vector2 start,Vector2 goal,Vector2 player,float range,int width,int height)
        {
            var contact=contactGoal;
            Vector2 reached=Vector2.Zero;
            if(contact.HasValue?ContactGoal(start,contact.Value,player,range,width,height,out reached):Clear(start,goal,width,height)){path.Add(contact.HasValue?reached:goal);return true;}
            // Tile centres represent steering corridors, independent of body
            // scale. The actual
            // start remains its own node: every connection into this lattice
            // still checks the full native ball, including its first edge.
            // Keep the original 1024 acquired nodes / 512 expansion bounds.
            const float offsetX=8,offsetY=8;
            int startX=(int)Math.Floor(start.X/16),startY=(int)Math.Floor(start.Y/16);
            var nodes=new List<Vector2>{start};var previous=new List<int>{-1};var xs=new List<int>{startX};var ys=new List<int>{startY};var seen=new HashSet<long>();
            if(start==new Vector2(startX*16+offsetX,startY*16+offsetY))seen.Add(((long)startX<<32)|(uint)startY);
            for(int at=0;at<nodes.Count && at<512;at++)
            {
                if(contact.HasValue?ContactGoal(nodes[at],contact.Value,player,range,width,height,out reached):Clear(nodes[at],goal,width,height))
                {path.Add(contact.HasValue?reached:goal);for(int i=at;i>0;i=previous[i])path.Add(nodes[i]);path.Reverse();Compress(start,width,height);return true;}
                // Equal-depth routes prefer progress toward the goal. The old
                // fixed -1-first order chose a backwards launch for a forward
                // opening before an equally short forward entrance was tried.
                int sx=goal.X>=nodes[at].X?1:-1,sy=goal.Y>=nodes[at].Y?1:-1;
                for(int ix=0;ix<3;ix++)for(int iy=0;iy<3;iy++)
                {
                    int dx=ix==0?sx:ix==1?0:-sx,dy=iy==0?0:iy==1?sy:-sy;
                    // Only the unsnapped start may connect to the centre of
                    // its own tile; ordinary lattice nodes have eight edges.
                    if(dx==0 && dy==0 && at!=0)continue;int x=xs[at]+dx,y=ys[at]+dy;long key=((long)x<<32)|(uint)y;
                    if(seen.Contains(key) || nodes.Count>=1024)continue;var next=new Vector2(x*16+offsetX,y*16+offsetY);
                    if(next==nodes[at])continue;
                    if(Vector2.Distance(next,player)>(contact.HasValue?range:range-1) || !RouteEdge(nodes[at],next,width,height))continue;
                    // A blocked incoming edge says nothing about another
                    // approach to this vertex. Visit only acquired nodes;
                    // accepted vertices still join the same finite BFS once.
                    seen.Add(key);
                    nodes.Add(next);previous.Add(at);xs.Add(x);ys.Add(y);
                }
            }
            return false;
        }
        private void Compress(Vector2 start,int width,int height)
        {
            // BFS's first grid vertex may point backwards even when a later
            // entrance is visible. Borrow the furthest whole-ball-clear vertex
            // on each segment, then still reject any motor/inertia collision.
            var compressed=new List<Vector2>();int at=0;
            while(at<path.Count){int next=path.Count-1;while(next>at && !Clear(start,path[next],width,height))next--;compressed.Add(path[next]);start=path[next];at=next+1;}
            path.Clear();path.AddRange(compressed);
        }
        private AttackContact Replay(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,bool secondary,float range,float speed,int remaining,out Vector2 input,out bool usable,HostAttackPlayerFrame playerFrame)
        {
#if DEBUG
            ReplayStop="NoContact";ReplayStep=0;
#endif
            usable=false;int age=clock.Age,at=0;float dead=5+speed/2+(secondary?20:0);Vector2 center=shot.Center,velocity=shot.velocity;input=path[0];var receive=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var playerMotion=playerFrame.Motion;var environment=playerFrame.Environment;
            for(int step=0;step<Math.Min(remaining,timeline.Count-age-1);step++)
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
                if(Vector2.Distance(center,playerCenter)>range*1.3f || Vector2.Distance(path[at],playerCenter)>range){
#if DEBUG
                    ReplayStop="Radius";
#endif
                    return null;}
                while(at<path.Count-1 && Vector2.Distance(center,path[at])<=dead && (!motorRoute || Clear(center,path[at+1],shot.width,shot.height)))at++;
                if(step==0)input=path[at];velocity=Step(center,velocity,path[at],playerCenter,range,speed,secondary);
                if(!HostProjectileEnvironment.Dry(shot,terrain,center.X,center.Y,velocity.X))return null;
                // AI99 primary/paired balls keep TileCollision's clipped
                // velocity and then move; hitting a wall does not kill or
                // bounce them. Replaying that slide is essential at a narrow
                // entrance. Geometric edges still require the full ball; this
                // does not make a blocked route traversable or shrink it.
                float vx,vy;if(!terrain.ProjectileCollision(center.X,center.Y,velocity.X,velocity.Y,shot.width,shot.height,out vx,out vy)){
#if DEBUG
                    ReplayStop="TerrainUnavailable";
#endif
                    return null;}
                velocity=new Vector2(vx,vy);center+=velocity;
                // A verified short motor prefix can guide this natural AI
                // without claiming final arrival. A later uncertain corner
                // removes Contact, not that bounded input capability; actual
                // progress/terrain changes still revise or retire the route.
                if(step>=2)usable=true;
                var contact=receive.Allows(clock.LaunchDelay+step+1)?AttackIntercept.BodyContact(timeline,clock.FirstTick+step,0,input.X,input.Y,center.X,center.Y,shot.width,shot.height,AttackConfidence.Conditional):null;
                if(contact!=null){
#if DEBUG
                    ReplayStop="Contact";
#endif
                    usable=true;return contact;}
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
