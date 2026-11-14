using System;
using System.Collections;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses the actual window, archived profiles and native Player/Item projections.
    // Layout-only preparation does not initialize graphics, run Draw or operate a
    // game. These assertions do not stand in for owner UI/IME/FPS acceptance.
    internal static class NativeCompanionUiChecks
    {
        internal static void Run(object shell)
        {
            var host = Get(shell, "Companions");
            var profiles = ((IEnumerable)Get(host, "Profiles")).Cast<object>().ToArray();
            Require(profiles.Length >= 2, "UI fixture must first persist two actual companion profiles through the production archive");
            var window = Get(shell, "CompanionUi"); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var screen = new Vector2(960, 760); var matrix = Matrix.Identity;
            var origin = Vector2.Zero;
            Action prepare = () => Call(window, "PrepareLayout", matrix, screen);
            Action<Vector2, bool, bool, int> sample = (point, left, focused, wheel) =>
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, matrix, screen, focused, wheel);
            };
            Action<string> click = key =>
            {
                var control = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
                var point = NativeToolsUiChecks.Point(Get(control, "Rect"));
                sample(point, false, true, 0); sample(point, true, true, 0); sample(point, false, true, 0);
            };
            try
            {
                NativeToolsUiChecks.Mouse(origin, false);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                Require((bool)Get(window, "Visible"), "real manager opens");
                string[] tabs = Controls(window).Select(c => (string)Get(c, "Text")).Where(t => t == "背包" || t == "配置" || t == "聊天").ToArray();
                Require(tabs.SequenceEqual(new[] { "背包", "配置" }), "first batch exposes exactly the two actual tabs");
                var list = Get(window, "listView"); var plus = Get(Controls(window).Single(c => (string)Get(c, "Text") == "＋"), "Rect");
                Require((float)Get(plus, "Y") >= (float)Get(list, "Bottom"), "create plus remains below the persistent left list");
                int actors = ((IDictionary)Get(host, "actors")).Count;
                Guid other = (Guid)Get(profiles[1], "Id");
                click("profile:" + other);
                Require((Guid)Get(window, "selected") == other && ((IDictionary)Get(host, "actors")).Count == actors,
                    "selecting another real profile neither joins it nor changes any actor");
                int builds = (int)Get(window, "LayoutBuilds"), measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 120; i++) prepare();
                Require((int)Get(window, "LayoutBuilds") == builds && (int)Get(layout, "MeasurementCount") == measurements,
                    "120 stable real window prepares rebuild no controls and measure no text");
                var beforeList = Get(window, "listView"); var body = Get(window, "bodyView");
                var bodyPoint = NativeToolsUiChecks.Point(body);
                sample(bodyPoint, false, true, -120);
                Require((int)Get(window, "bodyOffset") > 0 && Get(window, "listView").Equals(beforeList) && (bool)Get(window, "ConsumeWheel"),
                    "actual right inventory scroll consumes the wheel while retaining the left list geometry");
                click("configuration");
                Require((int)Get(window, "page") == 1 && Get(window, "listView").Equals(beforeList), "configuration keeps the same left character list");
                Action scrollBottom = () =>
                {
                    while ((int)Get(window, "bodyOffset") < (int)Get(window, "bodyMaximum"))
                    {
                        int before = (int)Get(window, "bodyOffset"); sample(bodyPoint, false, true, -120);
                        Require((int)Get(window, "bodyOffset") > before, "configuration scroll advances toward its actual body maximum");
                    }
                };
                scrollBottom(); click("request-reset"); scrollBottom();
                int expandedOffset = (int)Get(window, "bodyOffset");
                long epoch = ((JueMingR.Platform.Companions.CompanionProfile)profiles[1]).DataEpoch;
                click("cancel-reset");
                Require(!(bool)Get(window, "confirmingReset") && (int)Get(window, "bodyOffset") < expandedOffset &&
                    (int)Get(window, "bodyOffset") == (int)Get(window, "bodyMaximum"),
                    "cancelling expanded reset confirmation shrinks content and clamps the actual offset without resetting a character");
                var request = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("request-reset/", StringComparison.Ordinal));
                var requestRect = Get(request, "Rect");
                Require(Math.Abs((float)Get(requestRect, "Bottom") - ((float)Get(body, "Bottom") - 8)) <= 1.1f,
                    "same preparation reprojects the collapsed last control to the clamped offset");
                prepare();
                request = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("request-reset/", StringComparison.Ordinal));
                Require(Get(request, "Rect").Equals(requestRect) && ((IEnumerable)Get(host, "Profiles")).Cast<JueMingR.Platform.Companions.CompanionProfile>().Single(p => p.Id == other).DataEpoch == epoch,
                    "the next stable frame retains the corrected geometry and cancellation never performs reset");
                click("inventory");
                var config = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("configuration/", StringComparison.Ordinal));
                var oldPoint = NativeToolsUiChecks.Point(Get(config, "Rect"));
                sample(oldPoint, false, true, 0); sample(oldPoint, true, true, 0);
                matrix = Matrix.CreateScale(1.5f); screen = new Vector2(1280, 720);
                sample(oldPoint, false, true, 0);
                Require((int)Get(window, "page") == 0, "a release after native UI-matrix/viewport change cannot execute the old tab press");
                prepare();
                config = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("configuration/", StringComparison.Ordinal));
                var point2 = NativeToolsUiChecks.Point(Get(config, "Rect"));
                sample(point2, true, true, 0); sample(point2, false, false, 0);
                Require(!(bool)Get(window, "Visible") && (int)Get(window, "page") == 0, "focus loss closes the view without submitting its armed tab");
                sample(origin, false, true, 0);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                var close = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("close/", StringComparison.Ordinal));
                var closePoint = NativeToolsUiChecks.Point(Get(close, "Rect"));
                sample(closePoint, false, true, 0); sample(closePoint, true, true, 0); sample(closePoint, false, true, 0);
                Require(!(bool)Get(window, "Visible") && (bool)Get(window, "ConsumeLeft"), "closing retains its actual mouse release for the shared shell consumer");
                sample(origin, false, true, 0);
                Require(!(bool)Get(window, "ConsumeLeft"), "a later trusted neutral sample retires only the owned physical tail");
                builds = (int)Get(window, "LayoutBuilds"); measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 120; i++) prepare();
                Require((int)Get(window, "LayoutBuilds") == builds && (int)Get(layout, "MeasurementCount") == measurements,
                    "closed window performs no layout or font work");
                Console.WriteLine("PASS companion actual two-page layout, native profile selection, stable/closed work, independent scroll, changed matrix/focus gesture retirement and real close release tail; no Draw, Windows IME, owner input or FPS claim.");
            }
            finally { Call(window, "Close"); NativeToolsUiChecks.Mouse(origin, false); }
        }
        internal static void Equipment(object shell, Guid id)
        {
            var host = Get(shell, "Companions"); var window = Get(shell, "CompanionUi"); var layout = Get(Get(shell, "State"), "Layout");
            var companion = (Player)Call(host, "GetBody", id); var owner = Main.LocalPlayer;
            Require((bool)Call(host, "CanTransfer", id), "equipment UI scenario uses a real active owned actor");
            var screen = new Vector2(960, 760); var matrix = Matrix.Identity;
            Action prepare = () => Call(window, "PrepareLayout", matrix, screen);
            Action<Vector2, bool, int> sample = (point, left, wheel) =>
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, matrix, screen, true, wheel);
            };
            Action<int> scroll = direction =>
            {
                int before = (int)Get(window, "bodyOffset");
                sample(NativeToolsUiChecks.Point(Get(window, "bodyView")), false, direction * 120);
                Require((int)Get(window, "bodyOffset") != before, "equipment source can be reached through the actual body wheel");
            };
            Action<string> click = prefix =>
            {
                while ((int)Get(window, "bodyOffset") > 0) scroll(1);
                object control;
                while ((control = Controls(window).SingleOrDefault(c => ((string)Get(c, "Key")).StartsWith(prefix, StringComparison.Ordinal))) == null)
                {
                    Require((int)Get(window, "bodyOffset") < (int)Get(window, "bodyMaximum"), "actual equipment control exists: " + prefix);
                    scroll(-1);
                }
                Require((bool)Get(control, "Enabled"), "actual equipment control is enabled: " + prefix);
                var point = NativeToolsUiChecks.Point(Get(control, "Rect"));
                sample(point, false, 0);
                Console.WriteLine("COMPANION UI equipment press key=" + Get(control, "Key") + " point=" + point +
                    " offset=" + Get(window, "bodyOffset") + " generation=" + Get(window, "generation") +
                    " selected=" + Get(window, "selected"));
                sample(point, true, 0);
                Console.WriteLine("COMPANION UI equipment armed=" + (GetOptional(window, "armedKey") ?? "<none>") +
                    " armedGeneration=" + Get(window, "armedGeneration") + " generation=" + Get(window, "generation"));
                sample(point, false, 0);
                Console.WriteLine("COMPANION UI equipment release prefix=" + prefix + " sourceOwner=" + Get(window, "sourceOwner") +
                    " sourceArea=" + Get(window, "sourceArea") + " sourceSlot=" + Get(window, "sourceSlot") +
                    " feedback=" + Get(host, "Feedback"));
            };
            var savedSlot = owner.inventory[49].Clone();
            int[] types = { ItemID.RedDye, ItemID.GrapplingHook };
            Func<int, int> total = type => InventoryCount(owner, type) + InventoryCount(companion, type);
            var originalShaders = Terraria.Graphics.Shaders.GameShaders.Armor;
            var hook = new Item(); hook.SetDefaults(ItemID.GrapplingHook);
            Require(hook.shoot > 0, "native grappling hook exposes its actual projectile category");
            bool originalHookCategory = Main.projHook[hook.shoot];
            try
            {
                // This CPU fixture bypasses Main.LoadContent. Item.SetDefaults
                // obtains dye IDs from the native armor-shader registry, while
                // Main's content loading derives hook categories from original
                // Projectile defaults. Supply those exact metadata dependencies,
                // not item.dye or a successful Equip substitute. ShaderData here
                // is never applied/rendered; no effect/Draw behavior is asserted.
                if (originalShaders.GetShaderIdFromItemId(ItemID.RedDye) == 0)
                {
                    Terraria.Graphics.Shaders.GameShaders.Armor = new Terraria.Graphics.Shaders.ArmorShaderDataSet();
                    var initializeDye = typeof(Terraria.Initializers.DyeInitializer).GetMethod("LoadBasicColorDye",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
                        new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int) }, null);
                    Require(initializeDye != null, "native red-dye initializer matches the fixed original game API");
                    initializeDye.Invoke(null, new object[] { ItemID.RedDye, 1f, 0f, 0f, 1.2f, 1 });
                }
                var hookProjectile = new Projectile(); hookProjectile.SetDefaults(hook.shoot);
                Main.projHook[hook.shoot] = hookProjectile.aiStyle == 7;
                Require(Terraria.Graphics.Shaders.GameShaders.Armor.GetShaderIdFromItemId(ItemID.RedDye) > 0 && Main.projHook[hook.shoot],
                    "native content metadata classifies red dye and the original grappling-hook projectile");
                Console.WriteLine("COMPANION UI equipment fixture native dyeId=" + Terraria.Graphics.Shaders.GameShaders.Armor.GetShaderIdFromItemId(ItemID.RedDye) +
                    " hookProjectile=" + hook.shoot + " nativeAiStyle=" + hookProjectile.aiStyle + " priorHookCategory=" + originalHookCategory + "; no shader Apply/Draw");
                Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                click("profile:" + id + "/"); click("inventory/");
                Require(companion.dye[0].IsAir && companion.miscEquips[4].IsAir && types.All(t => total(t) == 0),
                    "isolated fresh profile has no fixture dye/hook before the UI resource scenario");
                for (int i = 0; i < types.Length; i++)
                {
                    int type = types[i]; string area = i == 0 ? "Dye" : "Misc"; int slot = i == 0 ? 0 : 4;
                    owner.inventory[49].SetDefaults(type); owner.inventory[49].stack = 1; prepare();
                    Require(total(type) == 1, "one real fixture item exists before equipment: " + area);
                    var source = owner.inventory[49];
                    Console.WriteLine("COMPANION UI equipment native source=" + source.type + ":" + source.stack + " dye=" + source.dye +
                        " head/body/leg=" + source.headSlot + "/" + source.bodySlot + "/" + source.legSlot + " accessory=" + source.accessory +
                        " mount=" + source.mountType + " shoot=" + source.shoot + " canTransfer=" + Call(host, "CanTransfer", id));
                    click("slot:True:Inventory:49:"); click("equip:");
                    var equipped = i == 0 ? companion.dye[slot] : companion.miscEquips[slot];
                    Console.WriteLine("COMPANION UI equipment actual owner49=" + owner.inventory[49].type + ":" + owner.inventory[49].stack +
                        " expected=" + area + "[" + slot + "]=" + equipped.type + ":" + equipped.stack +
                        " dyeSlots=" + string.Join(",", companion.dye.Select((item, index) => index + "=" + item.type + ":" + item.stack)) +
                        " miscSlots=" + string.Join(",", companion.miscEquips.Select((item, index) => index + "=" + item.type + ":" + item.stack)) +
                        " ownerCount=" + InventoryCount(owner, type) + " companionCount=" + InventoryCount(companion, type) +
                        " sameBody=" + ReferenceEquals(companion, Call(host, "GetBody", id)) + " feedback=" + Get(host, "Feedback"));
                    Require(equipped.type == type && equipped.stack == 1 && total(type) == 1,
                        "UI equip moves the real item into its native " + area + " slot without duplicating it");
                    click("slot:False:" + area + ":" + slot + ":"); click("unequip:");
                    Require(equipped.IsAir && InventoryCount(companion, type) == 1 && InventoryCount(owner, type) == 0 && total(type) == 1,
                        "area-aware UI unequip removes the exact native " + area + " slot and retains one item in companion inventory");
                    int received = Array.FindIndex(companion.inventory, item => item.type == type && item.stack == 1);
                    Require(received >= 0, "actual companion inventory received unequipped item: " + area);
                    click("slot:False:Inventory:" + received + ":"); click("transfer-stack:");
                    Require(InventoryCount(owner, type) == 1 && InventoryCount(companion, type) == 0 && total(type) == 1,
                        "UI take-back completes " + area + " round trip with the actual owner/companion total unchanged");
                    var archive = Get(host, "profiles");
                    NativeQuickItemChecks.Until(() => { Call(archive, "Pump"); return (bool)Call(archive, "IsSaved", id); });
                    var committed = ((IEnumerable)Get(host, "Profiles")).Cast<JueMingR.Platform.Companions.CompanionProfile>().Single(p => p.Id == id);
                    var readback = (Player)Call(archive, "LoadBody", committed);
                    Require(InventoryCount(readback, type) == 0 && InventoryCount(owner, type) == 1,
                        "actual saved native profile retains the completed " + area + " take-back without restoring an equipment copy");
                    foreach (var item in owner.inventory) if (item.type == type) item.TurnToAir();
                }
                Console.WriteLine("PASS real companion UI Dye/Misc equip, correct area/slot selection, unequip and take-back with native item conservation; no Draw/IME/FPS claim.");
            }
            finally
            {
                Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                owner.inventory[49] = savedSlot;
                Terraria.Graphics.Shaders.GameShaders.Armor = originalShaders;
                Main.projHook[hook.shoot] = originalHookCategory;
            }
        }
        private static int InventoryCount(Player player, int type)
        {
            return new[] { player.inventory, player.armor, player.dye, player.miscEquips, player.miscDyes }
                .Sum(items => items.Where(item => item != null && item.type == type).Sum(item => item.stack));
        }
        private static object[] Controls(object window) { return ((IEnumerable)Get(window, "controls")).Cast<object>().ToArray(); }
    }
}
