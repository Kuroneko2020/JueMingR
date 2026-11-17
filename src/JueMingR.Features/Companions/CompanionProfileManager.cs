using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JueMingR.Platform.Companions;

namespace JueMingR.Features.Companions
{
    // Game-thread semantic owner. One background worker performs mechanical I/O;
    // completion never replaces a newer in-memory resource revision with disk data.
    public sealed class CompanionProfileManager : IDisposable
    {
        private sealed class Entry
        {
            internal CompanionProfile Profile;
            internal bool Published;
            internal long SavedRevision;
            internal long Activation;
            internal string SaveError;
        }
        private readonly ICompanionArchive archive;
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly Dictionary<string, Guid> owners = new Dictionary<string, Guid>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> ownerErrors = new Dictionary<Guid, string>();
        private readonly object gate = new object();
        private readonly Queue<Action> work = new Queue<Action>();
        private readonly Queue<Action> completions = new Queue<Action>();
        private readonly Dictionary<Guid, CompanionProfile> pending = new Dictionary<Guid, CompanionProfile>();
        private Task worker;
        private bool running, disposed;
        private string ownerKey;
        private long nextActivation;
        private IReadOnlyList<CompanionProfile> profiles = new CompanionProfile[0];
        public CompanionProfileManager(ICompanionArchive archive)
        { this.archive = archive ?? throw new ArgumentNullException(nameof(archive)); }
        public Guid OwnerId { get; private set; }
        public bool Ready { get { return OwnerId != Guid.Empty; } }
        public string Error { get; private set; }
        public IReadOnlyList<CompanionProfile> Profiles { get { return profiles; } }
        public void RetryLoad()
        { string retry = ownerKey; ownerKey = null; SetOwner(retry); }

        public void SetOwner(string characterKey)
        {
            if (disposed || String.Equals(ownerKey, characterKey, StringComparison.Ordinal)) return;
            ownerKey = characterKey; OwnerId = Guid.Empty; profiles = new CompanionProfile[0]; Error = null;
            if (String.IsNullOrWhiteSpace(characterKey)) return;
            Guid known;
            if (owners.TryGetValue(characterKey, out known)) { OwnerId = known; RefreshError(); Refresh(); return; }
            string requested = characterKey;
            Enqueue(() =>
            {
                try
                {
                    var loaded = archive.LoadOwner(requested);
                    Complete(() =>
                    {
                        MergeLoaded(requested, loaded);
                        if (ownerKey == requested) { OwnerId = loaded.OwnerId; RefreshError(); Refresh(); }
                    });
                }
                catch (Exception exception) { Complete(() => { if (ownerKey == requested) Error = exception.Message; }); }
            });
        }

        public void MigrateOwner(string oldKey, string newKey)
        {
            if (String.IsNullOrWhiteSpace(oldKey) || String.IsNullOrWhiteSpace(newKey) || oldKey == newKey) return;
            // Only a successful native local/cloud move supplies this pair. Names
            // and coincidentally equal inventory bytes never establish ownership.
            Enqueue(() =>
            {
                try
                {
                    var source = archive.LoadOwner(oldKey); archive.LinkOwner(newKey, source.OwnerId);
                    Complete(() =>
                    {
                        // A menu-time migration can precede the first SetOwner.
                        // Cache the loaded profiles and errors with the alias;
                        // a known-key fast path must not hide the actual roster.
                        MergeLoaded(oldKey, source); owners[newKey] = source.OwnerId;
                        if (ownerKey == oldKey || ownerKey == newKey) OwnerId = source.OwnerId;
                        if (OwnerId == source.OwnerId) { RefreshError(); Refresh(); }
                    });
                }
                catch (Exception exception) { Complete(() => Error = exception.Message); }
            });
        }

        private void MergeLoaded(string key, CompanionOwnerArchive loaded)
        {
            owners[key] = loaded.OwnerId; ownerErrors[loaded.OwnerId] = loaded.Error;
            foreach (var profile in loaded.Profiles)
                // The game-thread owner may already contain a later unsaved
                // resource revision. Loading an alias must never replace it.
                if (!entries.ContainsKey(profile.Id)) entries.Add(profile.Id, new Entry { Profile = profile, Published = true, SavedRevision = profile.Revision });
        }
        private void RefreshError()
        {
            string loadError; ownerErrors.TryGetValue(OwnerId, out loadError);
            var messages = new List<string>();
            if (loadError != null) messages.Add(loadError);
            bool saveError = false;
            foreach (var entry in entries.Values)
                if (entry.Profile.OwnerId == OwnerId && entry.SaveError != null) { messages.Add(entry.SaveError); saveError = true; }
            if (saveError) messages.Add("当前人物数据保留在内存，请重试保存。");
            Error = messages.Count == 0 ? null : String.Join("；", messages);
        }

