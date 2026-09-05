using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;

namespace Terraria
{
    public static class FocusHelper
    { public static bool IsSelectedApplication = true; public static bool AllowInputProcessing { get { return IsSelectedApplication; } } }

    public partial class Main
    {
        public static Main instance;
        public static bool dedServ, mouseLeft, mouseRight, mouseText, blockMouse, HoveringOverAnNPC;
        public static int netMode;
        public static KeyboardState keyState;
        public static Matrix UIScaleMatrix { get; set; } = Matrix.Identity;
        public int currentNPCShowingChatBubble = -1;
        public static int UseCount, TileUseCount, SelectedSlot, NpcHits, DropHits, SpecialInteractions, BubbleDraws, CursorDraws, DamageDraws;
        internal static bool SampleLeft, SampleRight, SampleF5;
        internal static int SampleX = 1850, SampleY = 900, SampleWheel;
        internal static bool SpecialNpc;
        internal static string PendingText, DrawnText;
        internal static bool PendingLocked;
        internal static bool OtherUiHover;
        internal const int VanillaLayerCount = 11;

        public Main() { instance = this; }

        private void FixtureInputUpdate()
        {
            PendingText = null; PendingLocked = false; // MouseOversClear before input.
            DoUpdate_HandleInput();
            if (LocalPlayer == null) return;
            LocalPlayer.controlUseItem = GameInput.PlayerInput.Triggers.Current.MouseLeft && !blockMouse && !LocalPlayer.mouseInterface;
            LocalPlayer.controlUseTile = GameInput.PlayerInput.Triggers.Current.MouseRight && !blockMouse && !LocalPlayer.mouseInterface;
            if (LocalPlayer.controlUseItem) UseCount++;
            if (LocalPlayer.controlUseTile) TileUseCount++;
            SelectedSlot = (SelectedSlot + GameInput.PlayerInput.ScrollWheelDelta / 120 + 100) % 10;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DoUpdate_HandleInput()
        {
            var input = GameInput.PlayerInput.Triggers;
            input.JustPressed.MouseLeft = SampleLeft && !input.Current.MouseLeft;
            input.JustPressed.MouseRight = SampleRight && !input.Current.MouseRight;
            input.JustReleased.MouseLeft = !SampleLeft && input.Current.MouseLeft;
            input.JustReleased.MouseRight = !SampleRight && input.Current.MouseRight;
            input.Current.MouseLeft = FocusHelper.AllowInputProcessing && SampleLeft;
            input.Current.MouseRight = FocusHelper.AllowInputProcessing && SampleRight;
            mouseLeft = input.Current.MouseLeft; mouseRight = input.Current.MouseRight;
            GameInput.PlayerInput.ScrollWheelValueOld = GameInput.PlayerInput.ScrollWheelValue;
            GameInput.PlayerInput.ScrollWheelValue += SampleWheel;
            GameInput.PlayerInput.ScrollWheelDelta = SampleWheel;
            GameInput.PlayerInput.ScrollWheelDeltaForUI = SampleWheel;
            GameInput.PlayerInput.MouseInfo = new MouseState(SampleX, SampleY, GameInput.PlayerInput.ScrollWheelValue,
                mouseLeft ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                mouseRight ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
            keyState = SampleF5 ? new KeyboardState(Keys.F5) : new KeyboardState();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HoverOverNPCs(Rectangle mouseRectangle)
        {
            // Deliberately do NOT put a mouseInterface/mouseText guard before
            // the intersection: the real NPC original lacks such an outer gate.
            if (!new Rectangle(SampleX - 5, SampleY - 5, 20, 20).Intersects(mouseRectangle)) return;
            NpcHits++;
            if (SpecialNpc && mouseRight) SpecialInteractions++;
            if (!mouseText) MouseTextNoLock("NPC");
            if (!LocalPlayer.mouseInterface)
            { HoveringOverAnNPC = true; currentNPCShowingChatBubble = 0; }
        }

        public static void ClearHoverItem() { }
        public static void NewText(string text, byte r = 255, byte g = 255, byte b = 255) { Console.WriteLine("FIXTURE_CHAT: " + text); }
        public void MouseTextNoOverride(string text, int rare = 0, byte diff = 0, int x = -1, int y = -1,
            int width = -1, int height = -1, int pushWidth = 0)
        { if (!PendingLocked) { PendingText = text; PendingLocked = true; } }
        private static void MouseTextNoLock(string text) { if (!PendingLocked) PendingText = text; }

        private List<UI.GameInterfaceLayer> CreateFixtureLayers()
        {
            return new List<UI.GameInterfaceLayer>
            {
                new UI.LegacyGameInterfaceLayer("Vanilla: Interface Logic 1", AlwaysContinue),
                new UI.LegacyGameInterfaceLayer("Vanilla: Emote Bubbles", () =>
                { if (currentNPCShowingChatBubble >= 0) BubbleDraws++; currentNPCShowingChatBubble = -1; DamageDraws++; return true; }),
                new UI.LegacyGameInterfaceLayer("Vanilla: Map / Minimap", AlwaysContinue, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Interface Logic 2", () => { mouseText = false; return true; }),
                new UI.LegacyGameInterfaceLayer("Vanilla: Mouse Text", () =>
                { if (OtherUiHover) LocalPlayer.mouseInterface = true; return true; }, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Cursor", () => { CursorDraws++; return true; }, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Mouse Item / NPC Head", () => { mouseText = false; return true; }, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Mouse Over", () =>
                {
                    if (!mouseText) { DropHits++; MouseTextNoLock("DROP"); }
                    HoveringOverAnNPC = false;
                    HoverOverNPCs(new Rectangle(SampleX, SampleY, 1, 1));
                    return true;
                }),
                new UI.LegacyGameInterfaceLayer("Vanilla: Interact Item Icon", AlwaysContinue, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Interface Logic 4", AlwaysContinue, UI.InterfaceScaleType.UI),
                new UI.LegacyGameInterfaceLayer("Fixture: End", AlwaysContinue)
            };
        }

        internal void DrawAllFixtureLayers()
        {
            LocalPlayer.mouseInterface = false; // DoDraw reset, after the update hook.
            DrawnText = null;
            foreach (UI.GameInterfaceLayer layer in _gameInterfaceLayers)
            {
                spriteBatch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, null, null, null, null, null, UIScaleMatrix);
                var device = spriteBatch.GraphicsDevice;
                var scissor = device.ScissorRectangle;
                var rasterizer = device.RasterizerState;
                var blend = device.BlendState;
                var depth = device.DepthStencilState;
                var sampler = device.SamplerStates[0];
                bool next = layer.Draw();
                if (layer.Name == "JueMingR: F5 Window" && (device.ScissorRectangle != scissor ||
                    !ReferenceEquals(device.RasterizerState, rasterizer) || !ReferenceEquals(device.BlendState, blend) ||
                    !ReferenceEquals(device.DepthStencilState, depth) || !ReferenceEquals(device.SamplerStates[0], sampler)))
                    throw new InvalidOperationException("F5 did not restore the actual XNA graphics state.");
                spriteBatch.End();
                if (!next) break;
            }
            DrawnText = PendingText;
            PendingLocked = false;
        }
    }
}

namespace Terraria.GameInput
{
    public class TriggersSet { public bool MouseLeft { get; set; } public bool MouseRight { get; set; } }
    public class TriggersPack
    {
        public TriggersSet Current = new TriggersSet(), JustPressed = new TriggersSet(), JustReleased = new TriggersSet();
    }
    public static class PlayerInput
    {
        public static TriggersPack Triggers = new TriggersPack();
        public static MouseState MouseInfo;
        public static Vector2 RawMouseScale = Vector2.One;
        public static int ScrollWheelValue, ScrollWheelValueOld, ScrollWheelDelta, ScrollWheelDeltaForUI;
        internal static Vector2 FixtureScreen = new Vector2(1920, 1080);
        public static Vector2 OriginalScreenSize { get { return FixtureScreen; } }
        public static bool UsingGamepad { get; set; }
        public static bool ShouldFastUseItem { get; set; }
    }
}

namespace Terraria.GameContent
{
    public static class FontAssets { public static Asset<DynamicSpriteFont> MouseText; }
    public static class TextureAssets
    { public static Asset<Microsoft.Xna.Framework.Graphics.Texture2D> SettingsPanel, InventoryBack13, InventoryBack, MagicPixel; }
}
