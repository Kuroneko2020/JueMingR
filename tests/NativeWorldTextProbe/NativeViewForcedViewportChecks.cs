using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // One physical/forced counterexample, independent of the new cache helper.
    // Old-path A and test-only forced-native-direct B retain the actual target,
    // matrix, tiles and original Draw consumers. No owner profile/settings.
    internal static class NativeViewForcedViewportChecks
    {
        private static bool Direct(ref bool __result){__result=true;return false;}
        internal static void Run(Main game,object view,Action draw,Func<RenderTarget2D> final,Action<int,int> resize,string output)
        {
            resize(2560,1440);Call(view,"SetUnlockEnabled",true);Main.GameZoomTarget=.75f;for(int i=0;i<8;i++)draw();
            var native=(WorldSceneLayerTarget)AccessTools.Field(typeof(Main),"tile2Target").GetValue(null);
            Console.WriteLine("FORCED OLD actual physical="+Main.screenWidth+"x"+Main.screenHeight+" target="+Main.GameZoomTarget+" forced="+Main.ForcedMinimumZoom+" final="+Main.GameViewMatrix.RenderZoom+" camera="+Main.Camera.ScaledSize+" native-RT="+native.Texture.Width+"x"+native.Texture.Height+" direct="+Main.drawToScreen);
            Require(Main.GameViewMatrix.RenderZoom.X==1f&&!Main.drawToScreen&&Main.Camera.ScaledSize.X>native.Texture.Width,"preserved old absolute-zoom path actually uses undersized native world RT");
            Vector2 start=Main.Camera.ScaledPosition;
            var locations=new[]{start+new Vector2(24,Main.screenHeight*.45f),start+new Vector2(Main.screenWidth-24,Main.screenHeight*.45f)};
            var positions=new Point[2];var saved=new Tile[2];
            var audit=new Harmony("JueMingR.Tests.View.ForcedOldReference");
            try
            {
                for(int i=0;i<2;i++)
                {
                    positions[i]=new Point((int)locations[i].X/16,(int)locations[i].Y/16);
                    var tile=Main.tile[positions[i].X,positions[i].Y];saved[i]=(Tile)tile.Clone();tile.active(true);tile.type=Terraria.ID.TileID.Torches;tile.frameX=tile.frameY=0;
                }
                Main.renderNow=true;for(int i=0;i<12;i++)draw();var before=Pixels(final());Save(final(),output,"forced-old-native-cropped.png");
                var matrix=Main.GameViewMatrix.TransformationMatrix;var screen=Main.screenPosition;
                var early=view.GetType().Assembly.GetType("JueMingR.TerrariaHost.View.ViewHooks").GetMethod("DirectDraw",BindingFlags.Static|BindingFlags.NonPublic);
                audit.Patch(early,prefix:new HarmonyMethod(typeof(NativeViewForcedViewportChecks),nameof(Direct)));
                for(int i=0;i<12;i++)draw();var after=Pixels(final());Save(final(),output,"forced-old-test-direct-reference.png");
                Require(Main.GameViewMatrix.TransformationMatrix==matrix&&Main.screenPosition==screen&&Main.GameZoomTarget==.75f,"old/direct counterexample keeps exact native camera and target");
                for(int i=0;i<2;i++)
                {
                    var at=Vector2.Transform(new Vector2(positions[i].X*16+8,positions[i].Y*16+8)-screen,matrix);int changed=0;long oldRgb=0,newRgb=0;
                    for(int y=Math.Max(0,(int)at.Y-20);y<Math.Min(Main.screenHeight,(int)at.Y+20);y++)for(int x=Math.Max(0,(int)at.X-20);x<Math.Min(Main.screenWidth,(int)at.X+20);x++)
                    {var a=before[y*Main.screenWidth+x];var b=after[y*Main.screenWidth+x];if(a!=b)changed++;oldRgb+=a.R+a.G+a.B;newRgb+=b.R+b.G+b.B;}
                    Console.WriteLine("FORCED OLD native peripheral torch="+positions[i]+" physical="+at+" changed="+changed+" old-rgb="+oldRgb+" direct-rgb="+newRgb);
                    Require(changed>2&&newRgb>oldRgb,"old undersized native RT loses actual outer torch pixels restored by original direct Draw");
                }
                Console.WriteLine("PASS FORCED OLD true rendering counterexample: native2304 RT cannot cover actual2560 world; original direct consumer restores both outer torches at unchanged matrix, beyond semantic target comparison.");
            }
            finally{audit.UnpatchAll(audit.Id);for(int i=0;i<2;i++)if(saved[i]!=null)Main.tile[positions[i].X,positions[i].Y].CopyFrom(saved[i]);}
        }
        private static Color[] Pixels(RenderTarget2D value){var pixels=new Color[value.Width*value.Height];value.GetData(pixels);return pixels;}
        private static void Save(RenderTarget2D value,string output,string name){using(var file=File.Create(Path.Combine(output,name)))value.SaveAsPng(file,value.Width,value.Height);}
    }
}
