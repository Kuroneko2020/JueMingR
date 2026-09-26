using System;
using System.Collections;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Features.Processing;
using JueMingR.Features.Text;
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
            Require(Parts(ui).Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="自动重铸"),"reforge uses existing Misc page");
            float height=(float)Get(layout,"ContentHeight");
            Save(settings,new ProcessingOptions(true));prepare();Require(height==(float)Get(layout,"ContentHeight"),"toggle does not add waiting/status rows");
            long builds=(long)Get(ui,"LayoutBuilds"),measures=(long)Get(ui,"EditMeasurements");for(int i=0;i<120;i++)prepare();
            Require(builds==(long)Get(ui,"LayoutBuilds") && measures==(long)Get(ui,"EditMeasurements"),"stable reforge page reuses geometry and edit measurement");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.W,Keys.Space));
            Require(!Main.blockInput && !PlayerInput.WritingText && Main.keyState.IsKeyDown(Keys.Space),"plain list leaves movement keyboard alone");
            var field=Parts(ui).First(p=>(int)Get(p,"Command")==1);
            var add=Parts(ui).First(p=>(int)Get(p,"Command")==2);
            var on=Parts(ui).First(p=>(int)Get(p,"Command")==4);
            Require(Y(field)==Y(add) && Y(field)==Y(on),"input, add and toggles share one row");
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
            ime.Composition="ni";Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
            Require(settings.Value.Names.Count==0 && GetOptional(ui,"Editor")!=null,"composition Enter cannot add unfinished name");
            ime.Composition="";Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Step(context,ui,false,new Vector2(-10,-10),new KeyboardState(Keys.Enter),"\r");
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});
            Require(settings.Value.Names.SequenceEqual(new[]{name}),"fresh Enter persists one complete target");
            Step(context,ui,false,new Vector2(-10,-10),new KeyboardState());Require(!Main.blockInput && !PlayerInput.WritingText,"editor completion releases keyboard tail");prepare();
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
            prepare=()=>Prepare(context,960,440,1);
            var names=Lang.prefix.Skip(1).Select(p=>p.Value).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();Save(settings,new ProcessingOptions(false,names));prepare();
            Call(state,"ScrollTo",100000f);prepare();float bottom=(float)Get(state,"Scroll");Require(bottom>0,"full list owns shared Misc scroll height");
            for(int i=0;i<20;i++)prepare();Require(bottom==(float)Get(state,"Scroll"),"tax preparation cannot reset reforge list scroll");
            remove=Parts(ui).Last(p=>(int)Get(p,"Command")==3);string identity=(string)Get(remove,"Name");Click(context,ui,remove);
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(!settings.Value.Names.Contains(identity) && settings.Value.Names.Count==names.Length-1,"visible bottom removes exact whole-name identity");
            Call(state,"ScrollTo",0f);prepare();
            // A mouse-down in one layout must not delete after a scale reflow.
            remove=Parts(ui).First(p=>(int)Get(p,"Command")==3);point=Point(remove);int count=settings.Value.Names.Count;
            Step(context,ui,true,point,new KeyboardState());Prepare(context,1280,720,1.5f);Step(context,ui,false,point,new KeyboardState());
            Require(count==settings.Value.Names.Count,"scale change invalidates armed corner delete");
            prepare();Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));
            Call(ui,"ProcessInput",true,new KeyboardState(),point,true,false,false);prepare();
            Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));
            Require(GetOptional(ui,"Editor")==null,"focus loss breaks a pending double click");
            Click(context,ui,Parts(ui).First(p=>(int)Get(p,"Command")==1));prepare();
            Require(GetOptional(ui,"Editor")!=null,"fresh same-field double click works after focus return");
            Call(ui,"ProcessInput",true,new KeyboardState(),point,true,true,true);prepare();
            Require(GetOptional(ui,"Editor")==null && !(bool)Get(ui,"OwnsTextToken"),"higher popup yields editor and keyboard ownership");
            Call(state,"Close");Call(ui,"Suspend");Save(settings,new ProcessingOptions());
            Console.WriteLine("PASS G08 UI: physical editor/list controls, isolated IME boundary, whole-name persistence, stable height/measurement, movement keys and shared scroll.");
        }
        internal static void Prepare(object context,float width,float height,float scale)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var recovery=Get(shell,"RecoveryUi");var ui=Get(shell,"ReforgeUi");var matrix=Matrix.CreateScale(scale);
            Call(Get(shell,"renderer"),"Prepare",state,width,height,scale);Call(recovery,"PrepareLayout",matrix);
            float bottom=(float)Get(recovery,"ContentBottom");Call(ui,"PrepareLayout",matrix,bottom);Call(Get(state,"Layout"),"SetRecoveryContentHeight",Get(ui,"Height"));Call(state,"ClampScroll");Call(recovery,"PrepareLayout",matrix);Call(ui,"PrepareLayout",matrix,bottom);
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
            internal Ime(Type contract):base(contract){}
            public override IMessage Invoke(IMessage message)
            {var call=(IMethodCallMessage)message;object value=call.MethodName=="get_Composition"?(object)Composition:call.MethodName=="get_Candidates"?(object)false:null;return new ReturnMessage(value,null,0,call.LogicalCallContext,call);}
        }
    }
}
