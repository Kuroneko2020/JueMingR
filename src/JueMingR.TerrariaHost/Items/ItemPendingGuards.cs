using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Items
{
    // Native source slots remain real and retain their original network locks.
    // Guards exclude only an owned range from an operation that can touch it.
    internal static class ItemPendingGuards
    {
        private static HostItems host;
        private static readonly Item unavailable = new Item();
        private static ulong materialTick = ulong.MaxValue;
        private static readonly HashSet<int> projectileMaterials = new HashSet<int>();
        private struct SlotMutation
        {
            internal ItemHostObservation World;
            internal ItemValue Slot, Cursor;
            internal ItemMaterialPreparation Preparation;
            internal bool MainInventory, Physical;
        }
        private readonly struct ItemValue
        {
            private readonly Item item;
            private readonly int type, stack, prefix;
            private readonly bool favorite;
            internal Item Item { get { return item; } }
            internal ItemValue(Item value)
            { item = value; type = value?.type ?? 0; stack = value?.stack ?? 0; prefix = value?.prefix ?? 0; favorite = value?.favorited ?? false; }
            internal bool Matches(Item value)
            { return ReferenceEquals(item, value) && type == (value?.type ?? 0) && stack == (value?.stack ?? 0) && prefix == (value?.prefix ?? 0) && favorite == (value?.favorited ?? false); }
        }
        internal static void Install(HostItems owner, Harmony harmony)
        {
            host = owner;
            Type[] slot = { typeof(Item[]), typeof(int), typeof(int) };
            Patch(harmony, typeof(ItemSlot), "Handle", new[] { typeof(Item[]), typeof(int), typeof(int), typeof(bool) }, nameof(Handle));
            Patch(harmony, typeof(ItemSlot), "LeftClick", slot, nameof(ManualLeftSlot));
            Patch(harmony, typeof(ItemSlot), "RightClick", slot, nameof(ManualRightSlot));
            Patch(harmony, typeof(ItemSlot), "OverrideLeftClick", slot, nameof(OverrideClick));
            Patch(harmony, typeof(ItemSlot), "GetGamepadInstructions", slot, nameof(Gamepad));
            // These existing UI consumers can move a carried capability without
            // GetItem or an automation snapshot. Observe only their two touched
            // endpoints on an actual command edge; idle/hover does no sampling.
            foreach (string name in new[] { "LeftClick", "RightClick", "OverrideLeftClick", "GetGamepadInstructions" })
                harmony.Patch(AccessTools.DeclaredMethod(typeof(ItemSlot), name, slot),
                    prefix: new HarmonyMethod(typeof(ItemPendingGuards), name == "GetGamepadInstructions" ? nameof(BeforeGamepadSlotMutation) : nameof(BeforeSlotMutation)),
                    finalizer: new HarmonyMethod(typeof(ItemPendingGuards), nameof(AfterSlotMutation)));
            Patch(harmony, typeof(ItemSlot), "TryOpenContainer", new[] { typeof(Item[]), typeof(int), typeof(int), typeof(Player) }, nameof(ManualSlot));
            Type[] fill = { typeof(Item), typeof(GetItemSettings), typeof(Item), typeof(int) };
            Patch(harmony, typeof(Player), "GetItem_FillIntoOccupiedSlot", fill, nameof(Fill));
            Patch(harmony, typeof(Player), "GetItem_FillEmptyInventorySlot", fill, nameof(Fill));
            foreach (string name in new[] { "GetItem_FillIntoOccupiedSlot", "GetItem_FillEmptyInventorySlot" })
                harmony.Patch(AccessTools.DeclaredMethod(typeof(Player), name, fill),
                    prefix: new HarmonyMethod(typeof(ItemPendingGuards), nameof(BeforeDeathReturnFill)),
                    finalizer: new HarmonyMethod(typeof(ItemPendingGuards), nameof(AfterDeathReturnFill)));
            Patch(harmony, typeof(Player), "FillAmmo", new[] { typeof(Item), typeof(GetItemSettings) }, transpiler: nameof(ItemGuardTranspilers.AmmoReceiver));
            Patch(harmony, typeof(Player), "PickAmmo_IterateRange", new[] { typeof(Item), typeof(Item[]), typeof(int[]), typeof(bool) }, nameof(Ammo));
            Patch(harmony, typeof(Player), "ItemCheck_TryStartUse", new[] { typeof(Item), typeof(bool) }, nameof(StartUse));
            Patch(harmony, typeof(Player), "DropSelectedItem", new[] { typeof(int), typeof(Item).MakeByRefType() }, nameof(Drop));
            Patch(harmony, typeof(Player), "SellItem", new[] { typeof(Item), typeof(int) }, nameof(Sell));
            Patch(harmony, typeof(Player), "BuyItem", new[] { typeof(long), typeof(int) }, nameof(Buy));
            Patch(harmony, typeof(Player), "ConsumeItem", new[] { typeof(int), typeof(bool), typeof(bool) }, nameof(Consume), nameof(ItemGuardTranspilers.SelectionReads));
            Patch(harmony, typeof(Player), "FindPaintOrCoating", Type.EmptyTypes, transpiler: nameof(ItemGuardTranspilers.SelectionReads));
            Patch(harmony, typeof(Player), "ItemCheck_CheckFishingBobber_ConsumeBait", new[] { typeof(Projectile), typeof(int).MakeByRefType() }, transpiler: nameof(ItemGuardTranspilers.SelectionReads));
            Patch(harmony, typeof(Player), "PlaceThing_Tiles", new[] { typeof(bool) }, nameof(PlaceTiles));
            Patch(harmony, typeof(ItemSorting), "SortInventory", Type.EmptyTypes, nameof(Sort));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(ItemSorting), "SortInventory", Type.EmptyTypes),
                prefix: new HarmonyMethod(typeof(ItemPendingGuards), nameof(BeforeManualSort)),
                finalizer: new HarmonyMethod(typeof(ItemPendingGuards), nameof(AfterManualSort)));
            Patch(harmony, typeof(Player), "PlaceThing_Tiles_CheckWandUsability", new[] { typeof(bool) }, nameof(Wand));
            Patch(harmony, typeof(Player), "PlaceThing_Tiles_PlaceIt_AutoPaintAndActuate", new[] { typeof(Microsoft.Xna.Framework.Vector3[,]), typeof(int) }, transpiler: nameof(ItemGuardTranspilers.ActuatorSelection));
            Patch(harmony, typeof(Player), "ItemCheck_UseWiringTools", new[] { typeof(Item) }, nameof(Wiring));
            foreach (MethodInfo method in typeof(QuickStacking).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.Name == "QuickStackToNearbyChests" || method.Name == "QuickStackToNearbyInventories")
                    harmony.Patch(method, new HarmonyMethod(typeof(ItemPendingGuards).GetMethod(nameof(QuickStack), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static void Patch(Harmony harmony, Type type, string name, Type[] args, string prefix = null, string transpiler = null)
        {
            MethodInfo original = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null);
            if (original == null) throw new MissingMethodException(type.FullName, name);
            harmony.Patch(original, prefix == null ? null : new HarmonyMethod(typeof(ItemPendingGuards).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)),
                null, transpiler == null ? null : new HarmonyMethod(typeof(ItemGuardTranspilers).GetMethod(transpiler, BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Active { get { return host != null && host.World.SessionPlayer != null && !host.World.AutomaticOperation; } }
        private static bool Protected(Item[] array, int slot)
        {
            if(host==null || !Active)return false;
            int account=Account(host.World.SessionPlayer,array);
            return account>=0 && (host.Ownership.IsProtected(account,slot) || account==0 && slot>=0 && slot<array.Length && (host.ConstructionPendingProtected?.Invoke(array[slot])??false)) &&
                !(host.AllowsOwnedProcessing!=null && host.AllowsOwnedProcessing(array,slot)) &&
                !(host.AllowsOwnedRecovery!=null && host.AllowsOwnedRecovery(array,slot)) &&
                !(host.AllowsOwnedTools!=null && host.AllowsOwnedTools(array,slot)) &&
                !(host.AllowsOwnedCombat!=null && host.AllowsOwnedCombat(array,slot)) &&
                !(host.AllowsOwnedConstructionMaterial?.Invoke(array,slot)??false) &&
                !(account==0 && host.Ownership.IsUseSlot(slot) && host.AllowsOwnedUse!=null && host.AllowsOwnedUse(slot));
        }
        private static int Account(Player p,Item[] array)
        {return ReferenceEquals(p.inventory,array)?0:ReferenceEquals(p.bank.item,array)?1:ReferenceEquals(p.bank2.item,array)?2:ReferenceEquals(p.bank3.item,array)?3:ReferenceEquals(p.bank4.item,array)?4:ReferenceEquals(p.armor,array)?5:ReferenceEquals(p.miscEquips,array)?6:-1;}
        private static bool Protected(Item item)
        {
            if (host == null || !host.Ownership.AnyProtected && !(host.HasConstructionPending?.Invoke()??false) || item == null || !Active) return false;
            Item[] inv = host.World.SessionPlayer.inventory;
            for (int i = 0; i < 58; i++) if (ReferenceEquals(item, inv[i]) && Protected(inv, i)) return true;
            Player p=host.World.SessionPlayer;
            if(host.Ownership.HasFallEquipment)
            {
                // Only a live rescue has these two small real equipment ranges.
                // Other item operations retain their original five accounts.
                for(int i=0;i<Math.Min(20,p.armor.Length);i++)if(ReferenceEquals(item,p.armor[i]) && Protected(p.armor,i))return true;
                for(int i=0;i<Math.Min(5,p.miscEquips.Length);i++)if(ReferenceEquals(item,p.miscEquips[i]) && Protected(p.miscEquips,i))return true;
            }
            for(int a=1;a<5;a++){Item[] bank=a==1?p.bank.item:a==2?p.bank2.item:a==3?p.bank3.item:p.bank4.item;for(int i=0;i<40;i++)if(host.Ownership.IsProtected(a,i) && ReferenceEquals(item,bank[i]) && Protected(bank,i))return true;}
            return false;
        }
        internal static Item ReadSelectionSlot(Item[] array, int slot)
        { return Protected(array, slot) ? unavailable : array[slot]; }
        internal static int ReadAmmoSlotType(Item[] array, int slot)
        { return Protected(array, slot) ? -1 : array[slot].type; }
        private static void Handle(Item[] __0, int __1, int __2, ref bool __3)
        { Remember(__0, __2); if (Protected(__0, __2) || TrashRange(__0, __2)) __3 = false; }
        private static bool ManualSlot(Item[] __0, int __1, int __2)
        { if(ItemMaterialPreparation.Current?.AllowsNativeSlot(__0,__1,__2)==true)return true;Remember(__0, __2); return !Protected(__0, __2) && !TrashRange(__0, __2); }
        private static void NotifyManualMutation(Item[] array, int slot)
        {
            // Explicit physical click intent hands control to the player even
            // if a later vanilla animation gate declines the write. Hover is
            // neither takeover nor an inventory-mutation notification.
            if (Active) host.World.BeforeManualFallSlot?.Invoke(array, slot);
        }
        private static bool GamepadEdge()
        {
            var pressed = Terraria.GameInput.PlayerInput.Triggers.JustPressed;
            bool grapple, smart;
            return Terraria.GameInput.PlayerInput.AllowExecutionOfGamepadInstructions &&
                (pressed.KeyStatus.TryGetValue("Grapple", out grapple) && grapple || pressed.KeyStatus.TryGetValue("SmartCursor", out smart) && smart);
        }
        private static void BeforeSlotMutation(Item[] __0, int __2, out SlotMutation __state)
        { CaptureSlotMutation(__0, __2, Main.mouseLeft && Main.mouseLeftRelease || Main.mouseRight, out __state); }
        private static void BeforeGamepadSlotMutation(Item[] __0, int __2, out SlotMutation __state)
        { CaptureSlotMutation(__0, __2, Active && GamepadEdge(), out __state); }
        private static void CaptureSlotMutation(Item[] array, int slot, bool commandEdge, out SlotMutation state)
        {
            state = default(SlotMutation);
            if (!commandEdge || !Active || array == null || slot < 0 || slot >= array.Length) return;
            int account = Account(host.World.SessionPlayer, array);
            if (account != 0 && account != 5 && account != 6) return;
            state = new SlotMutation { World = host.World, Slot = new ItemValue(array[slot]), Cursor = new ItemValue(Main.mouseItem),
                Preparation = ItemMaterialPreparation.Current, MainInventory = account == 0 && slot < 50,
                Physical = PhysicalInventoryCommand && ItemMaterialPreparation.Current?.NativeMutation != true };
        }
        private static Exception AfterSlotMutation(Item[] __0, int __2, SlotMutation __state, Exception __exception)
        {
            if (__state.World != null && (!__state.Slot.Matches(__0[__2]) || !__state.Cursor.Matches(Main.mouseItem)))
            {
                if (__state.Physical) __state.Preparation?.AfterManualMutation(__state.Slot.Item, __state.Cursor.Item,
                    __0[__2], Main.mouseItem, __state.MainInventory);
                __state.World.InvalidateObservation();
            }
            return __exception;
        }
        private static bool ManualLeftSlot(Item[] __0, int __1, int __2)
        { if(ItemMaterialPreparation.Current?.AllowsNativeSlot(__0,__1,__2)==true)return true;if (Main.mouseLeft && Main.mouseLeftRelease) NotifyManualMutation(__0, __2); return ManualSlot(__0, __1, __2); }
        private static bool ManualRightSlot(Item[] __0, int __1, int __2)
        { if (Main.mouseRight) NotifyManualMutation(__0, __2); return ManualSlot(__0, __1, __2); }
        private static bool Gamepad(Item[] __0, int __1, int __2, ref string __result)
        {
            // GetGamepadInstructions is also the native command consumer. Its
            // locked-.8 CanExecuteCommand gate is this flag; only actual UI
            // command edges may hand an intersecting rescue slot to the player.
            if (GamepadEdge()) NotifyManualMutation(__0, __2);
            if (ManualSlot(__0, __1, __2)) return true; __result = string.Empty; return false;
        }
        private static bool TrashRange(Item[] array, int slot)
        { return Active && host.Ownership.DiscardBlocked && slot >= 0 && slot < array.Length && ReferenceEquals(array[slot], host.World.SessionPlayer.trashItem); }
        private static bool OverrideClick(Item[] __0, int __1, int __2, ref bool __result)
        {
            if(ItemMaterialPreparation.Current?.AllowsNativeSlot(__0,__1,__2)==true)return true;
            if (Main.mouseLeft && Main.mouseLeftRelease) NotifyManualMutation(__0, __2);
            bool blocked = !ManualSlot(__0, __1, __2) || Active &&
                (Main.cursorOverride == 10 && host.Ownership.SaleBlocked || Main.cursorOverride == 6 && host.Ownership.DiscardBlocked);
            if (!blocked) return true;
            __result = true; return false;
        }
        private static void Remember(Item[] array, int slot)
        {
            if (host == null || host.World.Player == null || !host.Feature.Enabled || !Active) return;
            if (!ReferenceEquals(array, host.World.SessionPlayer.inventory))
            { if (Main.mouseLeft || Main.mouseRight) host.Storage.InvalidateCapacity(); return; }
            if (slot < 0 || slot >= 58) return;
            if(host.ControlledBag?.Invoke(array,slot)??false)return;
            // Handle runs on hover even with both buttons released. That is not
            // a manual transaction; only physical input creates this fact.
            if(Terraria.GameInput.PlayerInput.MouseInfo.LeftButton==Microsoft.Xna.Framework.Input.ButtonState.Released &&
                Terraria.GameInput.PlayerInput.MouseInfo.RightButton==Microsoft.Xna.Framework.Input.ButtonState.Released)return;
            // Physical release clears this in observation; vanilla consumes its
            // mouse flags before that point, so those flags cannot end ownership.
            host.World.ManualSlot = slot; host.World.ManualItem = array[slot];
        }
        private static bool Fill(Player __instance, int __3, ref bool __result)
        { if (!Protected(__instance.inventory, __3)) return true; __result = false; return false; }
        private static void Ammo(Item[] __1, ref int[] __2)
        { if (Active && host.Ownership.ProtectedSlots != 0 && ReferenceEquals(__1, host.World.SessionPlayer.inventory)) __2 = __2.Where(i => !host.Ownership.IsProtected(i)).ToArray(); }
        private static bool StartUse(Player __instance, Item __0, ref bool __result)
        {
            if (ItemMaterialPreparation.BlocksNativeCursorUse(__instance, __0)) { __result = false; return false; }
            bool current=ReferenceEquals(__instance,host?.World.SessionPlayer);
            // A rescue keeps both exchange endpoints protected throughout the
            // native call. Only its exact once-only use may cross this gate;
            // never relax Protected, which also serves movement/sale consumers.
            // Other Player objects may not consume an alias of the real owned
            // item either. Their independent native material searches retain
            // their own inventory semantics, without borrowing this session.
            if((!current || !PendingDependency(__instance,__0)) && (!Protected(__0) || current && (host.World.AllowsOwnedFallNativeUse?.Invoke(__instance,__0)??false)))return true;
            __result=false;return false;
        }
        private static bool Drop(Player __instance, int __0)
        { return !ItemMaterialPreparation.BlocksNativeCursorDrop(__instance, __0) && !Protected(__instance.inventory, __0); }
        private static void BeforeDeathReturnFill(Player __instance, Item __0, int __3, out ItemMaterialPreparation.DeathReturnScope __state)
        { __state = ItemMaterialPreparation.BeforeDeathReturn(__instance, __0, __3); }
        private static Exception AfterDeathReturnFill(ItemMaterialPreparation.DeathReturnScope __state, Exception __exception)
        { ItemMaterialPreparation.AfterDeathReturn(__state, __exception); return __exception; }
        private static bool QuickStack(Player __0, MethodBase __originalMethod)
        {
            if (!Active || !ReferenceEquals(__0, host.World.SessionPlayer)) return true;
            // Native storage shares scratch/network state even for disjoint
            // slots. Other uncertain actions protect only intersecting writes.
            if (host.Ownership.StoreBlocked) return false;
            if (host.Ownership.ProtectedSlots == 0) return true;
            bool movesCoins = __originalMethod.Name == "QuickStackToNearbyInventories";
            for (int slot = 0; slot < 58; slot++)
            {
                if (!host.Ownership.IsProtected(slot)) continue;
                Item item = __0.inventory[slot];
                if (item == null || item.IsAir || item.favorited) continue;
                bool coin = item.type >= 71 && item.type <= 74;
                // .8 Pack selects ordinary slots 10..49. MoveCoins only
                // removes/restores existing non-favorite coin slots, not Air.
                if (coin ? movesCoins : slot >= 10 && slot < 50) return false;
            }
            return true;
        }
        private static bool Sort() { return !Active || host.Ownership.ProtectedSlots == 0; }
        private static bool PhysicalInventoryCommand
        {
            get { return Terraria.GameInput.PlayerInput.MouseInfo.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                Terraria.GameInput.PlayerInput.MouseInfo.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed || GamepadEdge(); }
        }
        private static void BeforeManualSort(out ItemMaterialPreparation.ManualSortState __state)
        {
            __state = Active && PhysicalInventoryCommand && host.Ownership.ProtectedSlots == 0 ?
                ItemMaterialPreparation.Current?.ManualSortItems() : null;
        }
        private static Exception AfterManualSort(ItemMaterialPreparation.ManualSortState __state, Exception __exception)
        {
            if (__state != null && __exception == null)
            {
                ItemMaterialPreparation.Current?.AfterManualSort(__state);
                host.World.InvalidateObservation();
            }
            return __exception;
        }
        private static bool Buy(Player __instance, int __1, ref bool __result)
        {
            if (!Active || !ReferenceEquals(__instance, host.World.SessionPlayer) || !host.Ownership.AnyProtected || (host.AllowsOwnedPayment?.Invoke()??false) || (host.AllowsProcessingPayment?.Invoke()??false)) return true;
            // Custom currency backs up every source inventory. Normal currency
            // also writes change into empty slots, including an early Air reply
            // whose sibling source slots have not completed the batch yet.
            bool conflict = __1 != -1;
            for(int a=0;a<5 && !conflict;a++)
            {
                var array=a==0?__instance.inventory:a==1?__instance.bank.item:a==2?__instance.bank2.item:a==3?__instance.bank3.item:__instance.bank4.item;
                for(int i=0;i<(a==0?54:40) && !conflict;i++)if(host.Ownership.IsProtected(a,i)){Item item=array[i];conflict=item==null || item.IsAir || item.type>=71 && item.type<=74;}
            }
            if (!conflict) return true;
            __result = false; return false;
        }
        private static bool Sell(Player __instance, ref bool __result)
        {
            // Mouse-held shop sales bypass the inventory's cursor override.
            // Every native sale owns the same 58-slot backup/coin footprint.
            if (!Active || !ReferenceEquals(__instance, host.World.SessionPlayer) || !host.Ownership.SaleBlocked) return true;
            __result = false; return false;
        }
        private static bool PlaceTiles(Player __instance)
        {
            if (!Active || !ReferenceEquals(__instance, host.World.SessionPlayer) || host.Ownership.ProtectedSlots == 0 && !(host.HasConstructionPending?.Invoke()??false)) return true;
            Item held = __instance.inventory[__instance.selectedItem];
            if (Protected(held) || PendingType(held.tileWand)) return false;
            FlexibleTileWand flexible = held.GetFlexibleTileWand();
            return flexible == null || !flexible.ConsumesAmmoItem ||
                !flexible.TryGetPlacementOption(__instance, Player.FlexibleWandRandomSeed, Player.FlexibleWandCycleOffset, out var option, out Item material) || !Protected(material);
        }
        private static void Consume(Player __instance, int __0, bool __1)
        {
            if (!Active || !ReferenceEquals(__instance, host.World.Player) || host.World.CausalDepth == 0) return;
            for (int n = 0; n < 58; n++)
            {
                int i = __1 ? 57 - n : n; Item item = __instance.inventory[i];
                if (item.type == __0 && item.stack > 0 && !Protected(item))
                {
                    if(!(host.AllowsOwnedProcessing?.Invoke(__instance.inventory,i)??false) &&
                        !(host.AllowsOwnedTools?.Invoke(__instance.inventory,i)??false) &&
                        !(host.AllowsOwnedConstructionMaterial?.Invoke(__instance.inventory,i)??false))host.World.ManualMaterials.Add(item);
                    return;
                }
            }
        }
        private static bool Wand(Player __instance, ref bool __result)
        { if (!Active || !ReferenceEquals(__instance, host.World.SessionPlayer) || !PendingType(__instance.inventory[__instance.selectedItem].tileWand)) return true; __result = false; return false; }
        internal static int FindAvailableItem(Player player, int type)
        { int slot = player.FindItem(type); return slot >= 0 && Protected(player.inventory, slot) ? -1 : slot; }
        private static bool Wiring(Player __instance, Item __0)
        { return !ReferenceEquals(__instance, host?.World.SessionPlayer) || !PendingDependency(__instance, __0); }
        private static bool PendingType(int type)
        {
            if (!Active || type <= 0 || host.Ownership.ProtectedSlots == 0 && !(host.HasConstructionPending?.Invoke()??false)) return false;
            // Match the first original main-inventory consumer, not every stack
            // of this type. A protected later stack does not block an earlier one.
            Item[] inv = host.World.SessionPlayer.inventory;
            for (int i = 0; i < 58; i++) if (inv[i].type == type && inv[i].stack > 0) return Protected(inv,i);
            return false;
        }
        private static bool WireTool(Item item)
        { return item.type == 509 || item.type == 850 || item.type == 851 || item.type == 3612 || item.type == 3625 || item.shoot == 651; }
        private static bool PendingDependency(Player player, Item item)
        {
            if (item == null) return false;
            if (PendingType(item.tileWand)) return true;
            // MassWire counts the whole inventory before spending. A first
            // available stack does not make a later owned stack safe to spend.
            if (item.shoot == 651 || item.type == 3625)
            {
                if (!Active) return false;
                for (int i = 0; i < 58; i++) if (Protected(player.inventory,i) &&
                    (player.inventory[i].type == 530 || item.shoot == 651 && player.inventory[i].type == 849)) return true;
                return false;
            }
            return WireTool(item) && PendingType(530);
        }
        internal static bool ProtectActiveMaterial(Item item)
        {
            Player player = host.World.Player;
            if (player == null || item == null || item.IsAir) return false;
            Item held = player.inventory[player.selectedItem];
            // The finite build lease already protects its exact source slots.
            // Do not let its own native animation masquerade as a foreign
            // held use and revoke that same lease. Other ownership guards and
            // projectile material protection below remain independently live.
            if (!(host.OwnsBuildHeldMaterial?.Invoke(item) ?? false) && (player.itemAnimation > 0 || player.itemTime > 0 || player.channel))
            {
                if (ReferenceEquals(item, held) || held.useAmmo > 0 && item.ammo == held.useAmmo || held.tileWand > 0 && item.type == held.tileWand ||
                    item.PaintOrCoating && player.autoPaint || item.type == 849 && player.autoActuator || WireTool(held) && (item.type == 530 || item.type == 849)) return true;
                FlexibleTileWand flexible = held.GetFlexibleTileWand();
                if (flexible != null && flexible.TryGetPlacementOption(player, Player.FlexibleWandRandomSeed, Player.FlexibleWandCycleOffset, out var option, out Item material) && ReferenceEquals(material, item)) return true;
            }
            if (item.bait <= 0 && item.type != 530 && item.type != 849) return false;
            if(host.OwnsBuildHeldMaterial?.Invoke(item)==true)return false;
            // One bounded scan on a relevant observation tick, not per slot.
            if (materialTick != host.Tick)
            {
                materialTick = host.Tick; projectileMaterials.Clear();
                foreach (Projectile projectile in Main.projectile)
                {
                    if (projectile == null || !projectile.active || projectile.owner != player.whoAmI) continue;
                    if (projectile.bobber && projectile.localAI[2] > 0) projectileMaterials.Add((int)projectile.localAI[2]);
                    if (projectile.type == 651) { projectileMaterials.Add(530); projectileMaterials.Add(849); }
                }
            }
            return projectileMaterials.Contains(item.type);
        }
    }
}
