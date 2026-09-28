using System;

namespace JueMingR.TerrariaHost.F5
{
    internal static class F5PopupPlacement
    {
        // All coordinates are logical UI coordinates. Prefer the triggering
        // button's lower/left edge, flip above or align right when necessary.
        // A viewport too short for either side retains the readable body and
        // clamps the whole panel instead of discarding controls or text.
        internal static F5Rect Place(F5Rect anchor,float width,float height,float screenWidth,float screenHeight)
        {
            const float gap=6;
            float marginX=Math.Min(12,Math.Max(0,(screenWidth-width)/2)),marginY=Math.Min(12,Math.Max(0,(screenHeight-height)/2));
            float right=Math.Max(marginX,screenWidth-marginX-width),bottom=Math.Max(marginY,screenHeight-marginY-height);
            float x=anchor.X;if(x>right)x=anchor.Right-width;
            float y=anchor.Bottom+gap;
            if(y>bottom)y=anchor.Y-gap-height;
            return new F5Rect(Math.Max(marginX,Math.Min(right,x)),Math.Max(marginY,Math.Min(bottom,y)),width,height);
        }
        internal static bool SameAnchor(F5Rect a,F5Rect b)
        {return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
    }
}
