using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
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
            int priorState = Netplay.Connection.State;
            var priorActorConnection = GetOptional(actor, "Connection");
            var priorBuffers = NetMessage.buffer;
            var primaryPlayers = Main.player;
            var primaryClients = Netplay.Clients;
            var primaryNpcs = Main.npc;
            var originalTiles = Main.tile;
            var originalSections = Main.sectionManager;
            var originalBodyPosition = body.position;
            var originalHumanPosition = human.position;
            var originalNursePosition = nurse.position;
            int originalTilesX = Main.maxTilesX, originalTilesY = Main.maxTilesY;
            int originalHomeX = nurse.homeTileX, originalHomeY = nurse.homeTileY;
            var originalNpcSlots = primaryNpcs.Take(4).ToArray();
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

                // Feed the original packet contracts through the ready
                // companion stream; 60 has no generation in its own payload.
                outlets.Unpatch(send, HarmonyPatchType.Prefix, outlets.Id);
                Main.netMode = 1;
                NetMessage.buffer = new MessageBuffer[257];
                for (int i = 0; i < 257; i++) NetMessage.buffer[i] = new MessageBuffer { whoAmI = i };
                Netplay.Connection.Socket = new TcpSocket(humanClient);
                Netplay.Connection.State = 10;
                Main.maxTilesX = 400; Main.maxTilesY = 160;
                Main.tile = new Tile[400, 160];
                for (int x = 0; x < 400; x++)
                    for (int y = 0; y < 160; y++) Main.tile[x, y] = new Tile();
                Main.sectionManager = new WorldSections(2, 2);
                Main.sectionManager.SetSectionLoaded(0, 0);
                body.position = new Vector2(185 * 16, 58 * 16);
                nurse.position = body.position + new Vector2(24, 0);
                nurse.homeTileX = 187; nurse.homeTileY = 60;
                primaryNpcs[nurse.whoAmI] = new NPC { whoAmI = nurse.whoAmI, active = false };

                Call(connection, "ReceiveRelay", NpcState(nurse, nurse.position + new Vector2(3000, 0)));
                Call(connection, "ReceiveRelay", Housing(nurse, 2));
                Call(host, "PumpConnection", actor);
                var observed = (NPC[])Get(host, "observedNpcs");
                Require(!observed[nurse.whoAmI].active, "distant initial 23/60 retains room facts without materializing the nurse");
                Call(connection, "ReceiveRelay", NpcState(nurse, nurse.position));
                Call(host, "PumpConnection", actor);
                var observedNurse = observed[nurse.whoAmI];
                Require(observedNurse.active && !observedNurse.homeless && observedNurse.homeTileX == nurse.homeTileX &&
                    observedNurse.homeTileY == nurse.homeTileY,
                    "same-generation near 23 restores the earlier distant actor-stream room with no second 60 or primary fallback");
                primaryNpcs[nurse.whoAmI] = nurse;

                int[] residentTypes = { NPCID.Guide, NPCID.Merchant, NPCID.ArmsDealer };
                for (int i = 0; i < residentTypes.Length; i++)
                {
                    var resident = new NPC();
                    resident.SetDefaults(residentTypes[i]);
                    resident.whoAmI = i + 1;
                    resident.active = true; resident.homeless = false;
                    resident.position = nurse.position + new Vector2(2800, 0);
                    resident.homeTileX = nurse.homeTileX + i + 1;
                    resident.homeTileY = nurse.homeTileY;
                    primaryNpcs[i + 1] = resident;
                }
                Call(connection, "ReceiveRelay", Housing(nurse, 1));
                Call(host, "PumpConnection", actor);
                Require(observedNurse.active && observedNurse.homeless && !nurse.homeless,
                    "ordered companion-only 60 updates its native NPC observation, without mutating the primary town table");
                Call(connection, "ReceiveRelay", Housing(nurse, 2));
                Call(host, "PumpConnection", actor);
                Require(!observedNurse.homeless && observedNurse.homeTileX == nurse.homeTileX && observedNurse.homeTileY == nurse.homeTileY,
                    "the later housing assignment updates this exact living generation");
                int expectedQuote;
                var previous = Call(host, "EnterNpcObservation");
                try
                {
                    for (int i = 0; i < 64; i++) NativeQuickItemChecks.BeginWorldStep();
                    var center = body.Center.ToTileCoordinates();
                    var loadedTile = Main.tile[center.X, center.Y];
                    Main.tile[center.X, center.Y] = null;
                    Require(Call(host, "TryCompanionNurse", actor).ToString() == "NotRun" &&
                        Main.tile[center.X, center.Y] == null &&
                        NativeRecoveryServiceChecks.Total(body) + NativeRecoveryServiceChecks.Total(human) == oldBalance,
                        "unknown terrain produces no bill and never materializes an empty tile");
                    Main.tile[center.X, center.Y] = loadedTile;
                    Call(host, "RequestSharedTerrain");
                    using (var reader = new BinaryReader(humanPeer.GetStream(), System.Text.Encoding.UTF8, leaveOpen: true))
                    {
                        var terrain = reader.ReadBytes(reader.ReadUInt16() - 2);
                        Require(terrain.Length >= 9 && terrain[0] == 8 && Netplay.GetSectionX(BitConverter.ToInt32(terrain, 1)) == 1,
                            "actual shared primary request loads the neighbouring missing biome section even though the actor section is loaded");
                    }
                    Main.sectionManager.SetSectionLoaded(1, 0);
                    var originalSettings = body.currentShoppingSettings;
                    var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks", true);
                    using ((IDisposable)hooks.GetMethod("Enter", Flags).Invoke(null, new[] { actor, (object)body.Center, false }))
                    {
                        var observedTable = Main.npc;
                        var shop = new ShopHelper();
                        var withoutResidents = shop.GetShoppingSettings(body, nurse);
                        Main.npc = primaryNpcs;
                        try
                        {
                            body.currentShoppingSettings = shop.GetShoppingSettings(body, nurse);
                            Require(body.currentShoppingSettings.PriceAdjustment < 1.50f &&
                                body.currentShoppingSettings.PriceAdjustment != withoutResidents.PriceAdjustment,
                                "native housed nurse oracle counts nearby rooms even with the bodies outside interest");
                            expectedQuote = Main.GetNurseHealCost();
                        }
                        finally { Main.npc = observedTable; body.currentShoppingSettings = originalSettings; }
                    }
                    Require(Call(host, "TryCompanionNurse", actor).ToString() == "Treated",
                        "loopback native nurse runs its normal AI payment and effect consumers: " + GetOptional(host, "Feedback"));
                }
                finally { Call(host, "LeaveNpcObservation", previous); }
                Require(oldBalance - NativeRecoveryServiceChecks.Total(body) - NativeRecoveryServiceChecks.Total(human) == expectedQuote,
                    "accepted 60 lowers the actual two-wallet debit to the original housed nurse price");

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
                VerifyPrimaryRoomHandoff(host, actor, human, nurse);

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
                Array.Copy(originalNpcSlots, primaryNpcs, originalNpcSlots.Length);
                Main.npc = primaryNpcs;
                Main.tile = originalTiles; Main.sectionManager = originalSections;
                Main.maxTilesX = originalTilesX; Main.maxTilesY = originalTilesY;
                body.position = originalBodyPosition; nurse.position = originalNursePosition;
                human.position = originalHumanPosition;
                nurse.homeTileX = originalHomeX; nurse.homeTileY = originalHomeY;
                Main.myPlayer = local;
                Main.netMode = mode;
                Main.dedServ = dedicated;
                Netplay.Clients = primaryClients;
                NetMessage.buffer = priorBuffers;
                Netplay.Connection.Socket = priorSocket;
                Netplay.Connection.State = priorState;
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

        private static void VerifyPrimaryRoomHandoff(object host, object actor, Player human, NPC nurse)
        {
            // Give the original primary MessageBuffer this same native
            // generation before arranging a primary observation takeover.
            Apply(NpcState(nurse, nurse.position), 256);
            var native = Main.npc[nurse.whoAmI];
            var lease = ((IDictionary)Get(host, "npcSources"))[nurse.whoAmI];
            Call(host, "RetireLease", lease, actor);
            human.Center = native.Center;
            Call(host, "FlushSourceRequests");
            Apply(Housing(nurse, 1), 256);
            var observed = (NPC[])Get(host, "observedNpcs");
            Require(native.homeless && !observed[nurse.whoAmI].homeless,
                "real primary 60 changes the native room while the observation lease still waits at its barrier");
            Require((bool)Call(host, "PrimaryPingReceived"), "primary observation's outstanding ordered barrier is accepted");
            Apply(NpcState(nurse, nurse.position), 256);
            var previous = Call(host, "EnterNpcObservation");
            try
            {
                var reentered = observed[nurse.whoAmI];
                var settings = (ShoppingSettings)Call(host, "NurseShoppingSettings", actor, reentered);
                Require(reentered.homeless && settings.PriceAdjustment == 1.50f,
                    "same-generation primary takeover replays the explicit intervening 60, including the actual homeless price");
            }
            finally { Call(host, "LeaveNpcObservation", previous); }
        }

        private static byte[] NpcState(NPC npc, Vector2 position)
        {
            return Frame(writer =>
            {
                var anchor = position + NPCID.Sets.SyncAnchor[npc.type] * npc.Size;
                writer.Write((byte)23); writer.Write((byte)npc.whoAmI); writer.Write((byte)1);
                writer.Write(anchor.X); writer.Write(anchor.Y); writer.Write(0f); writer.Write(0f);
                writer.Write((ushort)0); writer.Write((byte)128); writer.Write((byte)8); writer.Write((short)npc.netID);
            });
        }

        private static byte[] Housing(NPC nurse, byte houseStatus)
        {
            return Frame(writer =>
            {
                writer.Write((byte)60); writer.Write((short)nurse.whoAmI); writer.Write((short)nurse.homeTileX);
                writer.Write((short)nurse.homeTileY); writer.Write(houseStatus);
            });
        }

        private static byte[] Frame(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                write(writer);
                writer.Seek(0, SeekOrigin.Begin);
                writer.Write((ushort)stream.Length);
                return stream.ToArray();
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
            Set(actor, "SyncFavorite", original.Select(item => item.favorited).ToArray());
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
