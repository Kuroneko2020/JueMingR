using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI010's ordinary released ammunition. It shares only finite hand
    // candidate/receive orchestration with AI002; its native movement bypasses
    // the ordinary subdivided projectile/slopes branch and caps Y at ten.
    internal static class HostSeedThrowAttack
    {
        internal static bool Handles(int type){return type==42 || type==65 || type==68 || type==354 || type==1081;}
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref HostFiniteThrowAttack.State state)
        {
            if(!state.Alive || state.Left<=0){state.Alive=false;return true;}
            var half=sample.Size*.5f;var position=state.Center-half;
            if(position.X<=Main.leftWorld || position.X+sample.width>=Main.rightWorld || position.Y<=Main.topWorld || position.Y+sample.height>=Main.bottomWorld)
            {state.Alive=false;return true;}
            // Native AI can destroy special tiles or flatten a top slope
            // before collision. Do not silently treat that mutation as an
            // unchanged world, and never execute it against the live tiles.
            for(int x=(int)(position.X/16);x<=(int)((position.X+sample.width)/16);x++)
                for(int y=(int)(position.Y/16);y<=(int)((position.Y+sample.height)/16);y++)
                {
                    if(!WorldGen.InWorld(x,y))continue;
                    PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.RawActive && (tile.Type>=185 && tile.Type<=187 || tile.Type==165 || tile.Type==12 || tile.Type==665 || tile.Type==639 || tile.Type==105 || tile.Type==178 || tile.TopSlope && !tile.Platform))return false;
                }
            var wetVelocity=state.Velocity;
            if(sample.type==1081)
            {
                if(state.Counter==0)state.Counter=1;state.Counter++;
                if(state.Counter>=20){state.Velocity.Y+=.3f;state.Velocity.X*=.98f;}
            }
            else {state.Counter++;if(state.Counter>=60)state.Velocity.Y+=.2f;}
            if(state.Velocity.Y>10)state.Velocity.Y=10;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out wind))return false;state.Velocity.X+=wind;
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind) || kind==3 && wet || !terrain.ProjectileLava(state.Center.X,state.Center.Y,sample.width,sample.height,out lava))return false;
                state.Wet=wet;if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;if(!wet)state.Honey=state.Lava=false;
            }
            // AI010 enters native TileCollision directly, even while wet.
            // wetVelocity remains the pre-AI value; the ordinary wet .5/.25
            // branch and SlopeCollision must not be substituted here.
            float vx,vy;var prior=state.Velocity;
            if(!terrain.ProjectileCollision(state.Center.X,state.Center.Y,prior.X,prior.Y,sample.width,sample.height,out vx,out vy))return false;
            state.Velocity=new Vector2(vx,vy);
            if(state.Velocity!=prior)
            {
                if(sample.type!=1081 || prior.Y>0 && prior.Y!=vy){state.Alive=false;return true;}
                if(state.Velocity.Y<0)state.Velocity.Y=0;
            }
            state.Center+=state.Wet?wetVelocity:state.Velocity;state.Left--;
            return true;
        }
    }
}
