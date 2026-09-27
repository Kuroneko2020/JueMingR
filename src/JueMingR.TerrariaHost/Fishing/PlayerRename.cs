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
        internal Func<bool> OwnTextInput;
        internal string Message {get;private set;}
        internal bool RuntimeNameApplied {get;private set;}
        internal bool NativeWriteCompleted {get;private set;}
        internal bool PersistedVerified {get;private set;}
        internal long Revision {get;private set;}
        internal bool CanRename {get{return Reason==null;}}
        internal string Status {get{return Message??Reason;}}
        private string Reason
        {
            get
            {
                if(!available)return "快捷改名暂不可用。";
                if(Main.netMode!=0)return "快捷改名仅限单人。";
                var p=host.Player;var file=Main.ActivePlayerFileData;
                if(busy || p==null || !p.active || !ReferenceEquals(p,Main.LocalPlayer) || Main.gameMenu || p.dead || WorldGen.isGeneratingOrLoadingWorld)return "当前不能改名。";
                if(file==null || !ReferenceEquals(file.Player,p) || string.IsNullOrWhiteSpace(file.Path) || file.Metadata==null || Main.ServerSideCharacter || file.ServerSideCharacter)return "当前角色存档身份不可用。";
                if(file.IsCloudSave && SocialAPI.Cloud==null)return "云存档服务不可用。";
                if(host.Tools.Items.World.Busy || host.Tools.Items.World.HasManualOperation || host.Tools.Use.Active || host.Tools.Fishing.Active || p.UsingOrReusingItem)return "请等待当前操作结束后改名。";
                if(Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.CurrentInputTextTakerOverride!=null && !(OwnTextInput?.Invoke()??false))return "请先结束其它文字输入。";
                return null;
            }
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
            }
            catch(Exception){Message="快捷改名暂不可用，原版存档未改动。";}
        }
        internal bool Rename(string requested)
        {
            RuntimeNameApplied=NativeWriteCompleted=PersistedVerified=false;
            string reason=Reason;if(reason!=null){Message=reason;Revision++;return false;}
            var p=host.Player;string name;
            try{name=requested==null?PlayerNameRules.Increment(p.name):PlayerNameRules.Normalize(requested);}
            catch(ArgumentException error){Message=error.Message;Revision++;return false;}
            if(!Monitor.TryEnter(nativeLock)){Message="角色正在保存，请稍后重试。";Revision++;return false;}
            PlayerRenameHooks.SaveScope scope=null;
            try
            {
                // Recheck after acquiring the same lock used by native autosave.
                if(Reason!=null){Message=Reason;return false;}
                var file=Main.ActivePlayerFileData;var cloud=file.IsCloudSave?SocialAPI.Cloud:null;
                if(cloud!=null)PlayerRenameHooks.EnsureCloud(cloud);
                busy=true;RuntimeNameApplied=NativeWriteCompleted=PersistedVerified=false;
                scope=new PlayerRenameHooks.SaveScope(file,p,file.Path,file.IsCloudSave,cloud,host.Tools.Runtime.Generation);
                PlayerRenameHooks.Begin(scope);
                file.Rename(name);
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
                // Publish only after the native save and readback were verified.
                // A later request or changed name invalidates this short result.
                var feedback=host.Tools.Feedback;
                feedback?.Show("fishing.rename","已改名为「"+name+"」",true,()=>PersistedVerified && p.name==name,feedback.Capture());
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
                // The name used by the actual angler gate is the runtime name,
                // including a partially successful save. Never clear today's list.
                if(ReferenceEquals(host.Player,p) && ReferenceEquals(Main.ActivePlayerFileData?.Player,p))
                {Main.ActivePlayerFileData.Player=p;Main.anglerQuestFinished=Main.anglerWhoFinishedToday.Contains(p.name);}
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
