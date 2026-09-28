using System;
using JueMingR.TerrariaHost.F5;

// This old source-linked fixture does not install G10. FishingCpu/Visual
// execute the real Host and fixed game; using this placeholder must fail.
namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed class FishingPresentation
    {
        private static Exception Missing(){return new InvalidOperationException("Use the real G10 native fixture.");}
        internal bool Visible {get{throw Missing();}}
        internal bool CanHint {get{throw Missing();}}
        internal bool Contains(float x,float y){throw Missing();}
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null){throw Missing();}
    }
}
