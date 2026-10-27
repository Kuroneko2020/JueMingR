using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JueMingR.Features.Blueprint;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Relations come from the readable-column and per-opening stability
    // contracts; the production metrics supply visible bounds and offsets.
    internal static class NativeBlueprintMaterialGeometryChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var type = Get(context, "Blueprint").GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout", true);
            var layout = Activator.CreateInstance(type, true);
            var measureType = type.GetMethod("Prepare", F).GetParameters()[3].ParameterType;
            var production = Delegate.CreateDelegate(measureType, Get(state, "Layout"), Get(state, "Layout").GetType().GetMethod("DynamicTextSize", F));
            var measured = new Dictionary<string, int>();
            Action<string> observe = value => { int count; measured.TryGetValue(value, out count); measured[value] = count + 1; };
            var textParameter = Expression.Parameter(typeof(string)); var scaleParameter = Expression.Parameter(typeof(float));
            var measure = Expression.Lambda(measureType, Expression.Block(Expression.Invoke(Expression.Constant(observe), textParameter),
                Expression.Invoke(Expression.Constant(production, measureType), textParameter, scaleParameter)), textParameter, scaleParameter).Compile();
            Func<int, string> name = id => id == 1 ? "石块" : id == 2 ? "木墙" : "木材";
            Func<BlueprintMaterialSnapshot, bool, float, float> prepare = (value, world, width) =>
                (float)Call(layout, "Prepare", value, world, width, measure, name, 1L, 1, 0f);
            var ordinary = new BlueprintMaterialSnapshot(1, BlueprintMaterialStatus.Exact,
                new[] { new BlueprintMaterialRow(1, 9, 4, true), new BlueprintMaterialRow(2, 3, 7, true) }, true);
            prepare(ordinary, true, 552);
            foreach (var cell in Objects(Get(layout, "cells")))
            {
                var row = Get(cell, "Rect"); var icon = Get(cell, "Icon"); var glyphs = Objects(Get(cell, "Glyphs"));
                float center = Y(row) + H(row) / 2;
                Require(H(row) <= 34 && Math.Abs(Y(icon) + H(icon) / 2 - center) < .01f,
                    "ordinary material rows use the 24-high icon and compact measured row padding");
                Require(glyphs.All(g => Math.Abs(Y(Get(Get(g, "Element"), "Rect")) + H(Get(Get(g, "Element"), "Rect")) / 2 - center) < .01f),
                    "ordinary name and all three numeric glyph bounds share the icon's visible center");
                var label = glyphs.Single(g => (bool)Get(g, "Name")); var labelRect = Get(Get(label, "Element"), "Rect");
                Require(X(labelRect) >= X(icon) + W(icon) + 5.9f && X(labelRect) + W(labelRect) <= (float)Get(layout, "RequiredX") - 7.9f,
                    "the complete direct item name follows its icon and cannot enter the required column");
                // Every text element preserves the real font's draw offset; the
                // renderer consumes it instead of centering by LineSpacing.
                Require(glyphs.All(g => Get(Get(g, "Element"), "TextSize") != null), "row glyphs retain their actual text metrics");
            }
            float budget = (float)Get(layout, "NumberWidth"), height = (float)Get(layout, "Height");
            var initialRow = Objects(Get(layout, "cells"))[0]; object measuredName = Get(initialRow, "NameLines"), measuredHeld = Get(initialRow, "HeldLines");
            int nameMeasures = measured["石块"], heldMeasures = measured["4"];
            for (int i = 0; i < 20; i++)
            {
                prepare(new BlueprintMaterialSnapshot(2 + i, BlueprintMaterialStatus.Exact,
                    new[] { new BlueprintMaterialRow(1, i % 2 == 0 ? 10 : 9, 4, true), new BlueprintMaterialRow(2, 3, 7, true) }, true), true, 552);
                Require((float)Get(layout, "NumberWidth") == budget && (float)Get(layout, "Height") == height,
                    "repeated 9/10 changes update values without breathing columns or row heights");
                var row = Objects(Get(layout, "cells"))[0];
                Require(ReferenceEquals(measuredName, Get(row, "NameLines")) && ReferenceEquals(measuredHeld, Get(row, "HeldLines")),
                    "the affected row reuses its measured name and unchanged held block while changed digits refresh");
                Require(measured["石块"] == nameMeasures && measured["4"] == heldMeasures,
                    "actual measurement calls for the unchanged name and held digits stay flat across numeric refreshes");
            }
            var huge = new BlueprintMaterialSnapshot(30, BlueprintMaterialStatus.Exact,
                new[] { new BlueprintMaterialRow(1, long.MaxValue - 3, 0, true), new BlueprintMaterialRow(2, 3, 7, true) }, true);
            prepare(huge, true, 320); float grown = (float)Get(layout, "Height"), grownBudget = (float)Get(layout, "NumberWidth");
            prepare(ordinary, true, 320);
            Require((float)Get(layout, "Height") == grown && (float)Get(layout, "NumberWidth") == grownBudget,
                "a wrapped long integer's same-ItemType row and number budget survive later digit decreases");
            Call(layout, "Clear");
            var staticBill = new BlueprintMaterialSnapshot(40, BlueprintMaterialStatus.Exact,
                new[] { new BlueprintMaterialRow(1, 1), new BlueprintMaterialRow(2, 4000000000), new BlueprintMaterialRow(3, long.MaxValue - 4000000001) });
            prepare(staticBill, false, 552);
            int columns = (int)Get(layout, "Columns");
            Require(columns == 2, "named static details choose readable columns from available name space");
            foreach (var cell in Objects(Get(layout, "cells")))
            {
                var icon = Get(cell, "Icon"); var glyphs = Objects(Get(cell, "Glyphs"));
                Require(string.Concat(glyphs.Where(g => (bool)Get(g, "Name")).Select(g => (string)Get(Get(g, "Element"), "Text"))) == (string)Get(cell, "Name"),
                    "every static detail name is directly drawn in full");
                Require(glyphs.Where(g => (bool)Get(g, "Name")).All(g => X(Get(Get(g, "Element"), "Rect")) >= X(icon) + W(icon) + 5.9f) &&
                    glyphs.Where(g => !(bool)Get(g, "Name")).All(g => X(Get(Get(g, "Element"), "Rect")) + W(Get(Get(g, "Element"), "Rect")) <= X(icon) + W(icon) + .01f),
                    "full static numbers remain in their icon group, separate from the right-hand name");
            }
            prepare(new BlueprintMaterialSnapshot(41, BlueprintMaterialStatus.Exact,
                new[] { new BlueprintMaterialRow(1, long.MaxValue - 2), new BlueprintMaterialRow(2, 1), new BlueprintMaterialRow(3, 1) }), false, 552);
            Require((int)Get(layout, "Columns") == columns, "static decimal digit changes never change grid column count");
            prepare(new BlueprintMaterialSnapshot(42, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(1, long.MaxValue) }), false, 552);
            Require(string.Concat(Objects(Get(Objects(Get(layout, "cells"))[0], "Glyphs")).Where(g => !(bool)Get(g, "Name")).Select(g => (string)Get(Get(g, "Element"), "Text"))) == long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "a legal single-item snapshot renders every digit of long.MaxValue");
            Set(layout, "ShowNames", false); prepare(staticBill, false, 552);
            Require(Objects(Get(layout, "cells")).All(c => Objects(Get(c, "Glyphs")).All(g => !(bool)Get(g, "Name"))) &&
                string.Concat(Objects(Get(Objects(Get(layout, "cells"))[0], "Glyphs")).Select(g => (string)Get(Get(g, "Element"), "Text"))) == "1",
                "the save workspace preserves its icon/count-only mode including explicit quantity one and available hover names");
            FrameCache(context);
            Console.WriteLine("PASS material geometry relations: visible-center compact rows, separate named/static count domains, 9/10 and long-digit stability, full static decimals, save-grid mode.");
        }
        private static void FrameCache(object context)
        {
            var host = Get(context, "Blueprint"); var state = Get(Get(context, "Shell"), "State");
            var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsPresentation", true);
            var presentation = Activator.CreateInstance(type, F, null, new[] { host, state }, null);
            try
            {
                // This exercises the production value-to-frame builder without
                // acquiring a renderer/device or claiming actual Prepare/input.
                Set(presentation, "shownInstances", Array.Empty<BlueprintMaterialInstance>());
                Func<long, BlueprintMaterialSnapshot> bill = held => new BlueprintMaterialSnapshot(50 + held, BlueprintMaterialStatus.Exact,
                    new[] { new BlueprintMaterialRow(1, 17, held, true), new BlueprintMaterialRow(2, 3, 7, true) }, true);
                Set(presentation, "snapshot", bill(9)); Call(presentation, "Build", 960f, 640f, false, false);
                var panel = Get(presentation, "Panel"); var body = Get(presentation, "Body");
                long placed = (long)Get(presentation, "FramePlacements"), headers = (long)Get(presentation, "HeaderMeasurements");
                for (int i = 0; i < 20; i++)
                {
                    Set(presentation, "snapshot", bill(i % 2 == 0 ? 10 : 9)); Call(presentation, "Build", 960f, 640f, true, true);
                    Require(Same(panel, Get(presentation, "Panel")) && Same(body, Get(presentation, "Body")) &&
                        (long)Get(presentation, "FramePlacements") == placed && (long)Get(presentation, "HeaderMeasurements") == headers,
                        "ordinary same-summary numeric updates reuse the measured header and proven outer frame without Place or geometry breathing");
                }
                Set(presentation, "snapshot", new BlueprintMaterialSnapshot(100, BlueprintMaterialStatus.Exact,
                    new[] { new BlueprintMaterialRow(1, 17, long.MaxValue, true), new BlueprintMaterialRow(2, 3, 7, true) }, true));
                Call(presentation, "Build", 960f, 640f, true, true);
                Require((long)Get(presentation, "FramePlacements") == placed + 1 && H(Get(presentation, "Panel")) > H(panel),
                    "a real wrapped numeric expansion performs one necessary content-driven frame placement");
                var grown = Get(presentation, "Panel"); placed = (long)Get(presentation, "FramePlacements");
                Set(presentation, "snapshot", bill(9)); Call(presentation, "Build", 960f, 640f, true, true);
                Require(Same(grown, Get(presentation, "Panel")) && (long)Get(presentation, "FramePlacements") == placed,
                    "shrinking digits retain the grown same-ItemType frame instead of breathing back");
                Console.WriteLine("PASS material CPU frame builder: 20 ordinary numeric updates add no header measurements/frame placements; wrapped expansion places once, digit shrink retains geometry. Actual Prepare/Draw/input NOT_EXECUTED by this check.");
            }
            finally { ((IDisposable)presentation).Dispose(); }
        }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static float X(object value) { return (float)Get(value, "X"); }
        private static float Y(object value) { return (float)Get(value, "Y"); }
        private static float W(object value) { return (float)Get(value, "Width"); }
        private static float H(object value) { return (float)Get(value, "Height"); }
        private static bool Same(object a, object b) { return X(a) == X(b) && Y(a) == Y(b) && W(a) == W(b) && H(a) == H(b); }
    }
}
