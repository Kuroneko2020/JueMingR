using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingOutcomeChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static bool trial,lineBroken;
        private static int baitSeed,pullSeed,giveSeed,gives,consumes,casts;
        // Fixed native RNG instances enter only the three actual consumer
        // boundaries. Other Player/Projectile randomness cannot shift a case,
        // and the original methods still decide consumption, loss and amount.
        private static void RandomBefore(MethodBase __originalMethod,out UnifiedRandom __state)
        {
            __state=null;if(!trial)return;__state=Main.rand;
            string name=__originalMethod.Name;
            Main.rand=new UnifiedRandom(name.EndsWith("ConsumeBait")?baitSeed:name.EndsWith("PullBobber")?pullSeed:giveSeed);
            if(name.EndsWith("ConsumeBait"))consumes++;
            if(name.EndsWith("GiveItemToPlayer"))gives++;
        }
        private static void RandomAfter(UnifiedRandom __state){if(__state!=null)Main.rand=__state;}
        private static void Created(Projectile __0){if(trial && __0.bobber)casts++;}
        private static void Pulled(Projectile __0){if(trial && __0.localAI[1]>0 && __0.ai[0]==2 && __0.ai[1]<0)lineBroken=true;}
        private static bool SkipAchievement(){return false;}
        internal static void Run(object context)
        {
            // Only profile metadata is needed for original NPC spawning. Its
            // native server initialization omits texture requests; every actual
            // player update below still runs with the original client flag.
            bool dedicated=Main.dedServ;
            try{Main.dedServ=true;System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.GameContent.TownNPCProfiles).TypeHandle);}
            finally{Main.dedServ=dedicated;}
            var audit=new Harmony("JueMingR.Tests.FishingOutcomes");
            var consumers=new[]{typeof(Player).GetMethod("ItemCheck_CheckFishingBobber_ConsumeBait",Flags),typeof(Player).GetMethod("ItemCheck_CheckFishingBobber_PullBobber",Flags),typeof(Projectile).GetMethod("AI_061_FishingBobber_GiveItemToPlayer",Flags)};
            foreach(var method in consumers)audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeFishingOutcomeChecks),nameof(RandomBefore)),finalizer:new HarmonyMethod(typeof(NativeFishingOutcomeChecks),nameof(RandomAfter)));
            audit.Patch(consumers[1],postfix:new HarmonyMethod(typeof(NativeFishingOutcomeChecks),nameof(Pulled)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeFishingOutcomeChecks),nameof(Created)));
            audit.Patch(typeof(WorldGen).GetMethod("CheckAchievement_RealEstateAndTownSlimes",Flags),prefix:new HarmonyMethod(typeof(NativeFishingOutcomeChecks),nameof(SkipAchievement)));
            bool slime=NPC.unlockedSlimeRedSpawn;
            object host=Get(context,"Fishing"),tools=Get(context,"Tools"),input=Get(context,"Input");
            try
            {
                // Each liquid is reached by a real manual cast and real native
                // collision, never by setting wet/honeyWet/lavaWet ourselves.
                RunCase(context,host,tools,input,0,ItemID.Bass,true,false,true);
                RunCase(context,host,tools,input,2,ItemID.Honeyfin,true,false,false);
                RunCase(context,host,tools,input,1,ItemID.Obsidifish,true,false,true);
                RunCase(context,host,tools,input,0,ItemID.Bass,false,true,true);
                foreach(int seed in new[]{19,43})
                {
                    giveSeed=seed;
                    RunCase(context,host,tools,input,0,ItemID.BombFish,true,false,false);
                    RunCase(context,host,tools,input,0,ItemID.FrostDaggerfish,true,false,false);
                }
                RunCase(context,host,tools,input,0,-586,true,false,false);
                RunCase(context,host,tools,input,0,-682,true,false,false);
            }
            finally
            {
                trial=false;NPC.unlockedSlimeRedSpawn=slime;NativeFishingChecks.Save(host,new FishingOptions());
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
            }
            Console.WriteLine("PASS G10 real water/honey/lava collisions and native item/NPC results, bait consumed/retained, line break and random multi-stack fish, each followed by one actual recast.");
        }
        private static void RunCase(object context,object host,object tools,object input,int liquid,int result,bool line,bool broken,bool consume)
        {
            NativeFishingChecks.Save(host,new FishingOptions());
            var p=NativeToolExecutionChecks.Reset(context,tools,input,liquid==1?ItemID.HotlineFishingHook:ItemID.WoodFishingPole,0,0);
            foreach(var item in p.armor)item.TurnToAir();if(line)p.armor[3].SetDefaults(ItemID.HighTestFishingLine);
            p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            for(int x=44;x<74;x++)for(int y=42;y<61;y++)
            {var tile=Main.tile[x,y];tile.ClearEverything();if(y>=44 && y<60){tile.liquid=255;tile.liquidType(liquid);}if(y==60)NativeToolsChecks.Tile(x,y,1);}
            NativeFishingChecks.Save(host,new FishingOptions(auto:true));var aim=new Vector2(850,718);
            for(int i=0;i<150;i++)NativeFishingChecks.Step(context,input,aim,i==0,0);
            var b=Main.projectile.FirstOrDefault(x=>x.active && x.bobber && x.owner==p.whoAmI);
            Require(b!=null && b.wet && b.honeyWet==(liquid==2) && b.lavaWet==(liquid==1) && (bool)Get(Get(host,"Session"),"Active"),"native collision admits actual liquid "+liquid);
            Require((int)Get(Get(host,"Session"),"Liquid")== (liquid==1?3:liquid==2?2:1),"session preserves native liquid identity");
            var fishingContext=new Terraria.GameContent.FishDropRules.FishingContext();
            Require((bool)Call(b,"TryBuildFishingContext",fishingContext),"native context accepts real pool");
            var fisher=Get(fishingContext,"Fisher");
            Require((bool)Get(fisher,"inHoney")== (liquid==2) && (bool)Get(fisher,"inLava")== (liquid==1) && (liquid!=1 || (bool)Get(fisher,"CanFishInLava")),"real native fishing context agrees with liquid and lava eligibility");
            baitSeed=Seed(r=>((float)r.NextDouble()*(1f+p.inventory[54].bait/6f)<1f)==consume);
            pullSeed=Seed(r=>(r.Next(7)==0)==broken);
            int key=(int)b.key,before=p.inventory.Where(x=>x.type==result).Sum(x=>x.stack),bait=p.inventory[54].stack;
            int npcBefore=result<0?Main.npc.Count(x=>x.active && x.type==-result):0;
            Vector2 spawn=new Vector2((int)b.position.X,(int)b.position.Y);
            gives=consumes=casts=0;lineBroken=false;trial=true;b.ai[1]=-120;b.localAI[1]=result;b.localAI[2]=ItemID.Worm;
            try
            {
                for(int i=0;i<300;i++)
                {
                    NativeFishingChecks.Step(context,input,aim,false,0);
                    // End this bite's observation when its replacement is
                    // waiting in liquid. A later natural bite is a new catch,
                    // not evidence of duplicate consumption of this one.
                    var session=Get(host,"Session");
                    if(casts>0 && Get(session,"Phase").ToString()=="Waiting" && (bool)Get(session,"InLiquid"))break;
                }
                Require(consumes==1 && casts==1 && p.inventory[54].stack==bait-(consume?1:0),"one actual bait consumer and recast preserves native consumption result: liquid="+liquid+" result="+result+" consumes="+consumes+" casts="+casts);
                Require(!Main.projectile.Any(x=>x.active && x.bobber && (int)x.key==key) && (bool)Get(Get(host,"Session"),"InLiquid"),"old native bobber retires and replacement reaches its real liquid");
                int delta=p.inventory.Where(x=>x.type==result).Sum(x=>x.stack)-before;
                if(result<0)
                {
                    var npcs=Main.npc.Where(x=>x.active && x.type==-result).ToArray();
                    Require(gives==0 && npcs.Length==npcBefore+1,"negative native result spawns exactly one actual NPC without an item product");
                    var npc=npcs.Last();Require(Vector2.Distance(npc.Bottom,spawn)<20f,"real NPC spawns at the original bobber location");
                    if(result==-682)Require(npc.friendly && NPC.unlockedSlimeRedSpawn,"friendly fishing NPC keeps its actual native friendly/unlock state");
                }
                else if(broken)Require(lineBroken && gives==0 && delta==0,"native line break gives no fish, but may consume bait and recasts once");
                else Require(gives==1 && (result==ItemID.BombFish || result==ItemID.FrostDaggerfish?delta>1:delta==1),"native Give preserves one fish or its actual random multi-stack amount: result="+result+" delta="+delta);
            }
            finally{trial=false;}
        }
        private static int Seed(Func<UnifiedRandom,bool> predicate)
        {for(int seed=0;seed<10000;seed++)if(predicate(new UnifiedRandom(seed)))return seed;throw new InvalidOperationException("No deterministic native RNG seed found.");}
    }
}
