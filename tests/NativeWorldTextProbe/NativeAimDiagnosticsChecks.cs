using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace NativeWorldTextProbe
{
    internal static class NativeAimDiagnosticsChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(string output)
        {
            Directory.CreateDirectory(output);
            string path=Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            var host=Assembly.LoadFrom(path);
            var recorder=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnosticRecorder");
            Require(recorder!=null,"Diagnostic build must provide the owned recorder; ordinary builds must exclude it.");
            string root=Path.Combine(output,"owned-session");
            var instance=Activator.CreateInstance(recorder,Flags,null,new object[]{root,1024L*1024,1024L*1024},null);
            byte[] original={1,2,3,4};
            recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"request-raw",1L,3L,4L,5L,"owned",original,true});
            original[0]=99;
            recorder.GetMethod("Stop",Flags).Invoke(instance,new object[]{"test-stop"});
            var blob=Directory.GetFiles(root,"detail-*.bin",SearchOption.AllDirectories).Single();
            Require(File.ReadAllBytes(blob).SequenceEqual(new byte[]{1,2,3,4}),"Queued raw bytes must survive producer buffer reuse.");
            string timeline=File.ReadAllText(Path.Combine(root,"timeline.tsv"));
            Require(timeline.Contains("request-raw") && timeline.Contains("test-stop"),"Request linkage and explicit stop survive collection shutdown.");
            var tape=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnosticTape",true);
            var a=Activator.CreateInstance(tape,true);var b=Activator.CreateInstance(tape,true);
            tape.GetMethod("BeginField",Flags).Invoke(a,new object[]{"NPC.ai[0]","Single"});tape.GetMethod("BeginField",Flags).Invoke(b,new object[]{"NPC.ai[0]","Single"});
            tape.GetMethod("Add",Flags).Invoke(a,new object[]{"u32",1UL});tape.GetMethod("Add",Flags).Invoke(b,new object[]{"u32",2UL});
            string difference=(string)tape.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
            Require(difference.Contains("NPC.ai[0]") && difference.Contains("0000000000000001") && difference.Contains("0000000000000002"),"Field diff must carry actual historical bits on both sides.");

            FailureChecks(recorder,output);
            BoundaryChecks(host,recorder,output);
            CaptureFailure(host,Path.Combine(output,"capture-failure"));
            Console.WriteLine("PASS aim diagnostics owned bytes, explicit stop, linked timeline, historical field bits, queue/disk/IO failure and shared controls.");
        }

        private static void FailureChecks(Type recorder,string output)
        {
            string root=Path.Combine(output,"queue-capacity");
            var instance=Activator.CreateInstance(recorder,Flags,null,new object[]{root,1024L*1024,4096L},null);
            recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"before-overflow",1L,1L,1L,1L,"",new byte[]{7},true});
            recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"overflow",2L,1L,1L,1L,"",new byte[8192],true});
            recorder.GetMethod("Stop",Flags).Invoke(instance,new object[]{"queue-test"});
            Require(File.ReadAllBytes(Directory.GetFiles(root,"detail-*.bin").Single()).SequenceEqual(new byte[]{7}),"Already accepted detail survives a later queue overflow.");
            Require(File.ReadAllText(Path.Combine(root,"completion.tsv")).Contains("dropped\t1"),"Overflow denominator counts exactly the rejected item.");
            Require(!(bool)recorder.GetProperty("Accepting",Flags).GetValue(instance),"Stopped recorder ceases producer work.");
            string shared=Path.Combine(output,"shared");Directory.CreateDirectory(shared);
            var a=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(shared,"host"),98304L,65536L,shared},null);
            var b=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(shared,"worker-1"),98304L,65536L,shared},null);
            recorder.GetMethod("Record",Flags).Invoke(a,new object[]{"host",1L,1L,1L,1L,"",new byte[24576],true});
            recorder.GetMethod("Record",Flags).Invoke(b,new object[]{"worker",1L,1L,1L,1L,"",new byte[24576],true});
            System.Threading.Thread.Sleep(1300);
            long total=Directory.GetFiles(shared,"*",SearchOption.AllDirectories).Sum(file=>new FileInfo(file).Length);
            Require(total<=98304,"Combined Host/worker actual disk bytes remain within the shared session limit.");
            Require(Directory.GetFiles(shared,"completion.tsv",SearchOption.AllDirectories).Select(File.ReadAllText).Any(text=>text.Contains("disk-limit")),"Combined accepted records reach the shared disk capacity branch.");
            string manual=Path.Combine(output,"manual-control");Directory.CreateDirectory(manual);
            recorder.GetMethod("Stop",Flags).Invoke(a,new object[]{"finish"});recorder.GetMethod("Stop",Flags).Invoke(b,new object[]{"finish"});
            a=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(manual,"host"),1048576L,65536L,manual},null);
            b=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(manual,"worker-2"),1048576L,65536L,manual},null);
            File.WriteAllText(Path.Combine(manual,"stop.txt"),"stop");System.Threading.Thread.Sleep(1300);
            Require(!(bool)recorder.GetProperty("Accepting",Flags).GetValue(a) && !(bool)recorder.GetProperty("Accepting",Flags).GetValue(b),"Host-root manual stop reaches worker and Host.");
            recorder.GetMethod("Stop",Flags).Invoke(a,new object[]{"finish"});recorder.GetMethod("Stop",Flags).Invoke(b,new object[]{"finish"});
            string bad=Path.Combine(output,"blocked-file");File.WriteAllText(bad,"fixture");
            var broken=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(bad,"child"),65536L,4096L},null);
            recorder.GetMethod("Record",Flags).Invoke(broken,new object[]{"io",1L,1L,1L,1L,"",null,false});
            System.Threading.Thread.Sleep(100);
            Require(!(bool)recorder.GetProperty("Accepting",Flags).GetValue(broken),"IO failure stops diagnostics without a caller exception.");
            recorder.GetMethod("Stop",Flags).Invoke(broken,new object[]{"finish"});
        }
        // Controlled clocks belong only to queued diagnostic DTOs and the
        // private shared deadline fixture. No prediction clock/response is changed.
        private static void BoundaryChecks(Assembly host,Type recorder,string output)
        {
            string root=Path.Combine(output,"long-manifest");Directory.CreateDirectory(root);
            var budgetType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnosticBudget",true);
            var budget=Activator.CreateInstance(budgetType,Flags,null,new object[]{root,131072L},null);
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            budgetType.GetMethod("Trigger",Flags).Invoke(budget,new object[]{now+3600L*System.Diagnostics.Stopwatch.Frequency});
            var instance=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(root,"host"),131072L,1048576L,root},null);
            object gate=recorder.GetField("gate",Flags).GetValue(instance);
            System.Threading.Monitor.Enter(gate);
            try
            {
                for(int i=0;i<181;i++)recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"segment",(long)i,1L,1L,1L,"",new byte[]{(byte)i},true});
                var queue=(System.Collections.IEnumerable)recorder.GetField("queue",Flags).GetValue(instance);int ordinal=0;
                foreach(object item in queue)item.GetType().GetField("Clock",Flags).SetValue(item,now+(ordinal++)*10L*System.Diagnostics.Stopwatch.Frequency);
            }
            finally{System.Threading.Monitor.Exit(gate);}
            recorder.GetMethod("Stop",Flags).Invoke(instance,new object[]{"long-metadata"});
            Require(Directory.GetFiles(Path.Combine(root,"host"),"detail-*.bin").Length==181,"Controlled 181-segment upper bound retained.");
            Require(new FileInfo(Path.Combine(root,"host/manifest.tsv")).Length<20480,"Longest retained manifest fits the documented footer reservation.");
            Require(Directory.GetFiles(root,"*",SearchOption.AllDirectories).Sum(file=>new FileInfo(file).Length)<=131072,"Long manifest and all streams remain inside the shared cumulative reservation.");
            ((IDisposable)budget).Dispose();
            root=Path.Combine(output,"shared-deadline");Directory.CreateDirectory(root);
            var a=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(root,"host"),1048576L,65536L,root},null);
            System.Threading.Thread.Sleep(100);
            budget=Activator.CreateInstance(budgetType,Flags,null,new object[]{root,1048576L},null);
            var view=(System.IO.MemoryMappedFiles.MemoryMappedViewAccessor)budgetType.GetField("view",Flags).GetValue(budget);
            view.Write(24,System.Diagnostics.Stopwatch.GetTimestamp()-1);
            var b=Activator.CreateInstance(recorder,Flags,null,new object[]{Path.Combine(root,"worker-late"),1048576L,65536L,root},null);
            System.Threading.Thread.Sleep(300);
            Require(!(bool)recorder.GetProperty("Accepting",Flags).GetValue(a) && !(bool)recorder.GetProperty("Accepting",Flags).GetValue(b),"Idle Host and late worker share the same expired session deadline.");
            recorder.GetMethod("Stop",Flags).Invoke(a,new object[]{"finish"});recorder.GetMethod("Stop",Flags).Invoke(b,new object[]{"finish"});((IDisposable)budget).Dispose();
            root=Path.Combine(output,"repeated-trigger");instance=Activator.CreateInstance(recorder,Flags,null,new object[]{root,1048576L,65536L},null);
            recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"prior",1L,1L,1L,1L,"",new byte[]{1},true});System.Threading.Thread.Sleep(100);
            recorder.GetMethod("Trigger",Flags).Invoke(instance,new object[]{"first"});System.Threading.Thread.Sleep(100);
            var segments=(System.Collections.IEnumerable)recorder.GetField("segments",Flags).GetValue(instance);
            foreach(object segment in segments){Require((bool)segment.GetType().GetField("Pinned",Flags).GetValue(segment),"Trigger pins existing pre-window segment.");segment.GetType().GetField("Clock",Flags).SetValue(segment,now-400L*System.Diagnostics.Stopwatch.Frequency);}
            recorder.GetMethod("Trigger",Flags).Invoke(instance,new object[]{"second"});System.Threading.Thread.Sleep(100);
            recorder.GetMethod("Record",Flags).Invoke(instance,new object[]{"post",2L,1L,1L,1L,"",new byte[]{2},true});
            recorder.GetMethod("Stop",Flags).Invoke(instance,new object[]{"trigger-test"});
            Require(Directory.GetFiles(root,"detail-*.bin").Length==2,"Repeated trigger never unpins an earlier preserved pre-window.");
        }
        private static void CaptureFailure(Assembly host,string root)
        {
            Terraria.Program.SavePath=Path.Combine(Path.GetFullPath(root),"isolated-save-root");Attach(host,root);typeof(NativeCombatWorkerChecks).GetMethod("Initialize",Flags).Invoke(null,null);NativeCombatWorkerChecks.Scene(false);
            var sessionType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true);
            var cache=new JueMingR.Features.Combat.NpcPredictionCache();
            var session=Activator.CreateInstance(sessionType,Flags,null,new object[]{null,cache},null);
            var slots=sessionType.GetField("npcs",Flags).GetValue(session);slots.GetType().GetMethod("Add").Invoke(slots,new object[]{0});
            var identity=host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{Terraria.Main.npc[0],1L});
            var player=Terraria.Main.player[0];var inventory=player.inventory;
            try
            {
                // Deliberately malformed native input tests original catch and
                // pre-dispose owned partial evidence, never injects a reply.
                player.inventory=null;
                sessionType.GetMethod("Capture",Flags).Invoke(session,new object[]{identity,1000L});
                Require(((string)sessionType.GetProperty("Reason",Flags).GetValue(session)).StartsWith("capture:"),"Original Capture catch retains its failure reason.");
                var terrain=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{1L,0,0,Terraria.Main.maxTilesX-1,Terraria.Main.maxTilesY-1});
                bool refused=false;
                try{host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureProductionValues",Flags).Invoke(null,new object[]{new[]{0},new int[0],0,1001L,180,terrain,new[]{Terraria.Main.npc[0].type},false});}
                catch(TargetInvocationException){refused=true;}
                Require(refused,"Malformed capture input still follows original refusal path.");
            }
            finally{player.inventory=inventory;fixtureRecorder.GetType().GetMethod("Stop",Flags).Invoke(fixtureRecorder,new object[]{"capture-fixture-end"});}
            string timeline=File.ReadAllText(Path.Combine(root,"host/timeline.tsv"));
            Require(timeline.Contains("capture-failed") && timeline.Contains("capture-partial"),"Original Capture failure and pre-disposal partial bytes both survive.");
            Require(File.ReadAllText(Path.Combine(root,"host/completion.tsv")).Contains("dropped\t0"),"Failure evidence is retained without diagnostic loss.");
            Console.WriteLine("PASS malformed-input mechanism fixture: real Capture catch and Fill pre-dispose partial; no fabricated worker response.");
        }
        private static object fixtureRecorder;
        internal static void Attach(Assembly host,string root)
        {
            var diagnostic=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnostics",true);
            root=Path.GetFullPath(root);Directory.CreateDirectory(root);
            diagnostic.GetField("Root",Flags).SetValue(null,root);
            fixtureRecorder=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnosticRecorder",true),Flags,null,new object[]{Path.Combine(root,"host"),8L*1024*1024*1024,32L*1024*1024,root},null);
            diagnostic.GetField("recorder",Flags).SetValue(null,fixtureRecorder);
        }
        internal static void Visual(string content,string output)
        {
            Directory.CreateDirectory(output);
            string hostPath=Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            var host=Assembly.LoadFrom(hostPath);string root=Path.GetFullPath(Path.Combine(output,"session"));Attach(host,root);
            try{NativeQuickItemChecks.Run(context=>{using(var graphics=new ProbeGraphics(content)){Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");NativeCombatVisualChecks.World(context,graphics,output,true);}},processing:true,shortFeedback:true,candidateAssembly:hostPath);}
            finally{fixtureRecorder.GetType().GetMethod("Stop",Flags).Invoke(fixtureRecorder,new object[]{"visual-fixture-end"});}
            string timeline=File.ReadAllText(Path.Combine(root,"host/timeline.tsv"));
            Require(timeline.Contains("presentation-ready") && timeline.Split('\n').Any(line=>line.Contains("text-draw") && line.Contains("path=True") && line.Contains("drawn=True")) && timeline.Contains("draw-completed"),"Actual XNB font and path Draw evidence is retained before the display-off Draw.");
            Console.WriteLine("PASS actual native graphics World path/text Draw followed by display-off gap; fixture is not field cause evidence.");
        }
        internal static void Session(string output)
        {
            string root=Path.GetFullPath(Path.Combine(output,"session"));Environment.SetEnvironmentVariable("JMR_AIM_DIAGNOSTIC_FIXTURE_ROOT",root);
            Environment.SetEnvironmentVariable("JUEMINGR_NPC_SESSION_CAPACITY","1");
            try{NativeCombatWorkerChecks.Run(Path.Combine(output,"production"),productionOnly:true,content:"--cpu");}
            finally{fixtureRecorder?.GetType().GetMethod("Stop",Flags).Invoke(fixtureRecorder,new object[]{"fixture-session-end"});}
            string timeline=File.ReadAllText(Path.Combine(root,"host/timeline.tsv"));
            Require(timeline.Contains("request-raw") && timeline.Contains("terrain-pages") && timeline.Contains("history-compare") && timeline.Contains("request-retired") && timeline.Contains("acceptance"),"Real Session observes raw values, owned terrain, history, retirement and actual acceptance without injected replies.");
            Require(File.ReadAllText(Path.Combine(root,"host/completion.tsv")).Contains("dropped\t0"),"Real Session detailed queue loss is explicitly checked.");
            Require(timeline.Contains("presentation-begin") && timeline.Contains("presentation-cache") && timeline.Contains("presentation-ready"),"Real Host Prepare records cache-to-path and text preparation; CPU fixture does not execute GPU Draw.");
            Console.WriteLine("PASS real Session original capture/receive/history/retired delayed capacity reply diagnostics and real Prepare; GPU Draw remains unexecuted.");
        }
        internal static void Load(string output,bool off=false)
        {
            Directory.CreateDirectory(output);
            string path=Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            string alternate=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_BUILD");if(!string.IsNullOrEmpty(alternate))path=Path.Combine(alternate,"JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            var host=Assembly.LoadFrom(path);var diagnostic=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnostics");
            if(off)Require(diagnostic==null && !host.GetTypes().Any(type=>type.Name.StartsWith("AimDiagnostic",StringComparison.Ordinal)),"Ordinary Host contains no temporary diagnostic types.");
            else Require(diagnostic!=null,"Load test requires the diagnostic variant.");
            string expected=(string)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeMaterialIdentity",true).GetMethod("Expected",Flags).Invoke(null,null);Require(off?expected=="74811F6F671B5D51E1D12426479D096D9050CA443B1D81507B07FE395F5456B3":expected==null,"Ordinary approved material remains reusable; diagnostic private image is excluded from ordinary approval.");
            typeof(NativeCombatWorkerChecks).GetMethod("Initialize",Flags).Invoke(null,null);NativeCombatWorkerChecks.Scene(false);
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);var observe=alignment.GetMethod("Observe",Flags);
            string session=Path.GetFullPath(Path.Combine(output,"load-session"));object recorder=null;
            if(!off)
            {
                Directory.CreateDirectory(session);diagnostic.GetField("Root",Flags).SetValue(null,session);
                recorder=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimDiagnosticRecorder",true),Flags,null,new object[]{Path.Combine(session,"host"),8L*1024*1024*1024,32L*1024*1024,session},null);
                diagnostic.GetField("recorder",Flags).SetValue(null,recorder);
            }
            var first=observe.Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            var frame=first.GetType();
            File.WriteAllText(Path.Combine(output,"hashes.txt"),frame.GetField("World",Flags).GetValue(first)+"\n"+string.Join(",",(ulong[])frame.GetField("NpcState",Flags).GetValue(first))+"\n"+string.Join(",",(ulong[])frame.GetField("PlayerPremise",Flags).GetValue(first)));
            if(off){Console.WriteLine("PASS ordinary assembly exclusion and native acceptance hash sample.");return;}
            for(int i=1;i<10;i++){Terraria.Main.npc[i].SetDefaults(i%2==0?1:3);Terraria.Main.npc[i].whoAmI=i;Terraria.Main.npc[i].active=true;}
            for(int i=0;i<10;i++){Terraria.Main.projectile[i].SetDefaults(1);Terraria.Main.projectile[i].whoAmI=i;Terraria.Main.projectile[i].active=true;}
            int[] ns=Enumerable.Range(0,10).ToArray(),ps=Enumerable.Range(0,10).ToArray();
            var costs=new System.Collections.Generic.List<double>();var timer=System.Diagnostics.Stopwatch.StartNew();int samples=0;
            int seconds=int.TryParse(Environment.GetEnvironmentVariable("JMR_AIM_LOAD_SECONDS"),out int requested)?requested:10;
            while(timer.Elapsed.TotalSeconds<seconds)
            {
                var copied=System.Diagnostics.Stopwatch.StartNew();observe.Invoke(null,new object[]{1001L+samples,ns,ps,0});costs.Add(copied.Elapsed.TotalMilliseconds);samples++;
                double wait=samples/60.0-timer.Elapsed.TotalSeconds;if(wait>0)System.Threading.Thread.Sleep((int)(wait*1000));
            }
            double sampleSeconds=timer.Elapsed.TotalSeconds;costs.Sort();
            if(Environment.GetEnvironmentVariable("JMR_AIM_REAL_WORKER")=="1")
            {
                Environment.SetEnvironmentVariable("JMR_AIM_DIAGNOSTIC_ROOT",session);Environment.SetEnvironmentVariable("JMR_AIM_DIAGNOSTIC_GENERATION","100");
                NativeCombatWorkerChecks.Run(Path.Combine(output,"real-worker"),transportOnly:true,content:"--cpu");
                Require(Directory.GetDirectories(session,"worker-*").Any(),"Real worker diagnostics must actually initialize under the shared absolute session root.");
                Burst(host,Path.Combine(output,"prediction-worker-layout"),session);
            }
            recorder.GetType().GetMethod("Stop",Flags).Invoke(recorder,new object[]{"load-complete"});
            string completion=File.ReadAllText(Path.Combine(session,"host/completion.tsv"));
            File.WriteAllText(Path.Combine(output,"load.txt"),"samples="+samples+";actualHz="+(samples/sampleSeconds)+";copyAverageMs="+costs.Average()+";copyMaxMs="+costs.Max()+";copyP50Ms="+costs[costs.Count/2]+";copyP95Ms="+costs[(int)(costs.Count*.95)]+";sampleSeconds="+sampleSeconds+";totalSeconds="+timer.Elapsed.TotalSeconds+";files="+Directory.GetFiles(session,"*",SearchOption.AllDirectories).Length+";"+completion);
            Require(completion.Contains("dropped\t0") && completion.Contains("detailStopped\tFalse"),"Representative 10-NPC/10-projectile native field collection must retain all captured frames within its queue limit; achieved Hz is separately reported.");
            string[] rows=File.ReadAllLines(Path.Combine(session,"host/timeline.tsv"));
            Require(rows.Any(row=>row.Contains("alignment-fields")),"Load invokes real native acceptance-field observation.");
            Console.WriteLine("PASS real native 10 NPC/10 projectile acceptance keys; "+samples+" samples; "+timer.Elapsed.TotalSeconds.ToString("F2")+"s; "+completion.Replace("\n",";"));
        }
        private static void Burst(Assembly host,string layout,string session)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWorkerClient",true);
            string[] names=(string[])type.GetField("PayloadNames",Flags).GetValue(null);
            string[] hashes=names.Select(name=>{using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(layout,name)))).Replace("-","");}).ToArray();
            var client=Activator.CreateInstance(type,Flags,null,new object[]{layout,Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"),hashes,60000,10000},null);
            Func<int> state=()=> (int)type.GetProperty("State",Flags).GetValue(client);
            Action<int> wait=wanted=>{var watch=System.Diagnostics.Stopwatch.StartNew();while(state()!=wanted && state()!=4 && watch.ElapsedMilliseconds<65000)System.Threading.Thread.Sleep(10);Require(state()==wanted,"Diagnostic real burst transport reaches "+wanted+"; failure="+type.GetProperty("Failure",Flags).GetValue(client));};
            try
            {
                wait(1);NativeCombatWorkerChecks.Scene(false);
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                byte[] request=(byte[])capture.Invoke(null,new object[]{new[]{0},new[]{0,1,2},0,1000L,180});
                Require((bool)type.GetMethod("TrySend",Flags).Invoke(client,new object[]{request}),"Actual anonymous transport accepts one 180-step capture.");wait(3);
                Require(type.GetMethod("TryTake",Flags).Invoke(client,null)!=null,"Actual 181-frame result consumed once.");
                File.WriteAllText(Path.Combine(session,"stop.txt"),"load diagnostic stop only");System.Threading.Thread.Sleep(1600);
                string[] completions=Directory.GetFiles(session,"completion.tsv",SearchOption.AllDirectories);
                Require(completions.Any(file=>Path.GetDirectoryName(file).Contains("worker-")),"Worker writer drains before the original forced process-stop path.");
                foreach(string file in completions)Require(File.ReadAllText(file).Contains("dropped\t0") && File.ReadAllText(file).Contains("detailStopped\tFalse"),"Shared real burst retains queued diagnostics without loss: "+file);
                string worker=Directory.GetFiles(session,"timeline.tsv",SearchOption.AllDirectories).Select(File.ReadAllText).First(text=>text.Contains("worker-receive") && text.Contains("worker-completed"));
                Require(worker.Contains("alignment-frame"),"Actual worker produces the state proof side channel.");
            }
            finally{type.GetMethod("Stop",Flags).Invoke(client,null);}
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
