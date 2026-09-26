namespace JueMingR.Features.Tools
{
    public struct ReplantPoint
    {
        public int X,Y,Style;
        public ulong Created;
    }
    public sealed class ReplantQueue
    {
        public const int Capacity=48,Lifetime=600;
        private readonly ReplantPoint[] points=new ReplantPoint[Capacity];
        private int count,cursor;
        public int Count {get{return count;}}
        public ReplantPoint this[int index] {get{return points[index];}}
        // Repeated aim does not renew intent. Only a later, real harvest after
        // retirement can create a new lifetime for the same coordinate.
        public bool Add(int x,int y,int style,ulong tick)
        {
            if(style<0 || style>6)return false;
            for(int i=0;i<count;i++)if(points[i].X==x && points[i].Y==y)return false;
            if(count==Capacity)return false;points[count++]=new ReplantPoint{X=x,Y=y,Style=style,Created=tick};return true;
        }
        public void Expire(ulong tick){for(int i=count-1;i>=0;i--)if(tick-points[i].Created>=Lifetime)RemoveAt(i);}
        public int Next(){if(count==0)return -1;int result=cursor%count;cursor=(result+1)%count;return result;}
        public void RemoveAt(int index){for(int i=index+1;i<count;i++)points[i-1]=points[i];if(count>0)points[--count]=default(ReplantPoint);if(count==0)cursor=0;else cursor%=count;}
        public void Clear(){System.Array.Clear(points,0,points.Length);count=cursor=0;}
    }
}
