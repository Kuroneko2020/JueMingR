using System;
using System.Collections.Generic;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class BrowserHistoryChecks
    {
        internal static void Check(List<string> failures)
        {
            Type type = Assembly.Load("JueMingR.Features").GetType("JueMingR.Features.ItemBrowser.BrowserWorkspace");
            if (type == null) { failures.Add("G04 browser navigation history missing"); return; }
            object workspace = Activator.CreateInstance(type);
            Action<int, bool> navigate = (id, uses) => type.GetMethod("Navigate").Invoke(workspace, new object[] { id, uses });
            Func<string, object> get = name => type.GetProperty(name).GetValue(workspace);
            Action<string, object> set = (name, value) => type.GetProperty(name).SetValue(workspace, value);
            set("Query", "木"); set("Category", 16); set("CatalogOffset", 23); navigate(9, false);
            set("RelationOffset", 7); set("DetailScroll", 12); set("Group", 2); set("GroupPage", 3); navigate(172, true);
            type.GetMethod("Back").Invoke(workspace, null);
            if ((int)get("Selected") != 9 || (int)get("RelationOffset") != 7 || (string)get("Query") != "木" || (int)get("CatalogOffset") != 23 || (int)get("DetailScroll") != 12 || (int)get("Group") != 2 || (int)get("GroupPage") != 3)
                failures.Add("G04 back must restore the complete prior browse context");
            type.GetMethod("Forward").Invoke(workspace, null);
            if ((int)get("Selected") != 172 || !(bool)get("Uses")) failures.Add("G04 forward must restore direction");
            type.GetMethod("Back").Invoke(workspace, null); navigate(10, false);
            if ((bool)get("CanForward")) failures.Add("G04 new navigation must discard the abandoned forward branch");
            for (int i = 1; i < 300; i++) navigate(i, false);
            int steps = 0; while ((bool)get("CanBack")) { type.GetMethod("Back").Invoke(workspace, null); steps++; }
            if (steps != 63 || (int)get("Selected") != 236) failures.Add("G04 navigation retains exactly the newest 64 positions");
            navigate(236, false); if ((bool)get("CanBack")) failures.Add("G04 same target/direction must not add a duplicate navigation");
            set("Detail", false); navigate(236, false); if (!(bool)get("Detail")) failures.Add("G04 same target reopens the detail view without new history");
        }
    }
}
