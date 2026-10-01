using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Linq;
using JueMingR.Features.Combat;
using Terraria;

namespace NativeWorldTextProbe
{
    // Fault injection owns only the exact child created by this isolated Host.
    // No alternate responder, restart of the Host, or direct Failed mutation.
    internal static class NativeCombatFailureRecoveryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation");object before=Get(native,"Worker");
            Require(cache.Read(0)!=null,"Fault starts from an actually accepted Host route.");
            Capacity(native,cache,step,output);
            Process owned=(Process)Get(before,"child");int oldPid=owned.Id;owned.Kill();
            var rows=new List<string>{"frame,tick,shown,failed,path,workerState,childId,reason"};
            var wait=Stopwatch.StartNew();int first=-1,shown=0,frame=0;
            while(wait.Elapsed.TotalSeconds<45 && shown<120)
            {
                step();object worker=Get(native,"Worker");var route=cache.Read(0);
                int pid=(int)Get(worker,"ChildId");bool present=route!=null && pid!=oldPid;
                rows.Add(string.Join(",",frame,Main.GameUpdateCount,present?1:0,(bool)Get(native,"Failed")?1:0,(bool)Get(host,"Path")?1:0,Get(worker,"State"),pid,"\""+((string)Get(native,"Reason")??"").Replace("\"","\"\"")+"\""));
                Require(!(bool)Get(native,"Failed") && (bool)Get(host,"Path"),"A recoverable owned EOF never latches Host pathFailed.");
                if(present){if(first<0)first=frame;shown++;Require(route.SampleTick==Main.GameUpdateCount && route.Count==121,"Recovered consumer reads current plus 120 future steps.");}
                frame++;
            }
            File.WriteAllLines(Path.Combine(output,"failure-recovery.csv"),rows);
            Require(first>=0 && shown==120 && !ReferenceEquals(before,Get(native,"Worker")),"One automatic owned replacement restores a sustained route in the same Host/Session.");
            Require((bool)Get(before,"Closed") && Get(before,"child")==null,"Old process/pipes/leases are fully released before replacement.");
            Console.WriteLine("RECOVERY owned-pid="+oldPid+" -> "+Get(Get(native,"Worker"),"ChildId")+" first-updates="+first+" shown="+shown+" elapsed-ms="+wait.Elapsed.TotalMilliseconds);

            // A replacement which dies before proving any accepted request
            // cannot trigger an unbounded process restart loop.
            var healthy=Get(native,"Worker");((Process)Get(healthy,"child")).Kill();
            object replacement=null;wait.Restart();
            while(wait.Elapsed.TotalSeconds<30)
            {
                Call(host,"Poll");replacement=Get(native,"Worker");
                if(!ReferenceEquals(replacement,healthy) && (int)Get(replacement,"State")==1)break;
                System.Threading.Thread.Sleep(10);
            }
            Require(replacement!=null && !ReferenceEquals(replacement,healthy) && (int)Get(replacement,"State")==1,"One replacement becomes authenticated Ready before testing exhaustion.");
            ((Process)Get(replacement,"child")).Kill();
            for(int i=0;i<60 && !(bool)Get(native,"Failed");i++){Call(host,"Poll");System.Threading.Thread.Sleep(10);}
            Require((bool)Get(native,"Failed") && (bool)Get(host,"Path"),"A second consecutive fault exhausts native recovery without disabling healthy strategies.");
            NPC.ClearAll();Projectile.ClearAll();typeof(NativeCombatLongCoverageChecks).GetMethod("SpawnDestroyer",Flags).Invoke(null,new object[]{false});
            for(int i=0;i<30;i++)step();
            Require(cache.Read(0)!=null && cache.Read(0).Strategy==JueMingR.Platform.Combat.PredictionStrategy.SegmentedTrend,"Actual Host continues a natural segmented path while ordinary worker is terminal.");
            Console.WriteLine("PASS native consecutive-fault budget exhausted / actual segmented Host consumer still current+120");
            Call(host,"Set",1,true);for(int i=0;i<60 && !(bool)Get(Get(native,"Worker"),"Closed");i++)System.Threading.Thread.Sleep(10);
            // Explicit Retry is exercised only after the automatic recovery
            // proof and exhaustion proof, never to manufacture either result.
            NPC.ClearAll();Projectile.ClearAll();typeof(NativeCombatProductionPredictionChecks).GetMethod("Scene",Flags).Invoke(null,new object[]{2,0});
            wait.Restart();do{step();}while((cache.Read(0)==null || cache.Read(0).Strategy!=JueMingR.Platform.Combat.PredictionStrategy.NativeIsolated) && wait.Elapsed.TotalSeconds<30);

