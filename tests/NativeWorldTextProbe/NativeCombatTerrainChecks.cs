using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTerrainChecks
    {
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);
            var nativeMove=typeof(NPC).GetMethod("UpdateCollision",BindingFlags.Instance|BindingFlags.NonPublic);
            var nativeGravity=typeof(NPC).GetField("gravity",BindingFlags.Static|BindingFlags.NonPublic);var savedGravity=nativeGravity.GetValue(null);nativeGravity.SetValue(null,.3f);
            var saved=Main.tile[60,60];Main.tile[60,60]=new Tile();Main.tileSolid[TileID.Stone]=true;
            int cases=0,contacts=0;
            try
            {
                for(int variant=0;variant<9;variant++)
                {
                    byte slope=(byte)(variant<5?variant:variant==8?1:0);var tile=Main.tile[60,60];tile.active(true);tile.type=(ushort)(variant>=6?TileID.Platforms:TileID.Stone);tile.slope(slope);tile.halfBrick(variant==5);tile.frameX=0;tile.frameY=(short)(variant==7?18:0);
                    foreach(float vx in new[]{-4f,0f,4f})foreach(float vy in new[]{-4f,.3f,4f})
                    for(int x=938;x<=980;x+=7)for(int y=920;y<=982;y+=7)
                    {
                        var n=new NPC();n.SetDefaults(3);n.active=true;n.whoAmI=199;n.position=new Vector2(x+.25f,y+.75f);n.velocity=new Vector2(vx,vy);n.width=20;n.height=32;n.directionY=-1;
                        var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                        Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"terrain should evaluate actual slope contact, not stop for a nearby tile: slope="+slope+" stop="+stop);
                        nativeMove.Invoke(n,null);
                        float error=Vector2.Distance(n.position,new Vector2(model.X,model.Y));
                        Require(error<.015f && Math.Abs(n.velocity.X-model.Vx)<.015f && Math.Abs(n.velocity.Y-model.Vy)<.015f && n.collideX==model.CollideX && n.collideY==model.CollideY,
                            "native terrain variant="+variant+" slope="+slope+" start="+x+","+y+" v="+vx+","+vy+" native="+n.position+" / "+n.velocity+" model="+model.X+","+model.Y+" / "+model.Vx+","+model.Vy);
                        if(n.collideX || n.collideY || n.position!=new Vector2(x+.25f+vx,y+.75f+vy))contacts++;cases++;
                    }
                }
                Require(contacts>0,"slope corpus really makes contact");Console.WriteLine("TERRAIN native UpdateCollision slope cases="+cases+" contacts="+contacts);
            }
            finally{Main.tile[60,60]=saved;nativeGravity.SetValue(null,savedGravity);}
        }
    }
}
