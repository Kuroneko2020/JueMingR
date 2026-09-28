using System;

namespace JueMingR.Features.Combat
{
    public struct FacingCandidate
    {
        public int Slot,LifeMax;
        public float X,Y,Width,Height;
    }
    // Default Legacy Nearest reduced to its actual no-aim inputs. No lock,
    // preferred target, line-of-sight, velocity or projectile solver is needed.
    public sealed class FacingSelector
    {
        public const float Range=576;
        private readonly FacingCandidate[] nearest=new FacingCandidate[32];
        private readonly float[] distances=new float[32];
        private readonly int[] finalists=new int[4];
        private int count;
        private float x,y;
        public void Begin(float playerX,float playerY){x=playerX;y=playerY;count=0;}
        public void Consider(FacingCandidate value)
        {
            float d=Distance(x,y,Clamp(x,value.X,value.X+value.Width),Clamp(y,value.Y,value.Y+value.Height));
            if(d>Range)return;
            int at=0;while(at<count && (distances[at]<d || distances[at]==d && nearest[at].Slot<value.Slot))at++;
            if(at==32)return;int last=Math.Min(count,31);
            for(int i=last;i>at;i--){nearest[i]=nearest[i-1];distances[i]=distances[i-1];}
            nearest[at]=value;distances[at]=d;if(count<32)count++;
        }
        public int Finish()
        {
            int n=0;
            for(int i=0;i<count;i++)
            {
                int at=0;while(at<n && BetterCheap(finalists[at],i))at++;
                if(at==4)continue;for(int j=Math.Min(n,3);j>at;j--)finalists[j]=finalists[j-1];finalists[at]=i;if(n<4)n++;
            }
            float best=float.MinValue,bestDistance=float.MaxValue;int slot=-1;
            for(int i=0;i<n;i++)
            {
                var c=nearest[finalists[i]];float cx=c.X+c.Width/2,cy=c.Y+c.Height/2;
                Point(c,cx,cy,180,ref best,ref bestDistance,ref slot);
                Point(c,cx,c.Y+c.Height*.24f,45,ref best,ref bestDistance,ref slot);
                Point(c,cx,c.Y+c.Height*.76f,45,ref best,ref bestDistance,ref slot);
                Point(c,c.X+c.Width*.24f,cy,45,ref best,ref bestDistance,ref slot);
                Point(c,c.X+c.Width*.76f,cy,45,ref best,ref bestDistance,ref slot);
                Point(c,Clamp(x,c.X,c.X+c.Width),Clamp(y,c.Y,c.Y+c.Height),-280,ref best,ref bestDistance,ref slot);
            }
            return slot;
        }
        private bool BetterCheap(int a,int b)
        {
            float first=10000-distances[a]+Threat(nearest[a].LifeMax),second=10000-distances[b]+Threat(nearest[b].LifeMax);
            return first>second || first==second && (distances[a]<distances[b] || distances[a]==distances[b] && nearest[a].Slot<nearest[b].Slot);
        }
        private void Point(FacingCandidate candidate,float px,float py,float bias,ref float best,ref float bestDistance,ref int slot)
        {
            float distance=Distance(x,y,px,py),score=10000-distance+bias;
            if(score>best || Math.Abs(score-best)<=.001f && (distance<bestDistance || Math.Abs(distance-bestDistance)<=.001f && candidate.Slot<slot))
            {best=score;bestDistance=distance;slot=candidate.Slot;}
        }
        private static float Threat(int life){return life>=2000?24:life>=400?10:0;}
        private static float Distance(float x,float y,float px,float py){float dx=x-px,dy=y-py;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static float Clamp(float value,float min,float max){return Math.Max(min,Math.Min(max,value));}
    }
}
