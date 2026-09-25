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
            Require((float)Get(layout,"ContentHeight")==height,"medication popup never expands parent page");
            PopupInput(context,ui,popup,settings);
            Call(ui,"Suspend");prepare();
            var buffs=(RecoverySettings)Get(Get(context,"Recovery"),"Buffs");NativeRecoveryChecks.Save(buffs,new RecoveryOptions(allowedBuffs:new int[]{Terraria.ID.ItemID.RegenerationPotion}));
            Terraria.Main.LocalPlayer.inventory[9].SetDefaults(Terraria.ID.ItemID.IronskinPotion);
            Call(ui,"Refresh");prepare();
            Require(!((IEnumerable)Get(ui,"logical")).Cast<object>().Any(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==2),"buff list is visible by default without a toggle button");
            ListMovement(context,ui);
            var selected=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.RegenerationPotion);
            var candidate=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.IronskinPotion);
            var a=Get(Get(candidate,"Element"),"Rect");var b=Get(Get(selected,"Element"),"Rect");
            Require((float)Get(a,"Right")<(float)Get(b,"X") && (float)Get(a,"Y")== (float)Get(b,"Y"),"available left and selected right share grid start in separate panes");
            Require(!(bool)Get(selected,"Selected") && (bool)Get(selected,"MissingStock"),"selected missing inventory shows a cross, not a membership check");
            var tick=typeof(Terraria.Main).GetField("_gameUpdateCount",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var catalog=Get(Get(context,"Recovery"),"Catalog");long reads=(long)Get(catalog,"StockReads");
            var before=Get(ui,"candidates");for(int n=0;n<100;n++)prepare();Require(ReferenceEquals(before,Get(ui,"candidates")) && reads==(long)Get(catalog,"StockReads"),"stable draws reuse snapshot without reading inventory");
            for(int n=0;n<300;n++){tick.SetValue(null,Terraria.Main.GameUpdateCount+1);prepare();}
            long observed=(long)Get(catalog,"StockReads")-reads;Require(observed>0 && observed<=10*98,"visible list observes bounded actual inventory work at 30 update cadence");
            Call(state,"Close");reads=(long)Get(catalog,"StockReads");for(int n=0;n<300;n++){tick.SetValue(null,Terraria.Main.GameUpdateCount+1);Call(ui,"Prepare",false,Matrix.Identity,new Vector2(960,760));}
            Require(reads==(long)Get(catalog,"StockReads"),"closed presentation has zero stock reads");Call(state,"RestoreVisible");prepare();
            Terraria.Main.LocalPlayer.inventory[10].SetDefaults(Terraria.ID.ItemID.RegenerationPotion);
            tick.SetValue(null,Terraria.Main.GameUpdateCount+30);prepare();
            selected=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.RegenerationPotion);
            Require((bool)Get(selected,"Selected") && !(bool)Get(selected,"MissingStock"),"refill updates check through bounded visible-page observation");
            Terraria.Main.LocalPlayer.inventory[10].TurnToAir();tick.SetValue(null,Terraria.Main.GameUpdateCount+30);prepare();
            selected=((IEnumerable)Get(ui,"logical")).Cast<object>().First(p=>(int)Get(p,"Type")==Terraria.ID.ItemID.RegenerationPotion);
            Require((bool)Get(selected,"MissingStock") && buffs.Value.BuffAllowed(Terraria.ID.ItemID.RegenerationPotion) && !buffs.Busy,"depletion changes only display and preserves persistent whitelist");
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
        private static void ListMovement(object context,object ui)
        {
            var input=Get(context,"Input");var keys=new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.W,Microsoft.Xna.Framework.Input.Keys.A,Microsoft.Xna.Framework.Input.Keys.Space);
            Terraria.Main.blockInput=false;Terraria.GameInput.PlayerInput.WritingText=false;
            Call(Get(context,"Shell"),"BeforeInput");
            NativeQuickItemChecks.Sample(input,keys.GetPressedKeys());
            Call(ui,"ProcessInput",true,keys,new Vector2(-10,-10),true,true,false);
            Call(Get(context,"Shell"),"ConsumeSample");
            Require(!Terraria.Main.blockInput && !Terraria.GameInput.PlayerInput.WritingText && !(bool)Get(ui,"OwnsTextToken"),"inline buff list does not acquire the native movement-blocking text lease");
            Require(Terraria.Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space),"inline list keeps movement keyboard sample");
            NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);
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
