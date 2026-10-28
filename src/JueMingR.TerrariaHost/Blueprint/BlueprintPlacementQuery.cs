using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Blueprint
{
    // A reverse-patched copy of the locked original query keeps its anchor,
    // alternate and Hook rules. Only scratch preview ownership and the two
    // decoration RNG calls differ. The real consumer always runs the original
    // CanPlace and validates its actual random/alternate choice before PlaceIt.
    internal static class BlueprintPlacementQuery
    {
        private static TileObjectPreviewData queryPreview = new TileObjectPreviewData();
        private static TileObjectPreviewData queryPlacement = new TileObjectPreviewData();
        private static TileObjectPreviewData queryRandom = new TileObjectPreviewData();
        private static bool installed;
        private static int queryX, queryY;
        private static bool dyeAllowed;
        internal static void Install()
        {
            if (installed) return;
            if (queryPreview == null || queryPlacement == null || queryRandom == null) throw new InvalidOperationException("blueprint-query-scratch");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var original = typeof(TileObject).GetMethod(nameof(TileObject.CanPlace));
            Harmony.ReversePatch(original, new HarmonyMethod(typeof(BlueprintPlacementQuery).GetMethod(nameof(CanPlace), flags | BindingFlags.Public)),
                typeof(BlueprintPlacementQuery).GetMethod(nameof(Rewrite), flags));
            foreach (string name in new[] { nameof(RepeatedPumpkins), nameof(RepeatedRainbowBoulders), nameof(RepeatedCoralAndBeachPiles), nameof(RepeatedPigronatas) })
            {
                string native = "PlaceThing_Tiles_BlockPlacementFor" + name;
                Harmony.ReversePatch(typeof(Player).GetMethod(native, flags), new HarmonyMethod(typeof(BlueprintPlacementQuery).GetMethod(name, flags)),
                    typeof(BlueprintPlacementQuery).GetMethod(nameof(RewriteGate), flags));
            }
            Harmony.ReversePatch(typeof(WorldGen).GetMethod(nameof(WorldGen.PlaceAlch)), new HarmonyMethod(typeof(BlueprintPlacementQuery).GetMethod(nameof(Alch), flags)),
                typeof(BlueprintPlacementQuery).GetMethod(nameof(RewriteAlch), flags));
            Harmony.ReversePatch(typeof(WorldGen).GetMethod(nameof(WorldGen.PlaceDye)), new HarmonyMethod(typeof(BlueprintPlacementQuery).GetMethod(nameof(Dye), flags)),
                typeof(BlueprintPlacementQuery).GetMethod(nameof(RewriteDye), flags));
            installed = true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool CanPlace(int x, int y, int type, int style, int direction, out TileObject result, bool onlyCheck = false, int? forcedRandom = null)
        { throw new InvalidOperationException("blueprint-placement-query-not-installed"); }
        private static int Decoration(UnifiedRandom unused, int maximum) { return 0; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool Alch(int x, int y, int style) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Dye(int x, int y, int style) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        private static void AllowDye() { dyeAllowed = true; }
        internal static bool LegacyPlant(int x, int y, int type, int style)
        {
            // The caller has proved the complete 3x3 readable/non-null. These
            // locked legacy writers have no separate CanPlace API; their
            // copies return at the first commit instruction, preserving every
            // style/liquid/anchor branch without executing a world setter.
            // Alch's only earlier writes are its two null-Tile allocations,
            // excluded by that gate. Dye only reads Tiles/SolidTile before
            // commit; neither original prefix touches RNG or preview state.
            if (type == 82) return Alch(x, y, style);
            if (type != 227) return false;
            dyeAllowed = false; Dye(x, y, style); return dyeAllowed;
        }
        private static IEnumerable<CodeInstruction> RewriteAlch(IEnumerable<CodeInstruction> instructions)
        {
            int commits = 0;
            var setter = typeof(Tile).GetMethod(nameof(Tile.active), new[] { typeof(bool) });
            foreach (var instruction in instructions)
            {
                if (!instruction.Calls(setter)) { yield return instruction; continue; }
                commits++;
                instruction.opcode = OpCodes.Pop; instruction.operand = null; yield return instruction;
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                yield return new CodeInstruction(OpCodes.Ret);
            }
            if (commits != 1) throw new InvalidOperationException("blueprint-alchemy-query-abi");
        }
        private static IEnumerable<CodeInstruction> RewriteDye(IEnumerable<CodeInstruction> instructions)
        {
            int commits = 0;
            var first = typeof(Tile).GetField(nameof(Tile.type));
            foreach (var instruction in instructions)
            {
                if (instruction.opcode != OpCodes.Stfld || !Equals(instruction.operand, first)) { yield return instruction; continue; }
                commits++;
                instruction.opcode = OpCodes.Pop; instruction.operand = null; yield return instruction;
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Call, typeof(BlueprintPlacementQuery).GetMethod(nameof(AllowDye), BindingFlags.Static | BindingFlags.NonPublic));
                yield return new CodeInstruction(OpCodes.Ret);
            }
            if (commits != 1) throw new InvalidOperationException("blueprint-dye-query-abi");
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void RepeatedPumpkins(ref bool result, ref TileObject value) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void RepeatedRainbowBoulders(ref bool result, ref TileObject value) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void RepeatedCoralAndBeachPiles(ref bool result, ref TileObject value) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void RepeatedPigronatas(ref bool result, ref TileObject value) { throw new InvalidOperationException("blueprint-query-not-installed"); }
        private static int ReadX() { return queryX; }
        private static int ReadY() { return queryY; }
        private static IEnumerable<CodeInstruction> RewriteGate(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var field = instruction.operand as FieldInfo;
                if (field?.DeclaringType == typeof(Player) && (field.Name == nameof(Player.tileTargetX) || field.Name == nameof(Player.tileTargetY)))
                {
                    if (instruction.opcode != OpCodes.Ldsfld) throw new InvalidOperationException("blueprint-read-only-query-gate");
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(BlueprintPlacementQuery).GetMethod(field.Name == nameof(Player.tileTargetX) ? nameof(ReadX) : nameof(ReadY), BindingFlags.Static | BindingFlags.NonPublic);
                }
                yield return instruction;
            }
        }
        internal static bool PostConditions(TileObject value, int x, int y)
        {
            queryX = x; queryY = y;
            bool result = true;
            RepeatedPumpkins(ref result, ref value); RepeatedRainbowBoulders(ref result, ref value);
            RepeatedCoralAndBeachPiles(ref result, ref value); RepeatedPigronatas(ref result, ref value);
            return result;
        }
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            int random = 0, previews = 0, placements = 0, randomCaches = 0;
            var next = typeof(UnifiedRandom).GetMethod(nameof(UnifiedRandom.Next), new[] { typeof(int) });
            foreach (var instruction in instructions)
            {
                var field = instruction.operand as FieldInfo;
                string replacement = null;
                if (field?.DeclaringType == typeof(TileObject) && field.Name == nameof(TileObject.objectPreview)) { replacement = nameof(queryPreview); previews++; }
                else if (field?.DeclaringType == typeof(TileObjectPreviewData) && field.Name == "placementCache") { replacement = nameof(queryPlacement); placements++; }
                else if (field?.DeclaringType == typeof(TileObjectPreviewData) && field.Name == "randomCache") { replacement = nameof(queryRandom); randomCaches++; }
                if (replacement != null) instruction.operand = typeof(BlueprintPlacementQuery).GetField(replacement, BindingFlags.Static | BindingFlags.NonPublic);
                if (instruction.Calls(next)) { instruction.opcode = OpCodes.Call; instruction.operand = typeof(BlueprintPlacementQuery).GetMethod(nameof(Decoration), BindingFlags.Static | BindingFlags.NonPublic); random++; }
                yield return instruction;
            }
            if (random != 2 || previews == 0 || placements == 0 || randomCaches == 0) throw new InvalidOperationException("blueprint-placement-query-abi");
        }
    }
}
