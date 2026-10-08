using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCloseoutTerrainChecks
    {
        internal static void Run(object context)
        {
            var terrain=(IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");
            var saved=Main.tile;int width=Main.maxTilesX,height=Main.maxTilesY,mode=Main.netMode,cases=0;Main.netMode=0;
            try
            {
                foreach(var coordinate in new[]{new Point(60,60),new Point(7000,60),new Point(60,2200)})
                {
                    Main.maxTilesX=Math.Max(128,coordinate.X+20);Main.maxTilesY=Math.Max(128,coordinate.Y+60);Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
                    for(int x=coordinate.X-5;x<coordinate.X+6;x++)for(int y=coordinate.Y-5;y<coordinate.Y+6;y++)Main.tile[x,y]=new Tile();
                    Main.tileSolid[TileID.Stone]=true;Main.tileSolid[TileID.Platforms]=true;Main.tileSolidTop[TileID.Platforms]=true;
                    foreach(int variant in new[]{0,1,2})foreach(bool vertical in new[]{false,true})foreach(float overlap in new[]{-.125f,0,.125f})
                    {
                        var tile=Main.tile[coordinate.X,coordinate.Y];tile.ClearEverything();tile.active(true);tile.type=(ushort)(variant==2?TileID.Platforms:TileID.Stone);tile.halfBrick(variant==1);
                        var box=vertical?new MotionRect(coordinate.X*16+2,coordinate.Y*16-16+overlap,12,16):new MotionRect(coordinate.X*16-16+overlap,coordinate.Y*16+2,16,12);
                        terrain.Reset();PredictionStop stop;bool actual;
                        Require(terrain.Solid(box,out actual,out stop),"Matched-runtime solid sample available: "+coordinate+" stop="+stop);
                        bool expected=Collision.SolidCollision(new Vector2(box.X,box.Y),(int)box.Width,(int)box.Height);
                        Console.WriteLine("SOLID x="+box.X+" y="+box.Y+" variant="+variant+" overlap="+overlap+" original="+expected+" candidate="+actual);
                        Require(actual==expected,"Matched .8 x86 SolidCollision boundary mismatch at "+coordinate+" variant="+variant+" vertical="+vertical+" overlap="+overlap);cases++;
                    }
                }
            }
            finally{Main.tile=saved;Main.maxTilesX=width;Main.maxTilesY=height;Main.netMode=mode;terrain.Reset();}
            Console.WriteLine("PASS SOLID matched .8 CLR/x86 direct predicate, normal/high coordinate, separation/contact/overlap, horizontal/vertical, half/platform: "+cases);
        }
    }
}
