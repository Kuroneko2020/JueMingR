using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinUiChecks
    {
        internal static void Run(object context, ProbeGraphics graphics = null, string output = null)
        {
            if (graphics != null) Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            object host=Get(context,"CoinDeposit"),shell=Get(context,"Shell"),state=Get(shell,"State"),page=Get(shell,"items"),renderer=Get(shell,"renderer");
            var settings=(CoinSettings)Get(host,"Settings");
            Set(state,"Ready",true);Call(state,"Navigate",0);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action<int,int,float> prepare=(width,height,scale)=>{Call(renderer,"Prepare",state,(float)width,(float)height,scale);Call(page,"PrepareLayout",Matrix.CreateScale(scale),new Vector2(width,height));};
            prepare(960,760,1);
            object panel=Get(page,"CoinPanel");
            var rows=((IEnumerable)Get(panel,"rows")).Cast<object>().ToArray();
            var name=rows.First(row=>(string)GetOptional(row,"Text")=="自动存钱");
            Call(state,"ScrollTo",Math.Max(0,(float)Get(Get(name,"Rect"),"Y")-16));prepare(960,760,1);
            Click(page,Find(page,"Coin",settings.Enabled?"关闭":"开启"));
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});prepare(960,760,1);
            bool first=settings.Enabled;
            Click(page,Find(page,"Coin",first?"关闭":"开启"));
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});prepare(960,760,1);
            Require(settings.Enabled!=first,"real F5 coin pointer toggle commits independent preference");
            var key=Controls(page).First(control=>Get(control,"Command").ToString()=="Hotkey" && (string)Get(Get(control,"Element"),"HotkeyTarget")=="items.coin-deposit.toggle");
            Click(page,key);Click(page,key);
            Require((bool)Get(Get(shell,"HotkeyPopup"),"Visible"),"coin row enters the existing public binding window");Call(Get(shell,"HotkeyPopup"),"Close");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            NativeQuickGestureChecks.Bind(bindings,"items.coin-deposit.toggle","LeftControl+F9");
            object input=Get(context,"Input");
            Action press=()=>{NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");NativeQuickItemChecks.Sample(input,new[]{Keys.LeftControl,Keys.F9});Call(shell,"ProcessInput");};
            bool beforeKey=settings.Enabled;Call(state,"Close");press();
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
            Require(settings.Enabled!=beforeKey,"real public toggle binding commits the same preference as the F5 row");
            bool afterKey=settings.Enabled;Main.drawingPlayerChat=true;press();Main.drawingPlayerChat=false;
            Require(settings.Enabled==afterKey && !settings.Busy,"coin toggle does not penetrate native text entry");
            Call(state,"RestoreVisible");press();
            Require(settings.Enabled==afterKey && !settings.Busy,"gameplay coin toggle does not penetrate visible F5 controls");
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");
            if(settings.Enabled!=beforeKey){Require(settings.Set(beforeKey),"restore isolated toggle");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});}
            prepare(960,760,1);int layouts=(int)Get(page,"LayoutBuildCount");
            for(int i=0;i<240;i++)prepare(960,760,1);
            Require((int)Get(page,"LayoutBuildCount")==layouts,"stable coin status does not rebuild UI text/layout each frame");
            if(graphics!=null)
            {
                NativeCoinMatrix.Reset(Main.LocalPlayer,host);Main.tile[40,40].type=29;
                Main.LocalPlayer.bank.item[0]=NativeCoinChecks.Coin(72,7);Main.LocalPlayer.chest=-2;
                Terraria.UI.ItemSlot.PickupItemIntoMouse(Main.LocalPlayer.bank.item,4,0,Main.LocalPlayer);
                Main.LocalPlayer.chest=-1;Call(host,"Update",0UL);
                Require(Get(host,"Status").ToString()=="取出保护中","visual status comes from actual native withdrawal");
            }
            foreach(var shape in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1280,720,150}})
            {
                float scale=shape[2]/100f;prepare(shape[0],shape[1],scale);
                object view=Get(page,"view");
                foreach(var c in Controls(page).Where(c=>Get(c,"Command").ToString()=="Coin"))
                {object r=Get(c,"Rect");Require((float)Get(r,"X")>=(float)Get(view,"X") && (float)Get(r,"Right")<=(float)Get(view,"Right"),"coin controls fit actual viewport width");}
                if(graphics!=null)
                {
                    Directory.CreateDirectory(output);
                    Hover(state,shape[0],shape[1],scale,0,0);prepare(shape[0],shape[1],scale);
                    graphics.LoadItemTextures(((IEnumerable)Get(Get(page,"QuickPanel"),"visibleTypes")).Cast<int>());
                    Call(page,"Prepare",true,Matrix.CreateScale(scale),new Vector2(shape[0],shape[1]));
                    graphics.Image(Path.Combine(output,"coin-controls-"+shape[0]+"x"+shape[1]+"-"+shape[2]+".png"),()=>
                    {Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);},Matrix.CreateScale(scale),shape[0],shape[1]);
                    foreach(string hintKind in new[]{"name","protection"})
                    {
                        object target=hintKind=="name"?Get(((IEnumerable)Get(page,"elements")).Cast<object>().First(e=>(string)GetOptional(e,"Text")=="自动存钱"),"HintRect"):Get(panel,"projectedStatus");
                        Hover(state,shape[0],shape[1],scale,(float)Get(target,"X")+2,(float)Get(target,"Y")+2);
                        typeof(PlayerInput).GetField("_originalScreenWidth",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[0]);
                        typeof(PlayerInput).GetField("_originalScreenHeight",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[1]);
                        graphics.Image(Path.Combine(output,"coin-"+hintKind+"-"+shape[0]+"x"+shape[1]+"-"+shape[2]+".png"),()=>
                        {Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);Call(renderer,"DrawHints",state,Matrix.CreateScale(scale),page,false,false);},Matrix.CreateScale(scale),shape[0],shape[1]);
                        Require((bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual shared coin hint rendered: "+hintKind);
                    }
                }
            }
            Call(page,"Suspend");Call(state,"Close");
            Console.WriteLine("PASS: G06 real F5 toggles/public key window, three viewport shapes and stable layout."+(graphics==null?"":" Original-resource screenshots written."));
        }
        private static object[] Controls(object page){return ((IEnumerable)Get(page,"controls")).Cast<object>().ToArray();}
        private static void Hover(object state,int width,int height,float scale,float x,float y)
        {
            object input=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            Set(input,"Width",(float)width);Set(input,"Height",(float)height);Set(input,"Scale",scale);Set(input,"X",x);Set(input,"Y",y);
            Set(input,"Active",true);Set(input,"Focused",true);Call(state,"Update",input);
        }
        private static object Find(object page,string command,string text){return Controls(page).First(c=>Get(c,"Command").ToString()==command && (string)Get(Get(c,"Element"),"Text")==text);}
        private static void Click(object page,object control)
        {object r=Get(control,"Rect");var p=new Vector2((float)Get(r,"X")+8,(float)Get(r,"Y")+8);foreach(ButtonState left in new[]{ButtonState.Released,ButtonState.Pressed,ButtonState.Released}){PlayerInput.MouseInfo=new MouseState((int)p.X,(int)p.Y,0,left,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(page,"ProcessInput",true,new KeyboardState(),p,true,true,false);}}
    }
}
