using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Renderers;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.F5
{
    // A display copy is never installed in Main.player and never Update()s.
    // Cosmetic fields and required equipment display are prepared only on that
    // copy, using a bounded native appearance path without world simulation.
    internal sealed class CompanionPreview
    {
        private Player source, display;
        private int appearance;
        private static readonly FieldInfo[] visualFields = Fields();
        private readonly CompanionHeadLayout headLayout=new CompanionHeadLayout();
        private readonly CompanionPetPreview petPreview=new CompanionPetPreview();
        private readonly UnifiedRandom random = new UnifiedRandom(7139);
        private int showcaseRevision=-1,showcaseGeneration=-1;
        private object showcaseAssets;
        private Vector2 showcaseSize,showcaseCenter,showcaseFeet;
        private float showcaseScale=1.625f;
#if DEBUG
        internal int ShowcaseMeasurements {get;private set;}
#endif
        internal int Rebuilds { get; private set; }
        internal Player Display { get { return display; } }
        internal CompanionPetPreview Pet { get { return petPreview; } }
        private static FieldInfo[] Fields()
        {
            var result=new List<FieldInfo>();
            foreach(string name in "head body legs handon handoff back front shoe waist shield neck face faceHead faceFlower faceMask balloon balloonFront beard coat wings tail backpack wearsRobe drawFloatingTube hasFloatingTube hasAngelHalo hasUnicornHorn hasRainbowCursor drawnWingsLogic skinDyePacked cHead cBody cLegs cHandOn cHandOff cBack cFront cShoe cWaist cShield cNeck cFace cBalloon cWings cCarpet cFloatingTube cAngelHalo cLeinShampoo cBackpack cTail cFaceHead cFaceFlower cFaceMask cBalloonFront cBeard cCoat cHair cMinion cMount cMinecart cFlameWaker cShield cUnicornHorn cMerman cWolf cFaceFlower merfolk merman wolfAcc hideMerman hideWolf yoraiz0rEye yoraiz0rDarkness".Split(' '))
            {
                var f=typeof(Player).GetField(name,BindingFlags.Public|BindingFlags.Instance);
                if(f!=null && !f.IsInitOnly && f.FieldType.IsValueType) result.Add(f);
            }
            return result.ToArray();
        }
        internal void Clear() { source=null; display=null; appearance=0;headLayout.Clear();petPreview.Clear();showcaseRevision=showcaseGeneration=-1;showcaseAssets=null; }
        internal void Prepare(Player player)
        {
            if(player==null) { Clear(); return; }
            if(display!=null) { display.statLife=player.statLife; display.statLifeMax=player.statLifeMax; display.statLifeMax2=player.statLifeMax2; display.statMana=player.statMana; display.statManaMax=player.statManaMax; display.statManaMax2=player.statManaMax2; }
            int key=Fingerprint(player);
            if(ReferenceEquals(source,player)&&display!=null&&appearance==key) return;
            source=player; appearance=key;
            if(display==null) display=new Player();
            display.CopyVisuals(player);
            foreach(var f in visualFields) f.SetValue(display,f.GetValue(player));
            display.difficulty=player.difficulty;
            display.hairDye=player.hairDye;
            display.direction=1; display.gravDir=1; display.velocity=Vector2.Zero;
            display.position=Vector2.Zero; display.gfxOffY=0; display.fullRotation=0;
            display.bodyRotation=display.legRotation=display.headRotation=0;
            display.bodyFrame=display.legFrame=display.headFrame=new Rectangle(0,0,40,56);
            display.bodyPosition=display.legPosition=display.headPosition=Vector2.Zero;
            // CopyVisuals keeps SelectedItemState.player pointing at its source.
            // Rebind it before any frame/selection consumer can reset owner AFK.
            display.selectedItemState=new Player.SelectedItemState(display); display.inventory[0]=new Item(); display.heldProj=-1;
            display.itemAnimation=display.itemTime=0; display.dead=false; display.ghost=false;
            display.lastVisualizedSelectedItem=new Item();
            display.sitting=default(Terraria.GameContent.PlayerSittingHelper); display.sleeping=default(Terraria.GameContent.PlayerSleepingHelper);
            // Native PlayerFrame's CCed exit follows SetMatch, visible accessories
            // and cape preparation, but precedes every dust/animation branch.
            // Freeze only this disposable copy while preparing that bounded half;
            // never run equipment effects, timers or any real player's frame.
            display.ResetVisibleAccessories(); display.UpdateDyes();
            display.frozen=true; display.isDisplayDollOrInanimate=true;
            try { display.PlayerFrame(); }
            finally { display.frozen=false; display.isDisplayDollOrInanimate=false; }
            // Leinfors shampoo's draw layer calls Dust.NewDust directly. Suppress
            // that particle-only flag on this copy; hair, colors and dyes remain.
            display.leinforsHair=false;
            // Vanilla already has a portrait illumination branch that preserves
            // actual skin/clothes/hair colors. Copy only cosmetic derived fields
            // above: debuff, honey, regen and other particle-producing simulation
            // flags must never be imported into BoringSetup's draw effects.
            display.socialIgnoreLight=true; display.isDisplayDollOrInanimate=false;
            display.isFullbright=false; display.opacityForAnimation=1;
            display.immuneAlpha=0; display.shimmerTransparency=0;
            Rebuilds++;
        }
        private static int Fingerprint(Player p)
        {
            unchecked
            {
                int h=p.hair*397+p.skinVariant;
                h=h*31+p.hairColor.GetHashCode(); h=h*31+p.eyeColor.GetHashCode(); h=h*31+p.skinColor.GetHashCode();
                h=h*31+p.shirtColor.GetHashCode(); h=h*31+p.underShirtColor.GetHashCode(); h=h*31+p.pantsColor.GetHashCode(); h=h*31+p.shoeColor.GetHashCode(); h=h*31+p.hairDye;
                // BitsByte's inherited ValueType hash does not reliably expose
                // its single byte on this runtime. Visibility is an actual input.
                h=h*31+p.head; h=h*31+p.body; h=h*31+p.legs; h=h*31+(byte)p.hideMisc;
                h=Items(h,p.armor); h=Items(h,p.dye); h=Items(h,p.miscEquips); h=Items(h,p.miscDyes);
                foreach(bool hidden in p.hideVisibleAccessory) h=h*31+(hidden?1:0);
                return h;
            }
        }
        private static int Items(int h,Item[] items) { unchecked { foreach(var i in items) { h=h*31+(i?.type??0); h=h*31+(i?.prefix??0); h=h*31+(i?.dye??0); } return h; } }
        internal void DrawHead(LegacyPlayerRenderer renderer,F5Rect rect,Matrix transform,int skin)
        {
            var player=display;if(player==null)return;
            var batch=Main.spriteBatch;var device=batch.GraphicsDevice;var blend=device.BlendState;var sampler=device.SamplerStates[0];var depth=device.DepthStencilState;var rasterizer=device.RasterizerState;
            var oldScissor=device.ScissorRectangle;
            var previous=Main.rand;int shader=Mount.currentShader;
            batch.End(); bool began=false;
            try
            {
                Main.rand=random;
                headLayout.Prepare(player,Rebuilds,skin,new Vector2(rect.Width,rect.Height));
                device.ScissorRectangle=F5ControlRenderer.ContentClip(rect,transform,oldScissor);
                // Head layers render through SpriteBatch, unlike the full-body
                // SpriteDrawBuffer path. Immediate mode preserves each dye pass.
                batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate,blend,Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp,depth,rasterizer,null,transform);began=true;
                renderer.DrawPlayerHead(Main.Camera,player,headLayout.Anchor(rect),1,headLayout.Scale);
            }
            finally
            {
                try {if(began) batch.End();}
                finally {Main.rand=previous;Mount.currentShader=shader;device.ScissorRectangle=oldScissor;batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred,blend,sampler,depth,rasterizer,null,transform);}
            }
        }
        internal void DrawShowcase(LegacyPlayerRenderer renderer,F5Rect frame,Matrix transform,int generation)
        {
            if(display==null)return;
            var size=new Vector2(frame.Width,frame.Height);
            if(showcaseRevision!=Rebuilds||showcaseGeneration!=generation||showcaseSize!=size||!ReferenceEquals(showcaseAssets,Main.Assets))
            {
                // Measure the original public renderers' actual transformed
                // opaque layers once for this appearance/asset/frame identity.
                // The exact private-copy scope omits GPU submission only here.
                bool complete;
                using(var measurement=new CompanionPetPreviewHooks.Measurement(display))
                {
                    DrawBodyAt(renderer,Vector2.Zero,1.625f,Matrix.Identity);
                    petPreview.Draw(display,Vector2.Zero,1.625f,Matrix.Identity);
                    var b=measurement.Bounds;
                    if(b.X==Single.MaxValue)b=new Vector4(-16,-44,16,0);
                    float fit=Math.Min(1,Math.Min(Math.Max(1,frame.Width-16)/Math.Max(1,b.Z-b.X),Math.Max(1,frame.Height-16)/Math.Max(1,b.W-b.Y)));
                    showcaseScale=1.625f*fit;showcaseCenter=new Vector2((b.X+b.Z)/2,(b.Y+b.W)/2)*fit;
                    complete=measurement.Complete;
                }
                showcaseRevision=complete?Rebuilds:-1;showcaseGeneration=generation;showcaseSize=size;showcaseAssets=Main.Assets;
#if DEBUG
                ShowcaseMeasurements++;
#endif
            }
            // The frame is fixed by layout. Move and, only when needed, fit the
            // whole native body/pet union inside it; never center clipped pixels.
            showcaseFeet=new Vector2(frame.X+frame.Width/2,frame.Y+frame.Height/2)-showcaseCenter;
            petPreview.Draw(display,showcaseFeet,showcaseScale,transform);
            DrawBodyAt(renderer,showcaseFeet,showcaseScale,transform);
        }
        internal void DrawBodyAt(LegacyPlayerRenderer renderer,Vector2 feet,float scale,Matrix transform)
        {
            if(display==null) return;
            Vector2 position=feet-new Vector2(display.width/2f,display.height)+Main.screenPosition;
            // Some native glow layers sample Main.rand even without particles.
            // A private random stream keeps preview drawing out of world RNG.
            var previous=Main.rand; int priorMountShader=Mount.currentShader;
            var batch=Main.spriteBatch; var device=batch.GraphicsDevice;
            var blend=device.BlendState; var sampler=device.SamplerStates[0]; var depth=device.DepthStencilState; var rasterizer=device.RasterizerState;
            batch.End();
            try
            {
                Main.rand=random;
                // DrawPlayer renders its SpriteDrawBuffer immediately. Flush the
                // panel first and set that buffer's vertex transform to this UI
                // pass, rather than inheriting the world's camera matrix.
                Main.spriteBuffer.ApplyDefaultSpriteEffect(rasterizer,transform);
                renderer.DrawPlayer(Main.Camera,display,position,0,Vector2.Zero,0,scale);
            }
            finally
            {
                Main.rand=previous; Mount.currentShader=priorMountShader;
                batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred,blend,sampler,depth,rasterizer,null,transform);
            }
        }
    }
}
