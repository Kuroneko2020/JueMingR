using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickUiChecks
    {
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
            object favorite=Get(context,"KeepFavorited");
            Require(settings.TryChange(settings.Current.Toggles(true,false),null,out reason),"visible capability fixture enabled preference");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            Call(favorite,"FailClosed");Require((bool)Get(panel,"NeedsBuild"),"live capability failure invalidates existing panel");prepare(960,760,1);
            var favoriteOn=((IEnumerable)Get(panel,"logical")).Cast<object>().First(part=>Get(part,"Command").ToString()=="FavoriteOn");
            Require(!(bool)Get(favoriteOn,"Enabled") && !(bool)Get(favoriteOn,"Selected") && ((IEnumerable)Get(panel,"logical")).Cast<object>().Any(part=>((string)GetOptional(Get(part,"Element"),"Text")??"").Contains("保持收藏暂不可用")),"failed capability projects unavailable, not a selected effective on state");
            Require((bool)Get(quick,"ControlsEnabled"),"favorite failure leaves quick capability independent");Set(favorite,"failed",false);prepare(960,760,1);
            Set(settings,"Protected",true);prepare(960,760,1);Call(favorite,"FailClosed");
            Require((bool)Get(panel,"NeedsBuild"),"capability failure also invalidates an already write-protected panel");prepare(960,760,1);
            favoriteOn=((IEnumerable)Get(panel,"logical")).Cast<object>().First(part=>Get(part,"Command").ToString()=="FavoriteOn");
            Require(!(bool)Get(favoriteOn,"Selected"),"protected settings cannot mask failed capability state");Set(settings,"Protected",false);Set(favorite,"failed",false);prepare(960,760,1);
            Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
            Require((bool)Get(panel,"Editing"),"physical pointer opens inventory icon selector");
            Reveal(panel,state,"Cancel");prepare(960,760,1);Click(page,"Quick","×");prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing") && settings.Current.Entries.Count==96,"cancel selector preserves durable entries");
            Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
            Require(((IEnumerable)Get(panel,"candidates")).Cast<int>().Contains(5359),"inventory phone expands real legal target states");
            Reveal(panel,state,"Pick");prepare(960,760,1);var pick=Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Pick");
            ClickControl(page,pick);NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing") && settings.Current.Entries.Count==97,"inventory single click immediately saves chosen item");
            Reveal(panel,state,"Edit");prepare(960,760,1);ClickControl(page,Controls(page).First(c=>Get(c,"Command").ToString()=="Quick" && GetPart(panel,c,"Command")=="Edit"));prepare(960,760,1);
            Call(page,"ProcessInput",true,new KeyboardState(Keys.Escape),Vector2.Zero,true,true,false);prepare(960,760,1);
            Require(!(bool)Get(panel,"Editing"),"escape closes inventory selector without mutation");            // Shared key button uses the one real popup callback/target.
            Reveal(panel,state,"Edit");prepare(960,760,1);Click(page,"QuickHotkey",null);Click(page,"QuickHotkey",null);
            Require((bool)Get(Get(shell,"HotkeyPopup"),"Visible"),"dynamic row reaches common binding popup");Call(Get(shell,"HotkeyPopup"),"Close");
            long reads=(long)Get(panel,"PickerReads");int layouts=(int)Get(page,"LayoutBuildCount");
            for(int i=0;i<240;i++){Call(quick,"Update",0UL);prepare(960,760,1);}
            Require((long)Get(panel,"PickerReads")==reads && (int)Get(page,"LayoutBuildCount")==layouts,"long stable visible list does no repeated inventory/layout work");
            var shown=((IEnumerable)Get(panel,"visible")).Cast<object>().ToArray();object view=Get(page,"view");
            Require(shown.Length<((IList)Get(panel,"logical")).Count/2 && shown.All(part=>
                (float)Get(Get(Get(part,"Element"),"Rect"),"Bottom")>(float)Get(view,"Y") &&
                (float)Get(Get(Get(part,"Element"),"Rect"),"Y")<(float)Get(view,"Bottom")),"96 entries project only rectangles intersecting the viewport");
            if(graphics!=null)
            {
                Directory.CreateDirectory(output);
                File.WriteAllLines(Path.Combine(output,"state-names.tsv"),new[]{2611,5526,4131,5325,4346,5391,4767,5453,5059,5060,5309,5454,5323,5455,5324,5329,5330,5358,5360,5361,5359,5437,6168,6169,6193,6194,6190,6195}.Select(type=>type+"\t"+Lang.GetItemNameValue(type)));
                Action<int,int,float,string> draw=(width,height,scale,name)=>
                {
                    Call(renderer,"RefreshResources");prepare(width,height,scale);
                    graphics.LoadItemTextures(((IEnumerable)Get(panel,"visibleTypes")).Cast<int>());
                    Call(page,"Prepare",true,Matrix.CreateScale(scale),new Vector2(width,height));
                    graphics.Image(Path.Combine(output,name),()=>{Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(page,"Draw",Get(shell,"drawKeyboard"),true);},Matrix.CreateScale(scale),width,height);
                };
                Reveal(panel,state,"FavoriteOn");draw(960,760,1,"quick-items-controls.png");
                Reveal(panel,state,"Edit");draw(1280,720,1.5f,"quick-items-long-list-150.png");
                Reveal(panel,state,"Add");prepare(960,760,1);Click(page,"Quick","添加");prepare(960,760,1);
                Reveal(panel,state,"Cancel");draw(960,760,1,"quick-items-chooser.png");draw(960,440,1,"quick-items-small-viewport.png");
            }
            Call(page,"Suspend");Call(state,"Close");
            Console.WriteLine("PASS: real G05 F5 pointer/edit/cancel/shared binding, compact icon/key fields, inventory grid and stable viewport workload."+(graphics==null?"":" Original-resource PNGs produced."));
        }
        private static object[] Controls(object page){return ((IEnumerable)Get(page,"controls")).Cast<object>().ToArray();}
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
