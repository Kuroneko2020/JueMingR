using System;
using System.Collections.Generic;
using JueMingR.Features.DeathHistory;
using JueMingR.Features.Notes;
using JueMingR.Platform.DeathHistory;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class DeathPopupChecks
    {
        internal static void Run()
        {
            var failures = new List<string>();
            Check(failures, "physical quantity click", Quantity);
            Check(failures, "short viewport", Small);
            Check(failures, "full text can close during layout", FullClose);
            Check(failures, "CRLF", Lines);
            Check(failures, "page/full/back and stale row", Navigation);
            Check(failures, "font replacement cancels press", Resources);
            if (failures.Count != 0) throw new Exception(String.Join("\n", failures));
            Console.WriteLine("PASS: death popup physical gestures, short viewport, full-text exit and CRLF.");
        }
        private static void Check(List<string> failures, string name, Action test) { try { test(); } catch (Exception e) { failures.Add(name + ": " + e.Message); } }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static F5Size Measure(string value, float scale) { return new F5Size(value.Length * 12 * scale, 30 * scale); }
        private static void Quantity()
        {
            var h = new Owner(); var d = new Driver(h); d.Popup.Open(true, 2); d.Prepare(); d.Click(128);
            Require(h.Settings.Count == 128 && h.Changes == 1, "a complete physical click selects 128 exactly once");
            d.Click(128); Require(h.Changes == 1, "same value never creates another intent");
            int builds = d.Popup.LayoutBuilds; for (int i = 0; i < 100; i++) d.Prepare(); Require(builds == d.Popup.LayoutBuilds, "stable quantity reuses layout");
            d.Click(0); Require(!d.Popup.Visible, "close is reachable");
        }
        private static void Small()
        {
            var h = new Owner(); var d = new Driver(h) { Height = 220, MeasureText = (value, scale) => new F5Size(value.Length * 12 * scale, 40 * scale) };
            foreach (bool quantity in new[] { true, false })
            {
                d.Popup.Open(quantity, 2); d.Prepare();
                foreach (var text in d.Popup.Text) Require(text.Rect.Bottom <= d.Popup.Panel.Height && text.Rect.Right <= d.Popup.Panel.Width, "text inside viewport");
                for (int i = 0; i < d.Popup.Buttons.Count; i++)
                {
                    var a = d.Popup.Buttons[i].Rect; Require(a.Bottom <= d.Popup.Panel.Height, "button inside viewport");
                    for (int j = i + 1; j < d.Popup.Buttons.Count; j++) { var b = d.Popup.Buttons[j].Rect; Require(a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y, "buttons cannot overlap"); }
                }
                if (quantity) { d.Wheel(-120); d.Click(128); Require(h.Settings.Count == 128, "single visible slot can reach quantity options by wheel"); }
                d.Click(0); Require(!d.Popup.Visible, "short viewport keeps close reachable");
            }
        }
        private static void FullClose()
        {
            var h = new Owner(); var fact = new DeathFact(DeathEventId.Create(DateTimeOffset.Now, Guid.NewGuid()), TimeSpan.Zero, true, 160, 160, new string('W', 100000));
            h.Snapshot = new DeathHistorySnapshot(true, 1, 1, 0, 0, Array.AsReadOnly(new[] { fact }), Array.AsReadOnly(new DeathMarker[0]), null, null, false);
            var d = new Driver(h); d.Popup.Open(false, 2); d.Prepare(); d.Click(10);
            Require(d.Popup.Mode == 3, "reason click enters full state");
            h.Snapshot = new DeathHistorySnapshot(true, 1, 1, 0, 0, h.Snapshot.Rows, h.Snapshot.Markers, fact, null, false, new DeathReadText(fact.Reason));
            d.Prepare(); d.Click(0); Require(!d.Popup.Visible, "incremental layout does not cancel close release");
        }
        private static void Lines()
        {
            var text = new DeathReadText("甲\r\n乙"); var layout = new NotesTextLayout(text.Text, 400, _ => 10, text.Boundaries); while (!layout.Complete) layout.Continue(1024);
            Require(layout.Lines.Count == 2 && text.Text == "甲\r\n乙", "CRLF retains original text and one newline");
        }
        private static DeathFact Fact(int i) { return new DeathFact(DeathEventId.Create(DateTimeOffset.UtcNow.AddSeconds(i), new Guid(i, 0, 0, new byte[8])), TimeSpan.Zero, true, 16, 16, "原因 " + i); }
        private static void Navigation()
        {
            var h = new Owner(); var first = Fact(1); var second = Fact(2);
            h.Snapshot = new DeathHistorySnapshot(true, 12, 1, 1, 0, new[] { first }, new DeathMarker[0], null, null, false);
            var d = new Driver(h); d.Popup.Open(false, 2); d.Prepare(); d.Click(3);
            Require(h.PageRequest == 6 && d.Popup.Offset == 6, "next page requests offset six");
            h.QueryReady = false; d.Prepare(); Require(!d.Popup.Commands.Contains(10), "old row unavailable while replacement query is pending");
            h.Snapshot = new DeathHistorySnapshot(true, 12, 2, 2, 0, new[] { second }, new DeathMarker[0], null, null, false); h.QueryReady = true; d.Prepare(); d.Click(10);
            Require(h.SelectionRequest == second.EventId && d.Popup.SelectedId == second.EventId, "full view selects event identity on second page");
            d.Click(1); Require(d.Popup.Mode == 2 && d.Popup.Offset == 6 && h.SelectionRequest == null, "back preserves list page and clears selected request");
            d.Press(10); h.Snapshot = new DeathHistorySnapshot(true, 12, 3, 3, 0, new[] { first }, new DeathMarker[0], null, null, false); d.Prepare(); d.Release();
            Require(d.Popup.Mode == 2 && h.SelectionRequest == null, "row replacement cannot redirect a held click to another event");
            h.Session++; d.Prepare(); Require(!d.Popup.Visible && h.PageRequest == -1, "new session closes old popup and request");
            d.Popup.Open(false, 2); d.Prepare(); Require(d.Popup.Offset == 0 && h.PageRequest == 0, "explicit reopening starts earliest page");
        }
        private static void Resources()
        {
            var h = new Owner(); var d = new Driver(h); d.Popup.Open(true, 2); d.Prepare(); d.Press(128);
            d.Font = new object(); d.Prepare(); d.Release(); Require(h.Changes == 0, "same-size replacement font cancels old press");
        }
        internal sealed class Owner : IDeathControls
        {
            public long Session { get; set; } = 1;
            public bool ControlsEnabled { get { return true; } }
            public DeathDisplayPreferences Settings { get; private set; } = DeathDisplayPreferences.Default;
            public DeathHistorySnapshot Snapshot { get; set; } = new DeathHistorySnapshot(true, 0, 1, 0, 0, new DeathFact[0], new DeathMarker[0], null, null, false);
            public bool QueryReady { get; set; } = true;
            public string CountText { get { return "0"; } }
            public string DaysText { get { return "0"; } }
            public string PreferenceMessage { get; set; }
            internal int Changes;
            internal long PageRequest; internal string SelectionRequest;
            public bool SetEnabled(bool value) { Settings = new DeathDisplayPreferences(value, Settings.Count); return true; }
            public bool SetCount(int value) { if (value == Settings.Count) return false; Changes++; Settings = new DeathDisplayPreferences(Settings.Enabled, value); return true; }
            public void RequestDetails(long offset) { PageRequest = offset; }
            public void RequestSelection(string id) { SelectionRequest = id; }
            public void TakeFeedback(Action<string> display) { }
        }
        private sealed class Driver
        {
            private readonly HostInputState input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
            internal object Font = new object();
            private float heldX, heldY;
            internal readonly DeathHistoryPopup Popup;
            internal float Height = 600;
            internal Func<string, float, F5Size> MeasureText = Measure;
            internal Driver(Owner owner) { Popup = new DeathHistoryPopup(owner, input); Step(false, 0, 0); }
            internal void Prepare() { Popup.Prepare(800, Height, Font, MeasureText, 0); }
            private void Step(bool down, float x, float y, int wheel = 0)
            {
                input.BeginUpdate(); PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                input.AfterMapping(); Main.keyState = new KeyboardState(); input.AfterKeyboardRefresh();
                Popup.Process(true, 2, x, y, Popup.Matches(800, Height, Font, 0), wheel); Prepare();
            }
            internal void Click(int command)
            { Press(command); Release(); }
            internal void Press(int command)
            { int index = Popup.Commands.IndexOf(command); Require(index >= 0, "requested command exists"); var rect = Popup.Buttons[index].Rect.Offset(Popup.Panel.X, Popup.Panel.Y); heldX = rect.X + 4; heldY = rect.Y + 4; Step(true, heldX, heldY); }
            internal void Release() { Step(false, heldX, heldY); }
            internal void Wheel(int value) { Step(false, Popup.Body.X + 4, Popup.Body.Y + 4, value); }
        }
    }
}
