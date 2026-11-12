using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.AfkMode;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Combat;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.AfkMode
{
    // A run is a retained user intention, NOT a retained use lease. Manual
    // input and finite borrowers can own the native outlet while it survives.
    internal sealed partial class HostAfkMode : IRuntimeFeature
    {
        internal const string ActionId="afk.toggle";
        internal readonly HostTools Tools;
        private readonly PreferenceDocument<AfkPreferences> preference;
        private readonly Stopwatch startup=Stopwatch.StartNew();
        private bool failed,stopping,manual,yieldRequested,quickRequested,everStarted,automaticRequested,automaticBorrowed;
        private long sampled=-1,session,token;
        private Player player;
        private Item source;
        private int slot,type;
        private Vector2 point;
        private Scope syncScope;
        private QuickItems.HostQuickItems quick;
        private Processing.HostProcessing processing;
        private bool initialHold;
        internal Func<bool> OperationInterfaceOpen;
        internal Exception SetupError {get;private set;}
        internal bool Running {get{return player!=null;}}
        internal bool ControlsUse {get{return Running && !manual && !quickRequested && !Borrowed;}}
        public bool ControlsEnabled {get{return !failed && !stopping && Tools.Available && Tools.Items.Available && Tools.Player!=null && preference.Snapshot.IsLoaded;}}
        public bool ModeEnabled {get{return preference.Snapshot.Value.Enabled;}}
        public int GapMilliseconds {get{return preference.Snapshot.Value.GapMilliseconds;}}
        public string StatusMessage
        {
            get
            {
                if(failed || !Tools.Available)return "挂机模式暂不可用，原版使用方式不受影响。";
                var state=preference.Snapshot;
                if(!state.IsLoaded)return "正在读取挂机设置。";
                if(state.IsProtected || state.Status==PreferenceStatus.IoFailure)return "挂机设置未能保存，本次选择仍然有效。";
                return null;
            }
        }
        public bool Enabled {get{return ModeEnabled || Running;}}
        internal HostAfkMode(string directory,HostTools tools)
        {
            Tools=tools;tools.Afk=this;
            preference=new PreferenceDocument<AfkPreferences>(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features","afk-mode.json"),65536,true),new AfkPreferenceCodec(),new AfkPreferences(false,0));
            // Reuse the exact debit permission at the existing inventory guard;
            // it is granted only inside this owner's native ItemCheck scope.
            var prior=tools.Items.AllowsOwnedCombat;
            tools.Items.AllowsOwnedCombat=(items,index)=>(prior?.Invoke(items,index)??false) || Owns(items,index);
            try{AfkNativeHooks.Install(this);}catch(Exception error){SetupError=error;failed=true;}
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        public bool SetModeEnabled(bool value)
        {
            if(!ControlsEnabled)return false;
            bool changed=preference.Set(new AfkPreferences(value,GapMilliseconds));
            if(!value)Stop();
            return changed;
        }
        public bool SetGapMilliseconds(int value)
        {return value>=0 && ControlsEnabled && preference.Set(new AfkPreferences(ModeEnabled,value));}
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            Action toggle=()=>SetModeEnabled(!ModeEnabled);
            if(feedback!=null)toggle=feedback.Immediate(ActionId,"挂机模式",toggle,()=>ModeEnabled?1:0,()=>ControlsEnabled);
            registry.Register(new HotkeyAction(ActionId,"挂机模式",HotkeyContext.Gameplay,()=>ControlsEnabled,toggle));
        }
        internal void Attach(QuickItems.HostQuickItems value,Processing.HostProcessing process=null)
        {
            processing=process;quick=value;if(quick==null)return;
            quick.AfkYield=PrepareQuickBorrow;
            quick.AfkBorrowFinished=()=>{quickRequested=false;if(!operating)yieldRequested=false;};
        }
        internal void Poll()
        {
            if(!preference.Snapshot.IsLoaded && startup.ElapsedMilliseconds>=2000)preference.AbandonSlowLoad();
            if(!ModeEnabled)Stop();
        }
        private bool Identity()
        {
            if(!Running || session!=Tools.Runtime.Generation || !ReferenceEquals(player,Tools.Player) || slot<0 || slot>=player.inventory.Length)return false;
            Item item=player.inventory[slot];
            return item!=null && item.type==type && item.stack>0 && (slot==58 || ReferenceEquals(item,source));
        }
        private bool Unusable(Player p)
        {
            return p==null || !Tools.Runtime.IsSessionActive || Main.gameMenu || Main.dedServ || Main.netMode==2 ||
                p.dead || p.ghost || p.CCed || p.cursed || p.noItems || p.HasLockedInventory() || p.isControlledByFilm || p.isOperatingAnotherEntity ||
                Main.mapFullscreen || Main.blockInput || Main.drawingPlayerChat || Main.editSign || Main.editChest || PlayerInput.WritingText ||
                p.chest>=0 || p.talkNPC>=0 || p.sign>=0 || Main.npcShop!=0 || (OperationInterfaceOpen?.Invoke()??true);
        }
        private bool Borrowed {get{return Running && (player.selectedItem!=slot || slot!=58 && player.selectedItemState.HasActiveOverride || Tools.Use.Active || (quick?.Use.Active??false) || (processing?.Extraction.Active??false) || Tools.Items.World.HasManualOperation);}}
        // Called only after all world editors and UI owners consumed this
        // physical sample. Synthesized use bits are never a start edge.
        internal void Sample()
        {
            sampled=Tools.Input.Frame;var p=Tools.Player;
            if(!ModeEnabled || failed || Unusable(p) || !Tools.Input.CanRetainIntent){Stop();return;}
            if(Running && !Identity()){Stop();return;}
            bool fresh=Tools.Input.CanStartActions && Tools.Input.Hotkeys.IsNew(256) && PlayerInput.Triggers.Current.MouseLeft;
            if(!Running)
            {
                if(!fresh || CombatUse.NativeHoldBlocked(p) || !(Tools.CanMouseInterface?.Invoke()??false) || Tools.Items.Ownership.HasUse || p.selectedItemState.HasActiveOverride && p.selectedItem!=58 || p.selectedItemState.HasBufferedChange)return;
                Item item=p.HeldItem;int selected=p.selectedItem;
                if(item==null || item.IsAir || item.useStyle<=0 || item.useAnimation<=0 || selected<0 || selected>=50 && selected!=58)return;
                player=p;slot=selected;source=item;type=item.type;session=Tools.Runtime.Generation;point=PhysicalWorldPoint(p);
                initialHold=true;manual=yieldRequested=quickRequested=false;ResetOperation();
                return;
            }
            if(initialHold && !Tools.Input.RawMouseLeft)initialHold=false;
            if(fresh && !initialHold || Tools.Input.CanStartActions && PlayerInput.Triggers.Current.MouseRight)
            {manual=true;ReleaseLease();}
            if(Borrowed){manual=true;ReleaseLease();}
            // A same-slot hand use releases its own native controls and timers.
            // Neither an override nor a foreign use token is forcibly returned.
            if(manual && !Tools.Input.RawMouseLeft && !Tools.Input.RawMouseRight && !Borrowed && WeaponCatalog.Ready(player) && !player.channel)
                manual=false;
        }
        private bool CanOutput(Player p)
        {
            if(!Running || !ReferenceEquals(p,player))return false;
            if(!ModeEnabled || !Identity() || Unusable(p)){Stop();return false;}
            if(CombatUse.NativeHoldBlocked(p)){if(!everStarted)Stop();return false;}
            return sampled==Tools.Input.Frame && Tools.Input.CanRunAutomaticActions && !Main.gamePaused && Main.CanUpdateGameplay && !manual && !Borrowed && (Tools.CanMouseInterface?.Invoke()??false);
        }
        private Vector2 PhysicalWorldPoint(Player p)
        {
            // HandleInput has not yet applied SetZoom_World. Read the same
            // original mapped sample and native zoom conversion used by the
            // existing combat mouse observer, rather than capturing UI pixels.
            float zoom=Main.GameViewMatrix.RenderZoom.X;
            var center=PlayerInput.OriginalScreenSize*.5f;
            float x=center.X+(Tools.Input.PhysicalMapX-center.X)/zoom;
            float y=center.Y+(Tools.Input.PhysicalMapY-center.Y)/zoom;
            return Main.screenPosition+new Vector2(x,p.gravDir<0?Main.screenHeight-y:y);
        }
        private bool Acquire()
        {
            if(token!=0)return Tools.Items.Ownership.OwnsUse(session,token);
            if(Tools.Items.Ownership.HasUse)return false;
            long next=Tools.Items.Ownership.NewUseToken();
            if(!(slot==58?Tools.Items.Ownership.TryBeginCursorUse(session,next):Tools.Items.Ownership.TryBeginUse(session,slot,next)))return false;
            token=next;return true;
        }
        private void ReleaseLease(){if(token!=0)Tools.Items.Ownership.EndUse(session,token);token=0;}
        internal void BeforeSync(Player p)
        {
            FinishSync();ObserveCompletion(p);
            if(!CanOutput(p))return;
            if((quickRequested || automaticRequested) && !operating){ReleaseLease();return;}
            if(!operating && !GapReady){ReleaseLease();return;}
            if(!Acquire())return;
            // Sync carries held controls, not a cursor lease across native
            // tile/NPC interactions. ItemCheck and controller AI borrow their
            // own point only while that exact consumer executes.
            syncScope=BeginScope(p,false,false);
        }
        internal Scope Begin(Player p)
        {
            ObserveCompletion(p);
            if(!CanOutput(p) || (quickRequested || automaticRequested) && !operating || !operating && !GapReady || !Acquire())return null;
            return BeginScope(p,true);
        }
        private Scope BeginScope(Player p,bool itemCheck,bool aim=true)
        {
            bool press=DecidePress();
            var scope=new Scope(this,p,press,itemCheck,aim);
            if(itemCheck)inCheck++;
            try{scope.Apply(point);return scope;}catch{scope.End(new InvalidOperationException("afk-input-scope"));throw;}
        }
        internal void AfterReuse(Player p)
        {
            if(operating && ReferenceEquals(p,player)){handEnded=p.itemAnimation<=0;handEndedStep=Main.GameUpdateCount;}
            ObserveCompletion(p);
            if(inCheck==0 || !CanOutput(p))return;
            bool press=operating?DecidePress():GapReady && !quickRequested && !automaticRequested;
            // This is BEFORE TryStartUse inside the same ItemCheck. Native
            // AutoReuse can end an animation here; enforce the newly born gap
            // before a second actual start, without inserting a zero-gap frame.
            if(press && !Acquire())press=false;
            p.controlUseItem=press;
            if(press && !operating && WeaponCatalog.Ready(p))p.releaseUseItem=true;
        }
        internal void ManualSelection(int target)
        {
            // An override's currently selected slot is not the hotbar intent.
            // Select(current override) can still move Hotbar and dismiss the
            // override, so read both native facts before Select changes them.
            if(Running && (target!=player.selectedItem || target<10 && target!=player.selectedItemState.Hotbar))Stop();
        }
        private bool PrepareQuickBorrow()
        {
            if(!Running)return false;
            if(!Identity() || Unusable(player)){Stop();return false;}
            quickRequested=yieldRequested=true;
            ObserveCompletion(player);
            if(!operating && WeaponCatalog.Ready(player) && !player.channel && player.selectedItemState.CanChangeSelectedItemImmediately){ReleaseLease();return false;}
            return true;
        }
        // Observe an existing concrete candidate at the native selection turn.
        // This grants no new input and no second tool planner: the original
        // owner still rechecks, acquires the unique token, selects and returns.
        internal void BeforeSelection(Player p)
        {
            if(!Running || !ReferenceEquals(p,player) || !Identity())return;
            bool active=Tools.Use.Active || (processing?.Extraction.Active??false);
            if(automaticRequested)
            {
                if(active){automaticBorrowed=true;return;}
                if(automaticBorrowed)
                {
                    if(!Borrowed && WeaponCatalog.Ready(p) && p.selectedItemState.CanChangeSelectedItemImmediately)
                        automaticRequested=automaticBorrowed=yieldRequested=false;
                    return;
                }
                if(!operating && WeaponCatalog.Ready(p) && p.selectedItemState.CanChangeSelectedItemImmediately && !AutomaticCandidate())
                    automaticRequested=yieldRequested=false;
                return;
            }
            if(manual || quickRequested || Borrowed || Tools.Input.RawMouseLeft || Tools.Input.RawMouseRight || !Tools.Input.CanRunAutomaticActions || Unusable(p) || CombatUse.NativeHoldBlocked(p))return;
            if(AutomaticCandidate()){automaticRequested=yieldRequested=true;}
        }
        private bool AutomaticCandidate(){return Tools.Ready() || (processing?.Extraction.Ready()??false);}
        private bool Owns(Item[] items,int index)
        {return inCheck>0 && token!=0 && Identity() && ReferenceEquals(items,player.inventory) && index==slot;}
        private void FinishSync(){syncScope?.End(null);syncScope=null;}
        internal void Stop()
        {
            FinishSync();ReleaseLease();quick?.CancelAfkBorrow();player=null;source=null;manual=yieldRequested=quickRequested=initialHold=everStarted=automaticRequested=automaticBorrowed=false;ResetOperation();
        }
        public void OnSessionStarted(){Stop();sampled=-1;}
        public void OnSessionEnded(){Stop();sampled=-1;quick?.CancelAfkBorrow();}
        public void Update(ulong tick){FinishSync();if(Running){if(!Identity() || Unusable(player))Stop();else ObserveCompletion(player);}}
        public void FailClosed(){Stop();failed=true;}
        private void Exit(object sender,EventArgs args){AppDomain.CurrentDomain.ProcessExit-=Exit;Stop();stopping=true;preference.Stop(750);}

        internal sealed class Scope
        {
            private readonly HostAfkMode owner;private readonly CombatInputScope input;private readonly bool check,aim;
            private CombatCursorScope cursor;private bool ended;
            internal Scope(HostAfkMode owner,Player p,bool press,bool check,bool aim){this.owner=owner;this.check=check;this.aim=aim;input=new CombatInputScope(p,press,false,check);}
            internal void Apply(Vector2 world){input.Apply();if(aim)cursor=check?CombatCursorScope.BeginTileTarget(world):CombatCursorScope.Begin(world);}
            internal void End(Exception error)
            {
                if(ended)return;ended=true;
                try{cursor?.End();}finally{input.End(error==null);if(check){owner.ObserveCompletion(owner.player);if(!owner.operating)owner.ReleaseLease();if(owner.inCheck>0)owner.inCheck--;}}
                if(error!=null)owner.Stop();
            }
        }
    }
}
