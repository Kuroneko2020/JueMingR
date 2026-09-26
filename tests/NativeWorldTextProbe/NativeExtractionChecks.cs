using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExtractionChecks
    {
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
            Console.WriteLine("PASS G08 extraction: actual native ItemCheck, material consumption, world products and own input return.");
        }
        internal static void Frame(object context,object input)
        {NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");NativeQuickItemChecks.NativeFrame(Main.LocalPlayer);Call(context,"UpdateRuntime");}
        internal static void Machine(int left,int top,int type)
        {for(int y=0;y<3;y++)for(int x=0;x<3;x++){var t=Main.tile[left+x,top+y];t.active(true);t.type=(ushort)type;t.frameX=(short)(18*x);t.frameY=(short)(18*y);}}
    }
}
