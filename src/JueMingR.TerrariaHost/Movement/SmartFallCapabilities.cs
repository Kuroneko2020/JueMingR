using System;
using System.Reflection;
using System.Reflection.Emit;
using JueMingR.Features.Movement;
using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    internal static class SmartFallCapabilities
    {
        // Resolve once with the .8 host. Mount facts are read from original
        // data, never inferred from item names or a list of flying IDs.
        private static readonly FieldInfo MountsField = typeof(Mount).GetField("mounts", BindingFlags.Static | BindingFlags.NonPublic);
        private static Func<Mount.MountData[]> readMounts;
        internal static void Bind()
        {
            if (readMounts != null || MountsField == null) return;
            var read = new DynamicMethod("SmartFallNativeMountData", typeof(Mount.MountData[]), Type.EmptyTypes, typeof(Mount), true);
            var il = read.GetILGenerator(); il.Emit(OpCodes.Ldsfld, MountsField); il.Emit(OpCodes.Ret);
            readMounts = (Func<Mount.MountData[]>)read.CreateDelegate(typeof(Func<Mount.MountData[]>));
        }
        internal static bool Matches(Item item, SmartFallFamily family)
        {
            if (item == null || item.type <= 0 || item.stack <= 0 || item.expertOnly && !Main.expertMode) return false;
            int t = item.type;
            switch (family)
            {
                // These sets are the exact UpdateAccessory branches in locked
                // .8, including combined accessories, rather than Z fallbacks.
                case SmartFallFamily.DoubleJump:
                    switch (t) { case 53: case 399: case 857: case 983: case 987: case 1163: case 1164: case 1724: case 1863: case 3201: case 3241: case 1250: case 1251: case 1252: case 3250: case 3252: case 5331: return true; default: return false; }
                case SmartFallFamily.RocketBoots: return t == 128 || t == 405 || t == 898 || t == 1862 || t == 5000 || t == 4874;
                case SmartFallFamily.Carpet: return t == 934;
                case SmartFallFamily.Wings: return item.wingSlot > 0;
                case SmartFallFamily.Horseshoe: return t == 158 || t == 396 || t == 1250 || t == 1251 || t == 1252 || t == 3250 || t == 3251 || t == 3252 || t == 5331;
                case SmartFallFamily.Umbrella: return t == 946 || t == 4707;
                case SmartFallFamily.Grapple: return item.shoot > 0 && item.shoot < Main.projHook.Length && Main.projHook[item.shoot] && item.shoot != 403 && item.shootSpeed > 0;
                case SmartFallFamily.DjinnLegs: return t == 3770;
                case SmartFallFamily.TeleportRod: return t == 5335 || t == 1326;
                case SmartFallFamily.GravityGlobe: return t == 1131;
                case SmartFallFamily.FlyingMount:
                case SmartFallFamily.SafeMount:
                    var mounts = readMounts?.Invoke();
                    if (mounts == null || item.mountType < 0 || item.mountType >= mounts.Length || mounts[item.mountType] == null) return false;
                    var data = mounts[item.mountType];
                    bool flying = data.flightTimeMax > 0 || data.usesHover;
                    return family == SmartFallFamily.FlyingMount ? flying : !flying && data.fallDamage <= .001f;
                default: return false;
            }
        }
        internal static int NativeMask(Player p)
        {
            int result = 0;
            // Individual unused identities are an O(1) invalidation hint.
            // Cloud->Fart changes even while "any extra jump" stays true.
            result |= UnusedJumps(p) << 12;
            if (MountJump(p)) result |= 1;
            if (p.rocketBoots > 0 && p.rocketTime > 0 && p.CanUseBootFlyingAbilities) result |= 2;
            if (p.carpet && (p.canCarpet || p.carpetTime > 0) && p.gravDir > 0) result |= 4;
            if (p.hasWings || p.wingsLogic > 0) result |= 8;
            if (p.noFallDmg) result |= 16;
            if (p.slowFall) result |= 256;
            if (p.gravControl2 && !p.mount.Active) result |= 2048;
            return result;
        }
        internal static int UnusedJumps(Player p)
        { return (p.canJumpAgain_Cloud?1:0) | (p.canJumpAgain_Sandstorm?2:0) | (p.canJumpAgain_Blizzard?4:0) | (p.canJumpAgain_Fart?8:0) | (p.canJumpAgain_Sail?16:0) | (p.canJumpAgain_Unicorn?32:0) | (p.canJumpAgain_Santank?64:0) | (p.canJumpAgain_WallOfFleshGoat?128:0) | (p.canJumpAgain_Basilisk?256:0); }
        internal static bool UnusedForItem(Player p, Item item)
        {
            if (item == null) return false;
            int provided;
            // Exact ApplyEquipFunctional hasJumpOption branches. Merely
            // carrying Cloud cannot preserve an unused Fart charge through
            // Update's pre-Jump eligibility clearing, and vice versa.
            switch (item.type)
            {
                case 53: case 399: case 1250: provided=1; break;
                case 857: case 983: case 1252: provided=2; break;
                case 987: case 1163: case 1251: provided=4; break;
                case 1724: case 1863: case 3250: provided=8; break;
                case 3201: case 3241: case 3252: provided=16; break;
                case 1164: case 5331: provided=7; break;
                default: return false;
            }
            return (UnusedJumps(p) & provided) != 0;
        }
        internal static bool MountJump(Player p)
        {
            if (!p.mount.Active || p.mount.BlockExtraJumps) return false;
            switch (p.mount.Type)
            { case 10: return p.canJumpAgain_Unicorn; case 46: return p.canJumpAgain_Santank; case 47: return p.canJumpAgain_WallOfFleshGoat; case 14: return p.canJumpAgain_Basilisk; default: return false; }
        }
        internal static bool HoverMount(int type)
        { var data = readMounts?.Invoke(); return data != null && type >= 0 && type < data.Length && data[type] != null && data[type].usesHover; }
        internal static bool Risk(Player p)
        {
            float size = Main.maxTilesX / 4200f; size *= size;
            double ratio = (p.position.Y / 16f - (60 + 10 * size)) / (Main.worldSurface / (Main.remixWorld ? 1 : 6));
            bool reset = p.jump > 0 || p.rocketDelay > 0 || p.wet || p.slowFall || ratio < .8 || p.tongued;
            bool safe = p.noFallDmg || p.hasWings || p.mount.CanFly(p) || p.mount.Active && p.mount.Type == 1 || p.isPerformingJump_DownDash;
            return SmartFallDecision.Risk(p.position.Y, p.fallStart, p.gravDir, p.extraFall, p.mount.Active ? p.mount.ExtraFall : 0,
                p.velocity.Y * p.gravDir, p.stoned, safe, reset, p.mount.Active ? p.mount.FallDamage : 1);
        }
    }
}
