using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Feedback
{
    // Presentation only. No commands, input sampling, persistence or player-state
    // mirror. The existing runtime owns Session; each bounded result borrows its
    // generation and character identity only until retirement.
    internal sealed class LocalShortFeedback : IRuntimeFeature
    {
        internal sealed class Scope
        {
            internal long Generation;
            internal object Character, Player;
        }
        private sealed class Entry
        {
            internal string Id, Text, Token;
            internal bool On, Fallback;
            internal int Slot = -1;
            internal double Expires;
            internal PopupText Popup;
            internal Scope Scope;
            internal Func<bool> Valid;
            internal readonly LocalShortFeedbackRenderer Renderer = new LocalShortFeedbackRenderer();
        }
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private readonly List<Entry> entries = new List<Entry>(4);
        private FullscreenMapDrawing map;
        internal event Action Ended;
        public bool Enabled { get { return true; } }
        public void OnSessionStarted() { OnSessionEnded(); }
        public void OnSessionEnded() { Clear(); Ended?.Invoke(); }
        public void FailClosed() { OnSessionEnded(); }
        public void Update(ulong tick) { Refresh(); }
        internal int Count { get { return entries.Count; } }
#if DEBUG
        internal int Passes { get; private set; }
#endif
        internal static double Now { get { return Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency); } }
        internal LocalShortFeedback(SingleFeatureRuntime runtime, HostInputState input) { this.runtime = runtime; this.input = input; }
        internal Scope Capture()
        {
            if (!runtime.IsSessionActive || !input.CanPrepareText || Main.gameMenu || Main.netMode == 2) return null;
            return new Scope { Generation = runtime.Generation, Character = Main.ActivePlayerFileData, Player = Main.LocalPlayer };
        }
        internal bool Current(Scope scope)
        {
            return scope != null && runtime.IsSessionActive && scope.Generation == runtime.Generation && input.CanPrepareText && !Main.gameMenu &&
                ReferenceEquals(scope.Character, Main.ActivePlayerFileData) && ReferenceEquals(scope.Player, Main.LocalPlayer);
        }
        internal void Show(string id, string text, bool on, Func<bool> valid, Scope scope)
        {
            if (!Current(scope)) return;
            Remove(id);
            if (entries.Count == 4) Retire(0);
            entries.Add(new Entry { Id = id, Text = text, On = on, Valid = valid, Scope = scope, Expires = Now + 1800 });
            Refresh();
        }
        internal void Remove(string id)
        { for (int i = entries.Count - 1; i >= 0; i--) if (entries[i].Id == id) Retire(i); }
        private void Retire(int i)
        {
            var e = entries[i];
            try { NativePopupText.Release(e.Popup, e.Slot, e.Token); }
            catch { } // retiring presentation must never fail a healthy feature
            entries.RemoveAt(i);
        }
        internal void Clear()
        {
            for (int i = entries.Count - 1; i >= 0; i--) Retire(i);
            ReleaseMap();
        }
        private void ReleaseMap()
        {
            var old = map; map = null;
            if (old == null) return;
            try { old.Overlay -= DrawMap; old.Dispose(); } catch { }
        }
        internal void Refresh()
        {
            // OFF contract: before clock, font, pool, map lease or state getters.
            if (entries.Count == 0) return;
#if DEBUG
            Passes++;
#endif
            double now = Now;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                try
                {
                    if (!Current(e.Scope) || now >= e.Expires || !e.Valid()) { Retire(i); continue; }
                    Prepare(e, i);
                    if (!e.Fallback && NativePopupText.Owns(e.Popup, e.Slot, e.Token)) e.Popup.lifeTime = e.Expires - now > 250 ? 2 : 0;
                }
                catch { Retire(i); }
            }
            if (entries.Count == 0) { ReleaseMap(); return; }
            if (map == null)
            {
                try { map = FullscreenMapDrawing.Acquire(); map.Overlay += DrawMap; }
                catch { map = null; } // display failure cannot disable F5/business
            }
        }
        private static Color ColorFor(Entry e) { return e.On ? new Color(170, 245, 190) : new Color(225, 225, 225); }
        private void Prepare(Entry e, int index)
        {
            var font = FontAssets.MouseText?.Value;
            if (font == null) return;
            float width = PlayerInput.OriginalScreenSize.X, height = PlayerInput.OriginalScreenSize.Y;
            if (width < 64 || height < 64) return;
            e.Renderer.Prepare(font, e.Text, Math.Max(.5f, Main.UIScaleMatrix.M11) * .9f, width, height);
            bool native = Main.showItemText && !Main.mapFullscreen && !Main.hideUI && Main.LocalPlayer != null && !Main.LocalPlayer.dead;
            if (e.Popup != null && (!NativePopupText.Owns(e.Popup, e.Slot, e.Token) || !native || !Visible(e.Popup.position, e.Renderer.NativeSize))) e.Fallback = true;
            // Once native admission is lost, continue only the remaining lifetime
            // in the fallback. Switching maps/pools cannot restart or duplicate it.
            if (!native) e.Fallback = true;
            if (!e.Fallback && e.Popup == null)
            {
                bool inverted = Main.LocalPlayer.gravDir == -1;
                var size = e.Renderer.NativeSize;
                float offset = 24 + index * (size.Y + 8);
                Vector2 anchor = inverted ? Main.LocalPlayer.Bottom + new Vector2(0, offset + size.Y) : Main.LocalPlayer.Top - new Vector2(0, offset);
                if (!Visible(anchor - size / 2, size) || !NativePopupText.TryCreate(e.Text, ColorFor(e), 108, new Vector2(0, inverted ? 7 : -7), anchor, out e.Popup, out e.Slot, out e.Token)) e.Fallback = true;
            }
            if (e.Fallback) { NativePopupText.Release(e.Popup, e.Slot, e.Token); e.Popup = null; e.Token = null; e.Slot = -1; }
        }
        private static bool Visible(Vector2 position, Vector2 textSize)
        {
            if (!(PopupText.TargetScale > 0)) return false;
            Vector2 center = position - Main.screenPosition;
            if (Main.LocalPlayer.gravDir == -1) center.Y = Main.screenHeight - center.Y;
            center += textSize / 2;
            Vector2 half = (textSize / 2 + new Vector2(2)) * PopupText.TargetScale;
            Vector2 a = Vector2.Transform(center - half, Main.GameViewMatrix.ZoomMatrix), b = Vector2.Transform(center + half, Main.GameViewMatrix.ZoomMatrix);
            return a.X >= 0 && a.Y >= 0 && b.X <= PlayerInput.OriginalScreenSize.X && b.Y <= PlayerInput.OriginalScreenSize.Y && b.X > a.X && b.Y > a.Y;
        }
        internal void Draw()
        {
            if (entries.Count == 0 || Main.mapFullscreen) return;
            try { Refresh(); DrawFallback(Matrix.Invert(Main.UIScaleMatrix)); } catch { Clear(); }
        }
        private void DrawFallback(Matrix inverse)
        {
            if (entries.Count == 0 || Main.hideUI || FontAssets.MouseText?.Value == null) return;
            float y = Math.Min(96, PlayerInput.OriginalScreenSize.Y * .15f);
            double now = Now;
            foreach (var e in entries)
            {
                if (!e.Fallback) continue;
                float alpha = (float)Math.Min(1, Math.Max(0, (e.Expires - now) / 250));
                e.Renderer.Draw(Main.spriteBatch, new Vector2(PlayerInput.OriginalScreenSize.X / 2f, y), inverse, ColorFor(e) * alpha);
                y += e.Renderer.Height;
            }
        }
        private void DrawMap(MapView frame)
        {
            if (entries.Count == 0) return;
            try
            {
                Refresh(); if (entries.Count == 0 || !frame.MatchesScreen()) return;
                var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
                var blend = device.BlendState; var sampler = device.SamplerStates[0]; var depth = device.DepthStencilState; var rasterizer = device.RasterizerState; var scissor = device.ScissorRectangle;
                bool began = false;
                try
                {
                    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone); began = true;
                    DrawFallback(Matrix.Identity);
                }
                finally { try { if (began) batch.End(); } finally { device.BlendState = blend; device.SamplerStates[0] = sampler; device.DepthStencilState = depth; device.RasterizerState = rasterizer; device.ScissorRectangle = scissor; } }
            }
            catch { Clear(); }
        }
        internal void Discard(Player player, string text)
        {
            // Active voluntary receipts take priority over our own high-frequency
            // discard display. The actual discard has already completed.
            if (entries.Count != 0) return;
            PopupText popup; int slot; string token;
            NativePopupText.TryCreate(text, Color.White, 60, new Vector2(0, -7), player.Center, out popup, out slot, out token);
        }
    }
}
