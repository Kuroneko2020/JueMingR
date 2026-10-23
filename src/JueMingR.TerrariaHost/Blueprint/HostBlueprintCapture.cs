using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        internal LibraryState Library { get; }
        internal BlueprintCaptureFlow Flow { get; } = new BlueprintCaptureFlow();
        private CaptureJob capture;
        private bool discardPending;
        private long captureCommandId;
        private ulong latestTick;
        internal bool CanSave { get { return SaveDisabledReason == null; } }
        internal string SaveDisabledReason
        {
            get {
                if (!ControlsEnabled) return "蓝图当前不可用";
                if (!Input.CanStartActions) return "请先结束当前输入";
                if (Library == null) return "蓝图库暂不可用";
                if (Flow.Status == BlueprintCaptureStatus.Saving) return "正在保存蓝图，请稍候";
                if (Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed && !Flow.ContentHandle.IsEmpty) return "无法确认提交结果，相关文件已保护";
                if (discardPending) return "正在清理采集草稿，请稍候";
                if (Flow.Status == BlueprintCaptureStatus.Starting || Flow.Status == BlueprintCaptureStatus.Capturing || Flow.Status == BlueprintCaptureStatus.Sealing) return "采集或整理尚未完成";
                if (!State.Active || !State.Selection.HasSelection) return "请先完成蓝图选区";
                if (Flow.Status != BlueprintCaptureStatus.DraftReady && Library.Snapshot.Busy) return "蓝图库正在处理，请稍候";
                return null;
            }
        }
        internal bool HasActiveCaptureWork { get { return State.Active || discardPending || capture != null ||
            Flow.Status == BlueprintCaptureStatus.Starting || Flow.Status == BlueprintCaptureStatus.Capturing || Flow.Status == BlueprintCaptureStatus.Sealing ||
            Flow.Status == BlueprintCaptureStatus.DraftReady || Flow.Status == BlueprintCaptureStatus.Saving ||
            Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed && !Flow.ContentHandle.IsEmpty; } }
        internal bool Save()
        {
            if (!CanSave || !Input.CanStartActions) return false;
            if (Flow.Status == BlueprintCaptureStatus.DraftReady) { Shell?.BlueprintUi?.ShowCapture(); return true; }
            if (Library.Snapshot.Busy || discardPending) return false;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            var selection = State.Selection.Freeze();
            var token = new CaptureToken(Guid.NewGuid(), generation, State.ModeGeneration, selection.Revision);
            if (!Library.TryBeginStage(token)) return false;
            captureCommandId = Library.AcceptedCommandId;
            Flow.Token = token; Flow.ContentHandle = new ContentHandle(token.OperationId); Flow.Progress = 0; Flow.Total = selection.Count;
            Flow.Summary = null; Flow.DraftName = "新蓝图"; Flow.ExcludeUnreliable = false; Flow.ExcludedObjectIds = Array.Empty<int>();
            Flow.SelectedAssetId = null; SetFlow(BlueprintCaptureStatus.Starting, "正在准备采集…");
            capture = new CaptureJob(new BlueprintCaptureSource(() => HasSession && State.Active && State.ModeGeneration == token.ModeGeneration), selection, latestTick);
            Shell?.BlueprintUi?.ShowCapture(); return true;
        }
        internal void SetDraftName(string value) { if (Flow.Status == BlueprintCaptureStatus.DraftReady && Flow.DraftName != value) { Flow.DraftName = value; Flow.Revision++; } }
        internal void SetExcludeUnreliable(bool value)
        { if (Flow.Status == BlueprintCaptureStatus.DraftReady && Flow.ExcludeUnreliable != value) { Flow.ExcludeUnreliable = value; Flow.Revision++; } }
        internal void ExcludeObject(int id, bool exclude)
        {
            if (Flow.Status != BlueprintCaptureStatus.DraftReady || id <= 0 || Flow.ExcludeUnreliable) return;
            var ids = new List<int>(Flow.ExcludedObjectIds); bool present = ids.Contains(id);
            if (present == exclude) return;
            if (exclude) ids.Add(id); else ids.Remove(id);
            ids.Sort(); Flow.ExcludedObjectIds = ids.ToArray(); Flow.Revision++;
        }
        internal void ConfirmSave()
        {
            if (Flow.Status != BlueprintCaptureStatus.DraftReady || Library == null || Library.Snapshot.Busy) return;
            if (Library.TryCreate(Flow.ContentHandle, Flow.DraftName, Flow.Token, Flow.ExcludeUnreliable, Flow.ExcludedObjectIds))
            { captureCommandId = Library.AcceptedCommandId; SetFlow(BlueprintCaptureStatus.Saving, "正在保存蓝图…"); }
        }
        internal void CancelCaptureOrDraft()
        {
            // UI teardown can follow an earlier explicit/session cancellation.
            // Do not enqueue a discard after that stage was released or saved.
            if (Flow.Status == BlueprintCaptureStatus.Saving || Flow.Status == BlueprintCaptureStatus.Saved || Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed || Flow.Status == BlueprintCaptureStatus.Cancelled) return;
            capture?.Cancel(); capture = null;
            if (!Flow.ContentHandle.IsEmpty) discardPending = true;
            SetFlow(BlueprintCaptureStatus.Cancelled, "已取消，选区已保留。");
        }
        private void CancelOldCapture()
        {
            if (Flow.Status != BlueprintCaptureStatus.Saving && Flow.Status != BlueprintCaptureStatus.CommitUnconfirmed) CancelCaptureOrDraft();
            State.SetInteractionBlocked(false);
        }
        private void SetFlow(BlueprintCaptureStatus status, string message)
        { Flow.Status = status; Flow.Message = message ?? ""; Flow.Revision++; }
        private void AdvanceCapture(ulong tick)
        {
            latestTick = tick;
            if (Library == null) return;
            PumpLibrary();
            if (capture == null) return;
            if (!HasSession || !State.Active || State.ModeGeneration != Flow.Token.ModeGeneration)
            { CancelCaptureOrDraft(); return; }
            // UI edit admission is intentionally absent here. An accepted read
            // job and independent receipts must progress while its UI is open.
            if (Flow.Status != BlueprintCaptureStatus.Capturing || Library.Snapshot.Busy) return;
            if (capture.PendingChunk != null)
            { if (Library.TryAppendChunk(Flow.ContentHandle, capture.PendingChunk)) { captureCommandId = Library.AcceptedCommandId; capture.AcceptChunk(); } return; }
            capture.Advance(1024, 1.0);
            if (Flow.Progress != capture.Progress) { Flow.Progress = capture.Progress; Flow.Revision++; }
            if (capture.Error != null)
            { string reason = capture.Error; capture.Cancel(); capture = null; discardPending = true; SetFlow(BlueprintCaptureStatus.Failed, reason); return; }
            if (!capture.Complete) return;
            CaptureSummary summary = capture.Summary(tick);
            if (summary.ContentCount == 0)
            { capture = null; discardPending = true; SetFlow(BlueprintCaptureStatus.NoContent, "选区内没有可保存的内容"); return; }
            if (Library.TrySealDraft(Flow.ContentHandle, summary)) { captureCommandId = Library.AcceptedCommandId; capture = null; Flow.Summary = summary; SetFlow(BlueprintCaptureStatus.Sealing, "正在整理采集内容…"); }
        }
        private void PumpLibrary()
        {
            if (Library == null) return;
            Library.Pump();
            LibraryReceipt receipt;
            while (Library.TryTakeReceipt(out receipt)) ApplyReceipt(receipt);
            if (discardPending && !Library.Snapshot.Busy && Library.TryDiscardDraft(Flow.ContentHandle))
            { captureCommandId = Library.AcceptedCommandId; discardPending = false; }
        }
        private void ApplyReceipt(LibraryReceipt receipt)
        {
            ApplyProjectionMutation(receipt);
            if (receipt.OperationId != Flow.Token.OperationId) return;
            // The sealed capture summary is fixed. Filtered preview summaries
            // belong to their exclusion/cache key in the presentation owner;
            // a late read must never overwrite the original draft facts.
            if (receipt.Kind == LibraryCommandKind.Objects || receipt.Kind == LibraryCommandKind.List || receipt.Kind == LibraryCommandKind.Preview) return;
            // Operation identity alone does not authorize an unsolicited old
            // handle command to alter the projection. Match the actual accepted
            // Host command, including retirement receipts lacking an old token.
            if (receipt.CommandId != captureCommandId) return;
            captureCommandId = 0;
            if (receipt.OperationRetired && receipt.Status != LibraryStatus.Saved &&
                !(receipt.Kind == LibraryCommandKind.DiscardDraft && receipt.Status == LibraryStatus.Completed))
            {
                // Protected recovery material is not an executable draft. The
                // old operation cannot be retried, but a new explicit capture
                // may use the released slot without changing the old ID/files.
                capture?.Cancel(); capture = null; discardPending = false;
                Flow.ContentHandle = default(ContentHandle); Flow.Summary = null;
                SetFlow(receipt.Status == LibraryStatus.CommitUnconfirmed ? BlueprintCaptureStatus.CommitUnconfirmed : BlueprintCaptureStatus.Failed,
                    receipt.Status == LibraryStatus.CommitUnconfirmed ? "无法确认是否保存成功，相关文件已保留。可重新选择并采集新蓝图。" : "此次蓝图未保存，相关文件已保留。可再次采集，选区已保留。");
                return;
            }
            if (receipt.Kind == LibraryCommandKind.BeginStage && receipt.Status == LibraryStatus.Completed && receipt.SuggestedName != null)
            { Flow.DraftName = receipt.SuggestedName; Flow.Revision++; }
            if (receipt.Kind == LibraryCommandKind.DiscardDraft)
            { if (receipt.OperationRetired) Flow.ContentHandle = default(ContentHandle); return; }
            if (receipt.Kind == LibraryCommandKind.Create)
            {
                if (receipt.Status == LibraryStatus.Saved)
                {
                    Flow.ContentHandle = default(ContentHandle);
                    Flow.SelectedAssetId = receipt.AssetId;
                    if (HasSession && State.Active && generation == receipt.Token.SessionGeneration && State.ModeGeneration == receipt.Token.ModeGeneration && State.Selection.Revision == receipt.Token.SelectionRevision)
                    { State.Selection.Clear(); State.Interrupt(); Pointer.Cancel(); }
                    SetFlow(BlueprintCaptureStatus.Saved, "蓝图已保存。"); Shell?.BlueprintUi?.ShowSaved(receipt.AssetId);
                }
                else if (receipt.Status == LibraryStatus.CommitUnconfirmed || receipt.Status == LibraryStatus.Protected)
                    SetFlow(BlueprintCaptureStatus.CommitUnconfirmed, "无法确认是否保存成功，相关文件已保留。");
                else SetFlow(BlueprintCaptureStatus.DraftReady, receipt.Error ?? "保存失败，草稿和选区已保留。");
                return;
            }
            if (Flow.Status == BlueprintCaptureStatus.Cancelled || Flow.Status == BlueprintCaptureStatus.Failed || Flow.Status == BlueprintCaptureStatus.NoContent) return;
            if (receipt.Status != LibraryStatus.Completed)
            { capture?.Cancel(); capture = null; SetFlow(BlueprintCaptureStatus.Failed, receipt.Error ?? "采集暂存失败，选区已保留。"); return; }
            if (receipt.Kind == LibraryCommandKind.BeginStage) SetFlow(BlueprintCaptureStatus.Capturing, "正在采集蓝图…");
            else if (receipt.Kind == LibraryCommandKind.SealDraft) SetFlow(BlueprintCaptureStatus.DraftReady, "请确认内容并保存。");
        }
    }
}
