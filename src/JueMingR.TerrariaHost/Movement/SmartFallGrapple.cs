using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Movement
{
    internal struct SmartFallGrapplePlan
    {
        internal bool Found;
        internal Vector2 Target;
        internal int Delay;
        internal Item Item;
    }
    internal sealed class SmartFallGrapple
    {
        private readonly Projectile defaults = new Projectile();
        private readonly int[] substeps = new int[ProjectileID.Count];
        private readonly bool[] ignoreWater = new bool[ProjectileID.Count];
        internal long Samples = 0;

        internal SmartFallGrapplePlan Find(Player p, Item item, SmartFallContact contact, ref long reads)
        {
            var best = default(SmartFallGrapplePlan);
            if (!SmartFallCapabilities.Matches(item, SmartFallFamily.Grapple) || p.controlHook || p.grapCount > 0) return best;
            int updates = Prepare(item);
            if (updates < 1 || updates > 4) return best;
            // Compare a finite contact band plus side/upper targets. The
            // player's original landing surface is NOT a success constraint.
            for (int pass = 0; pass < 2; pass++) for (int ox = -2; ox <= 2; ox++) for (int oy = -2; oy <= 1; oy++)
            {
                int tx = (pass == 0 ? contact.TileX : (int)(p.Center.X / 16)) + ox;
                int ty = (pass == 0 ? contact.TileY : (int)(p.Center.Y / 16) - 3) + oy;
                var tile = SmartFallGeometry.Read(tx, ty, ref reads);
                if (!Latch(tile, item.shoot) || p.IsBlacklistedForGrappling(new Point(tx, ty))) continue;
                var target = new Vector2(tx * 16 + 8, ty * 16 + 8);
                int delay = FirstProtection(p, item, target, ref reads);
                if (delay < 0 || contact.Ticks <= delay) continue;
                if (!best.Found || delay < best.Delay)
                    best = new SmartFallGrapplePlan { Found = true, Target = target, Delay = delay, Item = item };
            }
            return best;
        }
        internal int FirstProtection(Player p, Item item, Vector2 target, ref long reads, int lead = 0)
        {
            int updates = Prepare(item);
            if (updates < 1 || updates > 4) return -1;
            Vector2 displacement = lead == 0 ? Vector2.Zero : new Vector2(p.velocity.X, p.gravDir * Math.Min(p.maxFallSpeed, p.velocity.Y * p.gravDir + Math.Abs(p.gravity)));
            Vector2 origin = p.Center + displacement, delta = target - origin;
            float length = delta.Length();
            if (length < 1) return -1;
            Vector2 velocity = delta * (item.shootSpeed / length), point = origin;
            float range = Range(item.shoot);
            if (range <= 0) return -1;
            for (int tick = 0; tick < SmartFallGeometry.Horizon - 1; tick++)
            {
                Vector2 playerCenter = p.MountedCenter + displacement + p.velocity * tick;
                for (int sub = 0; sub < updates; sub++)
                {
#if DEBUG
                    Samples++;
#endif
                    if (Vector2.DistanceSquared(point, playerCenter) > range * range) return -1;
                    int left = (int)Math.Floor((point.X - 5) / 16), right = (int)Math.Floor((point.X + 5 - .001f) / 16);
                    int top = (int)Math.Floor((point.Y - 5) / 16), bottom = (int)Math.Floor((point.Y + 5 - .001f) / 16);
                    for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++)
                    {
                        var t = SmartFallGeometry.Read(x, y, ref reads);
                        // Water updates depend on native liquid state. A dry
                        // representative cannot certify a submerged schedule.
                        if (t != null && t.liquid > 0 && !ignoreWater[item.shoot]) return -1;
                        if (!Latch(t, item.shoot) || p.IsBlacklistedForGrappling(new Point(x, y))) continue;
                        // AI runs before movement and players run before
                        // projectiles. ai2 is only preparation for next Player.
                        // The original ai0 attachment branch immediately adds
                        // this projectile to grappling[] BEFORE setting ai2
                        // (not only in the later stable-ai2 branch). Players
                        // have already updated, so true GrappleMovement is
                        // next Player phase even on the final sub-update.
                        return tick + 1;
                    }
                    point += velocity;
                }
            }
            return -1;
        }
        private int Prepare(Item item)
        {
            if (item == null || item.shoot <= 0 || item.shoot >= substeps.Length) return -1;
            if (substeps[item.shoot] == 0)
            {
                defaults.SetDefaults(item.shoot);
                substeps[item.shoot] = defaults.aiStyle == 7 ? defaults.extraUpdates + 1 : -1;
                ignoreWater[item.shoot] = defaults.ignoreWater;
            }
            return substeps[item.shoot];
        }
        private static bool Latch(Tile t, int type)
        { return t != null && t.nactive() && (Main.tileSolid[t.type] || t.type == 314 || type == 865 && (TileID.Sets.IsATreeTrunk[t.type] || t.type == 323)); }
        private static float Range(int type)
        {
            if (type >= 230 && type <= 235) return 300 + (type - 230) * 30;
            if (type >= 646 && type <= 649 || type == 322 || type == 332) return 550;
            if (type >= 486 && type <= 489) return 480;
            switch (type) { case 13: case 396: case 865: return 300; case 32: case 331: case 372: return 400; case 73: case 74: return 440; case 165: return 375; case 256: return 425; case 315: case 446: case 935: return 500; case 652: return 600; case 753: return 420; default: return 0; }
        }
    }
    internal static class SmartFallGrappleHooks
    {
        private const string Id = "JueMingR.Movement.SmartFallGrapple1458";
        [ThreadStatic] internal static SmartFallHost Launching;
        internal static bool Available { get; private set; }
        internal static void Install()
        {
            if (Available) return;
            var target = AccessTools.DeclaredMethod(typeof(Player), "QuickGrapple");
            if (target == null) return;
            try { new Harmony(Id).Patch(target, transpiler: new HarmonyMethod(typeof(SmartFallGrappleHooks), nameof(Track))); Available = true; }
            catch { new Harmony(Id).Unpatch(target, HarmonyPatchType.All, Id); Available = false; }
        }
        internal static void Uninstall()
        { new Harmony(Id).Unpatch(AccessTools.DeclaredMethod(typeof(Player), "QuickGrapple"), HarmonyPatchType.All, Id); Available = false; Launching = null; }
        private static IEnumerable<CodeInstruction> Track(IEnumerable<CodeInstruction> source)
        {
            var code = new List<CodeInstruction>(source); int count = 0;
            for (int i = code.Count - 1; i >= 0; i--)
            {
                var method = code[i].operand as MethodInfo;
                if (method == null || method.DeclaringType != typeof(Projectile) || method.Name != "NewProjectile" || method.ReturnType != typeof(int)) continue;
                code.InsertRange(i + 1, new[] { new CodeInstruction(OpCodes.Dup), new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SmartFallGrappleHooks), nameof(Record))) }); count++;
            }
            if (count != 1) throw new InvalidOperationException("smart-fall-grapple-birth-boundary:" + count);
            return code;
        }
        private static void Record(int index, Player player) { Launching?.RecordGrapple(player, index); }
    }
}
