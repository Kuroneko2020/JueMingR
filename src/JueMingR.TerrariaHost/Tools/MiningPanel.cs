using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class MiningPanel
    {
        private readonly HostTools host;
        private readonly F5Interaction shell;
        private readonly ToolsPanel panel;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly List<ItemUiControl> controls=new List<ItemUiControl>();
        private readonly List<F5Element> elements=new List<F5Element>();
        private F5Rect view;
        private Matrix matrix;
        private float start=-1,scroll;
        private int generation=-1,skin=-1;
        private bool ready,previousLeft,leftTail,rightTail;
        private ItemUiControl armed;
        internal Action<string,F5Rect> HotkeyClicked;
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal float Height {get{return panel.Height;}}
        internal MiningPanel(HostTools host,F5Interaction shell){this.host=host;this.shell=shell;panel=new ToolsPanel(host,true);}
        internal void Prepare(bool active,Matrix transform,float bottom)
        {
            if(!active || !shell.Visible || shell.Page!=1){Suspend();return;}if(!renderer.Refresh()){ready=false;return;}
            PrepareLayout(transform,bottom);
        }
        internal void PrepareLayout(Matrix transform,float bottom)
        {
            var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);bool build=panel.NeedsBuild || start!=bottom || generation!=shell.Layout.Generation || skin!=renderer.Generation || view.Width!=next.Width;
            bool project=build || scroll!=shell.Scroll || view.X!=next.X || view.Y!=next.Y || matrix!=transform;
            matrix=transform;view=next;ready=true;
            if(build)panel.Build(bottom,view.Width,shell.Layout.TextSize);
            if(project){armed=null;controls.Clear();elements.Clear();panel.Project(view,shell.Scroll,controls,elements);}
            start=bottom;scroll=shell.Scroll;generation=shell.Layout.Generation;skin=renderer.Generation;
        }
        internal void ProcessInput(bool active,Vector2 point,bool geometryCurrent,bool focused,bool blocked)
        {
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            ConsumeLeft=leftTail;ConsumeRight=rightTail;OwnsPointer=false;
            if(!active || !focused || !shell.Visible || shell.Page!=1){Suspend();previousLeft=focused?left:true;if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}return;}
            bool current=ready && geometryCurrent && !blocked && generation==shell.Layout.Generation && scroll==shell.Scroll && !panel.NeedsBuild && view.X==shell.X+shell.Layout.Viewport.X && view.Y==shell.Y+shell.Layout.Viewport.Y && renderer.Refresh() && skin==renderer.Generation;
            ItemUiControl hit=null;
            if(current && view.Contains(point.X,point.Y))foreach(var c in controls)if(c.Rect.Contains(point.X,point.Y)){hit=c;break;}
            OwnsPointer=hit!=null || leftTail || rightTail;
            if(OwnsPointer){if(left)leftTail=true;if(right)rightTail=true;}
            if(left && !previousLeft)armed=hit;
            if(!current)armed=null;
            if(!left && previousLeft && armed!=null && ReferenceEquals(hit,armed) && hit.Enabled)
            {if(hit.Command==ItemUiCommand.Hotkey)HotkeyClicked?.Invoke(hit.Element.HotkeyTarget,hit.Rect);else panel.Execute(hit);}
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        internal void Draw(Action<F5Rect> keyboard)
        {
            if(!ready || !shell.Visible || shell.Page!=1)return;
            renderer.Pass(matrix,view,()=>{foreach(var e in elements)if(e.Kind==F5ElementKind.Panel)renderer.Panel(e.Rect);else renderer.Label(e);foreach(var c in controls){if(c.Command==ItemUiCommand.Hotkey)keyboard?.Invoke(c.Rect);else renderer.Button(c.Element,c.Selected,c.Enabled,c.Argument%10==0,false);}});
        }
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null){rect=default(F5Rect);return ready && view.Contains(x,y) && (!clip.HasValue || clip.Value.Contains(x,y))?panel.Hint(x,y,out rect):null;}
        internal void Suspend(){armed=null;ready=false;OwnsPointer=false;renderer.Dispose();}
    }
}
