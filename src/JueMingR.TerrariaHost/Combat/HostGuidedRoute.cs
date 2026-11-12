using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum HostGuidedRouteStatus { Unknown,Ready,Blocked }
    // Geometry proposes a strict whole-body corridor. Unlike AI99, AI009
    // cannot borrow a centre-only wall edge and call its clipped motion safe.
    internal sealed class HostGuidedRoute
    {
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private readonly List<Vector2> points=new List<Vector2>();
        private readonly List<MotionRect> barriers=new List<MotionRect>();
        private readonly HashSet<long> barrierCells=new HashSet<long>();
        private PredictionTerrain.CellLease lease;
        private NpcIdentity target;
        private int width,height,type;
        private Vector2 offset;
        private bool incomplete;
        internal HostGuidedRouteStatus Status {get;private set;}
        internal bool LastClearUnknown {get;private set;}
        internal int Searches {get;private set;}
        internal int Reuses {get;private set;}
        internal int BlockRejections {get;private set;}
        internal IReadOnlyList<Vector2> Points {get{return points;}}
        internal PredictionTerrain.CellLease Lease {get{return lease;}}
        internal bool Clear(Vector2 from,Vector2 to)
        {
            LastClearUnknown=false;
            // A long goal ray is tried from many nearby A* vertices. Once an
            // actual native segment hit proves a full/half solid tile, reject
            // later rays through that SAME complete body obstacle cheaply.
            // This only rejects; accepted edges still use native geometry.
            for(int at=0;at<barriers.Count;at++)
            {
                if((at&31)==0)CombatPredictionWork.CheckGeometry();
                if(Crosses(from+offset,to+offset,barriers[at],width,height)){BlockRejections++;return false;}
            }
            int count=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(from,to)/8));var previous=from+offset;
            for(int i=1;i<=count;i++)
            {
                var next=Vector2.Lerp(from,to,(float)i/count)+offset;
                float x,y;
                if(!terrain.ProjectileCollision(previous.X,previous.Y,next.X-previous.X,next.Y-previous.Y,width,height,out x,out y)){LastClearUnknown=incomplete=true;return false;}
                if(Math.Abs(x-(next.X-previous.X))>.0001f || Math.Abs(y-(next.Y-previous.Y))>.0001f){RememberBarrier(previous,next);return false;}
                previous=next;
            }
            return true;
        }
        private void RememberBarrier(Vector2 from,Vector2 to)
        {
            int left=(int)Math.Floor((Math.Min(from.X,to.X)-width*.5f)/16),right=(int)Math.Floor((Math.Max(from.X,to.X)+width*.5f)/16);
            int top=(int)Math.Floor((Math.Min(from.Y,to.Y)-height*.5f)/16),bottom=(int)Math.Floor((Math.Max(from.Y,to.Y)+height*.5f)/16);
            for(int x=left;x<=right;x++)for(int y=top;y<=bottom;y++)
            {
                long key=((long)x<<32)|(uint)y;if(barrierCells.Contains(key))continue;PredictionTile tile;PredictionStop stop;
                if(!terrain.Tile(x,y,out tile,out stop) || !tile.Active || !tile.Solid || tile.SolidTop || tile.Slope!=0)continue;
                var box=new MotionRect(x*16,y*16+(tile.Half?8:0),16,tile.Half?8:16);
                if(!Crosses(from,to,box,width,height))continue;barrierCells.Add(key);barriers.Add(box);
            }
        }
        private static bool Crosses(Vector2 from,Vector2 to,MotionRect box,int width,int height)
        {
            float enter=0,exit=1;
            return Slab(from.X,to.X-from.X,box.X-width*.5f,box.X+box.Width+width*.5f,ref enter,ref exit) &&
                Slab(from.Y,to.Y-from.Y,box.Y-height*.5f,box.Y+box.Height+height*.5f,ref enter,ref exit) && enter<exit && exit>0 && enter<1;
        }
        private static bool Slab(float value,float delta,float low,float high,ref float enter,ref float exit)
        {
            if(delta==0)return value>low && value<high;
            float a=(low-value)/delta,b=(high-value)/delta;if(a>b){float swap=a;a=b;b=swap;}
            enter=Math.Max(enter,a);exit=Math.Min(exit,b);return enter<exit;
        }
        internal bool Prepare(Player player,Projectile shot,NpcIdentity identity,MotionRect receiver)
        {
            Status=HostGuidedRouteStatus.Unknown;incomplete=false;barriers.Clear();barrierCells.Clear();
            Vector2 anchor;int w,h;shot.GetCollisionParams(out anchor,out w,out h);
            var shift=new Vector2(w*(.5f-anchor.X),h*(.5f-anchor.Y));
            bool reuse=type==shot.type && width==w && height==h && offset==shift && target.Equals(identity) && lease!=null && lease.Current && points.Count>0;
            width=w;height=h;offset=shift;type=shot.type;target=identity;terrain.Reset();
            var goal=new Vector2(receiver.CenterX,receiver.CenterY);
            if(reuse)
            {
                // Ordinary target/player movement does not restart a search.
                // Reconnect from the real body and replace only the terminal.
                int first=points.Count-1;while(first>=0 && !Clear(shot.Center,points[first]))first--;
                if(first>=0)
                {
                    if(first>0)points.RemoveRange(0,first);
                    Vector2 prior=points.Count>1?points[points.Count-2]:shot.Center;
                    if(Reachable(player,goal) && OccupiedFree(goal) && Clear(prior,goal))points[points.Count-1]=goal;
                    else if(!Touches(points[points.Count-1],receiver,shot))reuse=false;
                    foreach(var point in points)if(!Reachable(player,point)){reuse=false;break;}
                    if(reuse)
                    {
                        var previous=shot.Center;
                        foreach(var point in points){if(!Clear(previous,point)){reuse=false;break;}previous=point;}
                        if(reuse){Reuses++;Status=HostGuidedRouteStatus.Ready;return true;}
                    }
                }
            }
            points.Clear();Searches++;
            // The unsnapped real start is a separate vertex. The lattice is
            // only a proposal representation, never a smaller damage body.
            var nodes=new List<Node>(256){new Node(shot.Center,-1,0)};
            var open=new List<Open>{new Open(0,Vector2.Distance(shot.Center,goal))};
            var seen=new Dictionary<long,int>();
            int sx=(int)Math.Floor(shot.Center.X/16),sy=(int)Math.Floor(shot.Center.Y/16);
            for(int expanded=0;open.Count>0 && expanded<512;expanded++)
            {
                CombatPredictionWork.Check();var next=Pop(open);var node=nodes[next.Index];if(node.Closed)continue;node.Closed=true;
                Vector2 terminal;
                if(Goal(node.Point,receiver,shot,player,out terminal))
                {
                    points.Add(terminal);for(int at=next.Index;at>0;at=nodes[at].Parent)points.Add(nodes[at].Point);
                    points.Reverse();Compress(shot.Center);lease=terrain.FreezeCells();Status=HostGuidedRouteStatus.Ready;return true;
                }
                int x=next.Index==0?sx:(int)Math.Floor(node.Point.X/16),y=next.Index==0?sy:(int)Math.Floor(node.Point.Y/16);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                {
                    if(dx==0 && dy==0 && next.Index!=0)continue;
                    int nx=x+dx,ny=y+dy;long key=((long)nx<<32)|(uint)ny;var point=new Vector2(nx*16+8,ny*16+8);
                    if(point==node.Point || !Reachable(player,point))continue;
                    float cost=node.Cost+Vector2.Distance(node.Point,point);int index;
                    if(seen.TryGetValue(key,out index) && (nodes[index].Closed || nodes[index].Cost<=cost))continue;
                    if(!OccupiedFree(point) || !Clear(node.Point,point))continue;
                    if(!seen.TryGetValue(key,out index))
                    {if(nodes.Count>=1024){incomplete=true;continue;}index=nodes.Count;seen.Add(key,index);nodes.Add(new Node(point,next.Index,cost));}
                    else{nodes[index].Parent=next.Index;nodes[index].Cost=cost;}
                    Push(open,new Open(index,cost+Vector2.Distance(point,goal)));
                }
            }
            // A bounded or interrupted query is not a cached physical no-path.
            // A subsequent preparation may retry from the then-current body.
            Status=open.Count==0 && !incomplete?HostGuidedRouteStatus.Blocked:HostGuidedRouteStatus.Unknown;lease=null;return false;
        }
        private bool Goal(Vector2 from,MotionRect receiver,Projectile shot,Player player,out Vector2 terminal)
        {
            terminal=new Vector2(receiver.CenterX,receiver.CenterY);
            if(Reachable(player,terminal) && OccupiedFree(terminal) && Clear(from,terminal))return true;
            if(Touches(from,receiver,shot)){terminal=from;return true;}
            // Receiver edges are legal geometric destinations too. A native
            // integer Damage contact, not this overlap test, remains the proof.
            terminal=new Vector2(MathHelper.Clamp(from.X,receiver.X-shot.width*.5f+1,receiver.X+receiver.Width+shot.width*.5f-1),MathHelper.Clamp(from.Y,receiver.Y-shot.height*.5f+1,receiver.Y+receiver.Height+shot.height*.5f-1));
            return Reachable(player,terminal) && OccupiedFree(terminal) && Clear(from,terminal);
        }
        private bool OccupiedFree(Vector2 point)
        {bool solid;PredictionStop stop;if(!terrain.Solid(new MotionRect(point.X+offset.X-width*.5f,point.Y+offset.Y-height*.5f,width,height),out solid,out stop)){incomplete=true;return false;}return !solid;}
        private static bool Touches(Vector2 point,MotionRect box,Projectile shot)
        {return point.X>box.X-shot.width*.5f && point.X<box.X+box.Width+shot.width*.5f && point.Y>box.Y-shot.height*.5f && point.Y<box.Y+box.Height+shot.height*.5f;}
        private static bool Reachable(Player player,Vector2 point)
        {var limited=point;player.LimitPointToPlayerReachableArea(ref limited);return Vector2.DistanceSquared(limited,point)<.01f;}
        private void Compress(Vector2 start)
        {
            var result=new List<Vector2>();for(int at=0;at<points.Count;)
            {int end=points.Count-1;while(end>at && !Clear(start,points[end]))end--;result.Add(points[end]);start=points[end];at=end+1;}
            points.Clear();points.AddRange(result);
        }
        internal void Freeze(){lease=terrain.FreezeCells();}
        private sealed class Node
        {internal Vector2 Point;internal int Parent;internal float Cost;internal bool Closed;internal Node(Vector2 point,int parent,float cost){Point=point;Parent=parent;Cost=cost;}}
        private readonly struct Open
        {internal readonly int Index;internal readonly float Score;internal Open(int index,float score){Index=index;Score=score;}}
        private static void Push(List<Open> heap,Open value)
        {int at=heap.Count;heap.Add(value);while(at>0){int parent=(at-1)/2;if(heap[parent].Score<=value.Score)break;heap[at]=heap[parent];at=parent;}heap[at]=value;}
        private static Open Pop(List<Open> heap)
        {
            var value=heap[0];var last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);int at=0;
            while(at*2+1<heap.Count){int child=at*2+1;if(child+1<heap.Count && heap[child+1].Score<heap[child].Score)child++;if(last.Score<=heap[child].Score)break;heap[at]=heap[child];at=child;}
            if(heap.Count>0)heap[at]=last;return value;
        }
    }
}
