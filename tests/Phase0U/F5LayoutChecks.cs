using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class F5LayoutChecks
    {
        internal static void Run()
        {
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
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 20, 26));
            Equal(generation + 1, layout.Generation, "actual font metric change triggers one reflow");
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

        private static void ValidateBounds(F5Layout layout)
        {
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
