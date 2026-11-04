using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class ToolsPanel
    {
        private readonly HostTools host;
        private readonly bool mining;
        private readonly List<F5Element>[] rows={new List<F5Element>(),new List<F5Element>(),new List<F5Element>()};
        private readonly List<F5Element> projected=new List<F5Element>();
        private readonly long[] revisions={-1,-1,-1};
        private readonly bool[] available=new bool[3];
        internal Action<F5Rect> Configure;
        internal float Height {get;private set;}
        internal F5Rect ConfigurationAnchor(F5Rect view,float scroll)
        {foreach(var e in rows[0])if(e.Text=="配置")return e.Rect.Offset(view.X,view.Y-scroll);return default(F5Rect);}
        internal ToolsPanel(HostTools host,bool mining=false){this.host=host;this.mining=mining;}
        internal bool NeedsBuild {get{for(int i=mining?2:0;i<(mining?3:2);i++)if(revisions[i]!=host.Settings[i].Revision || available[i]!=host.Controls(i))return true;return false;}}
        internal void Execute(ItemUiControl control){int domain=control.Argument/10,command=control.Argument%10;if(domain==0 && command==3)Configure?.Invoke(control.Rect);else host.Set(domain,command);}
        internal void Build(float start,float width,Func<string,float,F5Size> measure,float fieldWidth=120)
        {
            float y=start;
            for(int i=mining?2:0;i<(mining?3:2);i++)
            {
                revisions[i]=host.Settings[i].Revision;available[i]=host.Controls(i);rows[i].Clear();
                string help=i==0?"附近有小动物时自动挥网捕捉":i==1?"携带再生法杖自动收获草药":"自动连续挖掘矿脉";
                var description=new F5RowDescription(HostTools.Actions[i],help);
                if(i==2)F5PairedRowLayout.Row(rows[i],measure,ref y,width,HostTools.Names[i],"快捷键",true,new[]{"自动","关闭","键"},description,fieldWidth);
                else new F5RowLayout(rows[i],measure).Row(ref y,0,width,HostTools.Names[i],i==0?new[]{"配置","自动","手持","关闭","键"}:new[]{"开启","关闭","键"},description:description);
                // The select binding belongs beside its mode; the final
                // binding still toggles/restores the feature. Carry identity
                // in layout metadata so clipping cannot exchange the actions.
                for(int n=0;n<rows[i].Count;n++)
                {
                    var e=rows[i][n];
                    if(e.Kind==F5ElementKind.Hotkey || e.Kind==F5ElementKind.Field)rows[i][n]=new F5Element(e.Kind,e.Rect,e.Text,e.TextSize,e.TextScale,e.Command,e.Kind==F5ElementKind.Field?HostTools.SelectAction:HostTools.Actions[i]);
                }
            }
            Height=y;
        }
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements)
        {
            projected.Clear();
            for(int i=mining?2:0;i<(mining?3:2);i++)
            {
                foreach(var e in rows[i])
                {
                    if(e.Rect.Bottom<=scroll || e.Rect.Y>=scroll+view.Height)continue;
                    var rect=e.Rect.Offset(view.X,view.Y-scroll);var display=new F5Element(e.Kind,rect,e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(view.X,view.Y-scroll));projected.Add(display);
                    if(e.Kind==F5ElementKind.Button || e.HotkeyTarget!=null)
                    {
                        int mode=e.Text=="配置"?3:e.Text=="关闭"?0:e.Text=="手持" || i==2 && e.Text=="自动"?2:1;
                        controls.Add(new ItemUiControl{Command=e.HotkeyTarget!=null?ItemUiCommand.Hotkey:ItemUiCommand.Tools,Argument=i*10+mode,Rect=rect,Element=display,Enabled=available[i],Selected=e.Kind==F5ElementKind.Button && mode<3 && host.Settings[i].Value.Mode==mode});
                    }
                    else elements.Add(display);
                }
            }
        }
        internal string Hint(float x,float y,out F5Rect rect)
        {
            foreach(var e in projected)
            {
                if(!(e.Description!=null?e.HintRect:e.Rect).Contains(x,y))continue;rect=e.Description!=null?e.HintRect:e.Rect;
                if(e.HotkeyTarget!=null)return e.HotkeyTarget==HostTools.SelectAction?F5HotkeyHints.MiningSelection:F5HotkeyHints.Toggle;
                if(e.Description!=null){int domain=e.Description.Id==HostTools.Actions[0]?0:e.Description.Id==HostTools.Actions[1]?1:2;return host.Error??host.Settings[domain].Message??e.Description.Text;}
                if(e.Kind==F5ElementKind.Button)
                {
                    if(e.Text=="配置"){int disabled=0;for(int i=0;i<8;i++)if((host.Settings[0].Value.Categories&(1<<i))==0)disabled++;return disabled==0?"选择允许主动捕捉的分类":disabled+" 个捕捉分类已关闭";}
                    if(mining)return e.Text=="快捷键"?"光标指到矿物上按快捷键选中挖矿区域":e.Text=="自动"?"挖下第一个矿物开始接管":null;
                    if(e.Text=="手持")return "必须手持虫网";if(e.Text=="自动")return "身上带着虫网就行";
                }
            }
            rect=default(F5Rect);return null;
        }
    }
}
