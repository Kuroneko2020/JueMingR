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
            public readonly bool IsCenter;
            internal readonly double Error;
            internal Direction(double x,double y,int tick,double error,bool isCenter=false){X=(float)x;Y=(float)y;Tick=tick;Error=error;IsCenter=isCenter;}
        }
        // A preparation owns this dry response. Extension only appends whole
        // subupdates; it carries no trajectory, scene or execution permission.
        public sealed class Response
        {
            internal readonly int Updates;internal readonly bool Flame;
            internal readonly double[] Horizontal,Gravity,Drop;
            internal readonly Angles Proposals=new Angles();
            private int completed;private double factor=1;
            public Response(int updates,bool flame)
            {
                if(updates<=0 || updates>16)throw new ArgumentOutOfRangeException(nameof(updates));
                Updates=updates;Flame=flame;Horizontal=new double[120*updates+1];Gravity=new double[Horizontal.Length];Drop=new double[Horizontal.Length];
            }
            internal void Ensure(int count)
            {
                for(int n=completed+1;n<=count;n++)
                {
                    if((n&31)==1)CombatPredictionWork.Check();
                    if(Flame || n>=15)factor*=.99;
                    Horizontal[n]=Horizontal[n-1]+factor;
                    Gravity[n]=Gravity[n-1]+(Flame?(n>=10?.1:0)+(n>=20?.1:0):n>=15?.2:0);Drop[n]=Drop[n-1]+Gravity[n];completed=n;
                }
            }
        }
        private struct Proposal
        {
            internal float X,Y;internal int Tick;internal double Error;
        }
        // Each bucket is narrower than the old .99999 dot tolerance. Adjacent
        // buckets (including the -PI/PI seam) are checked before insertion.
        // Storage and selection stay bounded independently of path/receiver size.
        internal sealed class Angles
        {
            private const int Count=2048;
            private readonly Dictionary<int,Proposal> rows=new Dictionary<int,Proposal>();
            internal void Clear(){rows.Clear();}
            internal void Add(double x,double y,int tick,double error)
            {
                int at=(int)((Math.Atan2(y,x)+Math.PI)*Count/(2*Math.PI));if(at==Count)at=0;
                for(int d=-2;d<=2;d++)
                {
                    int index=(at+d+Count)%Count;Proposal prior;
                    if(!rows.TryGetValue(index,out prior) || prior.X*x+prior.Y*y<=.99999)continue;
                    if(tick<prior.Tick || tick==prior.Tick && error<prior.Error)
                    {
                        rows.Remove(index);Proposal destination;
                        if(!rows.TryGetValue(at,out destination) || tick<destination.Tick || tick==destination.Tick && error<destination.Error)rows[at]=new Proposal{X=(float)x,Y=(float)y,Tick=tick,Error=error};
                    }
                    return;
                }
                Proposal current;if(!rows.TryGetValue(at,out current) || tick<current.Tick || tick==current.Tick && error<current.Error)rows[at]=new Proposal{X=(float)x,Y=(float)y,Tick=tick,Error=error};
            }
            internal Direction[] Select()
            {
                var result=new List<Direction>(8);
                for(int pick=0;pick<8;pick++)
                {
                    Proposal best=default(Proposal);
                    foreach(var p in rows.Values)
                    {
                        if(p.Tick==0 || best.Tick!=0 && (p.Tick>best.Tick || p.Tick==best.Tick && p.Error>=best.Error))continue;
                        bool same=false;foreach(var q in result)if(p.X*q.X+p.Y*q.Y>.9998f){same=true;break;}if(!same)best=p;
                    }
                    if(best.Tick==0)break;result.Add(new Direction(best.X,best.Y,best.Tick,best.Error));
                }
                return result.ToArray();
            }
        }
        public static Direction[] Generate(NpcTrajectory path,int age,float originX,float originY,float speed,int updates,bool flame,float birthOffset,float bodyHalf)
        {return GeneratePrepared(path,age,originX,originY,speed,updates,flame,birthOffset,bodyHalf,null);}
        public static Direction[] GeneratePrepared(NpcTrajectory path,int age,float originX,float originY,float speed,int updates,bool flame,float birthOffset,float bodyHalf,Response response)
        {
            if(path==null || speed<=0 || float.IsNaN(speed) || float.IsInfinity(speed) || updates<=0 || updates>16 || age<0 || age+1>=path.Count)return new Direction[0];
            int steps=Math.Min(120,path.Count-age-1),count=steps*updates;
            if(response==null || response.Updates!=updates || response.Flame!=flame)response=new Response(updates,flame);
            response.Ensure(count);var horizontal=response.Horizontal;var gravity=response.Gravity;var drop=response.Drop;
            var proposals=response.Proposals;proposals.Clear();
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
                        if((n&15)==1)CombatPredictionWork.Check();
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
                            proposals.Add(ux,uy,tick,error);
                        }
                    }
                }
            }
            return proposals.Select();
        }
    }
}
