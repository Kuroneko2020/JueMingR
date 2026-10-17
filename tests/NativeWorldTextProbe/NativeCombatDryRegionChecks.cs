using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Geometry-only counterexamples compare the locked original wet query.
    // They never replace native AI, health, motion or a receive denominator.
    internal static class NativeCombatDryRegionChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static bool Wet(object terrain,float x)
        {
            var body=new NpcMotionState{X=x,Y=400.125f,Width=20,Height=20};
            var args=new object[]{body,false,false,(byte)0,PredictionStop.None,false};
            Require((bool)terrain.GetType().GetMethod("Wet",Flags).Invoke(terrain,args),"wet query acquires its normal complete terrain premise");
            bool observed=(bool)args[2];
            Require(observed==Collision.WetCollision(new Vector2(body.X,body.Y),body.Width,body.Height),"dry region query equals original sensor at x="+x);
            return observed;
        }
        private static void Empty()
        {for(int x=24;x<=28;x++)for(int y=23;y<=28;y++)Main.tile[x,y]=new Tile();}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");
            var terrain=Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);
            var saved=new List<Tuple<int,int,Tile>>();
            for(int x=24;x<=28;x++)for(int y=23;y<=28;y++)saved.Add(Tuple.Create(x,y,Main.tile[x,y]));
            try
            {
                Empty();Call(terrain,"Reset");
                Require(!Wet(terrain,400.125f) && !Wet(terrain,407.125f),"float movement within the same fully dry rectangle remains dry");
                // Water is in the scan envelope but outside the first sensor.
                // The second sensor crosses it without changing that envelope.
                Empty();Main.tile[26,25].liquid=255;Call(terrain,"Reset");
                Require(!Wet(terrain,400.125f) && Wet(terrain,407.125f),"a sensor miss cannot become an all-region dry proof");
                Empty();Main.tile[26,24].liquid=255;NativeToolsChecks.Tile(26,25,1);Main.tile[26,25].slope(1);Call(terrain,"Reset");
                Require(!Wet(terrain,400.125f) && Wet(terrain,407.125f),"active slope borrowing liquid above retains the original wet result");
                Empty();Call(terrain,"Reset");Require(!Wet(terrain,407.125f),"changed-cell fixture first acquires a complete dry lease");
                Main.tile[26,25].liquid=255;
                Require(!(bool)Get(terrain,"Unchanged"),"actual changed liquid denies the old full-cell lease");
                Call(terrain,"Reset");Require(Wet(terrain,407.125f),"a new real terrain epoch cannot reuse the old dry proof");
                Empty();Call(terrain,"Reset");Require(!Wet(terrain,400.125f),"ice fixture starts with a valid cached dry region");
                Call(terrain,"BeginIcePathReadCapture");Require(!Wet(terrain,407.125f),"ice path still executes the dry query");
                var reads=Call(terrain,"EndIcePathReadCapture");
                Require(!(bool)Call(reads,"Unaffected",new[]{new Point(25,25)}),"ice read tracking records cells even after a prior dry proof");
                Require((bool)Call(terrain,"BeginIcePlayerReadGuard",new[]{new Point(25,25)}),"private ice guard is admitted");
                var args=new object[]{new NpcMotionState{X=407.125f,Y=400.125f,Width=20,Height=20},false,false,(byte)0,PredictionStop.None,false};
                Require(!(bool)terrain.GetType().GetMethod("Wet",Flags).Invoke(terrain,args) && (PredictionStop)args[4]==PredictionStop.TerrainUnavailable,"dry proof cannot bypass an actual ice player guard");
                Call(terrain,"EndIcePlayerReadGuard");
                Console.WriteLine("PASS dry region: same rectangle float motion / sensor miss then water / slope above / actual tile change and reset / ice reads and guard; original WetCollision oracle");
            }
            finally{foreach(var tile in saved)Main.tile[tile.Item1,tile.Item2]=tile.Item3;Call(terrain,"Reset");}
        }
    }
}
