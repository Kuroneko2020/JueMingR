using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.F5
{
    internal static class CompanionPetPreviewHooks
    {
        [ThreadStatic] private static Projectile drawing;
        [ThreadStatic] private static Player owner;
        [ThreadStatic] private static float drawingScale;
        [ThreadStatic] private static Measurement measuring;
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Main),"PrepareDrawnProjectileDrawing"),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Prepare)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(int),typeof(int)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(White)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(Point)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(White)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(int),typeof(int),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Tint)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(Point),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(PointTint)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColorClamped",new[]{typeof(int),typeof(int),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Tint)));
            harmony.Patch(AccessTools.Method(typeof(PlayerDrawLayers),"DrawPlayer_RenderAllLayers"),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(BodyBounds)));
            harmony.Patch(AccessTools.Method(typeof(Main),"EntitySpriteDraw",new[]{typeof(Texture2D),typeof(Vector2),typeof(Rectangle?),typeof(Color),typeof(float),typeof(Vector2),typeof(Vector2),typeof(SpriteEffects),typeof(float)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(PetBounds)));
            harmony.Patch(AccessTools.Method(typeof(Main),"EntitySpriteDraw",new[]{typeof(DrawData)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(PetDataBounds)));
        }
        internal static IDisposable Enter(Projectile dummy,Player display,float scale)
        {return new Scope(dummy,display,scale);}
        private sealed class Scope : IDisposable
        {
            private readonly Projectile previous=drawing;private readonly Player previousOwner=owner;
            private readonly float previousScale=drawingScale;
            internal Scope(Projectile dummy,Player display,float scale) {drawing=dummy;owner=display;drawingScale=scale;}
            public void Dispose() {drawing=previous;owner=previousOwner;drawingScale=previousScale;}
        }
        internal sealed class Measurement : IDisposable
        {
            internal readonly Player Body;
            private readonly Measurement previous=measuring;
            internal Vector4 Bounds=CompanionHeadLayout.EmptyBounds;
            internal bool Complete=true;
            internal Measurement(Player display) {Body=display;measuring=this;}
            public void Dispose() {measuring=previous;}
        }
        private static bool BodyBounds(ref PlayerDrawSet drawinfo)
        {
            if(measuring==null||!ReferenceEquals(measuring.Body,drawinfo.drawPlayer))return true;
            // The public DrawPlayer has already built/transformed its original
            // layers. Layout reads those exact private-copy layers once and
            // skips only their final GPU submission, never any world player.
            foreach(var layer in drawinfo.DrawDataCache){if(layer.texture==null)measuring.Complete=false;CompanionHeadLayout.AddBounds(ref measuring.Bounds,layer);}
            return false;
        }
        private static bool PetBounds(Texture2D texture,Vector2 position,Rectangle? sourceRectangle,Color color,float rotation,Vector2 origin,Vector2 scale,SpriteEffects effects)
        {if(measuring==null||drawing==null||!ReferenceEquals(measuring.Body,owner))return true;return CollectPet(new DrawData(texture,position,sourceRectangle,color,rotation,origin,scale,effects,0));}
        private static bool PetDataBounds(DrawData data) {return CollectPet(data);}
        private static bool CollectPet(DrawData data)
        {
            if(measuring==null||drawing==null||!ReferenceEquals(measuring.Body,owner))return true;
            if(data.texture==null)measuring.Complete=false;
            data.position*=drawingScale;data.scale*=drawingScale;
            CompanionHeadLayout.AddBounds(ref measuring.Bounds,data);return false;
        }
        private static bool Prepare(Projectile proj)
        {
            if(!ReferenceEquals(proj,drawing)||owner==null)return true;
            // This exact dummy already has a bounded UI SpriteBatch/matrix. The
            // ordinary method would look up the world's owner dye and replace
            // that matrix. No player array slot is substituted, even temporarily.
            Main.CurrentDrawnEntity=proj;Main.CurrentDrawnEntityShader=owner.cPet;return false;
        }
        private static bool White(ref Color __result)
        // Character selection runs the menu's white-light branch. Reproduce
        // only its color result while this exact UI dummy is on the stack;
        // never turn on gameMenu or change the world's light engine.
        {if(drawing==null)return true;__result=Color.White;return false;}
        private static bool Tint(Color oldColor,ref Color __result)
        {if(drawing==null)return true;__result=oldColor;return false;}
        private static bool PointTint(Color originalColor,ref Color __result)
        {if(drawing==null)return true;__result=originalColor;return false;}
    }
}
