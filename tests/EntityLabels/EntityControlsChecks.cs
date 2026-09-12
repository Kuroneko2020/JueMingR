using System;
using System.Linq;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class EntityControlsChecks
    {
        internal static void Run()
        {
            WorldObjectControlsChecks.Run();
            var layout = new F5Layout();
            layout.Ensure(1280, 900, 1, 9, new object(), text => new F5Size(text.Length * 14, 24));
            var buttons = layout.Elements.Where(e => e.Kind == F5ElementKind.Button).ToArray();
            Check(buttons[0].Command != F5Command.None && buttons[1].Command != F5Command.None && buttons[2].Command != F5Command.None,
                "existing enemy row must carry real configure/enable/disable consumers");
            string[] expected = { "entity-labels.enemy.toggle", "entity-labels.critter.toggle", "entity-labels.npc.toggle", "biome-display.toggle",
                "world-targets.life-crystal.toggle", "world-targets.life-fruit.toggle", "world-targets.mana-crystal.toggle", "world-targets.sleeping-digtoise.toggle", "world-targets.chillet-egg.toggle",
                "world-object-text.chest.toggle", "world-object-text.sign.toggle", "world-object-text.tombstone.toggle" };
            Check(layout.Elements.Where(e => e.HotkeyTarget != null).Select(e => e.HotkeyTarget).OrderBy(id => id).SequenceEqual(expected.OrderBy(id => id)), "old stable hotkey rows survive with exactly three new identities");
            Check(layout.Elements.Where(e => e.Command != F5Command.None).All(e => EntityLabelControls.Target(e.Command).HasValue || WorldTargetControls.Target(e.Command).HasValue || WorldObjectControls.Target(e.Command).HasValue ||
                e.Command == F5Command.EnableBiome || e.Command == F5Command.DisableBiome), "only accepted information commands activate");
            var shell = new F5Interaction { Ready = true };
            shell.Update(new F5Input { Width = 1280, Height = 900, Scale = 1, Active = true, Focused = true, F5 = true });
            shell.Layout.Ensure(1280, 900, 1, 9, new object(), text => new F5Size(text.Length * 14, 24));
            F5Element enable = shell.Layout.Elements.First(e => e.Command == buttons[1].Command);
            F5Rect rect = enable.Rect.Offset(shell.X + shell.Layout.Viewport.X, shell.Y + shell.Layout.Viewport.Y);
            shell.Update(new F5Input { Width = 1280, Height = 900, Scale = 1, Active = true, Focused = true, X = rect.X + 3, Y = rect.Y + 3, Left = true });
            shell.Update(new F5Input { Width = 1280, Height = 900, Scale = 1, Active = true, Focused = true, X = rect.X + 3, Y = rect.Y + 3 });
            Check(shell.Command == enable.Command, "actual F5 hit consumer emits the stable command");
            Console.WriteLine("PASS: actual information rows and F5 click consumer expose only accepted entity/world/biome controls.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
