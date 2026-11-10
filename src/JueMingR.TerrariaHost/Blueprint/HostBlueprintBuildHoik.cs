using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class OwnedHoikBase
        {
            internal BlueprintProjectionPlacement Owner;
            internal BlueprintProjectionIdentity Identity;
            internal int OriginX, OriginY, X, Y;
            internal bool Mirrored;
            internal CaptureCell Expected;
            internal Tile Tile;
        }
        // Only actually started cells are retained. This is provenance for a
        // real intermediate stone, never a second final-completion table.
        private readonly Dictionary<long, OwnedHoikBase> hoikBases = new Dictionary<long, OwnedHoikBase>();
        private BlueprintMaterialInventory hoikInventory;
        private Item hoikPick;
        private int hoikPickSlot = -1;
        private bool HoikPickCurrent(Player p)
        {
            if (!ReferenceEquals(hoikInventory, MaterialInventory))
            {
                hoikInventory = MaterialInventory; hoikPick = null; hoikPickSlot = -1;
                // Match vanilla's best-pick selector once per already published
                // inventory, rather than scanning fifty slots per target.
                if (hoikInventory != null) foreach (var source in hoikInventory.Slots)
                {
                    int slot = source.Slot; if (slot < 0 || slot >= 50) continue;
                    var item = p.inventory[slot];
                    if (item != null && item.stack > 0 && item.pick > 0 && (hoikPick == null || item.pick > hoikPick.pick))
                    { hoikPick = item; hoikPickSlot = slot; }
                }
            }
            return hoikPickSlot >= 0 && ReferenceEquals(p.inventory[hoikPickSlot], hoikPick) && hoikPick.stack > 0 &&
                buildTools.BuildInventoryEligible(hoikPickSlot, hoikPick);
        }
        private static bool HoikOperation(BlueprintBuildOperation op) { return op?.Hoik == true; }
        private bool HoikTarget(CaptureCell cell)
        { return (cell.Layers & CaptureLayer.Tile) != 0 && cell.Slope >= 3 && cell.Slope <= 4 && !cell.HalfBrick && MaterialCatalog?.TileShape(cell)?.Platform == true; }
        private static bool SameHoikForeground(CaptureCell a, CaptureCell b)
        { return (a.Layers & CaptureLayer.Tile) == (b.Layers & CaptureLayer.Tile) && a.TileType == b.TileType && a.Slope == b.Slope && a.HalfBrick == b.HalfBrick && a.Inactive == b.Inactive && a.TilePaint == b.TilePaint && a.InvisibleTile == b.InvisibleTile && a.FullbrightTile == b.FullbrightTile; }
        private bool HoikFinalBody(BlueprintBuildTarget target, BlueprintWorldMatchSample actual)
        { return !actual.ObjectUnknown && (target.Body != null ? actual.Object != null && SameSubstitutionBody(target.Body, actual.Object) : BlueprintMaterialSteps.SameBody(target.Cell, actual, MaterialCatalog, policy: substitutionPolicy) == BlueprintBodyState.Same); }
        private OwnedHoikBase HoikBase(BlueprintBuildTarget target, BlueprintWorldMatchSample actual)
        {
            OwnedHoikBase value;
            if (!hoikBases.TryGetValue(WiringCoordinate(target.WorldX, target.WorldY), out value)) return null;
            var owner = target.Range.Owner;
            if (!ReferenceEquals(value.Owner, owner) || !value.Identity.Equals(owner.Identity) || value.OriginX != owner.OriginX || value.OriginY != owner.OriginY ||
                value.Mirrored != owner.MirroredHorizontally || !ReferenceEquals(value.Tile, Main.tile[target.WorldX, target.WorldY]) ||
                value.Expected.ObjectId != target.Cell.ObjectId || !SameHoikForeground(value.Expected, actual.Cell)) return null;
            return value;
        }
        private bool TryPlanHoikBuild(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, BlueprintBuildLane lane, out BlueprintBuildOperation operation)
        {
            operation = null;
            if (!HoikTarget(target.Cell) || !actual.Available || lane != BlueprintBuildLane.TileBody && lane != BlueprintBuildLane.Shape) return false;
            bool present = (actual.Cell.Layers & CaptureLayer.Tile) != 0;
            bool own = HoikBase(target, actual) != null;
            bool final = present && !own && HoikFinalBody(target, actual);
            if (final && actual.Cell.Slope == target.Cell.Slope && actual.Cell.HalfBrick == target.Cell.HalfBrick) return false;
            if (lane == BlueprintBuildLane.TileBody && final || lane == BlueprintBuildLane.Shape && !final && !own) return false;
            if (present && !own && !final) return false;
            bool hammer = own && (actual.Cell.Slope != target.Cell.Slope || actual.Cell.HalfBrick);
            bool swap = own && !hammer;
            operation = new BlueprintBuildOperation {
                Hoik = true, Kind = hammer ? BlueprintBuildOperationKind.Hammer : swap ? BlueprintBuildOperationKind.HoikSwap : BlueprintBuildOperationKind.HoikBase,
                Consumer = hammer ? ToolBuildConsumer.Hammer : ToolBuildConsumer.Tiles,
                Mode = actual.Cell.Slope | (actual.Cell.HalfBrick ? 8 : 0), HeldMaximum = hammer ? 0 : 1, PrimaryMaximum = 0,
                NativePlacement = hammer ? null : swap ? target.Placement :
                    new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, ItemID.StoneBlock, ItemID.StoneBlock, 1, TileID.Stone, 0)
            };
            return true;
        }
        private bool HoikCapabilities(BlueprintBuildTarget target, Player p, bool starting, out BlueprintBuildWait wait)
        {
            wait = BlueprintBuildWait.None; target.Operation.HoikWaitTool = 0;
            bool outside;
            var original = target.Operation;
            if (starting)
            {
                target.Operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.Hammer, Hoik = true };
                int hammer;
                try { hammer = FindAppearanceTool(target, p, out outside); }
                finally { target.Operation = original; }
                if (hammer < 0)
                { target.Operation.HoikWaitTool=1; wait=outside?BlueprintBuildWait.Player:BlueprintBuildWait.Material|BlueprintBuildWait.Capability; WaitForBuild(target,wait); return false; }
                int missing=ConstructionItemSlot(p,ItemID.StoneBlock)<0?ItemID.StoneBlock:ConstructionItemSlot(p,target.Placement.UseItemType)<0?target.Placement.UseItemType:0;
                if(missing>0)
                {
                    wait=BlueprintBuildWait.Material;WaitForBuild(target,wait);
                    if(nearWaitingFact!=null && ReferenceEquals(nearWaitingFact.Seed.Range,target.Range))nearWaitingFact.Supply=missing;
                    projectionFeedback="Hoik施工需要石块底材和目标平台，补充后会继续。";return false;
                }
            }
            if (!HoikPickCurrent(p))
            { target.Operation.HoikWaitTool=2;wait=BlueprintBuildWait.Material|BlueprintBuildWait.Capability;WaitForBuild(target,wait);return false; }
            return true;
        }
        private bool TryPrepareHoikBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight, BuildOperationPlan plan)
        {
            flight = null;
            var op = target.Operation;
            BlueprintBuildWait wait;
            if (!HoikCapabilities(target, p, op.Kind == BlueprintBuildOperationKind.HoikBase, out wait)) return false;
            bool outside = false;
            int slot = plan == null ? op.Kind == BlueprintBuildOperationKind.Hammer ? FindAppearanceTool(target, p, out outside) :
                ConstructionItemSlot(p, op.NativePlacement.UseItemType) : PlannedBuildToolSlot(p, plan);
            if (slot < 0) { WaitForBuild(target, outside ? BlueprintBuildWait.Player : BlueprintBuildWait.Material | BlueprintBuildWait.Capability); return false; }
            if (!TryCreateOperationFlight(target, p, slot, -1, op.Kind == BlueprintBuildOperationKind.Hammer ? 0 : 1, 0, out flight)) return false;
            if (op.Kind == BlueprintBuildOperationKind.Hammer) flight.InputGesture = p.poundRelease ? ToolInputGesture.HeldPress : ToolInputGesture.NeutralRelease;
            return true;
        }
        private bool HoikBuildCurrent(BuildFlight flight)
        {
            var target = flight.Target; var op = flight.Operation; var p = flight.Player;
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual)) return false;
            BlueprintBuildOperation current;
            if (!TryPlanHoikBuild(target, actual, BuildLane(target.Ordinal), out current) || current.Kind != op.Kind || current.Mode != op.Mode) return false;
            BlueprintBuildWait wait;
            if (!HoikCapabilities(target, p, op.Kind == BlueprintBuildOperationKind.HoikBase, out wait)) return false;
            if (op.Kind == BlueprintBuildOperationKind.Hammer)
            {
                if (!AppearanceToolMatches(target, flight.Source.Item) || !ShapePreflight(target, p, flight.Source.Item)) return false;
                flight.InputGesture = p.poundRelease ? ToolInputGesture.HeldPress : ToolInputGesture.NeutralRelease;
                return true;
            }
            if (flight.Source.Item.type != op.NativePlacement.UseItemType || !p.IsInTileInteractionRange(target.WorldX, target.WorldY,
                TileReachCheckSettings.Simple, flight.Source.Item.tileBoost + p.blockRange)) return false;
            if ((actual.Cell.Layers & CaptureLayer.Tile) != 0)
            {
                // Original replacement can remove natural hanging extras.
                // Do not grant those adjacent cells through this one-cell lease.
                var above = Main.tile[target.WorldX, target.WorldY - 1]; var below = Main.tile[target.WorldX, target.WorldY + 1];
                if (above == null || below == null || above.active() && (TileID.Sets.ReplaceTileBreakUp[above.type] || above.type == 165) ||
                    below.active() && (TileID.Sets.ReplaceTileBreakDown[below.type] || below.type == 165)) return false;
                return WorldGen.IsTileReplaceable(target.WorldX, target.WorldY) && WorldGen.WouldTileReplacementWork(op.NativePlacement.TileType, target.WorldX, target.WorldY);
            }
            return op.Kind == BlueprintBuildOperationKind.HoikBase && PreflightBuildOrdinary(target, flight.Source.Item) &&
                (BuildNeighbor(target.WorldX, target.WorldY, false) || BuildNeighbor(target.WorldX - 1, target.WorldY, false) ||
                 BuildNeighbor(target.WorldX + 1, target.WorldY, false) || BuildNeighbor(target.WorldX, target.WorldY - 1, false) || BuildNeighbor(target.WorldX, target.WorldY + 1, false));
        }
        private bool NativeHoikConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material, bool wall, bool animation)
        {
            if (wall || !animation || material != null || !HoikBuildCurrent(flight)) return false;
            var p = flight.Player;
            if (flight.Operation.Kind == BlueprintBuildOperationKind.Hammer)
                return consumer == ToolBuildConsumer.Hammer && p.toolTime == 0 && p.itemAnimation > 0 && p.controlUseItem && p.poundRelease;
            return (consumer == ToolBuildConsumer.Tiles || consumer == ToolBuildConsumer.TileReplacement) && p.ItemTimeIsZero && p.itemAnimation > 0 && p.controlUseItem;
        }
        private Action<Exception> BeginHoikConsumer(BuildFlight flight, ToolBuildConsumer consumer)
        {
            if (!HoikOperation(flight?.Operation) || consumer != ToolBuildConsumer.Tiles) return null;
            int old = flight.Player.builderAccStatus[10]; flight.Player.builderAccStatus[10] = 0; bool ended = false;
            return error => { if (ended) return; ended = true; if (flight.Player.builderAccStatus[10] == 0) flight.Player.builderAccStatus[10] = old; };
        }
        private bool HoikBuildApplied(BuildFlight flight)
        {
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(flight.Target.WorldX, flight.Target.WorldY, out actual) || (actual.Cell.Layers & CaptureLayer.Tile) == 0) return false;
            var op = flight.Operation; var target = flight.Target;
            bool applied = op.Kind == BlueprintBuildOperationKind.HoikBase ? actual.Cell.TileType == TileID.Stone :
                op.Kind == BlueprintBuildOperationKind.Hammer ? actual.Cell.TileType == TileID.Stone && (actual.Cell.Slope | (actual.Cell.HalfBrick ? 8 : 0)) != op.Mode :
                HoikFinalBody(target, actual) && actual.Cell.Slope == target.Cell.Slope && actual.Cell.HalfBrick == target.Cell.HalfBrick;
            if (!applied) return false;
            long key = WiringCoordinate(target.WorldX, target.WorldY);
            if (op.Kind == BlueprintBuildOperationKind.HoikSwap) hoikBases.Remove(key);
            else
            {
                var expected=actual.Cell;expected.ObjectId=target.Cell.ObjectId;
                hoikBases[key] = new OwnedHoikBase { Owner = target.Range.Owner, Identity = target.Identity, OriginX = target.OriginX, OriginY = target.OriginY,
                    X = target.WorldX, Y = target.WorldY, Mirrored = target.Range.Owner.MirroredHorizontally, Expected = expected, Tile = Main.tile[target.WorldX, target.WorldY] };
            }
            PublishProjectionScene(); return true;
        }
        private void ObserveHoikStore(int x, int y, int kind, int oldValue, int newValue)
        {
            int delta=oldValue^newValue;
            // Wall, liquid, wire and actuator fields have independent owners.
            // Only an actual foreground edit revokes this stone's provenance.
            if (delta==0 || !(kind==0 || kind==3 && (delta&0x747f)!=0 || kind==5 && (delta&0xa0)!=0)) return;
            long key = WiringCoordinate(x, y); OwnedHoikBase value;
            if (!hoikBases.TryGetValue(key, out value)) return;
            var flight = activeBuild;
            if (HoikOperation(flight?.Operation) && flight.Entered && buildTools?.Use.InNativeUse == true && flight.Token == buildTools.Use.Operation &&
                flight.Target.WorldX == x && flight.Target.WorldY == y && ReferenceEquals(flight.Target.Range.Owner, value.Owner)) return;
            hoikBases.Remove(key); PublishProjectionScene();
        }
        private IReadOnlyList<BlueprintHoikPending> CaptureHoikPending(BlueprintProjectionPlacement owner)
        {
            var result = new List<BlueprintHoikPending>();
            foreach (var pair in hoikBases)
            {
                var value = pair.Value;
                if (!ReferenceEquals(value.Owner, owner) || value.OriginX != owner.OriginX || value.OriginY != owner.OriginY || value.Mirrored != owner.MirroredHorizontally) continue;
                var cell = value.Expected; cell.X = value.X - owner.OriginX; cell.Y = value.Y - owner.OriginY;
                var source=cell;if(owner.MirroredHorizontally)source.X=owner.Summary.Width-1-source.X;
                if ((owner.Mask.Apply(source).Layers & CaptureLayer.Tile) == 0) continue;
                result.Add(new BlueprintHoikPending(cell));
            }
            result.Sort((a,b) => a.Expected.Y == b.Expected.Y ? a.Expected.X.CompareTo(b.Expected.X) : a.Expected.Y.CompareTo(b.Expected.Y));
            return result;
        }
        private void RestoreHoikPending(BlueprintProjectionPlacement owner, IReadOnlyList<BlueprintHoikPending> pending)
        {
            foreach (var record in pending)
            {
                var cell = record.Expected; int x = owner.OriginX + cell.X, y = owner.OriginY + cell.Y;
                if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) continue;
                // The pending record is not an action grant. Rebind its exact
                // foreground and immutable target in normal candidate planning.
                hoikBases[WiringCoordinate(x,y)] = new OwnedHoikBase { Owner=owner, Identity=owner.Identity, OriginX=owner.OriginX, OriginY=owner.OriginY,
                    X=x, Y=y, Mirrored=owner.MirroredHorizontally, Expected=cell, Tile=Main.tile[x,y] };
            }
        }
        private void ReleaseHoikPending(BlueprintProjectionPlacement owner)
        {
            if (hoikBases.Count == 0) return;
            var remove = new List<long>();
            foreach (var pair in hoikBases) if (ReferenceEquals(pair.Value.Owner, owner)) remove.Add(pair.Key);
            foreach (long key in remove) hoikBases.Remove(key);
        }
        private void RetireMaskedHoikPending(BlueprintProjectionPlacement owner)
        {
            if(hoikBases.Count==0)return;
            var remove=new List<long>();
            foreach(var pair in hoikBases)
            {
                var value=pair.Value;if(!ReferenceEquals(value.Owner,owner))continue;
                var cell=value.Expected;cell.X=value.X-owner.OriginX;cell.Y=value.Y-owner.OriginY;
                if(owner.MirroredHorizontally)cell.X=owner.Summary.Width-1-cell.X;
                if((owner.Mask.Apply(cell).Layers&CaptureLayer.Tile)==0)remove.Add(pair.Key);
            }
            foreach(long key in remove)hoikBases.Remove(key);
        }
        private void HoikWorldMaterials(BlueprintProjectionPlacement owner, CaptureCell target, BlueprintWorldMatchSample actual,
            bool sameBody, BlueprintMaterialAccumulator into)
        {
            if (!HoikTarget(target) || !actual.Available || sameBody && actual.Cell.Slope == target.Slope && actual.Cell.HalfBrick == target.HalfBrick) return;
            int x = owner.OriginX + target.X, y = owner.OriginY + target.Y;
            OwnedHoikBase own;
            bool owned = hoikBases.TryGetValue(WiringCoordinate(x,y), out own) && ReferenceEquals(own.Owner,owner) &&
                own.OriginX == owner.OriginX && own.OriginY == owner.OriginY && own.Mirrored == owner.MirroredHorizontally && SameHoikForeground(own.Expected,actual.Cell);
            // The ordinary reducer already counts a missing/conflicting final
            // platform. A wrong-shape existing platform must be placed again.
            if (sameBody) into.Add(MaterialCatalog.Tile(target));
            if (!owned) into.Add(ItemID.StoneBlock, 1);
        }
    }
}
