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
    { public static bool IsSelectedApplication = true; public static bool AllowInputProcessing { [MethodImpl(MethodImplOptions.NoInlining)] get { return IsSelectedApplication; } } }

    public partial class Main
    {
        public static Main instance;
        public static bool dedServ, mouseLeft, mouseRight, mouseText, blockMouse, HoveringOverAnNPC;
        public static bool mapFullscreen, hideUI, onlyDrawFancyUI, ingameOptionsWindow, inFancyUI;
        public static int netMode;
        public static KeyboardState keyState;
        public static KeyboardState oldKeyState;
        public static Matrix UIScaleMatrix { get; set; } = Matrix.Identity;
        public int currentNPCShowingChatBubble = -1;
        public static int UseCount, TileUseCount, SelectedSlot, NpcHits, DropHits, SpecialInteractions, BubbleDraws, CursorDraws, DamageDraws;
        internal static bool SampleLeft, SampleRight, SampleF5, SampleCapture, SampleMap, SampleShift, SampleControl;
        internal static int SampleX = 1850, SampleY = 900, SampleWheel;
        internal static bool SpecialNpc;
        internal static string PendingText, DrawnText;
        internal static bool PendingLocked;
        internal static bool OtherUiHover;
        internal static int NativeClicks, NativeWheel, PendingMeasurements;
        internal const int VanillaLayerCount = 15;

        public Main() { instance = this; }

        private void FixtureInputUpdate()
        {
            PendingText = null; PendingLocked = false; // MouseOversClear before input.
            if (keyState.IsKeyDown(Keys.F11)) NativeEarlyKeyActions++;
            DoUpdate_HandleInput();
            if (NativeMode || SampleCapture || SampleMap)
            {
                if (GameInput.PlayerInput.Triggers.Current.MouseLeft) NativeClicks++;
                NativeWheel += GameInput.PlayerInput.ScrollWheelDeltaForUI / 120;
            }
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
            GameInput.PlayerInput.UpdateInput();
            // Real .8 consumes mapped zoom/navigation before the outer input
            // postfix, then refreshes keyState after those consumers.
            if (GameInput.PlayerInput.Triggers.Current.KeyStatus["ViewZoomIn"]) NativeZoom++;
            var keys = new List<Keys>();
            if (SampleF5) keys.Add(Keys.F5);
            if (SampleShift) keys.Add(Keys.LeftShift);
            if (SampleControl) keys.Add(Keys.LeftControl);
            oldKeyState = keyState;
            keyState = FocusHelper.AllowInputProcessing ? new KeyboardState(keys.ToArray()) : default(KeyboardState);
        }
        internal static int NativeZoom, NativeEarlyKeyActions;
        public static bool inputTextEnter, inputTextEscape;
        internal static int NativeTextReads;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetInputText(string oldString, bool allowMultiLine = false)
        {
            if (!FocusHelper.AllowInputProcessing) return oldString;
            inputTextEnter = inputTextEscape = false; NativeTextReads++;
            for (int i = 0; i < keyCount; i++)
            {
                if (keyInt[i] == 13) inputTextEnter = true;
                else if (keyInt[i] == 27) inputTextEscape = true;
                else oldString += keyString[i];
            }
            keyCount = 0;
            return oldString;
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
                new UI.LegacyGameInterfaceLayer("Vanilla: Capture Manager Check", () =>
                {
                    if (SampleCapture) Graphics.Capture.CaptureManager.Instance.Active = true;
                    if (Graphics.Capture.CaptureManager.Instance.Active) MouseTextNoLock("CAPTURE");
                    return !Graphics.Capture.CaptureManager.Instance.Active;
                }),
                new UI.LegacyGameInterfaceLayer("Vanilla: Ingame Options", () => !ingameOptionsWindow),
                new UI.LegacyGameInterfaceLayer("Vanilla: Fancy UI", () => !inFancyUI),
                new UI.LegacyGameInterfaceLayer("Vanilla: Achievement Complete Popups", AlwaysContinue),
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
            if (mapFullscreen || hideUI || onlyDrawFancyUI) return; // DoDraw skips DrawInterface altogether.
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
            if (PendingText != null)
            {
                // DrawPendingMouseText -> MouseTextInner measures non-null text.
                GameContent.FontAssets.MouseText.Value.MeasureString(PendingText);
                PendingMeasurements++;
            }
            PendingLocked = false;
        }

        internal static bool NativeMode { get { return mapFullscreen || hideUI || onlyDrawFancyUI ||
            ingameOptionsWindow || inFancyUI || Graphics.Capture.CaptureManager.Instance.Active; } }
    }
}

