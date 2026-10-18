using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.View
{
    // RequestSection is the native client's single-section protocol. It does
    // not activate distant server simulation or prove any entity was synced.
    internal sealed class ViewSectionRequests
    {
        private struct Pending { internal bool Used; internal int X, Y; internal ulong Sent; }
        private readonly Pending[] pending = new Pending[16];
        private readonly Action<string> feedback;
        private bool timedOut, hasPending;
        internal ViewSectionRequests(Action<string> feedback) { this.feedback = feedback; }
        internal void Clear() { if (hasPending) Array.Clear(pending, 0, pending.Length); timedOut = hasPending = false; }
        internal void Update(ulong tick, bool expanded)
        {
            if (!expanded || Main.netMode != 1) { Clear(); return; }
            if (timedOut || Main.mapFullscreen || Main.inFancyUI || Main.onlyDrawFancyUI || Main.sectionManager == null || Netplay.Connection.State < 10) return;
            Vector2 p = Main.Camera.ScaledPosition, size = Main.Camera.ScaledSize;
            int left = Netplay.GetSectionX(Math.Max(0, (int)Math.Floor(p.X / 16f) - 2));
            int top = Netplay.GetSectionY(Math.Max(0, (int)Math.Floor(p.Y / 16f) - 2));
            int right = Netplay.GetSectionX(Math.Min(Main.maxTilesX - 1, (int)Math.Ceiling((p.X + size.X) / 16f) + 2));
            int bottom = Netplay.GetSectionY(Math.Min(Main.maxTilesY - 1, (int)Math.Ceiling((p.Y + size.Y) / 16f) + 2));
            int free = -1;
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Used && Main.sectionManager.SectionLoaded(pending[i].X, pending[i].Y)) pending[i].Used = false;
                // Already-sent TCP bytes cannot be recalled. Retiring a view's
                // local tracking does release its bounded slot and timeout;
                // old requests must not starve a newly visible cold section.
                if (pending[i].Used && (pending[i].X < left || pending[i].X > right || pending[i].Y < top || pending[i].Y > bottom)) pending[i].Used = false;
                if (pending[i].Used && tick - pending[i].Sent >= 300)
                {
                    timedOut = true; feedback("远处区域尚未收到，已停止额外请求；调回普通视野后可重试。"); return;
                }
                if (!pending[i].Used) free = i;
            }
            if (free < 0) return;
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
            {
                if (Main.sectionManager.SectionLoaded(x, y)) continue;
                bool sent = false;
                for (int i = 0; i < pending.Length; i++) if (pending[i].Used && pending[i].X == x && pending[i].Y == y) { sent = true; break; }
                if (sent) continue;
                // At most one new native packet per completed Update, with
                // sixteen outstanding slots. The next Update uses the latest
                // actual camera; no stale unsent viewport queue survives it.
                try { NetMessage.SendData(MessageID.RequestSection, -1, -1, null, x, y); }
                catch { timedOut = true; feedback("远处区域请求失败，已停止额外请求；调回普通视野后可重试。"); return; }
                pending[free] = new Pending { Used = true, X = x, Y = y, Sent = tick };
                hasPending = true;
                return;
            }
        }
    }
}
