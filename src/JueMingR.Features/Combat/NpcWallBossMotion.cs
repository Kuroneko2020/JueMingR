using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // The body writes a global, not a per-child anchor. Preserve Main's frame
    // cleanup, player-before-NPC feedback, then native slot-order writes.
    internal static class NpcWallBossMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.EffectiveType==113 && n.Style==27 || n.EffectiveType==114 && n.Style==28 || n.EffectiveType==115 && n.Style==29;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {
            if(n.Style!=27 && n.WallBossCaptured && !n.WallBossValid)return false;
            if(!n.TargetCaptured || !n.PlayerNativeDead)return true;
            if(e.WallPlayers!=null)for(int i=0;i<e.WallPlayers.Count;i++)if(e.WallPlayers[i].Active && !e.WallPlayers[i].Dead && !e.WallPlayers[i].Ghost)return true;
            return false;
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static void BeginFrame(NpcMotionState[] group,int count)
        {
            if(group==null || count<0 || count>group.Length)return;
            for(int i=0;i<count;i++)
            {
                var n=group[i];if(!n.WallRelationsCaptured || !n.WallBossCaptured || !n.WallBossValid)continue;
                int owner=Find(group,count,n.WallBossIdentity);
                // An absent observed identity is missing, never proven dead.
                if(owner>=0 && (!group[owner].Active || group[owner].EffectiveType!=113))
                {n.WallBossValid=false;n.WallBossIdentity=default(NpcIdentity);group[i]=n;}
            }
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.WallRelationsCaptured || !n.WallRelationsComplete || !n.WallBossCaptured || group==null || count<1 || count>group.Length)
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.Style==27)return Body(ref n,ref e,terrain,elapsed,group,count,confused,out stop);
            if(n.JustHit && n.Style==29)n.A1=10;
            if(!n.WallBossValid){n.Active=false;stop=PredictionStop.Despawn;return false;}
            int owner=Find(group,count,n.WallBossIdentity);if(owner<0){stop=PredictionStop.MissingDependency;return false;}
            var body=group[owner];if(body.Identity.Equals(n.Identity))body=n;
            if(n.Style==28)
            {NpcTargeting.Face(ref n,ref e,true,confused);if(!Ready(n,elapsed,out stop))return false;return Eye(ref n,e,terrain,body,out stop);}
            // Hungry's radius/clock/pursuit are already one mechanism. Supply
            // the raw global geometry rather than its old captured constant.
            n.PositionParameter=n.WallTop+(n.WallBottom-n.WallTop)*n.A0;
            if(!NpcHungryMotion.Step(ref n,body,e,confused,out stop))return false;
            return Ready(n,elapsed,out stop);
        }
        private static bool Body(ref NpcMotionState n,ref PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.X<160 || n.X>(e.WorldWidth-10)*16)n.Active=false;
            int top=n.WallTop,bottom=n.WallBottom;
            if(n.L0==0){n.L0=1;top=bottom=-1;}
            n.A1++;
            if(n.A2==0){if((double)n.Life<n.LifeMax*.5)n.A1++;if((double)n.Life<n.LifeMax*.2)n.A1++;if(n.A1>2700)n.A2=1;}
            if(n.A2>0 && n.A1>60){n.A2++;n.A1=0;int limit=3;if((double)n.Life<n.LifeMax*.3)limit++;if(n.A2>limit)n.A2=0;}
            // localAI3 chooses sounds only. Do not fabricate a future sound
            // RNG clock or stop a motor which never consumes that choice.
            if(!Band(n,e,terrain,ref top,ref bottom,out stop))return false;
            n.WallBossValid=true;n.WallBossIdentity=n.Identity;n.WallTop=top;n.WallBottom=bottom;
            Write(ref n,group,count);
            n.Vy=0;n.Y=(bottom+top)/2-n.Height/2;
            float speed=1.5f;
            if((double)n.Life<n.LifeMax*.75)speed+=.25f;if((double)n.Life<n.LifeMax*.5)speed+=.4f;
            if((double)n.Life<n.LifeMax*.25)speed+=.5f;if((double)n.Life<n.LifeMax*.1)speed+=.6f;
            if(e.Expert)
            {
                if((double)n.Life<n.LifeMax*.66)speed+=.3f;if((double)n.Life<n.LifeMax*.33)speed+=.3f;
                if((double)n.Life<n.LifeMax*.05)speed+=.6f;if((double)n.Life<n.LifeMax*.035)speed+=.6f;if((double)n.Life<n.LifeMax*.025)speed+=.6f;
                speed*=1.35f;speed+=.35f;
            }
            if(e.GoodWorld){speed*=1.1f;speed+=.2f;}
            if(n.Vx==0)
            {
                if(!Query(ref n,ref e,terrain,elapsed,confused,out stop,false))return false;
                // Locked native's fallback loop reads target, not its loop
                // counter. Preserve that fact even for active dead players.
                if(n.PlayerNativeDead)n.Direction=n.PlayerActive?(n.Bounds.CenterX<e.PlayerX?1:-1):0;
                n.Vx=n.Direction;
            }
            if(n.Vx<0){n.Vx=-speed;n.Direction=-1;}else{n.Vx=speed;n.Direction=1;}
            if(!n.TargetCaptured || !n.HasPlayer || n.PlayerNativeDead || !n.PlayerGross)
                if(!Query(ref n,ref e,terrain,elapsed,confused,out stop))return false;
            if(!Ready(n,elapsed,out stop))return false;
            if(n.PlayerNativeDead)
            {
                n.L1+=1f/180f;
                if(n.L1>=1){n.Life=0;n.Active=false;Write(ref n,group,count);stop=PredictionStop.Despawn;return false;}
            }
            else n.L1=Math.Max(0,Math.Min(1,n.L1-1f/30f));
            n.SpriteDirection=n.Direction;
            // New identities have no feedback to body motor. Only the real
            // following capture supplies a selected new eye/hungry identity.
            if(!e.Multiplayer && n.L0==1)n.L0=2;
            Write(ref n,group,count);return true;
        }
        private static bool Eye(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,NpcMotionState body,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.Health.RealLife=body.Identity.Slot;if(body.Life>0)n.Life=body.Life;
            n.X=body.X;n.Direction=n.SpriteDirection=body.Direction;
            float mid=(n.WallBottom+n.WallTop)/2;
            float goal=(mid+(n.A0>0?n.WallTop:n.WallBottom))/2-n.Height/2;
            if(n.Y>goal+1)n.Vy=-1;else if(n.Y<goal-1)n.Vy=1;else{n.Vy=0;n.Y=goal;}
            n.Vy=Math.Max(-5,Math.Min(5,n.Vy));if(e.Multiplayer)return true;
            int shots=4;n.L1++;
            if((double)body.Life<body.LifeMax*.75){n.L1++;shots++;}if((double)body.Life<body.LifeMax*.5){n.L1++;shots++;}
            if((double)body.Life<body.LifeMax*.25){n.L1++;shots+=2;}if((double)body.Life<body.LifeMax*.1){n.L1+=2;shots+=3;}
            if(e.Expert){n.L1+=.5f;shots++;if((double)body.Life<body.LifeMax*.1){n.L1+=2;shots+=3;}}
            if(n.L2==0){if(n.L1>600){n.L2=1;n.L1=0;}return true;}
            if(n.L1<=45)return true;
            bool clear;if(terrain==null || !terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
            if(clear){n.L1=0;n.L2++;if(n.L2>=shots)n.L2=0;}return true;
        }
        private static bool Band(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,ref int top,ref int bottom,out PredictionStop stop)
        {
            stop=PredictionStop.None;int under=e.WorldHeight-200,low=under+10,high=low+70;
            int left=(int)(n.X/16f),right=(int)((n.X+n.Width)/16f),center=(int)((n.Y+n.Height/2)/16f);
            int y=center+7,hits=0;
            while(hits<15 && y>under)
            {
                y++;if(y>e.WorldHeight-10){y=e.WorldHeight-10;break;}if(y<low)continue;
                if(!Row(terrain,e,left,right,y,ref hits,out stop))return false;
            }
            bottom=Ease(bottom,(y+4)*16);y=center-7;hits=0;
            while(hits<15 && y<e.WorldHeight-10)
            {
                y--;if(y<=10){y=10;break;}if(y>high)continue;if(y<low){y=low;break;}
                if(!Row(terrain,e,left,right,y,ref hits,out stop))return false;
            }
            top=Ease(top,(y-4)*16);top=Math.Max(low*16,Math.Min(high*16,top));bottom=Math.Max(low*16,Math.Min(high*16,bottom));
            if(top>bottom-160)top=bottom-160;else if(bottom<top+160)bottom=top+160;return true;
        }
        private static bool Row(IPredictionTerrain t,PredictionEnvironment e,int left,int right,int y,ref int hits,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            for(int x=left;x<=right;x++)
            {
                if(x<2 || y<2 || x>=e.WorldWidth-2 || y>=e.WorldHeight-2)continue;
                PredictionTile cell;if(t==null || !t.Tile(x,y,out cell,out stop))return false;
                if(cell.Active && cell.Solid && !cell.SolidTop && !cell.Half && cell.Slope==0 || cell.Liquid>0)hits++;
            }
            return true;
        }
        private static int Ease(int value,int goal){return value==-1?goal:value>goal?Math.Max(goal,value-1):Math.Min(goal,value+1);}
        internal static int SelectPlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {
            if(n.Style!=27)return n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;
            if(n.HasPlayer && !n.PlayerNativeDead && n.PlayerGross && n.Vx!=0)return n.PlayerIndex;
            int winner=Winner(n,e,true);
            if(winner<0 && n.Vx==0)winner=Winner(n,e,false);
            return winner<0?n.HasPlayer?n.PlayerIndex:0:e.WallPlayers[winner].Slot;
        }
        private static int Winner(NpcMotionState n,PredictionEnvironment e,bool grossOnly)
        {
            if(e.WallPlayers==null)return -1;int winner=-1;float best=0;
            for(int i=0;i<e.WallPlayers.Count;i++)
            {
                var p=e.WallPlayers[i];if(!p.Active || p.Dead || p.Ghost || grossOnly && !Gross(p,e))continue;
                var area=Area(p,e);float score=Math.Abs(area.CenterX-n.Bounds.CenterX)+Math.Abs(area.CenterY-n.Bounds.CenterY)-p.Aggro;
                if(p.NoAggro && n.Direction!=0)score+=1000;
                if(winner<0 || score<best){winner=i;best=score;}
            }
            return winner;
        }
        private static MotionRect Area(PredictionWallPlayer p,PredictionEnvironment e)
        {return e.WallTimelineCaptured && p.Slot==e.WallTimelineIndex?e.WallTimelineArea:p.Area;}
        private static bool Gross(PredictionWallPlayer p,PredictionEnvironment e)
        {return e.WallTimelineCaptured && p.Slot==e.WallTimelineIndex?e.WallTimelineGross:p.Gross;}
        private static bool Query(ref NpcMotionState n,ref PredictionEnvironment e,IPredictionTerrain t,int elapsed,bool confused,out PredictionStop stop,bool grossOnly=true)
        {
            stop=PredictionStop.None;if(e.WallPlayers==null){stop=PredictionStop.MissingDependency;return false;}
            int winner=-1,pet=-1;float best=0;
            for(int i=0;i<e.WallPlayers.Count;i++)
            {
                var p=e.WallPlayers[i];if(!p.Active || p.Dead || p.Ghost)continue;
                // Another player can move into both the activation rectangle
                // and this native Manhattan competition. No static substitute.
                if(elapsed>1 && (!e.WallTimelineCaptured || p.Slot!=e.WallTimelineIndex))
                {stop=PredictionStop.MissingDependency;return false;}
                if(grossOnly && !Gross(p,e))continue;
                var area=Area(p,e);float raw=Math.Abs(area.CenterX-n.Bounds.CenterX)+Math.Abs(area.CenterY-n.Bounds.CenterY),score=raw-p.Aggro;
                if(p.NoAggro && n.Direction!=0)score+=1000;
                if(winner<0 || score<best){winner=i;best=score;pet=-1;}
                if(!p.HasPet || p.NoAggro)continue;
                if(elapsed>1){stop=PredictionStop.MissingDependency;return false;}
                float petScore=Math.Abs(p.PetArea.CenterX-n.Bounds.CenterX)+Math.Abs(p.PetArea.CenterY-n.Bounds.CenterY)-200;
                if(petScore<best && petScore<200)
                {
                    bool clear;if(t==null || !t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(p.PetArea.CenterX,p.PetArea.CenterY,1,1),out clear,out stop))return false;
                    if(clear)pet=i;
                }
            }
            if(winner>=0)Capture(ref n,ref e,e.WallPlayers[winner]);
            else if(n.Target<0 || n.Target>=255)
            {
                int fallback=-1;for(int i=0;i<e.WallPlayers.Count;i++)if(e.WallPlayers[i].Slot==0){fallback=i;break;}
                if(fallback<0){stop=PredictionStop.MissingDependency;return false;}Capture(ref n,ref e,e.WallPlayers[fallback]);
            }
            if(!n.TargetCaptured || !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
            n.TrackingKind=1;
            if(pet>=0)
            {var area=e.WallPlayers[pet].PetArea;n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;n.DirectionY=(int)area.Y+(int)area.Height/2<n.Y+n.Height/2?-1:1;}
            else if(!n.PlayerNativeDead && !(n.TargetNoAggro && n.Direction!=0))
            {var area=n.PlayerArea;n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;n.DirectionY=(int)area.Y+(int)area.Height/2<n.Y+n.Height/2?-1:1;}
            if(confused)n.Direction=-n.Direction;return true;
        }
        private static void Capture(ref NpcMotionState n,ref PredictionEnvironment e,PredictionWallPlayer p)
        {
            var area=Area(p,e);n.Target=n.PlayerIndex=p.Slot;n.HasPlayer=n.TargetCaptured=true;n.PlayerActive=p.Active;n.PlayerNativeDead=p.Dead;n.PlayerDead=p.Dead || !p.Active;n.PlayerGhost=p.Ghost;
            n.PlayerGross=Gross(p,e);n.PlayerArea=new MotionRect((int)area.X,(int)area.Y,area.Width,area.Height);n.PlayerCenterX=area.CenterX;n.PlayerCenterY=area.CenterY;n.PlayerVelocityCaptured=true;n.PlayerVx=p.Vx;n.PlayerVy=p.Vy;n.TargetNoAggro=p.NoAggro;n.PlayerIdle=p.Idle;
            e.PlayerIndex=p.Slot;e.PlayerX=area.CenterX;e.PlayerY=area.CenterY;e.PlayerWidth=area.Width;e.PlayerHeight=area.Height;e.PlayerDead=n.PlayerDead;e.PlayerGross=n.PlayerGross;e.PlayerVx=p.Vx;e.PlayerVy=p.Vy;e.PlayerVelocityCaptured=true;
        }
        private static bool Ready(NpcMotionState n,int elapsed,out PredictionStop stop)
        {stop=PredictionStop.None;if(!n.TargetCaptured || !n.HasPlayer || n.TargetChoiceUnavailable || n.PlayerGhost && elapsed>1){stop=PredictionStop.MissingDependency;return false;}return true;}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity id){for(int i=0;i<count;i++)if(group[i].Identity.Equals(id))return i;return -1;}
        private static void Write(ref NpcMotionState n,NpcMotionState[] group,int count)
        {
            for(int i=0;i<count;i++)if(group[i].WallRelationsCaptured)
            {var row=group[i];row.WallBossCaptured=true;row.WallBossValid=n.WallBossValid;row.WallBossIdentity=n.WallBossIdentity;row.WallTop=n.WallTop;row.WallBottom=n.WallBottom;group[i]=row;}
        }

        // Native fall consumes completed gravDir/tongued before immunity,
        // ResetEffects and buffs. Both player continuations call this once,
        // before their shared gravity prefix; movement never reapplies it.
        internal static bool AdvanceFall(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!p.WallLocal || p.BordersDead)return true;
            if(!p.WallFallCaptured || !p.WallFallKnown){stop=PredictionStop.MissingDependency;return false;}
            if(!Finite(p.Y) || !Finite(p.Vy) || (p.GravityDirection!=1 && p.GravityDirection!=-1) || e.GravityWorldSurface<=0)
            {stop=PredictionStop.InvalidState;return false;}
            int row=(int)(p.Y/16f);
            if(p.Vy==0)
            {
                int fall=row-p.WallFallStart;
                bool harmful=(p.GravityDirection==1 && fall>25+p.WallExtraFall || p.GravityDirection==-1 && fall<-(25+p.WallExtraFall)) && !p.WallNoFallDamage && !p.WallHasWings;
                if(harmful)
                {
                    bool safe=false;int bottom=p.GravityDirection>0?(int)((p.Y+p.Height+1)/16f):(int)((p.Y-1)/16f);
                    for(int x=(int)(p.X/16f);x<=(int)((p.X+p.Width)/16f);x++)
                    {PredictionTile tile;if(!terrain.Tile(x,bottom,out tile,out stop))return false;if(tile.FallSafe){safe=true;break;}}
                    if(!safe)
                    {
                        // Player.Update deliberately clears immune before Hurt.
                        // NoNewHits ends before damage/dodge/knockback consumers,
                        // regardless of the observed immunity timer. A fresh real
                        // sample supplies their results and resumes known health.
                        if(!p.BordersGodMode){stop=PredictionStop.PhaseBoundary;return false;}
                        p.WallImmune=false;
                    }
                }
                p.WallFallStart=row;
            }
            float world=(float)e.WorldWidth/4200f;world*=world;
            float altitude=(float)((double)(p.Y/16f-(60f+10f*world))/(e.GravityWorldSurface/(e.Remix?1.0:6.0)));
            altitude=Math.Max(e.Remix?.1f:.25f,Math.Min(1f,altitude));
            if(p.Jump>0 || p.Wet || p.SlowFall || (double)altitude<.8 || p.WallTongued)p.WallFallStart=row;
            // Source certifies absence of equipment/non-Wall buff producers.
            // Consume their OLD completed flags first, then model ResetEffects.
            p.WallNoFallDamage=p.WallHasWings=false;p.WallExtraFall=0;
            return true;
        }

        // Called after the shared gravity prefix, before ordinary movement or
        // the settled fast path. Buff fields and pending buff slots are distinct:
        // WOFTongue adds a buff for the NEXT action; snap removes its slot but
        // retains this action's tongued field and direct movement.
        internal static bool AdvancePlayer(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,PredictionWallSource source,out bool handled,out PredictionStop stop)
        {
            handled=false;stop=PredictionStop.None;
            if(!p.WallFactsCaptured || !source.Captured){stop=PredictionStop.MissingDependency;return false;}
            if(p.BordersDead){handled=true;return true;}
            if(p.WallImmunityCaptured && p.WallImmune && --p.WallImmuneTime<=0)p.WallImmune=false;
            p.WallGross=p.WallHorrifiedTicks>0 && source.Valid;
            p.WallHorrifiedTicks=p.WallGross?10:0;
            p.WallTongued=p.WallTonguedTicks>0;
            if(p.WallTongued)p.WallTonguedTicks=10;
            if(!AdvanceHealth(ref p,e,out stop))return false;
            p.ObservationMechanism&=~128;
            if(p.WallTongued)p.ObservationMechanism|=128;
            if((p.ObservationMechanism&~128)==0)
            {p.Complex=p.WallTongued;p.UnsupportedGeometry=false;p.SkipConveyor=p.WallTongued;}
            if(p.WallTongued)
            {
                if((p.ObservationMechanism&~128)!=0 || p.Mounted || p.Grappled || p.Rope || p.Carpet || p.Sticky || p.OnTrack || p.RidingTracks || p.Shimmer || p.Vortex)
                {stop=PredictionStop.UnsupportedMechanism;return false;}
                // Native Player26692 gates the ordinary control motor on
                // !tongued. Its horizontal and jump clocks do not run here.
                bool snap=!source.Valid;
                if(source.Valid)
                {
                    float x=source.Area.CenterX+source.Direction*200-(p.X+p.Width*.5f),y=source.Area.CenterY-(p.Y+p.Height*.5f);
                    float length=(float)Math.Sqrt(x*x+y*y),speed=11;
                    if(e.Expert)
                    {
                        double vx=source.Vx,vy=source.Vy;float nativeLength=(float)Math.Sqrt(vx*vx+vy*vy);
                        float amount=Math.Min(1,nativeLength/5);speed=11+(22-11)*amount;
                    }
                    float scale=length>speed?speed/length:1;snap=length<=speed;p.Vx=x*scale;p.Vy=y*scale;
                }
                if(snap && p.WallLocal)p.WallTonguedTicks=0;
            }
            if(p.WallLocal && source.Valid)
            {
                float contact=source.Area.X+40;if(source.Direction>0)contact-=96;
                if(p.WallGross && p.X+p.Width>contact && p.X<contact+140 && !p.BordersGodMode)
                {
                    if(!p.WallImmunityCaptured){stop=PredictionStop.MissingDependency;return false;}
                    // This native Hurt precedes buff application and movement.
                    // A no-new-hits continuation ends BEFORE the real contact;
                    // the following sample captures the actual Hurt result.
                    if(!p.WallImmune){stop=PredictionStop.PhaseBoundary;return false;}
                }
                if(!p.WallGross && p.Y>(e.WorldHeight-250)*16 && p.X>contact-e.WallViewWidth && p.X<contact+e.WallViewWidth)p.WallHorrifiedTicks=10;
                if(p.WallGross && (p.Y<(e.WorldHeight-200)*16 || (source.Direction<0?p.X+p.Width/2>source.Area.X+(int)source.Area.Width/2+40:p.X+p.Width/2<source.Area.X+(int)source.Area.Width/2-40)))p.WallTonguedTicks=10;
                if(p.WallTongued)
                {
                    float dx=source.Area.X+(int)source.Area.Width/2-(p.X+p.Width*.5f),dy=source.Area.Y+(int)source.Area.Height/2-(p.Y+p.Height*.5f);
                    if(((float)Math.Sqrt(dx*dx+dy*dy)>3000 || source.Area.X<608 || source.Area.X>(e.WorldWidth-38)*16) && !p.BordersGodMode)
                    {stop=PredictionStop.Despawn;return false;}
                    // WOFTongue clears controlUseItem. It does not add CCed,
                    // which is the independently captured ItemCheck blocker.
                    p.UmbrellaUseHeld=false;
                }
            }
            if(!p.WallTongued)return true;
            handled=true;var playerTerrain=terrain as IPredictionPlayerTerrain;
            if(playerTerrain==null){stop=PredictionStop.MissingDependency;return false;}
            var body=new NpcMotionState{X=p.X,Y=p.Y,Vx=p.Vx,Vy=p.Vy,Width=p.Width,Height=p.Height,Active=true,Friendly=true,
                Wet=p.Wet,Honey=p.Honey,Lava=p.Lava,Shimmer=p.Shimmer,
                Health=new NpcHealthState{Immortal=true,DontTakeDamage=true,LavaImmune=true,ShimmerImmune=true},WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1};
            if(!playerTerrain.MovePlayer(ref body,e,ref p,out stop))return false;
            if(!Finite(body.X) || !Finite(body.Y) || !Finite(body.Vx) || !Finite(body.Vy)){stop=PredictionStop.InvalidState;return false;}
            p.X=body.X;p.Y=body.Y;p.Vx=body.Vx;p.Vy=body.Vy;
            PlayerVerticalMotion.AfterFluid(ref p,body.Wet,body.Honey,body.Lava,body.Shimmer);
            if(!PlayerMotionContinuation.ClampBorders(ref p,out stop))return false;
            PlayerMotionContinuation.CompleteItemCheck(ref p);return true;
        }

        // Expert Tongue is existing DOT, not a new-hit assumption. Source only
        // certifies this canonical profile when all effective regen modifiers
        // are absent. An uncertified profile is needed at its real health
        // consumer, not as an excuse to discard ordinary body motion.
        internal static bool AdvanceHealth(ref PredictionPlayerMotion p,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool tongue=p.WallTongued && e.Expert;
            if(!p.WallHealthCaptured || !p.WallHealthKnown)
            {if(tongue || p.WallHealthCaptured && p.WallRegenCount<0){stop=PredictionStop.MissingDependency;return false;}return true;}
            if(p.WallLifeMax<=0 || !Finite(p.WallRegenTime)){stop=PredictionStop.InvalidState;return false;}
            if(tongue)p.WallRegenTime=0;
            p.WallRegenTime+=1;
            float value=0;
            if(p.WallRegenTime>=300)value++;if(p.WallRegenTime>=600)value++;if(p.WallRegenTime>=900)value++;
            if(p.WallRegenTime>=1200)value++;if(p.WallRegenTime>=1500)value++;if(p.WallRegenTime>=1800)value++;
            if(p.WallRegenTime>=2400)value++;if(p.WallRegenTime>=3000)value++;
            if(p.WallRegenTime>=3600){value++;p.WallRegenTime=3600;}
            value=p.Vx!=0 && !p.Grappled?value*.5f:value*1.25f;
            if(e.Expert)value/=2;
            value*=((float)p.WallLifeMax/400f*.85f+.15f);
            p.WallRegenCount+=(int)Math.Round(value)-(tongue?100:0);
            while(p.WallRegenCount>=120)
            {p.WallRegenCount-=120;if(p.WallLife<p.WallLifeMax)p.WallLife++;if(p.WallLife>p.WallLifeMax)p.WallLife=p.WallLifeMax;}
            int threshold=tongue?600:120;
            while(p.WallRegenCount<=-threshold)
            {
                int damage=tongue?5:Math.Min(p.WallRegenCount/-120,4);
                p.WallRegenCount+=tongue?600:120*damage;p.WallLife-=damage;
                if(p.WallLife<=0 && p.WallLocal && !p.BordersGodMode){stop=PredictionStop.Despawn;return false;}
            }
            return true;
        }
        private static bool Finite(float n){return !float.IsNaN(n) && !float.IsInfinity(n);}
    }
}
