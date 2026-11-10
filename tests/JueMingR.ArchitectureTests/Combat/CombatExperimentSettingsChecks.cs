using System;
using System.Text;
using JueMingR.Features.Combat;
namespace JueMingR.ArchitectureTests
{
    internal static class CombatExperimentSettingsChecks
    {
        internal static void Run()
        {
            var storage=new HotkeyCoreChecks.MemoryStorage();
            using(var settings=new CombatExperimentSettings(storage))
            {
                Wait(settings,()=>settings.Ready);
                Require(settings.Value.EnabledMask==0 && storage.Bytes==null,"Missing experiment document is all off without a write");
                storage.Block.Reset();Require(settings.Set(settings.Value.WithEnabled(0,true)),"Accept explicit lock preference");
                Require(settings.Busy && settings.CanRun && settings.Value.EnabledMask==0,"Pending save does not publish an optimistic switch");
                storage.Block.Set();Wait(settings,()=>!settings.Busy);
                Require(settings.CompletionSucceeded && settings.Value.EnabledMask==1,"Reliable saved mask publishes exactly its accepted value");
                storage.Fail=true;Require(settings.Set(settings.Value.WithEnabled(2,true)),"Accept candidate for controlled failure");Wait(settings,()=>!settings.Busy);
                Require(!settings.CompletionSucceeded && settings.Value.EnabledMask==1 && !settings.CanRun && settings.Message!=null,"Failure retains committed preference and pauses this domain");
            }
            foreach(string field in new[]{"8","-1","true","1.0"})
            {
                var bad=new HotkeyCoreChecks.MemoryStorage{Bytes=Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.CombatExperiments\",\"version\":1,\"enabledMask\":"+field+"}")};
                using(var settings=new CombatExperimentSettings(bad)){Wait(settings,()=>settings.Loaded);Require(settings.Protected && settings.Value.EnabledMask==0 && !settings.CanRun,"Invalid experiment document stays protected");}
            }
            var unknown=new HotkeyCoreChecks.MemoryStorage{Unknown=true,Fail=true};
            using(var settings=new CombatExperimentSettings(unknown))
            {Wait(settings,()=>settings.Ready);settings.Set(new CombatExperimentOptions(7));Wait(settings,()=>!settings.Busy);Require(settings.Protected && !settings.CanRun && settings.Value.EnabledMask==0,"Unknown commit cannot publish or retry");}
        }
        private static void Wait(CombatExperimentSettings settings,Func<bool> done)
        {var time=System.Diagnostics.Stopwatch.StartNew();while(!done() && time.ElapsedMilliseconds<5000){settings.Poll();System.Threading.Thread.Sleep(1);}Require(done(),"Controlled experiment worker completed");}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
