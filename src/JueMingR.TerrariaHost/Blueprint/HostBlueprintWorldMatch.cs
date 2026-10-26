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
        internal bool Current, Reading, Demand;
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
        internal int OriginX, OriginY, Cursor, Current, Demands;
        internal long Matches, Targets, Unknown;
        internal bool ViewDirty;
        internal BlueprintProjectionMatchRange[] Ranges;
        internal BlueprintProjectionMatchGroup[] Groups;
        internal readonly Dictionary<long, BlueprintWorldMatchResult> Visible = new Dictionary<long, BlueprintWorldMatchResult>();
        internal IReadOnlyDictionary<long, BlueprintWorldMatchResult> View;
#if DEBUG
        internal long WorldReads, SourceRequests, SourceArrivals;
#endif
    }
    internal sealed class BlueprintProjectionMatchRead
    {
        internal BlueprintProjectionPlacement Owner;
        internal BlueprintProjectionMatchRange Range;
        internal long Token;
        internal bool Sampling;
        internal BlueprintProjectionSourcePart Batch;
        internal int SourcePart;
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
    // Transport is immutable and belongs to an identity, while a partial world
    // sample belongs to exactly one logical range/raw generation. Keeping these
    // lifetimes separate lets a dirty object yield without discarding siblings.
    internal sealed class BlueprintProjectionMatchWork
    {
        internal BlueprintProjectionPlacement Owner;
        internal long Token;
        internal bool Pending;
        internal int PartCursor, Restarts, SceneIndex;
        internal readonly List<BlueprintProjectionSourcePart> Parts = new List<BlueprintProjectionSourcePart>();
        internal readonly HashSet<int> Requested = new HashSet<int>();
        internal BlueprintProjectionMatchRead Read;
    }
    internal enum BlueprintProjectionMoveEligibility { Checking, Matched, Movable }

    internal sealed partial class HostBlueprint
    {
        private readonly BlueprintWorldChangeObserver projectionChanges = new BlueprintWorldChangeObserver();
        private readonly Dictionary<long, BlueprintProjectionMatchGroup> projectionMatchWatches = new Dictionary<long, BlueprintProjectionMatchGroup>();
        private BlueprintWorldMatchSource projectionMatchSource;
        private object projectionMatchTiles, projectionMatchSections;
        private BlueprintProjectionMatchRead projectionMatchRead;
        private readonly Dictionary<BlueprintProjectionIdentity, BlueprintProjectionMatchWork> projectionMatchWork = new Dictionary<BlueprintProjectionIdentity, BlueprintProjectionMatchWork>();
        private readonly BlueprintProjectionMatchWork[] projectionMatchChoices=new BlueprintProjectionMatchWork[4];
        private const int ProjectionMatchWorkLimit = 4, ProjectionMatchScheduleLimit = 64;
        private const long ProjectionMatchWorkOverhead = 4096;
        private static readonly long ProjectionMatchSourceLimit = 2 * BlueprintProjectionSourceReceipt.MaximumEstimatedBytes + ProjectionMatchWorkLimit * ProjectionMatchWorkOverhead;
        private BlueprintProjectionPlacement projectionMatchPriority;
        private long projectionMatchWatch, projectionMatchToken;
        private int projectionMatchCursor, projectionMatchAdmissionCursor;
        private bool projectionMatchPriorityTurn = true;
        private bool projectionMatchAdmissionPriorityTurn = true;
        private ulong projectionMatchTick;
        private bool projectionMatchTickSet;
#if DEBUG
        internal long ProjectionMatchCellReads { get; private set; }
        internal long ProjectionMatchSourceBatches { get; private set; }
        internal long ProjectionMatchSourceRequests { get; private set; }
        internal long ProjectionMatchSourceSteps { get; private set; }
        internal long ProjectionMatchSourceBytesPeak { get; private set; }
        internal long ProjectionMatchReservedBytesPeak { get; private set; }
        internal long ProjectionMatchRetainedBytesPeak { get; private set; }
        internal long ProjectionMatchCompletedObjects { get; private set; }
        internal long ProjectionMatchServices { get; private set; }
        internal long ProjectionMatchOrdinaryServices { get; private set; }
        internal long ProjectionMatchPriorityServices { get; private set; }
        internal long ProjectionMatchAdmissionAttempts { get; private set; }
        internal int ProjectionMatchWorkCount { get { return projectionMatchWork.Count; } }
        internal long ProjectionMatchSourceRetainedBytes { get { return ProjectionMatchSourceBytes(false); } }
        internal long ProjectionMatchReservedBytes { get { return ProjectionMatchSourceBytes(true); } }
        internal long ProjectionMatchInvalidations { get; private set; }
        internal long ProjectionMatchRetirements { get; private set; }
        internal int ProjectionMatchWatchCount { get { return projectionMatchWatches.Count; } }
        internal bool ProjectionMatchReading { get { return projectionMatchWork.Count != 0; } }
#endif
        private static long MatchKey(int x, int y) { return ((long)y << 32) | (uint)x; }
        private void ReleaseProjectionMatchRead(bool cancelSource)
        {
            var read=projectionMatchRead;if(read==null)return;
            if(cancelSource) { ReleaseProjectionMatchWork(read.Owner.Identity);return; }
            projectionMatchRead=null;read.WriteSeed?.Dispose();read.WriteSeed=null;
            BlueprintProjectionMatchWork work;
            if(projectionMatchWork.TryGetValue(read.Owner.Identity,out work) && ReferenceEquals(work.Read,read))
            {
                work.Parts[read.SourcePart]=null;work.Read=null;
                if(!work.Pending && !HasProjectionMatchParts(work))projectionMatchWork.Remove(read.Owner.Identity);
            }
        }
        private static bool HasProjectionMatchParts(BlueprintProjectionMatchWork work)
        { foreach(var part in work.Parts)if(part!=null)return true;return false; }
        private void ReleaseProjectionMatchWork(BlueprintProjectionIdentity identity)
        {
            BlueprintProjectionMatchWork work;
            if(projectionMatchWork.TryGetValue(identity,out work))
            {
                work.Read?.WriteSeed?.Dispose();
                if(ReferenceEquals(projectionMatchRead,work.Read))projectionMatchRead=null;
                projectionMatchWork.Remove(identity);
            }
            Library?.ClearProjectionSource(identity);
        }
        private void ClearProjectionMatchWork()
        {
            foreach(var identity in new List<BlueprintProjectionIdentity>(projectionMatchWork.Keys))ReleaseProjectionMatchWork(identity);
            projectionMatchRead=null;projectionMatchCursor=projectionMatchAdmissionCursor=0;projectionMatchPriorityTurn=projectionMatchAdmissionPriorityTurn=true;
        }
        private void ResetProjectionMatchRead(BlueprintProjectionMatchRange range)
        {
            BlueprintProjectionMatchWork work;
            if(!projectionMatchWork.TryGetValue(range.Owner.Identity,out work) || !ReferenceEquals(work.Read?.Range,range))return;
            var read=work.Read;read.WriteSeed?.Dispose();read.WriteSeed=null;work.Read=null;
            if(read.Sampling)work.Restarts++;
            if(ReferenceEquals(projectionMatchRead,read))projectionMatchRead=null;
            // The same source part remains in Parts. It contains no world
            // value; a new attempt will bind the new raw stamp at first read.
        }
        private static void DemandProjectionMatchRange(BlueprintProjectionMatchRange range)
        {
            if(range.Demand)return;range.Demand=true;range.Owner.Match.Demands++;
        }
        private bool YieldRestartedProjectionMatchWork()
        {
            // Stable large samples finish their finite range before releasing
            // a slot. A sole repeatedly revoked part must not retain all scene
            // admissions forever; its siblings have already been consumed and
            // their completed proofs are independent of this source eviction.
            BlueprintProjectionIdentity yielded=null;
            foreach(var work in projectionMatchWork.Values)
            {
                if(work.Pending || work.Read!=null || work.Restarts==0)continue;
                int parts=0;foreach(var part in work.Parts)if(part!=null)parts++;
                if(parts!=1)continue;
                yielded=work.Owner.Identity;break;
            }
            if(yielded==null)return false;ReleaseProjectionMatchWork(yielded);return true;
        }
        private void RemoveProjectionSceneItem(BlueprintProjectionPlacement item)
        {
            int index=projectionScene.IndexOf(item);if(index<0)return;
            projectionScene.RemoveAt(index);
            // Removing an earlier entry shifts the already chosen ordinary
            // successor left. Preserve that identity instead of skipping it.
            if(index<projectionMatchCursor)projectionMatchCursor--;
            if(index<projectionMatchAdmissionCursor)projectionMatchAdmissionCursor--;
            foreach(var work in projectionMatchWork.Values)if(index<work.SceneIndex)work.SceneIndex--;
            int count=projectionScene.Count;
            projectionMatchCursor=count==0?0:projectionMatchCursor%count;
            projectionMatchAdmissionCursor=count==0?0:projectionMatchAdmissionCursor%count;
        }
        private long ProjectionMatchSourceBytes(bool reservations)
        {
            long bytes=0;
            foreach(var work in projectionMatchWork.Values)
            {
                bytes+=256L+work.Parts.Capacity*8L+work.Requested.Count*32L;
                if(work.Pending)bytes+=reservations?BlueprintProjectionSourceReceipt.MaximumEstimatedBytes:0;
                // Parts and ObjectTargets can refer to the same window. Charge
                // each retained immutable allocation once, not once per alias.
                for(int i=0;i<work.Parts.Count;i++)
                {
                    var part=work.Parts[i];if(part==null)continue;bytes+=128;
                    bool seen=false;for(int j=0;j<i;j++)if(ReferenceEquals(work.Parts[j]?.Window,part.Window)){seen=true;break;}
                    if(!seen)bytes+=part.WindowEstimatedBytes;
                }
            }
            return bytes;
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void ObserveProjectionMatchRetained()
        {
#if DEBUG
            long source=ProjectionMatchSourceBytes(false),reserved=ProjectionMatchSourceBytes(true),samples=0;
            foreach(var work in projectionMatchWork.Values)
            {
                var read=work.Read;if(read==null)continue;
                // Results.Clear retains its dictionary backing store. Its
                // maximum entries are this token's 256 source steps, even
                // when the live count has returned to zero after publication.
                samples+=1024L+256L*96L+read.ObjectSamples.Capacity*128L+(read.ObjectUnion?.Capacity??0)*128L+read.Objects.Count*48L;
                // Sample values retain interpreted metadata objects and their
                // private arrays; report them as well as list backing storage.
                foreach(var sample in read.ObjectSamples)samples+=sample.Object?.EstimatedBytes??0;
                if(read.ObjectUnion!=null)for(int i=0;i<read.ObjectUnion.Count;i++)
                {
                    int x=read.ObjectTarget.AnchorX-1+i%3,y=read.ObjectTarget.AnchorY+i/3;
                    if(!read.ObjectBounds.Contains(x,y))samples+=read.ObjectUnion[i].Object?.EstimatedBytes??0;
                }
                samples+=read.ObjectTarget?.EstimatedBytes??0;
                var frame=read.WriteWitness as BlueprintWorldObjectFrameWitness;
                samples+=frame!=null?frame.EstimatedBytes:read.WriteWitness==null?0:512;
            }
            ProjectionMatchSourceBytesPeak=Math.Max(ProjectionMatchSourceBytesPeak,source);
            ProjectionMatchReservedBytesPeak=Math.Max(ProjectionMatchReservedBytesPeak,reserved);
            ProjectionMatchRetainedBytesPeak=Math.Max(ProjectionMatchRetainedBytesPeak,source+samples);
#endif
        }
        // Moving candidate and its scene restore share one worker identity.
        // Fatal ownership loss must release the scene-wide slot by identity,
        // even when the UI candidate is a different placement reference.
        private void ReleaseProjectionMatchIdentity(BlueprintProjectionIdentity identity)
        {
            if (identity == null) return;
            ReleaseProjectionMatchWork(identity);
            if (projectionMatchPriority?.Identity.Equals(identity) == true) projectionMatchPriority = null;
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
            BlueprintProjectionMatchWork work;
            if(projectionMatchWork.TryGetValue(item.Identity,out work) && ReferenceEquals(work.Owner,item))ReleaseProjectionMatchWork(item.Identity);
            if (item.Match != null) foreach (var group in item.Match.Groups) { projectionChanges.Unwatch(group.Watch); projectionMatchWatches.Remove(group.Watch); }
            item.Match = null; item.Renderer.SetMatchView(null);
            if (ReferenceEquals(projectionMatchPriority, item)) projectionMatchPriority = null;
        }
        private void InvalidateProjectionMatch(BlueprintProjectionMatchRange range)
        {
            var state = range.Owner.Match; if (state == null) return;
            range.Version++;range.RawVersion++;
            if (range.Current) { state.Current--; state.Targets -= range.Targets; state.Matches -= range.Matches; state.Unknown -= range.Unknown; }
            range.Current = range.Reading = false; range.Offset = 0;
            if(range.Visual!=null) { foreach (var pair in range.Visual) RemoveProjectionMatchVisual(state,pair.Key,pair.Value.TargetBits); range.Visual=null; }
            // A file request has no world samples to invalidate. Let that
            // immutable source arrive under its original request identity.
            // Discard only this world's attempt. A transported sibling and
            // this still-valid immutable source survive the local invalidation.
            ResetProjectionMatchRead(range);
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
                // samples are a different fact. Only an actual sampling
                // attempt is discarded; immutable source may still arrive.
                range.RawVersion++;range.Reading=false;range.Offset=0;
                ResetProjectionMatchRead(range);
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
            // A transport can contain several independent logical proofs.
            // Ready parts continue in this same Update, under the one token's
            // world/source and scheduling limits. Waiting for I/O never spins.
            int reads = 256, sourceSteps = 256, schedules = ProjectionMatchScheduleLimit;
            int admissions = ProjectionMatchWorkLimit, admissionAttempts=ProjectionMatchScheduleLimit;
            AdmitProjectionMatchSources(ref admissions,ref admissionAttempts);
            while(reads>0 && sourceSteps>0 && schedules-->0)
            {
                if(!StartProjectionMatchRead())break;
                var read=projectionMatchRead;int beforeReads=reads,beforeSteps=sourceSteps;
                AdvanceProjectionMatchRead(ref reads, ref sourceSteps);
                if (ReferenceEquals(projectionMatchRead, read)) PublishProjectionMatchReadResults(read);
                ObserveProjectionMatchRetained();
                if(reads==beforeReads && sourceSteps==beforeSteps && ReferenceEquals(projectionMatchRead,read))break;
            }
            foreach (var item in projectionScene) PublishProjectionMatchView(item);
            ApplyCompletedProjections();
            AdmitProjectionMatchSources(ref admissions,ref admissionAttempts);
            ObserveProjectionMatchRetained();
        }
        private void AdmitProjectionMatchSources(ref int admissions,ref int admissionAttempts)
        {
            int count=projectionScene.Count;
            if(count==0 || admissions==0 || admissionAttempts==0)return;
            // Admission has its own cursor. A pending request cannot consume
            // a normal world-sampling turn, nor stop another ready identity.
            int cursor=projectionMatchAdmissionCursor%count,sceneScanned=0;
            bool ordinaryAdmitted=false;
            int priorityIndex=projectionMatchAdmissionPriorityTurn && projectionMatchPriority!=null?projectionScene.IndexOf(projectionMatchPriority):-1;
            int attempts=Math.Min(count,admissionAttempts-(priorityIndex>=0?1:0));
            for(int n=priorityIndex>=0?-1:0;n<attempts && admissions>0;n++)
            {
                admissionAttempts--;
#if DEBUG
                ProjectionMatchAdmissionAttempts++;
#endif
                bool prioritized=n<0;
                int index=prioritized?priorityIndex:(cursor+n)%count;
                if(!prioritized)sceneScanned++;
                var item=projectionScene[index];var state=item.Match;
                if(state==null || state.Ranges.Length==0 || projectionMatchWork.ContainsKey(item.Identity) || state.Current==state.Ranges.Length && state.Demands==0)continue;
                if(projectionMatchWork.Count>=ProjectionMatchWorkLimit && !YieldRestartedProjectionMatchWork())continue;
                if(ProjectionMatchSourceBytes(true)+BlueprintProjectionSourceReceipt.MaximumEstimatedBytes+ProjectionMatchWorkOverhead>ProjectionMatchSourceLimit)continue;
                var demands=new List<BlueprintProjectionSourceDemand>();int group=-1,scanned=0;
                // At most one physical source locality and 64 logical parts.
                // A long current prefix is advanced over finite later Updates.
                while(scanned<Math.Min(state.Ranges.Length,BlueprintProjectionSourceReceipt.MaximumPartCount))
                {
                    int ordinal=(state.Cursor+scanned)%state.Ranges.Length;var range=state.Ranges[ordinal];
                    if(group>=0 && range.Source.ObservationGroup!=group)break;
                    scanned++;
                    if(range.Current && !range.Demand)continue;
                    if(group<0)group=range.Source.ObservationGroup;
                    demands.Add(new BlueprintProjectionSourceDemand(ordinal,offset:range.Offset));
                }
                state.Cursor=(state.Cursor+scanned)%state.Ranges.Length;
                if(demands.Count==0)continue;
                var work=new BlueprintProjectionMatchWork{Owner=item,SceneIndex=index,Token=++projectionMatchToken,Pending=true};
                if(!Library.RequestProjectionSources(item.Identity,work.Token,demands))continue;
                foreach(var demand in demands)work.Requested.Add(demand.Range);
                projectionMatchWork.Add(item.Identity,work);admissions--;
                // A capacity scan can inspect a whole scene while accepting
                // only one request. Advancing at every attempt would wrap to
                // the same first owner and refill it until its entire template
                // finished. Preserve the actual normal admission's successor.
                // Priority also needs source admission, before it has a work
                // slot; it alternates with normal admissions without spending
                // that ordinary turn or evicting stable partial samples.
                if(prioritized)projectionMatchAdmissionPriorityTurn=false;
                else {projectionMatchAdmissionCursor=(index+1)%count;projectionMatchAdmissionPriorityTurn=true;ordinaryAdmitted=true;}
#if DEBUG
                ProjectionMatchSourceRequests++;state.SourceRequests++;
#endif
            }
            // The scene has no fixed count cap. If a finite prefix is already
            // current (or unavailable), resume after that actual prefix next
            // token instead of examining the same first 64 identities forever.
            if(!ordinaryAdmitted && sceneScanned>0)projectionMatchAdmissionCursor=(cursor+sceneScanned)%count;
        }
        private bool StartProjectionMatchRead()
        {
            projectionMatchRead=null;
            // The observer owns one unproven 3x3 door seed. Finish that bounded
            // continuation (at most ten reads in total) before opening another
            // seed; yielding a large ordinary range still remains preemptible.
            foreach(var work in projectionMatchWork.Values)
            {
                var continuation=work.Read;
                if(continuation?.WriteSeed==null)continue;
                if(!ProjectionMatchReadCurrent(continuation.Range,continuation.Range.ProgressStamp)){ResetProjectionMatchRead(continuation.Range);break;}
                projectionMatchRead=continuation;return true;
            }
            int count = projectionScene.Count; if (count == 0) return false;
            // Inspect only the finite work slots, rather than walking every
            // waiting scene instance for each little object. Scene mutations
            // update these derived indices together with the normal cursor.
            int choices=projectionMatchWork.Count;projectionMatchWork.Values.CopyTo(projectionMatchChoices,0);
            BlueprintProjectionMatchWork selected=null,ordinary=null,priority=null;int selectedPart=-1,ordinaryPart=-1,priorityPart=-1,best=count;
            try
            {
                for(int w=0;w<choices;w++)
                {
                    var work=projectionMatchChoices[w];int part;
                    if(work.SceneIndex<0 || work.SceneIndex>=count || !ReferenceEquals(projectionScene[work.SceneIndex],work.Owner))continue;
                    if(!ReadyProjectionMatchPart(work,out part))
                    {if(!work.Pending && !HasProjectionMatchParts(work))projectionMatchWork.Remove(work.Owner.Identity);continue;}
                    if(ReferenceEquals(work.Owner,projectionMatchPriority)){priority=work;priorityPart=part;}
                    int distance=work.SceneIndex>=projectionMatchCursor?work.SceneIndex-projectionMatchCursor:count-projectionMatchCursor+work.SceneIndex;
                    if(distance<best){ordinary=work;ordinaryPart=part;best=distance;}
                }
                bool prioritized=projectionMatchPriorityTurn && priority!=null;
                selected=prioritized?priority:ordinary;selectedPart=prioritized?priorityPart:ordinaryPart;
                if(selected==null)return false;
                var read=selected.Read;
                if(read==null)
                {
                    var part=selected.Parts[selectedPart];var range=selected.Owner.Match.Ranges[part.Range];
                    read=new BlueprintProjectionMatchRead{Owner=selected.Owner,Range=range,Token=selected.Token,Batch=part,SourcePart=selectedPart};
                    if(range.Source.ObjectId>0){read.ObjectId=range.Source.ObjectId;read.ObjectTargets=part.Window;}
                    selected.PartCursor=(selectedPart+1)%selected.Parts.Count;
                }
                selected.Read=projectionMatchRead=read;
                if(prioritized)projectionMatchPriorityTurn=false;
                else {projectionMatchCursor=(selected.SceneIndex+1)%count;projectionMatchPriorityTurn=true;}
#if DEBUG
                ProjectionMatchServices++;if(prioritized)ProjectionMatchPriorityServices++;else ProjectionMatchOrdinaryServices++;
#endif
                return true;
            }
            finally{Array.Clear(projectionMatchChoices,0,projectionMatchChoices.Length);}
        }
        private bool ReadyProjectionMatchPart(BlueprintProjectionMatchWork work,out int partIndex)
        {
            partIndex=-1;
            if(work.Read!=null)
            {
                var range=work.Read.Range;partIndex=work.Read.SourcePart;
                return range.Group.Ready && projectionChanges.IsCurrent(range.Watch,range.Group.NativeStamp);
            }
            for(int n=0;n<work.Parts.Count;n++)
            {
                int p=(work.PartCursor+n)%work.Parts.Count;var part=work.Parts[p];if(part==null)continue;
                var range=work.Owner.Match.Ranges[part.Range];
                if(range.Current && !range.Demand){work.Parts[p]=null;continue;}
                if(!range.Group.Ready || !projectionChanges.IsCurrent(range.Watch,range.Group.NativeStamp))continue;
                partIndex=p;return true;
            }
            return false;
        }
        private void TakeProjectionMatchSources()
        {
            BlueprintProjectionSourceReceipt receipt;int remaining=ProjectionMatchScheduleLimit;
            while (remaining-->0 && Library.TryTakeProjectionSource(out receipt))
            {
                BlueprintProjectionMatchWork work;
                if(!projectionMatchWork.TryGetValue(receipt.Identity,out work) || !work.Pending || receipt.Generation!=work.Token || !projectionScene.Contains(work.Owner) || work.Owner.Match==null)continue;
                work.Pending=false;
                foreach(var part in receipt.Parts)
                {
                    if(part.ObjectId!=0 || !work.Requested.Contains(part.Range) || part.Range<0 || part.Range>=work.Owner.Match.Ranges.Length || part.Window==null || !part.Window.Identity.Equals(work.Owner.Identity) || part.Window.WindowGeneration!=work.Token)continue;
                    var range=work.Owner.Match.Ranges[part.Range];
                    if(part.LogicalObjectId!=range.Source.ObjectId || part.Offset!=range.Offset)continue;
                    work.Parts.Add(part);
                }
                if(!HasProjectionMatchParts(work))projectionMatchWork.Remove(receipt.Identity);
#if DEBUG
                ProjectionMatchSourceBatches++;work.Owner.Match.SourceArrivals++;
#endif
            }
            ObserveProjectionMatchRetained();
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
        private void AdvanceProjectionMatchRead(ref int reads, ref int sourceSteps)
        {
            var read = projectionMatchRead;
            if (read.Batch == null) return;
            if (!read.Sampling)
            {
                if (!read.Range.Group.Ready || !projectionChanges.IsCurrent(read.Range.Watch, read.Range.Group.NativeStamp)) { ResetProjectionMatchRead(read.Range); return; }
                // Request time is not sample time: bind raw qualification only
                // when immutable targets are ready for actual world sampling.
                read.Sampling = read.Range.Reading = true; read.Range.ProgressStamp = read.Range.RawVersion;
                read.Range.ProgressTargets = read.Range.ProgressMatches = read.Range.ProgressUnknown = 0;
            }
            if (!ProjectionMatchReadCurrent(read.Range,read.Range.ProgressStamp)) { InvalidateProjectionMatch(read.Range); return; }
            while (reads > 0 && sourceSteps > 0 && read.CellIndex < read.Batch.Window.Cells.Count)
            {
                sourceSteps--;
#if DEBUG
                ProjectionMatchSourceSteps++;
#endif
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
                {
                    // The complete object decides Tile semantics once; each
                    // effective member is then counted by the bounded source
                    // cursor, with no second world read or unbounded fan-out.
                    result = new BlueprintWorldMatchResult(1, objectState == BlueprintWorldMatchState.Match ? (ushort)1 : (ushort)0,
                        objectState == BlueprintWorldMatchState.Conflict ? (ushort)1 : (ushort)0, objectState == BlueprintWorldMatchState.Unknown ? (ushort)1 : (ushort)0);
                }
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
            PublishProjectionMatchReadResults(read);
            range.Offset = read.Batch.NextOffset;
            if (read.Batch.Complete)
            {
                if (range.Current) { state.Targets -= range.Targets; state.Matches -= range.Matches; state.Unknown -= range.Unknown; } else state.Current++;
                range.Current = true; range.Reading = false; range.Offset = 0; range.Stamp = range.Version;
                range.Targets = range.ProgressTargets; range.Matches = range.ProgressMatches; range.Unknown = range.ProgressUnknown;
                state.Targets += range.Targets; state.Matches += range.Matches; state.Unknown += range.Unknown;
                if(range.Demand){range.Demand=false;state.Demands--;}
#if DEBUG
                if(range.Source.ObjectId>0)ProjectionMatchCompletedObjects++;
#endif
                if(read.WriteWitness!=null && range.Targets>0 && range.Matches==range.Targets && range.Unknown==0)
                    projectionChanges.TrySetWitness(range.Watch,range.Group.NativeStamp,range.PartIndex,read.WriteWitness);
            }
            ReleaseProjectionMatchRead(false);
        }
        private void PublishProjectionMatchReadResults(BlueprintProjectionMatchRead read)
        {
            if (!read.Sampling || !ProjectionMatchReadCurrent(read.Range, read.Range.ProgressStamp)) return;
            var range = read.Range; var state = read.Owner.Match;
            // Drain only this Update's bounded results. Even an unusually
            // large complete object cannot accumulate a full second member
            // dictionary and publish it in one unbounded retirement fan-out.
            foreach (var pair in read.Results) if (ProjectionMatchCellResident(read.Owner, (int)(uint)pair.Key, (int)(pair.Key >> 32)))
            { if(range.Visual==null)range.Visual=new Dictionary<long,BlueprintWorldMatchResult>();BlueprintWorldMatchResult old;if(range.Visual.TryGetValue(pair.Key,out old))RemoveProjectionMatchVisual(state,pair.Key,old.TargetBits);range.Visual[pair.Key]=pair.Value;MergeProjectionMatchVisual(state,pair.Key,pair.Value); }
            read.Results.Clear();
        }
        private bool AdvanceProjectionMatchObject(BlueprintProjectionMatchRead read, ref int reads)
        {
            if (read.ObjectTargets == null) { FinishProjectionMatchObject(read, BlueprintWorldMatchState.Unknown); return true; }
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
            ObserveProjectionMatchRetained();
            FinishProjectionMatchObject(read, state); return true;
        }
        private static void FinishProjectionMatchObject(BlueprintProjectionMatchRead read, BlueprintWorldMatchState state)
        {
            // Keep only the complete structural result; bounded source cursor
            // consumption publishes its individual members and layer counts.
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
            foreach (var range in state.Ranges) if (MatchIntersects(range.Source.Bounds, bounds)) DemandProjectionMatchRange(range);
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
                foreach (var range in item.Match.Ranges) if (!range.Current || range.Unknown != 0) DemandProjectionMatchRange(range);
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
                if (ReferenceEquals(item, restoreProjection) || state == null || state.Ranges.Length == 0 ||
                    state.Current != state.Ranges.Length || state.Unknown != 0 || state.Targets <= 0 ||
                    state.Targets != item.TotalLayerCount - item.Mask.RemovedLayerCount || state.Matches != state.Targets) continue;
                var identity = item.Identity;
                var last = state.Ranges[state.Ranges.Length - 1];
                projectionChanges.TryRunStable(last.Watch, last.Group.NativeStamp, () =>
                {
                    if (!HasSession || identity.SessionGeneration != generation || !item.Identity.Equals(identity) || !projectionScene.Contains(item) || !ReferenceEquals(item.Match, state) || !ReferenceEquals(state.Mask, item.Mask) || state.OriginX != item.OriginX || state.OriginY != item.OriginY) return;
                    if (state.Current != state.Ranges.Length || state.Unknown != 0 || state.Targets <= 0 || state.Targets != item.TotalLayerCount - item.Mask.RemovedLayerCount || state.Matches != state.Targets) return;
                    // Complete proofs stay current via synchronous observer
                    // invalidation/witness maintenance. Recheck every scope
                    // under this one lock, rather than reading the template a
                    // mandatory second time or trusting only the last range.
                    foreach (var range in state.Ranges) if (!range.Current || !ProjectionMatchCurrent(range,range.Stamp)) return;
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
