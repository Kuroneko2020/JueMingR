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
        internal ToolsPanel(HostTools host,bool mining=false){this.host=host;this.mining=mining;}
        internal bool NeedsBuild {get{for(int i=mining?2:0;i<(mining?3:2);i++)if(revisions[i]!=host.Settings[i].Revision || available[i]!=host.Controls(i))return true;return false;}}
        internal void Execute(ItemUiControl control){int domain=control.Argument/10,command=control.Argument%10;if(domain==0 && command==3)Configure?.Invoke(control.Rect);else host.Set(domain,command);}
        internal void Build(float start,float width,Func<string,float,F5Size> measure)
        {
            float y=start;
            for(int i=mining?2:0;i<(mining?3:2);i++)
            {
                revisions[i]=host.Settings[i].Revision;available[i]=host.Controls(i);rows[i].Clear();
                string help=i==0?"自动选择主背包虫网；手持模式必须手持虫网。Boss 战时暂停，保留模式。":i==1?"携带再生法杖或再生之斧自动收获/种植。仅花盆和种植盆，优先免费复种。":"当前镐或电钻接管已选矿脉；绿色为当前可产生进度，红色为暂不可挖或未知。";
                new F5RowLayout(rows[i],measure).Row(ref y,0,width,HostTools.Names[i],i==0?new[]{"配置","自动","手持","关闭","键"}:i==1?new[]{"开启","关闭","键"}:new[]{"快捷键","自动","关闭","键"},description:new F5RowDescription(HostTools.Actions[i],help));
                if(i==2)new F5RowLayout(rows[i],measure).Row(ref y,0,width,"选择挖矿区域",new[]{"键"},description:new F5RowDescription(HostTools.SelectAction,"光标指到矿物上按快捷键选中挖矿区域；无效目标会保留原区域。"));
            }
            Height=y;
        }
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements)
        {
            projected.Clear();
            for(int i=mining?2:0;i<(mining?3:2);i++)
            {
                int hotkeys=0;
                foreach(var e in rows[i])
                {
                    string action=e.Kind==F5ElementKind.Hotkey && hotkeys++>0?HostTools.SelectAction:HostTools.Actions[i];
                    if(e.Rect.Bottom<=scroll || e.Rect.Y>=scroll+view.Height)continue;
                    var rect=e.Rect.Offset(view.X,view.Y-scroll);var display=new F5Element(e.Kind,rect,e.Text,e.TextSize,e.TextScale,e.Command,e.Kind==F5ElementKind.Hotkey?action:null,e.Description,e.HintRect.Offset(view.X,view.Y-scroll));projected.Add(display);
                    if(e.Kind==F5ElementKind.Button || e.Kind==F5ElementKind.Hotkey)
                    {
                        int mode=e.Text=="配置"?3:e.Text=="关闭"?0:e.Text=="手持" || i==2 && e.Text=="自动"?2:1;
                        controls.Add(new ItemUiControl{Command=e.Kind==F5ElementKind.Hotkey?ItemUiCommand.Hotkey:ItemUiCommand.Tools,Argument=i*10+mode,Rect=rect,Element=display,Enabled=available[i],Selected=e.Kind==F5ElementKind.Button && mode<3 && host.Settings[i].Value.Mode==mode});
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
                if(e.Kind==F5ElementKind.Hotkey)return "双击设置快捷键";
                if(e.Description!=null){int domain=e.Description.Id==HostTools.Actions[0]?0:e.Description.Id==HostTools.Actions[1]?1:2;return host.Error??host.Settings[domain].Message??e.Description.Text;}
                if(e.Kind==F5ElementKind.Button)
                {
                    if(e.Text=="配置"){int disabled=0;for(int i=0;i<8;i++)if((host.Settings[0].Value.Categories&(1<<i))==0)disabled++;return disabled==0?"选择允许主动捕捉的分类":disabled+" 个捕捉分类已关闭";}
                    if(mining)return e.Text=="快捷键"?"光标指到矿物上按快捷键选中挖矿区域":e.Text=="自动"?"挖下第一个矿物开始接管":"关闭自动挖矿";
                    if(e.Text=="手持")return "必须手持虫网";if(e.Text=="自动")return "身上带着虫网就行";
                }
            }
            rect=default(F5Rect);return null;
        }
    }
}
