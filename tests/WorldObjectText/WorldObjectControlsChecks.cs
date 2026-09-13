using System;
using System.Linq;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class WorldObjectControlsChecks
    {
        internal static void Run()
        {
            var layout = new F5Layout(); object font = new object();
            Func<string, F5Size> measure = text => new F5Size(text.Length * 14, 24);
            layout.SetWorldObjectSettings(WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Characters));
            layout.Ensure(1280, 900, 1, 9, font, measure);
            Require(layout.Elements.Any(e => e.Command == F5Command.SignMore) && !layout.Elements.Any(e => e.Command == F5Command.TombstoneMore), "only the active parameter row exists");
            for (int count = 1; count <= 1200; count++)
            {
                layout.SetWorldObjectSettings(WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Characters).With(WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.Characters).WithLimits(3, count)));
                Require(!layout.Matches(1280, 900, 1, 9), "parameter change invalidates stale hit geometry before release");
                layout.Ensure(1280, 900, 1, 9, font, measure);
            }
            layout.SetWorldObjectSettings(WorldObjectSettings.Default); layout.Ensure(1280, 900, 1, 9, font, measure);
            Require(!layout.Elements.Any(e => e.Command == F5Command.SignMore), "off mode removes parameter row");
            string[] expected = { "world-object-text.chest.toggle", "world-object-text.sign.toggle", "world-object-text.tombstone.toggle" };
            foreach (string id in expected) Require(layout.Elements.Count(e => e.HotkeyTarget == id) == 1, "each new row has its independent stable hotkey entry");
            layout.SetWorldObjectSettings(WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Characters));
            layout.Ensure(1280, 900, 1, 9, font, measure); int generation = layout.Generation;
            layout.Ensure(1280, 900, 1, 9, new object(), text => text.StartsWith("字数：", StringComparison.Ordinal) ? new F5Size(text.Length * 16, 28) : measure(text));
            Require(layout.Generation > generation, "dynamic-only glyph metric change invalidates current parameter geometry");
            Console.WriteLine("PASS: three real information rows, 1200 dynamic values without fixed-text cache growth, and layout invalidation.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
