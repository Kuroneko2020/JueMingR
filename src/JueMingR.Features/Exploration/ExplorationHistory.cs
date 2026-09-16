using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Exploration
{
    public sealed class ExplorationHistory
    {
        private sealed class Lease
        {
            internal string Pair;
            internal DocumentWorker<ExplorationSummary> Worker;
            internal bool Failed;
            internal bool Unknown, IdentityFailed;
            internal string Error;
            internal int Width, Height;
            internal ExplorationSummary Final;
        }
        private sealed class Failure { internal string Error; internal bool Unknown; }
        private readonly Func<string, IPreferenceStorage> storage;
        private readonly Func<long> clock;
        private readonly List<Lease> retired = new List<Lease>();
        private readonly Dictionary<string, Failure> failedPairs = new Dictionary<string, Failure>(StringComparer.Ordinal);
        private Lease current;
        private string pair;
        private int width, height, failures;
        private bool stopping, protectedFile, identityFailed;
        private long due, command;
        private ExplorationSummary latest, sent, confirmed;
        public ExplorationHistory(Func<string, IPreferenceStorage> storage, Func<long> clock)
        { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
        public bool Loaded { get; private set; }
        public ExplorationSummary Historical { get; private set; }
        public string Error { get; private set; }
        public string BackgroundError { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public bool Saved { get { return latest != null && ReferenceEquals(latest, confirmed); } }
        public void BeginSession(int worldWidth, int worldHeight)
        { EndSession(); width = worldWidth; height = worldHeight; }
        public void UsePair(string value)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("exploration-pair-invalid");
            foreach (char c in value) if (!(c >= 'a' && c <= 'f' || c >= '0' && c <= '9')) throw new ArgumentException("exploration-pair-invalid");
            if (pair != null && pair != value) { identityFailed = protectedFile = true; Error = "exploration-identity-changed"; if (current != null) current.IdentityFailed = true; return; }
            pair = value; Bind();
        }
        private void Bind()
        {
            if (pair == null || current != null || stopping || protectedFile) return;
            Failure failure;
            if (failedPairs.TryGetValue(pair, out failure)) { protectedFile = true; Loaded = true; Error = failure.Error; CommitUnconfirmed = failure.Unknown; return; }
            if (failedPairs.Count >= 32 || retired.Count >= 32) { protectedFile = true; Loaded = true; Error = "exploration-pending-capacity"; return; }
            if (retired.Count >= 8) { Error = "exploration-pending-capacity"; return; }
            foreach (var old in retired) if (old.Pair == pair) return;
            string captured = pair; int w = width, h = height;
            current = new Lease { Pair = captured, Width = w, Height = h, Worker = Worker(captured, w, h) };
        }
        private DocumentWorker<ExplorationSummary> Worker(string key, int w, int h)
        { return new DocumentWorker<ExplorationSummary>(storage(key), bytes => ExplorationSummary.Decode(bytes, key, w, h), value => value.Encode(key), null); }
        public void Offer(long count, long utcTicks, bool recalibrated = false)
        {
            if (stopping || protectedFile || width <= 0 || height <= 0 || latest != null && latest.Count == count && !recalibrated) return;
            // First-dirty deadline belongs to this unsent batch. Continuous
            // native changes cannot move it into the future indefinitely.
            if (latest == null || ReferenceEquals(latest, confirmed) || ReferenceEquals(latest, sent)) due = clock() + 10000;
            latest = new ExplorationSummary(width, height, count, utcTicks);
        }
        public void Poll()
        {
            Reap(); StartRetired(); if (stopping) return; Bind(); if (current == null) return;
            DocumentResult<ExplorationSummary> result;
            if (current.Worker.TryTake(out result))
            {
                if (result.CommandId == 0) { Loaded = true; Historical = result.Value; protectedFile |= !result.Success; }
                else { if (result.Success) { confirmed = sent; failures = 0; } else { failures++; due = clock() + 1000; } sent = null; }
                protectedFile |= result.IsProtected || result.CommitUnconfirmed; CommitUnconfirmed |= result.CommitUnconfirmed;
                current.Failed = !result.Success; current.Unknown |= result.CommitUnconfirmed; current.Error = Error = identityFailed ? "exploration-identity-changed" : result.Error;
            }
            if (Loaded && !protectedFile && failures < 3 && latest != null && sent == null && !ReferenceEquals(latest, confirmed) && clock() >= due)
                if (current.Worker.TrySubmit(++command, latest)) sent = latest;
        }
        public void EndSession()
        {
            if (current != null)
            {
                current.Final = !protectedFile && !identityFailed ? latest : null;
                Finish(current);
                retired.Add(current);
            }
            else if (pair != null && latest != null && !protectedFile && !identityFailed)
                retired.Add(new Lease { Pair = pair, Width = width, Height = height, Final = latest });
            current = null; pair = null; width = height = failures = 0; latest = sent = confirmed = Historical = null; Loaded = protectedFile = CommitUnconfirmed = identityFailed = false; Error = null; Reap(); StartRetired();
        }
        private static void Finish(Lease lease)
        { var final = lease.Final; lease.Worker.BeginStop(final == null ? (Func<ExplorationSummary, ExplorationSummary>)null : value => final); }
        private void StartRetired()
        {
            int active = 0; foreach (var lease in retired) if (lease.Worker != null) active++;
            for (int i = 0; i < retired.Count && active < 8; i++)
            {
                var lease = retired[i]; if (lease.Worker != null) continue;
                if (failedPairs.ContainsKey(lease.Pair)) { BackgroundError = failedPairs[lease.Pair].Error; retired.RemoveAt(i--); continue; }
                bool prior = false; for (int j = 0; j < i; j++) if (retired[j].Pair == lease.Pair) { prior = true; break; }
                if (prior) continue; lease.Worker = Worker(lease.Pair, lease.Width, lease.Height); active++; Finish(lease);
            }
        }
        private void Reap()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                var old = retired[i]; if (old.Worker == null || !old.Worker.IsFinished) continue;
                DocumentResult<ExplorationSummary> result; if (old.Worker.TryTake(out result)) { old.Failed = !result.Success; old.Unknown |= result.CommitUnconfirmed; old.Error = result.Error; }
                if (old.Failed || old.IdentityFailed)
                { var failure = new Failure { Error = old.IdentityFailed ? "exploration-identity-changed" : old.Error ?? "exploration-retired-save-failed", Unknown = old.Unknown }; failedPairs[old.Pair] = failure; BackgroundError = failure.Error; }
                retired.RemoveAt(i);
            }
        }
        public bool Stop(int milliseconds)
        {
            if (!stopping) { EndSession(); stopping = true; }
            var elapsed = Stopwatch.StartNew(); bool done = true;
            while (retired.Count != 0)
            {
                StartRetired();
                foreach (var old in retired) if (old.Worker != null) done &= old.Worker.Stop(Math.Max(0, milliseconds - (int)elapsed.ElapsedMilliseconds));
                Reap(); if (elapsed.ElapsedMilliseconds >= milliseconds && retired.Count != 0) return false;
            }
            return done;
        }
    }
}
