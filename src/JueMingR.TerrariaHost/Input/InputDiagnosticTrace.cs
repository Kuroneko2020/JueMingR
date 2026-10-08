#if JMR_INPUT_DIAGNOSTIC
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace JueMingR.TerrariaHost.Input
{
    // One-use evidence candidate, absent from ordinary assemblies. This owns
    // only its recorder: it cannot reset capture, focus, leases or input state.
    internal static class InputDiagnosticTrace
    {
        private struct Row {internal long Frame;internal int Point,State,Keys,Mouse,Actions;}
        private static readonly bool enabled=Environment.GetEnvironmentVariable("JMR_INPUT_DIAGNOSTIC")=="1";
        private static readonly object gate=new object(),writeGate=new object();
        private static readonly Row[] rows=new Row[256],previous=new Row[4];
        private static readonly bool[] seen=new bool[4];
        private static readonly long[] calls=new long[4],lastFrame=new long[4];
        private static volatile bool stopped;
        private static bool started,attempted;
        private static long deadline;
        private static int count;
        private static Timer timer;
        private static string reason,path;
        internal static bool Active
        {
            get
            {
                if(!enabled || stopped)return false;
                // A delayed timer/ThreadPool callback cannot admit samples
                // beyond the monotonic window on the next game-thread entry.
                if(started && Stopwatch.GetTimestamp()>=deadline){Stop("120-seconds",false);return false;}
                return true;
            }
        }
        internal static void Observe(int point,long frame,int state,int keys,int mouse,int actions)
        {
            if(!Active)return;
            try
            {
                bool full=false;
                lock(gate)
                {
                    if(stopped)return;
                    if(!started)
                    {
                        deadline=Stopwatch.GetTimestamp()+120*Stopwatch.Frequency;started=true;
                        path=Path.Combine(Terraria.Program.SavePath,"JueMingRData","diagnostics","input-boundary",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Process.GetCurrentProcess().Id+".log");
                        AppDomain.CurrentDomain.ProcessExit+=Exit;
                        timer=new Timer(_=>Stop("120-seconds",false),null,120000,Timeout.Infinite);
                    }
                    calls[point]++;lastFrame[point]=frame;
                    var row=new Row{Point=point,Frame=frame,State=state,Keys=keys,Mouse=mouse,Actions=actions};
                    var prior=previous[point];
                    // Epoch/point cycling is not a semantic change. Arrival
                    // counters still distinguish stable callbacks from a gap.
                    if(seen[point] && prior.State==state && prior.Keys==keys && prior.Mouse==mouse && prior.Actions==actions)return;
                    seen[point]=true;previous[point]=row;rows[count++]=row;
                    full=count==rows.Length;
                    if(full){stopped=true;reason="256-changes";}
                }
                if(full)Stop("256-changes",false);
            }
            catch {Fail();}
        }
        internal static void Fail()
        {stopped=true;if(reason==null)reason="observer-failure";try{timer?.Dispose();}catch{}}
        private static void Exit(object sender,EventArgs args){Stop("normal-process-exit",true);}
        private static void Stop(string why,bool exiting)
        {
            try
            {
                lock(gate){if(!started)return;if(!stopped){stopped=true;reason=why;}timer?.Dispose();}
                if(exiting)Export();else ThreadPool.QueueUserWorkItem(_=>Export());
            }
            catch {Fail();}
        }
        private static void Export()
        {
            bool acquired=false;
            try
            {
                // Normal exit before the time cap exports synchronously. If a
                // prior bounded export is already running, wait at most 250ms;
                // process termination can still truncate it. No promise covers
                // forced termination, crashes or failed filesystem operations.
                acquired=Monitor.TryEnter(writeGate,250);if(!acquired || attempted)return;attempted=true;
                var text=new StringBuilder(32768);
                lock(gate)
                {
                    text.AppendLine("JMR_INPUT_DIAGNOSTIC v1; point:0=Begin,1=Mapping,2=Final,3=Consumers; keys/mouse:-1=unknown");
                    text.AppendLine("host-mvid="+typeof(InputDiagnosticTrace).Assembly.ManifestModule.ModuleVersionId+"; stop="+reason);
                    text.AppendLine("state bits:focus=1,native=2,mapped=4,final=8,rearming=16,quarantine=32,capture=64,suppressed=128,hotkeyTail=256,block=512,writing=1024,canUse=2048,menu=4096,textOwner=8192,mapOwned=16384");
                    for(int p=0;p<4;p++)text.AppendLine("arrival,"+p+","+calls[p]+","+lastFrame[p]);
                    text.AppendLine("actions bits:left=1,right=2,leftRelease=4,rightRelease=8; -1=not-final");
                    text.AppendLine("point,frame,state,keyGroupHeld,mouseMask,gameActions");
                    for(int i=0;i<count;i++){var r=rows[i];text.AppendLine(r.Point+","+r.Frame+","+r.State+","+r.Keys+","+r.Mouse+","+r.Actions);}
                }
                var bytes=Encoding.UTF8.GetBytes(text.ToString());if(bytes.Length>65536){Fail();return;}
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Write(bytes,0,bytes.Length);
            }
            catch {Fail();}
            finally {if(acquired)Monitor.Exit(writeGate);}
        }
    }
}
#endif
