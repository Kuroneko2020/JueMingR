using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
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
                }
                else buildNearWorld=default(BlueprintProjectionBounds);
            }
            return new BlueprintProjectionBounds(buildNearWorld.Left-owner.OriginX,buildNearWorld.Top-owner.OriginY,buildNearWorld.Width,buildNearWorld.Height);
        }
        private static bool BuildBoundsOverlap(BlueprintProjectionBounds a,BlueprintProjectionBounds b)
        {return a.Left<b.Right && b.Left<a.Right && a.Top<b.Bottom && b.Top<a.Bottom;}
        private LinkedListNode<BuildSourcePart> BuildSourceVictim(BlueprintProjectionBounds incoming,BlueprintProjectionPlacement owner)
        {
            bool nearby=BuildBoundsOverlap(incoming,BuildNearBounds(owner));
            LinkedListNode<BuildSourcePart> finished=null;
            for(var node=buildSources.First;node!=null;node=node.Next)
            {
                if(!BuildBoundsOverlap(node.Value.Part.Window.Bounds,BuildNearBounds(node.Value.Owner)))return node;
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
        private bool TryNearbyBuildTarget(BuildSourcePart source,out BlueprintBuildTarget target)
        {
            target=null;var range=source.Range;var value=range.Materials;
            if(value==null || !ReferenceEquals(FindBuildSource(range),source.Part))return false;
            TryWakeBuildEligibility(range);
            if(value.Candidate!=null && IsBuildTargetCurrent(value.Candidate)){target=value.Candidate;return true;}
#if JMR_BLUEPRINT_DIAGNOSTIC
            int resetReasons=0;bool resetNeeded;
            if(BlueprintBuildTrace.Active)
            {
                resetReasons=(source.NearVersion!=range.Version?1:0)|(source.NearWake!=value.BuildWakeRevision?2:0)|
                    (source.NearPlayer!=MaterialBuildEligibilityRevision?4:0)|(source.NearSupply!=buildSupplyRevision?8:0);
                resetNeeded=resetReasons!=0;
            }
            else resetNeeded=source.NearVersion!=range.Version || source.NearWake!=value.BuildWakeRevision || source.NearPlayer!=MaterialBuildEligibilityRevision || source.NearSupply!=buildSupplyRevision;
            if(resetNeeded)
#else
            if(source.NearVersion!=range.Version || source.NearWake!=value.BuildWakeRevision || source.NearPlayer!=MaterialBuildEligibilityRevision || source.NearSupply!=buildSupplyRevision)
#endif
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                int resetBefore=0;var priorBounds=default(BlueprintProjectionBounds);
                if(BlueprintBuildTrace.Active){resetBefore=source.NearCursor;priorBounds=source.NearBounds;}
#endif
                source.NearVersion=range.Version;source.NearWake=value.BuildWakeRevision;
                source.NearPlayer=MaterialBuildEligibilityRevision;source.NearSupply=buildSupplyRevision;
                source.NearBounds=BuildNearBounds(source.Owner);
                source.NearCursor=source.Part.LogicalObjectId>0?0:BuildCellLowerBound(source.Part.Window.Cells,source.NearBounds.Left,source.NearBounds.Top)*2;
#if JMR_BLUEPRINT_DIAGNOSTIC
                if(BlueprintBuildTrace.Active)
                {
                    if(!priorBounds.Equals(source.NearBounds))resetReasons|=16;
                    source.DiagnosticResetBefore=resetBefore;source.DiagnosticResetAfter=source.NearCursor;source.DiagnosticResetReasons=resetReasons;
                    BlueprintBuildTrace.Count(BuildTraceReason.NearReset);
                    if((resetReasons&1)!=0)BlueprintBuildTrace.Count(BuildTraceReason.ResetVersion);
                    if((resetReasons&2)!=0)BlueprintBuildTrace.Count(BuildTraceReason.ResetWake);
                    if((resetReasons&4)!=0)BlueprintBuildTrace.Count(BuildTraceReason.ResetPlayer);
                    if((resetReasons&8)!=0)BlueprintBuildTrace.Count(BuildTraceReason.ResetSupply);
                    if((resetReasons&16)!=0)BlueprintBuildTrace.Count(BuildTraceReason.ResetBounds);
                }
#endif
            }
            if(source.Frontier)
            {
                if(ContinueLocalBuildSource(source,source.FrontierBounds,ref source.FrontierCursor,out target))return true;
                if(materialBuildSourceBudget<=0)return false;
                source.Frontier=false;
            }
            return ContinueLocalBuildSource(source,source.NearBounds,ref source.NearCursor,out target);
        }
        private bool ContinueLocalBuildSource(BuildSourcePart source,BlueprintProjectionBounds bounds,ref int cursor,out BlueprintBuildTarget target)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            bool diagnosticFrontier=false;
            if(BlueprintBuildTrace.Active){diagnosticFrontier=source.Frontier;BlueprintBuildTrace.Hit(diagnosticFrontier?BuildTraceStage.Frontier:BuildTraceStage.Near);}
#endif
            target=null;var part=source.Part;var range=source.Range;var cells=part.Window.Cells;
            long support;
            if(!range.Group.Ready || !ProjectionMatchReadCurrent(range,range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceNotCurrent);
#endif
                return false;
            }
            while(materialBuildSourceBudget>0)
            {
                int index=part.LogicalObjectId>0?0:cursor/2;
                if(index>=cells.Count || part.LogicalObjectId>0 && cursor>0)
                {
#if JMR_BLUEPRINT_DIAGNOSTIC
                    BlueprintBuildTrace.Count(BuildTraceReason.SourceEnd);
#endif
                    return false;
                }
                var cell=cells[index];
                if(part.LogicalObjectId==0)
                {
                    if(cell.Y>=bounds.Bottom)
                    {
#if JMR_BLUEPRINT_DIAGNOSTIC
                        BlueprintBuildTrace.Count(BuildTraceReason.SourceBoundsEnd);
#endif
                        return false;
                    }
                    if(cell.X<bounds.Left || cell.X>=bounds.Right || cell.Y<bounds.Top)
                    {cursor=BuildCellLowerBound(cells,bounds.Left,cell.X>=bounds.Right?cell.Y+1:Math.Max(cell.Y,bounds.Top))*2;continue;}
                }
                int ordinal=part.LogicalObjectId>0?0:(part.Offset+index)*2+(cursor&1);
                cursor=part.LogicalObjectId>0?1:cursor+((cursor&1)==0 && (cell.Layers&CaptureLayer.Wall)==0?2:1);
                materialBuildSourceBudget--;
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
        private bool TryCreateLocalBuildTarget(BuildSourcePart source,int index,int ordinal,long support,out BlueprintBuildTarget target)
        {
            target=null;var range=source.Range;var part=source.Part;var catalog=MaterialCatalog;
            if(!ProjectionQualificationCurrent(source.Owner))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceNotCurrent);
#endif
                return false;
            }
            CaptureObject? obj=part.LogicalObjectId>0 && part.Window.Objects.Count==1?part.Window.Objects[0]:(CaptureObject?)null;
            var cell=source.Owner.Mask.Apply(part.Window.Cells[index]);var selection=SelectProjectionTarget(source.Owner,cell,obj);cell=selection.Apply(cell);
            BlueprintWorldObject body=null;BlueprintPlacementDescriptor descriptor=null;
            if(part.LogicalObjectId>0)
            {
                if((cell.Layers&CaptureLayer.Tile)!=0 && obj.HasValue && projectionMatchSource.TryDescribeTarget(obj.Value,cell,source.Owner.OriginX,source.Owner.OriginY,out body))
                    descriptor=catalog.Tile(cell,obj.Value);
            }
            else if((ordinal&1)==0 && (cell.Layers&CaptureLayer.Tile)!=0 && cell.ObjectId==0)descriptor=catalog.Tile(cell);
            else if((ordinal&1)!=0 && (cell.Layers&CaptureLayer.Wall)!=0)descriptor=catalog.Wall(cell.WallType);
            ushort targetBit=part.LogicalObjectId>0 || (ordinal&1)==0?(ushort)1:(ushort)2;
            if((selection.UnknownBits&targetBit)!=0 || !BasicPlacement(descriptor) || descriptor.RequiresSubstrate)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count((selection.UnknownBits&targetBit)!=0?BuildTraceReason.Unconfirmed:BuildTraceReason.Unsupported);
#endif
                return false;
            }
            bool supplied=buildSupply.ContainsKey(descriptor.UseItemType);
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticSupply(descriptor.UseItemType,supplied);
#endif
            if(!supplied){range.Materials.BuildWaits|=BlueprintBuildWait.Material;return false;}
            var next=CreateMaterialBuildTarget(source.Owner,range,cell,descriptor,obj,body,ordinal,support,catalog.Revision);
            if(!buildNearWorld.Contains(next.WorldX,next.WorldY)){range.Materials.BuildWaits|=BlueprintBuildWait.Player;return false;}
            if(!BuildEmpty(next.Bounds.Left,next.Bounds.Top,descriptor.Kind==BlueprintPlacementKind.Wall))return false;
            next.LocalProof=next.LocalPriority=true;target=next;return true;
        }
        private void RememberBuildFrontier(BlueprintBuildTarget target)
        {
            for(var node=buildSources.First;node!=null;node=node.Next)
            {
                var source=node.Value;if(!ReferenceEquals(source.Range,target.Range))continue;
                var bounds=target.Bounds.Expand(1);
                source.FrontierBounds=new BlueprintProjectionBounds(bounds.Left-source.Owner.OriginX,bounds.Top-source.Owner.OriginY,bounds.Width,bounds.Height);
                source.FrontierCursor=source.Part.LogicalObjectId>0?0:BuildCellLowerBound(source.Part.Window.Cells,source.FrontierBounds.Left,source.FrontierBounds.Top)*2;
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
