using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Processing;
using JueMingR.Platform.Items;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingFaultChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool throwGrant,throwPayment,throwPopup;
        internal static void Run(object context)
        {
            var host=Get(context,"Processing");var input=Get(context,"Input");var p=Main.LocalPlayer;var settings=(ProcessingSettings[])Get(host,"Settings");
            var ownership=(ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(9);p.inventory[12].SetDefaults(4345);p.inventory[12].stack=20;
            Main.playerInventory=true;p.mouseInterface=false;Main.mouseItem.TurnToAir();p.itemTime=p.itemAnimation=0;
            NativeProcessingUiChecks.Save(settings[0],new ProcessingOptions(true));
            var audit=new Harmony("JueMingR.Tests.ProcessingFaults");var getItem=typeof(Player).GetMethods(Flags).Single(m=>m.Name=="GetItem" && m.GetParameters().Length==2);
            audit.Patch(getItem,postfix:new HarmonyMethod(typeof(NativeProcessingFaultChecks),nameof(AfterGrant)));
            try
            {
                throwGrant=true;NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
                Require(!throwGrant && p.inventory.Any(i=>i.type==2002 && i.stack>0) && p.inventory[12].stack==20,"injected exception follows real grant and precedes bag debit");
                ulong mask=ownership.ProtectedSlots;Require((mask&(1UL<<12))!=0 && mask!=((1UL<<58)-1) && ownership.SaleBlocked,"unknown source holds actual bag/affected stock, not all inventory");
                int stock=p.inventory.Where(i=>i.type==2002).Sum(i=>i.stack);
                for(int i=0;i<10;i++){NativeProcessingChecks.Sample(input,i%2==0);Call(context,"UpdateRuntime");}
                NativeProcessingUiChecks.Save(settings[0],new ProcessingOptions(false));NativeProcessingUiChecks.Save(settings[0],new ProcessingOptions(true));
                NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
                Require(p.inventory[12].stack==20 && stock==p.inventory.Where(i=>i.type==2002).Sum(i=>i.stack),"unknown grant cannot replay through release/enable cycles");
                var world=Main.ActiveWorldFileData;Main.ActiveWorldFileData=null;Call(context,"UpdateRuntime");Main.ActiveWorldFileData=world;Call(context,"UpdateRuntime");
                Require(ownership.ProtectedSlots==mask,"temporary same-world identity absence retains unknown protection");
            }
            finally{audit.Unpatch(getItem,HarmonyPatchType.All,audit.Id);throwGrant=false;}
            FreshWorld(context,input);
            Require(ownership.ProtectedSlots==0 && GetOptional(host,"Error")==null,"positive new native world retires old unknown resources and stale warning");
            NativeProcessingUiChecks.Save(settings[0],new ProcessingOptions());
            NativeProcessingFlowChecks.InitializeShop();Main.npc[0].SetDefaults(107);Main.npc[0].active=true;Main.npc[0].homeless=true;p.SetTalkNPC(0);Main.playerInventory=Main.InReforgeMenu=true;
            foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            p.bank.item[0]=NativeCoinChecks.Coin(74,4);Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(53);p.currentShoppingSettings.PriceAdjustment=1f;
            NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(i=>Lang.prefix[i].Value).Distinct()));
            var owner=Get(host,"Reforge");long fee=(long)Main.reforgeItem.value/3,before=NativeReforgeChecks.Total(p);typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);
            var buy=typeof(Player).GetMethod("BuyItem",new[]{typeof(long),typeof(int)});audit.Patch(buy,postfix:new HarmonyMethod(typeof(NativeProcessingFaultChecks),nameof(AfterPayment)));
            try
            {
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,fee);
                throwPayment=true;NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                Require(!throwPayment && before-NativeReforgeChecks.Total(p)==fee && Main.reforgeItem.prefix==0,"post-payment exception keeps actual bank debit/change, never free roll or refund");
                before=NativeReforgeChecks.Total(p);
                for(int i=0;i<10;i++){NativeReforgeChecks.Sample(input,i%2==0);Call(owner,"Observe",true,120,310,15,fee);Call(owner,"Update");}
                NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(false));NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(true,new[]{Lang.prefix[62].Value}));
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                Require(before==NativeReforgeChecks.Total(p) && (bool)Get(owner,"unknown"),"unknown payment cannot retry after new presses or list/toggle edits");
            }
            finally{audit.Unpatch(buy,HarmonyPatchType.All,audit.Id);throwPayment=false;}
            FreshWorld(context,input);Main.reforgeItem.ResetPrefix();p.bank.item[0]=NativeCoinChecks.Coin(74,4);
            NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(i=>Lang.prefix[i].Value).Distinct()));
            var popup=typeof(PopupText).GetMethod("NewText",new[]{typeof(PopupTextContext),typeof(Item),typeof(Microsoft.Xna.Framework.Vector2),typeof(int),typeof(bool),typeof(bool)});
            audit.Patch(popup,postfix:new HarmonyMethod(typeof(NativeProcessingFaultChecks),nameof(AfterPopup)));PopupText.ClearAll();
            try
            {
                typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);fee=(long)Main.reforgeItem.value/3;before=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,fee);throwPopup=true;
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                Require(!throwPopup && Main.reforgeItem.prefix!=0 && before-NativeReforgeChecks.Total(p)==fee && (bool)Get(owner,"unknown") && !(bool)Get(owner,"Executing"),"post-prefix feedback exception retains changed prefix and paid bank debit, closes execution lease");
                int prefix=Main.reforgeItem.prefix;before=NativeReforgeChecks.Total(p);
                for(int i=0;i<12;i++){NativeReforgeChecks.Sample(input,i%2==0);Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);Call(owner,"Update");}
                NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(false));NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions(true,new[]{Lang.prefix[62].Value}));
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Require(before==NativeReforgeChecks.Total(p) && prefix==Main.reforgeItem.prefix && ownership.SaleBlocked,"feedback fault cannot replay or refund after release, list and toggle changes");
            }
            finally{audit.Unpatch(popup,HarmonyPatchType.All,audit.Id);throwPopup=false;}
            Main.InReforgeMenu=false;p.SetTalkNPC(-1);FreshWorld(context,input);NativeProcessingUiChecks.Save(settings[2],new ProcessingOptions());Main.playerInventory=false;
            Console.WriteLine("PASS G08 unknowns: post-native grant/payment faults retain real effects and exact protection, no replay/refund, same identity retains and new identity retires.");
        }
        private static void AfterGrant(){if(throwGrant){throwGrant=false;throw new InvalidOperationException("isolated-after-native-grant");}}
        private static void AfterPayment(bool __result){if(throwPayment && __result){throwPayment=false;throw new InvalidOperationException("isolated-after-native-payment");}}
        private static void AfterPopup(Item __1){if(throwPopup && ReferenceEquals(__1,Main.reforgeItem)){throwPopup=false;throw new InvalidOperationException("isolated-after-native-prefix-feedback");}}
        private static void FreshWorld(object context,object input)
        {
            NativeProcessingChecks.Sample(input,false);Main.gamePaused=true;
            try{Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath,Guid.NewGuid().ToString("N")+".wld"),false){UniqueId=Guid.NewGuid()};Call(context,"UpdateRuntime");}
            finally{Main.gamePaused=false;}
        }
    }
}
