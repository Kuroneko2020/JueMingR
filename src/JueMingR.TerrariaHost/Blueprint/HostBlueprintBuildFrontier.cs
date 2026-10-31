using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private ulong buildNearTick;
        private bool buildNearTickSet;
        private BlueprintProjectionBounds buildNearWorld;

        private BlueprintProjectionBounds BuildNearBounds(BlueprintProjectionPlacement owner)
        {
            if(!buildNearTickSet || buildNearTick!=materialTick)
            {
                buildNearTickSet=true;buildNearTick=materialTick;
                var p=buildTools?.Player;
                if(p!=null)
                {
                    // Use vanilla's own reach rectangle and the largest
                    // currently supplied tool boost from this tick's one
                    // capability observation only as a broad phase. Final
                    // admission checks the exact item and current native reach.
                    int left,top,right,bottom;
                    TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out right,out bottom,buildEnvelopeBoost+p.blockRange);
                    buildNearWorld=new BlueprintProjectionBounds(left,top,Math.Max(0,right-left+1),Math.Max(0,bottom-top+1));
                    if(buildSupply.ContainsKey(3611) && Main.screenWidth>0 && Main.screenHeight>0)
                    {
                        // Grand Design is a native screen gesture rather than
                        // a wrench's Simple reach. This is only a broad phase;
                        // its actual fixed point must remain in the viewport.
                        int screenLeft=(int)Math.Floor(Main.screenPosition.X/16f),screenTop=(int)Math.Floor(Main.screenPosition.Y/16f);
                        int screenRight=(int)Math.Ceiling((Main.screenPosition.X+Main.screenWidth)/16f),screenBottom=(int)Math.Ceiling((Main.screenPosition.Y+Main.screenHeight)/16f);
                        left=Math.Min(left,screenLeft);top=Math.Min(top,screenTop);right=Math.Max(right,screenRight);bottom=Math.Max(bottom,screenBottom);
                        buildNearWorld=new BlueprintProjectionBounds(left,top,Math.Max(0,right-left+1),Math.Max(0,bottom-top+1));
                    }
                }
                else buildNearWorld=default(BlueprintProjectionBounds);
            }
            return new BlueprintProjectionBounds(buildNearWorld.Left-owner.OriginX,buildNearWorld.Top-owner.OriginY,buildNearWorld.Width,buildNearWorld.Height);
        }
        private static bool BuildBoundsOverlap(BlueprintProjectionBounds a,BlueprintProjectionBounds b)
        {return a.Left<b.Right && b.Left<a.Right && a.Top<b.Bottom && b.Top<a.Bottom;}
        private LinkedListNode<BuildSourcePart> BuildSourceVictim(BlueprintProjectionBounds incoming,BlueprintProjectionPlacement owner)
        {
            // Both incoming and retained window bounds belong to their raw
            // source view, while BuildNearBounds is display-local reach.
            bool nearby=BuildBoundsOverlap(incoming,owner.View.SourceBounds(BuildNearBounds(owner)));
            LinkedListNode<BuildSourcePart> finished=null;
            for(var node=buildSources.First;node!=null;node=node.Next)
            {
                var retained=node.Value;
                if(!BuildBoundsOverlap(retained.Part.Window.Bounds,retained.Owner.View.SourceBounds(BuildNearBounds(retained.Owner))))return node;
                if(finished==null && node.Value.Range.Materials?.BuildExhausted!=false)finished=node;
            }
            return finished ?? (nearby?buildSources.First:null);
        }
        private static int BuildCellLowerBound(IReadOnlyList<CaptureCell> cells,int x,int y)
        {
            int low=0,high=cells.Count;
            while(low<high)
            {
                int middle=low+(high-low)/2;var cell=cells[middle];
                if(cell.Y<y || cell.Y==y && cell.X<x)low=middle+1;else high=middle;
            }
            return low;
        }
        private bool TryNearbyBuildTarget(BuildSourcePart source,int stepLimit,out BlueprintBuildTarget target)
        {
            target=null;var range=source.Range;var value=range.Materials;
            if(value==null || !ReferenceEquals(FindBuildSource(range),source.Part))return false;
            TryWakeBuildEligibility(range);
            if(value.Candidate!=null && IsBuildTargetCurrent(value.Candidate)){target=value.Candidate;return true;}
            if(source.NearVersion!=range.Version || source.NearWake!=value.BuildWakeRevision || source.NearPlayer!=MaterialBuildEligibilityRevision || source.NearSupply!=buildSupplyRevision)
            {
                source.NearVersion=range.Version;source.NearWake=value.BuildWakeRevision;
                source.NearPlayer=MaterialBuildEligibilityRevision;source.NearSupply=buildSupplyRevision;
                source.NearBounds=source.Owner.View.SourceBounds(BuildNearBounds(source.Owner));
                // These revisions invalidate candidate proofs, not the stable
                // source ordinal. Merge a revisit and refresh the broad phase
                // now; finish the current finite pass before wrapping once.
                source.NearRevisit=true;
            }
            if(!source.NearInitialized || source.NearComplete && source.NearRevisit)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                source.DiagnosticResetBefore=source.NearCursor;
#endif
                source.NearInitialized=true;source.NearComplete=source.NearRevisit=false;
                source.NearCursor=BuildMemberOrdinal(BuildCellLowerBound(ProjectionSourceCells(source.Part.Window),source.NearBounds.Left,source.NearBounds.Top));
#if JMR_BLUEPRINT_DIAGNOSTIC
                source.DiagnosticResetAfter=source.NearCursor;
                // A merged revisit no longer has one immediate reset cause.
                source.DiagnosticResetReasons=0;
                BlueprintBuildTrace.Count(BuildTraceReason.NearReset);
#endif
            }
            bool complete;
            if(source.Frontier)
            {
                if(ContinueLocalBuildSource(source,source.FrontierBounds,ref source.FrontierCursor,ref stepLimit,out target,out complete))return true;
                // Slice exhaustion only pauses this pass. It does not consume
                // a pending revisit or discard unfinished frontier work.
                if(complete)source.Frontier=false;
                if(materialBuildSourceBudget<=0 || stepLimit<=0 || !complete)return false;
            }
            if(source.NearComplete)return false;
            if(ContinueLocalBuildSource(source,source.NearBounds,ref source.NearCursor,ref stepLimit,out target,out complete))return true;
            source.NearComplete=complete;return false;
        }
        private bool ContinueLocalBuildSource(BuildSourcePart source,BlueprintProjectionBounds bounds,ref int cursor,ref int stepLimit,out BlueprintBuildTarget target,out bool complete)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            bool diagnosticFrontier=false;
            if(BlueprintBuildTrace.Active){diagnosticFrontier=source.Frontier;BlueprintBuildTrace.Hit(diagnosticFrontier?BuildTraceStage.Frontier:BuildTraceStage.Near);}
