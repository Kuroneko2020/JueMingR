using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JueMingR.Features.Combat;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMenuPreparationChecks
    {
        internal static void Run(object context,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction"),native=Get(source,"Native"),worker=null;
            var cache=(NpcPredictionCache)Get(source,"Cache");
            try
            {
                Main.gameMenu=true;Call(context,"UpdateRuntime");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                for(int i=0;i<10;i++)Call(context,"UpdateRuntime");
                Require(GetOptional(native,"Worker")==null,"Menu all-off has no expensive preparation.");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));
                for(int i=0;i<10;i++)Call(context,"UpdateRuntime");
                Require(GetOptional(native,"Worker")==null,"Collision-only is not prediction intent.");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
                Call(context,"UpdateRuntime");worker=GetOptional(native,"Worker");
                Require(worker!=null,"Reliable menu prediction intent starts preparation through the production Host callback.");
                Require((long)Get(native,"Requests")==0 && cache.Read(0)==null,"Menu prepares code without sampling a world.");
                // A fast world entry must reuse even an unfinished preparation.
                Main.gameMenu=false;Call(context,"UpdateRuntime");
                Require(ReferenceEquals(worker,Get(native,"Worker")),"Entering a world does not discard menu preparation.");
                var watch=Stopwatch.StartNew();double longest=0;
                while((int)Get(worker,"State")!=1 && watch.ElapsedMilliseconds<65000)
                {
                    var update=Stopwatch.StartNew();Call(context,"UpdateRuntime");longest=Math.Max(longest,update.Elapsed.TotalMilliseconds);
                    Require((int)Get(worker,"State")!=4,"Menu worker fault: "+GetOptional(worker,"Failure"));Thread.Sleep(10);
                }
                Require((int)Get(worker,"State")==1,"World can use the prepared environment.");
                int pid=(int)Get(worker,"ChildId");
                Console.WriteLine("MENU fast-entry remaining-ms="+watch.Elapsed.TotalMilliseconds.ToString("F3")+" maximum-callback-ms="+longest.ToString("F3")+" ready-ms="+Get(worker,"ReadyMilliseconds"));
                NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(context,"UpdateRuntime");
                Require(!(bool)Get(worker,"Closed") && cache.Read(0)==null,"Short OFF releases world results but retains dormant preparation.");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));Call(context,"UpdateRuntime");
                Require(ReferenceEquals(worker,Get(native,"Worker")),"Short OFF/ON retains the same child.");
                Main.gameMenu=true;Call(context,"UpdateRuntime");
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(output,"other-world.wld"),false){UniqueId=Guid.NewGuid()};
                Main.gameMenu=false;Call(context,"UpdateRuntime");
                Require(ReferenceEquals(worker,Get(native,"Worker")) && (int)Get(worker,"ChildId")==pid,"Another world retains only the prepared execution environment.");
                Require((long)Get(native,"Requests")==0 && cache.Read(0)==null,"No-target world and transitions do not run predictions.");
                Console.WriteLine("PASS actual Host menu intent / all-off and collision-only / fast entry / no target / short OFF-ON / other world / same prepared child");
            }
            finally
            {
                Call(host,"Exit",null,EventArgs.Empty);if(worker==null)worker=GetOptional(native,"Worker");
                if(worker!=null)
                {
                    var wait=Stopwatch.StartNew();while(!(bool)Get(worker,"Closed")&&wait.ElapsedMilliseconds<6000)Thread.Sleep(10);
                    Require((bool)Get(worker,"Closed"),"Final Host exit reclaims menu-owned preparation.");
                    File.WriteAllText(Path.Combine(output,"menu-worker.log"),(string)GetOptional(worker,"Diagnostics")??"");
                }
            }
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
