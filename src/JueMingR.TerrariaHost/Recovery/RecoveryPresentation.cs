using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class RecoveryPresentation
    {
        private sealed class Part
        {internal F5Element Element;internal int Command,Value,Type;internal bool Enabled,Selected,MissingStock;internal string Hint;}
        private readonly HostRecovery host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly ItemPickerInput input=new ItemPickerInput();
        private readonly List<Part> logical=new List<Part>(),visible=new List<Part>();
        private readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        private int editor=-1,page=-1,generation=-1,skin=-1,armedGeneration;
        private long revision=-1,session=-1;
        private int[] candidates=new int[0];
        private readonly HashSet<int> stock=new HashSet<int>();
        private ulong nextStockScan;
        private bool ready,dirty=true,leftTail,rightTail,previousLeft,previousEscape;
        private float scroll;
        private F5Rect view;
        private Vector2 pointer;
        private Matrix matrix;
        private Part armed;
        internal Action<string,F5Rect> HotkeyClicked;
        internal readonly PotionConfigurationPopup PotionPopup;
        private bool pagePointer,pageLeft,pageRight;
        internal bool OwnsPointer {get{return pagePointer || PotionPopup.OwnsPointer;}}
        internal bool OwnsTextToken {get{return input.OwnsTextToken;}}
        // The inline list is mouse UI, not a text editor. Only the separate
        // medication overlay keeps the existing modal keyboard lease.
        internal bool Selecting {get{return PotionPopup.Visible;}}
        internal bool ConsumeLeft {get{return pageLeft || PotionPopup.ConsumeLeft;}}
        internal bool ConsumeRight {get{return pageRight || PotionPopup.ConsumeRight;}}
        internal bool ConsumeWheel {get{return PotionPopup.ConsumeWheel;}}
        private long Revision {get{return host.Potions.Revision+host.Buffs.Revision+host.Services.Revision+host.PresentationRevision;}}
        private static readonly string[] help={
            "受伤后根据策略自动使用回血物品",
            "蓝量不足时自动使用蓝药",
            "靠近护士自动回血",
            "靠近buff家具自动使用",
            "自动使用已选增益名单中的药品",
            "靠近税收官自动收税"};
        internal RecoveryPresentation(HostRecovery host,F5Interaction shell)
        {
            this.host=host;this.shell=shell;PotionPopup=new PotionConfigurationPopup(host,shell.Layout.TextSize);var prior=shell.BeforeLeave;
            shell.BeforeLeave=p=>{if(prior!=null && !prior(p))return false;Suspend();return true;};
        }
        internal void BeforeInput(bool active){input.BeforeInput(active && shell.Visible && shell.Page==10 && Selecting);}
        internal void ProcessInput(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,bool blocked)
        {
            pointer=point;input.Sample(keys,focused);
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            pageLeft=leftTail;pageRight=rightTail;pagePointer=false;
            bool on=active && focused && shell.Visible && (shell.Page==10 || shell.Page==1);
            if(!on || session!=host.Runtime.Generation){Suspend();if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}previousLeft=focused?left:true;previousEscape=focused?keys.IsKeyDown(Keys.Escape):true;return;}
            if(!geometryCurrent || generation!=shell.Layout.Generation || revision!=Revision || scroll!=shell.Scroll || page!=shell.Page || view.X!=shell.Layout.Viewport.X+shell.X || view.Y!=shell.Layout.Viewport.Y+shell.Y){armed=null;dirty=true;}
            if(keys.IsKeyDown(Keys.Escape) && !previousEscape && Selecting){input.Release();armed=null;}
            previousEscape=keys.IsKeyDown(Keys.Escape);
            pagePointer=ready && view.Contains(point.X,point.Y);
            if(pagePointer)
            {
                if(left)leftTail=true;if(right)rightTail=true;
                Part hit=dirty || blocked?null:visible.FirstOrDefault(p=>p.Enabled && (p.Element.Kind==F5ElementKind.Button || p.Element.Kind==F5ElementKind.Hotkey) && p.Element.Rect.Contains(point.X,point.Y));
                if(left && !previousLeft){armed=hit;armedGeneration=generation;}
                if(!left && previousLeft && hit!=null && ReferenceEquals(armed,hit) && armedGeneration==generation)Execute(hit);
            }
            pageLeft=leftTail;pageRight=rightTail;
            if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        private void Execute(Part p)
        {
            if(p.Command==-1){HotkeyClicked?.Invoke(p.Element.HotkeyTarget,p.Element.Rect);return;}
            if(p.Command>=0)host.Set(p.Command,p.Value);
            // Retain the hidden list toggle/close commands at the owner's
            // request; normal page entry always starts with the list visible.
            else if(p.Command==-2){if(p.Value<2){PotionPopup.Open(p.Value,p.Element.Rect);}else{PotionPopup.Close();editor=editor==2?-1:2;if(editor==2)Refresh();else input.Release();}}
            else if(p.Command==-3){editor=-1;input.Release();}
            else if(p.Command==-4)Refresh();
            else if(p.Command==-5)host.Buffs.Set(host.Buffs.Value.ClearBuffs());
            else if(p.Command==-6)
            {
                var settings=editor<2?host.Potions:host.Buffs;var value=settings.Value;
                bool include=editor==0?value.LifeAllowed(p.Type):editor==1?value.ManaAllowed(p.Type):!value.BuffAllowed(p.Type);
                settings.Set(value.ChangeType(editor,p.Type,include));
            }
            dirty=true;armed=null;
        }
        private void Refresh()
        {
            var next=host.Catalog.BuffCandidates();
            if(!candidates.SequenceEqual(next)){candidates=next;stock.Clear();foreach(int type in next)stock.Add(type);dirty=true;}
            nextStockScan=Main.GameUpdateCount+30;
        }
        internal void Prepare(bool active,Matrix transform,Vector2 screen)
        {
            if(!active || !shell.Visible || shell.Page!=10 && shell.Page!=1){Suspend();return;}
            if(!renderer.Refresh()){ready=false;return;}PrepareLayout(transform);
            foreach(var p in visible)if(p.Type>0 && p.Type<Terraria.ID.ItemID.Count && !icons.ContainsKey(p.Type)){Main.instance.LoadItem(p.Type);icons[p.Type]=TextureAssets.Item[p.Type]?.Value;}
            PreparePopup(transform,screen,true);
        }
        internal void PreparePopup(Matrix transform,Vector2 screen,bool resources)
        {
            if(!PotionPopup.Visible)return;
            var button=visible.FirstOrDefault(p=>p.Command==-2 && p.Value==PotionPopup.Feature);
            if(button==null){PotionPopup.Close();return;}
            PotionPopup.Prepare(transform,screen,button.Element.Rect,resources);
        }
        internal void PrepareLayout(Matrix transform)
        {
            matrix=transform;ready=true;
            // At most 98 actual slots per observation, only on the visible
            // page. Inventory absence changes a corner mark, never preferences.
            if(shell.Visible && shell.Page==10)
            {
                if(page!=10 || session!=host.Runtime.Generation){editor=2;Refresh();dirty=true;}
                else if(editor==2 && Main.GameUpdateCount>=nextStockScan)Refresh();
            }
            var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);
            bool changed=dirty || generation!=shell.Layout.Generation || skin!=renderer.Generation || revision!=Revision || page!=shell.Page || session!=host.Runtime.Generation || view.X!=next.X || view.Y!=next.Y;
            view=next;
            if(!changed && scroll==shell.Scroll)return;
            armed=null;
            if(changed)
            {
                logical.Clear();float y=0;
                if(shell.Page==1){foreach(var e in shell.Layout.Elements)y=Math.Max(y,e.Rect.Bottom+6);Row(ref y,5);}
                else{for(int i=0;i<5;i++)Row(ref y,i);if(editor==2)Editor(ref y);}
                shell.Layout.SetRecoveryContentHeight(y);shell.ClampScroll();
            }
            visible.Clear();icons.Clear();
            foreach(var p in logical)
            {
                var e=p.Element;if(e.Rect.Bottom<=shell.Scroll || e.Rect.Y>=shell.Scroll+view.Height)continue;
                visible.Add(new Part{Element=new F5Element(e.Kind,e.Rect.Offset(view.X,view.Y-shell.Scroll),e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(view.X,view.Y-shell.Scroll)),Command=p.Command,Value=p.Value,Type=p.Type,Enabled=p.Enabled,Selected=p.Selected,MissingStock=p.MissingStock,Hint=p.Hint});
            }
            revision=Revision;generation=shell.Layout.Generation;skin=renderer.Generation;scroll=shell.Scroll;page=shell.Page;session=host.Runtime.Generation;dirty=false;
        }
        private void Row(ref float y,int feature)
        {
            string[] labels=feature==0?new[]{"配置","智能","快速","关闭","键"}:feature==1?new[]{"配置","开启","关闭","键"}:new[]{"开启","关闭","键"};
            var elements=new List<F5Element>();new F5RowLayout(elements,shell.Layout.TextSize).Row(ref y,0,view.Width,HostRecovery.Names[feature],labels,description:new F5RowDescription(HostRecovery.Actions[feature],help[feature]));
            foreach(var e in elements)
            {
                int command=feature,value=e.Text=="智能"?2:e.Text=="关闭"?0:1;
                if(e.Text=="配置" || e.Text=="名单设置" || e.Text=="收起名单"){command=-2;value=feature==4?2:feature;}
                if(e.Kind==F5ElementKind.Hotkey)command=-1;
                var element=e.Kind==F5ElementKind.Hotkey?new F5Element(e.Kind,e.Rect,null,e.TextSize,e.TextScale,F5Command.None,HostRecovery.Actions[feature]):e;
                string hint=e.Text=="快速"?"掉血后立即选择合适的物品进行恢复":e.Text=="智能"?"掉血后会等待合适的阈值使用合适的药品":e.Description==null?null:host.Hint(feature)??help[feature];
                logical.Add(new Part{Element=element,Command=command,Value=value,Enabled=host.Controls(feature),Selected=command>=0 && e.Kind==F5ElementKind.Button && host.Value(feature)==value,Hint=hint});
            }
        }
        private void Editor(ref float y)
        {
            BuffPanes(ref y);
        }
        private void BuffPanes(ref float y)
        {
            float gap=8,width=(view.Width-gap)/2,start=y,left=y+8,right=y+8;
            var first=new Part();var second=new Part();logical.Add(first);logical.Add(second);
            PaneHeader(ref left,"可选增益",new[]{"刷新"},new[]{-4},0,width,"点击添加");PaneHeader(ref right,"已选增益",new[]{"跟随加","跟随删","清空"},new[]{6,7,-5},width+gap,width,"点击删除");
            left=right=Math.Max(left,right)+4;
            Grid(ref left,candidates.Where(t=>!host.Buffs.Value.BuffAllowed(t)),0,width,"暂无可选物品");
            Grid(ref right,host.Buffs.Value.AllowedBuffs,width+gap,width,"名单为空");
            y=Math.Max(left,right)+8;
            first.Element=new F5Element(F5ElementKind.Panel,new F5Rect(0,start,width,y-start),null,default(F5Size),0,F5Command.None);
            second.Element=new F5Element(F5ElementKind.Panel,new F5Rect(width+gap,start,width,y-start),null,default(F5Size),0,F5Command.None);
        }
        private void PaneText(ref float y,string text,float x,float width)
        {var list=new List<F5Element>();new F5RowLayout(list,shell.Layout.TextSize).TextLines(text,x+8,ref y,width-16,.65f);foreach(var e in list)logical.Add(new Part{Element=e});}
        private void PaneHeader(ref float y,string title,string[] actions,int[] commands,float x,float width,string hint)
        {
            float buttonWidth=actions.Sum(action=>Math.Max(30,shell.Layout.TextSize(action,.7f).Width+16)+4)-4,textY=y+6,buttonY=y;
            bool below=buttonWidth+shell.Layout.TextSize(title,.65f).Width+28>width;
            int first=logical.Count;PaneText(ref textY,title,x,below?width:width-buttonWidth-12);
            for(int i=first;i<logical.Count;i++)logical[i].Hint=hint;
            if(below)buttonY=textY+4;
            PaneButtons(ref buttonY,x+width-Math.Min(width-16,buttonWidth)-8,Math.Min(width-16,buttonWidth),actions,commands);
            y=Math.Max(textY,buttonY);
        }
        private void PaneButtons(ref float y,float x,float width,string[] labels,int[] commands)
        {
            var list=new List<F5Element>();new F5RowLayout(list,shell.Layout.TextSize).Buttons(ref y,x,width,labels);
            for(int i=0;i<list.Count;i++)logical.Add(new Part{Element=list[i],Command=commands[i],Value=commands[i]>=6?1-host.Value(commands[i]):0,Selected=commands[i]>=6 && host.Value(commands[i])!=0,Hint=commands[i]==6?"手动使用的增益物品同步加入名单":commands[i]==7?"手动删除的增益物品从名单同步删除":null,Enabled=commands[i]==-3 || host.Controls(editor==2?4:editor)});
        }
        private void Grid(ref float y,IEnumerable<int> types,float x=0,float width=0,string empty=null)
        {
            if(width==0)width=view.Width;int i=0,columns=ItemsLayout.Columns(width,ItemsLayout.CandidateWidth);
            foreach(int type in types)
            {
                var rect=new F5Rect(x+8+i%columns*(ItemsLayout.CandidateWidth+ItemsLayout.Gap),y+i/columns*(ItemsLayout.CandidateSize+ItemsLayout.Gap),ItemsLayout.CandidateWidth,ItemsLayout.CandidateSize);
                bool selected=editor==0?host.Potions.Value.LifeAllowed(type):editor==1?host.Potions.Value.ManaAllowed(type):host.Buffs.Value.BuffAllowed(type);
                logical.Add(new Part{Element=new F5Element(F5ElementKind.Button,rect,"",default(F5Size),.7f,F5Command.None),Command=-6,Type=type,Selected=selected && stock.Contains(type),MissingStock=selected && !stock.Contains(type),Enabled=host.Controls(editor==2?4:editor),Hint=host.Catalog.Describe(type,editor)});i++;
            }
            y+=(float)Math.Ceiling(i/(double)columns)*(ItemsLayout.CandidateSize+ItemsLayout.Gap);
            if(i==0)PaneText(ref y,empty??"药品目录为空，请重新打开重试。",x,width);
        }
        internal void Draw(Action<F5Rect> keyboard,bool hints)
        {
            if(!ready)return;renderer.Pass(matrix,view,()=>{foreach(var p in visible){var e=p.Element;
                if(p.Type>0){renderer.ItemButton(e.Rect,p.Enabled,e.Rect.Contains(pointer.X,pointer.Y));Texture2D icon;if(icons.TryGetValue(p.Type,out icon))renderer.PreparedItem(p.Type,icon,ItemsLayout.IconBounds(e.Rect,true));if(p.Selected)renderer.Selection(e.Rect);else if(p.MissingStock)renderer.Cross(new F5Rect(e.Rect.Right-23,e.Rect.Y-3,24,24),true);}
                else if(e.Kind==F5ElementKind.Hotkey)keyboard?.Invoke(e.Rect);
                else if(e.Kind==F5ElementKind.Button)renderer.Button(e,p.Selected,p.Enabled,false,e.Rect.Contains(pointer.X,pointer.Y));
                else if(e.Kind==F5ElementKind.Panel)renderer.Panel(e.Rect);else renderer.Label(e);}});
        }
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {
            if(PotionPopup.Contains(x,y))return PotionPopup.Hint(x,y,out rect);
            var v=clip.HasValue?F5HintLayout.Intersect(view,clip.Value):view;
            if(ready && v.Contains(x,y))foreach(var p in visible){var r=p.Element.Description!=null?p.Element.HintRect:p.Element.Rect;if(r.Contains(x,y) && p.Hint!=null){rect=F5HintLayout.Intersect(r,v);return p.Hint;}}
            rect=default(F5Rect);return null;
        }
        internal void Suspend(){ready=false;armed=null;pagePointer=false;editor=-1;page=-1;dirty=true;input.Release();PotionPopup.Close();visible.Clear();logical.Clear();icons.Clear();renderer.Dispose();}
    }
}
