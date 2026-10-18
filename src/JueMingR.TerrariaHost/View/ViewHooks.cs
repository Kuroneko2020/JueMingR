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
        private static readonly List<MethodBase> targets = new List<MethodBase>();
        internal static void Install(HostView value)
        {
            if (owner != null) throw new InvalidOperationException("View already has an owner.");
            var harmony = new Harmony(OwnerId);
            try
            {
                Apply(harmony, AccessTools.Method(typeof(Main), "UpdateViewZoomKeys"), prefix: "ZoomKeys");
                Apply(harmony, AccessTools.Method(typeof(Main), "DoDraw", new[] { typeof(GameTime) }), transpiler: "WorldDraw", finalizer: "EndWorldDraw");
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
            for (int i = 0; i < code.Count; i++)
            {
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
            if (clamps != 1 || direct != 1 || shimmer != 1 || captures != 1 || captureGuard != 1) throw new InvalidOperationException(".8 view draw shape mismatch: clamp=" + clamps + ", direct=" + direct + ", shimmer=" + shimmer + ", capture=" + captures + "/" + captureGuard);
            return code;
        }
        private static float ClampTarget(float value, float minimum, float maximum)
        { return MathHelper.Clamp(value, owner != null && owner.HasWorld && (owner.UnlockEnabled || owner.RetainsTarget) ? owner.MinimumTarget : minimum, maximum); }
        private static bool DirectDraw()
        {
            // This native choice occurs before this frame publishes its zoom.
            // Target below 1 predicts wider coverage without touching the real
            // screen dimensions, UI scale, or any server simulation rectangle.
            return Lighting.UpdateEveryFrame || owner != null && owner.RequiresDirectDraw;
        }
        private static bool PrepareSceneCapture(Terraria.Graphics.Effects.FilterManager filters)
        {
            bool active = filters.CanCapture();
            if (owner == null) return active;
            if (Expanded && Main.drawToScreen && !Main.mapFullscreen && !Main.onlyDrawFancyUI && Lighting.NotRetro && (active || Main.ForegroundSunlightEffects))
            {
                try { owner.Scene.Attach(); owner.Water.Prepare(); }
                catch (OutOfMemoryException error) { throw new ViewRenderFailure(error); }
            }
            else { owner.Scene.Dispose(); owner.Water.Dispose(); }
            return active;
        }
        private static bool SceneDirectDraw() { return Main.drawToScreen && !(owner != null && owner.Scene.Attached); }
        private static Exception EndWorldDraw(Exception __exception)
        {
            owner?.Water.Detach();
            owner?.Scene.Detach(__exception != null);
            if (!(__exception is ViewRenderFailure)) return __exception;
            // Only a typed failure from VIEW-owned preparation is contained.
            // Leave unrelated native exceptions visible. The next original
            // frame uses the intact ordinary arrays and no repeated growth.
            try { Main.spriteBatch.End(); } catch (InvalidOperationException) { }
            Main.instance.GraphicsDevice.SetRenderTarget(null);
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
            if (owner == null || !owner.NeedsExpandedRendering || !Main.drawToScreen) return;
            if (__originalMethod.DeclaringType == typeof(ScreenObstruction) && ScreenObstruction.screenObstruction == 0f ||
                __originalMethod.DeclaringType == typeof(ScreenDarkness) && ScreenDarkness.screenObstruction == 0f ||
                __originalMethod.DeclaringType == typeof(MoonlordDeathDrama) && (float)moonWhitening.GetValue(null) == 0f) return;
            __state = new ViewBackgroundScope(); background = __state;
        }
        private static Exception EndBackground(Exception __exception, ViewBackgroundScope __state, MethodBase __originalMethod)
        {
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
            if(owner.RequiresDirectDraw && Lighting.NotRetro && !Main.mapFullscreen && !Main.onlyDrawFancyUI)
            {owner.Water.Defer(__instance,gameTime);return false;}
            owner.Water.Dispose();return true;
        }
        private static bool WaterValidate() {return owner==null || !owner.Water.Running;}
        private static readonly FieldInfo imageScales=AccessTools.Field(typeof(ScreenShaderData),"_imageScales");
        private static void WaterApply(WaterShaderData __instance,out Vector2? __state)
        {
            __state=null;if(owner==null||!owner.Water.Active)return;
            var scales=(Vector2[])imageScales.GetValue(__instance);__state=scales[2];
            __instance.UseImageScale(Vector2.One/Main.GameViewMatrix.RenderZoom,2);
        }
        private static void WaterApplied(WaterShaderData __instance,Vector2? __state)
        {if(__state.HasValue)__instance.UseImageScale(__state.Value,2);}
        private static bool WaterQueueDirect() {return Main.drawToScreen && !(owner!=null && owner.RequiresDirectDraw && Lighting.NotRetro);}
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
