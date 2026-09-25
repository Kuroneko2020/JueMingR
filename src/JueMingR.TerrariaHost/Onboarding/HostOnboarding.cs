using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Onboarding;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private F5Size firstSize, secondSize;
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private const string First = "按 F5 打开决明R", Second = "使用帮助在“关于”页";
        public bool Enabled { get { return !failed; } }
        internal HostOnboarding(string gameDirectory)
        {
            State = new OnboardingState(key => new AtomicFileDocument(System.IO.Path.Combine(gameDirectory, "JueMingRData", "behavior", "onboarding", key + ".json"), OnboardingMarkerCodec.MaximumBytes, true));
            AppDomain.CurrentDomain.ProcessExit += Exit;
        }
        public void OnSessionStarted()
        { active = true; player = Main.LocalPlayer; path = lostPath = null; State.Begin(++admission); }
        public void OnSessionEnded()
        { active = false; State.End(); player = null; path = lostPath = null; }
        public void Update(ulong tick)
        {
            if (!active || failed) return;
            bool nextCloud; string nextPath = CharacterIdentity.Path(out nextCloud);
            bool replacement = !ReferenceEquals(player, Main.LocalPlayer) || path != null && nextPath != null && (path != nextPath || cloud != nextCloud);
            if (replacement) { State.Begin(++admission); player = Main.LocalPlayer; path = lostPath = null; }
            if (nextPath == null && path != null)
            { lostPath = path; lostCloud = cloud; path = null; State.DetachIdentity(); }
            if (nextPath != null && path == null)
            {
                // A known identity that disappeared cannot transfer an unknown
                // period's Draw receipt to a different file, even with reused Player.
                if (lostPath != null && (lostPath != nextPath || lostCloud != nextCloud)) State.Begin(++admission);
                lostPath = null; path = nextPath; cloud = nextCloud; State.Resolve(CharacterIdentity.Key(path, cloud));
            }
        }
        internal void Poll() { State.Poll(); }
        internal void Draw(bool eligible, Func<F5Rect, bool> overlaps)
        {
            if (!eligible || !active || failed || !State.Ready || !ReferenceEquals(player, Main.LocalPlayer) || Main.LocalPlayer.dead || Main.playerInventory)
            { State.Pause(); return; }
            try
            {
                var current = FontAssets.MouseText?.Value; var pixel = TextureAssets.MagicPixel?.Value; var batch = Main.spriteBatch;
                if (current == null || pixel == null || pixel.IsDisposed || batch == null) { State.Pause(); return; }
                if (!ReferenceEquals(font, current)) { font = current; firstSize = metrics.Measure(font, First); secondSize = metrics.Measure(font, Second); }
                float scale = .85f, uiScale = Main.UIScaleMatrix.M11;
                if (uiScale <= 0) { State.Pause(); return; }
                float width = PlayerInput.OriginalScreenSize.X / uiScale, height = PlayerInput.OriginalScreenSize.Y / uiScale;
                float w = Math.Max(firstSize.Width, secondSize.Width) * scale + 24, h = (firstSize.Height + secondSize.Height) * scale + 24;
                if (w > width - 24 || h > height - 160) { State.Pause(); return; }
                F5Rect rect = default(F5Rect); bool placed = false;
                // Bounded placement candidates avoid the information HUD and pinned
                // notes. If all are occupied, defer instead of painting over them.
                for (int i = 0; i < 3; i++)
                {
                    rect = new F5Rect((width - w) / 2, (height - h) * (.25f + .25f * i), w, h);
                    if (overlaps == null || !overlaps(rect)) { placed = true; break; }
                }
                if (!placed) { State.Pause(); return; }
                float opacity = State.Opacity; bool begun = false, flushed = false;
                batch.End();
                try
                {
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix); begun = true;
                    UiSurface.Panel(batch, pixel, rect, TextureAssets.InventoryBack?.Value, Color.White * opacity, true);
                    DrawText(batch, First, firstSize, rect.X + (w - firstSize.Width * scale) / 2, rect.Y + 8, scale, opacity);
                    DrawText(batch, Second, secondSize, rect.X + (w - secondSize.Width * scale) / 2, rect.Y + 14 + firstSize.Height * scale, scale, opacity);
                    batch.End(); begun = false; flushed = true;
                }
                finally { if (begun) batch.End(); batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix); }
                // No I/O, serialization or worker submission in Draw. A receipt
                // belongs to the captured role admission and is consumed by Poll.
                if (flushed) State.Presented(admission, Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
            }
            catch { FailClosed(); }
        }
        private void DrawText(SpriteBatch batch, string text, F5Size size, float x, float y, float scale, float opacity)
        { Utils.DrawBorderStringFourWay(batch, font, text, x - size.OffsetX * scale, y - size.OffsetY * scale, Color.White * opacity, Color.Black * opacity, Vector2.Zero, scale); }
        public void FailClosed() { failed = true; OnSessionEnded(); }
        private void Exit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= Exit; State.Stop(750); }
    }
}
