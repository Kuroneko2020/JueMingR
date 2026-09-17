using System;
using System.Reflection;
using System.Threading;
using JueMingR.Features.QuickItems;
using Terraria;
using Terraria.UI;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFavoriteChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            object favorite=Get(context,"KeepFavorited");
            Require(GetOptional(favorite,"SetupError")==null,"native favorite hooks: "+GetOptional(favorite,"SetupError"));
            object quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");string reason;
            Require(settings.TryChange(settings.Current.Toggles(true,false),null,out reason),"favorite config transaction");
            var until=DateTime.UtcNow.AddSeconds(5);while(settings.Busy){Call(quick,"Poll");if(DateTime.UtcNow>until)throw new Exception("favorite config timeout");Thread.Sleep(1);}
            var p=Main.LocalPlayer;
            for(int i=0;i<p.inventory.Length;i++)p.inventory[i]=new Item();Main.mouseItem=new Item();
            Item a=new Item();a.SetDefaults(ItemID.CopperShortsword);a.favorited=true;Item b=new Item();b.SetDefaults(ItemID.CopperShortsword);
            // .8 ordinary weapons can stack. Full real stacks force a zero-
            // quantity exchange without falsifying the native maxStack field.
            a.stack=a.maxStack;b.stack=b.maxStack;
            p.inventory[10]=a;p.inventory[11]=b;Call(favorite,"Observe");
            Click(p.inventory,0,10);Require(ReferenceEquals(Main.mouseItem,a),"native pickup of first singleton");
            Click(p.inventory,0,11);
            Require(ReferenceEquals(Main.mouseItem,b) && ReferenceEquals(p.inventory[11],a),"native equal weapon swap actually happened; mouse="+Main.mouseItem.type+":"+Main.mouseItem.stack+" slot="+p.inventory[11].type+":"+p.inventory[11].stack+" max="+a.maxStack+" animation="+p.itemAnimation+" time="+p.itemTime+" a="+ReferenceEquals(Main.mouseItem,a)+" protected="+Get(Get(Get(context,"items"),"Ownership"),"ProtectedSlots"));
            Require(a.favorited && !b.favorited,"favorite follows the exact singleton, not native swapped flags");
            Click(p.inventory,0,10); // Put the unstarred B back.
            typeof(ItemSlot).GetMethod("ToggleFavorited",Flags).Invoke(null,new object[]{a});Call(favorite,"Observe");
            Require(!a.favorited && !b.favorited,"explicit cancellation wins over later restoration");
            a.favorited=true;Call(favorite,"Clear");Call(favorite,"Observe");
            Item helmet=new Item();helmet.SetDefaults(ItemID.CopperHelmet);helmet.favorited=true;p.inventory[12]=helmet;Call(favorite,"Observe");
            Click(p.inventory,0,12);Click(p.armor,8,0);
            Require(ReferenceEquals(p.armor[0],helmet) && !helmet.favorited,"equipping retains intent without enabling loadout sharing");
            Click(p.armor,8,0);Click(p.inventory,0,12);
            Require(ReferenceEquals(p.inventory[12],helmet) && helmet.favorited,"proven equipment return restores favorite");
            Item external=new Item();external.SetDefaults(ItemID.CopperShortsword);external.stack=external.maxStack;p.bank4.item[0]=external;
            Click(p.inventory,0,11);p.chest=-5;Click(p.bank4.item,32,0);p.chest=-1;
            Require(ReferenceEquals(Main.mouseItem,external) && !external.favorited,"zero-transfer void exchange does not seed favorite onto an unknown incoming twin");
            Click(p.inventory,0,11);
            Click(p.inventory,0,12);Click(p.armor,8,0);p.dead=true;p.DropItems(true);Call(favorite,"Update",0UL);p.dead=false;
            Click(p.armor,8,0);Click(p.inventory,0,12);
            Require(helmet.favorited,"softcore death retains independent intent for an equipment item that never dropped");
            Transfers(favorite,p);
            Console.WriteLine("PASS: actual ItemSlot swaps/cancellation/equipment, void exit, softcore, split/merge, trash, loadouts and mouse bucket; bounded idle observation.");
        }
        private static void Transfers(object favorite,Player p)
        {
            Main.keyState=new Microsoft.Xna.Framework.Input.KeyboardState();
            Item stack=new Item();stack.SetDefaults(ItemID.Wood);stack.stack=10;stack.favorited=true;p.inventory[20]=stack;Call(favorite,"Observe");
            typeof(ItemSlot).GetMethod("PickupItemIntoMouse",Flags).Invoke(null,new object[]{p.inventory,0,20,p});
            Require(stack.stack==9 && Main.mouseItem.stack==1 && Main.mouseItem.favorited,"real one-item split inherits intent");
            Item plain=new Item();plain.SetDefaults(ItemID.Wood);plain.stack=5;p.inventory[21]=plain;Call(favorite,"Observe");
            Click(p.inventory,0,21);Require(p.inventory[21].stack==6 && p.inventory[21].favorited && stack.favorited,"mixed real merge ORs intent without changing neighbor count");
            Click(p.inventory,0,20);Main.mouseLeft=Main.mouseLeftRelease=true;ItemSlot.Handle(ref p.trashItem,6);Main.mouseLeft=false;
            Require(ReferenceEquals(p.trashItem,stack),"native ref trash assignment retained exact item");
            Main.mouseLeft=Main.mouseLeftRelease=true;ItemSlot.Handle(ref p.trashItem,6);Main.mouseLeft=false;Click(p.inventory,0,20);
            Require(ReferenceEquals(p.inventory[20],stack) && stack.favorited,"native trash round trip retains intent after ref wrapper");
            Click(p.inventory,0,12);Click(p.armor,8,0);Item equipped=p.armor[0];int old=p.CurrentLoadoutIndex;
            p.TrySwitchingLoadout((old+1)%3);Require(p.CurrentLoadoutIndex!=old,"real equipment group switched");Call(favorite,"Observe");
            p.TrySwitchingLoadout(old);Click(p.armor,8,0);Click(p.inventory,0,12);
            Require(ReferenceEquals(p.inventory[12],equipped) && equipped.favorited,"known inactive equipment handoff survives real switch away/back");
            long reads=(long)Get(favorite,"Reads");for(int i=0;i<200;i++){p.dropItemCheck();p.ItemCheck();}
            Require((long)Get(favorite,"Reads")==reads,"idle native mirror hooks do not add full-domain scans");
            Main.playerInventory=true;Item bucket=new Item();bucket.SetDefaults(ItemID.WaterBucket);bucket.favorited=true;Main.mouseItem=bucket;Call(favorite,"Observe");
            p.dropItemCheck();p.selectedItemState.Update();Require(p.selectedItem==58,"native mouse bucket selection");
            Player.tileTargetX=Player.tileTargetY=40;Main.tile[40,40].liquid=0;p.controlUseItem=p.releaseUseItem=true;Main.mouseLeft=true;
            p.ItemCheck();Require(p.inventory[58].type==ItemID.EmptyBucket && Main.mouseItem.type==ItemID.EmptyBucket && Main.mouseItem.favorited,"last mouse water bucket converts by native usage and retains intent");
            Main.mouseLeft=false;p.controlUseItem=false;p.ItemCheck();p.ItemCheck();
            Require(Main.mouseItem.favorited && p.inventory[58].stack==1,"continuing native clone from transformed slot58 keeps sourced intent");
            Main.mouseItem=new Item();p.inventory[58]=new Item();Main.playerInventory=false;p.itemAnimation=p.itemTime=0;p.selectedItemState.Select(2);p.selectedItemState.Update();
            Call(favorite,"Observe");Require((int)Get(favorite,"Count")<100,"retired mirror and external items do not accumulate");
        }
        private static void Click(Item[] inventory,int context,int slot)
        {Main.mouseLeft=Main.mouseLeftRelease=true;Main.cursorOverride=0;ItemSlot.LeftClick(inventory,context,slot);Main.mouseLeft=false;}
    }
}
