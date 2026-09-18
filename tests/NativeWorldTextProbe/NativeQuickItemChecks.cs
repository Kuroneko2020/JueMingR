using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickItemChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int recalls;
        internal static int Recalls {get{return recalls;}}
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Run(Action<object> visual=null)
        {
            Require(IntPtr.Size==4,"G05 native fixture must use .NET Framework x86");
            Require(typeof(Main).Assembly.ManifestModule.ModuleVersionId==new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"),"fixed .8 MVID");
            using(var file=File.OpenRead(typeof(Main).Assembly.Location))using(var sha=System.Security.Cryptography.SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")=="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3","fixed .8 source");
            var assembly=Assembly.LoadFrom(Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            Initialize();
            string root=Path.Combine(Terraria.Program.SavePath,"composition");Directory.CreateDirectory(root);
            Main.ActivePlayerFileData=new Terraria.IO.PlayerFileData(Path.Combine(root,"fixture.plr"),false){Player=Main.LocalPlayer};
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(root,"fixture.wld"),false){UniqueId=Guid.NewGuid()};
            object context=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext",Flags),Flags,null,
                new object[]{"favorite-quick-items-"+new string('5',40),Path.Combine(root,"evidence.txt"),root},null);
            var isolation=new Harmony("JueMingR.Tests.QuickItemOutlets");
            var inputHooks=new Harmony("JueMingR.Tests.QuickInput");
            try
            {
                // Only final effects unrelated to selection/use are intercepted.
                // The native ItemCheck, timers and selectedItemState remain real.
                Patch(isolation,typeof(Player).GetMethod("Spawn",Flags,null,new[]{typeof(PlayerSpawnContext)},null),nameof(Recall));
                Patch(isolation,typeof(Item).GetMethod("GetDrawHitbox",Flags,null,new[]{typeof(int),typeof(Player)},null),nameof(DrawHitbox));
                Patch(isolation,typeof(NetMessage).GetMethod("SendPacket",Flags),nameof(NoNetwork));
                Call(context,"InitializeRuntime",true);
                Call(context,"InstallInformationSources");
                Require((bool)Get(Get(context,"InformationReadiness"),"Installed"),"full G05 profile installs prior information source hooks");
                foreach(string layer in new[]{"Browser","Footprints","Information","Guidance","WorldObjects","WorldTargets","Labels"})Require(GetOptional(context,layer)!=null,"full G05 composition retains "+layer);
                object quick=Get(context,"QuickItems"), use=Get(quick,"Use"), input=Get(context,"Input"), shell=Get(context,"Shell");
                var inputType=assembly.GetType("JueMingR.TerrariaHost.Input.HostInputHooks");
                var inputTargets=(MethodInfo[])inputType.GetMethod("Resolve",Flags).Invoke(null,new object[]{typeof(Main).Assembly});
                Require(inputTargets.Length==5,"five exact shared input seams, including native mapping provenance");
                inputType.GetMethod("Install",Flags).Invoke(null,new object[]{inputHooks,inputTargets,input});
                Require((bool)Get(quick,"Available"),"production quick hooks installed: "+GetOptional(quick,"SetupError"));
                var settings=(QuickItemSettings)Get(quick,"Settings");var keys=Get(shell,"hotkeys");var bindings=(HotkeyBindings)Get(keys,"Bindings");
                Until(()=>{Call(context,"UpdateRuntime");bindings.Poll();return settings.Loaded && bindings.Loaded;});
                Require(!settings.Enabled && !settings.KeepFavorited && settings.Current.Entries.Count==0,"full profile defaults off with empty entries");
                Set(input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));
                Set(shell,"LayersReady",true);Set(Get(shell,"State"),"Ready",true);
                Sample(input,new Keys[0]);Sample(input,new Keys[0]);
                var entry=new QuickItemEntry("0123456789abcdef0123456789abcdef",50,QuickItemMode.Use,true,true);string reason;
                Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"isolated entry commit admitted: "+reason);
                Until(()=>{Call(quick,"Poll");return !settings.Busy;});
                HotkeyChord chord;Require(HotkeyChord.TryParse("J",out chord,out reason),"test chord");long command;
                Require(bindings.TrySet(entry.ActionId,chord,null,out command,out reason),"shared binding save");Until(()=>{bindings.Poll();return !bindings.Busy;});
                var player=Main.LocalPlayer;player.inventory[2].SetDefaults(3507);player.inventory[17].SetDefaults(50);player.selectedItemState.Select(2);player.selectedItemState.Update();
                Item[] before=player.inventory.ToArray();recalls=0;
                Sample(input,new[]{Keys.J,Keys.W});Call(shell,"ProcessInput");
                Require((bool)Get(use,"Active"),"real shared shell dispatch admitted main-bag item: "+GetOptional(quick,"Message")+" input="+Get(input,"CanStartActions")+" session="+Get(Get(context,"Runtime"),"SharedRuntime"));
                for(int frame=0;frame<130;frame++)
                {
                    NativeFrame(player);
                    if(frame==0)Require(player.selectedItem==17 && player.itemAnimation>0,"native override and first actual ItemCheck started mirror");
                    Sample(input,new[]{Keys.W});Call(shell,"ProcessInput");
                }
                Require(recalls==1,"native delayed recall branch exactly once, actual="+recalls);
                Require(player.selectedItem==2 && !(bool)Get(use,"Active") && !player.controlUseItem && !Main.mouseLeft,"held movement cannot strand selection or synthetic input");
                Require(before.Select((item,i)=>ReferenceEquals(item,player.inventory[i])).All(v=>v) && Main.mouseItem.IsAir && player.inventory[17].type==50,"no inventory movement, mouse replacement or type rollback");
                Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");player.selectedItemState.Update();
                Require(player.selectedItem==17 && (bool)Get(use,"Active"),"interrupted fixture obtained native override");
                Sample(input,new[]{Keys.W});NativeFrame(player);
                Require(player.selectedItem==2 && !(bool)Get(use,"Active"),"skip first ItemCheck then native return must retire the old use lease");
                Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");Main.drawingPlayerChat=true;
                try { NativeFrame(player);Require(player.selectedItem==2 && !(bool)Get(use,"Active"),"chat opening after dispatch cancels before transform/pulse"); }
                finally {Main.drawingPlayerChat=false;}
                NativeQuickUseMatrix.Run(context,entry);
                NativeQuickGestureChecks.Run(context,entry);
                NativeQuickManualInputChecks.Run(context,entry);
                NativeQuickNetworkChecks.Run(context,entry);
                NativeQuickLifecycleChecks.Run(context,entry);
                NativeQuickPersistenceChecks.Run(context,entry);
                Require((bool)Call(quick,"Delete",entry.Id),"production delete accepted");
                Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy && bindings.Get(entry.ActionId)==null;});
                var saved=HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(root,"JueMingRData","config","hotkeys.json")));
                Require(!saved.Entries.Any(row=>row.Key==entry.ActionId),"production domain deletion actually removes binding row");
                Console.WriteLine("PASS: production shared dispatch, real native temporary selection and delayed mirror ItemCheck branch, movement-held return; teleport outlet intercepted.");
                NativeFavoriteChecks.Run(context);
                NativeQuickUiChecks.Run(context);
                visual?.Invoke(context);
            }
            finally
            {
                Main.gameMenu=true;Call(context,"UpdateRuntime");
                var quick=GetOptional(context,"QuickItems");if(quick!=null)Call(quick,"Exit",null,EventArgs.Empty);
                var browser=GetOptional(context,"Browser");if(browser!=null)((IDisposable)browser).Dispose();StopContext(context);
                foreach(var method in isolation.GetPatchedMethods().ToArray())isolation.Unpatch(method,HarmonyPatchType.All,isolation.Id);
                foreach(var method in inputHooks.GetPatchedMethods().ToArray())inputHooks.Unpatch(method,HarmonyPatchType.All,inputHooks.Id);
                assembly.GetType("JueMingR.TerrariaHost.QuickItems.QuickItemHooks").GetMethod("Uninstall",Flags).Invoke(null,null);
                assembly.GetType("JueMingR.TerrariaHost.KeepFavorited.FavoriteHooks").GetMethod("Uninstall",Flags).Invoke(null,null);
            }
        }
        private static void Initialize()
        {
            LanguageManager.Instance.SetLanguage("en-US");Lang.InitializeLegacyLocalization();Main.rand=new UnifiedRandom(123);
            Main.gameMenu=Main.dedServ=Main.hideUI=Main.mapFullscreen=Main.inFancyUI=Main.onlyDrawFancyUI=Main.ingameOptionsWindow=false;
            Main.netMode=Main.myPlayer=0;Main.screenWidth=960;Main.screenHeight=640;
            Main.maxTilesX=Main.maxTilesY=120;Main.tile=new Tile[120,120];for(int x=0;x<120;x++)for(int y=0;y<120;y++)Main.tile[x,y]=new Tile();
            Main.Map=new Terraria.Map.WorldMap(120,120);Main.player[0]=new Player{active=true,whoAmI=0,position=new Vector2(640,640),gravDir=1};Main.clientPlayer=new Player();
            Main.LocalPlayer.chest=-1;Main.LocalPlayer.sign=-1;Player.tileTargetX=Player.tileTargetY=40;
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile();for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC();
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem();
            var profile=new PlayerInputProfile("G05 isolated defaults");profile.Initialize(PresetProfiles.Redigit);typeof(PlayerInput).GetField("_currentProfile",Flags).SetValue(null,profile);PlayerInput.Triggers.Initialize();
            ContentSamples.Initialize();ItemID.Sets.PostSetupContent();
            Main.dedServ=true;try{RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle);}finally{Main.dedServ=false;}
            typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.Identity);FiniteCostChecks.SetCpuFont(8);
            Terraria.GameContent.FontAssets.CombatText[0]=Terraria.GameContent.FontAssets.MouseText;
            Terraria.GameContent.FontAssets.CombatText[1]=Terraria.GameContent.FontAssets.MouseText;
            Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0,0,960,640));
            typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,960);typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,640);typeof(PlayerInput).GetField("RawMouseScale",Flags).SetValue(null,Vector2.One);
            Main.mouseItem=new Item();FocusHelper.IsSelectedApplication=true;
        }
        internal static void Sample(object input,Keys[] keys)
        {
            Main.keyState=new KeyboardState(keys);PlayerInput.MouseInfo=new MouseState(200,200,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.Up=keys.Contains(Keys.W);PlayerInput.Triggers.Update();Main.mouseLeft=false;
            Call(input,"BeginUpdate");Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
        }
        internal static void NativeFrame(Player player)
        {
            typeof(Player).GetMethod("ResetControls",Flags).Invoke(player,null);PlayerInput.Triggers.Current.CopyInto(player);
            player.selectedItemState.Update();typeof(Player).GetMethod("TrySyncingInput",Flags).Invoke(player,null);player.ItemCheck();
        }
        private static void Until(Func<bool> done) {var until=DateTime.UtcNow.AddSeconds(8);while(!done()){if(DateTime.UtcNow>until)throw new Exception("G05 worker timeout");Thread.Sleep(2);}}
        private static void Patch(Harmony harmony,MethodInfo method,string prefix) {Require(method!=null,"native fixture exact outlet exists: "+prefix);harmony.Patch(method,new HarmonyMethod(typeof(NativeQuickItemChecks).GetMethod(prefix,Flags)));}
        private static bool Recall(PlayerSpawnContext __0) {Require(__0==PlayerSpawnContext.RecallFromItem,"only recall outlet intercepted");recalls++;return false;}
        private static bool DrawHitbox(ref Rectangle __result) {__result=new Rectangle(0,0,24,24);return false;}
        private static bool NoNetwork() {throw new InvalidOperationException("Isolated fixture attempted real network output.");}
    }
}
