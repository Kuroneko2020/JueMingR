using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.View
{
    // The user's target, native forced zoom and rounded render zoom have
    // different units/owners. Input and Draw use this one target contract;
    // cache admission may reject a route, never raise this lower bound.
    internal static class ViewZoom
    {
        internal const float Minimum = .3f, Maximum = 10f;
        private const float InputRatio = 1.02f;
        internal static float MinimumTarget(int width, int height, float forced)
        {
            if (!Finite(forced) || forced <= 0) forced = 1f;
            // WorldTarget's512 tiles include its92px arrow halo. Preserve the
            // tighter496-tile usable domain and a native1/128 rounding margin.
            // WorldObject's1024 domain is a separate, less restrictive contract.
            float geometry = Math.Max(width, height) / (496f * 16f) + 1f / 128f;
            return Math.Min(1f, Math.Max(Minimum, geometry / forced));
        }
        internal static float Forced(int width, int height)
        {
            return Main.SupportWideScreen ? 1f : Math.Max(1f, Math.Max((float)width / Main.MaxWorldViewSize.X, (float)height / Main.MaxWorldViewSize.Y));
        }
        internal static float Quantize(float raw)
        {return (float)((float)Math.Round((float)(raw/(1f/128f)))*(1f/128f));}
        internal static bool IsExpanded(float actual,float forced)
        {
            // ForcedMinimumZoom can exceed1 on a larger physical viewport.
            // Actual1.5 with native forced2 is wider than the ordinary world
            // view, even though it is not a sample reduction below1:1.
            return Finite(actual)&&actual>0&&actual<Quantize(Finite(forced)?Math.Max(1f,forced):1f)-.0001f;
        }
        internal static Vector2 NativeCapacity(int width,int height)
        {
            // Locked Main.InitTargets59089..59103. HiDef's ordinary world
            // group remains1920-wide unless SupportWideScreen is also true;
            // TrySupporting8K changes XNA capability, not Main's4096 limit.
            bool high=Main.instance.GraphicsDevice.GraphicsProfile==GraphicsProfile.HiDef;
            int maximum=high&&Main.SupportWideScreen?4096:Main.MaxWorldViewSize.X;
            int x=Math.Min(width,maximum),y=Math.Min(height,maximum),p=192,cap=high?4096:2048;
            if(x+2*p>cap)p=(cap-x)/2;
            return new Vector2(x+2*p,y+2*p);
        }
        internal static bool RequiresRenderer(float actual,Vector2 visible,Vector2 native,bool cachedMode)
        {
            // Semantic expansion relative to forced zoom is separate. Keep
            // legal native caching when it covers the actual world. One world
            // pixel covers the native Round(center) half-pixel on either side.
            return actual<.9999f||cachedMode&&(visible.X+1>native.X||visible.Y+1>native.Y);
        }
        internal static float Clamp(float target, bool permission, float minimum)
        {
            if (!Finite(target) || target <= 0) return 1f;
            bool retained = target < 1f || target > 2f;
            return MathHelper.Clamp(target, permission || retained ? minimum : 1f, permission || retained ? Maximum : 2f);
        }
        internal static float Step(float current, int direction, bool permission, float minimum)
        {
            if (direction == 0 || !Finite(current) || current <= 0) return current;
            // Equal relative scale travel needs equal Mapping counts, not equal
            // percentage points. Keep the unrounded native target as the only
            // accumulator: small low-end steps must cross later1/128 plateaus.
            // Multiply/divide the same float ratio; clipping deliberately ends
            // reciprocity at a bound, never creates direction/timing state.
            if (!permission && current < 1f)
            { return direction < 0 ? current : Math.Min(1f, current * InputRatio); }
            if (!permission && current > 2f)
            { return direction > 0 ? current : Math.Max(2f, current / InputRatio); }
            // Ordinary OFF is still Terraria's original two-point operation.
            float next = permission ? (direction > 0 ? current * InputRatio : current / InputRatio) : current + direction * .02f;
            return MathHelper.Clamp(next, permission ? minimum : 1f, permission ? Maximum : 2f);
        }
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    // A prediction for this Draw's native publication, not another camera.
    // The early native render choice precedes the real zoom setter. Geometry
    // predicts only that publication; the partial cache verifies the actual
    // native facts after publication before borrowing any drawing leaf.
    internal enum ViewRenderRoute { Native, Direct, TileCache }
    internal sealed class ViewRenderPlan
    {
        internal readonly int Width, Height;
        internal readonly float ForcedZoom, Target, RawZoom, RenderZoom;
        internal readonly Vector2 Translation, WorldSize, NativeSize;
        internal readonly ViewRenderRoute Route;
        internal bool Expanded { get { return ViewZoom.IsExpanded(RenderZoom,ForcedZoom); } }
        internal bool Wide { get { return ViewZoom.RequiresRenderer(RenderZoom,WorldSize,NativeSize,Lighting.NotRetro); } }
        internal ViewRenderPlan(float target, bool permission)
        {
            Width = Main.screenWidth; Height = Main.screenHeight;
            ForcedZoom = ViewZoom.Forced(Width, Height);
            Target = ViewZoom.Clamp(target, permission, ViewZoom.MinimumTarget(Width, Height, ForcedZoom));
            RawZoom = (float)(ForcedZoom * Target);
            // Match SpriteViewMatrix's float32 division and Utils.Round's
            // default ToEven; decimal percentages are not matrix values.
            RenderZoom = ViewZoom.Quantize(RawZoom);
            float halfWidth = Width * .5f, halfHeight = Height * .5f;
            Translation = new Vector2((float)Math.Round((float)(halfWidth - halfWidth / RenderZoom)), (float)Math.Round((float)(halfHeight - halfHeight / RenderZoom)));
            WorldSize = new Vector2(Width, Height) - Translation * 2f;
            NativeSize=ViewZoom.NativeCapacity(Width,Height);
            Route=Wide?ViewRenderRoute.Direct:ViewRenderRoute.Native;
        }
        private ViewRenderPlan(ViewRenderPlan source,ViewRenderRoute route)
        {Width=source.Width;Height=source.Height;ForcedZoom=source.ForcedZoom;Target=source.Target;RawZoom=source.RawZoom;RenderZoom=source.RenderZoom;Translation=source.Translation;WorldSize=source.WorldSize;NativeSize=source.NativeSize;Route=route;}
        internal ViewRenderPlan WithRoute(ViewRenderRoute route){return Route==route?this:new ViewRenderPlan(this,route);}
        internal bool MatchesRequest(float target,bool permission)
        {
            float forced=ViewZoom.Forced(Main.screenWidth,Main.screenHeight);
            return Width==Main.screenWidth&&Height==Main.screenHeight&&ForcedZoom==forced&&NativeSize==ViewZoom.NativeCapacity(Width,Height)&&Target==ViewZoom.Clamp(target,permission,ViewZoom.MinimumTarget(Main.screenWidth,Main.screenHeight,forced));
        }
        internal bool MatchesNative()
        {
            return Main.screenWidth == Width && Main.screenHeight == Height && Main.GameViewMatrix.RenderZoom == new Vector2(RenderZoom) && Main.GameViewMatrix.Translation == Translation;
        }
    }
}
