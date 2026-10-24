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
                var delta=mounted-HostAttackModels.LaunchOrigin(p,p.HeldItem);
                if(type==3065)
                {
                    // Star Wrath reads Center, whereas the caller's origin is
                    // rotated MountedCenter. Retain its supplied future delta.
                    Y=p.Center.Y-600+delta.Y;A=0;B=p.Center.X-200*p.direction+delta.X;return;
                }
                Y=p.MountedCenter.Y-600-(type==4381?75:0)+delta.Y;
                float shift=type==65?(left?100:-100):-(type==4381?30:100)*p.direction;
                A=type==65?1:type==4381 || type==3029?10f/11:.5f;
                B=A*(p.width/2+shift)+(1-A)*(p.Center.X+delta.X);
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
        private sealed class SkyProposal
        {
            internal Vector2 Aim,Birth,Velocity;
            internal float Error;
            internal int Tick,Sub;
            internal bool Center;
        }
        private static HostAttackSolution Search(Player player,Item item,Vector2 mounted,Projectile sample,AttackMotion motion,NpcTrajectory timeline,int age,PredictionTerrain terrain,HostAttackReceive receive,HostAttackObstacles obstacles,int delay)
        {
            var proposals=new Dictionary<Vector2,SkyProposal>();SkyProposal best=null;
            // A complete, quantized natural command can survive an OPTIONAL
            // contact interruption. No partially scored candidate is published.
            HostAttackSolution point=default(HostAttackSolution);
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-1));
            try
            {
                // Unique centre/left/right/top/bottom intents precede inverse
                // work. The former eight edge seeds contained four duplicates.
                for(int seed=-1;seed<4;seed++)
                {
                    float speed=motion.Speed;
                    do
                    {
                        for(int k=1;k<=limit;k++)
                        {
                            CombatPredictionWork.Check();int tick=age+motion.OuterAtSubupdate(k);var b=timeline[tick].ProjectileReceiveBounds;
                            if(!timeline[tick].CanReceive)continue;
                            float tx=b.CenterX,ty=b.CenterY;
                            if(seed>=0){float edge=(seed&1)==0?-1:1;if(seed<2)tx+=edge*(b.Width+motion.Width-2.1f)*.5f;else ty+=edge*(b.Height+motion.Height-2.1f)*.5f;}
                            for(int branch=0;branch<(item.type==65?2:1);branch++)
                            {
                                var facts=new LaunchFacts(player,item.type,mounted,branch==1);
                                float q=item.type==4381?.675f:1,denominator=q+(1-q)*facts.A;
                                float vy=VerticalVelocity(motion,ty-facts.Y,k,speed);
                                if(float.IsNaN(vy) || Math.Abs(vy)>speed || denominator==0 || item.type!=65 && vy<=0)continue;
                                // C is the same-time free-flight intent. With
                                // no gravity/q=1 this is T, never a free distant
                                // ray point that makes native sky fans parallel.
                                var aim=Quantize(player,item.type,new Vector2((tx-(1-q)*facts.B)/denominator,facts.Y+k*vy));
                                if(float.IsNaN(aim.X) || float.IsNaN(aim.Y))continue;
                                if(item.type==65 && (aim.X<player.Left.X+mounted.X-HostAttackModels.LaunchOrigin(player,item).X)!=(branch==1) || item.type!=65 && aim.Y-facts.Y<20)continue;
                                Vector2 birth,velocity;Launch(player,item.type,mounted,aim,motion.Speed,out birth,out velocity);
                                float actualX=velocity.X,actualY=velocity.Y;motion.Launch(ref actualX,ref actualY);
                                if(Math.Abs(new Vector2(actualX/q,actualY).Length()-speed)>.003f)continue;
                                float x=birth.X+k*actualX,y=birth.Y+Vertical(motion,actualY,k);
                                // Lunar may freeze at an earlier trigger. Its
                                // conservative 140-box hint must not exclude a
                                // later receive in that stationary window.
                                float halfX=(b.Width+(sample.type==645?140:motion.Width))*.5f;
                                float halfY=(b.Height+(sample.type==645?140:motion.Height))*.5f;
                                if(sample.type==645)
                                {
                                    int window=(Main.projFrames[645]*motion.Updates*3+1)/2;
                                    float length=actualX*actualX+actualY*actualY;
                                    float nearest=length==0?k:((b.CenterX-birth.X)*actualX+(b.CenterY-birth.Y)*actualY)/length;
                                    nearest=Math.Max(Math.Max(1,k-window+1),Math.Min(k,nearest));
                                    // Any earlier trigger in the finite native
                                    // stationary window remains a cheap possible
                                    // receive. The dedicated replay decides if
                                    // that trigger and Damage can actually occur.
                                    x=birth.X+nearest*actualX;y=birth.Y+nearest*actualY;
                                }
                                float dx=(x-b.CenterX)/halfX,dy=(y-b.CenterY)/halfY,error=dx*dx+dy*dy;
                                if(Math.Abs(dx)>1 || Math.Abs(dy)>1)continue;
                                var candidate=new SkyProposal{Aim=aim,Birth=birth,Velocity=velocity,Tick=tick,Sub=motion.SubupdateInOuter(k),Error=error,Center=seed<0};
                                SkyProposal prior;
                                // Merge best arrival BEFORE cursor de-duplication.
                                // A static C at k=1 must not hide its true arrival.
                                if(proposals.TryGetValue(aim,out prior) && !Better(candidate,prior))continue;
                                proposals[aim]=candidate;
                                if(best!=null && !Better(candidate,best))continue;
                                if(!LegalPoint(player,item,candidate,sample,terrain))continue;
                                best=candidate;point=Point(timeline,best,HostAttackReachability.Unknown,null);
                            }
                        }
                        if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
                    }while(true);
                }
            }
            catch(CombatPredictionBudgetExceededException){return point;}
            if(best==null)return point;
            var ordered=new List<SkyProposal>(proposals.Values);ordered.Sort((a,b)=>Better(a,b)?-1:Better(b,a)?1:0);
            // Linear proposals plus only a few useful complete paths. This is
            // bounded representative/side-path work, not H complete replays.
            int checkedPaths=0;
            try
            {
                foreach(var candidate in ordered)
                {
                    if(checkedPaths==3)break;
                    CombatPredictionWork.Check();if(!LegalPoint(player,item,candidate,sample,terrain))continue;checkedPaths++;
                    int receiveTick;var reach=Representative(player,item,candidate,sample,motion,timeline,age,terrain,out receiveTick);
                    if(ReferenceEquals(candidate,best) || reach==HostAttackReachability.Reachable)point=Point(timeline,candidate,reach,null,receiveTick);
                    var contact=Replay(player,item,candidate.Aim,candidate.Birth,candidate.Velocity,sample,motion,timeline,age,terrain,receive,obstacles,delay);
                    if(contact!=null)return Point(timeline,candidate,HostAttackReachability.Reachable,contact);
                    if(reach==HostAttackReachability.Reachable)return Point(timeline,candidate,reach,null,receiveTick);
                }
            }
            catch(CombatPredictionBudgetExceededException){ /* Keep the sealed command and its proven reach state. */ }
            return point;
        }
        private static bool Better(SkyProposal a,SkyProposal b)
        {return a.Center!=b.Center?a.Center:a.Error!=b.Error?a.Error<b.Error:a.Tick<b.Tick;}
        private static HostAttackSolution Point(NpcTrajectory timeline,SkyProposal proposal,HostAttackReachability reach,AttackContact contact,int receiveTick=-1)
        {return new HostAttackSolution(timeline,proposal.Aim,true,reach,contact?.ReceiveTick??receiveTick,AttackIntercept.WithPointSemantics(contact,AttackPointSemantics.WorldPoint),pointSemantics:AttackPointSemantics.WorldPoint);}
        private static bool LegalPoint(Player player,Item item,SkyProposal proposal,Projectile sample,PredictionTerrain terrain)
        {
            if(!HostProjectileEnvironment.Dry(sample,terrain,proposal.Birth.X,proposal.Birth.Y,proposal.Velocity.X))return false;
            if(item.type!=65)return true;
            Vector2 scan=proposal.Aim,step=(proposal.Birth-proposal.Aim).SafeNormalize(new Vector2(0,-1))*16;bool blocked;PredictionStop stop;
            for(int k=0;k<120 && scan.Y>proposal.Birth.Y;k++){if(!terrain.Solid(new MotionRect(scan.X,scan.Y,1,1),out blocked,out stop))return false;if(!blocked)return true;scan+=step;}
            return true;
        }
        private static HostAttackReachability Representative(Player player,Item item,SkyProposal proposal,Projectile sample,AttackMotion motion,NpcTrajectory timeline,int age,PredictionTerrain terrain,out int receiveTick)
        {
            receiveTick=-1;
            // Proximity/earlier receiver/tile triggers can freeze lunar flight;
            // its dedicated ordered replay alone can certify that path.
            if(sample.type==645)return HostAttackReachability.Unknown;
            float x=proposal.Birth.X,y=proposal.Birth.Y,vx=proposal.Velocity.X,vy=proposal.Velocity.Y;motion.Launch(ref vx,ref vy);
            bool collides=sample.tileCollide;
            float gate=proposal.Aim.Y;
            if(item.type==65)
            {
                Vector2 scan=proposal.Aim,step=(proposal.Birth-proposal.Aim).SafeNormalize(new Vector2(0,-1))*16;bool blocked;PredictionStop stop;
                for(int k=0;k<120 && scan.Y>proposal.Birth.Y;k++){if(!terrain.Solid(new MotionRect(scan.X,scan.Y,1,1),out blocked,out stop))return HostAttackReachability.Unknown;if(!blocked)break;scan+=step;}
                gate=scan.Y;
            }
            int end=motion.SubupdatesForOuters(proposal.Tick-age);
            for(int k=1;k<=end;k++)
            {
                CombatPredictionWork.Check();float nx=x,ny=y;motion.Advance(ref nx,ref ny,ref vx,ref vy,k);
                if(!HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x))return HostAttackReachability.Unknown;
                if(item.type==2750)collides|=y-motion.Height*.5f>player.position.Y-300 || y-motion.Height*.5f<Main.worldSurface*16;
                else if(item.type==65)collides|=y+motion.Height*.5f>=gate;
                else if(item.type==3065)collides|=y>Math.Min(gate,player.Center.Y-200);
                if(collides)
                {
                    float rx,ry;if(!terrain.ProjectileCollision(x,y,nx-x,ny-y,(int)motion.Width,(int)motion.Height,out rx,out ry))return HostAttackReachability.Unknown;
                    if(rx!=nx-x || ry!=ny-y)return HostAttackReachability.Blocked;
                    if(!terrain.ProjectilePassage(x,y,nx,ny,(int)motion.Width,(int)motion.Height))return HostAttackReachability.Unknown;
                }
                x=nx;y=ny;int tick=age+motion.OuterAtSubupdate(k);
                if(AttackIntercept.BodyContact(timeline,tick,motion.SubupdateInOuter(k),proposal.Aim.X,proposal.Aim.Y,x,y,motion.Width,motion.Height,motion.Confidence)!=null){receiveTick=tick;return HostAttackReachability.Reachable;}
            }
            return HostAttackReachability.Unknown;
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
                else if(item.type==3065)collides|=y>Math.Min(gate,player.Center.Y-200);
                return HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x) && (!collides || terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h));
            };
            return AttackIntercept.Replay(aim.X,aim.Y,birth.X,birth.Y,velocity.X,velocity.Y,motion,timeline,age,passage,t=>receive.Allows(t+delay),(k,t,x,y)=>obstacles.PassOuter(k,t,delay+motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || obstacles.BeforeNpc(t,delay+motion.OuterAtSubupdate(k),x,y));
        }
    }
}
