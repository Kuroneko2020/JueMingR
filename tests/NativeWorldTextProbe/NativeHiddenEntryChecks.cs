using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Guidance;
using JueMingR.Features.HiddenFeatures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These checks drive the built Shell and native mapped samples. Only the
    // clock, foreground HWND and final spawn delegate are controlled substitutes.
    internal static class NativeHiddenEntryChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void RevealForComposition(object context)
        {
            var h = new Gesture(context);
            try
            {
                var access = (HiddenFeatureAccess)Get(h.Shell, "hiddenAccess");
                Require(!access.IsUnlocked, "complete page-composition profile starts Locked");
                h.Reset(); for (int i = 0; i < 5; i++) h.ClickTitle();
                Require(access.IsUnlocked, "complete profile unlocks through five real Shell releases");
            }
            finally { h.Restore(); }
        }
        internal static void Run(object context, object host)
        {
            var h = new Gesture(context);
            var access = (HiddenFeatureAccess)Get(h.Shell, "hiddenAccess");
            Require(access != null && !access.IsUnlocked && ReferenceEquals(access, Get(context, "hiddenAccess")), "real process composition starts Locked and shares one qualification");
            h.Open(1); h.Frame(940, 620);
            Require(!h.Has("SummonMerchant") && !h.Elements.Any(e => (string)GetOptional(e, "Text") == "游商测试"), "Locked initial Misc has no merchant row or label");
            float lockedHeight = (float)Get(h.Layout, "ContentHeight");
            Call(h.Layout, "SetMiscFooterStart", 123f);
            Require(!h.Has("SummonMerchant") && (float)Get(h.Layout, "ContentHeight") == 117f, "Locked dynamic footer advances no hidden row or spacing");
            h.Open(1); h.Frame(940, 620);
            NativeGuidanceChecks.CheckLocked(context, host);
            // The ordinary three Guidance capabilities keep their controls in
            // both states. Their real toggle/persistence checks continue below.
            h.Open(2); Require(h.Has("EnableRare") && h.Has("EnableMerchant"), "Locked map keeps ordinary direction controls");
            h.Open(8); Require(h.Has("EnableEquipment"), "Locked combat keeps ordinary equipment control");
            foreach (string command in new[] { "EnableRare", "EnableMerchant", "EnableEquipment" })
                Require(h.OrdinaryAvailable(command), "Locked ordinary action keeps original availability: " + command);
            int copies = 0;
            Call(h.Page, "Attach", (Func<string, bool>)(_ => { copies++; return true; }), null);
            try
            {
                h.Reset(); h.Point(h.Find("AboutRevealHidden")); h.Sample(true);
                for (int i = 0; i < 20; i++) h.Sample(true);
                Require(!access.IsUnlocked, "twenty held/repeated input callbacks never count as completed clicks"); h.Sample(false);
                h.Reset(); h.Sample(true, true); h.Sample(false);
                for (int i = 0; i < 4; i++) h.ClickTitle();
                Require(!access.IsUnlocked, "a first mixed-button press cannot seed four later legitimate clicks");
                foreach (string cancel in new[] { "hold", "leave-return", "drag", "outside-release", "right", "mixed-buttons", "wheel", "focus", "modal", "close", "page", "geometry", "titlebar" })
                {
                    h.Reset(); for (int i = 0; i < 4; i++) h.ClickTitle();
                    Require(!access.IsUnlocked, "four real clicks remain Locked before " + cancel);
                    h.Cancel(cancel); h.Open(5); h.ClickTitle();
                    Require(!access.IsUnlocked, "partial title sequence cannot survive " + cancel);
                }
                h.Reset();
                object small = h.Elements.First(e => Get(e, "Kind").ToString() == "AboutHeading" && Get(e, "Command").ToString() == "None");
                for (int i = 0; i < 6; i++) h.Click(small);
                Require(!access.IsUnlocked && copies == 0, "right-column heading is neither reveal nor clipboard");
                h.Reset(); int generation = (int)Get(h.Layout, "Generation"), measurements = (int)Get(h.Layout, "MeasurementCount");
                for (int i = 0; i < 4; i++) h.ClickTitle();
                Require(!access.IsUnlocked && generation == (int)Get(h.Layout, "Generation") && measurements == (int)Get(h.Layout, "MeasurementCount"), "first four clicks preserve static About geometry and font cache");
                h.ClickTitle();
                Require(access.IsUnlocked && copies == 0 && !((MerchantTestFeature)Get(host, "MerchantTest")).Pending, "fifth real Shell release only reveals, never copies or queues summon");
                Require((bool)Get(h.State, "ConsumeLeft") && !Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.JustReleased.MouseLeft, "fifth release remains consumed through native mapped input");
                PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require(!Main.LocalPlayer.controlUseItem, "native player input consumer receives no attack from reveal");
                object feedback = Get(h.Shell, "ShortFeedback");
                var shown = ((IEnumerable)Get(feedback, "entries")).Cast<object>().Where(e => (string)Get(e, "Id") == "hidden-entry.revealed").ToArray();
                Require(shown.Length == 1 && (string)Get(shown[0], "Text") == "隐藏功能已显示", "one exact existing local feedback is published");
                for (int i = 0; i < 6; i++) h.ClickTitle();
                Require(ReferenceEquals(shown[0], ((IEnumerable)Get(feedback, "entries")).Cast<object>().Single(e => (string)Get(e, "Id") == "hidden-entry.revealed")), "later clicks do not replace or repeat successful feedback");
                h.Open(1); h.Frame(940, 620);
                Require(h.Has("SummonMerchant") && (float)Get(h.Layout, "ContentHeight") > lockedHeight, "unlocked Misc adds the actual row and only its height");
                // Exercise the second builder independently with an unchanged
                // start offset; visibility is a cache key, not spare row space.
                Call(h.Layout, "SetMiscFooterStart", 123f);
                float revealedHeight = (float)Get(h.Layout, "ContentHeight");
                Call(h.Layout, "SetHiddenFeaturesVisible", false); Call(h.Layout, "SetMiscFooterStart", 123f);
                Require(!h.Has("SummonMerchant") && (float)Get(h.Layout, "ContentHeight") == 117f, "same-start footer removes both row and spacing when projection is hidden");
                Call(h.Layout, "SetHiddenFeaturesVisible", true); Call(h.Layout, "SetMiscFooterStart", 123f);
                Require(h.Has("SummonMerchant") && (float)Get(h.Layout, "ContentHeight") == revealedHeight, "same-start footer notices visible qualification without a position change");
                h.Frame(940, 620);
                generation = (int)Get(h.Layout, "Generation"); measurements = (int)Get(h.Layout, "MeasurementCount");
                for (int i = 0; i < 100; i++) h.Frame(940, 620);
                Require(generation == (int)Get(h.Layout, "Generation") && measurements == (int)Get(h.Layout, "MeasurementCount"), "stable visible state never rebuilds or remeasures per frame");
                var feature = (MerchantTestFeature)Get(host, "MerchantTest"); object port = Get(feature, "port"), previousSpawn = Get(port, "spawn"); int calls = 0;
                try
                {
                    Set(port, "spawn", (Action)(() => calls++));
                    Require(!feature.Pending && calls == 0, "changing pages after unlock still performs no world operation");
                    h.Click(h.Find("SummonMerchant"));
                    Require(feature.Pending && calls == 0, "separate explicit summon click queues one intent");
                    Call(context, "UpdateRuntime"); for (int i = 0; i < 8; i++) Call(context, "UpdateRuntime");
                    Require(calls == 1 && !feature.Pending, "complete Locked-to-five-click-to-Misc chain reaches one controlled native spawn outlet");
                }
                finally { Set(port, "spawn", previousSpawn); }
                h.Open(5); h.Click(h.Find("AboutCopyGroup")); Require(copies == 1, "QQ still invokes the clipboard once after reveal");
                Call(h.State, "Close"); h.Open(2); Require(access.IsUnlocked && h.Has("EnableRare") && h.Has("EnableMerchant"), "completed access survives UI exits and keeps directions");
                h.Open(8); Require(h.Has("EnableEquipment"), "Unlocked equipment entry remains ordinary");
                Console.WriteLine("PASS: HIDE real Shell five-click chain, thirteen cancellation classes, one local result, no clipboard/attack/spawn on reveal, explicit one-spawn consumer, Locked/Unlocked ordinary controls and stable layout.");
            }
            finally { h.Restore(); }
        }

        internal static void Geometry(object context, int width, int height, float scale)
        {
            var h = new Gesture(context);
            try
            {
                h.Geometry(width, height, scale); h.Open(5); h.Frame(20, 20); h.Open(5);
                h.ClickTitle(); Require(Get(h.State, "Command").ToString() == "AboutRevealHidden", "actual scaled Shell hits only the top heading");
                object title = h.Find("AboutRevealHidden"); h.Point(title); h.Sample(true);
                float maxScroll = (float)Get(h.Layout, "MaxScroll");
                if (maxScroll > 0) Call(h.State, "ScrollTo", maxScroll); else h.Sample(true, false, -120);
                h.Sample(false);
                Require(Get(h.State, "Command").ToString() == "None", "real clipped/scrolled title cannot retain an old press");
                h.Open(5); h.Point(h.Find("AboutRevealHidden")); h.Sample(true);
                // This forces viewport reflow while held; the visual fixture
                // separately replaces the actual XNB font/device while armed.
                h.Geometry(width, height - 12, scale); h.Sample(false);
                Require(Get(h.State, "Command").ToString() == "None", "viewport reflow rejects the scaled old title target");
                h.Geometry(width, height, scale); h.Open(5);
                int generation = (int)Get(h.Layout, "Generation"), measured = (int)Get(h.Layout, "MeasurementCount");
                for (int i = 0; i < 20; i++) h.Frame(h.X, h.Y);
                Require(generation == (int)Get(h.Layout, "Generation") && measured == (int)Get(h.Layout, "MeasurementCount"), "XNB scaled title input keeps stable measured geometry");
            }
            finally { h.Restore(); }
        }

        internal static Action ArmForFontChange(object context)
        {
            var h = new Gesture(context); h.Geometry(1280, 1080, 1); h.Open(5); h.Frame(20, 20); h.Open(5);
            h.Point(h.Find("AboutRevealHidden")); h.Sample(false); h.Sample(true);
            return () =>
            {
                try { h.Sample(false); Require(Get(h.State, "Command").ToString() == "None", "actual replacement XNB font/device cancels the armed hidden heading"); }
                finally { h.Restore(); }
            };
        }

        private sealed class Gesture
        {
            internal readonly object Shell, State, Layout, Page;
            private readonly object input, renderer, oldClock, oldForeground;
            private int width = 960, height = 640; private float scale = 1;
            private long now = 10000;
            internal float X, Y;
            internal object[] Elements { get { return ((IEnumerable)Get(Layout, "Elements")).Cast<object>().ToArray(); } }
            internal Gesture(object context)
            {
                Shell = Get(context, "Shell"); State = Get(Shell, "State"); Layout = Get(State, "Layout"); Page = Get(Layout, "About"); input = Get(context, "Input"); renderer = Get(Shell, "renderer");
                oldClock = Get(State, "TitleMilliseconds"); oldForeground = Get(input, "foregroundWindow");
                Set(State, "TitleMilliseconds", (Func<long>)(() => now));
                Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                Set(Shell, "LayersReady", true); Set(State, "Ready", true); Geometry(960, 640, 1);
            }
            internal void Restore() { Set(State, "TitleMilliseconds", oldClock); Set(input, "foregroundWindow", oldForeground); }
            internal void Geometry(int w, int h, float s)
            {
                width = w; height = h; scale = s;
                typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.CreateScale(s));
                typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, w); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, h);
                typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            }
            internal void Open(int page)
            { Call(State, "Navigate", page); Call(State, "RestoreVisible"); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", State, (float)width, (float)height, scale); }
            internal object Find(string command) { return Elements.First(e => Get(e, "Command").ToString() == command); }
            internal bool Has(string command) { return Elements.Any(e => Get(e, "Command").ToString() == command); }
            internal bool OrdinaryAvailable(string command)
            {
                var kind = Shell.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Command");
                return (bool)Call(Get(renderer, "GuidanceControls"), "Available", Enum.Parse(kind, command));
            }
            internal void Reset() { Call(Page, "Leave"); Open(5); Call(State, "ScrollTo", 0f); Point(Find("AboutRevealHidden")); Sample(false); }
            internal void Point(object e)
            {
                object r = Get(e, "Rect"), v = Get(Layout, "Viewport");
                X = (float)Get(State, "X") + (float)Get(v, "X") + (float)Get(r, "X") + (float)Get(r, "Width") / 2;
                Y = (float)Get(State, "Y") + (float)Get(v, "Y") + (float)Get(r, "Y") - (float)Get(State, "Scroll") + (float)Get(r, "Height") / 2;
            }
            internal void ClickTitle() { Click(Find("AboutRevealHidden")); }
            internal void Click(object e)
            {
                object r = Get(e, "Rect"), v = Get(Layout, "Viewport");
                Call(State, "ScrollTo", Math.Max(0, (float)Get(r, "Bottom") - (float)Get(v, "Height") + 4)); Point(e);
                Sample(false); Sample(true); Sample(true); Sample(false);
            }
            internal void Sample(bool left, bool right = false, int wheel = 0) { Frame(X, Y, left, right, wheel); }
            internal void Frame(float x, float y, bool left = false, bool right = false, int wheel = 0)
            {
                X = x; Y = y; now += 20;
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; Main.keyState = new KeyboardState(); FocusHelper.IsSelectedApplication = true;
                PlayerInput.MouseInfo = new MouseState((int)(x * scale), (int)(y * scale), 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = wheel;
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Current.MouseRight = right; PlayerInput.Triggers.Update(); Main.mouseLeft = left;
                Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(Shell, "ProcessInput");
                Require(!(bool)Get(Shell, "Failed"), "HIDE real input sample remains healthy");
            }
            internal void Cancel(string cause)
            {
                Point(Find("AboutRevealHidden")); float x = X, y = Y;
                if (cause == "hold") { Sample(true); now += 501; Sample(true); Sample(false); }
                if (cause == "leave-return" || cause == "outside-release") { Sample(true); Frame(1, 1, true); if (cause == "leave-return") Frame(x, y, true); Frame(cause == "leave-return" ? x : 1, cause == "leave-return" ? y : 1); }
                if (cause == "drag") { Sample(true); Frame(x + 8, y, true); Frame(x, y, true); Sample(false); }
                if (cause == "right") { Sample(false, true); Sample(false); }
                if (cause == "mixed-buttons") { Sample(true, true); Sample(false); }
                if (cause == "wheel") { Sample(false, false, -120); Call(State, "ScrollTo", 0f); }
                if (cause == "focus")
                {
                    Sample(true); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(2))); Sample(false);
                    Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1))); Sample(false); Sample(false); Sample(false);
                }
                if (cause == "modal") { Set(input, "HotkeyCapture", true); Sample(false); Set(input, "HotkeyCapture", false); Sample(false); }
                if (cause == "close") { Call(State, "Close"); Sample(false); }
                if (cause == "page") { Call(State, "Navigate", 2); Sample(false); }
                if (cause == "geometry") { Sample(true); Geometry(960, 620, 1); Sample(false); Geometry(960, 640, 1); }
                if (cause == "titlebar") { Frame((float)Get(State, "X") + 80, (float)Get(State, "Y") + 14, true); Sample(false); }
            }
        }
    }
}
