using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Features.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The OS IME is synthetic; controls, input leases, native character queue,
    // preference commands and (when available) rendering are production code.
    internal static class NativeAfkUiChecks
    {
        internal static void Run(object context, ProbeGraphics graphics = null, string output = null)
        {
            var host = Get(context, "Afk"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var ui = Get(shell, "MiscUi"); var row = Get(ui, "AfkRow");
            var textInput = Get(row, "TextInput");
            var imeField = textInput.GetType().GetField("ime", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalIme = imeField.GetValue(textInput); var ime = new Ime(imeField.FieldType);
            imeField.SetValue(textInput, ime.GetTransparentProxy());
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return (bool)Get(host, "ControlsEnabled"); });
            bool oldMode = (bool)Get(host, "ModeEnabled"); int oldGap = (int)Get(host, "GapMilliseconds");
            try
            {
                Main.blockInput = false; PlayerInput.WritingText = false; Main.CurrentInputTextTakerOverride = null;
                Call(host, "SetModeEnabled", false); Call(host, "SetGapMilliseconds", 0);
                Set(state, "Ready", true); Call(state, "Navigate", 1); Call(state, "RestoreVisible");
                Call(Get(shell, "renderer"), "RefreshResources");
                Prepare(shell, state, graphics, 960, 760, 1);
                Reveal(shell, state, row, graphics, 960, 760, 1);
                Require((string)Get(row, "valueText") == "双击输入空档" && GetOptional(row, "Editor") == null,
                    "zero gap is a measured placeholder with no passive keyboard editor");
                var field = Control(ui, "AfkGap"); var fieldPoint = Point(field);
                object[] hintArgs = { fieldPoint.X, fieldPoint.Y, null, null };
                Require(ui.GetType().GetMethod("Hint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, hintArgs) == null,
                    "gap input has no extra tooltip");
                var name = ((IEnumerable)Get(ui, "elements")).Cast<object>().First(e => GetOptional(e, "Description") != null && (string)Get(Get(e, "Description"), "Id") == "afk.toggle");
                FinalHint(shell, name, "开启功能后，手持道具，按一下左键开始持续使用这个道具");
                FinalHint(shell, Get(Control(ui, "Hotkey", "afk.toggle"), "Element"), "双击设置功能开关快捷键");
                int builds = (int)Get(ui, "LayoutBuildCount");
                for (int i = 0; i < 120; i++) Prepare(shell, state, graphics, 960, 760, 1);
                Require(builds == (int)Get(ui, "LayoutBuildCount"), "stable AFK controls do not rebuild Misc geometry");

                Click(context, ui, Controls(ui).Single(c => Get(c, "Command").ToString() == "Afk" && (int)Get(c, "Argument") == 1));
                Require((bool)Get(host, "ModeEnabled") && !(bool)Get(host, "Running"), "standard enable command changes the sole preference and enters standby");
                Reveal(shell, state, row, graphics, 960, 760, 1);
                Click(context, ui, Controls(ui).Single(c => Get(c, "Command").ToString() == "Afk" && (int)Get(c, "Argument") == 0));
                Require(!(bool)Get(host, "ModeEnabled"), "standard disable command reaches the same preference owner");
                Reveal(shell, state, row, graphics, 960, 760, 1);

                Click(context, ui, Control(ui, "AfkGap"));
                Require(GetOptional(row, "Editor") == null && !(bool)Get(row, "OwnsTextToken"), "one field click never acquires text input");
                Click(context, ui, Control(ui, "AfkGap"));
                Require(GetOptional(row, "Editor") != null, "same-geometry physical double click starts the numeric draft");
                Step(context, ui, false, Vector2.Zero, new KeyboardState(), "125");
                Require((bool)Get(row, "OwnsTextToken") && Main.blockInput && PlayerInput.WritingText &&
                    ((TextEditBuffer)Get(row, "Editor")).Text == "125", "real text lease receives only the numeric draft, excluding fixed ms");
                Require((bool)Call(shell, "NativePresentationAvailable", false, true), "Shell recognizes its own Misc editor token");
                ime.Composition = "候选";
                Step(context, ui, false, Vector2.Zero, new KeyboardState(Keys.Enter), "\r");
                Require((int)Get(host, "GapMilliseconds") == 0 && GetOptional(row, "Editor") != null, "IME confirmation Enter cannot also save");
                ime.Composition = "";
                Step(context, ui, false, Vector2.Zero, new KeyboardState());
                Step(context, ui, false, Vector2.Zero, new KeyboardState(Keys.Enter), "\r");
                Require((int)Get(host, "GapMilliseconds") == 125 && GetOptional(row, "Editor") == null && !(bool)Get(row, "OwnsTextToken"),
                    "fresh Enter immediately changes memory and releases this editor before any persistence polling");
                Require(Main.keyState.IsKeyUp(Keys.Enter) && !(bool)Get(host, "Running"), "saved Enter is consumed and never starts an AFK run");
                Step(context, ui, false, Vector2.Zero, new KeyboardState(Keys.Enter));
                Require(Main.keyState.IsKeyUp(Keys.Enter), "held Enter tail remains consumed after the editor closes");
                Step(context, ui, false, Vector2.Zero, new KeyboardState());

                foreach (var value in new[] { "", "0", "2147483647", "2147483648", "-1", "12ms" })
                {
                    Call(row, "CancelEdit"); Reveal(shell, state, row, graphics, 960, 760, 1);
                    Click(context, ui, Control(ui, "AfkGap")); Click(context, ui, Control(ui, "AfkGap"));
                    var editor = (TextEditBuffer)Get(row, "Editor"); editor.SelectAll(); editor.DeleteSelection(); editor.Insert(value);
                    int before = (int)Get(host, "GapMilliseconds");
                    Step(context, ui, false, Vector2.Zero, new KeyboardState());
                    Step(context, ui, false, Vector2.Zero, new KeyboardState(Keys.Enter), "\r");
                    bool valid = value == "" || value == "0" || value == "2147483647";
                    Require((int)Get(host, "GapMilliseconds") == (valid ? value == "2147483647" ? int.MaxValue : 0 : before),
                        "integer/empty/overflow submission preserves the required memory value: " + value);
                    Require((GetOptional(row, "Editor") == null) == valid, "invalid text stays editable without replacing the last preference: " + value);
                    Step(context, ui, false, Vector2.Zero, new KeyboardState());
                }

                Call(row, "CancelEdit"); Call(host, "SetGapMilliseconds", 0);
                Reveal(shell, state, row, graphics, 960, 760, 1);
                Click(context, ui, Control(ui, "AfkGap"));
                Reveal(shell, state, row, graphics, 1280, 720, 1.5f);
                Click(context, ui, Control(ui, "AfkGap"));
                Require(GetOptional(row, "Editor") == null, "layout/matrix change retires the first click of a double click");
                Click(context, ui, Control(ui, "AfkGap"));
                Step(context, ui, false, Vector2.Zero, new KeyboardState(), "77");
                Require((bool)Get(row, "OwnsTextToken"), "fresh double click reacquires editing after scale projection");
                Call(ui, "Suspend");
                Require(GetOptional(row, "Editor") == null && !(bool)Get(row, "OwnsTextToken") && (int)Get(host, "GapMilliseconds") == 0,
                    "closing or focus suspension discards an uncommitted numeric draft and releases its lease");

                foreach (var shape in new[] { new[] { 960, 760, 100 }, new[] { 640, 440, 100 }, new[] { 1280, 720, 150 } })
                {
                    float scale = shape[2] / 100f; Reveal(shell, state, row, graphics, shape[0], shape[1], scale);
                    var f = Get(Control(ui, "AfkGap"), "Rect"); var on = Get(Controls(ui).Single(c => Get(c, "Command").ToString() == "Afk" && (int)Get(c, "Argument") == 1), "Rect");
                    Require((float)Get(f, "Right") <= (float)Get(on, "X") && (float)Get(f, "Y") == (float)Get(on, "Y"),
                        "the fixed-unit field remains left of the standard enable button in each viewport");
                    if (graphics != null)
                    {
                        Directory.CreateDirectory(output);
                        var matrix = Matrix.CreateScale(scale);
                        graphics.Image(Path.Combine(output, "afk-controls-" + shape[0] + "x" + shape[1] + "-" + shape[2] + ".png"), () => {
                            Call(Get(shell, "renderer"), "Draw", state, matrix, false, false);
                            Call(ui, "Draw", Get(shell, "drawKeyboard"), true);
                        }, matrix, shape[0], shape[1]);
                    }
                }
                Console.WriteLine("PASS AFK UI: actual Misc controls, measured fixed ms, physical double click, production text lease/IME Enter tail, immediate memory values/overflow, scale retirement and stable layout." + (graphics == null ? " Draw not executed." : " Original-resource Draw previews written."));
            }
            finally
            {
                ime.Composition = ""; Call(ui, "Suspend"); imeField.SetValue(textInput, originalIme);
                Call(host, "SetGapMilliseconds", oldGap); Call(host, "SetModeEnabled", oldMode);
                Call(state, "Close"); Main.clrInput();
            }
        }
        private static object[] Controls(object ui) { return ((IEnumerable)Get(ui, "controls")).Cast<object>().ToArray(); }
        private static object Control(object ui, string command, string action = null)
        { return Controls(ui).First(c => Get(c, "Command").ToString() == command && (action == null || (string)Get(Get(c, "Element"), "HotkeyTarget") == action)); }
        private static Vector2 Point(object control)
        { var r = Get(control, "Rect"); return new Vector2((float)Get(r, "X") + 5, (float)Get(r, "Y") + 5); }
        private static void Prepare(object shell, object state, ProbeGraphics graphics, int width, int height, float scale)
        {
            // RestoreVisible does not establish the window's centered origin or
            // active sample. Settle that real interaction geometry BEFORE the
            // dynamic page projects it, as the normal Update/Prepare cycle does.
            var sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            Set(sample, "Width", (float)width); Set(sample, "Height", (float)height); Set(sample, "Scale", scale);
            Set(sample, "X", -10f); Set(sample, "Y", -10f); Set(sample, "Active", true); Set(sample, "Focused", true);
            Call(state, "Update", sample);
            Call(Get(shell, "renderer"), "Prepare", state, (float)width, (float)height, scale);
            Call(shell, "PrepareMisc", true, Matrix.CreateScale(scale), new Vector2(width, height), graphics != null);
        }
        private static void FinalHint(object shell, object element, string expected)
        {
            try { NativeQuickUiChecks.FinalHint(shell, element, expected); }
            catch
            {
                var hint = Get(Get(shell, "renderer"), "HintLayout");
                string actual = string.Concat(((IEnumerable)Get(hint, "Lines")).Cast<object>().Select(line => (string)Get(line, "Text")));
                Console.WriteLine("AFK final hint visible=" + Get(hint, "Visible") + " actual=[" + actual + "] expected=[" + expected + "]");
                throw;
            }
        }
        private static void Reveal(object shell, object state, object row, ProbeGraphics graphics, int width, int height, float scale)
        {
            Prepare(shell, state, graphics, width, height, scale);
            var panel = ((IEnumerable)Get(row, "rows")).Cast<object>().First(e => Get(e, "Kind").ToString() == "Panel");
            Call(state, "ScrollTo", (float)Get(Get(panel, "Rect"), "Y")); Prepare(shell, state, graphics, width, height, scale);
        }
        private static void Click(object context, object ui, object control)
        { var point = Point(control); Step(context, ui, false, point, new KeyboardState()); Step(context, ui, true, point, new KeyboardState()); Step(context, ui, false, point, new KeyboardState()); }
        private static void Step(object context, object ui, bool left, Vector2 point, KeyboardState keys, string characters = "")
        {
            var input = Get(context, "Input"); Main.keyState = keys;
            PlayerInput.MouseInfo = new MouseState((int)point.X, (int)point.Y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; Main.mouseLeft = left;
            Call(input, "BeginUpdate"); Call(ui, "BeforeInput", true); Call(input, "AfterMapping"); PlayerInput.WritingText = false; Call(input, "AfterKeyboardRefresh");
            Main.keyCount = characters.Length;
            for (int i = 0; i < characters.Length; i++) { Main.keyInt[i] = characters[i]; Main.keyString[i] = characters[i].ToString(); }
            Call(ui, "ProcessInput", true, keys, point, true, true, false);
            Call(Get(context, "Shell"), "ConsumeSample");
            if ((bool)Get(ui, "ConsumeLeft")) Require(!PlayerInput.Triggers.Current.MouseLeft && !Main.mouseLeft, "owned field click and its release tail never reach native world use");
        }
        private sealed class Ime : RealProxy
        {
            internal string Composition = "";
            internal Ime(Type contract) : base(contract) { }
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                object value = call.MethodName == "get_Composition" ? (object)Composition : call.MethodName == "get_Candidates" ? (object)false : null;
                return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
            }
        }
    }
}
