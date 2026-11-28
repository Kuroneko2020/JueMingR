using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;

namespace JueMingR.Features.Companions
{
    public sealed partial class CompanionProfileManager
    {
        private sealed class Deleting
        {
            internal CompanionDeletion Receipt;
            internal long MaximumRevision;
            internal long Operation;
            internal bool Busy;
        }

        private readonly Dictionary<Guid, Deleting> deletions = new Dictionary<Guid, Deleting>();
        private readonly HashSet<Guid> deleted = new HashSet<Guid>();
        public IReadOnlyList<CompanionDeletion> Deletions { get; private set; } = Array.Empty<CompanionDeletion>();
        public bool IsDeletionComplete(Guid id) => deleted.Contains(id);

        public bool NameInUse(string name)
        {
            if (FindName(name) != null) return true;
            foreach (var state in deletions.Values)
                if (state.Receipt.OwnerId == OwnerId && String.Equals(state.Receipt.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool RequestDeletion(Guid owner, Guid id)
        {
            if (disposed || owner != OwnerId || Find(id) == null || !entries[id].Published) return false;
            var profile = entries[id].Profile;
            // All older captures already belong to this same serial worker. Fence every
            // new save and activation before putting the delete behind those writes.
            BeginDeletion(new CompanionDeletion(owner, id, profile.Name), profile.Revision);
            RefreshError(); Refresh();
            return true;
        }

        public bool RetryDeletion(Guid owner, Guid id)
        {
            if (disposed || owner != OwnerId || !TryGetDeletion(owner, id, out var state) || state.Busy || state.Receipt.Error == null) return false;
            QueueDeletion(state);
            return true;
        }

        public void FailDeletionBindings(Guid owner, Guid id) =>
            FailDeletionCleanup(owner, id, "人物档案已删除，快捷键清理失败，可重试。");

        public void FailDeletionCleanup(Guid owner, Guid id, string reason)
        {
            if (!TryGetDeletion(owner, id, out var state) || state.Busy || !state.Receipt.FilesRemoved || state.Receipt.Error != null) return;
            ChangeDeletion(state, true, reason);
        }

        public void FinishDeletion(Guid owner, Guid id)
        {
            if (disposed || !TryGetDeletion(owner, id, out var state) || state.Busy || !state.Receipt.FilesRemoved || state.Receipt.Error != null) return;
            var receipt = state.Receipt;
            long operation = ++state.Operation;
            state.Busy = true;
            ChangeDeletion(state, true, null, finishing: true);
            Enqueue(() =>
            {
                try
                {
                    archive.FinishDeletion(receipt);
                    Complete(() =>
                    {
                        if (state.Operation != operation) return;
                        deleted.Add(id);
                        deletions.Remove(id);
                        PublishDeletions();
                    });
                }
                catch (Exception error)
                {
                    Complete(() =>
                    {
                        if (state.Operation != operation) return;
                        state.Busy = false;
                        ChangeDeletion(state, true, "人物删除尚未完成，可重试。" + error.Message);
                    });
                }
            });
        }

        private bool IsDeleting(Guid id) { return deletions.ContainsKey(id) || deleted.Contains(id); }

        private bool TryGetDeletion(Guid owner, Guid id, out Deleting state)
        { return deletions.TryGetValue(id, out state) && state.Receipt.OwnerId == owner; }

        private void BeginDeletion(CompanionDeletion receipt, long maximumRevision)
        {
            var state = new Deleting { Receipt = receipt, MaximumRevision = maximumRevision };
            deletions.Add(receipt.ProfileId, state);
            QueueDeletion(state);
        }

        private void QueueDeletion(Deleting state)
        {
            ChangeDeletion(state, false, null);
            var receipt = state.Receipt;
            long operation = ++state.Operation;
            state.Busy = true;
            Enqueue(() =>
            {
                try
                {
                    archive.DeleteFiles(receipt, state.MaximumRevision);
                    Complete(() =>
                    {
                        if (state.Operation != operation) return;
                        state.Busy = false;
                        entries.Remove(receipt.ProfileId);
                        ChangeDeletion(state, true, null);
                    });
                }
                catch (Exception error)
                {
                    Complete(() =>
                    {
                        if (state.Operation != operation) return;
                        state.Busy = false;
                        ChangeDeletion(state, false, "人物档案删除未完成，可重试。" + error.Message);
                    });
                }
            });
        }

        private void ChangeDeletion(Deleting state, bool filesRemoved, string error, bool finishing = false)
        {
            var receipt = state.Receipt;
            state.Receipt = new CompanionDeletion(receipt.OwnerId, receipt.ProfileId, receipt.Name, filesRemoved, error, finishing);
            PublishDeletions();
        }

        private void PublishDeletions()
        {
            var receipts = new List<CompanionDeletion>(deletions.Count);
            foreach (var state in deletions.Values) receipts.Add(state.Receipt);
            Deletions = receipts.AsReadOnly();
        }
    }
}
