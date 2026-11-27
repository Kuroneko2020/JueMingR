using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Companions;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private Guid deletionFeedbackId;
        private long deletionFeedbackSession;
        private string deletionProgress;
        private string deletionRetirementWarning;
        private readonly Dictionary<(Guid Owner, Guid Profile), Task> deletingChatData =
            new Dictionary<(Guid Owner, Guid Profile), Task>();
        internal Guid DeletionViewOwner => ArchiveIdentityCurrent() ? profiles.OwnerId : Guid.Empty;
        internal IReadOnlyList<CompanionDeletion> Deletions => profiles.Deletions;

        internal IReadOnlyList<CompanionDeletion> PollDeletionBindings()
        {
            // Once accepted, cleanup still belongs to its original owner in
            // the main menu or after the local character changes.
            if (profiles.Deletions.Count != 0) profiles.Pump();
            if (deletionFeedbackId != Guid.Empty && profiles.IsDeletionComplete(deletionFeedbackId))
            {
                if (session == deletionFeedbackSession && ArchiveIdentityCurrent() && ReferenceEquals(Feedback, deletionProgress))
                    Feedback = "人物已删除。" + deletionRetirementWarning;
                deletionFeedbackId = Guid.Empty;
                deletionProgress = deletionRetirementWarning = null;
            }
            return profiles.Deletions;
        }

        internal bool DeleteCompanion(Guid ownerId, Guid id, long confirmedSession)
        {
            if (Thread.CurrentThread.ManagedThreadId != itemThread || confirmedSession != session || ownerId == Guid.Empty ||
                !ArchiveIdentityCurrent() || ownerId != profiles.OwnerId || !profiles.RequestDeletion(ownerId, id))
                return Result(false, "人物或世界已变化，请重新确认删除。");

            RetireDeletedChat(ownerId, id);
            string warning = null;
            if (actors.TryGetValue(id, out var actor))
            {
                string previousFeedback = Feedback;
                // This activation is already fenced. Reuse native retirement
                // and slot ownership cleanup without capturing a final body.
                actor.Closing = true;
                actor.Body.controlUseItem = actor.Body.channel = false;
                try
                {
                    try { EndFishingActivity(actor, retire: true); }
                    finally { RetireProjectiles(actor); }
                }
                catch (Exception) { warning = "队友动作收尾时出现错误。"; }
                finally
                {
                    profiles.EndPlay(actor.Body);
                    Detach(actor);
                    profiles.Release(actor.Profile, actor.Generation);
                    actors.Remove(id);
                    if (actors.Count == 0) { prediction.EndWorld(); CompanionThreatField.Reset(); planningTurn = 0; }
                }
                if (!ReferenceEquals(Feedback, previousFeedback) && !String.IsNullOrEmpty(Feedback)) warning += Feedback;
            }
            if (itemStorageProfile == id) SetItemStorage(Guid.Empty, CompanionInventoryArea.Inventory);
            resetAfterRecall.Remove(id);
            pendingNurse.Remove(id);
            ItemCaptureSucceeded(id);
            previews.Remove(id); previewRevisions.Remove(id); states.Remove(id);
            if (savingCreation == id) savingCreation = Guid.Empty;
            if (LastCreatedId == id) LastCreatedId = Guid.Empty;
            deletionRetirementWarning = warning;
            return DeletionProgress(id, "正在删除人物。" + warning);
        }

        internal void FinishDeletionBindings(CompanionDeletion deletion)
        {
            var key = (deletion.OwnerId, deletion.ProfileId);
            if (chatStore == null || ChatConnections == null)
            {
                profiles.FinishDeletion(key.OwnerId, key.ProfileId);
                return;
            }
            if (!deletingChatData.TryGetValue(key, out var cleanup))
            {
                chats.TryGetValue(key, out var chat);
                var writer = chat?.Writer.Work;
                var previousClear = chat?.Clearing;
                var store = chatStore;
                var connections = ChatConnections;
                // This phase is reached again from the existing archive
                // deletion intent after interruption or restart. Never
                // finalize that intent while any accepted older save can
                // still recreate this character's chat.
                deletingChatData.Add(key, Task.Run(async () =>
                {
                    if (writer != null) await writer.ConfigureAwait(false);
                    if (previousClear != null)
                    {
                        try { await previousClear.ConfigureAwait(false); }
                        catch (Exception) { /* The cleanup below retries the clear. */ }
                    }
                    await store.ClearHistoryAsync(key.OwnerId, key.ProfileId).ConfigureAwait(false);
                    await connections.ForgetBindingAsync(key.OwnerId, key.ProfileId).ConfigureAwait(false);
                }));
                return;
            }
            if (!cleanup.IsCompleted) return;
            deletingChatData.Remove(key);
            if (cleanup.Status == TaskStatus.RanToCompletion)
            {
                chats.Remove(key);
                profiles.FinishDeletion(key.OwnerId, key.ProfileId);
            }
            else
            {
                _ = cleanup.Exception;
                profiles.FailDeletionCleanup(key.OwnerId, key.ProfileId, "人物档案已删除，聊天记录或模型绑定清理失败，可重试。");
            }
        }
        internal void FailDeletionBindings(CompanionDeletion deletion) => profiles.FailDeletionBindings(deletion.OwnerId, deletion.ProfileId);
        internal void RetryCompanionDeletion(Guid ownerId, Guid id)
        {
            if (ownerId != DeletionViewOwner || !profiles.RetryDeletion(ownerId, id)) return;
            if (deletionFeedbackId != id) deletionRetirementWarning = null;
            DeletionProgress(id, "正在重试删除人物。");
        }

        private bool DeletionProgress(Guid id, string text)
        {
            deletionFeedbackId = id; deletionFeedbackSession = session; deletionProgress = text;
            return Result(true, text);
        }
    }
}
