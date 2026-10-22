using System;
using System.Linq;
using JueMingR.Features.Movement;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class MovementConfigurationChecks
    {
        internal static void Run()
        {
            var owner = new Controls(); var controls = new MovementControls(owner);
            var layout = new F5Layout { MovementAttached = true }; object font = new object();
            Func<string, F5Size> measure = text => new F5Size(text.Length * 14, 22);
            layout.Ensure(1280, 720, 1.5f, 11, font, measure);
            Require(layout.Elements.Count(e => e.Kind == F5ElementKind.Hotkey) == 4, "four row-end shared bindings");
            Require(layout.Elements.Count(e => e.Text == "智能防摔") == 1, "one standard rescue row");
            Require(!controls.ConfigurationExpanded && !layout.Elements.Any(e => e.Command == F5Command.MovementFallConfigure), "configuration entry is temporarily hidden and starts collapsed");
            controls.Execute(F5Command.MovementFallAllow0+1);var retained=owner.SmartFallProtection;
            layout.Ensure(1280,720,1.5f,11,font,measure);
            Require(owner.SmartFallProtection.Equals(retained),"hidden entry preserves previously selected permissions");
            controls.Execute(F5Command.MovementFallAllow0);
            // The existing implementation remains callable when its source
            // label is restored; hiding the entry does not delete any handler.
            controls.Execute(F5Command.MovementFallConfigure); layout.SetMovementConfiguration(controls.ConfigurationExpanded);
            int generation = layout.Generation; layout.Ensure(1280, 720, 1.5f, 11, font, measure);
            Require(layout.Generation == generation + 1, "opening configuration invalidates only layout");
            foreach (var element in layout.Elements)
                Require(element.Rect.X >= 0 && element.Rect.Right <= 522.01f || element.Kind == F5ElementKind.Panel, "expanded measured rows stay in page width");
            for (int i = 0; i < 12; i++)
            {
                var allow = (F5Command)((int)F5Command.MovementFallAllow0 + 2 * i); var deny = allow + 1;
                Require(layout.Elements.Count(e => e.Command == allow) == 1 && layout.Elements.Count(e => e.Command == deny) == 1, "each permission has unique hit targets");
                Require(controls.Selected(allow) == Color.LightGreen, "default permission selection");
                controls.Execute(deny); Require(!owner.SmartFallProtection.Allows(i) && !owner.IsEnabled(3), "permission changes without enabling main");
                Require(controls.Selected(deny) == Color.IndianRed, "disabled permission selected");
                controls.Execute(allow);
            }
            controls.Execute(F5Command.MovementFallOn); Require(owner.IsEnabled(3), "fourth main command targets rescue");
            Require(!owner.IsEnabled(0) && !owner.IsEnabled(1) && !owner.IsEnabled(2), "rescue does not enable neighbouring features");
            controls.Execute(F5Command.MovementFallOff); controls.Execute(F5Command.MovementFallConfigure);
            layout.SetMovementConfiguration(controls.ConfigurationExpanded); layout.Ensure(1280, 720, 1.5f, 11, font, measure);
            Require(!layout.Elements.Any(e => e.Command == F5Command.MovementFallAllow0), "closing removes permission hit targets");
            Console.WriteLine("PASS movement layout: four rows, measured config, twelve independent permissions; CPU only");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class Controls : IMovementControls
        {
            private MovementOptions options = new MovementOptions();
            public event Action DashModeChanged { add { } remove { } }
            public bool ControlsEnabled => true;
            public bool Available(int feature) => true;
            public bool IsEnabled(int feature) => options.Enabled(feature);
            public ContinuousDashMode DashMode => options.DashMode;
            public SmartFallProtectionOptions SmartFallProtection => options.SmartFallProtection;
            public bool SetEnabled(int feature, bool enabled) { options = options.WithEnabled(feature, enabled); return true; }
            public bool SetDashMode(ContinuousDashMode mode) { options = options.WithMode(mode); return true; }
            public bool SetSmartFallPermission(int capability, bool allowed) { options = options.WithSmartFallProtection(options.SmartFallProtection.WithPermission(capability, allowed)); return true; }
            public void TakeFeedback(Action<string> display) { }
        }
    }
}
