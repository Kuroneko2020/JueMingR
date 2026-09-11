using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.EntityLabels;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using JueMingR.TerrariaHost.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class WorldTargetStyleChecks
    {
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-WorldStyles-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var runtime = new SingleFeatureRuntime(new Probe(), new Idle());
            var targets = new HostWorldTargets(root, runtime) { LayersReady = true }; var labels = new HostEntityLabels(root, runtime);
            runtime.AddFeature(targets); runtime.Update(0);
            var document = Document<WorldTargetSettings>(targets); var entityDocument = Document<EntityLabelSettings>(labels);
            bool stopped = false, focused = true;
            try
            {
                Wait(() => targets.Preferences.IsLoaded && labels.Preferences.IsLoaded); targets.PollPreferences();
                WorldTargetObservationChecks.Prepare(); Main.LocalPlayer.accOreFinder = false;
                Check(targets.ControlsEnabled, "no ability still allows real settings controls");
                string path = Path.Combine(root, "JueMingRData", "config", "features", "world-targets.json");
                Check(!File.Exists(path), "missing document does not pre-create defaults");
                var input = new HostInputState(() => new IntPtr(1), () => focused ? new IntPtr(1) : new IntPtr(2));
                var popup = new StylePopup(labels, input, new Clipboard(), new Ime(), targets);
                var anchor = new F5Rect(150, 60, 44, 30); object font = new object();
                Action prepare = () => popup.Prepare(900, 700, 1, font, (s, scale) => new F5Size(s.Length * 14 * scale, 24 * scale), 0, anchor);
                Action<float, float, bool> frame = (x, y, left) =>
                {
                    FocusHelper.IsSelectedApplication = focused; input.BeginUpdate(); popup.BeforeInput(input.CanPrepareText);
                    PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    input.AfterMapping(); Main.keyState = new KeyboardState(); input.AfterKeyboardRefresh(); popup.Process(input.CanUseInput, 9, x, y, true); prepare();
                };
                frame(0, 0, false);
                foreach (WorldTargetKind kind in Enum.GetValues(typeof(WorldTargetKind)))
                {
                    popup.Click(kind, anchor, 9); prepare(); long revision = targets.Preferences.Revision;
                    Check(popup.Visible && popup.NameSize == 0 && popup.Layout.Commands.SequenceEqual(new[] { StylePopupCommand.Close, StylePopupCommand.Reset }) &&
                        !popup.Layout.Text.Any(e => e.Text == "字号"), "five real color-only targets have no size controls or placeholder");
                    Check(targets.Preferences.Revision == revision, "opening exact RGB does not quantize or save");
                    popup.Editor.BeginHex(); popup.Editor.SelectAll(); popup.Editor.Insert("123456"); Check(popup.Editor.Commit(), "complete HEX commits through captured target");
                    Check(targets.Preferences.Value.Color(kind) == 0x123456, "current target color takes effect in memory");
                    popup.Close();
                }
                Check(labels.Preferences.Value.Equals(EntityLabelSettings.Default), "new five colors never alter existing three styles");
                targets.SetEnabled(WorldTargetKind.LifeCrystal, true);
                popup.Click(WorldTargetKind.LifeCrystal, anchor, 9); prepare();
                var slider = popup.Layout.Sliders[0].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                long before = targets.Preferences.Revision;
                frame(slider.X + slider.Width / 2, slider.Y + 3, true);
                Check(targets.Preferences.Revision == before && popup.ActiveSlider == 0, "real world-color drag is draft only");
                frame(slider.X + slider.Width / 2, slider.Y + 3, false);
                Check(targets.Preferences.Revision == before + 1, "real valid release commits exactly once");
                prepare(); slider = popup.Layout.Sliders[1].Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y); before = targets.Preferences.Revision;
                frame(slider.X, slider.Y + 2, true); focused = false; frame(0, 0, false);
                Check(!popup.Visible && targets.Preferences.Revision == before, "focus loss cancels draft and its release tail");
                focused = true; frame(0, 0, false); frame(0, 0, false);
                popup.Click(WorldTargetKind.LifeCrystal, anchor, 9); prepare();
                int resetIndex = popup.Layout.Commands.IndexOf(StylePopupCommand.Reset); F5Rect reset = popup.Layout.Buttons[resetIndex].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                frame(reset.X + 3, reset.Y + 3, true); frame(reset.X + 3, reset.Y + 3, false);
                Check(targets.Preferences.Value.Color(WorldTargetKind.LifeCrystal) == 0xFF69B4 && targets.Preferences.Value.Enabled(WorldTargetKind.LifeCrystal) &&
                    targets.Preferences.Value.Color(WorldTargetKind.LifeFruit) == 0x123456, "real restore resets only this color");
                popup.Editor.BeginHex(); popup.Editor.SelectAll(); popup.Editor.Insert("ABC"); before = targets.Preferences.Revision;
                popup.Click(WorldTargetKind.ChilletEgg, anchor, 9); prepare(); Check(targets.Preferences.Revision == before && popup.Editor.Hex == "123456", "switch cancels incomplete old target draft");
                popup.Click(EntityLabelKind.Enemy, anchor, 9); prepare();
                Check(popup.NameSize == 90 && popup.Layout.Commands.Contains(StylePopupCommand.Larger), "existing entity name/health defaults and size capability remain");
                popup.Close();
                Wait(() => targets.Preferences.Status == PreferenceStatus.Saved);
                string text = File.ReadAllText(path); Check(!text.Contains("nameSize") && !text.Contains("Tile") && !text.Contains("phase"), "natural world preferences contain no size/coordinates/time");
                Check(new WorldTargetCodec().Decode(File.ReadAllBytes(path)).Equals(targets.Preferences.Value), "actual atomic file reload gets newest five-target revision");
                File.WriteAllText(path, "external edit"); targets.SetColor(WorldTargetKind.ChilletEgg, 0x654321);
                Wait(() => targets.Preferences.Status == PreferenceStatus.Conflict);
                Check(File.ReadAllText(path) == "external edit" && targets.PreferenceMessage != null, "external edit preserved with truthful feedback");
            }
            finally
            {
                stopped = document.Stop(3000) & entityDocument.Stop(3000);
                string full = Path.GetFullPath(root);
                Check(Path.GetDirectoryName(full).Equals(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("JueMingR-WorldStyles-", StringComparison.Ordinal), "isolated cleanup containment");
                if (stopped) Directory.Delete(full, true);
            }
            Check(stopped, "all preference workers stopped");
            Console.WriteLine("PASS: five real color-only popup consumers, HEX/drag/focus/switch/restore, old styles and isolated atomic storage.");
        }
        private static PreferenceDocument<T> Document<T>(object host) where T : class
        { return (PreferenceDocument<T>)host.GetType().GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host); }
        private static void Wait(Func<bool> test) { Check(SpinWait.SpinUntil(test, 4000), "bounded asynchronous preference completion"); }
        private static void Check(bool value, string message) { WorldTargetObservationChecks.Check(value, message); }
        private sealed class Probe : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class Idle : IRuntimeFeature { public bool Enabled { get { return false; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
        private sealed class Clipboard : INotesClipboard { public bool TryPaste(out string text) { text = ""; return true; } public bool TryCopy(string text) { return true; } }
        private sealed class Ime : INotesIme { public string Composition { get { return ""; } } public bool Candidates { get { return false; } } public void Toggle(bool active) { } }
    }
}
