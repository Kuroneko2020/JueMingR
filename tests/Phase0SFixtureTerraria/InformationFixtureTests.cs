using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class InformationFixtureTests
    {
        // Catches a decorative configuration button which cannot deliver a
        // command to the real information-page consumer.
        internal static void Run()
        {
            var layout = new F5Layout();
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 10, 20));
            bool found = false;
            foreach (var element in layout.Elements)
                if (element.Command.ToString() == "ConfigureBiome" && element.Kind == F5ElementKind.Button)
                {
                    found = true;
                    var state = new F5Interaction { Ready = true };
                    state.Update(new F5Input { Width = 1920, Height = 1080, Scale = 1, Focused = true, Active = true, F5 = true });
                    state.Navigate(9);
                    state.Layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 10, 20));
                    state.ScrollTo(element.Rect.Y);
                    var sample = new F5Input { Width = 1920, Height = 1080, Scale = 1, Focused = true, Active = true,
                        X = state.X + state.Layout.Viewport.X + element.Rect.X + element.Rect.Width / 2,
                        Y = state.Y + state.Layout.Viewport.Y + element.Rect.Y - state.Scroll + element.Rect.Height / 2, Left = true };
                    state.Update(sample); sample.Left = false; state.Update(sample);
                    if (state.Command.ToString() != "ConfigureBiome") throw new InvalidOperationException("Biome configuration click is not delivered.");
                }
            if (!found) throw new InvalidOperationException("Information page has no working biome configuration entry.");
            Console.WriteLine("PASS: information configuration uses the real F5 page and click consumer.");
        }
    }
}
