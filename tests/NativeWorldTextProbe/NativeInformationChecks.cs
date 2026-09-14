using System;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Platform.Information;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeInformationChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void RunCpu()
        {
            string root = Path.Combine(Terraria.Program.SavePath, "information-composition"); Directory.CreateDirectory(root);
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = new Guid("00000000-0000-0000-0000-000000000071") };
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            string[] oldActions = NativeInformationConfigurationChecks.SeedOldBindings(worker, root);
            object context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "information-summary-" + new string('7', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources");
                object readiness = Get(context, "InformationReadiness");
                Require((bool)Get(readiness, "Installed"), "native success observer must install: " + GetOptional(readiness, "Failure"));
                CheckReceipts(readiness);
                Call(context, "InitializeRuntime", true);
                object information = Get(context, "Information");
                Require(information != null, "full package must compose real Information Host");
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (!(bool)Get(Get(information, "Preferences"), "IsLoaded"))
                { if (DateTime.UtcNow > deadline) throw new Exception("information preferences did not load"); Call(context, "UpdateRuntime"); Thread.Sleep(5); }
                var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer");
                var registry = (JueMingR.Platform.Hotkeys.HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
                Require(registry.Actions.Count == oldActions.Length + 4, "complete profile must preserve the actual previous catalog and register four information actions");
                foreach (string id in new[] { "biome-display.toggle", "information.infection.toggle", "information.luck.toggle", "information.angler.toggle", "information-window.adjust" })
                    Require(registry.Find(id) != null, "full frozen registry missing " + id);
                Require(Get(renderer, "InformationControls") != null, "real shell must route information commands");
                var controls = Get(renderer, "InformationControls");
                var command = assembly.GetType("JueMingR.TerrariaHost.F5.F5Command", true);
                Require(((string)Call(controls, "Hint", Enum.Parse(command, "ConfigureInfection"))).Contains("显示原版最近公布的统计，感染变化后不会立即刷新。"), "actual infection help explains publication cadence");
                Call(controls, "Execute", Enum.Parse(command, "EnableLuck"));
                Require((bool)Call(information, "Enabled", InformationKind.Luck), "actual F5 command must update the owning document");
                NativeInformationConfigurationChecks.Run(context, information, root, oldActions);
                Require((bool)Get(Call(readiness, "Snapshot"), "Infection"), "initial successful native data survives later Session composition");
                WithReadOnlyGuards(() => CheckObservationAndHud(context, information, readiness));
                NativeInformationInputChecks.Run(context, information);
                NativeInformationFailureChecks.Run(context, information);
                Console.WriteLine("PASS: built full Host composes independent information controls and preference command.");
            }
            finally
            {
                StopContext(context);
                Main.netMode = 0; Main.gameMenu = false;
            }
        }
        internal static void StopContext(object context)
        {
                var hooks = GetOptional(context, "informationHooks"); if (hooks != null) Call(hooks, "Dispose");
                foreach (string name in new[] { "Information", "Labels", "WorldTargets", "WorldObjects", "items", "notes", "preferences" })
                {
                    var owner = GetOptional(context, name);
                    (owner?.GetType().GetMethod("OnExit", Flags) ?? owner?.GetType().GetMethod("OnProcessExit", Flags))?.Invoke(owner, new object[] { null, EventArgs.Empty });
                }
                var shell = GetOptional(context, "Shell"); if (shell != null) { var hotkeys = GetOptional(shell, "hotkeys"); if (hotkeys != null) Call(hotkeys, "OnExit", null, EventArgs.Empty); }
        }
        internal static object Get(object value, string name) { return GetOptional(value, name) ?? throw new InvalidOperationException("Required consumer missing: " + name); }
        internal static object GetOptional(object value, string name) { var type = value.GetType(); return type.GetProperty(name, Flags)?.GetValue(value) ?? type.GetField(name, Flags)?.GetValue(value); }
        internal static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
        internal static void Set(object value, string name, object data)
        { var field = value.GetType().GetField(name, Flags); if (field != null) field.SetValue(value, data); else value.GetType().GetProperty(name, Flags).SetValue(value, data); }
        internal static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static bool SkipMessage() { return false; }
        private static int forbiddenCalls;
        private static bool RejectUnexpectedOperation() { forbiddenCalls++; return false; }
        private static void WithReadOnlyGuards(Action check)
        {
            var guard = new Harmony("JueMingR.NativeInformation.ReadOnlyGuard");
            var methods = new[] { typeof(WorldGen).GetMethod("CountTiles", Flags, null, new[] { typeof(int) }, null),
                typeof(Player).GetMethod("UpdateLuck", Flags), typeof(Player).GetMethod("UpdateLuckFactors", Flags), typeof(Player).GetMethod("RecalculateLuck", Flags),
                typeof(Player).GetMethod("RollLuck", Flags), typeof(Lang).GetMethod("AnglerQuestChat", Flags), typeof(NPC).GetMethod("GetFirstNPCNameOrNull", Flags) };
            var random = new Terraria.Utilities.UnifiedRandom(711); var expected = new Terraria.Utilities.UnifiedRandom(711); var priorRandom = Main.rand;
            forbiddenCalls = 0;
            try
            {
                foreach (var method in methods)
                {
                    Require(method != null, "fixed native read-only guard target exists");
                    guard.Patch(method, new HarmonyMethod(typeof(NativeInformationChecks).GetMethod(nameof(RejectUnexpectedOperation), Flags)));
                    Require(Harmony.GetPatchInfo(method).Owners.Contains(guard.Id), "guard is installed on actual native method");
                }
                Main.rand = random; check();
                Require(forbiddenCalls == 0 && ReferenceEquals(Main.rand, random) && random.Next() == expected.Next() && Main.LocalPlayer.luck == .2f && Main.LocalPlayer.torchLuck == 1,
                    "real source path cannot recalculate, use chat/NPC fallback, write luck or consume native RNG");
            }
            finally { foreach (var method in methods) if (method != null) guard.Unpatch(method, HarmonyPatchType.All, guard.Id); Main.rand = priorRandom; }
        }
        private static void CheckObservationAndHud(object context, object host, object readiness)
        {
            CheckDemandStates(context, host);
            var reader = Get(host, "source"); var hud = Get(host, "Hud");
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Microsoft.Xna.Framework.Matrix.Identity);
            Main.netMode = 1; NPC.savedWizard = NPC.savedAngler = true;
            Main.npc = new NPC[200]; Main.npc[0] = new NPC { active = true, type = Terraria.ID.NPCID.Dryad };
            Main.LocalPlayer.anglerQuestsFinished = 12; Main.LocalPlayer.luck = .2f; Main.LocalPlayer.torchLuck = 1;
            foreach (InformationKind kind in new[] { InformationKind.Infection, InformationKind.Luck, InformationKind.Angler }) Call(host, "SetEnabled", kind, true);
            Call(context, "UpdateRuntime"); Call(host, "PrepareHud");
            Require((bool)Get(hud, "Visible") && ((JueMingR.Features.Information.LuckSummary)Get(host, "Luck")).Content.Text.Contains("+0.2"), "workload must exercise visible populated real HUD before counting stable work");
            int reads = (int)Get(reader, "LocalizationReads"), queries = (int)Get(reader, "NpcQueries"), visits = (int)Get(reader, "NpcVisits");
            int measures = (int)Get(hud, "Measurements"), layouts = (int)Get(hud, "LayoutBuilds");
            int luckBuilds = ((JueMingR.Features.Information.LuckSummary)Get(host, "Luck")).TextBuilds;
            for (int i = 0; i < 100; i++) { Call(context, "UpdateRuntime"); Call(host, "PrepareHud"); }
            Require((int)Get(reader, "NpcQueries") - queries == 100 && (int)Get(reader, "NpcVisits") - visits == 100, "three enabled summaries share one demand-limited NPC traversal per update");
            Require((int)Get(reader, "LocalizationReads") == reads && (int)Get(hud, "Measurements") == measures && (int)Get(hud, "LayoutBuilds") == layouts &&
                ((JueMingR.Features.Information.LuckSummary)Get(host, "Luck")).TextBuilds == luckBuilds, "stable real Host reader/content/HUD does no localization, format or measurement rebuild");
            Call(host, "SetColor", InformationKind.Luck, 0x112233); Call(host, "SetPosition", new JueMingR.Platform.Settings.WindowPosition(40, 50)); Call(host, "PrepareHud");
            Require((int)Get(hud, "Measurements") == measures, "color and position must reuse real HUD glyph layout");
            Call(host, "StepSize", InformationKind.Luck, 1); Call(host, "PrepareHud");
            Require((int)Get(hud, "LayoutBuilds") == layouts + 1 && (int)Get(hud, "Measurements") > measures, "only resized block rebuilds");
            Call(hud, "Prepare", Terraria.GameContent.FontAssets.MouseText.Value, 240f, 48f, false, null);
            foreach (object block in (Array)Get(hud, "blocks"))
                if ((int)Get(block, "VisibleLines") > 0) Require((float)Get(block, "OffsetY") + (int)Get(block, "VisibleLines") * (float)Get(block, "LineHeight") <= 48,
                    "actual tiny-viewport packet cannot paint below its hit/projection rectangle");
            Call(host, "PrepareHud");
            Set(readiness, "Installed", false); Call(context, "UpdateRuntime");
            string angler = ((JueMingR.Features.Information.AnglerSummary)Get(host, "Angler")).Content.Text;
            Require(angler.Contains("累计完成：12") && angler.Contains("不可用"), "real reader keeps local count when source observer unavailable");
            Set(readiness, "Installed", true);
            NativeInformationLocalizationChecks.Run(context, host);
            foreach (InformationKind kind in new[] { InformationKind.Biome, InformationKind.Infection, InformationKind.Luck, InformationKind.Angler }) Call(host, "SetEnabled", kind, false);
            Call(context, "UpdateRuntime"); Call(host, "PrepareHud");
            reads = (int)Get(reader, "LocalizationReads"); queries = (int)Get(reader, "NpcQueries"); int scalars = (int)Get(reader, "ScalarSamples"); measures = (int)Get(hud, "Measurements");
            for (int i = 0; i < 100; i++) { Call(context, "UpdateRuntime"); Call(host, "PrepareHud"); }
            Require(!(bool)Get(hud, "Visible") && (int)Get(reader, "LocalizationReads") == reads && (int)Get(reader, "NpcQueries") == queries &&
                (int)Get(reader, "ScalarSamples") == scalars && (int)Get(hud, "Measurements") == measures, "all-off actual consumer performs zero summary queries, sampling and glyph measurement");
            Console.WriteLine("PASS: real Host demand traversal, stable/dirty/off work, partial facts and all native en-US/zh-Hans quest locations; no GPU or game loop.");
        }
        private static void CheckDemandStates(object context, object host)
        {
            var reader = Get(host, "source"); Main.netMode = 0; NPC.savedWizard = NPC.savedAngler = false; Main.npc = new NPC[200];
            foreach (InformationKind kind in new[] { InformationKind.Infection, InformationKind.Luck, InformationKind.Angler }) Call(host, "SetEnabled", kind, false);
            foreach (InformationKind kind in new[] { InformationKind.Infection, InformationKind.Luck, InformationKind.Angler })
            {
                Call(host, "SetEnabled", kind, true);
                int queries = (int)Get(reader, "NpcQueries"), samples = (int)Get(reader, "ScalarSamples"), localizations = (int)Get(reader, "LocalizationReads");
                Call(context, "UpdateRuntime");
                Require(((string)Call(host, "Text", kind)).Contains("需要") && (int)Get(reader, "NpcQueries") == queries + 1 &&
                    (int)Get(reader, "ScalarSamples") == samples && (int)Get(reader, "LocalizationReads") == localizations, "single enabled row with proven absent NPC samples no unrelated business: " + kind);
                Main.netMode = 1; Call(context, "UpdateRuntime");
                Require(((string)Call(host, "Text", kind)).Contains(kind == InformationKind.Infection ? "需要当前活动树妖" : "等待"), "current Dryad absence differs from ordinary client's unconfirmed saved history: " + kind);
                Main.netMode = 0; Main.npc = null; Call(context, "UpdateRuntime");
                Require(((string)Call(host, "Text", kind)).Contains("不可用"), "missing native NPC table cannot certify absence: " + kind);
                Main.npc = new NPC[200]; Call(host, "SetEnabled", kind, false);
            }
            NPC.savedWizard = true; Call(host, "SetEnabled", InformationKind.Luck, true);
            int before = (int)Get(reader, "NpcQueries"); Call(context, "UpdateRuntime");
            Require((int)Get(reader, "NpcQueries") == before, "saved Wizard independently satisfies single luck display without scanning NPCs");
            Call(host, "SetEnabled", InformationKind.Luck, false);
        }
        private static void CheckReceipts(object readiness)
        {
            Main.netMode = 1;
            Netplay.Connection.State = 10;
            var buffer = new MessageBuffer { whoAmI = 256 }; NetMessage.buffer[256] = buffer;
            Action reset = () => { Call(readiness, "BeginClear"); Call(readiness, "EndClear"); };
            Action<int, int> receive = (id, length) => { buffer.readBuffer[0] = (byte)id; buffer.readBuffer[1] = buffer.readBuffer[2] = buffer.readBuffer[3] = 0; int type; buffer.GetData(0, length, out type); };
            reset(); receive(57, 3);
            Require(!(bool)Get(Call(readiness, "Snapshot"), "Infection"), "truncated declared payload must not certify zero from residual buffer bytes");
            var skip = new Harmony("JueMingR.NativeInformation.TestSkip");
            var original = typeof(MessageBuffer).GetMethod("GetData");
            skip.Patch(original, new HarmonyMethod(typeof(NativeInformationChecks).GetMethod(nameof(SkipMessage), Flags)));
            try { receive(57, 4); Require(!(bool)Get(Call(readiness, "Snapshot"), "Infection"), "another prefix skipping the original cannot publish readiness"); }
            finally { skip.Unpatch(original, HarmonyPatchType.All, skip.Id); }
            buffer.reader = new BinaryReader(new MemoryStream(new byte[0]));
            try { receive(57, 4); throw new Exception("expected native truncated-reader failure"); }
            catch (EndOfStreamException) { Require(!(bool)Get(Call(readiness, "Snapshot"), "Infection"), "throwing native read is not a successful receipt"); }
            finally { buffer.reader.Dispose(); buffer.reader = null; }
            Main.netMode = 0; receive(57, 4);
            Require(!(bool)Get(Call(readiness, "Snapshot"), "Infection"), "native non-client early return does not publish readiness");
            Main.netMode = 1; reset(); receive(57, 4);
            Require((bool)Get(Call(readiness, "Snapshot"), "Infection") && WorldGen.tGood == 0 && WorldGen.tEvil == 0 && WorldGen.tBlood == 0,
                "real native successful zero is ready even before enabled display or R Session");
            buffer.readBuffer[0] = 57; var oldReceipt = Call(readiness, "BeginMessage", buffer, 0, 4); reset(); Call(readiness, "AppliedMessage", oldReceipt, 57);
            Require(!(bool)Get(Call(readiness, "Snapshot"), "Infection"), "old world receipt cannot repopulate a cleared epoch");
            receive(74, 3);
            Require((bool)Get(Call(readiness, "Snapshot"), "Quest") && (bool)Get(Call(readiness, "Snapshot"), "Today") && Main.anglerQuest == 0 && !Main.anglerQuestFinished,
                "real native 74 makes index zero and false meaningful");
            receive(57, 4);
            Console.WriteLine("PASS: actual T8 GetData + installed success hooks cover early return, skipped body, throw, malformed length, valid zero and old epoch.");
        }
    }
}
