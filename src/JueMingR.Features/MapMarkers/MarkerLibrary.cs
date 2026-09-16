using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.MapMarkers
{
    // User assets use acknowledged commands, not the overwrite slot used for
    // ordinary preferences. Saved only changes after this owner's receipt.
    public sealed class MarkerLibrary
    {
        private sealed class Lease
        {
            internal string Pair;
            internal DocumentWorker<MarkerDocument> Worker;
            internal bool Failed;
            internal bool Unknown, IdentityFailed;
            internal string Error;
        }
        private sealed class Failure { internal string Error; internal bool Unknown; }
        private readonly Func<string, IPreferenceStorage> storage;
        private readonly List<Lease> retired = new List<Lease>();
        private readonly Dictionary<string, Failure> protectedPairs = new Dictionary<string, Failure>(StringComparer.Ordinal);
        private Lease current;
        private string requestedPair;
        private int width, height;
        private bool stopping, identityFailed;
        private long nextCommand;
        public MarkerLibrary(Func<string, IPreferenceStorage> storage) { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); }
        public MarkerDocument Saved { get; private set; }
        public bool Loaded { get; private set; }
        public bool Protected { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public bool Busy { get { return InFlight != 0; } }
        public long InFlight { get; private set; }
        public long LastOperation { get; private set; }
        public bool LastSuccess { get; private set; }
        public long Generation { get; private set; } = -1;
        public string Error { get; private set; }
        public string BackgroundError { get; private set; }
        public bool CanEdit { get { return !stopping && Loaded && !Protected && !Busy && Saved != null && !Saved.IsReadOnly; } }
        public void BeginSession(long generation, int worldWidth, int worldHeight)
        { EndSession(); Generation = generation; width = worldWidth; height = worldHeight; }
        public void UsePair(string pair)
        {
            MarkerDocument.ValidatePair(pair);
            if (Generation < 0 || stopping) return;
            if (requestedPair != null && requestedPair != pair) { identityFailed = Protected = true; Error = "marker-identity-changed"; if (current != null) current.IdentityFailed = true; return; }
            requestedPair = pair; Bind();
        }
        private void Bind()
        {
            Reap(); if (current != null || requestedPair == null || stopping) return;
            Failure failure;
            if (protectedPairs.TryGetValue(requestedPair, out failure)) { Protected = true; Loaded = true; Error = failure.Error; CommitUnconfirmed = failure.Unknown; return; }
            if (retired.Count >= 8 || protectedPairs.Count >= 32) { Error = "marker-pending-capacity"; return; }
            foreach (var old in retired) if (old.Pair == requestedPair) return;
            string pair = requestedPair; int w = width, h = height;
            current = new Lease { Pair = pair, Worker = new DocumentWorker<MarkerDocument>(storage(pair), bytes => MarkerCodec.Decode(bytes, pair, w, h), MarkerCodec.Encode, new MarkerDocument(pair, w, h, 0, new MarkerRecord[0])) };
        }
        public long Create(MarkerRecord record)
        {
            if (!CanEdit || Saved.Records.Count >= MarkerDocument.MaximumNew) return 0;
            var list = new List<MarkerRecord>(Saved.Records); list.Add(record); return Submit(list);
        }
        public long Rename(string id, string name)
        {
            if (!CanEdit) return 0; var list = new List<MarkerRecord>(Saved.Records);
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id)
            { var old = list[i]; if (old.Name == name) return -1; list[i] = new MarkerRecord(old.Id, old.X, old.Y, old.Icon, name); return Submit(list); }
            return 0;
        }
        public long Delete(string id)
        {
            if (!CanEdit) return 0; var list = new List<MarkerRecord>(Saved.Records);
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) { list.RemoveAt(i); return Submit(list); } return 0;
        }
        private long Submit(List<MarkerRecord> records)
        {
            var next = new MarkerDocument(Saved.Pair, width, height, checked(Saved.Revision + 1), records);
            long id = checked(++nextCommand); if (!current.Worker.TrySubmit(id, next)) return 0;
            InFlight = id; Error = null; return id;
        }
        public void Poll()
        {
            Reap(); if (stopping || Generation < 0) return; Bind(); if (current == null) return;
            DocumentResult<MarkerDocument> result; if (!current.Worker.TryTake(out result)) return;
            if (result.CommandId == 0) { Loaded = true; Protected |= !result.Success; }
            else
            {
                if (result.CommandId != InFlight) { Protected = true; Error = "marker-unexpected-receipt"; return; }
                LastOperation = result.CommandId; LastSuccess = result.Success; InFlight = 0;
            }
            if (result.Success) Saved = result.Value;
            CommitUnconfirmed |= result.CommitUnconfirmed; Protected |= result.IsProtected || result.CommitUnconfirmed;
            Error = identityFailed ? "marker-identity-changed" : result.Error; current.Failed = !result.Success; current.Unknown |= result.CommitUnconfirmed; current.Error = Error;
        }
        public void EndSession()
        {
            if (current != null) { current.Worker.BeginStop(); retired.Add(current); }
            current = null; requestedPair = null; Saved = null; Loaded = Protected = CommitUnconfirmed = identityFailed = false; InFlight = LastOperation = 0; LastSuccess = false; Generation = -1; Error = null; Reap();
        }
        public string TakeBackgroundError() { string value = BackgroundError; BackgroundError = null; return value; }
        private void Reap()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                var old = retired[i]; if (!old.Worker.IsFinished) continue;
                DocumentResult<MarkerDocument> result;
                if (old.Worker.TryTake(out result)) { old.Failed = !result.Success; old.Unknown |= result.CommitUnconfirmed; old.Error = result.Error; }
                if (old.Failed || old.IdentityFailed) { var failure = new Failure { Error = old.IdentityFailed ? "marker-identity-changed" : old.Error ?? "marker-retired-save-failed", Unknown = old.Unknown }; protectedPairs[old.Pair] = failure; BackgroundError = failure.Error; }
                retired.RemoveAt(i);
            }
        }
        public bool Stop(int milliseconds)
        {
            if (!stopping) { EndSession(); stopping = true; }
            var elapsed = Stopwatch.StartNew(); bool done = true;
            foreach (var old in retired) done &= old.Worker.Stop(Math.Max(0, milliseconds - (int)elapsed.ElapsedMilliseconds)); Reap(); return done;
        }
    }
}
