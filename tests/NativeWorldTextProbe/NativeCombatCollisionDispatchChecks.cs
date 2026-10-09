using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCollisionDispatchChecks
    {
        internal static void Run(object context)
        {
            const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("Read",flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var original=typeof(NPC).GetMethod("UpdateCollision",flags);
            var tile=Main.tile[60,60];var saved=new Tile();saved.CopyFrom(tile);bool remix=Main.remixWorld;int cases=0;
            try
            {
                foreach(int type in new[]{72,542,543,544,545,417})foreach(int shape in new[]{0,1,2,3,4})foreach(bool alternate in new[]{false,true})
                {
                    tile.active(true);tile.inActive(false);tile.type=(ushort)(type>=542?(alternate?53:1):1);tile.slope((byte)shape);tile.halfBrick(false);Main.tileSolid[tile.type]=true;
                    var n=new NPC();n.SetDefaults(type);n.position=new Vector2(type==72?932:925,950);n.width=30;n.height=30;n.velocity=new Vector2(8,3);n.noGravity=true;n.noTileCollide=false;n.target=Main.myPlayer;n.ai[0]=alternate?6:0;n.ai[2]=2;
                    if(type==72 && alternate)tile.inActive(true);
                    Main.remixWorld=false;Collision.up=Collision.down=false;
                    var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();PredictionStop stop;Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"Dispatch shared snapshot: "+stop);original.Invoke(n,null);
                    Console.WriteLine("DISPATCH type="+type+" shape="+shape+" alternate="+alternate+" native="+n.position+" model="+new Vector2(model.X,model.Y)+" nativeV="+n.velocity+" modelV="+new Vector2(model.Vx,model.Vy));
                    Require(Vector2.Distance(n.position,new Vector2(model.X,model.Y))<.015f && Vector2.Distance(n.velocity,new Vector2(model.Vx,model.Vy))<.015f && n.ai[2]==model.A2 && n.ai[3]==model.A3 && n.direction==model.Direction,"Original full ApplyTileCollision dispatch / contact-owned phase: "+type);cases++;
                }
                foreach(int type in new[]{542,543,544,545})
                {
                    tile.type=1;tile.active(true);tile.inActive(false);tile.slope(0);Main.remixWorld=true;
                    var n=new NPC();n.SetDefaults(type);n.position=new Vector2(925,950);n.width=30;n.height=30;n.velocity=new Vector2(8,3);n.noGravity=true;n.target=Main.myPlayer;Collision.up=Collision.down=false;
                    var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();PredictionStop stop;Require(terrain.Move(ref model,new PredictionEnvironment{Remix=true},out stop),"Remix bypass available.");original.Invoke(n,null);
                    Require(Vector2.Distance(n.position,new Vector2(model.X,model.Y))<.015f,"Remix sandshark bypasses tile clipping.");cases++;
                }
            }
            finally{Main.remixWorld=remix;tile.CopyFrom(saved);}
            Console.WriteLine("PASS NPC COLLISION DISPATCH independent original cases="+cases);
        }
    }
}
