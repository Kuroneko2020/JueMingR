using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryFaultChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static int fault;
        internal static void Run(object context)
        {
            object host=Get(context,"Recovery"),input=Get(context,"Input"),nurse=Get(host,"Nurse"),tax=Get(host,"Tax");
            var settings=(RecoverySettings)Get(host,"Services");var p=Main.LocalPlayer;
            var patch=new Harmony("JueMingR.Tests.RecoveryFaults");
            Patch(patch,typeof(Main).GetMethod("GetNurseHealCost",Flags),nameof(Quote));
            Patch(patch,typeof(Player).GetMethod("BuyItem",Flags),nameof(Paid));
            Patch(patch,typeof(Item).GetMethods(Flags).Single(m=>m.Name=="NewItem" && m.GetParameters().Length==9),nameof(Requested));
            Patch(patch,typeof(Main).GetMethod("NPCChatText_DoTaxCollector",Flags),nameof(Portrait));
            try
            {
                Action reset=()=>{p.SetTalkNPC(-1);Main.npcChatText="";p.statLife=400;p.discountAvailable=false;p.mouseInterface=false;Call(nurse,"Reset");Main.npc[0].position=p.position+new Vector2(30,0);Call(Get(context,"nativeNpcs"),"BeginTick");};
                reset();NativeRecoveryChecks.Save(settings,new RecoveryOptions(nurse:true));fault=1;
                NativeRecoveryChecks.Frame(host,input,1400);
                Require(p.talkNPC==1 && p.currentShoppingSettings.PriceAdjustment==.42f,"quote never overwrites a later manual shopping context");
                reset();long funds=NativeRecoveryServiceChecks.Total(p);fault=2;NativeRecoveryChecks.Frame(host,input,1500);
                Require(p.statLife==400 && NativeRecoveryServiceChecks.Total(p)==funds,"payment boundary rechecks actual target range after native quote");
                reset();p.statLife=499;p.discountAvailable=true;NativeRecoveryChecks.Frame(host,input,1600);
                Require(p.statLife==499 && NativeRecoveryServiceChecks.Total(p)==funds && p.talkNPC==0,"real zero-cost nurse keeps vanilla dialog without fabricated heal");
                long calls=(long)Get(nurse,"NativeCalls");p.SetTalkNPC(-1);Main.npcChatText="";
                for(ulong t=1660;t<1900;t+=60)NativeRecoveryChecks.Frame(host,input,t);
                Require((long)Get(nurse,"NativeCalls")==calls,"same zero quote cannot loop after manual close");
                reset();fault=3;funds=NativeRecoveryServiceChecks.Total(p);NativeRecoveryChecks.Frame(host,input,2000);
                long paid=NativeRecoveryServiceChecks.Total(p);Require(paid<funds && p.statLife==400 && p.talkNPC==0 && (bool)Get(nurse,"unknown"),"post-payment fault protects real debit and retains dialog");
                Call(host,"Set",2,0);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
                NativeRecoveryChecks.Save(settings,new RecoveryOptions(nurse:true));p.SetTalkNPC(-1);Main.npcChatText="";
                for(ulong t=2100;t<2400;t+=60)NativeRecoveryChecks.Frame(host,input,t);
                Require(NativeRecoveryServiceChecks.Total(p)==paid,"toggle and close cannot retry unknown nurse debit");
                // A genuine world exit, not a transient runtime suspension,
                // ends the synthetic account's unknown operation ownership.
                Main.gameMenu=true;Call(context,"UpdateRuntime");Main.gameMenu=false;Call(context,"UpdateRuntime");NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Call(Get(context,"nativeNpcs"),"BeginTick");
                NativeRecoveryChecks.Save(settings,new RecoveryOptions(tax:true));Call(tax,"Reset");p.SetTalkNPC(-1);Main.npcChatText="";p.taxMoney=12345;fault=4;
                NativeRecoveryChecks.Frame(host,input,2500);Require(p.taxMoney==12345 && (bool)Get(tax,"unconfirmed"),"actual first world-coin request fault retains unknown cycle; fault="+fault+" admit="+Call(host,"Admit",p)+" tax="+p.taxMoney+" error="+GetOptional(tax,"LastFailure"));
                calls=(long)Get(tax,"NativeCalls");p.taxMoney+=500;p.SetTalkNPC(-1);Main.npcChatText="";
                for(ulong t=2560;t<2800;t+=60)NativeRecoveryChecks.Frame(host,input,t);
                Require((long)Get(tax,"NativeCalls")==calls,"balance growth never replays partial tax request");
                p.taxMoney=0;Call(host,"Poll");p.taxMoney=12345;fault=5;NativeRecoveryChecks.Frame(host,input,2900);
                Require(p.taxMoney==0 && p.talkNPC==1,"fault after native settlement retains dialog but observes zero immediately");
                p.taxMoney=100;Call(host,"Poll");p.SetTalkNPC(-1);Main.npcChatText="";NativeRecoveryChecks.Frame(host,input,3000);
                Require(p.taxMoney==0,"tax growth before next poll does not hide already observed settlement");
                Console.WriteLine("PASS G07 faults: native zero price, context takeover, payment range, post-debit hold, partial tax and observed-zero recovery.");
            }
            finally{fault=0;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);p.discountAvailable=false;p.SetTalkNPC(-1);Main.npcChatText="";NativeRecoveryChecks.Save(settings,new RecoveryOptions());}
        }
        private static void Patch(Harmony patch,MethodInfo method,string name){patch.Patch(method,postfix:new HarmonyMethod(typeof(NativeRecoveryFaultChecks).GetMethod(name,Flags)));}
        private static void Quote()
        {
            var p=Main.LocalPlayer;
            if(fault==1 && p.talkNPC<0){fault=0;p.SetTalkNPC(1);p.currentShoppingSettings.PriceAdjustment=.42f;}
            else if(fault==2 && p.talkNPC==0){fault=0;Main.npc[0].position+=new Vector2(5000,0);}
        }
        private static void Paid(bool __result){if(fault==3 && __result){fault=0;throw new InvalidOperationException("isolated fault after real payment");}}
        private static void Requested(){if(fault==4){fault=0;throw new InvalidOperationException("isolated fault after real world request");}}
        private static void Portrait(){if(fault==5 && Main.LocalPlayer.taxMoney==0 && Main.LocalPlayer.talkNPC==1){fault=0;throw new InvalidOperationException("isolated fault after real settlement");}}
    }
}
