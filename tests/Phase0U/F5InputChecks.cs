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
            Console.WriteLine("PASS: Phase 0-U input ownership and release tail.");
        }
        internal static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
