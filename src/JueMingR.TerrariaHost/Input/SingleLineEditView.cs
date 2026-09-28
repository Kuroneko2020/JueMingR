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
            int low=0,high=Array.BinarySearch(boundaries,caret);if(high<0)high=~high-1;
            // Glyph advances are nonnegative. Find the first whole-element
            // origin that exposes the caret, then the last visible boundary.
            // Long pasted text must not trigger one full measurement per glyph.
            while(low<high){int mid=(low+high)/2;if(measure(all.Substring(boundaries[mid],caret-boundaries[mid]),.70f).Width<=width)high=mid;else low=mid+1;}
            start=boundaries[low];high=boundaries.Length-1;
            while(low<high){int mid=(low+high+1)/2;if(measure(all.Substring(start,boundaries[mid]-start),.70f).Width<=width)low=mid;else high=mid-1;}
            end=boundaries[low];
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
