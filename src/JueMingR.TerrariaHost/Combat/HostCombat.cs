using System;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class HostCombat : IRuntimeFeature,F5.ICombatControls
    {
        internal static readonly string[] Names=F5.CombatControls.Names;
        internal static readonly string[] Actions=F5.CombatControls.Actions;
        internal readonly CombatSettings Settings;
        internal readonly HostTools Tools;
        internal readonly CombatUse Use;
        internal readonly CombatAim Aim=new CombatAim();
        internal readonly CombatFacing Facing;
        internal readonly GoblinHitHooks Goblin;
        internal readonly CombatProjectileReceipts Receipts;
        internal readonly CombatReports Reports;
        internal readonly CombatHandoff Handoff;
        internal Func<bool> CanInterface;
        internal bool Left {get;private set;}
        internal bool Right {get;private set;}
        private long sampledFrame=-1;
        internal bool Available {get;private set;}
        internal Exception SetupError {get;private set;}
        internal Player Player {get{return Tools.Player;}}
        internal SingleFeatureRuntime Runtime {get{return Tools.Runtime;}}
        internal bool FreshGesture {get{return sampledFrame==Tools.Input.Frame && Tools.Input.CanStartActions;}}
        internal HostCombat(string directory,HostTools tools)
        {
            Tools=tools;Settings=new CombatSettings(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features","combat.json"),65536));
            Use=new CombatUse(this);
            Handoff=new CombatHandoff(this);
            Facing=new CombatFacing(this);
            Goblin=new GoblinHitHooks(this);
            Receipts=new CombatProjectileReceipts(this);
            Reports=new CombatReports(this);
            tools.Combat=this;
            tools.Items.AllowsOwnedCombat=Use.Owns;
            Available=tools.Available && tools.Items.Available;
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal bool IsEnabled(int feature){return Available && Settings.CanRun && Settings.Value.Enabled(feature) && (feature!=7 || Goblin.Ready) && (feature!=1 || Receipts.Ready) && (feature!=6 || Reports.Ready);}
        internal bool Controls {get{return Available && Player!=null && Settings.Ready;}}
        public bool CanConfigure(int feature){return Controls && (feature!=7 || Goblin.Ready) && (feature!=1 || Receipts.Ready) && (feature!=6 || Reports.Ready);}
        bool F5.ICombatControls.IsEnabled(int feature){return IsEnabled(feature);}
        void F5.ICombatControls.Set(int feature,bool enabled){Set(feature,enabled);}
        int F5.ICombatControls.SwitchInterval {get{return Settings.Value.SwitchInterval;}}
        void F5.ICombatControls.Interval(int value){Interval(value);}
        public string Unavailable(int feature){return CanConfigure(feature)?null:Settings.Message??"当前暂不可用。";}
        public bool Enabled {get{return Available && (Settings.CanRun && Settings.Value.EnabledMask!=0 || Use.Active);}}
        internal void Set(int feature,bool enabled)
        {if(CanConfigure(feature) && Settings.Value.Enabled(feature)!=enabled)Settings.Set(Settings.Value.Toggle(feature));}
        internal void Interval(int value){if(Controls)Settings.Set(Settings.Value.WithInterval(value));}
        internal void Poll(){Settings.Poll();if(!Settings.CanRun || Use.Active && !Use.FeatureEnabled){Use.Stop();Handoff.Revoke();}Reports.Poll();if(!IsEnabled(5))Facing.Reset();}
        // Sample only after every shared UI owner consumed this native sample,
        // and before any automatic owner writes player controls. Raw MouseInfo
        // would resurrect a consumed browser/F5 click here.
        internal void Sample()
        {
            sampledFrame=Tools.Input.Frame;
            Left=Tools.Input.CanStartActions && PlayerInput.Triggers.Current.MouseLeft;
            Right=Tools.Input.CanStartActions && PlayerInput.Triggers.Current.MouseRight;
            Handoff.Sample();
            if(!Tools.Input.CanRetainIntent || !Left && !Right)Use.Stop();
        }
        internal bool Admitted(Player player,bool cursor=false)
        {
            return Available && ReferenceEquals(player,Player) && Runtime.IsSessionActive && FreshGesture && (CanInterface?.Invoke()??false) &&
                !Main.gamePaused && Main.CanUpdateGameplay && !player.dead && !player.ghost && !player.CCed && !player.cursed && !player.noItems &&
                !player.HasLockedInventory() && !player.isControlledByFilm && !Tools.Items.World.Busy && !Tools.Items.World.HasManualOperation &&
                !Main.mapFullscreen && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText &&
                player.chest==-1 && player.talkNPC<0 && player.sign<0 && Main.npcShop==0 &&
                (cursor || Main.mouseItem!=null && Main.mouseItem.IsAir);
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            for(int i=0;i<8;i++)
            {
                int feature=i;Action command=()=>{if(CanConfigure(feature))Settings.Set(Settings.Value.Toggle(feature));};
                if(feedback!=null)command=feedback.Committed(Actions[i],Names[i],command,()=>IsEnabled(feature)?1:0,()=>CanConfigure(feature),
                    ()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded);
                registry.Register(new HotkeyAction(Actions[i],Names[i],HotkeyContext.Gameplay,()=>CanConfigure(feature),command));
            }
        }
        public void OnSessionStarted(){Use.Stop();Handoff.Reset();Facing.Reset();Reports.Reset();sampledFrame=-1;}
        public void OnSessionEnded(){JueMingR.TerrariaHost.Tools.ToolHooks.EndCombatFrame();Use.Stop();Handoff.Reset();Facing.Reset();Reports.Reset();sampledFrame=-1;Left=Right=false;}
        public void Update(ulong tick){JueMingR.TerrariaHost.Tools.ToolHooks.EndCombatFrame();Use.FinishFrame();Handoff.FinishFrame();if(!Tools.Input.CanRetainIntent || !Settings.CanRun)Use.Stop();}
        public void FailClosed(){JueMingR.TerrariaHost.Tools.ToolHooks.EndCombatFrame();Available=false;Use.Stop();Handoff.Reset();Facing.Reset();Reports.Reset();}
        private void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Settings.Stop(750);Goblin.Dispose();Receipts.Dispose();Reports.Dispose();}
    }
}
