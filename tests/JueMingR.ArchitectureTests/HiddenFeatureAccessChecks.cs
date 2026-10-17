using System;
using JueMingR.Features.HiddenFeatures;

namespace JueMingR.ArchitectureTests
{
    internal static class HiddenFeatureAccessChecks
    {
        internal static void Run()
        {
            var access = new HiddenFeatureAccess();
            Require(!access.IsUnlocked, "new process owner starts Locked");
            for (int i = 0; i < 4; i++) Require(!access.TryCompleteTitleClick(i * 500, i), "four clicks cannot unlock");
            Require(access.TryCompleteTitleClick(2000, 4) && access.IsUnlocked, "inclusive 2000ms boundary");
            access.CancelGesture(); Require(access.IsUnlocked && !access.TryCompleteTitleClick(2100, 5), "cancel retains success and notifications never repeat");
            access = new HiddenFeatureAccess();
            foreach (long t in new long[] { 0, 1900, 2000, 2100, 2200 }) Require(!access.TryCompleteTitleClick(t, t), "expired prefix does not grant success");
            Require(access.TryCompleteTitleClick(2300, 2300), "recent five survive expired prefix");
            access = new HiddenFeatureAccess();
            for (int i = 0; i < 4; i++) access.TryCompleteTitleClick(i * 500, i);
            Require(!access.TryCompleteTitleClick(2001, 4), "2001ms is outside window");
            for (int i = 0; i < 20; i++) Require(!access.TryCompleteTitleClick(2100, 4), "repeated physical identity never counts");
            access.CancelGesture();
            for (int i = 5; i < 9; i++) Require(!access.TryCompleteTitleClick(2200 + i, i), "cancel clears unfinished sequence");
            Require(access.TryCompleteTitleClick(2210, 9), "fresh five after cancellation unlock");
            var second = new HiddenFeatureAccess(); Require(!second.IsUnlocked, "new process owner does not inherit another owner's success");
            second.TryCompleteTitleClick(100, 0); second.TryCompleteTitleClick(50, 1);
            for (int i = 2; i < 6; i++) Require(!second.TryCompleteTitleClick(100 + i, i), "clock regression discards unfinished history");
            Require(second.TryCompleteTitleClick(110, 6), "valid finite sequence resumes after clock regression");
            Console.WriteLine("Hidden access: default Locked; four/five, inclusive/expired rolling window, repeat identity, cancel, process lifetime and time regression PASS.");
        }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Hidden access: " + reason); }
    }
}
