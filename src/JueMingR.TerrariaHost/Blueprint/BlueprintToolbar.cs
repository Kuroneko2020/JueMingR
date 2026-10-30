using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal enum BlueprintToolbarAction { None, Capture, Save, ExitCapture, Library, Move, Remove, Mirror, Trim, ConfirmTrim, CancelPlace, RemoveAll, Materials }
    // Presentation owns only button rectangles and their UI gesture. Mode,
    // selection, world-action tails and preference all remain on HostBlueprint.
    internal sealed class BlueprintToolbar : IDisposable
    {
        internal static readonly Color HintColor = new Color(184, 134, 11);
        private readonly HostBlueprint host;
        internal readonly BlueprintToolbarLayout Layout = new BlueprintToolbarLayout();
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly Func<string, float, F5Size> measure;
        private DynamicSpriteFont font;
        private Texture2D skin, pixel;
        private RasterizerState clipped;
        private Matrix drawnMatrix;
        private F5Rect visibleClip;
        private bool presented, previousLeft = true, needsNeutral = true;
        private bool leftTail, rightTail;
        private long processedMapping = -1;
        private int armed = -1, armedGeneration;
        private BlueprintToolbarAction armedAction;
#if DEBUG
        internal int MeasurementCount { get; private set; }
#endif
        internal BlueprintToolbar(HostBlueprint host) { this.host = host; measure = Measure; }

        // This is a visible UI hit, never the capture mode or an aggregate tail.
        // HostInputState retains each already-owned physical mouse tail itself;
        // reporting either tail here would capture the other key outside the bar.
        internal bool HitCurrent()
        {
            if (!GeometryCurrent()) return false;
            Vector2 point = Pointer();
            return Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
        }

        private bool GeometryCurrent()
        {
            Layout.SetPreviewRevision(host.TrimPreviewRevision);
            return presented && host.ShowToolbar && drawnMatrix == Main.UIScaleMatrix &&
                Layout.MatchesProjection(PlayerInput.OriginalScreenSize.X, PlayerInput.OriginalScreenSize.Y,
                    Main.UIScaleMatrix.M11, FontAssets.MouseText?.Value, host.State.Active, ProjectionMode) &&
                ReferenceEquals(skin, TextureAssets.InventoryBack?.Value) && ReferenceEquals(pixel, TextureAssets.MagicPixel?.Value);
        }
        private Vector2 Pointer()
        {
            // Physical UI pixels use the UI inverse only. The selection's frozen
            // world tile coordinate is owned by the independent pointer proof.
            return Vector2.Transform(new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X,
                PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y), Matrix.Invert(Main.UIScaleMatrix));
        }
        private int ProjectionMode { get { return host.OwnMirrorCancellation ? 5 : host.OwnTrimCancellation ? host.CanInvokeConfirmTrim ? 7 : 6 : host.OwnPlaceCancellation ? 2 : host.OwnMoveCancellation ? 3 : host.OwnRemoveCancellation ? 4 : host.ProjectionScene.Count == 0 ? 0 : 1; } }
        // Draw and hints share stable business predicates. Process still routes
        // commands through Host entries with the current input permission.
        private bool Available(int index)
        {
            if (!Layout.Available(index)) return false;
            switch (Layout.Action(index))
            {
                case BlueprintToolbarAction.Materials: return host.CanOpenMaterials;
                case BlueprintToolbarAction.Save: return host.CanSave;
                case BlueprintToolbarAction.Capture: return !host.ProjectionEditing;
                case BlueprintToolbarAction.ExitCapture: return true;
                case BlueprintToolbarAction.Library: return host.CanOpenLibrary;
                case BlueprintToolbarAction.Move: return host.CanInvokeMove;
                case BlueprintToolbarAction.Remove: return host.CanInvokeRemove;
                case BlueprintToolbarAction.Mirror: return host.CanInvokeMirror;
                case BlueprintToolbarAction.Trim: return host.CanInvokeTrim;
                case BlueprintToolbarAction.ConfirmTrim: return host.CanInvokeConfirmTrim;
                case BlueprintToolbarAction.CancelPlace: return host.CanInvokePlace;
                case BlueprintToolbarAction.RemoveAll: return host.CanRemoveAllProjections;
                default: return false;
            }
        }

        internal void Process()
        {
            if (processedMapping == host.Input.Mapping) return;
            processedMapping = host.Input.Mapping;
            if (!host.Input.SampleFocused)
            {
                Cancel(); previousLeft = needsNeutral = true;
                return; // A synthetic unfocused release cannot retire UI tails.
            }
            bool left = host.Input.RawMouseLeft, right = host.Input.RawMouseRight;
            bool current = host.Input.CanStartActions && GeometryCurrent();
            Vector2 point = current ? Pointer() : Vector2.Zero;
            bool over = current && Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
            int hit = over ? Layout.Hit(point.X, point.Y) : -1;
            if (!current) Cancel();
            if (over)
            {
                // Disabled buttons and the bar's small gaps still own UI input.
                // Dragging into the bar cancels world editing, but never arms it.
                if (left) leftTail = true;
                if (right) rightTail = true;
                if (!needsNeutral && left && !previousLeft)
                { armed = hit >= 0 && Available(hit) ? hit : -1; armedGeneration = Layout.Generation; armedAction = Layout.Action(armed); }
            }
            int execute = -1;
            if (armed >= 0 && !left && previousLeft)
            {
                if (current && armedGeneration == Layout.Generation && armed == hit && armedAction == Layout.Action(hit) && Available(armed)) execute = armed;
                armed = -1;
            }
            if (right) armed = -1;
            // Consume before retiring each trusted release. Hiding or reflowing
            // cancels activation, while its physical tail remains UI-owned.
            Consume(leftTail, rightTail);
            if (!left) leftTail = false;
            if (!right) rightTail = false;
            if (!left && !right) needsNeutral = false;
            previousLeft = left;
            if (execute >= 0)
            {
                switch (Layout.Action(execute))
                {
                    case BlueprintToolbarAction.Materials: host.OpenMaterialsFromToolbar(Layout.Buttons[execute].Rect.Offset(Layout.Panel.X, Layout.Panel.Y)); break;
                    case BlueprintToolbarAction.Move: host.MoveProjection(); break;
                    case BlueprintToolbarAction.Remove: host.RemoveProjection(); break;
                    case BlueprintToolbarAction.Mirror: host.MirrorProjection(); break;
                    case BlueprintToolbarAction.Trim: host.TrimProjection(); break;
                    case BlueprintToolbarAction.ConfirmTrim: host.ConfirmTrimProjection(); break;
                    case BlueprintToolbarAction.CancelPlace: host.Place(null); break;
                    case BlueprintToolbarAction.RemoveAll: host.RemoveAllProjections(); break;
                    case BlueprintToolbarAction.Save: host.Save(); break;
                    case BlueprintToolbarAction.Library: host.OpenLibrary(); break;
                    case BlueprintToolbarAction.Capture:
                    case BlueprintToolbarAction.ExitCapture: host.Toggle(); break;
                }
            }
        }

        private void Consume(bool left, bool right)
        {
            if (left)
            {
                host.Input.Hotkeys.ConsumePress(256);
                PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.JustPressed.MouseLeft = PlayerInput.Triggers.JustReleased.MouseLeft = false;
                Main.mouseLeft = false;
            }
            if (right)
            {
                host.Input.Hotkeys.ConsumePress(257);
                PlayerInput.Triggers.Current.MouseRight = PlayerInput.Triggers.JustPressed.MouseRight = PlayerInput.Triggers.JustReleased.MouseRight = false;
                Main.mouseRight = false;
            }
        }

        internal void Cancel()
        {
            armed = -1; presented = false; hint.Hide();
            // Do not clear leftTail/rightTail here: visibility, session and
            // focus changes are not evidence that a physical key was released.
        }

        internal void Draw()
        {
            if (!host.ShowToolbar) { Cancel(); return; }
            var nextFont = FontAssets.MouseText?.Value;
            var nextSkin = TextureAssets.InventoryBack?.Value;
            var nextPixel = TextureAssets.MagicPixel?.Value;
            if (nextFont == null || nextPixel == null || nextPixel.IsDisposed || Main.spriteBatch == null) { Cancel(); return; }
            Matrix matrix = Main.UIScaleMatrix;
            Vector2 screen = PlayerInput.OriginalScreenSize;
            if (!ReferenceEquals(skin, nextSkin) || !ReferenceEquals(pixel, nextPixel) || matrix != drawnMatrix) Cancel();
            font = nextFont; skin = nextSkin; pixel = nextPixel;
            Layout.SetPreviewRevision(host.TrimPreviewRevision);
            int oldGeneration = Layout.Generation;
            if (!Layout.EnsureProjection(screen.X, screen.Y, matrix.M11, font, measure, host.State.Active, ProjectionMode)) { Cancel(); return; }
            if (oldGeneration != Layout.Generation) Cancel();
            drawnMatrix = matrix;
            var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
            Rectangle oldScissor = device.ScissorRectangle;
            var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState;
            var oldDepth = device.DepthStencilState; var oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            var bounds = new F5Rect(8, 8, screen.X / matrix.M11 - 16, screen.Y / matrix.M11 - 16);
            Rectangle clip = F5ControlRenderer.ContentClip(bounds, matrix, oldScissor);
            bool began = false;
            batch.End();
            try
            {
                device.ScissorRectangle = clip;
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, clipped, null, matrix);
                began = true;
                visibleClip = F5ControlRenderer.LogicalClip(clip, matrix);
                Vector2 point = Pointer();
                bool over = Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
                int hovered = over ? Layout.Hit(point.X, point.Y) : -1;
                UiSurface.Panel(batch, pixel, Layout.Panel, skin, new Color(232, 232, 232), roundedOuter: true);
                for (int i = 0; i < Layout.Buttons.Length; i++)
                    F5ControlRenderer.Button(batch, pixel, skin, font, Layout.Buttons[i], hovered == i,
                        Available(i), null, Layout.Panel.X, Layout.Panel.Y, true, armed == i && hovered == i);
                DrawHint(batch, bounds, hovered);
                if (over) Main.LocalPlayer.mouseInterface = true;
                presented = true;
            }
            finally
            {
                try { if (began) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                }
            }
        }

        private void DrawHint(SpriteBatch batch, F5Rect bounds, int hovered)
        {
            var action = Layout.Action(hovered);
            string value = action == BlueprintToolbarAction.Materials ? host.MaterialsDisabledReason ?? BlueprintControls.MaterialsHint :
                action == BlueprintToolbarAction.Move ? host.CanInvokeMove ? host.OwnMoveCancellation ? "取消移动，保留原蓝图" : "点击需要移动的蓝图" : host.MoveDisabledReason :
                action == BlueprintToolbarAction.Remove ? host.CanInvokeRemove ? host.OwnRemoveCancellation ? "退出移除选择" : "点击需要移除的蓝图" : host.RemoveDisabledReason :
                action == BlueprintToolbarAction.Mirror ? host.CanInvokeMirror ? host.OwnMirrorCancellation ? "取消镜像，保留原蓝图" : "点击需要镜像的蓝图" : host.MirrorDisabledReason :
                action == BlueprintToolbarAction.Trim ? host.CanInvokeTrim ? host.OwnTrimCancellation ? "取消修剪，保留图纸内容" : "点击需要修剪的蓝图" : host.TrimDisabledReason :
                action == BlueprintToolbarAction.ConfirmTrim ? host.CanInvokeConfirmTrim ? BlueprintControls.ConfirmTrimHint : host.ConfirmTrimDisabledReason :
                action == BlueprintToolbarAction.CancelPlace ? "取消此次摆放" :
                action == BlueprintToolbarAction.RemoveAll ? host.RemoveAllDisabledReason :
                action == BlueprintToolbarAction.Library ? host.ManualLibraryDisabledReason :
                action == BlueprintToolbarAction.Save && !host.CanSave ? host.SaveDisabledReason :
                action == BlueprintToolbarAction.Capture && host.ProjectionEditing ? "请先完成或取消当前蓝图操作" : Layout.Hint(hovered);
            if (value == null || leftTail || rightTail || !host.Input.CanPrepareText) { hint.Hide(); return; }
            // Reuse the common bounded wrapping and glyph metrics, but place
            // the measured result above the entire bar, explicitly centered.
            var above = new F5Rect(bounds.X, bounds.Y, bounds.Width, Layout.Panel.Y - 2 - bounds.Y);
            hint.Prepare(value, Layout.Panel, above, font, measure);
            if (!hint.Visible) return;
            // Lines already include their outline and native glyph offsets.
            // Discard the common hint panel padding: only text is presented,
            // each measured line centered above the bar. The native font's
            // transparent glyph rows already leave the visible outline gap;
            // adding another panel gap would move the actual text too far up.
            float contentHeight = hint.Lines.Count == 0 ? 0 : hint.Lines[hint.Lines.Count - 1].Rect.Bottom;
            float textTop = Layout.Panel.Y - contentHeight;
            foreach (var line in hint.Lines)
                Utils.DrawBorderStringFourWay(batch, font, line.Text,
                    Layout.Panel.X + (Layout.Panel.Width - line.Rect.Width) / 2 - line.TextSize.OffsetX,
                    textTop + line.Rect.Y - line.TextSize.OffsetY,
                    HintColor, Color.Black, Vector2.Zero, line.TextScale);
        }

        private F5Size Measure(string text, float scale)
        {
#if DEBUG
            MeasurementCount++;
#endif
            var size = metrics.Measure(font, text);
            return new F5Size(size.Width * scale + 4, size.Height * scale + 4, size.OffsetX * scale - 2, size.OffsetY * scale - 2);
        }

        public void Dispose()
        {
            Cancel(); hint.Clear();
            clipped?.Dispose(); clipped = null;
            // Font and skin textures belong to Terraria, never to this bar.
            font = null; skin = pixel = null;
        }
    }

    // CPU geometry is separate from the native adapter so the actual toolbar
    // placement, mode replacement and stable-layout cost can be checked directly.
    internal sealed class BlueprintToolbarLayout
    {
        internal F5Element[] Buttons = Array.Empty<F5Element>();
        private BlueprintToolbarAction[] actions = Array.Empty<BlueprintToolbarAction>();
        internal int MaterialsIndex { get { return Array.IndexOf(actions, BlueprintToolbarAction.Materials); } }
        internal BlueprintToolbarAction Action(int index) { return index >= 0 && index < actions.Length ? actions[index] : BlueprintToolbarAction.None; }
        internal F5Rect Panel { get; private set; }
        internal int Generation { get; private set; }
        private float width, height, scale;
        private object font;
        private bool active;
        private int projectionMode;
        private long previewRevision, builtPreviewRevision;
        internal void SetPreviewRevision(long value) { previewRevision = value; }
        internal bool Matches(float w, float h, float s, object f, bool mode)
        { return MatchesProjection(w, h, s, f, mode, 0); }
        internal bool MatchesProjection(float w, float h, float s, object f, bool mode, int projection)
        { return Generation > 0 && w == width && h == height && s == scale && ReferenceEquals(f, font) && mode == active && projection == projectionMode && previewRevision == builtPreviewRevision; }
        internal bool Ensure(float w, float h, float s, object f, Func<string, float, F5Size> measure, bool mode)
        { return EnsureProjection(w, h, s, f, measure, mode, 0); }
        internal bool EnsureProjection(float w, float h, float s, object f, Func<string, float, F5Size> measure, bool mode, int projection)
        {
            if (MatchesProjection(w, h, s, f, mode, projection)) return true;
            if (f == null || measure == null || !Finite(w) || !Finite(h) || !Finite(s) || s <= 0 || w <= 0 || h <= 0) return false;
            bool extra = projection == 2 || projection == 4;
            bool trimming = projection == 6 || projection == 7;
            var nextActions = mode ? new[] { BlueprintToolbarAction.Save, BlueprintToolbarAction.ExitCapture } :
                projection == 0 ? new[] { BlueprintToolbarAction.Capture, BlueprintToolbarAction.Library, BlueprintToolbarAction.Materials } :
                extra ? new[] { BlueprintToolbarAction.Capture, BlueprintToolbarAction.Library, BlueprintToolbarAction.Move, BlueprintToolbarAction.Remove,
                    BlueprintToolbarAction.Mirror, BlueprintToolbarAction.Trim, projection == 2 ? BlueprintToolbarAction.CancelPlace : BlueprintToolbarAction.RemoveAll, BlueprintToolbarAction.Materials } :
                trimming ? new[] { BlueprintToolbarAction.Capture, BlueprintToolbarAction.Library, BlueprintToolbarAction.Move, BlueprintToolbarAction.Remove,
                    BlueprintToolbarAction.Mirror, BlueprintToolbarAction.Trim, BlueprintToolbarAction.ConfirmTrim, BlueprintToolbarAction.Materials } :
                new[] { BlueprintToolbarAction.Capture, BlueprintToolbarAction.Library, BlueprintToolbarAction.Move, BlueprintToolbarAction.Remove,
                    BlueprintToolbarAction.Mirror, BlueprintToolbarAction.Trim, BlueprintToolbarAction.Materials };
            var text = new string[nextActions.Length]; var sizes = new F5Size[text.Length]; var widths = new double[text.Length];
            double maximum = (double)w / s - 16, total = 16 + 4 * (text.Length - 1); float bh = 30;
            for (int i = 0; i < text.Length; i++)
            {
                text[i] = Label(nextActions[i], projection); sizes[i] = measure(text[i], .70f);
                widths[i] = Math.Max(30, sizes[i].Width + 16); total += widths[i]; bh = Math.Max(bh, sizes[i].Height + 8);
            }
            // Measure and place the visible actions with one ordered double sum.
            // A proven single row must not be reconsidered using float grouping.
            bool single = total <= maximum; float pw = (float)Math.Min(maximum, total), y = 8; double used = 0;
            var nextButtons = new F5Element[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                if (widths[i] > maximum - 16) return false;
                double next = used == 0 ? widths[i] : used + 4 + widths[i];
                if (!single && next > maximum - 16 && used > 0) { used = 0; next = widths[i]; y += bh + 4; }
                double x = used == 0 ? 8 : used + 12;
                nextButtons[i] = new F5Element(F5ElementKind.Button, new F5Rect((float)x, y, (float)widths[i], bh), text[i], sizes[i], .70f, F5Command.None);
                used = next;
            }
            float ph = y + bh + 8;
            if (!Finite(pw) || !Finite(ph) || ph > h / s - 32) return false;
            Panel = new F5Rect((w / s - pw) / 2, h / s - ph - 24, pw, ph);
            Buttons = nextButtons; actions = nextActions;
            width = w; height = h; scale = s; font = f; active = mode; projectionMode = projection; builtPreviewRevision = previewRevision; Generation++;
            return true;
        }
        internal int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Length; i++) if (Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return i; return -1; }
        internal bool Available(int index) { return index >= 0 && index < Buttons.Length; }
        internal string Hint(int index)
        { return Action(index) == BlueprintToolbarAction.Save ? BlueprintControls.SaveHint : Action(index) == BlueprintToolbarAction.ExitCapture ? BlueprintControls.ExitHint : Action(index) == BlueprintToolbarAction.Capture ? BlueprintControls.StartHint : null; }
        private static string Label(BlueprintToolbarAction action, int projection)
        {
            switch (action)
            {
                case BlueprintToolbarAction.Save: return "保存蓝图";
                case BlueprintToolbarAction.ExitCapture: return "退出采集";
                case BlueprintToolbarAction.Capture: return "采集蓝图";
                case BlueprintToolbarAction.Library: return "打开蓝图库";
                case BlueprintToolbarAction.Move: return projection == 3 ? "取消移动" : "移动蓝图";
                case BlueprintToolbarAction.Remove: return projection == 4 ? "取消移除" : "移除蓝图";
                case BlueprintToolbarAction.Mirror: return projection == 5 ? "取消镜像" : "水平镜像";
                case BlueprintToolbarAction.Trim: return projection == 6 || projection == 7 ? "取消修剪" : "局部修剪";
                case BlueprintToolbarAction.ConfirmTrim: return "确定修剪";
                case BlueprintToolbarAction.CancelPlace: return "取消摆放";
                case BlueprintToolbarAction.RemoveAll: return "移除所有";
                default: return "材料";
            }
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
