using System.Collections.Generic;
using System;
using JueMingR.Platform.ItemCatalog;

namespace JueMingR.Features.ItemBrowser
{
    public sealed class RelationIndex
    {
        private readonly Dictionary<long, IReadOnlyList<ItemRelation>> index = new Dictionary<long, IReadOnlyList<ItemRelation>>();
        private static readonly IReadOnlyList<ItemRelation> Empty = Array.AsReadOnly(new ItemRelation[0]);
        public RelationIndex(IEnumerable<ItemRelation> relations)
        {
            var building = new Dictionary<long, List<ItemRelation>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemRelation relation in relations)
            {
                if (!seen.Add(relation.Id)) throw new ArgumentException("Duplicate relation identity");
                Add(building, relation.Output, false, relation);
                var inputs = new HashSet<int>();
                foreach (var ingredient in relation.Ingredients) foreach (int type in ingredient.Types) inputs.Add(type);
                if (relation.Station > 0) inputs.Add(relation.Station);
                foreach (int type in inputs) Add(building, type, true, relation);
            }
            foreach (var entry in building) index.Add(entry.Key, entry.Value.AsReadOnly());
        }
        private static long Key(int type, bool uses, int kind) { return ((long)type << 8) | (uses ? 128L : 0) | (uint)(kind + 1); }
        private static void Add(Dictionary<long, List<ItemRelation>> target, int type, bool uses, ItemRelation relation)
        {
            if (type <= 0) return;
            foreach (int kind in new[] { -1, (int)relation.Kind })
            { long key = Key(type, uses, kind); List<ItemRelation> list; if (!target.TryGetValue(key, out list)) target.Add(key, list = new List<ItemRelation>()); list.Add(relation); }
        }
        public IReadOnlyList<ItemRelation> Find(int type, bool uses, int kind = -1)
        { IReadOnlyList<ItemRelation> result; return index.TryGetValue(Key(type, uses, kind), out result) ? result : Empty; }
    }
}
