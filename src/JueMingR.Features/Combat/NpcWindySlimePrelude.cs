using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // These are the original AI001 selected-item branches before the -999
    // return, not ordinary ground/jump code. Shared caller must run it once
    // after health/gravity and before testing the original early-return flag.
    internal static class NpcWindySlimePrelude
    {
        internal static bool HeldAfterPrelude(NpcMotionState n)
        {
            if(n.EffectiveType!=1 || n.A0!=-999)return false;
            return !(n.WindySlimeContainsItems && (n.A1==3347 || n.A1==2 && n.Vy==0));
        }
        internal static bool Metal(NpcMotionState n)
        {return n.A1==364 || n.A1==1104 || n.A1==365 || n.A1==1105 || n.A1==366 || n.A1==1106;}
        internal static bool HeldNeedsPlayer(NpcMotionState n)
        {
            // Held ordinary slime consumes no target/jump choice. Metal reads
            // numbered player geometry without an alive gate: active dead or
            // ghost players must not be replaced with an inactive static box.
            return n.WindySlimeContainsItems && Metal(n) && n.PlayerActive;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool held,out PredictionStop stop)
        {
            held=false;stop=PredictionStop.None;
            if(n.EffectiveType!=1)return true;
            if(!n.WindyPreludeCaptured){stop=PredictionStop.MissingDependency;return false;}
            bool contained=n.WindySlimeContainsItems;
            if(contained && n.WindySlimePositiveValue && n.A1==0 && n.WindyNetMode!=1)
            {
                // -4/-5 skip all random item selection in native60990.
                // Other variants can choose3 even in plain rock (descending
                // gravity changes), or150 which mutates terrain. Stop before
                // this actual unobserved choice; an already selected item is
                // modeled below without discarding its known long window.
                bool excluded=n.Identity.NetId==-4 || n.Identity.NetId==-5;
                if(!excluded)
                {stop=PredictionStop.PhaseBoundary;return false;}
                n.A1=-1;
            }
            bool metal=Metal(n);
            bool holdDirection=false;
            if(contained && n.A1>0)
            {
                var h=n.Health;
                if(metal)
                {
                    h.LavaImmune=true;h.Defense=h.DefaultDefense+30;
                    if(n.LifeMax==n.WindyBaseLifeMax)
                    {
                        n.X+=n.Width/2;n.Y+=n.Height;n.Scale*=1.2f;
                        n.Width=(int)(n.Width*n.Scale);n.Height=(int)(n.Height*n.Scale);
                        n.X-=n.Width/2;n.Y-=n.Height;
                        if(n.Life==n.LifeMax)n.Life=n.WindyBaseLifeMax*3;n.LifeMax=n.WindyBaseLifeMax*3;
                    }
                    if(!n.TargetCaptured || !n.HasPlayer || !n.PlayerPreciseAreaCaptured)
                    {stop=PredictionStop.MissingDependency;return false;}
                    var area=NpcTargeting.Area(n,e);bool clear=false;
                    bool overlap=n.Y+n.Height<area.Y && n.X+n.Width>=area.X && n.X<=area.X+area.Width;
                    if(overlap && !CanHitNotSolidOrPlatforms(n.Bounds,area,e,t,out clear,out stop))return false;
                    if(overlap && clear)
                    {
                        holdDirection=true;n.Direction=0;n.Vx*=.5f;if(n.Vy<0)n.Vy=0;n.Vy+=n.Gravity;
                    }
                    else if(n.Vy>0)n.Vy+=n.Gravity;else if(n.Vy<0)n.Vy-=n.Gravity*.75f;
                }
                else if(n.A1==2 && n.Vy==0)n.A0+=9;
                else if(n.A1==3 && n.Vy>0)n.Vy+=n.Gravity*2;
                else if(n.A1==751 && n.Vy!=0)n.Vy-=n.Gravity*.6f;
                else if((n.A1==3736 || n.A1==3737 || n.A1==3738) && n.CollideY && n.OldVy>4)n.Vy=-n.OldVy*.7f;
                else if(n.A1==3347)n.A0++;
                else if(n.A1==29)
                {
                    h.Defense=h.DefaultDefense+4;
                    if(n.LifeMax==n.WindyBaseLifeMax){if(n.Life==n.LifeMax)n.Life=n.WindyBaseLifeMax*2;n.LifeMax=n.WindyBaseLifeMax*2;}
                }
                else if(n.A1==174)
                {
                    h.Defense=h.DefaultDefense+14;
                    if(n.LifeMax==n.WindyBaseLifeMax)
                    {
                        n.Life*=2;n.LifeMax*=2;n.Scale*=1.2f;
                        n.X+=n.Width/2;n.Y+=n.Height;n.Width=(int)(n.Width*1.2f);n.Height=(int)(n.Height*1.2f);n.X-=n.Width/2;n.Y-=n.Height;
                    }
                }
                else if(n.WindySlimeVoiceItem && n.LifeMax==n.WindyBaseLifeMax)
                {if(n.Life==n.LifeMax)n.Life=n.WindyBaseLifeMax*3;n.LifeMax=n.WindyBaseLifeMax*3;}
                else if(n.A1==9 || n.A1==3086 || n.A1==3081)h.Defense=h.DefaultDefense+16;
                else if(n.A1==3609)h.Defense=h.DefaultDefense+8;
                else if(n.A1==8 && e.GoodWorld && !h.FireImmune)NpcHealth.ApplyFire(ref h,216000);
                n.Health=h;
                // Existing150 can create future terrain before this NPC's
                // physics; random53/539 shots change outside actors only.
                // The narrow profile must stop at this actual terrain effect
                // opportunity until its new observed terrain lease restores.
                if(n.WindyNetMode!=1 && (n.A1==150 || n.A1==314 && n.Vy==0 && n.L3<5))
                {stop=PredictionStop.PhaseBoundary;return false;}
            }
            if(n.Direction==0 && !holdDirection)n.Direction=1;
            held=n.A0==-999;return true;
        }

        // Exact Collision.CanHitWithCheck walk with NotSolidOrPlatforms.
        // It uses raw active for the delegate and nactive for corridor solids.
        internal static bool CanHitNotSolidOrPlatforms(MotionRect a,MotionRect b,PredictionEnvironment e,IPredictionTerrain t,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;if(t==null){stop=PredictionStop.MissingDependency;return false;}
            int x=Clamp((int)((a.X+(int)a.Width/2)/16f),1,e.WorldWidth-1),y=Clamp((int)((a.Y+(int)a.Height/2)/16f),1,e.WorldHeight-40);
            int bx=Clamp((int)((b.X+(int)b.Width/2)/16f),1,e.WorldWidth-1),by=Clamp((int)((b.Y+(int)b.Height/2)/16f),1,e.WorldHeight-40);
            while(true)
            {
                if(x==bx && y==by){clear=true;return true;}
                PredictionTile first,second,current;
                if(Math.Abs(x-bx)>Math.Abs(y-by))
                {x+=x>=bx?-1:1;if(!Inside(x,y-1,e) || !Inside(x,y+1,e))return true;if(!Cell(x,y-1,e,t,out first,out stop) || !Cell(x,y+1,e,t,out second,out stop))return false;}
                else
                {y+=y>=by?-1:1;if(!Inside(x-1,y,e) || !Inside(x+1,y,e))return true;if(!Cell(x-1,y,e,t,out first,out stop) || !Cell(x+1,y,e,t,out second,out stop))return false;}
                if(Full(first) && Full(second))return true;
                if(!Cell(x,y,e,t,out current,out stop))return false;
                if(current.Active && current.RawSolid && !current.SolidTop)return true;
                if(current.RawActive && (current.RawSolid || current.Platform))return true;
            }
        }
        private static bool Full(PredictionTile n)
        {return n.Active && n.RawSolid && !n.SolidTop && n.Slope==0 && !n.Half;}
        private static bool Cell(int x,int y,PredictionEnvironment e,IPredictionTerrain t,out PredictionTile n,out PredictionStop stop)
        {
            n=default(PredictionTile);stop=PredictionStop.None;
            return t.Tile(x,y,out n,out stop);
        }
        private static bool Inside(int x,int y,PredictionEnvironment e){return x>=0 && y>=0 && x<e.WorldWidth && y<e.WorldHeight;}
        private static int Clamp(int value,int low,int high){return Math.Max(low,Math.Min(high,value));}
    }
}
