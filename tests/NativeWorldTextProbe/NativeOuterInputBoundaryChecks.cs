using System;
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
internal static class NativeOuterInputBoundaryChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static string root,hostPath; static object input,context,shell;
    static KeyboardState keyboard; static MouseState mouse; static bool foreground=true,skip,throwBody;
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
        Terraria.Program.SavePath=Path.Combine(output,"user-data");Directory.CreateDirectory(Terraria.Program.SavePath);
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
            NativeQuickItemChecks.Run(Exercise,shortFeedback:true,candidateAssembly:hostPath);
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
            Terraria.Main.gameMenu=false;Call(context,"UpdateRuntime");Frame(false);Frame(false);before=clicks;Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"world original Triggers.CopyInto fresh key");Frame(false);
            foreground=false;Frame(true,Keys.W);Require(!Terraria.Main.LocalPlayer.controlUp,"world loss blocks consumer");foreground=true;Frame(true,Keys.W);Frame(false);Require(clicks==before && !Terraria.Main.LocalPlayer.controlUp,"world activation consumed");Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"world release allows fresh key");Frame(false);
            skip=true;Frame(false,Keys.W);Require(!(bool)Get(input,"CanUseInput") && (bool)Get(input,"CanRetainIntent"),"outer skip revokes epoch permission without retiring intent");skip=false;Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp,"next sampled frame recovers after skip");Frame(false);
            throwBody=true;try{Frame(false,Keys.W);throw new Exception("expected isolated exception");}catch(TargetInvocationException){Require(!(bool)Get(input,"CanUseInput"),"exception before mapping leaves no epoch permission");}finally{throwBody=false;}
            Frame(false);Frame(false,Keys.W);Frame(false,Keys.W);Require(Terraria.Main.LocalPlayer.controlUp && !(bool)Get(shell,"Failed"),"fresh input after outer exception; shell stays valid");
            Console.WriteLine("PASS outer world focus/reactivation / skipped and exceptional input epoch; real Shell consumers remain available");
            CaptureAndText(patch,host);
            UseGestureClaim();
        }
        finally{patch.UnpatchAll(patch.Id);worker.GetField("hookCommitted",F).SetValue(null,0);worker.GetField("postfixContext",F).SetValue(null,null);}
    }
    static void Frame(bool down,params Keys[] keys)
    {
        keyboard=new KeyboardState(keys);mouse=new MouseState(px+(frames++%2),py,0,down?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);FocusHelper.IsSelectedApplication=foreground;
        Call(main,"Update",new GameTime(TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(250)));
        if(!skip){Terraria.Main.LocalPlayer.controlUp=false;PlayerInput.Triggers.Current.CopyInto(Terraria.Main.LocalPlayer);ui.Update(new GameTime(TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(250)));}
    }
    static bool IsolatedUpdate(){if(throwBody)throw new InvalidOperationException("isolated before-input exception");if(foreignFixture!=null && !foreground)foreignWritingAtOuter=Object.ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride,foreignFixture) && !Terraria.Main.blockInput && PlayerInput.WritingText;if(!skip)Call(main,"DoUpdate_HandleInput");return false;}
    static bool KeyboardPrefix(ref KeyboardState __result){keyboardSamples++;__result=keyboard;return false;}
    static bool MousePrefix(ref MouseState __result){__result=mouse;return false;}
    static bool NoGamepad(ref bool __result){__result=false;return false;}
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
        var layout=Get(popup,"Layout");var commands=(IList)Get(layout,"Commands");int record=Enumerable.Range(0,commands.Count).First(i=>commands[i].ToString()=="Record");
        var button=Get(((IList)Get(layout,"Buttons"))[record],"Rect");var panel=Get(layout,"Panel");px=(int)((float)Get(panel,"X")+(float)Get(button,"X")+4);py=(int)((float)Get(panel,"Y")+(float)Get(button,"Y")+4);
        Frame(false);Frame(true);Frame(false);Require((bool)Get(popup,"Capturing") && (bool)Get(input,"HotkeyCapture"),"actual popup Record click reaches capture owner");
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
        var text=Get(style,"TextInput");Require((bool)Call(text,"Begin",Get(style,"Editor")),"real style text edit starts");expectTextLease=true;Frame(false);expectTextLease=false;
        Console.WriteLine("TEXT editing="+Get(text,"Editing")+" owns="+Get(text,"OwnsTextToken")+" block="+Terraria.Main.blockInput+" writing="+PlayerInput.WritingText+" shellFailed="+Get(shell,"Failed"));
        // Native PlayerInput.UpdateInput clears WritingText at line903 after
        // consuming it. Assert the lease at the actual prefix boundary instead.
        Require((bool)Get(text,"OwnsTextToken") && Terraria.Main.blockInput && observedTextWriting,"actual InputPrefix/BeforeInput acquires popup text token and native writing gate");
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
