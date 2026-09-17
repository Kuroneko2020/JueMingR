using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JueMingR.Platform.ItemCatalog;

namespace JueMingR.Features.ItemBrowser
{
    public sealed class BrowserCatalog
    {
        private readonly CatalogItem[] byType, byName;
        private readonly Dictionary<int, CatalogItem> items;
        public int Count { get { return byType.Length; } }
        public BrowserCatalog(CatalogItem[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            byType = values.OrderBy(v => v.Type).ToArray();
            items = byType.ToDictionary(v => v.Type);
            byName = byType.OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(v => v.Type).ToArray();
        }
        public CatalogItem Find(int type) { CatalogItem value; return items.TryGetValue(type, out value) ? value : null; }
        // -1 = malformed numeric request; 0 = name/all request; positive = exact ID.
        public static int ClassifyQuery(string query)
        {
            string value = (query ?? "").Trim();
            bool hash = value.StartsWith("#", StringComparison.Ordinal);
            if (hash) value = value.Substring(1);
            bool digits = value.Length != 0;
            for (int i = 0; i < value.Length; i++) digits &= value[i] >= '0' && value[i] <= '9';
            if (!hash && !digits) return 0;
            int id;
            return digits && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 ? id : -1;
        }
        public int[] Search(string query, int category, bool sortByName)
        {
            string text = (query ?? "").Trim(); int id = ClassifyQuery(text);
            if (id < 0) return new int[0];
            if (id > 0)
            {
                CatalogItem item = Find(id);
                return item != null && (category == 0 || (item.Categories & category) != 0) ? new[] { id } : new int[0];
            }
            var result = new List<int>();
            foreach (CatalogItem item in sortByName ? byName : byType)
                if ((category == 0 || (item.Categories & category) != 0) && (text.Length == 0 ||
                    item.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    item.InternalName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)) result.Add(item.Type);
            return result.ToArray();
        }
    }
}