        public CompanionProfile Find(Guid id)
        { Entry entry; return entries.TryGetValue(id, out entry) && entry.Profile.OwnerId == OwnerId ? entry.Profile : null; }
        public CompanionProfile FindName(string name)
        {
            foreach (var entry in entries.Values)
                if (entry.Profile.OwnerId == OwnerId && String.Equals(entry.Profile.Name, name, StringComparison.OrdinalIgnoreCase)) return entry.Profile;
            return null;
        }
        public void Add(CompanionProfile profile)
        {
            if (!Ready || profile.OwnerId != OwnerId || entries.ContainsKey(profile.Id)) throw new InvalidOperationException("当前创建者不可创建此人物。");
            if (FindName(profile.Name) != null) throw new InvalidOperationException("已有此人物。");
            entries.Add(profile.Id, new Entry { Profile = profile }); QueueSave(profile);
        }
        public void Update(CompanionProfile profile)
        {
            Entry entry;
            if (!entries.TryGetValue(profile.Id, out entry) || profile.OwnerId != entry.Profile.OwnerId || profile.Revision <= entry.Profile.Revision || profile.DataEpoch < entry.Profile.DataEpoch)
                throw new InvalidOperationException("队友人物修订已失效。");
            if (entry.Activation != 0 && profile.DataEpoch != entry.Profile.DataEpoch)
                throw new InvalidOperationException("请先收回队友再清空人物存档。");
            entry.Profile = profile; Refresh(); QueueSave(profile);
        }
        public bool TryReserve(Guid id, out long generation)
        {
            generation = 0; Entry entry;
            if (!entries.TryGetValue(id, out entry) || entry.Profile.OwnerId != OwnerId || entry.Profile.PermanentlyDead || entry.SavedRevision == 0 || entry.Activation != 0) return false;
            entry.Activation = generation = checked(++nextActivation); return true;
        }
        public bool OwnsActivation(Guid id, long generation)
        { Entry entry; return generation > 0 && entries.TryGetValue(id, out entry) && entry.Activation == generation; }
        public void Release(Guid id, long generation)
        { Entry entry; if (generation > 0 && entries.TryGetValue(id, out entry) && entry.Activation == generation) entry.Activation = 0; }
        public bool IsSaved(Guid id)
        { Entry entry; return entries.TryGetValue(id, out entry) && entry.SavedRevision == entry.Profile.Revision; }
        public bool SaveFailed(Guid id)
        { Entry entry; return entries.TryGetValue(id, out entry) && entry.SaveError != null; }
        public string SaveError(Guid id)
        { Entry entry; return entries.TryGetValue(id, out entry) ? entry.SaveError : null; }
        public void RetrySave(Guid id)
        { var profile = Find(id); if (profile != null) QueueSave(profile); }
        public void Pump()
        {
            while (true)
            {
                Action completion;
                lock (gate) { if (completions.Count == 0) break; completion = completions.Dequeue(); }
                completion();
            }
        }
        private void Refresh()
        {
            var result = new List<CompanionProfile>();
            foreach (var entry in entries.Values) if (entry.Published && entry.Profile.OwnerId == OwnerId) result.Add(entry.Profile);
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)); profiles = result.AsReadOnly();
        }
        private void QueueSave(CompanionProfile profile)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(CompanionProfileManager));
                bool scheduled = pending.ContainsKey(profile.Id); pending[profile.Id] = profile;
                if (!scheduled) work.Enqueue(() => SavePending(profile.Id));
                StartWorker();
            }
        }
        private void SavePending(Guid id)
        {
            CompanionProfile candidate;
            lock (gate) { candidate = pending[id]; pending.Remove(id); }
            try
            {
                archive.Save(candidate);
                Complete(() =>
                {
                    Entry entry;
                    if (!entries.TryGetValue(id, out entry)) return;
                    entry.SavedRevision = Math.Max(entry.SavedRevision, candidate.Revision);
                    entry.Published = true; entry.SaveError = null;
                    RefreshError(); Refresh();
                });
            }
            catch (Exception exception)
            {
                // Keep the latest native bytes even after recall. A failed disk
                // write is never permission to reload an older bag or grant a kit.
                Complete(() =>
                {
                    Entry entry;
                    if (entries.TryGetValue(id, out entry))
                    {
                        entry.SaveError = exception.Message;
                        // A failed first commit is a visible recoverable draft,
                        // never an activatable character or a second starter kit.
                        entry.Published = true; Refresh();
                    }
                    RefreshError();
                });
            }
        }
        private void Enqueue(Action action)
        { lock (gate) { if (disposed) return; work.Enqueue(action); StartWorker(); } }
        private void Complete(Action action) { lock (gate) completions.Enqueue(action); }
        private void StartWorker()
        { if (!running) { running = true; worker = Task.Run((Action)Drain); } }
        private void Drain()
        {
            while (true)
            {
                Action action;
                lock (gate) { if (work.Count == 0) { running = false; return; } action = work.Dequeue(); }
                action();
            }
        }
        public void Dispose()
        {
            Task final;
            lock (gate) { if (disposed) return; disposed = true; final = worker; }
            // Only the final host shutdown joins the worker. Update/Draw and
            // individual recall never wait for disk or block another actor.
            if (final != null) final.GetAwaiter().GetResult();
            Pump(); archive.Dispose();
        }
    }
}
