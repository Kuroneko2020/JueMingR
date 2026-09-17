using System.Collections.Generic;
using JueMingR.Platform.ItemCatalog;
using JueMingR.Features.ItemBrowser;

namespace JueMingR.ArchitectureTests
{
    internal static class RelationChecks
    {
        internal static void Check(List<string> failures)
        {
            var recipe = new ItemRelation("recipe:17", RelationKind.Recipe, 1102, 4, 4, "制作", "水、工作台", new[] {
                new RelationIngredient(new[] { 9, 619 }, 5, "任一木"), new RelationIngredient(new[] { 619 }, 2) });
            var index = new RelationIndex(new[] { recipe });
            var obtain = index.Find(1102, false); var uses = index.Find(619, true);
            if (obtain.Count != 1 || obtain[0].Minimum != 4 || obtain[0].Ingredients.Count != 2 || obtain[0].Ingredients[0].Count != 5)
                failures.Add("G04 recipe must preserve output count and substitute the ingredient identity without adding a phantom group");
            if (uses.Count != 1 || uses[0].Id != "recipe:17" || index.Find(9, true).Count != 1)
                failures.Add("G04 reverse lookup must expand every alternative and deduplicate per relation");
            if (index.Find(619, true, (int)RelationKind.Shop).Count != 0 || index.Find(1102, true).Count != 0)
                failures.Add("G04 directions and source family must stay distinct");
        }
    }
}
