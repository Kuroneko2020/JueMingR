using System;
using System.Reflection;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input");
            Require((bool)Get(host,"Available"),"G09 hooks installed: "+GetOptional(host,"SetupError"));
            var settings=(ToolSettings[])Get(host,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings[0].Loaded && settings[1].Loaded && settings[2].Loaded;});
            Require(settings[0].Ready && settings[1].Ready && settings[2].Ready && settings[0].Value.Mode==0 && settings[1].Value.Mode==0 && settings[2].Value.Mode==0,"G09 defaults off in complete real Host");
            var p=Main.LocalPlayer;p.position=new Vector2(640,640);p.inventory[0].SetDefaults(Terraria.ID.ItemID.CopperPickaxe);p.selectedItemState.Select(0);p.selectedItemState.Update();
            for(int i=0;i<40;i++)Frame(context,input);
            Require((long)Get(Get(host,"Capture"),"Probes")==0 && (long)Get(Get(host,"Herbs"),"Probes")==0 && (long)Get(Get(host,"Mining"),"OverlayChecks")==0,"all three OFF consumers do zero candidate/overlay work through actual native hooks");
            var assembly=host.GetType().Assembly;
            var npc=Main.npc[0];npc.SetDefaults(13);npc.active=true;npc.life=100;npc.boss=false;Call(Get(host,"Npcs"),"BeginTick");
            Require((bool)Call(Get(host,"Capture"),"Boss"),"EoW segment pauses capture even with native boss=false");
            npc.active=false;Call(Get(host,"Npcs"),"BeginTick");Main.pumpkinMoon=true;
            Require(!(bool)Call(Get(host,"Capture"),"Boss"),"event without a boss does not pause capture");Main.pumpkinMoon=false;
            // A real native override is temporary. It must not be mistaken for
            // a user's choice and destroy an unrelated retained mining region.
            SetMode(host,2,1);Tile(42,40,6);p.inventory[12].SetDefaults(1991);
            object mining=Get(host,"Mining");Require((bool)Call(mining,"Select",p,42,40,6,false),"manual region selected");
            Require(settings[2].Set(settings[2].Value.WithMode(2)) && settings[2].Busy,"non-off mode save accepted asynchronously");Call(mining,"Update");
            Require(((MiningRegion)Get(mining,"Region")).Count==1,"saving another enabled mining mode pauses actions without deleting the selected region");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings[2].Busy;});
            object selection=p.selectedItemState;
            typeof(Player.SelectedItemState).GetMethod("OverrideSelection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(selection,new object[]{12});p.selectedItemState=(Player.SelectedItemState)selection;
            Call(mining,"Update");
            Require(((MiningRegion)Get(mining,"Region")).Count==1,"native temporary tool override preserves mining region");
            SetMode(host,2,0);for(int i=0;i<40;i++)Frame(context,input);
            NativeHerbChecks.Run(context);NativeMiningChecks.Run(context);NativeFishingBorrowChecks.Run(context);NativeToolsIntegrationChecks.Cpu(context);
            Console.WriteLine("PASS G09 real Host defaults, Boss classification and native temporary selection isolation.");
        }
        internal static void SetMode(object host,int domain,int mode)
        {Call(host,"Set",domain,mode);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Call(host,"Controls",domain);});Require((int)Call(host,"Mode",domain)==mode,"tool mode committed "+domain);}
        internal static void Frame(object context,object input){NativeExtractionChecks.Frame(context,input);}
        internal static void Tile(int x,int y,int type){var t=Main.tile[x,y];t.ClearEverything();t.type=(ushort)type;t.active(true);}
    }
}
