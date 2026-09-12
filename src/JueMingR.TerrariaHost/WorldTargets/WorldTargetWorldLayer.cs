using System;
using System.Collections.Generic;
using JueMingR.Features.WorldTargets;
using JueMingR.TerrariaHost.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.WorldTargets
{
    internal sealed class WorldTargetWorldLayer
    {
        private readonly IReadOnlyList<WorldTarget> targets;
        private readonly Func<bool> sessionActive;
        private readonly Func<WorldTargetSettings> settings;
        private Texture2D arrow;
        internal WorldTargetWorldLayer(IReadOnlyList<WorldTarget> targets, Func<bool> sessionActive, Func<WorldTargetSettings> settings)
        { this.targets = targets; this.sessionActive = sessionActive; this.settings = settings; }
        internal string Failure { get; private set; }
#if DEBUG
        internal int LastDrawn { get; private set; }
        internal int ResourceCreations { get; private set; }
#endif
        internal void Clear()
        {
            Texture2D owned = arrow; arrow = null;
            // Cleanup must not escape a local draw failure into later native UI.
            if (owned != null) try { owned.Dispose(); } catch { }
#if DEBUG
            LastDrawn = 0;
#endif
        }
        internal bool Draw()
        {
#if DEBUG
            LastDrawn = 0;
#endif
            try
            {
                if (Failure != null || targets.Count == 0 || !sessionActive() || !WorldPresentation.CanDraw || Main.spriteBatch == null) return true;
                GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
                if (device == null || device.IsDisposed) { Clear(); return true; }
                float gravity = Main.LocalPlayer.gravDir == -1 ? -1 : 1;
                var animation = default(WorldTargetAnimation); bool timeReady = false;
                WorldTargetSettings current = settings(); Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
                for (int i = 0; i < targets.Count; i++)
                {
                    WorldTarget target = targets[i];
                    if (!current.Enabled(target.Kind)) continue;
                    Vector2 center = Screen(target.CenterX, target.CenterY, gravity);
                    float extent = WorldTargetArrows.Extent(target);
                    Vector2 a = Vector2.Transform(center - new Vector2(extent), zoom), b = Vector2.Transform(center + new Vector2(extent), zoom);
                    if (b.X < 0 || b.Y < 0 || a.X > Main.screenWidth || a.Y > Main.screenHeight) continue;
                    if (!timeReady) { animation = new WorldTargetAnimation(Main.gameTimeCache.TotalGameTime.Ticks, gravity); timeReady = true; }
                    EnsureArrow(device);
                    int rgb = current.Color(target.Kind); var color = new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                    for (int index = 0; index < 3; index++)
                    {
                        ArrowPose pose = WorldTargetArrows.At(target, animation, index);
                        Vector2 point = Screen(pose.X, pose.Y, gravity);
                        float rotation = (float)Math.Atan2(pose.DirectionY * gravity, pose.DirectionX);
                        // The caller's Game layer owns ZoomMatrix/batch state.
                        // Mirror both position and direction, never UI-scale twice.
                        Main.spriteBatch.Draw(arrow, point, null, color, rotation, new Vector2(10, 10), pose.Length / 20,
                            SpriteEffects.None, 0);
#if DEBUG
                        LastDrawn++;
#endif
                    }
                }
            }
            catch (Exception e) { Failure = "world-target-draw-" + e.GetType().Name; Clear(); }
            return true;
        }
        internal static Vector2 Screen(float x, float y, float gravity)
        { return new Vector2(x - Main.screenPosition.X, gravity == -1 ? Main.screenHeight - (y - Main.screenPosition.Y) : y - Main.screenPosition.Y); }
        private void EnsureArrow(GraphicsDevice device)
        {
            if (arrow != null && !arrow.IsDisposed && ReferenceEquals(arrow.GraphicsDevice, device)) return;
            Clear();
            // One owned short, wide arrow on a square 20x20 canvas. The draw
            // origin/scale above use this same canvas, preserving world position.
            // White fill plus a one-pixel black border.
            // Black remains a contrasting outline under any user RGB tint.
            var pixels = new Color[20 * 20];
            for (int y = 0; y < 20; y++) for (int x = 0; x < 20; x++)
            {
                bool fill = Inside(x, y);
                bool inner = fill && Inside(x - 1, y) && Inside(x + 1, y) && Inside(x, y - 1) && Inside(x, y + 1);
                pixels[y * 20 + x] = !fill ? Color.Transparent : inner ? Color.White : Color.Black;
            }
            var created = new Texture2D(device, 20, 20);
            try { created.SetData(pixels); arrow = created; }
            catch { created.Dispose(); throw; }
#if DEBUG
            ResourceCreations++;
#endif
        }
        private static bool Inside(int x, int y)
        {
            if (x < 0 || x > 19 || y < 0 || y > 19) return false;
            return x < 10 ? y >= 6 && y <= 13 : Math.Abs(y - 9.5) <= (19.5 - x) * (9.5 / 10);
        }
    }
}
