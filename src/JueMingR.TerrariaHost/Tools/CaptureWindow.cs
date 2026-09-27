using System;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class CaptureWindow
    {
        internal static readonly string[] Names={"鱼饵","仙灵","金色","宝石","普通","松露虫","七彩草蛉","其他"};
        private readonly HostTools host;
        private readonly Func<string,float,F5Size> measure;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly F5Rect[] cells=new F5Rect[8];
        private readonly F5Element[] buttons=new F5Element[8];
        internal int LayoutBuilds {get;private set;}
        private F5Rect anchor,close,title,note,note2;
        private Matrix matrix;
        private Vector2 screen,pointer;
        private long revision,session;
        private int armed=-1,skin;
        private bool previousLeft,previousEscape,leftTail,rightTail,layoutDirty=true;
        internal bool Visible {get;private set;}
        internal bool Captured {get{return armed!=-1 || leftTail || rightTail;}}
        internal F5Rect Panel {get;private set;}
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool CanHint {get;private set;}
        internal Action Opened;
        internal CaptureWindow(HostTools host,Func<string,float,F5Size> measure){this.host=host;this.measure=measure;}
        internal void Open(F5Rect button){bool was=Visible;Close();if(was)return;Visible=true;layoutDirty=true;anchor=button;session=host.Runtime.Generation;Opened?.Invoke();}
        internal void Close(){Visible=false;armed=-1;CanHint=false;renderer.Dispose();}
        internal bool Contains(float x,float y){return Visible && Panel.Contains(x,y);}
        internal void Prepare(Matrix transform,Vector2 physical,bool resources=true)
        {
            if(!Visible)return;if(resources && !renderer.Refresh())return;
            if(revision!=host.Settings[0].Revision){armed=-1;revision=host.Settings[0].Revision;}
            if(!layoutDirty && buttons[0]!=null && screen==physical && matrix==transform && skin==renderer.Generation)return;
            var size=measure("分类仅限制主动目标，挥网可能顺带捕获其他类别",.6f);
            float width=Math.Max(360,size.Width+24),row=Math.Max(34,measure("七彩草蛉",.7f).Height+12),header=Math.Max(40,measure("自动捕捉配置",.75f).Height+12),line=Math.Max(24,size.Height+8);
            float height=header+row*4+line*2+16;Vector2 logical=physical/transform.M11;
            if(width>logical.X-24 || height>logical.Y-24){Close();return;}
            float x=Math.Max(12,Math.Min(logical.X-width-12,anchor.Right+8)),y=Math.Max(12,Math.Min(logical.Y-height-12,anchor.Y));
            var panel=new F5Rect(x,y,width,height);
            if(screen!=physical || matrix!=transform || Panel.X!=x || Panel.Y!=y || Panel.Width!=width || Panel.Height!=height || revision!=host.Settings[0].Revision || skin!=renderer.Generation)armed=-1;
            Panel=panel;screen=physical;matrix=transform;revision=host.Settings[0].Revision;skin=renderer.Generation;
            title=new F5Rect(x+8,y+4,width-50,header-8);close=new F5Rect(x+width-36,y+5,30,30);
            for(int i=0;i<8;i++){cells[i]=new F5Rect(x+8+i%2*(width/2),y+header+i/2*row,width/2-16,row-4);buttons[i]=new F5Element(F5ElementKind.Button,cells[i],Names[i],measure(Names[i],.7f),.7f,F5Command.None);}LayoutBuilds++;
            note=new F5Rect(x+8,y+header+4*row,width-16,line);note2=new F5Rect(x+8,note.Bottom,width-16,line);
            layoutDirty=false;
        }
        internal void Process(bool active,KeyboardState keys,Vector2 point,Matrix transform,Vector2 physical,bool focused)
        {
            pointer=point;OwnsPointer=false;CanHint=false;ConsumeLeft=leftTail;ConsumeRight=rightTail;
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed,escape=keys.IsKeyDown(Keys.Escape);
            if(!active || !focused || session!=host.Runtime.Generation){Close();previousLeft=focused?left:true;previousEscape=focused?escape:true;if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}return;}
            if(!Visible){previousLeft=left;previousEscape=escape;if(!left)leftTail=false;if(!right)rightTail=false;return;}
            bool current=matrix==transform && screen==physical && revision==host.Settings[0].Revision && renderer.Refresh() && skin==renderer.Generation;
            OwnsPointer=Captured || Contains(point.X,point.Y);int hit=-1;
            if(current){if(close.Contains(point.X,point.Y))hit=-2;else for(int i=0;i<8;i++)if(cells[i].Contains(point.X,point.Y)){hit=i;break;}}
            if(escape && !previousEscape){host.Input.Hotkeys.SuppressKey((int)Keys.Escape);host.Input.ConsumeHotkeyActions();Close();}previousEscape=escape;
            if(left && !previousLeft)armed=hit;if(!current)armed=-1;
            if(OwnsPointer){if(left)leftTail=true;if(right)rightTail=true;for(int key=256;key<=260;key++)if(host.Input.Hotkeys.IsNew(key))host.Input.Hotkeys.SuppressKey(key);}
            if(Visible && !left && previousLeft && armed==hit && hit!=-1){if(hit==-2)Close();else if(host.Controls(0))host.Settings[0].Set(host.Settings[0].Value.WithCategories(host.Settings[0].Value.Categories^(1<<hit)));}
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=-1;}if(!right)rightTail=false;previousLeft=left;
            CanHint=Visible && current && !left && !right && !ConsumeLeft && !ConsumeRight;
        }
        internal string Hint(float x,float y,out F5Rect rect)
        {rect=default(F5Rect);if(!Visible)return null;for(int i=0;i<8;i++)if(cells[i].Contains(x,y)){rect=cells[i];return host.Error??host.Settings[0].Message??(i==7?"保留此分类；当前原版正常捕捉观察没有独立候选。":"勾选后允许主动捕捉"+Names[i]);}return null;}
        internal void Draw()
        {
            if(!Visible)return;
            renderer.Pass(matrix,Panel,()=>{renderer.Panel(Panel);renderer.Text("自动捕捉配置",title,Color.White,.75f);renderer.Cross(close,true);
                for(int i=0;i<8;i++)renderer.Button(buttons[i],(host.Settings[0].Value.Categories&(1<<i))!=0,host.Controls(0),false,cells[i].Contains(pointer.X,pointer.Y));
                renderer.Text("分类仅限制主动目标，挥网可能顺带捕获其他类别",note,Color.White,.6f);renderer.Text("Boss 战时暂停；关闭后保留上次模式与分类",note2,Color.White,.6f);});
        }
    }
}
