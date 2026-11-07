using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.HiddenFeatures;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using JourneyFixture = NativeWorldTextProbe.NativeJourneyCloseAdmissionChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRuntimeFailureChecks
    {
        private const BindingFlags Flags = JourneyFixture.Flags;
        private static void Require(bool condition, string message) { JourneyFixture.Require(condition, message); }
        private static FileStream heldEvidence;
        private static bool fifthEntered, fifthIoFailed;
        internal static void RunInitial(object context)
        {
            Type worker = context.GetType().DeclaringType;
            Type writer = worker.Assembly.GetType("JueMingR.TerrariaHost.EvidenceWriter", true);
            string path = (string)Get(context, "EvidencePath"), package = (string)Get(context, "PackageId");
            string utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            File.WriteAllText(path, "PHASE0S|1|" + package + "|01|TERRARIA_ASSEMBLY_READY|" + utc + "|1" + Environment.NewLine);
            writer.GetMethod("AppendEvent", Flags).Invoke(null, new object[] { path, package, 2, "HARMONY_READY" });
            writer.GetMethod("AppendEvent", Flags).Invoke(null, new object[] { path, package, 3, "HOOK_INSTALLED" });
            var values = new Dictionary<string, object>();
            foreach (string field in new[] { "postfixContext", "hookCommitted", "postfixGate", "handoffGate", "biomeFeatureFailed", "f5LayersReady" })
                values[field] = worker.GetField(field, Flags).GetValue(null);
            var fault = new Harmony("JueMingR.Tests.SharedRuntimeFifthEvent"); fifthEntered = fifthIoFailed = false;
            try
            {
                fault.Patch(writer.GetMethod("AppendEvent", Flags), prefix: new HarmonyMethod(typeof(NativeRuntimeFailureChecks), nameof(BeforeAppend)), finalizer: new HarmonyMethod(typeof(NativeRuntimeFailureChecks), nameof(AfterAppend)));
                worker.GetField("postfixContext", Flags).SetValue(null, context); worker.GetField("hookCommitted", Flags).SetValue(null, 1);
                worker.GetField("postfixGate", Flags).SetValue(null, 0); worker.GetField("handoffGate", Flags).SetValue(null, 0);
                worker.GetField("biomeFeatureFailed", Flags).SetValue(null, 0); worker.GetField("f5LayersReady", Flags).SetValue(null, false);
                var layers = new List<GameInterfaceLayer>();
                foreach (string name in new[] { "Vanilla: Ingame Options", "Vanilla: Achievement Complete Popups", "Vanilla: Cursor", "Vanilla: Mouse Over", "Vanilla: Interface Logic 4" })
                    layers.Add(new LegacyGameInterfaceLayer(name, () => true, InterfaceScaleType.UI));
                worker.GetMethod("Postfix", Flags).Invoke(null, new object[] { layers });
                Require(fifthEntered && fifthIoFailed && Get(context, "Journey") != null && Get(context, "Shell") != null,
                    "fifth event fails in the real FileStream open after complete composition, not before Journey exists");
                object runtime = Get(Get(context, "Runtime"), "SharedRuntime");
                Require((long)Get(runtime, "Generation") == 0, "initial failure scene has no existing Journey or session baseline");
                JourneyFixture.Prepare(context);
                object journey = Get(context, "Journey");
                // This is the same command owner used by the hidden row and
                // binding. Unlock after the failed handoff cannot resurrect it.
                Call(Get(context, "Shell"), "ToggleJourneyFromHotkey");
                Require(!(bool)Get(journey, "IsActive") && !(bool)Get(journey, "CanEnable"), "fifth-event failure remains terminal through later real Update/Shell/Journey consumers");
                Require((bool)Get(Get(context, "Shell"), "Failed"), "common failure closes the shared Shell");
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("other-isolated-world.wld", false) { UniqueId = Guid.NewGuid() };
                Call(context, "UpdateRuntime");
                Require(!(bool)Get(runtime, "IsSessionActive") && !(bool)Call(journey, "SetEnabled", true), "new world cannot restart a failed shared runtime");
                Console.WriteLine("PASS shared initial failure: real fifth append IO fault, complete composition, no prior baseline and terminal later Update/commands/new world.");
            }
            finally
            {
                heldEvidence?.Dispose(); heldEvidence = null;
                fault.Unpatch(writer.GetMethod("AppendEvent", Flags), HarmonyPatchType.All, fault.Id);
                foreach (var value in values) worker.GetField(value.Key, Flags).SetValue(null, value.Value);
            }
        }

        internal static void RunActive(object context)
        {
            JourneyFixture.Prepare(context); object journey = Get(context, "Journey"), runtime = Get(Get(context, "Runtime"), "SharedRuntime");
            Require((bool)Call(journey, "SetEnabled", true), "running scene owns a real Journey baseline before fault");
            var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>();
            bool baseline = god.IsEnabledForPlayer(Main.myPlayer); god.SetEnabledState(Main.myPlayer, !baseline);
            Type worker = context.GetType().DeclaringType;
            worker.GetMethod("HandlePostfixFailure", Flags).Invoke(null, new object[] { context, "RUNTIME", new IOException("controlled shared callback fault") });
            Require(!(bool)Get(journey, "IsActive") && god.IsEnabledForPlayer(Main.myPlayer) == baseline,
                "running common failure ends active Journey and returns its personal baseline");
            JourneyFixture.Frame(context); Call(Get(context, "Shell"), "ToggleJourneyFromHotkey");
            Require(!(bool)Get(runtime, "IsSessionActive") && !(bool)Get(journey, "IsActive"), "post-fault input and update never activate a new Journey");
            Console.WriteLine("PASS running shared failure: existing Journey personal baseline returned and later input/updates remain terminal.");
        }
        private static void BeforeAppend(string __0, int __2)
        {
            if (__2 != 5) return;
            fifthEntered = true;
            heldEvidence = new FileStream(__0, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        private static Exception AfterAppend(Exception __exception)
        {
            if (heldEvidence != null) { fifthIoFailed |= __exception is IOException; heldEvidence.Dispose(); heldEvidence = null; }
            return __exception;
        }
    }
}
