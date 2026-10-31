using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Blueprint;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed partial class NativeItemCatalog
    {
        private readonly List<BlueprintSubstitutionMember> substitutionMembers = new List<BlueprintSubstitutionMember>();
        private readonly HashSet<long> substitutionExcludedOutputs = new HashSet<long>();
        private readonly HashSet<int> substitutionExclusions = new HashSet<int>();
        private void ClearBlueprintSubstitution()
        { substitutionMembers.Clear(); substitutionExcludedOutputs.Clear(); substitutionExclusions.Clear(); }
        private static long SubstitutionOutput(int tile, int style) { return (long)tile << 32 | (uint)style; }
        private void CaptureBlueprintSubstitutionExclusion(Item sample)
        {
            if (sample.type != ItemID.DynastyTable && sample.type != ItemID.CloudPlatform && sample.type != ItemID.AetheriumPlatform) return;
            if (sample.createTile < 0 || sample.placeStyle < 0) return;
            substitutionExclusions.Add(sample.type);
            substitutionExcludedOutputs.Add(SubstitutionOutput(sample.createTile, sample.placeStyle));
        }
        private bool BlueprintSubstitutionMetadataReady
        {
            get
            {
                // Main allocates this array before Initialize sets its values.
                // These locked-client sentinels distinguish that early array
                // from initialized safety facts; no initializer is invoked here.
                return typeof(Main).Assembly.GetName().Version == new Version(1, 4, 5, 8) && substitutionExclusions.Count == 3 &&
                    Main.wallHouse != null && Main.wallHouse.Length >= WallID.Count && Main.wallHouse[WallID.Stone] && Main.wallHouse[WallID.Wood] && !Main.wallHouse[WallID.DirtUnsafe];
            }
        }
        private BlueprintSubstitutionCatalog CompileBlueprintSubstitution(long revision, bool version)
        {
            bool ready = version && BlueprintSubstitutionMetadataReady;
            var members = new List<BlueprintSubstitutionMember>();
            if (ready)
            {
                foreach (var member in substitutionMembers)
                    if (!substitutionExcludedOutputs.Contains(SubstitutionOutput(member.Placement.TileType, member.Placement.Style))) members.Add(member);
                // Initialized native safety and a reliable direct producer are
                // the wall membership gates, including safe glass and fences.
                // Unsafe walls remain in materialWalls for exact construction.
                foreach (var pair in materialWalls)
                    if (pair.Value != null && pair.Key > 0 && pair.Key < Main.wallHouse.Length && Main.wallHouse[pair.Key])
                        members.Add(new BlueprintSubstitutionMember(BlueprintSubstitutionCategory.Wall,
                            new BlueprintPlacementRule(pair.Value, true), Array.Empty<int>(), safeWall: true));
            }
            return new BlueprintSubstitutionCatalog(revision, members, ready);
        }
        private void CaptureBlueprintSubstitutionRule(Item sample, BlueprintPlacementRule rule)
        {
            var p = rule.Descriptor;
            if (sample.createTile != p.TileType || sample.placeStyle != p.Style || !sample.consumable ||
                p.UseItemType != p.ConsumeItemType || p.Units != 1 || p.Producer != null || p.Flexible || p.RequiresSubstrate) return;
            BlueprintSubstitutionCategory category;
            if (!TryBlueprintSubstitutionCategory(sample, p, out category)) return;
            if (p.PlanterBox)
            {
                // This is a frozen legacy-cell declaration, not a synthesized
                // native metadata object. Execution keeps Object=null.
                var prototype = new CaptureObject { Type = (ushort)p.TileType, Width = 1, Height = 1,
                    Style = p.Style, PlacementStyle = p.Style, Reliable = true };
                substitutionMembers.Add(new BlueprintSubstitutionMember(category, rule, new[] { 1, 1 }, prototype));
                return;
            }
            short x, y; if (!rule.TryMemberFrame(0, 0, out x, out y)) return;
            var cell = new CaptureCell { Layers = CaptureLayer.Tile, TileType = (ushort)p.TileType, FrameX = x, FrameY = y, ObjectId = 1 };
            try
            {
                var shape = BlueprintCaptureSource.DescribeRead(cell, BlueprintWorldObjectSemantics.TileValue(cell));
                if (shape == null || !shape.Object.Reliable) return;
                var prototype = shape.Object; prototype.Id = 1;
                if (prototype.Width != p.Width || prototype.Height != p.Height) return;
                var data = BlueprintHorizontalMirrorResolver.NativeData(cell);
                if (data == null || data.GetStyleOverride != null) return;
                var physical = rule.Door ? new[] { 1, 3 } : BlueprintSubstitutionStructure(data);
                substitutionMembers.Add(new BlueprintSubstitutionMember(category, rule, physical, prototype));
            }
            catch (ArgumentException) { }
            catch (IndexOutOfRangeException) { }
            catch (OverflowException) { }
        }
        private static bool TryBlueprintSubstitutionCategory(Item sample, BlueprintPlacementDescriptor p, out BlueprintSubstitutionCategory category)
        {
            category = default(BlueprintSubstitutionCategory);
            int width = 0, height = 0;
            switch (p.TileType)
            {
                case TileID.WorkBenches: category = BlueprintSubstitutionCategory.Workbench; width = 2; height = 1; break;
                case TileID.Tables: case TileID.Tables2: category = BlueprintSubstitutionCategory.Table; width = 3; height = 2; break;
                case TileID.PicnicTable: category = BlueprintSubstitutionCategory.Table; width = 4; height = 2; break;
                case TileID.Chairs: category = BlueprintSubstitutionCategory.Chair; width = 1; height = 2; break;
                case TileID.ClosedDoor: category = BlueprintSubstitutionCategory.Door; width = 1; height = 3; break;
                case TileID.Beds: category = BlueprintSubstitutionCategory.Bed; width = 4; height = 2; break;
                case TileID.PlanterBox: category = BlueprintSubstitutionCategory.PlanterBox; width = height = 1; break;
                case TileID.Anvils:
                    if (sample.type != ItemID.IronAnvil && sample.type != ItemID.LeadAnvil) return false;
                    category = BlueprintSubstitutionCategory.Anvil; width = 2; height = 1; break;
                case TileID.MythrilAnvil:
                    if (sample.type != ItemID.MythrilAnvil && sample.type != ItemID.OrichalcumAnvil) return false;
                    category = BlueprintSubstitutionCategory.MythrilAnvil; width = 2; height = 1; break;
                case TileID.Containers: case TileID.Containers2:
                    category = BlueprintSubstitutionCategory.Chest; width = height = 2; break;
                case TileID.Torches: category = BlueprintSubstitutionCategory.Torch; width = height = 1; break;
                case TileID.Platforms: category = BlueprintSubstitutionCategory.Platform; width = height = 1; break;
                default: return false;
            }
            // Real direct item output plus full metadata shape define a member,
            // rather than every housing table/chair/container flag. In .8 the
            // Dead Man's Chest is a Containers2 producer and is intentionally
            // included; Safes/FakeContainers never enter. Picnic tables share
            // the Table category but keep their own compatible 4x2 structure.
            return p.Width == width && p.Height == height;
        }
        private static int[] BlueprintSubstitutionStructure(TileObjectData data)
        {
            // Texture row heights, padding, draw offsets, style numbers and
            // alternate indices encode art. Physical attachment and click
            // origin remain, while per-position liquid legality is native.
            var value = new List<int> { data.Width, data.Height, data.Origin.X, data.Origin.Y, (int)data.Direction, data.AnchorWall ? 1 : 0 };
            AddBlueprintSubstitutionAnchor(value, data.AnchorTop); AddBlueprintSubstitutionAnchor(value, data.AnchorBottom);
            AddBlueprintSubstitutionAnchor(value, data.AnchorLeft); AddBlueprintSubstitutionAnchor(value, data.AnchorRight);
            AddBlueprintSubstitutionTiles(value, data.AnchorValidTiles); AddBlueprintSubstitutionTiles(value, data.AnchorInvalidTiles);
            AddBlueprintSubstitutionTiles(value, data.AnchorAlternateTiles);
            return value.ToArray();
        }
        private static void AddBlueprintSubstitutionAnchor(List<int> into, AnchorData anchor)
        { into.Add((int)anchor.type); into.Add(anchor.tileCount); into.Add(anchor.tileCount == 0 ? 0 : anchor.checkStart); }
        private static void AddBlueprintSubstitutionTiles(List<int> into, int[] tiles)
        {
            into.Add(tiles?.Length ?? 0); if (tiles == null) return;
            var sorted = (int[])tiles.Clone(); Array.Sort(sorted); into.AddRange(sorted);
        }
    }
}
