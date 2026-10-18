using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // A disposable native Main initializes its own device/content/menu before
    // drawing a deterministic owned scene. No owner save, cloud, or installation
    // is used; this is a rendering specialist, not ordinary package load proof.
    internal static class NativeViewOriginalChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static int Run(string content, string output,bool waterRepair=false)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            Terraria.Program.SavePath = Path.Combine(output, "isolated-user"); Directory.CreateDirectory(Terraria.Program.SavePath);
            return RunNative(content, output,waterRepair);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int RunNative(string content, string output,bool waterRepair)
        {
            NativeTestAudio.Install(typeof(Main).Assembly);
            AppDomain.MonitoringIsEnabled = true;
            // Program.RunGame normally installs its all-assembly JIT preload
            // callback. This specialist skips that unrelated warmup through the
            // original native test flag, otherwise DrawSplash waits forever for
            // Program.LoadedEverything despite completed assets.
            Main.SkipAssemblyLoad = true;
            Require(GraphicsAdapter.Adapters.Count > 0, "Native view requires an actual XNA device.");
            // Explicit native None selects no Steam/cloud modules. This test
            // does not call Program.LaunchGame, whose client default is Steam.
            Terraria.Social.SocialAPI.Initialize(Terraria.Social.SocialMode.None);
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US"); Lang.InitializeLegacyLocalization();
            string old = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = output;
                using (var game = new ViewGame(content, output,waterRepair))
                {
                    // XNA's nonblocking entry performs CreateDevice and the
                    // complete native Initialize first. Explicit Tick then
                    // drives its native loop in a disconnected desktop session.
                    typeof(Game).GetMethod("StartGameLoop", F).Invoke(game, null);
                    while (!game.Complete) game.Tick();
                }
            }
            finally { Environment.CurrentDirectory = old; Terraria.Social.SocialAPI.Shutdown(); }
            return 0;
        }

        private sealed class ViewGame : Main
        {
            private readonly string output, content;
            private readonly Stopwatch deadline = Stopwatch.StartNew();
            private readonly Harmony device = new Harmony("JueMingR.Tests.View.Devices");
            private object context, input, view;
            private int frame;
            private bool prepared;
            private readonly bool waterRepair;
            private bool ordinaryImage, wideImage, retainedImage;
            private readonly HashSet<string> images = new HashSet<string>();
            private readonly Dictionary<string, int> stableRebuilds = new Dictionary<string, int>();
            private readonly Dictionary<string, int> stableTargets = new Dictionary<string, int>();
            private readonly Dictionary<string,int> stableScenes=new Dictionary<string,int>(),stableWaters=new Dictionary<string,int>();
            private bool throwBackgroundEnd;
            private bool caveWaterfalls;
            private readonly Dictionary<string,List<double>> drawTimes = new Dictionary<string,List<double>>();
            private readonly Dictionary<string,long> drawAllocations = new Dictionary<string,long>();
            private int legacyRebuilds, targetInitializations;
            internal bool Complete { get; private set; }
            private static KeyboardState keyboard;
            private static RenderTarget2D capture;
            private static bool redirectOutput;
            internal ViewGame(string content, string output,bool waterRepair)
            {
                this.output = output; this.content = content;this.waterRepair=waterRepair;
                // Caller supplies a local Content directory alias rooted in the
                // test output because native XnaContentSource takes relative URI.
                Content.RootDirectory = "Content";
                Require(Directory.Exists(Path.Combine(output, "Content")), "Owned Content alias required.");
                Configuration.Put("Fullscreen", false); Configuration.Put("DisplayWidth", 1920); Configuration.Put("DisplayHeight", 1080);
                Configuration.Put("Support4K", false); Configuration.Put("MusicVolume", 0f); Configuration.Put("SoundVolume", 0f); Configuration.Put("AmbientVolume", 0f);
                device.Patch(typeof(Keyboard).GetMethod("GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(ViewGame), nameof(ReadKeyboard)));
                device.Patch(typeof(Mouse).GetMethod("GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(ViewGame), nameof(ReadMouse)));
                device.Patch(typeof(GraphicsDevice).GetMethod("SetRenderTarget", new[] { typeof(RenderTarget2D) }), prefix: new HarmonyMethod(typeof(ViewGame), nameof(PhysicalOutput)));
                activeGame = this;


            }
            private static ViewGame activeGame;
            private static void LegacyBuilt() { activeGame.legacyRebuilds++; }
            private static void TargetsBuilt() { activeGame.targetInitializations++; }
            private static void PhysicalOutput(ref RenderTarget2D __0) { if (__0 == null && redirectOutput && capture != null) __0 = capture; }
            private static bool ReadKeyboard(ref KeyboardState __result) { __result = keyboard; return false; }
            private static bool ReadMouse(ref MouseState __result)
            { __result = new MouseState(1400, 540, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released); return false; }
            protected override void Initialize() { Console.WriteLine("VIEW original Initialize begin"); base.Initialize(); Console.WriteLine("VIEW original Initialize complete"); device.Patch(typeof(Terraria.Graphics.Light.LegacyLighting).GetMethod("Rebuild"), postfix: new HarmonyMethod(typeof(ViewGame), nameof(LegacyBuilt))); device.Patch(typeof(Main).GetMethod("InitTargets", F), postfix: new HarmonyMethod(typeof(ViewGame), nameof(TargetsBuilt))); }
            protected override void LoadContent() { Console.WriteLine("VIEW original LoadContent begin"); base.LoadContent(); Console.WriteLine("VIEW original LoadContent complete"); }
            protected override void Update(GameTime time)
            {
                if (deadline.Elapsed.TotalSeconds > 120) throw new TimeoutException("Native view specialist timed out before completion, frame=" + frame);
                // DrawSplash owns the original deferred asset load. This
                // deterministic render specialist never runs native simulation.
                if (showSplash) return;
                if (!prepared && Environment.GetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_ONLY") == "1")
                {
                    prepared = true;
                    NativeViewNetworkChecks.RunOnInitializedMain(content, output, NetworkDraw);
                    Complete = true; Exit(); return;
                }
                if (!prepared)
                {
                    if(Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_PROFILE")=="HiDef")
                    {
                        // Select the native profile before composing the owned
                        // world/sink/context. Mid-scene switching also retires
                        // native map/capture resources unrelated to VIEW, and
                        // cannot validate a newly initialized profile fairly.
                        Require(GraphicsDevice.Adapter.IsProfileSupported(GraphicsProfile.HiDef),"isolated native HiDef profile supported by actual adapter");
                        Main.SetGraphicsProfile(GraphicsProfile.HiDef,false);
                        Require(GraphicsDevice.GraphicsProfile==GraphicsProfile.HiDef,"native profile switch actually remained HiDef");
                    }
                    Prepare(); prepared = true;
                }
                frame++;
                FocusHelper.IsSelectedApplication = true;
                Call(input, "BeginUpdate");
                keyboard = new KeyboardState(frame >= 140 && frame < 165 || frame >= 260 && frame < 270 || frame >= 475 && frame < 500 ? new[] { Keys.Z } : frame >= 350 && frame < 390 ? new[] { Keys.X } : frame >= 130 && frame < 139 ? new[] { Keys.Z, Keys.X } : new Keys[0]);
                // Execute the original input stage, including mapping, zoom-key
                // consumption, native world mouse conversion and final sample.
                typeof(Main).GetMethod("DoUpdate_HandleInput", F).Invoke(this, null);
                Call(context, "UpdateRuntime");
                if (frame == 70) { Require((bool)Call(view, "SetUnlockEnabled", true), "Native view enabled after document load"); Require(GameZoomTarget == 1f, "ON does not jump target"); }
                if(frame==70 && waterRepair)
                {
                    PrepareCave();Draw(new GameTime());waterfallManager.FindWaterfalls(true);
                    NativeViewWaterChecks.Run(this,view,()=>Draw(new GameTime()),()=>capture,output,true);
                    NativeViewWaterFailureChecks.Run(view,()=>Draw(new GameTime()));
                    Console.WriteLine("PASS VIEW WATER REPAIR exact R4/R5 scope; unrelated accepted matrices reviewed separately.");Complete=true;Exit();return;
                }
                if(frame==70 && Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_PROBE")=="1")
                {
                    if(Environment.GetEnvironmentVariable("JUEMINGR_VIEW_CACHE_FOCUS")=="ZoomSpeed")
                    {PrepareCave();NativeViewZoomSpeedChecks.Run(this,view,k=>InputStep(k),()=>Draw(new GameTime()),()=>capture,output);CheckInputOwnership();Complete=true;Exit();return;}
                    NativeViewCacheRangeChecks.Run(this,view,()=>Draw(new GameTime()),PrepareCave,()=>capture,output,CheckRangeInput,ResizeCapture);Complete=true;Exit();return;
                }
                if(frame==70 && Environment.GetEnvironmentVariable("JUEMINGR_VIEW_REPAIR_FOCUS")=="Water")
                { NativeViewWaterChecks.Run(this,view,()=>Draw(new GameTime()),()=>capture,output);Complete=true;Exit();return; }
                if (frame == 139) Require(GameZoomTarget == 1f, "both native mapped keys have zero net movement");
                // Render comparison below is a controlled .5 scene, separate
                // from the new dedicated equal-ratio Mapping travel proof.
                if (frame == 200) GameZoomTarget=.5f;
                if (frame == 260) { Call(view, "SetUnlockEnabled", false); Console.WriteLine("VIEW OFF target=" + GameZoomTarget); }
                if (frame == 340) Require(Math.Abs(GameZoomTarget - .5f) < .0001f, "OFF keeps .5 with no input and blocks further outward hold");
                if (frame == 355) Require(GameZoomTarget > .5f && GameZoomTarget < 1f, "OFF adjusts inward gradually");
                if (frame == 390) Require(GameZoomTarget == 1f, "OFF returns continuously to exact normal target");
                if (frame == 470) { Call(view,"SetUnlockEnabled",true); Lighting.Mode = Terraria.Graphics.Light.LightMode.White; }
                if (frame == 560) Lighting.Mode = Terraria.Graphics.Light.LightMode.Retro;
                if (frame == 630) Lighting.Mode = Terraria.Graphics.Light.LightMode.Trippy;
                if (frame == 690) { Lighting.Mode = Terraria.Graphics.Light.LightMode.Color; PrepareCave(); }
                if (frame == 750) Terraria.GameContent.Events.ScreenObstruction.screenObstruction = .8f;
                if (frame == 765) Terraria.GameContent.Events.ScreenObstruction.screenObstruction = 0f;
                if (frame == 780) Main.shimmerAlpha = .75f;
                if (frame == 795) Main.shimmerAlpha = 0f;
                if (frame == 800) Terraria.GameContent.Events.ScreenDarkness.screenObstruction=.7f;
                if (frame == 825) Terraria.GameContent.Events.ScreenDarkness.screenObstruction=0f;
                if (frame == 830) typeof(Terraria.GameContent.Events.MoonlordDeathDrama).GetField("whitening",F).SetValue(null,.7f);
                if (frame == 855) typeof(Terraria.GameContent.Events.MoonlordDeathDrama).GetField("whitening",F).SetValue(null,0f);
                if (frame >= 860)
                {
                    CheckLifecycle();
                    NativeViewFilterChecks.Run(this,view,()=>Draw(new GameTime()),()=>capture,ResizeCapture,output);
                    NativeViewWaterChecks.Run(this,view,()=>Draw(new GameTime()),()=>capture,output);
                    CheckCosts();
                    NativeViewFailureChecks.Run(view,()=>Draw(new GameTime()));
                    foreach (var pair in drawTimes)
                    { var sorted=pair.Value.OrderBy(v=>v).ToArray();Console.WriteLine("VIEW COST group="+pair.Key+" n="+sorted.Length+" mean-ms="+sorted.Average()+" p95-ms="+sorted[(int)((sorted.Length-1)*.95)]+" max-ms="+sorted.Last()+" native-draw-appdomain-allocated-bytes="+drawAllocations[pair.Key]); }
                    Console.WriteLine("VIEW RESOURCES legacy-rebuilds="+legacyRebuilds+" target-inits="+targetInitializations);
                    Console.WriteLine("PASS VIEW original initialized .8 input/DoDraw ordinary->wide->OFF-Retained; physical=" + screenWidth + "x" + screenHeight + " target=" + GameZoomTarget + " render=" + GameViewMatrix.RenderZoom + " profile=" + GraphicsDevice.GraphicsProfile);
                    Complete = true; Exit();
                }
            }
            private void Prepare()
            {
                Main.autoSave = false; gameMenu = false; netMode = 0; myPlayer = 0;
                maxTilesX = 800; maxTilesY = 1200;
                tile = new Tile[maxTilesX, maxTilesY];
                for (int x = 0; x < maxTilesX; x++) for (int y = 0; y < maxTilesY; y++) tile[x,y] = new Tile();
                Map = new Terraria.Map.WorldMap(maxTilesX, maxTilesY); sectionManager = new WorldSections(4, 8);
                worldSurface = 260; rockLayer = 700; leftWorld = 0; rightWorld = maxTilesX * 16; topWorld = 0; bottomWorld = maxTilesY * 16;
                spawnTileX = 400; spawnTileY = 220; dayTime = true; Main.time = 24000;
                player[0] = new Player { active = true, whoAmI = 0, position = new Vector2(6400, 3520), name = "VIEW owned" };
                player[0].statLife = player[0].statLifeMax = 100; player[0].PlayerFrame();
                for (int x = 200; x < 600; x++) for (int y = 225; y < 250; y++) { tile[x,y].active(true); tile[x,y].type = TileID.Dirt; }
                // These distinctive blocks lie beyond the original viewport.
                for (int x = 300; x < 310; x++) for (int y = 195; y < 215; y++) { tile[x,y].active(true); tile[x,y].type = TileID.GoldBrick; }
                for (int x = 490; x < 500; x++) for (int y = 195; y < 215; y++) { tile[x,y].active(true); tile[x,y].type = TileID.RedBrick; }
                for (int x = 315; x < 330; x++) for (int y = 215; y < 225; y++) tile[x,y].liquid = 255;
                for (int x = 310; x < 340; x++) for (int y = 190; y < 215; y++) tile[x,y].wall = WallID.Glass;
                for (int x = 200; x < 600; x++) for (int y = 190; y < 250; y++) WorldGen.TileFrame(x,y);
                npc[0].SetDefaults(NPCID.GreenSlime); npc[0].active = true; npc[0].position = new Vector2(490 * 16,210 * 16);
                item[0].inner.SetDefaults(ItemID.FallenStar); item[0].position = new Vector2(315*16,205*16); item[0].width = item[0].height = 20;
                projectile[0].SetDefaults(ProjectileID.WoodenArrowFriendly); projectile[0].active=true; projectile[0].position=new Vector2(315*16,195*16);
                Compose();
                // Warm original Draw/JIT/assets before comparative sampling.
                // Fixed-step catch-up can skip early frame numbers entirely.
                for(int i=0;i<20;i++) Draw(new GameTime(TimeSpan.FromSeconds(i/60.0),TimeSpan.FromSeconds(1/60.0)));
            }
            private void Compose()
            {
                string candidate = Environment.GetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE");
                var assembly = Assembly.LoadFrom(String.IsNullOrEmpty(candidate) ? Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll") : candidate);
                var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker");
                context = Activator.CreateInstance(worker.GetNestedType("PostfixContext",F),F,null,new object[]{"continuous-processing-"+new string('6',40),Path.Combine(output,"evidence.log"),output},null);
                Call(context,"InitializeRuntime",true); input = Get(context,"Input"); view = Get(context,"View");
                Set(Get(context,"Shell"),"LayersReady",true);
                Set(input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1))); Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));
                var hooks = assembly.GetType("JueMingR.TerrariaHost.Input.HostInputHooks");
                hooks.GetMethod("Install",F).Invoke(null,new object[]{device,hooks.GetMethod("Resolve",F).Invoke(null,new object[]{typeof(Main).Assembly}),input});
                // The ordinary load-chain's InputPostfix owns this late stage.
                // This specialist composes a context without the bootstrap's
                // global committed owner, so bind that same business entry at
                // the original method's real postfix (never before mapping).
                device.Patch(typeof(Main).GetMethod("DoUpdate_HandleInput",F),postfix:new HarmonyMethod(typeof(ViewGame),nameof(FinalizeKeyboard)));
                var profile = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];
                profile.KeyStatus["ViewZoomOut"].Clear(); profile.KeyStatus["ViewZoomOut"].Add("Z"); profile.KeyStatus["ViewZoomIn"].Clear(); profile.KeyStatus["ViewZoomIn"].Add("X");
                GameZoomTarget = 1f; Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
                // Reach cannot read the physical backbuffer. Capture only its
                // physical-sized final destination, leaving native temporary
                // targets and every original Draw consumer unchanged.
                capture = new RenderTarget2D(GraphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                foreach(var loaded in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("JueMingR.")||a==typeof(NativeViewOriginalChecks).Assembly))
                {
                    if(String.IsNullOrEmpty(loaded.Location))continue;
                    using(var stream=File.OpenRead(loaded.Location))using(var hash=System.Security.Cryptography.SHA256.Create())
                        Console.WriteLine("VIEW ACTUAL ASSEMBLY name="+loaded.GetName().Name+" MVID="+loaded.ManifestModule.ModuleVersionId+" SHA256="+BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")+" path="+loaded.Location);
                }
                Console.WriteLine("VIEW prepared original initialized scene; Hook error="+GetOptional(view,"SetupError"));
                Require(GetOptional(view,"SetupError") == null,"complete .8 view hook installation");
            }
            private static void FinalizeKeyboard() { if(activeGame.input!=null)Call(activeGame.input,"AfterKeyboardRefresh"); }
            private void NetworkDraw()
            {
                Compose();
                var section = NativeViewNetworkChecks.ReceivedSection;
                Main.gameMenu = false;
                var ready=Stopwatch.StartNew();
                while(!(bool)Get(view,"ControlsEnabled") && ready.ElapsedMilliseconds<2500)
                {Call(context,"UpdateRuntime");System.Threading.Thread.Sleep(2);}
                // Camera pan views the actually received cold section. The
                // player's server position and activation rights stay native.
                Main.CurrentPan = new Vector2((section.X * 200 + 100) * 16, (section.Y * 150 + 75) * 16) - Main.LocalPlayer.Center;
                Require((bool)Call(view,"SetUnlockEnabled",true),"network host preference ready");
                var pump=typeof(NativeViewNetworkChecks).GetMethod("Pump",F);
                for(int i=0;i<35;i++)
                {
                    FocusHelper.IsSelectedApplication=true;Call(input,"BeginUpdate");keyboard=new KeyboardState(Keys.Z);
                    typeof(Main).GetMethod("DoUpdate_HandleInput",F).Invoke(this,null);Call(context,"UpdateRuntime");
                    Draw(new GameTime(TimeSpan.FromSeconds(i/60.0),TimeSpan.FromSeconds(1/60.0)));
                }
                SaveImage("network-pending");
                // The previous bounded request test deliberately delayed ACKs.
                // Continue the real native network dispatcher here so the
                // final image distinguishes received geometry from pending
                // section edges, rather than claiming an unpumped Draw is full.
                Console.WriteLine("NETWORK DRAW before receive visible sections="+VisibleSections());
                var receive=Stopwatch.StartNew();
                while(VisibleSections().Contains("pending") && receive.ElapsedMilliseconds<5000)
                {pump.Invoke(null,null);InputStep();Draw(new GameTime());System.Threading.Thread.Sleep(2);}
                Console.WriteLine("NETWORK DRAW after receive visible sections="+VisibleSections());
                Require(!VisibleSections().Contains("pending"),"every section intersecting final actual viewport arrived through native decoder");
                Require(Main.sectionManager.SectionLoaded(section.X,section.Y),"actual decoder retained cold section during native Draw");
                Require(GameViewMatrix.RenderZoom.X<.55f,"network native Draw uses actual expanded matrix");
                SaveImage("network");
                Console.WriteLine("PASS VIEW NETWORK DRAW actual server-decoded cold section="+section+" physical="+screenWidth+"x"+screenHeight+" render="+GameViewMatrix.RenderZoom+" camera="+Main.Camera.ScaledPosition+"; bounded initialized native TCP/Draw specialist, no full simulation/owner acceptance claim.");
            }
            private string VisibleSections()
            {
                var p=Camera.ScaledPosition;var size=Camera.ScaledSize;var states=new List<string>();
                for(int x=Math.Max(0,(int)Math.Floor(p.X/3200));x<=Math.Min((maxTilesX-1)/200,(int)Math.Floor((p.X+size.X)/3200));x++)
                for(int y=Math.Max(0,(int)Math.Floor(p.Y/2400));y<=Math.Min((maxTilesY-1)/150,(int)Math.Floor((p.Y+size.Y)/2400));y++)
                    states.Add(x+","+y+":"+(sectionManager.SectionLoaded(x,y)?"loaded":"pending"));
                return String.Join(";",states);
            }
            private void SaveImage(string name)
            {
                var pixels=new Color[screenWidth*screenHeight];capture.GetData(pixels);
                using(var image=new Texture2D(GraphicsDevice,screenWidth,screenHeight))
                {image.SetData(pixels);using(var file=File.Create(Path.Combine(output,"view-"+name+".png")))image.SaveAsPng(file,screenWidth,screenHeight);}
            }
            private static void BackgroundEndFailure()
            {
                if(!activeGame.throwBackgroundEnd) return;
                var hooks=activeGame.view.GetType().Assembly.GetType("JueMingR.TerrariaHost.View.ViewHooks");
                if(hooks.GetField("background",F).GetValue(null)==null)return;
                activeGame.throwBackgroundEnd=false;throw new IOException("Controlled cave tile flush failure");
            }
            private void CheckLifecycle()
            {
                CheckInputOwnership();
                int width=screenWidth,height=screenHeight;var position=screenPosition;
                var end=typeof(Terraria.Graphics.TileBatch).GetMethod("End");
                device.Patch(end,prefix:new HarmonyMethod(typeof(ViewGame),nameof(BackgroundEndFailure)));
                spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,Main.Rasterizer,null,GameViewMatrix.TransformationMatrix);
                bool caught=false;throwBackgroundEnd=true;
                try{typeof(Main).GetMethod("DrawBackground",F).Invoke(this,null);}
                catch(TargetInvocationException error){caught=error.InnerException is IOException;}
                finally{throwBackgroundEnd=false;device.Unpatch(end,HarmonyPatchType.Prefix,device.Id);spriteBatch.End();}
                Require(caught && screenWidth==width && screenHeight==height && screenPosition==position,"native cave exception propagates after exact coordinate/batch restoration");
                Draw(new GameTime());
                Main.BackgroundEnabled=false;
                try{Draw(new GameTime());SaveImage("old-background");}
                finally{Main.BackgroundEnabled=true;}
                foreach(var size in new[]{new Point(1280,720),new Point(width,height)})
                {
                    ResizeCapture(size.X,size.Y);
                    for(int i=0;i<4;i++)Draw(new GameTime());
                    Require(screenWidth==size.X && screenHeight==size.Y && Camera.ScaledSize.X/16+12<=512 && Camera.ScaledSize.Y/16+12<=512,"actual resized expanded viewport stays within shared discovery bounds");
                    Console.WriteLine("VIEW RESIZE physical="+screenWidth+"x"+screenHeight+" scaled="+Camera.ScaledSize+" target="+GameZoomTarget+" render="+GameViewMatrix.RenderZoom);
                }
                Main.gameMenu=true;Call(context,"UpdateRuntime");Require(!(bool)Get(view,"ControlsEnabled"),"menu ends view control session");
                Main.gameMenu=false;Call(context,"UpdateRuntime");Require((bool)Get(view,"ControlsEnabled"),"reentry restores loaded permission owner");
                Console.WriteLine("PASS VIEW lifecycle native cave exception/next draw, actual1280x720->1920x1080 reset, menu/reentry.");
            }
            private void ResizeCapture(int width,int height)
            { Main.SetResolution(width,height);capture.Dispose();capture=new RenderTarget2D(GraphicsDevice,screenWidth,screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents); }
            private void InputStep(params Keys[] keys)
            {
                bool chat=Main.drawingPlayerChat;float before=GameZoomTarget;
                Call(input,"BeginUpdate");keyboard=new KeyboardState(keys);
                typeof(Main).GetMethod("DoUpdate_HandleInput",F).Invoke(this,null);Call(context,"UpdateRuntime");
                if(frame>=820)Console.WriteLine("VIEW input witness keys="+String.Join(",",keys)+" before="+before+" after="+GameZoomTarget+" chat="+chat+"->"+Main.drawingPlayerChat+" map="+Main.mapFullscreen+" writing="+PlayerInput.WritingText);
            }
            private void CheckInputOwnership()
            {
                // .8 maps the previous finalized Main.keyState, then refreshes
                // it at the end of this phase. Hold/release through both real
                // opportunities rather than assuming a fresh physical sample
                // has already become this mapping's trigger.
                CheckGate(v=>Main.mapFullscreen=v,"full map");
                CheckGate(v=>Main.drawingPlayerChat=v,"chat");
                CheckGate(v=>Main.inFancyUI=v,"fancy UI");
                CheckGate(v=>Set(input,"HotkeyCapture",v),"key capture");
                InputStep();InputStep();float selected=GameZoomTarget;
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));InputStep(Keys.X);InputStep(Keys.X);Require(GameZoomTarget==selected,"lost focus revokes mapped view input");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));InputStep(Keys.X);InputStep(Keys.X);Require(GameZoomTarget==selected,"held key after focus return waits for release");
                InputStep();InputStep();InputStep(Keys.X);InputStep(Keys.X);Require(GameZoomTarget>selected,"fresh post-release native mapping re-arms zoom");
                float once=GameZoomTarget;Call(view,"ConsumeZoom");Require(GameZoomTarget==once,"one mapped opportunity consumes at most once");
                InputStep();InputStep(Keys.Z);InputStep();
                Console.WriteLine("PASS VIEW shared input map/chat/fancy/capture/focus/held-release and one native mapping opportunity.");
            }
            private void CheckRangeInput()
            {
                foreach(bool enabled in new[]{true,false})
                foreach(float target in new[]{.3f,10f})
                foreach(int direction in new[]{-1,0,1})
                {
                    InputStep();InputStep();Call(view,"SetUnlockEnabled",enabled);GameZoomTarget=target;
                    Keys[] keys=direction==0?new[]{Keys.X,Keys.Z}:new[]{direction>0?Keys.X:Keys.Z};
                    InputStep(keys);InputStep(keys);
                    float expected=target;
                    if(direction>0&&target<1)expected=.306f;
                    if(direction<0&&target>2)expected=10f/1.02f;
                    Require(Math.Abs(GameZoomTarget-expected)<.00001f,"actual remapped native range input owns one relative opportunity at both ON/OFF ends");
                    float once=GameZoomTarget;Call(view,"ConsumeZoom");Require(GameZoomTarget==once,"range mapping cannot be consumed twice");
                }
                InputStep();InputStep();Call(view,"SetUnlockEnabled",true);GameZoomTarget=1.5f;CheckInputOwnership();
                InputStep();InputStep();Main.GameZoomTarget=1f;
                Console.WriteLine("PASS VIEW RANGE actual remapped keys .3/10 ON/OFF, dual-key0, relative perMapping, duplicate consumption and existing UI/focus gates.");
            }
            private void CheckGate(Action<bool> gate,string name)
            {
                InputStep();InputStep();float selected=GameZoomTarget;gate(true);
                try{InputStep(Keys.X);InputStep(Keys.X);Require(GameZoomTarget==selected,name+" owns native view keys");InputStep();InputStep();}
                finally{gate(false);}
            }
            private static bool ReferenceMinimum(ref float __result) { __result=.3f;return false; }
            private void CheckCosts()
            {
                var update=(Action<ulong>)Delegate.CreateDelegate(typeof(Action<ulong>),view,view.GetType().GetMethod("Update",F));
                foreach(bool enabled in new[]{false,true})
                {
                    Call(view,"SetUnlockEnabled",enabled);GameZoomTarget=1f;Draw(new GameTime());
                    for(ulong i=0;i<1000;i++)update(i);
                    long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;var clock=Stopwatch.StartNew();
                    for(ulong i=0;i<100000;i++)update(i);
                    clock.Stop();allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated;
                    Console.WriteLine("VIEW UPDATE enabled="+enabled+" ordinary n=100000 total-ms="+clock.Elapsed.TotalMilliseconds+" appdomain-allocated-bytes="+allocated+"; direct feature Update, no reflective invocation allocation");
                }
                Call(view,"SetUnlockEnabled",true);
                var minimum=view.GetType().GetProperty("MinimumTarget",F).GetGetMethod(true);
                // .30 is an unshipped reference point requested by the task.
                // Only this isolated test overrides the floor; no product
                // setting, installed assembly or supported contract changes.
                device.Patch(minimum,prefix:new HarmonyMethod(typeof(ViewGame),nameof(ReferenceMinimum)));
                Vector2 origin=player[myPlayer].position;
                try
                {
                    foreach(float zoom in new[]{.75f,.5f,.3f})
                    foreach(bool moving in new[]{false,true})
                    {
                        GameZoomTarget=zoom;player[myPlayer].position=origin;
                        for(int i=0;i<12;i++)Draw(new GameTime());
                        var memory=Process.GetCurrentProcess();memory.Refresh();long beforePrivate=memory.PrivateMemorySize64,beforeManaged=GC.GetTotalMemory(false),allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                        int rebuilds=legacyRebuilds,targets=targetInitializations,scenes=(int)Get(Get(view,"Scene"),"Generation"),waters=(int)Get(Get(view,"Water"),"Generation");var samples=new List<double>();
                        for(int i=0;i<30;i++)
                        {
                            if(moving)player[myPlayer].position+=new Vector2(8,0);
                            var clock=Stopwatch.StartNew();Draw(new GameTime());clock.Stop();samples.Add(clock.Elapsed.TotalMilliseconds);
                        }
                        memory.Refresh();var sorted=samples.OrderBy(x=>x).ToArray();
                        Console.WriteLine("VIEW REFERENCE target="+zoom+" actual="+GameViewMatrix.RenderZoom.X+" moving="+moving+" n=30 mean-ms="+sorted.Average()+" p95-ms="+sorted[27]+" max-ms="+sorted.Last()+" visible="+Camera.ScaledSize+" appdomain-allocated-bytes="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated)+" managed-delta="+(GC.GetTotalMemory(false)-beforeManaged)+" private-delta="+(memory.PrivateMemorySize64-beforePrivate)+" private-total="+memory.PrivateMemorySize64+" rebuilds="+(legacyRebuilds-rebuilds)+" target-inits="+(targetInitializations-targets)+" scene-rebuilds="+((int)Get(Get(view,"Scene"),"Generation")-scenes)+" water-rebuilds="+((int)Get(Get(view,"Water"),"Generation")-waters)+" foreground-sun="+Main.ForegroundSunlightEffects);
                        Require(legacyRebuilds==rebuilds && targetInitializations==targets && (int)Get(Get(view,"Scene"),"Generation")==scenes && (int)Get(Get(view,"Water"),"Generation")==waters,"stationary/moving stable reference does not recreate lighting, native targets or owned scene/water buffers");
                        if(!moving)SaveImage("reference-"+(int)(zoom*100));
                    }
                }
                finally{device.Unpatch(minimum,HarmonyPatchType.Prefix,device.Id);player[myPlayer].position=origin;GameZoomTarget=.5f;}
                Draw(new GameTime());Require(GameViewMatrix.RenderZoom.X==.5f,"unshipped .30 reference returns to actual supported .50 camera");
            }
            private void PrepareCave()
            {
                player[0].position = new Vector2(6400,600*16);
                for(int x=250;x<550;x++)for(int y=520;y<680;y++)
                { tile[x,y].active(!(x>=280 && x<520 && y>=560 && y<630)); tile[x,y].type=TileID.Stone;tile[x,y].wall=WallID.Stone; }
                foreach(int x in new[]{315,485}){tile[x,590].active(true);tile[x,590].type=TileID.Torches;tile[x,590].frameX=0;tile[x,590].frameY=0;tile[x,591].active(true);tile[x,591].type=TileID.Stone;}
                for(int x=320;x<335;x++)for(int y=615;y<630;y++)tile[x,y].liquid=255;
                for(int x=290;x<510;x++)for(int y=550;y<640;y++)WorldGen.TileFrame(x,y);
                tile[320,590].active(true);tile[320,590].type=TileID.Stone;tile[320,590].halfBrick(true);
                tile[319,590].active(false);tile[319,590].liquid=255;tile[321,590].active(false);tile[321,590].liquid=0;
                Main.renderNow=true;
            }
            protected override void Draw(GameTime time)
            {
                if (capture != null) GraphicsDevice.SetRenderTarget(capture);
                redirectOutput = capture != null;
                long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var watch = Stopwatch.StartNew();
                try { base.Draw(time); }
                finally { watch.Stop(); redirectOutput = false; if (capture != null) GraphicsDevice.SetRenderTarget(null); }
                allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated;
                if (!prepared) return;
                if(frame>=700 && !caveWaterfalls){waterfallManager.FindWaterfalls(true);Require((int)Get(waterfallManager,"currentMax")>0,"native FindWaterfalls sees real peripheral half-brick/liquid source");caveWaterfalls=true;}
                string group=frame>=25 && frame<65?"normal-off":frame>=90&&frame<125?"normal-on":frame>=200&&frame<250?"wide-on":frame>=285&&frame<335?"wide-retained":frame>=410&&frame<455?"returned-off":frame>=525&&frame<555?"white-wide":frame>=590&&frame<625?"retro-wide":frame>=655&&frame<685?"trippy-wide":frame>=720&&frame<745?"cave-color":null;
                if(group!=null){if(!drawTimes.TryGetValue(group,out var samples)){drawTimes.Add(group,samples=new List<double>());drawAllocations.Add(group,0);stableRebuilds.Add(group,legacyRebuilds);stableTargets.Add(group,targetInitializations);stableScenes.Add(group,(int)Get(Get(view,"Scene"),"Generation"));stableWaters.Add(group,(int)Get(Get(view,"Water"),"Generation"));}else Require(stableRebuilds[group]==legacyRebuilds && stableTargets[group]==targetInitializations && stableScenes[group]==(int)Get(Get(view,"Scene"),"Generation") && stableWaters[group]==(int)Get(Get(view,"Water"),"Generation"),"stable view/mode does not rebuild lighting/targets per draw: "+group);samples.Add(watch.Elapsed.TotalMilliseconds);drawAllocations[group]+=allocated;}
                string imageName = !ordinaryImage && frame >= 25 && frame < 65 ? "ordinary" : !wideImage && frame >= 200 && frame < 250 ? "wide" : !retainedImage && frame >= 285 && frame < 335 ? "retained" : frame>=535&&frame<550?"white":frame>=605&&frame<620?"retro":frame>=665&&frame<680?"trippy":frame>=730&&frame<745?"cave":frame>=752&&frame<765?"obstruction":frame>=782&&frame<795?"shimmer":frame>=805&&frame<825?"darkness":frame>=835&&frame<855?"moon-white":null;
                if(imageName!=null && !images.Add(imageName)) imageName=null;
                if (imageName != null)
                {
                    var pixels = new Color[screenWidth * screenHeight];
                    capture.GetData(pixels);
                    using (var image = new Texture2D(GraphicsDevice,screenWidth,screenHeight))
                    { image.SetData(pixels); using (var file = File.Create(Path.Combine(output,"view-"+imageName+".png"))) image.SaveAsPng(file,screenWidth,screenHeight); }
                    Console.WriteLine("VIEW DRAW frame="+frame+" target="+GameZoomTarget+" render="+GameViewMatrix.RenderZoom+" visible="+Camera.ScaledSize+" direct="+drawToScreen+" ms="+watch.Elapsed.TotalMilliseconds);
                    if(!waterRepair && (imageName=="ordinary"||imageName=="wide"||imageName=="retained")) NativeViewNpcChecks.Run(this,view,imageName=="ordinary"?"normal":imageName=="wide"?"on":"retained",output);
                    if (imageName == "ordinary") ordinaryImage = true;
                    else { if(imageName == "wide") wideImage = true; else retainedImage = true; Require(GameViewMatrix.RenderZoom.X < .55f,"Actual original DoDraw publishes wide final matrix"); }
                }
            }
            protected override void Dispose(bool disposing)
            {
                if (context != null) { var current = GetOptional(context,"View"); if(current!=null)Call(current,"OnExit",null,EventArgs.Empty); NativeInformationChecks.StopContext(context); Require(Harmony.GetPatchInfo(typeof(Main).GetMethod("UpdateViewZoomKeys",F))?.Owners.Contains("JueMingR.View.Native1458")!=true,"view exit removes only its native hook owner");Console.WriteLine("PASS VIEW owner exit hook cleanup; preference worker stopped through existing lifecycle."); }
                device.UnpatchAll(device.Id); base.Dispose(disposing);
                capture?.Dispose(); capture = null;
            }
        }
    }
}
