using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintCaptureChecks
    {
        internal static void Check(IList<string> failures)
        {
            Action<bool, string> require = (value, name) => { if (!value) failures.Add("Blueprint: " + name); };
            var state = new BlueprintCaptureState();
            state.Observe(true, true, false, true, false); state.Enter();
            state.Pointer(true, true, false, 1, 1, 100, 100); state.Pointer(true, false, false, 1, 1, 100, 100);
            require(!state.Dragging && !state.Selection.HasSelection, "entry click is neutral barrier, not first drag");
            state.Pointer(true, true, false, 1, 1, 100, 100);
            for (int i = 0; i < 30; i++) state.Pointer(true, true, false, 4, 5, 100, 100);
            require(!state.Selection.HasSelection && state.Dragging, "many held frames only preview");
            state.Pointer(true, false, false, 4, 5, 100, 100);
            require(state.Selection.EnumerateVisible(0, 0, 100, 100).Sum(s => s.RightExclusive - s.Left) == 20, "one physical release commits inclusive rectangle once");
            state.Pointer(true, false, false, 4, 5, 100, 100);
            require(state.Selection.HasSelection, "repeated release cannot XOR twice");
            state.Pointer(true, true, false, 8, 8, 100, 100); state.Pointer(false, false, false, 9, 9, 100, 100);
            require(state.Active && state.Selection.HasSelection && !state.Dragging, "UI/geometry/focus interruption preserves committed mode and selection");
            state.Pointer(true, true, false, 9, 9, 100, 100);
            require(!state.Dragging, "held reactivation does not continue old drag");
            state.Pointer(true, false, false, 9, 9, 100, 100); state.Pointer(true, true, false, 8, 8, 100, 100); state.Interrupt();
            require(state.Active && state.Selection.HasSelection && !state.Dragging, "death interrupt has no implicit exit");
            state.Observe(true, true, true, false, false); state.Exit();
            require(!state.Active && !state.Selection.HasSelection && state.BlockedActions == 3, "exit clears current session but owns both held tails");
            state.Observe(false, false, false, false, false);
            require(state.BlockedActions == 3, "synthetic lost-focus release is not proof");
            state.Observe(true, false, true, false, false);
            require(state.BlockedActions == 3, "left release frame stays blocked");
            state.Observe(true, false, true, false, false);
            require(state.BlockedActions == 2, "left neutral does not release held right");
            state.Observe(true, false, false, false, false); require(state.BlockedActions == 2, "right release frame stays blocked");
            state.Observe(true, false, false, false, false); require(state.BlockedActions == 0, "trusted neutral retires independently of edit admission");
            state.Observe(true, false, false, true, true); state.Enter(); state.Exit();
            state.Observe(true, false, false, true, false); state.Observe(true, false, false, true, false);
            require(state.BlockedActions == 1, "keyboard UseItem remains blocked after UseTile neutral");
            state.Observe(true, false, false, false, false); state.Observe(true, false, false, false, false);
            require(state.BlockedActions == 0, "keyboard use neutral cannot self-lock behind CanEdit");
            var codec = new BlueprintPreferenceCodec();
            require(!codec.Decode(codec.Encode(false)) && codec.Decode(codec.Encode(true)), "only toolbar preference round-trips");
            bool rejected = false;
            try { codec.Decode(System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Blueprint\",\"version\":1,\"gelToolbar\":true,\"selection\":[]}")); }
            catch (JueMingR.Platform.Settings.PreferenceFormatException) { rejected = true; }
            require(rejected, "unknown configuration protects disk and cannot import selection");
        }
    }
}
