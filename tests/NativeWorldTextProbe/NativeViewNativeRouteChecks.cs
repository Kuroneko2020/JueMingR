using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Shaders;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewNativeRouteChecks
    {
        private static object native;
        private static Exception injected;
        private static bool armed;
        private static bool allocationArmed;
        private static int allocations;
        private static void Allocate(){if(allocationArmed){allocationArmed=false;allocations++;throw new OutOfMemoryException("controlled optional native-coverage batch admission");}}
        private static int attempts,steps,waterCalls,applies;
        private static bool leaf;
        private static WorldSceneLayerTarget appliedMask;
        private static Vector2 appliedScale;
        private static readonly FieldInfo scales=AccessTools.Field(typeof(Terraria.Graphics.Shaders.ScreenShaderData),"_imageScales");
        private static void FailWater(){if(armed&&(bool)Get(native,"Rendering")){armed=false;attempts++;throw injected;}}
        private static void Step(){steps++;}
        private static void Water(bool __0,bool __runOriginal){if(!__0&&__runOriginal)waterCalls++;}
        private static void Applied(WaterShaderData __instance){if(!leaf){applies++;appliedMask=Main.waterTarget;appliedScale=((Vector2[])scales.GetValue(__instance))[2];}}
        internal static void Run(Main game,object view,Action draw,Func<RenderTarget2D> final,string output)
        {
            native=Get(view,"Native");var water=Get(view,"Water");var shader=(WaterShaderData)Filters.Scene["WaterDistortion"].GetShader();
            int quality=Main.WaveQuality;var player=Main.LocalPlayer.position;var audit=new Harmony("JueMingR.Tests.View.NativeRoute");
            audit.Patch(AccessTools.Method(typeof(Main),"DrawWaters"),prefix:new HarmonyMethod(typeof(NativeViewNativeRouteChecks),nameof(FailWater)),postfix:new HarmonyMethod(typeof(NativeViewNativeRouteChecks),nameof(Water)));
            audit.Patch(AccessTools.Method(typeof(WaterShaderData),"StepLiquids"),prefix:new HarmonyMethod(typeof(NativeViewNativeRouteChecks),nameof(Step)));
            audit.Patch(AccessTools.Method(typeof(WaterShaderData),"Apply"),postfix:new HarmonyMethod(typeof(NativeViewNativeRouteChecks),nameof(Applied)));
            audit.Patch(typeof(Terraria.Graphics.TileBatch).GetConstructor(new[]{typeof(GraphicsDevice)}),prefix:new HarmonyMethod(typeof(NativeViewNativeRouteChecks),nameof(Allocate)));
            try
            {
                Main.GameZoomTarget=.9f;for(int i=0;i<8;i++)draw();
                Require(!Main.drawToScreen&&Get(Get(view,"Plan"),"Route").ToString()=="Native","sufficient native capacity remains native despite semantic expansion relative to forced zoom");Coverage();
                Main.GameZoomTarget=.85f;Main.LocalPlayer.position+=new Vector2(60,0);Main.renderNow=false;Main.renderCount=1;draw();Coverage();
                Console.WriteLine("PASS NATIVE COVERAGE simultaneous60-world-pixel move/.9->.85 zoom uses final predicted view in original coverage phase; actual="+Main.GameViewMatrix.RenderZoom+" visible="+Main.Camera.ScaledSize+" group="+Layer("tileTarget").Texture.Width+"x"+Layer("tileTarget").Texture.Height);
                using(var file=File.Create(Path.Combine(output,"forced-sufficient-native-moved.png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                Main.WaveQuality=3;Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);Filters.Scene["WaterDistortion"].Opacity=1;
                // Grow once for both legitimate b=P/native and b=0/direct
                // domains. Subsequent route changes may not clear that capacity.
                Main.GameZoomTarget=.9f;for(int i=0;i<3;i++){shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));draw();}
                Main.GameZoomTarget=.75f;shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));draw();
                Main.GameZoomTarget=.9f;shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));draw();
                var first=Get(water,"first");var second=Get(water,"second");var live=(RenderTarget2D)Get(shader,"_distortionTarget");
                var pixels=new Color[live.Width*live.Height];live.GetData(pixels);
                Vector2 origin=new Vector2(Main.drawToScreen?0:Main.offScreenRange)-(Vector2)Get(shader,"_lastDistortionDrawOffset")*4f;
                for(int i=0;i<12;i++)
                {
                    Main.GameZoomTarget=i%2==0?.75f:.9f;Main.LocalPlayer.gravDir=i>=6?-1:1;steps=waterCalls=0;draw();
                    var now=new Vector2(Main.drawToScreen?0:Main.offScreenRange)-(Vector2)Get(shader,"_lastDistortionDrawOffset")*4f;
                    Console.WriteLine("NATIVE WATER transition="+i+" direct="+Main.drawToScreen+" physical="+Main.screenWidth+"x"+Main.screenHeight+" p="+Main.offScreenRange+" origin="+now+" expected="+origin+" same-first="+ReferenceEquals(first,Get(water,"first"))+" steps="+steps+" generation="+Get(water,"Generation"));
                    Require(ReferenceEquals(first,Get(water,"first"))&&ReferenceEquals(second,Get(water,"second"))&&origin==now&&steps==0,"Native/Direct route converts quarter-buffer margin phase without growth/clear or repeated visual steps");
                    var after=new Color[pixels.Length];live.GetData(after);int changed=0;for(int p=0;p<pixels.Length;p++)if(pixels[p]!=after[p])changed++;
                    Require(changed==0,"frozen wave texture pixels remain identical across native/direct route and gravity changes");
                    if(!Main.drawToScreen)Require(!(bool)Get(water,"Active")&&waterCalls==1,"cached native water is actual liquid layer and shader mask, with no physical mask duplicate");
                    if(i==10||i==11)using(var file=File.Create(Path.Combine(output,"forced-water-"+(Main.drawToScreen?"direct":"native")+"-inverted.png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                }
                Main.LocalPlayer.gravDir=1;Main.GameZoomTarget=.9f;shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));steps=0;draw();
                Require(steps==2&&(int)Get(shader,"_queuedSteps")==0,"native route also defers to exactly one final-camera PreDraw and consumes two queued steps once");
                Console.WriteLine("PASS NATIVE WATER12 route/gravity transitions preserve two buffer identities, exact world wave origin and frozen pixels; native liquid layer remains mask; queued2->Step2 once.");
                Sample(game,view,draw,final,shader,output);
                Main.WaveQuality=0;Filters.Scene.Deactivate("WaterDistortion");Filters.Scene["WaterDistortion"].Opacity=0;Filters.Scene.Update(new GameTime());Main.GameZoomTarget=.9f;Main.renderCount=1;Main.renderNow=false;waterCalls=applies=0;draw();
                Require(!Main.drawToScreen&&waterCalls==1&&applies==0&&!(bool)Get(water,"Active"),"fully faded quality0 native route still refreshes its real liquid layer once, with no extra physical mask");
                Console.WriteLine("PASS NATIVE WATER quality0 original cached liquid refresh1, physical-mask0, waterApply0.");
                var sharedTiles=Main.tileBatch;var sharedSprites=Main.spriteBatch;var screen=Main.screenPosition;int width=Main.screenWidth,height=Main.screenHeight,padding=Main.offScreenRange;var zoom=Main.GameViewMatrix.Zoom;
                var begins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount");ushort entry=(ushort)begins.GetValue(game.GraphicsDevice);
                injected=new IOException("controlled swallowed native RenderWater failure");armed=true;attempts=0;Main.renderCount=1;Main.renderNow=false;bool exact=false;
                try{draw();}catch(Exception error){while(error is TargetInvocationException)error=error.InnerException;exact=ReferenceEquals(error,injected);}
                Require(exact&&attempts==1,"otherwise swallowed native water leaf error propagates as the exact first exception");
                Require(ReferenceEquals(sharedTiles,Main.tileBatch)&&ReferenceEquals(sharedSprites,Main.spriteBatch)&&Main.screenWidth==width&&Main.screenHeight==height&&Main.screenPosition==screen&&Main.offScreenRange==padding&&Main.GameViewMatrix.Zoom==zoom&&(ushort)begins.GetValue(game.GraphicsDevice)==entry,"native scope restores root domain, exact shared batches and XNA lease after UpdateContent skips its native restoration");
                Main.GameZoomTarget=1;int count=Main.fpsCount;for(int i=0;i<3;i++)draw();Require(Main.fpsCount==count+3&&(bool)Get(view,"ControlsEnabled"),"three real ordinary Draw execute after native optional scope unknown failure, without a stuck native Draw guard");
                Console.WriteLine("PASS NATIVE FAILURE real swallowed DrawWaters IO preserved; root/shared leases restored;3 subsequent ordinary Draw valid.");
                Call(native,"Dispose");Main.GameZoomTarget=.9f;bool map=Main.mapEnabled;var mode=Lighting.Mode;allocationArmed=true;allocations=0;count=Main.fpsCount;draw();
                Require(allocations==1&&Main.fpsCount==count+1&&Main.drawToScreen&&Main.GameZoomTarget==.9f&&Main.mapEnabled==map&&Lighting.Mode==mode,"optional native-coverage batch OOM before leaves falls back to correct direct in the same actual Draw, preserving target/settings");
                count=Main.fpsCount;for(int i=0;i<20;i++)draw();Require(Main.fpsCount==count+20&&allocations==1&&Main.drawToScreen&&Main.GameZoomTarget==.9f,"same native batch failure qualification uses20 actual direct Draw without retry");
                Console.WriteLine("PASS NATIVE ADMISSION optional batch OOM ->same-Draw direct then20 actual direct/no retry; target .9 and native lighting/map preserved.");
            }
            finally{armed=allocationArmed=false;audit.UnpatchAll(audit.Id);Main.LocalPlayer.position=player;Main.LocalPlayer.gravDir=1;Main.WaveQuality=quality;Filters.Scene.Deactivate("WaterDistortion");native=null;}
        }
        private static void Sample(Main game,object view,Action draw,Func<RenderTarget2D> final,WaterShaderData shader,string output)
        {
            var center=Main.LocalPlayer.Center+new Vector2(0,100);
            AccessTools.Method(typeof(NativeViewWaterChecks),"Pool").Invoke(null,new object[]{center});
            foreach(bool direct in new[]{false,true})foreach(bool inverted in new[]{false,true})
            {
                Main.LocalPlayer.gravDir=inverted?-1:1;Main.GameZoomTarget=direct?.75f:.9f;Main.renderNow=true;draw();
                Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));shader.QueueRipple(center,new Color(.5f,1,0,1),new Vector2(200,100),RippleShape.Circle);
                applies=0;draw();Console.WriteLine("NATIVE WATER APPLY direct="+direct+" inverted="+inverted+" count="+applies+" scale="+appliedScale+" actual="+Main.GameViewMatrix.RenderZoom);Require(applies==1&&appliedScale==(direct?Vector2.One/Main.GameViewMatrix.RenderZoom:Vector2.One),"actual complete Main.Draw applies native water once with route-correct mask scale");
                var saved=Main.waterTarget;var old=((Vector2[])scales.GetValue(shader))[2];var bindings=game.GraphicsDevice.GetRenderTargets();
                try
                {
                    leaf=true;Main.waterTarget=appliedMask;shader.UseImageScale(appliedScale,2);
                    using(var source=new Texture2D(game.GraphicsDevice,Main.screenWidth,Main.screenHeight))
                    using(var target=new RenderTarget2D(game.GraphicsDevice,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
                    {
                        var grid=new Color[Main.screenWidth*Main.screenHeight];for(int y=0;y<Main.screenHeight;y++)for(int x=0;x<Main.screenWidth;x++)grid[y*Main.screenWidth+x]=new Color((byte)(x%251),(byte)(y%239),(byte)((x+y)%241));source.SetData(grid);
                        Color[][] images=new Color[2][];
                        for(int i=0;i<2;i++)
                        {
                            shader.DrawRipples=i==1;game.GraphicsDevice.SetRenderTarget(target);game.GraphicsDevice.Clear(Color.Transparent);
                            Main.spriteBatch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,inverted?Main.GameViewMatrix.EffectMatrix:Matrix.Identity);
                            Filters.Scene["WaterDistortion"].Apply(new Vector2(Main.screenWidth,Main.screenHeight),new Vector2(Main.screenWidth,Main.screenHeight),Vector2.Zero);
                            Main.spriteBatch.Draw(source,Vector2.Zero,null,Color.White,0,Vector2.Zero,1,SpriteEffects.None,0);Main.spriteBatch.End();game.GraphicsDevice.SetRenderTarget(null);
                            images[i]=new Color[grid.Length];target.GetData(images[i]);
                        }
                        var point=Vector2.Transform(center-Main.screenPosition,Main.GameViewMatrix.TransformationMatrix);int changed=0;
                        for(int y=Math.Max(0,(int)point.Y-90);y<Math.Min(Main.screenHeight,(int)point.Y+90);y++)for(int x=Math.Max(0,(int)point.X-140);x<Math.Min(Main.screenWidth,(int)point.X+140);x++)if(images[0][y*Main.screenWidth+x]!=images[1][y*Main.screenWidth+x])changed++;
                        using(var file=File.Create(Path.Combine(output,"forced-frozen-water-"+(direct?"direct":"native")+(inverted?"-inverted":"")+".png")))target.SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                        Console.WriteLine("NATIVE WATER SAMPLE route="+(direct?"Direct":"Native")+" inverted="+inverted+" changed-at-known-pool="+changed+" physical="+point+" maskScale="+appliedScale+" maskSize="+appliedMask.Texture.Width+"x"+appliedMask.Texture.Height);
                        Require(changed>10,"real native water shader samples ripple at independently projected known liquid pool in both routes and gravity domains");
                    }
                }
                finally{leaf=false;shader.DrawRipples=true;shader.UseImageScale(old,2);Main.waterTarget=saved;game.GraphicsDevice.SetRenderTargets(bindings);}
                if(!inverted)
                {
                    int generation=(int)Get(Get(view,"Water"),"Generation"),batch=(int)Get(Get(view,"Native"),"BatchGeneration");
                    var values=new double[30];long allocation=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,memory=System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;int gc=GC.CollectionCount(0);var watch=new System.Diagnostics.Stopwatch();
                    for(int i=0;i<30;i++){watch.Restart();draw();watch.Stop();values[i]=watch.Elapsed.TotalMilliseconds;}
                    File.WriteAllLines(Path.Combine(output,"forced-active-water-cost-"+(direct?"direct":"native")+".csv"),Array.ConvertAll(values,v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
                    var sorted=(double[])values.Clone();Array.Sort(sorted);double sum=0;foreach(var value in values)sum+=value;
                    Console.WriteLine("NATIVE WATER COST route="+(direct?"Direct":"Native")+" actual="+Main.GameViewMatrix.RenderZoom+" n=30 mean="+sum/30+" median="+sorted[15]+" p95="+sorted[28]+" max="+sorted[29]+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation)+" private-delta="+(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64-memory)+" gen0="+(GC.CollectionCount(0)-gc)+" wave-generation="+generation+"->"+Get(Get(view,"Water"),"Generation")+" native-batch="+batch+"->"+Get(Get(view,"Native"),"BatchGeneration"));
                    Require(generation==(int)Get(Get(view,"Water"),"Generation")&&batch==(int)Get(Get(view,"Native"),"BatchGeneration"),"representative active-water native/direct costs retain resource and private batch generations");
                }
            }
            Main.LocalPlayer.gravDir=1;Main.GameZoomTarget=.9f;draw();
        }
        private static WorldSceneLayerTarget Layer(string name){return (WorldSceneLayerTarget)AccessTools.Field(typeof(Main),name).GetValue(null);}
        private static void Coverage()
        {
            var p=Main.Camera.ScaledPosition;var end=p+Main.Camera.ScaledSize;
            foreach(var layer in new[]{Layer("tileTarget"),Layer("tile2Target"),Layer("wallTarget"),Main.waterTarget,Layer("backWaterTarget"),Layer("backgroundTarget")})
            {var start=layer.Position;Require(start.X<=p.X&&start.Y<=p.Y&&start.X+layer.Texture.Width>=end.X&&start.Y+layer.Texture.Height>=end.Y,"each actual native composite layer covers final camera after move/zoom");}
        }
    }
}
