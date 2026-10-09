using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Combat
{
    public sealed class ObservationSettings : IDisposable
    {
        private readonly DocumentWorker<ObservationOptions> worker;
        private long command;
        private bool feedback;
        public ObservationOptions Value {get;private set;}=new ObservationOptions();
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
        public ObservationSettings(IPreferenceStorage storage)
        {worker=new DocumentWorker<ObservationOptions>(storage,Decode,Encode,Value);}
        public bool Set(ObservationOptions value)
        {
            if(!Ready || value==null || !worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<ObservationOptions> result;if(!worker.TryTake(out result))return;
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
        private static string FailureMessage(DocumentResult<ObservationOptions> result)
        {
            if(result.CommitUnconfirmed)return "战斗显示设置保存结果未确认，已暂停并保护文件。";
            string error=result.Error??string.Empty;
            string reason=error=="UnsupportedVersion"?"版本不受支持":error=="UnknownFields"?"含有未知字段":error=="Invalid"?"内容损坏或字段无效":
                error.IndexOf("access-denied",StringComparison.Ordinal)>=0?"没有文件访问权限":error.IndexOf("identity",StringComparison.Ordinal)>=0?"文件已被其它程序修改":
                error=="another-writer"?"文件正由其它程序使用":error=="document-too-large"?"文件超过允许大小":result.CommandId==0?"读取失败":"保存失败";
            return "战斗显示设置"+reason+"，已暂停；原文件保留。";
        }
        private static ObservationOptions Decode(byte[] bytes)
        {
            var root=PreferenceJson.ReadOptional(bytes,"JueMingR.CombatObservation","marker",new[]{"format","version","collision","path","clearLine","mouseCenter","dummy","radius"});
            Func<string,bool> flag=name=>{var node=PreferenceJson.Required(root,name,"boolean");bool value;if(node.HasElements || !bool.TryParse(node.Value,out value))throw PreferenceJson.Invalid();return value;};
            var radius=PreferenceJson.Required(root,"radius","number");int amount;
            if(radius.HasElements || !int.TryParse(radius.Value,NumberStyles.None,CultureInfo.InvariantCulture,out amount))throw PreferenceJson.Invalid();
            try{return new ObservationOptions(flag("collision"),flag("path"),flag("clearLine"),flag("mouseCenter"),flag("dummy"),amount,root.Element("marker")!=null && flag("marker"));}catch(ArgumentException){throw PreferenceJson.Invalid();}
        }
        private static byte[] Encode(ObservationOptions value)
        {
            return new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR.CombatObservation\",\"version\":1,\"collision\":"+B(value.Collision)+",\"path\":"+B(value.Path)+",\"clearLine\":"+B(value.ClearLine)+",\"mouseCenter\":"+B(value.MouseCenter)+",\"dummy\":"+B(value.Dummy)+",\"radius\":"+value.Radius.ToString(CultureInfo.InvariantCulture)+",\"marker\":"+B(value.Marker)+"}\n");
        }
        private static string B(bool value){return value?"true":"false";}
    }
}
