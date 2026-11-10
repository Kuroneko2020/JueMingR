using System;
using System.Collections;
using System.Reflection;
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
        {return PointInView(origin,velocity,logicalTarget,gravity,View.Capture());}
        internal static Vector2 PointInView(Vector2 origin,Vector2 velocity,Vector2 logicalTarget,float gravity,View view)
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
            float zoom=view.Zoom;
            if(float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom<=0 || view.Width<4 || view.Height<4)return logicalTarget;
            float cx=view.Width/2f,cy=view.Height/2f;
            float minX=cx+(1-cx)/zoom,maxX=cx+(view.Width-2-cx)/zoom;
            float minY=cy+(1-cy)/zoom,maxY=cy+(view.Height-2-cy)/zoom;
            var screen=origin-view.Position;
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
            var world=origin+mouse*(distance-1);var native=world-view.Position;
            if(gravity==-1)native.Y=view.Height-native.Y;
            // A continuous native rectangle is insufficient at zoom<1: e.g.
            // .5 maps integer raw X to a two-pixel native lattice. Choose an
            // actual integer raw pixel, then replay vanilla's float transform
            // and int truncation, on each axis. The signed half-cell encoding makes
            // CombatCursorScope reproduce those exact ints even at fractional
            // or large world screen positions and reversed gravity.
            int x=NativePixel(native.X,cx,view.Width,zoom),y=NativePixel(native.Y,cy,view.Height,zoom);
            // C# casts truncate towards zero, including negative native pixels
            // allowed by zoom<1. -476 must be encoded inside (-477,-476],
            // never as -475.5, which would select another (unreachable) pixel.
            float cellX=x+(x<0?-.5f:.5f),cellY=y+(y<0?-.5f:.5f);
            return view.Position+new Vector2(cellX,gravity==-1?view.Height-cellY:cellY);
        }
        internal readonly struct View
        {
            internal readonly Vector2 Position;internal readonly int Width,Height;internal readonly float Zoom;
            internal View(Vector2 position,int width,int height,float zoom){Position=position;Width=width;Height=height;Zoom=zoom;}
            internal static View Capture(){return new View(Main.screenPosition,Main.screenWidth,Main.screenHeight,Main.GameViewMatrix.RenderZoom.X);}
            internal View At(Vector2 position){return new View(position,Width,Height,Zoom);}
            internal bool Current{get{return Position==Main.screenPosition && Width==Main.screenWidth && Height==Main.screenHeight && Zoom==Main.GameViewMatrix.RenderZoom.X;}}
        }
        internal readonly struct ViewLease
        {
            private readonly Camera camera;private readonly bool present,acceptFirst;
            internal readonly bool Unknown,Complete;
            internal ViewLease(Camera camera,bool acceptFirst,bool unknown=false,bool complete=false){this.camera=camera;this.acceptFirst=acceptFirst;present=true;Unknown=unknown;Complete=complete && !unknown;}
            // Only the beam loop's normal window end may supply this fact.
            // Current view validity or Controlled membership is not completion.
            internal ViewLease CompleteWindow(){return new ViewLease(camera,acceptFirst,Unknown,true);}
            internal ViewLease UnknownTail(){return new ViewLease(camera,acceptFirst,true);}
            // CompletedWorld predicts the intervening Draw before the next
            // player. That exact normal Draw may occur before presentation.
            internal bool Current{get{return !present || camera.ConfigurationCurrent && (camera.InitialView.Current && camera.InitialOffset==new Vector2(Main.cameraX,Main.cameraY) || acceptFirst && camera.View.Current && camera.Offset==new Vector2(Main.cameraX,Main.cameraY));}}
        }
        internal struct Camera
        {
            private static readonly FieldInfo Lerp=typeof(Main).GetField("cameraLerp",BindingFlags.Static|BindingFlags.NonPublic);
            private static readonly FieldInfo NetTarget=typeof(Player).GetField("netCameraTarget",BindingFlags.Instance|BindingFlags.NonPublic);
            private static readonly FieldInfo Modifiers=typeof(Terraria.Graphics.CameraModifiers.CameraModifierStack).GetField("_modifiers",BindingFlags.Instance|BindingFlags.NonPublic);
            internal View View;internal readonly View InitialView;internal readonly Vector2 InitialOffset;private readonly Vector2 min,max,pan,translation;private readonly Rectangle area;private readonly bool clamp,supported,tracking;private readonly float lerp;private readonly ICollection rows;private readonly int count;private float x,y;
            internal Vector2 Offset{get{return new Vector2(x,y);}}
            internal Camera(Player player)
            {
                View=HostPrismSteering.View.Capture();InitialView=View;x=Main.cameraX;y=Main.cameraY;InitialOffset=new Vector2(x,y);clamp=!Terraria.Testing.DebugOptions.noLimits;pan=Main.CurrentPan;tracking=NetTarget?.GetValue(player)!=null;lerp=Lerp==null?float.NaN:(float)Lerp.GetValue(null);
                area=Terraria.WorldBuilding.WorldUtils.GetWorldPlayArea();translation=Main.GameViewMatrix.Translation;
                min=Utils.Round(area.TopLeft()-translation);max=Utils.Round(area.BottomRight()-(new Vector2(View.Width,View.Height)-translation*2)-translation);
                var stack=Main.instance?.CameraModifiers;rows=stack==null?null:Modifiers?.GetValue(stack) as ICollection;count=rows?.Count??0;
                // These observed branches change the camera beyond normal
                // focus/offset decay. Only their affected FUTURE contact tail
                // is unknown; the live cursor/first existing view is unchanged.
                supported=NetTarget!=null && lerp==0 && pan==Vector2.Zero && !tracking && (stack==null || rows!=null && count==0);
            }
            internal bool ConfigurationCurrent{get{
                if(pan!=Main.CurrentPan || NetTarget==null || tracking!=(NetTarget.GetValue(Main.LocalPlayer)!=null) || clamp==Terraria.Testing.DebugOptions.noLimits || area!=Terraria.WorldBuilding.WorldUtils.GetWorldPlayArea() || translation!=Main.GameViewMatrix.Translation || Lerp==null || lerp!=(float)Lerp.GetValue(null))return false;
                var stack=Main.instance?.CameraModifiers;var current=stack==null?null:Modifiers?.GetValue(stack) as ICollection;
                return ReferenceEquals(rows,current) && (current?.Count??0)==count;
            }}
            internal bool Next(Player completed)
            {
                if(!supported)return false;
                // Main.PlayerFocusedScreenPosition / DoDraw_UpdateCameraPosition
                // (1.4.5.8:63526/63549) run in Draw, after completed world, not
                // after Player.Update. Read Bottom + gfxOffY - 21, retain the
                // original camera offsets/decay, int-truncate, then the exact
                // WorldUtils play-area / Translation / ScaledSize clamp.
                x=completed.pulley?Approach(x,1):0;y=Approach(y,2);
                var position=completed.Bottom+new Vector2(x,completed.gfxOffY-21+y)-new Vector2(View.Width,View.Height)*.5f;
                if(!Finite(position))return false;
                position=new Vector2((int)position.X,(int)position.Y);if(clamp)position=Vector2.Clamp(position,min,max);
                View=View.At(position);return true;
            }
            private static float Approach(float value,float amount){return value>0?Math.Max(0,value-amount):Math.Min(0,value+amount);}
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
