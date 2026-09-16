using System;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class FootprintPopupChecks
    {
        internal static void Run()
        {
            FocusHelper.IsSelectedApplication = true;
            var owner = new Owner(); var driver = new Driver(owner); driver.Popup.Open(2); driver.Prepare();
            driver.Click(2); Require(owner.Deleted == 0 && driver.Label == "清除足迹", "unpresented control cannot confirm");
            driver.Present(); driver.Click(2); Require(driver.Label == "确定？" && owner.Deleted == 0, "first rendered click only advances stage");
            driver.Click(2); driver.Click(2); Require(driver.Label == "确定？" && owner.Deleted == 0, "multiple Update gestures without Draw cannot advance");
            driver.Present(); driver.Click(2); Require(driver.Label == "不可恢复，确定？" && owner.Deleted == 0, "second click still cannot delete");
            driver.Present(); driver.Press(2); owner.Revision++; driver.Release(); Require(owner.Deleted == 1, "third real click targets archive; appending a tail does not cancel");
            driver.Release(); Require(owner.Deleted == 1, "duplicate release is inert");
            driver.Popup.Open(2); driver.Present(); driver.Click(2); driver.Present(); driver.Press(2); owner.Generation = new string('b', 32); driver.Release();
            Require(owner.Deleted == 1 && driver.Label == "清除足迹", "archive change cancels confirmation");
            driver.Present(); driver.Click(2); driver.Present(); driver.Click(3); Require(driver.Label == "清除足迹", "cancel resets all stages");
            driver.Click(1); Require(!owner.Recording && owner.Display == false, "recording control never changes display");
            driver.Present(); driver.Click(2); driver.Present(); driver.Press(2); driver.Font = new object(); driver.Release(); Require(driver.Label == "清除足迹" && owner.Deleted == 1, "font replacement cancels old press and confirmation");
            driver.Click(0); Require(!driver.Popup.Visible, "close stays reachable");
            Console.WriteLine("PASS: footprint popup three physically released and presented stages; cancellation, archive identity, resource changes, independent recording.");
        }
        private static void Require(bool value, string why) { if (!value) throw new Exception(why); }
        private sealed class Owner : IFootprintControls
        {
            public long Session { get; set; } = 1;
            public bool ControlsEnabled { get { return true; } }
            public bool Display { get; private set; }
            public bool Recording { get; private set; } = true;
            public string Generation { get; set; } = new string('a', 32);
            public string StatusMessage { get { return "正在录制；关闭显示仍会记录。"; } }
            public bool CanClear { get { return true; } }
            public bool Clearing { get { return false; } }
            public bool CanRetryClear { get { return false; } }
            public bool CanRetrySave { get { return false; } }
            public bool HasIssue { get { return false; } }
            internal int Deleted, Revision;
            public bool SetDisplay(bool value) { Display = value; return true; }
            public bool SetRecording(bool value) { Recording = value; return true; }
            public bool Clear(string generation) { Require(generation == Generation, "clear exact target"); Deleted++; return true; }
            public void RetryClear() { }
            public void RetrySave() { }
            public void TakeFeedback(Action<string> show) { }
            public void PrepareConfiguration() { }
        }
        private sealed class Driver
        {
            private readonly HostInputState input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
            internal readonly FootprintPopup Popup;
            internal object Font = new object();
            private float x, y;
            internal string Label { get { return Popup.Buttons[Popup.Commands.IndexOf(2)].Text; } }
            internal Driver(Owner owner) { Popup = new FootprintPopup(owner, input); Step(false); }
            internal void Prepare() { Popup.Prepare(800, 600, Font, (s, scale) => new F5Size(s.Length * 12 * scale, 30 * scale), 0); }
            internal void Present() { Prepare(); Popup.Presented(); }
            internal void Press(int command) { var r = Popup.Buttons[Popup.Commands.IndexOf(command)].Rect.Offset(Popup.Panel.X, Popup.Panel.Y); x = r.X + 4; y = r.Y + 4; Step(true); }
            internal void Release() { Step(false); }
            internal void Click(int command) { Press(command); Release(); }
            private void Step(bool left)
            {
                input.BeginUpdate(); PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                input.AfterMapping(); Main.keyState = new KeyboardState(); input.AfterKeyboardRefresh(); Popup.Process(true, 2, x, y, Popup.Matches(800, 600, Font, 0)); Prepare();
            }
        }
    }
}
