using System;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal enum BlueprintProjectionMode { None, Following, Fixed, Moving }
    internal sealed class BlueprintProjectionPlacement
    {
        internal BlueprintProjectionIdentity Identity;
        internal CaptureSummary Summary;
        internal int OriginX, OriginY;
        internal BlueprintProjectionPlacement Copy()
        { return new BlueprintProjectionPlacement { Identity = Identity, Summary = Summary, OriginX = OriginX, OriginY = OriginY }; }
    }

    internal sealed partial class HostBlueprint
    {
        internal readonly BlueprintProjectionRenderer ProjectionRenderer = new BlueprintProjectionRenderer();
        internal BlueprintProjectionPlacement CurrentProjection { get; private set; }
        internal BlueprintProjectionMode ProjectionMode { get; private set; }
        private BlueprintProjectionPlacement restoreProjection, pendingProjection, deletedProjection;
        private long projectionGeneration, projectionWindowGeneration;
        private BlueprintProjectionBounds requestedWindow;
        private bool hasRequestedWindow, projectionOpened, projectionPositioned, projectionNeedsNeutral = true;
        private bool projectionLeft, projectionRight, projectionBaseline;
        private int projectionMouseX, projectionMouseY, projectionBaseX, projectionBaseY;
        private bool resumeFixed, deletePending;
        private string projectionFeedback;
        private Guid deletedAsset;
        internal bool ProjectionVisible { get { return CurrentProjection != null && projectionPositioned && !deletePending; } }
        internal bool ProjectionEditing { get { return ProjectionMode == BlueprintProjectionMode.Following || ProjectionMode == BlueprintProjectionMode.Moving; } }
        internal bool WorldPointerActive { get { return State.Active || ProjectionEditing; } }
        // Selection/draft/in-flight capture owns its data until explicit save or
        // exit. Merely hiding F5 is never permission to destroy that ownership.
        internal bool ProjectionCaptureBlocked { get { return State.Selection.HasSelection || discardPending || capture != null ||
            Flow.Status == BlueprintCaptureStatus.Starting || Flow.Status == BlueprintCaptureStatus.Capturing ||
            Flow.Status == BlueprintCaptureStatus.Sealing || Flow.Status == BlueprintCaptureStatus.DraftReady || Flow.Status == BlueprintCaptureStatus.Saving ||
            !Flow.ContentHandle.IsEmpty && Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; } }
        internal bool CanPlaceProjection { get { return ControlsEnabled && Library != null && !ProjectionCaptureBlocked && !ProjectionEditing && pendingProjection == null && !deletePending; } }
        internal bool CanMoveProjection { get { return ControlsEnabled && CurrentProjection != null && !ProjectionCaptureBlocked && pendingProjection == null && !deletePending &&
            (ProjectionMode == BlueprintProjectionMode.Fixed || ProjectionMode == BlueprintProjectionMode.Moving); } }
        internal bool CanRemoveProjection { get { return ControlsEnabled && (CurrentProjection != null || pendingProjection != null) && !deletePending; } }
        internal string ProjectionActionHint { get { return ProjectionCaptureBlocked ? "请先保存或退出当前采集" : ProjectionEditing ? "请先完成或取消当前摆放" : pendingProjection != null ? "正在读取蓝图" : "当前没有世界蓝图"; } }
        internal bool ProjectionReferences(Guid id)
        { return CurrentProjection?.Identity.AssetId == id || pendingProjection?.Identity.AssetId == id || restoreProjection?.Identity.AssetId == id; }
        internal bool Place(LibraryEntry entry)
        {
            if (entry == null || entry.IsProtected || !CanPlaceProjection || !Input.CanStartActions) return false;
            Guid revision; if (!Guid.TryParseExact(entry.ContentRevision, "N", out revision)) return false;
            var next = new BlueprintProjectionPlacement { Identity = new BlueprintProjectionIdentity(entry.AssetId, revision, entry.ContentHash, generation, ++projectionGeneration) };
            if (!Library.RequestProjectionOpen(next.Identity)) return false;
            if (State.Active) State.Exit();
            pendingProjection = next; resumeFixed = false; Pointer.Cancel(); Toolbar.Cancel();
            projectionFeedback = null; return true;
        }
        internal void MoveProjection()
        {
            if (!CanMoveProjection || !Input.CanStartActions) return;
            if (ProjectionMode == BlueprintProjectionMode.Moving) { CancelProjection(); return; }
            if (State.Active) State.Exit();
            restoreProjection = CurrentProjection.Copy(); ProjectionMode = BlueprintProjectionMode.Moving;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel(); Shell?.CloseAndSubmitPosition();
        }
        internal void CancelProjection()
        {
            if (!ProjectionEditing && pendingProjection == null) return;
            if (ProjectionMode == BlueprintProjectionMode.Moving)
            {
                CurrentProjection.OriginX = restoreProjection.OriginX; CurrentProjection.OriginY = restoreProjection.OriginY;
                ProjectionMode = BlueprintProjectionMode.Fixed; restoreProjection = null;
            }
            else if (pendingProjection != null && !ProjectionEditing)
            { pendingProjection = null; ReopenCurrentProjection(); }
            else
            {
                CurrentProjection = restoreProjection; restoreProjection = null; pendingProjection = null;
                ProjectionMode = CurrentProjection == null ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
                ProjectionRenderer.Clear(); ReopenCurrentProjection();
            }
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
        }
        internal void RemoveProjection()
        {
            if (!CanRemoveProjection || !Input.CanStartActions) return;
            ClearProjection(); Pointer.Cancel(); Toolbar.Cancel();
        }
        private void ClearProjection()
        {
            Library?.CloseProjection(); projectionGeneration++;
            CurrentProjection = pendingProjection = restoreProjection = deletedProjection = null;
            ProjectionMode = BlueprintProjectionMode.None; deletePending = projectionOpened = projectionPositioned = hasRequestedWindow = false;
            ProjectionRenderer.Clear(); InterruptProjection();
        }
        private void ReopenCurrentProjection()
        {
            Library.CloseProjection(); projectionOpened = hasRequestedWindow = false;
            if (CurrentProjection == null) return;
            var old = CurrentProjection.Identity;
            CurrentProjection.Identity = new BlueprintProjectionIdentity(old.AssetId, old.ContentRevision, old.ContentHash, generation, ++projectionGeneration);
            pendingProjection = CurrentProjection; resumeFixed = true;
            if (!Library.RequestProjectionOpen(pendingProjection.Identity)) { pendingProjection = null; projectionFeedback = "蓝图暂不可读取，请从图库重新取出。"; }
        }
        private void PumpProjection()
        {
            // A temporarily unavailable local player/view is not a new session.
            // Leave the bounded receipt in its owner until the same session is
            // usable; OnSessionEnded/Close retires it on a real identity change.
            if (Library == null || !HasSession) return;
            BlueprintProjectionReceipt receipt;
            while (Library.TryTakeProjectionReceipt(out receipt))
            {
                if (receipt.Identity == null || receipt.Identity.SessionGeneration != generation) continue;
                if (pendingProjection != null && receipt.Identity.Equals(pendingProjection.Identity))
                {
                    if (receipt.Kind == BlueprintProjectionReceiptKind.Failed)
                    {
                        bool failedResume = resumeFixed; pendingProjection = null;
                        projectionFeedback = receipt.Error ?? "蓝图读取失败，原图已保留。";
                        if (!failedResume) ReopenCurrentProjection();
                        else { CurrentProjection = restoreProjection = null; ProjectionMode = BlueprintProjectionMode.None; ProjectionRenderer.Clear(); Library.CloseProjection(); }
                        continue;
                    }
                    if (receipt.Kind == BlueprintProjectionReceiptKind.Opened)
                    {
                        bool fixedResume = resumeFixed;
                        if (!fixedResume) restoreProjection = CurrentProjection?.Copy();
                        CurrentProjection = pendingProjection; pendingProjection = null; CurrentProjection.Summary = receipt.Summary;
                        projectionOpened = true; hasRequestedWindow = false;
                        ProjectionMode = fixedResume ? BlueprintProjectionMode.Fixed : BlueprintProjectionMode.Following;
                        projectionPositioned = fixedResume; InterruptProjection();
                        if (!fixedResume) { ProjectionRenderer.Clear(); Shell?.CloseAndSubmitPosition(); }
                        continue;
                    }
                }
                if (CurrentProjection == null || !CurrentProjection.Identity.Equals(receipt.Identity) || deletePending) continue;
                if (receipt.Kind == BlueprintProjectionReceiptKind.Window && receipt.WindowGeneration == projectionWindowGeneration && receipt.Window != null)
                {
                    ProjectionRenderer.SetWindow(receipt.Window);
                    string error; if (ProjectionRenderer.TryTakeError(out error)) ProjectionDrawFailed(error);
                }
                else if (receipt.Kind == BlueprintProjectionReceiptKind.Failed)
                { ProjectionDrawFailed(receipt.Error ?? "这部分蓝图暂不可读取。"); }
            }
        }
        private void InterruptProjection()
        { projectionNeedsNeutral = true; projectionLeft = projectionRight = true; projectionBaseline = false; }
        internal void ProjectionDrawFailed(string message)
        {
            var restore = ProjectionMode == BlueprintProjectionMode.Following ? restoreProjection : null;
            ClearProjection();
            if (restore != null) { CurrentProjection = restore; ProjectionMode = BlueprintProjectionMode.Fixed; projectionPositioned = true; ReopenCurrentProjection(); }
            projectionFeedback = message ?? "世界蓝图暂不可显示，库中原文件已保留。";
        }
        internal void AcceptWorldPointer(bool trusted, bool left, bool right, int x, int y, int width, int height)
        {
            if (State.Active) State.Pointer(trusted, left, right, x, y, width, height);
            if (!ProjectionEditing) return;
            if (!trusted) { InterruptProjection(); return; }
            if (ProjectionMode == BlueprintProjectionMode.Following)
            {
                CurrentProjection.OriginX = x - (CurrentProjection.Summary.Width - 1) / 2;
                CurrentProjection.OriginY = y - (CurrentProjection.Summary.Height - 1) / 2; projectionPositioned = true;
            }
            else if (!projectionNeedsNeutral)
            {
                if (!projectionBaseline)
                { projectionMouseX = x; projectionMouseY = y; projectionBaseX = CurrentProjection.OriginX; projectionBaseY = CurrentProjection.OriginY; projectionBaseline = true; }
                CurrentProjection.OriginX = projectionBaseX + x - projectionMouseX;
                CurrentProjection.OriginY = projectionBaseY + y - projectionMouseY;
            }
            if (projectionNeedsNeutral)
            {
                if (!left && !right && Input.KeyboardSample.GetPressedKeys().Length == 0) { projectionNeedsNeutral = false; projectionLeft = projectionRight = false; }
                if (!projectionNeedsNeutral && ProjectionMode == BlueprintProjectionMode.Moving)
                { projectionMouseX = x; projectionMouseY = y; projectionBaseX = CurrentProjection.OriginX; projectionBaseY = CurrentProjection.OriginY; projectionBaseline = true; }
                return;
            }
            bool confirm = left && !projectionLeft, cancel = right && !projectionRight;
            projectionLeft = left; projectionRight = right;
            if (cancel) { CancelProjection(); return; }
            if (!confirm) return;
            var current = CurrentProjection;
            if (current.OriginX < 0 || current.OriginY < 0 || (long)current.OriginX + current.Summary.Width > width || (long)current.OriginY + current.Summary.Height > height)
            { projectionFeedback = "蓝图超出世界边界，请移回世界内再固定。"; return; }
            ProjectionMode = BlueprintProjectionMode.Fixed; restoreProjection = null; InterruptProjection(); Pointer.Cancel();
        }
        internal void UpdateProjectionWindow(Platform.WorldTargets.WorldTargetView view)
        {
            var current = CurrentProjection;
            if (current == null || !projectionOpened || !projectionPositioned || deletePending) return;
            int halo = ProjectionRenderer.RequiredHalo;
            int left = Math.Max(-halo, view.X - current.OriginX - halo), top = Math.Max(-halo, view.Y - current.OriginY - halo);
            int right = Math.Min(current.Summary.Width + halo, view.X + view.Width - current.OriginX + halo);
            int bottom = Math.Min(current.Summary.Height + halo, view.Y + view.Height - current.OriginY + halo);
            var bounds = new BlueprintProjectionBounds(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
            if (hasRequestedWindow && requestedWindow.Equals(bounds)) return;
            long next = projectionWindowGeneration + 1;
            if (Library.RequestProjectionWindow(current.Identity, next, bounds))
            { projectionWindowGeneration = next; requestedWindow = bounds; hasRequestedWindow = true; }
        }
        internal bool DeleteAsset(LibraryEntry entry)
        {
            if (entry == null || !Library.TryDelete(entry.AssetId, entry.MetadataIdentity)) return false;
            if (ProjectionReferences(entry.AssetId))
            {
                // Deleting the rollback asset also retires a different current
                // replacement. Close that reader too; Delete itself only knows
                // the asset ID being deleted and cannot release its sibling.
                Library.CloseProjection();
                // Worker closes the pinned index before Delete. Retain only a
                // restorable fixed transaction for a proven precommit failure.
                deletedProjection = ProjectionMode == BlueprintProjectionMode.Moving ? restoreProjection?.Copy() : ProjectionMode == BlueprintProjectionMode.Fixed ? CurrentProjection?.Copy() : restoreProjection?.Copy();
                deletedAsset = entry.AssetId; deletePending = true; pendingProjection = restoreProjection = null;
                projectionOpened = false; ProjectionRenderer.Clear(); ProjectionMode = BlueprintProjectionMode.None; CurrentProjection = null; InterruptProjection();
            }
            return true;
        }
        private void ApplyProjectionMutation(LibraryReceipt receipt)
        {
            if (receipt.Kind == LibraryCommandKind.Rename && receipt.Status == LibraryStatus.Saved)
            {
                // Metadata rename has no immutable-content or origin identity.
                // List refresh supplies the visible name; no reopen is needed.
                return;
            }
            if (receipt.Kind != LibraryCommandKind.Delete || !deletePending || receipt.AssetId != deletedAsset) return;
            deletePending = false;
            if (deletedProjection != null && (deletedProjection.Identity.AssetId != receipt.AssetId || receipt.Status == LibraryStatus.Failed))
            { CurrentProjection = deletedProjection; ProjectionMode = BlueprintProjectionMode.Fixed; projectionPositioned = true; ReopenCurrentProjection(); }
            else if (receipt.Status != LibraryStatus.Deleted && receipt.Status != LibraryStatus.Completed)
                projectionFeedback = receipt.Status == LibraryStatus.CommitUnconfirmed ? "无法确认删除结果，已停止读取这个蓝图，原文件已保护。" : "蓝图删除失败，原文件已保护。";
            deletedProjection = null;
        }
    }
}
