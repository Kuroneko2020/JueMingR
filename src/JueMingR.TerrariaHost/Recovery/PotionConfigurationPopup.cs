using System;
using System.Collections.Generic;
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
    internal sealed class PotionConfigurationPopup
    {
        // Separate overlay ownership: keep the complete physical gesture after
        // closing, and reject releases prepared for another layout or setting.
        private readonly HostRecovery host;
        private readonly ItemsRenderer renderer=new ItemsRenderer();
        private readonly List<F5Rect> cells=new List<F5Rect>();
        private readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        private F5Rect body,close,anchor;
        private Matrix matrix;
        private Vector2 screen,pointer;
        private int feature=-1,armed=-1,rows,columns,firstRow,skin;
        private long revision,session;
        private bool previousLeft,leftTail,rightTail,previousEscape;
        internal bool Visible {get{return feature>=0;}}
        internal int Feature {get{return feature;}}
        internal int[] Candidates {get;private set;}=new int[0];
        internal F5Rect Panel {get;private set;}
        internal bool OwnsPointer {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeRight {get;private set;}
        internal bool ConsumeWheel {get;private set;}
        internal Action Opened;
        internal PotionConfigurationPopup(HostRecovery host){this.host=host;}
        internal void Open(int target,F5Rect button)
        {
            bool same=Visible && feature==target;Close();if(same)return;
            feature=target;anchor=button;Candidates=host.Catalog.Get(target==0);firstRow=0;session=host.Runtime.Generation;Opened?.Invoke();
        }
        internal void Close(){feature=-1;armed=-1;cells.Clear();icons.Clear();renderer.Dispose();}
        internal bool Contains(float x,float y){return Visible && Panel.Contains(x,y);}
        internal bool Captured {get{return armed!=-1 || leftTail || rightTail;}}
        internal void Prepare(Matrix transform,Vector2 physical,F5Rect button,bool resources)
        {
            if(!Visible)return;
            if(resources && !renderer.Refresh())return;
            Vector2 logical=physical/transform.M11;float width=Math.Min(352,logical.X-24),maxHeight=Math.Min(400,logical.Y-24);
            if(width<100 || maxHeight<72+ItemsLayout.CandidateSize+ItemsLayout.Gap){Close();return;}
            int cols=ItemsLayout.Columns(width,ItemsLayout.CandidateWidth),visibleRows=Math.Max(1,(int)((maxHeight-72)/(ItemsLayout.CandidateSize+ItemsLayout.Gap)));
            int totalRows=(Candidates.Length+cols-1)/cols;visibleRows=Math.Min(visibleRows,Math.Max(1,totalRows));
            firstRow=Math.Min(firstRow,Math.Max(0,totalRows-visibleRows));
            float height=72+visibleRows*(ItemsLayout.CandidateSize+ItemsLayout.Gap),x=button.Right+8;
            if(x+width>logical.X-12)x=button.X-width-8;
            x=Math.Max(12,Math.Min(logical.X-width-12,x));float y=Math.Max(12,Math.Min(logical.Y-height-12,button.Y));
            var panel=new F5Rect((float)Math.Floor(x),(float)Math.Floor(y),width,height);
            if(screen!=physical || matrix!=transform || anchor.X!=button.X || anchor.Y!=button.Y || Panel.X!=panel.X || Panel.Y!=panel.Y || skin!=renderer.Generation || revision!=host.Potions.Revision)armed=-1;
            Panel=panel;screen=physical;matrix=transform;anchor=button;skin=renderer.Generation;revision=host.Potions.Revision;rows=visibleRows;columns=cols;
            body=new F5Rect(x+4,y+64,width-8,height-68);close=new F5Rect(x+width-40,y+8,30,30);cells.Clear();
            for(int i=firstRow*cols;i<Candidates.Length && i<(firstRow+visibleRows)*cols;i++)
            {
                cells.Add(new F5Rect(x+8+(i%cols)*(ItemsLayout.CandidateWidth+ItemsLayout.Gap),body.Y+(i/cols-firstRow)*(ItemsLayout.CandidateSize+ItemsLayout.Gap),ItemsLayout.CandidateWidth,ItemsLayout.CandidateSize));
                int type=Candidates[i];if(resources && !icons.ContainsKey(type)){Main.instance.LoadItem(type);icons[type]=TextureAssets.Item[type]?.Value;}
            }
        }
        internal void Process(bool active,KeyboardState keys,Vector2 point,Matrix transform,Vector2 physical,bool focused,int wheel)
        {
            pointer=point;OwnsPointer=ConsumeWheel=false;ConsumeLeft=leftTail;ConsumeRight=rightTail;
            bool left=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed,right=PlayerInput.MouseInfo.RightButton==ButtonState.Pressed,escape=keys.IsKeyDown(Keys.Escape);
            if(!active || !focused || session!=host.Runtime.Generation){Close();previousLeft=focused?left:true;previousEscape=focused?escape:true;if(focused){if(!left)leftTail=false;if(!right)rightTail=false;}return;}
            if(!Visible){previousLeft=left;previousEscape=escape;if(!left)leftTail=false;if(!right)rightTail=false;return;}
            bool current=matrix==transform && screen==physical && revision==host.Potions.Revision;
            if(icons.Count>0 && (!renderer.Refresh() || renderer.Generation!=skin))current=false;
            OwnsPointer=Captured || Contains(point.X,point.Y);
            if(escape && !previousEscape){host.Input.Hotkeys.SuppressKey((int)Keys.Escape);host.Input.ConsumeHotkeyActions();Close();}
            previousEscape=escape;
            int hit=-1;if(current){if(close.Contains(point.X,point.Y))hit=-2;else for(int i=0;i<cells.Count;i++)if(cells[i].Contains(point.X,point.Y)){hit=firstRow*columns+i;break;}}
            if(left && !previousLeft)armed=hit;
            if(!current)armed=-1;
            if(OwnsPointer){if(left)leftTail=true;if(right)rightTail=true;for(int key=256;key<=260;key++)if(host.Input.Hotkeys.IsNew(key))host.Input.Hotkeys.SuppressKey(key);}
            if(Visible && !left && previousLeft && armed==hit && hit!=-1)
            {
                if(hit==-2)Close();
                else if(host.Controls(feature)){int type=Candidates[hit];bool allowed=feature==0?host.Potions.Value.LifeAllowed(type):host.Potions.Value.ManaAllowed(type);host.Potions.Set(host.Potions.Value.ChangeType(feature,type,allowed));}
            }
            if(Visible && Contains(point.X,point.Y) && wheel!=0){firstRow=Math.Max(0,Math.Min(Math.Max(0,(Candidates.Length+columns-1)/columns-rows),firstRow+(wheel<0?1:-1)));armed=-1;ConsumeWheel=true;}
            ConsumeLeft=leftTail;ConsumeRight=rightTail;if(!left){leftTail=false;armed=-1;}if(!right)rightTail=false;previousLeft=left;
        }
        internal string Hint(float x,float y,out F5Rect rect)
        {rect=default(F5Rect);if(!Visible)return null;for(int i=0;i<cells.Count;i++)if(cells[i].Contains(x,y)){rect=cells[i];return host.Catalog.Describe(Candidates[firstRow*columns+i],feature);}return null;}
        internal void Draw()
        {
            if(!Visible)return;
            renderer.Pass(matrix,Panel,()=>{renderer.Panel(Panel);renderer.Text(feature==0?"自动回血 · 配置":"自动回蓝 · 配置",new F5Rect(Panel.X+8,Panel.Y+8,Panel.Width-52,30),Color.White,.7f);renderer.Text("已勾选的药品允许自动使用",new F5Rect(Panel.X+8,Panel.Y+40,Panel.Width-16,20),Color.White,.6f);renderer.Text("×",close,Color.White,.75f);});
            renderer.Pass(matrix,body,()=>{for(int i=0;i<cells.Count;i++){int type=Candidates[firstRow*columns+i];var rect=cells[i];renderer.ItemButton(rect,host.Controls(feature),rect.Contains(pointer.X,pointer.Y));Texture2D icon;if(icons.TryGetValue(type,out icon))renderer.PreparedItem(type,icon,ItemsLayout.IconBounds(rect,true));if(feature==0?host.Potions.Value.LifeAllowed(type):host.Potions.Value.ManaAllowed(type))renderer.Selection(rect);}});
        }
    }
}
