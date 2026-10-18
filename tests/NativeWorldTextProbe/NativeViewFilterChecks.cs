using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewFilterChecks
    {
        private const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Filter witness;
        private static int applied;
        private static void Apply(Filter __instance) { if(ReferenceEquals(__instance,witness))applied++; }
        internal static void Run(Main game,object view,Action draw,Func<RenderTarget2D> final,Action<int,int> resize,string output,bool cacheCheck=false)
        {
            var audit=new Harmony("JueMingR.Tests.View.Filters");var filters=Filters.Scene;
            witness=filters["testInvert"];Require(witness!=null,"original initialized invert scene filter");
            audit.Patch(typeof(Filter).GetMethod("Apply"),prefix:new HarmonyMethod(typeof(NativeViewFilterChecks),nameof(Apply)));
            try
            {
                Lighting.Mode=LightMode.Color;
                foreach(string phase in new[]{"ordinary","wide","retained"})
                {
                    bool wide=phase!="ordinary";
                    Call(view,"SetUnlockEnabled",phase=="wide");Main.GameZoomTarget=wide?.5f:1f;
                    filters.Deactivate("testInvert");filters.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(2)));
                    for(int i=0;i<6;i++)draw();
                    var before=Pixels(final());
                    filters.Activate("testInvert",Main.LocalPlayer.Center);witness.Opacity=1;filters.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(2)));
                    int start=applied;for(int i=0;i<6;i++)draw();
                    var scene=Get(view,"Scene");int generation=(int)Get(scene,"Generation");
                    for(int i=0;i<12;i++)draw();
                    Require((int)Get(scene,"Generation")==generation,"stable scene filter reuses the physical buffers");
                    var after=Pixels(final());int left=Changed(before,after,32,700),right=Changed(before,after,Main.screenWidth-48,700),ui=Changed(before,after,35,35);
                    Save(game.GraphicsDevice,after,output,"view-filter-"+phase);
                    Console.WriteLine("VIEW FILTER phase="+phase+" target="+Main.GameZoomTarget+" render="+Main.GameViewMatrix.RenderZoom+" applies="+(applied-start)+" leftChanged="+left+" rightChanged="+right+" UIchanged="+ui+" generation="+generation+" physical="+Main.screenWidth+"x"+Main.screenHeight);
                    Require(applied-start>=18 && left>100 && right>100,"original filter Apply changes final pixels at both physical edges");
                    Require(ui==0,"native UI remains outside world scene filter");
                }
                foreach(var size in new[]{new Point(1280,720),new Point(1920,1080)})
                {
                    int before=(int)Get(Get(view,"Scene"),"Generation"),cacheBefore=cacheCheck?(int)Get(Get(view,"Cache"),"Generation"):0;resize(size.X,size.Y);for(int i=0;i<6;i++)draw();
                    Require((int)Get(Get(view,"Scene"),"Generation")==before+1,"native device/size change replaces scene buffers once");
                    Console.WriteLine("VIEW FILTER resize="+Main.screenWidth+"x"+Main.screenHeight+" generation="+Get(Get(view,"Scene"),"Generation"));
                    if(cacheCheck){Require((int)Get(Get(view,"Cache"),"Generation")==cacheBefore+1,"actual native resize/reset invalidates tile cache and admits one complete replacement group");Console.WriteLine("CACHE RESIZE physical="+Main.screenWidth+"x"+Main.screenHeight+" generation="+cacheBefore+"->"+Get(Get(view,"Cache"),"Generation"));}
                }
                Console.WriteLine("PASS VIEW original active filter ordinary/wide/Retained Apply and physical-edge pixels, UI exclusion, stable reuse and actual native device resize.");
            }
            finally {filters.Deactivate("testInvert");filters.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(2)));audit.UnpatchAll(audit.Id);witness=null;}
        }
        private static Color[] Pixels(RenderTarget2D target){var value=new Color[target.Width*target.Height];target.GetData(value);return value;}
        private static int Changed(Color[] a,Color[] b,int left,int top)
        {int n=0;for(int y=top;y<top+16;y++)for(int x=left;x<left+16;x++)if(a[y*Main.screenWidth+x]!=b[y*Main.screenWidth+x])n++;return n;}
        private static void Save(GraphicsDevice device,Color[] pixels,string output,string name)
        {using(var image=new Texture2D(device,Main.screenWidth,Main.screenHeight)){image.SetData(pixels);using(var file=File.Create(Path.Combine(output,name+".png")))image.SaveAsPng(file,Main.screenWidth,Main.screenHeight);}}
    }
}
