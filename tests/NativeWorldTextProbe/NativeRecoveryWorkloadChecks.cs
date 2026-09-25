using System;
using System.Linq;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryWorkloadChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"Recovery"),input=Get(context,"Input"),potions=Get(host,"PotionsUse"),buffs=Get(host,"BuffUse"),furniture=Get(host,"Furniture"),nurse=Get(host,"Nurse"),tax=Get(host,"Tax");
            var ps=(RecoverySettings)Get(host,"Potions");var bs=(RecoverySettings)Get(host,"Buffs");var ss=(RecoverySettings)Get(host,"Services");var p=Main.LocalPlayer;
            p.SetTalkNPC(-1);Main.npcChatText="";p.statLife=300;p.potionDelay=p.manaPotionDelay=0;p.taxMoney=500;p.statManaMax2=200;p.statMana=19;
            p.inventory[0].SetDefaults(112);p.inventory[0].mana=20;p.selectedItemState.Select(0);p.selectedItemState.Update();p.manaCost=1;
            Action<ulong> frames=start=>{for(ulong t=start;t<start+600;t++){Call(Get(context,"worldTiles"),"BeginTick");NativeRecoveryChecks.Frame(host,input,t);}};
            Func<long[]> counts=()=>new[]{Count(potions,"CandidateReads"),Count(potions,"NativeCalls"),Count(buffs,"DefinitionReads"),Count(buffs,"CandidateReads"),Count(buffs,"NativeCalls"),Count(furniture,"TileReads"),Count(furniture,"ValidationReads"),Count(furniture,"NativeCalls"),Count(Get(nurse,"Target"),"Reads"),Count(nurse,"Quotes"),Count(nurse,"NativeCalls"),Count(Get(tax,"Target"),"Reads"),Count(tax,"NativeCalls")};
            NativeRecoveryChecks.Save(ps,new RecoveryOptions());NativeRecoveryChecks.Save(bs,new RecoveryOptions());NativeRecoveryChecks.Save(ss,new RecoveryOptions());
            NativeRecoveryChecks.Frame(host,input,4000);Require((bool)Call(host,"Admit",p),"OFF counters are measured in a genuinely admitted session");
            long[] before=counts();frames(4001);Require(before.SequenceEqual(counts()),"all OFF skips actual inventory/definition/tile/NPC/payment work for 600 updates");
            NativeRecoveryChecks.Save(ps,new RecoveryOptions(1,mana:true));p.statLife=500;p.statMana=20;long reads=Count(potions,"CandidateReads"),uses=Count(potions,"NativeCalls");frames(4700);
            Require(reads==Count(potions,"CandidateReads") && uses==Count(potions,"NativeCalls"),"full life and exact actual mana cost avoid inventory scans");
            p.statLife=300;p.statMana=19;p.potionDelay=p.manaPotionDelay=100;frames(5400);
            Require(reads==Count(potions,"CandidateReads") && uses==Count(potions,"NativeCalls"),"real active cooldown returns before candidate scans");
            p.inventory[6].SetDefaults(ItemID.ManaPotion);p.inventory[6].stack=2;p.manaPotionDelay=0;NativeRecoveryChecks.Frame(host,input,6001);
            Require(p.inventory[6].stack==1 && Count(potions,"NativeCalls")==uses+1 && Count(potions,"CandidateReads")-reads<=98,"positive control performs one bounded native refill after cooldown");
            NativeRecoveryChecks.Save(ps,new RecoveryOptions());NativeRecoveryChecks.Save(bs,new RecoveryOptions(buffs:true,allowedBuffs:Enumerable.Range(80000,1000)));
            long definitions=Count(buffs,"DefinitionReads");reads=Count(buffs,"CandidateReads");frames(6100);
            Require(Count(buffs,"DefinitionReads")-definitions<=20000 && Count(buffs,"CandidateReads")==reads,"stable long whitelist avoids per-frame full definition traversal");
            foreach(var item in p.inventory)item.TurnToAir();foreach(var item in p.bank4.item)item.TurnToAir();Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
            NativeRecoveryChecks.Save(bs,new RecoveryOptions(buffs:true,allowedBuffs:new int[]{ItemID.IronskinPotion}));reads=Count(buffs,"CandidateReads");uses=Count(buffs,"NativeCalls");frames(6800);
            Require(Count(buffs,"CandidateReads")-reads<=20*98 && Count(buffs,"NativeCalls")==uses,"missing provider has finite interval scans and no native retries");
            NativeRecoveryChecks.Save(bs,new RecoveryOptions());NativeRecoveryChecks.Save(ss,new RecoveryOptions(furniture:true));
            foreach(int buff in new[]{29,93,150,159,348,192,366})p.AddBuff(buff,108000);reads=Count(furniture,"TileReads");long validation=Count(furniture,"ValidationReads");frames(7500);
            Require(Count(furniture,"TileReads")==reads && Count(furniture,"ValidationReads")==validation,"all seven current furniture effects skip discovery and full-object validation");
            NativeRecoveryChecks.Save(ss,new RecoveryOptions());p.taxMoney=0;
            Console.WriteLine("PASS G07 workload: admitted OFF, exact need, real cooldown, positive refill, 1000-entry stable whitelist, missing provider and satisfied furniture.");
        }
        private static long Count(object owner,string name){return (long)Get(owner,name);}
    }
}
