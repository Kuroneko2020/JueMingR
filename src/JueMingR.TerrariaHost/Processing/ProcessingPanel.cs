using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.Processing
{
    // Stable rows: waiting, saving and failure reasons never change row height.
    internal sealed class ProcessingPanel
    {
        private readonly HostProcessing host;
        private readonly List<F5Element>[] rows={new List<F5Element>(),new List<F5Element>()};
        private readonly long[] revisions={-1,-1};
        private readonly bool[] available=new bool[2];
        private readonly List<F5Element> names=new List<F5Element>();
        internal float Height {get;private set;}
        internal ProcessingPanel(HostProcessing host){this.host=host;}
        internal bool NeedsBuild {get{for(int i=0;i<2;i++)if(revisions[i]!=host.Settings[i].Revision || available[i]!=host.Controls(i))return true;return false;}}
        internal void Execute(ItemUiControl control){host.Set(control.Argument/2,control.Argument%2!=0);}
        internal void Build(float start,float width,Func<string,float,F5Size> measure)
        {
            float y=start;
            for(int i=0;i<2;i++)
            {
                revisions[i]=host.Settings[i].Revision;available[i]=host.Controls(i);rows[i].Clear();
                new F5RowLayout(rows[i],measure).Row(ref y,0,width,HostProcessing.Names[i],new[]{"开启","关闭","键"},description:new F5RowDescription(HostProcessing.Actions[i],HostProcessing.Help[i]));
            }
            Height=y;
        }
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements)
        {
            names.Clear();
            for(int i=0;i<2;i++)foreach(var e in rows[i])
            {
                if(e.Rect.Bottom<=scroll || e.Rect.Y>=scroll+view.Height)continue;
                var rect=e.Rect.Offset(view.X,view.Y-scroll);
                var projected=new F5Element(e.Kind,rect,e.Text,e.TextSize,e.TextScale,e.Command,e.Kind==F5ElementKind.Hotkey?HostProcessing.Actions[i]:e.HotkeyTarget,e.Description,e.HintRect.Offset(view.X,view.Y-scroll));
                if(e.Kind==F5ElementKind.Button || e.Kind==F5ElementKind.Hotkey)
                    controls.Add(new ItemUiControl{Command=e.Kind==F5ElementKind.Hotkey?ItemUiCommand.Hotkey:ItemUiCommand.Processing,Argument=i*2+(e.Text=="开启"?1:0),Generation=unchecked((int)revisions[i]),Rect=rect,Element=projected,Enabled=available[i],Selected=e.Kind==F5ElementKind.Button && host.Settings[i].Value.Enabled==(e.Text=="开启")});
                else{elements.Add(projected);if(e.Description!=null)names.Add(projected);}
            }
        }
        internal string Hint(float x,float y,out F5Rect rect)
        {
            foreach(var e in names)if(e.HintRect.Contains(x,y)){rect=e.HintRect;int i=e.Description.Id==HostProcessing.Actions[0]?0:1;return host.Error??host.Settings[i].Message??e.Description.Text;}
            rect=default(F5Rect);return null;
        }
    }
}
