using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked NPC.cs AI_006_Worms 51489–52810. This bounded route covers the
    // bounded chains plus375/402 and Solar412..414; ordinary worms/AI37 keep
    // their existing solver.
    // Birth is stopped at its actual consumer, then a real post-birth capture
    // supplies identities. Dust/sound RNG does not choose this actor's motor.
    internal static class NpcSegmentedWormMotion
    {
        internal static bool Head(int type){return type==87 || type==117 || type==375 || type==402 || type==412 || type==454 || type==510 || type==513 || type==621;}
        internal static bool Known(NpcMotionState n)
        {int t=n.EffectiveType;return n.Style==6 && (t>=87 && t<=92 || t>=117 && t<=119 || t==375 || t==402 || t>=412 && t<=414 || t>=454 && t<=459 || t>=510 && t<=515 || t>=621 && t<=623);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool BirthPending(NpcMotionState n,PredictionEnvironment e)
        {return !e.Multiplayer && n.A0==0 && (Head(n.EffectiveType) && !Solitary(n.EffectiveType) || n.EffectiveType==118);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.Target<0 || n.Target==255 || n.PlayerNativeDead || SurfaceFlag(n) && (double)PlayerArea(n,e).Y<e.GravityWorldSurface*16d;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !(n.A1>0 && n.A1<201) && (Retargets(n,e)?n.HasClosestPlayer && n.ClosestPlayerActive && !n.ClosestPlayerNativeDead:n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead);}
        private static bool SurfaceFlag(NpcMotionState n)
        {int t=n.EffectiveType;return t==117 || t==510 || t==513 && !n.PlayerUndergroundDesert;}
        private static MotionRect PlayerArea(NpcMotionState n,PredictionEnvironment e)
        {return n.PlayerPreciseAreaCaptured?n.PlayerPreciseArea:new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);}

        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.WormRelationsCaptured || !n.WormLinkFactsCaptured){stop=PredictionStop.MissingDependency;return false;}
            int type=n.EffectiveType,parent=-1;bool head=Head(type),fade=type>=454 && type<=459 || type>=621 && type<=623;
            if(type==402)StardustPrelude(ref n,e);
            if(type==117 && n.L1==0)n.L1=1;
            if(type==454 && n.L3==0)n.L3=1;
            if(!head && (fade || !e.Multiplayer || n.A1>0))
            {parent=Find(n.ParentIdentity,group,count);if(parent<0){stop=PredictionStop.MissingDependency;return false;}}
            if(fade)
            {
                // Receiving uses the pre-decrement alpha. At alpha3->0 the
                // current action remains protected; the following action opens.
                if(type>=621){n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;}
                n.Health.DontTakeDamage=n.Alpha>0;
                if(head || group[parent].Alpha<85)n.Alpha=Math.Max(0,n.Alpha-42);
                if(type>=621){n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;}
                n.CanReceive=!n.Health.DontTakeDamage && !n.Health.Immortal && !n.Friendly;
            }
            bool surface=SurfaceFlag(n); // Native computes this before TargetClosest.
            if(n.A3>0)n.Health.RealLife=(int)n.A3;
            if(!n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            int oldTarget=n.Target;
            if(Retargets(n,e))NpcWormMotion.Target(ref n,ref e,oldTarget,confused);
            var player=PlayerArea(n,e);
            if(n.PlayerNativeDead || surface && (double)player.Y<e.GravityWorldSurface*16d)
            {n.TimeLeft=Math.Min(n.TimeLeft,300);if(surface)n.Vy+=type==513?.1f:.2f;}
            if(type==621 && e.Day){n.TimeLeft=Math.Min(n.TimeLeft,60);n.Vy+=1;}
            if(BirthPending(n,e)){stop=PredictionStop.PhaseBoundary;return false;}
            if(!e.Multiplayer && !Solitary(type))
            {
                // Raw guards read index0 and accept any active style6, even
                // life0 or a foreign worm. Geometry later has a different gate.
                if(!head && (!group[parent].Active || group[parent].Style!=6))return Retire(ref n,out stop);
                if(!Tail(type))
                {
                    int child=Find(n.ChildIdentity,group,count);if(child<0){stop=PredictionStop.MissingDependency;return false;}
                    if(!group[child].Active || group[child].Style!=6)return Retire(ref n,out stop);
                }
            }
            // Current-action oracle can consume observed raw values. A future
            // cannot certify active foreign style6 using generic Known(style6)
            // or a static snapshot when its required motor is unclosed.
            if(elapsed>0 && !n.WormRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
            bool earth=Flight(type) || type==402 || type>=412 && type<=414;
            if(!earth && !Sense(n,e,terrain,out earth,out stop))return false;
            if(!earth && head)
            {
                if(e.Players==null){stop=PredictionStop.MissingDependency;return false;}
                bool close=false,unknownPlayer=false;
                for(int i=0;i<e.Players.Count;i++)
                {
                    var p=e.Players[i];
                    if(e.PlayerTimelineActive)
                    {
                        if(e.Players.SlotAt(i)==e.PlayerIndex)
                        {
                            if(n.PlayerIndex!=e.PlayerIndex || !n.PlayerPreciseAreaCaptured){stop=PredictionStop.MissingDependency;return false;}
                            p=n.PlayerPreciseArea;
                        }
                        else if(elapsed>0){unknownPlayer=true;continue;}
                    }
                    if((int)n.X<(int)p.X+1000 && (int)n.X+n.Width>(int)p.X-1000 && (int)n.Y<(int)p.Y+1000 && (int)n.Y+n.Height>(int)p.Y-1000){close=true;break;}
                }
                // A known close player settles the existential native census.
                // Otherwise a second player's future area cannot be frozen.
                if(!close && unknownPlayer){stop=PredictionStop.MissingDependency;return false;}
                earth=!close;
            }
            if(Flight(type))Sprite(ref n,n.Vx);
            // Native constrains any positive fractional ai1 using its truncated
            // raw index. Exact zero never takes this geometric branch.
            // Main.npc.Length is maxNPCs+1 in the locked build: raw dummy200
            // is legal here even though child-retirement iteration excludes it.
            if(n.A1>0 && n.A1<201)
            {
                if(parent<0)parent=Find(n.ParentIdentity,group,count);
                if(parent<0){stop=PredictionStop.MissingDependency;return false;}
                return Link(ref n,group[parent],out stop);
            }
            if(!n.HasPlayer || !n.PlayerPreciseAreaCaptured || n.PlayerActive && !n.PlayerNativeDead && (n.PlayerGhost || e.PlayerTimelineActive && n.PlayerIndex!=e.PlayerIndex))
            {stop=PredictionStop.MissingDependency;return false;}
            float speed,acceleration;if(!Speed(n,group,count,out speed,out acceleration,out stop))return false;
            float pcx=player.CenterX,pcy=player.CenterY;
            if(type==412 && !SolarGoal(ref n,e,terrain,player,group,count,ref pcx,ref pcy,out speed,out acceleration,out stop))return false;
            float dx=(int)(pcx/16f)*16-(int)(n.Bounds.CenterX/16f)*16,dy=(int)(pcy/16f)*16-(int)(n.Bounds.CenterY/16f)*16;
            if(type==375){dx*=-1;dy*=-1;}
            if(!earth)
            {
                NpcWormMotion.Target(ref n,ref e,oldTarget,confused);
                n.Vy+=.11f;if(n.Vy>speed)n.Vy=speed;
                if((double)(Math.Abs(n.Vx)+Math.Abs(n.Vy))<(double)speed*.4d)n.Vx=(float)(n.Vx+(n.Vx<0?-1d:1d)*acceleration*1.1f);
                else if(n.Vy==speed)Toward(ref n.Vx,dx,acceleration);
                else if(n.Vy>4)n.Vx=(float)(n.Vx+(n.Vx<0?1d:-1d)*acceleration*.9f);
            }
            else
            {
                float distance=(float)Math.Sqrt((double)dx*dx+(double)dy*dy),ax=Math.Abs(dx),ay=Math.Abs(dy),amount=speed/distance;
                dx*=amount;dy*=amount; // Zero distance NaN locals do not persist.
                bool retreat=n.PlayerNativeDead || (type==510 || type==513) && (double)player.Y<e.GravityWorldSurface*16d && !n.PlayerSandstorm && !n.PlayerUndergroundDesert;
                if(retreat && !e.AnyLivingCorrupt)
                {
                    if(!e.Multiplayer && (double)(n.Y/16f)>(e.GravityRockLayer+e.WorldHeight)/2d)
                    {if(!RetireChain(ref n,group,count,out stop))return false;return false;}
                    dx=0;dy=speed;
                }
                bool turning=false;
                if(type==87 || type==454 || type==621)
                {
                    float radius=type==621?120:300;
                    if((Opposite(n.Vx,dx) || Opposite(n.Vy,dy)) && Math.Abs(n.Vx)+Math.Abs(n.Vy)>acceleration/2f && distance<radius)
                    {turning=true;if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed){n.Vx*=1.1f;n.Vy*=1.1f;}}
                    bool upward=type==87?n.Bounds.CenterY>pcy || (double)(pcy/16f)>e.GravityWorldSurface || n.PlayerNativeDead:n.Y>player.Y || n.PlayerNativeDead;
                    if(upward)
                    {
                        turning=true;
                        if(Math.Abs(n.Vx)<speed/2f){if(n.Vx==0)n.Vx-=n.Direction;n.Vx*=1.1f;}
                        else if(n.Vy>-speed)n.Vy-=acceleration;
                    }
                }
                if(!turning)
                {
                    if(SameSign(n.Vx,dx) || SameSign(n.Vy,dy))
                    {
                        Toward(ref n.Vx,dx,acceleration);Toward(ref n.Vy,dy,acceleration);
                        if(Math.Abs(dy)<speed*.2f && Opposite(n.Vx,dx))n.Vy+=n.Vy>0?acceleration*2f:-acceleration*2f;
                        if(Math.Abs(dx)<speed*.2f && Opposite(n.Vy,dy))n.Vx+=n.Vx>0?acceleration*2f:-acceleration*2f;
                    }
                    else if(ax>ay)
                    {Toward(ref n.Vx,dx,(double)acceleration*1.1f);if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed*.5f)n.Vy+=n.Vy>0?acceleration:-acceleration;}
                    else
                    {Toward(ref n.Vy,dy,(double)acceleration*1.1f);if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed*.5f)n.Vx+=n.Vx>0?acceleration:-acceleration;}
                }
            }
            if(type==117 || type==510 || type==513 || type==621)n.L0=earth?1:0;
            return true;
        }
        private static bool Speed(NpcMotionState n,NpcMotionState[] group,int count,out float speed,out float acceleration,out PredictionStop stop)
        {
            stop=PredictionStop.None;int t=n.EffectiveType;speed=8;acceleration=.07f;
            if(t==375){speed=6;acceleration=.15f;}else if(t==402){speed=9;acceleration=.3f;}else if(t==87){speed=11;acceleration=.25f;}else if(t==454){speed=20;acceleration=.55f;}
            else if(t==621){speed=15;acceleration=.45f;}else if(t==513){speed=7;acceleration=.1f;}
            else if(t==510){speed=!n.PlayerNativeDead && n.PlayerSandstorm?16:10;acceleration=!n.PlayerNativeDead && n.PlayerSandstorm?.35f:.25f;}
            if(t==117)
            {
                if(!n.WallBossCaptured){stop=PredictionStop.MissingDependency;return false;}
                if(n.WallBossValid)
                {
                    int owner=Find(n.WallBossIdentity,group,count);if(owner<0){stop=PredictionStop.MissingDependency;return false;}
                    float ratio=(float)group[owner].Life/(float)group[owner].LifeMax;
                    if(ratio<.5f){speed+=1;acceleration+=.1f;}if(ratio<.25f){speed+=1;acceleration+=.1f;}if(ratio<.1f){speed+=2;acceleration+=.1f;}
                }
            }
            return true;
        }
        private static bool Link(ref NpcMotionState n,NpcMotionState parent,out PredictionStop stop)
        {
            stop=PredictionStop.None;float dx=parent.Bounds.CenterX-n.Bounds.CenterX,dy=parent.Bounds.CenterY-n.Bounds.CenterY;
            float length=(float)Math.Sqrt((double)dx*dx+(double)dy*dy),spacing=n.Width;int type=n.EffectiveType;
            if(type>=87 && type<=92)spacing=42;else if(type>=454 && type<=459)spacing=36;else if(type>=513 && type<=515)spacing-=6;else if(type>=412 && type<=414)spacing+=6;else if(type>=621 && type<=623)spacing=24;
            if(length==0){stop=PredictionStop.InvalidState;return false;} // Original writes NaN; never invent a direction.
            float amount=(length-spacing)/length;dx*=amount;dy*=amount;n.Vx=n.Vy=0;n.X+=dx;n.Y+=dy;
            if(Flight(type))Sprite(ref n,dx);return true;
        }
        private static bool Sense(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out bool earth,out PredictionStop stop)
        {
            earth=false;stop=PredictionStop.None;
            for(int x=Math.Max(0,(int)(n.X/16f)-1);x<Math.Min(e.WorldWidth,(int)((n.X+n.Width)/16f)+2);x++)
            for(int y=Math.Max(0,(int)(n.Y/16f)-1);y<Math.Min(e.WorldHeight,(int)((n.Y+n.Height)/16f)+2);y++)
            {
                PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;
                if((tile.Active && (tile.Solid || tile.SolidTop && tile.SurfacePlatform) || tile.Liquid>64) && n.X+n.Width>x*16 && n.X<x*16+16 && n.Y+n.Height>y*16 && n.Y<y*16+16)earth=true;
            }
            return true;
        }
        private static bool RetireChain(ref NpcMotionState n,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.WormRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
            // Preflight identities before mutating any caller-owned row. Native
            // stops on child0/inactive/other style. Cycles have no finite proof.
            NpcIdentity next=n.ChildIdentity;int hops=0;
            while(next.Slot>0 && next.Slot<200)
            {
                int index=Find(next,group,count);if(index<0 || ++hops>count){stop=PredictionStop.MissingDependency;return false;}
                if(!group[index].Active || group[index].Style!=6)break;next=group[index].ChildIdentity;
            }
            next=n.ChildIdentity;bool any=false;
            while(next.Slot>0 && next.Slot<200)
            {
                int index=Find(next,group,count);if(!group[index].Active || group[index].Style!=6)break;
                next=group[index].ChildIdentity;var child=group[index];child.Active=false;group[index]=child;any=true;
            }
            n.Active=false;if(any)n.Life=0;stop=PredictionStop.Despawn;return true;
        }
        private static bool Retire(ref NpcMotionState n,out PredictionStop stop){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
        private static int Find(NpcIdentity identity,NpcMotionState[] group,int count)
        {if(identity.Token==null || group==null)return -1;for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        private static void StardustPrelude(ref NpcMotionState n,PredictionEnvironment e)
        {
            n.A2++;
            float length=(float)Math.Sqrt((double)n.Vx*n.Vx+(double)n.Vy*n.Vy);
            if(length>=1 && n.A2<=570)
            {
                // Original Utils.GetLerpValue returns a float to MathHelper.Lerp.
                // Below285 its clamped value is0;570 produces the .5 factor.
                float t=n.A2<285?0:(n.A2-285)/285;
                float factor=(float)(1d-.5d*t);n.Vx*=factor;n.Vy*=factor;
            }
            if(n.A2==570 && !e.Multiplayer)
            {
                // The original random540 marks only choose external spawns.
                // No mark/count/RNG return feeds this actor's motor/receiving.
                // Keep SafeNormalize(Vector2.Zero)*6, including its zero case.
                if(float.IsNaN(n.Vx) || float.IsNaN(n.Vy) || n.Vx==0 && n.Vy==0)n.Vx=n.Vy=0;
                else
                {
                    length=(float)Math.Sqrt((double)n.Vx*n.Vx+(double)n.Vy*n.Vy);
                    double inverse=1d/length;n.Vx=(float)(n.Vx*inverse);n.Vy=(float)(n.Vy*inverse);n.Vx*=6;n.Vy*=6;
                }
            }
            if(n.A2>=600 && !e.Multiplayer)n.A2=0;
        }
        private static bool Solitary(int type){return type==375 || type==402;}
        private static bool SolarGoal(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,MotionRect player,NpcMotionState[] group,int count,ref float goalX,ref float goalY,out float speed,out float acceleration,out PredictionStop stop)
        {
            speed=10;acceleration=.3f;stop=PredictionStop.None;
            int px=(int)(player.CenterX/16f),py=(int)(player.CenterY/16f),ground=-1;
            // WorldGen.SolidTile2 scans columns first and stops at a positive
            // row. Its slope/platform rule is not ordinary rectangle Solid.
            for(int x=px-2;x<=px+2;x++)
            {
                for(int y=py;y<=py+15;y++)
                {
                    if(x<0 || y<0 || x>=e.WorldWidth || y>=e.WorldHeight)continue; // Native catch is false.
                    PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    // Known native null is solid to SolidTile2. Existing Tile
                    // cannot express it and honestly remains unavailable; it
                    // must never be silently substituted with an empty cell.
                    if(tile.Active && tile.RawSolid && !tile.Half && (tile.Slope==0 || tile.Platform && tile.TopSlope)){ground=y;break;}
                }
                if(ground>0)break;
            }
            if(ground>0)
            {
                int hover=ground*16-800;
                if(player.Y>hover)
                {
                    goalY=hover;
                    if(Math.Abs(n.Bounds.CenterX-player.CenterX)<500)goalX=player.CenterX+(n.Vx>0?600:-600);
                }
            }
            else{speed=14;acceleration=.5f;}
            float length=NativeLength(n.Vx,n.Vy),upper=speed*1.3f,lower=speed*.7f;
            if(length>0 && (length>upper || length<lower))
            {NativeNormalize(ref n.Vx,ref n.Vy,length);float scale=length>upper?upper:lower;n.Vx*=scale;n.Vy*=scale;}
            int radius=ground>0?400:60;float push=ground>0?1000:200;
            // Complete means Source captured every active412 motor and its
            // required chain, in native slot order. Other types never enter
            // this census. Later slots still expose their pre-action center.
            if(group==null){stop=PredictionStop.MissingDependency;return false;}
            // Raw ai1 permits dummy200, while native crowd stops before200.
            // A captured raw value must not become an extra crowd actor.
            for(int i=0;i<count;i++)
            {
                var other=group[i];if(!other.Active || other.EffectiveType!=412 || other.Identity.Equals(n.Identity) || other.Identity.Slot<0 || other.Identity.Slot>=NpcPredictionCache.Capacity)continue;
                float dx=other.Bounds.CenterX-n.Bounds.CenterX,dy=other.Bounds.CenterY-n.Bounds.CenterY;length=NativeLength(dx,dy);
                if(length<radius)
                {
                    // Zero-vector Normalize has the original NaN local
                    // behavior; do not invent a direction or drop a neighbor.
                    NativeNormalize(ref dx,ref dy,length);dx*=push;dy*=push;goalX-=dx;goalY-=dy;
                }
            }
            return true;
        }
        private static float NativeLength(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static void NativeNormalize(ref float x,ref float y,float length)
        {double inverse=1d/length;x=(float)(x*inverse);y=(float)(y*inverse);}
        private static bool Flight(int type){return type>=87 && type<=92 || type>=454 && type<=459 || type>=621 && type<=623;}
        private static bool Tail(int type){return type==92 || type==119 || type==414 || type==459 || type==512 || type==515 || type==623;}
        private static bool SameSign(float a,float b){return a>0 && b>0 || a<0 && b<0;}
        private static bool Opposite(float a,float b){return a>0 && b<0 || a<0 && b>0;}
        private static void Sprite(ref NpcMotionState n,float dx){if(dx<0)n.SpriteDirection=1;else if(dx>0)n.SpriteDirection=-1;}
        private static void Toward(ref float value,float target,double amount){if(value<target)value=(float)(value+amount);else if(value>target)value=(float)(value-amount);}
    }
}
