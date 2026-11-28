using System;
using System.Collections.Generic;
using JueMingR.Features.Text;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class ChatDraftLayout
    {
        internal sealed class Line
        {
            internal Line(int first, float[] positions, F5Element label)
            { First = first; Positions = positions; Label = label; }
            internal int First { get; }
            internal int Last => First + Positions.Length - 1;
            internal float[] Positions { get; }
            internal F5Element Label { get; }
            internal float X(int element) => Positions[element - First] - Label.TextSize.OffsetX;
        }

        private readonly IReadOnlyList<int> boundaries;
        private int affinity = -1;
        private long affinityRevision = -1;
        private readonly long revision;
        internal string Text { get; }
        internal IReadOnlyList<Line> Lines { get; }

        internal ChatDraftLayout(TextEditBuffer draft, DynamicSpriteFont font, float scale, float width, float height,
            Func<string, float, F5Size> measure, ChatDraftLayout previous)
        {
            Text = draft.Text;
            revision = draft.Revision;
            boundaries = draft.Boundaries;
            var lines = new List<Line>();
            // Restart before the edited wrap boundary. A font or size change
            // supplies no previous layout. Empty frames never enter this path.
            int prefix = previous == null || revision != previous.revision + 1 ? 0 : Math.Max(0, previous.LineOf(draft.LastChangeStart) - 1);
            for (int i = 0; i < prefix; i++) lines.Add(previous.Lines[i]);
            int first = prefix == 0 ? 0 : previous.Lines[prefix].First;
            int element = first;
            var positions = new List<float> { 0 };
            float pen = 0;
            while (element < boundaries.Count - 1)
            {
                int start = boundaries[element], end = boundaries[element + 1];
                if (Text[start] == '\n' || Text[start] == '\r')
                {
                    FinishLine();
                    first = ++element;
                    continue;
                }

                float next = pen;
                if (font != null)
                {
                    // Match the native multiline SpriteBatch pen, including
                    // the final right bearing and advances for trailing spaces.
                    // Cropping moves glyph ink, while the label offsets move the
                    // whole pen with its outline. Keep those effects separate.
                    next = Advance(font, Text, start, end, element == first, pen);
                }
                else next += measure(Text.Substring(start, end - start), scale).Width / scale;

                if (element > first && (next * scale > width || end - boundaries[first] > 1024))
                {
                    FinishLine();
                    first = element;
                    continue;
                }
                positions.Add(next * scale);
                pen = next;
                element++;
            }
            FinishLine();
            Lines = lines;

            void FinishLine()
            {
                string value = Text.Substring(boundaries[first], boundaries[element] - boundaries[first]);
                F5Size size = measure(value, scale);
                var label = new F5Element(F5ElementKind.Text, new F5Rect(0, lines.Count * height, size.Width, height),
                    value, size, scale, F5Command.None);
                lines.Add(new Line(first, positions.ToArray(), label));
                positions.Clear();
                positions.Add(0);
                pen = 0;
            }
        }

        internal static float Advance(DynamicSpriteFont font, string value, int start, int end, bool lineStart, float pen = 0)
        {
            for (int i = start; i < end; i++)
            {
                var metric = font.GetCharacterMetrics(value[i]);
                pen += lineStart && i == start ? metric.KernedWidthOnNewLine : font.CharacterSpacing + metric.KernedWidth;
            }
            return pen;
        }

        internal int CurrentLine(TextEditBuffer editor) => affinityRevision == editor.CaretRevision ? affinity : LineOf(editor.Caret);
        internal int LineOf(int offset)
        {
            int low = 0, high = Lines.Count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (boundaries[Lines[mid].First] <= offset) low = mid;
                else high = mid - 1;
            }
            return low;
        }

        internal int Start(Line line) => boundaries[line.First];
        internal int End(Line line) => boundaries[line.Last];
        internal float X(Line line, int offset)
        {
            int low = line.First, high = line.Last;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (boundaries[mid] <= offset) low = mid;
                else high = mid - 1;
            }
            return line.X(low);
        }

        internal void Click(TextEditBuffer editor, float x, int line, bool extend)
        {
            line = Math.Max(0, Math.Min(Lines.Count - 1, line));
            Move(editor, line, Hit(Lines[line], x), extend);
        }

        internal void Navigate(TextEditBuffer editor, Keys key, bool extend)
        {
            int current = CurrentLine(editor);
            Line line = Lines[current];
            if (key == Keys.Home || key == Keys.End)
                Move(editor, current, key == Keys.Home ? Start(line) : End(line), extend);
            else Click(editor, X(line, editor.Caret), current + (key == Keys.Up ? -1 : 1), extend);
        }

        private int Hit(Line line, float x)
        {
            for (int i = line.First; i < line.Last; i++)
                if (x < (line.X(i) + line.X(i + 1)) / 2) return boundaries[i];
            return End(line);
        }

        private void Move(TextEditBuffer editor, int line, int offset, bool extend)
        {
            editor.MoveTo(offset, extend);
            // A soft break has two visible caret positions for one text index.
            // Retain the line chosen by a click/vertical move until another edit.
            affinity = line;
            affinityRevision = editor.CaretRevision;
        }
    }
}
