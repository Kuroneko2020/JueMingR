using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI45 writes the global boss slot before its motor. AI46/47/48 read that
    // slot without an active/type check; Main clears it only at frame start.
    // Part existence is a separate global collection, not an owned-child pair.
    internal static class NpcGolemMotion
    {
        internal static bool Known(NpcMotionState n)
        {int type=n.EffectiveType;return type==245 && n.Style==45 || type==246 && n.Style==46 || (type==247 || type==248) && n.Style==47 || type==249 && n.Style==48;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !(n.GolemBossCaptured && !n.GolemBossValid && n.Style!=45) && (!n.TargetCaptured || n.PlayerActive && (!n.PlayerNativeDead || n.HasClosestPlayer));}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            // Choose the possible real TargetClosest timeline for the closure;
            // this does not execute that call early. Initial parts retain the
            // observed dummy255 biome/geometry until their actual AI branch.
            return true;
        }
        internal static void BeginFrame(NpcMotionState[] group,int count)
        {
            if(group==null || count<0 || count>group.Length)return;
            for(int i=0;i<count;i++)
            {
                var n=group[i];if(!Known(n) || !n.GolemBossCaptured || !n.GolemBossValid)continue;
                int owner=Find(group,count,n.GolemBossIdentity);
                // Missing an observed identity is not evidence of death.
                if(owner>=0 && (!group[owner].Active || group[owner].EffectiveType!=245))
                {n.GolemBossValid=false;n.GolemBossIdentity=default(NpcIdentity);group[i]=n;}
            }
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.GolemRelationsCaptured || !n.GolemRelationsComplete || !n.GolemBossCaptured || !n.GolemBalanceCaptured || group==null || count<1 || count>group.Length)
            {stop=PredictionStop.MissingDependency;return false;}
            float balance=n.GolemBalance;if(e.GoodWorld)balance+=n.Style==45?2:3;
            if((!e.Temple && !e.Jungle) || (double)e.PlayerY<e.GravityWorldSurface*16)balance*=2;
            if(n.Style==45)
            {
                WriteBoss(ref n,group,count);
                return Body(ref n,ref e,terrain,balance,group,count,confused,out stop);
            }
            if(n.Style==46)n.NoTileCollide=true;
            // Free-head visibility happens BEFORE the native orphan strike.
            bool blocked=false;
            if(n.Style==48)
            {
                bool clear;if(terrain==null || !terrain.CanHit(Point(n.Bounds.CenterX,n.Bounds.CenterY),Point(e.PlayerX,e.PlayerY),out clear,out stop))return false;
                blocked=!clear;if(blocked)n.NoTileCollide=true;
                else if(n.NoTileCollide){bool solid;if(!SolidTiles(n,e,terrain,out solid,out stop))return false;if(solid)n.NoTileCollide=false;}
            }
            if(!n.GolemBossValid)return Orphan(ref n,e,group,count,out stop);
            int owner=Find(group,count,n.GolemBossIdentity);if(owner<0){stop=PredictionStop.MissingDependency;return false;}
            var body=group[owner];if(body.Identity.Equals(n.Identity))body=n;
            if(n.TargetCaptured && n.PlayerGhost && elapsed>1){stop=PredictionStop.MissingDependency;return false;}
            if(n.Style==46)return Head(ref n,ref e,body,balance,confused,out stop);
            if(n.Style==47)return Fist(ref n,ref e,body,balance,terrain,confused,out stop);
            return FreeHead(ref n,ref e,body,balance,blocked,confused,out stop);
        }
        private static bool Body(ref NpcMotionState n,ref PredictionEnvironment e,IPredictionTerrain t,float balance,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L0==0)
            {
                // Server birth creates three identities. The current sample
                // cannot manufacture them; the real next sample resumes.
                if(!e.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}n.L0=1;
            }
            if(n.Target>=0 && Dead(n,e)){Face(ref n,ref e,confused);if(Dead(n,e))n.NoTileCollide=true;}
            Fade(ref n,0);
            bool head=false,left=false,right=false;
            for(int i=0;i<count;i++)if(group[i].Active)
            {switch(group[i].EffectiveType){case 246:head=true;break;case 247:left=true;break;case 248:right=true;break;}}
            n.Health.DontTakeDamage=head;n.CanReceive=!head && !n.Health.Immortal;
            if(e.GoodWorld && !e.Multiplayer && n.Vy>0 && !TorchScan(n,e,t,0,out stop))return false;
            // Native dust briefly adds/subtracts netOffset. Preserve its float
            // position writes even though dust RNG has no motor consumer.
            n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;
            if(n.NoTileCollide && !Dead(n,e))
            {
                if(n.Vy>0 && n.Y+n.Height>e.PlayerY-e.PlayerHeight*.5f)n.NoTileCollide=false;
                else
                {
                    bool clear;if(t==null || !t.CanHit(n.Bounds,Point(e.PlayerX,e.PlayerY),out clear,out stop))return false;
                    if(clear){bool solid;if(!SolidTiles(n,e,t,out solid,out stop))return false;if(!solid)n.NoTileCollide=false;}
                }
            }
            if(n.A0==0 && n.Vy==0)
            {
                n.Vx*=.8f;float rate=1;
                if(n.A1>0)
                {if(!left)rate+=2;if(!right)rate+=2;if(!head)rate+=2;if(n.Life<n.LifeMax)rate++;if(n.Life<n.LifeMax/2)rate+=4;if(n.Life<n.LifeMax/3)rate+=8;rate*=balance;if(e.GoodWorld)rate+=100;}
                n.A1+=rate;
                if(n.A1>=300)n.A1=-20;
                else if(n.A1==-1)
                {n.NoTileCollide=true;Face(ref n,ref e,confused);n.Vx=4*n.Direction;n.Vy=n.Life<n.LifeMax?-12.1f*(balance+9)/10:-12.1f;if(n.Vy<-19.1f)n.Vy=-19.1f;n.A0=1;n.A1=0;}
            }
            else if(n.A0==1)
            {
                if(n.Vy==0)n.A0=0;
                else
                {
                    Face(ref n,ref e,confused);float px=e.PlayerX-e.PlayerWidth*.5f;
                    if(n.X<px && n.X+n.Width>px+e.PlayerWidth)
                    {n.Vx*=.9f;if(n.Y+n.Height<e.PlayerY-e.PlayerHeight*.5f)n.Vy+=.2f*(balance+1)/2;}
                    else
                    {if(n.Direction<0)n.Vx-=.2f;else if(n.Direction>0)n.Vx+=.2f;float cap=3;if(n.Life<n.LifeMax)cap++;if(n.Life<n.LifeMax/2)cap++;if(n.Life<n.LifeMax/4)cap++;cap*=(balance+1)/2;n.Vx=Math.Max(-cap,Math.Min(cap,n.Vx));}
                }
            }
            if(n.Target<=0 || n.Target==255 || Dead(n,e))Face(ref n,ref e,confused);
            if(Far(n,e)){Face(ref n,ref e,confused);if(Far(n,e)){n.Active=false;stop=PredictionStop.Despawn;return false;}}
            return true;
        }
        private static bool Head(ref NpcMotionState n,ref PredictionEnvironment e,NpcMotionState body,float balance,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;float x=body.Bounds.CenterX-n.Bounds.CenterX,y=body.Bounds.CenterY-n.Bounds.CenterY;y-=57*n.Scale;x-=3*n.Scale;
            float length=ManualLength(x,y);if(length<100){n.Vx=x;n.Vy=y;}else{float factor=100/length;n.Vx=x*factor;n.Vy=y*factor;}
            Fade(ref n,30);
            if(n.A0==0)
            {n.A1++;if(n.A1<20 || n.A1>280){n.A1+=2*(balance-1)/3;n.L0=1;}else{n.A1+=(balance-1)/2;n.L0=0;}if(n.A1>=300){Face(ref n,ref e,confused);n.A1=0;}}
            else if(n.A0==1)
            {
                Face(ref n,ref e,confused);n.L1=e.PlayerX<n.Bounds.CenterX-n.Width?-1:e.PlayerX>n.Bounds.CenterX+n.Width?1:0;
                float rate=(balance+3)/4;n.A1+=rate;if((double)n.Life<n.LifeMax*.4)n.A1+=rate;if((double)n.Life<n.LifeMax*.2)n.A1+=rate;
                n.L0=n.A1<20 || n.A1>280?1:0;if(n.A1>=300){Face(ref n,ref e,confused);n.A1=0;}
                // ai2 is a randomized projectile clock. No subsequent AI46
                // motor/facing/receiving branch reads it; do not fake its RNG
                // and do not discard this deterministic attachment window.
            }
            n.A0=n.Life<n.LifeMax/2?1:0;return true;
        }
        private static bool Fist(ref NpcMotionState n,ref PredictionEnvironment e,NpcMotionState body,float balance,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;Fade(ref n,0);
            float anchorX=body.Bounds.CenterX+body.Vx,anchorY=body.Bounds.CenterY+body.Vy;anchorY+=-9*n.Scale;anchorX+=(n.EffectiveType==247?-84:78)*n.Scale;
            float x=anchorX-n.Bounds.CenterX,y=anchorY-n.Bounds.CenterY,length=XnaLength(x,y);bool hurt=body.Life<body.LifeMax;
            if(n.A0==0)
            {
                n.NoTileCollide=true;float speed=14;if(n.Life<n.LifeMax/2)speed+=3;if(n.Life<n.LifeMax/4)speed+=3;if(hurt)speed+=8;speed*=(balance+3)/4;speed=Math.Min(32,speed);
                if(length<12+speed)
                {
                    n.Vx=x;n.Vy=y;n.A1+=balance;if(n.Life<n.LifeMax/2)n.A1+=balance;if(n.Life<n.LifeMax/4)n.A1+=balance;if(hurt)n.A1+=10*balance;
                    if(n.A1>=60){Face(ref n,ref e,confused);n.A1=0;if(n.EffectiveType==247 && n.Bounds.CenterX+100>e.PlayerX || n.EffectiveType==248 && n.Bounds.CenterX-100<e.PlayerX)n.A0=1;}
                }
                else{float factor=speed/length;n.Vx=x*factor;n.Vy=y*factor;}
            }
            else if(n.A0==1)
            {
                n.A1++;n.X=anchorX-n.Width*.5f;n.Y=anchorY-n.Height*.5f;n.Vx=n.Vy=0;
                if(n.A1>=30)
                {
                    n.NoTileCollide=true;n.CollideX=n.CollideY=false;n.A0=2;n.A1=0;float speed=12;if(n.Life<n.LifeMax/2)speed+=4;if(n.Life<n.LifeMax/4)speed+=4;if(hurt)speed+=10;speed*=(balance+3)/4;speed=Math.Min(speed,48);
                    Aim(ref n,e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY,speed);
                }
            }
            else if(n.A0==2)
            {
                if(e.GoodWorld && !e.Multiplayer && !TorchScan(n,e,t,1,out stop))return false;n.A1++;
                if(Math.Abs(n.Vx)>Math.Abs(n.Vy))
                {if(n.Vx>0 && n.Bounds.CenterX>e.PlayerX || n.Vx<0 && n.Bounds.CenterX<e.PlayerX)n.NoTileCollide=false;}
                else if(n.Vy>0 && n.Bounds.CenterY>e.PlayerY || n.Vy<0 && n.Bounds.CenterY<e.PlayerY)n.NoTileCollide=false;
                if(length>700 || n.CollideX || n.CollideY){n.NoTileCollide=true;n.A0=0;}
            }
            else if(n.A0==3)
            {n.NoTileCollide=true;float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,factor=12/ManualLength(dx,dy);dx*=factor;dy*=factor;Accelerate(ref n.Vx,dx,.4f,2);Accelerate(ref n.Vy,dy,.4f,2);}
            return true;
        }
        private static bool FreeHead(ref NpcMotionState n,ref PredictionEnvironment e,NpcMotionState body,float balance,bool blocked,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;Face(ref n,ref e,confused);float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY-300,factor=7/ManualLength(x,y);x*=factor;y*=factor;
            Accelerate(ref n.Vx,x,.05f,1);Accelerate(ref n.Vy,y,.05f,1);
            float rate=(balance+4)/5;n.A1+=rate;if((double)body.Life<body.LifeMax*.8)n.A1+=rate;if((double)body.Life<body.LifeMax*.6)n.A1+=rate;if((double)body.Life<body.LifeMax*.2)n.A1+=rate;if((double)body.Life<body.LifeMax*.1)n.A1+=rate;
            n.L0=n.A1<20 || n.A1>280?1:0;if(blocked)n.A1=20;if(n.A1>=300){Face(ref n,ref e,confused);n.A1=0;}
            // AI48's ai2 and random target-leading only feed emitted lasers.
            // Their world effects are new inputs; the own-body motor above
            // consumes no randomized clock or projectile state.
            if(!e.GoodWorld){n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;}return true;
        }
        private static bool Orphan(ref NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.GolemOrphanDamageCaptured || n.GolemOrphanDamage<1){stop=PredictionStop.MissingDependency;return false;}
            if(!n.Active || n.Life<=0)return true;n.JustHit=true;if(n.Health.Immortal)return true;
            n.Life-=n.GolemOrphanDamage;
            if(n.Life<=0)
            {
                // A lethal attached head creates249 in native HitEffect on
                // server. Later collection consumers require a fresh sample.
                if(n.Style==46 && !e.Multiplayer)for(int i=0;i<count;i++)if(Known(group[i]) && !group[i].Identity.Equals(n.Identity)){var other=group[i];other.GolemRelationsComplete=false;group[i]=other;}
                n.Active=false;stop=PredictionStop.Despawn;return false;
            }
            return true;
        }
        private static bool SolidTiles(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;int left=(int)(n.X/16),right=(int)((n.X+n.Width)/16),top=(int)(n.Y/16),bottom=(int)((n.Y+n.Height)/16);
            if(left<0 || right>=e.WorldWidth || top<0 || bottom>=e.WorldHeight-40){solid=true;return true;}
            for(int x=left;x<=right;x++)for(int y=top;y<=bottom;y++)
            {PredictionTile cell;if(!t.Tile(x,y,out cell,out stop))return false;if(cell.Active && cell.RawSolid && !cell.SolidTop){solid=true;return true;}}
            return true;
        }
        private static bool TorchScan(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int pad,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(t==null){stop=PredictionStop.TerrainUnavailable;return false;}
            for(int x=(int)(n.X/16)-pad;x<(n.X+n.Width)/16+pad;x++)for(int y=(int)(n.Y/16)-pad;y<(n.Y+n.Width)/16+pad;y++)
            {PredictionTile cell;if(!t.Tile(x,y,out cell,out stop))return false;}
            // Locked TileID.Sets.Torches contains only type4, a non-solid.
            // Native deactivation changes no Golem/PC collision consumer. The
            // observed real edit still retires the old terrain publication.
            return true;
        }
        private static void WriteBoss(ref NpcMotionState n,NpcMotionState[] group,int count)
        {n.GolemBossValid=true;n.GolemBossIdentity=n.Identity;for(int i=0;i<count;i++)if(Known(group[i]) && !group[i].Identity.Equals(n.Identity)){var other=group[i];other.GolemBossCaptured=true;other.GolemBossValid=true;other.GolemBossIdentity=n.Identity;group[i]=other;}}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity identity){for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        private static void Fade(ref NpcMotionState n,float clock){if(n.Alpha>0){n.Alpha=Math.Max(0,n.Alpha-10);n.A1=clock;}}
        private static bool Dead(NpcMotionState n,PredictionEnvironment e){return n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;}
        private static bool Far(NpcMotionState n,PredictionEnvironment e){return Math.Abs(n.Bounds.CenterX-e.PlayerX)+Math.Abs(n.Bounds.CenterY-e.PlayerY)>3000;}
        private static MotionRect Point(float x,float y){return new MotionRect(x,y,1,1);}
        private static void Face(ref NpcMotionState n,ref PredictionEnvironment e,bool confused){NpcTargeting.Face(ref n,ref e,confused:confused);}
        private static float XnaLength(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static float ManualLength(float x,float y){float squared=x*x+y*y;return (float)Math.Sqrt(squared);}
        private static void Aim(ref NpcMotionState n,float x,float y,float speed){float factor=speed/ManualLength(x,y);n.Vx=x*factor;n.Vy=y*factor;}
        private static void Accelerate(ref float value,float desired,float acceleration,float opposite)
        {if(value<desired){value+=acceleration;if(value<0 && desired>0)value+=acceleration*opposite;}else if(value>desired){value-=acceleration;if(value>0 && desired<0)value-=acceleration*opposite;}}
    }
}
