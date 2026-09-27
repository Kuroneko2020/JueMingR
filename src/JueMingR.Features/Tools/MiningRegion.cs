using System;
using System.Collections.Generic;

namespace JueMingR.Features.Tools
{
    public struct MiningPoint
    {
        public int X,Y,Type;
        public MiningPoint(int x,int y,int type){X=x;Y=y;Type=type;}
    }
    public sealed class MiningRegion
    {
        public const int Capacity=512,Radius=80;
        private readonly List<MiningPoint> points=new List<MiningPoint>(Capacity);
        private readonly bool[] visited=new bool[(Radius*2+1)*(Radius*2+1)];
        private int seedX,seedY,seedType;
        public int Count {get{return points.Count;}}
        public bool Truncated {get;private set;}
        public MiningPoint this[int index] {get{return points[index];}}
        public static bool Supported(int type)
        {
            switch(type)
            {
                case 6:case 7:case 8:case 9:case 166:case 167:case 168:case 169:
                case 22:case 37:case 56:case 58:case 204:case 107:case 108:case 111:case 211:case 221:case 222:case 223:
                case 63:case 64:case 65:case 66:case 67:case 68:case 178:case 566:
                case 123:case 224:case 404:case 407:case 408:case 48:case 232:case 745:case 750:return true;
                default:return false;
            }
        }
        public static bool Gem(int type){return type>=63 && type<=68 || type==178 || type==566;}
        public static bool SameGroup(int first,int second){return Supported(first) && Supported(second) && (first==second || Gem(first) && Gem(second));}
        // read returns -1 for unreadable and 0 for empty. A failed read never
        // becomes a removed seed. Callers prove a removed manual seed separately.
        public bool Select(int x,int y,int type,Func<int,int,int> read,bool removedSeed=false)
        {
            if(!Supported(type) || read==null || !removedSeed && !SameGroup(type,read(x,y)))return false;
            Clear();seedX=x;seedY=y;seedType=type;Array.Clear(visited,0,visited.Length);
            if(!removedSeed)Add(x,y,type,x,y,read);
            Probe(x,y,type,x,y,read);
            for(int cursor=0;cursor<points.Count && !Truncated;cursor++)
            {var p=points[cursor];Probe(p.X,p.Y,type,x,y,read);}
            return points.Count!=0;
        }
        private void Probe(int x,int y,int type,int sx,int sy,Func<int,int,int> read)
        {for(int dx=-3;dx<=3 && !Truncated;dx++)for(int dy=-3;dy<=3 && !Truncated;dy++)Add(x+dx,y+dy,type,sx,sy,read);}
        private void Add(int x,int y,int type,int sx,int sy,Func<int,int,int> read)
        {
            int dx=x-sx,dy=y-sy;if(Math.Abs(dx)>Radius || Math.Abs(dy)>Radius)return;
            int key=(dx+Radius)*(Radius*2+1)+dy+Radius;if(visited[key])return;visited[key]=true;
            int current=read(x,y);if(!SameGroup(type,current))return;
            if(points.Count==Capacity){Truncated=true;return;}points.Add(new MiningPoint(x,y,current));
        }
        public bool AddFallen(MiningPoint point)
        {
            if(points.Count==Capacity || Math.Abs(point.X-seedX)>Radius || Math.Abs(point.Y-seedY)>Radius || !SameGroup(seedType,point.Type))return false;
            for(int i=0;i<points.Count;i++)if(points[i].X==point.X && points[i].Y==point.Y)return false;
            points.Add(point);return true;
        }
        public void RemoveAt(int index){points.RemoveAt(index);}
        public void Clear(){points.Clear();Truncated=false;}
        public bool TooFar(int x,int y)
        {
            if(points.Count==0)return false;int left=int.MaxValue,right=int.MinValue,top=int.MaxValue,bottom=int.MinValue;
            foreach(var p in points){left=Math.Min(left,p.X);right=Math.Max(right,p.X);top=Math.Min(top,p.Y);bottom=Math.Max(bottom,p.Y);}
            return Math.Max(Math.Max(left-x,x-right),Math.Max(top-y,y-bottom))>30;
        }
    }
}
