using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.WorldTargets;
using Terraria.UI;

namespace NativeWorldTextProbe
{
    internal static class NativeLayerReadinessChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run()
        {
            string root = Path.Combine(Terraria.Program.SavePath, "layer-readiness");
            string config = Path.Combine(root, "JueMingRData", "config", "features"); Directory.CreateDirectory(config);
            File.WriteAllBytes(Path.Combine(config, "entity-labels.json"), new EntityLabelCodec().Encode(EntityLabelSettings.Default.WithEnabled(EntityLabelKind.Enemy, true)));
            File.WriteAllBytes(Path.Combine(config, "world-targets.json"), new WorldTargetCodec().Encode(WorldTargetSettings.Default.WithEnabled(WorldTargetKind.LifeCrystal, true)));
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts", "build", "Debug", "work", "bin", "JueMingR.TerrariaHost", "x86", "Debug", "net472", "JueMingR.TerrariaHost.dll"));
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            var context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null, new object[] { "world-object-text-" + new string('6', 40), Path.Combine(root, "evidence.txt"), root }, null);
            Call(context, "InitializeRuntime", true); worker.GetField("postfixContext", Flags).SetValue(null, context);
            object labels = Get(context, "Labels"), targets = Get(context, "WorldTargets"), objects = Get(context, "WorldObjects");
            var messages = new List<string>(); Action<string> display = messages.Add;
            Action feedback = () => { Call(labels, "TakeFeedback", display); Call(targets, "TakeFeedback", display); };
            try
            {
                Require(SpinWait.SpinUntil(() => (bool)Get(Get(labels, "Preferences"), "IsLoaded") && (bool)Get(Get(targets, "Preferences"), "IsLoaded"), 5000), "isolated preference completion");
                Call(labels, "PollPreferences"); Call(targets, "PollPreferences");
                Require((bool)Get(labels, "Enabled") && (bool)Get(targets, "Enabled"), "saved enabled settings are the first-entry precondition");
                // Update feedback can precede the first native draw setup. This
                // must not announce a failure merely because readiness is false.
                feedback(); Require(messages.Count == 0, "first feedback before draw setup must not report unavailable; actual: " + String.Join(" | ", messages));
                var ensure = worker.GetMethod("EnsureEntityLayer", Flags);
                ensure.Invoke(null, new object[] { null, false });
                for (int i = 0; i < 100; i++) feedback();
                Require(messages.Count == 0, "handoff with no native layer list remains pending without a timer");
                ensure.Invoke(null, new object[] { ValidLayers(), true });
                feedback();
                Require((bool)Get(labels, "LayersReady") && (bool)Get(targets, "LayersReady") && (bool)Get(objects, "LayersReady") && messages.Count == 0, "first draw setup publishes readiness to all three consumers without a stale alert");
                ensure.Invoke(null, new object[] { new List<GameInterfaceLayer>(), true });
                feedback(); feedback();
                Require(messages.Count == 2 && messages[0] == "显名绘制层不可用，选择已保留。" && messages[1] == "附近目标绘制层不可用，选择已保留。", "a completed setup with a missing anchor still reports one real failure per consumer");
                Require(!(bool)Get(labels, "LayersReady") && !(bool)Get(targets, "LayersReady") && !(bool)Get(objects, "LayersReady"), "real failure disables every shared drawing consumer");
                // F5 suppresses feedback while hideUI/menu is active. Recovery
                // must reset the layer alert even if nobody consumes that state.
                ensure.Invoke(null, new object[] { ValidLayers(), true });
                ensure.Invoke(null, new object[] { new List<GameInterfaceLayer>(), true }); feedback(); feedback();
                Require(messages.Count == 4, "unobserved recovery ends alert deduplication so a later real failure is still reported once");
                Require((bool)Get(labels, "Enabled") && (bool)Get(targets, "Enabled"), "readiness transitions preserve saved choices");
                ensure.Invoke(null, new object[] { ValidLayers(), true }); feedback();
                Console.WriteLine("PASS: built Host first-entry pending/ready/failure feedback, shared consumer readiness, recovery and preserved choices; no graphics device or game loop.");
            }
            finally
            {
                foreach (string name in new[] { "Labels", "WorldTargets", "WorldObjects", "items", "notes", "preferences" })
                { var host = Get(context, name); host?.GetType().GetMethod("OnExit", Flags)?.Invoke(host, new object[] { null, EventArgs.Empty }); }
                var hotkeys = Get(Get(context, "Shell"), "hotkeys"); Call(hotkeys, "OnExit", null, EventArgs.Empty);
                worker.GetField("postfixContext", Flags).SetValue(null, null);
            }
        }
        private static List<GameInterfaceLayer> ValidLayers()
        { return new List<GameInterfaceLayer> { new LegacyGameInterfaceLayer("Vanilla: Ingame Options", () => true, InterfaceScaleType.UI) }; }
        private static object Get(object value, string name)
        { var field = value.GetType().GetField(name, Flags); return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, Flags).GetValue(value, null); }
        private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Native readiness: " + reason); }
    }
}
