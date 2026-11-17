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
        private readonly Action<string> returnToManager;
        private readonly UnifiedRandom previewRandom = new UnifiedRandom(17513);
        private readonly Dictionary<UIElement,float> keyboardTops = new Dictionary<UIElement,float>();
        private readonly object previousKeyboard;
        private readonly int previousOffset;
        private readonly bool previousHidden;
        private readonly UIText error = new UIText("", .8f);
        private bool live = true, dirty, submitting;
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

        private CompanionCreationSession(HostCompanions host,HostInputState input, Action<string> restore)
        {
            Host=host;this.input=input; owner=Main.ActivePlayerFileData?.Player; world=Main.ActiveWorldFileData;
            token=host.CreationSessionToken; returnToManager=restore; Draft=host.NewCreationDraft(); Draft.whoAmI=255;
            Hairstyles.AvailableHairstyles.AddRange(host.GetCreationHairstyles());
            previousKeyboard=KeyboardInstance.GetValue(null); previousOffset=UIVirtualKeyboard.OffsetDown; previousHidden=UIVirtualKeyboard.ShouldHideText;
        }
        internal static void Open(HostCompanions host,HostInputState input,Action<string> restore)
        {
            DisposeFor(host);
            var session=new CompanionCreationSession(host,input,restore); Active=session;
            try
            {
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
                    IngameFancyUI.OpenUIState(session.Creator);
                    session.Adapt();
                }
            }
            catch { session.End(false,null); throw; }
        }
        internal bool Owns(UIState state) { return live && state!=null && (ReferenceEquals(state,Creator)||ReferenceEquals(state,Keyboard)); }
        internal bool Valid { get { return live&&!Main.gameMenu&&ReferenceEquals(owner,Main.ActivePlayerFileData?.Player)&&ReferenceEquals(world,Main.ActiveWorldFileData)&&Host.CreationSessionToken==token; } }
        internal static void Validate(HostCompanions host)
        {
            var s=Active;if(s==null||!ReferenceEquals(s.Host,host))return;
            if(!s.Valid||!Main.inFancyUI||!s.Owns(Main.InGameUI.CurrentState))s.End(false,null);
        }
        internal static void DisposeFor(HostCompanions host)
        { var s=Active;if(s!=null&&ReferenceEquals(s.Host,host))s.End(false,null); }
        internal IDisposable Enter(bool draw) { return new DispatchScope(this,draw); }
        private sealed class DispatchScope : IDisposable
        {
            private readonly CompanionCreationSession session;
            private readonly HairstyleUnlocksHelper hair;
            private readonly bool dirty,draw;
            private readonly UnifiedRandom random;
            private readonly int shader;
            internal DispatchScope(CompanionCreationSession s,bool drawing)
            {
                s.Adapt();
                session=s;draw=drawing;hair=Main.Hairstyles;dirty=(bool)Dirty.GetValue(null);
                Main.Hairstyles=s.Hairstyles;Dirty.SetValue(null,s.dirty);
                if(draw){random=Main.rand;shader=Mount.currentShader;Main.rand=s.previewRandom;s.DrawDepth++;}
            }
            public void Dispose()
            {
                session.dirty=(bool)Dirty.GetValue(null);Dirty.SetValue(null,dirty);Main.Hairstyles=hair;
                if(draw){session.DrawDepth--;Main.rand=random;Mount.currentShader=shader;}
            }
        }
        internal void Name(bool create)
        {
            if(!Valid)return;
            if(create&&!String.IsNullOrWhiteSpace(Draft.name)){Submit();return;}
            Main.clrInput();
            var weak=new WeakReference(this);
            Keyboard=new UIVirtualKeyboard(Lang.menu[45].Value,Draft.name??"",name=> {
                var session=weak.Target as CompanionCreationSession;
                if(session==null||!session.Valid||!ReferenceEquals(Main.InGameUI.CurrentState,session.Keyboard))return;
                session.Draft.name=(name??"").Trim();session.ReturnFromName();
                if(create)session.Submit();
            },()=> {var session=weak.Target as CompanionCreationSession;if(session!=null&&session.Valid&&ReferenceEquals(Main.InGameUI.CurrentState,session.Keyboard))session.ReturnFromName();},0,true,UICharacterCreation.MAX_NAME_LENGTH);
            managedKeyboards.Add(Keyboard,managed);
            keyboardTops.Clear();foreach(var child in Keyboard.Children)keyboardTops[child]=child.Top.Pixels;
            foreach(var name in new[]{"outerLayer1","outerLayer2"}) {var child=(UIElement)AccessTools.Field(typeof(UIVirtualKeyboard),name).GetValue(Keyboard);keyboardTops[child]=child.Top.Pixels;}
            adaptedHeight=-1;Main.InGameUI.SetState(Keyboard);Adapt();
        }
        private void ReturnFromName()
        {
            Main.InGameUI.SetState(Creator);RestoreKeyboard();
            ((UICharacterNameButton)NameButton.GetValue(Creator)).SetContents(Draft.name);
            adaptedHeight=-1;Adapt();
        }
        internal void Submit()
        {
            if(!Valid||submitting)return;
            submitting=true;
            try
            {
                AssignHair.Invoke(Creator,null);
                if(Host.CreateDraft(Draft)){End(true,Draft.name);return;}
                error.SetText(Host.Feedback??"无法创建人物。");
            }
            finally{submitting=false;}
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
            if(ReferenceEquals(KeyboardInstance.GetValue(null),Keyboard))
            {KeyboardInstance.SetValue(null,previousKeyboard);UIVirtualKeyboard.OffsetDown=previousOffset;UIVirtualKeyboard.ShouldHideText=previousHidden;}
        }
        private void End(bool restore,string name)
        {
            if(!live)return;bool valid=Valid;bool owns=Owns(Main.InGameUI.CurrentState);live=false;
            // Native history can retain old states. Leave it only UI/draft
            // objects, not a delegate chain back to the retired Host/world.
            if(Creator!=null)Creator.OnScrollWheel-=ScrollOuter;
            if(ReferenceEquals(Active,this))Active=null;
            if(owns)
            {
                Main.InGameUI.SetState(null);Main.inFancyUI=false;Main.playerInventory=false;
                if(owner!=null)owner.releaseInventory=false;
                UILinkPointNavigator.Shortcuts.FANCYUI_SPECIAL_INSTRUCTIONS=0;
                UILinkPointNavigator.Shortcuts.BackButtonCommand=0;
            }
            RestoreKeyboard();
            if(restore&&valid&&owns)returnToManager(name);
        }
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
