using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.GameContent.LeashedEntities;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool payloadFilelessReceiver;
        private static void ConstructionPayload(object context,object host,object tools,object input,Action<string,Action> check)
        {
            var saves=new Harmony("JueMingR.Tests.BlueprintPayloadFilelessReceiver");
            saves.Patch(typeof(WorldGen).GetMethod("saveToonWhilePlaying",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(PayloadSaveOutlet)));
            payloadFilelessReceiver=true;
            try
            {
            check("native entity item identity through capture/library/real alt placement",()=>PayloadEntities(context,host,tools,input));
            check("native87 empty86 then156 and real Leashed FullSync",()=>PayloadLeashedNetworkCases(context,host,tools,input));
            check("native named chest body and zero-cost rename retirement",()=>PayloadContainer(context,host,tools,input,TileID.Containers,"原版箱名·甲",false));
            check("known empty chest name on existing body",()=>PayloadContainer(context,host,tools,input,TileID.Containers,"",true));
            check("native dresser name follows complete object",()=>PayloadContainer(context,host,tools,input,TileID.Dressers,"梳妆台·乙",false));
            for(int scenario=0;scenario<4;scenario++)
            {int current=scenario;check("native name permission ownership scenario="+current,()=>PayloadContainerNetwork(context,host,tools,input,current));}
            check("continuous moving mixed construction lends and resumes after real web",()=>PayloadCobwebContinuity(context,host,tools,input));
            }
            finally{payloadFilelessReceiver=false;saves.UnpatchAll(saves.Id);}
        }
        private static readonly List<byte[]> payloadChestPackets=new List<byte[]>();
        private static bool payloadChestWatching;
        private static void PayloadChestSent(int __0)
        {
            if(!payloadChestWatching || Main.netMode!=1 || (__0!=31 && __0!=33))return;
            byte[] bytes=NetMessage.buffer[256].writeBuffer;int length=BitConverter.ToUInt16(bytes,0);
            Require(length>=3 && length<=bytes.Length && bytes[2]==__0,"The actual native chest serializer produced the observed request.");
            payloadChestPackets.Add(bytes.Take(length).ToArray());
        }
        private static int PayloadCloseCount()
        {return payloadChestPackets.Count(bytes=>bytes[2]==33 && BitConverter.ToInt16(bytes,3)==-1);}
        private static byte[] PayloadNamePacket(int index,Chest chest,string name)
        {return PayloadPacket(69,w=>{w.Write((short)index);w.Write((short)chest.x);w.Write((short)chest.y);w.Write(name);});}
        private static byte[] PayloadOpenPacket(int index,Chest chest)
        {return PayloadPacket(33,w=>{w.Write((short)index);w.Write((short)chest.x);w.Write((short)chest.y);w.Write((byte)0);});}
        private static void PayloadContainerNetwork(object context,object host,object tools,object input,int scenario)
        {
            Player p=Reset(context,host,tools,input);int mode=Main.netMode;bool disconnected=Netplay.Disconnect;var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];
            var client=Main.clientPlayer;var sections=Main.sectionManager;
            using(new PayloadChestFixture())
            {
                var audit=new Harmony("JueMingR.Tests.BlueprintNamePermission"+scenario);
                var current=new RemoteServer{State=10,PendingTermination=true,ReadBuffer=new byte[2048]};
                try
                {
                    p.position.X=58*16;int source=WorldGen.PlaceChest(60,42,TileID.Containers,false,0);Require(source>=0,"Original named network source is a complete real chest.");
                    const string wanted="联机原版目标名";PayloadRename(p,source,wanted);Chest sourceChest=Main.chest[source];
                    if(scenario==0)Require(WorldGen.PlaceTile(64,42,TileID.Stone,true,true,p.whoAmI),"A real ordinary source cell shares this named asset.");
                    var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),sourceChest.x,sourceChest.y,scenario==0?5:2,2,"有限命名许可"+scenario);
                    // Distinct coordinates keep genuine manual history from
                    // a prior case out of this case's automatic-history oracle.
                    int left=44+4*scenario;
                    int a=WorldGen.PlaceChest(left,42,TileID.Containers,false,0),b=WorldGen.PlaceChest(left+2,42,TileID.Containers,false,0);
                    Require(a>=0 && b>=0,"Both destination bodies are independently placed native chests with no copied name.");
                    Chest ca=Main.chest[a],cb=Main.chest[b];p.position=new Vector2((left-2)*16,646);p.inventory[12].SetDefaults(ItemID.Chest);p.inventory[12].stack=3;
                    Netplay.Connection=current;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};Main.netMode=1;Netplay.Disconnect=false;
                    PayloadClientSession(context,host);
                    audit.Patch(typeof(NetMessage).GetMethod("SendData",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(PayloadChestSent)));
                    payloadChestPackets.Clear();payloadChestWatching=true;
                    Place(context,host,input,entry,ca.x,ca.y);SetAutomatic(host,input,true);
                    var cell=new CaptureCell{X=ca.x,Y=ca.y,Layers=CaptureLayer.Tile,TileType=TileID.Containers};
                    Require(PayloadRead(host,cell).NameState==BlueprintPayloadState.Unknown,"A client local name alone cannot authorize a rename.");
                    PayloadReceive(current,PayloadNamePacket(a,ca,""),true);
                    Until(context,input,()=>GetOptional(host,"waitingContainer")!=null && GetOptional(Get(host,"waitingContainer"),"PendingOpen")!=null);
                    object originalTarget=Get(host,"waitingContainer"),originalOwner=Get(originalTarget,"Owner");int closes=PayloadCloseCount();
                    Require(p.chest==-1 && !(bool)Get(host,"AutomaticContainerOperation"),"An outstanding31 has not opened a UI or acquired another chest's history suppression.");
                    if(scenario==3)
                    {
                        PayloadReceive(current,PayloadOpenPacket(a,ca),true);
                        Require((bool)Get(host,"AutomaticContainerOperation"),"Only the exact current automatically opened chest is marked automatic.");
                        var world=Get(context,"WorldObjects");var observer=Get(world,"observer");var history=(JueMingR.Features.WorldObjectText.OpenedPositionHistory)Get(world,"History");
                        Call(observer,"Update",false);long ka=JueMingR.Platform.WorldObjectText.WorldObject.PositionKey(ca.x,ca.y),kb=JueMingR.Platform.WorldObjectText.WorldObject.PositionKey(cb.x,cb.y);
                        Require(!history.Contains(ka),"The real opened-container observer excludes the exact automatic chest.");
                        typeof(Player).GetMethod("OpenChest",F,null,new[]{typeof(int),typeof(int),typeof(int)},null).Invoke(p,new object[]{cb.x,cb.y,b});
                        Require((bool)Get(originalTarget,"Requested") && !(bool)Get(host,"AutomaticContainerOperation"),"A real different native open is manual even before the old naming target's next tick.");
                        Call(observer,"Update",false);Require(history.Contains(kb),"The real observer records the manual other chest while an automatic naming target still exists.");
                        Step(context,input);Require(p.chest==b && PayloadCloseCount()==closes,"The naming owner's later cleanup cannot close the manual other chest.");
                    }
                    else
                    {
                        SetAutomatic(host,input,false);
                        if(scenario==0)
                        {
                            Call(host,"ClearProjection");Place(context,host,input,entry,ca.x,ca.y);SetAutomatic(host,input,true);
                            for(int frame=0;frame<16;frame++)Step(context,input);
                            Require(GetOptional(host,"waitingContainer")==null && Scene(host).Length==1,"A new instance cannot borrow an old outstanding31 permission at the same coordinate.");
                            PayloadReceive(current,PayloadOpenPacket(a,ca),true);
                            Require(p.chest==-1 && PayloadCloseCount()==closes+1,"A cancelled old33 is rejected before UI assignment and closed only when it has no newer manual successor.");
                            Until(context,input,()=>GetOptional(host,"waitingContainer")!=null && GetOptional(Get(host,"waitingContainer"),"PendingOpen")!=null);
                            Require(!ReferenceEquals(Get(Get(host,"waitingContainer"),"Owner"),originalOwner),"The new instance obtains its own new native31.");
                            PayloadReceive(current,PayloadOpenPacket(a,ca),true);Step(context,input);
                            Require(ca.name=="" && Scene(host).Length==1 && p.chest==-1 && !Main.playerInventory,"A real submission remains unconfirmed and promptly returns its own UI before a delayed69.");
                            byte[] submitted=payloadChestPackets.Last(bytes=>bytes[2]==33 && BitConverter.ToInt16(bytes,3)==a && bytes[9]!=0);
                            using(var reader=new BinaryReader(new MemoryStream(submitted),Encoding.UTF8))
                            {reader.BaseStream.Position=9;Require(reader.ReadByte()==wanted.Length && reader.ReadString()==wanted,"The actual native33 serializer commits the precise desired name.");}
                            p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;Call(Get(Get(context,"items"),"World"),"InvalidateObservation");
                            Until(context,input,()=>Main.tile[ca.x+4,ca.y+1].nactive() && Main.tile[ca.x+4,ca.y+1].type==TileID.Stone);
                            Require(p.inventory[13].stack==1 && p.inventory[12].stack==3 && ca.name=="" && Scene(host).Length==1,"Delayed69 does not block a same-asset ordinary cell's real material debit/world progress or fake name retirement.");
                            PayloadReceive(current,PayloadNamePacket(a,ca,wanted),true);Until(context,input,()=>Scene(host).Length==0);
                            Require(ca.name==wanted && p.inventory[12].stack==3,"Only the current true69 finishes the new name duty, with zero body consumption.");
                        }
                        else
                        {
                            bool same=scenario==2;int manual=same?a:b;Chest manualChest=same?ca:cb;
                            NetMessage.SendData(31,-1,-1,null,manualChest.x,manualChest.y);
                            PayloadReceive(current,PayloadOpenPacket(manual,manualChest),true);
                            if(!same)PayloadReceive(current,PayloadOpenPacket(a,ca),true);
                            for(int frame=0;frame<4;frame++)Step(context,input);
                            Require(p.chest==manual && PayloadCloseCount()==closes && manualChest.name=="" && !(bool)Get(host,"AutomaticContainerOperation"),
                                "A same-box manual31 takes its permission; a different-box manual31 keeps its UI and cannot be followed by an old synthetic close.");
                        }
                    }
                    Console.WriteLine("BLUEPRINT NAME NETWORK scenario="+scenario+" real31/33/69=true stale-instance="+(scenario==0?"not-borrowed":"not-this-case")+" exact-native-history="+(scenario==3?"observed":"not-this-case"));
                }
                finally
                {
                    try{SetAutomatic(host,input,false);Call(host,"ClearProjection");}
                    finally
                    {
                        payloadChestWatching=false;payloadChestPackets.Clear();audit.UnpatchAll(audit.Id);
                        Main.netMode=mode;Netplay.Disconnect=disconnected;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;Main.clientPlayer=client;Main.sectionManager=sections;current.Socket.Close();Call(context,"UpdateRuntime");
                    }
                }
            }
        }
        private static bool PayloadSaveOutlet()
        {
            if(!payloadFilelessReceiver)return true;
            // The owned receiver has no real character file. This unrelated
            // queue would throw on a ThreadPool thread after a REAL death.
            // Keep Hurt/KillMe/dead and the failed rescue assertion unchanged;
            // no save result/receipt is synthesized by this isolated outlet.
            Console.Error.WriteLine("PAYLOAD FILELESS SAVE outlet excluded; original death remains dead="+Main.LocalPlayer.dead+" life="+Main.LocalPlayer.statLife);
            return false;
        }
        private sealed class PayloadChestFixture : IDisposable
        {
            private readonly Chest[] original=Main.chest;
            private readonly object byCoordinates=AccessTools.Field(typeof(Chest),"_chestsByCoords").GetValue(null);
            internal PayloadChestFixture()
            {Main.chest=new Chest[8000];AccessTools.Field(typeof(Chest),"_chestsByCoords").SetValue(null,new Dictionary<Point,Chest>());}
            public void Dispose()
            {
                Player p=Main.LocalPlayer;
                if(p.chest>=0 && p.chest<Main.chest.Length && Main.chest[p.chest]!=null)
                {var chest=Main.chest[p.chest];typeof(Player).GetMethod("OpenChest",F,null,new[]{typeof(int),typeof(int),typeof(int)},null).Invoke(p,new object[]{chest.x,chest.y,-1});}
                if(Main.playerInventory)p.ToggleInv();
                Main.chest=original;AccessTools.Field(typeof(Chest),"_chestsByCoords").SetValue(null,byCoordinates);
            }
        }
        private sealed class PayloadEntityFixture : IDisposable
        {
            private readonly Dictionary<int,TileEntity> ids=TileEntity.ByID;
            private readonly Dictionary<Point16,TileEntity> positions=TileEntity.ByPosition;
            private readonly List<TileEntity> updates=TileEntity.UpdateEntities;
            private readonly int next=TileEntity.TileEntitiesNextID;
            internal PayloadEntityFixture()
            {
                if(TileEntity.manager==null)TileEntity.InitializeAll();
                TileEntity.ByID=new Dictionary<int,TileEntity>();TileEntity.ByPosition=new Dictionary<Point16,TileEntity>();TileEntity.UpdateEntities=new List<TileEntity>();TileEntity.TileEntitiesNextID=0;
            }
            public void Dispose(){TileEntity.ByID=ids;TileEntity.ByPosition=positions;TileEntity.UpdateEntities=updates;TileEntity.TileEntitiesNextID=next;}
        }
        private static int PayloadItemAt(int x,int y)
        {
            TileEntity entity;
            Require(TileEntity.ByPosition.TryGetValue(new Point16(x,y),out entity) && entity is TELeashedEntityAnchorWithItem,"Actual original leashed-anchor TE exists at its exact coordinate.");
            return (int)typeof(TELeashedEntityAnchorWithItem).GetField("itemType",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(entity);
        }
        private static void PayloadEntities(object context,object host,object tools,object input)
        {
            // Main.Initialize registers these native prototypes immediately
            // after TileEntity.InitializeAll. The CPU receiver omitted that
            // stage; a real kite's CreateLeashedEntity dereferences Prototype.
            if(LeashedKite.Prototype==null)LeashedEntity.Registry.RegisterAll();
            int[] kites=ContentSamples.ItemsByType.Values.Where(i=>i.createTile==723 && ItemID.Sets.IsAKite[i.type]).Select(i=>i.type).OrderBy(i=>i).Take(2).ToArray();
            int critter=ContentSamples.ItemsByType.Values.Where(i=>i.createTile==724 && i.makeNPC>0).Select(i=>i.type).OrderBy(i=>i).First();
            Require(kites.Length==2,"Locked original content provides two different kite items for equal-Tile identity control.");
            CaptureCell firstEntity=default(CaptureCell);short firstX=0,firstY=0;
            foreach(int itemType in kites.Concat(new[]{critter}))
            using(new PayloadEntityFixture())
            {
                Player p=Reset(context,host,tools,input);p.position.X=58*16;p.inventory[0].SetDefaults(itemType);p.inventory[0].stack=3;
                int tileType=p.inventory[0].createTile;
                // SOURCE ONLY: genuine alternate input reaches the original
                // ItemCheck promotion1->2/post-place consumer. Unrelated full
                // Update systems are not the oracle for source construction.
                PayloadOriginalAltPlacement(p,60,42);
                Require(Main.tile[60,42].nactive() && Main.tile[60,42].type==tileType && PayloadItemAt(60,42)==itemType && p.inventory[0].stack==2,
                    "Original item/right-use creates source Tile and TE identity, with real post-place debit: "+itemType);
                var source=Main.tile[60,42];
                if(itemType==kites[0])PayloadSourceGuard(context,host,source,itemType);
                LibraryEntry entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,42,1,1,"真实实体物品"+itemType);
                var savedEntity=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry).Cells.Single(c=>c.Payload.EntityState==BlueprintPayloadState.Known);
                if(itemType==kites[0]){firstEntity=savedEntity;firstX=source.frameX;firstY=source.frameY;}
                else if(itemType==kites[1])
                {
                    var replaced=firstEntity;replaced.Payload.EntityItemType=itemType;
                    Require(source.frameX==firstX && source.frameY==firstY && firstEntity.Payload.EntityItemType==kites[0] && savedEntity.Payload.EntityItemType==itemType &&
                        PayloadCellBytes(replaced).SequenceEqual(PayloadCellBytes(savedEntity)) && !PayloadCellBytes(firstEntity).SequenceEqual(PayloadCellBytes(savedEntity)),
                        "Equal native Tile/frame and otherwise equal saved cell fields retain distinct item bytes in canonical chunks, independently of random capture-root metadata.");
                }
                entry=PayloadImportEntity(context,host,input,entry,source,itemType);
                p.position=new Vector2(640,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(itemType);p.inventory[12].stack=5;
                Place(context,host,input,entry,44,42);SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0);
                Require(Main.tile[44,42].nactive() && Main.tile[44,42].type==tileType && PayloadItemAt(44,42)==itemType && p.inventory[12].stack==4,
                    "Captured/saved asset restores exact original entity identity through production alt ItemCheck and debits one real item: "+itemType);
                for(int frame=0;frame<12;frame++)Step(context,input);
                Require(p.inventory[12].stack==4,"Entity/Tile completion retires without a second material use.");
                SetAutomatic(host,input,false);
                Console.WriteLine("BLUEPRINT PAYLOAD native-item="+itemType+" tile="+tileType+" source=original-right-ItemCheck capture=real saved=true destination=production-alt-ItemCheck debit=1 TE=exact retired=true");
            }
        }
        private static byte[] PayloadCellBytes(CaptureCell cell)
        {
            // Spatial origin and object-link ids are separate capture context.
            // Hold every actual semantic cell field fixed for this content
            // comparison; root revision/ticks are deliberately not its oracle.
            cell.X=cell.Y=cell.ObjectId=0;
            return (byte[])typeof(LibraryState).Assembly.GetType("JueMingR.Features.Blueprint.LibraryCodec",true).GetMethod("EncodeChunk",F).Invoke(null,new object[]{new CaptureChunk(new[]{cell},1)});
        }
        private static byte[] PayloadWindowBytes(BlueprintProjectionWindow window)
        {
            return (byte[])typeof(LibraryState).Assembly.GetType("JueMingR.Features.Blueprint.LibraryCodec",true).GetMethod("EncodeChunk",F).Invoke(null,new object[]{new CaptureChunk(window.Cells.ToArray(),window.Cells.Count,window.Objects.ToArray(),window.Objects.Count)});
        }
        private static byte[] PayloadPacket(byte kind,Action<BinaryWriter> body)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write((ushort)0);writer.Write(kind);body(writer);writer.Flush();stream.Position=0;writer.Write(checked((ushort)stream.Length));writer.Flush();return stream.ToArray();}
        }
        private static void PayloadReceive(RemoteServer source,byte[] packet,bool accepted)
        {
            Array.Copy(packet,source.ReadBuffer,packet.Length);int prior=NetMessage.buffer[256].totalData;
            source.ClientReadCallBack(null,packet.Length);
            Require(NetMessage.buffer[256].totalData==prior+(accepted?packet.Length:0) && !Netplay.Disconnect,
                "Original callback/ReceiveBytes admits exactly the current source bytes before shared buffer256.");
            if(accepted)
            {
                // Main's normal network reset prepares this reader before
                // CheckBytes reads BaseStream.Position. GetData's own lazy
                // initialization is later and cannot prepare a fresh receiver.
                if(NetMessage.buffer[256].reader==null)NetMessage.buffer[256].ResetReader();
                NetMessage.CheckBytes(256);
            }
        }
        private static BlueprintObjectPayload PayloadRead(object host,CaptureCell cell)
        {return (BlueprintObjectPayload)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintNativePayload").GetMethod("Read",F).Invoke(null,new object[]{cell,null});}
        private static void PayloadClientSession(object context,object host)
        {
            // Same native client prerequisites as NetworkProgress: the new
            // connection must pass the runtime's real end/start boundary.
            // Do not patch host.connection or bypass projection admission.
            Main.clientPlayer=new Player();Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();
            Call(context,"UpdateRuntime");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Get(host,"ControlsEnabled") && (bool)Get(host,"ProjectionPersistenceReady");});
        }
        private static void PayloadSourceGuard(object context,object host,Tile source,int itemType)
        {
            int mode=Main.netMode;bool disconnected=Netplay.Disconnect;RemoteServer connection=Netplay.Connection;MessageBuffer buffer=NetMessage.buffer[256];
            var client=Main.clientPlayer;var sections=Main.sectionManager;
            using(new PayloadChestFixture())
            {
                int index=WorldGen.PlaceChest(68,42,TileID.Containers,false,0);Require(index>=0,"Original source guard fixture owns an independently placed chest.");
                Chest chest=Main.chest[index];var chestCell=new CaptureCell{X=chest.x,Y=chest.y,Layers=CaptureLayer.Tile,TileType=TileID.Containers};
                var entityCell=new CaptureCell{X=60,Y=42,Layers=CaptureLayer.Tile,TileType=source.type,FrameX=source.frameX,FrameY=source.frameY};
                TileEntity entity=TileEntity.ByPosition[new Point16(60,42)];
                var current=new RemoteServer{State=10,PendingTermination=true,ReadBuffer=new byte[2048]};
                var old=new RemoteServer{State=10,ReadBuffer=new byte[2048]};
                try
                {
                    Netplay.Connection=current;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};Main.netMode=1;Netplay.Disconnect=false;
                    PayloadClientSession(context,host);
                    var baselineEntity=PayloadRead(host,entityCell);
                    old.ClientReadCallBack(null,0);Netplay.Disconnect=true;old.ClientReadCallBack(null,0);Netplay.Disconnect=false;
                    var afterEmpty=PayloadRead(host,entityCell);
                    Require(PayloadItemAt(60,42)==itemType && afterEmpty.EntityState==baselineEntity.EntityState && afterEmpty.EntityItemType==baselineEntity.EntityItemType,"An old empty callback changes neither the actual TE nor the preexisting native identity observation.");
                    Chest.DestroyChestDirect(chest.x,chest.y,index);
                    Require(Main.tile[chest.x,chest.y].nactive() && Chest.FindChest(chest.x,chest.y)<0 && PayloadRead(host,chestCell).NameState==BlueprintPayloadState.Unknown,
                        "A real Tile with no client Chest slot remains Unknown without calling native Send69(-1).");
                    PayloadReceive(current,PayloadPacket(34,w=>{w.Write((byte)0);w.Write((short)chest.x);w.Write((short)(chest.y+1));w.Write((short)0);w.Write((short)index);}),true);
                    Require(Chest.FindChest(chest.x,chest.y)==index,"Original GetData34 supplies the real missing slot for this exact anchor.");chest=Main.chest[index];
                    Require(PayloadRead(host,chestCell).NameState==BlueprintPayloadState.Unknown,"Client name is not inferred from local Chest or a slot receipt.");
                    byte[] name=PayloadPacket(69,w=>{w.Write((short)index);w.Write((short)chest.x);w.Write((short)chest.y);w.Write("当前原生69名字");});
                    PayloadReceive(old,name,false);
                    Require(chest.name!="当前原生69名字" && PayloadRead(host,chestCell).NameState==BlueprintPayloadState.Unknown,"Old ReadBuffer name bytes never complete the new connection's name query.");
                    PayloadReceive(current,name,true);
                    Require(PayloadRead(host,chestCell).NameState==BlueprintPayloadState.Known && PayloadRead(host,chestCell).Name=="当前原生69名字","Current real GetData69 still establishes the exact authoritative name after old empty callbacks.");
                    byte[] sync=PayloadPacket(86,w=>{w.Write(entity.ID);w.Write(true);TileEntity.Write(w,entity,true);});
                    TileEntity.Remove(entity);
                    Require(PayloadRead(host,entityCell).EntityState==BlueprintPayloadState.Unknown,"A missing current TE remains unknown before a genuine native sync.");
                    PayloadReceive(old,sync,false);Require(!TileEntity.ByPosition.ContainsKey(new Point16(60,42)),"Old serialized TE bytes cannot create a current entity.");
                    PayloadReceive(current,sync,true);
                    Require(PayloadRead(host,entityCell).EntityState==BlueprintPayloadState.Unknown && PayloadItemAt(60,42)==itemType,"Current original86 restores actual TE bytes but cannot replace the missing real Leashed FullSync authority.");
                    // Native156 is client->server insertion, not a server->
                    // client TE confirmation. Preserve that exact distinction.
                    byte[] insertion=PayloadPacket(156,w=>{w.Write((short)60);w.Write((short)42);w.Write((short)ItemID.StoneBlock);});
                    PayloadReceive(current,insertion,true);
                    Require(PayloadItemAt(60,42)==itemType,"A client156 cannot masquerade as an authoritative changed TE item.");
                    Console.WriteLine("BLUEPRINT PAYLOAD source-guard tile-without-slot=Unknown real34+69=Known old-empty=harmless old69/86=rejected current86=not-Leashed-authority client156=no-authority");
                }
                finally{Main.netMode=mode;Netplay.Disconnect=disconnected;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;Main.clientPlayer=client;Main.sectionManager=sections;current.Socket.Close();old.Socket.Close();Call(context,"UpdateRuntime");}
            }
        }
        private static LibraryEntry PayloadImportEntity(object context,object host,object input,LibraryEntry captured,Tile source,int itemType)
        {
            var library=(LibraryState)Get(host,"Library");
            var profile=(IBlueprintImportProfile)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver").GetMethod("Create",F).Invoke(null,null);
            string area=Path.Combine(Program.OutputDirectory,"sampled-native-entity-"+itemType);Directory.CreateDirectory(area);
            // This is a schema-generated fixture sampled from a genuinely
            // placed native object, not an alleged external Helper export.
            string entity="{\"x\":0,\"y\":0,\"itemType\":"+itemType+"}";
            string tile="\"tiles\":[{\"x\":0,\"y\":0,\"tileType\":"+source.type+",\"frameX\":"+source.frameX+",\"frameY\":"+source.frameY+",\"color\":0,\"slope\":0,\"isHalfBrick\":false,\"inActive\":false,\"fullbright\":false,\"invisible\":false}]";
            string head="\"name\":\"Native sampled entity\",\"createdAt\":\"2026-11-11T00:00:00.0000000Z\",\"width\":1,\"height\":1,";
            string rest=tile+",\"walls\":[],\"wires\":[],\"containers\":[]";
            Func<string,bool,string> json=(records,first)=>"{"+head+(first?"\"entities\":["+records+"],"+rest:rest+",\"entities\":["+records+"]")+"}";
            string path=Path.Combine(area,"entities-after.json");File.WriteAllText(path,json(entity,false),new UTF8Encoding(false));byte[] original=File.ReadAllBytes(path);
            BlueprintExchangeReceipt receipt=NativeBlueprintHelperImportChecks.Import(library,profile,path);
            Require(receipt.Status==BlueprintExchangeStatus.Completed || receipt.Status==BlueprintExchangeStatus.AlreadyExists,"Real Helper-schema entity import publishes or deduplicates the sampled native identity.");
            var entry=NativeBlueprintHelperImportChecks.Command(library,library.TryList(31001,0,receipt.AssetId)).Page.Entries.Single(e=>e.AssetId==receipt.AssetId);
            var window=OpenCapturedAppearance(library,entry);
            var nativeWindow=OpenCapturedAppearance(library,captured);
            Require(window.Cells.Count(c=>c.Payload.EntityState==BlueprintPayloadState.Known && c.Payload.EntityItemType==itemType)==1 && PayloadWindowBytes(window).SequenceEqual(PayloadWindowBytes(nativeWindow)),
                "JSON import/save/reopen preserves all independently captured Tile/frame/payload and complete-object chunk semantics; independent roots may have different revision/tick/origin metadata.");
            string before=Path.Combine(area,"entities-before.json");File.WriteAllText(before,json(entity,true),new UTF8Encoding(false));
            var reordered=NativeBlueprintHelperImportChecks.Import(library,profile,before);
            Require(reordered.Status==BlueprintExchangeStatus.AlreadyExists && reordered.AssetId==entry.AssetId,"entities before or after tiles produces the same normalized content identity.");
            string exchange=Path.Combine(area,"native-entity.jmbp");NativeBlueprintHelperImportChecks.Export(library,entry,exchange);
            var roundtrip=NativeBlueprintHelperImportChecks.Import(library,profile,exchange);
            Require(roundtrip.Status==BlueprintExchangeStatus.AlreadyExists && roundtrip.AssetId==entry.AssetId,"Normal jmbp exchange retains the sampled entity identity.");
            string coldRoot=Path.Combine(area,"cold-library"),coldCounter=Path.Combine(area,"cold-counter.json");Guid coldId;
            using(var cold=NativeBlueprintHelperImportChecks.State(coldRoot,coldCounter))
            {
                var first=NativeBlueprintHelperImportChecks.Import(cold,profile,exchange);coldId=first.AssetId;
                Require(first.Status==BlueprintExchangeStatus.Completed,"The genuine exported asset imports into a separate owned library before its worker is closed.");
            }
            using(var reopened=NativeBlueprintHelperImportChecks.State(coldRoot,coldCounter))
            {
                var persisted=NativeBlueprintHelperImportChecks.Command(reopened,reopened.TryList(31002,0,coldId)).Page.Entries.Single(e=>e.AssetId==coldId);
                var coldWindow=OpenCapturedAppearance(reopened,persisted);
                Require(persisted.ContentHash==entry.ContentHash && coldWindow.Cells.Count(c=>c.Payload.EntityState==BlueprintPayloadState.Known && c.Payload.EntityItemType==itemType)==1,
                    "A newly opened real library worker reads the exact saved entity identity after disposal, not an old in-memory projection.");
            }
            foreach(var bad in new[]{new{Key="duplicate",Records=entity+","+entity},new{Key="wrong-item",Records=entity.Replace(itemType.ToString(),ItemID.StoneBlock.ToString())},new{Key="outside",Records=entity.Replace("\"x\":0","\"x\":1")}})
            {
                string invalid=Path.Combine(area,bad.Key+".json");File.WriteAllText(invalid,json(bad.Records,false),new UTF8Encoding(false));
                Require(NativeBlueprintHelperImportChecks.Import(library,profile,invalid).Status==BlueprintExchangeStatus.Failed,"Invalid/conflicting sampled entity record rejects without publication: "+bad.Key);
            }
            Call(host,"SetMaterialViewDemand",true);
            try
            {
                // The catalog grants one finite scan batch per real host input
                // mapping. After a real session restart, Poll/outer Update
                // alone cannot recreate it; advance the ordinary native step.
                Action tick=()=>{Step(context,input);Call(host,"Poll");};
                NativeQuickItemChecks.Until(()=>{tick();return GetOptional(host,"MaterialCatalog")!=null;});
                BlueprintMaterialSnapshot bill=null;
                using(NativeBlueprintLibraryWait.ObserveHost(library))
                    NativeBlueprintLibraryWait.HostCondition(library,"sampled real entity material bill",()=>{tick();bill=(BlueprintMaterialSnapshot)Call(host,"MaterialAsset",entry);return bill.Status!=BlueprintMaterialStatus.Pending;});
                Require(bill.Status==BlueprintMaterialStatus.Exact && bill.Rows.Count==1 && bill.Rows[0].ItemType==itemType && bill.Rows[0].Required==1,
                    "The real imported material consumer requires exactly one independently observed TE item, not a generic same-frame producer.");
            }
            finally{Call(host,"SetMaterialViewDemand",false);}
            Require(original.SequenceEqual(File.ReadAllBytes(path)),"Import and exchange leave the sampled source JSON unchanged.");
            Console.WriteLine("BLUEPRINT ENTITY JSON item="+itemType+" source=native-sampled schema-generated=true order-invariant=true saved-reopen=true jmbp=true material=exact-one invalid-records=rejected");
            return entry;
        }
        private static void PayloadRename(Player p,int index,string name)
        {
            Chest chest=Main.chest[index];
            typeof(Player).GetMethod("OpenChest",F,null,new[]{typeof(int),typeof(int),typeof(int)},null).Invoke(p,new object[]{chest.x,chest.y,index});
            Main.npcChatText=name;Main.editChest=true;ChestUI.RenameChestSubmit(p);
            Require(chest.name==name,"Independent source name is consumed by original ChestUI.RenameChestSubmit, including empty.");
            typeof(Player).GetMethod("OpenChest",F,null,new[]{typeof(int),typeof(int),typeof(int)},null).Invoke(p,new object[]{chest.x,chest.y,-1});
            if(Main.playerInventory)p.ToggleInv();
        }
        private static void PayloadContainer(object context,object host,object tools,object input,int tileType,string name,bool existing)
        {
            Player p=Reset(context,host,tools,input);
            using(new PayloadChestFixture())
            try
            {
            p.position.X=58*16;
            int source=WorldGen.PlaceChest(60,42,(ushort)tileType,false,0);
            Require(source>=0,"Original PlaceChest creates complete source container, type="+tileType);
            PayloadRename(p,source,name);Chest original=Main.chest[source];
            int width=tileType==TileID.Dressers?3:2;
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),original.x,original.y,width,2,"原版容器名"+tileType+(name.Length==0?"空":"有名"));
            var savedName=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry).Cells.Where(c=>c.Payload.NameState==BlueprintPayloadState.Known).ToArray();
            Require(savedName.Length==1 && savedName[0].Payload.Name==name,"Actual capture/save/reopen carries one authoritative container anchor, including explicit empty name.");
            p.position=new Vector2(640,646);p.inventory[12].SetDefaults(tileType==TileID.Dressers?ItemID.Dresser:ItemID.Chest);p.inventory[12].stack=3;
            int before=3,destination=-1;Chest previous=null;
            if(existing)
            {
                destination=WorldGen.PlaceChest(44,42,(ushort)tileType,false,0);Require(destination>=0,"Existing correct destination body is an independent original container.");
                previous=Main.chest[destination];PayloadRename(p,destination,"玩家原来的名字");
            }
            Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);
            Until(context,input,()=>Scene(host).Length==0);
            destination=Chest.FindChest(44,41);
            Require(destination>=0 && Main.chest[destination].name==name && p.inventory[12].stack==before-(existing?0:1) && (!existing || ReferenceEquals(previous,Main.chest[destination])),
                "Name is restored only on the real correct body; an existing body is renamed with zero extra material.");
            PayloadRename(p,destination,"退休后玩家改名");for(int frame=0;frame<12;frame++)Step(context,input);
            Require(Main.chest[destination].name=="退休后玩家改名","Completed name duty retires and never overwrites a later player name.");
            SetAutomatic(host,input,false);
            Console.WriteLine("BLUEPRINT PAYLOAD container="+tileType+" known-name="+(name.Length==0?"empty":"text")+" source=original-RenameChestSubmit capture/save=true body-debit="+(existing?0:1)+" destination-name=original-consumer retired=true");
            }
            catch
            {
                var pending=GetOptional(host,"namingContainer");
                var first=Get(Get(host,"containerNames"),"First");var target=pending??(first==null?null:Get(first,"Value"));
                var payload=target==null?default(BlueprintObjectPayload):((CaptureCell)Get(target,"Cell")).Payload;
                Console.Error.WriteLine("PAYLOAD NAME failure pending="+(pending!=null)+" targets="+Get(Get(host,"containerNames"),"Count")+" chest="+p.chest+" inventory="+Main.playerInventory+" actual="+(Chest.FindChest(44,41)>=0?Main.chest[Chest.FindChest(44,41)].name:"<absent>")+
                    " requested="+(pending==null?"none":Get(pending,"Requested"))+" submitted="+(pending==null?"none":Get(pending,"Submitted"))+" suspended="+(target==null?"none":Get(target,"Suspended"))+" targetNameState="+payload.NameState+" targetName="+(payload.Name??"<null>"));
                throw;
            }
            finally{SetAutomatic(host,input,false);Call(host,"ClearProjection");}
        }
        private static void PayloadCobwebContinuity(object context,object host,object tools,object input)
        {
            object movement=Get(context,"Movement"),fall=Get(movement,"FallHost");
            var options=(SmartFallProtectionOptions)Get(movement,"SmartFallProtection");
            double surface=Main.worldSurface;Vector2 camera=Main.screenPosition;var map=Main.Map;
            try
            {
                Player p=Reset(context,host,tools,input);p.position.Y=90*16-p.height;p.fallStart=(int)(p.position.Y/16);Main.screenPosition=p.position-new Vector2(300,280);
                // This receiver's finite world has its own native map too;
                // keep placement's neighbor refresh inside the same dimensions.
                Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,90,1);
                p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=200;
                p.inventory[13].SetDefaults(ItemID.Wood);p.inventory[13].stack=200;
                p.inventory[17].SetDefaults(ItemID.Cobweb);p.inventory[17].stack=30;
                var cells=new List<CaptureCell>();
                for(int y=0;y<30;y++){cells.Add(new CaptureCell{Y=y,Layers=CaptureLayer.Tile,TileType=TileID.Stone});cells.Add(new CaptureCell{X=1,Y=y,Layers=CaptureLayer.Tile,TileType=TileID.WoodBlock});}
                Place(context,host,input,Asset(host,cells.ToArray()),44,87);SetAutomatic(host,input,true);
                Action<bool> step=moving=>
                {
                    Main.screenPosition=p.position-new Vector2(300,280);
                    NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(100,0),false,moving?1:0);
                    NativeQuickItemChecks.BeginWorldStep();NativeCombatHandoffChecks.CobwebWorldStep(context,()=>p.Update(0));Call(context,"UpdateRuntime");
                    if(constructionChecksActive)constructionInputTail?.Invoke();
                };
                for(int frame=0;frame<100;frame++){step(false);if(p.inventory[12].stack<200 && (bool)Get(Get(tools,"Use"),"Active"))break;}
                Require(p.inventory[12].stack<200 && (bool)Get(Get(tools,"Use"),"Active") && Scene(host).Length==1,"Construction has an active real debit and unfinished projection before the fall.");
                for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++)Call(movement,"SetSmartFallPermission",i,i==12);Call(movement,"SetEnabled",3,true);Main.worldSurface=10;
                for(int x=35;x<44;x++){Main.tile[x,90].active(false);NativeToolsChecks.Tile(x,117,1);for(int y=86;y<117;y++)Main.tile[x,y].wall=1;}
                p.fallStart=0;p.velocity.Y=8;float startX=p.position.X;
                bool requested=false,placed=false,touched=false,returned=false,progress=false;int atReturn=-1;
                Func<int> actual=()=>cells.Count(c=>Main.tile[44+c.X,87+c.Y].nactive() && Main.tile[44+c.X,87+c.Y].type==c.TileType);
                for(int frame=0;frame<360;frame++)
                {
                    step(frame<12);
                    if(!requested && (bool)Get(fall,"webRequest")){requested=true;Console.WriteLine("BLUEPRINT COBWEB phase=request frame="+frame+" owner="+Get(Get(tools,"Use"),"Operation")+" anim/time="+p.itemAnimation+"/"+p.itemTime+" deadline="+Get(fall,"webDeadline"));}
                    if(!placed && (bool)Get(fall,"CobwebPlaced")){placed=true;Console.WriteLine("BLUEPRINT COBWEB phase=placed frame="+frame+" stack="+p.inventory[17].stack+" fallStart="+p.fallStart);}
                    if(!touched && (bool)Get(fall,"CobwebProtected")){touched=true;Console.WriteLine("BLUEPRINT COBWEB phase=protected frame="+frame+" fallStart="+p.fallStart+" velocity="+p.velocity);}
                    if(placed && !returned && !(bool)Get(fall,"HasPending")){returned=true;atReturn=actual();Console.WriteLine("BLUEPRINT COBWEB phase=returned frame="+frame+" world-count="+atReturn);}
                    if(p.dead){Console.Error.WriteLine("BLUEPRINT COBWEB phase=real-death frame="+frame+" requested="+requested+" placed="+placed+" touched="+touched+" returned="+returned+" life="+p.statLife+" pos="+p.position);break;}
                    if(returned && actual()>atReturn){progress=true;break;}
                }
                Require(placed && touched && returned && progress && p.inventory[17].stack==29 && p.position.X>startX+1,
                    "Moving continuous construction legally yields after its debit, places/debits/touches one web, returns selection and makes a NEW actual construction change without resetting its projection: request="+requested+" placed="+placed+" touched="+touched+" returned="+returned+" progress="+progress+" stack="+p.inventory[17].stack+" life="+p.statLife+" dead="+p.dead+" pos="+p.position);
                Console.WriteLine("BLUEPRINT COBWEB continuity prior-real-debit=true movement=true web-debit=1 native-touch=true return=true new-world-progress=true projection-intent=retained");
            }
            finally
            {
                SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(fall,"ClearSession");
                for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++)Call(movement,"SetSmartFallPermission",i,options.Allows(i));Call(movement,"SetEnabled",3,options.Enabled);
                Main.worldSurface=surface;Main.screenPosition=camera;Main.Map=map;
            }
        }
    }
}
