using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using HarmonyLib;
using Terraria;
using Terraria.IO;
using Terraria.Social;
using Terraria.Social.Base;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativePlayerRenameChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static bool truncate,skip;
        private static bool BeforeSave(){return !skip;}
        private static void AfterSave(PlayerFileData __0)
        {if(truncate && !__0.IsCloudSave)using(var file=new FileStream(__0.Path,FileMode.Open,FileAccess.Write))file.SetLength(file.Length-1);}
        internal static void Run(object context)
        {
            object owner=Get(Get(context,"Fishing"),"Rename"),runtime=Get(Get(context,"Tools"),"Runtime");var p=Main.LocalPlayer;
            p.itemAnimation=p.itemTime=p.reuseDelay=0;p.dead=false;p.active=true;p.name="Test009";Main.netMode=0;Main.CurrentInputTextTakerOverride=null;Main.playerInventory=false;
            var file=Main.ActivePlayerFileData;file.Metadata=FileMetadata.FromCurrentSettings(FileType.Player);file.Player=p;
            string path=file.Path;Require(path.StartsWith(Terraria.Program.SavePath,StringComparison.OrdinalIgnoreCase) && Main.PlayerPath.StartsWith(Terraria.Program.SavePath,StringComparison.OrdinalIgnoreCase),"all native save locations are isolated");
            typeof(Main).GetField("_achievements",Flags).SetValue(Main.instance,new Terraria.Achievements.AchievementManager());
            Main.mapEnabled=false;
            Main.anglerWhoFinishedToday.Clear();Main.anglerWhoFinishedToday.Add("Test009");Main.anglerQuestFinished=true;
            p.inventory[12].SetDefaults(8);p.inventory[12].stack=7;p.inventory[12].favorited=true;var retained=p.inventory[12];var generation=Get(runtime,"Generation");
            Require((bool)Get(owner,"CanRename"),"native rename admission: "+GetOptional(owner,"Status"));
            Require((bool)Call(owner,"Rename",new object[]{null}) && p.name=="Test010" && (bool)Get(owner,"PersistedVerified"),"real Rename plus outer SavePlayer increments and verifies encrypted player");
            Require(GetOptional(owner,"Message")==null && NativeFishingExperienceChecks.FeedbackText(context,"fishing.rename")=="已改名为「Test010」","verified rename succeeds through player-head feedback without an inline message");
            Require(ReferenceEquals(Main.ActivePlayerFileData,file) && ReferenceEquals(file.Player,p) && file.Path==path && file.Name==p.name && Equals(Get(runtime,"Generation"),generation),"rename retains player, file path, native name cache and R generation");
            Require(!Main.anglerQuestFinished && Main.anglerWhoFinishedToday.SequenceEqual(new[]{"Test009"}),"new runtime name refreshes angler eligibility without clearing native today list");
            Require(File.Exists(Path.Combine(Terraria.Program.SavePath,"achievements.dat")),"actual outer native achievement save ran in isolated root");
            var copy=Player.LoadPlayer(path,false);Require(copy.Player.loadStatus==Terraria.ID.StatusID.Ok && copy.Player.name=="Test010" && copy.Player.inventory[12].type==8 && copy.Player.inventory[12].stack==7 && copy.Player.inventory[12].favorited && ReferenceEquals(p.inventory[12],retained),"test-only native reload validates real player content without replacing runtime player");
            Require((bool)Call(owner,"Rename","  钓鱼009\r\n\t ") && p.name=="钓鱼009","Chinese name normalization through actual native save");
            var before=File.ReadAllBytes(path);Require(!(bool)Call(owner,"Rename",new string('x',21)) && File.ReadAllBytes(path).SequenceEqual(before) && p.name=="钓鱼009" && !(bool)Get(owner,"RuntimeNameApplied") && !(bool)Get(owner,"NativeWriteCompleted") && !(bool)Get(owner,"PersistedVerified"),"invalid name never enters native write or inherits the prior success receipt");
            NativeFishingNetworkChecks.Rename(context,owner);
            Require((bool)Call(owner,"Rename","钓鱼009"),"restore isolated name after multiplayer scenario");
            var ioLock=typeof(Player).GetField("IOLock",Flags).GetValue(null);using(var held=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))
            {
                var thread=new Thread(()=>{lock(ioLock){held.Set();release.WaitOne();}});thread.Start();Require(held.WaitOne(5000),"native autosave contention fixture");
                try{Require(!(bool)Call(owner,"Rename","锁忙") && p.name=="钓鱼009","busy native save lock rejects before runtime mutation");}finally{release.Set();thread.Join();}
            }
            var faults=new Harmony("JueMingR.Tests.RenameSaveFault");var save=typeof(Player).GetMethod("InternalSavePlayerFile",Flags);
            faults.Patch(save,prefix:new HarmonyMethod(typeof(NativePlayerRenameChecks),nameof(BeforeSave)),postfix:new HarmonyMethod(typeof(NativePlayerRenameChecks),nameof(AfterSave)));
            try
            {
                truncate=true;Require(!(bool)Call(owner,"Rename","截断测试") && p.name=="截断测试" && (bool)Get(owner,"RuntimeNameApplied") && !(bool)Get(owner,"PersistedVerified"),"truncated real save is a partial effect, never reported as saved");truncate=false;
                skip=true;Require(!(bool)Call(owner,"Rename","跳过保存") && p.name=="跳过保存" && !(bool)Get(owner,"NativeWriteCompleted"),"skipped native save is not inferred from unchanged or absent exceptions");skip=false;
            }
            finally{truncate=skip=false;foreach(var method in faults.GetPatchedMethods().ToArray())faults.Unpatch(method,HarmonyPatchType.All,faults.Id);}
            var oldCloud=SocialAPI.Cloud;var cloud=new MemoryCloud();SocialAPI.Cloud=cloud;
            var cloudFile=new PlayerFileData("players/g10-isolated.plr",true){Player=p,Metadata=FileMetadata.FromCurrentSettings(FileType.Player)};Main.ActivePlayerFileData=cloudFile;
            try
            {
                Require((bool)Call(owner,"Rename","云端成功"),"real native cloud serialization, actual bool and complete identical readback");
                cloud.Mode=1;Require(!(bool)Call(owner,"Rename","云端拒绝") && p.name=="云端拒绝","cloud false is surfaced after native Rename ignores it");
                cloud.Mode=2;Require(!(bool)Call(owner,"Rename","云端短读"),"short cloud read is rejected with candidate-complement sentinel");
                cloud.Mode=3;Require(!(bool)Call(owner,"Rename","云端旧值"),"cloud true with old same-length body cannot pass exact candidate comparison");
            }
            finally{SocialAPI.Cloud=oldCloud;Main.ActivePlayerFileData=file;file.Player=p;}
            Ui(context,owner,path);
            Call(owner,"Clear");Console.WriteLine("PASS G10 real Rename/outer SavePlayer, local decrypt/reload, identity/angler rules, multiplayer attempt, invalid/lock refusal, truncated/skipped native saves and isolated cloud true/false/short/stale results. Map saving disabled; no real cloud service.");
        }
        private static void Ui(object context,object owner,string path)
        {
            var state=Get(Get(context,"Shell"),"State");var ui=Get(Get(context,"Shell"),"FishingUi");
            Call(state,"Navigate",7);Call(state,"RestoreVisible");NativeFishingUiChecks.Prepare(context);
            NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));
            Require(GetOptional(ui,"Editor")!=null,"two physical field clicks enter the actual rename editor");
            NativeFishingUiChecks.Step(context,false,Vector2.Zero,keys:new KeyboardState(Keys.LeftControl,Keys.A));NativeFishingUiChecks.Step(context,false,Vector2.Zero,"界面钓鱼009");NativeFishingUiChecks.Prepare(context);
            NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"Rename"));
            Require(Main.LocalPlayer.name=="界面钓鱼009" && (bool)Get(owner,"PersistedVerified") && GetOptional(ui,"Editor")==null && Player.LoadPlayer(path,false).Player.name=="界面钓鱼009","Chinese native queue and physical confirm execute real rename/save/reload and release editor");
            NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));
            var bytes=File.ReadAllBytes(path);NativeFishingUiChecks.Step(context,false,Vector2.Zero,"未提交");NativeFishingUiChecks.Step(context,false,Vector2.Zero,"\u001b",new KeyboardState(Keys.Escape));
            Require(GetOptional(ui,"Editor")==null && Main.LocalPlayer.name=="界面钓鱼009" && File.ReadAllBytes(path).SequenceEqual(bytes),"physical Escape discards only the uncommitted name draft");
            NativeFishingUiChecks.Prepare(context);
            var clipboard=Get(Get(ui,"TextInput"),"clipboard");var original=Get(clipboard,"source");var fake=new Clipboard(original.GetType().GetInterface("INotesClipboard"));Set(clipboard,"source",fake.GetTransparentProxy());
            try
            {
                foreach(var pair in new[]{Tuple.Create(new string('A',17)+"\tB",new string('A',17)+" B"),Tuple.Create("A\r\nB","AB")})
                {
                    NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));
                    fake.Text=pair.Item1;NativeFishingUiChecks.Step(context,false,Vector2.Zero,keys:new KeyboardState(Keys.LeftControl,Keys.A));NativeFishingUiChecks.Step(context,false,Vector2.Zero,keys:new KeyboardState(Keys.LeftControl,Keys.V));NativeFishingUiChecks.Step(context,false,Vector2.Zero);NativeFishingUiChecks.Prepare(context);
                    Require((string)Get(Get(ui,"Editor"),"Text")==pair.Item2,"rename paste normalizes tabs/newlines before generic editor filtering");NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"Rename"));Require(Main.LocalPlayer.name==pair.Item2 && (bool)Get(owner,"PersistedVerified"),"normalized paste saves through actual rename confirmation");
                }
                NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));NativeFishingUiChecks.Click(context,NativeFishingUiChecks.Part(ui,"RenameField"));
                fake.Text=new string('x',17000);var editor=Get(ui,"Editor");long revision=(long)Get(editor,"Revision");NativeFishingUiChecks.Step(context,false,Vector2.Zero,keys:new KeyboardState(Keys.LeftControl,Keys.V));NativeFishingUiChecks.Prepare(context);
                Require((long)Get(editor,"Revision")==revision && ((IEnumerable)Get(ui,"pageParts")).Cast<object>().Any(part=>((string)GetOptional(Get(part,"Element"),"Text"))?.Contains("过长")==true),"rejected edit preserves revision but refreshes the visible rename error");
                NativeFishingUiChecks.Step(context,false,Vector2.Zero);NativeFishingUiChecks.Step(context,false,Vector2.Zero,"\u001b",new KeyboardState(Keys.Escape));
            }
            finally{Set(clipboard,"source",original);Call(state,"Close");Call(ui,"Suspend");}
        }
        private sealed class Clipboard : RealProxy
        {
            internal string Text;
            internal Clipboard(Type type):base(type){}
            public override IMessage Invoke(IMessage message)
            {var call=(IMethodCallMessage)message;return new ReturnMessage(true,call.MethodName=="TryPaste"?new object[]{Text}:null,call.MethodName=="TryPaste"?1:0,call.LogicalCallContext,call);}
        }
        // Only the external cloud service is replaced. Serialization, native
        // Rename/SavePlayer and production write/result observers remain real.
        private sealed class MemoryCloud : CloudSocialModule
        {
            internal int Mode;
            private readonly Dictionary<string,byte[]> files=new Dictionary<string,byte[]>();
            public override void Initialize(){}public override void Shutdown(){}
            public override IEnumerable<string> GetFiles(){return files.Keys;}
            [MethodImpl(MethodImplOptions.NoInlining)]
            public override bool Write(string path,byte[] data,int length)
            {if(Mode==1)return false;var bytes=data.Take(length).ToArray();if(Mode==3 && path.EndsWith(".plr"))bytes[bytes.Length/2]^=1;files[path]=bytes;return true;}
            public override void Read(string path,byte[] buffer,int length){byte[] bytes;if(files.TryGetValue(path,out bytes))Array.Copy(bytes,buffer,Math.Min(Math.Min(bytes.Length,length),Mode==2?Math.Max(0,length-1):length));}
            public override Stream OpenRead(string path){return new MemoryStream(files[path],false);}
            public override bool HasFile(string path){return files.ContainsKey(path);}
            public override int GetFileSize(string path){return files[path].Length;}
            public override bool Delete(string path){return files.Remove(path);}
            public override bool Forget(string path){return files.Remove(path);}
        }
    }
}
