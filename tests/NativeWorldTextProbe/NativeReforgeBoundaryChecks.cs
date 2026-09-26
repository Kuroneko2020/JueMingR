using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Processing;
using Terraria;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeReforgeBoundaryChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var owner=Get(host,"Reforge");var targets=Get(owner,"Targets");var settings=((ProcessingSettings[])Get(host,"Settings"))[2];
            Main.playerInventory=Main.InReforgeMenu=true;p.SetTalkNPC(0);p.discountAvailable=false;p.currentShoppingSettings.PriceAdjustment=1;
            foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(53);p.bank.item[0]=NativeCoinChecks.Coin(74,3);
            foreach(var names in new[]{new string[0],new[]{"not-a-prefix"},new[]{Lang.prefix[81].Value}})
            {
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,names));
                long before=NativeReforgeChecks.Total(p);Press(owner,input);
                Require(before==NativeReforgeChecks.Total(p) && Main.reforgeItem.prefix==0,"empty/unknown/incompatible target never auto charges: "+string.Join("|",names));
                if(names.Length>0)Require(GetOptional(targets,"Message")!=null,"invalid target explains why it cannot run");
            }
            var saved=Main.rand;int chosen=0,seed=0;bool top;
            try
            {
                for(seed=0;seed<100;seed++){Main.rand=new UnifiedRandom(seed);var probe=new Item();probe.SetDefaults(53);probe.Prefix(-2,out top);if(!top && probe.prefix>0){chosen=probe.prefix;break;}}
                Require(chosen>0,"real prefix generator supplies repeatable non-top-tier result");
                Main.reforgeItem.ResetPrefix();Main.reforgeItem.Prefix(chosen);
                int other=Main.reforgeItem.GetRollablePrefixes().First(id=>id!=chosen);
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[other].Value}));
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");
                for(int i=0;i<3;i++)
                {
                    long fee=(long)Main.reforgeItem.value*Main.reforgeItem.stack/3,before=NativeReforgeChecks.Total(p);
                    Call(owner,"Observe",true,120,310,15,fee);Main.rand=new UnifiedRandom(seed);typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);
                    NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                    Require(Main.reforgeItem.prefix==chosen && before-NativeReforgeChecks.Total(p)==fee && !(bool)Get(owner,"unknown") && !(bool)Get(owner,"Executing"),"same-prefix result is a new paid native roll with real bank change");
                }
                foreach(int boundary in Enumerable.Range(0,4))
                {
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                    NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[other].Value}));
                    Press(owner,input,seed);long before=NativeReforgeChecks.Total(p);
                    if(boundary==0)Main.reforgeItem=Main.reforgeItem.Clone();
                    if(boundary==1){Main.npc[1].SetDefaults(107);Main.npc[1].active=true;p.SetTalkNPC(1);}
                    if(boundary==2)NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[chosen].Value}));
                    if(boundary==3)Main.InReforgeMenu=false;
                    for(int i=0;i<3;i++){Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);NativeReforgeChecks.Sample(input,true);Call(owner,"Update");}
                    Require(before==NativeReforgeChecks.Total(p),"item/NPC/list/menu change revokes existing hold: "+boundary);
                    p.SetTalkNPC(0);Main.InReforgeMenu=true;
                }
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[other].Value}));
                foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
                Press(owner,input);Require(NativeReforgeChecks.Total(p)==0 && Main.reforgeItem.prefix==0 && (bool)Get(owner,"tail"),"insufficient funds stop current hold without a free roll");
                Call(targets,"HasTargets",Main.reforgeItem);long builds=(long)Get(targets,"Resolutions");for(int i=0;i<1000;i++)Call(targets,"HasTargets",Main.reforgeItem);
                Require(builds==(long)Get(targets,"Resolutions"),"stable target list is not rescanned/sorted every frame");
                string culture=Language.ActiveCulture.Name;LanguageManager.Instance.SetLanguage("zh-Hans");
                Require(!(bool)Call(targets,"HasTargets",Main.reforgeItem) && GetOptional(targets,"Message")!=null,"language change invalidates cached localized target");LanguageManager.Instance.SetLanguage(culture);
            }
            finally{Main.rand=saved;}
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.InReforgeMenu=false;Main.playerInventory=false;p.SetTalkNPC(-1);NativeProcessingUiChecks.Save(settings,new ProcessingOptions());
            Console.WriteLine("PASS G08 reforge boundaries: whole compatible targets, repeated same-prefix paid rolls, bank change, insufficient money, identity revocations, language and stable-cache costs.");
        }
        private static void Press(object owner,object input,int? seed=null)
        {
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value*Main.reforgeItem.stack/3);
            if(seed.HasValue)Main.rand=new UnifiedRandom(seed.Value);typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);
            NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
        }
    }
}
