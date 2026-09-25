using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryUiChecks
    {
        internal static void Run(object context)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var renderer=Get(shell,"renderer");var ui=Get(shell,"RecoveryUi");var layout=Get(state,"Layout");
            Call(state,"Navigate",10);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action prepare=()=>{Call(renderer,"Prepare",state,960f,760f,1f);Call(ui,"PrepareLayout",Matrix.Identity);};prepare();
            var rows=((IEnumerable)Get(ui,"logical")).Cast<object>().Where(p=>GetOptional(Get(p,"Element"),"Description")!=null).Select(p=>(string)Get(Get(p,"Element"),"Text")).ToArray();
            Require(string.Join("|",rows)=="自动回血|自动回蓝|自动护士|家具增益|自动增益","actual F5 recovery order");
            float height=(float)Get(layout,"ContentHeight");var settings=(RecoverySettings)Get(Get(context,"Recovery"),"Potions");
            Require(settings.Set(new RecoveryOptions(2)),"UI save starts");prepare();Require((float)Get(layout,"ContentHeight")==height,"pending settings no reserved row");
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});prepare();Require((float)Get(layout,"ContentHeight")==height,"saved settings no flash height");
            var button=((IEnumerable)Get(ui,"visible")).Cast<object>().First(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==0);
            Call(ui,"Execute",button);prepare();
            var popup=Get(ui,"PotionPopup");Require((bool)Get(popup,"Visible") && ((int[])Get(popup,"Candidates")).Length>10,"full medication catalogue opens independent popup");
            Require((float)Get(layout,"ContentHeight")==height && !((IEnumerable)Get(ui,"logical")).Cast<object>().Any(part=>(int)Get(part,"Type")>0),"medication popup never expands parent page or inserts an inline grid");
            PopupInput(context,ui,popup,settings);
            Call(ui,"Suspend");prepare();
            var buffs=(RecoverySettings)Get(Get(context,"Recovery"),"Buffs");NativeRecoveryChecks.Save(buffs,new RecoveryOptions(allowedBuffs:new int[]{Terraria.ID.ItemID.RegenerationPotion}));
            Terraria.Main.LocalPlayer.inventory[9].SetDefaults(Terraria.ID.ItemID.IronskinPotion);
            button=((IEnumerable)Get(ui,"visible")).Cast<object>().First(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==2);Call(ui,"Execute",button);prepare();
            var selected=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.RegenerationPotion);
            var candidate=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.IronskinPotion);
            var a=Get(Get(candidate,"Element"),"Rect");var b=Get(Get(selected,"Element"),"Rect");
            Require((float)Get(a,"Right")<(float)Get(b,"X") && (float)Get(a,"Y")== (float)Get(b,"Y"),"available left and selected right share grid start in separate panes");
            var parts=((IEnumerable)Get(ui,"logical")).Cast<object>().ToArray();
            var panels=parts.Where(part=>Get(Get(part,"Element"),"Kind").ToString()=="Panel").Select(part=>Get(Get(part,"Element"),"Rect")).ToArray();
            foreach(var action in parts.Where(part=>new[]{-4,-5,6,7}.Contains((int)Get(part,"Command"))))
            {
                var rect=Get(Get(action,"Element"),"Rect");
                Require(panels.Any(panel=>(float)Get(rect,"X")>=(float)Get(panel,"X") && (float)Get(rect,"Right")<=(float)Get(panel,"Right") && (float)Get(rect,"Y")>=(float)Get(panel,"Y") && (float)Get(rect,"Bottom")<=(float)Get(panel,"Bottom")),"list actions belong visually to their pane rather than a detached toolbar");
            }
            NativeRecoveryChecks.Save(buffs,new RecoveryOptions());
            Call(state,"ScrollTo",60f);prepare();float scroll=(float)Get(state,"Scroll");prepare();Require((float)Get(state,"Scroll")==scroll,"dynamic page preparation preserves scroll");
            Call(state,"Navigate",1);prepare();Require(((IEnumerable)Get(ui,"logical")).Cast<object>().Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="自动收税"),"tax remains on misc page");
            Call(state,"Close");Call(ui,"Suspend");NativeRecoveryChecks.Save(settings,new RecoveryOptions());
            Console.WriteLine("PASS G07 UI: real composition controls, full catalogue, stable off/pending/saved heights and scroll.");
        }
        private static void PopupInput(object context,object ui,object popup,RecoverySettings settings)
        {
            var input=Get(context,"Input");var screen=new Vector2(960,760);Call(ui,"PreparePopup",Matrix.Identity,screen,false);
            var cell=((IEnumerable)Get(popup,"cells")).Cast<object>().First();var point=new Vector2((float)Get(cell,"X")+10,(float)Get(cell,"Y")+10);int type=((int[])Get(popup,"Candidates"))[0];
            Action<int,bool,Vector2> step=(mouse,focus,size)=>{NativeQuickGestureChecks.Sample(input,mouse);Call(popup,"Process",true,new Microsoft.Xna.Framework.Input.KeyboardState(),point,Matrix.Identity,size,focus,0);};
            step(0,true,screen);step(1,true,screen);Require((bool)Get(popup,"ConsumeLeft"),"popup claims physical press before underlying page");step(0,true,screen);
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(!settings.Value.LifeAllowed(type),"real popup press/release reliably disables clicked medication");
            Call(ui,"PreparePopup",Matrix.Identity,screen,false);step(1,true,screen);step(0,true,new Vector2(960,440));Require(!settings.Value.LifeAllowed(type),"resize invalidates old captured cell before release");
            Call(ui,"PreparePopup",Matrix.Identity,screen,false);step(1,true,screen);step(0,false,screen);
            Require(!(bool)Get(popup,"Visible") && (bool)Get(popup,"ConsumeLeft"),"focus close preserves owned mouse tail without applying cell");step(0,true,screen);
            Call(Get(context,"Shell"),"ConsumeSample");Require(!Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft,"shell consumer suppresses popup mouse tail");
        }
    }
}
