using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Light;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Liquid;
using Terraria.GameContent.Events;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Shaders;

namespace JueMingR.TerrariaHost.View
{
    internal static class ViewHooks
    {
        private const string OwnerId = "JueMingR.View.Native1458";
        private static HostView owner;
        internal static bool Expanded { get { return owner != null && owner.NeedsExpandedRendering; } }
        [ThreadStatic] private static ViewBackgroundScope background;
        [ThreadStatic] private static Exception ownedDrawFailure;
        [ThreadStatic] private static int drawDepth;
        [ThreadStatic] private static RenderTargetBinding[] drawBindings;
        private struct OriginalDrawState { internal bool Guard;internal Exception PriorFailure;internal RenderTargetBinding[] PriorBindings; }
        private static readonly FieldInfo drawGuard=AccessTools.Field(typeof(Main),"_isDrawingOrUpdating");
        private static readonly List<MethodBase> targets = new List<MethodBase>();
        internal static void Install(HostView value)
        {
            if (owner != null) throw new InvalidOperationException("View already has an owner.");
            var harmony = new Harmony(OwnerId);
            try
            {
                Apply(harmony, AccessTools.Method(typeof(Main), "UpdateViewZoomKeys"), prefix: "ZoomKeys");
                Apply(harmony, AccessTools.Method(typeof(IngameOptions), "Draw"), transpiler: "Display", patchType: typeof(ViewOptions));
                foreach(string method in new[]{"DoDraw_Tiles_Solid","DoDraw_Tiles_NonSolid","DoDraw_WallsAndBlacks"})
                    Apply(harmony,AccessTools.Method(typeof(Main),method),transpiler:"TileComposite");
                foreach(string method in new[]{"DrawTiles","DrawWalls","DrawBlack"})
                    Apply(harmony,AccessTools.Method(typeof(Main),method),transpiler:"TileLeaf");
                Apply(harmony,AccessTools.Method(typeof(TileDrawingBase),"Begin"),transpiler:"TileLeafSampler");
                Apply(harmony,AccessTools.Method(typeof(TileBatch),"GetNewSpriteBufferSlice"),finalizer:"CacheLeafFailure");
                Apply(harmony,AccessTools.Method(typeof(TileBatch),"End"),finalizer:"CacheLeafFailure");
                Apply(harmony,AccessTools.Method(typeof(Main),"RenderToTargets"),prefix:"BeginNativeCoverage",finalizer:"EndNativeCoverage");
                foreach(string leaf in new[]{"DrawTiles","DrawWalls","DrawBlack","DrawWaters"})Apply(harmony,AccessTools.Method(typeof(Main),leaf),finalizer:"CacheLeafFailure");
                Apply(harmony,AccessTools.Method(typeof(Main),"ApplyPendingBackgroundTargetSwap"),prefix:"BackgroundSwap");
                Apply(harmony, AccessTools.Method(typeof(Main), "DoDraw", new[] { typeof(GameTime) }), transpiler: "WorldDraw", finalizer: "EndWorldDraw");
                Apply(harmony, AccessTools.Method(typeof(Main), "Draw", new[] { typeof(GameTime) }),prefix:"BeginOriginalDraw",finalizer:"EndOriginalDraw");
                Apply(harmony, AccessTools.Method(typeof(Main), "DrawNPCs", new[] { typeof(bool) }), transpiler: "Expand", patchType: typeof(ViewNpcCulling));
                Apply(harmony, AccessTools.Method(typeof(Main), "GetAreaToLight"), postfix: "LightArea");
                Apply(harmony, AccessTools.Method(typeof(Main), "DrawBackground"), prefix: "BeginBackground", transpiler: "BackgroundLayer", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(Main), "OldDrawBackground"), transpiler: "BackgroundLayer");
                Apply(harmony, AccessTools.Method(typeof(Main), "DrawBackground_DirtBackground"), transpiler: "BackgroundLayer");
                Apply(harmony, AccessTools.Method(typeof(ScreenObstruction), "Draw"), prefix: "BeginBackground", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(ScreenDarkness), "DrawBack"), prefix: "BeginBackground", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(ScreenDarkness), "DrawFront"), prefix: "BeginBackground", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(MoonlordDeathDrama), "DrawWhite"), prefix: "BeginBackground", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(Main), "DrawBackgroundBlackFill"), prefix: "BeginBackground", finalizer: "EndBackground");
                Apply(harmony, AccessTools.Method(typeof(Main), "DrawWaters"), prefix: "PrepareLiquids");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "PreDraw"), prefix: "WaterPreDraw");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "ValidateRenderTargets"), prefix: "WaterValidate");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "InitRenderTargets"), prefix: "WaterNativeTargets");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "ReleaseRenderTargets"), prefix: "WaterNativeTargets");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "Apply"), prefix: "WaterApply", finalizer: "WaterApplied");
                Apply(harmony, AccessTools.Method(typeof(WaterShaderData), "QueueRipple", new[]{typeof(Vector2),typeof(Color),typeof(Vector2),typeof(RippleShape),typeof(float)}), transpiler: "WaterQueue");
                Apply(harmony, AccessTools.Method(typeof(Main), "oldDrawWater"), prefix: "WaterBounds", transpiler: "OldWaterArea");
                Apply(harmony, AccessTools.Method(typeof(WaterfallManager), "FindWaterfalls"), prefix: "WaterBounds", transpiler: "OldWaterArea");
                Apply(harmony, AccessTools.Method(typeof(WaterfallManager), "DrawWaterfall", new[] { typeof(int), typeof(float) }), prefix: "WaterBounds", transpiler: "OldWaterArea");
                foreach (string method in new[] { "Rebuild", "AddLight", "ProcessArea", "Clear", "PreRenderPhase" })
                    Apply(harmony, AccessTools.Method(typeof(LegacyLighting), method), transpiler: "LegacyArea");
                Apply(harmony, AccessTools.Method(typeof(LegacyLighting), "ProcessArea"), prefix: "EnsureLegacyCapacity");
                // Publish only when the complete version-bound minimum is in
                // place. A failed transpiler must never admit a wider target.
                owner = value;
            }
            catch { foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, OwnerId); targets.Clear(); throw; }
        }
        internal static void Uninstall(HostView value)
        {
            if (!ReferenceEquals(owner, value)) return;
            owner = null; var harmony = new Harmony(OwnerId);
            foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, OwnerId);
            targets.Clear();
        }
        private static void Apply(Harmony harmony, MethodInfo target, string prefix = null, string postfix = null, string transpiler = null, string finalizer = null, Type patchType = null)
        {
            if (target == null) throw new MissingMethodException("Required .8 view method.");
            targets.Add(target);
            harmony.Patch(target, prefix: prefix == null ? null : new HarmonyMethod(typeof(ViewHooks), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(ViewHooks), postfix),
                transpiler: transpiler == null ? null : new HarmonyMethod(patchType ?? typeof(ViewHooks), transpiler),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(ViewHooks), finalizer));
        }
        private static bool ZoomKeys()
        {
            if (owner == null || !owner.HasWorld) return true;
            owner.ConsumeZoom(); return false;
        }
        private static IEnumerable<CodeInstruction> WorldDraw(IEnumerable<CodeInstruction> source)
        {
            var code = new List<CodeInstruction>(source);
            var zoom = AccessTools.Field(typeof(Main), "GameZoomTarget");
            var clamp = AccessTools.Method(typeof(MathHelper), "Clamp", new[] { typeof(float), typeof(float), typeof(float) });
            var update = AccessTools.PropertyGetter(typeof(Lighting), "UpdateEveryFrame");
            int clamps = 0, direct = 0, shimmer = 0, captures = 0, captureGuard = 0;
            var draw = AccessTools.Method(typeof(SpriteBatch), "Draw", new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float) });
            var unscaled = AccessTools.PropertyGetter(typeof(Camera), "UnscaledSize");
            var canCapture = AccessTools.Method(typeof(Terraria.Graphics.Effects.FilterManager), "CanCapture");
            var directField = AccessTools.Field(typeof(Main), "drawToScreen");
            var worldLayers=AccessTools.Method(typeof(Main),"DoDraw_WallsTilesNPCs");int cachedErrors=0,displays=0;
            var display=AccessTools.Method(typeof(Main),"UpdateDisplaySettings");int worldReturns=0;
            var debugWorld=AccessTools.Field(typeof(Terraria.Testing.DebugVisualizer),"World");
            for (int i = 0; i < code.Count; i++)
            {
                if(code[i].LoadsField(debugWorld)){code.Insert(i,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ViewHooks),"ReturnCachedWorld")));i++;worldReturns++;}
                if(code[i].Calls(worldLayers)){code.Insert(i+1,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ViewHooks),"CacheDrawComplete")));cachedErrors++;}
                if(code[i].Calls(display)){code.Insert(i+1,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ViewHooks),"DisplayReady")));displays++;}
                if (i >= 3 && code[i].Calls(clamp) && code[i - 3].LoadsField(zoom) && code[i - 2].opcode == OpCodes.Ldc_R4 && Equals(code[i - 2].operand, 1f) && code[i - 1].opcode == OpCodes.Ldc_R4 && Equals(code[i - 1].operand, 2f))
                { code[i].operand = AccessTools.Method(typeof(ViewHooks), "ClampTarget"); clamps++; }
                if (code[i].Calls(update)) { code[i].operand = AccessTools.Method(typeof(ViewHooks), "DirectDraw"); direct++; }
                if (code[i].Calls(canCapture))
                {
                    code[i].opcode = OpCodes.Call; code[i].operand = AccessTools.Method(typeof(ViewHooks), "PrepareSceneCapture"); captures++;
                    for (int p = i + 1; p < Math.Min(code.Count, i + 18); p++)
                        if (code[p].LoadsField(directField)) { code[p].opcode = OpCodes.Call; code[p].operand = AccessTools.Method(typeof(ViewHooks), "SceneDirectDraw"); captureGuard++; break; }
                }
                if (code[i].Calls(draw))
                {
                    int sizeReads = 0;
                    for (int p = Math.Max(0, i - 45); p < i; p++) if (code[p].Calls(unscaled)) sizeReads++;
                    if (sizeReads == 2) { code[i].opcode = OpCodes.Call; code[i].operand = AccessTools.Method(typeof(ViewHooks), "ShimmerFill"); shimmer++; }
                }
            }
            if (clamps != 1 || direct != 1 || shimmer != 1 || captures != 1 || captureGuard != 1 || cachedErrors!=1||displays!=1||worldReturns!=1) throw new InvalidOperationException(".8 view draw shape mismatch: clamp=" + clamps + ", direct=" + direct + ", shimmer=" + shimmer + ", capture=" + captures + "/" + captureGuard+",cache="+cachedErrors+",display="+displays+",worldReturn="+worldReturns);
            return code;
        }
        private static float ClampTarget(float value, float minimum, float maximum)
        { return owner != null && owner.HasWorld ? ViewZoom.Clamp(value, owner.UnlockEnabled, owner.MinimumTarget) : MathHelper.Clamp(value, minimum, maximum); }
        private static IEnumerable<CodeInstruction> TileLeaf(IEnumerable<CodeInstruction> source)
        {
            var identity=AccessTools.PropertyGetter(typeof(Matrix),"Identity");var raster=AccessTools.Field(typeof(RasterizerState),"CullCounterClockwise");int count=0,rasters=0;
            foreach(var item in source){if(item.Calls(identity)){item.operand=AccessTools.Method(typeof(ViewHooks),"LeafMatrix");count++;}if(item.LoadsField(raster)){item.opcode=OpCodes.Call;item.operand=AccessTools.Method(typeof(ViewHooks),"LeafRaster");rasters++;}yield return item;}
            if(count!=1||rasters!=1)throw new InvalidOperationException(".8 cached leaf Identity/raster shape="+count+"/"+rasters);
        }
        private static Matrix LeafMatrix(){return owner!=null&&owner.Cache.Rendering?owner.Cache.LeafMatrix:Matrix.Identity;}
        private static RasterizerState LeafRaster(){return owner!=null&&owner.Cache.Rendering&&owner.Cache.Inverted?RasterizerState.CullClockwise:RasterizerState.CullCounterClockwise;}
        private static IEnumerable<CodeInstruction> TileLeafSampler(IEnumerable<CodeInstruction> source)
        {
            var sampler=AccessTools.PropertyGetter(typeof(Main),"DefaultSamplerState");int count=0;
            foreach(var item in source){if(item.Calls(sampler)){item.operand=AccessTools.Method(typeof(ViewHooks),"LeafSampler");count++;}yield return item;}
            if(count!=1)throw new InvalidOperationException(".8 tile leaf sampler shape="+count);
        }
        private static SamplerState LeafSampler(){return owner!=null&&owner.Cache.Rendering?SamplerState.LinearClamp:Main.DefaultSamplerState;}
        private static IEnumerable<CodeInstruction> TileComposite(IEnumerable<CodeInstruction> source)
        {
            var direct=AccessTools.Field(typeof(Main),"drawToScreen");
            var draw=AccessTools.Method(typeof(SpriteBatch),"Draw",new[]{typeof(Texture2D),typeof(Vector2),typeof(Color)});int choices=0,draws=0;
            foreach(var item in source)
            {
                if(item.LoadsField(direct)){item.opcode=OpCodes.Call;item.operand=AccessTools.Method(typeof(ViewHooks),"TileDirect");choices++;}
                if(item.Calls(draw)){item.opcode=OpCodes.Call;item.operand=AccessTools.Method(typeof(ViewHooks),"DrawTileCache");draws++;}
                yield return item;
            }
            if(choices!=1||draws!=1)throw new InvalidOperationException(".8 tile composite shape="+choices+"/"+draws);
        }
        private static bool TileDirect(){return Main.drawToScreen&&!(owner!=null&&owner.Cache.Attached);}
        private static void DrawTileCache(SpriteBatch batch,Texture2D texture,Vector2 position,Color color)
        {try{if(owner!=null)owner.Cache.Composite(batch,texture,position,color);else batch.Draw(texture,position,color);}catch(Exception error){owner?.Cache.NoteCompositeFailure(error);throw;}}
        private static Exception CacheLeafFailure(Exception __exception){owner?.Cache.NoteLeafFailure(__exception);owner?.Native.NoteFailure(__exception);return __exception;}
        private static bool BeginNativeCoverage(out bool __state)
        {
            __state=false;
            try
            {
                if(owner!=null&&owner.Plan!=null&&!owner.Plan.Expanded)owner.Native.ExitOrdinary();
                if(owner==null||owner.Plan==null||!owner.Plan.Expanded||Main.drawToScreen||!Lighting.NotRetro)return true;
                if(owner.Native.Begin(owner.Plan)){__state=true;return true;}
            }
            catch(Exception error){MarkDrawFailure(error);throw;}
            // No native liquid/tile leaf has run yet. An optional private-batch
            // admission failure can use correct Direct in this same Draw.
            Main.drawToScreen=true;owner.Plan=owner.Plan.WithRoute(ViewRenderRoute.Direct);return false;
        }
        private static Exception EndNativeCoverage(Exception __exception,bool __state)
        {if(!__state)return __exception;var error=owner.Native.End(__exception,owner.Plan);if(error is OutOfMemoryException)error=new ViewOptionalCacheFailure((OutOfMemoryException)error);if(error!=null)MarkDrawFailure(error);return error;}
        // The partial cache owns only tile/tile2/wall. RenderWallsAndBlacks
        // normally consumes a background swap; preserve that separate native
        // owner while borrowing its tile leaf in our temporary logical domain.
        private static bool BackgroundSwap(){return owner==null||!owner.Cache.Rendering;}
        private static void CacheDrawComplete(){try{owner?.Cache.ThrowCompositeFailure(true);}catch(Exception error){MarkDrawFailure(error);throw;}}
        private static void MarkDrawFailure(Exception error){if(drawDepth>0)ownedDrawFailure=error;}
        private static void BeginOriginalDraw(Main __instance,out OriginalDrawState __state)
        {
            __state=new OriginalDrawState{Guard=(bool)drawGuard.GetValue(__instance),PriorFailure=ownedDrawFailure,PriorBindings=drawBindings};drawDepth++;ownedDrawFailure=null;drawBindings=null;
            // Only a potentially owned expanded frame needs this abort return.
            // Stable ordinary Draw does not allocate a target snapshot. A
            // native-guard-blocked reentry borrows neither GPU targets nor work.
            if(!__state.Guard&&owner!=null&&owner.HasWorld&&(Main.GameZoomTarget<1||owner.NeedsExpandedRendering||owner.Plan!=null&&owner.Plan.Wide))drawBindings=__instance.GraphicsDevice.GetRenderTargets();
        }
        private static void ReturnDrawTargets(){if(drawBindings!=null)Main.instance.GraphicsDevice.SetRenderTargets(drawBindings);else Main.instance.GraphicsDevice.SetRenderTarget(null);}
        private static Exception EndOriginalDraw(Main __instance,Exception __exception,OriginalDrawState __state)
        {
            // Locked Main.Draw has no finally around DoDraw. Only an exact
            // first exception observed in these new owned scopes closes its
            // entered Draw guard/timing interval; unrelated native exceptions
            // retain their original behavior and are never swallowed here.
            try
            {
                if(!__state.Guard&&__exception!=null&&ReferenceEquals(__exception,ownedDrawFailure))
                {drawGuard.SetValue(__instance,__state.Guard);try{ReturnDrawTargets();}catch{}try{Terraria.Testing.DetailedFPS.End();}catch{}}
                return __exception;
            }
            finally
            {
                // A legitimate reentrant Draw is blocked by the native true
                // guard. It must return that exact prior value and outer marker,
                // not erase the failing outer frame's exception ownership.
                ownedDrawFailure=__state.PriorFailure;drawBindings=__state.PriorBindings;drawDepth--;
            }
        }
        private static bool DirectDraw()
        {
            // This native choice precedes this Draw's zoom publication. The
            // narrow plan predicts that publication without changing the UI,
            // physical dimensions, original target or simulation rectangle.
            if (owner != null && owner.HasWorld){owner.Water.BeginFrame();owner.Cache.NativeRefreshRequested=Main.renderNow;if(owner.Plan==null||!owner.Plan.MatchesRequest(Main.GameZoomTarget,owner.UnlockEnabled))owner.Plan=new ViewRenderPlan(Main.GameZoomTarget,owner.UnlockEnabled);else owner.Plan=owner.Plan.WithRoute(owner.Plan.Wide?ViewRenderRoute.Direct:ViewRenderRoute.Native);if(owner.Plan.Expanded&&owner.Native.Rejected(owner.Plan))owner.Plan=owner.Plan.WithRoute(ViewRenderRoute.Direct);if(owner.Plan.Route==ViewRenderRoute.Direct&&Lighting.NotRetro)owner.Water.AdoptNative();}
            else if (owner != null) {owner.Plan = null;owner.Cache.NativeRefreshRequested=false;}
            return Lighting.UpdateEveryFrame || owner != null && owner.Plan != null && owner.Plan.Route!=ViewRenderRoute.Native;
        }
        private static void DisplayReady()
        {
            if(owner==null||!owner.HasWorld)return;
            if(owner.Plan==null||!owner.Plan.MatchesRequest(Main.GameZoomTarget,owner.UnlockEnabled))owner.Plan=new ViewRenderPlan(Main.GameZoomTarget,owner.UnlockEnabled);
            if(owner.Plan.Wide||owner.Plan.Expanded&&owner.Native.Rejected(owner.Plan))Main.drawToScreen=true;
            // The early native choice may already have released its group.
            // Keep Direct for this one changed-display Draw, never sample an
            // absent group; the next Draw re-evaluates the native route.
            if(Main.drawToScreen)owner.Plan=owner.Plan.WithRoute(ViewRenderRoute.Direct);
        }
        private static bool PrepareSceneCapture(Terraria.Graphics.Effects.FilterManager filters)
        {
            bool active = filters.CanCapture();
            if (owner == null) return active;
            try
            {
            owner.Cache.Prepare(owner.Plan);
            if(owner.Plan!=null)owner.Plan=owner.Plan.WithRoute(Main.drawToScreen?(owner.Cache.Attached?ViewRenderRoute.TileCache:ViewRenderRoute.Direct):ViewRenderRoute.Native);
            if (owner.Plan!=null && owner.Plan.Route!=ViewRenderRoute.Native && !Main.mapFullscreen && !Main.onlyDrawFancyUI && Lighting.NotRetro)
            {
                try { if(active || Main.ForegroundSunlightEffects)owner.Scene.Attach();else owner.Scene.Dispose(); owner.Water.Prepare(); }
                catch (OutOfMemoryException error) { throw new ViewRenderFailure(error); }
            }
            else { owner.Scene.Dispose();if(owner.Plan!=null&&owner.Plan.Expanded&&!Main.mapFullscreen&&!Main.onlyDrawFancyUI&&Lighting.NotRetro)owner.Water.PrepareNative();else owner.Water.Dispose(); }
            owner.Cache.BeginWorld();
            if(owner.Plan!=null&&owner.Plan.Route==ViewRenderRoute.TileCache&&!owner.Cache.Attached)owner.Plan=owner.Plan.WithRoute(ViewRenderRoute.Direct);
            return active;
            }
            catch(Exception error)
            {
                // A Fill or owned mask can fail before composition. Its own
                // scope has already returned the drawing domain/batches; also
                // identify this precise exception for Main.Draw's guard finally.
                MarkDrawFailure(error);throw;
            }
        }
        private static bool SceneDirectDraw() { return Main.drawToScreen && !(owner != null && owner.Scene.Attached); }
        private static void ReturnCachedWorld(){owner?.Cache.EndWorld(null);}
        private static Exception EndWorldDraw(Exception __exception)
        {
            if(owner!=null)owner.Cache.NativeRefreshRequested=false;
            if(owner!=null){bool copied=owner.Cache.HasCompositeFailure;__exception=owner.Cache.TakeCompositeFailure(__exception);if(copied)MarkDrawFailure(__exception);}
            owner?.Cache.EndWorld(__exception);
            owner?.Cache.Detach();
            owner?.Water.Detach();
            owner?.Scene.Detach(__exception != null);
            if(__exception is ViewOptionalCacheFailure)
            {
                // The private caches already retired/restored their own state
                // and remembered the failed qualification. Do not redraw a
                // partially submitted frame (water alpha/steps are not a retry
                // transaction), reset the user's target, or leak this internal
                // control exception into ordinary Game.Run. Next Draw is Direct.
                ReturnDrawTargets();Main.renderNow=true;return null;
            }
            if (!(__exception is ViewRenderFailure)) return __exception;
            // Only a typed failure from VIEW-owned preparation is contained.
            // Leave unrelated native exceptions visible. The next original
            // frame uses the intact ordinary arrays and no repeated growth.
            try { Main.spriteBatch.End(); } catch (InvalidOperationException) { }
            ReturnDrawTargets();
            owner?.RenderingFailed(); return null;
        }
        private static void LightArea(ref Rectangle __result)
        {
            if (owner == null || !owner.NeedsExpandedRendering) return;
            Vector2 p = Main.Camera.ScaledPosition, size = Main.Camera.ScaledSize;
            int left = Math.Max(0, (int)Math.Floor(p.X / 16f) - 1), top = Math.Max(0, (int)Math.Floor(p.Y / 16f) - 1);
            int right = Math.Min(Main.maxTilesX, (int)Math.Floor((p.X + size.X) / 16f) + 2), bottom = Math.Min(Main.maxTilesY, (int)Math.Floor((p.Y + size.Y) / 16f) + 2);
            __result = new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
        private static IEnumerable<CodeInstruction> LegacyArea(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int count = 0;
            int expected = __originalMethod.Name == "PreRenderPhase" ? 5 : 2;
            MethodInfo size = AccessTools.PropertyGetter(typeof(Camera), "UnscaledSize");
            MethodInfo replacement = AccessTools.Method(typeof(ViewHooks), __originalMethod.Name == "Rebuild" ? "LegacyCapacity" : "LegacySize");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(size)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; count++; }
                yield return instruction;
            }
            if (count != expected) throw new InvalidOperationException(".8 old lighting size shape: " + __originalMethod.Name + "=" + count);
        }
        private static Vector2 LegacySize(Camera camera)
        { return owner != null && owner.NeedsExpandedRendering ? camera.ScaledSize : camera.UnscaledSize; }
        private static Vector2 LegacyCapacity(Camera camera)
        {
            Vector2 size = LegacySize(camera);
            // Rebuild occurs only on capacity growth/mode/device changes. A
            // 256-world-pixel bucket avoids rebuilding for every .02 key step.
            if (owner != null && owner.NeedsExpandedRendering)
                return new Vector2((float)Math.Ceiling(size.X / 256f) * 256f, (float)Math.Ceiling(size.Y / 256f) * 256f);
            return size;
        }
        private static void EnsureLegacyCapacity(LegacyLighting __instance)
        { ViewLegacyCapacity.Ensure(__instance); }
        private static readonly FieldInfo moonWhitening = AccessTools.Field(typeof(MoonlordDeathDrama), "whitening");
        private static void BeginBackground(MethodBase __originalMethod, out ViewBackgroundScope __state)
        {
            __state = null;
            if (owner == null || owner.Cache.Rendering || !owner.NeedsExpandedRendering || !Main.drawToScreen) return;
            if (__originalMethod.DeclaringType == typeof(ScreenObstruction) && ScreenObstruction.screenObstruction == 0f ||
                __originalMethod.DeclaringType == typeof(ScreenDarkness) && ScreenDarkness.screenObstruction == 0f ||
                __originalMethod.DeclaringType == typeof(MoonlordDeathDrama) && (float)moonWhitening.GetValue(null) == 0f) return;
            __state = new ViewBackgroundScope(); background = __state;
        }
        private static Exception EndBackground(Exception __exception, ViewBackgroundScope __state, MethodBase __originalMethod)
        {
            owner?.Native.NoteFailure(__exception);
            try
            {
                // Native cave tiles normally flush before returning. If a
                // layer throws after queueing, finish that owned partial batch
                // before restoring its coordinate domain; the next frame must
                // not inherit sprites transformed for the failed layer.
                if (__state != null && __exception != null && __originalMethod.DeclaringType == typeof(Main) && __originalMethod.Name == "DrawBackground")
                    Main.tileBatch.End();
            }
            finally
            {
                try { __state?.Dispose(); }
                finally { if (__state != null) background = null; }
            }
            return __state != null && __exception is OutOfMemoryException ? new ViewRenderFailure(__exception) : __exception;
        }
        private static IEnumerable<CodeInstruction> BackgroundLayer(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int count = 0, positions = 0; var begin = AccessTools.Method(typeof(TileBatch), "Begin", Type.EmptyTypes);
            var position = AccessTools.PropertyGetter(typeof(Camera), "ScaledPosition");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(begin)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ViewHooks), "BeginBackgroundTiles"); count++; }
                // The enclosing scope already substituted the scaled origin.
                // These two native leaves mix that origin with a Camera getter;
                // adding its translation again would shift layer transitions.
                if (instruction.Calls(position)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ViewHooks), "BackgroundPosition"); positions++; }
                yield return instruction;
            }
            int expectedBatch = __originalMethod.Name == "DrawBackground_DirtBackground" ? 0 : 1;
            int expectedPosition = __originalMethod.Name == "DrawBackground" ? 0 : 1;
            if (count != expectedBatch || positions != expectedPosition) throw new InvalidOperationException(".8 background layer shape=" + __originalMethod.Name);
        }
        private static Vector2 BackgroundPosition(Camera camera) { return background != null ? Main.screenPosition : camera.ScaledPosition; }
        private static void BeginBackgroundTiles(TileBatch batch)
        { if (background != null) batch.Begin(Main.Rasterizer, background.Matrix); else batch.Begin(); }
        private static bool PrepareLiquids(bool isBackground)
        {
            if(owner != null && !owner.Water.DrawForeground(isBackground))return false;
            if (!isBackground || owner == null || !owner.NeedsExpandedRendering || !Main.drawToScreen || !Lighting.NotRetro || owner.Water.Active) return true;
            TileDrawing.GetScreenDrawArea(false, out var _, out int left, out int right, out int top, out int bottom);
            LiquidRenderer.Instance.PrepareDraw(new Rectangle(left, top, right - left, bottom - top));return true;
        }
        private static bool WaterPreDraw(WaterShaderData __instance,GameTime gameTime)
        {
            if(owner==null)return true;
            if(owner.Water.Running)return true;
            if(owner.Plan!=null && (owner.Plan.Expanded||owner.Plan.Route!=ViewRenderRoute.Native) && Lighting.NotRetro && !Main.mapFullscreen && !Main.onlyDrawFancyUI)
            {owner.Water.AdoptNative();owner.Water.Defer(__instance,gameTime);return false;}
            owner.Water.Dispose();owner.Water.NativePreDraw();return true;
        }
        private static bool WaterValidate() {return owner==null || !owner.Water.Running;}
        private static bool WaterNativeTargets(WaterShaderData __instance){return owner==null||!owner.Water.OwnsBuffers(__instance);}
        private static readonly FieldInfo imageScales=AccessTools.Field(typeof(ScreenShaderData),"_imageScales");
        private static void WaterApply(WaterShaderData __instance,out Vector2? __state)
        {
            __state=null;if(owner==null||!owner.Water.Active)return;
            var scales=(Vector2[])imageScales.GetValue(__instance);__state=scales[2];
            __instance.UseImageScale(Vector2.One/Main.GameViewMatrix.RenderZoom,2);
        }
        private static void WaterApplied(WaterShaderData __instance,Vector2? __state)
        {if(__state.HasValue)__instance.UseImageScale(__state.Value,2);}
        private static bool WaterQueueDirect() {return Main.drawToScreen && !(owner!=null && owner.NeedsExpandedRendering && Lighting.NotRetro);}
        private static IEnumerable<CodeInstruction> WaterQueue(IEnumerable<CodeInstruction> source)
        {
            int count=0;var field=AccessTools.Field(typeof(Main),"drawToScreen");
            foreach(var instruction in source)
            {if(instruction.LoadsField(field)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ViewHooks),"WaterQueueDirect");count++;}yield return instruction;}
            if(count!=1)throw new InvalidOperationException(".8 water queue direct guard mismatch.");
        }
        [ThreadStatic] private static Vector2 waterPosition;
        private static void WaterBounds()
        { waterPosition = owner != null && owner.NeedsExpandedRendering ? Main.Camera.ScaledPosition : Main.screenPosition; }
        private static int WaterWidth() { return (int)Math.Ceiling(owner != null && owner.NeedsExpandedRendering ? Main.Camera.ScaledSize.X : Main.screenWidth); }
        private static int WaterHeight() { return (int)Math.Ceiling(owner != null && owner.NeedsExpandedRendering ? Main.Camera.ScaledSize.Y : Main.screenHeight); }
        private static IEnumerable<CodeInstruction> OldWaterArea(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int positions = 0, widths = 0, heights = 0;
            bool waterfallDraw = __originalMethod.DeclaringType == typeof(WaterfallManager) && __originalMethod.Name == "DrawWaterfall";
            int positionCount = waterfallDraw ? 3 : 4;
            FieldInfo p = AccessTools.Field(typeof(Main), "screenPosition"), w = AccessTools.Field(typeof(Main), "screenWidth"), h = AccessTools.Field(typeof(Main), "screenHeight");
            foreach (var instruction in instructions)
            {
                // The first four address reads are precisely the native X/X/Y/Y
                // tile bounds. Later world-to-draw subtraction stays untouched.
                if (positions < positionCount && instruction.LoadsField(p, true)) { instruction.operand = AccessTools.Field(typeof(ViewHooks), "waterPosition"); positions++; }
                if (widths == 0 && instruction.LoadsField(w)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ViewHooks), "WaterWidth"); widths++; }
                if (!waterfallDraw && heights == 0 && instruction.LoadsField(h)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ViewHooks), "WaterHeight"); heights++; }
                yield return instruction;
            }
            if (positions != positionCount || widths != 1 || heights != (waterfallDraw ? 0 : 1)) throw new InvalidOperationException(".8 water tile bounds mismatch: " + __originalMethod.Name);
        }
        private static void ShimmerFill(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle? source, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float depth)
        {
            if (owner != null && owner.NeedsExpandedRendering)
            { position = Main.Camera.ScaledPosition - Main.screenPosition; scale = Main.Camera.ScaledSize + new Vector2(Main.offScreenRange * 2); }
            batch.Draw(texture, position, source, color, rotation, origin, scale, effects, depth);
        }
    }
}
