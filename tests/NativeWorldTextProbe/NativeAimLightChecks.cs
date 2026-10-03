using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace NativeWorldTextProbe
{
    // File/queue failure checks use only a disposable root. No game is started.
    internal static class NativeAimLightChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(string output)
        {
            Directory.CreateDirectory(output);
            var host=Assembly.LoadFrom(Environment.GetEnvironmentVariable("JUEMINGR_AIM_LIGHT_HOST"));
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimLightLog");
            Require(type!=null,"Diagnostic build must contain the bounded light recorder.");
            string root=Path.Combine(output,"normal");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"arm.txt"),"test");
            var log=Activator.CreateInstance(type,Flags,null,new object[]{root,"test"},null);
            for(int i=0;i<100 && !(bool)type.GetProperty("Accepting",Flags).GetValue(log);i++)Thread.Sleep(10);
            Require((bool)type.GetProperty("Accepting",Flags).GetValue(log),"Only the authenticated arm enables producer work.");
            var trace=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace");trace.GetField("log",Flags).SetValue(null,log);
            var frameType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment+Frame");
            var expected=Activator.CreateInstance(frameType,true);var actual=Activator.CreateInstance(frameType,true);
            foreach(var frame in new[]{expected,actual})
            {frameType.GetField("Npcs",Flags).SetValue(frame,new[]{7});frameType.GetField("NpcState",Flags).SetValue(frame,new ulong[]{ReferenceEquals(frame,expected)?111UL:222UL});frameType.GetField("NpcShootClock",Flags).SetValue(frame,new[]{ReferenceEquals(frame,expected)?12f:13f});}
            trace.GetMethod("Difference",Flags).Invoke(null,new object[]{5L,2,"NPC slot=7",expected,actual});
            trace.GetMethod("Difference",Flags).Invoke(null,new object[]{5L,2,"NPC sampled shooting clock slot=7",expected,actual});
            Call(log,"Offer","before-ready",1L,"worker=starting");
            for(int i=0;i<12;i++)Call(log,"Sample","world1:type110",(long)i,true,i%2==0,false,i%2==0,i%2==0);
            Call(log,"Sample","world1:type204",12L,true,false,false,false,false);
            Call(log,"Sample","world1:type110",13L,true,true,false,true,true);
            Call(log,"Sample","world1:collision",14L,false,false,true,true,false);
            Call(log,"Sample","world1:type110",15L,true,true,false,false,false);
            // Read a flushed live prefix before Stop exists. A truncated final
            // row is excluded; every earlier row retains its parseable shape.
            Thread.Sleep(1100);
            string liveFile=Directory.GetFiles(root,"trace.tsv",SearchOption.AllDirectories).Single();
            string live;using(var stream=new FileStream(liveFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(stream))live=reader.ReadToEnd();
            Require(live.Contains("before-ready") && !live.Contains("\tend\t"),"The active file is readable without Stop.");
            string partial=live+"999\ttruncated";
            var complete=partial.Substring(0,partial.LastIndexOf('\n')).Split('\n');
            Require(complete.Length>2 && complete.All(row=>row.Split('\t').Length==5),"Incomplete tail cannot corrupt preceding complete records.");
            // Hold only the recorder's queue lock: the writer cannot drain it,
            // while the same producer can fill the real bounded queue.
            var gate=type.GetField("gate",Flags).GetValue(log);
            lock(gate)for(int i=0;i<600;i++)Call(log,"Offer","burst",20L,"bounded");
            Require((long)type.GetField("Dropped",Flags).GetValue(log)>0,"Full queue records loss without throwing to its caller.");
            Call(log,"Stop","test-end");Wait(log);
            string file=Directory.GetFiles(root,"trace.tsv",SearchOption.AllDirectories).Single();
            string text=File.ReadAllText(file);
            Require(text.Contains("before-ready") && text.Contains("scene-summary") && text.Contains("blankSegments=6"),"Not-ready, short repeated gaps and switch-away retain their own denominators.");
            Require(text.Contains("cacheWithoutPrepare=1") && text.Contains("collision=1"),"Cache without Prepare and collision-only are separate observations.");
            Require(text.Contains("end") && text.Contains("dropped="),"Readable completion accounts for loss.");
            Require(text.Contains("slot=7;expectedHash=111;actualHash=222") && text.Contains("slot=7;expectedClock=12;actualClock=13"),"Existing slot= categories preserve the actual compared hashes or shooting clocks.");
            var bytes=File.ReadAllBytes(file);Require(bytes.Length<16*1024*1024,"File bound holds.");
            File.WriteAllText(Path.Combine(output,"interrupted.tsv"),partial);
            var again=Activator.CreateInstance(type,Flags,null,new object[]{root,"test"},null);Wait(again);
            Require((long)type.GetField("started",Flags).GetValue(again)==0,"Missing arm never starts the diagnostic clock.");
            string mismatch=Path.Combine(output,"mismatch");Directory.CreateDirectory(mismatch);File.WriteAllText(Path.Combine(mismatch,"arm.txt"),"wrong-host");
            var mismatchLog=Activator.CreateInstance(type,Flags,null,new object[]{mismatch,"test"},null);Call(mismatchLog,"Offer","must-not-record",0L,"unarmed");Wait(mismatchLog);
            Require((long)type.GetField("started",Flags).GetValue(mismatchLog)==0 && (long)type.GetField("sequence",Flags).GetValue(mismatchLog)==0,"Wrong arm never starts the clock or queues producer records.");
            Require(Directory.GetFiles(root,"trace.tsv",SearchOption.AllDirectories).Length==1,"Ordinary restart cannot re-arm consumed collection.");
            string blocked=Path.Combine(output,"blocked");File.WriteAllText(blocked,"file, not directory");
            var fault=Activator.CreateInstance(type,Flags,null,new object[]{blocked,"test"},null);
            Call(fault,"Offer","business-result",42L,"accepted");Wait(fault);
            Require((string)type.GetField("Failure",Flags).GetValue(fault)!=null,"Filesystem failure stays in diagnostic status.");
            foreach(string limit in new[]{"time","file"})
            {
                string bounded=Path.Combine(output,limit);Directory.CreateDirectory(bounded);File.WriteAllText(Path.Combine(bounded,"arm.txt"),"test");
                if(limit=="file"){FileLimit(type,bounded);continue;}
                var limited=Activator.CreateInstance(type,Flags,null,new object[]{bounded,"test"},null);
                for(int i=0;i<100 && Directory.GetFiles(bounded,"trace.tsv",SearchOption.AllDirectories).Length==0;i++)Thread.Sleep(10);
                type.GetField("started",Flags).SetValue(limited,System.Diagnostics.Stopwatch.GetTimestamp()-601*System.Diagnostics.Stopwatch.Frequency);
                Wait(limited);
                string boundedText=File.ReadAllText(Directory.GetFiles(bounded,"trace.tsv",SearchOption.AllDirectories).Single());
                Require(boundedText.Contains("reason="+limit+"-limit"),"Recorder stops automatically at the actual "+limit+" guard.");
            }
            Console.WriteLine("PASS light recorder: bounded queue, scene gaps, no Prepare, collision, consumed arm, write failure and readable prefix");
        }
        private static void FileLimit(Type type,string root)
        {
            using(var describing=new ManualResetEventSlim())using(var proceed=new ManualResetEventSlim())
            {
                // Pause at the existing identity callback, before armed and
                // before the first dequeue. Hold the queue lock while arming,
                // so the final row must be queued before the real limit check.
                Func<string> describe=()=>{describing.Set();if(!proceed.Wait(2000))throw new TimeoutException("Fixture identity barrier.");return "fixture=file-limit";};
                var log=Activator.CreateInstance(type,Flags,null,new object[]{root,"test",describe},null);
                try
                {
                    Require(describing.Wait(2000),"Writer reaches the pre-arm identity barrier.");
                    lock(type.GetField("gate",Flags).GetValue(log))
                    {
                        proceed.Set();
                        for(int i=0;i<1000 && !(bool)type.GetProperty("Accepting",Flags).GetValue(log);i++)Thread.Sleep(1);
                        Require((bool)type.GetProperty("Accepting",Flags).GetValue(log),"File-limit Offer is after authenticated arming.");
                        Call(log,"Offer","force-limit",0L,"last-row");
                        Require((long)type.GetField("sequence",Flags).GetValue(log)==1,"The final row is actually enqueued.");
                        type.GetField("bytes",Flags).SetValue(log,16L*1024*1024-4096);
                    }
                    Wait(log);
                    string[] lines=File.ReadAllLines(Directory.GetFiles(root,"trace.tsv",SearchOption.AllDirectories).Single());
                    string footer=lines.Single(line=>line.Contains("\tend\t"));
                    long unwritten=long.Parse(footer.Split('\t')[4].Split(';').Single(field=>field.StartsWith("unwritten=",StringComparison.Ordinal)).Split('=')[1]);
                    long dropped=(long)type.GetField("Dropped",Flags).GetValue(log),highWater=(long)type.GetField("HighWater",Flags).GetValue(log);
                    int written=lines.Count(line=>line.Contains("\tforce-limit\t"));
                    int queued=((System.Collections.ICollection)type.GetField("queue",Flags).GetValue(log)).Count;
                    Console.WriteLine("FILE-LIMIT offered=1 enqueued=1 written="+written+" dropped="+dropped+" unwritten="+unwritten+" queued="+queued+" highWater="+highWater);
                    Require(footer.Contains("reason=file-limit") && highWater==1 && queued==0 && written==0 && dropped==0,"The writer dequeues the last queued row and the actual file guard refuses it.");
                    Require(unwritten==1 && written+dropped+unwritten==1,"The dequeued refused row remains in the complete loss account.");
                }
                finally{proceed.Set();}
            }
        }
        private static void Wait(object log)
        {for(int i=0;i<100 && !(bool)log.GetType().GetProperty("Closed",Flags).GetValue(log);i++)Thread.Sleep(20);Require((bool)log.GetType().GetProperty("Closed",Flags).GetValue(log),"Recorder closes within bounded check.");}
        private static void Call(object value,string method,params object[] args){value.GetType().GetMethod(method,Flags).Invoke(value,args);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
