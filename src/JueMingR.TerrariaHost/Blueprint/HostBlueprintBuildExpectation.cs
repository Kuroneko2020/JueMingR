using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // The plan owns semantic source facts, never source windows or item
        // leases. It survives an admitted material change; cancellation retires
        // it. World truth and exact native permission are rebound before use.
        private sealed class NearBuildFact
        {
            internal RepeatedBuildSeed Seed;
            internal BlueprintBuildOperation Operation,CapabilityOperation;
            internal int Type, Supply,Reach,ContinuationType;
            internal int RestoreTilePaint,RestoreWallPaint,RestoreFullbright,RestoreInvisible;
            internal Tools.ToolBuildConsumer Consumer;
            internal long Version=-1, Support=-1, Wake=-1, SupplyRevision=-1, Player=-1;
            internal long Container,Restoration,PlanGroup;internal ulong Dependencies;internal BlueprintBuildWait Wait;
            internal ulong Retry;
#if DEBUG
            internal ulong ObservedNativeRetry;
#endif
            internal bool Ready, Pending=true;
            internal NearCapabilityDependency Capability;
            internal long CapabilitySerial=-1;
        }
        private sealed class NearBuildRange
        {
            internal readonly Dictionary<int,NearBuildFact> Facts=new Dictionary<int,NearBuildFact>();
            internal int Cursor;
            internal bool Complete,Initialized,WiringPass;
            internal ulong ExtractionTick;
            internal bool ExtractionWaiting;
            internal BlueprintProjectionBounds Bounds,Covered,WiringCovered;
            internal readonly List<int> Retired=new List<int>();
            internal BlueprintWorldObject Body;
            internal BlueprintProjectionBounds LastNear,RetirementBounds;
            internal long Version=-1,Support=-1,Wake=-1,Container=-1,Restoration=-1,Group=-1,SupplyRevision=-1;
            internal bool RawCurrent,NeedsClassification=true,Pending=true,WaitContainer,WaitRestoration;
            internal ulong Dependencies;
            internal readonly Dictionary<NearCapabilityDependency,long> Capabilities=new Dictionary<NearCapabilityDependency,long>();
            internal ulong NextRetry;
            internal readonly Dictionary<int,long> SupplyTypes=new Dictionary<int,long>();
        }
        private BuildFocusDiscovery nearBuildDiscovery=new BuildFocusDiscovery();
        private readonly Dictionary<BlueprintProjectionMatchRange,NearBuildRange> nearBuildRanges=new Dictionary<BlueprintProjectionMatchRange,NearBuildRange>();
        private NearBuildRange capturingNearRange;
        private int nearRangeCursor;
        private bool nearCoverageCurrent;
        private Vector2 nearPosition;
        private int nearWidth,nearHeight,nearDirection,nearPulleyDirection,nearBlockRange,nearReachX,nearReachY;
        private bool nearPulley,nearProofNoSupply;
        private long nearSelection=-1,nearSession=-1,nearPolicy=-1,nearCatalog=-1;
        private NearBuildFact nearSelected, nearWaitingFact, nearPreferredHead, nearForeignHead;
        private BuildContinuation nearProofGroup;
        private bool nearAbsenceReady,nearPlanDirty=true;
        private readonly Dictionary<NearCapabilityDependency,long> nearProofCapabilities=new Dictionary<NearCapabilityDependency,long>();
        private sealed class NearBuildRangeProof
        {
            internal long Version,RawVersion,Wake,Support;
        }
        private long nearProofContainer=-1,nearProofRestoration=-1;
        private ulong nearProofDependencies,nearProofRetry;
        private readonly Dictionary<int,long> nearProofMaterialDependencies=new Dictionary<int,long>();
        private readonly Dictionary<int,int> nearProofMaterialPresence=new Dictionary<int,int>();
        private readonly Dictionary<BlueprintProjectionMatchRange,NearBuildRangeProof> nearProofSupports=new Dictionary<BlueprintProjectionMatchRange,NearBuildRangeProof>();
        private readonly Dictionary<int,int> nearLiveMaterialSupply=new Dictionary<int,int>();
        private readonly List<BlueprintProjectionMatchRange> nearRetiredRanges=new List<BlueprintProjectionMatchRange>();
#if DEBUG
        internal long NearBuildClassifications { get; private set; }
        internal long NearBuildHeadChecks { get; private set; }
        internal long NearBuildProofChecks { get; private set; }
        internal long NearBuildFactChecks { get; private set; }
        internal long NearBuildRetirementChecks { get; private set; }
        internal long NearBuildRangeChecks { get; private set; }
        internal long NearBuildCapabilityChecks { get; private set; }
        internal long NearBuildSemanticSteps { get; private set; }
        internal long NearBuildPlanBytes
        {
            get { long bytes=128+NearCapabilityPlanBytes;foreach(var range in nearBuildRanges.Values){bytes+=112+range.Retired.Capacity*4L;if(range.Body!=null)bytes+=128+range.Body.Width*range.Body.Height*8L;bytes+=range.Facts.Count*272L;}return bytes; }
        }
        internal int NearBuildFacts { get { int count=0;foreach(var range in nearBuildRanges.Values)count+=range.Facts.Count;return count; } }
