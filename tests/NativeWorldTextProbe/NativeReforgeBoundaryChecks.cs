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
            SharedTargets(context,host,input);
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
                NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,initialFee);
                Require(!(bool)Call(owner,"NativePayment",p,initialFee,-1) && initialMoney==NativeReforgeChecks.Total(p),"first press with a late quote is owned before native Draw payment");
                NativeReforgeChecks.SampleWorld(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                Require(initialMoney-NativeReforgeChecks.Total(p)==initialFee && Main.reforgeItem.prefix!=0,"first held gesture proceeds on the next Update without another click");
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                initialFee=(long)Main.reforgeItem.value/3;initialMoney=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                NativeReforgeChecks.SampleWorld(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                Call(owner,"Observe",true,120,310,15,initialFee);
                Require(!(bool)Call(owner,"NativePayment",p,initialFee,-1) && initialMoney==NativeReforgeChecks.Total(p),"frame skip late first Draw still owns the original press without a native debit");
                NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                Require(initialMoney-NativeReforgeChecks.Total(p)==initialFee && Main.reforgeItem.prefix!=0,"multiple Updates before the first Draw do not require a second click");
                foreach(int change in Enumerable.Range(0,4))
                {
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                    owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
                    NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                    float oldScale=Main.UIScale;int oldWidth=(int)Terraria.GameInput.PlayerInput.OriginalScreenSize.X;
                    if(change==0)NativeReforgeChecks.Sample(input,false);
                    else
                    {
                        if(change==1)Main.UIScale=oldScale+0.5f;
                        if(change==2)typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,oldWidth+10);
                        if(change==3)Main.reforgeItem.Prefix(62);
                        NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
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
                    NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                    string language=Language.ActiveCulture.Name;
                    if(change==0)Main.reforgeItem=Main.reforgeItem.Clone();
                    if(change==1){Main.npc[1].SetDefaults(107);Main.npc[1].active=true;p.SetTalkNPC(1);}
                    if(change==2)NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[62].Value}));
                    if(change==3)LanguageManager.Instance.SetLanguage(language=="zh-Hans"?"en-US":"zh-Hans");
                    if(change==4)Main.reforgeItem.Prefix(62);
                    long before=NativeReforgeChecks.Total(p);
                    Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);
                    Require(!(bool)Call(owner,"NativePayment",p,(long)Main.reforgeItem.value/3,-1),"late press identity change cannot leak a native debit: "+change);
                    NativeReforgeChecks.SampleWorld(input,true);Main.mouseLeftRelease=false;Call(owner,"Update");
                    Require(before==NativeReforgeChecks.Total(p),"late quote cannot adopt changed item/NPC/list/language/prefix: "+change);
                    p.SetTalkNPC(0);LanguageManager.Instance.SetLanguage(language);
                }
                NativeReforgeChecks.Sample(input,false,200,600);Call(owner,"Update");Main.reforgeItem.ResetPrefix();
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
                NativeReforgeChecks.SampleWorld(input,true,200,600);Call(owner,"Update");Call(owner,"Observe",false,120,310,15,(long)Main.reforgeItem.value/3);
                long outsideMoney=NativeReforgeChecks.Total(p);
                NativeReforgeChecks.SampleWorld(input,true);Main.mouseLeftRelease=false;Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);Call(owner,"Update");
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
                    NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
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
                    NativeReforgeChecks.SampleWorld(input,true);Main.mouseLeftRelease=i==0;Call(owner,"Update");
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
                    for(int i=0;i<3;i++){Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value/3);NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");}
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
        private static void SharedTargets(object context,object host,object input)
        {
            var owner=Get(host,"Reforge");var targets=Get(owner,"Targets");var settings=((ProcessingSettings[])Get(host,"Settings"))[2];var p=Main.LocalPlayer;
            var melee=new Item();melee.SetDefaults(Terraria.ID.ItemID.NightsEdge);var ranged=new Item();ranged.SetDefaults(Terraria.ID.ItemID.Minishark);
            var a=melee.GetRollablePrefixes().Select(id=>(int)id).ToArray();var b=ranged.GetRollablePrefixes().Select(id=>(int)id).ToArray();
            var saved=Main.rand;
            try
            {
                int[] types={melee.type,ranged.type};var allowed=new[]{a.Except(b).ToArray(),b.Except(a).ToArray()};var seeds=new int[2];var ids=new int[2];
                for(int index=0;index<2;index++)
                {
                    for(int seed=0;seed<4096;seed++)
                    {var probe=new Item();probe.SetDefaults(types[index]);Main.rand=new UnifiedRandom(seed);bool top;probe.Prefix(-2,out top);if(allowed[index].Contains(probe.prefix)){seeds[index]=seed;ids[index]=probe.prefix;break;}}
                    Require(ids[index]>0,"native roll supplies a target exclusive to each weapon category");
                }
                string[] names=ids.Select(id=>Lang.prefix[id].Value).ToArray();NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,names));p.bank.item[0]=NativeCoinChecks.Coin(74,10);
                for(int index=0;index<2;index++)
                {
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(types[index]);
                    FinalTargetHint(context,"双击输入词缀");Require(GetOptional(targets,"Message")==null,"mixed shared list is normal when at least one target is reachable");
                    long fee=(long)Main.reforgeItem.value*Main.reforgeItem.stack/3,before=NativeReforgeChecks.Total(p);Press(owner,input,seeds[index]);
                    Require(before-NativeReforgeChecks.Total(p)==fee && Main.reforgeItem.prefix==ids[index],"mixed list pays once through native BuyItem and stops on this weapon's reachable target");
                    long stopped=NativeReforgeChecks.Total(p);NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                    Require(NativeReforgeChecks.Total(p)==stopped && settings.Value.Names.SequenceEqual(names),"hit stop and switching weapons never modify the shared list");
                }
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{names[1]}));Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(types[0]);
                FinalTargetHint(context,"名单中没有适用于当前物品的词缀。");long empty=NativeReforgeChecks.Total(p);Press(owner,input);
                Require(NativeReforgeChecks.Total(p)==empty && Main.reforgeItem.prefix==0 && settings.Value.Names.SequenceEqual(new[]{names[1]}),"all unreachable targets refuse native automatic spending and retain the list");
                Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(Terraria.ID.ItemID.DirtBlock);FinalTargetHint(context,"当前物品无法重铸。");Press(owner,input);
                Require(NativeReforgeChecks.Total(p)==empty,"non-reforgeable item has an accurate reason and no automatic payment");
                Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(types[1]);FinalTargetHint(context,"双击输入词缀");
                Require(GetOptional(targets,"Message")==null,"slot switch clears the old unreachable final hint before another reforge press");
                long switchFee=(long)Main.reforgeItem.value*Main.reforgeItem.stack/3;Press(owner,input,seeds[1]);
                Require(empty-NativeReforgeChecks.Total(p)==switchFee && Main.reforgeItem.prefix==ids[1] && settings.Value.Names.SequenceEqual(new[]{names[1]}),"the same saved target becomes usable after switching items and stops on a native paid hit");
                Main.reforgeItem=new Item();FinalTargetHint(context,"双击输入词缀");
                Console.WriteLine("PASS Reforge shared targets: mixed weapons, native debit/roll/hit stop, all-unreachable and non-reforgeable refusal, unchanged list, slot-change final Hint refresh, empty-slot guidance.");
            }
            finally{Main.rand=saved;NativeReforgeChecks.Sample(input,false);Call(owner,"Update");NativeProcessingUiChecks.Save(settings,new ProcessingOptions());}
        }
        private static void FinalTargetHint(object context,string expected)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var ui=Get(shell,"ReforgeUi");var renderer=Get(shell,"renderer");
            Call(state,"Navigate",1);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            var input=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            foreach(var field in new[]{Tuple.Create("Width",(object)960f),Tuple.Create("Height",(object)760f),Tuple.Create("Scale",(object)1f),Tuple.Create("X",(object)(-10f)),Tuple.Create("Y",(object)(-10f)),Tuple.Create("Active",(object)true),Tuple.Create("Focused",(object)true)})Set(input,field.Item1,field.Item2);
            Call(state,"Update",input);NativeProcessingUiChecks.Prepare(context,960,760,1);
            NativeQuickUiChecks.FinalHint(shell,Get(NativeProcessingUiChecks.Parts(ui).Single(part=>(int)Get(part,"Command")==1),"Element"),expected);
            Call(state,"Close");Call(ui,"Suspend");
        }
        private static void Press(object owner,object input,int? seed=null)
        {
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,(long)Main.reforgeItem.value*Main.reforgeItem.stack/3);
            if(seed.HasValue)Main.rand=new UnifiedRandom(seed.Value);typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0);
            NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
        }
    }
}
