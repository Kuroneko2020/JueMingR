using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using JueMingR.Features.Tools;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsWorkloadChecks
    {
        private static long reads,eligibility;
        private static void Read(){reads++;}
        private static void Eligible(){eligibility++;}
        private static bool SkipAchievement(){return false;}
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),mining=Get(host,"Mining");var p=Main.LocalPlayer;
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return ((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings")).All(s=>s.Loaded);});
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(2176);p.position=new Vector2(640,640);p.selectedItemState.Select(0);p.selectedItemState.Update();
            for(int x=30;x<62;x++)for(int y=30;y<46;y++)NativeToolsChecks.Tile(x,y,6);
            NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,42,40,6,false),"full workload region");
            Call(context,"UpdateRuntime");
            var count=new Harmony("JueMingR.Tests.G09Workload");var assembly=host.GetType().Assembly;
            var read=assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation").GetMethod("ReadCurrent",BindingFlags.Static|BindingFlags.NonPublic);
            var progress=assembly.GetType("JueMingR.TerrariaHost.Tools.MiningEligibility").GetMethod("CanProgress",BindingFlags.Static|BindingFlags.NonPublic);
            count.Patch(read,prefix:new HarmonyMethod(typeof(NativeToolsWorkloadChecks),nameof(Read)));
            count.Patch(progress,prefix:new HarmonyMethod(typeof(NativeToolsWorkloadChecks),nameof(Eligible)));
            try
            {
                long before=(long)Get(mining,"OverlayChecks");reads=eligibility=0;
                for(int i=0;i<64;i++){NativeQuickItemChecks.Sample(input,new Keys[0]);Call(context,"UpdateRuntime");}
                long checks=(long)Get(mining,"OverlayChecks")-before;
                Console.WriteLine("G09 mining warm 512 cells x64 updates: overlay="+checks+" eligibility="+eligibility+" ReadCurrent="+reads);
                Require(checks<=24*64 && eligibility<=24*64,"stable coverage must not run full region eligibility each update");
                var region=(MiningRegion)Get(mining,"Region");var coverage=Get(mining,"Coverage");
                int index=Enumerable.Range(0,region.Count).First(i=>region[i].X==42 && region[i].Y==40);
                Require((bool)Call(coverage,"Green",index),"reachable interior cell remains green");
                Main.tile[42,40].inActive(true);Call(context,"UpdateRuntime");Require(!(bool)Call(coverage,"Green",index),"actuation invalidates coverage immediately");
                Main.tile[42,40].inActive(false);Call(context,"UpdateRuntime");Require((bool)Call(coverage,"Green",index),"unactuation restores green immediately");
                p.noBuilding=true;Call(context,"UpdateRuntime");Require(!(bool)Call(coverage,"Green",index),"capability loss updates coverage");p.noBuilding=false;
                p.position+=new Vector2(160,0);Call(context,"UpdateRuntime");Require(!(bool)Call(coverage,"Green",index) && region.Count==512,"unreachable cells stay selected/red");
                p.position-=new Vector2(160,0);Call(context,"UpdateRuntime");Require((bool)Call(coverage,"Green",index),"movement back restores coverage without stale negatives");
                for(int x=30;x<62;x++)for(int y=30;y<46;y++)Main.tile[x,y].ClearEverything();
                Set(mining,"manualHeld",true);Call(context,"UpdateRuntime");
                Require(region.Count==0 && GetOptional(mining,"tool")==null && (int)Get(coverage,"Count")==0 && (bool)Get(mining,"manualHeld") && (int)Call(host,"Mode",2)==1,"ordinary completion retires references, preserves mode and physical gesture");
                reads=eligibility=0;for(int i=0;i<240;i++){NativeQuickItemChecks.Sample(input,new Keys[0]);Call(context,"UpdateRuntime");}
                Require(reads==0 && eligibility==0,"completed mining has zero tile/eligibility work while mode stays on");
                NativeToolsChecks.Tile(42,40,6);Require((bool)Call(mining,"Select",p,42,40,6,false),"fresh trigger works after normal completion");
            }
            finally{count.Unpatch(read,HarmonyPatchType.All,count.Id);count.Unpatch(progress,HarmonyPatchType.All,count.Id);NativeToolsChecks.SetMode(host,2,0);}
            Herbs(context,host,input);
            Boss(context,host,input);
            IdleConsumers(context,host,input);
        }
        private static void IdleConsumers(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var mining=Get(host,"Mining");var herbs=Get(host,"Herbs");
            var assembly=host.GetType().Assembly;var counter=new Harmony("JueMingR.Tests.G09IdleConsumers");
            var read=assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation").GetMethod("ReadCurrent",BindingFlags.Static|BindingFlags.NonPublic);
            var progress=assembly.GetType("JueMingR.TerrariaHost.Tools.MiningEligibility").GetMethod("CanProgress",BindingFlags.Static|BindingFlags.NonPublic);
            counter.Patch(read,prefix:new HarmonyMethod(typeof(NativeToolsWorkloadChecks),nameof(Read)));
            counter.Patch(progress,prefix:new HarmonyMethod(typeof(NativeToolsWorkloadChecks),nameof(Eligible)));
            var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",BindingFlags.Static|BindingFlags.Public);
            // Match the existing native mining fixture: do not touch account
            // achievement storage; native hits, timers and tile drops remain.
            counter.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeToolsWorkloadChecks),nameof(SkipAchievement)));
            bool miningBound,herbBound;
            try
            {
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(3509);p.position=new Vector2(640,640);p.selectedItemState.Select(0);p.selectedItemState.Update();
                for(int x=25;x<65;x++)for(int y=25;y<65;y++)Main.tile[x,y].ClearEverything();
                for(int x=30;x<62;x++)for(int y=30;y<46;y++)NativeToolsChecks.Tile(x,y,107);
                NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,42,40,107,false),"512 retained cells beyond copper pick power");
                NativeToolsChecks.Frame(context,input);reads=eligibility=0;long intents=(long)Get(mining,"IntentCreations");
                for(int i=0;i<240;i++)NativeToolsChecks.Frame(context,input);
                Console.WriteLine("G09 actual idle native selection 512 low-power cells x240: eligibility="+eligibility+" ReadCurrent="+reads);
                miningBound=eligibility<=24*240 && ((MiningRegion)Get(mining,"Region")).Count==512;
                Require((long)Get(mining,"IntentCreations")==intents,"no idle mining intent/closure construction");
                p.inventory[0].pick=200;
                for(int i=0;i<35 && ((MiningRegion)Get(mining,"Region")).Count==512;i++)NativeToolsChecks.Frame(context,input);
                Require(((MiningRegion)Get(mining,"Region")).Count<512,"real changed pick power restores execution without stale negative candidates");
                NativeToolsChecks.SetMode(host,2,0);
                for(int i=0;i<50;i++)NativeToolsChecks.Frame(context,input);
                for(int x=25;x<65;x++)for(int y=25;y<65;y++)Main.tile[x,y].ClearEverything();
                p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(213);
                // A completed ordinary staff harvest leaves these immature
                // plants. Exercise the full selected-item/ItemCheck path.
                for(int x=31;x<=51;x++)for(int y=31;y<=51;y++)NativeToolsChecks.Tile(x,y,82);
                NativeToolsChecks.SetMode(host,1,1);NativeToolsChecks.Frame(context,input);reads=eligibility=0;intents=(long)Get(herbs,"IntentCreations");
                for(int i=0;i<240;i++)NativeToolsChecks.Frame(context,input);
                Console.WriteLine("G09 actual idle native selection 441 immature plants x240: ReadCurrent="+reads);
                herbBound=reads<=24*240*3;
                Require((long)Get(herbs,"IntentCreations")==intents,"no immature-field intent/closure construction");
                NativeToolsChecks.SetMode(host,1,0);
            }
            finally{counter.Unpatch(read,HarmonyPatchType.All,counter.Id);counter.Unpatch(progress,HarmonyPatchType.All,counter.Id);counter.Unpatch(achievement,HarmonyPatchType.All,counter.Id);}
            Require(miningBound,"idle mining selection must share bounded negative eligibility instead of repeating 512 checks per native update");
            Require(herbBound,"immature plants must share bounded negative discovery instead of full candidate reads every probe");
            HerbEnvironment(context,host,input);
        }
        private static void HerbEnvironment(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var herbs=Get(host,"Herbs");
            for(int x=25;x<65;x++)for(int y=25;y<65;y++)Main.tile[x,y].ClearEverything();
            NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"environment pot");
            NativeToolsChecks.SetMode(host,1,1);bool day=Main.dayTime,rain=Main.raining,blood=Main.bloodMoon,remix=Main.remixWorld;int moon=Main.moonPhase;float cloud=Main.cloudAlpha;double time=Main.time,surface=Main.worldSurface;
            try
            {
                foreach(int style in new[]{0,1,3,4,5,2,6})
                {
                    Main.dayTime=style!=0;Main.bloodMoon=false;Main.moonPhase=1;Main.raining=false;Main.cloudAlpha=0;Main.time=100;Main.remixWorld=false;Main.worldSurface=50;
                    NativeToolsChecks.Tile(42,40,83);Main.tile[42,40].frameX=(short)(style*18);
                    // Invalidating the field models a fresh arrival; subsequent
                    // changes must invalidate through production dependencies.
                    Call(Get(herbs,"Field"),"Clear");Require(!(bool)Call(herbs,"Ready",p),"dormant herb style "+style);
                    if(style==0)Main.dayTime=true;else if(style==1)Main.dayTime=false;else if(style==3){Main.dayTime=false;Main.bloodMoon=true;}
                    else if(style==4)Main.cloudAlpha=.1f;else if(style==5)Main.time=40501;else NativeToolsChecks.Tile(42,40,84);
                    if(style==2 || style==6){Main.tile[42,40].frameX=(short)(style*18);for(int i=0;i<19;i++){NativeQuickItemChecks.Sample(input,new Keys[0]);Call(herbs,"Update");}}
                    Require((bool)Call(herbs,"Ready",p),"native environment/maturity change restores herb style "+style);
                }
            }
            finally{Main.dayTime=day;Main.raining=rain;Main.bloodMoon=blood;Main.remixWorld=remix;Main.moonPhase=moon;Main.cloudAlpha=cloud;Main.time=time;Main.worldSurface=surface;NativeToolsChecks.SetMode(host,1,0);}
            Console.WriteLine("PASS G09 dormant herb candidates: native day/night, blood moon, cloud/rain predicate, time threshold and 83-to-84 recovery.");
        }
        private static void Herbs(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var herbs=Get(host,"Herbs");var field=Get(herbs,"Field");
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(213);p.selectedItemState.Select(0);p.selectedItemState.Update();
            for(int x=25;x<65;x++)for(int y=25;y<65;y++)Main.tile[x,y].ClearEverything();
            NativeToolsChecks.SetMode(host,1,1);NativeToolsChecks.Frame(context,input);
            long before=(long)Get(field,"TileReads"),slots=(long)Get(herbs,"ToolSlotsVisited");
            for(int i=0;i<240;i++)NativeToolsChecks.Frame(context,input);
            long visits=(long)Get(field,"TileReads")-before,inventory=(long)Get(herbs,"ToolSlotsVisited")-slots;
            Console.WriteLine("G09 empty herbs x240: tileReads="+visits+" toolSlots="+inventory);
            Require(visits==24*240 && inventory<=50*9,"empty field distributed exploration with retained exact tool");
            p.position+=new Vector2(16,0);before=(long)Get(field,"TileReads");NativeToolsChecks.Frame(context,input);
            Require((long)Get(field,"TileReads")-before==441,"moving a tile refreshes real field immediately");p.position-=new Vector2(16,0);NativeToolsChecks.Frame(context,input);
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            typeof(Main).GetMethod("Initialize_TileAndNPCData2",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,null);TileObjectData.Initialize();
            NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"new external pot");NativeToolsChecks.Tile(42,40,84);
            int latency=0;while(latency<24 && Main.tile[42,40].type==84){NativeToolsChecks.Frame(context,input);latency++;}
            Require(Main.tile[42,40].type==82 && latency<=23,"new plant discovered and actually harvested within distributed scan plus existing selection probe");
            for(int i=0;i<40;i++)NativeToolsChecks.Frame(context,input);
            p.inventory[12].TurnToAir();NativeToolsChecks.Frame(context,input);before=(long)Get(field,"TileReads");
            for(int i=0;i<120;i++)NativeToolsChecks.Frame(context,input);
            Require((long)Get(field,"TileReads")==before && (int)Get(field,"Count")==0,"removing last tool stops field reads and releases candidates");
            NativeToolsChecks.SetMode(host,1,0);before=(long)Get(field,"TileReads");slots=(long)Get(herbs,"ToolSlotsVisited");for(int i=0;i<120;i++)NativeToolsChecks.Frame(context,input);
            Require((long)Get(field,"TileReads")==before && (long)Get(herbs,"ToolSlotsVisited")==slots,"off and no pending has no business reads");
            Console.WriteLine("PASS G09 herb workload: empty steady, movement invalidation, new-plant actual harvest latency="+latency+", no-tool and off retirement.");
        }
        private static void Boss(object context,object host,object input)
        {
            var capture=Get(host,"Capture");var npcs=Get(host,"Npcs");foreach(var n in Main.npc)n.active=false;
            Call(npcs,"BeginTick");long visits=(long)Get(capture,"BossSlotVisits"),evaluations=(long)Get(capture,"BossEvaluations");
            for(int i=0;i<120;i++)Require(!(bool)Call(capture,"Boss"),"shared no-boss observation");
            Require((long)Get(capture,"BossSlotVisits")-visits==Main.maxNPCs && (long)Get(capture,"BossEvaluations")-evaluations==1,"120 danger requests share one actual 200-slot traversal");
            var boss=Main.npc[1];boss.SetDefaults(4);boss.active=true;boss.life=100;
            NativeQuickItemChecks.Sample(input,new Keys[0]);Call(npcs,"BeginActions",Get(input,"Frame"));Require((bool)Call(capture,"Boss"),"next real player action epoch observes newly spawned boss before action");
            boss.active=false;Call(npcs,"BeginTick");Require(!(bool)Call(capture,"Boss"),"post NPC epoch observes removal");
            Console.WriteLine("PASS G09 Boss workload: 120 requests / one 200-slot traversal, fresh player-action and post-NPC epochs.");
        }
    }
}
