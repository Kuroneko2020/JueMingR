using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using JueMingR.Platform.Blueprint;

namespace JueMingR.Infrastructure.Blueprint
{
    // External paths never run on the library or game thread. This lane has one
    // owner, one command and one acknowledgement; a blocked system call cannot
    // spawn a replacement lane or surrender its buffer/handles on timeout.
    public sealed class FileBlueprintExchange : IBlueprintExchangeIo
    {
        private readonly object gate = new object();
        private Thread thread;
        private BlueprintExchangeIoCommand pending;
        private BlueprintExchangeIoReceipt receipt;
        private Action completed;
        private bool occupied, stopping;
        private Stream stream;
        private readonly Func<string, FileMode, FileAccess, FileShare, Stream> open;
        private readonly Action<string> fault;
        private BlueprintExchangeOwnedFile ownedCandidate;
        private WindowsBlueprintFileTicket ticket;
        private string candidate, backup;
        private byte[] expectedTarget;
        private bool attempted, committed;
        public FileBlueprintExchange() : this((path, mode, access, share) => new FileStream(path, mode, access, share, 65536, FileOptions.SequentialScan)) { }
        // Isolated tests wrap the actual external stream to hold a single OS
        // boundary; the production queue/worker/scheduler remain unchanged.
        internal FileBlueprintExchange(Func<string, FileMode, FileAccess, FileShare, Stream> open) : this(open, null) { }
        internal FileBlueprintExchange(Func<string, FileMode, FileAccess, FileShare, Stream> open, Action<string> fault) { this.open = open ?? throw new ArgumentNullException(nameof(open)); this.fault = fault; }
        public bool TrySubmit(BlueprintExchangeIoCommand command, Action wake)
        {
            lock (gate)
            {
                if (occupied || stopping) return false;
                pending = command; completed = wake; occupied = true;
                if (thread == null) { thread = new Thread(Run) { IsBackground = true, Name = "JueMingR blueprint exchange IO" }; thread.Start(); }
                Monitor.Pulse(gate); return true;
            }
        }
        public bool TryTake(out BlueprintExchangeIoReceipt value)
        { lock (gate) { value = receipt; if (value == null) return false; receipt = null; occupied = false; return true; } }
        public void BeginStop() { lock (gate) { stopping = true; Monitor.Pulse(gate); } }
        public bool Stop(int milliseconds) { BeginStop(); Thread running; lock (gate) running = thread; return running == null || running.Join(Math.Max(0, milliseconds)); }
        private void Run()
        {
            try
            {
                while (true)
                {
                    BlueprintExchangeIoCommand command; Action wake;
                    lock (gate) { while (pending == null && !stopping) Monitor.Wait(gate); if (pending == null && stopping) return; command = pending; pending = null; wake = completed; }
                    BlueprintExchangeIoReceipt value;
                    try { value = Execute(command); }
                    catch (Exception error) { value = new BlueprintExchangeIoReceipt { ErrorCode = error is InvalidDataException ? error.Message : error.GetType().Name, CommitUnconfirmed = attempted && !committed, FileCommitted = committed }; }
                    lock (gate) receipt = value;
                    wake?.Invoke();
                }
            }
            finally { Close(); }
        }
        private BlueprintExchangeIoReceipt Execute(BlueprintExchangeIoCommand command)
        {
            switch (command.Kind)
            {
                case BlueprintExchangeIoKind.OpenSource:
                    RequireFresh(command.Ticket); Guard(ticket.NormalizedPath);
                    stream = open(ticket.NormalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return new BlueprintExchangeIoReceipt { Success = true, Length = stream.Length };
                case BlueprintExchangeIoKind.Read:
                    if (stream == null || command.Buffer == null || command.Count < 0 || command.Count > command.Buffer.Length || command.Count > 65536 || command.Offset < 0 || checked(command.Offset + command.Count) > stream.Length) throw new InvalidDataException("blueprint-exchange-read-range");
                    stream.Position = command.Offset; int at = 0;
                    while (at < command.Count) { int count = stream.Read(command.Buffer, at, command.Count - at); if (count == 0) throw new EndOfStreamException(); at += count; }
                    return new BlueprintExchangeIoReceipt { Success = true, Count = at, Length = stream.Length };
                case BlueprintExchangeIoKind.BeginTarget:
                    RequireFresh(command.Ticket); Guard(ticket.NormalizedPath); Guard(Path.GetDirectoryName(ticket.NormalizedPath));
                    if (ticket.Disposition == BlueprintTargetDisposition.ConfirmedReplace)
                        using (FileStream target = ticket.OpenConfirmedTarget()) expectedTarget = Hash(target);
                    else if (File.Exists(ticket.NormalizedPath) || Directory.Exists(ticket.NormalizedPath)) throw new InvalidDataException("blueprint-exchange-target-changed");
                    candidate = Path.Combine(Path.GetDirectoryName(ticket.NormalizedPath), "." + Path.GetFileName(ticket.NormalizedPath) + "." + ticket.RequestId.ToString("N") + ".tmp");
                    // CreateNew is the proof of ownership. An existing path is
                    // never adopted as this request's temporary material.
                    if (File.Exists(candidate) || Directory.Exists(candidate)) { candidate = null; throw new InvalidDataException("blueprint-exchange-candidate-conflict"); }
                    stream = open(candidate, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
                    ownedCandidate = new BlueprintExchangeOwnedFile(); ownedCandidate.Capture(stream);
                    return new BlueprintExchangeIoReceipt { Success = true };
                case BlueprintExchangeIoKind.Write:
                    if (stream == null || candidate == null || command.Buffer == null || command.Count < 0 || command.Count > command.Buffer.Length || command.Count > 65536) throw new InvalidDataException("blueprint-exchange-write-range");
                    stream.Write(command.Buffer, 0, command.Count); return new BlueprintExchangeIoReceipt { Success = true, Count = command.Count };
                case BlueprintExchangeIoKind.CommitTarget:
                    if (stream == null || candidate == null) throw new InvalidOperationException("blueprint-exchange-no-candidate");
                    var durable = stream as FileStream; if (durable != null) durable.Flush(true); else stream.Flush(); stream.Position = 0; byte[] expectedCandidate = command.ExpectedDigest; long length = command.ExpectedLength;
                    if (expectedCandidate == null || expectedCandidate.Length != 32 || stream.Length != length || !Equal(Hash(stream), expectedCandidate)) throw new InvalidDataException("blueprint-exchange-written-bytes-mismatch");
                    ownedCandidate.Capture(stream); stream.Dispose(); stream = null;
                    using (var readback = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read))
                        if (readback.Length != length || !Equal(expectedCandidate, Hash(readback))) throw new InvalidDataException("blueprint-exchange-candidate-readback");
                    Guard(ticket.NormalizedPath); Guard(candidate);
                    if (ticket.Disposition == BlueprintTargetDisposition.ConfirmedReplace)
                    {
                        // The native permission belongs to the object it showed.
                        // Hashing a newly appearing same-path file cannot inherit it.
                        using (FileStream target = ticket.OpenConfirmedTarget())
                        {
                            if (!Equal(Hash(target), expectedTarget)) throw new InvalidDataException("blueprint-exchange-target-changed");
                            backup = candidate + ".backup";
                            if (File.Exists(backup) || Directory.Exists(backup)) { backup = null; throw new InvalidDataException("blueprint-exchange-backup-conflict"); }
                            ThrowIfStopping(); fault?.Invoke("BeforeCommit"); attempted = true; File.Replace(candidate, ticket.NormalizedPath, backup, false); fault?.Invoke("AfterCommit");
                            using (var old = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read)) if (!Equal(Hash(old, false), expectedTarget)) throw new InvalidDataException("blueprint-exchange-replaced-object-changed");
                            VerifyFinal(length, expectedCandidate);
                        }
                    }
                    else { ThrowIfStopping(); fault?.Invoke("BeforeCommit"); attempted = true; File.Move(candidate, ticket.NormalizedPath); fault?.Invoke("AfterCommit"); VerifyFinal(length, expectedCandidate); }
                    committed = true; candidate = null;
                    if (backup != null)
                    {
                        Guard(backup);
                        using (var original = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                        {
                            if (!Equal(Hash(original, false), expectedTarget)) throw new InvalidDataException("blueprint-exchange-backup-changed");
                            // The verified recovery object may have been moved
                            // under delete sharing. Retire that exact object,
                            // never a replacement now occupying its pathname.
                            var ownedBackup = new BlueprintExchangeOwnedFile(); ownedBackup.Capture(original);
                            if (!ownedBackup.Delete(backup, true)) throw new InvalidDataException("blueprint-exchange-backup-changed");
                        }
                        backup = null;
                    }
                    return new BlueprintExchangeIoReceipt { Success = true, FileCommitted = true, Length = length };
                case BlueprintExchangeIoKind.Close:
                    bool retained = Close(); return new BlueprintExchangeIoReceipt { Success = true, ErrorCode = retained ? "blueprint-exchange-cleanup-retained" : null };
                default: throw new ArgumentOutOfRangeException();
            }
        }
        private void RequireFresh(IBlueprintExchangeTicket value)
        {
            if (ticket != null || stream != null) throw new InvalidOperationException("blueprint-exchange-lane-owned");
            ticket = value as WindowsBlueprintFileTicket ?? throw new ArgumentException("blueprint-exchange-ticket");
            attempted = committed = false;
        }
        private bool Close()
        {
            bool retained = false;
            if (candidate != null && !attempted && ownedCandidate != null)
            {
                if (stream != null) ownedCandidate.Capture(stream);
                try { retained = !ownedCandidate.Delete(candidate, stream != null); } catch { retained = true; }
            }
            stream?.Dispose(); stream = null;
            // Attempted/unknown commits and changed/unprovable candidate
            // objects remain recovery material. Never adopt an existing tmp.
            candidate = backup = null; ownedCandidate = null; expectedTarget = null; ticket?.Dispose(); ticket = null; attempted = committed = false; return retained;
        }
        private static void Guard(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("blueprint-exchange-link-protected");
                string parent = Path.GetDirectoryName(current); if (parent == current) break; current = parent;
            }
        }
        private void VerifyFinal(long length, byte[] expected)
        { using (var final = new FileStream(ticket.NormalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read)) if (final.Length != length || !Equal(Hash(final, false), expected)) throw new InvalidDataException("blueprint-exchange-final-readback"); }
        private void ThrowIfStopping() { lock (gate) if (stopping) throw new OperationCanceledException(); }
        private byte[] Hash(Stream value, bool cancellable = true)
        {
            using (var hash = SHA256.Create())
            {
                byte[] buffer = new byte[65536]; int count;
                while ((count = value.Read(buffer, 0, buffer.Length)) != 0) { if (cancellable) ThrowIfStopping(); hash.TransformBlock(buffer, 0, count, null, 0); }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return hash.Hash;
            }
        }
        private static bool Equal(byte[] left, byte[] right) { if (left == null || right == null || left.Length != right.Length) return false; for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false; return true; }
    }
}
