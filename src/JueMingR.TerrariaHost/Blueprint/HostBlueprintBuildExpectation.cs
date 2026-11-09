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
            internal LinkedListNode<NearBuildFact> Visit,RowVisit;
            internal NearBuildRange State;
            internal int RowKey;
            internal int PlayerX,PlayerY,PlayerBoost,PlayerDirection,PlayerPulleyDirection;
            internal bool PlayerInReach,PlayerCollision,PlayerPulley,PlayerNoBuilding,PlayerHammer,PlayerUsesGrand,PlayerGrandAvailable,PlayerGrandVisible;
            internal BlueprintProjectionBounds PlayerBody;
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
            // Source-space lane/row index shares the same semantic facts. It
            // skips retained corridors for current-A coverage; nodes, not
            // dictionary offsets, retain an interrupted local visit.
            internal readonly Dictionary<int,LinkedList<NearBuildFact>>[] Rows=new Dictionary<int,LinkedList<NearBuildFact>>[BuildLaneCount];
            internal int PreferredLane,PreferredY;
            internal LinkedListNode<NearBuildFact> PreferredNode;
            internal bool PreferredInitialized,HasBodies;
#if DEBUG
            internal int FactHighWater;
            internal readonly int[] RowHighWater=new int[BuildLaneCount];
#endif
            internal int Cursor;
            internal bool Complete,Initialized,WiringPass;
            internal ulong ExtractionTick;
            internal bool ExtractionWaiting;
            internal BlueprintProjectionBounds Bounds,Covered,WiringCovered;
            internal readonly List<int> Retired=new List<int>();
            internal BlueprintWorldObject Body;
            internal BlueprintProjectionBounds LastNear,RetirementBounds;
            internal long Version=-1,Support=-1,Wake=-1,Group=-1;
            internal bool RawCurrent;
        }
        private BuildFocusDiscovery nearBuildDiscovery=new BuildFocusDiscovery();
        private readonly Dictionary<BlueprintProjectionMatchRange,NearBuildRange> nearBuildRanges=new Dictionary<BlueprintProjectionMatchRange,NearBuildRange>();
        private NearBuildRange capturingNearRange;
        private int nearRangeCursor,nearPreferredRangeCursor;
        // Stable member nodes survive insertion/removal and Update yields. No
        // dictionary prefix, enumerator or per-frame copy owns classification.
        private readonly LinkedList<NearBuildFact> nearBuildVisits=new LinkedList<NearBuildFact>();
        private LinkedListNode<NearBuildFact> nearBuildVisit;
        private int nearVisitRemaining;
        private bool nearVisitCompleted,nearVisitNeeded=true,nearVisitPending;
        private long nearVisitEpoch,nearVisitStartEpoch;
        private int nearGrandPlayerFacts,nearGrandScreenWidth,nearGrandScreenHeight;
        private Vector2 nearGrandScreen;
        private ulong nearVisitRetry,nearVisitDependencies;
        private long nearVisitContainer=-1,nearVisitRestoration=-1;
        private readonly Dictionary<int,long> nearVisitSupplies=new Dictionary<int,long>();
        private readonly Dictionary<NearCapabilityDependency,long> nearVisitCapabilities=new Dictionary<NearCapabilityDependency,long>();
        private BlueprintProjectionBounds nearNativeRegion,nearPlayerTiles;
        private readonly List<NearBuildFact> nearProofPlayers=new List<NearBuildFact>();
        private bool nearCoverageCurrent;
        private int nearWidth,nearHeight,nearDirection,nearPulleyDirection,nearBlockRange,nearReachX,nearReachY;
        private bool nearPulley,nearProofNoSupply;
        private long nearSelection=-1,nearSession=-1,nearPolicy=-1,nearCatalog=-1;
        private NearBuildFact nearSelected, nearWaitingFact, nearPreferredHead, nearForeignHead;
        private BuildContinuation nearProofGroup,nearHintGroup;
        private readonly NearBuildFact[] nearBuildHints=new NearBuildFact[8],nearOtherHints=new NearBuildFact[8];
        private int nearProofDomainBoost;
        private BlueprintProjectionBounds nearProofWorldDomain;
        private bool nearProofDomainGrand;
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
        private int nearVisitSupplyHighWater,nearVisitCapabilityHighWater;
        internal long NearBuildClassifications { get; private set; }
        internal long NearBuildHeadChecks { get; private set; }
        internal long NearBuildProofChecks { get; private set; }
        internal long NearBuildFactChecks { get; private set; }
        internal long NearBuildRetirementChecks { get; private set; }
        internal long NearBuildRangeChecks { get; private set; }
        internal long NearBuildCapabilityChecks { get; private set; }
        internal long NearBuildSemanticSteps { get; private set; }
        internal long NearBuildNativeRegionChecks { get; private set; }
        internal long NearBuildPlanBytes
        {
            get { long bytes=768+NearCapabilityPlanBytes+4L*nearVisitSupplyHighWater*48+4L*nearVisitCapabilityHighWater*48+nearProofPlayers.Capacity*8L;
                foreach(var range in nearBuildRanges.Values)
                {
                    bytes+=160+range.Retired.Capacity*4L+4L*range.FactHighWater*40;
                    if(range.Body!=null)bytes+=128+range.Body.Width*range.Body.Height*8L;
                    bytes+=range.Facts.Count*480L;
                    for(int lane=0;lane<BuildLaneCount;lane++)if(range.Rows[lane]!=null)bytes+=128+4L*range.RowHighWater[lane]*48+range.Rows[lane].Count*64L;
                }
                return bytes; }
        }
        internal int NearBuildFacts { get { int count=0;foreach(var range in nearBuildRanges.Values)count+=range.Facts.Count;return count; } }