            for(int i=0;i<20 && Get(native,"pending")==null;i++)step();
            Require(Get(native,"pending")!=null,"OFF boundary has a real in-flight request.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(host,"Poll");
            long requests=(long)Get(native,"Requests");for(int i=0;i<30;i++)step();
            Require(cache.Read(0)==null && cache.Required==0 && requests==(long)Get(native,"Requests"),"OFF retires late replies and stops sampling while healthy cleanup finishes.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
            for(int i=0;i<120 && cache.Read(0)==null;i++)step();Require(cache.Read(0)!=null,"Actual preference return resumes normal prediction.");
            for(int i=0;i<20 && Get(native,"pending")==null;i++)step();
            Call(host,"OnSessionEnded");Require(cache.Read(0)==null && Get(native,"pending")==null,"World exit retires cache and pending ownership.");
            Console.WriteLine("PASS real Host recovery / current+120 consumer / released old owner / OFF no sampling / late reply retirement / world cleanup");
        }
        private static object Get(object owner,string name){var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static void Capacity(object native,NpcPredictionCache cache,Action step,string output)
        {
            for(int i=0;i<10 && Get(native,"pending")!=null;i++)step();
            var pages=Get(native,"projectiles");
            for(int i=0;i<300;i++)
            {
                var shot=Main.projectile[i];shot.SetDefaults(1);shot.whoAmI=i;shot.active=true;shot.position=new Microsoft.Xna.Framework.Vector2(700,700);shot.velocity=Microsoft.Xna.Framework.Vector2.Zero;
                shot.aiStyle=0;shot.friendly=shot.hostile=false;shot.damage=0;shot.tileCollide=false;shot.ignoreWater=true;shot.timeLeft=10000;
                pages.GetType().GetMethod("Add").Invoke(pages,new object[]{i});
            }
            long refused=(long)Get(native,"Refused");object worker=Get(native,"Worker");long start=Main.GameUpdateCount;string reason="";
            for(int i=0;i<240;i++){step();reason=(string)Get(native,"Reason")??"";if((long)Get(native,"Refused")>refused && reason.StartsWith("PredictionCapacityException",StringComparison.Ordinal))break;}
            Require(reason.StartsWith("PredictionCapacityException",StringComparison.Ordinal) && !(bool)Get(native,"Failed"),"Production Session receives a real combined capacity refusal without global failure.");
            // Reach repeated refusal backoff, then change a real relevant
            // page state without changing target, Host, world or consumer.
            var repeated=Stopwatch.StartNew();
            var attempts=new List<string>{"tick,requests,refused,rejected,workerState,pending,lastAttempt"};
            while(repeated.Elapsed.TotalSeconds<30 && (long)Get(native,"Refused")<refused+4)
            {
                step();attempts.Add(string.Join(",",Main.GameUpdateCount,Get(native,"Requests"),Get(native,"Refused"),Get(native,"Rejected"),Get(worker,"State"),Get(native,"pending")!=null?1:0,Get(native,"lastAttempt")));
            }
            File.WriteAllLines(Path.Combine(output,"capacity-attempts.csv"),attempts);
            Require((long)Get(native,"Refused")>=refused+4,"Repeated identical oversized input exercises bounded growing backoff.");
            long heldRequests=(long)Get(native,"Requests");step();
            Require((long)Get(native,"Requests")==heldRequests && (long)Get(native,"lastAttempt")+3>Main.GameUpdateCount,"Unchanged refused input really waits instead of retrying each update.");
            long rejectedAt=Main.GameUpdateCount;
            foreach(var shot in Main.projectile)shot.active=false;
            long requests=(long)Get(native,"Requests");step();
            Require((long)Get(native,"Requests")>requests,"A changed capacity-bearing page state bypasses the old target's refusal cooldown immediately.");
            int recovery=-1;for(int i=0;i<180;i++){step();var path=cache.Read(0);if(path!=null && path.CaptureTick>rejectedAt){recovery=i;break;}}
            Require(recovery>=0 && ReferenceEquals(worker,Get(native,"Worker")),"Same Host/native owner/worker takes a fresh ordinary request after capacity refusal automatically.");
            File.WriteAllText(Path.Combine(output,"host-capacity.txt"),"start="+start+" refusedAt="+rejectedAt+" recoveryUpdates="+recovery+" reason="+reason+"\n");
            Console.WriteLine("CAPACITY real Host refused and freshly recovered updates="+recovery+" same-worker=true");
        }
        private static void Call(object owner,string name,params object[] args){owner.GetType().GetMethod(name,Flags).Invoke(owner,args);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
