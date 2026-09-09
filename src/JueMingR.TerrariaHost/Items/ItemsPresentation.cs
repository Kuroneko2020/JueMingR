using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed class ItemsPresentation
    {
        private enum Command { Enable, Disable, Add, Edit, Replace, Remove, Select, Confirm, Cancel, Refresh }
        private sealed class Control
        {
            internal Command Command;
            internal int Argument, Type;
            internal F5Rect Rect;
            internal string Text;
            internal F5Element Element;
            internal bool Selected, Enabled = true;
        }
        private readonly HostItems host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly List<Control> controls = new List<Control>();
        private readonly List<F5Element> elements = new List<F5Element>();
        private ItemAutomationSettings laidOutValue;
        private int layoutGeneration = -1, skinGeneration = -1, editing;
        private ItemListKind? editList;
        private float laidOutScroll, laidOutPickerScroll;
        private bool dirty = true, laidOutEnabled;
        private string laidOutMessage;
        private Vector2 pointerPosition;
        internal int LayoutBuildCount { get; private set; }
        private readonly HashSet<int> selected = new HashSet<int>();
        private readonly ItemPickerInput input = new ItemPickerInput();
        private ItemListKind? picker;
        private int replacing;
        private int[] candidates = new int[0];
        private float pickerScroll;
        private string layoutMessage;
        private F5Rect view, popup, pickerView;
        private Matrix matrix;
        private Control armed;
        private bool ready, previousLeft, previousEscape, leftTail, rightTail;
        private int armedGeneration;
        private bool ownPointer;
        internal bool Modal { get { return picker.HasValue || editing != 0; } }
        internal bool OwnsPointer { get { return ready && (ownPointer || Modal); } }
        internal bool OwnsTextToken { get { return input.OwnsTextToken; } }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal ItemsPresentation(HostItems host, F5Interaction shell)
        {
            this.host = host; this.shell = shell;
            Func<int, bool> prior = shell.BeforeLeave;
            shell.BeforeLeave = page => { if (prior != null && !prior(page)) return false; Suspend(); return true; };
        }
        internal static string Name(ItemActionKind action)
        { return action == ItemActionKind.Stack ? "自动堆叠" : action == ItemActionKind.Sell ? "自动出售" : "自动丢弃"; }
        internal void BeforeInput(bool active) { input.BeforeInput(active && shell.Visible && shell.Page == 0 && Modal); }
        internal bool Wheel(float x, float y, int wheel)
        {
            if (!ready || !shell.Visible || !Modal) return false;
            if (!picker.HasValue) return true;
            if (pickerView.Contains(x, y) && wheel != 0)
            { pickerScroll = Math.Max(0, Math.Min(MaxPickerScroll, pickerScroll - wheel / 120f * renderer.RowHeight * 2)); armed = null; dirty = true; }
            return true; // Even at either end, the background does not scroll.
        }
        private float MaxPickerScroll { get { return Math.Max(0, candidates.Length * renderer.RowHeight - pickerView.Height); } }
        internal void ProcessInput(bool active, KeyboardState sample, Vector2 pointer, bool geometryCurrent = true)
        {
            pointerPosition = pointer;
            bool focused = FocusHelper.AllowInputProcessing;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            bool pageActive = active && focused && shell.Visible && shell.Page == 0;
            input.Sample(sample, focused);
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = false; ownPointer = false;
            if (!pageActive || !ready)
            {
                Suspend();
                if (focused) { if (!left) leftTail = false; if (!right) rightTail = false; }
                previousLeft = focused ? left : true; previousEscape = focused ? sample.IsKeyDown(Keys.Escape) : true; return;
            }
            // Input precedes AfterUpdate reflow: the shell must also compare this
            // sample's screen/scale with the cache, before Generation can change.
            // Stale rectangles cannot submit; existing mouse tails still consume.
            if (!geometryCurrent || layoutGeneration != shell.Layout.Generation || laidOutEnabled != ControlsEnabled || laidOutScroll != shell.Scroll ||
                view.X != shell.Layout.Viewport.X + shell.X || view.Y != shell.Layout.Viewport.Y + shell.Y)
            { armed = null; dirty = true; }
            if (sample.IsKeyDown(Keys.Escape) && !previousEscape && Modal) CancelPicker();
            previousEscape = sample.IsKeyDown(Keys.Escape);
            ownPointer = Modal || view.Contains(pointer.X, pointer.Y);
            if (ownPointer)
            {
                if (left) leftTail = true; if (right) rightTail = true; ConsumeWheel = Modal;
                Control hit = dirty ? null : Hit(pointer);
                if (left && !previousLeft) { armed = hit; armedGeneration = shell.Layout.Generation; }
                if (!left && previousLeft && armed != null && hit != null && armedGeneration == shell.Layout.Generation && Same(armed, hit)) Execute(hit);
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!left) { leftTail = false; armed = null; } if (!right) rightTail = false;
            previousLeft = left;
        }
        private Control Hit(Vector2 pointer)
        {
            foreach (Control control in controls)
                if (control.Enabled && control.Rect.Contains(pointer.X, pointer.Y) &&
                    (Modal ? popup.Contains(pointer.X, pointer.Y) && (control.Command != Command.Select || pickerView.Contains(pointer.X, pointer.Y)) : view.Contains(pointer.X, pointer.Y))) return control;
            return null;
        }
        private static bool SameRect(F5Rect a, F5Rect b)
        { return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height; }
        private static bool Same(Control a, Control b)
        { return a.Command == b.Command && a.Argument == b.Argument && a.Type == b.Type && a.Rect.X == b.Rect.X && a.Rect.Y == b.Rect.Y && a.Rect.Width == b.Rect.Width && a.Rect.Height == b.Rect.Height; }
        private void Execute(Control control)
        {
            dirty = true;
            ItemAutomationSettings value = host.Preferences.Value;
            var action = (ItemActionKind)control.Argument;
            var list = (ItemListKind)control.Argument;
            switch (control.Command)
            {
                case Command.Enable: host.Change(value.WithEnabled(action, true)); break;
                case Command.Disable: host.Change(value.WithEnabled(action, false)); break;
                case Command.Add: OpenPicker(list, 0); break;
                case Command.Edit: editList = list; editing = control.Type; armed = null; break;
                case Command.Replace: OpenPicker(list, control.Type); break;
                case Command.Remove: host.Change(value.WithTypes(list, Types(value, list).Where(t => t != control.Type))); CancelPicker(); break;
                case Command.Select:
                    if (replacing != 0)
                    {
                        var current = Types(value, picker.Value);
                        if (current.Contains(replacing) && !current.Contains(control.Type)) host.Change(value.WithTypes(picker.Value, current.Where(t => t != replacing).Concat(new[] { control.Type })));
                        CancelPicker();
                    }
                    else if (!selected.Add(control.Type)) selected.Remove(control.Type);
                    break;
                case Command.Confirm:
                    host.Change(value.WithTypes(picker.Value, Types(value, picker.Value).Concat(selected))); CancelPicker(); break;
                case Command.Cancel: CancelPicker(); break;
                case Command.Refresh: candidates = host.PickerTypes(picker.Value); selected.IntersectWith(candidates); pickerScroll = Math.Min(pickerScroll, MaxPickerScroll); break;
            }
        }
        private static IReadOnlyList<int> Types(ItemAutomationSettings value, ItemListKind list)
        { return list == ItemListKind.Sell ? value.SellTypes : value.DiscardTypes; }
        private void OpenPicker(ItemListKind list, int target)
        { input.Release(); editing = 0; editList = null; picker = list; replacing = target; candidates = host.PickerTypes(list); selected.Clear(); pickerScroll = 0; armed = null; dirty = true; }
        private void CancelPicker() { picker = null; editing = 0; editList = null; selected.Clear(); candidates = new int[0]; replacing = 0; armed = null; dirty = true; input.Release(); }
        internal void Suspend() { ready = false; ownPointer = false; CancelPicker(); controls.Clear(); elements.Clear(); editing = 0; editList = null; armed = null; renderer.Dispose(); }
        internal void Prepare(bool active, Matrix transform, Vector2 screen)
        {
            matrix = transform; ready = active && shell.Visible && shell.Page == 0 && renderer.Refresh();
            if (!ready) { controls.Clear(); if (!active) { Suspend(); renderer.Dispose(); } return; }
            PrepareLayout(transform, screen);
        }
        // Production geometry has no graphics-device dependency: draw and input
        // consume this exact cache, including in the non-graphical fixture.
        internal void PrepareLayout(Matrix transform, Vector2 screen)
        {
            matrix = transform; ready = shell.Visible && shell.Page == 0;
            if (!ready) return;
            renderer.RowHeight = Math.Max(30, shell.Layout.TextSize("开启", .7f).Height + 8);
            F5Rect nextView = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            if (!SameRect(view, nextView)) dirty = true;
            view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            float popupHeight = Math.Min(540, screen.Y / matrix.M11 - 32);
            float popupWidth = Math.Min(520, screen.X / matrix.M11 - 32);
            F5Rect nextPopup = new F5Rect((screen.X / matrix.M11 - popupWidth) / 2, (screen.Y / matrix.M11 - popupHeight) / 2, popupWidth, popupHeight);
            if (!SameRect(popup, nextPopup)) dirty = true;
            popup = nextPopup;
            pickerView = new F5Rect(popup.X + 12, popup.Y + renderer.RowHeight * 2 + 12, popup.Width - 24, popup.Height - renderer.RowHeight * 3 - 28);
            // A valid small F5 viewport can be too short for this picker. Close
            // only its draft and keep the rest of F5/Notes usable and recoverable.
            bool tooSmall = pickerView.Height < renderer.RowHeight;
            layoutMessage = tooSmall ? "当前视口不足以显示物品弹层；请调小 UI 缩放或增大窗口" : null;
            if (Modal && layoutMessage != null) CancelPicker();
            pickerScroll = Math.Min(pickerScroll, MaxPickerScroll);
            BuildIfNeeded();
        }
        private void Add(Command command, int argument, int type, F5Rect rect, string text, bool selected = false, bool enabled = true)
        {
            var control = new Control { Command = command, Argument = argument, Type = type, Rect = rect, Text = text, Selected = selected, Enabled = enabled };
            string label = command == Command.Select ? "" : text;
            control.Element = new F5Element(F5ElementKind.Button, rect, label, shell.Layout.TextSize(label, .7f), .7f, F5Command.None);
            controls.Add(control);
        }
        private bool ControlsEnabled { get { return host.Available && !host.Feature.HasFailed && host.Preferences.IsLoaded; } }
        private string ErrorMessage
        {
            get
            {
                string message = host.CapabilityError ?? host.SourceMessage ?? host.PreferenceMessage ?? layoutMessage;
                if (message != null) return message;
                for (int i = 0; i < 3; i++)
                {
                    ItemOperationResult result = host.Feature.LastResult((ItemActionKind)i);
                    if (result != null && (result.State == ItemOperationState.TimedOut || result.State == ItemOperationState.Unconfirmed || result.State == ItemOperationState.Failed)) return Status(result);
                }
                return null;
            }
        }
        private void BuildIfNeeded()
        {
            string message = ErrorMessage;
            if (!dirty && ReferenceEquals(laidOutValue, host.Preferences.Value) && layoutGeneration == shell.Layout.Generation &&
                skinGeneration == renderer.Generation && laidOutScroll == shell.Scroll && laidOutPickerScroll == pickerScroll &&
                laidOutEnabled == ControlsEnabled && laidOutMessage == message) return;
            armed = null; dirty = false; controls.Clear(); elements.Clear(); LayoutBuildCount++;
            laidOutValue = host.Preferences.Value; layoutGeneration = shell.Layout.Generation; skinGeneration = renderer.Generation;
            laidOutScroll = shell.Scroll; laidOutPickerScroll = pickerScroll; laidOutEnabled = ControlsEnabled; laidOutMessage = message;
            if (Modal) { BuildPopup(); return; }
            float y = view.Y - shell.Scroll;
            for (int i = 0; i < 3; i++)
            {
                var action = (ItemActionKind)i;
                ItemListKind list = action == ItemActionKind.Sell ? ItemListKind.Sell : ItemListKind.Discard;
                string[] actions = i == 0 ? new[] { "开启", "关闭" } : new[] { "添加", "开启", "关闭" };
                int start = elements.Count;
                new F5RowLayout(elements, shell.Layout.TextSize).Row(ref y, view.X, view.Width, Name(action), actions);
                for (int j = start; j < elements.Count; j++)
                {
                    F5Element e = elements[j]; if (e.Kind != F5ElementKind.Button) continue;
                    bool add = e.Text == "添加", on = e.Text == "开启";
                    Add(add ? Command.Add : on ? Command.Enable : Command.Disable, add ? (int)list : i, 0, e.Rect, e.Text,
                        !add && laidOutValue.Enabled(action) == on, laidOutEnabled);
                }
                if (i != 0) BuildCards(list, ref y);
            }
            var rows = new F5RowLayout(elements, shell.Layout.TextSize);
            rows.TextLines("启用后，名单提交会影响已有库存。", view.X + 8, ref y, view.Width - 16, .63f);
            rows.TextLines("点选图标可替换或移除；悬停查看名称。", view.X + 8, ref y, view.Width - 16, .63f);
            // Variable error text does not enter the fixed-label metric cache.
            if (message != null) y += renderer.RowHeight * 2;
            shell.Layout.SetItemsContentHeight(y - view.Y + shell.Scroll); shell.ClampScroll();
            if (laidOutScroll != shell.Scroll) { dirty = true; BuildIfNeeded(); }
        }
        private void BuildCards(ItemListKind list, ref float y)
        {
            IReadOnlyList<int> types = Types(laidOutValue, list);
            if (types.Count == 0) { if (editList == list) { editList = null; editing = 0; } return; }
            const float card = 40, gap = 4;
            int columns = Math.Max(1, (int)((view.Width - 16 + gap) / (card + gap)));
            int firstRow = Math.Max(0, (int)Math.Floor((view.Y - y) / (card + gap)));
            int lastRow = Math.Min((types.Count - 1) / columns, (int)Math.Floor((view.Bottom - y) / (card + gap)));
            for (int row = firstRow; row <= lastRow; row++)
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column; if (index >= types.Count) break;
                    Add(Command.Edit, (int)list, types[index], new F5Rect(view.X + 8 + column * (card + gap), y + row * (card + gap), card, card), "",
                        editList == list && editing == types[index], laidOutEnabled);
                }
            y += ((types.Count + columns - 1) / columns) * (card + gap) + 2;

        }

        private void BuildPopup()
        {
            float row = renderer.RowHeight;
            Add(Command.Cancel, 0, 0, new F5Rect(popup.Right - 108, popup.Bottom - row - 10, 96, row), "取消");
            if (editing != 0)
            {
                Add(Command.Replace, (int)editList.Value, editing, new F5Rect(popup.X + 12, popup.Y + row * 2, 70, row), "\u66ff\u6362", enabled: ControlsEnabled);
                Add(Command.Remove, (int)editList.Value, editing, new F5Rect(popup.X + 86, popup.Y + row * 2, 70, row), "\u79fb\u9664", enabled: ControlsEnabled);
                return;
            }
            if (!picker.HasValue) return;
            Add(Command.Refresh, 0, 0, new F5Rect(popup.Right - 136, popup.Y + row + 4, 124, row), "刷新背包");
            if (replacing == 0) Add(Command.Confirm, 0, 0, new F5Rect(popup.X + 12, popup.Bottom - row - 10, 132, row), "确定添加", enabled: selected.Count != 0);
            int first = Math.Max(0, (int)(pickerScroll / row));
            int last = Math.Min(candidates.Length - 1, (int)((pickerScroll + pickerView.Height) / row));
            for (int i = first; i <= last; i++)
                Add(Command.Select, 0, candidates[i], new F5Rect(pickerView.X, pickerView.Y + i * row - pickerScroll, pickerView.Width, row - 2),
                    Lang.GetItemNameValue(candidates[i]), selected.Contains(candidates[i]));
        }
        internal void Draw()
        {
            if (!ready || !shell.Visible || shell.Page != 0) return;
            if (Modal)
            {
                renderer.Pass(matrix, popup, () =>
                {
                    renderer.Panel(popup);
                    string title = editing != 0 ? Lang.GetItemNameValue(editing) : replacing == 0 ? "批量添加：选好后点确定" : "替换：点选一项立即提交";
                    renderer.Text(title, new F5Rect(popup.X + 12, popup.Y + 4, popup.Width - 24, renderer.RowHeight), Color.White);
                    renderer.Text("只保存类型；收藏物也可提供名单类型",
                        new F5Rect(popup.X + 12, popup.Y + renderer.RowHeight + 4, popup.Width - 160, renderer.RowHeight), Color.LightGray, .63f);
                    foreach (Control c in controls) if (c.Command != Command.Select) DrawButton(c);
                });
                if (picker.HasValue) renderer.Pass(matrix, pickerView, () =>
                {
                    if (candidates.Length == 0) renderer.Text("背包中没有可添加的新类型", pickerView, Color.Gray);
                    foreach (Control c in controls)
                    {
                        if (c.Command != Command.Select) continue;
                        DrawButton(c); renderer.Item(c.Type, new F5Rect(c.Rect.X, c.Rect.Y, renderer.RowHeight, renderer.RowHeight));
                        renderer.Text((c.Selected ? "✓ " : "") + c.Text, new F5Rect(c.Rect.X + renderer.RowHeight + 2, c.Rect.Y, c.Rect.Width - renderer.RowHeight - 8, c.Rect.Height), c.Selected ? Color.LightGreen : Color.White);
                    }
                });
            }
            else
            {
                renderer.Pass(matrix, view, () =>
                {
                    foreach (F5Element e in elements)
                    {
                        if (e.Rect.Bottom <= view.Y || e.Rect.Y >= view.Bottom) continue;
                        if (e.Kind == F5ElementKind.Panel) renderer.Panel(e.Rect);
                        else if (e.Kind == F5ElementKind.Text) renderer.Label(e);
                    }
                    foreach (Control c in controls)
                    {
                        if (c.Rect.Bottom <= view.Y || c.Rect.Y >= view.Bottom) continue;
                        DrawButton(c);
                        if (c.Command == Command.Edit) renderer.Item(c.Type, c.Rect);
                    }
                    if (laidOutMessage != null) renderer.Text(laidOutMessage,
                        new F5Rect(view.X + 8, view.Y - shell.Scroll + shell.Layout.ContentHeight - renderer.RowHeight * 2, view.Width - 16, renderer.RowHeight * 2), Color.Gold, .63f);
                });
                Control hover = Hit(pointerPosition);
                if (hover != null && hover.Command == Command.Edit)
                {
                    float width = Math.Min(view.Width, 300), height = renderer.RowHeight;
                    var hint = new F5Rect(Math.Min(view.Right - width, Math.Max(view.X, pointerPosition.X + 12)),
                        Math.Min(view.Bottom - height, pointerPosition.Y + 18), width, height);
                    renderer.Pass(matrix, view, () => { renderer.Panel(hint); renderer.Text(Lang.GetItemNameValue(hover.Type), hint, Color.White); });
                }
            }
        }
        private void DrawButton(Control c)
        { renderer.Button(c.Element, c.Selected, c.Enabled, c.Command == Command.Disable, c.Rect.Contains(pointerPosition.X, pointerPosition.Y)); }
        private static string Status(ItemOperationResult result)
        {
            if (result == null) return "尚无处理结果";
            switch (result.State)
            {
                case ItemOperationState.Completed: return "已处理 " + result.ConfirmedQuantity + " 个" + (result.ConfirmedCopper > 0 ? "；收入 " + result.ConfirmedCopper + " 铜币" : "");
                case ItemOperationState.PartiallyCompleted: return "已存放 " + result.ConfirmedQuantity + " 个，其余保留";
                case ItemOperationState.Executing: return "正在等待来源槽回复";
                case ItemOperationState.NotApplicable: return "本次不适用或没有可接收的附近箱子";
                case ItemOperationState.Rejected: return "本次未开始，等待条件变化";
                case ItemOperationState.TimedOut: return "等待超时；相关槽仍受保护，不会重发";
                case ItemOperationState.Unconfirmed: return "结果未确认；相关槽仍受保护，不会重试";
                case ItemOperationState.Cancelled: return "已取消";
                default: return "处理失败；相关槽仍受保护";
            }
        }
    }
}
