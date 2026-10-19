using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;

namespace JueMingR.ArchitectureTests
{
    internal static class TeleportSearchChecks
    {
        internal static void Check(List<string> failures)
        { try { Run(); } catch (Exception error) { failures.Add("Teleport search: " + error.Message); } }

        internal static void Run()
        {
            float x, y; int count;
            var legal = new Query { OriginalLegal = true };
            Require(!TeleportTargetSearch.TryFind(100, 200, ref legal, out x, out y, out count) && x == 100 && y == 200 && count == 0 && legal.Calls == 1,
                "legal original is retained without enumerating candidates");
            var nearby = new Query { AcceptedX = 100, AcceptedY = 192 };
            Require(TeleportTargetSearch.TryFind(100, 200, ref nearby, out x, out y, out count) && x == 100 && y == 192 && count == 1,
                "nearest upward candidate wins and stops immediately");
            var horizontal = new Query { AcceptedX = 92, AcceptedY = 200 };
            Require(TeleportTargetSearch.TryFind(100, 200, ref horizontal, out x, out y, out count) && count == 2,
                "equal-distance order is upward then left");
            var trace = new Query { Trace = new List<Point>() };
            Require(!TeleportTargetSearch.TryFind(100, 200, ref trace, out x, out y, out count) && x == 100 && y == 200,
                "no candidate retains both original coordinates");
            int independentCount = 0;
            for (int row = -26; row <= 26; row++) for (int col = -26; col <= 26; col++)
                if ((row != 0 || col != 0) && Math.Sqrt(row * row + col * col) <= 26) independentCount++;
            Require(count == independentCount && count == TeleportTargetSearch.CandidateCount && trace.Trace.Count == count + 1,
                "finite complete half-tile circle is enumerated exactly once");
            var seen = new HashSet<string>(); int previousD = -1, previousY = int.MinValue, previousX = int.MinValue;
            bool northEdge = false, outsideEdge = false;
            for (int i = 1; i < trace.Trace.Count; i++)
            {
                int dx = (int)trace.Trace[i].X - 100, dy = (int)trace.Trace[i].Y - 200, d = dx * dx + dy * dy;
                Require((dx != 0 || dy != 0) && dx % 8 == 0 && dy % 8 == 0 && d <= 208 * 208 && seen.Add(dx + ":" + dy),
                    "only unique nonzero circle offsets occur");
                Require(d > previousD || d == previousD && (dy > previousY || dy == previousY && dx > previousX),
                    "global distance, vertical and horizontal ordering is strict");
                previousD = d; previousY = dy; previousX = dx;
                if (dx == 0 && dy == -208) northEdge = true;
                if (dx == 208 && dy == 8) outsideEdge = true;
            }
            Require(northEdge && !outsideEdge, "exact radius included; square corner outside circle excluded");
            var boundary = new Query { AcceptedX = 100, AcceptedY = -8 };
            Require(TeleportTargetSearch.TryFind(100, 200, ref boundary, out x, out y, out count) && y == -8,
                "radius endpoint remains available after nearer samples fail");
            var bad = new Query();
            Require(!TeleportTargetSearch.TryFind(float.NaN, 200, ref bad, out x, out y, out count) && count == 0 && bad.Calls == 0,
                "nonfinite input performs no query");
            Console.WriteLine("PASS teleport pure circle candidates=" + independentCount + " legal=0 nearby=1 worst=" + independentCount);
        }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private struct Point { internal float X, Y; internal Point(float x, float y) { X = x; Y = y; } }
        private struct Query : ITeleportTargetValidator
        {
            internal bool OriginalLegal;
            internal float AcceptedX, AcceptedY;
            internal int Calls;
            internal List<Point> Trace;
            public bool IsValid(float x, float y)
            { Calls++; Trace?.Add(new Point(x, y)); return OriginalLegal || AcceptedX == x && AcceptedY == y; }
        }
    }
}
