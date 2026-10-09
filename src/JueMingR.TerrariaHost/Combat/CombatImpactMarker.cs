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
        private Matrix inverse;
        private object failedPixel,failedBatch;
        private bool resourceFailure;
        internal bool Failed {get;private set;}
        internal bool Visible {get;private set;}
        internal Vector2 Position {get{return center;}}
        internal CombatImpactMarker(HostCombatObservation host){this.host=host;}
        internal void Clear(){contact=null;Visible=false;}
        internal void Reset(){Clear();Failed=resourceFailure=false;failedPixel=failedBatch=null;}
        internal void PollResources(){if(Failed && resourceFailure && (!ReferenceEquals(failedPixel,TextureAssets.MagicPixel) || !ReferenceEquals(failedBatch,Main.spriteBatch)))Reset();}
        internal void Capture()
        {
            Clear();if(Failed || !host.Path || !host.Options.Aim)return;
            try
            {
                var prepared=host.Attack?.ExpectedImpact;
                if(prepared!=null && ReferenceEquals(prepared.Timeline,host.Prediction.Cache.Read(0)))contact=prepared;
            }
            catch(Exception error){Fail(error,false);}
        }
        internal void Project(Matrix zoom,Matrix inverse)
        {
            Visible=false;if(Failed || contact==null || !host.Path || !host.Options.Aim)return;
            try
            {
                if(!ReferenceEquals(contact,host.Attack?.ExpectedImpact) || !ReferenceEquals(contact.Timeline,host.Prediction.Cache.Read(0)))return;
                center=GuidanceWorldLayer.Project(new Vector2(contact.ImpactX,contact.ImpactY),zoom);this.inverse=inverse;
                Visible=!float.IsNaN(center.X) && !float.IsNaN(center.Y) && !float.IsInfinity(center.X) && !float.IsInfinity(center.Y) && center.X>=-4 && center.Y>=-4 && center.X<=Main.screenWidth+4 && center.Y<=Main.screenHeight+4;
            }
            catch(Exception error){Fail(error,false);}
        }
        internal void Draw(SpriteBatch batch)
        {
            if(!Visible)return;
            try
            {
                var pixel=TextureAssets.MagicPixel.Value;
                // Screen-space size remains eight pixels under camera zoom.
                var top=center-new Vector2(4,4);var red=new Color(255,65,65);
                Segment(batch,pixel,top,top+new Vector2(8,0),red);Segment(batch,pixel,top+new Vector2(8,0),top+new Vector2(8,8),red);
                Segment(batch,pixel,top+new Vector2(8,8),top+new Vector2(0,8),red);Segment(batch,pixel,top+new Vector2(0,8),top,red);
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
        {a=Vector2.Transform(a,inverse);b=Vector2.Transform(b,inverse);var delta=b-a;batch.Draw(pixel,a,new Rectangle(0,0,1,1),color,(float)Math.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(delta.Length(),1.5f*inverse.M11),SpriteEffects.None,0);}
    }
}
