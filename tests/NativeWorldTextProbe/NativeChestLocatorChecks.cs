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
                Call(receiver, "ReadBefore", new RemoteServer()); Call(receiver, "Update");
                Require(!(bool)Get(receiver, "Trusted") && !(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "stale connection callback taints inventory knowledge");
                Netplay.Connection = new RemoteServer(); Call(receiver, "Update");
                Require((bool)Get(receiver, "Trusted") && !(bool)Call(knowledge, "Complete", 7, chest, 10, 20), "new connection starts empty trusted observation epoch");
                Console.WriteLine("PASS: actual GetData 155/32, duplicate/missing slots, update revision and stale connection invalidation; no server opened.");
            }
            finally { ((IDisposable)receiver).Dispose(); Main.netMode = 0; }
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
