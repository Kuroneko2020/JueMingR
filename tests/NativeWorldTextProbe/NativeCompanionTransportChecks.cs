using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionTransportChecks
    {
        internal static void Run()
        {
            var host=AppDomain.CurrentDomain.GetAssemblies();var assembly=Array.Find(host,a=>a.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection",false)!=null);
            var type=assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            object transport=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{"127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port},null);
            var packet=new byte[]{7,0,5,1,2,3,4};
            var server=Task.Run(()=>{using(var socket=listener.AcceptTcpClient())using(var stream=socket.GetStream()){stream.Write(packet,0,1);stream.Write(packet,1,3);stream.Write(packet,4,3);var read=new byte[7];int count=0;while(count<read.Length){int n=stream.Read(read,count,read.Length-count);if(n==0)throw new EndOfStreamException();count+=n;}return read;}});
            try
            {
                Call(transport,"Start");byte[] received=null;NativeQuickItemChecks.Until(()=>{received=(byte[])Call(transport,"Receive");return received!=null;});
                Require(received.Length==5 && received[0]==5 && received[4]==4,"real TCP split reads preserve exact frame");
                Call(transport,"Send",packet);Require(server.Wait(5000) && System.Linq.Enumerable.SequenceEqual(server.Result,packet),"real independent TCP exact send");
            }
            finally{((IDisposable)transport).Dispose();listener.Stop();}
            SteamFlowFaults(assembly);
        }
        private static void SteamFlowFaults(Assembly assembly)
        {
            var bridge=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionSteamBridge"),true);var flowType=bridge.GetType().GetNestedType("Flow",BindingFlags.NonPublic);
            var flows=(IDictionary)Get(bridge,"flows");var connectionType=assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            Set(bridge,"steamId",typeof(FaultSteamId));Set(bridge,"network",typeof(FaultSteamApi));Set(bridge,"matchmaking",typeof(FaultSteamApi));Set(bridge,"user",typeof(FaultSteamApi));Set(bridge,"reliable",FaultSend.Reliable);Set(bridge,"resolved",true);Set(bridge,"lobby",100UL);
            var bad=Guid.NewGuid();var good=Guid.NewGuid();var sendBad=Guid.NewGuid();
            Func<ulong,Guid,bool,object> add=(peer,id,server)=>
            {
                var c=Activator.CreateInstance(connectionType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{new Action<byte[]>(p=>{}),new Action(()=>{})},null);
                var flow=Activator.CreateInstance(flowType,true);Set(flow,"Peer",peer);Set(flow,"Id",id);Set(flow,"Server",server);Set(flow,"Connection",c);Set(flow,"Opened",DateTime.UtcNow);Set(flow,"Acknowledged",true);flows.Add(peer+":"+id.ToString("N"),flow);return c;
            };
            var badConnection=add(2,bad,false);var healthy=add(3,good,false);var sendFailure=add(2,sendBad,true);
            ulong lobby=Main.LobbyId;int mode=Main.netMode;bool menu=Main.gameMenu;Main.LobbyId=100;Main.netMode=1;Main.gameMenu=false;
            FaultSteamApi.Pending.Clear();FaultSteamApi.Fail=sendBad;FaultSteamApi.Reads=0;
            try
            {
                FaultSteamApi.Pending.Enqueue(Tuple.Create(2UL,Envelope(bad,new byte[70000])));
                FaultSteamApi.Pending.Enqueue(Tuple.Create(3UL,Envelope(good,new byte[]{3,0,154})));
                Call(sendFailure,"ReceiveRelay",new byte[]{3,0,154});Call(bridge,"Pump");
                Require((bool)Get(badConnection,"Finished") && (bool)Get(sendFailure,"Finished") && !(bool)Get(healthy,"Finished") && flows.Count==1,"known oversize/input and P2P-send failures retire only their own real stream state");
                var packet=(byte[])Call(healthy,"Receive");Require(packet!=null && packet.Length==1 && packet[0]==154 && FaultSteamApi.Pending.Count==0 && FaultSteamApi.Reads==2,"bounded oversized P2P head is consumed before healthy following stream continues");
                Console.WriteLine("PASS companion Steam stream fault scope using injected API failure boundary and real bridge/connection queues; no Steam SDK handshake/two-PC acceptance.");
            }
            finally{((IDisposable)bridge).Dispose();Main.LobbyId=lobby;Main.netMode=mode;Main.gameMenu=menu;FaultSteamApi.Pending.Clear();}
        }
        private static byte[] Envelope(Guid stream,byte[] payload)
        {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(0x43524d4aU);w.Write((byte)1);w.Write((byte)3);w.Write(100UL);w.Write(stream.ToByteArray());w.Write(payload);return s.ToArray();}}
        public struct FaultSteamId {public ulong m_SteamID;public FaultSteamId(ulong value){m_SteamID=value;}}
        public enum FaultSend {Reliable}
        // Explicit fault injection at the SDK boundary, not an SDK substitute
        // or evidence of external Steam connectivity/receive ownership.
        public static class FaultSteamApi
        {
            public static readonly Queue<Tuple<ulong,byte[]>> Pending=new Queue<Tuple<ulong,byte[]>>();public static Guid Fail;public static int Reads;
            public static FaultSteamId GetSteamID(){return new FaultSteamId(1);}
            public static FaultSteamId GetLobbyOwner(FaultSteamId lobby){return new FaultSteamId(1);}
            public static int GetNumLobbyMembers(FaultSteamId lobby){return 3;}
            public static FaultSteamId GetLobbyMemberByIndex(FaultSteamId lobby,int index){return new FaultSteamId((ulong)index+1);}
            public static bool IsP2PPacketAvailable(ref uint size,int channel){size=Pending.Count==0?0U:(uint)Pending.Peek().Item2.Length;return Pending.Count!=0;}
            public static bool ReadP2PPacket(byte[] data,uint capacity,ref uint written,ref FaultSteamId peer,int channel)
            {var value=Pending.Dequeue();written=(uint)Math.Min(value.Item2.Length,(int)capacity);Buffer.BlockCopy(value.Item2,0,data,0,(int)written);peer=new FaultSteamId(value.Item1);Reads++;return true;}
            public static bool SendP2PPacket(FaultSteamId peer,byte[] data,uint size,FaultSend mode,int channel)
            {return new Guid(data.Skip(14).Take(16).ToArray())!=Fail;}
        }
    }
}
