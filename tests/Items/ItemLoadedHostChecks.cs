using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Terraria
{
    // This process loads the separately compiled production Host through the
    // real bootstrap. Linked adapter tests alone cannot prove its ABI/composition.
    internal static class ItemLoadedHostChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        internal static void CheckUncommittedUpdate(Main main, string evidencePath)
        {
            Type worker = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost")
                .GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo committed = worker.GetField("hookCommitted", flags);
            byte[] evidence = System.IO.File.ReadAllBytes(evidencePath);
            // Deterministically expose the real callback while the patch set is
            // not committed. No sleeps or production test hooks are necessary.
            committed.SetValue(null, 0);
            try { main.RunUpdateLoop(1); }
            finally { committed.SetValue(null, 1); }
            Check(System.IO.File.ReadAllBytes(evidencePath).SequenceEqual(evidence) &&
                (int)worker.GetField("postfixGate", flags).GetValue(null) == 0 &&
                (int)worker.GetField("handoffGate", flags).GetValue(null) == 0 &&
                (int)worker.GetField("biomeFeatureFailed", flags).GetValue(null) == 0,
                "uncommitted callback must not record errors, consume handoff or fail a feature");
        }
        internal static void FailLayerBeforeHandoff(Main main, string evidencePath)
        {
            Type worker = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost")
                .GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            byte[] startupPrefix = System.IO.File.ReadAllBytes(evidencePath);
            worker.GetMethod("DrawSetupPostfix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { null });
            Check(System.IO.File.ReadAllBytes(evidencePath).SequenceEqual(startupPrefix),
                "local layer failure before handoff must not interrupt the startup evidence prefix");
            // A real, non-null layer list can lack only the optional biome
            // anchor. Exercise the handoff catch-up insertion as well as setup.
            var layers = (System.Collections.Generic.List<UI.GameInterfaceLayer>)
                typeof(Main).GetMethod("CreateFixtureLayers", Flags).Invoke(main, null);
            Check(layers.RemoveAll(layer => layer.Name == "Vanilla: Map / Minimap") == 1 && layers.Count > 0,
                "fixture isolates the missing biome anchor in an otherwise populated layer list");
            typeof(Main).GetField("_gameInterfaceLayers", Flags).SetValue(main, layers);
        }
        internal static void Run(Main main, bool earlyLayerFailure = false)
        {
            // This executable has no native game window. Reuse the input
            // fixture's OS boundary; the loaded production input logic runs.
            HostInputChecks.ConfigureLoadedHost(); FocusHelper.IsSelectedApplication = true;
            Main.SampleLeft = Main.SampleRight = Main.SampleF5 = false; Main.SampleWheel = 0;
            main.RunUpdateLoop(2);
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "JueMingR.TerrariaHost");
            object context = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true)
                .GetField("postfixContext", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            object items = Get(context, "items");
            Check(items != null && (bool)Get(items, "Available"), "production item hooks: " + (items == null ? "missing" : Get(items, "SetupError")));
            for (int i = 0; i < 200 && !(bool)Get(Get(items, "Preferences"), "IsLoaded"); i++) Thread.Sleep(5);
            main.RunUpdateLoop(1);
            Check(!(bool)Get(Get(items, "Feature"), "HasFailed"), "production feature did not fail during observation");
            Check(!(bool)Get(Get(context, "Shell"), "Failed"), "production F5 input remains healthy with item profile");
            object settings = Get(Get(items, "Preferences"), "Value");
            Check(!(bool)Get(settings, "StackEnabled") && !(bool)Get(settings, "SellEnabled") && !(bool)Get(settings, "DiscardEnabled"), "new profile defaults are safe");
            Check((bool)Get(settings, "DiscardFeedbackEnabled"), "loaded production preferences default discard feedback on");
            PopupText.Reset(); Item.AffixReads = 0;
            Type action = settings.GetType().Assembly.GetType("JueMingR.Features.Items.ItemActionKind", true);
            Type list = settings.GetType().Assembly.GetType("JueMingR.Features.Items.ItemListKind", true);
            object discard = settings.GetType().GetMethod("WithTypes").Invoke(settings, new[] { Enum.ToObject(list, 1), new[] { 100 } });
            discard = settings.GetType().GetMethod("WithEnabled").Invoke(discard, new[] { Enum.ToObject(action, 2), (object)true });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { discard });
            Main.LocalPlayer.inventory[10] = new Item { type = 100, stack = 2 }; main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].stack == 2, "loaded discard list does not invent source for old inventory");
            HostInputChecks.Foreground = false; main.RunUpdateLoop(1);
            Main.LocalPlayer.Pickup(new WorldItem { inner = new Item { type = 100, stack = 1 } }); main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].stack == 3, "loaded input gate blocks new actions but keeps genuine source capture");
            HostInputChecks.Foreground = true; Main.SampleLeft = true; main.RunUpdateLoop(1);
            Main.SampleLeft = false; main.RunUpdateLoop(1);
            Check(Main.LocalPlayer.inventory[10].stack == 3, "loaded activation press and release cannot start an item action");
            main.RunUpdateLoop(7);
            if (!Main.LocalPlayer.inventory[10].IsAir)
            {
                object world = Get(items, "World"); var observed = new object[] { null };
                world.GetType().GetMethod("TryObserve", Flags).Invoke(world, observed);
                Console.WriteLine("Loaded item diagnostic: failed={0}, enabled={1}, capability={2}, result={3}", Get(Get(items, "Feature"), "HasFailed"), Get(Get(items, "Feature"), "Enabled"), Get(items, "CapabilityError"), Get(Get(items, "Ownership"), "DiscardResult"));
            }
            Check(Main.LocalPlayer.inventory[10].IsAir && Main.LocalPlayer.trashItem.type == 100 && Main.LocalPlayer.trashItem.stack == 3,
                "production configuration/selection/native trash/result chain");
            Check(PopupText.Calls == 1 && PopupText.Last.Text == "自动丢弃了3个fixture-item-100" && Item.AffixReads == 1,
                "loaded production discard adapter resolves the independent native popup/name ABI once");
            discard = settings.GetType().GetMethod("WithDiscardFeedbackEnabled").Invoke(discard, new[] { (object)false });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { discard });
            // Keep this feedback case off the currently selected slot 0, which
            // is deliberately protected by the production inventory boundary.
            Main.LocalPlayer.inventory[10] = new Item { type = 100, stack = 1 };
            Main.LocalPlayer.Pickup(new WorldItem { inner = new Item { type = 100, stack = 2 } }); main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].IsAir && Main.LocalPlayer.trashItem.stack == 3 && PopupText.Calls == 1 && Item.AffixReads == 1,
                "loaded Host preference wiring disables only feedback while the next real pickup is still discarded");
            object sale = settings.GetType().GetMethod("WithTypes").Invoke(settings, new[] { Enum.ToObject(list, 0), new[] { 100 } });
            sale = settings.GetType().GetMethod("WithEnabled").Invoke(sale, new[] { Enum.ToObject(action, 1), (object)true });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { sale });
            Main.playerInventory = true; Main.npcShop = 1;
            Main.LocalPlayer.inventory[10] = new Item { type = 100, stack = 2 }; main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].stack == 2, "loaded sale list/shop does not invent source for old inventory");
            Main.LocalPlayer.Pickup(new WorldItem { inner = new Item { type = 100, stack = 1 } }); main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].IsAir && Main.LocalPlayer.inventory[50].stack == 15 && Main.instance.shop[1].item[0].buyOnce,
                "production configuration/native sale verifies source, coin value and buyback");
            object stack = settings.GetType().GetMethod("WithEnabled").Invoke(settings, new[] { Enum.ToObject(action, 0), (object)true });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { stack });
            Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 20 }; main.RunUpdateLoop(1);
            var chest = new Chest(); chest.item[0] = new Item { type = 8, stack = 1 };
            GameContent.NearbyChests.Targets.Add(new GameContent.PositionedChest { chest = chest });
            Main.LocalPlayer.Pickup(new WorldItem { inner = new Item { type = 8, stack = 3 } }); main.RunUpdateLoop(7);
            if (!Main.LocalPlayer.inventory[10].IsAir)
            {
                Console.WriteLine("Loaded storage diagnostic: source={0}, target={1}, failed={2}, result={3}", Main.LocalPlayer.inventory[10].stack, chest.item[0].stack,
                    Get(Get(items, "Feature"), "HasFailed"), Get(Get(items, "Ownership"), "StoreResult") == null ? "none" : Get(Get(Get(items, "Ownership"), "StoreResult"), "Reason"));
            }
            Check(Main.LocalPlayer.inventory[10].IsAir && chest.item[0].stack == 24,
                "production pickup hook/whole-stack/native selective storage chain");
            main.SetupAndDrawBiomeLayer();
            // Inject a display-only fault after the real shared composition has
            // already processed all three actions. It must not stop that owner.
            if (!earlyLayerFailure)
            {
                int draws = Main.FixtureDrawCount;
                Main.FixtureThrowOnDraw = true;
                try { main.DrawBiomeLayer(); }
                finally { Main.FixtureThrowOnDraw = false; }
                Check(Main.FixtureDrawCount == draws + 1, "controlled biome draw failure reached the production callback");
            }
            Check((bool)Get(Get(context, "Runtime"), "FeatureFailed"), "biome failure remains local and terminal");
            Check(!(bool)Get(Get(items, "Feature"), "HasFailed"), "biome display failure must not disable item automation");
            Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 2 };
            Main.LocalPlayer.Pickup(new WorldItem { inner = new Item { type = 8, stack = 1 } }); main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].IsAir && chest.item[0].stack == 27,
                "new reliable pickup still reaches native storage after a biome display failure");
            string[] evidence = System.IO.File.ReadAllLines((string)Get(context, "EvidencePath"));
            Check(evidence.Length == 6 && evidence[5].Contains("|ERROR|" + (earlyLayerFailure ? "BIOME_LAYER" : "BIOME_DRAW") + "|FEATURE_FAILED|InvalidOperationException"),
                "first runtime failure remains diagnosable after all five successful startup events");
            main.DrawBiomeLayer(); main.RunUpdateLoop(2);
            Check(System.IO.File.ReadAllLines((string)Get(context, "EvidencePath")).SequenceEqual(evidence),
                "failed display does not repeat error writes");
            Check(Get(context, "notes") != null && Get(context, "Shell") != null, "existing Notes and F5 remain composed");
            Console.WriteLine("PASS: separately compiled item Host loaded through bootstrap; all three config/native/result chains, shared session, defaults and existing composition.");
        }
        private static object Get(object value, string name)
        { FieldInfo field = value.GetType().GetField(name, Flags); return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, Flags).GetValue(value); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
