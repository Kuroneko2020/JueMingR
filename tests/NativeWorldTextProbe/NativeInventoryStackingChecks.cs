using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Items;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // GetItem, Add, locks and serialization stay native. Client cases inject
    // source replies; isolated server cases consume real 5/85 and produce 5/32.
    // Both realms run in this process, without a live friend/Steam connection.
    internal static class NativeInventoryStackingChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<byte[]> packets = new List<byte[]>();
        private static readonly List<byte[]> serverPackets = new List<byte[]>();
        private static Action beforeProduct;
        private static void BeforeProduct() { Action action = beforeProduct; beforeProduct = null; action?.Invoke(); }
        private static bool SkipAchievement() { return false; }
        internal static void Run(object context)
        {
            var audit = new Harmony("JueMingR.Tests.InventoryStacking");
            MethodInfo send = typeof(NetMessage).GetMethod("SendData", Flags);
            audit.Patch(send, postfix: new HarmonyMethod(typeof(NativeInventoryStackingChecks), nameof(Capture)));
            // Match the existing native fishing/AFK fixture: the headless
            // process owns no Steam achievement manager. Item use stays native.
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                audit.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper), name),
                    prefix: new HarmonyMethod(typeof(NativeInventoryStackingChecks), nameof(SkipAchievement)));
            var connection = Netplay.Connection; var buffer = NetMessage.buffer[256]; var sections = Main.sectionManager;
            var logger = Player.GetItemLogger;
            object tools = Get(context, "Tools"), input = Get(context, "Input"), items = Get(tools, "Items"), world = Get(items, "World"), storage = Get(items, "Storage");
            var feature = (ItemAutomationFeature)Get(items, "Feature");
            ItemAutomationSettings settings = ((JueMingR.Platform.Settings.PreferenceSnapshot<ItemAutomationSettings>)Get(items, "Preferences")).Value;
            var ownership = (ItemOperationOwnership)Get(items, "Ownership");
            try
            {
                NativeFishingChecks.InitializeNativeEnvironment();
                Terraria.UI.ItemSorting.SetupWhiteLists();
                NativeQuickItemChecks.Until(() => { Call(items, "PollPreferences"); return (bool)Get(Get(items, "Preferences"), "IsLoaded"); });
                var gapFailures = new List<Exception>();
                foreach (string gap in new[] { "growth", "first", "late" })
                {
                    try { ReplyGap(context, tools, input, items, world, storage, feature, ownership, gap); }
                    catch (JueMingR.Testing.BusinessAssertionException error)
                    { Console.WriteLine("FAIL native receive/pickup/update " + gap + ": " + error.Message); gapFailures.Add(error); }
                }
                if (gapFailures.Count != 0) throw new AggregateException("native reply-gap reception", gapFailures);
                foreach (string scenario in new[] { "zero", "partial", "all", "many", "limit", "favorite", "replace", "manual", "foreign", "lease", "disabled", "unknown", "late", "ammo", "server-zero", "server-partial", "server-all", "death", "bag", "full", "void" })
                {
                    Main.netMode = 0; Call(context, "UpdateRuntime");
                    var p = NativeToolExecutionChecks.Reset(context, tools, input, ItemID.CopperPickaxe, 0, 0);
                    Netplay.Connection = new RemoteServer { PendingTermination = true }; NetMessage.buffer[256] = new MessageBuffer();
                    Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Main.netMode = 1; Call(context, "UpdateRuntime");
                    feature.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true));
                    p.IsVoidVaultEnabled = false; foreach (Item item in p.bank4.item) item.TurnToAir();
                    int type = scenario == "ammo" ? ItemID.WoodenArrow : scenario == "bag" ? ItemID.Worm : ItemID.DirtBlock;
                    int source = scenario == "ammo" ? 54 : 12;
                    p.inventory[source].SetDefaults(type); p.inventory[source].stack = scenario == "limit" ? p.inventory[source].maxStack - 1 : 100;
                    int original = p.inventory[source].stack;
                    if (scenario == "partial") { p.inventory[14].SetDefaults(type); p.inventory[14].stack = 20; original += 20; }
                    Call(world, "InvalidateObservation");
                    object[] observation = { null };
                    Require((bool)Call(world, "TryObserveAcquisition", observation), "native source observation is available");
                    var inventory = (ItemInventoryObservation)observation[0];
                    var sources = inventory.Slots.Where(s => s.Slot == source || scenario == "partial" && s.Slot == 14).ToArray();
                    packets.Clear();
                    var result = (ItemOperationResult)Call(storage, "Execute", new StoreItemsRequest(inventory.Session, sources));
                    Require(result.State == ItemOperationState.Executing && p.inventoryChestStack[source], "real client request locks original source");
                    byte[][] requestFrames = packets.ToArray();
                    // A preserved sentinel proves the observer does not clear or
                    // reconfigure vanilla's shared animation logger.
                    Player.GetItemLogger = new Terraria.DataStructures.PlayerGetItemLogger(); Player.GetItemLogger.Start();
                    Player.GetItemLogger.Add(p.bank4.item, 0, 32, 7);
                    if (scenario == "full" || scenario == "void")
                    {
                        foreach (int slot in Enumerable.Range(0, 58).Where(i => i != source && (i < 50 || i >= 54)))
                        { p.inventory[slot].SetDefaults(ItemID.StoneBlock); p.inventory[slot].stack = p.inventory[slot].maxStack; }
                        p.IsVoidVaultEnabled = scenario == "void";
                        var drop = new WorldItem(new Item(type) { stack = 1 }) { whoAmI = 399 };
                        Main.item[drop.whoAmI] = drop;
                        typeof(Player).GetMethod("PickupItem", Flags).Invoke(p, new object[] { drop });
                        int bank = p.bank4.item.Where(i => i.type == type).Sum(i => i.stack);
                        Require((scenario == "void" ? drop.IsAir && bank == 1 : !drop.IsAir && drop.stack == 1 && bank == 0) &&
                            GetOptional(Get(storage, "pending"), "Fragments") == null, "real full-main receipt remains world/void only and creates no main fragment");
                        var receiveOverflow = new MessageBuffer { whoAmI = 256 };
                        NativeChestLocatorChecks.Receive(receiveOverflow, 5, w => { w.Write((byte)p.whoAmI); w.Write((short)(PlayerItemSlotID.Inventory0 + source)); w.Write((short)original); w.Write((byte)0); w.Write((short)type); w.Write((byte)0); });
                        Call(storage, "Update");
                        Require(!ownership.StoreBlocked && p.inventory[source].stack == original && bank + (drop.IsAir ? 0 : drop.stack) == 1,
                            "known result cannot pull a void receipt or world remainder into the original source");
                        Player.GetItemLogger.Stop(); Player.GetItemLogger = logger;
                        Console.WriteLine("PASS I1/I2 native client stacking " + scenario); continue;
                    }
                    int gained = scenario == "many" || scenario == "limit" ? 3 : 1;
                    if (scenario == "bag")
                    {
                        p.inventory[10].SetDefaults(ItemID.CanOfWorms); p.inventory[10].stack = 1;
                        typeof(Terraria.UI.ItemSlot).GetMethod("TryOpenContainer", Flags).Invoke(null, new object[] { p.inventory, 0, 10, p });
                        gained = p.inventory.Where(i => i.type == type).Sum(i => i.stack) - original;
                        Require(gained >= 5 && gained <= 8 && p.inventory[10].IsAir, "real can of worms consumes one bag and grants its native product amount");
                    }
                    else for (int i = 0; i < gained; i++) Pickup(p, type, 1);
                    int fragment = Enumerable.Range(0, 58).First(i => i != source && i != 14 && p.inventory[i].type == type);
                    Item newItem = p.inventory[fragment];
                    var initialOpportunities = (IDictionary)Get(feature, "acquisitions");
                    object initialOpportunity = initialOpportunities[new ItemIdentity(type, 0)];
                    ulong started = initialOpportunity == null ? 0 : (ulong)Get(initialOpportunity, "Started");
                    Require(newItem.stack == gained && p.inventory[source].stack == original - (scenario == "partial" ? 20 : 0), "real GetItem picks a separate stack without touching locked sources");
                    if (scenario != "bag") Require(Player.GetItemLogger.Entries[0].Stack == 7 && ReferenceEquals(Player.GetItemLogger.Entries[0].TargetArray, p.bank4.item), "native logger entries survive the observer");
                    if (scenario == "favorite") newItem.favorited = true;
                    if (scenario == "replace") p.inventory[fragment] = newItem.Clone();
                    if (scenario == "manual")
                    {
                        var guards = items.GetType().Assembly.GetType("JueMingR.TerrariaHost.Items.ItemPendingGuards");
                        guards.GetMethod("NotifyManualMutation", Flags).Invoke(null, new object[] { p.inventory, fragment });
                    }
                    if (scenario == "foreign") { var returned = new Item(type) { stack = 2 }; p.GetItem(returned, new GetItemSettings(NoText: true, NoSound: true)); }
                    if (scenario == "lease") Require(ownership.TryBeginUse(inventory.Session, fragment, 919), "foreign use takes the actual fragment slot");
                    if (scenario == "disabled") feature.Configure(ItemAutomationSettings.Default);
                    if (scenario == "death") { p.dead = true; Call(items, "DiscardUnsubmittedAcquisitions"); p.dead = false; }
                    if (scenario == "late") { Set(storage, "Tick", (ulong)Get(Get(storage, "pending"), "Started") + 600); Call(storage, "Update"); Require(ownership.StoreResult.State == ItemOperationState.TimedOut && newItem.stack == gained, "unknown deadline does not consolidate"); }
                    var receive = new MessageBuffer { whoAmI = 256 };
                    void Reply(int slot, int id, int quantity)
                    {
                        NativeChestLocatorChecks.Receive(receive, 5, w => { w.Write((byte)p.whoAmI); w.Write((short)(PlayerItemSlotID.Inventory0 + slot)); w.Write((short)quantity); w.Write((byte)0); w.Write((short)id); w.Write((byte)0); });
                        Call(storage, "Update");
                    }
                    packets.Clear();
                    int remaining = scenario == "all" || scenario == "server-all" ? 0 : scenario == "partial" || scenario == "server-partial" ? 60 : original;
                    if (scenario.StartsWith("server-", StringComparison.Ordinal))
                    {
                        int moved = original - remaining;
                        var authority = Serve(p, requestFrames, type, moved);
                        Require(authority.Item1 == moved && authority.Item2 == remaining, "real server capacity handling computes exact moved and remaining quantities");
                        foreach (byte[] frame in authority.Item3.Where(b => b[2] == 5))
                        { ReceiveFrame(receive, frame); Call(storage, "Update"); }
                    }
                    else if (scenario == "partial")
                    {
                        Reply(source, type, remaining);
                        Require(p.inventory[fragment].stack == gained && ownership.StoreBlocked, "first authoritative source cannot complete a two-source batch");
                        Reply(14, 0, 0);
                    }
                    else Reply(source, scenario == "unknown" ? ItemID.Wood : remaining == 0 ? 0 : type, remaining);
                    bool skipped = new[] { "favorite", "replace", "manual", "foreign", "lease", "unknown", "death" }.Contains(scenario);
                    if (scenario == "unknown") Require(ownership.StoreResult.State == ItemOperationState.Unconfirmed && ownership.StoreBlocked, "unknown identity retains exact ownership and no consolidation");
                    else
                    {
                        Require(!ownership.StoreBlocked && ownership.StoreResult.ConfirmedQuantity == original - remaining, "sealed server transfer result excludes later acquisitions");
                        if (scenario == "all" || scenario == "server-all") Require(p.inventory[source].IsAir && p.inventory[fragment].stack == gained, "all stored old items leave the original slot empty");
                        else if (skipped) Require(p.inventory[source].stack == remaining && !p.inventory[fragment].IsAir, "taken over or protected fragment remains where its owner left it");
                        else
                        {
                            int target = Math.Min(p.inventory[source].maxStack, remaining + gained);
                            Require(p.inventory[source].stack == target && p.inventory[fragment].stack == remaining + gained - target, "known result consolidates only bounded compatible quantity");
                            Slot(p, source); Slot(p, fragment);
                            var opportunities = (IDictionary)Get(feature, "acquisitions");
                            if (scenario == "disabled" || scenario == "late") Require(opportunities.Count == 0, "configuration cancellation or age expiry is never resurrected by consolidation");
                            else
                            {
                                object entry = opportunities[new ItemIdentity(type, 0)];
                                object[] mapped = { null }; Require((bool)Call(world, "TryObserveAcquisition", mapped), "mapped post-transfer inventory available");
                                ItemSlotObservation current = ((ItemInventoryObservation)mapped[0]).Slots.First(s => s.Slot == source);
                                Require(entry != null && (bool)Call(entry, "Contains", current), "real acquisition remains eligible at its exact surviving destination");
                                Require((ulong)Get(entry, "Started") == started, "consolidation preserves the first acquisition age");
                            }
                        }
                        int total = p.inventory.Where(x => x.type == type).Sum(x => x.stack);
                        Require(total + ownership.StoreResult.ConfirmedQuantity == original + gained + (scenario == "foreign" ? 2 : 0), "client inventory and sealed server movement conserve all accepted quantities");
                        int beforePackets = packets.Count; Call(storage, "Update");
                        Require(packets.Count == beforePackets, "completed batch cannot consolidate or explicitly sync twice");
                    }
                    if (scenario == "lease") ownership.EndUse(inventory.Session, 919);
                    Player.GetItemLogger.Stop(); Player.GetItemLogger = logger;
                    Console.WriteLine("PASS I1/I2 native client stacking " + scenario);
                }
                Fishing(context, items, storage, world, feature);
                AfkAcrossStorage(context, items, storage, world, feature);
                // The new Add observer has no reception/list work without one
                // pending batch, including after settings are switched off.
                feature.Configure(ItemAutomationSettings.Default);
                Main.netMode = 0; Call(context, "UpdateRuntime");
                object hooks = items.GetType().Assembly.GetType("JueMingR.TerrariaHost.Items.ItemSourceHooks");
                for (int i = 0; i < 120; i++) Player.GetItemLogger.Add(Main.LocalPlayer.inventory, 12, 0, 1);
                Require(((Type)hooks).GetField("reception", Flags).GetValue(null) == null && GetOptional(storage, "pending") == null, "idle/disabled Add retains no reception or batch");
            }
            finally
            {
                Player.GetItemLogger = logger; feature.Configure(settings); Main.netMode = 0; Netplay.Connection = connection; NetMessage.buffer[256] = buffer; Main.sectionManager = sections;
                foreach (MethodBase method in audit.GetPatchedMethods().ToArray()) audit.Unpatch(method, HarmonyPatchType.All, audit.Id);
                Call(context, "UpdateRuntime"); packets.Clear();
            }
        }
        private static void Pickup(Player player, int type, int quantity)
        {
            var item = new Item(); item.SetDefaults(type); item.stack = quantity;
            var drop = new WorldItem(item) { whoAmI = 399 };
            Main.item[drop.whoAmI] = drop;
            typeof(Player).GetMethod("PickupItem", Flags).Invoke(player, new object[] { drop });
            Require(drop.IsAir, "real PickupItem consumes the accepted world source");
        }
        private static void ReplyGap(object context, object tools, object input, object items, object world, object storage,
            ItemAutomationFeature feature, ItemOperationOwnership ownership, string scenario)
        {
            Main.netMode = 0; Call(context, "UpdateRuntime");
            Player p = NativeToolExecutionChecks.Reset(context, tools, input, ItemID.CopperPickaxe, 0, 0);
            Netplay.Connection = new RemoteServer { PendingTermination = true }; NetMessage.buffer[256] = new MessageBuffer();
            Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Main.netMode = 1; Call(context, "UpdateRuntime");
            feature.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true));
            p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 100; Call(world, "InvalidateObservation");
            object[] observed = { null }; Require((bool)Call(world, "TryObserveAcquisition", observed), "gap source observation");
            var inventory = (ItemInventoryObservation)observed[0];
            var request = new StoreItemsRequest(inventory.Session, inventory.Slots.Where(s => s.Slot == 12).ToArray());
            Require(((ItemOperationResult)Call(storage, "Execute", request)).State == ItemOperationState.Executing, "gap starts actual store");
            bool existing = scenario != "first";
            if (existing) Pickup(p, ItemID.DirtBlock, 1);
            var acquisitions = (IDictionary)Get(feature, "acquisitions"); var identity = new ItemIdentity(ItemID.DirtBlock, 0);
            object chance = acquisitions[identity]; ulong started = chance == null ? 0 : (ulong)Get(chance, "Started");
            if (existing)
            {
                NativeToolsChecks.Frame(context, input);
                Require((ulong)Get(items, "Tick") > started && ownership.StoreBlocked && p.inventoryChestStack[12],
                    "a real pending native frame separates the acquisition ages before Receive(5)");
            }
            if (scenario == "late")
            {
                Set(storage, "Tick", (ulong)Get(Get(storage, "pending"), "Started") + 600); Call(storage, "Update");
                Require(ownership.StoreResult.State == ItemOperationState.TimedOut && ownership.StoreBlocked, "timeout retains this original store source");
            }
            var receiver = new MessageBuffer { whoAmI = 256 };
            NativeChestLocatorChecks.Receive(receiver, 5, w => { w.Write((byte)p.whoAmI); w.Write((short)(PlayerItemSlotID.Inventory0 + 12)); w.Write((short)60); w.Write((byte)0); w.Write((short)ItemID.DirtBlock); w.Write((byte)0); });
            Require(!p.inventoryChestStack[12] && ownership.StoreBlocked && p.inventory[12].stack == 60,
                "real Receive(5) unlocks native slot before this pending Store is settled");
            Pickup(p, ItemID.DirtBlock, 2); // No Storage.Update or Runtime update between reply and pickup.
            int fragment = Enumerable.Range(0, 58).First(i => i != 12 && p.inventory[i].type == ItemID.DirtBlock);
            int gained = existing ? 3 : 2;
            Require(p.inventory[12].stack == 60 && p.inventory[fragment].stack == gained, "original Store protection keeps real late pickup separate");
            if (chance == null) { chance = acquisitions[identity]; started = (ulong)Get(chance, "Started"); }
            else Require(ReferenceEquals(chance, acquisitions[identity]), "new growth retains the same live acquisition opportunity");
            Call(storage, "Update");
            Require(!ownership.StoreBlocked && ownership.StoreResult.ConfirmedQuantity == 40 && p.inventory[12].stack == 60 + gained && p.inventory[fragment].IsAir,
                "receive/pickup/update merges confirmed new quantities after sealing only the original forty stored items");
            object[] mapped = { null }; Require((bool)Call(world, "TryObserveAcquisition", mapped), "gap mapped observation");
            var destination = ((ItemInventoryObservation)mapped[0]).Slots.First(s => s.Slot == 12);
            if (scenario == "late") Require(acquisitions.Count == 0, "late physical cleanup does not renew an expired opportunity");
            else Require(ReferenceEquals(chance, acquisitions[identity]) && (ulong)Get(chance, "Started") == started && (bool)Call(chance, "Contains", destination),
                "reply gap mapping preserves opportunity age and its exact surviving member");
            Require(p.inventory.Where(i => i.type == ItemID.DirtBlock).Sum(i => i.stack) + ownership.StoreResult.ConfirmedQuantity == 100 + gained,
                "reply gap server movement and inventory conserve all old and newly accepted units");
            Console.WriteLine("PASS native receive/pickup/update " + scenario + " received=60 new=" + gained + " stored=40");
        }
        private static void Fishing(object context, object items, object storage, object world, ItemAutomationFeature feature)
        {
            object fishing = Get(context, "Fishing"), tools = Get(context, "Tools"), input = Get(context, "Input");
            var settings = (FishingSettings)Get(fishing, "Settings"); FishingOptions saved = settings.Value;
            var audit = new Harmony("JueMingR.Tests.StackingFishingProduct");
            MethodInfo give = typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer", Flags);
            audit.Patch(give, prefix: new HarmonyMethod(typeof(NativeInventoryStackingChecks), nameof(BeforeProduct)) { priority = Priority.First });
            try
            {
                Main.netMode = 0; Call(context, "UpdateRuntime");
                var p = (Player)typeof(NativeFishingStorageChecks).GetMethod("Reset", Flags).Invoke(null, new[] { context, fishing, tools, input });
                Main.netMode = 1; Call(context, "UpdateRuntime"); feature.Configure(ItemAutomationSettings.Default);
                NativeFishingChecks.Save(fishing, new FishingOptions(auto: true, storeMode: 1));
                Projectile bobber = NativeFishingChecks.CastToWaiting(context, input);
                p.inventory[12].SetDefaults(ItemID.Bass); p.inventory[12].stack = 100;
                byte[][] requests = null;
                beforeProduct = () =>
                {
                    Call(world, "InvalidateObservation"); object[] observed = { null };
                    Require((bool)Call(world, "TryObserveAcquisition", observed), "pre-product current source observation available");
                    var inventory = (ItemInventoryObservation)observed[0]; packets.Clear();
                    var result = (ItemOperationResult)Call(storage, "Execute", new StoreItemsRequest(inventory.Session, inventory.Slots.Where(s => s.Slot == 12)));
                    Require(result.State == ItemOperationState.Executing && p.inventoryChestStack[12], "real independent storage begins before actual returning fishing product");
                    requests = packets.ToArray();
                };
                bobber.ai[1] = -120; bobber.localAI[1] = ItemID.Bass; bobber.localAI[2] = ItemID.Worm;
                for (int i = 0; i < 240 && p.inventory.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) == 100; i++)
                    NativeFishingChecks.Step(context, input, new Microsoft.Xna.Framework.Vector2(850, 718), false, 0);
                Require(requests != null && p.inventory.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) == 101, "real automatic consumed pull grants exactly one Bass through the native product/GetItem chain");
                int fragment = Enumerable.Range(0, 58).Single(i => i != 12 && p.inventory[i].type == ItemID.Bass);
                var opportunities = (IDictionary)Get(feature, "fishingAcquisitions");
                object entry = opportunities[new ItemIdentity(ItemID.Bass, 0)];
                Require(entry != null, "returning native fishing product creates its existing dedicated opportunity");
                ulong started = (ulong)Get(entry, "Started"); long token = (long)Get(feature, "fishingToken");
                var authority = Serve(p, requests, ItemID.Bass, 0); packets.Clear(); var receiver = new MessageBuffer { whoAmI = 256 };
                foreach (byte[] frame in authority.Item3.Where(b => b[2] == 5)) { ReceiveFrame(receiver, frame); Call(storage, "Update"); }
                Require(p.inventory[12].stack == 101 && p.inventory[fragment].IsAir, "real direct fishing product consolidates after authoritative zero result");
                object[] current = { null }; Require((bool)Call(world, "TryObserveAcquisition", current), "post-fishing inventory available");
                Require((bool)Call(entry, "Contains", ((ItemInventoryObservation)current[0]).Slots.First(s => s.Slot == 12)) &&
                    (ulong)Get(entry, "Started") == started && (long)Get(feature, "fishingToken") == token,
                    "fishing opportunity maps its exact member while retaining its first age and owner token");
                Require(((IDictionary)Get(feature, "acquisitions")).Count == 0, "direct fishing does not manufacture ordinary acquisition permission");
                Slot(p, 12); Slot(p, fragment);
                Console.WriteLine("PASS I1/I2 actual auto-pull/product/GetItem plus server85/source5 and dedicated opportunity mapping.");
            }
            finally { beforeProduct = null; audit.Unpatch(give, HarmonyPatchType.All, audit.Id); NativeFishingChecks.Save(fishing, saved); }
        }
        private static void Slot(Player p, int slot)
        {
            var packet = packets.Single(b => b[2] == 5 && BitConverter.ToInt16(b, 4) == PlayerItemSlotID.Inventory0 + slot);
            Item actual = p.inventory[slot];
            Require(BitConverter.ToInt16(packet, 6) == actual.stack && packet[8] == actual.prefix && BitConverter.ToInt16(packet, 9) == actual.type && ((packet[11] & 1) != 0) == actual.favorited,
                "message5 serializes exact current physical slot including Air/prefix/favorite");
        }
        private static void AfkAcrossStorage(object context, object items, object storage, object world, ItemAutomationFeature feature)
        {
            object afk = Get(context, "Afk"), tools = Get(context, "Tools"), input = Get(context, "Input");
            bool enabled = (bool)Get(afk, "ModeEnabled"); int gap = (int)Get(afk, "GapMilliseconds");
            try
            {
                Call(afk, "SetModeEnabled", false); Main.netMode = 0; Call(context, "UpdateRuntime");
                Player p = NativeToolExecutionChecks.Reset(context, tools, input, ItemID.CopperPickaxe, 0, 0);
                Main.netMode = 1; Call(context, "UpdateRuntime");
                Call(Get(context, "Shell"), "CloseAndSubmitPosition"); p.releaseUseItem = true; p.delayUseItem = false; Main.blockMouse = false;
                NativeCombatCadenceChecks.Step(context, false, false, 0);
                Call(afk, "SetGapMilliseconds", 0); Call(afk, "SetModeEnabled", true);
                NativeQuickItemChecks.Until(() => { Call(afk, "Poll"); return (bool)Get(afk, "ControlsEnabled"); });
                NativeCombatCadenceChecks.Step(context, true, false, 0);
                Require((bool)Get(afk, "Running") && (int)Get(afk, "Starts") > 0, "one accepted physical gesture starts the actual non-rod AFK operation");
                feature.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true));
                p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 100;
                Call(world, "InvalidateObservation"); object[] observed = { null };
                Require((bool)Call(world, "TryObserveAcquisition", observed), "AFK-running storage observes unrelated current material");
                var inventory = (ItemInventoryObservation)observed[0]; packets.Clear();
                var result = (ItemOperationResult)Call(storage, "Execute", new StoreItemsRequest(inventory.Session, inventory.Slots.Where(s => s.Slot == 12)));
                Require(result.State == ItemOperationState.Executing && p.inventoryChestStack[12], "unrelated real material storage locks its source while AFK owns the held item");
                byte[][] requests = packets.ToArray(); int starts = (int)Get(afk, "Starts");
                for (int i = 0; i < 40; i++) NativeCombatCadenceChecks.Step(context, false, false, 0);
                Require((bool)Get(afk, "Running") && (int)Get(afk, "Starts") == starts && !p.controlUseItem,
                    "real network wait crosses AFK Sample/Sync: intent remains and no new owned output starts");
                Pickup(p, ItemID.DirtBlock, 1);
                int fragment = Enumerable.Range(0, 58).Single(i => i != 12 && p.inventory[i].type == ItemID.DirtBlock);
                var authority = Serve(p, requests, ItemID.DirtBlock, 0); packets.Clear(); var receiver = new MessageBuffer { whoAmI = 256 };
                foreach (byte[] frame in authority.Item3.Where(b => b[2] == 5)) { ReceiveFrame(receiver, frame); Call(storage, "Update"); }
                Require(!p.inventoryChestStack[12] && p.inventory[12].stack == 101 && p.inventory[fragment].IsAir &&
                    (bool)Get(afk, "Running"), "real source5 releases the batch and consolidates while the same AFK run survives");
                for (int i = 0; i < 90 && (int)Get(afk, "Starts") == starts; i++) NativeCombatCadenceChecks.Step(context, false, false, 0);
                Require((int)Get(afk, "Starts") > starts && (bool)Get(afk, "Running"), "AFK resumes an actual owned operation after unlock without another physical click");
                Console.WriteLine("PASS I1/B2 actual AFK gesture/use + unrelated locked storage + Pickup + server85/source5/consolidation + no-click resume.");
            }
            finally { Call(afk, "SetModeEnabled", false); Call(afk, "SetGapMilliseconds", gap); Call(afk, "SetModeEnabled", enabled); }
        }
        private static void Capture(int __0)
        {
            if (Main.netMode != 1 && Main.netMode != 2 || __0 != 5 && __0 != 85 && __0 != 32) return;
            byte[] buffer = NetMessage.buffer[256].writeBuffer;
            int length = BitConverter.ToUInt16(buffer, 0);
            (Main.netMode == 2 ? serverPackets : packets).Add(buffer.Take(length).ToArray());
        }
        private static void ReceiveFrame(MessageBuffer receiver, byte[] frame)
        {
            Array.Copy(frame, 2, receiver.readBuffer, 0, frame.Length - 2); receiver.ResetReader();
            int kind; receiver.GetData(0, frame.Length - 2, out kind);
            Require(kind == frame[2], "real GetData consumes a serialized frame");
        }
        private static Tuple<int, int, byte[][]> Serve(Player client, byte[][] requests, int type, int capacity)
        {
            var players = Main.player; var chests = Main.chest; var clientShadow = Main.clientPlayer;
            MessageBuffer[] buffers = NetMessage.buffer;
            FieldInfo coordinates = AccessTools.Field(typeof(Chest), "_chestsByCoords"); object previousCoordinates = coordinates.GetValue(null);
            var remoteClients = (RemoteClient[])Netplay.Clients.Clone();
            int mode = Main.netMode, me = Main.myPlayer; bool dedicated = Main.dedServ;
            try
            {
                Main.player = Enumerable.Range(0, players.Length).Select(i => new Player { whoAmI = i, chest = -1 }).ToArray();
                Player authority = Main.player[client.whoAmI]; authority.active = true; authority.position = client.position;
                Main.clientPlayer = new Player(); Main.chest = new Chest[8000];
                coordinates.SetValue(null, new Dictionary<Microsoft.Xna.Framework.Point, Chest>());
                var chest = Chest.CreateWorldChest(0, 44, 40);
                for (int i = 0; i < chest.item.Length; i++) { chest.item[i].SetDefaults(ItemID.StoneBlock); chest.item[i].stack = chest.item[i].maxStack; }
                chest.item[0].SetDefaults(type); chest.item[0].stack = chest.item[0].maxStack - capacity;
                int before = chest.item[0].stack;
                for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                // Normal server dispatch owns the same per-client buffer that
                // GetData receives. Keep that invariant in this isolated realm.
                // Server SendData examines every buffer's broadcast flag before
                // checking disconnected sockets. A client-only fixture leaves
                // those slots null; the normal server initializer does not.
                NetMessage.buffer = Enumerable.Range(0, buffers.Length).Select(i => new MessageBuffer { whoAmI = i }).ToArray();
                Main.netMode = 2; Main.dedServ = true; Main.myPlayer = 255; serverPackets.Clear();
                var receiver = NetMessage.buffer[client.whoAmI];
                foreach (byte[] frame in requests)
                {
                    if (frame[2] == 85) serverPackets.Clear();
                    Console.WriteLine("I1 server dispatch native kind=" + frame[2] + " sender=" + client.whoAmI + " capacity=" + capacity);
                    ReceiveFrame(receiver, frame);
                }
                int moved = chest.item[0].stack - before;
                int remaining = authority.inventory.Where(i => i.type == type).Sum(i => i.stack);
                Require(moved == capacity && serverPackets.Any(b => b[2] == 5), "native85 actually handles server chest capacity and serializes authoritative source5");
                return Tuple.Create(moved, remaining, serverPackets.ToArray());
            }
            finally
            {
                Main.netMode = mode; Main.dedServ = dedicated; Main.myPlayer = me; Main.player = players; Main.chest = chests; Main.clientPlayer = clientShadow;
                NetMessage.buffer = buffers;
                coordinates.SetValue(null, previousCoordinates);
                Array.Copy(remoteClients, Netplay.Clients, remoteClients.Length); serverPackets.Clear();
            }
        }
    }
}
