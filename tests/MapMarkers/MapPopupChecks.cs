using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
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
                Require(d.Popup.Panel.Width <= 480, "marker window is narrower than the main feature content");
                Require(owner.Workspace.Editor != null, "double click starts real name editor");
                d.Prepare(); Require(d.Popup.Buttons[d.Popup.Commands.IndexOf(10)].Text == "", "editor uses its own aligned text viewport, not a centered button label");
                owner.Workspace.Editor.SelectAll(); Require(owner.Workspace.Editor.Insert("12345678901234567890"), "twenty half-width elements accepted"); Require(!owner.Workspace.Editor.Insert("1") && owner.Workspace.Editor.Text == "12345678901234567890", "twenty-first is atomic rejection");
                owner.Workspace.Editor.SelectAll(); Require(owner.Workspace.Editor.Insert("一二三四五六七八九十"), "ten Chinese elements accepted"); Require(!owner.Workspace.Editor.Insert("一") && owner.Workspace.Editor.Text == "一二三四五六七八九十", "Chinese overflow keeps original draft");
                owner.Workspace.Editor.SelectAll(); Require(owner.Workspace.Editor.Insert("1234567890"), "replace selection keeps whole draft transaction");
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
                d.Height = 600; d.Prepare();
                Require(d.Popup.Panel.Width <= 320 && d.Popup.Buttons[d.Popup.Commands.IndexOf(0)].Rect.Y < d.Popup.Body.Y - d.Popup.Panel.Y, "narrow details keep close in the header");
                Require(!d.Popup.Text.Any(t => t.Text.StartsWith("开启后")), "dynamic explanation does not occupy a permanent body row");
                Require(d.Popup.Sections.Count == 1, "three control rows share one frame");
                var scanAction = d.Popup.Buttons[d.Popup.Commands.IndexOf(23)].Rect;
                var slow = d.Popup.Buttons[d.Popup.Commands.IndexOf(20)].Rect;
                var fast = d.Popup.Buttons[d.Popup.Commands.IndexOf(21)].Rect;
                var dynamicAction = d.Popup.Buttons[d.Popup.Commands.IndexOf(24)].Rect;
                Require(slow.Y - scanAction.Y == dynamicAction.Y - slow.Y && scanAction.X == slow.X && slow.X == dynamicAction.X && fast.Right == dynamicAction.Right && slow.Width == fast.Width, "three rows have equal spacing and aligned control columns");
                foreach (int c in new[] { 20, 21, 22, 23, 24 })
                {
                    var r = d.Popup.Buttons[d.Popup.Commands.IndexOf(c)].Rect;
                    Require(d.Popup.Sections.Any(s => r.X >= s.X + 6 && r.Y >= s.Y + 6 && r.Right <= s.Right - 6 && r.Bottom <= s.Bottom - 6), "group frames leave visible padding around every action");
                }
                d.Hover(24); Require(d.Popup.Hint.Visible && d.Popup.Hint.Lines.Any(t => t.Text.Contains("重新统计")), "physical hover reveals dynamic-update help");
                int hintBuilds = d.Popup.Hint.BuildCount; for (int n = 0; n < 2000; n++) d.Prepare(); Require(d.Popup.Hint.BuildCount == hintBuilds, "stable switch hover reuses the existing hint layout");
                d.Step(false, 0, 0); Require(!d.Popup.Hint.Visible, "moving off the switch hides the help");
                var valueLine = d.Popup.Text.First(t => t.Text == "统计中…"); var progressLine = d.Popup.Text.First(t => t.Text.StartsWith("已扫描："));
                Require(Math.Abs(valueLine.Rect.Y - progressLine.Rect.Y) < 10 && valueLine.Rect.Right <= progressLine.Rect.X, "scan status and progress share a nonoverlapping row");
                Require(valueLine.TextScale > d.Popup.Buttons[0].TextScale, "main statistical value is larger than controls");
                Require(d.Popup.Commands.Count(c => c == 24 || c == 25) == 1, "automatic updating has one explicit state/action control");
                int generation = d.Popup.Generation; owner.ExplorationText = "已揭示 12.34%"; owner.ScanText = "已扫描：34.00%"; d.Prepare(); Require(d.Popup.Generation == generation, "text-only progress preserves held actions");
                long layouts = d.Popup.LayoutBuilds; for (int i = 0; i < 2000; i++) { owner.ScanText = "已扫描：" + (i / 100d).ToString("0.00") + "%"; d.Prepare(); } Require(d.Popup.LayoutBuilds == layouts, "2000 detail changes never rebuild controls");
                owner.ScanActive = false; owner.ScanText = "上次结果"; d.Prepare(); Require(!d.Popup.Commands.Contains(22), "completed scan removes pause action");
                Require(d.Popup.Buttons[d.Popup.Commands.IndexOf(23)].Rect.Width == dynamicAction.Width, "single recount action fills the shared control column after scan completion");
                d.Click(24); Require(owner.DynamicEnabled && d.Popup.Commands.Contains(25) && !d.Popup.Commands.Contains(24), "state/action toggle sends enable then offers disable");
                d.Click(25); Require(!owner.DynamicEnabled, "toggle sends disable");
                var elements = new List<F5Element>(); float rowY = 0; MapControls.AddRows(elements, (text, scale) => new F5Size(text.Length * 12 * scale, 30 * scale), ref rowY);
                var summary = elements.Single(e => e.Command == F5Command.ExplorationValue); var details = elements.Single(e => e.Command == F5Command.ExplorationDetails);
                Require(summary.Rect.Y <= details.Rect.Y && summary.Rect.Bottom >= details.Rect.Bottom && summary.Rect.Right < details.Rect.X, "menu summary stays inside the feature row beside details");
                d.Click(0); for (int i = 2; i <= 11; i++) { long op = owner.Markers.Create(new MarkerRecord(i.ToString("x32"), i, i, 48, "同名")); Until(owner.Markers.Poll, () => owner.Markers.LastOperation == op); }
                d.Height = 800; d.Popup.Open(false); d.Prepare(); Require(d.Popup.Icons.Count == 10, "normal first page shows all ten rows");
                d.Height = 220; d.Prepare(); var close = d.Popup.Buttons[d.Popup.Commands.IndexOf(0)].Rect; var seen = new HashSet<string>();
                for (int n = 0; n < 12; n++) { foreach (var id in d.Popup.Targets) if (id != null) seen.Add(id); d.Wheel(-120); Require(d.Popup.Buttons[d.Popup.Commands.IndexOf(0)].Rect.Equals(close), "scroll keeps close in a fixed footer"); }
                Require(seen.Count == 10, "every stable ID on a full page is reachable in a short viewport");
                d.Height = 800; d.Popup.Suspend(); d.Popup.Open(false); d.Prepare(); d.Click(2); Require(d.Popup.Icons.Count == 1 && d.Popup.Text.Any(t => t.Text == "第 2 / 2 页"), "second page states its identity explicitly"); d.Click(12); d.Click(12); Until(() => { owner.Markers.Poll(); owner.Workspace.Poll(); }, () => owner.Markers.Saved.Records.Count == 10); d.Prepare();
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
            public bool ScanActive { get; set; } = true;
            public string ExplorationText { get; set; } = "统计中…";
            public string ScanText { get; set; } = "已扫描：0.00%";
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
            internal void Hover(int command) { var r = Popup.Buttons[Popup.Commands.IndexOf(command)].Rect.Offset(Popup.Panel.X, Popup.Panel.Y); Step(false, r.X + 4, r.Y + 4); }
            internal void Wheel(int delta) { Popup.Process(true, Popup.Body.X + 2, Popup.Body.Y + 2, true, delta); Prepare(); }
        }
    }
}
