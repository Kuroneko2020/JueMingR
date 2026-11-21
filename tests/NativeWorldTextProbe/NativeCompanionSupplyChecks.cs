using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionSupplyChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static Player faultPatient;
        private static bool tailFailure;
        private static int captureFailures;
        private static Vector2? stalePosition;

        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            var spawn = AccessTools.Method(typeof(Player), nameof(Player.Spawn));
            var outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            outlets.Unpatch(spawn, HarmonyPatchType.Prefix, outlets.Id);
            var harness = new Harmony("JueMingR.Tests.CompanionSupplies");
            foreach (string name in new[] { "HandleMining", "HandleRunning" })
                harness.Patch(AccessTools.Method(typeof(AchievementsHelper), name),
                    prefix: new HarmonyMethod(typeof(NativeCompanionSupplyChecks), nameof(SkipHeadlessOutlet)));
            harness.Patch(AccessTools.Method(typeof(AchievementsHelper), nameof(AchievementsHelper.HandleSpecialEvent)),
                prefix: new HarmonyMethod(typeof(NativeCompanionSupplyChecks), nameof(KeepGrowthAchievements)));
            var host = Get(context, "Companions");
            try
            {
                Terraria.GameContent.Creative.CreativePowerManager.Initialize();
                Main.spawnTileX = 60;
                Main.spawnTileY = 58;
                Main.leftWorld = Main.topWorld = 0;
                Main.rightWorld = Main.maxTilesX * 16;
                Main.bottomWorld = Main.maxTilesY * 16;
                for (int x = 2; x < Math.Min(Main.maxTilesX, 118); x++)
                {
                    Main.tile[x, 60].active(true);
                    Main.tile[x, 60].type = TileID.Stone;
                }
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                Require((bool)Get(host, "Enabled"), "companion native supply hooks installed: " + GetOptional(host, "Feedback"));
                var profiles = Get(host, "profiles");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(host, "Update", 0UL); return (bool)Get(profiles, "Ready"); });
                var first = Join(host, "原生补给甲");
                var second = Join(host, "原生补给乙");
                var human = Main.LocalPlayer;
                CheckIdleWork(host, first, second);
                CheckPersonalPickup((Player)Get(first, "Body"), human);
                CheckShortcuts(host, first, second, human);
                CheckLongTerm(host, first);
                CheckPortableVoid(host, first);
                Configure(host, first, new CompanionBehavior { HealthRecoveryEnabled = false, ManaRecoveryEnabled = false,
                    GrowthEnabled = false, BuffEnabled = false });
                NativeCompanionItemOnceChecks.Run(host, first, human, () => Step(host, (Player)Get(first, "Body"), first));
                NativeCompanionFurnitureChecks.Run(host, first, second);
                CheckNurse(host, first, second, human, harness);
            }
            finally
            {
                faultPatient = null;
                stalePosition = null;
                tailFailure = false;
                captureFailures = 0;
                harness.UnpatchAll(harness.Id);
                Call(host, "OnSessionEnded");
            }
        }

        private static void CheckPersonalPickup(Player actor, Player human)
        {
            int slot = Main.maxItems - 1;
            var before = Main.item[slot];
            var timber = ItemPickupCondition.Create(ItemID.Wood);
            try
            {
                foreach (var picker in new[] { actor, human })
                {
                    int wood = Count(picker, ItemID.Wood);
                    Main.item[slot] = new WorldItem(new Item(ItemID.Wood))
                    {
                        whoAmI = slot, position = picker.position, stack = 2,
                        playerIndexTheItemIsReservedFor = picker.whoAmI
                    };
                    Call(picker, "GrabItems", picker.whoAmI);
                    Require(Count(picker, ItemID.Wood) == wood + 2 &&
                        timber.IsCompleted == ReferenceEquals(picker, human),
                        "native GrabItems -> PickupItem keeps the wood on its picker and completes TIMBER only for the human");
                }
            }
            finally { Main.item[slot] = before; }
        }

        private static object Join(object host, string name)
        {
            var profiles = Get(host, "profiles");
            Require((bool)Call(host, "Create", name), "native supply profile created: " + name);
            Guid id = Guid.Empty;
            NativeQuickItemChecks.Until(() =>
            {
                Call(host, "Update", 0UL);
                var newest = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().FirstOrDefault(p => p.Name == name);
                if (newest == null) return false;
                id = newest.Id;
                return (bool)Call(profiles, "IsSaved", id);
            });
            Require((bool)Call(host, "Join", id), "native supply actor joins own activation: " + GetOptional(host, "Feedback"));
            return Call(host, "Owned", Call(host, "GetBody", id));
        }

        private static void Configure(object host, object actor, CompanionBehavior settings)
        {
            settings.Follow = settings.AttackEnabled = false;
            Guid id = ((CompanionProfile)Get(actor, "Profile")).Id;
            Require((bool)Call(host, "SetBehavior", id, settings), "independent supply settings published");
        }

        private static void CheckIdleWork(object host, object first, object second)
        {
            var tested = new[] { first, second };
            var spell = new Item(ItemID.MagicMissile);
            foreach (var actor in tested)
            {
                var body = (Player)Get(actor, "Body");
                body.statLife = body.statLifeMax2;
                body.statMana = body.statManaMax2;
            }
            for (int frame = 0; frame < 90; frame++)
            {
                NativeQuickItemChecks.BeginWorldStep();
                foreach (var actor in tested)
                {
                    Call(host, "TryCompanionLife", actor);
                    Call(host, "TryCompanionMana", actor, spell);
                    Call(host, "TryCompanionBuff", actor);
                    Require((int)Call(host, "CompanionHeldSlot", actor, true) == -1, "empty long-term permits never activate growth");
                }
            }
            foreach (var actor in tested)
            {
                var state = Get(actor, "Supplies");
                if (GetOptional(state, "SourceReads") != null)
                    Require((long)Get(state, "SourceReads") == 0 && (long)Get(state, "GrowthReads") == 0 &&
                        (long)Get(state, "NurseReads") == 0 && (long)Get(state, "WalletPlans") == 0,
                        "default stable two-actor frames perform zero inventory and nurse discovery/payment work");
            }
        }

        private static void CheckShortcuts(object host, object first, object second, Player human)
        {
            var body = (Player)Get(first, "Body");
            var other = (Player)Get(second, "Body");
            var settings = new CompanionBehavior { AllowNurse = false };
            Configure(host, first, settings);
            body.statLifeMax2 = 400;
            body.statLife = 350;
            body.inventory[15] = new Item(ItemID.HealingPotion) { stack = 3 };
            other.inventory[15] = new Item(ItemID.HealingPotion) { stack = 3 };
            human.inventory[15] = new Item(ItemID.HealingPotion) { stack = 3 };
            int humanLife = human.statLife, otherLife = other.statLife;
            Call(host, "TryCompanionLife", first);
            Require(body.inventory[15].stack == 3 && body.statLife == 350, "real smart life at exactly half a potion holds");
            body.statLife = 349;
            Call(host, "TryCompanionLife", first);
            Require(body.inventory[15].stack == 2 && body.statLife == 400 && body.potionDelay > 0 &&
                body.FindBuffIndex(BuffID.PotionSickness) >= 0 && human.statLife == humanLife && other.statLife == otherLife &&
                human.inventory[15].stack == 3 && other.inventory[15].stack == 3 && ReferenceEquals(human, Main.LocalPlayer),
                "actor alone uses the real potion, sickness and identity are native");

            AdvanceClock();
            body.potionDelay = 0;
            body.statLife = 375;
            settings.HealthRecoveryMode = CompanionHealthRecoveryMode.Fast;
            settings.ExcludedHealingTypes = new[] { (int)ItemID.HealingPotion };
            Configure(host, first, settings);
            Call(host, "TryCompanionLife", first);
            Require(body.statLife == 375 && body.inventory[15].stack == 2, "fast explicit blacklist does not call native alternate selector");
            settings.ExcludedHealingTypes = Array.Empty<int>();
            Configure(host, first, settings);
            Call(host, "TryCompanionLife", first);
            Require(body.statLife == 400 && body.inventory[15].stack == 1, "fast on equivalent facts spends only the allowed actor potion");

            AdvanceClock();
            var spell = new Item(ItemID.MagicMissile) { mana = 20 };
            body.statMana = 19;
            body.statManaMax2 = 200;
            body.manaPotionDelay = 0;
            body.manaCost = 1f;
            body.inventory[20] = new Item(ItemID.VoidLens);
            body.bank4.item[8] = new Item(ItemID.ManaPotion) { stack = 3, favorited = true };
            Require(body.useVoidBag(), "native void bag is available for the actor source");
            Call(host, "TryCompanionMana", first, spell);
            Require(body.statMana > 19 && body.bank4.item[8].stack == 2 && body.FindBuffIndex(BuffID.ManaSickness) >= 0,
                "real spell deficits consume the exact native void source under ManaV2");
            body.statMana = 19;
            body.manaPotionDelay = 0;
            body.manaFlower = true;
            using (ActorScope(host, first)) body.CheckMana(20);
            Require(body.bank4.item[8].stack == 2, "native mana flower does not repeat real active mana use in the same frame");

            AdvanceClock();
            settings.ManaRecoveryEnabled = false;
            Configure(host, first, settings);
            Call(host, "TryCompanionMana", first, spell);
            Require(body.bank4.item[8].stack == 2, "disabled active mana keeps its own source");
            using (ActorScope(host, first)) body.CheckMana(20);
            Require(body.bank4.item[8].stack == 1 && body.statMana > 19, "original actor mana flower remains enabled");
            var achievements = new AchievementManager();
            var waterAchievement = GrowthAchievement(achievements, "DRINK_BOTTLED_WATER_WHILE_DROWNING");
            SetAchievementManager(achievements);
            foreach (var drinker in new[] { body, human })
            {
                var before = drinker.inventory[15];
                var water = new Item(ItemID.BottledWater) { stack = 2 };
                drinker.inventory[15] = water;
                drinker.statLife = drinker.statLifeMax2 - 30;
                drinker.breath = drinker.potionDelay = 0;
                if (ReferenceEquals(drinker, body))
                {
                    using (ActorScope(host, first)) drinker.QuickHeal();
                    Require(!waterAchievement.IsCompleted, "actual AI bottled water does not earn the human drowning achievement");
                }
                else
                {
                    drinker.QuickHeal();
                    Require(waterAchievement.IsCompleted, "same native bottled water action by the human still earns the achievement");
                }
                Require(water.stack == 1 && drinker.statLife == drinker.statLifeMax2, "the native bottled water was used and healed its own drinker");
                drinker.inventory[15] = before;
                drinker.breath = drinker.breathMax;
            }
            Console.WriteLine("PASS companion native smart/fast/blacklist, own void mana and real mana flower dedup/disabled invariants.");
        }

        private static void CheckLongTerm(object host, object actor)
        {
            var body = (Player)Get(actor, "Body");
            var achievements = new AchievementManager();
            var star = GrowthAchievement(achievements, "STAR_POWER");
            var topped = GrowthAchievement(achievements, "TOPPED_OFF");
            var fruitAchievement = GrowthAchievement(achievements, "GET_A_LIFE");
            GrowthAchievement(achievements, "GAIN_TORCH_GODS_FAVOR");
            SetAchievementManager(achievements);
            CheckManualAndFood(host, actor);
            body.statLife = body.statLifeMax = body.statLifeMax2 = 360;
            body.statMana = body.statManaMax = body.statManaMax2 = 160;
            var settings = new CompanionBehavior
            {
                HealthRecoveryEnabled = false,
                AllowedGrowthTypes = new[] { (int)ItemID.LifeCrystal, ItemID.ManaCrystal, ItemID.ArtisanLoaf, ItemID.MinecartPowerup, ItemID.DemonHeart },
                AllowedBuffTypes = new[] { (int)ItemID.PumpkinPie, ItemID.Apple, ItemID.IronskinPotion, ItemID.SwiftnessPotion }
            };
            Configure(host, actor, settings);
            body.inventory[15] = new Item(ItemID.LifeCrystal) { stack = 4 };
            body.inventory[16] = new Item(ItemID.ManaCrystal) { stack = 4 };
            body.inventory[17] = new Item(ItemID.ArtisanLoaf) { stack = 2 };
            body.inventory[18] = new Item(ItemID.MinecartPowerup) { stack = 2 };
            body.inventory[19] = new Item(ItemID.LifeFruit) { stack = 3 };
            body.inventory[21] = new Item(ItemID.PumpkinPie) { stack = 2 };
            body.inventory[22] = new Item(ItemID.Apple) { stack = 2 };
            body.inventory[23] = new Item(ItemID.IronskinPotion) { stack = 2 };
            body.inventory[24] = new Item(ItemID.EndurancePotion);
            body.inventory[26] = new Item(ItemID.DemonHeart) { stack = 2 };
            int cart = Count(body, 3353);
            for (int i = 0; i < 220; i++) Step(host, body, actor);
            Require(body.statLifeMax == 400 && body.inventory[15].stack == 2 && body.statManaMax == 200 &&
                body.inventory[16].stack == 2, "exclusive native ItemCheck repeats permanent growth and stops at original limits");
            Require(!star.IsCompleted, "native AI mana growth leaves human achievement unclaimed");
            AchievementsHelper.HandleSpecialEvent(Main.LocalPlayer, 1);
            Require(star.IsCompleted, "the real human growth achievement remains live outside the AI scope");
            Require(body.ateArtisanBread && body.inventory[17].stack == 1 && body.unlockedSuperCart &&
                body.inventory[18].stack == 1 && Count(body, 3353) == cart + 1 && body.inventory[19].stack == 3,
                "native one-time flags and quick-spawn output belong to the actor while unlicensed fruit is preserved");
            Require(body.inventory[21].stack == 1 && body.inventory[22].stack == 2 &&
                body.inventory[23].stack == 1 && body.inventory[24].stack == 1 && body.FindBuffIndex(BuffID.Ironskin) >= 0,
                $"narrow native QuickBuff uses only licensed sources, same food group stops replacement " +
                $"(pie={body.inventory[21].stack}, apple={body.inventory[22].stack}, ironskin={body.inventory[23].stack}, " +
                $"endurance={body.inventory[24].stack}, ironskin effect={body.FindBuffIndex(BuffID.Ironskin) >= 0}, animation={body.itemAnimation})");
            body.inventory[25] = new Item(ItemID.SwiftnessPotion);
            for (int i = 0; i < 65; i++) Step(host, body, actor);
            Require(body.inventory[25].IsAir && body.FindBuffIndex(BuffID.Swiftness) >= 0 &&
                ((CompanionProfile)Get(actor, "Profile")).Behavior.AllowedBuffTypes.Contains(ItemID.SwiftnessPotion),
                "buff license outlives lack of stock and consuming the restock");
            int mode = Main.GameMode;
            try
            {
                Require(!Main.expertMode && !body.extraAccessory && body.inventory[26].stack == 2,
                    "native Expert-only growth waits while this fixture is in Classic");
                Main.GameMode = 1;
                for (int i = 0; i < 40; i++) Step(host, body, actor);
                Require(body.extraAccessory && body.inventory[26].stack == 1 &&
                    ((CompanionProfile)Get(actor, "Profile")).Behavior.AllowedGrowthTypes.Contains(ItemID.DemonHeart),
                    "unchanged long-term consent executes exactly once when native mode becomes eligible");
            }
            finally { Main.GameMode = mode; }
            body.statLife = body.statLifeMax = body.statLifeMax2 = 495;
            settings.AllowedGrowthTypes = new[] { (int)ItemID.LifeFruit };
            Configure(host, actor, settings);
            for (int i = 0; i < 52; i++) Step(host, body, actor);
            Require(body.statLifeMax == 500 && body.inventory[19].stack == 2 && !topped.IsCompleted && !fruitAchievement.IsCompleted,
                "original life-fruit reaches its higher cap with one debit and leaves both human achievements alone");
            Console.WriteLine("PASS companion native growth caps, permanent marker, mode eligibility and minecart output, exact Buff source and restock permissions.");
        }

        private static void CheckManualAndFood(object host, object actor)
        {
            var body = (Player)Get(actor, "Body");
            var id = ((CompanionProfile)Get(actor, "Profile")).Id;
            var settings = new CompanionBehavior { HealthRecoveryEnabled = false, ManaRecoveryEnabled = false,
                GrowthEnabled = false, BuffEnabled = true, AllowedGrowthTypes = new[] { (int)ItemID.LifeCrystal },
                AllowedBuffTypes = new[] { (int)ItemID.Apple, ItemID.PumpkinPie, ItemID.SeafoodDinner } };
            Configure(host, actor, settings);
            body.statLife = body.statLifeMax = body.statLifeMax2 = 300;
            body.inventory[15] = new Item(ItemID.LifeCrystal) { stack = 10 };
            body.inventory[20] = new Item(ItemID.Apple) { stack = 2 };
            body.inventory[21] = new Item(ItemID.PumpkinPie) { stack = 2 };
            body.inventory[22] = new Item(ItemID.SeafoodDinner) { stack = 2 };
            Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 15),
                "manual crystal admission ignores the disabled automatic growth toggle");
            for (int i = 0; i < 65; i++) Step(host, body, actor);
            var behavior = ((CompanionProfile)Get(actor, "Profile")).Behavior;
            Require(body.statLifeMax == 320 && body.inventory[15].stack == 9 && !behavior.GrowthEnabled &&
                behavior.AllowedGrowthTypes.SequenceEqual(new[] { (int)ItemID.LifeCrystal }),
                "one actual crystal effect and debit leave automatic growth and its policy untouched");
            Require(body.inventory[20].stack == 2 && body.inventory[21].stack == 2 && body.inventory[22].stack == 1 &&
                body.FindBuffIndex(body.inventory[22].buffType) >= 0,
                "automatic food picks the highest permitted native tier after scanning past earlier lower-tier food");
            body.DelBuff(body.FindBuffIndex(body.inventory[22].buffType));
            settings.BuffEnabled = false;
            Configure(host, actor, settings);
            Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 20),
                "manual choice can request one precise food source while its policy is disabled");
            for (int i = 0; i < 35; i++) Step(host, body, actor);
            Require(body.inventory[20].stack == 1 && body.inventory[21].stack == 2 && body.inventory[22].stack == 1 &&
                body.FindBuffIndex(body.inventory[20].buffType) >= 0,
                "manual native QuickBuff eats only the selected lower-tier food");
            body.DelBuff(body.FindBuffIndex(body.inventory[20].buffType));
            body.inventory[20] = body.inventory[21] = body.inventory[22] = new Item();
            body.inventory[23] = new Item(ItemID.ManaPotion) { stack = 2 };
            body.statLife = body.statLifeMax2;
            body.statMana = 10;
            body.statManaMax = body.statManaMax2 = 160;
            body.potionDelay = body.manaPotionDelay = 0;
            Require((bool)Call(host, "UseItemOnce", id, CompanionInventoryArea.Inventory, 23),
                "manual mana potion is valid when only mana is missing");
            Step(host, body, actor);
            Require(body.statMana > 10 && body.inventory[23].stack == 1,
                "one potion uses the native mana entrance and debits its exact source once");
            body.inventory[23] = new Item();
        }

        private static void CheckPortableVoid(object host, object actor)
        {
            var body = (Player)Get(actor, "Body");
            var id = ((CompanionProfile)Get(actor, "Profile")).Id;
            var area = CompanionInventoryArea.VoidVault;
            var priorItem = body.inventory[24];
            var priorBank = body.bank4.item[7];
            int priorLock = body.disableVoidBag;
            int priorSelection = body.selectedItem;
            Projectile provider = null;
            try
            {
                var bag = new Item(ItemID.ClosedVoidBag);
                body.inventory[24] = bag;
                Require(!body.useVoidBag() && !(bool)Call(host, "StorageAvailable", new object[] { id, area, null }),
                    "a carried closed void bag gives no automatic supply or manual storage entrance");
                Require((bool)Call(host, "OpenStorage", id, area) &&
                    !(bool)Call(host, "StorageAvailable", new object[] { id, area, null }),
                    "accepting a portable-use command does not unlock the bank before ItemCheck");
                for (int tick = 0; tick < 70 && provider == null; tick++)
                {
                    Step(host, body, actor);
                    provider = Main.projectile.FirstOrDefault(p => p.active && p.type == ProjectileID.VoidLens && p.owner == body.whoAmI);
                }
                Require(provider != null && body.inventory[24].type == ItemID.ClosedVoidBag && !body.useVoidBag() &&
                    (bool)Call(host, "StorageAvailable", new object[] { id, area, null }),
                    "real companion ItemCheck spawns 734 and opens manual Bank4 without activating automatic void supply");
                Require(body.itemAnimation > 0, "the provider was born before the real portable animation was over");
                var gestureType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemGesture", true);
                body.bank4.item[7] = new Item(ItemID.Wood) { stack = 6 };
                Require((bool)Call(host, "ItemGesture", id, false, area, 7, Enum.Parse(gestureType, "Left")),
                    "the live portable provider admits a draft");
                body.LockNetSlot(PlayerItemSlotID.Bank4_0 + 7);
                Require(!(bool)Call(host, "StorageAvailable", new object[] { id, area, null }) &&
                    !(bool)Call(host, "ValidateItemGesture") && body.bank4.item[7].stack == 6,
                    "native void slot lock revokes UI access and cancels the bank draft with no debit");
                // A projectile can appear on the first use tick. Finish its
                // native hand animation before handing this body to another case.
                for (int tick = 0; tick <= bag.useAnimation + 2 &&
                    (body.UsingOrReusingItem || body.itemTime > 0 || body.selectedItem != priorSelection); tick++)
                    Step(host, body, actor);
                Require(!body.UsingOrReusingItem && body.itemTime == 0 && body.selectedItem == priorSelection,
                    "the portable command lets the real hand settle and restores the previous selection");
            }
            finally
            {
                if (provider?.active == true) provider.Kill();
                body.inventory[24] = priorItem;
                body.bank4.item[7] = priorBank;
                body.disableVoidBag = priorLock;
                Call(host, "SetItemStorage", id, CompanionInventoryArea.Inventory);
            }
        }

        private static void CheckNurse(object host, object first, object second, Player human, Harmony harness)
        {
            var body = (Player)Get(first, "Body");
            var other = (Player)Get(second, "Body");
            Configure(host, first, new CompanionBehavior());
            Configure(host, second, new CompanionBehavior());
            Main.BestiaryTracker = new BestiaryUnlocksTracker();
            Main.BestiaryDB = new BestiaryDatabase();
            new BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            var achievements = new AchievementManager();
            var flyer = new Achievement("FREQUENT_FLYER");
            var spent = (CustomFloatCondition)CustomFloatCondition.Create("Pay", 10000000f);
            flyer.AddCondition(spent);
            achievements.Register(flyer);
            SetAchievementManager(achievements);
            for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i] = new NPC();
            body.position = new Vector2(60 * 16, 60 * 16 - body.height);
            other.position = body.position + new Vector2(10, 0);
            human.position = body.position + new Vector2(-10, 0);
            var nurse = new NPC();
            nurse.SetDefaults(NPCID.Nurse);
            nurse.whoAmI = 0;
            nurse.active = true;
            nurse.position = body.position + new Vector2(24, 0);
            nurse.homeless = false;
            nurse.homeTileX = 63;
            nurse.homeTileY = 60;
            Main.npc[0] = nurse;
            CheckNurseActorRange(host, first, body, nurse);
            int bodyLife = body.statLifeMax2;
            human.statLife = human.statLifeMax2 - 1;
            int humanLife = human.statLife;
            human.AddBuff(BuffID.Poisoned, 600);
            Set(human, "talkNPC", 22);
            Main.npcChatText = "真人原生聊天";
            Main.npcChatPortraitFrameCounter = 12;
            body.discountAvailable = true;
            ClearCoins(body);
            ClearCoins(human);
            body.statLife = bodyLife - 120;
            var source = new Item(ItemID.HealingPotion) { stack = 2 };
            body.inventory[35] = source;
            PutCoin(body.inventory, 50, ItemID.CopperCoin, 12);
            PutCoin(human.bank4.item, 7, ItemID.GoldCoin, 1);
            int quote = Quote(host, first, nurse);
            long original = NativeRecoveryServiceChecks.Total(human);
            var ownerOperations = (ItemOperationOwnership)Get(host, "OwnerOperations");
            var locked = new ulong[5];
            locked[4] = 1UL << 7;
            long token = ownerOperations.NewUseToken();
            Require(ownerOperations.TryBeginRecovery(ownerOperations.Session, locked, token), "fixture acquires the real owner bank lease");
            AdvanceClock();
            Require(Call(host, "TryCompanionNurse", first).ToString() == "Unpaid" &&
                NativeRecoveryServiceChecks.Total(body) == 12 && NativeRecoveryServiceChecks.Total(human) == original &&
                body.statLife == bodyLife - 120, "owner bank refusal leaves also the patient's first share unspent");
            ownerOperations.EndRecovery(ownerOperations.Session, locked, token, false);
            Configure(host, first, new CompanionBehavior { HealthRecoveryEnabled = false, AllowNurse = true });
            AdvanceClock();
            Call(host, "TryCompanionLife", first);
            Require(quote > 12 && body.statLife == bodyLife && NativeRecoveryServiceChecks.Total(body) == 0 &&
                NativeRecoveryServiceChecks.Total(human) == original - (quote - 12) && source.stack == 2,
                "real discounted AI quote, copper debit, owner's gold/void change and no equivalent blood potion; quote=" + quote +
                " after=" + body.statLife + " owner=" + NativeRecoveryServiceChecks.Total(human) + " feedback=" + GetOptional(host, "Feedback"));
            Require(human.statLife == humanLife && human.FindBuffIndex(BuffID.Poisoned) >= 0 && human.talkNPC == 22 &&
                spent.Value == 0 && Main.npcChatText == "真人原生聊天" && Main.npcChatPortraitFrameCounter == 12 &&
                ReferenceEquals(human, Main.LocalPlayer), "AI debit leaves human state, dialogue, achievement and identity untouched");

            body.statLife = bodyLife - 1;
            original = NativeRecoveryServiceChecks.Total(human);
            AdvanceClock();
            Require(Quote(host, first, nurse) == 0 && Call(host, "TryCompanionNurse", first).ToString() == "Unpaid" &&
                body.statLife == bodyLife - 1 && NativeRecoveryServiceChecks.Total(human) == original,
                "zero native price leaves the original dialogue-only case without free healing");

            other.statLife = other.statLifeMax2;
            other.AddBuff(BuffID.Poisoned, 600);
            Configure(host, second, new CompanionBehavior { HealthRecoveryEnabled = false, AllowNurse = true });
            original = NativeRecoveryServiceChecks.Total(human);
            quote = Quote(host, second, nurse);
            Call(host, "TryCompanionLife", second);
            Require(NativeRecoveryServiceChecks.Total(human) == original - quote && other.FindBuffIndex(BuffID.Poisoned) < 0,
                "full-life debuff treatment uses the separate nurse switch with automatic potions off");

            // With a nearby affordable nurse, only the enabled potion policy
            // may act. The complementary full-life debuff case above exercises
            // real payment with the potion policy disabled.
            Configure(host, first, new CompanionBehavior { HealthRecoveryEnabled = true, AllowNurse = false });
            body.statLife = bodyLife - 120;
            body.potionDelay = 0;
            var independentPotion = new Item(ItemID.HealingPotion) { stack = 2 };
            body.inventory[35] = independentPotion;
            original = NativeRecoveryServiceChecks.Total(human);
            AdvanceClock();
            Require(Call(host, "TryCompanionNurse", first).ToString() == "NotRun", "disabled independent nurse cannot reach payment");
            Call(host, "TryCompanionLife", first);
            Require(independentPotion.stack == 1 && NativeRecoveryServiceChecks.Total(human) == original &&
                body.statLife > bodyLife - 120, "potions work while an affordable disabled nurse spends no money");
            body.inventory[35] = source;
            body.ClearBuff(BuffID.PotionSickness);
            Configure(host, first, new CompanionBehavior());

            ClearCoins(human);
            body.statLife = bodyLife - 120;
            body.potionDelay = 0;
            PutCoin(body.inventory, 50, ItemID.CopperCoin, 3);
            AdvanceClock();
            Call(host, "TryCompanionLife", first);
            Require(source.stack == 1 && NativeRecoveryServiceChecks.Total(body) == 3 && NativeRecoveryServiceChecks.Total(human) == 0,
                "combined insufficient zero debit, fallback potion used exactly once");

            PutCoin(body.bank.item, 8, ItemID.GoldCoin, 1);
            body.statLife = bodyLife - 120;
            original = NativeRecoveryServiceChecks.Total(body);
            quote = Quote(host, first, nurse);
            AdvanceClock();
            Require(Call(host, "TryCompanionNurse", first).ToString() == "Treated" &&
                NativeRecoveryServiceChecks.Total(body) == original - quote && NativeRecoveryServiceChecks.Total(human) == 0,
                "AI with sufficient gold funds owns every coin of its own original native treatment");

            // Real native invocation with an induced move immediately at the
            // boundary. Planning, quote and payment consumers stay unchanged.
            body.statLife = bodyLife - 120;
            original = NativeRecoveryServiceChecks.Total(body);
            Vector2 near = body.position;
            stalePosition = near + new Vector2(400, 0);
            faultPatient = body;
            harness.Patch(AccessTools.Method(typeof(Main), nameof(Main.NPCChatText_DoNurseHeal)),
                prefix: new HarmonyMethod(typeof(NativeCompanionSupplyChecks), nameof(StaleRange)));
            AdvanceClock();
            Require(Call(host, "TryCompanionNurse", first).ToString() == "Unpaid" &&
                NativeRecoveryServiceChecks.Total(body) == original && body.statLife == bodyLife - 120,
                "out-of-range patient at real payment boundary changes neither purse nor health");
            body.position = near;
            stalePosition = null;

            tailFailure = true;
            harness.Patch(AccessTools.Method(typeof(Player), nameof(Player.HealEffect), new[] { typeof(int), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(NativeCompanionSupplyChecks), nameof(FailNurseTail)));
            AdvanceClock();
            quote = Quote(host, first, nurse);
            Require(Call(host, "TryCompanionNurse", first).ToString() == "Unconfirmed" &&
                NativeRecoveryServiceChecks.Total(body) == original - quote && body.statLife == bodyLife - 120,
                "native failure after actual payment keeps last real coin results");
            AdvanceClock();
            Require(Call(host, "TryCompanionNurse", first).ToString() == "NotRun" &&
                NativeRecoveryServiceChecks.Total(body) == original - quote, "uncertain payment cannot debit the patient again");

            PutCoin(human.inventory, 50, ItemID.SilverCoin, 30);
            human.currentShoppingSettings = new ShoppingSettings { PriceAdjustment = 1f };
            quote = Main.GetNurseHealCost();
            Main.NPCChatText_DoNurseHeal(quote);
            Require(human.statLife == human.statLifeMax2 && human.FindBuffIndex(BuffID.Poisoned) < 0 &&
                spent.Value == quote && Main.npcChatPortraitFrameCounter == 0,
                "existing human BuyItem, treatment, portrait and achievement survive scoped nurse hooks");
            CheckNurseCaptureRetry(host, first, harness);
            CheckNurseHallow(host, second, human, nurse);
            Console.WriteLine("PASS original nurse quote + treatment + native TryPurchasing, both wallets and change, no funds, stale range, post-payment uncertainty, capture/rejoin without debit replay and human dialogue/achievement.");
            NativeCompanionSupplyWireChecks.Run(host, second, human, nurse);
        }

        private static void CheckNurseActorRange(object host, object actor, Player body, NPC nurse)
        {
            Vector2 position = nurse.position;
            int ownerX = Player.tileRangeX, ownerY = Player.tileRangeY;
            bool accessory = body.equippedAnyTileRangeAcc, pending = (bool)Get(actor, "TileReachPending");
            var state = Get(actor, "Supplies");
            try
            {
                nurse.position = body.position + new Vector2(126, 0);
                Player.tileRangeX = Player.tileRangeY = 20;
                body.equippedAnyTileRangeAcc = false;
                Require(Call(host, "FindNurse", actor) == null, "the AI cannot borrow the human's expanded native reach for nurse discovery");
                Player.tileRangeX = Player.DefaultTileRangeX;
                Player.tileRangeY = Player.DefaultTileRangeY;
                body.equippedAnyTileRangeAcc = true;
                Require(ReferenceEquals(Call(host, "FindNurse", actor), nurse) &&
                    (bool)Call(host, "NurseCurrent", body, nurse, 0, nurse.generation),
                    "the same nurse is eligible through this AI's own effective range accessory at scan and treatment");
                Call(host, "InvalidateActorTileReach", body);
                Require(Call(host, "FindNurse", actor) == null &&
                    !(bool)Call(host, "NurseCurrent", body, nurse, 0, nurse.generation),
                    "an equipment write retires the stale cached range before the native body refresh");
            }
            finally
            {
                nurse.position = position;
                Player.tileRangeX = ownerX;
                Player.tileRangeY = ownerY;
                body.equippedAnyTileRangeAcc = accessory;
                Set(actor, "TileReachPending", pending);
                Set(state, "Nurse", null);
                Set(state, "NurseSlot", -1);
                Set(state, "NurseScan", 0u);
            }
        }

        private static void CheckNurseHallow(object host, object actor, Player human, NPC nurse)
        {
            var patient = (Player)Get(actor, "Body");
            var previousHumanPosition = human.position;
            var savedTiles = new Tile[16, 10];
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 10; y++)
                {
                    savedTiles[x, y] = Main.tile[86 + x, 32 + y];
                    var tile = new Tile { type = TileID.Pearlstone };
                    tile.active(true);
                    Main.tile[86 + x, 32 + y] = tile;
                }
            try
            {
                human.position = new Vector2(3 * 16, patient.position.Y);
                patient.statLife = patient.statLifeMax2 - 90;
                bool stale = patient.ZoneHallow;
                patient.ZoneHallow = true;
                int expected = Quote(host, actor, nurse);
                patient.ZoneHallow = false;
                int staleQuote = Quote(host, actor, nurse);
                long total = NativeRecoveryServiceChecks.Total(human) + NativeRecoveryServiceChecks.Total(patient);
                var mainScene = Main.PlayerSceneMetrics;
                var mainSceneCenter = mainScene.Center;
                AdvanceClock();
                Require(expected < staleQuote && Call(host, "TryCompanionNurse", actor).ToString() == "Treated" &&
                    total - NativeRecoveryServiceChecks.Total(human) - NativeRecoveryServiceChecks.Total(patient) == expected &&
                    !patient.ZoneHallow && ReferenceEquals(mainScene, Main.PlayerSceneMetrics) && mainScene.Center == mainSceneCenter,
                    "distant payer and stale zone flags still charge the native local Hallow quote and leave the human scene alone");
                patient.ZoneHallow = stale;
            }
            finally
            {
                human.position = previousHumanPosition;
                for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 10; y++) Main.tile[86 + x, 32 + y] = savedTiles[x, y];
            }
        }

        private static void CheckNurseCaptureRetry(object host, object actor, Harmony harness)
        {
            var body = (Player)Get(actor, "Body");
            var profiles = Get(host, "profiles");
            var id = ((CompanionProfile)Get(actor, "Profile")).Id;
            var committed = NativeRecoveryServiceChecks.Total(body);
            harness.Patch(AccessTools.DeclaredMethod(profiles.GetType(), "Capture"),
                prefix: new HarmonyMethod(typeof(NativeCompanionSupplyChecks), nameof(FailCapture)));
            faultPatient = body;
            captureFailures = 2;
            Set(actor, "NextSave", 0u);
            Call(host, "Update", 0UL);
            Require((bool)Get(actor, "Closing") && !(bool)Get(actor, "CloseCaptured") &&
                NativeRecoveryServiceChecks.Total(body) == committed,
                "failed persistence retains the one already-debited body");
            Require((bool)Call(host, "RetrySave", id), "retry captures current real post-payment body");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !(bool)Call(host, "IsJoined", id); });
            Require((bool)Call(host, "Join", id), "same DataEpoch rejoins after latest capture saved");
            var reopened = (Player)Call(host, "GetBody", id);
            var reopenedActor = Call(host, "Owned", reopened);
            reopened.statLife = Math.Max(1, reopened.statLifeMax2 - 50);
            Require(NativeRecoveryServiceChecks.Total(reopened) == committed &&
                Call(host, "TryCompanionNurse", reopenedActor).ToString() == "NotRun",
                "uncertain nurse receipt survives ordinary restore and retry without repeating payment");
        }

        private static IDisposable ActorScope(object host, object actor)
        {
            var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks", true);
            return (IDisposable)hooks.GetMethod("Enter", Flags).Invoke(null, new[] { actor, ((Player)Get(actor, "Body")).Center, (object)false });
        }

        private static int Quote(object host, object actor, NPC nurse)
        {
            var body = (Player)Get(actor, "Body");
            var before = body.currentShoppingSettings;
            try
            {
                using (ActorScope(host, actor))
                {
                    body.currentShoppingSettings = new ShopHelper().GetShoppingSettings(body, nurse);
                    return Main.GetNurseHealCost();
                }
            }
            finally { body.currentShoppingSettings = before; }
        }

        private static void AdvanceClock()
        {
            var clock = typeof(Main).GetField("_gameUpdateCount", Flags);
            clock.SetValue(null, Main.GameUpdateCount + 90u);
        }

        private static void Step(object host, Player body, object actor)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", 0UL);
            body.Update(body.whoAmI);
            Require(!(bool)Get(actor, "Faulted"), "native growth actor update valid: " + Get(actor, "Status"));
        }

        private static int Count(Player body, int type) { return body.inventory.Where(i => i.type == type).Sum(i => i.stack); }

        private static void ClearCoins(Player body)
        {
            foreach (var item in body.inventory.Concat(body.bank.item).Concat(body.bank2.item).Concat(body.bank3.item).Concat(body.bank4.item))
                if (item.IsACoin) item.TurnToAir();
        }

        private static void PutCoin(Item[] items, int slot, int type, int count) { items[slot] = NativeCoinChecks.Coin(type, count); }
        private static bool SkipHeadlessOutlet() { return false; }
        private static bool KeepGrowthAchievements(int __1) { return __1 == 0 || __1 == 1 || __1 == 2 || __1 == 24 || __1 == 25; }
        private static AchievementCondition GrowthAchievement(AchievementManager manager, string name)
        {
            var achievement = new Achievement(name);
            var condition = CustomFlagCondition.Create("Use");
            achievement.AddCondition(condition);
            manager.Register(achievement);
            return condition;
        }
        private static void SetAchievementManager(AchievementManager manager)
        {
            if (Main.instance == null)
                Main.instance = (Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
            typeof(Main).GetField("_achievements", Flags).SetValue(Main.instance, manager);
        }
        private static void StaleRange()
        {
            if (stalePosition.HasValue && ReferenceEquals(Main.LocalPlayer, faultPatient)) faultPatient.position = stalePosition.Value;
        }
        private static void FailNurseTail(Player __instance)
        {
            if (!tailFailure || !ReferenceEquals(__instance, faultPatient)) return;
            tailFailure = false;
            throw new IOException("isolated failure after original nurse's accepted purchase");
        }
        private static void FailCapture(Player __0)
        {
            if (captureFailures == 0 || !ReferenceEquals(__0, faultPatient)) return;
            captureFailures--;
            throw new IOException("isolated failure capturing latest spent wallet");
        }
    }
}
