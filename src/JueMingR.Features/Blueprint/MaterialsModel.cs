using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintMaterialStatus { Pending, Exact, Unresolved }
    public enum BlueprintPlacementKind { Tile, Wall }

    // A placement declaration describes nominal direct consumption. It is not
    // an operation permit, a world proof or an inventory reservation.
    public sealed class BlueprintPlacementDescriptor
    {
        public BlueprintPlacementDescriptor(BlueprintPlacementKind kind, int useItemType, int consumeItemType, long units,
            int tileType, int style, int wallType = 0, bool flexible = false, int width = 1, int height = 1,
            int originX = 0, int originY = 0, int alternate = 0, int direction = 0, bool requiresObject = false, bool platform = false, bool requiresSubstrate = false)
        {
            Kind = kind; UseItemType = useItemType; ConsumeItemType = consumeItemType; Units = units;
            TileType = tileType; Style = style; WallType = wallType; Flexible = flexible;
            Width = width; Height = height; OriginX = originX; OriginY = originY; Alternate = alternate; Direction = direction;
            RequiresObject = requiresObject; Platform = platform; RequiresSubstrate = requiresSubstrate;
        }
        public BlueprintPlacementKind Kind { get; }
        public int UseItemType { get; }
        public int ConsumeItemType { get; }
        public long Units { get; }
        public int TileType { get; }
        public int WallType { get; }
        public int Style { get; }
        public bool Flexible { get; }
        public int Width { get; }
        public int Height { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int Alternate { get; }
        public int Direction { get; }
        public bool RequiresObject { get; }
        public bool Platform { get; }
        public bool RequiresSubstrate { get; }
        public bool SameConsumption(BlueprintPlacementDescriptor other)
        { return other != null && ConsumeItemType == other.ConsumeItemType && Units == other.Units; }
    }

    public sealed class BlueprintMaterialRow
    {
        public BlueprintMaterialRow(int itemType, long required, long held = 0, bool inventoryReady = false)
        {
            if (itemType <= 0 || required < 0 || held < 0) throw new ArgumentOutOfRangeException(nameof(required));
            ItemType = itemType; Required = required; Held = held; InventoryReady = inventoryReady;
            Missing = inventoryReady ? checked(required > held ? required - held : 0) : 0;
        }
        public int ItemType { get; }
        public long Required { get; }
        public long Needed { get { return Required; } }
        public long Held { get; }
        public long Missing { get; }
        public bool InventoryReady { get; }
    }

    public sealed class BlueprintMaterialSnapshot
    {
        public static readonly BlueprintMaterialSnapshot Pending = new BlueprintMaterialSnapshot(0, BlueprintMaterialStatus.Pending, Array.Empty<BlueprintMaterialRow>());
        public static readonly BlueprintMaterialSnapshot Empty = new BlueprintMaterialSnapshot(0, BlueprintMaterialStatus.Exact, Array.Empty<BlueprintMaterialRow>(), true);
        public BlueprintMaterialSnapshot(long revision, BlueprintMaterialStatus status, BlueprintMaterialRow[] rows, bool inventoryReady = false, string error = null)
        {
            Revision = revision; Status = status; InventoryReady = inventoryReady; Error = error;
            Rows = Array.AsReadOnly((BlueprintMaterialRow[])(rows ?? throw new ArgumentNullException(nameof(rows))).Clone());
            long total = 0; int kinds = 0;
            foreach (var row in rows) if (row.Required > 0) { total = checked(total + row.Required); kinds++; }
            Total = total; KindCount = kinds;
        }
        private BlueprintMaterialSnapshot(long revision, BlueprintMaterialStatus status, IReadOnlyList<BlueprintMaterialRow> rows, long total, bool inventoryReady, string error)
        { Revision = revision; Status = status; Rows = rows; Total = total; KindCount = rows.Count; InventoryReady = inventoryReady; Error = error; }
        // Ownership transfers. The bounded publisher must never mutate this
        // list again; publication does not copy/recount a large accumulated set.
        public static BlueprintMaterialSnapshot FromOwnedRows(long revision, BlueprintMaterialStatus status, List<BlueprintMaterialRow> rows,
            long total, bool inventoryReady, string error = null)
        { return new BlueprintMaterialSnapshot(revision, status, rows.AsReadOnly(), total, inventoryReady, error); }
        public long Revision { get; }
        public BlueprintMaterialStatus Status { get; }
        public bool InventoryReady { get; }
        public IReadOnlyList<BlueprintMaterialRow> Rows { get; }
        public int KindCount { get; }
        public long Total { get; }
        public string Error { get; }
    }

    public sealed class BlueprintMaterialInstance
    {
        public BlueprintMaterialInstance(long session, long projection, long sequence, string name, int x, int y)
        { SessionGeneration = session; ProjectionGeneration = projection; Sequence = sequence; Name = name ?? "蓝图"; OriginX = x; OriginY = y; }
        public long SessionGeneration { get; }
        public long ProjectionGeneration { get; }
        public long Sequence { get; }
        public string Name { get; }
        public int OriginX { get; }
        public int OriginY { get; }
    }

    // Fifty value slots are frozen together once per demanded game update.
    // Live Item references belong solely to the short-lived native use owner.
    public readonly struct BlueprintMaterialInventorySlot
    {
        public BlueprintMaterialInventorySlot(int slot, int itemType, long stack, bool eligible)
        { Slot = slot; ItemType = itemType; Stack = stack; Eligible = eligible; }
        public int Slot { get; }
        public int ItemType { get; }
        public long Stack { get; }
        public bool Eligible { get; }
    }
    public sealed class BlueprintMaterialInventory
    {
        public BlueprintMaterialInventory(long session, long revision, ulong tick, bool ready, BlueprintMaterialInventorySlot[] slots)
        { SessionGeneration = session; Revision = revision; Tick = tick; Ready = ready; Slots = Array.AsReadOnly((BlueprintMaterialInventorySlot[])(slots ?? throw new ArgumentNullException(nameof(slots))).Clone()); }
        public long SessionGeneration { get; }
        public long Revision { get; }
        public ulong Tick { get; }
        public bool Ready { get; }
        public IReadOnlyList<BlueprintMaterialInventorySlot> Slots { get; }
        public long Held(int itemType)
        { long result = 0; foreach (var slot in Slots) if (slot.Eligible && slot.ItemType == itemType) result = checked(result + slot.Stack); return result; }
    }

    // Key excludes names, viewport and GPU identity. The immutable stage is
    // private to the operation; a template includes its reliable content key.
    public sealed class BlueprintMaterialKey : IEquatable<BlueprintMaterialKey>
    {
        public const int ParserVersion = 1;
        private readonly int[] excluded;
        public BlueprintMaterialKey(Guid id, bool stage, Guid contentRevision, string contentHash, bool excludeUnreliable,
            int[] excludedObjectIds, long placementRevision, string targetVersion = "1.4.5.8", int classifierVersion = BlueprintPlacementCatalog.ClassifierVersion)
        {
            Id = id; Stage = stage; ContentRevision = contentRevision; ContentHash = contentHash ?? "";
            ExcludeUnreliable = excludeUnreliable; PlacementRevision = placementRevision; TargetVersion = targetVersion; ClassifierVersion = classifierVersion;
            excluded = excludedObjectIds == null ? Array.Empty<int>() : (int[])excludedObjectIds.Clone(); Array.Sort(excluded);
        }
        public Guid Id { get; }
        public bool Stage { get; }
        public Guid ContentRevision { get; }
        public string ContentHash { get; }
        public bool ExcludeUnreliable { get; }
        public long PlacementRevision { get; }
        public string TargetVersion { get; }
        public int ClassifierVersion { get; }
        public bool Excluded(int objectId, bool unreliable) { return objectId > 0 && (ExcludeUnreliable && unreliable || Array.BinarySearch(excluded, objectId) >= 0); }
        public bool Equals(BlueprintMaterialKey other)
        {
            if (other == null || Id != other.Id || Stage != other.Stage || ContentRevision != other.ContentRevision || ContentHash != other.ContentHash ||
                ExcludeUnreliable != other.ExcludeUnreliable || PlacementRevision != other.PlacementRevision || TargetVersion != other.TargetVersion || ClassifierVersion != other.ClassifierVersion || excluded.Length != other.excluded.Length) return false;
            for (int i = 0; i < excluded.Length; i++) if (excluded[i] != other.excluded[i]) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as BlueprintMaterialKey); }
        public override int GetHashCode()
        { unchecked { int hash = Id.GetHashCode() ^ ContentRevision.GetHashCode() ^ ContentHash.GetHashCode() ^ PlacementRevision.GetHashCode() ^ ClassifierVersion ^ (TargetVersion?.GetHashCode() ?? 0) ^ (Stage ? 1 : 0) ^ (ExcludeUnreliable ? 2 : 0); foreach (int id in excluded) hash = hash * 397 ^ id; return hash; } }
    }
}
