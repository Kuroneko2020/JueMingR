using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Security.Cryptography;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBrowserChecks
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void Run()
        {
            Require(IntPtr.Size == 4 && typeof(object).Assembly.GetName().Name == "mscorlib", "G04 requires .NET Framework x86");
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            if (assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeItemCatalog") == null)
                throw new InvalidOperationException("G04 native item/recipe read-only catalog adapter missing");
            Require(typeof(Main).Assembly.ManifestModule.ModuleVersionId == new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"), "G04 native MVID");
            using (var file = File.OpenRead(typeof(Main).Assembly.Location)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3", "G04 fixed native .8 source");
            LanguageManager.Instance.SetLanguage("en-US"); Lang.InitializeLegacyLocalization();
            Main.netMode = Main.myPlayer = 0; Main.dedServ = false; Main.rand = new UnifiedRandom(123);
            Main.player[0] = new Player { active = true };
            var profile = new Terraria.GameInput.PlayerInputProfile("G04 isolated defaults");
            profile.Initialize((Terraria.GameInput.PresetProfiles)0);
            typeof(Terraria.GameInput.PlayerInput).GetField("_currentProfile", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, profile);
            ContentSamples.Initialize();
            foreach (var p in ContentSamples.ProjectilesByType) { Main.projHook[p.Key] = p.Value.aiStyle == 7; Main.projHostile[p.Key] = p.Value.hostile; }
            Recipe.SetupRecipeGroups(); ItemID.Sets.PostSetupContent();
            for (int i = 0; i < Main.recipe.Length; i++) Main.recipe[i] = new Recipe();
            Recipe.SetupRecipes(); ContentSamples.FixItemsAfterRecipesAreAdded(); MapHelper.Initialize();
            NativeAnnouncementIconChecks.Run(assembly);
            Console.WriteLine("Native aliases: " + string.Join(",", ContentSamples.ItemsByType.Where(p => p.Key > 0 && p.Value.type > 0 && p.Key != p.Value.type).Select(p => p.Key + "->" + p.Value.type)));
            object subject = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeItemCatalog"), true);
            try
            {
                Main.rand = new UnifiedRandom(789); var reference = new UnifiedRandom(789);
                for (int i = 0; i < 100; i++) Call(subject, "Step", false);
                Require(subject.GetType().GetProperty("Catalog", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(subject) == null && (long)Get(subject, "ItemReads") == 0, "closed browser does no cold catalog work");
                int steps = 0;
                while (!(bool)Get(subject, "Ready") && steps++ < 400)
                {
                    long before = (long)Get(subject, "ItemReads"), recipes = (long)Get(subject, "RecipeReads");
                    Call(subject, "Step", true);
                    Require((long)Get(subject, "ItemReads") - before <= 64 && (long)Get(subject, "RecipeReads") - recipes <= 32, "native cold batches remain bounded");
                }
                Require((bool)Get(subject, "Ready"), "full native catalog finishes: " + Get(subject, "Status") + "; steps=" + steps + "; failure=" + GetOptional(subject, "CaptureFailure"));
                Require(Main.rand.Next() == reference.Next(), "native read-only capture never consumes shared RNG");
                object catalog = Get(subject, "Catalog");
                Require((int)Get(catalog, "Count") > 6000, "normal catalog covers all native positive types, not inventory only");
                var relations = (System.Collections.IEnumerable)Get(subject, "RecipeValues");
                object wooden = null, multi = null;
                foreach (object relation in relations)
                { if ((int)Get(relation, "Output") == 172) wooden = relation; if ((int)Get(relation, "Output") == 1102) multi = relation; }
                Require(wooden != null && multi != null && (int)Get(multi, "Minimum") == 4, "independent actual recipe output 1102 has quantity four");
                var ingredient = ((System.Collections.IEnumerable)Get(wooden, "Ingredients")).Cast<object>().First();
                Require((int)Get(ingredient, "Count") == 5 && ((System.Collections.IEnumerable)Get(ingredient, "Types")).Cast<int>().Contains(9) &&
                    ((System.Collections.IEnumerable)Get(ingredient, "Types")).Cast<int>().Count() > 1, "actual item172 uses any wood five, not phantom group one");
                long itemsRead = (long)Get(subject, "ItemReads"), recipesRead = (long)Get(subject, "RecipeReads");
                for (int i = 0; i < 120; i++) Call(subject, "Step", true);
                Require((long)Get(subject, "ItemReads") == itemsRead && (long)Get(subject, "RecipeReads") == recipesRead, "stable browser rereads no native item or recipe");
                Console.WriteLine("PASS: actual .8 directory and registered recipes; bounded capture; no RNG or stable rescans.");
                var sourceType = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeRelationSources", true);
                object sources = Activator.CreateInstance(sourceType, true);
                Main.ItemDropsDB = new Terraria.GameContent.ItemDropRules.ItemDropDatabase(); Main.ItemDropsDB.Populate();
                Main.rand = new UnifiedRandom(975); reference = new UnifiedRandom(975);
                for (int i = 0; i < 100; i++) Call(sources, "Step", false, subject);
                Require((long)Get(sources, "NpcReads") == 0 && (long)Get(sources, "ShimmerReads") == 0, "closed sources do no declaration reads");
                for (int i = 0; i < 1200; i++) Call(sources, "Step", true, subject);
                Require(GetOptional(sources, "Drops") != null && GetOptional(sources, "Shimmer") != null,
                    "native families complete independently: " + Get(sources, "DropStatus") + "; " + Get(sources, "ShimmerStatus"));
                Require((long)Get(sources, "GlobalReads") == 1 && (long)Get(sources, "NpcReads") == 762, "global declarations reported once, not per NPC");
                Require(Main.rand.Next() == reference.Next(), "drop reports and shimmer declarations consume no RNG");
                foreach (int excluded in new[] { 71, 72, 73, 74, 560, 4986 })
                    Require(!((System.Collections.IEnumerable)Call(Get(sources, "Shimmer"), "Find", excluded, true, -1)).Cast<object>().Any(), "non-item shimmer branch must not become decrafting: " + excluded);
                var lunar = ((System.Collections.IEnumerable)Call(Get(sources, "Shimmer"), "Find", 3461, true, -1)).Cast<object>().Select(r => (int)Get(r, "Output")).ToArray();
                Require(lunar.SequenceEqual(new[] { 5408, 5401, 5403, 5402, 5406, 5407, 5405, 5404 }), "actual moon index order");
                Console.WriteLine("PASS: actual .8 drop reports and shimmer declarations; globals once; no random operations.");
                CheckShops(assembly);
                NativeTargetGestureChecks.Run(assembly, subject);
                NativeBrowserUiChecks.Run(assembly, subject);
                NativeChestLocatorChecks.Run(assembly);
                NativeChestScanChecks.Run(assembly, subject);
                NativeBrowserCompositionChecks.Run(assembly);
            }
            finally { ((IDisposable)subject).Dispose(); }
        }
        private static void CheckShops(Assembly assembly)
        {
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { active = false };
            Main.BestiaryDB = new Terraria.GameContent.Bestiary.BestiaryDatabase();
            new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            Main.BestiaryTracker = new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
            Main.travelShop[0] = ItemID.CompanionCube; int[] travel = (int[])Main.travelShop.Clone();
            var inventory = Main.LocalPlayer.inventory.Select(i => Tuple.Create(i.type, i.stack, i.prefix)).ToArray();
            object shops = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeShopSnapshot", true), true);
            Main.rand = new UnifiedRandom(642); var reference = new UnifiedRandom(642);
            Call(shops, "Step", true); Require((long)Get(shops, "ShopReads") == 0, "no implicit shop capture");
            Call(shops, "Request"); Call(shops, "Step", false); Require((long)Get(shops, "ShopReads") == 0, "closed shop capture does no work");
            for (int page = 1; page <= 25; page++)
            {
                long before = (long)Get(shops, "ShopReads"); Call(shops, "Step", true);
                Require((long)Get(shops, "ShopReads") - before == 1, "actual native shop page failed: " + page);
            }
            Require(!(bool)Get(shops, "Busy") && (int)Get(shops, "failures") == 0, "all native shop pages succeeded");
            foreach (var pair in new[] { Tuple.Create(88, "商人"), Tuple.Create(4767, "动物学家"), Tuple.Create(5071, "公主"), Tuple.Create(2158, "画家（装饰）"), Tuple.Create(3628, "旅商") })
                Require(((System.Collections.IEnumerable)Call(Get(shops, "Index"), "Find", pair.Item1, false, 3)).Cast<object>().Any(r => (string)Get(r, "Title") == pair.Item2), "native shop anchor " + pair.Item1);
            for (int i = 0; i < 120; i++) Call(shops, "Step", true);
            Require((long)Get(shops, "ShopReads") == 25, "stable pages never rebuild shops");
            Require(Main.rand.Next() == reference.Next() && Main.travelShop.SequenceEqual(travel) && Main.LocalPlayer.inventory.Select(i => Tuple.Create(i.type, i.stack, i.prefix)).SequenceEqual(inventory), "snapshot does not reroll travel stock, consume RNG or mutate inventory");
            Console.WriteLine("PASS: actual 25 native shop pages, bounded explicit capture, independent item anchors, no stock/RNG/inventory mutations.");
        }
    }
}
