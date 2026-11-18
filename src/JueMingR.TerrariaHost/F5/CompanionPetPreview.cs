using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.F5
{
    // This is the character-list preview dummy, never a world projectile. The
    // official preview animation settings change only this privately owned body.
    internal sealed class CompanionPetPreview
    {
        private Projectile dummy;
        private int type;
        private readonly UnifiedRandom random=new UnifiedRandom(41917);
#if DEBUG
        internal int Rebuilds { get; private set; }
        internal int Draws { get; private set; }
#endif
        internal Projectile Dummy { get { return dummy; } }
        internal static bool Visible(Player player)
        {return player!=null&&!player.hideMisc[0]&&!player.miscEquips[0].IsAir&&player.miscEquips[0].shoot>0;}
        internal void Clear() {dummy=null;type=0;}
        internal void Draw(Player display,Vector2 feet,float scale,Matrix transform)
        {
            if(!Visible(display)) {Clear();return;}
            int next=display.miscEquips[0].shoot;
            var previousRandom=Main.rand;var previousEntity=Main.CurrentDrawnEntity;int previousShader=Main.CurrentDrawnEntityShader,previousMountShader=Mount.currentShader;
            Vector2 previousPosition=display.position;
            var batch=Main.spriteBatch;var device=batch.GraphicsDevice;
            bool began=false;batch.End();
            // The private native image borrows this UI pass, whose continuation
            // is text. Never infer its sampler from an unflushed deferred batch.
            var blend=Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend;
            var sampler=Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp;
            var depth=Microsoft.Xna.Framework.Graphics.DepthStencilState.None;
            var rasterizer=device.RasterizerState;
            try
            {
                Main.rand=random;
                if(dummy==null||type!=next)
                {
                    dummy=new Projectile();dummy.SetDefaults(next);dummy.isAPreviewDummy=true;dummy.owner=255;
                    type=next;
#if DEBUG
                    Rebuilds++;
#endif
                }
                // Match UICharacter's relative player/pet feet and its ordinary
                // not-selected pose. Scaling the whole native drawing keeps the
                // animation offsets, special layers and multi-part pets together.
                dummy.position=Main.screenPosition+new Vector2(20-display.width/2f,-dummy.height);
                dummy.velocity=new Vector2(.1f,0);dummy.direction=1;
                ProjectileID.Sets.CharacterPreviewAnimations[type].ApplyTo(dummy,false);
                display.position=Main.screenPosition-new Vector2(display.width/2f,display.height);
                Matrix petMatrix=Matrix.CreateScale(scale)*Matrix.CreateTranslation(feet.X,feet.Y,0)*transform;
                batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate,blend,Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp,depth,rasterizer,null,petMatrix);began=true;
                using(CompanionPetPreviewHooks.Enter(dummy,display,scale))
                    Main.instance.DrawProjDirect(dummy,display);
#if DEBUG
                Draws++;
#endif
            }
            finally
            {
                try {if(began)batch.End();}
                finally
                {
                    display.position=previousPosition;Main.rand=previousRandom;
                    Main.CurrentDrawnEntity=previousEntity;Main.CurrentDrawnEntityShader=previousShader;Mount.currentShader=previousMountShader;
                    batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred,blend,sampler,depth,rasterizer,null,transform);
                }
            }
        }
    }
}
