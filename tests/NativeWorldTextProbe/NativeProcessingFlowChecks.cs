using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingFlowChecks
    {
        internal static void Run(object context,object host,object input)
        {
            var audit=new Harmony("JueMingR.Tests.ProcessingStorageEvidence");
            var method=typeof(Terraria.GameContent.QuickStacking).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="QuickStackToNearbyChests" && m.GetParameters().Length==3);
            audit.Patch(method,finalizer:new HarmonyMethod(typeof(NativeProcessingFlowChecks),nameof(StorageError)));
            try{RunCases(context,host,input);}finally{audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void StorageError(Exception __exception){if(__exception!=null)Console.WriteLine(__exception);}
        private static void RunCases(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var items=Get(host,"Items");var feature=Get(items,"Feature");
            InitializeShop();p.position=new Vector2(640,640);Main.playerInventory=true;
            ItemSorting.SetupWhiteLists();
            for(int kind=0;kind<4;kind++)
            {
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(9);p.inventory[12].SetDefaults(4345);p.inventory[12].stack=20;
                p.inventory[3].SetDefaults(2002);p.inventory[3].stack=20; // A reliable grant authorizes the old and new compatible whole stack.
                p.selectedItemState.Select(0);p.selectedItemState.Update();p.mouseInterface=false;p.itemAnimation=p.itemTime=0;Main.mouseItem.TurnToAir();Main.HoverItem.TurnToAir();
                Main.SetNPCShopIndex(kind==0 || kind==3?1:0);p.SetTalkNPC(kind==0 || kind==3?0:-1);Main.InReforgeMenu=false;
                var chest=MakeChest();
                Call(items,"Change",new ItemAutomationSettings(kind==2 || kind==3,kind==0 || kind==3,kind==1 || kind==3,new[]{2002},new[]{2002},false));
                NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
                Call(host,"Set",0,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",0);});
                for(int frame=0;frame<8;frame++)
                {
                    // Fixed native RNG fixture yields the mandatory worm stack;
                    // lottery correctness is separately covered by all bag kinds.
                    Main.rand=new Terraria.Utilities.UnifiedRandom(123);
                    long money=NativeCoinChecks.Total(p.inventory,54);int stored=chest.item.Where(i=>i.type==2002).Sum(i=>i.stack);
                    NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
                    Require(p.inventory[12].stack==19-frame,"flow kind="+kind+" frame="+frame+" one bag every held update");
                    var result=Call(feature,"LastResult",(ItemActionKind)(kind==0 || kind==3?1:kind==1?2:0));
                    Require(!p.inventory.Any(i=>i.type==2002 && i.stack>0),"flow kind="+kind+" frame="+frame+" real native processing completes before next bag; result="+(result==null?"null":Get(result,"State"))+" reason="+(result==null?"":GetOptional(result,"Reason")));
                    if(kind==0 || kind==3)Require(NativeCoinChecks.Total(p.inventory,54)>money && Main.instance.shop[1].item.Any(i=>i.type==2002 && i.buyOnce),"each held update sells with real coin and buyback changes");
                    if(kind==1)Require(p.trashItem.type==2002 && p.trashItem.stack>=(frame==0?25:5),"each held update replaces trash with actual whole-stack product");
                    if(kind==2)Require(chest.item.Where(i=>i.type==2002).Sum(i=>i.stack)>=stored+(frame==0?25:5),"each held update quick-stacks actual full product into actual chest");
                    Main.mouseRight=Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,12);Require(p.inventory[12].stack==19-frame,"real Draw cannot duplicate flow consumption");
                    Console.WriteLine("G08 sequence kind="+kind+" update="+frame+" bag=1 downstream=1 eligibleWormBacklog=0 completed="+(frame+1)+" extraDrawBag=0");
                }
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
                p.inventory[12].TurnToAir();p.inventory[3].TurnToAir();chest.item[1].SetDefaults(2002);chest.item[1].stack=7;
                Main.SetNPCShopIndex(0);p.SetTalkNPC(-1);p.chest=0;Main.cursorOverride=0;
                NativeReforgeChecks.Sample(input,true);ItemSlot.LeftClick(chest.item,3,1);
                NativeReforgeChecks.Sample(input,false);Call(context,"UpdateRuntime");
                NativeReforgeChecks.Sample(input,true);ItemSlot.LeftClick(p.inventory,0,3);
                Require(Main.mouseItem.IsAir && p.inventory[3].type==2002 && p.inventory[3].stack==7,"actual chest/mouse/inventory manual transfer completed");
                NativeReforgeChecks.Sample(input,false);p.chest=-1;Main.SetNPCShopIndex(kind==0 || kind==3?1:0);p.SetTalkNPC(kind==0 || kind==3?0:-1);Call(context,"UpdateRuntime");
                Require(!(bool)Get(Get(items,"World"),"HasManualOperation"),"manual transfer barrier retires on real release");
                // Withdrawal becomes current sale/trash stock, but cannot reuse
                // the old bag grant as a storage opportunity.
                for(int i=0;i<12;i++){NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");}
                Require(kind==2 ? p.inventory[3].type==2002 && p.inventory[3].stack==7 : p.inventory[3].IsAir,"withdrawn stock is processed by sale/trash only; storage still requires a new causal source");
            }
            Main.SetNPCShopIndex(0);p.SetTalkNPC(-1);Main.chest[0]=null;
            Call(items,"Change",ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return !(bool)Get(feature,"Enabled");});
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            NoTargets(context,host,input);NonShop(context,host,input);ConflictAndRecovery(context,host,input);
            NativeProcessingPickupChecks.Run(context,host,input);
            Console.WriteLine("PASS G08 flow: four configurations each have eight successive native completions during held bags; no-target states keep bag cadence, exact conflict resumes immediately, G07 remains independent.");
        }
        private static void NoTargets(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var items=Get(host,"Items");
            for(int test=0;test<3;test++)
            {
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(9);p.inventory[12].SetDefaults(4345);p.inventory[12].stack=30;
                if(test==2){var chest=MakeChest();foreach(var item in chest.item){item.SetDefaults(2002);item.stack=item.maxStack;}}else Main.chest[0]=null;
                Call(items,"Change",new ItemAutomationSettings(test!=0,true,true,new[]{9},new[]{9},false));NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
                for(int frame=0;frame<20;frame++){NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");Require(p.inventory[12].stack==29-frame,"no shop/list match/chest capacity cannot add a fixed pause: "+test);}
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");Require(p.inventory[12].stack==10,"no target backlog starts an extra bag after release");
            }
            Main.chest[0]=null;Call(items,"Change",ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
        }
        private static void NonShop(object context,object host,object input)
        {
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(4345);p.inventory[12].stack=10;
            for(int mode=0;mode<5;mode++)
            {
                p.SetTalkNPC(mode==0 || mode==3?0:-1);Main.SetNPCShopIndex(mode==3?1:0);
                Main.InReforgeMenu=mode==1;Main.InGuideCraftMenu=mode==2;Main.npcChatText=mode==4?"ordinary dialogue":"";
                Main.npc[0].active=mode!=3;NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
                Require(p.inventory[12].stack==10,"only valid merchant shop may relax NPC/chat/service admission: "+mode);
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            }
            p.SetTalkNPC(-1);Main.SetNPCShopIndex(0);Main.InReforgeMenu=Main.InGuideCraftMenu=false;Main.npcChatText="";Main.npc[0].active=true;
        }
        private static void ConflictAndRecovery(object context,object host,object input)
        {
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(9);p.inventory[12].SetDefaults(4345);p.inventory[12].stack=10;
            var ownership=(JueMingR.Platform.Items.ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");Require(ownership.TryBeginUse(ownership.Session,12,987),"synthetic real overlapping use lease admitted");
            for(int i=0;i<8;i++){NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");}Require(p.inventory[12].stack==10,"real overlapping source blocks bag consume");
            ownership.EndUse(ownership.Session,987);NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");Require(p.inventory[12].stack==9,"conflict release resumes next Update without cooldown");
            var recovery=Get(context,"Recovery");var prefs=(JueMingR.Features.Recovery.RecoverySettings)Get(recovery,"Potions");
            p.statLifeMax2=500;p.statLife=449;p.potionDelay=0;p.inventory[3].SetDefaults(188);p.inventory[3].stack=4;
            NativeRecoveryChecks.Save(prefs,new JueMingR.Features.Recovery.RecoveryOptions(2));NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
            Require(p.inventory[12].stack==8 && p.inventory[3].stack==3 && p.statLife==500,"finished bag scope permits actual G07 heal in same held update");
            NativeRecoveryChecks.Save(prefs,new JueMingR.Features.Recovery.RecoveryOptions());NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
        }
        internal static void InitializeShop()
        {
            for(int i=0;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{active=false};
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            var panel=typeof(Main).GetField("_newChatPanel",BindingFlags.Instance|BindingFlags.NonPublic);panel.SetValue(Main.instance,Activator.CreateInstance(panel.FieldType,true));
            Main.instance.shop=new Chest[100];Main.instance.shop[1]=Chest.CreateShop();
            Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();Main.ShopHelper=new Terraria.GameContent.ShopHelper();
            Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            Main.npc[0].SetDefaults(17);Main.npc[0].active=true;Main.npc[0].homeless=true;Main.LocalPlayer.currentShoppingSettings.PriceAdjustment=1f;
        }
        private static Chest MakeChest()
        {
            for(int y=0;y<2;y++)for(int x=0;x<2;x++){var t=Main.tile[42+x,40+y];t.active(true);t.type=21;t.frameX=(short)(x*18);t.frameY=(short)(y*18);}
            Main.chest[0]=null;var chest=Chest.CreateWorldChest(0,42,40);chest.item[0].SetDefaults(2002);chest.item[0].stack=1;return chest;
        }
    }
}
