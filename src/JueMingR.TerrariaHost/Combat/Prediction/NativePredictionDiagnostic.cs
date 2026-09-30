#if PREDICTION_DIAGNOSTIC
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // One task-owned investigation, compiled out of ordinary builds. The arm
    // is consumed before recording and never recreated here. Only immutable
    // text crosses to the writer; no Terraria object is read off-thread.
    internal sealed class NativePredictionDiagnostic
    {
        internal const string ArmName="ordinary-live-20260930.arm",ClaimName="ordinary-live-20260930.claimed",LogName="ordinary-live-20260930.log";
        private const int Limit=128,LineLimit=4096;
        private readonly object gate=new object();
        private readonly Queue<string> pending=new Queue<string>();
        private string path;
        private int armed,stopped,count,writing;
        internal bool Initialized {get{return Volatile.Read(ref initialized)!=0;}}
        private int initialized;
        private bool faultRecorded,workerFaultRecorded,workerExitRecorded;
        private long start,nextTick;
        internal NativePredictionDiagnostic(string directory)
        {
            // Claim and log creation are cold background work. A collision,
            // missing arm, or I/O failure disables this optional investigation.
            ThreadPool.QueueUserWorkItem(_=>
            {
                try
                {
                    string arm=Path.Combine(directory,ArmName),claim=Path.Combine(directory,ClaimName);
                    if(!File.Exists(arm) || File.Exists(claim))return;
                    File.Move(arm,claim);
                    string output=Path.Combine(directory,LogName);
                    using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                    using(var writer=new StreamWriter(file,new UTF8Encoding(false)))
                        writer.WriteLine("#112 one-shot ordinary prediction diagnostic; host="+typeof(NativePredictionDiagnostic).Module.ModuleVersionId);
                    path=output;Volatile.Write(ref armed,1);
                }
                catch(Exception error){if(error is OutOfMemoryException)throw;Volatile.Write(ref stopped,1);}
                finally{Volatile.Write(ref initialized,1);}
            });
        }
        internal void Observe(NativePredictionSession session,NpcIdentity identity,long tick,long previousTick)
        {
            if(!CanRecord())return;
            long now=Stopwatch.GetTimestamp();
            if(start==0)start=now;
            if(tick<nextTick && tick>=previousTick)return;
            nextTick=tick+30;
            var worker=session.Worker;var p=Main.LocalPlayer;
            Add("tick="+tick+" previous="+previousTick+" npc="+identity.Type+" slot="+identity.Slot+
                " worker="+(worker==null?-1:worker.State)+" ready="+(worker!=null && worker.ReadyMilliseconds>0)+
                " requests="+session.Requests+" published="+session.Published+" rejected="+session.Rejected+" refused="+session.Refused+
                " failed="+session.Failed+" reason="+session.Reason+
                " mounted="+p.mount.Active+" wing="+p.wingsLogic+" jump="+p.controlJump+" wet="+p.wet+
                " day="+Main.dayTime+" time="+Main.time+" rate="+Main.dayRate+" wind="+Main.windSpeedCurrent+" targetWind="+Main.windSpeedTarget);
        }
        internal void CaptureFault(Exception error)
        {
            if(!CanRecord() || faultRecorded)return;
            faultRecorded=true;Add("first-capture-fault="+error);
        }
        internal void WorkerFault(string reason)
        {
            if(!CanRecord() || workerFaultRecorded)return;
            workerFaultRecorded=true;Add("first-worker-fault="+reason);
        }
        internal void WorkerExit(string details)
        {
            if(!CanRecord() || workerExitRecorded || details==null)return;
            workerExitRecorded=true;Add("worker-exit="+details);
        }
        private void Finish(string reason)
        {Add("stopped="+reason);Volatile.Write(ref stopped,1);}
        private bool CanRecord()
        {
            if(Volatile.Read(ref armed)==0 || Volatile.Read(ref stopped)!=0)return false;
            if(start!=0 && Stopwatch.GetTimestamp()-start>=Stopwatch.Frequency*120L){Finish("time-limit");return false;}
            return true;
        }
        private void Add(string row)
        {
            if(Volatile.Read(ref stopped)!=0)return;
            row=row.Replace('\r',' ').Replace('\n',' ');if(row.Length>LineLimit)row=row.Substring(0,LineLimit);
            lock(gate)
            {
                if(stopped!=0 || count>=Limit)return;
                count++;pending.Enqueue(row);if(count==Limit)Volatile.Write(ref stopped,1);
                if(writing!=0)return;writing=1;
            }
            ThreadPool.QueueUserWorkItem(_=>Drain());
        }
        private void Drain()
        {
            try
            {
                while(true)
                {
                    string row;
                    lock(gate){if(pending.Count==0){writing=0;return;}row=pending.Dequeue();}
                    File.AppendAllText(path,row+Environment.NewLine,new UTF8Encoding(false));
                }
            }
            catch(Exception error)
            {
                if(error is OutOfMemoryException)throw;
                lock(gate){Volatile.Write(ref stopped,1);pending.Clear();writing=0;}
            }
        }
    }
}
#endif
