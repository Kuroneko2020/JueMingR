using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Tools;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeSeedDiscoveryChecks
    {
        private static readonly int[] Seeds={307,308,309,310,311,312,2357};
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),herbs=Get(host,"Herbs");
            var failures=new List<string>();
            foreach(int count in new[]{1,8,48})foreach(bool mixed in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,host,input,ItemID.CopperPickaxe,0,0);
                NativeToolsChecks.SetMode(host,1,1);p.blockRange=24;
                var pending=(ReplantQueue)Get(herbs,"Pending");
                // Seed-discovery scale is isolated from harvesting. Real native
                // harvest/registration and late consumption are tested separately.
                for(int i=0;i<count;i++)
                {
                    int x=35+i%8,y=22+i/8*3;NativeToolsChecks.Tile(x,y+2,1);
                    Require(WorldGen.PlaceTile(x,y+1,78,mute:true,forced:true,plr:0),"real supported seed-discovery pot");
                    pending.Add(x,y,mixed?i%7:0,(ulong)Get(host,"Tick"));
                }
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                long before=(long)Get(herbs,"SeedSlotsVisited");
                for(int n=0;n<3;n++)
                {Require(!(bool)Call(herbs,"SeedReady",p),"absent seeds not ready");Require(Call(herbs,"ChooseSeed",p)==null,"absent seeds no intent");}
                long reads=(long)Get(herbs,"SeedSlotsVisited")-before;
                Console.WriteLine("G09 seed discovery plots="+count+" mixed="+mixed+" reads="+reads);
                if(reads!=50)failures.Add("plots="+count+" mixed="+mixed+" reads="+reads);

                // A new observation sees late material; live eligibility must
                // still reject protected, exhausted, replaced or occupied sources.
                p.inventory[17].SetDefaults(Seeds[0]);p.inventory[17].stack=2;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                Require((bool)Call(herbs,"SeedReady",p),"late source discovered next observation");
                var ownership=(ItemOperationOwnership)Get(Get(host,"Items"),"Ownership");
                long generation=(long)Get(Get(host,"Runtime"),"Generation"),token=ownership.NewUseToken();
                Require(ownership.TryBeginUse(generation,17,token),"temporary source protection");
                Require(!(bool)Call(herbs,"SeedReady",p),"cached source protection remains live");
                ownership.EndUse(generation,token);
                Require((bool)Call(herbs,"SeedReady",p),"same observation protection release is visible");
                p.inventory[17].stack=0;Require(!(bool)Call(herbs,"SeedReady",p),"exhausted stack not usable");
                p.inventory[17].SetDefaults(ItemID.Wood);Require(!(bool)Call(herbs,"SeedReady",p),"replacement item not a seed");
                p.inventory[17].TurnToAir();p.inventory[23].SetDefaults(Seeds[0]);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                Require((bool)Call(herbs,"SeedReady",p),"moved source discovered next observation");
                for(int i=0;i<pending.Count;i++)NativeToolsChecks.Tile(pending[i].X,pending[i].Y,1);
                Require(!(bool)Call(herbs,"SeedReady",p),"occupied plots never consume cached material");
                Call(herbs,"Update");Require(pending.Count==0,"occupied responsibilities retire");
                before=(long)Get(herbs,"SeedSlotsVisited");
                Require(!(bool)Call(herbs,"SeedReady",p) && Call(herbs,"ChooseSeed",p)==null,"completed queue idle");
                Require((long)Get(herbs,"SeedSlotsVisited")==before,"completed queue stops discovery");
                // Keep every responsibility, but supply only two real seeds
                // per available style. Native ItemCheck determines consumption.
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(ItemID.CopperPickaxe);
                for(int i=0;i<count;i++)
                {
                    int x=35+i%8,y=22+i/8*3;Main.tile[x,y].ClearEverything();
                    pending.Add(x,y,mixed?i%7:0,(ulong)Get(host,"Tick"));
                }
                int expected=0;
                for(int s=0;s<(mixed?7:1);s++)
                {
                    p.inventory[17+s].SetDefaults(Seeds[s]);p.inventory[17+s].stack=2;
                    expected+=Math.Min(2,Enumerable.Range(0,count).Count(i=>(mixed?i%7:0)==s));
                }
                for(int n=0;n<400;n++)NativeToolsChecks.Frame(context,input);
                int planted=Enumerable.Range(0,count).Count(i=>Main.tile[35+i%8,22+i/8*3].active() && Main.tile[35+i%8,22+i/8*3].type==82);
                int remaining=Enumerable.Range(0,mixed?7:1).Sum(s=>p.inventory[17+s].IsAir?0:p.inventory[17+s].stack);
                Require(planted==expected && remaining==(mixed?14:2)-expected,"native partial material consumes exactly needed quantity: planted="+planted+" expected="+expected+" remaining="+remaining);
                Require(pending.Count==count-expected,"unsupplied plots remain finite responsibilities");
                p.blockRange=0;
            }
            NativeToolsChecks.SetMode(host,1,0);
            Require(failures.Count==0,"one 50-slot seed discovery per observation: "+string.Join("; ",failures));
            Console.WriteLine("PASS G09 seed discovery 1/8/48 same/mixed plots, shared readiness/choice, late/replaced/moved/exhausted/protected sources and occupied plots.");
        }
    }
}
