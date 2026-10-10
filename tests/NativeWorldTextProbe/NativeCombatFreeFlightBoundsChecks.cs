using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFreeFlightBoundsChecks
    {
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");int cases=0;
            NativeCombatLiveContextChecks.FlightWorld();
            foreach(int liquid in new[]{-1,0,2})
            {
                for(int x=0;x<4;x++)for(int y=35;y<45;y++){Main.tile[x,y].ClearEverything();if(liquid>=0){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}}
                foreach(float x in new[]{-110f,-31f,-12f,1f,32f,Main.maxTilesX*16f-15,Main.maxTilesX*16f+20})foreach(bool client in new[]{false,true})
                {
                    terrain.Reset();var state=new NpcMotionState{X=x,Y=610,Width=32,Height=20,Vx=-3.25f,Vy=1.5f,NoTileCollide=true,Health=new NpcHealthState{Fire=60,Fire3=40}};
                    Vector2 position=new Vector2(state.X+state.Vx,state.Y+state.Vy);bool wet=Collision.WetCollision(position,state.Width,state.Height);bool honey=Collision.honey,shimmer=Collision.shimmer;PredictionStop stop;
                    Require(terrain.Move(ref state,new PredictionEnvironment{Multiplayer=client},out stop),"native free flight permits the original clipped edge query: "+x+"/"+liquid+" stop="+stop);
                    Require(state.X==position.X && state.Y==position.Y && state.OldX==x && state.OldY==610 && state.Health.Fire==(!client && wet?0:60) && state.Health.Fire3==(!client && wet?0:40),"free flight position and wet extinguish match original clipped query");
                    Require(Collision.honey==honey && Collision.shimmer==shimmer,"private prediction does not change native liquid scratch flags");cases++;
                }
            }
            var wetMethod=terrain.GetType().GetMethod("ProjectileWet",BindingFlags.Instance|BindingFlags.NonPublic);
            object[] args={8f,610f,10,10,false,(byte)0,false};Require(!(bool)wetMethod.Invoke(terrain,args),"projectile unknown border acquisition remains distinct from NPC free flight");
            Console.WriteLine("PASS native clipped noTileCollide world-edge movement/liquid cases="+cases+"; projectile strict-boundary negative retained");
        }
    }
}
