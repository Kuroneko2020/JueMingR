using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Only the existing MaterialsVisual consumer runs this resource check.
    // Pixel readback stays in this test; production borrows native item frames.
    internal static class NativeBlueprintCaptureMaterialIconChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static object auditedSurface;
        private static int itemButtonCalls;
        internal static void Capture(object context, ProbeGraphics graphics)
        {
            var ui = Get(Get(context, "Shell"), "BlueprintUi");
            Check(Get(ui, "captureMaterials"), Get(ui, "materialRenderer"), Get(ui, "materialClip"), graphics);
            Console.WriteLine("PASS capture material native icons: actual exact two-wall save grid calls no ItemButton, removes proven visible old-skin pixels, and retains native visible-art centres/count one at each icon's lower right; real name hover follows separately.");
        }
        internal static void Values(object context, ProbeGraphics graphics, string output)
        {
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var assembly = shell.GetType().Assembly;
            var layoutType = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout", true);
            var layout = Activator.CreateInstance(layoutType, true);
            var renderer = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsRenderer", true), true);
            var rectType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            var measureType = layoutType.GetMethod("Prepare", F).GetParameters()[3].ParameterType;
            var measure = Delegate.CreateDelegate(measureType, Get(state, "Layout"), Get(state, "Layout").GetType().GetMethod("DynamicTextSize", F));
            var bill = new BlueprintMaterialSnapshot(101, BlueprintMaterialStatus.Exact, new[] {
                new BlueprintMaterialRow(2, 1), new BlueprintMaterialRow(3, 99), new BlueprintMaterialRow(9, 4000000000),
                new BlueprintMaterialRow(Terraria.ID.ItemID.StoneWall, long.MaxValue - 4000000100) });
            graphics.LoadItemTextures(bill.Rows.Select(row => row.ItemType));
            float oldScale = Main.UIScaleWanted; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            try
            {
                Set(layout, "ShowNames", false);
                foreach (float scale in new[] { 1f, 1.2f })
                {
                    Main.screenWidth = 1280; Main.screenHeight = 900; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                    Require(Main.UIScale == scale && Main.UIScaleMatrix.M11 == scale && Main.UIScaleMatrix.M22 == scale,
                        "the capture value drawing uses the actual legal native 100/120 percent matrix");
                    Require((bool)Call(renderer, "Refresh"), "the native capture font and item surface are available");
                    Call(layout, "Prepare", bill, false, 440f, measure, (Func<int, string>)Lang.GetItemNameValue, 1L, (int)Get(renderer, "Generation"), 0f);
                    var clip = Activator.CreateInstance(rectType, F, null, new object[] { 40f, 40f, 440f, 260f }, null);
                    Call(layout, "Project", 40f, 40f, clip); Call(renderer, "Prepare", layout, scale == 1 ? 1 : 2);
                    long loads = (long)Get(renderer, "IconLoads"), sets = (long)Get(renderer, "VisibleSetBuilds"), poses = (long)Get(renderer, "CaptureIconProjects");
                    for (int i = 0; i < 120; i++) Call(renderer, "Prepare", layout, scale == 1 ? 1 : 2);
                    Require(loads == (long)Get(renderer, "IconLoads") && sets == (long)Get(renderer, "VisibleSetBuilds") && poses == (long)Get(renderer, "CaptureIconProjects"),
                        "120 stable capture preparations reload no borrowed icons, visible sets or native-frame/count poses");
                    Check(layout, renderer, clip, graphics);
                    foreach (var row in Objects(Get(layout, "Visible")))
                    {
                        int id = (int)Get(Get(row, "Cell"), "ItemType");
                        string number = string.Concat(Objects(Get(row, "Glyphs")).Select(g => (string)Get(Get(g, "Element"), "Text")));
                        Require(number == bill.Rows.Single(r => r.ItemType == id).Required.ToString(CultureInfo.InvariantCulture) &&
                            Objects(Get(row, "Glyphs")).All(g => (float)Get(Get(g, "Element"), "TextScale") == .65f),
                            "capture quantities preserve one, ordinary counts and complete long integers at the original readable text scale");
                    }
                    graphics.Image(Path.Combine(output, "blueprint-capture-material-values-" + scale.ToString(CultureInfo.InvariantCulture) + ".png"),
                        () => Call(renderer, "DrawContent", layout, Main.UIScaleMatrix, clip), Main.UIScaleMatrix, 1280, 900);
                    Console.WriteLine("CAPTURE ICON VIEW actual=" + Main.UIScale.ToString("R", CultureInfo.InvariantCulture) + " matrix=" + Main.UIScaleMatrix.M11.ToString("R", CultureInfo.InvariantCulture) + "/" + Main.UIScaleMatrix.M22.ToString("R", CultureInfo.InvariantCulture));
                }
                Console.WriteLine("PASS capture material values: no visible slot backing, native-frame art, lower-right ordinary quantities and complete wrapped long integers at actual 100/120 percent; stable 120-frame preparation costs.");
            }
            finally { ((IDisposable)renderer).Dispose(); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = oldScale; }
        }
        private static void Check(object layout, object renderer, object clip, ProbeGraphics graphics)
        {
            var surface = Get(renderer, "surface");
            var audit = new Harmony("JueMingR.Tests.CaptureMaterialBacking");
            auditedSurface = surface; itemButtonCalls = 0;
            audit.Patch(AccessTools.Method(surface.GetType(), "ItemButton"), prefix: new HarmonyMethod(typeof(NativeBlueprintCaptureMaterialIconChecks), nameof(BeforeItemButton)));
            Color[] pixels;
            try { pixels = graphics.Pixels(() => Call(renderer, "DrawContent", layout, Main.UIScaleMatrix, clip), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight); }
            finally { auditedSurface = null; audit.UnpatchAll(audit.Id); }
            Require(itemButtonCalls == 0, "the actual save-grid GPU draw calls no ItemButton skin consumer");
            // Draw only the original backing on a separate reference canvas.
            // A positive reference pixel proves the zero sample below is inside
            // that skin, rather than outside its two-unit inset as a corner is.
            var oldBacking = graphics.Pixels(() => Call(surface, "Pass", Main.UIScaleMatrix, clip, (Action)(() =>
            { foreach (var row in Objects(Get(layout, "Visible"))) Call(surface, "ItemButton", Get(row, "Rect"), true, false); })),
                Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
            var poses = (IDictionary)Get(renderer, "captureIcons");
            foreach (var row in Objects(Get(layout, "Visible")))
            {
                var rect = Get(row, "Rect"); var icon = Get(row, "Icon"); var cell = Get(row, "Cell"); int id = (int)Get(cell, "ItemType");
                var pose = poses[id]; var texture = (Texture2D)Get(pose, "Texture"); var frame = (Rectangle)Get(pose, "Frame"); float scale = (float)Get(pose, "Scale");
                // Independently inspect actual native alpha, not just the
                // enclosing slot/frame rectangles. No generated replacement art.
                var source = new Color[texture.Width * texture.Height]; texture.GetData(source);
                int left = frame.Right, top = frame.Bottom, right = -1, bottom = -1;
                for (int y = frame.Y; y < frame.Bottom; y++) for (int x = frame.X; x < frame.Right; x++)
                    if (source[y * texture.Width + x].A != 0) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
                Require(right >= left && bottom >= top, "the actual borrowed native capture item contains visible art");
                float artLeft = X(icon) + (left - frame.X) * scale, artTop = Y(icon) + (top - frame.Y) * scale;
                float artRight = X(icon) + (right + 1 - frame.X) * scale, artBottom = Y(icon) + (bottom + 1 - frame.Y) * scale;
                Require(W(rect) <= 44 && W(icon) <= 32 && H(icon) <= 32 &&
                    Math.Abs((artLeft + artRight) / 2 - (X(rect) + W(rect) / 2)) <= 1.01f &&
                    Math.Abs((artTop + artBottom) / 2 - (Y(rect) + 22)) <= 1.01f,
                    "actual native nontransparent art, including small wall/block frames, is visually centred on the compact inventory baseline");
                var glyphs = Objects(Get(row, "Glyphs"));
                if (glyphs.Length == 1)
                {
                    var count = Get(Get(glyphs[0], "Element"), "Rect");
                    Require(Math.Abs(X(count) + W(count) - artRight) <= 4.01f && Math.Abs(Y(count) + H(count) - artBottom) <= 4.01f,
                        "ordinary quantity ink is attached to the actual icon's lower-right art rather than the old cell footer");
                }
                else Require(glyphs.All(g => Y(Get(Get(g, "Element"), "Rect")) >= artBottom + 2),
                    "long decimal quantities continue below the icon instead of being clipped, shrunk or entering another slot");
                Require(glyphs.All(g => X(Get(Get(g, "Element"), "Rect")) >= X(rect) + 1 &&
                    X(Get(Get(g, "Element"), "Rect")) + W(Get(Get(g, "Element"), "Rect")) <= X(rect) + W(rect) - 1 &&
                    Y(Get(Get(g, "Element"), "Rect")) + H(Get(Get(g, "Element"), "Rect")) <= Y(rect) + H(rect) - 1),
                    "each full quantity remains inside its own invisible slot");
                int px = (int)((X(rect) + 5) * Main.UIScale), py = (int)((Y(rect) + 22) * Main.UIScale);
                Require(oldBacking[py * Main.screenWidth + px].A > 0 && pixels[py * Main.screenWidth + px].A == 0,
                    "a proven visible old-skin location, clear of native item art and quantities, becomes transparent in the actual save-grid draw");
                Console.WriteLine("CAPTURE BACKING item=" + id + " ItemButtonCalls=" + itemButtonCalls + " referenceAlpha=" + oldBacking[py * Main.screenWidth + px].A + " actualAlpha=" + pixels[py * Main.screenWidth + px].A);
                Console.WriteLine("CAPTURE ART item=" + id + " frame=" + frame + " alpha=" + left + "," + top + ".." + right + "," + bottom +
                    " visible=" + artLeft.ToString("R", CultureInfo.InvariantCulture) + "," + artTop.ToString("R", CultureInfo.InvariantCulture) + ".." +
                    artRight.ToString("R", CultureInfo.InvariantCulture) + "," + artBottom.ToString("R", CultureInfo.InvariantCulture) + " countLines=" + glyphs.Length);
            }
        }
        private static void BeforeItemButton(object __instance)
        { if (ReferenceEquals(__instance, auditedSurface)) itemButtonCalls++; }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static float X(object value) { return (float)Get(value, "X"); }
        private static float Y(object value) { return (float)Get(value, "Y"); }
        private static float W(object value) { return (float)Get(value, "Width"); }
        private static float H(object value) { return (float)Get(value, "Height"); }
    }
}
