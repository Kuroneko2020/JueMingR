using System;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionExternalModal(object context, object host, object tools, object input)
        {
            var player = Reset(context, host, tools, input);
            object preparation = Get(host, "buildMaterialPreparation");
            player.inventory[12].SetDefaults(ItemID.RedPaint); player.inventory[12].stack = 2;
            player.inventory[54].SetDefaults(ItemID.BluePaint); player.inventory[54].stack = 7;
            Item material = player.inventory[12], originalPriority = player.inventory[54];
            var identity = new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "external-modal-owned-preparation", (long)Get(Get(tools, "Runtime"), "Generation"), 1);
            Step(context, input);
            object[] args = { player, 12, 54, identity, -1 };
            long before = (long)Get(preparation, "NativeClicks");
            Call(preparation, "TryPrepare", args); constructionInputTail();
            Require((bool)Get(preparation, "HasWork") && Main.playerInventory && ReferenceEquals(Main.mouseItem, material), "accepted preparation has moved the real material Item to owned cursor");
            Guid request = Guid.NewGuid(); Require((bool)Call(input, "TryAcquireExternalModal", request), "modal acquires after the first actual native swap");
            bool oldAutoPause = Main.autoPause, oldPaused = Main.gamePaused;
            Main.autoPause = true;
            try
            {
                uint world = Main.GameUpdateCount; int paused = 0;
                for (int i = 0; i < 16 && !(bool)Get(preparation, "Ready"); i++) if (SupplyAutoPauseStep(context, input)) paused++;
                Require((bool)Get(preparation, "Ready") && !(bool)Get(input, "CanRunAutomaticActions") &&
                    (long)Get(preparation, "NativeClicks") == before + 3 && ReferenceEquals(player.inventory[54], material) &&
                    Main.mouseItem.IsAir && !Main.playerInventory && material.stack == 2 && originalPriority.stack == 7 && paused > 0 && Main.GameUpdateCount == world,
                    "modal permits only the existing finite three-swap preparation to finish without a new world action");
                SupplyAutoPauseStep(context, input);
                Require(!(bool)Get(preparation, "CanClickNow"), "Ready is not an inventory cleanup phase");
                Call(preparation, "RequestReturn");
                for (int i = 0; i < 20 && (bool)Get(preparation, "HasWork"); i++) SupplyAutoPauseStep(context, input);
                Require(!(bool)Get(preparation, "HasWork") && (long)Get(preparation, "NativeClicks") == before + 6 &&
                    ReferenceEquals(player.inventory[12], material) && ReferenceEquals(player.inventory[54], originalPriority) &&
                    Main.mouseItem.IsAir && !Main.playerInventory && material.stack == 2 && originalPriority.stack == 7,
                    "modal permits exact inverse native clicks to return all real endpoint identities");
                Call(preparation, "TryPrepare", args);
                Require(!(bool)Get(preparation, "HasWork"), "the continuation permission cannot start another preparation after cleanup");
            }
            finally
            {
                Call(input, "ReturnExternalModal", request);
                Main.autoPause = oldAutoPause; Main.gamePaused = oldPaused;
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            }
        }
    }
}
