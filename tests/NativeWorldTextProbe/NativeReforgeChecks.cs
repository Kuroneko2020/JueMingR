using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Processing;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeReforgeChecks
    {
        internal static void Run(object context,object host,object input)
        {
            object owner=GetOptional(host,"Reforge");Require(owner!=null,"full composition includes reforge owner");
            Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);
            var panel=typeof(Main).GetField("_newChatPanel",BindingFlags.NonPublic|BindingFlags.Instance);panel.SetValue(Main.instance,Activator.CreateInstance(panel.FieldType,true));
            Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();Main.ShopHelper=new Terraria.GameContent.ShopHelper();
            Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            Main.playerInventory=Main.InReforgeMenu=true;Main.npc[0].SetDefaults(107);Main.npc[0].active=true;Main.npc[0].homeless=true;p.SetTalkNPC(0);
            Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(53);p.inventory[50]=NativeCoinChecks.Coin(74,10);p.currentShoppingSettings.PriceAdjustment=1f;
            var settings=((ProcessingSettings[])Get(host,"Settings"))[2];
            Require(settings.Set(new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct())),"isolated complete-prefix list save admitted");
            NativeQuickItemChecks.Until(()=>{settings.Poll();return settings.Ready;});
            long quote=(long)Main.reforgeItem.value*Main.reforgeItem.stack/3,before=Total(p);
            Sample(input,false);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,quote);
            Sample(input,true);Call(owner,"Update");
            if(GetOptional(owner,"Failure")!=null)Console.WriteLine(Get(owner,"Failure"));
            Require(before-Total(p)==quote && Main.reforgeItem.prefix!=0,"one exact debit and native prefix roll; error="+GetOptional(host,"Error"));
            Require(!(bool)Call(owner,"NativePayment",p,quote,-1),"auto hit consumes the rest of the same physical hold");
            Sample(input,true);Call(owner,"Update");Require(!(bool)Call(owner,"NativePayment",p,quote,-1),"new Update with held left cannot bypass hit stop");
            Sample(input,false);Call(owner,"Update");
            quote=(long)Main.reforgeItem.value*Main.reforgeItem.stack/3;before=Total(p);
            Call(owner,"Observe",true,120,310,15,quote);Sample(input,true);Call(owner,"Update");Call(owner,"Observe",true,120,310,15,quote);
            Require((bool)Call(owner,"NativePayment",p,quote,-1),"ordinary matched-item payment preserved");
            typeof(Main).GetMethod("ReforgeItemInReforgeSlot",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            Require(before-Total(p)==quote && !(bool)Call(owner,"NativePayment",p,quote,-1),"manual exception is one paid roll, not a new auto tail");
            Sample(input,false);Call(owner,"Update");Main.InReforgeMenu=false;p.SetTalkNPC(-1);Main.playerInventory=false;
            Console.WriteLine("PASS G08 reforge: complete target list, real BuyItem/inner roll, exact fee, auto-hit tail and fresh manual exception. This CPU case supplies the quote/hit observation; it does not exercise outer DrawInventory.");
        }
        internal static long Total(Player p){long total=NativeCoinChecks.Total(p.inventory,54);for(int a=0;a<4;a++)total+=NativeCoinChecks.Total(NativeCoinChecks.Bank(p,a).item,40);return total;}
        internal static void Sample(object input,bool left,int x=120,int y=310)
        {
            Main.keyState=new KeyboardState();
            PlayerInput.MouseInfo=new MouseState(x,y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            Main.mouseLeft=left;Main.mouseLeftRelease=true;
            PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.MouseLeft=left;PlayerInput.Triggers.Update();
            Call(input,"BeginUpdate");Call(input,"AfterNativeMouse",new System.Collections.Generic.List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
        }
    }
}
