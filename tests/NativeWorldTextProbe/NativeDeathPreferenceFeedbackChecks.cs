using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.DeathHistory;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathPreferenceFeedbackChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"DeathRecords"), original=Get(host,"preferences");
            object oldLocal=GetOptional(host,"localError"),oldReported=GetOptional(host,"reportedError"),oldPreference=GetOptional(host,"reportedPreferenceError");
            var codec=new DeathDisplayCodec();var messages=new List<string>();Action<string> display=messages.Add;
            try
            {
                Set(host,"localError",null);Set(host,"reportedError",null);Set(host,"reportedPreferenceError",null);
                string path=Path.Combine((string)Get(context,"gameDirectory"),"death-preference-consumer","success.json");
                using(var document=new PreferenceDocument<DeathDisplayPreferences>(new AtomicFileDocument(path,65536,true),codec,DeathDisplayPreferences.Default,0))
                {
                    NativeQuickItemChecks.Until(()=>document.Snapshot.IsLoaded);Set(host,"preferences",document);
                    Call(host,"TakeFeedback",display);Require(messages.Count==0,"healthy quantity load has no resident/error success message");
                    Require((bool)Call(host,"SetCount",128),"real death owner accepts a changed count");
                    NativeQuickItemChecks.Until(()=>document.Snapshot.Status==PreferenceStatus.Saved);
                    Require(codec.Decode(File.ReadAllBytes(path)).Count==128,"actual atomic file persists the owner quantity selection");
                    long revision=document.Snapshot.Revision;
                    Require(!(bool)Call(host,"SetCount",128) && document.Snapshot.Revision==revision,"same quantity never creates another save intent");
                    Call(host,"TakeFeedback",display);Require(messages.Count==0,"successful save stays silent");
                    Set(host,"preferences",original);
                }
                foreach(bool unknown in new[]{false,true})
                {
                    byte[] initial=codec.Encode(DeathDisplayPreferences.Default);
                    var storage=new NativeQuickUiChecks.UiStore(initial){Fail=true,Unknown=unknown};
                    using(var document=new PreferenceDocument<DeathDisplayPreferences>(storage,codec,DeathDisplayPreferences.Default,0))
                    {
                        NativeQuickItemChecks.Until(()=>document.Snapshot.IsLoaded);Set(host,"preferences",document);
                        Set(host,"reportedError",null);Set(host,"reportedPreferenceError",null);Set(host,"localError",null);messages.Clear();
                        Require((bool)Call(host,"SetCount",512),"real death owner accepts an in-memory selection before storage outcome");
                        NativeQuickItemChecks.Until(()=>document.Snapshot.Status==PreferenceStatus.IoFailure);
                        Require(document.Snapshot.Value.Count==512 && document.Snapshot.CommitUnconfirmed==unknown,"real preference worker retains the choice and exact failure classification");
                        if(unknown)Set(host,"localError","isolated-ledger-error");
                        Call(host,"TakeFeedback",display);
                        string preference=messages.Single(m=>m.StartsWith("死亡点设置：",StringComparison.Ordinal));
                        if(unknown)
                            Require(messages.Count==2 && messages.Any(m=>m.Contains("死亡记录或世界天数")) && preference.Contains("无法确认") && preference.Contains("本次仍可使用") && preference.Contains("原文件已保护"),"ledger error and unknown preference persistence both retain their distinct consequences");
                        else Require(messages.Count==1 && preference.Contains("读取或保存失败") && preference.Contains("当前选择仅本次有效") && preference.Contains("原文件已保留"),"failed preference save reaches the real consumer with concrete session-only/original-file consequence");
                        foreach(string message in messages)Console.WriteLine("DEATH PREFERENCE FEEDBACK "+message);
                        int count=messages.Count;Call(host,"TakeFeedback",display);Require(messages.Count==count,"unchanged failures are delivered only once per actual error channel");
                        Set(host,"reportedPreferenceError",null);
                        try {Call(host,"TakeFeedback",(Action<string>)(_=>{throw new InvalidOperationException("isolated display failure");}));throw new Exception("display failure expected");}
                        catch(TargetInvocationException error){Require(error.InnerException is InvalidOperationException,"actual failed display is preserved");}
                        Require(GetOptional(host,"reportedPreferenceError")==null,"failed display cannot acknowledge the preference warning");
                        Call(host,"TakeFeedback",display);Require(messages.Count==count+1,"a warning whose display failed remains deliverable");
                        Require((bool)Call(host,"SetCount",1024) && document.Snapshot.Value.Count==1024 && document.Snapshot.CommitUnconfirmed==unknown,"protected/failed persistence still permits this-session quantity choice without lying about save outcome");
                        Require(initial.SequenceEqual((byte[])Get(storage,"bytes")),"controlled failed storage port preserves its original bytes");
                        Set(host,"preferences",original);
                    }
                }
            }
            finally{Set(host,"preferences",original);Set(host,"localError",oldLocal);Set(host,"reportedError",oldReported);Set(host,"reportedPreferenceError",oldPreference);}
            Console.WriteLine("PASS actual death count preference/atomic save and controlled failed/unknown storage results through PreferenceDocument and Host.TakeFeedback, coexisting ledger errors and failed-display retry; no resident description or user data.");
        }
    }
}
