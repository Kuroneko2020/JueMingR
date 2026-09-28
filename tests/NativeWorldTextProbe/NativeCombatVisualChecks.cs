using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    internal static class NativeCombatVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            var shell=Get(context,"Shell");var state=Get(shell,"State");var combat=Get(context,"Combat");var renderer=Get(shell,"renderer");var drag=Get(shell,"CombatInterval");
            var settings=(CombatSettings)Get(combat,"Settings");NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Ready;});
            NativeCombatUiChecks.Run(context);Call(shell,"CloseAndSubmitPosition");
            foreach(var size in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1440,900,150}})
            {
                Main.screenWidth=size[0];Main.screenHeight=size[1];PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=size[2]/100f;
                UiFrame(context,Vector2.Zero,false);UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);Nav(context,8);
                var layout=Get(state,"Layout");var elements=((IEnumerable)Get(layout,"Elements")).Cast<object>().ToArray();
                string[] names={"自动连点","链球连击","光剑快切","完美左轮","省力魔法绳","自动转向","装备提示","自动汇报","哥布林必死"};
                var labels=names.Select(n=>elements.Single(e=>(string)GetOptional(e,"Text")==n)).ToArray();
                Require(labels.Zip(labels.Skip(1),(a,b)=>(float)Get(Get(a,"Rect"),"Y")<(float)Get(Get(b,"Rect"),"Y")).All(x=>x),"nine actual rows retain Legacy relative order");
                var field=elements.Single(e=>Get(e,"Command").ToString()=="CombatInterval");
                Require((float)Get(Get(field,"Rect"),"Width")-66==108,"owner-requested track doubles from 54 to 108 logical pixels");
                Require(Math.Abs(CenterY(field)-CenterY(labels[2]))<.01f,"interval remains vertically centered in quick-switch row");
                Image("page");
                if(size[1]==760)
                {
                    foreach(var element in elements.Where(e=>Get(e,"Command").ToString().StartsWith("Combat") && Get(e,"Command").ToString().EndsWith("On")))
                    {
                        string command=Get(element,"Command").ToString();Click(context,Position(element));
                        NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});
                        Require(settings.CompletionSucceeded,"real F5 button commits "+command);
                    }
                    Require(settings.Value.EnabledMask==255,"all eight actual buttons update independent persisted settings");
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    foreach(int value in new[]{0,12,30})
                    {
                        UiFrame(context,Vector2.Zero,false);var track=Get(drag,"Track");Vector2 at=(new Vector2((float)Get(track,"X")+(float)Get(track,"Width")*value/30,(float)Get(track,"Y")+7))*Main.UIScale;
                        Vector2 pressAt=new Vector2((float)Get(track,"X")+(float)Get(track,"Width")/2,(float)Get(track,"Y")+7)*Main.UIScale;
                        long before=settings.AcceptedCommandId;UiFrame(context,pressAt,true);for(int i=0;i<8;i++)UiFrame(context,at,true);
                        Require((bool)Get(drag,"Captured") && settings.AcceptedCommandId==before,"drag preview never saves on held samples, value="+value+" capture="+Get(drag,"Captured")+" accepted="+settings.AcceptedCommandId+" before="+before);
                        UiFrame(context,at,false);NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});
                        Require(settings.Value.SwitchInterval==value && settings.AcceptedCommandId==before+1,"focused release saves exactly once at "+value);
                    }
                    UiFrame(context,Position(field),false);Image("interval-hint");Require((bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual interval field has visible help");
                    var trackNow=Get(drag,"Track");Vector2 middle=new Vector2((float)Get(trackNow,"X")+(float)Get(trackNow,"Width")/2,(float)Get(trackNow,"Y")+7)*Main.UIScale;
                    long accepted=settings.AcceptedCommandId;UiFrame(context,middle,true);UiFrame(context,middle,true,Keys.Escape);UiFrame(context,Vector2.Zero,false,Keys.Escape);
                    Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted && (bool)Get(Get(Get(context,"Input"),"Hotkeys"),"HasSuppressedKeys"),"Escape cancels draft and retains key tail beyond mouse release");
                    UiFrame(context,Vector2.Zero,false);UiFrame(context,middle,true);graphics.SetMouseFont(Terraria.GameContent.FontAssets.ItemStack.Value);UiFrame(context,middle,false);
                    Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted,"font identity change cancels stale geometry before release");graphics.SetMouseFont(graphics.Font);UiFrame(context,Vector2.Zero,false);
                    var hotkey=elements.Single(e=>(string)GetOptional(e,"HotkeyTarget")=="combat.quick-switch");Vector2 keyPoint=Position(hotkey);Click(context,keyPoint);Click(context,keyPoint);
                    var popup=Get(shell,"HotkeyPopup");Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")=="combat.quick-switch","actual adjacent binding opens its own action");
                    PopupClick(context,popup,"Record");UiFrame(context,Vector2.Zero,false,Keys.LeftControl);UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F9);UiFrame(context,Vector2.Zero,false);
                    var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                    Require(bindings.CompletionSucceeded && bindings.Get("combat.quick-switch").MainKey==(int)Keys.F9,"real shared binding capture/save succeeds");Image("binding");PopupClick(context,popup,"Close");
                }
                var view=Get(layout,"Viewport");Vector2 inside=(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale;
                for(int i=0;i<30 && (float)Get(state,"Scroll")<(float)Get(layout,"MaxScroll");i++)UiFrame(context,inside,false,new Keys[0],-120);
                Image("bottom");var last=elements.Single(e=>Get(e,"Command").ToString()=="CombatGoblinOn");Click(context,Position(last));
                NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});Require(settings.Value.Enabled(7),"bottom row remains actionable after real scroll/scale");
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());Call(shell,"CloseAndSubmitPosition");UiFrame(context,Vector2.Zero,false);
                Vector2 Position(object element)
                {var viewport=Get(Get(state,"Layout"),"Viewport");return (Point(Get(element,"Rect"))+new Vector2((float)Get(state,"X")+(float)Get(viewport,"X"),(float)Get(state,"Y")+(float)Get(viewport,"Y")-(float)Get(state,"Scroll")))*Main.UIScale;}
                void Image(string name){graphics.Image(Path.Combine(output,"combat-"+name+"-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);}
            }
            Console.WriteLine("PASS G11A actual F5 rows/buttons, interval release/cancel/geometry, binding capture/save, original fonts and scroll at 100/150 percent.");
        }
        private static float CenterY(object element){var rect=Get(element,"Rect");return (float)Get(rect,"Y")+(float)Get(rect,"Height")/2;}
    }
}
