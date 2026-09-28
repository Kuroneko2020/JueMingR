using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // Only the migrated presentation blocks live here. Their settings, hotkey
    // identities and update order still belong to the original business owners.
    internal sealed class MiscAutomationPanel
    {
        private readonly F5Interaction shell;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly List<ItemUiControl> controls=new List<ItemUiControl>();
        private readonly List<F5Element> elements=new List<F5Element>();
        internal Tools.ToolsPanel ToolsPanel {get;private set;}
        internal CoinDeposit.CoinPanel CoinPanel {get;private set;}
        internal Action<string,F5Rect> HotkeyClicked;
        private F5Rect view;
        private Matrix matrix;
        private Vector2 pointer;
        private float start=-1,scroll;
        private int generation=-1,skin=-1;
        private bool ready,dirty=true,previousLeft,leftTail,rightTail;
        private ItemUiControl armed;
        internal float Height {get;private set;}
        internal int LayoutBuildCount {get;private set;}
        internal F5Rect ConfigurationAnchor {get{return ToolsPanel==null?default(F5Rect):ToolsPanel.ConfigurationAnchor(view,scroll);}}
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool NeedsBuild {get{return dirty || ToolsPanel!=null && ToolsPanel.NeedsBuild || CoinPanel!=null && CoinPanel.NeedsBuild;}}
        internal MiscAutomationPanel(F5Interaction shell){this.shell=shell;}
        internal void AttachTools(Tools.HostTools host,Action<F5Rect> configure){ToolsPanel=new Tools.ToolsPanel(host){Configure=configure};dirty=true;}
        internal void AttachCoins(CoinDeposit.HostCoinDeposit host){CoinPanel=new CoinDeposit.CoinPanel(host);dirty=true;}
        internal void Prepare(bool active,Matrix transform,float bottom)
        {
            if(!active || !shell.Visible || shell.Page!=1){Suspend();return;}
            if(!renderer.Refresh()){ready=false;return;}
            PrepareLayout(transform,bottom);
        }
        internal void PrepareLayout(Matrix transform,float bottom)
        {
            var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);
            bool build=NeedsBuild || start!=bottom || generation!=shell.Layout.Generation || skin!=renderer.Generation || view.Width!=next.Width;
            bool project=build || scroll!=shell.Scroll || view.X!=next.X || view.Y!=next.Y || matrix!=transform || !ready;
            matrix=transform;view=next;ready=true;
            if(build)
            {
                ToolsPanel?.Build(bottom,view.Width,shell.Layout.TextSize);
                float y=ToolsPanel==null?bottom:ToolsPanel.Height;
                CoinPanel?.Build(y,view.Width,shell.Layout.TextSize);
                Height=CoinPanel==null?y:CoinPanel.Height;dirty=false;LayoutBuildCount++;
            }
            if(project)
            {
                armed=null;controls.Clear();elements.Clear();
                ToolsPanel?.Project(view,shell.Scroll,controls,elements);
                CoinPanel?.Project(view,shell.Scroll,controls,elements);
            }
            start=bottom;scroll=shell.Scroll;generation=shell.Layout.Generation;skin=renderer.Generation;
        }
        internal void ProcessInput(bool active,KeyboardState keys,Vector2 point,bool geometryCurrent,bool focused,bool blocked)
        {
            pointer=point;
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            ConsumeLeft=leftTail;ConsumeRight=rightTail;OwnsPointer=false;
            if(!active || !focused || !shell.Visible || shell.Page!=1)
            {Suspend();previousLeft=focused?left:true;if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}return;}
            bool current=ready && geometryCurrent && !blocked && !NeedsBuild && generation==shell.Layout.Generation && scroll==shell.Scroll &&
                view.X==shell.X+shell.Layout.Viewport.X && view.Y==shell.Y+shell.Layout.Viewport.Y;
            ItemUiControl hit=null;
            if(current && view.Contains(point.X,point.Y))foreach(var c in controls)if(c.Enabled && c.Rect.Contains(point.X,point.Y)){hit=c;break;}
            OwnsPointer=hit!=null || leftTail || rightTail;
            if(OwnsPointer){if(left)leftTail=true;if(right)rightTail=true;}
            if(left && !previousLeft)armed=hit;
            if(!current)armed=null;
            if(!left && previousLeft && armed!=null && ReferenceEquals(hit,armed))
            {
                if(hit.Command==ItemUiCommand.Hotkey)HotkeyClicked?.Invoke(hit.Element.HotkeyTarget,hit.Rect);
                else {if(hit.Command==ItemUiCommand.Tools)ToolsPanel?.Execute(hit);else if(hit.Command==ItemUiCommand.Coin)CoinPanel?.Execute(hit);dirty=true;}
            }
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=null;}if(!right)rightTail=false;previousLeft=left;
        }
        internal void Draw(Action<F5Rect> keyboard,bool allowHints=true)
        {
            if(!ready || !shell.Visible || shell.Page!=1)return;
            renderer.Pass(matrix,view,()=>
            {
                foreach(var e in elements)if(e.Kind==F5ElementKind.Panel)renderer.Panel(e.Rect);else renderer.Label(e);
                foreach(var c in controls)if(c.Command==ItemUiCommand.Hotkey)keyboard?.Invoke(c.Rect);else renderer.Button(c.Element,c.Selected,c.Enabled,c.Element.Text=="关闭",allowHints && view.Contains(pointer.X,pointer.Y) && c.Rect.Contains(pointer.X,pointer.Y));
            });
        }
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {
            rect=default(F5Rect);
            if(!ready || NeedsBuild || generation!=shell.Layout.Generation || scroll!=shell.Scroll || !view.Contains(x,y) || clip.HasValue && !clip.Value.Contains(x,y))return null;
            foreach(var c in controls)if(c.Command==ItemUiCommand.Hotkey && c.Rect.Contains(x,y)){rect=c.Rect;return "双击设置快捷键";}
            string hint=ToolsPanel?.Hint(x,y,out rect);
            return hint??CoinPanel?.Hint(x,y,out rect);
        }
        internal void Suspend(){armed=null;ready=false;OwnsPointer=false;renderer.Dispose();}
    }
}
