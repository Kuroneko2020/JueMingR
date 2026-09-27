using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Items;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeShortFeedbackChecks
    {
        internal static object[] Entries(object display) { return ((IEnumerable)Get(display,"entries")).Cast<object>().ToArray(); }
        internal static bool Has(object display,string text) { return Entries(display).Any(e=>(string)Get(e,"Text")==text); }
        internal static void Run(object context)
        {
            object shell=Get(context,"Shell"), items=Get(context,"items"), display=Get(context,"ShortFeedback"), input=Get(context,"Input");
            var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");
            var feedback=Get(Get(shell,"hotkeys"),"Feedback");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(items,"ControlsEnabled");});
            Set(Get(context,"onboarding"),"CanPresent",(Func<bool>)(()=>false));
            // Like the existing CPU world-layer checks, qualify only the render
            // installation gate; real fonts/pixels are checked in the visual scope.
            foreach(string name in new[]{"Labels","WorldTargets","WorldObjects","Guidance"})
            { var host=Get(context,name);Set(host,"LayerStatus",Enum.Parse(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Rendering.WorldLayerStatus"),"Ready")); }
            Main.LocalPlayer.position=new Vector2(480,400);Main.screenPosition=Vector2.Zero;
            Main.showItemText=true;PopupText.ClearAll();
            Require(registry.Find("items.auto-stack.toggle").Invoke(HotkeyContext.SinglePlayer),"Actual registered stack command admitted");
            Call(context,"UpdateRuntime");
            Require(PopupText.popupText.Any(p=>p.active&&p.name=="自动堆叠 已开启"),"Actual registered hotkey must present applied stack state in native popup pool");
            Call(display,"Clear");
            var preferences=Get(items,"Preferences");
            Call(items,"Change",((ItemAutomationSettings)Get(preferences,"Value")).WithEnabled(ItemActionKind.Stack,false));Call(items,"PollPreferences");
            Require((int)Get(display,"Count")==0,"F5/domain setter does not manufacture hotkey success");
            string[] once={"information-window.adjust","announcement.send","item-browser.query","tools.mining.select"};
            var actions=registry.Actions.Where(a=>!once.Contains(a.Id) && !a.Id.StartsWith("items.quick-use.",StringComparison.Ordinal)).ToArray();
            Require(actions.Length==40 && once.All(id=>registry.Find(id)!=null),"40 registered switches plus four distinct once-actions");
            foreach(var action in actions)
            {
                Call(display,"Clear");
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return action.Invoke(HotkeyContext.SinglePlayer);});
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return Entries(display).Any(e=>(string)Get(e,"Id")==action.Id);});
                string first=(string)Get(Entries(display).Single(),"Text");
                string name=action.Name=="快捷宣告开关"?"快捷宣告":action.Name=="快捷物品开关"?"快捷物品":action.Name;
                Require(first.StartsWith(name,StringComparison.Ordinal) && (first.EndsWith(" 已开启") || first.EndsWith(" 已关闭")),"result text: "+action.Id+" = "+first);
                Require(action.Invoke(HotkeyContext.SinglePlayer),"reverse command: "+action.Id);
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return Entries(display).Any(e=>(string)Get(e,"Id")==action.Id && (string)Get(e,"Text")!=first);});
                string second=(string)Get(Entries(display).Single(),"Text");
                Require(first.Substring(0,first.Length-3)==second.Substring(0,second.Length-3) && first.EndsWith("已开启")!=second.EndsWith("已开启"),"captured mode and latest result: "+action.Id);
            }
            Call(display,"Clear");
            object recovery=Get(context,"Recovery");
            Call(recovery,"Set",0,2);NativeQuickItemChecks.Until(()=>{Call(recovery,"Poll");return (int)Call(recovery,"Value",0)==2;});
            Require(registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"smart close admitted");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return Has(display,"自动回血（智能） 已关闭");});
            Require(registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"recovery enable admitted");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(Get(recovery,"Potions"),"Ready");});
            Require((int)Call(recovery,"Value",0)==2,"off-to-on restores the last selected smart mode");
            Require(Has(display,"自动回血（智能） 已开启"),"successful smart restoration reports its actual mode");
            Call(recovery,"Set",0,0);NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(Get(recovery,"Potions"),"Ready");});
            Require(!Has(display,"自动回血（智能） 已开启"),"later F5 command retires active result");
            NativeRecoveryModeChecks.Run(context,registry,display);
            InputDispatch(context,registry,display);
            AsyncFailures(context,registry,display,feedback);
            Call(display,"Clear");
            foreach(var action in actions.Take(6)) { Require(action.Invoke(HotkeyContext.SinglePlayer),"full display cannot block commands");Call(context,"UpdateRuntime"); }
            Require((int)Get(display,"Count")==4 && (int)Get(feedback,"PendingCount")==0,"four active results and no hidden queue");
            Call(display,"Clear");
            var stack=registry.Find("items.auto-stack.toggle");var sell=registry.Find("items.auto-sell.toggle");
            Require(stack.Invoke(HotkeyContext.SinglePlayer) && sell.Invoke(HotkeyContext.SinglePlayer),"same-frame switches");Call(context,"UpdateRuntime");
            Require(Entries(display).Length==2,"shared preferences preserve different same-frame switches");
            string latest=(string)Get(Entries(display).First(e=>(string)Get(e,"Id")==stack.Id),"Text");
            stack.Invoke(HotkeyContext.SinglePlayer);Call(context,"UpdateRuntime");
            Require(Entries(display).Length==2 && !Has(display,latest),"same identity replaces old result");
            Set(Entries(display).First(),"Valid",(Func<bool>)(()=>{throw new InvalidOperationException("isolated display failure");}));
            Call(display,"Refresh");Require(!(bool)Get(shell,"Failed") && (bool)Get(items,"ControlsEnabled"),"display failure does not disable healthy shell or business");
            int active=PopupText.popupText.Count(p=>p.active);Call(display,"Discard",Main.LocalPlayer,"自动丢弃了23个木头");
            Require(PopupText.popupText.Count(p=>p.active)==active,"voluntary result takes display priority over discard");
            Call(display,"Clear");PopupText.ClearAll();
            for(int i=0;i<20;i++)PopupText.NewText(new AdvancedPopupRequest{Text="foreign-"+i,Color=Color.White,DurationInFrames=180},new Vector2(100,300));
            var foreign=PopupText.popupText.Select(p=>p.name).ToArray();stack.Invoke(HotkeyContext.SinglePlayer);Call(context,"UpdateRuntime");
            Require(Entries(display).All(e=>(bool)Get(e,"Fallback")) && foreign.SequenceEqual(PopupText.popupText.Select(p=>p.name)),"full pool uses fallback without foreign eviction");
            Call(display,"Clear");PopupText.ClearAll();stack.Invoke(HotkeyContext.SinglePlayer);
            var entry=Entries(display).Single();var popup=(PopupText)Get(entry,"Popup");string same=popup.name;
            PopupText.ResetText(popup);popup.active=popup.freeAdvanced=true;popup.context=PopupTextContext.Advanced;popup.name=popup.displayText=new string(same.ToCharArray());popup.lifeTime=123;
            Call(display,"Refresh");Require(popup.active && popup.lifeTime==123 && (bool)Get(entry,"Fallback"),"same-text successor on same native object is not owned");
            Call(display,"Clear");Require(popup.active,"clear preserves successor");PopupText.ClearAll();
            stack.Invoke(HotkeyContext.SinglePlayer);PopupText.ClearAll();Call(display,"Refresh");
            Require(Entries(display).All(e=>(bool)Get(e,"Fallback")) && PopupText.popupText.All(p=>!p.active),"ClearAll loses lease without re-admission");
            Call(display,"Clear");Main.showItemText=false;stack.Invoke(HotkeyContext.SinglePlayer);
            Require(Entries(display).Length==1 && (bool)Get(Entries(display).Single(),"Fallback") && !Main.showItemText,"item-text preference stays unchanged while confirmation remains presentable");
            Main.showItemText=true;Main.mapFullscreen=true;Call(display,"Refresh");Require(Entries(display).All(e=>(bool)Get(e,"Fallback")),"map continues one fallback lifetime");Main.mapFullscreen=false;
            Set(Entries(display).Single(),"Expires",0d);Call(display,"Refresh");Require((int)Get(display,"Count")==0,"expired result never waits for a later scene");
            int passes=(int)Get(display,"Passes");for(int i=0;i<1000;i++){Call(display,"Refresh");Call(display,"Draw");Call(feedback,"Poll");}
            Require((int)Get(display,"Passes")==passes,"1000 idle calls do no display work");
            stack.Invoke(HotkeyContext.SinglePlayer);Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));Call(input,"BeginUpdate");Call(context,"UpdateRuntime");
            Require((int)Get(display,"Count")==0 && (int)Get(feedback,"PendingCount")==0,"focus loss has no deferred replay");
            Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));NativeQuickItemChecks.Sample(input,new Keys[0]);NativeQuickItemChecks.Sample(input,new Keys[0]);
            stack.Invoke(HotkeyContext.SinglePlayer);Call(Get(Get(context,"Runtime"),"SharedRuntime"),"InvalidateSession");
            Require((int)Get(display,"Count")==0 && (int)Get(feedback,"PendingCount")==0,"session end releases all requests synchronously");
            Call(context,"UpdateRuntime");
            MapFailure(context,registry,display);
            Console.WriteLine("PASS: 37 registered consumers, runtime/commit separation, modes, owner tokens, bounded replacement, native ownership, fallback, no network and idle/session cleanup.");
        }
        private static void AsyncFailures(object context,HotkeyRegistry registry,object display,object feedback)
        {
            object quick=Get(context,"QuickItems");var original=(QuickItemSettings)Get(quick,"Settings");
            var store=new NativeQuickUiChecks.UiStore(QuickItemDocument.Encode(QuickItemDocument.Empty));
            using(var settings=new QuickItemSettings(store))
            {
                try
                {
                    Set(quick,"Settings",settings);NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return settings.Loaded;});
                    Call(display,"Clear");store.Gate.Reset();
                    var action=registry.Find("items.quick-items.toggle");Require(action.Invoke(HotkeyContext.SinglePlayer),"async accepted");
                    Call(feedback,"Poll");Require(settings.Busy && !settings.Enabled && (int)Get(display,"Count")==0 && (int)Get(feedback,"PendingCount")==1,"acceptance/suspension is not success");
                    Require(!action.Invoke(HotkeyContext.SinglePlayer),"Busy blocks duplicate");
                    store.Gate.Set();NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return !settings.Busy;});
                    Require(Has(display,"快捷物品 已开启") && settings.Enabled,"reliable commit publishes success");
                    store.Fail=true;Require(action.Invoke(HotkeyContext.SinglePlayer),"failing close accepted");
                    Require(GetOptional(display,"map")==null,"replacing last active result while save is pending releases the map lease immediately");
                    NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return !settings.Busy;});
                    Require(!settings.Enabled && !settings.CompletionSucceeded && (int)Get(display,"Count")==0 && settings.QuickMessage!=null,"failed suspension is not successful close");
                    int alerts=0;settings.TakeFeedback(text=>alerts++);settings.TakeFeedback(text=>alerts++);Require(alerts==1,"original error channel remains unconsumed");
                    store.Fail=false;Require(action.Invoke(HotkeyContext.SinglePlayer),"retry accepted");
                    NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
                    string reason;Require(settings.TryChange(settings.Current.Toggles(true,settings.Current.Enabled),null,out reason,changeQuick:false),"later F5 same-file command");
                    NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return !settings.Busy;});
                    Require((int)Get(display,"Count")==0 && (int)Get(feedback,"PendingCount")==0,"later same-owner F5 cancels delayed success");
                    store.Gate.Reset();Require(action.Invoke(HotkeyContext.SinglePlayer),"cross-session pending command");
                    Call(Get(Get(context,"Runtime"),"SharedRuntime"),"InvalidateSession");
                    Require((int)Get(feedback,"PendingCount")==0,"session exit synchronously cancels pending provenance");
                    store.Gate.Set();NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return !settings.Busy;});
                    Require((int)Get(display,"Count")==0,"settlement in next session never replays old success");
                    store.Fail=store.Unknown=true;Require(action.Invoke(HotkeyContext.SinglePlayer),"unknown accepted");NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return !settings.Busy;});
                    Require(settings.Protected && settings.CommitUnconfirmed && (int)Get(display,"Count")==0 && !action.Invoke(HotkeyContext.SinglePlayer),"unknown stays protected without success or retries");
                }
                finally{store.Gate.Set();Set(quick,"Settings",original);Set(quick,"published",-1L);Call(quick,"Poll");Call(display,"Clear");}
            }
        }
        private static int acquireCalls;
        private static void RejectMap() { acquireCalls++;throw new InvalidOperationException("isolated map presentation failure"); }
        private static void MapFailure(object context,HotkeyRegistry registry,object display)
        {
            Call(display,"Clear");var harmony=new Harmony("JueMingR.Tests.ShortFeedbackMapFailure");
            var method=display.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.FullscreenMapDrawing").GetMethod("Acquire",BindingFlags.Static|BindingFlags.NonPublic);
            try
            {
                harmony.Patch(method,new HarmonyMethod(typeof(NativeShortFeedbackChecks).GetMethod(nameof(RejectMap),BindingFlags.Static|BindingFlags.NonPublic)));
                acquireCalls=0;Require(registry.Find("items.auto-stack.toggle").Invoke(HotkeyContext.SinglePlayer),"map failure cannot reject the original command");
                for(int i=0;i<20;i++)Call(display,"Refresh");
                Require(acquireCalls==1 && Entries(display).Length==1,"failed map hook admission is attempted once per active display lifetime, not retried every frame");
            }
            finally{harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);Call(display,"Clear");}
        }
        private static void InputDispatch(object context,HotkeyRegistry registry,object display)
        {
            object shell=Get(context,"Shell"), input=Get(context,"Input");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            foreach(var pair in new[]{new[]{"items.auto-stack.toggle","J"},new[]{"items.auto-sell.toggle","K"},new[]{"items.auto-discard.toggle","Mouse4"}})
            {
                HotkeyChord chord;string reason;long command;Require(HotkeyChord.TryParse(pair[1],out chord,out reason),"isolated chord");
                Require(bindings.TrySet(pair[0],chord,null,out command,out reason),"isolated binding accepted");NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});
            }
            Call(display,"Clear");NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");
            NativeQuickItemChecks.Sample(input,new[]{Keys.J,Keys.K});Call(shell,"ProcessInput");Call(context,"UpdateRuntime");
            Require(Entries(display).Length==2,"real keyboard dispatch applies two different main keys in one frame");
            var tokens=Entries(display);NativeQuickItemChecks.Sample(input,new[]{Keys.J,Keys.K});Call(shell,"ProcessInput");
            Require(tokens.SequenceEqual(Entries(display)),"holding keys never repeats a result");
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");Call(display,"Clear");
            Main.drawingPlayerChat=true;NativeQuickItemChecks.Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");
            Require((int)Get(display,"Count")==0,"chat blocks command and feedback");Main.drawingPlayerChat=false;
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");
            Main.keyState=new KeyboardState();Terraria.GameInput.PlayerInput.MouseInfo=new MouseState(940,620,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Pressed,ButtonState.Released);
            Terraria.GameInput.PlayerInput.Triggers.Reset();Terraria.GameInput.PlayerInput.Triggers.Update();
            Call(input,"BeginUpdate");Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");Call(context,"UpdateRuntime");
            Require(Entries(display).Length==1 && (string)Get(Entries(display).Single(),"Id")=="items.auto-discard.toggle","real mouse side-button dispatch reaches same feedback consumer");
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");Call(display,"Clear");
        }
    }
}
