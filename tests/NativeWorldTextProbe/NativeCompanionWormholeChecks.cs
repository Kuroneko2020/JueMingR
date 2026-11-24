using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Testing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionWormholeChecks
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const int PotionSlot = 44;
        private static int rejectKind;
        private static Player interruptedLocalBody;

        private static bool SkipCosmetic() => false;
        private static bool SkipLocalTeleport(Player __instance, int Style) =>
            Style != 3 || !ReferenceEquals(__instance, interruptedLocalBody);
        private static void InterruptLocalTeleport(Player player)
        {
            if (ReferenceEquals(player, interruptedLocalBody)) throw new IOException("fixture interruption before original position write");
        }
        private static bool RefuseOneNativeFrame(byte[] packet, ref bool __result)
        {
            if (packet[2] != rejectKind) return true;
            rejectKind = 0;
            __result = false;
            return false;
        }

        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            var outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            outlets.Unpatch(AccessTools.Method(typeof(Player), "Spawn"), HarmonyPatchType.Prefix, outlets.Id);
            var cosmetics = new Harmony("JueMingR.Tests.WormholeCosmetics");
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                cosmetics.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper), name),
                    prefix: new HarmonyMethod(typeof(NativeCompanionWormholeChecks), nameof(SkipCosmetic)));
            var host = Get(context, "Companions");
            try
            {
                Main.spawnTileX = 60; Main.spawnTileY = 58;
                Main.leftWorld = Main.topWorld = 0;
                Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
                Main.maxSectionsX = Math.Max(1, Main.maxTilesX / 200);
                Main.maxSectionsY = Math.Max(1, Main.maxTilesY / 150);
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                var human = Main.LocalPlayer;
                var profiles = Get(host, "profiles");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(profiles, "Ready"); });
                Require((bool)Call(host, "Create", "虫洞验证甲") && (bool)Call(host, "Create", "虫洞验证乙"), "two real recall profiles created");
                CompanionProfile[] roster = null;
                NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "Update", 0UL);
                    roster = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().ToArray();
                    return roster.Length == 2 && roster.All(p => (bool)Call(profiles, "IsSaved", p.Id));
                });
                Guid first = roster.Single(p => p.Name == "虫洞验证甲").Id;
                Guid second = roster.Single(p => p.Name == "虫洞验证乙").Id;
                Require((bool)Call(host, "Join", first) && (bool)Call(host, "Join", second), "real native bodies are ready");
                var body = (Player)Call(host, "GetBody", first);
                var other = (Player)Call(host, "GetBody", second);
                var actor = Call(host, "Owned", body);
                var feedback = new List<(Guid Id, string Text, bool Result)>();
                Set(host, "ManualRecallFeedback", new Action<Guid, string, bool>((id, text, ok) => feedback.Add((id, text, ok))));
                // The wire scene supplies fresh positions and inventory. A
                // local assertion can report its first failure without hiding
                // a separate pre-write transport regression in the same run.
                var failed = new List<BusinessAssertionException>();
                try { Local(host, actor, first, second, human, body, other, feedback); }
                catch (BusinessAssertionException error) { failed.Add(error); }
                try { Wire(host, actor, first, human, body, feedback); }
                catch (BusinessAssertionException error) { failed.Add(error); }
                if (failed.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failed[0]).Throw();
                if (failed.Count != 0) throw new BusinessAssertionException(String.Join(Environment.NewLine, failed));
            }
            finally
            {
                Call(host, "OnSessionEnded");
                cosmetics.UnpatchAll(cosmetics.Id);
            }
        }

        private static void Local(object host, object actor, Guid first, Guid second, Player human, Player body, Player other,
            List<(Guid Id, string Text, bool Result)> feedback)
        {
            var oldCamera = Main.screenPosition;
            human.position = new Vector2(960, 820); body.position = new Vector2(510, 820); other.position = new Vector2(380, 820);
            body.team = 2; human.team = 4;
            Put(body.inventory, PotionSlot, 2); Put(human.inventory, PotionSlot, 1);
            Require((bool)Call(host, "SetBehavior", first, new CompanionBehavior { Follow = false }), "the actual AI starts with Follow off");
            var mobility = Get(actor, "Mobility");
            object previous = Get(mobility, "PositionRevision");
            Set(mobility, "Result", Enum.Parse(Get(mobility, "Result").GetType(), "Started"));
            Set(mobility, "DoorNeedsClose", true);
            Require(Query(host, first, out _) && body.inventory[PotionSlot].stack == 2 && feedback.Count == 0,
                "read-only query changes neither payer nor position");
            Require(Recall(host, first) && body.Bottom == human.Bottom && body.teleportStyle == 3 && body.inventory[PotionSlot].stack == 1 &&
                human.inventory[PotionSlot].stack == 1 && Main.screenPosition == oldCamera && human.team == 4 && body.team == 2,
                "singleplayer applies original style 3, charges AI first, and leaves human camera and teams unchanged");
            var behavior = Get(actor, "Behavior");
            Vector2 anchor = (Vector2)Get(behavior, "anchor");
            var navigator = Get(behavior, "Navigator");
            Require((int)Get(mobility, "PositionRevision") == (int)previous + 1 && (string)Get(mobility, "Result").ToString() == "Idle" &&
                !(bool)Get(mobility, "DoorNeedsClose") && anchor == body.Center &&
                (long)Get(navigator, "ActionId") == 0 && Get(navigator, "QueryStatus").ToString() == "Idle" &&
                !((CompanionProfile)Get(actor, "Profile")).Behavior.Follow,
                "real landing retires position action and reanchors the still-disabled Follow");
            Require(!Recall(host, first) && body.inventory[PotionSlot].stack == 1, "same landing cannot be charged twice");
            Require(feedback.Count == 2 && feedback[0].Text.Contains("队友虫洞药水") && !feedback[1].Result,
                "the backend emits one action result per entry");

            body.position -= new Vector2(70, 0);
            body.inventory[PotionSlot] = new Item(); Put(body.bank4.item, 0, 2);
            Require(Recall(host, second) && other.Bottom == human.Bottom && human.inventory[PotionSlot].IsAir,
                "another AI uses the human's exact final bottle from ordinary inventory");
            Require(!Query(host, first, out string reason) && reason == "需要消耗虫洞药水" && !Recall(host, first) && body.bank4.item[0].stack == 2,
                "an available void-bag bottle does not count when the normal shared backpacks are empty");
            human.dead = true;
            Put(body.inventory, PotionSlot, 1);
            Require(!Recall(host, first) && body.inventory[PotionSlot].stack == 1, "a dead target cannot be assigned a paid recall");
            human.dead = false;
            var position = body.position;
            var pot = body.inventory[PotionSlot];
            int localRevision = (int)Get(mobility, "PositionRevision");
            body.environmentBuffImmunityTimer = 0;
            body.shimmering = body.shimmerWet = body.wet = true;
            interruptedLocalBody = body;
            var patch = new Harmony("JueMingR.Tests.WormholeLocalBoundary");
            try
            {
                var teleport = AccessTools.Method(typeof(Player), nameof(Player.Teleport));
                patch.Patch(teleport, prefix: new HarmonyMethod(typeof(NativeCompanionWormholeChecks), nameof(SkipLocalTeleport)) { priority = Priority.First });
                Require(!Recall(host, first) && ReferenceEquals(body.inventory[PotionSlot], pot) && body.environmentBuffImmunityTimer == 0 &&
                    body.shimmering && feedback.Last().Text == "未能提交传送。", "an actual Harmony skip before the native body releases the exact provisional slot");
                patch.Unpatch(teleport, HarmonyPatchType.Prefix, patch.Id);
                patch.Patch(AccessTools.Method(typeof(Terraria.GameContent.PressurePlateHelper), "UpdatePlayerPosition"),
                    prefix: new HarmonyMethod(typeof(NativeCompanionWormholeChecks), nameof(InterruptLocalTeleport)));
                bool accepted = Recall(host, first);
                Require(accepted && body.position == position && body.inventory[PotionSlot].IsAir &&
                    body.environmentBuffImmunityTimer == 4 && !body.shimmering && !body.shimmerWet && !body.wet &&
                    (int)Get(mobility, "PositionRevision") == localRevision && feedback.Last().Text == "传送结果未确认，已消耗 1 瓶虫洞药水。",
                    "an original caught failure before position preserves prior original environment effects and the debit, with an immediate unconfirmed result; " +
                    "accepted=" + accepted + ", immunity=" + body.environmentBuffImmunityTimer + ", count=" + body.inventory[PotionSlot].stack +
                    ", result=" + feedback.Last().Text);
            }
            finally { interruptedLocalBody = null; patch.UnpatchAll(patch.Id); }
            Console.WriteLine("PASS local original style 3, exact AI then human payer, no duplicate or void bag, dead owner, standby anchor and actual motion state.");
            Console.WriteLine("PASS original SP skip restores the exact bottle, while a later native fault preserves actual environmental side effects and the charge.");
        }

        private static void TransportBoundary(object host, object actor, Guid id, Player body, Type connectionType,
            List<(Guid Id, string Text, bool Result)> feedback)
        {
            var original = Get(actor, "Connection");
            var position = body.position;
            var potion = body.inventory[PotionSlot];
            // Keep the TCP worker unstarted so the real production queue
            // stays at its physical capacity boundary deterministically.
            using (var blocked = (IDisposable)Activator.CreateInstance(connectionType, Fields, null, new object[] { "127.0.0.1", 1 }, null))
            try
            {
                Set(actor, "Connection", blocked);
                for (int remaining = 4 * 1024 * 1024 - 8; remaining > 0;)
                {
                    int size = Math.Min(remaining, ushort.MaxValue);
                    var frame = new byte[size]; frame[0] = (byte)size; frame[1] = (byte)(size >> 8); frame[2] = 10;
                    Require((bool)Call(blocked, "TrySend", frame), "fill real direct TCP queue through native-format framed admission");
                    remaining -= size;
                }
                bool accepted = Recall(host, id);
                Require(!accepted && ReferenceEquals(body.inventory[PotionSlot], potion) && body.position == position &&
                    (long)Get(blocked, "sentPings") == 2 && (int)Get(blocked, "outgoingBytes") == 4 * 1024 * 1024 - 2 &&
                    feedback.Last().Text == "未能提交传送。",
                    "real capacity accepts both three-byte fences but rejects the native mode 2 before TCP enqueue and restores its exact source; " +
                    "accepted=" + accepted + ", count=" + body.inventory[PotionSlot].stack + ", bytes=" + Get(blocked, "outgoingBytes") +
                    ", result=" + feedback.Last().Text);
            }
            finally
            {
                Call(host, "RetireWormholes", actor);
                ((Queue<long>)Get(Get(actor, "WorldSource"), "Pings")).Clear();
                Set(actor, "Connection", original);
            }

            var relayed = new List<byte[]>();
            Action<byte[]> forward = frame =>
            {
                relayed.Add(frame);
                if (frame[2] == 65) throw new IOException("fixture failure after handing mode 2 to the relay");
            };
            using (var relay = (IDisposable)Activator.CreateInstance(connectionType, Fields, null, new object[] { forward, (Action)(() => { }) }, null))
            try
            {
                Set(actor, "Connection", relay);
                Require(Recall(host, id) && body.inventory[PotionSlot].stack == potion.stack - 1 && body.position == position &&
                    relayed.Select(frame => frame[2]).Take(3).SequenceEqual(new byte[] { 154, 65, 154 }) &&
                    feedback.Last().Text == "正在等待传送结果。",
                    "an exception after the relay sees the real mode 2 leaves the payer charged and the position unconfirmed");
            }
            finally
            {
                Call(host, "RetireWormholes", actor);
                ((Queue<long>)Get(Get(actor, "WorldSource"), "Pings")).Clear();
                Set(actor, "Connection", original);
                body.inventory[PotionSlot] = potion;
            }
            Console.WriteLine("PASS actual TCP capacity pre-write rejection restores the exact bottle and relay failure after acceptance cannot refund it.");
        }

        private static void Wire(object host, object actor, Guid id, Player human, Player body,
            List<(Guid Id, string Text, bool Result)> feedback)
        {
            var clientPlayers = Main.player;
            var clients = Netplay.Clients;
            var buffers = NetMessage.buffer;
            var socket = Netplay.Connection.Socket;
            var sections = Main.sectionManager;
            int clientState = Netplay.Connection.State, oldMode = Main.netMode, clientIndex = Main.myPlayer;
            bool dedicated = Main.dedServ;
            var connectionType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection", true);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            outlets.Unpatch(AccessTools.Method(typeof(NetMessage), "SendPacket"), HarmonyPatchType.Prefix, outlets.Id);
            try
            {
                if (Convert.ToInt32(Get(Terraria.Net.NetManager.Instance, "_moduleCount")) == 0)
                    Terraria.Initializers.NetworkInitializer.Load();
                using (var ownerWire = new WirePair())
                using (var remoteWire = new WirePair())
                using (var connection = (IDisposable)Activator.CreateInstance(connectionType, Fields, null,
                    new object[] { IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port }, null))
                {
                    Call(connection, "Start");
                    NativeQuickItemChecks.Until(listener.Pending);
                    using (var aiPeer = listener.AcceptTcpClient())
                    {
                        var stream = aiPeer.GetStream(); stream.ReadTimeout = 6000;
                        aiPeer.NoDelay = true;
                        int ai = body.whoAmI, player = human.whoAmI, observer = 249;
                        body.position = new Vector2(520, 820); human.position = new Vector2(960, 820);
                        var initial = body.position;
                        var dest = human.Bottom - new Vector2(body.width * .5f, body.height);
                        Put(body.inventory, PotionSlot, 2);
                        var serverPlayers = (Player[])clientPlayers.Clone();
                        var observerPlayers = (Player[])clientPlayers.Clone();
                        foreach (var table in new[] { serverPlayers, observerPlayers })
                        {
                            table[player] = Replica(human); table[ai] = Replica(body);
                            table[observer] = new Player { active = true, whoAmI = observer, position = new Vector2(600, 780), name = "Observer" };
                        }
                        Netplay.Clients = Enumerable.Range(0, 256).Select(index => new RemoteClient { Id = index }).ToArray();
                        NetMessage.buffer = Enumerable.Range(0, 257).Select(index => new MessageBuffer { whoAmI = index }).ToArray();
                        foreach (var endpoint in new[] { (player, ownerWire.Peer), (ai, aiPeer), (observer, remoteWire.Peer) })
                        {
                            Netplay.Clients[endpoint.Item1] = new RemoteClient { Id = endpoint.Item1, State = 10, Socket = new TcpSocket(endpoint.Item2) };
                            Netplay.Clients[endpoint.Item1].TileSections[0, 0] = true;
                            NetMessage.buffer[endpoint.Item1].broadcast = true;
                        }
                        Main.sectionManager = new WorldSections(Main.maxSectionsX, Main.maxSectionsY);
                        Main.sectionManager.SetAllSectionsLoaded();
                        Netplay.Connection.State = 10; Netplay.Connection.Socket = new TcpSocket(ownerWire.Client);
                        Set(actor, "Connection", connection);
                        var camera = Main.screenPosition;
                        void Client(Player[] table, int index) { Main.netMode = 1; Main.dedServ = false; Main.player = table; Main.myPlayer = index; }
                        void Server() { Main.netMode = 2; Main.dedServ = true; Main.player = serverPlayers; Main.myPlayer = 255; }
                        Client(clientPlayers, clientIndex);
                        TransportBoundary(host, actor, id, body, connectionType, feedback);
                        feedback.Clear();
                        var reject = new Harmony("JueMingR.Tests.WormholeQueueRefusal");
                        reject.Patch(AccessTools.Method(connectionType, "TrySend"),
                            prefix: new HarmonyMethod(typeof(NativeCompanionWormholeChecks), nameof(RefuseOneNativeFrame)));
                        try
                        {
                            rejectKind = 65;
                            Require(!Recall(host, id) && body.inventory[PotionSlot].stack == 2 && body.position == initial &&
                                feedback.Last().Text == "未能提交传送。", "a known original mode-2 queue rejection rolls back only the exact provisional payer slot");
                            var rejectedFirst = ReadFrame(stream); var rejectedLast = ReadFrame(stream);
                            Require(rejectedFirst[2] == 154 && rejectedLast[2] == 154, "a rejected native command leaves just its two original fences");
                            Server(); Apply(rejectedFirst, ai); Apply(rejectedLast, ai);
                            Client(clientPlayers, player);
                            NativeQuickItemChecks.Until(() => { Call(host, "PumpConnection", actor); return ((IDictionary)Get(host, "wormholeWindows")).Count == 0; });
                            rejectKind = 5;
                            var flow = Get(actor, "WorldSource");
                            Set(flow, "Requested", true);
                            Call(host, "FlushSource", actor);
                            Require(Recall(host, id) && !Recall(host, id) && body.position == initial && body.inventory[PotionSlot].stack == 1,
                                "secondary submission charges exactly once while authoritative local position waits");
                            Require(((IList)Get(host, "pendingItemSync")).Count == 1 && rejectKind == 0,
                                "a refusal at the actual AI inventory send leaves that exact slot pending");
                            Call(host, "FlushItemSync", true);
                            Require(((IList)Get(host, "pendingItemSync")).Count == 0,
                                "the later original accepted inventory send clears the pending slot without consuming again");
                        }
                        finally { rejectKind = 0; reject.UnpatchAll(reject.Id); }
                        var worldFence = ReadFrame(stream);
                        var begin = ReadFrame(stream); var request = ReadFrame(stream); var end = ReadFrame(stream); var debit = ReadFrame(stream);
                        Require(worldFence[2] == 154 && begin[2] == 154 && request[2] == 65 && request[3] == 2 && request[14] == 3 && end[2] == 154 &&
                            debit[2] == 5 && debit[3] == ai && BitConverter.ToInt16(debit, 4) == PotionSlot,
                            "the AI's actual TCP stream contains two original ordered 154s around original mode 2 and its own inventory frame");
                        Server();
                        foreach (var frame in new[] { worldFence, begin, request, end, debit }) Apply(frame, ai);
                        Require(serverPlayers[ai].position == dest && serverPlayers[player].position == human.position &&
                            serverPlayers[ai].inventory[PotionSlot].stack == 1, "original server applies mode 2 only to sender and independently applies its potion slot");
                        var primaryEcho = NativeCompanionTeamChecks.ReadKind(ownerWire.Client.GetStream(), 65);
                        var observed = NativeCompanionTeamChecks.ReadKind(remoteWire.Client.GetStream(), 65);
                        Client(clientPlayers, player); Apply(primaryEcho, 256);
                        Require(body.position == initial, "primary original broadcast does not apply the owned AI displacement twice");
                        NativeQuickItemChecks.Until(() => { Call(host, "PumpConnection", actor); return feedback.Any(entry => entry.Text.Contains("已传送")); });
                        Require((long)Get(Get(actor, "WorldSource"), "Completed") == 1, "the older native world-snapshot reply keeps its own FIFO marker");
                        Require(body.position == dest && body.teleportStyle == 3 && Main.screenPosition == camera, "original directed decoder moves only the secondary body and leaves the camera alone");
                        var ack = NativeCompanionTeamChecks.ReadKind(stream, 65);
                        Require(ack[3] == 3, "the secondary decoder itself emitted native mode 3");
                        Client(observerPlayers, observer); Apply(observed, 256);
                        Require(observerPlayers[ai].position == dest && observerPlayers[player].position == human.position,
                            "a separately connected observer's original decoder receives the actual AI teleport");
                        Server(); Apply(ack, ai);
                        Require((int)Get(serverPlayers[ai], "unacknowledgedTeleports") == 0, "original server consumes the genuine ACK and releases its position gate");

                        Client(clientPlayers, player);
                        human.position += new Vector2(144, 0);
                        Require(Recall(host, id) && body.inventory[PotionSlot].IsAir, "second explicit command pays the last AI bottle before its delayed response");
                        begin = ReadFrame(stream); request = ReadFrame(stream); end = ReadFrame(stream); debit = ReadFrame(stream);
                        long oldGeneration = (long)Get(actor, "Generation");
                        body.KillMe(PlayerDeathReason.ByCustomReason("isolated recall-generation death"), 999, 0);
                        Require(body.dead && (long)Get(actor, "Generation") != oldGeneration && ReferenceEquals(Get(actor, "Connection"), connection),
                            "real ordinary death renews the activation while retaining body and secondary TCP connection");
                        body.respawnTimer = 0;
                        Call(host, "AfterBody", actor);
                        Require(!body.dead, "actual native after-body consumer revives the retained body before old responses");
                        var respawn = body.position;
                        int completed = feedback.Count(entry => entry.Text.Contains("已传送"));
                        Server();
                        foreach (var frame in new[] { begin, request, end, debit }) Apply(frame, ai);
                        Client(clientPlayers, player);
                        NativeQuickItemChecks.Until(() =>
                        {
                            Call(host, "PumpConnection", actor);
                            return ((IDictionary)Get(host, "wormholeWindows")).Count == 0;
                        });
                        ack = NativeCompanionTeamChecks.ReadKind(stream, 65);
                        Require(ack[3] == 3 && body.position == respawn && feedback.Count(entry => entry.Text.Contains("已传送")) == completed,
                            "known old mode 0 is stopped before native displacement but still emits its native mode 3 ACK on the same transport");
                        Server(); Apply(ack, ai);
                        Require((int)Get(serverPlayers[ai], "unacknowledgedTeleports") == 0, "the stale displacement still releases the server position gate");
                        Client(clientPlayers, player);

                        Put(body.inventory, PotionSlot, 1); Put(human.inventory, PotionSlot, 1);
                        human.position += new Vector2(96, 0);
                        Require(Recall(host, id), "first timed attempt takes the exact AI bottle");
                        begin = ReadFrame(stream); request = ReadFrame(stream); end = ReadFrame(stream); debit = ReadFrame(stream);
                        var windows = (IList)((IDictionary)Get(host, "wormholeWindows"))[actor];
                        Set(windows[0], "Deadline", Stopwatch.GetTimestamp() - 1);
                        Call(host, "PumpConnection", actor);
                        Require(feedback.Last().Text.Contains("超时") && body.inventory[PotionSlot].IsAir && Query(host, id, out _),
                            "the waiting deadline ends the active result without refund or a permanent busy state");
                        Require(Recall(host, id) && human.inventory[PotionSlot].IsAir, "new same-destination action revalidates and takes the actual human fallback bottle");
                        var newBegin = ReadFrame(stream); var newRequest = ReadFrame(stream); var newEnd = ReadFrame(stream);
                        // The owner's socket has the actual 5, never the AI's. The
                        // same-destination response to the retired attempt is read
                        // before the new attempt's first original fence response.
                        var ownerDebit = NativeCompanionTeamChecks.ReadKind(ownerWire.Peer.GetStream(), 5);
                        Require(ownerDebit[3] == player && BitConverter.ToInt16(ownerDebit, 4) == PotionSlot,
                            "human fallback sends the original 5 only on the human's primary TCP connection");
                        Server(); foreach (var frame in new[] { begin, request, end, debit }) Apply(frame, ai);
                        Client(clientPlayers, player);
                        NativeQuickItemChecks.Until(() => { Call(host, "PumpConnection", actor); return windows.Count == 1; });
                        Require(feedback.Count(entry => entry.Text.Contains("已传送")) == completed && body.Bottom == human.Bottom,
                            "late same-generation movement can occur, but it never completes the newer same-position request");
                        var mobility = Get(actor, "Mobility");
                        int movedRevision = (int)Get(mobility, "PositionRevision");
                        ack = NativeCompanionTeamChecks.ReadKind(stream, 65);
                        Server(); Apply(ack, ai); Apply(ownerDebit, player);
                        foreach (var frame in new[] { newBegin, newRequest, newEnd }) Apply(frame, ai);
                        Client(clientPlayers, player);
                        NativeQuickItemChecks.Until(() => { Call(host, "PumpConnection", actor); return windows.Count == 0; });
                        ack = NativeCompanionTeamChecks.ReadKind(stream, 65);
                        Require(ack[3] == 3 && body.inventory[PotionSlot].IsAir && human.inventory[PotionSlot].IsAir,
                            "the newer reply remains an independent native ACK with no debit replay");
                        Require(feedback.Last().Text == "传送结果未确认。" && feedback.Count(entry => entry.Text.Contains("已传送")) == completed &&
                            (int)Get(mobility, "PositionRevision") == movedRevision,
                            "a matching native reply which did not displace the body does not manufacture a completed movement or retire its new navigation");
                        Server(); Apply(ack, ai);
                        Require((int)Get(serverPlayers[ai], "unacknowledgedTeleports") == 0, "both original requests settle their own server movement protection");
                        Client(clientPlayers, player);
                        Set(actor, "Connection", null);
                    }
                }
            }
            finally
            {
                Netplay.Clients = clients; NetMessage.buffer = buffers; Netplay.Connection.Socket = socket; Netplay.Connection.State = clientState;
                Main.player = clientPlayers; Main.myPlayer = clientIndex; Main.netMode = oldMode; Main.dedServ = dedicated; Main.sectionManager = sections;
                Set(actor, "Connection", null);
                listener.Stop();
            }
            Console.WriteLine("PASS actual TCP mode 2/0/3, independent observer, shared native FIFO, exact two payer streams, queue rejection, bounded timeout and same-coordinate separation, and old-activation suppression after real respawn.");
        }

        private static bool Query(object host, Guid id, out string reason)
        {
            object[] args = { id, null };
            bool usable = (bool)Call(host, "QueryManualRecall", args);
            reason = (string)args[1];
            return usable;
        }

        private static bool Recall(object host, Guid id) => (bool)Call(host, "TryManualRecall", id);
        private static void Set(object value, string field, object content) => value.GetType().GetField(field, Fields).SetValue(value, content);
        private static void Put(Item[] items, int slot, int count) => items[slot] = new Item(ItemID.WormholePotion) { stack = count };
        private static Player Replica(Player source) => new Player { whoAmI = source.whoAmI, active = true, name = source.name, team = source.team, position = source.position };

        private static byte[] ReadFrame(NetworkStream stream)
        {
            int low = stream.ReadByte(), high = stream.ReadByte();
            if (low < 0 || high < 0 || (low | high << 8) < 3) throw new EndOfStreamException("Missing original frame header.");
            var frame = new byte[low | high << 8]; frame[0] = (byte)low; frame[1] = (byte)high;
            for (int at = 2; at < frame.Length;)
            {
                int read = stream.Read(frame, at, frame.Length - at);
                if (read == 0) throw new EndOfStreamException("Missing original frame body.");
                at += read;
            }
            return frame;
        }

        private static void Apply(byte[] frame, int sender)
        {
            var buffer = new MessageBuffer { whoAmI = sender };
            Buffer.BlockCopy(frame, 2, buffer.readBuffer, 0, frame.Length - 2);
            buffer.ResetReader();
            try { buffer.GetData(0, frame.Length - 2, out _); }
            catch (Exception e) { throw new InvalidOperationException("Original wormhole fixture frame " + frame[2] + " from slot " + sender, e); }
        }

        private sealed class WirePair : IDisposable
        {
            internal TcpClient Client, Peer;
            internal WirePair()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                try
                {
                    Client = new TcpClient { NoDelay = true };
                    Client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                    Peer = listener.AcceptTcpClient(); Peer.NoDelay = true;
                    Client.GetStream().ReadTimeout = Peer.GetStream().ReadTimeout = 6000;
                }
                finally { listener.Stop(); }
            }
            public void Dispose() { Client.Dispose(); Peer.Dispose(); }
        }
    }
}
