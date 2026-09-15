using System;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Guidance;
using JueMingR.Platform.Guidance;
using JueMingR.Platform.Operations;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceChecks
    {
        internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static int forbidden;
        private static bool RejectSpawn() { forbidden++; throw new InvalidOperationException("Real world generation forbidden in probe"); }
        internal static void RunCpu()
        {
            string root = Path.Combine(Terraria.Program.SavePath, "guidance-composition"); Directory.CreateDirectory(root);
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "isolated.wld"), false);
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            string[] old = NativeInformationConfigurationChecks.SeedOldBindings(worker, root);
            NativeGuidanceStyleChecks.Codec(); NativeGuidanceStyleChecks.Seed(root);
            object context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "direction-equipment-" + new string('8', 40), Path.Combine(root, "evidence.txt"), root }, null);
            var guard = new Harmony("JueMingR.Guidance.NoWorldGeneration");
            var spawnMethod = typeof(WorldGen).GetMethod("SpawnTravelNPC", Flags, null, Type.EmptyTypes, null);
            guard.Patch(spawnMethod, prefix: new HarmonyMethod(typeof(NativeGuidanceChecks).GetMethod(nameof(RejectSpawn), Flags)));
            try
            {
                Main.npc = new NPC[Main.maxNPCs]; Main.dayTime = true; Main.bloodMoon = Main.eclipse = Main.slimeRain = Main.pumpkinMoon = Main.snowMoon = false; Main.invasionType = 0;
                Call(context, "InstallInformationSources"); Require((bool)Get(Get(context, "InformationReadiness"), "Installed"), "new complete profile preserves real information observers");
                Call(context, "InitializeRuntime", true); var host = Get(context, "Guidance"); var npcs = Get(context, "nativeNpcs");
                Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
                Require(((JueMingR.Platform.Settings.PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Status==JueMingR.Platform.Settings.PreferenceStatus.Saved &&
                    File.ReadAllText(Path.Combine(root,"JueMingRData/config/features/guidance.json"))==NativeGuidanceStyleChecks.Legacy &&
                    !File.Exists(Path.Combine(root,"JueMingRData/config/features/guidance.json.schema1-original")),"loading schema1 preserves exact original without writing or archiving");
                Require(Get(context, "Labels") != null && Get(context, "WorldObjects") != null && Get(context, "Information") != null && Get(context, "items") != null, "new profile retains prior complete feature chain");
                var registry = (HotkeyRegistry)Get(Get(Get(context, "Shell"), "hotkeys"), "Registry");
                Require(registry.Actions.Count == old.Length + 7 && registry.Find("merchant-test.once") == null, "three toggle actions added; summon has no hotkey action");
                int reads = (int)Get(npcs, "BasicReads"); for (int i = 0; i < 120; i++) Call(context, "UpdateRuntime");
                Require((int)Get(npcs, "BasicReads") == reads, "all closed and never clicked: zero NPC source reads");
                NativeGuidanceEquipmentChecks.Run(assembly);
                NativeGuidanceInputChecks.Run(context, host, root, old);
                CheckDisplays(context, host, npcs);
                NativeGuidanceCostChecks.Run(context, host);
                CheckOperations(context, host);
                Require(forbidden == 0, "probe never called actual generation entry");
                Console.WriteLine("PASS: built complete Guidance Host, actual F5/shortcut/preferences, 0/1 observation, independent warning and protected single attempt.");
            }
            finally { StopContext(context); guard.Unpatch(spawnMethod, HarmonyPatchType.All, guard.Id); Main.netMode = 0; Main.gameMenu = Main.dedServ = false; }
        }
        internal static void Until(Func<bool> ready)
        { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!ready()) { if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("guidance readiness/save"); Thread.Sleep(5); } }
        internal static NPC Npc(int slot, int type, int rarity, float x)
        { return new NPC { active = true, whoAmI = slot, type = type, netID = type, rarity = rarity, life = 100, width = 20, height = 40, position = new Vector2(x, 450) }; }
        private static void CheckDisplays(object context, object host, object npcs)
        {
            var rare = (RareCreatureDirection)Get(host, "Rare"); var merchant = (TravellingMerchantDirection)Get(host, "Merchant"); var warning = (EquipmentWarning)Get(host, "Equipment");
            var p = Main.LocalPlayer; p.accCritterGuide = true; p.hideInfo[11] = false;
            Main.npc[1] = Npc(1, NPCID.Tim, 4, 1300); Main.npc[2] = Npc(2, NPCID.TravellingMerchant, 0, 2200); Main.npc[3] = Npc(3, 4, 0, 200); Main.npc[3].boss = true;
            p.armor[3] = NativeGuidanceEquipmentChecks.Accessory(ItemID.Toolbelt);
            foreach (GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind))) Call(host, "SetEnabled", kind, true);
            var world = Get(host, "World"); Set(host, "LayerStatus", Enum.Parse(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Rendering.WorldLayerStatus"), "Ready"));
            for (int mode = 0; mode <= 1; mode++)
            {
                Main.netMode = mode; Call(context, "UpdateRuntime"); Call(world, "Prepare");
                Require(rare.Visible && merchant.Visible && warning.Alpha == 1, "ordinary local client mode " + mode + " consumes real new observations");
                int before = (int)Get(npcs, "BasicReads"), show = warning.Notifications, layouts = (int)Get(Get(world, "MerchantText"), "Layouts");
                long revision = (long)Get(Get(host, "Preferences"), "Revision");
                for (int i = 0; i < 60; i++) { Call(context, "UpdateRuntime"); Call(world, "Prepare"); }
                Require((int)Get(npcs, "BasicReads") - before == 60 * Main.maxNPCs, "three consumers share at most one native slot read per Update at full capacity");
                Require(warning.Notifications == show && (int)Get(Get(world, "MerchantText"), "Layouts") == layouts && (long)Get(Get(host, "Preferences"), "Revision") == revision,
                    "stable state does not re-Show/re-layout/re-save");
                p.armor[3] = NativeGuidanceEquipmentChecks.Accessory(ItemID.TreasureMagnet); Call(context, "UpdateRuntime"); Require(warning.Notifications == show + 1, "same danger still detects changed effective equipment");
                p.armor[3].TurnToAir(); Call(context, "UpdateRuntime"); Require(warning.Alpha == 0, "correction immediately removes warning");
                p.armor[3] = NativeGuidanceEquipmentChecks.Accessory(ItemID.Toolbelt);
                p.hideInfo[11] = true; Call(context, "UpdateRuntime"); Require(!rare.Visible, "independent native information hidden gate"); p.hideInfo[11] = false;
            }
            Main.netMode = 0;
            Call(context, "UpdateRuntime"); NativeGuidanceCadenceChecks.Displays(context, host);
            NativeGuidancePresentationChecks.Run(context, host, npcs);
            Main.npc[3].active = false; Main.bloodMoon = true; Call(context, "UpdateRuntime"); Require(warning.Alpha == 0, "single blood moon does not trigger");
            Main.npc[3].active = true; Call(context, "UpdateRuntime"); Require(warning.Alpha == 1, "blood moon does not veto boss");
            Main.npc[3].active = false; Main.invasionType = 1; Call(context, "UpdateRuntime"); int notifications = warning.Notifications;
            Main.invasionType = 3; Call(context, "UpdateRuntime"); Require(warning.Notifications == notifications + 1, "different invasion identity is meaningful danger change"); Main.invasionType = 0;
            Main.npc[1].life = 0; Call(context, "UpdateRuntime"); Require(!rare.Visible, "dead rare clears before next full discovery");
            Main.gameMenu = true; Call(context, "UpdateRuntime"); Require(!rare.Visible && !merchant.Visible && warning.Alpha == 0, "session end clears all views"); Main.gameMenu = false;
            Call(context, "UpdateRuntime"); Main.dedServ = true; Call(context, "UpdateRuntime"); Require(!merchant.Visible, "server cannot display client guidance"); Main.dedServ = false; Call(context, "UpdateRuntime");
            Console.WriteLine("Guidance Host stable 60 updates/mode at N=200: basic reads=12000 shared; repeated Show/layout/save=0; modes=0,1 are isolated adapters, not live multiplayer.");
        }
        private static void CheckOperations(object context, object host)
        {
            var feature = (MerchantTestFeature)Get(host, "MerchantTest"); var port = Get(feature, "port"); int calls = 0;
            Set(port, "spawn", (Action)(() => { calls++; Main.npc[8] = Npc(8, 368, 0, 2500); }));
            Action update = () => Call(context, "UpdateRuntime");
            Main.npc[2].active = false; Main.npc[8] = null; Main.dayTime = true; Main.eclipse = false; Main.invasionType = 0;
            object shell = Get(context, "Shell"); Call(Get(shell, "State"), "RestoreVisible");
            Require((bool)Get(shell, "CanExecuteMerchantInput"), "final world-gate tests start with an actual valid F5 input owner");
            // Full F5 pointer chain submitted by the input check; here final
            // native adapter receives explicit requests against changed facts.
            long session = (long)Get(host, "Session");
            foreach (Action invalid in new Action[] { () => Main.netMode = 1, () => Main.dayTime = false, () => Main.eclipse = true,
                () => { Main.invasionType = 1; Main.invasionDelay = 0; Main.invasionSize = 100; }, () => { Main.npc[8] = Npc(8,368,0,200); Main.npc[8].hide = true; }, () => Main.npc = null })
            {
                var table = Main.npc; invalid();
                var result = (MerchantTestReceipt)Call(port, "Execute", new MerchantTestRequest(session));
                Require(result.Outcome != GameOperationOutcome.Succeeded && calls == 0, "final native gate rejects invalid world before invocation");
                Main.npc = table; Main.netMode = 0; Main.dayTime = true; Main.eclipse = false; Main.invasionType = 0; Main.npc[8] = null;
            }
            Call(host, "RequestMerchant"); update(); for (int i = 0; i < 20; i++) update();
            Require(calls == 1 && feature.Result.Outcome == GameOperationOutcome.Succeeded && ((TravellingMerchantDirection)Get(host, "Merchant")).Visible, "one operation result enters ordinary NPC observation, no injected render target");
            Require(feature.Result.Message == "旅商已到访。", "confirmed native result contains only the arrival message");
            Call(host, "RequestMerchant"); update(); Require(calls == 1 && feature.Result.Outcome == GameOperationOutcome.Rejected, "existing active target never refreshes shop");
            Main.npc[8] = null; Set(port, "spawn", (Action)(() => calls++)); Call(host, "RequestMerchant"); update(); update(); Require(calls == 2 && feature.Result.Outcome == GameOperationOutcome.Unconfirmed, "empty result is unconfirmed without retry");
            Require(feature.Result.Message == "已尝试召唤，无法确认旅商是否到访；不会自动重试。", "unconfirmed native result never uses the confirmed arrival message");
            Set(port, "spawn", (Action)(() => { calls++; throw new InvalidOperationException(); })); Call(host, "RequestMerchant"); update(); update(); Require(calls == 3 && feature.Result.Outcome == GameOperationOutcome.Failed, "exception is terminal with possible partial native side effects");
            Require(feature.Result.Message == "召唤出错，结果未确认；不会自动重试。", "thrown operation preserves uncertainty about partial results");
        }
    }
}
