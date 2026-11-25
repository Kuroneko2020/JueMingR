using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The loopback peer runs the pinned server MessageBuffer for 1/38 and its
    // real reply writer. Only the post-auth world baseline is a small fixture.
    internal static class NativeCompanionPasswordChecks
    {
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            Main.spawnTileX = 60; Main.spawnTileY = 58;
            Main.leftWorld = Main.topWorld = 0; Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
            for (int x = 2; x < 118; x++) { Main.tile[x, 60].active(true); Main.tile[x, 60].type = TileID.Stone; }
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"), HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            using (var fixture = new Fixture(context))
            {
                const string firstValid = "loopback-first-password";
                const string secondValid = "loopback-new-password";
                fixture.ServerPassword = firstValid;
                var id = fixture.Create("口令初次角色");
                var first = fixture.Start(id, 42);
                fixture.WaitForPrompt(1, out long originalToken);
                OwnerLossBeforeUpdate(context,fixture,originalToken);
                Require(fixture.Submit(originalToken, "loopback-wrong"), "submit one fresh input to the exact awaiting attempt");
                fixture.Authenticate(first, 42, "loopback-wrong", false);
                fixture.WaitForPrompt(1, out long retryToken);
                Require(retryToken != originalToken && fixture.Flow(id) == null && fixture.Slot(42) == null,
                    "a native localization-key rejection closes the old transport before any slot is allocated");
                Require(!fixture.Submit(originalToken, firstValid) && fixture.Submit(retryToken, firstValid), "only the current retry prompt can create the next connection");
                var second = fixture.Accept(42);
                fixture.Authenticate(second, 42, firstValid, true);
                fixture.Joined(second, 42);
                Require(fixture.Ready(id) && !fixture.Prompt(out _, out _), "correct password on a new connection completes the ordinary join");

                var cached = fixture.Create("当前会话缓存角色");
                var cachedFlow = fixture.Start(cached, 43);
                fixture.Authenticate(cachedFlow, 43, firstValid, true);
                fixture.Joined(cachedFlow, 43);
                Require(fixture.Ready(cached) && !fixture.Prompt(out _, out _), "a different character reuses only the accepted session credential after 37");

                var early = fixture.Create("先返回失败角色");
                var late = fixture.Create("晚返回失败角色");
                var pending = fixture.Create("新认证在途时拒绝");
                var earlyFlow = fixture.Start(early, 44);
                var lateFlow = fixture.Start(late, 45);
                var pendingFlow = fixture.Start(pending, 46);
                // Wait for all three real outgoing 38 frames before retiring
                // the credential. A merely accepted TCP connection can still
                // be waiting for its native 37 to reach the game thread.
                byte[] earlySubmission = fixture.ReadCredential(earlyFlow, firstValid);
                byte[] lateSubmission = fixture.ReadCredential(lateFlow, firstValid);
                byte[] pendingSubmission = fixture.ReadCredential(pendingFlow, firstValid);
                fixture.ServerPassword = secondValid;
                fixture.FinishAuthentication(earlyFlow, 44, earlySubmission, false);
                fixture.WaitForPrompt(1, out long newToken);
                Require(fixture.Submit(newToken, secondValid), "latest prompt receives the new password");
                var earlyRetry = fixture.Accept(44);
                fixture.FinishAuthentication(pendingFlow, 46, pendingSubmission, false);
                fixture.WaitForPrompt(1, out _);
                var justChallenged = fixture.Create("新认证在途时挑战");
                var stillOpenChallenge = fixture.Start(justChallenged, 47);
                fixture.WaitForPrompt(2, out _);
                fixture.Authenticate(earlyRetry, 44, secondValid, true);
                fixture.Joined(earlyRetry, 44);
                var parkedRetry = fixture.Accept(46);
                fixture.Authenticate(parkedRetry, 46, secondValid, true);
                fixture.Joined(parkedRetry, 46);
                fixture.Authenticate(stillOpenChallenge, 47, secondValid, true);
                fixture.Joined(stillOpenChallenge, 47);
                fixture.FinishAuthentication(lateFlow, 45, lateSubmission, false);
                NativeQuickItemChecks.Until(() => { fixture.Pump(); return fixture.Pending; });
                var lateRetry = fixture.Accept(45);
                fixture.Authenticate(lateRetry, 45, secondValid, true);
                fixture.Joined(lateRetry, 45);
                Require(fixture.Ready(early) && fixture.Ready(late) && fixture.Ready(pending) && fixture.Ready(justChallenged) && !fixture.Prompt(out _, out _),
                    "an accepted revision resolves already waiting retries and first challenges while later old replies cannot clear it");

                var waitingA = fixture.Create("共享取消角色甲");
                var waitingB = fixture.Create("共享取消角色乙");
                var pendingA = fixture.Start(waitingA, 48);
                var pendingB = fixture.Start(waitingB, 49);
                byte[] sharedA = fixture.ReadCredential(pendingA, secondValid);
                byte[] sharedB = fixture.ReadCredential(pendingB, secondValid);
                fixture.ServerPassword = "loopback-cancelled-batch";
                fixture.FinishAuthentication(pendingA, 48, sharedA, false);
                fixture.FinishAuthentication(pendingB, 49, sharedB, false);
                fixture.WaitForPrompt(2, out long shared);
                Call(fixture.Host, "CancelPassword", shared);
                NativeQuickItemChecks.Until(() => { fixture.Pump(); return !fixture.Joining(waitingA) && !fixture.Joining(waitingB); });
                Require(fixture.Ready(id) && fixture.Ready(cached) && fixture.OwnerConnected && !fixture.Prompt(out _, out _),
                    "one shared cancellation retires only the two waiting attempts while ready teammates and the owner stay connected");

                var refusal = fixture.Create("普通拒绝角色");
                var refusalFlow = fixture.Start(refusal, 50);
                fixture.WaitForPrompt(1, out long refusalToken);
                fixture.Submit(refusalToken, "loopback-refusal");
                fixture.ReadCredential(refusalFlow, "loopback-refusal");
                fixture.RejectNormally(50);
                NativeQuickItemChecks.Until(() => { fixture.Pump(); return !fixture.Joining(refusal); });
                Require(!fixture.Prompt(out _, out _) && ((string)Call(fixture.Host, "Status", refusal)).Contains("普通拒绝"),
                    "an ordinary rejection finishes the join without reopening authentication or losing its reason");

                fixture.AcceptPrimaryPassword();
                var primaryCredential = fixture.Create("真人认证缓存角色");
                var primaryFlow = fixture.Start(primaryCredential, 50);
                fixture.Authenticate(primaryFlow, 50, "loopback-cancelled-batch", true);
                fixture.Joined(primaryFlow, 50);
                Require(fixture.Ready(primaryCredential) && !fixture.Prompt(out _, out _) && fixture.OwnerConnected,
                    "only native primary message 3 publishes the human's accepted current-session password");

                fixture.DelayedLobbyCredential();
            }
            Console.WriteLine("PASS native password: actual TCP 1/37/38/2/3, fresh retry flow, ready join, latest accepted reuse, stale reply, shared cancellation, and ordinary rejection.");
        }

        private static void OwnerLossBeforeUpdate(object context,Fixture fixture,long token)
        {
            var window=Get(Get(context,"Shell"),"CompanionUi");
            var text=Get(window,"textInput");
            var ime=text.GetType().GetField("ime",BindingFlags.Instance|BindingFlags.NonPublic);
            object previous=ime.GetValue(text);
            bool previousSsc=Main.ServerSideCharacter;
            ime.SetValue(text,new NativeCompanionUiChecks.InputIme(ime.FieldType).GetTransparentProxy());
            try
            {
                Call(window,"BeforeInput",true);
                Call(Get(window,"editor"),"Insert","retired-projected-password");
                Require((bool)Get(window,"PasswordPromptVisible") && (bool)Get(window,"OwnsTextToken"),
                    "real pending loopback authentication opens and owns the companion text lease without drawing");
                Main.ServerSideCharacter=true;
                Require(!fixture.Prompt(out _,out _) && !fixture.Submit(token,"retired-projected-password"),
                    "SSC identity retires visible authentication and rejects its earlier submit before Host update");
                Call(window,"BeforeInput",true);
                Require(GetOptional(window,"editor")==null && GetOptional(text,"prepared")==null && GetOptional(text,"bound")==null &&
                    !(bool)Get(window,"OwnsTextToken") && !(bool)Get(window,"PasswordPromptVisible"),
                    "input prefix releases the actual masked buffer and bound lease before the next sample");
                Main.ServerSideCharacter=previousSsc;
                Call(window,"BeforeInput",true);
                Require((string)Get(Get(window,"editor"),"Text")=="" && fixture.Prompt(out var resumed,out _) && resumed==token,
                    "returning to the current original identity can resume the same challenge with a fresh draft");
            }
            finally
            {
                Main.ServerSideCharacter=previousSsc;
                Call(window,"Close");
                ime.SetValue(text,previous);
            }
        }

        internal static void WithChallenge(object context, Action<object> show)
        {
            using (var fixture = new Fixture(context))
            {
                fixture.ServerPassword = "loopback-rendering-challenge";
                var id = fixture.Create("密码界面原生角色");
                fixture.Start(id, 49);
                fixture.WaitForPrompt(1, out long token);
                show(fixture.Host);
                if (fixture.Prompt(out long current, out _) && token == current) Call(fixture.Host, "CancelPassword", token);
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal object Host { get; }
            private readonly object profiles;
            private readonly TcpListener listener;
            private readonly TcpClient primary;
            private readonly TcpClient primaryPeer;
            private readonly List<TcpClient> peers = new List<TcpClient>();
            private readonly Terraria.Net.Sockets.ISocket oldSocket;
            private readonly WorldSections oldSections;
            private readonly RemoteClient[] oldClients;
            private readonly MessageBuffer[] oldBuffers;
            private readonly int oldMode, oldState;
            private readonly bool oldDedicated;
            private readonly Player owner;
            private readonly string oldPassword;
            private readonly Harmony outlets = new Harmony("JueMingR.Tests.QuickItemOutlets");
            private readonly System.Reflection.MethodInfo send = AccessTools.Method(typeof(NetMessage), "SendPacket");

            internal Fixture(object context)
            {
                Host = Get(context, "Companions"); profiles = Get(Host, "profiles");
                if (!(bool)Get(profiles, "Ready")) NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(profiles, "Ready"); });
                owner = Main.LocalPlayer;
                oldSocket = Netplay.Connection.Socket; oldState = Netplay.Connection.State;
                oldMode = Main.netMode; oldDedicated = Main.dedServ; oldSections = Main.sectionManager;
                oldClients = Netplay.Clients; oldBuffers = NetMessage.buffer; oldPassword = Netplay.ServerPassword;
                listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                primary = new TcpClient();
                primary.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                primaryPeer = listener.AcceptTcpClient();
                primaryPeer.GetStream().ReadTimeout = primary.GetStream().ReadTimeout = 5000;
                peers.Add(primaryPeer);
                Main.netMode = 1; Main.dedServ = false;
                Main.sectionManager = new WorldSections((Main.maxTilesX + 199) / 200, (Main.maxTilesY + 149) / 150);
                Main.sectionManager.SetSectionLoaded(0, 0);
                Netplay.Clients = Enumerable.Range(0, 256).Select(i => new RemoteClient { Id = i }).ToArray();
                NetMessage.buffer = Enumerable.Range(0, 257).Select(i => new MessageBuffer { whoAmI = i }).ToArray();
                Netplay.Connection.Socket = new TcpSocket(primary);
                Netplay.Connection.State = 10;
                outlets.Unpatch(send, HarmonyPatchType.Prefix, outlets.Id);
                Call(Host, "BeginPasswordSession");
            }

            internal string ServerPassword { set { Netplay.ServerPassword = value; } }
            internal bool Pending => listener.Pending();
            internal bool OwnerConnected => Netplay.Connection.IsConnected() && ReferenceEquals(owner, Main.LocalPlayer) && !Netplay.Disconnect;
            internal void Pump() => Call(Host, "Update", 0UL);
            internal bool Joining(Guid id) => (bool)Call(Host, "IsJoined", id);
            internal bool Ready(Guid id) => (bool)Call(Host, "CanTransfer", id);
            internal object Slot(int slot) => Call(Host, "OwnedSlot", slot);
            internal object Flow(Guid id)
            {
                var actors = (IDictionary)Get(Host, "actors");
                return actors[id] == null ? null : GetOptional(actors[id], "Connection");
            }

            internal Guid Create(string name)
            {
                Require((bool)Call(Host, "Create", name), "native password fixture owns its actual character");
                var id = (Guid)Get(Host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Pump(); return (bool)Call(profiles, "IsSaved", id); });
                return id;
            }

            internal TcpClient Start(Guid id, int slot)
            {
                Require((bool)Call(Host, "Join", id), "real loopback connection starts: " + Get(Host, "Feedback"));
                return Accept(slot);
            }

            internal TcpClient Accept(int slot)
            {
                NativeQuickItemChecks.Until(listener.Pending);
                var peer = listener.AcceptTcpClient();
                peer.NoDelay = true;
                peer.GetStream().ReadTimeout = 5000;
                peers.Add(peer);
                Netplay.Clients[slot] = Program.CreateRemoteClient(slot, 0, new TcpSocket(peer));
                var message = Read(peer, 1);
                Server(() => Decode(message, slot));
                Require(Netplay.Clients[slot].State == -1, "native message 1 admits this independent challenged stream");
                Pump();
                return peer;
            }

            internal byte[] ReadCredential(TcpClient peer, string expected)
            {
                NativeQuickItemChecks.Until(() => { Pump(); return peer.Available > 0; });
                byte[] frame = Read(peer, 38);
                using (var reader = new BinaryReader(new MemoryStream(frame, 3, frame.Length - 3, false)))
                    Require(reader.ReadString() == expected, "the intended attempt submits its credential after native 37");
                return frame;
            }

            internal void Authenticate(TcpClient peer, int slot, string expected, bool success)
            { FinishAuthentication(peer, slot, ReadCredential(peer, expected), success); }

            internal void FinishAuthentication(TcpClient peer, int slot, byte[] frame, bool success)
            {
                Server(() => Decode(frame, slot));
                var remote = Netplay.Clients[slot];
                if (success)
                    Require(remote.State == 1 && remote.Status == RemoteClient.ConnStatus.Active, "native message 38 produces normal acceptance 3");
                else
                {
                    Require(remote.Status == RemoteClient.ConnStatus.PendingTermination, "native message 38 schedules termination with the original rejection reason");
                    NativeQuickItemChecks.Until(() =>
                    { Pump(); return peer.Client.Poll(0, SelectMode.SelectRead) && peer.Available == 0; });
                    peer.Dispose();
                }
            }

            internal void Joined(TcpClient peer, int slot)
            {
                NativeQuickItemChecks.Until(() => { Pump(); return ((IDictionary)Get(Host, "joiningSlots")).Contains(slot); });
                Read(peer, 6);
                Write(peer, Frame(7, w =>
                {
                    w.Write(0); w.Write((byte)0); w.Write((byte)0);
                    for (int i = 0; i < 6; i++) w.Write((short)0);
                    w.Write(0); w.Write("isolated password world"); w.Write((byte)Main.GameMode);
                    w.Write(Main.ActiveWorldFileData.UniqueId.ToByteArray());
                }));
                NativeQuickItemChecks.Until(() => { Pump(); var actor = ((IDictionary)Get(Host, "joiningSlots"))[slot]; return actor != null && (bool)Get(actor, "SpawnSent"); });
                Read(peer, 8);
                Write(peer, Frame(49));
                var pending = ((IDictionary)Get(Host, "joiningSlots"))[slot];
                NativeQuickItemChecks.Until(() => { Pump(); return (bool)Get(pending, "InitialNpcComplete"); });
                for (int n = 0; n < 32 && !(bool)Get(pending, "PrimarySlotConfirmed"); n++)
                    Decode(Read(primaryPeer, 154), 256);
                Require((bool)Get(pending, "PrimarySlotConfirmed"), "native primary acknowledgement opens this secondary birth");
                Pump();
                Read(peer, 12);
                Write(peer, Frame(129));
                NativeQuickItemChecks.Until(() => { Pump(); return Slot(slot) is object actor && (bool)Get(actor, "Ready"); });
            }

            internal bool Prompt(out long token, out int waiting)
            {
                object[] state = { 0L, 0 };
                bool shown = (bool)Call(Host, "PasswordPromptState", state);
                token = (long)state[0]; waiting = (int)state[1];
                return shown;
            }
            internal void WaitForPrompt(int expected, out long token)
            {
                long found = 0;
                NativeQuickItemChecks.Until(() => { Pump(); return Prompt(out found, out int actual) && actual == expected; });
                token = found;
            }
            internal bool Submit(long token, string text) => (bool)Call(Host, "SubmitPassword", token, text);

            internal void RejectNormally(int slot)
            { Server(() => NetMessage.SendData(2, slot, -1, NetworkText.FromLiteral("普通拒绝：测试退出"))); }

            internal void AcceptPrimaryPassword(bool finish = true)
            {
                int slot = owner.whoAmI;
                Netplay.Connection.State = 1;
                Netplay.Clients[slot] = Program.CreateRemoteClient(slot, -1, new TcpSocket(primaryPeer));
                NetMessage.SendData(38);
                byte[] submitted = Read(primaryPeer, 38);
                Server(() => Decode(submitted, slot));
                Decode(Read(primary, 3), 256);
                Require(Netplay.Connection.State == 3 && ReferenceEquals(Main.LocalPlayer, owner),
                    "the original 3 consumer completes the primary authentication with the same player");
                if (finish) Netplay.Connection.State = 10;
            }

            internal void DelayedLobbyCredential()
            {
                var lobby = Main.LobbyId;
                var worldId = Main.ActiveWorldFileData.UniqueId;
                var socket = Netplay.Connection.Socket;
                try
                {
                    Call(Host, "EndPasswordSession");
                    Main.LobbyId = 0;
                    AcceptPrimaryPassword(false);
                    // Native 38 and 3 above ran through real wire/consumers.
                    // Exercise the first world-data handoff with the state and
                    // newly revealed lobby it supplies to this same socket.
                    Main.LobbyId = 87UL;
                    Netplay.Connection.State = 4;
                    Call(Host, "HumanWorldArrived");
                    Netplay.Connection.State = 10;
                    Call(Host, "BeginPasswordSession");
                    var id = Create("迟到大厅身份的真人口令");
                    var flow = Start(id, 51);
                    Authenticate(flow, 51, "loopback-cancelled-batch", true);
                    Joined(flow, 51);
                    Require(Ready(id) && !Prompt(out _, out _),
                        "initial world data supplies the missing lobby identity and the first companion reuses the accepted human credential");

                    Main.ActiveWorldFileData.UniqueId = Guid.NewGuid();
                    Require(!(bool)Call(Host, "CurrentAuthentication"), "changing world identity on the same socket invalidates credentials");
                    Main.ActiveWorldFileData.UniqueId = worldId;
                    Netplay.Connection.Socket = new TcpSocket();
                    Require(!(bool)Call(Host, "CurrentAuthentication"), "replacing the primary socket cannot reuse credentials");
                    Netplay.Connection.Socket.Close();
                }
                finally
                {
                    Main.LobbyId = lobby;
                    Main.ActiveWorldFileData.UniqueId = worldId;
                    Netplay.Connection.Socket = socket;
                }
            }

            private static void Server(Action action)
            {
                Main.netMode = 2; Main.dedServ = true;
                try { action(); }
                finally { Main.netMode = 1; Main.dedServ = false; }
            }

            public void Dispose()
            {
                Call(Host, "OnSessionEnded");
                foreach (var peer in peers) peer.Dispose();
                primary.Dispose(); listener.Stop();
                Netplay.Connection.Socket = oldSocket; Netplay.Connection.State = oldState;
                Main.netMode = oldMode; Main.dedServ = oldDedicated; Main.sectionManager = oldSections;
                Netplay.Clients = oldClients; NetMessage.buffer = oldBuffers; Netplay.ServerPassword = oldPassword;
                outlets.Patch(send, prefix: new HarmonyMethod(typeof(NativeQuickItemChecks), "NoNetwork"));
                Call(Host, "OnSessionStarted");
            }
        }

        private static void Decode(byte[] frame, int slot)
        {
            var buffer = NetMessage.buffer[slot];
            Buffer.BlockCopy(frame, 2, buffer.readBuffer, 0, frame.Length - 2);
            buffer.ResetReader();
            buffer.GetData(0, frame.Length - 2, out _);
        }

        private static byte[] Read(TcpClient peer, byte kind)
        {
            var stream = peer.GetStream();
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                for (int i = 0; i < 1024; i++)
                {
                    ushort length = reader.ReadUInt16();
                    Require(length >= 3, "real loopback frame length");
                    byte[] payload = reader.ReadBytes(length - 2);
                    Require(payload.Length == length - 2, "real loopback frame preserved");
                    if (payload[0] != kind) continue;
                    var frame = new byte[length];
                    frame[0] = (byte)length; frame[1] = (byte)(length >> 8);
                    Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
                    return frame;
                }
            }
            throw new InvalidDataException("Expected loopback packet was not received.");
        }

        private static byte[] Frame(byte kind, Action<BinaryWriter> contents = null)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0); writer.Write(kind); contents?.Invoke(writer); writer.Flush();
                var data = stream.ToArray();
                data[0] = (byte)data.Length; data[1] = (byte)(data.Length >> 8);
                return data;
            }
        }

        private static void Write(TcpClient peer, byte[] frame)
        { peer.GetStream().Write(frame, 0, frame.Length); }
    }
}
