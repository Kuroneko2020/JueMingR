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
        private enum Command { Enable, Disable, Add, Replace, Remove, Select, Confirm, Cancel, Refresh }
        private sealed class Control
        {
            internal Command Command;
            internal int Argument, Type;
            internal F5Rect Rect;
            internal string Text;
            internal bool Selected, Enabled = true;
        }
        private readonly HostItems host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly List<Control> controls = new List<Control>();
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
        internal bool Modal { get { return picker.HasValue; } }
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
            if (!ready || !shell.Visible || !picker.HasValue) return false;
            if (pickerView.Contains(x, y) && wheel != 0)
            { pickerScroll = Math.Max(0, Math.Min(MaxPickerScroll, pickerScroll - wheel / 120f * renderer.RowHeight * 2)); armed = null; }
            return true; // Even at either end, the background does not scroll.
        }
        private float MaxPickerScroll { get { return Math.Max(0, candidates.Length * renderer.RowHeight - pickerView.Height); } }
        internal void ProcessInput(bool active, KeyboardState sample, Vector2 pointer)
        {
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
            if (sample.IsKeyDown(Keys.Escape) && !previousEscape && picker.HasValue) CancelPicker();
            previousEscape = sample.IsKeyDown(Keys.Escape);
            ownPointer = Modal || view.Contains(pointer.X, pointer.Y);
            if (ownPointer)
            {
                if (left) leftTail = true; if (right) rightTail = true; ConsumeWheel = Modal;
                Control hit = Hit(pointer);
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
        private static bool Same(Control a, Control b)
        { return a.Command == b.Command && a.Argument == b.Argument && a.Type == b.Type && a.Rect.X == b.Rect.X && a.Rect.Y == b.Rect.Y && a.Rect.Width == b.Rect.Width && a.Rect.Height == b.Rect.Height; }
        private void Execute(Control control)
        {
            ItemAutomationSettings value = host.Preferences.Value;
            var action = (ItemActionKind)control.Argument;
            var list = (ItemListKind)control.Argument;
            switch (control.Command)
            {
                case Command.Enable: host.Change(value.WithEnabled(action, true)); break;
                case Command.Disable: host.Change(value.WithEnabled(action, false)); break;
                case Command.Add: OpenPicker(list, 0); break;
                case Command.Replace: OpenPicker(list, control.Type); break;
                case Command.Remove: host.Change(value.WithTypes(list, Types(value, list).Where(t => t != control.Type))); break;
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
        { input.Release(); picker = list; replacing = target; candidates = host.PickerTypes(list); selected.Clear(); pickerScroll = 0; armed = null; }
        private void CancelPicker() { picker = null; selected.Clear(); candidates = new int[0]; replacing = 0; armed = null; input.Release(); }
        internal void Suspend() { ready = false; ownPointer = false; CancelPicker(); controls.Clear(); armed = null; renderer.Dispose(); }
        internal void Prepare(bool active, Matrix transform, Vector2 screen)
        {
            matrix = transform; ready = active && shell.Visible && shell.Page == 0 && renderer.Refresh();
            if (!ready) { controls.Clear(); if (!active) { Suspend(); renderer.Dispose(); } return; }
            view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            float popupHeight = Math.Min(540, screen.Y / matrix.M11 - 32);
            float popupWidth = Math.Min(520, screen.X / matrix.M11 - 32);
            popup = new F5Rect((screen.X / matrix.M11 - popupWidth) / 2, (screen.Y / matrix.M11 - popupHeight) / 2, popupWidth, popupHeight);
            pickerView = new F5Rect(popup.X + 12, popup.Y + renderer.RowHeight * 2 + 12, popup.Width - 24, popup.Height - renderer.RowHeight * 3 - 28);
            // A valid small F5 viewport can be too short for this picker. Close
            // only its draft and keep the rest of F5/Notes usable and recoverable.
            bool tooSmall = pickerView.Height < renderer.RowHeight;
            layoutMessage = tooSmall ? "当前视口不足以显示物品弹层；请调小 UI 缩放或增大窗口" : null;
            if (Modal && layoutMessage != null) CancelPicker();
            pickerScroll = Math.Min(pickerScroll, MaxPickerScroll);
            Build(false);
        }
        private void Add(Command command, int argument, int type, F5Rect rect, string text, bool selected = false, bool enabled = true)
        {
            var control = new Control { Command = command, Argument = argument, Type = type, Rect = rect, Text = text, Selected = selected, Enabled = enabled };
            controls.Add(control);
        }
        private void Build(bool draw)
        {
            if (!draw) controls.Clear();
            if (picker.HasValue) { if (!draw) BuildPopup(); return; }
            float row = renderer.RowHeight, y = view.Y - shell.Scroll;
            ItemAutomationSettings value = host.Preferences.Value;
            bool enabled = host.Available && !host.Feature.HasFailed && host.Preferences.IsLoaded;
            for (int i = 0; i < 3; i++)
            {
                var action = (ItemActionKind)i;
                if (draw) renderer.Text(Name(action), new F5Rect(view.X, y, 148, row), Color.White);
                else
                {
                    Add(Command.Enable, i, 0, new F5Rect(view.Right - 150, y, 70, row - 3), "开启", value.Enabled(action), enabled);
                    Add(Command.Disable, i, 0, new F5Rect(view.Right - 76, y, 70, row - 3), "关闭", !value.Enabled(action), enabled);
                }
                y += row;
                ItemOperationResult result = host.Feature.LastResult(action);
                if (result != null)
                { if (draw) renderer.Text(Status(result), new F5Rect(view.X + 8, y, view.Width - 16, row), Color.LightGray, .63f); y += row; }
                y += 8;
            }
            if (draw)
            {
                renderer.Text("处理顺序：出售 → 丢弃 → 堆叠", new F5Rect(view.X, y, view.Width, row), Color.LightSkyBlue);
                renderer.Text("启用后，名单提交会影响已有库存", new F5Rect(view.X, y + row, view.Width, row), Color.LightGray, .63f);
            }
            y += row * 2 + 8;
            foreach (ItemListKind list in new[] { ItemListKind.Sell, ItemListKind.Discard })
            {
                IReadOnlyList<int> types = Types(value, list);
                if (draw) renderer.Text((list == ItemListKind.Sell ? "出售名单" : "丢弃名单") + "（" + types.Count + "）", new F5Rect(view.X, y, 340, row), Color.White);
                else Add(Command.Add, (int)list, 0, new F5Rect(view.Right - 136, y, 132, row - 3), "从背包添加", enabled: enabled);
                y += row;
                if (types.Count == 0)
                { if (draw) renderer.Text("名单为空", new F5Rect(view.X + 8, y, view.Width - 16, row), Color.Gray); y += row; }
                else
                {
                    int first = Math.Max(0, (int)Math.Floor((view.Y - y) / row));
                    int last = Math.Min(types.Count - 1, (int)Math.Floor((view.Bottom - y) / row));
                    for (int i = first; i <= last; i++)
                    {
                        float iy = y + i * row; int type = types[i];
                        if (draw)
                        { renderer.Item(type, new F5Rect(view.X, iy, row, row)); renderer.Text(Lang.GetItemNameValue(type), new F5Rect(view.X + row + 2, iy, view.Width - row - 158, row), Color.White); }
                        else
                        {
                            Add(Command.Replace, (int)list, type, new F5Rect(view.Right - 150, iy, 70, row - 3), "替换", enabled: enabled);
                            Add(Command.Remove, (int)list, type, new F5Rect(view.Right - 76, iy, 70, row - 3), "移除", enabled: enabled);
                        }
                    }
                    y += types.Count * row;
                }
                y += 10;
            }
            if (draw)
            {
                renderer.Text(host.CapabilityError ?? host.SourceMessage ?? host.PreferenceMessage ?? SaveStatus(), new F5Rect(view.X, y, view.Width, row), Color.Gold, .63f);
                renderer.Text(layoutMessage ?? "三项独立保存；同类型可以同时加入两份名单", new F5Rect(view.X, y + row, view.Width, row), Color.LightGray, .63f);
            }
            shell.Layout.SetItemsContentHeight(y + row * 2 - view.Y + shell.Scroll); shell.ClampScroll();
        }
        private string SaveStatus()
        { return host.Preferences.Status == JueMingR.Platform.Settings.PreferenceStatus.Pending ? "设置正在保存" : "物品设置已就绪"; }
        private void BuildPopup()
        {
            float row = renderer.RowHeight;
            Add(Command.Cancel, 0, 0, new F5Rect(popup.Right - 108, popup.Bottom - row - 10, 96, row), "取消");
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
                    string title = replacing == 0 ? "批量添加：选好后点确定" : "替换：点选一项立即提交";
                    renderer.Text(title, new F5Rect(popup.X + 12, popup.Y + 4, popup.Width - 24, renderer.RowHeight), Color.White);
                    renderer.Text("只保存类型；收藏物也可提供名单类型",
                        new F5Rect(popup.X + 12, popup.Y + renderer.RowHeight + 4, popup.Width - 160, renderer.RowHeight), Color.LightGray, .63f);
                    foreach (Control c in controls) if (c.Command != Command.Select) renderer.Button(c.Rect, c.Text, c.Selected, c.Enabled);
                });
                if (picker.HasValue) renderer.Pass(matrix, pickerView, () =>
                {
                    if (candidates.Length == 0) renderer.Text("背包中没有可添加的新类型", pickerView, Color.Gray);
                    foreach (Control c in controls)
                    {
                        if (c.Command != Command.Select) continue;
                        renderer.Button(c.Rect, "", c.Selected); renderer.Item(c.Type, new F5Rect(c.Rect.X, c.Rect.Y, renderer.RowHeight, renderer.RowHeight));
                        renderer.Text((c.Selected ? "✓ " : "") + c.Text, new F5Rect(c.Rect.X + renderer.RowHeight + 2, c.Rect.Y, c.Rect.Width - renderer.RowHeight - 8, c.Rect.Height), c.Selected ? Color.LightGreen : Color.White);
                    }
                });
            }
            else renderer.Pass(matrix, view, () => { Build(true); foreach (Control c in controls) renderer.Button(c.Rect, c.Text, c.Selected, c.Enabled); });
        }
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
