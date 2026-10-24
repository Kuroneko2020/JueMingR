using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    // CPU-only checks of the real layout/controls. Native input, copying and
    // powers remain separate original-Terraria checks, not claims from this fake.
    internal static class JourneyUiChecks
    {
        internal static void Run()
        {
            CheckClassification();
            CheckLayout(false); CheckLayout(true);
            var owner = new JourneyUiOwner(); var controls = new JourneyControls(owner);
            Require(controls.Available(F5Command.EnableJourney) && !controls.Available(F5Command.DisableJourney), "OFF can enable but cannot disable");
            Require(controls.Selected(F5Command.DisableJourney).HasValue && !controls.Selected(F5Command.EnableJourney).HasValue, "OFF projects one red state line");
            owner.Active = true;
            Require(controls.Available(F5Command.EnableJourney) && controls.Available(F5Command.DisableJourney), "active Enable can reopen or Disable can end its session");
            owner.Ready = false;
            Require(!controls.Available(F5Command.EnableJourney) && controls.Available(F5Command.DisableJourney), "enable readiness cannot trap active close");
            Require(controls.Selected(F5Command.EnableJourney).HasValue && !controls.Selected(F5Command.DisableJourney).HasValue, "actual activation remains the sole green projection");
            Require(owner.ToggleCalls == 0 && owner.EnableCalls == 0 && owner.OpenCalls == 0, "layout/status/hints never submit commands");
            Console.WriteLine("PASS: Journey hidden standard rows, both layout paths, stable reflow and actual-state projection.");
        }
        private static void CheckClassification()
        {
            var layout = new F5Layout(); object font = new object();
            layout.Ensure(1280, 720, 1.5f, 8, font, Measure);
            Require(Find(layout, "combat.npc-path") == null, "locked combat page has no path row or binding");
            int lockedGeneration = layout.Generation;
            layout.SetHiddenFeaturesVisible(true);
            layout.Ensure(1280, 720, 1.5f, 8, font, Measure);
            Require(layout.Generation == lockedGeneration + 1 && Has(layout, "寻路预测") && Find(layout, "combat.npc-path") != null,
                "unlock on the combat page invalidates its layout once and exposes path prediction");
            Require(!Has(layout, "旅行菜单") && !Has(layout, "游商测试"), "non-combat hidden entries stay in Misc");
            int generation = layout.Generation, measures = layout.MeasurementCount;
            for (int i = 0; i < 100; i++) layout.Ensure(1280, 720, 1.5f, 8, font, Measure);
            Require(layout.Generation == generation && layout.MeasurementCount == measures, "stable unlocked combat page does not reflow or remeasure");
            foreach (bool footer in new[] { false, true })
            {
                layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
                if (footer) layout.SetMiscFooterStart(180);
                Require(!Has(layout, "寻路预测") && Find(layout, "combat.npc-path") == null && Has(layout, "旅行菜单") && Has(layout, "游商测试"),
                    "both Misc layout paths retain only their own hidden entries");
                layout.Ensure(1280, 720, 1.5f, 8, font, Measure);
                Require(Has(layout, "寻路预测") && Find(layout, "combat.npc-path") != null, "returning to combat restores its single path row");
            }
        }
        private static void CheckLayout(bool footer)
        {
            var layout = new F5Layout(); object font = new object();
            layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
            if (footer) layout.SetMiscFooterStart(180);
            float lockedHeight = layout.ContentHeight;
            Require(Find(layout, JourneyControls.ActionId) == null && !Has(layout, "旅行菜单") && !Has(layout, "游商测试"), "locked layout has neither hidden entry nor binding");
            int lockedGeneration = layout.Generation, lockedMeasures = layout.MeasurementCount;
            for (int i = 0; i < 100; i++)
            {
                if (footer) layout.SetMiscFooterStart(180); else layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
            }
            Require(layout.Generation == lockedGeneration && layout.MeasurementCount == lockedMeasures, "locked stable page reuses its layout");
            layout.SetHiddenFeaturesVisible(true);
            if (footer) layout.SetMiscFooterStart(180); else layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
            int buttons = 0; F5Element journeyName = null, merchantName = null;
            foreach (var element in layout.Elements)
            {
                if (element.Text == "旅行菜单") journeyName = element;
                if (element.Text == "游商测试") merchantName = element;
                if (JourneyControls.Owns(element.Command)) buttons++;
                Require(element.Rect.Y >= 0 && element.Rect.Bottom <= layout.ContentHeight + .01f, "hidden row remains in scrollable content");
            }
            Require(buttons == 2 && journeyName != null && merchantName != null && journeyName.Rect.Y > merchantName.Rect.Y, "two real commands form the row next to merchant test");
            Require(Has(layout, "开启") && Has(layout, "关闭") && !Has(layout, "打开"), "both layout paths retain Enable/Disable without an Open element or hitbox");
            Require(journeyName.Description != null && journeyName.Description.Text == "单人临时使用原版旅行菜单", "name hover carries exact product description");
            var binding = Find(layout, JourneyControls.ActionId);
            Require(binding != null && binding.Kind == F5ElementKind.Hotkey && binding.Command == F5Command.None, "shared binding slot does not execute business");
            int generation = layout.Generation, measurements = layout.MeasurementCount;
            for (int i = 0; i < 100; i++)
            {
                if (footer) layout.SetMiscFooterStart(180); else layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
            }
            Require(layout.Generation == generation && layout.MeasurementCount == measurements, "unlocked stable page reuses geometry and metrics");
            layout.SetHiddenFeaturesVisible(false);
            if (footer) layout.SetMiscFooterStart(180); else layout.Ensure(1280, 720, 1.5f, 1, font, Measure);
            Require(layout.ContentHeight == lockedHeight && Find(layout, JourneyControls.ActionId) == null, "hidden rows leave no blank space");
        }
        private static F5Size Measure(string text) { return new F5Size(text.Length * 18, 24); }
        private static F5Element Find(F5Layout layout, string id)
        { foreach (var element in layout.Elements) if (element.HotkeyTarget == id) return element; return null; }
        private static bool Has(F5Layout layout, string text)
        { foreach (var element in layout.Elements) if (element.Text == text) return true; return false; }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Journey UI: " + message); }
    }
    internal sealed class JourneyUiOwner : IJourneyControls
    {
        internal bool Active, Ready = true, Menu;
        internal int ToggleCalls, EnableCalls, OpenCalls;
        public bool IsActive { get { return Active; } }
        public bool IsMenuOpen { get { return Menu; } }
        public bool CanEnable { get { return Ready; } }
        public string UnavailableReason { get { return Ready ? null : "当前暂不可用。"; } }
        public string LastFailure { get { return null; } }
        public bool SetEnabled(bool enabled) { EnableCalls++; if (enabled && Active) return Open(); Active = enabled; Menu = enabled; return true; }
        public bool Toggle() { ToggleCalls++; Active = !Active; Menu = Active; return true; }
        public bool Open() { OpenCalls++; Menu = true; return true; }
        public bool CloseMenu() { Menu = false; return true; }
    }
}
