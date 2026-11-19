using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace JueMingR.TerrariaHost.Companions
{
    // Transport owns bytes only. Native decoding and all role/world decisions
    // belong to the game thread; a slow consumer faults this one connection.
    internal sealed class CompanionConnection : IDisposable
    {
        private readonly string host; private readonly int port;
        private readonly object gate=new object();
        private readonly Queue<byte[]> incoming=new Queue<byte[]>(),outgoing=new Queue<byte[]>();
        private int incomingBytes,outgoingBytes; private volatile bool stopped,finished;
        private volatile string error; private TcpClient client;
        private readonly Action<byte[]> relay;private readonly Action closeRelay;
        private int deathReplies;
        private long deathDeadline;
        private volatile bool deathConfirmed;
        internal bool Finished {get{return finished;}}
        internal string Error {get{return error;}}
        internal bool IsRelay {get{return relay!=null;}}
        internal bool DeathConfirmed {get{return deathConfirmed;}}
        internal CompanionConnection(string host,int port){this.host=host;this.port=port;}
        internal CompanionConnection(Action<byte[]> relay,Action close){this.relay=relay;closeRelay=close;}
        internal void Start(){if(relay==null)new Thread(Run){IsBackground=true,Name="JMR companion transport"}.Start();}
        internal void Send(byte[] packet)
        { TrySend(packet); }
        internal bool TrySend(byte[] packet)
        {
            if(packet==null || packet.Length<3 || (packet[0]|packet[1]<<8)!=packet.Length)throw new InvalidDataException("Invalid companion frame");
            if(relay!=null){if(stopped || finished || deathDeadline!=0)return false;relay((byte[])packet.Clone());return true;}
            lock(gate){if(stopped || finished || deathDeadline!=0)return false;if(outgoingBytes+packet.Length>4*1024*1024)throw new IOException("队友发送队列已满。");outgoing.Enqueue((byte[])packet.Clone());outgoingBytes+=packet.Length;return true;}
        }
        internal byte[] Receive(){lock(gate){if(incoming.Count==0)return null;var p=incoming.Dequeue();incomingBytes-=p.Length;return p;}}
        internal void ReceiveRelay(byte[] framed)
        {
            if(framed.Length<3 || (framed[0]|framed[1]<<8)!=framed.Length)throw new InvalidDataException("无效队友桥接数据。");
            var p=new byte[framed.Length-2];Buffer.BlockCopy(framed,2,p,0,p.Length);
            lock(gate){if(incomingBytes+p.Length>8*1024*1024)throw new IOException("队友接收队列已满。");incoming.Enqueue(p);incomingBytes+=p.Length;}
        }
        internal void Disconnect(string reason){error=reason;finished=true;}
        internal bool CloseAfterDeath(int previousReplies)
        {
            lock (gate)
            {
                if (!TrySend(Packet(154))) return false;
                deathReplies = previousReplies + 1;
                deathDeadline = Stopwatch.GetTimestamp() + 5 * Stopwatch.Frequency;
                return true;
            }
        }
        internal bool DeathCloseReady()
        {
            lock (gate)
            {
                if (deathDeadline == 0) return false;
                // Nobody decodes gameplay on this detached flow. Older native
                // 154 replies cannot acknowledge the final sequence on it.
                for (int n = 0; n < 96 && deathReplies > 0 && incoming.Count != 0; n++)
                {
                    var packet = Receive();
                    if (packet[0] == 154) deathReplies--;
                    if (packet[0] == 2) return true;
                }
                deathConfirmed = deathReplies == 0;
                return deathConfirmed || Stopwatch.GetTimestamp() >= deathDeadline;
            }
        }
        internal static byte[] Packet(byte id,Action<BinaryWriter> write=null)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((ushort)0);w.Write(id);write?.Invoke(w);w.Flush();var p=s.ToArray();if(p.Length>UInt16.MaxValue)throw new InvalidDataException("Oversized frame");p[0]=(byte)p.Length;p[1]=(byte)(p.Length>>8);return p;}
        }
        private void Run()
        {
            try
            {
                client=new TcpClient();var pending=client.BeginConnect(host,port,null,null);
                using(pending.AsyncWaitHandle){if(!pending.AsyncWaitHandle.WaitOne(5000))throw new TimeoutException("连接服务器超时。");client.EndConnect(pending);}
                var stream=client.GetStream();stream.WriteTimeout=3000;
                var buffer=new byte[65536];int used=0;
                while(!stopped)
                {
                    if(DeathCloseReady())break;
                    byte[] tx=null;lock(gate){if(outgoing.Count!=0){tx=outgoing.Dequeue();outgoingBytes-=tx.Length;}}
                    if(tx!=null)stream.Write(tx,0,tx.Length);
                    if(client.Client.Poll(0,SelectMode.SelectRead) && client.Available==0){DeathCloseReady();break;}
                    if(client.Available>0)
                    {
                        int count=stream.Read(buffer,used,buffer.Length-used);if(count==0)break;used+=count;int offset=0;
                        while(used-offset>=2)
                        {
                            int length=buffer[offset]|buffer[offset+1]<<8;if(length<3)throw new InvalidDataException("服务器返回无效数据。");if(used-offset<length)break;
                            var packet=new byte[length-2];Buffer.BlockCopy(buffer,offset+2,packet,0,packet.Length);
                            lock(gate){if(incomingBytes+packet.Length>8*1024*1024)throw new IOException("队友接收队列已满。");incoming.Enqueue(packet);incomingBytes+=packet.Length;}
                            offset+=length;
                        }
                        if(offset!=0){Buffer.BlockCopy(buffer,offset,buffer,0,used-offset);used-=offset;}
                        if(used==buffer.Length)throw new InvalidDataException("服务器数据帧过大。");
                    }
                    if(tx==null)Thread.Sleep(2);
                }
                if(!stopped && !deathConfirmed)error="队友连接已断开。";
            }
            catch(Exception e){if(!stopped)error=e.Message;}
            finally{try{client?.Close();}catch{}finished=true;lock(gate){outgoing.Clear();outgoingBytes=0;}}
        }
        public void Dispose(){if(stopped)return;stopped=true;closeRelay?.Invoke();try{client?.Close();}catch{}lock(gate){incoming.Clear();outgoing.Clear();incomingBytes=outgoingBytes=0;}}
    }
}
