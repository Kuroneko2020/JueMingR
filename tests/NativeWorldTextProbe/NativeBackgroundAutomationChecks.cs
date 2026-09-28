using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.CoinDeposit;
using JueMingR.Features.Items;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Full Host composition and actual native effects in isolated player/world
    // data. Only the OS foreground observation is replaced, never business gates.
    internal static class NativeBackgroundAutomationChecks
    {
        internal static void Run(object context)
        {RunCore(context,false);}
        internal static void RunF5(object context)
        {RunCore(context,true);}
        private static void RunCore(object context,bool f5)
        {
            object input=Get(context,"Input"),shell=Get(context,"Shell"),coins=Get(context,"CoinDeposit"),recovery=Get(context,"Recovery"),processing=Get(context,"Processing"),tools=Get(context,"Tools"),items=Get(tools,"Items");
            var foreground=Get(input,"foregroundWindow");bool updates=Main.CanUpdateGameplay;var p=Main.LocalPlayer;
            var potions=(RecoverySettings)Get(recovery,"Potions");var coinSettings=(CoinSettings)Get(coins,"Settings");
            try
            {
                typeof(Main).GetMethod("Initialize_TileAndNPCData1",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return coinSettings.Loaded && potions.Loaded && (bool)Call(processing,"Controls",1) && (bool)Call(tools,"Controls",1);});
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(f5?1:2)));FocusHelper.IsSelectedApplication=f5;Main.ToggleGameplayUpdates(true);Main.gamePaused=false;
                if(f5)NativeF5AutomationChecks.Open(context);
                if(!f5)
                {
                NativeQuickItemChecks.Sample(input,new Keys[0]);p.mouseInterface=true;
                Require(!(bool)Get(input,"CanStartActions") && !(bool)Get(input,"CanRetainIntent") && (bool)Get(input,"CanRunAutomaticActions"),"background automatic permission never grants physical input");
                Require((bool)Call(tools,"Admit",p,false) && (bool)Call(processing,"AdmitAutomatic",p) && (bool)Call(recovery,"Admit",p),"all autonomous host gates tolerate stale Draw mouse state");
                Require(!(bool)Call(processing,"Admit",p) && !(bool)Get(shell,"CanTargetInput"),"held bag/reforge and manual targets remain foreground only");
                foreach(bool paused in new[]{true,false})
                {
                    Main.gamePaused=paused;Main.ToggleGameplayUpdates(paused);
                    Require(!(bool)Get(input,"CanRunAutomaticActions") && !(bool)Call(processing,"AdmitAutomatic",p) && !(bool)Call(recovery,"Admit",p) && !(bool)Call(tools,"Admit",p,false),"pause or stopped gameplay blocks background operations");
                }
                Main.gamePaused=false;Main.ToggleGameplayUpdates(true);
                Call(Get(shell,"State"),"RestoreVisible");
                Require((bool)Get(shell,"CanAutomaticTargetInput") && (bool)Get(shell,"CanAutomaticProcessingInput") && !((Func<bool>)Get(tools,"CanFishingInterface"))(),"F5 restricts only automatic fishing actions");
                Call(Get(shell,"State"),"Close");
                object world=Get(items,"World");var manual=(HashSet<Item>)Get(world,"ManualMaterials");var held=new Item();held.SetDefaults(ItemID.DirtBlock);manual.Add(held);
                Call(world,"RefreshManualRelease");Require(manual.Contains(held),"background synthetic release never clears manual ownership");manual.Clear();
                Call(input,"BeginUpdate");Require(!(bool)Get(input,"CanRunAutomaticActions") && (bool)Get(input,"CanRetainAutomaticIntent"),"empty outer Update retains intent but cannot execute autonomous work");
                NativeQuickItemChecks.Sample(input,new Keys[0]);
                }

                NativeCoinMatrix.Reset(p,coins);p.mouseInterface=true;p.inventory[50]=NativeCoinChecks.Coin(ItemID.GoldCoin,3);p.bank.item[0]=NativeCoinChecks.Coin(ItemID.CopperCoin,1);Main.tile[40,40].type=29;
                Require(coinSettings.Set(true),"enable coin deposit");NativeQuickItemChecks.Until(()=>{Call(coins,"Poll");return !coinSettings.Busy;});
                NativeCoinMatrix.Tick(coins,0,180);
                Require(NativeCoinChecks.Total(p.inventory,58)==0 && NativeCoinChecks.Total(p.bank.item,40)==30001,"background native coin transfer conserves wallet and bank: "+Get(coins,"Status")+" wallet="+NativeCoinChecks.Total(p.inventory,58)+" bank="+NativeCoinChecks.Total(p.bank.item,40)+" gate="+Get(input,"CanRunAutomaticActions"));
                coinSettings.Set(false);NativeQuickItemChecks.Until(()=>{coinSettings.Poll();return !coinSettings.Busy;});

                p.inventory[12].SetDefaults(ItemID.DirtBlock);p.inventory[12].stack=7;p.trashItem.TurnToAir();
                Call(items,"Change",new ItemAutomationSettings(false,false,true,new int[0],new int[]{ItemID.DirtBlock},false));
                NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(Get(items,"Feature"),"Enabled");});
                for(int i=0;i<40;i++){NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");}
                Require(p.inventory[12].IsAir && p.trashItem.type==ItemID.DirtBlock && p.trashItem.stack==7,"background ordinary item automation reaches actual discard outlet");
                Call(items,"Change",new ItemAutomationSettings(false,false,false,new int[0],new int[0],false));NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return !(bool)Get(Get(items,"Feature"),"Enabled");});

                foreach(var item in p.inventory)item.TurnToAir();p.inventory[3].SetDefaults(ItemID.HealingPotion);p.inventory[3].stack=3;p.statLifeMax2=500;p.statLife=300;p.potionDelay=0;
                NativeRecoveryChecks.Save(potions,new RecoveryOptions(1));NativeRecoveryChecks.Frame(recovery,input,1000);
                Require(p.statLife==400 && p.inventory[3].stack==2 && p.potionDelay>0,"background native healing consumes once and establishes cooldown");
                p.inventory[0].SetDefaults(112);p.inventory[0].mana=20;p.selectedItemState.Select(0);p.selectedItemState.Update();p.inventory[6].SetDefaults(ItemID.ManaPotion);p.inventory[6].stack=3;p.statManaMax2=200;p.statMana=19;p.manaCost=1;p.manaPotionDelay=0;
                p.controlUseItem=p.channel=true;p.itemAnimation=12;p.itemTime=8;NativeRecoveryChecks.Save(potions,new RecoveryOptions(mana:true));NativeRecoveryChecks.Frame(recovery,input,1001);
                Require(p.statMana>19 && p.inventory[6].stack==2 && p.channel && p.itemAnimation==12,"background native mana preserves current casting state");
                NativeRecoveryChecks.Save(potions,new RecoveryOptions());p.itemAnimation=p.itemTime=0;p.channel=p.controlUseItem=false;
                NativeRecoveryFurnitureChecks.Run(context);p.mouseInterface=true;NativeRecoveryServiceChecks.Run(context);
                var services=(RecoverySettings)Get(recovery,"Services");NativeRecoveryChecks.Save(services,new RecoveryOptions(nurse:true,tax:true));
                p.mouseInterface=true;p.statLife=400;p.taxMoney=5000;long nurseCalls=(long)Get(Get(recovery,"Nurse"),"NativeCalls");Call(Get(context,"nativeNpcs"),"BeginTick");
                for(ulong tick=3000;tick<3010;tick++)NativeRecoveryChecks.Frame(recovery,input,tick);
                Require(p.statLife==500 && p.taxMoney==0 && (long)Get(Get(recovery,"Nurse"),"NativeCalls")==nurseCalls+1,"background service target and final payment gates tolerate stale Draw state");
                NativeRecoveryChecks.Save(services,new RecoveryOptions());
                Require((bool)Get(input,"CanStartActions")==f5,"native services preserve physical input permission");

                foreach(var npc in Main.npc)npc.active=false;foreach(var item in p.inventory)item.TurnToAir();p.selectedItemState.Select(0);p.selectedItemState.Update();p.mouseInterface=true;p.inventory[10].SetDefaults(424);p.inventory[10].stack=4;
                NativeExtractionChecks.Machine(42,40,219);Call(processing,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",1);});
                int products=Main.item.Count(x=>x.active);for(int i=0;i<180;i++)NativeExtractionChecks.Frame(context,input);
                Require(p.inventory[10].IsAir && Main.item.Count(x=>x.active)>products && p.selectedItem==0,"background actual extraction consumes materials, produces items and restores selection");
                Call(processing,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});
                Require(!f5 || (bool)Get(Get(shell,"State"),"Visible"),"F5 remains open across actual automatic work");
                Console.WriteLine("PASS "+(f5?"foreground F5":"background")+": native coin/item/heal/mana/furniture/nurse/tax/extraction effects and actual ownership safeguards.");
            }
            finally
            {Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;Main.ToggleGameplayUpdates(updates);Main.gamePaused=false;p.mouseInterface=false;}
        }
    }
}
