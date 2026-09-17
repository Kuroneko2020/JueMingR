using System;
using System.IO;
using System.Reflection;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeChestLocatorChecks
    {
        internal static void Run(Assembly assembly)
        {
            Type type = assembly.GetType("JueMingR.TerrariaHost.ChestLocator.ChestReceiveObserver", true);
            object receiver = Activator.CreateInstance(type, true);
            try
            {
                Require((bool)Get(receiver, "Ready"), "fixed .8 chest observer hooks install");
                Main.netMode = 1; Netplay.Connection = new RemoteServer(); Netplay.Disconnect = false;
                var chest = Chest.CreateWorldChest(7, 10, 20);
                Call(receiver, "Update"); object knowledge = Get(receiver, "Knowledge");
                Require(!(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "allocation alone is unknown");
                var buffer = new MessageBuffer { whoAmI = 256 };
                Receive(buffer, 155, writer => { writer.Write((short)7); writer.Write((short)3); }); Call(receiver, "Update");
                Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
                Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
                Receive(buffer, 32, writer => Slot(writer, 2, 0, 0)); Call(receiver, "Update");
                Require(!(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "real duplicate packet cannot fill missing slot");
                Receive(buffer, 32, writer => Slot(writer, 1, 22, 6)); Call(receiver, "Update");
                Require((bool)Call(knowledge, "Complete", 7, chest, 10, 20) && chest.item[0].stack == 5 && chest.item[1].stack == 6,
                    "actual successful native capacity and all slots establish local coverage");
                long revision = (long)Call(knowledge, "Revision", 7);
                Receive(buffer, 32, writer => Slot(writer, 1, 22, 7)); Call(receiver, "Update");
                Require((long)Call(knowledge, "Revision", 7) != revision, "actual slot application invalidates old snapshot revision");
                OverflowAndRecover(receiver, knowledge, buffer, chest);
                CapacityAndEpochs(receiver, knowledge, buffer, chest);
                Call(receiver, "ReadBefore", new RemoteServer()); Call(receiver, "Update");
                Require(!(bool)Get(receiver, "Trusted") && !(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "stale connection callback taints inventory knowledge");
                Main.tile = (Tile[,])Main.tile.Clone(); Call(receiver, "Update");
                ReceiveAll(buffer); Call(receiver, "Update");
                Require(!(bool)Get(receiver, "Trusted") && !(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "changing world arrays cannot rehabilitate a contaminated connection");
                Netplay.Connection = new RemoteServer(); Call(receiver, "Update");
                Require((bool)Get(receiver, "Trusted") && !(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "new connection starts empty trusted observation epoch");
                ReceiveAll(buffer); Call(receiver, "Update");
                Require((bool)Call(knowledge, "Complete", 7, chest, 10, 20), "reconnected source still requires and accepts full fresh coverage");
                Console.WriteLine("PASS: actual GetData 155/32, bounded overflow recovery, fresh capacity/distinct slots, queue consumption, world/connection epochs and stale-source protection; no server opened.");
            }
            finally { ((IDisposable)receiver).Dispose(); Main.netMode = 0; }
        }
        private static void OverflowAndRecover(object receiver, object knowledge, MessageBuffer buffer, Chest chest)
        {
            Func<bool> complete = () => (bool)Call(knowledge, "Complete", 7, chest, 10, 20);
            long generation = (long)Get(receiver, "Generation");
            for (int i = 0; i < 4098; i++) Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
            Require(((System.Collections.ICollection)Get(receiver, "receipts")).Count <= 4096, "native packet burst keeps the local observation queue bounded");
            Call(receiver, "Update");
            Require(!complete() && (long)Get(receiver, "Generation") != generation, "local queue loss invalidates every old complete record and result generation");
            for (int i = 0; i < 3; i++) { int slot = i; Receive(buffer, 32, writer => Slot(writer, slot, 9, 5)); }
            Call(receiver, "Update"); Require(!complete(), "all slots without a new capacity boundary cannot reuse pre-loss completeness");
            Receive(buffer, 155, writer => { writer.Write((short)7); writer.Write((short)3); });
            for (int i = 0; i < 5; i++) Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
            Receive(buffer, 32, writer => Slot(writer, 2, 0, 0)); Call(receiver, "Update");
            Require(!complete(), "fresh capacity plus repeated slot and missing slot remains unknown after overflow");
            Receive(buffer, 32, writer => Slot(writer, 1, 22, 6)); Call(receiver, "Update");
            Require(complete(), "same-connection complete fresh native inventory must recover after local observation overflow");
            // A new full cycle may arrive after the drop but before Update.
            for (int i = 0; i < 4097; i++) Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
            ReceiveAll(buffer); Call(receiver, "Update");
            Require(complete(), "post-loss capacity and all slots queued before consumption establish fresh coverage");
        }
        private static void CapacityAndEpochs(object receiver, object knowledge, MessageBuffer buffer, Chest chest)
        {
            Func<bool> complete = () => (bool)Call(knowledge, "Complete", 7, chest, 10, 20);
            Receive(buffer, 155, writer => { writer.Write((short)7); writer.Write((short)4); });
            for (int i = 0; i < 3; i++) { int slot = i; Receive(buffer, 32, writer => Slot(writer, slot, 9, 5)); }
            Call(receiver, "Update"); Require(!complete(), "capacity change requires every slot in the new size");
            Receive(buffer, 32, writer => Slot(writer, 3, 0, 0)); Call(receiver, "Update"); Require(complete(), "actual fourth slot completes the changed capacity");
            Receive(buffer, 155, writer => { writer.Write((short)7); writer.Write((short)3); });
            for (int i = 0; i < 254; i++) Receive(buffer, 32, writer => Slot(writer, 0, 9, 5));
            Receive(buffer, 32, writer => Slot(writer, 1, 22, 6)); Receive(buffer, 32, writer => Slot(writer, 2, 0, 0));
            Call(receiver, "Update");
            Require((bool)Get(receiver, "Pending") && !complete() && ((System.Collections.ICollection)Get(receiver, "receipts")).Count == 1, "one Update consumes exactly its 256 bound, leaving the final slot pending");
            Call(receiver, "Update"); Require(!(bool)Get(receiver, "Pending") && complete(), "next Update completes only the remaining queued receipt");
            ReceiveAll(buffer);
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "receive-new-world.wld"), false);
            Call(receiver, "Update"); Require(!complete(), "old-world queued receipts cannot fill a new world even when chest and tile objects were reused");
            ReceiveAll(buffer); Netplay.Connection = new RemoteServer(); Call(receiver, "Update");
            Require(!complete(), "old-connection queued receipts cannot fill the current connection");
            ReceiveAll(buffer); Call(receiver, "Update"); Require(complete(), "current world and connection accept new full native coverage");
        }
        private static void ReceiveAll(MessageBuffer buffer)
        {
            Receive(buffer, 155, writer => { writer.Write((short)7); writer.Write((short)3); });
            for (int i = 0; i < 3; i++) { int slot = i; Receive(buffer, 32, writer => Slot(writer, slot, slot == 0 ? 9 : 0, slot == 0 ? 5 : 0)); }
        }
        private static void Slot(BinaryWriter writer, int slot, int type, int quantity)
        { writer.Write((short)7); writer.Write((byte)slot); writer.Write((short)quantity); writer.Write((byte)0); writer.Write((short)type); }
        internal static void Receive(MessageBuffer buffer, byte message, Action<BinaryWriter> payload)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            { writer.Write(message); payload(writer); byte[] bytes = stream.ToArray(); Array.Copy(bytes, buffer.readBuffer, bytes.Length); int actual; buffer.GetData(0, bytes.Length, out actual); Require(actual == message, "actual packet dispatch"); }
        }
    }
}
