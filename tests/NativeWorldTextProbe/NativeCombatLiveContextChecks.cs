using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    // Focused reproduction of ordinary production acceptance under additional
    // live-world context. The callback keeps the existing original/Host order;
    // no prediction result or history state is patched by this fixture.
    internal static class NativeCombatLiveContextChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            string mode=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT");
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            if(mode=="diagnostic" || mode=="diagnostic-off"){Diagnostic(context,native,step,output,mode=="diagnostic");return;}
            if(mode!="shared-rng")throw new InvalidOperationException("Unknown live-context scenario.");
            int failed=0;
            foreach(bool neighbor in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;
                foreach(var projectile in Main.projectile)projectile.active=false;
                var player=Main.LocalPlayer;player.controlRight=player.controlLeft=player.controlJump=false;
                player.position=new Vector2(640,70*16-player.height);player.velocity=Vector2.Zero;
                if(neighbor)
                {
                    int bunny=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300,70*16,NPCID.Bunny);
                    Require(bunny==0 && Main.npc[bunny].friendly,"Unrelated native Bunny occupies the earlier slot.");
                }
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),680,70*16,NPCID.Zombie,Start:1);
                Require(slot>=1 && slot<Main.maxNPCs,"Selected native Zombie occupies a later slot, respecting original spawn protection.");
                long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused");
                int shown=0,longest=0,blank=0;var reasons=new Dictionary<string,int>();
                for(int frame=0;frame<300;frame++)
                {
                    step();var path=cache.Read(0);
                    string reason=(string)Get(native,"Reason")??"none";
                    int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    if(path==null){longest=Math.Max(longest,++blank);continue;}
                    blank=0;shown++;
                    Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]) && path.Strategy==PredictionStrategy.NativeIsolated,"Current native target owns every displayed window.");
                    Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"Current origin and complete future remain truthful.");
                }
                Console.WriteLine("LIVE-CONTEXT neighbor="+neighbor+" slot="+slot+" shown="+shown+" longest-blank="+longest+" requests="+((long)Get(native,"Requests")-requests)+" rejected="+((long)Get(native,"Rejected")-rejected)+" refused="+((long)Get(native,"Refused")-refused));
                foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("LIVE-REASON frames="+pair.Value+" "+pair.Key);
                if(shown<180 || longest>=60)failed++;
            }
            Require(failed==0,"Ordinary production must remain usable with an unrelated earlier NPC; failing scenes="+failed);
        }
        private static void Diagnostic(object context,object native,Action step,string output,bool enabled)
        {
            var type=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionDiagnostic");
            if(!enabled){Require(type==null,"Ordinary build excludes the diagnostic type and all call sites.");Console.WriteLine("PASS ordinary build contains no live diagnostic");return;}
            Require(type!=null,"Explicit diagnostic build must contain the bounded collector.");
            string actualDirectory=Path.Combine(Terraria.Program.SavePath,"composition/JueMingRData/cache/npc-prediction");
            string actualLog=Path.Combine(actualDirectory,"ordinary-live-20260930.log");
            object actualObserver=Get(native,"diagnostic");Wait(()=> (int)Get(actualObserver,"writing")==0,"Production diagnostic drain");
            Require(!File.Exists(Path.Combine(actualDirectory,"ordinary-live-20260930.arm")) && File.Exists(actualLog) && File.ReadAllLines(actualLog).Any(l=>l.Contains("ready=True") && !l.Contains("published=0 ")),"Actual production constructor and Prepare persist ready/published state after consuming the isolated arm.");
            string directory=Path.Combine(output,"one-shot");Directory.CreateDirectory(directory);
            string arm=Path.Combine(directory,"ordinary-live-20260930.arm"),claim=Path.Combine(directory,"ordinary-live-20260930.claimed"),log=Path.Combine(directory,"ordinary-live-20260930.log");
            var ctor=type.GetConstructor(Flags,null,new[]{typeof(string)},null);
            object dormant=ctor.Invoke(new object[]{directory});Wait(()=> (bool)Get(dormant,"Initialized"),"Missing-arm initialization");
            Require((int)Get(dormant,"armed")==0 && !File.Exists(log),"Missing arm performs no recording.");
            File.WriteAllText(arm,"#112 one-shot test");object observer=ctor.Invoke(new object[]{directory});
            Wait(()=> (int)Get(observer,"armed")==1,"Diagnostic activation");
            Require(!File.Exists(arm) && File.Exists(claim),"Arm must be consumed before recording.");
            var identity=(NpcIdentity)Get(Get(Get(context,"CombatObservation"),"Selection"),"Target");
            var observe=type.GetMethod("Observe",Flags);var fault=type.GetMethod("CaptureFault",Flags);
            observe.Invoke(observer,new object[]{native,identity,100L,99L});
            type.GetMethod("WorkerFault",Flags).Invoke(observer,new object[]{"fixture-startup-fault"});
            fault.Invoke(observer,new object[]{new InvalidDataException("fixture-capture-fault "+new string('x',10000))});
            fault.Invoke(observer,new object[]{new InvalidDataException("second-fault-must-not-record")});
            for(int i=1;i<160;i++)observe.Invoke(observer,new object[]{native,identity,100L+i*30,99L+i*30});
            Wait(()=> (int)Get(observer,"writing")==0,"Bounded writer drain");
            string[] lines=File.ReadAllLines(log);
            Require(lines.Length==129 && lines.All(l=>l.Length<=4096),"At most 128 bounded records plus header are persisted.");
            Require(lines.Count(l=>l.StartsWith("first-capture-fault="))==1 && lines.Any(l=>l.Contains("fixture-capture-fault")) && !lines.Any(l=>l.Contains("second-fault")),"First capture exception is visible and is recorded only once.");
            Require(lines.Any(l=>l=="first-worker-fault=fixture-startup-fault"),"Worker failure can be recorded without a selected target.");
            Require(lines.Any(l=>l.Contains("requests="+(long)Get(native,"Requests")) && l.Contains("worker=")),"Real production owner status is recorded.");
            string original=File.ReadAllText(log);object restart=ctor.Invoke(new object[]{directory});Wait(()=> (bool)Get(restart,"Initialized"),"Consumed-arm initialization");
            observe.Invoke(restart,new object[]{native,identity,1000L,999L});
            Require((int)Get(restart,"armed")==0 && File.ReadAllText(log)==original,"Consumed arm cannot record again on an ordinary restart.");
            string timed=Path.Combine(output,"time-limit");Directory.CreateDirectory(timed);File.WriteAllText(Path.Combine(timed,"ordinary-live-20260930.arm"),"#112");
            object timer=ctor.Invoke(new object[]{timed});Wait(()=> (int)Get(timer,"armed")==1,"Timed activation");
            type.GetField("start",Flags).SetValue(timer,Stopwatch.GetTimestamp()-Stopwatch.Frequency*121L);
            // No subsequent Observe is needed for the deadline: a late fault
            // after losing the target must stop before adding its own record.
            type.GetMethod("WorkerFault",Flags).Invoke(timer,new object[]{"late-fault-must-not-record"});
            fault.Invoke(timer,new object[]{new InvalidDataException("late-capture-must-not-record")});
            Wait(()=> (int)Get(timer,"writing")==0,"Timed writer drain");
            Require((int)Get(timer,"stopped")==1 && File.ReadAllText(Path.Combine(timed,"ordinary-live-20260930.log")).Contains("stopped=time-limit"),"Elapsed window stops actual collection and records the reason.");
            Require(!File.ReadAllText(Path.Combine(timed,"ordinary-live-20260930.log")).Contains("must-not-record"),"All fault entries honor the elapsed window.");
            // Terminate only the exact process handle owned by this isolated
            // probe, then exercise the real Host path that closes Path.
            var worker=Get(native,"Worker");((Process)Get(worker,"child")).Kill();
            var failed=Stopwatch.StartNew();while(!(bool)Get(native,"Failed") && failed.ElapsedMilliseconds<6000)step();
            Require((bool)Get(native,"Failed"),"Actual helper failure closes the production path.");
            Wait(()=> (int)Get(actualObserver,"writing")==0,"Production fault drain");
            Require(File.ReadAllLines(actualLog).Any(l=>l.StartsWith("first-worker-fault=")),"The terminating production callback records its failure before later Prepare calls stop.");
            Console.WriteLine("PASS diagnostic: actual owner status, first capture fault, absent/consumed arm, row/text/time bounds and ordinary restart");
        }
        private static void Wait(Func<bool> complete,string reason)
        {var watch=Stopwatch.StartNew();while(!complete() && watch.ElapsedMilliseconds<3000)Thread.Sleep(5);Require(complete(),reason);}
        private static object Get(object owner,string name)
        {var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
