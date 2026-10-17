using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI107 motor, including its old-target query before each later
    // targeting call. Projectiles, visual RNG and sound handles do not own the
    // actor's geometry and are never produced by this scalar continuation.
    internal static class NpcInvasionGroundMotion
    {
        internal static bool Known(NpcMotionState n)
        {int t=n.EffectiveType;return n.Style==107 && (t>=552 && t<=557 || t>=561 && t<=563 || t>=566 && t<=573 || t==576 || t==577 || t==620);}
        internal static bool NeedsPlayer(NpcMotionState n){return NpcInvasionTargeting.NeedsPlayer(n);}
        internal static bool Retargets(NpcMotionState n){return true;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        private static float Clamp(float x,float a,float b){return Math.Max(a,Math.Min(b,x));}
        private static float Distance(MotionRect a,MotionRect b){float x=b.CenterX-a.CenterX,y=b.CenterY-a.CenterY;return (float)Math.Sqrt(x*x+y*y);}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            NpcInvasionTarget target;if(!NpcInvasionTargeting.ReadTarget(n,e,elapsed,group,count,out target,out stop))return false;
            if(target.Kind==3 && target.Identity.Type==548)
                for(int i=0;i<count;i++)if(group[i].Identity.Equals(target.Identity) && group[i].HostileProtected)
                {if(!Target(ref n,e,t,elapsed,group,count,true,confused,out target,out stop))return false;break;}
            bool still=n.Vx==0 && n.Vy==0 && !n.JustHit,portal=type!=566 && type!=567 && type!=620,phasing=false;
            // Locked DD2Event.EnemiesShouldChasePlayers returns true in both
            // event branches. Ordinary Goblin Shark uses Main.dayTime instead.
            bool melee=false,ranged=false,hold=false,noStuck=false,jumpAttack=false,throwAttack=false,turnAtShot=false,chase=type!=620 || !e.Day,wanderDespawn=type==620 && e.Day;
            float speed=1,acceleration=.07f,friction=.8f,meleeRange=40,meleeFriction=.9f,rangedRange=700,cooldown=0,hitClock=30,arc=.1f;
            int meleeClock=30,meleeCooldown=0,rangedClock=70,shotClock=35,jumpClock=32,airClock=15;float jumpSpeed=9,offsetX=0,offsetY=0;
            bool faceDuringAttack=true,skipStuck=false;float front=n.Width/2+6;
            bool pitUp=n.DirectionY<0;
            if(portal && !target.Invalid)
            {
                // Locked Collision.CanHit only uses integer half dimensions.
                // 1x1 has the same zero half-size as native's 0x0 center query,
                // while preserving the terrain API's nonempty-area contract.
                bool clear;if(!t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(target.Area.CenterX,target.Area.CenterY,1,1),out clear,out stop))return false;
                phasing=!clear && (n.Direction==Math.Sign(target.Area.CenterX-n.Bounds.CenterX) || n.NoGravity && Distance(n.Bounds,target.Area)>50 && n.Bounds.CenterY>target.Area.CenterY);
                phasing&=n.A0<=0;
            }
            if(phasing){if(n.Vy==0 || Math.Abs(target.Area.CenterY-n.Bounds.CenterY)>800)n.NoGravity=n.NoTileCollide=true;}
            else n.NoGravity=n.NoTileCollide=false;
            switch(type)
            {
                case 576:case 577:
                    melee=true;front-=32;meleeCooldown=60;meleeRange=130;meleeClock=44;meleeFriction=.7f;
                    if(n.L0>0)n.L0--;
                    if(n.A0<=0)
                    {
                        float old=n.A1,distance=Distance(n.Bounds,target.Area);
                        if(n.L3>=60){if(distance<=430 && n.L0<=0)n.A1=2;else if(distance>160)n.A1=1;else if(distance<=130){n.A1=0;if(old==1)n.A0=0;}}
                    }
                    else if(n.A1==2)n.L0=300;
                    if((int)n.A1==2){meleeClock=90;meleeRange=250;jumpAttack=throwAttack=true;jumpClock=56;airClock=41;jumpSpeed=13;shotClock=36;faceDuringAttack=false;offsetX=-n.Direction*20;offsetY=n.Height/2-1;}
                    else if((int)n.A1==1){throwAttack=true;meleeClock=90;meleeRange=1000;meleeCooldown=240;faceDuringAttack=false;offsetX=n.Direction*30;offsetY=-70;}
                    if(n.A0<-meleeCooldown)n.A0=-meleeCooldown;
                    break;
                case 552:case 553:case 554:
                    melee=true;if(type==553){acceleration+=.01f;speed+=.2f;}if(type==554){acceleration+=.02f;speed+=.4f;}
                    if(n.L3<60)acceleration=.01f+n.L3/60*.05f;break;
                case 570:case 571:
                    ranged=hold=true;rangedClock=60;shotClock=40;faceDuringAttack=n.A1>40;
                    if(n.A1>10 && n.A1<=40 && (int)n.A1%(type==571?6:9)==0)shotClock=(int)n.A1-1;
                    offsetX=22*n.Direction;arc=.15f;rangedRange=600;speed=.77f;
                    if(n.L3<60){rangedRange=1;acceleration=.01f+n.L3/60*.05f;}break;
                case 572:case 573:
                    if(n.L3==0)n.L0=n.L1=-1;
                    if(n.A1==2)
                    {
                        float cx=n.Bounds.CenterX,cy=n.Bounds.CenterY;n.Width=n.Height=192;n.X=cx-96;n.Y=cy-96;n.Vx=n.Vy=0;n.Alpha=255;
                        n.A0++;if(n.A0>=3){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}return true;
                    }
                    if(n.A0>0 && n.A1==0 && n.Life>0)n.L2++;
                    if(n.A0>0 && n.A1==1){n.A0=0;n.A1=2;return true;}
                    speed=.88f;if(n.A0==1)n.A1=1;
                    melee=true;meleeClock=40;meleeRange=700;
                    if(n.A1>0 && n.A0==0){meleeRange=64;acceleration=.3f;speed=4;}
                    if(n.L3<60)acceleration=.01f+n.L3/60*.05f;break;
                case 568:case 569:
                    melee=true;meleeClock=110;acceleration=.16f;friction=.7f;speed=1.4f;meleeRange=600;
                    if(n.L3<60)acceleration=.01f+n.L3/60*.05f;
                    if(n.A0==0)n.L1=-1;
                    if(n.A0==1){n.A0++;n.L0++;if(n.A1>0)n.A1--;if(n.A1<=0){n.A1=60;if(!e.Multiplayer)n.Life+=Math.Min(n.LifeMax/20,n.LifeMax-n.Life);}}
                    break;
                case 561:case 562:case 563:
                    ranged=hold=true;rangedClock=90;shotClock=82;faceDuringAttack=n.A1>82;offsetY=-14;arc=0;rangedRange=600;
                    if(n.L3<60){rangedRange=1;acceleration=.01f+n.L3/60*.05f;}
                    if(type==561){speed=.88f;rangedRange-=100;}else if(type==562){speed=.94f;rangedRange-=50;}break;
                case 555:case 556:case 557:
                    ranged=hold=true;rangedClock=42;shotClock=18;faceDuringAttack=n.A1>18;offsetY=-14;arc=.4f;rangedRange=280;speed=type==557?1.12f:.88f;
                    if(n.L3<60){rangedRange=1;acceleration=.01f+n.L3/60*.05f;}break;
                case 566:case 567:
                    if(n.L3>=120){n.CanReceive=!n.Friendly && !n.Health.Immortal;n.Health.DontTakeDamage=false;}
                    else{acceleration=0;n.Health.DontTakeDamage=true;n.CanReceive=false;n.Vx=0;hold=noStuck=true;n.A3=0;if(n.L3==0)n.Alpha=255;n.L3++;if(n.L3>=110)n.Alpha=Math.Max(0,n.Alpha-26);}
                    break;
                case 620:
                    turnAtShot=true;cooldown=150;hitClock=20;ranged=hold=!e.Day;faceDuringAttack=n.A1>40;rangedClock=60;shotClock=40;
                    if(n.A1>10 && n.A1<=40 && (int)n.A1%5==0)shotClock=(int)n.A1-1;
                    offsetX=-4*n.Direction;offsetY=-20;arc=.15f;rangedRange=600;speed=8;acceleration*=3;friction=.9f;
                    if(n.Alpha==255){n.SpriteDirection=n.Direction;n.Vy=-6;}n.Alpha=Math.Max(0,n.Alpha-15);
                    if(!e.Day && n.Vy==0 && target.Hitbox.Y+target.Hitbox.Height<n.Y && Math.Abs(n.Bounds.CenterX-(target.Hitbox.X+(int)target.Hitbox.Width/2))<n.Width*3)
                    {bool clear;if(!t.CanHit(Integer(n.Bounds),target.Hitbox,out clear,out stop))return false;if(clear){int height=(int)((n.Y+n.Height-16-target.Hitbox.Y-target.Hitbox.Height)/16);if(height<27)n.Vy=height<11?-11:height<15?-13:height<19?-14:-15.9f;}}
                    break;
            }
            if(portal){if(n.L3==0)n.Alpha=255;if(n.L3<60){n.L3++;n.Alpha=Math.Max(0,n.Alpha-5);}}
            if(type==620 && n.Wet)
            {
                n.NoGravity=true;n.TrackingKind=0;var playerEnvironment=NpcTargeting.Player(n,e);NpcTargeting.Face(ref n,ref playerEnvironment,false,confused);
                if(n.CollideX)n.Vx=-n.OldVx;if(n.Vx!=0)n.Direction=Math.Sign(n.Direction);
                bool clear;if(!t.CanHit(n.Bounds,target.Area,out clear,out stop))return false;
                float x,y,amount;
                if(clear){x=target.Area.CenterX-n.Bounds.CenterX;y=target.Area.CenterY-n.Bounds.CenterY;if(!Normalize(ref x,ref y,5,out stop))return false;amount=.05f;}
                else{float swim=n.Vy>0?3:n.Vy<0?8:5;x=n.Direction;y=-1;if(!Normalize(ref x,ref y,swim,out stop))return false;amount=swim<5?.04f:.1f;}
                n.Vx=n.Vx+(x-n.Vx)*amount;n.Vy=n.Vy+(y-n.Vy)*amount;return true;
            }
            bool attackAtEntry=(ranged || melee) && n.A0>0;
            skipStuck=attackAtEntry;bool locked=(ranged && n.A1>0) || melee && n.A0>0;
            if(melee)
            {
                if(n.A0<0){n.A0++;still=false;}
                if(n.A0==0 && n.Vy==0 && !target.Invalid)
                {
                    bool clear;if(!t.CanHit(n.Bounds,target.Area,out clear,out stop))return false;
                    if(!clear && !Line(t,e,n.Bounds,target.Area,out clear,out stop))return false;
                    if(clear && Distance(n.Bounds,target.Area)<meleeRange)n.A0=meleeClock;
                }
                if(n.A0>0){n.SpriteDirection=n.Direction;n.Vx*=meleeFriction;hold=noStuck=true;n.A3=0;n.A0--;if(n.A0==0)n.A0=-meleeCooldown;}
            }
            if(throwAttack && n.A0>0 && faceDuringAttack && !Target(ref n,e,t,elapsed,group,count,true,confused,out target,out stop))return false;
            if(jumpAttack && n.A0>0){if(n.Vy!=0 && n.A0<airClock)n.A0=airClock;if(n.A0==jumpClock)n.Vy=-jumpSpeed;}
            if(!skipStuck)
            {
                bool against=n.Vy==0 && n.Vx*n.Direction<0;
                if(n.X==n.OldX || n.A3>=30 || against)n.A3++;else if(Math.Abs(n.Vx)>.9f && n.A3>0)n.A3--;
                if(n.A3>300)n.A3=0;if(n.JustHit && type==620)n.A3=0;
                if(Intersects(target.Hitbox,Integer(n.Bounds)))n.A3=0;
                if(n.A3==30 && type!=620){n.NoGravity=n.NoTileCollide=true;n.X+=n.Direction*n.Width*2;n.NewSegment=true;return true;}
            }
            if(!noStuck)
            {
                if(n.A3<30 && chase){if(!Target(ref n,e,t,elapsed,group,count,true,confused,out target,out stop))return false;}
                else if(!locked)
                {
                    if(wanderDespawn)n.TimeLeft=Math.Min(n.TimeLeft,10);
                    if(n.Vx==0){if(n.Vy==0){n.A2++;if(n.A2>=2){n.Direction*=-1;n.SpriteDirection=n.Direction;n.A2=0;}}}
                    else if(n.A2!=0)n.A2=0;if(n.Direction==0)n.Direction=1;
                }
            }
            if(!hold)Accelerate(ref n,speed,acceleration,friction,true);
            if(ranged)
            {
                if(confused)n.A0=0;
                else
                {
                    if(n.A1>0)n.A1--;if(n.JustHit){n.A1=hitClock;n.A0=0;}
                    if(n.A0>0)
                    {
                        if(faceDuringAttack && !Target(ref n,e,t,elapsed,group,count,true,confused,out target,out stop))return false;
                        if(n.A1==shotClock){float dx=target.Area.CenterX-(n.Bounds.CenterX+offsetX),dy=target.Area.CenterY-(n.Bounds.CenterY+offsetY);dy-=Math.Abs(dx)*arc;AimPose(ref n,dx,dy,turnAtShot);}
                        if(n.Vy!=0 || n.A1<=0){n.A0=0;n.A1=n.A1<=0 && cooldown!=0?cooldown:0;}
                        else{n.Vx*=.9f;n.SpriteDirection=n.Direction;}
                    }
                    if(n.A0<=0 && n.Vy==0 && n.A1<=0 && !target.Invalid)
                    {
                        bool clear;if(!t.CanHit(n.Bounds,target.Area,out clear,out stop))return false;
                        if(clear && Distance(n.Bounds,target.Area)<rangedRange){n.Vx*=.5f;n.A1=rangedClock;AimPose(ref n,target.Area.CenterX-n.Bounds.CenterX,target.Area.CenterY-n.Bounds.CenterY,turnAtShot);}
                    }
                    if(n.A0<=0)Accelerate(ref n,speed,acceleration,friction,false);
                }
            }
            bool supported=false;
            if(n.Vy==0)
            {
                int y=(int)((n.Y+n.Height+7)/16),left=(int)(n.X/16),right=(int)((n.X+n.Width)/16);
                for(int x=left;x<=right;x++){PredictionTile cell;if(!t.Tile(Math.Max(0,Math.Min(e.WorldWidth,x)),Math.Max(0,Math.Min(e.WorldHeight,y)),out cell,out stop))return false;if(cell.Active && cell.Solid){supported=true;break;}}
            }
            int centerX=(int)(n.Bounds.CenterX/16),centerY=(int)(n.Bounds.CenterY/16);
            if(centerX>=5 && centerY>=5 && centerX<e.WorldWidth-5 && centerY<e.WorldHeight-5 && !n.NoGravity && !StepUp(ref n,e,t,out stop))return false;
            if(supported)
            {
                int x=(int)(n.Bounds.CenterX+front*n.Direction)/16,y=((int)(n.Y+n.Height)-15)/16;
                PredictionTile bottom,next,feet,one,two,three;
                if(!t.Tile(x+n.Direction,y+1,out next,out stop) || !t.Tile(x,y+1,out bottom,out stop) || !t.Tile(x,y,out feet,out stop) || !t.Tile(x,y-1,out one,out stop) || !t.Tile(x,y-2,out two,out stop) || !t.Tile(x,y-3,out three,out stop))return false;
                if(n.Vx*n.SpriteDirection>0)
                {
                    if(n.Height>=32 && two.Active && two.Solid)n.Vy=three.Active && three.Solid?-8:-7;
                    else if(one.Active && one.Solid)n.Vy=-6;
                    else if(n.Y+n.Height-y*16>20 && feet.Active && !feet.TopSlope && feet.Solid)n.Vy=-5;
                    else if(pitUp && (!bottom.Active || !bottom.Solid) && (!next.Active || !next.Solid)){n.Vx*=1.5f;n.Vy=-8;}
                    if(n.Vy==0 && still && n.A3==1)n.Vy=-5;
                }
            }
            if(phasing && n.NoTileCollide)
            {
                n.Wet=false;if(portal){if(n.Alpha<60)n.Alpha+=20;n.L3=40;}
                bool zero=n.Vy==0;
                if(Math.Abs(n.Bounds.CenterX-target.Area.CenterX)>200){n.SpriteDirection=n.Direction=target.Area.CenterX>n.Bounds.CenterX?1:-1;n.Vx=n.Vx+(n.Direction-n.Vx)*.05f;}
                if(n.Y+n.Height<target.Area.Y+target.Area.Height-16)n.Vy+=.5f;
                else
                {
                    bool solid;if(!t.Solid(new MotionRect(n.Bounds.CenterX-40,n.Y,80,n.Height),out solid,out stop))return false;
                    if(solid || target.Area.CenterY-n.Bounds.CenterY<-100 || target.Area.CenterY-n.Bounds.CenterY<10 && Math.Abs(target.Area.CenterX-n.Bounds.CenterX)<60)
                    {if(n.Vy>0)n.Vy=0;n.Vy-=n.Vy>-.2?.025f:.2f;if(n.Vy<-4)n.Vy=-4;}
                    else{if(n.Vy<0)n.Vy=0;n.Vy+=n.Vy<.1?.025f:.5f;}
                }
                if(n.Vy>10)n.Vy=10;if(zero)n.Vy=0;
            }
            return true;
        }
        private static bool Target(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int elapsed,NpcMotionState[] group,int count,bool face,bool confused,out NpcInvasionTarget target,out PredictionStop stop)
        {
            if(n.EffectiveType!=620)return NpcInvasionTargeting.TryTarget(ref n,e,t,elapsed,group,count,face,confused,out target,out stop);
            // Common targeting acquires a numbered player even when the old
            // GetTargetData read was an encoded NPC. Keep the old motor data
            // until this actual query, then release that tracking relation.
            n.TrackingKind=0;var player=NpcTargeting.Player(n,e);int old=n.DD2OldTarget;
            NpcTargeting.Retarget(ref n,ref player);
            if(face && NpcTargeting.CanFace(n,player,old))
            {
                // TargetClosest writes an integer targetRect. At the attack
                // exit its center can lie across the subpixel actor center;
                // using the player's float center reverses this acceleration.
                var area=NpcTargeting.Area(n,player);
                n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;
                n.DirectionY=(int)area.Y+(int)area.Height/2<n.Y+n.Height/2?-1:1;
            }
            if(confused)n.Direction=-n.Direction;
            if(n.Target>=0 && n.Target<255 && e.InvasionPlayers!=null)for(int i=0;i<e.InvasionPlayers.Count;i++)if(e.InvasionPlayers[i].Slot==n.Target){n.DD2TargetPlayerToken=e.InvasionPlayers[i].Token;n.DD2TargetIdentity=default(NpcIdentity);break;}
            return NpcInvasionTargeting.ReadTarget(n,e,elapsed,group,count,out target,out stop);
        }
        private static void Accelerate(ref NpcMotionState n,float speed,float acceleration,float friction,bool whole)
        {if(n.Vx<-speed || n.Vx>speed){if(n.Vy==0){n.Vx*=friction;if(whole)n.Vy*=friction;}}else if(n.Vx<speed && n.Direction==1 || n.Vx>-speed && n.Direction==-1)n.Vx=Clamp(n.Vx+acceleration*n.Direction,-speed,speed);}
        private static void AimPose(ref NpcMotionState n,float x,float y,bool turn)
        {if(x==0 && y==0){x=0;y=-1;}n.A0=Math.Abs(y)>Math.Abs(x)*2?y>0?1:5:Math.Abs(x)>Math.Abs(y)*2?3:y>0?2:4;if(turn)n.Direction=x>0?1:-1;}
        private static bool Normalize(ref float x,ref float y,float speed,out PredictionStop stop)
        {stop=PredictionStop.None;double squared=(double)x*x+(double)y*y;if(squared==0){stop=PredictionStop.InvalidState;return false;}float root=(float)Math.Sqrt(squared);double inverse=1d/root;x=(float)(x*inverse);y=(float)(y*inverse);x*=speed;y*=speed;return true;}
        private static MotionRect Integer(MotionRect a){return new MotionRect((int)a.X,(int)a.Y,(int)a.Width,(int)a.Height);}
        private static bool Intersects(MotionRect a,MotionRect b){return a.X<b.X+b.Width && a.X+a.Width>b.X && a.Y<b.Y+b.Height && a.Y+a.Height>b.Y;}
        // AI107 uses generic height-dependent Collision.StepUp, not AI3's
        // four-cell custom step. Ogre steps with its separate narrow box.
        private static bool StepUp(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out PredictionStop stop)
        {
            stop=PredictionStop.None;int width=n.Width;if(n.EffectiveType==576 || n.EffectiveType==577)width-=64;
            float px=n.X+(n.EffectiveType==576 || n.EffectiveType==577?32:0),vx=px+n.Vx;int direction=Math.Sign(n.Vx),x=(int)((vx+width/2+(width/2+1)*direction)/16),y=(int)((n.Y+n.Height-1)/16),height=(n.Height+15)/16;
            if(x<1 || y<1 || x>=e.WorldWidth-1 || y>=e.WorldHeight-40)return true;
            PredictionTile feet,head,top,behind;if(!t.Tile(x,y,out feet,out stop))return false;
            for(int i=1;i<height+2;i++){PredictionTile cell;if(y-i<0)return true;if(!t.Tile(x,y-i,out cell,out stop))return false;}
            if(x-direction<0 || x-direction>=e.WorldWidth || y-height<0)return true;
            if(!t.Tile(x-direction,y-height,out behind,out stop) || !t.Tile(x,y-1,out head,out stop) || !t.Tile(x,y-height-1,out top,out stop))return false;
            for(int i=2;i<height+1;i++){PredictionTile cell;if(!t.Tile(x,y-i,out cell,out stop))return false;if(!Pass(cell))return true;}
            if(!Pass(behind))return true;
            bool headPass=Pass(head) || head.Slope==1 && px+width/2>x*16 || head.Slope==2 && px+width/2<x*16+16 || head.Half && Pass(top);
            bool footStep=feet.Active && (!feet.TopSlope || feet.Slope==1 && px+width/2<x*16 || feet.Slope==2 && px+width/2>x*16+16) && (!feet.TopSlope || n.Y+n.Height>y*16) && feet.Solid && !feet.SolidTop || head.Half && head.Active;
            footStep&=!feet.SolidTop || !head.SolidTop;
            if(!headPass || !footStep || x*16>=vx+width || x*16+16<=vx)return true;
            float surface=y*16;if(head.Half)surface-=8;else if(feet.Half)surface+=8;
            if(surface<n.Y+n.Height && (double)(n.Y+n.Height-surface)<=16.1)n.Y=surface-n.Height;return true;
        }
        private static bool Pass(PredictionTile a){return !a.Active || !a.Solid || a.SolidTop;}
        private static bool Line(IPredictionTerrain t,PredictionEnvironment e,MotionRect a,MotionRect b,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;if(e.WorldWidth<3 || e.WorldHeight<=40){stop=PredictionStop.TerrainUnavailable;return false;}
            int x=Math.Max(1,Math.Min(e.WorldWidth-1,(int)((a.X+(int)a.Width/2)/16))),y=Math.Max(1,Math.Min(e.WorldHeight-40,(int)((a.Y+(int)a.Height/2)/16))),tx=Math.Max(1,Math.Min(e.WorldWidth-1,(int)((b.X+(int)b.Width/2)/16))),ty=Math.Max(1,Math.Min(e.WorldHeight-40,(int)((b.Y+(int)b.Height/2)/16)));
            int rx=Math.Abs(x-tx),ry=Math.Abs(y-ty),sx=Math.Sign(tx-x),sy=Math.Sign(ty-y),axis=y<ty?2:1;if(rx==0 && ry==0){clear=true;return true;}
            float stepX=1,stepY=1,carryX=0,carryY=0;if(rx==0)stepX=0;else if(ry>0 && rx>ry)stepX=(float)rx/ry;if(ry==0)stepY=0;else if(rx>0 && ry>=rx)stepY=(float)ry/rx;
            bool done=false;do
            {
                int run;if(axis==2){carryX+=stepX;run=(int)carryX;carryX-=run;}else{carryY+=stepY;run=(int)carryY;carryY-=run;}
                for(int i=0;i<run;i++)
                {
                    PredictionTile c,d,f;bool horizontal=axis==2;if(!t.Tile(x-(horizontal?0:1),y-(horizontal?1:0),out c,out stop) || !t.Tile(x,y,out d,out stop) || !t.Tile(x+(horizontal?0:1),y+(horizontal?1:0),out f,out stop))return false;
                    if(Block(c) || Block(d) || Block(f))return true;if(rx==0 && ry==0){done=true;break;}if(axis==2){x+=sx;rx--;}else{y+=sy;ry--;}if(rx==0 && ry==0 && run==1)done=true;
                }
                if(axis==2 && ry!=0)axis=1;else if(axis==1 && rx!=0)axis=2;PredictionTile center;if(!t.Tile(x,y,out center,out stop))return false;if(Block(center))return true;
            }while(!done);clear=true;return true;
        }
        private static bool Block(PredictionTile a){return a.Active && a.Solid && !a.SolidTop;}
    }
}
