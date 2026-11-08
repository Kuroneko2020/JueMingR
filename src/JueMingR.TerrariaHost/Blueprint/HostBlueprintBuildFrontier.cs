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
                NearBuildRange plan;
                if(finished==null && nearBuildRanges.TryGetValue(node.Value.Range,out plan) && plan.Complete)finished=node;
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
        private void RememberBuildFrontier(BlueprintBuildTarget target)
        {
            nearPlanDirty=true;

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
