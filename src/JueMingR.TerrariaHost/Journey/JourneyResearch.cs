using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Journey
{
    internal sealed class JourneyResearch
    {
        private readonly Dictionary<int, int> caps = new Dictionary<int, int>();
        private readonly int[] items;
        private static readonly FieldInfo MenuState = AccessTools.Field(typeof(CreativeUI), "_uiState");
        private static readonly FieldInfo CheckedVersion = AccessTools.Field(typeof(UICreativeInfiniteItemsDisplay), "_lastCheckedVersionForEdits");
        private static readonly FieldInfo Initialized = AccessTools.Field(typeof(CreativeUI), "_initialized");

        internal JourneyResearch()
        {
            // The native positive-cap catalog and its canonical research IDs
            // define eligibility. Never invent research for unused/internal IDs.
            foreach (var entry in CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId)
            {
                int id = Canonical(entry.Key);
                if (entry.Key > 0 && entry.Key < ItemID.Count && id > 0 && id < ItemID.Count &&
                    entry.Value > 0 && CreativeItemSacrificesCatalog.Instance.TryGetSacrificeCountCapToUnlockInfiniteItems(id, out int cap) && cap > 0)
                    caps[id] = cap;
            }
            if (caps.Count == 0) throw new InvalidOperationException("Native research catalog is not initialized.");
            items = new int[caps.Count]; caps.Keys.CopyTo(items, 0); Array.Sort(items);
        }

        internal bool TryCap(int id, out int cap) { return caps.TryGetValue(Canonical(id), out cap); }
        internal void Enumerate(Action<int> action) { foreach (int id in items) action(id); }
        private static int Canonical(int id)
        { return ContentSamples.CreativeResearchItemPersistentIdOverride.TryGetValue(id, out int actual) ? actual : id; }

        internal static void InvalidateMenu()
        {
            // One edge invalidation, not a forged tracker edit or per-Draw scan.
            var root = MenuState.GetValue(Main.CreativeMenu) as UIElement;
            if (root != null) Invalidate(root);
        }
        internal static void EnsureMenuReady()
        { if (!(bool)Initialized.GetValue(Main.CreativeMenu)) Main.CreativeMenu.Initialize(); }
        private static void Invalidate(UIElement element)
        {
            if (element is UICreativeInfiniteItemsDisplay) CheckedVersion.SetValue(element, -1);
            foreach (UIElement child in element.Children) Invalidate(child);
        }
    }
}
