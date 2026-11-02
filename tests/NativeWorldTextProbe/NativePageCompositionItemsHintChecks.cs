using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The historical source-linked fixture remains an independent obligation.
    // This adapter checks its affected UI contracts against the compiled Host
    // and original Terraria already initialized by PageCompositionCpu.
    internal static class NativeItemsHintChecks
    {
        internal static void Run(object context)
        {
            var check=new Consumer(Get(context,"Shell"));
            var original=check.Value;var inventory=(Item[])Main.LocalPlayer.inventory.Clone();
            try
            {
                for(int i=0;i<22;i++){var item=new Item();item.SetDefaults(3000+i);Main.LocalPlayer.inventory[i]=item;}
                check.Open();check.Groups();check.CardsAndSelection();check.ScrollAndVisibility();check.OrdinaryBindings();
                Console.WriteLine("PASS native Items final hints, cross ownership/release tail, selector commit boundaries, full decorative groups, clipping/visibility and ordinary/action binding purposes; no GPU or synthetic receiver.");
            }
            finally
            {
                Call(check.Page,"Suspend");Array.Copy(inventory,Main.LocalPlayer.inventory,inventory.Length);
                check.Change(original);Call(check.Shell,"CloseAndSubmitPosition");
            }
        }
        private sealed class Consumer
        {
            internal readonly object Shell,Page;
            private readonly object state,renderer,host,layout,selection;
            private float height=1200;
            private const float Width=1200;
            internal Consumer(object shell)
            {
                Shell=shell;Page=Get(shell,"items");state=Get(shell,"State");renderer=Get(shell,"renderer");
                host=Get(Page,"host");layout=Get(Page,"layout");selection=Get(Page,"selection");
            }
            internal ItemAutomationSettings Value {get{return (ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");}}
            private long Revision {get{return (long)Get(Get(host,"Preferences"),"Revision");}}
            private object View {get{return Get(Page,"view");}}
            private static float F(object value,string field){return (float)Get(value,field);}
            private static object R(object value){return Get(value,"Rect");}
            private static string Command(object control){return Get(control,"Command").ToString();}
            private object[] Controls {get{return ((IEnumerable)Get(Page,"controls")).Cast<object>().ToArray();}}
            private object Control(string command,ItemListKind list,int type=0)
            {return Controls.Single(c=>Command(c)==command && (int)Get(c,"Argument")== (int)list && (type==0 || (int)Get(c,"Type")==type));}
            private object[] Panels {get{return ((IEnumerable)Get(layout,"rows")).Cast<object>().Where(e=>Get(e,"Kind").ToString()=="Panel").ToArray();}}
            private object Rect(float x,float y,float width,float h)
            {return Activator.CreateInstance(Get(Get(state,"Layout"),"Viewport").GetType(),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{x,y,width,h},null);}
            internal void Change(ItemAutomationSettings value)
            {
                if(!Value.Equals(value))Require((bool)Call(host,"Change",value),"native Items isolated settings command accepted");
                Call(host,"PollPreferences");
            }
            internal void Open()
            {
                Set(state,"Ready",true);Call(state,"Navigate",0);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
                Sample(-10,-10);Prepare();Require((bool)Get(host,"ControlsEnabled"),"real Items controls are available in the native session");
            }
            private void Prepare()
            {Call(renderer,"Prepare",state,Width,height,1f);Call(Page,"PrepareLayout",Matrix.Identity,new Vector2(Width,height));}
            private void Scroll(float y){Call(state,"ScrollTo",y);Prepare();}
            private void Sample(float x,float y,bool left=false,bool focused=true)
            {
                var input=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
                foreach(var pair in new[]{Tuple.Create("Width",(object)Width),Tuple.Create("Height",(object)height),Tuple.Create("Scale",(object)1f),Tuple.Create("X",(object)x),Tuple.Create("Y",(object)y),Tuple.Create("Active",(object)true),Tuple.Create("Focused",(object)focused),Tuple.Create("Left",(object)left)})Set(input,pair.Item1,pair.Item2);
                Call(state,"Update",input);
            }
            private void Pointer(float x,float y,bool left)
            {
                PlayerInput.MouseInfo=new MouseState((int)x,(int)y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                // The shell and child consume the same physical sample. No
                // direct Execute shortcut is used for the remove/selection proof.
                Sample(x,y,left);Call(Page,"ProcessInput",true,new KeyboardState(),new Vector2(x,y),true,true,false);
            }
            private void Click(object control)
            {
                var r=R(control);float x=F(r,"X")+2,y=F(r,"Y")+2;
                Pointer(x,y,false);Pointer(x,y,true);Pointer(x,y,false);Prepare();Sample(x,y);
            }
            private string Resolve(out object target,object clip=null,bool blocked=false)
            {
                var args=new object[]{state,Page,blocked,false,null,clip??View};
                string text=(string)Call(renderer,"ResolveHint",args);target=args[4];return text;
            }
            private void Final(string expected,object clip=null,bool blocked=false)
            {
                object target;Require(Resolve(out target,clip,blocked)==expected,"actual ResolveHint matches the current target: "+expected);
                Call(renderer,"PrepareHints",state,Page,blocked,false,Rect(8,8,Width-16,height-16),clip??View,Get(renderer,"font"),Get(renderer,"hintMeasure"));
                var hint=Get(renderer,"HintLayout");Require((bool)Get(hint,"Visible")== (expected!=null),"final hint visibility agrees with resolver");
                if(expected==null)return;
                string text=string.Concat(((IEnumerable)Get(hint,"Lines")).Cast<object>().Select(line=>(string)Get(line,"Text")));
                Require(text==expected,"final wrapped lines preserve the exact contextual content");
                var panel=Get(hint,"Panel");Require(F(panel,"X")>=8 && F(panel,"Y")>=8 && F(panel,"Right")<=Width-8 && F(panel,"Bottom")<=height-8,"final hint lies inside permitted screen bounds");
            }
            private void CardHint(object control,string suffix)
            {
                var r=R(control);float x=Math.Max(F(r,"X"),F(View,"X"))+1,y=Math.Min(F(r,"Bottom"),F(View,"Bottom"))-1;
                Sample(x,y);long revision=Revision;int count=(int)Get(selection,"Count"),builds=(int)Get(Page,"LayoutBuildCount");
                string text=Lang.GetItemNameValue((int)Get(control,"Type"))+suffix;Final(text);
                int hintBuilds=(int)Get(Get(renderer,"HintLayout"),"BuildCount");
                for(int i=0;i<8;i++){Sample(x+i*.01f,y);Final(text);}
                Require(Revision==revision && count==(int)Get(selection,"Count") && builds==(int)Get(Page,"LayoutBuildCount") &&
                    hintBuilds==(int)Get(Get(renderer,"HintLayout"),"BuildCount"),"hover neither submits settings nor changes selection, reflows Items or rewraps identical help");
            }
            private static bool Contains(object outer,object inner)
            {return F(inner,"X")>=F(outer,"X") && F(inner,"Right")<=F(outer,"Right") && F(inner,"Y")>=F(outer,"Y") && F(inner,"Bottom")<=F(outer,"Bottom");}
            private object[] GroupGeometry(int count)
            {
                var empty=new ItemAutomationSettings(false,false,false,new int[0],new int[0]);
                Change(empty.WithTypes(ItemListKind.Sell,Enumerable.Range(1000,count)).WithTypes(ItemListKind.Discard,Enumerable.Range(2000,count)));Scroll(0);
                var groups=Panels;Require(groups.Length==3 && groups.All(p=>Get(p,"Command").ToString()=="None"),"one command-free full logical group per Items feature");
                var dividers=((IEnumerable)Get(layout,"rows")).Cast<object>().Where(e=>Get(e,"Kind").ToString()=="Divider").ToArray();
                Require(dividers.Length==(count>0?2:0),"only populated Sell/Discard groups have a header divider");
                for(int i=0;i<2;i++)
                {
                    var panel=R(groups[i+1]);
                    var cards=Controls.Where(c=>Command(c)=="Replace" && (int)Get(c,"Argument")==i).ToArray();
                    Require(cards.Length==count,"representative native list is fully projected for group assertions");
                    if(count>0)
                    {
                        float dy=F(View,"Y")-(float)Get(state,"Scroll");
                        Require(cards.All(c=>Contains(Call(panel,"Offset",F(View,"X"),dy),R(c))),"single/multirow cards remain wholly inside their owner");
                        Require(Math.Abs(F(panel,"Bottom")+dy-cards.Max(c=>F(R(c),"Bottom"))-6)<.01f,"list group has exactly its bottom inset, without a trailing grid stride");
                        var divider=dividers.Single(e=>F(R(e),"Y")>F(panel,"Y") && F(R(e),"Bottom")<F(panel,"Bottom"));var line=R(divider);
                        Require(F(line,"Height")==1 && F(line,"X")==F(panel,"X")+12 && F(line,"Right")==F(panel,"Right")-12 &&
                            Math.Abs(cards.Min(c=>F(R(c),"Y"))-dy-F(line,"Bottom")-2.5f)<.01f && Get(divider,"Command").ToString()=="None" && GetOptional(divider,"Description")==null,
                            "one softly rendered inset divider uses the existing measured header/body gap");
                        float lx=F(line,"X")+F(View,"X")+1,ly=F(line,"Y")+dy+.5f;long lineRevision=Revision;
                        Require(!Controls.Any(c=>(bool)Call(R(c),"Contains",lx,ly)),"divider never becomes an interactive control");
                        Pointer(lx,ly,false);Pointer(lx,ly,true);Pointer(lx,ly,false);Prepare();Sample(lx,ly);Final(null);
                        Require(Revision==lineRevision && !(bool)Get(Page,"Selecting"),"clicking the real projected divider cannot toggle or open content");
                    }
                    var projected=Call(panel,"Offset",F(View,"X"),F(View,"Y")-(float)Get(state,"Scroll"));
                    float x=F(projected,"Right")-1,y=F(projected,"Bottom")-1;long revision=Revision;
                    Require(!Controls.Any(c=>(bool)Call(R(c),"Contains",x,y)),"decorative group padding has no command hit target");
                    Pointer(x,y,false);Pointer(x,y,true);Pointer(x,y,false);Prepare();Sample(x,y);Final(null);
                    Require(Revision==revision && !(bool)Get(Page,"Selecting"),"clicking group padding cannot issue a command or open a selector");
                }
                Require(F(R(groups[1]),"Bottom")+6==F(R(groups[2]),"Y"),"the next group starts outside the previous complete border");
                return groups;
            }
            internal void Groups()
            {
                var compact=GroupGeometry(0).Select(p=>F(R(p),"Height")).ToArray();
                foreach(int count in new[]{1,21})
                {
                    var groups=GroupGeometry(count);
                    Require(F(R(groups[0]),"Height")==compact[0] && F(R(groups[1]),"Height")>compact[1] && F(R(groups[2]),"Height")>compact[2],"only populated list owners grow beyond their compact headers");
                }
                var restored=GroupGeometry(0);Require(restored.Select(p=>F(R(p),"Height")).SequenceEqual(compact),"empty lists return to their compact measured groups");
                foreach(ItemListKind list in new[]{ItemListKind.Sell,ItemListKind.Discard})
                {
                    var before=Panels;float h=F(R(before[(int)list+1]),"Height");
                    Click(Control("Add",list));var expanded=Panels;
                    Require(F(R(expanded[(int)list+1]),"Height")>h,"opening the selector expands its owning complete group");
                    var editorDividers=((IEnumerable)Get(layout,"rows")).Cast<object>().Where(e=>Get(e,"Kind").ToString()=="Divider").ToArray();
                    Require(editorDividers.Length==1 && F(R(editorDividers[0]),"Bottom")<F(Get(layout,"Header"),"Y"),"an expanded selector owns one divider before its title without another frame");
                    if(list==ItemListKind.Sell)Require(F(R(expanded[1]),"Bottom")+6==F(R(expanded[2]),"Y"),"expanded sale selector pushes the next group outside");
                    var owner=Call(R(expanded[(int)list+1]),"Offset",F(View,"X"),F(View,"Y")-(float)Get(state,"Scroll"));
                    Require(Controls.Where(c=>Command(c)=="Select" && (int)Get(c,"Argument")== (int)list).All(c=>Contains(owner,R(c))),"selector candidates remain in their owner group");
                    Click(Control("Cancel",list));Require(F(R(Panels[(int)list+1]),"Height")==h,"cancelling selector restores original compact height");
                    Require(!((IEnumerable)Get(layout,"rows")).Cast<object>().Any(e=>Get(e,"Kind").ToString()=="Divider"),"cancelled empty selector leaves no divider behind");
                }
            }
            internal void CardsAndSelection()
            {
                foreach(ItemListKind list in new[]{ItemListKind.Sell,ItemListKind.Discard})
                {
                    Change(new ItemAutomationSettings(false,false,false,new int[0],new int[0]).WithTypes(list,Enumerable.Range(1000,20)));Scroll(0);
                    var cards=Controls.Where(c=>Command(c)=="Replace" && (int)Get(c,"Argument")== (int)list).OrderBy(c=>F(R(c),"Y")).ThenBy(c=>F(R(c),"X")).ToArray();
                    var first=cards.Where(c=>F(R(c),"Y")==F(R(cards[0]),"Y")).ToArray();
                    Require(first.Length>=3 && cards.Length>first.Length,"actual source projection spans columns and a second row");
                    foreach(var card in new[]{first[0],first[first.Length/2],first.Last(),cards[first.Length]})
                    {CardHint(card,"·点击替换");CardHint(Control("Remove",list,(int)Get(card,"Type")),"·点击移除");}
                    var cross=Control("Remove",list,1000);var r=R(cross);float x=F(r,"X")+2,y=F(r,"Y")+2;
                    Sample(x,y);Final(Lang.GetItemNameValue(1000)+"·点击移除");long revision=Revision;
                    Click(cross);Require((bool)Get(Page,"ConsumeLeft") && Revision==revision+1 && !(bool)Get(Page,"Selecting") && !Types(list).Contains(1000),"cross wins overlapping body and removes exactly once");
                    Pointer(x,y,false);Prepare();Require(Revision==revision+1 && Types(list).Contains(1001) && !(bool)Get(Page,"Selecting") && (bool)Get(Page,"ConsumeLeft")==false,"extra physical release never becomes a shifted-card replacement");
                    Click(Control("Add",list));var candidate=Control("Select",list,3000);CardHint(candidate,"·点击选择或取消，确定后添加");
                    revision=Revision;Click(candidate);Require(Revision==revision && (int)Get(selection,"Count")==1 && !Types(list).Contains(3000),"add candidate selection remains an unsaved draft");
                    CardHint(Control("Select",list,3000),"·点击选择或取消，确定后添加");Click(Control("Confirm",list));
                    Require(Types(list).Contains(3000) && Revision==revision+1 && !(bool)Get(Page,"Selecting"),"only confirm commits the added type");
                    Click(Control("Replace",list,1001));CardHint(Control("Select",list,3001),"·点击立即替换");revision=Revision;
                    Click(Control("Select",list,3001));Require(Types(list).Contains(3001) && !Types(list).Contains(1001) && Revision==revision+1 && !(bool)Get(Page,"Selecting"),"replacement candidate commits immediately without a confirmation step");
                }
            }
            private int[] Types(ItemListKind list){return (list==ItemListKind.Sell?Value.SellTypes:Value.DiscardTypes).ToArray();}
            internal void ScrollAndVisibility()
            {
                Change(new ItemAutomationSettings(false,false,false,Enumerable.Range(1000,200),new int[0]));Scroll(0);
                var logicalDivider=((IEnumerable)Get(layout,"rows")).Cast<object>().Single(e=>Get(e,"Kind").ToString()=="Divider");
                Scroll(F(R(logicalDivider),"Y")+.5f);
                var projectedDivider=((IEnumerable)Get(Page,"elements")).Cast<object>().Single(e=>Get(e,"Kind").ToString()=="Divider");
                Require(F(R(projectedDivider),"Y")==F(View,"Y")-.5f && F(R(projectedDivider),"Height")==1 && F(R(projectedDivider),"Width")==F(R(logicalDivider),"Width"),
                    "partly clipped divider keeps its full logical rectangle for the original scissor");
                Scroll(0);
                var first=Controls.First(c=>Command(c)=="Replace");float y=F(R(first),"Y")-F(View,"Y")+6;Scroll(y);
                var card=Controls.First(c=>Command(c)=="Replace" && F(R(c),"Y")<F(View,"Y"));
                var r=R(card);float x=F(r,"X")+2,py=F(View,"Y")+2;int old=(int)Get(card,"Type");
                var clip=Rect(F(View,"X"),F(View,"Y")+1,F(View,"Width"),F(View,"Height")-1);Sample(x,py);
                Final(Lang.GetItemNameValue(old)+"·点击替换",clip);object target;Resolve(out target,clip);
                Require(F(target,"Y")==F(clip,"Y") && F(target,"Height")<F(r,"Height"),"partly visible card reports its final clipped target");
                Sample(x,F(View,"Y"));Final(null,clip);Sample(x,py);
                Call(state,"ScrollTo",(float)Get(state,"Scroll")+38f);Final(null);Prepare();
                var next=Controls.First(c=>Command(c)=="Replace" && (bool)Call(R(c),"Contains",x,py));
                Require((int)Get(next,"Type")!=old,"stationary pointer maps to a new real projected item after scrolling");Final(Lang.GetItemNameValue((int)Get(next,"Type"))+"·点击替换");
                Final(null,null,true);Final(Lang.GetItemNameValue((int)Get(next,"Type"))+"·点击替换");
                Sample(F(View,"Right")+1,py);Final(null);
                Sample(x,py,false,false);Final(null);Sample(-10,-10);Call(state,"RestoreVisible");Prepare();
                Sample(x,py);Call(state,"Close");Final(null);Call(state,"RestoreVisible");Prepare();
                height=300;Sample(-10,-10);Prepare();Scroll(y);
                var panel=Panels[1];var projected=((IEnumerable)Get(Page,"elements")).Cast<object>().Single(e=>Get(e,"Kind").ToString()=="Panel" && F(R(e),"Y")<F(View,"Y"));
                Require(F(R(projected),"Height")==F(R(panel),"Height") && F(R(projected),"Bottom")>F(View,"Bottom"),"short viewport retains the full group through both clipping edges");
                height=1200;Sample(-10,-10);Prepare();Scroll(0);
            }
            internal void OrdinaryBindings()
            {
                Call(state,"Navigate",9);Sample(-10,-10);Call(renderer,"Prepare",state,Width,height,1f);
                var shared=Get(state,"Layout");var elements=((IEnumerable)Get(shared,"Elements")).Cast<object>().ToArray();
                foreach(bool action in new[]{false,true})
                {
                    var key=elements.First(e=>Get(e,"Kind").ToString()=="Hotkey" && GetOptional(e,"HotkeyTarget")!=null &&
                        (((string)Get(e,"HotkeyTarget")=="information-window.adjust")==action));
                    Call(state,"ScrollTo",F(R(key),"Y"));
                    var view=Call(Get(shared,"Viewport"),"Offset",Get(state,"X"),Get(state,"Y"));var r=Call(R(key),"Offset",F(view,"X"),F(view,"Y")-(float)Get(state,"Scroll"));
                    Sample(F(r,"X")+2,F(r,"Y")+2);Final(action?"双击设置调整信息窗位置的快捷键":"双击设置功能开关快捷键",view);
                }
            }
        }
    }
}
