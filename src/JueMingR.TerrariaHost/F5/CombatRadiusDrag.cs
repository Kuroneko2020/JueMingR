using System;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // Presentation owns a draft until a real focused release. Cancellation
    // keeps the physical tail, so closing F5 cannot turn it into an attack.
    internal sealed class CombatRadiusDrag
    {
        private readonly ICombatObservationControls host;
        private readonly F5Interaction state;
        private bool tail;
        private int generation,skin,start;
        private object font;
        private Matrix matrix;
        private F5Rect capturedTrack;
        internal bool Captured {get;private set;}
        internal bool ConsumeLeft {get;private set;}
        internal bool ConsumeWheel {get;private set;}
        internal bool OwnsPointer {get{return Captured || tail || ConsumeLeft;}}
        internal int Draft {get;private set;}
        internal int Value {get{return Captured?Draft:host.Options.Radius;}}
        internal bool Available {get{return host.CanConfigure && host.Options.Aim && host.Options.MouseCenter;}}
        internal F5Rect Track {get;private set;}
        internal CombatRadiusDrag(ICombatObservationControls host,F5Interaction state){this.host=host;this.state=state;}
        internal static F5Rect TrackIn(F5Rect field,float labelWidth){return new F5Rect(field.X+labelWidth+16,field.Y+field.Height/2-7,Math.Max(20,field.Width-labelWidth-24),14);}
        internal void Process(bool active,bool focused,bool left,bool newLeft,bool cancel,float x,float y,Matrix projection,object fontIdentity,int skinGeneration)
        {
            ConsumeLeft=tail;ConsumeWheel=Captured;
            F5Rect track=default(F5Rect),view=state.Layout.Viewport.Offset(state.X,state.Y);
            if(state.Visible && state.Page==8)
                foreach(var element in state.Layout.Elements)if(element.Command==F5Command.ObservationRadius)
                {track=TrackIn(element.Rect.Offset(view.X,view.Y-state.Scroll),element.TextSize.Width);break;}
            Track=track;
            bool valid=active && focused && state.Visible && state.Page==8 && Available && track.Width>0 && !cancel;
            if(Captured && (!valid || generation!=state.Layout.Generation || skin!=skinGeneration || !ReferenceEquals(font,fontIdentity) || matrix!=projection || !Same(track,capturedTrack)))Cancel();
            if(!valid){if(Captured)Cancel();}
            else if(!Captured && !tail && newLeft && left && track.Contains(x,y) && view.Contains(x,y))
            {
                Captured=tail=true;ConsumeLeft=ConsumeWheel=true;start=host.Options.Radius;Draft=start;
                generation=state.Layout.Generation;skin=skinGeneration;font=fontIdentity;matrix=projection;capturedTrack=track;
            }
            if(Captured)
            {
                Draft=Math.Max(0,Math.Min(50,(int)Math.Round((x-track.X)/track.Width*50,MidpointRounding.AwayFromZero)));
                ConsumeLeft=ConsumeWheel=true;
                if(!left){Captured=false;if(Draft!=start)host.Radius(Draft);}
            }
            // Publish consumption first; retire only after a focused native
            // release, never after synthetic releases produced by lost focus.
            if(focused && !left)tail=false;
        }
        private static bool Same(F5Rect a,F5Rect b){return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
        internal void Cancel(){if(Captured){Captured=false;tail=true;ConsumeLeft=true;}ConsumeWheel=false;}
    }
}
