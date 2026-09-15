using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.WorldTime
{
    public sealed class WorldTimeHistory
    {
        private sealed class Session
        {
            internal string Pair;
            internal double Base, Delta, Confirmed, Sent;
            internal long Due, Command;
            internal int Failures;
            internal bool Known, Protected, InFlight;
            internal bool Unconfirmed;
            internal string Error;
            internal DocumentWorker<WorldTimeValue> Worker;
        }
        private sealed class Failure { internal string Error; internal bool Unconfirmed; internal double RetainedDelta; }
        private readonly Func<string, IPreferenceStorage> factory;
        private readonly Func<long> clock;
        private readonly List<Session> retired = new List<Session>();
        private readonly Dictionary<string, Failure> failedPairs = new Dictionary<string, Failure>(StringComparer.Ordinal);
        private Session current;
        private bool stopping;
        public WorldTimeHistory(Func<string, IPreferenceStorage> factory, Func<long> clock)
        { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
        public bool Known { get { return current != null && current.Known; } }
        public double Total { get { return current == null ? 0 : current.Base + current.Delta; } }
        public long Days { get { return (long)Math.Floor(Total / 86400); } }
        public bool Saved { get { return Known && !current.Protected && current.Failures == 0 && current.Delta == current.Confirmed; } }
        public bool HasPair { get { return current?.Pair != null; } }
        public string Error { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public string BackgroundError { get; private set; }
        public long Generation { get; private set; }
        public void BeginSession(long generation)
        {
            EndSession(); if (stopping) return; Reap();
            if (retired.Count >= 32 || failedPairs.Count >= 32) { Error = "world-time-pending-capacity"; return; }
            current = new Session(); Generation = generation; Error = null; CommitUnconfirmed = false;
        }
        public void EndSession()
        {
            if (current?.Pair != null)
            {
                // A reliable pair may still be waiting for its old file lease.
                // Preserve this session's finite scalar tail even without a
                // worker; otherwise quick re-entry silently loses elapsed time.
                retired.Add(current);
                if (current.Worker != null) Finish(current);
            }
            current = null; Reap(); StartRetired();
        }
        private static void Finish(Session session)
        {
            bool known = session.Known; double delta = session.Delta, total = session.Base + delta;
            session.Worker.BeginStop(value =>
            { double final = known ? Math.Max(value.Total, total) : value.Total + delta; return final == value.Total ? value : new WorldTimeValue(final); });
        }
        public void Advance(double contribution)
        {
            var session = current; if (session == null || stopping || contribution == 0) return;
            if (contribution < 0 || Double.IsNaN(contribution) || Double.IsInfinity(contribution) || session.Base + session.Delta + contribution > 86400d * 1000000000)
            { Error = "invalid-world-time-contribution"; return; }
            if (session.Delta == session.Confirmed) session.Due = clock() + 10000;
            session.Delta += contribution;
        }
        public void UsePair(string pair)
        {
            if (current == null || stopping) return;
            if (pair == null || pair.Length != 64) throw new ArgumentException("invalid-world-time-pair");
            foreach (char c in pair) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new ArgumentException("invalid-world-time-pair");
            if (current.Pair != null && current.Pair != pair) { Error = "world-time-identity-changed"; return; }
            current.Pair = pair; Bind();
        }
        private void Bind()
        {
            Reap(); StartRetired(); if (current == null || current.Pair == null || current.Worker != null || ActiveWorkers() >= 8) return;
            Failure failure;
            if (failedPairs.TryGetValue(current.Pair, out failure)) { Error = failure.Error; CommitUnconfirmed = failure.Unconfirmed; current.Protected = true; return; }
            foreach (var old in retired) if (old.Pair == current.Pair) return;
            current.Worker = Worker(current.Pair);
        }
        private DocumentWorker<WorldTimeValue> Worker(string pair)
        { return new DocumentWorker<WorldTimeValue>(factory(pair), bytes => WorldTimeValue.Decode(bytes, pair), value => value.Encode(pair), new WorldTimeValue(0)); }
        private int ActiveWorkers() { int count = 0; foreach (var old in retired) if (old.Worker != null) count++; return count; }
        private void StartRetired()
        {
            int active = ActiveWorkers();
            for (int i = 0; i < retired.Count && active < 8; i++)
            {
                var session = retired[i]; if (session.Worker != null) continue; bool prior = false;
                Failure failed;
                if (failedPairs.TryGetValue(session.Pair, out failed)) { failed.RetainedDelta += session.Delta; retired.RemoveAt(i--); continue; }
                for (int j = 0; j < i; j++) if (retired[j].Pair == session.Pair) { prior = true; break; }
                if (prior) continue; session.Worker = Worker(session.Pair); active++; Finish(session);
            }
        }
        private void Reap()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                var session = retired[i]; if (session.Worker == null || !session.Worker.IsFinished) continue;
                DocumentResult<WorldTimeValue> result;
                if (session.Worker.TryTake(out result))
                {
                    if (!result.Success) { session.Error = result.Error; session.Unconfirmed |= result.CommitUnconfirmed; session.Protected = true; }
                    else if (!session.Protected && !session.Unconfirmed)
                    { session.Failures = 0; session.Error = null; session.Confirmed = session.Delta; }
                }
                if (session.Protected || session.Failures != 0)
                {
                    // Thread completion proves ownership was released, not
                    // persistence. Retain the failed final receipt and prevent
                    // same-process re-entry from silently clearing its fence.
                    var failure = new Failure { Error = session.Error ?? "world-time-final-save-failed", Unconfirmed = session.Unconfirmed, RetainedDelta = session.Delta - session.Confirmed };
                    failedPairs[session.Pair] = failure; BackgroundError = failure.Error;
                }
                retired.RemoveAt(i);
            }
        }
        public void Poll()
        {
            if (stopping) return; Reap(); StartRetired(); if (current == null) return; var session = current;
            if (session.Worker == null) { Bind(); if (session.Worker == null) return; }
            DocumentResult<WorldTimeValue> result;
            if (session.Worker.TryTake(out result))
            {
                if (result.CommandId == 0)
                { session.Known = result.Success; if (result.Success) session.Base = result.Value.Total; else session.Protected = true; }
                else
                { session.InFlight = false; if (result.Success) { session.Confirmed = session.Sent; session.Failures = 0; } else { session.Failures++; session.Due = clock() + 1000; } }
                session.Protected |= result.IsProtected; session.Unconfirmed |= result.CommitUnconfirmed; CommitUnconfirmed |= result.CommitUnconfirmed; session.Error = Error = result.Success ? null : result.Error;
            }
            // This deadline is checked even if native time is now frozen. It
            // depends on neither a new positive contribution nor visible days.
            if (session.Known && !session.Protected && !session.InFlight && session.Failures < 3 && session.Delta != session.Confirmed && clock() >= session.Due)
            {
                var value = new WorldTimeValue(session.Base + session.Delta);
                if (session.Worker.TrySubmit(++session.Command, value)) { session.Sent = session.Delta; session.InFlight = true; }
            }
        }
        public bool Stop(int milliseconds)
        {
            if (!stopping) { EndSession(); stopping = true; }
            var elapsed = Stopwatch.StartNew(); bool complete = true;
            while (retired.Count != 0)
            {
                StartRetired();
                foreach (var session in retired) if (session.Worker != null) complete &= session.Worker.Stop(Math.Max(0, milliseconds - (int)elapsed.ElapsedMilliseconds));
                Reap(); if (retired.Count == 0) return complete;
                if (elapsed.ElapsedMilliseconds >= milliseconds) return false;
            }
            return complete;
        }
    }
}
