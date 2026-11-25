using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // Only forecasting a confirmed closed fixture uses these copies of the
    // original collision queries. The actual Player.Update sees the live map.
    internal static class CompanionDoorCollision
    {
        private static readonly Tile Empty = new Tile();
        [ThreadStatic] private static Rectangle omitted;

        static CompanionDoorCollision()
        {
            Install(nameof(TileCollision));
            Install(nameof(SlopeCollision));
            Install(nameof(SolidCollision));
            Install(nameof(WalkDownSlope));
            Install(nameof(StepDown));
            Install(nameof(StepUp));
        }

        private static void Install(string name)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var copy = typeof(CompanionDoorCollision).GetMethod(name, flags);
            var signature = Array.ConvertAll(copy.GetParameters(), parameter => parameter.ParameterType);
            var original = typeof(Collision).GetMethod(name, signature);
            if (original == null) throw new InvalidOperationException("companion-door-query-signature: " + name);
            Harmony.ReversePatch(original, new HarmonyMethod(copy), typeof(CompanionDoorCollision).GetMethod(nameof(Rewrite), flags));
        }

        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var read = typeof(CompanionDoorCollision).GetMethod(nameof(ReadTile), flags);
            var write = typeof(CompanionDoorCollision).GetMethod(nameof(NoTileWrite), flags);
            var collision = typeof(CompanionDoorCollision).GetMethod(nameof(TileCollision), flags);
            int reads = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.operand is MethodInfo method)
                {
                    if (method.DeclaringType == typeof(Tile[,]) && method.Name == "Get")
                    { instruction.opcode = OpCodes.Call; instruction.operand = read; reads++; }
                    else if (method.DeclaringType == typeof(Tile[,]) && method.Name == "Set")
                    { instruction.opcode = OpCodes.Call; instruction.operand = write; }
                    else if (method.DeclaringType == typeof(Collision) && method.Name == nameof(Collision.TileCollision))
                    { instruction.opcode = OpCodes.Call; instruction.operand = collision; }
                }
                yield return instruction;
            }
            if (reads == 0) throw new InvalidOperationException("companion-door-query-tile-read");
        }

        private static void NoTileWrite(Tile[,] map, int x, int y, Tile tile)
        { throw new InvalidOperationException("companion-door-query-unknown-tile"); }

        internal static Tile ReadTile(Tile[,] map, int x, int y) => omitted.Contains(x, y) ? Empty : map[x, y];

        internal readonly struct Fixture : IDisposable
        {
            private readonly Rectangle previous;
            internal Fixture(Rectangle tiles) { previous = omitted; omitted = tiles; }
            public void Dispose() { omitted = previous; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector2 TileCollision(Vector2 position, Vector2 velocity, int width, int height,
            bool fallThrough, bool fall2, int gravity, bool ignoreDoors, bool ignoreAetheriumPlatforms, bool hoik) => throw Missing();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector4 SlopeCollision(Vector2 position, Vector2 velocity, int width, int height,
            float gravity, bool fall, bool ignoreAetheriumPlatforms) => throw Missing();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool SolidCollision(Vector2 position, int width, int height) => throw Missing();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector4 WalkDownSlope(Vector2 position, Vector2 velocity, int width, int height, float gravity) => throw Missing();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void StepDown(ref Vector2 position, ref Vector2 velocity, int width, int height,
            ref float stepSpeed, ref float gfxOffY, int gravity, bool waterWalk) => throw Missing();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void StepUp(ref Vector2 position, ref Vector2 velocity, int width, int height,
            ref float stepSpeed, ref float gfxOffY, int gravity, bool holdsMatching, int specialChecksMode) => throw Missing();

        private static Exception Missing() => new InvalidOperationException("companion-door-query-not-installed");
    }
}
