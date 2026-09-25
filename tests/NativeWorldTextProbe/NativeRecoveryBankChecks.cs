using System;
using System.Reflection;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryBankChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"Recovery");var ownership=(ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");var p=Main.LocalPlayer;
            foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            p.bank4.item[0].SetDefaults(ItemID.IronskinPotion);p.bank4.item[0].stack=3;p.bank4.item[2]=NativeCoinChecks.Coin(73,1);
            var mask=new ulong[]{0,0,0,0,7};long session=ownership.Session;
            Require(ownership.TryBeginRecovery(session,mask,808),"synthetic pending bank ranges admitted");
            var refs=(Item[])p.bank4.item.Clone();p.inventory[50]=NativeCoinChecks.Coin(73,1);
            try
            {
                Require(ChestUI.MoveCoins(p.inventory,p.bank4)==0 && p.inventory[50].stack==1 && ReferenceEquals(refs[1],p.bank4.item[1]),"real MoveCoins protects target money and Air normalization");
                Require(!p.BuyItem(100),"ordinary native BuyItem respects protected bank payment range");
                p.chest=-5;p.inventory[10].SetDefaults(ItemID.IronskinPotion);p.inventory[10].stack=2;
                Require(!ChestUI.TryPlacingInChest(p.inventory,10,false,0) && p.bank4.item[0].stack==3,"shift store cannot merge into protected bank stack");
                ChestUI.LootAll();ChestUI.Restock();ChestUI.DepositAll();ChestUI.QuickStack();
                ItemSorting.SortInventory(p.bank4,false,false);
                Require(p.bank4.item[0].stack==3 && ReferenceEquals(refs[0],p.bank4.item[0]) && p.bank4.item[2].stack==1,"real bulk bank buttons and sort preserve protected range");
                var incoming=new Item();incoming.SetDefaults(ItemID.IronskinPotion);
                var method=typeof(Player).GetMethod("GetItem_FillIntoOccupiedSlot_VoidBag",BindingFlags.Instance|BindingFlags.NonPublic);
                Require(!(bool)method.Invoke(p,new object[]{p.bank4.item,incoming,GetItemSettings.LootAllFromBank,incoming,0}) && p.bank4.item[0].stack==3,"void pickup skips protected matching stack");
                p.chest=-2;ChestUI.TryPlacingInChest(p.inventory,10,false,0);Require(p.inventory[10].IsAir && p.bank.item[0].type==ItemID.IronskinPotion,"unrelated personal bank remains usable");
            }
            finally{ownership.EndRecovery(session,mask,808,false);p.chest=-1;}
            mask=new ulong[]{1UL<<10,0,0,0,0};Require(ownership.TryBeginRecovery(session,mask,809),"pending main medication admitted");
            p.inventory[10].SetDefaults(ItemID.IronskinPotion);p.inventory[10].stack=3;var original=p.inventory[10];p.chest=-3;
            try{ChestUI.DepositAll();Require(ReferenceEquals(p.inventory[10],original) && p.inventory[10].stack==3,"bank deposit cannot move protected main medication source");}
            finally{ownership.EndRecovery(session,mask,809,false);p.chest=-1;}
            Console.WriteLine("PASS G07 ownership: native bank coin/Air, manual payment, shift/bulk/sort, void receiver, main source and independent bank.");
        }
    }
}
