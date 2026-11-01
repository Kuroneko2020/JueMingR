using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // Geometry only, shared by instances of the same qualified source.
        // Weak ownership follows that immutable source's existing lifetime;
        // no world/material facts or per-cell material index are retained.
        private sealed class BuildPhysicalDirectory
        {
            internal struct Node { internal BlueprintProjectionBounds Bounds; internal int Left, Right, Group; }
            internal readonly Node[] Nodes;
            private readonly IReadOnlyList<BlueprintProjectionSourceRange> ranges;
            private int nextLeaf, nextParent;
            internal bool Ready { get { return nextLeaf == ranges.Count && nextParent < 0; } }
            internal long Bytes { get { return 72L+Nodes.LongLength*32; } }
            internal BuildPhysicalDirectory(IReadOnlyList<BlueprintProjectionSourceRange> ranges)
            {
                this.ranges=ranges;nextParent=ranges.Count-2;
                Nodes=new Node[Math.Max(0,ranges.Count*2-1)];
            }
            internal void Step()
            {
                // The immutable physical source order supplies a balanced
                // segment tree. One leaf or parent costs one shared search
                // step; no synchronous sort or temporary order array exists.
                if(nextLeaf<ranges.Count)
                {int group=nextLeaf++;Nodes[ranges.Count-1+group]=new Node{Bounds=ranges[group].Bounds,Left=-1,Right=-1,Group=group};return;}
                if(nextParent<0)return;
                int node=nextParent--,left=node*2+1,right=left+1;
                Nodes[node]=new Node{Bounds=BuildBoundsUnion(Nodes[left].Bounds,Nodes[right].Bounds),Left=left,Right=right,Group=-1};
            }
        }
        private static readonly ConditionalWeakTable<IReadOnlyList<BlueprintProjectionSourceRange>,BuildPhysicalDirectory> buildPhysicalDirectories=
            new ConditionalWeakTable<IReadOnlyList<BlueprintProjectionSourceRange>,BuildPhysicalDirectory>();
        private sealed class BuildFocusNearQuery
        {
            internal BlueprintProjectionMatchState Match;
            internal IReadOnlyList<BlueprintProjectionSourceRange> Sources;
            internal BuildPhysicalDirectory Directory;
            internal BlueprintProjectionBounds Covered, Query;
            internal bool Complete, Started;
            internal readonly Stack<int> Stack=new Stack<int>();
        }
        private sealed class BuildFocusDiscovery
        {
            internal readonly Dictionary<BlueprintProjectionPlacement,BuildFocusNearQuery> Owners=new Dictionary<BlueprintProjectionPlacement,BuildFocusNearQuery>();
            internal readonly List<BlueprintProjectionMatchRange> Ranges=new List<BlueprintProjectionMatchRange>();
            internal readonly HashSet<BlueprintProjectionMatchRange> Members=new HashSet<BlueprintProjectionMatchRange>();
            // One final result per near range, reused by the current query.
            // A CPU slice can be revisited; a pending source is not polled.
            internal readonly Dictionary<BlueprintProjectionMatchRange,PreferredBuildSearch> Results=new Dictionary<BlueprintProjectionMatchRange,PreferredBuildSearch>();
            internal readonly List<BlueprintProjectionPlacement> Retired=new List<BlueprintProjectionPlacement>();
            internal readonly List<BlueprintProjectionPlacement> NearOwners=new List<BlueprintProjectionPlacement>();
            internal readonly HashSet<BlueprintProjectionPlacement> NearOwnerSet=new HashSet<BlueprintProjectionPlacement>();
            internal int Cursor, OwnerCursor, ResultPeakCount;
            internal ulong Tick;
            internal bool TickSet;
            internal BlueprintProjectionBounds World;
            internal float PlayerX,PlayerY,ScreenX,ScreenY;
            internal int PlayerWidth,PlayerHeight,BlockRange,ReachX,ReachY,ScreenWidth,ScreenHeight;
        }
