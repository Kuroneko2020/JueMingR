using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewRangeOptionsChecks
    {
        private static int bars,zoomBars;
        private static Rectangle bar;
        private static void Bar(SpriteBatch sb,float scale,float perc)
        {
            if(++bars!=4)return;
            zoomBars++;
            Require(perc==MathHelper.Clamp(Main.GameZoomTarget,1,2)-1,"native zoom bar receives only clamped display fraction");
            var size=new Vector2(TextureAssets.ColorBar.Width(),TextureAssets.ColorBar.Height())*scale;
            bar=new Rectangle((int)IngameOptions.valuePosition.X-(int)size.X,(int)IngameOptions.valuePosition.Y-(int)size.Y/2,(int)size.X,(int)size.Y);
            bar.Inflate((int)(-5*scale),0);
        }
        internal static void Run(Main game,object view)
        {
            int x=Main.mouseX,y=Main.mouseY;bool left=Main.mouseLeft,release=Main.mouseLeftRelease,window=Main.ingameOptionsWindow;float selected=Main.GameZoomTarget;
            var audit=new Harmony("JueMingR.Tests.View.RangeOptions");
            audit.Patch(AccessTools.Method(typeof(IngameOptions),"DrawValueBar"),prefix:new HarmonyMethod(typeof(NativeViewRangeOptionsChecks),nameof(Bar)));
            try
            {
                Call(view,"SetUnlockEnabled",false);
                foreach(float target in new[]{.3f,10f})
                {
                    Main.GameZoomTarget=target;Main.mouseX=Main.mouseY=0;Main.mouseLeft=Main.mouseLeftRelease=false;IngameOptions.Open();
                    Draw(game);Draw(game);
                    Require(Main.GameZoomTarget==target,"opening and drawing original options does not write clamped display back");
                    Main.mouseX=bar.X+bar.Width/2;Main.mouseY=bar.Y+bar.Height/2;Draw(game);
                    Require(Main.GameZoomTarget==target,"hovering original zoom bar leaves retained outside target unchanged");
                    Main.mouseLeft=Main.mouseLeftRelease=true;Draw(game);
                    Main.mouseLeftRelease=false;Draw(game);
                    Require(Main.GameZoomTarget>=1&&Main.GameZoomTarget<=2&&Math.Abs(Main.GameZoomTarget-1.5f)<.05f,"actual press/held native slider interaction writes normal range using original lock and pointer calculation");
                    Main.mouseLeft=false;Draw(game);Main.ingameOptionsWindow=false;
                    Console.WriteLine("PASS VIEW RANGE original options target="+target+" open/hover unchanged; real slider press/hold wrote="+Main.GameZoomTarget);
                }
                Require(zoomBars>=12,"real native options repeatedly reached audited zoom bar");
            }
            finally{audit.UnpatchAll(audit.Id);Main.mouseX=x;Main.mouseY=y;Main.mouseLeft=left;Main.mouseLeftRelease=release;Main.ingameOptionsWindow=window;Main.GameZoomTarget=selected;}
        }
        private static void Draw(Main game)
        {
            // Match the native UI caller's zoom-domain pair. Cursor helpers at
            // the end of options re-enter UI zoom; repeatedly calling this
            // leaf without restoring physical input would scale mouse/size
            // again and click the far end of the bar instead of its center.
            int width=Main.screenWidth,height=Main.screenHeight,x=Main.mouseX,y=Main.mouseY;
            Main.mouseX=(int)Math.Round(x*Main.UIScale);Main.mouseY=(int)Math.Round(y*Main.UIScale);
            PlayerInput.CacheOriginalScreenDimensions();PlayerInput.CacheMousePositionForZoom();PlayerInput.SetZoom_UI();
            bars=0;Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);
            try{IngameOptions.Draw(game,Main.spriteBatch);Console.WriteLine("RANGE OPTIONS native target="+Main.GameZoomTarget+" mouse="+Main.mouseX+","+Main.mouseY+" left="+Main.mouseLeft+" bar="+bar+" lock="+IngameOptions.rightLock+" hover="+IngameOptions.rightHover+" notBar="+IngameOptions.notBar);}
            finally{Main.spriteBatch.End();PlayerInput.SetZoom_Unscaled();Main.screenWidth=width;Main.screenHeight=height;Main.mouseX=x;Main.mouseY=y;}
        }
    }
}
