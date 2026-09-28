using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using JueMingR.Platform.Information;
using JueMingR.Features.Fishing;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static int pulls,products,casts;
        private static Vector2 lastVelocity,firstNativeAim;
        private static ProbeGraphics textures;
        private static readonly System.Collections.Generic.List<string> prompts=new System.Collections.Generic.List<string>();
        private static void Shown(string __0,string __1){if(__0=="fishing.session")prompts.Add(__1);}
        private sealed class UnexpectedFishingCondition : Terraria.GameContent.FishDropRules.AFishingCondition
        {internal static int Calls;public override bool Matches(Terraria.GameContent.FishDropRules.FishingContext context){Calls++;Main.rand.Next();return true;}}
        private static bool SkipAchievement(){return false;}
        private static void Pulled(){pulls++;}
        private static void Given(){products++;}
        private static void Created(Projectile __0){if(__0.bobber){if(casts==0)firstNativeAim=Main.MouseWorld;casts++;lastVelocity=__0.velocity;}}
        internal static void Run(object context,ProbeGraphics graphics=null)
        {
            textures=graphics;
            if(graphics!=null)
            {
                graphics.LoadItemTextures(new[]{0,ItemID.BugNet,ItemID.WoodFishingPole});graphics.LoadTexture("Npc","Images/NPC_46",46);
                new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(typeof(Item).GetMethod("GetDrawHitbox"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            }
            var property=context.GetType().GetProperty("Fishing",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            Require(property!=null && property.GetValue(context)!=null,"complete Host composition must own the fishing participant");
            object host=property.GetValue(context);
            Require((bool)Get(host,"Available"),"fixed native fishing hooks must install");
            Require(GetOptional(host,"Equipment")!=null,"fishing composition owns both equipment strategies and their return responsibility");
            Console.WriteLine("PASS G10 actual Host composition and native hook admission.");
            Require(GetOptional(Get(context,"Shell"),"FishingUi")!=null,"actual F5 composition must attach fishing controls and list workflows");
            FullLoop(context,host,graphics!=null);
            NativeFishingUiChecks.Run(context);
            NativePlayerRenameChecks.Run(context);
            var observation=Get(host,"Observation");var catalog=Get(host,"Catalog");var equipment=Get(host,"Equipment");
            long scans=(long)Get(observation,"Scans"),tiles=(long)Get(catalog,"TileReads"),sources=(long)Get(equipment,"SourceReads"),shapes=(long)Get(catalog,"ShapeReads"),versions=(long)Get(catalog,"VersionReads");
            for(int i=0;i<120;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)i);}
            Require((long)Get(observation,"Scans")==scans && (long)Get(catalog,"TileReads")==tiles && (long)Get(equipment,"SourceReads")==sources && (long)Get(catalog,"ShapeReads")==shapes && (long)Get(catalog,"VersionReads")==versions,"all G10 consumers off with no return tail performs no projectile/water/source/rule reads");
            textures=null;
        }
        private static void FullLoop(object context,object host,bool nativeNet)
        {
            object tools=Get(context,"Tools"),input=Get(context,"Input");
            var settings=(FishingSettings)Get(host,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Loaded;});
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            Main.FishDropsDB=new Terraria.GameContent.FishDropRules.FishDropRuleList();
            new Terraria.GameContent.FishDropRules.GameContentFishDropPopulator(Main.FishDropsDB).Populate();
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            var chum=typeof(Main).GetField("ChumBucketProjectileHelper",Flags);chum.SetValue(Main.instance,Activator.CreateInstance(chum.FieldType));
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            var audit=new Harmony("JueMingR.Tests.G10NativeLoop");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeFishingChecks),nameof(SkipAchievement)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_PullFishingBobbers",Flags),postfix:new HarmonyMethod(typeof(NativeFishingChecks),nameof(Pulled)));
            audit.Patch(typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer",Flags),postfix:new HarmonyMethod(typeof(NativeFishingChecks),nameof(Given)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeFishingChecks),nameof(Created)));
            audit.Patch(Get(context,"ShortFeedback").GetType().GetMethod("Show",Flags),postfix:new HarmonyMethod(typeof(NativeFishingChecks),nameof(Shown)));
            try
            {
                foreach(bool inventory in new[]{false,true})foreach(int empty in new[]{0,1,3})
                {
                    Save(host,new FishingOptions());
                    int rodSlot=inventory?3:0;var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,rodSlot,rodSlot);
                    if(inventory){for(int i=0;i<10;i++)if(i!=rodSlot)p.inventory[i].SetDefaults(ItemID.WoodFishingPole);}
                    else if(empty!=0){p.inventory[1].SetDefaults(ItemID.StoneBlock);p.inventory[1].stack=17;}
                    foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
                    for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
                    Save(host,new FishingOptions(auto:true));pulls=products=casts=0;prompts.Clear();var point=new Vector2(850,718);
                    for(int frame=0;frame<150;frame++)Step(context,input,point,frame==0,empty);
                    var b=Main.projectile.FirstOrDefault(q=>q.active && q.bobber && q.owner==p.whoAmI);
                    Require(b!=null && b.wet && (bool)Get(Get(host,"Session"),"Active"),"manual real native cast enters liquid before auto session; empty="+empty+" casts="+casts);
                    Require(prompts.SequenceEqual(new[]{"开始钓鱼"}),"one actual wet-session start emits its player-head prompt");
                    if(!inventory && empty==0)Catalog(host,p,b);
                    Require(settings.Set(settings.Value.Change(5,2)),"active session filter edit accepted");
                    Call(host,"Update",(ulong)0);
                    Require((bool)Get(Get(host,"Session"),"Active"),"pending unrelated settings commit pauses admission without deleting fishing session");
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"active filter edit reliable");
                    int oldKey=(int)b.key;int oldCasts=casts,before=p.inventory.Where(i=>i.type==ItemID.Bass).Sum(i=>i.stack);
                    Main.playerInventory=inventory;
                    b.ai[1]=-120;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;int startPull=pulls;
                    for(int frame=0;frame<240 && casts==oldCasts;frame++)
                    {
                        Step(context,input,new Vector2(300,640),false,empty);
                    }
                    Require(pulls==startPull+2 && products==1,"one real pull followed by one real cast check, one real item product; empty="+empty+" pulls="+(pulls-startPull)+" products="+products);
                    Require(p.inventory.Where(i=>i.type==ItemID.Bass).Sum(i=>i.stack)==before+1,"actual Bass enters inventory once through original product path");
                    Require(casts==oldCasts+1 && lastVelocity.X>0 && !Main.projectile.Any(q=>q.active && q.bobber && (int)q.key==oldKey),"single new bobber uses original rightward world aim despite current mouse left");
                    for(int frame=0;frame<70;frame++)Step(context,input,point,false,empty);
                    Require(Get(Get(host,"Session"),"Phase").ToString()=="Waiting","new cast receipt retires and session waits again");
                    if(nativeNet && !inventory)Borrow(context,host,tools,input,p,firstNativeAim,empty);
                    {
                        if(empty==0 && !inventory)
                        {
                        var questBobber=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);int savedQuest=Main.anglerQuest;bool savedFinished=Main.anglerQuestFinished;
                        p.AddBuff(122,2000);Save(host,new FishingOptions(auto:true,filterMode:1,crates:0,quests:1,npcs:0));
                        Main.anglerQuest=0;Main.anglerQuestFinished=false;questBobber.ai[1]=-240;questBobber.localAI[1]=Main.anglerQuestItemNetIDs[0];questBobber.localAI[2]=ItemID.Worm;
                        Call(Get(host,"Observation"),"Invalidate");Call(Get(host,"Observation"),"Read",p);
                        Require(Call(Get(host,"Session"),"Choose",p)!=null,"unfinished current quest gets its explicit keep exception");
                        Main.anglerQuest=1;Require(Call(Get(host,"Session"),"Choose",p)==null,"yesterday's bitten quest item falls back to active ordinary whitelist");
                        Main.anglerQuest=0;Main.anglerQuestFinished=true;Require(Call(Get(host,"Session"),"Choose",p)==null,"completed current quest no longer receives the actual-bite exception");
                        Main.anglerQuest=savedQuest;Main.anglerQuestFinished=savedFinished;
                        }
                        Save(host,new FishingOptions(auto:true,filterMode:1,crates:0,quests:0,npcs:0));
                        p.AddBuff(122,2000);var rejected=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);
                        rejected.ai[1]=-240;rejected.localAI[1]=ItemID.Bass;rejected.localAI[2]=ItemID.Worm;
                        int rejectedKey=(int)rejected.key,priorPulls=pulls,priorProducts=products,priorBait=p.inventory[54].stack;
                        for(int frame=0;frame<180;frame++)Step(context,input,point,false,empty);
                        Require(pulls==priorPulls && products==priorProducts && rejected.active && p.inventory[54].stack==priorBait,"sonar refusal waits beyond old forced-pull timeout without consuming bait");
                        rejected.ai[1]=-1;for(int frame=0;frame<3;frame++)Step(context,input,point,false,empty);
                        Save(host,settings.Value.Change(4,1));rejected.ai[1]=-240;rejected.localAI[1]=ItemID.Bass;rejected.localAI[2]=ItemID.Worm;
                        int priorCasts=casts;long cutSession=(long)Get(Get(host,"Session"),"Token");
                        for(int frame=0;frame<150 && casts==priorCasts;frame++)Step(context,input,point,false,empty);
                        Require(casts==priorCasts+1 && products==priorProducts && p.inventory[54].stack==priorBait && !Main.projectile.Any(q=>q.active && q.bobber && (int)q.key==rejectedKey),"cut selects an empty slot without use, native AI removes old bobber, one recast and no rejected product");
                        for(int frame=0;frame<70;frame++)Step(context,input,point,false,empty);
                        Require(p.selectedItem==rodSlot && (bool)Get(Get(host,"Session"),"Active") && (long)Get(Get(host,"Session"),"Token")==cutSession,"cut returns rod without treating owned selection as manual exit; empty="+empty+" inventory="+inventory);
                        if(!inventory && empty!=0)Require(p.inventory[1].type==ItemID.StoneBlock && p.inventory[1].stack==17,"nonempty temporary selection is never used or consumed: type="+p.inventory[1].type+" stack="+p.inventory[1].stack);
                        Console.WriteLine("PASS G10 native sonar refusal/natural wait and exact rod return after selection-only cut, inventory="+inventory+" extraUpdates="+empty+".");
                    }
                    Step(context,input,point,true,empty);
                    Require(!(bool)Get(Get(host,"Session"),"Active"),"real manual pull immediately takes back the fishing session");
                    Require(prompts.SequenceEqual(new[]{"开始钓鱼","停止钓鱼"}),"native pull/recast/cut/borrow preserve one prompt pair until genuine manual exit");
                }
                Console.WriteLine("PASS G10 native manual cast, liquid admission, actual pull/item/recast with 0/1/3 unsampled outer updates.");
                Background(context,host,tools,input);
                // The F5 seam case adds native simulation steps. Give it its
                // own RNG so unrelated later bite timing does not change.
                var priorRandom=Main.rand;
                try{Main.rand=new Terraria.Utilities.UnifiedRandom(107);F5CutRefresh(context,host,tools,input);}
                finally{Main.rand=priorRandom;}
                NativeFishingOutcomeChecks.Run(context);
                Truffle(context,host,tools,input);
                NativeFishingStorageChecks.Run(context);
                NativeFishingEquipmentChecks.Run(context);
            }
            finally{Save(host,new FishingOptions());foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        internal static void Save(object host,FishingOptions value)
        {var settings=(FishingSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.Set(value),"fishing save admitted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"fishing save completed");}
        private static void F5CutRefresh(object context,object host,object tools,object input)
        {
            Save(host,new FishingOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
            p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
            Save(host,new FishingOptions(auto:true,cut:true,filterMode:1,crates:0,quests:0,npcs:0));var point=new Vector2(850,718);
            for(int i=0;i<150;i++)Step(context,input,point,i==0,0);
            p.AddBuff(122,4000);var bobber=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);
            bobber.ai[1]=-240;bobber.localAI[1]=ItemID.Bass;bobber.localAI[2]=ItemID.Worm;
            NativeToolExecutionChecks.Sample(context,input,point,false);NativeQuickItemChecks.BeginWorldStep();Call(Get(host,"Observation"),"Invalidate");Call(Get(host,"Observation"),"Read",p);
            var pick=typeof(Player).GetMethod("PickItemSelectionOverride",Flags);object[] choice={0};
            Require((bool)pick.Invoke(p,choice) && (int)choice[0]!=0,"native selection callback acquires a real pending cut");
            object use=Get(tools,"Use"),intent=Get(use,"Intent"),shell=Get(context,"Shell"),state=Get(shell,"State");
            Require(Get(intent,"Kind").ToString()=="FishingCut" && ((Func<bool>)Get(intent,"Refresh"))(),"live cut refresh would otherwise reapply the selection");
            int priorPulls=pulls,priorCasts=casts,priorProducts=products,bait=p.inventory[54].stack;
            try
            {
                // The real selection callback is the seam before vanilla applies
                // the chosen slot and advances bobber AI. No private use state is set.
                NativeF5AutomationChecks.Open(context);NativeQuickItemChecks.BeginWorldStep();choice[0]=0;
                Require(!(bool)pick.Invoke(p,choice),"F5 blocks an already admitted cut at its native selection refresh");
                Require(p.selectedItem==0 && bobber.active && pulls==priorPulls && casts==priorCasts && products==priorProducts && p.inventory[54].stack==bait,"opening F5 cannot turn pending cut into a native use or consume bait");
                Require((bool)Get(Get(host,"Session"),"Active"),"F5 pause preserves the established fishing session");
            }
            finally{Call(state,"Close");Call(shell,"EndPointerLayer");p.mouseInterface=false;Save(host,new FishingOptions());}
            Console.WriteLine("PASS G10 actual pending cut selection refresh is paused by F5 without native consumption.");
        }
        private static void Background(object context,object host,object tools,object input)
        {
            var foreground=Get(input,"foregroundWindow");bool updates=Main.CanUpdateGameplay;
            try
            {
                foreach(bool staleMouse in new[]{false,true})
                {
                    Save(host,new FishingOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
                    p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
                    for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
                    Save(host,new FishingOptions(auto:true,filterMode:2));var point=new Vector2(850,718);
                    for(int i=0;i<150;i++)Step(context,input,point,i==0,1);
                    long token=(long)Get(Get(host,"Session"),"Token");p.AddBuff(122,4000);
                    Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));Terraria.FocusHelper.IsSelectedApplication=false;Main.ToggleGameplayUpdates(true);p.mouseInterface=staleMouse;
                    var b=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);b.ai[1]=-240;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;
                    int priorCasts=casts,priorProducts=products;
                    for(int i=0;i<240 && casts==priorCasts;i++)Step(context,input,point,false,1);
                    Require(!(bool)Get(input,"CanStartActions") && !(bool)Get(input,"CanRetainIntent"),"background fishing never grants physical input permission");
                    Require(products==priorProducts+1 && casts==priorCasts+1,"background accepted bite completes native product and one recast, staleMouse="+staleMouse);
                    for(int i=0;i<70;i++)Step(context,input,point,false,1);
                    Save(host,new FishingOptions(auto:true,filterMode:1,crates:0,quests:0,npcs:0));
                    b=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);b.ai[1]=-240;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;int key=(int)b.key;priorCasts=casts;priorProducts=products;
                    for(int i=0;i<30;i++)Step(context,input,point,false,1);
                    Require(products==priorProducts && casts==priorCasts && b.active,"background rejected bite waits naturally with cut off");
                    Save(host,new FishingOptions(auto:true,cut:true,filterMode:1,crates:0,quests:0,npcs:0));
                    Call(Get(host,"Observation"),"Invalidate");Call(Get(host,"Observation"),"Read",p);
                    Action<Action,Action,string> blocked=(enter,leave,label)=>{enter();try{Require(Call(Get(host,"Session"),"Choose",p)==null,"background fishing refuses "+label);}finally{leave();}};
                    blocked(()=>Main.gamePaused=true,()=>Main.gamePaused=false,"pause");
                    blocked(()=>Main.ToggleGameplayUpdates(false),()=>Main.ToggleGameplayUpdates(true),"stopped simulation");
                    blocked(()=>Main.drawingPlayerChat=true,()=>Main.drawingPlayerChat=false,"chat");
                    blocked(()=>Main.mouseItem.SetDefaults(ItemID.DirtBlock),()=>Main.mouseItem.TurnToAir(),"held mouse item");
                    blocked(()=>p.chest=0,()=>p.chest=-1,"open chest");
                    blocked(()=>Call(Get(Get(context,"Shell"),"State"),"RestoreVisible"),()=>Call(Get(Get(context,"Shell"),"State"),"Close"),"F5 tool pause");
                    for(int i=0;i<240 && casts==priorCasts;i++)Step(context,input,point,false,1);
                    Require(products==priorProducts && casts==priorCasts+1 && !Main.projectile.Any(q=>q.active && q.bobber && (int)q.key==key),"background rejected bite cuts and recasts without a product");
                    for(int i=0;i<70;i++)Step(context,input,point,false,1);
                    Require(p.selectedItem==0 && (long)Get(Get(host,"Session"),"Token")==token && (bool)Get(Get(host,"Session"),"Active"),"background cut retains its original fishing session and rod");
                    Set(input,"foregroundWindow",foreground);Terraria.FocusHelper.IsSelectedApplication=true;p.mouseInterface=false;
                    Step(context,input,point,false,1);Step(context,input,point,false,1);
                    p.selectedItemState.Select(1);Step(context,input,point,false,1);Require(!(bool)Get(Get(host,"Session"),"Active"),"real manual selection still ends background-owned session after focus returns");
                }
                Console.WriteLine("PASS G10 background native product/recast, rejection/cut/rod return, unchanged input quarantine and real manual takeover.");
            }
            finally{Set(input,"foregroundWindow",foreground);Terraria.FocusHelper.IsSelectedApplication=true;Main.ToggleGameplayUpdates(updates);Main.LocalPlayer.mouseInterface=false;Save(host,new FishingOptions());}
        }
        private static void Truffle(object context,object host,object tools,object input)
        {
            foreach(bool damage in new[]{false,true})
            {
                Save(host,new FishingOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
                foreach(var item in p.armor)item.TurnToAir();p.inventory[12].SetDefaults(5591);p.inventory[54].SetDefaults(ItemID.TruffleWorm);p.inventory[54].stack=10;
                for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
                Save(host,new FishingOptions(auto:true,equipment:true,cut:true,filterMode:1,crates:2,quests:2,npcs:2));prompts.Clear();
                var point=new Vector2(850,718);for(int i=0;i<150;i++)Step(context,input,point,i==0,1);
                Require(prompts.SequenceEqual(new[]{"开始鲨猪"}) && p.armor[0].IsAir,"actual Truffle Worm session prompts once and leaves equipment alone");
                if(damage){p.statLife-=20;Step(context,input,point,false,1);}
                else
                {
                    var b=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);int previousCasts=casts,previousProducts=products;
                    p.AddBuff(122,2000);b.ai[1]=-120;b.localAI[1]=1;b.localAI[2]=ItemID.TruffleWorm;
                    for(int i=0;i<240 && casts==previousCasts;i++)Step(context,input,point,false,1);
                    Require(NPC.AnyNPCs(NPCID.DukeFishron) && p.inventory[54].stack==9 && products==previousProducts && casts==previousCasts+1,
                        "original truffle pull spawns the real isolated NPC and consumes one bait despite empty whitelist/reject specials; no Item1 product or cut");
                    Require(prompts.SequenceEqual(new[]{"开始鲨猪"}),"automatic truffle recast is not a second session or a boss-success prompt");
                    for(int i=0;i<70;i++)Step(context,input,point,false,1);
                    p.selectedItemState.Select(1);Step(context,input,point,false,1);
                }
                Require(prompts.SequenceEqual(new[]{"开始鲨猪",damage?"停止钓鱼":"鲨猪啦！"}),"Truffle session ending distinguishes damage from real selection exit: damage="+damage+" prompts="+string.Join("/",prompts));
                Call(Get(host,"Session"),"Stop");Require(prompts.Count==2,"duplicate stop has no duplicate prompt");Save(host,new FishingOptions());
            }
            Console.WriteLine("PASS G10 real truffle session, original Duke Fishron spawn/consumption, equipment exclusion, auto recast continuity and exact start/end feedback.");
        }
        private static void Borrow(object context,object host,object tools,object input,Player p,Vector2 originalAim,int empty)
        {
            object borrow=Get(tools,"Fishing"),session=Get(host,"Session"),use=Get(tools,"Use");long token=(long)Get(session,"Token"),beforeBorrow=(long)Get(borrow,"Token");
            int beforeCasts=casts,oldKey=(int)Main.projectile.First(x=>x.active && x.bobber && x.owner==p.whoAmI).key;
            p.inventory[12].SetDefaults(ItemID.BugNet);var npc=Main.npc[0];npc.SetDefaults(NPCID.Bunny);npc.whoAmI=0;npc.active=true;npc.position=p.position+new Vector2(37,2);npc.velocity=Vector2.Zero;
            Call(Get(tools,"Npcs"),"BeginTick");NativeToolsChecks.SetMode(tools,0,1);bool active=false;
            for(int i=0;i<290;i++)
            {
                Step(context,input,new Vector2(300,640),false,empty);bool borrowed=(bool)Get(borrow,"Active");active|=borrowed;
                if(borrowed)
                {
                    Require((bool)Get(session,"Active") && (long)Get(session,"Token")==token,"G10 retains its same fishing identity throughout G09's actual net loan");
                    var intent=GetOptional(use,"Intent");Require(intent==null || Get(intent,"Kind").ToString()!="FishingCast","G10 never acquires a second recast while G09 owns the loan");
                }
                if(active && !(bool)Get(borrow,"Active"))break;
            }
            Require(active && !npc.active && (long)Get(borrow,"Token")==beforeBorrow+1 && Get(borrow,"Phase").ToString()=="Completed" && (bool)Get(borrow,"RecastObserved"),"real BugNet catches the original bunny and completes its single rod compensation: "+Get(borrow,"Phase"));
            Require(casts==beforeCasts+1 && lastVelocity.X>0 && ((Vector2)Get(borrow,"OriginalTarget")).Equals(originalAim) && !Main.projectile.Any(x=>x.active && x.bobber && (int)x.key==oldKey),"borrow after a G10 automatic cast reuses the original world aim exactly once; casts="+(casts-beforeCasts)+" velocity="+lastVelocity+" target="+Get(borrow,"OriginalTarget")+" requested="+originalAim+" session="+Get(session,"Target"));
            for(int i=0;i<70;i++)Step(context,input,originalAim,false,empty);
            var bobber=Main.projectile.First(x=>x.active && x.bobber && x.owner==p.whoAmI);int beforeProducts=products;beforeCasts=casts;
            bobber.ai[1]=-120;bobber.localAI[1]=ItemID.Bass;bobber.localAI[2]=ItemID.Worm;
            for(int i=0;i<240 && casts==beforeCasts;i++)Step(context,input,new Vector2(300,640),false,empty);
            Require(products==beforeProducts+1 && casts==beforeCasts+1 && (long)Get(session,"Token")==token,"G10 resumes actual native product and recast after borrowed bobber handoff");
            for(int i=0;i<70;i++)Step(context,input,originalAim,false,empty);NativeToolsChecks.SetMode(tools,0,0);
            Console.WriteLine("PASS G10 actual auto recast -> real net/bunny/G09 compensation -> same-session product/recast, unsampled outer="+empty);
        }
        private static void Catalog(object host,Player p,Projectile bobber)
        {
            var field=host.GetType().GetField("Catalog",Flags);Require(field!=null,"Host must own the shared read-only fishing catalog");
            object catalog=field.GetValue(host);
            long reads=(long)Get(catalog,"TileReads"),builds=(long)Get(catalog,"CatalogBuilds");
            Require(((Array)Call(catalog,"Search"," ")).Length==0 && (long)Get(catalog,"TileReads")==reads && (long)Get(catalog,"CatalogBuilds")==builds,"blank plus query performs no directory or water expansion");
            var savedRandom=Main.rand;Main.rand=new Terraria.Utilities.UnifiedRandom(341927);
            var expected=new Terraria.Utilities.UnifiedRandom(341927);int bait=p.inventory[54].stack;string nativeText=p.displayedFishingInfo;
            try
            {
                object result=Call(catalog,"Current",p,bobber,true);
                if(!(bool)Get(result,"Known")){Call(catalog,"Build",p,bobber);throw new InvalidOperationException("real pond candidates: "+Get(result,"Message"));}
                var keys=(FishKey[])Get(result,"Keys");Require(keys.Contains(new FishKey(FishKind.Item,ItemID.Bass)),"actual ordinary pond rules include Bass");
                var display=Get(host,"Display");var information=Get(display,"information");
                NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return (bool)Get(information,"CanConfigure");});
                Require((bool)Call(information,"SetEnabled",InformationKind.FullFish,true),"full fish display setting admitted");
                NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return Get(Get(information,"Preferences"),"Status").ToString()=="Saved";});
                Call(Get(host,"Observation"),"Invalidate");Call(Get(host,"Observation"),"Read",p);Call(display,"Update",p);long displayBuilds=(long)Get(display,"Builds");
                var localized=Lang.GetItemName(ItemID.Bass);string oldName=localized.Value;var culture=Language.ActiveCulture;
                var languageEvent=(Delegate)Get(LanguageManager.Instance,"OnLanguageChanged");
                try
                {
                    Call(localized,"SetValue","资源重载鲈鱼");languageEvent.DynamicInvoke(LanguageManager.Instance);Call(display,"Update",p);
                    Require(ReferenceEquals(culture,Language.ActiveCulture) && (long)Get(display,"Builds")==displayBuilds+1 && ((string)Get(display,"Full")).Contains("资源重载鲈鱼"),"same-culture native language-resource event invalidates prepared fish names");
                }
                finally{Call(localized,"SetValue",oldName);languageEvent.DynamicInvoke(LanguageManager.Instance);Call(information,"SetEnabled",InformationKind.FullFish,false);NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return Get(Get(information,"Preferences"),"Status").ToString()=="Saved";});Call(display,"Clear");}
                Require(Main.rand.Next()==expected.Next() && p.inventory[54].stack==bait && p.displayedFishingInfo==nativeText,"preview never consumes native RNG, bait, or displayed native fishing state");
                reads=(long)Get(catalog,"TileReads");Call(catalog,"Current",p,bobber,false);Require((long)Get(catalog,"TileReads")==reads,"same simulation revision reuses actual pond observation");
                long checks=(long)Get(catalog,"RuleChecks"),shapes=(long)Get(catalog,"ShapeReads"),versions=(long)Get(catalog,"VersionReads");for(int i=0;i<31;i++)NativeQuickItemChecks.BeginWorldStep();
                Require(ReferenceEquals(result,Call(catalog,"Current",p,bobber,false)) && (long)Get(catalog,"TileReads")>reads && (long)Get(catalog,"RuleChecks")==checks,"periodic water read keeps stable normalized candidates without rule reevaluation");
                Require((long)Get(catalog,"ShapeReads")>shapes && (long)Get(catalog,"VersionReads")>versions,"stable predicate cache still truthfully counts periodic native shape and version validation");
                var found=(Array)Call(catalog,"Search","#2290");Require(found.Length>0,"global fishing search contains native Bass ID");
                int baitType=p.inventory[54].type,baitStack=p.inventory[54].stack;
                p.inventory[54].SetDefaults(ItemID.TruffleWorm);p.inventory[54].stack=baitStack;
                result=Call(catalog,"Current",p,bobber,true);Require((bool)Get(result,"Known") && ((FishKey[])Get(result,"Keys")).Length==0,"Truffle Worm has no ordinary display candidates");
                p.inventory[54].SetDefaults(baitType);p.inventory[54].stack=baitStack;
                result=Call(catalog,"Current",p,bobber,true);Require(((FishKey[])Get(result,"Keys")).Contains(new FishKey(FishKind.Item,ItemID.Bass)),"return from Truffle Worm restores ordinary candidate cache");
                float a=bobber.ai[1],l1=bobber.localAI[1],l2=bobber.localAI[2];bobber.ai[1]=-1;bobber.localAI[1]=1;bobber.localAI[2]=ItemID.TruffleWorm;
                Call(Get(host,"Observation"),"Invalidate");Call(Get(host,"Observation"),"Read",p);
                var observed=((Array)Get(Get(host,"Observation"),"Bobbers")).GetValue(0);Require(GetOptional(observed,"Candidate")==null,"Truffle Worm summon marker is never interpreted as Iron Pickaxe");
                bobber.ai[1]=a;bobber.localAI[1]=l1;bobber.localAI[2]=l2;
                for(int x=44;x<74;x++)for(int y=44;y<60;y++)Main.tile[x,y].liquid=0;
                for(int i=0;i<31;i++)NativeQuickItemChecks.BeginWorldStep();result=Call(catalog,"Current",p,bobber,false);
                Require(!(bool)Get(result,"Known"),"same bobber coordinate detects changed water on bounded refresh");
                for(int x=44;x<74;x++)for(int y=44;y<60;y++)Main.tile[x,y].liquid=255;
                Call(catalog,"Current",p,bobber,true);
                var rules=(System.Collections.Generic.List<Terraria.GameContent.FishDropRules.FishDropRule>)typeof(Terraria.GameContent.FishDropRules.FishDropRuleList).GetField("_rules",Flags).GetValue(Main.FishDropsDB);
                var unsafeRule=new Terraria.GameContent.FishDropRules.FishDropRule{PossibleItems=new[]{(int)ItemID.Bass},Conditions=new Terraria.GameContent.FishDropRules.AFishingCondition[]{new UnexpectedFishingCondition()},Rarity=rules[0].Rarity};
                rules.Insert(0,unsafeRule);
                try
                {
                    result=Call(catalog,"Current",p,bobber,true);
                    Require(!(bool)Get(result,"Known") && UnexpectedFishingCondition.Calls==0,"unknown native rule shape is rejected before invoking a condition that could mutate RNG");
                }
                finally{rules.Remove(unsafeRule);Call(catalog,"Current",p,bobber,true);}
            }
            finally{Main.rand=savedRandom;}
            Console.WriteLine("PASS G10 native candidate rules, blank-search cost, RNG/bait/state isolation and stationary pond invalidation.");
        }
        internal static void Step(object context,object input,Vector2 aim,bool held,int empty)
        {
            // Full native ItemCheck also measures the temporarily selected
            // non-rod item. Load the actual textures for newly caught inventory.
            textures?.LoadItemTextures(Main.LocalPlayer.inventory.Where(item=>item!=null).Select(item=>item.type).Distinct());
            NativeToolExecutionChecks.Sample(context,input,aim,held);NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active)Main.projectile[i].Update(i);
            Call(context,"UpdateRuntime");NativeToolExecutionChecks.Outer(context,input,empty);
        }
    }
}
