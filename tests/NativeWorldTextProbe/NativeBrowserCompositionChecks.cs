using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Announcements;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBrowserCompositionChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static int local, network;
        private static string last;
        internal static void Run(Assembly assembly)
        {
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            string root = Path.Combine(Terraria.Program.SavePath, "browser-composition"); Directory.CreateDirectory(root);
            CheckAnnouncementPersistence(assembly, Path.Combine(root, "preferences"));
            Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
            Main.netMode = Main.myPlayer = 0; Main.LocalPlayer.active = true; Main.LocalPlayer.chest = -1;
            Main.Map = new Terraria.Map.WorldMap(Main.maxTilesX, Main.maxTilesY);
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.LocalPlayer };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity); FiniteCostChecks.SetCpuFont(8);
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "item-browser-" + new string('9', 40), Path.Combine(root, "evidence.txt"), root }, null);
            var isolation = new Harmony("JueMingR.Tests.AnnouncementOutlets");
            MethodInfo send = typeof(ChatHelper).GetMethod("SendChatMessageFromClient", Flags), incoming = typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage", Flags);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true);
                object browser = Get(context, "Browser"), announcements = Get(browser, "Announcements");
                var keys = Get(Get(context, "Shell"), "hotkeys"); var bindings = (HotkeyBindings)Get(keys, "Bindings");
                Until(() => { Call(context, "UpdateRuntime"); bindings.Poll(); return (bool)Get(announcements, "CanConfigure") && bindings.Loaded; });
                foreach (string name in new[] { "Footprints", "MapFeatures", "DeathRecords", "Guidance", "Information", "WorldObjects", "WorldTargets", "Labels", "items", "notes" })
                    Require(GetOptional(context, name) != null, "complete browser package retains " + name);
                Require((bool)Get(Get(browser, "Targets"), "Ready") && (bool)Get(Get(Get(browser, "Locator"), "Receiver"), "Ready"), "complete profile installs both fixed native observation owners");
                var registry = (HotkeyRegistry)Get(keys, "Registry");
                foreach (string id in new[] { "announcement.send", "item-browser.query" })
                    Require(registry.Actions.Any(a => a.Id == id && a.Context == HotkeyContext.Gameplay) && bindings.Get(id) == null, "shared action is available to both topologies and unbound by default: " + id);
                var native = Get(Get(browser, "Knowledge"), "Native");
                for (int i = 0; i < 120; i++) Call(context, "UpdateRuntime");
                Require((long)Get(native, "ItemReads") == 0 && !(bool)Get(announcements, "Enabled"), "complete closed profile does no directory work and announcements default off");

                // Execute production Submit and native message construction while
                // intercepting both final outlets: no local/public chat is sent.
                isolation.Patch(send, prefix: new HarmonyMethod(typeof(NativeBrowserCompositionChecks).GetMethod(nameof(Network), Flags)));
                isolation.Patch(incoming, prefix: new HarmonyMethod(typeof(NativeBrowserCompositionChecks).GetMethod(nameof(Local), Flags)));
                object value = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("UiItem", Flags).Invoke(null, new object[] { 9, 23 });
                Main.hideUI = true; local = network = 0; Call(announcements, "Submit", value);
                Require(local == 0 && network == 0, "disabled announcement has no chat outlet");
                Require((bool)Call(announcements, "SetEnabled", true), "isolated normal preference enables announcement");
                string statusBeforeSend = (string)Get(announcements, "Status");
                Main.netMode = 1; Call(announcements, "Submit", value); Call(announcements, "Submit", value);
                Require(network == 1 && local == 0 && last.Contains("23") && !last.StartsWith("/") && System.Text.Encoding.UTF8.GetByteCount(last) <= 1024, "production client submits one bounded ordinary message and cannot replay within cooldown");
                Call(Get(announcements, "cooldown"), "Clear"); Main.netMode = 0; Call(announcements, "Submit", value);
                Require(local == 1 && network == 1, "single player selects the native local processor only");
                Require((string)Get(announcements, "Status") == statusBeforeSend, "normal announcements do not add a success feedback message");
                object empty = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("UiItem", Flags).Invoke(null, new object[] { 0, 0 });
                Call(Get(announcements, "cooldown"), "Clear"); Call(announcements, "Submit", empty);
                Require(local == 1 && network == 1, "empty UI slot never falls back to public world text");
                Console.WriteLine("PASS: complete ItemBrowser profile, retained consumers, shared unbound actions, closed cost and production SP/client chat outlet selection. Outlets intercepted; no network or in-game claim.");
            }
            finally
            {
                isolation.Unpatch(send, HarmonyPatchType.All, isolation.Id); isolation.Unpatch(incoming, HarmonyPatchType.All, isolation.Id);
                Main.netMode = 0; Main.hideUI = false; Main.gameMenu = true; Call(context, "UpdateRuntime");
                var browser = GetOptional(context, "Browser"); if (browser != null) ((IDisposable)browser).Dispose(); StopContext(context); Main.gameMenu = false;
            }
        }
        private static bool Network(ChatMessage __0) { network++; last = __0.Text; return false; }
        private static bool Local(ChatMessage __0) { local++; last = __0.Text; return false; }
        private static void CheckAnnouncementPersistence(Assembly assembly, string root)
        {
            var type = assembly.GetType("JueMingR.TerrariaHost.Announcements.HostAnnouncements");
            Func<string, object> create = path => Activator.CreateInstance(type, Flags, null, new object[] { path }, null);
            object owner = create(root);
            try
            {
                var document = (PreferenceDocument<AnnouncementSettings>)Get(owner, "preferences");
                Until(() => document.Snapshot.IsLoaded);
                Require(!(bool)Get(owner, "Enabled") && (bool)Call(owner, "SetEnabled", true), "fresh announcement preference starts off and accepts enable");
                Until(() => document.Snapshot.Status != PreferenceStatus.Pending);
                Require(document.Snapshot.Status == PreferenceStatus.Saved, "announcement enable must be committed to the actual isolated file");
                long revision = document.Snapshot.Revision;
                Require(!(bool)Call(owner, "SetEnabled", true) && document.Snapshot.Revision == revision, "repeated announcement selection does not enqueue another save");
            }
            finally { ((IDisposable)owner).Dispose(); }
            owner = create(root);
            try
            {
                Until(() => (bool)Get(owner, "CanConfigure"));
                Require((bool)Get(owner, "Enabled"), "recreated announcement owner restores enabled from disk");
            }
            finally { ((IDisposable)owner).Dispose(); }
            string protectedRoot = Path.Combine(root, "protected"), pathName = Path.Combine(protectedRoot, "JueMingRData", "config", "features", "announcements.json");
            Directory.CreateDirectory(Path.GetDirectoryName(pathName)); File.WriteAllText(pathName, "invalid-owner-test");
            owner = create(protectedRoot);
            try
            {
                Until(() => (bool)Get(owner, "CanConfigure"));
                Require((bool)Call(owner, "SetEnabled", true) && (bool)Get(owner, "Enabled"), "protected preference still permits session-only selection");
                Require(!string.IsNullOrEmpty((string)Get(owner, "SettingsMessage")), "protected announcement preference has a visible persistence warning");
            }
            finally { ((IDisposable)owner).Dispose(); }
            Require(File.ReadAllText(pathName) == "invalid-owner-test", "protected announcement file is retained unchanged");
        }
        private static void Until(Func<bool> predicate)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (clock.ElapsedMilliseconds > 10000) throw new TimeoutException("browser composition ready"); Thread.Sleep(5); } }
    }
}
