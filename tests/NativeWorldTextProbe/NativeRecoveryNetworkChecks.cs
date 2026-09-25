using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryNetworkChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        private static readonly List<byte[]> packets=new List<byte[]>();
        internal static void Run(object context)
        {
            var patch=new Harmony("JueMingR.Tests.RecoveryPackets");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            patch.Patch(send,postfix:new HarmonyMethod(typeof(NativeRecoveryNetworkChecks).GetMethod(nameof(Capture),Flags)));
            object host=Get(context,"Recovery"),input=Get(context,"Input");var ps=(RecoverySettings)Get(host,"Potions");var ss=(RecoverySettings)Get(host,"Services");var p=Main.LocalPlayer;
            try
            {
                // Native serialization runs; the enclosing fixture guards the
                // final SendPacket outlet. No connection or server is created.
                Netplay.Connection=new RemoteServer();Netplay.Connection.PendingTermination=true;NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;Main.clientPlayer=new Player();
                Call(context,"UpdateRuntime");NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[3].SetDefaults(ItemID.HealingPotion);p.inventory[3].stack=2;p.statLifeMax2=500;p.statLife=300;p.potionDelay=0;
                NativeRecoveryChecks.Save(ps,new RecoveryOptions(1));packets.Clear();NativeRecoveryChecks.Frame(host,input,8500);Sync();
                Require(p.inventory[3].stack==1 && p.statLife==400 && packets.Any(b=>b[2]==5 && BitConverter.ToInt16(b,4)==3) && packets.Any(b=>b[2]==16),"ordinary client uses real native medication and inventory/life serialization");
                NativeRecoveryChecks.Save(ps,new RecoveryOptions());NativeRecoveryChecks.Save(ss,new RecoveryOptions(tax:true));Call(Get(context,"nativeNpcs"),"BeginTick");p.taxMoney=12345;p.SetTalkNPC(-1);Main.npcChatText="";long funds=NativeRecoveryServiceChecks.Total(p);packets.Clear();
                NativeRecoveryChecks.Frame(host,input,8600);var requests=packets.Where(b=>b[2]==21).ToArray();
                Require(p.taxMoney==0 && NativeRecoveryServiceChecks.Total(p)==funds && requests.Length>0 && requests.All(b=>BitConverter.ToInt16(b,3)==400),"client tax uses actual vanilla world-item request slot400 without wallet injection");
                int count=requests.Length;for(ulong t=8601;t<8700;t++)NativeRecoveryChecks.Frame(host,input,t);
                Require(packets.Count(b=>b[2]==21)==count,"no pickup/ACK cannot cause client tax replay");
                NativeRecoveryChecks.Save(ps,new RecoveryOptions(1));Main.ServerSideCharacter=true;p.statLife=300;p.potionDelay=0;p.taxMoney=100;
                NativeRecoveryChecks.Frame(host,input,8800);Require(p.statLife==300 && p.inventory[3].stack==1 && p.taxMoney==100,"SSC refuses local autonomous resource changes");
                Console.WriteLine("PASS G07 client: real packets5/16 and tax21 slot400, no replay and SSC refusal; network outlet isolated, no real server acceptance.");
            }
            finally{Main.ServerSideCharacter=false;Main.netMode=0;patch.Unpatch(send,HarmonyPatchType.All,patch.Id);NativeRecoveryChecks.Save(ps,new RecoveryOptions());NativeRecoveryChecks.Save(ss,new RecoveryOptions());p.taxMoney=0;Call(context,"UpdateRuntime");}
        }
        private static void Sync(){typeof(Main).GetMethod("TrySyncingMyPlayer",Flags).Invoke(null,null);}
        private static void Capture(int __0)
        {if(Main.netMode!=1 || __0!=5 && __0!=16 && __0!=21 && __0!=42)return;byte[] bytes=NetMessage.buffer[256].writeBuffer;int n=BitConverter.ToUInt16(bytes,0);Require(n>=3 && n<=bytes.Length && bytes[2]==__0,"native serialized packet boundary");packets.Add(bytes.Take(n).ToArray());}
    }
}
