using System;
using System.IO;
using JueMingR.Features.Tools;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsLifecycleChecks
    {
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),mining=Get(host,"Mining"),herbs=Get(host,"Herbs"),use=Get(host,"Use"),runtime=Get(host,"Runtime");
            var originalPlayer=Main.LocalPlayer;var originalWorld=Main.ActiveWorldFileData;
            try
            {
                foreach(bool playerChange in new[]{false,true})
                {
                    var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(2176);p.inventory[12].SetDefaults(213);
                    p.position=new Vector2(640,640);p.itemAnimation=p.itemTime=p.toolTime=0;p.selectedItemState.Select(0);p.selectedItemState.Update();
                    for(int x=25;x<65;x++)for(int y=25;y<65;y++)Main.tile[x,y].ClearEverything();
                    NativeToolsChecks.Tile(30,30,6);NativeToolsChecks.SetMode(host,2,1);NativeToolsChecks.SetMode(host,1,1);
                    Require((bool)Call(mining,"Select",p,30,30,6,false),"lifecycle retained unreachable region");
                    NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"lifecycle pot");NativeToolsChecks.Tile(42,40,84);
                    // Establish the lease with actual ItemCheck. Add a separate
                    // known pending plot as fixture state, not a harvest claim.
                    for(int i=0;i<4 && !(bool)Get(use,"Active");i++)NativeToolsChecks.Frame(context,input);
                    Require((bool)Get(use,"Active") && ((MiningRegion)Get(mining,"Region")).Count==1,"real tool use coexists with retained unreachable mining");
                    NativeToolsChecks.Tile(43,41,78);var pending=(ReplantQueue)Get(herbs,"Pending");Require(pending.Add(43,40,0,(ulong)Get(host,"Tick")),"pending fixture established");
                    long generation=(long)Get(runtime,"Generation");
                    if(playerChange)Main.player[Main.myPlayer]=new Player{active=true,whoAmI=Main.myPlayer,position=p.position,statLife=100,statLifeMax2=100};
                    else Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath,"g09-new-world.wld"),false){UniqueId=Guid.NewGuid()};
                    Call(context,"UpdateRuntime");
                    Require((long)Get(runtime,"Generation")>generation && !(bool)Get(use,"Active") && pending.Count==0 && ((MiningRegion)Get(mining,"Region")).Count==0 && GetOptional(mining,"tool")==null && (int)Get(Get(mining,"Coverage"),"Count")==0,"real identity switch retires old G09 responsibility: player="+playerChange);
                    Require((int)Call(host,"Mode",1)==1 && (int)Call(host,"Mode",2)==1,"identity switch preserves configured modes");
                    // Session retirement must not forcibly rewind the native
                    // animation. Let its ordinary selection override expire.
                    for(int i=0;i<80;i++)NativeToolsChecks.Frame(context,input);
                    var current=Main.LocalPlayer;current.inventory[0].SetDefaults(2176);current.selectedItemState.Select(0);current.selectedItemState.Update();NativeToolsChecks.Tile(42,40,6);
                    Require((bool)Call(mining,"Select",current,42,40,6,false),"new identity accepts a fresh legal trigger");
                    Call(context,"UpdateRuntime");Require(Call(mining,"Choose",current)!=null,"new identity can execute its own fresh mining target");
                    NativeToolsChecks.SetMode(host,1,0);NativeToolsChecks.SetMode(host,2,0);
                }
            }
            finally
            {
                NativeToolsChecks.SetMode(host,1,0);NativeToolsChecks.SetMode(host,2,0);Main.player[Main.myPlayer]=originalPlayer;Main.ActiveWorldFileData=originalWorld;Call(context,"UpdateRuntime");
            }
            Console.WriteLine("PASS G09 real Runtime world/player changes retire lease/pending/region, preserve modes and accept new work.");
        }

        // Called only after the existing real post-mutation exception has
        // established unknown ownership; toggling a boolean is not evidence.
        internal static void Unknown(object context)
        {
            var host=Get(context,"Tools");var ownership=(ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");
            Require((ulong)Get(host,"unknown")!=0 && ownership.IsProtected(0),"actual unknown source before lifecycle change");
            var world=Main.ActiveWorldFileData;bool menu=Main.gameMenu;
            try
            {
                Main.gameMenu=true;Call(context,"UpdateRuntime");Main.gameMenu=false;Call(context,"UpdateRuntime");
                Require((ulong)Get(host,"unknown")!=0 && ownership.IsProtected(0),"same identity resumption retains real unknown protection");
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath,"g09-unknown-new-world.wld"),false){UniqueId=Guid.NewGuid()};Call(context,"UpdateRuntime");
                Require((ulong)Get(host,"unknown")==0 && !ownership.IsProtected(0),"confirmed new identity retires old unknown protection");
            }
            finally{Main.gameMenu=menu;Main.ActiveWorldFileData=world;Call(context,"UpdateRuntime");}
            Console.WriteLine("PASS G09 unknown lifecycle: same identity retains protection; confirmed new world clears it.");
        }
    }
}
