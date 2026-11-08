using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // Projection target bits are not CaptureLayer: tile=1, wall=2,
        // liquid=4, actuator=8, and the four wire colors occupy bits4..7.
        // Grand Design's screen extension must never route liquid as wiring.
        private const ushort NearBuildWireTargetBits=0xf0;
        private const ushort NearBuildWiringTargetBits=0xf8;
        // Immutable geometry is owned and charged by the worker-qualified
        // source. Host retains only current per-instance query/coverage state.
        private sealed class BuildFocusNearQuery
        {
            internal BlueprintProjectionMatchState Match;
            internal IReadOnlyList<BlueprintProjectionSourceRange> Sources;
            internal BlueprintProjectionGeometryDirectory Directory;
            internal BlueprintProjectionBounds Covered, Query;
            internal bool Complete, Started;
            internal readonly Stack<int> Stack=new Stack<int>();
        }
        private sealed class BuildFocusDiscovery
        {
            internal readonly Dictionary<BlueprintProjectionPlacement,BuildFocusNearQuery> Owners=new Dictionary<BlueprintProjectionPlacement,BuildFocusNearQuery>();
            internal readonly List<BlueprintProjectionMatchRange> Ranges=new List<BlueprintProjectionMatchRange>();
            internal readonly HashSet<BlueprintProjectionMatchRange> Members=new HashSet<BlueprintProjectionMatchRange>();
            internal readonly List<BlueprintProjectionPlacement> Retired=new List<BlueprintProjectionPlacement>();
            internal readonly List<BlueprintProjectionPlacement> NearOwners=new List<BlueprintProjectionPlacement>();
            internal readonly HashSet<BlueprintProjectionPlacement> NearOwnerSet=new HashSet<BlueprintProjectionPlacement>();
            internal int OwnerCursor;
            internal ulong Tick;
            internal bool TickSet;
            internal BlueprintProjectionBounds World;
            internal float PlayerX,PlayerY,ScreenX,ScreenY;
            internal int PlayerWidth,PlayerHeight,BlockRange,ReachX,ReachY,ScreenWidth,ScreenHeight;
        }
#if DEBUG
        internal long BuildFocusDirectorySteps { get; private set; }
        internal long BuildFocusDirectoryRoutes { get; private set; }
        // Keep these Debug cost observations explicit: construction moved to
        // the qualification worker; zero here does not mean zero total work.
        internal long BuildFocusDirectoryConstructionSteps { get { return 0; } }
        internal long BuildFocusDirectoryBuilds { get { return 0; } }
        internal long BuildFocusDirectoryTemporaryBytes { get { return 0; } }
        internal long BuildFocusDirectorySharedBytes { get { return 0; } }
        internal long BuildFocusDirectoryBytes
        {
            get
            {
                var discovery=nearBuildDiscovery;if(discovery==null)return 0;
                // Collection estimates include reusable result storage even
                // after Clear retained its buckets for the next near query.
                long bytes=224+discovery.Ranges.Capacity*8L+discovery.Members.Count*32L+discovery.Ranges.Count*192L;
                bytes+=discovery.NearOwners.Capacity*8L+discovery.NearOwnerSet.Count*32L+discovery.Retired.Capacity*8L;
                foreach(var query in discovery.Owners.Values)bytes+=128+query.Stack.Count*4L;
                return bytes;
            }
        }
