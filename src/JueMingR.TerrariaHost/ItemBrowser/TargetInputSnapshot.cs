using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // A bounded, completed visible-frame receipt. Draw records geometry AND
    // displayed contents; Request only freezes a matching already-visible slot.
    // Later Hover may confirm this source, never select a replacement item.
    internal sealed class TargetInputSnapshot
    {
        private static readonly FieldInfo ActiveResourceSet = typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager).GetField("_activeSet", BindingFlags.NonPublic | BindingFlags.Instance);
        internal struct Slot
        {
            internal Item[] Items;
            internal Item Item;
            internal int Context, Index, Type, Stack, Prefix;
            internal Vector4 Rect;
            internal bool Blocker;
            internal bool NativeControl;
            internal bool MatchesContent()
            {
                if (Items == null || Index < 0 || Index >= Items.Length || !ReferenceEquals(Item, Items[Index])) return false;
                return Item == null || Item.type == Type && Item.stack == Stack && Item.prefix == Prefix;
            }
            internal bool Contains(Vector2 point) { return point.X >= Rect.X && point.Y >= Rect.Y && (Blocker ? point.X < Rect.Z && point.Y < Rect.W : point.X <= Rect.Z && point.Y <= Rect.W); }
        }
        private struct Layout
        {
            internal Player Player;
            internal object World, Tiles, Connection;
            internal Item[] Inventory, Armor, Dye, Misc, MiscDye, ChestItems;
            internal Chest Chest;
            internal int ChestIndex, ChestCapacity, ChestRow, EquipPage, Loadout, ExtraSlots, Height, Bottom;
            internal bool InventoryOpen, Expert, Master;
            internal object BackAsset, BackTexture, TextOwner, Culture;
            internal bool HideUi, FullMap, Fancy, Options, BlockInput, Chat, Sign, ChestText, Writing, Creative;
            internal int Shop, TalkNpc, SignIndex, MapStyle;
            internal bool MapEnabled;
            internal bool EmptyMouseItem, SuperCart;
            internal object TickOn, TickOff, CartOn, CartOff;
            internal int ButtonFlags;
            internal object ResourceManager, ResourceSet, Assets;
            internal string ResourceKey;
            internal int LifeMax, LifeMax2, ManaMax2, InfoMask, NpcIcons, NpcColumns;
            internal Matrix Ui;
            internal Vector2 Screen;
            internal static Layout Capture()
            {
                Player p = Main.LocalPlayer;
                Chest chest = p.chest >= 0 && p.chest < Main.chest.Length ? Main.chest[p.chest] :
                    p.chest == -2 ? p.bank : p.chest == -3 ? p.bank2 : p.chest == -4 ? p.bank3 : p.chest == -5 ? p.bank4 : null;
                return new Layout { Player = p, World = Main.ActiveWorldFileData, Tiles = Main.tile, Connection = Netplay.Connection, Inventory = p.inventory, Armor = p.armor, Dye = p.dye, Misc = p.miscEquips, MiscDye = p.miscDyes,
                    Chest = chest, ChestItems = chest?.item, ChestIndex = p.chest, ChestCapacity = chest?.maxItems ?? 0, ChestRow = ChestUI.StartingRowForDrawing,
                    EquipPage = Main.EquipPage, Loadout = p.CurrentLoadoutIndex, ExtraSlots = p.extraAccessorySlots,
                    Height = (int)(PlayerInput.OriginalScreenSize.Y / Main.UIScaleMatrix.M11), Bottom = Main.instance == null ? 0 : Main.instance.invBottom,
                    InventoryOpen = Main.playerInventory, Expert = Main.expertMode, Master = Main.masterMode,
                    BackAsset = Terraria.GameContent.TextureAssets.InventoryBack, BackTexture = InventoryTexture(), TextOwner = Main.CurrentInputTextTakerOverride, Culture = Terraria.Localization.Language.ActiveCulture,
                    HideUi = Main.hideUI, FullMap = Main.mapFullscreen, Fancy = Main.inFancyUI || Main.onlyDrawFancyUI, Options = Main.ingameOptionsWindow,
                    BlockInput = Main.blockInput, Chat = Main.drawingPlayerChat, Sign = Main.editSign, ChestText = Main.editChest, Writing = PlayerInput.WritingText,
                    Shop = Main.npcShop, TalkNpc = p.talkNPC, SignIndex = p.sign, Creative = Main.CreativeMenu.Enabled, MapStyle = Main.mapStyle, MapEnabled = Main.mapEnabled,
                    ResourceManager = Main.ResourceSetsManager, ResourceSet = Main.ResourceSetsManager == null ? null : ActiveResourceSet?.GetValue(Main.ResourceSetsManager), ResourceKey = Main.ResourceSetsManager?.ActiveSetKeyName,
                    Assets = Main.Assets, LifeMax = p.statLifeMax, LifeMax2 = p.statLifeMax2, ManaMax2 = p.statManaMax2,
                    InfoMask = InformationMask(p), NpcIcons = Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsTotal, NpcColumns = Terraria.UI.Gamepad.UILinkPointNavigator.Shortcuts.NPCS_IconsPerColumn,
                    EmptyMouseItem = Main.mouseItem == null || Main.mouseItem.IsAir, SuperCart = p.unlockedSuperCart, ButtonFlags = ButtonMask(p),
                    TickOn = LoadedTexture(Terraria.GameContent.TextureAssets.InventoryTickOn), TickOff = LoadedTexture(Terraria.GameContent.TextureAssets.InventoryTickOff),
                    CartOn = LoadedTexture(Terraria.GameContent.TextureAssets.Extra[255]), CartOff = LoadedTexture(Terraria.GameContent.TextureAssets.Extra[256]),
                    Ui = Main.UIScaleMatrix, Screen = PlayerInput.OriginalScreenSize };
            }
            internal bool Matches(Layout other)
            {
                return ReferenceEquals(Player, other.Player) && ReferenceEquals(World, other.World) && ReferenceEquals(Tiles, other.Tiles) && ReferenceEquals(Connection, other.Connection) && ReferenceEquals(Inventory, other.Inventory) && ReferenceEquals(Armor, other.Armor) &&
                    ReferenceEquals(Dye, other.Dye) && ReferenceEquals(Misc, other.Misc) && ReferenceEquals(MiscDye, other.MiscDye) &&
                    ReferenceEquals(Chest, other.Chest) && ReferenceEquals(ChestItems, other.ChestItems) && ChestIndex == other.ChestIndex && ChestCapacity == other.ChestCapacity && ChestRow == other.ChestRow &&
                    EquipPage == other.EquipPage && Loadout == other.Loadout && ExtraSlots == other.ExtraSlots && Height == other.Height && Bottom == other.Bottom &&
                    InventoryOpen == other.InventoryOpen && Expert == other.Expert && Master == other.Master && Ui == other.Ui && Screen == other.Screen;
            }
            internal bool UiOwnersMatch(Layout other)
            {
                return ReferenceEquals(ResourceManager, other.ResourceManager) && ReferenceEquals(ResourceSet, other.ResourceSet) && ResourceKey == other.ResourceKey && ReferenceEquals(Assets, other.Assets) &&
                    LifeMax == other.LifeMax && LifeMax2 == other.LifeMax2 && ManaMax2 == other.ManaMax2 && InfoMask == other.InfoMask && NpcIcons == other.NpcIcons && NpcColumns == other.NpcColumns &&
                    EmptyMouseItem == other.EmptyMouseItem && SuperCart == other.SuperCart && ButtonFlags == other.ButtonFlags && ReferenceEquals(TickOn, other.TickOn) && ReferenceEquals(TickOff, other.TickOff) && ReferenceEquals(CartOn, other.CartOn) && ReferenceEquals(CartOff, other.CartOff) &&
                    ReferenceEquals(BackAsset, other.BackAsset) && ReferenceEquals(BackTexture, other.BackTexture) && ReferenceEquals(TextOwner, other.TextOwner) && ReferenceEquals(Culture, other.Culture) && Creative == other.Creative &&
                    HideUi == other.HideUi && FullMap == other.FullMap && Fancy == other.Fancy && Options == other.Options && BlockInput == other.BlockInput &&
                    Chat == other.Chat && Sign == other.Sign && ChestText == other.ChestText && Writing == other.Writing && Shop == other.Shop && TalkNpc == other.TalkNpc && SignIndex == other.SignIndex && MapStyle == other.MapStyle && MapEnabled == other.MapEnabled;
            }
            internal bool Supports(Slot slot)
            {
                if (slot.Context == 22 || slot.Context == 41 || slot.Context == 42) return false;
                if (ReferenceEquals(slot.Items, Inventory)) return slot.Context == 13 ? !InventoryOpen && slot.Index < 10 : InventoryOpen && slot.Context >= 0 && slot.Context <= 2;
                if (!InventoryOpen) return false;
                return ReferenceEquals(slot.Items, Armor) || ReferenceEquals(slot.Items, Dye) || ReferenceEquals(slot.Items, Misc) ||
                    ReferenceEquals(slot.Items, MiscDye) || Chest != null && ReferenceEquals(slot.Items, Chest.item);
            }
        }
        private Slot[] committed = new Slot[512], building = new Slot[512];
        private int[] committedBuffs = new int[64], buildingBuffs = new int[64], triggerBuffs = new int[64];
        private int committedBuffCount, buildingBuffCount, triggerBuffCount;
        private int committedCount, buildingCount;
        private bool recording, overflow, available;
        private Layout committedLayout, buildingLayout, triggerLayout;
        private Slot selected;
        private Vector2 triggerPoint;
        private bool checking, currentBlocked;
        internal bool HasSlot { get; private set; }
        internal bool RejectedSlot { get; private set; }
        internal bool Confirmed { get; private set; }
        internal bool TriggerValid { get; private set; }
        internal TargetValue Value { get; private set; }
        internal void Invalidate() { available = recording = TriggerValid = false; }
        internal void Retire() { checking = currentBlocked = TriggerValid = HasSlot = RejectedSlot = Confirmed = false; Value = null; }
        private static object InventoryTexture()
        { try { return Terraria.GameContent.TextureAssets.InventoryBack?.Value; } catch { return null; } }

        internal void BeginDraw()
        {
            recording = true; overflow = false; buildingCount = 0; buildingLayout = Layout.Capture();
            currentBlocked = false;
            buildingBuffCount = CaptureBuffs(buildingBuffs); if (buildingBuffCount < 0) overflow = true;
        }
        internal void RecordBlocker(Rectangle rect)
        { AppendBlocker(rect, false); }
        internal void RecordNativeControl(Rectangle rect)
        { AppendBlocker(rect, true); }
        private void AppendBlocker(Rectangle rect, bool nativeControl)
        {
            if (!recording) return;
            if (buildingCount == building.Length) { overflow = true; return; }
            building[buildingCount++] = new Slot { Blocker = true, NativeControl = nativeControl, Rect = new Vector4(rect.Left, rect.Top, rect.Right, rect.Bottom) };
            if (checking && rect.Contains(triggerPoint.ToPoint())) currentBlocked = true;
        }
        internal bool NativeControlOwns(Vector2 point)
        {
            Layout now = Layout.Capture();
            if (!available || !committedLayout.Matches(now) || !committedLayout.UiOwnersMatch(now) || !BuffsMatch(committedBuffs, committedBuffCount)) return false;
            for (int i = committedCount - 1; i >= 0; i--) if (committed[i].Contains(point)) return committed[i].Blocker && committed[i].NativeControl;
            return false;
        }
        internal void Record(Item[] items, int context, int index, Vector2 position, float width, float height)
        {
            if (!recording || items == null || index < 0 || index >= items.Length) return;
            if (buildingCount == building.Length) { overflow = true; return; }
            Item item = items[index];
            var slot = new Slot { Items = items, Item = item, Context = context, Index = index,
                Type = item?.type ?? 0, Stack = item?.stack ?? 0, Prefix = item?.prefix ?? 0,
                Rect = new Vector4(position.X, position.Y, position.X + width, position.Y + height) };
            building[buildingCount++] = slot;
            if (checking && slot.Contains(triggerPoint))
                currentBlocked = !HasSlot || !ReferenceEquals(items, selected.Items) || context != selected.Context || index != selected.Index || !selected.MatchesContent();
        }
        internal void EndDraw(bool complete)
        {
            if (!recording) return; recording = false;
            Layout end = Layout.Capture();
            if (!complete || overflow || !buildingLayout.Matches(end) || !buildingLayout.UiOwnersMatch(end) || !BuffsMatch(buildingBuffs, buildingBuffCount)) { available = false; return; }
            var old = committed; committed = building; building = old;
            committedCount = buildingCount; committedLayout = buildingLayout; available = true;
            var oldBuffs = committedBuffs; committedBuffs = buildingBuffs; buildingBuffs = oldBuffs; committedBuffCount = buildingBuffCount;
        }
        internal bool Freeze(Vector2 uiPoint)
        {
            HasSlot = RejectedSlot = Confirmed = false; Value = null;
            checking = true; currentBlocked = false; triggerPoint = uiPoint;
            triggerLayout = Layout.Capture();
            TriggerValid = available && committedLayout.Matches(triggerLayout) && committedLayout.UiOwnersMatch(triggerLayout) && BuffsMatch(committedBuffs, committedBuffCount);
            triggerBuffCount = CaptureBuffs(triggerBuffs);
            if (!TriggerValid) return false;
            // Last drawn slot wins exactly as the visible painting does. An
            // unsupported/empty candidate never falls through to the world.
            for (int i = committedCount - 1; i >= 0; i--)
            {
                Slot slot = committed[i]; if (!slot.Contains(uiPoint)) continue;
                if (slot.Blocker) { RejectedSlot = true; return false; }
                HasSlot = true; selected = slot;
                RejectedSlot = !triggerLayout.Supports(slot) || !slot.MatchesContent();
                Value = RejectedSlot ? null : NativeTargetObservation.UiItem(slot.Type, slot.Stack);
                return !RejectedSlot;
            }
            return true;
        }
        internal bool Confirm(Item[] items, int context, int index)
        {
            bool same = HasSlot && ReferenceEquals(items, selected.Items) && context == selected.Context && index == selected.Index && selected.MatchesContent();
            if (same) Confirmed = true;
            return same;
        }
        internal bool StillMatches()
        {
            Layout now = Layout.Capture();
            return TriggerValid && !currentBlocked && triggerLayout.Matches(now) && triggerLayout.UiOwnersMatch(now) && BuffsMatch(triggerBuffs, triggerBuffCount) && (!HasSlot || !RejectedSlot && selected.MatchesContent());
        }
        private static int InformationMask(Player p)
        {
            int mask = (p.accWatch > 0 ? 1 : 0) | (p.accWeatherRadio ? 2 : 0) | (p.accCalendar ? 4 : 0) | (p.accFishFinder ? 8 : 0) |
                (p.accOreFinder ? 16 : 0) | (p.accCritterGuide ? 32 : 0) | (p.accThirdEye ? 64 : 0) | (p.accJarOfSouls ? 128 : 0) |
                (p.accDreamCatcher ? 256 : 0) | (p.accStopwatch ? 512 : 0) | (p.accCompass > 0 ? 1024 : 0) | (p.accDepthMeter > 0 ? 2048 : 0);
            for (int i = 0; i < 13 && i < p.hideInfo.Length; i++) if (p.hideInfo[i]) mask |= 1 << (16 + i);
            return mask;
        }
        private static int ButtonMask(Player p)
        {
            int bits = p.enabledSuperCart ? 1 : 0;
            for (int i = 0; i < 8; i++) if (p.hideMisc[i]) bits |= 1 << (i + 1);
            for (int i = 3; i < p.hideVisibleAccessory.Length && i < 10; i++) if (p.hideVisibleAccessory[i]) bits |= 1 << (i + 8);
            return bits;
        }
        private static object LoadedTexture(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> asset)
        { try { return asset?.Value; } catch { return null; } }
        private static int CaptureBuffs(int[] destination)
        {
            int[] buffs = Main.LocalPlayer.buffType;
            if (buffs == null || buffs.Length > destination.Length) return -1;
            Array.Copy(buffs, destination, buffs.Length); return buffs.Length;
        }
        private static bool BuffsMatch(int[] expected, int count)
        {
            int[] buffs = Main.LocalPlayer.buffType;
            if (count < 0 || buffs == null || buffs.Length != count) return false;
            // Timer ticks do not move icons. Expiry/removal/reordering does, and
            // must not turn a previously visible buff into world click-through.
            for (int i = 0; i < count; i++) if (buffs[i] != expected[i]) return false;
            return true;
        }

        internal static bool WorldInputMatches()
        {
            if (PlayerInput.UsingGamepad || PlayerInput.IgnoreMouseInterface || Main.screenWidth != (int)PlayerInput.OriginalScreenSize.X || Main.screenHeight != (int)PlayerInput.OriginalScreenSize.Y) return false;
            int x = (int)(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X), y = (int)(PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y);
            if (PlayerInput.MouseX != x || PlayerInput.MouseY != y) return false;
            // Independently authenticate the post-native point before any Host
            // temporary aim writer. This is the locked .8 SetZoom mapping.
            Vector2 center = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            Vector2 transformed = center + (new Vector2(x, y) - center) / Main.GameViewMatrix.RenderZoom.X;
            return Main.mouseX == (int)transformed.X && Main.mouseY == (int)transformed.Y;
        }
    }
}
