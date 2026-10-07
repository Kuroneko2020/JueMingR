using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Known AI3 cooldowns affect the actor's motor, independently of spawned
    // ammunition. Only a new randomized range decision ends this prefix.
    internal static class NpcFighterClockMotion
    {
        internal static bool Shooter(int t)
        {return t==110 || t==111 || t==206 || t==214 || t==215 || t==216 || t==290 || t==291 || t==292 || t==293 || t==350 || t==379 || t==380 || t==381 || t==382 || t>=449 && t<=452 || t==468 || t==481 || t==411 || t==409 || t>=498 && t<=506 || t==424 || t==426 || t==520;}
        internal static bool Waiting(int t){return t>=430 && t<=436 || t==494 || t==495 || t==591;}
        internal static bool Preparing(NpcMotionState n){return n.EffectiveType==471 && n.A3==1;}
        internal static bool Retargets(NpcMotionState n,bool confused)
        {return Shooter(n.EffectiveType) && n.A2>0 && !n.JustHit && !confused && (n.EffectiveType!=411 || n.A1>220);}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            if(Preparing(n)){n.NoGravity=n.NoTileCollide=false;n.L3=0;n.Health.Defense=n.Health.DefaultDefense+10;n.Vx*=.8f;if(++n.A2>=90){n.A3=-2;n.A2=0;}return true;}
            if(Waiting(type))
            {
                if(n.A2!=0){n.A3=1;n.Vx*=.9f;if(Math.Abs(n.Vx)<.1f)n.Vx=0;if(++n.A2>=20 || n.Vy!=0 || e.Day && !e.Remix && n.Y<e.WorldSurface*16)n.A2=0;return true;}
                FighterHorizontalMotion.Step(ref n,e);
                if(n.Vy==0 && (!e.Day || e.Remix || n.Y>e.WorldSurface*16) && !e.PlayerDead)
                {float dx=n.Bounds.CenterX-e.PlayerX,dy=n.Bounds.CenterY-e.PlayerY;int distance=type==494 || type==495?42:50;if(dx*dx+dy*dy<distance*distance){bool clear;if(!t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;if(clear){n.Vx*=.7f;n.A2=1;}}}
                return true;
            }
            bool run=type==381 || type==382 || type==520 || type==411,air=type==426,face=type!=411 || n.A1>220;
            if(n.A1>0)n.A1--;
            bool empowered=type==216 && n.L2>=20;
            if(n.JustHit){if(empowered && n.A2>0)n.L3++;n.A1=30;n.A2=0;}
            if(confused)n.A1=n.A2=0;
            if(n.A2>0)
            {
                if(face)NpcTargeting.Face(ref n,ref e,true,confused);
                int shot=Cooldown(n,empowered)/2;if(type==424 || type==426)shot=Cooldown(n,empowered)-1;if(type==411)shot=220;
                if(type==216 && n.A1==shot){n.L2++;if(empowered)n.L2=n.L3=0;}
                // Projectile aim can change the positive pose number; only
                // positivity is consumed by this actor's subsequent motor.
                if(n.Vy!=0 && !air || n.A1<=0)n.A1=n.A2=0;
                else if(!run || type==411 && n.A1>=120 && n.A1<240){n.Vx*=.9f;n.SpriteDirection=n.Direction;}
            }
            if(!(type==468 && !e.Eclipse) && (n.A2<=0 || run) && (n.Vy==0 || air) && n.A1<=0 && !e.PlayerDead && !n.PlayerAttackHidden)
            {
                bool clear;var target=new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight);
                if(type==520){if(!Line(t,e,new MotionRect(n.Bounds.CenterX,n.Y+20,1,1),target,out clear,out stop))return false;}
                else if(!t.CanHit(n.Bounds,target,out clear,out stop))return false;
                if(clear)
                {
                    float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY-Math.Abs(dx)*.1f,range=type==214?500:type==215?650:type>=498 && type<=506?190:type>=449 && type<=452?200:type==481 || type==468?400:700;
                    float minX=Math.Max(0,Math.Abs(dx)-40),minY=Math.Max(0,Math.Abs(dy)-40),maxX=Math.Abs(dx)+40,maxY=Math.Abs(dy)+40;
                    if(minX*minX+minY*minY<range*range)
                    {if(maxX*maxX+maxY*maxY>=range*range){stop=PredictionStop.RandomDecision;return false;}n.Vx*=.5f;n.A1=Cooldown(n,empowered);n.A2=3;}
                }
            }
            if(n.A2<=0 || run && (type!=411 || n.A1<120 || n.A1>=240) || type==468 && !e.Eclipse)
            {
                float speed=1,acc=.07f,drag=.8f;
                if(type==214){speed=2;acc=.09f;}else if(type==215){speed=1.5f;acc=.08f;}
                else if(type==381 || type==382 || type==411 || type==409){speed=2;acc=.5f;}
                else if(type==520){speed=4;acc=1;drag=.7f;}else if(type==426){speed=4;acc=.6f;drag=.95f;}
                bool brake=false;float dx=n.Bounds.CenterX-e.PlayerX,dy=n.Bounds.CenterY-e.PlayerY,range=type==520?400:300;
                if((type==381 || type==382 || type==520) && dx*dx+dy*dy<range*range)
                {if(!Line(t,e,new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out brake,out stop))return false;if(brake)n.A3=0;}
                if(Math.Abs(n.Vx)>speed || brake){if(n.Vy==0){n.Vx*=drag;n.Vy*=drag;}}
                else if(n.Direction==1 && n.Vx<speed)n.Vx=Math.Min(speed,n.Vx+acc);
                else if(n.Direction==-1 && n.Vx>-speed)n.Vx=Math.Max(-speed,n.Vx-acc);
            }
            return true;
        }
        private static int Cooldown(NpcMotionState n,bool empowered)
        {
            switch(n.EffectiveType)
            {
                case 379:case 380:case 381:case 382:return 80;case 520:return 15;case 350:return 110;
                case 291:return 200;case 292:return 120;case 293:return 90;case 111:return 180;case 206:case 214:return 50;
                case 481:return 100;case 215:return 90;case 290:return 30;case 411:return 330;case 409:case 426:return 60;case 424:return 180;
                case 216:return empowered?Math.Max(60,180-(int)n.L3*20):9;default:return 70;
            }
        }
        // Locked CanHitLine alternates proportional X/Y runs and rejects any
        // solid side cell; CanHit's paired-side rule is not interchangeable.
        // All cells join the same bounded terrain snapshot and failure owner.
        private static bool Line(IPredictionTerrain t,PredictionEnvironment e,MotionRect a,MotionRect b,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;
            if(e.WorldWidth<3 || e.WorldHeight<=40){stop=PredictionStop.TerrainUnavailable;return false;}
            int x=Math.Max(1,Math.Min(e.WorldWidth-1,(int)((a.X+(int)a.Width/2)/16))),y=Math.Max(1,Math.Min(e.WorldHeight-40,(int)((a.Y+(int)a.Height/2)/16))),tx=Math.Max(1,Math.Min(e.WorldWidth-1,(int)((b.X+(int)b.Width/2)/16))),ty=Math.Max(1,Math.Min(e.WorldHeight-40,(int)((b.Y+(int)b.Height/2)/16)));
            int rx=Math.Abs(x-tx),ry=Math.Abs(y-ty),sx=Math.Sign(tx-x),sy=Math.Sign(ty-y),axis=y<ty?2:1;
            if(rx==0 && ry==0){clear=true;return true;}
            float stepX=1,stepY=1,carryX=0,carryY=0;if(rx==0)stepX=0;else if(ry>0 && rx>ry)stepX=(float)rx/ry;if(ry==0)stepY=0;else if(rx>0 && ry>=rx)stepY=(float)ry/rx;
            bool done=false;
            do
            {
                int run;if(axis==2){carryX+=stepX;run=(int)carryX;carryX-=run;}else{carryY+=stepY;run=(int)carryY;carryY-=run;}
                for(int i=0;i<run;i++)
                {
                    bool blocked;if(!LineCell(t,x,y,axis==2,out blocked,out stop))return false;if(blocked)return true;
                    if(rx==0 && ry==0){done=true;break;}
                    if(axis==2){x+=sx;rx--;}else{y+=sy;ry--;}
                    if(rx==0 && ry==0 && run==1)done=true;
                }
                if(axis==2 && ry!=0)axis=1;else if(axis==1 && rx!=0)axis=2;
                PredictionTile center;if(!t.Tile(x,y,out center,out stop))return false;if(center.Active && center.Solid && !center.SolidTop)return true;
            }while(!done);
            clear=true;return true;
        }
        private static bool LineCell(IPredictionTerrain t,int x,int y,bool horizontal,out bool blocked,out PredictionStop stop)
        {
            PredictionTile a,b,c;blocked=false;
            if(!t.Tile(x-(horizontal?0:1),y-(horizontal?1:0),out a,out stop) || !t.Tile(x,y,out b,out stop) || !t.Tile(x+(horizontal?0:1),y+(horizontal?1:0),out c,out stop))return false;
            blocked=a.Active && a.Solid && !a.SolidTop || b.Active && b.Solid && !b.SolidTop || c.Active && c.Solid && !c.SolidTop;return true;
        }
    }
}
