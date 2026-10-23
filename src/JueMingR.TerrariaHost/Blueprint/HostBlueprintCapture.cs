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
        private ulong latestTick;
        internal bool CanSave { get { return ControlsEnabled && State.Active && State.Selection.HasSelection && Library != null &&
            Flow.Status != BlueprintCaptureStatus.Starting && Flow.Status != BlueprintCaptureStatus.Capturing &&
            Flow.Status != BlueprintCaptureStatus.Sealing && Flow.Status != BlueprintCaptureStatus.Saving && Flow.Status != BlueprintCaptureStatus.CommitUnconfirmed; } }
        internal bool Save()
        {
            if (!CanSave || !Input.CanStartActions) return false;
            if (Flow.Status == BlueprintCaptureStatus.DraftReady) { Shell?.BlueprintUi?.ShowCapture(); return true; }
            if (Library.Snapshot.Busy || discardPending) return false;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            var selection = State.Selection.Freeze();
            var token = new CaptureToken(Guid.NewGuid(), generation, State.ModeGeneration, selection.Revision);
            if (!Library.TryBeginStage(token)) return false;
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
                SetFlow(BlueprintCaptureStatus.Saving, "正在保存蓝图…");
        }
        internal void CancelCaptureOrDraft()
        {
            if (Flow.Status == BlueprintCaptureStatus.Saving || Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed) return;
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
            { if (Library.TryAppendChunk(Flow.ContentHandle, capture.PendingChunk)) capture.AcceptChunk(); return; }
            capture.Advance(1024, 1.0);
            if (Flow.Progress != capture.Progress) { Flow.Progress = capture.Progress; Flow.Revision++; }
            if (capture.Error != null)
            { string reason = capture.Error; capture.Cancel(); capture = null; discardPending = true; SetFlow(BlueprintCaptureStatus.Failed, reason); return; }
            if (!capture.Complete) return;
            CaptureSummary summary = capture.Summary(tick);
            if (summary.ContentCount == 0)
            { capture = null; discardPending = true; SetFlow(BlueprintCaptureStatus.NoContent, "选区内没有可保存的内容"); return; }
            if (Library.TrySealDraft(Flow.ContentHandle, summary)) { capture = null; Flow.Summary = summary; SetFlow(BlueprintCaptureStatus.Sealing, "正在整理采集内容…"); }
        }
        private void PumpLibrary()
        {
            if (Library == null) return;
            Library.Pump();
            LibraryReceipt receipt;
            while (Library.TryTakeReceipt(out receipt)) ApplyReceipt(receipt);
            if (discardPending && !Library.Snapshot.Busy && Library.TryDiscardDraft(Flow.ContentHandle)) discardPending = false;
        }
        private void ApplyReceipt(LibraryReceipt receipt)
        {
            if (receipt.OperationId != Flow.Token.OperationId) return;
            if (receipt.Kind == LibraryCommandKind.Preview && receipt.Summary != null && Flow.Status == BlueprintCaptureStatus.DraftReady)
            { Flow.Summary = receipt.Summary; Flow.Revision++; return; }
            if (receipt.Kind == LibraryCommandKind.Objects || receipt.Kind == LibraryCommandKind.List || receipt.Kind == LibraryCommandKind.Preview) return;
            if (receipt.Kind == LibraryCommandKind.DiscardDraft) return;
            if (receipt.Kind == LibraryCommandKind.Create)
            {
                if (receipt.Status == LibraryStatus.Saved)
                {
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
