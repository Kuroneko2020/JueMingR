using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;

namespace NativeWorldTextProbe
{
    // Load the built Host itself. Reflection reaches internal composition seams;
    // no source-linked pretend Host, Harmony installation or native game loop.
    internal static class NativeCompositionChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] oldIds = { "biome-display.toggle", "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle",
            "entity-labels.enemy.toggle", "entity-labels.critter.toggle", "entity-labels.npc.toggle", "world-targets.life-crystal.toggle", "world-targets.life-fruit.toggle",
            "world-targets.mana-crystal.toggle", "world-targets.sleeping-digtoise.toggle", "world-targets.chillet-egg.toggle" };
        private static readonly string[] ids = { "world-object-text.chest.toggle", "world-object-text.sign.toggle", "world-object-text.tombstone.toggle" };
        internal static void Run(ProbeGraphics graphics, string output)
        {
            string root = Path.Combine(Terraria.Program.SavePath, "composition");
            string path = Path.Combine(root, "JueMingRData", "config", "hotkeys.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var entries = oldIds.Select((id, i) => new KeyValuePair<string, string>(id, ((char)('A' + i)).ToString())).ToList();
            entries.Add(new KeyValuePair<string, string>("future.unknown", "Z")); File.WriteAllBytes(path, HotkeyDocument.Encode(new HotkeyDocument(entries)));
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts", "build", "Debug", "work", "bin", "JueMingR.TerrariaHost", "x86", "Debug", "net472", "JueMingR.TerrariaHost.dll"));
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            var type = worker.GetNestedType("PostfixContext", Flags);
            object context = Activator.CreateInstance(type, Flags, null, new object[] { "world-object-text-" + new string('6', 40), Path.Combine(root, "probe-evidence.txt"), root }, null);
            Call(context, "InitializeRuntime", true); worker.GetField("postfixContext", Flags).SetValue(null, context);
            var host = Get(context, "WorldObjects"); var shell = Get(context, "Shell");
            var hotkeys = Get(shell, "hotkeys"); var bindings = (HotkeyBindings)Get(hotkeys, "Bindings"); var registry = (HotkeyRegistry)Get(hotkeys, "Registry");
            var controls = Get(Get(shell, "renderer"), "ObjectControls"); var popup = Get(shell, "StylePopup");
            try
            {
                Until(() => { Call(context, "UpdateRuntime"); bindings.Poll(); return bindings.Loaded && (bool)Get(Get(host, "Preferences"), "IsLoaded"); });
                var layers = new List<GameInterfaceLayer> { new LegacyGameInterfaceLayer("Vanilla: Ingame Options", () => true, InterfaceScaleType.UI) };
                worker.GetMethod("EnsureEntityLayer", Flags).Invoke(null, new object[] { layers });
                Require(layers.Count == 2 && layers[0].Name == "JueMingR: Entity Labels" && layers[0].ScaleType == InterfaceScaleType.Game && (bool)Get(host, "LayersReady"), "one composed Game layer enables actual controls");
                Require(registry.Actions.Count == oldIds.Length + ids.Length && oldIds.All(id => bindings.Get(id) != null) && ids.All(id => bindings.Get(id) == null), "all twelve old bindings preserved and three new actions unbound");
                Require(!Value(host).AnyEnabled && (bool)Get(host, "ControlsEnabled"), "actual production default off with usable controls");
                Require(bindings.Validate(ids[0], bindings.Get(oldIds[0])) != null, "new-old chord conflict shares existing registry");
                for (int i = 0; i < 3; i++)
                {
                    Require(registry.Find(ids[i]).Invoke(HotkeyContext.SinglePlayer), "registered command invokes actual owner");
                    Require(Value(host).Style((WorldObjectKind)i).Mode == (i == 0 ? WorldObjectMode.Opened : WorldObjectMode.Lines), "first hotkey mode is conservative");
                }
                Command(controls, "ChestAlways"); registry.Find(ids[0]).Invoke(HotkeyContext.SinglePlayer); registry.Find(ids[0]).Invoke(HotkeyContext.SinglePlayer);
                Require(Value(host).Style(WorldObjectKind.Chest).Mode == WorldObjectMode.Always, "button and hotkey share last non-off mode");
                Command(controls, "SignCharacters"); Command(controls, "SignMore");
                Require(Value(host).Style(WorldObjectKind.Sign).Characters == 81 && Value(host).Style(WorldObjectKind.Tombstone).Characters == 80, "actual parameter consumer changes only active target");
                Call(host, "SetLimits", WorldObjectKind.Sign, 3, 1200); Command(controls, "SignMore");
                Require(Value(host).Style(WorldObjectKind.Sign).Characters == 1200 && !Available(controls, "SignMore"), "bound and hit availability agree");
                // Select through the same popup entry, then use its captured
                // StyleTarget delegates; changing targets cancels only drafts.
                var rect = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), Flags, null, new object[] { 0f, 0f, 80f, 30f }, null);
                var click = popup.GetType().GetMethods(Flags).Single(m => m.Name == "Click" && m.GetParameters()[0].ParameterType == typeof(WorldObjectKind));
                click.Invoke(popup, new[] { (object)WorldObjectKind.Sign, rect, 9 });
                var selection = Get(popup, "selection"); var editor = Get(popup, "Editor"); Call(editor, "BeginHex"); Call(editor, "Insert", "ABCDEF"); ((Func<int, bool>)Get(selection, "StepSize"))(1);
                Require(Value(host).Style(WorldObjectKind.Sign).Rgb == 0xABCDEF && Value(host).Style(WorldObjectKind.Sign).Size == 80, "shared popup submits real independent color and size");
                ((Action)Get(selection, "Reset"))();
                Require(Value(host).Style(WorldObjectKind.Sign).Size == 70 && Value(host).Style(WorldObjectKind.Sign).Characters == 1200 && Value(host).Style(WorldObjectKind.Sign).Mode == WorldObjectMode.Characters, "style reset preserves mode and parameter");
                click.Invoke(popup, new[] { (object)WorldObjectKind.Tombstone, rect, 9 });
                Require((WorldObjectKind)Get(popup, "WorldObject") == WorldObjectKind.Tombstone && (int)Get(popup, "NameSize") == 70, "popup target cannot reuse previous target state");
                var renderer = Get(shell, "renderer"); Require((bool)Call(renderer, "RefreshResources"), "native F5 resources ready");
                // Native Initialize normally sets this value. The probe has no
                // game initialization, so provide the actual UI-layer matrix.
                typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
                var state = Get(shell, "State"); Set(state, "Ready", true);
                var input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
                foreach (string name in new[] { "Active", "Focused", "F5" }) Set(input, name, true);
                Set(input, "Width", 960f); Set(input, "Height", 640f); Set(input, "Scale", 1f); Call(state, "Update", input);
                Call(Get(state, "Layout"), "SetWorldObjectSettings", Value(host)); Call(renderer, "Prepare", state, 960f, 640f, 1f);
                Call(popup, "Close");
                click.Invoke(popup, new[] { (object)WorldObjectKind.Tombstone, rect, 9 });
                var prepare = popup.GetType().GetMethod("Prepare", Flags); var measureType = prepare.GetParameters()[4].ParameterType;
                var measure = Delegate.CreateDelegate(measureType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
                Call(popup, "Prepare", 960f, 640f, 1f, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration"), rect);
                Require((bool)Get(popup, "Visible") && Get(popup, "Failure") == null, "actual style window remains visible after real-font preparation: " + Get(popup, "Failure"));
                graphics.Image(Path.Combine(output, "native-f5-modes-and-style.png"), () => { Call(renderer, "Draw", state, Matrix.Identity, false, false); Call(renderer, "DrawStylePopup", popup); }, Main.UIScaleMatrix);
                Call(popup, "Close");
                foreach (var id in ids) { long command; string reason; HotkeyChord chord; HotkeyChord.TryParse("F" + (Array.IndexOf(ids, id) + 1), out chord, out reason); Require(bindings.TrySet(id, chord, null, out command, out reason), "new binding goes through real persistence owner"); Until(() => { bindings.Poll(); return !bindings.Busy; }); }
                var physical = new bool[HotkeyChord.KeyCount]; var hotkeyInput = new HotkeyInput(); hotkeyInput.Update(physical, true); hotkeyInput.Update(physical, true);
                var beforeMode = Value(host).Style(WorldObjectKind.Chest).Mode;
                physical[112] = true; hotkeyInput.Update(physical, true); bindings.Dispatch(hotkeyInput, HotkeyContext.SinglePlayer, false);
                Require(Value(host).Style(WorldObjectKind.Chest).Mode == beforeMode, "shared text/focus permission blocks actual new binding");
                physical[112] = false; hotkeyInput.Update(physical, true); physical[112] = true; hotkeyInput.Update(physical, true); bindings.Dispatch(hotkeyInput, HotkeyContext.SinglePlayer, true);
                Require(Value(host).Style(WorldObjectKind.Chest).Mode == WorldObjectMode.Off, "actual bound F1 edge invokes the shared mode command");
                hotkeyInput.Update(physical, true); bindings.Dispatch(hotkeyInput, HotkeyContext.SinglePlayer, true);
                Require(Value(host).Style(WorldObjectKind.Chest).Mode == WorldObjectMode.Off, "held new hotkey does not toggle repeatedly");
                var saved = HotkeyDocument.Decode(File.ReadAllBytes(path)).Entries;
                Require(saved.Count == 16 && entries.All(old => saved.Any(e => e.Key == old.Key && e.Value == old.Value)), "binding save preserves twelve old and future entries byte-semantic values");
                Main.gameMenu = true; Call(context, "UpdateRuntime");
                Require(!(bool)Get(host, "ControlsEnabled") && !registry.Find(ids[0]).Invoke(HotkeyContext.SinglePlayer), "session exit disables actual registered action");
                Require(!Directory.Exists(Path.Combine(root, "JueMingRData", "records", "opened-containers")), "merely enabling empty Opened never writes an empty history");
                Console.WriteLine("PASS: built Host composition, shared world layer, fifteen actions, real row/parameter/style commands and old-binding persistence.");
            }
            finally
            {
                Call(host, "OnExit", null, EventArgs.Empty); Call(hotkeys, "OnExit", null, EventArgs.Empty);
                worker.GetField("postfixContext", Flags).SetValue(null, null); Main.gameMenu = false;
            }
        }
        private static WorldObjectSettings Value(object host) { return (WorldObjectSettings)Get(Get(host, "Preferences"), "Value"); }
        private static object CommandValue(object controls, string name) { return Enum.Parse(controls.GetType().GetMethod("Execute", Flags).GetParameters()[0].ParameterType, name); }
        private static void Command(object controls, string name) { Call(controls, "Execute", CommandValue(controls, name)); }
        private static bool Available(object controls, string name) { return (bool)Call(controls, "Available", CommandValue(controls, name)); }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
        private static object Get(object target, string name) { var field = target.GetType().GetField(name, Flags); return field != null ? field.GetValue(target) : target.GetType().GetProperty(name, Flags).GetValue(target, null); }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, Flags); if (field != null) field.SetValue(target, value); else target.GetType().GetProperty(name, Flags).SetValue(target, value, null); }
        private static void Until(Func<bool> condition) { var time = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { if (time.ElapsedMilliseconds > 5000) throw new TimeoutException("composition readiness"); Thread.Sleep(1); } }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Built Host: " + reason); }
    }
}
