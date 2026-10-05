using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatConveyorChecks
    {
        internal static void Run(object context)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("Read",flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var original=typeof(NPC).GetMethod("UpdateCollision",flags);
            Main.tileSolid[421]=Main.tileSolid[422]=true;var tile=Main.tile[60,60];tile.active(true);tile.type=421;tile.slope(0);tile.halfBrick(false);
            var n=new NPC();n.SetDefaults(1);n.position=new Vector2(961,928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.noTileCollide=false;n.target=Main.myPlayer;
            var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();PredictionStop stop;Require(terrain.Move(ref state,default(PredictionEnvironment),out stop),"Conveyor shared terrain available: "+stop);original.Invoke(n,null);
            Console.WriteLine("CONVEYOR native="+n.position+" candidate="+new Vector2(state.X,state.Y));
            Require(Vector2.Distance(n.position,new Vector2(state.X,state.Y))<.015f && n.velocity==Vector2.Zero && state.Vx==0 && state.Vy==0,"Native body conveyor position contribution must survive zero velocity without changing velocity.");
            tile.active(false);Console.WriteLine("PASS CONVEYOR independent original body position.");
        }
    }
}
