using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Map;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private readonly BlueprintSceneHistory projectionHistory;
        private readonly List<BlueprintProjectionPlacement> persistedProjectionRestore = new List<BlueprintProjectionPlacement>();
        private BlueprintSceneDocument projectionRestoreDocument;
        private int projectionRestoreCursor, projectionRestoreOpened;
        private bool projectionScopeBound, projectionRestoreComplete, projectionSceneDirty, projectionSaveAfterReentry;
        private string projectionPersistenceReported, projectionBoundScope;
        private sealed class FailedSceneRecord
        {
            internal long Instance;
            internal BlueprintSceneRecord Record;
            internal string Name, Code;
            internal BlueprintProjectionFailureScope FailureScope;
        }
        private readonly Dictionary<long, FailedSceneRecord> failedSceneRecords = new Dictionary<long, FailedSceneRecord>();
        private readonly HashSet<string> reportedSceneRecords = new HashSet<string>(StringComparer.Ordinal);
        private long failedSceneRevision;
        private FailedSceneRecord repairingSceneRecord;
        private BlueprintSceneRecord sceneRetryRecord;
        private BlueprintProjectionPlacement sceneRetryPlacement;
        internal bool ProjectionPersistenceReady { get { return projectionRestoreComplete; } }
        private void BeginProjectionSession()
        {
            projectionScopeBound = projectionRestoreComplete = false; projectionBoundScope = null;
            failedSceneRecords.Clear(); failedSceneRevision++; projectionSceneDirty = projectionSaveAfterReentry = false;
            repairingSceneRecord = null; sceneRetryRecord = null; sceneRetryPlacement = null;
            projectionHistory.BeginSession(generation, Main.maxTilesX, Main.maxTilesY);
        }
        private void EndProjectionSession()
        {
            // Every structural business commit has already offered an immutable
            // snapshot. Moving/Following and resource disposal cannot replace it.
            AcceptProjectionSceneSnapshot(); projectionHistory.EndSession(); projectionScopeBound = projectionRestoreComplete = false;
            if (sceneRetryPlacement != null)
            { Library?.ReleaseProjection(sceneRetryPlacement.Identity); DisposeProjectionData(sceneRetryPlacement); }
            repairingSceneRecord = null; sceneRetryRecord = null; sceneRetryPlacement = null;
        }
        private void ClearProjectionPersistenceRead()
        { persistedProjectionRestore.Clear(); projectionRestoreDocument = null; projectionRestoreCursor = projectionRestoreOpened = 0; }
        private void PumpProjectionPersistence()
        {
            projectionHistory.Poll();
            if (!HasSession || Library == null) return;
            if (!projectionScopeBound)
            {
                string scope = LocalWorldIdentity.Observe(); if (scope == null) return;
                projectionHistory.UseScope(scope); projectionScopeBound = true; projectionBoundScope = scope;
            }
            if (projectionRestoreComplete) { PumpSceneRecordRecovery(); return; }
            if (!projectionHistory.Loaded) return;
            if (projectionRestoreDocument == null) projectionRestoreDocument = projectionHistory.Restored;
            if (projectionHistory.Protected && projectionRestoreDocument == null) { projectionRestoreComplete = true; return; }
            if (projectionRestoreDocument == null) return;
            // Admission is bounded independently of the saved scene size. The
            // eligible restored scene becomes visible after every reference has
            // settled, in committed order rather than I/O order. One failed
            // template cannot withdraw another independently valid instance.
            for (int n = 0; n < 4 && projectionRestoreCursor < projectionRestoreDocument.Records.Count; n++)
            {
                var record = projectionRestoreDocument.Records[projectionRestoreCursor];
                if (record.MirroredHorizontally && !EnsureMirrorProfile())
                {
                    if (!mirrorProfileUnavailable) break;
                    var unsupported = SceneRecordPlacement(record, ++projectionGeneration);
                    RecordRecoveryFailure(unsupported, "blueprint-scene-mirror-profile");
                    projectionOrder = Math.Max(projectionOrder, record.Order);
                    projectionRestoreCursor++; projectionRestoreOpened++;
                    continue;
                }
                if (!TryRequestSceneRecordRestore(record, out var placement)) break;
                persistedProjectionRestore.Add(placement);
                projectionOrder = Math.Max(projectionOrder, record.Order);
                projectionRestoreCursor++;
            }
            FinishProjectionRestore();
        }
        private bool TryRequestSceneRecordRestore(BlueprintSceneRecord record, out BlueprintProjectionPlacement placement)
        {
            placement = null;
            var identity = new BlueprintProjectionIdentity(record.Template.AssetId, record.Template.ContentRevision, record.Template.ContentHash, generation, projectionGeneration + 1);
            if (!Library.RequestProjectionRestore(identity, record.Mask, record.MirroredHorizontally ? mirrorProfile : null)) return false;
            placement = SceneRecordPlacement(record, ++projectionGeneration);
            return true;
        }
        private BlueprintProjectionPlacement SceneRecordPlacement(BlueprintSceneRecord record, long instance)
        {
            return new BlueprintProjectionPlacement { Identity = new BlueprintProjectionIdentity(record.Template.AssetId, record.Template.ContentRevision,
                record.Template.ContentHash, generation, instance), OriginX = record.OriginX, OriginY = record.OriginY,
                CommitOrder = record.Order, Mask = record.Mask, MirroredHorizontally = record.MirroredHorizontally, MirrorProfile = mirrorProfile, Positioned = true };
        }
        private bool TakePersistedProjectionReceipt(BlueprintProjectionReceipt receipt)
        {
            if (sceneRetryPlacement?.Identity.Equals(receipt.Identity) == true) return TakeRetriedProjectionReceipt(receipt);
            var item = persistedProjectionRestore.Find(value => value.Identity.Equals(receipt.Identity)); if (item == null) return false;
            if (receipt.Kind == BlueprintProjectionReceiptKind.Failed)
            { RejectProjectionRestoreItem(item, receipt.FailureCode ?? "blueprint-scene-template-failed", receipt.FailureScope); return true; }
            if (receipt.Kind != BlueprintProjectionReceiptKind.Opened || item.Opened) return true;
            if (!TryOpenRestoredProjection(item, receipt))
            { RejectProjectionRestoreItem(item, "blueprint-scene-template-bounds"); return true; }
            var record = FindSavedSceneRecord(projectionRestoreDocument, item.CommitOrder, item.Identity.TemplateKey);
            if (record != null) RestoreHoikPending(item, record.HoikPending);
            projectionRestoreOpened++;
            FinishProjectionRestore(); return true;
        }
        private bool TryOpenRestoredProjection(BlueprintProjectionPlacement item, BlueprintProjectionReceipt receipt)
        {
            var document = projectionRestoreDocument ?? projectionHistory.Confirmed;
            if (document == null || receipt.Summary == null || item.OriginX < 0 || item.OriginY < 0 ||
                (long)item.OriginX + receipt.Summary.Width > document.Width || (long)item.OriginY + receipt.Summary.Height > document.Height) return false;
            item.Summary = receipt.Summary; item.Name = receipt.Name; item.TotalLayerCount = receipt.TotalLayerCount; item.TotalConstructionLayerCount = receipt.TotalConstructionLayerCount;
            item.SourceRanges = receipt.SourceRanges; item.MatchRanges = receipt.MatchRanges; item.Opened = true;
            return true;
        }
        private void FinishProjectionRestore()
        {
            if (projectionRestoreDocument == null || projectionRestoreCursor != projectionRestoreDocument.Records.Count || projectionRestoreOpened != projectionRestoreCursor) return;
            projectionScene.AddRange(persistedProjectionRestore);
            if (projectionScene.Count != 0) { projectionOrder = Math.Max(projectionOrder, projectionScene[projectionScene.Count - 1].CommitOrder); ProjectionMode = BlueprintProjectionMode.Fixed; }
            if (failedSceneRecords.Count == 0) projectionHistory.CompleteRecordRecovery();
            projectionSceneRevision++; projectionRestoreComplete = true; ClearProjectionPersistenceRead();
        }
        private void RejectProjectionRestoreItem(BlueprintProjectionPlacement item, string error, BlueprintProjectionFailureScope failureScope = BlueprintProjectionFailureScope.SavedInstance)
        {
            RecordRecoveryFailure(item, error, failureScope);
            if (!item.Opened) projectionRestoreOpened++;
            Library.ReleaseProjection(item.Identity); DisposeProjectionData(item); persistedProjectionRestore.Remove(item);
            // Keep the original complete archive intact. A missing/corrupt or
            // changed template is not proof that its saved record was a user
            // deletion, but it also must not hide unrelated valid references.
            FinishProjectionRestore();
        }
        private void RecordRecoveryFailure(BlueprintProjectionPlacement item, string failureCode, BlueprintProjectionFailureScope failureScope = BlueprintProjectionFailureScope.SharedAsset)
        {
            var document = projectionRestoreDocument ?? projectionHistory.Confirmed;
            var record = FindSavedSceneRecord(document, item.CommitOrder, item.Identity.TemplateKey);
            if (record == null) { ProtectProjectionPersistence(failureCode); return; }
            if (!failedSceneRecords.TryGetValue(item.Identity.ProjectionGeneration, out var existing))
            {
                existing = new FailedSceneRecord { Instance = item.Identity.ProjectionGeneration, Record = record, Name = item.Name };
                failedSceneRecords.Add(existing.Instance, existing);
            }
            NoteSceneRecordFailure(document.Scope, existing, failureCode, failureScope);
            projectionHistory.ProtectRecord(failureCode);
        }
        private void NoteSceneRecordFailure(string scope, FailedSceneRecord row, string code, BlueprintProjectionFailureScope failureScope = BlueprintProjectionFailureScope.SavedInstance)
        {
            row.Code = code; row.FailureScope = failureScope; failedSceneRevision++;
            var template = row.Record.Template;
            string identity = scope + ":" + row.Record.Order + ":" + template.AssetId + ":" + template.ContentRevision + ":" + template.ContentHash + ":" + failureScope + ":" + code;
            if (reportedSceneRecords.Add(identity))
                System.Diagnostics.Trace.TraceWarning("Blueprint scene recovery scope={0} asset={1} instance={2} order={3} effect={4} stage=restore reason={5}; original retained",
                    scope, template.AssetId, row.Instance, row.Record.Order, failureScope, code);
        }
        private static BlueprintSceneRecord FindSavedSceneRecord(BlueprintSceneDocument document, long order, BlueprintTemplateKey template)
        {
            if (document == null) return null;
            int lo = 0, hi = document.Records.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                var record = document.Records[mid];
                if (record.Order == order) return record.Template.Equals(template) ? record : null;
                if (record.Order < order) lo = mid + 1; else hi = mid - 1;
            }
            return null;
        }
        private bool RequestSceneRecordRecovery(long session, long instance, bool remove)
        {
            if (!HasSession || session != generation || !Input.CanStartActions || !CanOpenPlacedList || repairingSceneRecord != null) return false;
            if (LocalWorldIdentity.Observe() != projectionBoundScope) { ProtectProjectionPersistence("blueprint-scene-identity-changed"); return false; }
            if (!failedSceneRecords.TryGetValue(instance, out var row)) return false;
            var record = FindSavedSceneRecord(projectionHistory.Confirmed, row.Record.Order, row.Record.Template);
            if (!projectionHistory.RequestRecordRecovery(record, remove)) return false;
            repairingSceneRecord = row; failedSceneRevision++;
            return true;
        }
        private void PumpSceneRecordRecovery()
        {
            if (repairingSceneRecord == null) return;
            if (projectionHistory.TryTakeRecordRecovery(out var record, out bool removed))
            {
                if (LocalWorldIdentity.Observe() != projectionBoundScope)
                { ProtectProjectionPersistence("blueprint-scene-identity-changed"); FinishSceneRecordRecovery(false); return; }
                if (record == null) { FinishSceneRecordRecovery(false); return; }
                if (removed)
                {
                    FinishSceneRecordRecovery(true);
                    projectionFeedback = projectionSaveAfterReentry ? "已从存档移除这份蓝图。请重新进入世界以恢复保存。" : "已从存档移除这份蓝图。";
                    return;
                }
                sceneRetryRecord = record;
            }
            if (sceneRetryRecord == null || sceneRetryPlacement != null) return;
            if (sceneRetryRecord.MirroredHorizontally && !EnsureMirrorProfile())
            {
                if (mirrorProfileUnavailable)
                {
                    NoteSceneRecordFailure(projectionHistory.Confirmed.Scope, repairingSceneRecord, "blueprint-scene-mirror-profile");
                    FinishSceneRecordRecovery(false);
                }
                return;
            }
            TryRequestSceneRecordRestore(sceneRetryRecord, out sceneRetryPlacement);
        }
        private bool TakeRetriedProjectionReceipt(BlueprintProjectionReceipt receipt)
        {
            var placement = sceneRetryPlacement;
            if (receipt.Kind != BlueprintProjectionReceiptKind.Failed && receipt.Kind != BlueprintProjectionReceiptKind.Opened) return true;
            if (receipt.Kind == BlueprintProjectionReceiptKind.Opened && TryOpenRestoredProjection(placement, receipt))
            {
                var samplingNext = ProjectionMatchCursorOwner(projectionMatchCursor);
                var admissionNext = ProjectionMatchCursorOwner(projectionMatchAdmissionCursor);
                RestoreHoikPending(placement, sceneRetryRecord.HoikPending);
                projectionScene.Add(placement);
                projectionScene.Sort((left, right) => left.CommitOrder.CompareTo(right.CommitOrder));
                ReconcileProjectionSceneOrder(samplingNext, admissionNext);
                projectionSceneRevision++;
                if (!ProjectionEditing) ProjectionMode = BlueprintProjectionMode.Fixed;
                FinishSceneRecordRecovery(true);
                projectionFeedback = projectionSaveAfterReentry ? "已恢复这份蓝图。请重新进入世界以恢复保存。" : "已恢复这份蓝图。";
            }
            else
            {
                Library.ReleaseProjection(placement.Identity); DisposeProjectionData(placement);
                NoteSceneRecordFailure(projectionHistory.Confirmed.Scope, repairingSceneRecord, receipt.FailureCode ?? "blueprint-scene-template-bounds", receipt.FailureScope);
                FinishSceneRecordRecovery(false);
                projectionFeedback = "这份蓝图仍无法恢复，原存档已保留。";
            }
            return true;
        }
        private void FinishSceneRecordRecovery(bool success)
        {
            if (success)
            {
                failedSceneRecords.Remove(repairingSceneRecord.Instance);
                if (failedSceneRecords.Count == 0)
                {
                    // A later ordinary Offer would still carry the whole
                    // modified live scene, even after clearing its dirty bit.
                    // Reentry proves the newly confirmed full file before save
                    // protection can lift when changes were session-only.
                    projectionSaveAfterReentry = projectionSceneDirty;
                    if (!projectionSaveAfterReentry) projectionHistory.CompleteRecordRecovery();
                }
            }
            repairingSceneRecord = null; sceneRetryRecord = null; sceneRetryPlacement = null;
            failedSceneRevision++;
        }
        private void ProtectProjectionPersistence(string error)
        { if (projectionScopeBound) projectionHistory.Protect(error); }
        private void PublishProjectionScene()
        {
            projectionSceneDirty = true;
        }
        private void AcceptProjectionSceneSnapshot()
        {
            // A template delete temporarily withdraws readers. Only its reliable
            // receipt may publish that hole; an unknown outcome protects the old
            // archive instead of saving the provisional scene as empty.
            if (!projectionSceneDirty || !projectionRestoreComplete || deletePending || projectionHistory.Protected) return;
            var loaded = projectionHistory.Restored;
            if (loaded == null || loaded.Width != Main.maxTilesX || loaded.Height != Main.maxTilesY)
            { ProtectProjectionPersistence("blueprint-scene-world-dimensions-changed"); return; }
            var records = new List<BlueprintSceneRecord>(projectionScene.Count);
            foreach (var item in projectionScene) records.Add(new BlueprintSceneRecord(item.Identity.TemplateKey, item.OriginX, item.OriginY, item.CommitOrder, item.Mask, item.MirroredHorizontally, CaptureHoikPending(item)));
            projectionHistory.Offer(records); projectionSceneDirty = false;
        }
        private void TakeProjectionPersistenceFeedback(Action<string> display)
        {
            string error = projectionHistory.Error ?? projectionHistory.BackgroundError;
            if (error == null) { projectionPersistenceReported = null; return; }
            string identity = projectionBoundScope + ":" + error + ":" + projectionSaveAfterReentry;
            if (identity == projectionPersistenceReported) return;
            string message = projectionSaveAfterReentry ? "保护期间的变更仅本次有效。重新进入世界后即可恢复保存。" :
                projectionHistory.CommitUnconfirmed ? "无法确认世界蓝图是否保存成功；原文件已保护。" :
                error == PreferenceStatus.UnsupportedVersion.ToString() ? "世界蓝图的存档版本暂不支持，请使用匹配的程序；原文件已保留。" :
                error == PreferenceStatus.Invalid.ToString() ? "世界蓝图存档已损坏；本次修改仅本次有效，原文件已保留。" :
                error == PreferenceStatus.Conflict.ToString() ? "世界蓝图存档与当前角色或世界不符；本次修改仅本次有效，原文件已保留。" :
                "世界蓝图未能可靠读取或保存；本次修改仅本次有效，原文件已保留。";
            display(message);
            projectionPersistenceReported = identity;
        }
    }
}
