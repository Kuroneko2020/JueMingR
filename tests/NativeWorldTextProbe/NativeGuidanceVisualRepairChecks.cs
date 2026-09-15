using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceVisualRepairChecks
    {
        private const BindingFlags Flags = NativeGuidanceChecks.Flags;
        internal static void Stroke(ProbeGraphics graphics, Assembly assembly)
        {
            const string message = "装备存在非战斗用品";
            var text = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Guidance.GuidanceText"), true);
            Call(text,"Prepare",graphics.Font,message,1.25f,900f);
            var metrics = new JueMingR.TerrariaHost.F5.UiTextMetrics(); var size = metrics.Measure(graphics.Font,message);
            var position = new Vector2(64+((float)Get(text,"Width")-size.Width*1.25f)/2-size.OffsetX*1.25f,66-size.OffsetY*1.25f);
            foreach(float alpha in new[]{1f,.5f})
            {
                Color color=Color.Gold*alpha;
                var actual=graphics.Pixels(()=>Call(text,"Draw",Main.spriteBatch,new Vector2(64),Matrix.Identity,color),Matrix.Identity);
                var expected=graphics.Pixels(()=>Utils.DrawBorderStringFourWay(Main.spriteBatch,graphics.Font,message,position.X,position.Y,color,Color.Black*(color.A/255f),Vector2.Zero,1.25f),Matrix.Identity);
                Require(actual.SequenceEqual(expected),"Guidance uses the actual native four-way black outline, including fade alpha");
            }
        }
        internal static void Cadence(ProbeGraphics graphics, object context, object host)
        {
            // This oracle isolates sampling cadence, not the independently
            // tested 3.25-second lifetime. Slow GPU readback cannot expire it.
            var clock=(System.Diagnostics.Stopwatch)Get(host,"clock"); clock.Stop();
            try { ((EquipmentWarning)Get(host,"Equipment")).Clear(); CadenceCore(graphics,context,host); }
            finally { clock.Start(); }
        }
        private static void CadenceCore(ProbeGraphics graphics, object context, object host)
        {
            object input=Get(context,"Input"),world=Get(host,"World");
            Set(input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));
            FocusHelper.IsSelectedApplication=true;Main.keyState=default(Microsoft.Xna.Framework.Input.KeyboardState);PlayerInput.MouseInfo=default(Microsoft.Xna.Framework.Input.MouseState);
            Call(input,"BeginUpdate");Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,true);
            Main.npc[1].GivenName="松露虫";Main.npc[1].position=Main.LocalPlayer.Center+new Vector2(900,0);
            Call(context,"UpdateRuntime");Call(world,"Prepare");
            var expected=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            Require(expected.Any(p=>p.A>0),"cadence reference contains actual rendered content");
            for(int i=0;i<12;i++)
            {
                Call(input,"BeginUpdate");if(i%3==0){Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");}
                Call(context,"UpdateRuntime");Call(world,"Prepare");
                var actual=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                Require(actual.SequenceEqual(expected),"consecutive actual GPU guidance frames remain identical without fresh input");
            }
            Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            F5Cadence(graphics,context,input);
            Console.WriteLine("PASS: native four-way outline pixel oracle and 12 actual Draw frames across missing input samples.");
        }
        private static void F5Cadence(ProbeGraphics graphics, object context, object input)
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer");
            Set(shell,"LayersReady",true);Set(state,"Ready",true);Call(state,"Navigate",1);Call(state,"RestoreVisible");Call(renderer,"Prepare",state,960f,640f,1f);
            typeof(PlayerInput).GetField("RawMouseScale",Flags).SetValue(null,Vector2.One);PlayerInput.Triggers.Initialize();
            Action<int,int> frame=(x,y)=>
            {
                Main.LocalPlayer.mouseInterface=Main.mouseText=false;Main.keyState=default(Microsoft.Xna.Framework.Input.KeyboardState);
                PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(x,y,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
                PlayerInput.Triggers.Reset();Call(input,"BeginUpdate");Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");Call(shell,"AfterUpdate");
            };
            frame(940,620);object layout=Get(state,"Layout"),view=Get(layout,"Viewport");
            object name=((System.Collections.IEnumerable)Get(layout,"Elements")).Cast<object>().First(e=>GetOptional(e,"Description")!=null);object rect=Get(name,"Rect");
            frame((int)((float)Get(state,"X")+(float)Get(view,"X")+(float)Get(rect,"X")+8),(int)((float)Get(state,"Y")+(float)Get(view,"Y")+(float)Get(rect,"Y")-(float)Get(state,"Scroll")+8));
            var expected=graphics.Pixels(()=>Call(shell,"DrawLayer"),Matrix.Identity);
            Require(!(bool)Get(shell,"Failed")&&(bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual F5 name hint pixels are prepared");
            for(int i=0;i<6;i++)
            {
                Call(input,"BeginUpdate");Call(context,"UpdateRuntime");Call(context,"UpdateShell");
                var actual=graphics.Pixels(()=>Call(shell,"DrawLayer"),Matrix.Identity);
                Require((bool)Get(Get(renderer,"HintLayout"),"Visible")&&actual.SequenceEqual(expected),"actual F5 tooltip Draw remains identical on unsampled updates");
            }
            Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
        }
        internal static void LargeWindow(ProbeGraphics graphics, object context, object host, string output)
        {
            Main.screenWidth=2560;Main.screenHeight=1400;
            Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0,0,2560,1400));
            Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=Vector2.Zero;Main.LocalPlayer.position=new Vector2(1270,680);
            Main.npc[1].position=Main.LocalPlayer.Center+new Vector2(0,950);Main.npc[2].position=new Vector2(3600,500);
            typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,2560);typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,1400);
            object information=Get(context,"Information"),world=Get(host,"World");
            NativeGuidanceChecks.Until(()=>(bool)Get(information,"CanConfigure"));
            foreach(JueMingR.Platform.Information.InformationKind kind in Enum.GetValues(typeof(JueMingR.Platform.Information.InformationKind)))Call(information,"SetEnabled",kind,true);
            foreach(float ui in new[]{1f,1.25f,1.5f})
            {
                var matrix=Matrix.CreateScale(ui,ui,1);typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,matrix);
                Call(host,"SetEnabled",GuidanceKind.Equipment,false);Call(host,"SetEnabled",GuidanceKind.Equipment,true);
                Call(context,"UpdateRuntime");Call(world,"Prepare");Call(information,"PrepareHud");
                Require((bool)Get(world,"rareOutside")&&(bool)Get(world,"merchantVisible")&&(bool)Get(world,"equipmentVisible"),"large window contains all three actual hints");
                graphics.Image(Path.Combine(output,"guidance-2560x1400-ui-"+(int)(ui*100)+".png"),()=>
                {
                    Main.spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,new Rectangle(1260,680,40,50),new Rectangle(0,0,1,1),Color.SteelBlue);
                    Call(world,"Draw");Main.spriteBatch.End();
                    Main.spriteBatch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred,null,null,null,null,null,matrix);
                    Call(Get(information,"Hud"),"Draw",Main.spriteBatch,false);
                },Matrix.Identity,2560,1400);
                Require((int)Get(world,"Failures")==0,"large window render stays healthy");
            }
            var uiMatrix=Matrix.CreateScale(1.25f,1.25f,1);typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,uiMatrix);
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer"),popup=Get(shell,"StylePopup");
            Call(state,"Navigate",2);Call(state,"RestoreVisible");Call(renderer,"Prepare",state,2560f,1400f,1.25f);
            var click=popup.GetType().GetMethods(Flags).Single(m=>m.Name=="Click"&&m.GetParameters()[0].ParameterType==typeof(GuidanceKind));
            foreach(GuidanceKind kind in new[]{GuidanceKind.Rare,GuidanceKind.Merchant})
            {
                var anchor=Activator.CreateInstance(shell.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"));
                click.Invoke(popup,new[]{(object)kind,anchor,2});
                var prepare=popup.GetType().GetMethod("Prepare",Flags);var measure=Delegate.CreateDelegate(prepare.GetParameters()[4].ParameterType,renderer,renderer.GetType().GetMethod("PopupMeasure",Flags));
                prepare.Invoke(popup,new[]{(object)(2560f/1.25f),1400f/1.25f,1.25f,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration"),Call(shell,"StyleAnchor")});
                Require((bool)Get(popup,"Visible"),"new style popup has a readable large-window layout");
                graphics.Image(Path.Combine(output,"guidance-style-"+kind+".png"),()=>{Call(renderer,"Draw",state,uiMatrix,false,false);Call(renderer,"DrawStylePopup",popup);},uiMatrix,2560,1400);
                Call(popup,"Close");
            }
        }
    }
}
