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
        internal static void Run(Main main)
        {
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
            Type action = settings.GetType().Assembly.GetType("JueMingR.Features.Items.ItemActionKind", true);
            Type list = settings.GetType().Assembly.GetType("JueMingR.Features.Items.ItemListKind", true);
            object discard = settings.GetType().GetMethod("WithTypes").Invoke(settings, new[] { Enum.ToObject(list, 1), new[] { 100 } });
            discard = settings.GetType().GetMethod("WithEnabled").Invoke(discard, new[] { Enum.ToObject(action, 2), (object)true });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { discard });
            Main.LocalPlayer.inventory[10] = new Item { type = 100, stack = 2 }; main.RunUpdateLoop(7);
            if (!Main.LocalPlayer.inventory[10].IsAir)
            {
                object world = Get(items, "World"); var observed = new object[] { null };
                world.GetType().GetMethod("TryObserve", Flags).Invoke(world, observed);
                Console.WriteLine("Loaded item diagnostic: failed={0}, enabled={1}, capability={2}, result={3}", Get(Get(items, "Feature"), "HasFailed"), Get(Get(items, "Feature"), "Enabled"), Get(items, "CapabilityError"), Get(Get(items, "Ownership"), "DiscardResult"));
            }
            Check(Main.LocalPlayer.inventory[10].IsAir && Main.LocalPlayer.trashItem.type == 100 && Main.LocalPlayer.trashItem.stack == 2,
                "production configuration/selection/native trash/result chain");
            object sale = settings.GetType().GetMethod("WithTypes").Invoke(settings, new[] { Enum.ToObject(list, 0), new[] { 100 } });
            sale = settings.GetType().GetMethod("WithEnabled").Invoke(sale, new[] { Enum.ToObject(action, 1), (object)true });
            items.GetType().GetMethod("Change", Flags).Invoke(items, new[] { sale });
            Main.playerInventory = true; Main.npcShop = 1;
            Main.LocalPlayer.inventory[10] = new Item { type = 100, stack = 2 }; main.RunUpdateLoop(7);
            Check(Main.LocalPlayer.inventory[10].IsAir && Main.LocalPlayer.inventory[50].stack == 10 && Main.instance.shop[1].item[0].buyOnce,
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
            Check(Get(context, "notes") != null && Get(context, "Shell") != null, "existing Notes and F5 remain composed");
            Console.WriteLine("PASS: separately compiled item Host loaded through bootstrap; all three config/native/result chains, shared session, defaults and existing composition.");
        }
        private static object Get(object value, string name)
        { FieldInfo field = value.GetType().GetField(name, Flags); return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, Flags).GetValue(value); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
