using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    // At most four personal accounts. This is a local discovery cursor, never
    // a world index. Incremental absence is NOT a proof for releasing intent.
    internal sealed class CoinRange
    {
        internal struct Entrance
        {
            internal Chest Bank;
            internal int Kind, Projectile, X, Y;
            internal bool Valid(Player player, bool voidClosed)
            {
                if (Kind == 3 && voidClosed || !ReferenceEquals(Bank, Account(player, Kind))) return false;
                Rectangle area = Area(player);
                if (Projectile >= 0)
                {
                    if (Main.projectile == null || Projectile >= Main.projectile.Length) return false;
                    var p = Main.projectile[Projectile]; int container;
                    return p != null && p.active && p.TryGetContainerIndex(out container) && container == -2 - Kind &&
                        area.Contains(p.Hitbox.ClosestPointInRect(player.Center).ToTileCoordinates());
                }
                return area.Contains(X, Y) && WorldGen.InWorld(X, Y) && Main.tile != null && Main.tile[X, Y] != null && TileKind(Main.tile[X, Y].type) == Kind;
            }
        }
        private readonly Entrance[] building = new Entrance[4];
        internal readonly Entrance[] Banks = new Entrance[4];
        internal int Count { get; private set; }
        internal long Revision { get; private set; }
        private int cursor, count;
        private Rectangle bounds;
        private Player owner;
        private bool closed, invalid;
#if DEBUG
        internal long ProjectileReads, TileReads, WitnessReads, CompleteQueries;
#endif
        internal static Chest Account(Player p, int kind) { return kind == 0 ? p.bank : kind == 1 ? p.bank2 : kind == 2 ? p.bank3 : kind == 3 ? p.bank4 : null; }
        private static int TileKind(int type) { return type == 29 ? 0 : type == 97 ? 1 : type == 463 ? 2 : type == 491 ? 3 : -1; }
        private static Rectangle Area(Player p) { var c = p.Center.ToTileCoordinates(); return new Rectangle(c.X - 39, c.Y - 39, 79, 79); }
        internal void Clear()
        { owner = null; cursor = count = Count = 0; Array.Clear(Banks, 0, 4); Array.Clear(building, 0, 4); Revision++; }
        internal bool HasWitness(Player p, bool voidClosed)
        {
            for (int i = 0; i < Count; i++)
            {
#if DEBUG
                WitnessReads++;
#endif
                if (Banks[i].Valid(p, voidClosed)) return true;
            }
            return false;
        }
        internal void Step(Player p, bool voidClosed)
        {
            var area = Area(p);
            if (!ReferenceEquals(owner, p) || Math.Abs(bounds.Center.X - area.Center.X) >= 79 || Math.Abs(bounds.Center.Y - area.Center.Y) >= 79 || closed != voidClosed)
            { owner = p; bounds = area; closed = voidClosed; cursor = count = 0; invalid = false; Array.Clear(building, 0, 4); }
            // Fixed work, independent of pickup rate. A quiet no-target/full
            // state still discovers furniture/projectile changes within a sweep.
            for (int work = 0; work < 128 && cursor < 7241; work++, cursor++) Read(p, cursor);
            if (cursor < 7241) return;
            if (!invalid)
            {
                int valid = 0;
                for (int i = 0; i < count; i++) if (building[i].Valid(p, closed)) building[valid++] = building[i];
                bool changed = valid != Count;
                for (int i = 0; !changed && i < valid; i++) changed = !Same(Banks[i], building[i]);
                Count = valid; Array.Copy(building, Banks, valid);
                for (int i = valid; i < 4; i++) Banks[i] = default(Entrance);
                if (changed) Revision++;
            }
            cursor = count = 0; invalid = false; bounds = area; Array.Clear(building, 0, 4);
        }
        // Only called when protection has no remaining positive witness. All
        // 7,241 possible entries are observed in this synchronous phase; failure
        // retains protection. Unlike periodic background work this is an edge.
        internal bool TryComplete(Player p, bool voidClosed, out bool any)
        {
#if DEBUG
            CompleteQueries++;
#endif
            any = false; owner = p; bounds = Area(p); closed = voidClosed;
            cursor = count = 0; invalid = false; Array.Clear(building, 0, 4);
            try
            {
                for (int i = 0; i < 7241; i++) Read(p, i);
                if (invalid) return false;
                bool changed = Count != count;
                for (int i = 0; !changed && i < count; i++) changed = !Same(Banks[i], building[i]);
                Count = count; Array.Copy(building, Banks, count);
                for (int i = count; i < 4; i++) Banks[i] = default(Entrance);
                if (changed) Revision++; any = Count > 0; return true;
            }
            catch { return false; }
            finally { cursor = count = 0; Array.Clear(building, 0, 4); }
        }
        private void Read(Player p, int index)
        {
            int kind = -1; var entrance = new Entrance { Projectile = -1 };
            if (index < 1000)
            {
#if DEBUG
                ProjectileReads++;
#endif
                if (Main.projectile == null || Main.projectile.Length < 1000) { invalid = true; return; }
                var projectile = Main.projectile[index];
                if (projectile == null) { invalid = true; return; }
                int container;
                if (!projectile.active || !projectile.TryGetContainerIndex(out container) ||
                    !bounds.Contains(projectile.Hitbox.ClosestPointInRect(p.Center).ToTileCoordinates())) return;
                kind = -2 - container; entrance.Projectile = index;
            }
            else
            {
                int tile = index - 1000, x = bounds.Left + tile / 79, y = bounds.Top + tile % 79;
                if (!WorldGen.InWorld(x, y)) return;
#if DEBUG
                TileReads++;
#endif
                if (Main.tile == null || Main.tile[x, y] == null) { invalid = true; return; }
                kind = TileKind(Main.tile[x, y].type); entrance.X = x; entrance.Y = y;
            }
            if (kind < 0 || kind > 3 || kind == 3 && closed) return;
            Chest bank = Account(p, kind);
            if (bank == null || bank.item == null) { invalid = true; return; }
            for (int i = 0; i < count; i++) if (ReferenceEquals(building[i].Bank, bank)) return;
            entrance.Bank = bank; entrance.Kind = kind; building[count++] = entrance;
        }
        private static bool Same(Entrance a, Entrance b)
        { return ReferenceEquals(a.Bank, b.Bank) && a.Kind == b.Kind && a.Projectile == b.Projectile && a.X == b.X && a.Y == b.Y; }
    }
}
