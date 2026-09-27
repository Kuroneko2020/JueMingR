using System;
using System.IO;
using JueMingR.Features.Fishing;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed class HostFishing : IRuntimeFeature
    {
        internal static readonly string[] Names={"自动钓鱼","自动换装","自动配装","自动存放鱼","切杆跳过","钓鱼过滤"};
        internal static readonly string[] Actions={"fishing.auto-fish.toggle","fishing.auto-loadout.toggle","fishing.auto-equipment.toggle","fishing.auto-store.toggle","fishing.cut-rod.toggle","fishing.filter.toggle"};
        internal readonly HostTools Tools;
        internal readonly FishingSettings Settings;
        internal readonly FishingObservation Observation=new FishingObservation();
        internal readonly FishingCatalog Catalog=new FishingCatalog();
        internal FishingDisplay Display {get;private set;}
        internal void AttachInformation(Information.HostInformation information){Display=new FishingDisplay(this,information);}
        internal readonly FishingSession Session;
        internal readonly PlayerRename Rename;
        internal readonly FishingEquipment Equipment;
        internal Func<bool> CanEquipmentInterface;
        internal bool Available {get;private set;}
        internal Exception SetupError {get;private set;}
        internal string Error {get;private set;}
        private bool feedback;
        private string endedNotice;
        internal Player Player {get{return Tools.Player;}}
        internal bool Ready {get{return Available && Settings.Ready;}}
        internal bool Controls {get{return Available && Player!=null && Settings.Loaded && !Settings.Busy && !Settings.Protected;}}
        internal bool NeedsSession {get{var v=Settings.Value;return Ready && (v.Auto || v.Loadout || v.Equipment || v.StoreMode!=0);}}
        internal bool KeepsSession {get{return Available && Settings.Loaded && !Settings.Protected && Settings.Message==null && WantsSession(Settings.Value) && (!Settings.Busy || WantsSession(Settings.Requested));}}
        private static bool WantsSession(FishingOptions value){return value!=null && (value.Auto || value.Loadout || value.Equipment || value.StoreMode!=0);}
        public bool Enabled {get{return NeedsSession || Session.Active || Equipment.Active || (Display?.Enabled??false);}}
        internal HostFishing(string directory,HostTools tools,KeepFavorited.HostKeepFavorited favorites)
        {
            Tools=tools;Settings=new FishingSettings(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features","fishing.json"),FishingCodec.MaximumBytes));
            Session=new FishingSession(this);
            Rename=new PlayerRename(this);
            tools.Items.UpdateFishingStorage=SyncStorage;
            tools.Items.FishingClock=()=>Tools.Tick;
            tools.FishingEnabled=()=>NeedsSession || Session.Active;tools.FishingChoice=Session.Choose;tools.FishingStarted=Session.ObserveUse;tools.FishingProjectile=Session.ObserveCreated;tools.FishingManualSelection=Session.ManualSelection;
            var prior=tools.Items.World.AdditionalProtection;Equipment=new FishingEquipment(this,favorites,prior);
            tools.Items.World.AdditionalProtection=item=>(prior?.Invoke(item)??false) || Session.Protect(item) || Equipment.Protect(item);
            try{FishingHooks.Install(this);FishingEquipmentHooks.Install(Equipment);Available=tools.Available;}
            catch(Exception error){SetupError=error;FishingHooks.Uninstall();FishingEquipmentHooks.Uninstall();Report("钓鱼辅助暂不可用，原版操作不受影响。");}
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal void Poll(){Settings.Poll();}
        internal void SyncStorage()
        {
            bool retained=Session.StorageSession;
            int mode=retained?Settings.Value.StoreMode:0;
            if(Settings.Busy && Settings.Requested.StoreMode!=mode)mode=0;
            int quest=mode==2 && !Main.anglerQuestFinished && Main.anglerQuest>=0 && Main.anglerQuest<Main.anglerQuestItemNetIDs.Length?Main.anglerQuestItemNetIDs[Main.anglerQuest]:0;
            Tools.Items.Feature.SetFishingStorage(Tools.Runtime.Generation,retained?Session.Token:0,mode,quest,Ready && CanStore());
        }
        private bool CanStore()
        {
            var p=Player;
            // Storage has its own native container admission. Ordinary inventory
            // or an unrelated open chest must not inherit tool-use restrictions.
            return p!=null && Tools.Input.CanStartActions && (Tools.CanInterface?.Invoke()??false) && !Tools.Fishing.Active && !Main.gamePaused && !p.dead && !p.CCed && !p.cursed && !p.noItems &&
                !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride==null &&
                !Main.ServerSideCharacter && !(Main.ActivePlayerFileData?.ServerSideCharacter??false) && !WorldGen.isGeneratingOrLoadingWorld && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 && Main.mouseItem!=null && Main.mouseItem.IsAir;
        }
        internal void Set(int feature,int value){if(Controls && Settings.Value.State(feature)!=value)Settings.Set(Settings.Value.Change(feature,value));}
        internal static string ModeName(int feature,int value)
        {return value==0?(feature==5?"关闭过滤":"关闭"):feature==3?(value==1?"所有":"任务鱼"):feature==5?(value==1?"白名单":"黑名单"):"开启";}
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            // The owner chose an on-page cycle for the filter mode, without a
            // public key binding. The five automation actions remain bindable.
            for(int i=0;i<5;i++)
            {
                int feature=i;Action command=()=>{if(Controls)Settings.Set(Settings.Value.Toggle(feature));};
                if(feedback!=null)command=feedback.Committed(Actions[i],Names[i],command,()=>Settings.Value.State(feature),()=>Controls,()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded,value=>ModeName(feature,value));
                registry.Register(new HotkeyAction(Actions[i],Names[i],HotkeyContext.Gameplay,()=>Controls,command));
            }
        }
        internal void Report(string message){if(Error==message)return;Error=message;feedback=true;}
        internal void ClearReport(string message){if(message!=null && Error==message){Error=null;feedback=false;}}
        internal void TakeFeedback(Action<string> display)
        {
            Settings.TakeFeedback(display);
            if(endedNotice!=null){string notice=endedNotice;endedNotice=null;display(notice);if(Error==notice)feedback=false;}
            if(feedback){feedback=false;display(Error);}
        }
        public void OnSessionStarted(){Session.Reset();Observation.Clear();Catalog.Clear();Display?.Clear();Rename.Clear();Equipment.StartSession();Error=endedNotice;feedback=false;}
        public void OnSessionEnded()
        {
            // SaveAndQuit enters gameMenu before UI feedback can run. Retain
            // only this outcome text until the next presentable session; never
            // retain item objects or an old character's restoration journal.
            bool pending=Equipment.Active;Equipment.EndSession();
            if(pending){endedNotice="上一会话的部分钓鱼装备未归还，已保留在角色实际物品格中；请手动检查。";Error=endedNotice;}
            Session.Reset();Observation.Clear();Catalog.Clear();Display?.Clear();Rename.Clear();
        }
        public void Update(ulong tick)
        {if(!Available){Equipment.Update();return;}if(!Enabled){Observation.Clear();Display?.Clear();return;}var p=Player;if(p==null){Session.Reset();Equipment.Update();Observation.Clear();Display?.Clear();return;}Observation.Read(p);Session.Update();Equipment.Update();Display?.Update(p);}
        public void FailClosed(){Available=false;Session.Stop();Equipment.Fail("钓鱼辅助已停止，仍需归还的装备保留实际记录。");Observation.Clear();Display?.Clear();Report("钓鱼辅助已停止，未确认的操作不会重试。");}
        private void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Settings.Stop(1000);Rename.Dispose();Display?.Dispose();}
    }
}