#endif
            target=null;complete=false;var part=source.Part;var range=source.Range;var cells=ProjectionSourceCells(part.Window);
            long support;
            if(!range.Group.Ready || !ProjectionMatchReadCurrent(range,range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceNotCurrent);
#endif
                return false;
            }
            while(materialBuildSourceBudget>0 && stepLimit>0)
            {
                int index=BuildMemberIndex(cursor);
                if(index>=cells.Count)
                {
#if JMR_BLUEPRINT_DIAGNOSTIC
                    BlueprintBuildTrace.Count(BuildTraceReason.SourceEnd);
#endif
                    complete=true;return false;
                }
                var cell=cells[index];
                if(part.LogicalObjectId==0)
                {
                    if(cell.Y>=bounds.Bottom)
                    {
#if JMR_BLUEPRINT_DIAGNOSTIC
                        BlueprintBuildTrace.Count(BuildTraceReason.SourceBoundsEnd);
#endif
                        complete=true;return false;
                    }
                    if(cell.X<bounds.Left || cell.X>=bounds.Right || cell.Y<bounds.Top)
                    {cursor=BuildMemberOrdinal(BuildCellLowerBound(cells,bounds.Left,cell.X>=bounds.Right?cell.Y+1:Math.Max(cell.Y,bounds.Top)));continue;}
                }
                int ordinal=BuildMemberOrdinal(part.Offset+index)+(int)BuildLane(cursor);
                cursor=NextBuildOrdinal(cursor,cell);
                materialBuildSourceBudget--;stepLimit--;
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(diagnosticFrontier?BuildTraceReason.FrontierStep:BuildTraceReason.NearStep);
#endif
#if DEBUG
                ProjectionMatchSourceSteps++;
#endif
                if(TryCreateLocalBuildTarget(source,index,ordinal,support,out target))
                {range.Materials.Candidate=target;return true;}
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Count(BuildTraceReason.SourceBudgetEmpty);
#endif
            return false;
        }
        private bool TryCreateLocalBuildTarget(BuildSourcePart source,int index,int ordinal,long support,out BlueprintBuildTarget target,int preferredItemType=0)
        {
            bool found=TryCreateConstructionTarget(source.Owner,source.Range,source.Part,index,ordinal,support,out target,preferredItemType);
            if(found)target.LocalPriority=true;return found;
        }
        private void RememberBuildFrontier(BlueprintBuildTarget target)
        {
            for(var node=buildSources.First;node!=null;node=node.Next)
            {
                var source=node.Value;if(!ReferenceEquals(source.Range,target.Range))continue;
                var bounds=target.Bounds.Expand(1);
                source.FrontierBounds=source.Owner.View.SourceBounds(new BlueprintProjectionBounds(bounds.Left-source.Owner.OriginX,bounds.Top-source.Owner.OriginY,bounds.Width,bounds.Height));
                source.FrontierCursor=BuildMemberOrdinal(BuildCellLowerBound(ProjectionSourceCells(source.Part.Window),source.FrontierBounds.Left,source.FrontierBounds.Top));
                source.Frontier=true;return;
            }
        }
        private bool BuildDiscoveryPending()
        {
            var p=buildTools?.Player;
            // Discovery continues without a lease. A brief empty ready slot
            // can preserve only the current vanilla use's natural tail; at the
            // real selection boundary it yields, never leasing an idle inventory.
            return p!=null && (p.itemAnimation>0 || p.itemTime>0 || p.reuseDelay>0 || p.channel);
        }
    }
}
