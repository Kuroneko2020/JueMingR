using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintSelectionChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { Boundaries(); SplitMerge(); MathematicalParity(); VisibleWork(); }
            catch (Exception e) { failures.Add("blueprint selection: " + e.Message); }
        }

        private static void Boundaries()
        {
            var selection = new BlueprintSelection();
            selection.ToggleRectangle(3, 2, 3, 2, 8, 6);
            Expect(selection, 0, 0, 8, 6, new BlueprintSpan(2, 3, 4));
            selection.ToggleRectangle(3, 2, 3, 2, 8, 6);
            Require(!selection.HasSelection, "same single tile twice must delete the empty row");
            selection.ToggleRectangle(7, 5, 5, 4, 8, 6);
            Expect(selection, 0, 0, 8, 6, new BlueprintSpan(4, 5, 8), new BlueprintSpan(5, 5, 8));
            selection.Clear();
            selection.ToggleRectangle(-4, -3, 1, 1, 8, 6);
            Expect(selection, -20, -20, 20, 20, new BlueprintSpan(0, 0, 2), new BlueprintSpan(1, 0, 2));
            selection.ToggleRectangle(-10, -10, -1, -1, 8, 6);
            selection.ToggleRectangle(8, 0, int.MaxValue, 5, 8, 6);
            Expect(selection, 0, 0, 8, 6, new BlueprintSpan(0, 0, 2), new BlueprintSpan(1, 0, 2));
            selection.Clear();
            selection.ToggleRectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, 8, 6);
            Require(Area(selection, 0, 0, 8, 6) == 48, "inclusive normalization must not overflow Int32 endpoints");
            selection.Clear();
            selection.ToggleRectangle(0, 0, 4, 4, 0, 6);
            selection.ToggleRectangle(0, 0, 4, 4, 8, -1);
            Require(!selection.HasSelection, "invalid world geometry cannot invent a selection");
            BlueprintRectangle preview = BlueprintRectangle.FromEndpoints(10, 8, -2, -1, 8, 6);
            Require(preview.Left == 0 && preview.Top == 0 && preview.RightExclusive == 8 && preview.BottomExclusive == 6,
                "preview uses the same world-clipped inclusive rectangle");
        }

        private static void SplitMerge()
        {
            var selection = new BlueprintSelection();
            selection.ToggleRectangle(1, 3, 9, 3, 20, 10);
            selection.ToggleRectangle(4, 3, 6, 3, 20, 10);
            Expect(selection, 0, 0, 20, 10, new BlueprintSpan(3, 1, 4), new BlueprintSpan(3, 7, 10));
            selection.ToggleRectangle(4, 3, 6, 3, 20, 10);
            Expect(selection, 0, 0, 20, 10, new BlueprintSpan(3, 1, 10));
            selection.ToggleRectangle(10, 3, 12, 3, 20, 10);
            Expect(selection, 0, 0, 20, 10, new BlueprintSpan(3, 1, 13));
            selection.ToggleRectangle(8, 3, 15, 3, 20, 10);
            Expect(selection, 0, 0, 20, 10, new BlueprintSpan(3, 1, 8), new BlueprintSpan(3, 13, 16));
            selection.ToggleRectangle(0, 3, 19, 3, 20, 10);
            Expect(selection, 0, 0, 20, 10, new BlueprintSpan(3, 0, 1), new BlueprintSpan(3, 8, 13), new BlueprintSpan(3, 16, 20));
            // Adjacent rows remain independent; erasing one must not erase its neighbor.
            selection.ToggleRectangle(2, 4, 5, 4, 20, 10);
            selection.ToggleRectangle(0, 3, 19, 3, 20, 10);
            Expect(selection, 0, 4, 20, 5, new BlueprintSpan(4, 2, 6));
        }

        private static void MathematicalParity()
        {
            const int width = 19, height = 13;
            var selection = new BlueprintSelection();
            var rectangles = new List<int[]>();
            var random = new Random(49371);
            for (int step = 0; step < 180; step++)
            {
                int[] rectangle = { random.Next(-6, 25), random.Next(-5, 19), random.Next(-6, 25), random.Next(-5, 19) };
                rectangles.Add(rectangle);
                selection.ToggleRectangle(rectangle[0], rectangle[1], rectangle[2], rectangle[3], width, height);
                bool[,] actual = ExpandSmall(selection, width, height);
                bool any = false;
                // Independent oracle: membership is odd rectangle containment.
                // It does not use row intervals, clipping helpers or XOR code.
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int contained = 0;
                        foreach (int[] r in rectangles)
                            if (Between(x, r[0], r[2]) && Between(y, r[1], r[3])) contained++;
                        bool expected = contained % 2 != 0;
                        Require(actual[x, y] == expected, "independent odd-containment oracle at step " + step + " tile " + x + "," + y);
                        any |= expected;
                    }
                Require(selection.HasSelection == any, "HasSelection agrees with mathematical membership");
            }
        }

        private static void VisibleWork()
        {
            var selection = new BlueprintSelection();
            selection.ToggleRectangle(0, 0, 8399, 2399, 8400, 2400);
            Require(Area(selection, 0, 0, 8400, 2400) == 20160000L, "full large world has no 16384 or replacement area cap");
            Expect(selection, 4100, 1100, 4110, 1103,
                new BlueprintSpan(1100, 4100, 4110), new BlueprintSpan(1101, 4100, 4110), new BlueprintSpan(1102, 4100, 4110));
#if DEBUG
            Require(selection.DebugChangedRows == 2400, "solid commit work follows height, not area");
            Require(selection.DebugVisibleRows == 3 && selection.DebugVisibleSpans == 3 && selection.DebugVisibleSearchComparisons == 3,
                "narrow view visits only visible Y rows and one span per row");
#endif
            selection.ToggleRectangle(4200, 1200, 4200, 1200, 8400, 2400);
#if DEBUG
            Require(selection.DebugChangedRows == 1, "local edit touches only its own row");
#endif
            Expect(selection, 4199, 1200, 4202, 1201, new BlueprintSpan(1200, 4199, 4200), new BlueprintSpan(1200, 4201, 4202));
            Expect(selection, 1, 1, 1, 7);
            selection.Clear();
            selection.ToggleRectangle(0, 0, int.MaxValue, 0, int.MaxValue, 1);
            Require(Area(selection, 0, 0, int.MaxValue, 1) == int.MaxValue, "one huge horizontal span stores width without per-tile expansion");
            selection.Clear();
            for (int i = 0; i < 4096; i++) selection.ToggleRectangle(i * 4, 10, i * 4, 10, 20000, 20);
            Expect(selection, 16380, 10, 16381, 11, new BlueprintSpan(10, 16380, 16381));
#if DEBUG
            Require(selection.DebugVisibleRows == 1 && selection.DebugVisibleSpans == 1 && selection.DebugVisibleSearchComparisons <= 13,
                "fragmented row starts by binary search instead of scanning its offscreen prefix");
#else
            Require(typeof(BlueprintSelection).GetProperty("DebugVisibleRows") == null, "normal Release excludes finite workload probes");
#endif
            // Querying neither toggles nor discards offscreen content.
            Require(Area(selection, 0, 0, 20000, 20) == 4096, "visible query preserves every offscreen span");
            selection.Clear();
            Expect(selection, 0, 0, 100, 100);
            Require(!selection.HasSelection, "Clear removes all committed rows");
        }

        private static bool Between(int value, int a, int b)
        { return (a <= value && value <= b) || (b <= value && value <= a); }

        private static bool[,] ExpandSmall(BlueprintSelection selection, int width, int height)
        {
            var actual = new bool[width, height];
            int previousY = -1, previousRight = -1;
            foreach (BlueprintSpan span in selection.EnumerateVisible(0, 0, width, height))
            {
                Require(span.Left >= 0 && span.RightExclusive <= width && span.Left < span.RightExclusive, "nonempty bounded span");
                Require(span.Y > previousY || (span.Y == previousY && span.Left > previousRight), "sorted spans must be disjoint and nonadjacent");
                for (int x = span.Left; x < span.RightExclusive; x++) actual[x, span.Y] = true;
                previousY = span.Y; previousRight = span.RightExclusive;
            }
            return actual;
        }

        private static long Area(BlueprintSelection selection, int left, int top, int right, int bottom)
        {
            long area = 0;
            foreach (BlueprintSpan span in selection.EnumerateVisible(left, top, right, bottom)) area += (long)span.RightExclusive - span.Left;
            return area;
        }

        private static void Expect(BlueprintSelection selection, int left, int top, int right, int bottom, params BlueprintSpan[] expected)
        {
            int index = 0;
            foreach (BlueprintSpan span in selection.EnumerateVisible(left, top, right, bottom))
            {
                Require(index < expected.Length, "unexpected extra visible span");
                Require(span.Y == expected[index].Y && span.Left == expected[index].Left && span.RightExclusive == expected[index].RightExclusive,
                    "visible span differs at index " + index);
                index++;
            }
            Require(index == expected.Length, "missing visible span");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
