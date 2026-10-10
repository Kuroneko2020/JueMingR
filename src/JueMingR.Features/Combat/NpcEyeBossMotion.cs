using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // The numbered player supplies motion even when TargetClosest faces a pet.
    // Own charge choices stop at their actual motor RNG. MechQueen Spazmatism
    // also reuses its flame-emission position in this action's smooth motor;
    // its observed rotation and real visibility clock therefore remain inputs.
    internal static class NpcEyeBossMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.EffectiveType==4 && n.Style==4 || n.EffectiveType==125 && n.Style==30 || n.EffectiveType==126 && n.Style==31;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !e.Day && (!n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer);}
        internal static bool Retargets(NpcMotionState n)
        {return n.Target<0 || n.Target==255 || n.TargetCaptured && (!n.PlayerActive || n.PlayerNativeDead) || n.Style==31 && n.A0==0 && n.A1==0 || n.Style==4 && n.A0==3 && n.A1==3;}

        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;n.ReflectsProjectiles=false;
            if(n.Target<0 || n.Target==255 || n.TargetCaptured && (!n.PlayerActive || n.PlayerNativeDead))Query(ref n,ref e,confused);
            bool dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
            if(e.Day || dead)
            {
                if(n.Style==31 && n.MotorRotationCaptured)
                {
                    bool rotationQueen=false;if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.MechQueenIdentity)){rotationQueen=group[i].Active && group[i].EffectiveType==127;break;}
                    SpazRotation(ref n,e,rotationQueen);
                }
                n.Vy-=.04f;if(n.TimeLeft>10)n.TimeLeft=10;return true;
            }
            if(n.TargetCaptured && (!n.HasPlayer || n.PlayerGhost || !n.PlayerActive))
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.Style==4)return Cthulhu(ref n,e,terrain,confused,out stop);
            if(!n.EyeRelationsCaptured || !n.EyeRelationsComplete || group==null)
            {stop=PredictionStop.MissingDependency;return false;}
            // DiscourageDespawn scans every actual twin, not just a partner.
            if(!e.Multiplayer && n.TimeLeft<10)for(int i=0;i<count;i++)
                if(group[i].Active && !group[i].Identity.Equals(n.Identity) && Twin(group[i].EffectiveType) && n.TimeLeft<group[i].TimeLeft-1)n.TimeLeft=group[i].TimeLeft-1;
            float qx=0,qy=0;bool queen=n.MechQueenIdentity.Token!=null;
            if(!n.MechFactsCaptured || queen && !QueenPoint(n,group,count,out qx,out qy,out queen))
            {stop=PredictionStop.MissingDependency;return false;}
            if(queen && (n.A0==1 || n.A0==2))n.ReflectsProjectiles=true;
            return n.Style==30?Retinazer(ref n,e,qx,qy,queen,confused,out stop):Spazmatism(ref n,e,terrain,qx,qy,queen,confused,out stop);
        }
        private static bool Twin(int type){return type==125 || type==126;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused)
        {NpcTargeting.Face(ref n,ref e,confused:confused);}
        private static bool QueenPoint(NpcMotionState n,NpcMotionState[] group,int count,out float x,out float y,out bool active)
        {
            x=y=0;active=false;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.MechQueenIdentity))
            {
                var q=group[i];if(!q.Active || q.EffectiveType!=127)return true;
                // GetMechQueenCenter is Center + (0,-14). Original RotatedBy
                // rotates the .75-scaled offset around this exact center.
                active=true;double angle=q.Vx*.025f;float cx=q.Bounds.CenterX,cy=q.Bounds.CenterY-14;
                float ox=cx+(n.Style==30?-150f:150f)*.75f,oy=cy-250f*.75f;
                ox-=cx;oy-=cy;float cosine=(float)Math.Cos(angle),sine=(float)Math.Sin(angle);
                x=cx;x+=ox*cosine-oy*sine;y=cy;y+=ox*sine+oy*cosine;return true;
            }
            return false;
        }
        private static bool Cthulhu(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool low=e.Expert && (double)n.Life<(double)n.LifeMax*.12,tiny=e.Expert && (double)n.Life<(double)n.LifeMax*.04;
            if(n.A0==0)
            {
                if(n.A1==0)
                {
                    float speed=e.Expert?7:5,accel=e.Expert?.15f:.04f;if(e.GoodWorld){speed+=1;accel+=.05f;}
                    float distance=Chase(ref n,e.PlayerX,e.PlayerY-200,speed,accel);n.A2++;
                    if(n.A2>=(e.Expert?600f*.35f:600f)){n.A1=1;n.A2=n.A3=0;n.Target=255;}
                    else if((n.Y+n.Height<e.PlayerY-e.PlayerHeight/2 || e.Expert) && distance<500)
                    {
                        n.A3++;float spawnClock=e.Expert?110f*.4f:110f;if(e.GoodWorld)spawnClock*=.8f;
                        if(n.A3>=spawnClock)n.A3=0; // Servant birth has no feedback into this motor.
                    }
                }
                else if(n.A1==1)
                {Charge(ref n,e.PlayerX,e.PlayerY,(e.Expert?7f:6f)+(e.GoodWorld?1:0));n.A1=2;}
                else if(n.A1==2)
                {
                    n.A2++;if(n.A2>=40){Damp(ref n,.98f);if(e.Expert)Damp(ref n,.985f);if(e.GoodWorld)Damp(ref n,.99f);Snap(ref n);}
                    int limit=(e.Expert?100:150)-(e.GoodWorld?15:0);
                    if(n.A2>=limit){n.A3++;n.A2=0;n.Target=255;if(n.A3>=3){n.A1=0;n.A3=0;}else n.A1=1;}
                }
                if((float)n.Life<(float)n.LifeMax*(e.Expert?.65f:.5f)){n.A0=1;n.A1=n.A2=n.A3=0;}
                return true;
            }
            if(n.A0==1 || n.A0==2)
            {
                if(e.GoodWorld)n.ReflectsProjectiles=true;
                if(n.A0==1 || n.A3==1)n.A2=Math.Min(.5f,n.A2+.005f);else n.A2=Math.Max(0,n.A2-.005f);
                n.A1++;
                if(n.A1>=100){if(n.A3==1){n.A3=0;n.A1=0;}else{n.A0++;n.A1=0;if(n.A0==3)n.A2=0;}}
                Damp(ref n,.98f);Snap(ref n);return true;
            }
            n.Health.Defense=low?-15:0;if(tiny)n.Health.Defense=-30;
            if(n.A1==0 && low)n.A1=5;
            if(n.A1==0)
            {
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-120-n.Bounds.CenterY,d=Length(dx,dy),speed=6,accel=.07f;
                if(e.Expert){if(d>400){speed++;accel+=.05f;}if(d>600){speed++;accel+=.05f;}if(d>800){speed++;accel+=.05f;}}
                if(e.GoodWorld){speed++;accel+=.1f;}Chase(ref n,e.PlayerX,e.PlayerY-120,speed,accel);n.A2++;
                if(n.A2>=200){n.A1=e.Expert && (double)n.Life<(double)n.LifeMax*.35?3:1;n.A2=n.A3=0;n.Target=255;}
                // The native tiny-health branch is retained even though the
                // preceding low-health switch normally diverts hover to5.
                if(tiny){Query(ref n,ref e,confused);n.A1=3;n.A2=0;n.A3-=1000;}
            }
            else if(n.A1==1)
            {
                float speed=6.8f;if(e.Expert && n.A3==1)speed*=1.15f;if(e.Expert && n.A3==2)speed*=1.3f;if(e.GoodWorld)speed*=1.2f;
                Charge(ref n,e.PlayerX,e.PlayerY,speed);n.A1=2;
            }
            else if(n.A1==2)
            {
                n.A2++;if(n.A2>=(e.Expert?50:40)){Damp(ref n,.97f);if(e.Expert)Damp(ref n,.98f);Snap(ref n);}
                if(n.A2>=(e.Expert?90:130))
                {
                    // Native consumes Next(1,4) after the third charge. Its
                    // chosen count changes the next motor, so stop beforehand.
                    if(n.A3+1>=3 && e.Expert && !e.Multiplayer && (double)n.Life<(double)n.LifeMax*.5)
                    {stop=PredictionStop.RandomDestination;return false;}
                    n.A3++;n.A2=0;n.Target=255;if(n.A3>=3){n.A1=0;n.A3=0;}else n.A1=1;
                }
            }
            else if(n.A1==3)
            {
                if(n.A3==4 && low && n.Bounds.CenterY>e.PlayerY){Query(ref n,ref e,confused);n.A1=n.A2=n.A3=0;}
                else if(!e.Multiplayer){stop=PredictionStop.RandomDestination;return false;}
            }
            else if(n.A1==4)
            {
                float brake=tiny?10:20;n.A2++;
                float px=e.PlayerX-e.PlayerWidth/2,py=e.PlayerY-e.PlayerHeight/2;
                if(n.A2==brake && Length(n.X-px,n.Y-py)<200)n.A2--;
                if(n.A2>=brake){Damp(ref n,.95f);Snap(ref n);}
                if(n.A2>=brake+13)
                {
                    n.A3++;n.A2=0;
                    if(n.A3>=5)
                    {
                        n.A1=0;n.A3=0;
                        if(n.Target>=0 && e.GoodWorld)
                        {
                            bool clear;if(terrain==null || !terrain.CanHit(n.Bounds,new MotionRect(px,py,n.Width,n.Height),out clear,out stop))
                            {if(stop==PredictionStop.None)stop=PredictionStop.MissingDependency;return false;}
                            if(clear){n.A0=2;n.A1=n.A2=0;n.A3=1;}
                        }
                    }
                    else n.A1=3;
                }
            }
            else if(n.A1==5)
            {
                Chase(ref n,e.PlayerX,e.PlayerY+600,9,.3f);n.A2++;
                if(n.A2>=70){stop=PredictionStop.RandomDestination;return false;}
            }
            if(tiny && n.A1==5)n.A1=3;return true;
        }
        private static bool Retinazer(ref NpcMotionState n,PredictionEnvironment e,float qx,float qy,bool queen,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.A0==0)
            {
                if(n.A1==0)
                {
                    float speed=e.Expert?8.25f:7,accel=e.Expert?.115f:.1f;if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}
                    int side=n.X+n.Width/2<e.PlayerX+e.PlayerWidth/2?-1:1;
                    float distance=queen?Smooth(ref n,qx,qy,14,60):Chase(ref n,e.PlayerX+side*300,e.PlayerY-300,speed,accel);
                    n.A2++;if(n.A2>=(queen?1200:600)){n.A1=1;n.A2=n.A3=0;n.Target=255;}
                    else if(n.Y+n.Height<e.PlayerY-e.PlayerHeight/2 && distance<400)
                    {
                        n.A3++;if(e.Expert){if((double)n.Life<(double)n.LifeMax*.9)n.A3+=.3f;if((double)n.Life<(double)n.LifeMax*.8)n.A3+=.3f;if((double)n.Life<(double)n.LifeMax*.7)n.A3+=.3f;if((double)n.Life<(double)n.LifeMax*.6)n.A3+=.3f;}
                        if(e.GoodWorld)n.A3+=.5f;
                        // MechQueen projectile cadence does not affect the
                        // eye motor and is reset before the charge counter.
                        if(n.A3>=(queen?120:60))n.A3=0;
                    }
                }
                else if(n.A1==1){Charge(ref n,e.PlayerX,e.PlayerY,(e.Expert?15:12)+(e.GoodWorld?2:0));n.A1=2;}
                else if(n.A1==2){n.A2++;if(n.A2>=25){Damp(ref n,.96f);Snap(ref n);}if(n.A2>=70){n.A3++;n.A2=0;n.Target=255;if(n.A3>=4){n.A1=0;n.A3=0;}else n.A1=1;}}
                if((double)n.Life<(double)n.LifeMax*.4){n.A0=1;n.A1=n.A2=n.A3=0;}return true;
            }
            if(Transform(ref n))return true;
            n.Health.Defense=n.Health.DefaultDefense+10;
            if(n.A1==0)
            {
                float speed=e.Expert?9.5f:8,accel=e.Expert?.175f:.15f;if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}
                if(queen)Smooth(ref n,qx,qy,14,5);else Chase(ref n,e.PlayerX,e.PlayerY-300,speed,accel);
                n.A2++;if(n.A2>=(queen?1200:300)){n.A1=1;n.A2=n.A3=0;Query(ref n,ref e,confused);}
            }
            else
            {
                int side=n.X+n.Width/2<e.PlayerX+e.PlayerWidth/2?-1:1;float speed=e.Expert?9.5f:8,accel=e.Expert?.25f:.2f;if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}
                Chase(ref n,e.PlayerX+side*340,e.PlayerY,speed,accel);
                n.A2++;if(n.A2>=180){n.A1=n.A2=n.A3=0;Query(ref n,ref e,confused);}
            }
            return true;
        }
        private static bool Spazmatism(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,float qx,float qy,bool queen,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!n.MotorRotationCaptured){stop=PredictionStop.MissingDependency;return false;}
            float goal=SpazRotation(ref n,e,queen);
            if(n.A0==0)
            {
                if(n.A1==0)
                {
                    Query(ref n,ref e,confused);float speed=12,accel=.4f;if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}
                    int side=n.X+n.Width/2<e.PlayerX+e.PlayerWidth/2?-1:1;
                    float distance=queen?Smooth(ref n,qx,qy,14,5):Chase(ref n,e.PlayerX+side*400,e.PlayerY,speed,accel);
                    n.A2++;if(n.A2>=(queen?1200:600)){n.A1=1;n.A2=n.A3=0;n.Target=255;}
                    else if(n.Y+n.Height<e.PlayerY-e.PlayerHeight/2 && distance<400){n.A3++;if(e.Expert && (double)n.Life<(double)n.LifeMax*.8)n.A3+=.6f;if(e.GoodWorld)n.A3+=.4f;if(n.A3>=60)n.A3=0;}
                }
                else if(n.A1==1)
                {
                    float speed=13;if(e.Expert){if((double)n.Life<(double)n.LifeMax*.9)speed+=.5f;if((double)n.Life<(double)n.LifeMax*.8)speed+=.5f;if((double)n.Life<(double)n.LifeMax*.7)speed+=.55f;if((double)n.Life<(double)n.LifeMax*.6)speed+=.6f;if((double)n.Life<(double)n.LifeMax*.5)speed+=.65f;}
                    n.MotorRotation=goal;if(e.GoodWorld)speed*=1.2f;Charge(ref n,e.PlayerX,e.PlayerY,speed);n.A1=2;
                }
                else if(n.A1==2){n.A2++;if(n.A2>=8){Damp(ref n,.9f);Snap(ref n);}else n.MotorRotation=(float)Math.Atan2(n.Vy,n.Vx)-1.57f;if(n.A2>=42){n.A3++;n.A2=0;n.Target=255;n.MotorRotation=goal;if(n.A3>=10){n.A1=0;n.A3=0;}else n.A1=1;}}
                if((double)n.Life<(double)n.LifeMax*.4){n.A0=1;n.A1=n.A2=n.A3=0;}return true;
            }
            if(Transform(ref n))return true;
            n.Health.Defense=n.Health.DefaultDefense+18;
            if(n.A1==0)
            {
                int side=n.X+n.Width/2<e.PlayerX+e.PlayerWidth/2?-1:1;
                float tx=e.PlayerX+side*180,ty=e.PlayerY,speed=4,accel=.1f,distance=Length(tx-n.Bounds.CenterX,ty-n.Bounds.CenterY);
                if(!queen)
                {
                    if(e.Expert){if(distance>300)speed+=.5f;if(distance>400)speed+=.5f;if(distance>500)speed+=.55f;if(distance>600)speed+=.55f;if(distance>700)speed+=.6f;if(distance>800)speed+=.6f;}
                    if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}Chase(ref n,tx,ty,speed,accel);
                }
                n.A2++;if(n.A2>=(queen?1200:400)){n.A1=1;n.A2=n.A3=0;n.Target=255;}
                if(queen)
                {
                    MotionRect playerArea=new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
                    // This branch clears target before its SAME flame query.
                    // Native player[255] is a separately observed rectangle,
                    // never the old player's future or an invented zero pose.
                    if(n.Target==255)
                    {if(!n.ClearedTargetAreaCaptured){stop=PredictionStop.MissingDependency;return false;}playerArea=n.ClearedTargetArea;}
                    bool clear;if(terrain==null || !terrain.CanHit(n.Bounds,playerArea,out clear,out stop))
                    {if(stop==PredictionStop.None)stop=PredictionStop.MissingDependency;return false;}
                    float originX=n.Bounds.CenterX,originY=n.Bounds.CenterY;
                    if(clear)
                    {
                        n.L2++;if(n.L2>22)n.L2=0;
                        if(!e.Multiplayer)
                        {
                            n.L1++;if((double)n.Life<(double)n.LifeMax*.75)n.L1++;if((double)n.Life<(double)n.LifeMax*.5)n.L1++;if((double)n.Life<(double)n.LifeMax*.25)n.L1++;if((double)n.Life<(double)n.LifeMax*.1)n.L1+=2;
                            if(n.L1>8)
                            {
                                n.L1=0;float angle=n.MotorRotation+(float)Math.PI/2f;
                                float shotX=(float)Math.Cos(angle)*6f+n.Vx*.5f,shotY=(float)Math.Sin(angle)*6f+n.Vy*.5f;
                                // Original random spread is overwritten by
                                // this Queen direction before vector46 changes.
                                // The mutated origin then feeds the SAME motor.
                                originX=n.Bounds.CenterX-shotX*3f;originY=n.Bounds.CenterY-shotY*3f;
                            }
                        }
                    }
                    SmoothFrom(ref n,qx,qy,originX,originY,14,60);
                }
            }
            else if(n.A1==1){n.MotorRotation=goal;Charge(ref n,e.PlayerX,e.PlayerY,14+(e.Expert?2.5f:0));n.A1=2;}
            else if(n.A1==2){n.A2+=e.Expert?1.5f:1;if(n.A2>=50){Damp(ref n,.93f);Snap(ref n);}else n.MotorRotation=(float)Math.Atan2(n.Vy,n.Vx)-1.57f;if(n.A2>=80){n.A3++;n.A2=0;n.Target=255;if(n.A3>=6){n.A1=0;n.A3=0;}else n.A1=1;}}
            return true;
        }
        private static bool Transform(ref NpcMotionState n)
        {if(n.A0!=1 && n.A0!=2)return false;if(n.A0==1)n.A2=Math.Min(.5f,n.A2+.005f);else n.A2=Math.Max(0,n.A2-.005f);if(n.Style==31 && n.MotorRotationCaptured)n.MotorRotation+=n.A2;n.A1++;if(n.A1>=100){n.A0++;n.A1=0;if(n.A0==3)n.A2=0;}Damp(ref n,.98f);Snap(ref n);return true;}
        private static float SpazRotation(ref NpcMotionState n,PredictionEnvironment e,bool queen)
        {
            float px=e.PlayerX-e.PlayerWidth*.5f,py=e.PlayerY-e.PlayerHeight*.5f;
            float dx=n.X+n.Width/2-px-(int)e.PlayerWidth/2,dy=n.Y+n.Height-59f-py-(int)e.PlayerHeight/2;
            float goal=(float)Math.Atan2(dy,dx)+1.57f;if(goal<0)goal+=6.283f;else if((double)goal>6.283)goal-=6.283f;
            float rate=.15f;if(queen && n.A0==3 && n.A1==0)rate*=.25f;
            if(n.MotorRotation<goal)n.MotorRotation+=(double)(goal-n.MotorRotation)>3.1415?-rate:rate;
            else if(n.MotorRotation>goal)n.MotorRotation+=(double)(n.MotorRotation-goal)>3.1415?rate:-rate;
            if(n.MotorRotation>goal-rate && n.MotorRotation<goal+rate)n.MotorRotation=goal;
            if(n.MotorRotation<0)n.MotorRotation+=6.283f;else if((double)n.MotorRotation>6.283)n.MotorRotation-=6.283f;
            if(n.MotorRotation>goal-rate && n.MotorRotation<goal+rate)n.MotorRotation=goal;return goal;
        }
        internal static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        internal static void Charge(ref NpcMotionState n,float x,float y,float speed)
        {float dx=x-n.Bounds.CenterX,dy=y-n.Bounds.CenterY,d=Length(dx,dy),factor=speed/d;n.Vx=dx*factor;n.Vy=dy*factor;}
        private static float Chase(ref NpcMotionState n,float x,float y,float speed,float accel)
        {float dx=x-n.Bounds.CenterX,dy=y-n.Bounds.CenterY,d=Length(dx,dy),factor=speed/d;Axis(ref n.Vx,dx*factor,accel);Axis(ref n.Vy,dy*factor,accel);return d;}
        private static float Smooth(ref NpcMotionState n,float x,float y,float speed,float inertia)
        {return SmoothFrom(ref n,x,y,n.Bounds.CenterX,n.Bounds.CenterY,speed,inertia);}
        private static float SmoothFrom(ref NpcMotionState n,float x,float y,float originX,float originY,float speed,float inertia)
        {float dx=x-originX,dy=y-originY,d=Length(dx,dy);if(d>speed){float factor=speed/d;dx*=factor;dy*=factor;}n.Vx=(n.Vx*(inertia-1)+dx)/inertia;n.Vy=(n.Vy*(inertia-1)+dy)/inertia;return d;}
        private static void Axis(ref float v,float desired,float accel)
        {if(v<desired){v+=accel;if(v<0 && desired>0)v+=accel;}else if(v>desired){v-=accel;if(v>0 && desired<0)v-=accel;}}
        private static void Damp(ref NpcMotionState n,float factor){n.Vx*=factor;n.Vy*=factor;}
        private static void Snap(ref NpcMotionState n)
        {if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;if((double)n.Vy>-.1 && (double)n.Vy<.1)n.Vy=0;}
    }
}
