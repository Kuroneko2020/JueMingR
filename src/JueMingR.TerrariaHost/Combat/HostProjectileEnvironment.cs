using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // A finite uniform-liquid continuation. Unsupported transitions, shimmer,
    // native water transformations and changing wind gates retire Contact;
    // absence of a solution never changes the actual projectile/environment.
    internal sealed class HostProjectileEnvironment
    {
        internal struct State
        {
            private readonly bool wind;private readonly float speed,strength;private readonly double surface;
            internal State(bool capture){wind=Main.windPhysics;speed=Main.windSpeedCurrent;strength=Main.windPhysicsStrength;surface=Main.worldSurface;}
            internal bool Current {get{return wind==Main.windPhysics && speed==Main.windSpeedCurrent && strength==Main.windPhysicsStrength && surface==Main.worldSurface;}}
        }
        private readonly Projectile sample;
        private readonly PredictionTerrain terrain;
        private readonly bool wind;
        private readonly float windSpeed,windStrength;
        private readonly double surface;
        internal readonly float Scale,WindX;
        private HostProjectileEnvironment(Projectile sample,PredictionTerrain terrain,float scale,float windX)
        {this.sample=sample;this.terrain=terrain;Scale=scale;WindX=windX;wind=Main.windPhysics;windSpeed=Main.windSpeedCurrent;windStrength=Main.windPhysicsStrength;surface=Main.worldSurface;}
        internal bool Unchanged {get{return wind==Main.windPhysics && windSpeed==Main.windSpeedCurrent && windStrength==Main.windPhysicsStrength && surface==Main.worldSurface;}}
        internal static AttackContact Solve(Projectile sample,PredictionTerrain terrain,Vector2 origin,AttackMotion motion,NpcTrajectory timeline,int age,int delay,int firstTick,Func<int,bool> receives,out HostProjectileEnvironment environment,Func<int,int,float,float,bool> damagePassage=null)
        {
            environment=null;float scale;if(!Factor(sample,terrain,origin.X,origin.Y,out scale))return null;
            // At most two uniform gate cases. Each actual candidate replay
            // checks the native gate before applying its chosen impulse; the
            // zero-velocity basis used for inversion is never a game query.
            float impulse=sample.ShouldUseWindPhysics()?Main.windSpeedCurrent*Main.windPhysicsStrength:0;
            for(int mode=0;mode<(impulse==0?1:2);mode++)
            {var candidate=new HostProjectileEnvironment(sample,terrain,scale,mode==0?0:impulse);var contact=AttackIntercept.Solve(origin.X,origin.Y,motion.WithEnvironment(scale,candidate.WindX),timeline,age,delay,candidate.Passage,firstTick,receives,damagePassage);if(contact!=null){environment=candidate;return contact;}}
            return null;
        }
        private static bool Factor(Projectile sample,PredictionTerrain terrain,float x,float y,out float scale)
        {
            scale=1;bool wet;byte kind;if(!terrain.ProjectileWet(x,y,sample.width,sample.height,out wet,out kind) || wet && kind==3 || sample.shimmerWet)return false;
            if(!wet || sample.ignoreWater || sample.type==242 || sample.type==302 || sample.type==638)return true;
            // These native AI/type changes require another phase model. Lava
            // and tileCollide=false also have distinct movement/exit rules.
            if(kind==1 || sample.type==2 || sample.type==34 || !sample.tileCollide)return false;
            scale=kind==2?.25f:.5f;return true;
        }
        private static bool Impulse(Projectile sample,PredictionTerrain terrain,float x,float y,float vx,out float impulse)
        {
            impulse=0;
            if(!sample.ShouldUseWindPhysics() || y>=Main.worldSurface*16 || Math.Abs(vx)>=16)return true;
            float delta=Main.windSpeedCurrent*Main.windPhysicsStrength;
            if(delta==0 || !(vx>0 && Main.windSpeedCurrent<0 || vx<0 && Main.windSpeedCurrent>0 || Math.Abs(vx)<Math.Abs(delta)*180))return true;
            PredictionTile tile;PredictionStop stop;if(!terrain.Tile((int)x/16,(int)y/16,out tile,out stop))return false;
            if(tile.Wall==0)impulse=delta;return true;
        }
        internal bool Passage(float x,float y,float nx,float ny,float width,float height)
        {
            float scale,impulse,rx,ry,vx=(nx-x)/Scale,vy=(ny-y)/Scale;
            return Unchanged && Factor(sample,terrain,x,y,out scale) && scale==Scale && Impulse(sample,terrain,x,y,vx-WindX,out impulse) && impulse==WindX &&
                terrain.ProjectileCollision(x,y,vx,vy,(int)width,(int)height,out rx,out ry) && Math.Abs(rx-vx)<.0001f && Math.Abs(ry-vy)<.0001f;
        }
        internal static bool Dry(Projectile shot,PredictionTerrain terrain,float x,float y,float vx)
        {
            float scale,impulse;return !(shot.type==34 && shot.wet && !shot.lavaWet) && Factor(shot,terrain,x,y,out scale) && scale==1 && Impulse(shot,terrain,x,y,vx,out impulse) && impulse==0;
        }
    }
}
