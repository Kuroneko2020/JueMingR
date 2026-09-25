using System;
using System.Reflection;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryBuffChecks
    {
        internal static void Run(object context)
        {
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Require(Main.debuff[BuffID.Poisoned] && Main.meleeBuff[BuffID.WeaponImbuePoison],"real native buff classification initialized");
            object host=Get(context,"Recovery"),input=Get(context,"Input");var s=(RecoverySettings)Get(host,"Buffs");
            NativeQuickItemChecks.Until(()=>{s.Poll();return s.Loaded;});
            var p=Main.LocalPlayer;Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
            foreach(var item in p.inventory)item.TurnToAir();p.statManaMax2=p.statMana=200;p.controlUseItem=p.channel=false;p.itemAnimation=p.itemTime=0;Main.mouseLeft=false;
            p.inventory[2].SetDefaults(ItemID.IronskinPotion);p.inventory[2].stack=3;p.inventory[3].SetDefaults(ItemID.RegenerationPotion);p.inventory[3].stack=3;
            NativeRecoveryChecks.Save(s,new RecoveryOptions(buffs:true,allowedBuffs:new int[]{ItemID.IronskinPotion,ItemID.RegenerationPotion}));
            NativeRecoveryChecks.Frame(host,input,10);NativeRecoveryChecks.Frame(host,input,11);
            Require((p.FindBuffIndex(BuffID.Ironskin)>=0) && (p.FindBuffIndex(BuffID.Regeneration)>=0) && p.inventory[2].stack==2 && p.inventory[3].stack==2,"two missing allowed buffs both finish through real QuickBuff");
            for(ulong t=12;t<40;t++)NativeRecoveryChecks.Frame(host,input,t);
            Require(p.inventory[2].stack==2 && p.inventory[3].stack==2,"existing buffs neither refresh nor duplicate consumption");
            p.ClearBuff(BuffID.Ironskin);p.ClearBuff(BuffID.Regeneration);p.buffImmune[BuffID.Ironskin]=true;
            for(ulong t=40;t<70;t++)NativeRecoveryChecks.Frame(host,input,t);
            Require(!(p.FindBuffIndex(BuffID.Ironskin)>=0) && (p.FindBuffIndex(BuffID.Regeneration)>=0) && p.inventory[2].stack==2,"immune A cannot consume or starve missing B");
            p.buffImmune[BuffID.Ironskin]=false;
            NativeRecoveryChecks.Save(s,new RecoveryOptions(followAdd:true,followRemove:true));
            p.ClearBuff(BuffID.Ironskin);p.inventory[0].SetDefaults(ItemID.IronskinPotion);p.inventory[0].stack=1;
            p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;
            NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;
            for(int frame=0;frame<40 && p.inventory[0].type!=0;frame++)NativeQuickItemChecks.NativeFrame(p);
            Require(p.FindBuffIndex(BuffID.Ironskin)>=0 && p.inventory[0].IsAir,"native manual last potion really applied and consumed");
            Call(host,"Poll");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !s.Busy;});
            Require(s.Value.BuffAllowed(ItemID.IronskinPotion),"follow learns original type of last consumed manual potion while automatic buff is off");
            p.ClearBuff(BuffID.Ironskin);Call(host,"Poll");Require(s.Value.BuffAllowed(ItemID.IronskinPotion),"natural/generic removal does not unlearn");
            p.AddBuff(BuffID.Regeneration,3600);Call(host,"Poll");Require(!s.Value.BuffAllowed(ItemID.RegenerationPotion),"unrelated AddBuff is not manual learning");
            NativeRecoveryChecks.Save(s,new RecoveryOptions());
            Console.WriteLine("PASS G07 buffs: real multi-target completion, existing effects, immune provider and independent progress.");
        }
    }
}
