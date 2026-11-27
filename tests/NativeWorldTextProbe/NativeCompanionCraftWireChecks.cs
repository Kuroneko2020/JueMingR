using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Net;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCraftWireChecks
    {
        private static List<byte[]> contents;
        private static List<byte[]> actorReplies;
        private static int primaryRecipient, actorRecipient;

        internal static void Run(object host, object actor, Player body, Player human, Chest chest, object anchor)
        {
            var profile = (CompanionProfile)Get(actor, "Profile");
            var identity = new CompanionTaskIdentity(profile.OwnerId, profile.Id, (long)Get(host, "session"),
                (long)Get(actor, "Generation"), profile.DataEpoch);
            var oldClients = Netplay.Clients;
            var oldBuffers = NetMessage.buffer;
            var oldSocket = Netplay.Connection.Socket;
            var oldSections = Main.sectionManager;
            int oldMode = Main.netMode, oldPlayer = Main.myPlayer, oldState = Netplay.Connection.State;
            bool oldDedServ = Main.dedServ, oldTerminating = Netplay.Connection.PendingTermination;
            var oldChestGel = chest.item[0];
            var oldInventory = body.inventory.ToArray();
            var oldCursor = Main.mouseItem;
            var sent = new List<byte[]>();
            var connectionType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection", true);
            var connection = Activator.CreateInstance(connectionType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { new Action<byte[]>(sent.Add), new Action(() => { }) }, null);
            var observerType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.ChestLocator.ChestReceiveObserver", true);
            var observer = observerType.GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Require(observer != null && (bool)Get(observer, "Ready"), "live NativeProbe uses the product's actual closed-chest receiver");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            TcpClient primary = null, serverPrimary = null, assistant = null, serverAssistant = null;
            var wire = new Harmony("JueMingR.Tests.NativeCompanionCraftWire");
            listener.Start();
            try
            {
                contents = new List<byte[]>();
                actorReplies = new List<byte[]>();
                primaryRecipient = human.whoAmI;
                actorRecipient = body.whoAmI;
                wire.Patch(AccessTools.Method(typeof(NetMessage), "SendPacket"), prefix: new HarmonyMethod(typeof(NativeCompanionCraftWireChecks), nameof(CaptureChest))
                { priority = Priority.First, before = new[] { "JueMingR.Tests.QuickItemOutlets" } });
                var address = (IPEndPoint)listener.LocalEndpoint;
                primary = new TcpClient();
                primary.Connect(address);
                serverPrimary = listener.AcceptTcpClient();
                assistant = new TcpClient();
                assistant.Connect(address);
                serverAssistant = listener.AcceptTcpClient();
                primary.ReceiveTimeout = assistant.ReceiveTimeout = 3500;
                Main.sectionManager = new WorldSections(Math.Max(2, Main.maxSectionsX), Math.Max(2, Main.maxSectionsY));
                Main.sectionManager.SetAllSectionsLoaded();
                NetMessage.buffer = Enumerable.Range(0, 257).Select(i => new MessageBuffer { whoAmI = i }).ToArray();
                // The original server's broadcast loop examines the state of
                // unused clients as well. Real Netplay initializes every slot.
                Netplay.Clients = Enumerable.Range(0, 256).Select(i => new RemoteClient { Id = i }).ToArray();
                Netplay.Clients[human.whoAmI] = Program.CreateRemoteClient(human.whoAmI, 10, new TcpSocket(serverPrimary));
                Netplay.Clients[body.whoAmI] = Program.CreateRemoteClient(body.whoAmI, 10, new TcpSocket(serverAssistant));
                NetMessage.buffer[human.whoAmI].broadcast = true;
                Netplay.Connection.Socket = new TcpSocket(primary);
                Netplay.Connection.PendingTermination = false;
                Netplay.Connection.State = 10;
                if (NetManager.Instance.GetModule<CraftingRequests.NetCraftingRequestsModule>() == null) Terraria.Initializers.NetworkInitializer.Load();
                Set(actor, "Connection", connection);

                var replica = new Player { whoAmI = body.whoAmI, active = true };
                void Server()
                { Main.netMode = 2; Main.dedServ = true; Main.myPlayer = 255; Main.player[body.whoAmI] = replica; }
                void Client()
                { Main.netMode = 1; Main.dedServ = false; Main.myPlayer = human.whoAmI; Main.player[body.whoAmI] = body; }
                int Count(int type) => body.inventory.Where(item => item.type == type).Sum(item => item.stack);
                void VerifiedChest()
                {
                    contents.Clear();
                    Server();
                    NetMessage.SendChestContentsTo(chest.index, human.whoAmI);
                    Client();
                    Call(observer, "Update");
                    Require(contents.Count == chest.maxItems + 1, "original chest serializer gives the exact capacity and every slot to the primary");
                    foreach (byte[] frame in contents) Decode(NetMessage.buffer[256], frame);
                    Call(observer, "Update");
                }
                CompanionTaskSnapshot Start()
                {
                    var step = new CompanionTaskStep("craft_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.Torch,
                        ["amount"] = 3, ["allow_nearby"] = true });
                    return (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "原版服务器制作确认", new[] { step }, anchor, Guid.NewGuid());
                }
                CompanionTaskSnapshot Snapshot(Guid id) => (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, id);
                byte[] Issue(Guid id)
                {
                    sent.Clear();
                    NativeQuickItemChecks.BeginWorldStep();
                    Call(host, "PumpTask", actor);
                    Require(Snapshot(id).State == "waiting", "original multiplayer craft is outstanding without a grant: " + Snapshot(id).Detail);
                    var frame = sent.Single(packet => packet[2] == MessageID.NetModules && BitConverter.ToUInt16(packet, 3) ==
                        NetManager.Instance.GetId<CraftingRequests.NetCraftingRequestsModule>());
                    return frame;
                }
                void Receive(byte[] frame)
                {
                    Server();
                    Decode(NetMessage.buffer[body.whoAmI], frame);
                    var reply = ReadFrame(assistant.GetStream());
                    Require(reply[2] == MessageID.NetModules, "real original server replies using the craft module");
                    Client();
                    Call(connection, "ReceiveRelay", reply);
                    Call(host, "PumpConnection", actor);
                }

                for (int i = 0; i < 58; i++) body.inventory[i] = new Item();
                body.inventory[5] = new Item(ItemID.Wood) { stack = 2 };
                chest.item[0] = new Item(ItemID.Gel) { stack = 3 };
                VerifiedChest();
                var accepted = Start();
                byte[] request = Issue(accepted.TaskId);
                Require(Count(ItemID.Wood) == 1 && Count(ItemID.Torch) == 0 && chest.item[0].stack == 3,
                    "native client debits local ingredient once and grants no product before remote approval");
                Receive(request);
                Require(Snapshot(accepted.TaskId).State == "succeeded" && Count(ItemID.Torch) == 3 && chest.item[0].stack == 2 &&
                    ReferenceEquals(oldCursor, Main.mouseItem), "original server debits its chest and receipt gives the actual actor its product");

                VerifiedChest();
                var rejected = Start();
                request = Issue(rejected.TaskId);
                Require(Count(ItemID.Wood) == 0 && Count(ItemID.Torch) == 3, "second client request has its own outstanding resource debit");
                chest.item[0] = new Item(); // Another legal server action exhausted this source before request consumption.
                Receive(request);
                Require(Snapshot(rejected.TaskId).State == "failed" && Count(ItemID.Wood) == 1 && Count(ItemID.Torch) == 3,
                    "actual original server denial triggers native refund for only the second attempt");
                Console.WriteLine("PASS true Terraria craft NetModule writer, server chest debit/denial, role-bound product, and native local refund.");

                // Native chest take uses a distinct ordinary 31/33 interaction,
                // then receives the original server's actual 32 on the primary.
                VerifiedChest();
                var sourceReport = (CompanionTaskOutcome)Call(host, "TryGetTaskObservation", identity, "resource_sources",
                    new Dictionary<string, object> { ["item_type"] = (int)ItemID.StoneBlock }, anchor);
                Require(sourceReport.Value?.StartsWith("chest:", StringComparison.Ordinal) == true && sourceReport.Completed == 6,
                    "authoritatively received current chest supplies the requested source: " + sourceReport.Detail);
                var transfer = new CompanionTaskStep("transfer_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.StoneBlock,
                    ["amount"] = 1, ["source"] = sourceReport.Value, ["destination"] = "self_inventory" });
                var box = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "真正服务器箱子转移", new[] { transfer }, anchor, Guid.NewGuid());
                sent.Clear();
                NativeQuickItemChecks.BeginWorldStep(); Call(host, "PumpTask", actor);
                Require(sent.Any(packet => packet[2] == 31) && !sent.Any(packet => packet[2] == 32), "opening goes through the actor's original connection before storage writes");
                actorReplies.Clear();
                Server();
                foreach (var packet in sent.Where(packet => packet[2] == 154 || packet[2] == 31)) Decode(NetMessage.buffer[body.whoAmI], packet);
                Require(replica.chest == chest.index && actorReplies.Any(packet => packet[2] == 33), "original server assigned this socket an open chest");
                Client();
                foreach (var response in actorReplies) Call(connection, "ReceiveRelay", response);
                Call(host, "PumpConnection", actor);
                sent.Clear();
                NativeQuickItemChecks.BeginWorldStep(); Call(host, "PumpTask", actor);
                var write = sent.Single(packet => packet[2] == 32);
                Require(Snapshot(box.TaskId).State == "waiting" && Count(ItemID.StoneBlock) == 1 && chest.item[1].stack == 6,
                    "one exact body-side change waits for the server's explicit chest-state relay");
                contents.Clear();
                Server(); Decode(NetMessage.buffer[body.whoAmI], write); Client();
                Require(contents.Count == 1 && chest.item[1].stack == 5, "original server applied the exact new chest stack and relayed it");
                Decode(NetMessage.buffer[256], contents[0]);
                NativeQuickItemChecks.BeginWorldStep(); Call(host, "PumpTask", actor);
                Require(Snapshot(box.TaskId).State == "succeeded" && Count(ItemID.StoneBlock) == 1 && body.chest == -1,
                    "actual authoritative relay settles once and releases the body chest lease");
                var release = sent.Last(packet => packet[2] == 33);
                Server(); Decode(NetMessage.buffer[body.whoAmI], release); Client();
                Require(replica.chest == -1, "the real server processes the release of the actor's chest lease");
                Console.WriteLine("PASS true Terraria chest open, assigned actor, ordered write, server debit/primary relay and close.");
            }
            finally
            {
                wire.UnpatchAll(wire.Id);
                contents = null;
                actorReplies = null;
                Set(actor, "Connection", null);
                Call(connection, "Dispose");
                Main.netMode = oldMode;
                Main.dedServ = oldDedServ;
                Main.myPlayer = oldPlayer;
                Main.player[body.whoAmI] = body;
                Main.sectionManager = oldSections;
                NetMessage.buffer = oldBuffers;
                Netplay.Clients = oldClients;
                Netplay.Connection.Socket = oldSocket;
                Netplay.Connection.PendingTermination = oldTerminating;
                Netplay.Connection.State = oldState;
                chest.item[0] = oldChestGel;
                Array.Copy(oldInventory, body.inventory, oldInventory.Length);
                primary?.Dispose(); serverPrimary?.Dispose(); assistant?.Dispose(); serverAssistant?.Dispose();
                listener.Stop();
            }
        }

        private static bool CaptureChest(byte[] data, int remoteClient)
        {
            if (Main.netMode != 2) return true;
            if (remoteClient == primaryRecipient && (data[2] == 32 || data[2] == 155))
            {
                contents.Add(data.Take(BitConverter.ToUInt16(data, 0)).ToArray());
                return false;
            }
            if (remoteClient == actorRecipient && (data[2] == 154 || data[2] == 32 || data[2] == 33 || data[2] == 155))
            {
                actorReplies.Add(data.Take(BitConverter.ToUInt16(data, 0)).ToArray());
                return false;
            }
            return true;
        }

        private static byte[] ReadFrame(NetworkStream stream)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                int size = reader.ReadUInt16();
                var frame = new byte[size];
                frame[0] = (byte)size;
                frame[1] = (byte)(size >> 8);
                byte[] payload = reader.ReadBytes(size - 2);
                Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
                return frame;
            }
        }

        private static void Decode(MessageBuffer decoder, byte[] frame)
        {
            Buffer.BlockCopy(frame, 2, decoder.readBuffer, 0, frame.Length - 2);
            decoder.ResetReader();
            decoder.GetData(0, frame.Length - 2, out _);
        }
    }
}
