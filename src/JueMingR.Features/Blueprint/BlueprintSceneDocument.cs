using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintSceneRecord
    {
        public BlueprintSceneRecord(BlueprintTemplateKey template, int originX, int originY, long order, BlueprintProjectionMask mask, bool mirroredHorizontally = false)
        { Template = template ?? throw new ArgumentNullException(nameof(template)); OriginX = originX; OriginY = originY; Order = order; Mask = mask ?? BlueprintProjectionMask.Empty; MirroredHorizontally = mirroredHorizontally; }
        public BlueprintTemplateKey Template { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public long Order { get; }
        public BlueprintProjectionMask Mask { get; }
        public bool MirroredHorizontally { get; }
    }

    // Only committed scene facts cross the file boundary. Template summaries,
    // world Match, input/source ownership and execution generations are rebuilt.
    public sealed class BlueprintSceneDocument
    {
        public BlueprintSceneDocument(string scope, int width, int height, long revision, IReadOnlyList<BlueprintSceneRecord> records, int sourceVersion = 2)
        {
            ValidateScope(scope);
            if (width <= 0 || height <= 0 || revision < 0 || records == null) throw new ArgumentException("blueprint-scene-header");
            Scope = scope; Width = width; Height = height; Revision = revision;
            SourceVersion = sourceVersion;
            var copy = new BlueprintSceneRecord[records.Count]; long order = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                var item = records[i];
                if (item == null || item.Template.AssetId == Guid.Empty || item.Template.ContentRevision == Guid.Empty ||
                    item.OriginX < 0 || item.OriginY < 0 || item.OriginX >= width || item.OriginY >= height || item.Order <= order)
                    throw new ArgumentException("blueprint-scene-record");
                byte[] hash = Convert.FromBase64String(item.Template.ContentHash);
                if (hash.Length != 32 || Convert.ToBase64String(hash) != item.Template.ContentHash) throw new ArgumentException("blueprint-scene-template-hash");
                order = item.Order; copy[i] = item;
            }
            Records = new ReadOnlyCollection<BlueprintSceneRecord>(copy);
        }
        public string Scope { get; }
        public int Width { get; }
        public int Height { get; }
        public long Revision { get; }
        // SourceVersion is a validated decode fact, not a requested writer mode.
        // A real committed mutation writes v2; merely loading v1 never rewrites it.
        public int SourceVersion { get; }
        public IReadOnlyList<BlueprintSceneRecord> Records { get; }
        public static void ValidateScope(string value)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("blueprint-scene-identity");
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new ArgumentException("blueprint-scene-identity");
        }
    }
}
