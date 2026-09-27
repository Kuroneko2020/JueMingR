using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeFishingUiChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingExperienceChecks
    {
        internal static string FeedbackText(object context,string id)
        {
            var entries=((IEnumerable)Get(Get(context,"ShortFeedback"),"entries")).Cast<object>();
            var entry=entries.FirstOrDefault(e=>(string)Get(e,"Id")==id);return entry==null?null:(string)Get(entry,"Text");
        }
        // The real shell input/runtime chain without the graphics-resource
        // preparation at UpdateShell. CPU fixtures have no GPU panel textures;
        // geometry uses the same production Prepare methods as the UI checks.
        internal static void ShellFrame(object context,Vector2 point,bool left)
        {
            object shell=Get(context,"Shell"),input=Get(context,"Input");Call(input,"BeginUpdate");Call(shell,"BeforeInput");
            PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.MouseLeft=left;PlayerInput.Triggers.Update();Main.mouseLeft=left;Main.mouseX=(int)point.X;Main.mouseY=(int)point.Y;
            Call(input,"AfterMapping");Main.keyState=new KeyboardState();Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");Prepare(context);
            Require(!(bool)Get(shell,"Failed"),"actual shell input/runtime remains healthy");
        }
        internal static void RunUi(object context)
        {
            object host=Get(context,"Fishing"),ui=Get(Get(context,"Shell"),"FishingUi");
            var settings=(FishingSettings)Get(host,"Settings");
            Click(context,Part(ui,"Special"));
            Require(settings.Busy,"physical special-rule click starts the real asynchronous writer");
            Require(((IEnumerable)Get(ui,"Parts")).Cast<object>().Where(p=>Get(p,"Command").ToString()=="Feature").All(p=>(bool)Get(p,"Enabled")),
                "saving one rule must not dim every unrelated fishing feature");
            long accepted=settings.AcceptedCommandId;
            Click(context,Part(ui,"Feature"));
            Require(settings.AcceptedCommandId==accepted,"stable enabled appearance does not admit a second mutation before the receipt");
            Save(host,new FishingOptions(filterMode:0));Prepare(context);
            var commands=new[]{"Current","Plus","Clear","Save","PresetList"};
            Require(((IEnumerable)Get(ui,"Parts")).Cast<object>().Where(p=>commands.Contains(Get(p,"Command").ToString())).All(p=>!(bool)Get(p,"Enabled")),
                "filter off visibly disables every list operation");
            Require((bool)Get(Part(ui,"Match"),"Enabled") && (bool)Get(Part(ui,"Special"),"Enabled"),"off retains the independent mode and special-rule controls");
            Save(host,new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0));Prepare(context);
            PageNotice(context,host,ui);
            PopupExperience(context,host,ui);
            Console.WriteLine("PASS G10 physical save appearance stability, reliable single command admission and filter-off list affordances.");
        }
        private static object[] Parts(object ui){return ((IEnumerable)Get(ui,"Parts")).Cast<object>().ToArray();}
        private static void PageNotice(object context,object host,object ui)
        {
            Click(context,Part(ui,"Save"));Drain(host);Prepare(context);
            var parts=((IEnumerable)Get(ui,"pageParts")).Cast<object>().ToArray();
            var rules=parts.Where(p=>Get(p,"Command").ToString()=="Special").Select(p=>Get(Get(p,"Element"),"Rect")).ToArray();
            var notice=parts.Where(p=>((string)GetOptional(Get(p,"Element"),"Text")??"").Contains("当前名单")).ToArray();
            Require(notice.Length>0 && notice.All(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"X")>=(float)Get(rules[0],"X") && (float)Get(Get(Get(p,"Element"),"Rect"),"Y")>rules.Max(r=>(float)Get(r,"Bottom"))),"completed list notice appears below the right-hand special rules, never under the left list");
            // Move the monotonic deadline past without sleeping or changing the
            // business result. Stable retained errors must not restart a toast.
            string error="当前操作未完成，请稍后重试；这是一条需要在右侧完整换行显示的提示。";
            Call(host,"Report",error);Prepare(context);
            var lines=((IEnumerable)Get(ui,"pageParts")).Cast<object>().Where(p=>GetOptional(p,"Name") as string=="page-notice").ToArray();
            Require(lines.Length>1 && string.Concat(lines.Select(p=>(string)Get(Get(p,"Element"),"Text")))==error,"right-column feedback wraps without dropping text");
            ui.GetType().GetField("noticeExpires",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ui,0d);Prepare(context);
            for(int i=0;i<30;i++)Prepare(context);
            Require(!((IEnumerable)Get(ui,"pageParts")).Cast<object>().Any(p=>GetOptional(p,"Name") as string=="page-notice") && (string)Get(host,"Error")==error,"expired notice stays hidden while its diagnostic cause remains intact");
            Call(host,"ClearReport",error);Prepare(context);Click(context,Part(ui,"Save"));Drain(host);Prepare(context);
            Require(((IEnumerable)Get(ui,"pageParts")).Cast<object>().Any(p=>GetOptional(p,"Name") as string=="page-notice"),"a new completed operation can display the same confirmation again");
            Save(host,new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0));Prepare(context);
        }
        private static float PopupHeight(object ui){return (float)Get(Get(ui,"popupRect"),"Height");}
        private static void PopupExperience(object context,object host,object ui)
        {
            Click(context,Part(ui,"Plus"));float blank=PopupHeight(ui);
            Step(context,false,Vector2.Zero,"#2290");Prepare(context);float single=PopupHeight(ui);
            Require(blank<250 && single<300,"empty and one-result search use compact content-sized windows");
            var draft=Get(ui,"Editor");var main=Get(Get(context,"Shell"),"State");
            var oldPoint=new Vector2((float)Get(main,"X")+20,(float)Get(main,"Y")+20);
            PlayerInput.MouseInfo=new MouseState((int)oldPoint.X,(int)oldPoint.Y,0,ButtonState.Pressed,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            Call(ui,"ProcessPopup",true,new KeyboardState(),oldPoint,false,true,0);
            Require((bool)Get(ui,"Visible") && ReferenceEquals(draft,GetOptional(ui,"Editor")),"stale resize geometry never dismisses an uncommitted secondary draft");
            Step(context,false,Vector2.Zero);Prepare(context);
            Click(context,Part(ui,"Select"));Require(Parts(ui).Where(p=>Get(p,"Command").ToString()=="Feature").All(p=>(bool)Get(p,"Enabled")),"selection-only clicks do not dim the underlying feature controls");
            Call(ui,"CloseOverlay");Prepare(context);Click(context,Part(ui,"Plus"));Step(context,false,Vector2.Zero,"fish");Prepare(context);
            Require(((FishKey[])Get(ui,"candidates")).Length>3 && PopupHeight(ui)>single,"more actual search results grow the compact window before scrolling");
            Call(ui,"CloseOverlay");Prepare(context);
            var keys=Enumerable.Range(2290,20).Select(id=>new FishKey(FishKind.Item,id)).ToArray();
            var preset=new FishPreset(1,0,new FishList(keys),"icons");Save(host,new FishingOptions(filterMode:1,presets:new[]{preset}));Prepare(context);Click(context,Part(ui,"PresetList"));
            var icons=Parts(ui).Where(p=>(int)Get(p,"Region")==3 && GetOptional(p,"Fish")!=null).ToArray();
            Require(icons.Length==20 && icons.Select(p=>Get(Get(p,"Element"),"Rect")).Select(r=>(float)Get(r,"Y")).Distinct().Count()>1,"exact preset exposes each real icon and wraps within its own row");
            Require(Parts(ui).Where(p=>(int)Get(p,"Region")>=2).All(p=>GetOptional(p,"Hint")==null),"preset controls and contents have no hover reminders");
            Call(ui,"CloseOverlay");
            string[] words={"鱼","很长的关键词鱼获",new string('x',90),"crates","鲨"};
            Save(host,new FishingOptions(filterMode:1,match:1,presets:new[]{new FishPreset(1,1,new FishList(keywords:words),"words")}));Prepare(context);Click(context,Part(ui,"PresetList"));
            var pills=Parts(ui).Where(p=>(int)Get(p,"Region")==3 && Get(p,"Command").ToString()=="None" && GetOptional(p,"Label")!=null).ToArray();
            Require(pills.Length==words.Length && pills.Select(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Width")).Distinct().Count()>1 && pills.Select(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Y")).Distinct().Count()>1,"keyword preset shows variable-width capsules and wraps even an oversized word");
            var visibleText=Parts(ui).Where(p=>(int)Get(p,"Region")==3).SelectMany(p=>new[]{GetOptional(Get(p,"Element"),"Text") as string,GetOptional(p,"Label")==null?null:Get(Get(p,"Label"),"Text") as string}).Where(s=>!string.IsNullOrEmpty(s)).ToArray();
            Require(words.Where(w=>w.Length<90).All(w=>visibleText.Contains(w)) && visibleText.Where(s=>s.All(c=>c=='x')).Sum(s=>s.Length)==90,"every keyword remains fully readable without hints or ellipsis, including a word wider than the window");
            var close=Part(ui,"Close");Require((float)Get(Get(Get(close,"Element"),"Rect"),"Bottom")<(float)Get(Get(ui,"popupBody"),"Y"),"preset close is a separate header action above the scrollable contents");
            string cluster="a"+new string('\u0301',30);
            var clusterLines=(string[])Call(ui,"PresetWordLines",cluster,80f);var layout=Get(Get(ui,"shell"),"Layout");
            Require(string.Concat(clusterLines)==cluster && clusterLines.All(line=>(float)Get(Call(layout,"DynamicTextSize",line,.7f),"Width")<=80),"a single oversized combining sequence also preserves all text within the readable capsule width");
            Call(ui,"CloseOverlay");Save(host,new FishingOptions(filterMode:1));Prepare(context);
            Main.screenWidth=960;Main.screenHeight=760;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();ShellFrame(context,Vector2.Zero,false);
            Click(context,Part(ui,"Plus"));var feature=Part(ui,"Feature",p=>(int)Get(p,"Feature")==0 && (int)Get(p,"Value")==1);var point=NativeToolsUiChecks.Point(Get(Get(feature,"Element"),"Rect"));
            var settings=(FishingSettings)Get(host,"Settings");long accepted=settings.AcceptedCommandId;
            ShellFrame(context,point,true);Require(!(bool)Get(ui,"Visible"),"fresh main-F5 press dismisses the secondary window");ShellFrame(context,point,false);
            Require(settings.AcceptedCommandId==accepted,"dismissal release cannot activate the underlying feature");
            ShellFrame(context,point,true);ShellFrame(context,point,false);Require(settings.AcceptedCommandId>accepted,"a new subsequent main-window click executes normally");
            Save(host,new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0));Prepare(context);
            Console.WriteLine("PASS G10 compact result-driven overlays, inline wrapped presets, no preset hints and actual shell dismissal without clickthrough.");
        }
    }
}
