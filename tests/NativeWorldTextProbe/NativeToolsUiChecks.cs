using System;
using System.Collections;
using System.Linq;
using System.IO;
using JueMingR.Features.Tools;
using JueMingR.Features.Processing;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsUiChecks
    {
        internal static void Run(object context,ProbeGraphics graphics)
        {
            object host=Get(context,"Tools"),shell=Get(context,"Shell"),state=Get(shell,"State"),items=Get(shell,"items"),popup=Get(shell,"CaptureUi"),renderer=Get(shell,"renderer");
            var settings=((ToolSettings[])Get(host,"Settings"))[0];Prepare(context,0,960,760,1);
            NativePageCompositionChecks.ItemsOrder(items);
            var misc=Get(shell,"MiscUi");Prepare(context,1,960,760,1);
            NativePageCompositionChecks.MiscOrder(shell);
            var panel=Get(misc,"ToolsPanel");var rows=(IEnumerable[])Get(panel,"rows");var first=rows[0].Cast<object>().First();
            Call(state,"ScrollTo",Get(Get(first,"Rect"),"Y"));Prepare(context,1,960,760,1);
            var config=Controls(misc).First(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==3);var point=Point(Get(config,"Rect"));
            foreach(bool left in new[]{false,true,false}){Mouse(point,left);Call(misc,"ProcessInput",true,new KeyboardState(),point,true,true,false);}
            Require((bool)Get(popup,"Visible"),"actual Misc configuration button opens capture window");Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            var cells=(Array)Get(popup,"cells");int saved=settings.Value.Categories;
            for(int i=0;i<8;i++)
            {
                point=Point(cells.GetValue(i));long before=settings.AcceptedCommandId;
                foreach(bool left in new[]{false,true,false}){Mouse(point,left);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);}
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
                Require(settings.AcceptedCommandId==before+1 && settings.Value.Categories==(saved^((1<<(i+1))-1)),"each real category button commits exactly once: "+i);
                Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            }
            int layouts=(int)Get(popup,"LayoutBuilds");for(int i=0;i<240;i++)Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            Require((int)Get(popup,"LayoutBuilds")==layouts,"stable popup does no repeated text/layout construction");
            point=Point(cells.GetValue(0));long fontCommand=settings.AcceptedCommandId;
            Mouse(point,true);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            graphics.SetMouseFont(Terraria.GameContent.FontAssets.ItemStack.Value);Prepare(context,1,960,760,1);
            Mouse(point,false);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            Require(fontCommand==settings.AcceptedCommandId,"font/entry reflow cannot submit an old category press");
            graphics.SetMouseFont(graphics.Font);Prepare(context,1,960,760,1);
            point=Point(cells.GetValue(0));
            foreach(bool left in new[]{false,true,false}){Mouse(point,left);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);}
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
            Require(settings.AcceptedCommandId==fontCommand+1,"fresh category click works after font reflow");Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            point=Point(cells.GetValue(0));Mouse(point,false);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            Set(state,"PointerX",point.X);Set(state,"PointerY",point.Y);Set(state,"PointerBlocked",true);
            object[] hintArgs={state,items,false,false,null,null};string hint=(string)renderer.GetType().GetMethod("ResolveHint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(renderer,hintArgs);
            Require(hint!=null && hint.Contains("鱼饵"),"popup hint remains reachable while the underlying page pointer is blocked");
            long command=settings.AcceptedCommandId;Mouse(point,true);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            graphics.SetMouseFont(graphics.Font); // New asset wrapper only must not fake a different font identity.
            Call(popup,"Close");Mouse(point,false);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            Require(command==settings.AcceptedCommandId && (bool)Get(popup,"ConsumeLeft"),"close cancels pending category and consumes the release tail");
            Require(settings.Set(settings.Value.WithCategories(saved)),"restore isolated category fixture");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
            Prepare(context,1,960,760,1);var mining=Get(shell,"MiningUi");var all=Controls(mining);
            Require(all.Count(c=>Get(c,"Command").ToString()=="Hotkey")==3,"separate mining mode and select action binding controls");
            Require(all.Where(c=>Get(c,"Command").ToString()=="Hotkey").Select(c=>(string)Get(Get(c,"Element"),"HotkeyTarget")).Distinct().OrderBy(x=>x).SequenceEqual(new[]{"tools.mining","tools.mining.select"}),"both keys route to exact shared registry IDs");
            var on=all.First(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==21);point=Point(Get(on,"Rect"));var miningSetting=((ToolSettings[])Get(host,"Settings"))[2];command=miningSetting.AcceptedCommandId;
            Mouse(point,false);Call(mining,"ProcessInput",true,point,true,true,false);Mouse(point,true);Call(mining,"ProcessInput",true,point,true,true,false);Mouse(point,false);Call(mining,"ProcessInput",true,point,false,true,false);
            Require(miningSetting.AcceptedCommandId==command,"changed native viewport cancels armed mining action before layout catches up");
            Call(shell,"CloseAndSubmitPosition");Require(!(bool)Get(popup,"Visible") && !(bool)Get(mining,"ready"),"explicit shell exit releases both G09 views");
            Console.WriteLine("PASS G09 UI: real configuration/category persistence, modal hints, close tails, distinct shared mining bindings and stale-geometry rejection.");
        }
        internal static object[] Controls(object ui){return ((IEnumerable)Get(ui,"controls")).Cast<object>().ToArray();}
        internal static void FullBindings(object context,ProbeGraphics graphics,string output)
        {
            object host=Get(context,"Tools"),shell=Get(context,"Shell"),state=Get(shell,"State"),input=Get(context,"Input"),mining=Get(shell,"MiningUi"),popup=Get(shell,"HotkeyPopup");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var names=Lang.prefix.Skip(1).Select(p=>p.Value).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();
            var reforge=((ProcessingSettings[])Get(Get(context,"Processing"),"Settings"))[2];
            NativeProcessingUiChecks.Save(reforge,new ProcessingOptions(false,names));
            NativeToolsChecks.SetMode(host,2,1);Call(shell,"CloseAndSubmitPosition");
            foreach(var size in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1440,900,150}})
            {
                Main.screenWidth=size[0];Main.screenHeight=size[1];PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=size[2]/100f;
                Require(Math.Abs(Main.UIScale-size[2]/100f)<0.001f && Main.UIScaleMatrix.M11==Main.UIScale,"requested native UI scale is actually active");
                UiFrame(context,Vector2.Zero,false);UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);
                Require((bool)Get(state,"Visible"),"real F5 opens complete G09 shell");
                Nav(context,1);var layout=Get(state,"Layout");if(size[1]==440)Require((float)Get(layout,"MaxScroll")>0,"short populated Reforge produces genuine misc scrolling");
                var view=Get(layout,"Viewport");var inside=(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale;
                for(int n=0;n<80 && (Controls(mining).Count(c=>Get(c,"Command").ToString()=="Hotkey")<3 || (float)Get(state,"Scroll")==0 && (float)Get(layout,"MaxScroll")>0);n++)
                {UiFrame(context,inside,false,new Keys[0],-120);}
                var icons=Controls(mining).Where(c=>Get(c,"Command").ToString()=="Hotkey").ToArray();
                Require(icons.Length==3 && ((float)Get(layout,"MaxScroll")==0 || (float)Get(state,"Scroll")>0),"both independent mining controls reached through actual scroll input: icons="+icons.Length+" scroll="+Get(state,"Scroll")+" max="+Get(layout,"MaxScroll"));
                var mode=Controls(mining).Single(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==21);
                var select=icons.Single(c=>Get(Get(c,"Element"),"Kind").ToString()=="Field");
                var modeRect=Get(mode,"Rect");var selectRect=Get(select,"Rect");
                Require((float)Get(selectRect,"Y")== (float)Get(modeRect,"Y") && (float)Get(selectRect,"X")==(float)Get(modeRect,"Right")+4,"select binding is immediately beside the hotkey mode button");
                Require(Get(Get(select,"Element"),"Kind").ToString()=="Field","select action is a visible binding box, not a second keyboard icon");
                graphics.Image(Path.Combine(output,"tools-full-misc-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                foreach(var tuple in new[]{Tuple.Create("tools.mining",Keys.F9,"自动挖矿"),Tuple.Create("tools.mining.select",Keys.F10,"选择挖矿区域")})
                {
                    var icon=Controls(mining).Single(c=>Get(c,"Command").ToString()=="Hotkey" && (string)Get(Get(c,"Element"),"HotkeyTarget")==tuple.Item1 && (tuple.Item1=="tools.mining" || Get(Get(c,"Element"),"Kind").ToString()=="Field"));
                    Vector2 point=Point(Get(icon,"Rect"))*Main.UIScale;Click(context,point);Click(context,point);
                    Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")==tuple.Item1,"full shell double click exact binding "+tuple.Item1+" size="+size[1]+" target="+GetOptional(popup,"Target")+" visible="+Get(popup,"Visible")+" point="+point+" view="+Get(mining,"view"));
                    var popupLayout=Get(popup,"Layout");Require(((IEnumerable)Get(popupLayout,"Text")).Cast<object>().Any(e=>((string)Get(e,"Text")).Contains(tuple.Item3)),"actual binding title matches its business action");
                    PopupClick(context,popup,"Record");Require((bool)Get(popup,"Capturing"),"actual Record click starts shared capture");
                    UiFrame(context,Vector2.Zero,false,Keys.LeftControl);UiFrame(context,Vector2.Zero,false,Keys.LeftControl,tuple.Item2);UiFrame(context,Vector2.Zero,false);
                    NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                    Require(bindings.CompletionSucceeded && bindings.Get(tuple.Item1)!=null && bindings.Get(tuple.Item1).MainKey==(int)tuple.Item2,"actual record and reliable save "+tuple.Item1+" "+Get(popup,"Status"));
                    PopupClick(context,popup,"Close");UiFrame(context,Vector2.Zero,false);
                }
                Require(((string)Get(Get(mining,"bindingDisplay"),"Text")).Contains("F10"),"saved action key is visibly displayed in the adjacent box");
                var second=Controls(mining).Single(c=>Get(Get(c,"Element"),"Kind").ToString()=="Hotkey" && (string)Get(Get(c,"Element"),"HotkeyTarget")=="tools.mining.select");
                Vector2 secondPoint=Point(Get(second,"Rect"))*Main.UIScale;Click(context,secondPoint);Click(context,secondPoint);
                Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")=="tools.mining.select","retained second row opens the same select binding after real scrolling");
                PopupClick(context,popup,"Close");UiFrame(context,Vector2.Zero,false);
                graphics.Image(Path.Combine(output,"tools-bound-misc-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                if(size[1]==760)
                {
                    var field=Controls(mining).Single(c=>Get(Get(c,"Element"),"Kind").ToString()=="Field");
                    var point=Point(Get(field,"Rect"))*Main.UIScale;UiFrame(context,point,false);
                    graphics.Image(Path.Combine(output,"tools-binding-hint.png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                    var hint=Get(Get(shell,"renderer"),"HintLayout");Require((bool)Get(hint,"Visible"),"binding hover displays necessary help can="+Get(state,"CanShowHint")+" xy="+Get(state,"PointerX")+","+Get(state,"PointerY")+" target="+point+" blocked="+Get(state,"PointerBlocked"));
                    var off=Controls(mining).Single(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==20);
                    UiFrame(context,Point(Get(off,"Rect"))*Main.UIScale,false);graphics.Image(Path.Combine(output,"tools-off-no-hint.png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                    Require(!(bool)Get(hint,"Visible"),"ordinary Off clears the prior binding hint without redundant help");
                    int fixedText=((IDictionary)Get(layout,"textSizes")).Count;
                    object fitted=Get(mining,"bindingDisplay");for(int i=0;i<30;i++)UiFrame(context,Vector2.Zero,false);
                    Require(ReferenceEquals(fitted,Get(mining,"bindingDisplay")) && ((IDictionary)Get(layout,"textSizes")).Count==fixedText,"stable binding display has no refit/allocation or fixed-cache growth");
                    Click(context,point);UiFrame(context,point,true);
                    var changedFont=Terraria.GameContent.FontAssets.ItemStack.Value;Require(!ReferenceEquals(changedFont,graphics.Font),"font identity actually changes");graphics.SetMouseFont(changedFont);UiFrame(context,point,false);UiFrame(context,Vector2.Zero,false);
                    Require(!(bool)Get(popup,"Visible") && ReferenceEquals(Get(mining,"bindingFont"),changedFont),"font replacement refits dynamic binding and cancels a stale second click");
                    graphics.SetMouseFont(graphics.Font);UiFrame(context,Vector2.Zero,false);UiFrame(context,Vector2.Zero,false);
                    Require(Controls(mining).Count(c=>Get(c,"Command").ToString()=="Hotkey")==3,"font restore retains both action controls");
                }
                Nav(context,0);Nav(context,1);UiFrame(context,Vector2.Zero,false);
                Require((float)Get(Get(state,"Layout"),"ContentHeight")> (float)Get(mining,"Height"),"page return preserves all blocks after Mining and the merchant footer");
                Call(shell,"CloseAndSubmitPosition");UiFrame(context,Vector2.Zero,false);
            }
            NativeProcessingUiChecks.Save(reforge,new ProcessingOptions(false,new string[0]));
            Main.screenWidth=960;Main.screenHeight=760;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();
            UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);Nav(context,1);
            Require((float)Get(state,"Scroll")<=(float)Get(Get(state,"Layout"),"MaxScroll") && Controls(mining).Count(c=>Get(c,"Command").ToString()=="Hotkey")==3,"removing Reforge cards clamps scroll and leaves both bindings reachable");
            Call(shell,"CloseAndSubmitPosition");UiFrame(context,Vector2.Zero,false);
            DispatchBindings(context);
            Console.WriteLine("PASS G09 full F5: populated Reforge scroll, adjacent action binding box and independent toggle, real double-click/Record/save paths, short and actual 150% windows, font replacement, Off hint clearing, stable cache, page return, reopen and content shrink.");
        }
        internal static void DispatchBindings(object context)
        {
            var host=Get(context,"Tools");var p=Main.LocalPlayer;var mining=Get(host,"Mining");
            Main.screenPosition=Vector2.Zero;
            NativeToolsChecks.SetMode(host,2,1);p.inventory[0].SetDefaults(2176);p.itemAnimation=p.itemTime=0;p.selectedItemState.Select(0);p.selectedItemState.Update();p.position=new Vector2(640,640);
            for(int x=35;x<55;x++)for(int y=35;y<48;y++)Main.tile[x,y].ClearEverything();NativeToolsChecks.Tile(42,40,6);
            var aim=new Vector2(42*16+8,40*16+8);UiFrame(context,aim,false);
            UiFrame(context,aim,false,Keys.LeftControl,Keys.F10);UiFrame(context,aim,false);
            Require(((MiningRegion)Get(mining,"Region")).Count==1 && (int)Call(host,"Mode",2)==1,"actual selection key only selects, leaving mode unchanged region="+((MiningRegion)Get(mining,"Region")).Count+" mode="+Call(host,"Mode",2)+" input="+Get(Get(context,"Input"),"CanStartActions")+" retain="+Call(host,"CanRetainUse",p,false)+" mouse="+p.mouseInterface+" inventory="+Main.playerInventory+" manual="+Get(Get(Get(host,"Items"),"World"),"HasManualOperation")+" aim="+Main.MouseWorld);
            UiFrame(context,aim,false,Keys.LeftControl,Keys.F9);UiFrame(context,aim,false);
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",2);});
            Require((int)Call(host,"Mode",2)==0,"actual main key turns mode off");
            UiFrame(context,aim,false,Keys.LeftControl,Keys.F10);UiFrame(context,aim,false);
            Require(((MiningRegion)Get(mining,"Region")).Count==0 && (int)Call(host,"Mode",2)==0,"selection binding stays configured but does not act while off");
            UiFrame(context,aim,false,Keys.LeftControl,Keys.F9);UiFrame(context,aim,false);
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",2);});
            Require((int)Call(host,"Mode",2)==1 && ((MiningRegion)Get(mining,"Region")).Count==0,"main key restores previous mode without selecting a vein");
            NativeToolsChecks.SetMode(host,2,0);
        }
        internal static void Nav(object context,int page)
        {var state=Get(Get(context,"Shell"),"State");Vector2 point=(Point(Call(Get(state,"Layout"),"Navigation",page))+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale;Click(context,point);Require((int)Get(state,"Page")==page,"real navigation click "+page);}
        internal static void PopupClick(object context,object popup,string command)
        {
            var layout=Get(popup,"Layout");var commands=((IEnumerable)Get(layout,"Commands")).Cast<object>().ToArray();var buttons=((IEnumerable)Get(layout,"Buttons")).Cast<object>().ToArray();
            int index=Array.FindIndex(commands,c=>c.ToString()==command);Require(index>=0,"popup command exists: "+command);
            Vector2 point=(Point(Get(buttons[index],"Rect"))+new Vector2((float)Get(Get(layout,"Panel"),"X"),(float)Get(Get(layout,"Panel"),"Y")))*Main.UIScale;Click(context,point);
        }
        internal static void Click(object context,Vector2 point){UiFrame(context,point,false);UiFrame(context,point,true);UiFrame(context,point,false);}
        internal static void UiFrame(object context,Vector2 point,bool left,params Keys[] keys){UiFrame(context,point,left,keys,0);}
        internal static void UiFrame(object context,Vector2 point,bool left,Keys[] keys,int wheel)
        {
            var shell=Get(context,"Shell");var input=Get(context,"Input");Call(input,"BeginUpdate");Call(shell,"BeforeInput");
            PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,PlayerInput.MouseInfo.ScrollWheelValue+wheel,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            PlayerInput.ScrollWheelDelta=PlayerInput.ScrollWheelDeltaForUI=wheel;
            PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.MouseLeft=left;PlayerInput.Triggers.Update();Main.mouseLeft=left;Main.mouseX=(int)point.X;Main.mouseY=(int)point.Y;
            Call(input,"AfterMapping");Main.keyState=new KeyboardState(keys);Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");Call(context,"UpdateRuntime");Call(context,"UpdateShell");
            Require(!(bool)Get(shell,"Failed"),"complete input/runtime/layout frame remains healthy");
        }
        internal static Vector2 Point(object rect){return new Vector2((float)Get(rect,"X")+5,(float)Get(rect,"Y")+5);}
        internal static void Mouse(Vector2 point,bool left){PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);}
        internal static void Prepare(object context,int page,int width,int height,float scale)
        {
            Main.screenWidth=width;Main.screenHeight=height;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=scale;
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer");var matrix=Matrix.CreateScale(scale);
            if((int)Get(state,"Page")!=page)Call(state,"Navigate",page);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");Call(renderer,"Prepare",state,(float)width,(float)height,scale);
            if(page==0)Call(Get(shell,"items"),"Prepare",true,matrix,new Vector2(width,height));
            else
            {
                Call(shell,"PrepareMisc",true,matrix,new Vector2(width,height),true);
            }
        }
    }
}
