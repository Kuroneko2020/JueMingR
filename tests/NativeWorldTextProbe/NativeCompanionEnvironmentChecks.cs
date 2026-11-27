using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionEnvironmentChecks
    {
        private const BindingFlags StaticFields = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly FieldInfo PlayerScene = typeof(Main).GetField("_playerSceneMetrics", StaticFields);
        private static Player faultBody;
        private static readonly Dictionary<int, byte[]> RelayedZones = new Dictionary<int, byte[]>();

        internal static void Run(object context)
        {
            Program.RequireOriginalGame(typeof(Player).Assembly);
            NativeFishingChecks.InitializeNativeEnvironment();
            Main.netMode = 0;
            Main.maxTilesX = 640;
            Main.maxTilesY = 640;
            Main.worldSurface = 90;
            Main.rockLayer = 150;
            Main.leftWorld = Main.topWorld = 0;
            Main.rightWorld = Main.maxTilesX * 16;
            Main.bottomWorld = Main.maxTilesY * 16;
            Main.tile = new Tile[Main.maxTilesX, Main.maxTilesY];
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = 0; y < Main.maxTilesY; y++) Main.tile[x, y] = new Tile();
            for (int x = 185; x < 225; x++)
                for (int y = 170; y < 180; y++)
                {
                    Main.tile[x, y].active(true);
                    Main.tile[x, y].type = TileID.Crimstone;
                }
            for (int i = 0; i < Main.player.Length; i++) Main.player[i].active = false;
            for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i].active = false;
            Player human = Main.LocalPlayer, companion = Body(1, 210);
            human.active = true;
            human.Center = new Vector2(198 * 16, 160 * 16);
            Require(SampleOriginalBiome(human).ZoneCrimson && human.ZoneCrimson, "human has original Crimson terrain and Zone");
            var creeper = Main.npc[1];
            creeper.type = NPCID.Creeper;
            creeper.active = true;
            foreach (float phase in new[] { 0f, -1f })
            {
                companion.ZoneCrimson = false;
                var brain = new NPC
                {
                    whoAmI = 0, type = NPCID.BrainofCthulhu, aiStyle = 54,
                    active = true, life = 100, width = 160, height = 110, direction = 1
                };
                brain.ai[0] = phase;
                brain.localAI[0] = 1f; // Brood already present.
                brain.localAI[2] = 1f; // Phase presentation already happened.
                brain.Center = new Vector2(197 * 16, 160 * 16);
                brain.AI();
                Require(brain.target == human.whoAmI && brain.ai[0] == phase && brain.localAI[3] == 0f,
                    "the original Brain pursues a living human on legal Crimson terrain");
                brain.Center = new Vector2(211 * 16, 160 * 16);
                brain.AI();
                Require(brain.target == companion.whoAmI && brain.ai[0] == 2f && brain.localAI[3] == 1f && brain.alpha == 10,
                    "moving the Brain makes its real AI pick the live companion, whose false Crimson flag is sufficient for retreat");
                Console.WriteLine("Brain AI54 phase={0} target={1} sameBody={2} active={3} dead={4} ghost={5} crimson={6} state={7} retreat={8}",
                    phase, brain.target, ReferenceEquals(Main.player[brain.target], companion), companion.active, companion.dead,
                    companion.ghost, companion.ZoneCrimson, brain.ai[0], brain.localAI[3]);
                Require(SampleOriginalBiome(companion).ZoneCrimson && companion.ZoneCrimson,
                    "the target body's separate original scan also detects Crimson");
                brain.ai[0] = phase;
                brain.AI();
                Require(brain.target == companion.whoAmI && brain.ai[0] == phase && brain.localAI[3] == 0f,
                    "the same selected body's valid Zone prevents original retreat without altering targeting");
            }
            companion.active = false;
            RunActors(context, human);
        }

        private static void RunActors(object context, Player human)
        {
            var audit = new Harmony("JueMingR.Tests.CompanionEnvironment");
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"),
                HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                audit.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper), name),
                    prefix: new HarmonyMethod(typeof(NativeCompanionEnvironmentChecks), nameof(SkipOutlet)));
            var host = Get(context, "Companions");
            var profiles = Get(host, "profiles");
            Main.spawnTileX = 210;
            Main.spawnTileY = 163;
            for (int x = 150; x < 610; x++)
            {
                Main.tile[x, 164].active(true);
                Main.tile[x, 164].type = TileID.Stone;
            }
            Terraria.UI.ItemSorting.SetupWhiteLists();
            var oldScene = Main.PlayerSceneMetrics;
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(host, "Update", 0UL); return (bool)Get(profiles, "Ready"); });
                var first = Join(host, profiles, "环境队友甲");
                var second = Join(host, profiles, "环境队友乙");
                var firstBody = (Player)Call(host, "GetBody", first.Id);
                var secondBody = (Player)Call(host, "GetBody", second.Id);
                object firstActor = Call(host, "Owned", firstBody);
                object secondActor = Call(host, "Owned", secondBody);
                Require(firstBody.ZoneCrimson && secondBody.ZoneCrimson, "Join projects private native Crimson before the first body step");
                human.Center = new Vector2(345 * 16, 160 * 16);
                firstBody.Center = new Vector2(210 * 16, 160 * 16);
                secondBody.Center = new Vector2(475 * 16, 160 * 16);
                Refresh(host, firstActor);
                Refresh(host, secondActor);
                Require(firstBody.ZoneCrimson && !secondBody.ZoneCrimson && ReferenceEquals(oldScene, Main.PlayerSceneMetrics),
                    "two actor scenes stay independent of each other and the human scene");
                CheckBrain(firstBody, true);

                var environment = Get(firstActor, "Environment");
                uint scan = (uint)Get(environment, "ScanAt");
                for (int i = 0; i < 15; i++)
                {
                    NativeQuickItemChecks.BeginWorldStep();
                    Refresh(host, firstActor);
                    Refresh(host, firstActor);
                }
                Require((uint)Get(environment, "ScanAt") == scan, "stationary body and repeated consumers do not rescan the area");
                Require((bool)Call(host, "SetFishing", first.OwnerId, first.Id, true), "actor enters the real fishing activity");
                Refresh(host, firstActor);
                Require((bool)Call(host, "SetFishing", first.OwnerId, first.Id, false), "actor leaves the real fishing activity");
                Refresh(host, firstActor);
                Require((uint)Get(environment, "ScanAt") == scan && firstBody.ZoneCrimson,
                    "fishing activity transitions reuse the live native body scene");

                CheckNetwork(host, firstActor, firstBody, human, audit);
                CheckOtherConsumers(host, secondActor, secondBody);

                firstBody.Center = new Vector2(395 * 16, 160 * 16);
                Refresh(host, firstActor);
                Require(!firstBody.ZoneCrimson, "actual movement out of Crimson computes false");
                CheckBrain(firstBody, false);
                firstBody.Teleport(new Vector2(210 * 16 - firstBody.width / 2, 160 * 16 - firstBody.height / 2), 10);
                Require(firstBody.ZoneCrimson, "native teleport immediately reprojects at the new destination");
                CheckBrain(firstBody, true);

                CheckRevival(firstBody, firstActor);

                for (int x = 185; x < 225; x++)
                    for (int y = 170; y < 180; y++) Main.tile[x, y].ClearEverything();
                for (int i = 0; i < 61; i++) NativeQuickItemChecks.BeginWorldStep();
                Refresh(host, firstActor);
                Require(!firstBody.ZoneCrimson, "ordinary in-place terrain conversion invalidates the resting sample");
                CheckBrain(firstBody, false);

                var savedSlot = firstBody.whoAmI;
                firstBody.inventory[10].SetDefaults(ItemID.Wood);
                firstBody.inventory[10].stack = 23;
                faultBody = firstBody;
                audit.Patch(AccessTools.Method(typeof(Player), nameof(Player.Update), new[] { typeof(int) }),
                    prefix: new HarmonyMethod(typeof(NativeCompanionEnvironmentChecks), nameof(ThrowOnNativeBodyUpdate)) { priority = Priority.Last });
                NativeQuickItemChecks.BeginWorldStep();
                firstBody.Update(savedSlot);
                Require(!firstBody.active && !ReferenceEquals(Main.player[savedSlot], firstBody) && Call(host, "Owned", firstBody) == null &&
                    (bool)Get(firstActor, "Closing"), "a faulted non-fishing body detaches before the next NPC pass");
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(profiles, "IsSaved", first.Id); });
                var saved = (Player)Call(profiles, "LoadBody", Call(host, "Find", first.Id));
                Require(saved.inventory[10].type == ItemID.Wood && saved.inventory[10].stack == 23,
                    "ordinary fault retirement saves the actual held resources");
                Console.WriteLine("PASS native actor biome placement, movement, scene ownership, real fishing transition, conversion and fault retirement.");
            }
            finally
            {
                faultBody = null;
                audit.UnpatchAll(audit.Id);
                Call(host, "OnSessionEnded");
            }
        }

        private static CompanionProfile Join(object host, object profiles, string name)
        {
            Require((bool)Call(host, "Create", name), "create isolated companion profile: " + Get(host, "Feedback"));
            Guid id = (Guid)Get(host, "LastCreatedId");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(profiles, "IsSaved", id); });
            Require((bool)Call(host, "Join", id), "join isolated companion through the normal entry: " + Get(host, "Feedback"));
            return (CompanionProfile)Call(host, "Find", id);
        }

        private static bool SkipOutlet() => false;

        private static void ThrowOnNativeBodyUpdate(Player __instance)
        {
            if (ReferenceEquals(__instance, faultBody)) throw new InvalidOperationException("isolated actor native update fault");
        }

        private static void Refresh(object host, object actor) =>
            Require((bool)Call(host, "RefreshActorEnvironment", actor), "actor is in a fully loaded native environment");

        private static void CheckBrain(Player body, bool crimson)
        {
            foreach (float phase in new[] { 0f, -1f })
            {
                var brain = new NPC { whoAmI = 0, type = NPCID.BrainofCthulhu, aiStyle = 54, active = true, life = 100, width = 160, height = 110 };
                brain.ai[0] = phase;
                brain.localAI[0] = brain.localAI[2] = 1;
                brain.Center = body.Center + new Vector2(16, 0);
                brain.AI();
                Require(brain.target == body.whoAmI && brain.ai[0] == (crimson ? phase : 2f) && brain.localAI[3] == (crimson ? 0f : 1f),
                    "native Brain chooses this body and applies its actual environment at phase " + phase);
            }
        }

        private static void CheckNetwork(object host, object actor, Player body, Player human, Harmony audit)
        {
            var previousSections = Main.sectionManager;
            var previousBuffers = NetMessage.buffer;
            var previousClients = Netplay.Clients;
            var previousSocket = Netplay.Connection.Socket;
            int previousState = Netplay.Connection.State;
            bool previousTermination = Netplay.Connection.PendingTermination;
            int previousWidth = Main.maxSectionsX, previousHeight = Main.maxSectionsY;
            int clientSlot = Main.myPlayer, actorSlot = body.whoAmI;
            var sent = new List<byte[]>();
            var kind = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection", true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object connection = Activator.CreateInstance(kind, flags, null, new object[] { new Action<byte[]>(sent.Add), new Action(() => { }) }, null);
            object replacement = Activator.CreateInstance(kind, flags, null, new object[] { new Action<byte[]>(sent.Add), new Action(() => { }) }, null);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using (var client = new TcpClient())
            {
                client.Connect((IPEndPoint)listener.LocalEndpoint);
                using (var peer = listener.AcceptTcpClient())
                try
                {
                    audit.Patch(AccessTools.Method(typeof(NetMessage), "SendPacket"), prefix: new HarmonyMethod(typeof(NativeCompanionEnvironmentChecks), nameof(CaptureRelay))
                    { priority = Priority.First, before = new[] { "JueMingR.Tests.QuickItemOutlets" } });
                    Main.maxSectionsX = 4;
                    Main.maxSectionsY = 5;
                    Main.sectionManager = new WorldSections(4, 5);
                    Main.sectionManager.SetAllSectionsLoaded();
                    NetMessage.buffer = Enumerable.Range(0, 257).Select(i => new MessageBuffer { whoAmI = i }).ToArray();
                    Netplay.Clients = new RemoteClient[256];
                    foreach (int slot in new[] { clientSlot, actorSlot })
                    {
                        Netplay.Clients[slot] = Program.CreateRemoteClient(slot, 10, new TcpSocket(client));
                        NetMessage.buffer[slot].broadcast = true;
                    }
                    Main.netMode = 1;
                    Netplay.Connection.Socket = new TcpSocket(client);
                    Netplay.Connection.State = 10;
                    Netplay.Connection.PendingTermination = false;
                    Set(actor, "Connection", connection);
                    Call(host, "Publish", actor);
                    var zone = sent.Single(f => f[2] == MessageID.SyncPlayerZone);
                    Call(host, "Publish", actor);
                    Require(sent.Count(f => f[2] == MessageID.SyncPlayerZone) == 1, "unchanged zone payload is not sent twice on its connection");

                    var serverBody = new Player { whoAmI = actorSlot, active = true, width = body.width, height = body.height };
                    serverBody.Center = body.Center;
                    Main.netMode = 2;
                    Main.myPlayer = 255;
                    Main.player[actorSlot] = serverBody;
                    RelayedZones.Clear();
                    Decode(NetMessage.buffer[actorSlot], zone);
                    Require(serverBody.ZoneCrimson && RelayedZones.ContainsKey(clientSlot) && !RelayedZones.ContainsKey(actorSlot),
                        "original server consumes the companion's 36 on its own connection and relays to the primary only");
                    CheckBrain(serverBody, true);
                    byte[] echo = RelayedZones[clientSlot];

                    Main.netMode = 1;
                    Main.myPlayer = clientSlot;
                    Main.player[actorSlot] = body;
                    body.Center = new Vector2(395 * 16, 160 * 16);
                    Refresh(host, actor);
                    Require(!body.ZoneCrimson, "local native scope reads the actual new terrain after crossing sections");
                    Decode(NetMessage.buffer[256], echo);
                    Require(!body.ZoneCrimson, "late original server echo cannot replace the owned body's newer independent environment");
                    Call(host, "Publish", actor);
                    byte[] changed = sent.Last(f => f[2] == MessageID.SyncPlayerZone);
                    Require(!ReferenceEquals(changed, zone), "a real environment change publishes the latest original payload");

                    Main.netMode = 2;
                    Main.myPlayer = 255;
                    serverBody.Center = body.Center;
                    Main.player[actorSlot] = serverBody;
                    Decode(NetMessage.buffer[actorSlot], changed);
                    Require(!serverBody.ZoneCrimson, "original server drops Crimson after receiving the new location's payload");
                    CheckBrain(serverBody, false);

                    Main.netMode = 1;
                    Main.myPlayer = clientSlot;
                    Main.player[actorSlot] = body;
                    Set(actor, "Connection", replacement);
                    int before = sent.Count(f => f[2] == MessageID.SyncPlayerZone);
                    Call(host, "Publish", actor);
                    Require(sent.Count(f => f[2] == MessageID.SyncPlayerZone) == before + 1,
                        "replacement connection receives a fresh baseline even when numeric slot and zone bytes match");

                    Main.sectionManager = new WorldSections(4, 5);
                    body.Center = new Vector2(210 * 16, 160 * 16);
                    Require(!(bool)Call(host, "RefreshActorEnvironment", actor), "incomplete primary section footprint is unknown");
                    before = sent.Count(f => f[2] == MessageID.SyncPlayerZone);
                    Call(host, "Publish", actor);
                    Require(sent.Count(f => f[2] == MessageID.SyncPlayerZone) == before, "unknown terrain does not publish false biome values");
                    Main.sectionManager.SetAllSectionsLoaded();
                    for (int i = 0; i < 13; i++) NativeQuickItemChecks.BeginWorldStep();
                    Refresh(host, actor);
                    Call(host, "Publish", actor);
                    Require(body.ZoneCrimson && sent.Count(f => f[2] == MessageID.SyncPlayerZone) == before + 1,
                        "loaded primary terrain awakens the actor's native environment and publishes its new truth");
                    Console.WriteLine("PASS isolated original server message 36 decode/relay, native Boss use, own late echo, reconnected baseline, and section recovery.");
                }
                finally
                {
                    Main.netMode = 0;
                    Main.myPlayer = clientSlot;
                    Main.player[actorSlot] = body;
                    Main.player[clientSlot] = human;
                    Set(actor, "Connection", null);
                    Call(connection, "Dispose");
                    Call(replacement, "Dispose");
                    Main.sectionManager = previousSections;
                    Main.maxSectionsX = previousWidth;
                    Main.maxSectionsY = previousHeight;
                    NetMessage.buffer = previousBuffers;
                    Netplay.Clients = previousClients;
                    Netplay.Connection.Socket = previousSocket;
                    Netplay.Connection.State = previousState;
                    Netplay.Connection.PendingTermination = previousTermination;
                    listener.Stop();
                }
            }
        }

        private static bool CaptureRelay(byte[] data, int remoteClient)
        {
            if (Main.netMode != 2 || data[2] != MessageID.SyncPlayerZone) return true;
            RelayedZones[remoteClient] = data.Take(BitConverter.ToUInt16(data, 0)).ToArray();
            return false;
        }

        private static void Decode(MessageBuffer buffer, byte[] frame)
        {
            Buffer.BlockCopy(frame, 2, buffer.readBuffer, 0, frame.Length - 2);
            buffer.ResetReader();
            buffer.GetData(0, frame.Length - 2, out int type);
            Require(type == MessageID.SyncPlayerZone, "original reader processed SyncPlayerZone");
        }

        private static void CheckOtherConsumers(object host, object actor, Player body)
        {
            Player human = Main.LocalPlayer;
            Vector2 humanPosition = human.position;
            human.Center = new Vector2(600 * 16, 159 * 16);
            for (int x = 430; x < 465; x++)
                for (int y = 178; y < 188; y++)
                {
                    Main.tile[x, y].type = TileID.JungleGrass;
                    Main.tile[x, y].active(true);
                }
            body.Center = new Vector2(450 * 16, 159 * 16);
            Refresh(host, actor);
            Require(body.ZoneJungle && body.ZoneRockLayerHeight, "ordinary independent scan projects underground Jungle");
            var hook = Main.npc[2];
            hook.active = true;
            hook.aiStyle = 52;
            hook.Center = body.Center + new Vector2(80, 0);
            var plant = new NPC { whoAmI = 0, type = NPCID.Plantera, aiStyle = 51, active = true, life = 1000, lifeMax = 1000, width = 75, height = 75 };
            plant.localAI[0] = 1;
            plant.Center = body.Center + new Vector2(120, 0);
            plant.AI();
            float jungle = Math.Abs(plant.velocity.X);
            Require(plant.target == body.whoAmI && jungle > 0f, "native Plantera targets the live Jungle body");
            body.Center = new Vector2(340 * 16, 159 * 16);
            Refresh(host, actor);
            plant.Center = body.Center + new Vector2(120, 0);
            hook.Center = body.Center + new Vector2(80, 0);
            plant.velocity = Vector2.Zero;
            plant.AI();
            Require(!body.ZoneJungle && Math.Abs(plant.velocity.X) > jungle, "native Plantera enrages on the same body after it really leaves Jungle");
            hook.active = false;

            Main.tile[340, 159].wall = WallID.LihzahrdBrickUnsafe;
            NativeQuickItemChecks.BeginWorldStep();
            Refresh(host, actor);
            Require(body.ZoneLihzhardTemple, "native environment detects the Temple wall even without entering Jungle");
            float temple = GolemCadence(body);
            Main.tile[340, 159].wall = 0;
            NativeQuickItemChecks.BeginWorldStep();
            Refresh(host, actor);
            Require(!body.ZoneLihzhardTemple && GolemCadence(body) > temple, "native Golem cadence changes as the body actually leaves the Temple wall");

            for (int x = 516; x < 576; x++)
                for (int y = 175; y < 205; y++)
                {
                    Main.tile[x, y].type = TileID.HardenedSand;
                    Main.tile[x, y].active(true);
                }
            Main.tile[545, 159].wall = WallID.DesertFossil;
            body.Center = new Vector2(545 * 16, 159 * 16);
            Refresh(host, actor);
            Require(body.ZoneDesert && body.ZoneUndergroundDesert, "ordinary actor receives the original underground-desert flag used by Tomb Crawlers");
            human.position = humanPosition;
            Console.WriteLine("PASS shared actor environment consumers Plantera, Golem, and native underground-desert classification.");
        }

        private static void CheckRevival(Player body, object actor)
        {
            long before = (long)Get(actor, "Generation");
            body.KillMe(PlayerDeathReason.ByCustomReason("isolated native actor death"), 999, 0);
            Require(body.dead && (long)Get(actor, "Generation") != before, "real native death renews the actor activation");
            var npc = new NPC { type = NPCID.EyeofCthulhu, width = 30, height = 30, direction = 1 };
            npc.Center = body.Center;
            npc.TargetClosest();
            Require(npc.target != body.whoAmI, "ordinary native targeting excludes the truly dead actor");
            // Start at the tail of the real respawn clock. Vanilla still
            // consumes UpdateDead and the host's ordinary revival path.
            body.respawnTimer = 1;
            for (int i = 0; i < 3 && body.dead; i++)
            {
                NativeQuickItemChecks.BeginWorldStep();
                body.Update(body.whoAmI);
            }
            Require(body.active && !body.dead && body.ZoneCrimson, "native revival fills the renewed body's biome at its real spawn position");
            CheckBrain(body, true);
        }

        private static float GolemCadence(Player body)
        {
            var golem = new NPC { whoAmI = 0, type = NPCID.Golem, aiStyle = 45, active = true, life = 5000, lifeMax = 5000,
                width = 80, height = 80, target = body.whoAmI };
            golem.localAI[0] = 1;
            golem.ai[1] = 10;
            golem.Center = body.Center + new Vector2(120, 0);
            golem.AI();
            return golem.ai[1];
        }

        private static Player Body(int slot, int tileX)
        {
            var body = new Player { whoAmI = slot, active = true, width = 20, height = 42 };
            body.Center = new Vector2(tileX * 16, 160 * 16);
            Main.player[slot] = body;
            return body;
        }

        private static SceneMetrics SampleOriginalBiome(Player body)
        {
            var scene = new SceneMetrics();
            scene.Scan(new SceneMetricsScanSettings { BiomeScanCenterPositionInWorld = body.Center, PerspectivePlayer = body });
            object humanScene = PlayerScene.GetValue(null);
            try
            {
                PlayerScene.SetValue(null, scene);
                body.UpdateBiomes();
            }
            finally { PlayerScene.SetValue(null, humanScene); }
            return scene;
        }
    }
}
