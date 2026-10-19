using System;
using System.Linq;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;

namespace Terraria
{
    // Real row/layout/interaction consumers with a narrow command owner. This
    // does not substitute for native zoom input, world drawing or file I/O.
    internal static class ViewControlChecks
    {
        internal static void Run()
        {
            foreach (bool hidden in new[] { false, true }) LayoutAndInput(hidden);
            var owner = new ViewOwner(); var controls = new ViewControls(owner);
            Require(controls.Available(F5Command.EnableViewUnlock) && controls.Available(F5Command.DisableViewUnlock), "ready public controls");
            Require(controls.Selected(F5Command.DisableViewUnlock) == Color.IndianRed, "default disabled projection");
            controls.Execute(F5Command.EnableViewUnlock);
            Require(owner.Enabled && owner.Commands == 1 && controls.Selected(F5Command.EnableViewUnlock) == Color.LightGreen, "enable reaches actual owner once");
            controls.Execute(F5Command.MarkerDisable);
            Require(owner.Commands == 1 && !ViewControls.Owns(F5Command.MarkerEnable) && !MapControls.Owns(F5Command.EnableViewUnlock), "distinct command ownership");
            controls.Execute(F5Command.DisableViewUnlock);
            Require(!owner.Enabled && controls.Selected(F5Command.DisableViewUnlock) == Color.IndianRed, "retained wide camera cannot turn permission projection green");
            owner.Ready = false;
            controls.Execute(F5Command.EnableViewUnlock);
            Require(!owner.Enabled && owner.Commands == 2 && controls.Hint(F5Command.EnableViewUnlock) == "视距解锁暂不可用。", "loading/unavailable cannot dispatch");
            Console.WriteLine("PASS: public View standard row, exact description, cached layout and click ownership.");
        }

        private static void LayoutAndInput(bool hidden)
        {
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Active = true, Focused = true, Width = 1280, Height = 720, Scale = 1.5f };
            state.Navigate(2); state.RestoreVisible(); state.Layout.SetHiddenFeaturesVisible(hidden);
            object font = new object(); Func<string, F5Size> measure = s => new F5Size(s.Length * 18, 24);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, measure);
            var name = state.Layout.Elements.Single(e => e.Text == "视距解锁");
            Require(name.Description != null && name.Description.Text == "可以调整到更大的视野范围", "exact requested public hint");
            Require(name.Command == F5Command.None && name.HotkeyTarget == null, "name is display only");
            var buttons = state.Layout.Elements.Where(e => ViewControls.Owns(e.Command)).ToArray();
            Require(buttons.Length == 2 && buttons.Select(e => e.Text).SequenceEqual(new[] { "开启", "关闭" }), "only standard Enable/Disable");
            Require(state.Layout.Elements.All(e => e.HotkeyTarget != "view-unlock"), "no extra R binding entry");
            Require(buttons.All(e => e.Rect.Y >= 0 && e.Rect.Bottom <= state.Layout.ContentHeight + .01f), "row participates in final scroll geometry");
            int generation = state.Layout.Generation, measurements = state.Layout.MeasurementCount;
            for (int i = 0; i < 100; i++) state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, measure);
            Require(state.Layout.Generation == generation && state.Layout.MeasurementCount == measurements, "stable page reuses geometry and font metrics");
            var button = buttons[0];
            // Let the real interaction project its centered window before
            // calculating page-local coordinates for the physical gesture.
            state.Update(input);
            state.ScrollTo(button.Rect.Y);
            input.X = state.X + state.Layout.Viewport.X + button.Rect.X + 2;
            input.Y = state.Y + state.Layout.Viewport.Y + button.Rect.Y - state.Scroll + 2;
            state.Update(input); input.Left = true; state.Update(input); input.Left = false; state.Update(input);
            Require(state.Command == F5Command.EnableViewUnlock && state.ConsumeLeft, "real press/release emits one command and owns its release");
            input.Left = true; state.Update(input); input.Focused = false; input.Left = false; state.Update(input);
            Require(state.Command == F5Command.None, "focus loss cancels an armed view control");
            state.Navigate(9); state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, measure);
            Require(!state.Layout.Elements.Any(e => ViewControls.Owns(e.Command)), "row is confined to Map page");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("View UI: " + message); }

        private sealed class ViewOwner : IViewControls
        {
            internal bool Ready = true, Enabled;
            internal int Commands;
            public bool ControlsEnabled { get { return Ready; } }
            public bool UnlockEnabled { get { return Enabled; } }
            public bool SetUnlockEnabled(bool value) { Commands++; Enabled = value; return true; }
            public void TakeFeedback(Action<string> display) { }
        }
    }
}
