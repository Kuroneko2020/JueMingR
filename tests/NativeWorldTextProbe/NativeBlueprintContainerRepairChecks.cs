using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionContainerRepairs(object context,object host,object tools,object input,Action<string,Action> check)
        {
            check("N1 existing substituted chest name and explicit empty name",()=>ContainerReplacementNames(context,host,tools,input));
            check("N1 policy OFF and incomplete body reject; substitute placement debits once",()=>ContainerReplacementBoundaries(context,host,tools,input));
            check("N2 original server contention and same-intent permission recovery",()=>ContainerServerPermission(context,host,tools,input,false));
            check("N2 real delayed server33 cannot replace a later manual chest",()=>ContainerServerPermission(context,host,tools,input,true));
            ConstructionContainerInventoryRepair(context,host,tools,input,check);
        }
        private static void ConstructionContainerInventoryRepair(object context,object host,object tools,object input,Action<string,Action> check)
        {
            check("N2 actual manual inventory/cursor gates retry, late permission and commit/cancel",()=>
            {for(int phase=0;phase<4;phase++)ContainerServerPermission(context,host,tools,input,false,phase);});
        }
        private static LibraryEntry ContainerNameSource(object context,object host,object input,Player p,string name,bool ordinary=false)
        {
            p.position.X=58*16;
            int index=WorldGen.PlaceChest(60,42,TileID.Containers,false,0);
            Require(index>=0,"Original wooden source chest exists.");PayloadRename(p,index,name);
            var chest=Main.chest[index];
            if(ordinary)Require(WorldGen.PlaceTile(64,42,TileID.Stone,true,true,p.whoAmI),"Original ordinary source cell shares the name intention's asset.");
            return NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),chest.x,chest.y,ordinary?5:2,2,"替代箱名·"+(name.Length==0?"明确空名":name));
        }
        private static int ContainerAlternative(Player p,string name)
        {
            var item=new Item();item.SetDefaults(ItemID.RichMahoganyChest);
            int index=WorldGen.PlaceChest(44,42,(ushort)item.createTile,false,item.placeStyle);
            Require(index>=0,"Original PlaceChest independently creates the different native chest style.");
            PayloadRename(p,index,name);return index;
        }
        private static void ContainerReplacementNames(object context,object host,object tools,object input)
        {
            foreach(string wanted in new[]{"替代箱目标名",""})
            using(new PayloadChestFixture())
            try
            {
                Player p=SubstitutionReset(context,host,tools,input);
                var entry=ContainerNameSource(context,host,input,p,wanted);int index=ContainerAlternative(p,"原来的自定义名");Chest actual=Main.chest[index];
                p.position=new Vector2(640,646);p.inventory[12].SetDefaults(ItemID.RichMahoganyChest);p.inventory[12].stack=3;
                SubstitutionCatalog(context,host,input);Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0);
                Require(ReferenceEquals(actual,Main.chest[index]) && actual.name==wanted && p.inventory[12].stack==3,
                    "An accepted different complete chest restores the precise name, including explicit empty, with zero extra chest material.");
                Console.WriteLine("CONTAINER N1 existing substitute name="+(wanted.Length==0?"known-empty":"known-text")+" real-Rename=true debit=0 final-Match=true");
            }
            finally{SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(host,"SetSameTypeReplacement",false);}
        }
        private static void ContainerReplacementBoundaries(object context,object host,object tools,object input)
        {
            using(new PayloadChestFixture())
            try
            {
                Player p=SubstitutionReset(context,host,tools,input);var entry=ContainerNameSource(context,host,input,p,"开关恢复名");
                int index=ContainerAlternative(p,"开关前原名");Chest actual=Main.chest[index];p.position=new Vector2(640,646);
                SubstitutionCatalog(context,host,input);Call(host,"SetSameTypeReplacement",false);
                Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);
                for(int i=0;i<80;i++)Step(context,input);
                Require(actual.name=="开关前原名" && Scene(host).Length==1,"Policy OFF cannot authorize the different style merely for naming.");
                Call(host,"SetSameTypeReplacement",true);Until(context,input,()=>Scene(host).Length==0);
                Require(actual.name=="开关恢复名","The same diagram continues after the genuine substitution switch is enabled.");
            }
            finally{SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(host,"SetSameTypeReplacement",false);}
            using(new PayloadChestFixture())
            try
            {
                Player p=SubstitutionReset(context,host,tools,input);var entry=ContainerNameSource(context,host,input,p,"不完整不改名");
                int index=ContainerAlternative(p,"破损箱保留名");Chest actual=Main.chest[index];p.position=new Vector2(640,646);
                // Failure fixture only: remove one physical member, never copy
                // template frames into the target or fabricate a complete body.
                Main.tile[45,42].ClearTile();SubstitutionCatalog(context,host,input);
                Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);
                for(int i=0;i<80;i++)Step(context,input);
                Require(actual.name=="破损箱保留名" && Scene(host).Length==1,"A surviving Chest record cannot stand in for a complete accepted object body.");
            }
            finally{SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(host,"SetSameTypeReplacement",false);}
            using(new PayloadChestFixture())
            try
            {
                Player p=SubstitutionReset(context,host,tools,input);var entry=ContainerNameSource(context,host,input,p,"替代材料只扣一次");
                p.position=new Vector2(640,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(ItemID.RichMahoganyChest);p.inventory[12].stack=3;
                SubstitutionCatalog(context,host,input);Place(context,host,input,entry,44,41);SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0);int index=Chest.FindChest(44,41);var material=new Item();material.SetDefaults(ItemID.RichMahoganyChest);
                Require(index>=0 && Main.chest[index].name=="替代材料只扣一次" && Main.tile[44,41].frameX==material.placeStyle*36 && p.inventory[12].stack==2,
                    "Actual substitute placement creates its own complete style, consumes exactly one real chest and finishes naming without a second body debit.");
                Console.WriteLine("CONTAINER N1 policy-off/incomplete refused; policy-on recovered; real-substitute-ItemCheck debit=1 final-Match=true");
            }
            finally{SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(host,"SetSameTypeReplacement",false);}
        }

        private static bool containerRepairRecording;
        private static readonly List<byte[]> containerRepairClient=new List<byte[]>(),containerRepairServer=new List<byte[]>();
        private static void ContainerRepairSent(int __0,int __1)
        {
            if(!containerRepairRecording || __0!=31 && __0!=33 && __0!=69)return;
            int at=Main.netMode==2 && __1>=0?__1:256;byte[] data=NetMessage.buffer[at].writeBuffer;int count=BitConverter.ToUInt16(data,0);
            Require(count>=3 && data[2]==__0,"Actual original chest serializer emitted the captured packet.");
            (Main.netMode==2?containerRepairServer:containerRepairClient).Add(data.Take(count).ToArray());
        }
        // These are finite native server containers, not a protocol simulator.
        // Original GetData31/33/69 owns every refusal, permission and edit.
        private sealed class ContainerRepairServerRealm
        {
            internal readonly Player[] Players=new Player[Main.player.Length];
            internal Chest[] Chests=new Chest[8000];
            private object coordinates=new Dictionary<Point,Chest>();
            private readonly Tile[,] tiles=new Tile[Main.maxTilesX,Main.maxTilesY];
            internal int A,B;
            internal ContainerRepairServerRealm()
            {
                for(int i=0;i<Players.Length;i++)Players[i]=new Player{whoAmI=i,chest=-1};Players[0].active=Players[1].active=true;
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)tiles[x,y]=new Tile();
                Run(()=>
                {
                    for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);
                    Require(WorldGen.PlaceChest(60,42,TileID.Containers,false,0)>=0,"Original server source body.");
                    A=WorldGen.PlaceChest(44,42,TileID.Containers,false,0);B=WorldGen.PlaceChest(46,42,TileID.Containers,false,0);
                    Require(A>=0 && B>=0,"Original server independently owns both complete real chest bodies.");
                });
            }
            internal void Run(Action action)
            {
                int mode=Main.netMode,my=Main.myPlayer;bool dedicated=Main.dedServ;var player=Main.player;var chest=Main.chest;var tile=Main.tile;
                object map=AccessTools.Field(typeof(Chest),"_chestsByCoords").GetValue(null);
                try{Main.netMode=2;Main.dedServ=true;Main.myPlayer=255;Main.player=Players;Main.chest=Chests;Main.tile=tiles;AccessTools.Field(typeof(Chest),"_chestsByCoords").SetValue(null,coordinates);action();}
                finally{coordinates=AccessTools.Field(typeof(Chest),"_chestsByCoords").GetValue(null);Main.netMode=mode;Main.dedServ=dedicated;Main.myPlayer=my;Main.player=player;Main.chest=chest;Main.tile=tile;AccessTools.Field(typeof(Chest),"_chestsByCoords").SetValue(null,map);}
            }
            internal void Receive(byte[] packet,int user=0)
            {
                Run(()=>{var receiver=new MessageBuffer{whoAmI=user};Array.Copy(packet,2,receiver.readBuffer,0,packet.Length-2);receiver.ResetReader();int kind;receiver.GetData(0,packet.Length-2,out kind);Require(kind==packet[2],"Original server GetData consumed the real chest request.");});
            }
        }
        private static void ContainerServerPermission(object context,object host,object tools,object input,bool lateManual,int inventoryPhase=-1)
        {
            int mode=Main.netMode;bool disconnected=Netplay.Disconnect;var connection=Netplay.Connection;var client=Main.clientPlayer;var sections=Main.sectionManager;var buffers=NetMessage.buffer;
            var clients=(RemoteClient[])Netplay.Clients.Clone();var current=new RemoteServer{State=10,PendingTermination=true,ReadBuffer=new byte[2048]};var audit=new Harmony("JueMingR.Tests.ContainerRepair"+lateManual+":"+inventoryPhase);
            Player cursorPlayer=null;Item ownedCursor=null;
            using(new PayloadChestFixture())
            try
            {
                Player p=Reset(context,host,tools,input);string wanted="争箱恢复目标名";var entry=ContainerNameSource(context,host,input,p,wanted,!lateManual && inventoryPhase<0);
                int a=WorldGen.PlaceChest(44,42,TileID.Containers,false,0),b=WorldGen.PlaceChest(46,42,TileID.Containers,false,0);Require(a>=0 && b>=0,"Original client owns matching chest bodies.");
                Chest ca=Main.chest[a],cb=Main.chest[b];p.position=new Vector2(42*16,646);p.inventory[12].SetDefaults(ItemID.Chest);p.inventory[12].stack=3;
                var server=new ContainerRepairServerRealm();Require(server.A==a && server.B==b,"Independent original realms assign matching native chest indices.");
                NetMessage.buffer=(MessageBuffer[])buffers.Clone();NetMessage.buffer[0]=new MessageBuffer{whoAmI=0};NetMessage.buffer[1]=new MessageBuffer{whoAmI=1};NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};
                for(int i=0;i<Netplay.Clients.Length;i++)Netplay.Clients[i]=new RemoteClient{Id=i,State=10,PendingTermination=true};
                Netplay.Connection=current;Main.netMode=1;Netplay.Disconnect=false;PayloadClientSession(context,host);
                audit.Patch(AccessTools.Method(typeof(NetMessage),"SendData"),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ContainerRepairSent)));
                containerRepairClient.Clear();containerRepairServer.Clear();containerRepairRecording=true;
                int outbound=0,inbound=0;var held=new List<byte[]>();
                Action<bool> pump=allowOpen=>
                {
                    while(outbound<containerRepairClient.Count)server.Receive(containerRepairClient[outbound++]);
                    while(inbound<containerRepairServer.Count)
                    {byte[] packet=containerRepairServer[inbound++];if(packet[2]==33 && !allowOpen)held.Add(packet);else PayloadReceive(current,packet,true);}
                };
                if(!lateManual && inventoryPhase<=0)
                {
                    // Occupancy belongs only to the server's other player.
                    // The client cannot pre-reject on a copied local flag.
                    server.Players[1].chest=a;
                }
                Place(context,host,input,entry,44,41);object intent=Scene(host).Single();SetAutomatic(host,input,true);
                for(int i=0;i<240 && !containerRepairClient.Any(q=>q[2]==31);i++){Step(context,input);pump(!lateManual && inventoryPhase<1);}
                Require(containerRepairClient.Count(q=>q[2]==31)==1,"The real automatic name intention submits its first original31.");
                if(inventoryPhase>=0)
                {
                    pump(inventoryPhase==0);
                    if(inventoryPhase==0)
                        Require(!containerRepairServer.Any(q=>q[2]==33) && p.chest==-1,"The retry boundary begins with a genuine rejected31 and no native permission.");
                    else
                    {
                        Require(held.Count==1 && server.Players[0].chest==a,"The permission/commit boundary uses an actual delayed original server33.");
                        if(inventoryPhase>=2)
                        {PayloadReceive(current,held.Single(),true);Require(p.chest==a && Main.playerInventory,"Original GetData33 opens its real granted chest before the player's slot operation.");}
                    }
                    if(!Main.playerInventory)p.ToggleInv();
                    Require(Main.playerInventory && Main.mouseItem.IsAir,"The player really opens inventory with no preexisting cursor loan.");
                    if(inventoryPhase==0)
                    {
                        server.Receive(PayloadPacket(33,w=>{w.Write((short)-1);w.Write((short)0);w.Write((short)0);w.Write((byte)0);}),1);
                        Require(server.Players[1].chest==-1,"Original other-player close makes the server chest available while manual inventory remains busy.");
                        for(int i=0;i<660;i++){Step(context,input);pump(true);}
                        Require(containerRepairClient.Count(q=>q[2]==31)==1 && Main.playerInventory && Main.mouseItem.IsAir && p.chest==-1 && ca.name=="",
                            "An actual open player inventory alone closes retry admission even after the contention ends and600 ticks elapse.");
                    }
                    p.inventory[20].SetDefaults(ItemID.StoneBlock);p.inventory[20].stack=3;
                    ManualPreparationClick(context,input,p,20);ownedCursor=Main.mouseItem;cursorPlayer=p;
                    Require(Main.playerInventory && ownedCursor.type==ItemID.StoneBlock && ownedCursor.stack==3 && p.inventory[20].IsAir,
                        "Original ItemSlot.LeftClick really transfers the player's bag stack to the cursor.");
                    // Same granted-window/manual-item boundary, but exercise
                    // the synchronous cancellation outlet before any Advance
                    // can retire the active name. It owns no earlier bag bit.
                    if(inventoryPhase==3)SetAutomatic(host,input,false);
                    if(inventoryPhase==0)
                    {
                        // Player.Update calls dropItemCheck before selection;
                        // closing inventory causes its original GetOrDropItem
                        // to return the cursor on the next Update. Keep the
                        // real inventory open for a genuinely held-item wait,
                        // rather than inventing660 closed-panel cursor ticks.
                        for(int i=0;i<660;i++){Step(context,input);pump(true);}
                        Require(containerRepairClient.Count(q=>q[2]==31)==1 && Main.playerInventory && ReferenceEquals(Main.mouseItem,ownedCursor) && ownedCursor.stack==3 && ca.name=="",
                            "A genuinely persistent player cursor item keeps retry admission closed without suppressing original cursor-return behavior; actual31="+containerRepairClient.Count(q=>q[2]==31)+" sameCursor="+ReferenceEquals(Main.mouseItem,ownedCursor)+" cursor="+Main.mouseItem.type+":"+Main.mouseItem.stack+" inventory="+Main.playerInventory+" chest="+p.chest+" actualName="+ca.name);
                    }
                    else if(inventoryPhase==1)
                    {Step(context,input);pump(false);PayloadReceive(current,held.Single(),true);}
                    for(int i=0;i<12;i++){Step(context,input);pump(true);}
                    Require(Main.playerInventory && ReferenceEquals(Main.mouseItem,ownedCursor) && ownedCursor.type==ItemID.StoneBlock && ownedCursor.stack==3 && p.inventory[20].IsAir &&
                        ca.name=="" && server.Chests[a].name=="" && server.Chests[b].name=="" && p.inventory[12].stack==3 && Scene(host).Length==1,
                        "Manual inventory/cursor ownership stops automatic naming and survives retry/permission/commit cleanup without a stale InventoryWasOpen=false close, item rewrite or fake Match; phase="+inventoryPhase);
                    Require(!containerRepairClient.Any(q=>q[2]==33 && q[9]!=0),"No naming submission crosses the player's current inventory operation gate; phase="+inventoryPhase);
                    if(inventoryPhase==0)Require(containerRepairClient.Count(q=>q[2]==31)==1 && p.chest==-1,"Crossing600 ticks while the player holds an item must not bypass initial31 admission.");
                    if(inventoryPhase==1)Require(p.chest==-1,"A late permission must be rejected before changing the player's UI when manual admission is closed.");
                    if(inventoryPhase==3)Require(!(bool)Get(host,"AutoBuildEnabled") && p.chest==-1 && server.Players[0].chest==-1,
                        "Immediate automatic-OFF returns only its exact granted chest through original33 without closing the player's bag or borrowing a name edit.");
                    Console.WriteLine("CONTAINER N2 MANUAL phase="+inventoryPhase+" original-ToggleInv/LeftClick=true retry/permission/commit-gated=true inventory-preserved=true cursor-stack=3 authority-unmodified=true");
                    if(inventoryPhase==0)
                    {
                        ManualPreparationClick(context,input,p,20);
                        Require(Main.mouseItem.IsAir && p.inventory[20].type==ItemID.StoneBlock && p.inventory[20].stack==3,
                            "Original slot consumer returns the exact player stack before manual inventory is closed.");
                        ownedCursor=null;p.ToggleInv();Require(!Main.playerInventory,"The player actually closes the inventory after returning its cursor item.");
                        for(int i=0;i<1320 && Scene(host).Length!=0;i++)
                        {Require(ReferenceEquals(Scene(host).Single(),intent),"Temporary inventory blockage retains the original projection intention.");Step(context,input);pump(true);}
                        Require(containerRepairClient.Count(q=>q[2]==31)>=2 && containerRepairServer.Any(q=>q[2]==33) &&
                            Scene(host).Length==0 && ca.name==wanted && server.Chests[a].name==wanted && server.Chests[b].name=="" &&
                            Main.mouseItem.IsAir && p.inventory[20].type==ItemID.StoneBlock && p.inventory[20].stack==3 && p.inventory[12].stack==3,
                            "After the real player operation ends, the same intention obtains fresh native31/33 permission, commits through server33/69 and reaches final Match without changing either item source.");
                        Console.WriteLine("CONTAINER N2 MANUAL recovery same-intent=true original-slot-return=true original31/33/69=true final-Match=true player-stack=3 body-debit=0");
                    }
                    return;
                }
                if(lateManual)
                {
                    pump(false);Require(held.Count==1 && server.Players[0].chest==a,"Original server accepted A and produced its real permission before the delayed delivery.");
                    int successes=containerRepairServer.Count(q=>q[2]==33);
                    server.Receive(containerRepairClient.First(q=>q[2]==31));
                    Require(containerRepairServer.Count(q=>q[2]==33)==successes && server.Players[0].chest==a,
                        "Original UsingChest also rejects a duplicate31 from the player already owning A; it does not mint another success permission.");
                    byte[] old=held.Single();SetAutomatic(host,input,false);
                    NetMessage.SendData(31,-1,-1,null,cb.x,cb.y);pump(true);
                    Require(p.chest==b && server.Players[0].chest==b,"A genuine later manual31/server33 opens B on both ends.");
                    int closes=containerRepairClient.Count(q=>q[2]==33 && BitConverter.ToInt16(q,3)==-1);
                    PayloadReceive(current,old,true);for(int i=0;i<12;i++){Step(context,input);pump(true);}
                    Require(p.chest==b && server.Players[0].chest==b && ca.name=="" && cb.name=="" && server.Chests[a].name=="" && server.Chests[b].name=="" &&
                        containerRepairClient.Count(q=>q[2]==33 && BitConverter.ToInt16(q,3)==-1)==closes,
                        "The old real A permission neither edits either chest nor overwrites/closes the successor manual B UI.");
                }
                else
                {
                    pump(true);Require(!containerRepairServer.Any(q=>q[2]==33) && p.chest==-1 && server.Players[0].chest==-1 && ca.name=="" && Scene(host).Length==1,
                        "Original case31 really refuses server contention without a successful33; local body/name is not permission.");
                    Require(!(bool)Get(Get(tools,"Use"),"Active") && !(bool)Get(Get(Get(context,"items"),"Ownership"),"HasUse"),
                        "Waiting for a server open permission retains no tool/material use token or local chest UI.");
                    p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;Call(Get(Get(context,"items"),"World"),"InvalidateObservation");
                    for(int i=0;i<120;i++){Step(context,input);pump(true);}
                    Require(containerRepairClient.Count(q=>q[2]==31)==1 && Main.tile[48,42].nactive() && Main.tile[48,42].type==TileID.Stone && p.inventory[13].stack==1 && ca.name=="" && Scene(host).Length==1,
                        "A refused name does not resend31 every Update or block this same asset's ordinary native placement/debit while authority is absent.");
                    // Original case33 closes the other player's real server
                    // ownership. It does not send a synthetic successful33.
                    server.Receive(PayloadPacket(33,w=>{w.Write((short)-1);w.Write((short)0);w.Write((short)0);w.Write((byte)0);}),1);
                    Require(server.Players[1].chest==-1,"The other player really released its server chest via GetData33.");
                    for(int i=0;i<1320 && Scene(host).Length!=0;i++)
                    {Require(ReferenceEquals(Scene(host).Single(),intent),"Permission retries retain the same projection intention.");Step(context,input);pump(true);}
                    int attempts=containerRepairClient.Count(q=>q[2]==31);
                    Require(attempts>=2 && attempts<=4 && containerRepairServer.Any(q=>q[2]==33) && Scene(host).Length==0 && ca.name==wanted && server.Chests[a].name==wanted && server.Chests[b].name=="" && p.inventory[12].stack==3,
                        "After real contention ends, the same automatic intention retries, receives actual permission, submits via original33, observes authoritative69 and retires with zero body debit.");
                }
                Console.WriteLine("CONTAINER N2 original-server31/33/69=true scenario="+(lateManual?"old-A-after-manual-B":"contention-release-same-intent-retry")+" native31-count="+containerRepairClient.Count(q=>q[2]==31)+" debit=0");
            }
            finally
            {
                try
                {
                    SetAutomatic(host,input,false);Call(host,"ClearProjection");
                    // Return only this fixture's still-identical cursor stack
                    // through the original slot consumer, never assign or
                    // erase Main.mouseItem during failure cleanup.
                    if(ownedCursor!=null && ReferenceEquals(Main.mouseItem,ownedCursor) && !ownedCursor.IsAir && cursorPlayer.inventory[20].IsAir)
                        ManualPreparationClick(context,input,cursorPlayer,20);
                }
                finally{containerRepairRecording=false;audit.UnpatchAll(audit.Id);containerRepairClient.Clear();containerRepairServer.Clear();Main.netMode=mode;Netplay.Disconnect=disconnected;Netplay.Connection=connection;Main.clientPlayer=client;Main.sectionManager=sections;NetMessage.buffer=buffers;Array.Copy(clients,Netplay.Clients,clients.Length);current.Socket.Close();Call(context,"UpdateRuntime");}
            }
        }
    }
}
