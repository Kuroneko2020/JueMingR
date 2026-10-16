using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private Point? sharpTearsView;
        internal void CaptureSharpTearsView(){if(!sharpTearsView.HasValue)sharpTearsView=Main.MaxWorldViewSize;}
        private bool SharpTearsInputsCurrent {get{return !sharpTearsView.HasValue || sharpTearsView.Value==Main.MaxWorldViewSize;}}
    }
}
