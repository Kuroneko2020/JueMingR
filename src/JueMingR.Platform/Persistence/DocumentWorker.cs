using System;
using System.Threading;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.Persistence
{
    public sealed class DocumentResult<T>
    {
        internal DocumentResult(long id, bool success, T value, string error, bool commitUnconfirmed = false, bool isProtected = false)
        { CommandId = id; Success = success; Value = value; Error = error; CommitUnconfirmed = commitUnconfirmed; IsProtected = isProtected; }
        public long CommandId { get; }
        public bool Success { get; }
        public T Value { get; }
        public string Error { get; }
        public bool CommitUnconfirmed { get; }
        public bool IsProtected { get; }
    }

    // One consistency unit, one worker, one accepted immutable command. The slot
    // remains occupied until its result is collected; commands are never merged.
    // IPreferenceStorage is the existing opaque-byte lease port, despite its name.
    public sealed class DocumentWorker<T> : IDisposable
    {
        private readonly object gate = new object();
        private readonly IPreferenceStorage storage;
        private readonly Func<byte[], T> decode;
        private readonly Func<T, byte[]> encode;
        private readonly T empty;
        private readonly Thread thread;
        private bool stopping, occupied = true, pending, writable;
        private volatile bool cancelled;
        private long command;
        private T candidate;
        private DocumentResult<T> result;
        private string identity;
        private T current;
        private Func<T, T> finalUpdate;
        private bool lastWriteSucceeded = true;

        public DocumentWorker(IPreferenceStorage storage, Func<byte[], T> decode, Func<T, byte[]> encode, T empty)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
            this.encode = encode ?? throw new ArgumentNullException(nameof(encode)); this.empty = empty;
            thread = new Thread(Run) { IsBackground = true, Name = "JueMingR document I/O" }; thread.Start();
        }
        public bool TrySubmit(long id, T value)
        {
            lock (gate)
            {
                if (stopping || occupied || !writable || id <= 0) return false;
                occupied = pending = true; command = id; candidate = value; Monitor.Pulse(gate); return true;
            }
        }
        public bool TryTake(out DocumentResult<T> value)
        {
            lock (gate)
            {
                value = result; if (value == null) return false;
                result = null; occupied = false; return true;
            }
        }
        public bool Stop(int milliseconds) { return Stop(milliseconds, null); }
        public bool Stop(int milliseconds, Func<T, T> finishAccepted)
        {
            lock (gate)
            {
                if (!stopping) finalUpdate = finishAccepted;
                stopping = true; Monitor.Pulse(gate);
            }
            if (thread.Join(Math.Max(0, milliseconds))) return true;
            cancelled = true; return false;
        }
        public void Dispose()
        { if (!Stop(3000)) throw new TimeoutException("Document worker still owns storage; do not clean its data root."); }

        private void Run()
        {
            try
            {
                DocumentResult<T> loaded;
                try
                {
                    PreferenceReadResult read = storage.Read();
                    if (read.Status == PreferenceReadStatus.Missing || read.Status == PreferenceReadStatus.Loaded)
                    {
                        T value = read.Status == PreferenceReadStatus.Missing ? empty : decode(read.Contents);
                        identity = read.Identity; current = value; writable = true; loaded = new DocumentResult<T>(0, true, value, null);
                    }
                    else loaded = new DocumentResult<T>(0, false, default(T), read.Error ?? read.Status.ToString());
                }
                catch (PreferenceFormatException e) { loaded = new DocumentResult<T>(0, false, default(T), e.Status.ToString()); }
                catch (Exception) { loaded = new DocumentResult<T>(0, false, default(T), "document-load-failed"); }
                lock (gate) result = loaded;
                while (true)
                {
                    long id; T value; Func<T, T> finish = null;
                    lock (gate)
                    {
                        while (!pending && !stopping) Monitor.Wait(gate);
                        if (cancelled) break;
                        if (pending) { id = command; value = candidate; candidate = default(T); pending = false; }
                        else if (stopping && finalUpdate != null && writable && lastWriteSucceeded)
                        { id = -1; value = current; finish = finalUpdate; finalUpdate = null; }
                        else break;
                    }
                    DocumentResult<T> completion;
                    bool writeEntered = false;
                    try
                    {
                        if (finish != null)
                        {
                            value = finish(current);
                            if (ReferenceEquals(value, current)) break;
                        }
                        byte[] bytes = encode(value);
                        // Validate encoded semantics before the mechanical commit. No UI
                        // callback or mutable game object reaches this background owner.
                        decode(bytes);
                        if (cancelled) break; // Native I/O already entered cannot be aborted; before it can.
                        writeEntered = true;
                        PreferenceWriteResult written = storage.Write(identity, bytes);
                        bool success = written.Status == PreferenceWriteStatus.Saved;
                        if (success) { identity = written.Identity; current = value; }
                        if (written.IsProtected) { lock (gate) writable = false; }
                        completion = new DocumentResult<T>(id, success, success ? value : default(T), written.Error, written.CommitUnconfirmed, written.IsProtected);
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is PreferenceFormatException)
                    { completion = new DocumentResult<T>(id, false, default(T), "document-encoding-or-size-failed", writeEntered, writeEntered); }
                    catch (Exception) { completion = new DocumentResult<T>(id, false, default(T), "document-write-failed", writeEntered, writeEntered); }
                    // A throwing storage boundary may have committed before it
                    // threw. Never turn that unknown outcome into a retry.
                    lock (gate) { if (completion.IsProtected) writable = false; result = completion; lastWriteSucceeded = completion.Success; }
                }
            }
            finally { storage.Dispose(); }
        }
    }
}
