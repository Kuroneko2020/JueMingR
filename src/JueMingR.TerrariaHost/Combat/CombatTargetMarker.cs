using System;
using JueMingR.Platform.Combat;
using JueMingR.TerrariaHost.Guidance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    // Constant six-sprite display of the existing selection. This owner neither
    // requests a future nor seeks a head/guardian or writes vanilla lock-on.
    internal sealed class CombatTargetMarker
    {
        internal struct Piece {internal Vector2 Position,Scale;internal float Rotation;internal int SourceY;internal Color Color;}
        internal readonly Piece[] Pieces=new Piece[6];
        private readonly HostCombatObservation host;
        private NpcIdentity identity;
        private Rectangle receiveBox;
        internal bool Failed {get;private set;}
        internal bool Visible {get;private set;}
        internal CombatTargetMarker(HostCombatObservation host){this.host=host;}
        internal void Clear(){Visible=false;identity=default(NpcIdentity);}
        internal void Reset(){Clear();Failed=false;}
        private void Fail(Exception error)
        {
            Clear();if(Failed)return;Failed=true;
            Prediction.AimLightTrace.Fault("target-marker",error,(long)Main.GameUpdateCount);
        }
        private bool Current()
        {return host.Marker && host.Selection.HasTarget && host.Selection.Target.Equals(identity) && CombatSelection.Valid(identity,host.Session) && CombatSelection.Receives(Main.npc[identity.Slot],host.Options.Dummy);}
        internal void Capture()
        {Clear();if(!host.Marker){Failed=false;return;}if(Failed || !host.Selection.HasTarget)return;identity=host.Selection.Target;if(Current())receiveBox=CombatSelection.ReceiveBounds(Main.npc[identity.Slot]);}
        internal void Prepare(Matrix zoom,Matrix inverse){Capture();Project(zoom,inverse);}
        internal void Project(Matrix zoom,Matrix inverse)
        {
            Visible=false;if(!host.Marker){Failed=false;return;}if(Failed)return;
            try{PrepareCore(zoom,inverse);}
            catch(ArgumentException error){Fail(error);}
            catch(OverflowException error){Fail(error);}
        }
        private void PrepareCore(Matrix zoom,Matrix inverse)
        {
            Visible=false;if(!Current())return;
            var box=receiveBox;if(box.Width<=0 || box.Height<=0)return;
            var center=new Vector2(box.Center.X,box.Center.Y);var screen=GuidanceWorldLayer.Project(center,zoom);
            float diameter=Math.Max(box.Width,box.Height)+20,radius=(int)diameter/2;
            if(!Finite(screen) || screen.X+radius*zoom.M11<0 || screen.X-radius*zoom.M11>Main.screenWidth || screen.Y+radius*zoom.M22<0 || screen.Y-radius*zoom.M22>Main.screenHeight)return;
            float pulse=.94f+(float)Math.Sin(Main.GlobalTimeWrappedHourly*Math.PI*2)*.06f,scale=Math.Min(1,diameter/70);
            var front=Main.OurFavoriteColor;front.A=220;front*=pulse;
            var back=Main.OurFavoriteColor.MultiplyRGBA(new Color(.75f,.75f,.75f,1));back.A=220;back*=pulse;
            int sign=Main.LocalPlayer.gravDir<0?-1:1;
            for(int i=0;i<3;i++)
            {
                float phase=(float)(Math.PI*2/3*i+Main.GlobalTimeWrappedHourly*Math.PI*.5);
                var point=center+new Vector2(-(float)Math.Sin(phase),(float)Math.Cos(phase))*radius;
                var position=Vector2.Transform(GuidanceWorldLayer.Project(point,zoom),inverse);
                float rotation=phase*sign+(sign>0?(float)Math.PI:0);
                Pieces[i*2]=new Piece{Position=position,Scale=new Vector2(.58f,1)*scale,Rotation=rotation,SourceY=0,Color=front};
                Pieces[i*2+1]=new Piece{Position=position,Scale=new Vector2(.58f,1)*scale,Rotation=rotation,SourceY=16,Color=back};
            }
            Visible=true;
        }
        internal void Draw(SpriteBatch batch)
        {
            if(Failed || !Visible || !Current())return;
            // Borrow the game's loaded atlas on its graphics thread. Clearing
            // this display must never dispose or replace the game's asset.
            Texture2D texture;
            try{texture=TextureAssets.LockOnCursor?.Value;}
            catch(InvalidOperationException error){Fail(error);return;}
            catch(ArgumentException error){Fail(error);return;}
            catch(System.IO.IOException error){Fail(error);return;}
            if(texture==null || texture.IsDisposed || texture.Height<28){Fail(new InvalidOperationException("Target marker atlas unavailable."));return;}
            try
            {
                for(int i=0;i<Pieces.Length;i++)
                {var p=Pieces[i];batch.Draw(texture,p.Position,new Rectangle(0,p.SourceY,texture.Width,12),p.Color,p.Rotation,new Vector2(texture.Width/2f,6),p.Scale,SpriteEffects.None,0);}
            }
            catch(ArgumentException error){Fail(error);}
            catch(ObjectDisposedException error)
            {
                if(batch.IsDisposed || batch.GraphicsDevice.IsDisposed || !texture.IsDisposed)throw;
                Fail(error);
            }
            // An invalid shared SpriteBatch/device remains a world-layer fault;
            // resource/marker parameter failures above cannot erase the path.
        }
        private static bool Finite(Vector2 p){return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y);}
    }
}
