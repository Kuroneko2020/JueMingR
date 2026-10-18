using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewWaterChecks
    {
        private static WaterShaderData witness;
        private static readonly FieldInfo scalesField=typeof(ScreenShaderData).GetField("_imageScales",BindingFlags.Instance|BindingFlags.NonPublic);
        private static int applies;
        private static bool leaf;
        private static Vector2 appliedScale;
        private static WorldSceneLayerTarget appliedMask;
        private static void Applied(WaterShaderData __instance)
        {if(!leaf && ReferenceEquals(__instance,witness)){appliedScale=((Vector2[])scalesField.GetValue(__instance))[2];appliedMask=Main.waterTarget;Require((bool)Get(__instance,"_usingRenderTargets"),"actual water Apply owns valid buffers");applies++;}}
        internal static void Run(Main game,object view,Action draw,Func<RenderTarget2D> final,string output,bool capacity=false)
        {
            var audit=new Harmony("JueMingR.Tests.View.Water");var filter=Filters.Scene["WaterDistortion"];
            witness=(WaterShaderData)filter.GetShader();int quality=Main.WaveQuality;float gravity=Main.LocalPlayer.gravDir;
            audit.Patch(typeof(WaterShaderData).GetMethod("Apply"),postfix:new HarmonyMethod(typeof(NativeViewWaterChecks),nameof(Applied)));
            try
            {
                Main.WaveQuality=3;Lighting.Mode=LightMode.Color;
                Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);filter.Opacity=1;
                var sharedTiles=Main.tileBatch;var sharedSprites=Main.spriteBatch;
                foreach(string phase in capacity?new[]{"ordinary","wide","narrow","reexpanded","supported","retained","inverted"}:new[]{"ordinary","wide","retained","inverted"})
                {
                    Call(view,"SetUnlockEnabled",phase=="wide"||phase=="narrow"||phase=="reexpanded"||phase=="supported");Main.GameZoomTarget=phase=="ordinary"?1f:capacity?(phase=="wide"?.5390625f:phase=="narrow"?.75f:phase=="reexpanded"?.53125f:.5f):.5f;
                    Main.LocalPlayer.gravDir=phase=="inverted"?-1:1;draw();
                    Vector2 start=Main.Camera.ScaledPosition;float z=Main.GameViewMatrix.RenderZoom.X;
                    var centers=new[]{start+new Vector2(Main.screenWidth*.18f,Main.screenHeight*.48f)/z,start+new Vector2(Main.screenWidth*.82f,Main.screenHeight*.48f)/z};
                    foreach(var center in centers)Pool(center);
                    Main.renderNow=true;
                    for(int i=0;i<20;i++)
                    {
                        Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));
                        foreach(var center in centers)witness.QueueRipple(center,new Color(.5f,1f,0f,1f),new Vector2(180,80),RippleShape.Circle);
                        draw();
                    }
                    Console.WriteLine("VIEW WATER native flags paused="+Main.gamePaused+" focusPause="+FocusHelper.PauseLiquidRenderer+" use="+Get(witness,"_useRippleWaves")+" queued="+Get(witness,"_queuedSteps")+" ripples="+Get(witness,"_rippleQueueCount"));
                    witness.DrawRipples=false;draw();var before=Pixels(final());
                    Save(game.GraphicsDevice,before,output,"view-water-baseline-"+phase);
                    int n=applies;witness.DrawRipples=true;draw();var after=Pixels(final());
                    int left=Changed(before,after,(int)(Main.screenWidth*.18f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f))),right=Changed(before,after,(int)(Main.screenWidth*.82f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f)));
                    int ui=Changed(before,after,35,35,10);
                    witness.DrawRipples=false;draw();var neutralAgain=Pixels(final());
                    witness.DrawRipples=true;draw();var rippleAgain=Pixels(final());
                    int repeatLeft=Changed(neutralAgain,rippleAgain,(int)(Main.screenWidth*.18f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f))),repeatRight=Changed(neutralAgain,rippleAgain,(int)(Main.screenWidth*.82f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f)));
                    var buffer=(RenderTarget2D)Get(witness,"_distortionTarget");
                    var water=Get(view,"Water");object layer=phase=="ordinary"?(object)Main.waterTarget:Get(water,"mask");
                    var mask=(Texture2D)Get(layer,"Texture");
                    var wavePixels=new Color[buffer.Width*buffer.Height];buffer.GetData(wavePixels);int varied=0;foreach(var pixel in wavePixels)if(Math.Abs(pixel.R-128)>2||Math.Abs(pixel.G-128)>2)varied++;
                    using(var file=File.Create(Path.Combine(output,"view-water-wave-"+phase+".png")))buffer.SaveAsPng(file,buffer.Width,buffer.Height);
                    using(var file=File.Create(Path.Combine(output,"view-water-mask-"+phase+".png")))mask.SaveAsPng(file,mask.Width,mask.Height);
                    var maskPixels=new Color[mask.Width*mask.Height];mask.GetData(maskPixels);int ink=0;foreach(var pixel in maskPixels)if(pixel.A>0)ink++;
                    Require(ink>1000 && varied>100,"actual water mask has liquid pixels and native quarter buffer has ripple state");
                    Console.WriteLine("VIEW WATER wave varied="+varied+" mask-liquid-pixels="+ink);
                    Save(game.GraphicsDevice,after,output,"view-water-"+phase);
                    Console.WriteLine("VIEW WATER phase="+phase+" render="+Main.GameViewMatrix.RenderZoom+" applies="+(applies-n)+" distortion="+buffer.Width+"x"+buffer.Height+" mask="+mask.Width+"x"+mask.Height+" leftChanged="+left+" rightChanged="+right+" repeat="+repeatLeft+"/"+repeatRight+" UIchanged="+ui+" generation="+Get(water,"Generation"));
                    Require(applies-n==3 && left>10 && right>10 && repeatLeft>10 && repeatRight>10 && ui==0,"real native water shader changes ripple pixels in both independently placed pools while UI stays unchanged");
                    FrozenLeaf(game,filter,after,phase,output);
                    var samples=new List<double>();long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,managed=GC.GetTotalMemory(false),memory=Process.GetCurrentProcess().PrivateMemorySize64;
                    int sceneGeneration=(int)Get(Get(view,"Scene"),"Generation"),waterGeneration=(int)Get(water,"Generation"),batchGeneration=Convert.ToInt32(GetOptional(water,"MaskBatchGeneration"));
                    for(int i=0;i<30;i++)
                    {
                        Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));
                        foreach(var center in centers)witness.QueueRipple(center,new Color(.5f,1f,0f,1f),new Vector2(180,80),RippleShape.Circle);
                        var watch=Stopwatch.StartNew();draw();watch.Stop();samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                    Require((int)Get(water,"Generation")==waterGeneration && (int)Get(Get(view,"Scene"),"Generation")==sceneGeneration,"active water sample does not rebuild scene or wave resources");
                    Require(ReferenceEquals(Main.tileBatch,sharedTiles)&&ReferenceEquals(Main.spriteBatch,sharedSprites)&&Convert.ToInt32(GetOptional(water,"MaskBatchGeneration"))==batchGeneration,"normal mask scope restores both exact shared batches and reuses its own pair");
                    var sorted=samples.OrderBy(v=>v).ToArray();
                    Console.WriteLine("VIEW WATER COST phase="+phase+" n=30 native-draw-mean-ms="+sorted.Average()+" p95-ms="+sorted[27]+" max-ms="+sorted.Last()+" appdomain-allocated-bytes="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated)+" managed-delta="+(GC.GetTotalMemory(false)-managed)+" private-delta="+(Process.GetCurrentProcess().PrivateMemorySize64-memory)+" private-total="+Process.GetCurrentProcess().PrivateMemorySize64+" stable-scene-generation="+sceneGeneration+" stable-water-generation="+waterGeneration+" stable-mask-batch-generation="+batchGeneration);
                    if(phase!="ordinary")
                    {
                        Require(mask.Width==Main.screenWidth && mask.Height==Main.screenHeight && buffer.Width>=Main.Camera.ScaledSize.X/4 && buffer.Height>=Main.Camera.ScaledSize.Y/4,"physical water mask and quarter-world wave buffers cover actual wide camera");
                        int generation=(int)Get(water,"Generation");for(int i=0;i<10;i++)draw();Require((int)Get(water,"Generation")==generation,"stable actual water buffers are reused");
                    }
                    if(capacity && phase=="wide")CapacityTravel(view,draw);
                }
                Console.WriteLine("PASS VIEW original water wave Update/QueueRipple/StepLiquids/Apply and final pixels ordinary/wide/Retained/negative gravity; physical mask and quarter-world resources reused.");
            }
            finally {witness.DrawRipples=true;Main.WaveQuality=quality;Main.LocalPlayer.gravDir=gravity;Filters.Scene.Deactivate("WaterDistortion");Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(2)));audit.UnpatchAll(audit.Id);witness=null;}
        }
        private static void CapacityTravel(object view,Action draw)
        {
            var water=Get(view,"Water");
            int before=(int)Get(water,"Generation");
            var previous=(RenderTarget2D)Get(water,"first");
            Require(previous.Width==896,"cold .5390625 camera starts with independently expected896 capacity");
            Main.GameZoomTarget=.53125f;
            Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));draw();
            var first=(RenderTarget2D)Get(water,"first");var second=Get(water,"second");
            int generation=(int)Get(water,"Generation"),batchGeneration=Convert.ToInt32(GetOptional(water,"MaskBatchGeneration"));
            Require(generation==before+1 && first.Width==960,"first actual .53125 camera exceeds896 capacity and grows once to960");
            var shader=(WaterShaderData)Filters.Scene["WaterDistortion"].GetShader();
            var seed=Main.Camera.ScaledPosition+new Vector2(Main.screenWidth*.18f,Main.screenHeight*.48f)/Main.GameViewMatrix.RenderZoom;
            Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));shader.QueueRipple(seed,new Color(.5f,1f,0f,1f),new Vector2(180,80),RippleShape.Circle);draw();
            var current=(RenderTarget2D)Get(shader,"_distortionTarget");var pixels=new Color[current.Width*current.Height];current.GetData(pixels);
            int varied=0;foreach(var pixel in pixels)if(Math.Abs(pixel.R-128)>2||Math.Abs(pixel.G-128)>2)varied++;Require(varied>100,"capacity travel starts with real nonneutral ripple state");
            for(int i=0;i<100;i++)
            {
                Main.GameZoomTarget=i%2==0?.5390625f:.53125f;draw();
                Require(Main.GameViewMatrix.RenderZoom.X==Main.GameZoomTarget,"bucket traversal uses the actual native final matrix");
            }
            Main.GameZoomTarget=.75f;draw();Main.GameZoomTarget=.53125f;draw();
            Console.WriteLine("VIEW WATER CAPACITY before="+generation+" after-travel="+Get(water,"Generation")+" captured-wave-disposed="+current.IsDisposed+" seeded-wave-pixels="+varied);
            Require(!current.IsDisposed,"cross-bucket traversal must not dispose the still-sufficient wave capacity");
            var after=new Color[pixels.Length];current.GetData(after);
            int changes=0;for(int i=0;i<pixels.Length;i++)if(pixels[i]!=after[i])changes++;
            Console.WriteLine("VIEW WATER CAPACITY actual .53125<->.5390625 x100 then.75->.53125 before="+before+" after-growth="+generation+" final="+Get(water,"Generation")+" allocated="+first.Width+"x"+first.Height+" frozen-wave-pixel-changes="+changes+" mask-batch-generation="+GetOptional(water,"MaskBatchGeneration"));
            Require((int)Get(water,"Generation")==generation && ReferenceEquals(Get(water,"first"),first)&&ReferenceEquals(Get(water,"second"),second)&&changes==0,"cross-bucket inward/outward and narrowing/re-expansion keep both capacity buffers and existing wave state without recreation or clearing");
            Require(Convert.ToInt32(GetOptional(water,"MaskBatchGeneration"))==batchGeneration,"capacity traversal does not recreate the owned liquid batch pair");
        }
        // Keep exactly one world image, mask, wave state and shader inputs.
        // This narrow shader leaf separates actual ripple sampling from native
        // torch lighting/flicker differences between complete Main.Draw calls.
        // The preceding complete Draw audit remains the integration evidence.
        private static void FrozenLeaf(Main game,Filter filter,Color[] frozen,string phase,string output)
        {
            var device=game.GraphicsDevice;var targets=device.GetRenderTargets();var saved=Main.waterTarget;
            var scales=(Vector2[])scalesField.GetValue(witness);var oldScale=scales[2];
            Require(appliedMask!=null && appliedScale==(phase=="ordinary"?Vector2.One:Vector2.One/Main.GameViewMatrix.RenderZoom),"actual original water Apply observed independent logical mask size");
            try
            {
                leaf=true;Main.waterTarget=appliedMask;witness.UseImageScale(appliedScale,2);
                using(var source=new Texture2D(device,Main.screenWidth,Main.screenHeight))
                using(var target=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
                {
                    source.SetData(frozen);Color[][] results=new Color[2][];
                    for(int i=0;i<2;i++)
                    {
                        device.SetRenderTarget(target);device.Clear(Color.Transparent);witness.DrawRipples=i==1;
                        Main.spriteBatch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,phase=="inverted"?Main.GameViewMatrix.EffectMatrix:Matrix.Identity);
                        filter.Apply(new Vector2(Main.screenWidth,Main.screenHeight),new Vector2(Main.screenWidth,Main.screenHeight),Vector2.Zero);
                        Main.spriteBatch.Draw(source,Vector2.Zero,null,Color.White,0,Vector2.Zero,1,phase=="inverted"?SpriteEffects.FlipVertically:SpriteEffects.None,0);Main.spriteBatch.End();device.SetRenderTarget(null);
                        results[i]=Pixels(target);
                    }
                    int left=Changed(results[0],results[1],(int)(Main.screenWidth*.18f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f))),right=Changed(results[0],results[1],(int)(Main.screenWidth*.82f),(int)(Main.screenHeight*(phase=="inverted"?.52f:.48f)));
                    Save(device,results[1],output,"view-water-frozen-"+phase);
                    Console.WriteLine("VIEW WATER frozen-original-shader-leaf phase="+phase+" leftChanged="+left+" rightChanged="+right+" imageScale="+appliedScale+" maskPosition="+appliedMask.Position);
                    Require(left>10 && right>10,"same frozen world/mask/wave inputs differ under native DrawRipples without any world lighting update");
                }
            }
            finally {leaf=false;witness.DrawRipples=true;witness.UseImageScale(oldScale,2);Main.waterTarget=saved;device.SetRenderTargets(targets);}
        }
        private static void Pool(Vector2 center)
        {
            int cx=(int)(center.X/16),cy=(int)(center.Y/16);
            for(int x=cx-16;x<=cx+16;x++)for(int y=cy-8;y<=cy+12;y++)
            {
                if(x<2||y<2||x>=Main.maxTilesX-2||y>=Main.maxTilesY-2)continue;
                var tile=Main.tile[x,y];tile.active(y==cy+12||x==cx-16||x==cx+16);tile.type=TileID.GoldBrick;tile.wall=(ushort)((x+y)%2==0?WallID.GoldBrick:WallID.Glass);tile.liquid=(byte)(y>=cy?255:0);tile.liquidType(0);WorldGen.TileFrame(x,y);
                Lighting.AddLight(x,y,1,1,1);
                if(y==cy-2 && (x==cx-10||x==cx||x==cx+10)){tile.active(true);tile.type=TileID.Torches;WorldGen.TileFrame(x,y);}
            }
        }
        private static Color[] Pixels(RenderTarget2D target){var data=new Color[target.Width*target.Height];target.GetData(data);return data;}
        private static int Changed(Color[] a,Color[] b,int cx,int cy,int radius=100)
        {int n=0;for(int y=Math.Max(0,cy-radius);y<Math.Min(Main.screenHeight,cy+radius);y++)for(int x=Math.Max(0,cx-radius);x<Math.Min(Main.screenWidth,cx+radius);x++)if(a[y*Main.screenWidth+x]!=b[y*Main.screenWidth+x])n++;return n;}
        private static void Save(GraphicsDevice device,Color[] data,string output,string name)
        {using(var image=new Texture2D(device,Main.screenWidth,Main.screenHeight)){image.SetData(data);using(var file=File.Create(Path.Combine(output,name+".png")))image.SaveAsPng(file,Main.screenWidth,Main.screenHeight);}}
    }
}
