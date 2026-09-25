using System;
using System.Diagnostics;
using JueMingR.Features.Onboarding;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
namespace JueMingR.TerrariaHost.Onboarding
{
    internal sealed class HostOnboarding : IRuntimeFeature
    {
        internal readonly OnboardingState State;
        private object player;
        private string path;
        private string lostPath;
        private bool lostCloud;
        private bool cloud, active, failed;
        private long admission;
        private DynamicSpriteFont font;
        private Vector2 textSize;
        private PopupText popup;
        private int slot = -1;
        private int fadeTicks;
        internal Func<bool> CanPresent;
        internal Func<F5Rect, bool> Overlaps;
        private const string Prompt = "按 F5 打开决明R";
        public bool Enabled { get { return !failed; } }
        internal HostOnboarding(string gameDirectory)
        {
            State = new OnboardingState(key => new AtomicFileDocument(System.IO.Path.Combine(gameDirectory, "JueMingRData", "behavior", "onboarding", key + ".json"), OnboardingMarkerCodec.MaximumBytes, true));
            AppDomain.CurrentDomain.ProcessExit += Exit;
        }
        public void OnSessionStarted()
        { ReleasePopup(); active = true; player = Main.LocalPlayer; path = lostPath = null; State.Begin(++admission); }
        public void OnSessionEnded()
        { ReleasePopup(); active = false; State.End(); player = null; path = lostPath = null; }
        public void Update(ulong tick)
        {
            if (!active || failed) return;
            bool nextCloud; string nextPath = CharacterIdentity.Path(out nextCloud);
            bool replacement = !ReferenceEquals(player, Main.LocalPlayer) || path != null && nextPath != null && (path != nextPath || cloud != nextCloud);
            if (replacement) { ReleasePopup(); State.Begin(++admission); player = Main.LocalPlayer; path = lostPath = null; }
            if (nextPath == null && path != null)
            { lostPath = path; lostCloud = cloud; path = null; State.DetachIdentity(); }
            if (nextPath != null && path == null)
            {
                // A known identity that disappeared cannot transfer an unknown
                // period's Draw receipt to a different file, even with reused Player.
                if (lostPath != null && (lostPath != nextPath || lostCloud != nextCloud)) { ReleasePopup(); State.Begin(++admission); }
                lostPath = null; path = nextPath; cloud = nextCloud; State.Resolve(CharacterIdentity.Key(path, cloud));
            }
            try { UpdatePopup(); } catch { FailClosed(); }
        }
        internal void Poll() { State.Poll(); }
        private bool Eligible()
        {
            return active && !failed && ReferenceEquals(player, Main.LocalPlayer) && Main.LocalPlayer != null &&
                !Main.LocalPlayer.dead && !Main.playerInventory && !Main.gameMenu && !Main.mapFullscreen && !Main.hideUI &&
                Main.showItemText && Main.netMode != 2 && CanPresent != null && CanPresent();
        }
        private bool OwnsPopup()
        {
            // Native NewText reuses objects; a reference alone is insufficient.
            // ClearAll also replaces the array entries. Never retire a successor.
            return popup != null && PopupText.popupText != null && slot >= 0 && slot < PopupText.popupText.Length && ReferenceEquals(PopupText.popupText[slot], popup) &&
                popup.active && popup.freeAdvanced && popup.context == PopupTextContext.Advanced && popup.name == Prompt && popup.displayText == Prompt;
        }
        private void ReleasePopup()
        {
            if (OwnsPopup()) popup.active = false;
            popup = null; slot = -1; State.Pause();
        }
        private void UpdatePopup()
        {
            if (!State.Ready) { if (popup != null) ReleasePopup(); return; }
            bool owns = OwnsPopup();
            if (State.Progress >= 1 && !owns) { ReleasePopup(); State.FinishPresentation(); return; }
            if (!Eligible()) { ReleasePopup(); if (State.Progress >= 1) State.FinishPresentation(); return; }
            var current = FontAssets.MouseText?.Value;
            if (current == null) { ReleasePopup(); return; }
            if (!ReferenceEquals(font, current)) { font = current; textSize = font.MeasureString(Prompt); }
            if (!owns)
            {
                ReleasePopup();
                bool inverted = Main.LocalPlayer.gravDir == -1;
                var anchor = inverted ? Main.LocalPlayer.Bottom + new Vector2(0, 24 + textSize.Y) : Main.LocalPlayer.Top - new Vector2(0, 24);
                if (!VisibleBounds(anchor - textSize / 2)) return;
                // FindNextItemTextSlot overwrites an existing popup when full.
                // Admission and NewText run synchronously on the game thread.
                bool free = false;
                foreach (var item in PopupText.popupText) if (item != null && !item.active) { free = true; break; }
                if (!free) return;
                slot = PopupText.NewText(new AdvancedPopupRequest { Text = Prompt, Color = new Color(255, 250, 150), DurationInFrames = 180, Velocity = new Vector2(0, inverted ? 7 : -7) }, anchor);
                if (slot >= 0) { popup = PopupText.popupText[slot]; fadeTicks = 0; }
            }
            if (!OwnsPopup()) return;
            if (!VisibleBounds(popup.position)) { ReleasePopup(); return; }
            // Only this entry is kept alive for three seconds of readable frames.
            // Pauses release its slot; resumption uses fresh native admission.
            // Native scale, motion, outline, collisions and fade remain native.
            popup.lifeTime = State.Progress < 1 ? 2 : 0;
            // Native overlap can extend lifetimes before its fade branch. Cap
            // this final native animation, without touching any neighbour.
            if (State.Progress >= 1 && ++fadeTicks >= 40) { ReleasePopup(); State.FinishPresentation(); }
        }
        private bool VisibleBounds(Vector2 position)
        {
            float uiScale = Main.UIScaleMatrix.M11, scale = PopupText.TargetScale;
            if (!(uiScale > 0) || !(scale > 0) || float.IsInfinity(uiScale) || float.IsInfinity(scale) || textSize.X <= 0) return false;
            var center = position - Main.screenPosition;
            if (Main.LocalPlayer.gravDir == -1) center.Y = Main.screenHeight - center.Y;
            center += textSize / 2;
            // Match native DrawItemTextPopups, including its two-pixel outline.
            // Reserve full-size bounds even while the native entry grows/fades.
            var half = (textSize / 2 + new Vector2(2)) * scale;
            var a = Vector2.Transform(center - half, Main.GameViewMatrix.ZoomMatrix) / uiScale;
            var b = Vector2.Transform(center + half, Main.GameViewMatrix.ZoomMatrix) / uiScale;
            var rect = new F5Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
            return a.X >= 0 && a.Y >= 0 && b.X <= PlayerInput.OriginalScreenSize.X / uiScale && b.Y <= PlayerInput.OriginalScreenSize.Y / uiScale &&
                b.X > a.X && b.Y > a.Y && (Overlaps == null || !Overlaps(rect));
        }
        internal void ConfirmNativeDraw()
        {
            if (!State.Ready) return;
            // Locked Terraria .8 Main.Draw flushes DrawItemTextPopups before
            // DrawInterface reaches our F5 layer. No new draw hook/batch needed.
            // NewText runs only in Update; its initial zero scale is not shown.
            // This receipt submits no I/O; Poll owns the durable write.
            try
            {
                if (!OwnsPopup() || !Eligible() || !ReferenceEquals(FontAssets.MouseText?.Value, font) || !(popup.scale >= PopupText.TargetScale * .75f) || !(popup.alpha > 0) || !VisibleBounds(popup.position))
                { State.Pause(); return; }
                State.Presented(admission, Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
            }
            catch { FailClosed(); }
        }
        public void FailClosed() { failed = true; OnSessionEnded(); }
        private void Exit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= Exit; State.Stop(750); }
    }
}
