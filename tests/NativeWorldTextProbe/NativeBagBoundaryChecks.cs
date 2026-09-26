using System;
using System.Linq;
using JueMingR.Features.Items;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBagBoundaryChecks
    {
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var items=Get(host,"Items");
            Call(items,"Change",ItemAutomationSettings.Default);
            NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return !(bool)Get(Get(items,"Feature"),"Enabled");});
            Reset(p);p.inventory[10].SetDefaults(3093);p.inventory[11].SetDefaults(3093);p.inventory[11].stack=2;
            Frame(context,input);
            Require(p.inventory[10].IsAir,"last bag native SetDefaults transition recognized");
            Main.mouseRight=Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,10);
            Frame(context,input);
            Require(p.inventory[11].stack==1,"spent hover slot cannot strand continuation to another stack");
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Reset(p);p.inventory[10].SetDefaults(3085);p.inventory[10].stack=2;p.inventory[8].SetDefaults(327);p.inventory[8].stack=2;
            Frame(context,input);
            Require(p.inventory[10].stack==1 && p.inventory[8].stack==1,"native golden lockbox consumes the actual main-inventory key");
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Reset(p);p.inventory[10].SetDefaults(3085);p.inventory[10].stack=2;
            Frame(context,input);Require(p.inventory[10].stack==2,"golden lockbox cannot open without a key");
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Reset(p);p.inventory[10].SetDefaults(4879);p.inventory[10].stack=2;p.inventory[8].SetDefaults(329);
            Frame(context,input);Require(p.inventory[10].stack==1 && p.inventory[8].type==329 && p.inventory[8].stack==1,"shadow lockbox checks possession without consuming key");
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Reset(p);p.inventory[10].SetDefaults(3093);p.inventory[10].stack=2;p.inventory[10].favorited=true;
            Frame(context,input);Require(p.inventory[10].stack==1 && p.inventory[10].favorited,"favorite remains normally usable");
            Main.playerInventory=false;Frame(context,input);Require(p.inventory[10].stack==1,"closed inventory stops next consume");Main.playerInventory=true;
            var state=Get(Get(context,"Shell"),"State");Set(state,"Visible",true);
            Frame(context,input);Require(p.inventory[10].stack==1,"F5 takeover stops next consume");Set(state,"Visible",false);
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            int[] all=Enumerable.Range(1,Terraria.ID.ItemID.Sets.OpenableBag.Length-1).Where(id=>Terraria.ID.ItemID.Sets.OpenableBag[id]).ToArray();
            Require(all.Length==58,"fixed .8 native bag qualification set has 58 entries");
            int actual=0,retired=0;
            foreach(int id in all)
            {
                Reset(p);p.inventory[10].SetDefaults(id);p.inventory[10].stack=2;
                if(Terraria.ID.ItemID.Sets.Deprecated[id])
                {Require((id==3861 || id==3862) && p.inventory[10].type==0,"native deprecated bag becomes Air instead of a fabricated usable bag");retired++;continue;}
                Require(p.inventory[10].type==id,"native bag definition preserves identity "+id);
                if(id==3085 || id==4879){p.inventory[8].SetDefaults(id==3085?327:329);p.inventory[8].stack=2;}
                Frame(context,input);
                if(GetOptional(Get(host,"Bags"),"Failure")!=null)Console.WriteLine(Get(Get(host,"Bags"),"Failure"));
                Require(p.inventory[10].stack==1,"complete original grant and one consumption for bag "+id+"; "+GetOptional(host,"Error"));
                Main.mouseRight=Main.mouseRightRelease=true;ItemSlot.Handle(p.inventory,0,10);
                Require(p.inventory[10].stack==1,"no native duplicate for bag "+id);
                actual++;
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            }
            Require(actual==56 && retired==2,"all 58 set entries accounted for: 56 real usable bags and two native deprecated definitions");
            Reset(p);p.inventory[10].SetDefaults(3085);p.inventory[10].stack=2;p.inventory[9].SetDefaults(4131);p.bank4.item[0].SetDefaults(327);
            Frame(context,input);Require(p.inventory[10].stack==2,"native void FindItem rejects an empty same-index main slot; automation cannot invent a broader source");
            p.inventory[0].SetDefaults(9);
            var guards=Get(host,"BankGuardsReady");Set(host,"BankGuardsReady",(Func<bool>)(()=>false));Frame(context,input);
            Require(p.inventory[10].stack==2 && p.bank4.item[0].type==327,"void key requires actual shared bank guards");
            Set(host,"BankGuardsReady",guards);Frame(context,input);
            Require(p.inventory[10].stack==1 && p.bank4.item[0].IsAir,"exact final void key consumed, not a guarded pseudo Air item; bag="+p.inventory[10].stack+" key="+p.bank4.item[0].type+"/"+p.bank4.item[0].stack+" void="+p.useVoidBag()+" error="+GetOptional(host,"Error"));
            NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");
            Reset(p);p.inventory[10].SetDefaults(3093);
            Call(host,"Set",0,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",0);});
            Main.mouseRight=Main.mouseRightRelease=true;ItemSlot.RightClick(p.inventory,0,10);
            Require(p.inventory[10].IsAir,"ordinary manual native bag opening remains available");
            Console.WriteLine("PASS G08 bag boundaries: 58 native entries (56 actual opens + 2 deprecated-to-Air), final/main/void keys, shadow possession, favorite, inventory/F5 stop and manual fallback.");
        }
        private static void Reset(Player p){foreach(var item in p.inventory)item.TurnToAir();Main.mouseItem.TurnToAir();p.trashItem.TurnToAir();}
        private static void Frame(object context,object input){NativeProcessingChecks.Sample(input,true);Call(context,"UpdateRuntime");}
    }
}
