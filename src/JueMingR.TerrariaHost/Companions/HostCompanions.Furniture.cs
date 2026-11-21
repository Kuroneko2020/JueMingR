using System;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private void UpdateCompanionFurniture(Actor actor)
        {
            if (!actor.Preferences.FurnitureEnabled)
            {
                actor.Furniture = null;
                return;
            }
            var body = actor.Body;
            if (!CanUseFurniture(actor, body)) return;
            if (actor.Furniture == null)
                actor.Furniture = new FurnitureRecovery(player => CanUseFurniture(actor, player) && !FurnitureInventoryBusy(player), (player, x, y) =>
                {
                    // The native tile method checks the local player for some
                    // effects. Only its actual use runs under this actor/socket.
                    using (CompanionHooks.Enter(actor, new Vector2(x * 16 + 8, y * 16 + 8)))
                        FurnitureRecovery.NativeUse(player, x, y);
                }, message => Feedback = message);
            actor.Furniture.UpdateInRange(body, Main.GameUpdateCount, actor.Generation, ActorTileReach(body));
        }

        private bool CanUseFurniture(Actor actor, Player body)
        {
            return ReferenceEquals(actor.Body, body) && actor.Preferences.FurnitureEnabled && SupplyActorReady(actor) &&
                ActorTileReachReady(body) && !Main.gamePaused && !WorldGen.isGeneratingOrLoadingWorld && !Main.ServerSideCharacter &&
                Main.ActivePlayerFileData?.ServerSideCharacter != true && !body.ghost && body.spectating < 0 &&
                !body.CCed && !body.cursed && !body.noItems && !body.isOperatingAnotherEntity && !body.mouseInterface &&
                body.chest == -1 && body.talkNPC < 0 && body.sign < 0;
        }

        private static bool FurnitureInventoryBusy(Player body)
        {
            // HasLockedInventory also consults the human crafting cursor and
            // clears empty slot locks. A placed object only needs a read-only
            // check of this body's live transfer, immediately before using it.
            if (body.disableVoidBag >= 0) return true;
            for (int i = 0; i < body.inventoryChestStack.Length; i++)
                if (body.inventoryChestStack[i] && body.inventory[i]?.IsAir == false) return true;
            return false;
        }
    }
}
