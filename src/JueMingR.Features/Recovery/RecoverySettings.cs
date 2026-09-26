using System;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Recovery
{
    public sealed class RecoverySettings : IDisposable
    {
        private readonly DocumentWorker<RecoveryOptions> worker;
        private long command;
        private bool feedback;
        public RecoveryOptions Value {get;private set;}=new RecoveryOptions();
        public bool Loaded {get;private set;}
        public bool Busy {get;private set;}
        public bool Protected {get;private set;}
        public bool Ready {get{return Loaded && !Busy && !Protected && Message==null;}}
        public long Revision {get;private set;}
        // Poll is the sole worker consumer; observers cannot publish a draft.
        public long AcceptedCommandId {get;private set;}
        public long CompletedCommandId {get;private set;}
        public bool CompletionSucceeded {get;private set;}
        public string Message {get;private set;}
        public RecoverySettings(IPreferenceStorage storage,int domain)
        {var codec=new RecoveryCodec(domain);worker=new DocumentWorker<RecoveryOptions>(storage,codec.Decode,codec.Encode,Value);}
        public bool Set(RecoveryOptions value)
        {
            if(!Loaded || Busy || Protected || value==null)return false;
            if(!worker.TrySubmit(++command,value))return false;
            AcceptedCommandId=command;
            // Suspension immediately revokes old permits. Enabling, additions
            // and learning publish only after the same reliable file commit.
            Busy=true;Revision++;return true;
        }
        public void Poll()
        {
            DocumentResult<RecoveryOptions> result;if(!worker.TryTake(out result))return;
            if(result.CommandId==0)Loaded=true;
            else {CompletedCommandId=result.CommandId;CompletionSucceeded=result.Success;}
            Busy=false;Protected=result.IsProtected || result.CommitUnconfirmed || result.CommandId==0 && !result.Success;
            if(result.Success){Value=result.Value;Message=null;feedback=false;}
            else{Message=result.CommitUnconfirmed?"设置保存结果未确认，已暂停并保护文件。":"设置无法保存或读取，已暂停；原文件保留。";feedback=true;}
            Revision++;
        }
        public void TakeFeedback(Action<string> display){if(feedback){display(Message);feedback=false;}}
        public bool Stop(int milliseconds){return worker.Stop(milliseconds);}
        public void Dispose(){worker.Dispose();}
    }
}
