using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameContent.Bestiary;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionVanityShopChecks
    {
        private static Player interruptActor;
        private static int interrupted;
        private static int shopInsideSwap;
        private static Player playerInsideSwap;

        private static void FailAfterExchange(Player __0)
        {
            if (!ReferenceEquals(__0, interruptActor)) return;
            interrupted++;
            shopInsideSwap = Main.npcShop;
            playerInsideSwap = Main.LocalPlayer;
            throw new IOException("isolated native vanity equip notification failure");
        }

        internal static void Run(object shell, Guid id, Player human, Player companion)
        {
            var host = Get(shell, "Companions");
            var window = Get(shell, "CompanionUi");
            var screen = new Vector2(1280, 900);
            const int npcSlot = 22, shop = 2; // The vanilla Arms Dealer shop.
            var previousNpc = Main.npc[npcSlot];
            var previousShops = Main.instance.shop;
            var bestiary = Main.BestiaryTracker;
            var bestiaryDb = Main.BestiaryDB;
            var shopHelper = Main.ShopHelper;
            var previousSettings = human.currentShoppingSettings;
            var previousPortrait = Main.npcChatPortrait;
            string previousChat = Main.npcChatText;
            int portraitFrame = Main.npcChatPortraitFrameCounter, oldTalk = human.talkNPC;
            int oldShop = Main.npcShop, split = Main.stackSplit;
            bool inventory = Main.playerInventory;
            var mouse = PlayerInput.MouseInfo;
            var oldHumanArmor = human.armor[0]; var oldHumanVanity = human.armor[10];
            var oldArmor = companion.armor[0]; var oldVanity = companion.armor[10];
            var patch = new Harmony("JueMingR.Tests.CompanionVanityShop");
            Require(!(bool)Get(window,"Visible"), "shopping gesture starts at a closed companion manager");

            void Sample(Vector2 point, bool left, bool right)
            {
                PlayerInput.MouseInfo = new MouseState((int)point.X, (int)point.Y, 0,
                    left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                    right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, Matrix.Identity, screen, true, 0);
            }
            void Click(string key, bool right = false)
            {
                var control = ((IEnumerable)Get(window, "controls")).Cast<object>()
                    .Single(c => ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
                Require((bool)Get(control,"Enabled"), "shopping leaves companion management control reachable: " + key);
                var point = NativeToolsUiChecks.Point(Get(control, "Rect"));
                Sample(point, false, false); Sample(point, !right, right); Sample(point, false, false);
            }
            try
            {
                Main.ShopHelper = new ShopHelper();
                Main.BestiaryTracker = new BestiaryUnlocksTracker();
                Main.BestiaryDB = new BestiaryDatabase();
                Main.instance.shop = new Chest[100];
                var counter = Main.instance.shop[shop] = Chest.CreateShop();
                var dealer = Main.npc[npcSlot] = new NPC { whoAmI = npcSlot };
                dealer.SetDefaults(NPCID.ArmsDealer);
                dealer.active = dealer.homeless = true;
                dealer.Center = human.Center;
                human.SetTalkNPC(npcSlot);
                Main.instance.OpenShop(shop);
                var wares = counter.item[0];
                var settings = human.currentShoppingSettings;
                var portrait = Main.npcChatPortrait;
                string chat = Main.npcChatText;
                Require(wares.type == ItemID.MusketBall && settings.PriceAdjustment > 0 && human.talkNPC == npcSlot &&
                    Main.npcShop == shop && Main.playerInventory, "actual NPC dialogue and native shop inventory are open");

                var ownArmor = human.armor[0] = new Item(ItemID.IronHelmet);
                var ownVanity = human.armor[10] = new Item(ItemID.CopperHelmet);
                var armor = companion.armor[0] = new Item(ItemID.GoldHelmet);
                var vanity = companion.armor[10] = new Item(ItemID.SilverHelmet);
                void OwnerStillShopping()
                {
                    Require(ReferenceEquals(Main.LocalPlayer, human) && ReferenceEquals(human.armor[0], ownArmor) &&
                        ReferenceEquals(human.armor[10], ownVanity) && Main.npcShop == shop && Main.playerInventory &&
                        ReferenceEquals(Main.instance.shop[shop], counter) && ReferenceEquals(counter.item[0], wares) &&
                        human.talkNPC == npcSlot && ReferenceEquals(Main.npc[npcSlot], dealer) &&
                        human.currentShoppingSettings.Equals(settings) && Main.npcChatText == chat &&
                        ReferenceEquals(Main.npcChatPortrait, portrait) && (bool)Get(window,"Visible"),
                        "owner keeps both helmets, original shop stock, NPC conversation and shopping terms");
                }

                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                var renderer = Get(shell, "renderer");
                Call(renderer, "RefreshResources");
                Call(renderer, "Prepare", Get(shell,"State"), screen.X, screen.Y, 1f);
                Call(window, "ToggleIndependent", new object[] { null });
                Call(window, "PrepareLayout", Matrix.Identity, screen);
                Require((Guid)Get(window,"selected") == id, "independent manager selects the joined character");
                Click("gear:True:1"); Click("gear:False:1");
                Click("slot:True:Armor:10", right: true);
                OwnerStillShopping();
                Click("slot:False:Armor:10", right: true);
                Require(ReferenceEquals(companion.armor[0], vanity) && ReferenceEquals(companion.armor[10], armor),
                    "the companion's real functional and social helmets exchange while the owner shops: " + Get(host,"Feedback"));
                OwnerStillShopping();

                // Fail after the original method has swapped the real items,
                // before it returns to the host. Exercise both the scope exit
                // and the existing partial-commit receipt without replaying it.
                interruptActor = companion;
                interrupted = 0;
                patch.Patch(AccessTools.Method(typeof(AchievementsHelper), nameof(AchievementsHelper.HandleOnEquip)),
                    prefix: new HarmonyMethod(typeof(NativeCompanionVanityShopChecks), nameof(FailAfterExchange)) { priority = Priority.First });
                Click("slot:False:Armor:10", right: true);
                Require(interrupted == 1 && shopInsideSwap == 0 && ReferenceEquals(playerInsideSwap, companion) &&
                    ReferenceEquals(companion.armor[0], armor) && ReferenceEquals(companion.armor[10], vanity) &&
                    ((string)Get(host,"Feedback")).Contains("原版操作中断"), "native interruption preserves actual exchanged items once");
                OwnerStillShopping();
                Main.SetNPCShopIndex(0);
                Click("slot:True:Armor:10", right: true);
                Require(ReferenceEquals(human.armor[0], ownVanity) && ReferenceEquals(human.armor[10], ownArmor) &&
                    human.talkNPC == npcSlot, "the same owner slot exchanges after leaving the shop while still talking");
                Console.WriteLine("PASS native Arms Dealer dialogue and shop: owner vanity blocked, companion swap succeeds, owner and shop restored after native equip notification throws.");
            }
            finally
            {
                patch.UnpatchAll(patch.Id);
                interruptActor = playerInsideSwap = null;
                Call(window, "Close");
                human.armor[0] = oldHumanArmor; human.armor[10] = oldHumanVanity;
                companion.armor[0] = oldArmor; companion.armor[10] = oldVanity;
                Main.npc[npcSlot] = previousNpc; Main.instance.shop = previousShops;
                Main.ShopHelper = shopHelper; Main.BestiaryTracker = bestiary; Main.BestiaryDB = bestiaryDb;
                Set(human,"talkNPC",oldTalk); human.currentShoppingSettings = previousSettings;
                Main.npcChatText = previousChat; Main.npcChatPortrait = previousPortrait; Main.npcChatPortraitFrameCounter = portraitFrame;
                Main.SetNPCShopIndex(oldShop); Main.stackSplit = split; Main.playerInventory = inventory; PlayerInput.MouseInfo = mouse;
            }
        }
    }
}
