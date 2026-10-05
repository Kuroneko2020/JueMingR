using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMovementRegionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("Read",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var collision=typeof(NPC).GetMethod("UpdateCollision",Flags);var parameters=typeof(NPC).GetMethod("GetTileCollisionParameters",Flags);
            var tile=Main.tile[60,60];var saved=new Tile();saved.CopyFrom(tile);int cases=0;
            try
            {
                Main.tileSolid[1]=true;Main.LocalPlayer.position=new Vector2(800,900);
                foreach(int type in new[]{243,290,594,576,391,686})foreach(int variant in type==594?new[]{0,1,2}:type==391?new[]{0,1}:new[]{0})foreach(int shape in new[]{0,1,2})
                {
                    tile.active(true);tile.inActive(false);tile.type=1;tile.liquid=0;tile.halfBrick(false);tile.slope((byte)(shape==2?1:0));
                    var n=new NPC();n.SetDefaults(type);n.whoAmI=199;n.target=Main.myPlayer;n.ai[1]=variant*.5f;n.noGravity=true;n.noTileCollide=false;Set(n,"gravity",.3f);n.position=new Vector2(1000,1000);
                    var part=Main.npc[198];part.SetDefaults(390);part.active=type==391 && variant==1;part.ai[0]=199;
                    var args=new object[]{null,0,0};parameters.Invoke(n,args);var offset=(Vector2)args[0]-n.position;int w=(int)args[1],h=(int)args[2];
                    n.position=(shape==0?new Vector2(961,978):shape==1?new Vector2(938-w,960):new Vector2(955,959-h))-offset;n.velocity=shape==0?new Vector2(0,-8):shape==1?new Vector2(24,0):new Vector2(4,3);
                    var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"Movement-region snapshot: "+stop);collision.Invoke(n,null);
                    Console.WriteLine("REGION type="+type+" variant="+variant+" shape="+shape+" native="+n.position+" model="+new Vector2(model.X,model.Y)+" movement="+w+"x"+h+" body="+n.width+"x"+n.height);
                    Require(Vector2.Distance(n.position,new Vector2(model.X,model.Y))<.02f && Vector2.Distance(n.velocity,new Vector2(model.Vx,model.Vy))<.02f,"Original ceiling/wall/slope queries return movement-box offsets to the actual body.");part.active=false;cases++;
                }
                int ignored=0;
                for(int type=0;type<TileID.Sets.ForAdvancedCollision.ForSandshark.Length;type++)if(TileID.Sets.ForAdvancedCollision.ForSandshark[type])
                {
                    ignored++;tile.active(true);tile.inActive(false);tile.slope(0);tile.type=(ushort)type;Main.tileSolid[type]=true;
                    var n=new NPC();n.SetDefaults(542);n.target=Main.myPlayer;n.position=new Vector2(925,950);n.width=n.height=30;n.velocity=new Vector2(8,3);n.noGravity=true;var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                    Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"Sand filter snapshot.");collision.Invoke(n,null);Require(Vector2.Distance(n.position,new Vector2(model.X,model.Y))<.02f && Vector2.Distance(n.velocity,new Vector2(model.Vx,model.Vy))<.02f,"Complete original sand ignored collection: "+type);cases++;
                }
                Require(ignored==14,"Locked original sandshark collection contains fourteen entries.");
                foreach(int phase in new[]{0,6})foreach(int remaining in new[]{0,2})
                {
                    tile.active(true);tile.type=1;tile.slope(0);var n=new NPC();n.SetDefaults(417);n.target=Main.myPlayer;n.position=new Vector2(930,950);n.width=n.height=30;n.velocity=new Vector2(8,3);n.noGravity=true;n.ai[0]=phase;n.ai[2]=remaining;var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"Fully blocked cell contact.");collision.Invoke(n,null);
                    Require(Vector2.Distance(n.position,new Vector2(model.X,model.Y))<.02f && Vector2.Distance(n.velocity,new Vector2(model.Vx,model.Vy))<.02f && n.ai[2]==model.A2 && n.ai[3]==model.A3,"417 phase/contact count controls include exhausted and zero-axis contact.");cases++;
                }
            }
            finally{tile.CopyFrom(saved);Main.npc[198].active=false;}
            Console.WriteLine("PASS MOVEMENT REGIONS original actual consumers cases="+cases);
        }
    }
}