#endif
        private void ClearNearBuildPlan()
        {
            // Closed/no-session callbacks may repeat every Update. Retire a
            // real plan once; an already empty owner allocates no new sets.
            if(nearSession<0 && nearBuildRanges.Count==0 && nearBuildDiscovery.Owners.Count==0 && nearBuildDiscovery.Members.Count==0)return;
            ClearNearCapabilityDependencies();
            nearBuildRanges.Clear();nearBuildDiscovery=new BuildFocusDiscovery();nearSelected=nearPreferredHead=nearForeignHead=null;
            nearProofGroup=null;nearProofSupports.Clear();nearProofMaterialDependencies.Clear();nearProofMaterialPresence.Clear();nearLiveMaterialSupply.Clear();nearProofCapabilities.Clear();nearAbsenceReady=false;nearPlanDirty=true;nearProofNoSupply=false;
            nearCoverageCurrent=false;nearSession=nearSelection=nearPolicy=nearCatalog=-1;nearRangeCursor=0;
        }
        private void CaptureNearBuildSeed(BlueprintBuildTarget target)
        {
            if(capturingNearRange==null || capturingNearRange.Facts.ContainsKey(target.Ordinal))return;
            if(capturingNearRange.Body==null && target.Body!=null)capturingNearRange.Body=target.Body;
            capturingNearRange.Facts.Add(target.Ordinal,new NearBuildFact{Seed=new RepeatedBuildSeed(target,capturingNearRange.Body)});capturingNearRange.NeedsClassification=true;nearPlanDirty=true;
        }
        private bool NearFactCurrent(NearBuildFact fact,Player p)
        {
            long support;var range=fact.Seed.Range;
            return RepeatedBuildIdentityCurrent(fact.Seed) && range.Version==fact.Version &&
                fact.Player>=0 && NearFactReachCurrent(fact) && NearFactSupplyCurrent(fact) &&
                ((fact.Wait&BlueprintBuildWait.Container)==0 || fact.Container==buildContainerRevision) &&
                ((fact.Wait&BlueprintBuildWait.Restoration)==0 || fact.Restoration==appearanceRestorationRevision) &&
                ((fact.Wait&BlueprintBuildWait.Dependencies)==0 || !BuildDependencyReleased(fact.Dependencies)) &&
                NearPlayerCurrent(p) &&
                (fact.Retry==0 || materialTick<fact.Retry) && ProjectionMatchReadCurrent(range,range.RawVersion) &&
                projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support) && support==fact.Support;
        }
        private bool NearPlayerCurrent(Player p)
        {return nearPosition==p.position && nearWidth==p.width && nearHeight==p.height && nearDirection==p.direction && nearPulleyDirection==p.pulleyDir && nearPulley==p.pulley && nearBlockRange==p.blockRange && nearReachX==Player.tileRangeX && nearReachY==Player.tileRangeY;}
        private bool NearFactReachCurrent(NearBuildFact fact)
        {
            // A completed world-only negative does not depend on an unrelated
            // Item becoming qualified when our selection override returns.
            // Only a ready/player-rejected plan depends on its actual reach.
            if(!fact.Ready && (fact.Wait&BlueprintBuildWait.Player)==0)return true;
            int reach,type=fact.Type>0?fact.Type:fact.Seed.Placement?.UseItemType??0;
            buildItemReach.TryGetValue(type,out reach);return reach==fact.Reach;
        }
        private bool NearFactSupplyCurrent(NearBuildFact fact)
        {
            if((fact.Wait&(BlueprintBuildWait.Material|BlueprintBuildWait.Capability))==0)return true;
            if(NearMaterialRecovered(fact.RestoreTilePaint,fact.SupplyRevision) || NearMaterialRecovered(fact.RestoreWallPaint,fact.SupplyRevision) ||
                NearMaterialRecovered(fact.RestoreFullbright,fact.SupplyRevision) || NearMaterialRecovered(fact.RestoreInvisible,fact.SupplyRevision))return false;
            if((fact.Wait&BlueprintBuildWait.Capability)!=0 && !NearCapabilityRecoveryCurrent(fact))return false;
            if((fact.Wait&BlueprintBuildWait.Capability)!=0)
            {long supplyRecovered;return !(fact.Supply>0 && buildSupply.TryGetValue(fact.Supply,out supplyRecovered) && supplyRecovered>fact.SupplyRevision);}
            long recovered;int requested=fact.Seed.Placement?.UseItemType??0;
            return !(buildSupply.TryGetValue(requested,out recovered) && recovered>fact.SupplyRevision) &&
                !(fact.Type>0 && buildSupply.TryGetValue(fact.Type,out recovered) && recovered>fact.SupplyRevision) &&
                !(fact.ContinuationType>0 && buildSupply.TryGetValue(fact.ContinuationType,out recovered) && recovered>fact.SupplyRevision) &&
                !(fact.Supply>0 && buildSupply.TryGetValue(fact.Supply,out recovered) && recovered>fact.SupplyRevision);
        }
        private bool NearMaterialRecovered(int type,long cutoff)
        {long recovered;return type>0 && buildSupply.TryGetValue(type,out recovered) && recovered>cutoff;}
        private bool NearCapabilityRecoveryCurrent(NearBuildFact fact)
        {return fact.Capability!=null && CurrentNearCapabilitySerial(fact.Capability)<=fact.CapabilitySerial;}
        private void SynchronizeNearBuildIdentity()
        {
            if(nearSession==generation && nearSelection==buildTools.SelectionIntent && nearPolicy==substitutionRevision && nearCatalog==MaterialCatalog.Revision)return;
            ClearNearBuildPlan();nearSession=generation;nearSelection=buildTools.SelectionIntent;nearPolicy=substitutionRevision;nearCatalog=MaterialCatalog.Revision;
        }
        private void ReleaseNearBuildRange(NearBuildRange state)
        {foreach(var fact in state.Facts.Values)ReleaseNearCapabilityDependency(fact.Capability);}
        private void ClassifyNearFact(NearBuildFact fact,Player p,ref int allowance)
        {
            if(allowance<=0)return;allowance--;nearPlanDirty=true;
#if DEBUG
            NearBuildClassifications++;
            var retry=fact.Seed.Range.Materials;
            if(retry?.EligibilityRetry==true && retry.EligibilityRetryOrdinal==fact.Seed.Ordinal && materialTick>=retry.EligibilityRetryAt && fact.ObservedNativeRetry!=retry.EligibilityRetryAt)
            {fact.ObservedNativeRetry=retry.EligibilityRetryAt;BuildEligibilityRetryWakes++;}
#endif
            BlueprintBuildTarget target;BuildOperationPlan plan;bool pending;
            ReleaseNearCapabilityDependency(fact.Capability);fact.Capability=null;fact.CapabilitySerial=-1;
            fact.Wait=BlueprintBuildWait.None;fact.Dependencies=0;fact.ContinuationType=0;fact.CapabilityOperation=null;nearWaitingFact=fact;
            fact.RestoreTilePaint=fact.RestoreWallPaint=fact.RestoreFullbright=fact.RestoreInvisible=0;
            try
            {
                var hint=PreferredBuildSourceHint(p);bool preferred=false;pending=false;target=null;plan=null;
                if(buildContinuation!=null && NearSeedMayContinue(fact.Seed))
                {
                    fact.ContinuationType=buildContinuation.Source.Type;
                    preferred=TryRebindBuildSeed(fact.Seed,p,hint,out target,out plan,out pending,BuildPlanningIntent.ContinuationProbe) && PlanContinuesBuild(target,plan);
                    // A pending same-material substitution prefix is not a
                    // negative permission to prefer the original C material.
                    if(!preferred && pending){fact.Ready=false;}
                    else if(!preferred)
                    {
                        if(allowance<=1){fact.Ready=false;pending=true;}
                        else
                        {
                            allowance--;
#if DEBUG
                            NearBuildClassifications++;
#endif
                            fact.Ready=TryRebindBuildSeed(fact.Seed,p,hint,out target,out plan,out pending);
                        }
                    }
                    else fact.Ready=true;
                }
                else fact.Ready=TryRebindBuildSeed(fact.Seed,p,hint,out target,out plan,out pending);
                fact.PlanGroup=buildContinuation?.Group??0;
            }
            finally{nearWaitingFact=null;}
            fact.Pending=pending;
            if((fact.Wait&BlueprintBuildWait.Capability)!=0)
            {
                fact.Capability=GetNearCapabilityDependency(fact.CapabilityOperation??fact.Operation,fact.Seed.Cell,fact.Seed.Object,fact.Type>0?fact.Type:fact.Seed.Placement?.UseItemType??0);
                fact.CapabilitySerial=CurrentNearCapabilitySerial(fact.Capability);
                // A reset before this epoch's publisher leaves this unknown;
                // it cannot sign absence until a qualified snapshot exists.
                if(fact.Capability==null)fact.Pending=true;
            }
            fact.Container=buildContainerRevision;fact.Restoration=appearanceRestorationRevision;fact.Version=fact.Seed.Range.Version;fact.Wake=fact.Seed.Range.Materials?.BuildWakeRevision??-1;
            fact.Player=MaterialBuildEligibilityRevision;fact.SupplyRevision=buildSupplyRevision;
            projectionChanges.TryGetBuildStamp(fact.Seed.Range.Watch,fact.Seed.Range.PartIndex,out fact.Support);
            fact.Retry=pending?materialTick+1:(fact.Wait&BlueprintBuildWait.Dynamic)!=0?materialTick+8:0;
            // Only the actual unfinished plan may register supply waits.
            // A known already-complete body must not acquire a Material wait
            // merely because our own selection temporarily hides its Item.
            if(fact.Ready){fact.Operation=CopyBuildOperation(plan.Operation);fact.Type=plan.ToolType;fact.Supply=plan.SupplyType;fact.Consumer=BuildConsumer(plan.Operation,target);}
            buildItemReach.TryGetValue(fact.Type>0?fact.Type:fact.Seed.Placement?.UseItemType??0,out fact.Reach);
        }
        private void AdvanceNearBuildPlan(Player p,ref int allowance)
        {
            SynchronizeNearBuildIdentity();
            bool moved=!NearPlayerCurrent(p);
            nearPosition=p.position;nearWidth=p.width;nearHeight=p.height;nearDirection=p.direction;nearPulleyDirection=p.pulleyDir;nearPulley=p.pulley;nearBlockRange=p.blockRange;nearReachX=Player.tileRangeX;nearReachY=Player.tileRangeY;
            bool previousCoverage=nearCoverageCurrent;nearCoverageCurrent=RefreshBuildFocusNear(p);
            if(previousCoverage!=nearCoverageCurrent)nearPlanDirty=true;
            var retired=nearRetiredRanges;retired.Clear();
            foreach(var pair in nearBuildRanges)if(!nearBuildDiscovery.Members.Contains(pair.Key))retired.Add(pair.Key);
            foreach(var range in retired){ReleaseNearBuildRange(nearBuildRanges[range]);nearBuildRanges.Remove(range);nearPlanDirty=true;}
            if(moved)nearPlanDirty=true;
            int count=nearBuildDiscovery.Ranges.Count;
            // Fairness applies to discovery, never to native execution. All
            // ranges spend the existing single 64-source-step Update balance.
            int idleRanges=0,rangeVisits=0;
            while(count>0 && idleRanges<count)
            {
                rangeVisits++;nearRangeCursor%=count;var range=nearBuildDiscovery.Ranges[nearRangeCursor];nearRangeCursor=(nearRangeCursor+1)%count;
                NearBuildRange state;if(!nearBuildRanges.TryGetValue(range,out state))nearBuildRanges.Add(range,state=new NearBuildRange());
                var wireBounds=IntersectBuildBounds(BuildFocusBounds(range.Owner,p),range.Source.Bounds);
                var bounds=IntersectBuildBounds(BuildNearOrdinaryBounds(range.Owner,p),range.Source.Bounds);
                if(EmptyBuildBounds(wireBounds)){idleRanges++;continue;}
                if(!state.LastNear.Equals(bounds))
                {
                    var ordinaryCorridor=bounds.Expand(Math.Max(bounds.Width,bounds.Height));
                    state.Covered=IntersectBuildBounds(state.Covered,ordinaryCorridor);
                    state.LastNear=bounds;state.NeedsClassification=true;
                }
                var retainedDomain=buildSupply.ContainsKey(3611)?wireBounds:bounds;
                if(!state.RetirementBounds.Equals(retainedDomain))
                {
                    var corridor=retainedDomain.Expand(Math.Max(retainedDomain.Width,retainedDomain.Height));
                    state.WiringCovered=IntersectBuildBounds(state.WiringCovered,corridor);state.Retired.Clear();
                    foreach(var entry in state.Facts)
                    {
#if DEBUG
                        NearBuildRetirementChecks++;
#endif
                        var seed=entry.Value.Seed;
                        // Seeds execute in display coordinates; coverage is raw source.
                        // Transform the WHOLE object rectangle (not its anchor).
                        var display=seed.Body==null?new BlueprintProjectionBounds(seed.Cell.X,seed.Cell.Y,1,1):
                            new BlueprintProjectionBounds(seed.Body.WorldX-seed.Owner.OriginX,seed.Body.WorldY-seed.Owner.OriginY,seed.Body.Width,seed.Body.Height);
                        if(!BuildBoundsOverlap(corridor,seed.Owner.View.SourceBounds(display)))state.Retired.Add(entry.Key);
                    }
                    foreach(int ordinal in state.Retired){ReleaseNearCapabilityDependency(state.Facts[ordinal].Capability);state.Facts.Remove(ordinal);nearPlanDirty=true;}
                    state.RetirementBounds=retainedDomain;
                }
                bool ordinaryDone=NearBoundsCovered(state.Covered,bounds);
                bool wiringDone=!buildSupply.ContainsKey(3611) || NearBoundsCovered(state.WiringCovered,wireBounds);
                bool coveredNow=ordinaryDone && wiringDone;
                if(state.Complete!=coveredNow)nearPlanDirty=true;
                state.Complete=coveredNow;
                if(!state.Complete && !state.Initialized)
                {
                    state.WiringPass=ordinaryDone;var desired=state.WiringPass?wireBounds:bounds;
                    state.Bounds=NextNearBuildStrip(state.WiringPass?state.WiringCovered:state.Covered,desired);state.Cursor=0;
                }
                if(state.ExtractionTick!=materialTick){state.ExtractionTick=materialTick;state.ExtractionWaiting=false;}
                bool progressed=false;
                if(!state.Complete && !state.ExtractionWaiting && materialBuildSourceBudget>0)
                {
                    int beforeBudget=materialBuildSourceBudget,beforeCursor=state.Cursor;
                    bool beforeInitialized=state.Initialized;
                    var beforeCovered=state.Covered;var beforeWiringCovered=state.WiringCovered;
                    bool available=ExtractNearBuildRange(state,range,p,bounds);
                    progressed=beforeBudget!=materialBuildSourceBudget || beforeCursor!=state.Cursor || beforeInitialized!=state.Initialized ||
                        !beforeCovered.Equals(state.Covered) || !beforeWiringCovered.Equals(state.WiringCovered);
                    state.ExtractionWaiting=!available;
                    // Publish completion in this same paid extraction epoch.
                    // Deferring it would cache a false absence for one more
                    // native opportunity after the final source strip.
                    bool complete=NearBoundsCovered(state.Covered,bounds) &&
                        (!buildSupply.ContainsKey(3611) || NearBoundsCovered(state.WiringCovered,wireBounds));
                    if(state.Complete!=complete)nearPlanDirty=true;
                    state.Complete=complete;
                }
#if DEBUG
                NearBuildRangeChecks++;
#endif
                // Slice exhaustion only yields to other ranges. A complete
                // idle round (real source/support wait), or the unchanged shared
                // budget reaching zero, ends extraction. No worker busy wait.
                idleRanges=progressed?0:idleRanges+1;
                if(materialBuildSourceBudget<=0 && rangeVisits>=count)idleRanges=count;
                long support;bool stamped=projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support);
                bool raw=ProjectionMatchReadCurrent(range,range.RawVersion);
                if(state.Version!=range.Version || state.Support!=(stamped?support:-1) || state.RawCurrent!=raw || state.Wake!=(range.Materials?.BuildWakeRevision??-1) ||
                    state.WaitContainer && state.Container!=buildContainerRevision || state.WaitRestoration && state.Restoration!=appearanceRestorationRevision || state.Dependencies!=0 && BuildDependencyReleased(state.Dependencies) || state.Group!=(buildContinuation?.Group??0) ||
                    moved || state.Pending || state.NextRetry!=0 && materialTick>=state.NextRetry || NearRangeSupplyChanged(state))state.NeedsClassification=true;
                state.Version=range.Version;state.Support=stamped?support:-1;state.RawCurrent=raw;state.Wake=range.Materials?.BuildWakeRevision??-1;
                state.Container=buildContainerRevision;state.Restoration=appearanceRestorationRevision;state.Group=buildContinuation?.Group??0;
            }
            // Urgent same-material facts use the common allowance before
            // other groups. Source waits cannot prevent known semantic seeds
            // in that same range from being rebound without their transport.
            for(int pass=0;pass<2;pass++)foreach(var state in nearBuildRanges.Values)
            {
                if(!state.NeedsClassification)continue;
                foreach(var fact in state.Facts.Values)
                {
#if DEBUG
                    NearBuildFactChecks++;
#endif
                    bool preferred=buildContinuation!=null && NearSeedMayContinue(fact.Seed);
                    if((pass==0)!=preferred)continue;
                    if(moved && (fact.Ready || (fact.Wait&BlueprintBuildWait.Player)!=0))fact.Player=-1;
                    if(allowance>1 && (!NearFactCurrent(fact,p) || fact.Pending || fact.PlanGroup!=(buildContinuation?.Group??0) && preferred))ClassifyNearFact(fact,p,ref allowance);
                }
            }
            foreach(var state in nearBuildRanges.Values)
            {
                if(!state.NeedsClassification)continue;
                state.Pending=false;state.NextRetry=0;state.WaitContainer=state.WaitRestoration=false;state.Dependencies=0;state.SupplyTypes.Clear();state.Capabilities.Clear();
                foreach(var fact in state.Facts.Values)
                {
#if DEBUG
                    NearBuildFactChecks++;
#endif
                    state.Pending|=fact.Pending || !NearFactCurrent(fact,p);
                    if(fact.Retry>materialTick && (state.NextRetry==0 || fact.Retry<state.NextRetry))state.NextRetry=fact.Retry;
                    state.WaitContainer|=(fact.Wait&BlueprintBuildWait.Container)!=0;
                    state.WaitRestoration|=(fact.Wait&BlueprintBuildWait.Restoration)!=0;
                    if((fact.Wait&BlueprintBuildWait.Dependencies)!=0)state.Dependencies|=fact.Dependencies;
                    if(fact.Capability!=null)state.Capabilities[fact.Capability]=fact.CapabilitySerial;
                    if(fact.Ready || (fact.Wait&BlueprintBuildWait.Material)!=0)
                    {
                        AddNearRangeSupply(state,fact.Type);AddNearRangeSupply(state,fact.Supply);
                        AddNearRangeSupply(state,fact.RestoreTilePaint);AddNearRangeSupply(state,fact.RestoreWallPaint);
                        AddNearRangeSupply(state,fact.RestoreFullbright);AddNearRangeSupply(state,fact.RestoreInvisible);
                        if((fact.Wait&(BlueprintBuildWait.Material|BlueprintBuildWait.Capability))==BlueprintBuildWait.Material){AddNearRangeSupply(state,fact.Seed.Placement?.UseItemType??0);AddNearRangeSupply(state,fact.ContinuationType);}
                    }
                }
                state.SupplyRevision=buildSupplyRevision;state.NeedsClassification=false;
            }
            RankNearBuildHeads(p);
        }
        private void AddNearRangeSupply(NearBuildRange range,int type)
        {if(type>0){long value;buildSupply.TryGetValue(type,out value);range.SupplyTypes[type]=value;}}
        private bool NearRangeSupplyChanged(NearBuildRange range)
        {
            foreach(var entry in range.SupplyTypes){long value;buildSupply.TryGetValue(entry.Key,out value);if(value!=entry.Value)return true;}
            foreach(var entry in range.Capabilities)if(CurrentNearCapabilitySerial(entry.Key)>entry.Value)return true;
            return false;
        }
        private bool ExtractNearBuildRange(NearBuildRange state,BlueprintProjectionMatchRange range,Player p,BlueprintProjectionBounds bounds)
        {
                    var part=FindBuildSource(range);
                    if(part==null || BuildMemberIndex(state.Cursor)<part.Offset){DemandProjectionMatchRange(range);return false;}
                    int steps=BuildSourceSlice;long support;
                    if(!projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support)){DemandProjectionMatchRange(range);return false;}
                    var cells=ProjectionSourceCells(part.Window);
                    if(!state.Initialized && part.Offset==0)
                    {state.Cursor=BuildMemberOrdinal((part.LogicalObjectId>0?0:BuildCellLowerBound(cells,state.Bounds.Left,state.Bounds.Top)));state.Initialized=true;}
                    while(steps>0 && materialBuildSourceBudget>0)
                    {
                        int index=BuildMemberIndex(state.Cursor)-part.Offset;
                        if(index>=cells.Count){if(part.Complete){CompleteNearBuildStrip(state,bounds);return true;}DemandProjectionMatchRange(range);return false;}
                        var cell=cells[index];
                        if(part.LogicalObjectId==0 && cell.Y>=state.Bounds.Bottom){CompleteNearBuildStrip(state,bounds);return true;}
                        materialBuildSourceBudget--;steps--;
#if DEBUG
                        ProjectionMatchSourceSteps++;NearBuildSemanticSteps++;
#endif
                        if(part.LogicalObjectId==0 && !state.Bounds.Contains(cell.X,cell.Y))
                        {
                            state.Cursor=BuildMemberOrdinal(part.Offset+BuildCellLowerBound(cells,state.Bounds.Left,cell.X>=state.Bounds.Right?cell.Y+1:Math.Max(cell.Y,state.Bounds.Top)));
                            continue;
                        }
                        // The screen extension certifies wiring lanes only.
                        // Ordinary coverage has its own moving native domain;
                        // an object routed into it keeps its complete source
                        // body even when its first member lies across the edge.
                        if(state.WiringPass && BuildLane(state.Cursor)!=BlueprintBuildLane.Wire && BuildLane(state.Cursor)!=BlueprintBuildLane.Actuation)
                        {state.Cursor=NextBuildSourceOrdinal(state.Cursor,cell,part.LogicalObjectId>0);continue;}
                        BlueprintBuildTarget target;int next;
                        capturingNearRange=state;
                        try{CreateConstructionCandidate(range.Owner,range,part,index,state.Cursor,support,out target,out next,true);}
                        finally{capturingNearRange=null;}
                        state.Cursor=NextBuildSourceOrdinal(state.Cursor,cell,part.LogicalObjectId>0);
                    }
                    return true; // Slice/budget ended without an external wait.
        }
        private static BlueprintProjectionBounds NextNearBuildStrip(BlueprintProjectionBounds covered,BlueprintProjectionBounds near)
        {
            covered=IntersectBuildBounds(covered,near);
            if(EmptyBuildBounds(covered))return near;
            if(near.Left<covered.Left)return new BlueprintProjectionBounds(near.Left,covered.Top,covered.Left-near.Left,covered.Height);
            if(near.Right>covered.Right)return new BlueprintProjectionBounds(covered.Right,covered.Top,near.Right-covered.Right,covered.Height);
            if(near.Top<covered.Top)return new BlueprintProjectionBounds(covered.Left,near.Top,covered.Width,covered.Top-near.Top);
            return new BlueprintProjectionBounds(covered.Left,covered.Bottom,covered.Width,near.Bottom-covered.Bottom);
        }
        private static bool NearBoundsCovered(BlueprintProjectionBounds covered,BlueprintProjectionBounds wanted)
        {return EmptyBuildBounds(wanted) || BuildBoundsContain(covered,wanted);}
        private static BlueprintProjectionBounds MergeNearBuildStrip(BlueprintProjectionBounds old,BlueprintProjectionBounds pass)
        {
            if(EmptyBuildBounds(old))return pass;
            if(BuildBoundsContain(old,pass))return old;
            var union=BuildBoundsUnion(old,pass);var intersection=IntersectBuildBounds(old,pass);
            return (long)union.Width*union.Height==(long)old.Width*old.Height+(long)pass.Width*pass.Height-(long)intersection.Width*intersection.Height?union:pass;
        }
        private void CompleteNearBuildStrip(NearBuildRange state,BlueprintProjectionBounds near)
        {
            if(state.WiringPass)state.WiringCovered=MergeNearBuildStrip(state.WiringCovered,state.Bounds);
            else
            {
                state.Covered=MergeNearBuildStrip(state.Covered,state.Bounds);
                // A full ordinary-lane pass also classified its wire members.
                state.WiringCovered=MergeNearBuildStrip(state.WiringCovered,state.Bounds);
            }
            state.Complete=false;state.Initialized=false;state.Cursor=0;nearPlanDirty=true;
        }
        private void RecordNearBuildWait(BlueprintBuildTarget target,BlueprintBuildWait reason,ulong slots)
        {
            var fact=nearWaitingFact;
            if(fact==null || !ReferenceEquals(fact.Seed.Range,target?.Range) || fact.Seed.Ordinal!=target.Ordinal)return;
            fact.Wait|=reason;fact.Dependencies|=slots;
            if((reason&BlueprintBuildWait.Capability)!=0)fact.CapabilityOperation=CopyBuildOperation(target.Operation);
            if(target.Operation?.SupplyType>0)fact.Supply=target.Operation.SupplyType;
        }
        private void RecordNearAppearanceRestorationWait(BlueprintBuildTarget target,int tilePaint,int wallPaint,int fullbright,int invisible)
        {
            var fact=nearWaitingFact;
            if(fact==null || !ReferenceEquals(fact.Seed.Range,target?.Range) || fact.Seed.Ordinal!=target.Ordinal)return;
            // A destructive scrape is not an operation until its foreground
            // can be restored. Subscribe to that prerequisite now, rather than
            // the old operation/body fallback. These four finite material types
            // reuse the qualified 0->positive / one->two restoration serials;
            // the shared Brush family observes tool recovery without a bag scan
            // per fact. Admission still checks the real quantities and reach.
            fact.RestoreTilePaint=tilePaint;fact.RestoreWallPaint=wallPaint;
            fact.RestoreFullbright=fullbright;fact.RestoreInvisible=invisible;
            fact.CapabilityOperation=new BlueprintBuildOperation{Kind=BlueprintBuildOperationKind.PaintTile};
        }
        private void BlockNearBuildFact(BlueprintBuildTarget target,ulong retry)
        {
            NearBuildRange state;NearBuildFact fact;
            if(target==null || !nearBuildRanges.TryGetValue(target.Range,out state) || !state.Facts.TryGetValue(target.Ordinal,out fact))return;
            fact.Ready=fact.Pending=false;fact.Retry=retry;state.NeedsClassification=true;nearPlanDirty=true;
        }
        private void RejectNearBuildCandidate(BlueprintBuildTarget target)
        {
            if(nearSelected==null || !ReferenceEquals(nearSelected.Seed.Range,target.Range) || nearSelected.Seed.Ordinal!=target.Ordinal)return;
            var fact=nearSelected;NearBuildRange state;if(nearBuildRanges.TryGetValue(target.Range,out state))state.NeedsClassification=true;
            nearPlanDirty=true;fact.Ready=false;fact.Pending=target.Readiness==BlueprintBuildReadiness.Deferred;
            fact.Player=MaterialBuildEligibilityRevision;
            fact.Container=buildContainerRevision;fact.Restoration=appearanceRestorationRevision;
            fact.Retry=target.Range.Materials?.EligibilityRetryAt??0;
            ulong dynamic=target.Range.Materials?.DynamicRetryAt??0;
            if(dynamic>materialTick && (fact.Retry<=materialTick || dynamic<fact.Retry))fact.Retry=dynamic;
            if(fact.Retry<=materialTick)fact.Retry=fact.Pending?materialTick+1:0;
        }
        private bool NearFactContinues(NearBuildFact fact)
        {
            return OperationContinuesBuild(buildContinuation,fact.Operation,fact.Type,fact.Supply,fact.Consumer);
        }
        private bool NoNearSupplyNow(Player p)
        {
            var group=buildContinuation;
            if(group==null || group.Operation!=null && group.Operation.Kind!=BlueprintBuildOperationKind.PlaceBase && group.Operation.Kind!=BlueprintBuildOperationKind.ConvertSubstrate ||
                MaterialInventory?.Ready!=true || activeBuild!=null && !activeBuild.Checked && !activeBuild.Rejected || buildMaterialPreparation?.HasWork==true)return false;
            for(int slot=0;slot<50;slot++)
            {
#if DEBUG
                NearBuildProofChecks++;
#endif
                var item=p.inventory[slot];if(item!=null && item.type==group.Source.Type && item.stack>0 && buildTools.BuildInventoryEligible(slot,item))return false;
            }
            return CurrentBuildSourceHint(p)==null;
        }
        private bool NearSeedMayContinue(RepeatedBuildSeed seed)
        {
            var group=buildContinuation;if(group==null)return true;
            bool ordinary=group.Operation==null || group.Operation.Kind==BlueprintBuildOperationKind.PlaceBase || group.Operation.Kind==BlueprintBuildOperationKind.ConvertSubstrate;
            var lane=BuildLane(seed.Ordinal);
            if(!ordinary)
            {
                switch(group.Operation.Kind)
                {
                    case BlueprintBuildOperationKind.Hammer:return lane==BlueprintBuildLane.Shape;
                    case BlueprintBuildOperationKind.Scrape:case BlueprintBuildOperationKind.PaintTile:case BlueprintBuildOperationKind.PaintWall:return lane==BlueprintBuildLane.Appearance;
                    case BlueprintBuildOperationKind.AddWire:return lane==BlueprintBuildLane.Wire && (seed.Cell.Wires&group.Operation.Mode)!=0;
                    case BlueprintBuildOperationKind.AddActuator:case BlueprintBuildOperationKind.SetInactive:case BlueprintBuildOperationKind.RemoveOwnedActuator:return lane==BlueprintBuildLane.Actuation;
                    default:return lane==BlueprintBuildLane.TileBody;
                }
            }
            if(lane!=BlueprintBuildLane.TileBody && lane!=BlueprintBuildLane.WallBody)return false;
            if(seed.Placement==null)return true;
            if(seed.Placement.UseItemType==group.Source.Type)return true;
            var choices=MaterialCatalog.Substitution?.Candidates(seed.Placement,substitutionPolicy);
            if(choices!=null)foreach(var choice in choices)if(choice.UseItemType==group.Source.Type)return true;
            return false;
        }
        private bool NearRangeRelevantToPreference(BlueprintProjectionMatchRange range,Player p)
        {
            if(!BuildBoundsOverlap(range.Source.Bounds,BuildPreferredFocusBounds(range.Owner,p)))return false;
            var group=buildContinuation;if(group==null)return true;
            ushort bits=range.Source.TargetBits;
            if(group.Operation?.Kind==BlueprintBuildOperationKind.AddWire)return (bits&NearBuildWireTargetBits)!=0;
            // Temporary actuator/switch/cleanup chains can originate from a
            // tile-only target with an inactive pose, without actuator bit8.
            if(WiringOperation(group.Operation))return (bits&9)!=0;
            // A scraper's business group deliberately spans tile and wall
            // consumers. Immutable prefiltering must preserve that same group.
            if(group.Operation?.Kind==BlueprintBuildOperationKind.Scrape)return (bits&3)!=0;
            if(group.Consumer==Tools.ToolBuildConsumer.Walls || group.Operation?.Kind==BlueprintBuildOperationKind.PaintWall)return (bits&2)!=0;
            return (bits&1)!=0;
        }
        private bool ComputeNearBuildAbsence(Player p)
        {
            // World absence is independent of a temporarily unavailable own
            // material. Keep this stronger certificate through its natural
            // selection tail; scarcity may serve only as a separate fallback.
            if(!BuildFocusNearCurrent(p) || !NearPlayerCurrent(p))return false;
            // Coverage and the still-unsettled native receipt have different
            // lifetimes. Compute current world coverage during the natural
            // tail; the final reader separately requires Checked/settled.
            // Caching that receipt gate as world absence would defer a legal
            // same-Update Checked -> Pick boundary until another Host Update.
            foreach(var range in nearBuildDiscovery.Ranges)
            {
#if DEBUG
                NearBuildProofChecks++;
#endif
                if(!NearRangeRelevantToPreference(range,p))continue;
                NearBuildRange state;if(!nearBuildRanges.TryGetValue(range,out state))return false;
                var required=IntersectBuildBounds(BuildPreferredFocusBounds(range.Owner,p),range.Source.Bounds);
                if(!NearBoundsCovered(WiringOperation(buildContinuation?.Operation)?state.WiringCovered:state.Covered,required))return false;
                foreach(var fact in state.Facts.Values)
                {
#if DEBUG
                    NearBuildProofChecks++;
#endif
                    if(!NearSeedMayContinue(fact.Seed))continue;
                    if(!NearFactCurrent(fact,p) || fact.Pending)return false;
                    if(fact.Ready && NearFactContinues(fact))return false;
                }
            }
            return true;
        }
        // Ranking/classification is paid once in Update. Pick only reads the
        // cached heads and per-range proof stamps; it never ranks all members.
        private void RankNearBuildHeads(Player p)
        {
            if(!nearPlanDirty && ReferenceEquals(nearProofGroup,buildContinuation))return;
            nearPlanDirty=false;nearPreferredHead=nearForeignHead=null;double preferred=double.MaxValue,foreign=double.MaxValue;
            foreach(var range in nearBuildRanges.Values)foreach(var fact in range.Facts.Values)
            {
#if DEBUG
                NearBuildHeadChecks++;
#endif
                if(!fact.Ready || !NearFactCurrent(fact,p))continue;
                double dx=fact.Seed.Owner.OriginX+fact.Seed.Cell.X-p.Center.X/16,dy=fact.Seed.Owner.OriginY+fact.Seed.Cell.Y-p.Center.Y/16;
                double next=dx*dx+dy*dy;
                if(next<foreign){nearForeignHead=fact;foreign=next;}
                if(NearFactContinues(fact) && next<preferred){nearPreferredHead=fact;preferred=next;}
            }
            nearProofGroup=buildContinuation;nearProofNoSupply=NoNearSupplyNow(p);nearAbsenceReady=ComputeNearBuildAbsence(p);nearProofSupports.Clear();nearProofMaterialDependencies.Clear();nearProofMaterialPresence.Clear();nearProofCapabilities.Clear();nearProofContainer=nearProofRestoration=-1;nearProofDependencies=nearProofRetry=0;
            if(nearAbsenceReady)foreach(var range in nearBuildDiscovery.Ranges)
            {
                if(!NearRangeRelevantToPreference(range,p))continue;
                var state=nearBuildRanges[range];bool relevant=false;
                foreach(var fact in state.Facts.Values)
                {
#if DEBUG
                    NearBuildProofChecks++;
#endif
                    if(!NearSeedMayContinue(fact.Seed))continue;
                    relevant=true;
                    if((fact.Wait&BlueprintBuildWait.Capability)!=0 && fact.Capability!=null)
                    {long cutoff;nearProofCapabilities[fact.Capability]=nearProofCapabilities.TryGetValue(fact.Capability,out cutoff)?Math.Min(cutoff,fact.CapabilitySerial):fact.CapabilitySerial;}
                    if((fact.Wait&BlueprintBuildWait.Container)!=0)nearProofContainer=fact.Container;
                    if((fact.Wait&BlueprintBuildWait.Restoration)!=0)nearProofRestoration=fact.Restoration;
                    if((fact.Wait&BlueprintBuildWait.Dependencies)!=0)nearProofDependencies|=fact.Dependencies;
                    if(fact.Retry!=0 && (nearProofRetry==0 || fact.Retry<nearProofRetry))nearProofRetry=fact.Retry;
                    if((fact.Wait&BlueprintBuildWait.Material)!=0)
                    {
                        if((fact.Wait&BlueprintBuildWait.Capability)==0)
                        {
                            AddNearProofMaterial(fact.Seed.Placement?.UseItemType??0,fact.SupplyRevision,p);
                            AddNearProofMaterial(fact.Type,fact.SupplyRevision,p);AddNearProofMaterial(fact.ContinuationType,fact.SupplyRevision,p);
                        }
                        AddNearProofMaterial(fact.Supply,fact.SupplyRevision,p);
                        AddNearProofMaterial(fact.RestoreTilePaint,fact.SupplyRevision,p);AddNearProofMaterial(fact.RestoreWallPaint,fact.SupplyRevision,p);
                        AddNearProofMaterial(fact.RestoreFullbright,fact.SupplyRevision,p);AddNearProofMaterial(fact.RestoreInvisible,fact.SupplyRevision,p);
                    }
                }
                // Complete immutable classification excludes non-A ranges even
                // if C's world/body is still Unknown. Only actual A-capable
                // ranges own A's final world-stamp validation responsibility.
                if(!relevant)continue;
                long stamp;if(projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out stamp))nearProofSupports[range]=new NearBuildRangeProof{Version=range.Version,RawVersion=range.RawVersion,Wake=range.Materials?.BuildWakeRevision??-1,Support=stamp};
            }
            if(nearProofMaterialDependencies.Count!=0)
            {
                ReadNearMaterialSupply(p,nearLiveMaterialSupply);
                foreach(var dependency in nearProofMaterialDependencies)
                {int amount;nearLiveMaterialSupply.TryGetValue(dependency.Key,out amount);nearProofMaterialPresence[dependency.Key]=amount;}
            }
        }
        private void AddNearProofMaterial(int type,long cutoff,Player p)
        {
            if(type<=0)return;long old;
            nearProofMaterialDependencies[type]=nearProofMaterialDependencies.TryGetValue(type,out old)?Math.Min(old,cutoff):cutoff;
        }
        private void ReadNearMaterialSupply(Player p,Dictionary<int,int> destination)
        {
            destination.Clear();
            // Final negative validation must see a real newly filled slot even
            // before the next published type->slot inventory index. Count only
            // currently qualified main-bag material; foreign endpoints never
            // become type-only permission. Two is the finite restore threshold.
            for(int slot=0;slot<50;slot++)
            {
#if DEBUG
                NearBuildProofChecks++;
#endif
                var item=p.inventory[slot];
                if(item==null || item.IsAir || !buildTools.BuildInventoryEligible(slot,item))continue;
                int amount;destination.TryGetValue(item.type,out amount);destination[item.type]=(int)Math.Min(2L,amount+Math.Min(2,item.stack));
            }
        }
        private bool NearBuildAbsenceCurrent(Player p,bool live=false)
        {
            if(NoNearSupplyNow(p))return true;
            if(nearProofNoSupply && !nearAbsenceReady)return false; // Actual refill revokes a scarcity-only certificate before publication.
            if(!ReferenceEquals(nearProofGroup,buildContinuation) || !nearAbsenceReady || !BuildFocusNearCurrent(p) ||
                !NearPlayerCurrent(p) ||
                activeBuild!=null && !activeBuild.Checked && !activeBuild.Rejected)return false;
            // Completed world-only A facts have no supply dependency. In
            // particular, release of our own selection must not revoke them.
            // These are only the actual A-negative dependencies captured at
            // classification. A late native reply can release them after Host
            // Update, so the unique B admission must read them once again.
#if DEBUG
            NearBuildProofChecks++;
#endif
            if(nearProofContainer>=0 && nearProofContainer!=buildContainerRevision ||
                nearProofRestoration>=0 && nearProofRestoration!=appearanceRestorationRevision ||
                nearProofRetry!=0 && materialTick>=nearProofRetry ||
                nearProofDependencies!=0 && BuildDependencyReleased(nearProofDependencies,true))return false;
            if(nearProofCapabilities.Count!=0)
            {
                // Suggestions use the current published serial. Only a real
                // selection/preparation admission pays the live bag fence;
                // another writer before Acquire requires its own new fence.
                if(live)BeginNearCapabilityLiveValidation(p,true);
                foreach(var capability in nearProofCapabilities)
                    if(CurrentNearCapabilitySerial(capability.Key)>capability.Value || live && LiveNearCapabilityRecovery(capability.Key,capability.Value))return false;
            }
            if(live && nearProofMaterialDependencies.Count!=0)ReadNearMaterialSupply(p,nearLiveMaterialSupply);
            foreach(var dependency in nearProofMaterialDependencies)
            {
#if DEBUG
                NearBuildProofChecks++;
#endif
                long recovered;int amount;nearLiveMaterialSupply.TryGetValue(dependency.Key,out amount);
                if(buildSupply.TryGetValue(dependency.Key,out recovered) && recovered>dependency.Value || live && amount>nearProofMaterialPresence[dependency.Key])return false;
            }
            foreach(var proof in nearProofSupports)
            {
#if DEBUG
                NearBuildProofChecks++;
#endif
                long stamp;
                if(proof.Key.Version!=proof.Value.Version || proof.Key.RawVersion!=proof.Value.RawVersion || (proof.Key.Materials?.BuildWakeRevision??-1)!=proof.Value.Wake ||
                    !ProjectionMatchReadCurrent(proof.Key,proof.Value.RawVersion) || !projectionChanges.TryGetBuildStamp(proof.Key.Watch,proof.Key.PartIndex,out stamp) || stamp!=proof.Value.Support)return false;
            }
            return true;
        }
        private void InvalidateNearBuildHead(NearBuildFact fact,bool pending)
        {
            fact.Ready=false;fact.Pending=pending;fact.Player=-1;
            // A final rebind can fail after classification (including a door
            // member's current restoration prerequisite). Invalidating the
            // head also wakes its range owner: a dirty ranking alone cannot
            // make a sleeping range reclassify this now-unknown fact. The next
            // Update pays the existing allowance; Pick opens no new search.
            NearBuildRange state;
            if(nearBuildRanges.TryGetValue(fact.Seed.Range,out state))state.NeedsClassification=true;
            nearPlanDirty=true;
        }
        private PreferredBuildSearch ReadNearBuildPreference(Player p,out BlueprintBuildTarget target,out BuildOperationPlan plan)
        {
            target=null;plan=null;var fact=nearPreferredHead;
            if(fact!=null && fact.Ready && NearFactCurrent(fact,p) && NearFactContinues(fact))
            {
                bool pending;
                // Rebinding a saved member may expose another operation's
                // prerequisite (for example a door's foreground restoration).
                // That valid plan is not permission to call it the current A.
                if(TryRebindBuildSeed(fact.Seed,p,PreferredBuildSourceHint(p),out target,out plan,out pending,BuildPlanningIntent.ContinuationProbe) && PlanContinuesBuild(target,plan))
                {nearSelected=fact;return PreferredBuildSearch.Found;}
                InvalidateNearBuildHead(fact,pending);
            }
            bool absent=NearBuildAbsenceCurrent(p);buildContinuation.Pending=!absent;
            return absent?PreferredBuildSearch.NoRunnable:PreferredBuildSearch.BudgetPending;
        }
        private bool TryNearBuildTarget(Player p,out BlueprintBuildTarget target)
        {
            target=null;var fact=nearForeignHead;if(fact==null || !fact.Ready || !NearFactCurrent(fact,p))return false;
            BuildOperationPlan plan;bool pending;
            if(!TryRebindBuildSeed(fact.Seed,p,PreferredBuildSourceHint(p),out target,out plan,out pending))
            {InvalidateNearBuildHead(fact,pending);return false;}
            target.Operation=CopyBuildOperation(plan.Operation);nearSelected=fact;return true;
        }
    }
}

