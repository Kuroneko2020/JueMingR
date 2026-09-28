using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class PopupPlacementChecks
    {
        internal static void Run()
        {
            var anchor=new F5Rect(100,80,90,30);
            var below=F5PopupPlacement.Place(anchor,260,180,900,700);
            Check(below.X==anchor.X && below.Y==anchor.Bottom+6,"button lower/left alignment");
            anchor=new F5Rect(770,610,90,30);
            var above=F5PopupPlacement.Place(anchor,260,180,900,700);
            Check(above.Right==anchor.Right && above.Bottom==anchor.Y-6,"bottom/right edge flips above and aligns right");
            var shortView=F5PopupPlacement.Place(new F5Rect(270,90,80,30),300,196,360,220);
            Check(shortView.X>=12 && shortView.Right<=348 && shortView.Y==12 && shortView.Height==196,"short viewport preserves readable panel inside screen");
            var footprint=F5PopupPlacement.Place(anchor,340,212,600,220);
            Check(footprint.Y>=0 && footprint.Bottom<=220 && footprint.Height==212,"short footprint panel reduces margins without clipping content");
            Console.WriteLine("PASS button-anchored popup placement, edge flip and short viewport.");
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
