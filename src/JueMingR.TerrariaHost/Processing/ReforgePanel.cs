using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Processing
{
    // This panel owns only its draft and projected commands. List browsing has
    // no keyboard lease; removal is bound to a full name, never a row index.
    internal sealed class ReforgePanel : ITextEditSession,IDisposable
    {
        internal sealed class Part {internal F5Element Element,Label;internal int Command;internal string Name;internal bool Enabled,Selected;}
        private readonly HostProcessing host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        internal readonly TextEditInput TextInput;
        internal readonly SingleLineEditView EditView=new SingleLineEditView();
        private TextEditBuffer draft=NewDraft();
        private readonly List<Part> logical=new List<Part>();
        internal readonly List<Part> Parts=new List<Part>();
        private bool editing,ready,dirty=true,previousLeft,leftTail,rightTail,available,clickPending;
        private int lastClick;
        private long revision=-1,session=-1;
        private int generation=-1,skin=-1;
        private float scroll,start;
        private F5Rect view,editRect;
        private Matrix matrix;
        private Vector2 pointer;
        private Part armed;
        private string message;
        private long editRevision=-1,caretRevision=-1;
        private string composition;
        private float editWidth;
#if DEBUG
        internal long LayoutBuilds,EditMeasurements;
#endif
        internal Action<string,F5Rect> HotkeyClicked;
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool OwnsTextToken {get{return TextInput.OwnsTextToken;}}
        internal float Height {get;private set;}
        public TextEditBuffer Editor {get{return editing?draft:null;}}
        internal ReforgePanel(HostProcessing host,F5Interaction shell,INotesIme ime=null)
        {
            this.host=host;this.shell=shell;
            TextInput=new TextEditInput(this,new NotesClipboard(()=>Main.instance.Window.Handle),ime);
            var prior=shell.BeforeLeave;shell.BeforeLeave=p=>{if(prior!=null && !prior(p))return false;Suspend();return true;};
        }
        private static TextEditBuffer NewDraft(){return new TextEditBuffer("",true,128,512,"词缀名称最多 128 个字");}
        public void PreserveUncommittedInput(TextEditBuffer editor){}
        public bool RequestFinish()
        {
            TextInput.FinishComposition(false);if(TextInput.HasComposition)return false;
            if(!host.Reforge.Targets.Add(draft.Text,out message)){dirty=true;return false;}
            editing=false;TextInput.Release(false);draft=NewDraft();dirty=true;return true;
        }
        public void CancelEdit(){editing=false;TextInput.Release(true);dirty=true;}
        internal void BeforeInput(bool active){TextInput.BeforeSample(active && shell.Visible && shell.Page==1);}
        internal void ProcessInput(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,bool blocked)
        {
            pointer=point;bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            ConsumeLeft=leftTail;ConsumeRight=rightTail;OwnsPointer=false;
            bool on=active && focused && shell.Visible && shell.Page==1;
            TextInput.AfterSample(on && !blocked,null,keys,focused);
            if(!on || session!=host.Runtime.Generation)
            {Suspend();if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}previousLeft=focused?left:true;return;}
            if(!geometryCurrent || generation!=shell.Layout.Generation || revision!=host.Settings[2].Revision || available!=host.Controls(2) || scroll!=shell.Scroll || view.X!=shell.X+shell.Layout.Viewport.X || view.Y!=shell.Y+shell.Layout.Viewport.Y){dirty=true;ResetGesture();}
            OwnsPointer=ready && view.Contains(point.X,point.Y);
            if(OwnsPointer){if(left)leftTail=true;if(right)rightTail=true;}
            Part hit=ready && !dirty && !blocked && OwnsPointer?Parts.FirstOrDefault(p=>p.Command!=0 && p.Enabled && p.Element.Rect.Contains(point.X,point.Y)):null;
            if(blocked){ResetGesture();if(editing)CancelEdit();}
            if(left && !previousLeft){armed=hit;if(hit==null || hit.Command!=1)clickPending=false;}
            if(!left && previousLeft && armed!=null && ReferenceEquals(armed,hit)){var action=armed;armed=null;Execute(action);}
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        private void Execute(Part part)
        {
            if(part.Command==1)
            {
                // Browsing/clicking the row never borrows the keyboard. A double
                // click must stay in the same projected input field generation.
                int now=Environment.TickCount;
                bool twice=clickPending && unchecked((uint)(now-lastClick))<=500;
                lastClick=now;clickPending=!twice;
                if(!twice || editing)return;
                editing=true;TextInput.PrepareEditor();
            }
            else if(part.Command==7){clickPending=false;return;}
            else if(part.Command==2){clickPending=false;RequestFinish();}
            else
            {
                CancelEdit();
                if(part.Command==3)host.Reforge.Targets.Remove(part.Name);
                else if(part.Command==4 || part.Command==5)host.Set(2,part.Command==4);
                else if(part.Command==6)HotkeyClicked?.Invoke(HostProcessing.Actions[2],part.Element.Rect);
            }
            dirty=true;
        }
        internal void Prepare(bool active,Matrix transform,float contentStart)
        {
            if(!active || !shell.Visible || shell.Page!=1){Suspend();return;}
            if(!renderer.Refresh()){ready=false;return;}
            PrepareLayout(transform,contentStart);
        }
        internal void PrepareLayout(Matrix transform,float contentStart)
        {
            matrix=transform;ready=true;var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);
            bool rebuild=dirty || generation!=shell.Layout.Generation || skin!=renderer.Generation || revision!=host.Settings[2].Revision || available!=host.Controls(2) || session!=host.Runtime.Generation || start!=contentStart || view.X!=next.X || view.Y!=next.Y;
            view=next;
            if(rebuild)
            {
#if DEBUG
                LayoutBuilds++;
#endif
                logical.Clear();float y=contentStart;var rows=new List<F5Element>();
                new F5RowLayout(rows,shell.Layout.TextSize).Row(ref y,0,view.Width,HostProcessing.Names[2],new[]{null,"添加","开启","关闭","键"},description:new F5RowDescription(HostProcessing.Actions[2],HostProcessing.Help[2]));
                foreach(var e in rows)
                {
                    int command=e.Kind==F5ElementKind.Field?1:e.Kind==F5ElementKind.Hotkey?6:e.Kind==F5ElementKind.Button?e.Text=="添加"?2:e.Text=="开启"?4:5:0;
                    logical.Add(new Part{Element=e,Command=command,Enabled=host.Controls(2),Selected=(command==4 || command==5) && host.Settings[2].Value.Enabled==(command==4)});
                }
                int columns=ItemsLayout.Columns(view.Width,ItemsLayout.CardWidth),index=0;
                float gridX=(view.Width-columns*(ItemsLayout.CardWidth+ItemsLayout.Gap)+ItemsLayout.Gap)/2;
                foreach(string name in host.Settings[2].Value.Names)
                {
                    var rect=new F5Rect(gridX+index%columns*(ItemsLayout.CardWidth+ItemsLayout.Gap),y+index/columns*(ItemsLayout.CardHeight+ItemsLayout.Gap),ItemsLayout.CardWidth,ItemsLayout.CardHeight);
                    // Match item-list cards and corner hit areas. The full name is
                    // the identity even when its compact visible label is clipped.
                    Add(new F5Rect(rect.Right-ItemsLayout.CrossSize,rect.Y,ItemsLayout.CrossSize,ItemsLayout.CrossSize),"",3,name,host.Controls(2));
                    AddCard(rect,name,host.Controls(2));index++;
                }
                if(index>0)y+=(float)Math.Ceiling(index/(double)columns)*(ItemsLayout.CardHeight+ItemsLayout.Gap)+2;
                Height=y+6;start=contentStart;revision=host.Settings[2].Revision;available=host.Controls(2);session=host.Runtime.Generation;generation=shell.Layout.Generation;skin=renderer.Generation;dirty=false;
            }
            if(rebuild || scroll!=shell.Scroll)
            {
                ResetGesture();Parts.Clear();editRect=default(F5Rect);
                foreach(var p in logical)
                {
                    var e=p.Element;if(e.Rect.Bottom<=shell.Scroll || e.Rect.Y>=shell.Scroll+view.Height)continue;
                    var rect=e.Rect.Offset(view.X,view.Y-shell.Scroll);
                    Parts.Add(new Part{Element=new F5Element(e.Kind,rect,e.Text,e.TextSize,e.TextScale,e.Command,p.Command==6?HostProcessing.Actions[2]:null,e.Description,e.HintRect.Offset(view.X,view.Y-shell.Scroll)),Command=p.Command,Name=p.Name,Enabled=p.Enabled,Selected=p.Selected,Label=p.Label==null?null:LabelAt(p.Label,p.Label.Rect.Offset(view.X,view.Y-shell.Scroll))});
                    if(p.Command==1)editRect=rect;
                }
                scroll=shell.Scroll;
            }
            // Dynamic user strings use the uncached measurement lane.
            string nextComposition=editing?TextInput.Composition:"";
            if(editRect.Width>0 && (rebuild || editRevision!=draft.Revision || caretRevision!=draft.CaretRevision || composition!=nextComposition || editWidth!=editRect.Width))
            {
                EditView.Prepare(draft,nextComposition,editRect.Width-12,shell.Layout.DynamicTextSize);
                editRevision=draft.Revision;caretRevision=draft.CaretRevision;composition=nextComposition;editWidth=editRect.Width;
#if DEBUG
                EditMeasurements++;
#endif
            }
        }
        private void Add(F5Rect rect,string text,int command,string name,bool enabled)
        {logical.Add(new Part{Element=new F5Element(F5ElementKind.Button,rect,text,name==null?shell.Layout.TextSize(text,.7f):shell.Layout.DynamicTextSize(text,.7f),.7f,F5Command.None),Command=command,Name=name,Enabled=enabled});}
        private static F5Element LabelAt(F5Element label,F5Rect rect)
        {return new F5Element(F5ElementKind.Text,rect,label.Text,label.TextSize,label.TextScale,F5Command.None);}
        private void AddCard(F5Rect rect,string name,bool enabled)
        {
            // Cache clipped visible glyph bounds, including font offsets. The
            // card and full-name identity stay the same for draw and hit testing.
            string text=name;var size=shell.Layout.DynamicTextSize(text,.7f);
            for(int n=name.Length;size.Width>rect.Width-8 && n>0;)
            {text=name.Substring(0,--n)+"…";size=shell.Layout.DynamicTextSize(text,.7f);}
            var label=new F5Element(F5ElementKind.Text,new F5Rect(rect.X+(rect.Width-size.Width)/2,rect.Y+(rect.Height-size.Height)/2,size.Width,size.Height),text,size,.7f,F5Command.None);
            logical.Add(new Part{Element=new F5Element(F5ElementKind.Button,rect,null,default(F5Size),0,F5Command.None),Label=label,Command=7,Name=name,Enabled=enabled});
        }
        internal string Hint(float x,float y,out F5Rect target,F5Rect? clip=null)
        {
            target=default(F5Rect);var visible=clip.HasValue?F5HintLayout.Intersect(view,clip.Value):view;
            if(!ready || dirty || !visible.Contains(x,y))return null;
            foreach(var p in Parts)
            {
                var rect=p.Element.Description!=null?p.Element.HintRect:p.Element.Rect;if(!rect.Contains(x,y))continue;
                target=F5HintLayout.Intersect(rect,visible);
                if(p.Command==6)return "双击设置快捷键";
                if(p.Command==3)return "从名单移除「"+p.Name+"」";
                if(p.Command==7)return p.Name;
                if(p.Command==1 || p.Command==2)return message??TextInput.Error??draft.Error??"双击输入完整词缀名，按回车或点击添加。";
                if(p.Element.Description!=null)return host.Error??host.Settings[2].Message??host.Reforge.Targets.Message??HostProcessing.Help[2];
            }
            return null;
        }
        internal void Draw(Action<F5Rect> keyboard)
        {
            if(!ready)return;
            renderer.Pass(matrix,view,()=>
            {
                foreach(var p in Parts)
                {
                    var e=p.Element;
                    if(p.Command==6)keyboard?.Invoke(e.Rect);
                    else if(e.Kind==F5ElementKind.Panel)renderer.Panel(e.Rect);
                    else if(p.Command==3)continue;
                    else if(p.Command==7)
                    {
                        bool hover=e.Rect.Contains(pointer.X,pointer.Y) && view.Contains(pointer.X,pointer.Y);
                        renderer.ItemButton(e.Rect,p.Enabled,hover);
                        renderer.Label(p.Label);
                        if(hover)renderer.Cross(new F5Rect(e.Rect.Right-ItemsLayout.CrossSize,e.Rect.Y,ItemsLayout.CrossSize,ItemsLayout.CrossSize),p.Enabled);
                    }
                    else if(p.Command==1)
                    {
                        renderer.ItemButton(e.Rect,p.Enabled,e.Rect.Contains(pointer.X,pointer.Y));
                        if(editing && EditView.SelectionRight>EditView.SelectionLeft)Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)(e.Rect.X+6+EditView.SelectionLeft),(int)e.Rect.Y+4,(int)(EditView.SelectionRight-EditView.SelectionLeft),(int)e.Rect.Height-8),Color.CornflowerBlue);
                        renderer.Text(draft.Text.Length==0 && !editing?"双击输入词缀":EditView.Text,new F5Rect(e.Rect.X+4,e.Rect.Y,e.Rect.Width-8,e.Rect.Height),Color.White,.7f);
                        if(editing){Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)(e.Rect.X+6+EditView.Caret),(int)e.Rect.Y+5,1,(int)e.Rect.Height-10),Color.White);Main.instance.SetIMEPanelAnchor(new Vector2(e.Rect.X+6+EditView.Caret,e.Rect.Bottom+32),0);}
                    }
                    else if(e.Kind!=F5ElementKind.Button)renderer.Label(e);
                    else renderer.Button(e,p.Selected,p.Enabled,p.Command==5,e.Rect.Contains(pointer.X,pointer.Y));
                }
            });
        }
        private void ResetGesture(){armed=null;clickPending=false;}
        internal void Suspend(){TextInput.Release(false);editing=false;ready=false;ResetGesture();OwnsPointer=false;dirty=true;Parts.Clear();renderer.Dispose();}
        public void Dispose(){Suspend();}
    }
}
