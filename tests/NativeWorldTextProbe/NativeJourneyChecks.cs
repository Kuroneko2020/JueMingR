using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The writer uses the real Host composition and original serializers. The
    // reader is a fresh process which never initializes Host or installs R.
    // All paths belong to this invocation's disposable output directory.
    internal static class NativeJourneyChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static ManualResetEvent saveEntered, saveContinue;
        private static bool pauseWorldSave;
        private static bool failWorldSave;
        private static Action savePauseAction;
        private static int admissionSaveThreadId;
        [ThreadStatic] private static bool insideAdmissionNestedSave;
        private static int nativeMenuCloseFailures;
        private static int checks;
        private static bool saveTraceInstalled;

        internal static int Run(string content, string output, bool reopenOnly = false)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            string root = NewRoot(output, "run");
            NativeQuickItemChecks.Run(context =>
            {
                using (var graphics = new ProbeGraphics(content))
                {
                    Prepare(context);
                    NativeJourneyUiChecks.Run(context, graphics, output, reopenOnly);
                    object journey = Get(context, "Journey");
                    Check((bool)Call(journey, "SetEnabled", false), "UI completion returns the borrowed state");
                    // The closeout scope reuses original preparation/UI input,
                    // then stops before any copy/save/consumer/kill matrix.
                    if (reopenOnly) return;
                    UseDuplicatedItem(context);
                    VerticalSave(context, content, output);
                    ConsumerChecks(context);
                    SaveMatrix(context, output);
                    RenameWhileActive(context, output);
                    SaveException(context);
                    SaveInterleave(context, output);
                    AdmissionAndReset(context, output);
                }
            }, processing: true, shortFeedback: true);
            if (reopenOnly)
            {
                Console.WriteLine("PASS JOURNEY REOPEN: real Shell Enable/reopen/Disable/binding, stable Persistence and selected power; no save/reader/kill/coexist matrix.");
                return 0;
            }
            ReadInNewProcess(content, output);
            KillOwnWriter(content, output);
            Console.WriteLine("PASS JOURNEY core checks=" + checks + " root=" + root + "; native consumers, real save/independent native reads, owned writer killed without cleanup.");
            return 0;
        }

        internal static int RunCpu(string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output); NewRoot(output, "cpu");
            NativeQuickItemChecks.Run(context =>
            {
                Prepare(context); object journey = Get(context, "Journey");
                Check(!(bool)Call(journey, "SetEnabled", true) && !(bool)Get(journey, "IsActive"), "Locked CPU request cannot obtain native menu/capability admission");
                byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer), tracker = CaptureTracker(Main.LocalPlayer);
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 100; i++) { Call(journey, "Update", (ulong)i); Check((bool)Call(journey, "SetEnabled", false), "stable OFF cleanup remains idempotent"); }
                double ms = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                Check(GetOptional(journey, "research") == null && GetOptional(journey, "baseline") == null && !Main.CreativeMenu.Enabled &&
                    CaptureWorld().SequenceEqual(world) && CapturePlayer(Main.LocalPlayer).SequenceEqual(player) && CaptureTracker(Main.LocalPlayer).SequenceEqual(tracker), "Locked/OFF never constructs research/baseline or changes native persistent values");
                NativeHiddenEntryChecks.RevealForComposition(context);
                ConfigureFiles(0, 0, "cpu-native"); Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 19;
                OffSaveAdmission(context, output);
                Console.WriteLine("JOURNEY OFF-100-reflective-probe-ms=" + ms.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; includes test reflection; no FPS inference.");
            }, processing: true, shortFeedback: true);
            ReadInNewProcess(content, output);
            Console.WriteLine("PASS JOURNEY CPU exact patch installation, Locked refusal/OFF bounds, OFF-save first-enable admission and original non-Active serialization; Active ability/save/graphics/kill obligations remain Journey.");
            return 0;
        }

        private static void OffSaveAdmission(object context, string output)
        {
            object journey = Get(context, "Journey");
            Check((bool)Get(journey, "CanEnable"), "unlocked local OFF session can admit an enable before serialization begins");
            OffSavePaused(journey, output, false);
            OffSavePaused(journey, output, true);
            OffSaveFailure(journey, false);
            OffSaveFailure(journey, true);
            // The player fault deliberately exercises the real file writer.
            // Re-save normally before the independent reader; no assertion
            // treats an interrupted native player file as an atomic replace.
            SaveCurrent(output, "cpu-native", 0, 0, CaptureWorld(), CapturePlayer(Main.LocalPlayer));
        }

        private static void OffSavePaused(object journey, string output, bool playerBoundary)
        {
            byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer), tracker = CaptureTracker(Main.LocalPlayer);
            var h = new Harmony("NativeJourneyChecks.OffSaveAdmission." + playerBoundary);
            var target = playerBoundary ? typeof(CreativePowerManager).GetMethod("SaveToPlayer", Flags) : typeof(WorldFile).GetMethod("SaveCreativePowers", Flags);
            saveEntered = new ManualResetEvent(false); saveContinue = new ManualResetEvent(false); pauseWorldSave = true;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                admissionSaveThreadId = Thread.CurrentThread.ManagedThreadId;
                try { SaveCurrent(output, "cpu-native", 0, 0, world, player); } catch (Exception error) { failure = error; }
            }) { IsBackground = true };
            if (!playerBoundary) savePauseAction = () =>
            {
                // This inner original serializer uses only its own memory
                // stream. Its exit must not retire the still-active outer
                // real file serializer's admission protection.
                insideAdmissionNestedSave = true;
                try { using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) WorldFile.SaveWorld_Version2(writer); }
                finally { insideAdmissionNestedSave = false; }
                Check(!(bool)Get(journey, "CanEnable") && !(bool)Call(journey, "SetEnabled", true),
                    "a nested original world serializer exits while its real outer save still refuses first activation");
            };
            h.Patch(target, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(PauseSave)));
            try
            {
                thread.Start();
                Check(saveEntered.WaitOne(10000), "actual non-Active " + (playerBoundary ? "player" : "world") + " file serializer reaches its creative-power section");
                Check(!(bool)Get(journey, "CanEnable") && !(bool)Call(journey, "SetEnabled", true) && !(bool)Get(journey, "IsActive"),
                    "a real in-flight OFF serializer refuses first activation before any native menu initialization");
                Check(GetOptional(journey, "research") == null && GetOptional(journey, "Persistence") == null &&
                    CaptureWorld().SequenceEqual(world) && CapturePlayer(Main.LocalPlayer).SequenceEqual(player) && CaptureTracker(Main.LocalPlayer).SequenceEqual(tracker),
                    "save admission refusal constructs no research/baseline and changes no original powers or research progress");
                Check(((string)Get(journey, "LastFailure")).Contains("保存"), "in-flight save refusal reports the actual save boundary");
                saveContinue.Set();
                Check(thread.Join(20000), "original non-Active player/world save finishes after the real serialization pause is released");
                if (failure != null) throw new Exception("Original OFF save failed", failure);
                Check((bool)Get(journey, "CanEnable"), "first-enable admission returns after the original serializer exits");
                Console.WriteLine("PASS JOURNEY OFF " + (playerBoundary ? "player" : "world/nested-world") + " original serialization refuses first activation without side effects and releases admission on completion.");
            }
            finally
            {
                pauseWorldSave = false; saveContinue.Set(); thread.Join(20000);
                h.Unpatch(target, HarmonyPatchType.All, h.Id); savePauseAction = null; admissionSaveThreadId = 0;
                saveEntered.Dispose(); saveContinue.Dispose();
            }
        }

        private static void OffSaveFailure(object journey, bool playerBoundary)
        {
            var h = new Harmony("NativeJourneyChecks.OffSaveFailure." + playerBoundary);
            var target = playerBoundary ? typeof(CreativePowerManager).GetMethod("SaveToPlayer", Flags) : typeof(WorldFile).GetMethod("SaveCreativePowers", Flags);
            admissionSaveThreadId = Thread.CurrentThread.ManagedThreadId;
            savePauseAction = () => Check(!(bool)Get(journey, "CanEnable") && !(bool)Call(journey, "SetEnabled", true),
                "actual OFF serializer refuses first activation immediately before its controlled original failure");
            failWorldSave = true;
            h.Patch(target, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(PauseSave)));
            try
            {
                bool thrown = false;
                try
                {
                    if (playerBoundary) typeof(Player).GetMethod("InternalSavePlayerFile", Flags).Invoke(null, new object[] { Main.ActivePlayerFileData });
                    else using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) WorldFile.SaveWorld_Version2(writer);
                }
                catch (SaveBoundaryFailure error) { thrown = true; Console.WriteLine("JOURNEY OFF controlled original serializer exception: " + error); }
                catch (TargetInvocationException error) when (error.InnerException is SaveBoundaryFailure) { thrown = true; Console.WriteLine("JOURNEY OFF controlled original serializer reflection exception: " + error); }
                Check(thrown && (bool)Get(journey, "CanEnable") && GetOptional(journey, "research") == null && GetOptional(journey, "Persistence") == null,
                    "original " + (playerBoundary ? "player" : "world") + " serializer exception is preserved and its Finalizer releases first-enable admission without side effects");
                Console.WriteLine("PASS JOURNEY OFF " + (playerBoundary ? "player" : "world") + " original serializer failure preserves its exception and releases first-enable admission.");
            }
            finally { h.Unpatch(target, HarmonyPatchType.All, h.Id); failWorldSave = false; savePauseAction = null; admissionSaveThreadId = 0; }
        }

        // This separate entry preserves the full Journey obligation while
        // allowing its real Active-save/abrupt-exit boundary to be checked
        // independently of the original interactive UI fixtures.
        internal static int RunKillCheck(string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            KillOwnWriter(content, output);
            Console.WriteLine("PASS JOURNEY independent Active native save, owned writer killed without exit cleanup, and fresh original reader without R; interactive copy, consumers and full mode matrix remain Journey obligations.");
            return 0;
        }

        internal static int KillWriter(string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output); NewRoot(output, "kill-writer");
            NativeQuickItemChecks.Run(context =>
            {
                using (var graphics = new ProbeGraphics(content))
                {
                    Prepare(context); NativeJourneyUiChecks.PrepareNativeMenu(graphics);
                    NativeHiddenEntryChecks.RevealForComposition(context);
                    ConfigureFiles(0, 2, "killed"); Call(context, "UpdateRuntime");
                    object journey = Get(context, "Journey");
                    byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
                    Check((bool)Call(journey, "SetEnabled", true), "owned kill writer uses real asset-backed native menu admission");
                    Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 23;
                    Shared(CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>(), 0f);
                    CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
                    SaveCurrent(output, "killed", 0, 2, world, player);
                    Check((bool)Get(journey, "IsActive"), "writer remains Active after durable native save");
                    File.WriteAllText(Path.Combine(output, "kill-ready.txt"), Process.GetCurrentProcess().Id.ToString());
                    // The parent owns this exact Process instance. Deliberate
                    // absence of a finally/Stop on this path is the test point.
                    new ManualResetEvent(false).WaitOne();
                }
            }, processing: true, shortFeedback: true);
            return 3;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ReadChild(string output)
        {
            output = Path.GetFullPath(output);
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Terraria.Program.SavePath = Path.Combine(output, "reader-empty-root"); Directory.CreateDirectory(Terraria.Program.SavePath);
            NativeTestAudio.Install(typeof(Main).Assembly);
            return ReadOriginal(output);
        }

        // Main is beforefieldinit. This must be a separate non-inlined native
        // stage, after the caller has installed its owned Program.SavePath.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadOriginal(string output)
        {
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("JueMingR.", StringComparison.Ordinal)), "reader has no loaded R runtime assembly");
            LanguageManager.Instance.SetLanguage("en-US"); Lang.InitializeLegacyLocalization();
            Main.myPlayer = Main.netMode = 0; Main.rand = new UnifiedRandom(123); Main.gameMenu = true;
            // Original Item.GetDrawHitbox explicitly supports the original
            // headless reader: dedServ returns Rectangle.Empty before GPU work.
            // LoadPlayer/Deserialize and their acceptance rules remain original.
            Main.dedServ = true;
            // Original item samples include the local player's clothing colors
            // (Item.SetDefaults1 IDs269..271); supply a genuine native Player.
            Main.player[0] = new Player { whoAmI = 0 };
            ContentSamples.Initialize(); CreativePowerManager.Initialize(); CreativeItemSacrificesCatalog.Instance.Initialize();
            string[] manifests = Directory.GetFiles(output, "journey-save-*.txt"); Check(manifests.Length > 0, "native reader receives exact writer manifests");
            foreach (string manifest in manifests)
            {
                string[] rows = File.ReadAllLines(manifest); string playerPath = rows[0], worldPath = rows[1];
                Check(IsWithin(playerPath, output) && IsWithin(worldPath, output), "reader only accepts invocation-owned save paths");
                int playerMode = Int32.Parse(rows[2]), worldMode = Int32.Parse(rows[3]);
                // Deserialize directly first so an original load exception is
                // retained rather than reduced by LoadPlayer's empty catch.
                Player parsedPlayer;
                byte[] rawPlayerPowers = ExtractPlayerPowers(playerPath, out parsedPlayer);
                parsedPlayer.PlayerFrame();
                PlayerFileData loaded = Player.LoadPlayer(playerPath, false); Player p = loaded.Player;
                Check(p != null && p.loadStatus == StatusID.Ok && p.difficulty == playerMode, "unmodified native Player.LoadPlayer retains identity " + Path.GetFileName(manifest) + " status=" + p?.loadStatus + " difficulty=" + p?.difficulty);
                Check(p.inventory[12].type == ItemID.Torch && p.inventory[12].stack == Int32.Parse(rows[6]), "saved item progress matches the writer's actual inventory manifest");
                Check(p.inventory[10].type == Int32.Parse(rows[8]) && p.inventory[10].stack == Int32.Parse(rows[9]), "saved inventory slot matches the writer's recorded item and stack");
                if (rows.Length > 10) Check(p.name == rows[10], "saved player name matches the writer's manifest");
                Check(Convert.ToBase64String(CaptureTracker(p)) == rows[7], "saved native research tracker matches the writer's actual tracker manifest");
                byte[] expectedPlayer = Convert.FromBase64String(rows[5]);
                // LoadPlayer stores these three values on the loaded Player;
                // non-Journey native loading resets them to native defaults.
                // Exact output bytes below independently verify both modes.
                Check(rawPlayerPowers.SequenceEqual(expectedPlayer), "encrypted player creative substream is the original baseline");
                WorldFileData metadata = WorldFile.GetAllMetadata(worldPath, false);
                Check(metadata != null && metadata.LoadStatus == StatusID.Ok && metadata.GameMode == worldMode, "unmodified native world metadata retains base GameMode");
                using (var stream = File.OpenRead(worldPath)) using (var reader = new BinaryReader(stream))
                {
                    bool[] important; int[] offsets;
                    Check(WorldFile.LoadFileFormatHeader(reader, out important, out offsets) && offsets.Length == 11, "native world section table parses version326");
                    stream.Position = offsets[9];
                    CreativePowerManager.Instance.LoadFromWorld(reader, 326);
                    Check(CaptureWorld().SequenceEqual(Convert.FromBase64String(rows[4])), "native LoadFromWorld receives original persistent powers");
                }
                Console.WriteLine("JOURNEY-READ " + Path.GetFileName(manifest) + " player=" + playerMode + " world=" + worldMode + " item=" + p.inventory[12].stack);
            }
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("JueMingR.", StringComparison.Ordinal)), "reader completed without loading R or recovery code");
            return 0;
        }

        private static void Prepare(object context)
        {
            NativeCombatAimChecks.Initialize();
            CreativeItemSacrificesCatalog.Instance.Initialize();
            Main.mapEnabled = false; Main.WorldFileMetadata = FileMetadata.FromCurrentSettings(FileType.World);
            WorldGen.Manifest = new Terraria.WorldBuilding.WorldManifest();
            Main.BestiaryTracker = new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
            typeof(Main).GetField("_achievements", Flags).SetValue(Main.instance, new Terraria.Achievements.AchievementManager());
            Main.ActivePlayerFileData.Metadata = FileMetadata.FromCurrentSettings(FileType.Player);
            Main.ActiveWorldFileData.SetSeed("journey-isolated"); Main.ActiveWorldFileData.WorldId = 151818;
            Main.ActiveWorldFileData.CreationTime = DateTime.UtcNow;
            Main.worldName = "Journey isolated"; Main.worldSurface = 45; Main.rockLayer = 70;
            Main.spawnTileX = Main.spawnTileY = 40; Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
            Main.dayTime = true; Main.time = 1000; Main.rainTime = 600; Main.maxRaining = 0;
            WorldGen.isGeneratingOrLoadingWorld = false;
            if (!saveTraceInstalled)
            {
                // Bounded original section receipts retain the exact first
                // failing native serializer when JIT inlining erases its name.
                var trace = new Harmony("NativeJourneyProbe.WorldSaveSections");
                foreach (string name in new[] { "SaveFileFormatHeader", "SaveWorldHeader", "SaveWorldTiles", "SaveChests", "SaveSigns", "SaveNPCs", "SaveTileEntities", "SaveWeightedPressurePlates", "SaveTownManager", "SaveBestiary", "SaveCreativePowers", "SaveFooter" })
                    trace.Patch(typeof(WorldFile).GetMethod(name, Flags), prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(TraceSaveSection)));
                saveTraceInstalled = true;
            }
            object journey = Get(context, "Journey");
            Check(journey != null && GetOptional(journey, "SetupError") == null, "all exact Journey patches install: " + GetOptional(journey, "SetupError"));
            Check(!(bool)Get(journey, "IsActive"), "new process defaults OFF");
            Call(context, "UpdateRuntime");
        }
        private static void TraceSaveSection(MethodBase __originalMethod) { Console.WriteLine("JOURNEY-WORLD-SAVE section=" + __originalMethod.Name); }

        private static void ConsumerChecks(object context)
        {
            object journey = Get(context, "Journey"); Player p = Main.LocalPlayer;
            Check((bool)Call(journey, "SetEnabled", true), "real menu activation for consumers: " + GetOptional(journey, "LastFailure"));
            ResearchChecks(p);
            Check((bool)Call(journey, "CloseMenu") && (bool)Get(journey, "IsActive") && !Main.CreativeMenu.Enabled, "closing native window retains Active powers");
            var freeze = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
            Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), 1f); freeze.SetPowerInfo(false); Main.UpdateTimeRate();
            Check(Main.dayRate == 24 && WorldGen.GetWorldUpdateRate() == 24, "original time/world-update consumers run at chosen rate");
            freeze.SetPowerInfo(true); Main.UpdateTimeRate(); Check(Main.dayRate == 0 && WorldGen.GetWorldUpdateRate() == 0, "original time and world-tile consumers freeze");
            freeze.SetPowerInfo(false); Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), 0f);
            foreach (var entry in new[] { Tuple.Create(typeof(CreativePowers.StartDayImmediately), 0d, true), Tuple.Create(typeof(CreativePowers.StartNoonImmediately), 27000d, true), Tuple.Create(typeof(CreativePowers.StartNightImmediately), 0d, false), Tuple.Create(typeof(CreativePowers.StartMidnightImmediately), 16200d, false) })
            {
                object power = typeof(CreativePowerManager).GetMethod("GetPower").MakeGenericMethod(entry.Item1).Invoke(CreativePowerManager.Instance, null);
                Call(power, "UsePower"); Check(Main.dayTime == entry.Item3 && Main.time == entry.Item2, "native instant time command " + entry.Item1.Name);
            }
            Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyWindDirectionAndStrength>(), .75f);
            Check(Math.Abs(Main.windSpeedTarget - .4f) < .0001f && Main.windSpeedCurrent == Main.windSpeedTarget, "native wind command changes actual weather values");
            Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyRainPower>(), .7f);
            Check(Main.raining && Math.Abs(Main.maxRaining - .7f) < .0001f, "native rain strength command starts real rain");
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeRainPower>().SetPowerInfo(true); Main.dayRate = 1; Main.time = 1000; Main.dayTime = true; Main.rainTime = 600;
            RainConsumerAtEventBoundary();
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeWindDirectionAndStrength>().SetPowerInfo(true); Main.windCounter = 0;
            Main.instance.UpdateWeather(new GameTime(), 1); Check(Main.windCounter == 0, "original UpdateWeather consumes wind freeze");
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeWindDirectionAndStrength>().SetPowerInfo(false); Main.instance.UpdateWeather(new GameTime(), 1); Check(Main.windCounter > 0, "original wind randomization resumes");
            var rate = CreativePowerManager.Instance.GetPower<CreativePowers.SpawnRateSliderPerPlayerPower>();
            Type spawnerType = typeof(NPC).GetNestedType("Spawner", Flags); object spawner = Activator.CreateInstance(spawnerType, true);
            rate.PushChangeAndSetSlider(.5f); int[] normal = SpawnRate(spawner, p);
            rate.PushChangeAndSetSlider(1f); int[] high = SpawnRate(spawner, p);
            Check(high[0] < normal[0] && high[1] > normal[1], "original spawner consumes default/high spawn multiplier");
            rate.PushChangeAndSetSlider(0f); Check(!(bool)CallStatic(spawnerType, "CanSpawnEnemiesNear", p), "original spawner rejects zero-rate enemies"); rate.PushChangeAndSetSlider(.5f);
            var difficulty = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
            Shared(difficulty, .33f); CallStatic(typeof(Main), "UpdateCreativeGameModeOverride"); var normalNpc = new NPC(); normalNpc.SetDefaults(NPCID.Zombie);
            Shared(difficulty, 1f); CallStatic(typeof(Main), "UpdateCreativeGameModeOverride"); var masterNpc = new NPC(); masterNpc.SetDefaults(NPCID.Zombie);
            Check(Main.GameMode == 0 && Main.Difficulty == 3 && Main.expertMode && Main.masterMode && masterNpc.lifeMax > normalNpc.lifeMax && masterNpc.damage > normalNpc.damage, "native effective difficulty/actual NPC life and damage change without base mode mutation");
            var range = CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>(); range.SetEnabledState(0, false); p.ResetEffects(); int x = Player.tileRangeX, y = Player.tileRangeY, grab = p.GetItemGrabRange(new Item(ItemID.Torch));
            range.SetEnabledState(0, true); p.ResetEffects(); Check(Player.tileRangeX == x * 2 + 8 && Player.tileRangeY == y * 2 + 8 && p.GetItemGrabRange(new Item(ItemID.Torch)) == grab + 240, "native placement and pickup consumers use range ability");
            Check(Terraria.Social.SocialAPI.Achievements == null && !(bool)Get(Main.Achievements, "_isCloudSave") && IsWithin((string)Get(Main.Achievements, "_savePath"), Terraria.Program.SavePath),
                "original achievement startup reads and writes only this isolated local save root");
            Console.WriteLine("JOURNEY native achievement definitions before startup=" + Main.Achievements.CreateAchievementsList().Count);
            Terraria.Initializers.AchievementInitializer.Load();
            var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>(); god.SetEnabledState(0, true);
            p.statLife = p.statLifeMax = p.statLifeMax2 = 500; p.position = new Vector2(640, 640); p.fallStart = p.fallStart2 = 40; p.velocity = Vector2.Zero;
            NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
            Check(p.creativeGodMode && p.Hurt(PlayerDeathReason.ByCustomReason("isolated Journey damage"), 40, 1, quiet: true, dodgeable: false) == 0 && p.statLife == 500, "actual Player.Update-to-Hurt rejects real damage");
            god.SetEnabledState(0, false); NativeQuickItemChecks.BeginWorldStep(); p.Update(0); p.immune = false; p.immuneTime = 0;
            Check(!p.creativeGodMode && p.Hurt(PlayerDeathReason.ByCustomReason("isolated Journey normal damage"), 40, 1, quiet: true, dodgeable: false) > 0, "original damage resumes after godmode toggle");
            InfectionConsumer();
            Check((bool)Call(journey, "SetEnabled", false) && Main.GameMode == 0 && p.difficulty == 0 && Main.Difficulty == 1, "complete close restores original identity/effective difficulty");
            Console.WriteLine("PASS JOURNEY original consumer categories include native UI closed during effects.");
        }

        private static void VerticalSave(object context, string content, string output)
        {
            object journey = Get(context, "Journey");
            Item copy = Main.LocalPlayer.inventory[10]; int remaining = copy.stack;
            Check(copy.type == ItemID.DirtBlock && remaining > 0, "vertical save retains the actual native UI copy after its original placement consumer");
            ConfigureFiles(0, 0, "vertical"); Call(context, "UpdateRuntime");
            byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
            Check((bool)Call(journey, "SetEnabled", true), "vertical original-save scenario enters real Active state");
            try
            {
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
                Main.UpdateTimeRate();
                Check(Main.dayRate == 0 && WorldGen.GetWorldUpdateRate() == 0, "vertical Active freeze reaches original time and world-update consumers before saving");
                Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 17;
                SaveCurrent(output, "vertical", 0, 0, world, player);
                ReadInNewProcess(content, output);
                Check((bool)Call(journey, "SetEnabled", false) && ReferenceEquals(Main.LocalPlayer.inventory[10], copy) && copy.stack == remaining,
                    "closing after independent native read returns borrowed powers without rolling back the actual copied-and-used inventory item");
                Console.WriteLine("PASS JOURNEY vertical native UI copy -> actual placement -> real Active freeze consumers -> original player/world save -> fresh no-R native read; copy progress survives close.");
            }
            finally { Call(journey, "SetEnabled", false); }
        }

        private static void ResearchChecks(Player player)
        {
            ItemsSacrificedUnlocksTracker tracker = player.creativeTracker.ItemSacrifices;
            byte[] before = CaptureTracker(player); int edit = tracker.LastEditId;
            var items = new List<int>(); tracker.ForEachItemWithResearchProgress(items.Add);
            var expected = new HashSet<int>();
            foreach (var row in CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId)
            {
                int id, cap; if (!ContentSamples.CreativeResearchItemPersistentIdOverride.TryGetValue(row.Key, out id)) id = row.Key;
                if (CreativeItemSacrificesCatalog.Instance.TryGetSacrificeCountCapToUnlockInfiniteItems(id, out cap) && cap > 0) expected.Add(id);
            }
            Check(expected.Count > 0 && expected.SetEquals(items) && items.Distinct().Count() == items.Count, "virtual native directory exactly matches unique native positive-cap research set");
            foreach (int id in items)
            {
                int have, cap, nativeCap; Check(tracker.TryGetSacrificeNumbers(id, out have, out cap) && CreativeItemSacrificesCatalog.Instance.TryGetSacrificeCountCapToUnlockInfiniteItems(id, out nativeCap) && cap > 0 && cap == nativeCap && have == cap, "virtual directory entry has its native positive cap " + id);
            }
            int amount, needed;
            Check(!tracker.TryGetSacrificeNumbers(-1, out amount, out needed) && !tracker.TryGetSacrificeNumbers(ItemID.Count + 1, out amount, out needed), "internal/out-of-range IDs never gain fake research");
            for (int i = 0; i < 100; i++) Check(tracker.IsFullyResearched(ItemID.DirtBlock), "stable native query remains complete");
            var item = new Item(ItemID.DirtBlock); item.stack = 10; int sacrificed;
            Check(Main.CreativeMenu.SacrificeItem(ref item, out sacrificed) == CreativeUI.ItemSacrificeResult.CannotSacrifice && item.stack == 10 && sacrificed == 0, "native research submit cannot consume or persist virtual progress");
            Check(tracker.LastEditId == edit && CaptureTracker(player).SequenceEqual(before), "100 stable virtual queries and refused research leave real tracker/version untouched");
        }

        private sealed class TimeConsumerBoundary : Exception { }
        private static void RainConsumerAtEventBoundary()
        {
            // In locked .8 Main.UpdateTime, all original rain conditions,
            // countdown and UpdateTimeRate precede this first unrelated event.
            // Stop only at that event entry; no rain instruction is replaced.
            // This proves the rain consumer, not an entire world/event tick.
            // The tiny general-purpose world uses surface 45. Native .8
            // explicitly requires >50 for a surface, so give this weather
            // sample a real surface within its existing rock layer (70).
            double previousSurface = Main.worldSurface; Main.worldSurface = 55;
            var h = new Harmony("NativeJourneyChecks.RainConsumerBoundary");
            var target = typeof(Terraria.GameContent.Events.CultistRitual).GetMethod("UpdateTime", Flags);
            h.Patch(target, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(StopAtTimeConsumerBoundary)));
            try
            {
                Check(Main.raining && Main.netMode == 0 && !Main.gameMenu && (Main.isThereAWorldSurface || Main.remixWorld),
                    "original rain consumer has an actual raining local surface-world input");
                double before = Main.time;
                UpdateTimeThroughRain(); Check(Main.rainTime == 600 && Main.time == before + 1d, "original weather simulation consumes rain freeze and advances actual time before the first unrelated world event");
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeRainPower>().SetPowerInfo(false);
                UpdateTimeThroughRain(); Check(Main.rainTime == 599, "original rain countdown resumes before the first unrelated world event");
                Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), 1f); Main.UpdateTimeRate(); before = Main.time;
                UpdateTimeThroughRain(); Check(Main.time == before + 24d, "original time advancement consumes the selected speed before the first unrelated world event");
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true); Main.UpdateTimeRate(); before = Main.time;
                UpdateTimeThroughRain(); Check(Main.time == before, "original time advancement actually stops under FreezeTime before the first unrelated world event");
            }
            finally
            {
                h.Unpatch(target, HarmonyPatchType.Prefix, h.Id);
                Main.worldSurface = previousSurface;
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(false);
                Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), 0f); Main.UpdateTimeRate();
            }
            Console.WriteLine("PASS JOURNEY original rain freeze/resume and actual time speed/freeze through the first event boundary; complete Main/event tick not claimed.");
        }
        private static void StopAtTimeConsumerBoundary() { throw new TimeConsumerBoundary(); }
        private static void UpdateTimeThroughRain()
        {
            bool reached = false;
            try { CallStatic(typeof(Main), "UpdateTime"); }
            catch (TargetInvocationException error) when (error.InnerException is TimeConsumerBoundary) { reached = true; }
            Check(reached, "unmodified original rain logic reaches the scoped first-event boundary");
        }

        private static void UseDuplicatedItem(object context)
        {
            Player p = Main.LocalPlayer;
            Check(p.inventory[10].type == ItemID.DirtBlock && p.inventory[10].stack > 1, "placement input is the actual preceding UI duplication result");
            int count = p.inventory[10].stack;
            Call(Get(Get(context, "Shell"), "State"), "Close"); Main.playerInventory = false; Main.CurrentInputTextTakerOverride = null;
            Main.tile[42, 42].ClearEverything(); NativeToolsChecks.Tile(42, 43, TileID.Stone);
            p.position = new Vector2(640, 646); p.velocity = Vector2.Zero; p.itemAnimation = p.itemTime = 0; p.releaseUseItem = true;
            p.selectedItemState.Select(10); p.selectedItemState.Update();
            for (int frame = 0; frame < 20 && !Main.tile[42, 42].active(); frame++)
            {
                p.mouseInterface = p.lastMouseInterface = false;
                NativeToolExecutionChecks.Sample(context, Get(context, "Input"), new Vector2(680, 680), true);
                Player.tileTargetX = Player.tileTargetY = 42;
                NativeQuickItemChecks.NativeFrame(p);
            }
            Check(Main.tile[42, 42].active() && Main.tile[42, 42].type == TileID.Dirt && p.inventory[10].stack == count - 1, "copied item is really consumed by native ItemCheck to place one world tile");
            NativeToolExecutionChecks.Sample(context, Get(context, "Input"), new Vector2(680, 680), false);
            p.controlUseItem = false; p.selectedItemState.Select(0); p.selectedItemState.Update(); p.itemAnimation = p.itemTime = 0;
        }

        private static void InfectionConsumer()
        {
            // Stop immediately after the real world-update permission consumer,
            // before unrelated whole-world event and tile update work begins.
            var h = new Harmony("JueMingR.Tests.JourneyInfectionBoundary"); var method = typeof(Wiring).GetMethod("UpdateMech", Flags);
            h.Patch(method, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(EndInfectionPrefix)));
            try
            {
                foreach (bool value in new[] { false, true })
                {
                    CreativePowerManager.Instance.GetPower<CreativePowers.StopBiomeSpreadPower>().SetPowerInfo(value);
                    try { WorldGen.UpdateWorld(); throw new InvalidOperationException("infection boundary not reached"); } catch (InfectionBoundary) { }
                    Check(WorldGen.AllowedToSpreadInfections == !value, "actual native world-update infection permission " + value);
                }
            }
            finally { h.Unpatch(method, HarmonyPatchType.All, h.Id); }
        }
        private sealed class InfectionBoundary : Exception { }
        private static bool EndInfectionPrefix() { throw new InfectionBoundary(); }

        private static void SaveMatrix(object context, string output)
        {
            object journey = Get(context, "Journey");
            foreach (int mode in new[] { 0, 1, 2, 3 })
            {
                Check((bool)Call(journey, "SetEnabled", false), "matrix starts with restored session");
                ConfigureFiles(mode, mode, "mode" + mode); CreativePowerManager.Instance.Reset();
                Main.LocalPlayer.creativeTracker.ItemSacrifices.RegisterItemSacrifice(ItemID.DirtBlock, 1);
                if (mode == 3) { CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true); Shared(CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>(), .665f); }
                byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer); float before = Main.Difficulty;
                Call(context, "UpdateRuntime");
                Check((bool)Call(journey, "SetEnabled", true), "matrix native menu enabled " + mode + ": " + GetOptional(journey, "LastFailure"));
                Check(Main.LocalPlayer.difficulty == mode && Main.GameMode == mode && (mode == 3 || Main.Difficulty == before), "neutral activation preserves current world behavior " + mode);
                Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), .8f);
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(mode != 3);
                CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(0, true);
                Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 20 + mode;
                SaveCurrent(output, "mode" + mode, mode, mode, world, player);
                Check((bool)Get(journey, "IsActive"), "saving never silently disables ability " + mode);
                Check((bool)Call(journey, "SetEnabled", false) && CaptureWorld().SequenceEqual(world) && CapturePlayer(Main.LocalPlayer).SequenceEqual(player), "restoration preserves native preexisting powers " + mode);
            }
        }

        private static void SaveInterleave(object context, string output)
        {
            ConfigureFiles(0, 1, "interleave"); CreativePowerManager.Instance.Reset(); Call(context, "UpdateRuntime");
            object journey = Get(context, "Journey"); byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
            Check((bool)Call(journey, "SetEnabled", true), "interleaved save begins Active"); Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 31;
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
            var h = new Harmony("JueMingR.Tests.JourneySaveInterleave"); var target = typeof(WorldFile).GetMethod("SaveCreativePowers", Flags);
            saveEntered = new ManualResetEvent(false); saveContinue = new ManualResetEvent(false); pauseWorldSave = true;
            h.Patch(target, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(PauseSave)));
            Exception error = null;
            var thread = new Thread(() => { try { WorldFile.SaveWorld(); } catch (Exception e) { error = e; } });
            try
            {
                thread.Start(); Check(saveEntered.WaitOne(15000), "real background serializer reached creative output after SaveView capture");
                Check((bool)Call(journey, "SetEnabled", false), "close while native save is in flight restores runtime values");
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
                byte[] nextWorldBaseline = CaptureWorld();
                Check(!nextWorldBaseline.SequenceEqual(world) && (bool)Call(journey, "SetEnabled", true), "a new Active session captures a distinct original baseline B while old save owns A");
                saveContinue.Set(); Check(thread.Join(15000) && error == null, "old background serializer completes after session ended: " + error);
                Check((bool)Get(journey, "IsActive") && CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().Enabled, "old save completion neither retires new Active nor borrows its baseline");
                Player.SavePlayer(Main.ActivePlayerFileData, skipMapSave: true);
                WriteManifest(output, "interleave", 0, 1, world, player);
                Check((bool)Call(journey, "SetEnabled", false) && CaptureWorld().SequenceEqual(nextWorldBaseline), "closing new session returns B independently of the old immutable A save");
            }
            finally { pauseWorldSave = false; saveContinue.Set(); thread.Join(15000); h.Unpatch(target, HarmonyPatchType.All, h.Id); saveEntered.Dispose(); saveContinue.Dispose(); }
        }

        private static void RenameWhileActive(object context, string output)
        {
            ConfigureFiles(0, 1, "active-rename"); CreativePowerManager.Instance.Reset(); Call(context, "UpdateRuntime");
            object journey = Get(context, "Journey"); byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
            Check((bool)Call(journey, "SetEnabled", true), "native rename scenario starts Active");
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
            CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(0, true);
            Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 29;
            PlayerFileData file = Main.ActivePlayerFileData; string path = file.Path;
            file.Rename("Journey renamed");
            Check(ReferenceEquals(Main.ActivePlayerFileData, file) && file.Path == path && Main.LocalPlayer.name == "Journey renamed" && (bool)Get(journey, "IsActive"), "real PlayerFileData.Rename keeps the same native file/session and writes its changed name");
            WorldFile.SaveWorld(); WriteManifest(output, "active-rename", 0, 1, world, player);
            Check((bool)Call(journey, "SetEnabled", false), "native rename scenario restores all borrowed powers");
        }
        private sealed class SaveBoundaryFailure : Exception { }
        private static void SaveException(object context)
        {
            object journey = Get(context, "Journey"); Check((bool)Call(journey, "SetEnabled", true), "exception scope starts active");
            byte[] before = File.ReadAllBytes(Main.ActiveWorldFileData.Path);
            byte[] baseline = CaptureWorld(); CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(!CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().Enabled);
            var h = new Harmony("JueMingR.Tests.JourneySaveException"); var target = typeof(WorldFile).GetMethod("SaveCreativePowers", Flags);
            h.Patch(target, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(PauseSave))); failWorldSave = true;
            try
            {
                bool thrown = false;
                try { using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) WorldFile.SaveWorld_Version2(writer); }
                catch (SaveBoundaryFailure) { thrown = true; }
                Check(thrown && File.ReadAllBytes(Main.ActiveWorldFileData.Path).SequenceEqual(before), "native serializer exception is preserved before any file replacement");
                Check(!CaptureWorld().SequenceEqual(baseline), "exception Finalizer releases save view; later ordinary power capture sees live values");
            }
            finally { failWorldSave = false; h.Unpatch(target, HarmonyPatchType.All, h.Id); Call(journey, "SetEnabled", false); }
        }
        private static void PauseSave()
        {
            if (insideAdmissionNestedSave || (admissionSaveThreadId != 0 && admissionSaveThreadId != Thread.CurrentThread.ManagedThreadId)) return;
            savePauseAction?.Invoke();
            if (failWorldSave) throw new SaveBoundaryFailure();
            if (pauseWorldSave) { saveEntered.Set(); if (!saveContinue.WaitOne(15000)) throw new TimeoutException("Journey save interleave timeout"); }
        }

        private static void AdmissionAndReset(object context, string output)
        {
            object journey = Get(context, "Journey"); PlayerFileData old = Main.ActivePlayerFileData;
            Main.ActivePlayerFileData = new PlayerFileData("test-only-cloud.plr", true) { Player = Main.LocalPlayer };
            Check(!(bool)Call(journey, "SetEnabled", true) && !(bool)Get(journey, "IsActive"), "unverified cloud path rejected before runtime side effects"); Main.ActivePlayerFileData = old;
            Check((bool)Call(journey, "SetEnabled", true), "reset boundary starts active");
            CreativePowerManager.Instance.Reset(); Check(!(bool)Get(journey, "IsActive"), "native world reset retires old owner before replacing singleton state");
            Call(context, "UpdateRuntime"); Check(!CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().Enabled, "later tick does not write old world powers into reset singleton");
            ConfigureFiles(0, 1, "player-reset"); Call(context, "UpdateRuntime");
            byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
            Check((bool)Call(journey, "SetEnabled", true), "per-player reset scenario starts Active");
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
            Shared(CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(), .8f);
            // Original Deserialize (56364) creates this native saved-field
            // carrier immediately before ResetDataForNewPlayer. A bare Player
            // constructor does not provide the reset method's actual input.
            var newPlayer = new Player { savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules() };
            CreativePowerManager.Instance.ResetDataForNewPlayer(newPlayer);
            Check(!(bool)Get(journey, "IsActive") && CaptureWorld().SequenceEqual(world), "actual per-player native reset returns borrowed world powers before retiring the old owner");
            Main.LocalPlayer.inventory[12].SetDefaults(ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 37;
            SaveCurrent(output, "player-reset", 0, 1, world, player);

            // Give the old lifetime a distinguishable true baseline. Full
            // native Reset owns the new false default even if closing the old
            // menu fails; a later session-end callback must not restore true.
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
            Check((bool)Call(journey, "SetEnabled", true), "full-reset close-failure scenario starts Active with a distinct old world baseline");
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(false);
            nativeMenuCloseFailures = 0;
            var failure = new Harmony("NativeJourneyChecks.FullResetCloseFailure");
            var toggle = typeof(CreativeUI).GetMethod("ToggleMenu", Flags);
            failure.Patch(toggle, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(ThrowNativeMenuClose)));
            try { CreativePowerManager.Instance.Reset(); }
            finally { failure.Unpatch(toggle, HarmonyPatchType.Prefix, failure.Id); }
            Check(nativeMenuCloseFailures > 0, "full reset actually encounters the controlled original menu-close exception");
            Check(!(bool)Get(journey, "IsActive") && GetOptional(journey, "Persistence") == null &&
                !CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().Enabled,
                "full native reset retires persistent ownership despite an actual native menu-close exception");
            Call(journey, "OnSessionEnded");
            Check(!CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().Enabled && GetOptional(journey, "Persistence") == null,
                "later real session-end callback cannot restore the retired pre-reset world baseline");
            if (Main.CreativeMenu.Enabled) Main.CreativeMenu.ToggleMenu();
        }

        private static void ThrowNativeMenuClose() { nativeMenuCloseFailures++; throw new IOException("owned full-reset native menu-close failure"); }

        private static void ConfigureFiles(int playerMode, int worldMode, string name)
        {
            string root = Path.Combine(Terraria.Program.SavePath, "composition"); Directory.CreateDirectory(root);
            Player p = Main.LocalPlayer; p.difficulty = (byte)playerMode; p.active = true; p.dead = false; p.name = "Journey " + name; p.itemAnimation = p.itemTime = 0;
            Main.ActivePlayerFileData = new PlayerFileData(Path.Combine(root, name + ".plr"), false) { Player = p, Metadata = FileMetadata.FromCurrentSettings(FileType.Player) };
            Main.ActiveWorldFileData = new WorldFileData(Path.Combine(root, name + ".wld"), false) { UniqueId = Guid.NewGuid(), WorldId = 151818, GameMode = worldMode, CreationTime = DateTime.UtcNow };
            Main.ActiveWorldFileData.SetSeed("journey-isolated"); Main.worldName = "Journey " + name; Main.WorldFileMetadata = FileMetadata.FromCurrentSettings(FileType.World);
            Main.gameMenu = false; Main.netMode = 0; Main.LocalPlayer.chest = -1; Main.LocalPlayer.SetTalkNPC(-1); Main.blockInput = false; Main.CurrentInputTextTakerOverride = null;
        }
        private static void SaveCurrent(string output, string name, int playerMode, int worldMode, byte[] world, byte[] player)
        {
            Player.SavePlayer(Main.ActivePlayerFileData, skipMapSave: true); WorldFile.SaveWorld();
            Check(File.Exists(Main.ActivePlayerFileData.Path) && File.Exists(Main.ActiveWorldFileData.Path), "actual native local player and world files written");
            WriteManifest(output, name, playerMode, worldMode, world, player);
        }
        private static void WriteManifest(string output, string name, int playerMode, int worldMode, byte[] world, byte[] player)
        {
            File.WriteAllLines(Path.Combine(output, "journey-save-" + name + ".txt"), new[] { Main.ActivePlayerFileData.Path, Main.ActiveWorldFileData.Path, playerMode.ToString(), worldMode.ToString(), Convert.ToBase64String(world), Convert.ToBase64String(player), Main.LocalPlayer.inventory[12].stack.ToString(), Convert.ToBase64String(CaptureTracker(Main.LocalPlayer)), Main.LocalPlayer.inventory[10].type.ToString(), Main.LocalPlayer.inventory[10].stack.ToString(), Main.LocalPlayer.name });
        }
        private static byte[] ExtractPlayerPowers(string path, out Player parsedPlayer)
        {
            // Deserialize itself owns the exact variable-length player layout.
            // Observe only its native power reader's input offset; no R patch
            // or replacement parser is used in this independent process.
            byte[] result = null; currentPowerRead = bytes => result = bytes; parsedPlayer = null;
            var h = new Harmony("NativeJourneyReader.RecordPowerBytes"); var method = typeof(CreativePowerManager).GetMethod("LoadToPlayer", Flags);
            h.Patch(method, prefix: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(ReadPlayerPowerBytes)));
            try
            {
                // Run original Deserialize first on seekable plaintext so its
                // exceptions retain details that LoadPlayer would swallow. The
                // caller subsequently requires original LoadPlayer acceptance.
                byte[] key = (byte[])typeof(Player).GetField("ENCRYPTION_KEY", Flags).GetValue(null);
                using (var encrypted = File.OpenRead(path)) using (var aes = new RijndaelManaged { Padding = PaddingMode.None })
                using (var crypto = new CryptoStream(encrypted, aes.CreateDecryptor(key, key), CryptoStreamMode.Read))
                using (var plain = new MemoryStream())
                {
                    crypto.CopyTo(plain); plain.Position = 0;
                    using (var reader = new BinaryReader(plain))
                    {
                        int version = reader.ReadInt32(); var file = new PlayerFileData(path, false) { Metadata = FileMetadata.Read(reader, FileType.Player) };
                        parsedPlayer = new Player(); object[] args = { file, parsedPlayer, reader, version, false };
                        typeof(Player).GetMethod("Deserialize", Flags).Invoke(null, args);
                    }
                }
                Check(result != null, "original decrypted Deserialize reached creative record"); return result;
            }
            finally { currentPowerRead = null; h.Unpatch(method, HarmonyPatchType.All, h.Id); }
        }
        private static Action<byte[]> currentPowerRead;
        private static void ReadPlayerPowerBytes(BinaryReader reader)
        {
            long at = reader.BaseStream.Position; var bytes = new List<byte>();
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                while (true)
                {
                    bool more = reader.ReadBoolean(); writer.Write(more); if (!more) break;
                    ushort id = reader.ReadUInt16(); writer.Write(id);
                    if (id == 5 || id == 11) writer.Write(reader.ReadBoolean());
                    else if (id == 14) writer.Write(reader.ReadSingle());
                    else throw new InvalidDataException("Unexpected locked .8 native player power id " + id);
                }
                writer.Flush(); bytes.AddRange(stream.ToArray());
            }
            reader.BaseStream.Position = at; currentPowerRead(bytes.ToArray());
        }
        private static byte[] CaptureWorld() { return Capture(w => CreativePowerManager.Instance.SaveToWorld(w)); }
        private static byte[] CapturePlayer(Player player) { return Capture(w => CreativePowerManager.Instance.SaveToPlayer(player, w)); }
        private static byte[] CaptureTracker(Player player) { return Capture(w => player.creativeTracker.Save(w)); }
        private static byte[] Capture(Action<BinaryWriter> write) { using (var s = new MemoryStream()) { using (var w = new BinaryWriter(s, System.Text.Encoding.UTF8, true)) write(w); return s.ToArray(); } }
        private static void Shared(CreativePowers.ASharedSliderPower power, float value) { using (var s = new MemoryStream()) using (var w = new BinaryWriter(s)) { w.Write(value); w.Flush(); s.Position = 0; power.DeserializeNetMessage(new BinaryReader(s), 0); } }
        private static int[] SpawnRate(object spawner, Player player) { object[] args = { player, 0, 0 }; spawner.GetType().GetMethod("GetSpawnRate", Flags).Invoke(spawner, args); return new[] { (int)args[1], (int)args[2] }; }
        private static object CallStatic(Type type, string name, params object[] args) { return type.GetMethod(name, Flags).Invoke(null, args); }
        private static void Check(bool condition, string reason) { Require(condition, "JOURNEY: " + reason); checks++; }
        private static bool IsWithin(string file, string root) { return Path.GetFullPath(file).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        private static string NewRoot(string output, string name) { string root = Path.Combine(output, name + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Terraria.Program.SavePath = root; return root; }
        private static Process StartChild(string content, string output, string scope)
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            var info = new ProcessStartInfo(exe, Quote(Program.Repository) + " " + Quote(content) + " " + Quote(output) + " " + scope) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Program.Repository };
            return Process.Start(info);
        }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        private static void ReadInNewProcess(string content, string output)
        {
            using (Process child = StartChild(content, output, "JourneyRead"))
            {
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(45000)) { child.Kill(); throw new TimeoutException("Owned native reader timed out"); }
                File.WriteAllText(Path.Combine(output, "reader-" + Guid.NewGuid().ToString("N") + ".log"), stdout.Result + stderr.Result);
                Check(child.ExitCode == 0, "independent native reader succeeded: " + stdout.Result + stderr.Result);
            }
        }
        private static void KillOwnWriter(string content, string output)
        {
            string ready = Path.Combine(output, "kill-ready.txt"); if (File.Exists(ready)) File.Delete(ready);
            using (Process child = StartChild(content, output, "JourneyKillWriter"))
            {
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync(); var clock = Stopwatch.StartNew();
                while (!File.Exists(ready) && !child.HasExited && clock.ElapsedMilliseconds < 45000) Thread.Sleep(20);
                if (!File.Exists(ready)) { if (!child.HasExited) child.Kill(); child.WaitForExit(); throw new Exception("Owned writer failed before Active save: " + stdout.Result + stderr.Result); }
                Check(File.ReadAllText(ready) == child.Id.ToString() && !child.HasExited, "strong termination targets only this exact newly-created writer");
                child.Kill(); child.WaitForExit(); File.WriteAllText(Path.Combine(output, "killed-writer.log"), stdout.Result + stderr.Result);
            }
            ReadInNewProcess(content, output);
        }
    }
}
