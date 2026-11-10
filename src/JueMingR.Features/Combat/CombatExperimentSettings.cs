using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Combat
{
    public sealed class CombatExperimentSettings : IDisposable
    {
        private readonly DocumentWorker<CombatExperimentOptions> worker;
        private long command;
        private bool feedback;
        public CombatExperimentOptions Value {get;private set;}=new CombatExperimentOptions();
        public bool Loaded {get;private set;}
        public bool Busy {get;private set;}
        public bool Protected {get;private set;}
        // An accepted disk command has not changed the committed preference.
        // Keep healthy runtime consumers on Value until its actual completion;
        // only further configuration is serialized by Busy.
        public bool CanRun {get{return Loaded && !Protected && Message==null;}}
        public bool Ready {get{return CanRun && !Busy;}}
        public long Revision {get;private set;}
        public long AcceptedCommandId {get;private set;}
        public long CompletedCommandId {get;private set;}
        public bool CompletionSucceeded {get;private set;}
        public string Message {get;private set;}
        public CombatExperimentSettings(IPreferenceStorage storage)
        {worker=new DocumentWorker<CombatExperimentOptions>(storage,Decode,Encode,Value);}
        public bool Set(CombatExperimentOptions value)
        {
            if(!Ready || value==null || !worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<CombatExperimentOptions> result;if(!worker.TryTake(out result))return;
            if(result.CommandId==0)Loaded=true;
            else{CompletedCommandId=result.CommandId;CompletionSucceeded=result.Success;}
            Busy=false;Protected=result.IsProtected || result.CommitUnconfirmed || result.CommandId==0 && !result.Success;
            if(result.Success){Value=result.Value;Message=null;feedback=false;}
            else{Message=FailureMessage(result);feedback=true;}
            Revision++;
        }
        public void TakeFeedback(Action<string> display){if(feedback){feedback=false;display(Message);}}
        public bool Stop(int milliseconds){return worker.Stop(milliseconds);}
        public void Dispose(){worker.Dispose();}
        private static string FailureMessage(DocumentResult<CombatExperimentOptions> result)
        {
            if(result.CommitUnconfirmed)return "隐藏战斗设置保存结果未确认，已暂停并保护文件。";
            string error=result.Error??string.Empty;
            string reason=error=="UnsupportedVersion"?"版本不受支持":error=="UnknownFields"?"含有未知字段":error=="Invalid"?"内容损坏或字段无效":
                error.IndexOf("access-denied",StringComparison.Ordinal)>=0?"没有文件访问权限":error.IndexOf("identity",StringComparison.Ordinal)>=0?"文件已被其它程序修改":
                error=="another-writer"?"文件正由其它程序使用":error=="document-too-large"?"文件超过允许大小":result.CommandId==0?"读取失败":"保存失败";
            return "隐藏战斗设置"+reason+"，已暂停；原文件保留。";
        }
        private static CombatExperimentOptions Decode(byte[] bytes)
        {
            var root=PreferenceJson.Read(bytes,"JueMingR.CombatExperiments",new[]{"format","version","enabledMask"});
            int mask;
            var field=PreferenceJson.Required(root,"enabledMask","number");
            if(field.HasElements || !int.TryParse(field.Value,NumberStyles.None,CultureInfo.InvariantCulture,out mask))throw PreferenceJson.Invalid();
            try{return new CombatExperimentOptions(mask);}catch(ArgumentException){throw PreferenceJson.Invalid();}
        }
        private static byte[] Encode(CombatExperimentOptions value)
        {return new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR.CombatExperiments\",\"version\":1,\"enabledMask\":"+value.EnabledMask+"}\n");}
    }
}
