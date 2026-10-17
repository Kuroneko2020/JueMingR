using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeToolsUiChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAimUiChecks
    {
        private static object traceHost,traceShell,tracePopup;private static HotkeyBindings traceBindings;
        // Exercise completion at the real later AfterUpdate Poll while leaving
        // the actual storage worker/compile active. This fixture defers only
        // the earlier Poll inside this popup; no product timing is changed.
        private static bool deferPopupPoll;private static int popupPollDepth;
        private static void EnterPopup(object __instance,out bool __state){__state=ReferenceEquals(__instance,tracePopup);if(__state)popupPollDepth++;}
        private static Exception LeavePopup(Exception __exception,bool __state){if(__state)popupPollDepth--;return __exception;}
        private static bool PollBoundary(HotkeyBindings __instance)
        {return !deferPopupPoll || popupPollDepth==0 || !ReferenceEquals(__instance,traceBindings);}
        private static void Dispatch(HotkeyBindings __instance,HotkeyInput __0,bool __2,string __3)
        {
            if(!ReferenceEquals(__instance,traceBindings) || __3!=null || !__0.IsDown((int)Keys.F11))return;
            var settings=(ObservationSettings)Get(traceHost,"Settings");Console.WriteLine("AIM DISPATCH new="+__0.IsNew((int)Keys.F11)+" modifiers="+__0.Modifiers+" reliable="+__0.Reliable+" suppressed="+__0.IsSuppressed((int)Keys.F11)+" permitted="+__2+" configure="+Get(traceHost,"CanConfigure")+" ready="+settings.Ready+" accepted="+settings.AcceptedCommandId+" pointer="+Get(traceShell,"OwnsPointer")+" mouseInterface="+Main.LocalPlayer.mouseInterface);
        }
        private static void Neutral(object context,object shell,ObservationSettings settings,HotkeyBindings bindings)
        {
            var input=Get(context,"Input");var keys=(HotkeyInput)Get(input,"Hotkeys");
            NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready && !bindings.Busy && (bool)Get(input,"CanStartActions") && !(bool)Get(input,"HotkeyCapture") && !(bool)Get(input,"HotkeyPointerOwned") && !keys.HasSuppressedKeys && !(bool)Get(shell,"OwnsPointer") && !Main.LocalPlayer.mouseInterface;});
            Console.WriteLine("AIM NEUTRAL binding="+bindings.Get("combat.aim")?.Text+" bindingCompletionId="+bindings.CompletionId+" modifiers="+keys.Modifiers+" down="+keys.IsDown((int)Keys.F11)+" reliable="+keys.Reliable+" accepted="+settings.AcceptedCommandId+" completed="+settings.CompletedCommandId+" aim="+settings.Value.Aim+" path="+settings.Value.Path+" marker="+settings.Value.Marker);
            Require(!keys.IsDown((int)Keys.F11) && !keys.IsDown((int)Keys.LeftControl),"fresh chord follows actual trusted neutral input, not merely a storage completion");
        }
        // A leaf for the owner's exact copy decision. It exercises the actual
        // Shell -> ResolveHint -> PrepareHints path without the unrelated Aim
        // binding, attack, NPC or world-render checks in Run below.
        internal static void RunHints(object context,ProbeGraphics graphics,string output)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var host=Get(context,"CombatObservation");
            var renderer=Get(shell,"renderer");var controls=Get(renderer,"CombatObservationControls");var hint=Get(renderer,"HintLayout");
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();
            try
            {
                foreach(bool mode in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,mode,mode,mode,25,mode,true));
                    UiFrame(context,Vector2.Zero,false);
                    if(!(bool)Get(state,"Visible")){UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);}
                    Nav(context,8);
                    Require(Elements().Any(e=>(string)GetOptional(e,"Text")=="寻路预测") && !Elements().Any(e=>(string)GetOptional(e,"Text")=="NPC寻路预测"),"actual path row uses the accepted short name");
                    foreach(string id in new[]{"combat.aim","combat.collision-display","combat.npc-path"})
                        Require(Elements().Count(e=>(string)GetOptional(e,"HotkeyTarget")==id)==1,"display copy keeps the stable action ID: "+id);
                    foreach(string command in new[]{"ObservationPolicy","ObservationCenter","ObservationDummy","ObservationMarker","ObservationRadius"})
                        Require(Elements().Count(e=>Get(e,"Command").ToString()==command)==1,"conditional card command remains unique: "+command);
                    Require((string)Get(Find("ObservationCenter"),"Text")== (mode?"鼠标中心":"玩家中心"),"center button name remains unchanged");
                    string policy=mode?"考虑攻击可达性，适合复杂地形":"忽略遮挡，瞄准最新的";
                    string center=mode?"瞄准距离光标最近的敌怪，下面滑条改半径":"瞄准距离玩家最近的敌怪";
                    Show(Find("ObservationPolicy"),policy);
                    Show(Find("ObservationCenter"),center);
                    Show(Name("寻路预测"),"显示NPC可能的未来路径");
                    Show(Name("碰撞箱显示"),"↓");Require(Text().Length==1 && Text()[0]=='\u2193',"collision hint is only U+2193");
                    if(mode)graphics.Image(Path.Combine(output,"collision-arrow-hint.png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,960,640);
                    foreach(var empty in new[]{Name("辅助瞄准"),Name("辅助瞄准设置"),Find("ObservationDummy"),Find("ObservationMarker")})
                    {Show(Find("ObservationPolicy"),policy);Show(empty,null);}
                    var field=Find("ObservationRadius");var fieldPoint=Position(field);var rect=Get(field,"Rect");
                    var drag=Get(shell,"CombatRadius");UiFrame(context,fieldPoint,false);var track=Get(drag,"Track");
                    float trackX=(float)Get(track,"X"),trackY=(float)Get(track,"Y"),trackWidth=(float)Get(track,"Width");
                    var view=Get(Get(state,"Layout"),"Viewport");
                    var labelPoint=new Vector2((float)Get(state,"X")+(float)Get(view,"X")+(float)Get(rect,"X")+8,fieldPoint.Y);
                    foreach(var point in new[]{labelPoint,new Vector2(trackX+trackWidth*.2f,trackY+7),new Vector2(trackX+trackWidth*.5f,trackY+7)})
                    {Show(Find("ObservationPolicy"),policy);At(point,null);}
                    Require((int)Get(drag,"Value")==25 && (bool)Get(drag,"Available")==mode,"radius visible value and player/mouse edit gate remain unchanged");
                    foreach(string command in new[]{"ObservationDummy","ObservationMarker","ObservationRadius"})
                        Require(Call(controls,"Hint",Get(Find(command),"Command"))==null,"removed hints are null in both option states: "+command);
                    if(mode){Show(Find("ObservationCenter"),center);graphics.Image(Path.Combine(output,"mouse-center-hint.png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,960,640);}
                }
                var on=Find("ObservationAimOn");var off=Find("ObservationAimOff");Show(on,null);Show(off,null);
                var attack=Get(host,"Attack");Call(attack,"FailLocal",new InvalidOperationException("Aim hint fault oracle"));
                try{string reason=(string)Call(host,"Unavailable",6);Require(reason!=null,"real Aim fault produces a reason");Show(on,reason);Show(off,reason);}
                finally{Call(attack,"Reset");}
                Console.WriteLine("PASS CombatHints: exact names/four mode hints/single arrow; actual null regions hide prior boxes, both toggle states and full radius field; stable actions/card/value/edit gate; real Aim fault feedback retained.");
            }
            finally{Call(shell,"CloseAndSubmitPosition");NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            object[] Elements(){return ((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().ToArray();}
            object Find(string command){return Elements().Single(e=>Get(e,"Command").ToString()==command);}
            object Name(string text){return Elements().Single(e=>(string)GetOptional(e,"Text")==text);}
            string Text(){return string.Concat(((IEnumerable)Get(hint,"Lines")).Cast<object>().Select(e=>(string)Get(e,"Text")));}
            void Show(object element,string expected){At(Position(element),expected);}
            void At(Vector2 point,string expected)
            {
                UiFrame(context,point,false);graphics.Pixels(()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix);
                Require((bool)Get(hint,"Visible")== (expected!=null),"actual hint visibility at "+point+" expected="+(expected??"null"));
                if(expected!=null)Require(Text()==expected,"actual rendered hint matches exact owner text: "+expected);
            }
            Vector2 Position(object element)
            {
                var rect=Get(element,"Rect");float y=(float)Get(rect,"Y"),h=(float)Get(rect,"Height");
                for(int i=0;i<80;i++){var view=Get(Get(state,"Layout"),"Viewport");float scroll=(float)Get(state,"Scroll");if(y>=scroll && y+h<=scroll+(float)Get(view,"Height"))return (Point(rect)+new Vector2((float)Get(state,"X")+(float)Get(view,"X"),(float)Get(state,"Y")+(float)Get(view,"Y")-scroll))*Main.UIScale;UiFrame(context,(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale,false,new Keys[0],y<scroll?120:-120);}
                throw new InvalidOperationException("real wheel must reveal hint region");
            }
        }
        internal static void Reload(object context)
        {
            var shell=Get(context,"Shell");var host=Get(context,"CombatObservation");var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");var settings=(ObservationSettings)Get(host,"Settings");
            NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return bindings.Loaded && settings.Ready;});
            var chord=bindings.Get("combat.aim");Require(chord!=null && chord.MainKey==(int)Keys.F11 && chord.Modifiers==HotkeyModifiers.LeftControl,"new process reloads the complete saved public Aim chord through its actual storage owner");Require(!settings.Value.Aim,"prior probe's committed final OFF survives reload");Neutral(context,shell,settings,bindings);long prior=settings.AcceptedCommandId;
            UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F11);UiFrame(context,Vector2.Zero,false);NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});Require(settings.CompletionSucceeded && settings.Value.Aim && settings.AcceptedCommandId==prior+1,"reloaded real binding dispatches a fresh edge into reliable Aim configuration");
            Console.WriteLine("PASS new-process Aim binding/configuration reload and actual gameplay dispatch.");NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var host=Get(context,"CombatObservation");var settings=(ObservationSettings)Get(host,"Settings");var drag=Get(shell,"CombatRadius");
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,true,false));
            Main.screenWidth=960;Main.screenHeight=440;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();UiFrame(context,Vector2.Zero,false);UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);Nav(context,8);
            var audit=new Harmony("JueMingR.Tests.AimUiDispatch");traceHost=host;traceShell=shell;traceBindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");audit.Patch(typeof(HotkeyBindings).GetMethod("Dispatch"),prefix:new HarmonyMethod(typeof(NativeCombatAimUiChecks).GetMethod("Dispatch",BindingFlags.Static|BindingFlags.NonPublic)));
            tracePopup=Get(shell,"HotkeyPopup");audit.Patch(typeof(HotkeyBindings).GetMethod("Poll"),prefix:new HarmonyMethod(typeof(NativeCombatAimUiChecks),"PollBoundary"));audit.Patch(tracePopup.GetType().GetMethod("Process",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(NativeCombatAimUiChecks),"EnterPopup"),finalizer:new HarmonyMethod(typeof(NativeCombatAimUiChecks),"LeavePopup"));
            try
            {
                var renderer=Get(shell,"renderer");var controls=Get(renderer,"CombatObservationControls");var aimOn=Find("ObservationAimOn");var aimOff=Find("ObservationAimOff");
                Require(Call(controls,"Hint",Get(aimOn,"Command"))==null && Call(controls,"Hint",Get(aimOff,"Command"))==null,"healthy Aim buttons do not repeat the name-row purpose");
                UiFrame(context,Position(aimOn),false);graphics.Pixels(()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix);Require(!(bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual healthy ON hover is empty");
                var name=Elements().Single(e=>(string)GetOptional(e,"Text")=="辅助瞄准");UiFrame(context,Position(name),false);graphics.Pixels(()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix);var hint=Get(renderer,"HintLayout");Require(!(bool)Get(hint,"Visible"),"actual Aim name hover has no purpose hint");
                var attack=Get(host,"Attack");Call(attack,"FailLocal",new InvalidOperationException("Aim UI fault oracle"));try{string reason=(string)Call(host,"Unavailable",6);Require(reason!=null && (string)Call(controls,"Hint",Get(aimOn,"Command"))==reason && (string)Call(controls,"Hint",Get(aimOff,"Command"))==reason,"both Aim buttons retain the real unavailable reason");UiFrame(context,Position(aimOff),false);graphics.Pixels(()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix);hint=Get(renderer,"HintLayout");Require((bool)Get(hint,"Visible") && string.Concat(((IEnumerable)Get(hint,"Lines")).Cast<object>().Select(e=>(string)Get(e,"Text")))==reason,"actual faulty OFF hover retains unavailable feedback");}finally{Call(attack,"Reset");}
                Apply("ObservationAimOn");Require(settings.Value.Aim && Elements().Any(e=>Get(e,"Command").ToString()=="ObservationRadius"),"natural ON button commits before showing the conditional card");
                var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");var key=Elements().Single(e=>(string)GetOptional(e,"HotkeyTarget")=="combat.aim");var keyPoint=Position(key);Click(context,keyPoint);Click(context,keyPoint);var popup=Get(shell,"HotkeyPopup");Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")=="combat.aim","real Aim binding button opens its own public action");
                PopupClick(context,popup,"Record");deferPopupPoll=true;UiFrame(context,Vector2.Zero,false,Keys.LeftControl);UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F11);UiFrame(context,Vector2.Zero,false);
                NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});Require(bindings.CompletionSucceeded && bindings.CompletionAction=="combat.aim" && bindings.Get("combat.aim").MainKey==(int)Keys.F11 && bindings.Get("combat.aim").Modifiers==HotkeyModifiers.LeftControl,"real recording commits complete Aim chord through the shared worker");
                Require((long)Get(popup,"pendingCommand")==bindings.CompletionId && Get(Get(popup,"Feedback"),"Kind").ToString()=="Saving","actual AfterUpdate Poll completion precedes this popup's pending feedback consumption");deferPopupPoll=false;
                long boundCommand=bindings.CompletionId;int savedGeneration=(int)Get(Get(popup,"Layout"),"Generation");
                // Storage completion is not layout completion. Consume this
                // command's popup feedback on trusted neutral frames before
                // reading the Close location, then send exactly one gesture.
                NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return bindings.CompletionId==boundCommand && (long)Get(popup,"pendingCommand")==0 && ReferenceEquals(Get(popup,"Feedback"),bindings.Feedback);});
                Require((int)Get(Get(popup,"Layout"),"Generation")>savedGeneration,"saved popup view is composed before reading its current Close button");Console.WriteLine("BIND popup-consumed command="+boundCommand+" generation="+savedGeneration+"->"+Get(Get(popup,"Layout"),"Generation"));
                Console.WriteLine("BIND committed="+bindings.CompletionId+" chord="+bindings.Get("combat.aim").Text+" popup="+Get(popup,"Visible"));CloseObserved(popup,bindings);Console.WriteLine("BIND close visible="+Get(popup,"Visible"));Require(!(bool)Get(popup,"Visible"),"actual close click must retire the binding popup before gameplay dispatch");
                Call(shell,"CloseAndSubmitPosition");Neutral(context,shell,settings,bindings);long accepted=settings.AcceptedCommandId;
                UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F11);for(int i=0;i<4;i++)UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F11);UiFrame(context,Vector2.Zero,false);Wait();Console.WriteLine("KEY accepted="+accepted+"->"+settings.AcceptedCommandId+" aim="+settings.Value.Aim+" path="+settings.Value.Path+" marker="+settings.Value.Marker+" visible="+Get(state,"Visible")+" input="+Get(Get(context,"Input"),"CanStartActions"));Require(!settings.Value.Aim && settings.AcceptedCommandId==accepted+1 && settings.Value.Path && settings.Value.Marker,"fresh actual binding toggles once; held samples preserve independent display preferences");
                UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F11);UiFrame(context,Vector2.Zero,false);Wait();Require(settings.Value.Aim,"second fresh edge restores Aim through the same reliable setting owner");
                UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);Nav(context,8);var field=Find("ObservationRadius");Position(field);UiFrame(context,Vector2.Zero,false);var track=Get(drag,"Track");var middle=new Vector2((float)Get(track,"X")+(float)Get(track,"Width")*.5f,(float)Get(track,"Y")+7)*Main.UIScale;accepted=settings.AcceptedCommandId;
                UiFrame(context,middle,true);Require((bool)Get(drag,"Captured"),"natural visible radius track captures physical press");UiFrame(context,middle,true,Keys.LeftControl,Keys.F11);UiFrame(context,middle,true);Require(settings.Value.Aim && settings.AcceptedCommandId==accepted,"drag ownership blocks gameplay Aim hotkey rather than folding the card through an unauthorized input path");
                UiFrame(context,middle,true,Keys.Escape);for(int i=0;i<3;i++){UiFrame(context,middle,true);Consumed(true);}
                Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted,"Escape cancels draft without an accidental save");UiFrame(context,middle,false);Consumed(false);UiFrame(context,middle,false);Require(!(bool)Get(drag,"OwnsPointer"),"only the next focused frame retires the physical release tail");
                if(!(bool)Get(state,"Visible")){UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);}Nav(context,8);Apply("ObservationAimOff");Console.WriteLine("OFF UI aim="+settings.Value.Aim+" accepted="+settings.AcceptedCommandId+" scroll="+Get(state,"Scroll")+" max="+Get(Get(state,"Layout"),"MaxScroll")+" radius="+Elements().Any(e=>Get(e,"Command").ToString()=="ObservationRadius")+" visible="+Get(state,"Visible"));Require(!Elements().Any(e=>Get(e,"Command").ToString()=="ObservationRadius") && (float)Get(state,"Scroll")<=(float)Get(Get(state,"Layout"),"MaxScroll"),"committed OFF removes card hit geometry and clamps actual scroll");
                graphics.Image(Path.Combine(output,"aim-off-folded.png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,960,440);
                Call(shell,"CloseAndSubmitPosition");UiFrame(context,Vector2.Zero,false);IndependentWorld(context,graphics,host);
                Console.WriteLine("PASS Aim real ON/OFF card, recording/worker binding/fresh dispatch, drag hotkey exclusion and held/release input tail; OFF keeps actual path and target marker.");
            }
            finally{deferPopupPoll=false;popupPollDepth=0;audit.UnpatchAll(audit.Id);traceHost=traceShell=tracePopup=null;traceBindings=null;Call(shell,"CloseAndSubmitPosition");NativeCombatObservationChecks.Save(host,new ObservationOptions());}

            object[] Elements(){return ((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().ToArray();}
            void CloseObserved(object popup,HotkeyBindings bindings)
            {
                var layout=Get(popup,"Layout");var commands=((IEnumerable)Get(layout,"Commands")).Cast<object>().ToArray();var buttons=((IEnumerable)Get(layout,"Buttons")).Cast<object>().ToArray();int index=Array.FindIndex(commands,c=>c.ToString()=="Close");Require(index>=0,"popup close exists");var panel=Get(layout,"Panel");var point=(Point(Get(buttons[index],"Rect"))+new Vector2((float)Get(panel,"X"),(float)Get(panel,"Y")))*Main.UIScale;
                var snapshots=new System.Collections.Generic.List<string>(4);Snapshot("before");UiFrame(context,point,false);Snapshot("neutral");UiFrame(context,point,true);Snapshot("press");UiFrame(context,point,false);Snapshot("release");foreach(string snapshot in snapshots)Console.WriteLine(snapshot);
                void Snapshot(string phase)
                {var input=Get(context,"Input");var keys=(HotkeyInput)Get(input,"Hotkeys");var nowPanel=Get(layout,"Panel");var nowCommands=((IEnumerable)Get(layout,"Commands")).Cast<object>().ToArray();var nowButtons=((IEnumerable)Get(layout,"Buttons")).Cast<object>().ToArray();var nowPoint=(Point(Get(nowButtons[Array.FindIndex(nowCommands,c=>c.ToString()=="Close")],"Rect"))+new Vector2((float)Get(nowPanel,"X"),(float)Get(nowPanel,"Y")))*Main.UIScale;snapshots.Add("BIND CLOSE "+phase+" point="+point+" currentClose="+nowPoint+" generation="+Get(layout,"Generation")+" panel="+Get(nowPanel,"X")+","+Get(nowPanel,"Y")+","+Get(nowPanel,"Width")+","+Get(nowPanel,"Height")+" hit="+Call(layout,"Hit",point.X/Main.UIScale,point.Y/Main.UIScale,(int)Get(popup,"DetailOffset"))+" hovered="+Get(popup,"Hovered")+" armed="+Get(popup,"Pressed")+" armedGeneration="+Get(popup,"armedGeneration")+" feedback="+Get(Get(popup,"Feedback"),"Kind")+" pending="+Get(popup,"pendingCommand")+" completion="+bindings.CompletionId+" busy="+bindings.Busy+" focused="+Get(input,"SampleFocused")+" reliable="+keys.Reliable+" down="+keys.IsDown(256)+" new="+keys.IsNew(256)+" suppressed="+keys.IsSuppressed(256)+" tail="+Get(input,"HotkeyPointerOwned"));}
            }
            object Find(string command){return Elements().Single(e=>Get(e,"Command").ToString()==command);}
            void Wait(){long command=settings.AcceptedCommandId;NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready && settings.CompletedCommandId==command;});Require(settings.CompletionSucceeded,"this accepted preference command actually completes successfully");}
            void Apply(string command){long prior=settings.AcceptedCommandId;var at=Position(Find(command));Click(context,at);Wait();Console.WriteLine("UI command="+command+" accepted="+prior+"->"+settings.AcceptedCommandId+" aim="+settings.Value.Aim+" pointer="+Get(drag,"OwnsPointer")+" at="+at);Require(settings.AcceptedCommandId==prior+1,"actual button accepts its own new reliable command: "+command);}
            Vector2 Position(object element)
            {
                var rect=Get(element,"Rect");float y=(float)Get(rect,"Y"),h=(float)Get(rect,"Height");
                for(int i=0;i<80;i++){var view=Get(Get(state,"Layout"),"Viewport");float scroll=(float)Get(state,"Scroll");if(y>=scroll && y+h<=scroll+(float)Get(view,"Height"))return (Point(rect)+new Vector2((float)Get(state,"X")+(float)Get(view,"X"),(float)Get(state,"Y")+(float)Get(view,"Y")-scroll))*Main.UIScale;UiFrame(context,(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale,false,new Keys[0],y<scroll?120:-120);}
                throw new InvalidOperationException("real wheel must reveal "+Get(element,"Command"));
            }
            void Consumed(bool physical)
            {Require(PlayerInput.MouseInfo.LeftButton==(physical?ButtonState.Pressed:ButtonState.Released) && !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.JustPressed.MouseLeft && !PlayerInput.Triggers.JustReleased.MouseLeft && !Main.mouseLeft,"drag cancellation consumes mapped held/release samples while preserving physical MouseInfo");}
        }
        private static void IndependentWorld(object context,ProbeGraphics graphics,object host)
        {
            foreach(var other in Main.npc)other.active=false;Main.LocalPlayer.position=new Vector2(640,640);Main.LocalPlayer.itemTime=Main.LocalPlayer.itemAnimation=0;Main.LocalPlayer.inventory[0].SetDefaults(1);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.position=new Vector2(720,650);n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;
            Main.screenWidth=960;Main.screenHeight=640;PlayerInput.CacheOriginalScreenDimensions();Main.screenPosition=new Vector2(300,300);Main.GameViewMatrix.Zoom=Vector2.One;Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));graphics.LoadTexture("LockOnCursor","Images/UI/LockOn_Cursor");
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center,false);Call(host,"SampleMouse");NativeCombatObservationChecks.Fresh(context,host);var world=Get(host,"World");Call(world,"Prepare");var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);var options=((ObservationSettings)Get(host,"Settings")).Value;
            Console.WriteLine("OFF WORLD selected="+Get(Get(host,"Selection"),"HasTarget")+" path="+Get(world,"StrokeCount")+" marker="+Get(Get(world,"Marker"),"Visible")+" pixels="+pixels.Count(c=>c.A>0));
            Require(!options.Aim && options.Path && options.Marker && (bool)Get(Get(world,"Marker"),"Visible") && (int)Get(world,"StrokeCount")>0 && pixels.Any(c=>c.A>0),"actual OFF button retains healthy independent world path and atlas marker draw");
        }
    }
}
