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
        private static readonly F5RowDescription[] descriptions = {
            new F5RowDescription(Hotkeys.HotkeyActionIds.Items[0], "将刚拾取的物品存入附近已有同类物品的箱子，优先级为出售→丢弃→存放。"),
            new F5RowDescription(Hotkeys.HotkeyActionIds.Items[1], "将刚拾取的名单内物品在已打开的商店界面出售，优先级为出售→丢弃→存放。"),
            new F5RowDescription(Hotkeys.HotkeyActionIds.Items[2], "将刚拾取的名单内物品放入垃圾桶，优先级为出售→丢弃→存放。") };
        internal static F5RowDescription Description(ItemActionKind action) { return descriptions[(int)action]; }
        private readonly HostItems host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly ItemsLayout layout = new ItemsLayout();
        private readonly ItemSelection selection;
        private readonly ItemPickerInput input = new ItemPickerInput();
        internal QuickItems.QuickItemPanel QuickPanel { get; private set; }
        internal void AttachQuick(QuickItems.HostQuickItems owner) { QuickPanel = new QuickItems.QuickItemPanel(owner, shell); dirty = true; }
        private readonly List<ItemUiControl> controls = new List<ItemUiControl>();
        private readonly List<F5Element> elements = new List<F5Element>();
        private ItemAutomationSettings laidOutValue;
        private int layoutGeneration = -1, skinGeneration = -1;
        private float laidOutScroll;
        private bool dirty = true, laidOutEnabled, ready, previousLeft, previousEscape, leftTail, rightTail, ownPointer;
        private string laidOutMessage, commandMessage;
        private F5Rect view;
        private Vector2 pointerPosition;
        private Matrix matrix;
        private ItemUiControl armed;
        private int armedGeneration;
        private int revealRow = -1;
        private float anchorY;
        internal int LayoutBuildCount { get; private set; }
        internal bool Selecting { get { return selection.Active || QuickPanel != null && QuickPanel.Editing; } }
        internal bool OwnsPointer { get { return ready && ownPointer; } }
        internal bool OwnsTextToken { get { return input.OwnsTextToken || QuickPanel != null && QuickPanel.OwnsTextToken; } }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal Action<string, F5Rect> HotkeyClicked { get; set; }
        internal ItemsPresentation(HostItems host, F5Interaction shell)
        {
            this.host = host; this.shell = shell; selection = new ItemSelection(host);
            Func<int, bool> prior = shell.BeforeLeave;
            shell.BeforeLeave = page => { if (prior != null && !prior(page)) return false; Suspend(); return true; };
        }
        internal static string Name(ItemActionKind action)
        { return action == ItemActionKind.Stack ? "自动堆叠" : action == ItemActionKind.Sell ? "自动出售" : "自动丢弃"; }
        private void ValidateSession()
        { if (!selection.ValidateSession()) { armed = null; dirty = true; input.Release(); revealRow = -1; } }
        internal void BeforeInput(bool active)
        { ValidateSession(); input.BeforeInput(active && shell.Visible && shell.Page == 0 && selection.Active); QuickPanel?.BeforeInput(active); }
        internal void ProcessInput(bool active, KeyboardState sample, Vector2 pointer, bool geometryCurrent = true, bool focused = true, bool blockPointer = false)
        {
            ValidateSession(); pointerPosition = pointer;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            bool pageActive = active && focused && shell.Visible && shell.Page == 0;
            input.Sample(sample, focused);
            QuickPanel?.Process(pageActive, sample, focused);
            if (QuickPanel != null && QuickPanel.NeedsBuild) { armed = null; dirty = true; }
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = false; ownPointer = false;
            if (!pageActive || !ready)
            {
                Suspend();
                if (focused) { if (!left) leftTail = false; if (!right) rightTail = false; }
                previousLeft = focused ? left : true; previousEscape = focused ? sample.IsKeyDown(Keys.Escape) : true; return;
            }
            // Input precedes reflow. Never submit rectangles from a previous
            // screen/font/scroll/settings generation; physical tails still live.
            if (!geometryCurrent || layoutGeneration != shell.Layout.Generation || laidOutEnabled != ControlsEnabled ||
                !ReferenceEquals(laidOutValue, host.Preferences.Value) || laidOutScroll != shell.Scroll ||
                view.X != shell.Layout.Viewport.X + shell.X || view.Y != shell.Layout.Viewport.Y + shell.Y)
            { armed = null; dirty = true; }
            if (sample.IsKeyDown(Keys.Escape) && !previousEscape && Selecting) { if (selection.Active) CancelPicker(); else QuickPanel.Suspend(); }
            previousEscape = sample.IsKeyDown(Keys.Escape);
            ownPointer = view.Contains(pointer.X, pointer.Y);
            if (ownPointer)
            {
                if (left) leftTail = true; if (right) rightTail = true;
                if (blockPointer) armed = null;
                ItemUiControl hit = dirty || blockPointer ? null : Hit(pointer);
                if (left && !previousLeft) { armed = hit; armedGeneration = shell.Layout.Generation; }
                if (!left && previousLeft && armed != null && hit != null && armedGeneration == shell.Layout.Generation && Same(armed, hit)) Execute(hit);
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!left) { leftTail = false; armed = null; } if (!right) rightTail = false;
            previousLeft = left;
        }
        private ItemUiControl Hit(Vector2 pointer)
        {
            if (!view.Contains(pointer.X, pointer.Y)) return null;
            // Remove is inserted before its body: one gesture has one target,
            // including when the removal shifts another card into this location.
            foreach (var c in controls) if (c.Enabled && c.Rect.Contains(pointer.X, pointer.Y)) return c;
            return null;
        }
        private static bool SameRect(F5Rect a, F5Rect b)
        { return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height; }
        private static bool Same(ItemUiControl a, ItemUiControl b)
        { return a.Command == b.Command && a.Argument == b.Argument && a.Type == b.Type && a.Generation == b.Generation && SameRect(a.Rect, b.Rect); }
        private void Execute(ItemUiControl c)
        {
            if (c.Command == ItemUiCommand.Hotkey || c.Command == ItemUiCommand.QuickHotkey) { HotkeyClicked?.Invoke(c.Element.HotkeyTarget, c.Rect); return; }
            if (c.Command == ItemUiCommand.Quick) { CancelPicker(); QuickPanel?.Execute(c); dirty = true; return; }
            dirty = true; commandMessage = null;
            var value = host.Preferences.Value; var list = (ItemListKind)c.Argument;
            bool wasSelecting = Selecting;
            switch (c.Command)
            {
                case ItemUiCommand.Enable: host.Change(value.WithEnabled((ItemActionKind)c.Argument, true)); break;
                case ItemUiCommand.Disable: host.Change(value.WithEnabled((ItemActionKind)c.Argument, false)); break;
                case ItemUiCommand.ToggleDiscardFeedback: host.Change(value.WithDiscardFeedbackEnabled(!value.DiscardFeedbackEnabled)); break;
                case ItemUiCommand.Add: OpenPicker(list, 0); break;
                case ItemUiCommand.Replace: OpenPicker(list, c.Type); break;
                case ItemUiCommand.Remove:
                    if (!ItemSelection.Types(value, list).Contains(c.Type)) commandMessage = "名单已变化，请重新选择图标。";
                    else if (!host.Change(value.WithTypes(list, ItemSelection.Types(value, list).Where(t => t != c.Type)))) commandMessage = "暂时无法移除，名单未改变。";
                    break;
                case ItemUiCommand.Select: selection.Select(c.Type); break;
                case ItemUiCommand.Confirm: selection.Confirm(); break;
                case ItemUiCommand.Cancel: CancelPicker(); break;
            }
            if (wasSelecting && !Selecting) { input.Release(); revealRow = -1; }
        }
        private void OpenPicker(ItemListKind list, int target)
        {
            QuickPanel?.Suspend();
            int row = list == ItemListKind.Sell ? 1 : 2;
            float oldY = layout.RowY[row] - shell.Scroll;
            if (selection.Open(list, target)) { revealRow = row; anchorY = oldY; armed = null; }
        }
        private void CancelPicker() { selection.Cancel(); revealRow = -1; armed = null; dirty = true; input.Release(); }
        internal void Suspend()
        { ready = false; ownPointer = false; CancelPicker(); QuickPanel?.Suspend(); commandMessage = null; controls.Clear(); elements.Clear(); renderer.Dispose(); }
        internal void Prepare(bool active, Matrix transform, Vector2 screen)
        {
            ValidateSession();
            ready = active && shell.Visible && shell.Page == 0 && renderer.Refresh();
            if (!ready) { if (!active || !shell.Visible || shell.Page != 0) Suspend(); else controls.Clear(); return; }
            PrepareLayout(transform, screen);
            QuickPanel?.PrepareIcons();
        }
        internal void PrepareLayout(Matrix transform, Vector2 screen)
        {
            ValidateSession(); matrix = transform; ready = shell.Visible && shell.Page == 0;
            if (!ready) return;
            renderer.RowHeight = Math.Max(30, shell.Layout.TextSize("开启", .7f).Height + 8);
            F5Rect next = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            if (!SameRect(view, next)) dirty = true;
            view = next;
            string message = ErrorMessage;
            bool rebuild = dirty || QuickPanel != null && QuickPanel.NeedsBuild || !ReferenceEquals(laidOutValue, host.Preferences.Value) || layoutGeneration != shell.Layout.Generation ||
                skinGeneration != renderer.Generation || laidOutEnabled != ControlsEnabled || laidOutMessage != message;
            if (!rebuild && laidOutScroll == shell.Scroll) return;
            armed = null;
            if (rebuild)
            {
                layout.Build(view.Width, renderer.RowHeight, host.Preferences.Value, selection, ControlsEnabled, message != null, shell.Layout.TextSize);
                QuickPanel?.Build(layout.Height, view.Width, renderer.RowHeight, shell.Layout.TextSize);
                shell.Layout.SetItemsContentHeight(QuickPanel == null ? layout.Height : QuickPanel.Height);
                if(QuickPanel!=null && QuickPanel.RevealRequested)
                {shell.ScrollTo(QuickPanel.SelectorTop);QuickPanel.RevealRequested=false;}
                if (revealRow >= 0)
                {
                    // Preserve the row across a switch, then reveal only once.
                    // Subsequent user scrolls must never snap back to this anchor.
                    shell.ScrollTo(layout.RowY[revealRow] - anchorY);
                    float bottom = Math.Min(layout.RevealBottom, layout.Header.Y + view.Height);
                    if (layout.Header.Y < shell.Scroll) shell.ScrollTo(layout.Header.Y);
                    else if (bottom > shell.Scroll + view.Height) shell.ScrollTo(bottom - view.Height);
                    revealRow = -1;
                }
                shell.ClampScroll();
            }
            layout.Project(view, shell.Scroll, elements, controls); LayoutBuildCount++;
            QuickPanel?.Project(view, shell.Scroll, controls, elements);
            dirty = false; laidOutValue = host.Preferences.Value; laidOutScroll = shell.Scroll;
            layoutGeneration = shell.Layout.Generation; skinGeneration = renderer.Generation;
            laidOutEnabled = ControlsEnabled; laidOutMessage = message;
        }
        private bool ControlsEnabled { get { return host.ControlsEnabled; } }
        private string ErrorMessage
        {
            get
            {
                if (host.CapabilityError != null) return host.CapabilityError;
                if (host.SourceMessage != null) return host.SourceMessage;
                if (host.PreferenceMessage != null) return host.PreferenceMessage;
                if (selection.Message != null) return selection.Message;
                if (commandMessage != null) return commandMessage;
                for (int i = 0; i < 3; i++)
                {
                    var result = host.Feature.LastResult((ItemActionKind)i);
                    if (result == null) continue;
                    if (result.State == ItemOperationState.TimedOut) return "物品处理超时，结果未确认；相关物品已暂停自动处理。";
                    if (result.State == ItemOperationState.Unconfirmed) return "处理结果未确认；相关物品已暂停自动处理，不会自动重试。";
                    if (result.State == ItemOperationState.Failed) return "处理失败，相关物品已暂停自动处理。";
                }
                return null;
            }
        }
        private F5Rect OnScreen(F5Rect rect) { return rect.Offset(view.X, view.Y - shell.Scroll); }
        // Consume only the final projection. Hover cannot prepare a picker,
        // refresh business state or invalidate the controls it is inspecting.
        internal string Hint(float x, float y, out F5Rect target, F5Rect? contentClip = null)
        {
            target = default(F5Rect);
            if (!ready || dirty || !shell.Visible || shell.Page != 0 || laidOutScroll != shell.Scroll ||
                layoutGeneration != shell.Layout.Generation || view.X != shell.X + shell.Layout.Viewport.X || view.Y != shell.Y + shell.Layout.Viewport.Y ||
                !view.Contains(x, y)) return null;
            var visible = contentClip.HasValue ? F5HintLayout.Intersect(view, contentClip.Value) : view;
            if (!visible.Contains(x, y)) return null;
            foreach (var control in controls)
            {
                if (!control.Rect.Contains(x, y)) continue;
                target = F5HintLayout.Intersect(control.Rect, visible);
                if (control.Command == ItemUiCommand.Hotkey || control.Command == ItemUiCommand.QuickHotkey)
                    return String.IsNullOrEmpty(control.Element.Text)?"双击设置快捷键":control.Element.Text+" · 双击设置快捷键";
                if (control.Command == ItemUiCommand.ToggleDiscardFeedback) return "开启或关闭自动丢弃提示。";
                if(control.Command==ItemUiCommand.Quick)return QuickPanel?.Hint(x,y,out target);
                return null; // Item cards retain their own name/replace/remove help.
            }
            string quickHint = QuickPanel?.Hint(x, y, out target);
            if (quickHint != null) return quickHint;
            var name = F5HintLayout.HitName(elements, visible, 0, 0, x, y, out target);
            return name == null ? null : name.Description.Text;
        }
        internal void Draw(Action<F5Rect> keyboard = null, bool allowHints = true)
        {
            if (!ready || !shell.Visible || shell.Page != 0) return;
            renderer.Pass(matrix, view, () =>
            {
                foreach (var e in elements)
                { if (e.Kind == F5ElementKind.Panel) renderer.Panel(e.Rect); else if (e.Kind == F5ElementKind.Text) renderer.Label(e); }
                if (selection.Active)
                {
                    string list = selection.List == ItemListKind.Sell ? "出售" : "丢弃";
                    renderer.Text(selection.Target == 0 ? "添加" + list + "物品" : "替换「" + Lang.GetItemNameValue(selection.Target) + "」", OnScreen(layout.Title), Color.White);
                    if (selection.Target == 0) renderer.Text("已选 " + selection.Count + " 项", OnScreen(layout.Count), Color.LightGray, .63f);
                    if (layout.Risk.Height > 0) renderer.Text("名单用于处理刚拾取或开出的物品。", OnScreen(layout.Risk), Color.Gold, .63f);
                    if (layout.Empty.Height > 0) renderer.Text(selection.HasInventoryTypes ? "背包中可选的物品都已在名单中。" : "背包中没有可添加的物品，钱币不能加入名单。", OnScreen(layout.Empty), Color.Gray, .63f);
                }
                foreach (var c in controls)
                {
                    if (c.Command == ItemUiCommand.Quick || c.Command == ItemUiCommand.QuickHotkey) continue;
                    if (c.Command == ItemUiCommand.Hotkey) { keyboard?.Invoke(c.Rect); continue; }
                    if (c.Command == ItemUiCommand.Remove) continue;
                    bool hover = c.Rect.Contains(pointerPosition.X, pointerPosition.Y) && view.Contains(pointerPosition.X, pointerPosition.Y);
                    if (c.Command == ItemUiCommand.Replace || c.Command == ItemUiCommand.Select)
                    {
                        renderer.ItemButton(c.Rect, c.Enabled, hover);
                        renderer.Item(c.Type, ItemsLayout.IconBounds(c.Rect, c.Command == ItemUiCommand.Select));
                        if (c.Selected) renderer.Selection(c.Rect);
                        if (c.Command == ItemUiCommand.Replace && hover)
                            renderer.Cross(new F5Rect(c.Rect.Right - ItemsLayout.CrossSize, c.Rect.Y, ItemsLayout.CrossSize, ItemsLayout.CrossSize), c.Enabled);
                    }
                    else renderer.Button(c.Element, c.Selected, c.Enabled, c.Command == ItemUiCommand.Disable, hover);
                }
                if (laidOutMessage != null) renderer.Text(laidOutMessage, OnScreen(layout.Error), Color.Gold, .63f);
                QuickPanel?.Draw(renderer, pointerPosition, keyboard);
            });
            var hovered = allowHints ? Hit(pointerPosition) : null;
            if (hovered != null && hovered.Type != 0)
            {
                var target = hovered.Command == ItemUiCommand.Remove ? new F5Rect(hovered.Rect.Right - ItemsLayout.CardWidth,
                    hovered.Rect.Y, ItemsLayout.CardWidth, ItemsLayout.CardHeight) : hovered.Rect;
                F5Rect hint = Tooltip(target, view, Math.Min(300, view.Width), renderer.RowHeight * 2);
                if (CoversAction(hint))
                {
                    var below = new F5Rect(hint.X, target.Bottom + 8, hint.Width, hint.Height);
                    hint = below.Bottom <= view.Bottom && !CoversAction(below) ? below : default(F5Rect);
                }
                if (hint.Height <= 0) return;
                string action = hovered.Command == ItemUiCommand.Remove ? "从" + (hovered.Argument == (int)ItemListKind.Sell ? "出售" : "丢弃") + "名单移除" :
                    hovered.Command == ItemUiCommand.Replace ? "点击替换" : selection.Target == 0 ? "点击选择或取消，确定后添加" : "点击立即替换";
                renderer.Pass(matrix, view, () =>
                {
                    renderer.Panel(hint);
                    renderer.Text(Lang.GetItemNameValue(hovered.Type), new F5Rect(hint.X + 4, hint.Y, hint.Width - 8, hint.Height / 2), Color.White);
                    renderer.Text(action, new F5Rect(hint.X + 4, hint.Y + hint.Height / 2, hint.Width - 8, hint.Height / 2), Color.LightGray, .63f);
                });
            }
        }
        private bool CoversAction(F5Rect hint)
        {
            foreach (var c in controls)
                if (c.Type == 0 && hint.X < c.Rect.Right && hint.Right > c.Rect.X && hint.Y < c.Rect.Bottom && hint.Bottom > c.Rect.Y) return true;
            return false;
        }
        internal static F5Rect Tooltip(F5Rect target, F5Rect bounds, float width, float height)
        {
            float x = Math.Max(bounds.X, Math.Min(bounds.Right - width, target.X));
            if (target.Y - height - 8 >= bounds.Y) return new F5Rect(x, target.Y - height - 8, width, height);
            if (target.Bottom + height + 8 <= bounds.Bottom) return new F5Rect(x, target.Bottom + 8, width, height);
            return default(F5Rect); // Very short views cannot show a hint without hiding its target.
        }
    }
}
