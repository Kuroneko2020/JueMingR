using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatLineChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var query=terrain.GetType().GetMethod("CanHitLine",Flags);var oldTiles=Main.tile;int oldWidth=Main.maxTilesX,oldHeight=Main.maxTilesY;
            int cases=0,positives=0,negatives=0;
            try
            {
                Main.maxTilesX=Main.maxTilesY=192;Main.tile=new Tile[192,192];for(int x=0;x<192;x++)for(int y=0;y<192;y++)Main.tile[x,y]=new Tile();
                var cell=Main.tile[96,96];
                foreach(int kind in new[]{0,1,2,3,4,5,6,7})
                {
                    cell.ClearEverything();cell.type=kind==6?TileID.Platforms:TileID.Stone;cell.active(kind!=0);cell.inActive(kind==7);cell.halfBrick(kind==1);cell.slope((byte)(kind>=2 && kind<=5?kind-1:0));
                    foreach(int size in new[]{0,1,21,80})
                    foreach(var endpoints in new[]{new[]{-300.25f,-.25f,1536.25f,1536.75f},new[]{1535.9999f,1503.9999f,1536.25f,1536.25f},new[]{80.25f,80.75f,2800.25f,2400.75f},new[]{80.25f,2400.75f,2800.25f,80.75f},new[]{80.25f,1536.75f,2800.25f,1536.75f},new[]{1536.25f,80.75f,1536.25f,2400.75f},new[]{3100.25f,200f,3100.25f,500f},new[]{1536.25f,1536.75f,1536.25f,1536.75f}})
                    foreach(bool reverse in new[]{false,true})
                    {
                        var a=new MotionRect(endpoints[reverse?2:0],endpoints[reverse?3:1],size,size+2);var b=new MotionRect(endpoints[reverse?0:2],endpoints[reverse?1:3],21,41);
                        bool actual=Collision.CanHitLine(new Vector2(a.X,a.Y),(int)a.Width,(int)a.Height,new Vector2(b.X,b.Y),(int)b.Width,(int)b.Height);
                        terrain.Reset();object[] args={a,b,false,PredictionStop.None};bool known=(bool)query.Invoke(terrain,args);
                        Require(known && (bool)args[2]==actual,"original CanHitLine floating center/strip/border: kind="+kind+" size="+size+" reverse="+reverse+" native="+actual+" model="+args[2]+" stop="+args[3]);
                        cases++;if(actual)positives++;else negatives++;
                    }
                }
                Require(positives>0 && negatives>0,"native line corpus includes real clear and blocked outcomes");cell.ClearEverything();
                // CanHit permits one solid neighbour. CanHitLine rejects it;
                // replacing the new query with the old predicate must fail.
                var edge=Main.tile[45,29];edge.active(true);edge.type=TileID.Stone;
                var from=new Vector2(600,480);var to=new Vector2(800,480);terrain.Reset();
                Require(Collision.CanHit(from,0,0,to,0,0) && !Collision.CanHitLine(from,0,0,to,0,0),"native CanHit and CanHitLine have different side-strip predicates");
                object[] side={new MotionRect(from.X,from.Y,0,0),new MotionRect(to.X,to.Y,0,0),false,PredictionStop.None};
                Require((bool)query.Invoke(terrain,side) && !(bool)side[2],"line query keeps the native single side blocker");edge.ClearEverything();
                // A long empty diagonal needs a narrow corridor of cells. The
                // old melee rectangle rejects it at64 tiles, and scanning its
                // bounding square would exceed the retained4096-cell limit.
                var longFrom=new MotionRect(80.25f,80.75f,14,14);var longTo=new MotionRect(2336.25f,2336.75f,21,41);terrain.Reset();
                var readCounter=terrain.GetType().GetProperty("Reads",Flags);int beforeReads=(int)readCounter.GetValue(terrain);
                object[] longer={longFrom,longTo,false,PredictionStop.None};Require((bool)query.Invoke(terrain,longer) && (bool)longer[2],"long native line remains usable with bounded corridor capture");
                int reads=(int)readCounter.GetValue(terrain)-beforeReads;Require(reads>140 && reads<1000,"long line captures touched corridor, not a rectangle: "+reads);
                Require(terrain.Unchanged,"fresh line lease is current");Main.tile[180,20].active(true);Main.tile[180,20].type=TileID.Stone;Require(terrain.Unchanged,"unread distant terrain does not retire a line lease");
                Main.tile[5,5].active(true);Main.tile[5,5].type=TileID.Stone;Require(!terrain.Unchanged,"native starting strip participates in terrain invalidation");Main.tile[5,5].ClearEverything();
                terrain.Reset();var saved=Main.tile[5,5];Main.tile[5,5]=null;
                try{longer=new object[]{longFrom,longTo,false,PredictionStop.None};Require(!(bool)query.Invoke(terrain,longer) && (PredictionStop)longer[3]==PredictionStop.TerrainUnavailable,"missing in-world line cell remains unknown, not physical blockage");}
                finally{Main.tile[5,5]=saved;}
                Console.WriteLine("PASS native CanHitLine cases="+cases+" clear="+positives+" blocked="+negatives+" longCorridorReads="+reads+" differentCanHit/lease/missing-cell");
            }
            finally{Main.tile=oldTiles;Main.maxTilesX=oldWidth;Main.maxTilesY=oldHeight;terrain.Reset();}
        }
    }
}
