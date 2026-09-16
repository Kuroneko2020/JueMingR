using System;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Input
{
    // One measured origin owns glyphs, selection, caret and IME. The viewport
    // moves only at complete text-element boundaries; the stored draft is intact.
    internal sealed class SingleLineEditView
    {
        internal string Text = "";
        internal F5Size Size;
        internal float Caret, SelectionLeft, SelectionRight;
        internal void Prepare(TextEditBuffer editor, string composition, float width, Func<string, float, F5Size> measure)
        {
            composition = composition ?? ""; width = Math.Max(0, width);
            string all = editor.Text.Insert(editor.Caret, composition);
            int caret = editor.Caret + composition.Length, start = 0, end = all.Length;
            var boundaries = TextElements.Boundaries(all);
            for (int i = 1; i < boundaries.Length && boundaries[i] <= caret && measure(all.Substring(start, caret - start), .70f).Width > width; i++) start = boundaries[i];
            for (int i = boundaries.Length - 2; i >= 0 && end > start && measure(all.Substring(start, end - start), .70f).Width > width; i--) end = Math.Max(start, boundaries[i]);
            Text = all.Substring(start, end - start); Size = measure(Text, .70f);
            Caret = MeasureTo(all, start, end, caret, measure);
            int left = editor.SelectionStart, right = editor.SelectionEnd;
            if (left > editor.Caret) left += composition.Length;
            if (right >= editor.Caret) right += composition.Length;
            SelectionLeft = MeasureTo(all, start, end, left, measure);
            SelectionRight = MeasureTo(all, start, end, right, measure);
        }
        private static float MeasureTo(string text, int start, int end, int index, Func<string, float, F5Size> measure)
        { return measure(text.Substring(start, Math.Max(start, Math.Min(end, index)) - start), .70f).Width; }
    }
}
