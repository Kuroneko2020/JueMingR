using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Combat
{
    public sealed class CombatSettings : IDisposable
    {
        private readonly DocumentWorker<CombatOptions> worker;
        private long command;
        private bool feedback;
        public CombatOptions Value {get;private set;}=new CombatOptions();
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
        public CombatSettings(IPreferenceStorage storage)
        {worker=new DocumentWorker<CombatOptions>(storage,Decode,Encode,Value);}
        public bool Set(CombatOptions value)
        {
            if(!Ready || value==null || !worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<CombatOptions> result;if(!worker.TryTake(out result))return;
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
        private static string FailureMessage(DocumentResult<CombatOptions> result)
        {
            if(result.CommitUnconfirmed)return "战斗设置保存结果未确认，已暂停并保护文件。";
            string error=result.Error??string.Empty;
            string reason=error=="UnsupportedVersion"?"版本不受支持":error=="UnknownFields"?"含有未知字段":error=="Invalid"?"内容损坏或字段无效":
                error.IndexOf("access-denied",StringComparison.Ordinal)>=0?"没有文件访问权限":error.IndexOf("identity",StringComparison.Ordinal)>=0?"文件已被其它程序修改":
                error=="another-writer"?"文件正由其它程序使用":error=="document-too-large"?"文件超过允许大小":result.CommandId==0?"读取失败":"保存失败";
            return "战斗设置"+reason+"，已暂停；原文件保留。";
        }
        private static CombatOptions Decode(byte[] bytes)
        {
            var root=PreferenceJson.Read(bytes,"JueMingR.Combat",new[]{"format","version","enabledMask","switchInterval"});
            int mask,interval;
            var a=PreferenceJson.Required(root,"enabledMask","number");var b=PreferenceJson.Required(root,"switchInterval","number");
            if(a.HasElements || b.HasElements || !int.TryParse(a.Value,NumberStyles.None,CultureInfo.InvariantCulture,out mask) ||
                !int.TryParse(b.Value,NumberStyles.None,CultureInfo.InvariantCulture,out interval))throw PreferenceJson.Invalid();
            try{return new CombatOptions(mask,interval);}catch(ArgumentException){throw PreferenceJson.Invalid();}
        }
        private static byte[] Encode(CombatOptions value)
        {return new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR.Combat\",\"version\":1,\"enabledMask\":"+value.EnabledMask+",\"switchInterval\":"+value.SwitchInterval+"}\n");}
    }
}
