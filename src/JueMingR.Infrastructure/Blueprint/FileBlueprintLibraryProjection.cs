using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.Blueprint;

namespace JueMingR.Infrastructure.Blueprint
{
    public sealed partial class FileBlueprintLibrary : IBlueprintLibraryLayoutSteps
    {
        public IEnumerable<int> ValidateLayoutSteps(Guid id, bool stage, long blockCount, int budget)
        {
            Ready(); if (blockCount < 0 || blockCount > Int64.MaxValue / 32L) throw new InvalidDataException("blueprint-block-count");
            if (budget < 1 || budget > 64) throw new ArgumentOutOfRangeException(nameof(budget));
            string content = Content(id, stage); Guard(content); int direct = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(content))
            {
                Guard(entry); string name = Path.GetFileName(entry);
                if (++direct > (stage ? 4 : 3) || !(name == "root.bin" && File.Exists(entry) || name == "index.bin" && File.Exists(entry) || name == "chunks" && Directory.Exists(entry) || stage && name == ".owner.bin" && File.Exists(entry))) throw new InvalidDataException("blueprint-unknown-content-entry");
            }
            using (Stream index = OpenIndex(id, stage)) if (index.Length != blockCount * 32L) throw new InvalidDataException("blueprint-index-length");
            string chunks = Path.Combine(content, "chunks"); Guard(chunks); long found = 0; int batch = 0;
            if (Directory.Exists(chunks)) foreach (string entry in Directory.EnumerateFileSystemEntries(chunks))
            {
                Guard(entry); long number;
                if (Directory.Exists(entry) || !BlockNumber(Path.GetFileName(entry), out number) || number >= blockCount) throw new InvalidDataException("blueprint-unknown-chunk");
                if (new FileInfo(entry).Length < 1 || new FileInfo(entry).Length > MaximumBlockBytes) throw new InvalidDataException("blueprint-chunk-length");
                found++; if (++batch == budget) { yield return batch; batch = 0; }
            }
            if (found != blockCount) throw new InvalidDataException("blueprint-missing-chunk");
            if (batch != 0) yield return batch;
        }
    }
}
