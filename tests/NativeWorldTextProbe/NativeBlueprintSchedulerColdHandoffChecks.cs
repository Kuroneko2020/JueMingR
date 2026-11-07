using System;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
            internal readonly bool Trace;
            internal int BType,Rows,PriorityDepth,FlightDepth,RejectionPhase;
            internal string TargetGate="NOT_CALLED",GeometryGate="NOT_CALLED";
            internal object LastReadRange;
            internal bool LastReadResult;
            internal bool? LastNearResult;
            internal long LastReadVersion,LastStampWatch,LastStampValue;
            internal int LastStampPart;
            internal bool LastStampResult;
            internal readonly HashSet<string> Emitted=new HashSet<string>();
            internal ColdHandoffObservation(object host,bool trace=false)
            {
                Host=host;Trace=trace;coldHandoffObservation=this;
                try
                {
                    patch.Patch(host.GetType().GetMethod("TryPreferredBuild",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ColdHandoffPreferredResult)));
                    if(trace)
                    {
                        Patch("BuildFlightCurrent",nameof(ColdHandoffFlightBefore),nameof(ColdHandoffFlightResult));
                        Patch("IsBuildTargetCurrent",null,nameof(ColdHandoffTargetResult));
                        Patch("NativeBuildGeometry",null,nameof(ColdHandoffGeometryResult));
                        Patch("PreparedBuildPriorityCurrent",nameof(ColdHandoffPriorityBefore),nameof(ColdHandoffPriorityResult));
                        Patch("PreferredBuildAbsenceCurrent",null,nameof(ColdHandoffAbsenceResult));
                        Patch("BuildFocusNearCurrent",null,nameof(ColdHandoffNearResult));
                        Patch("ProjectionMatchReadCurrent",null,nameof(ColdHandoffReadResult));
                        Patch("AdvanceAutomaticBuildSelection",nameof(ColdHandoffAdvanceBefore),nameof(ColdHandoffAdvanceResult));
                        Patch("TryGetBuildTarget",null,nameof(ColdHandoffOrdinaryResult));
                        var changes=Get(host,"projectionChanges");
                        patch.Patch(changes.GetType().GetMethod("TryGetBuildStamp",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ColdHandoffStampResult)));
                    }
                }
                catch {Dispose();throw;}
            }
            private void Patch(string name,string before,string after)
            {patch.Patch(Host.GetType().GetMethod(name,F),prefix:before==null?null:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),before),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),after));}
            internal bool Watching {get{return Trace && AfterA?.Invoke()==true;}}
            internal bool Foreign(object flight){return flight!=null && (int)Get(Get(flight,"Source"),"Type")==BType;}
            internal void Emit(string key,string message)
            {
                if(!Watching || Rows>=64 || !Emitted.Add(key))return;
                Rows++;Console.WriteLine("BlueprintColdHandoffGate world="+Main.GameUpdateCount+" "+message);
            }
            internal string Flight(object flight)
            {
                if(flight==null)return "none";
                var target=Get(flight,"Target");var range=Get(target,"Range");var source=Get(flight,"Source");
                return "gen="+Get(flight,"Generation")+" type="+Get(source,"Type")+" slot="+Get(source,"Slot")+
                    " xy="+Get(target,"WorldX")+","+Get(target,"WorldY")+" local="+Get(target,"LocalProof")+
                    " range="+Get(range,"Index")+" version="+Get(target,"RangeStamp")+"/"+Get(range,"Version")+
                    " support="+Get(target,"SupportStamp")+" raw="+Get(range,"RawVersion")+" current="+Get(range,"Current")+
                    " groupReady="+Get(Get(range,"Group"),"Ready")+" checked="+Get(flight,"Checked")+" rejected="+Get(flight,"Rejected");
            }
            private static string Bounds(object bounds)
            {return Get(bounds,"Left")+","+Get(bounds,"Top")+","+Get(bounds,"Right")+","+Get(bounds,"Bottom");}
            // Read captured fields only. In particular, never call another
            // TryPreferredBuild/currentness/native-stamp query to observe one.
            // The last native results below came from the production calls.
            internal string Proofs()
            {
                var group=GetOptional(Host,"buildContinuation");if(group==null)return "group=none";
                var p=Main.LocalPlayer;var discovery=Get(group,"Discovery");var result=new StringBuilder();
                result.Append("group=").Append(Get(group,"Group")).Append(" pending=").Append(Get(group,"Pending"))
                    .Append(" nearActual=").Append(LastNearResult?.ToString()??"NOT_CALLED").Append(" repeated=").Append(GetOptional(Host,"repeatedBuild")!=null)
                    .Append(" player=").Append(p.position).Append(" direction=").Append(p.direction)
                    .Append(" eligibility=").Append(Get(Host,"MaterialBuildEligibilityRevision"))
                    .Append(" materialTick=").Append(Get(Host,"materialTick"))
                    .Append(" supply=").Append(Get(Host,"buildSupplyRevision")).Append(" permission=").Append(Get(Host,"buildFocusPermissionRevision"))
                    .Append(" container=").Append(Get(Host,"buildContainerRevision")).Append(" restoration=").Append(Get(Host,"appearanceRestorationRevision"))
                    .Append(" world=").Append(Bounds(Get(discovery,"World"))).Append(" active[").Append(Flight(GetOptional(Host,"activeBuild"))).Append("]");
                int owners=0;foreach(DictionaryEntry entry in (IDictionary)Get(discovery,"Owners"))
                {
                    if(owners++>=8)break;var owner=entry.Key;var query=entry.Value;
                    result.Append(" Q{origin=").Append(Get(owner,"OriginX")).Append(',').Append(Get(owner,"OriginY"))
                        .Append(" complete=").Append(Get(query,"Complete")).Append(" started=").Append(Get(query,"Started"))
                        .Append(" covered=").Append(Bounds(Get(query,"Covered"))).Append(" query=").Append(Bounds(Get(query,"Query"))).Append('}');
                }
                int count=0;
                foreach(var range in ((IEnumerable)Get(discovery,"Ranges")))
                {
                    if(count++>=8){result.Append(" ranges=TRUNCATED");break;}
                    var value=GetOptional(range,"Materials");
                    result.Append(" R").Append(Get(range,"Index")).Append("{version=").Append(Get(range,"Version"))
                        .Append(" bounds=").Append(Bounds(Get(Get(range,"Source"),"Bounds")))
                        .Append(" raw=").Append(Get(range,"RawVersion")).Append(" watch=").Append(Get(range,"Watch"))
                        .Append(" part=").Append(Get(range,"PartIndex")).Append(" ready=").Append(Get(Get(range,"Group"),"Ready"))
                        .Append(" nativeReadActual=").Append(ReferenceEquals(LastReadRange,range)?LastReadResult.ToString():"OTHER-RANGE")
                        .Append(" nativeReadVersion=").Append(LastReadVersion)
                        .Append(" stampActual=").Append(LastStampWatch).Append('/').Append(LastStampPart).Append('/').Append(LastStampResult).Append('/').Append(LastStampValue);
                    if(value!=null)
                    {
                        foreach(string name in new[]{"PreferredGroup","PreferredRevisit","PreferredFrontier","PreferredComplete","PreferredCoveredBounds","PreferredVersion","PreferredWake","BuildWakeRevision","PreferredSupply","PreferredSupport","PreferredWaits","PreferredPermission","PreferredContainer","PreferredRestoration","PreferredRetryAt","PreferredDependencySlots"})
                            result.Append(' ').Append(name).Append('=').Append(name=="PreferredCoveredBounds"?Bounds(Get(value,name)):Get(value,name));
                        var negatives=GetOptional(value,"PreferredPositionNegatives") as IDictionary;
                        if(negatives!=null)
                        {
                            int n=0;foreach(var proof in negatives.Values)
                            {
                                if(n++>=8){result.Append(" positions=TRUNCATED");break;}
                                result.Append(" P{");foreach(string name in new[]{"Active","Player","Position","Reach","Width","Height","Direction","PulleyDirection","Pulley","OutOfReach","X","Y"})
                                    result.Append(name).Append('=').Append(name=="Reach"?Bounds(Get(proof,name)):Get(proof,name)).Append(' ');result.Append('}');
                            }
                        }
                    }
                    result.Append('}');
                }
                return result.ToString();
            }
            public void Dispose(){patch.UnpatchAll(patch.Id);coldHandoffObservation=null;}
        }
        private sealed class ColdHandoffGateState {internal object Flight;internal bool Relevant;}
        private static void ColdHandoffFlightBefore(object __instance,object __0,bool __1,out ColdHandoffGateState __state)
        {
            var o=coldHandoffObservation;__state=new ColdHandoffGateState{Flight=__0,Relevant=o?.Watching==true && ReferenceEquals(o.Host,__instance) && !__1 && o.Foreign(__0)};
            if(__state.Relevant){o.FlightDepth++;o.TargetGate=o.GeometryGate="NOT_CALLED";}
        }
        private static void ColdHandoffFlightResult(bool __result,ColdHandoffGateState __state)
        {
            var o=coldHandoffObservation;
            if(o==null || !__state.Relevant)return;
            if(!__result){o.RejectionPhase=1;o.Emit("flight-false-"+Get(__state.Flight,"Generation"),"BuildFlightCurrent=false targetActual="+o.TargetGate+" geometryActual="+o.GeometryGate+" "+o.Flight(__state.Flight)+" "+o.Proofs());}
            o.FlightDepth--;
        }
        private static void ColdHandoffTargetResult(object __instance,bool __result)
        {var o=coldHandoffObservation;if(o?.Watching==true && o.FlightDepth>0 && ReferenceEquals(o.Host,__instance))o.TargetGate=__result.ToString();}
        private static void ColdHandoffGeometryResult(object __instance,bool __result)
        {var o=coldHandoffObservation;if(o?.Watching==true && o.FlightDepth>0 && ReferenceEquals(o.Host,__instance))o.GeometryGate=__result.ToString();}
        private static void ColdHandoffPriorityBefore(object __instance,object __0,out ColdHandoffGateState __state)
        {
            var o=coldHandoffObservation;bool relevant=o?.Watching==true && ReferenceEquals(o.Host,__instance) && o.Foreign(__0);
            __state=new ColdHandoffGateState{Flight=__0,Relevant=relevant};if(relevant)o.PriorityDepth++;
        }
        private static void ColdHandoffPriorityResult(bool __result,ColdHandoffGateState __state)
        {
            var o=coldHandoffObservation;if(o==null || !__state.Relevant)return;
            if(!__result){o.RejectionPhase=1;o.Emit("priority-false-"+Get(__state.Flight,"Generation"),"PreparedBuildPriorityCurrent=false "+o.Flight(__state.Flight)+" "+o.Proofs());}
            o.PriorityDepth--;
        }
        private static void ColdHandoffAbsenceResult(object __instance,bool __result)
        {
            var o=coldHandoffObservation;if(o?.Watching==true && o.PriorityDepth>0 && ReferenceEquals(o.Host,__instance) && !__result)
                o.Emit("absence-false","PreferredBuildAbsenceCurrent=false "+o.Proofs());
        }
        private static void ColdHandoffNearResult(object __instance,bool __result)
        {var o=coldHandoffObservation;if(o?.Watching==true && ReferenceEquals(o.Host,__instance))o.LastNearResult=__result;}
        private static void ColdHandoffReadResult(object __instance,object __0,long __1,bool __result)
        {var o=coldHandoffObservation;if(o?.Watching==true && ReferenceEquals(o.Host,__instance)){o.LastReadRange=__0;o.LastReadVersion=__1;o.LastReadResult=__result;}}
        private static void ColdHandoffStampResult(long __0,int __1,long __2,bool __result)
        {var o=coldHandoffObservation;if(o?.Watching==true){o.LastStampWatch=__0;o.LastStampPart=__1;o.LastStampValue=__2;o.LastStampResult=__result;}}
        private static void ColdHandoffAdvanceBefore(object __instance,out ColdHandoffGateState __state)
        {var o=coldHandoffObservation;var flight=o==null?null:GetOptional(o.Host,"preparedBuild");__state=new ColdHandoffGateState{Flight=flight,Relevant=o?.Watching==true && ReferenceEquals(o.Host,__instance) && o.Foreign(flight)};}
        private static void ColdHandoffAdvanceResult(object __instance,ColdHandoffGateState __state)
        {
            var o=coldHandoffObservation;if(o==null || !__state.Relevant)return;var next=GetOptional(o.Host,"preparedBuild");
            if(!ReferenceEquals(next,__state.Flight))o.Emit("discard-"+Get(__state.Flight,"Generation"),"Advance prepared changed old["+o.Flight(__state.Flight)+"] next["+o.Flight(next)+"] "+o.Proofs());
        }
        private static void ColdHandoffOrdinaryResult(object __instance,bool __result)
        {
            var o=coldHandoffObservation;if(o?.Watching==true && ReferenceEquals(o.Host,__instance))
                o.Emit("ordinary-"+o.RejectionPhase+"-"+__result,"TryGetBuildTarget="+__result+" afterGateFailure="+o.RejectionPhase+" "+o.Proofs());
        }
        private static void ColdHandoffPreferredResult(object __instance,object __result)
        {
            var value=coldHandoffObservation;
            if(value!=null && ReferenceEquals(value.Host,__instance) && value.NegativeWorld==0 && value.AfterA?.Invoke()==true && __result.ToString()=="NoRunnable")
                value.NegativeWorld=Main.GameUpdateCount;
            if(value?.Watching==true && ReferenceEquals(value.Host,__instance))value.Emit("preferred-"+value.RejectionPhase+"-"+__result,"TryPreferredBuild="+__result+" afterGateFailure="+value.RejectionPhase+" "+value.Proofs());
        }
        // Real normalized file, genuine qualification and consumers. The first
        // run starts at Place/Enable without WaitReady or preparation-only ticks;
        // the next instance retains this exact source for the warm comparison.
        internal static void SchedulerColdHandoffRuntime(object context,object host,object tools,object input)
        {
            // This temporary narrow witness belongs only to the explicit
            // non-formal diagnostic. Inherited environment cannot shrink the
            // real Cadence family or change any original opportunity assertion.
            bool trace=(string)typeof(NativeScopeDispatch).GetField("requested",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)=="BlueprintColdHandoffDiagnostic" &&
                Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT")==null && Environment.GetEnvironmentVariable("JUEMINGR_PA_HANDOFF_TRACE")=="1";
            if(trace)Console.WriteLine("BlueprintColdHandoffGate diagnostic=stack4-cold-only rows<=64 no-extra-production-query=true formal=NOT_EVALUATED");
            bool allOpportunities=true;
            foreach(int firstStack in trace?new[]{4}:new[]{1,4})
            {
                BusinessEvent[] cold, warm;
                using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,2200,600))
                using(var scope=new BusinessScope(context,host,tools,input))
                using(var observation=new ColdHandoffObservation(host,trace))
                {
                    var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                    var a=BusinessStock(p,12,ItemID.WoodWall,firstStack);
                    var b=BusinessStock(p,13,ItemID.StoneWall,3);scope.Sources=new[]{a,b};
                    observation.BType=b.Type;
                    var entry=SchedulerColdRealAsset(host,257,true);
                    BlueprintProjectionQualificationReceipt firstQualification=null;
                    BusinessEvent[][] actual=new BusinessEvent[2][];
                    for(int run=0;run<(trace?1:2);run++)
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
                            if(observation.Watching && observation.Foreign(prepared))observation.Emit("prepared-"+Get(prepared,"Generation"),"prepared observed "+observation.Flight(prepared));
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
                uint expected=unchecked(original[1].World-original[0].World),coldGap=unchecked(cold[1].World-cold[0].World);
                uint? warmGap=warm==null?(uint?)null:unchecked(warm[1].World-warm[0].World);
                Console.WriteLine("BlueprintSchedulerColdHandoffCompare stack="+firstStack+" originalGap="+expected+" coldGap="+coldGap+" warmGap="+(warmGap?.ToString()??"NOT_RUN_DIAGNOSTIC"));
                allOpportunities&=coldGap==expected && (trace || warmGap==expected);
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
