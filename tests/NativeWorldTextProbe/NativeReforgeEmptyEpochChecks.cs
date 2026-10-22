using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Processing;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real BuyItem and native inner rolls, with only input and Draw's narrow
    // observation supplied by this isolated fixture. Full Draw is checked by
    // NativeReforgeOuterChecks, not inferred from these seam calls.
    internal static class NativeReforgeEmptyEpochChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static int rolls;
        internal static void Run(object host,object input)
        {
            var p=Main.LocalPlayer;var owner=Get(host,"Reforge");var settings=((ProcessingSettings[])Get(host,"Settings"))[2];
            var audit=new Harmony("JueMingR.Tests.ReforgeEmptyEpoch");
            var roll=typeof(Main).GetMethod("ReforgeItemInReforgeSlot",Flags);audit.Patch(roll,prefix:new HarmonyMethod(typeof(NativeReforgeEmptyEpochChecks),nameof(Roll)));
            object foreground=Get(input,"foregroundWindow");bool paused=Main.gamePaused;var random=Main.rand;
            Main.playerInventory=Main.InReforgeMenu=true;Main.InGuideCraftMenu=false;Main.SetNPCShopIndex(0);p.chest=-1;p.SetTalkNPC(0);
            Main.npc[0].SetDefaults(107);Main.npc[0].active=true;p.discountAvailable=false;p.currentShoppingSettings.PriceAdjustment=1;Main.mouseItem.TurnToAir();
            foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();p.inventory[50]=NativeCoinChecks.Coin(74,30);
            try
            {
                Fresh(owner,input,settings);
                // R1: a real release survives empties; a press is created only
                // by the following real sample, never by stale MouseInfo.
                for(int i=0;i<4;i++)NativeProcessingChecks.EmptyOuterUpdate(host,input);
                Require(!(bool)Get(owner,"down") && !(bool)Get(owner,"pressReady"),"R1 empty epochs cannot manufacture a press");
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                Require((bool)Get(owner,"pressReady"),"R1 next reliable first press remains available");
                // R2: first quote arrives in empty Draw and is consumed once
                // by the next world step; no outer Frame ageing.
                long money=NativeReforgeChecks.Total(p),fee=Fee();int count=rolls;
                for(int i=0;i<4;i++)NativeProcessingChecks.EmptyOuterUpdate(host,input);
                Call(owner,"Observe",true,120,310,15,fee);
                Require((bool)Get(owner,"owned") && !(bool)Call(owner,"NativePayment",p,fee,-1),"R2 empty Draw confirms original press but cannot repeat automatically");
                NativeProcessingChecks.EmptyOuterUpdate(host,input);
                Require(money==NativeReforgeChecks.Total(p) && rolls==count,"R2 empties have zero automatic debits and rolls");
                NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                Require(money-NativeReforgeChecks.Total(p)==fee && rolls==count+1,"R2 next reliable world step consumes quote exactly once");
                // R3: keep owned across empty Updates, then continue with a
                // refreshed real quote. Choose a target different from result.
                Fresh(owner,input,settings);int target=Main.reforgeItem.GetRollablePrefixes().Last();
                NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,new[]{Lang.prefix[target].Value}));
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,Fee());
                int repeatSeed=0;
                for(;repeatSeed<128;repeatSeed++){Main.rand=new Terraria.Utilities.UnifiedRandom(repeatSeed);var probe=new Item();probe.SetDefaults(53);bool top;probe.Prefix(-2,out top);if(probe.prefix!=target)break;}
                Require(repeatSeed<128,"R3 native prefix generator supplies a non-target roll");
                Main.rand=new Terraria.Utilities.UnifiedRandom(repeatSeed);NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                Require((bool)Get(owner,"owned") && Main.reforgeItem.prefix!=target,"R3 automatic hold already completed a paid non-target roll");Call(owner,"Observe",true,120,310,15,Fee());
                money=NativeReforgeChecks.Total(p);count=rolls;
                for(int i=0;i<4;i++)NativeProcessingChecks.EmptyOuterUpdate(host,input);
                Require((bool)Get(owner,"owned") && money==NativeReforgeChecks.Total(p) && rolls==count,"R3 owned hold survives true empties without automatic work");
                fee=Fee();Main.rand=new Terraria.Utilities.UnifiedRandom(repeatSeed);NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                Require(money-NativeReforgeChecks.Total(p)==fee && rolls==count+1,"R3 owned repeat resumes at next actual world step");
                // R4: an old quote is invalidated by actual source/price/UI
                // changes or two reliable samples, not by empty Frames.
                for(int change=0;change<8;change++)
                {
                    Fresh(owner,input,settings);NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,Fee());
                    money=NativeReforgeChecks.Total(p);count=rolls;float oldScale=Main.UIScale;int oldWidth=(int)PlayerInput.OriginalScreenSize.X;
                    if(change==0)Main.reforgeItem=Main.reforgeItem.Clone();
                    if(change==1)Main.reforgeItem.Prefix(62);
                    if(change==2)Main.reforgeItem.value++;
                    if(change==3)Main.reforgeItem.stack++;
                    if(change==4)p.discountAvailable=true;
                    if(change==5)p.currentShoppingSettings.PriceAdjustment=0.9f;
                    if(change==6)Main.UIScale=oldScale+0.5f;
                    if(change==7)typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,oldWidth+10);
                    NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                    Require(money==NativeReforgeChecks.Total(p) && rolls==count,"R4 changed quote identity/price/geometry refuses old debit: "+change);
                    Main.UIScale=oldScale;typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,oldWidth);p.discountAvailable=false;p.currentShoppingSettings.PriceAdjustment=1;
                }
                Fresh(owner,input,settings);NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,Fee());
                money=NativeReforgeChecks.Total(p);count=rolls;
                NativeReforgeChecks.Sample(input,true);Call(owner,"Update");NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                Require(money==NativeReforgeChecks.Total(p) && rolls==count,"R4 quote ages after two real mappings");
                // R5: initial matched item, reliable press, then only empty
                // outer updates. First quote and native manual debit remain
                // legal even paused; only the actual Draw button gates apply.
                Fresh(owner,input,settings);Main.reforgeItem.Prefix(62);NativeReforgeChecks.Sample(input,true);Call(owner,"Update");
                money=NativeReforgeChecks.Total(p);count=rolls;fee=Fee();Main.gamePaused=true;
                NativeProcessingChecks.EmptyOuterUpdate(host,input);Call(owner,"Observe",true,120,310,15,fee);
                Require((bool)Call(owner,"NativePayment",p,fee,-1),"R5 original manual single payment survives empty mapping and pause");roll.Invoke(null,null);
                Require(!(bool)Call(owner,"NativePayment",p,fee,-1) && money-NativeReforgeChecks.Total(p)==fee && rolls==count+1,"R5 same hold retains exactly one manual paid roll");Main.gamePaused=false;
                // R6: real invalidation in an empty epoch cancels immediately;
                // restoring context cannot rearm the still-held old gesture.
                for(int change=0;change<3;change++)
                {
                    Fresh(owner,input,settings);NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,Fee());
                    money=NativeReforgeChecks.Total(p);count=rolls;
                    if(change==0)Main.InReforgeMenu=false;
                    if(change==1)Main.reforgeItem=Main.reforgeItem.Clone();
                    if(change==2)Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));
                    NativeProcessingChecks.EmptyOuterUpdate(host,input,change!=2);
                    Require(!(bool)Get(owner,"owned") && !(bool)Get(owner,"pressReady") && !(bool)Get(owner,"manual"),"R6 empty epoch observes real menu/item/focus invalidation: "+change);
                    Main.InReforgeMenu=true;Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;
                    Call(owner,"Observe",true,120,310,15,Fee());NativeReforgeChecks.SampleWorld(input,true);Call(owner,"Update");
                    Require(money==NativeReforgeChecks.Total(p) && rolls==count,"R6 restored context cannot revive old hold: "+change);
                    NativeReforgeChecks.Sample(input,false);Call(owner,"Update");NativeReforgeChecks.Sample(input,false);Call(owner,"Update");
                }
            }
            finally
            {
                audit.Unpatch(roll,HarmonyPatchType.All,audit.Id);Main.rand=random;Main.gamePaused=paused;Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;
                NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.InReforgeMenu=false;Main.playerInventory=false;p.SetTalkNPC(-1);NativeProcessingUiChecks.Save(settings,new ProcessingOptions());
            }
            Console.WriteLine("PASS G08 true empty epochs R1-R6: reliable press/quote ownership, mapping age, zero automatic debit/roll, exact native single manual opportunity and real cancellation; CPU seam evidence only.");
        }
        private static void Fresh(object owner,object input,ProcessingSettings settings)
        {
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(53);
            NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
            owner.GetType().GetField("quoteReady",Flags).SetValue(owner,false);
        }
        private static long Fee(){return (long)Main.reforgeItem.value*Main.reforgeItem.stack/3;}
        private static void Roll(){rolls++;}
        private static void Set(object owner,string name,object value){owner.GetType().GetField(name,Flags).SetValue(owner,value);}
    }
}
