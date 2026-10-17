using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 relation/lifecycle controllers and receiving boss phases.
    // Only scalar copies are advanced. A life-only owner is not permission to
    // replay that owner's movement, random choices, attacks or native AI.
    internal static class NpcRelatedMotion
    {
        internal static bool Known(NpcMotionState n)
        {
            int type=n.EffectiveType;
            return type==523 && n.Style==101 ||
                type==399 && n.Style==80 ||
                type==395 && n.Style==76 && (n.A0==1 || (n.A0==2 || n.A0==-2) && n.CanReceive) ||
                type==266 && n.Style==54 && (n.A0<0 || n.A0==2 && n.L3>0 && n.CanReceive) && n.L0!=0 ||
                type==398 && n.Style==77 && n.A0==1 && n.L3!=0;
        }
        internal static bool NeedsPlayer(NpcMotionState n)
        {return n.EffectiveType==399?n.A0==1 || n.A0==0 && (!n.SearchPlayerCaptured || n.SearchPlayerIndex>=0 && !n.SearchPlayerDead):n.EffectiveType==395?!n.TargetCaptured || n.HasPlayer && !n.PlayerDead || n.HasClosestPlayer && !n.ClosestPlayerDead:n.EffectiveType!=523;}
        internal static bool Retargets(NpcMotionState n)
        {return n.EffectiveType==266 || n.EffectiveType==398 || n.EffectiveType==395 && n.TargetCaptured && (!n.HasPlayer || n.PlayerDead || n.Target<0 || n.Target==255 || n.PlayerVelocityCaptured && Distance(n.Bounds.CenterX,n.Bounds.CenterY,n.PlayerCenterX,n.PlayerCenterY)>5600);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.A0==b.A0;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(n.EffectiveType==523)return AncientDoom(ref n,group,count,out stop);
            if(n.EffectiveType==266)return Brain(ref n,e,out stop);
            if(n.EffectiveType==398)return Core(ref n,e,out stop);
            if(n.EffectiveType==399)return Probe(ref n,e,terrain,confused,out stop);
            if(n.EffectiveType==395)return Saucer(ref n,e,terrain,confused,out stop);
            stop=PredictionStop.UnsupportedMechanism;return false;
        }
        private static bool Saucer(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.A0!=1 && n.A0!=2 && n.A0!=-2){stop=PredictionStop.UnsupportedMechanism;return false;}
            bool invalid=n.TargetCaptured?!n.HasPlayer || n.PlayerDead:e.PlayerDead;
            if(invalid || n.Target<0 || n.Target==255 || Distance(n.Bounds.CenterX,n.Bounds.CenterY,e.PlayerX,e.PlayerY)>5600)
            {NpcTargeting.Face(ref n,ref e,true,confused);invalid=n.TargetCaptured?!n.HasPlayer || n.PlayerDead:e.PlayerDead;}
            if((invalid || Distance(n.Bounds.CenterX,n.Bounds.CenterY,e.PlayerX,e.PlayerY)>5600) && n.A0==2)n.A0=-2;
            if(n.A0==-2)
            {
                n.Vy-=.4f;n.TimeLeft=Math.Min(n.TimeLeft,10);
                if(!(n.TargetCaptured?n.PlayerDead:e.PlayerDead))
                {
                    // A living distant target restores phase2 in this same
                    // action, but does not execute that phase's motor yet.
                    n.TimeLeft=300;n.A0=2;n.A1=n.A2=n.A3=0;
                }
            }
            else if(n.A0==1)
            {
                n.CanReceive=true;n.Health.DontTakeDamage=false;n.Vx*=.96f;n.Vy*=.96f;n.A1++;
                if(n.A1>=150){n.A0=2;n.A1=0;}
            }
            else
            {
                int before=n.A3%120>=60?1:0;n.A3++;int attack=n.A3%120>=60?1:0;
                if(attack!=before && attack==1)n.A2=e.PlayerX-n.Bounds.CenterX>0?1:-1;
                if(n.A3>=3600){n.A0=2;n.A1=n.A2=n.A3=0;}
                else if(attack==0)
                {
                    float dx=e.PlayerX+n.A2*350-n.Bounds.CenterX,dy=e.PlayerY-250-n.Bounds.CenterY;
                    double squared=(double)dx*dx+(double)dy*dy;
                    if(squared==0){stop=PredictionStop.InvalidState;return false;}
                    // Original AI76 calls XNA Vector2.Normalize directly.
                    // Locked x86 XNA retains an extended reciprocal until
                    // each float component assignment; plain float locals
                    // change real long steering paths at one-ULP boundaries.
                    float root=(float)Math.Sqrt(squared);double scale=1d/root;
                    dx=(float)(dx*scale);dy=(float)(dy*scale);dx*=16;dy*=16;
                    n.Vx=n.Vx+(dx-n.Vx)*.1f;n.Vy=n.Vy+(dy-n.Vy)*.1f;
                }
                else
                {
                    if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                    int x=(int)n.Bounds.CenterX/16,y=(int)(n.Y+n.Height)/16,below=0;
                    PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.Active && tile.Solid && !tile.SolidTop)below=1;
                    else for(;below<150 && y+below<e.WorldHeight;below++)
                    {
                        if(!terrain.Tile(x,y+below,out tile,out stop))return false;
                        if(tile.Active && tile.Solid && !tile.SolidTop){below--;break;}
                    }
                    float height=below*16;
                    if(height<250){float desired=-4;if(4>height)desired=-height;n.Vy=n.Vy+(desired-n.Vy)*.05f;}
                    else n.Vy*=.95f;
                    n.Vx=8*n.A2;
                }
            }
            if(n.X<-100 || n.Y<-100 || n.X>e.WorldWidth*16+100 || n.Y>e.WorldHeight*16+100)
            {
                n.X=Math.Max(-100,Math.Min(e.WorldWidth*16+100,n.X));n.Y=Math.Max(-100,Math.Min(e.WorldHeight*16+100,n.Y));
                n.Active=false;stop=PredictionStop.Despawn;return false;
            }
            return true;
        }
        private static float Distance(float x,float y,float a,float b)
        {float dx=x-a,dy=y-b;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static bool Probe(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.A0==0)
            {
                // TargetClosest only supplies initial facing. FindClosestPlayer
                // below is a separate two-pass Euclidean search and does not
                // write target or replace the rolling player's environment.
                if(n.Direction==0){var facing=e;NpcTargeting.Face(ref n,ref facing,true,confused);}
                if(n.CollideX)n.Direction=-n.Direction;
                n.Vx=3*n.Direction;
                int x=(int)(n.Bounds.CenterX/16),y=(int)(n.Bounds.CenterY/16),below=30;
                if(x>=30 && y>=30 && x<e.WorldWidth-30 && y<e.WorldHeight-30)
                {
                    if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                    for(int i=0;i<30;i++)
                    {
                        PredictionTile tile;if(!terrain.Tile(x,y+i,out tile,out stop))return false;
                        if(tile.Active && tile.Solid && !tile.SolidTop && !tile.Half && tile.Slope==0){below=i;break;}
                    }
                }
                if(below<15)n.Vy=Math.Max(n.Vy-.05f,-3.5f);
                else if(below<20)n.Vy*=.95f;
                else n.Vy=Math.Min(n.Vy+.05f,1.5f);
                int player;MotionRect area;bool alive,dead;float distance;
                if(!Search(n,e,out player,out area,out alive,out dead,out distance,out stop))return false;
                if(player<0 || dead)return true;
                if(!alive || !e.PlayerTimelineActive || player!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}
                if((float)Math.Sqrt(distance)<352 && area.CenterY>n.Bounds.CenterY){n.A0=1;n.A1=0;}
            }
            else if(n.A0==1)
            {
                n.A1++;n.Vx*=.95f;n.Vy*=.95f;
                if(n.A1>=60)
                {
                    int player;MotionRect area;bool alive,dead;float distance;
                    if(!Search(n,e,out player,out area,out alive,out dead,out distance,out stop))return false;
                    if(player>=0 && (!alive || !e.PlayerTimelineActive || player!=e.PlayerIndex)){stop=PredictionStop.MissingDependency;return false;}
                    n.A1=0;n.A0=2;n.A3=player>=0 && area.CenterX>n.Bounds.CenterX?-1:1;
                }
            }
            else if(n.A0==2)
            {
                n.NoTileCollide=true;n.A1++;n.Vy=Math.Max(n.Vy-.1f,-10);
                // The native escape motor has only an upper X clamp. Its left
                // branch can pass -4; a symmetric clamp changes the whole arc.
                n.Vx=Math.Min(n.Vx+n.A3*.05f,4);
                if((n.Y<-n.Height || n.A1>=180) && !e.Multiplayer)
                {n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            return true;
        }
        private static bool Search(NpcMotionState n,PredictionEnvironment e,out int player,out MotionRect area,out bool alive,out bool dead,out float distance,out PredictionStop stop)
        {
            player=-1;area=default(MotionRect);alive=false;dead=false;distance=float.MaxValue;stop=PredictionStop.None;
            if(e.Players==null || !e.Players.SearchCaptured){stop=PredictionStop.MissingDependency;return false;}
            for(int pass=0;pass<2 && player<0;pass++)for(int i=0;i<e.Players.Count;i++)
            {
                bool valid=e.Players.SearchAliveAt(i);if(pass==0 && !valid)continue;
                int slot=e.Players.SlotAt(i);var candidate=e.Players.SearchAreaAt(i);
                if(e.PlayerTimelineActive && slot==e.PlayerIndex)candidate=new MotionRect(e.PlayerX-candidate.Width*.5f,e.PlayerY-candidate.Height*.5f,candidate.Width,candidate.Height);
                float dx=n.Bounds.CenterX-candidate.CenterX,dy=n.Bounds.CenterY-candidate.CenterY,squared=dx*dx+dy*dy;
                if(squared<distance || squared==distance && slot<player)
                {distance=squared;player=slot;area=candidate;alive=valid;dead=e.Players.SearchDeadAt(i);}
            }
            return true;
        }
        private static bool AncientDoom(ref NpcMotionState n,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;int index=-1;
            // Capture distinguishes native-invalid ownership from a missing
            // identity in our private group. Only the latter is unavailable.
            if(n.A1<0 || n.PositionParameter<0)
            {n.Active=false;stop=PredictionStop.Despawn;return false;}
            if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.PositionOwner)){index=i;break;}
            if(index<0){stop=PredictionStop.MissingDependency;return false;}
            var owner=group[index];
            if(!owner.Active || owner.EffectiveType!=439)
            {n.Active=false;stop=PredictionStop.Despawn;return false;}
            int clock=1;
            // Native uses integer division and strict comparisons. Half/quarter
            // equality must not advance the expiry one or two times faster.
            if(owner.Life<owner.LifeMax/2)clock=2;
            if(owner.Life<owner.LifeMax/4)clock=3;
            n.A1+=clock;float growth=Math.Max(0,Math.Min(1,n.A1/120f));
            n.Scale=growth;n.Alpha=(int)(255f-growth*255f);
            n.L0+=(float)Math.PI/60f;
            n.L1=.25f+(float)Math.Cos(n.A1*((float)Math.PI*2f)/60f)*.25f;
            // AI101 does not set velocity or resize its body. The usual zero
            // velocity is observed at birth, never imposed on a hit/packet.
            if(n.A1>=420){n.Active=false;stop=PredictionStop.Despawn;return false;}
            return true;
        }
        private static bool Brain(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(e.GoodWorld){stop=PredictionStop.UnsupportedMechanism;return false;}
            bool retreat=n.A0==2 && n.L3>0 && n.CanReceive;
            if(n.A0!=-1 && n.A0!=-2 && n.A0!=-3 && !retreat)
            {stop=PredictionStop.UnsupportedMechanism;return false;}
            NpcTargeting.Retarget(ref n,ref e,false);
            // The target observation owns ZoneCrimson; Retarget copies that
            // exact fact alongside its rectangle. Rolling verifies that the
            // resulting numbered player has the one acquired future timeline.
            if(!e.Multiplayer && Math.Abs(n.Bounds.CenterX-e.PlayerX)+Math.Abs(n.Bounds.CenterY-e.PlayerY)>6000)
            {n.Active=false;n.Life=0;stop=PredictionStop.Despawn;return false;}
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
            float length=(float)Math.Sqrt(dx*dx+dy*dy);
            if(length==0 && !retreat){stop=PredictionStop.InvalidState;return false;}
            float normal=retreat && length<1?1:(retreat?1:8)/length;dx*=normal;dy*=normal;
            n.CanReceive=true;n.Health.DontTakeDamage=false;n.L2=1;
            if(retreat)
            {
                n.Vx=dx;n.Vy=dy;n.Alpha-=5;
                if(n.Alpha<=0 && e.Crimson && !e.PlayerDead){stop=PredictionStop.PhaseBoundary;return false;}
                if(n.Alpha<=0){n.Alpha=0;n.A0=0;}
            }
            else{n.Vx=(n.Vx*50+dx)/51;n.Vy=(n.Vy*50+dy)/51;}
            if(n.A0==-1 && !e.Multiplayer)
            {
                // Every random threshold is >=60. A hit's random subtraction
                // can delay that boundary, but cannot bring it forward. Hold
                // the earliest clock and stop before any destination choice;
                // no private random draw or invented teleport is needed.
                if(n.L1+1>=60){stop=PredictionStop.RandomDestination;return false;}
                n.L1++;
            }
            else if(n.A0==-2)
            {
                n.Vx*=.9f;n.Vy*=.9f;n.A3+=e.Multiplayer?15:25;
                if(n.A3>=255)
                {n.A3=255;n.X=n.A1*16-n.Width/2;n.Y=n.A2*16-n.Height/2;n.A0=-3;n.NewSegment=true;}
                n.Alpha=(int)n.A3;
            }
            else if(n.A0==-3)
            {
                n.A3-=e.Multiplayer?15:25;
                if(n.A3<=0){n.A3=0;n.A0=-1;}
                n.Alpha=(int)n.A3;
            }
            // This is the common AI54 tail, including its receiving second
            // phase. Leaving Crimson changes the next action's motor to the
            // positive retreat state, irrespective of the negative fade clock.
            if(e.PlayerDead || !e.Crimson)
            {
                if(n.L3<120)n.L3++;
                if(n.L3>60)n.Vy+=(n.L3-60)*.25f;
                n.A0=2;n.Alpha=10;
            }
            else if(n.L3>0)n.L3--;
            return true;
        }
        private static bool Core(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.A0!=1 || n.L3==0){stop=PredictionStop.PhaseBoundary;return false;}
            n.CanReceive=true;n.Health.DontTakeDamage=false;
            NpcTargeting.Retarget(ref n,ref e,false);
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
            // Native server phase1 relocates the core, three parts and eyes
            // together at this strict distance. Their relocation closure is
            // not acquired here; stop before publishing an invented long arc.
            if(!e.Multiplayer && (float)Math.Sqrt(dx*dx+dy*dy)>2400)
            {stop=PredictionStop.PhaseBoundary;return false;}
            dy+=130;
            if((float)Math.Sqrt(dx*dx+dy*dy)<=20)return true;
            dx-=n.Vx;dy-=n.Vy;float length=(float)Math.Sqrt(dx*dx+dy*dy);
            // XNA Normalize(Zero) is invalid in this exact native branch. Do
            // not substitute an arbitrary escape direction for it.
            if(length==0){stop=PredictionStop.InvalidState;return false;}
            float normal=8/length;dx*=normal;dy*=normal;
            float oldX=n.Vx,oldY=n.Vy;
            Fly(ref n.Vx,dx);Fly(ref n.Vy,dy);
            // Native Vector2.Lerp(newVelocity, oldVelocity, .5) uses this
            // subtraction/addition order, independently of its Fly motor.
            n.Vx=n.Vx+(oldX-n.Vx)*.5f;n.Vy=n.Vy+(oldY-n.Vy)*.5f;
            return true;
        }
        private static void Fly(ref float value,float desired)
        {
            if(value<desired){value+=.5f;if(value<0 && desired>0)value+=.5f;}
            else if(value>desired){value-=.5f;if(value>0 && desired<0)value-=.5f;}
        }
    }
}
