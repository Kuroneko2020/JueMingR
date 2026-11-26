using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // One native-entity snapshot and spatial index per world tick for the whole
    // team. Actor queries visit nearby bins; they do not rescan every projectile.
    // This is linear body/swept-box avoidance, not future Boss-specific geometry.
    internal static partial class CompanionThreatField
    {
        private const int Cell = 256;
        private struct Threat
        {
            internal Rectangle Bounds, Contact;
            internal Vector2 Center;
            internal NPC Enemy;
            internal Projectile Shot;
            internal int Identity;
        }
        private static readonly Dictionary<long, List<int>> bins = new Dictionary<long, List<int>>();
        private static readonly Stack<List<int>> free = new Stack<List<int>>();
        private static readonly List<int> large = new List<int>();
        private static Threat[] threats = new Threat[0];
        private static uint tick;
        private static object world;
        private static int count;
        internal static long SnapshotBuilds { get; private set; }
        internal static void Reset()
        {
            ResetIndex();
            Array.Clear(threats, 0, threats.Length);
            if (shotSources != null) Array.Clear(shotSources, 0, shotSources.Length);
        }

        private static void ResetIndex()
        {
            foreach (var list in bins.Values) { list.Clear(); free.Push(list); }
            bins.Clear(); large.Clear(); count = 0; world = null;
        }
        private static void Ensure()
        {
            if (ReferenceEquals(world, Main.ActiveWorldFileData) && tick == Main.GameUpdateCount) return;
            ResetIndex(); world = Main.ActiveWorldFileData; tick = Main.GameUpdateCount;
            SnapshotBuilds++;
            int capacity = Main.projectile.Length + Main.npc.Length;
            if (threats.Length < capacity) threats = new Threat[capacity];
            foreach (var projectile in Main.projectile)
                if (projectile != null && projectile.active && projectile.hostile && projectile.damage > 0)
                    Add(ObserveProjectileContact(projectile), projectile.velocity * 10, null, projectile, (int)projectile.key);
            foreach (var npc in Main.npc)
                if (npc != null && npc.active && !npc.friendly && !npc.townNPC && npc.damage > 0)
                    Add(npc.Hitbox, npc.velocity * 8, npc, null, npc.generation);
        }
        private static void Add(Rectangle current, Vector2 future, NPC enemy, Projectile shot, int identity)
        {
            var next = current; next.Offset((int)future.X, (int)future.Y);
            Rectangle swept = Rectangle.Union(current, next); swept.Inflate(8, 8);
            int index = count++; threats[index] = new Threat { Bounds = swept, Contact = current, Center = current.Center.ToVector2(), Enemy = enemy, Shot = shot, Identity = identity };
            // Ordinary movement keeps its original bounds. Fishing contact
            // can also use Terraria's interpolated NPC position in this index.
            var coverage = swept;
            if (enemy != null && enemy.netOffset != Vector2.Zero)
            {
                coverage.Offset((int)enemy.netOffset.X, (int)enemy.netOffset.Y);
                coverage = Rectangle.Union(coverage, swept);
            }
            int left = coverage.Left / Cell, right = coverage.Right / Cell, top = coverage.Top / Cell, bottom = coverage.Bottom / Cell;
            if ((right - left + 1) * (bottom - top + 1) > 16) { large.Add(index); return; }
            for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++)
            {
                long key = Key(x, y); List<int> list;
                if (!bins.TryGetValue(key, out list)) { list = free.Count > 0 ? free.Pop() : new List<int>(); bins.Add(key, list); }
                list.Add(index);
            }
        }

        private static Rectangle ObserveProjectileContact(Projectile projectile)
        {
            // The 1.4.6 damage read can consume an explosion's one-shot
            // localAI size. Only the real damage consumer may retire it.
            float pendingSize = projectile.localAI[0];
            try { return projectile.Damage_GetHitbox(); }
            finally { projectile.localAI[0] = pendingSize; }
        }
        private static long Key(int x, int y) { return ((long)x << 32) | (uint)y; }
        internal static bool Observe(Player body, int[] seen, int stamp, out int escape, out bool jumpClear)
        {
            Ensure(); int currentRisk = 0, leftRisk = 0, rightRisk = 0, jumpRisk = 0; float push = 0;
            Rectangle box = body.Hitbox, left = box, right = box, up = box;
            left.Offset(-32, 0); right.Offset(32, 0); up.Offset(0, -48);
            var query = box; query.Inflate(64, 64);
            for (int x = query.Left / Cell; x <= query.Right / Cell; x++) for (int y = query.Top / Cell; y <= query.Bottom / Cell; y++)
            {
                List<int> list;
                if (bins.TryGetValue(Key(x, y), out list))
                    foreach (int index in list) Score(index, body, seen, stamp, box, left, right, up, ref currentRisk, ref leftRisk, ref rightRisk, ref jumpRisk, ref push);
            }
            foreach (int index in large) Score(index, body, seen, stamp, box, left, right, up, ref currentRisk, ref leftRisk, ref rightRisk, ref jumpRisk, ref push);
            escape = leftRisk < rightRisk ? -1 : rightRisk < leftRisk ? 1 : Math.Sign(push);
            jumpClear = jumpRisk < currentRisk; return currentRisk > 0;
        }
        private static void Score(int index, Player body, int[] seen, int stamp, Rectangle box, Rectangle left, Rectangle right, Rectangle up,
            ref int currentRisk, ref int leftRisk, ref int rightRisk, ref int jumpRisk, ref float push)
        {
            if (seen[index] == stamp) return; seen[index] = stamp;
            var threat = threats[index];
            if (threat.Bounds.Intersects(box)) { currentRisk++; push += body.Center.X - threat.Center.X; }
            if (threat.Bounds.Intersects(left)) leftRisk++;
            if (threat.Bounds.Intersects(right)) rightRisk++;
            if (threat.Bounds.Intersects(up)) jumpRisk++;
        }
    }
}
