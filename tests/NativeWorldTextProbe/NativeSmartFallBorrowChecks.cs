using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Movement;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real .8 Item/Player/SelectedItemState objects in the existing isolated
    // host fixture. These checks prove borrowing, not complete fall rescue.
    internal static class NativeSmartFallBorrowChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object owner;
        private static Type ownerType;
        private static ItemOperationOwnership ownership;
        private static long generation;
        private static object items;
        private static readonly List<byte[]> packets=new List<byte[]>();
        private static bool Match(Item item,SmartFallFamily family)
        {return family==SmartFallFamily.DoubleJump?item.type==ItemID.CloudinaBottle:family==SmartFallFamily.Horseshoe?item.type==ItemID.LuckyHorseshoe:family==SmartFallFamily.DjinnLegs?item.type==ItemID.DjinnsCurse:family==SmartFallFamily.Grapple?Main.projHook[item.shoot]:family==SmartFallFamily.Umbrella?item.type==ItemID.Umbrella:family==SmartFallFamily.TeleportRod && item.type==ItemID.RodofDiscord;}
        internal static void Run(object context)
        {
            items=Get(context,"items");ownership=(ItemOperationOwnership)Get(items,"Ownership");generation=(long)Get(Get(items,"Runtime"),"Generation");
            ownerType=context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallBorrowOwner",true);
            var saved=Main.LocalPlayer;bool mouse=Main.mouseLeft;var patch=new Harmony("JueMingR.Tests.SmartFallBorrow");
            var pick=typeof(Player).GetMethod("PickItemSelectionOverride");
            patch.Patch(pick,postfix:new HarmonyMethod(typeof(NativeSmartFallBorrowChecks),nameof(Pick)));
            try
            {
                Conservation();PreparationFacts();PartialWrite();Takeover();MovedOriginal();BlockedReturn();HeldSelection();GrappleBorrow();Lifecycle();NativeSync();
                Main.player[Main.myPlayer]=saved;Call(context,"UpdateRuntime");FishingOverlap(context);
                Console.WriteLine("PASS SmartFallBorrow real item conservation, second-write failure, same-signature takeover, moved original, native held selection, pulse finalizer and generation boundary.");
            }
            finally{if(owner!=null)Call(owner,"EndSession");owner=null;patch.Unpatch(pick,HarmonyPatchType.All,patch.Id);Main.player[Main.myPlayer]=saved;Main.mouseLeft=mouse;}
        }
        private static Player Reset()
        {
            if(owner!=null)Call(owner,"EndSession");
            var p=new Player{whoAmI=Main.myPlayer,active=true};Main.player[Main.myPlayer]=p;Main.mouseItem.TurnToAir();Main.mouseLeft=false;
            PlayerInput.Triggers.Current.MouseLeft=PlayerInput.Triggers.Current.MouseRight=PlayerInput.Triggers.Current.SmartSelect=false;
            owner=Activator.CreateInstance(ownerType,Flags,null,new object[]{items,(Func<long>)(()=>generation),(Func<Item,SmartFallFamily,bool>)Match,null},null);
            return p;
        }
        private static Item Put(Item[] array,int slot,int type){array[slot].SetDefaults(type);return array[slot];}
        private static bool Borrow(Player p,Item item,SmartFallFamily family)
        {return (bool)Call(owner,"TryBorrow",p,generation,item,family);}
        private static void Return(Player p){Call(owner,"Restore",p,generation);}
        private static int Target(Player p,Item item){return Array.FindIndex(p.armor,x=>ReferenceEquals(x,item));}
        private static void Conservation()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);shoe.favorited=true;Require(shoe.Prefix(PrefixID.Warding) && shoe.prefix==PrefixID.Warding && shoe.prefix>0,"real accessory receives an applicable nonzero native prefix");int prefix=shoe.prefix;
            var vacant=p.armor[3];Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"real accessory exchange admitted");
            Require(ReferenceEquals(p.armor[3],shoe) && ReferenceEquals(p.inventory[12],vacant) && !shoe.favorited && shoe.stack==1 && shoe.prefix==prefix,"exact singleton/prefix exchanged and inventory favorite does not enable loadout sharing");
            Require(ownership.IsProtected(0,12) && ownership.IsProtected(5,3),"source and functional endpoint owned before consumer");
            Return(p);Require(ReferenceEquals(p.inventory[12],shoe) && ReferenceEquals(p.armor[3],vacant) && shoe.favorited && !(bool)Get(owner,"HasPending"),"exact original objects and favorite return without cloning");
            Require(p.inventory.Count(x=>ReferenceEquals(x,shoe))==1 && !p.armor.Any(x=>ReferenceEquals(x,shoe)),"one actual item remains exactly once");
            p=Reset();shoe=Put(p.armor,13,ItemID.LuckyHorseshoe);Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"social source obeys original accessory compatibility");Return(p);Require(ReferenceEquals(p.armor[13],shoe),"social item returns to its real slot");
            p=Reset();shoe=Put(p.bank.item,12,ItemID.LuckyHorseshoe);Require(ownerType.GetMethod("GetCandidate",Flags,null,new[]{typeof(Player),typeof(SmartFallFamily)},null).Invoke(owner,new object[]{p,SmartFallFamily.Horseshoe})==null && !Borrow(p,shoe,SmartFallFamily.Horseshoe),"bank item never becomes a rescue source");
            p=Reset();shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Main.mouseItem.SetDefaults(ItemID.StoneBlock);var cursor=Main.mouseItem;
            Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"an unrelated real cursor item does not block disjoint equipment endpoints");Return(p);
            Require(ReferenceEquals(Main.mouseItem,cursor) && cursor.type==ItemID.StoneBlock && ReferenceEquals(p.inventory[12],shoe) && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.Returned,"disjoint equipment return preserves the exact unrelated cursor object");Main.mouseItem.TurnToAir();
        }
        private static void PartialWrite()
        {
            foreach(int failAt in new[]{1,2})
            {
                var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);var vacant=p.armor[3];bool observed=false;
                Set(owner,"WriteBoundary",(Action<int>)(stage=>{observed=ownership.IsProtected(12)&&ownership.IsProtected(5,3);if(stage==failAt)throw new InvalidOperationException("injected-write");}));
                Require(!Borrow(p,shoe,SmartFallFamily.Horseshoe) && observed,"fault happens only after source and target ownership exists");
                Require(ReferenceEquals(p.inventory[12],shoe) && ReferenceEquals(p.armor[3],vacant) && !(bool)Get(owner,"HasPending") && !ownership.IsProtected(12),"failed write safely returns both exact objects, failAt="+failAt);
            }
        }
        private static object Preparation(Player p,Item item,SmartFallFamily family)
        {return Call(owner,"GetPreparation",p,generation,item,family);}
        private static long Blocker(Player p){return (long)Call(owner,"BlockerStamp",p);}
        private static void PreparationFacts()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);var empty=p.armor[3];
            var plan=Preparation(p,shoe,SmartFallFamily.Horseshoe);long exchanges=(long)Get(owner,"Exchanges");
            Require((bool)Get(plan,"AvailableNow") && !(bool)Get(plan,"AlreadyInPlace") && (int)Get(plan,"BorrowWrites")==2 && (int)Get(plan,"SelectionWrites")==0 &&
                ReferenceEquals(Get(plan,"Source"),p.inventory) && (int)Get(plan,"SourceSlot")==12 && ReferenceEquals(Get(plan,"Target"),p.armor) && (int)Get(plan,"TargetSlot")==3 && !(bool)Get(plan,"Displaced") &&
                ReferenceEquals(p.inventory[12],shoe) && ReferenceEquals(p.armor[3],empty) && !ownership.HasFallEquipment && !(bool)Get(owner,"HasPending") && (long)Get(owner,"Exchanges")==exchanges,"preparation exposes the two exact legal endpoints without acquiring a token or writing an item");
            p.itemAnimation=3;long blocked=Blocker(p);plan=Preparation(p,shoe,SmartFallFamily.Horseshoe);
            Require(!(bool)Get(plan,"AvailableNow") && Get(plan,"Availability").ToString()=="Busy" && !Borrow(p,shoe,SmartFallFamily.Horseshoe),"a carried passive capability is not ready while its actual equipment operation is busy");
            long reads=(long)Get(owner,"SourceReads");p.itemAnimation=2;
            for(int i=0;i<64;i++)Require(Blocker(p)==blocked,"unchanged busy qualification does not reopen a rejected borrow");
            Require((long)Get(owner,"SourceReads")==reads && (long)Get(owner,"Exchanges")==exchanges,"constant-size blocker observation does not scan sources or exchange while busy");
            p.itemAnimation=0;Require(Blocker(p)!=blocked && (bool)Get(Preparation(p,shoe,SmartFallFamily.Horseshoe),"AvailableNow"),"busy-to-idle qualification alone reopens the same exact source without inventory revision");
            p.selectedItemState.Select(4);blocked=Blocker(p);Require(Get(Preparation(p,shoe,SmartFallFamily.Horseshoe),"Availability").ToString()=="SelectionBlocked","actual buffered native selection blocks a new equipment loan");
            p.selectedItemState.Update();Require(Blocker(p)!=blocked && Borrow(p,shoe,SmartFallFamily.Horseshoe),"native SelectedItemState consumes its buffer and the same source becomes borrowable");Return(p);
            p=Reset();var cloud=Put(p.armor,3,ItemID.CloudinaBottle);p.noItems=p.cursed=true;p.itemAnimation=2;
            plan=Preparation(p,cloud,SmartFallFamily.DoubleJump);
            Require((bool)Get(plan,"AlreadyInPlace") && (bool)Get(plan,"AvailableNow") && (int)Get(plan,"BorrowWrites")==0 && Borrow(p,cloud,SmartFallFamily.DoubleJump) && !(bool)Get(owner,"HasPending"),"already equipped jump capability needs no item operation even under cursed/noItems and an unrelated animation; native Jump consumption is checked by the action representative");
            p=Reset();p.noItems=p.cursed=true;shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);
            Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"cursed/noItems does not invent an armor-exchange restriction absent from native ItemSlot");Return(p);
            var rod=Put(p.inventory,14,ItemID.RodofDiscord);
            Require(Get(Preparation(p,rod,SmartFallFamily.TeleportRod),"Availability").ToString()=="ItemUseBlocked" && !Borrow(p,rod,SmartFallFamily.TeleportRod) && !p.controlUseItem,"the same cursed state still rejects the rod's real item-use preparation");
            p.noItems=p.cursed=false;Require(Borrow(p,rod,SmartFallFamily.TeleportRod),"rod can prepare once its actual item restriction clears");p.selectedItemState.Update();p.noItems=p.cursed=true;Call(owner,"BeforeSync",p,true);
            Require(!p.controlUseItem,"a newly applied noItems condition cannot receive an old prepared rod use pulse");Return(p);p.selectedItemState.Update();
            Console.WriteLine("PASS SmartFallBorrow preparation facts: exact read-only endpoints, unchanged busy stamp/no source scans, native selection buffer release, worn cursed capability and separate rod use restriction. Animation values here are component gate states, not a full rescue.");
        }
        private static void Takeover()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"takeover fixture");int at=Target(p,shoe);
            var twin=new Item();twin.SetDefaults(shoe.type);p.armor[at]=twin;p.inventory[18]=shoe;
            Return(p);Require(ReferenceEquals(p.armor[at],twin) && ReferenceEquals(p.inventory[18],shoe) && !(bool)Get(owner,"HasPending"),"same type/stack/prefix replacement remains player-owned");
            p=Reset();shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Borrow(p,shoe,SmartFallFamily.Horseshoe);int slot=Target(p,shoe);p.TrySwitchingLoadout(1);var newArmor=p.armor;
            Call(owner,"ManualLoadout",p);Return(p);Require(ReferenceEquals(p.armor,newArmor) && ReferenceEquals(p.Loadouts[0].Armor[slot],shoe) && !(bool)Get(owner,"HasPending") && !ownership.IsProtected(5,slot),"later native loadout choice cannot be overwritten or keep the old mask on the new group");
        }
        private static void MovedOriginal()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);
            int[] legal={ItemID.Aglet,ItemID.BandofRegeneration,ItemID.BandofStarpower,ItemID.Shackle,ItemID.Compass};
            for(int i=0;i<legal.Length;i++)Put(p.armor,3+i,legal[i]);
            for(int i=3;i<8;i++)for(int j=i+1;j<8;j++)Require(ItemSlot.CanEquipBothAccessories(p.armor[i],p.armor[j],false),"moved-original fixture starts in an independently legal unique native loadout");
            var old=p.armor[3];Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"occupied functional endpoint fixture");int at=Target(p,shoe);old=p.inventory[12];
            p.inventory[16]=old;var twin=new Item();twin.SetDefaults(old.type);p.inventory[12]=twin;Call(items.GetType().GetProperty("World",Flags)?.GetValue(items)??Get(items,"World"),"InvalidateObservation");
            Return(p);Require(ReferenceEquals(p.armor[at],old) && ReferenceEquals(p.inventory[16],shoe) && ReferenceEquals(p.inventory[12],twin),"moved exact original returns through its actual legal endpoint; equal signature replacement untouched");
            Require((SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.Returned,"actual reverse exchange is explicitly distinguished from takeover");
            p=Reset();shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);for(int i=0;i<legal.Length;i++)Put(p.armor,3+i,legal[i]);Borrow(p,shoe,SmartFallFamily.Horseshoe);old=p.inventory[12];p.bank.item[0]=old;p.inventory[12]=new Item();Call(Get(items,"World"),"InvalidateObservation");Return(p);
            Require(!(bool)Get(owner,"HasPending") && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.TakenOver && ReferenceEquals(p.bank.item[0],old) && p.armor.Any(x=>ReferenceEquals(x,shoe)),"manual move outside legal loan domain is player takeover, never bank scanning or infinite return debt");
        }
        private static void HeldSelection()
        {
            var p=Reset();var anchor=p.inventory[0];var umbrella=Put(p.inventory,12,ItemID.Umbrella);
            Require(Borrow(p,umbrella,SmartFallFamily.Umbrella),"held umbrella uses real inventory source");p.selectedItemState.Update();Require(p.selectedItem==12 && p.selectedItemState.HasActiveOverride,"native SelectedItemState owns actual selection override");
            for(int i=0;i<3;i++){Call(owner,"EndItemCheck",p,(long)Get(owner,"Operation"),false,null);p.selectedItemState.Update();Require(p.selectedItem==12,"umbrella remains held across one-shot item-check finalizers");}
            Return(p);p.selectedItemState.Update();Require(p.selectedItem==0 && ReferenceEquals(p.inventory[0],anchor) && ReferenceEquals(p.inventory[12],umbrella),"umbrella earliest permitted release restores native anchor");
            p=Reset();umbrella=Put(p.inventory,12,ItemID.Umbrella);Borrow(p,umbrella,SmartFallFamily.Umbrella);p.selectedItemState.Update();Call(owner,"ManualSelection");p.selectedItemState.Select(4);p.selectedItemState.Update();Return(p);Require(p.selectedItem==4,"later player hotbar choice wins");
            p=Reset();var rod=Put(p.inventory,12,ItemID.RodofDiscord);Borrow(p,rod,SmartFallFamily.TeleportRod);p.selectedItemState.Update();Call(owner,"BeforeSync",p,true);long operation=(long)Get(owner,"Operation");bool entered=(bool)Call(owner,"BeginItemCheck",p);Require(entered && Main.mouseLeft && p.controlUseItem,"one actual use scope borrows required bits");
            Call(owner,"EndItemCheck",p,operation,true,new InvalidOperationException("native-error"));Call(owner,"EndUpdate",p,generation);Require(!Main.mouseLeft && !p.controlUseItem,"exception finalizer releases only borrowed mouse/input contribution");p.selectedItemState.Update();Require(p.selectedItem==0,"exception return follows native selection state");
            p=Reset();rod=Put(p.inventory,0,ItemID.RodofDiscord);Require(Borrow(p,rod,SmartFallFamily.TeleportRod) && (bool)Get(owner,"SelectionReady"),"already selected rod still has the exact-use token and immediate native selection readiness");Call(owner,"BeforeSync",p,true);Require(p.controlUseItem,"already selected rod receives its one native pulse");Call(owner,"EndUpdate",p,generation);Return(p);Require(p.selectedItem==0 && !(bool)Get(owner,"HasPending"),"same-slot use cleanup has no artificial selection return");
            p=Reset();umbrella=Put(p.inventory,12,ItemID.Umbrella);p.controlUseItem=true;Require(!Borrow(p,umbrella,SmartFallFamily.Umbrella),"ongoing real item-use input cannot be hijacked for new held selection");
            p=Reset();umbrella=Put(p.inventory,12,ItemID.Umbrella);Borrow(p,umbrella,SmartFallFamily.Umbrella);p.selectedItemState.Update();PlayerInput.Triggers.Current.MouseLeft=true;Call(owner,"BeforeSync",p,false);Require(!(bool)Get(owner,"HasPending") && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.TakenOver,"later real use takes over a sustained umbrella selection");PlayerInput.Triggers.Current.MouseLeft=false;
        }
        private static void GrappleBorrow()
        {
            var p=Reset();var old=Put(p.miscEquips,4,ItemID.GrapplingHook);var chosen=Put(p.inventory,12,ItemID.BatHook);p.grappling[0]=42;p.grapCount=1;
            var saved=Main.projectile[42];var hook=new Projectile();hook.SetDefaults(chosen.shoot);hook.owner=p.whoAmI;hook.active=true;hook.ai[0]=2;Main.projectile[42]=hook;
            try
            {
                Require(Borrow(p,chosen,SmartFallFamily.Grapple) && ReferenceEquals(p.QuickGrapple_GetItemToUse(),chosen),"the original quick-grapple lookup consumes the exact selected temporary hook, not the previous misc/first carried hook");
                Return(p);Require(ReferenceEquals(p.miscEquips[4],old) && ReferenceEquals(p.inventory[12],chosen) && p.grappling[0]==42 && p.grapCount==1 && hook.active && hook.ai[0]==2 && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.Returned,"returning hook objects never invokes Jump, clears grappling or kills the initialized attached original projectile");
            }
            finally{Main.projectile[42]=saved;}
        }
        private static void BlockedReturn()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Borrow(p,shoe,SmartFallFamily.Horseshoe);
            for(int i=0;i<50;i++){p.inventory[i]=new Item();p.inventory[i].SetDefaults(ItemID.StoneBlock);}
            Return(p);long searches=(long)Get(owner,"ReturnSearches");
            for(int i=0;i<100;i++)Return(p);
            Require((long)Get(owner,"ReturnSearches")==searches && (bool)Get(owner,"HasPending"),"100 unchanged blocked returns perform no repeat full inventory search or exchange");
            p.inventory[18]=new Item();Call(Get(items,"World"),"InvalidateObservation");Return(p);
            Require((long)Get(owner,"ReturnSearches")==searches+1 && ReferenceEquals(p.inventory[18],shoe) && !(bool)Get(owner,"HasPending"),"actual empty-space change enables one exact recovery and retires debt");
        }
        private static void Lifecycle()
        {
            var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Borrow(p,shoe,SmartFallFamily.Horseshoe);var previous=p.armor[Target(p,shoe)];
            var successor=new Player{whoAmI=Main.myPlayer,active=true};Main.player[Main.myPlayer]=successor;Return(successor);Require(ReferenceEquals(p.armor[Target(p,shoe)],previous) && successor.armor.All(x=>x.IsAir),"same player index successor never receives old restoration");Call(owner,"EndSession");Require(!(bool)Get(owner,"HasPending"),"ended session has no cross-player recovery job");
            p=Reset();shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);Borrow(p,shoe,SmartFallFamily.Horseshoe);int at=Target(p,shoe);long oldGeneration=generation;
            try
            {
                generation++;Call(owner,"Restore",p,oldGeneration);Call(owner,"EndSession");
                Require(ReferenceEquals(p.armor[at],shoe) && !(bool)Get(owner,"HasPending") && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.Unconfirmed,"same-object generation replacement revokes old write eligibility and records actual unrecovered layout");
            }
            finally{generation=oldGeneration;}
        }
        private static void NativeSync()
        {
            var patch=new Harmony("JueMingR.Tests.SmartFallBorrowSync");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];int mode=Main.netMode;
            patch.Patch(send,postfix:new HarmonyMethod(typeof(NativeSmartFallBorrowChecks),nameof(Capture)));
            try
            {
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;
                var p=Reset();var shoe=Put(p.inventory,12,ItemID.LuckyHorseshoe);packets.Clear();Require(Borrow(p,shoe,SmartFallFamily.Horseshoe),"client exact exchange");
                int target=Target(p,shoe);Packet(PlayerItemSlotID.Inventory0+12,p.inventory[12]);Packet(PlayerItemSlotID.Armor0+target,p.armor[target]);
                packets.Clear();Return(p);Packet(PlayerItemSlotID.Inventory0+12,p.inventory[12]);Packet(PlayerItemSlotID.Armor0+target,p.armor[target]);
                Require(!(bool)Get(owner,"SyncUnconfirmed"),"both minimal native slot frames serialized without source/target ambiguity");
                Console.WriteLine("PASS SmartFallBorrow original message5 source/equipment apply and reverse serialization; no server acceptance or multiplayer rescue claim.");
            }
            finally{Main.netMode=mode;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;patch.Unpatch(send,HarmonyPatchType.All,patch.Id);packets.Clear();}
        }
        private static void FishingOverlap(object context)
        {
            if(owner!=null)Call(owner,"EndSession");
            var fixture=typeof(NativeFishingEquipmentChecks);var fishing=Get(context,"Fishing");var equipment=Get(fishing,"Equipment");
            foreach(var pair in new[]{new{Key="context",Value=context},new{Key="host",Value=fishing},new{Key="owner",Value=equipment},new{Key="tools",Value=Get(context,"Tools")},new{Key="input",Value=Get(context,"Input")}})
                fixture.GetField(pair.Key,Flags).SetValue(null,pair.Value);
            // Reuse the existing actual wet-bobber/native fishing fixture,
            // without running its unrelated G10 parent matrix.
            // Unlike the Item/selection component cases, this representative
            // executes full original Player.Update and actual wet bobber AI.
            // Reuse the existing native world/equipment setup, not the broad
            // G10 matrix. Steam achievements are the same unrelated outlets
            // isolated by the existing G09/G10 full-update fixtures.
            NativeToolExecutionChecks.Initialize(context,null);
            Main.LocalPlayer.fallStart=Main.LocalPlayer.fallStart2=(int)(Main.LocalPlayer.position.Y/16);
            var audit=new Harmony("JueMingR.Tests.SmartFallAfsOutlets");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeSmartFallBorrowChecks),nameof(SkipAfsAchievement)));
            try
            {
            var p=(Player)fixture.GetMethod("Reset",Flags).Invoke(null,null);
            p.fallStart=p.fallStart2=(int)(p.position.Y/16);
            var original=Put(p.armor,2,ItemID.CopperGreaves);var fishingLegs=Put(p.inventory,13,ItemID.AnglerPants);var rescue=Put(p.inventory,12,ItemID.DjinnsCurse);
            NativeFishingChecks.Save(fishing,new FishingOptions(equipment:true));
            for(int frame=0;frame<150 && (!ReferenceEquals(p.armor[2],fishingLegs) || !(bool)Get(Get(fishing,"Session"),"Active"));frame++)
                NativeFishingChecks.Step(context,Get(context,"Input"),new Microsoft.Xna.Framework.Vector2(850,718),frame==0,0);
            Require(ReferenceEquals(p.armor[2],fishingLegs) && ReferenceEquals(p.inventory[13],original),"actual fishing A->F journal and physical source are established");
            generation=(long)Get(Get(items,"Runtime"),"Generation");
            owner=Activator.CreateInstance(ownerType,Flags,null,new object[]{items,(Func<long>)(()=>generation),(Func<Item,SmartFallFamily,bool>)Match,equipment},null);
            int journal=(int)Get(equipment,"Count");
            var preparation=Preparation(p,rescue,SmartFallFamily.DjinnLegs);
            Require((bool)Get(preparation,"AvailableNow") && (bool)Get(preparation,"Displaced") && (bool)Get(preparation,"FishingEndpoint") && (int)Get(preparation,"BorrowWrites")==2 && ReferenceEquals(Get(preparation,"Target"),p.armor) && (int)Get(preparation,"TargetSlot")==2,"actual A-F-S preparation reports the unique shared fishing endpoint and displaced real F before any exchange");
            Require(Borrow(p,rescue,SmartFallFamily.DjinnLegs) && ReferenceEquals(p.armor[2],rescue) && ReferenceEquals(p.inventory[12],fishingLegs),"legs' unique valid endpoint permits exact F->S temporary overlap");
            NativeFishingChecks.Save(fishing,new FishingOptions());Call(equipment,"Update");
            Require((int)Get(equipment,"Count")==journal && ReferenceEquals(p.armor[2],rescue) && ReferenceEquals(p.inventory[13],original),"fishing OFF preserves its journal and cannot restore A over active S");
            Return(p);Require(ReferenceEquals(p.armor[2],fishingLegs) && ReferenceEquals(p.inventory[12],rescue),"rescue first returns S->F using the actual objects");
            fixture.GetMethod("Stop",Flags).Invoke(null,null);
            Require(ReferenceEquals(p.armor[2],original) && ReferenceEquals(p.inventory[13],fishingLegs) && ReferenceEquals(p.inventory[12],rescue) && (int)Get(equipment,"Count")==0,"fishing's current OFF intention then returns F->A, no erased journal or duplicated item");
            int animation=p.itemAnimation,time=p.itemTime,settle=0;
            for(;settle<120 && (p.itemAnimation!=0 || p.itemTime!=0);settle++)
                NativeFishingChecks.Step(context,Get(context,"Input"),new Microsoft.Xna.Framework.Vector2(850,718),false,0);
            Console.WriteLine("SMART-FALL AFS-UI native-settle="+settle+" before-animation="+animation+" before-time="+time+" animation="+p.itemAnimation+" time="+p.itemTime+" pending="+Main.LocalPlayerHasPendingInventoryActions()+" cursor="+Main.mouseItem.type);
            Require(p.itemAnimation==0 && p.itemTime==0 && !Main.LocalPlayerHasPendingInventoryActions() && Main.mouseItem.IsAir && p.inventory[19].IsAir,"actual native updates reach a lawful idle UI boundary with empty cursor and recovery slot");
            // Use this same real SessionPlayer for the actual native UI guard
            // boundary. The standalone Item/Player cases above are component
            // checks; this one exercises the installed mask/hand-off adapter.
            var world=Get(items,"World");var prior=(Action<Item[],int>)GetOptional(world,"BeforeManualFallSlot");
            Set(world,"BeforeManualFallSlot",(Action<Item[],int>)((array,slot)=>{prior?.Invoke(array,slot);Call(owner,"ManualEquipment",array,slot);}));
            try
            {
                Require(Borrow(p,rescue,SmartFallFamily.DjinnLegs),"manual guard fixture starts with a real loan");
                long revision=(long)Get(world,"Revision");Main.mouseLeft=false;Main.mouseLeftRelease=true;ItemSlot.LeftClick(p.armor,8,2);Require((bool)Get(owner,"HasPending") && (long)Get(world,"Revision")==revision,"native hover-style LeftClick with no physical press neither retires ownership nor invalidates item source hints");
                var twin=Put(p.inventory,18,ItemID.DjinnsCurse);revision=(long)Get(world,"Revision");
                Require(!rescue.IsNotTheSameAs(twin),"replacement is a distinct real object with the same original native signature");
                // Native action1 intentionally ignores an occupied same-signature
                // swap. A player can replace it by removing/storing S first,
                // then equipping the twin into the now-empty legal armor slot.
                Click(p.armor,8,2);Require(ReferenceEquals(Main.mouseItem,rescue) && p.armor[2].IsAir,"native player click removes the exact borrowed object before replacement");
                Click(p.inventory,0,19);Click(p.inventory,0,18);Click(p.armor,8,2);
                Return(p);Require(!(bool)Get(owner,"HasPending") && (SmartFallResult)Get(owner,"ReturnResult")==SmartFallResult.TakenOver && ReferenceEquals(p.armor[2],twin) && ReferenceEquals(p.inventory[19],rescue),"real native same-signature manual exchange is permitted and cannot be replaced by old restoration");
                Require((long)Get(world,"Revision")>revision,"actual native manual inventory/equipment transfers invalidate source hints even with Items automatic processing off");
                GamepadBoundary(p);
                GetItemRevision(p,world);
            }
            finally{Set(world,"BeforeManualFallSlot",prior);Main.mouseLeft=false;}
            Console.WriteLine("PASS SmartFallBorrow real wet-bobber A->F->S, fishing OFF waits, exact S->F then F->A; only the actual shared legs endpoint coordinates.");
            }
            finally{foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void Click(Item[] array,int context,int slot)
        {Main.mouseLeft=Main.mouseLeftRelease=true;ItemSlot.LeftClick(array,context,slot);Main.mouseLeft=false;}
        private static bool SkipAfsAchievement(){return false;}
        private static void GetItemRevision(Player p,object world)
        {
            var saved=p.inventory.ToArray();
            try
            {
                for(int i=0;i<50;i++){p.inventory[i]=new Item();p.inventory[i].SetDefaults(ItemID.StoneBlock);p.inventory[i].stack=p.inventory[i].maxStack;}
                var incoming=new Item();incoming.SetDefaults(ItemID.Shackle);int quantity=incoming.stack;long revision=(long)Get(world,"Revision");
                var settings=new GetItemSettings(LongText:false,NoText:true,CanGoIntoVoidVault:false,NoSound:true);
                var remainder=p.GetItem(incoming,settings);
                Require(ReferenceEquals(remainder,incoming) && remainder.stack==quantity && (long)Get(world,"Revision")==revision,"native GetItem with full normal inventory and no actual gain leaves the source revision unchanged");
                p.inventory[23]=new Item();remainder=p.GetItem(incoming,settings);
                Require(remainder.IsAir && p.inventory[23].type==ItemID.Shackle && p.inventory[23].stack==quantity && (long)Get(world,"Revision")>revision,"native GetItem with a real empty slot acquires the item and advances the source revision");
                Console.WriteLine("PASS SmartFallBorrow native GetItem hints: full-inventory no-op retains revision, actual empty-slot gain advances it; no Void, text or sound.");
            }
            finally{for(int i=0;i<saved.Length;i++)p.inventory[i]=saved[i];}
        }
        private static void GamepadBoundary(Player p)
        {
            var guard=ownerType.Assembly.GetType("JueMingR.TerrariaHost.Items.ItemPendingGuards",true).GetMethod("Gamepad",Flags);
            bool allowed=PlayerInput.AllowExecutionOfGamepadInstructions;
            bool grapple=PlayerInput.Triggers.JustPressed.Grapple,smart=PlayerInput.Triggers.JustPressed.SmartCursor;
            bool Prefix(){object[] args={p.armor,8,2,null};return (bool)guard.Invoke(null,args);}
            try
            {
                var selected=Put(p.inventory,20,ItemID.DjinnsCurse);var original=p.armor[2];Require(Borrow(p,selected,SmartFallFamily.DjinnLegs),"gamepad local guard fixture owns the real two endpoints");
                PlayerInput.AllowExecutionOfGamepadInstructions=true;PlayerInput.Triggers.JustPressed.Grapple=PlayerInput.Triggers.JustPressed.SmartCursor=false;
                Require(!Prefix() && (bool)Get(owner,"HasPending"),"mere native gamepad instruction query does not retire ownership");
                PlayerInput.AllowExecutionOfGamepadInstructions=false;PlayerInput.Triggers.JustPressed.SmartCursor=true;
                Require(!Prefix() && (bool)Get(owner,"HasPending"),"an edge without native command execution permission does not retire ownership");
                PlayerInput.AllowExecutionOfGamepadInstructions=true;
                Require(Prefix() && !(bool)Get(owner,"HasPending") && ReferenceEquals(p.armor[2],selected) && ReferenceEquals(p.inventory[20],original),"real SmartCursor edge and native execution permission hand the exact endpoint to the player without moving either object");
                selected=Put(p.inventory,21,ItemID.DjinnsCurse);original=p.armor[2];PlayerInput.Triggers.JustPressed.SmartCursor=false;
                Require(Borrow(p,selected,SmartFallFamily.DjinnLegs),"second gamepad edge fixture");PlayerInput.Triggers.JustPressed.Grapple=true;
                Require(Prefix() && !(bool)Get(owner,"HasPending") && ReferenceEquals(p.armor[2],selected) && ReferenceEquals(p.inventory[21],original),"real Grapple command edge likewise releases only its intersecting equipment owner");
                Console.WriteLine("PASS SmartFallBorrow installed gamepad guard prefix: no-edge/disabled execution retain, SmartCursor/Grapple true command edges hand over exact real objects. Full gamepad UI body not executed.");
            }
            finally{PlayerInput.AllowExecutionOfGamepadInstructions=allowed;PlayerInput.Triggers.JustPressed.Grapple=grapple;PlayerInput.Triggers.JustPressed.SmartCursor=smart;}
        }
        private static void Packet(int slot,Item item)
        {
            var packet=packets.LastOrDefault(x=>BitConverter.ToInt16(x,4)==slot);
            Require(packet!=null && BitConverter.ToInt16(packet,6)==item.stack && packet[8]==item.prefix && BitConverter.ToInt16(packet,9)==item.type && ((packet[11]&1)!=0)==item.favorited,"native packet5 exact physical slot "+slot);
        }
        private static void Capture(int __0)
        {if(__0==5 && Main.netMode==1){var bytes=NetMessage.buffer[256].writeBuffer;packets.Add(bytes.Take(BitConverter.ToUInt16(bytes,0)).ToArray());}}
        private static void Pick(Player __instance,ref int __0,ref bool __result)
        {if(owner==null)return;object[] args={__instance,__0,__result};ownerType.GetMethod("Pick",Flags).Invoke(owner,args);__0=(int)args[1];__result=(bool)args[2];}
    }
}
