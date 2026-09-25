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
                Call(page, "Execute", Enum.Parse(assemblyCommand(page), "AboutHelp")); Call(renderer, "Prepare", state, 1280f, (float)height, scale); Call(state, "ScrollTo", 0f);
                graphics.Image(Path.Combine(output, "help-top-" + size + ".png"), () => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix, 1280, height);
                Call(state, "ScrollTo", (float)Get(layout, "MaxScroll"));
                graphics.Image(Path.Combine(output, "help-bottom-" + size + ".png"), () => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix, 1280, height);
                Call(page, "Leave");
            }
            DrawOnboarding(context, graphics, output);
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
            Console.WriteLine("PASS: actual XNB full About/help previews at normal, short and 150 percent; embedded image decode/reuse/disposal.");
        }
        private static void DrawOnboarding(object context, ProbeGraphics graphics, string output)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            typeof(Main).GetField("_uiScaleMatrix", flags).SetValue(null, Matrix.Identity);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth", flags).SetValue(null, 1280);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight", flags).SetValue(null, 720);
            Main.playerInventory = false; Main.LocalPlayer.dead = false;
            object owner = Get(context, "onboarding"); Call(owner, "OnSessionStarted"); Call(owner, "Update", 0UL);
            var marker = (OnboardingState)Get(owner, "State"); NativeQuickItemChecks.Until(() => { marker.Poll(); return marker.Ready; });
            Action<bool, object> draw = (eligible, overlap) => Call(owner, "Draw", eligible, overlap);
            graphics.Pixels(() => draw(false, null), Matrix.Identity); Require(!marker.Shown && !marker.Saved, "Ineligible actual Draw never marks the role");
            var font = Terraria.GameContent.FontAssets.MouseText;
            try { Terraria.GameContent.FontAssets.MouseText = null; graphics.Pixels(() => draw(true, null), Matrix.Identity); Require(!marker.Shown, "Missing font is not a presentation"); }
            finally { Terraria.GameContent.FontAssets.MouseText = font; }
            Type rectType = owner.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect");
            var predicate = Expression.Lambda(typeof(Func<,>).MakeGenericType(rectType, typeof(bool)), Expression.Constant(true), Expression.Parameter(rectType)).Compile();
            graphics.Pixels(() => draw(true, predicate), Matrix.Identity); Require(!marker.Shown, "All occupied placement candidates defer actual Draw");
            graphics.Image(Path.Combine(output, "onboarding.png"), () => draw(true, null), Matrix.Identity, 1280, 720);
            Require(marker.Shown && !marker.Saved, "Successful production SpriteBatch flush marks only memory before Poll");
            graphics.Pixels(() => draw(true, predicate), Matrix.Identity); Require((double)Get(marker, "lastDraw") == -1, "Occlusion breaks visible-time accounting");
            NativeQuickItemChecks.Until(() => { marker.Poll(); return marker.Saved; });
            Require(marker.Saved, "Actual Draw receipt becomes durable only after the real isolated file worker");
            Call(owner, "OnSessionEnded");
            Console.WriteLine("PASS: production onboarding Draw ineligible/missing-font/occluded/success receipt and isolated durable Poll.");
        }
        private static Type assemblyCommand(object page) { return page.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Command"); }
    }
}
