using System;
using System.Collections.Generic;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Items
{
    internal enum ItemUiCommand { Enable, Disable, Add, Replace, Remove, Select, Confirm, Cancel, ToggleDiscardFeedback, Hotkey }
    internal sealed class ItemUiControl
    {
        internal ItemUiCommand Command;
        internal int Argument, Type, Generation;
        internal F5Rect Rect;
        internal F5Element Element;
        internal bool Selected, Enabled;
    }

    // Layout keeps full logical heights without materializing off-screen cards.
    // Once height and scroll are settled, Project creates the single cache used
    // by both paint and hit testing. No recursive clamp/reflow is needed.
    internal sealed class ItemsLayout
    {
        internal const float CardWidth = 47, CardHeight = 34, CandidateWidth = 47, CandidateSize = 48, Gap = 4, CrossSize = 18;
        private readonly List<F5Element> rows = new List<F5Element>();
        private readonly List<ItemUiControl> buttons = new List<ItemUiControl>();
        private readonly float[] listY = new float[2];
        internal readonly float[] RowY = new float[3];
        internal float Height { get; private set; }
        internal F5Rect Header { get; private set; }
        internal F5Rect Title { get; private set; }
        internal F5Rect Count { get; private set; }
        internal F5Rect Risk { get; private set; }
        internal F5Rect Empty { get; private set; }
        internal F5Rect Error { get; private set; }
        internal float RevealBottom { get; private set; }
        private float width;
        private ItemAutomationSettings value;
        private ItemSelection selection;
        private bool enabled;
        private Func<string, float, F5Size> measure;
        private static string[] basic = { "开启", "关闭", "键" }, listed = { "添加", "开启", "关闭", "键" };
        internal static int Columns(float width, float card) { return Math.Max(1, (int)((width - 16 + Gap) / (card + Gap))); }
        // Button width fits ten columns in the 506px inner grid. Keep the old
        // icon fitting box centered independently so wide items never shrink
        // with the surrounding button; their drawn width still fits the card.
        internal static F5Rect IconBounds(F5Rect card, bool candidate)
        {
            float width = candidate ? 48 : 50;
            return new F5Rect(card.X + (card.Width - width) / 2, card.Y, width, card.Height);
        }
        internal void Build(float width, float rowHeight, ItemAutomationSettings value, ItemSelection selection,
            bool enabled, bool error, Func<string, float, F5Size> measure)
        {
            this.width = width; this.value = value; this.selection = selection; this.enabled = enabled; this.measure = measure;
            rows.Clear(); buttons.Clear(); Header = Title = Count = Risk = Empty = Error = default(F5Rect); RevealBottom = 0;
            var feedbackOn = measure("提示 开", .7f); var feedbackOff = measure("提示 关", .7f);
            var feedbackSpace = new F5Size(Math.Max(feedbackOn.Width, feedbackOff.Width), Math.Max(feedbackOn.Height, feedbackOff.Height));
            // Reserve both toggle states before row sizing/wrapping. These row
            // button labels are not drawn: Make below keeps the current text's
            // real metrics/offsets for the shared renderer and centered label.
            var rowLayout = new F5RowLayout(rows, (text, scale) => text == "提示 开" || text == "提示 关" ? feedbackSpace : measure(text, scale));
            float y = 0;
            for (int i = 0; i < 3; i++)
            {
                RowY[i] = y; int start = rows.Count;
                var action = (ItemActionKind)i; var list = action == ItemActionKind.Sell ? ItemListKind.Sell : ItemListKind.Discard;
                // An open selector owns adding/replacing; omit its redundant entry from both paint and hit controls.
                string[] actions = i == 0 || selection.List == list ? basic : listed;
                if (action == ItemActionKind.Discard)
                {
                    var withFeedback = new string[actions.Length + 1];
                    withFeedback[0] = value.DiscardFeedbackEnabled ? "提示 开" : "提示 关";
                    Array.Copy(actions, 0, withFeedback, 1, actions.Length); actions = withFeedback;
                }
                rowLayout.Row(ref y, 0, width, ItemsPresentation.Name(action), actions);
                foreach (F5Element e in rows.GetRange(start, rows.Count - start))
                {
                    if (e.Kind == F5ElementKind.Hotkey)
                        buttons.Add(new ItemUiControl { Command = ItemUiCommand.Hotkey, Argument = i, Rect = e.Rect, Generation = selection.Generation,
                            Enabled = enabled, Element = new F5Element(e.Kind, e.Rect, null, e.TextSize, e.TextScale, F5Command.None, Hotkeys.HotkeyActionIds.Items[i]) });
                    if (e.Kind == F5ElementKind.Button)
                    {
                        bool add = e.Text == "添加", on = e.Text == "开启", feedback = e.Text == "提示 开" || e.Text == "提示 关";
                        buttons.Add(Make(feedback ? ItemUiCommand.ToggleDiscardFeedback : add ? ItemUiCommand.Add : on ? ItemUiCommand.Enable : ItemUiCommand.Disable,
                            add ? (int)list : i, 0, e.Rect, e.Text, !add && !feedback && value.Enabled(action) == on, enabled));
                    }
                }
                if (i == 0) continue;
                listY[(int)list] = y;
                if (selection.List == list) BuildSelector(ref y, rowHeight, list);
                else
                {
                    int count = ItemSelection.Types(value, list).Count;
                    if (count > 0) y += (float)Math.Ceiling(count / (double)Columns(width, CardWidth)) * (CardHeight + Gap) + 2;
                }
            }
            if (error) { Error = new F5Rect(8, y, width - 16, rowHeight * 2); y = Error.Bottom; }
            Height = y;
        }
        private void BuildSelector(ref float y, float row, ItemListKind list)
        {
            Header = new F5Rect(8, y, width - 16, row);
            float cross = Math.Max(30, measure("×", .7f).Width + 16);
            float confirm = Math.Max(54, measure("确定", .7f).Width + 16);
            float right = Header.Right;
            if (selection.Target == 0)
            {
                buttons.Add(Make(ItemUiCommand.Confirm, (int)list, 0, new F5Rect(right - confirm, y, confirm, row), "确定", false, enabled && selection.Count > 0));
                right -= confirm + Gap;
            }
            buttons.Add(Make(ItemUiCommand.Cancel, (int)list, 0, new F5Rect(right - cross, y, cross, row), "×", false, true));
            right -= cross + 8;
            if (selection.Target == 0)
            {
                float countWidth = Math.Min((right - Header.X) * .48f, measure("已选 58 项", .63f).Width + 8);
                Count = new F5Rect(right - countWidth, y, countWidth, row); right = Count.X - 4;
            }
            Title = new F5Rect(Header.X, y, Math.Max(0, right - Header.X), row); y += row + Gap;
            if (value.Enabled(list == ItemListKind.Sell ? ItemActionKind.Sell : ItemActionKind.Discard))
            { Risk = new F5Rect(8, y, width - 16, row); y += row + Gap; }
            listY[(int)list] = y;
            if (selection.Candidates.Count == 0) { Empty = new F5Rect(8, y, width - 16, row); y += row; }
            else y += (float)Math.Ceiling(selection.Candidates.Count / (double)Columns(width, CandidateWidth)) * (CandidateSize + Gap);
            RevealBottom = Math.Min(y, listY[(int)list] + CandidateSize);
            y += 6;
        }
        private ItemUiControl Make(ItemUiCommand command, int list, int type, F5Rect rect, string text, bool selected, bool enabled)
        { return new ItemUiControl { Command = command, Argument = list, Type = type, Rect = rect, Generation = selection.Generation,
            Selected = selected, Enabled = enabled, Element = new F5Element(F5ElementKind.Button, rect, text, measure(text, .7f), .7f, F5Command.None) }; }
        internal void Project(F5Rect view, float scroll, List<F5Element> elements, List<ItemUiControl> controls)
        {
            elements.Clear(); controls.Clear(); float dy = view.Y - scroll;
            foreach (var e in rows)
                if (Visible(e.Rect, scroll, view.Height)) elements.Add(new F5Element(e.Kind, e.Rect.Offset(view.X, dy), e.Text, e.TextSize, e.TextScale, e.Command));
            foreach (var c in buttons)
                if (Visible(c.Rect, scroll, view.Height)) controls.Add(Offset(c, view.X, dy));
            for (int i = 0; i < 2; i++)
            {
                var list = (ItemListKind)i; bool choosing = selection.List == list;
                var types = choosing ? selection.Candidates : ItemSelection.Types(value, list);
                float w = choosing ? CandidateWidth : CardWidth, h = choosing ? CandidateSize : CardHeight;
                int columns = Columns(width, w);
                int first = Math.Max(0, (int)Math.Floor((scroll - listY[i]) / (h + Gap)));
                int last = Math.Min((types.Count - 1) / columns, (int)Math.Floor((scroll + view.Height - listY[i]) / (h + Gap)));
                for (int row = first; row <= last; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col; if (index >= types.Count) break;
                        var rect = new F5Rect(view.X + 8 + col * (w + Gap), dy + listY[i] + row * (h + Gap), w, h);
                        if (!choosing) controls.Add(Make(ItemUiCommand.Remove, i, types[index], new F5Rect(rect.Right - CrossSize, rect.Y, CrossSize, CrossSize), "", false, enabled));
                        controls.Add(Make(choosing ? ItemUiCommand.Select : ItemUiCommand.Replace, i, types[index], rect, "", choosing && selection.IsSelected(types[index]), enabled && (!choosing || selection.IsSelected(types[index]) || selection.CanSelect(types[index]))));
                    }
            }
        }
        private static bool Visible(F5Rect r, float scroll, float height) { return r.Bottom > scroll && r.Y < scroll + height; }
        private static ItemUiControl Offset(ItemUiControl c, float x, float y)
        { var r = c.Rect.Offset(x, y); return new ItemUiControl { Command = c.Command, Argument = c.Argument, Type = c.Type, Generation = c.Generation,
            Rect = r, Selected = c.Selected, Enabled = c.Enabled, Element = new F5Element(c.Element.Kind, r, c.Element.Text, c.Element.TextSize, c.Element.TextScale, c.Element.Command, c.Element.HotkeyTarget) }; }
    }
}
