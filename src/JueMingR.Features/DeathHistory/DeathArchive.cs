using System;
using System.Collections.Generic;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.DeathHistory
{
    // Synchronous domain engine; the history worker is its sole runtime caller.
    public sealed class DeathArchive
    {
        private readonly IDeathArchiveFiles files;
        private readonly string pair;
        private readonly Func<bool> cancelled;
        private readonly DeathTimeIndex index;
        private string tree = "", last = "", position = "", identity;
        private bool loaded, protectedFile;
        public DeathArchive(IDeathArchiveFiles files, string pair, Func<bool> cancelled = null)
        {
            if (files == null || !DeathArchiveCodec.PageId(pair)) throw new ArgumentException("invalid-death-archive");
            this.files = files; this.pair = pair; this.cancelled = cancelled ?? (() => false); index = new DeathTimeIndex(files, this.cancelled);
        }
        public long Count { get; private set; }
        public bool IsProtected { get { return protectedFile; } }
        public bool CommitUnconfirmed { get; private set; }
        public void Load()
        {
            if (loaded) return;
            try
            {
                var result = files.ReadRoot(); identity = result.Identity;
                if (result.Status == PreferenceReadStatus.Loaded)
                {
                    long count; DeathArchiveCodec.Root(result.Contents, pair, out count, out tree, out last, out position); index.Validate(tree, count);
                    // Count, index and occurrence chain must describe the same
                    // facts before the frontend may publish a reliable total.
                    string current = last, valid = position;
                    for (long occurrence = count; occurrence > 0; occurrence--)
                    {
                        Check(); var header = Header(current);
                        if (header.Occurrence != occurrence || index.Find(tree, header.Id) != current) throw PreferenceJson.Invalid();
                        DeathArchiveCodec.Text(files.ReadPage(header.Text));
                        if (header.Position) { if (valid != current) throw PreferenceJson.Invalid(); valid = header.PreviousPosition; }
                        else if (header.PreviousPosition != valid) throw PreferenceJson.Invalid();
                        current = header.Previous;
                    }
                    if (current != "" || valid != "") throw PreferenceJson.Invalid(); Count = count;
                }
                else if (result.Status != PreferenceReadStatus.Missing) throw new InvalidOperationException(result.Error ?? "death-history-read-failed");
                loaded = true;
            }
            catch { protectedFile = true; throw; }
        }
        public void Append(DeathFact[] facts)
        {
            if (!loaded || protectedFile) throw new InvalidOperationException("death-history-not-writable");
            if (facts == null || facts.Length > 64) throw new ArgumentException("death-batch-limit");
            string candidateTree = tree, candidateLast = last, candidatePosition = position; long count = Count;
            foreach (DeathFact fact in facts)
            {
                Check(); if (fact == null) throw new ArgumentException("missing-death-fact");
                string existing = index.Find(candidateTree, fact.EventId);
                if (existing != null)
                {
                    var old = Fact(Header(existing));
                    if (!old.SameSource(fact))
                    { protectedFile = true; throw new InvalidOperationException("death-event-identity-conflict"); }
                    continue;
                }
                string text = files.CreatePage(DeathArchiveCodec.Text(fact.Reason, fact.DirectCause));
                var header = new DeathHeader { Id = fact.EventId, Offset = checked((short)fact.Time.Offset.TotalMinutes), Text = text, Previous = candidateLast,
                    PreviousPosition = candidatePosition, Occurrence = checked(count + 1), Position = fact.HasPosition, X = fact.X, Y = fact.Y };
                byte[] bytes = DeathArchiveCodec.Header(header); DeathArchiveCodec.Header(bytes);
                string id = files.CreatePage(bytes); candidateTree = index.Insert(candidateTree, fact.EventId, id); candidateLast = id;
                if (fact.HasPosition) candidatePosition = id; count++;
            }
            if (count == Count) return;
            var root = DeathArchiveCodec.Root(pair, count, candidateTree, candidateLast, candidatePosition);
            Check();
            PreferenceWriteResult saved;
            // Escaping after entry into the mechanical commit is ambiguous;
            // retain all pages and never replay on the assumption it failed.
            try { saved = files.WriteRoot(identity, root); }
            catch { CommitUnconfirmed = protectedFile = true; throw; }
            if (saved.Status != PreferenceWriteStatus.Saved)
            { CommitUnconfirmed = saved.CommitUnconfirmed; protectedFile |= saved.IsProtected || CommitUnconfirmed || saved.Status != PreferenceWriteStatus.IoFailure; throw new InvalidOperationException(saved.Error ?? "death-history-write-failed"); }
            identity = saved.Identity; tree = candidateTree; last = candidateLast; position = candidatePosition; Count = count;
        }
        public DeathFact[] Page(long offset, int count)
        {
            if (!loaded || offset < 0 || count < 1 || count > 6) throw new ArgumentException("invalid-death-page");
            string[] ids = index.Page(tree, offset, count); var values = new DeathFact[ids.Length];
            for (int i = 0; i < ids.Length; i++) values[i] = Fact(Header(ids[i])); return values;
        }
        public DeathFact Find(string eventId)
        { if (!loaded) return null; string id = index.Find(tree, eventId); return id == null ? null : Fact(Header(id)); }
        public long Before(string eventId) { if (!loaded) throw new InvalidOperationException("death-history-not-loaded"); return index.Rank(tree, eventId); }
        internal void Protect() { protectedFile = true; }
        public DeathMarker[] Recent(int count)
        {
            if (!loaded || count != 128 && count != 256 && count != 512 && count != 1024) throw new ArgumentException("invalid-death-marker-count");
            var values = new List<DeathMarker>(count); string id = position; long previous = Int64.MaxValue;
            while (id != "" && values.Count < count)
            { Check(); var header = Header(id); if (!header.Position || header.Occurrence >= previous) throw PreferenceJson.Invalid(); values.Add(header.Marker); previous = header.Occurrence; id = header.PreviousPosition; }
            return values.ToArray();
        }
        private DeathHeader Header(string id) { if (!DeathArchiveCodec.PageId(id)) throw PreferenceJson.Invalid(); return DeathArchiveCodec.Header(files.ReadPage(id)); }
        private DeathFact Fact(DeathHeader header)
        {
            string cause; string original = DeathArchiveCodec.Text(files.ReadPage(header.Text), out cause);
            return new DeathFact(header.Id, TimeSpan.FromMinutes(header.Offset), header.Position, header.X, header.Y, original, cause);
        }
        private void Check() { if (cancelled()) throw new OperationCanceledException(); }
    }
}
