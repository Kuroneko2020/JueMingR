using System;
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
            Console.WriteLine("PASS: Phase 0-U input ownership and release tail.");
        }
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
