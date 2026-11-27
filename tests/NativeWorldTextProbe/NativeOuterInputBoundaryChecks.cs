using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Gamepad;

namespace NativeWorldTextProbe
{
internal static partial class NativeOuterInputBoundaryChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static readonly MethodInfo ResetControls = typeof(Player).GetMethod("ResetControls", F);
    static string root,hostPath; static object input,context,shell;
    static KeyboardState keyboard; static MouseState mouse; static bool foreground=true,nativeSelected=true,skip,throwBody;
    static readonly Keys[] StateKeys = new[] { Keys.Kanji, Keys.None }.Concat(Enumerable.Range(240, 14).Select(key => (Keys)key)).ToArray();
    static Main main; static UserInterface ui; static int clicks,frames,keyboardSamples,px=150,py=150;
    static bool expectTextLease,observedTextWriting,foreignWritingAtOuter;static object foreignFixture;
    static object Get(object o,string n){var t=o.GetType();var p=t.GetProperty(n,F);return p!=null?p.GetValue(o,null):t.GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){var t=o.GetType();var p=t.GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else t.GetField(n,F).SetValue(o,v);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static int Check(string output)
    {
        root=Program.Repository;
        hostPath=Environment.GetEnvironmentVariable("JUEMINGR_INPUT_BOUNDARY_CANDIDATE");
        if(String.IsNullOrEmpty(hostPath))hostPath=Path.Combine(root,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
        hostPath=Path.GetFullPath(hostPath);
        // This Check starts with the shared full-profile defaults. Its later
        // real UseGesture command persists an enabled entry, so a repeated
        // execution needs a fresh root; all phases within this Run share it.
        Terraria.Program.SavePath=Path.Combine(output,"u-"+Guid.NewGuid().ToString("N").Substring(0,12));
        Require(!Directory.Exists(Terraria.Program.SavePath),"Fresh input fixture must not reuse prior user preferences.");
        Directory.CreateDirectory(Terraria.Program.SavePath);
        Run();return 0;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        // Install device substitutes before the composition patches compile
        // native input callers; XNA's tiny getter can otherwise be inlined.
        var devices=new Harmony("JueMingR.Tests.I01.OuterDevices");
        try
        {
            devices.Patch(typeof(Keyboard).GetMethod("GetState",Type.EmptyTypes),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("KeyboardPrefix",F)));
            devices.Patch(typeof(Mouse).GetMethod("GetState",Type.EmptyTypes),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("MousePrefix",F)));
            devices.Patch(typeof(PlayerInput).GetMethod("GamePadInput",F),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("NoGamepad",F)));
            NativeQuickItemChecks.Run(Exercise,processing:true,shortFeedback:true,candidateAssembly:hostPath);
        }
        finally{devices.UnpatchAll(devices.Id);}
    }
    static void Exercise(object c)
    {
        context=c;input=Get(c,"Input");shell=Get(c,"Shell");
        Set(input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Set(input,"foregroundWindow",(Func<IntPtr>)(()=>foreground?new IntPtr(1):new IntPtr(2)));
        main=(Main)FormatterServices.GetUninitializedObject(typeof(Main));
        Terraria.Initializers.UILinksInitializer.Load();Terraria.Main.ChromaPainter=new Terraria.GameContent.ChromaHotkeyPainter();
        var host=input.GetType().Assembly;var worker=host.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker");
        Require(host.GetType("JueMingR.TerrariaHost.Input.InputDiagnosticTrace",false)==null && input.GetType().GetMethod("ObserveDiagnostics",F)==null,"ordinary Host omits diagnostic recorder and observer");
        Require(input.GetType().GetProperty("DiagnosticGameplayActive",F)==null && input.GetType().GetField("DiagnosticGameplayActive",F)==null && c.GetType().GetProperty("DiagnosticGameplayActive",F)==null && c.GetType().GetField("DiagnosticGameplayActive",F)==null,"ordinary Host omits both gameplay waiting properties/fields");
        string hostHash;using(var stream=File.OpenRead(host.Location))using(var sha=System.Security.Cryptography.SHA256.Create())hostHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        Console.WriteLine("PASS ordinary input diagnostic OFF Host="+host.Location+" MVID="+host.ManifestModule.ModuleVersionId+" SHA256="+hostHash);

        worker.GetField("postfixContext",F).SetValue(null,c);worker.GetField("hookCommitted",F).SetValue(null,1);
        var update=typeof(Main).GetMethod("Update",F,null,new[]{typeof(GameTime)},null);
        var originalInput=typeof(Main).GetMethod("DoUpdate_HandleInput",F);
        var patch=new Harmony("JueMingR.Tests.I01.Outer");
        try
        {
            patch.Patch(update,new HarmonyMethod(worker.GetMethod("UpdatePrefix",F)));
            var body=new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("IsolatedUpdate",F));body.priority=Priority.Last;patch.Patch(update,body);
            patch.Patch(originalInput,new HarmonyMethod(worker.GetMethod("InputPrefix",F)),new HarmonyMethod(worker.GetMethod("InputPostfix",F)));
            var observePrefix=new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("ObserveBeforeNativeInput",F));observePrefix.priority=Priority.Last;patch.Patch(originalInput,observePrefix);
            var infos=Harmony.GetPatchInfo(originalInput);
            Require(Array.Exists(infos.Prefixes.ToArray(),x=>x.PatchMethod==worker.GetMethod("InputPrefix",F)) && Array.Exists(infos.Postfixes.ToArray(),x=>x.PatchMethod==worker.GetMethod("InputPostfix",F)),"actual production input patches installed");
            ui=new UserInterface();var state=new UIState();var button=new UIElement();button.Left.Set(100,0);button.Top.Set(100,0);button.Width.Set(200,0);button.Height.Set(100,0);button.OnLeftClick+=(e,v)=>clicks++;state.Append(button);ui.SetState(state);
            Console.WriteLine("SOURCE original="+typeof(Main).Assembly.ManifestModule.ModuleVersionId+" Host="+host.ManifestModule.ModuleVersionId+"; production outer/input patches; Main.Update heavy body replaced");
            Terraria.Main.gameMenu=true;Call(context,"UpdateRuntime");Frame(false);Frame(false);
            Require(!(bool)Get(Get(Get(c,"runtime"),"SharedRuntime"),"IsSessionActive"),"menu has no Session");
            int before=clicks;Frame(true);Frame(false);Require(clicks==before+1,"menu real native UI click without Session");
            // Native page geometry is fixture material; dispatch/travel is original.
            // KeyboardInput maps Main.keyState from the previous frame; native
            // DoUpdate_HandleInput refreshes it only after navigator dispatch.
            var page=UILinkPointNavigator.Pages[1000];page.LinkMap[2000].Down=2001;page.LinkMap[2001].Up=2000;UILinkPointNavigator.ChangePoint(2000);Frame(false,Keys.Down);Frame(false,Keys.Down);
            Console.WriteLine("NAV point="+UILinkPointNavigator.CurrentPoint+" page="+UILinkPointNavigator.CurrentPage+" inUse="+UILinkPointNavigator.InUse+" mappedDown="+PlayerInput.Triggers.Current.Down+" menuDown="+PlayerInput.Triggers.Current.MenuDown+" permit="+Get(input,"CanUseInput")+" keyboard="+Terraria.Main.keyState.IsKeyDown(Keys.Down)+" deviceKeyboardSamples="+keyboardSamples);
            Require(UILinkPointNavigator.CurrentPoint==2001,"menu keyboard reaches original navigation travel");Frame(false);
            foreground=false;Frame(true,Keys.Down);Require(!Terraria.Main.mouseLeft && !PlayerInput.Triggers.Current.Down,"background menu actions filtered");
            foreground=true;UILinkPointNavigator.ChangePoint(2000);Frame(true,Keys.Down);Frame(false);Require(clicks==before+1 && UILinkPointNavigator.CurrentPoint==2000,"reactivation held/neutral actions do not travel or click");
            Frame(false,Keys.Down);Frame(false,Keys.Down);Require(UILinkPointNavigator.CurrentPoint==2001,"menu new navigation after release");Frame(false);Frame(false);
            Console.WriteLine("PASS outer menu Session absent / native UI click / original UILinkPointNavigator travel / background and activation tails");
            MenuStateKeyRecovery();
            Terraria.Main.gameMenu=false;Call(context,"UpdateRuntime");Frame(false);Frame(false);before=clicks;Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"world original Triggers.CopyInto fresh key");Frame(false);
            foreground=false;Frame(true,Keys.W);Require(!Terraria.Main.LocalPlayer.controlUp,"world loss blocks consumer");foreground=true;Frame(true,Keys.W);Frame(false);Require(clicks==before && !Terraria.Main.LocalPlayer.controlUp,"world activation consumed");Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"world release allows fresh key");Frame(false);
            skip=true;Frame(false,Keys.W);Require(!(bool)Get(input,"CanUseInput") && (bool)Get(input,"CanRetainIntent"),"outer skip revokes epoch permission without retiring intent");skip=false;Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"next sampled frame recovers after skip");Frame(false);
            throwBody=true;try{Frame(false,Keys.W);throw new Exception("expected isolated exception");}catch(TargetInvocationException){Require(!(bool)Get(input,"CanUseInput"),"exception before mapping leaves no epoch permission");}finally{throwBody=false;}
            Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp && !(bool)Get(shell,"Failed"),"fresh input after outer exception; shell stays valid");
            Console.WriteLine("PASS outer world focus/reactivation / skipped and exceptional input epoch; real Shell consumers remain available");
            WorldStateKeyRecovery();
            StateKeysDoNotSuppressOrRecord();
            OrdinaryPopupInput(worker);
            InventoryMenuClose();
            RejectedCompanionOpen();
            CaptureAndText(patch,host);
            UseGestureClaim();
        }
        finally{patch.UnpatchAll(patch.Id);worker.GetField("hookCommitted",F).SetValue(null,0);worker.GetField("postfixContext",F).SetValue(null,null);}
    }
    static void Frame(bool down,params Keys[] keys) => MouseFrame(down, false, keys);

    static void MouseFrame(bool down, bool side, params Keys[] keys)
        => ButtonFrame((down ? 1 : 0) | (side ? 8 : 0), keys);

    static void ButtonFrame(int buttons, params Keys[] keys)
    {
        keyboard = RawKeyboardSample(keys);
        mouse = new MouseState(px + frames++ % 2, py, 0, Button(buttons, 1), Button(buttons, 4),
            Button(buttons, 2), Button(buttons, 8), Button(buttons, 16));
        FocusHelper.IsSelectedApplication = foreground && nativeSelected;
        Call(main,"Update",new GameTime(TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(250)));
        if (!skip)
        {
            ResetControls.Invoke(Main.LocalPlayer, null);
            // Match the native Player.Update text gate before consuming the
            // original mappings. Bypassing it would conceal a leaked blockInput.
            if (!Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !Main.blockInput)
                PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            ui.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250)));
        }
    }

    static ButtonState Button(int buttons, int mask) => (buttons & mask) != 0 ? ButtonState.Pressed : ButtonState.Released;

    static KeyboardState RawKeyboardSample(Keys[] keys)
    {
        var sample = new KeyboardState(keys);
        uint missingStateBits = 0;
        foreach (Keys key in keys)
            if ((int)key >= 240 && (int)key <= 253 && sample.IsKeyUp(key))
                missingStateBits |= 1u << ((int)key % 32);
        if (missingStateBits == 0) return sample;

        // XNA's public test constructor drops undefined enum values within
        // this range. Inject those four bits in the emulated device result,
        // before the real native refresh, to exercise the full numeric range.
        object boxed = sample;
        var highWord = typeof(KeyboardState).GetField("currentState7", F);
        highWord.SetValue(boxed, (uint)highWord.GetValue(boxed) | missingStateBits);
        return (KeyboardState)boxed;
    }

    static void MenuStateKeyRecovery()
    {
        foreach (var stateKey in StateKeys)
        {
            int before = clicks;
            foreground = false; Frame(true, stateKey);
            foreground = true; Frame(true, stateKey);
            Require(!Main.mouseLeft && clicks == before, "menu activation click remains consumed");
            int readsBeforeNeutral = keyboardSamples;
            Frame(false, stateKey);
            Require(keyboardSamples == readsBeforeNeutral + 1, "neutral recovery uses the original final keyboard read without polling again");
            Require(!Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft && !(bool)Get(input, "CanUseInput"),
                "menu neutral frame itself remains consumed");
            Require(((KeyboardState)Get(input, "KeyboardSample")).IsKeyDown(stateKey),
                "menu retains raw keyboard state " + (int)stateKey + " before consumption");
            Frame(true, stateKey); Frame(false, stateKey);
            Require(clicks == before + 1, "menu original UI must receive an independent click with persistent state key " + (int)stateKey);
        }
        Frame(false);
        Console.WriteLine("PASS no-Session menu refocus with persistent state keys 0,25,240..253; activation consumed, next original UI click delivered");
    }

    static void WorldStateKeyRecovery()
    {
        Keys stateKey = Keys.Kanji;
        int before = clicks;
        foreground = false; Frame(true, stateKey, Keys.W);
        foreground = true; nativeSelected = false; Frame(false);
        Require(!(bool)Get(input, "SampleFocused"), "foreground HWND without native selection cannot prove neutrality");
        nativeSelected = true;
        skip = true; Frame(false); skip = false;
        Frame(true, stateKey, Keys.W);
        Require(!Main.LocalPlayer.controlUp && !Main.LocalPlayer.controlUseItem,
            "native denial and skipped Update must not clear rearm before focused held input");
        foreach (var key in new[] { Keys.W, Keys.LeftAlt, Keys.RightShift, Keys.LeftWindows, Keys.OemClear })
        {
            Frame(false, stateKey, key); Frame(false, stateKey, key);
            Require(!(bool)Get(input, "CanUseInput") && !Main.LocalPlayer.controlUp, "operation key must release: " + key);
        }
        for (int button = 0; button < 5; button++)
        {
            ButtonFrame(1 << button, stateKey);
            Require(!(bool)Get(input, "CanUseInput") && !Main.LocalPlayer.controlUseItem,
                "Mouse" + (button + 1) + " must release before recovery");
        }
        Frame(false, stateKey);
        Require(!(bool)Get(input, "CanUseInput") && clicks == before, "world neutral sample is still consumed");
        Frame(true, stateKey, Keys.W);
        Require(Main.LocalPlayer.controlUseItem, "fresh click reaches native world action after neutral");
        Frame(false, stateKey, Keys.W);
        Require(Main.LocalPlayer.controlUp && clicks == before + 1, "new W and click reach original world consumers with state key still present");
        var state = Get(shell, "State");
        Frame(false, stateKey, Keys.F5);
        Require((bool)Get(state, "Visible"), "fresh F5 works after recovery with state key still present");
        Frame(false, stateKey); Frame(false, stateKey, Keys.F5); Frame(false, stateKey);
        Require(!(bool)Get(state, "Visible"), "fresh F5 can close again");
        Frame(false);
        Console.WriteLine("PASS world refocus with state-key noise: native selection and skipped-sample guards, modifiers/OemClear/five mouse buttons, fresh movement/attack/F5");
    }

    static void StateKeysDoNotSuppressOrRecord()
    {
        var hotkeys = Get(input, "Hotkeys");
        Frame(false, StateKeys.Concat(new[] { Keys.Q }).ToArray());
        object[] primary = { 0 };
        Require((int)Call(hotkeys, "NewPrimary", primary) == 1 && (int)primary[0] == (int)Keys.Q,
            "status keys cannot become extra recording primaries");
        Call(hotkeys, "SuppressHeld");
        Require((bool)Call(hotkeys, "IsSuppressed", (int)Keys.Q), "true key is suppressed");
        foreach (var key in StateKeys)
            Require(!(bool)Call(hotkeys, "IsSuppressed", (int)key), "state key cannot enter suppression: " + (int)key);
        Frame(false, StateKeys);
        Require(!(bool)Get(hotkeys, "HasSuppressedKeys"), "state keys cannot outlive released suppressed key");
        Frame(false, StateKeys.Concat(new[] { Keys.W }).ToArray());
        Frame(false, StateKeys.Concat(new[] { Keys.W }).ToArray());
        Require(Main.LocalPlayer.controlUp, "direct suppression release restores mapped movement");
        Frame(false);
        Console.WriteLine("PASS state keys 0,25,240..253 excluded from public recording and held suppression; native movement resumes");
    }
    static bool IsolatedUpdate(){if(throwBody)throw new InvalidOperationException("isolated before-input exception");if(foreignFixture!=null && !foreground)foreignWritingAtOuter=Object.ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride,foreignFixture) && !Terraria.Main.blockInput && PlayerInput.WritingText;if(!skip)Call(main,"DoUpdate_HandleInput");return false;}
    static bool KeyboardPrefix(ref KeyboardState __result){keyboardSamples++;__result=keyboard;return false;}
    static bool MousePrefix(ref MouseState __result){__result=mouse;return false;}
    static bool NoGamepad(ref bool __result){__result=false;return false;}
    static void InventoryMenuClose()
    {
        var state = Get(shell, "State");
        var settings = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus;
        var original = settings["Inventory"];
        try
        {
            settings["Inventory"] = new List<string> {nameof(Keys.Escape)};
            Frame(false); Frame(false, Keys.F5); Frame(false);
            Require((bool)Get(state,"Visible"), "F5 opens before the default Inventory action");
            Frame(false, Keys.Escape, Keys.W);
            Require(!(bool)Get(state,"Visible"), "the bound Escape press closes the actual shell");
            Frame(false, Keys.Escape, Keys.W); Frame(false, Keys.Escape, Keys.W);
            Require(!PlayerInput.Triggers.Current.Inventory && !Main.LocalPlayer.controlInv && Main.LocalPlayer.controlUp,
                "the delayed mapping and held tail leave vanilla Inventory inactive, with movement still mapped");
            Frame(false); Frame(false, Keys.Escape); Frame(false, Keys.Escape);
            Require(PlayerInput.Triggers.JustPressed.Inventory && Main.LocalPlayer.controlInv,
                "a fresh press after trusted release reaches native CopyInto when the shell is closed");
            Frame(false); Frame(false, Keys.F5); Frame(false);

            settings["Inventory"] = new List<string> {nameof(Keys.U)};
            Frame(false, Keys.Escape); Frame(false);
            Require((bool)Get(state,"Visible"), "Escape no longer closes the shell after rebinding");
            Frame(false, Keys.U); Frame(false, Keys.U);
            Require(!(bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv,
                "the rebound Inventory key replaces Escape and consumes its native mapping");
            Frame(false); Frame(false);

            settings["Inventory"] = new List<string> {nameof(Keys.F5)};
            Frame(false, Keys.F5); Frame(false, Keys.F5);
            Require((bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv,
                "one Inventory/F5 press opens only F5 and holding it does not close F5 or open Inventory");
            Frame(false); Frame(false, Keys.F5); Frame(false, Keys.F5);
            Require(!(bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv,
                "the next shared key press closes the menu once without opening Inventory");
            Frame(false); Frame(false);

            InventoryMouseClose(state, settings);
            Console.WriteLine("PASS F5 Inventory close / live key and Mouse4 bindings / shared F5 key / native CopyInto / held movement and trusted fresh press");
        }
        finally { settings["Inventory"] = original; }
    }

    static void InventoryMouseClose(object state, Dictionary<string, List<string>> settings)
    {
        settings["Inventory"] = new List<string> {"Mouse4"};
        Frame(false, Keys.F5); Frame(false);
        MouseFrame(false, true, Keys.W);
        Require(!(bool)Get(state,"Visible") && !PlayerInput.Triggers.Current.Inventory && !Main.LocalPlayer.controlInv,
            "a rebound Mouse4 closes now and consumes its same-frame native Inventory request");
        MouseFrame(false, true, Keys.W);
        Require(!Main.LocalPlayer.controlInv && Main.LocalPlayer.controlUp,
            "the owned side-button tail keeps Inventory inactive without suppressing movement");
        MouseFrame(false, true, Keys.F5); MouseFrame(false, true);
        Require((bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv,
            "a held side button cannot close a menu reopened with F5");
        Frame(false); Frame(false, Keys.F5); Frame(false);
        MouseFrame(false, true);
        Require(PlayerInput.Triggers.JustPressed.Inventory && Main.LocalPlayer.controlInv,
            "a genuinely released Mouse4 reaches native Inventory on its next closed-menu press");
        Frame(false); Frame(false);

        settings["Inventory"] = new List<string> {nameof(Keys.U), "Mouse4"};
        Frame(false, Keys.F5); Frame(false); Frame(false, Keys.U);
        MouseFrame(false, true);
        Require(!(bool)Get(state,"Visible") && Main.LocalPlayer.controlInv,
            "a distinct new mouse press survives consumption of the previous keyboard mapping");
        Frame(false); Frame(false);

        settings["Inventory"] = new List<string> {"Mouse1"};
        Main.LocalPlayer.SetTalkNPC(-1);
        Frame(false, Keys.F5); Frame(false);
        var screen = PlayerInput.OriginalScreenSize;
        Call(Get(shell, "renderer"), "Prepare", state, screen.X, screen.Y, Main.UIScaleMatrix.M11);
        int originalX = px, originalY = py;
        try
        {
            var title = Get(Get(state, "Layout"), "Title");
            px = (int)((float)Get(state, "X") + (float)Get(title, "X") + 8);
            py = (int)((float)Get(state, "Y") + (float)Get(title, "Y") + 8);
            Frame(true);
            Require((bool)Get(state,"Visible") && (int)Get(state,"capture") == 1 && !Main.LocalPlayer.controlInv,
                "Mouse1 over F5 starts its existing title drag without closing the menu or opening native Inventory");
            Frame(true);
            Require(!Main.LocalPlayer.controlInv && (int)Get(state,"capture") == 1,
                "the title owner keeps the held Mouse1 from replaying its direct Inventory mapping");
            Frame(false);
        }
        finally { px = originalX; py = originalY; }
        Frame(false); Frame(true);
        Require(!(bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv && !Main.LocalPlayer.controlUseItem,
            "the unowned Mouse1 closes F5 and consumes the same native Inventory and attack press. " +
            "F5=" + Get(state,"Visible") + " Inventory=" + Main.LocalPlayer.controlInv + " Use=" + Main.LocalPlayer.controlUseItem);
        Frame(false); Frame(false);
    }

    static void RejectedCompanionOpen()
    {
        var state = Get(shell, "State");
        var window = Get(shell, "CompanionUi");
        Frame(false, Keys.F5); Frame(false);
        Call(state, "Navigate", 1);
        Require((bool)Get(state, "Visible") && window != null, "full F5 composition hosts the Misc companion entry");
        int height = Main.screenHeight;
        try
        {
            Main.screenHeight = 270;
            PlayerInput.CacheOriginalScreenDimensions();
            Call(shell, "OpenCompanions", Get(Get(state, "Layout"), "Viewport"));
            Require((bool)Get(state, "Visible") && !(bool)Get(window, "Visible"),
                "the Misc entry retains its parent when the companion window cannot prepare");
        }
        finally
        {
            Main.screenHeight = height;
            PlayerInput.CacheOriginalScreenDimensions();
            Call(state, "Close");
        }
        Frame(false);
        Console.WriteLine("PASS rejected companion window preparation leaves the F5 parent visible");
    }

    static void CaptureAndText(Harmony patch,Assembly host)
    {
        Frame(false);Frame(false);Frame(false,Keys.F5);Frame(false);Frame(false);
        var state=Get(shell,"State");Require((bool)Get(state,"Visible"),"real F5 consumer opens shell");Call(state,"Navigate",0);
        var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");
        var rectType=host.GetType("JueMingR.TerrariaHost.F5.F5Rect");var rect=Activator.CreateInstance(rectType,F,null,new object[]{100f,100f,40f,20f},null);
        Call(shell,"OpenHotkey","item-browser.query",rect);Call(shell,"OpenHotkey","item-browser.query",rect);
        var popup=Get(shell,"HotkeyPopup");Require((bool)Get(popup,"Visible"),"legitimate configurable popup opens");
        var prepare=popup.GetType().GetMethod("Prepare",F);var measure=Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType,renderer,renderer.GetType().GetMethod("PopupMeasure",F));
        prepare.Invoke(popup,new object[]{960f,640f,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration")});
        var layout=Get(popup,"Layout");var commands=(IList)Get(layout,"Commands");
        var hotkeys = (JueMingR.Platform.Hotkeys.HotkeyInput)Get(input, "Hotkeys");
        int close = Enumerable.Range(0, commands.Count).First(i => commands[i].ToString() == "Close");
        var closePanel = Get(layout, "Panel");
        var closePoint = PopupTestCenter(Get(((IList)Get(layout, "Buttons"))[close], "Rect"), (float)Get(closePanel, "X"), (float)Get(closePanel, "Y"));
        px = (int)closePoint.X; py = (int)closePoint.Y;
        Frame(false, Keys.W); Frame(true, Keys.W); Frame(false, Keys.W);
        Require(!(bool)Get(popup, "Visible") && !(bool)Get(input, "HotkeyCapture") && !hotkeys.IsSuppressed((int)Keys.W),
            "actual normal shortcut popup close does not start recording or claim held W");
        Frame(false, Keys.W); Frame(false);
        Call(shell, "OpenHotkey", "item-browser.query", rect);
        Call(shell, "OpenHotkey", "item-browser.query", rect);
        Require((bool)Get(popup, "Visible"), "shortcut popup reopens after its ordinary Close");
        prepare.Invoke(popup,new object[]{960f,640f,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration")});
        int record=Enumerable.Range(0,commands.Count).First(i=>commands[i].ToString()=="Record");
        var button=Get(((IList)Get(layout,"Buttons"))[record],"Rect");var panel=Get(layout,"Panel");px=(int)((float)Get(panel,"X")+(float)Get(button,"X")+4);py=(int)((float)Get(panel,"Y")+(float)Get(button,"Y")+4);
        Frame(false,Keys.Kanji);Frame(true,Keys.Kanji);Frame(false,Keys.Kanji);
        Require((bool)Get(popup,"Capturing") && (bool)Get(input,"HotkeyCapture"),"actual popup Record click reaches capture owner with state key present");
        var inventoryBindings = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus;
        var normalInventory = inventoryBindings["Inventory"];
        try
        {
            inventoryBindings["Inventory"] = new List<string> {nameof(Keys.Escape)};
            Frame(false, Keys.Kanji, Keys.Escape); Frame(false, Keys.Kanji, Keys.Escape); Frame(false, Keys.Kanji);
            Require(!(bool)Get(popup,"Capturing") && (bool)Get(state,"Visible") && !Main.LocalPlayer.controlInv,
                "recording owns the same Escape edge without closing F5 or opening native Inventory");
        }
        finally { inventoryBindings["Inventory"] = normalInventory; }
        Frame(false,Keys.Kanji);Frame(true,Keys.Kanji);Frame(false,Keys.Kanji);
        Require((bool)Get(popup,"Capturing"),"recording can rearm after Escape with state key still present");
        int before=clicks;foreground=false;Frame(true,Keys.W);Require(!(bool)Get(popup,"Capturing") && !(bool)Get(input,"HotkeyCapture") && !(bool)Get(popup,"Visible"),"production UpdatePrefix focus cancellation retires actual capture");
        foreground=true;px=py=150;Frame(true,Keys.W);Frame(false);Require(clicks==before,"capture activation tail cannot click");Frame(false);Call(state,"Close");Frame(false);Frame(true);Frame(false);Require(clicks==before+1,"capture retired; new native UI click arrives after release");Frame(false);
        Console.WriteLine("PASS real F5 / HotkeyPopup Record consumer / UpdatePrefix focus cancel / fresh native click after capture tail");
        // Use the real popup/editor/text owner. Only OS IME endpoints are sinks.
        var textType=host.GetType("JueMingR.TerrariaHost.Input.HexTextInput");var ime=textType.GetNestedType("NativeIme",F);
        patch.Patch(ime.GetProperty("Composition",F).GetGetMethod(true),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("ImeComposition",F)));
        patch.Patch(ime.GetProperty("Candidates",F).GetGetMethod(true),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("NoGamepad",F)));
        patch.Patch(ime.GetMethod("Toggle",F),new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks).GetMethod("ImeToggleSink",F)));
        Frame(false,Keys.F5);Frame(false);Frame(false);Call(state,"Navigate",0);
        var style=Get(shell,"StylePopup");var click=style.GetType().GetMethods(F).Single(m=>m.Name=="Click" && m.GetParameters()[0].ParameterType.Name=="EntityLabelKind");
        click.Invoke(style,new object[]{Enum.Parse(click.GetParameters()[0].ParameterType,"Enemy"),rect,0});Require((bool)Get(style,"Visible"),"real style popup available");
        // Match existing popup fixtures: prepare the actual layout before a
        // gesture. Missing geometry legitimately cancels an editor's lease.
        Call(renderer,"Prepare",state,960f,640f,1f);
        Call(style,"Prepare",960f,640f,1f,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration"),Call(shell,"StyleAnchor"));
        var styleLayout = Get(style, "Layout");
        var stylePanel = Get(styleLayout, "Panel");
        var resetCommands = (IList)Get(styleLayout, "Commands");
        int reset = Enumerable.Range(0, resetCommands.Count).First(i => resetCommands[i].ToString() == "Reset");
        var resetPoint = PopupTestCenter(Get(((IList)Get(styleLayout, "Buttons"))[reset], "Rect"), (float)Get(stylePanel, "X"), (float)Get(stylePanel, "Y"));
        px = (int)resetPoint.X; py = (int)resetPoint.Y;
        Frame(false, Keys.W); Frame(true, Keys.W); Frame(false, Keys.W);
        Require((bool)Get(style, "Visible") && !(bool)Get(Get(style, "TextInput"), "Editing") && !hotkeys.IsSuppressed((int)Keys.W),
            "ordinary Style reset gesture stays outside text capture and leaves W unclaimed");
        px=py=150; Frame(false, Keys.W, Keys.Escape); Frame(false, Keys.W); Frame(false, Keys.W);
        Require(!(bool)Get(style, "Visible") && !hotkeys.IsSuppressed((int)Keys.W) && Main.LocalPlayer.controlUp,
            "ordinary Style Esc closes with its own tail while held W resumes");
        Frame(false);
        click.Invoke(style,new object[]{Enum.Parse(click.GetParameters()[0].ParameterType,"Enemy"),rect,0});
        Call(style,"Prepare",960f,640f,1f,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration"),Call(shell,"StyleAnchor"));
        var text=Get(style,"TextInput");Require((bool)Call(text,"Begin",Get(style,"Editor")),"real style text edit starts");expectTextLease=true;Frame(false);expectTextLease=false;
        Console.WriteLine("TEXT editing="+Get(text,"Editing")+" owns="+Get(text,"OwnsTextToken")+" block="+Terraria.Main.blockInput+" writing="+PlayerInput.WritingText+" shellFailed="+Get(shell,"Failed"));
        // Native PlayerInput.UpdateInput clears WritingText at line903 after
        // consuming it. Assert the lease at the actual prefix boundary instead.
        Require((bool)Get(text,"OwnsTextToken") && Terraria.Main.blockInput && observedTextWriting,"actual InputPrefix/BeforeInput acquires popup text token and native writing gate");
        // The raw state bit survives the editor and Escape release. The real
        // text owner must retire only its keyboard tail before native movement.
        Frame(false, Keys.Kanji);
        Main.keyInt[0] = 27; Main.keyString[0] = "\u001b"; Main.keyCount = 1;
        Frame(false, Keys.Kanji, Keys.Escape);
        Require(!(bool)Get(text, "OwnsTextToken") && !Main.blockInput, "Escape retires only the real editor lease");
        Frame(false, Keys.Kanji, Keys.Escape, Keys.W);
        Require(!Main.LocalPlayer.controlUp, "held edit Escape keeps the keyboard tail");
        Frame(false, Keys.Kanji); Frame(false, Keys.Kanji, Keys.W); Frame(false, Keys.Kanji, Keys.W);
        Require(Main.LocalPlayer.controlUp, "native movement after editor release while state key persists");
        Frame(false);
        Require((bool)Call(text, "Begin", Get(style, "Editor")), "same popup can start a new real text edit");
        Frame(false);
        var foreign=new object();foreignFixture=foreign;Terraria.Main.CurrentInputTextTakerOverride=foreign;PlayerInput.WritingText=true;foreground=false;Frame(false);
        Require(!(bool)Get(text,"Editing") && Object.ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride,foreign) && !Terraria.Main.blockInput && foreignWritingAtOuter,"focus cleanup restores own block but preserves replacement foreign text owner before native refresh");foreignFixture=null;
        // Retire only the fixture's foreign owner, as that owner's own action.
        Terraria.Main.CurrentInputTextTakerOverride=null;PlayerInput.WritingText=false;foreground=true;Frame(false);Frame(false);Call(state,"Close");Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);
        Require(Terraria.Main.LocalPlayer.controlUp && !(bool)Get(shell,"Failed"),"native key after genuine release and text-owner retirement");
        Console.WriteLine("PASS real StylePopup text lease / Shell.BeforeInput / focus cancellation preserves foreign owner / original Triggers key resumes");
    }
    static bool ImeComposition(ref string __result){__result="";return false;}
    static bool ImeToggleSink(bool enabled){PlayerInput.WritingText=enabled;return false;}
    static void ObserveBeforeNativeInput(){if(expectTextLease)observedTextWriting=Terraria.Main.CurrentInputTextTakerOverride!=null && Terraria.Main.blockInput && PlayerInput.WritingText;}
    static void UseGestureClaim()
    {
        // F5 itself does not claim an attack tail. Reuse the existing quick
        // item's missing-provider consumer, without executing ItemCheck.
        var quick=Get(context,"QuickItems");var settings=Get(quick,"Settings");var features=settings.GetType().Assembly;
        var entryType=features.GetType("JueMingR.Features.QuickItems.QuickItemEntry");var mode=features.GetType("JueMingR.Features.QuickItems.QuickItemMode");
        var entry=Activator.CreateInstance(entryType,new object[]{"0123456789abcdef0123456789abcdef",50,Enum.Parse(mode,"Use"),false,true});
        var array=Array.CreateInstance(entryType,1);array.SetValue(entry,0);var doc=Activator.CreateInstance(features.GetType("JueMingR.Features.QuickItems.QuickItemDocument"),new object[]{false,true,array});
        var args=new object[]{doc,Get(entry,"Id"),null,true,true};Require((bool)settings.GetType().GetMethod("TryChange",F).Invoke(settings,args),"existing quick settings command admitted");
        var until=DateTime.UtcNow.AddSeconds(8);while((bool)Get(settings,"Busy")){if(DateTime.UtcNow>until)throw new Exception("existing settings deadline");Call(quick,"Poll");System.Threading.Thread.Sleep(1);}Call(quick,"Poll");
        var bind=typeof(NativeQuickGestureChecks).GetMethod("Bind",F);bind.Invoke(null,new object[]{Get(Get(shell,"hotkeys"),"Bindings"),Get(entry,"ActionId"),"Mouse1"});
        var tail=Get(input,"UseGesture");Frame(false);Frame(false);int before=clicks;Frame(true);
        Require((bool)Get(tail,"HasTail") && !Terraria.Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft,"actual Shell dispatch -> quick Request -> Claim subtracts its mapped attack");Frame(true);foreground=false;Frame(false);
        Require((bool)Get(tail,"HasTail"),"background synthetic release cannot retire claimed physical tail");foreground=true;Frame(true);Frame(false);
        Require(!(bool)Get(tail,"HasTail") && clicks==before,"genuine focused release retires owned tail without leaked click");Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);
        Require(Terraria.Main.LocalPlayer.controlUp,"independent original key remains usable after real claimed tail");
        Console.WriteLine("PASS real quick-item missing-provider dispatch / UseGesture Claim / background release unknown / genuine release retires self-owned tail");
    }
}

}
