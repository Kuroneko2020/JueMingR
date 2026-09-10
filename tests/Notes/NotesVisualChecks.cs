using System;
using System.IO;
using JueMingR.Features.Notes;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria
{
    // Deliberately synthetic content, rendered by production Notes classes with
    // the explicitly loaded local XNB font/skin. Never reads the user's notebook.
    internal static class NotesVisualChecks
    {
        internal static void Run(F5FixtureGraphics graphics, string output)
        {
            // Standalone previews need the same fixture IME-anchor host as NotesHostChecks.
            new Main();
            NotesHostChecks.WithWorkspace(workspace =>
            {
                using (var renderer = new NotesRenderer())
                using (var chrome = new F5Renderer())
                {
                    renderer.Refresh(); chrome.RefreshResources();
                    var input = new NotesInput(workspace, new Clipboard(), new Ime());
                    var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                    var pins = new NotesPins(workspace, renderer, action => workspace.Request(action));
                    var shell = NewShell();
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-empty", "点击 + 新建笔记，双击标题或正文编辑。");
                }
            }, Notebook.Empty);
            Note note = Note.Create().WithText(true, "旅途笔记").WithText(false,
                "带上火把与绳索\n记住回家的方向\n\n选中这一段文字，可以局部复制。\n字面文本：[i:2] [c/ff0000:红字]\n" + new string('测', 80)).Pin(840, 80).WithOpacity(85);
            Note other = Note.Create().WithText(true, "临时清单").WithText(false, "另一张便签保留自己的大小与字号。").Pin(1120, 580);
            NotesHostChecks.WithWorkspace(workspace =>
            {
                using (var renderer = new NotesRenderer())
                using (var chrome = new F5Renderer())
                {
                    renderer.Refresh(); chrome.RefreshResources(); Main.UIScaleMatrix = Matrix.Identity;
                    var input = new NotesInput(workspace, new Clipboard(), new Ime());
                    var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                    var pins = new NotesPins(workspace, renderer, action => workspace.Request(action));
                    var shell = NewShell();
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-browse", "双击标题或正文编辑。Shift 滚轮调区域，Ctrl 滚轮调字号；< > 调背景，× 取消悬挂。");
                    workspace.RequestDelete(other.Id);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-delete-confirmation", "点击确认删除，或取消删除。");
                    workspace.CancelDelete();
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, note.Id, false, 0)); workspace.Editor.MoveTo(8, true); workspace.Editor.Insert("已准备火把和绳索");
                    workspace.Editor.MoveTo(10); workspace.Editor.MoveTo(22, true);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-selection", "Enter 换行，Esc 取消编辑。");
                    workspace.CancelEdit(); workspace.Feature.AdjustReading(note.Id, true, 7); workspace.Feature.AdjustReading(note.Id, false, 6);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-large-reading", "阅读区域与字号只作用于该张悬挂笔记，工具栏保持可读。");
                    workspace.Feature.AdjustReading(note.Id, true, -8); workspace.Feature.AdjustReading(note.Id, false, -10);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-small-reading", "区域与正文字号下限；工具栏仍为同一字体与高度。");
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-footer-last-line", "悬挂正文滚动到底：末行与下方提示区分离。", footer: true);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-scale150", "F5 缩放 150%；两张便签按屏幕坐标保持各自阅读偏好。", 1920, 1080, 1.5f);
                    Render(graphics, renderer, chrome, cards, pins, shell, output, "notes-actual-narrow-scale150", "窄视口下检查导航、操作按钮、正文与滚动条。", 1000, 900, 1.5f);
                }
            }, new Notebook(new[] { note, other }));
            Console.WriteLine("PASS: Notes actual-XNB previews generated from synthetic content; pack and owner acceptance remain pending.");
        }
        private static F5Interaction NewShell()
        {
            var shell = new F5Interaction { Ready = true };
            shell.RestorePosition(new WindowPosition(12, 12));
            shell.Update(new F5Input { Width = 1440, Height = 960, Scale = 1, Active = true, Focused = true, F5 = true });
            shell.Navigate(4); return shell;
        }
        private static void Render(F5FixtureGraphics graphics, NotesRenderer renderer, F5Renderer chrome, NotesCards cards, NotesPins pins,
            F5Interaction shell, string output, string name, string status, int width = 1440, int height = 960, float scale = 1, bool footer = false)
        {
            Main.UIScaleMatrix = Matrix.CreateScale(scale, scale, 1);
            shell.Update(new F5Input { Width = width, Height = height, Scale = scale, Active = true, Focused = true });
            chrome.Prepare(shell, width, height, scale);
            for (int i = 0; i < 30; i++) { renderer.BeginLayoutFrame(); cards.Prepare(shell, status); pins.Prepare(width, height); }
            if (pins.Pins.Count != 0)
            {
                NotesPin pin = pins.Pins[0];
                F5Rect region = footer ? pin.Body : pin.Drag;
                pins.Pointer(region.X + 10, region.Y + 10, false, false, footer ? -12000 : 0, true, true, false, width, height);
            }
            using (var target = new RenderTarget2D(graphics.Device, width, height))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(new Color(70, 100, 103));
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                renderer.Pass(Matrix.Identity, null, pins.Draw);
                chrome.Draw(shell, Main.UIScaleMatrix, false, false);
                renderer.Pass(Main.UIScaleMatrix, shell.Layout.Viewport.Offset(shell.X, shell.Y), () => cards.Draw(shell));
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                using (var stream = File.Create(Path.Combine(output, name + ".png"))) target.SaveAsPng(stream, target.Width, target.Height);
            }
        }
        private sealed class Clipboard : INotesClipboard
        { public bool TryCopy(string text) { return true; } public bool TryPaste(out string text) { text = ""; return true; } }
        private sealed class Ime : INotesIme
        { public string Composition { get { return ""; } } public bool Candidates { get { return false; } } public void Toggle(bool enabled) { } }
    }
}
