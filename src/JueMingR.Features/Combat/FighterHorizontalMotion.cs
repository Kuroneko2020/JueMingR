using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // The bounded common AI_003 horizontal branches. This does not claim
    // independent attacks, destinations or every fighter's obstacle policy.
    internal static class FighterHorizontalMotion
    {
        private static readonly float[] zombieSpeeds={2,1,1.5f,3,1.25f,3,3.25f,2,2.75f,1.8f,1.3f,2.5f};
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e)
        {
            int t=n.EffectiveType;float speed=1,acc=.07f,overspeed=.8f,reverse=1;
            bool reverseInAir=false;float threshold=0;
            if(t==159 || t==349){speed=6;reverse=.99f;if(t==159 && n.Vx*n.Direction<0)n.Vx*=.95f;}
            else if(n.CritterTurns)
            {
                float dx=e.PlayerX-n.Bounds.CenterX;
                if(!e.PlayerDead && n.Vy==0 && Math.Abs(dx)>0 && Math.Abs(dx)<100 && n.Vx*dx>0){n.Vy=-4;n.Vx+=n.Direction*3;}
                speed=3.5f;acc=.1f;reverse=.8f;
            }
            else if(t==199){speed=4;acc=.1f;reverse=.8f;}
            else if(t==120 || t==166 || t==213 || t==258 || t==528 || t==529){speed=3;reverse=.99f;}
            else if(Two(t))speed=2;
            else if(t==109){speed=2;acc=.04f;}
            else if(Variable(t))
            {
                speed=1.5f;
                switch(t)
                {
                    case 181:if(e.Remix)speed=3.75f;break;
                    case 294:speed=2;break;case 295:speed=1.75f;break;case 296:speed=1.25f;break;
                    case 201:speed=1.1f;break;case 202:speed=.9f;break;case 203:speed=1.2f;break;
                    case 338:speed=1.75f;break;case 339:speed=1.25f;break;case 340:speed=2;break;
                    case 385:speed=1.8f;break;case 389:speed=2.25f;break;case 462:speed=4;break;
                    case 463:speed=.75f;break;case 466:speed=3.75f;reverse=.9f;threshold=2;reverseInAir=true;break;
                    case 469:speed=3.25f;break;case 480:speed=1.5f+(1f-(float)n.Life/n.LifeMax)*2f;break;
                    case 425:speed=6;break;case 429:speed=4;break;case 631:speed=.9f;break;
                    case 586:speed=1.5f+(1f-(float)n.Life/n.LifeMax)*3.5f;reverse=.9f;threshold=1;break;
                }
                if(t==21 || t==201 || t==202 || t==203 || t==342 || t==635)speed*=1f+(1f-n.Scale);
            }
            else if(t>=269 && t<=280)
            {speed=zombieSpeeds[t-269]*(1f+(1f-n.Scale));}
            else if(t>=305 && t<=314)
            {
                int form=(t-305)%5;speed=form==0?2:form==1?1.25f:form==2?2.25f:form==3?1.5f:1;
                if(t<310)
                {
                    if(n.Vy==0){n.Vx*=.85f;if(n.Vx>-.3f && n.Vx<.3f){n.Vy=-7;n.Vx=speed*n.Direction;}}
                    else if(n.SpriteDirection==n.Direction)n.Vx=(n.Vx*10f+speed*n.Direction)/11f;
                    return true;
                }
            }
            else if(t==67 || t==220 || t==428){speed=.5f;acc=.03f;overspeed=.7f;}
            else if(t==78 || t==79 || t==80 || t==630)
            {speed=n.Life<n.LifeMax/2?2:1;acc=n.Life<n.LifeMax/2?.1f:.05f;if(t==79 || t==630)speed*=1.5f;overspeed=.7f;}
            else if(t==287){speed=5;acc=.2f;overspeed=.7f;}
            else if(t==243 || t==251)
            {float damage=1f-(float)n.Life/n.LifeMax;speed=1+damage*(t==243?1.5f:2);acc=(t==243?.07f:.08f)+damage*(t==243?.15f:.2f);overspeed=.7f;}
            else if(t==386)
            {if(n.A2>0){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}return true;}speed=1.5f;acc=.15f;overspeed=.7f;}
            else if(t==460)
            {speed=3+(1f-(float)n.Life/n.LifeMax)*3;acc=.1f;for(float at=2;at<=5.5f;at+=.5f)if(Math.Abs(n.Vx)>at)acc*=.8f;overspeed=.7f;reverse=.93f;reverseInAir=true;}
            else if(t==508 || t==580 || t==582)
            {
                float magnitude=Math.Abs(n.Vx),weight=t==582?7:10;speed=t==582?2.25f:2.5f;
                if(t==582){if(magnitude>2.5f){speed=3;weight+=75;}else if(magnitude>2){speed=2.75f;weight+=55;}}
                else if(magnitude>2.75f){speed=3.5f;weight+=80;}else if(magnitude>2.25f){speed=3;weight+=60;}
                if(Math.Abs(n.Vy)<.5f && n.Vx*n.Direction<0){n.Vx*=.95f;n.Vy*=.95f;}
                if(Math.Abs(n.Vy)>n.Gravity)weight*=t==582?2:3;
                if(n.Vx*n.Direction>=0 && n.Direction!=0)n.Vx=(n.Vx*weight+n.Direction*speed)/(weight+1);
                else if(Math.Abs(n.Bounds.CenterX-e.PlayerX)>20 && Math.Abs(n.Vy)<=n.Gravity){n.Vx*=.99f;n.Vx+=n.Direction*.025f;}
                return true;
            }
            else if(t==391 || t==427 || t==415 || t==419 || t==518 || t==532)
            {speed=t==427 || t==419?6:t==415?4:5;acc=t==427?.2f:t==415 || t==518?.1f:t==419 || t==532?.15f:.25f;overspeed=t==427?.8f:t==415 || t==518?.95f:t==419?.85f:t==532?.98f:.7f;}
            else if(t>=430 && t<=436 || t==494 || t==495 || t==591)
            {if(n.A2!=0)return false;speed=1f+(1f-n.Scale);}
            else if(Independent(t))return false;
            else
            {
                switch(t){case 624:speed=2.5f;break;case 186:speed=1.1f;break;case 187:speed=.9f;break;case 188:speed=1.2f;break;case 189:case 632:speed=.8f;break;case 132:speed=.95f;break;case 200:speed=.87f;break;case 223:speed=1.05f;break;case 691:speed=.85f;break;}
                if(t==489){float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,distance=(float)Math.Sqrt(dx*dx+dy*dy)*.0025f;if(distance>1.5f)distance=1.5f;speed=(e.Expert?3-distance:2.5f-distance)*.8f;}
                if(t==489 || t==3 || t==132 || t>=186 && t<=189 || t==200 || t==223 || t==331 || t==332)speed*=1f+(1f-n.Scale);
            }
            if(n.Vx<-speed || n.Vx>speed){if(n.Vy==0){n.Vx*=overspeed;n.Vy*=overspeed;}}
            else if(n.Direction==1 && n.Vx<speed || n.Direction==-1 && n.Vx>-speed)
            {if((n.Vy==0 || reverseInAir) && n.Vx*n.Direction< -threshold)n.Vx*=reverse;n.Vx=n.Direction==1?Math.Min(speed,n.Vx+acc):Math.Max(-speed,n.Vx-acc);}
            if(t==462 && n.Vy==0 && n.Vx*n.Direction<0)n.Vx*=.9f;
            return true;
        }
        private static bool Two(int t)
        {return t==461 || t==27 || t==77 || t==104 || t==163 || t==162 || t==196 || t==197 || t==212 || t==257 || t==326 || t==343 || t==348 || t==351 || t>=524 && t<=527 || t==530 || t==236;}
        private static bool Variable(int t)
        {
            switch(t){case 21:case 26:case 31:case 294:case 295:case 296:case 47:case 73:case 140:case 164:case 239:case 167:case 168:case 185:case 198:case 201:case 202:case 203:case 217:case 218:case 219:case 226:case 181:case 254:case 338:case 339:case 340:case 342:case 385:case 389:case 462:case 463:case 466:case 464:case 469:case 470:case 480:case 482:case 425:case 429:case 586:case 631:case 635:return true;default:return false;}
        }
        private static bool Independent(int t)
        {return t==110 || t==111 || t==206 || t==214 || t==215 || t==216 || t==290 || t==291 || t==292 || t==293 || t==350 || t==379 || t==380 || t==381 || t==382 || t>=449 && t<=452 || t==468 || t==481 || t==411 || t==409 || t>=498 && t<=506 || t==424 || t==426 || t==520;}
    }
}
