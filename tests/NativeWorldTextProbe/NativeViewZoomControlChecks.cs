using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These checks execute the real Host target owner and F5 presentation.
    // Synthetic samples/time prove input semantics; GPU pixels prove this UI,
    // neither claims physical Windows input or a complete native world Draw.
    internal static class NativeViewZoomControlChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Cpu(object context)
        {
            object host = Get(context, "View");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
            Assembly assembly = host.GetType().Assembly;
            Type zoom = assembly.GetType("JueMingR.TerrariaHost.View.ViewZoom", true);
            Type controlType = assembly.GetType("JueMingR.TerrariaHost.F5.ViewZoomControl", true);
            Func<float, float, bool, float, float> request = (c, q, permission, m) => (float)zoom.GetMethod("Request", Flags).Invoke(null, new object[] { c, q, permission, m });
            foreach (var row in new[] {
                new[]{1.5f,.5f,1f},new[]{1.5f,5f,2f},new[]{.75f,.7f,.75f},new[]{.75f,.8f,.8f},
                new[]{.75f,.6f,.75f},new[]{.75f,1.5f,1.5f},new[]{.75f,5f,2f},new[]{.85f,.75f,.85f},
                new[]{2.5f,2.55f,2.5f},new[]{2.5f,2.45f,2.45f},new[]{2.5f,5f,2.5f},new[]{2.5f,1.5f,1.5f},
                new[]{2.5f,.5f,1f},new[]{1f,.99f,1f},new[]{2f,2.01f,2f},new[]{3f,5f,3f}})
                Near(request(row[0], row[1], false, .3f), row[2], "OFF latest-target request");
            Near(request(.85f, .75f, false, .3f), .85f, "inward low gesture cannot retreat");
            Near(request(2.45f, 2.5f, false, .3f), 2.45f, "inward high gesture cannot retreat");
            Near(request(1f, 0f, true, .42f), .42f, "dynamic minimum");
            Near(request(1f, 20f, true, .3f), 10f, "maximum");
            Near(request(.75f, float.NaN, true, .3f), .75f, "NaN rejected");
            Near(request(.75f, float.PositiveInfinity, true, .3f), .75f, "infinity rejected");
            Require(float.IsNaN(request(float.NaN, 1f, true, .3f)), "invalid current not fabricated as one");
            foreach (float minimum in new[] { .3f, .57f, 1f })
            {
                float previous = minimum;
                for (int i = 0; i <= 500; i++)
                {
                    float p = i / 500f, value = (float)controlType.GetMethod("FromPosition", Flags).Invoke(null, new object[] { p, minimum, 10f });
                    float inverse = (float)controlType.GetMethod("ToPosition", Flags).Invoke(null, new object[] { value, minimum, 10f });
                    Require(value >= previous && value >= minimum && value <= 10f, "monotonic bounded mapping");
                    Near(inverse, p, "inverse mapping"); previous = value;
                }
                Near((float)controlType.GetMethod("FromPosition", Flags).Invoke(null, new object[] { (float)controlType.GetMethod("BlueStart", Flags).Invoke(null, new object[] { minimum }), minimum, 10f }), 1f, "blue 100 endpoint");
                Near((float)controlType.GetMethod("FromPosition", Flags).Invoke(null, new object[] { (float)controlType.GetMethod("BlueEnd", Flags).Invoke(null, new object[] { minimum }), minimum, 10f }), 2f, "blue 200 endpoint");
            }
            object state = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Interaction", true), true);
            Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible");
            object layout = Get(state, "Layout"), font = new object();
            var sizeType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Size", true);
            var measure = typeof(NativeViewZoomControlChecks).GetMethod(nameof(Measure), Flags).MakeGenericMethod(sizeType).CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), sizeType));
            Call(layout, "Ensure", 1280f, 900f, 1f, 2, font, measure);
            double time = 0; Func<double> clock = () => time;
            object control = Activator.CreateInstance(controlType, Flags, null, new object[] { host, state, clock }, null);
            Func<string, Vector2> point = command => Point(state, Element(layout, command));
            Action<Vector2, bool, bool, bool, bool, object> sample = (p, left, edge, active, focused, currentFont) =>
                Call(control, "Process", active, focused, left, edge, false, p.X, p.Y, Matrix.Identity, currentFont, 0);
            float saved = Main.GameZoomTarget; bool enabled = (bool)Get(host, "UnlockEnabled");
            try
            {
                Call(host, "SetUnlockEnabled", true); Main.GameZoomTarget = 1.23f;
                var plus = point("ViewZoomPlus"); sample(plus, true, true, true, true, font);
                Near(Main.GameZoomTarget, 1.28f, "single plus five percentage points from nonmultiple");
                time = 349; sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, 1.28f, "350 ms threshold");
                time = 350; sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, 1.33f, "first repeat");
                time = 420; sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, 1.38f, "70 ms repeat");
                time = 5000; sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, 1.43f, "stall issues just one repeat");
                sample(plus, false, false, true, true, font); Near(Main.GameZoomTarget, 1.43f, "release never double dispatches");
                Require((bool)Get(control, "ConsumeLeft"), "release consumed");
                sample(plus, true, true, true, true, font); float accepted = Main.GameZoomTarget;
                sample(Vector2.Zero, true, false, true, true, font); time += 900;
                sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, accepted, "leaving button ends repeat without changing direction");
                sample(plus, false, false, true, true, font);
                sample(plus, true, true, true, true, font); accepted = Main.GameZoomTarget;
                sample(plus, false, false, false, false, font); time += 900;
                sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, accepted, "focus return cannot resume held gesture");
                Require((bool)Get(control, "ConsumeLeft"), "unfocused synthetic release keeps tail"); sample(plus, false, false, true, true, font);
                sample(plus, true, true, true, true, font); accepted = Main.GameZoomTarget;
                sample(plus, true, false, true, true, new object()); time += 900;
                sample(plus, true, false, true, true, font); Near(Main.GameZoomTarget, accepted, "font generation retires capture"); sample(plus, false, false, true, true, font);
                foreach (string reason in new[] { "page", "close", "modal", "escape", "matrix", "skin" })
                {
                    Call(state, "Navigate", 2); Call(state, "RestoreVisible");
                    sample(plus, true, true, true, true, font); accepted = Main.GameZoomTarget;
                    if (reason == "page") Call(state, "Navigate", 9);
                    if (reason == "close") Call(state, "Close");
                    Call(control, "Process", reason != "modal", true, true, false, reason == "escape", plus.X, plus.Y,
                        reason == "matrix" ? Matrix.CreateScale(1.5f) : Matrix.Identity, font, reason == "skin" ? 1 : 0);
                    time += 900;
                    sample(plus, true, false, true, true, font);
                    Near(Main.GameZoomTarget, accepted, reason + " ends capture without rollback/repeat");
                    Require(!(bool)Get(control, "Captured") && (bool)Get(control, "ConsumeLeft"), reason + " retains physical tail");
                    sample(plus, false, false, true, true, font);
                }
                Call(state, "Navigate", 2); Call(state, "RestoreVisible");
                Call(host, "SetUnlockEnabled", false); Main.GameZoomTarget = .75f;
                var track = Element(layout, "ViewZoomTrack"); var target = Point(state, track);
                float width = (float)Get(Get(track, "Rect"), "Width"); target.X -= width / 2;
                sample(target, true, true, true, true, font); Near(Main.GameZoomTarget, .75f, "OFF low red refuses outward drag");
                target.X += width * .3f; sample(target, true, false, true, true, font); Near(Main.GameZoomTarget, 1f, "same drag reaches blue endpoint");
                target.X -= width * .3f; sample(target, true, false, true, true, font); Near(Main.GameZoomTarget, 1f, "same drag cannot retreat to old red");
                sample(target, false, false, true, true, font);
                Main.GameZoomTarget = 2.5f; Call(host, "SetUnlockEnabled", false); Near(Main.GameZoomTarget, 2.5f, "switch alone retains target");
                bool oldMenu = Main.gameMenu; Main.gameMenu = true;
                try { Require(!(bool)Call(host, "TrySetZoomTarget", 1f) && Main.GameZoomTarget == 2.5f, "no world cannot write"); }
                finally { Main.gameMenu = oldMenu; }
            }
            finally { Call(control, "Cancel"); Main.GameZoomTarget = saved; Call(host, "SetUnlockEnabled", enabled); }
            Console.WriteLine("PASS ViewZoomControlCpu: latest-target OFF table, finite rejection, dynamic/log mapping, real Host writes, deterministic 350/70ms and stall, focus/font/button-exit tails. No native world Draw or physical input claimed.");
        }
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            Console.WriteLine("ZOOM CONTROL font supports unicode-minus=" + graphics.Font.IsCharacterSupported('−') + " ascii-minus=" + graphics.Font.IsCharacterSupported('-'));
            Require(graphics.Font.IsCharacterSupported('-'), "original font supports visible ASCII minus");
            object host = Get(context, "View"), shell = Get(context, "Shell"), state = Get(shell, "State"), renderer = Get(shell, "renderer");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
            float saved = Main.GameZoomTarget; bool enabled = (bool)Get(host, "UnlockEnabled");
            try
            {
                foreach (float scale in new[] { .5f, 1f, 1.5f })
                {
                    Call(shell, "CloseAndSubmitPosition");
                    NativeToolsUiChecks.Prepare(context, 2, 960, 640, scale);
                    typeof(Main).GetField("_uiScaleUsed", Flags).SetValue(null, scale);
                    typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.CreateScale(scale));
                    Call(host, "SetUnlockEnabled", true); Main.GameZoomTarget = 1.25f;
                    NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                    object layout = Get(state, "Layout"); Call(state, "ScrollTo", (float)Get(Get(Element(layout, "ViewZoomTrack"), "Rect"), "Y") - 20);
                    NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                    var ordered = new[] { "ViewZoomValue", "ViewZoomTrack", "ViewZoomMinus", "ViewZoomPlus", "EnableViewUnlock" }.Select(c => Get(Element(layout, c), "Rect")).ToArray();
                    Require(ordered.All(cell => (float)Get(cell, "Y") == (float)Get(ordered[0], "Y")), "fixed percentage before track and right-hand minus/plus stay in original single row");
                    for (int i = 1; i < ordered.Length; i++) Require((float)Get(ordered[i - 1], "Right") <= (float)Get(ordered[i], "X"), "single-row controls never overlap");
                    Vector2 plus = Point(state, Element(layout, "ViewZoomPlus")) * scale;
                    NativeToolsUiChecks.Click(context, plus); Near(Main.GameZoomTarget, 1.3f, "real Shell plus is exactly one step");
                    Vector2 minus = Point(state, Element(layout, "ViewZoomMinus")) * scale;
                    NativeToolsUiChecks.Click(context, minus); Near(Main.GameZoomTarget, 1.25f, "real Shell minus is exactly one step");
                    if (scale == 1f) PausedRepeat(context, shell, plus);
                    Main.GameZoomTarget = 10f; NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                    Require((string)Get(renderer, "zoomText") == "1000%", "current host target rendered without forced or quantized multiplier");
                    int measures = (int)Get(renderer, "ZoomMeasurements"), generation = (int)Get(layout, "Generation"), fixedCount = ((IDictionary)Get(layout, "textSizes")).Count;
                    for (int i = 0; i < 40; i++) NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                    Require(measures == (int)Get(renderer, "ZoomMeasurements") && generation == (int)Get(layout, "Generation") && fixedCount == ((IDictionary)Get(layout, "textSizes")).Count, "stable target reuses one label and geometry");
                    graphics.Image(Path.Combine(output, "view-zoom-" + (int)(scale * 100) + "-1000.png"), () => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
                    PixelThickness(graphics, shell, state, scale);
                    GlyphOnlyButtons(graphics, shell, state, scale);
                    Call(host, "SetUnlockEnabled", false); Near(Main.GameZoomTarget, 10f, "OFF keeps high red in real F5");
                    graphics.Image(Path.Combine(output, "view-zoom-" + (int)(scale * 100) + "-retained.png"), () => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
                    Underline(graphics, shell, state, "DisableViewUnlock", Color.IndianRed, scale);
                    // The single live percentage replaces itself for native or
                    // settings writes as well; no cached target is written back.
                    Main.GameZoomTarget = 1.275f; NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                    Require((string)Get(renderer, "zoomText") == "127.5%", "external/native target change updates percentage");
                    object track = Element(layout, "ViewZoomTrack"); object r = Get(track, "Rect");
                    var start = Point(state, track); start.X -= (float)Get(r, "Width") * .45f;
                    var end = start; end.X += (float)Get(r, "Width");
                    NativeToolsUiChecks.UiFrame(context, start * scale, true); Near(Main.GameZoomTarget, 1f, "OFF track click is clipped to blue");
                    NativeToolsUiChecks.UiFrame(context, end * scale, true); Near(Main.GameZoomTarget, 2f, "OFF same drag reaches upper blue");
                    var outside = new Vector2(950, 630); NativeToolsUiChecks.UiFrame(context, outside, true);
                    NativeToolsUiChecks.UiFrame(context, outside, false);
                    Require((bool)Get(Get(shell, "ViewZoom"), "ConsumeLeft") && !Main.LocalPlayer.controlUseItem, "outside release remains F5-owned");
                    // Rebuild and F5 close retire the gesture before it can
                    // produce an additional sample against a different owner.
                    Call(host, "SetUnlockEnabled", true); Main.GameZoomTarget = 1.25f;
                    NativeToolsUiChecks.UiFrame(context, plus, false); NativeToolsUiChecks.UiFrame(context, plus, true);
                    float accepted = Main.GameZoomTarget; NativeToolsUiChecks.UiFrame(context, plus, true, Keys.F5);
                    NativeToolsUiChecks.UiFrame(context, plus, false);
                    Near(Main.GameZoomTarget, accepted, "closing F5 ends repeat and consumes release");
                }
            }
            finally { Call(shell, "CloseAndSubmitPosition"); Main.GameZoomTarget = saved; Call(host, "SetUnlockEnabled", enabled); }
            Console.WriteLine("PASS ViewZoomControlVisual: actual F5 Shell gestures, actual XNB GPU row at 50/100/150%, exact two-pixel red/blue rails, fixed 1000% slot, external target sync, OFF retain, outside/close tails and stable cache. World/aim render propagation and physical Windows input are separate.");
        }
        private static void PixelThickness(ProbeGraphics graphics, object shell, object state, float scale)
        {
            var track = Element(Get(state, "Layout"), "ViewZoomTrack"); var r = Get(track, "Rect"); var center = Point(state, track);
            float left = center.X - (float)Get(r, "Width") / 2, width = (float)Get(r, "Width");
            var pixels = graphics.Pixels(() => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
            foreach (var part in new[] { new { Position = .15f, Color = Color.IndianRed }, new { Position = .5f, Color = Color.LightSkyBlue }, new { Position = .85f, Color = Color.IndianRed } })
            {
                int x = (int)Math.Round((left + width * part.Position) * scale), y = (int)Math.Round(center.Y * scale);
                int count = Enumerable.Range(Math.Max(0, y - 8), 16).Count(py => pixels[py * 960 + x] == part.Color);
                Require(count == 2, "rail is exactly two device pixels at " + scale + " segment " + part.Position + " got " + count);
            }
            Underline(graphics, shell, state, "EnableViewUnlock", Color.LightGreen, scale);
            Console.WriteLine("ZOOM CONTROL track-width logical=" + width + " physical=" + width * scale + " ui-scale=" + scale);
        }
        private static void GlyphOnlyButtons(ProbeGraphics graphics, object shell, object state, float scale)
        {
            object layout = Get(state, "Layout"), track = Element(layout, "ViewZoomTrack");
            var reference = Point(state, track); reference.Y -= (float)Get(Get(track, "Rect"), "Height") / 2 - 2;
            var pixels = graphics.Pixels(() => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
            var background = pixels[(int)(reference.Y * scale) * 960 + (int)(reference.X * scale)];
            foreach (string command in new[] { "ViewZoomMinus", "ViewZoomPlus" })
            {
                object element = Element(layout, command); var point = Point(state, element);
                point.Y -= (float)Get(Get(element, "Rect"), "Height") / 2 - 2;
                Require(pixels[(int)(point.Y * scale) * 960 + (int)(point.X * scale)] == background,
                    "glyph-only right-hand control has no background/frame at " + scale + " " + command);
            }
        }
        private static void PausedRepeat(object context, object shell, Vector2 plus)
        {
            object control = Get(shell, "ViewZoom"); var originalClock = Get(control, "clock"); bool paused = Main.gamePaused;
            double time = 0; Set(control, "clock", (Func<double>)(() => time)); Main.gamePaused = true;
            try
            {
                Main.GameZoomTarget = 1.25f;
                NativeToolsUiChecks.UiFrame(context, plus, false); NativeToolsUiChecks.UiFrame(context, plus, true);
                Near(Main.GameZoomTarget, 1.3f, "paused shell immediate step");
                time = 349; NativeToolsUiChecks.UiFrame(context, plus, true); Near(Main.GameZoomTarget, 1.3f, "paused shell no early repeat");
                time = 350; NativeToolsUiChecks.UiFrame(context, plus, true); Near(Main.GameZoomTarget, 1.35f, "paused shell repeats at threshold");
                time = 2000; NativeToolsUiChecks.UiFrame(context, plus, true); Near(Main.GameZoomTarget, 1.4f, "paused shell stall gives one step");
                NativeToolsUiChecks.UiFrame(context, plus, false); Near(Main.GameZoomTarget, 1.4f, "paused shell release never adds a step");
            }
            finally { Main.gamePaused = paused; Set(control, "clock", originalClock); }
        }
        private static void Underline(ProbeGraphics graphics, object shell, object state, string command, Color color, float scale)
        {
            object layout = Get(state, "Layout"), view = Get(layout, "Viewport");
            var method = layout.GetType().GetMethods(Flags).Single(m => m.Name == "ButtonUnderline" && m.GetParameters().Length == 4);
            var line = method.Invoke(null, new object[] { Element(layout, command), Main.UIScaleMatrix,
                (float)Get(state, "X") + (float)Get(view, "X"), (float)Get(state, "Y") + (float)Get(view, "Y") - (float)Get(state, "Scroll") });
            var pixels = graphics.Pixels(() => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
            int x = (int)Math.Round(((float)Get(line, "X") + (float)Get(line, "Width") / 2) * scale);
            int y = (int)Math.Round((float)Get(line, "Y") * scale);
            int count = Enumerable.Range(Math.Max(0, y - 3), 8).Count(py => pixels[py * 960 + x] == color);
            Require(count == 2, "selected switch underline retains exactly two physical rows at " + scale);
        }
        private static T Measure<T>(string text) { return (T)Activator.CreateInstance(typeof(T), Flags, null, new object[] { text.Length * 14f, 24f, 0f, 0f }, null); }
        private static object Element(object layout, string command) { return ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == command); }
        private static Vector2 Point(object state, object element)
        {
            object r = Get(element, "Rect"), view = Get(Get(state, "Layout"), "Viewport");
            return new Vector2((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(r, "X") + (float)Get(r, "Width") / 2,
                (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(r, "Y") - (float)Get(state, "Scroll") + (float)Get(r, "Height") / 2);
        }
        private static void Near(float actual, float expected, string reason) { Require(Math.Abs(actual - expected) < .00005f, reason + ": " + actual + " != " + expected); }
    }
}
