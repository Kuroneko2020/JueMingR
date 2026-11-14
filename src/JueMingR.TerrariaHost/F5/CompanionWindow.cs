using System;
using System.Collections.Generic;
using JueMingR.Features.Text;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // The window owns selection, drafts and physical gestures only. Every item
    // command reads the current native inventories again at the Host outlet.
    internal sealed partial class CompanionWindow : ITextEditSession
    {
        private sealed class Control
        {
            internal string Key, Text;
            internal F5Rect Rect;
            internal bool Enabled = true, Selected, Off;
            internal Action Execute;
            internal int ItemType, Stack;
            internal Player Portrait;
            internal F5Element Element;
            internal string Hint;
        }
        private readonly HostCompanions host;
        private readonly HostInputState input;
        private readonly Func<string, float, F5Size> measure;
        private readonly Func<string, float, F5Size> measureSource;
        private readonly Dictionary<string, F5Size> sizes70 = new Dictionary<string, F5Size>();
        private readonly Dictionary<string, F5Size> sizes65 = new Dictionary<string, F5Size>();
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly PreparedItemIcons icons = new PreparedItemIcons();
        private readonly Terraria.Graphics.Renderers.LegacyPlayerRenderer portraits = new Terraria.Graphics.Renderers.LegacyPlayerRenderer();
        private readonly List<Control> controls = new List<Control>();
        private readonly List<F5Element> labels = new List<F5Element>();
        private readonly TextEditInput textInput;
        private readonly SingleLineEditView editView = new SingleLineEditView();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly List<string> visibleStatuses = new List<string>();
        private readonly List<Guid> visibleIds = new List<Guid>();
        private readonly List<string> visibleNames = new List<string>();
        private readonly List<Player> visibleBodies = new List<Player>();
        private readonly List<int> itemValues = new List<int>();
        private int preparedGeneration = -1, previousProfileCount = -1, life, mana, maximumLife, maximumMana, weapon;
        private long profileRevision;
        private bool transferable, editing, saveFailed;
        private string feedbackValue, noticeValue, selectedStatus;
        private Player selectedBody;
        private float rowHeight;
        private long viewDataEpoch;
        private long editRevision = -1, caretRevision = -1;
        private string editComposition, editError;
        private float editWidth = -1;
        private F5Element editElement;
        private TextEditBuffer editor;
        private string editKind, armedKey, notice, pendingCreatedName;
        private F5Rect panel, listView, bodyView, field;
        private Matrix matrix;
        private Vector2 screen, pointer;
        private Guid selected;
        private int page, listOffset, bodyOffset, listMaximum, bodyMaximum, generation, armedGeneration, skin;
        private bool previousLeft, leftTail, rightTail, ready, creating, confirmingReset;
        private bool sourceOwner;
        private CompanionInventoryArea sourceArea;
        private int sourceSlot = -1;
        private string sourceIdentity;
        public TextEditBuffer Editor { get { return editor; } }
        internal bool Visible { get; private set; }
        internal bool Captured { get { return armedKey != null || leftTail || rightTail; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal bool OwnsTextToken { get { return textInput.OwnsTextToken; } }
        internal int LayoutBuilds { get; private set; }
        internal CompanionWindow(HostCompanions host, HostInputState input, Func<string, float, F5Size> measure)
        {
            this.host = host; this.input = input; measureSource = measure; this.measure = MeasureCached;
            textInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle));
        }
        internal void Toggle(F5Rect anchor)
        {
            if (Visible) { Close(); return; }
            Visible = true; ready = false; notice = null;
            // The entry's release belongs to the entry. A held button cannot
            // become a fresh slot press when this upper surface appears.
            previousLeft = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed;
            armedKey = null; generation++;
        }
        internal bool Contains(float x, float y) { return Visible && ready && panel.Contains(x, y); }
        internal void BeforeInput(bool active) { textInput.BeforeSample(active && Visible); }
        internal void Close()
        {
            if (!Visible && !ready && editor == null) return;
            Visible = false; ready = false; armedKey = null; creating = confirmingReset = false;
            CancelEdit(); icons.Clear(); renderer.Dispose(); controls.Clear(); labels.Clear(); generation++;
            // Keep already owned physical tails until the shared trusted sample
            // observes release, including close, lost focus and world departure.
        }
        internal void PrepareLayout(Matrix transform, Vector2 physical) { PrepareCore(transform, physical, false); }
        internal void Prepare(Matrix transform, Vector2 physical) { PrepareCore(transform, physical, true); }
        private void PrepareCore(Matrix transform, Vector2 physical, bool resources)
        {
            if (!Visible) return;
            if (resources && !renderer.Refresh() || transform.M11 <= 0 || transform.M22 <= 0) { Close(); return; }
            Vector2 logical = physical / transform.M11;
            float width = Math.Min(860, logical.X - 24), height = Math.Min(710, logical.Y - 24);
            if (width < 520 || height < 270) { Close(); return; }
            var next = new F5Rect((logical.X - width) / 2, (logical.Y - height) / 2, width, height);
            if (!panel.Equals(next) || matrix != transform || screen != physical || skin != renderer.Generation)
            { armedKey = null; generation++; }
            if (skin != renderer.Generation) { sizes70.Clear(); sizes65.Clear(); hint.Clear(); editRevision = -1; }
            if (skin != renderer.Generation || rowHeight == 0) rowHeight = Math.Max(34, measure("主人附近优先", .7f).Height + 14);
            panel = next; matrix = transform; screen = physical; skin = renderer.Generation;
            float row = rowHeight;
            listView = new F5Rect(panel.X + 10, panel.Y + row + 8, Math.Max(142, width * .24f), height - row * 2 - 28);
            bodyView = new F5Rect(listView.Right + 12, listView.Y + row + 8, panel.Right - listView.Right - 24, panel.Bottom - listView.Y - row - 64);
            if (ready && !ViewChanged()) { if (resources) icons.Prepare(); PrepareEditPresentation(); return; }
            controls.Clear(); labels.Clear(); icons.BeginVisibleSet();
            Text("AI 队友", new F5Rect(panel.X + 12, panel.Y + 4, width - 64, row));
            Button("close", "×", new F5Rect(panel.Right - 40, panel.Y + 4, 30, row), Close);
            BuildList(row);
            viewDataEpoch = CurrentProfile()?.DataEpoch ?? 0;
            Button("create", "＋", new F5Rect(listView.X, panel.Bottom - row - 12, listView.Width, row), () => { creating = true; confirmingReset = false; bodyOffset = 0; BeginEdit("name", ""); Invalidate(); });
            Button("inventory", "背包", new F5Rect(bodyView.X, listView.Y, bodyView.Width / 2 - 3, row), () => SelectPage(0), true, page == 0);
            Button("configuration", "配置", new F5Rect(bodyView.X + bodyView.Width / 2 + 3, listView.Y, bodyView.Width / 2 - 3, row), () => SelectPage(1), true, page == 1);
            int bodyControlStart = controls.Count, bodyLabelStart = labels.Count;
            bodyOffsetClamped = false;
            if (creating) BuildCreate(row); else BuildSelected(row);
            if (bodyOffsetClamped)
            {
                // Content height is independent of the scroll offset. Shrinking
                // content can clamp that offset only after the first projection;
                // replace those cells now, before hit testing or caching its epoch.
                controls.RemoveRange(bodyControlStart, controls.Count - bodyControlStart);
                labels.RemoveRange(bodyLabelStart, labels.Count - bodyLabelStart);
                icons.BeginVisibleSet(); bodyOffsetClamped = false;
                if (creating) BuildCreate(row); else BuildSelected(row);
            }
            Text(notice ?? textInput.Error ?? editor?.Error ?? host.Feedback, new F5Rect(bodyView.X, panel.Bottom - 54, bodyView.Width, 44), .65f);
            icons.EndVisibleSet(); if (resources) icons.Prepare(); ready = true; LayoutBuilds++; CaptureView(); PrepareEditPresentation();
        }
        private void BuildList(float row)
        {
            var profiles = host.Profiles;
            if (pendingCreatedName != null) for (int i = 0; i < profiles.Count; i++)
                if (String.Equals(profiles[i].Name, pendingCreatedName, StringComparison.OrdinalIgnoreCase))
                { selected = profiles[i].Id; pendingCreatedName = null; sourceSlot = -1; break; }
            int visible = Math.Max(1, (int)(listView.Height / (row + 16)));
            listMaximum = Math.Max(0, profiles.Count - visible); listOffset = Math.Max(0, Math.Min(listOffset, listMaximum));
            if (profiles.Count == 0) { Text("点击左下角＋创建队友", listView, .65f); return; }
            bool found = false; for (int i = 0; i < profiles.Count; i++) if (profiles[i].Id == selected) found = true;
            if (!found) { selected = profiles[0].Id; sourceSlot = -1; confirmingReset = false; }
            for (int i = listOffset; i < Math.Min(profiles.Count, listOffset + visible); i++)
            {
                var profile = profiles[i]; Guid id = profile.Id;
                var rect = new F5Rect(listView.X, listView.Y + (i - listOffset) * (row + 16), listView.Width, row + 12);
                var c = Button("profile:" + id, profile.Name, rect, () => SelectProfile(id), true, selected == id);
                c.Portrait = host.GetBody(id);
                c.Hint = profile.Name + "\n" + host.Status(id);
                c.Element = null;
                Text(profile.Name, new F5Rect(rect.X + 42, rect.Y + 1, rect.Width - 44, row / 2 + 8), .65f);
                Text(host.Status(id), new F5Rect(rect.X + 42, rect.Y + row / 2 + 5, rect.Width - 44, row / 2 + 5), .58f);
            }
        }
        private void SelectProfile(Guid id)
        { CancelEdit(); selected = id; creating = confirmingReset = false; sourceSlot = -1; bodyOffset = 0; notice = null; Invalidate(); }
        private void SelectPage(int value)
        { CancelEdit(); creating = confirmingReset = false; page = value; bodyOffset = 0; Invalidate(); }
        private void Invalidate() { armedKey = null; generation++; }
        private F5Size MeasureCached(string text, float scale)
        {
            if (scale != .7f && scale != .65f) return measureSource(text, scale);
            var cache = scale == .7f ? sizes70 : sizes65; F5Size size;
            if (!cache.TryGetValue(text, out size))
            { if (cache.Count >= 256) cache.Clear(); cache[text] = size = measureSource(text, scale); }
            return size;
        }
        // Stable frames compare bounded visible facts and fixed native slot
        // arrays. They neither remeasure nor allocate all of the window's cells.
        // This is a presentation cache, never the transfer authority.
        private bool ViewChanged()
        {
            if (preparedGeneration != generation || editing != (editor != null) || feedbackValue != host.Feedback || noticeValue != notice || editError != (textInput.Error ?? editor?.Error)) return true;
            var profiles = host.Profiles;
            if (profiles.Count != previousProfileCount) return true;
            for (int i = 0; i < visibleIds.Count; i++)
            {
                int index = listOffset + i;
                if (index >= profiles.Count || profiles[index].Id != visibleIds[i] || profiles[index].Name != visibleNames[i] ||
                    host.Status(profiles[index].Id) != visibleStatuses[i] || !ReferenceEquals(host.GetBody(profiles[index].Id), visibleBodies[i])) return true;
            }
            var profile = CurrentProfile(); var body = host.GetBody(selected);
            if ((profile?.Revision ?? 0) != profileRevision || !ReferenceEquals(body, selectedBody) || host.CanTransfer(selected) != transferable || host.SaveFailed(selected) != saveFailed || host.Status(selected) != selectedStatus) return true;
            if (body != null && (life != body.statLife || mana != body.statMana || maximumLife != body.statLifeMax2 || maximumMana != body.statManaMax2 || weapon != body.selectedItem)) return true;
            int offset = 0;
            if (!InventoryMatches(body?.inventory, 58, ref offset) || !InventoryMatches(body?.armor, body?.armor?.Length ?? 0, ref offset) ||
                !InventoryMatches(body?.dye, body?.dye?.Length ?? 0, ref offset) ||
                !InventoryMatches(body?.miscEquips, body?.miscEquips?.Length ?? 0, ref offset) ||
                !InventoryMatches(body?.miscDyes, body?.miscDyes?.Length ?? 0, ref offset) ||
                !InventoryMatches(Main.LocalPlayer?.inventory, 58, ref offset)) return true;
            return offset != itemValues.Count;
        }
        private bool InventoryMatches(Item[] items, int count, ref int offset)
        {
            if (items == null) return true;
            for (int i = 0; i < Math.Min(count, items.Length); i++)
            {
                var item = items[i]; int type = item?.type ?? 0, stack = item?.stack ?? 0, prefix = item?.prefix ?? 0;
                if (offset + 3 > itemValues.Count || itemValues[offset++] != type || itemValues[offset++] != stack || itemValues[offset++] != prefix) return false;
            }
            return true;
        }
        private void CaptureView()
        {
            preparedGeneration = generation; editing = editor != null; feedbackValue = host.Feedback; noticeValue = notice; editError = textInput.Error ?? editor?.Error;
            var profiles = host.Profiles; previousProfileCount = profiles.Count;
            visibleIds.Clear(); visibleNames.Clear(); visibleStatuses.Clear(); visibleBodies.Clear();
            int count = Math.Max(1, (int)(listView.Height / (rowHeight + 16)));
            for (int i = listOffset; i < Math.Min(profiles.Count, listOffset + count); i++)
            { var p = profiles[i]; visibleIds.Add(p.Id); visibleNames.Add(p.Name); visibleStatuses.Add(host.Status(p.Id)); visibleBodies.Add(host.GetBody(p.Id)); }
            profileRevision = CurrentProfile()?.Revision ?? 0; selectedBody = host.GetBody(selected); transferable = host.CanTransfer(selected); saveFailed = host.SaveFailed(selected); selectedStatus = host.Status(selected);
            if (selectedBody != null) { life = selectedBody.statLife; mana = selectedBody.statMana; maximumLife = selectedBody.statLifeMax2; maximumMana = selectedBody.statManaMax2; weapon = selectedBody.selectedItem; }
            itemValues.Clear(); CaptureInventory(selectedBody?.inventory, 58); CaptureInventory(selectedBody?.armor, selectedBody?.armor?.Length ?? 0);
            CaptureInventory(selectedBody?.dye, selectedBody?.dye?.Length ?? 0);
            CaptureInventory(selectedBody?.miscEquips, selectedBody?.miscEquips?.Length ?? 0);
            CaptureInventory(selectedBody?.miscDyes, selectedBody?.miscDyes?.Length ?? 0);
            CaptureInventory(Main.LocalPlayer?.inventory, 58);
        }
        private void CaptureInventory(Item[] items, int count)
        { if (items != null) for (int i = 0; i < Math.Min(count, items.Length); i++) { var item = items[i]; itemValues.Add(item?.type ?? 0); itemValues.Add(item?.stack ?? 0); itemValues.Add(item?.prefix ?? 0); } }
        private Control Button(string key, string text, F5Rect rect, Action action, bool enabled = true, bool selectedValue = false, bool off = false)
        {
            var c = new Control { Key = key + "/" + selected + "/" + viewDataEpoch, Text = text, Rect = rect, Execute = action, Enabled = enabled, Selected = selectedValue, Off = off,
                Element = new F5Element(F5ElementKind.Button, rect, text, measure(text, .7f), .7f, F5Command.None) };
            controls.Add(c); return c;
        }
        private void Text(string text, F5Rect rect, float scale = .7f)
        {
            if (string.IsNullOrEmpty(text)) return;
            labels.Add(new F5Element(F5ElementKind.Text, rect, text, default(F5Size), scale, F5Command.None));
        }
        internal void Process(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel)
        { ProcessCore(active, keys, point, transform, physical, focused, wheel, true); }
        internal void ProcessLayoutInput(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel)
        { ProcessCore(active, keys, point, transform, physical, focused, wheel, false); }
        private void ProcessCore(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel, bool resources)
        {
            pointer = point; OwnsPointer = ConsumeWheel = false;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            bool wasEditing = editor != null;
            textInput.AfterSample(active && Visible && focused, null, keys, focused);
            if (!active || !focused) { Close(); previousLeft = focused ? left : true; ReleaseTails(left, right, focused); return; }
            if (!Visible) { previousLeft = left; ReleaseTails(left, right, true); return; }
            if (!wasEditing && editor == null && input.Hotkeys.IsNew((int)Keys.Escape)) { input.ConsumeCancellation((int)Keys.Escape); Close(); ReleaseTails(left, right, true); return; }
            PrepareCore(transform, physical, resources);
            OwnsPointer = Captured || Contains(point.X, point.Y);
            if (OwnsPointer) { if (left) leftTail = true; if (right) rightTail = true; }
            if (wheel != 0 && Contains(point.X, point.Y))
            {
                if (listView.Contains(point.X, point.Y)) listOffset = Math.Max(0, Math.Min(listMaximum, listOffset - Math.Sign(wheel)));
                else if (bodyView.Contains(point.X, point.Y)) bodyOffset = Math.Max(0, Math.Min(bodyMaximum, bodyOffset - Math.Sign(wheel) * 48));
                ConsumeWheel = true; Invalidate(); PrepareCore(transform, physical, resources);
            }
            Control hit = null;
            if (ready) foreach (var c in controls) if (c.Enabled && c.Rect.Contains(point.X, point.Y)) { hit = c; break; }
            if (left && !previousLeft) { armedKey = hit?.Key; armedGeneration = generation; }
            if (!left && previousLeft && armedKey != null && armedGeneration == generation && hit?.Key == armedKey)
            {
                var execute = hit.Execute; armedKey = null; execute?.Invoke();
                if (Visible) PrepareCore(transform, physical, resources);
            }
            ReleaseTails(left, right, true); previousLeft = left; PrepareHint();
        }
        private void ReleaseTails(bool left, bool right, bool focused)
        {
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!focused) return;
            if (!left) { leftTail = false; armedKey = null; } if (!right) rightTail = false;
        }
        public bool RequestFinish()
        {
            if (editor == null) return true;
            textInput.FinishComposition(false); if (textInput.HasComposition) return false;
            if (editKind == "name")
            {
                string name = editor.Text.Trim(); if (name.Length == 0) { notice = "请输入人物名字。"; return false; }
                var existing = host.Profiles;
                for (int i = 0; i < existing.Count; i++) if (String.Equals(existing[i].Name, name, StringComparison.OrdinalIgnoreCase))
                { Guid id = existing[i].Id; SelectProfile(id); notice = "已有此人物。"; return true; }
                if (!host.Create(name)) { notice = host.Feedback; return false; }
                pendingCreatedName = name;
                creating = false;
            }
            else host.ConnectionPassword = editor.Text;
            CancelEdit(); notice = null; Invalidate(); return true;
        }
        private void BeginEdit(string kind, string value)
        {
            if (editor != null && editKind == kind) return;
            CancelEdit(); notice = null; editKind = kind; editor = new TextEditBuffer(value, true, kind == "name" ? 20 : 256, 1024, "输入内容过长。");
            editor.SelectAll(); textInput.PrepareEditor();
        }
        public void CancelEdit() { textInput.Release(true); editor = null; editKind = null; editRevision = -1; editElement = null; }
        public void PreserveUncommittedInput(TextEditBuffer value) { }
        private void PrepareEditPresentation()
        {
            if (editor == null || field.Width <= 0) { editElement = null; return; }
            if (editRevision == editor.Revision && caretRevision == editor.CaretRevision && editComposition == textInput.Composition && editWidth == field.Width &&
                editElement != null && editElement.Rect.X == field.X + 6 && editElement.Rect.Y == field.Y + (field.Height - editView.Size.Height) / 2) return;
            TextEditBuffer display = editor; string composition = textInput.Composition;
            if (editKind == "password")
            {
                // The prepared glyph view and its bounded metrics cache contain
                // only mask glyphs, including the native IME composition preview.
                display = new TextEditBuffer(new string('•', editor.Text.Length), true, 1024, 1024, "");
                display.MoveTo(editor.Anchor); display.MoveTo(editor.Caret, true);
                composition = new string('•', composition.Length);
            }
            editView.Prepare(display, composition, field.Width - 12, measure);
            editElement = new F5Element(F5ElementKind.Text, new F5Rect(field.X + 6, field.Y + (field.Height - editView.Size.Height) / 2,
                editView.Size.Width, editView.Size.Height), editView.Text, editView.Size, .7f, F5Command.None);
            editRevision = editor.Revision; caretRevision = editor.CaretRevision; editComposition = textInput.Composition; editWidth = field.Width;
        }
        private void PrepareHint()
        {
            string hintText = null; F5Rect target = default(F5Rect);
            if (Visible && ready && editor == null && !Captured) foreach (var c in controls)
                if (c.Hint != null && c.Rect.Contains(pointer.X, pointer.Y)) { hintText = c.Hint; target = c.Rect; break; }
            hint.Prepare(hintText, target, new F5Rect(8, 8, screen.X / matrix.M11 - 16, screen.Y / matrix.M11 - 16), renderer, measure);
        }
        internal void Draw()
        {
            if (!Visible || !ready || !renderer.ResourcesCurrent) return;
            renderer.Pass(matrix, panel, () =>
            {
                renderer.Panel(panel); renderer.Panel(listView);
                foreach (var c in controls)
                {
                    bool hover = c.Rect.Contains(pointer.X, pointer.Y);
                    if (c.Element != null) renderer.Button(c.Element, c.Selected, c.Enabled, c.Off, hover);
                    else renderer.ItemButton(c.Rect, c.Enabled, hover);
                    if (c.Selected && c.Element == null) renderer.Selection(c.Rect);
                    if (c.Portrait != null) portraits.DrawPlayerHead(Main.Camera, c.Portrait, new Vector2(c.Rect.X + 21, c.Rect.Y + c.Rect.Height / 2), 1, .7f);
                    if (c.ItemType > 0)
                    {
                        renderer.PreparedItem(c.ItemType, icons.Get(c.ItemType), c.Rect);
                        renderer.Text(c.Stack.ToString(), new F5Rect(c.Rect.X + 1, c.Rect.Bottom - 15, c.Rect.Width - 2, 15), Color.White, .52f);
                    }
                }
                foreach (var e in labels) renderer.Text(e.Text, e.Rect, Color.White, e.TextScale);
                if (editor != null && field.Width > 0) DrawEditor();
                ScrollIndicator(listView, listOffset, listMaximum);
                ScrollIndicator(bodyView, bodyOffset, bodyMaximum);
            });
            if (hint.Visible && editor == null && !Captured) renderer.Pass(matrix, hint.Panel, () => { renderer.Panel(hint.Panel); foreach (var line in hint.Lines) renderer.Label(new F5Element(F5ElementKind.Text,line.Rect.Offset(hint.Panel.X+8,hint.Panel.Y+8),line.Text,line.TextSize,line.TextScale,F5Command.None)); });
        }
        private void DrawEditor()
        {
            if (editElement == null) return;
            var pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            if (editView.SelectionRight > editView.SelectionLeft)
                Main.spriteBatch.Draw(pixel, new Vector2(field.X + 6 + editView.SelectionLeft, field.Y + 4), new Rectangle(0, 0, 1, 1), Color.CornflowerBlue, 0,
                    Vector2.Zero, new Vector2(editView.SelectionRight - editView.SelectionLeft, field.Height - 8), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            renderer.Label(editElement);
            Main.spriteBatch.Draw(pixel, new Vector2(field.X + 6 + editView.Caret, field.Y + 5), new Rectangle(0, 0, 1, 1), Color.White, 0,
                Vector2.Zero, new Vector2(1, field.Height - 10), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Main.instance.SetIMEPanelAnchor(new Vector2(field.X + 6 + editView.Caret, field.Bottom + 32), 0);
        }
        private void ScrollIndicator(F5Rect view, int offset, int maximum)
        {
            if (maximum <= 0) return;
            renderer.Divider(new F5Rect(view.Right + 2, view.Y, 2, view.Height));
            renderer.Divider(new F5Rect(view.Right + 1, view.Y + (view.Height - 18) * offset / maximum, 4, 18));
        }
    }
}
