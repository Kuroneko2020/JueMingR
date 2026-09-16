using System;
using JueMingR.Features.Footprints;
using JueMingR.Platform.Footprints;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Capture;

namespace JueMingR.TerrariaHost.Footprints
{
    internal sealed class FootprintMapLayer : IDisposable
    {
        private readonly HostFootprints host;
        private readonly HostInputState input;
        private readonly FullscreenMapDrawing drawing;
        internal readonly FootprintPlayback Playback = new FootprintPlayback();
        internal Func<bool> UiOwnsInput;
        private MapView visible, projectedView;
        private FootprintQuery query, requestedPrevious;
        private FootprintSample[] saved = new FootprintSample[0];
        private Vector2[] projected = new Vector2[0];
        private readonly Vector2[] activeProjected = new Vector2[256];
        private long activeVersion = -1, visibleSession = -1, requestGeometry = -1;
        private long requestCommitted = -1;
        private string visibleArchive;
        private bool shown, drewBar, pending, dragging, previousLeft;
        private int pressed = -1, geometry, pressedGeometry;
        private double lastUi, nextRequest;
        private F5Rect panel, play, track, speed, latest;
        private DynamicSpriteFont font;
        private int layoutWidth, layoutHeight;
        private long shownSecond = -1, totalSecond = -1;
        private string timeText = "0:00:00 / 0:00:00";
        private static readonly string[] speedLabels = { "1×", "10×", "60×", "300×", "1800×" };
        private int speedIndex;
        private readonly Vector2[] speedSizes = new Vector2[5];
        private Vector2 playSize, pauseSize, latestSize;
        private string shownHint;
        private float hintWidth;
#if DEBUG
        internal long ProjectedPoints, DrawnEdges, QueryRequests;
#endif
        internal FootprintMapLayer(HostFootprints host, HostInputState input)
        {
            this.host = host; this.input = input; drawing = FullscreenMapDrawing.Acquire();
            drawing.Routes += DrawRoutes; drawing.Overlay += DrawBar; drawing.Completed += Completed; drawing.Begin += Begin;
            input.ClaimsMapPointer = ClaimsPointer;
        }
        private bool Active { get { return host.Session >= 0 && host.Display && !host.Clearing && Main.mapFullscreen && !Main.gameMenu && !Main.hideUI && !Main.dedServ && !CaptureManager.Instance.Active && !CaptureManager.Instance.UsingMap; } }
        private bool InputAvailable { get { return Active && !PlayerInput.UsingGamepad && !input.HotkeyCapture && !Main.blockInput && !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && (UiOwnsInput == null || !UiOwnsInput()); } }
        private bool GeometryCurrent { get { return visible != null && ReferenceEquals(font, FontAssets.MouseText?.Value) && visibleSession == host.Session && visibleArchive == host.Generation && visible.MatchesScreen() && visible.MatchesMap(); } }
        private bool ClaimsPointer()
        {
            if (!InputAvailable || !GeometryCurrent) return false;
            return dragging || pressed >= 0 || panel.Contains(input.PhysicalMapX, input.PhysicalMapY);
        }
        internal void Invalidate()
        {
            visible = projectedView = null; saved = new FootprintSample[0]; projected = new Vector2[0]; query = requestedPrevious = null;
            pressed = -1; dragging = pending = shown = false; activeVersion = requestGeometry = -1; Playback.Hide(); host.CancelQuery();
            // HostInputState still owns any physical tail. Hiding never gives an
            // already-consumed mouse gesture back to vanilla or another feature.
        }
        internal void Update()
        {
            double now = host.UiSeconds, elapsed = shown ? Math.Max(0, now - lastUi) : 0; lastUi = now;
            if (!Active) { if (shown || visible != null || query != null) Invalidate(); return; }
            if (!shown) { Playback.Show(host.End); speedIndex = 0; shown = true; lastUi = now; nextRequest = 0; }
            Playback.Advance(elapsed, host.End, input.SampleFocused && InputAvailable && !dragging);
            var received = host.Query;
            if (received != null && received.Generation == host.Generation && !ReferenceEquals(received, query)) { query = received; pending = false; }
            long geometryVersion = host.Recorder?.GeometryVersion ?? 0;
            var recorder = host.Recorder;
            bool activeCovers = recorder != null && recorder.ActiveCount > 0 && Playback.Cursor >= recorder.At(0).Start && Playback.Cursor <= recorder.End;
            bool uncovered = query == null || !activeCovers && (query.Samples.Length == 0 || Playback.Cursor < query.Samples[0].Start || Playback.Cursor > query.Samples[query.Samples.Length - 1].End);
            long savedLast = query == null || query.Samples.Length == 0 ? 0 : query.Samples[query.Samples.Length - 1].Sequence;
            bool historyBehind = Playback.Latest && recorder != null && recorder.ActiveCount > 0 && savedLast < recorder.At(0).Sequence - 1;
            if (!pending && now >= nextRequest && (uncovered || historyBehind && (requestGeometry != geometryVersion || requestCommitted != host.CommittedCount)))
            {
                requestedPrevious = received; pending = host.RequestQuery((long)Playback.Cursor); requestGeometry = geometryVersion; requestCommitted = host.CommittedCount; nextRequest = now + .25;
#if DEBUG
                QueryRequests++;
#endif
            }
            if (pending && received != null && !ReferenceEquals(received, requestedPrevious)) pending = false;
        }
        internal void ProcessInput()
        {
            bool left = input.Hotkeys.IsDown(256), fresh = input.Hotkeys.IsNew(256);
            try
            {
                if (!input.CanUseInput || !InputAvailable || !GeometryCurrent || input.Hotkeys.IsNew(116))
                { pressed = -1; dragging = false; previousLeft = input.SampleFocused ? left : true; return; }
                float x = input.PhysicalMapX, y = input.PhysicalMapY;
                if (fresh && (input.OwnedMouseMask & 1) != 0)
                {
                    pressed = Hit(x, y); pressedGeometry = geometry;
                    if (pressed == 1 && host.End > 0) { dragging = true; Seek(x); }
                }
                if (dragging && left) Seek(x);
                if (!left && previousLeft && pressed >= 0)
                {
                    int command = pressed; pressed = -1;
                    if (pressedGeometry == geometry && (dragging || Hit(x, y) == command))
                    {
                        if (command == 0) Playback.Toggle(host.End);
                        else if (command == 2) { Playback.CycleSpeed(); speedIndex = (speedIndex + 1) % speedLabels.Length; }
                        else if (command == 3) Playback.GoLatest(host.End);
                    }
                    dragging = false;
                }
                previousLeft = left;
            }
            finally { input.ConsumeOwnedMouseEdges(); }
        }
        private void Seek(float x)
        { long end = dragging && !Playback.Latest ? Playback.DisplayEnd : host.End; Playback.Seek((long)(Math.Max(0, Math.Min(1, (x - track.X) / track.Width)) * end), end); }
        private int Hit(float x, float y) { return play.Contains(x, y) ? 0 : track.Contains(x, y) ? 1 : speed.Contains(x, y) ? 2 : latest.Contains(x, y) ? 3 : -1; }
        private void Begin() { drewBar = false; }
        private void Completed(MapView frame)
        { visible = Active && drewBar ? frame : null; visibleSession = host.Session; visibleArchive = host.Generation; if (visible == null) { pressed = -1; dragging = false; } }
        private static bool SameView(MapView a, MapView b)
        { return a != null && a.Position == b.Position && a.Offset == b.Offset && a.Zoom == b.Zoom && a.Width == b.Width && a.Height == b.Height; }
        private void DrawRoutes(MapView frame)
        {
            if (!Active || !shown) return;
            var pixel = TextureAssets.MagicPixel?.Value; if (pixel == null || pixel.IsDisposed) return;
            var points = query?.Samples ?? saved; bool changedView = !SameView(projectedView, frame);
            if (!ReferenceEquals(saved, points)) { saved = points; projected = new Vector2[points.Length]; changedView = true; }
            if (changedView) for (int i = 0; i < saved.Length; i++) { projected[i] = frame.Project(saved[i].X, saved[i].Y);
#if DEBUG
                ProjectedPoints++;
#endif
            }
            var recorder = host.Recorder;
            bool activeVisible = recorder != null && recorder.ActiveCount > 0 && Playback.Cursor >= recorder.At(0).Start;
            if (activeVisible && (changedView || activeVersion != recorder.BufferVersion))
            {
                for (int i = 0; i < recorder.ActiveCount; i++) { var p = recorder.At(i); activeProjected[i] = frame.Project(p.X, p.Y);
#if DEBUG
                    ProjectedPoints++;
#endif
                }
                activeVersion = recorder.BufferVersion;
            }
            projectedView = frame; double cursor = Playback.Cursor; Vector2? marker = null;
            for (int i = 0; i < saved.Length; i++)
            {
                if (saved[i].HasPosition && cursor >= saved[i].Start && cursor <= saved[i].End) marker = projected[i];
                if (i > 0) DrawEdge(pixel, saved[i - 1], saved[i], projected[i - 1], projected[i], cursor, frame, ref marker);
            }
            long savedEnd = saved.Length == 0 ? 0 : saved[saved.Length - 1].Sequence;
            if (activeVisible) for (int i = 0; i < recorder.ActiveCount; i++)
            {
                var p = recorder.At(i);
                if (p.HasPosition && cursor >= p.Start && cursor <= p.End) marker = activeProjected[i];
                if (i > 0 && p.Sequence > savedEnd) DrawEdge(pixel, recorder.At(i - 1), p, activeProjected[i - 1], activeProjected[i], cursor, frame, ref marker);
            }
            if (marker.HasValue && marker.Value.X >= 0 && marker.Value.Y >= 0 && marker.Value.X < frame.Width && marker.Value.Y < frame.Height)
                Main.spriteBatch.Draw(pixel, marker.Value - new Vector2(2, 2), new Rectangle(0, 0, 1, 1), Color.LimeGreen, 0, Vector2.Zero, new Vector2(4, 4), SpriteEffects.None, 0);
        }
        private void DrawEdge(Texture2D pixel, FootprintSample previous, FootprintSample next, Vector2 a, Vector2 b, double cursor, MapView frame, ref Vector2? marker)
        {
            if (!next.Follows(previous) || cursor <= next.Start) return;
            // Each moving sample is the position at its first completed step;
            // later merged duration is a stay, never slower movement along a chord.
            double fraction = Math.Max(0, Math.Min(1, cursor - next.Start));
            if (fraction < 1) { b = Vector2.Lerp(a, b, (float)fraction); marker = b; }
            if (!Clip(ref a, ref b, frame.Width, frame.Height)) return;
            Vector2 edge = b - a; float length = edge.Length(); if (length <= 0) return;
            Main.spriteBatch.Draw(pixel, a, new Rectangle(0, 0, 1, 1), Color.LimeGreen * (frame.Alpha / 255f), (float)Math.Atan2(edge.Y, edge.X), new Vector2(0, .5f), new Vector2(length, 1), SpriteEffects.None, 0);
#if DEBUG
            DrawnEdges++;
#endif
        }
        internal static bool Clip(ref Vector2 a, ref Vector2 b, float width, float height)
        {
            double x = a.X, y = a.Y, dx = b.X - x, dy = b.Y - y, from = 0, to = 1;
            if (!ClipSide(-dx, x, ref from, ref to) || !ClipSide(dx, width - 1 - x, ref from, ref to) || !ClipSide(-dy, y, ref from, ref to) || !ClipSide(dy, height - 1 - y, ref from, ref to)) return false;
            a = new Vector2((float)(x + from * dx), (float)(y + from * dy)); b = new Vector2((float)(x + to * dx), (float)(y + to * dy)); return true;
        }
        private static bool ClipSide(double p, double q, ref double from, ref double to)
        { if (p == 0) return q >= 0; double t = q / p; if (p < 0) { if (t > to) return false; if (t > from) from = t; } else { if (t < from) return false; if (t < to) to = t; } return true; }
        private void Layout(MapView frame, DynamicSpriteFont nextFont)
        {
            if (layoutWidth == frame.Width && layoutHeight == frame.Height && ReferenceEquals(font, nextFont)) return;
            layoutWidth = frame.Width; layoutHeight = frame.Height; font = nextFont; geometry++; pressed = -1; dragging = false;
            playSize = font.MeasureString("播放") * .70f; pauseSize = font.MeasureString("暂停") * .70f; latestSize = font.MeasureString("最新") * .70f;
            for (int i = 0; i < speedSizes.Length; i++) speedSizes[i] = font.MeasureString(speedLabels[i]) * .70f;
            shownHint = null;
            float textHeight = font.MeasureString("测试Ag").Y * .70f, row = Math.Max(32, textHeight + 10), width = Math.Min(900, frame.Width - 24);
            panel = new F5Rect((frame.Width - width) / 2, frame.Height - row * 3 - 26, width, row * 3 + 14);
            float pw = Math.Max(54, font.MeasureString("播放").X * .70f + 20), sw = Math.Max(64, font.MeasureString("1800×").X * .70f + 20), lw = Math.Max(54, font.MeasureString("最新").X * .70f + 20);
            play = new F5Rect(panel.X + 8, panel.Y + 6, pw, row); latest = new F5Rect(panel.Right - lw - 8, play.Y, lw, row); speed = new F5Rect(latest.X - sw - 8, play.Y, sw, row); track = new F5Rect(play.Right + 12, play.Y, Math.Max(16, speed.X - play.Right - 24), row);
        }
        private void DrawBar(MapView frame)
        {
            if (!Active || !shown) return;
            var nextFont = FontAssets.MouseText?.Value; var pixel = TextureAssets.MagicPixel?.Value;
            if (nextFont == null || pixel == null || pixel.IsDisposed) return;
            Layout(frame, nextFont); var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
            var blend = device.BlendState; var sampler = device.SamplerStates[0]; var depth = device.DepthStencilState; var rasterizer = device.RasterizerState; var scissor = device.ScissorRectangle; bool began = false;
            try
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); began = true;
                UiSurface.Panel(batch, pixel, panel, TextureAssets.InventoryBack?.Value, Color.White, true);
                Button(play, Playback.Playing ? "暂停" : "播放", Playback.Playing ? pauseSize : playSize, host.End > 0, pixel); Button(speed, speedLabels[speedIndex], speedSizes[speedIndex], true, pixel); Button(latest, "最新", latestSize, true, pixel);
                float center = track.Y + track.Height / 2;
                batch.Draw(pixel, new Vector2(track.X, center), new Rectangle(0, 0, 1, 1), Color.Gray, 0, Vector2.Zero, new Vector2(track.Width, 2), SpriteEffects.None, 0);
                float portion = Playback.DisplayEnd == 0 ? 0 : (float)Math.Max(0, Math.Min(1, Playback.Cursor / Playback.DisplayEnd));
                batch.Draw(pixel, new Vector2(track.X + portion * track.Width - 2, center - 6), new Rectangle(0, 0, 1, 1), Color.LimeGreen, 0, Vector2.Zero, new Vector2(4, 14), SpriteEffects.None, 0);
                long second = (long)Playback.Cursor / 60, total = Playback.DisplayEnd / 60;
                if (second != shownSecond || total != totalSecond) { shownSecond = second; totalSecond = total; timeText = FootprintPlayback.FormatTime((long)Playback.Cursor) + " / " + FootprintPlayback.FormatTime(Playback.DisplayEnd); }
                float y = play.Bottom + 5; batch.DrawString(font, timeText, new Vector2(panel.X + 12, y), Color.White, 0, Vector2.Zero, .70f, SpriteEffects.None, 0);
                string hint = host.HasFailure ? host.StatusMessage : host.End == 0 ? "暂无足迹" : query == null ? "读取中…" : query.Partial ? "局部显示，拖动查看其他时段" : "拖动时间轴回看；最新会暂停播放";
                if (shownHint != hint) { shownHint = hint; hintWidth = font.MeasureString(hint).X * .65f; }
                float hintScale = hintWidth > panel.Width - 24 ? .65f * (panel.Width - 24) / hintWidth : .65f;
                batch.DrawString(font, hint, new Vector2(panel.X + 12, y + play.Height), host.HasFailure ? Color.Orange : Color.LightGray, 0, Vector2.Zero, hintScale, SpriteEffects.None, 0);
                drewBar = true;
            }
            finally { try { if (began) batch.End(); } finally { device.BlendState = blend; device.SamplerStates[0] = sampler; device.DepthStencilState = depth; device.RasterizerState = rasterizer; device.ScissorRectangle = scissor; } }
        }
        private void Button(F5Rect rect, string label, Vector2 size, bool enabled, Texture2D pixel)
        {
            bool hover = rect.Contains(input.PhysicalMapX, input.PhysicalMapY);
            UiSurface.Panel(Main.spriteBatch, pixel, rect, TextureAssets.InventoryBack?.Value, enabled && hover ? Color.White : new Color(210, 210, 210), true);
            Main.spriteBatch.DrawString(font, label, new Vector2(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2), enabled ? Color.White : Color.Gray, 0, Vector2.Zero, .70f, SpriteEffects.None, 0);
        }
        public void Dispose()
        { input.ClaimsMapPointer = null; drawing.Routes -= DrawRoutes; drawing.Overlay -= DrawBar; drawing.Completed -= Completed; drawing.Begin -= Begin; drawing.Dispose(); Invalidate(); }
    }
}
