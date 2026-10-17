using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeCombatTerrainLineChecks
    {
        internal static void Run(object context)
        {
            NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3006,0,0);Main.tileSolid[1]=true;
            var type=Get(context,"Combat").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var method=type.GetMethod("CanHitLine",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            int productMismatch=0,cases=0;
            foreach(int layout in new[]{0,1,2,3})
            {
                for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                for(int at=5;at<78;at++)NativeToolsChecks.Tile(55,at,1);
                if(layout>0)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,24,1);
                if(layout==2)for(int y=5;y<78;y++)Main.tile[55,y].inActive(true);
                if(layout==3){Main.tileSolid[19]=Main.tileSolidTop[19]=true;for(int y=5;y<78;y++){Main.tile[55,y].type=19;}}
                var terrain=Activator.CreateInstance(type,true);Call(terrain,"Reset");
                foreach(var start in new[]{new Vector2(676,667),new Vector2(920.5f,234.1f),new Vector2(501.7f,880.3f)})
                for(int x=400;x<=1180;x+=13)for(int y=200;y<=950;y+=17)
                {
                    var end=new Vector2(x+.7096f,y+.6895f);bool native=Collision.CanHitLine(start,0,0,end,0,0);var a=new MotionRect(start.X,start.Y,0,0);var b=new MotionRect(end.X,end.Y,0,0);
                    var args=new object[]{a,b,false,PredictionStop.None};Require((bool)method.Invoke(terrain,args),"bounded isolated terrain line is available");
                    bool product=(bool)args[2];cases++;
                    if(product!=native){productMismatch++;if(productMismatch<4)Console.WriteLine("LINE product="+product+" native="+native+" from="+start+" to="+end);}
                }
            }
            Console.WriteLine("LINE cases="+cases+" productMismatch="+productMismatch);
            Require(productMismatch==0,"the private terrain line matches original x86 strip/last-run decisions in all bounded layouts");
        }
    }
}
