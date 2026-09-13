using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.World;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeTextAnchorChecks
    {
        internal static void Metrics()
        {
            // Real ReLogic CPU callbacks with deliberately different glyph
            // padding/height and line spacing; no graphics device or texture.
            var glyphs = new List<Rectangle> { new Rectangle(0, 0, 10, 13), new Rectangle(0, 0, 10, 13), new Rectangle(0, 0, 30, 28), new Rectangle(0, 0, 10, 4), new Rectangle(0, 0, 1, 1) };
            var padding = new List<Rectangle> { new Rectangle(0, 7, 10, 13), new Rectangle(0, 7, 10, 13), new Rectangle(0, 0, 30, 28), new Rectangle(0, 8, 10, 4), new Rectangle(0, 40, 1, 1) };
            var characters = new List<char> { '?', 'H', 'W', '…', ' ' };
            var kerning = new List<Vector3> { new Vector3(0, 10, 0), new Vector3(0, 10, 0), new Vector3(0, 30, 0), new Vector3(0, 10, 0), new Vector3(0, 7, 0) };
            var font = new DynamicSpriteFont(0, 30, '?');
            Type pageType = typeof(DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage", true);
            object page = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { null, glyphs, padding, characters, kerning }, null);
            Array pages = Array.CreateInstance(pageType, 1); pages.SetValue(page, 0);
            typeof(DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(font, new object[] { pages });
            foreach (int size in new[] { 50, 70, 180 })
            {
                var style = WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.All).WithSize(size);
                foreach (string text in new[] { "H", "H  ", "H\n", "H\n\n", "H\n\nH" })
                {
                    var layout = new NativeWorldTextLayout(font, text, style, 460);
                    while (!layout.Ready) layout.Step(512);
                    float expected = (text == "H\n\nH" ? 80 : 20) * size / 100f + 1.5f;
                    Require(Math.Abs(layout.VisualBottom - expected) < .001f, "glyph padding, trailing spaces/lines, internal lines and unscaled shadow: " + text);
                    var position = new Vector2(0, -layout.VisualBottom - .1f);
                    Require(!layout.HasVisibleInk(position, Matrix.Identity, 960, 640), "line box cannot keep entirely offscreen ink eligible");
                    Require(layout.HasVisibleInk(position + new Vector2(0, 1), Matrix.Identity, 960, 640), "thin visible ink at viewport top remains eligible");
                }
            }
            var truncated = new NativeWorldTextLayout(font, "WZ", WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.Characters).WithSize(100).WithLimits(2, 1), 32);
            while (!truncated.Ready) truncated.Step(512);
            Require(truncated.Truncated && Math.Abs(truncated.VisualBottom - 13.5f) < .001f, "removed tall final unit cannot leave a stale bottom behind the ellipsis");
            Require(typeof(Terraria.GameContent.UI.Chat.ItemTagHandler).GetNestedType("ItemSnippet", BindingFlags.Public | BindingFlags.NonPublic)?.GetField("_item", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(Item), "fixed native parsed-item layout ABI");
            var shortLayout = new NativeWorldTextLayout(font, "H", WorldObjectSettings.Default.Style(WorldObjectKind.Chest).WithMode(WorldObjectMode.Always), 460);
            while (!shortLayout.Ready) shortLayout.Step(512);
            Main.screenPosition = Vector2.Zero;
            var projectPosition = typeof(WorldObjectTextWorldLayer).GetMethod("Position", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (int width in new[] { 2, 3 })
            {
                var value = new WorldObject { Kind = WorldObjectKind.Chest, TileX = 10, TileY = 20, Width = width };
                var actual = (Vector2)projectPosition.Invoke(null, new object[] { value, shortLayout, 320f });
                Require(Math.Abs(actual.X - (width == 2 ? 172.5f : 180.5f)) < .001f && Math.Abs(actual.Y - 304.5f) < .001f, "production projection uses distinct chest/dresser center and shared top");
            }
            var oldStackFont = Terraria.GameContent.FontAssets.ItemStack;
            try
            {
                var asset = (ReLogic.Content.Asset<DynamicSpriteFont>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<DynamicSpriteFont>), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "cpu-stack-font" }, null);
                asset.GetType().GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(asset, new object[] { font, new ReLogic.Content.Sources.FileSystemContentSource(Terraria.Program.SavePath) });
                Terraria.GameContent.FontAssets.ItemStack = asset;
                var itemBottom = typeof(NativeWorldTextLayout).GetMethod("ItemBottom", BindingFlags.Instance | BindingFlags.NonPublic);
                var icon = Terraria.UI.Chat.ChatManager.ParseMessage("[i/s20:8]", Color.White)[0];
                float actual = (float)itemBottom.Invoke(shortLayout, new object[] { icon, 16.8f });
                Require(Math.Abs(actual - 19.425f) < .001f, "native stack uses its own glyph padding and shadow beyond the icon slot");
            }
            finally { Terraria.GameContent.FontAssets.ItemStack = oldStackFont; }
            Console.WriteLine("PASS: actual ReLogic CPU glyph-bottom geometry, clipping, internal/trailing whitespace, 50/70/180 scale, ellipsis rollback and native item ABI; GPU pixels are separate.");
        }
        internal static void Run(ProbeGraphics graphics, WorldTileObservation world, WorldObjectHostObservation source, string output)
        {
            int[] columns = { 8, 20, 34, 48 }, types = { 21, 88, 55, 85 }, widths = { 2, 3, 2, 2 };
            string[] texts = { "H", "gyp", "中文", "A\n\n", "A\n\n中", "[i/s20:8]", "ABCDEFGHIJKLMN" };
            foreach (int size in new[] { 70, 50, 180 }) foreach (string text in texts)
            {
                Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; Main.chest = new Chest[8000]; source.EndSession();
                Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = new Vector2(450, 400);
                Main.LocalPlayer.gravDir = size == 50 ? -1 : 1;
                Main.GameViewMatrix.Zoom = new Vector2(size == 50 ? 1.25f : 1);
                for (int i = 0; i < 4; i++)
                {
                    NativeWorldChecks.Put(columns[i], 20, types[i], 0, widths[i]);
                    if (i < 2) NativeWorldChecks.Register(i, columns[i], 20).name = text;
                    else Main.sign[i - 2] = new Sign { x = columns[i], y = 20, text = text };
                }
                var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always).WithMode(WorldObjectKind.Sign, WorldObjectMode.All).WithMode(WorldObjectKind.Tombstone, WorldObjectMode.All);
                foreach (WorldObjectKind kind in new[] { WorldObjectKind.Chest, WorldObjectKind.Sign, WorldObjectKind.Tombstone }) settings = settings.With(settings.Style(kind).WithSize(size));
                if (text == texts[texts.Length - 1])
                    foreach (WorldObjectKind kind in new[] { WorldObjectKind.Sign, WorldObjectKind.Tombstone }) settings = settings.With(settings.Style(kind).WithMode(WorldObjectMode.Characters).WithLimits(2, 3));
                var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
                for (int tick = 0; tick < 80; tick++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
                Color[] pixels = graphics.WorldPixels(layer);
                Require(layer.Failure == null && layer.LastDrawn == 4, "all four native objects draw in anchor fixture");
                for (int i = 0; i < 4; i++)
                {
                    // Oracle uses independent complete tile rectangles and GPU
                    // pixels, never the production layout's height/ink fields.
                    float top = Main.LocalPlayer.gravDir == -1 ? 640 - (20 + 2) * 16 : 20 * 16;
                    var expected = Vector2.Transform(new Vector2(columns[i] * 16 + widths[i] * 8, top), Main.GameViewMatrix.ZoomMatrix);
                    int left = 960, right = -1, bottom = -1;
                    for (int y = 0; y < 640; y++) for (int x = Math.Max(0, (int)expected.X - 88); x < Math.Min(960, (int)expected.X + 88); x++)
                        if (pixels[y * 960 + x].A != 0) { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                    float gap = expected.Y - (bottom + 1);
                    Require(bottom >= 0 && gap >= -1 && gap <= 3, "ink bottom touches object top: type=" + types[i] + ", size=" + size + ", text=" + text.Replace("\n", "\\n") + ", gap=" + gap);
                    if (text == "H") Require(Math.Abs((left + right + 1) / 2f - expected.X) <= 3, "label centers on independent 2x2 chest / 3x2 dresser rectangle");
                }
                if (size == 70 && text == "H") graphics.Scene(layer, Path.Combine(output, "native-anchor-four-sizes.png"));
                layer.Clear(); discovery.Clear();
            }
            Main.GameViewMatrix.Zoom = Vector2.One; Main.LocalPlayer.gravDir = 1;
            Console.WriteLine("PASS: actual GPU ink bounds touch chest/dresser/sign/tombstone tops; 2x2/3x2 centers, glyph padding, trailing/internal blanks, truncation, font scale, zoom and gravity.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
