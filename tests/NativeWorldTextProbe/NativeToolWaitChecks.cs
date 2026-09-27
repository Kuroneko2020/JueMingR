using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolWaitChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly Vector2 Away=new Vector2(480,540);
        internal static void Run(object context,object host,object input,bool release)
        {
            Require(host.GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration==(release?"Release":"Debug"),"wait checks use declared production configuration");
            var failures=new List<string>();
            foreach(int empty in new[]{0,1,3})
            {
                Check(()=>Herbs(context,host,input,empty,false,release),"late seed empty="+empty,failures);
                Check(()=>Herbs(context,host,input,empty,true,release),"seed expiry empty="+empty,failures);
                foreach(int material in new[]{123,224})Check(()=>Gravity(context,host,input,empty,material),"gravity="+material+" empty="+empty,failures);
                Check(()=>Loan(context,host,input,empty,false),"rod return empty="+empty,failures);
                Check(()=>Loan(context,host,input,empty,true),"rod expiry empty="+empty,failures);
            }
            Check(()=>ClockBoundary(context,host,input),"native wrap and rollback",failures);
            foreach(bool returning in new[]{false,true})foreach(string boundary in new[]{"focus","ui","off","selection","source","death","boss","unknown"})
                if(!returning || boundary!="unknown")Check(()=>LoanSafety(context,host,input,returning,boundary),"loan safety "+returning+"/"+boundary,failures);
            Require(failures.Count==0,"finite simulation waits: "+string.Join("; ",failures));
            Console.WriteLine("PASS G09 finite waits: equal native simulation with 0/1/3 empty outer updates; late seed, expiry, real silt/slush motion, delayed rod completion and single recast.");
            if(release)Console.WriteLine("Release behavior checked; DEBUG-only herb field read counter is absent and not measured.");
        }
        private static void Check(Action action,string label,List<string> failures)
        {try{action();}catch(Exception e){failures.Add(label+": "+e.GetBaseException().Message);Console.WriteLine("FAIL G09 wait "+label+": "+e.GetBaseException());}}
        private static void Step(object context,object input,int empty,bool held=false,Vector2? aim=null)
        {
            NativeToolExecutionChecks.Sample(context,input,aim??Away,held);
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            for(int i=0;i<Main.projectile.Length;i++)
            {
                var q=Main.projectile[i];if(!q.active)continue;
                if(q.type==71 || q.type==179)q.Update(i);
                else if(q.bobber)Call(q,"AI_061_FishingBobber");
            }
            Call(context,"UpdateRuntime");NativeToolExecutionChecks.Outer(context,input,empty);
        }
        private static ReplantQueue Harvest(object context,object host,object input,int empty)
        {
            var p=NativeToolExecutionChecks.Reset(context,host,input,ItemID.StaffofRegrowth,12,0);
            Require(WorldGen.PlaceTile(42,42,78,mute:true,forced:true,plr:0),"wait actual pot");
            NativeToolsChecks.Tile(42,41,84);Main.tile[42,41].liquid=255;
            NativeToolsChecks.SetMode(host,1,1);
            for(int i=0;i<80 && Main.tile[42,41].active();i++)Step(context,input,empty);
            var pending=(ReplantQueue)Get(Get(host,"Herbs"),"Pending");
            Require(!Main.tile[42,41].active() && pending.Count==1,"wait starts from real water-blocked harvest");
            // The scenario has no reachable seed until the explicit late arrival.
            // Move actual harvest drops away; do not intercept consumption.
            foreach(var item in Main.item.Where(item=>item.active))item.position=new Vector2(1600,1600);
            Require(!p.inventory.Any(item=>item.type==ItemID.DaybloomSeeds && item.stack>0),"wait begins without a carried seed");
            return pending;
        }
        private static void Herbs(object context,object host,object input,int empty,bool expire,bool release)
        {
            var pending=Harvest(context,host,input,empty);ulong created=pending[0].Created;
            object field=Get(Get(host,"Herbs"),"Field");ulong before=(ulong)Get(host,"Tick");
            object reads=release?GetOptional(field,"TileReads"):Get(field,"TileReads");
            Require(!release || reads==null,"ordinary Release has no DEBUG field counter");NativeToolExecutionChecks.Outer(context,input,20);
            Require((ulong)Get(host,"Tick")==before && pending[0].Created==created,"empty callbacks preserve both native age and original responsibility");
            if(reads!=null)Require((long)Get(field,"TileReads")== (long)reads,"empty stable world does not repeat herb-field exploration");
            uint start=Main.GameUpdateCount;
            for(int i=0;i<400;i++)Step(context,input,empty);
            Require(pending.Count==1 && pending[0].Created==created,"400 native steps retain the original unrenewed 600-step responsibility; pending="+pending.Count);
            if(expire)
            {
                while((ulong)Get(host,"Tick")-created<599)Step(context,input,empty);
                Require(pending.Count==1,"responsibility still present immediately before its deadline");Step(context,input,empty);
                Require(pending.Count==0,"real 600-step deadline retires without renewal");
                Main.LocalPlayer.inventory[17].SetDefaults(ItemID.DaybloomSeeds);Main.tile[42,41].liquid=0;
                for(int i=0;i<40;i++)Step(context,input,empty);
                Require(!Main.tile[42,41].active() && Main.LocalPlayer.inventory[17].stack==1,"late material after expiry does not revive an old operation");
            }
            else
            {
                var p=Main.LocalPlayer;p.inventory[17].SetDefaults(ItemID.DaybloomSeeds);p.inventory[17].stack=2;Main.tile[42,41].liquid=0;
                for(int i=0;i<80 && !Main.tile[42,41].active();i++)Step(context,input,empty);
                Require(Main.tile[42,41].active() && Main.tile[42,41].type==82 && p.inventory[17].stack==1,"late actual native seed placement consumes exactly one");
                for(int i=0;i<40;i++)Step(context,input,empty);
                Require(pending.Count==0 && p.inventory[17].stack==1,"completed seed responsibility cannot consume twice");
            }
            Console.WriteLine("G09 seed wait empty="+empty+" expire="+expire+" nativeSteps="+unchecked(Main.GameUpdateCount-start));
        }
        private static void Gravity(object context,object host,object input,int empty,int material)
        {
            var p=NativeToolExecutionChecks.Reset(context,host,input,ItemID.ShroomiteDiggingClaw,0,0);
            for(int y=38;y<=56;y++)Main.tile[43,y].ClearEverything();
            NativeToolsChecks.Tile(43,39,material);NativeToolsChecks.Tile(43,40,material);NativeToolsChecks.Tile(43,56,1);
            NativeToolsChecks.Tile(44,51,material); // Unrelated old lower cell, outside the selected vein.
            NativeToolsChecks.SetMode(host,2,2);
            Step(context,input,empty,true,new Vector2(43*16+8,40*16+8));
            int projectile=material==123?71:179;
            object mining=Get(host,"Mining");
            Require(!Main.tile[43,40].active() && Main.projectile.Any(q=>q.active && q.type==projectile),"native first pick starts actual delayed falling motion");
            int landed=-1;
            for(int i=1;i<=120;i++)
            {
                Step(context,input,empty);
                if(Main.tile[43,55].active() && Main.tile[43,55].type==material){landed=i;break;}
            }
            Require(landed>34 && landed<135,"actual motion covers the prematurely shortened window; landed="+landed);
            var region=(MiningRegion)Get(mining,"Region");
            Require(Enumerable.Range(0,region.Count).Any(i=>region[i].X==43 && region[i].Y==55),"real late landing retains admission inside 135 native steps; region="+region.Count);
            Require(!Enumerable.Range(0,region.Count).Any(i=>region[i].X==44 && region[i].Y==51),"unrelated old material is never absorbed");
            for(int i=0;i<145;i++)Step(context,input,empty);
            Require((int)Get(mining,"falls")==0,"fall witnesses retire after finite native progress");
            NativeToolsChecks.Tile(42,55,1);
            Require(WorldGen.PlaceTile(42,54,material,mute:true,forced:true,plr:0) && Main.tile[42,54].active() && Main.tile[42,54].type==material,"late stable material actually exists inside the old witness area");
            Step(context,input,empty);
            Require(!Enumerable.Range(0,region.Count).Any(i=>region[i].X==42 && region[i].Y==54),"new cell after expiry is not admitted");
            Console.WriteLine("G09 actual falling wait material="+material+" empty="+empty+" landed="+landed);
        }
        private static void Loan(object context,object host,object input,int empty,bool expire)
        {
            var p=NativeToolExecutionChecks.Reset(context,host,input,ItemID.WoodFishingPole,17,17);
            p.inventory[12].SetDefaults(ItemID.BugNet);
            Step(context,input,empty,true,new Vector2(880,620));
            Require(Main.projectile.Any(q=>q.active && q.bobber),"deadline fixture starts with native cast");
            for(int i=0;i<40;i++)Step(context,input,empty);
            NativeToolsChecks.SetMode(host,0,1);object fish=Get(host,"Fishing");
            long token=(long)Call(fish,"Prepare",p);Require(token>0,"real rod and bobber acquire finite loan contract");ulong created=(ulong)Get(host,"Tick");
            object selected=p.selectedItemState;typeof(Player.SelectedItemState).GetMethod("OverrideSelection",Flags).Invoke(selected,new object[]{12});p.selectedItemState=(Player.SelectedItemState)selected;
            foreach(var q in Main.projectile.Where(q=>q.active && q.bobber))Call(q,"AI_061_FishingBobber");
            Require(!Main.projectile.Any(q=>q.active && q.bobber),"native bobber actually ends on borrowed net selection");
            // Delay only the participant's completion notification. Vanilla
            // selection, animation and subsequent player updates remain real.
            for(int i=0;i<180;i++)Step(context,input,empty);
            Require((bool)Get(fish,"Active") && (long)Get(fish,"Token")==token,"180 native steps retain the same 300-step loan");
            if(expire)
            {
                while((ulong)Get(host,"Tick")-created<299)Step(context,input,empty);
                Require((bool)Get(fish,"Active"),"loan still present immediately before its deadline");Step(context,input,empty);
                Require(!(bool)Get(fish,"Active") && Get(fish,"Phase").ToString()=="Expired","loan ends at real finite deadline");
                Call(fish,"NetFinished",token,false,false);
                for(int i=0;i<60;i++)Step(context,input,empty);
                Require(!(bool)Get(fish,"RecastAttempted") && !Main.projectile.Any(q=>q.active && q.bobber),"late completion never revives expired recast permission");
            }
            else
            {
                Call(fish,"NetFinished",token,false,false);
                for(int i=0;i<80;i++)Step(context,input,empty);
                Require(Get(fish,"Phase").ToString()=="Completed" && (bool)Get(fish,"RecastAttempted") && (bool)Get(fish,"RecastObserved"),"delayed completion owns one observed native recast");
                var bobbers=Main.projectile.Where(q=>q.active && q.bobber).ToArray();Require(bobbers.Length==1,"one new actual bobber");
                Call(fish,"NetFinished",token,false,false);
                for(int i=0;i<30;i++)Step(context,input,empty);
                Require(bobbers[0].active && Main.projectile.Count(q=>q.active && q.bobber)==1,"duplicate completion neither pulls nor repeats the cast");
            }
        }
        private static void ClockBoundary(object context,object host,object input)
        {
            var pending=Harvest(context,host,input,0);
            var counter=typeof(Main).GetField("_gameUpdateCount",Flags);uint saved=Main.GameUpdateCount;
            try
            {
                counter.SetValue(null,uint.MaxValue-2);Call(context,"UpdateRuntime");
                pending.Add(42,41,0,(ulong)Get(host,"Tick"));ulong start=(ulong)Get(host,"Tick");
                for(int i=0;i<5;i++){NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Outer(context,input,1);}
                Require((ulong)Get(host,"Tick")-start==5 && pending.Count==1,"native uint wrap advances five steps, not a deadline jump");
                counter.SetValue(null,Main.GameUpdateCount-1);NativeToolExecutionChecks.Outer(context,input,1);
                Require(pending.Count==0,"native rollback retires finite responsibility instead of renewing it");
            }
            finally{counter.SetValue(null,saved);NativeToolsChecks.SetMode(host,1,0);}
        }
        private static void LoanSafety(object context,object host,object input,bool returning,string boundary)
        {
            var p=NativeToolExecutionChecks.Reset(context,host,input,ItemID.WoodFishingPole,17,17);
            Step(context,input,0,true,new Vector2(880,620));for(int i=0;i<40;i++)Step(context,input,0);
            NativeToolsChecks.SetMode(host,0,1);object fish=Get(host,"Fishing");long token=(long)Call(fish,"Prepare",p);
            Require(token>0,"safety native cast establishes loan");if(returning)Call(fish,"NetFinished",token,false,false);
            ulong before=(ulong)Get(host,"Tick");
            try
            {
                if(boundary=="focus")Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));
                else if(boundary=="ui")Main.drawingPlayerChat=true;
                else if(boundary=="off")NativeToolsChecks.SetMode(host,0,0);
                else if(boundary=="selection")p.selectedItemState.Select(0);
                else if(boundary=="source")p.inventory[17]=new Item();
                else if(boundary=="death")p.dead=true;
                else if(boundary=="unknown")Call(fish,"NetFinished",token,false,true);
                else {Main.npc[1].SetDefaults(4);Main.npc[1].active=true;Main.npc[1].life=100;Call(Get(host,"Npcs"),"BeginTick");}
                NativeToolExecutionChecks.Outer(context,input,1);
                Require((ulong)Get(host,"Tick")==before && !(bool)Get(fish,"Active"),"unsampled "+boundary+" cancels loan before any native time advances");
            }
            finally
            {
                Main.drawingPlayerChat=false;p.dead=false;Main.npc[1].active=false;Call(Get(host,"Npcs"),"BeginTick");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));
                NativeToolExecutionChecks.Sample(context,input,Away,false);
                NativeToolsChecks.SetMode(host,0,0);
            }
            Call(fish,"NetFinished",token,false,false);NativeToolsChecks.SetMode(host,0,1);
            for(int i=0;i<12;i++)Step(context,input,0);
            Require(!(bool)Get(fish,"Active") && !(bool)Get(fish,"RecastAttempted"),"reactivation cannot revive cancelled loan token");
        }
    }
}