#if DEBUG
        private static readonly List<WeakReference<BuildPhysicalDirectory>> buildFocusDirectoryWitnesses = new List<WeakReference<BuildPhysicalDirectory>>();
        internal long BuildFocusDirectorySteps { get; private set; }
        internal long BuildFocusDirectoryRoutes { get; private set; }
        internal long BuildFocusDirectoryConstructionSteps { get; private set; }
        internal long BuildFocusDirectoryBuilds { get; private set; }
        internal long BuildFocusDirectoryTemporaryBytes { get { return 0; } }
        internal long BuildFocusDirectorySharedBytes
        {
            get
            {
                long bytes=32+buildFocusDirectoryWitnesses.Capacity*8L;
                for(int i=buildFocusDirectoryWitnesses.Count-1;i>=0;i--)
                {
                    BuildPhysicalDirectory directory;
                    if(!buildFocusDirectoryWitnesses[i].TryGetTarget(out directory)){buildFocusDirectoryWitnesses.RemoveAt(i);continue;}
                    bytes+=32+directory.Bytes;
                }
                return bytes;
            }
        }
        internal long BuildFocusDirectoryBytes
        {
            get
            {
                var discovery=buildContinuation?.Discovery;if(discovery==null)return 0;
                // Collection estimates include reusable result storage even
                // after Clear retained its buckets for the next near query.
                long bytes=224+discovery.Ranges.Capacity*8L+discovery.Members.Count*32L+discovery.Ranges.Count*192L+discovery.ResultPeakCount*80L;
                bytes+=discovery.NearOwners.Capacity*8L+discovery.NearOwnerSet.Count*32L+discovery.Retired.Capacity*8L;
                foreach(var range in discovery.Ranges)
                {bytes+=range.Materials?.PreferredNegativeOrdinals?.Count*32L??0;bytes+=range.Materials?.PreferredPositionNegatives?.Count*112L??0;}
                var seen=new HashSet<BuildPhysicalDirectory>();
                foreach(var query in discovery.Owners.Values){bytes+=128+query.Stack.Count*4L;if(seen.Add(query.Directory))bytes+=query.Directory.Bytes;}
                return bytes;
            }
        }
