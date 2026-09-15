using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class DeathPreferenceChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var codec = new DeathDisplayCodec();
                DeathArchiveChecks.Require(!DeathDisplayPreferences.Default.Enabled && DeathDisplayPreferences.Default.Count == 256, "markers start off with latest 256");
                foreach (int count in new[] { 128, 256, 512, 1024 })
                { var value = codec.Decode(codec.Encode(new DeathDisplayPreferences(true, count))); DeathArchiveChecks.Require(value.Enabled && value.Count == count, "all four quantities survive restart"); }
                bool failed = false;
                try { codec.Decode(Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.DeathMarkers\",\"version\":1,\"enabled\":false,\"count\":999}")); }
                catch (PreferenceFormatException) { failed = true; }
                DeathArchiveChecks.Require(failed, "unsupported count cannot silently turn into a valid preset");
            }
            catch (Exception e) { failures.Add("death marker settings: " + e.Message); }
        }
    }
}
