using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Fishing;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingStorageChecks
    {
        private static FishingSettings pendingSettings;
        private static bool pauseProduct,observedBusyProduct;
        private static bool checkOverflow,overflowObserved;
        private static int productCount;
        private static void AfterGive(){productCount++;if(checkOverflow)overflowObserved=Main.LocalPlayer.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==9999;}
        private static void BeforeGive()
        {if(!pauseProduct)return;pauseProduct=false;Require(pendingSettings.Set(pendingSettings.Value.Change(5,2)),"unrelated filter save admitted during returning product");observedBusyProduct=pendingSettings.Busy;}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.FishingStoreBusy");var give=typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer",BindingFlags.Instance|BindingFlags.NonPublic);
            audit.Patch(give,prefix:new HarmonyMethod(typeof(NativeFishingStorageChecks),nameof(BeforeGive)){priority=Priority.First},postfix:new HarmonyMethod(typeof(NativeFishingStorageChecks),nameof(AfterGive)));
            try{RunCases(context);}finally{pauseProduct=checkOverflow=false;pendingSettings=null;audit.Unpatch(give,HarmonyPatchType.All,audit.Id);}
        }
        private static void RunCases(object context)
        {
            object host=Get(context,"Fishing"),tools=Get(context,"Tools"),input=Get(context,"Input"),items=Get(tools,"Items");
            ItemSorting.SetupWhiteLists();
            foreach(int type in new[]{(int)ItemID.Bass,(int)ItemID.ReaverShark})
            {
                var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(type);p.inventory[12].stack=type==ItemID.Bass?12:1;
                p.inventory[13].SetDefaults(type);p.inventory[13].favorited=true;
                var chest=Chest(type);int old=p.inventory[12].stack;
                NativeFishingChecks.Save(host,new FishingOptions(auto:true,storeMode:1));var bobber=Cast(context,input);
                Require(p.inventory[12].stack==old,"All does not sweep old fish before a real automatic product");
                if(type==ItemID.Bass){pendingSettings=(FishingSettings)Get(host,"Settings");pauseProduct=true;}
                bobber.ai[1]=-120;bobber.localAI[1]=type;bobber.localAI[2]=ItemID.Worm;
                for(int i=0;i<280;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                int stored=chest.item.Where(x=>x.type==type).Sum(x=>x.stack);
                Require(stored==old+2 && p.inventory.Where(x=>x.type==type && !x.favorited).Sum(x=>x.stack)==0 && p.inventory[13].type==type && p.inventory[13].favorited,"fish-only real Give/GetItem captures old whole stacks and native nearby chest conserves protected/nonstackable items: type="+type+" stored="+stored+" source="+GetOptional(items,"SourceMessage"));
                Require(!(bool)Get(Get(items,"Feature"),"OrdinaryEnabled"),"only dedicated fish storage was enabled");
                if(type==ItemID.Bass)Require(observedBusyProduct,"real Give source survives a pending unrelated settings save");
                // Returning a known item from a chest is a new manual intention,
                // not another catch. The old finite permission is already retired.
                p.inventory[12].SetDefaults(type);p.inventory[12].stack=1;chest.item.First(x=>x.type==type).stack--;
                Call(Get(items,"World"),"InvalidateObservation");for(int i=0;i<24;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                Require(p.inventory[12].type==type && p.inventory[12].stack==1,"later same-type inventory cannot reuse the retired catch opportunity");
            }
            {
                var p=Reset(context,host,tools,input);Main.anglerQuest=0;Main.anglerQuestFinished=false;int type=Main.anglerQuestItemNetIDs[0];p.inventory[12].SetDefaults(type);p.inventory[12].stack=3;var chest=Chest(type);
                NativeFishingChecks.Save(host,new FishingOptions(storeMode:2));Cast(context,input);
                Require(p.inventory[12].IsAir && chest.item.Where(x=>x.type==type).Sum(x=>x.stack)==4,"Quest-only active manual fishing session stores current old quest fish without any automatic pull");
                Main.anglerQuestFinished=true;p.inventory[12].SetDefaults(type);p.inventory[12].stack=2;
                for(int i=0;i<24;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                Require(p.inventory[12].stack==2,"today-completed quest revokes current inventory eligibility");Main.anglerQuestFinished=false;
                var state=Get(Get(context,"Shell"),"State");Call(state,"RestoreVisible");
                for(int i=0;i<24;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                Require(p.inventory[12].IsAir && chest.item.Where(x=>x.type==type).Sum(x=>x.stack)==6 && (bool)Get(Get(host,"Session"),"Active"),"F5 allows actual fish storage while retaining the fishing session");Call(state,"Close");
                p.inventory[12].SetDefaults(type);p.inventory[12].stack=2;
                for(int y=0;y<2;y++)for(int x=0;x<2;x++){var tile=Main.tile[39+x,40+y];tile.active(true);tile.type=21;tile.frameX=(short)(x*18);tile.frameY=(short)(y*18);}
                Main.chest[1]=null;Terraria.Chest.CreateWorldChest(1,39,40);Call(p,"OpenChest",39,40,1);
                for(int i=0;i<24;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                Require(p.chest==1,"the native fixture keeps the unrelated chest open at its actual coordinates");
                Require(p.inventory[12].IsAir && chest.item.Where(x=>x.type==type).Sum(x=>x.stack)==8,"an unrelated open chest does not prohibit current quest storage into the other valid nearby target");p.chest=-1;Main.chest[1]=null;Main.playerInventory=false;
            }
            {
                var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.Bass);p.inventory[12].stack=12;var chest=Chest(ItemID.Bass);
                NativeFishingChecks.Save(host,new FishingOptions(storeMode:1));var b=Cast(context,input);b.ai[1]=-120;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;
                NativeFishingChecks.Step(context,input,new Vector2(850,718),true,0);for(int i=0;i<150;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                Require(chest.item.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==1 && p.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==13,"real manual pull product never creates dedicated All permission");
            }
            foreach(bool overflow in new[]{true,false})
            {
                var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.Bass);p.inventory[12].stack=overflow?9999:12;var chest=Chest(ItemID.Bass);
                if(overflow)for(int i=1;i<50;i++){if(i==12)continue;p.inventory[i].SetDefaults(ItemID.StoneBlock);p.inventory[i].stack=9999;}
                else{chest.item[0].stack=9998;for(int i=1;i<40;i++){chest.item[i].SetDefaults(ItemID.StoneBlock);chest.item[i].stack=9999;}}
                NativeFishingChecks.Save(host,new FishingOptions(auto:true,storeMode:1));var b=Cast(context,input);int originalKey=(int)b.key;checkOverflow=overflow;overflowObserved=false;productCount=0;b.ai[1]=-120;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;
                bool replacementObserved=false;for(int i=0;i<280;i++)
                {
                    NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                    int count=chest.item.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack);var session=Get(host,"Session");
                    // Native projectile pooling reuses this very object for
                    // the replacement. Completion belongs to the old full key.
                    if(productCount==1 && count==(overflow?10000:9999) && (!b.active || (int)b.key!=originalKey) && Get(session,"Phase").ToString()=="Waiting" && (bool)Get(session,"InLiquid")){replacementObserved=true;break;}
                }
                Require(replacementObserved,"capacity observation reached new wet bobber; originalKey="+originalKey+" currentKey="+(int)b.key+" active="+b.active+" products="+productCount);
                checkOverflow=false;Require(productCount==1,"capacity fixture observes exactly its first actual native product; overflow="+overflow+" products="+productCount+" originalKey="+originalKey+" currentKey="+(int)b.key+" active="+b.active+" phase="+Get(Get(host,"Session"),"Phase"));
                int stored=chest.item.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack);
                if(overflow)Require(overflowObserved && stored==10000 && p.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)+Main.item.Where(x=>x.active && x.type==ItemID.Bass).Sum(x=>x.stack)==1,"real entirely overflowing catch qualifies existing full stack once, without assigning future ground pickup to the catch");
                else
                {
                    Require(stored==9999 && p.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==12,"native partial chest capacity conserves the complete remainder");
                    // Keep the same active storage session, but stop further
                    // automatic catches. A new natural Bass otherwise grants a
                    // valid new permission and cannot test old-permission replay.
                    var session=Get(host,"Session");long token=(long)Get(session,"Token");
                    NativeFishingChecks.Save(host,((FishingSettings)Get(host,"Settings")).Value.Change(0,0));
                    chest.item[1].TurnToAir();Call(Get(items,"World"),"InvalidateObservation");
                    for(int i=0;i<60;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                    Require((bool)Get(session,"Active") && (long)Get(session,"Token")==token && (bool)Get(Get(items,"Feature"),"FishingStorageEnabled"),"replay check keeps the same session and dedicated storage consumer active");
                    Require(productCount==1 && p.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==12,"new chest capacity does not replay a consumed partial catch opportunity");
                    NativeFishingChecks.Save(host,((FishingSettings)Get(host,"Settings")).Value.Change(0,1));
                    var next=Main.projectile.First(x=>x.active && x.bobber && x.owner==p.whoAmI);next.ai[1]=-120;next.localAI[1]=ItemID.Bass;next.localAI[2]=ItemID.Worm;
                    for(int i=0;i<200 && chest.item.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)!=10012;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                    Require(productCount==2 && chest.item.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==10012 && p.inventory.Where(x=>x.type==ItemID.Bass).Sum(x=>x.stack)==0,"a genuinely new native catch grants a new finite opportunity for remaining old stock");
                }
            }
            Background(context,host,tools,input,items);
            NativeFishingNetworkChecks.Storage(context,()=>Reset(context,host,tools,input),Chest,()=>Cast(context,input));
            NativeFishingChecks.Save(host,new FishingOptions());Main.chest[0]=null;
            Console.WriteLine("PASS G10 actual auto Give/GetItem -> same-type old inventory -> native nearby chest; Bass/ReaverShark/favorite protection, finite member retirement, Quest-only old stock and manual-pull exclusion.");
        }
        private static void Background(object context,object host,object tools,object input,object items)
        {
            object foreground=Get(input,"foregroundWindow");bool updates=Main.CanUpdateGameplay;
            try
            {
                foreach(int mode in new[]{1,2})foreach(bool staleMouse in new[]{false,true})
                {
                    Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;Main.LocalPlayer.mouseInterface=false;Main.ToggleGameplayUpdates(true);
                    var p=Reset(context,host,tools,input);Main.anglerQuest=0;Main.anglerQuestFinished=true;
                    int type=mode==1?ItemID.Bass:Main.anglerQuestItemNetIDs[0];var chest=Chest(type);
                    NativeFishingChecks.Save(host,new FishingOptions(auto:mode==1,storeMode:mode));var b=Cast(context,input);
                    p.inventory[12].SetDefaults(type);p.inventory[12].stack=3;Main.anglerQuestFinished=false;Call(Get(items,"World"),"InvalidateObservation");
                    Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));FocusHelper.IsSelectedApplication=false;Main.ToggleGameplayUpdates(true);p.mouseInterface=staleMouse;
                    int productsBefore=productCount;
                    if(mode==1){b.ai[1]=-120;b.localAI[1]=type;b.localAI[2]=ItemID.Worm;}
                    // Observe completion of this actual storage opportunity.
                    // A blind 280-step wait can naturally catch a second Bass
                    // after automatic recast (vanilla's wait counter is random,
                    // not a minimum tick duration), making exact totals flaky.
                    for(int i=0;i<280 && (p.inventory.Any(x=>x.type==type) || mode==1 && productCount==productsBefore);i++)
                        NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                    Require(productCount-productsBefore==(mode==1?1:0),"background fixture observes exactly the intended original product");
                    Require(!(bool)Get(input,"CanStartActions") && !(bool)Get(input,"CanRetainIntent"),"background storage does not grant physical input");
                    Require(chest.item.Where(x=>x.type==type).Sum(x=>x.stack)==(mode==1?5:4) && p.inventory.Where(x=>x.type==type).Sum(x=>x.stack)==0,
                        "background native fish storage completes both modes through actual shared item outlet; mode="+mode+" staleMouse="+staleMouse+" chest="+chest.item.Where(x=>x.type==type).Sum(x=>x.stack)+" inventory="+p.inventory.Where(x=>x.type==type).Sum(x=>x.stack)+" session="+Get(Get(host,"Session"),"Active"));
                }
            }
            finally{Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;Main.ToggleGameplayUpdates(updates);Main.LocalPlayer.mouseInterface=false;}
            Console.WriteLine("PASS G10 background actual fish storage, both modes and stale Draw mouse state; input quarantine retained.");
        }
        private static Player Reset(object context,object host,object tools,object input)
        {
            NativeFishingChecks.Save(host,new FishingOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
            foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
            return p;
        }
        private static Projectile Cast(object context,object input)
        {
            if(((FishingSettings)Get(Get(context,"Fishing"),"Settings")).Value.Auto)return NativeFishingChecks.CastToWaiting(context,input);
            for(int i=0;i<150;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),i==0,0);
            var b=Main.projectile.FirstOrDefault(x=>x.active && x.bobber && x.owner==Main.myPlayer);
            Require(b!=null && b.wet && (bool)Get(Get(Get(context,"Fishing"),"Session"),"Active"),"real manually cast wet bobber admits storage session");return b;
        }
        private static Terraria.Chest Chest(int type)
        {
            for(int y=0;y<2;y++)for(int x=0;x<2;x++){var t=Main.tile[42+x,40+y];t.active(true);t.type=21;t.frameX=(short)(x*18);t.frameY=(short)(y*18);}
            Main.chest[0]=null;var chest=Terraria.Chest.CreateWorldChest(0,42,40);chest.item[0].SetDefaults(type);chest.item[0].stack=1;return chest;
        }
    }
}
