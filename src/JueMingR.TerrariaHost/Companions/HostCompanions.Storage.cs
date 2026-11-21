using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class BankAccess
        {
            internal Guid Profile;
            internal CompanionInventoryArea Area;
            internal Actor Actor;
            internal Player Body;
            internal long Generation, Session;
            internal object World;
            internal Rectangle Tiles;
            internal short FrameX, FrameY;
            internal Projectile Provider;
            internal TrackedProjectileReference Tracking;
        }

        private Guid itemStorageProfile;
        private CompanionInventoryArea itemStorageArea;
        private BankAccess bankAccess;
        private uint nextBankSearch;

        internal void SetItemStorage(Guid id, CompanionInventoryArea area)
        {
            if (itemStorageProfile == id && itemStorageArea == area) return;
            CancelItemGesture();
            itemStorageProfile = id;
            itemStorageArea = area;
            bankAccess = null;
            nextBankSearch = 0;
        }

        internal bool StorageAvailable(Guid id, CompanionInventoryArea area, out string reason)
        {
            reason = null;
            if (!ItemOwnerCurrent())
            { reason = "主人当前不能整理物品。"; return false; }
            if (!actors.TryGetValue(id, out var actor))
            { reason = "队友未加入，无法操作储存。"; return false; }
            if (!ItemActorCurrent(id, actor))
            {
                reason = actor.Body?.dead == true ? "队友已死亡，暂时无法操作储存。" :
                    actor.Closing ? "队友正在收回。" : "队友尚未就绪，暂时无法操作储存。";
                return false;
            }
            if (!CompanionResources.IsBank(area)) return area == CompanionInventoryArea.Inventory;
            if (area == CompanionInventoryArea.VoidVault && actor.Body.disableVoidBag >= 0)
            { reason = "正在等待虚空储存的同步确认。"; return false; }
            if (!ActorTileReachReady(actor.Body))
            { reason = "正在刷新队友的交互范围。"; return false; }
            if (itemStorageProfile != id || itemStorageArea != area) SetItemStorage(id, area);
            if (BankCurrent(bankAccess, area)) return true;
            if (bankAccess != null)
            {
                bankAccess = null;
                nextBankSearch = 0;
                ValidateItemGesture();
            }
            uint now = Main.GameUpdateCount;
            if (nextBankSearch == 0 || unchecked((int)(now - nextBankSearch)) >= 0)
            {
                nextBankSearch = now + 12;
                bankAccess = SearchBank(actor, area);
            }
            if (bankAccess != null) return true;
            reason = MissingBank(area);
            return false;
        }

        internal bool OpenStorage(Guid id, CompanionInventoryArea area)
        {
            SetItemStorage(id, area);
            nextBankSearch = 0;
            if (StorageAvailable(id, area, out string reason)) return true;
            if (!actors.TryGetValue(id, out var actor) || !ItemActorCurrent(id, actor) || !ItemOwnerCurrent()) return Result(false, reason);
            if (reason != MissingBank(area) || area != CompanionInventoryArea.PiggyBank && area != CompanionInventoryArea.VoidVault)
                return Result(false, reason);
            var inventory = actor.Body.inventory;
            for (int slot = 0; slot < 50; slot++)
            {
                var item = inventory[slot];
                if (item == null || item.IsAir ||
                    (area == CompanionInventoryArea.PiggyBank ? item.type != ItemID.MoneyTrough :
                        item.type != ItemID.VoidLens && item.type != ItemID.ClosedVoidBag)) continue;
                if (TryQueueItemOnce(actor, slot, true, out reason)) return Result(true, reason);
            }
            return Result(false, reason);
        }

        private static string MissingBank(CompanionInventoryArea area)
        {
            switch (area)
            {
                case CompanionInventoryArea.PiggyBank: return "队友附近没有可用的猪猪存钱罐入口。";
                case CompanionInventoryArea.Safe: return "队友附近没有可用的保险箱。";
                case CompanionInventoryArea.DefenderForge: return "队友附近没有可用的护卫熔炉。";
                case CompanionInventoryArea.VoidVault: return "队友附近没有可用的虚空入口。";
                default: return "该储存区不可用。";
            }
        }

        private static int BankTile(CompanionInventoryArea area)
        {
            switch (area)
            {
                case CompanionInventoryArea.PiggyBank: return 29;
                case CompanionInventoryArea.Safe: return 97;
                case CompanionInventoryArea.DefenderForge: return 463;
                case CompanionInventoryArea.VoidVault: return 491;
                default: return -1;
            }
        }

        private bool BankCurrent(BankAccess access, CompanionInventoryArea area)
        {
            if (access == null || access.Area != area || access.Session != session || !ReferenceEquals(access.World, world) ||
                !actors.TryGetValue(access.Profile, out var actor) || !ReferenceEquals(actor, access.Actor) ||
                !ReferenceEquals(access.Body, actor.Body) || access.Generation != actor.Generation || !ItemActorCurrent(access.Profile, actor) ||
                !ActorTileReachReady(actor.Body) || area == CompanionInventoryArea.VoidVault && actor.Body.disableVoidBag >= 0) return false;
            var reach = ActorTileReach(actor.Body);
            if (access.Provider != null)
                return access.Tracking.Key.TryGetActive(out var actual) &&
                    ReferenceEquals(actual, access.Provider) && actual.type == access.Tracking.ProjectileType &&
                    BankProjectileInReach(actor.Body, reach, area, actual);
            return BankTileCurrent(access.Tiles, BankTile(area), access.FrameX, access.FrameY) &&
                BankHitboxInReach(actor.Body, reach, access.Tiles, area);
        }

        private BankAccess SearchBank(Actor actor, CompanionInventoryArea area)
        {
            var body = actor.Body;
            var reach = ActorTileReach(body);
            var access = new BankAccess { Profile = actor.Profile.Id, Actor = actor, Body = body, Generation = actor.Generation,
                Session = session, World = world, Area = area };
            reach.GetTileRegion(body, out int left, out int top, out int right, out int bottom);
            int type = BankTile(area);
            for (int x = Math.Max(0, left); x <= Math.Min(right, Main.maxTilesX - 1); x++)
                for (int y = Math.Max(0, top); y <= Math.Min(bottom, Main.maxTilesY - 1); y++)
                {
                    if (!WorldTileKnowledge.CellKnown(x, y)) continue;
                    var tile = Main.tile[x, y];
                    if (tile == null || !tile.active() || tile.type != type ||
                        !TileObjectData.TryGetTileBounds(x, y, out var tiles) ||
                        !WorldTileKnowledge.CellKnown(tiles.X, tiles.Y)) continue;
                    var origin = Main.tile[tiles.X, tiles.Y];
                    if (origin == null || !BankTileCurrent(tiles, type, origin.frameX, origin.frameY) ||
                        !BankHitboxInReach(body, reach, tiles, area)) continue;
                    access.Tiles = tiles;
                    access.FrameX = origin.frameX;
                    access.FrameY = origin.frameY;
                    return access;
                }
            if (area == CompanionInventoryArea.PiggyBank || area == CompanionInventoryArea.VoidVault)
                foreach (var projectile in Main.projectile)
                {
                    if (!BankProjectileInReach(body, reach, area, projectile)) continue;
                    access.Provider = projectile;
                    access.Tracking.Set(projectile);
                    return access;
                }
            return null;
        }

        // The native shape uses the full object, including the outer cells of
        // the forge/vault. Every tile must still belong to the original frame.
        private static bool BankTileCurrent(Rectangle tiles, int type, short frameX, short frameY)
        {
            int width = type == 463 || type == 491 ? 3 : 2;
            int height = type == 463 ? 4 : type == 491 ? 3 : type == 29 ? 1 : 2;
            if (tiles.Width != width || tiles.Height != height ||
                !WorldTileKnowledge.RegionKnown(tiles.Left, tiles.Top, tiles.Right, tiles.Bottom)) return false;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var tile = Main.tile[tiles.X + x, tiles.Y + y];
                    if (tile == null || !tile.active() || tile.type != type ||
                        tile.frameX != frameX + x * 18 || tile.frameY != frameY + y * 18) return false;
                }
            return true;
        }

        private static bool BankHitboxInReach(Player body, TileReachCheckSettings reach, Rectangle tiles, CompanionInventoryArea area)
        {
            // Tile491 is physically three rows, but HandleBeingInChestRange
            // uses the same four-row hitbox as the defender's forge.
            int height = area == CompanionInventoryArea.VoidVault ? 4 : tiles.Height;
            var hitbox = new Rectangle(tiles.X * 16, tiles.Y * 16, tiles.Width * 16, height * 16);
            hitbox.Inflate(-1, -1);
            return HitboxInReach(body, reach, hitbox);
        }

        private static bool BankProjectileInReach(Player body, TileReachCheckSettings reach, CompanionInventoryArea area, Projectile projectile)
        {
            return projectile != null && projectile.active && projectile.TryGetContainerIndex(out int index) &&
                index == (area == CompanionInventoryArea.PiggyBank ? -2 : -5) &&
                projectile.IsInteractable() && HitboxInReach(body, reach, projectile.Hitbox);
        }

        private static bool HitboxInReach(Player body, TileReachCheckSettings reach, Rectangle hitbox)
        {
            var point = hitbox.ClosestPointInRect(body.Center).ToTileCoordinates();
            return body.IsInTileInteractionRange(point.X, point.Y, reach);
        }
    }
}
