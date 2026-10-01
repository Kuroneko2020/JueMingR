using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // One client owns one exact child and two raw pipes. Only this background
    // thread touches process/file/pipe APIs. Host retains request identities,
    // latest pending intent and all Terraria observations on the game thread.
    internal sealed class PredictionWorkerClient
    {
        // Starting, Ready, Busy, Result, Faulted, Stopping, Stopped, Clearing.
        private readonly object gate=new object(),processGate=new object();
        private readonly AutoResetEvent wake=new AutoResetEvent(false);
        private readonly string directory,gamePath,cacheDirectory;
        private readonly string[] expectedHashes;
        internal static readonly string[] PayloadNames={"JueMingR.PredictionWorker.exe","JueMingR.PredictionWorker.exe.config","JueMingR.TerrariaHost.dll","JueMingR.Platform.dll","JueMingR.Features.dll","JueMingR.Infrastructure.dll","0Harmony.dll"};
        private readonly int startupMilliseconds,requestMilliseconds;
        private int state,stopping,childId,closed,readySeen,recoverable;
        private long deadline;
        private long worldRevision,clearedRevision;
        private byte[] request,result;
        private sealed class ValueRequest
        {
            internal NativeCapturedValues Values;
            internal NpcIdentity Identity;
            internal long Tick;
            internal bool NetworkObservation;
        }
        internal sealed class DecodedReply
        {
            internal NativePredictionResult Result;
            internal int Bytes,ReplyBytes,MissingAsset;
            internal double EncodeMs,ExchangeMs,DecodeMs;
        }
        private ValueRequest valueRequest;
        private DecodedReply decodedReply;
        private NativeCapturedValues.Storage captureStorage;
        private string failure;
        private Process child;
        private readonly FileStream[] leases=new FileStream[PayloadNames.Length+1];
        internal double ReadyMilliseconds {get;private set;}
        internal double CapturePreparationMilliseconds {get;private set;}
        internal double ExchangeMilliseconds {get;private set;}
        internal string Diagnostics {get;private set;}
        internal byte[] Alignment {get;private set;}
        internal int MissingAsset {get;private set;}
        internal int State {get{return Volatile.Read(ref state);}}
        internal int ChildId {get{return Volatile.Read(ref childId);}}
        internal bool Closed {get{return Volatile.Read(ref closed)!=0;}}
        internal string Failure {get{return Volatile.Read(ref failure);}}
        internal bool Recoverable {get{return Volatile.Read(ref recoverable)!=0;}}
        internal PredictionWorkerClient(string directory,string gamePath,string[] expectedHashes,int startupMilliseconds=60000,int requestMilliseconds=5000)
            :this(directory,gamePath,expectedHashes,startupMilliseconds,requestMilliseconds,Path.Combine(directory,"prediction-materials")){}
        internal PredictionWorkerClient(string directory,string gamePath,string[] expectedHashes,int startupMilliseconds,int requestMilliseconds,string cacheDirectory)
        {
            this.cacheDirectory=cacheDirectory??throw new ArgumentNullException(nameof(cacheDirectory));
            if(string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(gamePath) || startupMilliseconds<1 || startupMilliseconds>60000 || requestMilliseconds<1 || requestMilliseconds>60000)throw new ArgumentOutOfRangeException();
            if(expectedHashes==null || expectedHashes.Length!=PayloadNames.Length)throw new ArgumentException("Trusted prediction payload identities are required.");
            this.expectedHashes=(string[])expectedHashes.Clone();
            foreach(string hash in this.expectedHashes)if(hash==null || hash.Length!=64)throw new ArgumentException("Invalid trusted prediction payload identity.");
            this.directory=directory;this.gamePath=gamePath;this.startupMilliseconds=startupMilliseconds;this.requestMilliseconds=requestMilliseconds;
            new Thread(Run){IsBackground=true,Name="JueMingR prediction transport"}.Start();
        }
        // Accepted arrays transfer ownership: the game-thread producer must
        // never mutate them afterwards. No frame is copied or encoded here.
        internal bool TrySend(byte[] bytes)
        {
            if(bytes==null || bytes.Length<1 || bytes.Length>PredictionPipeProtocol.MaximumPayload)throw new ArgumentOutOfRangeException(nameof(bytes));
            lock(gate){if(state!=1 || stopping!=0)return false;request=bytes;Volatile.Write(ref state,2);wake.Set();return true;}
        }
        internal byte[] TryTake()
        {lock(gate){if(state!=3 || stopping!=0 || result==null)return null;byte[] answer=result;result=null;Volatile.Write(ref state,1);return answer;}}
        internal NativeCapturedValues BeginCapture()
        {lock(gate){if(state!=1 || stopping!=0)throw new InvalidOperationException("Capture requires a ready transport.");var values=new NativeCapturedValues(captureStorage);captureStorage=null;return values;}}
        internal bool TrySendValues(NativeCapturedValues values,NpcIdentity identity,long tick,bool networkObservation)
        {
            if(values==null || !values.IsSealed || identity.Token!=null || tick<0)throw new ArgumentException("Prediction transport accepts sealed values and a token-free identity only.");
            lock(gate){if(state!=1 || stopping!=0)return false;valueRequest=new ValueRequest{Values=values,Identity=identity,Tick=tick,NetworkObservation=networkObservation};Volatile.Write(ref state,2);wake.Set();return true;}
        }
        internal DecodedReply TryTakeResult()
        {lock(gate){if(state!=3 || stopping!=0 || decodedReply==null)return null;var answer=decodedReply;decodedReply=null;Volatile.Write(ref state,1);return answer;}}
        // Host retirement is immediate. A running request is allowed to finish
        // within its deadline, but cannot publish after this revision changes.
        // The same serial pipe acknowledges private cleanup before Ready.
        internal void ResetWorld()
        {
            lock(gate)
            {
                if(stopping!=0)return;
                worldRevision++;request=result=null;valueRequest=null;decodedReply=null;Alignment=null;
                if(state!=0)Volatile.Write(ref state,7);wake.Set();
            }
        }
        internal void Stop()
        {
            lock(gate)
            {
                if(stopping!=0)return;Volatile.Write(ref stopping,1);request=result=null;valueRequest=null;decodedReply=null;captureStorage=null;
                Volatile.Write(ref state,5);wake.Set();
            }
        }
        private void Limit(int milliseconds){lock(gate)deadline=Stopwatch.GetTimestamp()+(long)(milliseconds*(double)Stopwatch.Frequency/1000);}
        private void Watch(object ignored)
        {
            // Deadline decisions and successful stage publication share one
            // boundary. A timer cannot carry an old deadline across Ready or
            // Result and accidentally fail a completed or newer operation.
            lock(gate)if(stopping==0 && deadline!=0 && Stopwatch.GetTimestamp()>=deadline)Fail(new TimeoutException("Prediction helper exceeded its bounded transport wait."));
            if(Volatile.Read(ref stopping)!=0)Terminate();
        }
        private void Fail(Exception error)
        {
            lock(gate)
            {
                if(stopping!=0)return;string message=error.GetType().Name+": "+error.Message;
                // Only loss/timeout of an already authenticated connection can
                // invite one Session-owned restart. InvalidDataException also
                // derives from IOException: exclude it explicitly so corrupt
                // identity, protocol or decode never becomes a retry signal.
                Volatile.Write(ref recoverable,readySeen!=0 && (error is TimeoutException || error is IOException && !(error is InvalidDataException))?1:0);
                Volatile.Write(ref failure,message.Length>1024?message.Substring(0,1024):message);
                Volatile.Write(ref stopping,1);request=result=null;valueRequest=null;decodedReply=null;Volatile.Write(ref state,4);wake.Set();
            }
        }
        private void Terminate()
        {
            // Start retained the exact process handle before publishing it.
            // Never reacquire ownership by PID or search by executable name.
            lock(processGate)if(child!=null)try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        }
        private void Run()
        {
            Task<string> diagnostics=null;
            using(var watchdog=new Timer(Watch,null,100,100))
            try
            {
                var startup=Stopwatch.StartNew();
                Limit(startupMilliseconds);
                using(var output=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.Inheritable))
                using(var input=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.Inheritable))
                using(var parent=Process.GetCurrentProcess())
                {
                    // Expected hashes come from the authenticated installation
                    // manifest, never from the candidate files themselves.
                    // Config must be authenticated BEFORE CLR startup can load
                    // any AppDomainManager, not merely rejected in Main.
                    // FileShare.Read denies both writing and replacement. Hold
                    // the authenticated files until the child has exited, so
                    // CLR cannot reopen a different config or lazy dependency.
                    for(int i=0;i<PayloadNames.Length;i++)if(Hash(leases[i]=new FileStream(Path.Combine(directory,PayloadNames[i]),FileMode.Open,FileAccess.Read,FileShare.Read))!=expectedHashes[i])throw new InvalidDataException("Prediction payload identity mismatch: "+PayloadNames[i]);
                    if(Hash(leases[PayloadNames.Length]=new FileStream(gamePath,FileMode.Open,FileAccess.Read,FileShare.Read))!=PredictionPipeProtocol.GameHash)throw new InvalidDataException("Prediction original identity mismatch.");
                    var layouts=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;PredictionWire.PrepareCaptureLayouts();CapturePreparationMilliseconds=layouts==null?0:layouts.Elapsed.TotalMilliseconds;
                    string hostHash=expectedHashes[2];Guid nonce=Guid.NewGuid();
                    int parentId=parent.Id;long parentStarted=parent.StartTime.ToUniversalTime().Ticks;
                    var start=new ProcessStartInfo(Path.Combine(directory,"JueMingR.PredictionWorker.exe"),"--anonymous-pipes "+Quote(gamePath)+" "+hostHash+" "+parentId.ToString(CultureInfo.InvariantCulture)+" "+parentStarted.ToString(CultureInfo.InvariantCulture)+" "+output.GetClientHandleAsString()+" "+input.GetClientHandleAsString()+" "+nonce.ToString("N")+" "+Quote(cacheDirectory))
                    {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory,RedirectStandardError=true};
                    foreach(string key in new[]{"APPDOMAIN_MANAGER_ASM","APPDOMAIN_MANAGER_TYPE","COMPLUS_Version","COMPLUS_ApplicationMigrationRuntimeActivationConfigPath","COR_ENABLE_PROFILING","COR_PROFILER","COR_PROFILER_PATH"})start.EnvironmentVariables.Remove(key);
                    long childStarted;
                    lock(processGate)
                    {
                        if(Volatile.Read(ref stopping)!=0)return;
                        child=Process.Start(start);IntPtr handle=child.Handle;
                        if(handle==IntPtr.Zero)throw new InvalidOperationException("Owned prediction process handle unavailable.");
                        Volatile.Write(ref childId,child.Id);childStarted=child.StartTime.ToUniversalTime().Ticks;
                    }
                    // These parent copies would otherwise keep the child end
                    // alive and hide EOF after a worker failure.
                    output.DisposeLocalCopyOfClientHandle();input.DisposeLocalCopyOfClientHandle();
                    StreamReader errorReader=child.StandardError;diagnostics=Task.Run(()=>Drain(errorReader));
                    PredictionPipeProtocol.VerifyReady(PredictionPipeProtocol.ReadFrame(input),nonce,parentId,parentStarted,childId,childStarted,hostHash);
                    lock(gate){deadline=0;if(stopping!=0)return;Volatile.Write(ref readySeen,1);ReadyMilliseconds=startup.Elapsed.TotalMilliseconds;Volatile.Write(ref state,worldRevision==clearedRevision?1:7);}
                    long sequence=0;
                    while(Volatile.Read(ref stopping)==0)
                    {
                        byte[] payload;long revision;bool clear;ValueRequest values;
                        lock(gate){revision=worldRevision;clear=revision!=clearedRevision;payload=clear?PredictionPipeProtocol.ClearWorld():request;values=clear?null:valueRequest;request=null;valueRequest=null;}
                        if(payload==null && values==null){wake.WaitOne(100);if(child.HasExited)throw new EndOfStreamException("Prediction helper exited while idle.");continue;}
                        Limit(requestMilliseconds);double encodeMs=0;
                        if(values!=null)
                        {
                            var encoding=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;payload=values.Values.Encode();
                            var reusable=values.Values.ReleaseStorage();values.Values=null;
                            lock(gate){if(stopping==0)captureStorage=reusable;}
                            encodeMs=encoding?.Elapsed.TotalMilliseconds??0;
                        }
                        var exchange=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;PredictionPipeProtocol.WriteFrame(output,PredictionPipeProtocol.Envelope(payload,checked(++sequence),false));
                        byte[] answer=PredictionPipeProtocol.ReadFrame(input);if(answer==null)throw new EndOfStreamException("Prediction helper ended without a result.");
                        answer=PredictionPipeProtocol.OpenEnvelope(answer,sequence,true);
                        if(clear)
                        {
                            if(!PredictionPipeProtocol.IsClearWorld(answer))throw new InvalidDataException("Prediction world reset was not acknowledged.");
                            lock(gate){deadline=0;if(stopping!=0)return;clearedRevision=revision;Volatile.Write(ref state,revision==worldRevision?1:7);}
                            continue;
                        }
                        byte[] alignment;int asset;answer=PredictionPipeProtocol.OpenResult(answer,out alignment,out asset);
                        double exchangeMs=exchange?.Elapsed.TotalMilliseconds??0;DecodedReply decoded=null;
                        if(values!=null)
                        {
                            var decoding=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;var parsed=NativePredictionResult.Read(answer,alignment,values.Identity,values.Tick,0,values.NetworkObservation);
                            decoded=new DecodedReply{Result=parsed,Bytes=payload.Length,ReplyBytes=answer.Length+(alignment?.Length??0),MissingAsset=asset,EncodeMs=encodeMs,ExchangeMs=exchangeMs,DecodeMs=decoding?.Elapsed.TotalMilliseconds??0};
                        }
                        // Encoding and decoding do not hold the mailbox lock.
                        // World retirement can happen during either operation;
                        // that revision must never publish a late old result.
                        lock(gate){deadline=0;if(stopping!=0)return;if(revision!=worldRevision){Volatile.Write(ref state,7);continue;}Alignment=values==null?alignment:null;MissingAsset=asset;ExchangeMilliseconds=exchangeMs;result=values==null?answer:null;decodedReply=decoded;Volatile.Write(ref state,3);}
                    }
                }
            }
            catch(Exception error){Fail(error);}
            finally
            {
                lock(gate)deadline=0;Terminate();
                lock(processGate)if(child!=null)
                {try{child.WaitForExit(2000);if(diagnostics!=null && diagnostics.Wait(1000))Diagnostics=diagnostics.Result;}catch(InvalidOperationException){}catch(AggregateException){}finally{child.Dispose();child=null;}}
                foreach(var lease in leases)lease?.Dispose();
                lock(gate){Volatile.Write(ref stopping,1);request=result=null;valueRequest=null;decodedReply=null;captureStorage=null;Volatile.Write(ref state,failure==null?6:4);Volatile.Write(ref closed,1);wake.Dispose();}
            }
        }
        private static string Drain(StreamReader reader)
        {
            var text=new StringBuilder();var chars=new char[512];int count;
            while((count=reader.Read(chars,0,chars.Length))>0)if(text.Length<4096)text.Append(chars,0,Math.Min(count,4096-text.Length));
            return text.ToString();
        }
        private static string Quote(string value)
        {if(value.IndexOf('"')>=0 || value.EndsWith("\\",StringComparison.Ordinal))throw new ArgumentException("Invalid worker game path.");return "\""+value+"\"";}
        private static string Hash(Stream input)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
    }
}
