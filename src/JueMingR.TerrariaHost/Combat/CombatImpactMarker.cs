using System;
using JueMingR.Features.Combat;
using JueMingR.TerrariaHost.Guidance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class CombatImpactMarker
    {
        private readonly HostCombatObservation host;
        private AttackContact contact;
        private Vector2 center;
        private Vector2 worldA,worldB,worldC,worldD,a,b,c,d;
        private Matrix inverse;
        private object failedPixel,failedBatch;
        private bool resourceFailure;
        private bool execution;
        internal bool Failed {get;private set;}
        internal bool Visible {get;private set;}
        internal Vector2 Position {get{return center;}}
        internal CombatImpactMarker(HostCombatObservation host){this.host=host;}
        internal void Clear(){contact=null;Visible=false;execution=false;}
        internal void Reset(){Clear();Failed=resourceFailure=false;failedPixel=failedBatch=null;}
        internal void PollResources(){if(Failed && resourceFailure && (!ReferenceEquals(failedPixel,TextureAssets.MagicPixel) || !ReferenceEquals(failedBatch,Main.spriteBatch)))Reset();}
        internal void Capture()
        {
            Clear();if(Failed || !host.Path)return;
            try
            {
                var prepared=host.Attack?.ExpectedImpact;execution=prepared!=null;
                if(prepared==null)prepared=host.Attack?.ExpectedPreview;
                if(prepared!=null && prepared.ReceiveTick>=0 && prepared.ReceiveTick<prepared.Timeline.Count && host.DisplayPath!=null)
                {
                    // Capture a lightweight immutable receive rectangle, not
                    // an aim point/contact edge or the NPC's current body.
                    var bounds=prepared.ReceiveBounds;
                    if(bounds.Width<=0 || bounds.Height<=0)return;
                    worldA=new Vector2(bounds.X,bounds.Y);worldB=worldA+new Vector2(bounds.Width,0);
                    worldC=worldA+new Vector2(bounds.Width,bounds.Height);worldD=worldA+new Vector2(0,bounds.Height);
                    if(!Finite(worldA) || !Finite(worldB) || !Finite(worldC) || !Finite(worldD))return;
                    if(execution)host.Attack.BindPresentation(prepared);else host.Attack.BindPreview(prepared);contact=prepared;
                }
            }
            catch(Exception error){Fail(error,false);}
        }
        internal void Project(Matrix zoom,Matrix inverse)
        {
            Visible=false;if(Failed || contact==null || !host.Path)return;
            try
            {
                if(host.Attack==null || !(execution?host.Attack.PresentationCurrent(contact):host.Attack.PreviewCurrent(contact)) || host.DisplayPath==null)return;
                // The final camera projects frozen world corners once. The
                // inverse below only cancels SpriteBatch's existing transform;
                // world dimensions therefore scale exactly once, even upside down.
                a=GuidanceWorldLayer.Project(worldA,zoom);b=GuidanceWorldLayer.Project(worldB,zoom);
                c=GuidanceWorldLayer.Project(worldC,zoom);d=GuidanceWorldLayer.Project(worldD,zoom);
                center=(a+c)*.5f;this.inverse=inverse;
                if(!Finite(a) || !Finite(b) || !Finite(c) || !Finite(d))return;
                float left=Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X)),right=Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X));
                float top=Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y)),bottom=Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y));
                Visible=right>=-4 && bottom>=-4 && left<=Main.screenWidth+4 && top<=Main.screenHeight+4;
            }
            catch(Exception error){Fail(error,false);}
        }
        internal void Draw(SpriteBatch batch)
        {
            if(!Visible)return;
            try
            {
                var pixel=TextureAssets.MagicPixel.Value;
                var red=new Color(255,65,65);
                Segment(batch,pixel,a,b,red);Segment(batch,pixel,b,c,red);
                Segment(batch,pixel,c,d,red);Segment(batch,pixel,d,a,red);
            }
            catch(Exception error){Fail(error,true);}
        }
        private void Fail(Exception error,bool resources)
        {
            if(error is OutOfMemoryException || error is AccessViolationException)throw error;
            // This optional presentation owns its fault, never the shared
            // attack/path/marker health. Read faults require explicit retry;
            // only a draw-resource fault may recover on resource replacement.
            Clear();Failed=true;resourceFailure=resources;failedPixel=TextureAssets.MagicPixel;failedBatch=Main.spriteBatch;
        }
        private void Segment(SpriteBatch batch,Texture2D pixel,Vector2 a,Vector2 b,Color color)
        {if(!CombatObservationWorldLayer.Clip(ref a,ref b))return;a=Vector2.Transform(a,inverse);b=Vector2.Transform(b,inverse);var delta=b-a;if(delta.LengthSquared()<=.00000001f)return;batch.Draw(pixel,a,new Rectangle(0,0,1,1),color,(float)Math.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(delta.Length(),1.5f*inverse.M11),SpriteEffects.None,0);}
        private static bool Finite(Vector2 p){return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y);}
    }
}
