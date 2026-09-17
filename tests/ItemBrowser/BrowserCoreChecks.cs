using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class BrowserCoreChecks
    {
        // Missing implementation is an expected failure, not a compiler error.
        // Once present, these tests call the production search/navigation boundary.
        internal static void Check(List<string> failures)
        {
            Type catalog = Assembly.Load("JueMingR.Features").GetType("JueMingR.Features.ItemBrowser.BrowserCatalog");
            if (catalog == null) { failures.Add("G04: complete catalog search is not implemented"); return; }
            try
            {
                Type entry = Assembly.Load("JueMingR.Platform").GetType("JueMingR.Platform.ItemCatalog.CatalogItem");
                Array values = Array.CreateInstance(entry, 4);
                values.SetValue(Activator.CreateInstance(entry, 1, "铜短剑", "CopperShortsword", 1), 0);
                values.SetValue(Activator.CreateInstance(entry, 2, "木材", "Wood", 16), 1);
                values.SetValue(Activator.CreateInstance(entry, 3, "木剑", "WoodenSword", 1), 2);
                values.SetValue(Activator.CreateInstance(entry, 4, "工具", "Tool", 2), 3);
                object subject = Activator.CreateInstance(catalog, new object[] { values });
                Action<string, int, bool, int[]> search = (query, category, names, expected) =>
                {
                    var actual = (int[])catalog.GetMethod("Search").Invoke(subject, new object[] { query, category, names });
                    if (!actual.SequenceEqual(expected)) failures.Add("G04 search: " + query + " => " + string.Join(",", actual));
                };
                search("", 0, false, new[] { 1, 2, 3, 4 });
                search("木", 0, false, new[] { 2, 3 });
                search("wood", 1, false, new[] { 3 });
                search("#2", 0, false, new[] { 2 });
                search("0002", 0, false, new[] { 2 });
                search("#0", 0, false, new int[0]);
                search("999999999999", 0, false, new int[0]);
                search("#bad", 0, false, new int[0]);
                search("2", 1, false, new int[0]);
                search("", 16, false, new[] { 2 });
                search("CopperShortsword", 0, false, new[] { 1 });
                if ((int)catalog.GetMethod("ClassifyQuery").Invoke(null, new object[] { "#bad" }) != -1)
                    failures.Add("G04 invalid numeric query must be distinct from no matches");
            }
            catch (Exception e) { failures.Add("G04 catalog: " + (e.InnerException ?? e)); }
        }
    }
}
