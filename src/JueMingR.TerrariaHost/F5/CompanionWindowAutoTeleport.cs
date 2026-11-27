using System;
using System.Globalization;
using JueMingR.Features.Text;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private Control autoTeleportAnchor;
        private readonly AutoTeleportPopup autoTeleportPopup;
        private bool autoTeleportLeftTail, autoTeleportRightTail;

        private sealed class AutoTeleportPopup : ITextEditSession
        {
            private enum Hit { None, Close, BlockedCheck, BlockedValue, DistanceCheck, DistanceValue }

            private sealed class Condition
            {
                internal readonly string Name, Unit;
                internal readonly CompanionSetting EnabledSetting, ValueSetting;
                internal F5Rect Check, Label, Field, UnitBounds;
                internal bool Enabled;
                internal int Value;
                internal string DisplayValue;

                internal Condition(string name, string unit, CompanionSetting enabledSetting, CompanionSetting valueSetting)
                { Name = name; Unit = unit; EnabledSetting = enabledSetting; ValueSetting = valueSetting; }

                internal void Read(CompanionBehavior behavior)
                {
                    Enabled = behavior.SettingValue(EnabledSetting) != 0;
                    int value = behavior.SettingValue(ValueSetting);
                    if (DisplayValue == null || Value != value)
                    { Value = value; DisplayValue = value.ToString(CultureInfo.InvariantCulture); }
                }
            }

            private const string Title = "自动传送";
            private const string InvalidValue = "请输入大于 0 的整数。";
            private const float Padding = 12, Gap = 8;
            private readonly CompanionWindow window;
            private readonly TextEditInput textInput;
            private readonly SingleLineEditView editView = new SingleLineEditView();
            private readonly Condition blocked = new Condition("连续受阻", "秒",
                CompanionSetting.AutoTeleportBlockedEnabled, CompanionSetting.AutoTeleportBlockedSeconds);
            private readonly Condition distance = new Condition("距离玩家", "格",
                CompanionSetting.AutoTeleportDistanceEnabled, CompanionSetting.AutoTeleportDistanceTiles);
            private Guid owner, profile;
            private long session, epoch, revision;
            private F5Rect panel, anchor, close, title, errorLine;
            private Matrix matrix;
            private Vector2 screen;
            private int skin;
            private Hit pressed;
            private bool pressedValue;
            private Condition editing;
            private string validation, composition;
            private long editRevision = -1, caretRevision = -1;

            internal bool Visible => profile != Guid.Empty;
            internal bool OwnsTextToken => textInput.OwnsTextToken;
            public TextEditBuffer Editor { get; private set; }

            internal AutoTeleportPopup(CompanionWindow window)
            {
                this.window = window;
                textInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle));
            }

            internal void Toggle(CompanionProfile target)
            {
                bool opening = !Visible;
                Close();
                if (!opening) return;
                owner = target.OwnerId;
                profile = target.Id;
                epoch = target.DataEpoch;
                session = window.host.CreationSessionToken;
                revision = -1;
            }

            internal void Close()
            {
                if (!Visible) return;
                CancelEdit();
                profile = Guid.Empty;
                panel = default(F5Rect);
                pressed = Hit.None;
            }

            internal bool Contains(float x, float y) => Visible && panel.Contains(x, y);
            private bool Matches(CompanionProfile current) => current != null && current.Id == profile && current.OwnerId == owner &&
                current.DataEpoch == epoch && window.host.CreationSessionToken == session;

            internal void BeforeInput(bool active)
            {
                // Retire a draft before the sampler can deliver queued text to
                // another selection, archive epoch or session.
                if (Visible && (!active || !Matches(window.CurrentProfile()))) Close();
                textInput.BeforeSample(active && Visible);
            }

            internal void SampleInactive(KeyboardState keys, bool focused) => textInput.AfterSample(false, null, keys, focused);

            internal void Prepare()
            {
                if (!Visible) return;
                CompanionProfile current = window.CurrentProfile();
                Control entry = window.autoTeleportAnchor;
                if (!window.Visible || !window.ready || window.page != 1 || window.fishingPage || entry == null || !Matches(current))
                { Close(); return; }
                if (revision != current.Revision)
                {
                    CompanionBehavior behavior = current.Behavior;
                    blocked.Read(behavior);
                    distance.Read(behavior);
                    revision = current.Revision;
                }

                if (panel.Width == 0 || !anchor.Equals(entry.Rect) || matrix != window.matrix || screen != window.screen || skin != window.skin)
                {
                    // A projection can move under a held press. Keep the
                    // parent's mouse tail, but retire the old hit and draft.
                    pressed = Hit.None;
                    CancelEdit();
                    anchor = entry.Rect; matrix = window.matrix; screen = window.screen; skin = window.skin;
                    Layout();
                }
                PrepareEditor();
            }

            private void Layout()
            {
                float checkSize = 22;
                // ItemsRenderer.Text subtracts its outline before fitting. An
                // exact floating-point fit can otherwise ellipsize a single glyph.
                float labelWidth = (float)Math.Ceiling(Math.Max(window.measure(blocked.Name, .7f).Width,
                    window.measure(distance.Name, .7f).Width)) + 2;
                float unitWidth = (float)Math.Ceiling(Math.Max(window.measure(blocked.Unit, .7f).Width,
                    window.measure(distance.Unit, .7f).Width)) + 2;
                float widestDigit = 0;
                foreach (string digit in new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" })
                    widestDigit = Math.Max(widestDigit, window.measure(digit, .7f).Width);
                float fieldWidth = widestDigit * 10 + 14;
                float width = Math.Max(260, 2 * Padding + checkSize + labelWidth + fieldWidth + unitWidth + 3 * Gap);
                float row = Math.Max(30, window.rowHeight);
                float errorHeight = window.measure(InvalidValue, .65f).Height + 6;
                panel = F5PopupPlacement.Place(anchor, width, 2 * Padding + 3 * row + 2 * Gap + errorHeight,
                    screen.X / matrix.M11, screen.Y / matrix.M22);
                float x = panel.X + Padding, y = panel.Y + Padding;
                title = new F5Rect(x, y, width - 2 * Padding - row, row);
                close = new F5Rect(panel.Right - Padding - row, y, row, row);
                LayoutCondition(blocked, y + row + Gap);
                LayoutCondition(distance, y + 2 * row + 2 * Gap);
                errorLine = new F5Rect(x, distance.Field.Bottom + 4, width - 2 * Padding, errorHeight);

                void LayoutCondition(Condition condition, float top)
                {
                    condition.Check = new F5Rect(x, top + (row - checkSize) / 2, checkSize, checkSize);
                    condition.Label = new F5Rect(condition.Check.Right + Gap, top, labelWidth, row);
                    condition.Field = new F5Rect(condition.Label.Right + Gap, top, fieldWidth, row);
                    condition.UnitBounds = new F5Rect(condition.Field.Right + Gap, top, unitWidth, row);
                }
            }

            private Hit Target(Vector2 point)
            {
                if (close.Contains(point.X, point.Y)) return Hit.Close;
                if (blocked.Check.Contains(point.X, point.Y)) return Hit.BlockedCheck;
                if (blocked.Field.Contains(point.X, point.Y)) return Hit.BlockedValue;
                if (distance.Check.Contains(point.X, point.Y)) return Hit.DistanceCheck;
                if (distance.Field.Contains(point.X, point.Y)) return Hit.DistanceValue;
                return Hit.None;
            }

            internal bool Process(KeyboardState keys, Vector2 point, bool left, bool right, int wheel)
            {
                bool wasEditing = Editor != null;
                textInput.AfterSample(Visible, null, keys, window.input.SampleFocused);
                if (!Visible) return false;
                bool inside = Contains(point.X, point.Y);
                bool newLeft = left && !window.previousLeft, newRight = right && !window.previousRight;
                bool escape = window.input.Hotkeys.IsNew((int)Keys.Escape);
                if (!inside && !wasEditing && pressed == Hit.None && !escape) return false;
                // Keep this sample owned even when the close control or Escape
                // removes the popup before the shell consumes pointer input.
                window.OwnsPointer = inside || wasEditing || pressed != Hit.None;
                window.ConsumeWheel = window.OwnsPointer && wheel != 0;
                if (window.OwnsPointer && (newLeft || newRight))
                {
                    window.ClaimAutoTeleportClick(left, right);
                }
                Hit hit = inside ? Target(point) : Hit.None;
                if (escape)
                {
                    window.input.ConsumeCancellation((int)Keys.Escape);
                    if (!wasEditing) Close();
                }
                else if (wasEditing)
                {
                    // A click leaving the field abandons the draft and owns
                    // the entire gesture. The next press may activate a control.
                    if (newLeft && editing != null && !editing.Field.Contains(point.X, point.Y)) CancelEdit();
                }
                else if (newLeft)
                {
                    pressed = hit;
                    if (hit == Hit.BlockedCheck) pressedValue = !blocked.Enabled;
                    else if (hit == Hit.DistanceCheck) pressedValue = !distance.Enabled;
                }
                else if (!left)
                {
                    Hit target = pressed;
                    pressed = Hit.None;
                    if (window.previousLeft && target == hit) Execute(target);
                }
                window.armedKey = null;
                window.hint.Hide();
                window.ReleaseTails(left, right, true);
                window.previousLeft = left;
                window.previousRight = right;
                return true;
            }

            private void Execute(Hit hit)
            {
                switch (hit)
                {
                    case Hit.Close: Close(); break;
                    case Hit.BlockedCheck: Set(blocked.EnabledSetting, pressedValue ? 1 : 0); break;
                    case Hit.DistanceCheck: Set(distance.EnabledSetting, pressedValue ? 1 : 0); break;
                    case Hit.BlockedValue: StartEdit(blocked); break;
                    case Hit.DistanceValue: StartEdit(distance); break;
                }
            }

            private bool Set(CompanionSetting setting, int value)
            {
                if (!Matches(window.CurrentProfile())) return false;
                return window.host.SetAutoTeleportSetting(owner, profile, session, epoch, setting, value);
            }

            private void StartEdit(Condition condition)
            {
                if (textInput.OtherTextOwner) return;
                editing = condition;
                Editor = new TextEditBuffer(condition.DisplayValue, true, 256, 1024, "输入内容过长。");
                Editor.SelectAll();
                textInput.PrepareEditor();
                PrepareEditor();
            }

            public bool RequestFinish()
            {
                if (Editor == null) return true;
                textInput.FinishComposition(false);
                if (textInput.HasComposition) return false;
                if (!int.TryParse(Editor.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0)
                { validation = InvalidValue; return false; }
                if (!Set(editing.ValueSetting, value))
                {
                    if (!Matches(window.CurrentProfile())) Close();
                    else validation = window.host.Feedback;
                    return false;
                }
                CancelEdit();
                return true;
            }

            public void CancelEdit()
            {
                textInput.Release(true);
                Editor = null;
                textInput.ForgetRetiredDraft();
                editing = null;
                validation = null;
                editRevision = -1;
            }

            public void PreserveUncommittedInput(TextEditBuffer editor) { }

            private void PrepareEditor()
            {
                if (Editor == null || editRevision == Editor.Revision && caretRevision == Editor.CaretRevision &&
                    composition == textInput.Composition) return;
                editView.Prepare(Editor, textInput.Composition, editing.Field.Width - 12, window.measure);
                editRevision = Editor.Revision;
                caretRevision = Editor.CaretRevision;
                composition = textInput.Composition;
            }

            internal void Draw()
            {
                if (!Visible) return;
                var renderer = window.renderer;
                renderer.Pass(matrix, panel, () =>
                {
                    renderer.Panel(panel);
                    renderer.Text(Title, title, Color.White, .7f);
                    renderer.Cross(close, true);
                    DrawCondition(blocked);
                    DrawCondition(distance);
                    string error = validation ?? textInput.Error ?? Editor?.Error;
                    if (error != null) renderer.Text(error, errorLine, Color.Orange, .65f);
                });
            }

            private void DrawCondition(Condition condition)
            {
                var renderer = window.renderer;
                Vector2 point = window.pointer;
                var check = condition.Check;
                renderer.ItemButton(check, true, check.Contains(point.X, point.Y));
                if (condition.Enabled) renderer.Selection(check);
                renderer.Text(condition.Name, condition.Label, Color.White, .7f);
                renderer.Text(condition.Unit, condition.UnitBounds, Color.White, .7f);
                var field = condition.Field;
                renderer.ItemButton(field, true, field.Contains(point.X, point.Y));
                var content = new F5Rect(field.X + 6, field.Y, field.Width - 12, field.Height);
                if (editing != condition)
                {
                    renderer.Text(condition.DisplayValue, content, Color.White, .7f);
                    return;
                }
                Fill(new F5Rect(content.X + editView.SelectionLeft, field.Y + 4,
                    editView.SelectionRight - editView.SelectionLeft, field.Height - 8), Color.CornflowerBlue);
                renderer.Label(new F5Element(F5ElementKind.Text, new F5Rect(content.X, field.Y + (field.Height - editView.Size.Height) / 2,
                    editView.Size.Width, editView.Size.Height), editView.Text, editView.Size, .7f, F5Command.None));
                Fill(new F5Rect(content.X + editView.Caret, field.Y + 5, 1, field.Height - 10), Color.White);
                Main.instance.SetIMEPanelAnchor(new Vector2(content.X + editView.Caret, field.Bottom + 32), 0);
            }
        }

        private void ClaimAutoTeleportClick(bool left, bool right)
        {
            // The native mapper already merged this sample. Consume the one
            // handled sample now and retain only the physical pointer source.
            if (left && !previousLeft)
            {
                leftTail = true;
                autoTeleportLeftTail = input.Hotkeys.IsNew(256);
                input.ClaimUiMousePress(256);
            }
            if (right && !previousRight)
            {
                rightTail = true;
                autoTeleportRightTail = input.Hotkeys.IsNew(257);
                input.ClaimUiMousePress(257);
            }
            input.ConsumeHotkeyActions();
        }
    }
}
