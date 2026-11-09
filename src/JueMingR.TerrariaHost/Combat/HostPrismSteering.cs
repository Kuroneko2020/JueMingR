using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // 633 is an instantaneous beam with a damped native direction, not a
    // projectile travelling towards a permanently fixed future mouse point.
    // This stateless O(1) transform owns only this action's cursor direction.
    internal static class HostPrismSteering
    {
        private const float OldWeight=.92f,MouseWeight=.08f;
        // The ray is also clipped INSIDE the real client viewport below. Its
        // distance has no damage role; never invent off-client input precision.
        private const float CursorDistance=1024f;
        internal static Vector2 Point(Vector2 origin,Vector2 velocity,Vector2 logicalTarget,float gravity)
        {
            Vector2 delta=logicalTarget-origin;
            if(!Finite(delta) || !Finite(velocity) || delta.LengthSquared()<.000001f || velocity.LengthSquared()<.000001f)return logicalTarget;
            Vector2 desired=Vector2.Normalize(delta),old=Vector2.Normalize(velocity);
            float cross=old.X*desired.Y-old.Y*desired.X,dot=Vector2.Dot(old,desired),ratio=MouseWeight/OldWeight;
            Vector2 mouse;
            if(dot>0 && Math.Abs(cross)<=ratio)
            {
                // Solve oldWeight*old + mouseWeight*mouse = length*desired,
                // with |mouse|=1. The larger positive root is the stable,
                // forward-facing input, including an already aligned beam.
                float length=OldWeight*dot+(float)Math.Sqrt(Math.Max(0,MouseWeight*MouseWeight-OldWeight*OldWeight*cross*cross));
                mouse=(desired*length-old*OldWeight)/MouseWeight;
            }
            else
            {
                // No cursor can turn native 633 more than asin(.08/.92) in
                // one AI. Choose that legal tangent, never overwrite velocity.
                // An exact 180-degree tie always turns counter-clockwise;
                // there is no cross-frame accumulator to wind up or oscillate.
                float sign=cross<0?-1:1;
                mouse=-old*ratio+new Vector2(-old.Y,old.X)*(sign*(float)Math.Sqrt(1-ratio*ratio));
            }
            // SetZoom_MouseInWorld maps raw client pixels around the centre
            // by 1/RenderZoom.X, then truncates. At zoom>1 the reachable native
            // rectangle is smaller than screenWidth/Height. Use an interior
            // margin so the following int/gravity mapping stays reachable.
            float zoom=Main.GameViewMatrix.RenderZoom.X;
            if(float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom<=0 || Main.screenWidth<4 || Main.screenHeight<4)return logicalTarget;
            float cx=Main.screenWidth/2f,cy=Main.screenHeight/2f;
            float minX=cx+(1-cx)/zoom,maxX=cx+(Main.screenWidth-2-cx)/zoom;
            float minY=cy+(1-cy)/zoom,maxY=cy+(Main.screenHeight-2-cy)/zoom;
            var screen=origin-Main.screenPosition;
            if(screen.X<minX || screen.Y<minY || screen.X>maxX || screen.Y>maxY)return logicalTarget;
            float distance=CursorDistance;
            if(mouse.X>0)distance=Math.Min(distance,(maxX-screen.X)/mouse.X);
            else if(mouse.X<0)distance=Math.Min(distance,(minX-screen.X)/mouse.X);
            if(mouse.Y>0)distance=Math.Min(distance,(maxY-screen.Y)/mouse.Y);
            else if(mouse.Y<0)distance=Math.Min(distance,(minY-screen.Y)/mouse.Y);
            // An off-screen or edge holder has no sufficiently resolved ray
            // here. Retain the original accepted logical point rather than
            // manufacturing an unreachable compensating mouse coordinate.
            if(distance<16)return logicalTarget;
            var world=origin+mouse*(distance-1);var native=world-Main.screenPosition;
            if(gravity==-1)native.Y=Main.screenHeight-native.Y;
            // A continuous native rectangle is insufficient at zoom<1: e.g.
            // .5 maps integer raw X to a two-pixel native lattice. Choose an
            // actual integer raw pixel, then replay vanilla's float transform
            // and int truncation, on each axis. The half-cell encoding makes
            // CombatCursorScope reproduce those exact ints even at fractional
            // or large world screen positions and reversed gravity.
            int x=NativePixel(native.X,cx,Main.screenWidth,zoom),y=NativePixel(native.Y,cy,Main.screenHeight,zoom);
            return Main.screenPosition+new Vector2(x+.5f,gravity==-1?Main.screenHeight-(y+.5f):y+.5f);
        }
        private static int NativePixel(float desired,float centre,int extent,float zoom)
        {
            int raw=Math.Max(0,Math.Min(extent-1,(int)Math.Round(centre+(desired-centre)*zoom)));
            return (int)(centre+(raw-centre)*(1f/zoom));
        }
        private static bool Finite(Vector2 value)
        {return !float.IsNaN(value.X) && !float.IsInfinity(value.X) && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);}
    }
}
