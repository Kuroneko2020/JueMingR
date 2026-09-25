using System;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryServiceChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"Recovery");var input=Get(context,"Input");var s=(RecoverySettings)Get(host,"Services");var p=Main.LocalPlayer;
            Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();Main.ShopHelper=new Terraria.GameContent.ShopHelper();
            Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
            var panel=typeof(Main).GetField("_newChatPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            panel.SetValue(Main.instance,Activator.CreateInstance(panel.FieldType,true));
            var achievements=new Terraria.Achievements.AchievementManager();
            var frequent=new Terraria.Achievements.Achievement("FREQUENT_FLYER");frequent.AddCondition(Terraria.GameContent.Achievements.CustomFloatCondition.Create("Pay",10000f));achievements.Register(frequent);
            typeof(Main).GetField("_achievements",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(Main.instance,achievements);
            foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
            for(int i=0;i<Main.maxNPCs;i++)Main.npc[i]=new NPC();
            var nurse=new NPC();nurse.SetDefaults(18);nurse.whoAmI=0;nurse.active=true;nurse.position=p.position+new Microsoft.Xna.Framework.Vector2(30,0);Main.npc[0]=nurse;
            p.statLifeMax2=500;p.statLife=400;p.mouseInterface=false;p.SetTalkNPC(-1);p.tileInteractionHappened=false;p.stinky=false;
            NativeRecoveryChecks.Save(s,new RecoveryOptions(nurse:true));Call(Get(context,"nativeNpcs"),"BeginTick");
            NativeRecoveryChecks.Frame(host,input,800);Require(p.statLife==400 && p.talkNPC==-1,"known unaffordable nurse never opens dialog");
            p.bank4.item[0]=NativeCoinChecks.Coin(73,1);long total=Total(p);
            NativeRecoveryChecks.Frame(host,input,900);
            Require(p.statLife==500 && Total(p)<total && p.talkNPC==-1,"native nurse pays from inaccessible void bank and closes only success: error="+GetOptional(host,"Error")+"; native="+GetOptional(Get(host,"Nurse"),"LastFailure"));
            p.AddBuff(BuffID.Poisoned,600);total=Total(p);NativeRecoveryChecks.Frame(host,input,1000);
            Require(p.FindBuffIndex(BuffID.Poisoned)<0 && Total(p)<total,"full-life curable debuff triggers native paid nurse removal");
            p.bank.item[0]=NativeCoinChecks.Coin(74,9999);p.bank2.item[0]=NativeCoinChecks.Coin(74,9999);
            p.statLife=400;total=Total(p);NativeRecoveryChecks.Frame(host,input,1060);
            Require(p.statLife==500 && Total(p)<total && p.talkNPC==-1 && !(bool)Get(Get(host,"Nurse"),"unknown"),"wealth beyond native affordability cap still confirms one exact payment and closes owned dialog");
            var tax=new NPC();tax.SetDefaults(441);tax.whoAmI=1;tax.active=true;tax.position=p.position+new Microsoft.Xna.Framework.Vector2(-30,0);Main.npc[1]=tax;
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem();
            p.taxMoney=12345;total=Total(p);NativeRecoveryChecks.Save(s,new RecoveryOptions(tax:true));Call(Get(context,"nativeNpcs"),"BeginTick");
            NativeRecoveryChecks.Frame(host,input,1100);
            long dropped=WorldCoins();Require(p.taxMoney==0 && dropped>0 && Total(p)==total && p.talkNPC==-1,"native tax emits world coins without wallet injection and closes successful dialog");
            for(ulong tick=1101;tick<1250;tick++)NativeRecoveryChecks.Frame(host,input,tick);
            Require(WorldCoins()==dropped,"unpicked tax world coins never duplicate");
            NativeRecoveryChecks.Save(s,new RecoveryOptions());
            Console.WriteLine("PASS G07 nurse: no-funds/no-dialog, actual four-bank payment and native healing/debuff removal.");
        }
        internal static long Total(Player p){long sum=NativeCoinChecks.Total(p.inventory,54);for(int a=0;a<4;a++)sum+=NativeCoinChecks.Total(NativeCoinChecks.Bank(p,a).item,40);return sum;}
        private static long WorldCoins(){long sum=0;for(int i=0;i<Main.maxItems;i++)if(Main.item[i].active){var item=Main.item[i];long value;if(JueMingR.Features.CoinDeposit.CoinRules.TryValue(item.type,item.stack,out value))sum+=value;}return sum;}
    }
}
