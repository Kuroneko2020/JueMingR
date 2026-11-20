using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Net.Sockets;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Three loopback TCP links, the product's real AI byte transport, and
    // original server/client MessageBuffer consumers. Pre-admitted role slots
    // isolate team/inventory semantics; this is not a full game/Steam handshake.
    internal static class NativeCompanionTeamChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static Player captureTarget;
        private static bool failCapture;
        private static void CaptureFailure(Player __0)
        {if(failCapture && ReferenceEquals(__0,captureTarget)){failCapture=false;throw new IOException("isolated team capture failure");}}
        private static bool SkipCosmetic(){return false;}
        private static void Saved(object host,Guid id)
        {NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return (bool)Call(Get(host,"profiles"),"IsSaved",id);});}
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            Main.spawnTileX=60;Main.spawnTileY=58;
            var cosmetics=new Harmony("JueMingR.Tests.TeamNativeCosmetics");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})cosmetics.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeCompanionTeamChecks),nameof(SkipCosmetic)));
            try
            {
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                NativeCompanionTransportChecks.Run();
                var host=Get(context,"Companions");var profiles=Get(host,"profiles");
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(profiles,"Ready");});
                Require((bool)Call(host,"Create","队伍接收甲") && (bool)Call(host,"Create","队伍接收乙"),"native network profiles accepted");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Count()==2;});
                var list=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().ToArray();Guid first=list.Single(p=>p.Name=="队伍接收甲").Id,second=list.Single(p=>p.Name=="队伍接收乙").Id;
                Saved(host,first);Saved(host,second);
                Require((bool)Call(host,"SetTeam",first,2) && (bool)Call(host,"SetTeam",second,3),"offline teams written to native archives");Saved(host,first);Saved(host,second);
                Require((bool)Call(host,"Join",first) && (bool)Call(host,"Join",second),"two native bodies join saved teams");
                var a=(Player)Call(host,"GetBody",first);var b=(Player)Call(host,"GetBody",second);
                Require(a.team==2 && b.team==3,"native offline team state is restored on join");
                WireScene(host,first,a,b);
                Saved(host,first);Require(!(bool)Call(host,"IsJoined",first) && (bool)Call(host,"Join",first),"same archive rejoins after actual wire death and public recall");
                var reloaded=(Player)Call(host,"GetBody",first);
                Require(reloaded.team==5 && reloaded.inventory[42].IsAir && reloaded.inventory[39].IsAir &&
                    reloaded.armor[0].IsAir, "recall saves the unique post-death body and latest team");
            }
            finally {cosmetics.UnpatchAll(cosmetics.Id);Call(Get(context,"Companions"),"OnSessionEnded");}
        }
        private sealed class Pair : IDisposable
        {
            internal readonly TcpListener Listener;
            internal readonly TcpClient Client,Peer;
            internal Pair()
            {
                Listener=new TcpListener(IPAddress.Loopback,0);Listener.Start();Client=new TcpClient{NoDelay=true};
                Client.Connect(IPAddress.Loopback,((IPEndPoint)Listener.LocalEndpoint).Port);Peer=Listener.AcceptTcpClient();Peer.NoDelay=true;
                Client.GetStream().ReadTimeout=Peer.GetStream().ReadTimeout=5000;
            }
            public void Dispose(){Client.Dispose();Peer.Dispose();Listener.Stop();}
        }
        private static void Exact(Stream stream,byte[] bytes,int start,int count)
        {while(count>0){int n=stream.Read(bytes,start,count);if(n==0)throw new EndOfStreamException("native wire closed before expected frame");start+=n;count-=n;}}
        private static byte[] Frame(NetworkStream stream)
        {
            var header=new byte[2];Exact(stream,header,0,2);int length=header[0]|header[1]<<8;
            if(length<3)throw new InvalidDataException("native TCP frame length");var frame=new byte[length];frame[0]=header[0];frame[1]=header[1];Exact(stream,frame,2,length-2);return frame;
        }
        private static List<byte[]> ReadKinds(NetworkStream stream,int itemFrames,int teamFrames)
        {
            var result=new List<byte[]>();int items=0,teams=0;
            for(int n=0;n<64 && (items<itemFrames || teams<teamFrames);n++)
            {var frame=Frame(stream);result.Add(frame);if(frame[2]==5)items++;if(frame[2]==45)teams++;}
            Require(items==itemFrames && teams==teamFrames,"actual TCP stream supplied all native item/team frames");return result;
        }
        private static byte[] ReadKind(NetworkStream stream, byte kind)
        {
            for (int n = 0; n < 512; n++) { var frame = Frame(stream); if (frame[2] == kind) return frame; }
            throw new InvalidDataException("missing native wire frame " + kind);
        }
        private static void Apply(byte[] frame,int sender)
        {var buffer=new MessageBuffer{whoAmI=sender};Buffer.BlockCopy(frame,2,buffer.readBuffer,0,frame.Length-2);buffer.ResetReader();int type;buffer.GetData(0,frame.Length-2,out type);}
        private static Player Replica(Player source)
        {return new Player{whoAmI=source.whoAmI,active=true,name=source.name,team=source.team};}
        private static void Put(Item[] items,int slot,int type,int count)
        {items[slot]=new Item(type){stack=count};}
        private static void WireScene(object host,Guid id,Player actorBody,Player other)
        {
            var human=Main.LocalPlayer;int humanSlot=human.whoAmI,actorSlot=actorBody.whoAmI,otherSlot=other.whoAmI,observerSlot=249;
            Require(observerSlot!=humanSlot && observerSlot!=actorSlot && observerSlot!=otherSlot,"separate native observing slot");
            var actor=Call(host,"Owned",actorBody);var originalActorConnection=GetOptional(actor,"Connection");
            var sourcePlayers=Main.player;var clients=Netplay.Clients;var buffers=NetMessage.buffer;var socket=Netplay.Connection.Socket;
            int mode=Main.netMode,myPlayer=Main.myPlayer,humanTeam=human.team,otherTeam=other.team,primaryState=Netplay.Connection.State;bool dedicated=Main.dedServ;
            var diagnostics=AccessTools.Field(typeof(Main),"_activeNetDiagnosticsUI");var oldDiagnostics=diagnostics.GetValue(null);
            var connectionType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();object connection=null,closed=null;TcpClient aiPeer=null;
            var noNetwork=AccessTools.Method(typeof(NetMessage),"SendPacket");var outlets=new Harmony("JueMingR.Tests.QuickItemOutlets");
            var fault=new Harmony("JueMingR.Tests.TeamCaptureFailure");
            using(var ownerWire=new Pair())using(var observerWire=new Pair())
            try
            {
                if(Convert.ToInt32(Get(Terraria.Net.NetManager.Instance,"_moduleCount"))==0)Terraria.Initializers.NetworkInitializer.Load();
                connection=Activator.CreateInstance(connectionType,Flags,null,new object[]{IPAddress.Loopback.ToString(),((IPEndPoint)listener.LocalEndpoint).Port},null);Call(connection,"Start");
                NativeQuickItemChecks.Until(()=>listener.Pending());aiPeer=listener.AcceptTcpClient();aiPeer.NoDelay=true;aiPeer.GetStream().ReadTimeout=5000;
                closed=Activator.CreateInstance(connectionType,Flags,null,new object[]{new Action<byte[]>(_=>{}),new Action(()=>{})},null);Call(closed,"Disconnect","isolated closed team connection");
                outlets.Unpatch(noNetwork,HarmonyPatchType.Prefix,outlets.Id);
                Main.netMode=1;Main.dedServ=false;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};Netplay.Connection.State=10;
                Netplay.Connection.Socket=new TcpSocket();Set(actor,"Connection",closed);
                captureTarget=actorBody;failCapture=true;
                fault.Patch(AccessTools.Method(Get(host,"profiles").GetType(),"Capture"),prefix:new HarmonyMethod(typeof(NativeCompanionTeamChecks),nameof(CaptureFailure)));
                Require((bool)Call(host,"SetTeam",id,1) && actorBody.team==1 && (bool)Get(actor,"TeamPending") && (bool)Get(actor,"TeamSavePending"),"latest team survives capture failure and silent disconnected publication");
                Require((bool)Call(host,"RetrySave",id) && !(bool)Get(actor,"TeamSavePending") && actorBody.team==1,"retry captures latest native team without replaying another state change");
                Set(actor,"Connection",connection);Call(host,"PublishTeam",actor);
                Require(!(bool)Get(actor,"TeamPending"),"old connection-bound intent cannot post into a replacement connection");
                Require((bool)Call(host,"SetTeam",id,2) && (bool)Get(actor,"TeamPending"),"new intent remains pending while the real primary is disconnected");
                Netplay.Connection.Socket=new TcpSocket(ownerWire.Client);
                Require((bool)Call(host,"SetTeam",id,2) && (bool)Call(host,"SetTeam",id,5) && !(bool)Get(actor,"TeamPending"),"rapid native teams are actually accepted by the AI TCP queue");
                Put(human.inventory,40,ItemID.Wood,7);Put(actorBody.inventory,42,ItemID.DirtBlock,5);human.trashItem=new Item(ItemID.StoneBlock){stack=3};
                actorBody.trashItem=new Item(ItemID.Gel){stack=9};var aiTrash=actorBody.trashItem;
                Require((bool)Call(host,"QuickTrashGesture",id,true,CompanionInventoryArea.Inventory,40) && human.trashItem.type==ItemID.Wood && human.trashItem.stack==7,"owner source commits to the real owner scalar trash");
                Require((bool)Call(host,"QuickTrashGesture",id,false,CompanionInventoryArea.Inventory,42) && actorBody.inventory[42].IsAir && human.trashItem.type==ItemID.DirtBlock && human.trashItem.stack==5 && ReferenceEquals(aiTrash,actorBody.trashItem),"AI source commits to the same owner trash without changing AI trash");
                var primaryFrames=ReadKinds(ownerWire.Peer.GetStream(),3,0);var actorFrames=ReadKinds(aiPeer.GetStream(),1,2);
                var teams=actorFrames.Where(p=>p[2]==45).ToArray();
                Require(teams.Select(p=>(int)p[4]).SequenceEqual(new[]{2,5}) && teams.All(p=>p[3]==actorSlot),"native AI TCP FIFO carries its own team slot and two latest values");
                Require(primaryFrames.Where(p=>p[2]==5).All(p=>p[3]==humanSlot) && actorFrames.Where(p=>p[2]==5).All(p=>p[3]==actorSlot),"actual source streams preserve the two inventory send identities");

                var server=(Player[])sourcePlayers.Clone();var observer=(Player[])sourcePlayers.Clone();
                foreach(var table in new[]{server,observer})
                {
                    table[humanSlot]=Replica(human);table[actorSlot]=Replica(actorBody);table[otherSlot]=Replica(other);table[observerSlot]=new Player{active=true,whoAmI=observerSlot};
                    table[actorSlot].team=0;Put(table[humanSlot].inventory,40,ItemID.Wood,7);Put(table[actorSlot].inventory,42,ItemID.DirtBlock,5);table[humanSlot].trashItem=new Item(ItemID.StoneBlock){stack=3};
                }
                // The original server broadcast loop also reads State on idle
                // slots before testing message type. Native startup creates all
                // RemoteClient entries; null placeholders silently abort the
                // TrySendData broadcast before a later observer can receive it.
                Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i};
                NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                foreach(var pair in new[]{Tuple.Create(humanSlot,ownerWire.Peer),Tuple.Create(actorSlot,aiPeer),Tuple.Create(observerSlot,observerWire.Peer)})
                {Netplay.Clients[pair.Item1]=new RemoteClient{Id=pair.Item1,State=10,Socket=new TcpSocket(pair.Item2)};NetMessage.buffer[pair.Item1].broadcast=true;}
                Main.netMode=2;Main.dedServ=true;Main.myPlayer=255;Main.player=server;
                foreach(var packet in primaryFrames.Where(p=>p[2]==5))Apply(packet,humanSlot);
                foreach(var packet in actorFrames.Where(p=>p[2]==5 || p[2]==45))Apply(packet,actorSlot);
                Require(server[actorSlot].team==5 && server[humanSlot].team==humanTeam && server[otherSlot].team==otherTeam && server[humanSlot].inventory[40].IsAir && server[actorSlot].inventory[42].IsAir && server[humanSlot].trashItem.type==ItemID.DirtBlock && server[humanSlot].trashItem.stack==5,"actual server native consumers converge to both debits, human trash and latest AI team");
                Require(!PlayerItemSlotID.CanRelay[PlayerItemSlotID.TrashItem],"native trash protocol slot is private and is not fabricated as a broadcast");
                var observed=ReadKinds(observerWire.Client.GetStream(),2,2);
                Main.netMode=1;Main.dedServ=false;Main.myPlayer=observerSlot;Main.player=observer;
                foreach(var packet in observed.Where(p=>p[2]==5 || p[2]==45))Apply(packet,256);
                Require(observer[actorSlot].team==5 && observer[otherSlot].team==otherTeam && observer[humanSlot].team==humanTeam && observer[humanSlot].inventory[40].IsAir && observer[actorSlot].inventory[42].IsAir && observer[humanSlot].trashItem.type==ItemID.StoneBlock,"separate actual observer TCP/native consumer gets relay items/team while private trash remains private");
                Main.player=sourcePlayers;Main.myPlayer=myPlayer;
                var echo=ReadKinds(ownerWire.Client.GetStream(),0,1).First(p=>p[2]==45);Apply(echo,256);
                Require(actorBody.team==5 && human.team==humanTeam && other.team==otherTeam,"late old primary team echo cannot revert current owned intent or affect another role");
                Healing(host, actor, actorBody, other, human, observerSlot, sourcePlayers, server, ownerWire.Client, aiPeer);
                SlotGeneration(host, observerSlot, ownerWire);
                DeathThenRecall(host, id, actor, actorBody, sourcePlayers, server, aiPeer, true);
                var ordinaryId = ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p => p.Name == other.name).Id;
                var ordinaryActor = Call(host, "Owned", other);
                var ordinaryConnection = Activator.CreateInstance(connectionType, Flags, null,
                    new object[] { IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port }, null);
                using ((IDisposable)ordinaryConnection)
                {
                    Call(ordinaryConnection, "Start");
                    NativeQuickItemChecks.Until(listener.Pending);
                    using (var ordinaryPeer = listener.AcceptTcpClient())
                    {
                        ordinaryPeer.GetStream().ReadTimeout = 5000;
                        Netplay.Clients[otherSlot] = new RemoteClient { Id = otherSlot, State = 10, Socket = new TcpSocket(ordinaryPeer) };
                        NetMessage.buffer[otherSlot].broadcast = true;
                        Set(ordinaryActor, "Connection", ordinaryConnection);
                        DeathThenRecall(host, ordinaryId, ordinaryActor, other, sourcePlayers, server, ordinaryPeer, false);
                    }
                }
                Console.WriteLine("PASS actual loopback TCP owner+AI source streams -> native server(5/45) -> separate observer native(5/45); rapid teams 2->5/old echo, human private trash and both source debits, capture retry and disconnected/changed-connection intent. Pre-admitted component topology, not full server startup/Steam/gameplay/FPS.");
            }
            finally
            {
                failCapture=false;captureTarget=null;fault.UnpatchAll(fault.Id);
                Main.player=sourcePlayers;Main.myPlayer=myPlayer;Main.netMode=mode;Main.dedServ=dedicated;Netplay.Clients=clients;NetMessage.buffer=buffers;Netplay.Connection.Socket=socket;Netplay.Connection.State=primaryState;
                diagnostics.SetValue(null,oldDiagnostics);Set(actor,"Connection",originalActorConnection);
                if(connection!=null)((IDisposable)connection).Dispose();if(closed!=null)((IDisposable)closed).Dispose();aiPeer?.Dispose();listener.Stop();
                outlets.Patch(noNetwork,prefix:new HarmonyMethod(typeof(NativeQuickItemChecks),"NoNetwork"));
            }
        }
        private static void Healing(object host, object actor, Player source, Player teammate, Player human, int externalSlot,
            Player[] client, Player[] server, TcpClient primary, TcpClient secondary)
        {
            var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var enter = hooks.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic);
            var oldRemote = client[externalSlot];
            var oldMoon = source.moonLeech;
            int primarySlot = Main.myPlayer;
            client[externalSlot] = Replica(server[externalSlot]);
            try
            {
                source.moonLeech = false;
                foreach (var recipient in new[] { source, teammate, human, client[externalSlot] })
                {
                    int slot = recipient.whoAmI;
                    int oldLife = recipient.statLife, oldCap = recipient.statLifeMax2;
                    try
                    {
                        recipient.statLife = server[slot].statLife = 20;
                        recipient.statLifeMax2 = server[slot].statLifeMax2 = 100;
                        var actorFlow = Get(actor, "Connection");
                        if (slot == source.whoAmI)
                        {
                            Set(actor, "NextPublish", 0U);
                            Call(host, "Publish", actor);
                            Require(BitConverter.ToInt16(ReadKind(secondary.GetStream(), 16), 4) == 20,
                                "the real HP baseline is first sent on its normal secondary wire");
                        }
                        var projectile = new Projectile();
                        projectile.SetDefaults(ProjectileID.SpiritHeal);
                        projectile.owner = source.whoAmI;
                        projectile.Center = recipient.Center;
                        projectile.ai[0] = slot;
                        projectile.ai[1] = 7;
                        projectile.active = true;
                        using ((IDisposable)enter.Invoke(null, new object[] { actor, projectile.Center, false })) projectile.AI();
                        var frame = ReadKind(secondary.GetStream(), 66);
                        Require(frame[3] == slot && BitConverter.ToInt16(frame, 4) == 7 && recipient.statLife == 20,
                            "native MP shot emits on its own secondary connection and leaves shared HP to the primary packet");
                        if (slot == source.whoAmI)
                        {
                            Set(actor, "NextPublish", 0U);
                            Call(host, "Publish", actor);
                            Call(actorFlow, "Send", new byte[] { 3, 0, 154 });
                            byte id;
                            do
                            {
                                id = Frame(secondary.GetStream())[2];
                                Require(id != 16, "unchanged absolute HP cannot be sent over the unconsumed heal");
                            } while (id != 154);
                        }

                        Main.player = server; Main.myPlayer = 255; Main.netMode = 2; Main.dedServ = true;
                        if (slot == source.whoAmI) Apply(Packet(154), source.whoAmI);
                        // The same target and amount come separately from an
                        // ordinary external player, in both relative orders.
                        if ((slot & 1) == 0) Apply(frame, externalSlot);
                        Apply(frame, source.whoAmI);
                        if ((slot & 1) != 0) Apply(frame, externalSlot);
                        Require(server[slot].statLife == 34, "native server settles both independent 66 frames");
                        Main.player = client; Main.myPlayer = primarySlot; Main.netMode = 1; Main.dedServ = false;
                        Apply(ReadKind(primary.GetStream(), 66), 256);
                        Require(recipient.statLife == 27, "the primary applies one native heal once");
                        Apply(ReadKind(primary.GetStream(), 66), 256);
                        Require(recipient.statLife == 34, "same-valued independent remote heal is also applied once");
                        if (slot == source.whoAmI)
                        {
                            Set(actor, "NextPublish", 0U);
                            Call(host, "Publish", actor);
                            Require(BitConverter.ToInt16(ReadKind(secondary.GetStream(), 16), 4) == 34,
                                "the changed shared HP is synchronized after primary consumption");
                        }
                    }
                    finally
                    {
                        Main.player = client; Main.myPlayer = primarySlot; Main.netMode = 1; Main.dedServ = false;
                        recipient.statLife = oldLife; recipient.statLifeMax2 = oldCap;
                    }
                }
            }
            finally { client[externalSlot] = oldRemote; source.moonLeech = oldMoon; }
            Console.WriteLine("PASS native style-52 secondary healing through real TCP/server broadcasts and primary decode for self, teammate, human, and external targets with equal independent 66.");
        }
        private static void DeathThenRecall(object host, Guid id, object actor, Player body, Player[] client,
            Player[] server, TcpClient secondary, bool mediumcore)
        {
            var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var enter = hooks.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic);
            var savedDrops = Main.item;
            int originalPrimary = Main.myPlayer;
            int slot = body.whoAmI;
            var wire = secondary.GetStream();
            var previousPing = Packet(154);
            int droppedType = mediumcore ? ItemID.Wood : ItemID.LargeAmethyst;
            int droppedCount = mediumcore ? 19 : 1;
            try
            {
                Main.item = new WorldItem[savedDrops.Length];
                for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new WorldItem { whoAmI = i };
                body.difficulty = server[slot].difficulty = (byte)(mediumcore ? 1 : 0);
                Put(body.inventory, 39, droppedType, droppedCount);
                Put(body.armor, 0, ItemID.CopperHelmet, 1);
                Set(actor, "NextPublish", 0U);
                Call(host, "Publish", actor);
                var flow = Get(actor, "Connection");
                long olderReply = (long)Get(flow, "sentPings") + 1;
                Call(flow, "Send", previousPing);
                // The ordinary periodic snapshot was 19. An immediate native
                // sync of 22 then reaches the wire before the slot returns to
                // 19 within the same six-frame publish window.
                if (mediumcore)
                {
                    body.inventory[39].stack = 22;
                    using ((IDisposable)enter.Invoke(null, new object[] { actor, body.Center, false }))
                        NetMessage.SendData(5, -1, -1, null, slot, 39);
                    body.inventory[39].stack = 19;
                }
                body.KillMe(PlayerDeathReason.ByCustomReason("fixture death before recall"), 999, 0);
                Require(body.dead && body.inventory[39].IsAir && body.armor[0].IsAir == mediumcore,
                    "the local body mutates through the actual difficulty-specific DropItems");
                Require(ReferenceEquals(flow, Get(actor, "Connection")), "ordinary death retains its independent connection for respawn");
                // No server frame is consumed until the public action detaches
                // the body. The live decoder never sees the old ping or death reply.
                Require((bool)Call(host, "Recall", id) && !body.active && !server[slot].dead,
                    "public Recall detaches the body before the native server consumes any death");
                Require((bool)Get(flow, "HasUnconfirmedDeath") && !(bool)Get(flow, "Finished"),
                    "the detached flow remains responsible for the queued death");
                Main.player = server; Main.myPlayer = 255; Main.netMode = 2; Main.dedServ = true;
                var updates = new List<int>();
                int deaths = 0;
                bool sentOldPing = false;
                for (int n = 0; n < 512; n++)
                {
                    byte[] frame = Frame(wire);
                    if (frame[2] == 5)
                    {
                        if (BitConverter.ToInt16(frame, 4) == 39) updates.Add(BitConverter.ToInt16(frame, 6));
                        Apply(frame, slot);
                    }
                    if (frame[2] == 118)
                    {
                        deaths++;
                        var expected = mediumcore ? new[] { 19, 22, 19 } : new[] { 1 };
                        Require(updates.SequenceEqual(expected) && server[slot].inventory[39].stack == droppedCount &&
                            server[slot].armor[0].type == ItemID.CopperHelmet, "server reads current native inventory and equipment before actual 118");
                        Apply(frame, slot);
                        Require(server[slot].dead && server[slot].inventory[39].IsAir &&
                            server[slot].armor[0].IsAir == mediumcore, "native server settles the actual difficulty's property");
                    }
                    if (frame[2] != 154) continue;
                    if (!sentOldPing)
                    {
                        Require(deaths == 0, "outstanding ping is older than this death");
                        Apply(frame, slot); sentOldPing = true;
                        NativeQuickItemChecks.Until(() => (long)Get(flow, "receivedPings") == olderReply);
                        Require(!(bool)Get(flow, "DeathConfirmed") && !(bool)Get(flow, "Finished"),
                            "older native reply cannot close the detached flow");
                        continue;
                    }
                    Require(deaths == 1 && Main.item.Count(item => item.type == droppedType) == 1 &&
                        Main.item.Single(item => item.type == droppedType).stack == droppedCount &&
                        Main.item.Count(item => item.type == ItemID.CopperHelmet) == (mediumcore ? 1 : 0),
                        "the final 154 follows exactly one native death and one set of difficulty-specific drops");
                    Apply(frame, slot);
                    NativeQuickItemChecks.Until(() => (bool)Get(flow, "Finished"));
                    Require((bool)Get(flow, "DeathConfirmed"), "only the post-settlement native reply retires the TCP worker");
                    Console.WriteLine(mediumcore ? "PASS real mediumcore KillMe -> public Recall -> native server inventory and equipment drops -> fresh 154."
                        : "PASS real softcore KillMe -> public Recall -> native server large gem drop -> fresh 154.");
                    return;
                }
                throw new InvalidDataException("native mediumcore death did not emit 118");
            }
            finally
            {
                Main.player = client; Main.myPlayer = originalPrimary; Main.netMode = 1; Main.dedServ = false;
                Main.item = savedDrops;
            }
        }
        private static byte[] Packet(byte id, Action<BinaryWriter> write = null)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write(id);
                write?.Invoke(writer);
                writer.Flush();
                var frame = stream.ToArray();
                frame[0] = (byte)frame.Length;
                frame[1] = (byte)(frame.Length >> 8);
                return frame;
            }
        }
        private static void WritePacket(NetworkStream stream, byte[] frame) => stream.Write(frame, 0, frame.Length);
        private static byte[] Presence(int slot, bool active) => new byte[] { 5, 0, 14, (byte)slot, active ? (byte)1 : (byte)0 };
        private static byte[] Death(int slot) => Packet(118, w =>
        {
            w.Write((byte)slot);
            PlayerDeathReason.ByCustomReason("fixture previous generation").WriteSelfTo(w);
            w.Write((short)999);
            w.Write((byte)1);
            w.Write((byte)0);
        });
        private static void SlotGeneration(object host, int externalSlot, Pair wire)
        {
            for (int phase = 0; phase < 3; phase++)
            {
                int slot = externalSlot - 1 - phase;
                var old = Main.player[slot];
                try
                {
                    Require((bool)Call(host, "Create", "槽接管" + phase), "create the real reservation test profile");
                    var id = (Guid)Get(host, "LastCreatedId");
                    Saved(host, id);
                    Require((bool)Call(host, "Join", id), "normal Join starts the exact secondary");
                    NativeQuickItemChecks.Until(wire.Listener.Pending);
                    using (var secondary = wire.Listener.AcceptTcpClient())
                    {
                        var output = secondary.GetStream();
                        output.ReadTimeout = 5000;
                        var joining = (IDictionary)Get(host, "joiningSlots");
                        var previousBody = new Player { whoAmI = slot, active = true, difficulty = 1, name = "original remote" };
                        previousBody.position = Main.LocalPlayer.position;
                        Put(previousBody.inventory, 39, ItemID.Wood, 7);
                        Main.player[slot] = previousBody;
                        WritePacket(output, Packet(3, w => { w.Write((byte)slot); w.Write(false); }));
                        NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return joining.Contains(slot); });
                        var role = joining[slot];
                        var body = (Player)Get(role, "Body");
                        Put(body.inventory, 39, ItemID.Wood, 23);
                        ReadKind(output, 6);
                        Require(ReferenceEquals(Main.player[slot], previousBody) && Call(host, "OwnedSlot", slot) == null,
                            "header 3 reserves serialization, leaving the primary's original player installed");
                        Apply(Death(slot), 256);
                        Require(previousBody.dead && previousBody.inventory[39].IsAir && !body.dead && body.inventory[39].stack == 23,
                            "original GetData(118) settles the previous remote body without killing the reserved AI or dropping its property");
                        Apply(Presence(slot, false), 256);

                        WritePacket(output, Packet(7, w =>
                        {
                            w.Write(0); w.Write((byte)0); w.Write((byte)0);
                            for (int i = 0; i < 6; i++) w.Write((short)0);
                            w.Write(0); w.Write("isolated takeover world"); w.Write((byte)Main.GameMode);
                            w.Write(Main.ActiveWorldFileData.UniqueId.ToByteArray());
                        }));
                        NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Get(role, "SpawnSent"); });
                        ReadKind(output, 8);
                        WritePacket(output, Packet(49));
                        NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Get(role, "InitialNpcComplete"); });
                        var ping = ReadKind(wire.Peer.GetStream(), 154);
                        Require(!(bool)Get(role, "BirthSubmitted"), "49 starts a primary fence and cannot send 12 ahead of its reply");
                        if (phase != 0)
                        {
                            Apply(ping, 256);
                            Call(host, "Update", 0UL);
                            ReadKind(output, 12);
                            Require(!ReferenceEquals(Main.player[slot], body), "outgoing birth still leaves the primary's remote body intact until 129");
                            if (phase == 2)
                            {
                                WritePacket(output, Packet(129));
                                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return ReferenceEquals(Call(host, "OwnedSlot", slot), role); });
                                Apply(Presence(slot, false), 256);
                                Require(ReferenceEquals(Main.player[slot], body) && body.active && ReferenceEquals(Call(host, "OwnedSlot", slot), role),
                                    "a previous-generation off after the marker and before this on cannot retire the attached AI");
                                Apply(Presence(slot, true), 256);
                                Apply(Death(slot), 256);
                                Require(!body.dead && body.inventory[39].stack == 23, "owned death relay is an echo after 129 and cannot settle twice");
                            }
                            else
                            {
                                Apply(Presence(slot, false), 256);
                                Require(joining.Contains(slot), "a delayed previous off can straddle the primary fence before this join");
                                Apply(Presence(slot, true), 256);
                            }
                        }
                        // Before the fence and between 12/129 a real off/on
                        // still reaches native remote storage. After 129 an
                        // immediate native reuse can omit the separate off.
                        if (phase != 2) Apply(Presence(slot, false), 256);
                        Apply(Presence(slot, true), 256);
                        var successor = Main.player[slot];
                        var remote = new Player
                        {
                            whoAmI = slot, active = true, name = "takeover " + phase,
                            position = Main.LocalPlayer.position + new Microsoft.Xna.Framework.Vector2(24, 0), controlRight = true
                        };
                        Put(remote.armor, 0, ItemID.CopperHelmet, 1);
                        byte[][] frames;
                        Main.player[slot] = remote;
                        try
                        {
                            NetMessage.SendData(4, -1, -1, null, slot);
                            NetMessage.SendData(5, -1, -1, null, slot, PlayerItemSlotID.Armor0);
                            NetMessage.SendData(13, -1, -1, null, slot);
                            frames = new[] { ReadKind(wire.Peer.GetStream(), 4), ReadKind(wire.Peer.GetStream(), 5), ReadKind(wire.Peer.GetStream(), 13) };
                        }
                        finally { Main.player[slot] = successor; }
                        foreach (var frame in frames) Apply(frame, 256);
                        Require(successor.active && !ReferenceEquals(successor, body) && successor.name == remote.name &&
                            successor.armor[0].type == ItemID.CopperHelmet && successor.controlRight && successor.position == remote.position &&
                            Call(host, "OwnedSlot", slot) == null,
                            "the successor's first native join, name, equipment and control retain their own player immediately");
                        secondary.Close();
                        if (phase == 0) Apply(ping, 256);
                        NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !joining.Contains(slot); });
                        Require(ReferenceEquals(Main.player[slot], successor) && successor.active,
                            "late secondary disconnect or marker cannot replace a takeover already consumed by the primary");
                    }
                }
                finally { Main.player[slot] = old; }
            }
            Console.WriteLine("PASS native FIFO slot generations: prior 118, takeover before the fence and 129, and first real successor packets after 129 even without a separate off.");
        }
    }
}
