using System;
using System.Collections;
using System.Linq;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintMaterialNameChecks
    {
        internal static void Standalone(object context, ProbeGraphics graphics)
        {
            var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi"); var content = Get(popup, "Content");
            var row = Objects(Get(content, "Visible")).First(); var glyphs = Objects(Get(row, "Glyphs"));
            string name = (string)Get(Get(row, "Cell"), "Name");
            Require(string.Concat(glyphs.Where(g => (bool)Get(g, "Name")).Select(g => (string)Get(Get(g, "Element"), "Text"))) == name,
                "the standalone detail directly prepares the complete item name beside its icon");
            var icon = Get(row, "Icon"); var rect = Get(row, "Rect");
            var nameRect = Get(Get(glyphs.First(g => (bool)Get(g, "Name")), "Element"), "Rect");
            var number = Get(Get(glyphs.First(g => !(bool)Get(g, "Name")), "Element"), "Rect");
            foreach (var target in new[] { icon, nameRect, number, rect })
            {
                float x = X(target) + (ReferenceEquals(target, rect) ? W(target) - 2 : W(target) / 2), y = Y(target) + H(target) / 2;
                NativeBlueprintMaterialsUiChecks.Frame(shell, Get(context, "Input"), x, y, false);
                NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
                Require(!(bool)Get(Get(Get(popup, "renderer"), "hint"), "Visible"), "icon, direct name, quantity and row whitespace produce no item-name hint in the actual standalone Prepare/Draw chain");
            }
        }
        internal static void Capture(object context, ProbeGraphics graphics)
        {
            var shell = Get(context, "Shell"); var ui = Get(shell, "BlueprintUi"); var grid = Get(ui, "captureMaterials");
            var row = Objects(Get(grid, "Visible")).First();
            Require(Objects(Get(row, "Glyphs")).All(g => !(bool)Get(g, "Name")), "the save workspace's real material grid retains icon/count-only presentation");
            var rect = Get(row, "Icon");
            NativeBlueprintMaterialsUiChecks.Frame(shell, Get(context, "Input"), X(rect) + W(rect) / 2, Y(rect) + H(rect) / 2, false);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            var hint = Get(Get(ui, "materialRenderer"), "hint");
            Require((bool)Get(hint, "Visible") && string.Concat(Objects(Get(hint, "Lines")).Select(e => (string)Get(e, "Text"))) == (string)Get(Get(row, "Cell"), "Name"),
                "the actual save workspace still identifies its original icon through the full item-name hover");
        }
        internal static void PendingSummary(object context, ProbeGraphics graphics)
        {
            var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi"); var rect = Get(popup, "summaryRect");
            NativeBlueprintMaterialsUiChecks.Frame(shell, Get(context, "Input"), X(rect) + 12, Y(rect) + H(rect) / 2, false);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            var hint = Get(Get(popup, "renderer"), "hint");
            Require((bool)Get(hint, "Visible") && string.Concat(Objects(Get(hint, "Lines")).Select(e => (string)Get(e, "Text"))) == "正在核对材料",
                "removing item-name hints preserves the real Pending reason at the actual summary target");
        }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static float X(object value) { return (float)Get(value, "X"); }
        private static float Y(object value) { return (float)Get(value, "Y"); }
        private static float W(object value) { return (float)Get(value, "Width"); }
        private static float H(object value) { return (float)Get(value, "Height"); }
    }
}
