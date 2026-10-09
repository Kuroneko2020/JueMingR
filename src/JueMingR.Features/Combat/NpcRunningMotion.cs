using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcRunningMotion
    {
        internal static bool Known(int t){return t==86 || t==155 || t==315 || t==329 || t==410 || t==423 || t==546;}
        private static bool Count(ref NpcMotionState n,PredictionEnvironment e,bool first=true)
        {
            bool reverse=n.Vy==0 && n.Vx*n.Direction<0;if(reverse)n.A3++;
            if(n.EffectiveType==546 && first){bool grounded=n.Vy==0;n.Vx+=n.RunPushX;n.Vy+=n.RunPushY;if(grounded)n.Vy=0;}
            bool blocked=n.X==n.OldX || n.A3>=30 || reverse;
            if(blocked)n.A3++;else if(n.A3>0)n.A3--;
            if(n.A3>(n.EffectiveType==546?120:300) || n.JustHit)n.A3=0;
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
            if(dx*dx+dy*dy<40000 && !blocked)n.A3=0;return blocked;
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            Count(ref n,e);int t=n.EffectiveType;
            return t==546 && !n.PlayerDesert || n.A3<30 && !((t==315 || t==329) && !e.PumpkinMoon);
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool enteredAttack=false;
            Count(ref n,e,elapsed==1);
            // This finite current neighbor impulse is observed once. Unknown
            // future neighbors retain conditional quality; no background AI
            // or world search is performed by a forecast action.
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
            float distanceSquared=dx*dx+dy*dy;
            bool toward=n.Vx>0 && dx>0 || n.Vx<0 && dx<0;
            if(type==410)
            {
                bool retire=++n.A1>=240;
                if(!retire && n.Vy==0)
                {float px=e.PlayerX-n.Bounds.CenterX,py=e.PlayerY-n.Bounds.CenterY;retire=elapsed==1?n.RunRetirePlayer:!e.PlayerDead && py<0 && Math.Abs(px)<20 && px*px+py*py<640000;}
                if(retire && !e.Multiplayer){n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            else if(type==423)
            {
                if(n.A2==1)
                {
                    n.A1++;n.Vx*=.7f;if(Math.Abs(n.Vx)<.5f)n.Vx=0;
                    // Randomness sets only the next attack cooldown, not this
                    // recovery motor. Keep its guaranteed common prefix and
                    // stop only before the earliest divergent attack decision.
                    if(n.A1>=60){n.A1=-320;n.A2=0;n.UncertainRunCooldown=true;}
                }
                else
                {
                    n.A1++;
                    if(!confused && n.A1>=180 && distanceSquared<250000 && n.Vy==0)
                    {if(n.UncertainRunCooldown){stop=PredictionStop.RandomDecision;return false;}enteredAttack=true;n.A1=0;n.A2=1;}
                    else if(n.Vy==0 && distanceSquared<10000 && Math.Abs(n.Vx)>3 && toward)n.Vy-=4;
                }
            }
            else if((type==155 || type==329) && n.Vy==0 && distanceSquared<10000 && Math.Abs(n.Vx)>3 && toward || type==546 && n.Vy==0 && Math.Abs(n.Vx)>3 && toward)n.Vy-=4;
            if(type==546 && !n.PlayerDesert)
            {int old=n.Direction;NpcTargeting.Face(ref n,ref e,true,confused);if(!n.PlayerDesert){n.TimeLeft=Math.Min(n.TimeLeft,10);n.A3=30;n.Direction=old;}}
            if(n.A3<30)
            {if((type==315 || type==329) && !e.PumpkinMoon)n.TimeLeft=Math.Min(n.TimeLeft,10);else NpcTargeting.Face(ref n,ref e,true,confused);}
            else
            {if(n.Vx==0 && n.Vy==0){if(++n.A0>=2){n.Direction=-n.Direction;n.SpriteDirection=n.Direction;n.A0=0;}}else if(n.Vx!=0)n.A0=0;n.DirectionY=-1;if(n.Direction==0)n.Direction=1;}
            float speed=6,acc=.07f;
            if(!enteredAttack && (n.Vy==0 || n.Wet || n.Vx*n.Direction>=0))
            {
                if(type==155 && n.Vx*n.Direction<0)n.Vx*=.95f;
                else if(type==329){if(n.Vx*n.Direction<0)n.Vx*=.9f;if(n.Vx*n.Direction<3)n.Vx+=n.Direction*.1f;}
                else if(type==315){if(n.Vx*n.Direction<0)n.Vx*=.95f;Motor(ref n,6,.07f);}
                else if(type==410){if(Math.Sign(n.Vx)!=n.Direction)n.Vx*=.9f;acc=.2f;}
                else if(type==423){if(Math.Sign(n.Vx)!=n.Direction)n.Vx*=.85f;speed=10;acc=.2f;}
                else if(type==546){if(Math.Sign(n.Vx)!=n.Direction)n.Vx*=.92f;float wind=n.PlayerSandstorm?(.6f+.4f*Math.Abs(e.WindTarget))*Math.Sign(e.WindTarget):0;speed=4+wind*n.Direction*3;acc=.05f;}
                Motor(ref n,speed,acc);
            }
            if(n.Vy>=0 && !NpcGroundMotion.StepUp(ref n,terrain,out stop))return false;
            if(n.Vy==0 && !Jump(ref n,terrain,out stop))return false;
            if(type==546)n.SpriteDirection=-n.Direction;return true;
        }
        private static void Motor(ref NpcMotionState n,float speed,float acc)
        {if(Math.Abs(n.Vx)>speed){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}}else if(n.Direction==1 && n.Vx<speed)n.Vx=Math.Min(speed,n.Vx+acc);else if(n.Direction==-1 && n.Vx>-speed)n.Vx=Math.Max(-speed,n.Vx-acc);}
        internal static void AfterMove(ref NpcMotionState n)
        {
            // These native FindFrame scalar writes affect the NEXT obstacle
            // jump gate. Animation frames/assets themselves are not predicted.
            int type=n.EffectiveType;
            if((type==86 || type==315) && (n.Vy==0 || n.Wet))
            {float gate=type==86?2:1;n.SpriteDirection=Math.Abs(n.Vx)>gate?Math.Sign(n.Vx):n.Direction;}
            else if(type==329 && n.Vy==0)n.SpriteDirection=n.Direction;
            else if(type==155 && n.Vy==0 && n.Vx!=0)n.SpriteDirection=n.Vx*n.Direction<0 && Math.Abs(n.Vx)<4?n.Direction:Math.Sign(n.Vx);
            else if(type==423 && n.A2!=1 && n.Vy==0 && n.Vx!=0)n.SpriteDirection=-Math.Sign(n.Vx);
        }
        private static bool Jump(ref NpcMotionState n,IPredictionTerrain t,out PredictionStop stop)
        {
            stop=PredictionStop.None;int row=(int)(n.Y-7)/16;PredictionTile tile;
            for(int x=(int)(n.X-7)/16;x<=(int)(n.X+n.Width+7)/16;x++){if(!t.Tile(x,row,out tile,out stop))return false;if(tile.Active && tile.Solid)return true;}
            int type=n.EffectiveType,sprite=n.SpriteDirection;if(type==410 || type==423 || type==546)sprite=-sprite;
            if(!(n.Vx<0 && sprite==-1 || n.Vx>0 && sprite==1))return true;
            int cx=(int)((n.X+n.Width/2+(n.Width/2+2)*n.Direction+n.Vx*5)/16),cy=(int)((n.Y+n.Height-15)/16);
            PredictionTile foot,one,two,three,down,down2,forward;
            if(!t.Tile(cx,cy,out foot,out stop) || !t.Tile(cx,cy-1,out one,out stop) || !t.Tile(cx,cy-2,out two,out stop) || !t.Tile(cx,cy-3,out three,out stop) || !t.Tile(cx,cy+1,out down,out stop) || !t.Tile(cx,cy+2,out down2,out stop) || !t.Tile(cx+n.Direction,cy+3,out forward,out stop))return false;
            if(two.Active && two.Solid)n.Vy=three.Active && three.Solid?-8.5f:-7.5f;
            else if(one.Active && one.Solid && !one.TopSlope)n.Vy=-7;
            else if(n.Y+n.Height-cy*16>20 && foot.Active && foot.Solid && !foot.TopSlope)n.Vy=-6;
            else if((n.DirectionY<0 || Math.Abs(n.Vx)>3) && (!(type==410 || type==423) || !down.Active || !down.Solid) && (!down2.Active || !down2.Solid) && (!forward.Active || !forward.Solid))n.Vy=-8;
            return true;
        }
    }
}
