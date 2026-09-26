using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeShortFeedbackChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeShortFeedbackVisualChecks
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);
            object display=Get(context,"ShortFeedback"), shell=Get(context,"Shell");
            var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");
            Set(Get(context,"onboarding"),"CanPresent",(Func<bool>)(()=>false));
            Action reset=()=>{Call(display,"Clear");PopupText.ClearAll();Main.playerInventory=Main.gameMenu=Main.hideUI=Main.mapFullscreen=false;Main.showItemText=true;Main.LocalPlayer.dead=false;Main.LocalPlayer.gravDir=1;Viewport(960,640,1);Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=new Vector2(100,100);Main.LocalPlayer.position=new Vector2(560,430);};
            Action trigger=()=>{Require(registry.Find("items.auto-stack.toggle").Invoke(HotkeyContext.SinglePlayer),"actual graphical command");Call(context,"UpdateRuntime");};
            Action animate=()=>{for(int i=0;i<12;i++){PopupText.UpdateItemText();Call(display,"Refresh");}};
            Action native=()=>PopupText.DrawItemTextPopups(PopupText.TargetScale);
            reset();trigger();
            var entry=Entries(display).Single();Require(!(bool)Get(entry,"Fallback"),"real font admitted native head popup");
            double expiry=(double)Get(entry,"Expires");
            Require(graphics.Pixels(native,Main.GameViewMatrix.ZoomMatrix).All(c=>c.A==0),"NewText starts at native zero scale");
            animate();var pixels=graphics.Pixels(native,Main.GameViewMatrix.ZoomMatrix);var bounds=Bounds(pixels);
            Require(bounds.Width>80 && bounds.Height>10 && bounds.Bottom<330 && Math.Abs(bounds.Center.X-470)<5,"real XNB pixels are centered above the player's head");
            Require(pixels.Any(c=>c.A>100 && c.R<20 && c.G<20 && c.B<20),"actual native glyph outline");
            graphics.Image(Path.Combine(output,"head-normal.png"),native,Main.GameViewMatrix.ZoomMatrix);
            Main.screenPosition+=new Vector2(80,30);var camera=Bounds(graphics.Pixels(native,Main.GameViewMatrix.ZoomMatrix));
            Require(camera.X==bounds.X-80 && camera.Y==bounds.Y-30,"native camera projection matches text position");Main.screenPosition-=new Vector2(80,30);
            Viewport(960,640,1.5f);Main.GameViewMatrix.Zoom=new Vector2(1.25f);animate();var scaled=Bounds(graphics.Pixels(native,Main.GameViewMatrix.ZoomMatrix));
            Require(scaled.Width>bounds.Width*1.35 && scaled.Width<bounds.Width*1.7,"native game/UI scaling uses real font width");
            graphics.Image(Path.Combine(output,"head-scaled.png"),native,Main.GameViewMatrix.ZoomMatrix);
            Require((double)Get(entry,"Expires")==expiry,"layout changes never restart lifetime");
            reset();Main.LocalPlayer.gravDir=-1;trigger();animate();var inverted=Bounds(graphics.Pixels(native,Main.GameViewMatrix.ZoomMatrix));
            Require(inverted.Bottom<Main.screenHeight-(Main.LocalPlayer.Bottom.Y-Main.screenPosition.Y),"inverted gravity stays on screen head side");
            graphics.Image(Path.Combine(output,"head-inverted.png"),native,Main.GameViewMatrix.ZoomMatrix);

            reset();Main.showItemText=false;trigger();entry=Entries(display).Single();
            Action fallback=()=>Call(display,"Draw");
            var fallbackPixels=graphics.Pixels(fallback,Main.UIScaleMatrix);var fb=Bounds(fallbackPixels);
            Require(fb.Top>=80 && fb.Bottom<150 && Math.Abs(fb.Center.X-480)<4 && !Main.showItemText,"item-text OFF fallback has actual centered outlined pixels without changing option");
            Require(PopupText.popupText.All(p=>!p.active),"fallback does not duplicate native text");
            graphics.Image(Path.Combine(output,"item-text-off.png"),fallback,Main.UIScaleMatrix);
            object renderer=Get(entry,"Renderer");int layouts=(int)Get(renderer,"Layouts");
            for(int i=0;i<100;i++)Call(display,"Refresh");
            Require((int)Get(renderer,"Layouts")==layouts,"stable text/font/scale/viewport does not remeasure");
            Viewport(640,360,1.5f);graphics.Pixels(fallback,Main.UIScaleMatrix);
            Require((int)Get(renderer,"Layouts")==layouts+1,"actual viewport/UI change invalidates layout once");
            graphics.Image(Path.Combine(output,"fallback-small-150.png"),fallback,Main.UIScaleMatrix,640,360);
            reset();trigger();entry=Entries(display).Single();expiry=(double)Get(entry,"Expires");
            Main.mapFullscreen=true;Call(display,"Refresh");
            var mapType=display.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.MapView");
            object frame=Activator.CreateInstance(mapType,Flags,null,new object[]{Vector2.Zero,Vector2.Zero,1f,1f,255},null);
            Action mapDraw=()=>{Main.spriteBatch.End();Call(display,"DrawMap",frame);Main.spriteBatch.Begin();};
            Require(Bounds(graphics.Pixels(mapDraw,Matrix.Identity)).Width>80,"actual map Overlay batch produces fallback pixels");
            Require((double)Get(entry,"Expires")==expiry && PopupText.popupText.All(p=>!p.active),"map transition releases own native slot without duplicate/restart");
            graphics.Image(Path.Combine(output,"fullscreen-map.png"),mapDraw,Matrix.Identity);
            Main.mapFullscreen=false;graphics.Pixels(fallback,Main.UIScaleMatrix);
            Require((double)Get(entry,"Expires")==expiry && (bool)Get(entry,"Fallback"),"return from map never replays native admission");
            reset();Main.showItemText=false;
            foreach(string id in new[]{"items.auto-stack.toggle","items.auto-sell.toggle","items.auto-discard.toggle","biome-display.toggle"})registry.Find(id).Invoke(HotkeyContext.SinglePlayer);
            var four=Bounds(graphics.Pixels(fallback,Main.UIScaleMatrix));Require(Entries(display).Length==4 && four.Height>60 && four.Bottom<300,"four bounded readable lines");
            graphics.Image(Path.Combine(output,"four-results.png"),fallback,Main.UIScaleMatrix);
            Call(display,"Clear");Main.showItemText=true;
            Console.WriteLine("PASS: real .8 popup/XNB pixels, head/camera/inverted/zoom/UI, full-text fallback/map batch, stable layouts and unchanged lifetime.");
        }
        private static void Viewport(int width,int height,float scale)
        {
            Main.screenWidth=width;Main.screenHeight=height;
            typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,width);typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,height);
            typeof(Main).GetField("_uiScaleUsed",Flags).SetValue(null,scale);typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.CreateScale(scale));
            Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,width,height));
        }
        private static Rectangle Bounds(Color[] pixels)
        {
            int left=960,right=-1,top=640,bottom=-1;
            for(int i=0;i<pixels.Length;i++)if(pixels[i].A>0){int x=i%960,y=i/960;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            Require(right>=left,"nonempty actual glyph pixels");return new Rectangle(left,top,right-left+1,bottom-top+1);
        }
    }
}
