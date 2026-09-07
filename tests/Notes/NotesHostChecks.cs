using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Persistence;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Terraria
{
    internal static class NotesHostChecks
    {
        internal static void Run(bool includeGraphics = true)
        {
            new Main(); Main.blockInput = Main.drawingPlayerChat = Main.editSign = Main.editChest = false;
            Main.CurrentInputTextTakerOverride = null; Main.UIScaleMatrix = Matrix.Identity;
            WithWorkspace(workspace =>
            {
                string id = workspace.Feature.Saved.Notes[0].Id;
                var clipboard = new Clipboard(); var ime = new Ime(); var input = new NotesInput(workspace, clipboard, ime);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, true, 3)); input.PrepareEditor();
                ime.Preview = "ni"; Frame(input, workspace, "", Keys.N);
                Frame(input, workspace, "\r", Keys.Enter);
                Check(workspace.Editor != null && !workspace.Feature.Busy, "IME Enter never submits title");
                ime.Preview = ""; Frame(input, workspace, "你好\r", Keys.Enter);
                Check(workspace.Editor.Text == "one你好", "confirmed characters go to draft only");
                Frame(input, workspace, "\r", Keys.Enter);
                Check(workspace.Editor != null && !workspace.Feature.Busy, "held confirmation Enter tail stays IME-owned");
                Frame(input, workspace, ""); Frame(input, workspace, "\r", Keys.Enter); Drain(workspace);
                Check(workspace.Editor == null && workspace.Feature.Saved.Find(id).Title == "one你好", "next independent title Enter commits");
                input.Release(false);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false, 0)); input.PrepareEditor();
                Frame(input, workspace, "[i:123]中文"); Frame(input, workspace, "\r", Keys.Enter);
                Check(workspace.Editor.Text.Contains("\n"), "body Enter is newline"); Frame(input, workspace, "");
                string before = workspace.Editor.Text;
                Frame(input, workspace, "", Keys.LeftControl, Keys.X);
                Check(workspace.Editor.Text == before && input.Error != null, "failed cut keeps entire draft");
                Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.Z);
                Check(workspace.Editor.Text == before, "no destructive Ctrl Z");
                clipboard.Available = true; Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.X);
                Check(workspace.Editor.Text == "" && clipboard.Text == before, "successful cut copies entire draft before clearing");
                clipboard.Text = "👩🏽‍💻中"; Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.V);
                Check(workspace.Editor.Text == "👩🏽‍💻中", "paste into caret preserves complete clusters");
                ime.Final = "尾"; input.FinishComposition(false);
                Check(workspace.Editor.Text.EndsWith("尾", StringComparison.Ordinal), "IME finalize chars precede save snapshot");
                workspace.Request(new NotesAction(NotesActionKind.Save)); Drain(workspace);
                Check(workspace.Feature.Saved.Find(id).Body == "👩🏽‍💻中尾", "finalized text persisted");
                Frame(input, workspace, "new"); Main.drawingPlayerChat = true;
                input.BeforeSample(true); Check(!input.Owned && Main.drawingPlayerChat, "existing chat wins without being closed");
                Main.drawingPlayerChat = false;
                Check(!Main.blockInput, "chat handoff restores our blockInput lease for later vanilla input");
                input.Release(true); workspace.CancelEdit();
                Main.CurrentInputTextTakerOverride = null; GameInput.PlayerInput.WritingText = false;
            });
            Console.WriteLine("PASS: Notes native-queue arbitration with synthetic IME and clipboard failure; actual Windows IME remains pending.");
            WithWorkspace(workspace =>
            {
                Note note = workspace.Feature.Saved.Notes[0].Pin(100, 100);
                var pins = new NotesPins(workspace, null, action => { return false; });
                var pin = new NotesPin { Note = note, Rect = new F5Rect(100, 100, NotesPins.Width, NotesPins.Height),
                    Layout = new NotesTextLayout(note.Body, 200, s => 1) };
                ((List<NotesPin>)pins.Pins).Add(pin);
                pins.Pointer(110, 85, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(150, 125, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(150, 125, false, false, 0, true, true, false, 1920, 1080);
                Check(pin.Rect.X == 100 && pin.Rect.Y == 100, "rejected drop immediately returns to trusted position without another Prepare");
            });
            if (!includeGraphics) return;
            using (var graphics = new F5FixtureGraphics())
            using (var renderer = new NotesRenderer())
            {
                Check(renderer.Refresh(), "real hidden XNA resources ready");
                WithWorkspace(workspace => CheckCardsAndPins(workspace, renderer, graphics));
            }
            Console.WriteLine("PASS: Notes text/IME arbitration, clipboard failure, cards, pin targeting and real XNA state/pixels (synthetic IME/font, no real clipboard).");
        }
        private static void Frame(NotesInput input, NotesWorkspace workspace, string text, params Keys[] keys)
        {
            input.BeforeSample(true); GameInput.PlayerInput.WritingText = false; Main.keyState = new KeyboardState(keys); Main.NotesText(text);
            var editor = workspace.Editor;
            input.AfterSample(true, editor == null ? null : new NotesTextLayout(editor.Text, 10, s => 1));
            if (input.Owned) Check(Main.keyState.GetPressedKeys().Length == 0 && GameInput.PlayerInput.WritingText, "consumed raw keys not replayed");
        }
        private static void CheckCardsAndPins(NotesWorkspace workspace, NotesRenderer renderer, F5FixtureGraphics graphics)
        {
            var input = new NotesInput(workspace, new Clipboard(), new Ime());
            var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
            var state = new F5Interaction { Ready = true };
            state.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true, PageWheelHandled = false }); state.Navigate(4);
            using (var chrome = new F5Renderer())
            { chrome.RefreshResources(); chrome.Prepare(state, 1920, 1080, 1); }
            cards.Prepare(state, "双击编辑");
            NotesCard first = cards.Cards[0], second = cards.Cards[1];
            Click(cards, state, first.Body, true); Click(cards, state, first.Body, false);
            Check(workspace.Editor == null, "single click doesn't edit");
            Click(cards, state, first.Body, true); Click(cards, state, first.Body, false);
            Check(workspace.Editor != null && workspace.EditingId == first.Note.Id, "double click selects body");
            workspace.Editor.Insert(" draft");
            Click(cards, state, second.Title, true); Click(cards, state, second.Title, false);
            Check(workspace.EditingId == first.Note.Id && workspace.Editor.Dirty && !workspace.Feature.Busy, "single different field neither switches nor saves");
            workspace.CancelEdit();
            workspace.Request(new NotesAction(NotesActionKind.Pin, first.Note.Id, x: 100, y: 100)); Drain(workspace);
            workspace.Request(new NotesAction(NotesActionKind.Pin, second.Note.Id, x: 100, y: 100)); Drain(workspace);
            var pins = new NotesPins(workspace, renderer, action => workspace.Request(action)); pins.Prepare(1920, 1080);
            pins.Pointer(120, 120, false, false, -120, true, true, false, 1920, 1080);
            Check(pins.ConsumeWheel && pins.Pins[0].Scroll == 0 && pins.Pins[1].Scroll > 0, "overlap wheel affects exactly top pin");
            pins.Pointer(120, 120, true, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(200, 200, true, false, 0, true, true, false, 1920, 1080);
            Check(pins.Pins[1].Rect.X == 100, "body does not start drag");
            pins.Pointer(200, 200, false, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(110, 85, true, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(150, 125, true, false, 0, true, true, false, 1920, 1080);
            pins.Suspend(); Drain(workspace); pins.Prepare(1920, 1080);
            Check(workspace.Feature.Saved.Find(second.Note.Id).X == 140, "interrupt persists last valid drag sample");
            pins.Pointer(120, 120, false, false, -120, false, true, false, 1920, 1080);
            Check(!pins.OwnsPointer && !pins.ConsumeWheel, "hidden pins do not own pointer/wheel");
            using (var target = new RenderTarget2D(graphics.Device, 700, 500))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                Rectangle scissor = graphics.Device.ScissorRectangle;
                renderer.Pass(Matrix.Identity, null, pins.Draw);
                Check(graphics.Device.ScissorRectangle == scissor, "notes restores outer scissor");
                Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(650, 450, 10, 10), Color.Red);
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[700 * 500]; target.GetData(pixels);
                Check(pixels[460 * 700 + 380].A == 0, "transparent pin background remains transparent");
                Check(pixels[455 * 700 + 655].R == 255, "original SpriteBatch remains usable after notes");
                bool ink = false; for (int y = 148; y < 175; y++) for (int x = 148; x < 210; x++) ink |= pixels[y * 700 + x].A != 0;
                Check(ink, "transparent background does not hide text");
            }
        }
        private static void Click(NotesCards cards, F5Interaction state, F5Rect rect, bool down)
        {
            state.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1,
                X = state.X + state.Layout.Viewport.X + rect.X + 5, Y = state.Y + state.Layout.Viewport.Y + rect.Y + 5 - state.Scroll, Left = down });
            cards.Pointer(state, down, !down);
        }
        private static void WithWorkspace(Action<NotesWorkspace> action)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-notes-host-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "notes.json"); var codec = new NotebookCodec();
            File.WriteAllBytes(path, codec.Encode(Notebook.Empty.Add(Note.Create().WithText(true, "one")).Add(Note.Create().WithText(false, new string('长', 500)))));
            using (var worker = new DocumentWorker<Notebook>(new AtomicFileDocument(path, Notebook.MaximumBytes, true), codec.Decode, codec.Encode, Notebook.Empty))
            {
                var workspace = new NotesWorkspace(new NotesFeature(worker));
                Check(SpinWait.SpinUntil(() => { workspace.Poll(); return workspace.Feature.Loaded; }, 5000), "load deadline"); action(workspace);
            }
            Directory.Delete(root, true);
        }
        private static void Drain(NotesWorkspace workspace)
        { Check(SpinWait.SpinUntil(() => { workspace.Poll(); return !workspace.Feature.Busy; }, 5000), "save deadline"); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Notes: " + message); }
        private sealed class Clipboard : INotesClipboard
        {
            internal bool Available; internal string Text;
            public bool TryCopy(string text) { if (!Available) return false; Text = text; return true; }
            public bool TryPaste(out string text) { text = Text; return Available; }
        }
        private sealed class Ime : INotesIme
        {
            internal string Preview = "", Final; private bool enabled;
            public string Composition { get { return Preview; } }
            public bool Candidates { get { return Preview.Length != 0; } }
            public void Toggle(bool value) { if (enabled && !value && Final != null) { Main.NotesText(Final); Final = null; } enabled = value; }
        }
    }
}
