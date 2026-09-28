using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Net;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal sealed class CombatSocket : ISocket
    {
        internal readonly List<byte[]> Packets=new List<byte[]>();
        internal bool Connected=true,ThrowSend;
        public void AsyncSend(byte[] data,int offset,int size,SocketSendCallback callback,object state=null)
        {if(ThrowSend)throw new InvalidOperationException("isolated send failure");var copy=new byte[size];Array.Copy(data,offset,copy,0,size);Packets.Add(copy);callback?.Invoke(state);}
        public void Close(){Connected=false;}
        public bool IsConnected(){return Connected;}
        public bool IsDataAvailable(){return false;}
        public RemoteAddress GetRemoteAddress(){return null;}
        public void Connect(RemoteAddress address){throw new InvalidOperationException("Real connection prohibited.");}
        public void AsyncReceive(byte[] data,int offset,int size,SocketReceiveCallback callback,object state=null){throw new InvalidOperationException("Real receive prohibited.");}
        public bool StartListening(SocketConnectionAccepted callback){throw new InvalidOperationException("Real listener prohibited.");}
        public void StopListening(){}
    }
    internal static class CombatNetworkFixture
    {
        internal static byte[] Serialize(int type,int slot=0)
        {
            bool pending=Netplay.Connection.PendingTermination;Netplay.Connection.PendingTermination=true;
            try
            {
                NetMessage.buffer[256]=new MessageBuffer();NetMessage.SendData(type,number:slot);
                byte[] bytes=NetMessage.buffer[256].writeBuffer;int size=BitConverter.ToUInt16(bytes,0);
                Require(size>=3 && bytes[2]==type,"native frame serialized "+type);return bytes.Take(size).ToArray();
            }
            finally{Netplay.Connection.PendingTermination=pending;}
        }
        internal static void Receive(byte[] frame,int sender=256)
        {
            var receiver=new MessageBuffer{whoAmI=sender};Array.Copy(frame,receiver.readBuffer,frame.Length);
            int type;receiver.GetData(2,frame.Length-2,out type);Require(type==frame[2],"real native message dispatch");
        }
        internal static void Npc(int slot,byte generation,int type,bool active)
        {
            NPC old=Main.npc[slot];byte[] packet;
            try
            {
                typeof(NPC).GetMethod("NewNPCInstanceInSlot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{slot,generation});
                NPC n=Main.npc[slot];n.SetDefaults(type);n.whoAmI=slot;n.target=0;n.active=active;n.spawnNeedsSyncing=false;n.life=active?n.lifeMax:0;
                packet=Serialize(23,slot);
            }
            finally{Main.npc[slot]=old;}
            Receive(packet);
        }
        internal static void Event(int invasion,bool pumpkin=false,bool snow=false)
        {
            int old=Main.invasionType;bool a=Main.pumpkinMoon,b=Main.snowMoon;byte[] packet;
            try{Main.invasionType=invasion;Main.pumpkinMoon=pumpkin;Main.snowMoon=snow;packet=Serialize(7);}
            finally{Main.invasionType=old;Main.pumpkinMoon=a;Main.snowMoon=b;}
            Receive(packet);
        }
    }
}
