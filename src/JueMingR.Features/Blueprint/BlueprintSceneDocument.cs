using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Features.Blueprint
{
    // A confirmed intermediate foreground, relative to this exact committed
    // scene pose. Expected.ObjectId identifies the final template object for
    // mask retirement; its other foreground fields describe the actual stone.
    // It contains no inventory, action token or completion claim.
    public sealed class BlueprintHoikPending
    {
        public BlueprintHoikPending(CaptureCell expected)
        {
            if (expected.X < 0 || expected.Y < 0 || expected.ObjectId < 0 || expected.TileType != 1 || expected.Slope > 4 || expected.HalfBrick && expected.Slope != 0)
                throw new ArgumentException("blueprint-hoik-pending");
            Expected = expected;
        }
        public CaptureCell Expected { get; }
    }
    public sealed class BlueprintSceneRecord
    {
        public BlueprintSceneRecord(BlueprintTemplateKey template, int originX, int originY, long order, BlueprintProjectionMask mask, bool mirroredHorizontally = false, IReadOnlyList<BlueprintHoikPending> hoikPending = null)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template)); OriginX = originX; OriginY = originY; Order = order; Mask = mask ?? BlueprintProjectionMask.Empty; MirroredHorizontally = mirroredHorizontally;
            var copy = new BlueprintHoikPending[hoikPending?.Count ?? 0]; var seen = new HashSet<long>();
            for (int n = 0; n < copy.Length; n++)
            {
                var item = hoikPending[n] ?? throw new ArgumentException("blueprint-hoik-pending");
                if (!seen.Add(((long)item.Expected.Y << 32) | (uint)item.Expected.X)) throw new ArgumentException("blueprint-hoik-duplicate");
                copy[n] = item;
            }
            HoikPending = new ReadOnlyCollection<BlueprintHoikPending>(copy);
        }
        public BlueprintTemplateKey Template { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public long Order { get; }
        public BlueprintProjectionMask Mask { get; }
        public bool MirroredHorizontally { get; }
        public IReadOnlyList<BlueprintHoikPending> HoikPending { get; }
    }

    // Only committed scene facts cross the file boundary. Template summaries,
    // world Match, input/source ownership and execution generations are rebuilt.
    public sealed class BlueprintSceneDocument
    {
        public BlueprintSceneDocument(string scope, int width, int height, long revision, IReadOnlyList<BlueprintSceneRecord> records, int sourceVersion = 3)
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
        // A real committed mutation writes v3; merely loading v1/v2 never rewrites it.
        public int SourceVersion { get; }
        public IReadOnlyList<BlueprintSceneRecord> Records { get; }
        public static void ValidateScope(string value)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("blueprint-scene-identity");
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new ArgumentException("blueprint-scene-identity");
        }
    }
}
