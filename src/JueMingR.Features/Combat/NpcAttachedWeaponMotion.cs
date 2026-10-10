using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked NPC.cs AI75/76/93. Anchors read the owner's state at their native
    // actor slot, including rotation and post-AI movement. They are not a
    // frozen translation of a mount and never run native AI or create shots.
    internal static class NpcAttachedWeaponMotion
    {
        internal static bool Known(NpcMotionState n)
        {int t=n.EffectiveType;return n.Style==75 && (t==390 || t==392 || t==393 || t==394 || t==416 || t==492) || t==395 && n.Style==76 || t==491 && n.Style==93;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.A0==b.A0 && a.EffectiveType==b.EffectiveType;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.EffectiveType==491 || n.EffectiveType==390 || n.EffectiveType==393 || n.EffectiveType==492 || n.EffectiveType==395 && (!n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.EffectiveType==491 || n.EffectiveType==393 && !(n.A3>=280 && n.A3<420) || n.EffectiveType==492 && n.A3==1 || n.EffectiveType==395 && (!n.TargetCaptured || !n.PlayerActive || n.PlayerNativeDead || n.Target<0 || n.Target==255 || Length(n.PlayerCenterX-n.Bounds.CenterX,n.PlayerCenterY-n.Bounds.CenterY)>5600);}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0)
        {if(n.EffectiveType==390){int i=Find(n.PositionOwner,group,count);if(i>=0)return NpcGroundMotion.PlayerPremiseTarget(group[i],e);}return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!n.WeaponRelationsCaptured || !n.WeaponRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
            if(n.EffectiveType==395)return Saucer(ref n,e,terrain,confused,out stop);
            if(n.EffectiveType==491)return Ship(ref n,e,terrain,group,count,confused,out stop);
            int type=n.EffectiveType;
            if((type==390 || type==416) && n.L0==0 && !e.Multiplayer)
            {stop=PredictionStop.PhaseBoundary;return false;} // real owner birth has not been observed
            if(!n.PositionOwnerCaptured){stop=PredictionStop.MissingDependency;return false;}
            int owner=Find(n.PositionOwner,group,count);
            if(!n.PositionOwnerValid || owner>=0 && !group[owner].Active)
            {
                // Native Transform is server/single-player only. A client
                // with an observed missing owner retains this old form until
                // the server's real transformed sample arrives.
                if(e.Multiplayer && (type==390 || type==416))return true;
                if(type==390)return NpcPhasedFlightMotion.TransformKnown(ref n,ref e,382,18,40,3,25,false,out stop);
                if(type==416)return NpcPhasedFlightMotion.TransformKnown(ref n,ref e,518,22,56,3,28,false,out stop);
                n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;
            }
            if(owner<0){stop=PredictionStop.MissingDependency;return false;}
            var p=group[owner];float ox=0,oy=0;
            n.TimeLeft=Math.Max(n.TimeLeft,60);
            if(type==390)oy=-14;
            else if(type==416){ox=-p.SpriteDirection*10;oy=-30;}
            else
            {
                if(!p.MotorRotationCaptured){stop=PredictionStop.MissingDependency;return false;}
                if(type==392)oy=2;
                else if(type==393){ox=(n.A1==1?1:-1)*60;oy=29;}
                else if(type==394){ox=(n.A1==1?1:-1)*49;oy=-13;}
                else if(type==492){ox=(-122+68*n.A1)*(p.SpriteDirection==1?-1:1);oy=-6;}
                ox*=p.Scale;oy*=p.Scale;Rotate(ref ox,ref oy,p.MotorRotation);n.MotorRotation=p.MotorRotation;
            }
            if(type==392 && !e.Multiplayer && p.A0>=0 && p.A0<1 && !Alive(n.WeaponPart0,393,group,count) && !Alive(n.WeaponPart1,393,group,count) && !Alive(n.WeaponPart2,394,group,count) && !Alive(n.WeaponPart3,394,group,count))
            {p.A0=e.Expert?1:3;p.A1=p.A2=p.A3=0;group[owner]=p;}
            n.Vx=n.Vy=0;n.X=p.Bounds.CenterX-n.Width/2+ox;n.Y=p.Bounds.CenterY-n.Height/2+oy;
            n.Direction=p.Direction;n.SpriteDirection=type==394?(n.A1==1?1:-1):p.SpriteDirection;
            if(type==416)return true;
            n.TimeLeft=p.TimeLeft;
            if(type==390)
            {
                n.Vx=p.Vx;n.Vy=p.Vy;
                // The numbered target is copied from the owner before its
                // geometry-dependent half-speed branch. Preserve all facts.
                n.Target=p.Target;n.PlayerIndex=p.PlayerIndex;n.PlayerArea=p.PlayerArea;n.HasPlayer=p.HasPlayer;n.PlayerDead=p.PlayerDead;
                n.PlayerActive=p.PlayerActive;n.PlayerNativeDead=p.PlayerNativeDead;n.PlayerGhost=p.PlayerGhost;n.PlayerCenterX=p.PlayerCenterX;n.PlayerCenterY=p.PlayerCenterY;n.TargetNoAggro=p.TargetNoAggro;
                n.TargetCaptured=p.TargetCaptured;n.PlayerVelocityCaptured=p.PlayerVelocityCaptured;n.PlayerVx=p.PlayerVx;n.PlayerVy=p.PlayerVy;n.PlayerWet=p.PlayerWet;n.PlayerIdle=p.PlayerIdle;n.PlayerGraveyard=p.PlayerGraveyard;n.PlayerCrimson=p.PlayerCrimson;n.PlayerJungle=p.PlayerJungle;
                e=NpcTargeting.Player(n,e);
                if(n.A1<60)n.A1++;if(n.JustHit)n.A1=-30;
                bool clear;if(!Clear(n,e,terrain,false,out clear,out stop))return false;
                float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;
                if(clear && Length(x,y)<700)
                {
                    if(n.A1==60 && Math.Sign(x)==n.Direction)n.A1=-60; // random shot does not alter this motor
                    else
                    {
                        n.Vx*=.5f;float ux,uy;Unit(x,y,out ux,out uy);n.A2=3;
                        if(Math.Abs(uy)>Math.Abs(ux)*2)n.A2=uy>0?1:5;
                        else if(Math.Abs(ux)>Math.Abs(uy)*2)n.A2=3;else n.A2=uy>0?2:4;
                    }
                }
            }
            else if(type==492)
            {
                n.Vx=p.Vx;n.Vy=p.Vy;if(n.A3<240)n.A3++;
                if(n.A3==2)NpcTargeting.Face(ref n,ref e,false,confused);
                // A2 is a visual aim sector, not a motor input. The firing
                // clock is retained because its reset belongs to the AI state.
                bool clear;if(!Clear(n,e,terrain,false,out clear,out stop))return false;
                if(clear && n.A3>=240)n.A3=0;
                else if(clear)
                {
                    int best=0;float distance=float.MaxValue;
                    for(int i=0;i<8;i++){float x=0,y=1;Rotate(ref x,ref y,i*(-(float)Math.PI/4));x*=50;y*=50;float d=Length(n.Bounds.CenterX+x-e.PlayerX,n.Bounds.CenterY+y-e.PlayerY);if(i==0 || d<distance){best=i;distance=d;}}
                    n.A2=n.SpriteDirection==1?8-best:best+1;
                }
                else n.A2=0;
            }
            else
            {
                n.A3=p.A3;
                if(type==393 && !(n.A3>=280 && n.A3<420))
                {NpcTargeting.Face(ref n,ref e,false,confused);float x=e.PlayerX-n.Bounds.CenterX,y=Math.Max(0,e.PlayerY-n.Bounds.CenterY),ux,uy;Unit(x,y,out ux,out uy);if(float.IsNaN(ux) || float.IsNaN(uy)){ux=0;uy=1;}n.A2=(float)Math.Atan2(uy,ux);}
            }
            return true;
        }
        private static bool Saucer(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L3==0 && !e.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}
            if(n.A0==3){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            bool invalid=n.TargetCaptured?!n.PlayerActive || n.PlayerNativeDead:e.PlayerDead;
            if(invalid || n.Target<0 || n.Target==255 || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>5600)
            {NpcTargeting.Face(ref n,ref e,true,confused);invalid=n.TargetCaptured?!n.PlayerActive || n.PlayerNativeDead:e.PlayerDead;}
            if((invalid || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>5600) && n.A0!=1)
            {if(n.A0==0)n.A0=-1;if(n.A0==2)n.A0=-2;}
            if(n.A0==-1 || n.A0==-2)
            {
                n.Vy-=.4f;n.TimeLeft=Math.Min(n.TimeLeft,10);
                if(!(n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead))
                {n.TimeLeft=300;if(n.A0==-2)n.A0=2;n.A1=n.A2=n.A3=0;}
            }
            else if(n.A0==1)
            {
                n.Health.DontTakeDamage=false;n.CanReceive=true;n.Vx*=.96f;n.Vy*=.96f;n.A1++;
                if(n.A1>=150){n.A0=2;n.A1=0;n.MotorRotation=0;}
                else if(n.A1<120)
                {float divisor=n.A1<40?40:n.A1<80?20:10,amplitude=n.A1<40?.2f:n.A1<80?.3f:.4f;float x=0,y=1;Rotate(ref x,ref y,n.A1/divisor*((float)Math.PI*2));n.MotorRotation=y*amplitude;}
                else n.MotorRotation=(n.A1-120)/30*((float)Math.PI*2);
            }
            else if(n.A0==2)
            {
                int previous=n.A3%120>=60?1:0;n.A3++;int phase=n.A3%120>=60?1:0;
                if(phase!=previous && phase==1)n.A2=Math.Sign(e.PlayerX-n.Bounds.CenterX)==1?1:-1;
                if(n.A3>=3600){n.A0=2;n.A1=n.A2=n.A3=0;}
                else if(phase==0){if(!Steer(ref n,e.PlayerX+n.A2*350-n.Bounds.CenterX,e.PlayerY-250-n.Bounds.CenterY,16,out stop))return false;}
                else{float height;if(!Below(n,e,terrain,out height,out stop))return false;Height(ref n,height,250);n.Vx=8*n.A2;}
                n.MotorRotation=0;
            }
            else if(n.A0==0)
            {
                int prior=Stage(n.A3);if(++n.A3>=600)n.A3=0;int phase=Stage(n.A3);
                if(phase!=prior){if(phase==0 || phase==2)n.A2=0;else if(phase==1)n.A2=Math.Sign(e.PlayerX-n.Bounds.CenterX)==1?1:-1;}
                float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY,height;
                if(phase==0)
                {if(n.A2==0)n.A2=-600*Math.Sign(-x);x+=n.A2;y-=250;if(Length(x,y)<50)n.A3=19;else if(!Steer(ref n,x,y,16,out stop))return false;}
                else if(phase==1)
                {if(!Below(n,e,terrain,out height,out stop))return false;Height(ref n,height,250);n.Vx=3.5f*n.A2;}
                else if(phase==2)
                {if(n.A2==0)n.A2=300*Math.Sign(-x);x+=n.A2;y-=170;if(!Below(n,e,terrain,out height,out stop))return false;if(height<170)y-=170-height;if(Length(x,y)<70)n.A3=279;else if(!Steer(ref n,x,y,20,out stop))return false;}
                else if(phase==3)
                {if(!Below(n,e,terrain,out height,out stop))return false;if(height<170)n.Vy=n.Vy+(Math.Max(-4,-height)-n.Vy)*.05f;else n.Vy*=.85f;n.Vx*=.85f;}
                else if(phase==4){y-=250;if(Length(x,y)<50)n.A3=439;else if(!Steer(ref n,x,y,16,out stop))return false;}
                else{n.Vx*=.85f;n.Vy*=.85f;}
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.X<-100 || n.Y<-100 || n.X>e.WorldWidth*16+100 || n.Y>e.WorldHeight*16+100)
            {n.X=Math.Max(-100,Math.Min(e.WorldWidth*16+100,n.X));n.Y=Math.Max(-100,Math.Min(e.WorldHeight*16+100,n.Y));n.Active=false;stop=PredictionStop.Despawn;return false;}
            return true;
        }
        private static bool Ship(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L0==0 && !e.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}
            bool any=false;for(int i=0;i<4;i++)
            {var id=Part(n,i);bool alive=Alive(id,492,group,count);if(alive)any=true;else SetAi(ref n,i,-1);}
            if(!any){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            if(++n.L3>=64)n.L3=0;NpcTargeting.Face(ref n,ref e,true,confused);
            float height;if(!Below(n,e,terrain,out height,out stop,Math.Sign(n.Vx)*10))return false;
            if(height<350)n.Vy=n.Vy+(Math.Max(-4,height-350)-n.Vy)*.05f;
            else if(height>450)n.Vy=n.Vy+(Math.Min(4,height-350)-n.Vy)*.05f;else n.Vy*=.95f;
            if(Math.Abs(e.PlayerX-n.Bounds.CenterX)>=300 && (Math.Abs(n.Vx)<6 || Math.Sign(n.Vx)!=n.Direction))n.Vx+=n.Direction*.06f;
            n.MotorRotation=n.Vx*.025f;n.SpriteDirection=-Math.Sign(n.Vx);return true;
        }
        // AI415's independent action precedes the shared ground motor. Its
        // spawned projectiles are omitted, but a future new 516 cannot be
        // reused as a frozen global-count fact by another ready actor.
        internal static bool Prelude415(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,NpcMotionState[] group,int count,out bool handled,out PredictionStop stop,bool confused=false)
        {
            handled=false;stop=PredictionStop.None;
            if(n.JustHit)n.A2=120;
            if(n.A2>0)n.A2--;
            if(n.A2==0)
            {
                if(!n.WeaponRelationsCaptured || !n.WeaponRelationsComplete || n.WeaponSpawnAt>0 && n.WeaponSpawnAt<=elapsed){stop=PredictionStop.MissingDependency;return false;}
                int existing=0;for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==516)existing++;
                if(existing>6)n.A2=90;
                else if((e.PlayerX<n.Bounds.CenterX && n.Direction<0 || e.PlayerX>n.Bounds.CenterX && n.Direction>0))
                {bool clear;if(!Clear(n,e,terrain,true,out clear,out stop))return false;if(clear){n.A2=-1;NpcTargeting.Face(ref n,ref e,true,confused);}}
            }
            else if(n.A2<0 && n.A2>-42)
            {
                n.A2--;if(n.A2==-42){stop=PredictionStop.RandomDecision;return false;}n.Vx*=.8f;handled=true;
                if(n.A2==-18 || n.A2==-26 || n.A2==-34)
                {
                    n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;
                    if(n.Vx>-.5f && n.Vx<.5f)n.Vx=0;
                    if(!e.Multiplayer)
                    {n.WeaponSpawnAt=Math.Max(1,elapsed);for(int i=0;i<count;i++)if(group[i].EffectiveType==415)group[i].WeaponSpawnAt=n.WeaponSpawnAt;}
                }
            }
            return true;
        }
        private static int Stage(float clock){return clock>=580?0:clock>=440?5:clock>=420?4:clock>=280?3:clock>=260?2:clock>=20?1:0;}
        private static bool Below(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out float height,out PredictionStop stop,int ahead=0)
        {
            height=0;stop=PredictionStop.None;if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
            int x=(int)n.Bounds.CenterX/16+ahead,y=(int)(n.Y+n.Height)/16,below=0;PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;
            if(tile.Active && tile.Solid && !tile.SolidTop)below=1;
            else for(;below<150 && y+below<e.WorldHeight;below++){if(!terrain.Tile(x,y+below,out tile,out stop))return false;if(tile.Active && tile.Solid && !tile.SolidTop){below--;break;}}
            height=below*16;return true;
        }
        private static void Height(ref NpcMotionState n,float height,float desired){if(height<desired)n.Vy=n.Vy+(Math.Max(-4,-height)-n.Vy)*.05f;else n.Vy*=.95f;}
        private static bool Clear(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool centers,out bool clear,out PredictionStop stop)
        {clear=false;stop=PredictionStop.None;if(terrain==null){stop=PredictionStop.MissingDependency;return false;}return terrain.CanHit(centers?new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1):n.Bounds,centers?new MotionRect(e.PlayerX,e.PlayerY,1,1):new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight),out clear,out stop);}
        private static bool Steer(ref NpcMotionState n,float x,float y,float speed,out PredictionStop stop)
        {stop=PredictionStop.None;if(x==0 && y==0){stop=PredictionStop.InvalidState;return false;}float ux,uy;Unit(x,y,out ux,out uy);ux*=speed;uy*=speed;n.Vx=n.Vx+(ux-n.Vx)*.1f;n.Vy=n.Vy+(uy-n.Vy)*.1f;return true;}
        private static int Find(NpcIdentity id,NpcMotionState[] group,int count){if(id.Token==null || group==null)return -1;for(int i=0;i<count;i++)if(group[i].Identity.Equals(id))return i;return -1;}
        private static bool Alive(NpcIdentity id,int type,NpcMotionState[] group,int count){int i=Find(id,group,count);return i>=0 && group[i].Active && group[i].EffectiveType==type;}
        private static NpcIdentity Part(NpcMotionState n,int i){return i==0?n.WeaponPart0:i==1?n.WeaponPart1:i==2?n.WeaponPart2:n.WeaponPart3;}
        private static void SetAi(ref NpcMotionState n,int i,float value){if(i==0)n.A0=value;else if(i==1)n.A1=value;else if(i==2)n.A2=value;else n.A3=value;}
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static void Unit(float x,float y,out float ux,out float uy){double inverse=1d/Length(x,y);ux=(float)(x*inverse);uy=(float)(y*inverse);}
        private static void Rotate(ref float x,ref float y,float angle){float cosine=(float)Math.Cos(angle),sine=(float)Math.Sin(angle),old=x;x=(float)((double)old*cosine-(double)y*sine);y=(float)((double)old*sine+(double)y*cosine);}
    }
}
