using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Controlled managed-component checks, no Main loop, graphics or OS input.
    // Camera writes here prove the predicate/consumer gap only, never L3 motion.
    internal static class NativeTargetMotionInputChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void CameraPredicate(Assembly assembly, object catalog)
        {
            Main.gameMenu = Main.mapFullscreen = Main.hideUI = Main.playerInventory = false;
            Main.netMode = Main.myPlayer = 0; Main.LocalPlayer.chest = -1; Main.LocalPlayer.gravDir = 1;
            Main.LocalPlayer.position = new Vector2(800, 800);
            Main.maxTilesX = Main.maxTilesY = 64; Main.tile = new Tile[64, 64]; Main.tile[8, 8] = new Tile();
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640;
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960);
            typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            PlayerInput.MouseInfo = new MouseState(136, 136, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            PlayerInput.MouseX = Main.mouseX = 136; PlayerInput.MouseY = Main.mouseY = 136; Main.keyState = new KeyboardState();
            PlayerInput.Triggers.Initialize(); FocusHelper.IsSelectedApplication = true;
            var inputType = assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState");
            object input = Activator.CreateInstance(inputType, Flags, null, new object[] { (Func<IntPtr>)(() => new IntPtr(1)), (Func<IntPtr>)(() => new IntPtr(1)) }, null);
            Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
            var type = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.ReadOnlyTargetGesture");
            object gesture = Activator.CreateInstance(type, Flags, null, new[] { input, catalog }, null);
            try
            {
                Set(gesture, "CanPick", (Func<bool>)(() => true));
                HotkeyChord chord; string reason; HotkeyChord.TryParse("K", out chord, out reason);
                Set(gesture, "QueryBinding", (Func<HotkeyChord>)(() => chord));
                int delivered = 0; object target = null;
                Set(gesture, "Picked", Adapt(type.GetProperty("Picked", Flags).PropertyType, value => { delivered++; target = value; }));
                Pass(gesture); Call(gesture, "Request", 1);
                var frozen = Get(gesture, "frozen");
                Main.screenPosition = new Vector2(16, 0); Pass(gesture); Call(gesture, "Update", 1L);
                Console.WriteLine("MOTION-PREDICATE layer=L1 cameraDelta=16,0 delivered=" + delivered + " original=" + ReferenceEquals(target, frozen) + " full-native-motion=NOT_RUN");
                Require(delivered == 1 && ReferenceEquals(target, frozen), "normal camera travel must preserve the already-frozen world target");
                var observer = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("World", Flags);
                NPC savedNpc = Main.npc[0];
                try
                {
                    Main.screenPosition = Vector2.Zero;
                    var npc = new NPC { active = true, life = 20, lifeMax = 20, type = 1, netID = 1, position = new Vector2(130, 120), width = 60, height = 60 };
                    Main.npc[0] = npc; object actor = observer.Invoke(null, new object[] { new Vector2(136, 136), catalog, true });
                    Main.screenPosition = new Vector2(175, 0);
                    Require((bool)Call(actor, "StillMatches"), "actor hitbox remains partly visible with center and original click outside the viewport");
                    npc.position.X = 50; Require(!(bool)Call(actor, "StillMatches"), "fully offscreen frozen actor is rejected");
                    npc.position.X = 180; typeof(NPC).GetProperty("generation", Flags).SetValue(npc, (byte)(npc.generation + 1), null);
                    Require(!(bool)Call(actor, "StillMatches"), "controlled native generation mutation cannot reuse the frozen actor");
                }
                finally { Main.npc[0] = savedNpc; Main.screenPosition = Vector2.Zero; }
                SnapshotBoundaries(assembly);
                TransformOracle(assembly);
                NativeProducers(gesture);
            }
            finally { ((IDisposable)gesture).Dispose(); Main.screenPosition = Vector2.Zero; }
        }
        private static void TransformOracle(Assembly assembly)
        {
            var matrix = Main.GameViewMatrix; Matrix ui = Main.UIScaleMatrix; Vector2 scale = PlayerInput.RawMouseScale;
            int sx = Main.screenWidth, sy = Main.screenHeight, px = PlayerInput.MouseX, py = PlayerInput.MouseY, mx = Main.mouseX, my = Main.mouseY, lx = Main.lastMouseX, ly = Main.lastMouseY;
            int originalMouseX = (int)typeof(PlayerInput).GetField("_originalMouseX", Flags).GetValue(null), originalMouseY = (int)typeof(PlayerInput).GetField("_originalMouseY", Flags).GetValue(null);
            float gravity = Main.LocalPlayer.gravDir; MouseState mouse = PlayerInput.MouseInfo;
            var original = PlayerInput.OriginalScreenSize;
            MethodInfo matches = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.TargetInputSnapshot").GetMethod("WorldInputMatches", Flags);
            int cases = 0;
            try
            {
                foreach (int width in new[] { 960, 1280 }) foreach (float zoom in new[] { 1f, 1.4f })
                foreach (float rawScale in new[] { 1f, 1.25f }) foreach (float uiScale in new[] { 1f, 1.5f }) foreach (float grav in new[] { 1f, -1f })
                {
                    Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, width, 720)); Main.GameViewMatrix.Zoom = new Vector2(zoom);
                    Main.screenWidth = width; Main.screenHeight = 720; Main.LocalPlayer.gravDir = grav;
                    typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, width); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 720);
                    typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.CreateScale(uiScale, uiScale, 1));
                    typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, new Vector2(rawScale));
                    PlayerInput.MouseInfo = new MouseState(73, 89, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    PlayerInput.MouseX = Main.mouseX = (int)(73 * rawScale); PlayerInput.MouseY = Main.mouseY = (int)(89 * rawScale);
                    // The independent oracle is the locked native transform,
                    // including its own integer truncation and RenderZoom.
                    PlayerInput.CacheMousePositionForZoom(); PlayerInput.SetZoom_MouseInWorld();
                    Require((bool)matches.Invoke(null, null), "host authenticates actual native mapping across raw/UI/world zoom/viewport/gravity");
                    Main.mouseX++; Require(!(bool)matches.Invoke(null, null), "temporary Host/native point writer cannot mix with the frozen raw sample"); Main.mouseX--;
                    PlayerInput.MouseX++; Require(!(bool)matches.Invoke(null, null), "stale/navigated native cache cannot masquerade as current MouseInfo"); PlayerInput.MouseX--; cases++;
                }
                Console.WriteLine("PASS target transform L1: " + cases + " actual native CacheMousePositionForZoom/SetZoom_MouseInWorld oracle cases plus mismatched/cache-writer negatives; no OS or Draw.");
            }
            finally
            {
                Main.GameViewMatrix = matrix; Main.screenWidth = sx; Main.screenHeight = sy; Main.LocalPlayer.gravDir = gravity;
                typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, ui); typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, scale);
                typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, (int)original.X); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, (int)original.Y);
                PlayerInput.MouseInfo = mouse; PlayerInput.MouseX = px; PlayerInput.MouseY = py; Main.mouseX = mx; Main.mouseY = my;
                Main.lastMouseX = lx; Main.lastMouseY = ly;
                typeof(PlayerInput).GetField("_originalMouseX", Flags).SetValue(null, originalMouseX); typeof(PlayerInput).GetField("_originalMouseY", Flags).SetValue(null, originalMouseY);
            }
        }
        private static int cosmeticDraws;
        private static void NativeProducers(object targets)
        {
            var isolated = new Harmony("JueMingR.Tests.TargetUiProducers");
            var oldBatch = Main.spriteBatch; var oldBack = Terraria.GameContent.TextureAssets.InventoryBack13;
            var oldIcons = (ReLogic.Content.Asset<Texture2D>[])Terraria.GameContent.TextureAssets.InfoIcon.Clone();
            var oldFont = Terraria.GameContent.FontAssets.ItemStack; var oldMouseFont = Terraria.GameContent.FontAssets.MouseText;
            int watch = Main.LocalPlayer.accWatch; bool inventory = Main.playerInventory; int equipPage = Main.EquipPage;
            int oldNpcCount = Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsTotal, oldNpcColumns = Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsPerColumn;
            var points = new Dictionary<int, Terraria.UI.Gamepad.UILinkPoint>();
            try
            {
                // Only terminal display calls are sinks. Real notification
                // opacity/layout/DrawInvBG and DrawInfoAccs selection/layout/
                // transpiled icon callsite run, with no device or Main loop.
                foreach (var method in typeof(SpriteBatch).GetMethods().Where(m => m.Name == "Draw"))
                    isolated.Patch(method, prefix: new HarmonyMethod(typeof(NativeTargetMotionInputChecks).GetMethod(nameof(NoDisplay), Flags)));
                foreach (var method in typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethods().Where(m => m.Name == "DrawString"))
                    isolated.Patch(method, prefix: new HarmonyMethod(typeof(NativeTargetMotionInputChecks).GetMethod(nameof(NoDisplay), Flags)));
                foreach (var method in typeof(Utils).GetMethods().Where(m => m.Name == "DrawBorderString"))
                    isolated.Patch(method, prefix: new HarmonyMethod(typeof(NativeTargetMotionInputChecks).GetMethod(nameof(NoDisplay), Flags)));
                Main.spriteBatch = (SpriteBatch)FormatterServices.GetUninitializedObject(typeof(SpriteBatch)); GC.SuppressFinalize(Main.spriteBatch);
                FiniteCostChecks.SetCpuFont(10);
                Terraria.GameContent.TextureAssets.InventoryBack13 = CpuTexture(52, 52);
                for (int i = 0; i < Terraria.GameContent.TextureAssets.InfoIcon.Length; i++) Terraria.GameContent.TextureAssets.InfoIcon[i] = CpuTexture(20, 20);
                Terraria.GameContent.FontAssets.ItemStack = Terraria.GameContent.FontAssets.MouseText;
                var popup = (Terraria.UI.InGamePopups.AchievementUnlockedPopup)FormatterServices.GetUninitializedObject(typeof(Terraria.UI.InGamePopups.AchievementUnlockedPopup));
                Set(popup, "_ingameDisplayTimeLeft", 16); Set(popup, "_title", "receipt"); Set(popup, "_achievementTexture", CpuTexture(64, 64)); Set(popup, "_achievementBorderTexture", CpuTexture(72, 72)); Set(popup, "_achievementIconFrame", new Rectangle(0, 0, 64, 64));
                object snapshot = Get(targets, "snapshot"); cosmeticDraws = 0;
                Call(targets, "BeginPass"); popup.DrawInGame(Main.spriteBatch, new Vector2(500, 400));
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true);
                Require((int)Get(snapshot, "committedCount") == 1 && cosmeticDraws > 0, "real notification DrawInGame reaches normal-return DrawInvBG producer and commits one actual rectangle");
                object entry = ((Array)Get(snapshot, "committed")).GetValue(0); Vector4 rect = (Vector4)Get(entry, "Rect"); popup.Update();
                Require(!(bool)Call(snapshot, "Freeze", new Vector2((rect.X + rect.Z) / 2, (rect.Y + rect.W) / 2)), "native producer receipt blocks the expired notification at the trigger-visible point"); Call(snapshot, "Retire");

                Main.playerInventory = true; Main.EquipPage = 0; Main.LocalPlayer.accWatch = 1;
                Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsTotal = 0; Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsPerColumn = 8;
                for (int i = 1558; i < 1571; i++)
                {
                    Terraria.UI.Gamepad.UILinkPoint point; points[i] = Terraria.UI.Gamepad.UILinkPointNavigator.Points.TryGetValue(i, out point) ? point : null;
                    Terraria.UI.Gamepad.UILinkPointNavigator.Points[i] = new Terraria.UI.Gamepad.UILinkPoint(i, true, -1, -1, -1, -1);
                }
                object[] args = { 0, 0, 0, 0 }; typeof(Main).GetMethod("GetInfoAccIconPosition", Flags).Invoke(null, args);
                Vector2 icon = new Vector2((int)args[2], (int)args[3]);
                Call(targets, "BeginPass");
                Call(snapshot, "Record", Main.LocalPlayer.inventory, 0, 0, icon, 20f, 20f);
                var main = FormatterServices.GetUninitializedObject(typeof(Main)); GC.SuppressFinalize(main);
                typeof(Main).GetMethod("DrawInfoAccs", Flags).Invoke(main, null);
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true);
                Require((int)Get(snapshot, "committedCount") >= 2 && !(bool)Call(snapshot, "Freeze", icon + Vector2.One),
                    "actual DrawInfoAccs icon callsite records its real later rectangle above an earlier slot"); Call(snapshot, "Retire");
                Console.WriteLine("PASS target UI producers L2: original AchievementUnlockedPopup.DrawInGame->DrawInvBG normal return and original DrawInfoAccs patched icon callsite->ordered blocker receipt. Texture dimensions/font and final display calls are controlled; GPU/full DrawInterface/real render NOT RUN.");
            }
            finally
            {
                isolated.UnpatchAll(isolated.Id); Main.spriteBatch = oldBatch; Terraria.GameContent.TextureAssets.InventoryBack13 = oldBack;
                Array.Copy(oldIcons, Terraria.GameContent.TextureAssets.InfoIcon, oldIcons.Length); Terraria.GameContent.FontAssets.ItemStack = oldFont; Terraria.GameContent.FontAssets.MouseText = oldMouseFont;
                Main.LocalPlayer.accWatch = watch; Main.playerInventory = inventory; Main.EquipPage = equipPage;
                Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsTotal = oldNpcCount; Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsPerColumn = oldNpcColumns;
                foreach (var pair in points) { if (pair.Value == null) Terraria.UI.Gamepad.UILinkPointNavigator.Points.Remove(pair.Key); else Terraria.UI.Gamepad.UILinkPointNavigator.Points[pair.Key] = pair.Value; }
            }
        }
        private static ReLogic.Content.Asset<Texture2D> CpuTexture(int width, int height)
        {
            var texture = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D)); GC.SuppressFinalize(texture);
            typeof(Texture2D).GetField("_width", Flags).SetValue(texture, width); typeof(Texture2D).GetField("_height", Flags).SetValue(texture, height);
            var asset = (ReLogic.Content.Asset<Texture2D>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<Texture2D>), Flags, null, new object[] { "target-cpu-texture" }, null);
            asset.GetType().GetMethod("SubmitLoadedContent", Flags).Invoke(asset, new object[] { texture, new ReLogic.Content.Sources.FileSystemContentSource(Terraria.Program.SavePath) }); return asset;
        }
        private static bool NoDisplay() { cosmeticDraws++; return false; }
        private static void SnapshotBoundaries(Assembly assembly)
        {
            Type type = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.TargetInputSnapshot");
            object snapshot = Activator.CreateInstance(type, true);
            Item oldSlot = Main.LocalPlayer.inventory[0]; bool inventory = Main.playerInventory, enabled = Main.mapEnabled; int mapStyle = Main.mapStyle;
            int oldBuff = Main.LocalPlayer.buffType[0];
            try
            {
                Main.playerInventory = true; var item = new Item(); item.SetDefaults(9); item.stack = 4; Main.LocalPlayer.inventory[0] = item;
                var rect = new Rectangle(20, 20, 80, 40);
                Type popupType = typeof(Terraria.UI.InGamePopups.AchievementUnlockedPopup);
                object popup = FormatterServices.GetUninitializedObject(popupType);
                popupType.GetField("_ingameDisplayTimeLeft", Flags).SetValue(popup, 16);
                Require((float)popupType.GetProperty("Opacity", Flags).GetValue(popup, null) > 0, "native notification is still visible at time16");
                Call(snapshot, "BeginDraw"); Call(snapshot, "RecordBlocker", rect); Call(snapshot, "EndDraw", true);
                popupType.GetMethod("Update", Flags).Invoke(popup, null);
                Require((float)popupType.GetProperty("Opacity", Flags).GetValue(popup, null) == 0 && !(bool)Call(snapshot, "Freeze", new Vector2(30, 30)),
                    "native notification expires before input but its last displayed rectangle still prevents world fallback");
                Call(snapshot, "BeginDraw"); Call(snapshot, "RecordBlocker", rect);
                Call(snapshot, "Record", Main.LocalPlayer.inventory, 0, 0, new Vector2(20, 20), 44.2f, 44.2f); Call(snapshot, "EndDraw", true);
                Require((bool)Call(snapshot, "Freeze", new Vector2(30, 30)) && (bool)Get(snapshot, "HasSlot"), "later native slot painting can cover an earlier notification receipt");
                Call(snapshot, "BeginDraw"); Call(snapshot, "Record", Main.LocalPlayer.inventory, 0, 0, new Vector2(20, 20), 44.2f, 44.2f);
                Call(snapshot, "RecordBlocker", rect); Call(snapshot, "EndDraw", true);
                Require(!(bool)Call(snapshot, "Freeze", new Vector2(30, 30)), "a later blocker cannot be defeated by drawing the same slot earlier");
                Main.playerInventory = false; Main.mapEnabled = true; Main.mapStyle = 1;
                Call(snapshot, "BeginDraw"); Call(snapshot, "EndDraw", true); Main.mapStyle = 2;
                Require(!(bool)Call(snapshot, "Freeze", new Vector2(300, 300)), "native minimap-style change before trigger invalidates the old visible UI context");
                Main.mapStyle = 1; Call(snapshot, "BeginDraw"); Call(snapshot, "EndDraw", true);
                Main.LocalPlayer.buffType[0] = oldBuff == 0 ? 2 : 0;
                Require(!(bool)Call(snapshot, "Freeze", new Vector2(300, 300)), "buff icon removal/reordering cannot become a world click-through");
                Console.WriteLine("PASS target snapshot L1: native notification16->15 opacity transition plus controlled blocker receipt/order, minimap-style and buff-icon identity. Actual notification graphics primitive NOT RUN.");
            }
            finally { Main.LocalPlayer.inventory[0] = oldSlot; Main.playerInventory = inventory; Main.mapEnabled = enabled; Main.mapStyle = mapStyle; Main.LocalPlayer.buffType[0] = oldBuff; }
        }
        internal static void Pass(object targets)
        {
            Call(targets, "BeginPass"); Main.LocalPlayer.mouseInterface = Main.mouseText = false;
            Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true);
        }
        internal static void Consumers(object context, Func<int> outputCount, Func<string> outputText)
        {
            object browser = Get(context, "Browser"), targets = Get(browser, "Targets"), input = Get(context, "Input"), shell = Get(context, "Shell"), state = Get(shell, "State");
            object announcements = Get(browser, "Announcements"), catalog = Get(Get(browser, "Knowledge"), "Native");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            HotkeyChord oldSend = bindings.Get("announcement.send"), oldQuery = bindings.Get("item-browser.query");
            bool oldEnabled = (bool)Get(announcements, "Enabled"), oldLayers = (bool)Get(shell, "LayersReady"), oldReady = (bool)Get(state, "Ready"), oldInventory = Main.playerInventory;
            var oldDrops = Main.item; var oldCamera = Main.screenPosition; var oldPosition = Main.LocalPlayer.position;
            var oldDust = Main.dust;
            Player player = Main.LocalPlayer;
            Item oldUseSlot = player.inventory[3]; int oldSelected = player.selectedItem, oldLife = player.statLife, oldLifeMax = player.statLifeMax2;
            int oldPotionDelay = player.potionDelay;
            int[] oldBuffTypes = (int[])player.buffType.Clone(), oldBuffTimes = (int[])player.buffTime.Clone();
            var useIsolation = new Harmony("JueMingR.Tests.TargetInputItemUse");
            MethodInfo hitbox = typeof(Item).GetMethod("GetDrawHitbox", Flags, null, new[] { typeof(int), typeof(Player) }, null);
            var oldKeyboard = (Func<KeyboardState>)Get(input, "ReadOnlyKeyboardSource");
            Action<string, HotkeyChord> bind = (id, chord) =>
            {
                long command; string reason; Require(bindings.TrySet(id, chord, null, out command, out reason), "motion fixture binding: " + reason);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            };
            HotkeyChord send; string parse; Require(HotkeyChord.TryParse("LeftShift+LeftAlt+Mouse1", out send, out parse), "motion chord parses");
            int x = 30, y = 328, nativeMouseRequests = 0;
            Action<Keys[], Keys[], bool> sample = (early, final, down) =>
            {
                KeyboardState earlySample = new KeyboardState(early), finalSample = new KeyboardState(final);
                Set(input, "ReadOnlyKeyboardSource", (Func<KeyboardState>)(() => earlySample));
                Call(input, "BeginUpdate");
                PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.MouseX = Main.mouseX = x; PlayerInput.MouseY = Main.mouseY = y;
                Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                var tokens = new List<string>(); if (down) tokens.Add("Mouse1"); Call(input, "AfterNativeMouse", tokens);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = tokens.Contains("Mouse1"); PlayerInput.Triggers.Update();
                Main.mouseLeft = PlayerInput.Triggers.Current.MouseLeft; Main.mouseLeftRelease = true;
                if (down && Main.mouseLeft) nativeMouseRequests++;
                Call(input, "AfterMapping");
                PlayerInput.CacheMousePositionForZoom(); PlayerInput.SetZoom_MouseInWorld();
                Main.keyState = finalSample; Call(input, "AfterKeyboardRefresh"); Call(targets, "ProcessInput"); Call(shell, "ProcessInput");
                Require(!(bool)Get(shell, "Failed"), "motion consumer shell remains healthy");
            };
            Keys[] chordKeys = { Keys.LeftShift, Keys.LeftAlt };
            Action neutral = () => { sample(Array.Empty<Keys>(), Array.Empty<Keys>(), false); sample(Array.Empty<Keys>(), Array.Empty<Keys>(), false); };
            Action clearCooldown = () => Call(Get(announcements, "cooldown"), "Clear");
            Action update = () => Call(targets, "Update", (long)Get(targets, "tick") + 1);
            try
            {
                // Same cosmetic sink as the established QuickItems component
                // fixture. Native ItemCheck, use gate, healing and stack remain
                // real; no Main loop, device or native OS input is involved.
                useIsolation.Patch(hitbox, prefix: new HarmonyMethod(typeof(NativeTargetMotionInputChecks).GetMethod(nameof(DrawHitbox), Flags)));
                Set(shell, "LayersReady", true); Set(state, "Ready", true); Call(shell, "CloseAndSubmitPosition");
                Main.hideUI = Main.playerInventory = Main.blockInput = false;
                Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640; Main.LocalPlayer.gravDir = 1; Main.LocalPlayer.chest = -1;
                Main.LocalPlayer.position = new Vector2(800, 800);
                Main.item = new WorldItem[oldDrops.Length];
                Main.dust = new Dust[oldDust.Length]; for (int i = 0; i < Main.dust.Length; i++) Main.dust[i] = new Dust();
                var item = new Item(); item.SetDefaults(9); item.stack = 23;
                Main.item[0] = new WorldItem(item) { position = new Vector2(20, 320), width = 16, height = 16 };
                player.inventory[3] = new Item(); player.inventory[3].SetDefaults(28); player.inventory[3].stack = 3;
                player.selectedItemState.Select(3); player.selectedItemState.Update(); player.statLife = 50; player.statLifeMax2 = 100;
                player.potionDelay = player.itemAnimation = player.itemTime = 0;
                bind("item-browser.query", null); bind("announcement.send", send); Call(announcements, "SetEnabled", true); neutral();
                NativeQuickItemChecks.NativeFrame(player); // Real neutral consumer establishes releaseUseItem.
                foreach (int gap in new[] { 0, 1, 2 })
                {
                    Call(targets, "Cancel", (object)null); Main.screenPosition = Vector2.Zero; neutral(); Pass(targets); clearCooldown();
                    int before = outputCount(), leaked = nativeMouseRequests;
                    if (gap != 0)
                    {
                        Keys firstModifier = gap == 1 ? Keys.LeftAlt : Keys.LeftShift;
                        sample(new[] { firstModifier }, new[] { firstModifier }, false); sample(chordKeys, chordKeys, false);
                    }
                    // The previous cached keyboard is neutral. This same early
                    // sample acquires the mouse before mapping; Dispatch receives
                    // the immutable binding from the real compiled registry.
                    sample(chordKeys, chordKeys, true);
                    NativeQuickItemChecks.NativeFrame(player);
                    Require(player.inventory[3].stack == 3 && player.statLife == 50 && player.itemAnimation == 0,
                        "real native ItemCheck starts no use and consumes no healing item for the owned chord");
                    Require((int)Get(targets, "pending") == 2 && nativeMouseRequests == leaked && ((int)Get(input, "ReadOnlyMouseMask") & 1) != 0,
                        "same-opportunity modifiers acquire and dispatch before native mouse consumption");
                    var frozen = Get(targets, "frozen");
                    if (gap == 0) Main.screenPosition = new Vector2(16, 0);
                    Pass(targets); if (gap != 0) Main.screenPosition = new Vector2(16, 0); update();
                    Require(outputCount() == before + 1 && outputText().Contains("23 个 " + Lang.GetItemNameValue(9)) && (int)Get(frozen, "ItemType") == 9,
                        "real Host/registry/chat consumer sends trigger drop once across camera gap " + gap);
                    Console.WriteLine("MOTION-CONSUMER layer=L2 gap=" + gap + " cameraDelta=16,0 outlets=1 triggerType=9 triggerStack=23 nativeMappedMouseRequests=0 full-native-motion=NOT_RUN");
                    neutral();
                }
                Main.screenPosition = Vector2.Zero; Call(state, "RestoreVisible");
                x = 500; y = 300; neutral(); Require((bool)Get(state, "OwnsPointer"), "fixture point lies inside the actual F5 window");
                x = 30; y = 328; Pass(targets); clearCooldown();
                int outside = outputCount(); sample(chordKeys, chordKeys, true); Pass(targets); update();
                Require(outputCount() == outside + 1, "same-sample move from old F5 hover to current exterior retains actual world announcement"); neutral();

                // Pending repeat: preserve the first receipt/target/deadline,
                // but independently absorb a released-and-repressed mouse edge.
                Call(shell, "CloseAndSubmitPosition"); neutral(); Pass(targets); clearCooldown();
                sample(chordKeys, chordKeys, true); object first = Get(targets, "frozen"); long expires = (long)Get(targets, "expires");
                int pendingOutlets = outputCount(), pendingLeaks = nativeMouseRequests;
                sample(chordKeys, chordKeys, false); sample(chordKeys, chordKeys, true);
                NativeQuickItemChecks.NativeFrame(player);
                Require(player.inventory[3].stack == 3 && player.statLife == 50 && player.itemAnimation == 0, "pending repeat also has zero real native ItemCheck use/consumption");
                Require(ReferenceEquals(first, Get(targets, "frozen")) && expires == (long)Get(targets, "expires") && nativeMouseRequests == pendingLeaks,
                    "pending repeat owns native edge without replacing target or extending first deadline");
                Pass(targets); update(); Require(outputCount() == pendingOutlets + 1, "pending repeat sends exactly the first announcement once"); neutral();

                HotkeyChord queryChord; Require(HotkeyChord.TryParse("LeftControl+Mouse1", out queryChord, out parse), "query chord parses");
                bind("item-browser.query", queryChord); Call(shell, "CloseAndSubmitPosition"); neutral(); Pass(targets);
                int queryOutlets = outputCount(); sample(new[] { Keys.LeftControl }, new[] { Keys.LeftControl }, true);
                Require((int)Get(targets, "pending") == 1, "real compiled query binding enters its own pending request");
                object queryTarget = Get(targets, "frozen"); long queryExpires = (long)Get(targets, "expires"); int crossLeaks = nativeMouseRequests;
                sample(chordKeys, chordKeys, false); sample(chordKeys, chordKeys, true);
                Require(ReferenceEquals(queryTarget, Get(targets, "frozen")) && queryExpires == (long)Get(targets, "expires") && nativeMouseRequests == crossLeaks,
                    "pending query absorbs an enabled announcement chord despite CanAnnounce's dispatch Busy gate");
                Pass(targets); update();
                Require(outputCount() == queryOutlets && (int)Get(Get(Get(browser, "Knowledge"), "Workspace"), "Selected") == 9 && (bool)Get(state, "Visible"),
                    "the first query alone navigates the real browser and emits no chat"); neutral();
                var page = Get(browser, "page");
                var part = Activator.CreateInstance(page.GetType().Assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.BrowserPart"), true); Set(part, "Command", 7);
                Call(page, "Execute", part, false); Require((bool)Get(targets, "Picking"), "actual query-page command enters point selection");
                neutral(); Pass(targets); sample(Array.Empty<Keys>(), Array.Empty<Keys>(), true); Pass(targets); update();
                Require(outputCount() == queryOutlets && !(bool)Get(targets, "Busy") && (int)Get(Get(Get(browser, "Knowledge"), "Workspace"), "Selected") == 9,
                    "page point selection waits for release and navigates from its new native mouse edge without chat"); neutral();
                Call(shell, "CloseAndSubmitPosition"); bind("item-browser.query", null); neutral();

                var savedColdDrop = Main.item[0]; var savedColdTile = Main.tile[1, 20];
                try
                {
                    Main.item[0] = null; var chest = new Tile { type = 21, frameX = 36 }; chest.active(true); chest.fullbrightBlock(true); Main.tile[1, 20] = chest;
                    Call(catalog, "Reset"); Pass(targets); clearCooldown(); int coldOutlets = outputCount();
                    sample(chordKeys, chordKeys, true); object coldTarget = Get(targets, "frozen"); Pass(targets); update();
                    Require((bool)Get(targets, "preparingPlacement"), "real declaration enters cold placement preparation");
                    long deadline = (long)Get(targets, "placementDeadline"), coldExpires = (long)Get(targets, "expires"); int coldLeaks = nativeMouseRequests;
                    sample(chordKeys, chordKeys, false); sample(chordKeys, chordKeys, true); NativeQuickItemChecks.NativeFrame(player);
                    Require(ReferenceEquals(coldTarget, Get(targets, "frozen")) && deadline == (long)Get(targets, "placementDeadline") && coldExpires == (long)Get(targets, "expires") && nativeMouseRequests == coldLeaks && player.inventory[3].stack == 3,
                        "cold repeat consumes actual native use without renewing/replacing first target or either deadline");
                    Main.screenPosition = new Vector2(16, 0);
                    for (int i = 0; i < 110 && (bool)Get(targets, "Busy"); i++)
                    {
                        sample(chordKeys, chordKeys, false); long reads = (long)Get(catalog, "PlacementReads"); update();
                        Require((long)Get(catalog, "PlacementReads") - reads <= 64, "moving cold declaration keeps the shared 64-per-mapping budget");
                        long after = (long)Get(catalog, "PlacementReads"); update();
                        Require((long)Get(catalog, "PlacementReads") == after, "empty outer update spends no second preparation allowance");
                    }
                    Require(outputCount() == coldOutlets + 1 && !(bool)Get(targets, "Busy") && outputText().Contains(Lang.GetItemNameValue(Terraria.ID.ItemID.GoldChest)),
                        "cold ready across camera travel sends the original gold chest once after repeat coalescing");
                    Console.WriteLine("MOTION-CONSUMER layer=L2 gap=cold cameraDelta=16,0 outlets=1 sharedPlacementBudget=64 repeats=absorbed nativeItemUse=0 full-native-motion=NOT_RUN");
                }
                finally { Call(targets, "Cancel", (object)null); Main.item[0] = savedColdDrop; Main.tile[1, 20] = savedColdTile; Main.screenPosition = Vector2.Zero; neutral(); }

                Pass(targets); int mismatch = outputCount(); sample(chordKeys, new[] { Keys.LeftShift }, true);
                Require(!(bool)Get(targets, "Busy") && outputCount() == mismatch && ((int)Get(input, "ReadOnlyMouseMask") & 1) != 0,
                    "early/final modifier mismatch retires dispatch while retaining owned native tail"); neutral();
                Require((int)Get(input, "ReadOnlyMouseMask") == 0, "trusted physical release retires mismatch tail");
                int windowsLeaks = nativeMouseRequests; sample(new[] { Keys.LeftWindows, Keys.LeftShift, Keys.LeftAlt }, new[] { Keys.LeftWindows, Keys.LeftShift, Keys.LeftAlt }, true);
                Require(!(bool)Get(targets, "Busy") && (int)Get(input, "ReadOnlyMouseMask") == 0 && nativeMouseRequests == windowsLeaks + 1,
                    "system-modifier chord is never falsely claimed as a dispatchable read-only action"); neutral();

                foreach (string invalidation in new[] { "pointer", "zoom", "ui-page", "binding" })
                {
                    Call(targets, "Cancel", (object)null); neutral(); Pass(targets); clearCooldown(); int cancelled = outputCount();
                    sample(chordKeys, chordKeys, true);
                    Vector2 savedZoom = Main.GameViewMatrix.Zoom;
                    if (invalidation == "pointer") x++;
                    else if (invalidation == "zoom") Main.GameViewMatrix.Zoom = new Vector2(1.25f);
                    else if (invalidation == "ui-page") Call(state, "Navigate", ((int)Get(state, "Page") + 1) % 10);
                    else bind("announcement.send", null);
                    if (invalidation == "pointer") sample(chordKeys, chordKeys, false);
                    Pass(targets); update();
                    Require(outputCount() == cancelled && !(bool)Get(targets, "Busy"), "frozen gesture cancels on actual " + invalidation + " invalidation");
                    if (invalidation == "pointer") x--;
                    Main.GameViewMatrix.Zoom = savedZoom;
                    if (invalidation == "binding") bind("announcement.send", send);
                    neutral();
                }

                // T15: identity changes after request cannot retarget a new drop.
                Pass(targets); clearCooldown(); sample(chordKeys, chordKeys, true); int replaced = outputCount();
                var replacement = new Item(); replacement.SetDefaults(8); replacement.stack = 99;
                Main.item[0] = new WorldItem(replacement) { position = new Vector2(20, 320), width = 16, height = 16 };
                Pass(targets); update(); Require(outputCount() == replaced && !(bool)Get(targets, "Busy"), "replaced world-item identity cancels instead of selecting replacement"); neutral();

                object slotSnapshot = Get(targets, "snapshot");
                var savedSlot = Main.LocalPlayer.inventory[0]; var savedMouseItem = Main.mouseItem;
                try
                {
                    x = y = 30; Main.playerInventory = true; neutral();
                    var slotItem = new Item(); slotItem.SetDefaults(9); slotItem.stack = 7; Main.LocalPlayer.inventory[0] = slotItem;
                    Action<bool, int, float> slotPass = (hover, contextId, width) =>
                    {
                        Call(targets, "BeginPass");
                        Call(slotSnapshot, "Record", Main.LocalPlayer.inventory, contextId, 0, new Vector2(20, 20), width, width);
                        if (hover) Terraria.UI.ItemSlot.MouseHover(Main.LocalPlayer.inventory, contextId, 0);
                        Main.LocalPlayer.mouseInterface = true; Main.mouseText = false;
                        Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true);
                    };
                    slotPass(false, 0, 52 * .85f); clearCooldown(); int slotOut = outputCount();
                    sample(chordKeys, chordKeys, true); Main.mouseItem = new Item(); Main.mouseLeft = Main.mouseLeftRelease = true;
                    Terraria.UI.ItemSlot.LeftClick(Main.LocalPlayer.inventory, 0, 0);
                    Require(ReferenceEquals(Main.LocalPlayer.inventory[0], slotItem) && slotItem.stack == 7 && Main.mouseItem.IsAir,
                        "real ItemSlot.LeftClick is blocked before native take/place by the acquired read-only mask");
                    slotPass(true, 0, 52 * .85f); update();
                    Require(outputCount() == slotOut + 1 && outputText().Contains("7 个 " + Lang.GetItemNameValue(9)), "trigger-visible inventory contents reach the actual chat consumer once"); neutral();

                    slotPass(false, 0, 52 * .85f); slotOut = outputCount();
                    slotItem.stack = 8; sample(chordKeys, chordKeys, true); slotPass(true, 0, 52 * .85f); update();
                    Require(outputCount() == slotOut && !(bool)Get(targets, "Busy"), "content changed after visible commit but before trigger cannot reuse old slot geometry"); neutral();
                    slotPass(false, 0, 52 * .85f); sample(chordKeys, chordKeys, true); slotItem.stack = 9; slotPass(true, 0, 52 * .85f); update();
                    Require(outputCount() == slotOut && !(bool)Get(targets, "Busy"), "content changed between trigger and proof cancels rather than reading later Hover"); neutral();

                    Main.LocalPlayer.inventory[0] = new Item(); slotPass(false, 0, 52 * .85f);
                    sample(chordKeys, chordKeys, true); slotPass(true, 0, 52 * .85f); update();
                    Require(outputCount() == slotOut && !(bool)Get(targets, "Busy"), "frozen empty slot never announces the world behind it"); neutral();

                    Main.playerInventory = false; Main.LocalPlayer.inventory[0] = slotItem; slotItem.stack = 11; neutral();
                    slotPass(false, 13, 52 * .75f); clearCooldown();
                    sample(chordKeys, chordKeys, true);
                    // Native hotbar animates its scale in Draw. Confirm the
                    // frozen slot identity while the later rectangle grows.
                    Call(targets, "BeginPass"); Call(slotSnapshot, "Record", Main.LocalPlayer.inventory, 13, 0, new Vector2(20, 20), 52f, 52f);
                    Call(slotSnapshot, "Confirm", Main.LocalPlayer.inventory, 13, 0);
                    Main.LocalPlayer.mouseInterface = true; Main.mouseText = false; Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true); update();
                    Require(outputCount() == slotOut + 1 && outputText().Contains("11 个 " + Lang.GetItemNameValue(9)), "hotbar animation preserves the trigger's visible slot without retarget"); neutral();
                    Console.WriteLine("PASS target UI L2: controlled native slot geometry receipt plus actual MouseHover/LeftClick/chat; inventory frozen content/empty/pre-trigger replacement/post-trigger mutation and hotbar scale animation. Rendered layout NOT RUN.");
                }
                finally { Main.LocalPlayer.inventory[0] = savedSlot; Main.mouseItem = savedMouseItem; Main.playerInventory = false; x = 30; y = 328; }

                bind("announcement.send", null); Call(announcements, "SetEnabled", false); Call(targets, "Cancel", (object)null);
                object snapshot = Get(targets, "snapshot");
                for (int i = 0; i < 64; i++) Pass(targets);
                Require(!(bool)Get(snapshot, "recording") && !(bool)Get(snapshot, "available"), "disabled/unbound steady Draw does no slot-table preparation");
                int ordinary = nativeMouseRequests; sample(Array.Empty<Keys>(), Array.Empty<Keys>(), true);
                Require(nativeMouseRequests == ordinary + 1 && (int)Get(input, "ReadOnlyMouseMask") == 0, "unbound ordinary mouse reaches native mapping"); neutral();
                NativeQuickItemChecks.NativeFrame(player);
                sample(Array.Empty<Keys>(), Array.Empty<Keys>(), true); NativeQuickItemChecks.NativeFrame(player);
                Require(player.inventory[3].stack == 2 && player.statLife > 50 && player.itemAnimation > 0,
                    "positive control: after trusted release an unbound ordinary click really reaches native ItemCheck and consumes the potion"); neutral();
                Console.WriteLine("PASS motion/input L2: actual compiled registry -> Host -> frozen target -> intercepted native chat outlet; F5 exterior, both camera gaps, same-sample chord, repeat coalescing, mismatch tail, identity replacement and disabled cost. Draw boundary is controlled; native world motion/FPS/multiplayer NOT RUN.");
            }
            finally
            {
                Call(targets, "Cancel", (object)null); Call(shell, "CloseAndSubmitPosition");
                Main.item = oldDrops; Main.screenPosition = oldCamera; Main.LocalPlayer.position = oldPosition; Main.playerInventory = oldInventory;
                Main.dust = oldDust;
                player.inventory[3] = oldUseSlot; player.selectedItemState.Select(oldSelected); player.selectedItemState.Update();
                player.statLife = oldLife; player.statLifeMax2 = oldLifeMax; player.potionDelay = oldPotionDelay; player.itemAnimation = player.itemTime = 0;
                Array.Copy(oldBuffTypes, player.buffType, oldBuffTypes.Length); Array.Copy(oldBuffTimes, player.buffTime, oldBuffTimes.Length);
                bind("announcement.send", null); bind("item-browser.query", oldQuery); bind("announcement.send", oldSend);
                Call(announcements, "SetEnabled", oldEnabled); Set(shell, "LayersReady", oldLayers); Set(state, "Ready", oldReady);
                Call(Get(announcements, "cooldown"), "Clear");
                Set(input, "ReadOnlyKeyboardSource", oldKeyboard); Main.keyState = new KeyboardState();
                PlayerInput.MouseInfo = new MouseState();
                useIsolation.Unpatch(hitbox, HarmonyPatchType.All, useIsolation.Id);
            }
        }
        private static bool DrawHitbox(ref Rectangle __result) { __result = new Rectangle(0, 0, 24, 24); return false; }
        private static Delegate Adapt(Type type, Action<object> callback)
        { var value = Expression.Parameter(type.GetGenericArguments()[0]); return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), Expression.Convert(value, typeof(object))), value).Compile(); }
    }
}
