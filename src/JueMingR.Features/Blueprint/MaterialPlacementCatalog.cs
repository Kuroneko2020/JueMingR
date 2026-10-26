using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Host compiles original metadata once in the existing placement pass.
    // These frame grids and descriptors are values; workers never call native
    // TileObjectData, selectors, inventory choices or a random generator.
    public sealed class BlueprintPlacementRule
    {
        private readonly int[] rows;
        public BlueprintPlacementRule(BlueprintPlacementDescriptor descriptor, bool simple, int baseX = 0, int baseY = 0,
            int columnStep = 18, int[] rowOffsets = null, int operatingX = 0, int operatingY = 0, bool door = false, int operatingY2 = 0)
        { Descriptor = descriptor; Simple = simple; BaseX = baseX; BaseY = baseY; ColumnStep = columnStep; rows = rowOffsets == null ? new[] { 0 } : (int[])rowOffsets.Clone(); OperatingX = operatingX; OperatingY = operatingY; OperatingY2 = operatingY2; Door = door; }
        public BlueprintPlacementDescriptor Descriptor { get; }
        public bool Simple { get; }
        public int BaseX { get; }
        public int BaseY { get; }
        public int ColumnStep { get; }
        public int OperatingX { get; }
        public int OperatingY { get; }
        public int OperatingY2 { get; }
        public bool Door { get; }
        public bool Matches(CaptureCell cell)
        {
            if ((cell.Layers & CaptureLayer.Tile) == 0) return false;
            if (Door)
            {
                if (cell.TileType != 10 && cell.TileType != 11 || cell.FrameX < 0 || cell.FrameY < 0) return false;
                int style = cell.FrameX / (cell.TileType == 10 ? 54 : 72) * 36 + cell.FrameY / 54;
                return style == Descriptor.Style && cell.FrameX % 18 == 0 && cell.FrameY % 18 == 0;
            }
            if (cell.TileType != Descriptor.TileType) return false;
            if (Descriptor.Platform) return cell.FrameY == (long)Descriptor.Style * 18;
            if (Simple) return true;
            return Cell(cell.FrameX - BaseX, cell.FrameY - BaseY) || OperatingX != 0 && Cell(cell.FrameX - BaseX - OperatingX, cell.FrameY - BaseY) ||
                OperatingY != 0 && Cell(cell.FrameX - BaseX, cell.FrameY - BaseY - OperatingY) || OperatingY2 != 0 && Cell(cell.FrameX - BaseX, cell.FrameY - BaseY - OperatingY2);
        }
        private bool Cell(int x, int y)
        { return ColumnStep > 0 && x >= 0 && x % ColumnStep == 0 && x / ColumnStep < Descriptor.Width && Array.IndexOf(rows, y) >= 0; }
    }

    public sealed class BlueprintPlacementCatalog
    {
        private readonly Dictionary<int, BlueprintPlacementRule[]> tiles;
        private readonly Dictionary<int, BlueprintPlacementDescriptor> walls;
        private readonly Dictionary<int, int> paints, coatings;
        public BlueprintPlacementCatalog(long revision, IEnumerable<BlueprintPlacementRule> rules, IDictionary<int, BlueprintPlacementDescriptor> walls,
            IDictionary<int, int> paints, IDictionary<int, int> coatings, int wire, int actuator, string targetVersion = "1.4.5.8")
        {
            Revision = revision; TargetVersion = targetVersion; WireItemType = wire; ActuatorItemType = actuator;
            this.walls = new Dictionary<int, BlueprintPlacementDescriptor>(walls); this.paints = new Dictionary<int, int>(paints); this.coatings = new Dictionary<int, int>(coatings);
            var grouped = new Dictionary<int, List<BlueprintPlacementRule>>();
            foreach (var rule in rules)
            {
                int tile = rule.Descriptor.TileType; List<BlueprintPlacementRule> list;
                if (!grouped.TryGetValue(tile, out list)) grouped.Add(tile, list = new List<BlueprintPlacementRule>());
                list.Add(rule);
                if (rule.Door) { if (!grouped.TryGetValue(11, out list)) grouped.Add(11, list = new List<BlueprintPlacementRule>()); list.Add(rule); }
            }
            tiles = new Dictionary<int, BlueprintPlacementRule[]>(); foreach (var pair in grouped) tiles.Add(pair.Key, pair.Value.ToArray());
        }
        public long Revision { get; }
        public string TargetVersion { get; }
        public int WireItemType { get; }
        public int ActuatorItemType { get; }
        public int Paint(int paint) { int result; return paints.TryGetValue(paint, out result) && result > 0 ? result : 0; }
        public int Coating(int coating) { int result; return coatings.TryGetValue(coating, out result) && result > 0 ? result : 0; }
        public BlueprintPlacementDescriptor Wall(int wall) { BlueprintPlacementDescriptor result; return walls.TryGetValue(wall, out result) ? result : null; }
        public BlueprintPlacementDescriptor Tile(CaptureCell cell)
        { return Tile(cell, null); }
        public BlueprintPlacementDescriptor Tile(CaptureCell cell, CaptureObject? obj)
        {
            BlueprintPlacementRule[] list; if (!tiles.TryGetValue(cell.TileType, out list)) return null;
            BlueprintPlacementDescriptor found = null;
            foreach (var rule in list)
            {
                if (!rule.Matches(cell)) continue;
                // Original anchor alternates can write a frame whose reverse
                // metadata uses a different canonical origin/direction. Those
                // reverse values are not operation permits or a second
                // material identity. Preserve the actual rule's style,
                // origin/direction/alternate for native execution; keep frame,
                // complete dimensions and unique consumption as evidence.
                if (obj.HasValue && !rule.Door && (rule.Descriptor.Width != obj.Value.Width || rule.Descriptor.Height != obj.Value.Height)) continue;
                if (found != null && !found.SameConsumption(rule.Descriptor)) return null;
                // A normal direct declaration is preferable to an optional
                // selector using the same material, but never resolves a
                // genuinely ambiguous material identity by item-ID order.
                if (found == null || found.Flexible && !rule.Descriptor.Flexible) found = rule.Descriptor;
            }
            return found;
        }
    }

    public sealed class BlueprintMaterialAccumulator
    {
        private readonly Dictionary<int, long> counts = new Dictionary<int, long>();
        public BlueprintMaterialStatus Status { get; private set; } = BlueprintMaterialStatus.Exact;
        public string Error { get; private set; }
        public IReadOnlyDictionary<int, long> Counts { get { return counts; } }
        public void Unresolved(string reason) { Status = BlueprintMaterialStatus.Unresolved; if (Error == null) Error = reason; }
        public void Add(int itemType, long count)
        {
            if (count == 0) return;
            if (itemType <= 0 || count < 0) { Unresolved("材料身份尚未确定"); return; }
            try { long old; counts.TryGetValue(itemType, out old); counts[itemType] = checked(old + count); }
            catch (OverflowException) { Unresolved("材料数量超出整数范围"); }
        }
        public void Add(BlueprintPlacementDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Units < 0 || descriptor.UseItemType <= 0) { Unresolved("目标没有唯一的直接材料解释"); return; }
            if (descriptor.Units > 0) Add(descriptor.ConsumeItemType, descriptor.Units);
            if (descriptor.RequiresSubstrate) Unresolved("地表转换的底材步骤尚未确定");
        }
        public BlueprintMaterialSnapshot Snapshot(long revision, BlueprintMaterialInventory inventory = null, BlueprintMaterialStatus? status = null)
        {
            try
            {
                var keys = new List<int>(counts.Keys); keys.Sort(); var rows = new BlueprintMaterialRow[keys.Count];
                for (int i = 0; i < keys.Count; i++) rows[i] = new BlueprintMaterialRow(keys[i], counts[keys[i]], inventory?.Held(keys[i]) ?? 0, inventory?.Ready == true);
                return new BlueprintMaterialSnapshot(revision, status ?? Status, rows, inventory?.Ready == true, Error);
            }
            catch (OverflowException) { return new BlueprintMaterialSnapshot(revision, BlueprintMaterialStatus.Unresolved, Array.Empty<BlueprintMaterialRow>(), false, "材料数量超出整数范围"); }
        }
    }
}
