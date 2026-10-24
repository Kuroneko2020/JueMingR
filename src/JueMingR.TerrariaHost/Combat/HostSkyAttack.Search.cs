using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostSkyAttack
    {
        // These are the representative native birth facts, shared with Launch.
        // Random X/spread stays native; no proposal consumes Main.rand.
        private readonly struct LaunchFacts
        {
            internal readonly float A,B,Y;
            internal LaunchFacts(Player p,int type,Vector2 mounted,bool left)
            {
                Y=mounted.Y-600-(type==4381?75:0);
                if(type==3065){A=0;B=p.Center.X-200*p.direction+(mounted.X-p.MountedCenter.X);return;}
                float shift=type==65?(left?100:-100):-(type==4381?30:100)*p.direction;
                A=type==65?1:type==4381 || type==3029?10f/11:.5f;
                B=A*(p.width/2+shift)+(1-A)*p.Center.X;
            }
        }
        internal static int PreferredRange(Player player,Item item,AttackAmmoSnapshot ammo,NPC npc)
        {
            Projectile sample;if(ammo==null || !ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample))return 16;
            float speed=ammo.Speed*(item.type==2750?.75f:item.type==3570?.5f:1);
            Vector2 birth,velocity;Launch(player,item.type,HostAttackModels.LaunchOrigin(player,item),npc.Center,speed,out birth,out velocity);
            var motion=new AttackMotion(speed,0,0,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,componentBirthLimit:sample.aiStyle==1);
            float vx=velocity.X,vy=velocity.Y;motion.Launch(ref vx,ref vy);
            float closing=Math.Max(1,new Vector2(vx,vy).Length()*motion.Updates-npc.velocity.Length());
            // A scheduling hint from actual birth, speed and subupdates. Source
            // still supplies every point; neither this margin nor the estimate
            // is a search window or a proof of a miss.
            return Math.Min(NpcPredictionCache.Horizon,Math.Max(16,(int)Math.Ceiling(Vector2.Distance(birth,npc.Center)/closing)+8));
        }
        private static Vector2 Quantize(Player player,int type,Vector2 aim)
        {
            bool reverse=!RawCursor(type);var screen=aim-Main.screenPosition;
            if(reverse && player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            if(float.IsNaN(screen.X) || float.IsInfinity(screen.X) || float.IsNaN(screen.Y) || float.IsInfinity(screen.Y) || screen.X<int.MinValue || screen.X>int.MaxValue || screen.Y<int.MinValue || screen.Y>int.MaxValue)return new Vector2(float.NaN,float.NaN);
            screen=new Vector2((int)screen.X,(int)screen.Y);
            if(reverse && player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            return screen+Main.screenPosition;
        }
        // Closed discrete gravity response, including the native falling cap.
        // Advance applies gravity before translation, even when start is zero.
        internal static float Vertical(AttackMotion motion,float vy,int k)
        {
            int start=Math.Max(1,motion.GravityStart),m=Math.Max(0,k-start+1),plain=k-m;
            if(m==0 || motion.Gravity==0)return k*(motion.MaxFallSpeed>0?Math.Min(vy,motion.MaxFallSpeed):vy);
            int ramp=m;
            if(motion.MaxFallSpeed>0 && motion.Gravity>0)ramp=Math.Max(0,Math.Min(m,(int)Math.Floor((motion.MaxFallSpeed-vy)/motion.Gravity)));
            return plain*vy+ramp*vy+motion.Gravity*ramp*(ramp+1)*.5f+(m-ramp)*motion.MaxFallSpeed;
        }
        private static float VerticalVelocity(AttackMotion motion,float dy,int k,float speed)
        {
            if(motion.MaxFallSpeed<=0 || motion.Gravity==0)
            {int m=Math.Max(0,k-Math.Max(1,motion.GravityStart)+1);return (dy-motion.Gravity*m*(m+1)*.5f)/k;}
            float low=-speed,high=speed;
            if(dy<Vertical(motion,low,k) || dy>Vertical(motion,high,k))return float.NaN;
            for(int i=0;i<24;i++){float mid=(low+high)*.5f;if(Vertical(motion,mid,k)<dy)low=mid;else high=mid;}
            return (low+high)*.5f;
        }
        private static AttackContact Search(Player player,Item item,Vector2 mounted,Projectile sample,AttackMotion motion,NpcTrajectory timeline,int age,PredictionTerrain terrain,HostAttackReceive receive,HostAttackObstacles obstacles,int delay)
        {
            var cursors=new HashSet<Vector2>();
            // All real launch-speed classes get their centre proposals before
            // any receive-edge fallback. Capacity and enumeration order never
            // stand in for a physical speed-class limit.
            for(int seed=-1;seed<8;seed++)
            {
                float speed=motion.Speed;
                do
                {
                    for(int k=1;k<=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-1));k++)
                    {
                        CombatPredictionWork.Check();int tick=age+motion.OuterAtSubupdate(k);var b=timeline[tick].ProjectileReceiveBounds;
                        if(!timeline[tick].CanReceive)continue;
                        float rx=b.CenterX,ry=b.CenterY;
                        if(seed>=0){bool vertical=seed<4;float edge=seed%4<2?-1:1;rx+=vertical?edge*(b.Width+motion.Width-2.1f)*.5f:0;ry+=vertical?0:edge*(b.Height+motion.Height-2.1f)*.5f;}
                        for(int branch=0;branch<(item.type==65?2:1);branch++)
                        {
                            var facts=new LaunchFacts(player,item.type,mounted,branch==1);
                            float vy=VerticalVelocity(motion,ry-facts.Y,k,speed);
                            if(float.IsNaN(vy) || Math.Abs(vy)>speed || item.type!=65 && vy<=0)continue;
                            float horizontal=(float)Math.Sqrt(Math.Max(0,speed*speed-vy*vy));
                            for(int sign=-1;sign<=1;sign+=2)
                            {
                                float vx=horizontal*sign,storedX=vx*(item.type==4381?.675f:1);
                                float cursorX=(rx-facts.B-k*storedX)/facts.A;
                                float dx=(1-facts.A)*cursorX-facts.B;
                                if(Math.Abs(vx)<.00001f)continue;
                                float dy=dx*vy/vx;
                                if(item.type!=65 && dy<20 || item.type==65 && (cursorX<player.Left.X)!=(branch==1))continue;
                                var aim=Quantize(player,item.type,new Vector2(cursorX,facts.Y+dy));
                                if(float.IsNaN(aim.X) || float.IsNaN(aim.Y))continue;
                                if(!cursors.Add(aim))continue;
                                Vector2 birth,velocity;Launch(player,item.type,mounted,aim,motion.Speed,out birth,out velocity);
                                float actualX=velocity.X,actualY=velocity.Y;motion.Launch(ref actualX,ref actualY);
                                if(Math.Abs(new Vector2(actualX/(item.type==4381?.675f:1),actualY).Length()-speed)>.003f)continue;
                                // Cheap endpoint rejection precedes the sole full
                                // proof. Lunar is deliberately not flight-box
                                // filtered: a prior trigger can leave a stationary
                                // explosion that receives a later target point.
                                if(sample.type!=645 && AttackIntercept.BodyContact(timeline,tick,motion.SubupdateInOuter(k),aim.X,aim.Y,birth.X+k*actualX,birth.Y+Vertical(motion,actualY,k),motion.Width,motion.Height,motion.Confidence)==null)continue;
                                var contact=Replay(player,item,aim,birth,velocity,sample,motion,timeline,age,terrain,receive,obstacles,delay);
                                if(contact!=null)return contact;
                            }
                        }
                    }
                    if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
                }while(true);
            }
            return null;
        }
        private static AttackContact Replay(Player player,Item item,Vector2 aim,Vector2 birth,Vector2 velocity,Projectile sample,AttackMotion motion,NpcTrajectory timeline,int age,PredictionTerrain terrain,HostAttackReceive receive,HostAttackObstacles obstacles,int delay)
        {
            if(sample.type==645)return LunarReplay(aim,birth,velocity,motion,timeline,age,terrain,obstacles,delay);
            float gate=aim.Y;
            if(item.type==65)
            {
                Vector2 scan=aim,step=(birth-aim).SafeNormalize(new Vector2(0,-1))*16;bool blocked;PredictionStop stop;
                for(int k=0;k<120 && scan.Y>birth.Y;k++){if(!terrain.Solid(new MotionRect(scan.X,scan.Y,1,1),out blocked,out stop))return null;if(!blocked)break;scan+=step;}gate=scan.Y;
            }
            bool collides=sample.tileCollide;
            AttackPassage passage=(x,y,nx,ny,w,h)=>
            {
                if(item.type==2750)collides|=y-h/2>player.position.Y-300 || y-h/2<Main.worldSurface*16;
                else if(item.type==65)collides|=y+h/2>=gate;
                return HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x) && (!collides || terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h));
            };
            return AttackIntercept.Replay(aim.X,aim.Y,birth.X,birth.Y,velocity.X,velocity.Y,motion,timeline,age,passage,t=>receive.Allows(t+delay),(k,t,x,y)=>obstacles.PassOuter(k,t,delay+motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || obstacles.BeforeNpc(t,delay+motion.OuterAtSubupdate(k),x,y));
        }
    }
}
