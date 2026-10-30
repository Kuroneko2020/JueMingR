using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual production CPU layout/metrics and UI-coordinate adapter. The font
    // uses the established ReLogic CPU fixture; no GPU draw or OS click occurs.
    internal static class NativeBlueprintUiChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void SetScale(float value)
        {
            // Headless CPU scope replaces only the native presentation sample;
            // UIScale's real setter needs a GraphicsDevice and is not exercised.
            typeof(Main).GetField("_uiScaleUsed", F).SetValue(null, value);
            typeof(Main).GetField("_uiScaleMatrix", F).SetValue(null, Matrix.CreateScale(value, value, 1f));
        }
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var toolbar = Get(host, "Toolbar");
            var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer");
            var assembly = toolbar.GetType().Assembly;
            var oldFont = FontAssets.MouseText; object oldToolbarFont = GetOptional(toolbar, "font");
            var oldZoom = Main.GameViewMatrix.Zoom; float oldScale = Main.UIScale;
            var oldMouse = PlayerInput.MouseInfo; var oldRawScale = PlayerInput.RawMouseScale;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            try
            {
                FiniteCostChecks.SetCpuFont(10);
                Call(renderer, "RefreshResources");
                object font = Get(renderer, "FontIdentity"); Set(toolbar, "font", font);
                var layout = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintToolbarLayout", true), true);
                Type measureType = layout.GetType().GetMethod("Ensure", F).GetParameters()[4].ParameterType;
                int measurements = 0;
                var productionMeasure = Delegate.CreateDelegate(measureType, toolbar, toolbar.GetType().GetMethod("Measure", F));
                Delegate measure = Counted(productionMeasure, () => measurements++);
                foreach (var screen in new[] { new Vector2(960, 540), new Vector2(1280, 720), new Vector2(1920, 1080) })
                foreach (float ui in new[] { 1f, 1.5f, 2f })
                foreach (bool active in new[] { false, true })
                {
                    Main.screenWidth = (int)screen.X; Main.screenHeight = (int)screen.Y; NativeBlueprintUiChecks.SetScale(ui);
                    PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.RawMouseScale = Vector2.One;
                    Require((bool)Call(layout, "Ensure", screen.X, screen.Y, ui, font, measure, active), "toolbar fits supported viewport/UI scale");
                    var buttons = ((IEnumerable)Get(layout, "Buttons")).Cast<object>().ToArray();
                    Require(buttons.Length == (active ? 2 : 3) && buttons.Select(b => (string)Get(b, "Text")).SequenceEqual(active ? new[] { "保存蓝图", "退出采集" } : new[] { "采集蓝图", "打开蓝图库", "材料" }), "capture publishes exactly Save/Exit; only world mode includes Materials");
                    Require((int)Get(layout, "MaterialsIndex") == (active ? -1 : 2) && Call(layout, "Action", -1).ToString() == "None", "missing material action and no-hit sentinel have distinct safe identities");
                    Require((bool)Call(layout, "Available", 0) && (bool)Call(layout, "Available", 1), "capture/save and library/exit share the actual owner command paths");
                    Require((string)Call(layout, "Hint", 0) == (active ? "点击保存键保存世界上已有的蓝图选区。" : "点击开始键执行选区，反选取消选区。") &&
                        (string)Call(layout, "Hint", 1) == (active ? "退出选区，并清空已有选区" : null), "disabled save keeps its approved hint; disabled library has no hint");
                    object panel = Get(layout, "Panel");
                    Require(Near(X(panel) + W(panel) / 2, screen.X / ui / 2) && X(panel) >= 8 && X(panel) + W(panel) <= screen.X / ui - 8 &&
                        Y(panel) > screen.Y / ui / 2 && Y(panel) + H(panel) <= screen.Y / ui - 8, "toolbar is entirely visible at the bottom and horizontally centered");
                    int generation = (int)Get(layout, "Generation"), count = measurements;
                    foreach (float zoom in new[] { .3f, 1f, 10f })
                    {
                        Main.GameViewMatrix.Zoom = new Vector2(zoom);
                        for (int repeat = 0; repeat < 12; repeat++)
                            Require((bool)Call(layout, "Ensure", screen.X, screen.Y, ui, font, measure, active), "stable toolbar preparation succeeds");
                        Require(generation == (int)Get(layout, "Generation") && measurements == count, "world zoom 30%-1000% and stable UI frames do not rebuild or measure toolbar geometry");
                        // Independent physical pixels feed the same adapter as
                        // toolbar input, never the world zoom inverse.
                        var rect = Get(buttons[active ? 1 : 0], "Rect");
                        float cx = X(panel) + X(rect) + W(rect) / 2, cy = Y(panel) + Y(rect) + H(rect) / 2;
                        PlayerInput.MouseInfo = new MouseState((int)Math.Round(cx * ui), (int)Math.Round(cy * ui), 0,
                            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                        var pointer = (Vector2)Call(toolbar, "Pointer");
                        Require((int)Call(layout, "Hit", pointer.X, pointer.Y) == (active ? 1 : 0), "physical UI button hit stays aligned across world zoom and UI scale");
                    }
                    HintGeometry(assembly, layout, panel, screen.X / ui, font, measure, () => measurements);
                }
                foreach (int operation in new[] { 1, 2, 3, 4 })
                {
                    Require((bool)Call(layout, "EnsureProjection", 1920f, 1080f, 1f, font, measure, false, operation), "operation toolbar fits real measured geometry");
                    var actionButtons = ((IEnumerable)Get(layout, "Buttons")).Cast<object>().ToArray();
                    var actions = Enumerable.Range(0, actionButtons.Length).ToDictionary(i => Call(layout, "Action", i).ToString(), i => (string)Get(actionButtons[i], "Text"));
                    Require(actions["Move"] == (operation == 3 ? "取消移动" : "移动蓝图") && actions["Remove"] == (operation == 4 ? "取消移除" : "移除蓝图"),
                        "move/remove slots only cancel their own actual operation");
                    Require(actions["Mirror"] == "水平镜像" && actions["Trim"] == "局部修剪" && actions["Materials"] == "材料" &&
                        (operation == 2 || operation == 4 ? actions.Count == 8 && actions[operation == 2 ? "CancelPlace" : "RemoveAll"] == (operation == 2 ? "取消摆放" : "移除所有") : actions.Count == 7),
                        "world modes add mirror/trim while only place/remove own their exact extra action");
                    int modeGeneration = (int)Get(layout, "Generation");
                    for (int j = 0; j < 12; j++) Call(layout, "EnsureProjection", 1920f, 1080f, 1f, font, measure, false, operation);
                    Require(modeGeneration == (int)Get(layout, "Generation") && !(bool)Call(layout, "MatchesProjection", 1920f, 1080f, 1f, font, false, operation == 4 ? 1 : operation + 1),
                        "stable source reuses geometry and a different source invalidates release targets");
                }
                foreach (int operation in new[] { 0, 1, 2, 3, 4 })
                {
                    int count = measurements;
                    Call(layout, "EnsureProjection", 1920f, 1080f, 1f, font, measure, true, operation);
                    Require(Objects(Get(layout, "Buttons")).Select(b => (string)Get(b, "Text")).SequenceEqual(new[] { "保存蓝图", "退出采集" }) &&
                        (int)Get(layout, "MaterialsIndex") == -1 && measurements - count == 2,
                        "all residual projection modes measure and publish only their two capture actions");
                }
                FractionalWidths(assembly);
                Require((Color)toolbar.GetType().GetField("HintColor", F).GetValue(null) == new Color(184,134,11), "toolbar's actual text draw color is dark goldenrod #B8860B");
                int before = (int)Get(layout, "Generation");
                Require(!(bool)Call(layout, "Matches", 1920f, 1080f, 2f, new object(), true), "font identity changes retire old hit geometry");
                Require(!(bool)Call(layout, "Ensure", 1920f, 1080f, 0f, font, measure, true) && before == (int)Get(layout, "Generation"), "invalid UI scale cannot publish replacement geometry");
                F5Page(assembly, renderer, font);
                var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
                Require(registry.Find("blueprint.capture").CanConfigure && registry.Find("blueprint.save").CanConfigure && registry.Find("blueprint.toolbar").CanConfigure &&
                    !(bool)Call(Get(renderer, "BlueprintControls"), "Available", Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.F5.F5Command", true), "BlueprintSave")), "all three real binder targets remain editable while save execution is unavailable");
                Console.WriteLine("PASS Blueprint UI CPU: two capture actions versus world Materials identity, fractional-width single/wrapped geometry, 3 viewports x UI 100/150/200% x world 30/100/1000%, stable metrics, raw UI hit, centered gold hint, F5 invalidation/binder targets. GPU pixels/OS clicks/owner experience NOT_EXECUTED.");
            }
            finally
            {
                FontAssets.MouseText = oldFont; Set(toolbar, "font", oldToolbarFont);
                Call(renderer, "RefreshResources"); Main.GameViewMatrix.Zoom = oldZoom; SetScale(oldScale);
                Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions();
                PlayerInput.MouseInfo = oldMouse; PlayerInput.RawMouseScale = oldRawScale;
            }
        }

        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static void FractionalWidths(Assembly assembly)
        {
            var type = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintToolbarLayout", true);
            var layout = Activator.CreateInstance(type, true);
            var measureType = type.GetMethod("Ensure", F).GetParameters()[4].ParameterType;
            var sizeType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Size", true);
            Func<string, float, object> sizes = (text, scale) => Activator.CreateInstance(sizeType, F, null,
                new object[] { (text == "采集蓝图" ? 62f : text == "打开蓝图库" ? 64.0999984741211f : 45.19999694824219f) - 16, 14f, -2f, 3f }, null);
            var textParameter = Expression.Parameter(typeof(string)); var scaleParameter = Expression.Parameter(typeof(float));
            var measure = Expression.Lambda(measureType, Expression.Convert(Expression.Invoke(Expression.Constant(sizes), textParameter, scaleParameter), sizeType), textParameter, scaleParameter).Compile();
            object font = new object();
            Call(layout, "Ensure", 212f, 600f, 1f, font, measure, false);
            var buttons = Objects(Get(layout, "Buttons")); var panel = Get(layout, "Panel");
            Require(buttons.All(b => Near(Y(Get(b, "Rect")), 8)) && buttons.All(b => X(Get(b, "Rect")) >= 8 &&
                X(Get(b, "Rect")) + W(Get(b, "Rect")) <= W(panel) - 8 + .0001f),
                "the independent fractional-width counterexample stays one row when its measured total fits");
            foreach (var button in buttons)
            {
                var rect = Get(button, "Rect"); float x = X(panel) + X(rect) + W(rect) / 2, y = Y(panel) + Y(rect) + H(rect) / 2;
                Require((int)Call(layout, "Hit", x, y) == Array.IndexOf(buttons, button), "fractional toolbar hit consumes the same published button rectangle");
            }
            Call(layout, "Ensure", 210f, 600f, 1f, font, measure, false);
            Require(Objects(Get(layout, "Buttons")).Select(b => Y(Get(b, "Rect"))).Distinct().Count() == 2,
                "a genuinely narrower viewport wraps the same fractional-width action list");
        }

        private static void HintGeometry(Assembly assembly, object layout, object bar, float width, object font, Delegate measure, Func<int> count)
        {
            var hint = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5HintLayout", true), true);
            var rectType = bar.GetType();
            object bounds = Activator.CreateInstance(rectType, F, null, new object[] { 8f, 8f, width - 16, Y(bar) - 14 }, null);
            string text = (string)Call(layout, "Hint", 0);
            Call(hint, "Prepare", text, bar, bounds, font, measure);
            Require((bool)Get(hint, "Visible"), "approved start/save hint fits bounded space above bar");
            var lines=((IEnumerable)Get(hint,"Lines")).Cast<object>().ToArray();
            float contentHeight=Y(Get(lines.Last(),"Rect"))+H(Get(lines.Last(),"Rect"));
            Require(Y(bar)-contentHeight-2>=8 && lines.All(line=>W(Get(line,"Rect"))<=width-16), "native glyph wrapping fits text-only placement above the bar without panel padding");
            int before = count();
            for (int i = 0; i < 12; i++) Call(hint, "Prepare", text, bar, bounds, font, measure);
            Require(count() == before, "stable hover reuses common hint wrapping without glyph remeasurement");
        }

        private static void F5Page(Assembly assembly, object renderer, object font)
        {
            var layout = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Layout", true), true);
            Set(layout, "BlueprintAttached", true);
            Type measureType = layout.GetType().GetMethod("Ensure", F).GetParameters()[5].ParameterType;
            var measure = Delegate.CreateDelegate(measureType, renderer, renderer.GetType().GetMethod("Measure", F));
            foreach (bool mode in new[] { false, true })
            {
                Call(layout, "SetBlueprintMode", mode);
                Call(layout, "Ensure", 1280f, 720f, 1.5f, 6, font, measure);
                object[] elements = ((IEnumerable)Get(layout, "Elements")).Cast<object>().ToArray();
                Require(elements.Count(e => Get(e, "Kind").ToString() == "Panel") == 10, "blueprint F5 adds mirror and trim standard rows to its existing eight rows");
                var capture = elements.Single(e => GetOptional(e, "Text") as string == (mode ? "退出采集" : "采集蓝图"));
                Require((string)Get(Get(capture, "Description"), "Text") == (mode ? "退出选区，并清空已有选区" : "点击开始键执行选区，反选取消选区。"), "F5 capture row tracks the current owner mode");
                var save = elements.Single(e => GetOptional(e, "Text") as string == "保存采集");
                Require((string)Get(Get(save, "Description"), "Text") == "点击保存键保存世界上已有的蓝图选区。", "F5 save row keeps the exact approved description");
                Require(elements.Where(e => Get(e, "Kind").ToString() == "Hotkey").Select(e => (string)Get(e, "HotkeyTarget")).SequenceEqual(new[] { "blueprint.capture", "blueprint.save", "blueprint.library", "blueprint.move", "blueprint.remove", "blueprint.mirror", "blueprint.trim", "blueprint.toolbar", "blueprint.autobuild", "blueprint.materials" }), "all ten rows use stable common binder targets; start/exit still share one identity");
                Require(elements.Count(e => Get(e, "Command").ToString() == "BlueprintAutoBuildOn" || Get(e, "Command").ToString() == "BlueprintAutoBuildOff" || Get(e, "Command").ToString() == "BlueprintMaterials") == 3,
                    "new rows contain two idempotent construction buttons and one material opener, plus their two existing common key controls");
                var preference = elements.Single(e => GetOptional(e, "Text") as string == "手持凝胶显示快捷栏");
                Require(GetOptional(preference, "Description") == null, "gel-toolbar preference adds no hint");
                int generation = (int)Get(layout, "Generation"), measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 12; i++) { Call(layout, "SetBlueprintMode", mode); Call(layout, "Ensure", 1280f, 720f, 1.5f, 6, font, measure); }
                Require(generation == (int)Get(layout, "Generation") && measurements == (int)Get(layout, "MeasurementCount"), "stable capture mode does not reflow or measure the F5 page");
                Call(layout, "SetBlueprintMode", !mode);
                Require(!(bool)Call(layout, "Matches", 1280f, 720f, 1.5f, 6), "mode switch invalidates old row targets before release dispatch");
            }
            foreach (int operation in new[] { 1, 2, 0 })
            {
                Call(layout, "SetBlueprintOperation", operation);
                Require(!(bool)Call(layout, "Matches", 1280f, 720f, 1.5f, 6), "operation source invalidates cached F5 rows");
                Call(layout, "Ensure", 1280f, 720f, 1.5f, 6, font, measure);
                var elements = ((IEnumerable)Get(layout, "Elements")).Cast<object>().ToArray();
                Require(elements.Any(e => (string)GetOptional(e, "Text") == (operation == 1 ? "取消移动" : "移动蓝图")) &&
                    elements.Any(e => (string)GetOptional(e, "Text") == (operation == 2 ? "取消移除" : "移除蓝图")), "F5 measured rows use the real operation source");
            }
        }

        private static Delegate Counted(Delegate production, Action count)
        {
            var text = Expression.Parameter(typeof(string)); var scale = Expression.Parameter(typeof(float));
            return Expression.Lambda(production.GetType(), Expression.Block(Expression.Invoke(Expression.Constant(count)),
                Expression.Invoke(Expression.Constant(production, production.GetType()), text, scale)), text, scale).Compile();
        }
        private static float X(object r) { return (float)Get(r, "X"); }
        private static float Y(object r) { return (float)Get(r, "Y"); }
        private static float W(object r) { return (float)Get(r, "Width"); }
        private static float H(object r) { return (float)Get(r, "Height"); }
        private static bool Near(float a, float b) { return Math.Abs(a - b) < .01f; }
    }
}
