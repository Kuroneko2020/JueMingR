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
        private static void Recalled(object host,Guid id)
        {Require((bool)Call(host,"Recall",id),"wire fixture recall accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !(bool)Call(host,"IsJoined",id);});}
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
                Saved(host,first);Recalled(host,first);Require((bool)Call(host,"Join",first),"same archive rejoins after actual wire publication");
                var reloaded=(Player)Call(host,"GetBody",first);
                Require(reloaded.team==5 && reloaded.inventory[42].IsAir,"latest team and already trashed source survive archive capture/rejoin");
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
            int mode=Main.netMode,myPlayer=Main.myPlayer,humanTeam=human.team,otherTeam=other.team;bool dedicated=Main.dedServ;
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
                Main.netMode=1;Main.dedServ=false;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};
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
                Console.WriteLine("PASS actual loopback TCP owner+AI source streams -> native server(5/45) -> separate observer native(5/45); rapid teams 2->5/old echo, human private trash and both source debits, capture retry and disconnected/changed-connection intent. Pre-admitted component topology, not full server startup/Steam/gameplay/FPS.");
            }
            finally
            {
                failCapture=false;captureTarget=null;fault.UnpatchAll(fault.Id);
                Main.player=sourcePlayers;Main.myPlayer=myPlayer;Main.netMode=mode;Main.dedServ=dedicated;Netplay.Clients=clients;NetMessage.buffer=buffers;Netplay.Connection.Socket=socket;
                diagnostics.SetValue(null,oldDiagnostics);Set(actor,"Connection",originalActorConnection);
                if(connection!=null)((IDisposable)connection).Dispose();if(closed!=null)((IDisposable)closed).Dispose();aiPeer?.Dispose();listener.Stop();
                outlets.Patch(noNetwork,prefix:new HarmonyMethod(typeof(NativeQuickItemChecks),"NoNetwork"));
            }
        }
    }
}
