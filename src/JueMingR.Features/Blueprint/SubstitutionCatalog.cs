using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // A native pass freezes physical placement structure separately from the
    // art/frame encoding. The rule and decoded prototype retain the latter for
    // a concrete execution; none of these values grants an operation permit.
    public sealed class BlueprintSubstitutionMember
    {
        private readonly int[] structure;
        public BlueprintSubstitutionMember(BlueprintSubstitutionCategory category, BlueprintPlacementRule rule,
            int[] physicalStructure, CaptureObject prototype = default(CaptureObject), bool safeWall = false)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            Category = category; Rule = rule; Prototype = prototype;
            structure = physicalStructure == null ? Array.Empty<int>() : (int[])physicalStructure.Clone(); SafeWall = safeWall;
        }
        public BlueprintSubstitutionCategory Category { get; }
        public BlueprintPlacementRule Rule { get; }
        public BlueprintPlacementDescriptor Placement { get { return Rule.Descriptor; } }
        public CaptureObject Prototype { get; }
        public bool SafeWall { get; }
        internal bool Compatible(BlueprintSubstitutionMember other)
        {
            if (other == null || Category != other.Category || Placement.Kind != other.Placement.Kind ||
                Category == BlueprintSubstitutionCategory.Wall && (!SafeWall || !other.SafeWall) || structure.Length != other.structure.Length) return false;
            for (int i = 0; i < structure.Length; i++) if (structure[i] != other.structure[i]) return false;
            return true;
        }
        internal bool Matches(CaptureCell cell, CaptureObject? obj = null)
        {
            if (!Rule.Matches(cell)) return false;
            if (!obj.HasValue) return true;
            var value = obj.Value;
            return value.Reliable && (Rule.Door || value.Width == Prototype.Width && value.Height == Prototype.Height &&
                value.Direction == Prototype.Direction && value.Style == Prototype.Style && value.Alternate == Prototype.Alternate);
        }
        internal bool Matches(BlueprintWorldObject value)
        {
            if (value == null) return false;
            if (Placement.PlanterBox) return false;
            // Native semantics canonicalize raw Tile10/11 to Kind10 and
            // Direction0. Left/right opening changes only the occupied bounds;
            // neither raw Tile11 nor its sprite direction is building identity.
            if (Rule.Door) return value.Family == BlueprintWorldObjectFamily.Door && value.Kind == Placement.TileType && value.Direction == 0 &&
                value.Style == Placement.Style && value.Height == 3 && value.Width >= 1 && value.Width <= 2 && value.WorldY == value.AnchorY &&
                (value.WorldX == value.AnchorX || value.Width == 2 && (long)value.WorldX + 1 == value.AnchorX);
            return value.Family == BlueprintWorldObjectFamily.Metadata && value.Kind == Placement.TileType &&
                value.Style == Prototype.Style && value.Direction == Prototype.Direction && value.Width == Prototype.Width && value.Height == Prototype.Height &&
                Rule.Matches(new CaptureCell { Layers = CaptureLayer.Tile, TileType = value.Kind,
                    FrameX = (short)(value.FrameGroup >> 32), FrameY = (short)value.FrameGroup });
        }
    }

    public sealed class BlueprintSubstitutionCatalog
    {
        public static readonly BlueprintSubstitutionCatalog Empty = new BlueprintSubstitutionCatalog(0, null, false);
        private readonly Dictionary<long, BlueprintSubstitutionMember[]> products = new Dictionary<long, BlueprintSubstitutionMember[]>();
        private readonly Dictionary<long, BlueprintSubstitutionMember[]> outputs = new Dictionary<long, BlueprintSubstitutionMember[]>();
        private readonly HashSet<int> singleCellKinds = new HashSet<int>();
        private readonly Dictionary<int, BlueprintSubstitutionMember> walls = new Dictionary<int, BlueprintSubstitutionMember>();
        private readonly Dictionary<BlueprintPlacementDescriptor, ChoiceSet> choices = new Dictionary<BlueprintPlacementDescriptor, ChoiceSet>();
        private readonly Dictionary<int, MaterialSet> materials = new Dictionary<int, MaterialSet>();
        private readonly IReadOnlyList<BlueprintSubstitutionMember> members;
        private sealed class ChoiceSet
        {
            internal BlueprintSubstitutionMember Source;
            internal IReadOnlyList<BlueprintPlacementDescriptor> All;
            internal readonly Dictionary<int, IReadOnlyList<BlueprintPlacementDescriptor>> ByItem = new Dictionary<int, IReadOnlyList<BlueprintPlacementDescriptor>>();
        }
        private sealed class MaterialSet
        {
            internal BlueprintSubstitutionCategory Category;
            internal IReadOnlyList<int> Items;
        }
        public BlueprintSubstitutionCatalog(long revision, IEnumerable<BlueprintSubstitutionMember> values, bool ready = true)
        {
            Revision = revision; Ready = ready;
            var all = new List<BlueprintSubstitutionMember>();
            if (ready && values != null) foreach (var member in values)
            {
                if (member == null || (int)member.Category < 0 || (int)member.Category >= 12) continue;
                var p = member.Placement;
                // Substitution is the direct consumable lane. Flexible tools,
                // bucket ammo and substrate recipes retain their exact owner.
                if (p.UseItemType <= 0 || p.ConsumeItemType != p.UseItemType || p.Units != 1 || p.Producer != null || p.Flexible || p.RequiresSubstrate ||
                    p.Kind == BlueprintPlacementKind.Wall && (member.Category != BlueprintSubstitutionCategory.Wall || !member.SafeWall)) continue;
                all.Add(member);
            }
            all.Sort((a, b) => { int c = a.Placement.UseItemType.CompareTo(b.Placement.UseItemType); return c != 0 ? c : a.Placement.Alternate.CompareTo(b.Placement.Alternate); });
            members = all.AsReadOnly();
            var byProduct = new Dictionary<long, List<BlueprintSubstitutionMember>>();
            var byOutput = new Dictionary<long, List<BlueprintSubstitutionMember>>();
            foreach (var member in all)
            {
                if (member.Placement.Kind == BlueprintPlacementKind.Wall) { walls[member.Placement.WallType] = member; continue; }
                Group(byProduct, Product(member.Placement.TileType, member.Prototype.Style), member);
                Group(byOutput, Product(member.Placement.TileType, member.Placement.Style), member);
                if (member.Placement.Platform || member.Placement.PlanterBox) singleCellKinds.Add(member.Placement.TileType);
            }
            foreach (var pair in byProduct) products.Add(pair.Key, pair.Value.ToArray());
            foreach (var pair in byOutput) outputs.Add(pair.Key, pair.Value.ToArray());
            foreach (var member in all)
            {
                var set = new ChoiceSet { Source = member }; var list = new List<BlueprintPlacementDescriptor>();
                var byItem = new Dictionary<int, List<BlueprintPlacementDescriptor>>();
                foreach (var candidate in all) if (member.Compatible(candidate))
                {
                    var p = candidate.Placement;
                    list.Add(p); List<BlueprintPlacementDescriptor> item;
                    if (!byItem.TryGetValue(p.UseItemType, out item)) byItem.Add(p.UseItemType, item = new List<BlueprintPlacementDescriptor>());
                    item.Add(p);
                }
                set.All = list.AsReadOnly();
                foreach (var pair in byItem) set.ByItem.Add(pair.Key, pair.Value.AsReadOnly());
                choices[member.Placement] = set;
                MaterialSet old;
                if (!materials.TryGetValue(member.Placement.ConsumeItemType, out old))
                {
                    var items = new List<int>(byItem.Keys); items.Sort();
                    materials.Add(member.Placement.ConsumeItemType, new MaterialSet { Category = member.Category, Items = items.AsReadOnly() });
                }
                else
                {
                    // One direct item's alternates can have intersecting pools.
                    // Billing uses their union; execution still checks a single
                    // coordinate's actual structure and native eligibility.
                    var items = new SortedSet<int>(old.Items); foreach (int item in byItem.Keys) items.Add(item);
                    old.Items = new List<int>(items).AsReadOnly();
                }
            }
        }
        public long Revision { get; }
        public bool Ready { get; }
        public IReadOnlyList<BlueprintSubstitutionMember> Members { get { return members; } }
        private static long Product(int tile, int style) { return (long)tile << 32 | (uint)style; }
        private static void Group(Dictionary<long, List<BlueprintSubstitutionMember>> into, long key, BlueprintSubstitutionMember member)
        { List<BlueprintSubstitutionMember> group; if (!into.TryGetValue(key, out group)) into.Add(key, group = new List<BlueprintSubstitutionMember>()); group.Add(member); }
        private ChoiceSet Choices(BlueprintPlacementDescriptor target)
        {
            if (target == null) return null;
            ChoiceSet set; if (choices.TryGetValue(target, out set)) return set;
            // Effective native torch output keeps the exact held-item owner
            // while borrowing another admitted output's frame/geometry facts.
            if (target.Kind == BlueprintPlacementKind.Wall)
            { BlueprintSubstitutionMember wall; return walls.TryGetValue(target.WallType, out wall) ? choices[wall.Placement] : null; }
            BlueprintSubstitutionMember[] values;
            if (outputs.TryGetValue(Product(target.TileType, target.Style), out values))
                foreach (var member in values) if (SameOutput(member.Placement, target)) return choices[member.Placement];
            return null;
        }
        private static bool SameOutput(BlueprintPlacementDescriptor a, BlueprintPlacementDescriptor b)
        { return a.Kind == b.Kind && (a.Kind == BlueprintPlacementKind.Wall ? a.WallType == b.WallType :
            a.TileType == b.TileType && a.Style == b.Style && a.Width == b.Width && a.Height == b.Height && a.Direction == b.Direction && a.Alternate == b.Alternate &&
            a.Platform == b.Platform && a.PlanterBox == b.PlanterBox && a.RequiresObject == b.RequiresObject); }
        public IReadOnlyList<BlueprintPlacementDescriptor> Candidates(BlueprintPlacementDescriptor target, BlueprintSubstitutionPolicy policy)
        { var set = Choices(target); return set != null && policy.Allows(set.Source.Category) ? set.All : Array.Empty<BlueprintPlacementDescriptor>(); }
        public IReadOnlyList<BlueprintPlacementDescriptor> CandidatesForItem(BlueprintPlacementDescriptor target, int useItemType, BlueprintSubstitutionPolicy policy)
        {
            var set = Choices(target); IReadOnlyList<BlueprintPlacementDescriptor> result;
            return set != null && policy.Allows(set.Source.Category) && set.ByItem.TryGetValue(useItemType, out result) ? result : Array.Empty<BlueprintPlacementDescriptor>();
        }
        public IReadOnlyList<int> MaterialCandidates(int originalItemType, BlueprintSubstitutionPolicy policy)
        { MaterialSet set; return materials.TryGetValue(originalItemType, out set) && policy.Allows(set.Category) ? set.Items : Array.Empty<int>(); }
        public bool AllowsWall(int target, int actual, BlueprintSubstitutionPolicy policy)
        {
            BlueprintSubstitutionMember a, b;
            return policy.Allows(BlueprintSubstitutionCategory.Wall) && walls.TryGetValue(target, out a) && walls.TryGetValue(actual, out b) && a.Compatible(b);
        }
        public bool AllowsTile(CaptureCell target, CaptureCell actual, BlueprintSubstitutionPolicy policy)
        {
            BlueprintSubstitutionMember[] a, b;
            if (target.UnreliableObject || actual.UnreliableObject || !singleCellKinds.Contains(target.TileType) || !singleCellKinds.Contains(actual.TileType) ||
                !outputs.TryGetValue(Product(target.TileType, target.FrameY / 18), out a) || !outputs.TryGetValue(Product(actual.TileType, actual.FrameY / 18), out b)) return false;
            foreach (var source in a) if (!source.Placement.RequiresObject && policy.Allows(source.Category) && source.Matches(target) && ValidSingleCell(source.Placement, target))
                foreach (var observed in b) if (source.Compatible(observed) && observed.Matches(actual) && ValidSingleCell(observed.Placement, actual)) return true;
            return false;
        }
        private static bool ValidSingleCell(BlueprintPlacementDescriptor descriptor, CaptureCell cell)
        { return cell.FrameX >= 0 && cell.FrameX % 18 == 0 && cell.FrameY >= 0 && cell.FrameY % 18 == 0 &&
            (descriptor.Platform ? cell.FrameX < 27 * 18 : descriptor.PlanterBox && cell.FrameX <= 54 && !cell.HalfBrick && cell.Slope == 0); }
        public bool AllowsObject(BlueprintWorldObject target, BlueprintWorldObject actual, BlueprintSubstitutionPolicy policy)
        {
            if (target == null || actual == null || target.Family != actual.Family || target.AnchorX != actual.AnchorX || target.AnchorY != actual.AnchorY ||
                target.Direction != actual.Direction) return false;
            if (target.Family != BlueprintWorldObjectFamily.Door && (target.WorldX != actual.WorldX || target.WorldY != actual.WorldY || target.Width != actual.Width || target.Height != actual.Height)) return false;
            BlueprintSubstitutionMember[] a, b;
            if (!products.TryGetValue(Product(target.Kind, target.Style), out a) || !products.TryGetValue(Product(actual.Kind, actual.Style), out b)) return false;
            foreach (var source in a) if (policy.Allows(source.Category) && source.Matches(target))
                foreach (var observed in b) if (source.Compatible(observed) && observed.Matches(actual)) return true;
            return false;
        }
        public bool AcceptsObject(BlueprintWorldObject target, BlueprintWorldObject actual, BlueprintSubstitutionPolicy policy)
        { return target != null && (target.SamePlacement(actual) || AllowsObject(target, actual, policy)); }

        public bool TryEffectiveTile(BlueprintPlacementDescriptor source, int tile, int style, BlueprintSubstitutionPolicy policy,
            out BlueprintPlacementDescriptor effective)
        {
            effective = null; var set = Choices(source);
            if (set == null || !policy.Allows(set.Source.Category)) return false;
            foreach (var candidate in set.All) if (candidate.TileType == tile && candidate.Style == style && candidate.Direction == source.Direction)
            {
                effective = new BlueprintPlacementDescriptor(candidate.Kind, source.UseItemType, source.ConsumeItemType, source.Units,
                    candidate.TileType, candidate.Style, candidate.WallType, width: candidate.Width, height: candidate.Height,
                    originX: candidate.OriginX, originY: candidate.OriginY, alternate: candidate.Alternate, direction: candidate.Direction,
                    requiresObject: candidate.RequiresObject, platform: candidate.Platform, planterBox: candidate.PlanterBox);
                return true;
            }
            return false;
        }

        // Only private execution values change. The Host must decode this raw
        // grid again, check native CanPlace/choice and bind the concrete source.
        public bool TryCreateExecution(CaptureCell original, CaptureObject? originalObject, BlueprintPlacementDescriptor candidate,
            BlueprintSubstitutionPolicy policy, out CaptureCell execution, out CaptureObject? executionObject)
        {
            execution = original; executionObject = originalObject;
            if (candidate == null) return false;
            if (candidate.Kind == BlueprintPlacementKind.Wall)
            {
                if (candidate.WallType != original.WallType && !AllowsWall(original.WallType, candidate.WallType, policy)) return false;
                execution.WallType = checked((ushort)candidate.WallType); return true;
            }
            if (candidate.PlanterBox && (originalObject.HasValue || original.ObjectId > 0)) return false;
            var output = Choices(candidate)?.Source; BlueprintSubstitutionMember[] sources;
            int sourceKind = original.TileType == 11 ? 10 : original.TileType;
            int sourceStyle = originalObject.HasValue ? originalObject.Value.Style : original.FrameY / 18;
            if (output == null || !policy.Allows(output.Category) || !products.TryGetValue(Product(sourceKind, sourceStyle), out sources)) return false;
            foreach (var source in sources)
            {
                if (!source.Matches(original, originalObject) || !source.Compatible(output)) continue;
                var prototype = output.Prototype;
                int column = 0, row = 0;
                if (originalObject.HasValue)
                {
                    var obj = originalObject.Value;
                    if (!obj.Reliable) return false;
                    prototype.Id = obj.Id; prototype.X = obj.X; prototype.Y = obj.Y;
                    if (source.Rule.Door)
                    {
                        if (obj.Type == 11 && original.FrameX % 72 >= 36) prototype.X++;
                        execution.X = prototype.X; execution.Y = prototype.Y;
                    }
                    else { column = original.X - obj.X; row = original.Y - obj.Y; }
                    executionObject = prototype;
                }
                else if (candidate.RequiresObject && !candidate.Platform) return false;
                short x, y;
                if (!output.Rule.TryMemberFrame(column, row, out x, out y)) return false;
                execution.TileType = checked((ushort)candidate.TileType); execution.FrameX = x; execution.FrameY = y;
                return true;
            }
            return false;
        }
    }
}
