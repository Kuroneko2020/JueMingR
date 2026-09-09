using System;
using System.Collections.Generic;
using System.Reflection;
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
                clipboard.Text = "untouched";
                Frame(input, workspace, "", Keys.LeftControl, Keys.C);
                Check(clipboard.Text == "untouched" && input.Error == null, "no selection does not copy the whole field");
                Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.A);
                Check(workspace.Editor.SelectedText == before, "Ctrl A selects only active field");
                Frame(input, workspace, "");
                Frame(input, workspace, "", Keys.LeftControl, Keys.X);
                Check(workspace.Editor.Text == before && input.Error != null, "failed cut keeps entire draft");
                Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.Z);
                Check(workspace.Editor.Text == before, "no destructive Ctrl Z");
                clipboard.Available = true; Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.X);
                Check(workspace.Editor.Text == "" && clipboard.Text == before, "successful cut copies selected field before deleting");
                clipboard.Text = "👩🏽‍💻中"; Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftControl, Keys.V);
                Check(workspace.Editor.Text == "👩🏽‍💻中", "paste into caret preserves complete clusters");
                Frame(input, workspace, ""); Frame(input, workspace, "", Keys.LeftShift, Keys.Left);
                Check(workspace.Editor.SelectedText == "中", "Shift Left selects a whole character");
                ime.Preview = "ni"; Frame(input, workspace, "", Keys.N);
                Check(workspace.Editor.SelectedText == "中" && workspace.Editor.Text == "👩🏽‍💻中", "composition preview retains original selection");
                Frame(input, workspace, "\x1b", Keys.Escape); ime.Preview = ""; Frame(input, workspace, ""); Frame(input, workspace, "");
                Check(workspace.Editor != null && workspace.Editor.SelectedText == "中", "IME cancellation preserves selected original");
                ime.Preview = "hao"; Frame(input, workspace, ""); ime.Preview = ""; Frame(input, workspace, "好\r", Keys.Enter);
                Check(workspace.Editor.Text == "👩🏽‍💻好", "IME commit replaces selection once without body newline");
                workspace.Editor.MoveTo(workspace.Editor.Text.Length);
                ime.Final = "尾"; input.FinishComposition(false);
                Check(workspace.Editor.Text.EndsWith("尾", StringComparison.Ordinal), "IME finalize chars precede save snapshot");
                workspace.Request(new NotesAction(NotesActionKind.Save)); Drain(workspace);
                Check(workspace.Feature.Saved.Find(id).Body == "👩🏽‍💻好尾", "finalized text persisted");
                Frame(input, workspace, "new"); Main.drawingPlayerChat = true;
                input.BeforeSample(true); Check(!input.Owned && Main.drawingPlayerChat, "existing chat wins without being closed");
                Main.drawingPlayerChat = false;
                Check(!Main.blockInput, "chat handoff restores our blockInput lease for later vanilla input");
                input.Release(true); workspace.CancelEdit();
                Main.CurrentInputTextTakerOverride = null; GameInput.PlayerInput.WritingText = false;
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false, 0)); input.PrepareEditor();
                Frame(input, workspace, "\ud83d"); Frame(input, workspace, "\ude00");
                Check(workspace.Editor.Text.StartsWith("😀", StringComparison.Ordinal), "split WM_CHAR pair joins in same draft");
                input.Release(true); workspace.CancelEdit();
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false, 0)); input.PrepareEditor();
                Frame(input, workspace, "\ud83d");
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, workspace.Feature.Saved.Notes[1].Id, false, 0)); input.PrepareEditor();
                Frame(input, workspace, "A");
                Check(workspace.Editor.Text.StartsWith("A", StringComparison.Ordinal), "old half pair cannot poison another draft");
                input.Release(true); workspace.CancelEdit();
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false, 0)); input.PrepareEditor();
                input.BeforeSample(true); Main.keyState = new KeyboardState(Keys.End); Main.NotesText(""); input.AfterSample(true, null);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, workspace.Feature.Saved.Notes[1].Id, false, 0)); input.PrepareEditor();
                Frame(input, workspace, ""); Check(workspace.Editor.Caret == 0, "deferred navigation never transfers to same-numbered revision in another draft");
                input.Release(true); workspace.CancelEdit();
            });
            Console.WriteLine("PASS: Notes native-queue arbitration with synthetic IME and clipboard failure; actual Windows IME remains pending.");
            foreach (string pending in new[] { "composition", "surrogate" })
                WithWorkspace(workspace =>
                {
                    string id = workspace.Feature.Saved.Notes[0].Id;
                    var ime = new Ime(); var input = new NotesInput(workspace, new Clipboard(), ime);
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false, 0)); input.PrepareEditor(); Frame(input, workspace, "saved");
                    input.FinishComposition(false); workspace.Request(new NotesAction(NotesActionKind.FinishEdit));
                    NoteEditor editor = workspace.Editor; long revision = editor.Revision;
                    if (pending == "composition") { ime.Preview = "ni"; Frame(input, workspace, ""); }
                    else Frame(input, workspace, "\ud83d");
                    Check(editor.Revision == revision && input.HasComposition, "uncommitted input does not fabricate a text revision");
                    Drain(workspace);
                    Check(ReferenceEquals(workspace.Editor, editor) && workspace.Feature.Saved.Find(id).Body == "saved", "save completion retains the active uncommitted input owner");
                    if (pending == "composition") { ime.Preview = ""; Frame(input, workspace, "你"); }
                    else Frame(input, workspace, "\ude00");
                    Check(editor.Text == "saved" + (pending == "composition" ? "你" : "😀") && editor.Dirty, "late confirmed input remains visible and unsaved exactly once");
                    input.Release(true); workspace.CancelEdit();
                });
            WithWorkspace(workspace =>
            {
                string id = workspace.Feature.Saved.Notes[0].Id;
                var ime = new Ime(); var input = new NotesInput(workspace, new Clipboard(), ime);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, true, 0)); input.PrepareEditor();
                workspace.Editor.SelectAll(); Frame(input, workspace, " "); workspace.Editor.SelectAll();
                input.FinishComposition(false); workspace.Request(new NotesAction(NotesActionKind.FinishEdit));
                ime.Preview = "ni"; Frame(input, workspace, ""); Drain(workspace);
                Check(workspace.Feature.Saved.Find(id).Title == "新笔记" && workspace.Editor != null && workspace.Editor.Text == " " && workspace.Editor.SelectedText == " ",
                    "acknowledged title fallback cannot change the active IME replacement range");
                ime.Preview = ""; Frame(input, workspace, "你");
                Check(workspace.Editor.Text == "你" && workspace.Editor.Dirty, "new title composition replaces its original range exactly once");
                input.Release(true); workspace.CancelEdit();
            });
            WithWorkspace(workspace =>
            {
                string id = workspace.Feature.Saved.Notes[0].Id;
                var input = new NotesInput(workspace, new Clipboard(), new Ime());
                var presentation = new NotesPresentation(workspace);
                // Replace only external clipboard/IME adapters; exercise the actual
                // presentation save entry before any completion or next input sample.
                typeof(NotesPresentation).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(presentation, input);
                var request = typeof(NotesPresentation).GetMethod("Request", BindingFlags.Instance | BindingFlags.NonPublic);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, true, 3)); input.PrepareEditor(); Frame(input, workspace, "\ud83d");
                Check(!workspace.Editor.Dirty && input.HasComposition, "one pending high surrogate is not a saved text edit");
                Check(!(bool)request.Invoke(presentation, new object[] { new NotesAction(NotesActionKind.FinishEdit) }) && workspace.Editor != null && !workspace.Feature.Busy,
                    "save cannot synchronously discard an incomplete character");
                Frame(input, workspace, "\r", Keys.Enter);
                Check(workspace.Editor != null && !workspace.Feature.Busy, "title Enter also preserves an incomplete character");
                Frame(input, workspace, "\ude00");
                Check(workspace.Editor.Text == "one😀", "pending pair completes once after a deferred finish");
                input.Release(true); workspace.CancelEdit();
            });
            WithWorkspace(workspace =>
            {
                Note note = workspace.Feature.Saved.Notes[0].Pin(100, 100);
                var pins = new NotesPins(workspace, null, action => { return false; });
                var pin = new NotesPin { Note = note, Rect = new F5Rect(100, 100, NotesPins.Width, NotesPins.Height),
                    Layout = new NotesTextLayout(note.Body, 200, s => 1) };
                NotesPins.SetRect(pin, pin.Rect);
                ((List<NotesPin>)pins.Pins).Add(pin);
                pins.Pointer(110, 110, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(150, 150, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(150, 150, false, false, 0, true, true, false, 1920, 1080);
                Check(pin.Rect.X == 100 && pin.Rect.Y == 100, "rejected drop immediately returns to trusted position without another Prepare");
            });
            WithWorkspace(workspace =>
            {
                Note note = workspace.Feature.Saved.Notes[0].Pin(100, 100); int positions = 0, unpins = 0;
                var pins = new NotesPins(workspace, null, action => { if (action.Kind == NotesActionKind.Position) positions++; if (action.Kind == NotesActionKind.Unpin) unpins++; return true; });
                var pin = new NotesPin { Note = note, Rect = new F5Rect(100, 100, NotesPins.Width, NotesPins.Height), Layout = new NotesTextLayout(note.Body, 200, s => 1) };
                NotesPins.SetRect(pin, pin.Rect); ((List<NotesPin>)pins.Pins).Add(pin);
                pins.Pointer(110, 110, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(150, 150, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(0, 0, false, false, 0, false, false, false, 1920, 1080);
                pins.Suspend(true);
                Check(positions == 0 && pin.Rect.X == 100 && pin.Rect.Y == 100, "focus loss cancels pin projection without a position command");
                float x = pin.Close.X + 2, y = pin.Close.Y + 2;
                pins.Pointer(x, y, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(x, y, false, false, 0, true, true, false, 1920, 1080);
                Check(unpins == 0, "reactivating held pointer cannot unpin the old target");
                pins.Pointer(x, y, true, false, 0, true, true, false, 1920, 1080);
                pins.Pointer(x, y, false, false, 0, true, true, false, 1920, 1080);
                Check(unpins == 1, "new independent pin click still executes exactly once");
            });
            CheckLayoutScheduling();
            if (!includeGraphics) return;
            using (var graphics = new F5FixtureGraphics())
            using (var renderer = new NotesRenderer())
            {
                Check(renderer.Refresh(), "real hidden XNA resources ready");
                WithWorkspace(workspace => CheckCardsAndPins(workspace, renderer, graphics));
                CheckFooterPixels(renderer, graphics);
            }
            Console.WriteLine("PASS: Notes text/IME arbitration, clipboard failure, cards, pin targeting and real XNA state/pixels (synthetic IME/font, no real clipboard).");
        }
        private static void CheckLayoutScheduling()
        {
            // Texture-free metric fixture: real DynamicSpriteFont's CPU metric path,
            // with one synthetic glyph. This checks scheduling, not rendered pixels.
            var font = new ReLogic.Graphics.DynamicSpriteFont(0, 20, '?');
            Type pageType = typeof(ReLogic.Graphics.DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage", true);
            object page = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new object[] { null, new List<Rectangle> { new Rectangle(0, 0, 10, 20) }, new List<Rectangle> { new Rectangle(0, 0, 10, 20) },
                    new List<char> { '?' }, new List<Vector3> { new Vector3(0, 10, 0) } }, null);
            Array pages = Array.CreateInstance(pageType, 1); pages.SetValue(page, 0);
            typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(font, new object[] { pages });
            var asset = (ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "notes-cpu-metric" }, null);
            asset.GetType().GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(asset, new object[] { font, new MetricSource() });
            GameContent.FontAssets.MouseText = asset;
            var seed = new List<Note> { Note.Create().WithText(false, new string('中', Note.MaximumBodyUnits)) };
            for (int i = 0; i < 50; i++) seed.Add(Note.Create().WithText(false, new string('x', 240)));
            WithWorkspace(workspace =>
            {
                using (var renderer = new NotesRenderer())
                {
                    renderer.Refresh();
                    var input = new NotesInput(workspace, new Clipboard(), new Ime());
                    var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                    var state = new F5Interaction { Ready = true };
                    state.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); state.Navigate(4);
                    state.Layout.Ensure(1920, 1080, 1, 4, font, value => new F5Size(value.Length * 10, 20));
                    for (int i = 0; i < 600; i++) { renderer.BeginLayoutFrame(); cards.Prepare(state, "双击编辑"); }
                    Check(cards.Cards[0].BodyLayout.Complete, "visible long body completes despite many offscreen short cards");
                    var unchanged = cards.Cards[0].BodyLayout;
                    for (int i = 0; i < 10; i++) { renderer.BeginLayoutFrame(); cards.Prepare(state, "双击编辑"); }
                    Check(ReferenceEquals(unchanged, cards.Cards[0].BodyLayout) && !cards.PendingLayout, "unchanged layout stabilizes without offscreen rebuilding");
                }
            }, new Notebook(seed));
            using (var renderer = new NotesRenderer())
            {
                renderer.Refresh(); NotesRevisionHostChecks.Run(renderer);
                // A separate Notes-only metric boundary; this deliberately tall
                // resource is not claimed to pass the F5 navigation font gate.
                var tall = new ReLogic.Graphics.DynamicSpriteFont(0, 20, '?');
                object tallPage = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { null, new List<Rectangle> { new Rectangle(0, 0, 10, 32) }, new List<Rectangle> { new Rectangle(0, 0, 10, 32) },
                        new List<char> { '?' }, new List<Vector3> { new Vector3(0, 10, 0) } }, null);
                pages.SetValue(tallPage, 0);
                typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tall, new object[] { pages });
                var tallAsset = (ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "notes-tall-metric" }, null);
                tallAsset.GetType().GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tallAsset, new object[] { tall, new MetricSource() });
                GameContent.FontAssets.MouseText = tallAsset; renderer.Refresh();
                Check(renderer.LineHeight(1.2f) >= 32 * 1.2f + 4, "body rows contain actual tall glyphs and four-way border even when LineSpacing is smaller");
                NotesRevisionHostChecks.CheckFooterReading(renderer);
                NotesRevisionHostChecks.CheckCompactControls(renderer);
            }
            GameContent.FontAssets.MouseText = null;
            Console.WriteLine("PASS: Notes shared layout budget reaches visible long text and stabilizes (texture-free synthetic font metrics).");
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
            for (int i = 0; i < 10; i++) { renderer.BeginLayoutFrame(); cards.Prepare(state, "双击编辑"); }
            NotesCard first = cards.Cards[0], second = cards.Cards[1];
            var hitMethod = typeof(NotesCards).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Check((string)hitMethod.Invoke(cards, new object[] { 50f, 10f }) != "save", "browsing has no invisible save hit area");
            Click(cards, state, first.Body, true); Click(cards, state, first.Body, false);
            Check(workspace.Editor == null, "single click doesn't edit");
            Click(cards, state, first.Body, true); Click(cards, state, first.Body, false);
            Check(workspace.Editor != null && workspace.EditingId == first.Note.Id, "double click selects body");
            for (int i = 0; i < 3; i++) { renderer.BeginLayoutFrame(); cards.Prepare(state, "编辑正文"); }
            Check(!HasControl(cards, "save") && HasControl(cards, "cancel"), "clean edit exposes cancel editing only");
            workspace.Editor.Insert(" draft");
            renderer.BeginLayoutFrame(); cards.Prepare(state, "编辑正文");
            Check(HasControl(cards, "save"), "dirty edit exposes save");
            Click(cards, state, second.Title, true); Click(cards, state, second.Title, false);
            Check(workspace.EditingId == first.Note.Id && workspace.Editor.Dirty && !workspace.Feature.Busy, "single different field neither switches nor saves");
            workspace.CancelEdit();
            workspace.Request(new NotesAction(NotesActionKind.Pin, first.Note.Id, x: 100, y: 100)); Drain(workspace);
            workspace.Request(new NotesAction(NotesActionKind.Pin, second.Note.Id, x: 100, y: 100)); Drain(workspace);
            var pins = new NotesPins(workspace, renderer, action => workspace.Request(action));
            for (int i = 0; i < 10; i++) { renderer.BeginLayoutFrame(); pins.Prepare(1920, 1080); }
            pins.Pointer(120, 120, false, false, -120, true, true, false, 1920, 1080);
            Check(pins.ConsumeWheel && pins.Pins[0].Scroll == 0 && pins.Pins[1].Scroll > 0, "overlap wheel affects exactly top pin");
            pins.Pointer(120, 180, true, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(200, 200, true, false, 0, true, true, false, 1920, 1080);
            Check(pins.Pins[1].Rect.X == 100, "body does not start drag");
            pins.Pointer(200, 200, false, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(110, 110, true, false, 0, true, true, false, 1920, 1080);
            pins.Pointer(150, 150, true, false, 0, true, true, false, 1920, 1080);
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
                F5Rect visibleBody = pins.Pins[1].Body;
                bool ink = false;
                for (int y = (int)Math.Ceiling(visibleBody.Y); y < Math.Min(visibleBody.Bottom, visibleBody.Y + pins.Pins[1].LineHeight); y++)
                    for (int x = (int)Math.Ceiling(visibleBody.X); x < Math.Min(visibleBody.Right, visibleBody.X + 62); x++) ink |= pixels[y * 700 + x].A != 0;
                Check(ink, "transparent background does not hide text");
            }
        }
        private static void CheckFooterPixels(NotesRenderer renderer, F5FixtureGraphics graphics)
        {
            Note note = Note.Create().WithText(false, new string('文', 400) + "\n末行").Pin(20, 20);
            WithWorkspace(workspace =>
            {
                var pins = new NotesPins(workspace, renderer, action => workspace.Request(action));
                for (int i = 0; i < 10; i++) { renderer.BeginLayoutFrame(); pins.Prepare(700, 500); }
                NotesPin pin = pins.Pins[0];
                pins.Pointer(pin.Body.X + 2, pin.Body.Y + 2, false, false, -12000, true, true, false, 700, 500);
                pins.Pointer(0, 0, false, false, 0, true, true, false, 700, 500);
                Color[] baseline = DrawPins(pins, renderer, graphics);
                foreach (bool error in new[] { false, true })
                {
                    if (error)
                    {
                        workspace.Request(new NotesAction(NotesActionKind.BeginEdit, note.Id, false, 0)); workspace.Editor.Insert("draft");
                        workspace.Request(new NotesAction(NotesActionKind.FinishEdit));
                        Check(!workspace.Request(new NotesAction(NotesActionKind.Create)) && workspace.Error != null, "pending action exposes real error feedback");
                    }
                    pins.Pointer(pin.Body.X + 2, pin.Body.Y + 2, false, false, 0, true, true, false, 700, 500);
                    Color[] shown = DrawPins(pins, renderer, graphics); bool lastLineInk = false, footerInk = false;
                    for (int y = (int)Math.Ceiling(pin.Body.Y); y < (int)Math.Floor(pin.Body.Bottom); y++)
                        for (int x = (int)Math.Ceiling(pin.Body.X); x < (int)Math.Floor(pin.Body.Right); x++)
                        {
                            Check(shown[y * 700 + x] == baseline[y * 700 + x], "normal/error footer cannot cover any body pixel");
                            if (y >= pin.Body.Bottom - pin.LineHeight) lastLineInk |= shown[y * 700 + x].A != 0;
                        }
                    for (int y = (int)Math.Ceiling(pin.Body.Bottom); y < (int)Math.Floor(pin.Footer.Y); y++)
                        for (int x = (int)pin.Body.X; x < (int)pin.Body.Right; x++)
                            Check(shown[y * 700 + x].A == 0, "transparent gap separates actual body and footer drawing");
                    for (int y = (int)Math.Ceiling(pin.Footer.Y); y < (int)Math.Floor(pin.Footer.Bottom); y++)
                        for (int x = (int)pin.Footer.X; x < (int)pin.Footer.Right; x++) footerInk |= shown[y * 700 + x].A != 0;
                    Check(lastLineInk && footerInk, "last line and normal/error footer are both actually drawn");
                }
                Drain(workspace);
            }, new Notebook(new[] { note }));
        }
        private static Color[] DrawPins(NotesPins pins, NotesRenderer renderer, F5FixtureGraphics graphics)
        {
            using (var target = new RenderTarget2D(graphics.Device, 700, 500))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                renderer.Pass(Matrix.Identity, null, pins.Draw); Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[700 * 500]; target.GetData(pixels); return pixels;
            }
        }
        private static void Click(NotesCards cards, F5Interaction state, F5Rect rect, bool down)
        {
            state.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1,
                X = state.X + state.Layout.Viewport.X + rect.X + 5, Y = state.Y + state.Layout.Viewport.Y + rect.Y + 5 - state.Scroll, Left = down });
            cards.Pointer(state, down, !down);
        }
        private static bool HasControl(NotesCards cards, string key)
        { foreach (NotesControl control in cards.Controls) if (control.Key == key) return true; return false; }
        internal static void WithWorkspace(Action<NotesWorkspace> action, Notebook seed = null)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-notes-host-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "notes.json"); var codec = new NotebookCodec();
            File.WriteAllBytes(path, codec.Encode(seed ?? Notebook.Empty.Add(Note.Create().WithText(true, "one")).Add(Note.Create().WithText(false, new string('长', 500)))));
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
        private sealed class MetricSource : ReLogic.Content.Sources.IContentSource
        {
            public ReLogic.Content.IContentValidator ContentValidator { get; set; }
            public string FileWatcherPath { get { return null; } }
            public bool HasAsset(string name) { return false; }
            public List<string> GetAllAssetsStartingWith(string name) { return new List<string>(); }
            public string GetExtension(string name) { return null; }
            public Stream OpenStream(string name) { throw new NotSupportedException(); }
            public void RejectAsset(string name, ReLogic.Content.IRejectionReason reason) { }
            public void ClearRejections() { }
            public bool TryGetRejections(List<string> reasons) { return false; }
            public void Refresh() { }
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
