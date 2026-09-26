using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeHerbChecks
    {
        internal static void Run(object context)
        {
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            typeof(Main).GetMethod("Initialize_TileAndNPCData2",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            TileObjectData.Initialize();var host=Get(context,"Tools");var input=Get(context,"Input");var p=Main.LocalPlayer;
            Require(Main.tileAlch[82] && Main.tileAlch[83] && Main.tileAlch[84] && Main.tileCut[84],"real native alchemy and tile cut metadata");
            // KillTile's real bait/drop path calls Player.FindClosest across
            // every native slot, including inactive players.
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            foreach(int tool in new[]{ItemID.StaffofRegrowth,ItemID.AcornAxe})foreach(int support in new[]{78,380})for(int style=0;style<7;style++)
            {
                NativeToolsChecks.SetMode(host,1,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(tool);p.position=new Vector2(640,640);p.itemAnimation=p.itemTime=p.toolTime=0;p.controlUseItem=p.channel=false;
                p.selectedItemState.Select(0);p.selectedItemState.Update();Main.mouseLeft=false;
                for(int x=37;x<48;x++)for(int y=37;y<46;y++)Main.tile[x,y].ClearEverything();
                NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,support,mute:true,forced:true,plr:0),"real native container placed "+support);
                NativeToolsChecks.Tile(42,40,84);Main.tile[42,40].frameX=(short)(style*18);
                bool protectionCase=tool==ItemID.StaffofRegrowth && support==78 && style==0;var items=Get(host,"Items");
                if(protectionCase)
                {
                    p.inventory[0].SetDefaults(ItemID.CopperPickaxe);p.inventory[20].SetDefaults(9);p.trashItem.TurnToAir();
                    Call(items,"Change",new JueMingR.Features.Items.ItemAutomationSettings(false,false,true,new int[0],new[]{p.inventory[0].type,9},false));NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
                }
                NativeToolsChecks.SetMode(host,1,1);
                for(int f=0;f<100 && Main.tile[42,40].type!=82;f++)NativeToolsChecks.Frame(context,input);
                var t=Main.tile[42,40];
                Require(t.active() && t.type==82 && t.frameX/18==style,"native ItemCheck free replant tool="+tool+" support="+support+" style="+style+" actual="+t.active()+"/"+t.type+"/"+t.frameX+" error="+GetOptional(host,"Error"));
                Require(!p.inventory.Any(i=>i.type>=307 && i.type<=312 || i.type==2357),"no inventory seed source was required or fabricated");
                Require((int)Get(Get(Get(host,"Herbs"),"Pending"),"Count")==0,"successful free native replant creates no seed intent");
                if(protectionCase)
                {
                    for(int f=0;f<35;f++)NativeToolsChecks.Frame(context,input);
                    Require(p.inventory[0].type==ItemID.CopperPickaxe && p.inventory[20].IsAir && p.trashItem.type==9,"native automatic discard preserves original held pick during staff borrow while processing unrelated wood");
                    Call(items,"Change",JueMingR.Features.Items.ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
                }
            }
            var pending=(JueMingR.Features.Tools.ReplantQueue)Get(Get(host,"Herbs"),"Pending");
            Main.tile[42,40].ClearEverything();pending.Add(42,40,0,(ulong)Get(host,"Tick"));Main.tile[42,41].inActive(true);
            Call(Get(host,"Herbs"),"Update");Require(pending.Count==1,"temporarily actuated support retains finite seed fallback");Main.tile[42,41].inActive(false);
            p.inventory[17].SetDefaults(307);p.inventory[17].stack=1;var herbSettings=((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings"))[1];
            Require(herbSettings.Set(herbSettings.Value.WithMode(1)) && herbSettings.Busy,"same enabled mode saving window");
            Require((bool)Call(Get(host,"Herbs"),"ProtectSeed",p.inventory[17]),"pending exact seed protection survives reliable-save pause");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !herbSettings.Busy;});
            NativeToolsChecks.SetMode(host,1,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(ItemID.StaffofRegrowth);p.selectedItemState.Select(0);p.selectedItemState.Update();
            NativeToolsChecks.Tile(42,40,84);Main.tile[42,40].frameX=0;Main.tile[42,40].liquid=255;Main.tile[42,40].liquidType(0);NativeToolsChecks.SetMode(host,1,1);
            for(int f=0;f<100 && Main.tile[42,40].active();f++)NativeToolsChecks.Frame(context,input);
            Require(!Main.tile[42,40].active() && pending.Count==1,"real harvest can leave a water-blocked empty plot with no immediate seed source");
            for(int f=0;f<70;f++)NativeToolsChecks.Frame(context,input);Require(pending.Count==1,"absent seed does not discard finite delayed opportunity");
            p.inventory[17].SetDefaults(ItemID.DaybloomSeeds);p.inventory[17].stack=2;Main.tile[42,40].liquid=0;
            for(int f=0;f<100 && !Main.tile[42,40].active();f++)NativeToolsChecks.Frame(context,input);
            Require(Main.tile[42,40].active() && Main.tile[42,40].type==82 && Main.tile[42,40].frameX==0 && p.inventory[17].stack==1,"late seed uses real native placement and consumes exactly one matching seed");
            for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);Require(pending.Count==0 && p.selectedItem==0,"successful fallback releases seed and returns selection");NativeToolsChecks.SetMode(host,1,0);
            Console.WriteLine("PASS G09 herbs: two real tools x seven styles x two native containers, actual ItemCheck free replant without inventory seeds.");
            Console.WriteLine("PASS G09 real water-blocked harvest and delayed native seed consumption after material arrives.");
        }
    }
}
