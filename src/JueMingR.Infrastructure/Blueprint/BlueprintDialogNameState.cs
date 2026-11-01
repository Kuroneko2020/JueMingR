using System;
using System.Globalization;
using System.IO;

namespace JueMingR.Infrastructure.Blueprint
{
    // STA-owned state. Only observed callback differences count as manual edits;
    // Common Item Dialog has no per-character filename notification.
    internal sealed class BlueprintDialogNameState
    {
        internal readonly DateTime ExportDate;
        internal long CounterCandidate { get; private set; }
        internal string LastSuggestedLeaf { get; private set; }
        internal bool UserEdited { get; private set; }
        internal BlueprintDialogNameState(DateTime date, long candidate, string leaf)
        { ExportDate = date.Date; CounterCandidate = candidate; LastSuggestedLeaf = leaf; }
        internal void Observe(string leaf)
        { if (!string.Equals(leaf, LastSuggestedLeaf, StringComparison.Ordinal)) UserEdited = true; }
        internal bool AdvanceBatch(string folder, Func<string, bool> exists, out bool changed)
        {
            changed = false;
            for (int count = 0; count < 16; count++)
            {
                if (!exists(Path.Combine(folder, LastSuggestedLeaf))) return true;
                CounterCandidate = checked(CounterCandidate + 1);
                LastSuggestedLeaf = "JM" + ExportDate.ToString("MMdd", CultureInfo.InvariantCulture) + CounterCandidate.ToString("D3", CultureInfo.InvariantCulture) + ".jmbp";
                changed = true;
            }
            return false;
        }
    }
}
