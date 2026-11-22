using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Shaders;
using Terraria.GameContent.Skies;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionExitChecks
    {
        private const string ReaderVariable = "JUEMINGR_TEST_COMPANION_EXIT_READER";
        private const string ComparisonVariable = "JUEMINGR_COMPANION_EXIT_COMPARISON";
        private const string FixtureName = "companion-world-exit";
        private const int PotionSlot = 3;
        private const int WeaponSlot = 4;
        private const int ResourceSlot = 5;
        private static readonly string[] ToolKeys = { "拿走全部", "存放全部", "快速堆叠", "补货", "weapon-select", "item-sort", "item-once", "storage-Inventory" };
        private static ManualResetEvent workerEnded;
        private static Exception workerFailure;

        internal static int Run(string content, string output)
        {
            output = Path.GetFullPath(output);
            string candidate = Environment.GetEnvironmentVariable(ComparisonVariable);
            string nonce = Environment.GetEnvironmentVariable(ReaderVariable);
            bool reader = nonce != null;
            if ((reader || candidate != null) && Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT") != null)
                throw new InvalidOperationException("Explicit exit comparisons and re-entry children have no formal scope qualification.");
            nonce = reader ? Guid.ParseExact(nonce, "N").ToString("N") : Guid.NewGuid().ToString("N");
            if (!reader) output = Path.Combine(output, "owned-exit-" + nonce);
            Directory.CreateDirectory(output);
            Console.WriteLine("COMPANION-EXIT fixture=" + output);
            // The original .NET Framework file APIs require room for two
            // GUID components and the archive lock suffix below this root.
            // Keep actual game files in a short invocation-owned workspace
            // directory even when the formal evidence path is already deep.
            Terraria.Program.SavePath = Path.Combine(Program.Repository, ".local", "jexit", nonce.Substring(0, 16));
            Directory.CreateDirectory(Terraria.Program.SavePath);
            NativeTestAudio.Install(typeof(Main).Assembly);
            string request = Path.Combine(output, "exit-writer.txt");
            if (reader)
            {
                var receipt = File.ReadAllLines(request);
                Require(receipt.Length == 6 && receipt[0] == nonce &&
                    Int32.Parse(receipt[1], CultureInfo.InvariantCulture) != Process.GetCurrentProcess().Id,
                    "re-entry is a fresh process for the exact owned world-exit invocation");
                NativeQuickItemChecks.Run(context =>
                {
                    using (var graphics = new ProbeGraphics(content)) ReadAndReenter(context, graphics, output, receipt);
                    Main.gameMenu = true;
                    Call(context, "UpdateRuntime");
                    ((IDisposable)Get(context, "Companions")).Dispose();
                }, processing: true, shortFeedback: true, candidateAssembly: candidate);
                return 0;
            }

            NativeQuickItemChecks.Run(context =>
            {
                using (var graphics = new ProbeGraphics(content)) WriteAndExit(context, graphics, output, request, nonce);
                // Release this process's actual document locks before fresh
                // composition opens the very same saved bytes in the child.
                ((IDisposable)Get(context, "Companions")).Dispose();
            }, processing: true, shortFeedback: true, candidateAssembly: candidate);
            RunReentry(content, output, nonce);
            Console.WriteLine("PASS companion genuine SaveAndQuit and file reload through a fresh production composition, UI activation and native QuickHeal; no installed client, physical OS input, or Steam acceptance.");
            return 0;
        }

        private static void PrepareOriginal(object context, ProbeGraphics graphics)
        {
            NativeJourneyChecks.Prepare(context);
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"), HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            LanguageManager.Instance.SetLanguage("zh-Hans");
            Lang.InitializeLegacyLocalization();
            graphics.LoadCompanionAssets();
            Main.worldSurface = 60;
            Main.rockLayer = 90;
            Main.spawnTileX = 60;
            Main.spawnTileY = 58;
            for (int x = 2; x < 118; x++)
            {
                Main.tile[x, 60].active(true);
                Main.tile[x, 60].type = TileID.Stone;
            }
            for (int i = 0; i < Main.maxRain; i++) if (Main.rain[i] == null) Main.rain[i] = new Rain();
        }

        private static void WriteAndExit(object context, ProbeGraphics graphics, string output, string request, string nonce)
        {
            PrepareOriginal(context, graphics);
            NativeJourneyChecks.ConfigureFiles(0, 0, FixtureName);
            var human = Main.LocalPlayer;
            human.name = "验证主人";
            human.statLife = human.statLifeMax = human.statLifeMax2 = 120;
            human.inventory[25].SetDefaults(ItemID.Torch);
            human.inventory[25].stack = 17;
            var host = Get(context, "Companions");
            var archive = Get(host, "profiles");
            WaitForRoster(context, host);
            Require((bool)Call(host, "Create", "队友档案样本"), "writer creates an actual companion profile");
            var id = (Guid)Get(host, "LastCreatedId");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive, "IsSaved", id); });
            Require((bool)Call(host, "Join", id), "writer deploys the original native body");
            var body = (Player)Call(host, "GetBody", id);
            var profile = NativeCompanionConfigurationChecks.Profile(host, id);
            var settings = profile.Behavior;
            settings.Follow = settings.AttackEnabled = settings.HealthRecoveryEnabled = settings.AllowNurse = false;
            body.inventory[PotionSlot].SetDefaults(ItemID.HealingPotion);
            body.inventory[PotionSlot].stack = 2;
            body.inventory[WeaponSlot].SetDefaults(ItemID.CopperBroadsword);
            settings.MainWeapons = new[] { new CompanionWeaponPreference(ItemID.CopperBroadsword, body.inventory[WeaponSlot].prefix, WeaponSlot) };
            Require((bool)Call(host, "SetBehavior", id, settings), "writer captures the actual main weapon preference and native bag");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive, "IsSaved", id); });
            profile = NativeCompanionConfigurationChecks.Profile(host, id);

            body.statLife = 49;
            CheckToolbar(context, graphics, host, id, false, output, "before-exit-owner-alive");
            // A real classic player death is observed by the public shell;
            // this fixture keeps the original gear and skill lifetimes.
            KillHuman(human);
            CheckToolbar(context, graphics, host, id, true, output, "before-exit-owner-dead");
            // A last unsaved resource and a native tile differentiate ordinary
            // exit capture from reading an older file or assigning WorldFileData.
            body.inventory[ResourceSlot].SetDefaults(ItemID.Wood);
            body.inventory[ResourceSlot].stack = 37;
            Main.tile[44, 54].active(true);
            Main.tile[44, 54].type = TileID.Stone;
            Require((bool)Call(host, "IsJoined", id) && body.active && !body.dead, "companion stays deployed and alive before SaveAndQuit");
            PrepareQuitVisuals(graphics);
            SaveAndQuit(context);
            Require(Main.gameMenu && !body.active && !(bool)Call(host, "ArchiveIdentityCurrent"),
                "real world exit and runtime menu edge detach the deployed body and retire its old UI/archive admission");
            NativeQuickItemChecks.Until(() =>
            {
                // The menu does not run gameplay feature updates. Pump the
                // existing archive completion without opening a new session.
                Call(archive, "Pump");
                Require(!(bool)Call(archive, "SaveFailed", id), "real companion exit archive commits: " + Call(archive, "SaveError", id));
                return (bool)Call(archive, "IsSaved", id);
            });
            Require(File.Exists(Main.ActivePlayerFileData.Path) && File.Exists(Main.ActiveWorldFileData.Path),
                "original SaveAndQuit worker wrote final isolated .plr and .wld");
            File.WriteAllLines(request, new[] { nonce, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture), id.ToString("N"),
                profile.OwnerId.ToString("N"), profile.Revision.ToString(CultureInfo.InvariantCulture), Main.ActiveWorldFileData.UniqueId.ToString("N") });
        }

        private static void PrepareQuitVisuals(ProbeGraphics graphics)
        {
            graphics.LoadScreenEffects();
            // The short client startup did not run ScreenEffectInitializer.
            // Its queued menu cleanup directly requires these original entries
            // even in a plain world where all the effects are inactive.
            Filters.Scene["BloodMoon"] = new Filter(new BloodMoonScreenShaderData("FilterBloodMoon").UseColor(2f, -.8f, -.6f), EffectPriority.Medium);
            Filters.Scene["Graveyard"] = new Filter(new ScreenShaderData("FilterGraveyard"), EffectPriority.Medium);
            Filters.Scene["Sepia"] = new Filter(new SepiaScreenShaderData("FilterSepia").UseImage("Images/DSTNoise").UseIntensity(1f), EffectPriority.Medium);
            Filters.Scene["Noir"] = new Filter(new ScreenShaderData("FilterNoir").UseImage("Images/NoirNoise", 0, Microsoft.Xna.Framework.Graphics.SamplerState.PointWrap).UseIntensity(1f), EffectPriority.Medium);
            Filters.Scene["MoonLordShake"] = new Filter(new MoonLordScreenShaderData("FilterMoonLordShake", aimAtPlayer: false), EffectPriority.VeryHigh);
            Filters.Scene["WaterDistortion"] = new Filter(new WaterShaderData("FilterWaterDistortion").UseIntensity(1f).UseImage("Images/Misc/noise"), EffectPriority.VeryHigh);
            Filters.Scene["CRT"] = new Filter(new ScreenShaderData("FilterCRT"), EffectPriority.VeryHigh);
            Filters.Scene["Test2"] = new Filter(new ScreenShaderData("FilterTest2"), EffectPriority.VeryHigh);
            SkyManager.Instance["Martian"] = new MartianSky();
            SkyManager.Instance["Party"] = new PartySky();
            SkyManager.Instance["Slime"] = new SlimeSky();
            SkyManager.Instance["Ambience"] = new AmbientSky();
            SkyManager.Instance["Aurora"] = new AuroraSky();
        }

        private static void SaveAndQuit(object context)
        {
            var observer = new Harmony("JueMingR.Tests.CompanionOwnedQuit");
            var worker = AccessTools.Method(typeof(WorldGen), "SaveAndQuitCallBack");
            workerEnded = new ManualResetEvent(false);
            workerFailure = null;
            observer.Patch(worker, finalizer: new HarmonyMethod(typeof(NativeCompanionExitChecks), nameof(ObserveQuitWorker)));
            try
            {
                bool returned = false;
                WorldGen.SaveAndQuit(() => returned = true);
                Call(context, "UpdateRuntime");
                Call(context, "UpdateShell");
                Require(workerEnded.WaitOne(20000), "original SaveAndQuit worker reaches its completion boundary");
                if (workerFailure != null) throw new InvalidOperationException("Original SaveAndQuit worker failed", workerFailure);
                AccessTools.Method(typeof(Main), "ConsumeAllMainThreadActions").Invoke(null, null);
                Require(returned && Main.menuMode == 0, "original deferred main-thread SaveAndQuit callback returned to the menu");
            }
            finally
            {
                observer.Unpatch(worker, HarmonyPatchType.All, observer.Id);
                if (workerEnded.WaitOne(0)) workerEnded.Dispose();
            }
        }

        private static Exception ObserveQuitWorker(Exception __exception)
        { workerFailure = __exception; workerEnded.Set(); return null; }

        private static void ReadAndReenter(object context, ProbeGraphics graphics, string output, string[] receipt)
        {
            PrepareOriginal(context, graphics);
            var host = Get(context, "Companions");
            Main.gameMenu = true;
            Call(context, "UpdateRuntime");
            string root = Path.Combine(Terraria.Program.SavePath, "composition");
            var loadedHuman = Player.LoadPlayer(Path.Combine(root, FixtureName + ".plr"), false);
            Require(loadedHuman != null && loadedHuman.Player.name == "验证主人" && loadedHuman.Player.inventory[25].stack == 17,
                "fresh original Player.LoadPlayer consumes the exact saved human inventory");
            loadedHuman.SetAsActive();
            var loadedWorld = WorldFile.GetAllMetadata(Path.Combine(root, FixtureName + ".wld"), false);
            Require(loadedWorld != null && loadedWorld.UniqueId == Guid.ParseExact(receipt[5], "N"), "original world metadata keeps its saved identity");
            loadedWorld.SetAsActive();
            // Real clearWorld retires cached tile draws during the load. The
            // short probe startup omits Main's native renderer construction.
            Main.instance.TilePaintSystem = new Terraria.GameContent.TilePaintSystemV2();
            Main.instance.TilesRenderer = new Terraria.GameContent.Drawing.TileDrawing(Main.instance.TilePaintSystem);
            // Native startup constructs this before clearWorld resets it.
            Main.PylonSystem = new Terraria.GameContent.TeleportPylonsSystem();
            Wiring.Initialize();
            // Original LoadWorld restores the saved cloud count and may pick
            // an ordinary or rare cloud; provide the native objects and art.
            for (int i = 0; i < Main.cloud.Length; i++) Main.cloud[i] = new Cloud();
            for (int i = 0; i < CloudID.Count; i++) graphics.LoadTexture("Cloud", "Images/Cloud_" + i, i);
            WorldFile.LoadWorld();
            Require(!WorldGen.loadFailed && Main.tile[44, 54].active() && Main.tile[44, 54].type == TileID.Stone,
                "full original WorldFile.LoadWorld restores the exit tile: " + WorldFile.LastThrownLoadException);
            WorldGen.isGeneratingOrLoadingWorld = false;
            Main.gameMenu = Main.gamePaused = Main.playerInventory = false;
            Netplay.Disconnect = false;
            var human = loadedHuman.Player;
            human.active = true;
            Require(Terraria.Social.SocialAPI.Achievements == null && !(bool)Get(Main.Achievements, "_isCloudSave") &&
                String.Equals((string)Get(Main.Achievements, "_savePath"), Path.Combine(Terraria.Program.SavePath, "achievements.dat"), StringComparison.OrdinalIgnoreCase),
                "native achievements use only the owned temporary root during the loaded player's Spawn");
            Terraria.Initializers.AchievementInitializer.Load();
            Main.BestiaryDB = new Terraria.GameContent.Bestiary.BestiaryDatabase();
            new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            SpawnLoadedHuman(graphics, human);
            WaitForRoster(context, host);

            var id = Guid.ParseExact(receipt[2], "N");
            var profile = NativeCompanionConfigurationChecks.Profile(host, id);
            Require(profile.OwnerId == Guid.ParseExact(receipt[3], "N") && profile.Revision > Int64.Parse(receipt[4], CultureInfo.InvariantCulture) &&
                profile.Behavior.MainWeapons.Single().SlotHint == WeaponSlot && !(bool)Call(host, "IsJoined", id),
                "fresh production lookup loads the final original-owner archive and finds no stale deployed activation");
            var window = OpenManager(context, graphics, host, id);
            Require((string)Get(Tool(window, "actor-state"), "Text") == "点击加入", "fresh UI offers the correct actual join action");
            Click(context, window, "actor-state");
            var body = (Player)Call(host, "GetBody", id);
            Require((bool)Call(host, "IsJoined", id) && body.active && !body.dead && body.inventory[ResourceSlot].type == ItemID.Wood &&
                body.inventory[ResourceSlot].stack == 37 && body.inventory[PotionSlot].stack == 2,
                "fresh UI joins a native body from the final archive without starter regeneration");
            body.statLife = 49;
            body.potionDelay = 0;
            KillHuman(human);
            CheckToolbar(context, graphics, host, id, true, output, "reentered-owner-dead");
            Click(context, window, "item-once");
            Require(GetOptional(window, "onceSelection") != null, "trusted UI can arm a live teammate's real use mode while human is dead");
            Click(context, window, "slot:False:Inventory:" + PotionSlot);
            Require(GetOptional(window, "onceSelection") == null && ((string)Get(host, "Feedback")).Contains("已请求"),
                "real teammate inventory slot accepts exactly one UI use request");
            for (int i = 0; i < 4; i++)
            {
                NativeQuickItemChecks.BeginWorldStep();
                Call(context, "UpdateRuntime");
                body.Update(body.whoAmI);
            }
            Require(body.statLife > 49 && body.inventory[PotionSlot].stack == 1 && human.dead && human.inventory[25].stack == 17,
                "the re-entered companion consumes exactly one original potion with no human inventory write or revival");
            Console.WriteLine("PASS re-entry child original encrypted player/world/companion loads, final exit resource=37, preference unchanged, validated two physical toolbars and post-reentry heal consumes 2 -> 1.");
        }

        private static void SpawnLoadedHuman(ProbeGraphics graphics, Player human)
        {
            // The compact probe bootstraps CaptureManager as a dedicated
            // server. Real local Spawn enters the client camera's fade and
            // biome scan, so give that original method its native camera.
            var capture = Terraria.Graphics.Capture.CaptureManager.Instance;
            var cameraField = AccessTools.Field(capture.GetType(), "_camera");
            var existing = cameraField.GetValue(capture);
            using (var camera = existing == null ? (IDisposable)Activator.CreateInstance(cameraField.FieldType, new object[] { graphics.GraphicsDevice }) : null)
            {
                if (camera != null) cameraField.SetValue(capture, camera);
                try { human.Spawn(PlayerSpawnContext.SpawningIntoWorld); }
                finally { if (camera != null) cameraField.SetValue(capture, existing); }
            }
        }

        private static void WaitForRoster(object context, object host)
        {
            NativeQuickItemChecks.Until(() =>
            {
                Call(context, "UpdateRuntime");
                var archive = Get(host, "profiles");
                Require(GetOptional(archive, "LoadError") == null, "owned companion roster is readable: " + GetOptional(archive, "LoadError"));
                return (bool)Get(archive, "Ready");
            });
        }

        private static void KillHuman(Player human)
        {
            human.immune = false;
            human.immuneTime = 0;
            human.Hurt(PlayerDeathReason.ByCustomReason("isolated companion owner death"), 99999, 0);
            Require(human.dead && human.statLife == 0, "original human Hurt/KillMe supplies the 0-health screenshot state");
        }

        private static object OpenManager(object context, ProbeGraphics graphics, object host, Guid id)
        {
            Main.screenWidth = 1280;
            Main.screenHeight = 720;
            PlayerInput.CacheOriginalScreenDimensions();
            Main.UIScale = .764f;
            Call(Main.ResourceSetsManager, "SetActive", "HorizontalBarsWithFullText");
            var shell = Get(context, "Shell");
            var window = Get(shell, "CompanionUi");
            var selectedBody = (Player)Call(host, "GetBody", id);
            graphics.LoadItemTextures(Main.LocalPlayer.inventory.Concat(selectedBody.inventory).Where(i => i != null && !i.IsAir).Select(i => i.type).Distinct());
            if (!(bool)Get(window, "Visible"))
            {
                Set(window, "selected", id);
                var layout = Get(Get(shell, "State"), "Layout");
                Call(shell, "OpenCompanions", Get(layout, "Viewport"));
            }
            NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
            NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
            return window;
        }

        private static void CheckToolbar(object context, ProbeGraphics graphics, object host, Guid id, bool dead, string output, string stage)
        {
            var window = OpenManager(context, graphics, host, id);
            Require(Main.LocalPlayer.dead == dead && (string)Get(Tool(window, "actor-state"), "Text") == "点击收回", "fixture has the reported relationship of the human and joined companion");
            var renderer = Get(window, "renderer");
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            Require((bool)Get(renderer, "ResourcesCurrent"), "the actual companion renderer accepts its current native resources");
            Console.WriteLine("COMPANION-TOOL stage=" + stage + " viewport=1280x720 scale=" + Main.UIScale.ToString("R", CultureInfo.InvariantCulture) +
                " fontLine=" + font.LineSpacing + " fontWidth=" + font.MeasureString("选择主要武器").X +
                " ownerOffset=" + Get(window, "ownerToolbarOffset") + " companionOffset=" + Get(window, "companionToolbarOffset"));
            var pixels = graphics.Pixels(() => Call(window, "Draw"), Main.UIScaleMatrix, 1280, 720);
            for (int i = 0; i < ToolKeys.Length; i++)
            {
                string key = ToolKeys[i];
                var control = Tool(window, key);
                var rect = Get(control, "Rect");
                var row = Get(window, i < 4 ? "ownerToolbarView" : "companionToolbarView");
                var pane = Get(window, i < 4 ? "ownerView" : "companionView");
                Require(Inside(rect, row) && Inside(rect, pane), "both actual culling rectangles admit " + key + ": " + RectangleText(rect));
                int ink = Ink(pixels, rect);
                Require(ink > 5, "real button renderer leaves its text pixels for " + key + ", count=" + ink);
                Console.WriteLine("COMPANION-TOOL stage=" + stage + " key=" + key + " rect=" + RectangleText(rect) +
                    " toolbar=" + RectangleText(row) + " pane=" + RectangleText(pane) + " ink=" + ink);
            }
            if (dead) graphics.Image(Path.Combine(output, stage + ".png"), () => Call(window, "Draw"), Main.UIScaleMatrix, 1280, 720);
        }

        private static object Tool(object window, string prefix) => ((IEnumerable)Get(window, "controls")).Cast<object>().Single(c =>
            ((string)Get(c, "Key")).StartsWith(prefix + "/", StringComparison.Ordinal));

        private static void Click(object context, object window, string prefix) => NativeToolsUiChecks.Click(context,
            NativeToolsUiChecks.Point(Get(Tool(window, prefix), "Rect")) * Main.UIScale);

        private static bool Inside(object rect, object view) => Axis(rect, "X") >= Axis(view, "X") && Axis(rect, "Y") >= Axis(view, "Y") &&
            Axis(rect, "Right") <= Axis(view, "Right") && Axis(rect, "Bottom") <= Axis(view, "Bottom");

        private static float Axis(object rect, string axis) => (float)Get(rect, axis);

        private static string RectangleText(object rect) => String.Join(",", new[] { "X", "Y", "Width", "Height" }.Select(axis =>
            Axis(rect, axis).ToString("R", CultureInfo.InvariantCulture)));

        private static int Ink(Color[] pixels, object rect)
        {
            int left = (int)Math.Ceiling(Axis(rect, "X") * Main.UIScale) + 3;
            int top = (int)Math.Ceiling(Axis(rect, "Y") * Main.UIScale) + 3;
            int right = (int)Math.Floor(Axis(rect, "Right") * Main.UIScale) - 3;
            int bottom = (int)Math.Floor(Axis(rect, "Bottom") * Main.UIScale) - 3;
            int ink = 0;
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++)
            {
                var color = pixels[y * 1280 + x];
                if (color.R > 105 && color.G > 105 && color.B > 105 && Math.Abs(color.R - color.B) < 45) ink++;
            }
            return ink;
        }

        private static void RunReentry(string content, string output, string nonce)
        {
            var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
            {
                Arguments = $"\"{Program.Repository}\" \"{Path.GetFullPath(content)}\" \"{output}\" CompanionExitVisual",
                WorkingDirectory = output,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            NativeScopeDispatch.PrepareOwnedChild(start);
            start.EnvironmentVariables[ReaderVariable] = nonce;
            using (var child = Process.Start(start))
            {
                var elapsed = Stopwatch.StartNew();
                var stdout = child.StandardOutput.ReadToEndAsync();
                var stderr = child.StandardError.ReadToEndAsync();
                // Loading and drawing the saved world can itself exceed 45s
                // on the ordinary probe runner. Keep a finite child deadline
                // and retain partial native progress if it is exceeded.
                const int readerDeadline = 75000;
                bool completed = child.WaitForExit(readerDeadline);
                if (!completed)
                {
                    if (!child.HasExited) child.Kill();
                    child.WaitForExit();
                }
                string log = stdout.Result + stderr.Result;
                File.WriteAllText(Path.Combine(output, "exit-reader.log"), log);
                Console.WriteLine("COMPANION-EXIT reader elapsed-ms=" + elapsed.ElapsedMilliseconds + " completed=" + completed);
                if (!completed) throw new TimeoutException("Owned companion re-entry probe timed out: " + log);
                Require(child.ExitCode == 0, "fresh owned companion re-entry succeeds: " + log);
                Console.WriteLine(log);
            }
        }
    }
}
