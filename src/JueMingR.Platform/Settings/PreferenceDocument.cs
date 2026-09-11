using System;
using System.Collections.Generic;
using System.Threading;

namespace JueMingR.Platform.Settings
{
    // One process-level preference owner and one worker per actual document.
    // The single pending slot replaces older requests; no Terraria object crosses it.
    public sealed class PreferenceDocument<T> : IDisposable
    {
        private readonly object gate = new object();
        private readonly IPreferenceStorage storage;
        private readonly IPreferenceCodec<T> codec;
        private readonly int quietMilliseconds;
        private readonly Thread worker;
        private PreferenceSnapshot<T> snapshot;
        private bool writable, pending, stopping, cancelled, loadAbandoned;
        private DateTime due;

        public PreferenceDocument(IPreferenceStorage storage, IPreferenceCodec<T> codec, T defaultValue, int quietMilliseconds = 150)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (quietMilliseconds < 0 || quietMilliseconds > 1000) throw new ArgumentOutOfRangeException(nameof(quietMilliseconds));
            this.quietMilliseconds = quietMilliseconds;
            snapshot = new PreferenceSnapshot<T>(defaultValue, false, 0, PreferenceStatus.Loading);
            worker = new Thread(Work) { IsBackground = true, Name = "JueMingR preference I/O" };
            worker.Start();
        }

        public PreferenceSnapshot<T> Snapshot { get { return Volatile.Read(ref snapshot); } }

        public bool Set(T value)
        {
            lock (gate)
            {
                // No user command is accepted before initial recovery. This also
                // means a delayed read can never undo a first user selection.
                if (stopping || !snapshot.IsLoaded || EqualityComparer<T>.Default.Equals(snapshot.Value, value)) return false;
                snapshot = new PreferenceSnapshot<T>(value, true, snapshot.Revision + 1,
                    writable ? PreferenceStatus.Pending : snapshot.Status,
                    snapshot.CommitUnconfirmed, snapshot.IsProtected, snapshot.Error);
                if (writable)
                {
                    pending = true;
                    due = DateTime.UtcNow.AddMilliseconds(quietMilliseconds);
                    Monitor.PulseAll(gate);
                }
                return true;
            }
        }

        public void AbandonSlowLoad()
        {
            lock (gate)
            {
                if (snapshot.IsLoaded) return;
                // The Host may stop waiting after its startup bound. The worker
                // still owns its handles until it exits; its late bytes are inert.
                loadAbandoned = true;
                snapshot = new PreferenceSnapshot<T>(snapshot.Value, true, 0, PreferenceStatus.IoFailure, false, true, "load-timeout");
            }
        }

        public bool Stop(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            lock (gate) { stopping = true; Monitor.PulseAll(gate); }
            if (worker.Join(milliseconds)) return true;
            lock (gate)
            {
                // Native I/O already in progress cannot be aborted safely. A false
                // return retains worker ownership: callers must not release its root.
                cancelled = true; pending = false; writable = false;
                Monitor.PulseAll(gate);
            }
            return false;
        }

        public void Dispose()
        {
            if (!Stop(750)) throw new TimeoutException("Preference worker still owns its storage; do not remove its data root.");
        }

        private void Work()
        {
            try
            {
                PreferenceReadResult read = storage.Read();
                T initial = Snapshot.Value;
                PreferenceStatus status = ReadStatus(read.Status);
                if (read.Status == PreferenceReadStatus.Loaded)
                {
                    try { initial = codec.Decode(read.Contents); }
                    catch (PreferenceFormatException exception) { status = exception.Status; }
                    catch { status = PreferenceStatus.Invalid; }
                }
                lock (gate)
                {
                    if (loadAbandoned || cancelled) return;
                    writable = status == PreferenceStatus.Missing || status == PreferenceStatus.Saved;
                    snapshot = new PreferenceSnapshot<T>(initial, true, 0, status, false, !writable, read.Error);
                }
                string identity = read.Identity;
                while (true)
                {
                    T value;
                    long revision;
                    lock (gate)
                    {
                        while (!pending && !stopping) Monitor.Wait(gate);
                        if (cancelled || (stopping && !pending)) return;
                        while (!stopping && DateTime.UtcNow < due)
                            Monitor.Wait(gate, Math.Max(1, (int)(due - DateTime.UtcNow).TotalMilliseconds));
                        if (cancelled) return;
                        value = snapshot.Value; revision = snapshot.Revision; pending = false;
                    }

                    // Serialization, self-validation and disk operations are all
                    // outside the short command lock and outside game callbacks.
                    byte[] bytes = codec.Encode(value);
                    if (!EqualityComparer<T>.Default.Equals(codec.Decode(bytes), value)) throw PreferenceJson.Invalid();
                    lock (gate) { if (cancelled) return; }
                    PreferenceWriteResult result = storage.Write(identity, bytes);
                    lock (gate)
                    {
                        if (result.Status == PreferenceWriteStatus.Saved)
                        {
                            identity = result.Identity;
                            // An old completion may update its mechanical identity,
                            // but may not mark a newer, pending choice as saved.
                            if (snapshot.Revision == revision)
                                snapshot = new PreferenceSnapshot<T>(snapshot.Value, true, revision, PreferenceStatus.Saved);
                        }
                        else
                        {
                            writable = false; pending = false;
                            snapshot = new PreferenceSnapshot<T>(snapshot.Value, true, snapshot.Revision, WriteStatus(result.Status),
                                result.CommitUnconfirmed, result.IsProtected, result.Error);
                        }
                    }
                }
            }
            catch
            {
                lock (gate)
                {
                    writable = false; pending = false;
                    if (!loadAbandoned)
                        snapshot = new PreferenceSnapshot<T>(snapshot.Value, true, snapshot.Revision, PreferenceStatus.IoFailure,
                            snapshot.CommitUnconfirmed, true, snapshot.Error ?? "preference-worker-failed");
                }
            }
            finally { storage.Dispose(); }
        }

        private static PreferenceStatus ReadStatus(PreferenceReadStatus status)
        {
            switch (status)
            {
                case PreferenceReadStatus.Missing: return PreferenceStatus.Missing;
                case PreferenceReadStatus.Loaded: return PreferenceStatus.Saved;
                case PreferenceReadStatus.TooLarge: return PreferenceStatus.Invalid;
                case PreferenceReadStatus.Busy: return PreferenceStatus.Busy;
                default: return PreferenceStatus.IoFailure;
            }
        }
        private static PreferenceStatus WriteStatus(PreferenceWriteStatus status)
        {
            switch (status)
            {
                case PreferenceWriteStatus.Conflict: return PreferenceStatus.Conflict;
                case PreferenceWriteStatus.Busy: return PreferenceStatus.Busy;
                default: return PreferenceStatus.IoFailure;
            }
        }
    }
}
