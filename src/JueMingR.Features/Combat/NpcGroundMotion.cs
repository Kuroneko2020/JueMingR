using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Ordinary fighter decisions consume immutable tile values, never a live
    // NPC, WorldGen operation or RNG. Support is sampled before StepUp, as in
    // native AI; rechecking it afterwards changes obstacle and pit decisions.
    internal static class NpcGroundMotion
    {
        internal static bool Known(int type){return type==3 || type==21 || type==27 || type==77 || type==109 || type==120 || type==166 || NpcWallMotion.Ground(type);}
        internal static int PlayerPremiseTarget(NpcMotionState n,PredictionEnvironment e)
        {
            // Compute only the first real targeting decision on a value copy.
            // This shares the blocked-count/pursuit rule with movement instead
            // of assuming every fighter always keeps its old numbered target.
            if(FighterHorizontalMotion.IndependentEntry(n))
            {if(FighterHorizontalMotion.RetargetOnEntry(n))Target(ref n,ref e,false);}
            else if(!Known(n.EffectiveType) && !OrdinaryCounter(n))
            {if(n.A3<=0)Target(ref n,ref e,false);}
            else if(n.EffectiveType==166 && n.A2<0)Target(ref n,ref e,false);
            else CountAndTarget(ref n,ref e,false,n.EffectiveType==120?180:60);
            return n.PlayerIndex;
        }
        internal static bool Fallback(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            // Dormant/reveal and spawn/fluid actions run before the blocked
            // counter and common motor. Their future action remains a trend,
            // but an actual entry TargetClosest must share Source's premise.
            if(FighterHorizontalMotion.IndependentEntry(n))
            {if(FighterHorizontalMotion.RetargetOnEntry(n))Target(ref n,ref e,confused);NpcRollingMotion.Trend(ref n,elapsed);return true;}
            // These original actions skip the shared blocked counter. Their
            // positive ai[3] belongs to an independent action, not recovery.
            bool wasStopped=n.Vx==0 && !n.JustHit;
            if(!OrdinaryCounter(n))
            {if(n.A3>0){stop=PredictionStop.UnsupportedMechanism;return false;}Target(ref n,ref e,confused);}
            else CountAndTarget(ref n,ref e,confused,60);
            // Aerial target steering is an independent vector action. Until
            // that action is modeled, continue the finite observed trend;
            // common ground parameters are not permission to invent a jump.
            int type=n.EffectiveType;
            bool aerial=n.Vy!=0 && (type==258 || (type==425 || type==427) && n.A2==1);
            if(aerial || !FighterHorizontalMotion.Step(ref n,e))NpcRollingMotion.Trend(ref n,elapsed);
            return Common(ref n,e,t,wasStopped,out stop);
        }
        private static bool OrdinaryCounter(NpcMotionState n)
        {
            int type=n.EffectiveType;if(type==425 || type==471)return false;
            if(n.A2<=0)return true;
            switch(type)
            {
                case 110:case 111:case 206:case 214:case 215:case 216:
                case 291:case 292:case 293:case 350:case 379:case 380:
                case 381:case 382:case 409:case 411:case 424:case 426:
                case 466:case 498:case 499:case 500:case 501:case 502:
                case 503:case 504:case 505:case 506:case 520:return false;
                default:return true;
            }
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            if(e.PlayerY+e.PlayerHeight/2==n.Y+n.Height)n.DirectionY=-1;
            if(type==166 && n.A2<0)
            {
                Target(ref n,ref e,confused);bool clear;
                if(!t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;
                if(n.JustHit || clear)n.A2=0;
                else{n.Vx*=.9f;if(n.Vx>-.1f && n.Vx<.1f)n.Vx=0;if(++n.A2==0)n.Vx=n.Direction*.1f;return true;}
            }
            bool wasStopped=n.Vx==0 && !n.JustHit;int limit=type==120?180:60;
            CountAndTarget(ref n,ref e,confused,limit);
            // Armored skeleton shares ordinary blocked/turn/step decisions,
            // but its locked .8 speed is 2, not the later fighter default 3.
            FighterHorizontalMotion.Step(ref n,e);
            if(NpcWallMotion.Ground(type) && !NpcWallMotion.GroundAttachment(ref n,e,t,confused,out stop))return false;
            if(n.Style!=3)return true;
            return Common(ref n,e,t,wasStopped,out stop);
        }
        private static bool Common(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool wasStopped,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool door=Door(n);
            bool supported=false;
            // Native's additional support flag is produced by independent
            // pre-motor jump/attack phases. The ordinary entry has no such
            // flag; an airborne trend must not be declared grounded.
            if(n.Vy==0)
            {
                int row=(int)(n.Y+n.Height+7)/16,head=(int)(n.Y-9)/16;
                for(int x=(int)(n.X+8)/16;x<=(int)(n.X+n.Width-8)/16;x++)
                {PredictionTile foot,ceiling;if(!t.Tile(x,row,out foot,out stop) || !t.Tile(x,head,out ceiling,out stop))return false;if(ceiling.SolidNoPlatform){supported=false;break;}if(foot.Active && foot.Solid)supported=true;}
            }
            if(type==428)supported=false;
            if(n.Vy>=0 && (type!=580 || n.DirectionY!=1) && !StepUp(ref n,t,out stop))return false;
            if(supported)
            {
                int x=(int)((n.X+n.Width/2+(WideProbe(type)?n.Width/2+16:15)*n.Direction)/16),y=(int)((n.Y+n.Height-15)/16);
                PredictionTile cell,one,two,three;
                if(!t.Tile(x,y,out cell,out stop) || !t.Tile(x,y-1,out one,out stop) || !t.Tile(x,y-2,out two,out stop) || !t.Tile(x,y-3,out three,out stop))return false;
                if(door && one.Active && (one.Type==10 || one.Type==388))
                {
                    n.A3=0;if(++n.A2>=60)
                    {
                        // Ordinary repeated knocking is deterministic. A
                        // graveyard roll or an actual world-changing opening
                        // ends the local fixed-terrain forecast at that step.
                        if(e.Graveyard){stop=PredictionStop.RandomDecision;return false;}
                        if((!e.BloodMoon || e.GoodWorld) && ResetsDoor(type) && !e.PlayerProtected)n.A1=0;
                        n.Vx=-.5f*n.Direction;n.A1+=one.Type==388?2:5;n.A1+=e.PlayerProtected?6:type==27?1:type==31 || type>=294 && type<=296?6:0;n.A2=0;
                        if(n.A1>=10 || type==460){n.A1=Math.Min(n.A1,10);if(!e.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}}
                    }
                }
                else
                {
                    int sprite=type==425?-n.SpriteDirection:n.SpriteDirection;
                    if(n.Vx<0 && sprite==-1 || n.Vx>0 && sprite==1)
                    {
                        PredictionTile down,forward;
                        if(!t.Tile(x,y+1,out down,out stop) || !t.Tile(x+n.Direction,y+1,out forward,out stop))return false;
                        if(n.Height>=32 && two.SolidNoPlatform)n.Vy=three.SolidNoPlatform?-8:-7;
                        else if(one.SolidNoPlatform)
                        {
                            n.Vy=type==624?-8:-6;
                            if(type==624){PredictionTile top;if(!t.Tile((int)(n.Bounds.CenterX/16),(int)((n.Y+n.Height)/16)-8,out top,out stop))return false;if(top.Active && top.Solid){n.Direction*=-1;n.SpriteDirection=n.Direction;n.Vx=3*n.Direction;}}
                        }
                        else if(n.Y+n.Height-y*16>20 && !cell.TopSlope && cell.SolidNoPlatform)n.Vy=-5;
                        else if(n.DirectionY<0 && type!=67 && !down.SolidBottomSlope && !forward.SolidBottomSlope){n.Vy=-8;n.Vx*=1.5f;}
                        else if(door)n.A1=n.A2=0;
                    if(n.Vy==0 && wasStopped && n.A3==1)n.Vy=-5;
                    if(n.Vy==0 && (e.Expert || type==586) && e.PlayerY+e.PlayerHeight/2<n.Y && Math.Abs(n.Bounds.CenterX-e.PlayerX)<e.PlayerWidth*3)
                    {
                        bool clear;if(!t.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                        if(clear)
                        {
                            if(type==586)
                            {int rows=(int)((n.Y+n.Height-16-(e.PlayerY+e.PlayerHeight/2))/16);if(rows<14)n.Vy=rows<7?-8.8f:rows<8?-9.2f:rows<9?-9.7f:rows<10?-10.3f:rows<11?-10.6f:-11;}
                            bool platform=n.Y-(e.PlayerY+e.PlayerHeight/2)<96;
                            for(int dy=0;dy<6 && !platform;dy++){PredictionTile p;if(!t.Tile((int)(n.Bounds.CenterX/16),(int)((n.Y+n.Height)/16)-1-dy,out p,out stop))return false;platform=p.Active && p.Platform;}
                            if(platform && n.Vy==0)n.Vy=-7.9f;
                        }
                    }
                    }
                    // 586's inner near-pounce branch in native is unreachable
                    // under this outer finite list. Preserve that reachability.
                    if(Pounce(type) && n.Vy==0 && Math.Abs(n.Bounds.CenterX-e.PlayerX)<100 && Math.Abs(n.Bounds.CenterY-e.PlayerY)<50 && n.Vx*n.Direction>=1)
                    {n.Vx=Math.Max(-3,Math.Min(3,n.Vx*2));n.Vy=-4;}
                    if(type==120 && n.Vy<0)n.Vy*=1.1f;
                    if(type==287 && n.Vy==0 && Math.Abs(n.Bounds.CenterX-e.PlayerX)<150 && Math.Abs(n.Bounds.CenterY-e.PlayerY)<50 && n.Vx*n.Direction>=1){n.Vx=8*n.Direction;n.Vy=-4;}
                    if(type==287 && n.Vy<0){n.Vx*=1.2f;n.Vy*=1.1f;}
                    if(type==460 && n.Vy<0){n.Vx*=1.3f;n.Vy*=1.1f;}
                }
            }
            else if(door)n.A1=n.A2=0;
            if(type==120 && !e.Multiplayer && n.A3>=180){stop=PredictionStop.RandomDestination;return false;}
            return true;
        }
        private static bool Pounce(int t){return t==31 || t>=294 && t<=296 || t==47 || t==77 || t==104 || t==168 || t==196 || t==385 || t==389 || t==464 || t==470 || t>=524 && t<=527;}
        private static bool WideProbe(int t){switch(t){case 109:case 163:case 164:case 199:case 236:case 239:case 257:case 258:case 290:case 391:case 425:case 427:case 426:case 580:case 508:case 415:case 530:case 532:case 582:return true;default:return false;}}
        private static bool Door(NpcMotionState n)
        {
            int t=n.EffectiveType;if(n.CritterTurns || t>=430 && t<=436 || t>=449 && t<=452 || t>=494 && t<=506 || t>=524 && t<=527)return false;
            switch(t){case 343:case 47:case 67:case 109:case 110:case 111:case 120:case 163:case 164:case 239:case 168:case 199:case 206:case 214:case 215:case 216:case 217:case 218:case 219:case 220:case 226:case 243:case 251:case 257:case 258:case 290:case 291:case 292:case 293:case 305:case 306:case 307:case 308:case 309:case 348:case 349:case 350:case 351:case 379:case 591:case 380:case 381:case 382:case 383:case 386:case 391:case 466:case 464:case 166:case 469:case 468:case 471:case 470:case 480:case 481:case 482:case 411:case 424:case 409:case 425:case 427:case 426:case 428:case 580:case 508:case 415:case 419:case 520:case 528:case 529:case 530:case 532:case 582:case 624:case 631:return false;default:return true;}
        }
        private static bool ResetsDoor(int t){switch(t){case 3:case 691:case 430:case 590:case 331:case 332:case 132:case 161:case 186:case 187:case 188:case 189:case 200:case 223:case 320:case 321:case 319:case 21:case 324:case 323:case 322:case 44:case 196:case 167:case 77:case 197:case 202:case 203:case 449:case 450:case 451:case 452:case 481:case 201:case 635:return true;default:return false;}}
        private static void CountAndTarget(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,int limit)
        {
            int type=n.EffectiveType;
            if(type==120 && n.A3==-120){n.Vx=n.Vy=0;n.A3=0;}
            if(n.X==n.OldX || n.A3>=limit || n.Vy==0 && (n.Vx>0 && n.Direction<0 || n.Vx<0 && n.Direction>0))n.A3++;
            else if(Math.Abs(n.Vx)>.9f && n.A3>0)n.A3--;
            if(n.A3>limit*10 || n.JustHit || Intersects(n,e))n.A3=0;
            // .8's fighter despawn predicate exempts 77 on the daytime surface.
            // The ordinary blocked-count threshold still controls turn-away.
            bool pursue=Pursues(n,e);
            if(n.A3<limit && pursue)
            {Target(ref n,ref e,confused);if(n.DirectionY>0 && e.PlayerY<=n.Y+n.Height)n.DirectionY=-1;}
            else if(!(type==166 && n.A2>0))
            {if(e.Day && !e.Remix && n.Y/16<e.WorldSurface)n.TimeLeft=Math.Min(n.TimeLeft,10);if(n.Vx==0){if(n.Vy==0 && ++n.A0>=2){n.Direction*=-1;n.SpriteDirection=n.Direction;n.A0=0;}}else n.A0=0;if(n.Direction==0)n.Direction=1;}
        }
        private static bool Pursues(NpcMotionState n,PredictionEnvironment e)
        {
            int type=n.EffectiveType;
            if(e.Eclipse || !e.Day || e.Remix || n.SpawnedFromStatue || n.Y>e.WorldSurface*16 || e.Graveyard || e.SnowMoon && (type==343 || type==350) || e.InvasionType==1 && (type==26 || type==27 || type==28 || type==111 || type==471) || e.DontStarve && (type==163 || type==164) || e.InvasionType==3 && type>=212 && type<=216 || e.InvasionType==4 && (type==381 || type==382 || type==383 || type==385 || type==386 || type==389 || type==391 || type==520))return true;
            switch(type)
            {
                case 31:case 47:case 67:case 73:case 77:case 78:case 79:case 80:
                case 110:case 120:case 168:case 181:case 185:case 198:case 199:
                case 206:case 217:case 218:case 219:case 220:case 239:case 243:
                case 254:case 255:case 257:case 258:case 291:case 292:case 293:
                case 294:case 295:case 296:case 379:case 380:case 409:case 415:
                case 419:case 424:case 425:case 427:case 428:case 429:case 464:
                case 470:case 508:case 524:case 525:case 526:case 527:case 528:
                case 529:case 530:case 532:case 580:case 582:case 624:case 630:return true;
                case 631:return n.A2>0;
                case 411:return n.A1>=180 || n.A1<90;
                default:return n.CritterTurns;
            }
        }
        internal static bool StepUp(ref NpcMotionState n,IPredictionTerrain t,out PredictionStop stop,bool platforms=false)
        {
            stop=PredictionStop.None;int direction=Math.Sign(n.Vx),x=(int)((n.X+n.Vx+n.Width/2+(n.Width/2+1)*direction)/16),y=(int)((n.Y+n.Height-1)/16);
            PredictionTile c,a,b,d,f,behind;
            if(!t.Tile(x,y,out c,out stop) || !t.Tile(x,y-1,out a,out stop) || !t.Tile(x,y-2,out b,out stop) || !t.Tile(x,y-3,out d,out stop) || !t.Tile(x,y-4,out f,out stop) || !t.Tile(x-direction,y-3,out behind,out stop))return false;
            if(n.X+n.Vx+n.Width<=x*16 || n.X+n.Vx>=x*16+16 ||
                !(c.Active && !c.TopSlope && !a.TopSlope && (c.Solid && !c.SolidTop || platforms && c.SolidTop && (!a.Solid || !a.Active) && c.Type!=16 && c.Type!=18 && c.Type!=134) || a.Half && a.Active) ||
                !(Pass(a) || a.Half && Pass(f)) || !Pass(b) || !Pass(d) || behind.Active && behind.Solid && (!platforms || !behind.SolidTop))return true;
            float surface=y*16+(c.Half?8:0)-(a.Half?8:0),rise=n.Y+n.Height-surface;
            if(surface<n.Y+n.Height && rise<=(NpcWallMotion.Ground(n.EffectiveType)?24.1f:16.1f))n.Y=surface-n.Height;
            return true;
        }
        private static bool Pass(PredictionTile t){return !t.Active || !t.Solid || t.SolidTop;}
        private static bool Intersects(NpcMotionState n,PredictionEnvironment e)
        {return (int)n.X<(int)(e.PlayerX-e.PlayerWidth/2)+(int)e.PlayerWidth && (int)n.X+n.Width>(int)(e.PlayerX-e.PlayerWidth/2) && (int)n.Y<(int)(e.PlayerY-e.PlayerHeight/2)+(int)e.PlayerHeight && (int)n.Y+n.Height>(int)(e.PlayerY-e.PlayerHeight/2);}
        private static void Target(ref NpcMotionState n,ref PredictionEnvironment e,bool confused)
        {int oldTarget=n.Target;NpcTargeting.Retarget(ref n,ref e);var target=NpcTargeting.Area(n,e);if(NpcTargeting.CanFace(n,e,oldTarget)){n.Direction=(int)target.X+(int)target.Width/2<n.X+n.Width/2?-1:1;n.DirectionY=n.TrackingKind==2?(int)target.Y+(int)target.Height/2<n.Y+n.Height/2?-1:1:(int)target.Y+(int)target.Height<=n.Y+n.Height?-1:1;}if(confused)n.Direction=-n.Direction;}
        internal static void AfterMove(ref NpcMotionState n)
        {if(n.Vy==0 && (n.Direction==-1 || n.Direction==1) && (n.Identity.Type!=109 || n.Vx==0 || n.Vx*n.Direction>0))n.SpriteDirection=n.Direction;}
    }
}
