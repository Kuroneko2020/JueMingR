using System;
using System.Linq;
using System.Diagnostics;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static ColdHandoffObservation coldHandoffObservation;
        private sealed class ColdHandoffObservation : IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Tests.ColdHandoffObservation");
            internal readonly object Host;
            internal Func<bool> AfterA;
            internal uint NegativeWorld;
            internal ColdHandoffObservation(object host)
            {
                Host=host;coldHandoffObservation=this;
                try {patch.Patch(host.GetType().GetMethod("TryPreferredBuild",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ColdHandoffPreferredResult)));}
                catch {Dispose();throw;}
            }
            public void Dispose(){patch.UnpatchAll(patch.Id);coldHandoffObservation=null;}
        }
        private static void ColdHandoffPreferredResult(object __instance,object __result)
        {
            var value=coldHandoffObservation;
            if(value!=null && ReferenceEquals(value.Host,__instance) && value.NegativeWorld==0 && value.AfterA?.Invoke()==true && __result.ToString()=="NoRunnable")
                value.NegativeWorld=Main.GameUpdateCount;
        }
        // Real normalized file, genuine qualification and consumers. The first
        // run starts at Place/Enable without WaitReady or preparation-only ticks;
        // the next instance retains this exact source for the warm comparison.
        internal static void SchedulerColdHandoffRuntime(object context,object host,object tools,object input)
        {
            bool allOpportunities=true;
            foreach(int firstStack in new[]{1,4})
            {
                BusinessEvent[] cold, warm;
                using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,2200,600))
                using(var scope=new BusinessScope(context,host,tools,input))
                using(var observation=new ColdHandoffObservation(host))
                {
                    var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                    var a=BusinessStock(p,12,ItemID.WoodWall,firstStack);
                    var b=BusinessStock(p,13,ItemID.StoneWall,3);scope.Sources=new[]{a,b};
                    var entry=SchedulerColdRealAsset(host,257,true);
                    BlueprintProjectionQualificationReceipt firstQualification=null;
                    BusinessEvent[][] actual=new BusinessEvent[2][];
                    for(int run=0;run<2;run++)
                    {
                        int origin=run==0?36:1100;
                        if(run!=0)
                        {
                            SetAutomatic(host,input,false);
                            for(int i=0;i<160 && (bool)Get(Get(tools,"Use"),"Active");i++)scope.Step();
                            Require(!(bool)Get(Get(tools,"Use"),"Active"),"cold handoff releases its exact native use before a warm second instance");
                            // This is the next independent scene's starting
                            // inventory, set before Enable, never during use.
                            a.Item.SetDefaults(a.Type);a.Item.stack=firstStack;b.Item.SetDefaults(b.Type);b.Item.stack=3;
                            p.position=new Vector2((origin+6)*16,646);p.velocity=Vector2.Zero;
                            for(int x=origin-8;x<origin+22;x++)NativeToolsChecks.Tile(x,43,TileID.Stone);
                        }
                        scope.Goals=new[]{BusinessWallGoal(origin+8,42,a),BusinessWallGoal(origin+10,42,b)};
                        Require(scope.Goals.All(scope.Runnable),"cold A and B have independent native support, supply and unchanged reach");
                        uint placed=Main.GameUpdateCount;var clock=Stopwatch.StartNew();
                        var owner=Place(context,host,input,entry,origin,40);
                        scope.Begin(true);uint enabled=Main.GameUpdateCount;SetAutomatic(host,input,true);
                        observation.NegativeWorld=0;observation.AfterA=()=>scope.Events.Count>0;
                        uint qualified=0,preparedB=0,admittedB=0;long qualifiedMs=-1,firstMs=-1;
                        long directoryBefore=(long)Get(host,"BuildFocusDirectoryConstructionSteps");
                        while(!scope.Goals.All(goal=>goal.Complete) && clock.ElapsedMilliseconds<60000)
                        {
                            scope.Step();
                            if(qualifiedMs<0 && GetOptional(owner,"Qualification")!=null)
                            {qualified=Main.GameUpdateCount;qualifiedMs=clock.ElapsedMilliseconds;}
                            if(firstMs<0 && scope.Events.Count>0)firstMs=clock.ElapsedMilliseconds;
                            var prepared=GetOptional(host,"preparedBuild");var active=GetOptional(host,"activeBuild");
                            if(preparedB==0 && prepared!=null && (int)Get(Get(prepared,"Source"),"Type")==b.Type)preparedB=Main.GameUpdateCount;
                            if(admittedB==0 && active!=null && (int)Get(Get(active,"Source"),"Type")==b.Type)admittedB=Main.GameUpdateCount;
                            // This is a CPU worker opportunity, never a neutral
                            // native input frame or a changed vanilla cooldown.
                            System.Threading.Thread.Sleep(2);
                        }
                        scope.Report("cold A-B stack="+firstStack+" run="+run);
                        Require(scope.Goals.All(goal=>goal.Complete) && scope.Events.Count==2 &&
                            scope.Events.Select(e=>e.Type).SequenceEqual(new[]{a.Type,b.Type}) &&
                            a.Item.stack==firstStack-1 && b.Item.stack==2 && scope.Events.All(e=>e.Count==1),
                            "cold and warm handoffs really consume one exact A then B, preserving every item and far missing-Dirt obligations");
                        var receipt=(BlueprintProjectionQualificationReceipt)Get(owner,"Qualification");
                        Require(receipt.SourceRanges.Count==257,"cold handoff keeps all 257 actual normalized physical groups");
                        if(run==0)firstQualification=receipt;
                        else Require(ReferenceEquals(firstQualification.SourceRanges,receipt.SourceRanges),"warm second instance shares the exact qualified geometry source");
                        actual[run]=scope.Events.ToArray();
                        Console.WriteLine("BlueprintSchedulerColdHandoff stack="+firstStack+" run="+(run==0?"cold":"warm")+
                            " groups=257 placed="+placed+" enabled="+enabled+" qualified="+qualified+" firstA="+actual[run][0].World+
                            " lastA="+actual[run][0].World+" firstB="+actual[run][1].World+" enableToA="+unchecked(actual[run][0].World-enabled)+
                            " negativeA="+observation.NegativeWorld+" preparedB="+preparedB+" admittedB="+admittedB+" gap="+unchecked(actual[run][1].World-actual[run][0].World)+" qualifyMs="+qualifiedMs+" firstAMs="+firstMs+
                            " elapsedMs="+clock.ElapsedMilliseconds+" hostConstructionNodes="+((long)Get(host,"BuildFocusDirectoryConstructionSteps")-directoryBefore)+
                            " input=real-natural-Player.Update inventoryClicks=0 Draw=NOT_EVALUATED");
                    }
                    cold=actual[0];warm=actual[1];
                    SetAutomatic(host,input,false);Call(host,"ClearProjection");
                }
                var original=SchedulerColdOriginalHandoff(context,host,tools,input,firstStack,cold);
                uint expected=unchecked(original[1].World-original[0].World),coldGap=unchecked(cold[1].World-cold[0].World),warmGap=unchecked(warm[1].World-warm[0].World);
                Console.WriteLine("BlueprintSchedulerColdHandoffCompare stack="+firstStack+" originalGap="+expected+" coldGap="+coldGap+" warmGap="+warmGap);
                allOpportunities&=coldGap==expected && warmGap==expected;
            }
            Require(allOpportunities,"cold and warm A-B capture the first legal original Select plus held-input opportunity, without a directory-induced gap");
        }

        private static BusinessEvent[] SchedulerColdOriginalHandoff(object context,object host,object tools,object input,int firstStack,BusinessEvent[] replay)
        {
            using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,2200,600))
            using(var scope=new BusinessScope(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                var a=BusinessStock(p,12,ItemID.WoodWall,firstStack);var b=BusinessStock(p,13,ItemID.StoneWall,3);
                scope.Sources=new[]{a,b};scope.Goals=new[]{BusinessWallGoal(44,42,a),BusinessWallGoal(46,42,b)};scope.Begin(false);
                for(int frame=0;frame<240 && !scope.Goals.All(goal=>goal.Complete);frame++)
                {
                    var goal=replay[scope.Events.Count];var source=scope.Sources.Single(value=>value.Slot==goal.Slot);
                    if(p.selectedItem!=source.Slot)
                    {
                        if(!p.selectedItemState.HasBufferedChange)p.selectedItemState.Select(source.Slot);
                        if(!p.selectedItemState.CanChangeSelectedItemImmediately){scope.Step();continue;}
                    }
                    scope.Step(new Vector2(goal.X*16+8,goal.Y*16+8),p.itemAnimation>0 || p.releaseUseItem || source.Item.autoReuse);
                }
                scope.Report("cold A-B original stack="+firstStack);
                Require(scope.Goals.All(goal=>goal.Complete) && scope.Events.Count==2 && a.Item.stack==firstStack-1 && b.Item.stack==2 &&
                    scope.Events.Zip(replay,(x,y)=>x.Type==y.Type && x.Slot==y.Slot && x.Count==y.Count && x.X==y.X && x.Y==y.Y).All(same=>same),
                    "original legal input reaches the same ordered bodies with the same exact source slots and costs");
                return scope.Events.ToArray();
            }
        }
    }
}
