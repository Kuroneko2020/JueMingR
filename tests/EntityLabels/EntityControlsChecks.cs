using System;
using System.Linq;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class EntityControlsChecks
    {
        internal static void Run()
        {
            var layout = new F5Layout();
            layout.Ensure(1280, 900, 1, 9, new object(), text => new F5Size(text.Length * 14, 24));
            var buttons = layout.Elements.Where(e => e.Kind == F5ElementKind.Button).ToArray();
            Check(buttons[0].Command != F5Command.None && buttons[1].Command != F5Command.None && buttons[2].Command != F5Command.None,
                "existing enemy row must carry real configure/enable/disable consumers");
            Check(layout.Elements.Count(e => e.HotkeyTarget != null) == 9, "information page has three entity, five world targets and existing biome");
            Check(layout.Elements.Count(e => e.Command != F5Command.None) == 27, "accepted entity/world/biome controls activate; other samples stay inert");
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
