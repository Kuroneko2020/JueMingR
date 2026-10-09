using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcFighterFlightMotion
    {
        internal static bool Known(int type){return type==425 || type==427 || type==426;}
        internal static bool Retargets(NpcMotionState n,bool confused=false)
        {return n.Vy!=0 && (n.EffectiveType==426 || n.A2==1) && (n.EffectiveType!=425 || !confused);}
        internal static bool Form(ref NpcMotionState n,ref PredictionEnvironment e,IPredictionTerrain terrain,out bool changed,out PredictionStop stop)
        {
            changed=false;stop=PredictionStop.None;if(n.EffectiveType!=427)return true;
            n.L0+=1+Math.Abs(n.Vx)/2;if(n.L0<1200 || e.Multiplayer)return true;
            int x=(int)n.Bounds.CenterX/16-2,y=(int)n.Bounds.CenterY/16-3;
            if(x<0 || x+4>=e.WorldWidth || y<0 || y+4>=e.WorldHeight-40)return true;
            for(int tx=x;tx<=x+4;tx++)for(int ty=y;ty<=y+4;ty++)
            {PredictionTile tile;if(!terrain.Tile(tx,ty,out tile,out stop))return false;if(tile.Active && tile.Solid && !tile.SolidTop)return true;}
            // Only the locked normal-size 427 -> 426 reset is modeled. The
            // current world's difficulty-scaled max life is observed without
            // constructing an NPC or executing SetDefaults/RNG in the Source.
            if(e.GoodWorld || n.FighterFormLifeMax<=0){stop=PredictionStop.PhaseBoundary;return false;}
            n.Y+=n.Height-62;n.Width=50;n.Height=62;n.MotionType=426;n.Style=3;
            n.NoGravity=n.NoTileCollide=false;n.Scale=1;n.Alpha=0;n.TimeLeft=750;
            n.A0=n.A1=n.A2=n.A3=n.L0=n.L1=n.L2=n.L3=0;n.CollideX=n.CollideY=n.JustHit=false;n.WetCount=0;
            n.Life=n.LifeMax=n.FighterFormLifeMax;n.Health.Defense=n.Health.DefaultDefense=44;n.Health.RealLife=-1;n.Health.DamageMultiplier=1;n.Health.Regen=n.Health.RegenCount=0;n.Health.DontTakeDamage=n.Health.Immortal=n.Health.LavaImmune=false;
            n.CanReceive=n.CanHarm=true;n.NoContactDamage=false;n.WaterSpeed=n.LavaSpeed=.5f;n.HoneySpeed=.25f;
            // SetDefaults clears the old target before Transform's query.
            // Keeping it would wrongly suppress facing an idle negative-aggro
            // player; later ordinary queries keep their normal suppression.
            n.Target=255;NpcTargeting.Face(ref n,ref e,true,false);n.NewSegment=true;changed=true;
            // Native returns immediately after Transform; retain old velocity
            // and pre-AI gravity for this action's ordinary physical tail.
            return true;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,int elapsed,out bool jumping,out PredictionStop stop)
        {
            jumping=false;stop=PredictionStop.None;int type=n.EffectiveType;
            if(type==426)
            {
                if(n.A1>0 && n.Vy>0){n.Vy*=.85f;if(n.Vy==0)n.Vy=-.4f;}
                bool visible;
                if(n.Vy!=0)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);n.SpriteDirection=n.Direction;
                    if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out visible,out stop))return false;
                    if(visible)
                    {float dx=e.PlayerX-n.Direction*300-n.Bounds.CenterX;if(dx<40 && n.Vx>0 || dx>40 && n.Vx<0)n.Vx*=.98f;if(dx<40 && n.Vx>-6)n.Vx-=.2f;else if(dx>40 && n.Vx<6)n.Vx+=.2f;n.Vx=Math.Max(-6,Math.Min(6,n.Vx));}
                }
                else if(e.PlayerY+100<n.Y)
                {if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out visible,out stop))return false;if(visible){jumping=true;n.Vy=-6;}}
                if(elapsed==1){n.Vx+=n.RunPushX;n.Vy+=n.RunPushY;}return true;
            }
            if(type==425 && n.L3==0){n.L3=1;n.A3=-120;}
            if(n.Vy==0 || type==425 && confused)n.A2=0;
            bool clear;
            if(Retargets(n,confused))
            {
                NpcTargeting.Face(ref n,ref e,true,confused);n.SpriteDirection=-n.Direction;
                // Native point queries use size zero; size one preserves the
                // same integer center cell while satisfying bounded Area input.
                if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;
                if(clear)
                {
                    int sign=confused?-n.Direction:n.Direction;
                    float dx=e.PlayerX-n.Bounds.CenterX-(type==425?sign*300:0),dy=e.PlayerY+(type==425?e.PlayerHeight/2:0)-(type==425?n.Y+n.Height:n.Bounds.CenterY);
                    if(dx<0 && n.Vx>0 || dx>0 && n.Vx<0)n.Vx*=type==425?.9f:.98f;
                    float cap=type==425?7:6,acc=type==425?.3f:.015f,threshold=type==425?0:20;
                    if(dx< -threshold && n.Vx> -cap)n.Vx-=acc;else if(dx>threshold && n.Vx<cap)n.Vx+=acc;n.Vx=Math.Max(-cap,Math.Min(cap,n.Vx));
                    if(dy< -20 && n.Vy>0 || dy>20 && n.Vy<0)n.Vy*=type==425?.8f:.98f;
                    float verticalCap=type==425?8:6,verticalAcc=type==425?.3f:.15f;
                    if(dy< -20 && n.Vy> -verticalCap)n.Vy-=verticalAcc;else if(dy>20 && n.Vy<verticalCap)n.Vy+=verticalAcc;
                }
                // Current nearby same-type impulses are observed once. Future
                // neighbor AI remains a declared condition, not a field scan.
                if(elapsed==1){n.Vx+=n.RunPushX;n.Vy+=n.RunPushY;}
            }
            else if((type!=425 || !confused) && e.PlayerY+100<n.Y)
            {
                if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                if(clear){jumping=true;n.Vy=-5;n.A2=1;}
            }
            if(type==425)
            {
                if(n.A3<0)n.A3++;
                if(confused)n.A3=0;
                // The 0..30 firing-readiness band only affects shots/frames;
                // it remains below the shared motor's 60 facing threshold.
                // Preserve motion without inventing projectile/RNG outcomes.
                if(n.A3>=31 && ++n.A3>=40)n.A3=-150;
            }
            return true;
        }
    }
}
