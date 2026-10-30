using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal enum BlueprintProjectionMode { None, Following, Fixed, SelectingMove, SelectingRemove, Moving, Committing, SelectingMirror, MirrorPending, SelectingTrim, TrimReady, TrimDragging, TrimPreview }
    internal sealed class BlueprintProjectionPlacement
    {
        internal BlueprintProjectionIdentity Identity;
        internal CaptureSummary Summary;
        internal string Name;
        internal int OriginX, OriginY;
        internal int CoreSize = 128;
        internal long CommitOrder, TotalLayerCount, TotalConstructionLayerCount, WindowGeneration;
        internal IReadOnlyList<BlueprintProjectionSourceRange> SourceRanges;
        internal IReadOnlyList<BlueprintProjectionSourceRange> MatchRanges;
        internal BlueprintProjectionQualificationReceipt Qualification;
        internal BlueprintProjectionMask QualificationRequestedMask;
        internal BlueprintProjectionView QualificationRequestedView;
        internal long QualificationRequestedRevision=-1;
        internal BlueprintProjectionMatchState Match;
        internal BlueprintProjectionMask Mask = BlueprintProjectionMask.Empty;
        internal bool MirroredHorizontally;
        internal IBlueprintHorizontalMirrorProfile MirrorProfile;
        internal BlueprintProjectionView EffectiveView;
        internal BlueprintProjectionView View
        {
            get
            {
                if (Summary == null) return null;
                if (EffectiveView == null || EffectiveView.MirroredHorizontally != MirroredHorizontally ||
                    !ReferenceEquals(EffectiveView.Mask, Mask) || !ReferenceEquals(EffectiveView.Profile, MirrorProfile) ||
                    EffectiveView.Width != Summary.Width || EffectiveView.Height != Summary.Height)
                    EffectiveView = new BlueprintProjectionView(Summary, MirroredHorizontally, Mask, MirrorProfile);
                return EffectiveView;
            }
        }
        internal BlueprintProjectionWindow Window;
        internal readonly Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow> Windows = new Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow>();
        internal readonly Dictionary<BlueprintProjectionBounds, BlueprintProjectionDenial> Denied = new Dictionary<BlueprintProjectionBounds, BlueprintProjectionDenial>();
        internal readonly BlueprintProjectionRenderer Renderer = new BlueprintProjectionRenderer();
        internal BlueprintProjectionBounds RequestedWindow;
        internal BlueprintProjectionBounds RequestedCore, Coverage;
        internal bool HasRequestedWindow, Positioned, Opened, WindowPending, WindowBudgetBlocked;
        internal BlueprintProjectionPlacement Copy()
        {
            var copy = new BlueprintProjectionPlacement { Identity = Identity, Summary = Summary, Name = Name, OriginX = OriginX, OriginY = OriginY, CommitOrder = CommitOrder, TotalLayerCount = TotalLayerCount, TotalConstructionLayerCount = TotalConstructionLayerCount, Mask = Mask, Window = Window, Opened = Opened, Positioned = Positioned, Coverage = Coverage, CoreSize = CoreSize, SourceRanges = SourceRanges, MatchRanges = MatchRanges,
                Qualification=Qualification,QualificationRequestedMask=QualificationRequestedMask,QualificationRequestedRevision=QualificationRequestedRevision,QualificationRequestedView=QualificationRequestedView,
                MirroredHorizontally=MirroredHorizontally,MirrorProfile=MirrorProfile,EffectiveView=EffectiveView };
            foreach (var pair in Windows) copy.Windows.Add(pair.Key, pair.Value); return copy;
        }
        internal JueMingR.Features.Blueprint.BlueprintProjectionPlacement Snapshot()
        { return new JueMingR.Features.Blueprint.BlueprintProjectionPlacement(Identity, Summary, OriginX, OriginY, CommitOrder, Mask, TotalLayerCount, TotalConstructionLayerCount, MirroredHorizontally, MirrorProfile); }
    }
    internal sealed class BlueprintProjectionDenial
    {
        internal BlueprintProjectionMask Mask;
        internal long CapacityRevision, WindowBytes, CommandBytes;
    }
    internal sealed partial class HostBlueprint
    {
        // Host alone owns scene facts. Candidate movement never writes masks;
        // the worker computes an immutable delta against one exact revision.
        private readonly List<BlueprintProjectionPlacement> projectionScene = new List<BlueprintProjectionPlacement>();
        private readonly List<BlueprintProjectionPlacement> deletedProjections = new List<BlueprintProjectionPlacement>();
        // These are existing scene identities, not additional windows/readers.
        // An Open admission refusal is backpressure and must not erase a restore.
        private readonly Queue<BlueprintProjectionPlacement> projectionRestoreAdmissions = new Queue<BlueprintProjectionPlacement>();
        private readonly BlueprintProjectionRenderer emptyProjectionRenderer = new BlueprintProjectionRenderer();
        private BlueprintProjectionPlacement candidateProjection, pendingProjection, restoreProjection, hoveredProjection;
        private BlueprintProjectionEditKind projectionEditKind;
        private long projectionGeneration, projectionTransaction, projectionSceneRevision, projectionOrder, commitSceneRevision;
        private int projectionMouseX, projectionMouseY, projectionBaseX, projectionBaseY;
        private bool projectionNeedsNeutral = true, projectionLeft = true, projectionRight = true, projectionBaseline, deletePending;
        private Guid deletedAsset;
        private string projectionFeedback;
        private const long ProjectionSceneWindowBudget = 32L * 1024 * 1024, ProjectionSceneCommandBudget = 64L * 1024 * 1024;
        private int projectionWindowCursor, projectionWindowAdmissions;
        internal long ProjectionWindowBytes { get { long n = 0; foreach (var item in projectionScene) foreach (var w in item.Windows.Values) n += w.EstimatedBytes; if (candidateProjection != null) foreach (var w in candidateProjection.Windows.Values) if (!ProjectionOwnsWindow(restoreProjection, w)) n += w.EstimatedBytes; return n; } }
        internal long ProjectionCommandBytes { get { long n = 0; foreach (var item in projectionScene) n += item.Renderer.CommandCount * 80L; if (candidateProjection != null) n += candidateProjection.Renderer.CommandCount * 80L; return n; } }
        internal BlueprintProjectionMode ProjectionMode { get; private set; }
        internal BlueprintProjectionPlacement CurrentProjection { get { return candidateProjection ?? (projectionScene.Count == 0 ? null : projectionScene[projectionScene.Count - 1]); } }
        internal BlueprintProjectionRenderer ProjectionRenderer { get { return CurrentProjection?.Renderer ?? emptyProjectionRenderer; } }
        internal IReadOnlyList<BlueprintProjectionPlacement> ProjectionScene { get { return projectionScene; } }
        internal BlueprintProjectionPlacement ProjectionCandidate { get { return candidateProjection; } }
        internal BlueprintProjectionPlacement ProjectionMovingSource { get { return projectionEditKind == BlueprintProjectionEditKind.Move ? restoreProjection : null; } }
        internal bool ProjectionCandidateVisible { get { return projectionEditKind != BlueprintProjectionEditKind.Mirror && projectionEditKind != BlueprintProjectionEditKind.Trim; } }
        internal BlueprintProjectionPlacement ProjectionHover { get { return hoveredProjection; } }
        internal bool ProjectionVisible { get { return projectionScene.Count != 0 || candidateProjection?.Positioned == true; } }
        internal bool ProjectionEditing { get { return pendingProjection != null || ProjectionMode != BlueprintProjectionMode.None && ProjectionMode != BlueprintProjectionMode.Fixed; } }
        internal bool WorldPointerActive { get { return State.Active || ProjectionEditing; } }
        internal bool ProjectionCaptureBlocked { get { return State.Selection.HasSelection || discardPending || capture != null || Flow.Status == BlueprintCaptureStatus.Starting || Flow.Status == BlueprintCaptureStatus.Capturing || Flow.Status == BlueprintCaptureStatus.Sealing || Flow.Status == BlueprintCaptureStatus.DraftReady || Flow.Status == BlueprintCaptureStatus.Saving || !Flow.ContentHandle.IsEmpty && Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; } }
        internal bool CanPlaceProjection { get { return ControlsEnabled && Library != null && ProjectionPersistenceReady && !ProjectionCaptureBlocked && !ProjectionEditing && !deletePending; } }
        internal bool CanMoveProjection { get { return CanPlaceProjection && projectionScene.Count != 0; } }
        internal bool CanRemoveProjection { get { return CanPlaceProjection && projectionScene.Count != 0; } }
        internal bool OwnMoveCancellation { get { return ProjectionEditing && projectionEditKind == BlueprintProjectionEditKind.Move; } }
        internal bool OwnRemoveCancellation { get { return ProjectionEditing && projectionEditKind == BlueprintProjectionEditKind.Remove; } }
        internal bool OwnPlaceCancellation { get { return ProjectionEditing && projectionEditKind == BlueprintProjectionEditKind.Place; } }
        internal bool CanInvokeMove { get { return ControlsEnabled && (OwnMoveCancellation || CanMoveProjection); } }
        internal bool CanInvokeRemove { get { return ControlsEnabled && (OwnRemoveCancellation || CanRemoveProjection); } }
        internal bool CanInvokePlace { get { return ControlsEnabled && (OwnPlaceCancellation || CanPlaceProjection); } }
        internal string MoveDisabledReason { get { return CanInvokeMove ? null : ProjectionActionHint; } }
        internal string RemoveDisabledReason { get { return CanInvokeRemove ? null : ProjectionActionHint; } }
        internal string PlaceDisabledReason { get { return CanInvokePlace ? null : ProjectionActionHint; } }
        internal bool CanRemoveAllProjections { get { return ControlsEnabled && OwnRemoveCancellation; } }
        internal string RemoveAllDisabledReason { get { return CanRemoveAllProjections ? null : !ControlsEnabled ? "请先进入可用世界" : "请先进入移除蓝图模式"; } }
        internal string ProjectionActionHint { get { return !ControlsEnabled ? "请先进入可用世界" : Library == null ? "蓝图库暂不可用" : !ProjectionPersistenceReady ? "正在恢复当前世界蓝图" : ProjectionCaptureBlocked ? "请先保存或退出当前采集" : pendingProjection != null ? "正在读取蓝图，可按 Esc 或返回取消" : deletePending ? "正在删除蓝图模板" : ProjectionEditing ? "请先完成或取消当前蓝图操作" : projectionScene.Count == 0 ? "当前没有可选的世界蓝图" : null; } }
        internal int ProjectionReferenceCount(Guid id) { int n = 0; foreach (var p in projectionScene) if (p.Identity.AssetId == id) n++; return n; }
        internal bool ProjectionCandidateReferences(Guid id) { return candidateProjection?.Identity.AssetId == id || pendingProjection?.Identity.AssetId == id; }
        internal bool ProjectionReferences(Guid id) { return ProjectionReferenceCount(id) != 0 || ProjectionCandidateReferences(id); }
        internal bool Place(LibraryEntry entry)
        {
            // Check before the same-action cancellation branch as well: an
            // unsampled outer Update never queues or executes a UI command.
            if (!Input.CanStartActions) return false;
            if (OwnPlaceCancellation) { if (!CanInvokePlace) return false; CancelProjection(); return true; }
            if (entry == null || entry.IsProtected || !CanPlaceProjection) return false;
            Guid revision; if (!Guid.TryParseExact(entry.ContentRevision, "N", out revision)) return false;
            var next = new BlueprintProjectionPlacement { Identity = new BlueprintProjectionIdentity(entry.AssetId, revision, entry.ContentHash, generation, ++projectionGeneration), Name = entry.Name };
            if (!Library.RequestProjectionOpen(next.Identity)) return false;
            projectionTransaction++; projectionEditKind = BlueprintProjectionEditKind.Place; pendingProjection = next; if (State.Active) State.Exit();
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel(); projectionFeedback = null; return true;
        }
        internal void MoveProjection() { if (!CanInvokeMove || !Input.CanStartActions) return; if (OwnMoveCancellation) CancelProjection(); else BeginProjectionSelection(BlueprintProjectionMode.SelectingMove, "点击需要移动的蓝图"); }
        internal void RemoveProjection() { if (!CanInvokeRemove || !Input.CanStartActions) return; if (OwnRemoveCancellation) CancelProjection(); else BeginProjectionSelection(BlueprintProjectionMode.SelectingRemove, "点击需要移除的蓝图"); }
        internal void RemoveAllProjections()
        { if (!CanRemoveAllProjections || !Input.CanStartActions) return; ClearProjection(); PublishProjectionScene(); AcceptProjectionSceneSnapshot(); Pointer.Cancel(); Toolbar.Cancel(); }
        private void BeginProjectionSelection(BlueprintProjectionMode mode, string message)
        {
            if (!(mode == BlueprintProjectionMode.SelectingRemove ? CanRemoveProjection : CanMoveProjection) || !Input.CanStartActions) return;
            if (State.Active) State.Exit(); projectionTransaction++; projectionEditKind = mode == BlueprintProjectionMode.SelectingRemove ? BlueprintProjectionEditKind.Remove : BlueprintProjectionEditKind.Move; ProjectionMode = mode; projectionFeedback = message;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel(); Shell?.CloseAndSubmitPosition();
        }
        // Explicit navigation cancels Open; focus/UI suspension only retires
        // gestures. The separately accepted Save is never revoked here.
        internal void CancelProjectionOpen() { if (pendingProjection != null) CancelProjection(); }
        internal void CancelProjection()
        { EndProjectionEdit(true); }
        private void EndProjectionEdit(bool restoreSource)
        {
            if (!ProjectionEditing) return;
            if (OwnTrimCancellation) trimCanceledMapping = Input.Mapping;
            bool retainRollbackPoint = restoreSource && restoreProjection != null && ReferenceEquals(projectionPointTarget, restoreProjection);
            Library?.CancelProjectionCommit(generation, projectionTransaction);
            if (pendingProjection != null) Library?.ReleaseProjection(pendingProjection.Identity);
            if (candidateProjection != null && restoreProjection == null) Library?.ReleaseProjection(candidateProjection.Identity);
            if (restoreProjection != null) { Library?.ClearProjectionWindow(restoreProjection.Identity); restoreProjection.WindowPending = false; }
            if (candidateProjection?.Windows.Count > 0 || candidateProjection?.Renderer.CommandCount > 0) projectionCapacityRevision++;
            if (candidateProjection != null) DisposeProjectionData(candidateProjection); candidateProjection = pendingProjection = hoveredProjection = null;
            if (restoreSource && restoreProjection != null)
            {
                var alias = restoreProjection.Window;
                // Hidden source commands were retired to give Moving its full
                // budget. Recover from exact retained DTOs, without file reads;
                // restore keeps priority until its old scene is drawable again.
                RebuildProjectionMask(restoreProjection);
                if (alias != null && restoreProjection.Windows.ContainsValue(alias)) restoreProjection.Window = alias;
            }
            restoreProjection = null;
            ClearProjectionEditor(); projectionEditKind = BlueprintProjectionEditKind.None;
            // A point-loaded moving source is part of the exact rollback
            // snapshot, including its completed 16core. Cancellation keeps it.
            if (!retainRollbackPoint) ClearProjectionPoint();
            projectionTransaction++; ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
        }
        private void ClearProjection()
        {
            // Resource release is not a user deletion. The persistence owner
            // retains its last accepted committed scene across this cleanup.
            ClearProjectionPersistenceRead();
            projectionSceneDirty = false;
            ClearAutomaticBuild();
            projectionCapacityRevision++; ClearProjectionPoint();
            Library?.CloseProjection(); projectionTransaction++; projectionSceneRevision++; ClearProjectionEditor(); projectionEditKind = BlueprintProjectionEditKind.None;
            foreach (var item in projectionScene) DisposeProjectionData(item); projectionScene.Clear();
            ClearProjectionMatchWork(); projectionChanges.Clear(); projectionMatchWatches.Clear(); projectionMatchPriority = null; projectionMatchTickSet = false;
            projectionMatchSource = null; projectionMatchTiles = projectionMatchSections = null;
            if (candidateProjection != null) DisposeProjectionData(candidateProjection); candidateProjection = pendingProjection = restoreProjection = hoveredProjection = null;
            deletedProjections.Clear(); projectionRestoreAdmissions.Clear(); deletePending = false; ProjectionMode = BlueprintProjectionMode.None; emptyProjectionRenderer.Clear(); InterruptProjection(); ClearMaterials();
        }
        private void PumpProjection()
        {
            if (Library == null || !HasSession) return;
            for (int n = 0; n < 4 && projectionRestoreAdmissions.Count != 0; n++)
            {
                var restored = projectionRestoreAdmissions.Peek();
                if (!projectionScene.Contains(restored)) { projectionRestoreAdmissions.Dequeue(); continue; }
                if (!Library.RequestProjectionOpen(restored.Identity)) break;
                projectionRestoreAdmissions.Dequeue();
            }
            BlueprintProjectionReceipt receipt;
            while (Library.TryTakeProjectionReceipt(out receipt))
            {
                if (receipt.Identity == null || receipt.Identity.SessionGeneration != generation) continue;
                if (TakePersistedProjectionReceipt(receipt)) continue;
                var item = FindProjection(receipt.Identity); if (item == null) continue;
                if (receipt.Kind == BlueprintProjectionReceiptKind.Failed)
                {
                    if (receipt.IdentityRetired)
                    {
                        if (projectionScene.Exists(value => value.Identity.Equals(receipt.Identity))) ProtectProjectionPersistence(receipt.FailureCode ?? "blueprint-scene-read-failed");
                        // Moving has two Host objects for one worker identity.
                        // A revoked identity cannot roll back into an Opened
                        // restore or retain its scene-wide source execution slot.
                        RetireProjectionIdentity(receipt.Identity);
                        projectionFeedback = receipt.Error ?? "蓝图读取失败，库中原文件已保留。"; continue;
                    }
                    if (receipt.FailureCode == "blueprint-projection-window-budget")
                    {
                        if (!item.WindowPending || receipt.WindowGeneration != item.WindowGeneration) continue;
                        if (item.RequestedCore.Width == 16)
                        { item.WindowPending = false; DenyProjectionCore(item, item.RequestedCore, ProjectionSceneWindowBudget + 1, 0); continue; }
                        DropProjectionWindows(item);
                        if (item.CoreSize > 16) item.CoreSize /= 2; else item.WindowBudgetBlocked = true;
                        projectionFeedback = "当前蓝图内容较密，正在缩小读取窗口"; continue;
                    }
                    if (ReferenceEquals(item, pendingProjection) || ReferenceEquals(item, candidateProjection)) CancelProjection(); else { ProtectProjectionPersistence(receipt.FailureCode ?? "blueprint-scene-read-failed"); RetireProjection(item); } projectionFeedback = receipt.Error ?? "蓝图读取失败，库中原文件已保留。"; continue;
                }
                if (receipt.Kind == BlueprintProjectionReceiptKind.Opened)
                {
                    ReleaseProjectionMatch(item);item.Qualification=null;item.QualificationRequestedMask=null;item.QualificationRequestedRevision=-1;
                    item.Summary = receipt.Summary; item.Name = receipt.Name ?? item.Name; item.TotalLayerCount = receipt.TotalLayerCount; item.TotalConstructionLayerCount = receipt.TotalConstructionLayerCount; item.SourceRanges = receipt.SourceRanges; item.MatchRanges = receipt.MatchRanges; item.Opened = true;
                    if (restoreProjection?.Identity.Equals(receipt.Identity) == true)
                    {
                        // FindProjection selects the moving copy first. Keep
                        // source validation current, but reserve its command
                        // budget for the visible copy until actual cancellation.
                        restoreProjection.Opened = true;
                        ReleaseProjectionMatch(restoreProjection);restoreProjection.Qualification=null;restoreProjection.QualificationRequestedMask=null;restoreProjection.QualificationRequestedRevision=-1;
                        restoreProjection.SourceRanges = receipt.SourceRanges;
                        restoreProjection.MatchRanges = receipt.MatchRanges;
                    }
                    if (ReferenceEquals(item, pendingProjection)) { candidateProjection = item; pendingProjection = null; ProjectionMode = BlueprintProjectionMode.Following; InterruptProjection(); Shell?.CloseAndSubmitPosition(); }
                    else RebuildProjectionMask(item);
                }
                else if (receipt.Kind == BlueprintProjectionReceiptKind.Window && receipt.Window != null && item.WindowGeneration == receipt.WindowGeneration)
                {
                    if (!item.WindowPending) continue; item.WindowPending = false;
                    if (!item.Coverage.Contains(item.RequestedCore.Left, item.RequestedCore.Top) && !(editorPickPending && ReferenceEquals(projectionPointTarget,item) && item.RequestedCore.Equals(projectionPointCore))) continue;
                    AdmitProjectionWindow(item, item.RequestedCore, item.Mask.Apply(receipt.Window));
                    string error; if (item.Renderer.TryTakeError(out error)) ProjectionDrawFailed(item, error);
                }
            }
            BlueprintProjectionCommitReceipt commit;
            while (Library.TryTakeProjectionCommit(out commit))
            {
                if (TakeProjectionEditorReceipt(commit)) continue;
                var request = commit.Request;
                if (request.SessionGeneration != generation || request.TransactionId != projectionTransaction || ProjectionMode != BlueprintProjectionMode.Committing) continue;
                if (request.SceneRevision != projectionSceneRevision || request.SceneRevision != commitSceneRevision || candidateProjection == null)
                { CancelProjection(); projectionFeedback = "场景已变化，本次固定已取消，请重新操作。"; continue; }
                if (!commit.Succeeded) { CancelProjection(); projectionFeedback = commit.Error ?? "蓝图暂未固定，原场景已保留。"; continue; }
                if (restoreProjection != null)
                {
                    if (!TryRunProjectionMovable(restoreProjection, () => ApplyProjectionCommit(commit)))
                    { bool mirror = projectionEditKind == BlueprintProjectionEditKind.Mirror; CancelProjection(); projectionFeedback = mirror ? "原位置的建造情况已变化，本次镜像已取消。" : "原位置的建造情况已变化，本次移动已取消。"; }
                    continue;
                }
                ApplyProjectionCommit(commit);
            }
        }
        private void ApplyProjectionCommit(BlueprintProjectionCommitReceipt commit)
        {
                // One owner applies all masks/origin/order as one commit. Erased
                // layers are permanent: moving/removing their cover never revives them.
                foreach (var item in projectionScene) { BlueprintProjectionMask mask; if (commit.Masks.TryGetValue(item.Identity.ProjectionGeneration, out mask)) { item.Mask = mask; RebuildProjectionMask(item); } }
                foreach (long id in commit.RetiredInstanceIds) { var item = projectionScene.Find(p => p.Identity.ProjectionGeneration == id); if (item != null) RetireProjection(item, false); }
                if (restoreProjection != null)
                {
                    if (ReferenceEquals(projectionPointTarget, restoreProjection)) projectionPointTarget = candidateProjection;
                    RemoveProjectionSceneItem(restoreProjection); DisposeProjectionData(restoreProjection);
                }
                bool mirrored = projectionEditKind == BlueprintProjectionEditKind.Mirror;
                candidateProjection.CommitOrder = ++projectionOrder; projectionScene.Add(candidateProjection);
                if (mirrored) RebuildProjectionMask(candidateProjection);
                candidateProjection = restoreProjection = hoveredProjection = null; ClearProjectionEditor(); projectionEditKind = BlueprintProjectionEditKind.None;
                projectionSceneRevision++; ProjectionMode = BlueprintProjectionMode.Fixed; PublishProjectionScene(); InterruptProjection(); Pointer.Cancel();
                if (mirrored) projectionFeedback = "蓝图已水平镜像";
        }
        private BlueprintProjectionPlacement FindProjection(BlueprintProjectionIdentity id)
        { if (pendingProjection?.Identity.Equals(id) == true) return pendingProjection; if (candidateProjection?.Identity.Equals(id) == true) return candidateProjection; return projectionScene.Find(p => p.Identity.Equals(id)); }
        private void RetireProjectionIdentity(BlueprintProjectionIdentity identity)
        {
            ReleaseProjectionMatchIdentity(identity);
            var fixedItem = projectionScene.Find(p => p.Identity.Equals(identity));
            if (pendingProjection?.Identity.Equals(identity) == true || candidateProjection?.Identity.Equals(identity) == true || restoreProjection?.Identity.Equals(identity) == true)
            {
                // End the transaction before retirement, rejecting old commit
                // receipts. A dead source has no rollback drawing to rebuild;
                // ordinary user cancellation still restores its exact DTOs.
                EndProjectionEdit(false);
            }
            if (fixedItem != null) RetireProjection(fixedItem);
        }
        private void RetireProjection(BlueprintProjectionPlacement item, bool revision = true)
        {
            // An opponent's read/draw failure invalidates the whole pending
            // edit. Cancel before releasing its worker reference: cancellation
            // also restores the moving source and rejects any published delta.
            // Successful atomic application retires with revision=false.
            if (revision && (ProjectionMode == BlueprintProjectionMode.Committing || ReferenceEquals(editorTarget, item))) CancelProjection();
            RemoveProjectionSceneItem(item); DisposeProjectionData(item); Library?.ReleaseProjection(item.Identity);
            projectionCapacityRevision++; if (ReferenceEquals(projectionPointTarget, item)) ClearProjectionPoint();
            if (ReferenceEquals(hoveredProjection, item)) hoveredProjection = null;
            if (revision) projectionSceneRevision++;
            if (!ProjectionEditing) ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
        }
        private void DisposeProjectionData(BlueprintProjectionPlacement item)
        {
            RetireAutomaticBuild(item.Identity);
            // Delete rollback owns its separate Copy before this point. A dead
            // instance itself must release DTOs and the shared source index,
            // even if an old caller/receipt still temporarily holds its object.
            ReleaseProjectionMatch(item); item.Renderer.Dispose(); item.Window = null; item.Windows.Clear(); item.Denied.Clear(); item.SourceRanges = item.MatchRanges = null;
            item.Opened = item.WindowPending = item.HasRequestedWindow = false;
        }
        private void InterruptProjection() { InterruptProjectionGesture(false); }
        private void InterruptProjectionGesture(bool retainTrimConfirmation) { InterruptProjectionEditorGesture(retainTrimConfirmation); projectionNeedsNeutral = true; projectionLeft = projectionRight = true; projectionBaseline = false; hoveredProjection = null; }
        internal void ProjectionDrawFailed(string message) { if (CurrentProjection != null) ProjectionDrawFailed(CurrentProjection, message); }
        internal void ProjectionDrawFailed(BlueprintProjectionPlacement item, string message)
        { if (ReferenceEquals(item, candidateProjection)) CancelProjection(); else { ProtectProjectionPersistence("blueprint-scene-draw-failed"); RetireProjection(item); } projectionFeedback = message ?? "世界蓝图暂不可显示，库中原文件已保留。"; }
        internal void AcceptWorldPointer(bool trusted, bool left, bool right, int x, int y, int width, int height)
        { AcceptProjectionPointer(trusted, left, right, x, y, width, height, .5f, .5f); }
        internal void AcceptProjectionPointer(bool trusted, bool left, bool right, int x, int y, int width, int height, float subX, float subY)
        {
            if (State.Active) State.Pointer(trusted, left, right, x, y, width, height);
            if (!ProjectionEditing) return; if (!trusted) { InterruptProjection(); return; }
            if (candidateProjection != null && ProjectionMode == BlueprintProjectionMode.Following)
            { candidateProjection.OriginX = x - (candidateProjection.Summary.Width - 1) / 2; candidateProjection.OriginY = y - (candidateProjection.Summary.Height - 1) / 2; candidateProjection.Positioned = true; }
            else if (candidateProjection != null && ProjectionMode == BlueprintProjectionMode.Moving && projectionBaseline)
            { candidateProjection.OriginX = projectionBaseX + x - projectionMouseX; candidateProjection.OriginY = projectionBaseY + y - projectionMouseY; }
            if (projectionNeedsNeutral)
            {
                if (!left && !right && Input.KeyboardSample.GetPressedKeys().Length == 0) { projectionNeedsNeutral = false; projectionLeft = projectionRight = false; if (ProjectionMode == BlueprintProjectionMode.Moving && !projectionBaseline) SetProjectionBaseline(x, y); }
                return;
            }
            bool confirm = left && !projectionLeft, cancel = right && !projectionRight, released = !left && projectionLeft; projectionLeft = left; projectionRight = right;
            if (cancel) { CancelProjection(); return; }
            if (AcceptProjectionEditorPointer(left, x, y, subX, subY, confirm, released)) return;
            if (ProjectionMode == BlueprintProjectionMode.SelectingMove || ProjectionMode == BlueprintProjectionMode.SelectingRemove)
            {
                hoveredProjection = HitProjection(x, y, subX, subY, confirm); if (!confirm || hoveredProjection == null) return; var target = hoveredProjection;
                if (ProjectionMode == BlueprintProjectionMode.SelectingRemove) { RetireProjection(target); PublishProjectionScene(); AcceptProjectionSceneSnapshot(); ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed; InterruptProjection(); Pointer.Cancel(); return; }
                if (!CheckProjectionMove(target)) return;
                Library.ClearProjectionWindow(target.Identity); target.WindowPending = false;
                restoreProjection = target; candidateProjection = target.Copy();
                // Rollback owns immutable DTOs, not a second hidden full set
                // of commands. Otherwise one near64MiB instance blocks its own
                // Moving preview even though no other ordinary core can retire.
                if (target.Renderer.CommandCount != 0) { target.Renderer.Clear(); projectionCapacityRevision++; }
                RebuildProjectionMask(candidateProjection);
                ProjectionMode = BlueprintProjectionMode.Moving; InterruptProjection(); SetProjectionBaseline(x, y); return;
            }
            if (!confirm || candidateProjection == null || ProjectionMode == BlueprintProjectionMode.Committing) return; var current = candidateProjection;
            if (restoreProjection != null && !CheckProjectionMove(restoreProjection))
            {
                if (ProjectionMoveEligibility(restoreProjection) == BlueprintProjectionMoveEligibility.Matched) { string feedback = projectionFeedback; CancelProjection(); projectionFeedback = feedback; }
                return;
            }
            if (deletePending) { projectionFeedback = "正在删除模板，请稍后确认位置"; return; }
            if (!current.Opened) { projectionFeedback = "正在恢复蓝图，请稍后确认位置"; return; }
            if (current.OriginX < 0 || current.OriginY < 0 || (long)current.OriginX + current.Summary.Width > width || (long)current.OriginY + current.Summary.Height > height) { projectionFeedback = "蓝图超出世界边界，请移回世界内再固定。"; return; }
            var previous = new List<JueMingR.Features.Blueprint.BlueprintProjectionPlacement>();
            foreach (var old in projectionScene) if (!ReferenceEquals(old, restoreProjection) && Intersects(current, old))
            { if (!old.Opened) { projectionFeedback = "正在恢复蓝图，请稍后确认位置"; return; } previous.Add(old.Snapshot()); }
            commitSceneRevision = projectionSceneRevision;
            if (Library.RequestProjectionCommit(new BlueprintProjectionCommitRequest(generation, projectionTransaction, projectionSceneRevision, current.Snapshot(), previous, projectionEditKind))) ProjectionMode = BlueprintProjectionMode.Committing;
        }
        private void SetProjectionBaseline(int x, int y) { projectionMouseX = x; projectionMouseY = y; projectionBaseX = candidateProjection.OriginX; projectionBaseY = candidateProjection.OriginY; projectionBaseline = true; }
        private static bool Intersects(BlueprintProjectionPlacement a, BlueprintProjectionPlacement b) { return a.OriginX < (long)b.OriginX + b.Summary.Width && b.OriginX < (long)a.OriginX + a.Summary.Width && a.OriginY < (long)b.OriginY + b.Summary.Height && b.OriginY < (long)a.OriginY + a.Summary.Height; }
        private BlueprintProjectionPlacement HitProjection(int x, int y, float subX, float subY, bool explicitPoint = false)
        {
            for (int i = projectionScene.Count - 1; i >= 0; i--)
            {
                var item = projectionScene[i]; int lx = item.View.SourceX(x - item.OriginX), ly = y - item.OriginY; if (lx < 0 || ly < 0 || lx >= item.Summary.Width || ly >= item.Summary.Height) continue;
                // Unknown is not air. A bounded point request stops selection
                // from falling through a higher instance that is not loaded.
                BlueprintProjectionWindow loaded = null;
                foreach (var pair in item.Windows) if (pair.Key.Contains(lx, ly)) { loaded = pair.Value; break; }
                if (loaded == null) { if (explicitPoint) SetProjectionPoint(item, lx, ly); projectionFeedback = "正在读取目标位置，请稍后点击"; return null; }
                int low = 0, high = loaded.Cells.Count - 1;
                while (low <= high) { int mid = low + (high - low) / 2; var c = loaded.Cells[mid]; int compare = c.Y == ly ? c.X.CompareTo(lx) : c.Y.CompareTo(ly); if (compare < 0) { low = mid + 1; continue; } if (compare > 0) { high = mid - 1; continue; } if (HitCell(item.Mask.Apply(c), item.MirroredHorizontally ? 1f - subX : subX, subY)) return item; break; }
            }
            return null;
        }
        private static bool HitCell(CaptureCell c, float x, float y)
        { if ((c.Layers & ~CaptureLayer.Tile) != 0) return true; if ((c.Layers & CaptureLayer.Tile) == 0 || c.Slope == 0 && c.HalfBrick && y < .5f) return false; return c.Slope == 0 || c.Slope == 1 && y >= x || c.Slope == 2 && y >= 1 - x || c.Slope == 3 && y <= 1 - x || c.Slope == 4 && y <= x; }
        internal void UpdateProjectionWindow(Platform.WorldTargets.WorldTargetView view)
        {
            projectionWindowAdmissions = 0;
            UpdateProjectionPoint();
            if (candidateProjection?.Positioned == true) UpdateInstanceWindow(candidateProjection, view);
            int count = projectionScene.Count;
            for (int n = 0; n < count; n++) { var item = projectionScene[(projectionWindowCursor + n) % count]; if (!ReferenceEquals(item, restoreProjection)) UpdateInstanceWindow(item, view); }
            if (count != 0) projectionWindowCursor = (projectionWindowCursor + 1) % count;
        }
        private void UpdateInstanceWindow(BlueprintProjectionPlacement item, Platform.WorldTargets.WorldTargetView view)
        {
            if (!item.Opened || item.Summary == null) return; int halo = item.Renderer.RequiredHalo;
            if (editorPickPending && ReferenceEquals(item,projectionPointTarget)) return;
            int size = item.CoreSize;
            int left = Math.Max(-halo, view.X - item.OriginX - halo), top = Math.Max(-halo, view.Y - item.OriginY - halo), right = Math.Min(item.Summary.Width + halo, view.X + view.Width - item.OriginX + halo), bottom = Math.Min(item.Summary.Height + halo, view.Y + view.Height - item.OriginY + halo);
            if (left >= right || top >= bottom) { if (item.HasRequestedWindow || item.Denied.Count != 0) DropProjectionWindows(item); item.Coverage = default(BlueprintProjectionBounds); return; }
            if (item.MirroredHorizontally) { int sourceLeft = checked(item.Summary.Width - right); right = checked(item.Summary.Width - left); left = sourceLeft; }
            // Stable coverage and per-segment commands retain useful in-flight
            // intersections instead of invalidating everything per mouse tile.
            left = Math.Max(0, (int)Math.Floor(left / (double)size) * size); top = Math.Max(0, (int)Math.Floor(top / (double)size) * size);
            right = (int)Math.Ceiling(Math.Min(item.Summary.Width, right) / (double)size) * size; bottom = (int)Math.Ceiling(Math.Min(item.Summary.Height, bottom) / (double)size) * size;
            var coverage = new BlueprintProjectionBounds(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
            if (!coverage.Equals(item.Coverage))
            {
                item.Coverage = coverage; var remove = new List<BlueprintProjectionBounds>(); foreach (var pair in item.Windows) if (!coverage.Contains(pair.Key.Left, pair.Key.Top)) remove.Add(pair.Key);
                foreach (var key in remove) RemoveProjectionCore(item, key); item.Renderer.Trim(coverage); item.Denied.Clear();
                item.WindowBudgetBlocked = false;
            }
            if (item.WindowPending || item.WindowBudgetBlocked || projectionWindowAdmissions >= 4) return;
            for (int y = top; y < bottom; y += size) for (int x = left; x < right; x += size)
            { var core = new BlueprintProjectionBounds(x, y, size, size); if (item.Windows.ContainsKey(core) || ProjectionDenied(item, core)) continue; item.RequestedCore = core; RequestProjectionWindow(item, core.Expand(halo)); return; }
        }
        private void RequestProjectionWindow(BlueprintProjectionPlacement item, BlueprintProjectionBounds bounds)
        { long next = item.WindowGeneration + 1; if (Library.RequestProjectionWindow(item.Identity, next, bounds)) { item.WindowGeneration = next; item.RequestedWindow = bounds; item.HasRequestedWindow = item.WindowPending = true; projectionWindowAdmissions++; } }
        private void DropProjectionWindows(BlueprintProjectionPlacement item)
        {
            if (item.Windows.Count != 0 || item.Renderer.CommandCount != 0) projectionCapacityRevision++;
            Library.ClearProjectionWindow(item.Identity); item.WindowGeneration++; item.HasRequestedWindow = item.WindowPending = false; item.Windows.Clear(); item.Denied.Clear(); item.Window = null; item.Renderer.Clear();
            // Logical proofs may never have had visible cells. Drop the lazy
            // detail maps while retaining offscreen coverage summaries.
            if (item.Match != null) { foreach (var range in item.Match.Ranges) range.Visual=null; item.Match.Visible.Clear(); item.Match.ViewDirty = true; }
        }
        private void RebuildProjectionMask(BlueprintProjectionPlacement item)
        {
            item.Denied.Clear();
            // A new Copy/retired hidden renderer needs every retained core.
            // Freeze this before the loop: publishing its first core must not
            // make the remaining same-mask cores look already represented.
            bool rebuildAll = item.Renderer.CommandCount == 0;
            foreach (var core in new List<BlueprintProjectionBounds>(item.Windows.Keys))
            {
                var old = item.Windows[core]; var effective = item.Mask.Apply(old);
                if (ReferenceEquals(old, effective) && !rebuildAll) continue;
                // Retire the old commands before attempting a changed mask: an
                // admission refusal must never leave erased layers visible.
                RemoveProjectionCore(item, core); AdmitProjectionWindow(item, core, effective);
            }
        }
        internal bool DeleteAsset(LibraryEntry entry)
        {
            if (entry == null || Library == null || !ProjectionPersistenceReady || !Library.TryDelete(entry.AssetId, entry.MetadataIdentity)) return false;
            if (!ProjectionReferences(entry.AssetId)) return true; deletedAsset = entry.AssetId; deletePending = true; deletedProjections.Clear();
            if (ProjectionCandidateReferences(entry.AssetId) || restoreProjection?.Identity.AssetId == entry.AssetId || ProjectionMode == BlueprintProjectionMode.Committing) CancelProjection();
            for (int i = projectionScene.Count - 1; i >= 0; i--) if (projectionScene[i].Identity.AssetId == entry.AssetId) { var item = projectionScene[i]; deletedProjections.Add(item.Copy()); RetireProjection(item); }
            if (!ProjectionEditing) ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed; return true;
        }
        private void ApplyProjectionMutation(LibraryReceipt receipt)
        {
            if (receipt.Kind == LibraryCommandKind.Rename && receipt.Status == LibraryStatus.Saved && receipt.CommittedName != null)
            {
                // World names follow the reliable mutation receipt even when
                // the renamed asset is absent from the currently visible page.
                foreach (var item in projectionScene) if (item.Identity.AssetId == receipt.AssetId) item.Name = receipt.CommittedName;
                foreach (var item in deletedProjections) if (item.Identity.AssetId == receipt.AssetId) item.Name = receipt.CommittedName;
                foreach (var item in persistedProjectionRestore) if (item.Identity.AssetId == receipt.AssetId) item.Name = receipt.CommittedName;
                if (pendingProjection?.Identity.AssetId == receipt.AssetId) pendingProjection.Name = receipt.CommittedName;
                if (candidateProjection?.Identity.AssetId == receipt.AssetId) candidateProjection.Name = receipt.CommittedName;
                if (restoreProjection?.Identity.AssetId == receipt.AssetId) restoreProjection.Name = receipt.CommittedName;
                materialSceneIdentity = -1;
            }
            if (receipt.Kind != LibraryCommandKind.Delete || !deletePending || receipt.AssetId != deletedAsset) return; deletePending = false;
            // The receipt's mechanical certainty is narrower than Protected or
            // Failed. Only that proof may restore the exact old scene snapshot.
            if (receipt.DeleteProvenUncommitted)
            {
                // Resolve each successor before the first Add changes scene/count.
                var samplingNext = ProjectionMatchCursorOwner(projectionMatchCursor);
                var admissionNext = ProjectionMatchCursorOwner(projectionMatchAdmissionCursor);
                foreach (var item in deletedProjections)
                {
                    item.HasRequestedWindow = item.Windows.Count != 0; item.WindowPending = false; item.Opened = false;
                    projectionScene.Add(item); projectionRestoreAdmissions.Enqueue(item);
                }
                projectionScene.Sort((a, b) => a.CommitOrder.CompareTo(b.CommitOrder));
                ReconcileProjectionSceneOrder(samplingNext, admissionNext);
            }
            else if (receipt.Status != LibraryStatus.Deleted && receipt.Status != LibraryStatus.Completed)
            { ProtectProjectionPersistence("blueprint-scene-delete-unconfirmed"); projectionFeedback = receipt.Status == LibraryStatus.CommitUnconfirmed ? "无法确认删除结果，已停止读取这个模板，原文件已保护。" : "蓝图删除失败，原文件已保护。"; }
            deletedProjections.Clear(); projectionSceneRevision++;
            PublishProjectionScene();
            if (!ProjectionEditing) ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
        }
    }
}
