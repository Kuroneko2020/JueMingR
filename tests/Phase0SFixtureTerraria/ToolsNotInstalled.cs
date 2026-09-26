using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

// Old source-linked UI tests do not install tools. Native ToolsCpu/Visual
// load the real Host and locked executable; accidental use here must fail.
namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class HostTools {}
    internal sealed class ToolsPanel
    {
        private static Exception Missing(){return new InvalidOperationException("Use the real G09 native fixture.");}
        internal Action<F5Rect> Configure;
        internal ToolsPanel(HostTools host){throw Missing();}
        internal bool NeedsBuild {get{throw Missing();}}
        internal float Height {get{throw Missing();}}
        internal void Execute(ItemUiControl c){throw Missing();}
        internal void Build(float start,float width,Func<string,float,F5Size> measure){throw Missing();}
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements){throw Missing();}
        internal string Hint(float x,float y,out F5Rect rect){throw Missing();}
    }
    internal sealed class MiningPanel
    {internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null){throw new InvalidOperationException("Use the real G09 native fixture.");}}
    internal sealed class CaptureWindow
    {
        internal bool CanHint {get{throw new InvalidOperationException("Use the real G09 native fixture.");}}
        internal bool Contains(float x,float y){throw new InvalidOperationException("Use the real G09 native fixture.");}
        internal bool Visible {get{throw new InvalidOperationException("Use the real G09 native fixture.");}}
        internal string Hint(float x,float y,out F5Rect rect){throw new InvalidOperationException("Use the real G09 native fixture.");}
    }
}
