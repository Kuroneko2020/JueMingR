using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintSelectionSnapshot
    {
        private readonly BlueprintSpan[] spans;
        public BlueprintSelectionSnapshot(BlueprintSpan[] spans, long revision)
        {
            this.spans = (BlueprintSpan[])(spans ?? throw new ArgumentNullException(nameof(spans))).Clone();
            Revision = revision;
            long count = 0;
            foreach (var span in this.spans) count = checked(count + (long)span.RightExclusive - span.Left);
            Count = count;
        }
        public long Revision { get; }
        public long Count { get; }
        public int SpanCount { get { return spans.Length; } }
        public BlueprintSpan GetSpan(int index) { return spans[index]; }
        public bool Contains(int x, int y)
        {
            int low = 0, high = spans.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                var span = spans[middle];
                if (span.Y < y || span.Y == y && span.RightExclusive <= x) low = middle + 1;
                else high = middle;
            }
            return low < spans.Length && spans[low].Y == y && spans[low].Left <= x;
        }
    }
}
