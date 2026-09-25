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
        {internal F5Element Element;internal int Command,Value,Type;internal bool Enabled,Selected;internal string Hint;}
        private readonly HostRecovery host;
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly ItemPickerInput input=new ItemPickerInput();
        private readonly List<Part> logical=new List<Part>(),visible=new List<Part>();
        private readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        private int editor=-1,page=-1,generation=-1,skin=-1,armedGeneration;
        private long revision=-1,session=-1;
        private int[] candidates=new int[0];
        private bool ready,dirty=true,leftTail,rightTail,previousLeft,previousEscape;
        private float scroll;
        private F5Rect view;
        private Vector2 pointer;
        private Matrix matrix;
        private Part armed;
        internal Action<string,F5Rect> HotkeyClicked;
        internal bool OwnsPointer {get;private set;}
        internal bool OwnsTextToken {get{return input.OwnsTextToken;}}
        internal bool Selecting {get{return editor>=0;}}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool ConsumeWheel {get;private set;}
        private long Revision {get{return host.Potions.Revision+host.Buffs.Revision+host.Services.Revision;}}
        private static readonly string[] help={
            "智能采用单向50%门槛；快速选择最接近缺血量的允许药品。收藏药品可用，原版冷却不变。",
            "当前耗蓝武器不足下一次实际费用时补蓝，持续施法也可用。",
            "邻近护士治疗缺血或可治负面效果，会按原版从背包和个人银行付款。",
            "靠近七种完整增益家具时补缺失效果，自动交互不打开制作窗口。",
            "只使用名单内物品补缺失增益；不续期或升级已有同组效果。",
            "有税款并靠近税收官时领取世界金币，满包或未拾取不会重复领取。"};
        internal RecoveryPresentation(HostRecovery host,F5Interaction shell)
        {
            this.host=host;this.shell=shell;var prior=shell.BeforeLeave;
            shell.BeforeLeave=p=>{if(prior!=null && !prior(p))return false;Suspend();return true;};
        }
        internal void BeforeInput(bool active){input.BeforeInput(active && shell.Visible && shell.Page==10 && Selecting);}
        internal void ProcessInput(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,bool blocked)
        {
            pointer=point;input.Sample(keys,focused);
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            ConsumeLeft=leftTail;ConsumeRight=rightTail;ConsumeWheel=false;OwnsPointer=false;
            bool on=active && focused && shell.Visible && (shell.Page==10 || shell.Page==1);
            if(!on || session!=host.Runtime.Generation){Suspend();if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}previousLeft=focused?left:true;previousEscape=focused?keys.IsKeyDown(Keys.Escape):true;return;}
            if(!geometryCurrent || generation!=shell.Layout.Generation || revision!=Revision || scroll!=shell.Scroll || page!=shell.Page || view.X!=shell.Layout.Viewport.X+shell.X || view.Y!=shell.Layout.Viewport.Y+shell.Y){armed=null;dirty=true;}
            if(keys.IsKeyDown(Keys.Escape) && !previousEscape && Selecting){editor=-1;input.Release();dirty=true;armed=null;}
            previousEscape=keys.IsKeyDown(Keys.Escape);
            OwnsPointer=ready && view.Contains(point.X,point.Y);
            if(OwnsPointer)
            {
                if(left)leftTail=true;if(right)rightTail=true;
                Part hit=dirty || blocked?null:visible.FirstOrDefault(p=>p.Enabled && (p.Element.Kind==F5ElementKind.Button || p.Element.Kind==F5ElementKind.Hotkey) && p.Element.Rect.Contains(point.X,point.Y));
                if(left && !previousLeft){armed=hit;armedGeneration=generation;}
                if(!left && previousLeft && hit!=null && ReferenceEquals(armed,hit) && armedGeneration==generation)Execute(hit);
            }
            ConsumeLeft=leftTail;ConsumeRight=rightTail;
            if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        private void Execute(Part p)
        {
            if(p.Command==-1){HotkeyClicked?.Invoke(p.Element.HotkeyTarget,p.Element.Rect);return;}
            if(p.Command>=0)host.Set(p.Command,p.Value);
            else if(p.Command==-2){editor=p.Value;Refresh();}
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
        private void Refresh(){candidates=editor<2?host.Catalog.Get(editor==0):host.Catalog.BuffCandidates();dirty=true;}
        internal void Prepare(bool active,Matrix transform,Vector2 screen)
        {
            if(!active || !shell.Visible || shell.Page!=10 && shell.Page!=1){Suspend();return;}
            if(!renderer.Refresh()){ready=false;return;}PrepareLayout(transform);
            foreach(var p in visible)if(p.Type>0 && !icons.ContainsKey(p.Type)){Main.instance.LoadItem(p.Type);icons[p.Type]=TextureAssets.Item[p.Type]?.Value;}
        }
        internal void PrepareLayout(Matrix transform)
        {
            matrix=transform;ready=true;
            var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);
            bool changed=dirty || generation!=shell.Layout.Generation || skin!=renderer.Generation || revision!=Revision || page!=shell.Page || session!=host.Runtime.Generation || view.X!=next.X || view.Y!=next.Y;
            view=next;
            if(!changed && scroll==shell.Scroll)return;
            armed=null;
            if(changed)
            {
                logical.Clear();float y=0;
                if(shell.Page==1){foreach(var e in shell.Layout.Elements)y=Math.Max(y,e.Rect.Bottom+6);Row(ref y,5);}
                else{for(int i=0;i<5;i++)Row(ref y,i);if(editor>=0)Editor(ref y);}
                shell.Layout.SetRecoveryContentHeight(y);shell.ClampScroll();
            }
            visible.Clear();icons.Clear();
            foreach(var p in logical)
            {
                var e=p.Element;if(e.Rect.Bottom<=shell.Scroll || e.Rect.Y>=shell.Scroll+view.Height)continue;
                visible.Add(new Part{Element=new F5Element(e.Kind,e.Rect.Offset(view.X,view.Y-shell.Scroll),e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(view.X,view.Y-shell.Scroll)),Command=p.Command,Value=p.Value,Type=p.Type,Enabled=p.Enabled,Selected=p.Selected,Hint=p.Hint});
            }
            revision=Revision;generation=shell.Layout.Generation;skin=renderer.Generation;scroll=shell.Scroll;page=shell.Page;session=host.Runtime.Generation;dirty=false;
        }
        private void Row(ref float y,int feature)
        {
            string[] labels=feature==0?new[]{"药品设置","智能","快速","关闭","键"}:feature==1?new[]{"药品设置","开启","关闭","键"}:feature==4?new[]{"名单设置","开启","关闭","键"}:new[]{"开启","关闭","键"};
            var elements=new List<F5Element>();new F5RowLayout(elements,shell.Layout.TextSize).Row(ref y,0,view.Width,HostRecovery.Names[feature],labels,description:new F5RowDescription(HostRecovery.Actions[feature],help[feature]));
            foreach(var e in elements)
            {
                int command=feature,value=e.Text=="智能"?2:e.Text=="关闭"?0:1;
                if(e.Text=="药品设置" || e.Text=="名单设置"){command=-2;value=feature==4?2:feature;}
                if(e.Kind==F5ElementKind.Hotkey)command=-1;
                var element=e.Kind==F5ElementKind.Hotkey?new F5Element(e.Kind,e.Rect,null,e.TextSize,e.TextScale,F5Command.None,HostRecovery.Actions[feature]):e;
                logical.Add(new Part{Element=element,Command=command,Value=value,Enabled=host.Controls(feature),Selected=command>=0 && e.Kind==F5ElementKind.Button && host.Value(feature)==value,Hint=e.Description==null?null:host.Hint(feature)??help[feature]});
            }
        }
        private void Editor(ref float y)
        {
            Buttons(ref y,new[]{"刷新列表","完成"},new[]{-4,-3});
            if(editor==2)
            {
                Buttons(ref y,new[]{"清空名单",host.Value(6)==0?"跟随添加：关":"跟随添加：开",host.Value(7)==0?"跟随删除：关":"跟随删除：开"},new[]{-5,6,7});
                Text(ref y,"可选物品（主背包与可用虚空袋）");Grid(ref y,candidates.Where(t=>!host.Buffs.Value.BuffAllowed(t)));
                Text(ref y,"已选物品（点击移除）");Grid(ref y,host.Buffs.Value.AllowedBuffs);
            }
            else{Text(ref y,editor==0?"回血药品 · 已选表示允许":"回蓝药品 · 已选表示允许");Grid(ref y,candidates);}
        }
        private void Text(ref float y,string text)
        {var list=new List<F5Element>();new F5RowLayout(list,shell.Layout.TextSize).TextLines(text,8,ref y,view.Width-16,.65f);foreach(var e in list)logical.Add(new Part{Element=e});}
        private void Buttons(ref float y,string[] labels,int[] commands)
        {
            var list=new List<F5Element>();new F5RowLayout(list,shell.Layout.TextSize).Buttons(ref y,8,view.Width-16,labels);
            for(int i=0;i<list.Count;i++)logical.Add(new Part{Element=list[i],Command=commands[i],Value=commands[i]>=6?1-host.Value(commands[i]):0,Enabled=commands[i]==-3 || host.Controls(editor==2?4:editor)});
        }
        private void Grid(ref float y,IEnumerable<int> types)
        {
            int i=0,columns=ItemsLayout.Columns(view.Width,ItemsLayout.CandidateWidth);
            foreach(int type in types)
            {
                var rect=new F5Rect(8+i%columns*(ItemsLayout.CandidateWidth+ItemsLayout.Gap),y+i/columns*(ItemsLayout.CandidateSize+ItemsLayout.Gap),ItemsLayout.CandidateWidth,ItemsLayout.CandidateSize);
                bool selected=editor==0?host.Potions.Value.LifeAllowed(type):editor==1?host.Potions.Value.ManaAllowed(type):host.Buffs.Value.BuffAllowed(type);
                logical.Add(new Part{Element=new F5Element(F5ElementKind.Button,rect,"",default(F5Size),.7f,F5Command.None),Command=-6,Type=type,Selected=selected,Enabled=host.Controls(editor==2?4:editor),Hint=host.Catalog.Describe(type,editor)});i++;
            }
            y+=(float)Math.Ceiling(i/(double)columns)*(ItemsLayout.CandidateSize+ItemsLayout.Gap);
            if(i==0)Text(ref y,editor==2?"暂无物品，可刷新列表。":"药品目录为空，请重新打开重试。");
        }
        internal void Draw(Action<F5Rect> keyboard,bool hints)
        {
            if(!ready)return;renderer.Pass(matrix,view,()=>{foreach(var p in visible){var e=p.Element;
                if(p.Type>0){renderer.ItemButton(e.Rect,p.Enabled,e.Rect.Contains(pointer.X,pointer.Y));Texture2D icon;if(icons.TryGetValue(p.Type,out icon))renderer.PreparedItem(p.Type,icon,ItemsLayout.IconBounds(e.Rect,true));if(p.Selected)renderer.Selection(e.Rect);}
                else if(e.Kind==F5ElementKind.Hotkey)keyboard?.Invoke(e.Rect);
                else if(e.Kind==F5ElementKind.Button)renderer.Button(e,p.Selected,p.Enabled,false,e.Rect.Contains(pointer.X,pointer.Y));else renderer.Label(e);}});
        }
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {
            var v=clip.HasValue?F5HintLayout.Intersect(view,clip.Value):view;
            if(ready && v.Contains(x,y))foreach(var p in visible){var r=p.Element.Description!=null?p.Element.HintRect:p.Element.Rect;if(r.Contains(x,y) && p.Hint!=null){rect=F5HintLayout.Intersect(r,v);return p.Hint;}}
            rect=default(F5Rect);return null;
        }
        internal void Suspend(){ready=false;armed=null;OwnsPointer=false;editor=-1;dirty=true;input.Release();visible.Clear();logical.Clear();icons.Clear();renderer.Dispose();}
    }
}
