using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal static class CompanionPetPreviewHooks
    {
        [ThreadStatic] private static Projectile drawing;
        [ThreadStatic] private static Player owner;
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Main),"PrepareDrawnProjectileDrawing"),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Prepare)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(int),typeof(int)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(White)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(Point)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(White)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(int),typeof(int),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Tint)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColor",new[]{typeof(Point),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(PointTint)));
            harmony.Patch(AccessTools.Method(typeof(Lighting),"GetColorClamped",new[]{typeof(int),typeof(int),typeof(Color)}),prefix:new HarmonyMethod(typeof(CompanionPetPreviewHooks),nameof(Tint)));
        }
        internal static IDisposable Enter(Projectile dummy,Player display)
        {return new Scope(dummy,display);}
        private sealed class Scope : IDisposable
        {
            private readonly Projectile previous=drawing;private readonly Player previousOwner=owner;
            internal Scope(Projectile dummy,Player display) {drawing=dummy;owner=display;}
            public void Dispose() {drawing=previous;owner=previousOwner;}
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
