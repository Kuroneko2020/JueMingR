using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Reuse the existing native device/content/world fixture. Only that fixture's
    // simulation-skipping Update is replaced; Main.Update, DoUpdate, its focus
    // out parameter, CanPauseGame and DoUpdateInWorld all execute unchanged.
    internal static class NativeAfkOuterBackgroundChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Harmony audit = new Harmony("JueMingR.Tests.AfkOuterBackground");
        private static readonly Stopwatch deadline = new Stopwatch();
        private static Type fixture;
        private static object context, input, afk, fishing;
        private static Action<Main, GameTime> nativeUpdate;
        private static bool prepared, focused = true, lastPause;
        private static int stage, frames, inputCalls, focusCalls, priorInputs, priorFocus, priorStarts, priorAmmo;
        private static uint priorWorld;
        private static int bobberKey;

        internal static int Run(string content, string output)
        {
            Require(Directory.Exists(content) && Directory.Exists(Path.Combine(output, "Content")), "Outer native fixture requires the verified Content and its existing owned alias");
            // Harmony may initialize a declaring type while preparing patches.
            // Own all static native paths before installing even the OS seam.
            Terraria.Program.SavePath = Path.Combine(Path.GetFullPath(output), "isolated-user");
            Directory.CreateDirectory(Terraria.Program.SavePath); NativeTestAudio.Install(typeof(Main).Assembly);
            string candidate = Environment.GetEnvironmentVariable("JUEMINGR_COMBAT_GEOMETRY_CANDIDATE");
            Require(!String.IsNullOrEmpty(candidate) && File.Exists(candidate), "Outer native check requires an explicit fixed Host candidate");
            fixture = typeof(NativeViewOriginalChecks).GetNestedType("ViewGame", F);
            Require(fixture != null, "Existing initialized native view fixture is available");
            string oldCandidate = Environment.GetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE");
            Environment.SetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE", candidate);
            deadline.Restart(); prepared = false; focused = true; stage = frames = inputCalls = focusCalls = 0;
            audit.Patch(fixture.GetMethod("Update", F), prefix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(FixtureUpdate)));
            audit.Patch(fixture.GetMethod("Draw", F), prefix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(FixtureDraw)));
            audit.Patch(AccessTools.Method(typeof(FocusHelper), "UpdateFocus"),
                postfix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(FocusResult)) { priority = Priority.Last, after = new[] { "JueMingR.AfkMode" } },
                transpiler: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(FocusOsBoundary)));
            try { return NativeViewOriginalChecks.Run(content, output); }
            finally
            {
                audit.UnpatchAll(audit.Id);
                Environment.SetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE", oldCandidate);
                context = input = afk = fishing = null; nativeUpdate = null;
            }
        }
        private static bool FixtureDraw() { return !prepared; }
        private static bool FixtureUpdate(Main __instance, GameTime __0)
        {
            if (deadline.Elapsed.TotalSeconds > 120) throw new TimeoutException("Outer native initialization/scene deadline; stage=" + stage + " frames=" + frames);
            if (Main.showSplash) return false; // Existing DrawSplash loads assets.
            if (!prepared) Prepare(__instance);
            BeforeFrame();
            uint before = Main.GameUpdateCount;
            nativeUpdate(__instance, __0);
            Require(!Main.SettingPlayWhenUnfocused, "Outer test never changes the native default background setting");
            AfterFrame(__instance, before);
            return false;
        }
        private static void Prepare(Main game)
        {
            fixture.GetMethod("Prepare", F).Invoke(game, null);
            context = Get(game, "context"); input = Get(context, "Input"); afk = Get(context, "Afk"); fishing = Get(context, "Fishing");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(Terraria.Program.SavePath, "outer.plr"), false)
            { Metadata = Terraria.IO.FileMetadata.FromCurrentSettings(Terraria.IO.FileType.Player), Player = Main.LocalPlayer };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "outer.wld"), false)
            { UniqueId = Guid.NewGuid(), Name = "Outer owned world", WorldId = 1 };
            Main.Map.Load(); // Native missing-map path establishes this world's MapFileMetadata.
            Require(Main.MapFileMetadata != null, "native owned map load establishes its serializable metadata");
            // Native PlayerFileData.CreateAndSave supplies Metadata. The view
            // fixture's bare record was not serializable: InternalSavePlayerFile
            // dereferences Metadata before Serialize. Exercise the complete
            // native save here, synchronously, so a further missing outer-save
            // prerequisite fails with its original exception before world work.
            audit.Patch(typeof(Player).GetMethod("SavePlayer", F), prefix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(SaveEntry)));
            audit.Patch(typeof(Player).GetMethod("InternalSavePlayerFile", F), prefix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(SaveFileEntry)));
            Player.SavePlayer(Main.ActivePlayerFileData, skipMapSave: false);
            Require(File.Exists(Main.ActivePlayerFileData.Path), "complete native SavePlayer writes the owned initialized character record");
            Console.WriteLine("OUTER native SavePlayer completed before EnterWorld; no save outlet was replaced");
            // The drawing-only scene never enters a world. The native entry
            // event supplies its missing gameplay-enable transition; no pause
            // flag or world counter is changed by the fixture.
            Console.WriteLine("OUTER native entry before CanUpdateGameplay=" + Main.CanUpdateGameplay);
            Player.Hooks.EnterWorld(Main.myPlayer);
            Require(Main.CanUpdateGameplay, "real native EnterWorld enables gameplay in the existing draw-only scene");
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : new IntPtr(2)));
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            Set(Get(Get(context, "Shell"), "State"), "Ready", true); Call(Get(context, "Shell"), "CloseAndSubmitPosition");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(afk, "ControlsEnabled") && ((FishingSettings)Get(fishing, "Settings")).Loaded; });
            Type worker = context.GetType().DeclaringType;
            worker.GetField("postfixContext", F).SetValue(null, context); worker.GetField("hookCommitted", F).SetValue(null, 1);
            MethodInfo outer = typeof(Main).GetMethod("Update", F, null, new[] { typeof(GameTime) }, null);
            MethodInfo handle = typeof(Main).GetMethod("DoUpdate_HandleInput", F);
            // Replace only the old specialist's narrow late callback with the
            // ordinary production input callbacks, at the same real seam.
            audit.Unpatch(handle, fixture.GetMethod("FinalizeKeyboard", F));
            audit.Patch(outer, prefix: new HarmonyMethod(worker.GetMethod("UpdatePrefix", F)), postfix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(RuntimePostfix)));
            audit.Patch(handle, prefix: new HarmonyMethod(worker.GetMethod("InputPrefix", F)), postfix: new HarmonyMethod(worker.GetMethod("InputPostfix", F)));
            audit.Patch(handle, postfix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(InputVisited)));
            var direct = new DynamicMethod("CallOriginalMainUpdate", typeof(void), new[] { typeof(Main), typeof(GameTime) }, typeof(NativeAfkOuterBackgroundChecks), true);
            var il = direct.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, outer); il.Emit(OpCodes.Ret);
            nativeUpdate = (Action<Main, GameTime>)direct.CreateDelegate(typeof(Action<Main, GameTime>));
            Player p = Main.LocalPlayer;
            // View.Prepare places a new Player directly at tile Y=220 but never
            // calls Spawn. Its default fallStart=0 makes the first stationary
            // Player.Update charge a 220-tile fall. Native Spawn resets these
            // exact baselines after placement; do the same before simulation.
            Console.WriteLine("OUTER spawn baseline tileY=" + (int)(p.position.Y / 16f) + " fallStart=" + p.fallStart + " velocity=" + p.velocity);
            p.ResetFallDamage();
            audit.Patch(typeof(Player).GetMethod("KillMe", F), prefix: new HarmonyMethod(typeof(NativeAfkOuterBackgroundChecks), nameof(DeathObserved)));
            for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
            p.inventory[0].SetDefaults(ItemID.Minishark); p.inventory[54].SetDefaults(ItemID.MusketBall); p.inventory[54].stack = 9999;
            p.selectedItemState.Select(0); p.selectedItemState.Update(); p.statLife = p.statLifeMax2 = 400;
            Main.playerInventory = false; Main.autoPause = false; Main.autoSave = false; Main.SettingPlayWhenUnfocused = false;
            Main.ignoreErrors = false; // Native world exceptions must fail this isolated proof.
            Call(afk, "SetGapMilliseconds", 0); Call(afk, "SetModeEnabled", true);
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(afk, "ModeEnabled") && (bool)Get(afk, "ControlsEnabled"); });
            Require((bool)fixture.GetProperty("NativeInitialized", F).GetValue(game, null), "Existing native Main.Initialize actually completed");
            Set(game, "prepared", true); prepared = true;
            Console.WriteLine("OUTER native Initialize/LoadContent/owned scene complete; real Main.Update body retained; default PlayWhenUnfocused=false");
            BeginStage(0);
        }
        private static void RuntimePostfix() { if (prepared) Call(context, "UpdateRuntime"); }
        private static void InputVisited() { inputCalls++; }
        private static void BeforeFrame()
        {
            focused = stage == 1 || stage == 2 || stage == 6 || stage == 7 || stage == 10;
            fixture.GetField("movementMouseDown", F).SetValue(null, stage == 2 || stage == 7);
            fixture.GetField("keyboard", F).SetValue(null, new KeyboardState());
        }
        private static void BeginStage(int next)
        {
            stage = next; frames = 0; priorWorld = Main.GameUpdateCount; priorInputs = inputCalls; priorFocus = focusCalls;
            priorStarts = (int)Get(afk, "Starts"); priorAmmo = Main.LocalPlayer.inventory[54].stack;
            Console.WriteLine("OUTER stage=" + stage + " world=" + priorWorld + " starts=" + priorStarts);
        }
        private static void Observe(Main game)
        {
            Console.WriteLine("OUTER STATE stage=" + stage + " outer=" + frames + " world=" + Main.GameUpdateCount + " input=" + inputCalls +
                " focus=" + FocusHelper.IsSelectedApplication + " pauseOut=" + lastPause + " paused=" + Main.gamePaused + " gameplay=" + Main.CanUpdateGameplay +
                " entities=" + game.ShouldUpdateEntities() + " stepPaused=" + Terraria.Testing.WorldUpdateStepper.Paused +
                " canPause=" + typeof(Main).GetMethod("CanPauseGame", F).Invoke(null, null) + " mappedReady=" + Get(input, "CanStartActions") +
                " automatic=" + Get(input, "CanRunAutomaticActions") + " starts=" + Get(afk, "Starts") + " running=" + Get(afk, "Running") +
                " anim=" + Main.LocalPlayer.itemAnimation + " itemTime=" + Main.LocalPlayer.itemTime +
                " life=" + Main.LocalPlayer.statLife + " dead=" + Main.LocalPlayer.dead + " net=" + Main.netMode);
        }
        private static void SaveEntry(Terraria.IO.PlayerFileData __0)
        {
            Console.WriteLine("OUTER SAVE entry fileNull=" + (__0 == null) + " playerNull=" + (__0?.Player == null) + " metadataNull=" + (__0?.Metadata == null) +
                " achievementsNull=" + (Main.Achievements == null) + " mapNull=" + (Main.Map == null) + " activeFileNull=" + (Main.ActivePlayerFileData == null) +
                " nativePlayerDead=" + Main.LocalPlayer.dead + " path=" + __0?.Path);
        }
        private static void SaveFileEntry(Terraria.IO.PlayerFileData __0)
        { Console.WriteLine("OUTER SAVE native file stage metadataNull=" + (__0?.Metadata == null) + " playerNull=" + (__0?.Player == null)); }
        private static void DeathObserved(Player __instance, Terraria.DataStructures.PlayerDeathReason __0, double __1)
        {
            if (__instance.whoAmI == Main.myPlayer)
                Console.WriteLine("OUTER native death otherReason=" + __0?.SourceOtherIndex + " damage=" + __1 + " life=" + __instance.statLife +
                    " position=" + __instance.position + " fallStart=" + __instance.fallStart + " velocity=" + __instance.velocity);
        }
        private static void AfterFrame(Main game, uint before)
        {
            frames++;
            if (frames <= 3 || frames == 20) Observe(game);
            if (stage == 0 && focusCalls - priorFocus >= 3)
            {
                Require(Main.gamePaused && lastPause && Main.GameUpdateCount == priorWorld && inputCalls == priorInputs && focusCalls > priorFocus,
                    "armed idle owner leaves native unfocused pause before HandleInput/world intact"); BeginStage(1);
            }
            else if (stage == 1 && Main.GameUpdateCount > priorWorld && (bool)Get(input, "CanStartActions")) BeginStage(2);
            else if (stage == 2 && Main.GameUpdateCount > before)
            {
                Require((bool)Get(afk, "Running") && (int)Get(afk, "Starts") > priorStarts && Main.GameUpdateCount > before,
                    "focused physical press enters a real native AFK weapon operation"); BeginStage(3);
            }
            else if (stage == 3 && unchecked(Main.GameUpdateCount - priorWorld) >= 180)
            {
                Require(!lastPause && !Main.gamePaused && !FocusHelper.IsSelectedApplication && Main.GameUpdateCount > priorWorld && inputCalls > priorInputs &&
                    (bool)Get(afk, "Running") && (int)Get(afk, "Starts") > priorStarts && Main.LocalPlayer.inventory[54].stack < priorAmmo,
                    "default-background outer Main reaches HandleInput, native world and repeated real ammo-consuming AFK uses");
                Main.autoPause = true; Main.playerInventory = true; BeginStage(4);
            }
            else if (stage == 4 && inputCalls - priorInputs >= 3)
            {
                Require((bool)typeof(Main).GetMethod("CanPauseGame", F).Invoke(null, null) && Main.gamePaused && Main.GameUpdateCount == priorWorld && inputCalls > priorInputs,
                    "real native autoPause retains its later pause even during accepted background work");
                Main.autoPause = false; Main.playerInventory = false; Call(afk, "SetModeEnabled", false); BeginStage(5);
            }
            else if (stage == 5 && focusCalls - priorFocus >= 3)
            {
                Require(lastPause && Main.gamePaused && Main.GameUpdateCount == priorWorld && inputCalls == priorInputs,
                    "closing AFK restores original unfocused pause without changing settings");
                Player p = Main.LocalPlayer; p.position = new Vector2(312 * 16, 212 * 16); p.velocity = Vector2.Zero;
                p.inventory[0].SetDefaults(ItemID.WoodFishingPole); p.inventory[54].SetDefaults(ItemID.Worm); p.inventory[54].stack = 100;
                p.selectedItemState.Select(0); p.selectedItemState.Update();
                Main.screenPosition = p.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                Vector2 screen = p.Center + new Vector2(150, 20) - Main.screenPosition;
                fixture.GetField("movementMouseX", F).SetValue(null, (int)screen.X); fixture.GetField("movementMouseY", F).SetValue(null, (int)screen.Y);
                NativeFishingChecks.Save(fishing, new FishingOptions(auto: true)); BeginStage(6);
            }
            else if (stage == 6 && unchecked(Main.GameUpdateCount - priorWorld) >= 8 && (bool)Get(input, "CanStartActions") && Main.LocalPlayer.itemAnimation == 0 && Main.LocalPlayer.itemTime == 0) BeginStage(7);
            else if (stage == 7 && Main.GameUpdateCount > before)
            {
                Projectile b = Main.projectile.FirstOrDefault(p => p.active && p.bobber && p.owner == Main.myPlayer);
                Require(b != null && !b.wet && !(bool)Get(Get(fishing, "Session"), "Active") && (bool)Get(Get(fishing, "Session"), "WantsBackground"),
                    "real first cast has a dry pending bobber before focus loss"); bobberKey = (int)b.key; BeginStage(8);
            }
            else if (stage == 8)
            {
                Projectile b = Main.projectile.FirstOrDefault(p => p.active && p.bobber && (int)p.key == bobberKey);
                if (b != null && b.wet && (bool)Get(Get(fishing, "Session"), "Active"))
                {
                    Require(!lastPause && !FocusHelper.IsSelectedApplication && Main.GameUpdateCount > priorWorld && inputCalls > priorInputs,
                        "real pending first cast progresses through native outer update into liquid while unfocused");
                    NativeFishingChecks.Save(fishing, new FishingOptions()); BeginStage(9);
                }
                else Require(unchecked(Main.GameUpdateCount - priorWorld) < 180, "pending first bobber must enter the existing owned pond through native world updates");
            }
            else if (stage == 9 && focusCalls - priorFocus >= 3)
            {
                Require(lastPause && Main.gamePaused && Main.GameUpdateCount == priorWorld && inputCalls == priorInputs,
                    "closing automatic fishing restores original focus pause even with its old bobber present");
                Console.WriteLine("PASS OUTER real Main.Update -> FocusHelper(out) -> HandleInput -> native world/player/projectile; AFK use, pending first cast, close and genuine autoPause with default background=false");
                BeginStage(10);
            }
            else if (stage == 10 && Main.GameUpdateCount > priorWorld && (bool)Get(input, "CanStartActions"))
            {
                // Separate native Player/projectile borrowing proof, after the
                // complete outer-loop proof above has already closed. Its own
                // explicit steps are never counted as outer Main evidence.
                NativeFishingChecks.BorrowFocusRecovery(context);
                Console.WriteLine("PASS separate native borrowed net/rod focus and material-lock recovery after initialized outer fixture");
                Set(game, "Complete", true); game.Exit();
            }
        }
        private static void FocusResult(bool __0) { lastPause = __0; focusCalls++; }
        private static IEnumerable<CodeInstruction> FocusOsBoundary(IEnumerable<CodeInstruction> code)
        {
            var active = AccessTools.PropertyGetter(typeof(Game), "IsActive"); var cursor = AccessTools.PropertySetter(typeof(Game), "IsMouseVisible");
            var windows = AccessTools.Field(typeof(ReLogic.OS.Platform), "IsWindows"); var windowsGetter = AccessTools.PropertyGetter(typeof(ReLogic.OS.Platform), "IsWindows");
            foreach (var instruction in code)
            {
                if (instruction.Calls(active)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(NativeAfkOuterBackgroundChecks), nameof(OsActive)); }
                else if (instruction.Calls(cursor)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(NativeAfkOuterBackgroundChecks), nameof(OsCursor)); }
                else if (windows != null && instruction.LoadsField(windows) || windowsGetter != null && instruction.Calls(windowsGetter)) { instruction.opcode = OpCodes.Ldc_I4_0; instruction.operand = null; }
                yield return instruction;
            }
        }
        private static bool OsActive(Game game) { return focused; }
        private static void OsCursor(Game game, bool visible) { }
    }
}
