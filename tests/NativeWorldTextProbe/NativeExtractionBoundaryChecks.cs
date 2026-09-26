using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Invalidations happen inside the real use animation, after the host borrows
    // aim and before vanilla reaches placement; neither outcome is stubbed.
    internal static class NativeExtractionBoundaryChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static int mode,placements,drops;
        private static bool changed;
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var audit=new Harmony("JueMingR.Tests.ExtractionBoundary");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeExtractionBoundaryChecks),nameof(Invalidate)));
            audit.Patch(typeof(Player).GetMethod("PlaceThing_Tiles",Flags),prefix:new HarmonyMethod(typeof(NativeExtractionBoundaryChecks),nameof(Placement)));
            audit.Patch(typeof(Player).GetMethod("DropItemFromExtractinator",Flags),prefix:new HarmonyMethod(typeof(NativeExtractionBoundaryChecks),nameof(Drop)));
            try
            {
                for(mode=0;mode<4;mode++)
                {
                    foreach(var item in p.inventory)item.TurnToAir();p.inventory[10].SetDefaults(424);p.inventory[10].stack=5;
                    p.position=new Vector2(640,640);p.itemTime=p.itemAnimation=0;p.selectedItemState.Select(0);p.selectedItemState.Update();
                    NativeExtractionChecks.Machine(42,40,219);changed=false;placements=drops=0;Enable(host,true);
                    for(int frame=0;frame<25 && !changed;frame++)
                    {
                        NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");
                        Main.mouseX=103;Main.mouseY=117;Player.tileTargetX=81;Player.tileTargetY=82;
                        NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
                        if(changed)Require(Main.mouseX==103 && Main.mouseY==117 && Player.tileTargetX==81 && Player.tileTargetY==82,"invalid machine restores only borrowed mouse and tile target: mode="+mode+" actual="+Player.tileTargetX+","+Player.tileTargetY);
                    }
                    Require(changed && placements==0 && drops==0 && p.inventory[10].stack==5,"missing/frame-broken/unloaded/out-of-reach machine cannot place or consume: "+mode);
                    Enable(host,false);for(int frame=0;frame<40;frame++)NativeExtractionChecks.Frame(context,input);
                    Require(p.selectedItem==0 && !p.controlUseItem,"machine failure returns original selection and control");
                    for(int y=40;y<43;y++)for(int x=42;x<45;x++)if(Main.tile[x,y]==null)Main.tile[x,y]=new Tile();
                }
            }
            finally{foreach(var method in audit.GetPatchedMethods())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            p.position=new Vector2(640,640);NativeExtractionChecks.Machine(42,40,642);Enable(host,true);
            for(int frame=0;frame<120 && !p.inventory[10].IsAir && p.inventory[10].stack>0;frame++)NativeExtractionChecks.Frame(context,input);
            Require(p.inventory[10].IsAir || p.inventory[10].stack==0,"repaired/replaced machine resumes real consumption");
            p.inventory[10].SetDefaults(424);p.inventory[10].stack=4;
            for(int frame=0;frame<30 && p.inventory[10].stack==4;frame++)NativeExtractionChecks.Frame(context,input);
            Require(p.inventory[10].stack<4,"refilled material resumes without toggling");
            p.inventory[5].SetDefaults(3507);p.selectedItemState.Select(5);
            Enable(host,false);for(int frame=0;frame<45;frame++)NativeExtractionChecks.Frame(context,input);
            Require(p.selectedItem==5,"new player weapon selection beats stale automatic return");
            p.selectedItemState.Select(0);p.selectedItemState.Update();
            Console.WriteLine("PASS G08 extraction boundaries: in-use disappearance/frame/unloaded/reach changes cannot place, own aim returns, repair/refill resumes and player selection wins.");
        }
        private static void Invalidate()
        {
            if(changed)return;changed=true;int x=Player.tileTargetX,y=Player.tileTargetY;
            if(mode==0){Main.tile[x,y].active(false);Main.tile[x,y+1].active(true);Main.tile[x,y+1].type=1;}
            if(mode==1)Main.tile[x,y].frameX++;
            if(mode==2)Main.tile[x,y]=null;
            if(mode==3)Main.LocalPlayer.position=new Vector2(1100,1100);
        }
        private static void Placement(){placements++;}
        private static void Drop(){drops++;}
        internal static void Enable(object host,bool value)
        {Call(host,"Set",1,value);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",1) && (bool)Call(host,"Value",1)==value;});}
    }
}
