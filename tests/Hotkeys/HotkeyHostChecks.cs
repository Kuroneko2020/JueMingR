using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;

namespace Terraria
{
    // Loads the separate production assembly through bootstrap. Reflection only
    // reads UI/result state; all four business commands are driven by input.
    internal static class HotkeyHostChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] ids = { "biome-display.toggle", "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle" };
        private static readonly Keys[] keys = { Keys.K, Keys.J, Keys.L, Keys.O };
        private static Main main;
        private static object shell, state, popup, owner, items, preferences;
        private static bool draw;
        internal static void Prepare(string mode)
        {
            if (mode != "expect-hotkeys-runtime") return;
            string root = Path.GetDirectoryName(typeof(Main).Assembly.Location);
            Check(root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && root.Contains("JueMingR-Phase0S-Test-"), "runtime fixture writes only its precreated marked TEMP installation");
            string path = Path.Combine(root, "JueMingRData", "config", "hotkeys.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var entries = new List<KeyValuePair<string, string>>(); for (int i = 0; i < ids.Length; i++) entries.Add(new KeyValuePair<string, string>(ids[i], keys[i].ToString()));
            File.WriteAllBytes(path, HotkeyDocument.Encode(new HotkeyDocument(entries)));
            GameInput.PlayerInput.CurrentProfile.InputModes[GameInput.InputMode.Keyboard].KeyStatus["Inventory"] = new List<string> { "K" };
        }
        internal static void Run(Main instance, string mode)
        {
            main = instance; HostInputChecks.ConfigureLoadedHost(); FocusHelper.IsSelectedApplication = true;
            Main.SampleKeys = new Keys[0]; Main.SampleLeft = Main.SampleRight = Main.SampleF5 = Main.SampleMiddle = Main.SampleX1 = Main.SampleX2 = false;
            Main.SampleWheel = 0; Main.SampleX = 1850; Main.SampleY = 900;
            object context = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost")
                .GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true).GetField("postfixContext", Flags).GetValue(null);
            shell = Get(context, "Shell"); state = Get(shell, "State"); popup = Get(shell, "HotkeyPopup");
            Check(popup != null, "new package composed actual unified hotkey owner");
            owner = Get(Get(shell, "hotkeys"), "Bindings"); items = Get(context, "items"); preferences = Get(context, "preferences");
            Check(items != null && (bool)Get(items, "Available"), "all three item consumers actually composed");
            Until(() => (bool)Get(owner, "Loaded") && (bool)Get(preferences, "BiomeLoaded") && (bool)Get(Get(items, "Preferences"), "IsLoaded"));
            Frame(); Frame();
            if (mode == "expect-hotkeys-edit")
            {
                using (var graphics = new F5FixtureGraphics())
                {
                    main.SetupAndDrawBiomeLayer(); draw = true; Frame(); Frame(Keys.F5); Frame();
                    Check((bool)Get(state, "Visible"), "real F5 opened");
                    for (int i = 0; i < ids.Length; i++)
                    {
                        if (i == 1) { object nav = Get(state, "Layout").GetType().GetMethod("Navigation", Flags).Invoke(Get(state, "Layout"), new object[] { 0 }); Click(Rect(nav, state)); Frame(); }
                        object icon = FindIcon(ids[i], i == 0);
                        float[] r;
                        if (i == 0)
                        {
                            object layout = Get(state, "Layout"), view = Get(layout, "Viewport");
                            float targetY = (float)Get(Get(icon, "Rect"), "Y");
                            for (int attempt = 0; targetY - (float)Get(state, "Scroll") > (float)Get(view, "Height") - 35 && attempt < 30; attempt++)
                            { Main.SampleX = (int)((float)Get(state, "X") + 150); Main.SampleY = (int)((float)Get(state, "Y") + 210); Main.SampleWheel = -120; Frame(); Main.SampleWheel = 0; }
                            r = Rect(Get(icon, "Rect"), state, true);
                        }
                        else r = Rect(Get(icon, "Rect"));
                        Click(r); Click(r); Frame();
                        Check((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == ids[i], "double click targets correct real row " + ids[i]);
                        if (i == 1) CheckEarlyPopupMapping();
                        PopupClick(0); Frame(); Frame(keys[i]); Frame();
                        Until(() => !(bool)Get(owner, "Busy")); Frame();
                        var binding = (HotkeyChord)owner.GetType().GetMethod("Get").Invoke(owner, new object[] { ids[i] });
                        Check(binding != null && binding.MainKey == (int)keys[i], "real popup reliably saved " + ids[i] + ": " + Get(popup, "Status"));
                        PopupClick(2); Frame();
                    }
                    CheckPinCapture();
                    Frame(Keys.F5); Frame(); draw = false;
                }
            }
            else Check(ids.All(id => owner.GetType().GetMethod("Get").Invoke(owner, new object[] { id }) != null), "saved bindings loaded without UI resave/vanilla recheck");
            Main.SampleX = 1850; Main.SampleY = 900; Frame();
            for (int i = 0; i < ids.Length; i++)
            {
                bool[] before = Values(); Frame(keys[i]); bool[] after = Values();
                for (int j = 0; j < 4; j++) Check(after[j] == (i == j ? !before[j] : before[j]), "real primary toggles only registered consumer " + ids[i]);
                Frame(keys[i], Keys.RightControl); Check(Values().SequenceEqual(after), "held primary plus modifier does not retrigger actual command");
                Frame(); Frame(keys[i]); Check(Values().SequenceEqual(before), "same new primary re-enables a disabled consumer"); Frame();
            }
            bool[] unchanged = Values();
            // Native mapping consumes its cached keyboard sample before refresh.
            // A post-save profile change must keep both normal execution paths.
            var nativeMap = GameInput.PlayerInput.CurrentProfile.InputModes[GameInput.InputMode.Keyboard].KeyStatus;
            nativeMap["ViewZoomIn"] = new List<string> { "K" };
            int nativeZoom = Main.NativeZoom;
            Frame(Keys.K); Frame(Keys.K);
            Check(Values()[0] != unchanged[0] && Main.NativeZoom > nativeZoom, "later vanilla reassignment keeps binding and native action active");
            Frame(); Frame(Keys.K); Frame();
            nativeMap.Remove("ViewZoomIn");
            Check(Values().SequenceEqual(unchanged), "later overlap leaves independent state semantics intact");
            Main.drawingPlayerChat = true; Frame(Keys.K); Main.drawingPlayerChat = false; Frame(Keys.K); Check(Values().SequenceEqual(unchanged), "chat ownership release cannot replay held key"); Frame();
            HostInputChecks.Foreground = false; Frame(Keys.K); HostInputChecks.Foreground = true; Frame(Keys.K); Check(Values().SequenceEqual(unchanged), "focus activation retains old key quarantine"); Frame(); Frame();
            Check((bool)Get(Get(Get(items, "Preferences"), "Value"), "DiscardFeedbackEnabled"), "shortcut leaves discard feedback preference intact");
            Check(!(bool)Get(shell, "Failed"), "real shell remained healthy");
            string file = Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location), "JueMingRData", "config", "hotkeys.json");
            Check(HotkeyDocument.Decode(File.ReadAllBytes(file)).Entries.Count == 4, "exact independent real binding document");
            Console.WriteLine("PASS: " + mode + " production input/shell/four commands/independent file.");
        }
        private static object FindIcon(string id, bool biome)
        {
            IEnumerable source = (IEnumerable)Get(biome ? Get(state, "Layout") : Get(shell, "items"), biome ? "Elements" : "controls");
            foreach (object value in source) { object element = biome ? value : Get(value, "Element"); if ((string)Get(element, "HotkeyTarget") == id) return value; }
            throw new Exception("Real keyboard control missing: " + id);
        }
        private static void CheckPinCapture()
        {
            var workspace = (JueMingR.Features.Notes.NotesWorkspace)Get(Get(shell, "notes"), "workspace");
            Until(() => workspace.Feature.Loaded);
            Check(workspace.Request(new JueMingR.Features.Notes.NotesAction(JueMingR.Features.Notes.NotesActionKind.Create)), "isolated note creation");
            Until(() => !workspace.Feature.Busy);
            string id = workspace.Feature.Saved.Notes.Last().Id;
            Check(workspace.Request(new JueMingR.Features.Notes.NotesAction(JueMingR.Features.Notes.NotesActionKind.Pin, id, false, 1600, 200)), "isolated note pin");
            Until(() => !workspace.Feature.Busy); Frame();
            float[] icon = Rect(Get(FindIcon(ids[1], false), "Rect")); Click(icon); Click(icon); Frame(); PopupClick(0);
            Main.SampleX = 1620; Main.SampleY = 280; Main.SampleWheel = 120;
            int width = workspace.Feature.ReadingFor(id).Width;
            Frame(Keys.LeftShift); Main.SampleWheel = 0;
            Check(workspace.Feature.ReadingFor(id).Width == width + 40 && (bool)Get(popup, "Capturing"), "loaded capture preserves Notes Shift wheel without a candidate");
            Frame(); Main.SampleWheel = 120; int font = workspace.Feature.ReadingFor(id).FontPercent;
            Frame(Keys.LeftControl); Main.SampleWheel = 0;
            Check(workspace.Feature.ReadingFor(id).FontPercent == font + 10 && (bool)Get(popup, "Capturing"), "loaded capture preserves Notes Ctrl wheel using frozen physical modifiers");
            Frame();
            object notesPins = Get(Get(shell, "notes"), "pins"); object pin = null;
            foreach (object value in (IEnumerable)Get(notesPins, "Pins")) if ((string)Get(Get(value, "Note"), "Id") == id) pin = value;
            Check(pin != null, "actual displayed pin exists");
            Click(Rect(Get(pin, "Close"))); Until(() => !(bool)Get(owner, "Busy")); Frame();
            Check(workspace.Feature.Saved.Find(id).Pinned && !workspace.Feature.Busy, "capture click and release cannot unpin real Notes card");
            PopupClick(0); Frame(Keys.J); Frame(); Until(() => !(bool)Get(owner, "Busy")); Frame(); PopupClick(2); Frame();
            Check(workspace.Request(new JueMingR.Features.Notes.NotesAction(JueMingR.Features.Notes.NotesActionKind.Unpin, id)), "isolated pin cleanup");
            Until(() => !workspace.Feature.Busy);
        }
        private static void CheckEarlyPopupMapping()
        {
            var map = GameInput.PlayerInput.CurrentProfile.InputModes[GameInput.InputMode.Keyboard].KeyStatus;
            object panel = Get(Get(popup, "Layout"), "Panel");
            Main.SampleX = (int)(float)Get(panel, "X") + 18;
            Main.SampleY = (int)(float)Get(panel, "Y") + 18;
            Frame();
            for (int button = 1; button <= 5; button++)
            {
                map["ViewZoomIn"] = new List<string> { "Mouse" + button };
                int zoom = Main.NativeZoom;
                Main.SampleLeft = button == 1;
                Main.SampleRight = button == 2;
                Main.SampleMiddle = button == 3;
                Main.SampleX1 = button == 4;
                Main.SampleX2 = button == 5;
                Frame();
                Check(Main.NativeZoom == zoom, "visible popup blocks actual early native zoom on first mouse press " + button);
                Frame();
                Main.SampleLeft = Main.SampleRight = Main.SampleMiddle = Main.SampleX1 = Main.SampleX2 = false;
                Frame(); Frame();
                Check(Main.NativeZoom == zoom, "popup mouse tail cannot reach early native zoom " + button);
            }
            // Outside the visible popup, normal native mappings keep working.
            PopupClick(3);
            object help = Get(Get(popup, "Layout"), "HelpPanel");
            Main.SampleX = (int)(float)Get(help, "X") + 18; Main.SampleY = (int)(float)Get(help, "Y") + 18; Frame();
            Check((bool)Get(popup, "HelpVisible"), "production help surface keeps its hover ownership");
            int helpZoom = Main.NativeZoom;
            Main.SampleX2 = true; Frame();
            Check(Main.NativeZoom == helpZoom, "help surface blocks the early native mouse mapping");
            Main.SampleX = 1850; Main.SampleY = 900; Frame();
            Check(Main.NativeZoom == helpZoom, "help surface retains the physical tail after leaving it");
            Main.SampleX2 = false; Frame();
            int outsideZoom = Main.NativeZoom;
            Main.SampleX = 1850; Main.SampleY = 900; Main.SampleX2 = true; Frame();
            Check(Main.NativeZoom > outsideZoom, "popup does not globally disable native mouse mappings");
            Main.SampleX2 = false; Frame(); map.Remove("ViewZoomIn"); Frame();
        }
        private static bool[] Values()
        { object value = Get(Get(items, "Preferences"), "Value"); return new[] { (bool)Get(preferences, "BiomeEnabled"), (bool)Get(value, "StackEnabled"), (bool)Get(value, "SellEnabled"), (bool)Get(value, "DiscardEnabled") }; }
        private static void PopupClick(int index)
        { object layout = Get(popup, "Layout"); IList commands = (IList)Get(layout, "Commands"); int position = -1; for (int i = 0; i < commands.Count; i++) if (Convert.ToInt32(commands[i]) == index) position = i; Check(position >= 0, "popup action is present"); object element = ((IList)Get(layout, "Buttons"))[position]; float[] r = Rect(Get(element, "Rect")); object panel = Get(layout, "Panel"); r[0] += (float)Get(panel, "X"); r[1] += (float)Get(panel, "Y"); Click(r); }
        private static float[] Rect(object rect, object origin = null, bool page = false)
        {
            float x = (float)Get(rect, "X") + (float)Get(rect, "Width") / 2, y = (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2;
            if (origin != null) { x += (float)Get(origin, "X"); y += (float)Get(origin, "Y"); }
            if (page) { object view = Get(Get(state, "Layout"), "Viewport"); x += (float)Get(view, "X"); y += (float)Get(view, "Y") - (float)Get(state, "Scroll"); }
            return new[] { x, y };
        }
        private static void Click(float[] point) { Main.SampleX = (int)point[0]; Main.SampleY = (int)point[1]; Main.SampleLeft = true; Frame(); Main.SampleLeft = false; Frame(); }
        private static void Frame(params Keys[] keys) { Main.SampleKeys = keys; main.RunUpdateLoop(1); if (draw) main.DrawAllFixtureLayers(); }
        private static void Until(Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < 5000) { Frame(); Thread.Sleep(1); } Check(condition(), "production asynchronous readiness"); }
        private static object Get(object target, string name) { var field = target.GetType().GetField(name, Flags); return field == null ? target.GetType().GetProperty(name, Flags).GetValue(target, null) : field.GetValue(target); }
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException("Hotkeys loaded Host: " + message); }
    }
}
