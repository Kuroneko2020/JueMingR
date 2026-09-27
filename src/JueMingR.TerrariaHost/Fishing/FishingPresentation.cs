using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Fishing;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Fishing
{
    // This owner holds only presentation, drafts and command receipts. Native
    // fishing, inventory and rename operations remain in their domain owners.
    internal sealed partial class FishingPresentation : ITextEditSession,IDisposable
    {
        internal enum Command { None,Feature,Hotkey,Match,Filter,Special,Current,Plus,Clear,Save,PresetList,Remove,Select,Add,Close,Confirm,Field,ApplyPreset,DeletePreset,RenameField,Rename }
        private enum Overlay { None,Current,Search,Presets,Keyword }
        private enum Edit { None,Search,Keyword,Rename }
        internal sealed class Part
        {internal F5Element Element,Label;internal Command Command;internal int Feature,Value,Mode,Match,Region;internal FishKey? Fish;internal string Name,Hint;internal FishPreset Preset;internal bool Enabled,Selected;}
        private readonly HostFishing host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        internal readonly TextEditInput TextInput;
        private readonly SingleLineEditView editView=new SingleLineEditView();
        private readonly List<Part> pageParts=new List<Part>(),listParts=new List<Part>(),popupParts=new List<Part>();
        internal readonly List<Part> Parts=new List<Part>();
        private readonly HashSet<FishKey> selected=new HashSet<FishKey>();
        private FishKey[] candidates=Array.Empty<FishKey>();
        private Overlay overlay;
        private Edit editing;
        private TextEditBuffer draft;
        private Part armed;
        private bool ready,dirty=true,leftTail,rightTail,previousLeft,previousPopupLeft,previousEscape,clicked,canConfigure,framePopup;
        private long revision=-1,session=-1,epoch,pending,pendingEpoch,pendingRevision,queryRevision=-1;
        private TextEditBuffer pendingEditor;
        private bool pendingClose;
        private int generation=-1,skin=-1,scopeMode=1,scopeMatch,lastClick;
        private float scroll,listScroll,popupScroll,listHeight,popupHeight;
        private F5Rect view,listRect,popupRect,popupBody,editRect;
        private F5Rect clickRect;
        private Matrix clickMatrix;
        private int clickGeneration,clickSkin;
        private Matrix matrix;
        private Vector2 pointer,screen;
        private object culture;
        private string message,pendingMessage;
        internal Action<string,F5Rect> HotkeyClicked;
        internal Action Opened;
        internal Func<string,bool> RenameRequested {get;set;}
        internal Func<bool> CanRename {get;set;}
        internal Func<string> RenameMessage {get;set;}
        internal bool Visible {get{return overlay!=Overlay.None;}}
        // A pending durable write is a command gate, not a change in the
        // availability of every visible control. Keep adjacent pixels stable.
        private bool PresentationAvailable {get{return host.Available && host.Player!=null && host.Settings.Loaded && !host.Settings.Protected;}}
        private static bool ListCommand(Command command)
        {return command>=Command.Current && command<=Command.Add || command==Command.Confirm || command==Command.ApplyPreset || command==Command.DeletePreset;}
        internal bool Captured {get{return armed!=null || leftTail || rightTail;}}
        internal bool CanHint {get{return ready && !dirty && !Captured && !ConsumeLeft && !ConsumeRight && !previousPopupLeft;}}
        internal bool OwnsTextToken {get{return TextInput.OwnsTextToken;}}
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool ConsumeWheel {get;private set;}
        internal bool BlocksPageWheel {get;private set;}
        internal bool BlockPointer {get{return framePopup || Visible && (Contains(pointer.X,pointer.Y) || Captured);}}
        public TextEditBuffer Editor {get{return editing==Edit.None?null:draft;}}
        internal FishingPresentation(HostFishing host,F5Interaction shell,INotesIme ime=null)
        {
            this.host=host;this.shell=shell;TextInput=new TextEditInput(this,new RenameClipboard(this,new NotesClipboard(()=>Main.instance.Window.Handle)),ime);
            RenameRequested=host.Rename.Rename;CanRename=()=>host.Rename.CanRename;RenameMessage=()=>host.Rename.Message;
            host.Rename.OwnTextInput=()=>TextInput.OwnsTextToken && !TextInput.OtherTextOwner;
            var prior=shell.BeforeLeave;shell.BeforeLeave=p=>{if(prior!=null && !prior(p))return false;Suspend();return true;};
            LanguageManager.Instance.OnLanguageChanged+=LanguageChanged;
        }
        private void LanguageChanged(LanguageManager manager){culture=null;queryRevision=-1;presetDescriptions.Clear();dirty=true;}
        private sealed class RenameClipboard : INotesClipboard
        {
            private readonly FishingPresentation owner;private readonly INotesClipboard source;
            internal RenameClipboard(FishingPresentation owner,INotesClipboard source){this.owner=owner;this.source=source;}
            public bool TryCopy(string text){return source.TryCopy(text);}
            public bool TryPaste(out string text)
            {
                if(!source.TryPaste(out text))return false;
                // Normalize the rename-specific pasted code units BEFORE the
                // common single-line editor expands tabs/rejects newlines.
                // Trim still belongs to the final complete-name command.
                if(owner.editing==Edit.Rename)text=text.Replace("\r","").Replace("\n","").Replace('\t',' ');
                return true;
            }
        }
        internal bool Contains(float px,float py){return ready && Visible && popupRect.Contains(px,py);}
        internal void BeforeInput(bool active){TextInput.BeforeSample(active && shell.Visible && shell.Page==7);}
        private void BeginEdit(Edit mode,string text)
        {TextInput.Release(false);editing=mode;draft=new TextEditBuffer(text??"",true,16384,65536,"输入内容过长。");TextInput.PrepareEditor();queryRevision=-1;dirty=true;}
        public void PreserveUncommittedInput(TextEditBuffer editor){if(ReferenceEquals(editor,pendingEditor))pendingClose=false;}
        public bool RequestFinish()
        {
            TextInput.FinishComposition(false);if(TextInput.HasComposition)return false;
            if(editing==Edit.Search){RefreshSearch();TextInput.Release(false);editing=Edit.None;dirty=true;return true;}
            if(editing==Edit.Keyword)
            {
                string word=draft.Text.Trim();if(word.Length==0){message="请输入关键词。";dirty=true;return false;}
                return Submit(host.Settings.Value.WithList(scopeMode,scopeMatch,host.Settings.Value.List(scopeMode,scopeMatch).Add(word)),true,"关键词已添加。");
            }
            if(editing==Edit.Rename)
            {
                if(!(CanRename?.Invoke()??false))return false;
                var editor=Editor;long edit=editor.Revision,stamp=epoch;
                bool saved=RenameRequested?.Invoke(editor.Text)??false;
                if(saved && epoch==stamp && ReferenceEquals(editor,Editor) && editor.Revision==edit && !TextInput.HasComposition)EndEdit();
                dirty=true;return saved;
            }
            return true;
        }
        public void CancelEdit(){if(Visible)CloseOverlay();else EndEdit();}
        private void EndEdit(){TextInput.Release(true);editing=Edit.None;draft=null;dirty=true;}
        internal void CloseOverlay()
        {EndEdit();overlay=Overlay.None;candidates=Array.Empty<FishKey>();selected.Clear();armed=null;clicked=false;epoch++;dirty=true;}
        private void Open(Overlay value)
        {
            CloseOverlay();scopeMode=host.Settings.Value.EditingMode;scopeMatch=host.Settings.Value.Match;
            overlay=value;popupScroll=0;message=null;Opened?.Invoke();dirty=true;
            if(value==Overlay.Search)BeginEdit(Edit.Search,"");else if(value==Overlay.Keyword)BeginEdit(Edit.Keyword,"");
            else if(value==Overlay.Current)
            {
                host.Observation.Invalidate();var p=host.Player;if(p!=null)host.Observation.Read(p);
                Projectile b=null;for(int i=0;i<host.Observation.Count;i++)if(host.Observation.Bobbers[i].InLiquid){b=host.Observation.Bobbers[i].Projectile;break;}
                var result=host.Catalog.Current(p,b,true);candidates=result.Keys;message=result.Message;
            }
        }
        private bool Submit(FishingOptions value,bool close,string success)
        {
            if(!host.Controls || host.Settings.Value.FilterMode==0 || pending!=0 || !SameScope() || !host.Settings.Set(value))return false;
            pending=host.Settings.AcceptedCommandId;pendingEpoch=epoch;pendingClose=close;pendingEditor=Editor;pendingRevision=Editor?.Revision??0;pendingMessage=success;dirty=true;return true;
        }
        private bool SameScope(){return scopeMode==host.Settings.Value.EditingMode && scopeMatch==host.Settings.Value.Match;}
        private void PollReceipt()
        {
            if(pending==0 || host.Settings.CompletedCommandId!=pending)return;
            pending=0;
            if(pendingEpoch!=epoch)return;
            if(!host.Settings.CompletionSucceeded){message=host.Settings.Message;dirty=true;return;}
            bool sameEditor=pendingEditor==null || ReferenceEquals(Editor,pendingEditor) && Editor.Revision==pendingRevision && !TextInput.HasComposition;
            if(pendingClose && sameEditor)CloseOverlay();message=pendingMessage;dirty=true;
        }
        private void RefreshSearch()
        {
            if(overlay!=Overlay.Search || draft==null || queryRevision==draft.Revision && ReferenceEquals(culture,Language.ActiveCulture))return;
            queryRevision=draft.Revision;culture=Language.ActiveCulture;candidates=host.Catalog.Search(draft.Text);popupScroll=0;dirty=true;
        }
        internal void ProcessPopup(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,int wheel)
        {
            pointer=point;ConsumeLeft=leftTail;ConsumeRight=rightTail;ConsumeWheel=BlocksPageWheel=OwnsPointer=framePopup=false;
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            bool on=active && focused && shell.Visible && shell.Page==7;
            if(!on || session!=host.Tools.Runtime.Generation){Suspend();if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}previousPopupLeft=focused?left:true;previousEscape=focused?keys.IsKeyDown(Keys.Escape):true;return;}
            bool hadEditor=Editor!=null;
            TextInput.AfterSample(on,null,keys,focused);PollReceipt();RefreshSearch();
            if(!SameScope() || Visible && host.Settings.Value.FilterMode==0){CloseOverlay();scopeMode=host.Settings.Value.EditingMode;scopeMatch=host.Settings.Value.Match;listScroll=0;}
            // While editing, only TextEditInput may interpret Escape after IME
            // composition and its physical key tail have been consumed.
            bool escape=keys.IsKeyDown(Keys.Escape);if(escape && !previousEscape && Visible && !hadEditor){framePopup=true;CloseOverlay();}previousEscape=escape;
            if(!Visible)
            {
                if(ready && listRect.Contains(point.X,point.Y)){BlocksPageWheel=true;if(wheel!=0)Scroll(ref listScroll,listHeight,listLocal.Height,wheel);}
                previousPopupLeft=left;return;
            }
            if(!geometryCurrent || generation!=shell.Layout.Generation || revision!=host.Settings.Revision || canConfigure!=host.Controls){armed=null;dirty=true;}
            bool inside=Contains(point.X,point.Y);
            var window=F5Layout.WindowSize(screen.X,screen.Y,matrix.M11);
            if(ready && !dirty && !inside && left && !previousPopupLeft && new F5Rect(shell.X,shell.Y,window.Width,window.Height).Contains(point.X,point.Y))
            {
                // Dismiss on a fresh main-window press and retain its release.
                // The same gesture must not navigate, drag or toggle behind it.
                CloseOverlay();leftTail=ConsumeLeft=framePopup=OwnsPointer=true;previousPopupLeft=true;return;
            }
            OwnsPointer=inside;framePopup=inside || Captured;
            if(inside){if(left)leftTail=true;if(right)rightTail=true;BlocksPageWheel=true;if(wheel!=0 && popupBody.Contains(point.X,point.Y))Scroll(ref popupScroll,popupHeight,popupBody.Height,wheel);}
            Part hit=ready && !dirty && inside?Hit(point,true):null;
            if(left && !previousPopupLeft)armed=hit;
            if(!left && previousPopupLeft && armed!=null && ReferenceEquals(armed,hit)){var action=armed;armed=null;Execute(action);}
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousPopupLeft=left;
        }
        internal void ProcessInput(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,bool blocked,int wheel)
        {
            pointer=point;bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            if(!active || !focused || !shell.Visible || shell.Page!=7){previousLeft=focused?left:true;return;}
            // Popup processing already owns this gesture (including its closing
            // release). Page processing must not replace its armed candidate.
            if(Visible || framePopup){previousLeft=left;return;}
            if(!geometryCurrent || generation!=shell.Layout.Generation || revision!=host.Settings.Revision || canConfigure!=host.Controls || scroll!=shell.Scroll || view.X!=shell.X+shell.Layout.Viewport.X || view.Y!=shell.Y+shell.Layout.Viewport.Y){armed=null;dirty=true;}
            bool inside=ready && view.Contains(point.X,point.Y);OwnsPointer|=inside;
            if(inside){if(left)leftTail=true;if(right)rightTail=true;}
            Part hit=ready && !dirty && inside && !Visible && !blocked?Hit(point,false):null;
            if(left && !previousLeft){armed=hit;if(hit==null || hit.Command!=Command.RenameField)clicked=false;}
            if(!left && previousLeft && armed!=null && ReferenceEquals(armed,hit)){var action=armed;armed=null;Execute(action);}
            ConsumeLeft|=leftTail;ConsumeRight|=rightTail;if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        private Part Hit(Vector2 p,bool popup)
        {return Parts.FirstOrDefault(v=>v.Enabled && v.Command!=Command.None && (popup?v.Region>=2:v.Region<2) && v.Element.Rect.Contains(p.X,p.Y) && (v.Region==1?listRect.Contains(p.X,p.Y):v.Region==3?popupBody.Contains(p.X,p.Y):true));}
        private void Scroll(ref float offset,float total,float height,int wheel)
        {float next=Math.Max(0,Math.Min(Math.Max(0,total-height),offset-wheel/120f*48));if(next==offset)return;offset=next;dirty=true;armed=null;ConsumeWheel=true;}
        private void Execute(Part p)
        {
            if(p.Mode!=host.Settings.Value.EditingMode || p.Match!=host.Settings.Value.Match)return;
            if(p.Command!=Command.Close && (!host.Controls || ListCommand(p.Command) && host.Settings.Value.FilterMode==0))return;
            var value=host.Settings.Value;
            switch(p.Command)
            {
                case Command.Feature:host.Set(p.Feature,p.Value);break;
                case Command.Hotkey:CloseOverlay();HotkeyClicked?.Invoke(HostFishing.Actions[p.Feature],p.Element.Rect);break;
                case Command.Match:CloseOverlay();host.Set(6,p.Value);break;
                case Command.Filter:CloseOverlay();host.Set(5,(value.FilterMode+1)%3);break;
                case Command.Special:host.Set(p.Feature,(value.State(p.Feature)+1)%3);break;
                case Command.Current:Open(Overlay.Current);break;
                case Command.Plus:Open(value.Match==0?Overlay.Search:Overlay.Keyword);break;
                case Command.Clear:Submit(value.WithList(p.Mode,p.Match,new FishList()),false,"名单已清空。");break;
                case Command.Save:Submit(value.SavePreset(p.Mode,p.Match),false,"当前名单已保存为预设。");break;
                case Command.PresetList:Open(Overlay.Presets);break;
                case Command.Remove:Submit(value.WithList(p.Mode,p.Match,p.Fish.HasValue?value.List(p.Mode,p.Match).Remove(p.Fish.Value):value.List(p.Mode,p.Match).Remove(p.Name)),false,null);break;
                case Command.Select:if(!selected.Add(p.Fish.Value))selected.Remove(p.Fish.Value);break;
                case Command.Add:if(selected.Count>0)Submit(value.WithList(scopeMode,scopeMatch,value.List(scopeMode,scopeMatch).Add(selected)),true,"已添加至名单。");break;
                case Command.Close:CloseOverlay();break;
                case Command.Confirm:RequestFinish();break;
                case Command.Field:if(editing==Edit.None)BeginEdit(overlay==Overlay.Search?Edit.Search:Edit.Keyword,draft?.Text??"");break;
                case Command.ApplyPreset:Submit(value.ApplyPreset(p.Preset,p.Mode,p.Match),true,"已用预设替换当前名单。");break;
                case Command.DeletePreset:Submit(value.DeletePreset(p.Mode,p.Match,p.Name),false,"预设已删除。");break;
                case Command.RenameField:
                    if(editing==Edit.Rename)break;
                    int now=Environment.TickCount;bool twice=clicked && unchecked((uint)(now-lastClick))<=500 && clickRect.Equals(p.Element.Rect) && clickMatrix==matrix && clickGeneration==generation && clickSkin==skin;
                    lastClick=now;clickRect=p.Element.Rect;clickMatrix=matrix;clickGeneration=generation;clickSkin=skin;clicked=!twice;
                    if(twice && (CanRename?.Invoke()??false)){CloseOverlay();BeginEdit(Edit.Rename,host.Player?.name);}break;
                case Command.Rename:
                    if(editing==Edit.Rename)RequestFinish();else if(CanRename?.Invoke()??false)RenameRequested?.Invoke(null);break;
            }
            dirty=true;armed=null;
        }
        internal void Suspend()
        {if(!ready && !Visible && Editor==null)return;CloseOverlay();ready=false;OwnsPointer=false;Parts.Clear();pageParts.Clear();listParts.Clear();popupParts.Clear();renderer.Dispose();}
        public void Dispose(){LanguageManager.Instance.OnLanguageChanged-=LanguageChanged;Suspend();}
    }
}
