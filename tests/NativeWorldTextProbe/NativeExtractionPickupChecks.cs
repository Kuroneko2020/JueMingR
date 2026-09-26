using System;
using System.Linq;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExtractionPickupChecks
    {
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var items=Get(host,"Items");p.IsVoidVaultEnabled=false;
            foreach(var item in p.inventory){item.SetDefaults(9);item.stack=item.maxStack;}
            p.inventory[10].SetDefaults(424);p.inventory[10].stack=100;p.position=new Vector2(640,640);
            p.itemTime=p.itemAnimation=0;Main.playerInventory=false;Main.mouseItem.TurnToAir();p.trashItem.TurnToAir();
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem{whoAmI=i};
            NativeExtractionChecks.Machine(42,40,219);Main.rand=new Terraria.Utilities.UnifiedRandom(123);
            Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",1);});
            WorldItem product=null;
            for(int frame=0;frame<240 && product==null;frame++)
            {
                NativeExtractionChecks.Frame(context,input);
                product=Main.item.FirstOrDefault(i=>i.active && i.type>0 && (i.type<71 || i.type>74));
            }
            Require(product!=null && p.inventory[10].stack<100 && p.trashItem.IsAir,"full inventory extraction really consumes material and emits an unacquired noncoin world product");
            Call(host,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1);});
            for(int i=0;i<40;i++)NativeExtractionChecks.Frame(context,input);
            int type=product.type,quantity=product.stack;
            Call(items,"Change",new ItemAutomationSettings(false,false,true,new int[0],new[]{type},false));
            NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            for(int i=0;i<12;i++)NativeExtractionChecks.Frame(context,input);
            Require(product.active && product.stack==quantity && p.trashItem.IsAir,"unpicked extraction products never count as acquired inventory");
            // Only free a synthetic slot; acquisition itself must use the production native pickup hook.
            p.inventory[12].TurnToAir();Call(p,"PickupItem",product);
            Require(!product.active && p.inventory.Any(i=>i.type==type && i.stack==quantity),"native pickup transfers the actual extraction world product");
            for(int i=0;i<12 && p.trashItem.IsAir;i++)NativeExtractionChecks.Frame(context,input);
            Require(p.trashItem.type==type && p.trashItem.stack==quantity && !p.inventory.Any(i=>i.type==type),"real extraction pickup grants exact downstream discard eligibility");
            Call(items,"Change",ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            foreach(var item in p.inventory)item.TurnToAir();
            Console.WriteLine("PASS G08 extraction overflow: full inventory, actual native world product and pickup, exact downstream discard; unpicked product stays outside inventory sources.");
        }
    }
}
