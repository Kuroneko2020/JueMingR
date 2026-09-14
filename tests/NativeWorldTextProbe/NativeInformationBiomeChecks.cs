using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Information;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeInformationBiomeChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static int observations, draws;
        private static bool drawnUnavailable;
        private static void Observed() { observations++; }
        private static bool CaptureDraw(bool biomeFailed) { draws++; drawnUnavailable = biomeFailed; return false; }
        internal static void Run(object context, object host)
        {
            object biome = Get(context, "runtime"), feature = Get(biome, "feature"), runtime = Get(biome, "SharedRuntime");
            object source = Get(feature, "observationSource"), shell = Get(context, "Shell"), renderer = Get(shell, "renderer"), preferences = Get(context, "preferences");
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry"); var action = registry.Find("biome-display.toggle");
            var player = Main.LocalPlayer; var other = Main.player[1]; var world = Main.ActiveWorldFileData; var socket = Netplay.Connection.Socket;
            int mode = Main.netMode; var patch = new Harmony("JueMingR.Information.BiomeClientTests");
            MethodInfo observe = source.GetType().GetMethod("TryObserve", Flags), draw = renderer.GetType().GetMethod("Draw", Flags);
            patch.Patch(observe, prefix: new HarmonyMethod(typeof(NativeInformationBiomeChecks).GetMethod(nameof(Observed), Flags)));
            patch.Patch(draw, prefix: new HarmonyMethod(typeof(NativeInformationBiomeChecks).GetMethod(nameof(CaptureDraw), Flags)));
            Action update = () => { Call(context, "UpdateRuntime"); Call(host, "PrepareHud"); };
            try
            {
                Main.player[1] = new Player { active = true, ZoneSnow = true }; player.ZoneDesert = true;
                foreach (int clientMode in new[] { 0, 1 })
                {
                    Main.netMode = clientMode; Call(preferences, "SetBiomeEnabled", true); update();
                    string text = (string)Call(host, "Text", InformationKind.Biome);
                    Require(text != null && text.Contains("沙漠") && !text.Contains("雪原"), "actual biome adapter reads only the local player's Zones in mode " + clientMode);
                    object model = Get(biome, "CurrentViewModel"); int reads = observations;
                    for (int i = 0; i < 90; i++) update();
                    Require(observations - reads == 3 && ReferenceEquals(model, Get(biome, "CurrentViewModel")), "same sole Zone reader retains 30-tick cadence and stable model in mode " + clientMode);
                    Require(action.Invoke(clientMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer), "registered stable biome action accepts the local client context");
                    update(); reads = observations; for (int i = 0; i < 90; i++) update();
                    Require(Call(host, "Text", InformationKind.Biome) == null && observations == reads, "disabled actual biome source performs no observation");
                    Require(action.Invoke(clientMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer), "registered action re-enables biome"); update();
                }
                long generation = (long)Get(runtime, "Generation");
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath, "second.wld"), false);
                player.ZoneDesert = false; player.ZoneJungle = true; update();
                Require((long)Get(runtime, "Generation") > generation && ((string)Call(host, "Text", InformationKind.Biome)).Contains("丛林"), "world replacement clears cadence and old model in the shared Session");
                generation = (long)Get(runtime, "Generation"); Netplay.Connection.Socket = new Terraria.Net.Sockets.TcpSocket(); update();
                Require((long)Get(runtime, "Generation") > generation, "ordinary reconnect socket retires the shared Session");
                generation = (long)Get(runtime, "Generation"); Main.player[0] = new Player { active = true, ZoneSnow = true }; update();
                Require((long)Get(runtime, "Generation") > generation && ((string)Call(host, "Text", InformationKind.Biome)).Contains("雪原"), "local player replacement cannot retain previous player's biome");
                Main.player[0] = player;
                foreach (Action invalid in new Action[] { () => Main.gameMenu = true, () => Main.dedServ = true, () => Main.netMode = 2, () => player.active = false, () => Main.player[0] = null })
                {
                    invalid(); update();
                    Require(Call(host, "Text", InformationKind.Biome) == null && !action.Invoke(HotkeyContext.Gameplay), "menu/server/missing/inactive local player rejects HUD and registered action");
                    Main.gameMenu = Main.dedServ = false; Main.netMode = 1; Main.player[0] = player; player.active = true; update();
                    Require(Call(host, "Text", InformationKind.Biome) != null, "normal reentry restores local biome immediately");
                }
                // Direct adapter rejection must also be safe for malformed native
                // slots; never turn an unavailable source into synthetic Forest.
                int index = Main.myPlayer; Main.myPlayer = Main.player.Length;
                try { object[] args = { null }; Require(!(bool)observe.Invoke(source, args), "invalid local slot rejects observation without throwing"); }
                finally { Main.myPlayer = index; }
                CheckControls(context, host, action, renderer);
            }
            finally
            {
                patch.Unpatch(observe, HarmonyPatchType.All, patch.Id); patch.Unpatch(draw, HarmonyPatchType.All, patch.Id);
                Main.player[0] = player; Main.player[1] = other; player.ZoneDesert = player.ZoneJungle = false; player.active = true;
                Main.ActiveWorldFileData = world; Netplay.Connection.Socket = socket; Main.netMode = mode; Main.gameMenu = Main.dedServ = false;
                Call(preferences, "SetBiomeEnabled", false); update();
            }
            Console.WriteLine("PASS: actual biome local adapters, 0/1 Session lifecycle, 30-tick/off work, F5 commands/render availability and saved physical shortcut; live server play remains unverified.");
        }
        private static void CheckControls(object context, object host, HotkeyAction action, object renderer)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State"), preferences = Get(context, "preferences");
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960);
            typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            PlayerInput.Triggers.Initialize(); Main.blockInput = false; FocusHelper.IsSelectedApplication = true; Set(state, "Ready", true);
            Call(renderer, "RefreshResources"); Call(state, "Navigate", 9); Call(renderer, "Prepare", state, 960f, 640f, 1f);
            Action<int, int, bool, Keys[]> frame = (x, y, left, keys) =>
            {
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; Main.keyState = new KeyboardState(keys);
                PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update(); Main.mouseLeft = left;
                Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput");
                Require(!(bool)Get(shell, "Failed"), "actual F5/input biome path remains healthy");
            };
            var none = new Keys[0]; frame(940, 620, false, none); frame(940, 620, false, none);
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings"); var saved = bindings.Get(action.Id);
            Require(saved != null, "biome uses the already loaded old binding");
            foreach (int mode in new[] { 0, 1 })
            {
                Main.netMode = mode; Call(context, "UpdateRuntime"); Call(state, "RestoreVisible");
                foreach (string command in new[] { "DisableBiome", "EnableBiome" })
                {
                    object layout = Get(state, "Layout"), element = null;
                    foreach (object candidate in (IEnumerable)Get(layout, "Elements")) if (Get(candidate, "Command").ToString() == command) { element = candidate; break; }
                    Require(element != null, "actual information page biome command exists");
                    object rect = Get(element, "Rect"), viewport = Get(layout, "Viewport"); Call(state, "ScrollTo", (float)Get(rect, "Y"));
                    int x = (int)((float)Get(state, "X") + (float)Get(viewport, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2);
                    int y = (int)((float)Get(state, "Y") + (float)Get(viewport, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + (float)Get(rect, "Height") / 2);
                    frame(x, y, false, none); frame(x, y, true, none); frame(x, y, false, none);
                    Require(Get(state, "Command").ToString() == command && (bool)Get(preferences, "BiomeEnabled") == (command == "EnableBiome"), "real page click changes biome preference in mode " + mode);
                }
                int beforeDraw = draws; Call(shell, "DrawLayer");
                Require(draws == beforeDraw + 1 && !drawnUnavailable, "actual F5 renderer receives available biome in mode " + mode);
                // Only the GPU draw method is replaced above; physical sampling,
                // existing binding, dispatch and command are real consumers.
                frame(940, 620, false, none); frame(940, 620, false, none);
                bool before = (bool)Get(preferences, "BiomeEnabled");
                frame(940, 620, false, new[] { (Keys)saved.MainKey });
                Require((bool)Get(preferences, "BiomeEnabled") != before, "old physical binding dispatches biome in mode " + mode);
                frame(940, 620, false, none); frame(940, 620, false, new[] { (Keys)saved.MainKey }); frame(940, 620, false, none);
            }
            Call(state, "Close");
        }
    }
}
