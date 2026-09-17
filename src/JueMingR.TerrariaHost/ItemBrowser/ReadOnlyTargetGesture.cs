using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Shared observation/gesture lease only. Browser and announcement commands
    // retain distinct state and operation outlets. No delayed pointer resampling.
    internal sealed class ReadOnlyTargetGesture : IDisposable
    {
        private const string Owner = "JueMingR.ItemTargets.ReadOnlyGesture";
        private static ReadOnlyTargetGesture current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patches = new List<MethodInfo>();
        private readonly HostInputState input;
        private readonly NativeItemCatalog catalog;
        private long tick, expires;
        private int pending;
        private bool picking, released, rawPick, inPass, completed, seenSlot, uiBlocked;
        private bool uiBoundary, resourceBoundary;
        private object player, world, tiles, connection;
        private bool inventory;
        private int chest;
        private Vector2 raw, screen, mouseScale, camera;
        private Matrix matrix, zoom;
        private float gravity;
        private TargetValue frozen, hovered;
        internal Func<bool> CanPick { get; set; }
        internal Func<bool> CanAnnounce { get; set; }
        internal Func<HotkeyChord> AnnouncementBinding { get; set; }
        internal Func<HotkeyChord> QueryBinding { get; set; }
        internal Action<TargetValue> Picked { get; set; }
        internal Action PickCancelled { get; set; }
        internal Action<TargetValue> Announced { get; set; }
        internal Action<string> Feedback { get; set; }
        internal bool Ready { get; private set; }
        internal bool Picking { get { return picking || pending == 1; } }
        internal bool Busy { get { return picking || pending != 0; } }
        internal ReadOnlyTargetGesture(HostInputState input, NativeItemCatalog catalog)
        {
            this.input = input; this.catalog = catalog;
            try
            {
                if (current != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) return;
                current = this;
                Patch(typeof(Main), "DrawInterface", new[] { typeof(GameTime) }, nameof(BeginPass), nameof(EndPass), nameof(PassFinally));
                Patch(typeof(Main), "DrawInterface_39_MouseOver", Type.EmptyTypes, nameof(BeforeWorldHover), null);
                Patch(typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager), "TryToHoverOverResources", Type.EmptyTypes, null, nameof(AfterResources));
                Patch(typeof(ItemSlot), "MouseHover", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Hover), null);
                Patch(typeof(ItemSlot), "LeftClick", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Left), null);
                Patch(typeof(ItemSlot), "RightClick", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Right), null);
                Patch(typeof(ItemSlot), "Draw", new[] { typeof(SpriteBatch), typeof(Item[]), typeof(int), typeof(int), typeof(Vector2), typeof(Color) }, nameof(Hotbar), null);
                Ready = true; input.ClaimsReadOnlyGesture = Claim;
            }
            catch { Dispose(); }
        }
        private void Patch(Type type, string name, Type[] signature, string before, string after, string finalizer = null)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, signature, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            patches.Add(method); harmony.Patch(method, Hook(before), Hook(after), null, Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(ReadOnlyTargetGesture).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        internal void BeginPick()
        {
            if (!Ready || Busy || CanPick != null && !CanPick()) return;
            picking = true; released = false; expires = tick + 600; Feedback?.Invoke("松开按钮后点击物品；右键或 Esc 取消");
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData; tiles = Main.tile; connection = Netplay.Connection;
        }
        private int Claim(int down, int pressed)
        {
            if (!Ready) return 0;
            if (picking)
            {
                if (CanPick != null && !CanPick()) return 0;
                if (down == 0) released = true;
                if (released && (pressed & 3) != 0) { rawPick = true; return pressed & 3; }
                return 0;
            }
            if (pending != 0) return 0;
            int claimed = CanPick != null && CanPick() ? ClaimBinding(QueryBinding?.Invoke(), pressed) : 0;
            return claimed | (CanAnnounce != null && CanAnnounce() ? ClaimBinding(AnnouncementBinding?.Invoke(), pressed) : 0);
        }
        private static int ClaimBinding(HotkeyChord binding, int pressed)
        {
            if (binding == null || binding.MainKey < 256) return 0;
            // Native mapping consumes its cached keyboard sample before the final
            // keyboard refresh. Require this same modifier set at both seams;
            // a changed chord cancels instead of submitting without consumption.
            HotkeyModifiers modifiers = HotkeyModifiers.None;
            for (int i = 0; i < 6; i++) if (Main.keyState.IsKeyDown((Keys)HotkeyChord.ModifierCode(i))) modifiers |= (HotkeyModifiers)(1 << i);
            return modifiers == binding.Modifiers ? pressed & 1 << (binding.MainKey - 256) : 0;
        }
        internal void ProcessInput()
        {
            if (picking && (!SessionMatches() || !input.CanStartActions || CanPick != null && !CanPick() || input.Hotkeys.IsNew(27))) { Cancel("点选已取消"); return; }
            if (picking && rawPick)
            {
                rawPick = false;
                if (input.Hotkeys.IsNew(257)) { Cancel("点选已取消"); return; }
                if (input.Hotkeys.IsNew(256) && (input.ReadOnlyMouseMask & 1) != 0) { picking = false; Request(1); }
            }
        }
        internal void RequestQuery()
        {
            if (!Ready || Busy || !input.CanStartActions || CanPick == null || !CanPick()) return;
            var binding = QueryBinding?.Invoke();
            if (binding == null || binding.MainKey >= 256 && (input.ReadOnlyMouseMask & 1 << (binding.MainKey - 256)) == 0) { Feedback?.Invoke("这次手势未能安全接管，未查询"); return; }
            Request(1);
        }
        internal void RequestAnnouncement()
        {
            if (!Ready || Busy || !input.CanStartActions || CanAnnounce == null || !CanAnnounce()) return;
            HotkeyChord binding = AnnouncementBinding?.Invoke();
            if (binding == null || binding.MainKey >= 256 && (input.ReadOnlyMouseMask & 1 << (binding.MainKey - 256)) == 0)
            { Feedback?.Invoke("这次手势未能安全接管，未发送"); return; }
            Request(2);
        }
        private void Request(int kind)
        {
            pending = kind; completed = inPass = seenSlot = uiBlocked = false; hovered = null; expires = tick + 4;
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData; tiles = Main.tile; connection = Netplay.Connection;
            raw = new Vector2(PlayerInput.MouseInfo.X, PlayerInput.MouseInfo.Y); screen = PlayerInput.OriginalScreenSize;
            matrix = Main.UIScaleMatrix; zoom = Main.GameViewMatrix.ZoomMatrix; mouseScale = PlayerInput.RawMouseScale; camera = Main.screenPosition;
            gravity = Main.LocalPlayer.gravDir; inventory = Main.playerInventory; chest = Main.LocalPlayer.chest;
            try { frozen = NativeTargetObservation.World(Main.MouseWorld, catalog, kind == 2); }
            // A failed world observation must not preempt a later fresh UI slot.
            // With no slot, null fails closed at delivery instead of resampling.
            catch { frozen = null; }
        }
        private bool SessionMatches() { return ReferenceEquals(player, Main.LocalPlayer) && ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(tiles, Main.tile) && ReferenceEquals(connection, Netplay.Connection); }
        private bool Matches()
        {
            return pending != 0 && input.IsFocused && ReferenceEquals(player, Main.LocalPlayer) && ReferenceEquals(world, Main.ActiveWorldFileData) &&
                ReferenceEquals(tiles, Main.tile) && ReferenceEquals(connection, Netplay.Connection) && raw.X == PlayerInput.MouseInfo.X && raw.Y == PlayerInput.MouseInfo.Y &&
                screen == PlayerInput.OriginalScreenSize && matrix == Main.UIScaleMatrix && zoom == Main.GameViewMatrix.ZoomMatrix && mouseScale == PlayerInput.RawMouseScale &&
                camera == Main.screenPosition && gravity == Main.LocalPlayer.gravDir && inventory == Main.playerInventory && chest == Main.LocalPlayer.chest;
        }
        private static void BeginPass()
        { var owner = current; if (owner == null || owner.pending == 0 || owner.completed) return; owner.inPass = owner.Matches(); owner.seenSlot = owner.uiBoundary = owner.resourceBoundary = owner.uiBlocked = false; owner.hovered = null; }
        private static void BeforeWorldHover()
        { var owner = current; if (owner == null || !owner.inPass) return; owner.uiBoundary = true; owner.uiBlocked = Main.LocalPlayer.mouseInterface || Main.mouseText; }
        private static void AfterResources(bool __runOriginal)
        { var owner = current; if (owner == null || !owner.inPass || !owner.uiBoundary) return; owner.resourceBoundary = __runOriginal; owner.uiBlocked |= Main.LocalPlayer.mouseInterface || Main.mouseText; }
        private static void Hover(Item[] __0, int __1, int __2)
        {
            var owner = current;
            if (owner == null || !owner.inPass || __0 == null || __2 < 0 || __2 >= __0.Length) return;
            // Read the real slot before native empty-equipment substitution.
            // Crafting candidates (context22) are display samples, not possessions.
            owner.seenSlot = true;
            Item item = __0[__2]; owner.hovered = __1 == 22 || __1 == 41 || __1 == 42 || item == null ? NativeTargetObservation.UiItem(0, 0) : NativeTargetObservation.UiItem(item.type, item.stack);
        }
        private static void Hotbar(Item[] __1, int __2, int __3, Vector2 __4)
        {
            var owner = current;
            if (owner == null || !owner.inPass || __2 != 13 || Main.playerInventory || __1 == null || !ReferenceEquals(__1, Main.LocalPlayer.inventory) || __3 < 0 || __3 >= 10) return;
            var back = Terraria.GameContent.TextureAssets.InventoryBack?.Value;
            if (back == null || Main.mouseX < __4.X || Main.mouseY < __4.Y || Main.mouseX > __4.X + back.Width * Main.inventoryScale || Main.mouseY > __4.Y + back.Height * Main.inventoryScale) return;
            owner.seenSlot = true; var item = __1[__3]; owner.hovered = item == null ? NativeTargetObservation.UiItem(0, 0) : NativeTargetObservation.UiItem(item.type, item.stack);
        }
        private static bool Left() { return current == null || (current.input.ReadOnlyMouseMask & 1) == 0; }
        private static bool Right() { return current == null || (current.input.ReadOnlyMouseMask & 2) == 0; }
        private static void EndPass(bool __runOriginal)
        {
            var owner = current; if (owner == null || !owner.inPass) return;
            // World item/NPC tooltips also set mouseText later. Only the actual
            // UI boundary, including life/mana hover, can veto a world target.
            owner.inPass = false; owner.completed = __runOriginal && owner.uiBoundary && owner.resourceBoundary && owner.Matches();
        }
        private static Exception PassFinally(Exception __exception)
        { if (__exception != null && current != null) { current.inPass = current.completed = false; } return __exception; }
        internal void Update(long currentTick)
        {
            tick = currentTick;
            if (Busy && (tick > expires || Main.gameMenu || !input.IsFocused || !SessionMatches() || picking && CanPick != null && !CanPick())) { Cancel("未取得有效的新目标，点选或宣告已取消"); return; }
            if (pending == 0 || !completed) return;
            int kind = pending; bool valid = Matches() && (kind == 1 ? CanPick == null || CanPick() : CanAnnounce != null && CanAnnounce()); TargetValue target = seenSlot ? hovered : uiBlocked ? null : frozen;
            pending = 0; completed = false; frozen = hovered = null;
            if (!valid || target == null) { Feedback?.Invoke("目标已变化或被界面遮挡，请重新操作"); if (kind == 1) PickCancelled?.Invoke(); return; }
            if (kind == 1) Picked?.Invoke(target); else Announced?.Invoke(target);
        }
        internal void Cancel(string message = null)
        { bool returnPick = Picking; picking = rawPick = inPass = completed = false; pending = 0; frozen = hovered = null; if (message != null) Feedback?.Invoke(message); if (returnPick) PickCancelled?.Invoke(); }
        public void Dispose()
        { Ready = false; Cancel(); input.ClaimsReadOnlyGesture = null; foreach (var patch in patches) harmony.Unpatch(patch, HarmonyPatchType.All, Owner); patches.Clear(); if (ReferenceEquals(current, this)) current = null; }
    }
}
