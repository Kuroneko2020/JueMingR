using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.ChestLocator;
using Terraria;

namespace JueMingR.TerrariaHost.ChestLocator
{
    internal sealed class ChestReceiveObserver : IDisposable
    {
        private const string Owner = "JueMingR.ChestLocator.Receive";
        private static ChestReceiveObserver current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patches = new List<MethodInfo>();
        private readonly Queue<Receipt> receipts = new Queue<Receipt>();
        private readonly object gate = new object();
        private object connection, world, chests, tiles;
        private int contamination;
        internal readonly ChestKnowledge Knowledge = new ChestKnowledge();
        internal bool Ready { get; private set; }
        internal bool Trusted { get; private set; } = true;
        internal bool Pending { get { lock (gate) return receipts.Count != 0; } }
        internal long Generation { get; private set; }
        internal long LocalMutation { get; private set; }
        private long tick;
        internal long Tick { get { return Interlocked.Read(ref tick); } set { Interlocked.Exchange(ref tick, value); } }
        internal ChestReceiveObserver()
        {
            try
            {
                if (current != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) return;
                current = this;
                Patch(typeof(MessageBuffer), "GetData", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() }, nameof(Before), nameof(After));
                Patch(typeof(RemoteServer), "ClientReadCallBack", new[] { typeof(object), typeof(int) }, nameof(ReadBefore), null);
                // Both native quick-stack and our selective storage converge on
                // this overload. Invalidate at admission even if the write later
                // fails; an old content result must not survive uncertainty.
                var source = typeof(Terraria.GameContent.QuickStacking).GetNestedType("SourceInventory", BindingFlags.NonPublic);
                if (source == null) throw new MissingMemberException("selective-storage-source-abi");
                Patch(typeof(Terraria.GameContent.QuickStacking), "QuickStackToNearbyChests", new[] { typeof(Player), source, typeof(bool) }, nameof(LocalWrite), null);
                Patch(typeof(Terraria.GameContent.CraftingRequests), "CraftItem", new[] { typeof(Recipe), typeof(int), typeof(bool) }, nameof(LocalWrite), null);
                Ready = true;
            }
            catch { Dispose(); }
        }
        private void Patch(Type type, string name, Type[] signature, string before, string after)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, signature, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            patches.Add(method); harmony.Patch(method, Hook(before), Hook(after));
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(ChestReceiveObserver).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        private static void LocalWrite() { if (current != null) current.LocalMutation++; }
        private static void ReadBefore(RemoteServer __instance)
        {
            var owner = current;
            if (owner != null && Main.netMode == 1 && !Netplay.Disconnect && !ReferenceEquals(__instance, Netplay.Connection))
                Interlocked.Exchange(ref owner.contamination, 1);
        }
        private static int Signed(byte[] bytes, int offset) { return (short)(bytes[offset] | bytes[offset + 1] << 8); }
        private static void Before(MessageBuffer __instance, int __0, int __1, out Receipt __state)
        {
            __state = default(Receipt); var owner = current;
            if (owner == null || !owner.Ready || Main.netMode != 1 || __instance.whoAmI != 256 || __0 < 0 || __1 < 5 || __0 > __instance.readBuffer.Length - __1) return;
            byte[] bytes = __instance.readBuffer; int kind = bytes[__0];
            if (kind != 155 && kind != 32 || kind == 32 && __1 < 9) return;
            int index = Signed(bytes, __0 + 1);
            if (index < 0 || index >= Main.chest.Length || Main.chest[index] == null) return;
            var chest = Main.chest[index];
            __state = new Receipt { Kind = kind, Index = index, Chest = chest, Connection = Netplay.Connection, Tiles = Main.tile, Tick = owner.Tick,
                Size = kind == 155 ? Signed(bytes, __0 + 3) : chest.maxItems,
                Slot = kind == 32 ? bytes[__0 + 3] : -1, Quantity = kind == 32 ? Signed(bytes, __0 + 4) : 0,
                Prefix = kind == 32 ? bytes[__0 + 6] : 0, Type = kind == 32 ? Signed(bytes, __0 + 7) : 0 };
        }
        private static void After(Receipt __state, bool __runOriginal)
        {
            var owner = current;
            if (owner == null || !__runOriginal || __state.Chest == null || !ReferenceEquals(__state.Connection, Netplay.Connection) || !ReferenceEquals(__state.Tiles, Main.tile)) return;
            try
            {
                Chest chest = Main.chest[__state.Index];
                if (!ReferenceEquals(chest, __state.Chest) || chest.maxItems != __state.Size || chest.item == null || chest.item.Length < chest.maxItems) return;
                if (__state.Kind == 32)
                {
                    if (__state.Slot < 0 || __state.Slot >= chest.maxItems) return;
                    Item item = chest.item[__state.Slot];
                    if (item == null || item.type != __state.Type || item.stack != __state.Quantity || item.prefix != __state.Prefix) return;
                }
                lock (owner.gate)
                {
                    if (owner.receipts.Count >= 4096) { owner.receipts.Clear(); Interlocked.Exchange(ref owner.contamination, 1); return; }
                    owner.receipts.Enqueue(__state);
                }
            }
            catch { Interlocked.Exchange(ref owner.contamination, 1); }
        }
        internal void Update()
        {
            if (!ReferenceEquals(connection, Netplay.Connection) || !ReferenceEquals(world, Main.ActiveWorldFileData) || !ReferenceEquals(chests, Main.chest) || !ReferenceEquals(tiles, Main.tile))
            { connection = Netplay.Connection; world = Main.ActiveWorldFileData; chests = Main.chest; tiles = Main.tile; Knowledge.Clear(); Trusted = true; Generation++; }
            if (Interlocked.Exchange(ref contamination, 0) != 0) { Trusted = false; Knowledge.Clear(); Generation++; }
            for (int i = 0; i < 256; i++)
            {
                Receipt receipt;
                lock (gate) { if (receipts.Count == 0) break; receipt = receipts.Dequeue(); }
                if (!Trusted || !ReferenceEquals(receipt.Connection, connection) || !ReferenceEquals(receipt.Tiles, tiles) || !ReferenceEquals(Main.chest[receipt.Index], receipt.Chest)) continue;
                if (receipt.Kind == 155) Knowledge.Capacity(receipt.Index, receipt.Chest, receipt.Chest.x, receipt.Chest.y, receipt.Size, receipt.Tick);
                else Knowledge.Slot(receipt.Index, receipt.Chest, receipt.Slot, receipt.Tick);
            }
        }
        internal struct Receipt
        { internal int Kind, Index, Size, Slot, Type, Quantity, Prefix; internal long Tick; internal Chest Chest; internal object Connection, Tiles; }
        public void Dispose()
        { Ready = false; foreach (var method in patches) harmony.Unpatch(method, HarmonyPatchType.All, Owner); patches.Clear(); if (ReferenceEquals(current, this)) current = null; }
    }
}
