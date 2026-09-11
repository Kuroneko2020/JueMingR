using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Terraria
{
    // Actual bootstrap-loaded Host/Settings/Shell/registry. The independent
    // executable supplies game facts; native AI and multiplayer are not proved.
    internal static class EntityHostChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] oldIds = { "biome-display.toggle", "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle" };
        private static readonly string[] ids = { "entity-labels.enemy.toggle", "entity-labels.critter.toggle", "entity-labels.npc.toggle" };
        private static readonly Keys[] keys = { Keys.U, Keys.I, Keys.P };
        internal static void Prepare(string mode)
        {
            int abiErrors = 0;
            AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
            {
                if ((args.Exception is MissingFieldException || args.Exception is MissingMethodException || args.Exception is TypeLoadException) && abiErrors++ < 8)
                    Console.WriteLine("FIXTURE_ABI: " + args.Exception);
            };
            if (mode != "expect-entities-runtime") return;
            string root = Path.GetDirectoryName(typeof(Main).Assembly.Location);
            Check(root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && root.Contains("JueMingR-Phase0S-Test-"), "isolated loaded-host root before any preference read");
            string path = Path.Combine(root, "JueMingRData", "config", "hotkeys.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var entries = new List<KeyValuePair<string, string>>(); string[] oldKeys = { "K", "J", "L", "O" };
            for (int i = 0; i < 4; i++) entries.Add(new KeyValuePair<string, string>(oldIds[i], oldKeys[i]));
            entries.Add(new KeyValuePair<string, string>("future.unknown", "N"));
            File.WriteAllBytes(path, HotkeyDocument.Encode(new HotkeyDocument(entries)));
        }
        internal static void Run(Main main, string mode)
        {
            HostInputChecks.ConfigureLoadedHost(); FocusHelper.IsSelectedApplication = true;
            Main.SampleKeys = new Keys[0]; Main.SampleLeft = Main.SampleRight = Main.SampleF5 = false;
            Main.SampleX = 1850; Main.SampleY = 900; Main.SampleWheel = 0;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost");
            object context = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true).GetField("postfixContext", Flags).GetValue(null);
            object host = Get(context, "Labels"), shell = Get(context, "Shell"), hotkeys = Get(shell, "hotkeys");
            var bindings = (HotkeyBindings)Get(hotkeys, "Bindings"); var registry = (HotkeyRegistry)Get(hotkeys, "Registry");
            var feature = (EntityLabelFeature)Get(host, "Feature"); object renderer = Get(shell, "renderer"), controls = Get(renderer, "EntityControls");
            Action frame = () => main.RunUpdateLoop(1);
            Action<Keys> press = key => { Main.SampleKeys = new[] { key }; frame(); Main.SampleKeys = new Keys[0]; frame(); };
            Action<Func<bool>> wait = test => { for (int i = 0; i < 500 && !test(); i++) { frame(); Thread.Sleep(2); } Check(test(), "async readiness"); };
            wait(() => bindings.Loaded && (bool)Get(Get(host, "Preferences"), "IsLoaded"));
            main.SetupAndDrawBiomeLayer(); frame(); frame();
            Check(registry.Actions.Count == 7 && oldIds.All(id => bindings.Get(id) != null) && controls != null, "one real registry retains four old consumers and adds three");
            Check((bool)Get(host, "LayersReady") && !(bool)Get(shell, "Failed"), "actual world layer anchor and F5 composed");
            var layers = (List<UI.GameInterfaceLayer>)Get(main, "_gameInterfaceLayers");
            int layerIndex = layers.FindIndex(l => l.Name == "JueMingR: Entity Labels");
            Check(layerIndex >= 0 && layers[layerIndex + 1].Name == "Vanilla: Ingame Options" && layers[layerIndex].ScaleType == UI.InterfaceScaleType.Game, "one actual Game-scale layer precedes native options");
            if (mode == "expect-entities-runtime")
            {
                Check(!Value(host).AnyEnabled && ids.All(id => bindings.Get(id) == null), "first three defaults off and unbound");
                Check(bindings.Validate(ids[0], bindings.Get(oldIds[0])) != null, "new/old actions share duplicate protection");
                for (int i = 0; i < 3; i++)
                {
                    HotkeyChord chord; string reason; long command;
                    Check(HotkeyChord.TryParse(keys[i].ToString(), out chord, out reason) && bindings.TrySet(ids[i], chord, null, out command, out reason), "submit public binding");
                    wait(() => !bindings.Busy); Check(bindings.Get(ids[i]) != null, "new binding effective only after actual save");
                }
            }
            else Check(Value(host).NpcMode == NpcLabelMode.Off && Value(host).LastNpcMode == NpcLabelMode.Type, "normal restart restores last NPC mode without auto enabling");
            Main.npc = new NPC[Main.maxNPCs];
            Main.npc[0] = Npc(1, "史莱姆"); Main.npc[1] = Npc(46, "兔子"); Main.npc[1].CountsAsACritter = true;
            Main.npc[2] = Npc(17, "商人"); Main.npc[2].townNPC = true; Main.npc[2].GivenName = "史蒂夫";
            press(keys[0]); press(keys[1]); Command(controls, "NpcName"); frame();
            Check(feature.Labels.Count == 3 && feature.Labels[0].Health == "100/100" && feature.Labels[2].Name == "史蒂夫", "loaded Host input and row consumer produce actual labels together");
            Command(controls, "NpcType"); frame(); press(keys[2]); Check(Value(host).NpcMode == NpcLabelMode.Off, "NPC hotkey disables button-selected mode");
            press(keys[2]); Check(Value(host).NpcMode == NpcLabelMode.Type && feature.Labels[2].Name == "商人", "same public command restores Type");
            long generation = (long)Get(host, "SessionGeneration"); Main.LocalPlayer.dead = true; frame();
            Check(feature.Labels.Count == 3 && (long)Get(host, "SessionGeneration") == generation, "dead valid world still displays without replacing session");
            Main.LocalPlayer.dead = false; Main.netMode = 1; frame();
            Check(feature.Labels.Count == 3, "read-only client topology uses same actual adapter");
            Main.gameMenu = true; frame(); Check(feature.Labels.Count == 0, "world exit clears all emitted labels");
            Main.gameMenu = false; frame();
            Command(controls, "DisableEnemy"); Command(controls, "DisableCritter"); Command(controls, "DisableNpc"); frame();
            int grouping = feature.GroupingPasses; for (int i = 0; i < 100; i++) frame();
            Check(feature.Labels.Count == 0 && feature.GroupingPasses == grouping, "all off performs no grouping or world output");
            var entries = HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location), "JueMingRData", "config", "hotkeys.json"))).Entries;
            Check(entries.Count == 8 && entries.Any(e => e.Key == "future.unknown" && e.Value == "N") && oldIds.All(id => entries.Any(e => e.Key == id)), "new saves preserve all old and future entries");
            Check(!feature.HasFailed && !(bool)Get(shell, "Failed"), "actual composed owners stay healthy");
            Console.WriteLine("PASS: " + mode + " loaded entity Host, seven public actions, row consumers, labels, death/client/exit and isolated persistence.");
        }
        private static NPC Npc(int type, string name) { return new NPC { active = true, friendly = false, type = type, netID = type, TypeName = name, position = new Vector2(200, 250) }; }
        private static EntityLabelSettings Value(object host) { return (EntityLabelSettings)Get(Get(host, "Preferences"), "Value"); }
        private static void Command(object controls, string name)
        { var method = controls.GetType().GetMethod("Execute", Flags); method.Invoke(controls, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, name) }); }
        private static object Get(object target, string name) { var field = target.GetType().GetField(name, Flags); return field != null ? field.GetValue(target) : target.GetType().GetProperty(name, Flags).GetValue(target, null); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Entity loaded Host: " + message); }
    }
}
