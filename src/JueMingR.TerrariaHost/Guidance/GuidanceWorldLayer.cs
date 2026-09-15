using System;
using System.Globalization;
using JueMingR.Features.Guidance;
using JueMingR.TerrariaHost.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class GuidanceWorldLayer
    {
        private readonly HostGuidance host;
        internal readonly GuidanceText RareText = new GuidanceText(), MerchantText = new GuidanceText(), EquipmentText = new GuidanceText();
        internal readonly NearbyTextOccupancy Occupancy = new NearbyTextOccupancy();
        private Texture2D arrow;
        private Matrix inverse;
        private Vector2 rarePoint, rareLabel, merchantLabel, equipmentLabel;
        private float rotation, alpha, arrowScale;
        private Color rareColor, merchantColor;
        private bool rareVisible, rareOutside, merchantVisible, equipmentVisible;
        private int failed, rareDistance = -1, merchantDistance = -1;
        private string rareName, location, rareContent, merchantContent;
        private int resourcesChanged;
        internal GuidanceWorldLayer(HostGuidance host) { this.host = host; Terraria.Localization.LanguageManager.Instance.OnLanguageChanged += ResourcesChanged; }
        private void ResourcesChanged(Terraria.Localization.LanguageManager manager) { System.Threading.Interlocked.Exchange(ref resourcesChanged, 1); }
        internal void Stop() { Terraria.Localization.LanguageManager.Instance.OnLanguageChanged -= ResourcesChanged; }
        internal int Failures { get { return failed; } }
        internal void Recover(GuidanceKind kind)
        { failed &= ~(1 << (int)kind); if (kind == GuidanceKind.Rare) RareText.Invalidate(); else if (kind == GuidanceKind.Merchant) MerchantText.Invalidate(); else EquipmentText.Invalidate(); }
        internal void Clear()
        {
            rareVisible = rareOutside = merchantVisible = equipmentVisible = false; Occupancy.Clear(); failed = 0;
            if (arrow != null) { try { arrow.Dispose(); } catch { } arrow = null; }
        }
        internal static Vector2 Project(Vector2 world, Matrix zoom)
        {
            Vector2 p = world - Main.screenPosition;
            if (Main.LocalPlayer.gravDir == -1) p.Y = Main.screenHeight - p.Y;
            return Vector2.Transform(p, zoom);
        }
        internal void Prepare()
        {
            if (System.Threading.Interlocked.Exchange(ref resourcesChanged, 0) != 0)
            { RareText.Invalidate(); MerchantText.Invalidate(); EquipmentText.Invalidate(); }
            rareVisible = rareOutside = merchantVisible = equipmentVisible = false;
            if (!host.CanDraw || !WorldPresentation.CanDraw || Main.GameViewMatrix == null) return;
            if (!host.Rare.Visible && !host.Merchant.Visible && host.Equipment.Alpha <= 0) { Occupancy.Clear(); return; }
            Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
            if (zoom.M11 <= 0 || zoom.M22 <= 0 || float.IsNaN(zoom.M11) || float.IsNaN(zoom.M22)) return;
            // Text shares the information HUD's UI scale, while anchors and
            // the circle remain in physical pixels, independent of Game zoom.
            float uiScale = Main.UIScaleMatrix.M11;
            if (uiScale <= 0 || float.IsNaN(uiScale) || float.IsInfinity(uiScale)) return;
            inverse = Matrix.Invert(zoom);
            Vector2 player = Project(Main.LocalPlayer.Center, zoom);
            float width = Main.screenWidth, height = Main.screenHeight;
            var font = FontAssets.MouseText?.Value;
            try
            {
                if ((failed & 1) == 0 && host.Rare.Visible)
                {
                    var style = host.Preferences.Value.Style(GuidanceKind.Rare); rareColor = ColorFrom(style.Rgb);
                    var target = host.Rare.Target; Vector2 point = Project(new Vector2(target.DrawX, target.DrawY), zoom);
                    var pose = DirectionProjection.Circle(player.X, player.Y, point.X, point.Y, 46);
                    if (pose.Visible)
                    {
                        rareVisible = true; rarePoint = Vector2.Transform(new Vector2((float)pose.X, (float)pose.Y), inverse);
                        arrowScale = (float)pose.Scale;
                        rotation = (float)Math.Atan2(Math.Sin(pose.Angle) * inverse.M22, Math.Cos(pose.Angle) * inverse.M11);
                        rareOutside = font != null && !DirectionProjection.OnScreen(point.X, point.Y, width, height);
                        if (rareOutside)
                        {
                            string name = (target.Identity as NPC)?.GivenOrTypeName; if (string.IsNullOrWhiteSpace(name)) name = "稀有生物";
                            int distance = DirectionProjection.Tiles(Main.LocalPlayer.Center.X, Main.LocalPlayer.Center.Y, target.X, target.Y);
                            if (name != rareName || distance != rareDistance)
                            { rareName = name; rareDistance = distance; rareContent = name + "\n约" + distance.ToString(CultureInfo.InvariantCulture) + "格"; }
                            RareText.Prepare(font, rareContent, style.Size / 100f * uiScale, width - 16);
                            // Near the bottom edge put the label above its arrow;
                            // clamping a below-arrow box upward can cover the player.
                            float labelY = pose.Y + 24 + RareText.Height <= height - 8
                                ? (float)pose.Y + 24 + RareText.Height / 2 : (float)pose.Y - 24 - RareText.Height / 2;
                            rareLabel = RareText.Clamp(new Vector2((float)pose.X, labelY), width, height);
                            rareOutside = RareText.Height <= height - 16;
                        }
                    }
                }
            }
            catch { failed |= 1; rareVisible = rareOutside = false; }
            try
            {
                if ((failed & 2) == 0 && host.Merchant.Visible && font != null)
                {
                    var style = host.Preferences.Value.Style(GuidanceKind.Merchant); merchantColor = ColorFrom(style.Rgb);
                    var target = host.Merchant.Target; Vector2 point = Project(new Vector2(target.DrawX, target.DrawY), zoom);
                    if (!DirectionProjection.OnScreen(point.X, point.Y, width, height))
                    {
                        int distance = DirectionProjection.Tiles(Main.LocalPlayer.Center.X, Main.LocalPlayer.Center.Y, target.X, target.Y);
                        if (distance != merchantDistance || location != host.Location.Text)
                        { merchantDistance = distance; location = host.Location.Text; merchantContent = "旅商\n约" + distance.ToString(CultureInfo.InvariantCulture) + "格\n" + location; }
                        MerchantText.Prepare(font, merchantContent, style.Size / 100f * uiScale, width - 16);
                        var pose = DirectionProjection.Ellipse(point.X, point.Y, width, height);
                        merchantVisible = pose.Visible && MerchantText.Height <= height - 16;
                        merchantLabel = MerchantText.Clamp(new Vector2((float)pose.X, (float)pose.Y), width, height);
                    }
                }
            }
            catch { failed |= 2; merchantVisible = false; }
            try
            {
                alpha = host.Equipment.Alpha;
                if ((failed & 4) == 0 && alpha > 0 && font != null)
                {
                    EquipmentText.Prepare(font, EquipmentWarning.Text, uiScale, width - 16);
                    equipmentVisible = EquipmentText.Height <= height - 16;
                    Vector2 head = Project(Main.LocalPlayer.Top, zoom);
                    // Mirrored head anchor, upright text. The local lane is
                    // independent of PopupText/CombatText lifetime or priority.
                    if (Main.LocalPlayer.gravDir == -1) head = Project(Main.LocalPlayer.Bottom, zoom);
                    equipmentLabel = Occupancy.Place(head - new Vector2(0, 26), EquipmentText.Width, EquipmentText.Height, width, height, zoom);
                }
                else Occupancy.Clear();
            }
            catch { failed |= 4; equipmentVisible = false; }
        }
        internal bool Draw()
        {
            if (!host.CanDraw || !WorldPresentation.CanDraw || Main.spriteBatch == null) return true;
            SpriteBatch batch = Main.spriteBatch; var gold = new Color(255, 224, 96);
            try
            {
                if (rareVisible)
                {
                    if (arrow == null || arrow.IsDisposed || !ReferenceEquals(arrow.GraphicsDevice, batch.GraphicsDevice))
                    { if (arrow != null) arrow.Dispose(); arrow = WorldTargets.WorldTargetWorldLayer.CreateArrow(batch.GraphicsDevice); }
                    batch.Draw(arrow, rarePoint, null, rareColor, rotation, new Vector2(10), new Vector2(inverse.M11, inverse.M22) * arrowScale, SpriteEffects.None, 0);
                    if (rareOutside) RareText.Draw(batch, rareLabel, inverse, rareColor);
                }
            }
            catch { failed |= 1; rareVisible = rareOutside = false; }
            try { if (merchantVisible) MerchantText.Draw(batch, merchantLabel, inverse, merchantColor); } catch { failed |= 2; merchantVisible = false; }
            try { if (equipmentVisible) EquipmentText.Draw(batch, equipmentLabel, inverse, gold * alpha); } catch { failed |= 4; equipmentVisible = false; }
            return true;
        }
        private static Color ColorFrom(int rgb) { return new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); }
    }
}
