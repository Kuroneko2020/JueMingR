using System;
using System.IO;
using JueMingR.Features.Tools;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Npcs;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class HostTools : IRuntimeFeature
    {
        internal static readonly string[] Actions={"tools.capture","tools.herbs","tools.mining"};
        internal const string SelectAction="tools.mining.select";
        internal static readonly string[] Names={"自动捕捉","自动收获","自动挖矿"};
        internal readonly ToolSettings[] Settings=new ToolSettings[3];
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly HostItems Items;
        internal readonly HostInputState Input;
        internal readonly NativeNpcObservation Npcs;
        internal readonly ToolUse Use;
        internal readonly AutoCapture Capture;
        internal readonly HerbHarvest Herbs;
        internal readonly AutoMining Mining;
        internal readonly FishingBorrow Fishing;
        private readonly Func<Item,bool> priorProtection;
        internal Func<bool> CanInterface;
        internal Feedback.LocalShortFeedback Feedback;
        private bool report;
        private int roundRobin;
        private ulong unknown;
        private Player identityPlayer;
        private object identityWorld,identitySocket;
        private int identityMode;
        internal long SelectionIntent {get;private set;}
        internal long ManualSelectionFrame=-1,NextUseFrame;
        internal ulong Tick {get;private set;}
        internal bool Available {get;private set;}
        internal Exception SetupError {get;private set;}
        internal string Error {get;private set;}
        internal Player Player {get{return Runtime.IsSessionActive?Items.World.Player:null;}}
        public bool Enabled {get{return Mode(0)!=0 || Mode(1)!=0 || Mode(2)!=0 || Use.Active || Fishing.Active || unknown!=0;}}
        internal HostTools(string directory,SingleFeatureRuntime runtime,HostItems items,HostInputState input,NativeNpcObservation npcs)
        {
            Runtime=runtime;Items=items;Input=input;Npcs=npcs;
            string[] files={"auto-capture.json","herb-harvest.json","auto-mining.json"};
            for(int i=0;i<3;i++)Settings[i]=new ToolSettings(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features",files[i]),65536),i);
            Use=new ToolUse(this);Capture=new AutoCapture(this);Herbs=new HerbHarvest(this);Mining=new AutoMining(this);Fishing=new FishingBorrow(this);
            priorProtection=items.World.AdditionalProtection;
            items.World.AdditionalProtection=item=>(priorProtection?.Invoke(item)??false) || Use.ProtectOriginal(item) || Fishing.ProtectRod(item) || Herbs.ProtectSeed(item);
            items.AllowsOwnedTools=Use.Owns;
            try{ToolHooks.Install(this);Available=items.Available;}
            catch(Exception e){SetupError=e;ToolHooks.Uninstall();Report("自动采集暂不可用，原版操作不受影响。");}
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal int Mode(int i){return Available && Settings[i].Ready?Settings[i].Value.Mode:0;}
        // Pending persistence revokes execution, not a retained enabled
        // region. Explicit off or a failed/unknown save does retire intent.
        internal bool KeepsIntent(int i){var s=Settings[i];return Available && s.Loaded && !s.Protected && s.Message==null && s.Value.Mode!=0 && (!s.Busy || s.RequestedMode!=0);}
        internal bool Controls(int i){return Available && Player!=null && Settings[i].Loaded && !Settings[i].Busy && !Settings[i].Protected;}
        internal void Set(int i,int mode){if(Controls(i))Settings[i].Set(Settings[i].Value.WithMode(mode));}
        internal static string ModeName(int feature,int mode){return mode==0?"关闭":feature==0?(mode==1?"自动":"手持"):feature==2?(mode==1?"快捷键":"自动"):"开启";}
        internal void Poll()
        {
            foreach(var s in Settings)s.Poll();Input.UseGesture.ToolsEnabled=Available && Mode(2)!=0;
            if(Use.Active && ModeFor(Use.Intent.Kind)==0)Use.Cancel();
            if(Mode(0)!=1)Fishing.Cancel();if(!KeepsIntent(1))Herbs.Reset();if(!KeepsIntent(2))Mining.Clear();
        }
        private int ModeFor(ToolKind kind){return Mode(kind==ToolKind.Capture || kind==ToolKind.Recast?0:kind==ToolKind.Harvest || kind==ToolKind.Seed?1:2);}
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            for(int i=0;i<3;i++)
            {
                int domain=i;var s=Settings[i];Action command=()=>{if(Controls(domain))s.Set(s.Value.Toggle());};
                if(feedback!=null)command=feedback.Committed(Actions[i],Names[i],command,()=>Mode(domain),()=>Controls(domain),()=>s.AcceptedCommandId,()=>s.CompletedCommandId,()=>s.CompletionSucceeded,m=>ModeName(domain,m));
                registry.Register(new HotkeyAction(Actions[i],Names[i],HotkeyContext.Gameplay,()=>Controls(domain),command));
            }
            registry.Register(new HotkeyAction(SelectAction,"选择挖矿区域",HotkeyContext.Gameplay,()=>Controls(2) && Mode(2)!=0,Select));
        }
        private void Select(HotkeyChord chord)
        {
            Input.ClaimUseGesture(chord);var p=Player;if(!Admit(p,false))return;
            var point=Main.MouseWorld;int x=(int)(point.X/16),y=(int)(point.Y/16);var tile=World.WorldTileObservation.ReadCurrent(x,y);
            bool ok=tile.Readable && tile.Active && Mining.Select(p,x,y,tile.Type,false);
            string message=ok?(Mining.Region.Truncated?"已选中 512 格，超出部分未加入":"已选中 "+Mining.Region.Count+" 格挖矿区域"):"光标处没有可选矿物，保留原区域";
            Feedback?.Show(SelectAction,message,ok,()=>Runtime.IsSessionActive,Feedback.Capture());
        }
        internal bool Admit(Player p,bool heldInventory)
        {
            return Available && p!=null && ReferenceEquals(p,Player) && Input.CanStartActions && CanInterface!=null && CanInterface() &&
                !Main.gamePaused && !p.dead && !p.CCed && !p.cursed && !p.noItems && !p.isOperatingAnotherEntity && !p.HasLockedInventory() &&
                !Items.World.Busy && !Items.World.HasManualOperation && !Main.mapFullscreen && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride==null &&
                !Main.ServerSideCharacter && (Main.ActivePlayerFileData==null || !Main.ActivePlayerFileData.ServerSideCharacter) && !WorldGen.isGeneratingOrLoadingWorld &&
                !PlayerInput.UsingGamepadUI && p.chest==-1 && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 && !p.mouseInterface && Main.mouseItem!=null && Main.mouseItem.IsAir &&
                (!Main.playerInventory || heldInventory && p.selectedItem>=0 && p.selectedItem<10) && !PlayerInput.Triggers.Current.MouseRight && !PlayerInput.Triggers.Current.SmartSelect;
        }
        internal bool Candidate(Player p,int i,bool seed=false)
        {return i>=0 && i<50 && p.inventory[i]!=null && !p.inventory[i].IsAir && !Items.Ownership.IsProtected(i) && !p.inventoryChestStack[i] && !Items.World.ManualMaterials.Contains(p.inventory[i]) && !(priorProtection?.Invoke(p.inventory[i])??false) && (seed || !Herbs.ProtectSeed(p.inventory[i]));}
        internal ToolIntent Choose(Player p)
        {
            if(!Enabled || Use.Active || PlayerInput.Triggers.Current.MouseLeft || p.selectedItemState.HasBufferedChange)return null;
            var restore=Fishing.Choose(p);if(restore!=null)return restore;
            for(int i=0;i<3;i++)
            {
                int domain=(roundRobin+i)%3;ToolIntent result=domain==0?Capture.Choose(p):domain==1?Herbs.Choose(p):Mining.Choose(p);
                if(result==null)continue;roundRobin=(domain+1)%3;return result;
            }
            return null;
        }
        internal void ManualSelection(){SelectionIntent++;ManualSelectionFrame=Input.Frame;Fishing.Cancel();Use.Cancel();}
        internal void Yield(){Use.Cancel();if(Player!=null && Player.selectedItemState.CanChangeSelectedItemImmediately)Use.Retire();NextUseFrame=Input.Frame+3;}
        internal void HoldUnknown(int slot){unknown|=1UL<<slot;Items.Ownership.HoldInterruptedSource(Runtime.Generation,unknown);}
        public void OnSessionStarted()
        {
            object socket=Main.netMode==1?Netplay.Connection.Socket:null;
            bool fresh=!ReferenceEquals(identityPlayer,Main.LocalPlayer) || !ReferenceEquals(identityWorld,Main.ActiveWorldFileData) || !ReferenceEquals(identitySocket,socket) || identityMode!=Main.netMode;
            identityPlayer=Main.LocalPlayer;identityWorld=Main.ActiveWorldFileData;identitySocket=socket;identityMode=Main.netMode;
            Use.Retire();if(fresh){unknown=0;Capture.ClearUnknown();Herbs.ClearUnknown();Error=null;report=false;}Herbs.Reset();Mining.Clear();Fishing.Reset();SelectionIntent++;NextUseFrame=Input.Frame+1;
        }
        public void OnSessionEnded(){Use.Retire();Herbs.Reset();Mining.Clear();Fishing.Reset();SelectionIntent++;}
        public void Update(ulong tick)
        {
            Tick=tick;if(unknown!=0)Items.Ownership.HoldInterruptedSource(Runtime.Generation,unknown);
            Use.Update();Fishing.Update();Herbs.Update();Mining.Update();
        }
        public void FailClosed(){Available=false;Use.Cancel();Fishing.Cancel();Herbs.Reset();Mining.Clear();Report("自动采集已停止，未确认的操作不会重试。");}
        internal void Report(string message){if(Error==message)return;Error=message;report=true;}
        internal void TakeFeedback(Action<string> display){foreach(var s in Settings)s.TakeFeedback(display);if(report){report=false;display(Error);}}
        private void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;foreach(var s in Settings)s.Stop(750);}
    }
}