#endif
        private void ClearNearBuildPlan()
        {
            // Closed/no-session callbacks may repeat every Update. Retire a
            // real plan once; an already empty owner allocates no new sets.
            if(nearSession<0 && nearBuildRanges.Count==0 && nearBuildDiscovery.Owners.Count==0 && nearBuildDiscovery.Members.Count==0)return;
            ClearNearCapabilityDependencies();
            nearBuildRanges.Clear();nearBuildVisits.Clear();nearBuildVisit=null;nearVisitRemaining=0;nearVisitEpoch=nearVisitStartEpoch=0;nearGrandPlayerFacts=0;nearVisitNeeded=true;nearVisitRetry=nearVisitDependencies=0;nearVisitContainer=nearVisitRestoration=-1;nearVisitSupplies.Clear();nearVisitCapabilities.Clear();nearBuildDiscovery=new BuildFocusDiscovery();nearSelected=nearPreferredHead=nearForeignHead=null;
            nearProofGroup=nearHintGroup=null;Array.Clear(nearBuildHints,0,nearBuildHints.Length);Array.Clear(nearOtherHints,0,nearOtherHints.Length);nearProofPlayers.Clear();nearProofSupports.Clear();nearProofMaterialDependencies.Clear();nearProofMaterialPresence.Clear();nearLiveMaterialSupply.Clear();nearProofCapabilities.Clear();nearAbsenceReady=false;nearPlanDirty=nearVisitNeeded=true;nearProofNoSupply=false;
            nearCoverageCurrent=false;nearSession=nearSelection=nearPolicy=nearCatalog=-1;nearRangeCursor=nearPreferredRangeCursor=0;
        }
        private void CaptureNearBuildSeed(BlueprintBuildTarget target)
        {
            if(capturingNearRange==null || capturingNearRange.Facts.ContainsKey(target.Ordinal))return;
            if(capturingNearRange.Body==null && target.Body!=null)capturingNearRange.Body=target.Body;
            var fact=new NearBuildFact{Seed=new RepeatedBuildSeed(target,capturingNearRange.Body),State=capturingNearRange};
            int lane=(int)BuildLane(target.Ordinal);fact.RowKey=NearSeedSourceBounds(fact.Seed).Top;
            var rows=capturingNearRange.Rows[lane];if(rows==null)capturingNearRange.Rows[lane]=rows=new Dictionary<int,LinkedList<NearBuildFact>>();
            LinkedList<NearBuildFact> row;if(!rows.TryGetValue(fact.RowKey,out row))rows.Add(fact.RowKey,row=new LinkedList<NearBuildFact>());
            fact.RowVisit=row.AddLast(fact);capturingNearRange.HasBodies|=fact.Seed.Body!=null;
            fact.Visit=nearBuildVisits.AddLast(fact);nearVisitRemaining++;
            capturingNearRange.Facts.Add(target.Ordinal,fact);nearPlanDirty=nearVisitNeeded=true;
#if DEBUG
            capturingNearRange.FactHighWater=Math.Max(capturingNearRange.FactHighWater,capturingNearRange.Facts.Count);
            capturingNearRange.RowHighWater[lane]=Math.Max(capturingNearRange.RowHighWater[lane],rows.Count);
#endif
        }
        private bool NearFactCurrent(NearBuildFact fact,Player p)
        {
            long support;var range=fact.Seed.Range;
            return RepeatedBuildIdentityCurrent(fact.Seed) && range.Version==fact.Version &&
                fact.Player>=0 && NearFactReachCurrent(fact) && NearFactSupplyCurrent(fact) &&
                ((fact.Wait&BlueprintBuildWait.Container)==0 || fact.Container==buildContainerRevision) &&
                ((fact.Wait&BlueprintBuildWait.Restoration)==0 || fact.Restoration==appearanceRestorationRevision) &&
                ((fact.Wait&BlueprintBuildWait.Dependencies)==0 || !BuildDependencyReleased(fact.Dependencies)) &&
                NearFactPlayerCurrent(fact,p) &&
                (fact.Retry==0 || materialTick<fact.Retry) && ProjectionMatchReadCurrent(range,range.RawVersion) &&
                projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support) && support==fact.Support;
        }
        private BlueprintProjectionBounds NativeNearRegion(Player p,int boost)
        {
#if DEBUG
            NearBuildNativeRegionChecks++;
#endif
            int left,top,right,bottom;
            Terraria.DataStructures.TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out right,out bottom,boost);
            return new BlueprintProjectionBounds(left,top,right-left+1,bottom-top+1);
        }
        private static BlueprintProjectionBounds NearPlayerTileBounds(Player p)
        {
            var body=p.Hitbox;int left=(int)Math.Floor(body.Left/16d),top=(int)Math.Floor(body.Top/16d);
            return new BlueprintProjectionBounds(left,top,(int)Math.Ceiling(body.Right/16d)-left,(int)Math.Ceiling(body.Bottom/16d)-top);
        }
        private bool NearPlayerCurrent(Player p)
        {return nearNativeRegion.Equals(NativeNearRegion(p,0)) && nearWidth==p.width && nearHeight==p.height && nearDirection==p.direction && nearPulleyDirection==p.pulleyDir && nearPulley==p.pulley && nearBlockRange==p.blockRange && nearReachX==Player.tileRangeX && nearReachY==Player.tileRangeY;}
        private bool NearFactPlayerCurrent(NearBuildFact fact,Player p)
        {
            // World-only negatives survive movement and our own selection tail.
            // Ready/player negatives retain only target-relative native reach
            // and collision, never a permission borrowed from an old position.
            if(!fact.Ready && (fact.Wait&BlueprintBuildWait.Player)==0)return true;
            return fact.PlayerDirection==p.direction && fact.PlayerPulleyDirection==p.pulleyDir && fact.PlayerPulley==p.pulley && fact.PlayerNoBuilding==p.noBuilding &&
                fact.PlayerInReach==(fact.PlayerUsesGrand?GrandBuildPointVisible(p,fact.PlayerX,fact.PlayerY):NativeNearRegion(p,fact.PlayerBoost+(fact.PlayerHammer?0:p.blockRange)).Contains(fact.PlayerX,fact.PlayerY)) &&
                (!fact.PlayerGrandAvailable || fact.PlayerGrandVisible==GrandBuildPointVisible(p,fact.PlayerX,fact.PlayerY)) &&
                fact.PlayerCollision==new Rectangle(fact.PlayerBody.Left*16,fact.PlayerBody.Top*16,fact.PlayerBody.Width*16,fact.PlayerBody.Height*16).Intersects(p.Hitbox);
        }
        private void RememberNearFactPlayer(NearBuildFact fact,Player p,BlueprintBuildTarget target,BuildOperationPlan plan)
        {
            fact.PlayerX=target?.WorldX??fact.Seed.Owner.OriginX+fact.Seed.Cell.X;
            fact.PlayerY=target?.WorldY??fact.Seed.Owner.OriginY+fact.Seed.Cell.Y;
            fact.PlayerBody=target?.Bounds??new BlueprintProjectionBounds(fact.PlayerX,fact.PlayerY,1,1);
            fact.PlayerHammer=target?.Operation?.Kind==BlueprintBuildOperationKind.Hammer;
            fact.PlayerBoost=plan?.Tool?.tileBoost??NearOperationBoost(target?.Operation,fact.Seed.Placement?.UseItemType??0);
            if(fact.PlayerGrandAvailable)nearGrandPlayerFacts--;
            fact.PlayerUsesGrand=plan?.ToolType==3611 && WiringOperation(target?.Operation);
            fact.PlayerGrandAvailable=WiringOperation(target?.Operation) && WiringToolMatches(target.Operation,3611) && nearCapabilityTypes.ContainsKey(3611);
            if(fact.PlayerGrandAvailable)nearGrandPlayerFacts++;
            // GD uses the target CENTER's real screen predicate, including the
            // sub-tile edge that an integer discovery rectangle cannot express.
            // A compatible GD also wakes a native-tool negative on re-entry.
            fact.PlayerGrandVisible=fact.PlayerGrandAvailable && GrandBuildPointVisible(p,fact.PlayerX,fact.PlayerY);
            fact.PlayerInReach=fact.PlayerUsesGrand?fact.PlayerGrandVisible:NativeNearRegion(p,fact.PlayerBoost+(fact.PlayerHammer?0:p.blockRange)).Contains(fact.PlayerX,fact.PlayerY);
            fact.PlayerCollision=new Rectangle(fact.PlayerBody.Left*16,fact.PlayerBody.Top*16,fact.PlayerBody.Width*16,fact.PlayerBody.Height*16).Intersects(p.Hitbox);
            fact.PlayerDirection=p.direction;fact.PlayerPulleyDirection=p.pulleyDir;fact.PlayerPulley=p.pulley;fact.PlayerNoBuilding=p.noBuilding;
        }
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
        {foreach(var fact in state.Facts.Values)ReleaseNearBuildFact(fact);}
        private void ReleaseNearBuildFact(NearBuildFact fact)
        {
            ReleaseNearCapabilityDependency(fact.Capability);
            if(fact.PlayerGrandAvailable){nearGrandPlayerFacts--;fact.PlayerGrandAvailable=false;}
            var node=fact.Visit;if(node?.List==null)return;
            if(ReferenceEquals(nearBuildVisit,node))nearBuildVisit=nearBuildVisits.Count==1?null:node.Next??nearBuildVisits.First;
            nearBuildVisits.Remove(node);fact.Visit=null;
            var row=fact.RowVisit?.List;
            if(row!=null)
            {
                if(ReferenceEquals(fact.State.PreferredNode,fact.RowVisit))fact.State.PreferredNode=fact.RowVisit.Next;
                row.Remove(fact.RowVisit);fact.RowVisit=null;
                if(row.Count==0)fact.State.Rows[(int)BuildLane(fact.Seed.Ordinal)].Remove(fact.RowKey);
            }
            for(int i=0;i<nearBuildHints.Length;i++){if(ReferenceEquals(nearBuildHints[i],fact))nearBuildHints[i]=null;if(ReferenceEquals(nearOtherHints[i],fact))nearOtherHints[i]=null;}
            if(ReferenceEquals(nearSelected,fact))nearSelected=null;
            if(ReferenceEquals(nearPreferredHead,fact))nearPreferredHead=null;
            if(ReferenceEquals(nearForeignHead,fact))nearForeignHead=null;
            nearVisitRemaining=nearBuildVisits.Count;
        }
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
                if(buildContinuation!=null && NearSeedMayContinue(fact.Seed) && NearSeedInPreferredDomain(fact.Seed,p))
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
            RememberNearFactPlayer(fact,p,target,plan);
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
            bool moved=!NearPlayerCurrent(p) || !nearPlayerTiles.Equals(NearPlayerTileBounds(p));
            bool grandMoved=nearGrandPlayerFacts>0 && (nearGrandScreen!=Main.screenPosition || nearGrandScreenWidth!=Main.screenWidth || nearGrandScreenHeight!=Main.screenHeight);
            nearGrandScreen=Main.screenPosition;nearGrandScreenWidth=Main.screenWidth;nearGrandScreenHeight=Main.screenHeight;
            // Exact screen motion is only an O(1) wake signal for existing GD
            // dependencies. Facts are revisited by the same bounded cursors;
            // ordinary materials gain no screen reach or whole-plan scan.
            nearPlayerTiles=NearPlayerTileBounds(p);nearNativeRegion=NativeNearRegion(p,0);nearWidth=p.width;nearHeight=p.height;nearDirection=p.direction;nearPulleyDirection=p.pulleyDir;nearPulley=p.pulley;nearBlockRange=p.blockRange;nearReachX=Player.tileRangeX;nearReachY=Player.tileRangeY;
            bool previousCoverage=nearCoverageCurrent;nearCoverageCurrent=RefreshBuildFocusNear(p);
            if(previousCoverage!=nearCoverageCurrent)nearPlanDirty=nearVisitNeeded=true;
            var retired=nearRetiredRanges;retired.Clear();
            foreach(var pair in nearBuildRanges)if(!nearBuildDiscovery.Members.Contains(pair.Key))retired.Add(pair.Key);
            foreach(var range in retired){ReleaseNearBuildRange(nearBuildRanges[range]);nearBuildRanges.Remove(range);nearPlanDirty=nearVisitNeeded=true;}
            if(moved || grandMoved){nearVisitEpoch++;nearPlanDirty=nearVisitNeeded=true;}
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
                    state.LastNear=bounds;
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
                    foreach(int ordinal in state.Retired){ReleaseNearBuildFact(state.Facts[ordinal]);state.Facts.Remove(ordinal);nearPlanDirty=nearVisitNeeded=true;}
                    state.RetirementBounds=retainedDomain;
                }
                bool ordinaryDone=NearBoundsCovered(state.Covered,bounds);
                bool wiringDone=!buildSupply.ContainsKey(3611) || NearBoundsCovered(state.WiringCovered,wireBounds);
                bool coveredNow=ordinaryDone && wiringDone;
                if(state.Complete!=coveredNow)nearPlanDirty=nearVisitNeeded=true;
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
                    if(state.Complete!=complete)nearPlanDirty=nearVisitNeeded=true;
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
                if(state.Group!=(buildContinuation?.Group??0))state.PreferredInitialized=false;
                if(state.Version!=range.Version || state.Support!=(stamped?support:-1) || state.RawCurrent!=raw ||
                    state.Group!=(buildContinuation?.Group??0)){nearVisitEpoch++;nearPlanDirty=nearVisitNeeded=true;}
                state.Version=range.Version;state.Support=stamped?support:-1;state.RawCurrent=raw;state.Wake=range.Materials?.BuildWakeRevision??-1;
                state.Group=buildContinuation?.Group??0;
            }
            // Service a known A hint first, retaining one slot for the unique
            // preparer. A bad hint is visited once, then the persistent ring
            // advances all groups fairly with the same allowance.
            nearVisitCompleted=false;
            if(nearVisitRetry!=0 && materialTick>=nearVisitRetry || nearVisitContainer>=0 && nearVisitContainer!=buildContainerRevision ||
                nearVisitRestoration>=0 && nearVisitRestoration!=appearanceRestorationRevision || nearVisitDependencies!=0 && BuildDependencyReleased(nearVisitDependencies)){nearVisitEpoch++;nearVisitNeeded=true;}
            foreach(var dependency in nearVisitSupplies){long recovered;if(buildSupply.TryGetValue(dependency.Key,out recovered) && recovered>dependency.Value){nearVisitEpoch++;nearVisitNeeded=true;}}
            foreach(var dependency in nearVisitCapabilities)if(CurrentNearCapabilitySerial(dependency.Key)>dependency.Value){nearVisitEpoch++;nearVisitNeeded=true;}
            if(nearVisitNeeded && nearVisitRemaining==nearBuildVisits.Count)
            {nearVisitStartEpoch=nearVisitEpoch;nearVisitPending=false;nearVisitRetry=nearVisitDependencies=0;nearVisitContainer=nearVisitRestoration=-1;nearVisitSupplies.Clear();nearVisitCapabilities.Clear();}
            if(!ReferenceEquals(nearHintGroup,buildContinuation))
            {Array.Clear(nearBuildHints,0,nearBuildHints.Length);nearHintGroup=buildContinuation;}
            if(nearPreferredHead!=null && (!nearPreferredHead.Ready || !NearFactCurrent(nearPreferredHead,p) || !NearFactContinues(nearPreferredHead)))nearPreferredHead=null;
            // A writer invalidates world permits, not the other saved member
            // identities. Rebind a few positive A alternatives before the
            // background ring so one consumed/bad head cannot cost a full lap.
            int tried=0;
            for(int n=0;n<nearBuildHints.Length && nearPreferredHead==null && allowance>3;n++)
            {
                int index=-1;double nearest=double.MaxValue;
                for(int i=0;i<nearBuildHints.Length;i++)
                {
#if DEBUG
                    NearBuildHeadChecks++;
#endif
                    var candidate=nearBuildHints[i];
                    if((tried&(1<<i))!=0 || candidate==null || candidate.Visit?.List!=nearBuildVisits || !NearSeedInPreferredDomain(candidate.Seed,p))continue;
                    double distance=NearHeadDistance(candidate,p);if(distance<nearest){nearest=distance;index=i;}
                }
                if(index<0)break;tried|=1<<index;var hint=nearBuildHints[index];
                if(!NearFactCurrent(hint,p) || hint.Pending || hint.PlanGroup!=(buildContinuation?.Group??0))ClassifyNearFact(hint,p,ref allowance);
                if(hint.Ready && NearFactCurrent(hint,p) && NearFactContinues(hint))nearPreferredHead=hint;
                else if(!hint.Pending)nearBuildHints[index]=null;
            }
            if(nearForeignHead!=null && (!nearForeignHead.Ready || !NearFactCurrent(nearForeignHead,p)))nearForeignHead=null;
            if(buildContinuation!=null && nearVisitNeeded && allowance>3)
            {
                int urgent=nearPreferredHead==null?48:24,rangeCount=nearBuildDiscovery.Ranges.Count;
                for(int n=0;n<rangeCount && allowance>3 && urgent>0;n++)
                {
                    nearPreferredRangeCursor%=rangeCount;var range=nearBuildDiscovery.Ranges[nearPreferredRangeCursor];
                    NearBuildRange state;
                    bool complete=!nearBuildRanges.TryGetValue(range,out state) || !NearRangeRelevantToPreference(range,p) || AdvanceNearPreferredRange(state,range,p,ref allowance,ref urgent);
                    if(complete)nearPreferredRangeCursor=(nearPreferredRangeCursor+1)%rangeCount;
                    else break;
                }
            }
            ServiceNearOtherHint(p,ref allowance);
            int visits=nearBuildVisits.Count;
            while(nearVisitNeeded && visits-->0 && allowance>2 && nearBuildVisits.Count>0)
            {
                var node=nearBuildVisit?.List==nearBuildVisits?nearBuildVisit:nearBuildVisits.First;nearBuildVisit=node.Next??nearBuildVisits.First;
                var fact=node.Value;
#if DEBUG
                NearBuildFactChecks++;
#endif
                // Even a cheap retained/current visit spends a bounded visit
                // slot. Classification itself keeps its original probe charge.
                int before=allowance;
                var domain=BuildLane(fact.Seed.Ordinal)==BlueprintBuildLane.Wire || BuildLane(fact.Seed.Ordinal)==BlueprintBuildLane.Actuation?
                    BuildFocusBounds(fact.Seed.Owner,p):BuildNearOrdinaryBounds(fact.Seed.Owner,p);
                bool inDomain=BuildBoundsOverlap(domain,NearSeedSourceBounds(fact.Seed));
                if(inDomain &&
                    (!NearFactCurrent(fact,p) || fact.Pending && (fact.Retry==0 || materialTick>=fact.Retry) ||
                     fact.PlanGroup!=(buildContinuation?.Group??0) && NearSeedMayContinue(fact.Seed)))
                    ClassifyNearFact(fact,p,ref allowance);
                if(before==allowance)allowance--;
                ConsiderNearBuildHead(fact,p);
                // Retained corridor waits wake when their domain re-enters;
                // they cannot keep an otherwise settled active domain busy.
                if(inDomain)ObserveNearVisitWait(fact);
                if(--nearVisitRemaining<=0){nearVisitCompleted=true;nearVisitRemaining=nearBuildVisits.Count;nearVisitNeeded=nearVisitPending || nearVisitStartEpoch!=nearVisitEpoch;break;}
            }
            RankNearBuildHeads(p);
        }
        private bool NearPreferredLane(int lane)
        {
            var group=buildContinuation;if(group==null)return false;
            var kind=group.Operation?.Kind;
            if(kind==null || kind==BlueprintBuildOperationKind.PlaceBase || kind==BlueprintBuildOperationKind.ConvertSubstrate)
                return lane==(int)(group.Consumer==Tools.ToolBuildConsumer.Walls?BlueprintBuildLane.WallBody:BlueprintBuildLane.TileBody);
            if(kind==BlueprintBuildOperationKind.Hammer)return lane==(int)BlueprintBuildLane.Shape;
            if(kind==BlueprintBuildOperationKind.Scrape || kind==BlueprintBuildOperationKind.PaintTile || kind==BlueprintBuildOperationKind.PaintWall)return lane==(int)BlueprintBuildLane.Appearance;
            if(kind==BlueprintBuildOperationKind.AddWire)return lane==(int)BlueprintBuildLane.Wire;
            return lane==(int)BlueprintBuildLane.Actuation || lane==(int)BlueprintBuildLane.TileBody;
        }
        private bool AdvanceNearPreferredRange(NearBuildRange state,BlueprintProjectionMatchRange range,Player p,ref int allowance,ref int urgent)
        {
            var required=IntersectBuildBounds(BuildPreferredFocusBounds(range.Owner,p),range.Source.Bounds);
            var rowsBounds=state.HasBodies?range.Source.Bounds:required;
            if(EmptyBuildBounds(rowsBounds))return true;
            if(!state.PreferredInitialized){state.PreferredLane=0;state.PreferredY=rowsBounds.Top;state.PreferredInitialized=true;}
            while(state.PreferredLane<BuildLaneCount && allowance>3 && urgent>0)
            {
                int lane=state.PreferredLane;
                if(!NearPreferredLane(lane) || state.Rows[lane]==null){state.PreferredLane++;state.PreferredY=rowsBounds.Top;state.PreferredNode=null;continue;}
                if(state.PreferredY<rowsBounds.Top)state.PreferredY=rowsBounds.Top;
                if(state.PreferredY>=rowsBounds.Bottom){state.PreferredLane++;state.PreferredY=rowsBounds.Top;state.PreferredNode=null;continue;}
                LinkedList<NearBuildFact> row;
                if(!state.Rows[lane].TryGetValue(state.PreferredY,out row))
                {state.PreferredY++;state.PreferredNode=null;allowance--;urgent--;continue;}
                var node=state.PreferredNode?.List==row?state.PreferredNode:row.First;
                while(node!=null && allowance>3 && urgent>0)
                {
                    var fact=node.Value;node=node.Next;state.PreferredNode=node;
#if DEBUG
                    NearBuildFactChecks++;
#endif
                    int before=allowance;
                    if(NearSeedMayContinue(fact.Seed) && BuildBoundsOverlap(required,NearSeedSourceBounds(fact.Seed)))
                    {
                        if(!NearFactCurrent(fact,p) || fact.Pending && (fact.Retry==0 || materialTick>=fact.Retry) || fact.PlanGroup!=buildContinuation.Group)ClassifyNearFact(fact,p,ref allowance);
                        ConsiderNearBuildHead(fact,p);
                    }
                    if(before==allowance)allowance--;
                    urgent-=before-allowance;
                    if(node==null){state.PreferredY++;state.PreferredNode=null;}
                }
            }
            if(state.PreferredLane<BuildLaneCount)return false;
            state.PreferredInitialized=false;return true;
        }
        private IEnumerable<NearBuildFact> NearPreferredFacts(NearBuildRange state,BlueprintProjectionBounds required)
        {
            for(int lane=0;lane<BuildLaneCount;lane++)
            {
                if(!NearPreferredLane(lane) || state.Rows[lane]==null)continue;
                foreach(var row in state.Rows[lane])
                {
                    if(!state.HasBodies && (row.Key<required.Top || row.Key>=required.Bottom))continue;
                    foreach(var fact in row.Value)
                    {
#if DEBUG
                        NearBuildProofChecks++;
#endif
                        if(NearSeedMayContinue(fact.Seed) && BuildBoundsOverlap(required,NearSeedSourceBounds(fact.Seed)))yield return fact;
                    }
                }
            }
        }
        private void ObserveNearVisitWait(NearBuildFact fact)
        {
            nearVisitPending|=fact.Pending;
            if(fact.Retry>materialTick && (nearVisitRetry==0 || fact.Retry<nearVisitRetry))nearVisitRetry=fact.Retry;
            if((fact.Wait&BlueprintBuildWait.Container)!=0)nearVisitContainer=fact.Container;
            if((fact.Wait&BlueprintBuildWait.Restoration)!=0)nearVisitRestoration=fact.Restoration;
            nearVisitDependencies|=fact.Dependencies;
            if(fact.Capability!=null)
            {
                nearVisitCapabilities[fact.Capability]=fact.CapabilitySerial;
#if DEBUG
                nearVisitCapabilityHighWater=Math.Max(nearVisitCapabilityHighWater,nearVisitCapabilities.Count);
#endif
            }
            if((fact.Wait&BlueprintBuildWait.Material)!=0)
            {
                ObserveNearVisitSupply(fact.Supply,fact.SupplyRevision);
                ObserveNearVisitSupply(fact.RestoreTilePaint,fact.SupplyRevision);ObserveNearVisitSupply(fact.RestoreWallPaint,fact.SupplyRevision);
                ObserveNearVisitSupply(fact.RestoreFullbright,fact.SupplyRevision);ObserveNearVisitSupply(fact.RestoreInvisible,fact.SupplyRevision);
                if((fact.Wait&BlueprintBuildWait.Capability)==0){ObserveNearVisitSupply(fact.Type,fact.SupplyRevision);ObserveNearVisitSupply(fact.Seed.Placement?.UseItemType??0,fact.SupplyRevision);ObserveNearVisitSupply(fact.ContinuationType,fact.SupplyRevision);}
            }
        }
        private void ObserveNearVisitSupply(int type,long serial)
        {
            if(type<=0)return;long old;nearVisitSupplies[type]=nearVisitSupplies.TryGetValue(type,out old)?Math.Min(old,serial):serial;
#if DEBUG
            nearVisitSupplyHighWater=Math.Max(nearVisitSupplyHighWater,nearVisitSupplies.Count);
#endif
        }
        private static BlueprintProjectionBounds NearSeedSourceBounds(RepeatedBuildSeed seed)
        {
            var display=seed.Body==null?new BlueprintProjectionBounds(seed.Cell.X,seed.Cell.Y,1,1):
                new BlueprintProjectionBounds(seed.Body.WorldX-seed.Owner.OriginX,seed.Body.WorldY-seed.Owner.OriginY,seed.Body.Width,seed.Body.Height);
            return seed.Owner.View.SourceBounds(display);
        }
        private bool NearSeedInPreferredDomain(RepeatedBuildSeed seed,Player p)
        {return BuildBoundsOverlap(BuildPreferredFocusBounds(seed.Owner,p),NearSeedSourceBounds(seed));}
        private void ConsiderNearBuildHead(NearBuildFact fact,Player p)
        {
#if DEBUG
            NearBuildHeadChecks++;
#endif
            if(!fact.Ready || !NearFactCurrent(fact,p))return;
            bool current=NearFactContinues(fact);
            if(buildContinuation==null || !current)
            {
                if(nearForeignHead==null || NearFactContinues(nearForeignHead) || NearHeadDistance(fact,p)<NearHeadDistance(nearForeignHead,p))nearForeignHead=fact;
                RememberNearHint(nearOtherHints,fact,p);
            }
            if(current)
            {
                if(nearPreferredHead==null || NearHeadDistance(fact,p)<NearHeadDistance(nearPreferredHead,p))nearPreferredHead=fact;
                RememberNearHint(nearBuildHints,fact,p);
            }
        }
        private void RememberNearHint(NearBuildFact[] hints,NearBuildFact fact,Player p)
        {
            int replacement=-1;double farthest=NearHeadDistance(fact,p);
            for(int i=0;i<hints.Length;i++)
            {
#if DEBUG
                NearBuildHeadChecks++;
#endif
                var old=hints[i];if(ReferenceEquals(old,fact))return;
                if(old==null || old.Visit?.List!=nearBuildVisits || !old.Ready || ReferenceEquals(hints,nearOtherHints) && NearFactContinues(old))
                {replacement=i;farthest=double.MaxValue;continue;}
                double distance=NearHeadDistance(old,p);if(distance>farthest){replacement=i;farthest=distance;}
            }
            if(replacement>=0)hints[replacement]=fact;
        }
        private void ServiceNearOtherHint(Player p,ref int allowance)
        {
            if(buildContinuation==null)return;
            if(nearForeignHead!=null && NearFactContinues(nearForeignHead))nearForeignHead=null;
            if(nearForeignHead!=null && nearForeignHead.Ready && NearFactCurrent(nearForeignHead,p))return;
            nearForeignHead=null;int tried=0;
            // A cannot overwrite B's semantic preselection. Rebind one known
            // other-group hint with the SAME remaining balance; no Item/token
            // or extra preparer is retained by this small hint set.
            for(int n=0;n<nearOtherHints.Length && allowance>3;n++)
            {
                int index=-1;double nearest=double.MaxValue;
                for(int i=0;i<nearOtherHints.Length;i++)
                {
#if DEBUG
                    NearBuildHeadChecks++;
#endif
                    var fact=nearOtherHints[i];if((tried&(1<<i))!=0 || fact==null || fact.Visit?.List!=nearBuildVisits || NearFactContinues(fact))continue;
                    double distance=NearHeadDistance(fact,p);if(distance<nearest){nearest=distance;index=i;}
                }
                if(index<0)break;tried|=1<<index;var hint=nearOtherHints[index];
                if(!NearFactCurrent(hint,p) || hint.Pending)ClassifyNearFact(hint,p,ref allowance);
                if(hint.Ready && NearFactCurrent(hint,p) && !NearFactContinues(hint)){nearForeignHead=hint;return;}
                if(!hint.Pending)nearOtherHints[index]=null;
            }
        }
        private static double NearHeadDistance(NearBuildFact fact,Player p)
        {double dx=fact.Seed.Owner.OriginX+fact.Seed.Cell.X-p.Center.X/16,dy=fact.Seed.Owner.OriginY+fact.Seed.Cell.Y-p.Center.Y/16;return dx*dx+dy*dy;}
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
            state.Complete=false;state.Initialized=false;state.Cursor=0;nearPlanDirty=nearVisitNeeded=true;
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
            fact.Ready=fact.Pending=false;fact.Retry=retry;nearPlanDirty=nearVisitNeeded=true;
        }
        private void RejectNearBuildCandidate(BlueprintBuildTarget target)
        {
            if(nearSelected==null || !ReferenceEquals(nearSelected.Seed.Range,target.Range) || nearSelected.Seed.Ordinal!=target.Ordinal)return;
            var fact=nearSelected;
            nearPlanDirty=nearVisitNeeded=true;fact.Ready=false;fact.Pending=target.Readiness==BlueprintBuildReadiness.Deferred;
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
                foreach(var fact in NearPreferredFacts(state,required))
                {
                    if(!NearFactCurrent(fact,p) || fact.Pending || fact.PlanGroup!=(buildContinuation?.Group??0))return false;
                    if(fact.Ready && NearFactContinues(fact))return false;
                }
            }
            return true;
        }
        // Ranking/classification is paid once in Update. Pick only reads the
        // cached heads and per-range proof stamps; it never ranks all members.
        private void RankNearBuildHeads(Player p)
        {
            bool sameGroup=ReferenceEquals(nearProofGroup,buildContinuation);
            if(buildContinuation==null){nearProofGroup=null;nearAbsenceReady=false;return;}
            if(!sameGroup)nearAbsenceReady=false;
            if(nearPreferredHead!=null){nearAbsenceReady=false;nearProofGroup=buildContinuation;return;}
            // Completed negatives keep their dependency lifetime after the
            // visit cycle sleeps. A failed final fence wakes the same ring;
            // it must not require B to acquire in the exact signing Update.
            if(sameGroup && nearAbsenceReady && NearBuildAbsenceCurrent(p))return;
            if(!nearPlanDirty && !nearVisitCompleted && buildContinuation==null)return;
            nearPlanDirty=false;
            nearProofDomainBoost=NearOperationBoost(buildContinuation.Operation,buildContinuation.Source.Type);
            nearProofWorldDomain=BuildPreferredWorldBounds(p);
            nearProofDomainGrand=WiringOperation(buildContinuation.Operation) && buildSupply.ContainsKey(3611);
            nearProofGroup=buildContinuation;nearProofNoSupply=NoNearSupplyNow(p);nearAbsenceReady=ComputeNearBuildAbsence(p);nearProofPlayers.Clear();nearProofSupports.Clear();nearProofMaterialDependencies.Clear();nearProofMaterialPresence.Clear();nearProofCapabilities.Clear();nearProofContainer=nearProofRestoration=-1;nearProofDependencies=nearProofRetry=0;
            if(nearAbsenceReady)foreach(var range in nearBuildDiscovery.Ranges)
            {
                if(!NearRangeRelevantToPreference(range,p))continue;
                var state=nearBuildRanges[range];bool relevant=false;
                var required=IntersectBuildBounds(BuildPreferredFocusBounds(range.Owner,p),range.Source.Bounds);
                foreach(var fact in NearPreferredFacts(state,required))
                {
                    relevant=true;
                    if((fact.Wait&BlueprintBuildWait.Player)!=0)nearProofPlayers.Add(fact);
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
            // The last Update's player snapshot is not this certificate's
            // signing domain. A later Update can already have advanced that
            // snapshot while old absence remains cached. Only the signed
            // intersection is reusable; newly entered reach/visible GD tiles
            // must return to the existing bounded planner, never scan in Pick.
            if(!BuildBoundsContain(nearProofWorldDomain,BuildPreferredWorldBounds(p)))
            {nearVisitNeeded=true;return false;}
            // Native region equality alone does not cover leaving a target's
            // collision rectangle inside that region. Only actual A-player
            // negatives own this fence; completed world facts do not.
            foreach(var fact in nearProofPlayers)
                if(!NearFactPlayerCurrent(fact,p)){nearVisitNeeded=true;return false;}
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
            if(live)
            {
                // A new matching long tool can expand A beyond the old negative
                // domain, whose out-of-range facts deliberately are not proof
                // dependencies. Reuse ONE qualified live snapshot for both
                // domain growth and the existing capability negative fence.
                BeginNearCapabilityLiveValidation(p,true);
                if(NearLiveOperationBoost(buildContinuation.Operation,buildContinuation.Source.Type)>nearProofDomainBoost ||
                    WiringOperation(buildContinuation.Operation) && !nearProofDomainGrand && nearCapabilityLive.ContainsKey(3611) && WiringToolMatches(buildContinuation.Operation,3611))
                {nearVisitNeeded=true;return false;}
            }
            foreach(var capability in nearProofCapabilities)
                if(CurrentNearCapabilitySerial(capability.Key)>capability.Value || live && LiveNearCapabilityRecovery(capability.Key,capability.Value))return false;
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
            nearPlanDirty=nearVisitNeeded=true;
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