#endif
        private static BlueprintProjectionBounds BuildBoundsUnion(BlueprintProjectionBounds a,BlueprintProjectionBounds b)
        {int left=Math.Min(a.Left,b.Left),top=Math.Min(a.Top,b.Top);return new BlueprintProjectionBounds(left,top,Math.Max(a.Right,b.Right)-left,Math.Max(a.Bottom,b.Bottom)-top);}
        private static bool BuildBoundsContain(BlueprintProjectionBounds outer,BlueprintProjectionBounds inner)
        {return outer.Left<=inner.Left && outer.Top<=inner.Top && outer.Right>=inner.Right && outer.Bottom>=inner.Bottom;}
        private BuildPhysicalDirectory CreateBuildPhysicalDirectory(IReadOnlyList<BlueprintProjectionSourceRange> ranges)
        {
#if DEBUG
            BuildFocusDirectoryBuilds++;
            var directory=new BuildPhysicalDirectory(ranges);
            buildFocusDirectoryWitnesses.Add(new WeakReference<BuildPhysicalDirectory>(directory));return directory;
#else
            return new BuildPhysicalDirectory(ranges);
#endif
        }
        private BlueprintProjectionBounds BuildFocusWorldBounds(Player p)
        {
            var focus=buildContinuation;var discovery=focus.Discovery;
            if(discovery.TickSet && discovery.Tick==materialTick && discovery.PlayerX==p.position.X && discovery.PlayerY==p.position.Y &&
                discovery.PlayerWidth==p.width && discovery.PlayerHeight==p.height && discovery.BlockRange==p.blockRange &&
                discovery.ReachX==Player.tileRangeX && discovery.ReachY==Player.tileRangeY && discovery.ScreenX==Main.screenPosition.X &&
                discovery.ScreenY==Main.screenPosition.Y && discovery.ScreenWidth==Main.screenWidth && discovery.ScreenHeight==Main.screenHeight)return discovery.World;
            discovery.TickSet=true;discovery.Tick=materialTick;
            discovery.PlayerX=p.position.X;discovery.PlayerY=p.position.Y;discovery.PlayerWidth=p.width;discovery.PlayerHeight=p.height;
            discovery.BlockRange=p.blockRange;discovery.ReachX=Player.tileRangeX;discovery.ReachY=Player.tileRangeY;
            discovery.ScreenX=Main.screenPosition.X;discovery.ScreenY=Main.screenPosition.Y;discovery.ScreenWidth=Main.screenWidth;discovery.ScreenHeight=Main.screenHeight;
            int boost=0;bool hammer=focus.Operation?.Kind==BlueprintBuildOperationKind.Hammer;
            if(AppearanceOperation(focus.Operation) && EnsureAppearanceInventory(p))
            {
                int kind=hammer?0:focus.Operation.Kind==BlueprintBuildOperationKind.Scrape?3:focus.Operation.Kind==BlueprintBuildOperationKind.PaintTile?1:2;
                for(int n=0;n<appearanceToolCounts[kind];n++)
                {int slot=appearanceTools[kind,n];if(buildTools.BuildInventoryEligible(slot,p.inventory[slot]))boost=Math.Max(boost,p.inventory[slot].tileBoost);}
            }
            else if(WiringOperation(focus.Operation))
            {
                for(int n=0;n<3;n++){int type=WiringToolType(focus.Operation,n),reach;if(buildItemReach.TryGetValue(type,out reach))boost=Math.Max(boost,reach);}
            }
            else buildItemReach.TryGetValue(focus.Source.Type,out boost);
            var hint=PreferredBuildSourceHint(p);if(hint!=null)boost=Math.Max(boost,hint.Item.tileBoost);
            int left,top,right,bottom;
            TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out right,out bottom,boost+(hammer?0:p.blockRange));
            var world=new BlueprintProjectionBounds(left,top,Math.Max(0,right-left+1),Math.Max(0,bottom-top+1));
            if(WiringOperation(focus.Operation) && WiringToolMatches(focus.Operation,3611) && buildSupply.ContainsKey(3611) && Main.screenWidth>0 && Main.screenHeight>0)
                world=BuildBoundsUnion(world,new BlueprintProjectionBounds((int)Math.Floor(Main.screenPosition.X/16f),(int)Math.Floor(Main.screenPosition.Y/16f),
                    (int)Math.Ceiling((Main.screenPosition.X+Main.screenWidth)/16f)-(int)Math.Floor(Main.screenPosition.X/16f),
                    (int)Math.Ceiling((Main.screenPosition.Y+Main.screenHeight)/16f)-(int)Math.Floor(Main.screenPosition.Y/16f)));
            discovery.World=world;return world;
        }
        private BlueprintProjectionBounds BuildFocusBounds(BlueprintProjectionPlacement owner,Player p)
        {var world=BuildFocusWorldBounds(p);return owner.View.SourceBounds(new BlueprintProjectionBounds(world.Left-owner.OriginX,world.Top-owner.OriginY,world.Width,world.Height));}
        private bool AdvanceBuildFocusNearQuery(BuildFocusNearQuery query,Action<int> emit)
        {
            if(materialBuildSearchBudget<=0)return false;
            if(!query.Directory.Ready)
            {
                materialBuildSearchBudget--;query.Directory.Step();
#if DEBUG
                BuildFocusDirectoryConstructionSteps++;
#endif
                return true;
            }
            if(!query.Started)return false;
            materialBuildSearchBudget--;
#if DEBUG
            BuildFocusDirectorySteps++;
#endif
            if(query.Stack.Count>0)
            {
                var node=query.Directory.Nodes[query.Stack.Pop()];
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
            var discovery=buildContinuation.Discovery;
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
                    !(BuildBoundsOverlap(range.Source.Bounds,BuildFocusBounds(owner,p)) ||
                      query.Complete && BuildBoundsOverlap(range.Source.Bounds,query.Covered) || query.Started && BuildBoundsOverlap(range.Source.Bounds,query.Query)))
                {if(range.Materials!=null){range.Materials.PreferredNegativeOrdinals=null;range.Materials.PreferredPositionNegatives=null;}discovery.Members.Remove(range);discovery.Ranges.RemoveAt(i);}
            }
            foreach(var owner in discovery.NearOwners)
            {
                var state=owner.Match;
                if(state==null || !ProjectionQualificationCurrent(owner))continue;
                BuildFocusNearQuery query;
                if(!discovery.Owners.TryGetValue(owner,out query) || !ReferenceEquals(query.Match,state) || !ReferenceEquals(query.Sources,owner.Qualification.SourceRanges))
                {
                    query=new BuildFocusNearQuery{Match=state,Sources=owner.Qualification.SourceRanges,
                        Directory=buildPhysicalDirectories.GetValue(owner.Qualification.SourceRanges,CreateBuildPhysicalDirectory)};
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
                if(query.Directory.Ready && !query.Started && (!query.Complete || !BuildBoundsContain(query.Covered,BuildFocusBounds(owner,p))))
                {
                    var bounds=BuildFocusBounds(owner,p);
                    // Immutable geometry keeps a bounded corridor across
                    // visits; actual target legality is checked separately.
                    var previous=IntersectBuildBounds(query.Covered,bounds.Expand(Math.Max(bounds.Width,bounds.Height)));
                    query.Query=query.Complete && !EmptyBuildBounds(previous)?BuildBoundsUnion(previous,bounds):bounds;
                    query.Started=true;if(query.Directory.Nodes.Length>0)query.Stack.Push(0);
                }
                bool advanced=AdvanceBuildFocusNearQuery(query,index=>
                {
                    var group=query.Match.Groups[index];
                    group.Source.RouteProofs(query.Query,0xff,owner.Mask,ordinal=>
                    {
#if DEBUG
                        BuildFocusDirectoryRoutes++;
#endif
                        if(ordinal<0 || ordinal>=group.Parts.Count)return;
                        var range=group.Parts[ordinal];
                        if(discovery.Members.Add(range))discovery.Ranges.Add(range);
                    });
                });
                if(advanced)idle=0;else idle++;
            }
            foreach(var owner in discovery.NearOwners)
            {
                BuildFocusNearQuery query;
                if(owner.Match==null || !ProjectionQualificationCurrent(owner) || !discovery.Owners.TryGetValue(owner,out query) ||
                    !query.Directory.Ready || !query.Complete || !BuildBoundsContain(query.Covered,BuildFocusBounds(owner,p)))return false;
            }
            return true;
        }
    }
}
