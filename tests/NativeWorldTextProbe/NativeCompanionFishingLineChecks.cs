using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFishingLineChecks
    {
        private static bool isolateBiteRandom;
        private static int consumeSeed;
        private static Player skipPullOnce;
        private static int skippedPulls;

        private static bool SkipCosmetic() => false;

        private static void BaitRandomBefore(out UnifiedRandom __state)
        {
            __state = null;
            if (!isolateBiteRandom) return;
            __state = Main.rand;
            Main.rand = new UnifiedRandom(consumeSeed);
        }

        private static Exception BaitRandomAfter(UnifiedRandom __state, Exception __exception)
        {
            if (__state != null) Main.rand = __state;
            return __exception;
        }

        private static bool SkipFirstNativePull(Player __instance, Item __0, ref bool __result)
        {
            if (!ReferenceEquals(__instance, skipPullOnce) || __0.fishingPole <= 0) return true;
            skipPullOnce = null;
            skippedPulls++;
            __result = false;
            return false;
        }

        internal static void Run(object context)
        {
            NativeFishingChecks.InitializeNativeEnvironment();
            Terraria.UI.ItemSorting.SetupWhiteLists();
            var host = Get(context, "Companions");
            var profiles = Get(host, "profiles");
            var input = Get(context, "Input");
            var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks", true);
            var failure = AccessTools.Property(hooks, "FishingUnavailable").GetValue(null);
            Require(failure == null, "AI native fishing hooks installed: " + failure);

            var audit = new Harmony("JueMingR.Tests.CompanionFishingLine");
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                audit.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper), name),
                    prefix: new HarmonyMethod(typeof(NativeCompanionFishingLineChecks), nameof(SkipCosmetic)));
            // Headless runs do not own the camera's transition renderer.
            // Spawn, positions, equipment and subsequent body updates stay native.
            audit.Patch(AccessTools.Method(typeof(Main), nameof(Main.StartCameraTransitionForPlayerTeleport)),
                prefix: new HarmonyMethod(typeof(NativeCompanionFishingLineChecks), nameof(SkipCosmetic)));
            audit.Patch(AccessTools.Method(typeof(Player), "ItemCheck_CheckFishingBobber_ConsumeBait"),
                prefix: new HarmonyMethod(typeof(NativeCompanionFishingLineChecks), nameof(BaitRandomBefore)),
                finalizer: new HarmonyMethod(typeof(NativeCompanionFishingLineChecks), nameof(BaitRandomAfter)));
            audit.Patch(AccessTools.Method(typeof(Player), "ItemCheck_PullFishingBobbers"),
                prefix: new HarmonyMethod(typeof(NativeCompanionFishingLineChecks), nameof(SkipFirstNativePull)) { priority = Priority.First });
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"),
                HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            var previousScene = Main.PlayerSceneMetrics;
            var human = Main.LocalPlayer;
            var previousChest = Main.chest[0];
            bool questFinished = Main.anglerQuestFinished;
            var questNames = Main.anglerWhoFinishedToday.ToArray();
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(profiles, "Ready"); });
                Main.leftWorld = Main.topWorld = 0;
                Main.rightWorld = Main.maxTilesX * 16;
                Main.bottomWorld = Main.maxTilesY * 16;
                Main.spawnTileX = 40;
                Main.spawnTileY = 43;
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                NativeToolExecutionChecks.Reset(context, Get(context, "Tools"), input, ItemID.WoodFishingPole, 0, 0);
                for (int x = 44; x < 74; x++)
                    for (int y = 42; y <= 60; y++)
                    {
                        var tile = Main.tile[x, y];
                        tile.ClearEverything();
                        if (y >= 44 && y < 60) tile.liquid = 255;
                        if (y == 60) NativeToolsChecks.Tile(x, y, TileID.Stone);
                    }
                var a = Create(host, context, "钓鱼原生甲", new FishingOptions(equipment: true, storeMode: 1, filterMode: 2));
                var b = Create(host, context, "钓鱼原生乙", new FishingOptions());
                var first = (Player)Call(host, "GetBody", a.Id);
                var second = (Player)Call(host, "GetBody", b.Id);
                first.Bottom = second.Bottom = human.Bottom;
                first.velocity = second.velocity = Vector2.Zero;
                int humanSlot = human.whoAmI;
                human.inventory[54].SetDefaults(ItemID.TruffleWorm);
                SeedRod(first);
                SeedRod(second);
                first.inventory[10].SetDefaults(ItemID.SonarPotion);
                first.inventory[10].stack = 2;
                first.inventory[11].SetDefaults(ItemID.FishingPotion);
                first.inventory[12].SetDefaults(ItemID.AnglerEarring);
                var earring = first.inventory[12];
                var oldWear = first.armor[4];
                second.Loadouts[1].Armor[3].SetDefaults(ItemID.AnglerEarring);
                var loadoutEarring = second.Loadouts[1].Armor[3];
                first.inventory[14].SetDefaults(ItemID.Bass);
                first.inventory[14].stack = 3;
                first.inventory[15].SetDefaults(ItemID.Bass);
                first.inventory[15].favorited = true;
                var mouse = Main.mouseItem;
                var snapshot = Main.clientPlayer;
                Require((bool)Call(host, "TrySwitchCompanionLoadout", b.Id, 1), "manual actor II command admitted");

                var chest = MakeChest();
                for (int i = 0; i < 8; i++) Step(context, input, first, second);
                MeasureStableLines(context, input, first, second, active: false);
                Require((bool)Call(host, "SetFishing", a.OwnerId, a.Id, true) &&
                    (bool)Call(host, "SetFishing", b.OwnerId, b.Id, true), "two public fishing commands admitted");
                Projectile firstBobber = null, secondBobber = null;
                for (int i = 0; i < 420 && (firstBobber == null || secondBobber == null); i++)
                {
                    Step(context, input, first, second);
                    firstBobber = WetBobber(first);
                    secondBobber = WetBobber(second);
                }
                Require(firstBobber != null && secondBobber != null, "two independent native rod uses each reach real liquid: " +
                    DescribeCast(host, first) + " / " + DescribeCast(host, second));
                Require(second.CurrentLoadoutIndex == 1 && ReferenceEquals(second.armor[3], loadoutEarring) &&
                    human.CurrentLoadoutIndex == 0 && ReferenceEquals(Main.clientPlayer, snapshot), "actor native loadout swap preserves human selection and clone");
                Require(first.armor.Any(item => ReferenceEquals(item, earring)) && first.inventory[10].stack == 1 &&
                    first.FindBuffIndex(BuffID.Sonar) >= 0 && first.inventory[11].type == ItemID.FishingPotion,
                    "active loadout applies the actual loan and QuickBuff alone grants actor's permitted sonar");
                Require(first.GetFishingConditions().BaitItemType == ItemID.Worm && human.GetFishingConditions().BaitItemType == ItemID.TruffleWorm,
                    "actual native bait lookup excludes only the actor's Truffle Worm and retains earlier native bait slots");
                MeasureStableLines(context, input, first, second, active: true);

                Call(host, "SetFishingPage", a.OwnerId, a.Id);
                PopupText.sonarText = 0;
                PopupText.popupText[0].sonar = PopupText.popupText[0].active = true;
                PopupText.popupText[0].lifeTime = 60;
                DefineBite(firstBobber, ItemID.Bass);
                for (int i = 0; i < 8; i++) Step(context, input, first, second);
                Require(firstBobber.ai[0] == 0 && first.inventory[55].stack == 30, "actor's fishing page pauses native pull without cancelling the live line");
                Call(host, "SetFishingPage", Guid.Empty, Guid.Empty);
                int oldKey = (int)firstBobber.key;
                int secondKey = (int)secondBobber.key;
                for (consumeSeed = 0; new UnifiedRandom(consumeSeed).NextFloat() * (1f + first.inventory[55].bait / 6f) >= 1f; consumeSeed++) { }
                isolateBiteRandom = true;
                try
                {
                    skippedPulls = 0;
                    skipPullOnce = first;
                    for (int i = 0; i < 24 && skippedPulls == 0; i++) Step(context, input, first, second);
                    Require(skippedPulls == 1 && firstBobber.ai[0] == 0 && first.inventory[55].stack == 30 &&
                        chest.item.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) == 1,
                        "one definitively skipped native pull leaves the exact live line, bait and chest untouched");
                    for (int i = 0; i < 280 && chest.item.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) != 5; i++)
                        Step(context, input, first, second);
                }
                finally { isolateBiteRandom = false; skipPullOnce = null; }
                Require(chest.item.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) == 5 &&
                    first.inventory[15].stack == 1 && first.inventory[14].IsAir,
                    "actual native pull grants one Bass and nearby native chest stack moves it and old unprotected stock: " +
                    "chest=" + chest.item.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) +
                    " inventory=" + String.Join(",", first.inventory.Select((x, i) => new { x, i })
                        .Where(entry => entry.x.type == ItemID.Bass).Select(entry => entry.i + "=" + entry.x.stack)) +
                    " bait=" + first.inventory[55].stack + " qualified=" + Get(Get(Call(host, "Owned", first), "FishingLine"), "Confirmed") +
                    " storage=" + (GetOptional(Get(Call(host, "Owned", first), "FishingLine"), "Storage") != null) +
                    " chestLocked=" + chest.IsLockedOrInUse() +
                    " oldActive=" + (firstBobber.active && (int)firstBobber.key == oldKey) + " " + DescribeCast(host, first));
                Require(first.inventory[55].stack == 29 && first.inventory[54].type == ItemID.TruffleWorm && first.inventory[54].stack == 2 &&
                    human.inventory[54].type == ItemID.TruffleWorm && second.inventory[55].stack == 30 &&
                    secondBobber.active && (int)secondBobber.key == secondKey, "only first actor's correct native bait stack and line were consumed");
                Require(PopupText.sonarText == 0 && PopupText.popupText[0].active, "original AI projectile retirement preserves the human sonar cursor");
                for (int i = 0; i < 240 && WetBobber(first) == null; i++) Step(context, input, first, second);
                var replacement = WetBobber(first);
                Require(replacement != null && (int)replacement.key != oldKey, "one native replacement cast reaches the retained pond");

                Main.anglerQuestFinished = true;
                Main.anglerWhoFinishedToday.Clear();
                Main.anglerWhoFinishedToday.Add(first.name);
                VerifyQuest(replacement, secondBobber);
                VerifyOtherActorKeepsPulling(context, input, host, first, second, a, replacement, secondBobber);
                VerifyStorageWire(context, input, host, first, second, replacement);
                VerifyManualQuestStock(context, input, host, first, second, a, chest);
                Require(Main.myPlayer == humanSlot && ReferenceEquals(Main.PlayerSceneMetrics, previousScene) &&
                    ReferenceEquals(Main.mouseItem, mouse), "native actor scene and input scopes restore the human's exact state");
                Require((bool)Call(host, "SetFishing", a.OwnerId, a.Id, false), "actor stop command admitted");
                for (int i = 0; i < 160 && (first.armor.Any(item => ReferenceEquals(item, earring)) || OwnedBobber(first) != null); i++)
                    Step(context, input, first, second);
                Require(OwnedBobber(first) == null && ReferenceEquals(first.inventory[12], earring) &&
                    ReferenceEquals(first.armor[4], oldWear), "ordinary stop retires the native bobber and restores exact borrowed references");
                Console.WriteLine("PASS AI fishing: two real wet casts, pause, one native pull/bait debit/fish, chest source, recast, separate quests, sonar and exact loadout return.");
                NativeCompanionFishingOpeningChecks.Run(context, host, input, first, second, a, b);
            }
            finally
            {
                isolateBiteRandom = false;
                skipPullOnce = null;
                Main.chest[0] = previousChest;
                Main.anglerQuestFinished = questFinished;
                Main.anglerWhoFinishedToday.Clear();
                Main.anglerWhoFinishedToday.AddRange(questNames);
                audit.UnpatchAll(audit.Id);
            }
        }

        private static CompanionProfile Create(object host, object context, string name, FishingOptions options)
        {
            Require((bool)Call(host, "Create", name), "create isolated actor " + name);
            CompanionProfile profile = null;
            NativeQuickItemChecks.Until(() =>
            {
                Call(context, "UpdateRuntime");
                profile = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().FirstOrDefault(p => p.Name == name);
                return profile != null && (bool)Call(Get(host, "profiles"), "IsSaved", profile.Id);
            });
            var settings = profile.Behavior;
            settings.AttackEnabled = false;
            settings.BuffEnabled = false;
            settings.Fishing = options;
            Require((bool)Call(host, "SetBehavior", profile.Id, settings), "persist per-profile fishing policy");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Call(Get(host, "profiles"), "IsSaved", profile.Id); });
            Require((bool)Call(host, "Join", profile.Id), "join isolated actor " + name);
            return profile;
        }

        private static string DescribeCast(object host, Player player)
        {
            var a = Call(host, "Owned", player);
            var activity = GetOptional(a, "Fishing");
            var nav = Get(Get(a, "Behavior"), "Navigator");
            var line = Get(a, "FishingLine");
            return Get(a, "Status") + " feet=" + player.Bottom + " velocity=" + player.velocity +
                " requested=" + GetOptional(activity, "RequestedFeet") + " goal=" + GetOptional(activity, "FeetGoal") +
                " supported=" + Get(nav, "Supported") + " hold=" + GetOptional(activity, "Hold") +
                " search=" + GetOptional(activity, "Status") + " issued=" + Get(line, "Issued");
        }

        private static void SeedRod(Player p)
        {
            p.inventory[0].SetDefaults(ItemID.WoodFishingPole);
            p.inventory[54].SetDefaults(ItemID.TruffleWorm);
            p.inventory[54].stack = 2;
            p.inventory[55].SetDefaults(ItemID.Worm);
            p.inventory[55].stack = 30;
            p.inventory[1].SetDefaults(ItemID.EnchantedNightcrawler);
            p.armor[3].SetDefaults(ItemID.HighTestFishingLine);
        }

        private static Chest MakeChest()
        {
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    var tile = Main.tile[36 + x, 41 + y];
                    tile.active(true);
                    tile.type = TileID.Containers;
                    tile.frameX = (short)(18 * x);
                    tile.frameY = (short)(18 * y);
                }
            Main.chest[0] = null;
            Chest.CreateWorldChest(0, 36, 41);
            var chest = Main.chest[0];
            chest.item[0].SetDefaults(ItemID.Bass);
            return chest;
        }

        internal static void DefineBite(Projectile b, int type)
        {
            // Pin the test's input at the native bite boundary. Both the
            // preceding flight and all subsequent item/bait effects are real.
            b.ai[1] = -120;
            b.localAI[1] = type;
            b.localAI[2] = ItemID.Worm;
        }

        private static void VerifyQuest(Projectile first, Projectile second)
        {
            var npc = Main.npc[0];
            int quest = Main.anglerQuest;
            var method = AccessTools.Method(typeof(Projectile), "FishingCheck_ProbeForQuestFish");
            try
            {
                Main.npc[0] = new NPC { active = true, type = NPCID.Angler };
                Main.anglerQuest = 0;
                int Result(Projectile b)
                {
                    var args = new[] { Activator.CreateInstance(method.GetParameters()[0].ParameterType.GetElementType()) };
                    method.Invoke(b, args);
                    return (int)Get(args[0], "questFish");
                }
                Require(Result(first) == -1 && Result(second) == Main.anglerQuestItemNetIDs[0] && Main.anglerQuestFinished,
                    "native quest probe uses each owning actor's completion and preserves the human global");
            }
            finally { Main.npc[0] = npc; Main.anglerQuest = quest; }
        }

        private static void VerifyOtherActorKeepsPulling(object context, object input, object host, Player first, Player second,
            CompanionProfile profile, Projectile paused, Projectile other)
        {
            Call(host, "SetFishingPage", profile.OwnerId, profile.Id);
            try
            {
                // The other actor uses its own already selected loadout.
                // Equip a native anti-break accessory before its real pull.
                second.armor[4].SetDefaults(ItemID.HighTestFishingLine);
                Step(context, input, first, second);
                int firstKey = (int)paused.key;
                int oldOtherKey = (int)other.key;
                int before = second.inventory.Where(i => i.type == ItemID.Bass).Sum(i => i.stack);
                DefineBite(paused, ItemID.Bass);
                DefineBite(other, ItemID.Bass);
                for (int i = 0; i < 140 && second.inventory.Where(item => item.type == ItemID.Bass).Sum(item => item.stack) == before; i++)
                    Step(context, input, first, second);
                Require(paused.active && (int)paused.key == firstKey && paused.ai[0] == 0 &&
                    (!other.active || (int)other.key != oldOtherKey) &&
                    second.inventory.Where(i => i.type == ItemID.Bass).Sum(i => i.stack) == before + 1,
                    "while one fishing page pauses its original line, the other actor really reels and receives its own fish");
            }
            finally { Call(host, "SetFishingPage", Guid.Empty, Guid.Empty); }
        }

        private static void MeasureStableLines(object context, object input, Player first, Player second, bool active)
        {
            const int frames = 48;
            var measure = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
            var allocated = measure == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), measure);
            long bytes = allocated?.Invoke() ?? 0;
            long total = 0, peak = 0;
            int a = active ? (int)WetBobber(first).key : -1;
            int b = active ? (int)WetBobber(second).key : -1;
            for (int i = 0; i < frames; i++)
            {
                long start = Stopwatch.GetTimestamp();
                Step(context, input, first, second);
                long elapsed = Stopwatch.GetTimestamp() - start;
                total += elapsed;
                peak = Math.Max(peak, elapsed);
            }
            string memory = allocated == null ? "unavailable" : (allocated() - bytes).ToString();
            if (active)
                Require(WetBobber(first) is Projectile p && (int)p.key == a &&
                    WetBobber(second) is Projectile q && (int)q.key == b, "stable two-actor sample keeps both original live lines");
            else Require(OwnedBobber(first) == null && OwnedBobber(second) == null &&
                GetOptional(Get(Call(Get(context, "Companions"), "Owned", first), "FishingLine"), "Storage") == null,
                "disabled two-actor sample does not start either line or storage");
            Console.WriteLine("WORKLOAD AI fishing: mode=" + (active ? "stable-wet" : "off") + " actors=2 frames=" + frames + " elapsedMS=" +
                (1000.0 * total / Stopwatch.Frequency).ToString("F3") + " peakMS=" +
                (1000.0 * peak / Stopwatch.Frequency).ToString("F3") + " allocatedBytes=" + memory +
                " (fixture body/runtime/projectile step; no FPS claim)");
        }

        private static void VerifyStorageWire(object context, object input, object host, Player first, Player second, Projectile bobber)
        {
            var actor = Call(host, "Owned", first);
            var state = Get(Get(actor, "FishingLine"), "Storage");
            var sources = (IList)Get(state, "Queued");
            first.inventory[14].SetDefaults(ItemID.Bass);
            DefineBite(bobber, ItemID.Bass);
            for (int i = 0; i < 180 && sources.Count == 0; i++) Step(context, input, first, second);
            Require(sources.Count == 1, "a second actual native grant queues precisely one unprotected main inventory source");
            object source = sources[0];
            int slot = (int)Get(source, "Slot");
            var original = first.inventory[slot];
            var hostType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection", true);
            var wire = new List<byte[]>();
            var snapshot = Main.clientPlayer;
            int mode = Main.netMode;
            var priorFailure = GetOptional(actor, "TerminalFailure");
            var oldSlots = GetOptional(actor, "NetworkSlotIds");
            var primary = new CombatSocket();
            var primaryConnection = Netplay.Connection;
            var primaryBuffer = NetMessage.buffer[256];
            var sections = Main.sectionManager;
            var outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            outlets.Unpatch(AccessTools.Method(typeof(NetMessage), "SendPacket"), HarmonyPatchType.Prefix, outlets.Id);
            using (var connection = (IDisposable)Activator.CreateInstance(hostType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { (Action<byte[]>)wire.Add, (Action)(() => { }) }, null))
            {
                try
                {
                    Set(actor, "Connection", connection);
                    Set(actor, "NetworkSlotIds", new[] { PlayerItemSlotID.Inventory0 + slot });
                    // Original NetMessage only publishes through a connected
                    // primary client. The connected fixture never does real I/O;
                    // the actor relay below must receive the original frames.
                    Netplay.Connection = new RemoteServer { Socket = primary, State = 10 };
                    NetMessage.buffer[256] = new MessageBuffer { whoAmI = 256 };
                    Main.sectionManager = new WorldSections(1, 1);
                    Main.sectionManager.SetAllSectionsLoaded();
                    Main.netMode = 1;
                    Call(host, "AdvanceFishStorage", actor);
                    Require(wire.Select(x => x[2]).SequenceEqual(new byte[] { 5, 85 }) && first.inventoryChestStack[slot] &&
                        primary.Packets.Count == 0,
                        "native quick-stack sends one locked real source frame then 85 specifically on the actor relay: " +
                        "wire=" + String.Join(",", wire.Select(x => x[2])) + " lock=" + first.inventoryChestStack[slot] +
                        " primary=" + primary.Packets.Count + " pending=" + (GetOptional(state, "Pending") != null));
                    Require((ulong)Call(host, "FishingDepositEvidence", actor) == (1UL << slot) &&
                        ReferenceEquals(first.inventory[slot], original), "an unresolved slot remains original marked archive evidence while awaiting receipt");
                    Set(actor, "TerminalFailure", priorFailure);
                    byte[] wrong = Reply(second.whoAmI, slot);
                    Call(connection, "ReceiveRelay", wrong);
                    Call(host, "PumpConnection", actor);
                    Require(ReferenceEquals(first.inventory[slot], original) && first.inventoryChestStack[slot],
                        "a different actor's inventory frame cannot settle the in-flight source");
                    int beforeCount = first.inventory.Where(x => x.type == ItemID.Bass).Sum(x => x.stack);
                    int sourceStack = original.stack;
                    var additional = new Item();
                    additional.SetDefaults(ItemID.Bass);
                    Require(first.GetItem(additional, GetItemSettings.PickupItemFromWorld).IsAir &&
                        original.stack == sourceStack &&
                        first.inventory.Where(x => x.type == ItemID.Bass).Sum(x => x.stack) == beforeCount + 1,
                        "the actual native pickup fills another real slot while the original same-type source awaits confirmation");
                    Call(connection, "ReceiveRelay", Reply(first.whoAmI, slot));
                    Call(host, "PumpConnection", actor);
                    Require(first.inventory[slot].IsAir && !first.inventoryChestStack[slot] &&
                        GetOptional(state, "Pending") == null && ReferenceEquals(Main.clientPlayer, snapshot) &&
                        (ulong)Call(host, "FishingDepositEvidence", actor) == 0,
                        "native case 5 applies the exact actor receipt, unlocks the slot and excludes it from unresolved archive evidence");
                    Console.WriteLine("PASS AI storage relay: real native 5/85, wrong actor isolation, original id5 consumer and unresolved-slot evidence before receipt.");
                }
                finally
                {
                    Main.netMode = mode;
                    Netplay.Connection = primaryConnection;
                    NetMessage.buffer[256] = primaryBuffer;
                    Main.sectionManager = sections;
                    Set(actor, "Connection", null);
                    Set(actor, "NetworkSlotIds", oldSlots);
                    Set(actor, "TerminalFailure", priorFailure);
                }
            }
        }

        private static void VerifyManualQuestStock(object context, object input, object host, Player first, Player second,
            CompanionProfile profile, Chest chest)
        {
            var a = Call(host, "Owned", first);
            var behavior = ((CompanionProfile)Get(a, "Profile")).Behavior;
            var original = behavior.Fishing;
            int previousQuest = Main.anglerQuest;
            try
            {
                Main.anglerQuest = 0;
                Main.anglerWhoFinishedToday.Remove(first.name);
                int type = Main.anglerQuestItemNetIDs[0];
                chest.item[8].SetDefaults(type);
                first.inventory[26].SetDefaults(type);
                behavior.Fishing = behavior.Fishing.Change(3, 2);
                Require((bool)Call(host, "SetBehavior", profile.Id, behavior), "quest-only storage mode is accepted through the same profile settings");
                for (int i = 0; i < 165 && first.inventory[26].type == type; i++) Step(context, input, first, second);
                int Stored() => chest.item.Where(x => x.type == type).Sum(x => x.stack);
                Require(Stored() == 2 && first.inventory[26].IsAir, "today's native quest type stores existing actor stock without a new catch");
                var human = Main.LocalPlayer;
                human.inventory[49].SetDefaults(type);
                var gesture = Enum.Parse(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemGesture", true), "Shift");
                Require((bool)Call(host, "ItemGesture", profile.Id, true, CompanionInventoryArea.Inventory, 49, gesture) && human.inventory[49].IsAir,
                    "actual inventory handoff gives the same quest fish to the actor explicitly");
                for (int i = 0; i < 70; i++) Step(context, input, first, second);
                Require(first.inventory.Count(x => x.type == type) == 1 && Stored() == 2,
                    "the periodic quest scan respects the actual manual handoff while unchanged ordinary actor stock was storable");
            }
            finally
            {
                behavior.Fishing = original;
                Call(host, "SetBehavior", profile.Id, behavior);
                Main.anglerQuest = previousQuest;
            }
        }

        private static byte[] Reply(int player, int slot)
        {
            // Deterministic isolated server receiver input. The live product
            // submit is original, and the client GetData owns the mutation.
            var packet = new byte[12];
            packet[0] = 12;
            packet[2] = 5;
            packet[3] = (byte)player;
            Buffer.BlockCopy(BitConverter.GetBytes((short)(PlayerItemSlotID.Inventory0 + slot)), 0, packet, 4, 2);
            return packet;
        }

        internal static Projectile OwnedBobber(Player p) =>
            Main.projectile.FirstOrDefault(x => x.active && x.bobber && x.owner == p.whoAmI);

        internal static Projectile WetBobber(Player p)
        {
            var bobber = OwnedBobber(p);
            return bobber != null && bobber.wet && bobber.ai[0] == 0 ? bobber : null;
        }

        internal static void Step(object context, object input, Player first, Player second)
        {
            NativeToolExecutionChecks.Sample(context, input, new Vector2(300, 580), false);
            NativeQuickItemChecks.BeginWorldStep();
            Call(context, "UpdateRuntime");
            first.Update(first.whoAmI);
            second.Update(second.whoAmI);
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active) Main.projectile[i].Update(i);
        }
    }
}
