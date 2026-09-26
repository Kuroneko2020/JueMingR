using System;
using System.IO;
using JueMingR.Features.Processing;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Processing
{
    internal sealed class HostProcessing : IRuntimeFeature
    {
        internal static readonly string[] Actions={"processing.bags","processing.extraction","processing.reforge"};
        internal static readonly string[] Names={"持续开袋","自动提炼","自动重铸"};
        internal static readonly string[] Help={"按住shift长按右键点击匣子快速打开","靠近提炼机尝试自动提炼","按住重铸键直到名单的词缀停下"};
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly HostItems Items;
        internal readonly HostInputState Input;
        internal readonly ProcessingSettings[] Settings=new ProcessingSettings[3];
        internal readonly ContinuousBags Bags;
        internal readonly AutomaticExtraction Extraction;
        internal readonly AutomaticReforge Reforge;
        internal Func<bool> CanInterface;
        internal Func<bool> BankGuardsReady;
        private Player identityPlayer;
        private object identityWorld,identitySocket;
        private int identityMode;
        internal bool Available {get;private set;}
        internal Exception SetupError {get;private set;}
        internal string Error {get;private set;}
        private bool feedback;
        internal Player Player {get{return Runtime.IsSessionActive?Items.World.Player:null;}}
        public bool Enabled {get{return Value(0) || Value(1) || Value(2);}}
        internal HostProcessing(string directory,SingleFeatureRuntime runtime,HostItems items,HostInputState input)
        {
            Runtime=runtime;Items=items;Input=input;
            var files=new[]{"continuous-bags.json","extraction.json","reforge.json"};
            for(int i=0;i<3;i++)Settings[i]=new ProcessingSettings(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features",files[i]),65536),i);
            Bags=new ContinuousBags(this);
            Extraction=new AutomaticExtraction(this);
            Reforge=new AutomaticReforge(this);
            try{ProcessingHooks.Install(this);ReforgeHooks.Install(this);Available=items.Available;}
            catch(Exception e){SetupError=e;ProcessingHooks.Uninstall();ReforgeHooks.Uninstall();Report("连续加工暂不可用，原版操作不受影响。");}
            Items.ControlledBag=Bags.Controls;
            Items.ObservesProcessing=()=>Bags.Executing;
            Items.InterruptedProcessingSource=Bags.RetainInterruptedSource;
            Items.AllowsOwnedProcessing=(array,slot)=>Bags.Owns(array,slot) || Extraction.Owns(array,slot);
            Items.AllowsProcessingPayment=()=>Reforge.Executing;
            Items.World.ProcessingProtection=Bags.ProtectIntent;
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal bool Controls(int feature){var s=Settings[feature];return Available && Player!=null && s.Loaded && !s.Busy && !s.Protected;}
        internal bool Value(int feature){return Available && Settings[feature].Ready && Settings[feature].Value.Enabled;}
        internal void Set(int feature,bool value){if(Controls(feature))Settings[feature].Set(new ProcessingOptions(value,Settings[feature].Value.Names));}
        internal void Poll(){foreach(var s in Settings)s.Poll();}
        internal void Register(JueMingR.Platform.Hotkeys.HotkeyRegistry registry)
        {
            for(int i=0;i<3;i++){int feature=i;registry.Register(new JueMingR.Platform.Hotkeys.HotkeyAction(Actions[i],Names[i],JueMingR.Platform.Hotkeys.HotkeyContext.Gameplay,()=>Controls(feature),()=>Set(feature,!Settings[feature].Value.Enabled)));}
        }
        internal bool Admit(Player p)
        {
            return Available && p!=null && ReferenceEquals(p,Player) && Input.CanStartActions && CanInterface!=null && CanInterface() &&
                !Main.gamePaused && !p.dead && !p.CCed && !p.cursed && !p.noItems && !p.isOperatingAnotherEntity && !p.HasLockedInventory() &&
                !Items.World.Busy && !Main.mapFullscreen && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText &&
                Main.CurrentInputTextTakerOverride==null && !Main.ServerSideCharacter && (Main.ActivePlayerFileData==null || !Main.ActivePlayerFileData.ServerSideCharacter) &&
                !WorldGen.isGeneratingOrLoadingWorld && !PlayerInput.UsingGamepadUI;
        }
        public void OnSessionStarted()
        {
            object socket=Main.netMode==1?Netplay.Connection.Socket:null;
            bool fresh=!ReferenceEquals(identityPlayer,Main.LocalPlayer) || !ReferenceEquals(identityWorld,Main.ActiveWorldFileData) || !ReferenceEquals(identitySocket,socket) || identityMode!=Main.netMode;
            identityPlayer=Main.LocalPlayer;identityWorld=Main.ActiveWorldFileData;identitySocket=socket;identityMode=Main.netMode;
            Bags.StartSession(fresh);
            Extraction.Reset(fresh);
            Reforge.Reset(fresh);
        }
        public void OnSessionEnded(){Bags.EndSession();Extraction.Reset(false);Reforge.Reset(false);}
        public void Update(ulong tick){Bags.RestoreUnknown();Items.World.RefreshManualRelease();Extraction.Update();Reforge.Update();if(Value(0))Bags.Update();else Bags.ReleaseIntent();}
        public void FailClosed(){Available=false;Bags.ReleaseIntent();Extraction.Cancel();Report("连续加工已停止；结果未确认的操作不会重试。");}
        internal void Report(string message){if(Error==message)return;Error=message;feedback=true;}
        internal void TakeFeedback(Action<string> display){foreach(var s in Settings)s.TakeFeedback(display);if(feedback){feedback=false;display(Error);}}
        internal void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;foreach(var s in Settings)s.Stop(750);}
    }
}
