using System;
using System.Collections.Generic;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    // Host owns live scene semantics; this owner accepts immutable committed
    // snapshots and owns their serial file lifetime. There is at most one live
    // document worker, including retirement, so fast re-entry cannot race a lease.
    public sealed class BlueprintSceneHistory
    {
        private sealed class Lease
        {
            internal string Scope;
            internal DocumentWorker<BlueprintSceneDocument> Worker;
            internal bool Failed, Unknown;
            internal string Error, Identity;
            internal bool RecordsOnly;
            internal BlueprintSceneDocument Recovery;
        }
        private sealed class Failure
        {
            internal string Error, Identity;
            internal bool Unknown, RecordsOnly;
            internal BlueprintSceneDocument Recovery;
        }
        private sealed class RecordRecovery
        {
            internal BlueprintSceneRecord Record;
            internal bool Remove;
            internal string Identity;
        }
        private RecordRecovery repair;
        private BlueprintSceneRecord repaired;
        private bool repairedRemoved, repairFailed, recordsOnly, verifyCachedRestore;
        private string confirmedIdentity;
        private readonly Func<string, IPreferenceStorage> storage;
        private readonly Action<IPreferenceStorage, BlueprintSceneDocument> validatedLoad;
        private readonly Dictionary<string, Failure> failures = new Dictionary<string, Failure>(StringComparer.Ordinal);
        private Lease current, retired;
        private string requestedScope;
        private int width, height;
        private bool stopping;
        private long command;
        private BlueprintSceneDocument latest, sent, confirmed;
        public BlueprintSceneHistory(Func<string, IPreferenceStorage> storage, Action<IPreferenceStorage, BlueprintSceneDocument> validatedLoad = null)
        { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); this.validatedLoad = validatedLoad; }
        public long Generation { get; private set; } = -1;
        public bool Loaded { get; private set; }
        public bool Protected { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public string Error { get; private set; }
        public string BackgroundError { get { return requestedScope != null && failures.TryGetValue(requestedScope, out var failure) ? failure.Error : null; } }
        public BlueprintSceneDocument Restored { get; private set; }
        public bool Saved { get { return latest != null && ReferenceEquals(latest, confirmed); } }
        public BlueprintSceneDocument Confirmed { get { return confirmed ?? Restored; } }
        public bool RecordRecoveryBusy { get { return repair != null || repaired != null || repairFailed; } }
        public bool CanRecoverRecord { get { return Loaded && Protected && recordsOnly && !CommitUnconfirmed &&
            confirmedIdentity != null && sent == null && !RecordRecoveryBusy && retired == null; } }
        public void BeginSession(long generation, int worldWidth, int worldHeight)
        { EndSession(); Generation = generation; width = worldWidth; height = worldHeight; }
        public void UseScope(string scope)
        {
            BlueprintSceneDocument.ValidateScope(scope); if (stopping || Generation < 0) return;
            if (requestedScope != null && requestedScope != scope) { Protect("blueprint-scene-identity-changed"); return; }
            requestedScope = scope; Bind();
        }
        private void Bind()
        {
            Reap(); if (stopping || current != null || requestedScope == null || retired != null || Protected && repair == null) return;
            Failure failure;
            if (repair == null && failures.TryGetValue(requestedScope, out failure))
            {
                Loaded = true; Protected = true; Error = failure.Error; CommitUnconfirmed = failure.Unknown;
                Restored = failure.Recovery != null && failure.Recovery.Width == width && failure.Recovery.Height == height ? failure.Recovery : null;
                confirmedIdentity = failure.Identity; recordsOnly = failure.RecordsOnly;
                verifyCachedRestore = recordsOnly && Restored != null && confirmedIdentity != null && !CommitUnconfirmed;
                if (!verifyCachedRestore) return;
                Loaded = false;
            }
            string scope = requestedScope; int w = width, h = height;
            var file = storage(scope);
            current = new Lease { Scope = scope, Failed = Protected, Error = Error, RecordsOnly = recordsOnly,
                Worker = new DocumentWorker<BlueprintSceneDocument>(file, bytes =>
            {
                var value = BlueprintSceneCodec.Decode(bytes, scope, w, h);
                validatedLoad?.Invoke(file, value);
                return value;
            }, BlueprintSceneCodec.Encode, new BlueprintSceneDocument(scope, w, h, 0, Array.Empty<BlueprintSceneRecord>())) };
        }
        public bool Offer(IReadOnlyList<BlueprintSceneRecord> records)
        {
            if (stopping || !Loaded || Protected || current == null || latest == null) return false;
            latest = new BlueprintSceneDocument(requestedScope, width, height, checked(latest.Revision + 1), records); return true;
        }
        public void Protect(string error)
        { SetProtected(error, false); }
        public void ProtectRecord(string error)
        { SetProtected(error, true); }
        private void SetProtected(string error, bool record)
        {
            recordsOnly = record && (!Protected || recordsOnly);
            Protected = true; Error = error ?? "blueprint-scene-protected";
            if (requestedScope != null) failures[requestedScope] = new Failure { Error = Error, Unknown = CommitUnconfirmed,
                Recovery = CommitUnconfirmed ? null : Confirmed, Identity = confirmedIdentity, RecordsOnly = recordsOnly };
            if (current != null) { current.Failed = true; current.Error = Error; current.RecordsOnly = recordsOnly; }
        }
        public bool RequestRecordRecovery(BlueprintSceneRecord record, bool remove)
        {
            if (!CanRecoverRecord || record == null || Confirmed == null) return false;
            // A row obtained from the owner names precisely one persisted
            // record. The fresh worker must read those same bytes before any
            // proof or mutation is accepted.
            bool found = false;
            foreach (var saved in Confirmed.Records) if (ReferenceEquals(record, saved)) { found = true; break; }
            if (!found) return false;
            repair = new RecordRecovery { Record = record, Remove = remove, Identity = confirmedIdentity };
            RetireCurrent(null);
            return true;
        }
        public bool TryTakeRecordRecovery(out BlueprintSceneRecord record, out bool removed)
        {
            record = repaired; removed = repairedRemoved;
            if (record == null && !repairFailed) return false;
            repaired = null; repairedRemoved = repairFailed = false;
            return true;
        }
        // Host calls after the last missing record is proved healthy or a
        // reliable remove is collected. A cached read from an old session
        // cannot release protection without a new storage lease.
        public void CompleteRecordRecovery()
        {
            if (!recordsOnly || current == null || repair != null || CommitUnconfirmed) return;
            failures.Remove(requestedScope); Protected = recordsOnly = current.Failed = current.RecordsOnly = false;
            Error = current.Error = null;
            latest = confirmed = Confirmed;
        }
        private void TakeRepairResult(DocumentResult<BlueprintSceneDocument> result)
        {
            if (!result.Success || result.IsProtected || result.CommitUnconfirmed)
            {
                CommitUnconfirmed |= result.CommitUnconfirmed;
                current.Unknown |= result.CommitUnconfirmed;
                // A definite non-protected write rejection can be retried
                // from the same authoritative base. Conflicts cannot.
                SetProtected(result.Error ?? "blueprint-scene-repair-failed",
                    result.CommandId != 0 && !result.IsProtected && !result.CommitUnconfirmed &&
                    result.Error != PreferenceStatus.Conflict.ToString());
                repair = null; repairFailed = true; return;
            }
            if (result.CommandId == 0)
            {
                if (!String.Equals(result.StorageIdentity, repair.Identity, StringComparison.Ordinal))
                { Protect(PreferenceStatus.Conflict.ToString()); repair = null; repairFailed = true; return; }
                if (!repair.Remove)
                {
                    repaired = repair.Record; repair = null;
                    return;
                }
                var remaining = new List<BlueprintSceneRecord>(result.Value.Records.Count - 1);
                foreach (var item in result.Value.Records) if (item.Order != repair.Record.Order) remaining.Add(item);
                if (remaining.Count != result.Value.Records.Count - 1)
                { Protect(PreferenceStatus.Conflict.ToString()); repair = null; repairFailed = true; return; }
                if (result.Value.Revision == Int64.MaxValue)
                { Protect("blueprint-scene-revision-limit"); repair = null; repairFailed = true; return; }
                var replacement = new BlueprintSceneDocument(requestedScope, width, height, result.Value.Revision + 1, remaining);
                if (!current.Worker.TrySubmit(++command, replacement))
                { Protect("blueprint-scene-repair-submit"); repair = null; repairFailed = true; }
                return;
            }
            confirmedIdentity = result.StorageIdentity; latest = confirmed = Restored = result.Value;
            // Keep the same-process fallback in sync even if another record
            // still needs repair or the player switches world immediately.
            ProtectRecord(Error);
            repaired = repair.Record; repairedRemoved = true; repair = null;
        }
        public void Poll()
        {
            Reap(); if (stopping || Generation < 0) return; Bind(); if (current == null) return;
            DocumentResult<BlueprintSceneDocument> result;
            if (current.Worker.TryTake(out result))
            {
                if (repair != null) { TakeRepairResult(result); return; }
                if (result.CommandId == 0)
                {
                    Loaded = true;
                    if (verifyCachedRestore)
                    {
                        verifyCachedRestore = false;
                        if (result.Success && String.Equals(result.StorageIdentity, confirmedIdentity, StringComparison.Ordinal))
                            latest = confirmed = Restored = result.Value;
                        else if (result.Success) Protect(PreferenceStatus.Conflict.ToString());
                    }
                    else if (result.Success && !Protected) { latest = confirmed = Restored = result.Value; confirmedIdentity = result.StorageIdentity; }
                }
                else
                {
                    if (result.Success) { confirmed = sent; confirmedIdentity = result.StorageIdentity; if (recordsOnly) ProtectRecord(Error); }
                    sent = null;
                }
                CommitUnconfirmed |= result.CommitUnconfirmed;
                if (!result.Success || result.IsProtected || result.CommitUnconfirmed) Protect(result.Error ?? "blueprint-scene-save-failed");
                current.Unknown |= result.CommitUnconfirmed;
            }
            if (Loaded && !Protected && sent == null && latest != null && !ReferenceEquals(latest, confirmed) && current.Worker.TrySubmit(++command, latest)) sent = latest;
        }
        public void EndSession()
        {
            RetireCurrent(!Protected ? latest : null);
            requestedScope = null; width = height = 0; Generation = -1; Loaded = Protected = CommitUnconfirmed = false; Error = null;
            latest = sent = confirmed = Restored = null; confirmedIdentity = null;
            repair = null; repaired = null; repairedRemoved = repairFailed = recordsOnly = verifyCachedRestore = false; Reap();
        }
        private void RetireCurrent(BlueprintSceneDocument final)
        {
            if (current == null) return;
            current.Recovery = Confirmed; current.Identity = confirmedIdentity;
            current.Worker.BeginStop(final == null ? (Func<BlueprintSceneDocument, BlueprintSceneDocument>)null : value => ReferenceEquals(value, final) ? value : final);
            retired = current; current = null;
        }
        private void Reap()
        {
            if (retired == null || !retired.Worker.IsFinished) return;
            DocumentResult<BlueprintSceneDocument> result;
            if (retired.Worker.TryTake(out result))
            {
                if (!result.Success)
                {
                    retired.Failed = true; retired.Unknown |= result.CommitUnconfirmed; retired.Error = result.Error;
                    if (result.IsProtected || result.CommitUnconfirmed || result.CommandId == 0) retired.RecordsOnly = false;
                }
                else if (!retired.Unknown && (result.CommandId != 0 || retired.Recovery == null))
                { retired.Recovery = result.Value; retired.Identity = result.StorageIdentity; }
            }
            if (retired.Failed)
            {
                Failure previous; failures.TryGetValue(retired.Scope, out previous);
                failures[retired.Scope] = new Failure { Error = retired.Error ?? "blueprint-scene-retired-save-failed", Unknown = retired.Unknown,
                    Recovery = retired.Unknown ? null : retired.Recovery ?? previous?.Recovery,
                    Identity = retired.Identity ?? previous?.Identity, RecordsOnly = retired.RecordsOnly };
            }
            retired = null;
        }
        public bool Stop(int milliseconds)
        {
            if (!stopping) { EndSession(); stopping = true; }
            bool done = retired == null || retired.Worker.Stop(Math.Max(0, milliseconds)); Reap(); return done;
        }
    }
}
