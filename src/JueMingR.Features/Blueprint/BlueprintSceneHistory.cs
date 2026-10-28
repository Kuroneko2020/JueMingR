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
            internal string Error;
            internal BlueprintSceneDocument Recovery;
        }
        private sealed class Failure { internal string Error; internal bool Unknown; internal BlueprintSceneDocument Recovery; }
        private readonly Func<string, IPreferenceStorage> storage;
        private readonly Dictionary<string, Failure> failures = new Dictionary<string, Failure>(StringComparer.Ordinal);
        private Lease current, retired;
        private string requestedScope;
        private int width, height;
        private bool stopping;
        private long command;
        private BlueprintSceneDocument latest, sent, confirmed;
        public BlueprintSceneHistory(Func<string, IPreferenceStorage> storage) { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); }
        public long Generation { get; private set; } = -1;
        public bool Loaded { get; private set; }
        public bool Protected { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public string Error { get; private set; }
        public string BackgroundError { get; private set; }
        public BlueprintSceneDocument Restored { get; private set; }
        public bool Saved { get { return latest != null && ReferenceEquals(latest, confirmed); } }
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
            Reap(); if (stopping || current != null || requestedScope == null || retired != null || Protected) return;
            Failure failure;
            if (failures.TryGetValue(requestedScope, out failure))
            { Loaded = true; Protected = true; Error = failure.Error; CommitUnconfirmed = failure.Unknown; Restored = failure.Recovery != null && failure.Recovery.Width == width && failure.Recovery.Height == height ? failure.Recovery : null; return; }
            string scope = requestedScope; int w = width, h = height;
            current = new Lease { Scope = scope, Worker = new DocumentWorker<BlueprintSceneDocument>(storage(scope), bytes => BlueprintSceneCodec.Decode(bytes, scope, w, h), BlueprintSceneCodec.Encode, new BlueprintSceneDocument(scope, w, h, 0, Array.Empty<BlueprintSceneRecord>())) };
        }
        public bool Offer(IReadOnlyList<BlueprintSceneRecord> records)
        {
            if (stopping || !Loaded || Protected || current == null || latest == null) return false;
            latest = new BlueprintSceneDocument(requestedScope, width, height, checked(latest.Revision + 1), records); return true;
        }
        public void Protect(string error)
        {
            Protected = true; Error = error ?? "blueprint-scene-protected";
            if (requestedScope != null) failures[requestedScope] = new Failure { Error = Error, Unknown = CommitUnconfirmed, Recovery = CommitUnconfirmed ? null : confirmed ?? Restored };
            if (current != null) { current.Failed = true; current.Error = Error; }
        }
        public void Poll()
        {
            Reap(); if (stopping || Generation < 0) return; Bind(); if (current == null) return;
            DocumentResult<BlueprintSceneDocument> result;
            if (current.Worker.TryTake(out result))
            {
                if (result.CommandId == 0)
                { Loaded = true; if (result.Success && !Protected) latest = confirmed = Restored = result.Value; }
                else { if (result.Success) confirmed = sent; sent = null; }
                CommitUnconfirmed |= result.CommitUnconfirmed;
                if (!result.Success || result.IsProtected || result.CommitUnconfirmed) Protect(result.Error ?? "blueprint-scene-save-failed");
                current.Unknown |= result.CommitUnconfirmed;
            }
            if (Loaded && !Protected && sent == null && latest != null && !ReferenceEquals(latest, confirmed) && current.Worker.TrySubmit(++command, latest)) sent = latest;
        }
        public void EndSession()
        {
            if (current != null)
            {
                var final = !Protected ? latest : null;
                current.Recovery = confirmed ?? Restored;
                current.Worker.BeginStop(final == null ? (Func<BlueprintSceneDocument, BlueprintSceneDocument>)null : value => ReferenceEquals(value, final) ? value : final);
                retired = current; current = null;
            }
            requestedScope = null; width = height = 0; Generation = -1; Loaded = Protected = CommitUnconfirmed = false; Error = null;
            latest = sent = confirmed = Restored = null; Reap();
        }
        private void Reap()
        {
            if (retired == null || !retired.Worker.IsFinished) return;
            DocumentResult<BlueprintSceneDocument> result;
            if (retired.Worker.TryTake(out result))
            {
                if (!result.Success) { retired.Failed = true; retired.Unknown |= result.CommitUnconfirmed; retired.Error = result.Error; }
                else if (!retired.Unknown) retired.Recovery = result.Value;
            }
            if (retired.Failed)
            {
                Failure previous; failures.TryGetValue(retired.Scope, out previous);
                failures[retired.Scope] = new Failure { Error = BackgroundError = retired.Error ?? "blueprint-scene-retired-save-failed", Unknown = retired.Unknown,
                    Recovery = retired.Unknown ? null : retired.Recovery ?? previous?.Recovery };
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
