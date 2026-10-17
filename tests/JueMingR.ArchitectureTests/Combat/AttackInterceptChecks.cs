using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class AttackInterceptChecks
    {
        internal static void Run()
        {
            var solver=typeof(CombatOptions).Assembly.GetType("JueMingR.Features.Combat.AttackIntercept");
            if(solver==null)throw new InvalidOperationException("No time-matched attack intercept preparation exists.");
            foreach(float targetSpeed in new[]{0f,2f})foreach(int updates in new[]{1,3})
            {
                var line=Timeline(240,0,targetSpeed,0);
                var motion=new AttackMotion(10,0,0,updates,4,4,360);
                var contact=AttackIntercept.Solve(0,10,motion,line,0,0);
                Require(contact!=null,"fixed/moving straight contact, updates="+updates);
                Replay(contact,motion,0,10,0);
                Require(contact.AimX!=contact.ImpactX,"input is a direction point, not a mislabeled impact");
                var delayed=AttackIntercept.Solve(0,10,motion,line,2,7);
                Require(delayed!=null && delayed.Tick>9,"observed age and attack delay belong to target time");
                Replay(delayed,motion,0,10,2);
            }
            foreach(int updates in new[]{1,2})
            {
                var arrow=new AttackMotion(8,.1f,15,updates,4,4,360);
                var contact=AttackIntercept.Solve(0,10,arrow,Timeline(460,30,1,0),0,0);
                Require(contact!=null && contact.AimY<contact.ImpactY,"delayed gravity requires a distinct elevated input");
                Replay(contact,arrow,0,10,0);
            }
            var blocked=AttackIntercept.Solve(0,10,new AttackMotion(10,0,0,1,4,4,120),Timeline(240,0,0,0),0,0,(x,y,nx,ny,w,h)=>nx<100);
            Require(blocked==null,"a wall before the translated contact does not manufacture red impact");
            Require(AttackIntercept.Solve(0,10,new AttackMotion(1,0,0,1,4,4,120),Timeline(2000,0,0,0),0,0)==null,"insufficient shared horizon stays unsolved");
            var edge=AttackIntercept.Solve(100,100,new AttackMotion(10,0,0,1,2,2,1),Timeline(108,105,0,0,8,1),0,0);
            Require(edge!=null && edge.Tick==1,"a thin reachable edge is retained when the centre seed misses");Replay(edge,new AttackMotion(10,0,0,1,2,2,1),100,100,0);
            Require(AttackIntercept.Solve(100,100,new AttackMotion(10.6f,0,0,1,2,2,1),Timeline(111,100,0,0,1,1),0,0)==null,"floating overlap cannot bypass native integer top-left truncation");
            Console.WriteLine("PASS attack contact: fixed/moving, delay/age, subupdates, delayed gravity, blocked and horizon negatives.");
        }
        private static NpcTrajectory Timeline(float x,float y,float vx,float vy,int width=20,int height=20)
        {
            var id=new NpcIdentity(1,new object(),0,1,1,1);var points=new NpcTrajectoryPoint[121];
            for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=x+vx*i,Y=y+vy*i,Width=width,Height=height,Vx=vx,Vy=vy,CanReceive=true,Active=true});
            return new NpcTrajectory(id,100,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        private static void Replay(AttackContact c,AttackMotion m,float x,float y,int age)
        {
            float dx=c.AimX-x,dy=c.AimY-y,length=(float)Math.Sqrt(dx*dx+dy*dy),vx=dx/length*m.Speed,vy=dy/length*m.Speed;
            int steps=(c.Tick-age-c.Delay-1)*m.Updates+c.Subupdate+1;
            for(int i=1;i<=steps;i++)m.Advance(ref x,ref y,ref vx,ref vy,i);
            var b=c.Timeline[c.Tick].ProjectileReceiveBounds;
            int left=(int)(x-m.Width/2),top=(int)(y-m.Height/2);Require(left+m.Width>b.X && left<b.X+b.Width && top+m.Height>b.Y && top<b.Y+b.Height,"contact replays against the SAME future integer damage rectangle");
            Require(c.ImpactX>=b.X && c.ImpactX<=b.X+b.Width && c.ImpactY>=b.Y && c.ImpactY<=b.Y+b.Height,"reported red point belongs to the received target");
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