namespace Terraria.Graphics.Capture
{
    public class CaptureManager
    {
        public static CaptureManager Instance = new CaptureManager();
        public bool Active { get; set; }
    }
}

namespace Terraria.GameInput
{
    public class TriggersSet
    {
        public Dictionary<string, bool> KeyStatus = new Dictionary<string, bool> { { "MouseLeft", false }, { "MouseRight", false },
            { "MapFull", false }, { "ToggleCameraMode", false }, { "ViewZoomIn", false } };
        public bool MouseLeft { get { return KeyStatus["MouseLeft"]; } set { KeyStatus["MouseLeft"] = value; } }
        public bool MouseRight { get { return KeyStatus["MouseRight"]; } set { KeyStatus["MouseRight"] = value; } }
        public bool MapFull { get { return KeyStatus["MapFull"]; } set { KeyStatus["MapFull"] = value; } }
        public bool ToggleCameraMode { get { return KeyStatus["ToggleCameraMode"]; } set { KeyStatus["ToggleCameraMode"] = value; } }
    }
    public class TriggersPack
    {
        public TriggersSet Current = new TriggersSet(), Old = new TriggersSet(), JustPressed = new TriggersSet(), JustReleased = new TriggersSet();
    }
    public static partial class PlayerInput
    {
        public static TriggersPack Triggers = new TriggersPack();
        public static MouseState MouseInfo;
        public static Vector2 RawMouseScale = Vector2.One;
        public static int ScrollWheelValue, ScrollWheelValueOld, ScrollWheelDelta, ScrollWheelDeltaForUI;
        internal static Vector2 FixtureScreen = new Vector2(1920, 1080);
        public static Vector2 OriginalScreenSize { get { return FixtureScreen; } }
        public static bool UsingGamepad { get; set; }
        public static bool ShouldFastUseItem { get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateInput()
        {
            foreach (string key in new List<string>(Triggers.Current.KeyStatus.Keys))
            { Triggers.Old.KeyStatus[key] = Triggers.Current.KeyStatus[key]; Triggers.Current.KeyStatus[key] = false; }
            bool permitted = FocusHelper.AllowInputProcessing;
            Triggers.Current.MouseLeft = permitted && Main.SampleLeft;
            Triggers.Current.MouseRight = permitted && Main.SampleRight;
            Triggers.Current.MapFull = permitted && Main.SampleMap;
            Triggers.Current.ToggleCameraMode = permitted && Main.SampleCapture;
            Triggers.Current.KeyStatus["ViewZoomIn"] = !WritingText && Main.keyState.IsKeyDown(Keys.Z);
            foreach (string key in Triggers.Current.KeyStatus.Keys)
            { Triggers.JustPressed.KeyStatus[key] = Triggers.Current.KeyStatus[key] && !Triggers.Old.KeyStatus[key];
                Triggers.JustReleased.KeyStatus[key] = !Triggers.Current.KeyStatus[key] && Triggers.Old.KeyStatus[key]; }
            ScrollWheelValueOld = ScrollWheelValue; ScrollWheelValue += Main.SampleWheel;
            ScrollWheelDelta = ScrollWheelDeltaForUI = permitted ? Main.SampleWheel : 0;
            Main.mouseLeft = Triggers.Current.MouseLeft; Main.mouseRight = Triggers.Current.MouseRight;
            MouseInfo = new MouseState(Main.SampleX, Main.SampleY, ScrollWheelValue,
                Main.mouseLeft ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                Main.mouseRight ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
            WritingText = false;
        }
    }
}

namespace Terraria.GameContent
{
    public static class FontAssets { public static Asset<DynamicSpriteFont> MouseText; }
    public static partial class TextureAssets
    { public static Asset<Microsoft.Xna.Framework.Graphics.Texture2D> SettingsPanel, InventoryBack13, InventoryBack, MagicPixel; }
}
