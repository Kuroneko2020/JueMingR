using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;

namespace JueMingR.TerrariaHost.DeathHistory
{
    // Narrow, removable adapter for the fixed vanilla map. No AddLayer list
    // ownership, input interception, shared texture disposal or per-icon batch.
    internal sealed class DeathMapHooks : IDisposable
    {
        private const string Owner = "JueMingR.DeathHistory.Map";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static DeathMapHooks current;
        private readonly HostDeathRecords host;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patched = new List<MethodInfo>();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly Func<string, float, F5Size> measure;
        private DynamicSpriteFont font;
        private int depth;
        private Map.FullscreenMapDrawing drawing;
        private string nativeId, hoveredId, textId, text;
        private F5Rect hoveredRect;
        internal bool Ready { get; private set; }
        internal string Failure { get; private set; }
#if DEBUG
        internal long Projections, Draws, Hits;
#endif
        internal DeathMapHooks(HostDeathRecords host) { this.host = host; measure = Measure; }
        internal void Install()
        {
            try
            {
                if (current != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("death-map-identity");
                current = this;
                drawing = Map.FullscreenMapDrawing.Acquire(); drawing.Begin += BeginShared; drawing.Icons += DrawShared; drawing.Overlay += TooltipShared; drawing.Completed += EndShared;
                Patch(typeof(Main), "DrawPlayerDeathMarker", new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int), typeof(int) }, null, nameof(NativeDrawn), null);

                Ready = true;
            }
            catch (Exception e) { Failure = "death-map-install: " + e.GetType().Name; Dispose(); }
        }
        private void Patch(Type type, string name, Type[] arguments, string prefix, string postfix, string finalizer)
        {
            var method = type.GetMethod(name, Flags, null, arguments, null);
            if (method == null || method.GetMethodBody() == null) throw new MissingMethodException(name);
            patched.Add(method); harmony.Patch(method, Hook(prefix), Hook(postfix), null, Hook(finalizer));
        }
        private static HarmonyMethod Hook(string method) { return method == null ? null : new HarmonyMethod(typeof(DeathMapHooks).GetMethod(method, Flags)); }
        internal bool Active { get { return Ready && host.Session >= 0 && host.Settings.Enabled && Main.mapFullscreen && !Main.gameMenu && !Main.dedServ && !Main.hideUI; } }
        private void BeginShared() { depth = 1; nativeId = null; hoveredId = null; hint.Hide(); }
        private void EndShared(Map.MapView view) { depth = 0; nativeId = null; }
        private void DrawShared(Map.MapView view)
        {
            if (!Active) return;
            try { Draw(view.Position, view.Offset, view.Zoom, view.IconScale, view.Alpha); if (hoveredId != null) view.HoverOwner = this; }
            catch (Exception e) { Failure = "death-map-draw: " + e.GetType().Name; Ready = false; hint.Hide(); }
        }
        private void TooltipShared(Map.MapView view) { if (ReferenceEquals(view.HoverOwner, this)) Tooltip(Vector2.Zero, 0); }
        private static void NativeDrawn(int i, bool __runOriginal)
        {
            var owner = current; if (owner == null || !__runOriginal || !owner.Active || owner.depth != 1 || i != Main.myPlayer) return;
            var player = Main.LocalPlayer;
            if (owner.host.NativeEventId != null && player.lastDeathTime == owner.host.NativeStamp && player.lastDeathPostion.X == owner.host.NativeX && player.lastDeathPostion.Y == owner.host.NativeY)
                owner.nativeId = owner.host.NativeEventId;
        }
        private void Draw(Vector2 position, Vector2 offset, float zoom, float scale, int alpha)
        {
            if (zoom <= 0 || scale <= 0 || Single.IsNaN(zoom) || Single.IsInfinity(zoom) || Single.IsNaN(scale) || Single.IsInfinity(scale)) return;
            Texture2D texture = TextureAssets.MapDeath?.Value; if (texture == null || texture.IsDisposed) return;
            var view = new F5Rect(0, 0, Main.screenWidth, Main.screenHeight);
            var snapshot = host.Snapshot;
            int maximum = Math.Min(host.Settings.Count, snapshot.Markers.Count);
            // Candidates are already latest-by-occurrence, including offscreen
            // points. Never fill gaps with older visible deaths. A lower current
            // preference bounds even a still-arriving larger worker snapshot.
            for (int i = 0; i < maximum; i++)
            {
                var marker = snapshot.Markers[i]; if (marker.EventId == nativeId) continue;
                var geometry = DeathMapGeometry.Project(marker.X, marker.Y, position, offset, zoom, scale, texture.Width, texture.Height);
#if DEBUG
                Projections++;
#endif
                if (!geometry.Visible(view)) continue;
                Main.spriteBatch.Draw(texture, geometry.Center, null, Color.White * (alpha / 255f), 0, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0);
#if DEBUG
                Draws++; Hits++;
#endif
                if (hoveredId == null && geometry.Contains(Main.mouseX, Main.mouseY)) { hoveredId = marker.EventId; hoveredRect = geometry.Hit; }
            }
            host.RequestHover(hoveredId);
        }
        internal void Update()
        {
            if (Active) return;
            if (hoveredId != null) { hoveredId = null; host.RequestHover(null); }
            if (textId != null) { textId = text = null; hint.Clear(); }
        }
        private F5Size Measure(string value, float scale)
        { var size = metrics.Measure(font, value); return new F5Size(size.Width * scale, size.Height * scale, size.OffsetX * scale, size.OffsetY * scale); }
        private void Tooltip(Vector2 unusedPosition, float unusedZoom)
        {
            if (!Active || depth != 1 || hoveredId == null) return;
            try
            {
                var fact = host.Snapshot.Selected; if (fact == null || fact.EventId != hoveredId) return;
                font = FontAssets.MouseText?.Value; var pixel = TextureAssets.MagicPixel?.Value; if (font == null || pixel == null || pixel.IsDisposed) return;
                if (textId != hoveredId) { textId = hoveredId; text = DeathHistoryPopup.Stamp(fact) + "\n" + JueMingR.Platform.DeathHistory.DeathReadText.Preview(fact.DisplayCause, 80); }
                // This native event is after End(), before the next Begin().
                // Reuse the shared hint layout and plain font; bracket tags in
                // player names/reasons never enter Terraria's rich-text parser.
                hint.Prepare(text, hoveredRect, new F5Rect(8, 8, Main.screenWidth - 16, Main.screenHeight - 16), font, measure);
                if (!hint.Visible) return;
                var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
                var blend = device.BlendState; var sampler = device.SamplerStates[0]; var depthState = device.DepthStencilState; var rasterizer = device.RasterizerState; var scissor = device.ScissorRectangle;
                bool began = false;
                try
                {
                    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); began = true;
                    UiSurface.Panel(batch, pixel, hint.Panel, TextureAssets.InventoryBack?.Value, Color.White, true);
                    foreach (var line in hint.Lines)
                    {
                        var location = new Vector2(hint.Panel.X + 8 + line.Rect.X - line.TextSize.OffsetX, hint.Panel.Y + 8 + line.Rect.Y - line.TextSize.OffsetY);
                        batch.DrawString(font, line.Text, location, Color.White, 0, Vector2.Zero, line.TextScale, SpriteEffects.None, 0);
                    }
                }
                finally { try { if (began) batch.End(); } finally { device.BlendState = blend; device.SamplerStates[0] = sampler; device.DepthStencilState = depthState; device.RasterizerState = rasterizer; device.ScissorRectangle = scissor; } }
            }
            catch (Exception e) { Failure = "death-map-tooltip: " + e.GetType().Name; Ready = false; hint.Hide(); }
        }
        public void Dispose()
        {
            Ready = false;
            if (drawing != null) { drawing.Begin -= BeginShared; drawing.Icons -= DrawShared; drawing.Overlay -= TooltipShared; drawing.Completed -= EndShared; drawing.Dispose(); drawing = null; }
            if (ReferenceEquals(current, this)) current = null;
            foreach (var method in patched) try { harmony.Unpatch(method, HarmonyPatchType.All, Owner); } catch (Exception e) { Failure = "death-map-unpatch: " + e.GetType().Name; }
            patched.Clear(); hint.Clear();
        }
    }
}
