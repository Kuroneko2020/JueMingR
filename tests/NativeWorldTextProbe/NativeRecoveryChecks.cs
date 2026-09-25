using System;
using System.Linq;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"Recovery"), input=Get(context,"Input");
            Require(host!=null && (bool)Get(host,"Available"),"recovery production installed: "+GetOptional(host,"SetupError"));
            var settings=(RecoverySettings)Get(host,"Potions");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Loaded;});
            Require(settings.Ready && settings.Value.LifeMode==0 && !settings.Value.Mana,"recovery defaults off");
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();p.statLifeMax2=500;p.statLife=449;p.potionDelay=0;
            p.inventory[3].SetDefaults(188);p.inventory[3].stack=4; // native 100 life
            Save(settings,new RecoveryOptions(2));Frame(host,input,1);
            Require(p.statLife==500 && p.inventory[3].stack==3 && p.potionDelay>0,"smart 51: actual native heal, one consumption and cooldown");
            p.statLife=450;p.potionDelay=0;Frame(host,input,2);Require(p.statLife==450 && p.inventory[3].stack==3,"smart strict 50 refuses");
            p.inventory[4].SetDefaults(28);p.inventory[4].stack=3;p.statLife=425;Frame(host,input,3);
            Require(p.statLife==500 && p.inventory[3].stack==2 && p.inventory[4].stack==3,"smart deficit75 selects100 not50");
            p.potionDelay=0;p.statLife=400;Save(settings,new RecoveryOptions(2,noLife:new[]{188,28}));Frame(host,input,4);
            Require(p.statLife==400 && p.inventory[3].stack==2,"disabled catalogue types cannot fall through native selector");
            p.inventory[0].SetDefaults(112);p.inventory[0].mana=20;p.selectedItemState.Select(0);p.selectedItemState.Update();
            p.inventory[6].SetDefaults(Terraria.ID.ItemID.ManaPotion);p.inventory[6].stack=4;p.statManaMax2=200;p.statMana=19;p.manaCost=1;p.manaPotionDelay=0;
            Save(settings,new RecoveryOptions(mana:true));p.controlUseItem=true;p.channel=true;p.itemAnimation=12;p.itemTime=8;Main.mouseLeft=true;
            Frame(host,input,5,true);
            Require(p.statMana>19 && p.inventory[6].stack==3 && p.controlUseItem && p.channel && p.selectedItem==0 && p.itemAnimation==12,"continuous casting19/20 uses actual QuickMana without clearing attack or selecting potion");
            p.statMana=19;p.QuickMana();Require(p.inventory[6].stack==3,"same update native mana flower cannot drink twice");
            p.statMana=20;Frame(host,input,6);Require(p.inventory[6].stack==3,"20/20 costs met no potion");
            p.statMana=19;p.manaCost=.8f;Frame(host,input,7);Require(p.inventory[6].stack==3,"actual mana cost reduction");
            p.statManaMax2=10;p.statMana=0;p.manaCost=1;Frame(host,input,8);Frame(host,input,9);Require(p.inventory[6].stack==3,"impossible max mana does not drain potions");
            var red=(int[])Call(Get(host,"Catalog"),"Get",true);var blue=(int[])Call(Get(host,"Catalog"),"Get",false);
            Require(red.Contains(3001) && red.Contains(5496) && red.Contains(227) && !red.Contains(226) && !blue.Contains(3001),"current complete actual-type catalogue includes special and new potions without false duplicate");
            Save(settings,new RecoveryOptions());Console.WriteLine("PASS G07: real native healing/cooldown/consumption, licence, continuous mana cost and complete definition catalogue.");
            NativeRecoveryUiChecks.Run(context);
        }
        internal static void Save(RecoverySettings settings,RecoveryOptions value)
        {Require(settings.Set(value),"isolated recovery preference admitted");NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(settings.Ready,"isolated preference reliably committed");}
        internal static void Frame(object host,object input,ulong tick,bool attacking=false)
        {
            NativeQuickItemChecks.Sample(input,new Keys[0]);
            if(attacking){Main.mouseLeft=true;Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;}
            Call(host,"Update",tick);
        }
    }
}
