using System;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Fishing
{
    public sealed class FishingSettings : IDisposable
    {
        private readonly DocumentWorker<FishingOptions> worker;
        private long command;
        private bool feedback;
        public FishingOptions Value {get;private set;}=new FishingOptions();
        public FishingOptions Requested {get;private set;}
        public bool Loaded {get;private set;}
        public bool Busy {get;private set;}
        public bool Protected {get;private set;}
        public bool Ready {get{return Loaded && !Busy && !Protected && Message==null;}}
        public long Revision {get;private set;}
        public long AcceptedCommandId {get;private set;}
        public long CompletedCommandId {get;private set;}
        public bool CompletionSucceeded {get;private set;}
        public string Message {get;private set;}
        public FishingSettings(IPreferenceStorage storage)
        {var codec=new FishingCodec();worker=new DocumentWorker<FishingOptions>(storage,codec.Decode,codec.Encode,Value);}
        public bool Set(FishingOptions value)
        {
            if(!Loaded || Busy || Protected || value==null || !worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;Requested=value;Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<FishingOptions> result;if(!worker.TryTake(out result))return;
            if(result.CommandId==0)Loaded=true;else{CompletedCommandId=result.CommandId;CompletionSucceeded=result.Success;}
            Busy=false;Requested=null;Protected=result.IsProtected || result.CommitUnconfirmed || result.CommandId==0 && !result.Success;
            if(result.Success){Value=result.Value;Message=null;feedback=false;}
            else{Message=result.CommitUnconfirmed?"钓鱼设置保存结果未确认，原文件已保护。":"钓鱼设置未能保存或读取，原有设置保留。";feedback=true;}
            Revision++;
        }
        public void TakeFeedback(Action<string> display){if(feedback){feedback=false;display(Message);}}
        public bool Stop(int milliseconds){return worker.Stop(milliseconds);}
        public void Dispose(){worker.Dispose();}
    }
}
