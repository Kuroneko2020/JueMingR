using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Liquid;
using Terraria.GameContent.Shaders;
using Terraria.Graphics;

namespace JueMingR.TerrariaHost.View
{
    // Native water distortion needs a water mask even when tile layers draw
    // directly. Keep that mask physical-sized; only the native quarter-scale
    // simulation buffers represent world pixels. Reach never receives a full
    // expanded-world render target. No gameplay/simulation rectangle changes.
    internal sealed class ViewWaterResources : IDisposable
    {
        private static FieldInfo Field(string name) { return AccessTools.Field(typeof(WaterShaderData), name); }
        private static readonly FieldInfo distortion=Field("_distortionTarget"), swap=Field("_distortionTargetSwap"), neutral=Field("_noDistortionTexture"), usingTargets=Field("_usingRenderTargets"), clear=Field("_clearNextFrame");
        private static readonly FieldInfo maskPosition=AccessTools.Field(typeof(WorldSceneLayerTarget),"_position");
        private static readonly MethodInfo preDraw=AccessTools.Method(typeof(WaterShaderData),"PreDraw");
        private WaterShaderData shader;
        private GameTime time;
        private Action<GameTime> step;
        private GraphicsDevice device;
        private RenderTarget2D first, second;
        private Texture2D empty, savedEmpty;
        private WorldSceneLayerTarget mask, savedMask;
        private Action<bool> drawWater;
        private TileBatch maskTiles;
        private SpriteBatch maskSprites;
        private static readonly FieldInfo tileSprites=AccessTools.Field(typeof(TileBatch),"_spriteBatch");
        internal int MaskBatchGeneration { get; private set; }
        private static readonly FieldInfo spriteBegun=AccessTools.Field(typeof(SpriteBatch),"inBeginEndPair"),deviceBegins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount"),deviceImmediateBegins=AccessTools.Field(typeof(GraphicsDevice),"spriteImmediateBeginCount");
        private bool attached;
        internal bool Running { get; private set; }
        internal bool RenderingMask { get; private set; }
        internal bool Active { get { return attached; } }
        internal int Generation { get; private set; }
        internal void Defer(WaterShaderData value,GameTime gameTime) { if(!ReferenceEquals(shader,value)){Dispose();shader=value;step=(Action<GameTime>)Delegate.CreateDelegate(typeof(Action<GameTime>),value,preDraw);}time=gameTime; }
        internal void Prepare()
        {
            if(shader==null)return;
            try
            {
                Ensure();
                savedMask=Main.waterTarget;Main.waterTarget=mask;attached=true;
                RenderMask();
                // StepLiquids/DrawWaves are purely visual leaves. Their original
                // identity tile batch consumes world pixels at 1/4 scale. Use
                // the actual scaled origin here, then restore before any UI or
                // world consumer. drawToScreen stays true in Step and Apply so
                // both use the same zero offscreen margin and 4px phase.
                var position=Main.screenPosition;int width=Main.screenWidth,height=Main.screenHeight;
                try
                {
                    Main.screenPosition=Main.Camera.ScaledPosition;
                    var size=Main.Camera.ScaledSize;
                    Main.screenWidth=(int)Math.Ceiling(size.X);Main.screenHeight=(int)Math.Ceiling(size.Y);
                    Running=true;step(time);
                }
                finally {Running=false;Main.screenPosition=position;Main.screenWidth=width;Main.screenHeight=height;}
            }
            catch(OutOfMemoryException error) { throw new ViewRenderFailure(error); }
        }
        private void Ensure()
        {
            var current=Main.instance.GraphicsDevice;
            if(!ReferenceEquals(device,current))
            {
                Release();if(device!=null){device.DeviceResetting-=Reset;device.Disposing-=Reset;}
                device=current;device.DeviceResetting+=Reset;device.Disposing+=Reset;
            }
            var p=current.PresentationParameters;var world=Main.Camera.ScaledSize;
            int width=(int)Math.Ceiling(world.X/256f)*64,height=(int)Math.Ceiling(world.Y/256f)*64;
            bool waves=first==null || first.IsDisposed || second.IsDisposed || first.IsContentLost || second.IsContentLost || first.Width<width || first.Height<height;
            // The quarter-buffer dimensions are capacity, not the exact
            // current viewport. Native StepLiquids copies/reprojects that full
            // texture; Apply samples using its real image size and world offset.
            // Retaining slack is the same domain as the existing rounded bucket.
            // Do not clear waves when narrowing or oscillating across a bucket.
            if(first!=null && !first.IsDisposed){width=Math.Max(width,first.Width);height=Math.Max(height,first.Height);}
            bool surface=mask==null || mask.Texture.IsDisposed || mask.IsContentLost || mask.Texture.Width!=p.BackBufferWidth || mask.Texture.Height!=p.BackBufferHeight;
            bool blank=empty==null || empty.IsDisposed;
            if(!waves && !surface && !blank)return;
            RenderTarget2D a=null,b=null;Texture2D no=null;WorldSceneLayerTarget layer=null;
            try
            {
                if(waves)
                {
                    a=new RenderTarget2D(current,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                    b=new RenderTarget2D(current,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                }
                if(blank){no=new Texture2D(current,1,1,false,SurfaceFormat.Color);no.SetData(new[]{new Color(.5f,.5f,0f,1f)});}
                if(surface)layer=new WorldSceneLayerTarget(current,p.BackBufferWidth,p.BackBufferHeight);
            }
            catch {layer?.Dispose();no?.Dispose();b?.Dispose();a?.Dispose();throw;}
            // Prepare every required replacement before publishing any of it.
            // Capacity buckets only replace the quarter-scale pair; the full
            // physical mask and neutral pixel remain stable across zoom steps.
            if(waves){second?.Dispose();first?.Dispose();first=a;second=b;distortion.SetValue(shader,first);swap.SetValue(shader,second);}
            if(surface){mask?.Dispose();mask=layer;}
            if(blank){savedEmpty=(Texture2D)neutral.GetValue(shader);empty?.Dispose();empty=no;neutral.SetValue(shader,empty);}
            usingTargets.SetValue(shader,true);clear.SetValue(shader,true);Generation++;
        }
        private void RenderMask()
        {
            TileDrawing.GetScreenDrawArea(false,out var _,out int left,out int right,out int top,out int bottom);
            LiquidRenderer.Instance.PrepareDraw(new Rectangle(left,top,right-left,bottom-top));
            EnsureMaskBatches();
            var targets=device.GetRenderTargets();
            ushort begins=(ushort)deviceBegins.GetValue(device),immediateBegins=(ushort)deviceImmediateBegins.GetValue(device);
            var priorTiles=Main.tileBatch;var priorSprites=Main.spriteBatch;
            Exception failure=null;
            try
            {
                // Only this synchronous native liquid leaf sees these owned
                // batches. A partially published queue belongs to VIEW alone;
                // the original shared batch and its pending work stay untouched.
                Main.tileBatch=maskTiles;Main.spriteBatch=maskSprites;
                device.SetRenderTarget((RenderTarget2D)mask.Texture);device.Clear(Color.Transparent);
                maskPosition.SetValue(mask,Main.Camera.ScaledPosition);
                // Original liquid coordinates subtract the unscaled origin.
                // ZoomMatrix supplies its one translation and zoom; gravity
                // remains for the original scene capture's flip/unflip pair.
                // LiquidRenderer submits its real mesh through TileBatch. Its
                // Begin/End applies the GPU transform immediately; a deferred
                // SpriteBatch alone would leave the prior quarter-buffer
                // projection active and produce an empty physical mask.
                Main.tileBatch.Begin(RasterizerState.CullCounterClockwise,Main.GameViewMatrix.ZoomMatrix);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,RasterizerState.CullCounterClockwise,null,Main.GameViewMatrix.ZoomMatrix);
                RenderingMask=true;
                if(drawWater==null)drawWater=(Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>),Main.instance,AccessTools.Method(typeof(Main),"DrawWaters"));
                drawWater(false);
                Main.spriteBatch.End();
            }
            catch(Exception error){failure=error;throw;}
            finally
            {
                RenderingMask=false;
                Main.tileBatch=priorTiles;Main.spriteBatch=priorSprites;
                // Native GetNewSpriteBufferSlice can advance queued count
                // before Array.Resize succeeds. End is not an abort operation:
                // do not flush or index a failed queue, including on submission
                // failures. Retire only our batches; preserve the first error
                // even if their disposal or target restoration also fails.
                if(failure!=null)
                {
                    // Locked XNA End decrements device-wide Begin counts only
                    // after submission; Dispose does not decrement them at all.
                    // Do not submit damaged queues to obtain that bookkeeping.
                    // Close only our retired instances and restore the device
                    // lease snapshot; unrelated entry-time batches stay open.
                    try{spriteBegun.SetValue(maskSprites,false);}catch{}
                    try{spriteBegun.SetValue(tileSprites.GetValue(maskTiles),false);}catch{}
                    try{deviceBegins.SetValue(device,begins);}catch{}
                    try{deviceImmediateBegins.SetValue(device,immediateBegins);}catch{}
                    // Each return is independent: a secondary cleanup error
                    // must neither skip later returns nor replace the primary.
                    DropMaskBatches(failure);
                }
                try{device.SetRenderTargets(targets);}catch when(failure!=null){}
            }
        }
        private void EnsureMaskBatches()
        {
            if(maskTiles!=null && maskSprites!=null)return;
            TileBatch tiles=null;SpriteBatch sprites=null;
            try{tiles=new TileBatch(device);sprites=new SpriteBatch(device);}
            catch(Exception error){DisposeBatches(tiles,sprites,error);throw;}
            maskTiles=tiles;maskSprites=sprites;MaskBatchGeneration++;
        }
        private void DropMaskBatches(Exception primary)
        {
            var tiles=maskTiles;var sprites=maskSprites;maskTiles=null;maskSprites=null;
            DisposeBatches(tiles,sprites,primary);
        }
        private static void DisposeBatches(TileBatch tiles,SpriteBatch sprites,Exception primary)
        {
            // Native TileBatch.Dispose releases its vertex/index buffers but
            // not the private SpriteBatch it constructed. This extra owner
            // explicitly disposes both, plus its foreground SpriteBatch.
            Exception cleanup=null;
            SpriteBatch internalSprites=null;
            try{if(tiles!=null)internalSprites=(SpriteBatch)tileSprites.GetValue(tiles);}catch(Exception error){cleanup=error;}
            try{tiles?.Dispose();}catch(Exception error){if(cleanup==null)cleanup=error;}
            try{internalSprites?.Dispose();}catch(Exception error){if(cleanup==null)cleanup=error;}
            try{sprites?.Dispose();}catch(Exception error){if(cleanup==null)cleanup=error;}
            if(primary==null && cleanup!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        internal bool DrawForeground(bool background)
        {
            if(!attached || RenderingMask || background)return true;
            // This is the sole foreground liquid draw. Rendering it into the
            // mask already advanced native alpha/effects once this frame.
            Main.spriteBatch.Draw(mask.Texture,Main.Camera.ScaledPosition-Main.screenPosition,null,Color.White,0,Vector2.Zero,Vector2.One/Main.GameViewMatrix.RenderZoom,SpriteEffects.None,0);
            return false;
        }
        internal void Detach()
        {if(attached){Main.waterTarget=savedMask;savedMask=null;attached=false;}}
        private void Reset(object sender,EventArgs args) {Detach();Release();}
        private bool Owned(object value) {return first!=null && (ReferenceEquals(value,first)||ReferenceEquals(value,second));}
        private void Release()
        {
            Detach();
            if(shader!=null && (Owned(distortion.GetValue(shader))||Owned(swap.GetValue(shader))))
            {distortion.SetValue(shader,null);swap.SetValue(shader,null);usingTargets.SetValue(shader,false);}
            if(shader!=null && ReferenceEquals(neutral.GetValue(shader),empty))neutral.SetValue(shader,savedEmpty);
            DropMaskBatches(null);
            mask?.Dispose();empty?.Dispose();second?.Dispose();first?.Dispose();mask=null;empty=null;first=second=null;savedEmpty=null;
        }
        public void Dispose()
        {Release();if(device!=null){device.DeviceResetting-=Reset;device.Disposing-=Reset;device=null;}shader=null;time=null;step=null;}
    }
}
