using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;

namespace JueMingR.ArchitectureTests
{
    internal static class ContinuousDashGestureChecks
    {
        internal static void Check(List<string> failures)
        { try { Run(); } catch (Exception error) { failures.Add("Continuous dash gesture: " + error); } }
        private static void Run()
        {
            var gesture = new ContinuousDashGesture();
            Require(gesture.Sample(1, 1, false, true, 1, ContinuousDashMode.DoubleTap) == 0, "first press does not arm");
            Require(gesture.Sample(1, 1, false, true, 1, ContinuousDashMode.DoubleTap) == 0, "duplicate sample cannot create a double tap");
            gesture.Sample(2, 2, false, false, 1, ContinuousDashMode.DoubleTap);
            Require(gesture.Sample(19, 3, false, true, 1, ContinuousDashMode.DoubleTap) == 1, "inclusive 18 actual ticks arms");
            Require(gesture.Sample(20, 4, true, true, 1, ContinuousDashMode.DoubleTap) == 0, "opposite press cancels prior arm");
            gesture.Sample(21, 5, false, true, 1, ContinuousDashMode.DoubleTap);
            Require(gesture.Sample(22, 6, true, true, 1, ContinuousDashMode.DoubleTap) == -1, "new opposite double tap chooses recent left while still holding right");
            Require(gesture.Sample(23, 7, false, false, 1, ContinuousDashMode.DoubleTap) == 0, "neutral cannot dash");
            Require(gesture.Sample(24, 8, true, false, 1, ContinuousDashMode.DoubleTap) == -1, "two neutral ticks retain arm");
            gesture.Sample(25, 9, false, false, 1, ContinuousDashMode.DoubleTap); gesture.Sample(26, 10, false, false, 1, ContinuousDashMode.DoubleTap); gesture.Sample(27, 11, false, false, 1, ContinuousDashMode.DoubleTap);
            Require(gesture.Sample(28, 12, true, false, 1, ContinuousDashMode.DoubleTap) == 0, "third neutral tick retires arm and prior candidate");
            gesture.Reset(); gesture.Sample(1, 1, true, false, -1, ContinuousDashMode.DoubleTap); gesture.Sample(2, 2, false, false, -1, ContinuousDashMode.DoubleTap);
            Require(gesture.Sample(20, 3, true, false, -1, ContinuousDashMode.DoubleTap) == 0, "19 ticks is too late");
            gesture.Reset(); Require(gesture.Sample(1, 1, false, true, 1, ContinuousDashMode.Hold) == 1, "hold begins directly");
            Require(gesture.Sample(2, 2, true, true, 1, ContinuousDashMode.Hold) == -1, "hold turn follows recent left");
            Require(gesture.Sample(3, 3, true, false, 1, ContinuousDashMode.Hold) == -1, "hold reverse needs no double tap");
            gesture.Reset(); Require(gesture.Sample(0, 0, true, true, -1, ContinuousDashMode.Hold) == -1, "same tick uses valid facing");
            gesture.Reset(); Require(gesture.Sample(0, 0, true, true, 0, ContinuousDashMode.Hold) == 1, "same tick without facing defaults right");
            gesture.Reset(); Require(gesture.Sample(5, 8, true, false, -1, ContinuousDashMode.DoubleTap) == 0, "cancel/mode switch starts a new gesture");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
