using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Light;

namespace JueMingR.TerrariaHost.View
{
    // Conditional k=z three-layer route for a device which cannot admit
    // the seven1:1 targets. Logical world coverage is deliberately independent
    // from the smaller texture. Dynamic tile entities remain in native Draw.
    // Point-sampling and refresh phase are checked against the actual native
    // leaves. A direct-only test override preserves the same selected camera.
    internal sealed class ViewTileCache : IDisposable
    {
        internal bool ForceDirect { get; set; }
        internal bool NativeRefreshRequested { get; set; }
        internal bool Attached { get; private set; }
        internal bool Rendering { get; private set; }
        internal bool Inverted { get { return inverted; } }
        internal float Scale { get; private set; }
        internal int Generation { get; private set; }
#if DEBUG
        internal readonly int[] Refreshes = new int[3];
#endif
        private GraphicsDevice device;
        private readonly WorldSceneLayerTarget[] layers = new WorldSceneLayerTarget[3];
        private readonly bool[] valid = new bool[3];
        private readonly Vector2[] origins = new Vector2[3];
        private readonly Vector2[] sampleOrigins = new Vector2[3];
        private Vector2 leafOffset;
        private bool inverted;
        private LightMode mode,failedMode;
        private bool observed;
        private Vector2 priorCamera,priorWorld;
        private float priorScale;
        private int priorPadding;
        private bool priorInverted;
        private LightMode priorMode;
        private int leafHeight;
        private readonly WorldSceneLayerTarget[] saved=new WorldSceneLayerTarget[3];
        private static readonly FieldInfo[] native={AccessTools.Field(typeof(Main),"tileTarget"),AccessTools.Field(typeof(Main),"tile2Target"),AccessTools.Field(typeof(Main),"wallTarget")};
        private Vector2 logicalSize;
        private int padding;
        private TileBatch tiles;
        private SpriteBatch sprites;
        private readonly Action[] render = new Action[3];
        private static readonly FieldInfo position=AccessTools.Field(typeof(WorldSceneLayerTarget),"_position"),tileSprites=AccessTools.Field(typeof(TileBatch),"_spriteBatch"),spriteBegun=AccessTools.Field(typeof(SpriteBatch),"inBeginEndPair"),begins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount"),immediates=AccessTools.Field(typeof(GraphicsDevice),"spriteImmediateBeginCount");
        private GraphicsDevice failedDevice;
        private int failedWidth,failedHeight,failedPhysicalWidth,failedPhysicalHeight;
        private Exception leafFailure,compositeFailure;
        private SpriteBatch compositeCaller;
        private ViewRenderPlan attachedPlan;
        internal long PeakWorldBytes { get; private set; }
        internal ulong AvailableVirtualAtPrepare { get; private set; }
        private static readonly FieldInfo sort=AccessTools.Field(typeof(SpriteBatch),"spriteSortMode"),blend=AccessTools.Field(typeof(SpriteBatch),"blendState"),depth=AccessTools.Field(typeof(SpriteBatch),"depthStencilState"),raster=AccessTools.Field(typeof(SpriteBatch),"rasterizerState"),sampler=AccessTools.Field(typeof(SpriteBatch),"samplerState"),effect=AccessTools.Field(typeof(SpriteBatch),"customEffect"),transform=AccessTools.Field(typeof(SpriteBatch),"transformMatrix");
        internal string Rejection { get; private set; }
        internal Matrix LeafMatrix { get { return Matrix.CreateScale(Scale,inverted?-Scale:Scale,1f)*Matrix.CreateTranslation(leafOffset.X,inverted?leafHeight+leafOffset.Y:leafOffset.Y,0); } }
        internal void Prepare(ViewRenderPlan plan)
        {
            // Main clears renderNow before final zoom/capture. The early
            // native route decision snapshots that one frame's request; no
            // new refresh clock substitutes for native renderCount group3.
            bool refresh=NativeRefreshRequested;NativeRefreshRequested=false;
            if(ForceDirect || plan==null || !plan.Wide || plan.RenderZoom>=1f || !Lighting.NotRetro){observed=false;Release();return;}
            if(!plan.MatchesNative() || !Main.drawToScreen || Main.mapFullscreen || Main.onlyDrawFancyUI){Detach();return;}
            var current=Main.instance.GraphicsDevice;
            int cap=current.GraphicsProfile==GraphicsProfile.Reach?2048:4096;
            float k=plan.RenderZoom;
            int p=192;
            while(p>=16 && (Math.Ceiling((plan.WorldSize.X+2*p)*k)>cap || Math.Ceiling((plan.WorldSize.Y+2*p)*k)>cap))p-=16;
            if(p<16){Rejection="visible area exceeds low-density device cap";return;}
            int width=(int)Math.Ceiling((plan.WorldSize.X+2*p)*k),height=(int)Math.Ceiling((plan.WorldSize.Y+2*p)*k);
            int requestedWidth=width,requestedHeight=height;
            if(ReferenceEquals(failedDevice,current)&&failedMode==Lighting.Mode&&failedWidth==width&&failedHeight==height&&failedPhysicalWidth==plan.Width&&failedPhysicalHeight==plan.Height)return;
            try
            {
                bool flip=(Main.GameViewMatrix.Effects&SpriteEffects.FlipVertically)!=0;
                bool geometry=Scale!=k || padding!=p || logicalSize!=plan.WorldSize || inverted!=flip || mode!=Lighting.Mode;
                Vector2 start=Main.Camera.ScaledPosition;
                bool steady=observed&&priorScale==k&&priorPadding==p&&priorWorld==plan.WorldSize&&priorInverted==flip&&priorMode==Lighting.Mode&&SamePixelPhase(start-priorCamera,k);
                observed=true;priorCamera=start;priorScale=k;priorPadding=p;priorWorld=plan.WorldSize;priorInverted=flip;priorMode=Lighting.Mode;
                bool capacity=NeedsCapacity(current,width,height),phaseChanged=false;
                for(int i=0;i<3;i++)if(!valid[i]||!SamePixelPhase(start-sampleOrigins[i],k))phaseChanged=true;
                // Moving fractional output samples and continuous target changes
                // cannot reuse this texture. Draw directly until that geometry
                // is stable for one native Draw interval; do not pay a full
                // cache fill plus the same frame's composition while moving.
                // Existing correctly phased samples still follow native group3
                // refreshes and coverage, without delaying real content updates.
                if(!steady&&(geometry||capacity||phaseChanged)){Detach();return;}
                if(capacity)
                {
                    if(ReferenceEquals(device,current)&&layers[0]!=null&&!layers[0].Texture.IsDisposed){width=Math.Max(width,layers[0].Texture.Width);height=Math.Max(height,layers[0].Texture.Height);}
                    long old=0;foreach(var item in layers)if(item!=null&&!item.Texture.IsDisposed)old+=(long)item.Texture.Width*item.Texture.Height*4;
                    long added=(long)width*height*4*3;PeakWorldBytes=old+added;
                    ulong available;
                    if(!ViewCacheMemory.Admit(added,out available)){AvailableVirtualAtPrepare=available;RememberFailure(current,requestedWidth,requestedHeight,plan);Rejection="optional cache memory/old+new peak admission rejected";return;}
                    AvailableVirtualAtPrepare=available;
                    var group=new WorldSceneLayerTarget[3];
                    try{for(int i=0;i<3;i++)group[i]=new WorldSceneLayerTarget(current,width,height);}
                    catch(Exception error){DisposeGroup(group,error);throw;}
                    try{Release();}catch(Exception error){DisposeGroup(group,error);throw;}
                    if(device!=null&&!ReferenceEquals(device,current)){device.DeviceResetting-=Reset;device.Disposing-=Reset;}
                    if(!ReferenceEquals(device,current)){device=current;device.DeviceResetting+=Reset;device.Disposing+=Reset;}
                    Scale=k;padding=p;logicalSize=plan.WorldSize;inverted=flip;mode=Lighting.Mode;
                    for(int i=0;i<3;i++)layers[i]=group[i];Generation++;
                }
                else if(geometry){Scale=k;padding=p;logicalSize=plan.WorldSize;inverted=flip;mode=Lighting.Mode;Invalidate();}
                if(tiles==null){tiles=new TileBatch(device);sprites=new SpriteBatch(device);}
                if(render[0]==null)
                {
                    string[] names={"RenderTiles","RenderTiles2","RenderWallsAndBlacks"};
                    for(int i=0;i<3;i++)render[i]=(Action)Delegate.CreateDelegate(typeof(Action),Main.instance,AccessTools.Method(typeof(Main),names[i]));
                }
                Vector2 end=start+Main.Camera.ScaledSize;
                for(int i=0;i<3;i++)
                    if(!valid[i] || !SamePixelPhase(start-sampleOrigins[i],k) || Main.renderCount==3 || refresh || start.X<origins[i].X || start.Y<origins[i].Y || end.X>origins[i].X+logicalSize.X+2*padding || end.Y>origins[i].Y+logicalSize.Y+2*padding)
                        Fill(i);
                for(int i=0;i<3;i++){saved[i]=(WorldSceneLayerTarget)native[i].GetValue(null);native[i].SetValue(null,layers[i]);}Attached=true;attachedPlan=plan;Rejection=null;failedDevice=null;
            }
            catch(OutOfMemoryException error)
            {
                Release(error);RememberFailure(current,requestedWidth,requestedHeight,plan);Rejection="optional tile cache allocation failed; same target uses direct";
            }
        }
        private void RememberFailure(GraphicsDevice current,int width,int height,ViewRenderPlan plan)
        {failedDevice=current;failedMode=Lighting.Mode;failedWidth=width;failedHeight=height;failedPhysicalWidth=plan.Width;failedPhysicalHeight=plan.Height;}
        private bool NeedsCapacity(GraphicsDevice current,int width,int height)
        {
            if(!ReferenceEquals(device,current))return true;
            foreach(var layer in layers)
                if(layer==null||layer.Texture.IsDisposed||layer.IsContentLost||layer.Texture.Width<width||layer.Texture.Height<height)return true;
            return false;
        }
        internal void Invalidate(){for(int i=0;i<3;i++)valid[i]=false;}
        internal void NoteLeafFailure(Exception failure){if(Rendering&&failure!=null&&leafFailure==null)leafFailure=failure;}
        internal void NoteCompositeFailure(Exception failure){if(failure!=null&&compositeFailure==null)compositeFailure=failure;}
        internal bool HasCompositeFailure { get { return compositeFailure!=null; } }
        internal Exception TakeCompositeFailure(Exception fallback)
        {
            // Native's second tile wrapper may itself abort while trying to
            // reopen a still-open caller, before the post-call seam is reached.
            // This is the exact healthy caller we reopened in Composite, not
            // an unrelated shared/foreign batch or a failed owned queue.
            if(compositeFailure!=null&&ReferenceEquals(Main.spriteBatch,compositeCaller)&&(bool)spriteBegun.GetValue(compositeCaller))
                try{compositeCaller.End();}catch{}
            var error=compositeFailure;compositeFailure=null;compositeCaller=null;
            if(error is OutOfMemoryException&&attachedPlan!=null)
            {
                var plan=attachedPlan;var current=device;
                int width=(int)Math.Ceiling((plan.WorldSize.X+2*padding)*Scale),height=(int)Math.Ceiling((plan.WorldSize.Y+2*padding)*Scale);
                Release(error);RememberFailure(current,width,height,plan);Rejection="optional tile copy failed; same target uses direct";
            }
            return error??fallback;
        }
        internal void ThrowCompositeFailure(bool closeWorld)
        {
            if(compositeFailure==null)return;
            // The locked native WallsTilesNPCs wrapper reopens its healthy
            // caller batch after catching a leaf error. Close that exact batch
            // at this known abort seam before preserving the first copy error.
            if(closeWorld&&ReferenceEquals(Main.spriteBatch,compositeCaller)&&(bool)spriteBegun.GetValue(compositeCaller))
                try{compositeCaller.End();}catch{}
            var error=TakeCompositeFailure(null);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }
        private static bool SamePixelPhase(Vector2 offset,float scale)
        {
            // A k=z texture contains output samples, not full world detail.
            // Coverage alone cannot reuse it after a fractional output-pixel
            // camera move. Refill at the new native phase; never smear/omit a
            // thin object to obtain a low moving refresh count.
            float x=offset.X*scale,y=offset.Y*scale;
            return Math.Abs(x-(float)Math.Round(x))<.0001f&&Math.Abs(y-(float)Math.Round(y))<.0001f;
        }
        private void Fill(int index)
        {
            int width=Main.screenWidth,height=Main.screenHeight,p=Main.offScreenRange;
            Vector2 screen=Main.screenPosition,zoom=Main.GameViewMatrix.Zoom;
            bool direct=Main.drawToScreen;
            var priorTiles=Main.tileBatch;var priorSprites=Main.spriteBatch;
            var targets=device.GetRenderTargets();
            ushort entry=(ushort)begins.GetValue(device),immediate=(ushort)immediates.GetValue(device);
            Exception failure=null;
            var sampleOrigin=Main.Camera.ScaledPosition;
            Vector2 origin=Terraria.Utils.Round(Main.Camera.Center-logicalSize/2f)-new Vector2(padding);
            // Use the actual native transform, including its rounded camera
            // translation and gravity. At38/128 the rounded visible height
            // projects to1080.03125, so flipping about an assumed1080 would
            // select the wrong point samples at some horizontal edges.
            Vector2 projected=Vector2.Transform(origin-screen,Main.GameViewMatrix.TransformationMatrix);
            leafOffset=new Vector2(projected.X-(float)Math.Floor(projected.X+.5f),projected.Y-(float)Math.Floor(projected.Y+.5f));
            leafHeight=layers[index].Texture.Height;
            valid[index]=false;
            leafFailure=null;
            try
            {
                Main.tileBatch=tiles;Main.spriteBatch=sprites;
                Main.screenWidth=(int)logicalSize.X;Main.screenHeight=(int)logicalSize.Y;Main.screenPosition=origin+new Vector2(padding);
                Main.offScreenRange=padding;Main.drawToScreen=false;Main.GameViewMatrix.Zoom=Vector2.One;
                device.SetRenderTarget((RenderTarget2D)layers[index].Texture);device.Clear(Color.Transparent);
                Rendering=true;render[index]();
                if(leafFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(leafFailure).Throw();
                origins[index]=origin;sampleOrigins[index]=sampleOrigin;position.SetValue(layers[index],origin);valid[index]=true;
#if DEBUG
                Refreshes[index]++;
#endif
            }
            catch(Exception error){failure=leafFailure??error;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();throw;}
            finally
            {
                Rendering=false;Main.tileBatch=priorTiles;Main.spriteBatch=priorSprites;
                Main.screenWidth=width;Main.screenHeight=height;Main.screenPosition=screen;Main.offScreenRange=p;Main.drawToScreen=direct;Main.GameViewMatrix.Zoom=zoom;
                if(failure!=null)
                {
                    try{spriteBegun.SetValue(sprites,false);}catch{}
                    try{spriteBegun.SetValue(tileSprites.GetValue(tiles),false);}catch{}
                    try{begins.SetValue(device,entry);}catch{}
                    try{immediates.SetValue(device,immediate);}catch{}
                    DropBatches(failure);
                }
                try{device.SetRenderTargets(targets);}catch when(failure!=null){}
            }
        }
        internal void Composite(SpriteBatch batch,Texture2D texture,Vector2 at,Color color)
        {
            if(Attached && (ReferenceEquals(texture,layers[0].Texture)||ReferenceEquals(texture,layers[1].Texture)||ReferenceEquals(texture,layers[2].Texture)))
            {
                // k=z makes this a one-output-pixel copy. The surrounding
                // direct world batch is native LinearClamp; filtering this RT
                // a second time would blur its alpha/one-pixel edges. Flush the
                // caller's own prior work with its original state, use point
                // only for this copy, then reopen exactly that original state.
                var oldSort=(SpriteSortMode)sort.GetValue(batch);var oldBlend=(BlendState)blend.GetValue(batch);var oldDepth=(DepthStencilState)depth.GetValue(batch);var oldRaster=(RasterizerState)raster.GetValue(batch);var oldSampler=(SamplerState)sampler.GetValue(batch);var oldEffect=(Effect)effect.GetValue(batch);var oldTransform=(Matrix)transform.GetValue(batch);
                batch.End();
                ushort entry=(ushort)begins.GetValue(device),immediate=(ushort)immediates.GetValue(device);Exception failure=null;
                try
                {
                    sprites.Begin(oldSort,oldBlend,SamplerState.PointClamp,oldDepth,oldRaster,oldEffect,oldTransform);
                    sprites.Draw(texture,at,null,color,0,Vector2.Zero,1f/Scale,inverted?SpriteEffects.FlipVertically:SpriteEffects.None,0);
                    sprites.End();
                }
                catch(Exception error)
                {
                    compositeCaller=batch;
                    failure=error;
                    try{spriteBegun.SetValue(sprites,false);}catch{}
                    try{begins.SetValue(device,entry);}catch{}
                    try{immediates.SetValue(device,immediate);}catch{}
                    DropBatches(error);throw;
                }
                finally{try{batch.Begin(oldSort,oldBlend,oldSampler,oldDepth,oldRaster,oldEffect,oldTransform);}catch when(failure!=null){}}
            }
            else batch.Draw(texture,at,color);
        }
        internal void Detach()
        {
            if(!Attached)return;
            for(int i=0;i<3;i++){if(ReferenceEquals(native[i].GetValue(null),layers[i]))native[i].SetValue(null,saved[i]);saved[i]=null;}Attached=false;
        }
        private void DropBatches(Exception primary)
        {
            Exception secondary=null;SpriteBatch inside=null;
            try{if(tiles!=null)inside=(SpriteBatch)tileSprites.GetValue(tiles);}catch(Exception error){secondary=error;}
            try{tiles?.Dispose();}catch(Exception error){if(secondary==null)secondary=error;}
            try{inside?.Dispose();}catch(Exception error){if(secondary==null)secondary=error;}
            try{sprites?.Dispose();}catch(Exception error){if(secondary==null)secondary=error;}
            tiles=null;sprites=null;
            if(primary==null&&secondary!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(secondary).Throw();
        }
        private static void DisposeGroup(WorldSceneLayerTarget[] group,Exception primary)
        {
            Exception failure=primary;
            for(int i=0;i<group.Length;i++)
            {var item=group[i];group[i]=null;try{item?.Dispose();}catch(Exception error){if(failure==null)failure=error;}}
            if(primary==null&&failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        private void Release(Exception primary=null)
        {
            Exception failure=primary;
            try{Detach();}catch(Exception error){if(failure==null)failure=error;}
            try{DisposeGroup(layers,null);}catch(Exception error){if(failure==null)failure=error;}
            for(int i=0;i<3;i++)valid[i]=false;
            try{DropBatches(failure);}catch(Exception error){if(failure==null)failure=error;}
            if(primary==null&&failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        private void Reset(object sender,EventArgs args){try{Release();}finally{failedDevice=null;observed=false;}}
        public void Dispose()
        {
            try{Release();}
            finally{if(device!=null){device.DeviceResetting-=Reset;device.Disposing-=Reset;}device=null;failedDevice=null;compositeFailure=null;observed=false;NativeRefreshRequested=false;}
        }
    }
}
