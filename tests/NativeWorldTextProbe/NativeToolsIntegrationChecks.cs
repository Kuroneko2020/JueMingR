using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Items;
using JueMingR.Features.QuickItems;
using JueMingR.Features.Recovery;
using JueMingR.Features.Tools;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsIntegrationChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly List<byte[]> packets=new List<byte[]>();
        private static object faultUse;
        private static bool SkipAchievement(){return false;}
        private static void FailAfterMining(){if(faultUse!=null && (bool)Get(faultUse,"InNativeUse"))throw new InvalidOperationException("G09 isolated native consumer fault");}
        private static void Capture(int __0)
        {
            if(Main.netMode!=1 || __0!=5 && __0!=13 && __0!=17 && __0!=70 && __0!=125)return;
            var bytes=NetMessage.buffer[256].writeBuffer;int length=BitConverter.ToUInt16(bytes,0);
            Require(length>=3 && length<=bytes.Length && bytes[2]==__0,"G09 actual serialized native packet");packets.Add(bytes.Take(length).ToArray());
        }
        private static void Clear(object context)
        {
            var host=Get(context,"Tools");var input=Get(context,"Input");for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);
            for(int i=0;i<40;i++)NativeToolsChecks.Frame(context,input);
            foreach(var item in Main.LocalPlayer.inventory)item.TurnToAir();foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=30;x<55;x++)for(int y=30;y<65;y++)Main.tile[x,y].ClearEverything();
            var p=Main.LocalPlayer;p.position=new Vector2(640,640);p.itemAnimation=p.itemTime=p.toolTime=0;p.selectedItemState.Select(0);p.selectedItemState.Update();
            p.trashItem.TurnToAir();Main.mouseLeft=false;Main.playerInventory=false;Call(Get(host,"Npcs"),"BeginTick");
        }
        internal static void Cpu(object context)
        {
            Network(context);
        }
        internal static void Visual(object context,ProbeGraphics graphics)
        {
            graphics.LoadItemTextures(new[]{Terraria.ID.ItemID.CopperPickaxe,1991,213,424,188,50,0,2289});Clear(context);
            object host=Get(context,"Tools"),input=Get(context,"Input"),quick=Get(context,"QuickItems"),processing=Get(context,"Processing"),recovery=Get(context,"Recovery");var p=Main.LocalPlayer;
            var quickSettings=(QuickItemSettings)Get(quick,"Settings");var recoverySettings=(RecoverySettings)Get(recovery,"Potions");
            NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");Call(processing,"Poll");Call(recovery,"Poll");return quickSettings.Loaded && recoverySettings.Loaded && (bool)Call(processing,"Controls",1);});
            var entry=new QuickItemEntry("9876543210abcdef9876543210abcdef",50,QuickItemMode.Use,false,true);string reason;
            Require(quickSettings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"competition quick-item entry: "+reason);NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !quickSettings.Busy;});
            var bindings=(HotkeyBindings)Get(Get(Get(context,"Shell"),"hotkeys"),"Bindings");NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");
            p.inventory[0].SetDefaults(Terraria.ID.ItemID.CopperPickaxe);p.inventory[12].SetDefaults(1991);p.inventory[14].SetDefaults(213);p.inventory[10].SetDefaults(424);p.inventory[10].stack=5;p.inventory[3].SetDefaults(188);p.inventory[3].stack=2;p.inventory[17].SetDefaults(50);
            p.statLifeMax2=500;p.statLife=300;p.potionDelay=0;p.direction=1;
            for(int x=36;x<40;x++)for(int y=39;y<43;y++)NativeToolsChecks.Tile(x,y,6);
            NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"competition real flower pot");NativeToolsChecks.Tile(42,40,84);NativeExtractionChecks.Machine(43,40,219);
            var n=Main.npc[0];n.SetDefaults(46);n.whoAmI=0;n.position=new Vector2(677,642);n.active=true;n.life=n.lifeMax;Call(Get(host,"Npcs"),"BeginTick");
            for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,1);Require((bool)Call(Get(host,"Mining"),"Select",p,38,40,6,false),"competition mining region");
            NativeRecoveryChecks.Save(recoverySettings,new RecoveryOptions(1));Call(processing,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",1);});
            var audit=new Harmony("JueMingR.Tests.G09Competition");var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",Flags);audit.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(SkipAchievement)));
            int recalls=NativeQuickItemChecks.Recalls;
            try
            {
                // G05 intentionally rejects a press during an unfinished
                // animation. Issue one real press in a finite native idle
                // window while the background tool modes remain enabled.
                bool pressed=false;
                for(int f=0;f<320;f++){bool press=!pressed && f>=15 && p.selectedItemState.CanChangeSelectedItemImmediately && !(bool)Get(Get(processing,"Extraction"),"Active");if(press)pressed=true;NativeQuickItemChecks.Sample(input,press?new[]{Keys.J}:new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");}
                Require(!n.active && Main.tile[42,40].active() && Main.tile[42,40].type==82,"capture and native harvest each make actual progress in mixed activity");
                Require(Enumerable.Range(36,4).Any(x=>Enumerable.Range(39,4).Any(y=>!Main.tile[x,y].active())),"mining makes real progress alongside capture/harvest/extraction");
                Require(p.inventory[10].IsAir || p.inventory[10].stack<5,"native extraction is not starved by enabled tool consumers");
                Require(p.statLife>300 && p.inventory[3].stack==1,"actual recovery consumes exactly one potion during tool activity");
                Require(pressed && NativeQuickItemChecks.Recalls==recalls+1,"explicit quick-item request completes one actual native delayed recall during tool activity");
            }
            finally
            {
                audit.Unpatch(achievement,HarmonyPatchType.All,audit.Id);NativeRecoveryChecks.Save(recoverySettings,new RecoveryOptions());Call(processing,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});Call(quick,"Delete",entry.Id);NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");bindings.Poll();return !quickSettings.Busy && !bindings.Busy;});Clear(context);
            }
            CaptureClient(context);Console.WriteLine("PASS G09 mixed real consumers: capture, harvest, low-pick mining, quick-item native recall, native recovery and extraction all progress.");
        }
        private static void CaptureClient(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input");var p=Main.LocalPlayer;var audit=new Harmony("JueMingR.Tests.G09CapturePackets");var send=typeof(NetMessage).GetMethod("SendData",Flags);audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(Capture)));
            try
            {
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.clientPlayer=new Player();Main.netMode=1;Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();Call(context,"UpdateRuntime");
                p.inventory[12].SetDefaults(1991);p.direction=1;var n=Main.npc[0];n.SetDefaults(46);n.whoAmI=0;n.position=new Vector2(677,642);n.active=true;n.life=n.lifeMax;Call(Get(host,"Npcs"),"BeginTick");NativeToolsChecks.SetMode(host,0,1);packets.Clear();
                for(int f=0;f<110;f++)ClientFrame(context,input);
                Require(!n.active && packets.Count(b=>b[2]==70)==1 && !p.inventory.Any(i=>i.type==n.catchItem && i.stack>0),"client actual CatchNPC emits one native request and creates no local capture inventory reward");
                Console.WriteLine("PASS G09 client actual net capture: one serialized packet70, native local prediction, no invented local reward; server acceptance not tested.");
            }
            finally{Main.netMode=0;audit.Unpatch(send,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");Clear(context);}
        }
        private static void Network(object context)
        {
            Clear(context);object host=Get(context,"Tools"),input=Get(context,"Input");var p=Main.LocalPlayer;
            var audit=new Harmony("JueMingR.Tests.G09NativePackets");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",Flags);
            audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(Capture)));audit.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(SkipAchievement)));
            try
            {
                // Original SendData serializes; the enclosing fixture rejects
                // any final socket output. This is not a server acceptance test.
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.clientPlayer=new Player();Main.netMode=1;Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();Call(context,"UpdateRuntime");
                p.inventory[0].SetDefaults(Terraria.ID.ItemID.CopperPickaxe);NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.SetMode(host,2,1);
                Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"client real low-pick region selected");packets.Clear();
                for(int f=0;f<180 && Main.tile[42,40].active();f++)ClientFrame(context,input);
                Require(!Main.tile[42,40].active() && packets.Any(b=>b[2]==17 || b[2]==125),"native client mining predicts removal and serializes original tile messages");
                NativeToolsChecks.Tile(42,40,6);int before=packets.Count(b=>b[2]==17 || b[2]==125);
                for(int f=0;f<80;f++)ClientFrame(context,input);
                Require(Main.tile[42,40].active() && packets.Count(b=>b[2]==17 || b[2]==125)==before,"later authoritative-style correction does not resurrect an already removed region member");
                NativeToolsChecks.SetMode(host,2,0);p.inventory[12].SetDefaults(213);NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"client container setup");
                NativeToolsChecks.Tile(42,40,84);Main.tile[42,40].liquid=255;NativeToolsChecks.SetMode(host,1,1);
                for(int f=0;f<100 && Main.tile[42,40].active();f++)ClientFrame(context,input);
                Require(!Main.tile[42,40].active() && ((ReplantQueue)Get(Get(host,"Herbs"),"Pending")).Count==1,"client real harvest leaves bounded water-blocked seed fallback");
                Main.tile[42,40].liquid=0;p.inventory[17].SetDefaults(307);p.inventory[17].stack=2;packets.Clear();
                for(int f=0;f<150;f++)ClientFrame(context,input);
                Require(Main.tile[42,40].active() && Main.tile[42,40].type==82 && p.inventory[17].stack==1 && packets.Any(b=>b[2]==17) && packets.Any(b=>b[2]==5),"client native seed use consumes one source and serializes placement plus inventory sync");
                Require(packets.Any(b=>b[2]==13 && b[8]==17) && packets.Last(b=>b[2]==13)[8]==0,"client original selection sync returns seed source to original pick");
                Console.WriteLine("PASS G09 client native mining/harvest/seed packets, one real consumption, selection return and later local correction; no server/socket execution.");
            }
            finally{Main.netMode=0;audit.Unpatch(send,HarmonyPatchType.All,audit.Id);audit.Unpatch(achievement,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");Clear(context);}
        }
        private static void ClientFrame(object context,object input)
        {NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");typeof(Main).GetMethod("TrySyncingMyPlayer",Flags).Invoke(null,null);NativeQuickItemChecks.NativeFrame(Main.LocalPlayer);Call(context,"UpdateRuntime");}
        internal static void Fault(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),use=Get(host,"Use");var p=Main.LocalPlayer;
            p.inventory[0].SetDefaults(1294);NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.Tile(45,40,6);NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"fault region selected");
            var audit=new Harmony("JueMingR.Tests.G09NativeFault");var method=typeof(Player).GetMethod("ItemCheck_UseMiningTools",Flags);var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",Flags);
            faultUse=use;audit.Patch(method,postfix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(FailAfterMining)));audit.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeToolsIntegrationChecks),nameof(SkipAchievement)));
            bool failed=false;int mx=211,my=219,tx=34,ty=35;
            try
            {
                for(int i=0;i<12 && !failed;i++)
                {
                    NativeQuickItemChecks.Sample(input,new Keys[0]);Main.mouseX=mx;Main.mouseY=my;Player.tileTargetX=tx;Player.tileTargetY=ty;
                    try{NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");}catch(InvalidOperationException e){Require(e.Message.Contains("G09 isolated"),"only injected exception observed");failed=true;}
                }
                Require(failed && !Main.tile[42,40].active(),"actual native mining committed before the injected consumer exception");
                Require(Main.mouseX==mx && Main.mouseY==my && Player.tileTargetX==tx && Player.tileTargetY==ty && !Main.mouseLeft && !(bool)Get(use,"InNativeUse"),"native ItemCheck finalizer restores exact borrowed input without rollback");
            }
            finally{faultUse=null;audit.Unpatch(method,HarmonyPatchType.All,audit.Id);audit.Unpatch(achievement,HarmonyPatchType.All,audit.Id);}
            for(int i=0;i<80;i++)NativeToolsChecks.Frame(context,input);
            var ownership=(JueMingR.Platform.Items.ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");
            Require(ownership.IsProtected(0) && !ownership.IsUseSlot(0) && p.selectedItem==0 && !(bool)Get(use,"Active") && Main.tile[45,40].active(),"unknown source remains protected after finite use lease and selection cleanup, with no replay");
            NativeToolsChecks.SetMode(host,2,0);NativeToolsChecks.SetMode(host,2,1);Require(ownership.IsProtected(0),"preference toggle cannot settle unknown native result");NativeToolsChecks.SetMode(host,2,0);
            Console.WriteLine("PASS G09 injected native post-mutation exception: idempotent finalizer, exact input return, finite use lease, persistent narrow unknown protection and no replay.");
        }
    }
}
