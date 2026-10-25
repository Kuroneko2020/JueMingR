using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed class BlueprintProjectionMatchRange
    {
        internal BlueprintProjectionMatchGroup Group;
        internal BlueprintProjectionPlacement Owner { get { return Group.Owner; } }
        internal BlueprintProjectionSourceRange Source { get { return Owner.MatchRanges[Index]; } }
        internal long Version, RawVersion, Stamp, ProgressStamp, Targets, Matches, Unknown, ProgressTargets, ProgressMatches, ProgressUnknown;
        internal long Watch { get { return Group.Watch; } }
        internal int Index, Offset, PartIndex;
        internal bool Current, Verified, Reading, Demand;
        // Offscreen logical proofs retain summaries only. Do not allocate an
        // empty per-object dictionary or copy shared source geometry per instance.
        internal Dictionary<long, BlueprintWorldMatchResult> Visual;
    }
    internal sealed class BlueprintProjectionMatchGroup
    {
        internal BlueprintProjectionPlacement Owner;
        internal BlueprintProjectionSourceRange Source;
        internal long Watch, NativeStamp;
        internal bool Ready;
        internal readonly List<BlueprintProjectionMatchRange> Parts=new List<BlueprintProjectionMatchRange>();
    }
    internal sealed class BlueprintProjectionMatchState
    {
        internal BlueprintProjectionMask Mask;
        internal int OriginX, OriginY, Cursor, Current, Verified;
        internal long Matches, Targets, Unknown;
        internal bool Verifying, ViewDirty;
        internal BlueprintProjectionMatchRange[] Ranges;
        internal BlueprintProjectionMatchGroup[] Groups;
        internal readonly Dictionary<long, BlueprintWorldMatchResult> Visible = new Dictionary<long, BlueprintWorldMatchResult>();
        internal IReadOnlyDictionary<long, BlueprintWorldMatchResult> View;
#if DEBUG
        internal long WorldReads;
#endif
    }
    internal sealed class BlueprintProjectionMatchRead
    {
        internal BlueprintProjectionPlacement Owner;
        internal BlueprintProjectionMatchRange Range;
        internal long Token;
        internal bool Pending;
        internal BlueprintProjectionSourceReceipt Batch;
        internal int CellIndex, ObjectId, ObjectIndex;
        internal BlueprintProjectionWindow ObjectTargets;
        internal BlueprintWorldObject ObjectTarget;
        internal IBlueprintWorldObjectWriteWitness WriteWitness;
        internal IDisposable WriteSeed;
        internal BlueprintProjectionBounds ObjectBounds;
        internal readonly List<BlueprintWorldMatchSample> ObjectSamples = new List<BlueprintWorldMatchSample>();
        internal List<BlueprintWorldMatchSample> ObjectUnion;
        internal readonly Dictionary<int, BlueprintWorldMatchState> Objects = new Dictionary<int, BlueprintWorldMatchState>();
        internal readonly Dictionary<long, BlueprintWorldMatchResult> Results = new Dictionary<long, BlueprintWorldMatchResult>();
    }
    internal enum BlueprintProjectionMoveEligibility { Checking, Matched, Movable }

    internal sealed partial class HostBlueprint
    {
        private readonly BlueprintWorldChangeObserver projectionChanges = new BlueprintWorldChangeObserver();
        private readonly Dictionary<long, BlueprintProjectionMatchGroup> projectionMatchWatches = new Dictionary<long, BlueprintProjectionMatchGroup>();
        private BlueprintWorldMatchSource projectionMatchSource;
        private object projectionMatchTiles, projectionMatchSections;
        private BlueprintProjectionMatchRead projectionMatchRead;
        private BlueprintProjectionPlacement projectionMatchPriority;
        private long projectionMatchWatch, projectionMatchToken;
        private int projectionMatchCursor;
        private bool projectionMatchPriorityTurn = true;
        private ulong projectionMatchTick;
        private bool projectionMatchTickSet;
#if DEBUG
        internal long ProjectionMatchCellReads { get; private set; }
        internal long ProjectionMatchSourceBatches { get; private set; }
        internal long ProjectionMatchInvalidations { get; private set; }
        internal long ProjectionMatchRetirements { get; private set; }
        internal int ProjectionMatchWatchCount { get { return projectionMatchWatches.Count; } }
        internal bool ProjectionMatchReading { get { return projectionMatchRead != null; } }
#endif
        private static long MatchKey(int x, int y) { return ((long)y << 32) | (uint)x; }
        private void ReleaseProjectionMatchRead(bool cancelSource)
        {
            var read=projectionMatchRead;if(read==null)return;
            projectionMatchRead=null;read.WriteSeed?.Dispose();read.WriteSeed=null;
            if(cancelSource)Library?.ClearProjectionSource(read.Owner.Identity);
        }
        private void EnsureProjectionMatch(BlueprintProjectionPlacement item)
        {
            if (!item.Opened || item.SourceRanges == null || item.MatchRanges == null || item.Summary == null) return;
            var state = item.Match;
            if (state != null && ReferenceEquals(state.Mask, item.Mask) && state.OriginX == item.OriginX && state.OriginY == item.OriginY) return;
            ReleaseProjectionMatch(item);
            state = new BlueprintProjectionMatchState { Mask = item.Mask, OriginX = item.OriginX, OriginY = item.OriginY, Ranges = new BlueprintProjectionMatchRange[item.MatchRanges.Count], Groups=new BlueprintProjectionMatchGroup[item.SourceRanges.Count], ViewDirty = true };
            state.View = new ReadOnlyDictionary<long, BlueprintWorldMatchResult>(state.Visible);
            item.Match = state;
            for(int i=0;i<state.Groups.Length;i++)state.Groups[i]=new BlueprintProjectionMatchGroup{Owner=item,Source=item.SourceRanges[i],Watch=++projectionMatchWatch};
            for (int i = 0; i < state.Ranges.Length; i++)
            {
                var source = item.MatchRanges[i];var group=state.Groups[source.ObservationGroup];
                var range = new BlueprintProjectionMatchRange { Index = i,Group=group,PartIndex=group.Parts.Count };
                state.Ranges[i]=range;group.Parts.Add(range);
            }
            foreach(var group in state.Groups)
            {
                // A later physical block may contain only more members of an
                // object already owned by an earlier group's full footprint.
                if(group.Parts.Count==0)continue;
                var source=group.Source;var bounds=source.Bounds;projectionMatchWatches.Add(group.Watch,group);
                int originX = item.OriginX, originY = item.OriginY;
                projectionChanges.Watch(group.Watch, new BlueprintProjectionBounds(checked(originX + bounds.Left), checked(originY + bounds.Top), bounds.Width, bounds.Height),
                    (changed, layers) => source.IntersectsProofs(new BlueprintProjectionBounds(changed.Left - originX, changed.Top - originY, changed.Width, changed.Height), layers, state.Mask), source.TargetBits,
                    group.Parts.Count,(changed,layers,emit)=>source.RouteProofs(new BlueprintProjectionBounds(changed.Left-originX,changed.Top-originY,changed.Width,changed.Height),layers,state.Mask,emit));
                group.Ready=projectionChanges.TryGetStamp(group.Watch,out group.NativeStamp);
            }
        }
        private void ReleaseProjectionMatch(BlueprintProjectionPlacement item)
        {
            if (item == null) return;
            if (ReferenceEquals(projectionMatchRead?.Owner, item)) ReleaseProjectionMatchRead(true);
            if (item.Match != null) foreach (var group in item.Match.Groups) { projectionChanges.Unwatch(group.Watch); projectionMatchWatches.Remove(group.Watch); }
            item.Match = null; item.Renderer.SetMatchView(null);
            if (ReferenceEquals(projectionMatchPriority, item)) projectionMatchPriority = null;
        }
        private void InvalidateProjectionMatch(BlueprintProjectionMatchRange range)
        {
            var state = range.Owner.Match; if (state == null) return;
            range.Version++;range.RawVersion++;
            if (range.Current) { state.Current--; state.Targets -= range.Targets; state.Matches -= range.Matches; state.Unknown -= range.Unknown; }
            if (range.Verified) state.Verified--;
            range.Current = range.Verified = range.Reading = false; range.Offset = 0;
            if(range.Visual!=null) { foreach (var pair in range.Visual) RemoveProjectionMatchVisual(state,pair.Key,pair.Value.TargetBits); range.Visual=null; }
            if (ReferenceEquals(projectionMatchRead?.Range, range)) ReleaseProjectionMatchRead(true);
#if DEBUG
            ProjectionMatchInvalidations++;
#endif
        }
        private void DrainProjectionChanges()
        {
            // Drain runs under the observer lock. This callback only revokes
            // derived proof; it never reads world/files or mutates the scene.
            projectionChanges.DrainRoutes((id,part,hardDirty) =>
            {
                BlueprintProjectionMatchGroup group;if(!projectionMatchWatches.TryGetValue(id,out group) || part<0 || part>=group.Parts.Count)return;
                var range=group.Parts[part];
                if(hardDirty || !range.Current || range.Targets<=0 || range.Matches!=range.Targets || range.Unknown!=0) { InvalidateProjectionMatch(range);return; }
                // A native transaction can preserve only an already complete
                // canonical Match through its immutable frame witness. Raw
                // samples are a different fact: always cancel that in-flight
                // group, even if all completed building semantics survived.
                range.RawVersion++;range.Reading=false;range.Offset=0;
                if(ReferenceEquals(projectionMatchRead?.Range,range))ReleaseProjectionMatchRead(true);
            });
            // A physical native stamp guards in-flight writes. Only its routed
            // proofs get a new local version; stable sibling proofs retain
            // their progress after the exact real changes have been drained.
            // These local versions are not fabricated world mutation epochs.
            foreach(var group in projectionMatchWatches.Values)group.Ready=projectionChanges.TryGetStamp(group.Watch,out group.NativeStamp);
        }
        private bool ProjectionMatchCurrent(BlueprintProjectionMatchRange range,long version)
        { return range.Version==version && range.Group.Ready && projectionChanges.IsCurrent(range.Watch,range.Group.NativeStamp); }
        private bool ProjectionMatchReadCurrent(BlueprintProjectionMatchRange range,long rawVersion)
        { return range.RawVersion==rawVersion && range.Group.Ready && projectionChanges.IsCurrent(range.Watch,range.Group.NativeStamp); }
        private void AdvanceProjectionMatch(ulong updateTick)
        {
            if (projectionMatchTickSet && projectionMatchTick == updateTick) return;
            projectionMatchTickSet = true; projectionMatchTick = updateTick;
            if (!HasSession || Library == null) return;
            if (projectionScene.Count == 0) return;
            if (projectionMatchSource == null || !ReferenceEquals(projectionMatchTiles, Terraria.Main.tile) || !ReferenceEquals(projectionMatchSections, Terraria.Main.sectionManager))
            {
                foreach (var item in projectionScene) ReleaseProjectionMatch(item);
                projectionMatchTiles = Terraria.Main.tile; projectionMatchSections = Terraria.Main.sectionManager;
                projectionMatchSource = new BlueprintWorldMatchSource(() => HasSession);
            }
            foreach (var item in projectionScene) EnsureProjectionMatch(item);
            DrainProjectionChanges(); TakeProjectionMatchSources();
            // One bounded source slice is retained scene-wide, not one full
            // block/cache per instance. Draw and Poll never replenish this work.
            int reads = 256;
            if (projectionMatchRead == null) StartProjectionMatchRead();
            if (projectionMatchRead != null) AdvanceProjectionMatchRead(ref reads);
            foreach (var item in projectionScene) PublishProjectionMatchView(item);
            ApplyCompletedProjections();
        }
        private BlueprintProjectionMatchRange NextProjectionMatchRange(BlueprintProjectionPlacement item)
        {
            var state = item.Match; if (state == null || state.Ranges.Length == 0) return null;
            if (state.Current == state.Ranges.Length && !state.Verifying)
            {
                if (state.Unknown == 0 && state.Targets > 0 && state.Targets == item.TotalLayerCount - item.Mask.RemovedLayerCount && state.Matches == state.Targets)
                {
                    state.Verifying = true; state.Verified = 0; foreach (var range in state.Ranges) range.Verified = false;
                }
                else { bool demand = false; foreach (var range in state.Ranges) if (range.Demand) { demand = true; break; } if (!demand) return null; }
            }
            // Keep successive bounded slices of one physical source block
            // adjacent. Rotating 65 blocks through the shared 64-block cache
            // after every 128 records decodes each 2048-record block 16 times.
            // Scene scheduling still rotates instances after every slice; this
            // is source locality, not a larger world-read or retention budget.
            foreach (var range in state.Ranges) if (range.Reading) return range;
            for (int n = 0; n < state.Ranges.Length; n++)
            {
                int i = (state.Cursor + n) % state.Ranges.Length; var range = state.Ranges[i];
                if (!range.Current || range.Reading || range.Demand || state.Verifying && !range.Verified)
                { state.Cursor = (i + 1) % state.Ranges.Length; return range; }
            }
            return null;
        }
        private void StartProjectionMatchRead()
        {
            int count = projectionScene.Count; if (count == 0) return;
            for (int n = -1; n < count; n++)
            {
                // An explicit move query may take every other admission. It
                // cannot monopolize the scene until a large source is covered.
                if(n<0 && !projectionMatchPriorityTurn)continue;
                var item = n < 0 ? projectionMatchPriority : projectionScene[(projectionMatchCursor + n) % count];
                if (item == null || !projectionScene.Contains(item)) continue;
                var range = NextProjectionMatchRange(item); if (range == null) continue;
                if(!range.Group.Ready || !projectionChanges.IsCurrent(range.Watch,range.Group.NativeStamp))continue;
                long stamp=range.RawVersion;
                if (range.Reading && stamp != range.ProgressStamp) { InvalidateProjectionMatch(range);stamp=range.RawVersion; }
                if (!range.Reading)
                { range.Reading = true; range.Offset = 0; range.ProgressStamp = stamp; range.ProgressTargets = range.ProgressMatches = range.ProgressUnknown = 0; }
                projectionMatchRead = new BlueprintProjectionMatchRead { Owner = item, Range = range, Token = ++projectionMatchToken };
                projectionMatchPriorityTurn=n>=0;
                projectionMatchCursor = (projectionMatchCursor + Math.Max(1, n + 1)) % count;
                RequestProjectionMatchSource(projectionMatchRead); return;
            }
        }
        private void RequestProjectionMatchSource(BlueprintProjectionMatchRead read)
        {
            if (read.Pending) return;
            read.Pending = Library.RequestProjectionSource(read.Owner.Identity, read.Token, read.Range.Index, read.ObjectId, read.Range.Offset);
        }
        private void TakeProjectionMatchSources()
        {
            BlueprintProjectionSourceReceipt receipt;
            while (Library.TryTakeProjectionSource(out receipt))
            {
                var read = projectionMatchRead;
                if (read == null || receipt.Generation != read.Token || !receipt.Identity.Equals(read.Owner.Identity) || receipt.Range != read.Range.Index || receipt.ObjectId != read.ObjectId) continue;
                read.Pending = false;
                if (!ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp)) { InvalidateProjectionMatch(read.Range); continue; }
                if (receipt.ObjectId == 0) { read.Batch = receipt;
#if DEBUG
                    ProjectionMatchSourceBatches++;
#endif
                }
                else read.ObjectTargets = receipt.Window;
            }
        }
        private BlueprintWorldMatchSample ReadProjectionWorld(int x, int y)
        {
            BlueprintWorldMatchSample sample; projectionMatchSource.TryRead(x, y, out sample);
#if DEBUG
            ProjectionMatchCellReads++;
            if (projectionMatchRead?.Owner.Match != null) projectionMatchRead.Owner.Match.WorldReads++;
#endif
            return sample;
        }
        private void AdvanceProjectionMatchRead(ref int reads)
        {
            var read = projectionMatchRead;
            if (!ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp)) { InvalidateProjectionMatch(read.Range); return; }
            if (read.Batch == null) { RequestProjectionMatchSource(read); return; }
            while (reads > 0 && read.CellIndex < read.Batch.Window.Cells.Count)
            {
                var cell = read.Owner.Mask.Apply(read.Batch.Window.Cells[read.CellIndex]);
                // Permanent erasure is resolved from immutable source data.
                // A cell with no effective target has no world dependency and
                // must not spend observation reads on its old raw layers.
                if (!cell.HasContent) { read.CellIndex++; continue; }
                BlueprintWorldMatchState objectState = BlueprintWorldMatchState.Unknown;
                if ((cell.Layers & CaptureLayer.Tile) != 0 && cell.ObjectId > 0 && !read.Objects.TryGetValue(cell.ObjectId, out objectState))
                {
                    if (read.ObjectId == 0) read.ObjectId = cell.ObjectId;
                    if (!AdvanceProjectionMatchObject(read, ref reads)) return;
                    objectState = read.Objects[cell.ObjectId];
                }
                BlueprintWorldMatchResult result;
                if(cell.ObjectId>0 && cell.Layers==CaptureLayer.Tile)
                { read.CellIndex++;continue; } // The complete object was counted once by FinishProjectionMatchObject.
                else
                {
                    if (reads == 0) return;
                    var sample = ReadProjectionWorld(read.Owner.OriginX + cell.X, read.Owner.OriginY + cell.Y); reads--;
                    result=BlueprintWorldMatchComparer.CompareCell(cell,sample,objectState);
                }
                read.Results[MatchKey(cell.X, cell.Y)] = result;
                read.Range.ProgressTargets += result.TargetCount; read.Range.ProgressMatches += result.MatchCount;
                read.Range.ProgressUnknown += CountMatchBits(result.UnknownBits); read.CellIndex++;
            }
            if (read.CellIndex != read.Batch.Window.Cells.Count) return;
            if (!ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp)) { InvalidateProjectionMatch(read.Range); return; }
            var range = read.Range; var state = read.Owner.Match;
            foreach (var pair in read.Results) if (ProjectionMatchCellResident(read.Owner, (int)(uint)pair.Key, (int)(pair.Key >> 32)))
            { if(range.Visual==null)range.Visual=new Dictionary<long,BlueprintWorldMatchResult>();BlueprintWorldMatchResult old;if(range.Visual.TryGetValue(pair.Key,out old))RemoveProjectionMatchVisual(state,pair.Key,old.TargetBits);range.Visual[pair.Key]=pair.Value;MergeProjectionMatchVisual(state,pair.Key,pair.Value); }
            range.Offset = read.Batch.NextOffset;
            if (read.Batch.Complete)
            {
                if (range.Current) { state.Targets -= range.Targets; state.Matches -= range.Matches; state.Unknown -= range.Unknown; } else state.Current++;
                range.Current = true; range.Reading = false; range.Offset = 0; range.Stamp = range.Version;
                range.Targets = range.ProgressTargets; range.Matches = range.ProgressMatches; range.Unknown = range.ProgressUnknown;
                state.Targets += range.Targets; state.Matches += range.Matches; state.Unknown += range.Unknown; range.Demand = false;
                if (state.Verifying && !range.Verified) { range.Verified = true; state.Verified++; }
                if (state.Verifying && (range.Unknown != 0 || range.Matches != range.Targets)) { state.Verifying = false; state.Verified = 0; foreach (var other in state.Ranges) other.Verified = false; }
                if(read.WriteWitness!=null && range.Targets>0 && range.Matches==range.Targets && range.Unknown==0)
                    projectionChanges.TrySetWitness(range.Watch,range.Group.NativeStamp,range.PartIndex,read.WriteWitness);
            }
            ReleaseProjectionMatchRead(false);
        }
        private bool AdvanceProjectionMatchObject(BlueprintProjectionMatchRead read, ref int reads)
        {
            if (read.ObjectTargets == null) { RequestProjectionMatchSource(read); return false; }
            if (read.ObjectTarget == null)
            {
                if (read.ObjectTargets.Cells.Count == 0 || read.ObjectTargets.Objects.Count != 1 ||
                    !projectionMatchSource.TryDescribeTarget(read.ObjectTargets.Objects[0], read.ObjectTargets.Cells[0], read.Owner.OriginX, read.Owner.OriginY, out read.ObjectTarget))
                { FinishProjectionMatchObject(read, BlueprintWorldMatchState.Unknown); return true; }
                if(read.ObjectTarget.Family==BlueprintWorldObjectFamily.Door)
                {
                    // Raw inactive-wing frames are irrelevant to ordinary
                    // terrain Match but are inputs to a new door certificate.
                    // Protect this one in-flight seed before the first read,
                    // including the period before any witness exists.
                    var seedBounds=new BlueprintProjectionBounds(read.ObjectTarget.AnchorX-1,read.ObjectTarget.AnchorY,3,3);
                    var seedDependency=read.Range.Source.Bounds;
                    var seedWatched=new BlueprintProjectionBounds(read.Owner.OriginX+seedDependency.Left,read.Owner.OriginY+seedDependency.Top,seedDependency.Width,seedDependency.Height);
                    if(!ContainsCore(seedWatched,seedBounds)) { FinishProjectionMatchObject(read,BlueprintWorldMatchState.Unknown);return true; }
                    read.WriteSeed=projectionChanges.BeginDoorSeed(read.Range.Watch,read.Range.Group.NativeStamp,read.Range.PartIndex,seedBounds);
                    if(read.WriteSeed==null) { InvalidateProjectionMatch(read.Range);return false; }
                }
                if (reads == 0) return false;
                var hinge = ReadProjectionWorld(read.ObjectTarget.AnchorX, read.ObjectTarget.AnchorY); reads--;
                var actual = hinge.Object;
                var shape = actual != null && read.ObjectTarget.SamePlacement(actual) ? actual : read.ObjectTarget;
                read.ObjectBounds = new BlueprintProjectionBounds(shape.WorldX, shape.WorldY, shape.Width, shape.Height);
                var dependency = read.Range.Source.Bounds;
                var observed = new BlueprintProjectionBounds(read.Owner.OriginX + dependency.Left, read.Owner.OriginY + dependency.Top, dependency.Width, dependency.Height);
                if (!ContainsCore(observed, read.ObjectBounds)) { FinishProjectionMatchObject(read, BlueprintWorldMatchState.Unknown); return true; }
            }
            int total = checked(read.ObjectBounds.Width * read.ObjectBounds.Height);
            while (reads > 0 && read.ObjectIndex < total)
            {
                int x = read.ObjectBounds.Left + read.ObjectIndex % read.ObjectBounds.Width, y = read.ObjectBounds.Top + read.ObjectIndex / read.ObjectBounds.Width;
                read.ObjectSamples.Add(ReadProjectionWorld(x, y)); read.ObjectIndex++; reads--;
            }
            if (read.ObjectIndex != total) return false;
            var state = BlueprintWorldMatchComparer.CompareObject(read.ObjectTarget, read.ObjectTargets.Cells, read.ObjectSamples,
                ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp));
            if(state==BlueprintWorldMatchState.Match)
            {
                if(read.ObjectTarget.Family==BlueprintWorldObjectFamily.Door)
                {
                    // A door can later occupy either wing. Its certificate
                    // needs the actual gated 3x3 context, including inactive
                    // cells' raw residues. Reuse occupied samples and charge
                    // only additional reads to this same update allowance.
                    var union=new BlueprintProjectionBounds(read.ObjectTarget.AnchorX-1,read.ObjectTarget.AnchorY,3,3);
                    var dependency=read.Range.Source.Bounds;
                    var watched=new BlueprintProjectionBounds(read.Owner.OriginX+dependency.Left,read.Owner.OriginY+dependency.Top,dependency.Width,dependency.Height);
                    if(ContainsCore(watched,union))
                    {
                        if(read.ObjectUnion==null)read.ObjectUnion=new List<BlueprintWorldMatchSample>(9);
                        while(read.ObjectUnion.Count<9)
                        {
                            int index=read.ObjectUnion.Count,x=union.Left+index%3,y=union.Top+index/3;
                            BlueprintWorldMatchSample sample;
                            if(read.ObjectBounds.Contains(x,y))sample=read.ObjectSamples[(y-read.ObjectBounds.Top)*read.ObjectBounds.Width+x-read.ObjectBounds.Left];
                            else { if(reads==0)return false;sample=ReadProjectionWorld(x,y);reads--; }
                            read.ObjectUnion.Add(sample);
                        }
                        BlueprintWorldDoorWitness door;
                        if(projectionMatchSource.TryCreateDoorWitness(read.ObjectTarget,read.ObjectTargets.Cells,read.ObjectSamples,read.ObjectUnion,ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp),out door))read.WriteWitness=door;
                    }
                }
                else
                {
                    BlueprintWorldObjectFrameWitness frame;
                    if(projectionMatchSource.TryCreateFrameWitness(read.ObjectTarget,read.ObjectTargets.Cells,read.ObjectSamples,ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp),out frame))read.WriteWitness=frame;
                }
            }
            FinishProjectionMatchObject(read, state); return true;
        }
        private static void FinishProjectionMatchObject(BlueprintProjectionMatchRead read, BlueprintWorldMatchState state)
        {
            // Whole-object Tile is one structural result, published for every
            // effective member. Independent same-cell layers live in the
            // physical block's ordinary proof, so no second world sample is
            // needed and no complete object DTO stays after this workset.
            if(read.ObjectTargets!=null)foreach(var raw in read.ObjectTargets.Cells)
            {
                var cell=read.Owner.Mask.Apply(raw);if((cell.Layers&CaptureLayer.Tile)==0)continue;
                var result=new BlueprintWorldMatchResult(1,state==BlueprintWorldMatchState.Match?(ushort)1:(ushort)0,state==BlueprintWorldMatchState.Conflict?(ushort)1:(ushort)0,state==BlueprintWorldMatchState.Unknown?(ushort)1:(ushort)0);
                read.Results[MatchKey(cell.X,cell.Y)]=result;read.Range.ProgressTargets++;read.Range.ProgressMatches+=result.MatchCount;read.Range.ProgressUnknown+=CountMatchBits(result.UnknownBits);
            }
            read.Objects[read.ObjectId]=state;read.ObjectId=read.ObjectIndex=0;read.ObjectTarget=null;read.ObjectTargets=null;read.ObjectSamples.Clear();read.ObjectUnion=null;
        }
        private static int CountMatchBits(ushort bits) { int count = 0; while (bits != 0) { count += bits & 1; bits >>= 1; } return count; }
        private static void RemoveProjectionMatchVisual(BlueprintProjectionMatchState state,long key,ushort bits)
        { BlueprintWorldMatchResult old;if(!state.Visible.TryGetValue(key,out old))return;var remaining=new BlueprintWorldMatchResult((ushort)(old.TargetBits&~bits),old.MatchBits,old.ConflictBits,old.UnknownBits);if(remaining.TargetBits==0)state.Visible.Remove(key);else state.Visible[key]=remaining; }
        private static void MergeProjectionMatchVisual(BlueprintProjectionMatchState state,long key,BlueprintWorldMatchResult value)
        { BlueprintWorldMatchResult old;state.Visible.TryGetValue(key,out old);state.Visible[key]=new BlueprintWorldMatchResult((ushort)(old.TargetBits|value.TargetBits),(ushort)(old.MatchBits|value.MatchBits),(ushort)(old.ConflictBits|value.ConflictBits),(ushort)(old.UnknownBits|value.UnknownBits)); }
        private static bool ProjectionMatchCellResident(BlueprintProjectionPlacement item, int x, int y)
        { foreach (var window in item.Windows.Values) if (window.Bounds.Contains(x, y)) return true; return false; }
        private void DemandProjectionMatchView(BlueprintProjectionPlacement item, BlueprintProjectionBounds bounds)
        {
            var state = item.Match; if (state == null) return;
            foreach (var range in state.Ranges) if (MatchIntersects(range.Source.Bounds, bounds)) range.Demand = true;
            state.ViewDirty = true;
        }
        private static bool MatchIntersects(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        { return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom; }
        private void PublishProjectionMatchView(BlueprintProjectionPlacement item)
        {
            var state = item.Match; if (state == null || !state.ViewDirty) return;
            // Result values are immutable; the Host updates this bounded table
            // only at Update's publication point. Draw gets a read-only facade
            // on the same game thread. Copying every visible cell after each
            // 128-record source slice would turn first coverage quadratic.
            foreach (var range in state.Ranges)
            {
                if(range.Visual==null)continue;
                var remove = new List<long>();
                foreach (var pair in range.Visual)
                    if (!ProjectionMatchCellResident(item, (int)(uint)pair.Key, (int)(pair.Key >> 32))) remove.Add(pair.Key);
                foreach (long key in remove) { RemoveProjectionMatchVisual(state,key,range.Visual[key].TargetBits);range.Visual.Remove(key); }
                if(range.Visual.Count==0)range.Visual=null;
            }
            item.Renderer.SetMatchView(state.View); state.ViewDirty = false;
        }
        private BlueprintProjectionMoveEligibility ProjectionMoveEligibility(BlueprintProjectionPlacement item)
        {
            EnsureProjectionMatch(item); return ReadProjectionMoveEligibility(item);
        }
        private BlueprintProjectionMoveEligibility ReadProjectionMoveEligibility(BlueprintProjectionPlacement item)
        {
            var state = item.Match; if (state == null || state.Ranges.Length == 0) return BlueprintProjectionMoveEligibility.Checking;
            bool unknown = false;
            foreach (var range in state.Ranges)
            {
                if (!range.Current || !ProjectionMatchCurrent(range,range.Stamp)) { unknown = true; continue; }
                if (range.Matches > 0) return BlueprintProjectionMoveEligibility.Matched;
                if (range.Unknown > 0) unknown = true;
            }
            return unknown || state.Targets != item.TotalLayerCount - item.Mask.RemovedLayerCount ? BlueprintProjectionMoveEligibility.Checking : BlueprintProjectionMoveEligibility.Movable;
        }
        private bool CheckProjectionMove(BlueprintProjectionPlacement item)
        {
            var eligibility = ProjectionMoveEligibility(item); if (eligibility == BlueprintProjectionMoveEligibility.Movable) return true;
            projectionMatchPriority = item;
            if (eligibility == BlueprintProjectionMoveEligibility.Checking && item.Match != null)
                foreach (var range in item.Match.Ranges) if (!range.Current || range.Unknown != 0) range.Demand = true;
            projectionFeedback = eligibility == BlueprintProjectionMoveEligibility.Matched ? "这份蓝图已有建好的部分，不能移动；仍可移除。" : "正在核对这份蓝图的建造情况，请稍后再试。";
            return false;
        }
        internal BlueprintProjectionMoveEligibility ProjectionHoverMoveEligibility
        {
            get { return hoveredProjection == null || ProjectionMode != BlueprintProjectionMode.SelectingMove ? BlueprintProjectionMoveEligibility.Movable : ReadProjectionMoveEligibility(hoveredProjection); }
        }
        private bool TryRunProjectionMovable(BlueprintProjectionPlacement item, Action action)
        {
            if (ProjectionMoveEligibility(item) != BlueprintProjectionMoveEligibility.Movable) return false;
            var last = item.Match.Ranges[item.Match.Ranges.Length - 1]; bool applied = false;
            projectionChanges.TryRunStable(last.Watch, last.Group.NativeStamp, () => { if (ProjectionMoveEligibility(item) == BlueprintProjectionMoveEligibility.Movable) { action(); applied = true; } });
            return applied;
        }
        private void ApplyCompletedProjections()
        {
            // Completion never enters RetireProjection while Committing: that
            // path intentionally cancels failed manual transactions. Defer and
            // recheck exact masks/origins after this unrelated edit reaches safety.
            if (ProjectionMode == BlueprintProjectionMode.Committing) return;
            foreach (var item in new List<BlueprintProjectionPlacement>(projectionScene))
            {
                var state = item.Match;
                if (ReferenceEquals(item, restoreProjection) || state == null || state.Ranges.Length == 0 || !state.Verifying ||
                    state.Current != state.Ranges.Length || state.Verified != state.Ranges.Length || state.Unknown != 0 || state.Targets <= 0 ||
                    state.Targets != item.TotalLayerCount - item.Mask.RemovedLayerCount || state.Matches != state.Targets || ReferenceEquals(projectionMatchRead?.Owner, item)) continue;
                var last = state.Ranges[state.Ranges.Length - 1];
                projectionChanges.TryRunStable(last.Watch, last.Group.NativeStamp, () =>
                {
                    if (!projectionScene.Contains(item) || !ReferenceEquals(item.Match, state) || !ReferenceEquals(state.Mask, item.Mask) || state.OriginX != item.OriginX || state.OriginY != item.OriginY) return;
                    foreach (var range in state.Ranges) if (!range.Current || !range.Verified || !ProjectionMatchCurrent(range,range.Stamp)) return;
                    RetireProjection(item); InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
                    if ((ProjectionMode == BlueprintProjectionMode.SelectingMove || ProjectionMode == BlueprintProjectionMode.SelectingRemove) && projectionScene.Count == 0) ProjectionMode = BlueprintProjectionMode.None;
#if DEBUG
                    ProjectionMatchRetirements++;
#endif
                });
            }
        }
    }
}
