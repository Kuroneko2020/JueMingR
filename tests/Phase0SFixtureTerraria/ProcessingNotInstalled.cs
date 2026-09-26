using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

// The old source-linked UI fixture does not install G08. Fail every attempted
// use: ProcessingCpu/Visual load the actual production Host and locked EXE.
namespace JueMingR.TerrariaHost.Processing
{
    internal sealed class HostProcessing {}
    internal sealed class ProcessingPanel
    {
        private static Exception Missing(){return new InvalidOperationException("Use the real G08 native fixture.");}
        internal ProcessingPanel(HostProcessing host){throw Missing();}
        internal bool NeedsBuild {get{throw Missing();}}
        internal float Height {get{throw Missing();}}
        internal void Execute(ItemUiControl control){throw Missing();}
        internal void Build(float start,float width,Func<string,float,F5Size> measure){throw Missing();}
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements){throw Missing();}
        internal string Hint(float x,float y,out F5Rect rect){throw Missing();}
    }
    internal sealed class ReforgePanel
    {
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {throw new InvalidOperationException("Use the real G08 native fixture.");}
    }
}
