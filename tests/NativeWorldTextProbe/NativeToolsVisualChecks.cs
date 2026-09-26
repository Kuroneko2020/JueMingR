using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeToolsVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");Main.InitializeItemAnimations();
            NativeToolsUiChecks.Run(context,graphics);
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer"),items=Get(shell,"items"),popup=Get(shell,"CaptureUi");
            foreach(var size in new[]{new[]{960,760,100},new[]{1280,720,150},new[]{960,440,100}})
            {
                float scale=size[2]/100f;var matrix=Matrix.CreateScale(scale);NativeToolsUiChecks.Prepare(context,0,size[0],size[1],scale);
                var config=NativeToolsUiChecks.Controls(items).FirstOrDefault(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==3);
                if(config==null){var rows=(System.Collections.IEnumerable[])Get(Get(items,"ToolsPanel"),"rows");Call(state,"ScrollTo",Get(Get(rows[0].Cast<object>().First(),"Rect"),"Y"));NativeToolsUiChecks.Prepare(context,0,size[0],size[1],scale);config=NativeToolsUiChecks.Controls(items).First(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==3);}
                graphics.LoadItemTextures(NativeToolsUiChecks.Controls(items).Select(c=>(int)Get(c,"Type")).Where(t=>t>0));
                Call(popup,"Open",Get(config,"Rect"));Call(popup,"Prepare",matrix,new Vector2(size[0],size[1]),true);Require((bool)Get(popup,"Visible"),"capture configuration fits tested viewport");
                graphics.Image(Path.Combine(output,"tools-capture-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>{Call(renderer,"Draw",state,matrix,false,false);Call(items,"Draw",Get(shell,"drawKeyboard"),false);Call(popup,"Draw");},matrix,size[0],size[1]);Call(popup,"Close");
                NativeToolsUiChecks.Prepare(context,1,size[0],size[1],scale);var mining=Get(shell,"MiningUi");Call(state,"ScrollTo",(float)Get(mining,"Height"));NativeToolsUiChecks.Prepare(context,1,size[0],size[1],scale);
                graphics.Image(Path.Combine(output,"tools-mining-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>{Call(renderer,"Draw",state,matrix,false,false);Call(Get(shell,"RecoveryUi"),"Draw",Get(shell,"drawKeyboard"),false);Call(Get(shell,"ReforgeUi"),"Draw",Get(shell,"drawKeyboard"));Call(mining,"Draw",Get(shell,"drawKeyboard"));},matrix,size[0],size[1]);
            }
            Call(shell,"CloseAndSubmitPosition");Overlay(context,graphics,output);Console.WriteLine("PASS G09 original-resource controls at normal, 150 percent and short viewports.");
        }
        private static void Overlay(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"Tools");var mining=Get(host,"Mining");var p=Main.LocalPlayer;Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=new Vector2(480,480);
            p.inventory[0].SetDefaults(Terraria.ID.ItemID.CopperPickaxe);p.itemTime=p.itemAnimation=0;p.selectedItemState.Select(0);p.selectedItemState.Update();p.position=new Vector2(640,640);
            for(int x=35;x<52;x++)for(int y=35;y<47;y++)Main.tile[x,y].ClearEverything();foreach(int x in new[]{42,45,48})NativeToolsChecks.Tile(x,40,6);
            NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,42,40,6,false),"overlay actual selected region");Main.tile[45,40].inActive(true);
            foreach(float gravity in new[]{1f,-1f})foreach(float scale in new[]{1f,1.5f})
            {
                p.gravDir=gravity;Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0,0,960,640));Main.GameViewMatrix.Zoom=new Vector2(scale);Call(mining,"Update");
                long before=(long)Get(mining,"OverlayChecks");var pixels=graphics.Pixels(()=>Call(mining,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                Require((long)Get(mining,"OverlayChecks")==before,"world overlay Draw performs no eligibility evaluation");
                var painted=pixels.Where(c=>c.A>0).ToArray();Require(painted.Length>0 && painted.Any(c=>c.G>c.R) && painted.Any(c=>c.R>c.G),"actual overlay pixels contain prepared eligible green and blocked red");
                Require(painted.All(c=>c.R<=c.A && c.G<=c.A && c.B<=c.A),"AlphaBlend uses actual premultiplied low-noise overlay pixels");
                var expected=Vector2.Transform(Main.ReverseGravitySupport(new Vector2(42*16+8,40*16+8)-Main.screenPosition),Main.GameViewMatrix.ZoomMatrix);int center=(int)expected.Y*960+(int)expected.X;
                Require(center>=0 && center<pixels.Length && pixels[center].G>pixels[center].R,"actual world camera/zoom/reverse-gravity target center matches green overlay");
                graphics.Scene(()=>Call(mining,"Draw"),Path.Combine(output,"tools-world-"+gravity+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));
            }
            Main.hideUI=true;Require(graphics.Pixels(()=>Call(mining,"Draw"),Main.GameViewMatrix.ZoomMatrix).All(c=>c.A==0),"hidden native HUD emits no mining pixels");Main.hideUI=false;
            Main.screenPosition=new Vector2(3000,3000);Require(graphics.Pixels(()=>Call(mining,"Draw"),Main.GameViewMatrix.ZoomMatrix).All(c=>c.A==0),"offscreen region is clipped without world queries");
            NativeToolsChecks.SetMode(host,2,0);p.gravDir=1;Main.screenPosition=Vector2.Zero;Main.GameViewMatrix.Zoom=Vector2.One;
            Console.WriteLine("PASS G09 actual mining overlay pixels: premultiplied green/red, camera/zoom/reverse gravity, hide/offscreen clipping and no Draw eligibility work.");
        }
    }
}
