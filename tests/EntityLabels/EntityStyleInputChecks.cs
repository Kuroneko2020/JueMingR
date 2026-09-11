using System;
using JueMingR.TerrariaHost.EntityLabels;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class EntityStyleInputChecks
    {
        internal static void Run()
        {
            int submissions = 0, last = -1;
            var editor = new StyleEditor(0x123457, value => { submissions++; last = value; return true; });
            Check(editor.Rgb == 0x123457 && submissions == 0, "open preserves exact RGB without HSL roundtrip write");
            editor.PreviewHsl(0, 180); editor.PreviewHsl(1, 100); editor.PreviewHsl(2, 50);
            Check(editor.Rgb == 0x00FFFF && submissions == 0, "HSL preview does not publish settings");
            editor.CancelDraft(); Check(editor.Rgb == 0x123457 && submissions == 0, "cancel draft restores latest submitted color");
            editor.PreviewHsl(0, 180); editor.PreviewHsl(1, 100); editor.PreviewHsl(2, 50); editor.Commit();
            Check(submissions == 1 && last == 0x00FFFF, "one explicit release submits one color");
            editor.BeginHex(); editor.Insert("ABC"); Check(submissions == 1 && editor.Hex == "ABC", "first characters replace full prefilled field");
            editor.CancelDraft(); Check(editor.Rgb == 0x00FFFF, "escape cannot revert earlier HSL commit");
            editor.BeginHex(); editor.Insert("abcdef"); Check(submissions == 2 && last == 0xABCDEF, "six valid digits auto submit once");
            editor.CancelDraft(); Check(editor.Rgb == 0xABCDEF, "escape after auto commit preserves submitted value");
            editor.BeginHex(); editor.Insert("1234567"); Check(submissions == 2 && editor.Rgb == 0xABCDEF, "overlong candidate is rejected whole");
            editor.Insert("12ZZ34"); Check(submissions == 2, "invalid candidate does not partially commit");
            editor.Insert("123"); editor.CommitHex(); Check(submissions == 2, "Enter cannot save an incomplete field");
            editor.SelectAll(); editor.Insert("123456"); editor.CommitHex(); Check(submissions == 3 && last == 0x123456, "Enter after auto submit causes no duplicate write");
            editor.Load(0x808080); editor.PreviewHsl(0, 250); double hue = editor.Hue; editor.Commit(); editor.Load(0x808080);
            Check(editor.Hue == hue, "achromatic field retains transient hue without persisting another preference");
            NativeInput();
            PopupInput();
            Console.WriteLine("PASS: style draft, exact RGB, HSL preview/commit, HEX replacement/completeness and prior commit preservation.");
        }
        private static void NativeInput()
        {
            Main.CurrentInputTextTakerOverride = null; Main.blockInput = Main.drawingPlayerChat = Main.editSign = Main.editChest = false;
            var clipboard = new Clipboard(); var ime = new Ime(); bool focused = true;
            var input = new HostInputState(() => new IntPtr(1), () => focused ? new IntPtr(1) : new IntPtr(2));
            var text = new HexTextInput(input, clipboard, ime); int saved = 0;
            var editor = new StyleEditor(0x123457, _ => { saved++; return true; });
            Action<string, Keys[]> frame = (characters, keys) =>
            {
                FocusHelper.IsSelectedApplication = focused; input.BeginUpdate(); text.BeforeInput(input.CanPrepareText);
                PlayerInput.MouseInfo = new MouseState(); input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh();
                foreach (char c in characters) { Main.keyInt[Main.keyCount] = c; Main.keyString[Main.keyCount++] = c.ToString(); }
                text.Process(input.CanUseInput);
            };
            frame("", new Keys[0]); text.Begin(editor); frame("ABC", new[] { Keys.C });
            Check(text.OwnsTextToken && Main.blockInput && editor.Hex == "ABC" && saved == 0, "native queue reaches only current HEX owner");
            frame("", new Keys[0]); frame("", new[] { Keys.Escape });
            Check(!text.Editing && !Main.blockInput && editor.Rgb == 0x123457 && input.Hotkeys.IsSuppressed((int)Keys.Escape), "Esc cancels draft and preserves its complete key tail");
            frame("", new Keys[0]); text.Begin(editor); clipboard.Text = "#ABCDEF"; frame("", new[] { Keys.LeftControl, Keys.V });
            Check(saved == 1 && editor.Rgb == 0xABCDEF && clipboard.Reads == 1, "whole optional hash-prefixed paste submits once");
            frame("", new Keys[0]); editor.SelectAll(); clipboard.Text = "112233 invalid"; frame("", new[] { Keys.LeftControl, Keys.V });
            Check(saved == 1 && editor.Rgb == 0xABCDEF, "paste never truncates valid-looking prefix");
            frame("", new Keys[0]); editor.SelectAll(); ime.Current = "a"; frame("", new[] { Keys.Enter });
            ime.Current = ""; frame("123456", new[] { Keys.Enter });
            Check(saved == 2 && editor.Rgb == 0x123456 && text.Editing, "IME committed queue survives confirmation while Enter remains owned by composition");
            frame("", new Keys[0]); editor.SelectAll(); frame("12", new[] { Keys.D2 }); focused = false; frame("", new Keys[0]);
            Check(!text.Editing && editor.Rgb == 0x123456 && Main.CurrentInputTextTakerOverride == null, "focus loss discards only unfinished input and releases own token");
            focused = true; frame("", new Keys[0]); frame("", new Keys[0]); text.Begin(editor); frame("", new Keys[0]);
            object foreign = new object(); Main.CurrentInputTextTakerOverride = foreign; int toggles = ime.Toggles;
            frame("", new Keys[0]); Check(!text.Editing && ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign) && ime.Toggles == toggles,
                "foreign text takeover keeps its token and IME untouched");
            Main.CurrentInputTextTakerOverride = null; Main.blockInput = false; PlayerInput.WritingText = false;
        }
        private sealed class Clipboard : INotesClipboard
        { internal string Text; internal int Reads; public bool TryCopy(string text) { return false; } public bool TryPaste(out string text) { Reads++; text = Text; return true; } }
        private sealed class Ime : INotesIme
        { internal int Toggles; internal string Current = ""; public string Composition { get { return Current; } } public bool Candidates { get { return false; } } public void Toggle(bool value) { Toggles++; } }
        private static void PopupInput()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-StylePopup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var host = new HostEntityLabels(root, new SingleFeatureRuntime(new Probe(), new Idle()));
            var document = (PreferenceDocument<EntityLabelSettings>)typeof(HostEntityLabels).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            bool stopped = false, focused = true;
            try
            {
                Check(SpinWait.SpinUntil(() => host.Preferences.IsLoaded, 3000), "popup isolated preferences ready");
                var input = new HostInputState(() => new IntPtr(1), () => focused ? new IntPtr(1) : new IntPtr(2));
                var popup = new StylePopup(host, input, new Clipboard(), new Ime()); object font = new object();
                var anchor = new F5Rect(200, 100, 44, 30); int measurements = 0;
                Func<string, float, F5Size> measure = (s, scale) => { measurements++; return new F5Size(s.Length * 14 * scale, 24 * scale); };
                Action prepare = () => popup.Prepare(800, 600, 1, font, measure, 0, anchor);
                Action<float, float, bool, bool, Keys[]> frame = (x, y, left, geometry, keys) =>
                {
                    FocusHelper.IsSelectedApplication = focused; input.BeginUpdate(); popup.BeforeInput(input.CanPrepareText);
                    PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh(); popup.Process(input.CanUseInput, 9, x, y, geometry, 120);
                    prepare();
                };
                frame(0, 0, false, true, new Keys[0]); popup.Click(EntityLabelKind.Enemy, anchor, 9); prepare();
                int stable = measurements; for (int i = 0; i < 100; i++) prepare(); Check(measurements == stable, "stable popup does not reformat/measure every frame");
                var slider = popup.Layout.Sliders[0].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                long revision = host.Preferences.Revision; int old = host.Preferences.Value.EnemyStyle.Rgb;
                frame(slider.X + slider.Width / 2, slider.Y + 3, true, true, new Keys[0]); prepare();
                Check(popup.Editor.Rgb != old && host.Preferences.Revision == revision, "real slider down only previews");
                frame(slider.Right + 200, slider.Y, true, true, new Keys[0]); Check(popup.BlockPointer && popup.ConsumeWheel, "captured drag owns outside pointer and wheel");
                frame(slider.X + slider.Width / 2, slider.Y, false, true, new Keys[0]);
                Check(host.Preferences.Revision == revision + 1, "one genuine focused release publishes once");
                prepare(); slider = popup.Layout.Sliders[1].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y); revision = host.Preferences.Revision;
                frame(slider.X, slider.Y + 3, true, true, new Keys[0]); focused = false; frame(0, 0, false, true, new Keys[0]);
                Check(!popup.Visible && host.Preferences.Revision == revision, "focus synthesized release cancels without publishing");
                focused = true; frame(0, 0, false, true, new Keys[0]); frame(0, 0, false, true, new Keys[0]);
                popup.Click(EntityLabelKind.Enemy, anchor, 9); prepare(); slider = popup.Layout.Sliders[1].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                frame(slider.X, slider.Y + 3, true, true, new Keys[0]); frame(slider.X, slider.Y, false, false, new Keys[0]);
                Check(host.Preferences.Revision == revision && popup.Visible, "geometry invalidation is not a commit or whole F5 failure");
                prepare(); var field = popup.Layout.HexField.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                frame(field.X + 3, field.Y + 3, true, true, new Keys[0]); frame(field.X + 3, field.Y + 3, false, true, new Keys[0]);
                Check(popup.TextInput.Editing, "real HEX field click owns editor");
                popup.Editor.Insert("123"); frame(field.X, field.Y, false, true, new[] { Keys.Enter });
                Check(popup.TextInput.Editing && popup.Editor.Hex == "123" && popup.Editor.Error != null, "error content reflow preserves visible draft and feedback");
                frame(field.X, field.Y, false, true, new Keys[0]);
                frame(field.X, field.Y, false, true, new[] { Keys.Escape }); Check(popup.Visible && !popup.TextInput.Editing, "first Escape closes field only");
                frame(field.X, field.Y, false, true, new Keys[0]); frame(field.X, field.Y, false, true, new[] { Keys.Escape });
                Check(!popup.Visible && input.Hotkeys.IsSuppressed((int)Keys.Escape), "next Escape closes popup and owns same edge");
                frame(0, 0, false, true, new Keys[0]); popup.Click(EntityLabelKind.Enemy, anchor, 9); prepare();
                slider = popup.Layout.Sliders[0].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y); revision = host.Preferences.Revision;
                frame(slider.X, slider.Y + 3, true, true, new Keys[0]); frame(slider.X, slider.Y + 3, true, true, new[] { Keys.Escape });
                Check(popup.Visible && popup.ActiveSlider < 0 && host.Preferences.Revision == revision, "Esc cancels slider first and preserves the popup");
                frame(slider.X, slider.Y + 3, false, true, new Keys[0]); Check(host.Preferences.Revision == revision, "cancelled gesture cannot publish on its later release");
            }
            finally
            {
                stopped = document.Stop(3000);
                string full = Path.GetFullPath(root);
                Check(Path.GetDirectoryName(full).Equals(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(full).StartsWith("JueMingR-StylePopup-", StringComparison.Ordinal), "exact isolated popup root");
                if (stopped) Directory.Delete(full, true);
            }
            Check(stopped, "popup worker stopped before releasing exact isolated root");
        }
        private sealed class Probe : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class Idle : IRuntimeFeature
        { public bool Enabled { get { return false; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
