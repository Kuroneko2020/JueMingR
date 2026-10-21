using System;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Movement
{
    internal static class SmartFallGeometry
    {
        internal const int Horizon = 48;
        // Read-only contact band: no Collision static flags, missing Tile
        // creation, live clone, or complete Player.Update simulation.
        internal static SmartFallContact Contact(Player p, ref long reads)
        { return Contact(p.position, p.velocity, p.width, p.height, p.gravity, p.maxFallSpeed, p.gravDir, p.controlDown, ref reads); }
        internal static SmartFallContact Contact(Player p, ref long reads, SmartFallContactBand band)
        {
            band.Begin();
            var contact = Contact(p.position,p.velocity,p.width,p.height,p.gravity,p.maxFallSpeed,p.gravDir,p.controlDown,ref reads,band);
            // An out-of-band trajectory cannot certify a cached contact. Keep
            // it unknown until actual motion/source prerequisites change,
            // rather than rerunning an overflowing sweep every idle snapshot.
            return band.Complete ? contact : default(SmartFallContact);
        }

        // A full-width, level support strip certifies a short safe landing
        // independently of the exact horizontal braking curve. Earlier body
        // contacts can only shorten this already safe fall. Re-read the finite
        // reachable strip so a hole/Down/new reach cannot preserve stale safety;
        // slopes, half bricks and a wider/unmodelled reach use the normal sweep.
        internal static bool SafeSupport(Player p, SmartFallContact contact, ref long reads)
        {
            if (!contact.Found || contact.SafeSurface || contact.Slope != 0 || contact.HalfBrick) return false;
            // Only ordinary running is bounded by the native run-speed fields;
            // mounts, a possible dash, pulley and hooks keep exact-path timing.
            if (p.mount.Active || p.dashType != 0 || p.pulley || p.grapCount > 0) return false;
            if (contact.Platform && (p.controlDown || p.gravDir < 0 || p.mount.Active || p.pulley || p.grapCount > 0)) return false;
            float plane = p.gravDir > 0 ? contact.Y + p.height : contact.Y;
            float remaining = (plane - (p.gravDir > 0 ? p.position.Y + p.height : p.position.Y)) * p.gravDir;
            if (remaining < -.01f) return false;
            float speed = p.velocity.Y * p.gravDir; int ticks = 0;
            do
            {
                speed = Math.Min(p.maxFallSpeed, speed + Math.Abs(p.gravity));
                if (speed <= 0) return false;
                remaining -= speed; ticks++;
            } while (remaining > 0 && ticks < Horizon);
            if (remaining > 0) return false;
            float reach = Math.Max(Math.Abs(p.velocity.X), Math.Max(p.maxRunSpeed, p.accRunSpeed)) * ticks;
            int left = (int)Math.Floor((p.position.X - reach) / 16);
            int right = (int)Math.Floor((p.position.X + p.width + reach - .001f) / 16);
            if (right - left >= 64) return false;
            for (int x = left; x <= right; x++)
            {
                Tile tile = Read(x, contact.TileY, ref reads);
                if (tile == null || !tile.nactive() || !Main.tileSolid[tile.type] || tile.slope() != 0 || tile.halfBrick()) return false;
                if (Main.tileSolidTop[tile.type] && (p.gravDir < 0 || p.controlDown || p.mount.Active || p.pulley || p.grapCount > 0 || tile.frameY != 0)) return false;
            }
            return true;
        }

        internal static SmartFallContact Contact(Vector2 position, Vector2 velocity, int width, int height,
            float gravity, float maxFall, float direction, bool down, ref long reads, SmartFallContactBand band = null)
        {
            var contact = default(SmartFallContact);
            float x = position.X, y = position.Y, vy = velocity.Y, vx = velocity.X;
            for (int tick = 0; tick < Horizon; tick++)
            {
                vy = direction * Math.Min(maxFall, vy * direction + Math.Abs(gravity));
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(vx), Math.Abs(vy)) / 4));
                if (steps > 32) return contact; // unmodelled portal/high-speed motion
                float dx = vx / steps, dy = vy / steps;
                for (int step = 0; step < steps; step++)
                {
                    float nx = x + dx, ny = y + dy;
                    if (dx != 0)
                    {
                        int side = (int)Math.Floor((dx > 0 ? nx + width - .001f : nx) / 16);
                        for (int row = (int)Math.Floor((y + .001f) / 16); row <= (int)Math.Floor((y + height - .001f) / 16); row++)
                        {
                            var wall = Read(side, row, ref reads);
                            band?.Observe(side,row,wall,tick);
                            if (wall == null || !wall.nactive() || !Main.tileSolid[wall.type] || Main.tileSolidTop[wall.type] || wall.slope() != 0) continue;
                            float top = row * 16 + (wall.halfBrick() ? 8 : 0);
                            if (y + height <= top || y >= row * 16 + 16) continue;
                            nx = dx > 0 ? side * 16 - width : side * 16 + 16;
                            vx = dx = 0; break;
                        }
                    }
                    float oldEdge = direction > 0 ? y + height : y;
                    float edge = direction > 0 ? ny + height : ny;
                    int minX = (int)Math.Floor(Math.Min(x, nx) / 16), maxX = (int)Math.Floor((Math.Max(x, nx) + width - .001f) / 16);
                    int minY = (int)Math.Floor(Math.Min(oldEdge, edge) / 16) - 1, maxY = (int)Math.Floor(Math.Max(oldEdge, edge) / 16) + 1;
                    float nearest = direction > 0 ? float.MaxValue : float.MinValue;
                    for (int tx = minX; tx <= maxX; tx++) for (int ty = minY; ty <= maxY; ty++)
                    {
                        Tile tile = Read(tx, ty, ref reads);
                        band?.Observe(tx,ty,tile,tick);
                        if (tile == null || !tile.nactive()) continue;
                        bool platform = Main.tileSolidTop[tile.type];
                        if (!Main.tileSolid[tile.type] && (!platform || tile.frameY != 0) || platform && (down || direction < 0)) continue;
                        if (nx + width <= tx * 16 || nx >= tx * 16 + 16) continue;
                        byte slope = tile.slope();
                        float surface = SmartFallDecision.Surface(tx, ty, slope, tile.halfBrick(), nx, nx + width, direction);
                        // Width-leading crossing is also tested at the old X:
                        // an uphill front foot can contact before the centre.
                        float previous = SmartFallDecision.Surface(tx, ty, slope, tile.halfBrick(), x, x + width, direction);
                        if ((oldEdge - previous) * direction > 4.01f || (edge - surface) * direction < 0) continue;
                        if (direction > 0 ? surface >= nearest : surface <= nearest) continue;
                        nearest = surface;
                        contact = new SmartFallContact { Found = true, Platform = platform,
                            SafeSurface = TileID.Sets.Clouds[tile.type] || tile.type == 666 || tile.type == 19 && tile.frameY / 18 == 49,
                            X = nx, Y = direction > 0 ? surface - height : surface,
                            Ticks = tick + (step + 1f) / steps, TileX = tx, TileY = ty, Slope = slope,
                            Type = tile.type, FrameY = tile.frameY, HalfBrick = tile.halfBrick() };
                    }
                    if (contact.Found) return contact;
                    x = nx; y = ny;
                }
            }
            return contact;
        }
        internal static Tile Read(int x, int y, ref long reads)
        {
#if DEBUG
            reads++;
#endif
            if (Main.tile == null || x < 1 || y < 1 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 40) return null;
            return Main.tile[x, y];
        }
        internal static bool Empty(Vector2 position, int width, int height, ref long reads)
        {
            int left = (int)Math.Floor(position.X / 16), right = (int)Math.Floor((position.X + width - .001f) / 16);
            int top = (int)Math.Floor(position.Y / 16), bottom = (int)Math.Floor((position.Y + height - .001f) / 16);
            if (left < 1 || top < 1 || right >= Main.maxTilesX - 1 || bottom >= Main.maxTilesY - 40) return false;
            for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++)
            { var t = Read(x, y, ref reads); if (t == null || t.liquid > 0 || t.nactive() && Main.tileSolid[t.type] && !Main.tileSolidTop[t.type]) return false; }
            return true;
        }
    }
    // The cached sweep remembers only cells actually queried by its finite
    // foot/leading-body corridor. Re-reading their collision facts is not a
    // new sweep or plan. It detects a new closer surface, including an empty
    // cell becoming active, without a global terrain hook/world revision.
    internal sealed class SmartFallContactBand
    {
        private struct Cell { internal long Key; internal ulong Fact; internal int Stamp; }
        private readonly Cell[] cells=new Cell[4096];
        private readonly int[] occupied=new int[4096];
        private readonly bool skipPassed, liquid;
        private int stamp,count;
        private bool complete;
        internal bool Complete { get { return complete; } }
        internal SmartFallContactBand(bool skipPassed = true, bool liquid = false)
        { this.skipPassed = skipPassed; this.liquid = liquid; }
        internal void Begin(){if(++stamp==0){Array.Clear(cells,0,cells.Length);stamp=1;}count=0;complete=true;}
        private ulong Fact(Tile tile)
        { return tile == null ? 0 : (liquid ? (ulong)tile.liquid << 48 : 0) | (!tile.nactive()?0:1UL | ((ulong)tile.type<<32) | ((ulong)(ushort)tile.frameY<<8) | ((ulong)tile.slope()<<2) | (tile.halfBrick()?2UL:0)); }
        internal void Observe(int x,int y,Tile tile,int tick)
        {
            long key=((long)x<<32)|(uint)y;int slot=(int)(((uint)x*73856093U^(uint)y*19349663U)&4095);
            for(int probe=0;probe<cells.Length;probe++,slot=(slot+1)&4095)
            {
                if(cells[slot].Stamp!=stamp)
                {cells[slot]=new Cell{Key=key,Fact=Fact(tile),Stamp=stamp};occupied[count++]=slot;return;}
                if(cells[slot].Key==key)return;
            }
            complete=false;
        }
        internal bool Changed(Player player,ref long reads)
        {
            if(!complete)return false;
            for(int i=0;i<count;i++)
            {
                Cell cell=cells[occupied[i]];
                int x=(int)(cell.Key>>32),y=(int)cell.Key;
                if(skipPassed && (player.gravDir>0?y*16+16<player.position.Y:y*16>player.position.Y+player.height))continue;
                if(Fact(SmartFallGeometry.Read(x,y,ref reads))!=cell.Fact)return true;
            }
            return false;
        }
    }
}
