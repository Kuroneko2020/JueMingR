#if JMR_AIM_LIGHT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // One disposable Host-side recorder, not a second business state owner.
    // Producers never wait for disk or for this lock. Loss is explicit, and
    // complete newline-terminated records remain useful without the footer.
    internal sealed class AimLightLog
    {
        internal const int QueueLimit=256,RecordLimit=4096,FileLimit=16*1024*1024,SecondsLimit=600;
        private readonly object gate=new object();
        private readonly Queue<string> queue=new Queue<string>();
        private readonly Dictionary<string,Scene> scenes=new Dictionary<string,Scene>();
        private readonly Dictionary<string,FailureCount> failures=new Dictionary<string,FailureCount>();
        private readonly Dictionary<string,long> counts=new Dictionary<string,long>();
        private readonly string root,identity;
        private readonly Func<string> describe;
        private long started;
        private int armed,stopping,closed;
        private long sequence,bytes;
        private string ending="unknown-tail",lastScene;
        internal long Dropped,HighWater;
        internal string Failure;
        internal bool Closed=>Volatile.Read(ref closed)!=0;
        internal bool Accepting=>Volatile.Read(ref armed)!=0 && Volatile.Read(ref stopping)==0 && !Closed;
        private sealed class Scene
        {
            internal long Ticks,Demand,Blank,Gap,Longest,Segments,Short,Collision,CacheWithoutPrepare,CacheWithoutDraw,LastTick;
        }
        private sealed class FailureCount {internal long Count,First,Last;}
        internal AimLightLog(string root,string identity)
            :this(root,identity,null){}
        internal AimLightLog(string root,string identity,Func<string> describe)
        {this.root=root;this.identity=identity;this.describe=describe;new Thread(Run){IsBackground=true,Name="JueMingR light observation"}.Start();}
        internal void Offer(string kind,long tick,string detail)
        {
            if(!Accepting)return;
            bool entered=false;
            try
            {
                if(!(entered=Monitor.TryEnter(gate))){Interlocked.Increment(ref Dropped);return;}
                Enqueue(kind,tick,detail);
            }
            catch(Exception error){Abort(error);}
            finally{if(entered)Monitor.Exit(gate);}
        }
        internal void First(string kind,long tick,string detail)
        {
            if(!Accepting)return;
            bool entered=false;
            try
            {
                if(!(entered=Monitor.TryEnter(gate))){Interlocked.Increment(ref Dropped);return;}
                FailureCount value;
                if(!failures.TryGetValue(kind,out value))
                {
                    if(failures.Count>=64){kind="other-failure";if(failures.TryGetValue(kind,out value)){value.Count++;value.Last=tick;return;}}
                    value=new FailureCount{First=tick};failures[kind]=value;Enqueue("first-failure",tick,kind+";"+detail);
                }
                value.Count++;value.Last=tick;
            }
            catch(Exception error){Abort(error);}
            finally{if(entered)Monitor.Exit(gate);}
        }
        internal void Sample(string scene,long tick,bool demand,bool cache,bool collision,bool prepare,bool draw)
        {
            if(!Accepting)return;
            bool entered=false;
            try
            {
                if(!(entered=Monitor.TryEnter(gate))){Interlocked.Increment(ref Dropped);return;}
                if(lastScene!=scene && lastScene!=null)CloseGap(scenes[lastScene]);
                Scene value;
                if(!scenes.TryGetValue(scene,out value))
                {if(scenes.Count>=64)scene="other-scene";if(!scenes.TryGetValue(scene,out value))scenes[scene]=value=new Scene();}
                lastScene=scene;value.Ticks++;value.LastTick=tick;
                if(demand)
                {
                    value.Demand++;
                    if(!cache){value.Blank++;if(value.Gap++==0)value.Segments++;value.Longest=Math.Max(value.Longest,value.Gap);}
                    else CloseGap(value);
                }
                else CloseGap(value);
                if(collision)value.Collision++;
                if(cache && !prepare)value.CacheWithoutPrepare++;
                if(cache && !draw)value.CacheWithoutDraw++;
            }
            catch(Exception error){Abort(error);}
            finally{if(entered)Monitor.Exit(gate);}
        }
        private static void CloseGap(Scene value){if(value.Gap>0 && value.Gap<=60)value.Short++;value.Gap=0;}
        private void Enqueue(string kind,long tick,string detail)
        {
            long total;counts.TryGetValue(kind,out total);counts[kind]=total+1;
            if(queue.Count>=QueueLimit){Interlocked.Increment(ref Dropped);return;}
            // Escape after bounding the input; arbitrary exception text cannot
            // turn one event into fake rows or an unbounded temporary string.
            bool truncated=detail!=null && detail.Length>RecordLimit-256;
            if(truncated)detail=detail.Substring(0,RecordLimit-256);
            string line=(++sequence).ToString(CultureInfo.InvariantCulture)+"\t"+Elapsed().ToString(CultureInfo.InvariantCulture)+"\t"+tick.ToString(CultureInfo.InvariantCulture)+"\t"+kind+"\t"+Escape(detail)+(truncated?";truncated=true":"");
            if(line.Length>RecordLimit)line=line.Substring(0,RecordLimit-20)+";truncated=true";
            queue.Enqueue(line);HighWater=Math.Max(HighWater,queue.Count);
        }
        private static string Escape(string text){return (text??"unknown").Replace("\\","\\\\").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t");}
        private long Elapsed(){return (Stopwatch.GetTimestamp()-started)*1000/Stopwatch.Frequency;}
        internal void Stop(string reason){ending=reason;Interlocked.Exchange(ref stopping,1);}
        internal void Abort(Exception error)
        {
            // Even formatting the observer's own failure is best effort.
            try{Failure=error.GetType().FullName;ending="observer-failure";}catch{}
            Interlocked.Exchange(ref stopping,1);
        }
        private bool Write(Stream stream,string line)
        {
            var data=Encoding.UTF8.GetBytes(line+"\n");
            if(data.Length>FileLimit-4096-bytes){Stop("file-limit");return false;}
            stream.Write(data,0,data.Length);bytes+=data.Length;return true;
        }
        private List<string> Summaries(bool final)
        {
            var result=new List<string>();
            lock(gate)
            {
                foreach(var pair in scenes)
                {
                    var s=pair.Value;if(final)CloseGap(s);
                    result.Add("0\t"+Elapsed()+"\t"+s.LastTick+"\tscene-summary\t"+pair.Key+";ticks="+s.Ticks+";demand="+s.Demand+";blank="+s.Blank+";blankSegments="+s.Segments+";shortGaps="+s.Short+";currentGap="+s.Gap+";longest="+s.Longest+";collision="+s.Collision+";cacheWithoutPrepare="+s.CacheWithoutPrepare+";cacheWithoutDraw="+s.CacheWithoutDraw);
                }
                foreach(var pair in failures){var f=pair.Value;result.Add("0\t"+Elapsed()+"\t"+f.Last+"\tfailure-summary\t"+Escape(pair.Key)+";count="+f.Count+";first="+f.First+";last="+f.Last);}
                foreach(var pair in counts)result.Add("0\t"+Elapsed()+"\t-1\tstage-total\tstage="+Escape(pair.Key)+";count="+pair.Value);
                result.Add("0\t"+Elapsed()+"\t-1\twriter-summary\tdropped="+Interlocked.Read(ref Dropped)+";highWater="+HighWater+";queued="+queue.Count+";bytes="+bytes+";tail=unknown-until-end");
            }
            return result;
        }
        private void Run()
        {
            try
            {
                // The installer arms precisely this authenticated Host hash.
                // Claim once before creating output; a normal restart cannot
                // silently resume. Missing arm is OFF, not a product failure.
                string arm=Path.Combine(root,"arm.txt");
                if(!Directory.Exists(root)){if(File.Exists(root))throw new IOException("Diagnostic root is a file.");return;}
                if(!File.Exists(arm))return;
                string run=DateTime.UtcNow.ToString("yyyyMMddTHHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N");
                string used=Path.Combine(root,"used-"+run+".txt");File.Move(arm,used);
                var info=new FileInfo(used);if(info.Length>512 || File.ReadAllText(used).Trim()!=identity)throw new InvalidDataException("Diagnostic arm identity mismatch.");
                started=Stopwatch.GetTimestamp();
                string directory=Path.Combine(root,run);Directory.CreateDirectory(directory);
                using(var stream=new FileStream(Path.Combine(directory,"trace.tsv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096))
                {
                    Write(stream,"0\t0\t-1\tstart\tformat=JueMingR.AimLight.1;host="+identity+";utc="+DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+";queue=256;recordChars=4096;maxBytes=16777216;seconds=600;workerInternals=not-recorded;tail=unknown-until-end");
                    if(describe!=null)Write(stream,"0\t0\t-1\tidentity\t"+Escape(describe()));
                    // No producer formatting/counters before the async arm is
                    // authenticated. A delayed arm cannot claim earlier history.
                    Volatile.Write(ref armed,1);
                    long next=0;int dequeuedUnwritten=0;
                    while(true)
                    {
                        if(Elapsed()>=SecondsLimit*1000)Stop("time-limit");
                        string row=null;lock(gate){if(queue.Count!=0)row=queue.Dequeue();}
                        // This row already left the queue. A file-limit refusal
                        // must remain in the footer's producer-record loss count.
                        if(row!=null && !Write(stream,row)){dequeuedUnwritten=1;break;}
                        if(Elapsed()>=next){foreach(string summary in Summaries(false))if(!Write(stream,summary))break;stream.Flush();next=Elapsed()+1000;}
                        if(Volatile.Read(ref stopping)!=0 && row==null)break;
                        if(row==null)Thread.Sleep(25);
                    }
                    foreach(string summary in Summaries(true))if(!Write(stream,summary))break;
                    int remaining;lock(gate)remaining=queue.Count+dequeuedUnwritten;
                    string footer="0\t"+Elapsed()+"\t-1\tend\treason="+Escape(ending)+";observerFailure="+Escape(Failure)+";dropped="+Interlocked.Read(ref Dropped)+";unwritten="+remaining+";bytesBeforeEnd="+bytes+";businessTail=unobserved-after-stop\n";
                    byte[] tail=Encoding.UTF8.GetBytes(footer);stream.Write(tail,0,tail.Length);stream.Flush();
                }
            }
            catch(Exception error){Abort(error);}
            finally{Interlocked.Exchange(ref stopping,1);Volatile.Write(ref closed,1);}
        }
    }
}
#endif
