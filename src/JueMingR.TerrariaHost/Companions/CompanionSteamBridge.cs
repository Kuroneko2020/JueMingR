using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Net;

namespace JueMingR.TerrariaHost.Companions
{
    // JMR reserves virtual channel 73; vanilla uses 1/2. No Steam DLL is
    // distributed: bind the current game's already-loaded Steamworks API.
    // GUI/dedicated-server receive ownership still needs the owner two-PC card.
    internal sealed class CompanionSteamBridge : IDisposable
    {
        private const int Channel=73,MaximumEnvelope=65535+30;private const uint Magic=0x43524d4a;
        private readonly Dictionary<string,Flow> flows=new Dictionary<string,Flow>();
        private Type steamId,network,matchmaking,user;private object reliable;
        private bool resolved;private ulong lobby;private string error;
        internal string Status {get;private set;}="Steam专用桥尚未验证";
        private sealed class Flow {internal ulong Peer;internal Guid Id;internal CompanionConnection Connection;internal bool Server,Acknowledged;internal DateTime Opened;}
        private object Id(ulong value){return Activator.CreateInstance(steamId,new object[]{value});}
        private static ulong Number(object id){return (ulong)id.GetType().GetField("m_SteamID").GetValue(id);}
        private object Call(Type type,string name,params object[] args){return type.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(x=>x.Name==name && x.GetParameters().Length==args.Length).Invoke(null,args);}
        private bool Resolve()
        {
            if(resolved)return network!=null;resolved=true;
            var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetType("Steamworks.SteamNetworking",false)!=null);
            if(assembly==null)return false;
            steamId=assembly.GetType("Steamworks.CSteamID");network=assembly.GetType("Steamworks.SteamNetworking");matchmaking=assembly.GetType("Steamworks.SteamMatchmaking");user=assembly.GetType("Steamworks.SteamUser");reliable=Enum.Parse(assembly.GetType("Steamworks.EP2PSend"),"k_EP2PSendReliable");return true;
        }
        private ulong Self {get{return Number(Call(user,"GetSteamID"));}}
        private bool Member(ulong peer)
        {
            if(lobby==0)return false;int count=(int)Call(matchmaking,"GetNumLobbyMembers",Id(lobby));
            for(int i=0;i<count;i++)if(Number(Call(matchmaking,"GetLobbyMemberByIndex",Id(lobby),i))==peer)return true;return false;
        }
        private static string Key(ulong peer,Guid stream){return peer.ToString()+":"+stream.ToString("N");}
        internal CompanionConnection Open()
        {
            if(!Resolve() || Main.LobbyId==0)throw new InvalidOperationException("当前Steam会话的队友桥接接口不可用。");
            lobby=Main.LobbyId;ulong peer=Number(Call(matchmaking,"GetLobbyOwner",Id(lobby)));if(peer==Self)throw new InvalidOperationException("房主应使用当前本机服务器TCP入口。");
            var id=Guid.NewGuid();var f=new Flow{Peer=peer,Id=id,Opened=DateTime.UtcNow};
            f.Connection=new CompanionConnection(p=>{try{Send(peer,id,3,p);}catch(Exception failure){FailFlow(peer,id,f,failure.GetBaseException().Message);throw;}},()=>{try{Send(peer,id,4,null);}catch{}finally{flows.Remove(Key(peer,id));}});
            flows.Add(Key(peer,id),f);try{Call(network,"AcceptP2PSessionWithUser",Id(peer));Send(peer,id,2,null);}catch{flows.Remove(Key(peer,id));f.Connection.Disconnect("Steam桥接握手发送失败。");throw;}Status="已发送Steam桥接握手，等待房主游戏进程";return f.Connection;
        }
        private void Send(ulong peer,Guid stream,byte kind,byte[] payload)
        {
            if(network==null || lobby==0)return;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {w.Write(Magic);w.Write((byte)1);w.Write(kind);w.Write(lobby);w.Write(stream.ToByteArray());if(payload!=null)w.Write(payload);var bytes=s.ToArray();if(!(bool)Call(network,"SendP2PPacket",Id(peer),bytes,(uint)bytes.Length,reliable,Channel))throw new IOException("Steam队友通道发送失败。");}
        }
        internal void Pump()
        {
            if(Main.gameMenu || Main.netMode!=1 || Main.LobbyId==0){if(flows.Count!=0)Dispose();return;}
            if(!Resolve())return;
            if(lobby!=0 && lobby!=Main.LobbyId)Dispose();lobby=Main.LobbyId;
            try
            {
                for(int n=0;n<96;n++)
                {
                    object[] available={0u,Channel};if(!(bool)Call(network,"IsP2PPacketAvailable",available))break;uint size=(uint)available[0];
                    // Steam ReadP2PPacket consumes/truncates an oversized message. Read
                    // the bounded header/body first, so a bad head cannot repeat forever.
                    var bytes=new byte[Math.Min(size,(uint)MaximumEnvelope)];object[] receive={bytes,(uint)bytes.Length,0u,Id(0),Channel};if(!(bool)Call(network,"ReadP2PPacket",receive))break;
                    ulong peer=Number(receive[3]);if(!Member(peer))continue;
                    using(var s=new MemoryStream(bytes,0,(int)Math.Min((uint)receive[2],(uint)bytes.Length),false))using(var r=new BinaryReader(s))
                    {
                        if(s.Length<30 || r.ReadUInt32()!=Magic || r.ReadByte()!=1)continue;byte kind=r.ReadByte();if(r.ReadUInt64()!=lobby)continue;var id=new Guid(r.ReadBytes(16));string key=Key(peer,id);Flow f=null;
                        try{if(size>MaximumEnvelope)throw new InvalidDataException("Steam桥接消息过大。");
                        if(kind==2 && !flows.TryGetValue(key,out f))
                        {
                            var endpoint=Netplay.Connection?.Socket?.GetRemoteAddress() as TcpAddress;
                            if(Number(Call(matchmaking,"GetLobbyOwner",Id(lobby)))!=Self || endpoint==null || !System.Net.IPAddress.IsLoopback(endpoint.Address)){Send(peer,id,5,System.Text.Encoding.UTF8.GetBytes("房主游戏进程未发现当前本地TCP服务器。"));continue;}
                            f=new Flow{Peer=peer,Id=id,Server=true,Opened=DateTime.UtcNow,Connection=new CompanionConnection(endpoint.Address.ToString(),endpoint.Port)};
                            flows.Add(key,f);f.Connection.Start();Send(peer,id,1,null);Status="房主游戏进程已建立独立TCP流";
                        }
                        if(!flows.TryGetValue(key,out f))continue;
                        var payload=r.ReadBytes((int)(s.Length-s.Position));
                        if(kind==3){if(f.Server)f.Connection.Send(payload);else f.Connection.ReceiveRelay(payload);}
                        else if(kind==4 || kind==5)
                        {
                            string reason=kind==5?System.Text.Encoding.UTF8.GetString(payload):"Steam队友流已关闭。";
                            if(f.Server){flows.Remove(key);RetireServerConnection(f.Connection,reason);}
                            else f.Connection.Disconnect(reason);
                        }
                        else if(kind==1){f.Acknowledged=true;Status="Steam桥接双向握手已到达创建者";}
                        }catch(Exception failure){FailFlow(peer,id,f,failure.GetBaseException().Message);}
                    }
                }
                foreach(var pair in flows.ToArray())
                {
                    var f=pair.Value;
                    try{
                    if(!Member(f.Peer))
                    {
                        if(f.Server){flows.Remove(pair.Key);RetireServerConnection(f.Connection,"Steam房间成员已离开。");}
                        else f.Connection.Disconnect("Steam房间成员已离开。");
                        continue;
                    }
                    if(f.Server)
                    {
                        for(int n=0;n<96;n++){var payload=f.Connection.Receive();if(payload==null)break;var framed=new byte[payload.Length+2];framed[0]=(byte)framed.Length;framed[1]=(byte)(framed.Length>>8);Buffer.BlockCopy(payload,0,framed,2,payload.Length);Send(f.Peer,f.Id,3,framed);}
                        if(f.Connection.Finished){Send(f.Peer,f.Id,5,System.Text.Encoding.UTF8.GetBytes(f.Connection.Error??"房主TCP连接已关闭。"));f.Connection.Dispose();flows.Remove(pair.Key);}
                    }
                    else if((DateTime.UtcNow-f.Opened).TotalSeconds>15 && !f.Acknowledged)FailFlow(f.Peer,f.Id,f,"Steam桥接握手超时；请确认房主已启用对应决明能力。");
                    }catch(Exception failure){FailFlow(f.Peer,f.Id,f,failure.GetBaseException().Message);}
                }
            }
            catch(Exception e)
            {
                error=e.Message;Status=error;
                foreach(var pair in flows.ToArray())
                {
                    var f=pair.Value;
                    if(f.Server){flows.Remove(pair.Key);RetireServerConnection(f.Connection,error);}
                    else f.Connection.Disconnect(error);
                }
            }
        }
        private void FailFlow(ulong peer,Guid id,Flow flow,string message)
        {
            // A known stream failure never retires unrelated healthy streams.
            if(flow==null)flows.TryGetValue(Key(peer,id),out flow);flows.Remove(Key(peer,id));
            if(flow!=null)
            {
                if(flow.Server)RetireServerConnection(flow.Connection,message);
                else flow.Connection.Disconnect(message);
            }
            try{Send(peer,id,5,System.Text.Encoding.UTF8.GetBytes(message));}catch{}
            Status=message;
        }
        private static void RetireServerConnection(CompanionConnection connection,string reason=null)
        {
            // Steam close can follow an accepted 118 in the same Pump, before
            // the host TCP worker has sent it. Give that worker ownership of
            // the remaining FIFO and its bounded native settlement fence.
            if(connection.HasUnconfirmedDeath)
            {
                try{if(connection.CloseAfterDeath())return;}
                catch(IOException){/* A full queue cannot accept the fence. */}
            }
            if(reason!=null)connection.Disconnect(reason);
            connection.Dispose();
        }
        public void Dispose()
        {foreach(var f in flows.Values.ToArray()){try{Send(f.Peer,f.Id,4,null);}catch{}if(f.Server)RetireServerConnection(f.Connection);else f.Connection.Disconnect("Steam会话已结束。");}flows.Clear();lobby=0;}
    }
}
