using System;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private bool ReadBuildCell(int x, int y, out BlueprintWorldMatchSample sample)
        {
#if DEBUG
            BuildEligibilityReads++;
#endif
            sample = default(BlueprintWorldMatchSample);
            return projectionMatchSource != null && projectionMatchSource.TryRead(x, y, out sample) && sample.Available;
        }
        private bool PreflightBuildObject(BlueprintBuildTarget target, Player p, Item item, int direction)
        {
            // Framing.GetTileSafely inside CanPlace fills null entries. Prove
            // all alternate footprints and real anchor extents readable first;
            // preview queries never create a Tile or assume an absent section.
            var root = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            if (root == null) return false;
            int left = target.Bounds.Left, right = target.Bounds.Right, top = target.Bounds.Top, bottom = target.Bounds.Bottom;
            bool global = false;
            for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
            {
                var data = TileObjectData.GetTileData(item.createTile, item.placeStyle, alternate);
                if (data == null) return false;
                int x = target.WorldX - data.Origin.X, y = target.WorldY - data.Origin.Y;
                left = Math.Min(left, x - 1); right = Math.Max(right, x + data.Width + 1);
                top = Math.Min(top, y - 1); bottom = Math.Max(bottom, y + data.Height + 1);
                left = Math.Min(left, x + Math.Min(data.AnchorTop.checkStart, data.AnchorBottom.checkStart));
                right = Math.Max(right, x + Math.Max(data.AnchorTop.checkStart + data.AnchorTop.tileCount, data.AnchorBottom.checkStart + data.AnchorBottom.tileCount));
                top = Math.Min(top, y + Math.Min(data.AnchorLeft.checkStart, data.AnchorRight.checkStart) - 1);
                bottom = Math.Max(bottom, y + Math.Max(data.AnchorLeft.checkStart + data.AnchorLeft.tileCount, data.AnchorRight.checkStart + data.AnchorRight.tileCount) + 1);
                var hook = data.HookCheckIfCanPlace.hook;
                if (hook != null)
                {
                    var method = hook.Method;
                    if (method.DeclaringType == typeof(Chest) && method.Name == nameof(Chest.FindEmptyChest) ||
                        method.DeclaringType == typeof(Terraria.GameContent.Tile_Entities.TETeleportationPylon) && method.Name == "PlacementPreviewHook_CheckIfCanPlace") global = true;
                    else if (method.DeclaringType != typeof(WorldGen) || method.Name != "PlantCheck_CanPlaceHook" && method.Name != "CanPlaceProjectilePressurePad") return false;
                }
            }
            // The original Pigronata post-gate names clickX-2 explicitly;
            // cover it even if a future alternate changes the metadata origin.
            if (item.createTile == 454) { left = Math.Min(left, target.WorldX - 2); right = Math.Max(right, target.WorldX + 2); }
            for (int x = left; x < right; x++) for (int y = top; y < bottom; y++)
            {
                BlueprintWorldMatchSample sample;
                if (!ReadBuildCell(x, y, out sample)) return false;
                if (x >= target.Bounds.Left && x < target.Bounds.Right && y >= target.Bounds.Top && y < target.Bounds.Bottom &&
                    (sample.Cell.Layers & CaptureLayer.Tile) != 0) return false;
            }
            BlueprintWorldMatchSample point;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out point)) return false;
            var raw = Main.tile[target.WorldX, target.WorldY];
            if (raw.liquid > 0 && raw.lava() && (Main.tileSolid[item.createTile] || !TileObjectData.CheckLiquidPlacement(item.createTile, item.placeStyle, raw))) return false;
            if (item.createTile == TileID.Torches)
            {
                if (!PreflightLegacyLiquid(target, raw, item)) return false;
                // This one legacy branch does not call custom CanPlace. Reuse
                // the exact captured-form predicate also used at PlaceIt.
                return NativeLegacyBuildChoice(new BuildFlight { Target = target, Player = p, Source = SnapshotSource(0, item) }, item.createTile, true);
            }
            if (item.createTile == 82 || item.createTile == 227)
                return BlueprintPlacementQuery.LegacyPlant(target.WorldX, target.WorldY, item.createTile, item.placeStyle) &&
                    NativeLegacyBuildChoice(new BuildFlight { Target = target, Player = p, Source = SnapshotSource(0, item) }, item.createTile);
            TileObject choice;
            if (!BlueprintPlacementQuery.CanPlace(target.WorldX, target.WorldY, item.createTile, item.placeStyle, direction, out choice))
            { if (global) RequestBuildEligibilityRetry(target); return false; }
            if (choice.xCoord != target.Bounds.Left || choice.yCoord != target.Bounds.Top) return false;
            var chosen = TileObjectData.GetTileData(choice.type, choice.style, choice.alternate);
            if (chosen == null || chosen.Width != target.Bounds.Width || chosen.Height != target.Bounds.Height) return false;
            // The copied static gates read private query coordinates, never
            // borrow the player's live aim during preparation.
            if (!BlueprintPlacementQuery.PostConditions(choice, target.WorldX, target.WorldY)) return false;
            if (BuildObjectPlayerBlocked(choice)) { WaitForDynamicBuild(target); return false; }
            if (TileID.Sets.IsAContainer[item.createTile] && Chest.FindEmptyChest(choice.xCoord, choice.yCoord) < 0)
            { RequestBuildEligibilityRetry(target); return false; }
            return true;
        }
        private static bool BuildObjectPlayerBlocked(TileObject choice)
        {
            int width = 0, height = 0;
            switch (choice.type)
            {
                case 138: case 664: case 711: case 712: case 713: case 714: case 715: case 716: case 484: width = height = 32; break;
                case 235: width = 48; height = 16; break;
                case 476: width = height = 16; break;
                case 387: width = 32; height = 16; break;
            }
            if (width == 0) return false;
            var bounds = new Rectangle(choice.xCoord * 16, choice.yCoord * 16, width, height);
            for (int i = 0; i < 255; i++) { var player = Main.player[i]; if (player.active && !player.dead && !player.ghost && player.Hitbox.Intersects(bounds)) return true; }
            return false;
        }
        private bool PreflightBuildOrdinary(BlueprintBuildTarget target, Item item)
        {
            BlueprintWorldMatchSample sample;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out sample)) return false;
            var raw = Main.tile[target.WorldX, target.WorldY];
            if (raw.liquid > 0 && raw.lava() && (Main.tileSolid[item.createTile] || !TileObjectData.CheckLiquidPlacement(item.createTile, item.placeStyle, raw))) return false;
            if (!PreflightLegacyLiquid(target, raw, item)) return false;
            // WorldGen.PlaceTile calls this entity test only for solid tiles.
            // Walls, non-solid rope and custom furniture do not inherit it.
            if (Main.tileSolid[item.createTile] && !Collision.EmptyTile(target.WorldX, target.WorldY))
            { WaitForDynamicBuild(target); return false; }
            return true;
        }
        private bool PreflightLegacyLiquid(BlueprintBuildTarget target, Tile raw, Item item)
        {
            if (raw.liquid == 0 && !raw.checkingLiquid()) return true;
            bool allowed = true;
            if (TileID.Sets.Torches[item.createTile])
            {
                allowed = item.createTile != TileID.Torches || item.placeStyle == 8 || item.placeStyle == 11 || item.placeStyle == 17;
                var data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
                allowed &= data == null || data.WaterPlacement == Terraria.Enums.LiquidPlacement.Allowed;
            }
            else switch (item.createTile)
            {
                case 3: case 20: case 24: case 27: case 32: case 51: case 69: case 72: case 201:
                case 352: case 529: case 624: case 700: case 637: case 656: case 701: case 697: allowed = false; break;
            }
            // WorldGen's legacy writer tests this transient bit as well as
            // liquid amount. It has no SupportStamp event: use the existing
            // bounded transient retry, not a native no-spend rejection's long
            // deadline, rather than leave a dry target dormant.
            if (!allowed && raw.checkingLiquid()) WaitForDynamicBuild(target);
            return allowed;
        }
    }
}
