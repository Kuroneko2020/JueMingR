using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Reuses the initialized isolated Main scene. This finite smoke takes the
    // actual F5 pointer route through native Draw and one real prism AI cursor
    // leaf. It does not requalify prediction, every weapon or physical input.
    internal static class NativeViewZoomDrawChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context, Action<Keys[]> input, Action draw, Func<RenderTarget2D> final,
            string output, Action<int, int, bool> mouse)
        {
            object shell = Get(context, "Shell"), state = Get(shell, "State"), host = Get(context, "View");
            object selection = Get(Get(context, "CombatObservation"), "Selection");
            float original = Main.GameZoomTarget; bool enabled = (bool)Get(host, "UnlockEnabled");
            try
            {
                int index = 0;
                foreach (float position in new[] { .15f, .5f, .85f })
                {
                    Call(host, "SetUnlockEnabled", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible");
                    Call(context, "UpdateShell"); draw();
                    object layout = Get(state, "Layout");
                    object element = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == "ViewZoomTrack");
                    object rect = Get(element, "Rect"), viewport = Get(layout, "Viewport");
                    Call(state, "ScrollTo", (float)Get(rect, "Y") - 20); Call(context, "UpdateShell"); draw();
                    float x = (float)Get(state, "X") + (float)Get(viewport, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") * position;
                    float y = (float)Get(state, "Y") + (float)Get(viewport, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + (float)Get(rect, "Height") / 2;
                    int px = (int)(x * Main.UIScaleMatrix.M11), py = (int)(y * Main.UIScaleMatrix.M22);
                    mouse(px, py, false); input(new Keys[0]); input(new Keys[0]);
                    mouse(px, py, true); input(new Keys[0]);
                    Require((bool)Get(Get(shell, "ViewZoom"), "Captured"), "native mapped pointer enters real F5 track capture");
                    mouse(px, py, false); input(new Keys[0]);
                    float target = Main.GameZoomTarget;
                    Require(position < .3f ? target < 1f : position > .7f ? target > 2f : target > 1f && target < 2f, "F5 target reaches requested low/blue/high domain");
                    Call(shell, "CloseAndSubmitPosition"); mouse(1400, 540, false); input(new Keys[0]); input(new Keys[0]); draw();
                    float expected = (float)(Math.Round((float)(target * Main.ForcedMinimumZoom / (1f / 128f))) * (1f / 128f));
                    Require(Main.GameViewMatrix.RenderZoom.X == expected && (float)Get(Get(host, "Plan"), "Target") == target,
                        "F5 target follows existing native forced/quantized final camera plan");
                    Call(selection, "SampleMouse", Get(context, "Input"));
                    Require((bool)Get(selection, "HasMouse") && Vector2.Distance((Vector2)Get(selection, "RealMouse"), Main.MouseWorld) < 1f,
                        "representative selection observes native mouse world after F5 zoom");
                    var screen = Vector2.Transform(Main.MouseWorld - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
                    var center = new Vector2(Main.screenWidth, Main.screenHeight) * .5f;
                    var unrounded = center + (new Vector2(1400, 540) - center) / expected;
                    var nativeCursor = new Vector2((int)unrounded.X, (int)unrounded.Y);
                    Require(Main.MouseWorld == Main.screenPosition + nativeCursor, "world cursor follows exact original integer input formula");
                    // Native input truncates in world coordinates; native Draw
                    // separately rounds the world-space center translation.
                    // Their <1 and <=.5 world-unit residuals grow with zoom.
                    float residualBound = 1.5f * (float)Math.Sqrt(2) * expected + .01f;
                    Require(Vector2.Distance(screen, new Vector2(1400, 540)) <= residualBound, "cursor projection retains only original integer/translation residual");
                    Console.WriteLine("ZOOM CONTROL native-cursor physical=1400,540 projected=" + screen + " original-residual-bound=" + residualBound);
                    PrismCursorLeaf(context);
                    using (var file = File.Create(Path.Combine(output, "zoom-control-world-" + index + ".png"))) final().SaveAsPng(file, Main.screenWidth, Main.screenHeight);
                    Call(host, "SetUnlockEnabled", false); draw(); draw();
                    Require(Main.GameZoomTarget == target && Main.GameViewMatrix.RenderZoom.X == expected, "OFF keeps actual target and final camera without input");
                    using (var file = File.Create(Path.Combine(output, "zoom-control-world-" + index + "-retained.png"))) final().SaveAsPng(file, Main.screenWidth, Main.screenHeight);
                    Console.WriteLine("ZOOM CONTROL native-draw domain=" + index + " target=" + target + " forced=" + Main.ForcedMinimumZoom + " render=" + expected + " camera=" + Main.Camera.ScaledPosition + " visible=" + Main.Camera.ScaledSize);
                    index++;
                }
            }
            finally { Call(shell, "CloseAndSubmitPosition"); mouse(1400, 540, false); input(new Keys[0]); Main.GameZoomTarget = original; Call(host, "SetUnlockEnabled", enabled); }
            Console.WriteLine("PASS ViewZoomControlDraw: real F5 pointer -> unique target -> native Main.Draw/final camera for low/blue/high and OFF retention; physical-to-world selection and original prism AI075 cursor leaf. No full prediction, gameplay, multiplayer or FPS claim.");
        }
        private static void PrismCursorLeaf(object context)
        {
            var p = Main.LocalPlayer; Item item = p.inventory[0]; int slot = p.selectedItem, held = p.heldProj;
            int animation = p.itemAnimation, itemTime = p.itemTime, direction = p.direction;
            float rotation = p.itemRotation;
            bool channel = p.channel, noItems = p.noItems; int oldX = Main.mouseX, oldY = Main.mouseY;
            var type = context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope", true);
            object scope = null;
            try
            {
                p.inventory[0] = new Item(); p.inventory[0].SetDefaults(3541); p.selectedItemState.Select(0); p.selectedItemState.Update();
                p.channel = true; p.noItems = false;
                var origin = p.RotatedRelativePoint(p.MountedCenter);
                var aim = origin + new Vector2(240, -110);
                var shot = new Projectile(); shot.SetDefaults(633); shot.owner = p.whoAmI; shot.active = true;
                shot.ai[0] = 1f; shot.ai[1] = 0; shot.soundDelay = 10; shot.velocity = Vector2.UnitX * p.HeldItem.shootSpeed;
                scope = type.GetMethod("Begin", Flags).Invoke(null, new object[] { aim, true });
                var actualCursor = Main.screenPosition + new Vector2(Main.mouseX, Main.mouseY);
                if (p.gravDir < 0) actualCursor.Y = Main.screenPosition.Y + Main.screenHeight - Main.mouseY;
                Require(Vector2.Distance(actualCursor, aim) < 1.5f, "existing assisted cursor maps world point with current camera without a second zoom");
                Vector2 expected = Vector2.Normalize(Vector2.Lerp((actualCursor - origin).SafeNormalize(-Vector2.UnitY), Vector2.UnitX, .92f)) * p.HeldItem.shootSpeed * shot.scale;
                typeof(Projectile).GetMethod("AI_075", Flags).Invoke(shot, null);
                Require(Vector2.Distance(expected, shot.velocity) < .001f, "original prism AI consumes same assisted world point after F5 zoom");
            }
            finally
            {
                if (scope != null) Call(scope, "End");
                Require(Main.mouseX == oldX && Main.mouseY == oldY, "prism cursor leaf restores original physical sample");
                p.inventory[0] = item; p.selectedItemState.Select(slot); p.selectedItemState.Update(); p.heldProj = held; p.channel = channel; p.noItems = noItems;
                p.itemAnimation = animation; p.itemTime = itemTime; p.direction = direction; p.itemRotation = rotation;
            }
        }
    }
}
