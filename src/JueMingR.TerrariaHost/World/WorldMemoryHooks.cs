using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.NetModules;
using Terraria.IO;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.World
{
    internal static class WorldMemoryHooks
    {
        private static readonly Harmony Harmony = new Harmony("JueMingR.WorldMemory");
        private static WorldMemory owner;
        private static bool installed;
        private static readonly object loadGate = new object();
        private static WorldFileData lastWorldFile;
        private static Task<string> lastLoadStamp;
        [ThreadStatic] private static bool primaryDecoder;

        internal static void Attach(WorldMemory memory)
        {
            owner = memory;
            if (installed) return;
            try
            {
                Patch(typeof(WorldGen), "KillTile", new[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool) }, nameof(TileWrite));
                Patch(typeof(WorldGen), "PlaceTile", new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int), typeof(int) }, nameof(TileWrite));
                Patch(typeof(WorldGen), "KillWall", new[] { typeof(int), typeof(int), typeof(bool) }, nameof(WallWrite));
                Patch(typeof(WorldGen), "PlaceWall", new[] { typeof(int), typeof(int), typeof(int), typeof(bool) }, nameof(WallWrite));
                Patch(typeof(WorldGen), "Convert", new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool) }, nameof(ConvertArea));
                Patch(typeof(Liquid), "AddWater", new[] { typeof(int), typeof(int) }, nameof(EnqueueLiquid));
                Patch(typeof(NetMessage), "DecompressTileBlock_Inner", null, nameof(ReceiveSection));
                Harmony.Patch(AccessTools.Method(typeof(MessageBuffer), "GetData"),
                    prefix: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(BeginDecode)),
                    postfix: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(ReceiveDelta)),
                    finalizer: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(EndDecode)));
                Harmony.Patch(AccessTools.Method(typeof(NetLiquidModule), nameof(NetLiquidModule.Deserialize)),
                    prefix: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(BeginLiquids)),
                    postfix: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(ReceiveLiquids)));
                Harmony.Patch(AccessTools.Method(typeof(WorldFile), nameof(WorldFile.LoadWorld)),
                    transpiler: new HarmonyMethod(typeof(WorldMemoryHooks), nameof(WitnessLoadedBytes)));
                installed = true;
            }
            catch
            {
                Harmony.UnpatchAll(Harmony.Id);
                // Queries still recheck actual loaded terrain. Only the
                // automatic invalidation hook set is unavailable.
                memory.MarkHooksUnavailable();
            }
        }

        private static IEnumerable<CodeInstruction> WitnessLoadedBytes(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(FileUtilities), nameof(FileUtilities.ReadAllBytes), new[] { typeof(string), typeof(bool) });
            var witness = AccessTools.Method(typeof(WorldMemoryHooks), nameof(ReadWorldBytes));
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original))
                { instruction.opcode = OpCodes.Call; instruction.operand = witness; replaced++; }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Unable to identify the original world byte source.");
        }

        private static byte[] ReadWorldBytes(string path, bool cloud)
        {
            // LoadWorld's MemoryStream owns these exact readonly bytes. Hash
            // them on a worker, rather than reading a possibly replaced
            // filename later and promoting the wrong past index.
            var bytes = FileUtilities.ReadAllBytes(path, cloud);
            lock (loadGate)
            {
                lastWorldFile = Main.ActiveWorldFileData;
                lastLoadStamp = Task.Run(() => TerrainStamp(bytes));
            }
            return bytes;
        }

        internal static Task<string> LoadedStamp(WorldFileData world)
        { lock (loadGate) return ReferenceEquals(lastWorldFile, world) && !WorldGen.loadFailed ? lastLoadStamp : null; }

        private static string TerrainStamp(byte[] bytes)
        {
            // Version 2's section pointers bracket exactly LoadWorldTiles.
            // Ordinary saves can change NPCs and header time with identical
            // terrain, so those independent sections do not invalidate it.
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes, writable: false)))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    int version = reader.ReadInt32();
                    if (version < 88) return WorldMemory.Digest(bytes);
                    if (version >= 135) FileMetadata.Read(reader, FileType.World);
                    int sections = reader.ReadInt16();
                    if (sections < 3 || sections > 64) return null;
                    int tileHeader = reader.ReadInt32(), tileStart = reader.ReadInt32(), tileEnd = reader.ReadInt32();
                    if (tileHeader <= 0 || tileHeader > tileStart || tileStart >= tileEnd || tileEnd > bytes.Length) return null;
                    int importance = (int)reader.BaseStream.Position + (sections - 3) * sizeof(int);
                    if (importance >= tileHeader) return null;
                    sha.TransformBlock(bytes, 0, sizeof(int), null, 0);
                    sha.TransformBlock(bytes, importance, tileHeader - importance, null, 0);
                    sha.TransformFinalBlock(bytes, tileStart, tileEnd - tileStart);
                    return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is ArgumentException) { return null; }
        }

        private static void Patch(Type type, string target, Type[] parameters, string after) =>
            Harmony.Patch(AccessTools.Method(type, target, parameters), postfix: new HarmonyMethod(typeof(WorldMemoryHooks), after));

        private static void TileWrite(int __0, int __1) => owner?.Mark(new Rectangle(__0 - 4, __1 - 4, 9, 9));
        private static void WallWrite(int __0, int __1) => owner?.Mark(new Rectangle(__0, __1, 1, 1));
        private static void ConvertArea(int __0, int __1, int __3) =>
            owner?.Mark(new Rectangle(__0 - __3, __1 - __3, 2 * __3 + 1, 2 * __3 + 1));
        private static void EnqueueLiquid(int x, int y) => owner?.Mark(new Rectangle(x - 1, y - 1, 3, 3));

        private static void ReceiveSection(int xStart, int yStart, int width, int height, bool __runOriginal)
        {
            if (__runOriginal && primaryDecoder) owner?.Mark(new Rectangle(xStart, yStart, width, height));
        }

        private static void BeginDecode(MessageBuffer __instance, out bool __state)
        {
            __state = primaryDecoder;
            primaryDecoder = Main.netMode == 1 && __instance.whoAmI == 256 && ReferenceEquals(__instance, NetMessage.buffer[256]);
        }

        private static void EndDecode(bool __state) { primaryDecoder = __state; }

        private static void ReceiveDelta(MessageBuffer __instance, int start, int length, int messageType, bool __runOriginal)
        {
            if (!__runOriginal || !primaryDecoder || owner == null || !owner.Ready) return;
            var buffer = __instance.readBuffer;
            if (messageType == 20 && length >= 8)
                owner.Mark(new Rectangle(BitConverter.ToInt16(buffer, start + 1), BitConverter.ToInt16(buffer, start + 3),
                    buffer[start + 5], buffer[start + 6]));
            else if (messageType == 48 && length >= 7)
                owner.Mark(new Rectangle(BitConverter.ToInt16(buffer, start + 1), BitConverter.ToInt16(buffer, start + 3), 1, 1));
        }

        private static void BeginLiquids(BinaryReader reader, int userId, out long __state)
        {
            __state = primaryDecoder && userId == 256 && owner?.Ready == true && reader.BaseStream.CanSeek ? reader.BaseStream.Position : -1;
        }

        private static void ReceiveLiquids(BinaryReader reader, long __state, bool __runOriginal, bool __result)
        {
            if (__state < 0 || !__runOriginal || !__result) return;
            var stream = reader.BaseStream;
            long end = stream.Position;
            try
            {
                stream.Position = __state;
                int count = reader.ReadUInt16();
                if (end - __state != 2L + 6L * count) return;
                owner?.MarkLiquidPages(reader, count);
            }
            finally { stream.Position = end; }
        }
    }
}
