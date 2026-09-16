using System;
using System.Collections.Generic;
using JueMingR.Features.Exploration;

namespace JueMingR.ArchitectureTests
{
    internal static class ExplorationChecks
    {
        internal static void Check(List<string> failures)
        {
            bool[,] cells = new bool[130, 65]; cells[0, 0] = true; cells[129, 64] = true;
            int reads = 0;
            var count = new ExplorationCounter(130, 65, (x, y) => { reads++; return cells[x, y]; });
            count.SetDynamic(true); Drain(count);
            Check(count.Complete && count.Count == 2 && count.Total == 8450, "World extent and partial edge blocks", failures);
            int stableReads = reads; for (int i = 0; i < 2000; i++) count.Advance(4096, 64);
            Check(reads == stableReads, "Established unchanged map performs no cell reads", failures);
            cells[0, 0] = false; count.Changed(0, 0); cells[70, 32] = true; count.Changed(70, 32); count.Changed(70, 32); Drain(count);
            Check(count.Count == 2, "Duplicate dirty notifications replace block count without double counting", failures);
            count.SetDynamic(false); stableReads = reads; cells[3, 4] = true; count.Changed(3, 4);
            for (int i = 0; i < 2000; i++) count.Advance(4096, 64);
            Check(reads == stableReads && count.Count == 2, "Off has no map reads or dirty work", failures);
            count.Paused = true; count.SetDynamic(true); count.Advance(4096, 64);
            Check(reads == stableReads && count.Paused, "Reopening rebuild does not undo explicit pause", failures);
            count.Paused = false; Drain(count); Check(count.Count == 3, "Off-on starts fresh continuity", failures);
            count.Restart(); count.Advance(4096, 64); cells[0, 0] = true; count.Changed(0, 0); Drain(count);
            Check(count.Count == 4, "Changes behind initial cursor reconcile once", failures);
            int beforeRepeat = reads; count.SetDynamic(true); count.Advance(4096, 64);
            Check(reads == beforeRepeat, "Repeated enable does not restart", failures);
            count.BeginBatch(); cells[0, 0] = false; cells[3, 4] = false; count.Advance(32768, 64);
            Check(!count.Current, "Bulk mutation cannot publish a current result", failures);
            count.EndBatch(); Drain(count); Check(count.Count == 2, "Bulk invalidation rebuilds once", failures);
            count.SetDynamic(false); stableReads = reads; count.BeginBatch(); count.EndBatch();
            for (int i = 0; i < 2000; i++) count.Advance(4096, 64);
            Check(reads == stableReads, "Off bulk reset invalidates history without requesting a scan", failures);
            bool[,] two = new bool[128, 64]; var incremental = new ExplorationCounter(128, 64, (x, y) => two[x, y]); incremental.SetDynamic(true); Drain(incremental);
            two[0, 0] = true; incremental.Changed(0, 0); incremental.Advance(4096, 1);
            two[1, 0] = true; incremental.Changed(1, 0); incremental.Advance(4096, 1); Drain(incremental);
            Check(incremental.Count == 2 && incremental.Current, "Changes behind an active dirty cursor retain their next-pass signal", failures);
            for (int i = 2; i < 2002; i++)
            { two[i % 64, i / 64] = true; incremental.Changed(i % 64, i / 64); incremental.Advance(4096, 1); }
            Check(incremental.Count >= 1900 && incremental.Pending, "Continuous native changes publish bounded progress without falsely claiming caught-up", failures);
            Drain(incremental); Check(incremental.Count == 2002 && incremental.Current, "Continuous changes converge after writer stops", failures);
            incremental.Restart(); incremental.Paused = true; two[63, 63] = true; incremental.Changed(63, 63);
            for (int i = 0; i < 2000; i++) incremental.Advance(4096, 64);
            Check(incremental.Count == 2003 && incremental.Paused && incremental.Scanning, "Paused manual calibration still maintains an established dynamic baseline", failures);
        }
        private static void Drain(ExplorationCounter value)
        { for (int i = 0; i < 10000 && (!value.Complete || value.Pending); i++) value.Advance(32768, 64); if (!value.Complete || value.Pending) throw new InvalidOperationException("Exploration failed to converge."); }
        private static void Check(bool condition, string message, List<string> failures) { if (!condition) failures.Add(message); }
    }
}
