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
        private bool projectionScopeBound, projectionRestoreComplete, projectionSceneDirty;
        private string projectionPersistenceReported;
        internal bool ProjectionPersistenceReady { get { return projectionRestoreComplete; } }
        private void BeginProjectionSession()
        {
            projectionScopeBound = projectionRestoreComplete = false;
            projectionHistory.BeginSession(generation, Main.maxTilesX, Main.maxTilesY);
        }
        private void EndProjectionSession()
        {
            // Every structural business commit has already offered an immutable
            // snapshot. Moving/Following and resource disposal cannot replace it.
            AcceptProjectionSceneSnapshot(); projectionHistory.EndSession(); projectionScopeBound = projectionRestoreComplete = false;
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
                projectionHistory.UseScope(scope); projectionScopeBound = true;
            }
            if (!projectionHistory.Loaded || projectionRestoreComplete) return;
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
                var identity = new BlueprintProjectionIdentity(record.Template.AssetId, record.Template.ContentRevision, record.Template.ContentHash, generation, projectionGeneration + 1);
                if (!Library.RequestProjectionRestore(identity, record.Mask)) break;
                projectionGeneration++;
                persistedProjectionRestore.Add(new BlueprintProjectionPlacement { Identity = identity, OriginX = record.OriginX, OriginY = record.OriginY, CommitOrder = record.Order, Mask = record.Mask, Positioned = true });
                projectionRestoreCursor++;
            }
            FinishProjectionRestore();
        }
        private bool TakePersistedProjectionReceipt(BlueprintProjectionReceipt receipt)
        {
            var item = persistedProjectionRestore.Find(value => value.Identity.Equals(receipt.Identity)); if (item == null) return false;
            if (receipt.Kind == BlueprintProjectionReceiptKind.Failed)
            { RejectProjectionRestoreItem(item, receipt.FailureCode ?? "blueprint-scene-template-failed"); return true; }
            if (receipt.Kind != BlueprintProjectionReceiptKind.Opened || item.Opened) return true;
            if (receipt.Summary == null || item.OriginX < 0 || item.OriginY < 0 ||
                (long)item.OriginX + receipt.Summary.Width > projectionRestoreDocument.Width || (long)item.OriginY + receipt.Summary.Height > projectionRestoreDocument.Height)
            { RejectProjectionRestoreItem(item, "blueprint-scene-template-bounds"); return true; }
            item.Summary = receipt.Summary; item.Name = receipt.Name; item.TotalLayerCount = receipt.TotalLayerCount; item.TotalConstructionLayerCount = receipt.TotalConstructionLayerCount;
            item.SourceRanges = receipt.SourceRanges; item.MatchRanges = receipt.MatchRanges; item.Opened = true; projectionRestoreOpened++;
            FinishProjectionRestore(); return true;
        }
        private void FinishProjectionRestore()
        {
            if (projectionRestoreDocument == null || projectionRestoreCursor != projectionRestoreDocument.Records.Count || projectionRestoreOpened != projectionRestoreCursor) return;
            projectionScene.AddRange(persistedProjectionRestore);
            if (projectionScene.Count != 0) { projectionOrder = Math.Max(projectionOrder, projectionScene[projectionScene.Count - 1].CommitOrder); ProjectionMode = BlueprintProjectionMode.Fixed; }
            projectionSceneRevision++; projectionRestoreComplete = true; ClearProjectionPersistenceRead();
        }
        private void RejectProjectionRestoreItem(BlueprintProjectionPlacement item, string error)
        {
            Library.ReleaseProjection(item.Identity); DisposeProjectionData(item); persistedProjectionRestore.Remove(item); projectionRestoreOpened++;
            // Keep the original complete archive intact. A missing/corrupt or
            // changed template is not proof that its saved record was a user
            // deletion, but it also must not hide unrelated valid references.
            projectionHistory.Protect(error); FinishProjectionRestore();
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
            foreach (var item in projectionScene) records.Add(new BlueprintSceneRecord(item.Identity.TemplateKey, item.OriginX, item.OriginY, item.CommitOrder, item.Mask));
            projectionHistory.Offer(records); projectionSceneDirty = false;
        }
        private void TakeProjectionPersistenceFeedback(Action<string> display)
        {
            string error = projectionHistory.Error ?? projectionHistory.BackgroundError;
            if (error == null || error == projectionPersistenceReported) return;
            string message = projectionHistory.CommitUnconfirmed ? "无法确认世界蓝图是否保存成功；原文件已保护。" :
                error == PreferenceStatus.UnsupportedVersion.ToString() ? "世界蓝图的存档版本暂不支持，请使用匹配的程序；原文件已保留。" :
                error == PreferenceStatus.Invalid.ToString() ? "世界蓝图存档已损坏；本次修改仅本次有效，原文件已保留。" :
                error == PreferenceStatus.Conflict.ToString() ? "世界蓝图存档与当前角色或世界不符；本次修改仅本次有效，原文件已保留。" :
                "世界蓝图未能可靠读取或保存；本次修改仅本次有效，原文件已保留。";
            display(message);
            projectionPersistenceReported = error;
        }
    }
}
