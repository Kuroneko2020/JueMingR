using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Items;
using JueMingR.TerrariaHost.Items;

namespace Terraria
{
    internal static class ItemSelectionChecks
    {
        internal static void Run(HostItems host)
        {
            var original = host.Preferences.Value; var inventory = Main.LocalPlayer.inventory;
            var saved = inventory.ToArray();
            try
            {
                for (int i = 0; i < inventory.Length; i++) inventory[i] = new Item();
                inventory[0] = new Item { type = 102, stack = 1, favorited = true };
                inventory[1] = new Item { type = 8, stack = 2 };
                inventory[2] = new Item { type = 102, stack = 9 };
                inventory[3] = new Item { type = 71, stack = 1 };
                inventory[50] = new Item { type = 103, stack = 1 };
                inventory[54] = new Item { type = 101, stack = 1 };
                inventory[58] = new Item { type = 104, stack = 1 };
                host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 101 }).WithTypes(ItemListKind.Discard, new[] { 8 }));
                var s = new ItemSelection(host); Check(s.Open(ItemListKind.Sell, 0), "open add");
                Check(s.Candidates.SequenceEqual(new[] { 102, 8 }), "first legal slot order, dedupe, favorite, own-list exclusion, cross-list allowed");
                s.Select(102); s.Select(8); s.Select(8); Check(s.Count == 1, "multi-select toggles");
                int generation = s.Generation;
                Check(!s.Open(ItemListKind.Sell, 0) && s.Generation == generation && s.Count == 1, "same add keeps snapshot and draft");
                inventory[0] = new Item { type = 200, stack = 1 }; inventory[2].TurnToAir();
                Check(s.Candidates.SequenceEqual(new[] { 102, 8 }), "disappearance and new inventory type do not change open candidates");
                host.Change(host.Preferences.Value.WithEnabled(ItemActionKind.Sell, true).WithTypes(ItemListKind.Sell, new[] { 101, 201 }));
                s.Confirm(); Check(!s.Active && host.Preferences.Value.SellTypes.SequenceEqual(new[] { 101, 102, 201 }) && host.Preferences.Value.SellEnabled, "merge latest list and switches, disappeared selected type remains valid");
                s.Open(ItemListKind.Sell, 0); Check(s.Candidates.SequenceEqual(new[] { 200, 8 }), "reopen captures new inventory");
                s.Select(200); s.Open(ItemListKind.Discard, 0); Check(s.Count == 0 && s.List == ItemListKind.Discard, "switch discards hidden draft");
                s.Open(ItemListKind.Sell, 101); Check(s.Target == 101 && s.Count == 0, "direct replacement mode");
                host.Change(host.Preferences.Value.WithTypes(ItemListKind.Sell, new[] { 102, 201 }));
                s.Select(200); Check(!s.Active && s.Message != null && host.Preferences.Value.SellTypes.SequenceEqual(new[] { 102, 201 }), "missing stable target cannot replace neighbor");
                s.Open(ItemListKind.Sell, 102); s.Select(200);
                Check(!s.Active && host.Preferences.Value.SellTypes.SequenceEqual(new[] { 200, 201 }), "replacement commits stable target immediately");
                s.Open(ItemListKind.Sell, 0); s.Select(8);
                var stopping = typeof(HostItems).GetField("stopping", BindingFlags.Instance | BindingFlags.NonPublic);
                stopping.SetValue(host, true);
                try { s.Confirm(); Check(s.Active && s.Count == 1 && s.Message != null && !host.Preferences.Value.SellTypes.Contains(8), "refused command retains draft and explains failure"); }
                finally { stopping.SetValue(host, false); }
                s.Cancel();
                for (int i = 0; i < inventory.Length; i++) inventory[i] = new Item();
                s.Open(ItemListKind.Sell, 0); Check(s.Candidates.Count == 0 && !s.HasInventoryTypes, "no valid inventory empty reason"); s.Cancel();
                inventory[0] = new Item { type = 200, stack = 1 };
                s.Open(ItemListKind.Sell, 0); Check(s.Candidates.Count == 0 && s.HasInventoryTypes, "all already configured empty reason");
                host.Runtime.InvalidateSession(); Check(!s.ValidateSession() && !s.Active, "session end cancels even before next generation");
            }
            finally { Array.Copy(saved, inventory, saved.Length); host.Change(original); host.Runtime.Update(host.Tick + 1); }
            Console.WriteLine("PASS: stable type snapshot, latest preferences merge, one draft, direct replacement, refusal and session cancellation.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("ITEM SELECTION CHECK FAILED: " + message); }
    }
}
