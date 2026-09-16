using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Footprints
{
    // Sole worker owns this object and its file lease. Only the finite tail is
    // replaced on save. Physical blocks never imply a continuity boundary.
    public sealed class FootprintArchive
    {
        private readonly IFootprintArchiveFiles files;
        private readonly string pair;
        private readonly int width, height;
        private readonly Dictionary<long, FootprintSample[]> cache = new Dictionary<long, FootprintSample[]>();
        private readonly Queue<long> cacheOrder = new Queue<long>();
        private FootprintSample[] tail = new FootprintSample[0];
        private long blocks;
        private string catalogIdentity, rootIdentity, operation = "";
        private bool loaded, persisted, protectedWrite;
        private bool loadStarted, namesChecked, replacementCommitted;
        private long validatedBlocks;
        private FootprintSample validatedLast;
        private string replacementGeneration;
        public FootprintArchive(IFootprintArchiveFiles files, string pair, int width, int height)
        { this.files = files ?? throw new ArgumentNullException(nameof(files)); this.pair = pair; this.width = width; this.height = height; if (width <= 0 || height <= 0) throw new ArgumentException("footprint-world-size"); }
        public string Generation { get; private set; }
        public bool Clearing { get { return operation != ""; } }
        public long Count { get { return blocks * 256 + tail.Length; } }
        public long End { get { return tail.Length == 0 ? 0 : tail[tail.Length - 1].End; } }
        public long Segment { get { return tail.Length == 0 ? 0 : tail[tail.Length - 1].Segment; } }
        public bool Protected { get { return protectedWrite; } }
        public bool Loaded { get { return loaded; } }
        public void Load()
        {
            if (loadStarted || loaded) throw new InvalidOperationException("footprint-already-loaded");
            while (!LoadStep()) { }
        }
        public bool LoadStep()
        {
            if (loaded) return true;
            if (!loadStarted) { BeginLoad(); loadStarted = true; if (Clearing) { loaded = true; return true; } }
            if (!namesChecked) { namesChecked = files.ValidateBlockNamesStep(blocks, 32); return false; }
            // First recovery is O(S), but each call reads at most eight bounded
            // blocks. The worker can stop between calls; no full-history array
            // is ever published to the game thread on completion.
            for (int i = 0; i < 8 && validatedBlocks < blocks; i++, validatedBlocks++)
            {
                var block = ReadBlock(validatedBlocks); if (validatedBlocks > 0) FootprintCodec.Adjacent(validatedLast, block[0]); validatedLast = block[255];
            }
            if (validatedBlocks != blocks) return false;
            if (blocks > 0) FootprintCodec.Adjacent(validatedLast, tail[0]); loaded = true; return true;
        }
        private void BeginLoad()
        {
            var catalog = files.ReadCatalog(); catalogIdentity = catalog.Identity;
            if (catalog.Status == PreferenceReadStatus.Missing) Generation = Guid.NewGuid().ToString("N");
            else
            {
                RequireRead(catalog); string generation;
                FootprintCodec.ReadCatalog(catalog.Contents, pair, width, height, out generation, out operation); Generation = generation; persisted = true;
            }
            if (Clearing) return;
            // OpenRoot creates its lease directory. Persist the verified empty
            // catalog first so quitting before the first sample is still readable.
            EnsureCatalog();
            var root = files.OpenRoot(Generation); rootIdentity = root.Identity;
            if (root.Status != PreferenceReadStatus.Missing)
            {
                RequireRead(root); tail = FootprintCodec.Decode(root.Contents, pair, Generation, width, height, true, out blocks);
            }
        }
        public void Append(FootprintSample[] samples, int count)
        {
            Writable(); if (count < 1 || count > 256 || samples == null || count > samples.Length) throw new ArgumentException("footprint-batch-size");
            var candidate = new List<FootprintSample>(tail); long nextBlocks = blocks;
            for (int i = 0; i < count; i++)
            {
                var point = samples[i]; long currentCount = nextBlocks * 256 + candidate.Count;
                if (point.Sequence == currentCount && candidate.Count > 0)
                {
                    var prior = candidate[candidate.Count - 1]; if (!point.SameIdentity(prior) || point.End < prior.End) throw FootprintCodec.Invalid(); candidate[candidate.Count - 1] = point;
                }
                else
                {
                    if (point.Sequence != currentCount + 1 || point.Start != (candidate.Count == 0 ? 0 : candidate[candidate.Count - 1].End)) throw FootprintCodec.Invalid();
                    if (candidate.Count > 0) FootprintCodec.Adjacent(candidate[candidate.Count - 1], point);
                    if (candidate.Count == 256)
                    {
                        byte[] blockBytes = FootprintCodec.Encode(pair, Generation, width, height, nextBlocks, candidate.ToArray(), 256, false);
                        long decoded; FootprintCodec.Decode(blockBytes, pair, Generation, width, height, false, out decoded);
                        EnsureCatalog(); files.CreateBlock(nextBlocks, blockBytes); nextBlocks++; candidate.Clear();
                    }
                    candidate.Add(point);
                }
            }
            var nextTail = candidate.ToArray(); byte[] bytes = FootprintCodec.Encode(pair, Generation, width, height, nextBlocks, nextTail, nextTail.Length, true);
            long check; FootprintCodec.Decode(bytes, pair, Generation, width, height, true, out check);
            EnsureCatalog(); rootIdentity = Commit(files.WriteRoot(rootIdentity, bytes)); tail = nextTail; blocks = nextBlocks;
        }
        public FootprintQuery Query(long cursor, int maximum = 8192)
        {
            if (!loaded || Clearing || maximum < 2 || maximum > 16384) throw new InvalidOperationException("footprint-query-unavailable");
            if (Count == 0) return new FootprintQuery(Generation, 0, new FootprintSample[0], false);
            cursor = Math.Max(0, Math.Min(End, cursor)); long low = 0, high = blocks;
            while (low < high) { long mid = low + (high - low) / 2; var values = ReadBlock(mid); if (values[255].End < cursor) low = mid + 1; else high = mid; }
            var found = low == blocks ? tail : ReadBlock(low); int at = 0; while (at + 1 < found.Length && found[at].End < cursor) at++;
            long center = low * 256 + at, start = Math.Max(0, Math.Min(Count - maximum, center - maximum / 2)); int length = (int)Math.Min(maximum, Count - start);
            var output = new FootprintSample[length]; long lastBlock = -1; FootprintSample[] source = null;
            for (int i = 0; i < length; i++)
            {
                long index = start + i, number = index / 256;
                if (number != lastBlock) { source = number == blocks ? tail : ReadBlock(number); lastBlock = number; }
                output[i] = source[index % 256]; if (i > 0) FootprintCodec.Adjacent(output[i - 1], output[i]);
            }
            return new FootprintQuery(Generation, End, output, length < Count);
        }
        public void BeginClear(string generation, string operationId)
        {
            if (!loaded || protectedWrite || generation != Generation || !FootprintCodec.GuidText(operationId) || Clearing && operation != operationId) throw new InvalidOperationException("footprint-clear-target-changed");
            if (Clearing) return;
            catalogIdentity = Commit(files.WriteCatalog(catalogIdentity, FootprintCodec.Catalog(pair, Generation, operationId, width, height)));
            persisted = true; operation = operationId; cache.Clear(); cacheOrder.Clear();
        }
        public bool ClearStep(int budget)
        {
            if (!loaded || !Clearing || protectedWrite) throw new InvalidOperationException("footprint-clear-not-accepted");
            if (!files.DeleteGenerationStep(Generation, budget)) return false;
            if (replacementGeneration == null) replacementGeneration = Guid.NewGuid().ToString("N");
            if (!replacementCommitted) { catalogIdentity = Commit(files.WriteCatalog(catalogIdentity, FootprintCodec.Catalog(pair, replacementGeneration, "", width, height))); replacementCommitted = true; }
            // Do not change the in-memory clear target until the fresh root is
            // ready. A post-catalog opening failure retries this same empty root.
            var root = files.OpenRoot(replacementGeneration);
            if (root.Status != PreferenceReadStatus.Missing) { if (root.Status == PreferenceReadStatus.Loaded) throw FootprintCodec.Invalid(); throw new IOException(root.Error ?? "footprint-new-root-unavailable"); }
            rootIdentity = root.Identity; Generation = replacementGeneration; replacementGeneration = null; replacementCommitted = false;
            operation = ""; blocks = 0; tail = new FootprintSample[0]; cache.Clear(); cacheOrder.Clear(); return true;
        }
        private FootprintSample[] ReadBlock(long number)
        {
            FootprintSample[] value; if (cache.TryGetValue(number, out value)) return value;
            long ordinal; value = FootprintCodec.Decode(files.ReadBlock(number), pair, Generation, width, height, false, out ordinal);
            if (ordinal != number) throw FootprintCodec.Invalid();
            if (cache.Count == 64) cache.Remove(cacheOrder.Dequeue()); cache.Add(number, value); cacheOrder.Enqueue(number); return value;
        }
        private void EnsureCatalog()
        { if (!persisted) { catalogIdentity = Commit(files.WriteCatalog(catalogIdentity, FootprintCodec.Catalog(pair, Generation, "", width, height))); persisted = true; } }
        private string Commit(PreferenceWriteResult result)
        {
            if (result.Status == PreferenceWriteStatus.Saved) return result.Identity;
            protectedWrite |= result.IsProtected || result.CommitUnconfirmed || result.Status == PreferenceWriteStatus.Conflict;
            throw new IOException(result.Error ?? "footprint-save-failed");
        }
        private void Writable() { if (!loaded || Clearing || protectedWrite) throw new InvalidOperationException("footprint-archive-protected"); }
        private static void RequireRead(PreferenceReadResult value) { if (value.Status != PreferenceReadStatus.Loaded) throw new IOException(value.Error ?? "footprint-read-failed"); }
    }
}
