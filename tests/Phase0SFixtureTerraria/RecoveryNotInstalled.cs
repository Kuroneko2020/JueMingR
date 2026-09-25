using System;
using JueMingR.TerrariaHost.F5;
// The old source-linked UI fixture has no recovery native adapter. Never turn
// absence into fake success; real checks load the fixed EXE and production Host.
namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class RecoveryPresentation
    {
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {throw new InvalidOperationException("Use the real G07 native fixture.");}
    }
}
