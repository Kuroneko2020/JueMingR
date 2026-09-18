using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinNetworkChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static readonly List<byte[]> packets = new List<byte[]>();
        internal static void Run(object context, object host)
        {
            var audit = new Harmony("JueMingR.Tests.CoinSerialization");
            MethodInfo send = typeof(NetMessage).GetMethod("SendData", Flags);
            audit.Patch(send, postfix: new HarmonyMethod(typeof(NativeCoinNetworkChecks).GetMethod(nameof(Capture), Flags)));
            Player p = Main.LocalPlayer;
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true };
                NetMessage.buffer[256] = new MessageBuffer(); Main.netMode = 1;
                Call(context, "UpdateRuntime");
                int[] ids = { PlayerItemSlotID.Bank1_0, PlayerItemSlotID.Bank2_0, PlayerItemSlotID.Bank3_0, PlayerItemSlotID.Bank4_0 };
                int[] tiles = { 29, 97, 463, 491 };
                for (int b = 0; b < 4; b++)
                {
                    NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = (ushort)tiles[b];
                    p.inventory[50] = Coin(73, 1); Bank(p, b).item[0] = Coin(71, 1);
                    Main.clientPlayer = new Player(); typeof(Main).GetMethod("TrySyncingMyPlayer", Flags).Invoke(null, null); packets.Clear();
                    NativeCoinMatrix.Tick(host, 0, 150);
                    Require(Total(p.inventory, 58) == 0 && Total(Bank(p, b).item, 40) == 10001, "ordinary client real personal-bank deposit " + b);
                    Require(!packets.Any(row => row[2] == 32), "personal bank does not emit an invented world-chest transaction");
                    typeof(Main).GetMethod("TrySyncingMyPlayer", Flags).Invoke(null, null);
                    Require(packets.Any(row => Slot(row) == PlayerItemSlotID.Inventory0 + 50 && Type(row) == 0), "real inventory removal serialized " + b);
                    Require(packets.Any(row => Slot(row) >= ids[b] && Slot(row) < ids[b] + 40 && Type(row) == 73), "real selected bank serialized " + b);
                    foreach (var row in packets.Where(row => row[2] == 5))
                    {
                        int slot = Slot(row);
                        if (slot >= ids[b] && slot < ids[b] + 40)
                        {
                            Item item = Bank(p, b).item[slot - ids[b]];
                            Require(Type(row) == item.type && BitConverter.ToInt16(row, 6) == item.stack && row[8] == item.prefix && (row[11] & 1) == (item.favorited ? 1 : 0), "actual message5 slot payload matches live bank");
                        }
                    }
                }
                NativeCoinMatrix.Reset(p, host);
                Chest chest = Chest.CreateWorldChest(0, 40, 40); chest.item[0] = Coin(72, 5); p.chest = 0; packets.Clear();
                ChestUI.LootAll();
                Require(((CoinIntent)Get(host, "Intent")).Protected && Total(chest.item, 40) == 0 && Total(p.inventory, 58) == 500,
                    "client world-chest local application establishes real withdrawal before any fictional ACK");
                Require(packets.Any(row => row[2] == 32), "original world chest publishes its changed slot");
                NativeCoinMatrix.Reset(p, host); Main.ServerSideCharacter = true; Main.tile[40, 40].type = 29; p.inventory[50] = Coin(73, 1); p.bank.item[0] = Coin(71, 1);
                NativeCoinMatrix.Tick(host, 0, 150);
                Require(p.inventory[50].stack == 1 && Get(host, "Status").ToString().Contains("服务器角色"), "SSC is a precise local limitation");
                Console.WriteLine("PASS: four ordinary-client banks, actual native message5 payloads and world-chest withdrawal application; final network outlet isolated, no server ACK claimed.");
            }
            finally { Main.ServerSideCharacter = false; Main.netMode = 0; audit.Unpatch(send, HarmonyPatchType.All, audit.Id); Call(context, "UpdateRuntime"); NativeCoinMatrix.Reset(p, host); }
        }
        private static int Slot(byte[] row) { return row[2] == 5 ? BitConverter.ToInt16(row, 4) : -1; }
        private static int Type(byte[] row) { return BitConverter.ToInt16(row, 9); }
        private static void Capture(int __0)
        {
            if (Main.netMode != 1 || __0 != 5 && __0 != 32 && __0 != 138) return;
            byte[] bytes = NetMessage.buffer[256].writeBuffer; int length = BitConverter.ToUInt16(bytes, 0);
            Require(length >= 3 && length <= bytes.Length && bytes[2] == __0, "native money serialization boundary");
            packets.Add(bytes.Take(length).ToArray());
        }
    }
}
