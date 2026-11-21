using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal sealed class NativeCompanionSupplyMenuChecks
    {
        private readonly object window, host;
        private readonly Guid id;
        private readonly Action<Vector2, bool, Keys[], int> frame;
        private readonly Action<string> click, capture;
        private readonly Action prepare;
        private static readonly Keys[] Neutral = Array.Empty<Keys>();

        internal NativeCompanionSupplyMenuChecks(object window, object host, Guid id, Action<Vector2, bool, Keys[], int> frame,
            Action<string> click, Action prepare, Action<string> capture)
        {
            this.window = window; this.host = host; this.id = id;
            this.frame = frame; this.click = click; this.prepare = prepare; this.capture = capture;
        }

        internal static void LoadIcons(ProbeGraphics graphics, object host)
        {
            var catalog = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionSupplyCatalog", true);
            var options = AccessTools.Method(catalog, "Options");
            var textures = new SortedSet<int>();
            foreach (var kind in Enum.GetValues(options.GetParameters()[0].ParameterType))
            {
                var definitions = (IEnumerable)options.Invoke(null, new[] { kind });
                textures.UnionWith(definitions.Cast<object>().Select(option => ((Item)Get(option, "Icon")).type));
            }
            graphics.LoadItemTextures(textures);
            Console.WriteLine("SUPPLY-MENU-XNB item IDs=" + string.Join(",", textures));
        }

        private CompanionBehavior Behavior => NativeCompanionConfigurationChecks.Profile(host, id).Behavior;
        private object[] Cards => ((IEnumerable)Get(window, "supplyCells")).Cast<object>().ToArray();
        private static int[] Types(object card) => (int[])Get(Get(card, "Option"), "Types");
        private object Card(int type) => Cards.Single(card => Types(card).Contains(type));
        private static Vector2 Center(object rect) => NativeToolsUiChecks.Point(rect);
        private void PointAt(object card) => frame(Center(Get(card, "Rect")), false, Neutral, 0);
        private void ClickRect(object rect)
        {
            var point = Center(rect);
            frame(point, false, Neutral, 0); frame(point, true, Neutral, 0); frame(point, false, Neutral, 0);
            PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            Require(!Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlUseTile && !Main.mouseLeft,
                "the supply popup owns its full physical click");
        }
        private void ClickCard(int type) => ClickRect(Get(Card(type), "Rect"));
        private void Close()
        {
            ClickRect(Get(window, "supplyClose"));
            Require(!(bool)Get(window, "SupplyPopupVisible") && (bool)Get(window, "Visible"), "the large close cross retires only supply configuration");
        }

        internal void CheckWide(Player body)
        {
            var panel = Get(window, "panel");
            var parentMaximum = Get(window, "bodyMaximum");
            Require(Behavior.AllowNurse && !Behavior.FurnitureEnabled, "new profiles retain the established nurse default and start with automatic furniture off");
            click("config-Life/关闭/"); click("config-Furniture/开启/");
            Require(!Behavior.HealthRecoveryEnabled && Behavior.AllowNurse && Behavior.FurnitureEnabled, "nurse and furniture have independent parent rows beside potion recovery");
            click("config-Nurse/关闭/"); click("config-Furniture/关闭/");
            click("config-Life/快速/"); click("config-Life/配置/");
            Require((bool)Get(window, "SupplyPopupVisible") && (float)Get(Get(window, "supplyPanel"), "Width") < (float)Get(panel, "Width") / 2 &&
                Get(window, "bodyMaximum").Equals(parentMaximum), "a compact independent icon menu leaves parent height and scroll extent untouched");
            Require(Behavior.HealthRecoveryMode == CompanionHealthRecoveryMode.Fast && !Behavior.AllowNurse &&
                Get(Get(Get(window, "supplyAvailable"), "View"), "Y").Equals(Get(Get(window, "supplyTitleRect"), "Bottom")) &&
                Cards.All(card => Types(card).All(type => ContentSamples.ItemsByType[type].healLife > 0)),
                "the healing grid starts directly below its title, contains only healing items and leaves the nurse button on the parent");
            ClickCard(ItemID.HealingPotion);
            Require(Behavior.ExcludedHealingTypes.Contains(ItemID.HealingPotion), "an absent healing potion can be unchecked in its icon grid");
            PointAt(Card(ItemID.HealingPotion)); capture("companion-config-heal-modes");

            var icons = Get(window, "supplyIcons");
            object layoutBuilds = Get(icons, "VisibleSetBuilds"), loads = Get(icons, "IconLoads");
            for (int i = 0; i < 100; i++) prepare();
            Require(Get(icons, "VisibleSetBuilds").Equals(layoutBuilds) && Get(icons, "IconLoads").Equals(loads),
                "an unchanged supply window keeps its visible projection and borrowed icons");
            frame(Vector2.Zero, false, new[] { Keys.Escape }, 0); frame(Vector2.Zero, false, Neutral, 0);
            Require(!(bool)Get(window, "SupplyPopupVisible") && (bool)Get(window, "Visible"), "Escape closes only the supply popup");

            click("config-Mana/关闭/"); click("config-Mana/配置/"); ClickCard(ItemID.ManaPotion);
            Require(!Behavior.ManaRecoveryEnabled && Behavior.ExcludedManaTypes.Contains(ItemID.ManaPotion) && Behavior.ExcludedHealingTypes.Contains(ItemID.HealingPotion),
                "the independent mana grid edits only its list even with automation off");
            Close();
            click("config-Growth/关闭/"); click("config-Growth/配置/"); ClickCard(ItemID.LifeCrystal);
            Require(Behavior.BuffEnabled && !Behavior.GrowthEnabled && Behavior.AllowedGrowthTypes.SequenceEqual(new[] { (int)ItemID.LifeCrystal }) &&
                ((string)Get(Card(ItemID.LifeCrystal), "Detail")).Contains("缺货"), "the growth grid retains missing allowed growth items");
            int life = body.statLifeMax;
            body.statLifeMax = 400;
            AccessTools.Field(typeof(Main), "_gameUpdateCount").SetValue(null, Main.GameUpdateCount + 20);
            prepare(); PointAt(Card(ItemID.LifeCrystal));
            Require(((string)Get(Card(ItemID.LifeCrystal), "Detail")).Contains("已完成"), "native growth completion is visible independently of the lifetime permission");
            capture("companion-config-growth");
            body.statLifeMax = life; Close();

            var behavior = Behavior;
            const int unknown = 90001;
            behavior.AllowedBuffTypes = new[] { (int)ItemID.Apple, unknown };
            Require((bool)Call(host, "SetBehavior", id, behavior), "an existing partial food permission and an undisplayed saved type are accepted");
            click("config-Buff/配置/");
            var food = Card(ItemID.Apple);
            int[] foodTypes = Types(food);
            Require((bool)Get(food, "Partial") && foodTypes.Contains(ItemID.PumpkinPie) && foodTypes.Contains(ItemID.SeafoodDinner) &&
                foodTypes.Select(type => ContentSamples.ItemsByType[type].buffType).Distinct().OrderBy(value => value)
                    .SequenceEqual(new[] { BuffID.WellFed, BuffID.WellFed2, BuffID.WellFed3 }) &&
                Behavior.AllowedBuffTypes.SequenceEqual(new[] { (int)ItemID.Apple, unknown }),
                "all three native food strengths share one mixed option; opening it leaves the original partial permission intact");
            ClickCard(ItemID.Apple);
            Require(Behavior.AllowedBuffTypes.OrderBy(value => value).SequenceEqual(foodTypes.Concat(new[] { unknown }).OrderBy(value => value)),
                "adding food explicitly grants all concrete food types and preserves the undisplayed type");

            int[] luck = { ItemID.LuckPotionLesser, ItemID.LuckPotion, ItemID.LuckPotionGreater };
            foreach (int type in luck) Require(Types(Card(type)).SequenceEqual(new[] { type }), "a distinct strength has a separate item permission: " + type);
            ClickCard(ItemID.LuckPotionGreater);
            Require(!Behavior.AllowedBuffTypes.Contains(ItemID.LuckPotionLesser) && !Behavior.AllowedBuffTypes.Contains(ItemID.LuckPotion) &&
                Behavior.AllowedBuffTypes.Contains(ItemID.LuckPotionGreater), "selecting greater luck leaves the two other strengths disabled");

            var oldWeak = body.inventory[33]; var oldStrong = body.inventory[34];
            body.inventory[33] = new Item(ItemID.LuckPotionLesser); body.inventory[34] = new Item(ItemID.LuckPotionGreater);
            try
            {
                var actor = Call(host, "Owned", body);
                var chooser = AccessTools.Method(host.GetType(), "ChooseQuick");
                object kind = Enum.Parse(chooser.GetParameters()[1].ParameterType, "Buff");
                Func<object> choose = () => chooser.Invoke(host, new[] { actor, kind, 0, false });
                Require(ReferenceEquals(Get(choose(), "Item"), body.inventory[34]), "the running actor selects the allowed strength after skipping the earlier forbidden potion");
                body.AddBuff(BuffID.Lucky, 600);
                Require(!(bool)Get(choose(), "Found"), "an active native luck effect prevents a second same-effect potion");
                body.DelBuff(body.FindBuffIndex(BuffID.Lucky));
                AccessTools.Field(typeof(Main), "_gameUpdateCount").SetValue(null, Main.GameUpdateCount + 20);
                prepare(); PointAt(Card(ItemID.LuckPotionGreater));
                capture("companion-config-buff-panes");
            }
            finally { body.inventory[33] = oldWeak; body.inventory[34] = oldStrong; }
            ClickCard(ItemID.Apple);
            Require(Behavior.AllowedBuffTypes.OrderBy(value => value).SequenceEqual(new[] { (int)ItemID.LuckPotionGreater, unknown }),
                "removing the one selected food icon affects food alone");
            Close();
            Require(Get(window, "panel").Equals(panel) && Get(window, "bodyMaximum").Equals(parentMaximum), "all four supply menus preserve the parent geometry");
        }

        internal void CheckNarrow(Vector2 screen, Matrix matrix)
        {
            var panel = Get(window, "supplyPanel");
            var parent = Get(window, "panel"); var parentOffset = Get(window, "bodyOffset");
            Require((float)Get(panel, "X") >= 0 && (float)Get(panel, "Y") >= 0 &&
                (float)Get(panel, "Right") * matrix.M11 <= screen.X && (float)Get(panel, "Bottom") * matrix.M22 <= screen.Y,
                "the complete scaled popup is inside the real physical canvas");
            var available = Get(window, "supplyAvailable"); var chosen = Get(window, "supplyChosen");
            var original = Behavior.AllowedBuffTypes;
            var point = Center(Get(Card(ItemID.Apple), "Rect"));
            frame(point, false, Neutral, 0); frame(point, true, Neutral, 0);
            frame(point, true, Neutral, -120); frame(point, false, Neutral, 0);
            Require(Behavior.AllowedBuffTypes.SequenceEqual(original), "scrolling with a card pressed cancels the old option");
            int maximum = (int)Get(available, "Maximum");
            Require(maximum > 0, "the complete catalogue needs multiple pages at narrow scale");
            while ((int)Get(available, "FirstRow") < maximum) frame(Center(Get(available, "View")), false, Neutral, -120);
            object last = ((IEnumerable)Get(available, "Cards")).Cast<object>().Last();
            var lastRect = Get(last, "Rect");
            Require(Cards.Contains(last) && (float)Get(lastRect, "Bottom") <= (float)Get(Get(available, "View"), "Bottom"),
                "the last icon is visible and fully hittable by scrolling its actual pane");
            int type = Types(last)[0];
            ClickRect(lastRect);
            var selected = Card(type);
            Require(Behavior.AllowedBuffTypes.Contains(type) && (bool)Get(selected, "Allowed") &&
                (float)Get(Get(selected, "Rect"), "X") >= (float)Get(Get(chosen, "View"), "X") &&
                Get(window, "bodyOffset").Equals(parentOffset) && Get(window, "panel").Equals(parent),
                "the scaled click moves exactly the last option to the selected pane while the parent stays fixed");
            PointAt(selected); capture("companion-config-narrow-buffs");
            Close();
        }
    }
}
