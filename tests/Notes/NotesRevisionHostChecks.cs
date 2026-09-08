using System;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class NotesRevisionHostChecks
    {
        internal static void Run(NotesRenderer renderer)
        {
            CheckFooterReading(renderer);
            CheckSaveButton(renderer);
            Note a = Note.Create().WithText(false, "甲乙丙丁\n戊己庚辛\n" + new string('长', 400)).Pin(100, 100);
            Note b = Note.Create().WithText(false, "另一张\n" + new string('文', 400)).Pin(100, 100);
            NotesHostChecks.WithWorkspace(workspace =>
            {
                var input = new NotesInput(workspace, new Clipboard(), new Ime());
                var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                var shell = new F5Interaction { Ready = true };
                shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                using (var chrome = new F5Renderer()) { chrome.RefreshResources(); chrome.Prepare(shell, 1920, 1080, 1); }
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, a.Id, false, 0));
                Prepare(cards, shell, renderer);
                F5Rect body = cards.Cards[0].Body;
                Pointer(cards, shell, body.X + 1, body.Y + 3, true, true, false);
                Pointer(cards, shell, body.X + 25, body.Y + 3, true, false, false);
                Check(workspace.Editor.SelectedText == "甲乙丙", "pointer selection uses measured glyph boundaries");
                Pointer(cards, shell, body.Right + 500, body.Bottom + 500, false, false, true);
                Check(!cards.Selecting && workspace.Editor.SelectedText == "甲乙丙" && !workspace.Feature.Busy && !workspace.Editor.Dirty,
                    "outside release keeps selection without saving, editing or activating another target");
                workspace.Editor.Insert("替换"); Prepare(cards, shell, renderer);
                Check(workspace.Editor.Text.StartsWith("替换丁\n", StringComparison.Ordinal), "drag-selected source range is replacement target");
                Pointer(cards, shell, body.X + 16, body.Y + renderer.LineHeight(0.76f) + 3, true, true, false);
                Pointer(cards, shell, body.X + 8, body.Y + 3, true, false, false);
                Pointer(cards, shell, body.X + 8, body.Y + 3, false, false, true);
                Check(workspace.Editor.SelectedText == "换丁\n戊己", "reverse cross-line drag copies the original newline once");
                Pointer(cards, shell, body.X + 1, body.Y + 3, true, true, false);
                float oldScroll = cards.Cards[0].BodyScroll;
                Pointer(cards, shell, body.X + 16, body.Bottom + 12, true, false, false);
                Check(cards.Cards[0].BodyScroll > oldScroll && cards.Cards[0].BodyScroll <= oldScroll + 12,
                    "selection edge scrolling is positive and bounded per sample");
                float edgeScroll = cards.Cards[0].BodyScroll; int edgeCaret = workspace.Editor.Caret;
                Pointer(cards, shell, body.Right + 100, body.Bottom + 100, true, false, false);
                Check(cards.Cards[0].BodyScroll == edgeScroll && workspace.Editor.Caret == edgeCaret, "far outside drag cannot keep scrolling or selecting");
                Pointer(cards, shell, body.Right + 100, body.Bottom + 100, false, false, true);
                var save = Find(cards, "save");
                Pointer(cards, shell, save.Rect.X + 2, save.Rect.Y + 2, true, true, false);
                workspace.CancelEdit(); Prepare(cards, shell, renderer);
                Pointer(cards, shell, save.Rect.X + 2, save.Rect.Y + 2, false, false, true);
                Check(workspace.Editor == null && !workspace.Feature.Busy && Find(cards, "save") == null, "vanished button cannot transfer its release to another action");
                workspace.RequestDelete(a.Id); workspace.Request(new NotesAction(NotesActionKind.BeginEdit, b.Id, false, 0)); Prepare(cards, shell, renderer);
                workspace.CancelDelete(); Check(cards.ActionStateChanged, "cancel deletion invalidates action snapshot even when editing feedback stays identical");
                Prepare(cards, shell, renderer); Check(Find(cards, "cancel-delete") == null && !cards.ActionStateChanged, "cancel deletion disappears while editor remains usable");
                workspace.CancelEdit();
                var pins = new NotesPins(workspace, renderer, action => workspace.Request(action)); Prepare(pins, renderer);
                NotesPin top = pins.Pins[1]; float y = top.Body.Y + 5;
                pins.Pointer(120, y, false, false, 120, true, true, false, 1920, 1080, true, false); Prepare(pins, renderer);
                Check(workspace.Feature.ReadingFor(b.Id).Width == 320 && workspace.Feature.ReadingFor(a.Id).Width == 280 && pins.ConsumeWheel, "Shift wheel adjusts exactly top pin");
                float width = top.Rect.Width, toolbar = top.Drag.Height;
                pins.Pointer(120, y, false, false, -120, true, true, false, 1920, 1080, false, true); Prepare(pins, renderer);
                Check(top.Scale == 1.1f && top.Rect.Width == width && top.Drag.Height == toolbar, "Ctrl wheel changes only body font, controls remain readable");
                pins.Pointer(120, y, false, false, 120, true, true, false, 1920, 1080, true, true);
                Check(workspace.Feature.ReadingFor(b.Id).Width == 320 && workspace.Feature.ReadingFor(b.Id).FontPercent == 110 && pins.ConsumeWheel, "both modifiers consume without applying either adjustment");
                pins.Pointer(120, y, false, false, 120, true, true, true, 1920, 1080, true, false);
                Check(!pins.OwnsPointer && !pins.ConsumeWheel && workspace.Feature.ReadingFor(b.Id).Width == 320, "higher owner prevents pin input");
                pins.Pointer(top.Rect.X + 8, top.Rect.Y - 1, false, false, 120, true, true, false, 1920, 1080, true, false);
                Check(!pins.OwnsPointer && !pins.ConsumeWheel, "no enlarged invisible toolbar strip");
                Check(top.Drag.Height >= 36 && top.Close.Right <= top.Rect.Right && top.Close.Bottom < top.Body.Y, "toolbar pixels and hits fit actual geometry");
                var before = workspace.Feature.ReadingFor(b.Id);
                pins.Prepare(100, 80); Check(!pins.HasPins && workspace.Feature.ReadingFor(b.Id).Same(before), "tiny screen hides unusable geometry without changing preference");
            }, new Notebook(new[] { a, b }));
            Console.WriteLine("PASS: Notes action states, drag selection, reading modifiers, priority and bounded geometry.");
        }
        internal static void CheckFooterReading(NotesRenderer renderer)
        {
            Note note = Note.Create().WithText(false, "首行\n" + new string('文', 400) + "\n末行").Pin(100, 100);
            NotesHostChecks.WithWorkspace(workspace =>
            {
                var pins = new NotesPins(workspace, renderer, action => workspace.Request(action)); Prepare(pins, renderer);
                NotesPin pin = pins.Pins[0];
                Check(pin.Body.Bottom <= pin.Rect.Bottom - renderer.ControlHeight - 8,
                    "footer has its own space outside the scrollable body, including an error message");
                Check(pin.Footer.Width >= renderer.ButtonWidth("未保存；F5 查看原因") && pin.Footer.Bottom <= pin.Rect.Bottom - 8,
                    "footer error label fits inside the actual pin at the current font");
                pins.Pointer(pin.Body.X + 4, pin.Body.Y + 4, false, false, -12000, true, true, false, 1920, 1080);
                Check(pin.Layout.Complete && Math.Abs(pin.Scroll - (pin.Layout.Lines.Count * pin.LineHeight - pin.Body.Height)) < 0.01f,
                    "scroll bottom uses the body area left after reserving the footer");
                Check(pin.Body.Y + pin.Layout.Lines.Count * pin.LineHeight - pin.Scroll <= pin.Body.Bottom + 0.01f,
                    "the final complete line remains above the reserved footer");
                F5Rect body = pin.Body; float scroll = pin.Scroll; var layout = pin.Layout;
                foreach (F5Rect region in new[] { pin.Drag, pin.Less, pin.More, pin.Close, pin.Body, new F5Rect(0, 0, 1, 1) })
                {
                    pins.Pointer(region.X + 1, region.Y + 1, false, false, 0, true, true, false, 1920, 1080); Prepare(pins, renderer);
                    Check(pin.Body.Equals(body) && pin.Scroll == scroll && ReferenceEquals(pin.Layout, layout),
                        "hover changes neither reading geometry nor scroll nor body layout");
                }
                workspace.Feature.AdjustReading(note.Id, true, -1); workspace.Feature.AdjustReading(note.Id, false, 6); Prepare(pins, renderer);
                Check(pin.Body.Height >= pin.LineHeight && pin.Body.Bottom <= pin.Rect.Bottom - renderer.ControlHeight - 8,
                    "small reading area and largest body font still leave a complete line and separate footer");
                var reading = workspace.Feature.ReadingFor(note.Id);
                pins.Prepare(100, 80); Check(!pins.HasPins && workspace.Feature.ReadingFor(note.Id).Same(reading),
                    "unusable viewport hides a pin without rewriting reading preferences");
                Prepare(pins, renderer); Check(pins.HasPins && workspace.Feature.ReadingFor(note.Id).Same(reading), "viewport recovery restores the same reading preference");
            }, new Notebook(new[] { note }));
        }
        private static void CheckSaveButton(NotesRenderer renderer)
        {
            foreach (bool title in new[] { false, true })
            {
                Note note = Note.Create().WithText(false, "原文");
                NotesHostChecks.WithWorkspace(workspace =>
                {
                    var input = new NotesInput(workspace, new Clipboard(), new Ime());
                    var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                    var shell = new F5Interaction { Ready = true };
                    shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                    using (var chrome = new F5Renderer()) { chrome.RefreshResources(); chrome.Prepare(shell, 1920, 1080, 1); }
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, note.Id, title, 0)); workspace.Editor.Insert("已保存");
                    string expected = workspace.Editor.Text; Prepare(cards, shell, renderer); var save = Find(cards, "save");
                    Pointer(cards, shell, save.Rect.X + 2, save.Rect.Y + 2, true, true, false);
                    Pointer(cards, shell, save.Rect.X + 2, save.Rect.Y + 2, false, false, true);
                    Check(workspace.Editor != null && workspace.Feature.Busy, "save click waits for a reliable completion before ending editing");
                    Check(SpinWait.SpinUntil(() => { workspace.Poll(); return !workspace.Feature.Busy; }, 5000), "save button completion deadline");
                    Prepare(cards, shell, renderer);
                    Check(workspace.Editor == null && workspace.EditingId == null && cards.EditingLayout == null && Find(cards, "save") == null && Find(cards, "cancel") == null,
                        "successful save button returns to preview without an extra blank click");
                    Note actual = workspace.Feature.Saved.Find(note.Id);
                    Check((title ? actual.Title : actual.Body) == expected, "save button preview is the acknowledged content");
                }, new Notebook(new[] { note }));
            }
        }
        private static NotesControl Find(NotesCards cards, string key)
        { foreach (var value in cards.Controls) if (value.Key == key) return value; return null; }
        private static void Prepare(NotesCards cards, F5Interaction shell, NotesRenderer renderer)
        { for (int i = 0; i < 8; i++) { renderer.BeginLayoutFrame(); cards.Prepare(shell, "选区测试"); } }
        private static void Prepare(NotesPins pins, NotesRenderer renderer)
        { for (int i = 0; i < 8; i++) { renderer.BeginLayoutFrame(); pins.Prepare(1920, 1080); } }
        private static void Pointer(NotesCards cards, F5Interaction shell, float x, float y, bool left, bool pressed, bool released)
        {
            shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, Left = left,
                X = shell.X + shell.Layout.Viewport.X + x, Y = shell.Y + shell.Layout.Viewport.Y + y - shell.Scroll });
            cards.Pointer(shell, pressed, released, left);
        }
        private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException("Notes revision: " + message); }
        private sealed class Clipboard : INotesClipboard
        { public bool TryCopy(string text) { return true; } public bool TryPaste(out string text) { text = ""; return true; } }
        private sealed class Ime : INotesIme
        {
            public string Composition { get { return ""; } }
            public bool Candidates { get { return false; } }
            public void Toggle(bool enabled) { }
        }
    }
}
