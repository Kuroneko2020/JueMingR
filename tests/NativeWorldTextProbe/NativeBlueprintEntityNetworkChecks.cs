using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.LeashedEntities;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.Net;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void PayloadOriginalAltPlacement(Player p,int x,int y)
        {
            int mx=Main.mouseX,my=Main.mouseY,tx=Player.tileTargetX,ty=Player.tileTargetY;
            bool left=Main.mouseLeft,right=Main.mouseRight,sm=Main.SmartCursorWanted_Mouse,sp=Main.SmartCursorWanted_GamePad;
            bool use=p.controlUseItem,tile=p.controlUseTile;
            try
            {
                Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
                Main.mouseX=(int)(x*16+8-Main.screenPosition.X);Main.mouseY=(int)(y*16+8-Main.screenPosition.Y);Player.tileTargetX=x;Player.tileTargetY=y;
                p.controlUseItem=p.controlUseTile=false;
                for(int frame=0;frame<80 && (p.itemAnimation>0 || p.itemTime>0 || p.altFunctionUse!=0);frame++)p.ItemCheck();
                Require(p.itemAnimation==0 && p.itemTime==0 && p.altFunctionUse==0 && p.releaseUseItem,"Original source has a neutral native alternate-input boundary.");
                //1 is input, never the consumed2 state: ItemCheck itself must
                // promote and enter the real alternate placement/post hook.
                p.altFunctionUse=1;p.controlUseItem=p.controlUseTile=true;Main.mouseRight=true;Main.mouseLeft=false;p.ItemCheck();
                p.controlUseItem=p.controlUseTile=false;Main.mouseRight=false;
                for(int frame=0;frame<80 && (p.itemAnimation>0 || p.itemTime>0 || p.altFunctionUse!=0);frame++)p.ItemCheck();
            }
            finally{Main.mouseX=mx;Main.mouseY=my;Player.tileTargetX=tx;Player.tileTargetY=ty;Main.mouseLeft=left;Main.mouseRight=right;Main.SmartCursorWanted_Mouse=sm;Main.SmartCursorWanted_GamePad=sp;p.controlUseItem=use;p.controlUseTile=tile;}
        }

        // Two finite native realms own different Tile arrays, TE dictionaries
        // and original Leashed collections. Tile20/87/156/FullSync create the
        // server/client results; this snapshot only isolates their containers.
        private sealed class EntityNetworkState
        {
            private readonly Dictionary<int,TileEntity> ids=TileEntity.ByID;
            private readonly Dictionary<Point16,TileEntity> positions=TileEntity.ByPosition;
            private readonly List<TileEntity> updates=TileEntity.UpdateEntities;
            private readonly int next=TileEntity.TileEntitiesNextID;
            private readonly Tile[,] tiles=Main.tile;
            private readonly Array sections=(Array)((Array)AccessTools.Field(typeof(LeashedEntity),"BySection").GetValue(null)).Clone();
            private readonly object[] active=((IEnumerable)AccessTools.Field(typeof(LeashedEntity),"ActiveSectionList").GetValue(null)).Cast<object>().ToArray();
            private readonly object[] slots=((IEnumerable)AccessTools.Field(typeof(LeashedEntity),"ByWhoAmI").GetValue(null)).Cast<object>().ToArray();
            internal void Restore()
            {
                Main.tile=tiles;TileEntity.ByID=ids;TileEntity.ByPosition=positions;TileEntity.UpdateEntities=updates;TileEntity.TileEntitiesNextID=next;
                var target=(Array)AccessTools.Field(typeof(LeashedEntity),"BySection").GetValue(null);Array.Copy(sections,target,sections.Length);
                var a=(IList)AccessTools.Field(typeof(LeashedEntity),"ActiveSectionList").GetValue(null);a.Clear();foreach(object value in active)a.Add(value);
                var s=(IList)AccessTools.Field(typeof(LeashedEntity),"ByWhoAmI").GetValue(null);s.Clear();foreach(object value in slots)s.Add(value);
            }
        }
        private static readonly List<byte[]> entityClientRequests=new List<byte[]>(),entityServerTiles=new List<byte[]>(),entityServerModules=new List<byte[]>();
        private static bool entityNetworkWatching;
        private static void EntityNetworkSent(int __0,int __1)
        {
            if(!entityNetworkWatching || Main.netMode==1 && __0!=20 && __0!=87 && __0!=156 || Main.netMode==2 && __0!=86 || Main.netMode==0)return;
            byte[] data=NetMessage.buffer[Main.netMode==2 && __1>=0?__1:256].writeBuffer;int count=BitConverter.ToUInt16(data,0);
            Require(count>=3 && data[2]==__0,"Original entity message serializer produced the observed wire type.");
            (Main.netMode==1?entityClientRequests:entityServerTiles).Add(data.Take(count).ToArray());
        }
        private static void EntityNetworkBroadcast(NetPacket __0)
        {
            if(!entityNetworkWatching || Main.netMode!=2 || __0.Id!=NetManager.Instance.GetId<LeashedEntity.NetModule>())return;
            // The same original finalizer used by NetManager.SendData seals
            // framing before the owned disconnected transport recycles it.
            // No entity field or payload byte is invented by the recorder.
            __0.ShrinkToFit();entityServerModules.Add(__0.Buffer.Data.Take(__0.Length).ToArray());
        }
        private static void EntityServerReceive(byte[] packet)
        {
            var receiver=new MessageBuffer{whoAmI=0};Array.Copy(packet,2,receiver.readBuffer,0,packet.Length-2);receiver.ResetReader();int kind;
            receiver.GetData(0,packet.Length-2,out kind);Require(kind==packet[2],"Original server decoder consumed the real client entity request.");
        }
        private static void PayloadLeashedNetworkCases(object context,object host,object tools,object input)
        {
            if(LeashedKite.Prototype==null)LeashedEntity.Registry.RegisterAll();
            if(NetManager.Instance.GetModule<LeashedEntity.NetModule>()==null)Terraria.Initializers.NetworkInitializer.Load();
            int[] kites=ContentSamples.ItemsByType.Values.Where(i=>i.createTile==723 && ItemID.Sets.IsAKite[i.type]).GroupBy(i=>i.shoot).Where(g=>g.Count()==1).Select(g=>g.Single().type).OrderBy(i=>i).Take(2).ToArray();
            Require(kites.Length==2,"Independent original kite catalog has two uniquely identifiable projectiles.");
            int critter=ContentSamples.ItemsByType.Values.Where(i=>i.createTile==724 && i.makeNPC>0).GroupBy(i=>new{Type=TECritterAnchor.GetLeashedCritterPrototype(i.type).Type,Npc=i.makeNPC,Style=TECritterAnchor.GetLeashedCritterPrototype(i.type).anchorStyle}).Where(g=>g.Count()==1).Select(g=>g.Single().type).OrderBy(i=>i).First();
            PayloadLeashedNetwork(context,host,tools,input,kites[0],kites[1]);
            PayloadLeashedNetwork(context,host,tools,input,critter,0);
            var first=PayloadLeashedNetwork(context,host,tools,input,1994,1995);var second=PayloadLeashedNetwork(context,host,tools,input,1995,0);
            var replaced=first;replaced.Payload.EntityItemType=second.Payload.EntityItemType;
            Require(first.FrameX==second.FrameX && first.FrameY==second.FrameY && first.Payload.EntityItemType==1994 && second.Payload.EntityItemType==1995 &&
                PayloadCellBytes(replaced).SequenceEqual(PayloadCellBytes(second)) && !PayloadCellBytes(first).SequenceEqual(PayloadCellBytes(second)),
                "Original normal-butterfly variants have identical Tile/frame and otherwise equal semantic cell fields but distinct saved item bytes and true FullSync identities, without comparing independent root hashes.");
        }
        private static CaptureCell PayloadLeashedNetwork(object context,object host,object tools,object input,int itemType,int replacementItem)
        {
            var original=new EntityNetworkState();int mode=Main.netMode;bool dedicated=Main.dedServ,disconnected=Netplay.Disconnect;
            var clients=(RemoteClient[])Netplay.Clients.Clone();var connection=Netplay.Connection;var oldBuffers=NetMessage.buffer;EntityNetworkState clientState=null;
            var client=Main.clientPlayer;var sections=Main.sectionManager;
            int npcType=ContentSamples.ItemsByType[itemType].makeNPC;
            Asset<Texture2D> oldNpcAsset=npcType>0?TextureAssets.Npc[npcType]:null;
            var current=new RemoteServer{State=10,PendingTermination=true,ReadBuffer=new byte[2048]};var audit=new Harmony("JueMingR.Tests.EntityNetwork"+itemType);
            CaptureCell identity=default(CaptureCell);
            using(new PayloadEntityFixture())
            try
            {
                // AssetInitializer normally supplies a DoNotLoad handle for
                // every NPC. FullSync invokes the original critter Update and
                // FindFrame: its real unloaded-asset branch must see a handle,
                // rather than a missing CPU-host array entry. No texture/frame
                // dimensions, netMode or identity consumer is substituted.
                if(npcType>0 && oldNpcAsset==null)
                    TextureAssets.Npc[npcType]=(Asset<Texture2D>)Activator.CreateInstance(typeof(Asset<Texture2D>),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{"Images/NPC_"+npcType},null);
                LeashedEntity.Clear();Player p=Reset(context,host,tools,input);p.position.X=58*16;p.inventory[0].SetDefaults(itemType);p.inventory[0].stack=3;
                PayloadOriginalAltPlacement(p,60,42);int type=p.inventory[0].createTile;
                Require(Main.tile[60,42].nactive() && PayloadItemAt(60,42)==itemType && p.inventory[0].stack==2,"The source entity comes from original alternate ItemCheck/post-place and real debit.");
                Require(WorldGen.PlaceTile(62,42,TileID.Stone,true,true,p.whoAmI),"Independent ordinary source material beside the native entity.");
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,42,3,1,"原生联机实体"+itemType);
                identity=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry).Cells.Single(c=>c.Payload.EntityState==BlueprintPayloadState.Known);
                Require(identity.Payload.EntityItemType==itemType && identity.FrameX==Main.tile[60,42].frameX && identity.FrameY==Main.tile[60,42].frameY,"The saved network source retains the actual native item and frames.");
                TileEntity.Remove(TileEntity.ByPosition[new Point16(60,42)]);LeashedEntity.Clear();p.position=new Vector2(42*16,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(itemType);p.inventory[12].stack=3;
                Netplay.Connection=current;NetMessage.buffer=(MessageBuffer[])oldBuffers.Clone();
                for(int i=0;i<NetMessage.buffer.Length;i++)if(NetMessage.buffer[i]==null)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};Main.netMode=1;Main.dedServ=false;Netplay.Disconnect=false;
                for(int i=0;i<Netplay.Clients.Length;i++)Netplay.Clients[i]=new RemoteClient{Id=i,State=10,PendingTermination=true};
                PayloadClientSession(context,host);
                audit.Patch(AccessTools.Method(typeof(NetMessage),"SendData"),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(EntityNetworkSent)));
                foreach(var method in typeof(NetManager).GetMethods(F).Where(m=>m.Name=="Broadcast"))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(EntityNetworkBroadcast)));
                entityClientRequests.Clear();entityServerTiles.Clear();entityServerModules.Clear();entityNetworkWatching=true;
                Place(context,host,input,entry,44,42);SetAutomatic(host,input,true);
                Until(context,input,()=>entityClientRequests.Any(b=>b[2]==156));
                Require(p.inventory[12].stack==2 && Main.tile[44,42].type==type,"Production alternate ItemCheck debits one real client material before server identity confirmation.");
                var cell=new CaptureCell{X=44,Y=42,Layers=CaptureLayer.Tile,TileType=(ushort)type,FrameX=Main.tile[44,42].frameX,FrameY=Main.tile[44,42].frameY};
                clientState=new EntityNetworkState();
                using(new PayloadEntityFixture())
                {
                    LeashedEntity.Clear();Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
                    for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)Main.tile[x,y]=new Tile();
                    Main.netMode=2;Main.dedServ=true;
                    foreach(byte[] packet in entityClientRequests.Where(b=>b[2]==20).ToArray())EntityServerReceive(packet);
                    Require(Main.tile[44,42].nactive() && Main.tile[44,42].type==type,"The server's initially empty world receives the actual client Tile20 before87.");
                    EntityServerReceive(entityClientRequests.Single(b=>b[2]==87));
                    Require(PayloadItemAt(44,42)==0,"Real server87 creates an empty anchor before156, not a prefilled fixture TE.");
                    var serverTE=TileEntity.ByPosition[new Point16(44,42)];
                    Action<Action> onClient=action=>
                    {
                        var serverState=new EntityNetworkState();clientState.Restore();Main.netMode=1;Main.dedServ=false;
                        try{action();}
                        finally{clientState=new EntityNetworkState();serverState.Restore();Main.netMode=2;Main.dedServ=true;}
                    };
                    onClient(()=>
                    {
                        PayloadReceive(current,entityServerTiles.Last(),true);
                        Require(PayloadItemAt(44,42)==0 && PayloadRead(host,cell).EntityState==BlueprintPayloadState.Unknown,"Real empty86 cannot establish entity identity.");
                        p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;Call(Get(Get(context,"items"),"World"),"InvalidateObservation");
                        Until(context,input,()=>Main.tile[46,42].nactive() && Main.tile[46,42].type==TileID.Stone);
                        Require(p.inventory[13].stack==1 && p.inventory[12].stack==2 && Scene(host).Length==1,"An entity waiting for FullSync does not block a newly available ordinary material's actual debit and placement, or duplicate the entity debit.");
                    });
                    EntityServerReceive(entityClientRequests.Single(b=>b[2]==156));Require(PayloadItemAt(44,42)==itemType,"Real server156 inserts the original client item and creates a Leashed entity.");
                    byte[] full=entityServerModules.Last();
                    onClient(()=>
                    {
                        PayloadReceive(current,full,true);var actual=PayloadRead(host,cell);
                        Require(actual.EntityState==BlueprintPayloadState.Known && actual.EntityItemType==itemType && PayloadItemAt(44,42)==0,"Original FullSync establishes the unique real identity while the client TE remains genuinely empty.");
                        var old=new RemoteServer{ReadBuffer=new byte[2048]};try{old.ClientReadCallBack(null,0);}finally{old.Socket.Close();}
                        Require(PayloadRead(host,cell).EntityItemType==itemType,"An old empty callback cannot taint the current synchronized identity.");
                        Until(context,input,()=>Scene(host).Length==0);Require(p.inventory[12].stack==2 && p.inventory[13].stack==1,"Only final true entity identity retires the asset, with exactly one entity and ordinary material debit.");
                    });
                    if(replacementItem!=0)
                    {
                        NetMessage.SendData(86,-1,-1,null,serverTE.ID);byte[] filled=entityServerTiles.Last();
                        onClient(()=>{PayloadReceive(current,filled,true);Require(PayloadItemAt(44,42)==itemType && PayloadRead(host,cell).EntityState==BlueprintPayloadState.Unknown,"A new nonzero86 TE cannot borrow a prior live Leashed proof before its own FullSync.");});
                        LeashedEntity live=((IEnumerable)AccessTools.Field(typeof(LeashedEntity),"ByWhoAmI").GetValue(null)).Cast<LeashedEntity>().Single(e=>e!=null && e.active);
                        LeashedEntity.NetModule.Sync(live,true);byte[] refreshed=entityServerModules.Last();
                        onClient(()=>{PayloadReceive(current,refreshed,true);Require(PayloadRead(host,cell).EntityItemType==itemType,"A fresh real FullSync proves the replacement TE reference.");});
                        AccessTools.Method(typeof(LeashedEntity),"Remove",new[]{typeof(LeashedEntity)}).Invoke(null,new object[]{live});byte[] removed=entityServerModules.Last();
                        onClient(()=>{PayloadReceive(current,removed,true);Require(PayloadItemAt(44,42)==itemType && PayloadRead(host,cell).EntityState==BlueprintPayloadState.Unknown,"Original Remove revokes proof even under a stale nonzero TE item field.");});
                        EntityServerReceive(PayloadPacket(156,w=>{w.Write((short)44);w.Write((short)42);w.Write((short)replacementItem);}));byte[] replaced=entityServerModules.Last();
                        onClient(()=>{PayloadReceive(current,replaced,true);Require(PayloadItemAt(44,42)==itemType && PayloadRead(host,cell).EntityItemType==replacementItem,"A current real FullSync switches identity without falling back to the old nonzero TE.");});
                        TileEntity.Remove(serverTE);NetMessage.SendData(86,-1,-1,null,serverTE.ID);byte[] deleted=entityServerTiles.Last();
                        onClient(()=>{PayloadReceive(current,deleted,true);Require(PayloadRead(host,cell).EntityState==BlueprintPayloadState.Unknown,"Real client TileEntity.Remove revokes the exact anchor despite a live Leashed object.");});
                    }
                }
                clientState.Restore();Main.netMode=1;Main.dedServ=false;
                Console.WriteLine("BLUEPRINT ENTITY NETWORK item="+itemType+" real87/empty86/156/FullSync=true debit=1 ordinary-progress=true final-match=true replacement/remove="+(replacementItem!=0));
            }
            finally
            {
                try{if(clientState!=null)clientState.Restore();Main.netMode=mode;Main.dedServ=dedicated;SetAutomatic(host,input,false);Call(host,"ClearProjection");}
                finally{entityNetworkWatching=false;audit.UnpatchAll(audit.Id);original.Restore();if(npcType>0)TextureAssets.Npc[npcType]=oldNpcAsset;Main.netMode=mode;Main.dedServ=dedicated;Netplay.Disconnect=disconnected;Netplay.Connection=connection;NetMessage.buffer=oldBuffers;Array.Copy(clients,Netplay.Clients,clients.Length);Main.clientPlayer=client;Main.sectionManager=sections;current.Socket.Close();entityClientRequests.Clear();entityServerTiles.Clear();entityServerModules.Clear();Call(context,"UpdateRuntime");}
            }
            return identity;
        }
    }
}
