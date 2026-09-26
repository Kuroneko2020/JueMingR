using System;
using System.Linq;
using System.Text;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Processing
{
    public sealed class ProcessingSettings : IDisposable
    {
        private readonly DocumentWorker<ProcessingOptions> worker;
        private long command;
        private bool feedback;
        public ProcessingOptions Value {get;private set;}=new ProcessingOptions();
        public bool Loaded {get;private set;}
        public bool Busy {get;private set;}
        public bool Protected {get;private set;}
        public bool Ready {get{return Loaded && !Busy && !Protected && Message==null;}}
        public long Revision {get;private set;}
        public string Message {get;private set;}
        public ProcessingSettings(IPreferenceStorage storage,int domain)
        {
            if(domain<0 || domain>2)throw new ArgumentOutOfRangeException(nameof(domain));
            string identity="JueMingR."+new[]{"ContinuousBags","Extraction","Reforge"}[domain];
            worker=new DocumentWorker<ProcessingOptions>(storage,bytes=>Decode(bytes,identity,domain==2),value=>Encode(value,identity,domain==2),Value);
        }
        public bool Set(ProcessingOptions value)
        {
            if(!Loaded || Busy || Protected || value==null || !worker.TrySubmit(++command,value))return false;
            // The previous consumption permission ends on submission, before
            // asynchronous disk work. Only a confirmed commit publishes it.
            Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<ProcessingOptions> r;if(!worker.TryTake(out r))return;
            if(r.CommandId==0)Loaded=true;
            Busy=false;Protected=r.IsProtected || r.CommitUnconfirmed || r.CommandId==0 && !r.Success;
            if(r.Success){Value=r.Value;Message=null;feedback=false;}
            else{Message=r.CommitUnconfirmed?"设置保存结果未确认，已暂停并保护文件。":"设置无法保存或读取，已暂停；原文件保留。";feedback=true;}
            Revision++;
        }
        public void TakeFeedback(Action<string> display){if(feedback){feedback=false;display(Message);}}
        public bool Stop(int milliseconds){return worker.Stop(milliseconds);}
        public void Dispose(){worker.Dispose();}
        private static ProcessingOptions Decode(byte[] bytes,string identity,bool list)
        {
            var root=PreferenceJson.Read(bytes,identity,list?new[]{"format","version","enabled","names"}:new[]{"format","version","enabled"});
            var enabled=PreferenceJson.Required(root,"enabled","boolean");
            if(enabled.HasElements || enabled.Value!="true" && enabled.Value!="false")throw PreferenceJson.Invalid();
            string[] names=new string[0];
            if(list){var rows=PreferenceJson.Required(root,"names","array").Elements().ToArray();if(rows.Any(e=>e.Name!="item" || (string)e.Attribute("type")!="string" || e.HasElements))throw PreferenceJson.Invalid();names=rows.Select(e=>e.Value).ToArray();}
            try{return new ProcessingOptions(enabled.Value=="true",names);}catch(ArgumentException){throw PreferenceJson.Invalid();}
        }
        private static byte[] Encode(ProcessingOptions value,string identity,bool list)
        {
            string names=list?",\"names\":["+string.Join(",",value.Names.Select(s=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"")+"\""))+"]":"";
            var bytes=new UTF8Encoding(false,true).GetBytes("{\"format\":\""+identity+"\",\"version\":1,\"enabled\":"+(value.Enabled?"true":"false")+names+"}\n");
            if(bytes.Length>PreferenceJson.MaximumBytes)throw PreferenceJson.Invalid();return bytes;
        }
    }
}
