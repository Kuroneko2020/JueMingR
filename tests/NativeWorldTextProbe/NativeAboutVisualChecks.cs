using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using JueMingR.Features.Onboarding;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeAboutVisualChecks
    {
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            object shell = Get(context, "Shell"), state = Get(shell, "State"), renderer = Get(shell, "renderer"), layout = Get(state, "Layout"), page = Get(layout, "About");
            Call(page, "Leave");
            Call(state, "Navigate", 5); Call(state, "RestoreVisible"); Set(state, "Ready", true);
            for (int size = 0; size < 3; size++)
            {
                int height = size == 0 ? 1080 : size == 1 ? 640 : 720; float scale = size == 2 ? 1.5f : 1;
                typeof(Main).GetField("_uiScaleMatrix", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Matrix.CreateScale(scale));
                var sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
                Set(sample, "Width", 1280f); Set(sample, "Height", (float)height); Set(sample, "Scale", scale); Set(sample, "Active", true); Set(sample, "Focused", true);
                Call(state, "Update", sample);
                Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, (float)height, scale);
                Require(size == 0 ? (float)Get(layout, "MaxScroll") == 0 : (float)Get(layout, "MaxScroll") > 0, "Default home fits wholly; short viewport preserves content with scrolling");
                Call(state, "ScrollTo", 0f);
                graphics.Image(Path.Combine(output, "about-top-" + size + ".png"), () => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix, 1280, height);
                Call(state, "ScrollTo", (float)Get(layout, "MaxScroll"));
                graphics.Image(Path.Combine(output, "about-support-" + size + ".png"), () => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix, 1280, height);
                var resources = Get(renderer, "sponsorImages");
                Require(!(bool)Get(resources, "Failed"), "Both images decode from production assembly");
                var textures = (Texture2D[])Get(resources, "textures"); Require(textures[0].Width == 631 && textures[1].Width == 819, "Original square source dimensions retained");
                int attempts = (int)Get(resources, "LoadAttempts");
                int paletteReads = (int)Get(Get(renderer, "aboutPainter"), "PaletteReads");
                for (int i = 0; i < 10; i++) graphics.Pixels(() => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix);
                Require((int)Get(resources, "LoadAttempts") == attempts, "Stable display reuses exactly two owned textures");
                Require((int)Get(Get(renderer, "aboutPainter"), "PaletteReads") == paletteReads, "Stable page never resamples the borrowed skin");
                Call(page, "Leave");
            }
            DrawOnboarding(context, graphics, output);
            CheckQrFrames(renderer, graphics);
            Call(renderer, "Prepare", state, 1280f, 1080f, 1f);
            var images = (Texture2D[])Get(Get(renderer, "sponsorImages"), "textures"); var first = images[0]; var second = images[1];
            int oldGeneration = (int)Get(layout, "Generation");
            object failedImages = Activator.CreateInstance(Get(renderer, "sponsorImages").GetType(), true);
            object imageRect = Get(((System.Collections.IEnumerable)Get(layout, "Elements")).Cast<object>().First(e => Get(e, "Kind").ToString() == "Image"), "Rect");
            for (int i = 0; i < 2; i++) graphics.Pixels(() => Call(failedImages, "Draw", Main.spriteBatch, Terraria.GameContent.TextureAssets.MagicPixel.Value, imageRect, "missing.jpg", 1f), Matrix.Identity);
            Require((bool)Get(failedImages, "Failed") && (int)Get(failedImages, "LoadAttempts") == 1, "Missing embedded resource latches one decode attempt on the same device");
            using (var replacement = new ProbeGraphics((string)Get(graphics, "contentDirectory")))
            {
                replacement.Pixels(() => Call(failedImages, "Draw", Main.spriteBatch, Terraria.GameContent.TextureAssets.MagicPixel.Value, imageRect, "alipay.jpg", 1f), Matrix.Identity);
                Require(!(bool)Get(failedImages, "Failed") && (int)Get(failedImages, "LoadAttempts") == 2, "A real device generation permits one recovery load and clears failure");
                Call(failedImages, "Dispose");
                Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 1080f, 1f); Call(state, "ScrollTo", 0f);
                replacement.Pixels(() => Call(renderer, "Draw", state, Matrix.Identity, false, false), Matrix.Identity);
                Require((int)Get(layout, "Generation") > oldGeneration && first.IsDisposed && second.IsDisposed, "Actual replacement XNB font/device reflows and retires old owned image textures");
                first = images[0]; second = images[1]; Require(first != null && second != null && first.GraphicsDevice == replacement.GraphicsDevice, "Replacement device owns exactly the recreated images");
                Call(renderer, "Dispose"); Require(first.IsDisposed && second.IsDisposed, "Renderer exit releases owned sponsor textures");
            }
            Console.WriteLine("PASS: actual XNB About previews at normal, short and 150 percent; embedded image decode/reuse/disposal.");
        }
        private static void DrawOnboarding(object context, ProbeGraphics graphics, string output)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            typeof(Main).GetField("_uiScaleMatrix", flags).SetValue(null, Matrix.Identity);
            typeof(Main).GetField("_uiScaleUsed", flags).SetValue(null, 1f);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth", flags).SetValue(null, 1280);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight", flags).SetValue(null, 720);
            Main.playerInventory = Main.gameMenu = Main.hideUI = Main.mapFullscreen = false;
            Main.showItemText = true; Main.LocalPlayer.dead = false;
            Main.screenWidth = 1280; Main.screenHeight = 720;
            Main.LocalPlayer.position = new Vector2(600, 420); Main.LocalPlayer.width = 20; Main.LocalPlayer.height = 42; Main.LocalPlayer.gravDir = 1;
            Main.screenPosition = new Vector2(100, 100); Main.GameViewMatrix.Zoom = Vector2.One;
            PopupText.ClearAll();
            object owner = Get(context, "onboarding");
            bool eligible = false;
            Set(owner, "CanPresent", (Func<bool>)(() => eligible)); Set(owner, "Overlaps", null);
            Call(owner, "OnSessionStarted"); Call(owner, "Update", 0UL);
            var marker = (OnboardingState)Get(owner, "State"); NativeQuickItemChecks.Until(() => { marker.Poll(); return marker.Ready; });
            Action update = () => Call(owner, "Update", 0UL);
            Action confirm = () => Call(owner, "ConfirmNativeDraw");
            Action animate = () => { for (int i = 0; i < 12; i++) { PopupText.UpdateItemText(); update(); } };
            Func<Color[]> draw = () => graphics.Pixels(() => PopupText.DrawItemTextPopups(PopupText.TargetScale), Main.GameViewMatrix.ZoomMatrix);
            update(); confirm(); Require(!marker.Shown && !marker.Saved && PopupText.popupText.All(p => !p.active), "Ineligible admission neither enters native pool nor marks the role");
            eligible = true;
            var font = Terraria.GameContent.FontAssets.MouseText;
            try { Terraria.GameContent.FontAssets.MouseText = null; update(); confirm(); Require(!marker.Shown, "Missing font is not a presentation"); }
            finally { Terraria.GameContent.FontAssets.MouseText = font; }
            Type rectType = owner.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect");
            var predicate = Expression.Lambda(typeof(Func<,>).MakeGenericType(rectType, typeof(bool)), Expression.Constant(true), Expression.Parameter(rectType)).Compile();
            Set(owner, "Overlaps", predicate); update(); confirm(); Require(!marker.Shown && PopupText.popupText.All(p => !p.active), "Known HUD occlusion defers admission");
            Set(owner, "Overlaps", null);
            Main.LocalPlayer.position = new Vector2(-1000, -1000); update(); confirm(); Require(!marker.Shown, "Offscreen admission never creates a shown receipt");
            Main.LocalPlayer.position = new Vector2(600, 420);
            for (int i = 0; i < 20; i++) PopupText.NewText(new AdvancedPopupRequest { Text = "other-" + i, Color = Color.White, DurationInFrames = 180 }, new Vector2(100 + i * 10, 200));
            var previous = PopupText.popupText.Select(p => p.name).ToArray();
            update(); confirm();
            Require(PopupText.popupText.Select(p => p.name).SequenceEqual(previous) && !marker.Shown, "Full native pool waits without evicting any native entry");
            PopupText.ClearAll(); update();
            var owned = PopupText.popupText.Single(p => p.active);
            Require(owned.freeAdvanced && owned.context == PopupTextContext.Advanced && owned.name == "按 F5 打开决明R", "Production adapter enters the real shared reforge/pickup pool with exact text");
            var zero = draw(); confirm();
            Require(zero.All(c => c.A == 0) && !marker.Shown && !marker.Saved, "NewText initial zero-scale frame is not a displayed receipt");
            animate(); var pixels = draw();
            var bounds = PixelBounds(pixels);
            Require(bounds.Top > 220 && bounds.Bottom < 320 && Math.Abs(bounds.Center.X - 510) < 4, "Actual native glyphs rise from the player's head");
            Require(pixels.Count(c => c.A > 0) < 4000 && pixels.Any(c => c.R > 170 && c.G > 170 && c.B > 80 && c.B < 180), "Native yellow glyphs and outline have no panel background");
            Require(!marker.Shown && !marker.Saved, "Native batch submission alone does not bypass the host receipt gate");
            confirm(); Require(marker.Shown && !marker.Saved, "Post-flush host receipt marks memory before Poll");
            graphics.Image(Path.Combine(output, "onboarding.png"), () => PopupText.DrawItemTextPopups(PopupText.TargetScale), Main.GameViewMatrix.ZoomMatrix, 1280, 720);
            Main.screenPosition += new Vector2(80, 30);
            var camera = PixelBounds(draw());
            Require(camera.X == bounds.X - 80 && camera.Y == bounds.Y - 30, "Native world popup follows camera projection");
            Main.screenPosition -= new Vector2(80, 30);
            Main.GameViewMatrix.Zoom = new Vector2(1.25f);
            typeof(Main).GetField("_uiScaleUsed", flags).SetValue(null, 1.5f);
            typeof(Main).GetField("_uiScaleMatrix", flags).SetValue(null, Matrix.CreateScale(1.5f));
            animate(); var scaled = PixelBounds(draw()); confirm();
            Require(scaled.Width > bounds.Width * 1.4f && scaled.Width < bounds.Width * 1.65f, "Native world zoom and UI scale preserve intended text size");
            graphics.Image(Path.Combine(output, "onboarding-150.png"), () => PopupText.DrawItemTextPopups(PopupText.TargetScale), Main.GameViewMatrix.ZoomMatrix, 1280, 720);
            Main.GameViewMatrix.Zoom = Vector2.One;
            typeof(Main).GetField("_uiScaleUsed", flags).SetValue(null, 1f);
            typeof(Main).GetField("_uiScaleMatrix", flags).SetValue(null, Matrix.Identity);
            double before = (double)Get(marker, "visibleMilliseconds");
            Main.mapFullscreen = true; update();
            Require(!owned.active && (double)Get(marker, "lastDraw") == -1 && (double)Get(marker, "visibleMilliseconds") == before, "Map UI absence releases only the owned slot and pauses reading in Update");
            Main.mapFullscreen = false; Main.LocalPlayer.gravDir = -1; update(); animate();
            var inverted = PixelBounds(draw()); confirm();
            Require(inverted.Bottom < Main.screenHeight - 320 - Main.LocalPlayer.height, "Inverted gravity uses the screen head side and upright native glyphs");
            Main.LocalPlayer.gravDir = 1;
            eligible = false; update(); Require(PopupText.popupText.All(p => !p.active) && (double)Get(marker, "lastDraw") == -1, "Shell focus/eligibility loss releases native entry without consuming invisible reading time");
            eligible = true; update();
            // The native pool reuses the same object when exhausted. Retirement
            // must not deactivate a successor even when reference equality holds.
            int slot = (int)Get(owner, "slot");
            var reused = PopupText.popupText[slot]; PopupText.ResetText(reused);
            reused.active = true; reused.context = PopupTextContext.Advanced; reused.freeAdvanced = true; reused.name = reused.displayText = "foreign successor"; reused.lifeTime = 123;
            eligible = false; update();
            Require(reused.active && reused.lifeTime == 123 && reused.name == "foreign successor", "Losing native slot ownership never mutates the successor");
            PopupText.ClearAll(); eligible = true; update(); animate(); draw(); confirm();
            Set(marker, "visibleMilliseconds", 3000d);
            // Force persistent native collision; the final animation still has
            // an upper bound rather than indefinitely extending itself.
            owned = (PopupText)Get(owner, "popup");
            int neighbour = PopupText.NewText(new AdvancedPopupRequest { Text = "neighbour", Color = Color.White, DurationInFrames = 180 }, owned.position);
            for (int i = 0; i < 45; i++)
            {
                PopupText.popupText[neighbour].position = owned.position;
                PopupText.UpdateItemText(); update();
            }
            Require(!marker.Ready && !owned.active && PopupText.popupText[neighbour].active, "Native collision cannot prolong the finished prompt or retire its neighbour");
            NativeQuickItemChecks.Until(() => { marker.Poll(); return marker.Saved; });
            Require(marker.Saved, "Actual native draw receipt becomes durable through the isolated file worker");
            int eligibilityReads = 0;
            Set(owner, "CanPresent", (Func<bool>)(() => { eligibilityReads++; return true; }));
            for (int i = 0; i < 1000; i++) { update(); confirm(); }
            Require(eligibilityReads == 0, "Completed prompt performs zero presentation qualification work in 1000 Update/Draw calls");
            PopupText.ClearAll(); marker.Begin((long)Get(owner, "admission")); update(); animate();
            owned = (PopupText)Get(owner, "popup");
            Set(owner, "CanPresent", (Func<bool>)(() => { throw new InvalidOperationException("isolated presentation failure"); }));
            confirm();
            Require(!(bool)Get(owner, "Enabled") && !owned.active && !marker.Shown, "Receipt failure closes only onboarding, retires its slot, and never escapes into the game draw loop");
            Call(owner, "OnSessionEnded"); PopupText.ClearAll();
            Console.WriteLine("PASS: real native popup pool/full/zero-scale/head pixels/zoom/gravity/ownership/pause/bounded fade and durable post-flush receipt.");
        }
        private static Rectangle PixelBounds(Color[] pixels)
        {
            int left = 960, right = -1, top = 640, bottom = -1;
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].A > 0)
            { left = Math.Min(left, i % 960); right = Math.Max(right, i % 960); top = Math.Min(top, i / 960); bottom = Math.Max(bottom, i / 960); }
            Require(right >= left, "Expected actually visible glyph pixels");
            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
        private static void CheckQrFrames(object renderer, ProbeGraphics graphics)
        {
            Type rect = renderer.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect");
            Func<float, object> at = x => Activator.CreateInstance(rect, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { x, 30f, 96f, 96f }, null);
            var painter = Get(renderer, "aboutPainter");
            var pixels = graphics.Pixels(() =>
            {
                Call(painter, "QrFrame", Main.spriteBatch, Terraria.GameContent.TextureAssets.MagicPixel.Value, at(30), 1f, true);
                Call(painter, "QrFrame", Main.spriteBatch, Terraria.GameContent.TextureAssets.MagicPixel.Value, at(180), 1f, false);
            }, Matrix.Identity);
            Require(pixels[40 * 960 + 29] == new Color(64, 190, 108) && pixels[40 * 960 + 179] == new Color(64, 156, 235), "Actual QR frame pixels distinguish green WeChat and blue Alipay");
            Require(pixels[40 * 960 + 28].A == 0 && pixels[40 * 960 + 30] == Color.White && pixels[40 * 960 + 178].A == 0 && pixels[40 * 960 + 180] == Color.White, "Each colored frame is one logical pixel with a white quiet margin inside");
        }
    }
}
