using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Gamepad;
using Terraria.Utilities;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.F5
{
    // Owns a native modal page, not a second character creator. Every native
    // menu/file exit is intercepted for this exact instance before it can run.
    internal sealed class CompanionCreationSession
    {
        internal static CompanionCreationSession Active { get; private set; }
        private static readonly ConditionalWeakTable<UICharacterCreation,object> managedCreators=new ConditionalWeakTable<UICharacterCreation,object>();
        private static readonly ConditionalWeakTable<UIVirtualKeyboard,object> managedKeyboards=new ConditionalWeakTable<UIVirtualKeyboard,object>();
        private static readonly object managed=new object();
        internal static bool WasManaged(UICharacterCreation creator) {object marker;return managedCreators.TryGetValue(creator,out marker);}
        internal static bool WasManaged(UIVirtualKeyboard keyboard) {object marker;return managedKeyboards.TryGetValue(keyboard,out marker);}
        internal readonly HostCompanions Host;
        private readonly F5Shell shell;
        private readonly HostInputState input;
        internal bool InputAllowed {get{return input.CanUseInput;}}
        internal readonly Player Draft;
        internal UICharacterCreation Creator { get; private set; }
        internal UIVirtualKeyboard Keyboard { get; private set; }
        internal GameTipsDisplay Tips { get; private set; }
        internal float OuterScroll { get; private set; }
        private float outerMaximum,baseTop;
        internal readonly HairstyleUnlocksHelper Hairstyles = new HairstyleUnlocksHelper();
        private readonly Player owner;
        private readonly object world;
        private readonly long token;
        private readonly Action<string, bool> returnToManager;
        private readonly UnifiedRandom previewRandom = new UnifiedRandom(17513);
        private readonly Dictionary<UIElement,float> keyboardTops = new Dictionary<UIElement,float>();
        private object previousKeyboard;
        private int previousOffset;
        private bool previousHidden;
        private readonly UIText error = new UIText("", .8f) { IgnoresMouseInteraction = true };
        private bool live = true, dirty, submitting;
        private bool openingNativePage, accepted;
        private string operation;
        private GameTime queuedTime;
        private long queuedFrame=-1,flushedFrame=-1,intentFrame=-1;
        private bool dispatching,drawLeft,drawRight,backIntent,keyboardSubmitIntent,inventoryHeld;
        private int categoryIntent;
        private float adaptedWidth=-1, adaptedHeight=-1;
        internal int DrawDepth { get; private set; }
        private static readonly FieldInfo Dirty = AccessTools.Field(typeof(UICharacterCreation), "dirty");
        private static readonly FieldInfo KeyboardInstance = AccessTools.Field(typeof(UIVirtualKeyboard), "_currentInstance");
        private static readonly MethodInfo UpdateDifficulty = AccessTools.Method(typeof(UICharacterCreation), "UpdateDifficultyDescription");
        private static readonly MethodInfo Template = AccessTools.Method(typeof(UICharacterCreation), "GetPlayerTemplateValues");
        private static readonly MethodInfo AssignHair = AccessTools.Method(typeof(UICharacterCreation), "TryAutoAssigningHair");
        private static readonly FieldInfo InitialState = AccessTools.Field(typeof(UICharacterCreation), "initialState");
        private static readonly FieldInfo SelectedPicker=AccessTools.Field(typeof(UICharacterCreation),"_selectedPicker");
        private static readonly MethodInfo CharacterInfo=AccessTools.Method(typeof(UICharacterCreation),"Click_CharInfo");
        private static readonly FieldInfo NameButton = AccessTools.Field(typeof(UICharacterCreation), "_charName");
        private static readonly FieldInfo CurrentState = AccessTools.Field(typeof(UserInterface), "_currentState");
        private static readonly FieldInfo Hover = AccessTools.Field(typeof(UserInterface), "_lastElementHover");

        private CompanionCreationSession(F5Shell shell, HostCompanions host,HostInputState input, Action<string, bool> restore)
        {
            this.shell=shell;Host=host;this.input=input; owner=Main.ActivePlayerFileData?.Player; world=Main.ActiveWorldFileData;
            token=host.CreationSessionToken; returnToManager=restore; Draft=host.NewCreationDraft(); Draft.whoAmI=255;
            Hairstyles.AvailableHairstyles.AddRange(host.GetCreationHairstyles());
        }
        internal static void Open(F5Shell shell, HostCompanions host, HostInputState input, Action<string, bool> restore)
        {
            if (!shell.OwnsCompanionCreation || Main.inFancyUI) return;
            DisposeFor(shell);
            CompanionCreationSession session=null;
            try
            {
                session=new CompanionCreationSession(shell,host,input,restore); Active=session;
                session.inventoryHeld=PlayerInput.Triggers.Current.Inventory;
                byte difficulty=session.Draft.difficulty;
                using(session.Enter(false))
                {
                    session.Creator=new UICharacterCreation(session.Draft);managedCreators.Add(session.Creator,managed);
                    session.Tips=(GameTipsDisplay)AccessTools.Field(typeof(UICharacterCreation),"_tips").GetValue(session.Creator);
                    session.Creator.OnScrollWheel+=session.ScrollOuter;
                    session.Draft.difficulty=difficulty;
                    UpdateDifficulty.Invoke(session.Creator,new object[]{null,null});
                    InitialState.SetValue(session.Creator,Template.Invoke(session.Creator,null));
                    session.dirty=false; Dirty.SetValue(null,false);
                    session.error.Top.Set(348,0); session.error.HAlign=.5f;
                    session.Creator.Children.First().Append(session.error);
                    session.openingNativePage=true;
                    IngameFancyUI.OpenUIState(session.Creator);
                    session.openingNativePage=false;
                    session.Adapt();
                    input.ConsumeNativeUiActions();
                }
            }
            catch (Exception exception)
            {
                if (session != null) session.Recover("open",exception);
                else
                {
                    Exception cleanup=null;
                    Attempt(input.RetainNativeUiTail,ref cleanup);
                    Attempt(() => restore(null,true),ref cleanup);
                    shell.ReportCreationFailure("open",exception,cleanup,"creator=unconstructed");
                }
            }
        }
        internal bool Owns(UIState state) { return live && state!=null && (ReferenceEquals(state,Creator)||ReferenceEquals(state,Keyboard)); }
        internal bool Dispatching {get{return dispatching;}}
        internal void QueueNativeUpdate(GameTime time)
        {if(flushedFrame!=input.Frame){queuedTime=time;queuedFrame=input.Frame;}}
        internal void RequestBack()
        {
            if(!InputAllowed){intentFrame=input.Frame;backIntent=true;return;}
            try { Back(); } catch(Exception exception) { Recover("back",exception); }
        }
        internal void RequestCategory(int direction)
        {
            if(!InputAllowed){intentFrame=input.Frame;categoryIntent=direction;return;}
            try { if(ReferenceEquals(Main.InGameUI.CurrentState,Creator))Creator.TryMovingCategory(direction); }
            catch(Exception exception) { Recover("category",exception); }
        }
        internal void RequestKeyboardSubmit() {intentFrame=input.Frame;keyboardSubmitIntent=true;}
        internal static void FlushNativeUpdate(F5Shell shell)
        {
            var s=Active;if(s==null||!ReferenceEquals(s.shell,shell))return;
            try
            {
                if(!s.Valid||!s.Owns(Main.InGameUI.CurrentState)){s.End(false,null);return;}
                // Vanilla updates InGameUI before this frame's mouse/keyboard
                // mapping. Only this exact owned page is deferred until the
                // existing host InputPostfix has finalized its trusted sample.
                if(s.queuedTime==null||s.queuedFrame!=s.input.Frame||s.flushedFrame==s.input.Frame)return;
                var time=s.queuedTime;s.queuedTime=null;s.flushedFrame=s.input.Frame;
                if(!s.InputAllowed){Main.InGameUI.ResetLasts();s.backIntent=s.keyboardSubmitIntent=false;s.categoryIntent=0;return;}
                s.drawLeft=!s.input.HotkeyPointerOwned&&(Main.mouseLeft||s.input.RawMouseLeft);
                s.drawRight=!s.input.HotkeyPointerOwned&&(Main.mouseRight||s.input.RawMouseRight);
                bool inventory=PlayerInput.Triggers.Current.Inventory;
                bool inventoryPress=inventory&&!s.inventoryHeld&&!s.input.HotkeyPointerOwned;s.inventoryHeld=inventory;
                using(var pointer=new PointerScope(s))
                {
                    // The world packs are consumed below, so receive the real
                    // mapped Inventory action here exactly once, via vanilla's
                    // ToggleInv -> IngameFancyUI.Close path. A held press must
                    // not return from naming and then cancel its creator too.
                    if(inventoryPress){using(s.Enter(false))s.owner.ToggleInv();s.backIntent=s.keyboardSubmitIntent=false;s.categoryIntent=0;return;}
                    if(s.intentFrame==s.input.Frame)using(s.Enter(false)) {
                        if(s.backIntent&&!inventory)s.Back();else if(s.keyboardSubmitIntent&&ReferenceEquals(Main.InGameUI.CurrentState,s.Keyboard))UIVirtualKeyboard.Submit();else if(s.categoryIntent!=0&&ReferenceEquals(Main.InGameUI.CurrentState,s.Creator))s.Creator.TryMovingCategory(s.categoryIntent);
                    }
                    s.backIntent=s.keyboardSubmitIntent=false;s.categoryIntent=0;
                    if(!s.live||!s.Owns(Main.InGameUI.CurrentState))return;
                    s.dispatching=true;try{Main.InGameUI.Update(time);}finally{s.dispatching=false;}
                }
            }
            catch(Exception exception) {s.Recover("native-update",exception);}
            finally {s.input.ConsumeNativeUiActions();}
        }
        internal bool Valid { get { return live&&shell.OwnsCompanionCreation&&Host.ArchiveIdentityCurrent()&&ReferenceEquals(owner,Main.ActivePlayerFileData?.Player)&&ReferenceEquals(world,Main.ActiveWorldFileData)&&Host.CreationSessionToken==token; } }
        internal static void Validate(HostCompanions host)
        {
            var s=Active;if(s==null||!ReferenceEquals(s.Host,host))return;
            try {if(!s.Valid||!Main.inFancyUI||!s.Owns(Main.InGameUI.CurrentState))s.End(false,null);}
            catch(Exception exception) {s.End(false,null,exception,"validate");}
        }
        internal static void DisposeFor(HostCompanions host)
        { var s=Active;if(s!=null&&ReferenceEquals(s.Host,host))s.End(false,null); }
        internal static Exception DisposeFor(F5Shell shell)
        { var s=Active;return s!=null&&ReferenceEquals(s.shell,shell)?s.End(false,null, report:false):null; }
        internal static string DescribeFor(F5Shell shell)
        {
            var s=Active;
            if(s==null||!ReferenceEquals(s.shell,shell))return "creator=none";
            try {return s.FailureState(Main.InGameUI.CurrentState,s.Valid);}
            catch(Exception) {return "creator=owned;stateUnavailable=true";}
        }
        internal IDisposable Enter(bool draw) { return new DispatchScope(this,draw); }
        private sealed class DispatchScope : IDisposable
        {
            private readonly CompanionCreationSession session;
            private readonly HairstyleUnlocksHelper hair;
            private readonly bool dirty,draw;
            private readonly UnifiedRandom random;
            private readonly int shader;
            private readonly PointerScope pointer;
            private readonly TriggersSet triggers;
            private readonly bool triggerLeft,triggerRight;
            internal DispatchScope(CompanionCreationSession s,bool drawing)
            {
                s.Adapt();
                session=s;draw=drawing;hair=Main.Hairstyles;dirty=(bool)Dirty.GetValue(null);
                Dirty.SetValue(null,s.dirty);Main.Hairstyles=s.Hairstyles;
                if(draw){pointer=new PointerScope(s);triggers=PlayerInput.Triggers.Current;triggerLeft=triggers.MouseLeft;triggerRight=triggers.MouseRight;
                    // Native colored/pitch sliders read Current.MouseLeft in
                    // DrawSelf as well as Main.mouseLeft. Borrow only these two
                    // trusted buttons for this exact owned draw, then restore
                    // the already-consumed world sample even if a callback exits.
                    triggers.MouseLeft=Main.mouseLeft;triggers.MouseRight=Main.mouseRight;
                    random=Main.rand;shader=Mount.currentShader;Main.rand=s.previewRandom;s.DrawDepth++;}
            }
            public void Dispose()
            {
                try {session.dirty=(bool)Dirty.GetValue(null);Dirty.SetValue(null,dirty);}
                finally
                {
                    Main.Hairstyles=hair;
                    if(draw){session.DrawDepth--;Main.rand=random;Mount.currentShader=shader;triggers.MouseLeft=triggerLeft;triggers.MouseRight=triggerRight;pointer.Dispose();}
                }
            }
        }
        private sealed class PointerScope : IDisposable
        {
            private readonly int x=Main.mouseX,y=Main.mouseY,lastX=Main.lastMouseX,lastY=Main.lastMouseY,width=Main.screenWidth,height=Main.screenHeight;
            private readonly bool left=Main.mouseLeft,right=Main.mouseRight;
            internal PointerScope(CompanionCreationSession s)
            {
                PlayerInput.SetZoom_UI();
                bool trusted=s.InputAllowed&&s.flushedFrame==s.input.Frame;
                Main.mouseLeft=trusted&&s.drawLeft;Main.mouseRight=trusted&&s.drawRight;
            }
            public void Dispose()
            {Main.mouseX=x;Main.mouseY=y;Main.lastMouseX=lastX;Main.lastMouseY=lastY;Main.screenWidth=width;Main.screenHeight=height;Main.mouseLeft=left;Main.mouseRight=right;}
        }
        internal void Name(bool create)
        {
            if(!Valid)return;
            if(create&&!String.IsNullOrWhiteSpace(Draft.name)){Submit();return;}
            Main.clrInput();
            var weak=new WeakReference(this);
            // Native history can keep an older keyboard from this same draft.
            // Its delegates must still compare against that exact instance.
            UIVirtualKeyboard keyboard=null;
            previousKeyboard=KeyboardInstance.GetValue(null);
            previousOffset=UIVirtualKeyboard.OffsetDown;previousHidden=UIVirtualKeyboard.ShouldHideText;
            try
            {
                keyboard=new UIVirtualKeyboard(Lang.menu[45].Value,Draft.name??"",
                    name=>(weak.Target as CompanionCreationSession)?.CompleteName(keyboard,name,create,false),
                    ()=>(weak.Target as CompanionCreationSession)?.CompleteName(keyboard,null,false,true),
                    0,true,UICharacterCreation.MAX_NAME_LENGTH);
                Keyboard=keyboard;managedKeyboards.Add(keyboard,managed);
            }
            catch
            {
                // The vanilla constructor publishes its static instance before
                // building its controls. An incomplete owned keyboard still
                // needs its previous static state returned on retirement.
                if (keyboard==null && KeyboardInstance.GetValue(null) is UIVirtualKeyboard incomplete && !ReferenceEquals(incomplete,previousKeyboard))
                    Keyboard=incomplete;
                throw;
            }
            keyboardTops.Clear();foreach(var child in Keyboard.Children)keyboardTops[child]=child.Top.Pixels;
            foreach(var name in new[]{"outerLayer1","outerLayer2"}) {var child=(UIElement)AccessTools.Field(typeof(UIVirtualKeyboard),name).GetValue(Keyboard);keyboardTops[child]=child.Top.Pixels;}
            adaptedHeight=-1;Main.InGameUI.SetState(Keyboard);Adapt();
        }
        private void CompleteName(UIVirtualKeyboard keyboard,string name,bool create,bool cancelled)
        {
            if(keyboard==null||!Valid||!InputAllowed||!ReferenceEquals(keyboard,Keyboard)||!ReferenceEquals(Main.InGameUI.CurrentState,keyboard))return;
            try
            {
                if(!cancelled)Draft.name=(name??"").Trim();
                ReturnFromName();
                if(create)Submit();
            }
            catch(Exception exception) {Recover("return-from-naming",exception);}
        }
        private void ReturnFromName()
        {
            Main.InGameUI.SetState(Creator);RestoreKeyboard();
            Keyboard=null;keyboardTops.Clear();
            ((UICharacterNameButton)NameButton.GetValue(Creator)).SetContents(Draft.name);
            adaptedHeight=-1;Adapt();
        }
        internal void Submit()
        {
            if(!Valid||submitting)return;
            submitting=true;
            int hair=Draft.hair;
            try
            {
                operation="accept-draft";
                AssignHair.Invoke(Creator,null);
                if(Host.CreateDraft(Draft)){accepted=true;End(true,Draft.name);return;}
                // Vanilla chooses a default hair on confirmation. A business
                // refusal keeps this draft's appearance available for retry.
                Draft.hair=hair;
                operation="show-draft-rejection";
                error.SetText(Host.Feedback??"无法创建人物。");
            }
            catch(Exception exception) {Recover("submit",exception);}
            finally{submitting=false;operation=null;}
        }
        internal void Back()
        {
            if(!Valid){End(false,null);return;}
            if(ReferenceEquals(Main.InGameUI.CurrentState,Keyboard)){ReturnFromName();return;}
            if(Convert.ToInt32(SelectedPicker.GetValue(Creator))!=0)CharacterInfo.Invoke(Creator,new object[]{null,null});
            else Cancel();
        }
        internal void Cancel() { End(true,null); }
        private void RestoreKeyboard()
        {
            if(Keyboard!=null&&ReferenceEquals(KeyboardInstance.GetValue(null),Keyboard))
            {KeyboardInstance.SetValue(null,previousKeyboard);UIVirtualKeyboard.OffsetDown=previousOffset;UIVirtualKeyboard.ShouldHideText=previousHidden;}
        }
        internal void Recover(string phase, Exception primary, Exception scopeCleanup=null)
        { End(true,accepted?Draft.name:null,primary,operation??phase,scopeCleanup); }

        private Exception End(bool restore,string name,Exception primary=null,string phase="retire",Exception cleanup=null,bool report=true)
        {
            if(!live)
            {
                if(report&&primary!=null)shell.ReportCreationFailure(phase,primary,cleanup,"creator=retired;accepted="+accepted);
                return cleanup;
            }
            var ui=Main.InGameUI;
            var state=ui.CurrentState;
            bool owns=Owns(state),valid=false;
            if(restore)Attempt(() => valid=Valid,ref cleanup);
            string snapshot=primary==null?null:FailureState(state,valid);
            live=false;
            if(ReferenceEquals(Active,this))Active=null;
            queuedTime=null;backIntent=keyboardSubmitIntent=false;categoryIntent=0;
            Attempt(input.RetainNativeUiTail,ref cleanup);
            // Native history can retain old states. Leave it only UI/draft
            // objects, not a delegate chain back to the retired Host/world.
            if(Creator!=null)Attempt(() => Creator.OnScrollWheel-=ScrollOuter,ref cleanup);
            if(owns)Attempt(() => RemoveNativePage(ui,state),ref cleanup);
            // OpenUIState raises the global bit before assigning the page.
            // The constructor is only admitted with no other fancy UI open.
            if((owns||openingNativePage) && ui.CurrentState==null)
            {
                Main.inFancyUI=false;Main.playerInventory=false;
                if(owner!=null)owner.releaseInventory=false;
                UILinkPointNavigator.Shortcuts.FANCYUI_SPECIAL_INSTRUCTIONS=0;
                UILinkPointNavigator.Shortcuts.BackButtonCommand=0;
            }
            Attempt(RestoreKeyboard,ref cleanup);
            if(owns&&state==Keyboard&&ui.CurrentState==null&&PlayerInput.WritingText &&
                Main.CurrentInputTextTakerOverride==null&&!Main.drawingPlayerChat&&!Main.editSign&&!Main.editChest)
            {
                // Native Deactivate normally returns IME ownership. A broken
                // MouseOut can stop SetState before it reaches that callback.
                PlayerInput.WritingText=false;
                Attempt(() => Main.instance?.HandleIME(),ref cleanup);
            }
            if(valid&&ui.CurrentState==null && (owns||primary!=null))
                Attempt(() => returnToManager(name,primary!=null||cleanup!=null),ref cleanup);
            if(report)
            {
                if(primary!=null)shell.ReportCreationFailure(phase,primary,cleanup,snapshot);
                else if(cleanup!=null)shell.ReportCreationFailure(phase,cleanup,null,FailureState(state,valid));
            }
            return cleanup;
        }

        private static void Attempt(Action action,ref Exception first)
        { try { action(); } catch(Exception exception) { if(first==null)first=exception; } }

        private static void RemoveNativePage(UserInterface ui,UIState owned)
        {
            try { ui.SetState(null); }
            finally
            {
                // SetState invokes MouseOut and Deactivate before unmounting.
                // If either failed, retire only the exact still-owned page.
                if(ReferenceEquals(ui.CurrentState,owned))
                {
                    CurrentState.SetValue(ui,null);Hover.SetValue(ui,null);ui.ClearPointers();ui.IsVisible=false;
                }
            }
        }

        private string FailureState(UIState state,bool valid) =>
            $"creator={(state==null?"none":state==Creator?"creation":state==Keyboard?"naming":"other")};valid={valid};accepted={accepted};savePending={accepted&&Host.CreationSavePending};" +
            $"tokenMatches={Host.CreationSessionToken==token};worldMatches={ReferenceEquals(world,Main.ActiveWorldFileData)};" +
            $"ownerMatches={ReferenceEquals(owner,Main.ActivePlayerFileData?.Player)};inputAllowed={InputAllowed};" +
            $"queued={queuedFrame==input.Frame};flushed={flushedFrame==input.Frame};left={drawLeft};right={drawRight};writingText={PlayerInput.WritingText}";
        private void ScrollOuter(UIScrollWheelEvent evt,UIElement listening)
        {
            if(!ReferenceEquals(Main.InGameUI.CurrentState,Creator)||outerMaximum<=0)return;
            // The native hair list keeps its own wheel. Only the surrounding
            // page scrolls the finite overflow of preview/actions/tips.
            var hair=(UIElement)AccessTools.Field(typeof(UICharacterCreation),"_hairstylesContainer").GetValue(Creator);
            for(var element=evt.Target;element!=null;element=element.Parent)if(ReferenceEquals(element,hair))return;
            OuterScroll=MathHelper.Clamp(OuterScroll-Math.Sign(evt.ScrollWheelValue)*40,0,outerMaximum);
            Creator.Children.First().Top.Set(baseTop-OuterScroll,0);Creator.Recalculate();
        }
        internal bool TipsFit {get {return adaptedHeight-(baseTop-OuterScroll+339)>=120;}}
        private void Adapt()
        {
            if(Creator==null)return;
            var dimensions=Main.InGameUI.GetDimensions();float w=dimensions.Width,h=dimensions.Height;
            if(w==adaptedWidth&&h==adaptedHeight)return;adaptedWidth=w;adaptedHeight=h;
            // Keep native relative geometry and fonts. Move only the outer roots
            // so the preview above and action buttons below fit the same viewport.
            baseTop=Math.Max(24,Math.Min(150,(h-430)/2));
            outerMaximum=Math.Max(0,baseTop+339+120-h);OuterScroll=Math.Min(OuterScroll,outerMaximum);
            Creator.Children.First().Top.Set(baseTop-OuterScroll,0);
            Creator.Recalculate();
            if(Keyboard!=null)
            {
                float shift=Math.Min(0,(h-310)/2-270);
                foreach(var pair in keyboardTops)pair.Key.Top.Set(pair.Value+shift,0);
                Keyboard.Recalculate();
            }
        }
    }
}
