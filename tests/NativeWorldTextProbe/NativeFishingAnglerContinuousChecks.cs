using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.IO;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeFishingUiChecks;

namespace NativeWorldTextProbe
{
    // Actual outer vanilla submissions, rewards, achievements, saves and client
    // serialization. No server, user player/world or external cloud is used.
    internal static class NativeFishingAnglerContinuousChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int rewards,saves;
        private static bool skipSave,throwAtEnd;
        private static readonly List<byte> messages=new List<byte>();
        private static readonly List<string> sentNames=new List<string>();
        private static void Reward(){rewards++;if(throwAtEnd)throw new InvalidOperationException("isolated real reward tail failure");}
        private static bool Save(){saves++;return !skipSave;}
        private static bool SkipUnrelatedAchievement(){return false;}
        private static void Sent(int __0)
        {
            if(Main.netMode!=1 || __0!=4 && __0!=75)return;
            var buffer=NetMessage.buffer[256].writeBuffer;int size=BitConverter.ToUInt16(buffer,0);
            Require(size>=3 && size<=buffer.Length && buffer[2]==__0,"actual original serialized frame, not a requested message number");
            if(__0==4)using(var reader=new BinaryReader(new MemoryStream(buffer,0,size)))
            {reader.ReadInt16();reader.ReadByte();reader.ReadByte();reader.ReadByte();reader.ReadByte();reader.ReadSingle();reader.ReadByte();Require(reader.ReadString()==Main.LocalPlayer.name,"message4 serializes the new actual name");}
            messages.Add((byte)__0);sentNames.Add(Main.LocalPlayer.name);
        }
        internal static void Run(object context)
        {
            object host=Get(context,"Fishing"),owner=Get(host,"Angler"),rename=Get(host,"Rename"),shell=Get(context,"Shell"),state=Get(shell,"State"),ui=Get(shell,"FishingUi");
            Require((bool)Get(owner,"Available"),"independent native angler hook installed");
            var p=Main.LocalPlayer;var file=Main.ActivePlayerFileData;
            p.active=true;p.dead=false;p.itemAnimation=p.itemTime=p.reuseDelay=0;Main.gameMenu=false;Main.netMode=0;Main.CurrentInputTextTakerOverride=null;Main.drawingPlayerChat=Main.editSign=Main.editChest=false;
            file.Player=p;file.Metadata=FileMetadata.FromCurrentSettings(FileType.Player);Main.mapEnabled=false;
            var achievements=new AchievementManager();typeof(Main).GetField("_achievements",Flags).SetValue(Main.instance,achievements);
            var first=new Achievement("SERVANT_IN_TRAINING");first.AddCondition(CustomFlagCondition.Create("Finish"));achievements.Register(first);
            string[] names={"GOOD_LITTLE_SLAVE","TROUT_MONKEY","FAST_AND_FISHIOUS","SUPREME_HELPER_MINION"};int[] targets={10,25,50,200};
            for(int i=0;i<names.Length;i++){var achievement=new Achievement(names[i]);achievement.AddCondition(CustomIntCondition.Create("Finish",targets[i]));achievements.Register(achievement);}
            NativeProcessingFlowChecks.InitializeShop();Main.npc[0].SetDefaults(NPCID.Angler);Main.npc[0].active=true;Main.npc[0].homeless=true;p.SetTalkNPC(0);
            var audit=new Harmony("JueMingR.Tests.AnglerContinuous");
            // Match the existing complete native fishing fixture's cosmetic /
            // unrelated movement/mining outlets. AnglerService remains real.
            foreach(string method in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})
                audit.Patch(typeof(AchievementsHelper).GetMethod(method,Flags),prefix:new HarmonyMethod(typeof(NativeFishingAnglerContinuousChecks),nameof(SkipUnrelatedAchievement)));
            audit.Patch(typeof(Player).GetMethod("GetAnglerReward",Flags),postfix:new HarmonyMethod(typeof(NativeFishingAnglerContinuousChecks),nameof(Reward)));
            audit.Patch(typeof(Player).GetMethod("InternalSavePlayerFile",Flags),prefix:new HarmonyMethod(typeof(NativeFishingAnglerContinuousChecks),nameof(Save)));
            audit.Patch(typeof(NetMessage).GetMethod("SendData",Flags),postfix:new HarmonyMethod(typeof(NativeFishingAnglerContinuousChecks),nameof(Sent)));
            try
            {
                Call(state,"Close");Call(ui,"Suspend");
                Reset(p,"测试009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));
                long generation=(long)Get(Get(Get(context,"Tools"),"Runtime"),"Generation");int quest=Main.anglerQuest;
                for(int i=1;i<=2;i++)
                {
                    int before=Fish(p);Main.NPCChatText_DoAnglerQuest();
                    Require(Fish(p)==before-1 && p.anglerQuestsFinished==i && rewards==i && saves==i,"each real click consumes one quest fish, one actual reward and one native player save");
                    Require(p.name=="测试0"+(9+i) && p.talkNPC==0 && Main.anglerQuest==quest && !Main.anglerQuestFinished && Main.npcChatCornerItem==Main.anglerQuestItemNetIDs[quest],"outer completion increments name once and refreshes the same quest/dialog immediately");
                    Require(Main.anglerWhoFinishedToday.SequenceEqual(i==1?new[]{"测试009"}:new[]{"测试009","测试010"}),"vanilla records each OLD name before the automatic rename");
                    Require(Player.LoadPlayer(file.Path,false).Player.name==p.name && (bool)Get(rename,"PersistedVerified") && ReferenceEquals(Main.ActivePlayerFileData,file) && ReferenceEquals(file.Player,p) && (long)Get(Get(Get(context,"Tools"),"Runtime"),"Generation")==generation,"actual native save/readback retains the same file/player/session");
                }
                Require(achievements.GetCondition("SERVANT_IN_TRAINING","Finish").IsCompleted && ((CustomIntCondition)achievements.GetCondition("GOOD_LITTLE_SLAVE","Finish")).Value==2,"actual vanilla achievement completion and task counter remain intact");
                Require(p.inventory.Any(item=>!item.IsAir && item.type!=Main.anglerQuestItemNetIDs[quest]),"real random vanilla rewards produced actual inventory content");
                Console.WriteLine("PASS continuous angler two real synchronous outer submissions: exact fish/reward/save counts, native achievements, old-name list, same dialog and quest.");
                Workload(context,host,p);
                int saved=saves,earned=rewards;string name=p.name;p.inventory[0].TurnToAir();Main.NPCChatText_DoAnglerQuest();Lang.AnglerQuestChat(false);
                Require(p.name==name && saves==saved && rewards==earned,"no-fish and view-only calls never rename or save");
                p.inventory[0].SetDefaults(Main.anglerQuestItemNetIDs[Main.anglerQuest]);p.inventory[0].stack=5;Main.anglerQuestFinished=true;Main.NPCChatText_DoAnglerQuest();
                Require(p.name==name && saves==saved && rewards==earned && Fish(p)==5,"ineligible real native click keeps fish, rewards and name");
                Main.anglerQuestFinished=false;NativeFishingUiChecks.Save(host,new FishingOptions());Main.NPCChatText_DoAnglerQuest();
                Require(p.name==name && saves==saved && rewards==earned+1 && Main.anglerQuestFinished,"disabled still permits the ordinary successful vanilla submission without rename");
                Partial(context,host,p,rename);
                Pending(context,host,p,owner,rename,ui,state);
                Network(context,host,p);
                Ui(context,host,p,ui,state);
                Storage(context,host,p);
                Console.WriteLine("PASS continuous angler: two original outer submissions, real fish/reward/achievement/save effects, old-name list, same-window quest refresh, failures/OFF, partial one-shot, actual lock/draft postponement and retirement, original 75->4 client serialization; no server acknowledgement or gameplay claim.");
            }
            finally
            {
                skipSave=throwAtEnd=false;Main.netMode=0;Main.ServerSideCharacter=false;Main.CurrentInputTextTakerOverride=null;p.SetTalkNPC(-1);Call(state,"Close");Call(ui,"Suspend");NativeFishingUiChecks.Save(host,new FishingOptions());
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
            }
        }
        private static void Reset(Player p,string name)
        {
            p.name=name;p.anglerQuestsFinished=0;p.SetTalkNPC(0);p.itemAnimation=p.itemTime=p.reuseDelay=0;
            foreach(var item in p.inventory)item.TurnToAir();Main.anglerQuest=0;Main.anglerQuestFinished=false;Main.anglerWhoFinishedToday.Clear();
            p.inventory[0].SetDefaults(Main.anglerQuestItemNetIDs[0]);p.inventory[0].stack=5;rewards=saves=0;messages.Clear();sentNames.Clear();
        }
        private static int Fish(Player p){return p.inventory.Where(item=>item.type==Main.anglerQuestItemNetIDs[Main.anglerQuest]).Sum(item=>item.stack);}
        private static void Workload(object context,object host,Player p)
        {
            object observation=Get(host,"Observation"),catalog=Get(host,"Catalog"),equipment=Get(host,"Equipment");
            long scans=(long)Get(observation,"Scans"),tiles=(long)Get(catalog,"TileReads"),sources=(long)Get(equipment,"SourceReads");
            int writes=saves;var bytes=File.ReadAllBytes(Main.ActivePlayerFileData.Path);
            for(int i=0;i<120;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)i);}
            Require(!(bool)Get(host,"NeedsSession") && !(bool)Get(Get(host,"Session"),"Active") && (long)Get(observation,"Scans")==scans && (long)Get(catalog,"TileReads")==tiles && (long)Get(equipment,"SourceReads")==sources && saves==writes && File.ReadAllBytes(Main.ActivePlayerFileData.Path).SequenceEqual(bytes),"continuous mode alone has no bobber/water/equipment discovery or idle native save");
            NativeFishingUiChecks.Save(host,new FishingOptions(storeMode:2,anglerContinuous:true));
            Require(!(bool)Call(host,"CanStore"),"existing storage admission excludes the actual open angler dialog");
            NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));
        }
        private static void Partial(object context,object host,Player p,object rename)
        {
            Reset(p,"部分009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));skipSave=true;
            try{Main.NPCChatText_DoAnglerQuest();}finally{skipSave=false;}
            int attempted=saves;
            for(int i=0;i<10;i++)Call(host,"Update",(ulong)i);
            Require(p.name=="部分010" && rewards==1 && attempted==1 && saves==attempted && !(bool)Get(rename,"PersistedVerified") && !Main.anglerQuestFinished && !(bool)Get(Get(host,"Angler"),"Pending") && GetOptional(host,"Error")!=null,"runtime-applied skipped save consumes the event once and leaves an observable failure");
            Reset(p,"异常009");throwAtEnd=true;bool propagated=false;
            try{Main.NPCChatText_DoAnglerQuest();}catch(InvalidOperationException){propagated=true;}finally{throwAtEnd=false;}
            Require(propagated && p.name=="异常009" && p.anglerQuestsFinished==1 && rewards==1 && saves==0,"outer native exception propagates and cannot be promoted to an automatic rename event");
            Reset(p,new string('名',20));Main.NPCChatText_DoAnglerQuest();
            Require(p.anglerQuestsFinished==1 && rewards==1 && saves==0 && p.name.Length==20 && Main.anglerQuestFinished,"name length refusal keeps the completed original task and never invents eligibility");
        }
        private static void Pending(object context,object host,Player p,object owner,object rename,object ui,object state)
        {
            var ioLock=typeof(Player).GetField("IOLock",Flags).GetValue(null);
            foreach(int scenario in new[]{0,1,2,3,4})using(var held=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))
            {
                Reset(p,"等待009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));
                var thread=new Thread(()=>{lock(ioLock){held.Set();release.WaitOne();}});thread.Start();Require(held.WaitOne(5000),"native autosave lock held on another thread");
                try
                {
                    Main.NPCChatText_DoAnglerQuest();Require((bool)Get(owner,"Pending") && p.name=="等待009" && rewards==1 && saves==0,"real successful native submission creates exactly one pre-save lock intent");
                    long revision=(long)Get(rename,"Revision");for(int i=0;i<10;i++)Call(host,"Update",(ulong)i);
                    Require((long)Get(rename,"Revision")==revision && saves==0,"ordinary lock waiting does not republish errors or layout revisions");
                    if(scenario==1)Call(host,"Set",10,0);
                    if(scenario==2)p.SetTalkNPC(-1);
                    if(scenario==3)Call(host,"OnSessionEnded");
                }
                finally{release.Set();thread.Join();}
                if(scenario==4)Require((bool)Call(rename,"Rename","手动接管009"),"real explicit manual rename after contention");
                Call(host,"Update",(ulong)11);
                Require(!(bool)Get(owner,"Pending") && saves==(scenario==0 || scenario==4?1:0) && p.name==(scenario==0?"等待010":scenario==4?"手动接管009":"等待009"),"unlock executes once; explicit OFF, dialog leave and session exit retire pending intent");
                Drain(host);
            }
            Reset(p,"手动009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));
            Call(state,"Navigate",7);Call(state,"RestoreVisible");Prepare(context);Click(context,Part(ui,"RenameField"));Click(context,Part(ui,"RenameField"));
            Step(context,false,Vector2.Zero);Require((bool)Get(ui,"OwnsTextToken"),"real editor input phase acquires its native token before the conflicting native call");
            var draft=Get(ui,"Editor");string text=(string)Get(draft,"Text");Main.NPCChatText_DoAnglerQuest();
            Require((bool)Get(owner,"Pending") && ReferenceEquals(Get(ui,"Editor"),draft) && (string)Get(draft,"Text")==text && p.name=="手动009","automatic completion cannot borrow the manual editor lease or consume its draft");
            Call(ui,"CancelEdit");Call(host,"Update",(ulong)12);Require(p.name=="手动010" && saves==1,"cancel releases the real text lease and permits one pending increment");
            Call(state,"Close");Call(ui,"Suspend");
        }
        private static void Network(object context,object host,Player p)
        {
            var connection=Netplay.Connection;var buffer=NetMessage.buffer[256];
            try
            {
                Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;Call(context,"UpdateRuntime");
                Reset(p,"联机009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));
                Main.NPCChatText_DoAnglerQuest();Main.NPCChatText_DoAnglerQuest();
                Require(messages.SequenceEqual(new byte[]{75,4,75,4}) && sentNames.SequenceEqual(new[]{"联机009","联机010","联机010","联机011"}) && p.name=="联机011" && rewards==2 && saves==2 && p.talkNPC==0,"each real client submission serializes vanilla75 then one player-info4, never an extra turn-in or ACK");
                Main.ServerSideCharacter=true;messages.Clear();Main.NPCChatText_DoAnglerQuest();
                Require(p.name=="联机012" && messages.SequenceEqual(new byte[]{75,4}) && !(bool)Get(Get(host,"Rename"),"PersistedVerified") && !(bool)Get(Get(host,"Angler"),"Pending"),"SSC runtime name still notifies once and consumes the native skipped-save partial result");
            }
            finally{Main.ServerSideCharacter=false;Main.netMode=0;Netplay.Connection=connection;NetMessage.buffer[256]=buffer;Call(context,"UpdateRuntime");}
        }
        private static void Storage(object context,object host,Player p)
        {
            object tools=Get(context,"Tools"),input=Get(context,"Input"),items=Get(tools,"Items");
            Terraria.UI.ItemSorting.SetupWhiteLists();
            var fixture=typeof(NativeFishingStorageChecks);
            // Reuse the existing actual native rod/water/chest setup, without
            // replaying its unrelated randomized catch-retirement scenarios.
            fixture.GetMethod("Reset",Flags).Invoke(null,new[]{context,host,tools,input});
            p.SetTalkNPC(-1);Main.anglerQuest=0;Main.anglerQuestFinished=true;int fish=Main.anglerQuestItemNetIDs[0];
            var chest=(Chest)fixture.GetMethod("Chest",Flags).Invoke(null,new object[]{fish});
            NativeFishingUiChecks.Save(host,new FishingOptions(storeMode:2,anglerContinuous:true));
            fixture.GetMethod("Cast",Flags).Invoke(null,new[]{context,input});
            Require((bool)Get(Get(host,"Session"),"Active"),"real wet native cast established existing task-fish storage session");
            long token=(long)Get(Get(host,"Session"),"Token");
            Main.npc[0].SetDefaults(NPCID.Angler);Main.npc[0].active=true;Main.npc[0].homeless=true;Main.npc[0].position=p.position+new Vector2(30,0);p.SetTalkNPC(0);
            p.name="存鱼009";Main.anglerWhoFinishedToday.Clear();Main.anglerQuestFinished=false;p.inventory[12].SetDefaults(fish);p.inventory[12].stack=3;Call(Get(items,"World"),"InvalidateObservation");
            for(int i=0;i<3;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
            Require(Fish(p)==3 && chest.item.Where(item=>item.type==fish).Sum(item=>item.stack)==1 && !(bool)Call(host,"CanStore"),"active dedicated task-fish storage cannot move the fish inside the real open angler dialog");
            int prior=rewards;Main.NPCChatText_DoAnglerQuest();
            Require(p.name=="存鱼010" && Fish(p)==2 && rewards==prior+1 && p.talkNPC==0 && (bool)Get(Get(host,"Session"),"Active") && (long)Get(Get(host,"Session"),"Token")==token,"real submission/rename preserves current fish, same dialog and same fishing session identity");
            Main.CloseNPCChatOrSign(true);Call(Get(items,"World"),"InvalidateObservation");
            for(int i=0;i<30 && Fish(p)!=0;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
            Require(Fish(p)==0 && chest.item.Where(item=>item.type==fish).Sum(item=>item.stack)==3,"closing dialog restores existing storage using the actual new-name eligibility");
            Main.chest[0]=null;NativeFishingUiChecks.Save(host,new FishingOptions());
            Console.WriteLine("PASS continuous angler with existing actual wet-bobber task-fish storage: dialog keeps stock, one native submission, same session, close resumes native chest transfer under new eligibility.");
        }
        private static void Ui(object context,object host,Player p,object ui,object state)
        {
            Reset(p,"界面009");NativeFishingUiChecks.Save(host,new FishingOptions(anglerContinuous:true));Call(state,"Close");Call(ui,"Suspend");Main.NPCChatText_DoAnglerQuest();
            Call(state,"Navigate",7);Call(state,"RestoreVisible");Prepare(context);
            Require((string)Get(Get(Part(ui,"RenameField"),"Label"),"Text")==p.name,"reopened F5 name field projects the real automatically changed name");
            var parts=((IEnumerable)Get(ui,"pageParts")).Cast<object>().ToArray();
            var title=parts.Single(part=>(string)GetOptional(Get(part,"Element"),"Text")=="渔夫任务连续提交");
            Require((string)Get(title,"Hint")=="可以不关渔夫窗口连续提交多次任务，玩家名会自动变更","actual dynamic name has exact owner hover text");
            var buttons=parts.Where(part=>Get(part,"Command").ToString()=="Feature" && (int)Get(part,"Feature")==10).ToArray();
            Require(buttons.Length==2 && buttons.Any(part=>(int)Get(part,"Value")==1 && (bool)Get(part,"Selected")) && parts.Count(part=>Get(part,"Command").ToString()=="Hotkey" && (int)Get(part,"Feature")==10)==1,"real standard ON/OFF/keyboard row follows accepted state");
            Click(context,Part(ui,"Feature",part=>(int)Get(part,"Feature")==10 && (int)Get(part,"Value")==0));Drain(host);Prepare(context);
            Require(!((FishingSettings)Get(host,"Settings")).Value.AnglerContinuous,"actual standard OFF click reliably persists");
            Click(context,Part(ui,"Feature",part=>(int)Get(part,"Feature")==10 && (int)Get(part,"Value")==1));Drain(host);Prepare(context);
            Require(((FishingSettings)Get(host,"Settings")).Value.AnglerContinuous,"actual standard ON click reliably persists");
            Click(context,Part(ui,"RenameField"));Click(context,Part(ui,"RenameField"));Call(ui,"CancelEdit");Prepare(context);
            Require(GetOptional(ui,"Editor")==null && (string)Get(Get(Part(ui,"RenameField"),"Label"),"Text")==p.name,"cancelled edit returns to current actual name");
            long layouts=(long)Get(ui,"LayoutBuilds");for(int i=0;i<30;i++)Prepare(context);Require((long)Get(ui,"LayoutBuilds")==layouts,"stable name and new standard row reuse layout");
            Call(state,"Close");Call(ui,"Suspend");
        }
    }
}
