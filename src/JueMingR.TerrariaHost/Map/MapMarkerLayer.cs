using System;
using JueMingR.Features.MapMarkers;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Capture;

namespace JueMingR.TerrariaHost.Map
{
    internal sealed class MapMarkerLayer : IDisposable
    {
        private readonly HostMapFeatures host;
        private readonly HostInputState input;
        private readonly FullscreenMapDrawing drawing;
        private readonly MarkerIcons icons = new MarkerIcons();
        private MapView visible;
        private long visibleSession = -1, visibleGeneration, selectionSession, selectionGeneration;
        private double pointX, pointY;
        private string selectedPair, hoverName;
        private MarkerRecord candidate, pendingLocate;
        private long createCommand;
        private bool picking, tail, previousLeft;
        private Vector2 anchor;
        private F5Rect picker;
        private readonly F5Rect[] options = new F5Rect[8];
        private int pressed = -1, hover = -1, geometry, pressedGeometry;
        internal Func<bool> UiOwnsInput;
        internal Action CloseForLocate;
        internal bool Picking { get { return picking; } }
        internal bool OwnsPointer { get { return picking || tail; } }
        private bool NativeOwnsInput { get { return CaptureManager.Instance.Active || CaptureManager.Instance.UsingMap || PlayerInput.Triggers.Current.MapFull || PlayerInput.Triggers.Current.ToggleCameraMode || PlayerInput.UsingGamepad; } }
        internal bool CanLocate { get { return host.OwnershipCurrent && input.CanUseInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !Main.blockInput && Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText && !input.HotkeyCapture && !NativeOwnsInput; } }
        private bool Active { get { return host.OwnershipCurrent && host.MarkersEnabled && Main.mapFullscreen && !Main.gameMenu && !Main.hideUI && !Main.dedServ && !CaptureManager.Instance.Active && !CaptureManager.Instance.UsingMap; } }
        internal MapMarkerLayer(HostMapFeatures host, HostInputState input)
        {
            this.host = host; this.input = input; drawing = FullscreenMapDrawing.Acquire(); drawing.Icons += DrawIcons; drawing.Completed += Completed; drawing.Overlay += Overlay;
        }
        private void Completed(MapView frame)
        {
            visible = Active ? frame : null; visibleSession = host.Session; visibleGeneration = host.MapGeneration;
        }
        internal void Invalidate() { visible = null; pendingLocate = null; Cancel(); }
        internal void Update()
        {
            if (!Active) { visible = null; Cancel(); }
            if (picking && (selectionSession != host.Session || selectionGeneration != host.MapGeneration || selectedPair != host.Pair)) Cancel();
            if (createCommand != 0 && host.Markers.LastOperation == createCommand)
            {
                createCommand = 0;
                if (host.Markers.LastSuccess) { host.Feedback("标记已保存。"); Cancel(); }
                else host.Feedback(host.Markers.CommitUnconfirmed ? "标记保存结果未确认，已停止保存。" : "标记未保存，可重选图标重试或按 Esc 取消。");
            }
        }
        internal bool Locate(MarkerRecord value) { if (!CanLocate) return false; pendingLocate = value; return true; }
        internal void ProcessInput()
        {
            bool left = input.Hotkeys.IsDown(256), right = input.Hotkeys.IsDown(257);
            if (!input.SampleFocused || !input.CanUseInput) { if (!input.SampleFocused) Invalidate(); previousLeft = left; return; }
            // Input runs before Runtime.Update. Recheck the actual world/map
            // identity here so an old picker cannot commit in that interval.
            if (visible != null && (!visible.MatchesMap() || visibleSession != host.Session || visibleGeneration != host.MapGeneration))
            { bool owned = picking; Invalidate(); if (owned) { tail = true; ConsumePointer(); } }
            if (pendingLocate != null)
            {
                var record = pendingLocate; pendingLocate = null;
                if (CanLocate && host.Workspace.Find(record.Id) != null)
                {
                    float scale = Main.mapFullscreenScale;
                    if (scale <= 0 || Single.IsNaN(scale) || Single.IsInfinity(scale)) Main.mapFullscreenScale = 2.5f;
                    CloseForLocate?.Invoke(); Main.mapFullscreen = true; Main.resetMapFull = false; Main.PanTargetMapFullscreen = false;
                    Main.mapFullscreenPos = new Vector2((float)Math.Max(10, Math.Min(host.Width - 10, record.X)), (float)Math.Max(10, Math.Min(host.Height - 19, record.Y)));
                    Main.grabMapX = input.MapMouseX; Main.grabMapY = input.MapMouseY;
                    tail = true; ConsumePointer(); visible = null;
                }
            }
            if (tail)
            {
                ConsumePointer(); if (!left && !right) tail = false; previousLeft = left; return;
            }
            if (!Active || NativeOwnsInput || UiOwnsInput != null && UiOwnsInput() || Main.blockInput || Main.CurrentInputTextTakerOverride != null || PlayerInput.WritingText || Main.drawingPlayerChat || Main.editSign || Main.editChest || input.HotkeyCapture)
            { if (picking) { Cancel(); tail = true; ConsumePointer(); } previousLeft = left; return; }
            if (picking)
            {
                if (visible == null || !visible.MatchesScreen()) { pressed = -1; previousLeft = left; ConsumePointer(); return; }
                Layout(visible); hover = Hit(input.MapMouseX, input.MapMouseY);
                if (input.Hotkeys.IsNew(27) || input.Hotkeys.IsNew(257)) { bool escape = input.Hotkeys.IsNew(27); Cancel(); tail = true; input.Hotkeys.SuppressHeld(); if (escape) input.ConsumeHotkeyActions(); ConsumePointer(); return; }
                if (input.Hotkeys.IsNew(256))
                {
                    if (hover < 0) { Cancel(); tail = true; ConsumePointer(); return; }
                    pressed = hover; pressedGeometry = geometry;
                }
                if (!left && previousLeft && pressed >= 0)
                {
                    int choice = pressed; pressed = -1;
                    if (choice == hover && pressedGeometry == geometry && createCommand == 0 && host.Markers.CanEdit)
                    {
                        string id = candidate?.Id ?? Guid.NewGuid().ToString("N");
                        candidate = new MarkerRecord(id, pointX, pointY, MarkerIcons.Ids[choice], candidate?.Name ?? MarkerName.Normalize(null, DateTime.Now));
                        createCommand = host.Markers.Create(candidate); if (createCommand == 0) host.Feedback("暂时无法保存标记。");
                    }
                }
                ConsumePointer(); previousLeft = left; return;
            }
            if (input.Hotkeys.IsNew(257))
            {
                if (!host.Markers.CanEdit || host.Markers.Saved.Records.Count >= 120) { host.Feedback("标记尚未就绪、已受保护或已达到 120 个上限。"); tail = true; ConsumePointer(); return; }
                if (visible == null || visibleSession != host.Session || visibleGeneration != host.MapGeneration || !visible.MatchesScreen() || !visible.TryPoint(input.MapMouseX, input.MapMouseY, host.Width, host.Height, out pointX, out pointY))
                { host.Feedback("此处没有可用的地图坐标，请在地图显示后重新选点。"); tail = true; ConsumePointer(); return; }
                picking = true; candidate = null; selectionSession = host.Session; selectionGeneration = host.MapGeneration; selectedPair = host.Pair;
                anchor = new Vector2(input.MapMouseX, input.MapMouseY); Layout(visible); tail = true; ConsumePointer();
            }
            previousLeft = left;
        }
        private void Cancel() { picking = false; pressed = hover = -1; candidate = null; createCommand = 0; }
        private void ConsumePointer()
        {
            // Never overwrite physical samples or manufacture a release. Only
            // this claimed gesture's vanilla actions and complete tail disappear.
            Main.mouseLeft = Main.mouseRight = false;
            PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.Current.MouseRight = false;
            PlayerInput.Triggers.JustPressed.MouseLeft = PlayerInput.Triggers.JustPressed.MouseRight = false;
            PlayerInput.Triggers.JustReleased.MouseLeft = PlayerInput.Triggers.JustReleased.MouseRight = false;
        }
        private void Layout(MapView frame)
        {
            float cell = 48 * frame.IconScale; int columns = frame.Width >= cell * 4 + 24 ? 4 : 2;
            float w = columns * cell + 16, h = (8 / columns) * cell + 34;
            var next = new F5Rect(Math.Max(8, Math.Min(frame.Width - w - 8, anchor.X + 12)), Math.Max(8, Math.Min(frame.Height - h - 8, anchor.Y + 12)), w, h);
            if (!next.Equals(picker)) { picker = next; geometry++; pressed = -1; }
            for (int i = 0; i < 8; i++) options[i] = new F5Rect(picker.X + 8 + i % columns * cell, picker.Y + 26 + i / columns * cell, cell, cell);
        }
        private int Hit(float x, float y) { for (int i = 0; i < 8; i++) if (options[i].Contains(x, y)) return i; return -1; }
        private void DrawIcons(MapView frame)
        {
            hoverName = null; if (!Active) return;
            try
            {
                var saved = host.Markers.Saved;
                if (saved != null) foreach (var record in saved.Records)
                {
                    Vector2 p = frame.Project(record.X, record.Y); float size = 28 * frame.IconScale;
                    if (p.X + size < 0 || p.Y + size < 0 || p.X - size > frame.Width || p.Y - size > frame.Height) continue;
                    F5Rect bounds;
                    if (icons.Draw(Main.spriteBatch, record.Icon, p, size, frame.Alpha / 255f, out bounds) && bounds.Contains(Main.mouseX, Main.mouseY)) { hoverName = record.Name; frame.HoverOwner = this; }
                }
                if (picking)
                {
                    Layout(frame); F5Rect ignored; int selected = createCommand != 0 ? candidate.Icon : hover >= 0 ? MarkerIcons.Ids[hover] : candidate?.Icon ?? 8;
                    icons.Draw(Main.spriteBatch, selected, frame.Project(pointX, pointY), 28 * frame.IconScale, .55f * frame.Alpha / 255f, out ignored);
                    if (picker.Contains(Main.mouseX, Main.mouseY)) frame.HoverOwner = this;
                }
            }
            catch (Exception ex) { host.Feedback("标记绘制暂不可用：" + ex.GetType().Name); Invalidate(); }
        }
        private void Overlay(MapView frame)
        {
            if (!Active || !picking && (!ReferenceEquals(frame.HoverOwner, this) || hoverName == null)) return;
            var font = FontAssets.MouseText?.Value; var pixel = TextureAssets.MagicPixel?.Value; if (font == null || pixel == null) return;
            var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
            var blend = device.BlendState; var sampler = device.SamplerStates[0]; var depth = device.DepthStencilState; var rasterizer = device.RasterizerState; var scissor = device.ScissorRectangle;
            bool began = false;
            try
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); began = true;
                if (picking)
                {
                    UiSurface.Panel(batch, pixel, picker, TextureAssets.InventoryBack?.Value, Color.White, true);
                    batch.DrawString(font, createCommand != 0 ? "正在保存…" : "选择图标 · Esc 取消", new Vector2(picker.X + 8, picker.Y + 5), Color.White, 0, Vector2.Zero, .60f, SpriteEffects.None, 0);
                    for (int i = 0; i < 8; i++) { F5Rect bounds; var r = options[i]; if (hover == i) UiSurface.Panel(batch, pixel, r, TextureAssets.InventoryBack?.Value, Color.LightGoldenrodYellow, false); icons.Draw(batch, MarkerIcons.Ids[i], new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), 28 * frame.IconScale, 1, out bounds); }
                }
                else
                {
                    var size = font.MeasureString(hoverName) * .75f; var r = new F5Rect(Math.Max(8, Math.Min(frame.Width - size.X - 24, Main.mouseX + 16)), Math.Max(8, Math.Min(frame.Height - size.Y - 24, Main.mouseY + 16)), size.X + 16, size.Y + 16);
                    UiSurface.Panel(batch, pixel, r, TextureAssets.InventoryBack?.Value, Color.White, true); batch.DrawString(font, hoverName, new Vector2(r.X + 8, r.Y + 8), Color.White, 0, Vector2.Zero, .75f, SpriteEffects.None, 0);
                }
            }
            finally { try { if (began) batch.End(); } finally { device.BlendState = blend; device.SamplerStates[0] = sampler; device.DepthStencilState = depth; device.RasterizerState = rasterizer; device.ScissorRectangle = scissor; } }
        }
        public void Dispose() { drawing.Icons -= DrawIcons; drawing.Overlay -= Overlay; drawing.Completed -= Completed; drawing.Dispose(); Invalidate(); }
    }
}
