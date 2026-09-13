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
                Require(Math.Abs(actual.X - (width == 2 ? 172.5f : 180.5f)) < .001f && Math.Abs(actual.Y - 306f) < .001f, "production projection aligns the body bottom, allowing its 1.5-pixel shadow below the art edge");
            }
            // Independent samples from the actual .8 closed XNB alpha and
            // native tile draw offsets; not the implementation's table values.
            int[,] bounds = { {21,0,6,34}, {441,28,10,34}, {467,35,12,34}, {468,31,2,28},
                {88,0,0,32}, {88,57,0,28}, {55,1,10,32}, {55,2,6,28}, {425,0,0,22}, {425,3,6,28}, {573,0,4,22}, {573,1,14,32}, {573,4,8,26}, {85,0,4,34}, {85,1,2,34} };
            for (int i = 0; i < bounds.GetLength(0); i++)
            {
                var value = new WorldObject { Type = bounds[i,0], Style = bounds[i,1], TileY = 20 };
                Require(NativeWorldObjectBounds.AnchorY(value, 7, 640, false) == 313 + bounds[i,2], "normal anchor follows visible art, not tile top");
                Require(NativeWorldObjectBounds.AnchorY(value, 7, 640, true) == 327 - bounds[i,3], "inverted anchor follows visible bottom, including native draw overhang");
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
            int[] columns = { 8, 20, 34, 48 }, widths = { 2, 3, 2, 2 };
            string[] texts = { "H", "gyp", "中文", "药水/食物/材料", "A\n\n", "A\n\n中", "[i/s20:8]", "ABCDEFGHIJKLMN" };
            foreach (int group in new[] { 0, 1 }) foreach (int size in new[] { 70, 50, 180 }) foreach (string text in texts)
            {
                int[] types = group == 0 ? new[] {21,88,55,85} : new[] {467,88,573,425};
                int[] styles = group == 0 ? new[] {0,0,1,0} : new[] {35,57,4,2};
                Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; Main.chest = new Chest[8000]; source.EndSession();
                Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = new Vector2(450, 400);
                Main.LocalPlayer.gravDir = size == 50 ? -1 : 1;
                Main.GameViewMatrix.Zoom = new Vector2(size == 50 ? 1.25f : 1);
                for (int i = 0; i < 4; i++)
                {
                    NativeWorldChecks.Put(columns[i], 20, types[i], styles[i], widths[i]);
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
                if (size == 70 && text == "H") graphics.Scene(layer, Path.Combine(output, "native-anchor-art-" + group + ".png"));
                if (size == 70 && text == "药水/食物/材料") graphics.Scene(layer, Path.Combine(output, "native-anchor-sign-text-" + group + ".png"));
                layer.Clear(); discovery.Clear();
                for (int i = 0; i < 4; i++)
                {
                    // Long chest names may overlap a neighbor's sample area.
                    // Isolate the actual world object, not private draw packets,
                    // so the pixel oracle measures only this label's geometry.
                    Main.tile = new Tile[256, 128]; source.EndSession();
                    NativeWorldChecks.Put(columns[i], 20, types[i], styles[i], widths[i]);
                    discovery = new WorldObjectDiscovery(source); layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
                    for (int tick = 0; tick < 80; tick++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
                    pixels = graphics.WorldPixels(layer);
                    Require(layer.Failure == null && layer.LastDrawn == 1, "isolated object draws through normal production selection and layout");
                    // Oracle reads real texture alpha using the native closed
                    // tile slices, never production layout or its bounds table.
                    var art = graphics.ObjectArtBounds(types[i], styles[i], widths[i], true);
                    float top = Main.LocalPlayer.gravDir == -1 ? 640 - 20 * 16 - art.Bottom : 20 * 16 + art.Top;
                    var expected = Vector2.Transform(new Vector2(columns[i] * 16 + widths[i] * 8, top), Main.GameViewMatrix.ZoomMatrix);
                    int left = 960, right = -1, bottom = -1;
                    for (int y = 0; y < 640; y++) for (int x = Math.Max(0, (int)expected.X - 88); x < Math.Min(960, (int)expected.X + 88); x++)
                        if (pixels[y * 960 + x].A != 0) { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                    float gap = expected.Y - (bottom + 1);
                    Require(bottom >= 0 && gap >= -3 && gap <= 2, "body touches visible art, with at most its shadow below: type=" + types[i] + ", size=" + size + ", text=" + text.Replace("\n", "\\n") + ", gap=" + gap);
                    if (text == "H") Require(Math.Abs((left + right + 1) / 2f - expected.X) <= 3, "label centers on independent 2x2 chest / 3x2 dresser rectangle");
                    layer.Clear(); discovery.Clear();
                }
            }
            Main.GameViewMatrix.Zoom = Vector2.One; Main.LocalPlayer.gravDir = 1;
            Console.WriteLine("PASS: actual GPU ink bounds touch chest/dresser/sign/tombstone tops; 2x2/3x2 centers, glyph padding, trailing/internal blanks, truncation, font scale, zoom and gravity.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
