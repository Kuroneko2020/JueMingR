using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;

namespace JueMingR.TerrariaHost.View
{
    // A sufficient native group remains native. RenderToTargets precedes the
    // final zoom setter, so borrow only this Draw's predicted zoom for its
    // original coverage tests. Native UpdateContent/refresh groups still own
    // the seven textures and background swap. Its missing finally is contained
    // here, with private batches so a swallowed leaf error cannot poison Main's
    // shared queue. No original InitTargets/OOM/settings fallback is invoked.
    internal sealed class ViewNativeCoverage : IDisposable
    {
        private GraphicsDevice device, failedDevice;
        private TileBatch tiles;
        private SpriteBatch sprites;
        private int failedWidth,failedHeight,width,height,padding;
        private Vector2 screen,zoom;
        private bool direct,pending;
        private TileBatch priorTiles;
        private SpriteBatch priorSprites;
        private RenderTargetBinding[] bindings;
        private ushort entry,immediate;
        private Exception leafFailure;
        private readonly WorldSceneLayerTarget[] group=new WorldSceneLayerTarget[7];
        private static readonly FieldInfo[] fields=Array.ConvertAll(new[]{"tileTarget","tile2Target","wallTarget","waterTarget","backWaterTarget","backgroundTarget","backgroundTargetSwap"},name=>AccessTools.Field(typeof(Main),name));
        private static readonly FieldInfo position=AccessTools.Field(typeof(WorldSceneLayerTarget),"_position"),swapPending=AccessTools.Field(typeof(Main),"backgroundTargetSwapPending"),inside=AccessTools.Field(typeof(TileBatch),"_spriteBatch"),begun=AccessTools.Field(typeof(SpriteBatch),"inBeginEndPair"),begins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount"),immediates=AccessTools.Field(typeof(GraphicsDevice),"spriteImmediateBeginCount");
        internal bool Rendering { get; private set; }
        internal int BatchGeneration { get; private set; }
        internal bool Rejected(ViewRenderPlan plan)
        {return plan!=null&&ReferenceEquals(failedDevice,Main.instance.GraphicsDevice)&&failedWidth==plan.Width&&failedHeight==plan.Height;}
        internal bool Begin(ViewRenderPlan plan)
        {
            var current=Main.instance.GraphicsDevice;
            try
            {
                if(!ReferenceEquals(device,current)){Dispose();device=current;device.DeviceResetting+=Reset;device.Disposing+=Reset;}
                if(tiles==null)
                {
                    TileBatch next=null;SpriteBatch other=null;
                    try{next=new TileBatch(current);other=new SpriteBatch(current);}
                    catch(Exception error){Drop(next,other,error);throw;}
                    tiles=next;sprites=other;BatchGeneration++;
                }
            }
            catch(OutOfMemoryException){Reject(plan);return false;}
            width=Main.screenWidth;height=Main.screenHeight;padding=Main.offScreenRange;
            screen=Main.screenPosition;zoom=Main.GameViewMatrix.Zoom;direct=Main.drawToScreen;
            priorTiles=Main.tileBatch;priorSprites=Main.spriteBatch;bindings=current.GetRenderTargets();
            entry=(ushort)begins.GetValue(current);immediate=(ushort)immediates.GetValue(current);
            pending=(bool)swapPending.GetValue(null);for(int i=0;i<7;i++)group[i]=(WorldSceneLayerTarget)fields[i].GetValue(null);
            leafFailure=null;Rendering=true;Main.tileBatch=tiles;Main.spriteBatch=sprites;
            Main.GameViewMatrix.Zoom=new Vector2(plan.RawZoom);
            return true;
        }
        internal void NoteFailure(Exception error){if(Rendering&&error!=null&&leafFailure==null)leafFailure=error;}
        internal Exception End(Exception error,ViewRenderPlan plan)
        {
            if(!Rendering)return error;
            var failure=leafFailure??error;
            Rendering=false;Main.tileBatch=priorTiles;Main.spriteBatch=priorSprites;
            Main.screenWidth=width;Main.screenHeight=height;Main.offScreenRange=padding;Main.screenPosition=screen;Main.GameViewMatrix.Zoom=zoom;Main.drawToScreen=direct;
            if(failure!=null)
            {
                // A native UpdateContent publishes position before rendering.
                // Restore swap identity and invalidate its content, rather than
                // treating a partially drawn texture as a successful old frame.
                for(int i=0;i<7;i++){fields[i].SetValue(null,group[i]);if(group[i]!=null)position.SetValue(group[i],Vector2.Zero);}
                swapPending.SetValue(null,pending);
                try{begun.SetValue(sprites,false);}catch{}
                try{begun.SetValue(inside.GetValue(tiles),false);}catch{}
                try{begins.SetValue(device,entry);}catch{}
                try{immediates.SetValue(device,immediate);}catch{}
                var a=tiles;var b=sprites;tiles=null;sprites=null;Drop(a,b,failure);
                if(failure is OutOfMemoryException)Reject(plan);
            }
            try{device.SetRenderTargets(bindings);}catch(Exception cleanup){if(failure==null)failure=cleanup;}
            bindings=null;priorTiles=null;priorSprites=null;leafFailure=null;
            return failure;
        }
        private void Reject(ViewRenderPlan plan){failedDevice=Main.instance.GraphicsDevice;failedWidth=plan.Width;failedHeight=plan.Height;}
        private static void Drop(TileBatch tiles,SpriteBatch sprites,Exception primary)
        {
            Exception failure=primary;SpriteBatch nested=null;
            try{if(tiles!=null)nested=(SpriteBatch)inside.GetValue(tiles);}catch(Exception e){if(failure==null)failure=e;}
            try{tiles?.Dispose();}catch(Exception e){if(failure==null)failure=e;}
            try{nested?.Dispose();}catch(Exception e){if(failure==null)failure=e;}
            try{sprites?.Dispose();}catch(Exception e){if(failure==null)failure=e;}
            if(primary==null&&failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
        }
        private void Reset(object sender,EventArgs args){var a=tiles;var b=sprites;tiles=null;sprites=null;failedDevice=null;Drop(a,b,null);}
        // Semantic ordinary context is confirmed by the final frame plan.
        // Expanded/OFF-Retained and Native<->Direct transitions keep their
        // reusable batches; ordinary exits only this owner, not native/waves.
        internal void ExitOrdinary(){if(!Rendering&&device!=null)Dispose();}
        public void Dispose(){if(device!=null){device.DeviceResetting-=Reset;device.Disposing-=Reset;}device=null;Reset(null,EventArgs.Empty);}
    }
}
