using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingNetworkChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly List<byte[]> packets=new List<byte[]>();
        internal static void Rename(object context,object owner)
        {
            var audit=new Harmony("JueMingR.Tests.RenameNetwork");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];var p=Main.LocalPlayer;var file=Main.ActivePlayerFileData;
            audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeFishingNetworkChecks),nameof(Capture)));
            try
            {
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;Call(context,"UpdateRuntime");packets.Clear();
                Require((bool)Get(owner,"CanRename") && (bool)Call(owner,"Rename","多人改名009") && p.name=="多人改名009" && (bool)Get(owner,"PersistedVerified"),"multiplayer runs real native rename and verifies the isolated local player file");
                Require(ReferenceEquals(Main.ActivePlayerFileData,file) && ReferenceEquals(file.Player,p) && Player.LoadPlayer(file.Path,false).Player.name==p.name,"multiplayer rename retains the actual file and player identity");
                var packet=packets.Single(b=>b[2]==4);
                using(var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(packet)))
                {reader.ReadInt16();Require(reader.ReadByte()==4 && reader.ReadByte()==p.whoAmI,"rename serializes the original player-info message and local index");reader.ReadByte();reader.ReadByte();reader.ReadSingle();reader.ReadByte();Require(reader.ReadString()==p.name,"original player-info packet contains the new full name");}
                packets.Clear();Require(!(bool)Call(owner,"Rename",new string('x',21)) && packets.Count==0,"invalid input emits no rename packet");
                Main.ServerSideCharacter=true;var bytes=System.IO.File.ReadAllBytes(file.Path);
                Require((bool)Get(owner,"CanRename") && !(bool)Call(owner,"Rename","服务器角色") && p.name=="服务器角色" && !(bool)Get(owner,"PersistedVerified") && System.IO.File.ReadAllBytes(file.Path).SequenceEqual(bytes),"SSC may attempt rename; native skipped save remains an accurate partial result");
                Require(packets.Count(b=>b[2]==4)==1,"applied SSC runtime name sends one notification without claiming save/server acknowledgement");
                Console.WriteLine("PASS G10 multiplayer local rename/save and original message4 serialization; SSC partial result, no real server or cloud connection.");
            }
            finally{Main.ServerSideCharacter=false;Main.netMode=0;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;audit.Unpatch(send,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");packets.Clear();}
        }
        internal static void Equipment(object context,Func<Player> reset,Action<FishingOptions> cast,Action stop)
        {
            var audit=new Harmony("JueMingR.Tests.FishingNetwork");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeFishingNetworkChecks),nameof(Capture)));
            var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];var baseline=Main.clientPlayer;
            try
            {
                // Original serialization runs, but no server/socket exists.
                // The enclosing fixture also rejects every final SendPacket.
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;Call(context,"UpdateRuntime");
                var p=reset();p.armor[0].SetDefaults(ItemID.CopperHelmet);p.inventory[12].SetDefaults(5591);Main.clientPlayer=new Player();Sync();packets.Clear();
                cast(new FishingOptions(equipment:true));Sync();
                Require(p.armor[0].type==5591 && p.inventory[12].type==ItemID.CopperHelmet,"client applies the same real two-reference exchange");
                Slot(12,p.inventory[12]);Slot(59,p.armor[0]);packets.Clear();Sync();Require(!packets.Any(b=>b[2]==5),"unchanged second native synchronization emits no duplicate equipment differences");
                stop();Sync();Slot(12,p.inventory[12]);Slot(59,p.armor[0]);
                Require(p.armor[0].type==ItemID.CopperHelmet && p.inventory[12].type==5591,"client returns original real locations before sync");
                p=reset();p.Loadouts[1].Armor[0].SetDefaults(5591);Main.clientPlayer=new Player();Sync();packets.Clear();
                cast(new FishingOptions(loadout:true));Require(p.CurrentLoadoutIndex==1 && packets.Count(b=>b[2]==147 && b[4]==1)==1,"native group switch serializes exactly one target loadout message147");
                packets.Clear();stop();Require(p.CurrentLoadoutIndex==0 && packets.Count(b=>b[2]==147 && b[4]==0)==1,"native group return serializes exactly one original loadout message147");
                Console.WriteLine("PASS G10 original client message5 exact equipment/source state, return and no repeated differences; native message147 group switch/return. No server acceptance claim.");
            }
            finally{Main.netMode=0;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;Main.clientPlayer=baseline;audit.Unpatch(send,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");packets.Clear();}
        }
        private static void Sync(){typeof(Main).GetMethod("TrySyncingMyPlayer",Flags).Invoke(null,null);}
        internal static void Storage(object context,Func<Player> reset,Func<int,Chest> chest,Action cast)
        {
            var audit=new Harmony("JueMingR.Tests.FishingStorageNetwork");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeFishingNetworkChecks),nameof(Capture)));
            object host=Get(context,"Fishing"),items=Get(Get(context,"Tools"),"Items"),shell=Get(context,"Shell"),state=Get(shell,"State");
            var ownership=(ItemOperationOwnership)Get(items,"Ownership");var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];var baseline=Main.clientPlayer;var sections=Main.sectionManager;
            try
            {
                foreach(int scenario in new[]{0,1,2})
                {
                    Call(state,"Close");Main.netMode=0;Call(context,"UpdateRuntime");var p=reset();
                    Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.clientPlayer=new Player();Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();Main.netMode=1;Call(context,"UpdateRuntime");
                    Main.anglerQuest=0;Main.anglerQuestFinished=false;int type=Main.anglerQuestItemNetIDs[0];
                    p.inventory[12].SetDefaults(type);p.inventory[12].stack=3;p.inventory[14].SetDefaults(type);p.inventory[14].stack=2;p.inventory[13].SetDefaults(type);p.inventory[13].favorited=true;
                    var target=chest(type);packets.Clear();NativeFishingChecks.Save(host,new FishingOptions(storeMode:2));cast();
                    ulong mask=(1UL<<12)|(1UL<<14);
                    Require(packets.Count(b=>b[2]==85)==1 && p.inventoryChestStack[12] && p.inventoryChestStack[14] && !p.inventoryChestStack[13] && ownership.ProtectedSlots==mask && ownership.StoreResult.State==ItemOperationState.Executing && target.item[0].stack==1,"real fish-only client request locks exactly two nonfavorite members without claiming server movement");
                    var receive=new MessageBuffer{whoAmI=256};
                    void Reply(int slot,int id,int quantity)
                    {NativeChestLocatorChecks.Receive(receive,5,w=>{w.Write((byte)p.whoAmI);w.Write((short)slot);w.Write((short)quantity);w.Write((byte)0);w.Write((short)id);w.Write((byte)0);});Call(context,"UpdateRuntime");}
                    NativeChestLocatorChecks.Receive(receive,85,w=>w.Write(0));Call(context,"UpdateRuntime");Require(ownership.StoreResult.State==ItemOperationState.Executing,"native85 response does not serve as source acknowledgement");
                    if(scenario==0)
                    {
                        NativeFishingChecks.Save(host,new FishingOptions());Call(state,"RestoreVisible");Reply(12,type,1);Reply(12,type,1);
                        Require(ownership.StoreResult.State==ItemOperationState.Executing && ownership.ProtectedSlots==mask,"duplicate first source packet does not complete or release the group while disabled/F5");
                        Reply(14,0,0);Require(ownership.StoreResult.State==ItemOperationState.PartiallyCompleted && ownership.StoreResult.ConfirmedQuantity==4 && !ownership.StoreBlocked,"actual final source settles partial transfer after disabling fish storage");
                        Reply(12,type,1);Reply(14,0,0);Require(ownership.StoreResult.ConfirmedQuantity==4,"completed duplicate packets never double count");
                    }
                    else if(scenario==1)
                    {
                        Call(state,"RestoreVisible");var storage=Get(items,"Storage");var pending=Get(storage,"pending");Set(storage,"Tick",(ulong)Get(pending,"Started")+600);Call(storage,"Update");
                        Require(ownership.StoreResult.State==ItemOperationState.TimedOut && ownership.ProtectedSlots==mask && p.inventoryChestStack[12] && p.inventoryChestStack[14],"timeout keeps exact real locks and group protection");
                        Reply(12,0,0);Reply(14,0,0);Require(ownership.StoreResult.State==ItemOperationState.Completed && ownership.StoreResult.ConfirmedQuantity==5 && !ownership.StoreBlocked,"legal late original source packets settle the timed-out group");
                    }
                    else
                    {
                        Reply(12,ItemID.Wood,1);Reply(14,0,0);p.inventory[16].SetDefaults(type);Call(Get(items,"World"),"InvalidateObservation");
                        for(int i=0;i<30;i++)Call(context,"UpdateRuntime");NativeFishingChecks.Save(host,new FishingOptions());NativeFishingChecks.Save(host,new FishingOptions(storeMode:2));Call(state,"RestoreVisible");Call(state,"Close");
                        Require(ownership.StoreResult.State==ItemOperationState.Unconfirmed && ownership.ProtectedSlots==mask && ownership.StoreBlocked && p.inventory[12].type==ItemID.Wood && !p.inventoryChestStack[12],"unknown source reply preserves actual slots and owner protection even after native locks clear and switches change");
                    }
                    Require(packets.Count(b=>b[2]==85)==1,"paused, duplicate, timed-out or unknown fish storage never blindly resends the batch");
                }
                Console.WriteLine("PASS G10 real client dedicated-store85 and original MessageBuffer.GetData source receipts: disabled/F5 partial+duplicate, timeout+late, unknown group protection. Isolated client only.");
            }
            finally{Call(state,"Close");NativeFishingChecks.Save(host,new FishingOptions());Main.netMode=0;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;Main.clientPlayer=baseline;Main.sectionManager=sections;audit.Unpatch(send,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");packets.Clear();}
        }
        private static void Slot(int slot,Item item)
        {
            var packet=packets.LastOrDefault(b=>b[2]==5 && BitConverter.ToInt16(b,4)==slot);
            Require(packet!=null && BitConverter.ToInt16(packet,6)==item.stack && packet[8]==item.prefix && BitConverter.ToInt16(packet,9)==item.type && ((packet[11]&1)!=0)==item.favorited,"real serialized message5 matches physical slot "+slot);
        }
        private static void Capture(int __0)
        {
            if(Main.netMode!=1 || __0!=4 && __0!=5 && __0!=147 && __0!=85)return;
            var buffer=NetMessage.buffer[256].writeBuffer;int size=BitConverter.ToUInt16(buffer,0);
            Require(size>=3 && size<=buffer.Length && buffer[2]==__0,"native serialized frame boundary");packets.Add(buffer.Take(size).ToArray());
        }
    }
}
