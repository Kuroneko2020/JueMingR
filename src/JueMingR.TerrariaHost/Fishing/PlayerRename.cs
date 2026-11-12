using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Features.Fishing;
using Terraria;
using Terraria.IO;
using Terraria.Social;
using Terraria.Social.Base;

namespace JueMingR.TerrariaHost.Fishing
{
    // One explicit command owns the native call and its evidence. A failed save
    // can already have changed the runtime name: never retry or invent rollback.
    internal sealed class PlayerRename : IDisposable
    {
        private const int MaximumSaveBytes=64*1024*1024;
        private readonly HostFishing host;
        private readonly object nativeLock;
        private readonly byte[] nativeKey;
        private bool available,busy;
        private bool nativeRenameStarted,deferred;
        internal sealed class Result
        {
            internal bool Started,Deferred,RuntimeNameApplied,NativeWriteCompleted,PersistedVerified;
            internal string Name,Message;
        }
        internal Func<bool> OwnTextInput;
        internal string Message {get;private set;}
        internal bool RuntimeNameApplied {get;private set;}
        internal bool NativeWriteCompleted {get;private set;}
        internal bool PersistedVerified {get;private set;}
        internal long Revision {get;private set;}
        internal long ManualRevision {get;private set;}
        internal bool CanRename {get{return Reason==null;}}
        internal string Status {get{return Message??Reason;}}
        private string Reason {get{bool wait;return GetReason(false,out wait);}}
        private string GetReason(bool automatic,out bool wait)
        {
            wait=false;
            if(!available)return "快捷改名暂不可用。";
            var p=host.Player;var file=Main.ActivePlayerFileData;
            if(busy || p==null || !p.active || !ReferenceEquals(p,Main.LocalPlayer) || Main.gameMenu || p.dead || WorldGen.isGeneratingOrLoadingWorld)return "当前不能改名。";
            if(file==null || !ReferenceEquals(file.Player,p) || string.IsNullOrWhiteSpace(file.Path) || file.Metadata==null)return "当前角色存档身份不可用。";
            if(file.IsCloudSave && SocialAPI.Cloud==null)return "云存档服务不可用。";
            if(host.Tools.Items.World.Busy || host.Tools.Items.World.HasManualOperation || host.Tools.Use.Active || host.Tools.Fishing.Active || p.UsingOrReusingItem)
            {wait=true;return "请等待当前操作结束后改名。";}
            // Only the explicit editor confirmation may use its own text lease.
            // A completed NPC submission must never confirm or consume a draft.
            if(Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.CurrentInputTextTakerOverride!=null && (automatic || !(OwnTextInput?.Invoke()??false)))
            {wait=true;return "请先结束其它文字输入。";}
            return null;
        }
        internal PlayerRename(HostFishing owner)
        {
            host=owner;
            try
            {
                nativeLock=typeof(Player).GetField("IOLock",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);
                nativeKey=(byte[])typeof(Player).GetField("ENCRYPTION_KEY",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);
                if(nativeLock==null || nativeKey==null || nativeKey.Length!=16)throw new MissingFieldException("native player save identity");
                PlayerRenameHooks.Install();available=true;
                if(!PlayerRenameHooks.InstallMapOmission())host.Report("改名仍会保存探索地图，可能短暂停顿。");
            }
            catch(Exception){Message="快捷改名暂不可用，原版存档未改动。";}
        }
        internal bool Rename(string requested){ManualRevision++;return Execute(requested,false);}
        internal Result RenameAutomatic()
        {
            Execute(null,true);
            // Capture AFTER finally: runtime eligibility and message4 can change
            // the final result even when the native local save succeeded.
            return new Result{Started=nativeRenameStarted,Deferred=deferred,RuntimeNameApplied=RuntimeNameApplied,
                NativeWriteCompleted=NativeWriteCompleted,PersistedVerified=PersistedVerified,Name=host.Player?.name,Message=Message};
        }
        private bool Execute(string requested,bool automatic)
        {
            bool saved=RenameCore(requested,automatic);
            if(saved && Message==null)
            {
                var p=host.Player;string name=p?.name;var feedback=host.Tools.Feedback;
                feedback?.Show("fishing.rename","已改名为「"+name+"」",true,()=>PersistedVerified && ReferenceEquals(host.Player,p) && p.name==name,feedback.Capture());
            }
            return saved;
        }
        private bool RenameCore(string requested,bool automatic)
        {
            nativeRenameStarted=deferred=false;RuntimeNameApplied=NativeWriteCompleted=PersistedVerified=false;
            string reason=GetReason(automatic,out deferred);if(reason!=null){if(!automatic || !deferred){Message=reason;Revision++;}return false;}
            var p=host.Player;string previousName=p.name,name;
            try{name=requested==null?PlayerNameRules.Increment(p.name):PlayerNameRules.Normalize(requested);}
            catch(ArgumentException error){Message=error.Message;Revision++;return false;}
            if(!Monitor.TryEnter(nativeLock)){deferred=true;if(!automatic){Message="角色正在保存，请稍后重试。";Revision++;}return false;}
            PlayerRenameHooks.SaveScope scope=null;
            try
            {
                // Recheck after acquiring the same lock used by native autosave.
                string blocked=GetReason(automatic,out deferred);if(blocked!=null){Message=blocked;return false;}
                var file=Main.ActivePlayerFileData;var cloud=file.IsCloudSave?SocialAPI.Cloud:null;
                if(cloud!=null)PlayerRenameHooks.EnsureCloud(cloud);
                busy=true;RuntimeNameApplied=NativeWriteCompleted=PersistedVerified=false;
                scope=new PlayerRenameHooks.SaveScope(file,p,file.Path,file.IsCloudSave,cloud,host.Tools.Runtime.Generation);
                PlayerRenameHooks.Begin(scope);
                scope.NativeRenameActive=true;
                try{nativeRenameStarted=true;file.Rename(name);}
                finally{scope.NativeRenameActive=false;}
                RuntimeNameApplied=String.Equals(p.name,name,StringComparison.Ordinal);
                NativeWriteCompleted=scope.Completed==1 && scope.Started==1 && scope.Depth==0 && !scope.Failed;
                if(!scope.Matches(host.Player,host.Tools.Runtime.Generation) || !RuntimeNameApplied || !NativeWriteCompleted)throw new IOException("Native player write not confirmed.");
                byte[] bytes;
                if(cloud!=null)
                {
                    if(scope.CloudWrites!=1 || !scope.CloudSucceeded || scope.Candidate==null || !ReferenceEquals(cloud,SocialAPI.Cloud))throw new IOException("Cloud write not confirmed.");
                    bytes=scope.Candidate;
                    if(!cloud.HasFile(scope.Path) || cloud.GetFileSize(scope.Path)!=bytes.Length)throw new IOException("Cloud size differs.");
                    var readback=new byte[bytes.Length];for(int i=0;i<bytes.Length;i++)readback[i]=(byte)~bytes[i];
                    cloud.Read(scope.Path,readback,readback.Length);
                    if(!cloud.HasFile(scope.Path) || cloud.GetFileSize(scope.Path)!=bytes.Length)throw new IOException("Cloud size changed.");
                    for(int i=0;i<bytes.Length;i++)if(readback[i]!=bytes[i])throw new IOException("Cloud readback differs.");
                }
                else
                {
                    using(var input=new FileStream(scope.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        if(input.Length<=0 || input.Length>MaximumSaveBytes)throw new IOException("Player file length is unavailable.");
                        bytes=new byte[(int)input.Length];int offset=0;while(offset<bytes.Length){int read=input.Read(bytes,offset,bytes.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}
                        if(input.ReadByte()!=-1)throw new IOException("Player file length changed.");
                    }
                }
                Verify(bytes,name);
                if(!scope.Matches(host.Player,host.Tools.Runtime.Generation))throw new IOException("Player identity changed.");
                PersistedVerified=true;Message=null;
                return true;
            }
            catch(Exception)
            {
                RuntimeNameApplied=String.Equals(p.name,name,StringComparison.Ordinal);
                NativeWriteCompleted=scope!=null && scope.Completed==1 && scope.Started==1 && !scope.Failed;
                Message=RuntimeNameApplied?"运行时名字已改为「"+name+"」，角色存档保存未能确认；未自动重试。":"改名未完成，角色存档保存未能确认；未自动重试。";
                return false;
            }
            finally
            {
                PlayerRenameHooks.End(scope);busy=false;Revision++;
                // Only an ACTUAL name change changes this local eligibility.
                // Cloud hook setup can fail before Rename; in a client the local
                // list may be empty even though vanilla just completed the task.
                // Preserve that finished flag when the old name never changed.
                // Partial native saves still use their actual new runtime name.
                if(ReferenceEquals(host.Player,p) && ReferenceEquals(Main.ActivePlayerFileData?.Player,p) && !String.Equals(previousName,p.name,StringComparison.Ordinal))
                {
                    Main.ActivePlayerFileData.Player=p;Main.anglerQuestFinished=Main.anglerWhoFinishedToday.Contains(p.name);
                    // Vanilla's regular client diff does not include name.
                    // Notify once even when native SSC saving was skipped;
                    // serialization is not a server acknowledgement or retry.
                    if(RuntimeNameApplied && Main.netMode==1)
                    {
                        try{NetMessage.SendData(4,-1,-1,null,p.whoAmI);}
                        catch(Exception){Message=PersistedVerified?"本地改名已保存，联机通知未能发送。":Message+"联机通知未能发送。";}
                    }
                }
                Monitor.Exit(nativeLock);
            }
        }
        private void Verify(byte[] bytes,string name)
        {
            if(bytes.Length==0 || bytes.Length%16!=0 || bytes.Length>MaximumSaveBytes)throw new IOException("Incomplete encrypted player file.");
            byte[] plain;
            using(var cipher=new RijndaelManaged())using(var decryptor=cipher.CreateDecryptor(nativeKey,nativeKey))plain=decryptor.TransformFinalBlock(bytes,0,bytes.Length);
            // Full PKCS7 decryption is paired with the normally completed native
            // write; it is not a general integrity checksum or crash-durability proof.
            using(var reader=new BinaryReader(new MemoryStream(plain),new UTF8Encoding(false,true)))
            {if(reader.ReadInt32()!=326)throw new IOException("Unexpected player version.");FileMetadata.Read(reader,FileType.Player);if(!String.Equals(reader.ReadString(),name,StringComparison.Ordinal))throw new IOException("Persisted player name differs.");}
        }
        internal void Clear(){Message=null;RuntimeNameApplied=NativeWriteCompleted=PersistedVerified=false;Revision++;}
        public void Dispose(){PlayerRenameHooks.Uninstall();available=false;}
    }
}
