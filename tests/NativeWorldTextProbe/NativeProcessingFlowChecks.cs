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
            for(int kind=0;kind<3;kind++)
            {
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(9);p.inventory[12].SetDefaults(4345);p.inventory[12].stack=20;
                p.inventory[3].SetDefaults(2002);p.inventory[3].stack=20; // A reliable grant authorizes the old and new compatible whole stack.
                p.selectedItemState.Select(0);p.selectedItemState.Update();p.mouseInterface=false;p.itemAnimation=p.itemTime=0;Main.mouseItem.TurnToAir();Main.HoverItem.TurnToAir();
                Main.SetNPCShopIndex(kind==0?1:0);p.SetTalkNPC(kind==0?0:-1);Main.InReforgeMenu=false;
                var chest=MakeChest();
                Call(items,"Change",new ItemAutomationSettings(kind==2,kind==0,kind==1,new[]{2002},new[]{2002},false));
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
                    var result=Call(feature,"LastResult",(ItemActionKind)(kind==0?1:kind==1?2:0));
                    Require(!p.inventory.Any(i=>i.type==2002 && i.stack>0),"flow kind="+kind+" frame="+frame+" real native processing completes before next bag; result="+(result==null?"null":Get(result,"State"))+" reason="+(result==null?"":GetOptional(result,"Reason")));
                    if(kind==0)Require(NativeCoinChecks.Total(p.inventory,54)>money && Main.instance.shop[1].item.Any(i=>i.type==2002 && i.buyOnce),"each held update sells with real coin and buyback changes");
                    if(kind==1)Require(p.trashItem.type==2002 && p.trashItem.stack>=(frame==0?25:5),"each held update replaces trash with actual whole-stack product");
                    if(kind==2)Require(chest.item.Where(i=>i.type==2002).Sum(i=>i.stack)>=stored+(frame==0?25:5),"each held update quick-stacks actual full product into actual chest");
                    Main.mouseRight=Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,12);Require(p.inventory[12].stack==19-frame,"real Draw cannot duplicate flow consumption");
                }
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
                p.inventory[12].TurnToAir();p.inventory[3].SetDefaults(2002);p.inventory[3].stack=7;
                // No new acquisition scope: later same-type inventory growth is
                // not a permanent per-type product entitlement.
                for(int i=0;i<12;i++){NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");}
                Require(p.inventory[3].type==2002 && p.inventory[3].stack==7,"completed source cannot authorize unrelated later same-type stock");
            }
            Main.SetNPCShopIndex(0);p.SetTalkNPC(-1);Main.chest[0]=null;
            Call(items,"Change",ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return !(bool)Get(feature,"Enabled");});
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Console.WriteLine("PASS G08 flow: eight successive actual native sale/discard/storage completions during held bags, whole-stack source and no later same-type replay.");
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
