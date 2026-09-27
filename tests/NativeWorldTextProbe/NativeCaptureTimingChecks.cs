using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCaptureTimingChecks
    {
        private static int update,firstUse;
        private sealed class Flight
        {
            internal string Name;internal Vector2 Start,Velocity,Acceleration=Vector2.Zero;
            internal int Reverse=-1;internal Vector2 PlayerVelocity=Vector2.Zero;
            internal int Net=1991,Direction=1;internal float Gravity=1;
            internal bool NativeAi,Obstacle;
            internal Vector2 At(int t){return Start+Velocity*(Reverse<0 || t<=Reverse?t:2*Reverse-t)+Acceleration*(t*t*.5f);}
            internal Vector2 Motion(int t){return At(t+1)-At(t);}
        }
        internal static void Run(object context,ProbeGraphics graphics)
        {
            graphics.LoadItemTextures(new[]{0,1991,3183,4821,2289});
            graphics.LoadTexture("Npc","Images/NPC_46",46);
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(typeof(Item).GetMethod("GetDrawHitbox"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            object host=Get(context,"Tools"),input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return ((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings"))[0].Loaded;});
            var watch=new Harmony("JueMingR.Tests.G09CaptureTiming");
            var start=typeof(Player).GetMethod("ItemCheck_StartActualUse",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            watch.Patch(start,postfix:new HarmonyMethod(typeof(NativeCaptureTimingChecks),nameof(Started)));
            int misses=0,possible=0;
            try
            {
                var flights=new List<Flight>{
                    new Flight{Name="cross-from-behind",Start=new Vector2(580,675),Velocity=new Vector2(3,0)},
                    new Flight{Name="late-phase-entry",Start=new Vector2(740,650),Velocity=new Vector2(-3,0)},
                    new Flight{Name="fast-crossing",Start=new Vector2(740,650),Velocity=new Vector2(-6,0)},
                    new Flight{Name="early-phase-entry",Start=new Vector2(740,620),Velocity=new Vector2(-3,0)},
                    new Flight{Name="outward",Start=new Vector2(650,620),Velocity=new Vector2(3,0)},
                    new Flight{Name="falling-entry",Start=new Vector2(680,550),Velocity=new Vector2(-1,4)},
                    new Flight{Name="turning",Start=new Vector2(700,645),Velocity=new Vector2(-3,0),Reverse=12},
                    new Flight{Name="jumping",Start=new Vector2(715,660),Velocity=new Vector2(-2,-4),Acceleration=new Vector2(0,.3f)},
                    new Flight{Name="player-pursuit",Start=new Vector2(730,650),Velocity=new Vector2(1,0),PlayerVelocity=new Vector2(3,0)},
                    new Flight{Name="native-bunny-ground",Start=new Vector2(725,668),Velocity=new Vector2(-1,0),NativeAi=true},
                    new Flight{Name="native-bunny-obstacle",Start=new Vector2(710,668),Velocity=new Vector2(-1,0),NativeAi=true,Obstacle=true}
                };
                foreach(int net in new[]{1991,3183,4821})foreach(int dir in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})
                    flights.Add(new Flight{Name="symmetric-moving",Start=new Vector2(740,650),Velocity=new Vector2(-3,0),Net=net,Direction=dir,Gravity=gravity});
                foreach(var flight in flights)
                {
                    int earliest=-1,bestStart=-1;
                    for(int offset=0;offset<25;offset++)
                    {
                        if(earliest>=0 && offset>earliest)break;
                        int caught=Arm(context,host,input,flight.Net,flight,false,offset);
                        if(caught>=0 && (earliest<0 || caught<earliest)){earliest=caught;bestStart=offset;}
                    }
                    if(earliest<0){Console.WriteLine("G09 capture native-unreachable within tested start phases: "+flight.Name);Require(!flight.NativeAi,"native AI reference must exercise reachable trajectory");continue;}possible++;
                    int actual=Arm(context,host,input,flight.Net,flight,true,0),started=firstUse;
                    Console.WriteLine("G09 capture timing "+flight.Name+" net="+flight.Net+" direction="+flight.Direction+" gravity="+flight.Gravity+" nativeStart="+bestStart+" nativeCatch="+earliest+" autoStart="+started+" autoCatch="+actual);
                    if(actual<0 || !flight.NativeAi && flight.Reverse<0 && flight.Acceleration==Vector2.Zero && actual>earliest+1)misses++;
                }
                Require(possible>0 && misses==0,"dynamic capture missed "+misses+"/"+possible+" native-reachable trajectories");
            }
            finally{watch.Unpatch(start,HarmonyPatchType.All,watch.Id);NativeToolsChecks.SetMode(host,0,0);}
        }
        private static void Started(Player __instance,Item __0){if(__instance.whoAmI==0 && (__0.type==1991 || __0.type==3183 || __0.type==4821) && firstUse<0)firstUse=update;}
        private static int Arm(object context,object host,object input,int net,Flight flight,bool automatic,int offset)
        {
            var p=Main.LocalPlayer;NativeToolsChecks.SetMode(host,0,0);
            foreach(var n in Main.npc)n.active=false;
            for(int i=0;i<42;i++)NativeToolsChecks.Frame(context,input);
            foreach(var item in p.inventory)item.TurnToAir();foreach(var q in Main.projectile)q.active=false;
            // Each arm is a fresh isolated world. Real catches drop real items;
            // retaining 400 prior arms would enter unrelated emergency stacking.
            foreach(var item in Main.item)if(item!=null)item.TurnToAir();
            for(int x=30;x<60;x++)for(int y=30;y<48;y++)Main.tile[x,y].ClearEverything();
            if(flight.NativeAi){for(int x=30;x<60;x++)NativeToolsChecks.Tile(x,43,1);if(flight.Obstacle)NativeToolsChecks.Tile(43,42,1);Main.rand=new Terraria.Utilities.UnifiedRandom(7123);Main.dayTime=true;Main.time=27000;Main.raining=false;}
            p.inventory[12].SetDefaults(net);p.itemAnimation=p.itemTime=0;p.position=new Vector2(640,640);p.velocity=flight.PlayerVelocity*new Vector2(flight.Direction,flight.Gravity);p.direction=flight.Direction;p.gravDir=flight.Gravity;p.meleeScaleGlove=false;
            p.selectedItemState.Select(automatic?0:12);p.selectedItemState.Update();p.controlUseItem=false;p.releaseUseItem=true;
            var target=Main.npc[0];target.SetDefaults(46);target.whoAmI=0;target.active=true;target.position=Position(flight,target,0);target.velocity=flight.Motion(0)*new Vector2(flight.Direction,flight.Gravity);target.life=target.lifeMax;
            if(flight.NativeAi){target.direction=-1;target.ai[0]=1;target.ai[1]=400;target.target=0;}
            Call(Get(host,"Npcs"),"BeginTick");if(automatic)NativeToolsChecks.SetMode(host,0,1);
            firstUse=-1;
            for(update=0;update<60;update++)
            {
                NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");
                // Player actions precede NPC movement in Main.Update. The
                // controlled trajectory supplies future positions only after
                // the real catch consumer; neither arm changes native timers.
                p.position=new Vector2(640,640)+flight.PlayerVelocity*new Vector2(flight.Direction,flight.Gravity)*update;
                if(!automatic && update>=offset && update<offset+1)
                {Main.mouseLeft=true;PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseX=(int)target.Center.X;Main.mouseY=(int)target.Center.Y;}
                NativeQuickItemChecks.NativeFrame(p);
                if(!target.active)return update;
                if(flight.NativeAi)target.UpdateNPC(0);
                else{target.position=Position(flight,target,update+1);target.velocity=flight.Motion(update+1)*new Vector2(flight.Direction,flight.Gravity);}
                Call(context,"UpdateRuntime");
            }
            return -1;
        }
        private static Vector2 Position(Flight flight,NPC target,int t)
        {var position=flight.At(t);if(flight.Direction<0)position.X=1300-position.X-target.width;if(flight.Gravity<0)position.Y=1322-position.Y-target.height;return position;}
    }
}
