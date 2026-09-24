using System;
using System.Collections;
using System.Collections.Generic;
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
            string priorStatus=(string)Get(host,"Status");
            int statusLayouts=(int)Get(page,"LayoutBuildCount");
            Set(host,"Status","等待手动操作");prepare(960,760,1);
            Require((int)Get(page,"LayoutBuildCount")==statusLayouts,
                "ordinary background status does not rebuild layout or cancel a pointer gesture");
            Set(host,"Status",priorStatus);
            // The owner replaced the old short-status row contract. Measure the
            // complete blocks, not a zero-height status rectangle within them.
            object rowPanel=rows.First(row=>Get(row,"Kind").ToString()=="Panel");
            object[] basicRows=((IEnumerable)Get(Get(page,"layout"),"rows")).Cast<object>().Where(row=>Get(row,"Kind").ToString()=="Panel").ToArray();
            float sharedGap=(float)Get(Get(basicRows[1],"Rect"),"Y")-(float)Get(Get(basicRows[0],"Rect"),"Bottom");
            Require((float)Get(Get(rowPanel,"Rect"),"Y")== (float)Get(Get(page,"layout"),"Height") &&
                (float)Get(panel,"Height")-(float)Get(Get(rowPanel,"Rect"),"Bottom")==sharedGap,
                "coin block uses the shared row gap exactly once at both handoffs");
            var name=rows.First(row=>(string)GetOptional(row,"Text")=="自动存钱");
            Call(state,"ScrollTo",Math.Max(0,(float)Get(Get(name,"Rect"),"Y")-16));prepare(960,760,1);
            Click(page,Find(page,"Coin",settings.Enabled?"关闭":"开启"));
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});prepare(960,760,1);
            bool first=settings.Enabled;
            Require(!first && ((IEnumerable)Get(panel,"rows")).Cast<object>().Where(row=>Get(row,"Kind").ToString()=="Text").All(row=>(string)Get(row,"Text")=="自动存钱"),"normal row contains only its function name");
            if(graphics!=null)
            {
                Directory.CreateDirectory(output);
                graphics.LoadItemTextures(((IEnumerable)Get(Get(page,"QuickPanel"),"visibleTypes")).Cast<int>());
                Call(page,"Prepare",true,Matrix.Identity,new Vector2(960,760));
                graphics.Image(Path.Combine(output,"coin-off-960x760.png"),()=>
                {Call(renderer,"Draw",state,Matrix.Identity,false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);},Matrix.Identity,960,760);
            }
            FailurePresentation(host,page,prepare,null);
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
            float[] stable=NativeQuickUiChecks.Geometry(page);
            foreach(string status in new[]{"取出保护中","没有可存钱币","未找到个人银行","暂无可用空间","等待手动操作"})
                for(int i=0;i<48;i++){Set(host,"Status",status);prepare(960,760,1);}
            Require((int)Get(page,"LayoutBuildCount")==layouts && stable.SequenceEqual(NativeQuickUiChecks.Geometry(page)),"all silent status transitions preserve complete layout and cards");
            object rangeOwner=Get(host,"Range"),quickPanel=Get(page,"QuickPanel");
            Func<long[]> work=()=>new[]{(long)Get(host,"WalletReads"),(long)Get(host,"CapacityReads"),(long)Get(rangeOwner,"TileReads"),
                (long)Get(rangeOwner,"ProjectileReads"),(long)Get(rangeOwner,"WitnessReads"),(long)Get(rangeOwner,"CompleteQueries"),
                (long)Get(quickPanel,"PickerReads"),(long)Get(quickPanel,"IconLoads"),(long)(int)Get(page,"LayoutBuildCount")};
            long[] beforeHover=work();
            foreach(string featureName in new[]{"自动存钱","保持收藏","快捷物品"})
            {
                var names=((IEnumerable)Get(page,"elements")).Cast<object>().Concat(((IEnumerable)Get(quickPanel,"visible")).Cast<object>().Select(part=>Get(part,"Element")));
                object nameRect=Get(names.First(e=>(string)GetOptional(e,"Text")==featureName),"HintRect");
                for(int i=0;i<240;i++)Require(Call(page,"Hint",(float)Get(nameRect,"X")+2,(float)Get(nameRect,"Y")+2,null,null)!=null,"name hint remains reachable: "+featureName);
            }
            Require(beforeHover.SequenceEqual(work()),"name-only hover does no layout, bank, inventory, picker or icon work");
            PointerAndExpandedContent(host,page,state,prepare);
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
                        object target=Get(((IEnumerable)Get(page,"elements")).Cast<object>().First(e=>(string)GetOptional(e,"Text")=="自动存钱"),"HintRect");
                        Hover(state,shape[0],shape[1],scale,(float)Get(target,"X")+2,(float)Get(target,"Y")+2);
                        typeof(PlayerInput).GetField("_originalScreenWidth",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[0]);
                        typeof(PlayerInput).GetField("_originalScreenHeight",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[1]);
                        graphics.Image(Path.Combine(output,"coin-"+hintKind+"-"+shape[0]+"x"+shape[1]+"-"+shape[2]+".png"),()=>
                        {Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);Call(renderer,"DrawHints",state,Matrix.CreateScale(scale),page,false,false);},Matrix.CreateScale(scale),shape[0],shape[1]);
                        Require((bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual shared coin hint rendered: "+hintKind);
                    }
                }
            }
            if(graphics!=null)FailurePresentation(host,page,prepare,()=>
            {
                foreach(var shape in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1280,720,150}})
                {
                    float scale=shape[2]/100f;
                    Hover(state,shape[0],shape[1],scale,0,0);prepare(shape[0],shape[1],scale);
                    typeof(PlayerInput).GetField("_originalScreenWidth",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[0]);
                    typeof(PlayerInput).GetField("_originalScreenHeight",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,shape[1]);
                    object target=Get(((IEnumerable)Get(page,"elements")).Cast<object>().First(e=>(string)GetOptional(e,"Text")=="自动存钱"),"HintRect");
                    Hover(state,shape[0],shape[1],scale,(float)Get(target,"X")+2,(float)Get(target,"Y")+2);
                    graphics.Image(Path.Combine(output,"coin-error-"+shape[0]+"x"+shape[1]+"-"+shape[2]+".png"),()=>
                    {Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);Call(renderer,"DrawHints",state,Matrix.CreateScale(scale),page,false,false);},Matrix.CreateScale(scale),shape[0],shape[1]);
                    Require((bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual save failure is reread through the shared name hint");
                }
            });
            Call(page,"Suspend");Call(state,"Close");
            Console.WriteLine("PASS: G06 real F5 toggles/public key window, three viewport shapes and stable layout."+(graphics==null?"":" Original-resource screenshots written."));
        }
        private static object[] Controls(object page){return ((IEnumerable)Get(page,"controls")).Cast<object>().ToArray();}
        private static void PointerAndExpandedContent(object host,object page,object state,Action<int,int,float> prepare)
        {
            object quick=Get(page,"QuickPanel");
            Action reveal=()=>
            {
                prepare(960,760,1);
                object part=((IEnumerable)Get(quick,"logical")).Cast<object>().First(p=>Get(p,"Command").ToString()=="Add");
                Call(state,"ScrollTo",Math.Max(0,(float)Get(Get(Get(part,"Element"),"Rect"),"Y")-16));prepare(960,760,1);
            };
            reveal();var add=Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && (string)GetOptional(Get(c,"Element"),"Text")=="添加");
            object r=Get(add,"Rect");var point=new Vector2((float)Get(r,"X")+8,(float)Get(r,"Y")+8);
            Action<ButtonState> sample=left=>{PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(page,"ProcessInput",true,new KeyboardState(),point,true,true,false);};
            sample(ButtonState.Released);sample(ButtonState.Pressed);
            int builds=(int)Get(page,"LayoutBuildCount");
            foreach(string status in new[]{"等待手动操作","没有可存钱币","等待账户变化"})
            {Set(host,"Status",status);sample(ButtonState.Pressed);prepare(960,760,1);}
            sample(ButtonState.Released);Require((bool)Get(quick,"Editing") && (int)Get(page,"LayoutBuildCount")==builds,"background states between press/release do not cancel an unrelated add gesture");
            Call(quick,"Suspend");reveal();sample(ButtonState.Released);sample(ButtonState.Pressed);prepare(800,600,1);sample(ButtonState.Released);
            Require(!(bool)Get(quick,"Editing"),"real viewport change still rejects an obsolete pressed control");
            prepare(960,760,1);Call(state,"ScrollTo",0f);prepare(960,760,1);
            object selection=Get(page,"selection");
            var list=JueMingR.Features.Items.ItemListKind.Discard;
            Require((bool)Call(selection,"Open",list,0),"actual preceding discard selector opens");Set(page,"dirty",true);prepare(960,760,1);
            object coin=Get(page,"CoinPanel"),layout=Get(page,"layout");
            object coinRect=Get(((IEnumerable)Get(coin,"rows")).Cast<object>().First(e=>Get(e,"Kind").ToString()=="Panel"),"Rect");
            Require((float)Get(coinRect,"Y")== (float)Get(layout,"Height") && (float)Get(coinRect,"Y")>(float)Get(Get(layout,"Header"),"Bottom"),"coin and quick blocks follow the complete expanded preceding content");
            Call(selection,"Cancel");Set(page,"dirty",true);prepare(960,760,1);
            var name=((IEnumerable)Get(coin,"rows")).Cast<object>().First(e=>(string)GetOptional(e,"Text")=="自动存钱");
            Call(state,"ScrollTo",Math.Max(0,(float)Get(Get(name,"Rect"),"Y")-16));prepare(960,760,1);
        }
        private static void FailurePresentation(object host,object page,Action<int,int,float> prepare,Action preview)
        {
            var original=(CoinSettings)Get(host,"Settings");
            var store=new NativeQuickUiChecks.UiStore(new CoinPreferenceCodec().Encode(false));
            using(var temporary=new CoinSettings(store))
            {
                NativeQuickItemChecks.Until(()=>{temporary.Poll();return temporary.Loaded;});
                Set(host,"Settings",temporary);Set(page,"dirty",true);Call(host,"Poll");prepare(960,760,1);
                try
                {
                    float[] before=NativeQuickUiChecks.Geometry(page);var alerts=new List<string>();
                    store.Fail=true;Require(temporary.Set(true),"real isolated failing coin save accepted");
                    prepare(960,760,1);Require(before.SequenceEqual(NativeQuickUiChecks.Geometry(page)),"pending coin save adds no row height");
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !temporary.Busy;});prepare(960,760,1);
                    Call(host,"TakeFeedback",(Action<string>)alerts.Add);string error=(string)Get(host,"NameHint");
                    Require(alerts.Count==1 && !string.IsNullOrEmpty(error) && !temporary.Enabled && before.SequenceEqual(NativeQuickUiChecks.Geometry(page)),"actual coin failure alerts once and stays readable without moving following items");
                    for(int i=0;i<20;i++)Call(host,"TakeFeedback",(Action<string>)alerts.Add);Require(alerts.Count==1,"coin failure is not repeated every frame");
                    preview?.Invoke();
                    store.Fail=false;store.Gate.Reset();Require(temporary.Set(true),"explicit failed coin save retry admitted");
                    Require((string)Get(host,"NameHint")==error,"pending retry retains current error");store.Gate.Set();
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !temporary.Busy;});Require(temporary.Message==null && temporary.Enabled,"reliable coin preference retry clears its error");
                    store.Fail=store.Unknown=true;Require(temporary.Set(false),"unknown coin write admitted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !temporary.Busy;});
                    Call(host,"TakeFeedback",(Action<string>)alerts.Add);Require(temporary.Protected && !temporary.Enabled && alerts.Count==2,"new unknown coin failure is reported and cannot reactivate");
                }
                finally{store.Gate.Set();Set(host,"Settings",original);Set(page,"dirty",true);Call(host,"Poll");prepare(960,760,1);}
            }
        }
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
