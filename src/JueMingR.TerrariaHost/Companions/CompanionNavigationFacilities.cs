using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionNavigationFacilities
    {
        internal static bool HasNearbyTrack(Vector2 feet, NavigationBudget budget)
        {
            var tile = feet.ToTileCoordinates();
            for (int y = tile.Y - 4; y <= tile.Y; y++)
                for (int x = tile.X - 1; x <= tile.X + 1; x++)
                {
                    if (!budget.TryTiles()) return false;
                    if (CompanionNativeContact.Loaded(x, y) && Main.tile[x, y].nactive() &&
                        Main.tile[x, y].type == TileID.MinecartTrack) return true;
                }
            return false;
        }

        internal static void Collect(Player body, CompanionMobilitySnapshot state, Vector2 feet, Vector2 target,
            NavigationBudget budget, List<NavigationTransition> links)
        {
            float distance = Vector2.DistanceSquared(feet, target);
            if (distance < 160f * 160f || state.Controlled) return;
            CompanionMobilityGeometry.ReadPylonLinks(body, target, budget, links);
            CompanionMobilityGeometry.ReadPortalLinks(body, target, budget, links);
            CompanionMobilityWiring.ReadLinks(body, target, budget, links);

            if (body.gravDir < 0 || !state.CanUseRod || state.RodSlot < 0 || distance > 500f * 500f ||
                !budget.TryTiles(256)) return;
            var pose = CompanionNativeContact.TopLeft(body, target);
            if (CompanionNativeContact.Supported(body, pose) && CompanionMobilityGeometry.RodAim(body, target, false, out _))
                links.Add(new NavigationTransition(NavigationAction.Facility, feet, target,
                    irreversible: true, detail: NavigationTraversalDetail.Rod));
        }
    }
}
