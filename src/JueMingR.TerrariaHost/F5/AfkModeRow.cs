using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.AfkMode;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.F5
{
    // Presentation owns only the draft and its input lease. Enter changes the
    // domain's in-memory preference; leaving the page discards an uncommitted edit.
    internal sealed class AfkModeRow : ITextEditSession
    {
        internal const string Placeholder = "双击输入空档";
        private static readonly F5RowDescription description = new F5RowDescription(HostAfkMode.ActionId,
            "开启功能后，手持道具，按一下左键开始持续使用这个道具");
        private readonly HostAfkMode host;
        private readonly F5Interaction shell;
        private readonly List<F5Element> rows = new List<F5Element>();
        private readonly List<F5Rect> names = new List<F5Rect>();
        private readonly SingleLineEditView editView = new SingleLineEditView();
        internal readonly TextEditInput TextInput;
        private TextEditBuffer editor;
        private bool enabled, available, dirty = true, clicked;
        private int gap, lastClick, clickGeneration, clickSkin;
        private F5Rect field, clickRect;
        private Matrix clickMatrix;
        private F5Size unitSize, valueSize;
        private string valueText, validation;
        private long editRevision = -1, caretRevision = -1;
        private string composition;
        private float editWidth = -1;
        internal float Height { get; private set; }
        internal bool NeedsBuild { get { return dirty || enabled != host.ModeEnabled || available != host.ControlsEnabled || gap != host.GapMilliseconds; } }
        internal bool OwnsTextToken { get { return TextInput.OwnsTextToken; } }
        public TextEditBuffer Editor { get { return editor; } }
        internal AfkModeRow(HostAfkMode host, F5Interaction shell)
        {
            this.host = host; this.shell = shell;
            TextInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle));
        }
        internal void Build(float start, float width, Func<string, float, F5Size> measure)
        {
            enabled = host.ModeEnabled; available = host.ControlsEnabled; gap = host.GapMilliseconds;
            unitSize = measure("ms", .7f);
            valueText = gap == 0 ? Placeholder : gap.ToString(CultureInfo.InvariantCulture);
            valueSize = shell.Layout.DynamicTextSize(valueText, .7f);
            // Reserve measured room for both the placeholder and every valid
            // Int32 value. The fixed unit never belongs to the editable buffer.
            float fieldWidth = Math.Max(valueSize.Width, Math.Max(measure(Placeholder, .7f).Width, measure(int.MaxValue.ToString(CultureInfo.InvariantCulture), .7f).Width)) + unitSize.Width + 22;
            rows.Clear(); float y = start;
            F5PairedRowLayout.Row(rows, measure, ref y, width, "挂机模式", "开启", true,
                new[] { "关闭", "键" }, description, fieldWidth);
            if (validation != null)
            {
                var panel = rows[0]; y -= F5RowLayout.GroupGap;
                new F5RowLayout(rows, measure).TextLines(validation, 8, ref y, width - 16, .7f);
                rows[0] = F5RowLayout.CompleteGroup(panel, y);
                y = rows[0].Rect.Bottom + F5RowLayout.GroupGap;
            }
            Height = y; dirty = false; editWidth = -1;
        }
        internal void Project(F5Rect view, float scroll, List<ItemUiControl> controls, List<F5Element> elements)
        {
            names.Clear(); field = default(F5Rect); clicked = false;
            foreach (var e in rows)
            {
                if (e.Rect.Bottom <= scroll || e.Rect.Y >= scroll + view.Height) continue;
                var rect = e.Rect.Offset(view.X, view.Y - scroll);
                var projected = new F5Element(e.Kind, rect, e.Text, e.TextSize, e.TextScale, e.Command,
                    e.Kind == F5ElementKind.Hotkey ? HostAfkMode.ActionId : e.HotkeyTarget,
                    e.Description, e.HintRect.Offset(view.X, view.Y - scroll));
                if (e.Kind == F5ElementKind.Field) field = rect;
                if (e.Kind == F5ElementKind.Button || e.Kind == F5ElementKind.Hotkey || e.Kind == F5ElementKind.Field)
                    controls.Add(new ItemUiControl {
                        Command = e.Kind == F5ElementKind.Hotkey ? ItemUiCommand.Hotkey : e.Kind == F5ElementKind.Field ? ItemUiCommand.AfkGap : ItemUiCommand.Afk,
                        Argument = e.Text == "开启" ? 1 : 0, Rect = rect, Element = projected,
                        Enabled = available, Selected = e.Kind == F5ElementKind.Button && enabled == (e.Text == "开启") });
                else { elements.Add(projected); if (e.Description != null) names.Add(projected.HintRect); }
            }
        }
        internal void PrepareEdit(Func<string, float, F5Size> measure)
        {
            if (editor == null || field.Width <= 0) return;
            float width = Math.Max(0, field.Width - unitSize.Width - 22);
            if (editRevision == editor.Revision && caretRevision == editor.CaretRevision && composition == TextInput.Composition && editWidth == width) return;
            editView.Prepare(editor, TextInput.Composition, width, measure);
            editRevision = editor.Revision; caretRevision = editor.CaretRevision;
            composition = TextInput.Composition; editWidth = width;
        }
        internal void Click(ItemUiControl control, Matrix matrix, int generation, int skin)
        {
            if (!host.ControlsEnabled) return;
            if (control.Command == ItemUiCommand.Afk) { CancelEdit(); host.SetModeEnabled(control.Argument != 0); return; }
            if (editor != null) return;
            int now = Environment.TickCount;
            bool twice = clicked && unchecked((uint)(now - lastClick)) <= 500 && clickRect.Equals(control.Rect) &&
                clickMatrix == matrix && clickGeneration == generation && clickSkin == skin;
            lastClick = now; clickRect = control.Rect; clickMatrix = matrix; clickGeneration = generation; clickSkin = skin; clicked = !twice;
            if (!twice) return;
            validation = null; dirty = true;
            editor = new TextEditBuffer(host.GapMilliseconds == 0 ? "" : host.GapMilliseconds.ToString(CultureInfo.InvariantCulture), true, 16384, 65536, "输入内容过长。");
            editRevision = -1; editor.SelectAll(); TextInput.PrepareEditor();
        }
        internal void PointerPressed(ItemUiControl control)
        {
            if (control?.Command == ItemUiCommand.AfkGap) return;
            clicked = false;
            if (editor != null) CancelEdit();
        }
        public bool RequestFinish()
        {
            if (editor == null) return true;
            TextInput.FinishComposition(false); if (TextInput.HasComposition) return false;
            int milliseconds = 0;
            if (editor.Text.Length != 0 && !int.TryParse(editor.Text, NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds))
            { validation = "请输入 0 到 2147483647 之间的整数毫秒。"; dirty = true; return false; }
            if (!host.ControlsEnabled) return false;
            host.SetGapMilliseconds(milliseconds);
            // PreferenceDocument accepts this change synchronously in memory;
            // its worker owns persistence. Saving never requests an AFK start.
            CancelEdit(); return true;
        }
        public void CancelEdit()
        { TextInput.Release(true); editor = null; validation = null; clicked = false; dirty = true; editRevision = -1; }
        public void PreserveUncommittedInput(TextEditBuffer value) { }
        internal void Suspend() { if (editor != null || validation != null) CancelEdit(); clicked = false; }
        internal string Hint(float x, float y, out F5Rect rect)
        {
            foreach (var name in names) if (name.Contains(x, y)) { rect = name; return host.StatusMessage ?? description.Text; }
            rect = default(F5Rect); return null;
        }
        internal void DrawField(ItemsRenderer renderer, ItemUiControl control, bool hovered)
        {
            var rect = control.Rect; renderer.ItemButton(rect, control.Enabled, hovered);
            float unitX = rect.Right - unitSize.Width - 8;
            renderer.Label(new F5Element(F5ElementKind.Text, new F5Rect(unitX, rect.Y + (rect.Height - unitSize.Height) / 2, unitSize.Width, unitSize.Height), "ms", unitSize, .7f, F5Command.None));
            if (editor == null)
            {
                renderer.Label(new F5Element(F5ElementKind.Text, new F5Rect(rect.X + 6, rect.Y + (rect.Height - valueSize.Height) / 2, valueSize.Width, valueSize.Height), valueText, valueSize, .7f, F5Command.None), gap == 0 ? Color.LightGray : Color.White);
                return;
            }
            // SingleLineEditView clips at complete text elements within only
            // the numeric region; caret/selection can never overwrite "ms".
            var pixel = TextureAssets.MagicPixel.Value;
            if (editView.SelectionRight > editView.SelectionLeft)
                Main.spriteBatch.Draw(pixel, new Vector2(rect.X + 6 + editView.SelectionLeft, rect.Y + 4), new Rectangle(0, 0, 1, 1), Color.CornflowerBlue, 0, Vector2.Zero, new Vector2(editView.SelectionRight - editView.SelectionLeft, rect.Height - 8), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            renderer.Label(new F5Element(F5ElementKind.Text, new F5Rect(rect.X + 6, rect.Y + (rect.Height - editView.Size.Height) / 2, editView.Size.Width, editView.Size.Height), editView.Text, editView.Size, .7f, F5Command.None));
            Main.spriteBatch.Draw(pixel, new Vector2(rect.X + 6 + editView.Caret, rect.Y + 5), new Rectangle(0, 0, 1, 1), Color.White, 0, Vector2.Zero, new Vector2(1, rect.Height - 10), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Main.instance.SetIMEPanelAnchor(new Vector2(rect.X + 6 + editView.Caret, rect.Bottom + 32), 0);
        }
    }
}
