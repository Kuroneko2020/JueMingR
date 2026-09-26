using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JueMingR.Features.Processing
{
    // Each business has its own document/commit lifetime. Names are complete
    // user strings, never a comma-separated query or an item-name search.
    public sealed class ProcessingOptions
    {
        public bool Enabled {get;}
        public IReadOnlyList<string> Names {get;}
        public ProcessingOptions(bool enabled=false,IEnumerable<string> names=null)
        {
            var values=(names??Enumerable.Empty<string>()).ToArray();
            if(values.Length>256 || values.Any(s=>string.IsNullOrWhiteSpace(s) || s.Length>128 || s!=s.Trim() || s.Any(char.IsControl)) || values.Distinct(StringComparer.Ordinal).Count()!=values.Length)
                throw new ArgumentException("Invalid complete prefix names.");
            Enabled=enabled;Names=new ReadOnlyCollection<string>(values);
        }
    }
}
