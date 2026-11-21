using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private bool ActorTileReachReady(Player body)
        {
            return body != null && !body.dead &&
                (ReferenceEquals(body, owner) || Owned(body) is Actor actor && !actor.TileReachPending);
        }

        private void InvalidateActorTileReach(Player body)
        {
            if (Owned(body) is Actor actor) actor.TileReachPending = true;
        }

        private TileReachCheckSettings ActorTileReach(Player body)
        {
            var reach = TileReachCheckSettings.Simple;
            if (ReferenceEquals(body, owner)) return reach;

            // Terraria updates static tileRange only for its local player.
            // Reconstruct the requested body's Simple range from the same
            // Journey permission and the result of its native equipment pass.
            int x = Player.DefaultTileRangeX, y = Player.DefaultTileRangeY;
            if (Main.IsJourneyMode)
            {
                var power = CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>();
                if (power.GetIsUnlocked() && power.IsEnabledForPlayer(body.whoAmI))
                {
                    x = x * 2 + 8;
                    y = y * 2 + 8;
                }
            }
            if (body.equippedAnyTileRangeAcc)
            {
                x += 3;
                y += 2;
            }

            // Vanilla applies its overrides after the Simple cap.
            reach.OverrideXReach = Math.Min(x, reach.TileReachLimit.Value);
            reach.OverrideYReach = Math.Min(y, reach.TileReachLimit.Value);
            return reach;
        }
    }
}
