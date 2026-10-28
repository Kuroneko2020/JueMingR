#if JMR_BLUEPRINT_DIAGNOSTIC
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JueMingR.TerrariaHost.Blueprint
{
    // One process, one claim, one bounded window. Only the recorder owns these
    // observations. Business code cannot query them to decide an operation.
    internal sealed partial class BlueprintBuildTrace
    {
        internal const int SecondsLimit=120, EventLimit=512, TargetLimit=8, RawFileLimit=1048576, TraceByteLimit=RawFileLimit-4096;
        private const int HeadEvents=128, TailEvents=EventLimit-HeadEvents;
        private const int StageCount=(int)BuildTraceStage.Count, ReasonCount=(int)BuildTraceReason.Count;
        private readonly object gate=new object(), exportGate=new object();
        private readonly string root, hostFile, packageId;
        private readonly Func<long> clock;
        private readonly bool useTimer;
        private readonly BuildTraceSecond[] seconds=new BuildTraceSecond[SecondsLimit];
        private readonly BuildTraceSource[] sources=new BuildTraceSource[SecondsLimit*4];
        private readonly BuildTraceEvent[] events=new BuildTraceEvent[EventLimit];
        private readonly BuildTraceTarget[] targets=new BuildTraceTarget[TargetLimit];
        private readonly BuildTraceMaterial[] materials=new BuildTraceMaterial[TargetLimit];
        private readonly BuildTraceMaterial[] inventoryScratch=new BuildTraceMaterial[TargetLimit];
        private int inventoryScratchCount;
        private bool inventorySampleAccepted;
        private readonly long[] stages=new long[SecondsLimit*StageCount], reasons=new long[SecondsLimit*ReasonCount], lastSeen=new long[SecondsLimit*StageCount];
        private readonly long[] reasonUpdates=new long[SecondsLimit*ReasonCount], reasonLastOuter=new long[ReasonCount];
        private int state, initialWorld, initialMode, initialPlayer, targetCount, targetCursor, materialCount;
        private long started, stoppedAt, sequence, overwritten, suppressedEvents, contention, targetReplaced, targetIdentity;
        private long outerSerial;
        private bool hasPreviousOuter, attemptedExport;
        private BuildTraceOuter previousOuter;
        private readonly BuildTraceEvent[] previousEvents=new BuildTraceEvent[StageCount*ReasonCount];
        private string runId, hostSha, hostMvid, hostAssembly, gameVersion, stopReason, failure;
        private Timer timer;
        // 0=waiting for first world, 1=claim pending, 2=claimed, 3=recording,
        // 4=permanently stopped. A late claim cannot reopen a retired world.
        internal static BlueprintBuildTrace Current { get; private set; }
        internal static bool Active { get { return Current!=null && Volatile.Read(ref Current.state)==3; } }
        internal bool Accepting { get { return Volatile.Read(ref state)==3; } }
        internal bool Finished { get { return Volatile.Read(ref state)==4; } }
        internal string RunId { get { return runId; } }
#if JMR_BLUEPRINT_DIAGNOSTIC_TEST
        internal static void BindForTest(BlueprintBuildTrace value){Current=value;}
        internal Action BeforeClaimForTest;
        internal Action AfterClaimForTest;
#endif
        internal BlueprintBuildTrace(string root,string hostFile,string packageId,Func<long> clock,bool useTimer=true)
        {
            this.root=root;this.hostFile=hostFile;this.packageId=packageId;this.clock=clock;this.useTimer=useTimer;
            for(int i=0;i<lastSeen.Length;i++)lastSeen[i]=-1;
            for(int i=0;i<reasonLastOuter.Length;i++)reasonLastOuter[i]=-1;
        }
        internal static void Initialize(string gameDirectory,string packageId,string version)
        {
            try
            {
                if(Current!=null)return;
                Current=new BlueprintBuildTrace(Path.Combine(gameDirectory,"JueMingRData","logs","blueprint-autobuild-diagnostic"),
                    typeof(BlueprintBuildTrace).Assembly.Location,packageId,()=>Stopwatch.GetTimestamp()*1000/Stopwatch.Frequency);
                Current.gameVersion=version;
                AppDomain.CurrentDomain.ProcessExit+=Current.Exit;
            }
            catch { /* This observer must never fail the runtime's composition. */ }
        }
        internal static bool Gate(bool result,BuildTraceReason reason,BuildTraceStage stage=BuildTraceStage.ToolsAdmit)
        {
            if(Active)Current.RecordGate(result,reason,stage);
            return result;
        }
        internal static void Hit(BuildTraceStage stage,BuildTraceReason reason=BuildTraceReason.None,long amount=1)
        {if(Active)Current.Record(stage,reason,amount);}
        internal static void Count(BuildTraceReason reason,long amount=1)
        {if(Active)Current.RecordCounter(reason,amount);}
        internal static bool Changed(bool value,BuildTraceReason reason)
        {if(value)Count(reason);return value;}
        internal static void Fault(Exception error){if(Current!=null && !Current.Finished)Current.Abort(error);}
        internal static void Semantic(BuildTraceStage stage,BuildTraceReason reason,long a=0,long b=0,long c=0,long d=0,long flight=0,long token=0,long generation=0,int target=-1)
        {if(Active)Current.RecordEvent(stage,reason,a,b,c,d,flight,token,generation,target);}
        internal void Outer(BuildTraceOuter value)
        {
            try
            {
                int phase=Volatile.Read(ref state);
                if(phase==4)return;
                if(phase==0)
                {
                    if(!value.WorldPresent)return;
                    initialWorld=value.WorldId;initialMode=value.NetMode;initialPlayer=value.PlayerIndex;
                    if(Interlocked.CompareExchange(ref state,1,0)==0)ThreadPool.QueueUserWorkItem(_=>Claim());
                    return;
                }
                bool sameWorld=value.WorldPresent && value.WorldId==initialWorld && value.NetMode==initialMode && value.PlayerIndex==initialPlayer;
                if(phase==1){if(!sameWorld)Stop("session-ended-before-start");return;}
                if(phase==2)
                {
                    if(!sameWorld){Stop("session-ended-before-start");return;}
                    lock(gate)
                    {
                        if(state!=2)return;
                        started=clock();Volatile.Write(ref state,3);
                        if(useTimer)timer=new Timer(_=>Stop("time-limit"),null,SecondsLimit*1000,Timeout.Infinite);
                    }
                }
                if(!sameWorld)
                {Stop("session-ended-or-changed");return;}
                long at=Elapsed();if(at>=SecondsLimit*1000){Stop("time-limit");return;}
                if(!Enter())return;
                try
                {
                    if(state!=3)return;
                    int index=Bucket(at);ref var second=ref seconds[index];
                    second.OuterCalls++;outerSerial++;
                    bool moving=hasPreviousOuter && (previousOuter.X!=value.X || previousOuter.Y!=value.Y) || value.VelocityX!=0 || value.VelocityY!=0;
                    if(moving)second.MovingUpdates++;else second.StationaryUpdates++;
                    second.Outer=value;previousOuter=value;hasPreviousOuter=true;
                    AddStage(index,BuildTraceStage.Outer,at);
                }
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        private void Claim()
        {
            string status="claim-failed";
            try
            {
#if JMR_BLUEPRINT_DIAGNOSTIC_TEST
                BeforeClaimForTest?.Invoke();
#endif
                runId=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
                using(var file=File.OpenRead(hostFile))using(var sha=SHA256.Create())hostSha=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
                var assembly=AssemblyName.GetAssemblyName(hostFile);hostAssembly=assembly.FullName;
                hostMvid=typeof(BlueprintBuildTrace).Assembly.ManifestModule.ModuleVersionId.ToString("D");
                for(var directory=new DirectoryInfo(root);directory!=null;directory=directory.Parent)
                    if(directory.Exists && (directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse diagnostic root.");
                string arm=Path.Combine(root,"arm.txt");
                if(!File.Exists(arm)){status="missing-arm";return;}
                var info=new FileInfo(arm);
                if((info.Attributes&FileAttributes.ReparsePoint)!=0 || info.Length>128){status="invalid-arm";return;}
                string expected="hostSha256="+hostSha;
                string value=File.ReadAllText(arm,new UTF8Encoding(false,true)).Trim();
                if(!string.Equals(value,expected,StringComparison.Ordinal)){status="wrong-host";return;}
                if(Volatile.Read(ref state)!=1){status="session-ended-before-claim";return;}
                File.Move(arm,Path.Combine(root,"used-"+runId+".txt"));
                status="claimed";
            }
            catch(Exception error){failure=Limit(error.GetType().FullName,160);}
            finally
            {
                try
                {
                    Directory.CreateDirectory(root);
                    byte[] bytes=Encoding.UTF8.GetBytes("{\"status\":"+Quote(status)+",\"runId\":"+Quote(runId)+",\"hostSha256\":"+Quote(hostSha)+",\"failure\":"+Quote(failure)+"}");
                    // CreateNew retains any earlier process's status as evidence.
                    using(var file=new FileStream(Path.Combine(root,"claim-status.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Write(bytes,0,bytes.Length);
                }
                catch(Exception error){if(failure==null)failure=Limit(error.GetType().FullName,160);}
                int prior=Interlocked.CompareExchange(ref state,status=="claimed"?2:4,1);
                // A world may retire while this bounded I/O is in flight. A
                // late result never reopens it, even if the same world returns.
                if(prior==4 && status=="claimed" && stopReason!=null)ThreadPool.QueueUserWorkItem(_=>Export());
#if JMR_BLUEPRINT_DIAGNOSTIC_TEST
                AfterClaimForTest?.Invoke();
#endif
            }
        }
        private bool Enter()
        {
            if(!Monitor.TryEnter(gate)){Interlocked.Increment(ref contention);return false;}
            return true;
        }
        private long Elapsed(){return Math.Max(0,clock()-started);}
        private static int Bucket(long at){return (int)Math.Min(SecondsLimit-1,Math.Max(0,at/1000));}
        private void AddStage(int bucket,BuildTraceStage stage,long at)
        {stages[bucket*StageCount+(int)stage]++;lastSeen[bucket*StageCount+(int)stage]=at;}
        private void AddReason(int bucket,BuildTraceReason reason,long amount)
        {
            reasons[bucket*ReasonCount+(int)reason]+=amount;
            if(outerSerial>0 && reasonLastOuter[(int)reason]!=outerSerial){reasonLastOuter[(int)reason]=outerSerial;reasonUpdates[bucket*ReasonCount+(int)reason]++;}
        }
        private void Record(BuildTraceStage stage,BuildTraceReason reason,long amount)
        {
            try
            {
                if(!Enter())return;
                try{if(state!=3)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;int bucket=Bucket(at);AddStage(bucket,stage,at);AddReason(bucket,reason,amount);}
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        private void RecordGate(bool passed,BuildTraceReason reason,BuildTraceStage stage)
        {
            Record(stage,passed?BuildTraceReason.Passed:reason,1);
            try
            {
                if(!Enter())return;
                try{if(state!=3)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;ref var s=ref seconds[Bucket(at)];
                    int bitIndex=reason==BuildTraceReason.InputGate?0:reason==BuildTraceReason.InterfaceGate?1:reason==BuildTraceReason.MouseGate?2:reason==BuildTraceReason.InventoryNotReady?3:reason==BuildTraceReason.ToolsGate?4:5;
                    long bit=1L<<bitIndex;s.GateEvaluated|=bit;if(passed)s.GatePassed|=bit;}
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        private void RecordCounter(BuildTraceReason reason,long amount)
        {
            try{if(!Enter())return;try{if(state!=3)return;long at=Elapsed();if(at<SecondsLimit*1000)AddReason(Bucket(at),reason,amount);}finally{Monitor.Exit(gate);}}
            catch(Exception error){Abort(error);}
        }
        private void RecordEvent(BuildTraceStage stage,BuildTraceReason reason,long a,long b,long c,long d,long flight,long token,long generation,int target)
        {
            RecordCounter(reason,1);
            try
            {
                if(!Enter())return;
                try
                {
                    if(state!=3)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;
                    long detailId=target>=0 && target<targetCount?targets[target].DetailId:-1;
                    int eventKey=(int)stage*ReasonCount+(int)reason;var previousEvent=previousEvents[eventKey];
                    bool stablePhase=reason==BuildTraceReason.DirectionPhase || reason==BuildTraceReason.Ready || reason==BuildTraceReason.Pending || reason==BuildTraceReason.Yield || reason==BuildTraceReason.Invalid;
                    if(previousEvent.Sequence>0 && previousEvent.Stage==(int)stage && previousEvent.Reason==(int)reason && previousEvent.Flight==flight && previousEvent.Token==token && (stablePhase || previousEvent.Generation==generation) &&
                        previousEvent.A==a && previousEvent.B==b && previousEvent.C==c && previousEvent.D==d && previousEvent.Target==target && previousEvent.TargetDetailId==detailId){suppressedEvents++;return;}
                    long seq=++sequence;
                    var value=new BuildTraceEvent{Sequence=seq,At=at,Stage=(int)stage,Reason=(int)reason,Target=target,TargetDetailId=detailId,Flight=flight,Token=token,Generation=generation,A=a,B=b,C=c,D=d};
                    int slot=seq<=HeadEvents?(int)seq-1:HeadEvents+(int)((seq-HeadEvents-1)%TailEvents);
                    if(seq>EventLimit)overwritten++;
                    events[slot]=value;previousEvents[eventKey]=value;
                }
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        internal void Blueprint(BuildTraceBlueprint value)
        {
            try
            {
                if(!Enter())return;
                try{if(state!=3)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;ref var s=ref seconds[Bucket(at)];s.Blueprint=value;s.BlueprintObserved=true;
                    if((value.State&1)!=0)s.AutoEnabledUpdates++;if(value.SceneCount>0)s.SceneUpdates++;}
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        internal bool NeedSources {get{return Accepting && seconds[Bucket(Elapsed())].SourceCount==0;}}
        internal void Source(int slot,BuildTraceSource value,int total)
        {
            try
            {
                if(slot<0 || slot>=4 || !Enter())return;
                try{if(state!=3)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;int bucket=Bucket(at);sources[bucket*4+slot]=value;seconds[bucket].SourceCount=total+1;}
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        internal void EmptySources()
        {try{if(!Enter())return;try{if(state==3)seconds[Bucket(Elapsed())].SourceCount=1;}finally{Monitor.Exit(gate);}}catch(Exception error){Abort(error);}}
        internal int Target(BuildTraceTarget value)
        {
            try
            {
                if(!Enter())return -1;
                try
                {
                    if(state!=3)return -1;value.At=Elapsed();if(value.At>=SecondsLimit*1000)return -1;
                    int at=-1;for(int i=0;i<targetCount;i++)if(targets[i].Session==value.Session && targets[i].Instance==value.Instance && targets[i].Range==value.Range && targets[i].Ordinal==value.Ordinal){at=i;break;}
                    bool retained=at>=0;
                    if(!retained){if(targetCount<TargetLimit)at=targetCount++;else{at=targetCursor++%TargetLimit;targetReplaced++;}}
                    // A ring slot is storage, not target identity. Early retained
                    // events must never acquire a later target after replacement.
                    value.DetailId=retained?targets[at].DetailId:++targetIdentity;
                    if(value.ChoiceAt<0 && retained && targets[at].ChoiceAt>=0)
                    {var prior=targets[at];value.ChoiceAt=prior.ChoiceAt;value.ChoiceType=prior.ChoiceType;value.ChoiceStyle=prior.ChoiceStyle;value.ChoiceAlternate=prior.ChoiceAlternate;value.ChoiceRandom=prior.ChoiceRandom;value.ChoiceX=prior.ChoiceX;value.ChoiceY=prior.ChoiceY;}
                    else if(value.ChoiceAt>=0)value.ChoiceAt=value.At;
                    if(value.CanPlaceResult<0 && retained && targets[at].CanPlaceResult>=0)
                    {value.CanPlaceResult=targets[at].CanPlaceResult;value.CanPlaceAt=targets[at].CanPlaceAt;}
                    else if(value.CanPlaceResult>=0)value.CanPlaceAt=value.At;
                    targets[at]=value;return at;
                }
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);return -1;}
        }
        internal void Supply(int itemType,bool supplied,long revision,bool observed=true)
        {
            try
            {
                if(!Enter())return;
                try
                {
                    if(state!=3 || Elapsed()>=SecondsLimit*1000)return;int at=-1;for(int i=0;i<materialCount;i++)if(materials[i].ItemType==itemType){at=i;break;}
                    if(at<0 && !supplied && materialCount<TargetLimit){at=materialCount++;materials[at].ItemType=itemType;materials[at].RequestedAt=Elapsed();}
                    if(at>=0 && observed){materials[at].Supplied=supplied;materials[at].SupplyRevision=revision;materials[at].SupplyObserved=true;}
                }
                finally{Monitor.Exit(gate);}
            }
            catch(Exception error){Abort(error);}
        }
        internal void InventoryBegin()
        {
            try{if(!Enter())return;try{if(state!=3)return;inventorySampleAccepted=Elapsed()<SecondsLimit*1000;inventoryScratchCount=materialCount;
                for(int i=0;i<inventoryScratchCount;i++)inventoryScratch[i]=new BuildTraceMaterial{ItemType=materials[i].ItemType};}
                finally{Monitor.Exit(gate);}}catch(Exception error){Abort(error);}
        }
        internal void InventorySlot(int type,long stack,bool eligible)
        {
            try{if(!Enter())return;try{if(state!=3 || !inventorySampleAccepted)return;for(int i=0;i<inventoryScratchCount;i++){ref var m=ref inventoryScratch[i];m.CoveredSlots++;if(m.ItemType!=type)continue;m.MatchingSlots++;m.Stack+=stack;if(eligible){m.EligibleSlots++;m.EligibleStack+=stack;}}}finally{Monitor.Exit(gate);}}catch(Exception error){Abort(error);}
        }
        internal void InventoryEnd(ulong tick)
        {
            try{if(!Enter())return;try{if(state!=3 || !inventorySampleAccepted)return;long at=Elapsed();if(at>=SecondsLimit*1000)return;
                for(int i=0;i<inventoryScratchCount;i++){ref var m=ref materials[i];var sample=inventoryScratch[i];
                    m.MatchingSlots=sample.MatchingSlots;m.EligibleSlots=sample.EligibleSlots;m.CoveredSlots=sample.CoveredSlots;m.Stack=sample.Stack;m.EligibleStack=sample.EligibleStack;
                    m.Sampled=true;m.SampleTick=tick;m.SampleAt=at;}inventorySampleAccepted=false;}
                finally{Monitor.Exit(gate);}}catch(Exception error){Abort(error);}
        }
        internal void PrepareMaterial(int type,bool eligible)
        {try{if(!Enter())return;try{if(state!=3 || Elapsed()>=SecondsLimit*1000)return;for(int i=0;i<materialCount;i++)if(materials[i].ItemType==type){materials[i].PrepareVisited++;if(eligible)materials[i].PrepareEligible++;else materials[i].PrepareFiltered++;break;}}finally{Monitor.Exit(gate);}}catch(Exception error){Abort(error);}}
        internal void Stop(string reason,bool exiting=false)
        {
            try
            {
                int previous;
                lock(gate)
                {
                    previous=state;if(previous==4)return;
                    stoppedAt=previous==3?Math.Min(SecondsLimit*1000,Elapsed()):0;stopReason=Limit(reason,80);Volatile.Write(ref state,4);timer?.Dispose();
                }
                // No producer can mutate accepted records now. The export owns
                // only primitive arrays, never a live player, item or target.
                if(previous<2)return;
                if(exiting)Export();else ThreadPool.QueueUserWorkItem(_=>Export());
            }
            catch(Exception error){failure=Limit(error.GetType().FullName,160);Volatile.Write(ref state,4);}
        }
        internal void CheckDeadline(){if(Accepting && Elapsed()>=SecondsLimit*1000)Stop("time-limit");}
        private void Abort(Exception error)
        {try{failure=Limit(error.GetType().FullName,160);Stop("observer-failure");}catch{Volatile.Write(ref state,4);}}
        private void Exit(object sender,EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit-=Exit;
            if(Volatile.Read(ref state)<4)Stop("normal-process-exit",true);
            else if(Volatile.Read(ref state)==4 && stopReason!=null)Export();
        }
        private static string Limit(string value,int maximum){return value==null?null:value.Length<=maximum?value:value.Substring(0,maximum);}
    }
}
#endif
