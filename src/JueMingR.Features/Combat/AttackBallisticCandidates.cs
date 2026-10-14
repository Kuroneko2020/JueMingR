using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Only a dry-flight input proposal. Terrain, wind, native birth slots,
    // receivers and derived damage remain owned by the host's exact replay.
    public static class AttackBallisticCandidates
    {
        public static int PreferredWindow(float dx,float dy,float speed,int updates,bool flame,float muzzle,float tolerance)
        {
            double factor=1,horizontal=0,gravity=0,drop=0,c1=Math.Cos(Math.PI/180),s1=Math.Sin(Math.PI/180),c2=Math.Cos(Math.PI/90),s2=Math.Sin(Math.PI/90);
            if(speed<=0 || updates<=0)return 16;
            for(int n=1;n<=120*updates;n++)
            {
                if(flame || n>=15)factor*=.99;horizontal+=factor;
                gravity+=flame?(n>=10?.1:0)+(n>=20?.1:0):n>=15?.2:0;drop+=gravity;
                for(int member=0;member<(flame?5:1);member++)
                {
                    int index=(member+1)/2;double s=flame?speed*.8+index:speed,cos=index==0?1:index==1?c1:c2,sin=(member%2==1?1:-1)*(index==0?0:index==1?s1:s2);
                    double a=muzzle+horizontal*s*cos,b=-horizontal*s*sin,c=n*s*sin,d=muzzle+n*s*cos,det=a*d-b*c;
                    double x=(d*dx-b*(dy-drop))/det,y=(a*(dy-drop)-c*dx)/det,limit=1+tolerance/Math.Min(muzzle+horizontal*s,muzzle+n*s);
                    if(x*x+y*y<=limit*limit)return Math.Min(120,Math.Max(16,(n+updates-1)/updates+8));
                }
            }
            // Insufficient dry geometry is not a physical rejection: wind,
            // terrain bursts or future motion still own progressive search.
            return 16;
        }
        public sealed class Direction
        {
            public readonly float X,Y;
            public readonly int Tick;
            internal readonly double Error;
            internal Direction(double x,double y,int tick,double error){X=(float)x;Y=(float)y;Tick=tick;Error=error;}
        }
        public static Direction[] Generate(NpcTrajectory path,int age,float originX,float originY,float speed,int updates,bool flame,float birthOffset,float bodyHalf)
        {
            if(path==null || speed<=0 || float.IsNaN(speed) || float.IsInfinity(speed) || updates<=0 || age<0 || age+1>=path.Count)return new Direction[0];
            int steps=Math.Min(120,path.Count-age-1),count=steps*updates;
            var horizontal=new double[count+1];var gravity=new double[count+1];var drop=new double[count+1];double factor=1;
            for(int n=1;n<=count;n++)
            {
                if(flame || n>=15)factor*=.99;
                horizontal[n]=horizontal[n-1]+factor;
                gravity[n]=gravity[n-1]+(flame?(n>=10?.1:0)+(n>=20?.1:0):n>=15?.2:0);
                drop[n]=drop[n-1]+gravity[n];
            }
            var proposals=new List<Direction>();
            // The fan can face either way under rotation/reverse gravity;
            // exact Facing decides the real three births later. A blocked
            // 40px muzzle uses the second zero-offset response, never a fake
            // promise that a terrain query succeeded.
            for(int member=0;member<(flame?5:1);member++)
            {
                int index=(member+1)/2;double angle=(member%2==1?1:-1)*index*Math.PI/180;
                double cos=Math.Cos(angle),sin=Math.Sin(angle),memberSpeed=flame?speed*.8+index:speed;
                for(int offset=0;offset<(birthOffset>0?2:1);offset++)
                {
                    double muzzle=offset==0?birthOffset:0;
                    for(int n=1;n<=count;n++)
                    {
                        int tick=age+(n-1)/updates+1;var box=path[tick].ProjectileReceiveBounds;if(!path[tick].CanReceive)continue;
                        double a=muzzle+horizontal[n]*memberSpeed*cos,b=-horizontal[n]*memberSpeed*sin,c=n*memberSpeed*sin,d=muzzle+n*memberSpeed*cos,det=a*d-b*c;if(Math.Abs(det)<.0001)continue;
                        for(int edge=0;edge<5;edge++)
                        {
                            double tx=box.CenterX+(edge==1?-box.Width*.45:edge==2?box.Width*.45:0)-originX;
                            double ty=box.CenterY+(edge==3?-box.Height*.45:edge==4?box.Height*.45:0)-originY-drop[n];
                            double ux=(d*tx-b*ty)/det,uy=(a*ty-c*tx)/det,length=Math.Sqrt(ux*ux+uy*uy);if(length<.01)continue;ux/=length;uy/=length;
                            double vx=memberSpeed*(ux*cos-uy*sin),vy=memberSpeed*(ux*sin+uy*cos);
                            if(flame)while(vx>=16 || vx<=-16 || vy>=16 || vy< -16){vx*=.97;vy*=.97;}
                            // The scalar endpoint includes the native vertical
                            // cap. Monotone accumulated gravity finds its first
                            // capped interval in logarithmic work, not n steps.
                            int last=n;if(vy+gravity[n]>16){int lo=0,hi=n;while(lo<hi){int mid=(lo+hi+1)/2;if(vy+gravity[mid]<=16)lo=mid;else hi=mid-1;}last=lo;}
                            double x=originX+muzzle*ux+horizontal[n]*vx,y=originY+muzzle*uy+last*vy+drop[last]+(n-last)*16;
                            double ex=Math.Max(0,Math.Abs(x-box.CenterX)-box.Width*.5-bodyHalf+1),ey=Math.Max(0,Math.Abs(y-box.CenterY)-box.Height*.5-bodyHalf+1);
                            if(ex!=0 || ey!=0)continue;
                            double error=Math.Abs(x-box.CenterX)+Math.Abs(y-box.CenterY);
                            Add(proposals,ux,uy,tick,error);
                        }
                    }
                }
            }
            proposals.Sort((a,b)=>{int order=a.Tick.CompareTo(b.Tick);return order!=0?order:a.Error.CompareTo(b.Error);});
            // Separate low/high arcs and fan alternatives without spending
            // the exact receiver budget on near-identical rectangle seeds.
            var result=new List<Direction>();foreach(var p in proposals){bool same=false;foreach(var q in result)if(p.X*q.X+p.Y*q.Y>.9998f){same=true;break;}if(!same)result.Add(p);if(result.Count==8)break;}return result.ToArray();
        }
        private static void Add(List<Direction> rows,double x,double y,int tick,double error)
        {
            for(int i=0;i<rows.Count;i++)if(rows[i].X*x+rows[i].Y*y>.99999){if(tick<rows[i].Tick || tick==rows[i].Tick && error<rows[i].Error)rows[i]=new Direction(x,y,tick,error);return;}
            rows.Add(new Direction(x,y,tick,error));
        }
    }
}
