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
                Require(Get(renderer, "InformationControls") != null, "real shell must route information commands");
                var controls = Get(renderer, "InformationControls");
                var command = assembly.GetType("JueMingR.TerrariaHost.F5.F5Command", true);
                Call(controls, "Execute", Enum.Parse(command, "EnableLuck"));
                Require((bool)Call(information, "Enabled", InformationKind.Luck), "actual F5 command must update the owning document");
                Require((bool)Get(Call(readiness, "Snapshot"), "Infection"), "initial successful native data survives later Session composition");
                Console.WriteLine("PASS: built full Host composes independent information controls and preference command.");
            }
            finally
            {
                var hooks = GetOptional(context, "informationHooks"); if (hooks != null) Call(hooks, "Dispose");
                foreach (string name in new[] { "Information", "Labels", "WorldTargets", "WorldObjects", "items", "notes", "preferences" })
                {
                    var owner = GetOptional(context, name);
                    (owner?.GetType().GetMethod("OnExit", Flags) ?? owner?.GetType().GetMethod("OnProcessExit", Flags))?.Invoke(owner, new object[] { null, EventArgs.Empty });
                }
                var shell = GetOptional(context, "Shell"); if (shell != null) { var hotkeys = GetOptional(shell, "hotkeys"); if (hotkeys != null) Call(hotkeys, "OnExit", null, EventArgs.Empty); }
                Main.netMode = 0; Main.gameMenu = false;
            }
        }
        internal static object Get(object value, string name) { return GetOptional(value, name) ?? throw new InvalidOperationException("Required consumer missing: " + name); }
        internal static object GetOptional(object value, string name) { var type = value.GetType(); return type.GetProperty(name, Flags)?.GetValue(value) ?? type.GetField(name, Flags)?.GetValue(value); }
        internal static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
        internal static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static bool SkipMessage() { return false; }
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
