using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Features.Items;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Shell + real native UI consumers in the one isolated .8 fixture.
    // Only physical samples/foreground and its bounded world-step clock are
    // supplied by the probe. No business button handlers create the copy/power.
    internal static class NativeJourneyUiChecks
    {
        internal static void Run(object context, ProbeGraphics graphics, string output, bool reopenOnly = false)
        {
            var h = new Frames(context, graphics);
            object shell = Get(context, "Shell"), state = Get(shell, "State"), journey = Get(shell, "journey"), display = Get(context, "ShortFeedback");
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var access = (HiddenFeatureAccess)Get(shell, "hiddenAccess");
            Require(!((bool)Get(journey, "IsActive")) && !access.IsUnlocked && registry.Find("journey.toggle") == null && bindings.Get("journey.toggle") == null,
                "Journey starts OFF/Locked/unbound in the complete Host");
            NativeHiddenEntryChecks.RevealForComposition(context);
            Call(Get(shell, "hotkeys"), "PollJourneyRegistration");
            Require(registry.Find("journey.toggle") != null && !((bool)Get(journey, "IsActive")), "real reveal publishes binding without enabling Journey");
            PrepareNativeMenu(graphics);
            NativeToolsUiChecks.Prepare(context, 1, 960, 640, 1);
            h.Ui();
            h.HoverShell("EnableJourney");
            graphics.Image(Path.Combine(output, "journey-f5-row.png"), () => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
            RunInputChecks(context, graphics, output, h, shell, state, journey, display, registry, bindings, reopenOnly);
        }

        internal static void PrepareNativeMenu(ProbeGraphics graphics)
        {
            Call(graphics, "EnsureNativeAssets");
            PrepareNativeItemAssets(graphics);
            // Even singleplayer creative buttons send a real module packet
            // through NetManager.SendToSelf. Main.Initialize6844 normally loads
            // the whole protocol registry; an empty registry silently drops it.
            int modules = (ushort)Get(Terraria.Net.NetManager.Instance, "_moduleCount");
            Console.WriteLine("JOURNEY UI native-network-before modules=" + modules + " creative-module=" +
                (Terraria.Net.NetManager.Instance.GetModule<Terraria.GameContent.NetModules.NetCreativePowersModule>() != null));
            if (modules == 0) Terraria.Initializers.NetworkInitializer.Load();
            Require(Terraria.Net.NetManager.Instance.GetModule<Terraria.GameContent.NetModules.NetCreativePowersModule>() != null,
                "original complete network registry supplies actual singleplayer creative packet loopback");
            // Main.Initialize builds recipes, fixes content-sample material
            // flags, then rebuilds the complete creative sorting domain. The
            // shared narrow fixture only initialized samples; the real menu
            // sorter requires this original startup stage for every item ID.
            if (Recipe.numRecipes == 0)
            {
                Recipe.SetupRecipeGroups();
                for (int i = 0; i < Main.recipe.Length; i++) Main.recipe[i] = new Recipe();
                Recipe.SetupRecipes(); ContentSamples.FixItemsAfterRecipesAreAdded();
            }
            // The same native startup stage also initializes inventory sorting.
            // QuickStacking classifies destination items through its private
            // layer-index array; creative sort IDs alone do not supply it.
            Console.WriteLine("JOURNEY UI inventory-sort-before layers=" + ItemSorting.LayerCount + " index-array-null=" +
                (typeof(ItemSorting).GetField("_layerIndexForItemType", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null) == null));
            ItemSorting.SetupWhiteLists();
            ContentSamples.RebuildItemCreativeSortingIDsAfterRecipesAreSetUp();
            // The native builder keys by Item.SetDefaults(i).type, not by i:
            // e.g. 226 becomes 227, 1803..1807 become 1533..1537 and deprecated
            // IDs become air. Verify that complete normalized native domain;
            // demanding every raw net ID would reject the original initializer.
            int[] sortDomain = ContentSamples.ItemsByType.Where(pair => pair.Key > 0 && !pair.Value.IsAir).Select(pair => pair.Value.type).Distinct().ToArray();
            int[] missingSortTypes = sortDomain.Where(id => !ContentSamples.ItemCreativeSortingId.ContainsKey(id)).ToArray();
            Console.WriteLine("JOURNEY UI sort-domain native-types=" + sortDomain.Length + " missing=" + string.Join(",", missingSortTypes) +
                " raw-id-normalization=" + string.Join(",", ContentSamples.ItemsByType.Where(pair => pair.Key > 0 && !ContentSamples.ItemCreativeSortingId.ContainsKey(pair.Key)).Select(pair => pair.Key + "->" + pair.Value.type)));
            Require(sortDomain.Length > 0 && missingSortTypes.Length == 0, "native recipe/content startup builds the complete non-air normalized creative sort domain");
            // Main.Initialize normally loads the complete native navigation map.
            // CreativeUI.DrawToggleButton uses point 311 even with keyboard UI;
            // use that initializer rather than inventing an isolated dictionary key.
            if (Terraria.UI.Gamepad.UILinkPointNavigator.Points.Count == 0) Terraria.Initializers.UILinksInitializer.Load();
            Require(Terraria.UI.Gamepad.UILinkPointNavigator.Points.ContainsKey(311), "original UI navigation initializer supplies the native creative toggle point");
            Main.CreativeMenu.Initialize();
        }

        private static void RunInputChecks(object context, ProbeGraphics graphics, string output, Frames h, object shell, object state,
            object journey, object display, HotkeyRegistry registry, HotkeyBindings bindings, bool reopenOnly)
        {
            var failure = new Harmony("JueMingR.Tests.JourneyUi.OpenFailure");
            var toggle = typeof(Player).GetMethod("ToggleCreativeMenu");
            try
            {
                failure.Patch(toggle, prefix: new HarmonyMethod(typeof(NativeJourneyUiChecks), nameof(RejectCreativeToggle)));
                h.ClickShell("EnableJourney");
                Require(!(bool)Get(journey, "IsActive") && !(bool)Get(journey, "IsMenuOpen") && (bool)Get(state, "Visible"),
                    "unconfirmed native open withdraws temporary capability and leaves F5 reachable");
                Require(!string.IsNullOrEmpty((string)GetOptional(journey, "LastFailure")) && NativeShortFeedbackChecks.Entries(display).Any(e => (string)Get(e, "Id") == "journey.failure") && !NativeShortFeedbackChecks.Has(display, "旅行菜单 已开启"),
                    "failed open publishes a real failure instead of clearing it during rollback or claiming success");
            }
            finally { failure.Unpatch(toggle, HarmonyPatchType.Prefix, failure.Id); }
            Call(display, "Clear");
            var firstOpen = Stopwatch.StartNew(); h.ClickShell("EnableJourney"); firstOpen.Stop();
            Require((bool)Get(journey, "IsActive") && (bool)Get(journey, "IsMenuOpen") && !(bool)Get(state, "Visible"), "real standard Enable opens the confirmed native window before F5 yields");
            Require(!NativeShortFeedbackChecks.Has(display, "旅行菜单 已开启"), "ordinary F5 enable does not pretend to be a hotkey result");
            Require(!Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft, "opening release reaches neither copy nor attack consumer");
            Console.WriteLine("JOURNEY UI first-successful-open-ms=" + firstOpen.Elapsed.TotalMilliseconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " scope=real-Shell-click/native-open; prior-rejected-open-already-warmed-shared-path");
            for (int i = 0; i < 16; i++) h.Native(Vector2.Zero);

            object nativeState = Get(Main.CreativeMenu, "_uiState");
            var categories = (IDictionary)Get(Get(nativeState, "_mainCategory"), "Buttons");
            if (!reopenOnly)
            {
            h.ClickNative((UIElement)categories[1]);
            var infinite = (UIElement)Get(nativeState, "_infiniteItemsWindow");
            UISearchBar search = Walk(infinite).OfType<UISearchBar>().Single();
            h.ClickNative(search.Parent);
            Require(search.IsWritingText && PlayerInput.WritingText && ReferenceEquals(Main.CurrentInputTextTakerOverride, search), "native search owns text after actual pointer input");
            const string query = "Dirt Block";
            Main.keyCount = query.Length;
            for (int i = 0; i < query.Length; i++) { Main.keyInt[i] = query[i]; Main.keyString[i] = query[i].ToString(); }
            h.Native(Center(search));
            Require((string)Get(search, "actualContents") == query && Main.keyCount == 0, "native text queue reaches original search and is consumed once");
            h.ClickNative(search.Parent); h.Native(Vector2.Zero);
            Require(!search.IsWritingText && !PlayerInput.WritingText, "search releases native text ownership on a fresh click");
            var grid = (UICreativeItemGrid)Get(infinite, "_itemGrid");
            var shown = ((IEnumerable)grid.GetType().BaseType.GetField("_contents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(grid)).Cast<Item>().ToArray();
            Require(shown.Length > 0 && shown.Any(item => item.type == ItemID.DirtBlock), "real searched grid contains a researched Dirt Block");
            int index = Array.FindIndex(shown, item => item.type == ItemID.DirtBlock);
            object[] parameters = { 0, 0, 0, 0 };
            grid.GetType().BaseType.GetMethod("GetGridParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(grid, parameters);
            int columns = grid.GetItemsPerLine(), offset = index - (int)parameters[2];
            Require(offset >= 0 && index < (int)parameters[3] && columns > 0, "searched native item is in the actual visible grid");
            var point = new Vector2((int)parameters[0] + offset % columns * 44 + 20, (int)parameters[1] + offset / columns * 44 + 20);
            Main.mouseItem.TurnToAir(); Main.stackSplit = 0;
            // Match real move/hover -> press -> release: native UI.Update runs
            // before the fresh mapping, so moving from the search bar and
            // pressing in a single sampled frame still has the old UI hover.
            h.Native(point); h.Native(point, right: true); h.Native(point);
            Require(Main.mouseItem.type == ItemID.DirtBlock && Main.mouseItem.stack > 0, "native grid right click produces a real mouse item");
            int rightAmount = Main.mouseItem.stack;
            Main.mouseItem.TurnToAir(); h.Native(point, left: true); h.Native(point);
            Require(Main.mouseItem.type == ItemID.DirtBlock && Main.mouseItem.stack >= rightAmount, "native grid left click produces its normal stack");
            // Enter the empty real backpack slot through the original slot input
            // consumer with the same mapped press. Never assign a cloned copy.
            Main.LocalPlayer.inventory[10].TurnToAir();
            h.Native(new Vector2(800, 80), left: true, render: false);
            ItemSlot.Handle(Main.LocalPlayer.inventory, 0, 10);
            h.Native(new Vector2(800, 80));
            Require(Main.mouseItem.IsAir && Main.LocalPlayer.inventory[10].type == ItemID.DirtBlock && Main.LocalPlayer.inventory[10].stack > 0,
                "original inventory slot consumer transfers the real copied stack into the backpack");
            CopySourceChecks(context, h, point);
            }

            h.ClickNative((UIElement)categories[3]);
            if (!reopenOnly) DetachedCatalogBoundary(h, journey, nativeState, (UIElement)Get(nativeState, "_infiniteItemsWindow"), categories);
            var strip = (UIElement)Get(nativeState, "_timePowersStrip");
            UIElement freezeButton = ((IEnumerable)Get(strip, "_buttonsBySorting")).Cast<UIElement>().First();
            var freeze = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
            bool before = freeze.Enabled;
            if (!reopenOnly) FocusBoundary(context, h, freezeButton, freeze);
            h.ClickNative(freezeButton);
            Require(freeze.Enabled != before, "actual native power button sends and consumes its real creative command");
            bool chosen = freeze.Enabled;
            object persistence = Get(journey, "Persistence");
            Require(persistence != null, "active native session owns its captured persistence baseline");
            h.Key(Keys.F5); h.Key();
            Require((bool)Get(state, "Visible") && !(bool)Get(journey, "IsMenuOpen") && (bool)Get(journey, "IsActive") && freeze.Enabled == chosen,
                "F5 reopens its own layer, closes native window and retains continuous powers");
            Call(state, "Navigate", 1); Call(context, "UpdateShell");
            h.ClickShell("EnableJourney");
            Require((bool)Get(journey, "IsActive") && (bool)Get(journey, "IsMenuOpen") && freeze.Enabled == chosen &&
                ReferenceEquals(persistence, Get(journey, "Persistence")) && !(bool)Get(state, "Visible"),
                "repeated real Enable reopens the same active session, persistence baseline and selected power before F5 yields");
            Require(!Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft, "reopen consumes its release before native input");
            Require((bool)Call(journey, "SetEnabled", true) && (bool)Get(journey, "IsMenuOpen") && freeze.Enabled == chosen &&
                ReferenceEquals(persistence, Get(journey, "Persistence")), "legal Enable while visible preserves the window and original baseline");
            for (int i = 0; i < 3; i++) h.Native(Vector2.Zero);
            if (!reopenOnly) MeasureFrames(h, registry, "active-menu-open");
            graphics.Image(Path.Combine(output, "journey-native-menu.png"), () => Main.CreativeMenu.Draw(Main.spriteBatch), Main.UIScaleMatrix);
            Call(journey, "CloseMenu"); h.Native(Vector2.Zero);
            Require((bool)Get(journey, "IsActive") && !(bool)Get(journey, "IsMenuOpen") && (bool)Get(shell, "CanAutomaticProcessingInput") && (bool)Get(shell, "CanAutomaticTargetInput"),
                "active session with closed native window leaves ordinary automatic gates available");
            if (!reopenOnly)
            {
                MeasureFrames(h, registry, "active-menu-closed");
                Coexist(context, h, graphics, journey, categories, output);
            }
            h.Key(Keys.F5); h.Key(); Call(state, "Navigate", 1); Call(context, "UpdateShell");
            h.ClickShell("DisableJourney");
            Require(!(bool)Get(journey, "IsActive") && freeze.Enabled == before, "real F5 close restores the original continuous power");
            Call(shell, "CloseAndSubmitPosition"); h.Native(Vector2.Zero);
            if (!reopenOnly) MeasureFrames(h, registry, "off-after-close");
            h.Key(Keys.F5); h.Key(); Call(state, "Navigate", 1); Call(context, "UpdateShell");
            SetBindingThroughPopup(context, h, bindings);
            Call(shell, "CloseAndSubmitPosition"); Call(display, "Clear");
            h.Key(Keys.LeftControl, Keys.F13); h.Key(Keys.LeftControl, Keys.F13);
            Require((bool)Get(journey, "IsActive") && NativeShortFeedbackChecks.Has(display, "旅行菜单 已开启"), "saved physical binding reaches full owner and existing actual-state feedback exactly once");
            h.Key(); h.Key(Keys.LeftControl, Keys.F13); h.Key();
            Require(!(bool)Get(journey, "IsActive") && NativeShortFeedbackChecks.Has(display, "旅行菜单 已关闭"), "same binding closes active native owner and reports the confirmed reverse result");
            Console.WriteLine(reopenOnly ? "PASS Journey narrow actual Shell Enable/reopen/Disable, same Persistence/selected native power, input tail and original binding toggle." :
                "PASS Journey actual Shell reveal/standard commands/binding recorder, native search/right-left copy/backpack, native power button, retained power, menu reopen and real hotkey feedback.");
        }



        private static void PrepareNativeItemAssets(ProbeGraphics graphics)
        {
            // UIDynamicItemCollection.DrawSelf reads each Asset.State before its
            // normal lazy LoadItem. Supply AssetInitializer.LoadTextures' whole
            // item-array declaration stage, including native texture aliases;
            // actual pixels still come from the original repository/XNB loader.
            var textures = Terraria.GameContent.TextureAssets.Item;
            int[] absent = Enumerable.Range(0, textures.Length).Where(id => textures[id] == null).ToArray();
            Console.WriteLine("JOURNEY UI native item assets before initialization null-count=" + absent.Length + " first-ids=" + string.Join(",", absent.Take(16)));
            for (int i = 0; i < textures.Length; i++)
            {
                int copy = ItemID.Sets.TextureCopyLoad[i];
                textures[i] = copy != -1 ? textures[copy] : Main.Assets.Request<Texture2D>("Images/Item_" + i, AssetRequestMode.DoNotLoad);
            }
            Require(textures.All(asset => asset != null), "original item texture declaration supplies every native lazy-load handle");
            // ItemSlot.Draw compares several original slot-background assets,
            // including ones outside context29's selected background. Keep the
            // complete native InventoryBack domain from LoadTextures535-559.
            graphics.LoadTexture("InventoryBack", "Images/Inventory_Back");
            for (int i = 2; i <= 25; i++) graphics.LoadTexture("InventoryBack" + i, "Images/Inventory_Back" + i);
            // CoinSlot.DrawItemCoin uses this separate four-texture domain,
            // rather than the ordinary Item handles (LoadTextures371).
            for (int i = 0; i < Terraria.GameContent.TextureAssets.Coin.Length; i++) graphics.LoadTexture("Coin", "Images/Coin_" + i, i);
        }

        private static void DetachedCatalogBoundary(Frames h, object journey, object nativeState, UIElement infinite, IDictionary categories)
        {
            Require(infinite.Parent == null, "time category actually detaches the already-searched native item window");
            Require((bool)Call(journey, "SetEnabled", false), "R close invalidates virtual research while its native item window is detached");
            var actualProgress = new HashSet<int>();
            Main.LocalPlayerCreativeTracker.ItemSacrifices.ForEachItemWithResearchProgress(id => actualProgress.Add(id));
            Require(!actualProgress.Contains(ItemID.DirtBlock), "isolated character has no real Dirt Block research to disguise a leaked virtual entry");
            var player = Main.LocalPlayer; var world = Main.ActiveWorldFileData;
            byte originalPlayerMode = player.difficulty; int originalWorldMode = world.GameMode;
            try
            {
                // This disposable context alone temporarily becomes native 3/3.
                // No save occurs here and neither tracker identity nor edit ID
                // changes, so a stale cached virtual list cannot hide behind a
                // replacement tracker or another forged research notification.
                player.difficulty = 3; world.GameMode = 3;
                player.ToggleCreativeMenu(); h.Native(Vector2.Zero);
                h.ClickNative((UIElement)categories[1]); h.Native(Vector2.Zero);
                var originalList = ((IEnumerable)Get(infinite, "_itemList")).Cast<Item>().ToArray();
                Require(!(bool)Get(journey, "IsActive") && originalList.All(item => actualProgress.Contains(item.type)) && originalList.All(item => item.type != ItemID.DirtBlock),
                    "same native 3/3 item window after R close contains only real research, never stale searched virtual Dirt Block");
                h.ClickNative((UIElement)categories[3]);
                Require(infinite.Parent == null, "native time category detaches the same real-only item window before the enable edge");
            }
            finally
            {
                try { if (Main.CreativeMenu.Enabled) player.ToggleCreativeMenu(); }
                finally { player.difficulty = originalPlayerMode; world.GameMode = originalWorldMode; }
            }
            Require((bool)Call(journey, "SetEnabled", true), "R can reactivate after restoring this isolated character/world identity");
            Require(ReferenceEquals(Get(nativeState, "_infiniteItemsWindow"), infinite), "enable edge reuses the same native item window instead of replacing its cache");
            h.ClickNative((UIElement)categories[1]); h.Native(Vector2.Zero);
            Require(((IEnumerable)Get(infinite, "_itemList")).Cast<Item>().Any(item => item.type == ItemID.DirtBlock), "same searched item window returns to virtual research on the new R enable edge");
            h.ClickNative((UIElement)categories[3]);
            Console.WriteLine("PASS Journey detached native catalog: searched virtual list -> native 3/3 real research while OFF -> same window virtual list after re-enable; no tracker edits or temporary-mode save.");
        }

        private static void MeasureFrames(Frames h, HotkeyRegistry registry, string scenario)
        {
            // This bounded probe interval includes reflection-driven Host input,
            // runtime/layout work and original CreativeUI.Draw. It is neither a
            // production-only overhead benchmark nor a complete game FPS test.
            const int count = 100;
            long revision = registry.Revision, total = 0, slowest = 0;
            var watch = new Stopwatch();
            for (int i = 0; i < count; i++)
            {
                watch.Restart(); h.Native(Vector2.Zero); watch.Stop();
                long ticks = watch.ElapsedTicks; total += ticks; slowest = Math.Max(slowest, ticks);
            }
            Require(registry.Revision == revision, scenario + " stable interval never rebuilds the hotkey registry");
            double scale = 1000.0 / Stopwatch.Frequency;
            Console.WriteLine("JOURNEY UI interval=" + scenario + " frames=" + count +
                " total-ms=" + (total * scale).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " mean-ms=" + (total * scale / count).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " slowest-ms=" + (slowest * scale).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " registry-revision=" + revision + " scope=probe-reflection/Host-input-runtime-layout/native-CreativeUI-draw; F5-closed");
        }

        private static void FocusBoundary(object context, Frames h, UIElement button, CreativePowers.FreezeTime freeze)
        {
            object input = Get(context, "Input"); object foreground = Get(input, "foregroundWindow");
            bool before = freeze.Enabled; Vector2 point = Center(button);
            try
            {
                h.Native(point); h.Native(point, left: true);
                Require(freeze.Enabled == before, "fresh physical press is staged after native UI.Update, before the next native click phase");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(2)));
                h.Native(point); h.Native(point);
                Require(freeze.Enabled == before, "first loss and synthetic release cannot turn the previous fresh press into a native power click");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                for (int i = 0; i < 3; i++) h.Native(point);
                Require(freeze.Enabled == before && (bool)Get(input, "CanStartActions"), "real neutral refocus rearms without replaying native menu input");
            }
            finally { Set(input, "foregroundWindow", foreground); }
        }

        private static void CopySourceChecks(object context, Frames h, Vector2 point)
        {
            object shell = Get(context, "Shell"), journey = Get(shell, "journey"), items = Get(context, "items"), input = Get(context, "Input");
            var p = Main.LocalPlayer; Item savedCopy = p.inventory[10]; p.inventory[10] = new Item();
            Chest previousChest = Main.chest[0];
            try
            {
                // A second original grid copy supplies the destructive contrast;
                // the first exact copied object is retained for independent save.
                Call(items, "Change", new ItemAutomationSettings(false, false, true, new int[0], new int[] { ItemID.DirtBlock }, false));
                NativeQuickItemChecks.Until(() => { Call(items, "PollPreferences"); return (bool)Get(items, "ControlsEnabled"); });
                Main.mouseItem.TurnToAir(); h.Native(point); h.Native(point, left: true); h.Native(point);
                Item mouseCopy = Main.mouseItem; int amount = mouseCopy.stack;
                Require(mouseCopy.type == ItemID.DirtBlock && amount > 0, "second native copy enters the real mouse item for the manual-resource contrast");
                for (int i = 0; i < 15; i++) h.Native(point);
                Require(ReferenceEquals(Main.mouseItem, mouseCopy) && mouseCopy.stack == amount && p.trashItem.IsAir, "automatic discard never steals the actual copied mouse item");
                p.inventory[12].TurnToAir(); h.Native(new Vector2(800, 80), left: true, render: false);
                ItemSlot.Handle(p.inventory, 0, 12); h.Native(new Vector2(800, 80));
                Require(Main.mouseItem.IsAir && p.inventory[12].type == ItemID.DirtBlock, "copy enters an ordinary backpack slot through the native input consumer");
                Call(journey, "CloseMenu"); Main.playerInventory = false; p.mouseInterface = p.lastMouseInterface = false;
                for (int i = 0; i < 40 && p.trashItem.IsAir; i++) NativeExtractionChecks.Frame(context, input);
                Require(p.inventory[12].IsAir && p.trashItem.type == ItemID.DirtBlock && p.trashItem.stack == amount,
                    "copied inventory item follows the existing discard list after real release; no permanent copy exemption");
                p.trashItem.TurnToAir();
                p.inventory[0].SetDefaults(ItemID.CopperPickaxe); p.selectedItemState.Select(0); p.selectedItemState.Update();
                Call(items, "Change", new ItemAutomationSettings(true, false, false, new int[0], new int[0], false));
                NativeQuickItemChecks.Until(() => { Call(items, "PollPreferences"); return (bool)Get(items, "ControlsEnabled"); });
                // One nearby original chest accepts the duplicated type. A
                // different pre-existing inventory type must gain no source.
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                { var tile = Main.tile[42 + x, 40 + y]; tile.active(true); tile.type = 21; tile.frameX = (short)(x * 18); tile.frameY = (short)(y * 18); }
                Main.chest[0] = null; var chest = Chest.CreateWorldChest(0, 42, 40);
                chest.item[0].SetDefaults(ItemID.DirtBlock); chest.item[0].stack = 1;
                chest.item[1].SetDefaults(ItemID.Wood); chest.item[1].stack = 1;
                p.inventory[13].SetDefaults(ItemID.Wood); p.inventory[13].stack = 7;
                Require((bool)Call(journey, "Open"), "real native menu reopens for Shift-copy source check");
                h.Native(point); h.Native(point, left: true, keys: new[] { Keys.LeftShift }); h.Native(point);
                int gained = p.inventory.Where(item => item.type == ItemID.DirtBlock).Sum(item => item.stack);
                var acquisitions = (IDictionary)Get(Get(items, "Feature"), "acquisitions");
                Require(gained > 0 && Main.mouseItem.IsAir && acquisitions.Contains(new ItemIdentity(ItemID.DirtBlock, 0)) && !acquisitions.Contains(new ItemIdentity(ItemID.Wood, 0)),
                    "real Shift/context29/cursor7 native GetItem gain captures only the actual duplicated identity");
                Call(journey, "CloseMenu"); Main.playerInventory = false; p.mouseInterface = p.lastMouseInterface = false;
                for (int i = 0; i < 70 && p.inventory.Any(item => item.type == ItemID.DirtBlock); i++) NativeExtractionChecks.Frame(context, input);
                Require(p.inventory.All(item => item.type != ItemID.DirtBlock) && chest.item.Where(item => item.type == ItemID.DirtBlock).Sum(item => item.stack) == gained + 1,
                    "actual native quick stack consumes the finite Shift-copy acquisition with exact chest conservation");
                Require(p.inventory[13].type == ItemID.Wood && p.inventory[13].stack == 7 && chest.item[1].stack == 1,
                    "opening/copying the catalog never marks unrelated existing inventory as newly acquired");
                Call(items, "Change", ItemAutomationSettings.Default); NativeQuickItemChecks.Until(() => { Call(items, "PollPreferences"); return (bool)Get(items, "ControlsEnabled"); });
                Require((bool)Call(journey, "Open"), "native menu remains usable after actual downstream item processing");
                h.Native(Vector2.Zero); h.Native(Vector2.Zero);
                Console.WriteLine("PASS Journey copy sources: native mouse ownership, ordinary inventory discard after release, real Shift/GetItem acquisition and native quick-stack conservation without unrelated inventory capture.");
            }
            finally { p.inventory[10] = savedCopy; Main.chest[0] = previousChest; }
        }


        private static void Coexist(object context, Frames h, ProbeGraphics graphics, object journey, IDictionary categories, string output)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), processing = Get(context, "Processing"), information = Get(context, "Information");
            var p = Main.LocalPlayer;
            // Preserve the exact item produced above while an existing focused
            // tool fixture arranges its own inventory. This is the same object,
            // not a newly granted/cloned stand-in for the copy/save evidence.
            Item copied = p.inventory[10]; p.inventory[10] = new Item();
            try
            {
                Call(journey, "CloseMenu"); Main.playerInventory = false; p.mouseInterface = p.lastMouseInterface = false;
                // Prepare's original startup already built and permanently
                // locked TileObjectData. Reuse it for the real pot/rod scene;
                // Initialize is not a per-scenario reset API.
                NativeFishingBorrowChecks.Run(context);
                Require((bool)Get(journey, "IsActive"), "actual rod return/recast and unrelated item discard continue through an active closed-window Journey session");
                foreach (var item in p.inventory) item.TurnToAir();
                p.position = new Vector2(640, 640); p.selectedItemState.Select(0); p.selectedItemState.Update(); p.itemAnimation = p.itemTime = 0;
                p.inventory[14].SetDefaults(424); p.inventory[14].stack = 20; NativeExtractionChecks.Machine(42, 40, 219);
                Call(processing, "Set", 1, true); NativeQuickItemChecks.Until(() => { Call(processing, "Poll"); return (bool)Call(processing, "Value", 1); });
                int products = Main.item.Count(item => item.active);
                for (int i = 0; i < 100 && p.inventory[14].stack == 20; i++) NativeExtractionChecks.Frame(context, input);
                Require(p.inventory[14].stack < 20 && Main.item.Count(item => item.active) > products, "closed native window allows a real new extraction and its original product outlet");
                Require((bool)Call(journey, "Open"), "native menu can open during a real processing animation");
                Vector2 hover = Center((UIElement)categories[3]);
                h.Native(hover); h.Native(hover);
                int remaining = p.inventory[14].stack;
                Require(!(bool)Get(shell, "CanAutomaticProcessingInput") && !(bool)Get(shell, "CanStartJourneyItemOperation"), "actual native menu hover denies only conflicting new input/inventory work");
                NativeQuickItemChecks.Until(() => { Call(information, "PollPreferences"); return (bool)Get(information, "CanConfigure"); });
                bool wizard = NPC.savedWizard; NPC.savedWizard = true; p.luck = .25f; p.torchLuck = 1;
                Call(information, "SetEnabled", JueMingR.Platform.Information.InformationKind.Luck, true);
                for (int i = 0; i < 45; i++) NativeExtractionChecks.Frame(context, input);
                Call(information, "PrepareHud");
                Require(p.inventory[14].stack == remaining && p.selectedItem == 0 && !(bool)Get(Get(processing, "Extraction"), "Active"),
                    "native menu occupation stops new extraction but existing original selection/receipt responsibility finishes");
                Require((bool)Get(Get(information, "Hud"), "Visible") && ((string)Call(information, "Text", JueMingR.Platform.Information.InformationKind.Luck)).Contains("+0.25"),
                    "independent real information observation/HUD stays populated while native menu owns input");
                graphics.Image(Path.Combine(output, "journey-coexist-information.png"), () => Call(Get(information, "Hud"), "Draw", Main.spriteBatch, false), Main.UIScaleMatrix);
                Call(information, "SetEnabled", JueMingR.Platform.Information.InformationKind.Luck, false); NPC.savedWizard = wizard;
                Call(journey, "CloseMenu"); Main.playerInventory = false; p.mouseInterface = p.lastMouseInterface = false;
                for (int i = 0; i < 110 && p.inventory[14].stack == remaining; i++) NativeExtractionChecks.Frame(context, input);
                Require(p.inventory[14].stack < remaining, "closing native menu resumes actual extraction without resetting its feature switch");
                Call(processing, "Set", 1, false); NativeQuickItemChecks.Until(() => { Call(processing, "Poll"); return (bool)Call(processing, "Controls", 1); });
                for (int i = 0; i < 35; i++) NativeExtractionChecks.Frame(context, input);
                Main.gamePaused = true; NativeQuickItemChecks.Sample(input, new Keys[0]); NativeQuickItemChecks.BeginWorldStep();
                Require(!(bool)Get(input, "CanRunAutomaticActions"), "a genuinely paused world remains distinct from Journey activation/window ownership"); Main.gamePaused = false;
                Require(p.selectedItem == 0 && !(bool)Get(Get(processing, "Extraction"), "Active"), "final native processing selection and input are returned");
                Console.WriteLine("PASS Journey coexist: native fishing loan/return/recast, unrelated discard, extraction product/receipt/return, input-owned pause and resume, independent HUD and true pause boundary.");
            }
            finally { p.inventory[10] = copied; Main.gamePaused = false; Main.mouseItem.TurnToAir(); }
        }

        private static void SetBindingThroughPopup(object context, Frames h, HotkeyBindings bindings)
        {
            object shell = Get(context, "Shell"), state = Get(shell, "State"), layout = Get(state, "Layout"), popup = Get(shell, "HotkeyPopup");
            var icon = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => (string)GetOptional(e, "HotkeyTarget") == "journey.toggle");
            Vector2 point = h.ShellPoint(icon); NativeToolsUiChecks.Click(context, point); NativeToolsUiChecks.Click(context, point);
            Require((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == "journey.toggle", "standard journey keyboard slot opens the actual shared recorder");
            NativeToolsUiChecks.PopupClick(context, popup, "Record");
            h.Key(Keys.LeftControl); h.Key(Keys.LeftControl, Keys.F13); h.Key();
            NativeQuickItemChecks.Until(() => { h.Key(); return !bindings.Busy; });
            Require(bindings.CompletionSucceeded && bindings.Get("journey.toggle") != null && bindings.Get("journey.toggle").MainKey == (int)Keys.F13,
                "real shared recorder reliably saves only the explicit journey binding");
            Call(popup, "Close"); h.Key();
        }
        private static IEnumerable<UIElement> Walk(UIElement root)
        { yield return root; foreach (var child in root.Children) foreach (var element in Walk(child)) yield return element; }
        private static Vector2 Center(UIElement element)
        { var rect = element.GetDimensions(); return new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2); }
        private static bool RejectCreativeToggle() { return false; }
        private sealed class Frames
        {
            private readonly object context, shell, input, state;
            private readonly ProbeGraphics graphics;
            private double milliseconds;
            private bool previousLeft, previousRight;
            internal Frames(object context, ProbeGraphics graphics)
            { this.context = context; this.graphics = graphics; shell = Get(context, "Shell"); input = Get(context, "Input"); state = Get(shell, "State"); }
            internal void Ui() { NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false); }
            internal void Key(params Keys[] keys) { NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false, keys); }
            internal Vector2 ShellPoint(object element)
            {
                object layout = Get(state, "Layout"), rect = Get(element, "Rect"), view = Get(layout, "Viewport");
                Call(state, "ScrollTo", Math.Max(0, (float)Get(rect, "Bottom") - (float)Get(view, "Height") + 4));
                return new Vector2((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2,
                    (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + (float)Get(rect, "Height") / 2) * Main.UIScale;
            }
            internal void ClickShell(string command)
            {
                var layout = Get(state, "Layout"); var element = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == command);
                NativeToolsUiChecks.Click(context, ShellPoint(element));
            }
            internal void HoverShell(string command)
            {
                var layout = Get(state, "Layout"); var element = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == command);
                NativeToolsUiChecks.UiFrame(context, ShellPoint(element), false);
            }
            internal void ClickNative(UIElement element)
            { Vector2 point = Center(element) * Main.UIScale; Native(point); Native(point, left: true); Native(point); Native(point); }
            internal void Native(Vector2 point, bool left = false, bool right = false, bool render = true, Keys[] keys = null)
            {
                milliseconds += 16.6666667;
                Main.gameTimeCache = new GameTime(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.FromMilliseconds(16.6666667));
                Main.LocalPlayer.lastMouseInterface = Main.LocalPlayer.mouseInterface; Main.LocalPlayer.mouseInterface = false;
                Main.CurrentInputTextTakerOverride = null; PlayerInput.WritingText = false;
                Call(input, "BeginUpdate"); Call(shell, "BeforeInput");
                // Native UI.Update precedes DoUpdate_HandleInput in .8 and sees
                // the previously admitted mapping. Draw then sees the fresh one.
                Main.CreativeMenu.Update(Main.gameTimeCache);
                PlayerInput.MouseInfo = new MouseState((int)point.X, (int)point.Y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Main.mouseX = (int)point.X; Main.mouseY = (int)point.Y;
                Main.cursorOverride = -1;
                Main.mouseLeft = left; Main.mouseRight = right; Main.mouseLeftRelease = !previousLeft; Main.mouseRightRelease = !previousRight;
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Current.MouseRight = right; PlayerInput.Triggers.Update();
                Call(input, "AfterMapping"); Main.keyState = new KeyboardState(keys ?? new Keys[0]); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput");
                NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(context, "UpdateShell");
                if (render) graphics.Render(() => Main.CreativeMenu.Draw(Main.spriteBatch), Main.UIScaleMatrix);
                previousLeft = left; previousRight = right;
                Require(!(bool)Get(shell, "Failed"), "native creative input keeps the complete Shell healthy");
            }
        }
    }
}
