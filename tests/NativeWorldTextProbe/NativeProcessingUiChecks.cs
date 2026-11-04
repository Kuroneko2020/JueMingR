using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Features.Processing;
using JueMingR.Features.Text;
using JueMingR.Infrastructure.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingUiChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"Processing");var shell=Get(context,"Shell");var state=Get(shell,"State");var layout=Get(state,"Layout");var ui=Get(shell,"ReforgeUi");var renderer=Get(shell,"renderer");var input=Get(context,"Input");
            var settings=((ProcessingSettings[])Get(host,"Settings"))[2];Save(settings,new ProcessingOptions());
            Main.mouseItem.TurnToAir();Main.reforgeItem.TurnToAir();Main.blockInput=false;PlayerInput.WritingText=false;
            Call(state,"Navigate",1);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            var imeField=Get(ui,"TextInput").GetType().GetField("ime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var ime=new Ime(imeField.FieldType);imeField.SetValue(Get(ui,"TextInput"),ime.GetTransparentProxy());
            Action prepare=()=>Prepare(context,960,760,1);
            prepare();
            GroupGeometry(shell,ui);
            float compactReforgeHeight=(float)Get(ui,"Height");
            Require(Parts(ui).Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="自动重铸"),"reforge uses existing Misc page");
            var key=Parts(ui).Single(p=>(int)Get(p,"Command")==6);
            NativeQuickUiChecks.FinalHint(shell,Get(key,"Element"),"双击设置功能开关快捷键");
            float height=(float)Get(layout,"ContentHeight");
            Save(settings,new ProcessingOptions(true));prepare();Require(height==(float)Get(layout,"ContentHeight"),"toggle does not add waiting/status rows");
            long builds=(long)Get(ui,"LayoutBuilds"),measures=(long)Get(ui,"EditMeasurements");for(int i=0;i<120;i++)prepare();
            Require(builds==(long)Get(ui,"LayoutBuilds") && measures==(long)Get(ui,"EditMeasurements"),"stable reforge page reuses geometry and edit measurement");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.W,Keys.Space));
            Require(!Main.blockInput && !PlayerInput.WritingText && Main.keyState.IsKeyDown(Keys.Space),"plain list leaves movement keyboard alone");
            var field=Parts(ui).First(p=>(int)Get(p,"Command")==1);
            var on=Parts(ui).First(p=>(int)Get(p,"Command")==4);
            Require(!Parts(ui).Any(p=>(int)Get(p,"Command")==2),"blank draft has no visible or executable Add control");
            var hint=GetOptional(field,"Label");Require(hint!=null,"idle input hint has measured glyph layout");
            var hintRect=Get(hint,"Rect");var fieldRect=Get(Get(field,"Element"),"Rect");
            Require((float)Get(hintRect,"Width")<=(float)Get(fieldRect,"Width")-12 &&
                Math.Abs((float)Get(hintRect,"X")+(float)Get(hintRect,"Width")/2-(float)Get(fieldRect,"X")-(float)Get(fieldRect,"Width")/2)<.01f &&
                Math.Abs((float)Get(hintRect,"Y")+(float)Get(hintRect,"Height")/2-(float)Get(fieldRect,"Y")-(float)Get(fieldRect,"Height")/2)<.01f,"idle input hint is clipped and centered horizontally and vertically");
            Click(context,ui,field);prepare();Require(GetOptional(ui,"Editor")==null,"single click must not lease the text keyboard");
            Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));prepare();
            Require(GetOptional(ui,"Editor")!=null,"physical double click starts text editor");
            string name=Lang.prefix[1].Value;
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(),name);
            Require(Main.blockInput && PlayerInput.WritingText && ((TextEditBuffer)Get(ui,"Editor")).Text==name,"only actual editor leases native keyboard and commits whole name");
            prepare();var add=Parts(ui).Single(p=>(int)Get(p,"Command")==2);field=Parts(ui).Single(p=>(int)Get(p,"Command")==1);
            Require(Y(field)==Y(add) && X(add)<X(field),"valid draft Add shares its field row on the left");
            ime.Composition="ni";Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
            Require(settings.Value.Names.Count==0 && GetOptional(ui,"Editor")!=null,"composition Enter cannot add unfinished name");
            prepare();Require(!Parts(ui).Any(p=>(int)Get(p,"Command")==2) && GetOptional(ui,"Editor")!=null && ((TextEditBuffer)Get(ui,"Editor")).Text==name,"synthetic IME composition hides Add without dropping its existing draft/editor");
            ime.Composition="";ime.Candidates=true;Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());prepare();
            Require(!Parts(ui).Any(p=>(int)Get(p,"Command")==2) && (bool)Get(ui,"OwnsTextToken") && ((TextEditBuffer)Get(ui,"Editor")).Text==name,"candidate-only synthetic IME keeps its lease/draft and hides Add");
            ime.Candidates=false;Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());prepare();
            add=Parts(ui).Single(p=>(int)Get(p,"Command")==2);var addPoint=Point(add);long beforeComposition=settings.AcceptedCommandId;
            Step(context,ui,true,addPoint,new KeyboardState());ime.Candidates=true;Step(context,ui,false,addPoint,new KeyboardState());prepare();
            Require(settings.AcceptedCommandId==beforeComposition && !Parts(ui).Any(p=>(int)Get(p,"Command")==2) && (bool)Get(ui,"OwnsTextToken"),"candidate-only IME revokes an old Add press without losing text ownership");ime.Candidates=false;
            ime.Composition="";Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});
            Require(settings.Value.Names.SequenceEqual(new[]{name}),"fresh Enter persists one complete target");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Require(!Main.blockInput && !PlayerInput.WritingText,"editor completion releases keyboard tail");prepare();
            GroupGeometry(shell,ui);Require((float)Get(ui,"Height")>compactReforgeHeight,"single prefix expands its owning group");
            DoubleClick(context,ui);Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(),"not-a-prefix");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
            Require(GetOptional(ui,"Editor")!=null && settings.Value.Names.Count==1,"invalid target retains draft and does not save");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Escape),"\x1b");
            Require(GetOptional(ui,"Editor")==null && (bool)Get(state,"Visible"),"Esc cancels editing without closing page");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());prepare();
            var remove=Parts(ui).First(p=>(int)Get(p,"Command")==3);Vector2 point=Point(remove);
            Step(context,ui,true,point,new KeyboardState());
            Save(settings,new ProcessingOptions(true,new[]{name,Lang.prefix[2].Value}));prepare();Step(context,ui,false,point,new KeyboardState());
            Require(settings.Value.Names.Count==2,"changed list revision revokes old removal press");
            prepare();
            var cards=Parts(ui).Where(p=>(int)Get(p,"Command")==7).ToArray();
            GroupGeometry(shell,ui);
            Require(cards.Length==2 && Y(cards[0])==Y(cards[1]) && X(cards[1])>X(cards[0]),"prefix cards flow horizontally");
            var card=Get(Get(cards[0],"Element"),"Rect");
            Require((float)Get(card,"Width")==47 && (float)Get(card,"Height")==34,"prefix cards use the existing item-list size");
            foreach(var entry in cards)
            {
                var r=Get(Get(entry,"Element"),"Rect");var label=Get(Get(entry,"Label"),"Rect");
                Require(Math.Abs((float)Get(label,"X")+(float)Get(label,"Width")/2-(float)Get(r,"X")-(float)Get(r,"Width")/2)<.01f &&
                    Math.Abs((float)Get(label,"Y")+(float)Get(label,"Height")/2-(float)Get(r,"Y")-(float)Get(r,"Height")/2)<.01f,"prefix glyph bounds are centered in each item-size card");
            }
            Click(context,ui,cards[0]);Require(settings.Value.Names.Count==2,"card body does not delete a target");
            var group=Get(Get(((IEnumerable)Get(ui,"logical")).Cast<object>().First(),"Element"),"Rect");
            var ownerView=Get(ui,"view");var blank=new Vector2((float)Get(ownerView,"X")+(float)Get(group,"Right")-1,
                (float)Get(ownerView,"Y")+(float)Get(group,"Bottom")-(float)Get(state,"Scroll")-1);
            long beforeBlank=settings.AcceptedCommandId;
            Step(context,ui,false,blank,new KeyboardState());Step(context,ui,true,blank,new KeyboardState());Step(context,ui,false,blank,new KeyboardState());
            Require(settings.AcceptedCommandId==beforeBlank && GetOptional(ui,"Editor")==null,"group padding cannot toggle, edit, add or remove");
            prepare=()=>Prepare(context,960,440,1);
            var names=Lang.prefix.Skip(1).Select(p=>p.Value).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();Save(settings,new ProcessingOptions(false,names));prepare();
            Call(state,"ScrollTo",Math.Max(0,(float)Get(ui,"Height")-(float)Get(Get(Get(state,"Layout"),"Viewport"),"Height")));prepare();float bottom=(float)Get(state,"Scroll");Require(bottom>0,"full list owns shared Misc scroll height");
            GroupGeometry(shell,ui);
            for(int i=0;i<20;i++)prepare();Require(bottom==(float)Get(state,"Scroll"),"tax preparation cannot reset reforge list scroll");
            remove=Parts(ui).Last(p=>(int)Get(p,"Command")==3);string identity=(string)Get(remove,"Name");Click(context,ui,remove);
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(!settings.Value.Names.Contains(identity) && settings.Value.Names.Count==names.Length-1,"visible bottom removes exact whole-name identity");
            Call(state,"ScrollTo",0f);prepare();
            // A mouse-down in one layout must not delete after a scale reflow.
            remove=Parts(ui).First(p=>(int)Get(p,"Command")==3);point=Point(remove);int count=settings.Value.Names.Count;
            Step(context,ui,true,point,new KeyboardState());Prepare(context,1280,720,1.5f);Step(context,ui,false,point,new KeyboardState());
            GroupGeometry(shell,ui);
            Require(count==settings.Value.Names.Count,"scale change invalidates armed corner delete");
            prepare();Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));
            Call(ui,"ProcessInput",true,new KeyboardState(),point,true,false,false);prepare();
            Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));
            Require(GetOptional(ui,"Editor")==null,"focus loss breaks a pending double click");
            Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));prepare();
            Require(GetOptional(ui,"Editor")!=null,"fresh same-field double click works after focus return");
            Call(ui,"ProcessInput",true,new KeyboardState(),point,true,true,true);prepare();
            Require(GetOptional(ui,"Editor")==null && !(bool)Get(ui,"OwnsTextToken"),"higher popup yields editor and keyboard ownership");
            Save(settings,new ProcessingOptions());prepare();GroupGeometry(shell,ui);
            Require((float)Get(ui,"Height")==compactReforgeHeight,"clearing prefix content restores compact group and following layout");
            NativeMiscPolishUiChecks.Processing(context);
            ReforgeHints(context,ime);
            Call(state,"Close");Call(ui,"Suspend");Save(settings,new ProcessingOptions());
            Console.WriteLine("PASS G08 UI: physical editor/list controls, isolated IME boundary, whole-name persistence, stable height/measurement, movement keys and shared scroll.");
        }
        private static void ReforgeHints(object context,Ime ime)
        {
            var host=Get(context,"Processing");var shell=Get(context,"Shell");var ui=Get(shell,"ReforgeUi");var state=Get(shell,"State");
            var slots=(ProcessingSettings[])Get(host,"Settings");var settings=slots[2];var oldItem=Main.reforgeItem;
            Action prepare=()=>Prepare(context,960,760,1);
            Action<string> hint=expected=>{prepare();NativeQuickUiChecks.FinalHint(shell,Get(Parts(ui).Single(p=>(int)Get(p,"Command")==1),"Element"),expected);};
            Action<string> text=value=>{((TextEditBuffer)Get(ui,"Editor")).SelectAll();Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(),value);};
            const string invalid="需输入当前语言下存在的词缀";
            string first=Lang.prefix[1].Value,second=Lang.prefix[2].Value;
            try
            {
                Main.reforgeItem=new Item();Call(ui,"CancelEdit");Save(settings,new ProcessingOptions());
                hint("双击输入词缀");DoubleClick(context,ui);hint(invalid);
                text("not-a-prefix");hint(invalid);
                Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");hint(invalid);
                text(first);hint("双击输入词缀");Require(Parts(ui).Any(p=>(int)Get(p,"Command")==2),"correcting a failed name retires its old submit error");
                ((TextEditBuffer)Get(ui,"Editor")).SelectAll();Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Back));hint(invalid);
                ime.Composition="ni";Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());hint("双击输入词缀");
                ime.Composition="";ime.Candidates=true;Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");hint("双击输入词缀");
                Require(settings.Value.Names.Count==0 && !Parts(ui).Any(p=>(int)Get(p,"Command")==2),"IME composition/candidates suppress both premature name errors and Add");
                ime.Candidates=false;Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(),first);hint("双击输入词缀");
                Require(Parts(ui).Any(p=>(int)Get(p,"Command")==2) && settings.Value.Names.Count==0,"confirmed whole IME text becomes addable without implicit submit");

                text("not-a-prefix");var editor=(TextEditBuffer)Get(ui,"Editor");editor.MoveTo(1);editor.MoveTo(3,true);
                Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Escape),"\x1b");hint("双击输入词缀");
                DoubleClick(context,ui);editor=(TextEditBuffer)Get(ui,"Editor");Require(editor.Text=="not-a-prefix" && editor.Anchor==1 && editor.Caret==3,"cancel/re-edit preserves the unsubmitted draft and selection");hint(invalid);
                // Rejected buffer edits do not change its content revision. A new
                // edit cycle must retire that rejection while preserving text/caret.
                editor.SelectAll();Require(!editor.Insert(new string('x',129)),"oversized paste is rejected by the actual buffer transaction, not a fabricated oversized native key queue");hint(invalid);
                int anchor=editor.Anchor,caret=editor.Caret;string kept=editor.Text;Call(ui,"CancelEdit");hint("双击输入词缀");DoubleClick(context,ui);
                editor=(TextEditBuffer)Get(ui,"Editor");Require(editor.Text==kept && editor.Anchor==anchor && editor.Caret==caret,"retiring a rejected edit cannot reset the draft/caret");hint(invalid);
                Call(state,"Navigate",0);Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Call(state,"Navigate",1);prepare();
                Require(GetOptional(ui,"Editor")==null,"actual inactive-page input retires editing before returning to the page");hint("双击输入词缀");
                DoubleClick(context,ui);text(first);hint("双击输入词缀");

                var input=Get(ui,"TextInput");var clipField=input.GetType().GetField("clipboard",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var oldClipboard=clipField.GetValue(input);
                try
                {
                    clipField.SetValue(input,new FailingClipboard(clipField.FieldType).GetTransparentProxy());
                    editor=(TextEditBuffer)Get(ui,"Editor");editor.SelectAll();
                    Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.LeftControl,Keys.V),second);
                    hint("无法粘贴：剪贴板不可用或内容过长。草稿未改动。");
                    Require(((TextEditBuffer)Get(ui,"Editor")).Text==second,"same-sample text commit is kept while a NEW clipboard failure stays visible");
                    Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());text(first);hint("双击输入词缀");
                }
                finally{clipField.SetValue(input,oldClipboard);}

                Call(ui,"CancelEdit");Save(settings,new ProcessingOptions(false,new[]{first}));prepare();DoubleClick(context,ui);text(first);hint(invalid);
                Require(!Parts(ui).Any(p=>(int)Get(p,"Command")==2),"duplicate remains rejected");
                Save(settings,new ProcessingOptions(false,Enumerable.Range(0,256).Select(i=>"retained-target-"+i)));text(second);hint("词缀名单已满。");
                Require(!Parts(ui).Any(p=>(int)Get(p,"Command")==2),"list capacity keeps its own reason and still refuses addition");
                Save(settings,new ProcessingOptions());Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(1);Main.reforgeItem.damage=0;text(Lang.prefix[5].Value);hint("双击输入词缀");
                Require(Parts(ui).Any(p=>(int)Get(p,"Command")==2),"an inapplicable slot cannot veto adding a shared complete target");
                Main.reforgeItem=new Item();Call(ui,"CancelEdit");

                // Real document workers/files are isolated by this native scope's
                // SavePath. Block the destination after a successful missing read.
                foreach(bool protectedRead in new[]{false,true})
                {
                    string path=Path.Combine(Terraria.Program.SavePath,"hint-fault-"+Guid.NewGuid().ToString("N")+".json");
                    if(protectedRead)File.WriteAllText(path,"{\"format\":\"JueMingR.Reforge\",\"version\":99,\"enabled\":false,\"names\":[]}");
                    using(var fault=new ProcessingSettings(new AtomicFileDocument(path,65536),2))
                    {
                        NativeQuickItemChecks.Until(()=>{fault.Poll();return fault.Loaded;});slots[2]=fault;prepare();
                        if(!protectedRead)
                        {
                            Directory.CreateDirectory(path);DoubleClick(context,ui);text(first);
                            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
                            NativeQuickItemChecks.Until(()=>{fault.Poll();return !fault.Busy;});
                            Require(fault.CompletedCommandId>0 && !fault.CompletionSucceeded,"real save failure reaches the actual document worker");
                        }
                        Require(fault.Message!=null && (!protectedRead || fault.Protected),"save/protected-read failure remains a business error");
                        hint(fault.Message);Call(ui,"CancelEdit");hint(fault.Message);Call(ui,"Suspend");prepare();hint(fault.Message);
                    }
                    slots[2]=settings;
                }
                Console.WriteLine("PASS Reforge final hints: idle/empty, invalid-to-valid, cancel/re-entry selection, page leave, duplicate/shared-target addition, IME confirmation, same-sample clipboard error, real save/protected-read failures.");
            }
            finally{slots[2]=settings;ime.Composition="";ime.Candidates=false;Main.reforgeItem=oldItem;Call(ui,"CancelEdit");Save(settings,new ProcessingOptions());}
        }
        private static void GroupGeometry(object shell,object ui)
        {
            var logical=((IEnumerable)Get(ui,"logical")).Cast<object>().ToArray();
            var panels=logical.Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel").ToArray();
            Require(panels.Length==1 && (int)Get(panels[0],"Command")==0,"reforge has exactly one non-command background");
            var rect=Get(Get(panels[0],"Element"),"Rect");float bottom=(float)Get(rect,"Bottom");
            Require(logical.Skip(1).All(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Bottom")<=bottom),"header and complete prefix grid stay in their own single group");
            var cards=logical.Where(p=>(int)Get(p,"Command")==7).ToArray();
            var dividers=logical.Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Divider").ToArray();
            Require(dividers.Length==(cards.Length>0?1:0),"reforge has one divider only while prefixes populate the group");
            if(cards.Length>0)
            {
                var line=Get(Get(dividers[0],"Element"),"Rect");
                var header=logical.First(p=>(int)Get(p,"Command")==1);var headerRect=Get(Get(header,"Element"),"Rect");
                var title=logical.First(p=>GetOptional(Get(p,"Element"),"Description")!=null);var titleRect=Get(Get(title,"Element"),"Rect");
                if((float)Get(headerRect,"Y")<(float)Get(titleRect,"Bottom"))
                    Require(Math.Abs((float)Get(headerRect,"Y")+(float)Get(headerRect,"Height")/2-((float)Get(rect,"Y")+(float)Get(line,"Bottom"))/2)<.01f,
                        "actual reforge input/toggle band is centered between the top frame and divider bottom edge");
                Require((float)Get(line,"Height")==1 && (float)Get(line,"X")== (float)Get(rect,"X")+12 && (float)Get(line,"Right")== (float)Get(rect,"Right")-12 &&
                    Math.Abs(cards.Min(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Y"))-(float)Get(line,"Bottom")-6f)<.01f && (int)Get(dividers[0],"Command")==0,
                    "reforge divider closes the header, leaves both ends inset and cannot issue a command");
            }
            float last=logical.Skip(1).Max(p=>(float)Get(Get(Get(p,"Element"),"Rect"),"Bottom"));
            Require(cards.Length==0?bottom-last>=0 && bottom-last<=4:bottom-last==6,"reforge empty group keeps only header padding; populated group has one bottom inset");
            Require((float)Get(ui,"Height")==bottom+6 && (float)Get(Get(shell,"MiningUi"),"start")==bottom+6,
                "only one group gap separates complete reforge from mining");
            var state=Get(shell,"State");var view=Get(ui,"view");
            float projectedY=(float)Get(rect,"Y")+(float)Get(view,"Y")-(float)Get(state,"Scroll");
            if(projectedY+(float)Get(rect,"Height")>(float)Get(view,"Y") && projectedY<(float)Get(view,"Bottom"))
            {
                var visible=Parts(ui).Single(p=>Get(Get(p,"Element"),"Kind").ToString()=="Panel");var projected=Get(Get(visible,"Element"),"Rect");
                Require((float)Get(projected,"Y")==projectedY && (float)Get(projected,"Height")== (float)Get(rect,"Height"),"scroll/scale projects full reforge surface for the original viewport clip");
            }
            foreach(var divider in dividers)
            {
                var line=Get(Get(divider,"Element"),"Rect");float lineY=(float)Get(line,"Y")+(float)Get(view,"Y")-(float)Get(state,"Scroll");
                var shown=Parts(ui).Where(p=>Get(Get(p,"Element"),"Kind").ToString()=="Divider").ToArray();
                bool inView=lineY+1>(float)Get(view,"Y") && lineY<(float)Get(view,"Bottom");
                Require(shown.Length==(inView?1:0),"reforge divider is projected only when it intersects the viewport");
                if(inView)
                {
                    var projected=Get(Get(shown[0],"Element"),"Rect");float x=(float)Get(projected,"X")+1,y=Math.Max(lineY,(float)Get(view,"Y"))+.25f;
                    Require((float)Get(projected,"Y")==lineY && (float)Get(projected,"Height")==1 && (float)Get(projected,"Width")== (float)Get(line,"Width") &&
                        !Parts(ui).Any(p=>(int)Get(p,"Command")!=0 && (bool)Call(Get(Get(p,"Element"),"Rect"),"Contains",x,y)),"reforge divider keeps unclipped bounds and no interactive hit region");
                }
            }
        }
        internal static void Prepare(object context,float width,float height,float scale)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var recovery=Get(shell,"RecoveryUi");var ui=Get(shell,"ReforgeUi");var matrix=Matrix.CreateScale(scale);
            Call(Get(shell,"renderer"),"Prepare",state,width,height,scale);Call(shell,"PrepareMisc",true,matrix,new Vector2(width,height),false);
        }
        internal static object[] Parts(object ui){return ((IEnumerable)Get(ui,"Parts")).Cast<object>().ToArray();}
        private static float X(object part){return (float)Get(Get(Get(part,"Element"),"Rect"),"X");}
        private static float Y(object part){return (float)Get(Get(Get(part,"Element"),"Rect"),"Y");}
        private static void DoubleClick(object context,object ui)
        {Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));}
        private static Vector2 Point(object part){var r=Get(Get(part,"Element"),"Rect");return new Vector2((float)Get(r,"X")+5,(float)Get(r,"Y")+5);}
        private static void Click(object context,object ui,object part){var p=Point(part);Step(context,ui,false,p,new KeyboardState());Step(context,ui,true,p,new KeyboardState());Step(context,ui,false,p,new KeyboardState());}
        private static void Step(object context,object ui,bool left,Vector2 point,KeyboardState keys,string text="")
        {
            Main.keyState=keys;var input=Get(context,"Input");PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            Call(input,"BeginUpdate");Call(ui,"BeforeInput",true);Call(input,"AfterMapping");PlayerInput.WritingText=false;Call(input,"AfterKeyboardRefresh");
            Main.keyCount=text.Length;for(int i=0;i<text.Length;i++){Main.keyInt[i]=text[i];Main.keyString[i]=text[i].ToString();}
            Call(ui,"ProcessInput",true,keys,point,true,true,false);
        }
        internal static void Save(ProcessingSettings settings,ProcessingOptions value){Require(settings.Set(value),"processing save admitted");NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(settings.Ready,"processing save completed");}
        // Only the OS IME service is synthetic. The production text editor,
        // character queue, key tails, command routing and file worker are real.
        private sealed class Ime : RealProxy
        {
            internal string Composition="";
            internal bool Candidates;
            internal Ime(Type contract):base(contract){}
            public override IMessage Invoke(IMessage message)
            {var call=(IMethodCallMessage)message;object value=call.MethodName=="get_Composition"?(object)Composition:call.MethodName=="get_Candidates"?(object)Candidates:null;return new ReturnMessage(value,null,0,call.LogicalCallContext,call);}
        }
        private sealed class FailingClipboard : RealProxy
        {
            internal FailingClipboard(Type contract):base(contract){}
            public override IMessage Invoke(IMessage message)
            {var call=(IMethodCallMessage)message;bool paste=call.MethodName=="TryPaste";return new ReturnMessage(false,paste?new object[]{null}:null,paste?1:0,call.LogicalCallContext,call);}
        }
    }
}
