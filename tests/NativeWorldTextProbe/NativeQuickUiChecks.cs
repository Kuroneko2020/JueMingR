using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickUiChecks
    {
        private static List<string> nativeAlerts;
        private static bool CaptureAlert(string newText){nativeAlerts.Add(newText);return false;}
        internal static string[] ShellFeedback(object shell)
        {
            var audit=new Harmony("JueMingR.Tests.ItemsLocalFeedback");
            var method=typeof(Main).GetMethod("NewText",new[]{typeof(string),typeof(byte),typeof(byte),typeof(byte)});
            nativeAlerts=new List<string>();
            audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeQuickUiChecks).GetMethod(nameof(CaptureAlert),BindingFlags.Static|BindingFlags.NonPublic)));
            try{Call(shell,"AfterUpdate");return nativeAlerts.ToArray();}
            finally{audit.Unpatch(method,HarmonyPatchType.All,audit.Id);nativeAlerts=null;}
        }
        internal static void Run(object context,ProbeGraphics graphics=null,string output=null)
        {
            if(graphics!=null)Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            object quick=Get(context,"QuickItems"),shell=Get(context,"Shell"),state=Get(shell,"State"),page=Get(shell,"items"),panel=Get(page,"QuickPanel"),renderer=Get(shell,"renderer");
            var settings=(QuickItemSettings)Get(quick,"Settings");string reason;
            var entries=Enumerable.Range(1,96).Select(i=>new QuickItemEntry(i.ToString("x32"),new[]{50,3199,5358,5360,5361,5359,5453,5329}[i%8],QuickItemMode.Use,true,true)).ToArray();
            Require(settings.TryChange(new QuickItemDocument(false,false,entries),null,out reason),"UI isolated long-list save");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            Set(quick,"Message",null);Set(state,"Ready",true);Call(state,"Navigate",0);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Main.LocalPlayer.inventory[0].SetDefaults(50);Main.LocalPlayer.inventory[1].SetDefaults(5358);
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");long bindingCommand;HotkeyChord binding;
            Require(HotkeyChord.TryParse("LeftControl+LeftShift+LeftAlt+NumPad9",out binding,out reason),"long public chord parsed");
            if(!binding.Equals(bindings.Get(entries[0].ActionId)))
                Require(bindings.TrySet(entries[0].ActionId,binding,null,out bindingCommand,out reason),"real compact row long binding saved: "+reason);
            NativeQuickUseMatrix.Until(()=>{bindings.Poll();return !bindings.Busy;});
            Action<float,float,float> prepare=(width,height,scale)=>{Call(renderer,"Prepare",state,width,height,scale);Call(page,"PrepareLayout",Matrix.CreateScale(scale),new Vector2(width,height));};
            prepare(960,760,1);
            EmptyPresentation(quick,page,panel,prepare);
            GroupGeometry(page,panel);
            var header=((IEnumerable)Get(panel,"logical")).Cast<object>().First(part=>Get(Get(part,"Element"),"Kind").ToString()=="Panel");
            float preceding=0;
            Require((float)Get(Get(Get(header,"Element"),"Rect"),"Y")==preceding,
                "quick section starts at the top of Items with no empty migrated blocks");
            Require(!((IEnumerable)Get(panel,"logical")).Cast<object>().Any(part=>(string)GetOptional(Get(part,"Element"),"Text")=="已保存"),"successful save leaves no persistent redundant status row");
            ModePresentation(quick,shell,page,panel,state,prepare);
            SavePresentation(quick,shell,page,panel,state,prepare);
            object favorite=Get(context,"KeepFavorited");
            Require(settings.TryChange(settings.Current.Toggles(true,false),null,out reason),"visible capability fixture enabled preference");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            Call(favorite,"FailClosed");Require((bool)Get(panel,"NeedsBuild"),"live capability failure invalidates existing panel");prepare(960,760,1);
            var favoriteOn=((IEnumerable)Get(panel,"logical")).Cast<object>().First(part=>Get(part,"Command").ToString()=="FavoriteOn");
            Require(!(bool)Get(favoriteOn,"Enabled") && !(bool)Get(favoriteOn,"Selected") && ((string)Get(quick,"FavoriteHint")).Contains("暂不可用"),"failed capability projects unavailable with a readable name hint, not a selected effective on state");
            var alerts=new List<string>();Call(quick,"TakeFeedback",(Action<string>)alerts.Add);int notices=alerts.Count;
            for(int i=0;i<20;i++)Call(quick,"TakeFeedback",(Action<string>)alerts.Add);
            Require(notices>0 && alerts.Count==notices,"capability failure alerts once without a row");
            Require((bool)Get(quick,"ControlsEnabled"),"favorite failure leaves quick capability independent");Set(favorite,"failed",false);prepare(960,760,1);
            Set(settings,"Protected",true);prepare(960,760,1);Call(favorite,"FailClosed");
            Require((bool)Get(panel,"NeedsBuild"),"capability failure also invalidates an already write-protected panel");prepare(960,760,1);
            favoriteOn=((IEnumerable)Get(panel,"logical")).Cast<object>().First(part=>Get(part,"Command").ToString()=="FavoriteOn");
            Require(!(bool)Get(favoriteOn,"Selected"),"protected settings cannot mask failed capability state");Set(settings,"Protected",false);Set(favorite,"failed",false);prepare(960,760,1);
            Reveal(panel,state,"Add");prepare(960,760,1);
            float closedQuickHeight=(float)Get(Get(Get(((IList)Get(panel,"logical"))[0],"Element"),"Rect"),"Height");
            Click(page,"Quick","添加");prepare(960,760,1);
            Require((bool)Get(panel,"Editing"),"physical pointer opens inventory icon selector");
            GroupGeometry(page,panel);
            Reveal(panel,state,"Cancel");prepare(960,760,1);Click(page,"Quick","×");prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing") && settings.Current.Entries.Count==96,"cancel selector preserves durable entries");
            GroupGeometry(page,panel);
            Require((float)Get(Get(Get(((IList)Get(panel,"logical"))[0],"Element"),"Rect"),"Height")==closedQuickHeight,"cancel restores the closed quick-group height");
            Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
            Require(((IEnumerable)Get(panel,"candidates")).Cast<int>().Contains(5359),"inventory phone expands real legal target states");
            Reveal(panel,state,"Pick");prepare(960,760,1);var pick=Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Pick");
            ClickControl(page,pick);NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing") && settings.Current.Entries.Count==97,"inventory single click immediately saves chosen item");
            Reveal(panel,state,"Edit");prepare(960,760,1);ClickControl(page,Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Edit"));prepare(960,760,1);
            Call(page,"ProcessInput",true,new KeyboardState(Keys.Escape),Vector2.Zero,true,true,false);prepare(960,760,1);
            Require((bool)Get(panel,"Editing"),"raw Escape sample cannot independently cancel a Quick editor");
            Call(page,"CancelSelection");prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing"),"Shell-authorized cancellation closes inventory selector without mutation");
            // Shared key button uses the one real popup callback/target.
            Reveal(panel,state,"Edit");prepare(960,760,1);
            var entryKey=Controls(page).First(c=>Get(c,"Command").ToString()=="QuickHotkey" && (string)Get(Get(c,"Element"),"HotkeyTarget")==entries[0].ActionId);
            ClickControl(page,entryKey);ClickControl(page,entryKey);
            Require((bool)Get(Get(shell,"HotkeyPopup"),"Visible"),"dynamic row reaches common binding popup");Call(Get(shell,"HotkeyPopup"),"Close");
            long reads=(long)Get(panel,"PickerReads");int layouts=(int)Get(page,"LayoutBuildCount");
            for(int i=0;i<240;i++){Call(quick,"Update",0UL);prepare(960,760,1);}
            Require((long)Get(panel,"PickerReads")==reads && (int)Get(page,"LayoutBuildCount")==layouts,"long stable visible list does no repeated inventory/layout work");
            var shown=((IEnumerable)Get(panel,"visible")).Cast<object>().ToArray();object view=Get(page,"view");
            Require(shown.Length<((IList)Get(panel,"logical")).Count/2 && shown.All(part=>
                (float)Get(Get(Get(part,"Element"),"Rect"),"Bottom")>(float)Get(view,"Y") &&
                (float)Get(Get(Get(part,"Element"),"Rect"),"Y")<(float)Get(view,"Bottom")),"96 entries project only rectangles intersecting the viewport");
            foreach(var shape in new[]{new[]{960f,440f,1f},new[]{1280f,720f,1.5f}})
            {
                prepare(shape[0],shape[1],shape[2]);Call(state,"ScrollTo",100f);prepare(shape[0],shape[1],shape[2]);GroupGeometry(page,panel);
                var visibleGroup=((IEnumerable)Get(panel,"visible")).Cast<object>().First(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel");
                Require((float)Get(Get(Get(visibleGroup,"Element"),"Rect"),"Y")<(float)Get(Get(page,"view"),"Y"),"short/scaled view retains the body after header scrolls above clip");
            }
            prepare(960,760,1);
            if(graphics!=null)
            {
                Directory.CreateDirectory(output);
                File.WriteAllLines(Path.Combine(output,"state-names.tsv"),new[]{2611,5526,4131,5325,4346,5391,4767,5453,5059,5060,5309,5454,5323,5455,5324,5329,5330,5358,5360,5361,5359,5437,6168,6169,6193,6194,6190,6195}.Select(type=>type+"\t"+Lang.GetItemNameValue(type)));
                Action<int,int,float,string> draw=(width,height,scale,name)=>
                {
                    Call(renderer,"RefreshResources");prepare(width,height,scale);
                    graphics.LoadItemTextures(((IEnumerable)Get(panel,"visibleTypes")).Cast<int>());
                    graphics.LoadItemTextures(((IEnumerable)Get(page,"controls")).Cast<object>().Where(c=>Get(c,"Command").ToString()=="Replace" || Get(c,"Command").ToString()=="Select").Select(c=>(int)Get(c,"Type")));
                    Call(page,"Prepare",true,Matrix.CreateScale(scale),new Vector2(width,height));
                    graphics.Image(Path.Combine(output,name),()=>{Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"));},Matrix.CreateScale(scale),width,height);
                };
                Reveal(panel,state,"FavoriteOn");draw(960,760,1,"quick-items-controls.png");
                Reveal(panel,state,"Edit");draw(1280,720,1.5f,"quick-items-long-list-150.png");
                Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
                Reveal(panel,state,"Cancel");draw(960,760,1,"quick-items-chooser.png");draw(960,440,1,"quick-items-small-viewport.png");
            }
            Call(page,"Suspend");Call(state,"Close");
            int closedLayouts=(int)Get(page,"LayoutBuildCount");long closedIcons=(long)Get(panel,"IconLoads"),closedReads=(long)Get(panel,"PickerReads");
            int closedHints=(int)Get(Get(renderer,"HintLayout"),"BuildCount");
            for(int i=0;i<240;i++){Call(page,"Prepare",true,Matrix.Identity,new Vector2(960,760));Call(renderer,"DrawHints",state,Matrix.Identity,page,false,false);}
            Require((int)Get(page,"LayoutBuildCount")==closedLayouts && (long)Get(panel,"IconLoads")==closedIcons && (long)Get(panel,"PickerReads")==closedReads,
                "closed Items page performs no layout, picker or icon preparation");
            Require((int)Get(Get(renderer,"HintLayout"),"BuildCount")==closedHints && !(bool)Get(Get(renderer,"HintLayout"),"Visible"),"closed shell does not prepare or draw hidden hints");
            Console.WriteLine("PASS: real G05 F5 pointer/edit/cancel/shared binding, compact icon/key fields, inventory grid and stable viewport workload."+(graphics==null?"":" Original-resource PNGs produced."));
        }
        // Exercise the same final consumer as DrawHints, using production font
        // measurement and current projected target, without requiring a GPU.
        internal static void FinalHint(object shell,object element,string expected)
        {
            object state=Get(shell,"State"),renderer=Get(shell,"renderer"),layout=Get(state,"Layout");
            object rect=GetOptional(element,"Description")!=null?Get(element,"HintRect"):Get(element,"Rect");
            var view=Call(Get(layout,"Viewport"),"Offset",Get(state,"X"),Get(state,"Y"));
            float x=Math.Max((float)Get(rect,"X"),(float)Get(view,"X"))+1;
            float y=Math.Max((float)Get(rect,"Y"),(float)Get(view,"Y"))+1;
            Require(x<(float)Get(rect,"Right") && y<(float)Get(rect,"Bottom") && x<(float)Get(view,"Right") && y<(float)Get(view,"Bottom"),"hint target is actually visible");
            var input=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            foreach(var field in new[]{Tuple.Create("Width",(object)960f),Tuple.Create("Height",(object)760f),Tuple.Create("Scale",(object)1f),Tuple.Create("X",(object)x),Tuple.Create("Y",(object)y),Tuple.Create("Active",(object)true),Tuple.Create("Focused",(object)true)})Set(input,field.Item1,field.Item2);
            Call(state,"Update",input);
            var bounds=Activator.CreateInstance(rect.GetType(),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{8f,8f,944f,744f},null);
            Call(renderer,"PrepareHints",state,Get(shell,"items"),false,false,bounds,view,Get(renderer,"font"),Get(renderer,"hintMeasure"));
            var hint=Get(renderer,"HintLayout");
            Require((bool)Get(hint,"Visible") && string.Concat(((IEnumerable)Get(hint,"Lines")).Cast<object>().Select(line=>(string)Get(line,"Text")))==expected,
                "actual final hint text for "+GetOptional(element,"HotkeyTarget")+": "+expected);
        }
        private static void ModePresentation(object quick,object shell,object page,object panel,object state,Action<float,float,float> prepare)
        {
            var settings=(QuickItemSettings)Get(quick,"Settings");var original=settings.Current;var old=original.Entries[0];string reason;
            var legacy=new QuickItemEntry(old.Id,5358,QuickItemMode.SetStateAndUse,false,true);
            Require(settings.TryChange(original.Change(legacy),legacy.Id,out reason),"seed legal legacy entry");
            NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");var binding=bindings.Get(legacy.ActionId);
            try
            {
                // The total switch, favorite switch and per-entry key share a
                // command enum but keep distinct binding purposes.
                foreach(string id in new[]{"items.quick-items.toggle","items.keep-favorited.toggle",legacy.ActionId})
                {
                    var part=((IEnumerable)Get(panel,"logical")).Cast<object>().First(p=>(string)GetOptional(Get(p,"Element"),"HotkeyTarget")==id);
                    Call(state,"ScrollTo",Get(Get(Get(part,"Element"),"Rect"),"Y"));prepare(960,760,1);
                    var key=Controls(page).First(c=>(string)GetOptional(Get(c,"Element"),"HotkeyTarget")==id);
                    string text=(string)GetOptional(Get(key,"Element"),"Text");
                    FinalHint(shell,Get(key,"Element"),id==legacy.ActionId?(string.IsNullOrEmpty(text)?"双击设置快捷键":text+" · 双击设置快捷键"):"双击设置功能开关快捷键");
                }
                long commands=settings.AcceptedCommandId;
                Action open=()=>{Reveal(panel,state,"Edit");prepare(960,760,1);ClickControl(page,Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Edit"));prepare(960,760,1);};
                open();
                var parts=((IEnumerable)Get(panel,"logical")).Cast<object>().ToArray();var modes=parts.Where(p=>Get(p,"Command").ToString()=="Mode").ToArray();
                Require(modes.Length==2 && modes.Select(p=>(int)Get(p,"Type")).SequenceEqual(new[]{0,1}) && (bool)Get(modes[0],"Selected") && !(bool)Get(modes[1],"Selected"),"legacy 2 selects ordinary Use in a two-mode editor");
                Require(parts.Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="更换/配置快捷物品") && !parts.Any(p=>((string)GetOptional(p,"Hint")??"").Contains("设置后使用")),"editing title and legacy card hint use current visible modes");
                Reveal(panel,state,"Mode");prepare(960,760,1);
                foreach(var mode in ((IEnumerable)Get(panel,"visible")).Cast<object>().Where(p=>Get(p,"Command").ToString()=="Mode"))
                    FinalHint(shell,Get(mode,"Element"),(int)Get(mode,"Type")==0?"按快捷键后使用一次物品":"对于多形态物品，仅切换其形态但不使用");
                Reveal(panel,state,"Policy");prepare(960,760,1);
                var policy=((IEnumerable)Get(panel,"visible")).Cast<object>().Single(p=>Get(p,"Command").ToString()=="Policy");
                FinalHint(shell,Get(policy,"Element"),"首选物品不可用时，允许用同效果道具代替");
                Call(panel,"Suspend");prepare(960,760,1);
                Require(settings.AcceptedCommandId==commands && settings.Current.Find(old.Id).Mode==QuickItemMode.SetStateAndUse,"open/cancel leaves legacy document untouched");
                open();Reveal(panel,state,"Mode");prepare(960,760,1);
                var modeControl=Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Mode" && (int)Get(((IList)Get(panel,"logical"))[(int)Get(c,"Argument")],"Type")==0);
                ClickControl(page,modeControl);NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
                var saved=settings.Current.Find(old.Id);
                Require(saved.Mode==QuickItemMode.Use && saved.Target==5358 && !saved.Compatible && saved.Enabled && settings.Current.Entries.Count==original.Entries.Count &&
                    object.Equals(binding,bindings.Get(saved.ActionId)),"explicit mode save normalizes in place and preserves entry and binding identity");
            }
            finally
            {
                Call(panel,"Suspend");Require(settings.TryChange(original,null,out reason),"restore isolated UI fixture");
                NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            }
        }
        private static void EmptyPresentation(object quick,object page,object panel,Action<float,float,float> prepare)
        {
            var original=(QuickItemSettings)Get(quick,"Settings");
            using(var empty=new QuickItemSettings(new UiStore(QuickItemDocument.Encode(QuickItemDocument.Empty))))
            {
                NativeQuickUseMatrix.Until(()=>{empty.Poll();return empty.Loaded;});
                // Exercise the production layout against a real loaded empty
                // document, without publishing temporary actions or altering the
                // live fixture's saved binding identities.
                Set(quick,"Settings",empty);Set(panel,"dirty",true);
                try
                {
                    prepare(960,760,1);var parts=((IEnumerable)Get(panel,"logical")).Cast<object>().ToArray();
                    Require(parts.Count(p=>Get(p,"Command").ToString()=="Add")==1 && !parts.Any(p=>Get(p,"Command").ToString()=="Edit" || Get(p,"Command").ToString()=="Pick"),
                        "zero entries retain Add and both switches without phantom cards");
                    Require(parts.Count(p=>Get(p,"Command").ToString()=="FavoriteOn" || Get(p,"Command").ToString()=="QuickOn")==2,"empty document retains both real feature rows");
                    GroupGeometry(page,panel);
                    var compact=Get(Get(parts[0],"Element"),"Rect");float compactHeight=(float)Get(compact,"Height");
                    var ordinary=Get(Get(parts.Last(v=>Get(Get(v,"Element"),"Kind").ToString()=="Panel"),"Element"),"Rect");
                    Require(compactHeight==(float)Get(ordinary,"Height"),"empty quick group stays the ordinary single-row height with no reserved list area");
                    float[] geometry=Geometry(page);int layouts=(int)Get(page,"LayoutBuildCount");
                    for(int i=0;i<60;i++)prepare(960,760,1);
                    Require(geometry.SequenceEqual(Geometry(page)) && (int)Get(page,"LayoutBuildCount")==layouts,"zero-entry complete geometry is stable");
                    foreach(int count in new[]{1,4})
                    {
                        string reason;
                        Require(empty.TryChange(new QuickItemDocument(false,false,original.Current.Entries.Take(count)),null,out reason),"isolated single/multirow quick document");
                        NativeQuickUseMatrix.Until(()=>{empty.Poll();return !empty.Busy;});prepare(960,760,1);GroupGeometry(page,panel);
                        Require((float)Get(Get(Get(((IList)Get(panel,"logical"))[0],"Element"),"Rect"),"Height")>compactHeight,"populated quick surface grows around actual entries");
                    }
                }
                finally {Set(quick,"Settings",original);Set(panel,"dirty",true);prepare(960,760,1);}
            }
        }
        private static void GroupGeometry(object page,object panel)
        {
            var parts=((IEnumerable)Get(panel,"logical")).Cast<object>().ToArray();
            var backgrounds=parts.Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel").ToArray();
            Require(backgrounds.Length==2,"quick block and independent favorite each have exactly one background, including while editing");
            object group=Get(backgrounds[0],"Element"),rect=Get(group,"Rect"),favorite=Get(Get(backgrounds[1],"Element"),"Rect");
            float bottom=(float)Get(rect,"Bottom"),right=(float)Get(rect,"Right");
            Require(Get(group,"Command").ToString()=="None" && Get(backgrounds[0],"Command").ToString()=="None","quick group remains decoration");
            var children=parts.TakeWhile(p=>!ReferenceEquals(p,backgrounds[1])).Skip(1).ToArray();
            var dividers=children.Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Divider").ToArray();
            var bodies=children.Where(p=>Get(p,"Command").ToString()=="Edit" || Get(p,"Command").ToString()=="RetryCleanup").ToArray();
            bool hasBody=(bool)Get(panel,"Editing") || bodies.Length>0;
            Require(dividers.Length==(hasBody?1:0),"quick group gets one divider for entries/editor/cleanup, none when empty");
            if(hasBody)
            {
                var line=Get(Get(dividers[0],"Element"),"Rect");
                var toggle=children.First(p=>Get(p,"Command").ToString()=="QuickOn");var toggleRect=Get(Get(toggle,"Element"),"Rect");
                var title=children.First(p=>GetOptional(Get(p,"Element"),"Description")!=null);var titleRect=Get(Get(title,"Element"),"Rect");
                if((float)Get(toggleRect,"Y")<(float)Get(titleRect,"Bottom"))
                    Require(Math.Abs((float)Get(toggleRect,"Y")+(float)Get(toggleRect,"Height")/2-((float)Get(rect,"Y")+(float)Get(line,"Bottom"))/2)<.01f,
                        "actual quick header controls remain centered inside the top-edge/divider band");
                float bodyTop=(bool)Get(panel,"Editing")?(float)Get(panel,"SelectorTop"):bodies.Min(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Y"));
                Require((float)Get(line,"Height")==1 && (float)Get(line,"X")== (float)Get(rect,"X")+12 && (float)Get(line,"Right")==right-12 &&
                    Math.Abs(bodyTop-(float)Get(line,"Bottom")-6f)<.01f && Get(dividers[0],"Command").ToString()=="None" && GetOptional(Get(dividers[0],"Element"),"Description")==null,
                    "quick divider closes the measured header before the unchanged body gap and has no action or hint");
            }
            Require(children.All(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Bottom")<=bottom),"all quick header/editor/retry/cards remain inside their sole group");
            var rows=(float[])Get(Get(page,"layout"),"RowY");
            Require(bottom+6==rows[0] && (float)Get(favorite,"Y")>=bottom+6 && (float)Get(panel,"Height")==(float)Get(favorite,"Bottom")+6,
                "Build seals quick before stack/processing while AppendFavorite publishes the independent page footer");
            Require(!children.Where(p=>Get(p,"Command").ToString()!="None" || GetOptional(Get(p,"Element"),"HotkeyTarget")!=null).Any(p=>
                (float)Get(Get(Get(p,"Element"),"Rect"),"Right")>right-1 && (float)Get(Get(Get(p,"Element"),"Rect"),"Bottom")>bottom-1),"group bottom/right padding has no command");
            Require(children.Where(p=>GetOptional(Get(p,"Element"),"Description")!=null).All(p=>(float)Get(Get(Get(p,"Element"),"HintRect"),"Bottom")<bottom),
                "quick name help keeps measured text bounds");
            object view=Get(page,"view");float projectedY=(float)Get(rect,"Y")+(float)Get(view,"Y")-(float)Get(Get(page,"shell"),"Scroll");
            if(projectedY+(float)Get(rect,"Height")>(float)Get(view,"Y") && projectedY<(float)Get(view,"Bottom"))
            {
                object visible=((IEnumerable)Get(panel,"visible")).Cast<object>().First(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel");
                Require((float)Get(Get(Get(visible,"Element"),"Rect"),"Height")== (float)Get(rect,"Height") &&
                    (float)Get(Get(Get(visible,"Element"),"Rect"),"Y")==projectedY,"visible quick background retains its full logical bounds across viewport clipping");
            }
            foreach(var divider in dividers)
            {
                var line=Get(Get(divider,"Element"),"Rect");float lineY=(float)Get(line,"Y")+(float)Get(view,"Y")-(float)Get(Get(page,"shell"),"Scroll");
                var projected=((IEnumerable)Get(panel,"visible")).Cast<object>().Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Divider").ToArray();
                bool inView=lineY+1>(float)Get(view,"Y") && lineY<(float)Get(view,"Bottom");
                Require(projected.Length==(inView?1:0),"quick divider follows the same viewport intersection as content");
                if(inView)
                {
                    var visibleLine=Get(Get(projected[0],"Element"),"Rect");
                    Require((float)Get(visibleLine,"Y")==lineY && (float)Get(visibleLine,"Height")==1 && (float)Get(visibleLine,"Width")== (float)Get(line,"Width"),"quick divider preserves full bounds for scissor clipping");
                    float x=(float)Get(visibleLine,"X")+1,y=Math.Max(lineY,(float)Get(view,"Y"))+.25f;
                    Require(!Controls(page).Any(c=>(bool)Call(Get(c,"Rect"),"Contains",x,y)),"projected quick divider is never a command target");
                }
            }
        }
        private static object[] Controls(object page){return ((IEnumerable)Get(page,"controls")).Cast<object>().ToArray();}
        internal static float[] Geometry(object page)
        {
            var values=new List<float>();
            Action<object> rectangle=e=>{object r=Get(e,"Rect");values.Add((float)Get(r,"X"));values.Add((float)Get(r,"Y"));values.Add((float)Get(r,"Width"));values.Add((float)Get(r,"Height"));};
            object layout=GetOptional(page,"layout"),coin=GetOptional(page,"CoinPanel"),quick=GetOptional(page,"QuickPanel");
            if(layout!=null)foreach(object e in (IEnumerable)Get(layout,"rows"))rectangle(e);
            if(coin!=null){foreach(object e in (IEnumerable)Get(coin,"rows"))rectangle(e);values.Add((float)Get(coin,"Height"));}
            if(quick!=null){foreach(object part in (IEnumerable)Get(quick,"logical"))rectangle(Get(part,"Element"));values.Add((float)Get(quick,"Height"));}
            return values.ToArray();
        }
        private static void SavePresentation(object quick,object shell,object page,object panel,object state,Action<float,float,float> prepare)
        {
            var original=(QuickItemSettings)Get(quick,"Settings");
            var storage=new UiStore(QuickItemDocument.Encode(original.Current));
            using(var temporary=new QuickItemSettings(storage))
            {
                NativeQuickUseMatrix.Until(()=>{temporary.Poll();return temporary.Loaded;});
                Set(quick,"Settings",temporary);Set(quick,"published",-1L);Set(panel,"dirty",true);Call(quick,"Poll");
                var alerts=new List<string>();Action<string> display=alerts.Add;
                try
                {
                    prepare(960,760,1);
                    foreach(bool delayed in new[]{false,true})foreach(string command in new[]{"FavoriteOn","FavoriteOff","QuickOn","QuickOff"})
                    {
                        Reveal(panel,state,command);prepare(960,760,1);float[] before=Geometry(page);
                        if(delayed)storage.Gate.Reset();
                        var control=Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")==command);
                        ClickControl(page,control);Require(temporary.Busy,"physical switch release submits a real storage command");
                        prepare(960,760,1);Require(before.SequenceEqual(Geometry(page)),"accepted save has no message height or card movement");
                        if(delayed)for(int i=0;i<20;i++)
                        {Call(quick,"Poll");prepare(960,760,1);Require(temporary.Busy && before.SequenceEqual(Geometry(page)),"blocked save remains paused with stable complete geometry");}
                        storage.Gate.Set();NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});prepare(960,760,1);
                        Require(before.SequenceEqual(Geometry(page)),"reliable completion preserves all row/card positions");
                        Call(quick,"TakeFeedback",display);Require(alerts.Count==0,"normal pending/complete save stays silent");
                    }
                    // A failed new item never becomes an editable saved identity.
                    // Reopening Add creates a new draft; only reliable new-item
                    // completion replaces that failure, not an unrelated toggle.
                    Action add=()=>{Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
                        Reveal(panel,state,"Pick");prepare(960,760,1);ClickControl(page,Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Pick"));
                        NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});prepare(960,760,1);};
                    Action<string> nameHint=expected=>
                    {
                        Call(state,"ScrollTo",0f);prepare(960,760,1);
                        var name=((IEnumerable)Get(panel,"visible")).Cast<object>().Select(p=>Get(p,"Element")).First(e=>
                            GetOptional(e,"Description")!=null && (string)Get(Get(e,"Description"),"Id")=="items.quick-items.toggle");
                        FinalHint(shell,name,expected);
                    };
                    nameHint("按快捷键快速使用物品");
                    int count=temporary.Current.Entries.Count;storage.Fail=true;add();
                    Require(temporary.Current.Entries.Count==count && GetOptional(quick,"QuickHint")!=null,"failed actual Add preserves saved entries and its error");
                    nameHint((string)Get(quick,"QuickHint"));
                    Call(quick,"TakeFeedback",display);Require(alerts.Count==1,"failed new draft alerts once");
                    storage.Fail=false;Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(GetOptional(quick,"QuickHint")!=null,"unrelated successful toggle does not retire a failed draft");
                    add();Require(temporary.Current.Entries.Count==count+1 && GetOptional(quick,"QuickHint")==null,"reliable new Add replaces failed draft even with its new identity");
                    Call(quick,"TakeFeedback",display);Require(alerts.Count==1,"recovered new Add is quiet");alerts.Clear();
                    storage.Fail=true;Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    prepare(960,760,1);float[] failedGeometry=Geometry(page);string error=(string)Get(quick,"FavoriteHint");
                    Require(!string.IsNullOrEmpty(error) && !temporary.KeepFavorited,"actual failed favorite write remains stopped and readable");
                    Call(state,"Close");alerts.AddRange(ShellFeedback(shell).Where(text=>text==error));Require(alerts.Count==1,"failure while the page is closed reaches actual local Main.NewText through F5");
                    for(int i=0;i<30;i++){Call(quick,"Poll");Call(quick,"TakeFeedback",display);}Require(alerts.Count==1,"same failed result never repeats");
                    storage.Fail=false;Call(quick,"ToggleQuick");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require((string)Get(quick,"FavoriteHint")==error && !temporary.KeepFavorited,"unrelated saved switch cannot erase unresolved favorite failure");
                    Call(state,"RestoreVisible");prepare(960,760,1);Require(failedGeometry.SequenceEqual(Geometry(page)),"error and unrelated success leave whole geometry unchanged");
                    storage.Gate.Reset();storage.Fail=true;Call(quick,"ToggleFavorite");prepare(960,760,1);
                    Require((string)Get(quick,"FavoriteHint")==error && temporary.Busy,"retry in flight does not claim error recovery");
                    storage.Gate.Set();NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});Call(quick,"TakeFeedback",display);
                    Require(alerts.Count==2,"new independent failure with identical text alerts again");
                    storage.Fail=false;Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(GetOptional(quick,"FavoriteHint")==null && temporary.KeepFavorited,"reliable retry retires only its own failure");
                    storage.Fail=storage.Unknown=true;Call(quick,"ToggleQuick");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    alerts.AddRange(ShellFeedback(shell).Where(text=>text==temporary.QuickMessage));
                    // This CPU fixture has no installed render layers. The real shell
                    // correctly retires Ready after delivering feedback; re-enter the
                    // same explicit layout fixture used above before comparing geometry.
                    Set(state,"Ready",true);Call(state,"RestoreVisible");prepare(960,760,1);
                    Require(temporary.Protected && temporary.CommitUnconfirmed && !temporary.Enabled,"unknown save keeps file/execution protection");
                    Require(alerts.Count==3,"unknown save reaches local feedback exactly once; actual count="+alerts.Count);
                    Require(!string.IsNullOrEmpty((string)Get(quick,"QuickHint")),"unknown save keeps persistent explanation");
                    Require(failedGeometry.SequenceEqual(Geometry(page)),"unknown save keeps fixed geometry");
                }
                finally
                {
                    storage.Gate.Set();Set(quick,"Settings",original);Set(quick,"published",-1L);Set(panel,"dirty",true);Call(quick,"Poll");
                    // Restoring the isolated settings reliably retires the test
                    // addition and starts its real asynchronous binding cleanup.
                    // Drain that boundary before another test edits bindings.
                    var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
                    NativeQuickUseMatrix.Until(()=>{bindings.Poll();Call(quick,"Poll");return !bindings.Busy && GetOptional(quick,"cleaning")==null && (int)Get(Get(quick,"retiredBindings"),"Count")==0;});
                    Set(state,"Ready",true);Call(state,"RestoreVisible");prepare(960,760,1);
                }
            }
        }
        internal sealed class UiStore:IPreferenceStorage
        {
            private byte[] bytes;internal bool Fail,Unknown;internal readonly ManualResetEventSlim Gate=new ManualResetEventSlim(true);
            internal UiStore(byte[] bytes){this.bytes=bytes;}
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Loaded,bytes,"fixture",null);}
            public PreferenceWriteResult Write(string identity,byte[] value)
            {if(!Gate.Wait(5000))throw new TimeoutException("Isolated UI save gate timed out.");if(Fail)return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"isolated UI storage failure",Unknown,Unknown);bytes=value;return new PreferenceWriteResult(PreferenceWriteStatus.Saved,"fixture",null);}
            public void Dispose(){Gate.Dispose();}
        }
        private static string GetPart(object panel,object control,string field){return Get(((IList)Get(panel,"logical"))[(int)Get(control,"Argument")],field).ToString();}
        private static void Reveal(object panel,object state,string command)
        {object part=((IEnumerable)Get(panel,"logical")).Cast<object>().First(p=>Get(p,"Command").ToString()==command);Call(state,"ScrollTo",Math.Max(0,(float)Get(Get(Get(part,"Element"),"Rect"),"Y")-12));}
        private static void Click(object page,string command,string text)
        {ClickControl(page,Controls(page).First(c=>Get(c,"Command").ToString()==command && (text==null || (string)Get(Get(c,"Element"),"Text")==text)));}
        private static void ClickControl(object page,object control)
        {
            object r=Get(control,"Rect");var point=new Vector2((float)Get(r,"X")+8,(float)Get(r,"Y")+8);
            foreach(ButtonState left in new[]{ButtonState.Released,ButtonState.Pressed,ButtonState.Released})
            {PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(page,"ProcessInput",true,new KeyboardState(),point,true,true,false);}
        }
    }
}
