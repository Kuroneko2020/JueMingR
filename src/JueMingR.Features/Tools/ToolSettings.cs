using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Tools
{
    public sealed class ToolSettings : IDisposable
    {
        private readonly DocumentWorker<ToolOptions> worker;
        private readonly int domain;
        private long command;
        private bool feedback;
        public ToolOptions Value {get;private set;}=new ToolOptions();
        public int RequestedMode {get;private set;}
        public bool Loaded {get;private set;}
        public bool Busy {get;private set;}
        public bool Protected {get;private set;}
        public bool Ready {get{return Loaded && !Busy && !Protected && Message==null;}}
        public long Revision {get;private set;}
        public long AcceptedCommandId {get;private set;}
        public long CompletedCommandId {get;private set;}
        public bool CompletionSucceeded {get;private set;}
        public string Message {get;private set;}
        public ToolSettings(IPreferenceStorage storage,int domain)
        {
            if(domain<0 || domain>2)throw new ArgumentOutOfRangeException(nameof(domain));this.domain=domain;
            string identity="JueMingR."+new[]{"AutoCapture","HerbHarvest","AutoMining"}[domain];
            worker=new DocumentWorker<ToolOptions>(storage,b=>Decode(b,identity,domain),v=>Encode(v,identity),Value);
        }
        public bool Set(ToolOptions value)
        {
            if(!Loaded || Busy || Protected || value==null || domain==1 && (value.Mode>1 || value.LastMode>1) || !worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;RequestedMode=value.Mode;Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<ToolOptions> r;if(!worker.TryTake(out r))return;
            if(r.CommandId==0)Loaded=true;else{CompletedCommandId=r.CommandId;CompletionSucceeded=r.Success;}
            Busy=false;Protected=r.IsProtected || r.CommitUnconfirmed || r.CommandId==0 && !r.Success;
            if(r.Success){Value=r.Value;Message=null;feedback=false;}
            else{Message=r.CommitUnconfirmed?"设置保存结果未确认，已暂停并保护文件。":"设置无法保存或读取，已暂停；原文件保留。";feedback=true;}
            Revision++;
        }
        public void TakeFeedback(Action<string> display){if(feedback){feedback=false;display(Message);}}
        public bool Stop(int milliseconds){return worker.Stop(milliseconds);}
        public void Dispose(){worker.Dispose();}
        private static ToolOptions Decode(byte[] bytes,string identity,int domain)
        {
            var root=PreferenceJson.Read(bytes,identity,new[]{"format","version","mode","lastMode","categories"});
            int[] values=new int[3];string[] names={"mode","lastMode","categories"};
            for(int i=0;i<3;i++){var e=PreferenceJson.Required(root,names[i],"number");if(e.HasElements || !int.TryParse(e.Value,NumberStyles.None,CultureInfo.InvariantCulture,out values[i]))throw PreferenceJson.Invalid();}
            try{var v=new ToolOptions(values[0],values[1],values[2]);if(domain==1 && (v.Mode>1 || v.LastMode>1))throw PreferenceJson.Invalid();return v;}
            catch(ArgumentException){throw PreferenceJson.Invalid();}
        }
        private static byte[] Encode(ToolOptions v,string identity)
        {return new UTF8Encoding(false,true).GetBytes("{\"format\":\""+identity+"\",\"version\":1,\"mode\":"+v.Mode+",\"lastMode\":"+v.LastMode+",\"categories\":"+v.Categories+"}\n");}
    }
}
