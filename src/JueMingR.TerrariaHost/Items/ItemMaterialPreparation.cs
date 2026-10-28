using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Items
{
    // One short native inventory preparation, not another item scheduler. Its
    // finite endpoints pass to the existing use token and return through the
    // same owner. Only original ItemSlot.LeftClick moves live Items.
    internal sealed class ItemMaterialPreparation
    {
        private enum Phase { Idle, Preparing, Ready, Using, Returning, Unknown }
        private enum TailStep { None, Neutral, Press }
        private sealed class Endpoint
        {
            internal int Slot, OriginalType, OriginalStack, Type, Stack;
            internal Item Original, Expected;
        }
        private readonly HostTools tools;
        private readonly Endpoint[] endpoints = new Endpoint[3];
        private readonly int[] forward = new int[4], history = new int[4];
        private int endpointCount, forwardCount, forwardCursor, historyCount, sourceSlot, destination, activeSlot = -1;
        private Player player;
        private Item[] inventory;
        private Item material, displaced, expectedCursor;
        private int materialType, cursorType, cursorStack;
        private long session, token, selection;
        private BlueprintProjectionIdentity identity;
        private Phase phase;
        private bool nativeClick, tailOpen, oldLeft, ownLeft, clickApplied, returnRequested;
        private bool ownsInventory, nativeInventoryChange;
        private TailStep tailStep;
        internal static ItemMaterialPreparation Current { get; private set; }
        internal bool HasWork { get { return phase != Phase.Idle; } }
        internal bool Ready { get { return phase == Phase.Ready; } }
        internal bool Using { get { return phase == Phase.Using; } }
        internal bool Returning { get { return phase == Phase.Returning; } }
        internal bool Unknown { get { return phase == Phase.Unknown; } }
        internal Item Material { get { return material; } }
        internal string Reason { get; private set; }
        internal Func<int, int> UseConsumptionMaximum;
#if DEBUG
        internal long NativeClicks { get; private set; }
        internal long NativeNeutralTails { get; private set; }
#endif
        internal ulong Slots
        {
            get { ulong result = 0; for (int i = 0; i < endpointCount; i++) result |= 1UL << endpoints[i].Slot; return result; }
        }
        internal ItemMaterialPreparation(HostTools tools)
        { this.tools = tools ?? throw new ArgumentNullException(nameof(tools)); Current = this; }

        internal bool TryPrepare(Player p, int sourceSlot, int destination, BlueprintProjectionIdentity identity, out int currentSlot)
        {
            currentSlot = -1;
            if (!OnGameThread || p == null || identity == null || destination != 0 && destination != 54) return false;
            if (HasWork)
            {
                if (!SameSessionIdentity(p)) { ClearSession(); return false; }
                if (phase == Phase.Unknown) return false;
                if (!InputOwnershipCurrent()) return false;
                if (!this.identity.Equals(identity) || this.destination != destination ||
                    sourceSlot >= 0 && (sourceSlot >= 50 || !ReferenceEquals(p.inventory[sourceSlot], material)))
                { RequestReturn(); return false; }
                if (!ExpectedCurrent(phase == Phase.Using)) { LoseIdentity("material-preparation-identity-changed", PhysicalInput); return false; }
                if (!PreparationItemsNotPending()) { Reason = "material-preparation-endpoint-pending"; return false; }
                if (Reason == "material-preparation-endpoint-pending") Reason = null;
                if ((phase == Phase.Ready || phase == Phase.Using) && material != null && !material.IsAir && Main.mouseItem.IsAir)
                { currentSlot = MaterialSlot(); return currentSlot >= 0; }
                return false;
            }
            if (sourceSlot < 0 || sourceSlot >= 50 || sourceSlot >= p.inventory.Length || !StartAllowed(p) ||
                !EndpointAvailable(p, sourceSlot) || !tools.BuildInventoryEligible(sourceSlot, p.inventory[sourceSlot])) return false;
            var desired = p.inventory[sourceSlot];
            if (desired == null || desired.IsAir || destination >= p.inventory.Length ||
                destination == 54 && !desired.FitsAmmoSlot() || !EndpointAvailable(p, destination)) return false;
            int buffer = -1;
            var oldDestination = p.inventory[destination];
            if (sourceSlot != destination && !oldDestination.IsAir && (Item.CanStack(desired, oldDestination) || Item.CanStack(oldDestination, desired)))
            {
                // Isolate the old same-type stack before P ever reaches it.
                // A full main inventory is still usable when a third endpoint
                // C cannot stack with P/B: D,T,S,D rotates C into S and B into T.
                for (int pass = 0; pass < 2 && buffer < 0; pass++)
                    for (int slot = 0; slot < 50; slot++)
                    {
                        if (slot == sourceSlot || slot == destination || !EndpointAvailable(p, slot)) continue;
                        var candidate = p.inventory[slot];
                        if (pass == 0 ? candidate.IsAir : !candidate.IsAir &&
                            !Item.CanStack(candidate, desired) && !Item.CanStack(desired, candidate) &&
                            !Item.CanStack(candidate, oldDestination) && !Item.CanStack(oldDestination, candidate)) { buffer = slot; break; }
                    }
                if (buffer < 0) return false;
            }
            player = p; inventory = p.inventory; session = tools.Runtime.Generation; selection = tools.SelectionIntent;
            this.sourceSlot = sourceSlot; this.destination = destination; this.identity = identity;
            material = desired; materialType = desired.type; displaced = destination >= 50 && sourceSlot != destination ? oldDestination : null;
            endpointCount = forwardCount = forwardCursor = historyCount = 0;
            AddEndpoint(sourceSlot); if (destination != sourceSlot) AddEndpoint(destination); if (buffer >= 0) AddEndpoint(buffer);
            CaptureCursor(Main.mouseItem);
            token = tools.Items.Ownership.NewUseToken();
            if (!tools.Items.Ownership.TryBeginUse(session, sourceSlot, token, Slots)) { ResetReceipt(); return false; }
            if (sourceSlot == destination) phase = Phase.Ready;
            else if (buffer >= 0)
            { forward[forwardCount++] = destination; forward[forwardCount++] = buffer; forward[forwardCount++] = sourceSlot; forward[forwardCount++] = destination; phase = Phase.Preparing; }
            else
            {
                forward[forwardCount++] = sourceSlot; forward[forwardCount++] = destination;
                if (!oldDestination.IsAir) forward[forwardCount++] = sourceSlot;
                phase = Phase.Preparing;
            }
            Reason = null;
            if (phase == Phase.Ready) { currentSlot = sourceSlot; return true; }
            return false;
        }

        private bool OnGameThread { get { return Thread.CurrentThread.ManagedThreadId == tools.Items.ThreadId; } }
        private bool SameSessionIdentity(Player p)
        { return p != null && session > 0 && session == tools.Runtime.Generation && session == tools.Items.Ownership.Session && ReferenceEquals(player, p) &&
            ReferenceEquals(p, tools.Player) && ReferenceEquals(inventory, p.inventory); }
        private bool SameSession(Player p)
        { return SameSessionIdentity(p) && tools.Items.Ownership.OwnsUse(session, token); }
        private bool PhysicalInput
        {
            get
            {
                return tools.ManualLeft || tools.ManualRight || PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed ||
                    PlayerInput.MouseInfo.RightButton == ButtonState.Pressed || PlayerInput.Triggers.Current.SmartSelect ||
                    PlayerInput.Triggers.Current.Inventory || Main.keyState.IsKeyDown(Keys.Escape);
            }
        }
        private static bool Modifiers
        { get { return Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift) ||
            Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl) ||
            Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt); } }
        private bool StartAllowed(Player p)
        {
            return tools.Runtime.IsSessionActive && tools.Input.CanRunAutomaticActions && !tools.Use.Active && !tools.Items.Ownership.HasUse &&
                tools.CanRetainUse(p, false) && OrdinaryInventoryUi(p) && !PhysicalInput && !Modifiers && p.itemAnimation == 0 && p.itemTime == 0 && p.reuseDelay == 0 &&
                !p.selectedItemState.HasActiveOverride && !p.selectedItemState.HasBufferedChange &&
                !Main.LocalPlayerHasPendingInventoryActions() && Main.mouseItem != null && Main.mouseItem.IsAir && PreparationItemNotPending(Main.mouseItem);
        }
        private bool EndpointAvailable(Player p, int slot)
        {
            var item = p.inventory[slot];
            return item != null && !p.inventoryChestStack[slot] && !tools.Items.Ownership.IsProtected(slot) &&
                !tools.Items.World.ManualMaterials.Contains(item) && !tools.ProtectedAfterYield(item) && PreparationItemNotPending(item);
        }
        private bool PreparationItemNotPending(Item item)
        { return item != null && !(tools.BuildPendingMaterial?.Invoke(item) ?? false); }
        private bool PreparationItemsNotPending()
        {
            // A detached native 109/110 receipt is a different owner from
            // our use token. Recheck every live endpoint, including Air, and
            // the actual cursor before our precise ItemSlot bypass can move
            // anything. A newly pending source waits in place; no old stack
            // or click history is replayed to get around its decoder.
            if (!PreparationItemNotPending(Main.mouseItem)) return false;
            for (int i = 0; i < endpointCount; i++)
                if (!PreparationItemNotPending(inventory[endpoints[i].Slot])) return false;
            return true;
        }
        private void AddEndpoint(int slot)
        {
            var value = inventory[slot];
            endpoints[endpointCount++] = new Endpoint { Slot = slot, Original = value, OriginalType = value.type, OriginalStack = value.stack,
                Expected = value, Type = value.type, Stack = value.stack };
        }
        private Endpoint At(int slot)
        { for (int i = 0; i < endpointCount; i++) if (endpoints[i].Slot == slot) return endpoints[i]; return null; }
        private void CaptureCursor(Item value)
        { expectedCursor = value; cursorType = value?.type ?? 0; cursorStack = value?.stack ?? 0; }
        private static bool Air(int type, int stack) { return type <= 0 || stack <= 0; }
        private static bool Matches(Item value, Item expected, int type, int stack)
        {
            if (value == null) return false;
            if (Air(type, stack)) return value.IsAir;
            return ReferenceEquals(value, expected) && value.type == type && value.stack == stack;
        }
        private bool ExpectedCurrent(bool consume)
        {
            if (!SameSession(player) || !Matches(Main.mouseItem, expectedCursor, cursorType, cursorStack)) return false;
            for (int i = 0; i < endpointCount; i++)
            {
                var endpoint = endpoints[i];
                var current = inventory[endpoint.Slot];
                if (Matches(current, endpoint.Expected, endpoint.Type, endpoint.Stack)) continue;
                if (!consume || current == null) return false;
                int maximum = UseConsumptionMaximum?.Invoke(endpoint.Slot) ?? 0;
                int remaining = current.IsAir ? 0 : current.stack;
                bool realRemainder = ReferenceEquals(current, endpoint.Expected) && (current.IsAir || current.type == endpoint.Type) ||
                    current.IsAir && endpoint.Expected.IsAir;
                if (maximum <= 0 || !realRemainder || remaining < 0 || remaining >= endpoint.Stack || endpoint.Stack - remaining > maximum) return false;
                // Each declared native generation has its own independent
                // common budget. Observe its actual monotone remainder here,
                // including held B or accessory paint, not just primary P.
                // Update that Item's original return endpoint as well: a
                // legal consume never becomes a promise to recreate old B/C.
                for (int origin = 0; origin < endpointCount; origin++)
                    if (ReferenceEquals(endpoints[origin].Original, endpoint.Expected))
                    {
                        endpoints[origin].Original = current; endpoints[origin].OriginalType = current.type; endpoints[origin].OriginalStack = current.stack;
                        break;
                    }
                endpoint.Expected = current; endpoint.Type = current.type; endpoint.Stack = current.stack;
            }
            return true;
        }
        private int MaterialSlot()
        {
            for (int i = 0; i < endpointCount; i++)
                if (ReferenceEquals(inventory[endpoints[i].Slot], material) && material.type == materialType && material.stack > 0) return endpoints[i].Slot;
            return -1;
        }
        internal bool IsDisplaced(Item item)
        { return HasWork && item != null && displaced != null && ReferenceEquals(item, displaced) && !item.IsAir; }
        internal bool OwnsEndpoint(Item[] array, int slot)
        { return HasWork && phase != Phase.Unknown && ReferenceEquals(array, inventory) && At(slot) != null && ExpectedCurrent(phase == Phase.Using); }
        internal bool AllowsNativeSlot(Item[] array, int context, int slot)
        { return nativeClick && activeSlot == slot && ReferenceEquals(array, inventory) && context == (slot >= 54 ? 2 : 0) &&
            Main.mouseLeft && Main.mouseLeftRelease && OwnsEndpoint(array, slot) && PreparationItemsNotPending(); }

        internal bool TryAdoptUse(long nextToken, int toolSlot, ulong materialSlots)
        {
            if (!OnGameThread || phase != Phase.Ready || nextToken <= 0 || toolSlot < 0 || toolSlot >= 50 ||
                !SameSessionIdentity(player) || tailOpen || ownsInventory || !InputOwnershipCurrent() || !ExpectedCurrent(false) ||
                !PreparationItemsNotPending() || !Main.mouseItem.IsAir) return false;
            // No callback or native consumer runs in this single-threaded
            // transfer. A later EndUse(old) cannot release the new unique token.
            tools.Items.Ownership.EndUse(session, token);
            if (!tools.Items.Ownership.TryBeginUse(session, toolSlot, nextToken, materialSlots | Slots))
            { LoseIdentity("material-preparation-use-handoff", false); return false; }
            token = nextToken; phase = Phase.Using; return true;
        }
        internal void ReturnUse(long oldToken)
        {
            if (!OnGameThread || phase != Phase.Using || token != oldToken || !SameSessionIdentity(player)) return;
            if (!InputOwnershipCurrent()) return;
            if (!ExpectedCurrent(true)) { LoseIdentity("material-preparation-used-source-changed", PhysicalInput); return; }
            // Freeze every actual endpoint remainder. A prepared original
            // tool or accessory source can legally consume too; the declared
            // per-slot vector above is the only authority to accept that loss.
            for (int i = 0; i < endpointCount; i++)
            {
                var endpoint = endpoints[i];
                var value = inventory[endpoint.Slot]; endpoint.Expected = value; endpoint.Type = value.type; endpoint.Stack = value.stack;
            }
            tools.Items.Ownership.EndUse(session, oldToken);
            long nextToken = tools.Items.Ownership.NewUseToken();
            if (!tools.Items.Ownership.TryBeginUse(session, sourceSlot, nextToken, Slots))
            { LoseIdentity("material-preparation-return-handoff", false); return; }
            token = nextToken; phase = Phase.Returning;
            if (historyCount == 0) FinishReturn();
        }
        internal void RequestReturn()
        {
            if (!HasWork || !OnGameThread) return;
            if (phase == Phase.Unknown) return;
            if (!InputOwnershipCurrent()) return;
            if (phase == Phase.Using) return;
            // Invalidations can arrive from the native slot observer inside
            // this draw epoch. Record its completed swap before reversing the
            // finite history; do not interpret that same click as an undo.
            if (tailOpen) { returnRequested = true; return; }
            if (!ExpectedCurrent(false)) { LoseIdentity("material-preparation-return-identity", false); return; }
            phase = Phase.Returning;
            if (!tailOpen && historyCount == 0) FinishReturn();
        }
        internal bool Blocks(BlueprintProjectionIdentity value) { return HasWork && identity.Equals(value); }
        internal void ClearSession()
        {
            if (!OnGameThread) return;
            RestoreBorrowedInput();
            // Only an unchanged, still-owned UI with an empty cursor can be
            // closed. An occupied cursor is handed to the player in the real
            // open inventory; closing it would trigger vanilla GetOrDropItem.
            if (ownsInventory && SameSession(player) && !PhysicalInput && tools.SelectionIntent == selection &&
                Main.mouseItem != null && Main.mouseItem.IsAir && OrdinaryInventoryUi(player)) CloseNativeInventory();
            ownsInventory = false;
            if (session > 0) tools.Items.Ownership.EndUse(session, token);
            ResetReceipt();
        }
        private void LoseIdentity(string reason, bool manual)
        {
            RestoreBorrowedInput(); Reason = reason;
            if (!manual && ownsInventory && SameSession(player) && Main.mouseItem != null && Main.mouseItem.IsAir &&
                OrdinaryInventoryUi(player)) CloseNativeInventory();
            // Never reopen after an external close or close a UI the player
            // has taken over. With a live cursor, leave the native UI open.
            ownsInventory = false;
            if (session > 0 && session == tools.Items.Ownership.Session)
            {
                tools.Items.Ownership.EndUse(session, token);
                // Manual takeover owns its real Items immediately. Unexpected
                // nonmanual changes keep only this finite source range unknown.
                if (!manual) tools.Items.Ownership.HoldInterruptedSource(session, Slots);
            }
            phase = Phase.Unknown; nativeClick = false; activeSlot = -1;
        }
        private void ResetReceipt()
        {
            phase = Phase.Idle; player = null; inventory = null; material = displaced = expectedCursor = null;
            for (int i = 0; i < endpointCount; i++) endpoints[i] = null;
            endpointCount = forwardCount = forwardCursor = historyCount = 0;
            session = token = selection = 0; identity = default(BlueprintProjectionIdentity); Reason = null;
            UseConsumptionMaximum = null;
            ownsInventory = nativeInventoryChange = false;
            nativeClick = tailOpen = clickApplied = returnRequested = false; tailStep = TailStep.None; activeSlot = -1;
        }

        internal static void BeforeNativeInputTail() { Current?.BeforeTail(); }
        internal static void AfterNativeInputTail() { Current?.AfterTail(); }
        internal static void BeforeNativeInventoryToggle(Player p)
        {
            var current = Current;
            if (current != null && ReferenceEquals(p, current.player)) current.ExternalInventoryChange();
        }
        internal static void BeforeNativeInventoryOpen() { Current?.ExternalInventoryChange(); }
        private void ExternalInventoryChange()
        {
            if (!OnGameThread || !HasWork || phase == Phase.Unknown || nativeInventoryChange || !SameSession(player)) return;
            // This prefix observes intent before ToggleInv can close the UI
            // and dropItemCheck can reinterpret its cursor in the same Update.
            LoseIdentity("material-preparation-player-inventory", true);
        }
        private static bool OrdinaryInventoryUi(Player p)
        {
            return p != null && !Main.mapFullscreen && !Main.ingameOptionsWindow && !Main.inFancyUI && !Main.onlyDrawFancyUI &&
                !Main.clothesWindow && !Main.hairWindow && !Terraria.GameContent.UI.NewCraftingUI.Visible &&
                !Terraria.Graphics.Capture.CaptureManager.Instance.Active && !PlayerInput.InBuildingMode && !PlayerInput.UsingGamepadUI &&
                !Main.InGuideCraftMenu && !Main.InReforgeMenu && p.talkNPC < 0 && p.sign < 0 && p.chest == -1 && Main.npcShop == 0;
        }
        private bool InputOwnershipCurrent()
        {
            if (!SameSessionIdentity(player)) { ClearSession(); return false; }
            if (!tools.Items.Ownership.OwnsUse(session, token))
            { LoseIdentity("material-preparation-use-owner-changed", PhysicalInput); return false; }
            if (PhysicalInput || tools.SelectionIntent != selection || Main.playerInventory != ownsInventory)
            { LoseIdentity("material-preparation-player-takeover", true); return false; }
            return true;
        }
        private bool OpenOwnedInventory()
        {
            if (ownsInventory) return Main.playerInventory;
            if (Main.playerInventory || Main.mouseItem == null || !Main.mouseItem.IsAir || !OrdinaryInventoryUi(player)) return false;
            nativeInventoryChange = true;
            try { Player.OpenInventory(quiet: true); }
            catch { ownsInventory = false; LoseIdentity("material-preparation-inventory-open-failed", false); throw; }
            finally { nativeInventoryChange = false; }
            ownsInventory = Main.playerInventory;
            if (!ownsInventory) LoseIdentity("material-preparation-inventory-open-failed", false);
            return ownsInventory;
        }
        private bool CloseNativeInventory()
        {
            if (!ownsInventory || !Main.playerInventory || Main.mouseItem == null || !Main.mouseItem.IsAir || !OrdinaryInventoryUi(player)) return false;
            nativeInventoryChange = true;
            try { player.ToggleInv(); }
            catch { ownsInventory = false; LoseIdentity("material-preparation-inventory-close-failed", false); throw; }
            finally { nativeInventoryChange = false; }
            if (Main.playerInventory) return false;
            ownsInventory = false; return true;
        }
        private bool FinishInventoryPhase()
        {
            if (!InputOwnershipCurrent()) return false;
            if (!ownsInventory) return true;
            if (CloseNativeInventory()) return true;
            LoseIdentity("material-preparation-inventory-close-failed", false); return false;
        }
        private bool OwnsNativeCursorOverride
        {
            get
            {
                // SelectedItemState.Update selects vanilla cursor slot 58
                // while this exact prepared Item is carried. BeforeTail has
                // already proved every endpoint; only this cursor override is
                // admissible, and vanilla alone clears it after cursor Air.
                return ownsInventory && player.selectedItem == 58 && Main.mouseItem != null && !Main.mouseItem.IsAir &&
                    Matches(Main.mouseItem, expectedCursor, cursorType, cursorStack);
            }
        }
        private bool CanClickNow
        {
            get
            {
                return tools.Runtime.IsSessionActive && tools.Input.CanRunAutomaticActions && PreparationInterfaceAllowed &&
                    !tools.Use.Active && !player.dead && !player.CCed && !player.cursed && !player.noItems && !player.isOperatingAnotherEntity &&
                    !player.HasLockedInventory() && !Main.gamePaused && !Main.gameMenu && !Main.blockInput && !Main.mapFullscreen &&
                    !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                    !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !PlayerInput.UsingGamepadUI &&
                    player.chest == -1 && player.talkNPC < 0 && player.sign < 0 && Main.npcShop == 0 &&
                    !Main.LocalPlayerHasPendingInventoryActions() && player.itemAnimation == 0 && player.itemTime == 0 && player.reuseDelay == 0 &&
                    (!player.selectedItemState.HasActiveOverride || OwnsNativeCursorOverride) &&
                    !player.selectedItemState.HasBufferedChange && !Modifiers && OrdinaryInventoryUi(player);
            }
        }
        private bool PreparationInterfaceAllowed
        {
            get
            {
                if (!ownsInventory) return tools.CanInterface?.Invoke() ?? false;
                // This is continuation of the one exact native ItemSlot use,
                // never permission to start a new preparation or gameplay use.
                // Keep Shell's real text/pointer/overlay owners authoritative
                // while accepting hover from our already-open inventory.
                return (phase == Phase.Preparing || phase == Phase.Returning) && Main.playerInventory && SameSession(player) &&
                    !PhysicalInput && ExpectedCurrent(false) && (tools.CanInventoryPreparationInterface?.Invoke() ?? false);
            }
        }
        private void BeforeTail()
        {
            if (!OnGameThread || !HasWork || phase == Phase.Using || phase == Phase.Ready || phase == Phase.Unknown) return;
            // A skipped tail cannot be replaced by another scheduling call.
            // Its original input write must finish before a next native click.
            if (tailOpen) { LoseIdentity("material-preparation-native-tail-missing", false); return; }
            if (!SameSessionIdentity(player)) { ClearSession(); return; }
            if (!InputOwnershipCurrent()) return;
            if (!ExpectedCurrent(false)) { LoseIdentity("material-preparation-endpoint-changed", false); return; }
            if (!PreparationItemsNotPending()) { Reason = "material-preparation-endpoint-pending"; return; }
            if (Reason == "material-preparation-endpoint-pending") Reason = null;
            if (!CanClickNow) return;
            if (phase == Phase.Returning && historyCount == 0) { FinishReturn(); return; }
            // Real ItemSlot clicks may leave the cursor occupied for several
            // Draw/Update epochs. Vanilla dropItemCheck returns/drops it when
            // the inventory is closed, so hold the actual UI until cursor Air.
            if (!OpenOwnedInventory()) return;
            tailStep = Main.mouseLeftRelease ? TailStep.Press : TailStep.Neutral;
            oldLeft = Main.mouseLeft; ownLeft = tailStep == TailStep.Press; Main.mouseLeft = ownLeft; tailOpen = true; clickApplied = false;
            if (tailStep == TailStep.Neutral) return;
            activeSlot = phase == Phase.Returning ? history[historyCount - 1] : forward[forwardCursor];
            int context = activeSlot >= 54 ? 2 : 0;
            var beforeSlot = inventory[activeSlot]; var beforeCursor = Main.mouseItem;
            int slotType = beforeSlot.type, slotStack = beforeSlot.stack, oldCursorType = beforeCursor.type, oldCursorStack = beforeCursor.stack;
            if (ItemSlot.PickItemMovementAction(inventory, context, activeSlot, beforeCursor) != 0 ||
                !beforeSlot.IsAir && !beforeCursor.IsAir && Item.CanStack(beforeSlot, beforeCursor))
            { LoseIdentity("material-preparation-native-swap-not-legal", false); return; }
            try
            {
                nativeClick = true;
                ItemSlot.LeftClick(inventory, context, activeSlot);
            }
            catch { LoseIdentity("material-preparation-native-click-failed", false); throw; }
            finally { nativeClick = false; }
            bool swapped = Matches(inventory[activeSlot], beforeCursor, oldCursorType, oldCursorStack) &&
                Matches(Main.mouseItem, beforeSlot, slotType, slotStack);
            if (swapped)
            {
                var endpoint = At(activeSlot); endpoint.Expected = inventory[activeSlot]; endpoint.Type = inventory[activeSlot].type; endpoint.Stack = inventory[activeSlot].stack;
                CaptureCursor(Main.mouseItem); clickApplied = true;
            }
            else if (!ExpectedCurrent(false)) LoseIdentity("material-preparation-unexpected-native-movement", false);
        }
        private void AfterTail()
        {
            if (!tailOpen) return;
            bool nativeEdge = tailStep == TailStep.Press ? !Main.mouseLeftRelease : Main.mouseLeftRelease;
            TailStep completed = tailStep;
            RestoreBorrowedInput();
            if (!nativeEdge) { LoseIdentity("material-preparation-native-tail-not-observed", false); return; }
#if DEBUG
            if (completed == TailStep.Neutral) NativeNeutralTails++;
            if (completed == TailStep.Press && clickApplied) NativeClicks++;
#endif
            if (phase == Phase.Unknown) return;
            if (!clickApplied || completed != TailStep.Press)
            {
                if (returnRequested) { returnRequested = false; phase = Phase.Returning; if (historyCount == 0) FinishReturn(); }
                return;
            }
            if (!ExpectedCurrent(false)) { LoseIdentity("material-preparation-post-tail-identity", PhysicalInput); return; }
            if (phase == Phase.Preparing)
            {
                history[historyCount++] = activeSlot; forwardCursor++;
                if (forwardCursor == forwardCount)
                {
                    if (!Main.mouseItem.IsAir || MaterialSlot() != destination) { LoseIdentity("material-preparation-not-ready", false); return; }
                    if (!FinishInventoryPhase()) return;
                    phase = Phase.Ready;
                }
            }
            else if (phase == Phase.Returning)
            { historyCount--; if (historyCount == 0) FinishReturn(); }
            if (HasWork && returnRequested) { returnRequested = false; phase = Phase.Returning; if (historyCount == 0) FinishReturn(); }
            activeSlot = -1;
        }
        private void RestoreBorrowedInput()
        {
            if (!tailOpen) return;
            tailOpen = false;
            if (Main.mouseLeft == ownLeft) Main.mouseLeft = oldLeft;
            tailStep = TailStep.None;
        }
        private void FinishReturn()
        {
            if (!ExpectedCurrent(false) || !Main.mouseItem.IsAir) { LoseIdentity("material-preparation-return-not-complete", PhysicalInput); return; }
            for (int i = 0; i < endpointCount; i++)
            {
                var endpoint = endpoints[i];
                if (endpoint.Slot == sourceSlot) continue;
                if (!Matches(inventory[endpoint.Slot], endpoint.Original, endpoint.OriginalType, endpoint.OriginalStack))
                { LoseIdentity("material-preparation-original-endpoint-not-returned", false); return; }
            }
            if (!FinishInventoryPhase()) return;
            tools.Items.Ownership.EndUse(session, token); ResetReceipt();
        }

