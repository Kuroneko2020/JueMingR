using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private enum WiringZeroExit { Normal, Disabled, Retired, MissingWorldBit, ForeignMaterial, SoftcoreDeath, MediumcoreDeath }

        private static void ConstructionWiringZeroDebit(object context, object host, object tools, object input)
        {
            ConstructionConsumerCase("multi-wrench exact native zero debit", () => ConstructionZeroDebitCase(context, host, tools, input, ItemID.MulticolorWrench, false));
            ConstructionConsumerCase("GD wire exact native zero debit", () => ConstructionZeroDebitCase(context, host, tools, input, 3611, false));
            ConstructionConsumerCase("GD permanent actuator exact native zero debit", () => ConstructionZeroDebitCase(context, host, tools, input, 3611, true));
            ConstructionConsumerCase("zero debit with missing world bit remains retryable", () => ConstructionZeroDebitCase(context, host, tools, input, ItemID.MulticolorWrench, false, WiringZeroExit.MissingWorldBit));
            ConstructionConsumerCase("disabled GD late zero debit", () => ConstructionZeroDebitCase(context, host, tools, input, 3611, false, WiringZeroExit.Disabled));
            ConstructionConsumerCase("retired GD permanent actuator late zero debit", () => ConstructionZeroDebitCase(context, host, tools, input, 3611, true, WiringZeroExit.Retired));
            ConstructionConsumerCase("softcore native death preserves pending109 then settles zero", () => ConstructionZeroDebitCase(context, host, tools, input, ItemID.MulticolorWrench, false, WiringZeroExit.SoftcoreDeath));
            ConstructionConsumerCase("mediumcore native death cannot rebase pending110 material to Air", () => ConstructionZeroDebitCase(context, host, tools, input, ItemID.MulticolorWrench, false, WiringZeroExit.MediumcoreDeath));
            ConstructionConsumerCase("zero debit never adopts a foreign material identity", () => ConstructionZeroDebitCase(context, host, tools, input, ItemID.MulticolorWrench, false, WiringZeroExit.ForeignMaterial));
        }

        private static void ConstructionZeroDebitCase(object context, object host, object tools, object input, int toolType,
            bool actuator, WiringZeroExit exit = WiringZeroExit.Normal)
        {
            int x = actuator ? 50 : 44, y = 42, nextX = x + 2;
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager; var client = Main.clientPlayer;
            var tiles = Main.tile; var buffers = (MessageBuffer[])NetMessage.buffer.Clone(); var clients = (RemoteClient[])Netplay.Clients.Clone();
            var oldWireMode = WiresUI.Settings.ToolMode;
            Player deathPlayer = null; byte difficulty = 0; int spawnX = Main.spawnTileX, spawnY = Main.spawnTileY;
            var patch = new Harmony("JueMingR.Tests.WiringZeroDebitPackets"); var send = typeof(NetMessage).GetMethod("SendData", F);
            patch.Patch(send, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordConstructionWiringPacket)));
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 }; Main.clientPlayer = new Player();
                for (int i = 0; i < NetMessage.buffer.Length; i++) if (NetMessage.buffer[i] == null) NetMessage.buffer[i] = new MessageBuffer();
                for (int i = 0; i < Netplay.Clients.Length; i++) if (Netplay.Clients[i] == null) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                NetMessage.buffer[256] = new MessageBuffer(); NetMessage.buffer[0] = new MessageBuffer(); Netplay.Clients[0] = new RemoteClient { Id = 0, State = 10 };
                Main.netMode = 1; Main.sectionManager = new WorldSections(1, 2); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, p.position.Y);
                deathPlayer = p; difficulty = p.difficulty;
                p.inventory[12].SetDefaults(toolType); var tool = p.inventory[12];
                p.inventory[20].SetDefaults(actuator ? ItemID.Actuator : ItemID.Wire); p.inventory[20].stack = 2; var supply = p.inventory[20];
                if (actuator)
                {
                    NativeToolsChecks.Tile(x, y, TileID.Stone); NativeToolsChecks.Tile(nextX, y, TileID.Stone);
                    Require(!p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, supply.tileBoost + p.blockRange),
                        "permanent actuator uses the real GD range fallback rather than the short held actuator consumer");
                }
                var entry = Asset(host, actuator ? new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Actuator, TileType = TileID.Stone, Actuator = true } :
                    new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 });
                Place(context, host, input, entry, x, y);
                long unknown = (long)Get(host, "BuildUnknownResults"); int unconfirmed = ((ICollection)Get(host, "buildUnconfirmed")).Count;
                constructionWiringPackets.Clear(); SetAutomatic(host, input, true); ConstructionWaitWiringReply(context, host, input);
                var request = constructionWiringPackets.Single(packet => packet[2] == 109); var receipt = Get(host, "wiringReply");
                Require(request.Length == 12 && BitConverter.ToInt16(request, 3) == x && BitConverter.ToInt16(request, 5) == y &&
                    BitConverter.ToInt16(request, 7) == x && BitConverter.ToInt16(request, 9) == y &&
                    request[11] == (byte)(actuator ? WiresUI.Settings.MultiToolMode.Actuator : WiresUI.Settings.MultiToolMode.Blue) &&
                    !ConstructionZeroBit(x, y, actuator) && supply.stack == 2 && GetOptional(receipt, "TemporaryActuator") == null,
                    "the real selected tool submits one exact109 before any competing action or debit; tool=" + toolType);
                if (exit == WiringZeroExit.Disabled || exit == WiringZeroExit.Retired || exit == WiringZeroExit.SoftcoreDeath || exit == WiringZeroExit.MediumcoreDeath)
                    SetAutomatic(host, input, false);
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return !(bool)Get(Get(tools, "Use"), "Active") && p.itemAnimation == 0 && p.itemTime == 0; });
                Require(ReferenceEquals(GetOptional(host, "wiringReply"), receipt) && (bool)Call(host, "WiringPendingMaterial", supply),
                    "the detached request still protects its exact material until both original replies arrive");
                if (exit == WiringZeroExit.Retired)
                {
                    Call(host, "ClearProjection");
                    Require(Scene(host).Length == 0 && ReferenceEquals(GetOptional(host, "wiringReply"), receipt),
                        "projection retirement retains only the already-submitted finite decoder duty");
                }

                // Separate authority Tiles keep a server write from impersonating
                // the client's later MessageBuffer17 world observation.
                var clientTiles = Main.tile; var serverTiles = (Tile[,])clientTiles.Clone();
                serverTiles[x, y] = (Tile)clientTiles[x, y].Clone(); serverTiles[nextX, y] = (Tile)clientTiles[nextX, y].Clone();
                var ordered = ConstructionZeroAuthorityPackets(request, serverTiles, x, y, actuator, true);
                ConstructionRequireWiringPair(ordered, actuator, 0);
                Require(supply.stack == 2 && !ConstructionZeroBit(x, y, actuator),
                    "another original authority action wins before server109; server MassWire emits zero without touching client inventory or Tile");
                if (exit != WiringZeroExit.MissingWorldBit) ReplayConstructionPacket(ordered[0], 256);
                if (exit == WiringZeroExit.SoftcoreDeath)
                {
                    ConstructionPendingNativeDeath(context, host, tools, p, receipt, false);
                    Require(ReferenceEquals(p.inventory[12], tool) && ReferenceEquals(p.inventory[20], supply) && tool.stack == 1 && supply.stack == 2 &&
                        (bool)Call(host, "WiringPendingMaterial", supply),
                        "softcore KillMe/DropItems/Spawn retains the exact pending tool, material and finite receipt");
                }
                ReplayConstructionPacket(ordered[1], 256);
                Require(ReferenceEquals(GetOptional(host, "wiringReply"), receipt) && supply.stack == 2 &&
                    (long)Get(host, "BuildUnknownResults") == unknown && (bool)Call(host, "WiringPendingMaterial", supply),
                    "one accurate wire0 is an incomplete pair and cannot release the source or retirement duty");
                if (exit == WiringZeroExit.MediumcoreDeath)
                {
                    ConstructionPendingNativeDeath(context, host, tools, p, receipt, true);
                    Require(ReferenceEquals(p.inventory[12], tool) && ReferenceEquals(p.inventory[20], supply) && tool.IsAir && supply.IsAir &&
                        (bool)Call(host, "WiringPendingMaterial", supply),
                        "real mediumcore DropItems clears the original Item endpoints in place without changing request/session identity");
                    ReplayConstructionPacket(ordered[2], 256);
                    ulong affected = (1UL << 12) | (1UL << 20);
                    Require(GetOptional(host, "wiringReply") == null && (bool)Get(host, "wiringCorrelationLost") &&
                        (long)Get(host, "BuildUnknownResults") > unknown && ((ICollection)Get(host, "buildUnconfirmed")).Count > unconfirmed &&
                        ((ulong)Get(tools, "unknown") & affected) == affected && supply.IsAir && tool.IsAir,
                        "an accurate second zero cannot reclassify intervening native same-reference loss as confirmed zero; responsibility stays unknown and protected");
                    return;
                }
                if (exit == WiringZeroExit.ForeignMaterial)
                {
                    // Isolated fault injection preserves all values but loses
                    // the exact inventory endpoint; zero must not forgive it.
                    p.inventory[20] = supply.Clone();
                    ReplayConstructionPacket(ordered[2], 256);
                    Require(GetOptional(host, "wiringReply") == null && (bool)Get(host, "wiringCorrelationLost") &&
                        (long)Get(host, "BuildUnknownResults") > unknown && ((ICollection)Get(host, "buildUnconfirmed")).Count > unconfirmed &&
                        !(bool)Call(tools, "BuildInventoryEligible", 12, tool) && !(bool)Call(tools, "BuildInventoryEligible", 20, p.inventory[20]) &&
                        supply.stack == 2 && p.inventory[20].stack == 2,
                        "a foreign same-type reference remains unknown and protected despite actual zero debit");
                    long knownUnknown = (long)Get(host, "BuildUnknownResults"); ulong protectedSlots = (ulong)Get(tools, "unknown");
                    ConstructionPendingNativeDeath(context, host, tools, p, null, false);
                    Require((bool)Get(host, "wiringCorrelationLost") && GetOptional(host, "wiringReply") == null &&
                        (long)Get(host, "BuildUnknownResults") == knownUnknown && (ulong)Get(tools, "unknown") == protectedSlots &&
                        !(bool)Call(tools, "BuildInventoryEligible", 12, tool) && !(bool)Call(tools, "BuildInventoryEligible", 20, p.inventory[20]),
                        "real softcore death and Spawn preserve an already-established unknown source and lost correlation");
                    return;
                }
                ReplayConstructionPacket(ordered[2], 256);
                Require(GetOptional(host, "wiringReply") == null && !(bool)Get(host, "wiringCorrelationLost") &&
                    supply.stack == 2 && tool.stack == 1 && (long)Get(host, "BuildUnknownResults") == unknown &&
                    ((ICollection)Get(host, "buildUnconfirmed")).Count == unconfirmed && !(bool)Call(host, "WiringPendingMaterial", supply) &&
                    (bool)Call(tools, "BuildInventoryEligible", 12, tool) && (bool)Call(tools, "BuildInventoryEligible", 20, supply) &&
                    ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0,
                    "the exact completed zero pair releases consumption and tool/material protection without granting actuator ownership");
                Require(ConstructionZeroBit(x, y, actuator) == (exit != WiringZeroExit.MissingWorldBit),
                    "world satisfaction is independent of the exact zero debit receipt");

                if (exit == WiringZeroExit.MissingWorldBit)
                {
                    Require(Scene(host).Length == 1 && (bool)Get(Get(receipt, "Flight"), "Checked"),
                        "a known zero with no client world bit checks consumption but cannot retire the unmatched projection");
                    // The authority also removes its winning bit before the
                    // retry, so the next original request has a real one debit.
                    try { Main.netMode = 2; Main.tile = serverTiles; Require(WorldGen.KillWire2(x, y), "a separate authority action removes the wire before retry"); }
                    finally { Main.tile = clientTiles; Main.netMode = 1; }
                    nextX = x;
                }
                else
                {
                    if (exit == WiringZeroExit.Disabled || exit == WiringZeroExit.Retired || exit == WiringZeroExit.SoftcoreDeath)
                    {
                        for (int i = 0; i < 8; i++) ConstructionStepWithProjectiles(context, input);
                        Require(!(bool)Get(host, "AutoBuildEnabled") && !(bool)Get(Get(tools, "Use"), "Active") &&
                            GetOptional(host, "grandBuild") == null && constructionWiringPackets.All(packet => packet[2] != 109) &&
                            (exit != WiringZeroExit.Retired || Scene(host).Length == 0),
                            "late zero settlement never revives disabled input or a retired projection");
                    }
                    NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return Scene(host).Length == 0; });
                    SetAutomatic(host, input, false); Place(context, host, input, entry, nextX, y);
                }
                constructionWiringPackets.Clear(); SetAutomatic(host, input, true); ConstructionWaitWiringReply(context, host, input);
                request = constructionWiringPackets.Single(packet => packet[2] == 109);
                var nextFlight = Get(Get(host, "wiringReply"), "Flight");
                Require(BitConverter.ToInt16(request, 3) == nextX && ReferenceEquals(Get(Get(nextFlight, "Source"), "Item"), tool),
                    "the same exact unprotected tool can submit its next native request");
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return !(bool)Get(Get(tools, "Use"), "Active") && p.itemAnimation == 0 && p.itemTime == 0; });
                ordered = ConstructionZeroAuthorityPackets(request, serverTiles, nextX, y, actuator, false);
                ConstructionRequireWiringPair(ordered, actuator, 1);
                foreach (var packet in ordered) ReplayConstructionPacket(packet, 256);
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"); });
                Require(ConstructionZeroBit(nextX, y, actuator) && supply.stack == 1 && tool.stack == 1 &&
                    ReferenceEquals(p.inventory[12], tool) && ReferenceEquals(p.inventory[20], supply) &&
                    GetOptional(host, "wiringReply") == null && (long)Get(host, "BuildUnknownResults") == unknown &&
                    ((ICollection)Get(host, "buildUnconfirmed")).Count == unconfirmed,
                    "the next real server request debits once through actual client110 and full current Match retires naturally");
                Console.WriteLine("PASS WiringZeroDebit tool=" + toolType + " actuator=" + actuator + " exit=" + exit + " zero=0 nextDebit=1");
            }
            finally
            {
                SetAutomatic(host, input, false); Call(host, "ClearProjection"); Main.tile = tiles; Main.netMode = mode;
                if (deathPlayer != null) deathPlayer.difficulty = difficulty;
                Main.spawnTileX = spawnX; Main.spawnTileY = spawnY;
                Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client; WiresUI.Settings.ToolMode = oldWireMode;
                for (int i = 0; i < buffers.Length; i++) NetMessage.buffer[i] = buffers[i];
                for (int i = 0; i < clients.Length; i++) Netplay.Clients[i] = clients[i];
                patch.Unpatch(send, HarmonyPatchType.All, patch.Id); Call(context, "UpdateRuntime");
            }
        }

        private static bool ConstructionZeroBit(int x, int y, bool actuator)
        { return actuator ? Main.tile[x, y].actuator() : Main.tile[x, y].wire2(); }

        private static void ConstructionWaitWiringReply(object context, object host, object input)
        { NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return GetOptional(host, "wiringReply") != null; }); }

        private static void ConstructionPendingNativeDeath(object context, object host, object tools, Player p, object receipt, bool medium)
        {
            long generation = (long)Get(Get(tools, "Runtime"), "Generation");
            byte difficulty = p.difficulty; int spawnX = Main.spawnTileX, spawnY = Main.spawnTileY;
            try
            {
            p.difficulty = (byte)(medium ? 1 : 0); Main.spawnTileX = 40; Main.spawnTileY = 40;
            LifecycleNativeDeath(p, () => {
                Require(p.dead, "the pending-network fixture executes real native death before Spawn");
                Call(context, "UpdateRuntime");
                Require((long)Get(Get(tools, "Runtime"), "Generation") == generation && ReferenceEquals(GetOptional(host, "wiringReply"), receipt),
                    "death observation retains the real session and the exact pending/unknown network responsibility");
            });
            Call(context, "UpdateRuntime");
            Require(!p.dead && (long)Get(Get(tools, "Runtime"), "Generation") == generation && ReferenceEquals(GetOptional(host, "wiringReply"), receipt),
                "original Spawn revives the same session without clearing pending/unknown network responsibility");
            }
            finally { p.difficulty = difficulty; Main.spawnTileX = spawnX; Main.spawnTileY = spawnY; }
        }

        private static byte[][] ConstructionZeroAuthorityPackets(byte[] request, Tile[,] serverTiles, int x, int y, bool actuator, bool competingInstall)
        {
            var clientTiles = Main.tile; constructionWiringPackets.Clear();
            try
            {
                Main.netMode = 2; Main.tile = serverTiles;
                if (competingInstall)
                {
                    Require(actuator ? WorldGen.PlaceActuator(x, y) : WorldGen.PlaceWire2(x, y), "a separate original authority action fills the submitted target");
                    NetMessage.SendData(17, -1, -1, null, actuator ? 8 : 5, x, y);
                }
                ReplayConstructionPacket(request, 0);
            }
            finally { Main.tile = clientTiles; Main.netMode = 1; }
            return constructionWiringPackets.Where(packet => packet[2] == 17 || packet[2] == 110).ToArray();
        }

        private static void ConstructionRequireWiringPair(byte[][] ordered, bool actuator, int count)
        {
            Require(ordered.Length == 3 && ordered[0][2] == 17 && ordered[0][3] == (actuator ? 8 : 5) &&
                ordered[1][2] == 110 && BitConverter.ToInt16(ordered[1], 3) == ItemID.Wire &&
                BitConverter.ToInt16(ordered[1], 5) == (actuator ? 0 : count) &&
                ordered[2][2] == 110 && BitConverter.ToInt16(ordered[2], 3) == ItemID.Actuator &&
                BitConverter.ToInt16(ordered[2], 5) == (actuator ? count : 0),
                "actual server MassWire serializers produce world17 then ordered wire110 and actuator110 with exact debit=" + count);
        }
    }
}
