using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.ID;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Pre-admitted two-role loopback, with original senders and original
    // MessageBuffer receiving wallet, visual-effect, health and buff data.
    internal static class NativeCompanionSupplyWireChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Run(object host, object actor, Player human, NPC nurse)
        {
            var body = (Player)Get(actor, "Body");
            int actorSlot = body.whoAmI, humanSlot = human.whoAmI;
            var priorSocket = Netplay.Connection.Socket;
            var priorActorConnection = GetOptional(actor, "Connection");
            var priorBuffers = NetMessage.buffer;
            var primaryPlayers = Main.player;
            var primaryClients = Netplay.Clients;
            int mode = Main.netMode, local = Main.myPlayer;
            bool dedicated = Main.dedServ;
            var send = AccessTools.Method(typeof(NetMessage), "SendPacket");
            var outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            TcpClient humanClient = null, humanPeer = null, actorPeer = null;
            object connection = null;
            listener.Start();
            try
            {
                if (Convert.ToInt32(Get(Terraria.Net.NetManager.Instance, "_moduleCount")) == 0)
                    Terraria.Initializers.NetworkInitializer.Load();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                humanClient = new TcpClient { NoDelay = true };
                humanClient.Connect(IPAddress.Loopback, port);
                humanPeer = listener.AcceptTcpClient();
                humanPeer.GetStream().ReadTimeout = 5000;
                var connectionType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection", true);
                connection = Activator.CreateInstance(connectionType, Flags, null, new object[] { "127.0.0.1", port }, null);
                Call(connection, "Start");
                NativeQuickItemChecks.Until(listener.Pending);
                actorPeer = listener.AcceptTcpClient();
                actorPeer.GetStream().ReadTimeout = 5000;
                Set(actor, "Connection", connection);

                ClearCoins(body);
                ClearCoins(human);
                body.inventory[50] = NativeCoinChecks.Coin(ItemID.CopperCoin, 3);
                human.bank4.item[17] = NativeCoinChecks.Coin(ItemID.GoldCoin, 1);
                body.statLife = Math.Max(1, body.statLifeMax2 - 70);
                body.AddBuff(BuffID.Poisoned, 600);
                PrepareInitialPublication(actor, body);
                var serverBody = Replica(body);
                var serverHuman = Replica(human);
                var oldActorCoins = ItemSignatures(body);
                var oldHumanCoins = ItemSignatures(human);
                long oldBalance = NativeRecoveryServiceChecks.Total(body) + NativeRecoveryServiceChecks.Total(human);

                // The joined actor sees a nurse from its established observation
                // table. This leaf starts at this pre-admitted world snapshot.
                outlets.Unpatch(send, HarmonyPatchType.Prefix, outlets.Id);
                Main.netMode = 1;
                NetMessage.buffer = new MessageBuffer[257];
                for (int i = 0; i < 257; i++) NetMessage.buffer[i] = new MessageBuffer { whoAmI = i };
                Netplay.Connection.Socket = new TcpSocket(humanClient);
                Call(host, "EnsureNpcObservation");
                var observed = (NPC[])Get(host, "observedNpcs");
                var observedNurse = new NPC();
                observedNurse.SetDefaults(NPCID.Nurse);
                observedNurse.position = nurse.position;
                observedNurse.active = true;
                observedNurse.whoAmI = 0;
                observed[0] = observedNurse;
                var previous = Call(host, "EnterNpcObservation");
                try
                {
                    for (int i = 0; i < 64; i++) NativeQuickItemChecks.BeginWorldStep();
                    Require(Call(host, "TryCompanionNurse", actor).ToString() == "Treated",
                        "loopback native nurse runs its normal AI payment and effect consumers: " + GetOptional(host, "Feedback"));
                }
                finally { Call(host, "LeaveNpcObservation", previous); }

                var actorChanged = Changed(oldActorCoins, ItemSignatures(body));
                var humanChanged = Changed(oldHumanCoins, ItemSignatures(human));
                Require(actorChanged.Contains(PlayerItemSlotID.Inventory0 + 50) && humanChanged.Contains(PlayerItemSlotID.Bank4_0 + 17),
                    "actual AI main coin and owner void change produce separate slot writes");
                Call(host, "Update", 0UL); // No companion window is open.
                Call(host, "Publish", actor);
                var humanFrames = Read(humanPeer.GetStream(), humanChanged, humanSlot, false);
                var actorFrames = Read(actorPeer.GetStream(), actorChanged, actorSlot, true);
                Require(actorFrames.Count(frame => frame[2] == 35) == 1 && actorFrames.All(frame => frame[2] != 66),
                    "AI healing emits the original visual 35 with no fabricated 66");

                Main.netMode = 2;
                Main.dedServ = true;
                Main.myPlayer = 255;
                Main.player = (Player[])primaryPlayers.Clone();
                Main.player[actorSlot] = serverBody;
                Main.player[humanSlot] = serverHuman;
                Netplay.Clients = new RemoteClient[256];
                for (int i = 0; i < 256; i++) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                foreach (var frame in humanFrames) Apply(frame, humanSlot);
                foreach (var frame in actorFrames) Apply(frame, actorSlot);

                Require(serverBody.statLife == body.statLife && serverBody.FindBuffIndex(BuffID.Poisoned) < 0 &&
                    serverHuman.statLife == human.statLife &&
                    NativeRecoveryServiceChecks.Total(serverBody) == NativeRecoveryServiceChecks.Total(body) &&
                    NativeRecoveryServiceChecks.Total(serverHuman) == NativeRecoveryServiceChecks.Total(human) &&
                    NativeRecoveryServiceChecks.Total(serverBody) + NativeRecoveryServiceChecks.Total(serverHuman) < oldBalance,
                    "original server decodes actor life/debuff and both native main/void balances");
                Console.WriteLine("PASS split nurse actual owner and AI TCP send streams -> original server 5/16/35/50; no 66; pre-admitted links.");
            }
            finally
            {
                Main.player = primaryPlayers;
                Main.myPlayer = local;
                Main.netMode = mode;
                Main.dedServ = dedicated;
                Netplay.Clients = primaryClients;
                NetMessage.buffer = priorBuffers;
                Netplay.Connection.Socket = priorSocket;
                Set(actor, "Connection", priorActorConnection);
                if (connection != null) ((IDisposable)connection).Dispose();
                humanPeer?.Dispose();
                humanClient?.Dispose();
                actorPeer?.Dispose();
                listener.Stop();
                if (Harmony.GetPatchInfo(send)?.Prefixes.Any(patch => patch.owner == outlets.Id) != true)
                    outlets.Patch(send, prefix: new HarmonyMethod(typeof(NativeQuickItemChecks), "NoNetwork"));
            }
        }

        private static Player Replica(Player source)
        {
            var body = new Player { whoAmI = source.whoAmI, active = true, statLife = source.statLife, statLifeMax = source.statLifeMax };
            for (int i = 0; i < 54; i++) body.inventory[i] = source.inventory[i].Clone();
            for (int i = 0; i < 40; i++) body.bank4.item[i] = source.bank4.item[i].Clone();
            Array.Copy(source.buffType, body.buffType, source.buffType.Length);
            Array.Copy(source.buffTime, body.buffTime, source.buffTime.Length);
            return body;
        }

        private static void PrepareInitialPublication(object actor, Player body)
        {
            var slots = (int[])Get(actor, "NetworkSlotIds");
            var original = slots.Select(slot => new PlayerItemSlotID.SlotReference(body, slot).Item).ToArray();
            Set(actor, "SyncType", original.Select(item => item.type).ToArray());
            Set(actor, "SyncStack", original.Select(item => item.stack).ToArray());
            Set(actor, "SyncPrefix", original.Select(item => (int)item.prefix).ToArray());
            Set(actor, "SyncBuffType", (int[])body.buffType.Clone());
            Set(actor, "SyncBuffTime", (int[])body.buffTime.Clone());
        }

        private static void ClearCoins(Player body)
        {
            foreach (var item in body.inventory.Concat(body.bank.item).Concat(body.bank2.item).Concat(body.bank3.item).Concat(body.bank4.item))
                if (item.IsACoin) item.TurnToAir();
        }

        private static Dictionary<int, int> ItemSignatures(Player body)
        {
            var sig = new Dictionary<int, int>();
            for (int i = 0; i < 54; i++) sig[PlayerItemSlotID.Inventory0 + i] = CoinSignature(body.inventory[i]);
            for (int i = 0; i < 40; i++) sig[PlayerItemSlotID.Bank4_0 + i] = CoinSignature(body.bank4.item[i]);
            return sig;
        }
        private static int CoinSignature(Item item) { return item.IsACoin ? item.type * 10000 + item.stack : 0; }
        private static HashSet<int> Changed(Dictionary<int, int> before, Dictionary<int, int> after)
        { return new HashSet<int>(before.Keys.Where(slot => before[slot] != after[slot])); }

        private static List<byte[]> Read(NetworkStream stream, HashSet<int> slots, int sender, bool expectActorStatus)
        {
            var frames = new List<byte[]>();
            var remaining = new HashSet<int>(slots);
            bool life = false, buffs = false, heal = false;
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                while (remaining.Count > 0 || expectActorStatus && !(life && buffs && heal))
                {
                    int length = reader.ReadUInt16();
                    Require(length >= 3, "real TCP frame length");
                    var frame = reader.ReadBytes(length - 2);
                    Require(frame.Length == length - 2, "real TCP frame bytes");
                    switch (frame[0])
                    {
                        case 5: remaining.Remove(frame[2] | frame[3] << 8); break;
                        case 16: life = true; break;
                        case 35: heal = true; break;
                        case 50: buffs = true; break;
                        case 66: throw new InvalidOperationException("unexpected nonvisual healing 66");
                        default: continue;
                    }
                    Require(frame[1] == sender, "wallet and status packet use original sender identity");
                    var withHeader = new byte[length];
                    withHeader[0] = (byte)length;
                    withHeader[1] = (byte)(length >> 8);
                    Buffer.BlockCopy(frame, 0, withHeader, 2, frame.Length);
                    frames.Add(withHeader);
                }
            }
            return frames;
        }

        private static void Apply(byte[] frame, int sender)
        {
            var buffer = new MessageBuffer { whoAmI = sender };
            Buffer.BlockCopy(frame, 2, buffer.readBuffer, 0, frame.Length - 2);
            buffer.ResetReader();
            buffer.GetData(0, frame.Length - 2, out _);
        }
    }
}