#if DEBUG
        private static bool checkTailInstalled;
        // This executes only the original, contiguous two-button release tail.
        // It is a CPU consumer fixture, not a substitute for drawing Terraria.
        internal static void NativeInputTailForChecks()
        {
            if (!checkTailInstalled)
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                var original = AccessTools.Method(typeof(Main), "DoDraw", new[] { typeof(GameTime) });
                Harmony.ReversePatch(original, new HarmonyMethod(typeof(ItemMaterialPreparation).GetMethod(nameof(NativeTail), flags)),
                    typeof(ItemMaterialPreparation).GetMethod(nameof(ExtractNativeTail), flags));
                checkTailInstalled = true;
            }
            BeforeNativeInputTail();
            try { NativeTail(Main.instance, null); }
            finally { AfterNativeInputTail(); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NativeTail(Main instance, GameTime time) { throw new InvalidOperationException("material-native-tail-not-installed"); }
        private static IEnumerable<CodeInstruction> ExtractNativeTail(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int start, end; LocateNativeInputTail(code, out start, out end);
            for (int i = start; i < end; i++)
            {
                var copy = new CodeInstruction(code[i].opcode, code[i].operand); copy.labels.AddRange(code[i].labels); yield return copy;
            }
            var ret = new CodeInstruction(OpCodes.Ret); ret.labels.AddRange(code[end].labels); yield return ret;
        }
#endif
        // Production's DoDraw hook and the DEBUG reverse copy share this exact
        // locked-source boundary. No calls, locals or external branches belong
        // to this original release tail; an ABI change fails closed.
        internal static void LocateNativeInputTail(IList<CodeInstruction> code, out int start, out int end)
        {
            var anchor = AccessTools.Method(typeof(Main), "DrawIMEPanel", Type.EmptyTypes);
            var left = AccessTools.Field(typeof(Main), "mouseLeft"); var right = AccessTools.Field(typeof(Main), "mouseRight");
            var leftRelease = AccessTools.Field(typeof(Main), "mouseLeftRelease"); var rightRelease = AccessTools.Field(typeof(Main), "mouseRightRelease");
            start = end = -1; int anchors = 0;
            for (int i = 0; i < code.Count; i++) if (code[i].Calls(anchor)) { start = i + 1; anchors++; }
            if (anchors != 1 || start < 0 || start >= code.Count) throw new InvalidOperationException("material-native-tail-anchor");
            int leftReads = 0, rightReads = 0, leftWrites = 0, rightWrites = 0;
            for (int i = start; i < code.Count; i++)
            {
                var instruction = code[i];
                if (instruction.blocks.Count != 0) throw new InvalidOperationException("material-native-tail-exception-block");
                if (instruction.LoadsField(left)) leftReads++;
                else if (instruction.LoadsField(right)) rightReads++;
                else if (instruction.StoresField(leftRelease)) leftWrites++;
                else if (instruction.StoresField(rightRelease)) rightWrites++;
                else if (instruction.opcode != OpCodes.Nop && instruction.opcode != OpCodes.Ldc_I4_0 && instruction.opcode != OpCodes.Ldc_I4_1 &&
                    instruction.opcode.FlowControl != FlowControl.Branch && instruction.opcode.FlowControl != FlowControl.Cond_Branch)
                    throw new InvalidOperationException("material-native-tail-unexpected-instruction");
                if (rightWrites == 2) { end = i + 1; break; }
            }
            if (leftReads != 1 || rightReads != 1 || leftWrites != 2 || rightWrites != 2 || end >= code.Count)
                throw new InvalidOperationException("material-native-tail-fields");
            var labels = new Dictionary<Label, int>();
            for (int i = 0; i < code.Count; i++) foreach (var label in code[i].labels) labels[label] = i;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode.FlowControl != FlowControl.Branch && code[i].opcode.FlowControl != FlowControl.Cond_Branch) continue;
                if (!(code[i].operand is Label))
                { if (i >= start && i < end) throw new InvalidOperationException("material-native-tail-branch-kind"); continue; }
                int target;
                if (!labels.TryGetValue((Label)code[i].operand, out target)) throw new InvalidOperationException("material-native-tail-missing-label");
                bool inside = i >= start && i < end, targetInside = target >= start && target < end;
                if (inside && (target < start || target > end) || !inside && targetInside)
                    throw new InvalidOperationException("material-native-tail-open-branch");
            }
        }
    }
}
