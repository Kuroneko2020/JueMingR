using System;
using System.Collections.Generic;
using JueMingR.Features.EntityLabels;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // Exactly one Game-scaled interface callback. Terraria owns SpriteBatch,
    // ZoomMatrix, font and textures; this object owns only bounded text metrics.
    internal sealed class EntityWorldLayer
    {
        private readonly IReadOnlyList<EntityLabel> labels;
        private readonly Func<bool> sessionActive;
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly Entry[] cache = new Entry[Main.maxNPCs];
        private readonly bool[] seen = new bool[Main.maxNPCs];
        private DynamicSpriteFont font;
        internal EntityWorldLayer(IReadOnlyList<EntityLabel> labels, Func<bool> sessionActive)
        { this.labels = labels; this.sessionActive = sessionActive; }
        internal string Failure { get; private set; }
        internal bool FontUnavailable { get; private set; }
        internal int LastDrawn { get; private set; }
        internal int MeasurementCount { get; private set; }
        internal int CachedEntries { get; private set; }
        internal void Clear() { Array.Clear(cache, 0, cache.Length); font = null; CachedEntries = 0; LastDrawn = 0; }
        internal bool Draw()
        {
            LastDrawn = 0;
            try
            {
                if (labels.Count == 0) { if (CachedEntries != 0) Clear(); return true; }
                if (Failure != null || !sessionActive() || Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 ||
                    Main.LocalPlayer == null || !Main.LocalPlayer.active || Main.mapFullscreen || Main.hideUI || Main.onlyDrawFancyUI || Main.inFancyUI ||
                    Main.ingameOptionsWindow || Terraria.Graphics.Capture.CaptureManager.Instance == null || Terraria.Graphics.Capture.CaptureManager.Instance.Active) return true;
                DynamicSpriteFont current = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
                FontUnavailable = current == null || Main.spriteBatch == null;
                if (FontUnavailable) return true;
                if (!ReferenceEquals(font, current)) { Clear(); font = current; }
                Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
                Array.Clear(seen, 0, seen.Length);
                for (int i = 0; i < labels.Count; i++)
                {
                    EntityLabel label = labels[i];
                    if (label.SourceSlot < 0 || label.SourceSlot >= cache.Length) continue;
                    seen[label.SourceSlot] = true;
                    Entry entry = cache[label.SourceSlot];
                    if (entry == null) { cache[label.SourceSlot] = entry = new Entry(); CachedEntries++; }
                    if (entry.Name != label.Name || entry.NameSize != label.NameSize)
                    { entry.Name = label.Name; entry.NameSize = label.NameSize; entry.NameBounds = Measure(label.Name, label.NameSize); }
                    if (entry.Health != label.Health || entry.HealthSize != label.HealthSize)
                    { entry.Health = label.Health; entry.HealthSize = label.HealthSize; entry.HealthBounds = label.Health == null ? default(F5Size) : Measure(label.Health, label.HealthSize); }
                    float height = entry.NameBounds.Height + (label.Health == null ? 0 : 2 + entry.HealthBounds.Height);
                    float x = label.X - Main.screenPosition.X, top = ScreenTop(label, height);
                    float width = Math.Max(entry.NameBounds.Width, entry.HealthBounds.Width);
                    Vector2 leftTop = Vector2.Transform(new Vector2(x - width / 2, top), zoom);
                    Vector2 rightBottom = Vector2.Transform(new Vector2(x + width / 2, top + height), zoom);
                    if (rightBottom.X < 0 || rightBottom.Y < 0 || leftTop.X > Main.screenWidth || leftTop.Y > Main.screenHeight) continue;
                    Color color = new Color((byte)(label.Rgb >> 16), (byte)(label.Rgb >> 8), (byte)label.Rgb);
                    Text(label.Name, x, top, label.NameSize, entry.NameBounds, color);
                    if (label.Health != null) Text(label.Health, x, top + entry.NameBounds.Height + 2, label.HealthSize, entry.HealthBounds, color);
                    LastDrawn++;
                }
                // Retire vanished/disabled labels without accumulating old names
                // across slot reuse, while retaining unchanged visible metrics.
                for (int i = 0; i < cache.Length; i++) if (!seen[i] && cache[i] != null) { cache[i] = null; CachedEntries--; }
            }
            catch (Exception e)
            {
                // A false return or escaped exception stops Terraria's remaining
                // interface layers. Disable only this capability and keep UI alive.
                Failure = "entity-world-draw-" + e.GetType().Name; Clear();
            }
            return true;
        }
        private F5Size Measure(string text, int hundredths)
        {
            F5Size size = metrics.Measure(font, text); float scale = hundredths / 100f; MeasurementCount++;
            return new F5Size(size.Width * scale + 4, size.Height * scale + 4, size.OffsetX * scale - 2, size.OffsetY * scale - 2);
        }
        internal static float ScreenTop(EntityLabel label, float textHeight)
        {
            // GameInterfaceLayer uses ZoomMatrix, which does not flip gravity.
            // Like native combat text, mirror the world anchor but keep glyphs
            // upright. The object's lower world edge is its upper screen edge.
            float anchor = label.Y - Main.screenPosition.Y;
            if (Main.LocalPlayer != null && Main.LocalPlayer.gravDir == -1) anchor = Main.screenHeight - (anchor + label.Height);
            return anchor - 6 - textHeight;
        }
        private void Text(string text, float center, float top, int size, F5Size bounds, Color color)
        { Utils.DrawBorderStringFourWay(Main.spriteBatch, font, text, center - bounds.Width / 2 - bounds.OffsetX, top - bounds.OffsetY,
            color, Color.Black, Vector2.Zero, size / 100f); }
        private sealed class Entry
        { internal string Name, Health; internal int NameSize, HealthSize; internal F5Size NameBounds, HealthBounds; }
    }
}
