using System;
using System.Linq;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryPetChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"Recovery"),input=Get(context,"Input"),use=Get(host,"BuffUse");
            var s=(RecoverySettings)Get(host,"Buffs");var p=Main.LocalPlayer;
            foreach(var item in p.inventory)item.TurnToAir();Clear(p);p.potionDelay=0;p.silence=false;p.statMana=200;
            var carrot=p.inventory[2];carrot.SetDefaults(ItemID.Carrot);
            var fairy=p.inventory[3];fairy.SetDefaults(ItemID.FairyBell);
            Require(!carrot.consumable && !fairy.consumable && Main.vanityPet[carrot.buffType] && Main.lightPet[fairy.buffType] && fairy.buffTime==0,"native pet definitions include zero-duration fairy provider");
            Require(((int[])Call(Get(host,"Catalog"),"BuffCandidates")).Contains(ItemID.FairyBell),"actual zero-duration fairy provider appears in available list");
            bool edition=Main.runningCollectorsEdition;Main.runningCollectorsEdition=true;
            try
            {
                p.QuickBuff();Require(p.CountBuffs()==0,"ordinary native QuickBuff still excludes both pet groups");
                NativeRecoveryChecks.Save(s,new RecoveryOptions(buffs:true,allowedBuffs:new int[]{ItemID.Carrot,ItemID.FairyBell}));
                NativeRecoveryChecks.Frame(host,input,1000);NativeRecoveryChecks.Frame(host,input,1001);
                int light=FairyEffect(p);
                Require(p.FindBuffIndex(carrot.buffType)>=0 && light>0 && p.buffTime[p.FindBuffIndex(light)]==3600 && carrot.stack==1 && fairy.stack==1,"owned native QuickBuff grants both nonconsumable pets and actual default fairy duration");
                int anotherPet=ContentSamples.ItemsByType.Values.First(i=>i.type>0 && i.buffType>0 && Main.vanityPet[i.buffType] && i.buffType!=carrot.buffType && !i.summon).type;
                int anotherLight=ContentSamples.ItemsByType.Values.First(i=>i.type>0 && i.buffType>0 && Main.lightPet[i.buffType] && i.buffType!=27 && i.buffType!=101 && i.buffType!=102 && !i.summon).type;
                p.inventory[4].SetDefaults(anotherPet);p.inventory[5].SetDefaults(anotherLight);
                NativeRecoveryChecks.Save(s,new RecoveryOptions(buffs:true,allowedBuffs:new[]{anotherPet,anotherLight}));
                long before=(long)Get(use,"NativeCalls");NativeRecoveryChecks.Frame(host,input,1002);
                Require((long)Get(use,"NativeCalls")==before && p.FindBuffIndex(carrot.buffType)>=0 && FairyEffect(p)==light,"already present pet groups never replace each other");
                Clear(p);Main.runningCollectorsEdition=false;
                NativeRecoveryChecks.Save(s,new RecoveryOptions(buffs:true,allowedBuffs:new int[]{ItemID.Carrot}));
                NativeRecoveryChecks.Frame(host,input,1003);
                Require(p.CountBuffs()==0 && carrot.stack==1,"owned pet selection preserves real collector-edition refusal");
                NativeRecoveryChecks.Save(s,new RecoveryOptions());
            }
            finally{Main.runningCollectorsEdition=edition;Clear(p);}
            ManualFairy(context);NativeRecoveryChecks.Save(s,new RecoveryOptions());Clear(p);
            Console.WriteLine("PASS G07 pets: native nonconsumable/default duration, two-group preservation, CE permission and unchanged manual QuickBuff.");
        }
        internal static int ManualFairy(object context)
        {
            object host=Get(context,"Recovery"),input=Get(context,"Input");var s=(RecoverySettings)Get(host,"Buffs");var p=Main.LocalPlayer;
            Clear(p);foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(ItemID.FairyBell);
            p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;p.mouseInterface=false;
            NativeRecoveryChecks.Save(s,new RecoveryOptions(followAdd:true,followRemove:true,allowedBuffs:new int[]{1183})); // unrelated Wisp provider
            NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);
            var random=Main.rand;Main.rand=new Terraria.Utilities.UnifiedRandom(0);
            try
            {
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;
                for(int frame=0;frame<10 && FairyEffect(p)==0;frame++)NativeQuickItemChecks.NativeFrame(p);
            }
            finally{Main.rand=random;Main.mouseLeft=false;Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;p.controlUseItem=p.channel=false;}
            int effect=FairyEffect(p);Require(effect==101 || effect==102,"actual manual Fairy Bell applies a variant different from definition buff27");
            // Native DrawBuffIcon ignores the mouse while an item is still in
            // use. Finish the real released animation before attempting cancel.
            for(int frame=0;frame<120 && p.UsingOrReusingItem;frame++)NativeQuickItemChecks.NativeFrame(p);
            Require(!p.UsingOrReusingItem,"manual pet use actually finishes before a later icon gesture");
            Call(host,"Poll");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !s.Busy;});
            Require(s.Value.BuffAllowed(ItemID.FairyBell) && s.Value.BuffAllowed(1183),"manual variant effect learns its true fairy provider without confusing other light pets");
            return effect;
        }
        internal static int FairyEffect(Player p){return new[]{27,101,102}.FirstOrDefault(b=>p.FindBuffIndex(b)>=0);}
        private static void Clear(Player p){Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);}
    }
}
