using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Items;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content.Readers;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.GameInput;

namespace Terraria
{
    internal static class ItemUiChecks
    {
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        internal static void Run(HostItems host, string content = null, string output = null)
        {
            using (var graphics = new F5FixtureGraphics())
            using (var shellRenderer = new F5Renderer())
            {
                var shell = new F5Interaction { Ready = true }; bool notesMayLeave = false;
                shell.BeforeLeave = page => notesMayLeave;
                var presentation = new ItemsPresentation(host, shell);
                host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 100 }).WithTypes(ItemListKind.Discard, new int[0])); host.PollPreferences();
                Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 4, favorited = true };
                Main.LocalPlayer.inventory[11] = new Item { type = 101, stack = 2 };
                Main.LocalPlayer.inventory[50] = new Item { type = 71, stack = 10 };
                Main.UIScaleMatrix = Matrix.Identity;
                shell.Update(new F5Input { Width = 1920, Height = 1080, Scale = 1, Active = true, Focused = true, F5 = true }); shell.Navigate(0);
                Action prepare = () => { shellRenderer.RefreshResources(); shellRenderer.Prepare(shell, 1920, 1080, 1); presentation.Prepare(true, Matrix.Identity, new Vector2(1920, 1080)); };
                prepare();
                Control(presentation, "Enable", (int)ItemActionKind.Stack);
                Control(presentation, "Disable", (int)ItemActionKind.Stack);
                object add = Control(presentation, "Add", (int)ItemListKind.Sell);
                Pointer(presentation, add, true); Check(!presentation.Modal, "picker opens on release only");
                Pointer(presentation, add, false); prepare();
                Check(presentation.Modal && !host.Preferences.Value.SellTypes.Contains(8), "picker draft does not change running list");
                Check(HasControl(presentation, "Select", 8) && !HasControl(presentation, "Select", 71) && !HasControl(presentation, "Select", 100), "favorite supplies type; coins and duplicates excluded");
                Click(presentation, Control(presentation, "Select", type: 8)); prepare();
                Click(presentation, Control(presentation, "Select", type: 101)); prepare();
                Check(!host.Preferences.Value.SellTypes.Contains(8), "multi-select stays draft");
                Check(!shell.BeforeLeave(9) && presentation.Modal, "existing denied navigation callback preserves draft");
                Click(presentation, Control(presentation, "Confirm")); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 100, 101 }) && Main.LocalPlayer.inventory[10].stack == 4, "confirm commits types without acting on representative items");
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Click(presentation, Control(presentation, "Select", type: 8)); prepare();
                Click(presentation, Control(presentation, "Cancel")); prepare();
                Check(host.Preferences.Value.DiscardTypes.Count == 0, "cancel drops draft");
                Main.LocalPlayer.inventory[12] = new Item { type = 102, stack = 1 };
                Click(presentation, Control(presentation, "Replace", (int)ItemListKind.Sell, 100)); prepare();
                Click(presentation, Control(presentation, "Select", type: 102)); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 101, 102 }), "replace commits exactly one type");
                Click(presentation, Control(presentation, "Remove", (int)ItemListKind.Sell, 101)); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 102 }), "remove changes only selected list member");
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                bool inventoryTriggered = !PlayerInput.WritingText; PlayerInput.WritingText = false;
                Main.keyState = new KeyboardState(Keys.Escape); presentation.ProcessInput(true, Main.keyState, Vector2.Zero); prepare();
                Check(!inventoryTriggered && !presentation.Modal && Main.keyState.GetPressedKeys().Length == 0, "Esc cancels picker without native inventory action");
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                Check(PlayerInput.WritingText, "held Esc tail blocks next original input mapping");
                PlayerInput.WritingText = false; Main.keyState = new KeyboardState(); presentation.ProcessInput(true, Main.keyState, Vector2.Zero); prepare();
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                notesMayLeave = true;
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Check(shell.BeforeLeave(9) && !presentation.Modal, "accepted leave cancels picker without replacing Notes gate");
                prepare();
                Draw(graphics, shellRenderer, shell, presentation, null);
                // A taller offset font and skin replacement use the same real
                // draw pass, clipping, texture ownership and input rectangles.
                FontAssets.MouseText = graphics.Asset("item-offset-font", graphics.CreateFont(12, 26, -3, 8, 39)); prepare();
                Draw(graphics, shellRenderer, shell, presentation, null);
                // The main page remains usable even when the picker would be
                // too short at a legal small logical viewport and tall font.
                presentation.Prepare(true, Matrix.Identity, new Vector2(800, 220));
                Check(!presentation.Modal, "small viewport preserves ordinary page"); prepare();
                ItemAutomationSettings beforeLongList = host.Preferences.Value;
                host.Change(beforeLongList.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 5000))); prepare();
                Check(((ICollection)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(presentation)).Count < 100, "large valid list builds only visible controls");
                shell.ScrollTo(shell.Layout.MaxScroll); prepare(); Draw(graphics, shellRenderer, shell, presentation, null);
                host.Change(beforeLongList); shell.ScrollTo(0); prepare();
                if (content != null)
                {
                    var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                    using (var reader = new XnbReader(services))
                    using (Texture2D skin = Read<Texture2D>(reader, Path.Combine(content, "Images/Inventory_Back.xnb")))
                    {
                        FontAssets.MouseText = graphics.Asset("actual-item-font", Read<DynamicSpriteFont>(reader, Path.Combine(content, "Fonts/Mouse_Text.xnb")));
                        TextureAssets.InventoryBack = graphics.Asset("actual-item-skin", skin);
                        var icons = new System.Collections.Generic.List<Texture2D>();
                        foreach (int type in new[] { 8, 100, 101, 102 })
                        {
                            var texture = Read<Texture2D>(reader, Path.Combine(content, "Images/Item_" + type + ".xnb"));
                            icons.Add(texture); TextureAssets.Item[type] = graphics.Asset("actual-item-" + type, texture);
                        }
                        prepare(); Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-original.png"));
                        shell.ScrollTo(shell.Layout.MaxScroll); prepare();
                        Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                        Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-picker.png"));
                        Click(presentation, Control(presentation, "Cancel")); prepare();
                        shell.ScrollTo(0); prepare();
                        using (var replacement = new Texture2D(graphics.Device, 32, 32))
                        {
                            replacement.SetData(Enumerable.Repeat(new Color(40, 88, 64), 1024).ToArray());
                            TextureAssets.InventoryBack = graphics.Asset("synthetic-item-reskin", replacement); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-reskin.png"));
                            presentation.Suspend(); Check(!replacement.IsDisposed && !skin.IsDisposed, "borrowed current and old skins survive suspension");
                        }
                        foreach (Texture2D icon in icons) icon.Dispose();
                    }
                }
                presentation.Suspend();
                object renderer = typeof(ItemsPresentation).GetField("renderer", Fields).GetValue(presentation);
                Check(renderer.GetType().GetField("clipped", Fields).GetValue(renderer) == null, "owned rasterizer released on suspend");
                Console.WriteLine("PASS: Items production picker, release commit, draft/cancel/navigation, real XNA clipping/state and font replacement. Preview uses fixture item labels.");
            }
        }
        private static void Draw(F5FixtureGraphics graphics, F5Renderer renderer, F5Interaction shell, ItemsPresentation items, string path)
        {
            using (var target = new RenderTarget2D(graphics.Device, 1920, 1080))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                Rectangle clip = graphics.Device.ScissorRectangle;
                renderer.Draw(shell, Matrix.Identity, false, false); items.Draw();
                Check(graphics.Device.ScissorRectangle == clip, "item pass restores caller scissor");
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 8, 8), Color.Red);
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[1920 * 1080]; target.GetData(pixels);
                Check(pixels[3 * 1920 + 3].R == 255 && pixels[100 * 1920 + 100].A == 0, "restored batch still draws; distant pixels stay clipped");
                F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); bool ink = false;
                for (int y = (int)view.Y; y < view.Bottom; y++) for (int x = (int)view.X; x < view.Right; x++) ink |= pixels[y * 1920 + x].A != 0;
                Check(ink, "item content produces real pixels");
                if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(path)); using (var stream = File.Create(path)) target.SaveAsPng(stream, 1920, 1080); }
            }
        }
        private static object Control(ItemsPresentation presentation, string command, int argument = -1, int type = -1)
        {
            foreach (object control in (IEnumerable)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(presentation))
            {
                Type t = control.GetType();
                if (t.GetField("Command", Fields).GetValue(control).ToString() == command &&
                    (argument < 0 || (int)t.GetField("Argument", Fields).GetValue(control) == argument) &&
                    (type < 0 || (int)t.GetField("Type", Fields).GetValue(control) == type)) return control;
            }
            throw new InvalidOperationException("Expected visible control missing: " + command + "/" + argument + "/" + type);
        }
        private static bool HasControl(ItemsPresentation p, string command, int type) { try { Control(p, command, type: type); return true; } catch (InvalidOperationException) { return false; } }
        private static void Pointer(ItemsPresentation presentation, object control, bool pressed)
        {
            F5Rect rect = (F5Rect)control.GetType().GetField("Rect", Fields).GetValue(control);
            var point = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            PlayerInput.MouseInfo = new MouseState((int)point.X, (int)point.Y, 0, pressed ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            presentation.ProcessInput(true, new KeyboardState(), point);
        }
        private static void Click(ItemsPresentation presentation, object control) { Pointer(presentation, control, true); Pointer(presentation, control, false); }
        private static T Read<T>(XnbReader reader, string path) where T : class { using (var file = File.OpenRead(path)) return reader.FromStream<T>(file); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("ITEM UI CHECK FAILED: " + message); }
        private sealed class GraphicsService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; }
            internal GraphicsService(GraphicsDevice device) { GraphicsDevice = device; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
    }
}
