using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using JueMingR.Platform.Items;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinSeedWriteChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static int mode,step;
        private static object currentHost;
        private static Item source,postNativeEmpty;
        internal static void Run(object context,object host)
        {
            currentHost=host;Player p=Main.LocalPlayer;var originalWorld=Main.ActiveWorldFileData;
            object transfer=Get(host,"Transfer");Type type=transfer.GetType();
            var audit=new Harmony("JueMingR.Tests.CoinSeedWrite");
            MethodInfo write=type.GetMethod("WriteSeed",Flags),restore=type.GetMethod("RestoreSeed",Flags);
            audit.Patch(write,prefix:new HarmonyMethod(typeof(NativeCoinSeedWriteChecks).GetMethod(nameof(Before),Flags)),
                transpiler:new HarmonyMethod(typeof(NativeCoinSeedWriteChecks).GetMethod(nameof(AfterStores),Flags)));
            audit.Patch(restore,prefix:new HarmonyMethod(typeof(NativeCoinSeedWriteChecks).GetMethod(nameof(BeforeRestore),Flags)));
            try
            {
                for(int test=1;test<=9;test++)
                {
                    NativeCoinSeedAdmissionChecks.Fresh(context,host);step=0;mode=test;
                    long total=Total(p.inventory,58)+Total(p.bank.item,40),calls=(long)Get(transfer,"NativeCalls");
                    string result=NativeCoinSeedChecks.Execute(host);
                    if(test==1||test==2||test==7)
                    {
                        Require(result=="Failed" && !((CoinIntent)Get(host,"Intent")).Faulted && ReferenceEquals(p.inventory[50],source) &&
                            ReferenceEquals(p.bank.item[1],postNativeEmpty) && Total(p.inventory,58)+Total(p.bank.item,40)==total,
                            "exact exclusive interrupted seed restores only its two current post-native objects: "+test);
                    }
                    else
                    {
                        Require(result=="Unconfirmed" && ((CoinIntent)Get(host,"Intent")).Faulted,"uncertain partial write remains unknown: "+test);
                        Require(!ReferenceEquals(p.inventory[50],source),"unknown partial never refills old source: "+test);
                        mode=0;NativeCoinMatrix.Tick(host,0,300);
                        Require((long)Get(transfer,"NativeCalls")==calls+1,"unknown seed never replays another account: "+test);
                    }
                }
            }
            finally
            {
                mode=0;currentHost=null;source=postNativeEmpty=null;
                audit.Unpatch(write,HarmonyPatchType.All,audit.Id);audit.Unpatch(restore,HarmonyPatchType.All,audit.Id);
                Main.gamePaused=true;Main.ActiveWorldFileData=originalWorld;Call(context,"UpdateRuntime");Main.gamePaused=false;NativeCoinMatrix.Reset(p,host);
            }
            Console.WriteLine("PASS: real first/second array-write interruption, exact two-slot recovery, failed recovery, outside mutation/occupancy/session change; unknowns never refill or replay.");
        }
        private static void Before()
        {
            source=Main.LocalPlayer.inventory[50];postNativeEmpty=Main.LocalPlayer.bank.item[1];
            if(mode==7)throw new InvalidOperationException("before-seed-write");
        }
        private static IEnumerable<CodeInstruction> AfterStores(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var instruction in instructions)
            {yield return instruction;if(instruction.opcode==OpCodes.Stelem_Ref)yield return new CodeInstruction(OpCodes.Call,typeof(NativeCoinSeedWriteChecks).GetMethod(nameof(AfterStore),Flags));}
        }
        private static void AfterStore()
        {
            step++;
            if(mode==0||mode==7)return;
            if(mode==8){if(step==2)Main.ActiveWorldFileData=null;return;}
            if(step!=(mode==2||mode==4?2:1))return;
            if(mode==3||mode==4){Main.LocalPlayer.inventory[2].SetDefaults(8);Main.LocalPlayer.inventory[2].stack=17;}
            if(mode==9)source.stack++; // The detached object is still evidence, not a safe refill.
            if(mode==6){var own=(ItemOperationOwnership)Get(Get(currentHost,"Items"),"Ownership");own.HoldInterruptedSource(own.Session,1UL<<50);}
            throw new InvalidOperationException("after-actual-array-write-"+step);
        }
        private static void BeforeRestore(){if(mode==5)throw new InvalidOperationException("isolated-restore-failure");}
    }
}
