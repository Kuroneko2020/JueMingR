using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using JueMingR.Platform.Blueprint;

namespace JueMingR.Infrastructure.Blueprint
{
    public sealed partial class FileBlueprintLibrary
    {
        private sealed class CleanupOwner : IDisposable
        {
            internal Guid Id;
            internal bool Stage, IndexVerified, NamesVerified;
            internal long Blocks, VerifiedRecords, NextBlock;
            internal StageOwner Private;
            internal BlueprintCleanupProof Proof;
            internal byte[] RootHash, IntentHash, IndexHash, Chain = new byte[32];
            internal FileStream Index;
            internal SHA256 Digest;
            internal IEnumerator<string> Names;
            internal readonly List<FileStream> Pins = new List<FileStream>(5);
            public void Dispose()
            {
                if (Names != null) Names.Dispose(); Names = null;
                if (Digest != null) Digest.Dispose(); Digest = null;
                if (Index != null) Index.Dispose(); Index = null;
                foreach (FileStream pin in Pins) pin.Dispose(); Pins.Clear();
            }
        }
        // One worker owns at most one cleanup. Its file handles deny ordinary
        // in-place writes and path replacement while an authenticated index is
        // reused. Closing it discards trust and forces fresh proof next time.
        private CleanupOwner cleanupOwner;
#if DEBUG
        public long DebugCleanupIndexScans { get; private set; }
        public long DebugCleanupIndexBytes { get; private set; }
        public long DebugCleanupChainRecords { get; private set; }
        public long DebugCleanupNameVisits { get; private set; }
        public long DebugCleanupBlockVisits { get; private set; }
#endif
        public void CloseCleanup() { if (cleanupOwner != null) cleanupOwner.Dispose(); cleanupOwner = null; }
        public bool CleanupStep(Guid id, bool stage, long blockCount, int budget, BlueprintCleanupProof proof = null)
        {
            Ready(); if (budget < 1 || budget > 64 || blockCount < 0 || blockCount > Int64.MaxValue / 32L) throw new ArgumentOutOfRangeException(nameof(budget));
            try
            {
                if (cleanupOwner == null) cleanupOwner = OpenCleanup(id, stage, blockCount, proof);
                CleanupOwner owner = cleanupOwner;
                if (owner.Id != id || owner.Stage != stage || owner.Blocks != blockCount || !stage && !SameProof(owner.Proof, proof)) throw new InvalidOperationException("blueprint-cleanup-owner-busy");
                // Proof validation is incremental as well as deletion. A large
                // index cannot hide an uncancellable full scan inside one batch.
                if (!owner.IndexVerified) { VerifyCleanupIndex(owner, budget); return false; }
                if (!owner.NamesVerified) { VerifyCleanupNames(owner, budget); return false; }
                byte[] expected = new byte[32]; int visited = 0;
                while (owner.NextBlock < blockCount && visited++ < budget)
                {
                    long number = owner.NextBlock++; string path = Block(id, stage, number); Guard(path);
#if DEBUG
                    DebugCleanupBlockVisits++;
#endif
                    if (Directory.Exists(path)) throw new InvalidDataException("blueprint-cleanup-unknown-chunk");
                    if (!File.Exists(path)) continue; // An earlier safe batch may have deleted it.
                    if (owner.Index == null) throw new InvalidDataException("blueprint-cleanup-missing-index");
                    owner.Index.Position = checked(number * 32L); ReadExactly(owner.Index, expected);
#if DEBUG
                    DebugCleanupIndexBytes += 32;
#endif
                    DeleteVerified(path, expected, false, MaximumBlockBytes);
                }
                if (owner.NextBlock < blockCount) return false;
                FinishCleanup(owner); CloseCleanup(); return true;
            }
            catch { CloseCleanup(); throw; }
        }
        private CleanupOwner OpenCleanup(Guid id, bool stage, long blocks, BlueprintCleanupProof proof)
        {
            var owner = new CleanupOwner { Id = id, Stage = stage, Blocks = blocks, Proof = proof };
            try
            {
                if (stageWriter == id) CloseStageWriter();
                owner.Private = stage ? ReadOwner(id) : null;
                if (stage && owner.Private.Blocks != blocks || !stage && proof == null) throw new InvalidDataException("blueprint-cleanup-proof-required");
                if (!stage)
                {
                    BlueprintCleanupProof intent = ReadCleanupIntent(id);
                    if (!SameProof(intent, proof)) throw new InvalidDataException("blueprint-cleanup-intent-changed");
                    owner.IntentHash = Hash(CleanupIntentBytes(id, intent));
                }
                ValidateCleanupNames(id, stage);
                string content = Content(id, stage), asset = Asset(id);
                owner.RootHash = stage ? owner.Private.Root : IdentityBytes(proof.RootIdentity);
                PinCleanupFile(owner, Path.Combine(content, "root.bin"), owner.RootHash, true);
                if (stage) PinCleanupFile(owner, Path.Combine(content, ".owner.bin"), owner.Private.OwnerIdentity, false);
                else
                {
                    PinCleanupFile(owner, Path.Combine(asset, ".delete.bin"), owner.IntentHash, false);
                    PinCleanupFile(owner, Path.Combine(asset, "metadata.json"), IdentityBytes(proof.MetadataIdentity), false);
                    PinCleanupFile(owner, Path.Combine(asset, "metadata.json.bak"), IdentityBytes(proof.SourceMetadataIdentity), true);
                }
                string indexPath = Path.Combine(content, "index.bin"); Guard(indexPath);
                if (Directory.Exists(indexPath)) throw new InvalidDataException("blueprint-cleanup-index-directory");
                if (File.Exists(indexPath))
                {
                    owner.Index = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (owner.Index.Length != checked(blocks * 32L)) throw new InvalidDataException("blueprint-cleanup-index-length");
                    owner.Digest = SHA256.Create();
#if DEBUG
                    DebugCleanupIndexScans++;
#endif
                }
                else owner.IndexVerified = true; // Only a completed cleanup tail may lack an index.
                string chunks = Path.Combine(content, "chunks"); Guard(chunks);
                if (Directory.Exists(chunks)) owner.Names = Directory.EnumerateFileSystemEntries(chunks).GetEnumerator();
                else owner.NamesVerified = true;
                return owner;
            }
            catch { owner.Dispose(); throw; }
        }
        private static void PinCleanupFile(CleanupOwner owner, string path, byte[] expected, bool allowMissing)
        {
            Guard(path); if (Directory.Exists(path)) throw new InvalidDataException("blueprint-cleanup-file-is-directory");
            if (!File.Exists(path)) { if (allowMissing) return; throw new InvalidDataException("blueprint-cleanup-missing-file"); }
            if (expected == null) throw new InvalidDataException("blueprint-cleanup-unowned-file");
            var pin = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            owner.Pins.Add(pin);
            if (pin.Length > 65536 || !Equal(Hash(pin), expected)) throw new InvalidDataException("blueprint-cleanup-external-file");
        }
        private void VerifyCleanupIndex(CleanupOwner owner, int budget)
        {
            var record = new byte[32]; int read = 0;
            while (owner.VerifiedRecords < owner.Blocks && read++ < budget)
            {
                ReadExactly(owner.Index, record); owner.Digest.TransformBlock(record, 0, record.Length, record, 0);
                if (owner.Stage) owner.Chain = Chain(owner.Chain, record);
                owner.VerifiedRecords++;
#if DEBUG
                DebugCleanupIndexBytes += 32;
                if (owner.Stage) DebugCleanupChainRecords++;
#endif
            }
            if (owner.VerifiedRecords != owner.Blocks) return;
            owner.Digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0); owner.IndexHash = owner.Digest.Hash;
            if (owner.Stage ? !Equal(owner.Chain, owner.Private.Chain) : !Equal(owner.IndexHash, IdentityBytes(owner.Proof.IndexIdentity))) throw new InvalidDataException("blueprint-cleanup-index-identity");
            owner.Digest.Dispose(); owner.Digest = null; owner.IndexVerified = true;
        }
        private void VerifyCleanupNames(CleanupOwner owner, int budget)
        {
            int visited = 0;
            while (visited++ < budget)
            {
                if (!owner.Names.MoveNext()) { owner.Names.Dispose(); owner.Names = null; owner.NamesVerified = true; return; }
                string path = owner.Names.Current; Guard(path); long number;
                if (Directory.Exists(path) || !BlockNumber(Path.GetFileName(path), out number) || number >= owner.Blocks || owner.Index == null) throw new InvalidDataException("blueprint-cleanup-unknown-chunk");
#if DEBUG
                DebugCleanupNameVisits++;
#endif
            }
        }
        private void FinishCleanup(CleanupOwner owner)
        {
            string content = Content(owner.Id, owner.Stage), asset = Asset(owner.Id), chunks = Path.Combine(content, "chunks");
            Guard(chunks); if (Directory.Exists(chunks)) Directory.Delete(chunks, false);
            ValidateCleanupNames(owner.Id, owner.Stage);
            // The final small-file boundary cannot retain no-delete pins while
            // removing them. Recheck exact identities after release; the index
            // receives one final linear digest, never a full scan per batch.
            if (owner.Index != null) owner.Index.Dispose(); owner.Index = null;
            foreach (FileStream pin in owner.Pins) pin.Dispose(); owner.Pins.Clear();
            string index = Path.Combine(content, "index.bin"); Guard(index);
            if (File.Exists(index))
            {
                if (owner.IndexHash == null) throw new InvalidDataException("blueprint-cleanup-unowned-index");
#if DEBUG
                DebugCleanupIndexScans++; DebugCleanupIndexBytes += checked(owner.Blocks * 32L);
#endif
                DeleteVerified(index, owner.IndexHash, false, Int64.MaxValue);
            }
            DeleteVerified(Path.Combine(content, "root.bin"), owner.RootHash, true);
            if (owner.Stage) DeleteVerified(Path.Combine(content, ".owner.bin"), owner.Private.OwnerIdentity, false);
            if (Directory.Exists(content)) Directory.Delete(content, false);
            if (owner.Stage) return;
            CloseMetadata(); ValidateCleanupNames(owner.Id, false);
            DeleteVerified(Path.Combine(asset, "metadata.json.bak"), IdentityBytes(owner.Proof.SourceMetadataIdentity), true);
            DeleteVerified(Path.Combine(asset, "metadata.json"), IdentityBytes(owner.Proof.MetadataIdentity), false);
            string lockPath = Path.Combine(asset, "metadata.json.lock"); Guard(lockPath);
            if (File.Exists(lockPath))
                using (var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                { if (stream.Length != 0) throw new InvalidDataException("blueprint-cleanup-external-lock"); File.Delete(lockPath); }
            DeleteVerified(Path.Combine(asset, ".delete.bin"), owner.IntentHash, false);
            Directory.Delete(asset, false);
        }
    }
}