#endif
        private static BlueprintProjectionBounds BuildBoundsUnion(BlueprintProjectionBounds a,BlueprintProjectionBounds b)
        {int left=Math.Min(a.Left,b.Left),top=Math.Min(a.Top,b.Top);return new BlueprintProjectionBounds(left,top,Math.Max(a.Right,b.Right)-left,Math.Max(a.Bottom,b.Bottom)-top);}
        private static bool BuildBoundsContain(BlueprintProjectionBounds outer,BlueprintProjectionBounds inner)
        {return outer.Left<=inner.Left && outer.Top<=inner.Top && outer.Right>=inner.Right && outer.Bottom>=inner.Bottom;}
        private BlueprintProjectionBounds BuildFocusWorldBounds(Player p)
        {
            var discovery=nearBuildDiscovery;
            if(discovery.TickSet && discovery.Tick==materialTick && discovery.PlayerX==p.position.X && discovery.PlayerY==p.position.Y &&
                discovery.PlayerWidth==p.width && discovery.PlayerHeight==p.height && discovery.BlockRange==p.blockRange &&
                discovery.ReachX==Player.tileRangeX && discovery.ReachY==Player.tileRangeY && discovery.ScreenX==Main.screenPosition.X &&
                discovery.ScreenY==Main.screenPosition.Y && discovery.ScreenWidth==Main.screenWidth && discovery.ScreenHeight==Main.screenHeight)return discovery.World;
            discovery.TickSet=true;discovery.Tick=materialTick;
            discovery.PlayerX=p.position.X;discovery.PlayerY=p.position.Y;discovery.PlayerWidth=p.width;discovery.PlayerHeight=p.height;
            discovery.BlockRange=p.blockRange;discovery.ReachX=Player.tileRangeX;discovery.ReachY=Player.tileRangeY;
            discovery.ScreenX=Main.screenPosition.X;discovery.ScreenY=Main.screenPosition.Y;discovery.ScreenWidth=Main.screenWidth;discovery.ScreenHeight=Main.screenHeight;
            var world=BuildNearOrdinaryWorldBounds(p);
            if(buildSupply.ContainsKey(3611) && Main.screenWidth>0 && Main.screenHeight>0)
                world=BuildBoundsUnion(world,new BlueprintProjectionBounds((int)Math.Floor(Main.screenPosition.X/16f),(int)Math.Floor(Main.screenPosition.Y/16f),
                    (int)Math.Ceiling((Main.screenPosition.X+Main.screenWidth)/16f)-(int)Math.Floor(Main.screenPosition.X/16f),
                    (int)Math.Ceiling((Main.screenPosition.Y+Main.screenHeight)/16f)-(int)Math.Floor(Main.screenPosition.Y/16f)));
            discovery.World=world;return world;
        }
        // Conservative shared discovery only; each target's native tool
        // reach is checked independently. Screen extension routes wire layers
        // exclusively, never ordinary table/chair source demands.
        private BlueprintProjectionBounds BuildNearOrdinaryWorldBounds(Player p)
        {
            int left,top,right,bottom;
            Terraria.DataStructures.TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out right,out bottom,buildEnvelopeBoost+Math.Max(0,p.blockRange));
            return new BlueprintProjectionBounds(left,top,Math.Max(0,right-left+1),Math.Max(0,bottom-top+1));
        }
        private BlueprintProjectionBounds BuildNearOrdinaryBounds(BlueprintProjectionPlacement owner,Player p)
        {var world=BuildNearOrdinaryWorldBounds(p);return owner.View.SourceBounds(new BlueprintProjectionBounds(world.Left-owner.OriginX,world.Top-owner.OriginY,world.Width,world.Height));}
        private BlueprintProjectionBounds BuildFocusBounds(BlueprintProjectionPlacement owner,Player p)
        {var world=BuildFocusWorldBounds(p);return owner.View.SourceBounds(new BlueprintProjectionBounds(world.Left-owner.OriginX,world.Top-owner.OriginY,world.Width,world.Height));}
        private BlueprintProjectionBounds BuildPreferredFocusBounds(BlueprintProjectionPlacement owner,Player p)
        {return WiringOperation(buildContinuation?.Operation)?BuildFocusBounds(owner,p):BuildNearOrdinaryBounds(owner,p);}
        private bool AdvanceBuildFocusNearQuery(BuildFocusNearQuery query,Action<int> emit)
        {
            if(materialBuildSearchBudget<=0)return false;
            if(!query.Started)return false;
            materialBuildSearchBudget--;
#if DEBUG
            BuildFocusDirectorySteps++;
#endif
            if(query.Stack.Count>0)
            {
                var node=query.Directory.GetNode(query.Stack.Pop());
                if(BuildBoundsOverlap(node.Bounds,query.Query))
                {
                    if(node.Left>=0){query.Stack.Push(node.Right);query.Stack.Push(node.Left);}
                    else emit(node.Group);
                }
            }
            if(query.Stack.Count==0){query.Covered=query.Query;query.Complete=true;query.Started=false;}
            return true;
        }
        private bool RefreshBuildFocusNear(Player p)
        {
            var discovery=nearBuildDiscovery;
            discovery.NearOwners.Clear();discovery.NearOwnerSet.Clear();discovery.Retired.Clear();
            var world=BuildFocusWorldBounds(p);
            foreach(var owner in projectionScene)
            {
                var summary=owner.Summary;
                if(summary!=null && !BuildBoundsOverlap(world,new BlueprintProjectionBounds(owner.OriginX-1,owner.OriginY-1,summary.Width+2,summary.Height+2)))continue;
                // A current completed qualification failure keeps its display
                // and error, but owns no pending construction discovery. Only
                // a genuinely unfinished current receipt can defer a group.
                if(ProjectionQualificationFailed(owner))continue;
                discovery.NearOwners.Add(owner);discovery.NearOwnerSet.Add(owner);
            }
            foreach(var owner in discovery.Owners.Keys)
                if(!discovery.NearOwnerSet.Contains(owner) || !ProjectionQualificationCurrent(owner) || !ReferenceEquals(owner.Match,discovery.Owners[owner].Match))
                    discovery.Retired.Add(owner);
            foreach(var owner in discovery.Retired)discovery.Owners.Remove(owner);
            // Prune only the previous near domain. Moving never resets a
            // source ordinal or walks every member of every blueprint.
            for(int i=discovery.Ranges.Count-1;i>=0;i--)
            {
                var range=discovery.Ranges[i];var owner=range.Owner;
                BuildFocusNearQuery query;bool known=discovery.Owners.TryGetValue(owner,out query);
                if(!projectionScene.Contains(owner) || owner.Match==null || range.Index>=owner.Match.Ranges.Length ||
                    !ReferenceEquals(owner.Match.Ranges[range.Index],range) || !known ||
                    !BuildBoundsOverlap(range.Source.Bounds,BuildNearOrdinaryBounds(owner,p)) && (range.Source.TargetBits&NearBuildWiringTargetBits)==0 ||
                    !(BuildBoundsOverlap(range.Source.Bounds,BuildFocusBounds(owner,p)) ||
                      query.Complete && BuildBoundsOverlap(range.Source.Bounds,query.Covered) || query.Started && BuildBoundsOverlap(range.Source.Bounds,query.Query)))
                {discovery.Members.Remove(range);discovery.Ranges.RemoveAt(i);}
            }
            foreach(var owner in discovery.NearOwners)
            {
                var state=owner.Match;
                if(state==null || !ProjectionQualificationCurrent(owner))continue;
                BuildFocusNearQuery query;
                if(!discovery.Owners.TryGetValue(owner,out query) || !ReferenceEquals(query.Match,state) || !ReferenceEquals(query.Sources,owner.Qualification.SourceRanges))
                {
                    query=new BuildFocusNearQuery{Match=state,Sources=owner.Qualification.SourceRanges,
                        Directory=owner.Qualification.GeometryDirectory};
                    discovery.Owners[owner]=query;
                }
            }
            int ownerCount=discovery.NearOwners.Count,idle=0;
            // Each pending owner receives one node before another turn. A
            // moving first instance cannot consume every later source's turn.
            while(ownerCount>0 && materialBuildSearchBudget>0 && idle<ownerCount)
            {
                discovery.OwnerCursor%=ownerCount;var owner=discovery.NearOwners[discovery.OwnerCursor];
                discovery.OwnerCursor=(discovery.OwnerCursor+1)%ownerCount;
                BuildFocusNearQuery query;
                if(!discovery.Owners.TryGetValue(owner,out query)){idle++;continue;}
                if(!query.Started && (!query.Complete || !BuildBoundsContain(query.Covered,BuildFocusBounds(owner,p))))
                {
                    var bounds=BuildFocusBounds(owner,p);
                    // Immutable geometry keeps a bounded corridor across
                    // visits; actual target legality is checked separately.
                    var previous=IntersectBuildBounds(query.Covered,bounds.Expand(Math.Max(bounds.Width,bounds.Height)));
                    query.Query=query.Complete && !EmptyBuildBounds(previous)?BuildBoundsUnion(previous,bounds):bounds;
                    query.Started=true;if(query.Directory.NodeCount>0)query.Stack.Push(0);
                }
                bool advanced=AdvanceBuildFocusNearQuery(query,index=>
                {
                    var group=query.Match.Groups[index];
                    Action<int> route=ordinal=>
                    {
#if DEBUG
                        BuildFocusDirectoryRoutes++;
#endif
                        if(ordinal<0 || ordinal>=group.Parts.Count)return;
                        var range=group.Parts[ordinal];
                        if(discovery.Members.Add(range))discovery.Ranges.Add(range);
                    };
                    group.Source.RouteProofs(IntersectBuildBounds(query.Query,BuildNearOrdinaryBounds(owner,p)),0xff,owner.Mask,route);
                    if(buildSupply.ContainsKey(3611))group.Source.RouteProofs(query.Query,NearBuildWiringTargetBits,owner.Mask,route);
                });
                if(advanced)idle=0;else idle++;
            }
            foreach(var owner in discovery.NearOwners)
            {
                BuildFocusNearQuery query;
                if(owner.Match==null || !ProjectionQualificationCurrent(owner) || !discovery.Owners.TryGetValue(owner,out query) ||
                    !query.Complete || !BuildBoundsContain(query.Covered,BuildFocusBounds(owner,p)))return false;
            }
            return true;
        }
        // Final admission may read this existing complete coverage, but must
        // never advance another query or reset an Update work allowance. A new
        // nearby placement/current qualification, changed Match/source, or
        // newly entered reach returns Pending to the ordinary discovery lane.
        private bool BuildFocusNearCurrent(Player p)
        {
            var discovery=nearBuildDiscovery;if(discovery==null)return false;
            var world=WiringOperation(buildContinuation?.Operation)?BuildFocusWorldBounds(p):BuildNearOrdinaryWorldBounds(p);
            foreach(var owner in projectionScene)
            {
                var summary=owner.Summary;
                if(summary!=null && !BuildBoundsOverlap(world,new BlueprintProjectionBounds(owner.OriginX-1,owner.OriginY-1,summary.Width+2,summary.Height+2)))continue;
                if(ProjectionQualificationFailed(owner))continue;
                BuildFocusNearQuery query;
                if(owner.Match==null || !ProjectionQualificationCurrent(owner) || !discovery.Owners.TryGetValue(owner,out query) ||
                    !ReferenceEquals(query.Match,owner.Match) || !ReferenceEquals(query.Sources,owner.Qualification.SourceRanges) ||
                    !ReferenceEquals(query.Directory,owner.Qualification.GeometryDirectory) || !query.Complete ||
                    !BuildBoundsContain(query.Covered,BuildPreferredFocusBounds(owner,p)))return false;
            }
            return true;
        }
    }
}
