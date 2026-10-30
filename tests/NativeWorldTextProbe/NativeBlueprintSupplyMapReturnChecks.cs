using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static Item supplyMapIncoming, supplyMapRecipient;
        private static int supplyMapGetOrDropCalls, supplyMapGetItemCalls, supplyMapMergeCalls, supplyNumberSelections;

        // These observers leave the original mapping, selection and inventory
        // consumers intact. No fixture calls ManualSelection or death return.
        private static void SupplyRepairSelect(Player ___player, int __0)
        { if (ReferenceEquals(___player, Main.LocalPlayer) && __0 == 1) supplyNumberSelections++; }
        private static void SupplyRepairGetOrDrop(Player __instance, Item __0)
        { if (ReferenceEquals(__instance, Main.LocalPlayer) && ReferenceEquals(__0, supplyMapIncoming)) supplyMapGetOrDropCalls++; }
        private static void SupplyRepairGetItem(Player __instance, Item __0)
        { if (ReferenceEquals(__instance, Main.LocalPlayer) && ReferenceEquals(__0, supplyMapIncoming)) supplyMapGetItemCalls++; }
        private static void SupplyRepairFilled(Player __instance, Item __0, int __3, bool __result)
        {
            if (ReferenceEquals(__instance, Main.LocalPlayer) && ReferenceEquals(__0, supplyMapIncoming) && __result &&
                ReferenceEquals(__instance.inventory[__3], supplyMapRecipient) && supplyMapRecipient.stack == 12) supplyMapMergeCalls++;
        }
        private static void ConstructionPlacementSupplyRepair(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.PlacementSupplyRepair");
            patch.Patch(typeof(Player).GetNestedType("SelectedItemState", F).GetMethod("Select", F),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SupplyRepairSelect)));
            patch.Patch(AccessTools.Method(typeof(Player), "GetOrDropItem", new[] { typeof(Item), typeof(GetItemSettings) }),
                prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SupplyRepairGetOrDrop)));
            patch.Patch(AccessTools.Method(typeof(Player), "GetItem", new[] { typeof(Item), typeof(GetItemSettings) }),
                prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SupplyRepairGetItem)));
            patch.Patch(AccessTools.Method(typeof(Player), "GetItem_FillIntoOccupiedSlot",
                new[] { typeof(Item), typeof(GetItemSettings), typeof(Item), typeof(int) }),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SupplyRepairFilled)));
            try
            {
                SupplyNumberAfterShutdown(context, host, tools, input);
                SupplyMapFullReturn(context, host, tools, input);
            }
            finally
            {
                supplyMapIncoming = supplyMapRecipient = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
            }
        }
        private static void SupplyRepairInput(object context, object input, params Keys[] keys)
        {
            // Same locked native profile mapping and input epoch as the normal
            // CPU fixture; keyboard values never come from the desktop.
            Call(input, "BeginUpdate");
            var pixel = Vector2.Transform(new Vector2(480, 540) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
            PlayerInput.MouseInfo = new MouseState((int)pixel.X, (int)pixel.Y, 0, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            var tokens = new List<string>(); PlayerInput.Triggers.Reset(); Call(input, "AfterNativeMouse", tokens);
            tokens.AddRange(keys.Select(key => key.ToString()));
            foreach (string token in tokens) PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard]
                .Processkey(PlayerInput.Triggers.Current, token, InputMode.Keyboard);
            PlayerInput.Triggers.Update(); Main.mouseLeft = PlayerInput.Triggers.Current.MouseLeft; Main.mouseRight = PlayerInput.Triggers.Current.MouseRight;
            PlayerInput.MouseX = (int)pixel.X; PlayerInput.MouseY = (int)pixel.Y; PlayerInput.UpdateMainMouse(); PlayerInput.CacheZoomableValues();
            Main.oldKeyState = Main.keyState; Main.keyState = new KeyboardState(keys);
            Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(Get(context, "Shell"), "ProcessInput");
            PlayerInput.SetZoom_World(); Main.LocalPlayer.mouseInterface = false;
        }
        private static void SupplyRepairWorld(object context)
        { NativeQuickItemChecks.BeginWorldStep(); Main.LocalPlayer.Update(0); Call(context, "UpdateRuntime"); }

        private static void SupplyNumberAfterShutdown(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); var preparation = Get(host, "buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.Paintbrush);
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 4;
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7;
            var blue = p.inventory[54]; var red = p.inventory[20];
            NativeToolsChecks.Tile(44, 42, TileID.Stone);
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile,
                TileType = TileID.Stone, TilePaint = red.paint }), 44, 42);
            var bindings = (HotkeyBindings)Get(Get(Get(context, "Shell"), "hotkeys"), "Bindings");
            var oldBinding = bindings.Get("blueprint.autobuild");
            HotkeyChord chord; string reason; long command;
            Require(HotkeyChord.TryCreate((int)Keys.F11, HotkeyModifiers.None, out chord, out reason), "fixture creates an isolated automatic binding");
            Require(bindings.TrySet("blueprint.autobuild", chord, null, out command, out reason), "isolated automatic binding accepted: " + reason);
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Require(bindings.CompletionSucceeded, "isolated automatic binding is durably active before input");
            try
            {
                Step(context, input); SupplyRepairInput(context, input, Keys.F11);
                Require((bool)Get(host, "AutoBuildEnabled"), "real Gameplay binding enables automatic construction");
                // A live production projection owns this preparation. After
                // its real paint consumer settles, the ordinary completion
                // path returns the receipt; a naked TryPrepare under Auto=true
                // is correctly retired by ContinueBuildSupply's absent-target gate.
                Until(context, input, () => (bool)Get(preparation, "Returning") && ReferenceEquals(Main.mouseItem, blue));
                Require(Main.tile[44, 42].color() == red.paint && red.stack == 3 && !(bool)Get(Get(tools, "Use"), "Active"),
                    "the actual single-target consumer spends one paint and naturally leaves its finite return duty");
                Step(context, input); // original Update observes the live cursor override; this tail is neutral
                Require((bool)Get(preparation, "Returning") && Main.playerInventory && ReferenceEquals(Main.mouseItem, blue) && p.selectedItem == 58,
                    "actual inverse ItemSlot swap leaves a hidden owned return cursor and native 58 selection");
                SupplyRepairInput(context, input, Keys.F11);
                Require(!(bool)Get(host, "AutoBuildEnabled") && (bool)Get(preparation, "Returning") &&
                    !(bool)Get(tools, "Enabled") && !(bool)Get(Get(tools, "Use"), "Active") &&
                    (Get(tools, "Combat") == null || !(bool)Get(Get(tools, "Combat"), "Enabled")),
                    "real close binding leaves only finite return duty while every tool/combat Enabled reason is false");
                long clicks = (long)Get(preparation, "NativeClicks"), intent = (long)Get(tools, "SelectionIntent");
                supplyNumberSelections = 0;
                SupplyRepairInput(context, input, Keys.D2);
                Require(PlayerInput.Triggers.Current.Hotbar2, "actual number mapping produces native Hotbar2");
                SupplyRepairWorld(context);
                Require(supplyNumberSelections == 1 && (long)Get(tools, "SelectionIntent") == intent + 1 &&
                    !p.selectedItemState.HasBufferedChange && p.selectedItem == 58,
                    "original Select/Update consumes the number buffer behind cursor58 but retains its genuine manual intent");
                constructionInputTail();
                Require(!(bool)Get(preparation, "HasWork") && !(bool)Get(preparation, "Unknown") &&
                    !(bool)Get(Get(Get(tools, "Items"), "Ownership"), "HasUse") && (long)Get(preparation, "NativeClicks") == clicks &&
                    Main.playerInventory && ReferenceEquals(Main.mouseItem, blue) && !(bool)Get(tools, "Enabled"),
                    "the very next original inventory tail hands over without another inverse click or enabling world automation");
                object[] hidden = { null };
                preparation.GetType().GetMethod("BeginHiddenPresentation", F).Invoke(null, hidden);
                Require(hidden[0] == null, "the ended receipt cannot acquire another hidden presentation scope");
                // An ended receipt cannot close a later player UI or restart
                // its history. Native manual delivery clears the real cursor.
                Step(context, input);
                Require(Main.playerInventory && ReferenceEquals(Main.mouseItem, blue) && (long)Get(preparation, "NativeClicks") == clicks,
                    "release and later native tails keep the player's UI and exact cursor");
                ManualPreparationClick(context, input, p, 18); p.ToggleInv(); Step(context, input);
                Require(p.selectedItem == 1 && !(bool)Call(preparation, "IsDisplaced", blue),
                    "the original number selection survives cursor return and genuine manual main-bag delivery requalifies this Item");
                Console.WriteLine("PASS BlueprintPlacementSupply shutdownBinding=true Enabled=false numberMapping=Hotbar2 SelectUpdate=original nextTailManual=true staleClicks=0");
            }
            finally
            {
                SupplyRepairInput(context, input);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
                Require(bindings.TrySet("blueprint.autobuild", oldBinding, null, out command, out reason), "restore isolated automatic binding: " + reason);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            }
        }

        private static void SupplyMapFullReturn(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); var preparation = Get(host, "buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.RedPaint); p.inventory[12].stack = 3;
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7;
            p.inventory[30].SetDefaults(ItemID.BluePaint); p.inventory[30].stack = 5;
            p.inventory[31].SetDefaults(ItemID.BluePaint); p.inventory[31].stack = 3;
            for (int slot = 55; slot < 58; slot++)
            { p.inventory[slot].SetDefaults(ItemID.MusketBall + slot - 55); p.inventory[slot].stack = p.inventory[slot].maxStack; }
            var blue = p.inventory[54]; var recipient = p.inventory[30]; var unrelated = p.inventory[31];
            bool oldPause = Main.autoPause, oldMapEnabled = Main.mapEnabled;
            try
            {
                Main.autoPause = false; Main.mapEnabled = true;
                Step(context, input); long startClicks = (long)Get(preparation, "NativeClicks");
                Call(preparation, "TryPrepare", new object[] { p, 12, 54, LifecycleIdentity(tools), -1 });
                Until(context, input, () => (long)Get(preparation, "NativeClicks") == startClicks + 2 && ReferenceEquals(Main.mouseItem, blue));
                Require(Main.playerInventory && p.inventory[54].type == ItemID.RedPaint && blue.stack == 7 && recipient.stack == 5 &&
                    !blue.favorited && !recipient.favorited && p.itemAnimation == 0 && p.itemTime == 0 && p.reuseDelay == 0,
                    "second real swap holds old54 B7 while independent main-bag R5 can receive it");
                supplyMapIncoming = blue; supplyMapRecipient = recipient;
                supplyMapGetOrDropCalls = supplyMapGetItemCalls = supplyMapMergeCalls = 0;
                long clicks = (long)Get(preparation, "NativeClicks"), unknown = (long)Get(host, "BuildUnknownResults");
                SupplyRepairInput(context, input, Keys.M);
                Require(PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.Inventory && !PlayerInput.Triggers.Current.Throw &&
                    !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight,
                    "actual MapFull mapping has no inventory/mouse/throw handoff gesture");
                SupplyRepairWorld(context); constructionInputTail();
                Require(Main.mapFullscreen && !Main.playerInventory && Main.mouseItem.IsAir &&
                    supplyMapGetOrDropCalls == 1 && supplyMapGetItemCalls == 1 && supplyMapMergeCalls == 1 &&
                    ReferenceEquals(p.inventory[30], recipient) && recipient.stack == 12 && blue.stack == 7 && unrelated.stack == 3 &&
                    LifecycleQuantity(p, ItemID.BluePaint) == 15,
                    "MapFull->original Update/GetOrDropItem/GetItem fully merges B7 into exact R5=12; incoming remains7 and no carried quantity is lost or copied");
                Require(!(bool)Get(preparation, "HasWork") && !(bool)Get(preparation, "Unknown") &&
                    (bool)Call(preparation, "IsDisplaced", recipient) && !(bool)Call(tools, "BuildInventoryEligible", 30, recipient) &&
                    !(bool)Call(preparation, "IsDisplaced", unrelated) && (bool)Call(tools, "BuildInventoryEligible", 31, unrelated) &&
                    (long)Get(preparation, "NativeClicks") == clicks && (long)Get(host, "BuildUnknownResults") == unknown,
                    "only the real merged recipient inherits old54 exclusion; automatic map closure retires its return duty without Unknown or old clicks");
                SupplyRepairInput(context, input); SupplyRepairWorld(context); constructionInputTail();
                SupplyRepairInput(context, input, Keys.M); SupplyRepairWorld(context); constructionInputTail();
                Require(!Main.mapFullscreen && !Main.playerInventory && (bool)Call(preparation, "IsDisplaced", recipient) &&
                    (long)Get(preparation, "NativeClicks") == clicks, "actual map exit/release grants no supply and cannot reopen old inventory");
                Step(context, input);
                p.inventory[10].SetDefaults(ItemID.Paintbrush);
                var cells = Enumerable.Range(0, 7).Select(i => new CaptureCell { X = i % 3 * 2, Y = i / 3,
                    Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = recipient.paint }).ToArray();
                foreach (var cell in cells) NativeToolsChecks.Tile(42 + cell.X, 39 + cell.Y, TileID.Stone);
                Place(context, host, input, Asset(host, cells), 42, 39); SetAutomatic(host, input, true);
                Until(context, input, () => unrelated.IsAir && !(bool)Get(preparation, "HasWork") && !(bool)Get(Get(tools, "Use"), "Active"));
                long started = (long)Get(host, "BuildUsesStarted");
                for (int frame = 0; frame < 40; frame++) Step(context, input);
                Require(recipient.stack == 12 && (bool)Call(preparation, "IsDisplaced", recipient) && Scene(host).Length == 1 &&
                    (long)Get(host, "BuildUsesStarted") == started && cells.Count(c => Main.tile[42 + c.X, 39 + c.Y].color() == recipient.paint) == 3,
                    "seven real paint demands may spend unrelated3, never automatically promote the merged old54 recipient12");
                SetAutomatic(host, input, false); if (!Main.playerInventory) p.ToggleInv();
                ManualPreparationClick(context, input, p, 30); ManualPreparationClick(context, input, p, 32);
                Require(ReferenceEquals(p.inventory[32], recipient) && !(bool)Call(preparation, "IsDisplaced", recipient),
                    "actual manual pickup and main-bag delivery removes only this exact recipient exclusion");
                p.ToggleInv(); Step(context, input); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(preparation, "HasWork"));
                Require(recipient.stack == 8 && cells.All(c => Main.tile[42 + c.X, 39 + c.Y].color() == recipient.paint),
                    "the genuinely delivered source supplies the remaining four actual consumers and full Match retirement");
                SetAutomatic(host, input, false); Step(context, input);
                Console.WriteLine("PASS BlueprintPlacementSupply MapFullUpdate=true originalGetOrDropGetItem=true fullMerge=5+7=12 incomingUnchanged7=true exactRecipientExcluded=true unrelatedSpent=3 manualDeliveredSpent=4 unknownAdded=0");
            }
            finally
            { supplyMapIncoming = supplyMapRecipient = null; Main.autoPause = oldPause; Main.mapEnabled = oldMapEnabled; }
        }
    }
}
