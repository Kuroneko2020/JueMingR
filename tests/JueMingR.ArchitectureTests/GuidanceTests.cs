using System;
using System.Collections.Generic;
using JueMingR.Features.Guidance;
using JueMingR.Platform.Guidance;
using JueMingR.Platform.Operations;

namespace JueMingR.ArchitectureTests
{
    internal static class GuidanceTests
    {
        internal static void Check(IList<string> failures)
        {
            try { Selection(); Projection(); Equipment(); Locations(); Operations(); Preferences(); }
            catch (Exception e) { failures.Add("Guidance: " + e.Message); }
        }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static GuidanceNpc Npc(int slot, int type, int rarity, float x, float y = 0)
        { return new GuidanceNpc { Identity = new object(), Slot = slot, StableIndex = slot, Type = type, NetId = type, Active = true, Life = 10, Rarity = rarity, X = x, Y = y }; }
        private sealed class Source : IGuidanceNpcSource, IGuidanceLocationSource
        {
            internal readonly GuidanceNpc[] Npcs = new GuidanceNpc[200];
            internal GuidancePylon[] Pylons = new GuidancePylon[0];
            internal int Reads, PylonReads;
            internal bool WorldKnown = true;
            public int Count { get { return Npcs.Length; } }
            public bool TryRead(int slot, NpcDemand demand, out GuidanceNpc npc) { Reads++; npc = Npcs[slot]; return true; }
            public int PylonCount { get { return Pylons.Length; } }
            public bool TryPylon(int i, out GuidancePylon p) { PylonReads++; p = Pylons[i]; return true; }
            public bool TryWorld(out int width, out int height, out double surface) { width = 8400; height = 2400; surface = 400; return WorldKnown; }
        }
        private static void Selection()
        {
            var s = new Source(); var rare = new RareCreatureDirection(s); var merchant = new TravellingMerchantDirection(s);
            for (int i = 0; i < 100; i++) { rare.Update(false, 0, 0); merchant.Update(false); }
            Require(s.Reads == 0, "closed/ability-absent directions must not scan NPCs");
            s.Npcs[2] = Npc(2, 46, 0, 10); s.Npcs[7] = Npc(7, 443, 3, 200); s.Npcs[8] = Npc(8, 45, 4, 1000);
            rare.Update(true, 0, 0); Require(rare.Visible && rare.Target.Slot == 8, "runtime rarity outranks nearest; common animal excluded");
            s.Npcs[8].X = 1300; rare.Update(true, 0, 0); Require(rare.Target.Slot == 7, "strict 1300 boundary invalidates next observation");
            var prior = s.Npcs[7]; s.Npcs[7] = Npc(7, 443, 3, 230); rare.Update(true, 0, 0);
            Require(!rare.Target.SameIdentity(prior) && rare.Target.SameIdentity(s.Npcs[7]), "same-slot/type replacement must not retain old identity");
            s.Npcs[7].Hidden = true; rare.Update(true, 0, 0); Require(!rare.Visible, "hide exits without discovery delay");
            s.Npcs[7].Hidden = false; s.Npcs[8] = Npc(8, 105, 1, 50); for (int i = 0; i < 15; i++) rare.Update(true, 0, 0);
            Require(rare.Target.Slot == 7, "rescue entities remain eligible but obey rarity");
            s.Npcs[9] = Npc(9, 473, 5, 1100); for (int i = 0; i < 15; i++) rare.Update(true, 0, 0);
            Require(rare.Target.Slot == 9, "new higher priority enters within bounded discovery");
            s.Npcs[1] = Npc(1, 473, 5, 1100); for (int i = 0; i < 15; i++) rare.Update(true, 0, 0);
            Require(rare.Target.Slot == 9, "complete tie retains eligible current object");
            s.Reads = 0; for (int i = 0; i < 600; i++) rare.Update(true, 0, 0);
            Require(s.Reads <= 8600 && rare.Target.Slot == 9, "stable target uses bounded discovery plus O(1) current position");
            rare.Update(true, 10000, 10000); Require(!rare.Visible, "teleport clears stale circle target");
            s.Npcs[3] = Npc(3, 368, 0, 1000); s.Npcs[4] = Npc(4, 368, 0, 10); merchant.Update(true);
            Require(merchant.Target.Slot == 3, "merchant keeps stable index, not nearest");
            s.Npcs[3].Hidden = true; merchant.Update(true); Require(merchant.Target.Slot == 4, "merchant immediately invalidates hidden target");
            Console.WriteLine("Guidance directions: closed reads=0; 600 stable rare updates <=8600 slot reads at N=200 (includes 40 discoveries).");
        }
        private static void Projection()
        {
            for (int i = 0; i < 720; i++)
            {
                double angle = i * Math.PI / 360;
                var p = DirectionProjection.Circle(413, 279, 413 + 900 * Math.Cos(angle), 279 + 900 * Math.Sin(angle), 46);
                double x = p.X - 413, y = p.Y - 279;
                Require(p.Visible && Math.Abs(Math.Sqrt(x * x + y * y) - 46) < 1e-8, "final circle must have equal radius, not independently clamped axes");
                Require(Math.Abs(Math.Cos(p.Angle) - Math.Cos(angle)) < 1e-8 && Math.Abs(Math.Sin(p.Angle) - Math.Sin(angle)) < 1e-8, "continuous direction, including old sector edges and pi wrap");
            }
            Require(!DirectionProjection.Circle(0, 0, 0, 0, 46).Visible, "coincident target has no invented direction");
            Require(DirectionProjection.Circle(0, 0, 8, 0, 46).X == 4, "near target cannot be overshot");
            var near = DirectionProjection.Circle(0, 0, 8, 0, 46);
            Require(near.X + 10 * near.Scale <= 8, "entire 20px arrow tip, not only its center, stays before near target");
            Require(DirectionProjection.Tiles(0, 0, 8, 0) == 1, "half tile rounds away from zero");
        }
        private static void Equipment()
        {
            var w = new EquipmentWarning(); var bosses = new[] { 4, 4, 50 }; var items = new[] { 407, 5010 };
            w.Update(true, true, 0, bosses, 0, items, 2, 0); Require(w.Alpha == 0, "blood moon alone/no listed danger is quiet");
            w.Update(true, true, 0, bosses, 3, items, 2, 1); Require(w.Alpha == 1, "first danger warns");
            w.Update(true, true, 0, new[] { 50, 4 }, 2, new[] { 5010, 407 }, 2, 4); Require(w.Alpha == 1, "3 full seconds and set order/segments do not retrigger");
            w.Update(true, true, 0, bosses, 3, items, 2, 4.125); Require(Math.Abs(w.Alpha - .5) < 1e-6, "quarter-second fade uses controlled monotonic time");
            w.Update(true, true, 0, bosses, 3, items, 2, 4.25); Require(w.Alpha == 0, "3.25 total seconds");
            w.Update(true, true, 0, bosses, 3, items, 2, 50); Require(w.Alpha == 0, "unchanged risk never periodically resends");
            w.Update(true, true, 0, bosses, 3, new[] { 407 }, 1, 51); Require(w.Alpha == 1, "changed issue set while same danger must be observed");
            w.Update(true, true, 0, bosses, 3, new[] { 1 }, 1, 51.01); Require(w.Alpha == 0, "fully corrected immediately withdrawn");
            w.Update(true, true, 1, bosses, 0, items, 2, 52); Require(w.Alpha == 1, "listed event can warn without boss");
            w.Update(false, true, 1, bosses, 0, items, 2, 52.01); Require(w.Alpha == 0, "unknown cannot retain stale warning");
            w.Update(true, false, 1, bosses, 0, items, 2, 53); Require(w.Alpha == 0, "death withdraws");
            var segments = new EquipmentWarning(); segments.Update(true, true, 0, new[] { 4 }, 1, items, 2, 0);
            segments.Update(true, true, 0, new[] { 4, 4 }, 2, items, 2, 5);
            Require(segments.Alpha == 0, "same-type extra boss must not retrigger when scratch buffer grows");
            foreach (int type in new[] { 15, 407, 5452, 5146, 2799, 3989, 5113, 88, 410, 411, 576, 5014, 5040, 6146 }) Require(EquipmentRules.IsNonCombat(type), "independently mapped representative ID missing: " + type);
            foreach (int type in new[] { 0, 1, 486, 575, 577, 4242, 4255, 6147 }) Require(!EquipmentRules.IsNonCombat(type), "unmapped lookalike included: " + type);
            int count = 0; for (int i = 0; i < 7000; i++) if (EquipmentRules.IsNonCombat(i)) count++;
            Require(count == 176, "fixed .8 union is 176 identities, not 153 aliases");
        }
        private static void Locations()
        {
            var source = new Source(); var location = new MerchantLocation(); var merchant = Npc(1, 368, 0, 3000 * 16, 350 * 16);
            location.Update(merchant, source, source); Require(location.Text == "地表", "inland surface cannot be called forest");
            source.Pylons = new[] { new GuidancePylon { Type = 6, X = 3000, Y = 350 } };
            for (int i = 0; i < 15; i++) location.Update(merchant, source, source);
            Require(location.Text == "雪地晶塔附近", "late pylon metadata admitted");
            int reads = source.Reads; for (int i = 0; i < 100; i++) location.Update(merchant, source, source);
            Require(source.Reads == reads, "matched pylon skips resident traversal");
            source.Pylons = new GuidancePylon[0]; source.Npcs[2] = Npc(2, 17, 0, merchant.X, merchant.Y); source.Npcs[3] = Npc(3, 18, 0, merchant.X, merchant.Y);
            for (int i = 2; i <= 3; i++) { source.Npcs[i].Town = true; source.Npcs[i].HomeX = 3000; source.Npcs[i].HomeY = 350; }
            for (int i = 0; i < 15; i++) location.Update(merchant, source, source);
            Require(location.Text == "居民区附近", "removed pylon falls back to actual resident evidence");
            merchant.X = 4000 * 16; for (int i = 0; i < 15; i++) location.Update(merchant, source, source); Require(location.Text == "地表", "moving away invalidates location within bound");
            source.WorldKnown = false; for (int i = 0; i < 15; i++) location.Update(merchant, source, source); Require(location.Text == "位置未知", "missing evidence is unknown");
        }
        private sealed class Port : IMerchantTestPort
        {
            internal int Calls; internal bool Throw;
            public MerchantTestReceipt Execute(MerchantTestRequest request) { Calls++; if (Throw) throw new InvalidOperationException(); return new MerchantTestReceipt(GameOperationOutcome.Unconfirmed, "attempted"); }
        }
        private static void Operations()
        {
            var port = new Port(); var feature = new MerchantTestFeature(port);
            for (int i = 0; i < 100; i++) feature.Update(1, true); Require(port.Calls == 0, "no click means no operation");
            Require(feature.Request(1) && !feature.Request(1), "only one pending explicit intent");
            feature.Update(2, true); Require(port.Calls == 0 && feature.Result.Outcome == GameOperationOutcome.Cancelled, "late cross-session request cancelled");
            feature.Request(2); feature.Update(2, false); Require(port.Calls == 0, "lost input cancels");
            feature.Request(2); feature.Update(2, true); for (int i = 0; i < 100; i++) feature.Update(2, true);
            Require(port.Calls == 1 && feature.Result.Outcome == GameOperationOutcome.Unconfirmed, "one call and no unconfirmed retry/fake success");
            port.Throw = true; feature.Request(2); feature.Update(2, true); feature.Update(2, true);
            Require(port.Calls == 2 && feature.Result.Outcome == GameOperationOutcome.Failed, "exception is terminal, no retry");
        }
        private static void Preferences()
        {
            Require(GuidancePreferences.Default.Mask == 0, "all displays default closed");
            var codec = new GuidancePreferenceCodec(); var value = new GuidancePreferences(7);
            Require(codec.Decode(codec.Encode(value)).Equals(value), "three independent switches roundtrip");
        }
    }
}
