using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ObjectData;
using HarmonyLib;
using System.Reflection;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExtractionChecks
    {
        private static bool mismatchedTarget;
        internal static void Run(object context,object host,object input)
        {
            TileObjectData.Initialize();var p=Main.LocalPlayer;
            foreach(var item in p.inventory)item.TurnToAir();
            p.inventory[10].SetDefaults(424);p.inventory[10].stack=5;p.position=new Vector2(640,640);p.mouseInterface=false;p.itemTime=p.itemAnimation=0;
            Main.playerInventory=false;Main.mouseItem.TurnToAir();p.selectedItemState.Select(0);p.selectedItemState.Update();
            Machine(42,40,219);
            Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",1);});
            int before=Main.item.Count(w=>w!=null && w.active);
            for(int frame=0;frame<110 && p.inventory[10].stack>0;frame++)Frame(context,input);
            Require(p.inventory[10].IsAir || p.inventory[10].stack==0,"real ItemCheck consumes extraction material without a mouse hold; remaining="+p.inventory[10].stack+" error="+GetOptional(host,"Error"));
            Require(Main.item.Count(w=>w!=null && w.active)>before,"native extraction creates actual local world products");
            Call(host,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1);});
            for(int frame=0;frame<35;frame++)Frame(context,input);
            Require(p.selectedItem==0 && !p.controlUseItem && !Main.mouseLeft,"extraction restores its selection and input");
            Require(GetOptional(host,"Error")==null,"normal depletion and delayed native Air cleanup are confirmed, not an unknown result");
            NativeExtractionPickupChecks.Run(context,host,input);
            Matrix(context,host,input);
            StableMachine(context,host,input);
            NativeExtractionBoundaryChecks.Run(context,host,input);
            NativeExtractionTradeChecks.Run(context,host,input);
            Console.WriteLine("PASS G08 extraction: actual native ItemCheck, material consumption, world products and own input return.");
        }
        private static void StableMachine(object context,object host,object input)
        {
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();p.position=new Vector2(640,640);p.inventory[10].SetDefaults(424);p.inventory[10].stack=100;Machine(42,40,219);
            var field=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Processing.ExtractionMachine").GetField("Searches",BindingFlags.Static|BindingFlags.NonPublic);
            long before=(long)field.GetValue(null);Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",1);});
            for(int i=0;i<180;i++)Frame(context,input);
            Require(p.inventory[10].stack<95,"stable machine has real repeated consumes");
            Require((long)field.GetValue(null)-before<=1,"stable reachable machine is reused instead of repeated full-area searches");
            Call(host,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1);});for(int i=0;i<40;i++)Frame(context,input);
            before=(long)field.GetValue(null);foreach(var item in p.inventory)item.TurnToAir();Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",1);});
            for(int i=0;i<180;i++)Frame(context,input);Require(before==(long)field.GetValue(null),"no material does zero machine searches");
            p.inventory[10].SetDefaults(424);p.inventory[10].stack=100;for(int y=40;y<43;y++)for(int x=42;x<45;x++)Main.tile[x,y].active(false);
            before=(long)field.GetValue(null);for(int i=0;i<180;i++)Frame(context,input);
            Require((long)field.GetValue(null)-before<=30 && p.inventory[10].stack==100,"known missing machine probes at most once per six updates without native consumption");
            Call(host,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1);});
            before=(long)field.GetValue(null);for(int i=0;i<600;i++)Frame(context,input);Require(before==(long)field.GetValue(null),"disabled extraction does no machine work even with materials present");
        }
        private static void Matrix(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var extraction=Get(host,"Extraction");
            int[] materials=Enumerable.Range(1,Terraria.ID.ItemID.Count-1).Where(t=>Terraria.ID.ItemID.Sets.ExtractinatorMode[t]>=0).ToArray();
            Require(materials.Length==16 && materials.Select(t=>Terraria.ID.ItemID.Sets.ExtractinatorMode[t]).Distinct().Count()==7,"native .8 auto input is exactly 16 types / 7 modes");
            var audit=new Harmony("JueMingR.Tests.ExtractionTarget");
            var drop=typeof(Player).GetMethod("DropItemFromExtractinator",BindingFlags.Instance|BindingFlags.NonPublic);
            audit.Patch(drop,prefix:new HarmonyMethod(typeof(NativeExtractionChecks),nameof(CheckTarget)));
            try
            {
                foreach(int machine in new[]{219,642})foreach(int material in materials)
                {
                    foreach(var item in p.inventory)item.TurnToAir();p.inventory[10].SetDefaults(material);p.inventory[10].stack=2;p.position=new Vector2(640,640);
                    p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemAnimation=p.itemTime=0;Machine(42,40,machine);
                    Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Value",1);});
                    Frame(context,input);p.position=new Vector2(688,640); // Next use selects another legal part; no stale product position.
                    for(int f=0;f<140 && !p.inventory[10].IsAir && p.inventory[10].stack>0;f++)Frame(context,input);
                    Require(p.inventory[10].IsAir || p.inventory[10].stack==0,"full native material consumed: "+material+" machine="+machine);
                    Require(!mismatchedTarget,"drop mouse position follows current nearest machine part");
                    Call(host,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1);});for(int f=0;f<40;f++)Frame(context,input);
                    Require(!(bool)Get(extraction,"Active") && GetOptional(extraction,"Failure")==null,"completed extraction retires own selection/lease");
                }
            }
            finally{audit.Unpatch(drop,HarmonyPatchType.All,audit.Id);}
            Console.WriteLine("PASS G08 extraction matrix: all 16 native inputs on both machines, movement changes nearest part without stale output target.");
        }
        private static void CheckTarget()
        {var world=Main.ReverseGravitySupport(Main.MouseScreen)+Main.screenPosition;if(Math.Abs(world.X-(Player.tileTargetX*16+8))>1 || Math.Abs(world.Y-(Player.tileTargetY*16+8))>1)mismatchedTarget=true;}
        internal static void Frame(object context,object input)
        {NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");NativeQuickItemChecks.NativeFrame(Main.LocalPlayer);Call(context,"UpdateRuntime");}
        internal static void Machine(int left,int top,int type)
        {for(int y=0;y<3;y++)for(int x=0;x<3;x++){var t=Main.tile[left+x,top+y];t.active(true);t.type=(ushort)type;t.frameX=(short)(18*x);t.frameY=(short)(18*y);}}
    }
}
