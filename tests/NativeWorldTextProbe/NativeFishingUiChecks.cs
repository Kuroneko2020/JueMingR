using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Information;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingUiChecks
    {
        internal static void Run(object context)
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State"),ui=Get(shell,"FishingUi"),host=Get(context,"Fishing"),renderer=Get(shell,"renderer");
            LongText(ui.GetType().Assembly);
            var settings=(FishingSettings)Get(host,"Settings");Save(host,new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0));
            var registry=(JueMingR.Platform.Hotkeys.HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");Require(registry.Find("fishing.filter.toggle")==null,"filter mode is deliberately excluded from the public binding registry");
            FiniteCostChecks.SetCpuFont(10);Call(renderer,"RefreshResources");
            var text=Get(ui,"TextInput");var field=text.GetType().GetField("ime",BindingFlags.Instance|BindingFlags.NonPublic);var ime=new Ime(field.FieldType);field.SetValue(text,ime.GetTransparentProxy());
            Call(state,"Navigate",7);Call(state,"RestoreVisible");Prepare(context);
            NativeFishingExperienceChecks.RunUi(context);
            BalancedLayout(context,host,ui);
            long layouts=(long)Get(ui,"LayoutBuilds"),rules=(long)Get(Get(host,"Catalog"),"RuleChecks"),tiles=(long)Get(Get(host,"Catalog"),"TileReads");
            for(int i=0;i<30;i++)Prepare(context);
            Require((long)Get(ui,"LayoutBuilds")==layouts && (long)Get(Get(host,"Catalog"),"RuleChecks")==rules && (long)Get(Get(host,"Catalog"),"TileReads")==tiles,"stable CPU layout preparations do not rebuild or query fish/water");
            Require(!((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().Any(),"actual fishing page retires the static pressure layout");
            var hotkey=Part(ui,"Hotkey");var hr=Get(Get(hotkey,"Element"),"Rect");var hintArgs=new object[]{(float)Get(hr,"X")+2,(float)Get(hr,"Y")+2,null,null};
            Require((string)Call(ui,"Hint",hintArgs)=="双击设置快捷键","background row panels do not swallow keyboard hints");
            Click(context,Part(ui,"Plus"));Require((bool)Get(ui,"Visible") && GetOptional(ui,"Editor")!=null,"plus enters real search draft and candidate overlay");
            Require(((FishKey[])Get(ui,"candidates")).Length==0,"blank search opens without global expansion");
            Step(context,false,new Vector2(0,0),"#2290");Prepare(context);
            Click(context,Part(ui,"Select"));Require(((IEnumerable)Get(ui,"selected")).Cast<object>().Count()==1,"physical candidate click toggles selection");
            Click(context,Part(ui,"Add"));Require(settings.Busy && (bool)Get(ui,"Visible") && ((IEnumerable)Get(ui,"selected")).Cast<object>().Count()==1,"accepted save retains selected candidates until reliable completion");
            Drain(host);Prepare(context);
            Require(!(bool)Get(ui,"Visible") && settings.Value.List(1,0).Contains(new FishKey(FishKind.Item,2290)),"matching successful receipt commits exact fish and closes overlay");
            Click(context,Part(ui,"Save"));Drain(host);Prepare(context);Require(settings.Value.Presets.Count==1,"actual auto-name preset save");
            Click(context,Part(ui,"Clear"));Drain(host);Prepare(context);Require(settings.Value.List(1,0).Exact.Count==0,"clear affects current exact list");
            Click(context,Part(ui,"PresetList"));Click(context,Part(ui,"ApplyPreset"));Drain(host);Prepare(context);
            Require(settings.Value.List(1,0).Exact.Count==1 && !(bool)Get(ui,"Visible"),"preset overlay replaces scoped list and closes after save");
            Click(context,Part(ui,"PresetList"));var preset=Part(ui,"ApplyPreset");var pr=Get(Get(preset,"Element"),"Rect");var pp=new Vector2((float)Get(pr,"X")+4,(float)Get(pr,"Y")+4);Step(context,false,pp);
            var stateInput=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            foreach(var entry in new[]{Tuple.Create("Width",(object)960f),Tuple.Create("Height",(object)760f),Tuple.Create("Scale",(object)1f),Tuple.Create("X",(object)pp.X),Tuple.Create("Y",(object)pp.Y),Tuple.Create("Active",(object)true),Tuple.Create("Focused",(object)true),Tuple.Create("BlockPointer",(object)true)})stateInput.GetType().GetField(entry.Item1,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(stateInput,entry.Item2);
            Call(state,"Update",stateInput);var popupHint=new object[]{state,null,false,false,null,null};
            Require(!((bool)Get(state,"CanShowHint")) && Call(renderer,"ResolveHint",popupHint)==null,"preset contents are visible inline without a hover reminder");
            Click(context,Part(ui,"Close"));
            Click(context,Part(ui,"Match",p=>(int)Get(p,"Value")==1));Drain(host);Prepare(context);Click(context,Part(ui,"Plus"));
            Click(context,Part(ui,"Confirm"));Prepare(context);
            Require((bool)Get(ui,"Visible") && ((IEnumerable)Get(ui,"popupParts")).Cast<object>().Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="请输入关键词。"),"empty keyword confirmation retains its draft and shows validation inside the popup");
            Step(context,false,new Vector2(0,0),"鱼");Prepare(context);Click(context,Part(ui,"Confirm"));Drain(host);Prepare(context);
            Require(settings.Value.List(1,1).Keywords.SequenceEqual(new[]{"鱼"}) && settings.Value.List(1,0).Exact.Count==1,"Chinese committed character enters independent keyword scope");
            Click(context,Part(ui,"Plus"));Step(context,false,new Vector2(0,0),"取消草稿");Prepare(context);Click(context,Part(ui,"Close"));
            Require(settings.Value.List(1,1).Keywords.Count==1 && GetOptional(ui,"Editor")==null && !(bool)Get(ui,"OwnsTextToken"),"cancel closes uncommitted Chinese draft and its native text lease");
            Click(context,Part(ui,"Plus"));Step(context,false,Vector2.Zero,"草稿");var editor=Get(ui,"Editor");ime.Composition="候选";
            Step(context,false,Vector2.Zero,"\u001b",new KeyboardState(Keys.Escape));Require((bool)Get(ui,"Visible") && ReferenceEquals(GetOptional(ui,"Editor"),editor),"IME Escape cancels composition without discarding the fishing draft");
            ime.Composition="";Step(context,false,Vector2.Zero);Step(context,false,Vector2.Zero,"\u001b",new KeyboardState(Keys.Escape));
            Require(!(bool)Get(ui,"Visible") && GetOptional(ui,"Editor")==null,"later explicit Escape closes the uncommitted keyword overlay");
            Prepare(context);Click(context,Part(ui,"Plus"));Step(context,false,Vector2.Zero,"旧会话草稿");
            ui.GetType().GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,(long)Get(ui,"session")-1);Prepare(context);
            Require(!(bool)Get(ui,"Visible") && GetOptional(ui,"Editor")==null,"Prepare retires old-session drafts before recording the new generation");
            Call(state,"Close");Call(ui,"Suspend");Save(host,new FishingOptions());
            Console.WriteLine("PASS G10 physical list/search selection, save receipt, clear/preset replacement, Chinese keyword scope and cancellation; CPU layout only.");
        }
        internal static void Save(object host,FishingOptions options)
        {var s=(FishingSettings)Get(host,"Settings");Drain(host);Require(s.Set(options),"UI fixture settings admitted");Drain(host);}
        internal static void FullBindings(object context)
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State"),ui=Get(shell,"FishingUi"),host=Get(context,"Fishing"),information=Get(context,"Information"),popup=Get(shell,"HotkeyPopup");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var ids=new[]{"fishing.auto-fish.toggle","fishing.auto-loadout.toggle","fishing.auto-equipment.toggle","fishing.auto-store.toggle","fishing.cut-rod.toggle","information.full-fish.toggle","information.filtered-fish.toggle"};
            Main.screenWidth=960;Main.screenHeight=760;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();Call(state,"RestoreVisible");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);Save(host,new FishingOptions());
            for(int i=0;i<ids.Length;i++)
            {
                NativeToolsUiChecks.Nav(context,i<5?7:9);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);Vector2 point;
                if(i<5)point=NativeToolsUiChecks.Point(Get(Get(Part(ui,"Hotkey",p=>(int)Get(p,"Feature")==i),"Element"),"Rect"));
                else
                {
                    var layout=Get(state,"Layout");var viewport=Get(layout,"Viewport");var inside=NativeToolsUiChecks.Point(viewport)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y"));
                    for(int n=0;n<30 && (float)Get(state,"Scroll")<(float)Get(layout,"MaxScroll");n++)NativeToolsUiChecks.UiFrame(context,inside,false,new Keys[0],-120);
                    var icon=((IEnumerable)Get(layout,"Elements")).Cast<object>().Single(e=>Get(e,"Kind").ToString()=="Hotkey" && (string)GetOptional(e,"HotkeyTarget")==ids[i]);
                    point=NativeToolsUiChecks.Point(Get(icon,"Rect"))+new Vector2((float)Get(state,"X")+(float)Get(viewport,"X"),(float)Get(state,"Y")+(float)Get(viewport,"Y")-(float)Get(state,"Scroll"));
                }
                NativeToolsUiChecks.Click(context,point);NativeToolsUiChecks.Click(context,point);
                Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")==ids[i],"actual keyboard-icon double click routes to "+ids[i]);
                NativeToolsUiChecks.PopupClick(context,popup,"Record");Require((bool)Get(popup,"Capturing"),"shared recorder starts through its real button");
                var key=(Keys)((int)Keys.F13+i);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.LeftControl);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.LeftControl,key);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                NativeQuickItemChecks.Until(()=>{NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                Require(bindings.CompletionSucceeded && bindings.CompletionAction==ids[i] && bindings.Get(ids[i])?.MainKey==(int)key,"actual recorded chord persists for "+ids[i]);
                NativeToolsUiChecks.PopupClick(context,popup,"Close");NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
            }
            Call(shell,"CloseAndSubmitPosition");NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
            for(int i=0;i<ids.Length;i++)foreach(bool enabled in new[]{true,false})
            {
                var key=(Keys)((int)Keys.F13+i);for(int held=0;held<3;held++)NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.LeftControl,key);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                if(i<5){Drain(host);Require(((FishingSettings)Get(host,"Settings")).Value.State(i)==(enabled?1:0),"real shell dispatch changes once per press: "+ids[i]);}
                else
                {
                    NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return Get(Get(information,"Preferences"),"Status").ToString()=="Saved";});
                    Require((bool)Call(information,"Enabled",(InformationKind)(i-1))==enabled && !(bool)Call(information,"Enabled",i==5?InformationKind.FilteredFish:InformationKind.FullFish),"real information key changes only its own fish display");
                }
            }
            Console.WriteLine("PASS G10 seven actual keyboard buttons -> double click -> shared recorder -> reliable save -> physical shell dispatch, held-key single toggle and independent fish displays.");
        }
        private static void BalancedLayout(object context,object host,object ui)
        {
            Save(host,new FishingOptions(filterMode:2).WithList(2,0,new FishList(new[]{2290,2297,2334,2335,2336,2337}.Select(id=>new FishKey(FishKind.Item,id)))));Prepare(context);
            var parts=((IEnumerable)Get(ui,"pageParts")).Cast<object>().ToArray();
            var match=parts.Single(p=>Get(p,"Command").ToString()=="Match");
            var title=parts.Single(p=>(string)GetOptional(Get(p,"Element"),"Text")=="当前过滤模式：" && Get(Get(p,"Element"),"Kind").ToString()=="Text");
            var mr=Get(Get(match,"Element"),"Rect");var tr=Get(Get(title,"Element"),"Rect");
            var filter=parts.Single(p=>Get(p,"Command").ToString()=="Filter");var fr=Get(Get(filter,"Element"),"Rect");
            Require((float)Get(tr,"Y")>=(float)Get(mr,"Y") && (float)Get(tr,"Y")<(float)Get(mr,"Bottom") && (float)Get(fr,"Y")== (float)Get(mr,"Y") && (float)Get(tr,"Right")<(float)Get(fr,"X") && (float)Get(fr,"Right")<(float)Get(mr,"X"),"current-mode heading, filter and match toggle share one nonoverlapping row in that order");
            Require(!parts.Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="过滤模式"),"right column has no duplicate filter-mode heading");
            var special=parts.Where(p=>Get(p,"Command").ToString()=="Special").Select(p=>Get(Get(p,"Element"),"Rect")).ToArray();
            Require(special.Length==3 && special.All(r=>(float)Get(r,"Width")== (float)Get(special[0],"Width")),"three special rule buttons have equal widths");
            Require(!parts.Any(p=>Get(p,"Command").ToString()=="Hotkey" && (int)Get(p,"Feature")==5),"filter mode has no keyboard binding affordance");
            var toolbar=parts.Where(p=>new[]{"Current","Plus","Clear","Save","PresetList"}.Contains(Get(p,"Command").ToString())).Select(p=>Get(Get(p,"Element"),"Rect")).ToArray();
            var list=Get(ui,"listLocal");Require(Math.Abs((float)Get(toolbar.Last(),"Right")-(float)Get(list,"Right"))<.1f,"toolbar uses the available right edge without trailing empty space");
            var divider=Get(Get(parts.Single(p=>Get(Get(p,"Element"),"Kind").ToString()=="Divider"),"Element"),"Rect");
            Require(toolbar.Max(r=>(float)Get(r,"Bottom"))<(float)Get(divider,"Y") && (float)Get(divider,"Bottom")<(float)Get(list,"Y"),"visible divider and breathing room separate controls from list entries");
            var rulesPanel=Get(Get(parts.Last(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel"),"Element"),"Rect");
            float padding=(float)Get(rulesPanel,"Bottom")-special.Max(r=>(float)Get(r,"Bottom"));
            Require(padding>=8 && padding<=20,"special rules frame follows its own three buttons instead of stretching to list height");
            var cards=((IEnumerable)Get(ui,"listParts")).Cast<object>().Where(p=>GetOptional(p,"Fish")!=null && Get(p,"Command").ToString()=="None").Select(p=>Get(Get(p,"Element"),"Rect")).ToArray();
            Require(cards.Count(r=>(float)Get(r,"Y")==0)==3,"normal-width exact fish list fits three cards on each row");
            Click(context,Part(ui,"Match"));Drain(host);Prepare(context);Require(((FishingSettings)Get(host,"Settings")).Value.Match==1,"single match button switches to keyword mode");
            Click(context,Part(ui,"Match"));Drain(host);Prepare(context);Require(((FishingSettings)Get(host,"Settings")).Value.Match==0,"same match button switches back to exact mode");
            foreach(int expected in new[]{0,1,2}){Click(context,Part(ui,"Filter"));Drain(host);Prepare(context);Require(((FishingSettings)Get(host,"Settings")).Value.FilterMode==expected,"relocated filter button keeps its actual three-mode cycle");}
            var custom=new FishPreset(2,0,new FishList(new[]{new FishKey(FishKind.Item,2290)}),"builtin-trash");
            Save(host,new FishingOptions(filterMode:2,presets:new[]{custom}.Concat(Enumerable.Range(0,40).Select(i=>new FishPreset(2,0,new FishList(new[]{new FishKey(FishKind.Item,2290)}),"custom-"+i)))));Prepare(context);
            Click(context,Part(ui,"PresetList"));Require(((IEnumerable)Get(ui,"Parts")).Cast<object>().Any(p=>Get(p,"Command").ToString()=="DeletePreset" && (string)GetOptional(p,"Name")=="builtin-trash"),"a user preset with the built-in name retains its own identity and delete action");
            long scans=(long)Get(ui,"PresetScans");var body=Get(ui,"popupBody");var point=new Vector2((float)Get(body,"X")+4,(float)Get(body,"Y")+4);
            foreach(int wheel in new[]{-240,240}){Call(ui,"ProcessPopup",true,default(KeyboardState),point,true,true,wheel);Prepare(context);}
            long descriptions=(long)Get(ui,"PresetDescriptions");
            foreach(int wheel in new[]{-240,240}){Call(ui,"ProcessPopup",true,default(KeyboardState),point,true,true,wheel);Prepare(context);}
            Require((long)Get(ui,"PresetScans")==scans && (long)Get(ui,"PresetDescriptions")==descriptions,"revisiting preset rows reuses scoped filtering and already measured content descriptions");
            Call(ui,"CloseOverlay");
            Save(host,new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0));Prepare(context);
        }
        internal static void Drain(object host){var s=(FishingSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return s.Loaded && !s.Busy;});Require(s.Message==null,"UI fixture real storage result");}
        internal static void Prepare(object context,float width=960,float height=760,float scale=1)
        {var shell=Get(context,"Shell");var state=Get(shell,"State");Call(Get(shell,"renderer"),"Prepare",state,width,height,scale);Call(Get(shell,"FishingUi"),"PrepareLayout",Matrix.CreateScale(scale),new Vector2(width,height));}
        private static void LongText(Assembly assembly)
        {
            var type=assembly.GetType("JueMingR.TerrariaHost.Input.SingleLineEditView");var view=Activator.CreateInstance(type,true);var method=type.GetMethod("Prepare",BindingFlags.Instance|BindingFlags.NonPublic);var function=method.GetParameters()[3].ParameterType;var size=function.GetGenericArguments()[2];int calls=0;
            Func<string,float,object> count=(text,scale)=>{calls++;return Activator.CreateInstance(size,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{(float)text.Length,10f,0f,0f},null);};
            var value=Expression.Parameter(typeof(string));var scaleArg=Expression.Parameter(typeof(float));var measure=Expression.Lambda(function,Expression.Convert(Expression.Invoke(Expression.Constant(count),value,scaleArg),size),value,scaleArg).Compile();
            var editor=new JueMingR.Features.Text.TextEditBuffer(new string('a',12000)+"鱼",true,16384,65536,"too long");editor.MoveTo(editor.Text.Length,false);
            method.Invoke(view,new object[]{editor,"",80f,measure});Require(calls<50 && (string)Get(view,"Text")==new string('a',79)+"鱼" && (float)Get(view,"Caret")==80,"long single-line input uses bounded measurements while retaining exact final caret and text");
        }
        internal static object Part(object ui,string command,Func<object,bool> extra=null)
        {return ((IEnumerable)Get(ui,"Parts")).Cast<object>().First(p=>Get(p,"Command").ToString()==command && (extra==null || extra(p)));}
        internal static void Click(object context,object part)
        {var r=Get(Get(part,"Element"),"Rect");var p=new Vector2((float)Get(r,"X")+(float)Get(r,"Width")/2,(float)Get(r,"Y")+(float)Get(r,"Height")/2);Step(context,false,p);Step(context,true,p);Step(context,false,p);Prepare(context);}
        internal static void Step(object context,bool held,Vector2 point,string characters="",KeyboardState keys=default(KeyboardState))
        {
            var input=Get(context,"Input");var ui=Get(Get(context,"Shell"),"FishingUi");
            PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,held?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Main.keyState=keys;
            Call(input,"BeginUpdate");Call(ui,"BeforeInput",true);Call(input,"AfterMapping");PlayerInput.WritingText=false;Call(input,"AfterKeyboardRefresh");
            Main.keyCount=characters.Length;for(int i=0;i<characters.Length;i++){Main.keyInt[i]=characters[i];Main.keyString[i]=characters[i].ToString();}
            Call(ui,"ProcessPopup",true,keys,point,true,true,0);
            Call(ui,"ProcessInput",true,keys,point,true,true,Get(ui,"BlockPointer"),0);
        }
        // Only the operating system IME service is replaced. The production
        // native character queue, text owner, commands and writer are exercised.
        private sealed class Ime:RealProxy
        {
            internal string Composition="";
            internal Ime(Type type):base(type){}
            public override IMessage Invoke(IMessage message)
            {var call=(IMethodCallMessage)message;object value=call.MethodName=="get_Composition"?(object)Composition:call.MethodName=="get_Candidates"?(object)false:null;return new ReturnMessage(value,null,0,call.LogicalCallContext,call);}
        }
    }
}
