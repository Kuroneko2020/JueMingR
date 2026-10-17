using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI016 is a finite launch -> motor/contact -> Kill/Damage sequence.
    // A tile-stopped rocket explodes at its stopped center on the next AI,
    // whereas grenades/mines retain their native reflection and fuse. Only
    // copied state is advanced; Kill, world conversion and RNG are never run.
    internal static class HostExplosiveAttack
    {
        internal static bool Handles(int type)
        {return type>=133 && type<=144 || type>=338 && type<=341 || type>=776 && type<=801 || type>=803 && type<=810 || type==30 || type==397 || type==517 || type==588 || type==681 || type==862 || type==863 || type==930 || type==1088;}
        internal static bool Homing(int type){return type>=338 && type<=341 || type>=803 && type<=810 || type==930;}
        private static bool Rocket(int type){return type==134 || type==137 || type==140 || type==143 || type==776 || type==780 || type==793 || type==796 || type==799 || type==784 || type==787 || type==790;}
        private static bool Grenade(int type){return type==133 || type==136 || type==139 || type==142 || type==777 || type==781 || type==794 || type==797 || type==800 || type==785 || type==788 || type==791;}
        private static bool Mine(int type){return type==135 || type==138 || type==141 || type==144 || type==778 || type==782 || type==795 || type==798 || type==801 || type==786 || type==789 || type==792;}
        private static bool Fluid(int type){return type>=784 && type<=792 || type>=799 && type<=801 || type>=805 && type<=807 || type==810;}
        internal static int ChildType(int type){return type==776 || type==777 || type==778?779:type==780 || type==781 || type==782?783:type==803?862:type==804?863:0;}
        internal static int BurstSide(int type,int original)
        {
            if(type==681)return 80;
            if(type==779 || type==783 || type==862 || type==863 || Fluid(type))return 48;
            if(type==793 || type==794 || type==795 || type==796 || type==797 || type==798 || type==808)return 250;
            // Locked PrepareBombToBlow repeats 808 and omits 809. Its visual
            // Resize(50) happens AFTER Damage, so 809 keeps its original body.
            if(type==809)return original;
            if(type>=139 && type<=144 || type==340 || type==341)return 200;
            return 128;
        }
        internal struct State
        {
            internal Vector2 Position,Velocity;private Vector2 half;
            internal float Clock;internal int Left;internal bool Alive,Wet,Honey,Burst;
            internal Vector2 Center {get{return Position+half;}}
            internal State(Projectile shot){Position=shot.position;Velocity=shot.velocity;half=shot.Size*.5f;Clock=shot.ai[0];Left=shot.timeLeft;Alive=shot.active;Wet=shot.wet;Honey=shot.honeyWet;Burst=false;}
            internal State(Projectile sample,Vector2 center,Vector2 velocity):this(sample)
            {
                Position=center-half;Velocity=velocity;Clock=0;Alive=true;Wet=Honey=false;
                if(sample.type==30 || sample.type==397 || sample.type==517 || sample.type==588 || sample.type==133 || sample.type==136 || sample.type==139 || sample.type==142)Left=180;
                if(sample.type==779 || sample.type==783 || sample.type==862 || sample.type==863)Left=60;
                if(sample.type==681)Left=600;
            }
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State state,Vector2? homingTarget)
        {
            state.Burst=false;if(!state.Alive)return true;
            Vector2 wetVelocity=state.Velocity;int type=sample.type;
            if(state.Wet && Fluid(type))state.Left=1;
            if(state.Left<=3 || type==1088 && state.Velocity.Length()<=.01f){state.Burst=type!=1088 && state.Clock!=1000;state.Alive=false;return true;}
            if(type==397)
            {
                for(int x=(int)(state.Position.X/16)-1;x<(int)((state.Position.X+sample.width)/16)+2;x++)
                for(int y=(int)(state.Position.Y/16)-1;y<(int)((state.Position.Y+sample.height)/16)+2;y++)
                {
                    PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.Active && tile.Solid && !tile.SolidTop && state.Position.X+sample.width-4>x*16 && state.Position.X+4<x*16+16 && state.Position.Y+sample.height-4>y*16 && state.Position.Y+4<y*16+16)state.Velocity=new Vector2(0,-.2f);
                }
            }
            if(Main.getGoodWorld && Main.noTrapsWorld && !Main.tenthAnniversaryWorld)state.Left--;
            if(Homing(type))
            {
                state.Clock++;if(state.Clock>30)state.Clock=30;
                // Native deliberately builds the no-target point through
                // position/half/velocity and then subtracts Center again.
                Vector2 point=homingTarget??(state.Position+sample.Size*.5f+state.Velocity*100);
                Vector2 desired=(point-state.Center).SafeNormalize(-Vector2.UnitY)*(type==930?12:16);
                state.Velocity=Vector2.Lerp(state.Velocity,desired,1f/12f);
            }
            else if(Rocket(type) && Math.Abs(state.Velocity.X)<15 && Math.Abs(state.Velocity.Y)<15)state.Velocity*=1.1f;
            state.Clock++;
            if(Mine(type))
            {
                state.Velocity.Y+=.2f;state.Velocity*=.97f;
                if((double)state.Velocity.X>-.1 && (double)state.Velocity.X<.1)state.Velocity.X=0;
                if((double)state.Velocity.Y>-.1 && (double)state.Velocity.Y<.1)state.Velocity.Y=0;
            }
            else if(Grenade(type))
            {if(state.Clock>15){if(state.Velocity.Y==0)state.Velocity.X*=.95f;state.Velocity.Y+=.2f;}}
            // 681 appears in the native ten-tick side of the OR but is
            // absent from the second side's exclusions: its actual onset is 6.
            else if(!Rocket(type) && !Homing(type) && state.Clock>(type==681?5:10))
            {
                state.Clock=10;
                if(state.Velocity.Y==0 && state.Velocity.X!=0){state.Velocity.X*=.97f;if(state.Velocity.X>-.01f && state.Velocity.X<.01f)state.Velocity.X=0;}
                state.Velocity.Y+=.2f;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out wind))return false;state.Velocity.X+=wind;
            if(!sample.ignoreWater)
            {
                bool wet;byte kind;if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3)return false;
                state.Wet=wet;if(wet && kind==2)state.Honey=true;if(!wet)state.Honey=false;
            }
            if(sample.tileCollide)
            {
                Vector2 previous=state.Velocity;bool hit;
                if(!terrain.ProjectileTileMotion(sample,ref state.Position,ref state.Velocity,state.Wet,state.Honey,out wetVelocity,out hit))return false;
                if(hit)
                {
                    if(type==1088){bool plants;if(!terrain.AcornPlant(state.Center,out plants))return false;if(plants){state.Alive=false;return true;}}
                    if(type==517)
                    {if(state.Velocity.X!=previous.X && Math.Abs(previous.X)>1)state.Velocity.X=previous.X*-.9f;if(state.Velocity.Y!=previous.Y && Math.Abs(previous.Y)>1)state.Velocity.Y=previous.Y*-.9f;}
                    else if(type==681)state.Left=4;
                    else
                    {
                        if(state.Velocity.X!=previous.X)state.Velocity.X=previous.X*-.4f;
                        if(state.Velocity.Y!=previous.Y && (double)previous.Y>.7)state.Velocity.Y=previous.Y*-.4f;
                        if(Rocket(type) || Homing(type)){state.Velocity*=0;state.Left=3;}
                    }
                }
            }
            state.Position+=state.Wet?wetVelocity:state.Velocity;state.Left--;
            return true;
        }
        internal readonly struct Candidate
        {
            internal readonly int Slot;internal readonly MotionRect Bounds;
            internal Candidate(int slot,MotionRect bounds){Slot=slot;Bounds=bounds;}
        }
        internal static Candidate[] CaptureTargets(Projectile sample,HostAttackObstacles obstacles)
        {
            if(!Homing(sample.type))return Array.Empty<Candidate>();
            var targets=obstacles.CaptureChaseTargets(sample);var rows=new Candidate[targets.Count];
            for(int i=0;i<rows.Length;i++)rows[i]=new Candidate(targets.Slot(i),targets.Bounds(i));return rows;
        }
        internal static bool Target(Projectile sample,State state,NpcTrajectory timeline,int tick,PredictionTerrain terrain,Candidate[] candidates,out Vector2? point)
        {
            point=null;if(!Homing(sample.type) || state.Clock+1<=30)return true;
            float nearest=sample.type==930?650:600;
            foreach(var candidate in candidates)
            {
                var b=candidate.Slot==timeline.Identity.Slot?timeline[tick].Bounds:candidate.Bounds;
                float x=b.X+(int)b.Width/2,y=b.Y+(int)b.Height/2,distance=Math.Abs(state.Center.X-x)+Math.Abs(state.Center.Y-y);
                if(distance>=nearest)continue;bool clear;PredictionStop stop;
                if(!terrain.CanHit(new MotionRect(state.Position.X,state.Position.Y,sample.width,sample.height),b,out clear,out stop))return false;
                if(clear){nearest=distance;point=new Vector2(x,y);}
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            var candidates=CaptureTargets(sample,obstacles);
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            AttackMotion basic;var proposals=HostAttackModels.TryRead(ammo,out basic)?HostMotionAttackInputs.Generate(origin,basic,timeline,age):new Vector2[0];
            var directions=new Vector2[proposals.Length+41];int count=0;
            for(int input=0;input<directions.Length;input++)
            {
                int seed=input-proposals.Length;
                Vector2 unit;if(seed<0)unit=proposals[input];
                else if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}
                else unit=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==unit){duplicate=true;break;}if(duplicate)continue;directions[count++]=unit;
                var state=new State(sample,origin,unit*ammo.Speed);Vector2 aim=origin+unit*10000;obstacles.BeginCandidate();int k=0;
                if(HostClusterAttack.Handles(sample.type))
                {
                    var contact=HostClusterAttack.Solve(player,sample,timeline,terrain,obstacles,origin,unit*ammo.Speed,aim,age,delay,candidates);
                    if(contact!=null)return contact;continue;
                }
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3)return null;state.Wet=!sample.ignoreWater && wet;state.Honey=state.Wet && kind==2;
                for(int outer=1;outer<=limit && state.Alive;outer++)for(int sub=0;sub<=sample.extraUpdates && state.Alive;sub++)
                {
                    int tick=age+outer;Vector2? point;
                    if(!Target(sample,state,timeline,tick,terrain,candidates,out point) || !Step(sample,terrain,ref state,point)){state.Alive=false;break;}
                    if(!state.Alive && !state.Burst)break;
                    int side=state.Burst?BurstSide(sample.type,sample.width):sample.width;
                    int hits=obstacles.OtherHits;k++;
                    if(!obstacles.PassShapeOuter(k,tick,delay+outer,state.Center.X,state.Center.Y,side,state.Burst?side:sample.height)){state.Alive=false;break;}
                    if(receive.Allows(delay+outer))
                    {var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.Center.X,state.Center.Y,side,state.Burst?side:sample.height,Homing(sample.type)?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return contact;}
                    if(obstacles.OtherHits>hits){if(sample.type==1088)state.Alive=false;else if(state.Left>3)state.Left=3;}
                }
            }
            return null;
        }
    }
}
