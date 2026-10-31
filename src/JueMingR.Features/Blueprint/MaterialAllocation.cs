using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Features.Blueprint
{
    // One displayed scope, one frozen inventory and one supply ledger. This is
    // a nominal bill explanation, never a reservation or a native-use choice.
    public sealed class BlueprintMaterialAllocation
    {
        private sealed class Edge
        {
            internal int To, Reverse, Cost;
            internal long Capacity, Initial;
        }
        private readonly IReadOnlyDictionary<int, BlueprintMaterialRow> rows;
        public BlueprintMaterialAllocation(IReadOnlyDictionary<int, long> required, BlueprintMaterialInventory inventory,
            BlueprintPlacementCatalog catalog, BlueprintSubstitutionPolicy policy, bool inventoryReady = true)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            var keys = new List<int>(required.Keys); keys.Sort();
            var stock = new SortedDictionary<int, long>();
            if (inventory != null) foreach (var slot in inventory.Slots)
            {
                if (!slot.Eligible || slot.ItemType <= 0 || slot.Stack <= 0) continue;
                long old; stock.TryGetValue(slot.ItemType, out old); stock[slot.ItemType] = checked(old + slot.Stack);
            }
            bool ready = inventoryReady && inventory?.Ready == true;
            var result = new Dictionary<int, BlueprintMaterialRow>();
            foreach (int key in keys)
            {
                if (key <= 0 || required[key] < 0) throw new ArgumentOutOfRangeException(nameof(required));
                long held; stock.TryGetValue(key, out held);
                result.Add(key, new BlueprintMaterialRow(key, required[key], held, ready));
            }
            if (ready && policy.Enabled && catalog?.Substitution.Ready == true && keys.Count > 0 && stock.Count > 0)
                Allocate(keys, required, stock, catalog.Substitution, policy, result);
            rows = new ReadOnlyDictionary<int, BlueprintMaterialRow>(result);
        }
        public IReadOnlyDictionary<int, BlueprintMaterialRow> Rows { get { return rows; } }
        public BlueprintMaterialRow Row(int itemType) { BlueprintMaterialRow row; return rows.TryGetValue(itemType, out row) ? row : null; }
        private static Edge Add(List<Edge>[] graph, int from, int to, long capacity, int cost)
        {
            var edge = new Edge { To = to, Reverse = graph[to].Count, Capacity = capacity, Initial = capacity, Cost = cost };
            var reverse = new Edge { To = from, Reverse = graph[from].Count, Cost = -cost };
            graph[from].Add(edge); graph[to].Add(reverse); return edge;
        }
        private static void Allocate(List<int> keys, IReadOnlyDictionary<int, long> required, SortedDictionary<int, long> stock,
            BlueprintSubstitutionCatalog catalog, BlueprintSubstitutionPolicy policy, Dictionary<int, BlueprintMaterialRow> rows)
        {
            var supply = new List<int>(stock.Keys);
            int supplyStart = 1, demandStart = supplyStart + supply.Count, sink = demandStart + keys.Count;
            var graph = new List<Edge>[sink + 1]; for (int i = 0; i < graph.Length; i++) graph[i] = new List<Edge>();
            var allocations = new List<Edge>[keys.Count];
            for (int i = 0; i < supply.Count; i++) Add(graph, 0, supplyStart + i, stock[supply[i]], 0);
            for (int row = 0; row < keys.Count; row++)
            {
                int original = keys[row]; allocations[row] = new List<Edge>();
                Add(graph, demandStart + row, sink, required[original], 0);
                var candidates = catalog.MaterialCandidates(original, policy);
                for (int item = 0; item < supply.Count; item++)
                {
                    bool exact = original == supply[item], accepted = exact;
                    if (!accepted) for (int c = 0; c < candidates.Count; c++) if (candidates[c] == supply[item]) { accepted = true; break; }
                    if (accepted) allocations[row].Add(Add(graph, supplyStart + item, demandStart + row,
                        Math.Min(stock[supply[item]], required[original]), exact ? 0 : 1));
                }
            }
            // Minimum-cost augmenting paths first use exact edges (cost zero).
            // Reverse edges can relocate an earlier assignment when two pools
            // overlap, avoiding a greedy false shortage without copying stock.
            // Capacities are quantities, so a large stack is never looped per
            // unit. This finite graph is rebuilt only for a changed bill/input.
            var distance = new int[graph.Length]; var previous = new int[graph.Length]; var previousEdge = new int[graph.Length];
            var queued = new bool[graph.Length]; var queue = new Queue<int>();
            while (true)
            {
                for (int i = 0; i < graph.Length; i++) { distance[i] = int.MaxValue; previous[i] = -1; queued[i] = false; }
                distance[0] = 0; queue.Enqueue(0); queued[0] = true;
                while (queue.Count > 0)
                {
                    int node = queue.Dequeue(); queued[node] = false;
                    for (int i = 0; i < graph[node].Count; i++)
                    {
                        var edge = graph[node][i];
                        if (edge.Capacity <= 0 || distance[edge.To] <= distance[node] + edge.Cost) continue;
                        distance[edge.To] = distance[node] + edge.Cost; previous[edge.To] = node; previousEdge[edge.To] = i;
                        if (!queued[edge.To]) { queue.Enqueue(edge.To); queued[edge.To] = true; }
                    }
                }
                if (previous[sink] < 0) break;
                long amount = long.MaxValue;
                for (int node = sink; node != 0; node = previous[node]) amount = Math.Min(amount, graph[previous[node]][previousEdge[node]].Capacity);
                for (int node = sink; node != 0; node = previous[node])
                {
                    var edge = graph[previous[node]][previousEdge[node]];
                    edge.Capacity -= amount; graph[node][edge.Reverse].Capacity = checked(graph[node][edge.Reverse].Capacity + amount);
                }
            }
            for (int row = 0; row < keys.Count; row++)
            {
                long exact = 0, substitution = 0;
                foreach (var edge in allocations[row])
                    if (edge.Cost == 0) exact = checked(exact + edge.Initial - edge.Capacity);
                    else substitution = checked(substitution + edge.Initial - edge.Capacity);
                int key = keys[row]; long held; stock.TryGetValue(key, out held);
                rows[key] = new BlueprintMaterialRow(key, required[key], held, true, exact, substitution);
            }
        }
    }
}
