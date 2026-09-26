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
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");
                owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                long initialFee=(long)Main.reforgeItem.value/3,initialMoney=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,initialFee);
                Require(!(bool)Call(owner,"NativePayment",p,initialFee,-1) && initialMoney==NativeReforgeChecks.Total(p),"first press with a late quote is owned before native Draw payment");
                NativeReforgeChecks.Sample(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                Require(initialMoney-NativeReforgeChecks.Total(p)==initialFee && Main.reforgeItem.prefix!=0,"first held gesture proceeds on the next Update without another click");
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                initialFee=(long)Main.reforgeItem.value/3;initialMoney=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                NativeReforgeChecks.Sample(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                Call(owner,"Observe",true,120,310,15,initialFee);
                Require(!(bool)Call(owner,"NativePayment",p,initialFee,-1) && initialMoney==NativeReforgeChecks.Total(p),"frame skip late first Draw still owns the original press without a native debit");
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                Require(initialMoney-NativeReforgeChecks.Total(p)==initialFee && Main.reforgeItem.prefix!=0,"multiple Updates before the first Draw do not require a second click");
                foreach(int change in Enumerable.Range(0,4))
                {
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                    owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                    NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                    float oldScale=Main.UIScale;int oldWidth=(int)Terraria.GameInput.PlayerInput.OriginalScreenSize.X;
                    if(change==0)NativeReforgeChecks.Sample(input,false);
                    else
                    {
                        if(change==1)Main.UIScale=oldScale+0.5f;
                        if(change==2)typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,oldWidth+10);
                        if(change==3)Main.reforgeItem.Prefix(62);
                        NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                        Main.UIScale=oldScale;typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,oldWidth);Main.reforgeItem.ResetPrefix();
                    }
                    long before=NativeReforgeChecks.Total(p);
                    Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);Call(owner,"Update");
                    Require(!(bool)Get(owner,"owned") && before==NativeReforgeChecks.Total(p),"pending press cannot survive release or a changed-then-restored geometry/prefix: "+change);
                }
                foreach(int change in Enumerable.Range(0,5))
                {
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                    NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
                    owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                    NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                    string language=Language.ActiveCulture.Name;
                    if(change==0)Main.reforgeItem=Main.reforgeItem.Clone();
                    if(change==1){Main.npc[1].SetDefaults(107);Main.npc[1].active=true;p.SetTalkNPC(1);}
                    if(change==2)NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[62].Value}));
                    if(change==3)LanguageManager.Instance.SetLanguage(language=="zh-Hans"?"en-US":"zh-Hans");
                    if(change==4)Main.reforgeItem.Prefix(62);
                    long before=NativeReforgeChecks.Total(p);
                    Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);
                    Require(!(bool)Call(owner,"NativePayment",p,(long)Main.reforgeItem.value/3,-1),"late press identity change cannot leak a native debit: "+change);
                    NativeReforgeChecks.Sample(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                    Require(before==NativeReforgeChecks.Total(p),"late quote cannot adopt changed item/NPC/list/language/prefix: "+change);
                    p.SetTalkNPC(0);LanguageManager.Instance.SetLanguage(language);
                }
                NativeReforgeChecks.Sample(input,false,200,600);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
                NativeReforgeChecks.Sample(input,true,200,600);Call(owner,"Update");Call(owner,"Observe",false,120,310,15,(long)Main.reforgeItem.value/3);
                long outsideMoney=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.Sample(input,true);Main.mouseLeftRelease=false;Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);Call(owner,"Update");
                Require(outsideMoney==NativeReforgeChecks.Total(p),"pressing outside then sliding onto the button cannot start automation");
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
                int topSeed=0,topPrefix=0;
                for(;topSeed<512;topSeed++){Main.rand=new UnifiedRandom(topSeed);var probe=new Item();probe.SetDefaults(53);probe.Prefix(-2,out top);if(top){topPrefix=probe.prefix;break;}}
                Require(topPrefix>0,"real native generator supplies a top-tier result");
                int notTop=Main.reforgeItem.GetRollablePrefixes().First(id=>id!=topPrefix);
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[notTop].Value}));
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);
                for(int i=0;i<3;i++)
                {
                    long fee=(long)Main.reforgeItem.value/3,before=NativeReforgeChecks.Total(p);
                    Call(owner,"Observe",true,120,310,15,fee);Main.rand=new UnifiedRandom(topSeed);
                    NativeReforgeChecks.Sample(input,true);Main.mouseLeftRelease=i==0;Call(owner,"Update");
                    Require(Main.reforgeItem.prefix==topPrefix && before-NativeReforgeChecks.Total(p)==fee,"top-tier non-target result never pauses the next paid automatic Update: "+i);
                    Require((int)typeof(Main).GetField("reforgeCooldown",Flags).GetValue(null)==60,"automatic loop does not reset vanilla top-tier cooldown");
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
