using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;

namespace Terraria
{
    internal static class UiTextMetricsChecks
    {
        internal static void Run()
        {
            var metrics = new UiTextMetrics();
            var font = Font(true);
            Equal(new F5Size(43, 18), metrics.Measure(font, "中 中"), "space keeps advance without a phantom lower line");
            Equal(new F5Size(36, 18), metrics.Measure(font, "中中"), "unspaced glyph bounds");
            Equal(new F5Size(25, 68), metrics.Measure(font, "中\r\n 中"), "CR/LF retain native line movement and callback alignment");
            Equal(new F5Size(43, 18), metrics.Measure(font, "中\r 中"), "carriage return produces no glyph callback");
            Equal(new F5Size(1, 1, 0, 3), metrics.Measure(font, "A"), "visible one-pixel glyph remains measurable");
            Equal(new F5Size(12, 24, 0, 2), metrics.Measure(font, "\t"), "unsupported tab keeps its fallback glyph");
            Equal(new F5Size(12, 24, 0, 2), metrics.Measure(font, "\u00a0"), "unsupported NBSP keeps its fallback glyph");
            Equal(new F5Size(12, 24, 0, 2), metrics.Measure(Font(false), " "), "unsupported space keeps its fallback glyph");
            Equal(new F5Size(8, 50, 5, 0), metrics.Measure(font, "  "), "all spaces retain finite line height and horizontal bounds");
            Equal(new F5Size(0, 50), metrics.Measure(font, ""), "empty text retains finite line height");
            Equal(new F5Size(18, 18), metrics.Measure(font, "中"), "repeated measurement has no previous-text state");
            Console.WriteLine("PASS: shared text metrics preserve spaces, native newlines, fallback and visible one-pixel glyphs.");
        }

        private static DynamicSpriteFont Font(bool withSpace)
        {
            // Real ReLogic CPU drawing callbacks, with no texture or graphics device.
            // The space models Mouse_Text.xnb's transparent 1x1 glyph at padding Y=40.
            var glyphs = new List<Rectangle> { new Rectangle(0, 0, 12, 24), new Rectangle(0, 0, 1, 1), new Rectangle(0, 0, 18, 18) };
            var padding = new List<Rectangle> { new Rectangle(0, 2, 12, 24), new Rectangle(0, 3, 1, 1), new Rectangle(0, 0, 18, 18) };
            var characters = new List<char> { '?', 'A', '中' };
            var kerning = new List<Vector3> { new Vector3(0, 12, 0), new Vector3(0, 1, 0), new Vector3(0, 18, 0) };
            if (withSpace)
            {
                glyphs.Add(new Rectangle(0, 0, 1, 1)); padding.Add(new Rectangle(5, 40, 1, 1));
                characters.Add(' '); kerning.Add(new Vector3(0, 7, 0));
            }
            var font = new DynamicSpriteFont(0, 50, '?');
            Type pageType = typeof(DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage", true);
            object page = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { null, glyphs, padding, characters, kerning }, null);
            Array pages = Array.CreateInstance(pageType, 1); pages.SetValue(page, 0);
            typeof(DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(font, new object[] { pages });
            return font;
        }

        private static void Equal(F5Size expected, F5Size actual, string message)
        {
            if (Math.Abs(expected.Width - actual.Width) > .01f || Math.Abs(expected.Height - actual.Height) > .01f ||
                Math.Abs(expected.OffsetX - actual.OffsetX) > .01f || Math.Abs(expected.OffsetY - actual.OffsetY) > .01f)
                throw new InvalidOperationException(message + ": expected " + expected.Width + "x" + expected.Height + " @" + expected.OffsetX + "," + expected.OffsetY +
                    "; actual " + actual.Width + "x" + actual.Height + " @" + actual.OffsetX + "," + actual.OffsetY);
        }
    }
}
