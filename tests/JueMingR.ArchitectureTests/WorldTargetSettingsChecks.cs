using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class WorldTargetSettingsChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var defaults = WorldTargetSettings.Default; var changed = defaults;
                int[] colors = { 0xFF69B4, 0x7CFC00, 0x66CCFF, 0xFFC460, 0x9370DB };
                foreach (WorldTargetKind kind in Enum.GetValues(typeof(WorldTargetKind)))
                {
                    Require(!defaults.Enabled(kind) && defaults.Color(kind) == colors[(int)kind], "five exact defaults off/colors");
                    changed = changed.WithEnabled(kind, true).WithColor(kind, 0x123456 + (int)kind);
                }
                var reset = changed.ResetColor(WorldTargetKind.SleepingDigtoise);
                Require(reset.Enabled(WorldTargetKind.SleepingDigtoise) && reset.Color(WorldTargetKind.SleepingDigtoise) == 0xFFC460 &&
                    reset.Color(WorldTargetKind.ChilletEgg) == changed.Color(WorldTargetKind.ChilletEgg), "reset one color preserves intent/others");
                var codec = new WorldTargetCodec(); Require(codec.Decode(codec.Encode(changed)).Equals(changed), "five independent fields roundtrip");
                string json = Encoding.UTF8.GetString(codec.Encode(changed));
                Expect(codec, json.Replace("\"version\":1", "\"version\":9"), PreferenceStatus.UnsupportedVersion);
                Expect(codec, json.Replace("\"version\":1", "\"version\":1,\"future\":true"), PreferenceStatus.UnknownFields);
                Expect(codec, json.Replace("\"rgb\":1193046", "\"rgb\":1193046,\"nameSize\":90"), PreferenceStatus.UnknownFields);
                Expect(codec, json.Replace("\"version\":1", "\"version\":1,\"version\":1"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"rgb\":1193046", "\"rgb\":16777216"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"enabled\":true", "\"enabled\":1"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("JueMingR.WorldTargets", "JueMingR.EntityLabels"), PreferenceStatus.Invalid);
            }
            catch (Exception e) { failures.Add("world target preferences: " + e.Message); }
        }
        private static void Expect(WorldTargetCodec codec, string json, PreferenceStatus status)
        {
            try { codec.Decode(Encoding.UTF8.GetBytes(json)); }
            catch (PreferenceFormatException e) { Require(e.Status == status, "truthful protected reason " + e.Status); return; }
            throw new InvalidOperationException("unsafe target settings accepted");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
