using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.ArchitectureTests
{
    internal static class ObjectSettingsChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var value = WorldObjectSettings.Default; var codec = new WorldObjectCodec();
                Require(!value.AnyEnabled, "initially all off");
                int[] colors = { 0xFFA500, 0xE6C16A, 0xFF5555 };
                for (int i = 0; i < 3; i++)
                {
                    var kind = (WorldObjectKind)i; var style = value.Style(kind);
                    Require(style.Rgb == colors[i] && style.Size == 70 && style.Lines == 3 && style.Characters == 80, "independent style defaults");
                    var first = value.Toggle(kind);
                    Require(first.Style(kind).Mode == (i == 0 ? WorldObjectMode.Opened : WorldObjectMode.Lines), "first shortcut uses agreed initial mode");
                    var mode = i == 0 ? WorldObjectMode.Always : WorldObjectMode.Characters;
                    var changed = value.WithMode(kind, mode).WithMode(kind, WorldObjectMode.Off);
                    Require(changed.Toggle(kind).Style(kind).Mode == mode, "UI off and shortcut share last active mode");
                    value = value.With(value.Style(kind).WithColor(i).WithSize(180).WithLimits(10, 1200));
                }
                Require(codec.Decode(codec.Encode(value)).Equals(value), "all fields survive strict codec roundtrip");
                string json = Encoding.UTF8.GetString(codec.Encode(value));
                foreach (string invalid in new[] { json.Replace("\"version\":1", "\"version\":2"), json.Replace("\"size\":180", "\"size\":181"), json.Replace("\"lines\":10", "\"lines\":0"), json.Replace("\"characters\":1200", "\"characters\":1201"), json.Replace("\"rgb\":0", "\"extra\":0,\"rgb\":0"), json.Replace("\"lastMode\":2", "\"lastMode\":0") })
                { bool rejected = false; try { codec.Decode(Encoding.UTF8.GetBytes(invalid)); } catch (PreferenceFormatException) { rejected = true; } Require(rejected, "invalid/future/unknown document must protect original"); }
                Require(WorldObjectSettings.Default.Style(WorldObjectKind.Sign).Rgb == 0xE6C16A, "immutable defaults are not shared mutable arrays");
            }
            catch (Exception e) { failures.Add("World object settings: " + e.Message); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
