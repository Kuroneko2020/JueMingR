using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Terraria
{
    // Separate production Host assembly loaded through Bootstrap, not just the
    // linked adapter. Fixtures remain isolated and never execute native gameplay.
    internal static class WorldTargetHostChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] oldIds = { "biome-display.toggle", "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle",
            "entity-labels.enemy.toggle", "entity-labels.critter.toggle", "entity-labels.npc.toggle" };
        private static readonly string[] ids = { "world-targets.life-crystal.toggle", "world-targets.life-fruit.toggle", "world-targets.mana-crystal.toggle",
            "world-targets.sleeping-digtoise.toggle", "world-targets.chillet-egg.toggle" };
        private static readonly Keys[] keys = { Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F6 };
        internal static void Prepare(string mode)
        {
            if (mode != "expect-world-targets-runtime") return;
            string root = Path.GetDirectoryName(typeof(Main).Assembly.Location);
            Check(root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && root.Contains("JueMingR-Phase0S-Test-"), "isolated root before preparing files");
            string path = Path.Combine(root, "JueMingRData", "config", "hotkeys.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string[] oldKeys = { "K", "J", "L", "O", "U", "I", "P" };
            var entries = oldIds.Select((id, i) => new KeyValuePair<string, string>(id, oldKeys[i])).ToList();
            entries.Add(new KeyValuePair<string, string>("future.unknown", "N")); File.WriteAllBytes(path, HotkeyDocument.Encode(new HotkeyDocument(entries)));
        }
        internal static void Run(Main main, string mode)
        {
            HostInputChecks.ConfigureLoadedHost(); FocusHelper.IsSelectedApplication = true;
            Main.SampleKeys = new Keys[0]; Main.SampleLeft = Main.SampleRight = Main.SampleF5 = false;
            Main.SampleX = 1850; Main.SampleY = 900; Main.SampleWheel = 0;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost");
            object context = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true).GetField("postfixContext", Flags).GetValue(null);
            object host = Get(context, "WorldTargets"), shell = Get(context, "Shell"), hotkeys = Get(shell, "hotkeys"), labels = Get(context, "Labels");
            var feature = (WorldTargetFeature)Get(host, "Feature"); var labelFeature = (EntityLabelFeature)Get(labels, "Feature");
            var bindings = (HotkeyBindings)Get(hotkeys, "Bindings"); var registry = (HotkeyRegistry)Get(hotkeys, "Registry");
            object controls = Get(Get(shell, "renderer"), "WorldControls");
            Action frame = () => main.RunUpdateLoop(1);
            Action<Keys> press = key => { Main.SampleKeys = new[] { key }; frame(); Main.SampleKeys = new Keys[0]; frame(); };
            Action<Func<bool>> wait = test => { for (int i = 0; i < 500 && !test(); i++) { frame(); Thread.Sleep(2); } Check(test(), "async/scan readiness"); };
            wait(() => bindings.Loaded && (bool)Get(Get(host, "Preferences"), "IsLoaded"));
            WorldTargetObservationChecks.Prepare(); Main.LocalPlayer.accOreFinder = false; main.SetupAndDrawBiomeLayer(); frame(); frame();
            Check(registry.Actions.Count == 12 && oldIds.All(id => bindings.Get(id) != null) && controls != null, "real registry has original seven and five new actions");
            Check((bool)Get(host, "ControlsEnabled") && (bool)Get(host, "LayersReady") && !(bool)Get(shell, "Failed"), "no detector still permits real controls with composed world layer");
            var layers = (List<UI.GameInterfaceLayer>)Get(main, "_gameInterfaceLayers");
            int layer = layers.FindIndex(l => l.Name == "JueMingR: Entity Labels");
            Check(layers.Count(l => l.Name == "JueMingR: Entity Labels") == 1 && layers[layer].ScaleType == UI.InterfaceScaleType.Game && layers[layer + 1].Name == "Vanilla: Ingame Options", "one shared Game layer below native UI");
            bool first = mode == "expect-world-targets-runtime";
            if (first)
            {
                Check(!Value(host).AnyEnabled && ids.All(id => bindings.Get(id) == null), "five initially off/unbound");
                Check(bindings.Validate(ids[0], bindings.Get(oldIds[0])) != null, "new-old duplicate rejected by same registry");
            }
            else Check(!Value(host).AnyEnabled && Value(host).Color(WorldTargetKind.LifeCrystal) == 0xABCDEF && ids.All(id => bindings.Get(id) != null), "normal restart restores independent colors and bindings without enabling");
            // First complete path is the life crystal; then use the same wiring
            // for the other four, never a separate prototype scanner/UI.
            string[] enables = { "EnableLifeCrystal", "EnableLifeFruit", "EnableManaCrystal", "EnableDigtoise", "EnableChilletEgg" };
            int[] tileIds = { 12, 236, 639, 751, 752 };
            for (int i = 0; i < ids.Length; i++)
            {
                if (first)
                {
                    HotkeyChord chord; string reason; long command;
                    Check(HotkeyChord.TryParse(keys[i].ToString(), out chord, out reason) && bindings.TrySet(ids[i], chord, null, out command, out reason), "normal public binding submission");
                    wait(() => !bindings.Busy); Check(bindings.Get(ids[i]) != null, "reliable binding becomes effective after save");
                }
                Main.LocalPlayer.accOreFinder = false; Command(controls, enables[i]); frame();
                Check(Value(host).Enabled((WorldTargetKind)i) && feature.Targets.Count == 0, "row keeps intent while ability absent");
                WorldTargetObservationChecks.Put(4 + i * 4, 4, tileIds[i], i == 1 ? 2 : 0);
                Main.LocalPlayer.accOreFinder = true; wait(() => feature.Targets.Count == i + 1);
                press(keys[i]); Check(!Value(host).Enabled((WorldTargetKind)i), "public hotkey toggles row state");
                press(keys[i]); wait(() => feature.Targets.Count == i + 1);
            }
            host.GetType().GetMethod("SetColor", Flags).Invoke(host, new object[] { WorldTargetKind.LifeCrystal, 0xABCDEF }); frame();
            Check(Value(host).Color(WorldTargetKind.LifeCrystal) == 0xABCDEF, "real color command consumer updates target preferences");
            Main.npc = new NPC[Main.maxNPCs]; Main.npc[0] = new NPC { active = true, friendly = false, type = 1, netID = 1, TypeName = "测试史莱姆", position = new Vector2(200, 250) };
            press(Keys.U); Check(labelFeature.Labels.Count == 1 && feature.Targets.Count == 5, "old label action and new target observation coexist"); press(Keys.U);
            Main.drawingPlayerChat = true; bool intent = Value(host).Enabled(WorldTargetKind.LifeCrystal); press(keys[0]);
            Check(Value(host).Enabled(WorldTargetKind.LifeCrystal) == intent, "text editing blocks target hotkey dispatch"); Main.drawingPlayerChat = false;
            long generation = (long)Get(host, "SessionGeneration"); Main.LocalPlayer.dead = true; frame();
            Check(feature.Targets.Count == 5 && (long)Get(host, "SessionGeneration") == generation, "death does not replace shared world session");
            Main.LocalPlayer.dead = false; Main.netMode = 1; wait(() => feature.Targets.Count == 5);
            Main.sectionManager.Unknown.Add(((long)20 << 32) | 4); frame(); Check(feature.Targets.Count == 4, "loaded Host withdraws unreadable egg this update");
            Main.sectionManager.Unknown.Clear(); Main.tile[16, 4].Active = false; frame(); Check(feature.Targets.Count <= 4, "removed sleeping turtle never tracks summon/item");
            Main.gameMenu = true; frame(); Check(feature.Targets.Count == 0, "world exit clears output"); Main.gameMenu = false; frame();
            foreach (WorldTargetKind kind in Enum.GetValues(typeof(WorldTargetKind))) host.GetType().GetMethod("SetEnabled", Flags).Invoke(host, new object[] { kind, false });
            frame(); int reads = Main.sectionManager.Reads; for (int i = 0; i < 20; i++) frame();
            Check(feature.Targets.Count == 0 && Main.sectionManager.Reads == reads, "actual all-off Host stops tile reads");
            var entries = HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location), "JueMingRData", "config", "hotkeys.json"))).Entries;
            Check(entries.Count == 13 && entries.Any(e => e.Key == "future.unknown" && e.Value == "N") && oldIds.All(id => entries.Any(e => e.Key == id)), "all seven old and unknown entries preserved by new saves");
            Check(!feature.HasFailed && !(bool)Get(shell, "Failed") && !labelFeature.HasFailed, "all composed owners healthy");
            Console.WriteLine("PASS: " + mode + " production Host, 12 actions, five tile outputs, row/color consumers, text protection, old labels, death/client/exit and reload.");
        }
        private static WorldTargetSettings Value(object host) { return (WorldTargetSettings)Get(Get(host, "Preferences"), "Value"); }
        private static void Command(object controls, string name)
        { var method = controls.GetType().GetMethod("Execute", Flags); method.Invoke(controls, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, name) }); }
        private static object Get(object target, string name) { var field = target.GetType().GetField(name, Flags); return field != null ? field.GetValue(target) : target.GetType().GetProperty(name, Flags).GetValue(target, null); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("World target loaded Host: " + message); }
    }
}
