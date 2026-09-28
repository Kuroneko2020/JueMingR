using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Fishing;
using JueMingR.Features.QuickItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingEquipmentChecks
    {
        private static object context,host,owner,tools,input;
        private static Action saveBoundary;
        internal static void Run(object value)
        {
            context=value;host=Get(value,"Fishing");owner=Get(host,"Equipment");tools=Get(value,"Tools");input=Get(value,"Input");
            bool hint=Player.Settings.ShowLoadoutShareHint;Player.Settings.ShowLoadoutShareHint=false;
            try{NoBorrowNotice();ReturnOnlyObservation();F5Equipment();PendingLoadoutTakeover();Clothing();Social();Loadout();Void();ManualGroup();Favorites();MovedOriginal();LaterIntent();StackSource();WaterHook();UpgradeChain();Lifecycle();NativeFishingNetworkChecks.Equipment(context,Reset,Cast,Stop);}
            finally{Player.Settings.ShowLoadoutShareHint=hint;}
            NativeFishingChecks.Save(host,new FishingOptions());Frames(25);
            Console.WriteLine("PASS G10 real wet-bobber equipment admission, strict clothing upgrade, exact-reference return, social sources, shared loadout projection, usable void and manual group handoff.");
        }
        private static void NoBorrowNotice()
        {
            var p=Reset();Put(p.armor,0,5591);Cast(new FishingOptions(loadout:true));
            Require(!(bool)Get(owner,"Active"),"best current group does not create a loan");
            var notices=new List<string>();Call(host,"TakeFeedback",(Action<string>)notices.Add);notices.Clear();Stop();
            Require(GetOptional(owner,"player")==null,"ending an unborrowed session retires its prepared owner");
            NativeFishingChecks.Step(context,input,new Vector2(850,718),true,0);Frames(150);Cast(new FishingOptions(loadout:true));
            p.TrySwitchingLoadout(1);Frames(25);Stop();Call(host,"TakeFeedback",(Action<string>)notices.Add);
            Require(!notices.Any(x=>x.Contains("归还") || x.Contains("接管装备组")),"no actual loan never claims restoration or handoff");
        }
        private static void ReturnOnlyObservation()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
            Require(ReferenceEquals(p.armor[0],hat),"return-only fixture acquired real equipment");
            Main.mouseItem.SetDefaults(ItemID.StoneBlock);NativeFishingChecks.Save(host,new FishingOptions());
            NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)Main.GameUpdateCount);
            Require(!(bool)Get(Get(host,"Session"),"Active") && (bool)Get(owner,"Active"),"disabled fishing retains only its blocked equipment return");
            object observation=Get(host,"Observation");long scans=(long)Get(observation,"Scans");
            for(int i=0;i<120;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)Main.GameUpdateCount);}
            Require((long)Get(observation,"Scans")==scans && (bool)Get(owner,"Active") && ReferenceEquals(p.armor[0],hat),"equipment-only tail keeps exact items without waking projectile scans");
            Main.mouseItem.TurnToAir();Frames(25);
            Require(!(bool)Get(owner,"Active") && ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],hat),"return resumes independently when the real mouse ownership clears");
            Console.WriteLine("PASS G10 independent equipment return: 120 retained-tail updates, zero projectile scans, exact items restored.");
        }
        private static void F5Equipment()
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State");
            foreach(bool group in new[]{true,false})
            {
                var p=Reset();var hat=Put(group?p.Loadouts[1].Armor:p.inventory,group?0:12,5591);
                Cast(new FishingOptions(auto:true));
                Main.screenWidth=960;Main.screenHeight=760;Main.UIScale=1;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                FiniteCostChecks.SetCpuFont(10);Call(Get(shell,"renderer"),"RefreshResources");Call(state,"Navigate",7);Call(state,"RestoreVisible");NativeFishingUiChecks.Prepare(context);NativeFishingExperienceChecks.ShellFrame(context,Vector2.Zero,false);
                var point=new Vector2((float)Get(state,"X")+30,(float)Get(state,"Y")+30);
                NativeFishingChecks.Save(host,new FishingOptions(auto:true,loadout:group,equipment:!group));
                NativeFishingExperienceChecks.ShellFrame(context,point,true);
                Require(p.CurrentLoadoutIndex==0 && !ReferenceEquals(p.armor[0],hat),"consumed F5 physical press cannot apply equipment");
                p.chest=0;NativeFishingExperienceChecks.ShellFrame(context,point,false);Require(p.CurrentLoadoutIndex==0 && !ReferenceEquals(p.armor[0],hat),"open native chest still prevents F5 equipment");p.chest=-1;
                Main.drawingPlayerChat=true;NativeFishingExperienceChecks.ShellFrame(context,point,false);Require(p.CurrentLoadoutIndex==0 && !ReferenceEquals(p.armor[0],hat),"chat remains an equipment safety gate while F5 is visible");Main.drawingPlayerChat=false;
                // Native chat admission closes F5. Reopen its real presentation
                // before testing a held item and the ordinary hover exception.
                Call(state,"RestoreVisible");NativeFishingUiChecks.Prepare(context);
                Main.mouseItem.SetDefaults(ItemID.StoneBlock);NativeFishingExperienceChecks.ShellFrame(context,point,false);Require(p.CurrentLoadoutIndex==0 && !ReferenceEquals(p.armor[0],hat),"held mouse item remains protected in F5");Main.mouseItem.TurnToAir();
                NativeFishingExperienceChecks.ShellFrame(context,point,false);
                Require((bool)Get(state,"OwnsPointer") && p.mouseInterface,"fixture exercises the actual F5-owned native mouse-interface lease: visible="+Get(state,"Visible")+" ready="+Get(state,"Ready")+" xy="+Get(state,"X")+","+Get(state,"Y")+" point="+point+" pointer="+Get(state,"PointerX")+","+Get(state,"PointerY")+" mouse="+p.mouseInterface+" raw="+Terraria.GameInput.PlayerInput.RawMouseScale);
                Require((bool)Get(state,"Visible") && ReferenceEquals(p.armor[0],hat) && p.CurrentLoadoutIndex==(group?1:0),"actual F5 hover permits fishing equipment after physical release, group="+group+" interface="+Get(shell,"CanFishingEquipmentInput")+" start="+Get(input,"CanStartActions")+" retain="+Get(input,"CanRetainIntent")+" apply="+Call(owner,"ApplyGate")+" mouse="+p.mouseInterface+" own="+Get(state,"OwnsPointer")+" original="+Get(shell,"priorMouseInterface")+" session="+Get(Get(host,"Session"),"Phase")+" owner="+(GetOptional(owner,"player")!=null)+" use="+Get(Get(tools,"Use"),"Active")+" buffered="+p.selectedItemState.HasBufferedChange+" animation="+p.itemAnimation+" time="+p.itemTime+" return="+Get(owner,"returning"));
                Require(!(bool)Get(shell,"CanProcessingInput"),"fishing equipment exception leaves tool/processing permission closed");
                NativeFishingChecks.Save(host,new FishingOptions());
                for(int i=0;i<25;i++)
                {
                    NativeFishingExperienceChecks.ShellFrame(context,point,true);
                    Require(ReferenceEquals(p.armor[0],hat) && (bool)Get(owner,"Active"),"F5-consumed physical press also defers an existing equipment loan's return");
                }
                for(int i=0;i<25;i++)NativeFishingExperienceChecks.ShellFrame(context,point,false);
                Require(p.CurrentLoadoutIndex==0 && !ReferenceEquals(p.armor[0],hat) && !(bool)Get(owner,"Active"),"physical release completes the exact equipment return while F5 stays open");
                Call(shell,"CloseAndSubmitPosition");NativeFishingExperienceChecks.ShellFrame(context,Vector2.Zero,false);Stop();
            }
        }
        private static void PendingLoadoutTakeover()
        {
            var p=Reset();Put(p.Loadouts[1].Armor,0,5591);Cast(new FishingOptions(loadout:true));
            Require(p.CurrentLoadoutIndex==1 && (bool)Get(owner,"Active"),"takeover fixture actually borrows a saved loadout");
            NativeFishingChecks.Save(host,new FishingOptions());
            // Isolate the native CC rejection at the real return boundary; a
            // full Player.Update would recompute CCed before this observation.
            p.frozen=true;Call(owner,"Restore",false);
            string pending=(string)GetOptional(owner,"Status");
            Require(pending?.Contains("待归还")==true && (bool)Get(owner,"Active"),"blocked actual return retains an accurate pending result");
            p.frozen=false;p.TrySwitchingLoadout(2);
            var notices=new List<string>();Call(host,"TakeFeedback",(Action<string>)notices.Add);
            Require(p.CurrentLoadoutIndex==2 && !(bool)Get(owner,"Active") && GetOptional(owner,"Status")==null && !Equals(GetOptional(host,"Error"),pending) && !notices.Contains(pending),"real manual takeover clears the retired loan's status and pending announcement");
            Stop();
        }
        private static void Clothing()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.AnglerHat);var better=Put(p.inventory,12,5591);var equal=Put(p.armor,1,5592);var spare=Put(p.inventory,13,5592);
            NativeFishingChecks.Save(host,new FishingOptions(equipment:true));
            NativeFishingChecks.Step(context,input,new Vector2(850,718),true,3);
            Require(ReferenceEquals(p.armor[0],old) && (int)Get(owner,"Count")==0,"gear does not move on the first airborne cast");
            Frames(150,3);
            Require(ReferenceEquals(p.armor[0],better) && ReferenceEquals(p.inventory[12],old),"actual wet session equips upgraded fishing head by two real references: "+GetOptional(host,"Error"));
            Require(ReferenceEquals(p.armor[1],equal) && ReferenceEquals(p.inventory[13],spare),"equal-score clothing retains the currently worn real item");
            Require(p.fishingSkill==10,"the actually equipped fishing clothing contributes power through full native Player.Update");
            long plans=(long)Get(owner,"Plans"),exchanges=(long)Get(owner,"Exchanges");Frames(100,3);
            Require((long)Get(owner,"Plans")==plans && (long)Get(owner,"Exchanges")==exchanges,"stable equipment does not re-plan or repeat exchanges across extra outer callbacks");
            CutWhileBorrowed(p,false);
            Stop();Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],better) && (int)Get(owner,"Count")==0,"disabled strategy restores the exact original objects");
        }
        private static void Social()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.armor,10,5591);var bag=Put(p.armor,13,ItemID.LavaproofTackleBag);
            Cast(new FishingOptions(equipment:true));
            Require(ReferenceEquals(p.armor[0],hat) && ReferenceEquals(p.armor[10],old) && Enumerable.Range(3,7).Any(i=>ReferenceEquals(p.armor[i],bag)),"social candidates pass legality in the post-swap projection");
            Stop();Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.armor[10],hat) && ReferenceEquals(p.armor[13],bag),"social head and accessory return to their exact original slots");
        }
        private static void Loadout()
        {
            var p=Reset();var head=Put(p.armor,0,5591);head.favorited=true;var body=Put(p.armor,1,5592);body.favorited=true;
            var legs=Put(p.Loadouts[1].Armor,2,5593);Put(p.Loadouts[2].Armor,0,ItemID.AnglerHat);
            Cast(new FishingOptions(loadout:true));
            Require(p.CurrentLoadoutIndex==1 && p.armor[0].IsAir && ReferenceEquals(p.GetEffectiveArmor(0),head) && ReferenceEquals(p.GetEffectiveArmor(1),body) && ReferenceEquals(p.armor[2],legs),"native double-swap projection includes shared original wear and chooses group1, without moving shared items");
            Require(p.fishingSkill==15,"native effective shared head/body plus switched legs actually contribute fishing power");
            CutWhileBorrowed(p,true);
            Stop();Require(p.CurrentLoadoutIndex==0 && ReferenceEquals(p.armor[0],head) && ReferenceEquals(p.armor[1],body),"whole-group owner restores using actual native switching");
            NativeFishingChecks.Step(context,input,new Vector2(850,718),true,0);Frames(150);
            Cast(new FishingOptions(loadout:true));p.TrySwitchingLoadout(2);Frames(30);Stop();
            Require(p.CurrentLoadoutIndex==2,"a real manual group switch revokes automatic switch-back and same-session reapplication");
        }
        private static void CutWhileBorrowed(Player p,bool group)
        {
            var session=Get(host,"Session");long token=(long)Get(session,"Token"),swaps=(long)Get(owner,"Exchanges");int loadout=p.CurrentLoadoutIndex;
            var armor=p.armor.ToArray();var old=Main.projectile.First(q=>q.active && q.bobber && q.owner==p.whoAmI);int key=(int)old.key;
            NativeFishingChecks.Save(host,new FishingOptions(auto:true,cut:true,filterMode:1,crates:0,quests:0,npcs:0,loadout:group,equipment:!group));
            p.AddBuff(122,2000);old.ai[1]=-240;old.localAI[1]=ItemID.Bass;old.localAI[2]=ItemID.Worm;
            for(int i=0;i<300;i++)
            {
                Frames(1,3);
                Require((bool)Get(session,"Active") && (long)Get(session,"Token")==token && (long)Get(owner,"Exchanges")==swaps && p.CurrentLoadoutIndex==loadout && p.armor.SequenceEqual(armor),"owned cut never ends the equipment loan or swaps its real objects");
                if(!Main.projectile.Any(q=>q.active && q.bobber && (int)q.key==key) && (bool)Get(session,"InLiquid"))break;
            }
            Require(p.selectedItem==0 && (bool)Get(session,"InLiquid") && !Main.projectile.Any(q=>q.active && q.bobber && (int)q.key==key),"cut returns the rod and wet replacement with the same equipment loan");
        }
        private static void Void()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.bank4.item,2,5591);var closedBag=Put(p.inventory,13,5325);
            Cast(new FishingOptions(equipment:true));Require(ReferenceEquals(p.armor[0],old),"closed void bag does not authorize a readable bank4 array");
            closedBag.ChangeItemType(4131);Frames(45);
            Require(ReferenceEquals(p.armor[0],hat) && ReferenceEquals(p.bank4.item[2],old),"opening the real void bag updates a stable fishing session's available source");
            closedBag.ChangeItemType(5325);Stop();
            Require(ReferenceEquals(p.armor[0],hat) && (int)Get(owner,"Count")>0,"closing void access retains real return responsibility without writing the inaccessible array");
            closedBag.ChangeItemType(4131);Frames(25);Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.bank4.item[2],hat),"void equipment returns to the same actual source after access is restored");
        }
        private static void ManualGroup()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);var other=Put(p.Loadouts[1].Armor,0,ItemID.IronHelmet);
            Cast(new FishingOptions(equipment:true));p.TrySwitchingLoadout(1);Stop();Frames(90);
            Require(p.CurrentLoadoutIndex==1 && ReferenceEquals(p.armor[0],other) && ReferenceEquals(p.Loadouts[0].Armor[0],hat) && ReferenceEquals(p.inventory[12],old) && (int)Get(owner,"Count")>0,"per-item journal waits for its inactive logical group and never writes the player's new group");
            p.TrySwitchingLoadout(0);Frames(25);
            Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],hat) && (int)Get(owner,"Count")==0,"voluntary return to the recorded group completes its exact inverse");
        }
        private static Player Reset()
        {
            NativeFishingChecks.Save(host,new FishingOptions());Frames(25);Require((int)Get(owner,"Count")==0,"previous fixture completed its real return responsibility");
            var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
            foreach(var item in p.armor)item.TurnToAir();foreach(var loadout in p.Loadouts){foreach(var item in loadout.Armor)item.TurnToAir();foreach(var item in loadout.Dye)item.TurnToAir();}
            foreach(var item in p.bank4.item)item.TurnToAir();p.CurrentLoadoutIndex=0;Main.ActiveWorldFileData.GameMode=0;p.extraAccessory=false;Main.mouseItem.TurnToAir();
            p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
            return p;
        }
        private static void Favorites()
        {
            foreach(bool keep in new[]{false,true})foreach(bool changeShare in new[]{false,true})
            {
                var p=Reset();var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");string reason;
                Require(settings.TryChange(settings.Current.Toggles(keep,settings.Enabled),null,out reason),"favorite fixture setting admitted");NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
                var old=Put(p.armor,0,ItemID.CopperHelmet);old.favorited=true;var hat=Put(p.inventory,12,5591);hat.favorited=true;var other=Put(p.Loadouts[1].Armor,0,ItemID.IronHelmet);
                Cast(new FishingOptions(equipment:true));Require(ReferenceEquals(p.armor[0],hat) && !hat.favorited,"borrowing a favorite inventory item does not enable equipment sharing");
                if(changeShare)
                {
                    p.TrySwitchingLoadout(1);typeof(ItemSlot).GetMethod("ToggleLoadoutShare",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p.armor,8,0});
                    Require(other.favorited,"real native shared equipment toggle executed");p.TrySwitchingLoadout(0);
                }
                Stop();Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],hat) && hat.favorited && old.favorited==!changeShare,"return distinguishes saved inventory favorite from later same-slot sharing intent, keep="+keep+" share="+changeShare);
                if(changeShare)Require(other.favorited,"new shared item remains shared after return");
            }
        }
        private static void MovedOriginal()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
            Click(p.inventory,0,12);Click(p.inventory,0,16);var twin=Put(p.inventory,12,ItemID.CopperHelmet);Frames(2);Stop();
            Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[16],hat) && ReferenceEquals(p.inventory[12],twin),"real manually moved original is found by reference; equal-type replacement is never overwritten or substituted");
            p=Reset();hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
            // Native inventory clicks occupy the original empty return address.
            var stone=Put(p.inventory,16,ItemID.StoneBlock);Click(p.inventory,0,16);Click(p.inventory,0,12);Frames(2);Stop();
            Require(p.armor[0].IsAir && p.inventory.Take(50).Any(x=>ReferenceEquals(x,hat)) && ReferenceEquals(p.inventory[12],stone),"originally empty wear returns the borrowed item to another legal empty slot without replacing the occupied source");
        }
        private static void Click(Item[] array,int contextId,int slot)
        {Main.mouseLeft=Main.mouseLeftRelease=true;ItemSlot.LeftClick(array,contextId,slot);Main.mouseLeft=false;}
        private static void LaterIntent()
        {
            foreach(bool shared in new[]{false,true})
            {
                var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
                if(shared)typeof(ItemSlot).GetMethod("ToggleLoadoutShare",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p.armor,8,0});
                else typeof(ItemSlot).GetMethod("ToggleFavorited",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{old});
                Stop();Require(ReferenceEquals(p.armor[0],hat) && ReferenceEquals(p.inventory[12],old) && (shared?hat.favorited:old.favorited) && (int)Get(owner,"Count")==0,"later explicit favorite/share hands the real locations to the player without clearing the new flag or claiming restoration");
            }
            {
                var p=Reset();var bag=Put(p.armor,13,ItemID.AnglerEarring);bag.favorited=true;var other=Put(p.Loadouts[1].Armor,13,ItemID.TreasureMagnet);
                Cast(new FishingOptions(equipment:true));Require(!ReferenceEquals(p.armor[13],bag),"social favorite actually borrowed");p.TrySwitchingLoadout(1);
                typeof(ItemSlot).GetMethod("ToggleLoadoutShare",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p.armor,9,13});p.TrySwitchingLoadout(0);Stop();
                Require(ReferenceEquals(p.armor[13],bag) && !bag.favorited && other.favorited,"later same-slot social sharing also prevents reviving the source's old share flag");
            }
        }
        private static void StackSource()
        {
            foreach(bool bank in new[]{false,true})
            {
                var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);if(bank)Put(p.inventory,13,4131);var array=bank?p.bank4.item:p.inventory;
                var hats=Put(array,12,5591);hats.stack=2;hats.favorited=true;
                Require(hats.maxStack>=2,"native upgraded fishing clothing legitimately stacks");Cast(new FishingOptions(equipment:true));var single=p.armor[0];
                Require(single.type==5591 && single.stack==1 && !ReferenceEquals(single,hats) && ReferenceEquals(array[12],hats) && hats.stack==1 && hats.favorited && Main.mouseItem.IsAir,"real native split equips one, conserves source stack/favorite and leaves the mouse empty");Stop();
                Require(ReferenceEquals(p.armor[0],old) && array.Any(x=>ReferenceEquals(x,single)) && hats.stack+single.stack==2,"split singleton returns to its reserved original container without replacing the remaining stack");
                p=Reset();old=Put(p.armor,0,ItemID.CopperHelmet);hats=Put(p.inventory,12,5591);hats.stack=2;
                for(int i=1;i<50;i++)if(i!=12)Put(p.inventory,i,ItemID.StoneBlock);
                Cast(new FishingOptions(equipment:true));Require(ReferenceEquals(p.armor[0],old) && hats.stack==2 && Main.mouseItem.IsAir,"no same-container staging slot leaves the real stack untouched");
                p.inventory[20].TurnToAir();Frames(30);Require(p.armor[0].type==5591 && hats.stack==1,"creating a real safe empty slot resumes stacked equipment admission");Stop();
            }
        }
        private static void UpgradeChain()
        {
            var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var first=Put(p.inventory,12,ItemID.AnglerHat);Cast(new FishingOptions(equipment:true));
            var better=Put(p.inventory,13,5591);Frames(30);Require(ReferenceEquals(p.armor[0],better) && (int)Get(owner,"Count")==2,"a later upgrade records both exact exchanges");
            Put(p.inventory,20,ItemID.StoneBlock);Click(p.inventory,0,20);Click(p.inventory,0,21);Frames(2);Stop();
            Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],first) && ReferenceEquals(p.inventory[13],better),"unrelated real inventory clicks preserve the full reverse upgrade chain");
        }
        private static void WaterHook()
        {
            var p=Reset();var hook=Put(p.armor,3,ItemID.LavaFishingHook);var earring=Put(p.inventory,12,ItemID.AnglerEarring);Cast(new FishingOptions(equipment:true));
            Require(ReferenceEquals(p.armor[3],earring) && p.armor[4].IsAir,"ordinary water replaces the unneeded old lava hook before an empty accessory slot");Stop();Require(ReferenceEquals(p.armor[3],hook),"old lava hook remains an exact return participant");
        }
        private static void Lifecycle()
        {
            foreach(bool gemsOnly in new[]{true,false})
            {
                var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
                int oldWorld=Main.item.Where(x=>x.active && x.type==ItemID.CopperHelmet).Sum(x=>x.stack),hatWorld=Main.item.Where(x=>x.active && x.type==5591).Sum(x=>x.stack);
                p.DropItems(gemsOnly);
                Require((int)Get(owner,"Count")==0,"native death boundary resolves exact exchanges before native item loss");
                if(gemsOnly)Require(ReferenceEquals(p.armor[0],old) && ReferenceEquals(p.inventory[12],hat),"softcore native DropItems preserves both returned originals");
                else Require(Main.item.Where(x=>x.active && x.type==ItemID.CopperHelmet).Sum(x=>x.stack)==oldWorld+1 && Main.item.Where(x=>x.active && x.type==5591).Sum(x=>x.stack)==hatWorld+1 && p.armor[0].IsAir && p.inventory[12].IsAir,"real full death drop creates one of each physical item without a private copy");
                p.dead=true;Call(context,"UpdateRuntime");p.dead=false;Frames(25);
                if(!gemsOnly)Require(p.armor[0].IsAir && p.inventory[12].IsAir,"post-death frames never refill lost equipment");
            }
            var audit=new Harmony("JueMingR.Tests.FishingEquipmentSave");
            var save=typeof(WorldGen).GetMethod("SaveAndQuit",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Action)},null);
            // Every production prefix still runs in native Harmony order. Only
            // the body is isolated so no world save worker or real file starts.
            audit.Patch(save,transpiler:new HarmonyMethod(typeof(NativeFishingEquipmentChecks),nameof(IsolateSave)));
            try
            {
                foreach(bool blocked in new[]{false,true})
                {
                    var p=Reset();var old=Put(p.armor,0,ItemID.CopperHelmet);var hat=Put(p.inventory,12,5591);Cast(new FishingOptions(equipment:true));
                    if(blocked)Main.mouseItem.SetDefaults(ItemID.StoneBlock);Item mouse=Main.mouseItem;
                    saveBoundary=()=>
                    {
                        Require(!(bool)Get(Get(tools,"Runtime"),"IsSessionActive"),"Items session retirement runs before the isolated native save body");
                        Require(ReferenceEquals(p.armor[0],blocked?hat:old) && ReferenceEquals(p.inventory[12],blocked?old:hat) && ReferenceEquals(Main.mouseItem,mouse),"equipment prefix returns safe pairs before Items invalidates, or preserves blocked actual layout and mouse");
                        if(blocked)Require(((string)GetOptional(host,"Error"))?.Contains("未归还")==true,"blocked exit accurately reports its unrecovered real layout");
                    };
                    save.Invoke(null,new object[]{null});Main.mouseItem.TurnToAir();Call(context,"UpdateRuntime");Frames(25);
                    Require(ReferenceEquals(p.armor[0],blocked?hat:old) && (int)Get(owner,"Count")==0,"new session never replays an old borrowed-layout journal");
                    if(blocked)
                    {
                        var messages=new List<string>();Call(host,"TakeFeedback",(Action<string>)messages.Add);
                        Require(messages.Count(x=>x.Contains("上一会话"))==1 && ((string)Get(host,"Error")).Contains("上一会话"),"unrestored quit result survives reentry as an accurate reviewable prior-session outcome");
                        messages.Clear();Call(host,"TakeFeedback",(Action<string>)messages.Add);Require(!messages.Any(x=>x.Contains("上一会话")),"prior-session outcome is announced only once");
                    }
                }
            }
            finally{saveBoundary=null;audit.Unpatch(save,HarmonyPatchType.All,audit.Id);}
        }
        private static IEnumerable<CodeInstruction> IsolateSave(IEnumerable<CodeInstruction> ignored)
        {yield return new CodeInstruction(OpCodes.Call,typeof(NativeFishingEquipmentChecks).GetMethod(nameof(Saved),BindingFlags.NonPublic|BindingFlags.Static));yield return new CodeInstruction(OpCodes.Ret);}
        private static void Saved(){saveBoundary?.Invoke();}
        private static Item Put(Item[] array,int slot,int type){array[slot].SetDefaults(type);return array[slot];}
        private static void Cast(FishingOptions options)
        {NativeFishingChecks.Save(host,options);NativeFishingChecks.Step(context,input,new Vector2(850,718),true,0);Frames(150);Require((bool)Get(Get(host,"Session"),"Active"),"equipment fixture has a real wet fishing session");}
        private static void Frames(int count,int empty=0){for(int i=0;i<count;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,empty);}
        private static void Stop(){NativeFishingChecks.Save(host,new FishingOptions());Frames(25);}
    }
}
