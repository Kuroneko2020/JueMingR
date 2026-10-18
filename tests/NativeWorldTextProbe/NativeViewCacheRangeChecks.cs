using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.Graphics;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite owned-scene comparison through the existing initialized native
    // Main. The same probe can load the preserved3ea host or current candidate;
    // no owner process/save or whole-game input incident reproduction occurs.
    internal static class NativeViewCacheRangeChecks
    {
        internal static void Run(Main game, object view, Action draw, Action cave, Func<RenderTarget2D> final, string output,Action input,Action<int,int> resize)
        {
            Console.WriteLine("CACHE-RANGE native profile="+game.GraphicsDevice.GraphicsProfile+" physical="+Main.screenWidth+"x"+Main.screenHeight+" wideScreen="+Main.SupportWideScreen);
            cave();Lighting.Mode=LightMode.Color;Call(view,"SetUnlockEnabled",true);
            bool baseline=Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_BASELINE")=="1";
            string repair=Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_FOCUS");
            if(repair=="RepairCaller"){NativeViewCacheRepairChecks.Caller(game,view,draw);NativeViewCacheCopyChecks.Run(game,view,draw);return;}
            if(repair=="RepairDirty"){NativeViewCacheRepairChecks.Dirty(game,view,draw,output);NativeViewCacheRepairChecks.Motion(view,draw,output);return;}
            if(repair=="RepairExit"){NativeViewCacheRepairChecks.Exit(game,view,draw,resize);return;}
            if(Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_FOCUS")=="ForcedOld")
            {NativeViewForcedViewportChecks.Run(game,view,draw,final,resize,output);return;}
            if(Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_PROFILE")=="HiDef")
            {Profile(game,view,draw,final,resize,output);return;}
            string focus=Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_FOCUS");
            if(focus=="Lifecycle"){NativeViewCacheLifecycleChecks.Run(game,view,draw);NativeViewCacheCopyChecks.Run(game,view,draw);return;}
            if(focus=="Copy"){NativeViewCacheCopyChecks.Run(game,view,draw);return;}
            if(focus=="Costs"){NativeViewCacheCostChecks.Run(view,draw,output);return;}
            if(focus=="Content"){Content(game,view,draw,output);return;}
            if(focus=="Modes"){Modes(game,view,draw,final,output);return;}
            if(focus=="Water"){NativeViewWaterRouteChecks.Run(game,view,draw,final,output);NativeViewFilterChecks.Run(game,view,draw,final,resize,output,true);return;}
            var assembly=view.GetType().Assembly;
            if(!baseline)Set(Get(view,"Cache"),"ForceDirect",true);
            if(!baseline)Range(assembly);
            if(!baseline){input();NativeViewRangeOptionsChecks.Run(game,view);}
            foreach(float target in baseline?new[]{.5f}:new[]{.5f,.3f,10f})
            {
                Main.GameZoomTarget=target;Main.renderNow=true;
                for(int i=0;i<12;i++)draw();
                float actual=Main.GameViewMatrix.RenderZoom.X;
                Console.WriteLine("CACHE-RANGE actual target="+Main.GameZoomTarget+" zoom="+actual+" world="+Main.Camera.ScaledSize);
                Require(Math.Abs(actual-(float)Math.Round(target*128f)/128f)<.00001f,"native final matrix publishes selected low/high target");
                using(var file=File.Create(Path.Combine(output,"range-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                long allocations=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,managed=GC.GetTotalMemory(false),memory=Process.GetCurrentProcess().PrivateMemorySize64;
                var times=new double[30];
                for(int i=0;i<times.Length;i++){var watch=Stopwatch.StartNew();draw();watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
                File.WriteAllLines(Path.Combine(output,"draw-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+".csv"),new[]{"draw_wall_ms"}.Concat(times.Select(t=>t.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                var sorted=times.OrderBy(t=>t).ToArray();
                Console.WriteLine("CACHE-RANGE COST target="+target+" actual="+actual+" n=30 mean="+times.Average()+" median="+sorted[15]+" p95="+sorted[28]+" max="+sorted[29]+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocations)+" managed-delta="+(GC.GetTotalMemory(false)-managed)+" private-delta="+(Process.GetCurrentProcess().PrivateMemorySize64-memory));
                if(actual<1f)
                {
                    int width=(int)Math.Ceiling(Main.Camera.ScaledSize.X)+384,height=(int)Math.Ceiling(Main.Camera.ScaledSize.Y)+384;
                    int cap=game.GraphicsDevice.GraphicsProfile==GraphicsProfile.Reach?2048:4096;
                    Console.WriteLine("CACHE-RANGE seven-layer-admission minimum="+width+"x"+height+" cap="+cap+" rgba7bytes="+((long)width*height*4*7)+" admitted-axis="+(width<=cap&&height<=cap));
                }
            }
            if(!baseline)
            {
                Main.GameZoomTarget=10f;Call(view,"SetUnlockEnabled",false);draw();Require(Main.GameZoomTarget==10f&&Main.GameViewMatrix.RenderZoom.X==10f,"OFF high target retained through actual Draw");
                Main.GameZoomTarget=.3f;draw();Require(Main.GameZoomTarget==.3f&&Main.GameViewMatrix.RenderZoom.X==.296875f,"OFF low target retained through actual Draw");
                var cache=Get(view,"Cache");
                foreach(float target in new[]{.5f,.3f,.32f})
                {
                    Main.GameZoomTarget=target;Call(view,"SetUnlockEnabled",true);Set(cache,"ForceDirect",true);
                    for(int i=0;i<12;i++)draw();var before=new Color[Main.screenWidth*Main.screenHeight];final().GetData(before);
                    Set(cache,"ForceDirect",false);for(int i=0;i<12;i++)draw();var after=new Color[before.Length];final().GetData(after);
                    int changed=0;for(int i=0;i<after.Length;i++)if(before[i]!=after[i])changed++;
                    using(var file=File.Create(Path.Combine(output,"prototype-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                    Console.WriteLine("CACHE-RANGE PROTOTYPE target="+target+" actual="+Main.GameViewMatrix.RenderZoom+" changed-vs-direct="+changed+" generation="+Get(cache,"Generation")+" refresh="+String.Join(",",(int[])Get(cache,"Refreshes"))+" rejection="+cache.GetType().GetProperty("Rejection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache,null));
                    var times=new double[30];long allocation=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    for(int i=0;i<times.Length;i++){var timer=Stopwatch.StartNew();draw();timer.Stop();times[i]=timer.Elapsed.TotalMilliseconds;}
                    File.WriteAllLines(Path.Combine(output,"prototype-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+".csv"),new[]{"draw_wall_ms"}.Concat(times.Select(t=>t.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                    Console.WriteLine("CACHE-RANGE PROTOTYPE COST target="+target+" mean="+times.Average()+" max="+times.Max()+" allocation="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation)+" refresh="+String.Join(",",(int[])Get(cache,"Refreshes")));
                    foreach(string phase in new[]{"still","moving","inverted"})
                    {
                        var playerPosition=Main.LocalPlayer.position;float gravity=Main.LocalPlayer.gravDir;
                        try{if(phase=="moving")Main.LocalPlayer.position+=new Vector2(1,1);if(phase=="inverted")Main.LocalPlayer.gravDir=-1;draw();LeafCompare(game,view,cache,output,target,phase);}
                        finally{Main.LocalPlayer.position=playerPosition;Main.LocalPlayer.gravDir=gravity;draw();}
                    }
                    Set(cache,"ForceDirect",true);
                }
                NativeViewCacheCostChecks.Run(view,draw,output);
                Content(game,view,draw,output);
                Modes(game,view,draw,final,output);
                NativeViewWaterChecks.Run(game,view,draw,final,output,false,.3f);
                NativeViewWaterRouteChecks.Run(game,view,draw,final,output);
                NativeViewFilterChecks.Run(game,view,draw,final,resize,output,true);
                NativeViewCacheLifecycleChecks.Run(game,view,draw);
                NativeViewCacheCopyChecks.Run(game,view,draw);
            }
            Console.WriteLine("PASS CACHE-RANGE actual range/native options, three-layer sampling, finite same-environment cost and optional-cache failure scope; owner FPS/acceptance remains untested.");
        }
        private static void Range(Assembly assembly)
        {
            var type=assembly.GetType("JueMingR.TerrariaHost.View.ViewZoom",true);
            var step=type.GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);
            Func<float,int,bool,float> next=(v,d,on)=>(float)step.Invoke(null,new object[]{v,d,on,.3f});
            Require(next(.3f,-1,true)==.3f&&next(10f,1,true)==10f,"both enabled ends clamp");
            Require(next(.5f,-1,false)==.5f&&next(5f,1,false)==5f,"OFF outward leaves both ends exact");
            Require(Math.Abs(next(.5f,1,false)-.52f)<.00001f&&Math.Abs(next(5f,-1,false)-4.98f)<.00001f,"OFF both ends inward native step");
            Require(next(.99f,1,false)==1f&&next(2.01f,-1,false)==2f,"OFF enters normal range without crossing");
            Require(next(10f,0,false)==10f&&next(.3f,0,false)==.3f,"dual-key zero retains exact target");
        }
        private static void Profile(Main game,object view,Action draw,Func<RenderTarget2D> final,Action<int,int> resize,string output)
        {
            var cache=Get(view,"Cache");
            foreach(float target in new[]{.5f,.3f})
            {
                Main.GameZoomTarget=target;Set(cache,"ForceDirect",true);for(int i=0;i<8;i++)draw();
                Set(cache,"ForceDirect",false);for(int i=0;i<8;i++)draw();
                Require(Get(Get(view,"Plan"),"Route").ToString()=="TileCache","same native HiDef process actually admits and uses partial three-layer route");
                foreach(bool inverted in new[]{false,true})
                {Main.LocalPlayer.gravDir=inverted?-1:1;draw();LeafCompare(game,view,cache,output,target,inverted?"hidef-inverted":"hidef-still");}
                Main.LocalPlayer.gravDir=1;draw();
                Console.WriteLine("PASS CACHE PROFILE actual="+game.GraphicsDevice.GraphicsProfile+" target="+target+" final="+Main.GameViewMatrix.RenderZoom+" layer="+((WorldSceneLayerTarget[])Get(cache,"layers"))[0].Texture.Width+"x"+((WorldSceneLayerTarget[])Get(cache,"layers"))[0].Texture.Height+" owner-profile-unobserved=True seven-layer-implementation=False");
                foreach(bool direct in new[]{true,false,true,false})
                {
                    Set(cache,"ForceDirect",direct);for(int i=0;i<8;i++)draw();
                    var values=new double[30];long allocation=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;var watch=new Stopwatch();
                    for(int i=0;i<30;i++){watch.Restart();draw();watch.Stop();values[i]=watch.Elapsed.TotalMilliseconds;}
                    string name="hidef-cost-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+(direct?"direct":"auto");int pass=Directory.GetFiles(output,name+"-*.csv").Length+1;
                    File.WriteAllLines(Path.Combine(output,name+"-"+pass+".csv"),values.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
                    var sorted=values.OrderBy(v=>v).ToArray();Console.WriteLine("CACHE PROFILE COST target="+target+" actual="+Main.GameViewMatrix.RenderZoom+" override="+(direct?"direct":"auto")+" pass="+pass+" n=30 mean="+values.Average()+" p95="+sorted[28]+" max="+sorted[29]+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation)+" route="+Get(Get(view,"Plan"),"Route"));
                }
            }
            resize(2560,1080);Main.GameZoomTarget=.75f;for(int i=0;i<8;i++)draw();
            Require(Main.screenWidth==2560&&Main.ForcedMinimumZoom>1&&Main.GameViewMatrix.RenderZoom.X==1f,"actual HiDef physical2560/.75 publishes forced>1 but final native zoom1");
            Require((bool)Get(Get(view,"Plan"),"Wide")&&(bool)Get(view,"NeedsExpandedRendering")&&Get(Get(view,"Plan"),"Route").ToString()=="Direct"&&Main.drawToScreen,"expanded world classification is relative to native forced ordinary view; low-density cache does not claim z>=1 sampling");
            DirectEdges(game,draw,output);
            using(var file=File.Create(Path.Combine(output,"hidef-forced-native-direct.png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
            Console.WriteLine("PASS CACHE PROFILE forced-reference physical="+Main.screenWidth+"x"+Main.screenHeight+" target=.75 forced="+Main.ForcedMinimumZoom+" actual="+Main.GameViewMatrix.RenderZoom+" visible="+Main.Camera.ScaledSize+" route=Direct; actual native group capacity is insufficient.");
            NativeViewNativeRouteChecks.Run(game,view,draw,final,output);
            resize(1920,1080);Main.GameZoomTarget=.3f;draw();draw();
            Console.WriteLine("PASS CACHE PROFILE isolated native SetGraphicsProfile(HiDef,false) and current partial cache shader/point sampling; no owner settings/profile change or8K texture creation claim.");
        }
        private static void DirectEdges(Main game,Action draw,string output)
        {
            var left=Main.Camera.ScaledPosition;float z=Main.GameViewMatrix.RenderZoom.X;
            var points=new[]{left+new Vector2(24,Main.screenHeight*.45f)/z,left+new Vector2(Main.screenWidth-24,Main.screenHeight*.45f)/z};
            var saved=new Tile[2];var tiles=new Point[2];
            for(int i=0;i<2;i++)
            {
                tiles[i]=new Point((int)(points[i].X/16),(int)(points[i].Y/16));var tile=Main.tile[tiles[i].X,tiles[i].Y];saved[i]=(Tile)tile.Clone();
                tile.active(true);tile.type=Terraria.ID.TileID.Torches;tile.frameX=tile.frameY=0;
            }
            var device=game.GraphicsDevice;var bindings=device.GetRenderTargets();
            try
            {
                for(int i=0;i<8;i++)draw();
                using(var rt=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
                {
                    device.SetRenderTarget(rt);device.Clear(Color.Transparent);
                    typeof(Main).GetMethod("DrawTiles",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(game,new object[]{false,false,-1});
                    device.SetRenderTarget(null);var pixels=new Color[Main.screenWidth*Main.screenHeight];rt.GetData(pixels);
                    for(int i=0;i<2;i++)
                    {
                        var at=Vector2.Transform(new Vector2(tiles[i].X*16+8,tiles[i].Y*16+8)-Main.screenPosition,Main.GameViewMatrix.TransformationMatrix);int ink=0;
                        for(int y=Math.Max(0,(int)at.Y-20);y<Math.Min(Main.screenHeight,(int)at.Y+20);y++)for(int x=Math.Max(0,(int)at.X-20);x<Math.Min(Main.screenWidth,(int)at.X+20);x++)if(pixels[y*Main.screenWidth+x].A>0)ink++;
                        Require(ink>2,"native non-solid tile leaf submits real torch pixels at forced-view outer edge");
                        Console.WriteLine("CACHE PROFILE native peripheral torch world="+tiles[i]+" physical="+at+" original-leaf-alpha="+ink);
                    }
                    using(var file=File.Create(Path.Combine(output,"hidef-forced-original-tile-leaf.png")))rt.SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                }
            }
            finally{device.SetRenderTargets(bindings);for(int i=0;i<2;i++)Main.tile[tiles[i].X,tiles[i].Y].CopyFrom(saved[i]);}
        }
        private static void Modes(Main game,object view,Action draw,Func<RenderTarget2D> final,string output)
        {
            var cache=Get(view,"Cache");Call(view,"SetUnlockEnabled",true);Main.GameZoomTarget=.3f;Set(cache,"ForceDirect",false);
            try
            {
                foreach(var mode in new[]{LightMode.White,LightMode.Retro,LightMode.Trippy})
                {
                    Lighting.Mode=mode;for(int i=0;i<6;i++)draw();
                    Require(Main.GameViewMatrix.RenderZoom.X==.296875f&&(bool)Get(view,"ControlsEnabled"),"all accepted lighting modes retain actual low-range camera and available owner");
                    if(mode==LightMode.White)LeafCompare(game,view,cache,output,.3f,"white");
                    else
                    {
                        var legacy=typeof(Lighting).GetField("LegacyEngine",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                        var states=(Array)Get(legacy,"_states");
                        Require(states.Length>=Main.Camera.ScaledSize.X/16+100&&((Array)states.GetValue(0)).Length>=Main.Camera.ScaledSize.Y/16+100,"actual legacy lighting capacity covers final30-percent camera and native halo");
                        Require(Get(Get(view,"Plan"),"Route").ToString()=="Direct","legacy modes use their actual direct renderer without tile-cache resources");
                        var color=Lighting.GetColor(315,590);Console.WriteLine("CACHE MODE mode="+mode+" legacy="+states.Length+"x"+((Array)states.GetValue(0)).Length+" actual-light="+color);
                    }
                    using(var file=File.Create(Path.Combine(output,"range-mode-"+mode+".png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                }
                Console.WriteLine("PASS CACHE MODES low .296875 actual White cached native samples /Retro+Trippy direct, real enlarged legacy arrays/GetColor and final Draw images.");
            }
            finally{Lighting.Mode=LightMode.Color;draw();draw();}
        }
        private static void Content(Main game,object view,Action draw,string output)
        {
            var cache=Get(view,"Cache");Set(cache,"ForceDirect",false);Main.GameZoomTarget=.5f;Call(view,"SetUnlockEnabled",true);draw();draw();
            var tile=Main.tile[315,591];var saved=(Tile)tile.Clone();
            var layers=(WorldSceneLayerTarget[])Get(cache,"layers");
            var before=new Color[layers[0].Texture.Width*layers[0].Texture.Height];layers[0].Texture.GetData(before);
            int count=((int[])Get(cache,"Refreshes"))[0];
            try
            {
                tile.active(!saved.active());tile.type=Terraria.ID.TileID.Stone;WorldGen.TileFrame(315,591);Main.renderNow=true;draw();
                var after=new Color[before.Length];layers[0].Texture.GetData(after);int changed=0;for(int i=0;i<before.Length;i++)if(before[i]!=after[i])changed++;
                Console.WriteLine("CACHE CONTENT native refresh="+count+"->"+((int[])Get(cache,"Refreshes"))[0]+" actual edited pixels="+changed);
                Require(((int[])Get(cache,"Refreshes"))[0]>count&&changed>0,"real tile content edit/renderNow refreshes cached native pixels rather than retaining stale geometry");
                LeafCompare(game,view,cache,output,.5f,"edited");
                Console.WriteLine("PASS CACHE CONTENT native tile placement refresh changed="+changed+" output samples; actual original tile/wall leaves checked on same edited scene.");
                Main.mapFullscreen=true;Main.renderNow=true;draw();
                Require(!(bool)Get(cache,"NativeRefreshRequested"),"full-map exit clears unconsumed per-Draw refresh request");
                Main.mapFullscreen=false;draw();
                Main.gameMenu=true;Main.renderNow=true;draw();
                Require(!(bool)Get(cache,"NativeRefreshRequested"),"menu/non-world Draw cannot retain an earlier refresh request");
                Main.gameMenu=false;Main.GameZoomTarget=.5f;draw();draw();
                Require(!(bool)Get(cache,"NativeRefreshRequested"),"ordinary completion consumes/clears this frame's refresh snapshot once");
                Console.WriteLine("PASS CACHE CONTENT refresh snapshot cleared on full-map/menu/noncache boundaries and normal completion; native renderCount remains the refresh clock.");
            }
            finally{Main.mapFullscreen=false;Main.gameMenu=false;tile.CopyFrom(saved);WorldGen.TileFrame(315,591);Main.renderNow=true;draw();}
        }
        private static void LeafCompare(Main game,object view,object cache,string output,float target,string phase)
        {
            Call(cache,"Invalidate");
            Call(cache,"Prepare",Get(view,"Plan"));
            var device=game.GraphicsDevice;var bindings=device.GetRenderTargets();
            var layers=(WorldSceneLayerTarget[])Get(cache,"layers");
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            using(var physical=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
            try
            {
                for(int index=0;index<3;index++)
                {
                    device.SetRenderTarget(physical);device.Clear(Color.Transparent);
                    if(index<2)typeof(Main).GetMethod("DrawTiles",flags).Invoke(game,new object[]{index==0,false,-1});
                    else{typeof(Main).GetMethod("DrawBlack",flags).Invoke(game,new object[]{false,false});typeof(Main).GetMethod("DrawWalls",flags).Invoke(game,new object[]{false});}
                    device.SetRenderTarget(null);var direct=new Color[Main.screenWidth*Main.screenHeight];physical.GetData(direct);
                    using(var file=File.Create(Path.Combine(output,"leaf-direct-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+phase+"-"+index+".png")))physical.SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                    device.SetRenderTarget(physical);device.Clear(Color.Transparent);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,Main.Rasterizer,null,Main.GameViewMatrix.TransformationMatrix);
                    cache.GetType().GetMethod("Composite",flags).Invoke(cache,new object[]{Main.spriteBatch,layers[index].Texture,layers[index].Position-Main.screenPosition,Color.White});
                    Main.spriteBatch.End();
                    device.SetRenderTarget(null);var cached=new Color[direct.Length];physical.GetData(cached);
                    int changed=0,ink=0,alpha=0,maximumRgb=0;for(int i=0;i<direct.Length;i++){if(direct[i].A>0)ink++;if(cached[i]!=direct[i])changed++;if(cached[i].A!=direct[i].A)alpha++;maximumRgb=Math.Max(maximumRgb,Math.Max(Math.Abs(direct[i].R-cached[i].R),Math.Max(Math.Abs(direct[i].G-cached[i].G),Math.Abs(direct[i].B-cached[i].B))));}
                    using(var file=File.Create(Path.Combine(output,"leaf-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+phase+"-"+index+".png")))physical.SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                    Console.WriteLine("CACHE-RANGE FROZEN-LEAF target="+target+" phase="+phase+" layer="+index+" native-direct-ink="+ink+" differences="+changed+" alpha-diff="+alpha);
                    var examples=Enumerable.Range(0,direct.Length).Where(i=>direct[i]!=cached[i]).Take(8).Select(i=>(i%Main.screenWidth)+","+(i/Main.screenWidth)+":"+direct[i]+"->"+cached[i]);
                    Console.WriteLine("CACHE-RANGE LEAF-DIFF "+String.Join(";",examples));
                    Require(alpha==0&&maximumRgb<=1,"native point-sampled geometry/alpha exactly equal; the extra RGBA8 render pass introduces at most one color code of quantization");
                }
            }
            finally{device.SetRenderTargets(bindings);Call(cache,"Detach");}
        }
    }
}
