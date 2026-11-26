using System;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFishingOpeningChecks
    {
        internal static void Run(object context, object host, object input, Player actor, Player other,
            CompanionProfile profile, CompanionProfile otherProfile)
        {
            var owner = Main.LocalPlayer;
            var savedSeats = Main.sittingManager;
            var savedOwnerPosition = owner.position;
            var savedOwnerDirection = owner.direction;
            Main.sittingManager = new AnchoredEntitiesCollection();
            bool Toggle(Guid ownerId, Guid id, bool active) => (bool)Call(host, "SetFishing", ownerId, id, active);
            void Step()
            {
                Main.sittingManager.ClearPlayerAnchors();
                owner.sitting.UpdateSitting(owner);
                NativeCompanionFishingLineChecks.Step(context, input, actor, other);
            }

            try
            {
                Require(Toggle(otherProfile.OwnerId, otherProfile.Id, false), "finish other role's original test cast");
                for (int frame = 0; frame < 120 && NativeCompanionFishingLineChecks.OwnedBobber(other) != null; frame++) Step();
                Require(NativeCompanionFishingLineChecks.OwnedBobber(other) == null, "prior native line retires before the terrain fixture changes");
                Main.leftWorld = Main.topWorld = 0;
                Main.rightWorld = Main.maxTilesX * 16;
                Main.bottomWorld = Main.maxTilesY * 16;
                for (int x = 25; x <= 104; x++) for (int y = 22; y <= 85; y++) Main.tile[x, y].ClearEverything();
                for (int x = 37; x < 55; x++) NativeToolsChecks.Tile(x, 48, TileID.Stone);
                for (int y = 46; y <= 74; y++) NativeToolsChecks.Tile(55, y, TileID.Stone);
                for (int x = 59; x <= 65; x++) for (int y = 52; y < 71; y++) Main.tile[x, y].liquid = 255;

                var top = Main.tile[50, 46];
                var bottom = Main.tile[50, 47];
                top.active(true); top.type = TileID.Chairs; top.frameX = 18;
                bottom.active(true); bottom.type = TileID.Chairs; bottom.frameX = 18; bottom.frameY = 18;
                Require(PlayerSittingHelper.GetSittingTargetInfo(owner, 50, 47, out _, out Vector2 seat, out _, out _),
                    "fixture uses a real east-facing chair at the edge of the bank");
                owner.Bottom = seat + new Vector2(-8, 0);
                owner.velocity = Vector2.Zero;
                owner.controlLeft = owner.controlRight = owner.controlUp = owner.controlDown = owner.controlJump = false;
                owner.sitting.SitDown(owner, 50, 47);
                Require(owner.sitting.isSitting, "owner really occupies the chair before the mode is enabled");
                actor.Bottom = seat + new Vector2(-72, 0);
                actor.velocity = Vector2.Zero;
                other.Bottom = seat + new Vector2(-130, 0);
                other.velocity = Vector2.Zero;
                var role = Call(host, "Owned", actor);
                Require(Toggle(profile.OwnerId, profile.Id, true), "normal fishing command captures the actual occupied chair");
                object activity = Get(role, "Fishing");
                for (int frame = 0; frame < 180 && actor.chatOverhead.chatText != "椅子让一下，我要坐"; frame++) Step();
                Require(actor.chatOverhead.chatText == "椅子让一下，我要坐" && owner.sitting.isSitting && !actor.sitting.isSitting &&
                    NativeCompanionFishingLineChecks.OwnedBobber(actor) == null,
                    "fishing actor navigates to an occupied chair, asks from its own identity, and waits: " + Get(role, "Status"));
                owner.sitting.SitUp(owner, multiplayerBroadcast: false);
                owner.Bottom = new Vector2(84 * 16 + 8, 48 * 16);

                Projectile first = null;
                for (int frame = 0; frame < 320 && (first == null || !(bool)Get(activity, "Reported")); frame++)
                {
                    Step();
                    first = NativeCompanionFishingLineChecks.WetBobber(actor);
                }
                Require(actor.sitting.isSitting && actor.Bottom == seat && ReferenceEquals(Get(role, "Fishing"), activity),
                    "the actor takes the original native chair after the owner walks away: " + Get(role, "Status"));
                Require(first != null && first.Center.X >= 59 * 16 && first.Center.X < 66 * 16 && (bool)Get(activity, "Reported") &&
                    actor.chatOverhead.chatText.EndsWith("正在水中钓鱼。", StringComparison.Ordinal),
                    "real rod use crosses above the blocking wall, enters its selected pool and reports from the seated actor: " + Get(role, "Status"));

                int firstKey = (int)first.key;
                NativeCompanionFishingLineChecks.DefineBite(first, ItemID.Bass);
                Projectile next = null;
                for (int frame = 0; frame < 220; frame++)
                {
                    Step();
                    next = NativeCompanionFishingLineChecks.WetBobber(actor);
                    if (next != null && (int)next.key != firstKey) break;
                }
                Require(next != null && (int)next.key != firstKey && actor.sitting.isSitting && actor.Bottom == seat,
                    "native pull and replacement cast leave the seated actor on the fixed chair");
                Console.WriteLine("PASS AI fishing occupied chair, actor chat, original seat after owner moves, actual cast over stone into water, and seated recast.");

                var settings = ((CompanionProfile)Get(role, "Profile")).Behavior;
                settings.TeleportStopsFishing = false;
                Require((bool)Call(host, "SetBehavior", profile.Id, settings), "retain-fishing recall preference saved through its normal entry");
                actor.inventory[38].SetDefaults(ItemID.WormholePotion); actor.inventory[38].stack = 2;
                Require((bool)Call(host, "TryManualRecall", profile.Id), "real paid recall moves the fishing actor");
                object relocated = Get(role, "Fishing");
                Require(!ReferenceEquals(relocated, activity) && (Vector2)Get(relocated, "RequestedFeet") == actor.Bottom &&
                    GetOptional(relocated, "Seat") == null && actor.inventory[38].stack == 1,
                    "retaining the mode starts new fishing from the real wormhole arrival and drops the old chair");

                settings = ((CompanionProfile)Get(role, "Profile")).Behavior;
                settings.TeleportStopsFishing = true;
                Require((bool)Call(host, "SetBehavior", profile.Id, settings), "stop-fishing recall preference saved through its normal entry");
                owner.Bottom += new Vector2(64, 0);
                Require((bool)Call(host, "TryManualRecall", profile.Id) && GetOptional(role, "Fishing") == null &&
                    ((CompanionProfile)Get(role, "Profile")).Behavior.Follow && actor.inventory[38].IsAir,
                    "second real paid recall stops fishing and resumes owner follow");
                Console.WriteLine("PASS AI fishing both real paid wormhole arrivals, new landing request and stop-to-follow.");
            }
            finally
            {
                if (owner.sitting.isSitting) owner.sitting.SitUp(owner, multiplayerBroadcast: false);
                Toggle(profile.OwnerId, profile.Id, false);
                owner.position = savedOwnerPosition;
                owner.direction = savedOwnerDirection;
                Main.sittingManager = savedSeats;
            }
        }
    }
}
