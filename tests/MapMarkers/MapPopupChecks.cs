using System;
using System.IO;
using System.Threading;
using JueMingR.Features.MapMarkers;
using JueMingR.Infrastructure.Storage;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class MapPopupChecks
    {
        internal static void Run()
        {
            new Main(); Main.blockInput = Main.drawingPlayerChat = Main.editSign = Main.editChest = false; Main.CurrentInputTextTakerOverride = null;
            using (var owner = new Owner())
            {
                var d = new Driver(owner); d.Popup.Open(false); d.Prepare(); Require(d.Popup.Panel.Height < 260, "one row uses a compact management window"); d.Click(10); d.Click(10);
                Require(owner.Workspace.Editor != null, "double click starts real name editor");
                d.Prepare(); Require(d.Popup.Buttons[d.Popup.Commands.IndexOf(10)].Text == "", "editor uses its own aligned text viewport, not a centered button label");
                owner.Workspace.Editor.SelectAll(); Require(owner.Workspace.Editor.Insert("1234567890"), "ten elements accepted"); Require(!owner.Workspace.Editor.Insert("1") && owner.Workspace.Editor.Text == "1234567890", "eleventh is atomic rejection");
                owner.Workspace.Editor.MoveTo(0, false); d.Ime.Composition = "ni"; d.Step(false, 0, 0); Require(d.Popup.EditView.Text.StartsWith("ni1") && d.Popup.EditView.Caret > 0, "IME composition is drawn at caret with matching anchor"); d.Ime.Composition = ""; d.Step(false, 0, 0);
                d.Step(false, 0, 0, "\x1b", Keys.Escape);
                Require(owner.Workspace.Editor == null && d.Popup.Visible, "one Esc cancels draft only, not its management window");
                d.Step(false, 0, 0); d.Click(10); d.Click(10); Require(owner.Workspace.Editor != null, "re-edit");
                d.Ime.Composition = "ni"; d.Step(false, 0, 0, "\r", Keys.Enter); Require(owner.Workspace.Editor != null && !owner.Markers.Busy, "composition Enter does not save");
                d.Ime.Composition = ""; d.Step(false, 0, 0, "你好\r", Keys.Enter); d.Step(false, 0, 0); d.Step(false, 0, 0, "\r", Keys.Enter);
                Until(() => { owner.Markers.Poll(); owner.Workspace.Poll(); }, () => owner.Workspace.Editor == null);
                Require(owner.Markers.Saved.Records[0].Name == "你好", "independent next Enter persists committed whole name");
                d.Prepare(); d.Press(12); owner.Workspace.ConfirmDelete(owner.Markers.Saved.Records[0].Id); d.Prepare(); d.Release();
                Require(owner.Markers.Saved.Records.Count == 1, "delete label transition must invalidate previous unconfirmed press");
                d.Height = 220; d.Prepare();
                foreach (var button in d.Popup.Buttons) Require(button.Rect.Bottom <= d.Popup.Panel.Height && button.Rect.Right <= d.Popup.Panel.Width, "small view keeps controls in panel");
                d.Click(0); Require(!d.Popup.Visible, "close stays reachable"); d.Popup.Open(true); d.Prepare();
                int generation = d.Popup.Generation; owner.ExplorationText = "当前揭示 12.34%"; owner.ScanText = "完整扫描 34.0%"; d.Prepare(); Require(d.Popup.Generation == generation, "text-only progress preserves held actions");
                long layouts = d.Popup.LayoutBuilds; for (int i = 0; i < 2000; i++) { owner.ScanText = "完整扫描 " + i + "%"; d.Prepare(); } Require(d.Popup.LayoutBuilds == layouts, "2000 detail changes never rebuild controls");
                d.Click(0); for (int i = 2; i <= 11; i++) { long op = owner.Markers.Create(new MarkerRecord(i.ToString("x32"), i, i, 48, "同名")); Until(owner.Markers.Poll, () => owner.Markers.LastOperation == op); }
                d.Height = 800; d.Popup.Open(false); d.Prepare(); d.Click(2); d.Click(12); d.Click(12); Until(() => { owner.Markers.Poll(); owner.Workspace.Poll(); }, () => owner.Markers.Saved.Records.Count == 10); d.Prepare();
                Require(d.Popup.Icons.Count == 10 && d.Popup.Panel.Height > 400, "deleting second-page tail clamps page before deriving first-page geometry");
            }
            Console.WriteLine("PASS: map management physical clicks, name/IME transaction, confirmation identity and fixed progress geometry.");
        }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static void Until(Action pump, Func<bool> done) { if (!SpinWait.SpinUntil(() => { pump(); return done(); }, 5000)) throw new TimeoutException("map popup receipt"); }
        private sealed class Ime : INotesIme
        { public string Composition { get; set; } = ""; public bool Candidates { get { return false; } } public void Toggle(bool enabled) { } }
        private sealed class Owner : IMapControls, IDisposable
        {
            public long Session { get { return 1; } }
            public bool ControlsEnabled { get { return true; } }
            public bool MarkersEnabled { get; private set; }
            public bool DynamicEnabled { get; private set; }
            public bool FastScan { get; set; }
            public bool ScanPaused { get; private set; }
            public bool ScanActive { get { return true; } }
            public string ExplorationText { get; set; } = "统计中…";
            public string ScanText { get; set; } = "完整扫描 0.0%";
            public string StatusMessage { get { return null; } }
            public MarkerLibrary Markers { get; }
            public MarkerWorkspace Workspace { get; }
            internal Owner()
            {
                string root = Path.Combine(Path.GetTempPath(), "JueMingR-map-popup-" + Guid.NewGuid().ToString("N"));
                Markers = new MarkerLibrary(pair => new AtomicFileDocument(Path.Combine(root, pair + ".json"), MarkerCodec.MaximumBytes, true)); Workspace = new MarkerWorkspace(Markers);
                Markers.BeginSession(1, 128, 128); Markers.UsePair(new string('a', 64)); Until(Markers.Poll, () => Markers.Loaded);
                long id = Markers.Create(new MarkerRecord("00000000000000000000000000000001", 50.125, 60.875, 8, "原名")); Until(Markers.Poll, () => Markers.LastOperation == id); Workspace.Poll();
            }
            public bool SetMarkers(bool value) { MarkersEnabled = value; return true; }
            public bool SetDynamic(bool value) { DynamicEnabled = value; return true; }
            public void PauseScan(bool value) { ScanPaused = value; }
            public void Recount() { }
            public bool Locate(string id) { return true; }
            public void TakeFeedback(Action<string> display) { }
            public void Dispose() { Require(Markers.Stop(5000), "asset worker stopped"); }
        }
        private sealed class Driver
        {
            private readonly HostInputState input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
            internal readonly Ime Ime = new Ime();
            internal readonly MapManagementPopup Popup;
            private readonly object font = new object();
            internal float Height = 600;
            private float heldX, heldY;
            internal Driver(Owner owner) { Popup = new MapManagementPopup(owner, input, Ime); Step(false, 0, 0); }
            internal void Prepare() { Popup.Prepare(800, Height, font, (text, scale) => new F5Size(text.Length * 12 * scale, 30 * scale), 0); }
            internal void Step(bool left, float x, float y, string text = "", params Keys[] keys)
            {
                input.BeginUpdate(); Popup.BeforeInput(true);
                PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh();
                Main.keyCount = text.Length; for (int i = 0; i < text.Length; i++) { Main.keyInt[i] = text[i]; Main.keyString[i] = text[i].ToString(); }
                Popup.Process(true, x, y, Popup.Matches(800, Height, font, 0), 0); Prepare();
            }
            internal void Press(int command) { int i = Popup.Commands.IndexOf(command); Require(i >= 0, "command exists"); var rect = Popup.Buttons[i].Rect.Offset(Popup.Panel.X, Popup.Panel.Y); heldX = rect.X + 4; heldY = rect.Y + 4; Step(true, heldX, heldY); }
            internal void Release() { Step(false, heldX, heldY); }
            internal void Click(int command) { Press(command); Release(); }
        }
    }
}
