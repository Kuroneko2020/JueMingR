using System;
using System.Collections.Generic;
namespace JueMingR.TerrariaHost.F5
{
    // Shared row geometry; callers own commands and cache border-inclusive metrics.
    internal sealed class F5RowLayout
    {
        private const float HotkeySlotWidth = 22;
        private readonly List<F5Element> elements;
        private readonly Func<string, float, F5Size> TextSize;
        internal F5RowLayout(List<F5Element> elements, Func<string, float, F5Size> measure)
        { this.elements = elements; TextSize = measure; }
        internal void Row(ref float y, float x, float width, string label, string[] actions, Func<string, F5Command> commandFor = null)
        {
            float actionWidth = 0;
            foreach (string action in actions)
            {
                if (action == null) { actionWidth += 124; continue; }
                if (action == "键") { actionWidth += HotkeySlotWidth + 4; continue; }
                F5Size size = TextSize(action, 0.70f);
                actionWidth += Math.Max(30, size.Width + 16) + 4;
            }
            if (actions.Length > 0) actionWidth -= 4;
            bool below = actionWidth > width - 120;
            float textWidth = below || actions.Length == 0 ? width - 16 : width - actionWidth - 28;
            int panel = elements.Count;
            Panel(new F5Rect(x, y, width, 34));
            int firstText = elements.Count;
            float labelY = y + 4;
            TextLines(label, x + 8, ref labelY, textWidth, 0.75f);
            float textHeight = labelY - 1 - (y + 4);
            float buttonHeight = 30;
            foreach (string action in actions)
                if (action != null && action != "键") buttonHeight = Math.Max(buttonHeight, TextSize(action, 0.70f).Height + 8);
            float rowHeight = Math.Max(38, Math.Max(textHeight, below ? 0 : buttonHeight) + 8);
            float shift = (rowHeight - textHeight) / 2 - 4;
            for (int i = firstText; i < elements.Count; i++)
            {
                F5Element text = elements[i];
                elements[i] = new F5Element(text.Kind, text.Rect.Offset(0, shift), text.Text,
                    text.TextSize, text.TextScale, text.Command);
            }
            float end = y + rowHeight;
            if (actions.Length > 0)
            {
                float buttonY = below ? end + 2 : y + (rowHeight - buttonHeight) / 2;
                Buttons(ref buttonY, below ? x + 8 : x + width - 8 - actionWidth,
                    below ? width - 16 : actionWidth, actions, commandFor);
                if (below) end = buttonY;
            }
            elements[panel] = new F5Element(F5ElementKind.Panel, new F5Rect(x, y, width, end - y), null, default(F5Size), 0, F5Command.None);
            y = end + 6;
        }

        internal void Buttons(ref float y, float x, float width, string[] labels, Func<string, F5Command> commandFor = null)
        {
            float cursor = x, rowHeight = 30;
            foreach (string label in labels)
                if (label != null && label != "键") rowHeight = Math.Max(rowHeight, TextSize(label, 0.70f).Height + 8);
            foreach (string label in labels)
            {
                bool hotkey = label == "键";
                F5Size size = label == null || hotkey ? default(F5Size) : TextSize(label, 0.70f);
                float w = label == null ? 120 : hotkey ? HotkeySlotWidth : Math.Max(30, size.Width + 16), h = rowHeight;
                if (w > width) throw new InvalidOperationException("F5 button text exceeds its available column.");
                if (cursor > x && cursor + w > x + width + 0.01f) { y += rowHeight + 4; cursor = x; }
                F5Command command = commandFor == null ? F5Command.None : commandFor(label);
                elements.Add(new F5Element(label == null ? F5ElementKind.Field : hotkey ? F5ElementKind.Hotkey : F5ElementKind.Button,
                    new F5Rect(cursor, y, w, h), hotkey ? null : label, size, 0.70f, command));
                cursor += w + 4;
            }
            y += rowHeight + 4;
        }

        private void Panel(F5Rect rect)
        { elements.Add(new F5Element(F5ElementKind.Panel, rect, null, default(F5Size), 0, F5Command.None)); }

        internal void TextLines(string text, float x, ref float y, float width, float scale)
        {
            string remaining = text;
            while (remaining.Length > 0)
            {
                int count = remaining.Length;
                while (count > 0 && TextSize(remaining.Substring(0, count), scale).Width > width) count--;
                if (count == 0) throw new InvalidOperationException("F5 font cannot fit a readable character.");
                string line = remaining.Substring(0, count);
                F5Size size = TextSize(line, scale);
                elements.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), line, size, scale, F5Command.None));
                y += size.Height + 1;
                remaining = remaining.Substring(count);
            }
        }

    }
}
