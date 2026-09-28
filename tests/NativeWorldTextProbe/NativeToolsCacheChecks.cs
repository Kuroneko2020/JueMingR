using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These observers count the loaded Host's actual work. Positive samples
    // precede zero-work claims; they never substitute a production result.
    internal static class NativeToolsCacheChecks
    {
        private static long reads,deep;
        private static void Read(){reads++;}
        private static void Deep(){deep++;}
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input");
            var assembly=host.GetType().Assembly;
            var read=assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation").GetMethod("ReadCurrent",BindingFlags.NonPublic|BindingFlags.Static);
            var check=assembly.GetType("JueMingR.TerrariaHost.Tools.MiningEligibility").GetMethod("CanProgress",BindingFlags.NonPublic|BindingFlags.Static);
            Require(read!=null && check!=null,"actual Host workload observation methods exist");
            var observer=new Harmony("JueMingR.Tests.G09CacheWork");
            observer.Patch(read,prefix:new HarmonyMethod(typeof(NativeToolsCacheChecks),nameof(Read)));
            observer.Patch(check,prefix:new HarmonyMethod(typeof(NativeToolsCacheChecks),nameof(Deep)));
            try { Mining(context,host,input); ProtectedMining(context,host,input); Herbs(context,host,input); }
            finally { observer.Unpatch(read,HarmonyPatchType.All,observer.Id);observer.Unpatch(check,HarmonyPatchType.All,observer.Id); }
        }
        private static void Tick(object context,object input)
        {NativeQuickItemChecks.Sample(input,new Keys[0]);NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");}
        private static void ClearField()
        {for(int x=25;x<90;x++)for(int y=25;y<80;y++){if(Main.tile[x,y]==null)Main.tile[x,y]=new Tile();Main.tile[x,y].ClearEverything();}}
        internal static void NaturalCompletion(object context,bool herbs)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),mining=Get(host,"Mining");
            // Outside the cadence interval, advance native time normally until
            // the last animation/channel and its owned use lease have retired.
            for(int i=0;i<80;i++)NativeToolsChecks.Frame(context,input);
            Require(!(bool)Get(Get(host,"Use"),"Active") && (int)Call(host,"Mode",herbs?1:2)==1,"natural completion returns temporary use while retaining mode");
            if(!herbs)Require(((MiningRegion)Get(mining,"Region")).Count==0 && GetOptional(mining,"tool")==null,"naturally mined vein retires membership and tool");
            var method=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation").GetMethod("ReadCurrent",BindingFlags.NonPublic|BindingFlags.Static);
            var observer=new Harmony("JueMingR.Tests.G09CompletedWork");observer.Patch(method,prefix:new HarmonyMethod(typeof(NativeToolsCacheChecks),nameof(Read)));
            try
            {
                reads=0;for(int i=0;i<64;i++)NativeToolsChecks.Frame(context,input);
                Require(herbs?reads==24*64:reads==0,"natural completion stops obsolete work and preserves only herb discovery: "+reads);
            }
            finally{observer.Unpatch(method,HarmonyPatchType.All,observer.Id);}
        }
        private static void Mining(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var mining=Get(host,"Mining");var coverage=Get(mining,"Coverage");
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(2176);p.inventory[0].tileBoost=60;
            p.position=new Vector2(640,640);p.selectedItemState.Select(0);p.selectedItemState.Update();
            foreach(int size in new[]{8,64,512})
            {
                ClearField();int width=size==512?32:8;
                for(int i=0;i<size;i++)NativeToolsChecks.Tile(30+i%width,30+i/width,6);
                NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,30,30,6,false),"cache scale selection "+size);
                Tick(context,input);var region=(MiningRegion)Get(mining,"Region");Require(region.Count==size,"selected scale exactly "+size);
                StableMining(context,input,mining,size,"initial");
                if(size==512)
                {
                    int index=Enumerable.Range(0,size).Single(i=>region[i].X==45 && region[i].Y==38);
                    Require((bool)Call(coverage,"Green",index),"interior candidate green");
                    // A side neighbour is outside the vertical witness. This
                    // must be discovered by the documented bounded deep poll.
                    Tile side=Main.tile[46,38];Main.tile[46,38]=null;
                    for(int i=0;i<22;i++)Tick(context,input);
                    Require(!(bool)Call(coverage,"Green",index) && region.Count==size,"unreadable neighbour turns interior red without dropping unknown membership");
                    Require(Call(mining,"Choose",p)!=null,"other legal interior members remain executable");
                    Main.tile[46,38]=side;for(int i=0;i<22;i++)Tick(context,input);
                    Require((bool)Call(coverage,"Green",index),"restored neighbour becomes green within full polling rotation");
                    StableMining(context,input,mining,size,"after readability recovery");
                    p.noBuilding=true;Tick(context,input);Require(!(bool)Call(coverage,"Green",index) && Call(mining,"Choose",p)==null,"capability loss blocks display and execution");
                    p.noBuilding=false;Tick(context,input);Require((bool)Call(coverage,"Green",index),"capability recovery restores candidate");
                    StableMining(context,input,mining,size,"after capability recovery");
                    var intent=Call(mining,"Choose",p);Require(intent!=null,"current target intent exists");
                    Vector2 target=(Vector2)Get(intent,"Target");Main.tile[(int)(target.X/16),(int)(target.Y/16)].inActive(true);
                    Require(!((Func<bool>)Get(intent,"Valid"))(),"cached green cannot authorize changed target before next update");
                    Main.tile[(int)(target.X/16),(int)(target.Y/16)].inActive(false);
                }
                var previous=Call(mining,"Choose",p);Require(previous!=null,"old tool has a valid intent before replacement");
                p.inventory[0]=new Item();p.inventory[0].SetDefaults(2176);p.inventory[0].tileBoost=60;Tick(context,input);
                Require(((MiningRegion)Get(mining,"Region")).Count==0 && GetOptional(mining,"tool")==null && !((Func<bool>)Get(previous,"Valid"))(),"replacing exact tool retires old region and permission");
                NativeToolsChecks.SetMode(host,2,0);Tick(context,input);reads=deep=0;
                for(int i=0;i<32;i++)Tick(context,input);
                Require(reads==0 && deep==0 && GetOptional(mining,"tool")==null,"cancelled mining stops actual work and clears tool");
            }
            p.inventory[0].tileBoost=0;
            Console.WriteLine("PASS G09 cache mining: 8/64/512 actual update reads, bounded hidden-neighbour invalidation, recovery steady state, execution recheck and cancellation.");
        }
        private static void ProtectedMining(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var mining=Get(host,"Mining");var coverage=Get(mining,"Coverage");
            ClearField();for(int x=30;x<62;x++)for(int y=40;y<42;y++)NativeToolsChecks.Tile(x,y,22);
            p.inventory[0].tileBoost=60;NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,42,40,22,false),"protected cache region");Tick(context,input);
            var region=(MiningRegion)Get(mining,"Region");int index=Enumerable.Range(0,region.Count).Single(i=>region[i].X==42 && region[i].Y==40);
            Require((bool)Call(coverage,"Green",index),"support begins cached green");
            foreach(int furnishing in new[]{470,475})
            {
                NativeToolsChecks.Tile(42,39,furnishing);Require(WorldGen.CheckTileBreakability(42,40)==2,"native furnishing protects cached support");Tick(context,input);
                Require(!(bool)Call(coverage,"Green",index) && region.Count==64,"new furniture makes cached support red without losing selection");
                var next=Call(mining,"Choose",p);Require(next!=null && (Vector2)Get(next,"Target")!=new Vector2(680,648),"next executable target skips protected support");
                Main.tile[42,39].ClearEverything();Tick(context,input);Require((bool)Call(coverage,"Green",index),"removed support protection restores green");
                StableMining(context,input,mining,64,"after support change");
            }
            var clinger=Main.npc[2];clinger.SetDefaults(101);clinger.whoAmI=2;clinger.active=true;clinger.ai[0]=42;clinger.ai[1]=40;clinger.position=new Vector2(672,620);
            try
            {
                Terraria.GameContent.FixExploitManEaters.Update();clinger.AI();Require(Terraria.GameContent.FixExploitManEaters.SpotProtected(42,40),"real AI adds cached anchor protection");Tick(context,input);
                Require(!(bool)Call(coverage,"Green",index),"native anchor change invalidates cached green");
                clinger.active=false;Terraria.GameContent.FixExploitManEaters.Update();Tick(context,input);Require((bool)Call(coverage,"Green",index),"native anchor removal restores cached green");
                StableMining(context,input,mining,64,"after anchor change");
            }
            finally{clinger.active=false;Terraria.GameContent.FixExploitManEaters.Update();NativeToolsChecks.SetMode(host,2,0);}
            p.inventory[0].tileBoost=0;
        }
        private static void StableMining(object context,object input,object mining,int size,string label)
        {
            reads=deep=0;long before=(long)Get(mining,"OverlayChecks");
            for(int i=0;i<32;i++)Tick(context,input);
            long checks=(long)Get(mining,"OverlayChecks")-before;
            // All members are reachable in this scale fixture. Each update
            // owes N membership + 3N witness reads, plus <=24 deep checks.
            long budget=32*Math.Min(size,24);
            Require(deep>0 && checks==deep && deep<=budget,"bounded actual deep work after "+label+" N="+size+" actual="+deep);
            Require(reads>0 && reads<=32*4*size+10*deep,"bounded total tile work after "+label+" N="+size+" actual="+reads);
            Console.WriteLine("G09 cache mining N="+size+" "+label+": reads="+reads+" deep="+deep);
        }
        private static void Herbs(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var herbs=Get(host,"Herbs");var field=Get(herbs,"Field");
            ClearField();foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(213);p.selectedItemState.Select(0);p.selectedItemState.Update();
            NativeToolsChecks.SetMode(host,1,1);
            for(int i=0;i<8;i++)NativeToolsChecks.Frame(context,input);
            StableHerbs(context,input,field,"empty");
            p.position+=new Vector2(16,0);NativeToolsChecks.Frame(context,input);
            StableHerbs(context,input,field,"after movement");
            p.noBuilding=true;NativeToolsChecks.Frame(context,input);Require(!(bool)Call(herbs,"Ready",p),"blocked field has no ready target");
            p.noBuilding=false;NativeToolsChecks.Frame(context,input);StableHerbs(context,input,field,"after capability recovery");
            p.inventory[12].TurnToAir();NativeToolsChecks.Frame(context,input);reads=0;
            for(int i=0;i<64;i++)NativeToolsChecks.Frame(context,input);
            Require(reads==0 && (int)Get(field,"Count")==0,"enabled with no tool stops actual tile reads");
            NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"cache change pot");
            NativeToolsChecks.Tile(42,40,84);p.inventory[12]=new Item();p.inventory[12].SetDefaults(213);
            HarvestWithin(context,input,34,"new carried tool after tool-less idle");
            bool day=Main.dayTime;
            try
            {
                Main.dayTime=false;NativeToolsChecks.Tile(42,40,83);Main.tile[42,40].frameX=0;
                for(int i=0;i<40;i++)NativeToolsChecks.Frame(context,input);
                Require(Main.tile[42,40].type==83 && !(bool)Call(herbs,"Ready",p),"dormant native plant remains unharvested");
                Main.dayTime=true;HarvestWithin(context,input,4,"day change after negative cache");
                NativeToolsChecks.Tile(42,40,84);Main.tile[42,41].inActive(true);
                for(int i=0;i<40;i++)NativeToolsChecks.Frame(context,input);
                Require(Main.tile[42,40].type==84 && !(bool)Call(herbs,"Ready",p),"inactive support retains unharvested plant");
                Main.tile[42,41].inActive(false);HarvestWithin(context,input,23,"restored support after negative cache");
            }
            finally{Main.dayTime=day;}
            for(int i=0;i<40;i++)NativeToolsChecks.Frame(context,input);StableHerbs(context,input,field,"after real changed-field harvest");
            NativeToolsChecks.SetMode(host,1,0);reads=0;for(int i=0;i<32;i++)NativeToolsChecks.Frame(context,input);
            Require(reads==0,"disabled without responsibility stops actual tile reads");
            Console.WriteLine("PASS G09 cache herbs: actual empty-field reads, movement/capability recovery to steady state, no-tool and disabled work.");
        }
        private static void HarvestWithin(object context,object input,int limit,string reason)
        {
            int elapsed=0;while(elapsed<limit && Main.tile[42,40].type!=82){NativeToolsChecks.Frame(context,input);elapsed++;}
            Require(Main.tile[42,40].type==82 && elapsed>0,"changed field actually harvests within existing discovery/probe bound: "+reason+" updates="+elapsed);
        }
        private static void StableHerbs(object context,object input,object field,string label)
        {
            reads=0;long before=(long)Get(field,"TileReads");
            for(int i=0;i<64;i++)NativeToolsChecks.Frame(context,input);
            long observed=(long)Get(field,"TileReads")-before;
            // Empty discovery has exactly the 24-cell background poll and no
            // active-target reads. Count equality catches work outside Field.
            Require(observed==24*64 && reads==observed,"actual empty field work after "+label+" reads="+reads+" field="+observed);
        }
    }
}
