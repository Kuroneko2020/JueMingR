using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class F5LayoutChecks
    {
        internal static void Run()
        {
            CheckVisualRows();
            var layout = new F5Layout();
            var font = new object();
            layout.Ensure(1920, 1080, 1, 9, font, Measure);
            Equal(580, layout.Window.Width, "normal window width");
            Equal(740, layout.Window.Height, "normal window height");
            Equal(522, layout.Viewport.Width, "shared page viewport width");
            Equal(581, layout.Viewport.Height, "normal page viewport height");
            int generation = layout.Generation, measurements = layout.MeasurementCount;
            for (int i = 0; i < 100; i++) layout.Ensure(1920, 1080, 1, 9, font, Measure);
            Equal(generation, layout.Generation, "ordinary updates do not reflow");
            Equal(measurements, layout.MeasurementCount, "ordinary updates do not measure text");
            layout.Ensure(1920, 1080, 1, 9, new object(), Measure);
            Equal(generation, layout.Generation, "font replacement with identical metrics preserves layout");
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 18, 24, -3, 9));
            Equal(generation + 1, layout.Generation, "equal-size glyph offset change refreshes positioning");
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 20, 26));
            Equal(generation + 2, layout.Generation, "actual font metric change triggers one reflow");
            ValidateBounds(layout);
            layout.Ensure(1280, 720, 1.5f, 7, font, Measure);
            Equal(580, layout.Window.Width, "constrained window retains width");
            Equal(456, layout.Window.Height, "constrained viewport clips content only");
            Equal(297, layout.Viewport.Height, "constrained page viewport height");
            ValidateBounds(layout);
            for (int page = 0; page < 12; page++)
            { layout.Ensure(1280, 720, 1.5f, page, font, Measure); ValidateBounds(layout); }
            Console.WriteLine("PASS: Phase 0-U production layout geometry.");
        }

        private static void CheckVisualRows()
        {
            var layout = new F5Layout();
            var failures = new System.Collections.Generic.List<string>();
            for (int page = 0; page < 12; page++)
            {
                layout.Ensure(1920, 1080, 1, page, new object(), text => new F5Size(text.Length * 18, 32));
                F5Element panel = null;
                foreach (F5Element element in layout.Elements)
                {
                    if (element.Text != null && (element.Text.Contains("占位") || element.Text.Contains("未接入") ||
                        element.Text.Contains("请选择")))
                        failures.Add("development guidance remains on page " + page);
                    if (element.Kind == F5ElementKind.Panel) panel = element;
                    if (page != 9 || element.Kind != F5ElementKind.Button || panel == null) continue;
                    if (Math.Abs(element.Rect.Y + element.Rect.Height / 2 - panel.Rect.Y - panel.Rect.Height / 2) > 0.01f)
                        failures.Add("information button group is not vertically centered");
                    if (element.Command == F5Command.None && F5Layout.HintIndex(element, false) >= 0)
                        failures.Add("inactive control still has a development hover hint");
                }
                if (page == 9 && layout.Elements[0].Rect.Height > 42)
                    failures.Add("ordinary row retains the removed second-line space");
            }
            if (failures.Count != 0) throw new InvalidOperationException("Visual contract: " + string.Join("; ",
                new System.Collections.Generic.HashSet<string>(failures)));
        }

        private static void ValidateBounds(F5Layout layout)
        {
            for (int i = 0; i < 12; i++)
            {
                F5Rect nav = layout.Navigation(i), icon = layout.NavigationIcon(i), label = layout.NavigationLabel(i);
                Equal(nav.X + nav.Width / 2, (icon.X + label.Right) / 2, "icon-label group horizontal center");
                Equal(nav.Y + nav.Height / 2, icon.Y + icon.Height / 2, "icon visual midline");
                Equal(nav.Y + nav.Height / 2, label.Y + label.Height / 2, "label visual midline");
            }
            F5Rect track = layout.ScrollTrack, visual = layout.ScrollThumbVisual(layout.MaxScroll / 2);
            if (visual.X < track.X || visual.Right > track.Right || visual.Width >= track.Width ||
                track.X <= layout.Viewport.Right || visual.Y < track.Y || visual.Bottom > track.Bottom)
                throw new InvalidOperationException("Thin scrollbar must stay inside the existing grab channel.");
            Equal(track.X + track.Width / 2, visual.X + visual.Width / 2, "visual and interactive scroll centers");
            foreach (F5Element element in layout.Elements)
            {
                if (element.Rect.X < 0 || element.Rect.Right > layout.Viewport.Width + 0.01f ||
                    element.Rect.Y < 0 || element.Rect.Bottom > layout.ContentHeight + 0.01f)
                    throw new InvalidOperationException("A production page element exceeds its scrollable bounds.");
                if (element.Kind == F5ElementKind.Button &&
                    (element.TextSize.Width > element.Rect.Width - 8 || element.TextSize.Height > element.Rect.Height - 4))
                    throw new InvalidOperationException("A production button clips its measured label.");
            }
        }

        private static F5Size Measure(string text)
        { return new F5Size(text.Length * 18, 24); }

        internal static void Equal(float expected, float actual, string message)
        {
            if (Math.Abs(expected - actual) > 0.01f)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }
    }
}
