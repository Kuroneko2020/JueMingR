using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private readonly Dictionary<int, ulong> constructionSlots = new Dictionary<int, ulong>();
        private BlueprintMaterialInventory constructionInventory;
        private Player constructionPlayer;

        // The existing inventory publication owns discovery. Reuse its finite
        // 50-slot snapshot once, then validate only real candidate identities;
        // never run a 50-slot tool search for every blueprint member.
        private int ConstructionItemSlot(Player p, int type)
        {
            if (p == null || type <= 0 || MaterialInventory?.Ready != true) return -1;
            if (!ReferenceEquals(constructionInventory, MaterialInventory) || !ReferenceEquals(constructionPlayer, p))
            {
                constructionInventory = MaterialInventory; constructionPlayer = p; constructionSlots.Clear();
                foreach (var slot in constructionInventory.Slots)
                {
                    if (slot.Slot < 0 || slot.Slot >= 50 || !slot.Eligible || slot.Stack <= 0 || slot.ItemType <= 0) continue;
                    ulong mask; constructionSlots.TryGetValue(slot.ItemType, out mask);
                    constructionSlots[slot.ItemType] = mask | 1UL << slot.Slot;
                }
            }
            var hint = PreferredBuildSourceHint(p);
            if (hint != null && hint.Type == type && hint.Slot >= 0 && hint.Slot < 50 &&
                ReferenceEquals(p.inventory[hint.Slot], hint.Item) && buildTools.BuildInventoryEligible(hint.Slot, hint.Item)) return hint.Slot;
            ulong candidates;
            if (!constructionSlots.TryGetValue(type, out candidates)) return -1;
            for (int slot = 0; slot < 50 && candidates != 0; slot++)
            {
                ulong bit = 1UL << slot; if ((candidates & bit) == 0) continue; candidates &= ~bit;
                var item = p.inventory[slot];
                if (item != null && item.type == type && item.stack > 0 && buildTools.BuildInventoryEligible(slot, item)) return slot;
            }
            return -1;
        }

        private bool TryPlanSpecialBuild(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, out BlueprintBuildOperation operation)
        {
            operation = null;
            var placement = target.Placement;
            if (!actual.Available || placement == null || placement.Kind != BlueprintPlacementKind.Tile) return false;
            BlueprintSubstrateRecipe recipe;
            if (placement.RequiresSubstrate)
            {
                if (MaterialCatalog.Substrate(target.Cell) == null || !BlueprintSubstrateRecipe.TryGet(target.Cell.TileType, out recipe)) return false;
                bool present = (actual.Cell.Layers & CaptureLayer.Tile) != 0;
                if (present && (actual.Cell.TileType != recipe.BaseTile || actual.Cell.Inactive || actual.ObjectUnknown)) return false;
                operation = new BlueprintBuildOperation {
                    Kind = present ? BlueprintBuildOperationKind.ConvertSubstrate : BlueprintBuildOperationKind.PlaceBase,
                    Consumer = ToolBuildConsumer.Tiles, HeldMaximum = 1, PrimaryMaximum = 0,
                    NativePlacement = present ? new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, recipe.ConversionItem,
                        recipe.ConversionItem, 1, recipe.TargetTile, 0, requiresSubstrate: true) :
                        new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, recipe.BaseItem, recipe.BaseItem, 1, recipe.BaseTile, 0)
                };
                return true;
            }
            if (placement.Producer == null || (actual.Cell.Layers & CaptureLayer.Tile) != 0) return false;
            operation = SpecialOperation(placement); return true;
        }
        private static BlueprintBuildOperation SpecialOperation(BlueprintPlacementDescriptor placement)
        {
            var producer = placement.Producer;
            return new BlueprintBuildOperation {
                Kind = BlueprintBuildOperationKind.Body, Consumer = ToolBuildConsumer.Tiles, NativePlacement = placement,
                SupplyType = producer.BucketItemType, BucketType = producer.BucketItemType,
                SupplyDestination = producer.Kind == BlueprintSpecialProducerKind.Flexible ? 0 : -1,
                HeldMaximum = producer.ConsumesHeld ? 1 : 0, PrimaryMaximum = producer.ConsumesBucket ? 1 : 0
            };
        }
        private bool TryPrepareSpecialBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight, BuildOperationPlan selectedPlan)
        {
            flight = null; var operation = target.Operation;
            if (operation == null) return false;
            if (operation.Kind == BlueprintBuildOperationKind.PlaceBase || operation.Kind == BlueprintBuildOperationKind.ConvertSubstrate)
            {
                int slot = selectedPlan == null ? ConstructionItemSlot(p, operation.NativePlacement.UseItemType) : PlannedBuildToolSlot(p, selectedPlan);
                if (slot < 0) { WaitForBuild(target, BlueprintBuildWait.Material); return false; }
                return TryCreateOperationFlight(target, p, slot, -1, 1, 0, out flight);
            }
            BuildOperationPlan plan = selectedPlan; BlueprintBuildWait wait; ulong dependencySlots;
            if (plan == null && !TryPlanBuild(target, p, PreferredBuildSourceHint(p), out plan, out wait, out dependencySlots))
            { RegisterPlannedBuildWait(target, wait, dependencySlots); return false; }
            target.Operation = operation = CopyBuildOperation(plan.Operation);
            if (PlannedBuildToolSlot(p, plan, buildMaterialPreparation?.HasWork == true) < 0) { WaitForBuild(target, BlueprintBuildWait.Material, retryPending: true); return false; }
            int primary;
            if (!TryPrepareBuildSupply(target, p, plan.SupplyType, plan.SupplyDestination, out primary, plan)) return false;
            if (!PlannedBuildSupplyCurrent(p, plan, primary)) { WaitForDynamicBuild(target); return false; }
            int tool = PlannedBuildToolSlot(p, plan, true);
            if (tool < 0) { WaitForBuild(target, BlueprintBuildWait.Material, retryPending: true); return false; }
            var placement = operation.NativePlacement;
            if (target.Body != null)
            { target.TargetX = target.Bounds.Left + placement.OriginX; target.TargetY = target.Bounds.Top + placement.OriginY; }
            return TryCreateOperationFlight(target, p, tool, primary, operation.HeldMaximum, operation.PrimaryMaximum, out flight);
        }
        private bool SpecialBuildCurrent(BuildFlight flight)
        {
            var target = flight.Target; var operation = flight.Operation; var p = flight.Player; var item = flight.Source.Item;
            var placement = operation?.NativePlacement;
            if (placement == null || item.type != placement.UseItemType ||
                !p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, item.tileBoost + p.blockRange)) return false;
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual)) return false;
            if (operation.Kind == BlueprintBuildOperationKind.ConvertSubstrate)
            {
                BlueprintSubstrateRecipe recipe;
                return BlueprintSubstrateRecipe.TryGet(target.Cell.TileType, out recipe) && item.type == recipe.ConversionItem &&
                    (actual.Cell.Layers & CaptureLayer.Tile) != 0 && actual.Cell.TileType == recipe.BaseTile && !actual.Cell.Inactive && !actual.ObjectUnknown;
            }
            if ((actual.Cell.Layers & CaptureLayer.Tile) != 0) return false;
            var producer = placement.Producer;
            if (producer != null)
            {
                Item supply = SpecialPrimary(flight);
                if (supply == null || supply.type != producer.BucketItemType || supply.stack <= 0) return false;
                if (producer.Kind == BlueprintSpecialProducerKind.TileWand)
                {
                    if (item.tileWand != producer.BucketItemType || !ReferenceEquals(FirstSpecialMaterial(p, item.tileWand), supply)) return false;
                }
                else
                {
                    var wand = item.GetFlexibleTileWand();
                    Terraria.GameContent.FlexibleTileWand.PlacementOption option; Item selected;
                    if (wand == null || !wand.TryGetPlacementOption(p, Player.FlexibleWandRandomSeed, operation.Cycle, out option, out selected) ||
                        !ReferenceEquals(selected, supply) || option.TileIdToPlace != placement.TileType || option.TileStyleToPlace != placement.Style) return false;
                    bool prepared = specialSelection?.Token == flight.Token && specialSelection.Prepared && specialSelection.PreparedCycle == operation.Cycle;
                    flight.InputGesture = prepared || Player.FlexibleWandCycleOffset == operation.Cycle ? ToolInputGesture.HeldPress :
                        Player.FlexibleWandCycleOffset < operation.Cycle ? ToolInputGesture.SelectDown : ToolInputGesture.SelectUp;
                    // Legacy random style does not consume forcedRandom. It is
                    // not an exact selectable variant and must never be tried
                    // speculatively against a captured frame.
                    if (!TileObjectData.CustomPlace(placement.TileType, placement.Style)) return false;
                }
            }
            if (placement.RequiresObject || target.Body != null)
                return PreflightSpecialObject(flight);
            if (item.createTile != placement.TileType || !PreflightBuildOrdinary(target, item)) return false;
            return BuildNeighbor(target.WorldX, target.WorldY, false) || BuildNeighbor(target.WorldX - 1, target.WorldY, false) ||
                BuildNeighbor(target.WorldX + 1, target.WorldY, false) || BuildNeighbor(target.WorldX, target.WorldY - 1, false) ||
                BuildNeighbor(target.WorldX, target.WorldY + 1, false);
        }
        private static Item FirstSpecialMaterial(Player p, int type)
        { for (int i = 0; i < 58; i++) if (p.inventory[i].type == type && p.inventory[i].stack > 0) return p.inventory[i]; return null; }
        private static Item SpecialPrimary(BuildFlight flight)
        {
            foreach (var dependency in flight.Dependencies) if (dependency.Primary) return dependency.Item;
            return flight.Source.Type == flight.Operation.BucketType ? flight.Source.Item : null;
        }
        private bool PreflightSpecialObject(BuildFlight flight)
        {
            var target = flight.Target; var placement = flight.Operation.NativePlacement;
            var root = TileObjectData.GetTileData(placement.TileType, placement.Style);
            if (root == null) return false;
            int left = target.Bounds.Left, right = target.Bounds.Right, top = target.Bounds.Top, bottom = target.Bounds.Bottom;
            for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
            {
                var data = TileObjectData.GetTileData(placement.TileType, placement.Style, alternate);
                if (data == null) return false;
                int x = target.WorldX - data.Origin.X, y = target.WorldY - data.Origin.Y;
                left = Math.Min(left, x - 1); right = Math.Max(right, x + data.Width + 1);
                top = Math.Min(top, y - 1); bottom = Math.Max(bottom, y + data.Height + 1);
                left = Math.Min(left, x + Math.Min(data.AnchorTop.checkStart, data.AnchorBottom.checkStart));
                right = Math.Max(right, x + Math.Max(data.AnchorTop.checkStart + data.AnchorTop.tileCount, data.AnchorBottom.checkStart + data.AnchorBottom.tileCount));
                top = Math.Min(top, y + Math.Min(data.AnchorLeft.checkStart, data.AnchorRight.checkStart) - 1);
                bottom = Math.Max(bottom, y + Math.Max(data.AnchorLeft.checkStart + data.AnchorLeft.tileCount, data.AnchorRight.checkStart + data.AnchorRight.tileCount) + 1);
                var hook = data.HookCheckIfCanPlace.hook;
                if (hook != null && (hook.Method.DeclaringType != typeof(WorldGen) ||
                    hook.Method.Name != "PlantCheck_CanPlaceHook" && hook.Method.Name != "CanPlaceProjectilePressurePad")) return false;
            }
            for (int x = left; x < right; x++) for (int y = top; y < bottom; y++)
            {
                BlueprintWorldMatchSample sample;
                if (!ReadBuildCell(x, y, out sample)) return false;
                if (x >= target.Bounds.Left && x < target.Bounds.Right && y >= target.Bounds.Top && y < target.Bounds.Bottom &&
                    (sample.Cell.Layers & CaptureLayer.Tile) != 0) return false;
            }
            var raw = Main.tile[target.WorldX, target.WorldY];
            if (raw.liquid > 0 && raw.lava() && (Main.tileSolid[placement.TileType] ||
                !TileObjectData.CheckLiquidPlacement(placement.TileType, placement.Style, raw))) return false;
            TileObject choice;
            int? cycle = placement.Producer?.Kind == BlueprintSpecialProducerKind.Flexible ? flight.Operation.Cycle : (int?)null;
            return BlueprintPlacementQuery.CanPlace(target.WorldX, target.WorldY, placement.TileType, placement.Style, flight.Direction, out choice, false, cycle) &&
                SpecialChoiceMatches(target, choice) && BlueprintPlacementQuery.PostConditions(choice, target.WorldX, target.WorldY) && !BuildObjectPlayerBlocked(choice);
        }
        private static bool SpecialChoiceMatches(BlueprintBuildTarget target, TileObject choice)
        {
            var data = TileObjectData.GetTileData(choice.type, choice.style, choice.alternate);
            if (data == null || choice.xCoord != target.Bounds.Left || choice.yCoord != target.Bounds.Top ||
                data.Width != target.Bounds.Width || data.Height != target.Bounds.Height) return false;
            int encoded = data.CalculatePlacementStyle(choice.style, choice.alternate, choice.random), cross = 0;
            if (data.StyleWrapLimit > 0) { cross = encoded / data.StyleWrapLimit * data.StyleLineSkip; encoded %= data.StyleWrapLimit; }
            var cell = new CaptureCell { X = choice.xCoord, Y = choice.yCoord, Layers = CaptureLayer.Tile, TileType = (ushort)choice.type,
                FrameX = checked((short)((data.StyleHorizontal ? encoded : cross) * data.CoordinateFullWidth)),
                FrameY = checked((short)((data.StyleHorizontal ? cross : encoded) * data.CoordinateFullHeight)) };
            var shape = BlueprintCaptureSource.DescribeRead(cell, BlueprintWorldObjectSemantics.TileValue(cell)); BlueprintWorldObject actual;
            return target.Body != null ? BlueprintWorldObjectSemantics.TryCreate(cell, shape, out actual) && actual != null && target.Body.SamePlacement(actual) :
                target.Cell.TileType == cell.TileType && target.Cell.FrameX == cell.FrameX && target.Cell.FrameY == cell.FrameY;
        }
        private bool NativeSpecialConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material = null, bool wall = false, bool applyItemAnimation = true)
        {
            if (consumer != ToolBuildConsumer.Tiles || wall || !SpecialBuildCurrent(flight) || !flight.Player.ItemTimeIsZero ||
                flight.Player.itemAnimation <= 0 || !flight.Player.controlUseItem) return false;
            return flight.Operation.NativePlacement.Producer?.Kind != BlueprintSpecialProducerKind.Flexible ||
                Player.FlexibleWandCycleOffset == flight.Operation.Cycle && specialSelection?.NeutralRequired != true && specialSelection?.DownPending != true;
        }
        private bool NativeSpecialBuildChoice(BuildFlight flight, bool objectType, TileObject placement, int type)
        {
            var desired = flight.Operation.NativePlacement;
            if (desired == null || type != desired.TileType) return false;
            bool allowed = objectType ? SpecialChoiceMatches(flight.Target, placement) :
                flight.Operation.Kind == BlueprintBuildOperationKind.PlaceBase || flight.Operation.Kind == BlueprintBuildOperationKind.ConvertSubstrate ||
                flight.Target.Body == null && !Main.tileFrameImportant[type];
            if (allowed) flight.Chosen = true;
            return allowed;
        }
        private bool SpecialBuildApplied(BuildFlight flight)
        {
            var target = flight.Target; var placement = flight.Operation.NativePlacement;
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.Bounds.Left, target.Bounds.Top, out actual) || (actual.Cell.Layers & CaptureLayer.Tile) == 0 || actual.Cell.TileType != placement.TileType) return false;
            if (flight.Operation.Kind == BlueprintBuildOperationKind.PlaceBase || flight.Operation.Kind == BlueprintBuildOperationKind.ConvertSubstrate) return true;
            if (target.Body == null) return BlueprintMaterialSteps.SameBody(target.Cell, actual, MaterialCatalog) == BlueprintBodyState.Same;
            for (int x = target.Bounds.Left; x < target.Bounds.Right; x++) for (int y = target.Bounds.Top; y < target.Bounds.Bottom; y++)
                if (!ReadBuildCell(x, y, out actual) || actual.ObjectUnknown || actual.Object == null || !target.Body.SamePlacement(actual.Object)) return false;
            return true;
        }
        private Action<Exception> BeginSpecialBuildConsumer(BuildFlight flight, ToolBuildConsumer consumer)
        {
            if (consumer != ToolBuildConsumer.Tiles || flight?.Operation?.NativePlacement?.Producer?.Kind != BlueprintSpecialProducerKind.Flexible) return null;
            SpecialBuildReadiness(flight);
            var receipt = specialSelection;
            if (receipt == null || !receipt.Prepared || receipt.PreparedCycle != flight.Operation.Cycle || receipt.Token != flight.Token || receipt.Session != generation ||
                receipt.Invalid || buildTools.SelectionIntent != receipt.Selection || Player.FlexibleWandCycleOffset != receipt.OwnedCycle) return null;
            int seed = Player.FlexibleWandRandomSeed; Point position = Player.FlexibleWandLastPosition;
            int ownedCycle = receipt.PreparedCycle;
            // The preparation proof was earned by native +/-1 inputs and a
            // real neutral epoch. Reusing this one prepared mode does not earn
            // any other variant, and the original selector still runs inside.
            Player.FlexibleWandCycleOffset = ownedCycle; receipt.OwnedCycle = ownedCycle;
            bool ended = false;
            return error => {
                if (ended) return; ended = true;
                // The original consumer owns any RNG advancement. Return only
                // its temporary preview fields; never reset Main.rand or undo
                // a player's later variant selection.
                if (receipt.Session == generation && buildTools.SelectionIntent == receipt.Selection && Player.FlexibleWandCycleOffset == ownedCycle)
                {
                    Player.FlexibleWandCycleOffset = receipt.OriginalCycle; receipt.OwnedCycle = receipt.OriginalCycle;
                    if (Player.FlexibleWandLastPosition == new Point(flight.Target.WorldX, flight.Target.WorldY))
                    { Player.FlexibleWandRandomSeed = seed; Player.FlexibleWandLastPosition = position; }
                }
            };
        }
    }
}
