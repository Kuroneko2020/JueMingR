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
            Adjustment();
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
        private static void Adjustment()
        {
            var drag = new JueMingR.TerrariaHost.Information.InformationAdjustment();
            var bounds = new F5Rect(200, 200, 160, 40);
            var sample = new JueMingR.TerrariaHost.Information.InformationPointerSample { Active = true, Focused = true, HigherOwner = false, Session = 1, NativeEpoch = 0, Geometry = 1, X = 210, Y = 210, CanGrab = true };
            Action begin = () => { drag.Begin(new JueMingR.Platform.Settings.WindowPosition(5000, 5000), true, 1); drag.BindGeometry(1); };
            begin(); sample.Left = sample.NewLeft = true; drag.Step(sample, bounds);
            if (drag.Dragging) throw new Exception("entry press tail grabbed the HUD");
            sample.Left = sample.NewLeft = false; sample.Neutral = true; drag.Step(sample, bounds);
            sample.Left = sample.NewLeft = true; drag.Step(sample, bounds); sample.Left = sample.NewLeft = false;
            if (drag.Step(sample, bounds) != null || drag.Active) throw new Exception("stationary release overwrote clamped preference");
            begin(); drag.Step(sample, bounds); sample.Left = sample.NewLeft = true; drag.Step(sample, bounds);
            sample.NewLeft = false; sample.X = 230; drag.Step(sample, bounds); sample.Left = false;
            var saved = drag.Step(sample, bounds);
            if (saved == null || saved.X != 220 || saved.Y != 200 || drag.Active) throw new Exception("real drag did not submit its grab-offset anchor once");
            begin(); sample.X = 210; drag.Step(sample, bounds); sample.Left = sample.NewLeft = true; drag.Step(sample, bounds);
            sample.NewLeft = false; sample.X = 230; drag.Step(sample, bounds); sample.Focused = false; sample.Left = false; drag.Step(sample, bounds);
            if (drag.Active || drag.FinishNormal() != null) throw new Exception("focus loss became a synthetic completion or later exit commit");
            sample.Focused = true; sample.X = 210; begin(); drag.Step(sample, bounds); sample.Left = sample.NewLeft = true; drag.Step(sample, bounds);
            sample.NewLeft = false; sample.X = 240; drag.Step(sample, bounds); sample.Geometry = 2; drag.Step(sample, bounds);
            if (drag.Active || drag.FinishNormal() != null) throw new Exception("new geometry accepted an old drag");
            sample.Geometry = 1; sample.Left = sample.NewLeft = false; sample.X = 210; begin(); drag.Step(sample, bounds);
            sample.Left = sample.NewLeft = true; drag.Step(sample, bounds); sample.Left = sample.NewLeft = false; sample.X = 250;
            saved = drag.Step(sample, bounds);
            if (saved == null || saved.X != 240) throw new Exception("final physical release coordinates were lost");
            sample.X = 210; begin(); drag.Step(sample, bounds); sample.Left = sample.NewLeft = true; drag.Step(sample, bounds);
            sample.NewLeft = false; sample.X = Single.NaN; drag.Step(sample, bounds);
            if (drag.Active || drag.FinishNormal() != null) throw new Exception("nonfinite pointer preserved a committable gesture");
            sample.X = 210; sample.Left = sample.NewLeft = true;
            drag.Begin(null, false, 1, 0, true); drag.BindGeometry(1); drag.Step(sample, bounds);
            if (!drag.Dragging) throw new Exception("genuine neutral entry incorrectly required an extra blank frame");
            sample.NativeEpoch = 1; sample.Left = false; drag.Step(sample, bounds);
            if (drag.Active || drag.FinishNormal() != null) throw new Exception("native world clear retained an old adjustment");
        }
    }
}
