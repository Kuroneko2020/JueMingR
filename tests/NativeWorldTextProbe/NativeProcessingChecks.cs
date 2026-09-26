using System;
using System.Linq;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingChecks
    {
        internal static void Run(object context)
        {
            object host=GetOptional(context,"Processing");
            Require(host!=null,"complete profile includes continuous processing");
            Require((bool)Get(host,"Available"),"production processing hooks: "+GetOptional(host,"SetupError"));
            object input=Get(context,"Input");Player p=Main.LocalPlayer;
            PopupText.ClearAll();
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",0);});
            Require(!(bool)Call(host,"Value",0),"bags default off");
            Main.playerInventory=true;p.inventory[12].SetDefaults(1774);p.inventory[12].stack=12;
            Call(host,"Set",0,true);
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",0);});
            for(int frame=0;frame<5;frame++)
            {
                Sample(input,true);int before=p.inventory[12].stack;
                Call(context,"UpdateRuntime");
                if(GetOptional(Get(host,"Bags"),"Failure")!=null)Console.WriteLine(Get(Get(host,"Bags"),"Failure"));
                Require(p.inventory[12].stack==before-1,"one native bag consumed each held update; frame="+frame+" stack="+p.inventory[12].stack+" error="+GetOptional(host,"Error")+" hold="+Get(Get(host,"Bags"),"Holding")+" permit="+Get(input,"CanStartActions")+" ui="+Get(Get(context,"Shell"),"CanProcessingInput"));
                // A second Draw within one update must not create a second chain.
                for(int draw=0;draw<2;draw++){Main.mouseRight=true;Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,12);}
                Require(p.inventory[12].stack==before-1,"native Handle cannot double-open the claimed gesture");
            }
            Sample(input,false);int remaining=p.inventory[12].stack;Call(context,"UpdateRuntime");
            Require(p.inventory[12].stack==remaining,"physical release stops without a pending burst");
            var items=Get(host,"Items");
            Call(items,"Change",new ItemAutomationSettings(false,false,true,new int[0],Enumerable.Range(1,Terraria.ID.ItemID.Count-1),false));
            NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(Get(items,"Feature"),"Enabled");});
            for(int i=0;i<50;i++)p.inventory[i].TurnToAir();
            p.inventory[12].SetDefaults(3093);p.inventory[12].stack=40;p.trashItem.TurnToAir();
            for(int frame=0;frame<8;frame++)
            {
                Sample(input,true);Call(context,"UpdateRuntime");
                Require(p.inventory[12].type==3093 && p.inventory[12].stack==39-frame,"no burst/drain pause with downstream enabled");
                Require(!p.trashItem.IsAir,"actual native discard occurs before physical release");
                Require(!(bool)Get(Get(items,"World"),"HasManualOperation"),"controlled bag has no phantom manual slot/material; slot="+Get(Get(items,"World"),"ManualSlot"));
                Main.mouseRight=true;Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,12);
            }
            Sample(input,false);Call(context,"UpdateRuntime");
            NativeBagBoundaryChecks.Run(context,host,input);
            NativeExtractionChecks.Run(context,host,input);
            NativeReforgeChecks.Run(context,host,input);
            Console.WriteLine("PASS: native continuous bags consume once per Update and never again from Draw; release stops.");
        }
        internal static void Sample(object input,bool held)
        {
            Main.keyState=held?new KeyboardState(Keys.LeftShift):new KeyboardState();
            PlayerInput.MouseInfo=new MouseState(200,200,0,ButtonState.Released,ButtonState.Released,held?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released);
            PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.MouseRight=held;PlayerInput.Triggers.Update();Main.mouseLeft=false;Main.mouseRight=held;
            Call(input,"BeginUpdate");Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
        }
    }
}
