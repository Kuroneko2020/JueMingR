using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCaptureWorkloadChecks
    {
        internal static void Cpu(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),capture=Get(host,"Capture"),fish=Get(host,"Fishing"),npcs=Get(host,"Npcs");
            var p=Main.LocalPlayer;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(1991);p.inventory[17].SetDefaults(2289);
            p.itemAnimation=p.itemTime=0;p.selectedItemState.Select(17);p.selectedItemState.Update();NativeToolsChecks.SetMode(host,0,1);
            NativeToolsChecks.Frame(context,input);
            Require(!(bool)Call(capture,"Boss"),"prime prior post-NPC no-boss observation");
            long scans=(long)Get(capture,"BossEvaluations");int raw=(int)Get(npcs,"BasicReads");
            var boss=Main.npc[1];boss.SetDefaults(4);boss.active=true;boss.life=100;
            // Run the real selection hook before the runtime/NPC stage. A
            // missing BeginActions must not be rescued by manual invalidation.
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");NativeQuickItemChecks.NativeFrame(p);
            Require((long)Get(capture,"BossEvaluations")==scans+1 && (int)Get(npcs,"BasicReads")>raw && (bool)Get(capture,"boss"),"actual native selection refreshes danger before action");
            long visits=(long)Get(capture,"BossSlotVisits");Require((bool)Call(capture,"Boss") && (long)Get(capture,"BossSlotVisits")==visits,"same action observation reused");
            boss.active=false;NativeToolsChecks.Frame(context,input);Require(!(bool)Call(capture,"Boss"),"real completed update sees removed Boss");
            // An outer callback can receive a Boss without a new completed
            // sample. It owns no recast permission. The next native selection
            // must refresh action facts before any risky automatic action.
            var loan=Main.projectile[0];loan.SetDefaults(p.inventory[17].shoot);loan.owner=p.whoAmI;loan.active=true;loan.ai[0]=0;
            long staleToken=(long)Call(fish,"Prepare",p);Require(staleToken>0 && !(bool)Call(capture,"Boss"),"prime legal no-Boss loan before same-tick network-like correction");
            object borrowedSelection=p.selectedItemState;
            borrowedSelection.GetType().GetMethod("OverrideSelection",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(borrowedSelection,new object[]{12});p.selectedItemState=(Player.SelectedItemState)borrowedSelection;
            Call(loan,"AI_061_FishingBobber");Require(!loan.active,"native borrowed net selection ends original bobber");
            borrowedSelection=p.selectedItemState;borrowedSelection.GetType().GetMethod("OverrideSelection",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(borrowedSelection,new object[]{17});p.selectedItemState=(Player.SelectedItemState)borrowedSelection;
            Call(fish,"NetFinished",staleToken,false,false);
            NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");
            Require(Get(fish,"Phase").ToString()=="Returning" && (bool)Get(input,"CanRunAutomaticActions") && Call(fish,"Choose",p)!=null,"same-tick safety begins with a genuinely ready legal recast candidate");
            boss.SetDefaults(4);boss.active=true;boss.life=100;NativeToolExecutionChecks.Outer(context,input,1);
            Console.WriteLine("G09 same-tick Boss outer phase="+Get(fish,"Phase")+" automatic="+Get(input,"CanRunAutomaticActions")+" attempted="+Get(fish,"RecastAttempted"));
            Require(Get(fish,"Phase").ToString()=="Returning" && !(bool)Get(input,"CanRunAutomaticActions") && !(bool)Get(fish,"RecastAttempted"),"unsampled outer revokes action permission for otherwise ready recast");
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
            Require((bool)Get(capture,"boss") && Get(fish,"Phase").ToString()=="Cancelled" && !(bool)Get(fish,"RecastAttempted"),"next real selection refreshes danger and cancels borrowed recast");
            boss.active=false;loan.active=false;NativeToolsChecks.Frame(context,input);Require(!(bool)Call(capture,"Boss"),"same-tick danger test recovers through completed sample");
            foreach(int type in new[]{4,13})
            {
                var b=Main.projectile[0];b.SetDefaults(p.inventory[17].shoot);b.owner=p.whoAmI;b.active=true;b.ai[0]=0;
                long token=(long)Call(fish,"Prepare",p);Require(token>0 && (bool)Get(fish,"Active"),"legal isolated borrowed starting state");
                Require(!(bool)Call(capture,"Boss"),"prime no-boss cache before NPC changes");
                boss.SetDefaults(type);boss.active=true;boss.life=100;if(type==13)boss.boss=false;
                // Runtime owns post-NPC freshness and Fishing.Update consumes
                // it. No direct Boss/BeginTick call precedes this assertion.
                NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");
                Require(Get(fish,"Phase").ToString()=="Cancelled" && (long)Get(fish,"Token")==token && !(bool)Get(fish,"RecastAttempted"),"post-NPC Boss change cancels borrowed responsibility before recast: "+type);
                boss.active=false;b.active=false;NativeToolsChecks.Frame(context,input);Require(!(bool)Call(capture,"Boss"),"danger removal recovers after type "+type);
            }
            NativeToolsChecks.SetMode(host,0,0);NativeToolsChecks.Frame(context,input);visits=(long)Get(capture,"BossSlotVisits");raw=(int)Get(npcs,"BasicReads");
            for(int i=0;i<32;i++)NativeToolsChecks.Frame(context,input);
            Require((long)Get(capture,"BossSlotVisits")==visits && (int)Get(npcs,"BasicReads")==raw,"off capture with no loan stops danger work");
            Console.WriteLine("PASS G09 CPU danger consumers: actual selection freshness, post-NPC loan cancellation, composite Boss, recovery and off.");
        }

        // Used only around the existing real missed-capture/rod-return chain.
        // Observe TryRead inside Boss independently of its debug loop counter.
        internal sealed class Observation : IDisposable
        {
            private static Observation active;
            private readonly object capture,npcs;
            private readonly Harmony harmony=new Harmony("JueMingR.Tests.G09CaptureWork");
            private readonly MethodInfo bossMethod,readMethod;
            private readonly Dictionary<int,int> requests=new Dictionary<int,int>();
            private readonly long visits,refreshes;
            private readonly int basic,danger;
            private long traversals,captureCalls,fishingCalls;
            private int depth;
            internal Observation(object host)
            {
                Require(active==null,"capture observers run serially");capture=Get(host,"Capture");npcs=Get(host,"Npcs");
                visits=(long)Get(capture,"BossSlotVisits");refreshes=(long)Get(capture,"ActiveTargetRefreshes");basic=(int)Get(npcs,"BasicReads");danger=(int)Get(npcs,"DangerReads");
                bossMethod=capture.GetType().GetMethod("Boss",BindingFlags.Instance|BindingFlags.NonPublic);
                readMethod=npcs.GetType().GetMethod("TryRead");Require(bossMethod!=null && readMethod!=null,"loaded capture observation targets");active=this;
                harmony.Patch(bossMethod,prefix:new HarmonyMethod(typeof(Observation),nameof(Before)),postfix:new HarmonyMethod(typeof(Observation),nameof(After)));
                harmony.Patch(readMethod,prefix:new HarmonyMethod(typeof(Observation),nameof(Read)));
            }
            private static void Before(object __instance,out long[] __state)
            {
                var a=active;Require(ReferenceEquals(__instance,a.capture),"observe actual capture owner");
                int epoch=(int)Get(a.npcs,"Epoch"),prior;a.requests.TryGetValue(epoch,out prior);a.requests[epoch]=prior+1;
                __state=new[]{a.traversals,(long)Get(__instance,"BossEvaluations"),(long)prior};a.depth++;
                var frames=new StackTrace().GetFrames();
                if(frames.Any(f=>f.GetMethod()?.DeclaringType?.Name=="FishingBorrow"))a.fishingCalls++;
                if(frames.Any(f=>f.GetMethod()?.Name=="Find" || (f.GetMethod()?.DeclaringType?.FullName??"").Contains("AutoCapture+")))a.captureCalls++;
            }
            private static void Read(){if(active.depth>0)active.traversals++;}
            private static void After(object __instance,long[] __state)
            {
                var a=active;a.depth--;
                long evaluations=(long)Get(__instance,"BossEvaluations")-__state[1],reads=a.traversals-__state[0];
                Require(evaluations<=(__state[2]==0?1:0) && (__state[2]==0 || reads==0),"real capture and fishing consumers reuse one Boss traversal per observation epoch");
            }
            internal void Verify()
            {
                Require(traversals>0 && traversals==(long)Get(capture,"BossSlotVisits")-visits,"actual NPC traversal matches nonzero Boss work counter");
                Require((int)Get(npcs,"BasicReads")>basic && (int)Get(npcs,"DangerReads")>danger,"real raw NPC facts refreshed");
                Require(captureCalls>0 && fishingCalls>0 && requests.Values.Any(n=>n>1) && (long)Get(capture,"ActiveTargetRefreshes")>refreshes,"actual capture validity/refresh and borrowed-rod consumers executed");
                Console.WriteLine("G09 combined Boss work: epochs="+requests.Count+" actual slot reads="+traversals+" capture requests="+captureCalls+" rod requests="+fishingCalls);
            }
            public void Dispose(){harmony.Unpatch(bossMethod,HarmonyPatchType.All,harmony.Id);harmony.Unpatch(readMethod,HarmonyPatchType.All,harmony.Id);active=null;}
        }
    }
}
