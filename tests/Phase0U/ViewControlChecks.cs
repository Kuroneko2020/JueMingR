using System;
using System.Linq;
using System.Collections;
using System.Reflection;
#if !JMR_NATIVE_VIEW_CONTROLS
using JueMingR.TerrariaHost.F5;
#endif
using Microsoft.Xna.Framework;

namespace Terraria
{
    // Real row/layout/interaction consumers with a narrow command owner. This
    // does not substitute for native zoom input, world drawing or file I/O.
    internal static class ViewControlChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
#if !JMR_NATIVE_VIEW_CONTROLS
        internal static void Run()
        {
            foreach (bool hidden in new[] { false, true }) LayoutAndInput(typeof(F5Interaction).Assembly, hidden);
            var owner = new ViewOwner(); var controls = new ViewControls(owner);
            Require(controls.Available(F5Command.EnableViewUnlock) && controls.Available(F5Command.DisableViewUnlock), "ready public controls");
            Require(controls.Selected(F5Command.DisableViewUnlock) == Color.IndianRed, "default disabled projection");
            controls.Execute(F5Command.EnableViewUnlock);
            Require(owner.Enabled && owner.Commands == 1 && controls.Selected(F5Command.EnableViewUnlock) == Color.LightGreen, "enable reaches actual owner once");
            controls.Execute(F5Command.MarkerDisable);
            Require(owner.Commands == 1 && !ViewControls.Owns(F5Command.MarkerEnable) && !MapControls.Owns(F5Command.EnableViewUnlock), "distinct command ownership");
            controls.Execute(F5Command.DisableViewUnlock);
            Require(!owner.Enabled && controls.Selected(F5Command.DisableViewUnlock) == Color.IndianRed, "retained wide camera cannot turn permission projection green");
            owner.Ready = false;
            controls.Execute(F5Command.EnableViewUnlock);
            Require(!owner.Enabled && owner.Commands == 2 && controls.Hint(F5Command.EnableViewUnlock) == "视距解锁暂不可用。", "loading/unavailable cannot dispatch");
            Console.WriteLine("PASS: public View standard row, exact description, cached layout and click ownership.");
        }

#else
        internal static void RunNative(Assembly host)
        {
            foreach (bool hidden in new[] { false, true }) LayoutAndInput(host, hidden);
            Console.WriteLine("PASS: ViewControlChecks shared row/input/cache assertions executed against the actual Host assembly; native owner commands are checked by the real binding consumer.");
        }
#endif
        // One assertion body is used by the historical fixture and the actual
        // Host probe. Reflection changes access only; it does not replace any
        // product type or maintain a second expected layout implementation.
        private static void LayoutAndInput(Assembly host, bool hidden)
        {
            object state = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.F5.F5Interaction", true), true);
            object input = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Put(state, "Ready", true); Put(input, "Active", true); Put(input, "Focused", true);
            Put(input, "Width", 1280f); Put(input, "Height", 720f); Put(input, "Scale", 1.5f);
            Invoke(state, "Navigate", 2); Invoke(state, "RestoreVisible"); object layout = Read(state, "Layout");
            Invoke(layout, "SetHiddenFeaturesVisible", hidden);
            var size = host.GetType("JueMingR.TerrariaHost.F5.F5Size", true);
            var measure = typeof(ViewControlChecks).GetMethod(nameof(Measure), Flags).MakeGenericMethod(size)
                .CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), size));
            object font = new object(); Action ensure = () => Invoke(layout, "Ensure", 1280f, 720f, 1.5f, (int)Read(state, "Page"), font, measure);
            ensure(); Func<object[]> elements = () => ((IEnumerable)Read(layout, "Elements")).Cast<object>().ToArray();
            var name = elements().Single(e => (string)Read(e, "Text") == "视距解锁");
            Require(Read(name, "Description") != null && (string)Read(Read(name, "Description"), "Text") == "可以调整到更大的视野范围", "exact requested public hint");
            Require(Read(name, "Command").ToString() == "None" && Read(name, "HotkeyTarget") == null, "name is display only");
            Func<object, bool> owns = e => Read(e, "Command").ToString() == "EnableViewUnlock" || Read(e, "Command").ToString() == "DisableViewUnlock";
            var buttons = elements().Where(owns).ToArray();
            Require(buttons.Length == 2 && buttons.Select(e => (string)Read(e, "Text")).SequenceEqual(new[] { "开启", "关闭" }), "only standard Enable/Disable");
            var key = elements().Single(e => (string)Read(e, "HotkeyTarget") == "view-unlock.toggle");
            var keyRect = Read(key, "Rect"); var offRect = Read(buttons[1], "Rect");
            Require(Read(key, "Kind").ToString() == "Hotkey" && Read(key, "Text") == null && Read(key, "Command").ToString() == "None" &&
                Number(keyRect, "Y") >= Number(offRect, "Y") && (Number(keyRect, "Y") > Number(offRect, "Y") || Number(keyRect, "X") >= Number(offRect, "Right")),
                "unique public Hotkey element has null text and follows off");
            Require(buttons.Concat(new[] { key }).All(e => Number(Read(e, "Rect"), "Y") >= 0 && Number(Read(e, "Rect"), "Bottom") <= Number(layout, "ContentHeight") + .01f), "row participates in final scroll geometry");
            int generation = (int)Read(layout, "Generation"), measurements = (int)Read(layout, "MeasurementCount");
            for (int i = 0; i < 100; i++) ensure();
            Require((int)Read(layout, "Generation") == generation && (int)Read(layout, "MeasurementCount") == measurements, "stable page reuses geometry and font metrics");
            Action update = () => Invoke(state, "Update", input);
            Action<object> click = element => {
                object rect = Read(element, "Rect"); Invoke(state, "ScrollTo", Number(rect, "Y"));
                Put(input, "X", Number(state, "X") + Number(Read(layout, "Viewport"), "X") + Number(rect, "X") + 2);
                Put(input, "Y", Number(state, "Y") + Number(Read(layout, "Viewport"), "Y") + Number(rect, "Y") - Number(state, "Scroll") + 2);
                update(); Put(input, "Left", true); update(); Put(input, "Left", false); update();
            };
            update(); click(key);
            Require(ReferenceEquals(Read(state, "ClickedHotkey"), key) && (bool)Read(state, "ConsumeLeft"), "actual public icon hit emits its unique target and owns release");
            click(buttons[0]);
            Require(Read(state, "Command").ToString() == "EnableViewUnlock" && (bool)Read(state, "ConsumeLeft"), "real press/release emits one command and owns release");
            Put(input, "Left", true); update(); Put(input, "Focused", false); Put(input, "Left", false); update();
            Require(Read(state, "Command").ToString() == "None", "focus loss cancels an armed view control");
            Invoke(state, "Navigate", 9); ensure();
            Require(!elements().Any(owns) && !elements().Any(e => (string)Read(e, "HotkeyTarget") == "view-unlock.toggle"), "row is confined to Map page");
        }
        private static T Measure<T>(string text) { return (T)Activator.CreateInstance(typeof(T), Flags, null, new object[] { text.Length * 18f, 24f, 0f, 0f }, null); }
        private static object Read(object value, string name) { return value.GetType().GetProperty(name, Flags)?.GetValue(value) ?? value.GetType().GetField(name, Flags)?.GetValue(value); }
        private static float Number(object value, string name) { return (float)Read(value, name); }
        private static void Put(object value, string name, object data) { var field = value.GetType().GetField(name, Flags); if (field != null) field.SetValue(value, data); else value.GetType().GetProperty(name, Flags).SetValue(value, data); }
        private static object Invoke(object value, string name, params object[] arguments) { return value.GetType().GetMethod(name, Flags).Invoke(value, arguments); }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("View UI: " + message); }

#if !JMR_NATIVE_VIEW_CONTROLS
        private sealed class ViewOwner : IViewControls
        {
            internal bool Ready = true, Enabled;
            internal int Commands;
            public bool ControlsEnabled { get { return Ready; } }
            public bool UnlockEnabled { get { return Enabled; } }
            public bool SetUnlockEnabled(bool value) { Commands++; Enabled = value; return true; }
            public void TakeFeedback(Action<string> display) { }
        }
#endif
    }
}
