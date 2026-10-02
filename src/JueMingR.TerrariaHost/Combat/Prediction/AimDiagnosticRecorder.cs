#if JMR_AIM_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // This passive owner receives only immutable strings and cloned bytes.
    // Its writer owns rolling files and a process-shared cumulative disk budget.
    // Reservations count all streams and never refund rolled files: this is a
    // conservative total-write bound as well as an upper bound on retained data.
    internal sealed class AimDiagnosticRecorder
    {
        private sealed class Item
        {internal string Kind,Detail;internal DateTime Utc;internal long Tick,Request,Generation,Sequence,Clock;internal byte[] Bytes;internal bool Detailed,Text;internal int Cost;}
        private sealed class Segment
        {internal string Name;internal long Clock;internal bool Pinned;}
        private readonly object gate=new object();
        private readonly Queue<Item> queue=new Queue<Item>();
        private readonly AutoResetEvent wake=new AutoResetEvent(false);
        private readonly Thread writer;
        private readonly string root,sessionRoot;
        private readonly long diskLimit,queueLimit,started=Stopwatch.GetTimestamp();
        private long queued,highWater,dropped,ordinal,fileId,triggerUntil,lastControl,lastMark,bytesCopied,writeBatches;
        private double previousWriteMs,writerTotalMs,writerMaximumMs,recordOwnedTotalMs;
        private bool stopping,detailStopped;
        private bool metadataReserved;
        private string stopReason="process-exit-unconfirmed",failure,pendingTrigger;
        private readonly List<Segment> segments=new List<Segment>();
        internal bool Accepting {get{lock(gate)return !stopping;}}
        internal bool Detailed {get{lock(gate)return !stopping && !detailStopped;}}
        internal AimDiagnosticRecorder(string root,long diskLimit,long queueLimit):this(root,diskLimit,queueLimit,root){}
        internal AimDiagnosticRecorder(string root,long diskLimit,long queueLimit,string sessionRoot)
        {
            this.root=Path.GetFullPath(root);this.sessionRoot=Path.GetFullPath(sessionRoot);this.diskLimit=diskLimit;this.queueLimit=queueLimit;
            if(diskLimit<1 || queueLimit<1 || Directory.Exists(this.root))throw new ArgumentException("New bounded diagnostic directory required.");
            writer=new Thread(WriteLoop){IsBackground=true,Name="JueMingR aim diagnostics writer"};writer.Start();
        }
        internal void Record(string kind,long tick,long request,long generation,long sequence,string detail,byte[] bytes,bool detailed)
        {
            long recordStart=Stopwatch.GetTimestamp();
            try
            {
                lock(gate)
                {
                    if(stopping || detailed && detailStopped)return;
                    int size=checked((bytes?.Length??0)+(detail?.Length??0)*2+(kind?.Length??0)*2+512);
                    if(size>queueLimit-queued){dropped++;if(detailed)detailStopped=true;wake.Set();return;}
                    queue.Enqueue(new Item{Kind=kind,Tick=tick,Request=request,Generation=generation,Sequence=sequence,Detail=detail??"",Bytes=bytes==null?null:(byte[])bytes.Clone(),Detailed=detailed,Text=detailed && bytes==null,Clock=Stopwatch.GetTimestamp(),Utc=DateTime.UtcNow,Cost=size});
                    queued+=size;bytesCopied+=size;highWater=Math.Max(highWater,queued);wake.Set();
                }
            }
            catch(Exception error){lock(gate){dropped++;detailStopped=true;if(failure==null)failure="record-copy: "+error.GetType().Name;}}
            finally{lock(gate)recordOwnedTotalMs+=(Stopwatch.GetTimestamp()-recordStart)*1000.0/Stopwatch.Frequency;}
        }
        internal void Missing(string stage,Exception error)
        {lock(gate){dropped++;detailStopped=true;if(failure==null)failure=stage+": "+error.GetType().Name+": "+Bounded(error.Message);}Record("diagnostic-loss",-1,0,0,0,stage,null,false);}
        internal void Trigger(string reason)
        {lock(gate){if(stopping)return;pendingTrigger=reason;wake.Set();}Record("trigger",-1,0,0,0,reason,null,false);}
        internal void Stop(string reason)
        {lock(gate){if(!stopping){stopReason=reason;stopping=true;wake.Set();}}if(Thread.CurrentThread!=writer)writer.Join(2000);}
        private void WriteLoop()
        {
            try
            {
                Directory.CreateDirectory(root);
                using(var budget=new AimDiagnosticBudget(sessionRoot,diskLimit))
                using(var timeline=new StreamWriter(Path.Combine(root,"timeline.tsv"),false,new UTF8Encoding(false)))
                using(var index=new StreamWriter(Path.Combine(root,"files.tsv"),false,new UTF8Encoding(false)))
                {
                    // Reserve bounded headers/footer even if capacity later ends.
                    // <=181 ten-second segments in the shared 1800-second session:
                    // manifest <=20KiB, all roll-index rows <=9KiB, headers,
                    // completion and fixed ledger <=3KiB. Reserve all footer
                    // bytes before creating nonempty streams, even at capacity.
                    if(!budget.Reserve(32768)){budget.RequestStop();StopFromWriter("disk-limit");return;}
                    metadataReserved=true;
                    timeline.WriteLine("ordinal\tutc\tmonotonic\ttick\trequest\tgeneration\tsequence\tkind\tdetail\tfile\toffset\tlength\tencoding\tqueueBytes\tdropped\tpreviousWriteMs");
                    index.WriteLine("file\toffset\tlength\tsha256\tencoding\tstate");
                    File.WriteAllText(Path.Combine(root,"session.tsv"),"format\taim-diagnostics-v2\nprocess\t"+Process.GetCurrentProcess().Id+"\nprocessStartUtc\t"+Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("o")+"\nfrequency\t"+Stopwatch.Frequency+"\nstartedUtc\t"+DateTime.UtcNow.ToString("o")+"\nqueueLimit\t"+queueLimit+"\nsessionDiskLimit\t"+diskLimit+"\nmaxSeconds\t1800\npreSeconds\t180\npostSeconds\t180\n",new UTF8Encoding(false));
                    Segment segment=null;FileStream detailStream=null;
                    try
                    {
                        while(true)
                        {
                            Controls(budget);
                            if(budget.Expired(Stopwatch.GetTimestamp())){budget.RequestStop();StopFromWriter("duration-limit");}
                            Item item=null;lock(gate){if(queue.Count>0){item=queue.Dequeue();queued-=item.Cost;}else if(stopping)break;}
                            if(item==null){wake.WaitOne(100);continue;}
                            long before=Stopwatch.GetTimestamp();string relative="",encoding="raw";long offset=0,length=0;
                            string summary=item.Text?"copied-text-bytes="+Encoding.UTF8.GetByteCount(item.Detail):item.Detail;
                            byte[] data=null;
                            if(item.Detailed)
                            {
                                data=item.Bytes??Encoding.UTF8.GetBytes(item.Detail);
                                if(item.Text || item.Kind=="alignment-fields" || item.Kind=="terrain-pages")
                                {using(var buffer=new MemoryStream()){using(var gzip=new GZipStream(buffer,CompressionLevel.Fastest,true))gzip.Write(data,0,data.Length);data=buffer.ToArray();encoding="gzip";}}
                                if(segment==null || item.Clock-segment.Clock>=10L*Stopwatch.Frequency)
                                {
                                    detailStream?.Dispose();segment=new Segment{Name="detail-"+(++fileId).ToString("D8",CultureInfo.InvariantCulture)+".bin",Clock=item.Clock,Pinned=item.Clock<=triggerUntil};segments.Add(segment);
                                    detailStream=new FileStream(Path.Combine(root,segment.Name),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                                }
                                relative=segment.Name;offset=detailStream.Position;length=data.Length;
                            }
                            string row=(++ordinal)+"\t"+item.Utc.ToString("o")+"\t"+item.Clock+"\t"+item.Tick+"\t"+item.Request+"\t"+item.Generation+"\t"+item.Sequence+"\t"+Clean(item.Kind)+"\t"+Clean(summary)+"\t"+relative+"\t"+offset+"\t"+length+"\t"+encoding+"\t"+queued+"\t"+dropped+"\t"+previousWriteMs.ToString("R",CultureInfo.InvariantCulture);
                            string entry=data==null?"":relative+"\t"+offset+"\t"+length+"\t"+Hash(data)+"\t"+encoding+"\tpresent";
                            if(!budget.Reserve((data?.Length??0)+Encoding.UTF8.GetByteCount(row+entry)+4))
                            {lock(gate){dropped++;dropped+=queue.Count;queue.Clear();queued=0;}budget.RequestStop();StopFromWriter("disk-limit");break;}
                            if(data!=null){detailStream.Write(data,0,data.Length);index.WriteLine(entry);}
                            timeline.WriteLine(row);
                            if(Stopwatch.GetTimestamp()-lastControl>=Stopwatch.Frequency)
                            {
                                lastControl=Stopwatch.GetTimestamp();timeline.Flush();index.Flush();detailStream?.Flush();long cutoff=lastControl-180L*Stopwatch.Frequency;
                                for(int i=segments.Count-1;i>=0;i--){var old=segments[i];if(old==segment || old.Pinned || old.Clock+10L*Stopwatch.Frequency>=cutoff)continue;File.Delete(Path.Combine(root,old.Name));index.WriteLine(old.Name+"\t0\t0\t\traw\trolled-out");segments.RemoveAt(i);}
                            }
                            previousWriteMs=(Stopwatch.GetTimestamp()-before)*1000.0/Stopwatch.Frequency;writerTotalMs+=previousWriteMs;writerMaximumMs=Math.Max(writerMaximumMs,previousWriteMs);writeBatches++;
                        }
                    }
                    finally{detailStream?.Dispose();}
                    timeline.WriteLine((++ordinal)+"\t"+DateTime.UtcNow.ToString("o")+"\t"+Stopwatch.GetTimestamp()+"\t-1\t0\t0\t0\tstop\t"+Clean(stopReason)+"\t\t0\t0\traw\t"+queued+"\t"+dropped+"\t0");
                }
            }
            catch(Exception error){failure=error.GetType().Name+": "+Bounded(error.Message);lock(gate){dropped+=queue.Count;stopping=true;queue.Clear();queued=0;stopReason="io-failure";}}
            finally{if(metadataReserved)try{File.WriteAllText(Path.Combine(root,"completion.tsv"),"reason\t"+Clean(stopReason)+"\nfailure\t"+Clean(failure)+"\ndropped\t"+dropped+"\nqueueHighWater\t"+highWater+"\npreparedBytes\t"+bytesCopied+"\ndetailStopped\t"+detailStopped+"\nrecordOwnershipTotalMs\t"+recordOwnedTotalMs.ToString("R",CultureInfo.InvariantCulture)+"\nwriterBatches\t"+writeBatches+"\nwriterTotalMs\t"+writerTotalMs.ToString("R",CultureInfo.InvariantCulture)+"\nwriterMaximumMs\t"+writerMaximumMs.ToString("R",CultureInfo.InvariantCulture)+"\n",new UTF8Encoding(false));
                // Final immutable stream manifest detects removed whole rows or
                // missing footer/metadata as well as a corrupted detail slice.
                var text=new StringBuilder("file\tlength\tsha256\n");
                var names=new List<string>{"timeline.tsv","files.tsv","session.tsv","completion.tsv"};foreach(var part in segments)names.Add(part.Name);names.Sort(StringComparer.Ordinal);
                foreach(string name in names){string path=Path.Combine(root,name);using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())text.Append(name).Append('\t').Append(stream.Length).Append('\t').Append(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")).Append('\n');}
                string temporaryManifest=Path.Combine(root,"manifest.tmp");File.WriteAllText(temporaryManifest,text.ToString(),new UTF8Encoding(false));File.Move(temporaryManifest,Path.Combine(root,"manifest.tsv"));
            }catch{}}
        }
        private void StopFromWriter(string reason){lock(gate){stopping=true;stopReason=reason;}}
        private void Controls(AimDiagnosticBudget budget)
        {
            long now=Stopwatch.GetTimestamp();string trigger=null;lock(gate){trigger=pendingTrigger;pendingTrigger=null;}
            if(trigger!=null)budget.Trigger(now+180L*Stopwatch.Frequency);
            long shared=budget.TriggerUntil;
            if(shared>triggerUntil)
            {
                // Every trigger pins all surviving pre-window segments,
                // including an earlier trigger's history; subsequent triggers
                // cannot unpin them by overwriting a single expiry timestamp.
                triggerUntil=shared;foreach(var segment in segments)segment.Pinned=true;
            }
            if(now-lastControl<Stopwatch.Frequency)return;
            string mark=Path.Combine(sessionRoot,"mark.txt"),stop=Path.Combine(sessionRoot,"stop.txt");
            if(File.Exists(mark)){long stamp=File.GetLastWriteTimeUtc(mark).Ticks;if(stamp!=lastMark){lastMark=stamp;Trigger("manual: "+File.ReadAllText(mark));}}
            if(File.Exists(stop)){budget.RequestStop();StopFromWriter("manual-stop");}
            else if(budget.Stopped)StopFromWriter("session-stop");
        }
        private static string Bounded(string value){return value==null?"":value.Substring(0,Math.Min(1024,value.Length));}
        private static string Clean(string text){return (text??"").Replace("\t"," ").Replace("\r","\\r").Replace("\n","\\n");}
        internal static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    }
    // Accessed only by diagnostic writer threads. A named mutex covers all
    // Host/worker generations; the ledger survives a worker crash/replacement.
    internal sealed class AimDiagnosticBudget : IDisposable
    {
        private readonly Mutex mutex;private readonly MemoryMappedFile mapping;private readonly MemoryMappedViewAccessor view;private readonly long limit;
        internal AimDiagnosticBudget(string root,long limit)
        {
            this.limit=limit;Directory.CreateDirectory(root);string path=Path.Combine(root,"budget.bin");
            mutex=new Mutex(false,"Local\\JMR-Aim-"+AimDiagnosticRecorder.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(root))));
            Enter();try
            {
                var file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
                if(file.Length<32)file.SetLength(32);
                mapping=MemoryMappedFile.CreateFromFile(file,null,32,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,false);
                view=mapping.CreateViewAccessor(0,32,MemoryMappedFileAccess.ReadWrite);
                if(view.ReadInt64(24)==0)view.Write(24,Stopwatch.GetTimestamp()+1800L*Stopwatch.Frequency);
            }finally{mutex.ReleaseMutex();}
        }
        private void Enter(){try{mutex.WaitOne();}catch(AbandonedMutexException){}}
        internal bool Expired(long now){Enter();try{return now>=view.ReadInt64(24);}finally{mutex.ReleaseMutex();}}
        internal bool Reserve(long bytes){Enter();try{long current=view.ReadInt64(0);if(bytes>limit-current)return false;view.Write(0,current+bytes);return true;}finally{mutex.ReleaseMutex();}}
        internal long TriggerUntil {get{Enter();try{return view.ReadInt64(8);}finally{mutex.ReleaseMutex();}}}
        internal bool Stopped {get{Enter();try{return view.ReadInt64(16)!=0;}finally{mutex.ReleaseMutex();}}}
        internal void Trigger(long until){Enter();try{view.Write(8,Math.Max(view.ReadInt64(8),until));}finally{mutex.ReleaseMutex();}}
        internal void RequestStop(){Enter();try{view.Write(16,1L);}finally{mutex.ReleaseMutex();}}
        public void Dispose(){view.Flush();view.Dispose();mapping.Dispose();mutex.Dispose();}
    }
    internal sealed class AimDiagnosticTape
    {
        private struct Entry {internal int Field,Index;internal byte Kind;internal ulong Bits;}
        private readonly List<Entry> entries=new List<Entry>();
        private readonly List<string> names=new List<string>(),types=new List<string>();
        private readonly Dictionary<string,int> fields=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly List<int> indexes=new List<int>();
        private int field=-1;
        internal void BeginField(string name,string type)
        {
            try{int next;if(!fields.TryGetValue(name+"\t"+type,out next)){next=names.Count;fields.Add(name+"\t"+type,next);names.Add(name);types.Add(type);indexes.Add(0);}field=next;}catch(Exception error){AimDiagnostics.Missing("field-tape",error);}
        }
        internal void Add(string kind,ulong bits)
        {try{if(field<0)BeginField("unlabelled","primitive");entries.Add(new Entry{Field=field,Index=indexes[field]++,Kind=kind=="u8"?(byte)1:kind=="u16"?(byte)2:kind=="u32"?(byte)4:(byte)0,Bits=bits});}catch(Exception error){AimDiagnostics.Missing("field-tape",error);}}
        internal byte[] Encode()
        {
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
            {writer.Write(0x41494D54);writer.Write(names.Count);for(int i=0;i<names.Count;i++){writer.Write(names[i]);writer.Write(types[i]);}writer.Write(entries.Count);foreach(var entry in entries){writer.Write(entry.Field);writer.Write(entry.Index);writer.Write(entry.Kind);writer.Write(entry.Bits);}writer.Flush();return bytes.ToArray();}
        }
        private string Describe(Entry entry){return names[entry.Field]+"["+entry.Index+"]\t"+types[entry.Field]+"/"+entry.Kind+"\t"+entry.Bits.ToString("X16",CultureInfo.InvariantCulture);}
        internal static string Difference(AimDiagnosticTape expected,AimDiagnosticTape actual)
        {int n=Math.Min(expected.entries.Count,actual.entries.Count);for(int i=0;i<n;i++){string a=expected.Describe(expected.entries[i]),b=actual.Describe(actual.entries[i]);if(a!=b)return "expected="+a+"; actual="+b;}return expected.entries.Count==actual.entries.Count?null:"field count "+expected.entries.Count+"/"+actual.entries.Count;}
    }
}
#endif
