using System;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class F5InputChecks
    {
        internal static void Run()
        {
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Width = 1920, Height = 1080, Scale = 1,
                Active = true, Focused = true, Right = false, X = 1900, Y = 900 };
            state.Update(input);
            Check(!state.Visible && !state.OwnsPointer, "closed window preserves world hover");
            input.F5 = true;
            state.Update(input);
            Check(state.Visible, "F5 rising edge opens the window");
            state.Update(input);
            Check(state.Visible, "held F5 must not toggle repeatedly");
            input.F5 = false;
            input.X = state.X + 30; input.Y = state.Y + 150;
            input.Left = true; input.Wheel = 120;
            state.Update(input);
            Check(state.ConsumeLeft && state.ConsumeWheel && state.OwnsPointer,
                "first entry owns click, wheel and pure hover before consumers");
            input.X = 1900; input.Y = 900; input.Wheel = 0;
            state.Update(input);
            Check(state.ConsumeLeft, "owned click cannot transfer to world while held outside");
            input.Focused = false; input.Left = false;
            state.Update(input);
            input.Focused = true; input.Left = true;
            state.Update(input);
            Check(state.ConsumeLeft, "focus-loss synthetic release cannot end an owned press");
            input.Left = false;
            state.Update(input);
            Check(state.ConsumeLeft, "release sample is also consumed");
            input.Left = true;
            state.Update(input);
            Check(!state.ConsumeLeft, "new outside click is available without restart");
            input.Left = false; state.Update(input);
            input.F5 = true; state.Update(input);
            input.F5 = false; state.Update(input);
            input.X = state.X + 20; input.Y = state.Y + 150;
            input.F5 = true; input.Left = true; input.Right = true; input.Wheel = 120;
            state.Update(input);
            Check(!state.Visible && state.ConsumeLeft && state.ConsumeRight && state.ConsumeWheel,
                "closing and first owned input in one sample cannot fall through");
            CheckScrollChannel();
            CheckViewportDoesNotReplacePosition();
            CheckRestoredPosition();
            CheckDragSubmission();
            CheckInterruptedDrag();
            CheckInvalidDragSamples();
            Console.WriteLine("PASS: Phase 0-U input ownership and release tail.");
        }
        private static void CheckViewportDoesNotReplacePosition()
        {
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Width = 1920, Height = 1080, Scale = 1,
                Active = true, Focused = true, F5 = true };
            state.Update(input);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page,
                new object(), text => new F5Size(text.Length * 10, 20));
            input.F5 = false; input.Left = true;
            input.X = state.X + 20; input.Y = state.Y + 20;
            state.Update(input);
            input.X = 1120; input.Y = 270;
            state.Update(input);
            input.Left = false; state.Update(input);
            Check(state.X == 1100 && state.Y == 250, "title drag completes in integer UI logical coordinates");
            input.Width = 1280; input.Height = 720;
            state.Update(input);
            Check(state.X == 688 && state.Y == 12, "smaller viewport clamps the displayed position with twelve-unit margins");
            input.Width = 1920; input.Height = 1080;
            state.Update(input);
            Check(state.X == 1100 && state.Y == 250, "viewport clamp must not replace the user's completed position");
        }
        private static void CheckRestoredPosition()
        {
            F5Input input;
            F5Interaction state = OpenPositionState(new WindowPosition(1100, 250), out input);
            Check(state.X == 1100 && state.Y == 250, "saved UI logical position restores on first open");
            input.Width = 1280; input.Height = 720; input.Scale = 1.5f;
            state.Update(input);
            Check(state.X == 261 && state.Y == 12, "restored position clamps in the current scaled logical viewport");
            Check(state.TakePositionToSave() == null, "automatic viewport clamp never requests a save");
            input.Width = 1920; input.Height = 1080; input.Scale = 1;
            state.Update(input);
            Check(state.X == 1100 && state.Y == 250, "larger viewport recovers the stored preference");
            state.RestorePosition(new WindowPosition(int.MaxValue, int.MinValue));
            state.Update(input);
            Check(state.X == 1328 && state.Y == 12, "finite historical integer coordinates are clamped without overflow");
            state.RestorePosition(null); state.Update(input);
            Check(state.X == 670 && state.Y == 170, "missing position preserves default centering");
            Check(state.TakePositionToSave() == null, "restoring or centering does not manufacture user intent");
        }
        private static void CheckDragSubmission()
        {
            F5Input input;
            F5Interaction state = OpenPositionState(new WindowPosition(900, 200), out input);
            StartTitleDrag(state, ref input);
            input.X = 1020.9f; input.Y = 260.9f; state.Update(input);
            Check(state.TakePositionToSave() == null, "held drag never submits per-frame persistence work");
            input.Left = false; input.X = 1030.9f; input.Y = 270.9f; state.Update(input);
            CheckPosition(state.TakePositionToSave(), 1010, 250, "physical release submits its final integer logical position");
            state.Update(input);
            Check(state.TakePositionToSave() == null, "drag release submits only once");
            StartTitleDrag(state, ref input);
            input.Left = false; state.Update(input);
            Check(state.TakePositionToSave() == null, "a title click without movement does not request saving");
            input.Width = 1280; input.Height = 720; state.Update(input);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page,
                new object(), text => new F5Size(text.Length * 10, 20));
            StartTitleDrag(state, ref input);
            input.Left = false; state.Update(input);
            Check(state.TakePositionToSave() == null, "clicking an automatically clamped title cannot overwrite the preferred position");
            StartTitleDrag(state, ref input);
            input.X = 520; input.Y = 32; state.Update(input);
            input.Left = false; state.Update(input);
            CheckPosition(state.TakePositionToSave(), 500, 12, "deliberate drag in a smaller viewport establishes a new preference");
            input.Width = 1920; input.Height = 1080; state.Update(input);
            Check(state.X == 500 && state.Y == 12, "new deliberate position replaces the old projection");
        }
        private static void CheckInterruptedDrag()
        {
            for (int reason = 0; reason < 5; reason++)
            {
                F5Input input;
                F5Interaction state = OpenPositionState(new WindowPosition(900, 200), out input);
                StartTitleDrag(state, ref input);
                input.X = 1020; input.Y = 260; state.Update(input);
                input.X = 0; input.Y = 0;
                if (reason == 0) state.Close();
                else
                {
                    if (reason == 1) input.F5 = true;
                    if (reason == 2) { input.Focused = false; input.Left = false; }
                    if (reason == 3) input.Active = false;
                    if (reason == 4) { input.Width = 1280; input.Height = 720; input.Scale = 1.5f; }
                    state.Update(input);
                }
                CheckPosition(state.TakePositionToSave(), 1000, 240,
                    "interrupted drag keeps the last valid focused sample: " + reason);
                state.Close();
                Check(state.TakePositionToSave() == null, "repeated cancellation cannot duplicate a position submission");
            }
        }
        private static F5Interaction OpenPositionState(WindowPosition position, out F5Input input)
        {
            var state = new F5Interaction { Ready = true };
            state.RestorePosition(position);
            input = new F5Input { Width = 1920, Height = 1080, Scale = 1,
                Active = true, Focused = true, F5 = true };
            state.Update(input);
            input.F5 = false; state.Update(input);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page,
                new object(), text => new F5Size(text.Length * 10, 20));
            return state;
        }
        private static void CheckInvalidDragSamples()
        {
            for (int invalid = 0; invalid < 4; invalid++)
            {
                F5Input input;
                F5Interaction state = OpenPositionState(new WindowPosition(900, 200), out input);
                StartTitleDrag(state, ref input);
                input.X = 1020; input.Y = 260; state.Update(input);
                if (invalid == 0) input.X = float.NaN;
                if (invalid == 1) input.Y = float.PositiveInfinity;
                if (invalid == 2) input.Width = 600;
                if (invalid == 3) input.Scale = 0;
                bool rejected = false;
                try { state.Update(input); }
                catch (InvalidOperationException) { rejected = true; state.Close(); }
                Check(rejected, "invalid position sample uses the existing local failure path: " + invalid);
                CheckPosition(state.TakePositionToSave(), 1000, 240,
                    "invalid pointer or viewport cannot replace the last valid drag sample");
                Check(state.ConsumeLeft, "invalid sample cannot release the owned mouse tail to the world");
            }
        }
        private static void StartTitleDrag(F5Interaction state, ref F5Input input)
        {
            input.Left = true; input.X = state.X + 20; input.Y = state.Y + 20;
            state.Update(input);
        }
        private static void CheckPosition(WindowPosition position, int x, int y, string message)
        { Check(position != null && position.X == x && position.Y == y, message); }
        private static void CheckScrollChannel()
        {
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Width = 1280, Height = 720, Scale = 1.5f,
                Active = true, Focused = true, F5 = true, X = 1900, Y = 1000 };
            state.Update(input);
            var font = new object();
            Func<string, F5Size> measure = text => new F5Size(text.Length * 18, 24);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, measure);
            F5Rect track = state.Layout.ScrollTrack;
            input.F5 = false; input.Left = true;
            // The invisible outer part of the original 10-unit channel remains grabbable.
            input.X = state.X + track.X + 0.5f; input.Y = state.Y + track.Bottom - 1;
            state.Update(input);
            Check(state.DraggingScroll && state.Scroll > 0, "thin visual keeps the full original grab width");
            input.Y = state.Y + track.Y - 100; state.Update(input);
            Check(state.Scroll == 0, "scroll drag clamps above the track");
            input.Y = state.Y + track.Bottom + 100; state.Update(input);
            F5LayoutChecks.Equal(state.Layout.MaxScroll, state.Scroll, "scroll drag clamps below the track");
            input.Left = false; state.Update(input);
            state.Layout.Ensure(input.Width, input.Height, input.Scale, 0, font, measure);
            state.ClampScroll();
            Check(state.Scroll == 0, "shortened content immediately clamps an old offset");
        }
        internal static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
