using System;
using System.IO;
using System.Collections;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using System.Reflection;
using JueMingR.Features.Footprints;
using JueMingR.Platform.Footprints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprintVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(ProbeGraphics graphics, string output, bool popupOnly = false)
        {
            Directory.CreateDirectory(output);
            Main.gameMenu = Main.dedServ = Main.hideUI = Main.mapFullscreen = false; Main.netMode = Main.myPlayer = 0;
            Main.screenWidth = 960; Main.screenHeight = 640; typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, position = new Vector2(1600, 800), gravDir = 1 };
            string root = Path.Combine(Terraria.Program.SavePath, "footprint-visual");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.Map = new Terraria.Map.WorldMap(4200, 1200);
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "footprints-" + new string('8', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true); var host = Get(context, "Footprints");
                Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Recorder") != null; });
                var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer"); var popup = Get(shell, "FootprintPopup"); Call(renderer, "RefreshResources");
                var prepare = popup.GetType().GetMethod("Prepare", Flags); var measure = Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
                Action<float, float> layout = (w, h) => prepare.Invoke(popup, new object[] { w, h, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration") });
                if (popupOnly) { PopupChecks(context, host, popup, renderer, graphics, output, layout); return; }
                Call(popup, "Open", 2); layout(960, 640);
                graphics.Image(Path.Combine(output, "footprints-config.png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity);
                layout(640, 220); graphics.Image(Path.Combine(output, "footprints-config-small.png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity, 640, 220);
                var panel = Get(popup, "Panel"); Require((float)Get(panel, "Bottom") <= 220, "small configuration stays inside viewport");
                var confirmation = (FootprintClearConfirmation)Get(popup, "confirmation"); string generation = (string)Get(host, "Generation");
                for (int stage = 1; stage <= 2; stage++)
                {
                    confirmation.Presented(); confirmation.Press(generation); confirmation.Release(generation); Set(popup, "dirty", true); layout(640, 220);
                    graphics.Image(Path.Combine(output, "footprints-confirm-" + stage + ".png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity, 640, 220);
                }
                Call(popup, "Close"); Call(host, "SetDisplay", true); Main.mapFullscreen = true;
                var recorder = (FootprintRecorder)Get(host, "Recorder");
                recorder.Observe(100.25f, 100.25f, FootprintPosition.Valid, true); recorder.Observe(300.25f, 100.25f, FootprintPosition.Valid, false);
                recorder.Observe(300.25f, 250.25f, FootprintPosition.Valid, false); recorder.Observe(450.25f, 250.25f, FootprintPosition.Valid, true);
                recorder.Observe(550.25f, 350.25f, FootprintPosition.Valid, false, 201L * 3600 * FootprintSample.TicksPerSecond);
                var layer = Get(host, "Layer"); Call(layer, "Update");
                var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView");
                object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
                var pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
                int greenRows = 0; for (int y = 0; y < 640; y++) if (pixels[y * 960 + 200].G > 100) greenRows++;
                Require(greenRows == 2, "actual MagicPixel draws horizontal route at exactly two screen pixels");
                Require(pixels[150 * 960 + 200].A == 0 && pixels[250 * 960 + 400].A == 0, "no full-texture rectangle or cross-segment chord");
                long projections = (long)Get(layer, "ProjectedPoints");
                for (int i = 0; i < 20; i++) graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
                Require((long)Get(layer, "ProjectedPoints") == projections, "steady native draw never reprojects fixed geometry");
                graphics.Image(Path.Combine(output, "footprints-map-201h.png"), () => { Call(layer, "DrawRoutes", frame); Main.spriteBatch.End(); Call(layer, "DrawBar", frame); Main.spriteBatch.Begin(); }, Matrix.Identity);
                var playback = (FootprintPlayback)Get(layer, "Playback"); playback.Seek(2, recorder.End);
                graphics.Image(Path.Combine(output, "footprints-paused.png"), () => { Call(layer, "DrawRoutes", frame); Main.spriteBatch.End(); Call(layer, "DrawBar", frame); Main.spriteBatch.Begin(); }, Matrix.Identity);
                NativeFootprint200HourVisualChecks.Run(graphics, host, assembly, output);
                string generated = Environment.GetEnvironmentVariable("JUEMINGR_FOOTPRINTS_200H_ARCHIVE"); if (!String.IsNullOrEmpty(generated)) NativeFootprint200HourVisualChecks.RunLongArchive(graphics, host, assembly, output, generated, context);
                Console.WriteLine("PASS: original XNB footprint config/three-stage labels/compact viewport, 201h timeline and real two-pixel route pixels; inspect PNG layout separately.");
            }
            finally { Main.gameMenu = true; Call(context, "UpdateRuntime"); StopContext(context); Main.gameMenu = false; }
        }
        private static void PopupChecks(object context, object host, object popup, object renderer, ProbeGraphics graphics, string output, Action<float,float> layout)
        {
            PlayerInput.Triggers.Initialize();
            object input=Get(context,"Input");bool focused=true;
            Set(input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Set(input,"foregroundWindow",(Func<IntPtr>)(()=>focused?new IntPtr(1):IntPtr.Zero));
            Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"CanClear");});
            var underline=popup.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Layout").GetMethod("ButtonUnderline",Flags);
            float logicalWidth=960,logicalHeight=640,ui=1;int width=960,height=640;float mouseX=0,mouseY=0;
            Action prepare=()=>layout(logicalWidth,logicalHeight);
            Action<bool> frame=down=>{
                FocusHelper.IsSelectedApplication=focused;
                Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState((int)(mouseX*ui),(int)(mouseY*ui),0,down?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                Call(input,"AfterMapping");Main.keyState=new KeyboardState();Call(input,"AfterKeyboardRefresh");
                Call(popup,"Process",true,2,mouseX,mouseY,(bool)Call(popup,"Matches",logicalWidth,logicalHeight,Get(renderer,"FontIdentity"),Get(renderer,"SkinGeneration")));prepare();
            };
            Action<int> point=command=>{var commands=(IList)Get(popup,"Commands");var buttons=(IList)Get(popup,"Buttons");var r=Get(buttons[commands.IndexOf(command)],"Rect");var panel=Get(popup,"Panel");mouseX=(float)Get(panel,"X")+(float)Get(r,"X")+5;mouseY=(float)Get(panel,"Y")+(float)Get(r,"Y")+5;};
            Action<int> click=command=>{point(command);frame(true);frame(false);};
            Action geometry=()=>{
                var panel=Get(popup,"Panel");var buttons=((IEnumerable)Get(popup,"Buttons")).Cast<object>().ToArray();var text=((IEnumerable)Get(popup,"Text")).Cast<object>().ToArray();
                Require((float)Get(panel,"Bottom")<=logicalHeight && (float)Get(panel,"Right")<=logicalWidth,"configuration panel fits logical viewport");
                var rects=buttons.Concat(text).Select(e=>Get(e,"Rect")).ToArray();
                foreach(var r in rects)Require((float)Get(r,"X")>=0 && (float)Get(r,"Y")>=0 && (float)Get(r,"Right")<=(float)Get(panel,"Width") && (float)Get(r,"Bottom")<=(float)Get(panel,"Height"),"actual measured content stays inside panel");
                for(int i=0;i<rects.Length;i++)for(int j=i+1;j<rects.Length;j++)
                {var a=rects[i];var b=rects[j];Require((float)Get(a,"Right")<=(float)Get(b,"X") || (float)Get(b,"Right")<=(float)Get(a,"X") || (float)Get(a,"Bottom")<=(float)Get(b,"Y") || (float)Get(b,"Bottom")<=(float)Get(a,"Y"),"actual text and controls do not overlap");}
                object first=buttons[0];for(int i=0;i<100;i++)prepare();Require(ReferenceEquals(first,((IList)Get(popup,"Buttons"))[0]),"100 stable Prepare calls preserve layout objects");
            };
            Action<string> image=name=>{
                geometry();var commands=(IList)Get(popup,"Commands");var buttons=(IList)Get(popup,"Buttons");var panel=Get(popup,"Panel");
                var pixels=graphics.Pixels(()=>Call(renderer,"DrawFootprintPopup",popup),Main.UIScaleMatrix,width,height);
                Func<int,Color> mark=command=>{var line=underline.Invoke(null,new[]{buttons[commands.IndexOf(command)]});int x=(int)(((float)Get(panel,"X")+(float)Get(line,"X")+(float)Get(line,"Width")/2)*ui),y=(int)(((float)Get(panel,"Y")+(float)Get(line,"Y")+.5f)*ui);return pixels[y*width+x];};
                foreach(int c in new[]{2,4})if(commands.Contains(c))Require(mark(c)!=new Color(255,145,130),"clear and clear-retry action have no selected underline");
                Require(mark(1)==((bool)Get(host,"Recording")?Color.LightGreen:Color.LightGray),"recording selection underline remains the real preference projection");
                graphics.Image(Path.Combine(output,name+".png"),()=>Call(renderer,"DrawFootprintPopup",popup),Main.UIScaleMatrix,width,height);
            };
            foreach(var size in new[]{new[]{960,640,100},new[]{640,220,100},new[]{960,540,150}})
            {
                width=size[0];height=size[1];ui=size[2]/100f;logicalWidth=width/ui;logicalHeight=height/ui;
                Main.screenWidth=width;Main.screenHeight=height;typeof(Main).GetField("_uiScaleUsed",Flags).SetValue(null,ui);typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.CreateScale(ui));
                frame(false);frame(false);Call(popup,"Open",2);prepare();image("config-"+width+"-"+height+"-"+size[2]);
                Call(host,"SetRecording",false);prepare();image("recording-off-"+width+"-"+size[2]);Call(host,"SetRecording",true);prepare();
                // Controlled immutable snapshots exercise UI projection only.
                // No runtime/worker command is dispatched while the gate is held;
                // restore the real snapshot before releasing it. This is not an
                // I/O failure or deletion-recovery proof.
                object store=Get(host,"store");lock(Get(store,"gate"))
                {
                    object original=Get(store,"state");
                    try{foreach(var state in new[]{new[]{false,true},new[]{true,false},new[]{true,true}})
                    {
                        bool clearing=state[0],retry=state[1];
                        object snapshot=Activator.CreateInstance(original.GetType(),Flags,null,new object[]{true,(string)Get(original,"Generation"),(long)Get(original,"Count"),(long)Get(original,"End"),(long)Get(original,"Segment"),clearing,false,retry?"isolated-ui-state":null,false,!clearing&&retry},null);
                        Set(store,"state",snapshot);prepare();
                        if(clearing)Require(((IEnumerable)Get(popup,"Text")).Cast<object>().Any(e=>(string)Get(e,"Text")== (string)Get(host,"StatusMessage")),"clear projection displays the actual clearing status instead of claiming active recording");
                        image((!clearing?"save-retry":retry?"clear-retry":"clearing")+"-"+width+"-"+height+"-"+size[2]);
                    }}finally{Set(store,"state",original);prepare();}
                }
                Call(popup,"Close");
            }
            // All following deletions target only this process's generated
            // fixture identity and temporary footprint root.
            width=960;height=640;ui=1;logicalWidth=960;logicalHeight=640;Main.screenWidth=width;Main.screenHeight=height;typeof(Main).GetField("_uiScaleUsed",Flags).SetValue(null,1f);typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.Identity);
            string generation=(string)Get(host,"Generation");var confirmation=(FootprintClearConfirmation)Get(popup,"confirmation");
            frame(false);Call(popup,"Open",2);prepare();click(2);Require(confirmation.Stage==0 && (string)Get(host,"Generation")==generation,"unpresented physical click cannot authorize clearing");
            image("confirmation-0");click(2);Require(confirmation.Stage==1,"first presented physical click advances once");click(2);click(2);Require(confirmation.Stage==1,"updates without a new Draw cannot skip a stage");
            image("confirmation-1");click(3);Require(confirmation.Stage==0,"physical cancel retires confirmation");
            image("geometry-before");click(2);image("geometry-armed");point(2);frame(true);logicalWidth-=10;prepare();frame(false);Require(confirmation.Stage==0 && (string)Get(host,"Generation")==generation,"old geometry release cannot clear or advance");logicalWidth=960;prepare();
            image("focus-before");click(2);point(2);frame(true);focused=false;frame(false);Require(!(bool)Get(popup,"Visible") && (string)Get(host,"Generation")==generation,"focus loss cancels popup and armed clear");focused=true;frame(false);frame(false);
            Call(popup,"Open",2);prepare();for(int stage=0;stage<3;stage++){image("physical-confirm-"+stage);click(2);if(stage<2)Require((string)Get(host,"Generation")==generation,"first two physical confirmations preserve archive");}
            Until(()=>{Call(context,"UpdateRuntime");return !(bool)Get(host,"Clearing") && (string)Get(host,"Generation")!=generation;});
            Require((bool)Get(host,"Recording"),"three presented clicks clear isolated archive and preserve recording preference");
            Call(popup,"Close");
            Console.WriteLine("PASS footprint popup-only actual font/layout/underline/state projections, stable Prepare, physical three presented clicks/cancel/geometry/focus; no route/history workload.");
        }
        private static void Until(Func<bool> predicate) { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (clock.ElapsedMilliseconds > 10000) throw new TimeoutException("footprint visual ready"); System.Threading.Thread.Sleep(5); } }
    }
}
