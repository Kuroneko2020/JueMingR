using System;
using System.Linq;
using JueMingR.Features.Items;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingPickupChecks
    {
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var items=Get(host,"Items");p.IsVoidVaultEnabled=false;
            foreach(var item in p.inventory){item.SetDefaults(9);item.stack=item.maxStack;}p.inventory[12].SetDefaults(4345);p.inventory[12].stack=1;
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem{whoAmI=i};p.trashItem.TurnToAir();Main.playerInventory=true;Main.HoverItem.TurnToAir();
            Call(items,"Change",new ItemAutomationSettings(false,false,true,new int[0],new[]{2002},false));NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            Main.rand=new Terraria.Utilities.UnifiedRandom(123);NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");
            var drop=Main.item.FirstOrDefault(i=>i.active && i.type==2002);Require(drop!=null && p.inventory[12].IsAir && p.trashItem.IsAir,"full main inventory bag really overflows before last bag debit, world product is not already processed");
            int stock=drop.stack;NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Call(p,"PickupItem",drop);Require(!drop.active && p.inventory.Any(i=>i.type==2002 && i.stack==stock),"real PickupItem acquires world overflow into last source slot");
            for(int i=0;i<8 && p.trashItem.IsAir;i++){NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");}
            Require(p.trashItem.type==2002 && p.trashItem.stack==stock && !p.inventory.Any(i=>i.type==2002),"only real pickup creates source for exact downstream discard");
            Call(items,"Change",ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            foreach(var item in p.inventory)item.TurnToAir();
            Console.WriteLine("PASS G08 overflow: real bag world drop, native pickup, exact downstream discard; unpicked products never treated as acquired inventory.");
        }
    }
}
