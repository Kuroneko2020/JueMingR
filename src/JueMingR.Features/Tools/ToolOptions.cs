using System;

namespace JueMingR.Features.Tools
{
    public enum CaptureMode { Off, Auto, Held }
    public enum MiningMode { Off, Hotkey, Auto }
    // Domain 0 capture, 1 herbs, 2 mining. Each domain has its own document;
    // copies preserve the last nonzero mode even while the feature is off.
    public sealed class ToolOptions
    {
        public int Mode {get;}
        public int LastMode {get;}
        public int Categories {get;}
        public ToolOptions(int mode=0,int lastMode=1,int categories=255)
        {
            if(mode<0 || mode>2 || lastMode<1 || lastMode>2 || categories<0 || categories>255)throw new ArgumentOutOfRangeException();
            Mode=mode;LastMode=mode==0?lastMode:mode;Categories=categories;
        }
        public ToolOptions WithMode(int mode){return new ToolOptions(mode,LastMode,Categories);}
        public ToolOptions Toggle(){return WithMode(Mode==0?LastMode:0);}
        public ToolOptions WithCategories(int categories){return new ToolOptions(Mode,LastMode,categories);}
    }
}
