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
        public bool TryMemberFrame(int column, int row, out short x, out short y)
        {
            x = y = 0;
            if (column < 0 || row < 0 || column >= Descriptor.Width || row >= Descriptor.Height) return false;
            long frameX = Door ? (long)(Descriptor.Style / 36) * 54 : BaseX + (long)column * ColumnStep;
            long frameY = Door ? (long)(Descriptor.Style % 36) * 54 + row * 18 :
                Descriptor.Platform || Descriptor.PlanterBox ? (long)Descriptor.Style * 18 : row < rows.Length ? BaseY + (long)rows[row] : -1;
            if (frameX < 0 || frameY < 0 || frameX > short.MaxValue || frameY > short.MaxValue) return false;
            x = (short)frameX; y = (short)frameY; return true;
        }
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
            if (Descriptor.PlanterBox) return cell.FrameY == (long)Descriptor.Style * 18 &&
                cell.FrameX >= 0 && cell.FrameX <= 54 && cell.FrameX % 18 == 0 && !cell.HalfBrick && cell.Slope == 0;
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
        private readonly Dictionary<int, BlueprintPlacementDeclaration[]> declarations;
        public const int ClassifierVersion = 3;
        public BlueprintPlacementCatalog(long revision, IEnumerable<BlueprintPlacementRule> rules, IDictionary<int, BlueprintPlacementDescriptor> walls,
            IDictionary<int, int> paints, IDictionary<int, int> coatings, int wire, int actuator, string targetVersion = "1.4.5.8",
            IEnumerable<BlueprintPlacementDeclaration> declarations = null, BlueprintProducerCoverage coverage = null,
            BlueprintSubstitutionCatalog substitution = null)
        {
            Revision = revision; TargetVersion = targetVersion; WireItemType = wire; ActuatorItemType = actuator; Coverage = coverage;
            Substitution = substitution ?? BlueprintSubstitutionCatalog.Empty;
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
            var declared = new Dictionary<int, List<BlueprintPlacementDeclaration>>();
            if (declarations != null) foreach (var value in declarations)
            {
                DeclarationCount++; if (!value.Compiled) UncompiledDeclarationCount++;
                if (value.Kind != BlueprintPlacementKind.Tile) continue;
                List<BlueprintPlacementDeclaration> list;
                if (!declared.TryGetValue(value.TileType, out list)) declared.Add(value.TileType, list = new List<BlueprintPlacementDeclaration>());
                list.Add(value);
            }
            this.declarations = new Dictionary<int, BlueprintPlacementDeclaration[]>();
            foreach (var pair in declared) this.declarations.Add(pair.Key, pair.Value.ToArray());
        }
        public long Revision { get; }
        public string TargetVersion { get; }
        public BlueprintProducerCoverage Coverage { get; }
        public BlueprintSubstitutionCatalog Substitution { get; }
        public int DeclarationCount { get; }
        public int UncompiledDeclarationCount { get; }
        public int WireItemType { get; }
        public int ActuatorItemType { get; }
        public int Paint(int paint) { int result; return paints.TryGetValue(paint, out result) && result > 0 ? result : 0; }
        public int Coating(int coating) { int result; return coatings.TryGetValue(coating, out result) && result > 0 ? result : 0; }
        public BlueprintPlacementDescriptor Wall(int wall) { BlueprintPlacementDescriptor result; return walls.TryGetValue(wall, out result) ? result : null; }
        public BlueprintPlacementDescriptor Tile(CaptureCell cell)
        { return Tile(cell, null); }
        public BlueprintPlacementDescriptor Tile(CaptureCell cell, CaptureObject? obj)
        { return QualifyTile(cell, obj).Placement; }
        // The material bill may collapse equal consumption, while execution
        // must retain every matching tool/bucket/variant identity. This finite
        // query filters the compiled rules into a finite list; it contains no
        // native objects or permissions and does not cache a target's result.
        public IReadOnlyList<BlueprintPlacementDescriptor> Producers(CaptureCell cell, CaptureObject? obj = null)
        {
            BlueprintPlacementRule[] rules;
            if (!tiles.TryGetValue(cell.TileType, out rules)) return Array.Empty<BlueprintPlacementDescriptor>();
            var result = new List<BlueprintPlacementDescriptor>();
            foreach (var rule in rules)
                if (rule.Matches(cell) && (!obj.HasValue || rule.Door || rule.Descriptor.Width == obj.Value.Width && rule.Descriptor.Height == obj.Value.Height))
                    result.Add(rule.Descriptor);
            return result;
        }
        // The nine explicit construction recipes choose their known seed,
        // not an arbitrary first producer. General qualification may still be
        // ambiguous (grass also has nonconsuming regrowth tools); preserve that
        // fact while sharing this conservative base+seed plan with billing.
        public BlueprintPlacementDescriptor Substrate(CaptureCell cell)
        {
            BlueprintSubstrateRecipe recipe; BlueprintPlacementRule[] rules;
            if (TargetVersion != "1.4.5.8" || cell.ObjectId > 0 || cell.UnreliableObject || !BlueprintSubstrateRecipe.TryGet(cell.TileType, out recipe) ||
                !tiles.TryGetValue(cell.TileType, out rules)) return null;
            BlueprintPlacementDeclaration[] declared;
            if (declarations.TryGetValue(cell.TileType, out declared))
                foreach (var value in declared)
                    if (value.UseItemType == recipe.ConversionItem && !value.Compiled && value.Style == 0) return null;
            foreach (var rule in rules)
            {
                var value = rule.Descriptor;
                if (rule.Simple && rule.Matches(cell) && value.Kind == BlueprintPlacementKind.Tile && value.RequiresSubstrate && value.Producer == null &&
                    !value.RequiresObject && value.Width == 1 && value.Height == 1 &&
                    value.Style == 0 && value.UseItemType == recipe.ConversionItem && value.ConsumeItemType == recipe.ConversionItem && value.Units == 1)
                    return value;
            }
            return null;
        }
        // Body comparison may borrow compiled shape hints even when another
        // producer prevents a unique bill. This is not a placement/candidate
        // outlet; Tile returns only a fully explained qualification. The Host
        // may use these geometry hints for an already-present body, but must
        // still prove that body and its current native operation separately.
        public BlueprintPlacementDescriptor TileShape(CaptureCell cell)
        { return Substrate(cell) ?? ResolveTile(cell, null, out _); }
        private BlueprintPlacementDescriptor ResolveTile(CaptureCell cell, CaptureObject? obj, out bool ambiguous)
        {
            ambiguous = false;
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
                if (found != null && !found.SameConsumption(rule.Descriptor)) { ambiguous = true; return null; }
                // A normal direct declaration is preferable to an optional
                // selector using the same material, but never resolves a
                // genuinely ambiguous material identity by item-ID order.
                if (found == null || found.Flexible && !rule.Descriptor.Flexible) found = rule.Descriptor;
            }
            return found;
        }
        private BlueprintTargetQualification Qualification(BlueprintTargetDisposition disposition, BlueprintPlacementResolutionKind resolution,
            BlueprintPlacementDescriptor placement, BlueprintTargetReason reason)
        { return new BlueprintTargetQualification(disposition, resolution, placement, reason, Revision); }
        public BlueprintTargetQualification QualifyTile(CaptureCell cell, CaptureObject? obj = null)
        {
            if (TargetVersion != "1.4.5.8") return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.VersionMismatch);
            if (cell.UnreliableObject || obj.HasValue && (!obj.Value.Reliable || obj.Value.Type != cell.TileType))
                return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.UnreliableObject);
            bool ambiguous; var placement = ResolveTile(cell, obj, out ambiguous);
            if (ambiguous) return Qualification(BlueprintTargetDisposition.Construction, BlueprintPlacementResolutionKind.Ambiguous, null, BlueprintTargetReason.AmbiguousConsumption);
            BlueprintPlacementDeclaration[] declared;
            bool hasDeclarations = declarations.TryGetValue(cell.TileType, out declared);
            if (hasDeclarations)
            {
                // A successful rule does not account for an uncompiled producer
                // of this output. Declarations retain no consumed item/units or
                // failed grid, so even a second rule from the same use item may
                // change the bill. Only reliable style facts may exclude it;
                // absent object metadata keeps every failed style conservative.
                foreach (var value in declared) if (!value.Compiled && (!obj.HasValue || value.Style == obj.Value.PlacementStyle || value.Style == obj.Value.Style || placement != null && value.Style == placement.Style))
                    return Qualification(BlueprintTargetDisposition.Construction, BlueprintPlacementResolutionKind.Unsupported, null, BlueprintTargetReason.DeclaredOutputUnsupported);
            }
            if (placement != null) return Qualification(BlueprintTargetDisposition.Construction, BlueprintPlacementResolutionKind.Exact, placement, BlueprintTargetReason.None);
            if (hasDeclarations)
            {
                // A frame that no rule explains remains a qualification unknown;
                // even a completed scan cannot turn a decoder gap into scenery.
                return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.Unsupported, null, BlueprintTargetReason.TargetShapeUnresolved);
            }
            if (tiles.ContainsKey(cell.TileType)) return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.Unsupported, null, BlueprintTargetReason.TargetShapeUnresolved);
            if (Coverage == null || !Coverage.TileComplete(cell.TileType)) return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.None, null,
                Coverage != null && !Coverage.SourceAbiValid ? BlueprintTargetReason.SourceAbiFailure : BlueprintTargetReason.ProducerCoverageIncomplete);
            return Qualification(BlueprintTargetDisposition.DisplayOnly, BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.NoPlayerProducer);
        }
        public BlueprintTargetQualification QualifyWall(int wall)
        {
            if (TargetVersion != "1.4.5.8") return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.VersionMismatch);
            BlueprintPlacementDescriptor placement;
            if (walls.TryGetValue(wall, out placement)) return Qualification(BlueprintTargetDisposition.Construction,
                placement == null ? BlueprintPlacementResolutionKind.Ambiguous : BlueprintPlacementResolutionKind.Exact, placement,
                placement == null ? BlueprintTargetReason.AmbiguousConsumption : BlueprintTargetReason.None);
            if (Coverage == null || !Coverage.WallComplete(wall)) return Qualification(BlueprintTargetDisposition.Unresolved, BlueprintPlacementResolutionKind.None, null,
                Coverage != null && !Coverage.SourceAbiValid ? BlueprintTargetReason.SourceAbiFailure : BlueprintTargetReason.ProducerCoverageIncomplete);
            return Qualification(BlueprintTargetDisposition.DisplayOnly, BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.NoPlayerProducer);
        }
        public BlueprintConstructionSelection Select(CaptureCell cell, CaptureObject? obj = null)
        { return BlueprintConstructionTargets.Select(cell, this, obj); }
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
            if (descriptor.Producer?.ConsumesBucket == true && descriptor.Producer.ConsumesHeld && descriptor.UseItemType != descriptor.ConsumeItemType)
                Add(descriptor.UseItemType, 1);
            if (descriptor.RequiresSubstrate)
            {
                BlueprintSubstrateRecipe recipe;
                if (BlueprintSubstrateRecipe.TryGet(descriptor.TileType, out recipe)) Add(recipe.BaseItem, 1);
                else Unresolved("地表转换的底材步骤尚未确定");
            }
        }
        public void Add(BlueprintTargetQualification target)
        {
            if (target.Disposition == BlueprintTargetDisposition.DisplayOnly) return;
            if (target.Disposition != BlueprintTargetDisposition.Construction || target.Resolution != BlueprintPlacementResolutionKind.Exact)
            { Unresolved(target.MaterialReason); return; }
            Add(target.Placement);
        }
        public BlueprintMaterialSnapshot Snapshot(long revision, BlueprintMaterialInventory inventory = null, BlueprintMaterialStatus? status = null,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            try
            {
                var keys = new List<int>(counts.Keys); keys.Sort(); var rows = new BlueprintMaterialRow[keys.Count];
                var allocation = inventory?.Ready == true && policy.Enabled && catalog?.Substitution.Ready == true ?
                    new BlueprintMaterialAllocation(counts, inventory, catalog, policy) : null;
                for (int i = 0; i < keys.Count; i++) rows[i] = allocation?.Row(keys[i]) ?? new BlueprintMaterialRow(keys[i], counts[keys[i]], inventory?.Held(keys[i]) ?? 0, inventory?.Ready == true);
                return new BlueprintMaterialSnapshot(revision, status ?? Status, rows, inventory?.Ready == true, Error);
            }
            catch (OverflowException) { return new BlueprintMaterialSnapshot(revision, BlueprintMaterialStatus.Unresolved, Array.Empty<BlueprintMaterialRow>(), false, "材料数量超出整数范围"); }
        }
    }
}
