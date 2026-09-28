using System;
using System.Text;
using System.Threading;
using JueMingR.Features.Combat;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class CombatDomainChecks
    {
        internal static void Run()
        {
            var storage=new Storage();
            using(var settings=new CombatSettings(storage))
            {
                Wait(settings,()=>settings.Loaded);
                Require(settings.Ready && settings.Value.EnabledMask==0 && storage.Writes==0,"missing config is off without a fabricated save");
                Require(settings.Set(new CombatOptions(255,30)) && settings.Busy && settings.Value.EnabledMask==0,"accepted command is not applied before completion");
                Require(!settings.Set(new CombatOptions()),"one accepted immutable command at a time");
                Wait(settings,()=>!settings.Busy);
                Require(settings.CompletionSucceeded && settings.AcceptedCommandId==settings.CompletedCommandId && settings.Value.SwitchInterval==30,"committed value and receipt agree");
            }
            using(var reload=new CombatSettings(new Storage{Bytes=storage.Bytes}))
            {Wait(reload,()=>reload.Loaded);Require(reload.Ready && reload.Value.EnabledMask==255 && reload.Value.SwitchInterval==30,"all independent settings reload");}
            string valid=Encoding.UTF8.GetString(storage.Bytes);
            foreach(string bad in new[]{"{",valid.Replace("\"version\":1","\"version\":2"),valid.Replace("255","256"),valid.Replace("30","31"),valid.Replace("255","1.0"),valid.Replace("}",",\"unexpected\":true}")})
                using(var settings=new CombatSettings(new Storage{Bytes=Encoding.UTF8.GetBytes(bad)}))
                {Wait(settings,()=>settings.Loaded);Require(settings.Protected && !settings.Ready && !settings.Set(new CombatOptions()),"bad/future config is protected, never normalized over the source");}
            foreach(bool unknown in new[]{false,true})
                using(var settings=new CombatSettings(new Storage{Fail=true,Unknown=unknown}))
                {
                    Wait(settings,()=>settings.Loaded);Require(settings.Set(new CombatOptions(1)),"fault fixture accepts command");Wait(settings,()=>!settings.Busy);
                    Require(!settings.CompletionSucceeded && !settings.Ready && settings.Value.EnabledMask==0 && settings.Protected==unknown,"failure/unknown never publishes successful state");
                    int messages=0;settings.TakeFeedback(s=>messages++);settings.TakeFeedback(s=>messages++);Require(messages==1,"failure feedback delivered once");
                }
            var ledger=new BattleEndLedger(200);
            ledger.Observe(1,1,125,125,true);ledger.Observe(2,1,126,125,true);ledger.Observe(1,1,125,125,false);
            Require(!ledger.TakeEnded(),"one composite member is insufficient");ledger.Observe(2,1,126,125,false);
            Require(ledger.TakeEnded() && !ledger.TakeEnded(),"last member ends once");ledger.Observe(2,1,126,125,false);Require(!ledger.TakeEnded(),"duplicate terminal is inert");
            ledger.Events(63,true);Require(!ledger.TakeEnded(),"events baseline is silent");ledger.Events(0);Require(ledger.TakeEnded(),"all six event bits end in one batch");
            ledger.Observe(3,1,4,4,true);ledger.Clear();ledger.Observe(3,1,4,4,false);Require(!ledger.TakeEnded(),"new session cannot end old identity");
            var selector=new FacingSelector();selector.Begin(0,0);
            selector.Consider(new FacingCandidate{Slot=1,X=577,Y=0,Width=20,Height=20,LifeMax=50});Require(selector.Finish()==-1,"36 tile boundary excludes out of range hitbox");
            selector.Begin(0,0);selector.Consider(new FacingCandidate{Slot=2,X=570,Y=-10,Width=300,Height=20,LifeMax=50});Require(selector.Finish()==2,"large hitbox in range remains eligible when center is outside");
            selector.Begin(0,0);for(int i=100;i>=0;i--)selector.Consider(new FacingCandidate{Slot=i,X=100,Y=-10,Width=20,Height=20,LifeMax=50});
            Require(selector.Finish()==0,"bounded nearest candidates and equal-score tie are deterministic");
        }
        private static void Wait(CombatSettings settings,Func<bool> done)
        {var stop=System.Diagnostics.Stopwatch.StartNew();while(!done()){settings.Poll();if(stop.ElapsedMilliseconds>3000)throw new TimeoutException("combat worker");Thread.Sleep(1);}}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private sealed class Storage:IPreferenceStorage
        {
            internal byte[] Bytes;internal int Writes;internal bool Fail,Unknown;
            public PreferenceReadResult Read(){return new PreferenceReadResult(Bytes==null?PreferenceReadStatus.Missing:PreferenceReadStatus.Loaded,Bytes,"fixture",null);}
            public PreferenceWriteResult Write(string identity,byte[] bytes)
            {Writes++;if(Fail)return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"injected",Unknown,Unknown);Bytes=(byte[])bytes.Clone();return new PreferenceWriteResult(PreferenceWriteStatus.Saved,"committed",null);}
            public void Dispose(){}
        }
    }
}
