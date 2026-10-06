using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFoundationLiquidChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object source)
        {
            var read=source.GetType().GetMethod("Read",Flags);var collision=typeof(NPC).GetMethod("UpdateCollision",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");bool server=Main.dedServ;int network=Main.netMode;int cases=0;
            Main.dedServ=true;Main.netMode=1;
            try
            {
                foreach(int family in new[]{1,690,72,376,579,541,21,67,7,116})foreach(float phase in new[]{0f,25f})
                foreach(int liquid in new[]{-1,0,1,2,3})foreach(bool oldWet in new[]{false,true})foreach(bool flags in new[]{false,true})
                {
                    for(int x=23;x<30;x++)for(int y=23;y<30;y++){var tile=Main.tile[x,y];tile.active(false);tile.liquid=(byte)(liquid<0?0:255);tile.liquidType(liquid<0?0:liquid);}
                    var n=new NPC();n.SetDefaults(1);n.type=family;n.whoAmI=199;n.position=new Vector2(400,400);n.width=20;n.height=40;n.velocity=new Vector2(2,1);n.aiStyle=family==21 || family==67 || family==7 || family==116?family:99;n.ai[0]=phase;n.wet=oldWet;n.honeyWet=n.shimmerWet=n.lavaWet=flags;n.wetCount=1;n.lavaImmune=true;n.buffImmune[353]=true;n.noGravity=true;
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();PredictionStop stop;
                    collision.Invoke(n,null);Require(terrain.Move(ref state,new PredictionEnvironment{Multiplayer=true,PlayerIndex=-1},out stop),"C real geometry accepts narrow fluid case "+family+" stop="+stop);
                    Require(state.Wet==n.wet && state.Honey==n.honeyWet && state.Shimmer==n.shimmerWet && state.Lava==n.lavaWet && Math.Abs(state.Vx-n.velocity.X)<.00001 && Math.Abs(state.Vy-n.velocity.Y)<.00001,"C original full liquid eligibility/flags/exit family="+family+" phase="+phase+" liquid="+liquid+" oldWet="+oldWet+" flags="+flags+" expected="+n.wet+"/"+n.honeyWet+"/"+n.shimmerWet+"/"+n.lavaWet+"/"+n.velocity+" actual="+state.Wet+"/"+state.Honey+"/"+state.Shimmer+"/"+state.Lava+"/"+state.Vx+"/"+state.Vy);cases++;
                }
                // GetTargetData is independently exercised for player, encoded
                // NPC and invalid data. The encoded NPC case stops at this
                // water method: native620's separate platform method directly
                // indexes Main.player[target] and cannot accept target300+.
                var water=typeof(NPC).GetMethod("Collision_WaterCollision",Flags);
                var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);
                foreach(int kind in new[]{0,1,3})foreach(bool above in new[]{false,true})
                {
                    for(int x=23;x<30;x++)for(int y=23;y<30;y++)Main.tile[x,y].liquid=0;
                    var p=Main.player[1];p.active=kind==1;p.dead=false;p.ghost=false;p.position=new Vector2(400,above?200:600);
                    var other=Main.npc[198];other.active=kind==3;other.life=100;other.position=new Vector2(400,above?200:600);other.velocity=Vector2.Zero;
                    var n=new NPC();n.SetDefaults(620);n.whoAmI=199;n.position=new Vector2(400,400);n.width=20;n.height=40;n.velocity=new Vector2(2,1);n.target=kind==3?498:kind==1?1:255;n.wet=true;n.wetCount=1;
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});var args=new object[]{n,state,terrain};capture.Invoke(null,args);state=(NpcMotionState)args[1];
                    var e=new PredictionEnvironment{Multiplayer=true,PlayerIndex=kind==1?1:-1,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height};
                    water.Invoke(n,new object[]{false});
                    // Target data is already captured; use target=-1 ONLY for
                    // the local geometry comparison to avoid calling native's
                    // unrelated illegal numbered-player platform consumer.
                    state.Target=-1;terrain.Reset();PredictionStop stop;Require(terrain.Move(ref state,e,out stop),"C620 finite water exit accepted: "+stop);
                    Require(state.Vx==n.velocity.X && state.Vy==n.velocity.Y && !state.Wet,"C620 true target/default exit impulse kind="+kind+" above="+above+" expected="+n.velocity+" actual="+state.Vx+"/"+state.Vy);cases++;
                    p.active=false;other.active=false;
                }
                Console.WriteLine("PASS C locked full liquid collision families/old flags and620 true target water consumer cases="+cases);
            }
            finally{Main.dedServ=server;Main.netMode=network;for(int x=23;x<30;x++)for(int y=23;y<30;y++)Main.tile[x,y].liquid=0;}
        }
    }
}
