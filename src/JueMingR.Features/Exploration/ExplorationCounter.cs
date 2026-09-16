using System;
using System.Threading;

namespace JueMingR.Features.Exploration
{
    // All map reads and count ownership belong to the game thread. Native
    // writers may only call Changed/BeginBatch/EndBatch. Arrays are finite per
    // world, never one item per event, and are not shared across map identities.
    public sealed class ExplorationCounter
    {
        public const int BlockSize = 64;
        private readonly int width, height, columns;
        private readonly Func<int, int, bool> revealed;
        private readonly int[] counts, dirty;
        private readonly long[] versions;
        private int dynamicEnabled, activeBatch, epoch, seenEpoch, signalled;
        private int initial, dirtyCursor;
        private bool sweeping, complete, scanActive = true;
        private bool calibrationTurn;
        private long running, published;
#if DEBUG
        internal long CellReads, BlockReads, MetadataVisits, FullScans;
#endif
        public ExplorationCounter(int width, int height, Func<int, int, bool> revealed, bool startScan = true)
        {
            if (width <= 0 || height <= 0 || width > 20000 || height > 20000) throw new ArgumentException("exploration-dimensions-invalid");
            this.width = width; this.height = height; this.revealed = revealed ?? throw new ArgumentNullException(nameof(revealed));
            columns = (width + BlockSize - 1) / BlockSize; int blocks = checked(columns * ((height + BlockSize - 1) / BlockSize));
            counts = new int[blocks]; dirty = new int[blocks]; versions = new long[blocks]; Total = (long)width * height;
            scanActive = startScan;
        }
        public long Total { get; }
        public long Count { get { return published; } }
        public bool HasResult { get; private set; }
        public bool Complete { get { return complete && seenEpoch == Volatile.Read(ref epoch) && Volatile.Read(ref activeBatch) == 0; } }
        public bool Current { get { return Dynamic && Complete && !Pending; } }
        public bool Pending { get { return Dynamic && (sweeping || Volatile.Read(ref signalled) != 0 || !Complete); } }
        public bool Dynamic { get { return Volatile.Read(ref dynamicEnabled) != 0; } }
        public bool Paused { get; set; }
        public bool Scanning { get { return scanActive; } }
        public int CompletedBlocks { get { return initial; } }
        public int BlockCount { get { return counts.Length; } }
        public long Revision { get; private set; }
        public long CalibrationRevision { get; private set; }
        public void SetDynamic(bool enabled)
        {
            if (enabled == Dynamic) return;
            Volatile.Write(ref dynamicEnabled, enabled ? 1 : 0);
            // Off loses continuity even if GUID/dimensions remain unchanged.
            // Explicit pause is independent from this rebuild request.
            if (enabled) Restart(false); else { sweeping = false; Interlocked.Exchange(ref signalled, 0); }
            Revision++;
        }
        public void Restart()
        { Restart(Dynamic && Complete); }
        private void Restart(bool preserveBaseline)
        {
            initial = 0; scanActive = true;
            // Manual calibration may share the already valid per-block base.
            // Pausing its cursor must not pause ordinary dirty maintenance.
            // Lost continuity (off/on, replacement or bulk epoch) cannot reuse it.
            if (!preserveBaseline) { running = 0; complete = false; sweeping = false; dirtyCursor = 0; seenEpoch = Volatile.Read(ref epoch); }
            // A retained base keeps its original epoch, including when a batch
            // races the Complete check above. Never bless old counts as new.
            Revision++;
        }
        public void Changed(int x, int y)
        {
            if (Volatile.Read(ref dynamicEnabled) == 0 || Volatile.Read(ref activeBatch) != 0 || (uint)x >= (uint)width || (uint)y >= (uint)height) return;
            int i = x / BlockSize + y / BlockSize * columns;
            Interlocked.Increment(ref versions[i]); Volatile.Write(ref dirty[i], 1); Volatile.Write(ref signalled, 1);
        }
        public void BeginBatch() { Interlocked.Increment(ref activeBatch); Interlocked.Increment(ref epoch); }
        public void EndBatch() { Interlocked.Increment(ref epoch); Interlocked.Decrement(ref activeBatch); }
        public void Advance(int cellBudget, int metadataBudget, bool advanceScan = true)
        {
            if (cellBudget < BlockSize * BlockSize || metadataBudget < 1) throw new ArgumentOutOfRangeException(nameof(cellBudget));
            if (Volatile.Read(ref activeBatch) != 0) return;
            if (seenEpoch != Volatile.Read(ref epoch))
            {
                if (Dynamic || scanActive) Restart(false);
                else { seenEpoch = Volatile.Read(ref epoch); complete = false; Revision++; }
            }
            if (!scanActive && !Dynamic) return;
            int blocks = cellBudget / (BlockSize * BlockSize);
            if (scanActive && !Paused && advanceScan)
            {
                int scanBlocks = blocks;
                if (complete && Dynamic && Pending)
                {
                    // Calibration and changes share one cell budget. Reserve
                    // maintenance capacity; a one-block budget alternates.
                    if (blocks > 1) scanBlocks--;
                    else { calibrationTurn = !calibrationTurn; if (!calibrationTurn) scanBlocks = 0; }
                }
                while (blocks > 0 && scanBlocks > 0 && initial < counts.Length)
                {
                    blocks--; scanBlocks--;
                    int value; if (!ReadBlock(initial, out value)) return;
                    running += value - (complete ? counts[initial] : 0); counts[initial] = value; initial++;
                }
                if (initial == counts.Length)
                {
                    complete = true; scanActive = false; Revision++; CalibrationRevision++;
#if DEBUG
                    FullScans++;
#endif
                    if (!Dynamic) Publish();
                }
            }
            if (!complete || !Dynamic) return;
            if (!sweeping && Interlocked.Exchange(ref signalled, 0) != 0) { sweeping = true; dirtyCursor = 0; }
            // Check bounded metadata only while an actual native change has
            // signalled work. A later write behind the cursor starts another
            // metadata pass; untouched blocks never cause map cell reads.
            while (sweeping && metadataBudget-- > 0 && blocks > 0)
            {
                int i = dirtyCursor;
#if DEBUG
                MetadataVisits++;
#endif
                if (Volatile.Read(ref dirty[i]) != 0)
                {
                    int value; if (!ReadBlock(i, out value)) return;
                    running += value - counts[i]; counts[i] = value; blocks--;
                }
                if (++dirtyCursor == counts.Length)
                { sweeping = false; dirtyCursor = 0; if (Interlocked.Exchange(ref signalled, 0) != 0) sweeping = true; }
            }
            // Every block has a baseline now. Publish bounded maintenance even
            // while writers keep the dirty sweep alive; Pending labels this as
            // updating, never a fully caught-up snapshot.
            if (Complete) Publish();
        }
        private bool ReadBlock(int i, out int value)
        {
            value = 0; int beforeEpoch = Volatile.Read(ref epoch);
            if (beforeEpoch != seenEpoch || Volatile.Read(ref activeBatch) != 0) return false;
            Interlocked.Exchange(ref dirty[i], 0); long version = Interlocked.Read(ref versions[i]);
            int left = i % columns * BlockSize, top = i / columns * BlockSize;
#if DEBUG
            BlockReads++; CellReads += (long)(Math.Min(left + BlockSize, width) - left) * (Math.Min(top + BlockSize, height) - top);
#endif
            for (int y = top; y < Math.Min(top + BlockSize, height); y++) for (int x = left; x < Math.Min(left + BlockSize, width); x++) if (revealed(x, y)) value++;
            if (beforeEpoch != Volatile.Read(ref epoch) || Volatile.Read(ref activeBatch) != 0 || version != Interlocked.Read(ref versions[i]))
            { Volatile.Write(ref dirty[i], 1); Volatile.Write(ref signalled, 1); return false; }
            return true;
        }
        private void Publish()
        {
            if (running < 0 || running > Total) throw new InvalidOperationException("exploration-count-invalid");
            if (!HasResult || published != running) Revision++;
            published = running; HasResult = true;
        }
    }
}
