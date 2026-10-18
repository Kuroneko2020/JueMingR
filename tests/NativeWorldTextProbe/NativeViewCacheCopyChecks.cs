using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewCacheCopyChecks
    {
        private static SpriteBatch selected;
        private static object copyOwner;
        private static Exception injected;
        private static bool armed;
        private static int attempts;
        private static void Submit(SpriteBatch __instance)
        {if(armed&&ReferenceEquals(selected,__instance)&&!(bool)Get(copyOwner,"Rendering")&&(bool)Get(copyOwner,"Attached")){armed=false;attempts++;Console.WriteLine("CACHE COPY actual owned composite End, attached=True leaf=False exception="+injected.GetType().Name);throw injected;}}
        internal static void Run(Main game,object view,Action draw)
        {
            var cache=Get(view,"Cache");copyOwner=cache;var audit=new Harmony("JueMingR.Tests.View.CacheCopy");
            audit.Patch(AccessTools.Method(typeof(SpriteBatch),"End"),prefix:new HarmonyMethod(typeof(NativeViewCacheCopyChecks),nameof(Submit)));
            var lease=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount");
            try
            {
                foreach(bool oom in new[]{false,true})
                {
                    Call(cache,"Dispose");Main.GameZoomTarget=.5f;for(int i=0;i<3;i++)draw();
                    Console.WriteLine("CACHE COPY prepare oom="+oom+" route="+Get(Get(view,"Plan"),"Route")+" ready-sprites="+(cache.GetType().GetField("sprites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache)!=null)+" mode="+Lighting.Mode+" target="+Main.GameZoomTarget+" cache-generation="+Get(cache,"Generation")+" rejection="+cache.GetType().GetProperty("Rejection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache,null));
                    Require(Get(Get(view,"Plan"),"Route").ToString()=="TileCache","copy fault reaches actual admitted three-layer composite");
                    var sharedTiles=Main.tileBatch;var sharedSprites=Main.spriteBatch;ushort entry=(ushort)lease.GetValue(game.GraphicsDevice);
                    selected=(SpriteBatch)Get(cache,"sprites");injected=oom?(Exception)new OutOfMemoryException("controlled optional cached copy submission") : new IOException("controlled unknown cached copy submission");armed=true;attempts=0;bool exact=false;
                    try{draw();}catch(Exception error){while(error is TargetInvocationException)error=error.InnerException;exact=ReferenceEquals(error,injected);}
                    Require(exact&&attempts==1,"actual owned copy End failure survives native wrapper catch as the exact first exception");
                    Require(ReferenceEquals(Main.tileBatch,sharedTiles)&&ReferenceEquals(Main.spriteBatch,sharedSprites)&&(ushort)lease.GetValue(game.GraphicsDevice)==entry,"copy failure returns owned leases and closes only the native wrapper's exact reopened caller batch");
                    if(oom)
                    {
                        int count=Main.fpsCount;
                        for(int i=0;i<20;i++)draw();
                        Require(Main.fpsCount==count+20&&Main.GameZoomTarget==.5f&&Main.GameViewMatrix.RenderZoom.X==.5f&&Get(Get(view,"Plan"),"Route").ToString()=="Direct"&&(bool)Get(view,"ControlsEnabled"),"optional copy OOM keeps actual target/settings then20 correct direct Draw without retry or native guard short circuit");
                    }
                    else{Main.GameZoomTarget=1;int count=Main.fpsCount;for(int i=0;i<3;i++)draw();Require(Main.fpsCount==count+3,"three ordinary draws execute native DoDraw rather than returning from a stuck Draw guard");}
                    Require((ushort)lease.GetValue(game.GraphicsDevice)==entry,"subsequent real native Draw does not inherit a reopened world batch lease");
                    Console.WriteLine("PASS CACHE COPY original-submit "+injected.GetType().Name+" first exception retained, shared refs/XNA leases restored, subsequent native Draw="+(oom?20:3));
                }
            }
            finally{armed=false;selected=null;copyOwner=null;audit.UnpatchAll(audit.Id);}
        }
    }
}
