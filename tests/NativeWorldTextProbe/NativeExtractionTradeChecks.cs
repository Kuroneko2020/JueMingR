using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExtractionTradeChecks
    {
        private static int count,product,amount;
        internal static void Run(object context,object host,object input)
        {
            var options=(List<ItemTrader.TradeOption>)typeof(ItemTrader).GetField("_options",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ItemTrader.ChlorophyteExtractinator);
            Require(options.Count==52,"locked native extra trade set has 52 entries");
            var p=Main.LocalPlayer;var drop=typeof(Player).GetMethod("DropItemFromExtractinator",BindingFlags.NonPublic|BindingFlags.Instance);var audit=new Harmony("JueMingR.Tests.ManualExtractionTrade");
            audit.Patch(drop,postfix:new HarmonyMethod(typeof(NativeExtractionTradeChecks),nameof(Record)));
            try
            {
                NativeExtractionBoundaryChecks.Enable(host,true);NativeExtractionChecks.Machine(42,40,642);p.position=new Vector2(640,640);
                foreach(var trade in options)
                {
                    Require(ItemID.Sets.ExtractinatorMode[trade.TakingItemType]<0,"extra trade is not an automatic input");
                    foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(trade.TakingItemType);p.inventory[0].stack=trade.TakingItemStack*2;
                    p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemTime=p.itemAnimation=0;count=0;
                    for(int f=0;f<12;f++)NativeExtractionChecks.Frame(context,input);
                    Require(p.inventory[0].stack==trade.TakingItemStack*2 && count==0,"enabled automation ignores extra native trade: "+trade.TakingItemType);
                    NativeReforgeChecks.Sample(input,true,680,664);Call(Get(context,"Shell"),"ProcessInput");
                    Main.mouseX=680;Main.mouseY=664;Player.tileTargetX=42;Player.tileTargetY=41;Main.mouseLeft=true;p.releaseUseItem=true;
                    NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
                    Require(count==1 && product==trade.GivingItemType && amount==trade.GivingItemStack && p.inventory[0].stack==trade.TakingItemStack,"real manual trade has exact output and one debit: "+trade.TakingItemType+" actual="+count+"/"+product+"/"+p.inventory[0].stack);
                    for(int f=0;f<40;f++)NativeExtractionChecks.Frame(context,input);
                }
            }
            finally{audit.Unpatch(drop,HarmonyPatchType.All,audit.Id);NativeExtractionBoundaryChecks.Enable(host,false);}
            Console.WriteLine("PASS G08 extraction trades: all 52 native extra inputs ignored by automation and preserved through actual manual ItemCheck with exact debit/output.");
        }
        private static void Record(int __0,int __1){count++;product=__0;amount=__1;}
    }
}
