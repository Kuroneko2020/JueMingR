using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    [Flags]
    internal enum BlueprintBuildWait : byte
    {
        None = 0, Material = 1, Player = 2, Dependencies = 4, Container = 8
    }

    internal sealed partial class HostBlueprint
    {
        // These are current inventory types, not a history or a target index.
        // At most the existing 50 primary slots contribute. A positive type
        // retains its recovery serial through moves, merges and normal spend;
        // only loss followed by a qualified source creates another recovery.
        private Dictionary<int, long> buildSupply = new Dictionary<int, long>();
        private Dictionary<int, long> buildNextSupply = new Dictionary<int, long>();
        private BlueprintMaterialInventory buildSupplyInventory;
        private long buildSupplyRevision, buildActivationRevision, buildContainerRevision;
        private float buildPlayerX, buildPlayerY;
        private int buildRange, buildReachX, buildReachY, buildDirection, buildPulleyDirection;
        private bool buildEligible, buildPulley;

        private void ObserveBuildSignals()
        {
            if (MaterialBuildDemand?.Invoke() != true || projectionScene.Count == 0) return;
            var p = buildTools?.Player;
            bool eligible = p != null && CanBuildAutomatically(p);
            // Native reach includes Ceiling(position + body extent). A legal
            // subpixel step can cross that boundary without changing (int)X/Y.
            // This only wakes actual player-gate waits, never world proofs.
            float px = p?.position.X ?? 0f, py = p?.position.Y ?? 0f;
            int reach = p?.blockRange ?? 0, direction = p?.direction ?? 0, pulleyDirection = p?.pulleyDir ?? 0;
            bool pulley = p?.pulley == true;
            if (px != buildPlayerX || py != buildPlayerY || reach != buildRange || Player.tileRangeX != buildReachX || Player.tileRangeY != buildReachY ||
                direction != buildDirection || pulleyDirection != buildPulleyDirection || pulley != buildPulley || eligible != buildEligible)
            {
                buildPlayerX = px; buildPlayerY = py; buildRange = reach; buildReachX = Player.tileRangeX; buildReachY = Player.tileRangeY;
                buildDirection = direction; buildPulleyDirection = pulleyDirection; buildPulley = pulley; buildEligible = eligible;
                MaterialBuildEligibilityRevision++;
            }
            var inventory = MaterialInventory;
            if (ReferenceEquals(buildSupplyInventory, inventory)) return;
            buildSupplyInventory = inventory; buildNextSupply.Clear();
            if (inventory?.Ready == true) foreach (var slot in inventory.Slots)
            {
                if (!slot.Eligible || slot.ItemType <= 0 || slot.Stack <= 0 || buildNextSupply.ContainsKey(slot.ItemType)) continue;
                long recovered;
                if (!buildSupply.TryGetValue(slot.ItemType, out recovered)) recovered = ++buildSupplyRevision;
                buildNextSupply.Add(slot.ItemType, recovered);
            }
            var previous = buildSupply; buildSupply = buildNextSupply; buildNextSupply = previous;
            buildNextSupply.Clear();
        }

        private void CaptureBuildWalkSignals(BlueprintRangeMaterials value)
        {
            value.BuildPlayerRevision = MaterialBuildEligibilityRevision;
            value.BuildSupplyRevision = buildSupplyRevision;
            value.BuildContainerRevision = buildContainerRevision;
            value.BuildActivationRevision = buildActivationRevision;
        }
        private void ResetMaterialBuildWalk(BlueprintProjectionMatchRange range, bool resetNativeRetry)
        {
            var value = range.Materials; if (value == null) return;
            value.BuildEpoch++; value.CandidateCursor = 0; value.Candidate = value.CandidateFallback = null;
            value.BuildExhausted = value.BuildRetryPending = false;
            value.BuildWaits = BlueprintBuildWait.None; value.BuildDependencyWaitSlots = 0;
            CaptureBuildWalkSignals(value);
            if (resetNativeRetry) value.EligibilityRetry = false;
        }
        private void WaitForBuild(BlueprintBuildTarget target, BlueprintBuildWait reason, ulong dependencySlots = 0, bool retryPending = false)
        {
            if (target?.Range.Materials == null || !IsBuildTargetCurrent(target) || IsUnconfirmedTarget(target)) return;
            var value = target.Range.Materials;
            value.BuildWaits |= reason; value.BuildDependencyWaitSlots |= dependencySlots;
            // A prepared source may move to another qualified slot without a
            // type ever becoming unavailable. Its invalid flight gets one local
            // merged retry, while later ordinals still finish this same walk.
            value.BuildRetryPending |= retryPending;
        }
        private bool BuildDependencyReleased(BlueprintRangeMaterials value)
        {
            var p = player; if (p == null || buildTools == null) return false;
            bool paint = p.autoPaint && p.builderAccStatus[3] == 0, actuator = p.autoActuator && p.builderAccStatus[2] == 0;
            ulong waiting = value.BuildDependencyWaitSlots;
            for (int slot = 0; slot < 58 && waiting != 0; slot++)
            {
                ulong bit = 1UL << slot; if ((waiting & bit) == 0) continue; waiting &= ~bit;
                var item = p.inventory[slot];
                if (item == null || item.IsAir || !(paint && item.PaintOrCoating || actuator && item.type == Terraria.ID.ItemID.Actuator) ||
                    !p.inventoryChestStack[slot] && !buildTools.Items.World.ManualMaterials.Contains(item) && !buildTools.ProtectedAfterYield(item))
                    return true;
            }
            return false;
        }
        private void TryWakeBuildEligibility(BlueprintProjectionMatchRange range)
        {
            var value = range.Materials;
            if (value == null || MaterialBuildDemand?.Invoke() != true) return;
            // Re-enabling needs a fresh support read even when an independent
            // material view kept the bill current while build watches slept.
            // Keep this walk's forward cursor and the original source request.
            if (value.BuildEpoch == 0)
            { ResetMaterialBuildWalk(range, false); DemandProjectionMatchRange(range); }
            if (value.BuildActivationRevision != buildActivationRevision)
            {
                value.BuildActivationRevision = buildActivationRevision;
                value.Candidate = value.CandidateFallback = null; value.BuildRetryPending = true;
                DemandProjectionMatchRange(range);
            }
            if ((value.BuildWaits & BlueprintBuildWait.Material) != 0 && value.CatalogRevision == materialPlacementIdentity && value.Desired != null)
                foreach (var supply in buildSupply)
                    if (supply.Value > value.BuildSupplyRevision && value.Desired.ContainsKey(supply.Key)) { value.BuildRetryPending = true; break; }
            if ((value.BuildWaits & BlueprintBuildWait.Player) != 0 && value.BuildPlayerRevision != MaterialBuildEligibilityRevision ||
                (value.BuildWaits & BlueprintBuildWait.Container) != 0 && value.BuildContainerRevision != buildContainerRevision ||
                (value.BuildWaits & BlueprintBuildWait.Dependencies) != 0 && BuildDependencyReleased(value)) value.BuildRetryPending = true;

            bool nativeDue = value.EligibilityRetry && materialTick >= value.EligibilityRetryAt;
            // An outstanding source/sample is already an active walk. Later
            // signals coalesce into one permission and cannot cancel/restart it.
            if (!value.BuildExhausted || value.Candidate != null || range.Demand || range.Reading || value.Progress != null ||
                !value.Current || !range.Current || !value.BuildRetryPending && !nativeDue) return;
            ResetMaterialBuildWalk(range, false);
            DemandProjectionMatchRange(range);
#if DEBUG
            if (nativeDue) BuildEligibilityRetryWakes++;
#endif
        }
        private bool BuildRetryReady(BlueprintBuildTarget target)
        {
            var value = target.Range.Materials;
            if (value == null || !value.EligibilityRetry || value.EligibilityRetryOrdinal != target.Ordinal) return true;
            // Local recovery must not bypass a registered original rejection's
            // deadline. Consume it only when that same target gets its turn.
            if (materialTick < value.EligibilityRetryAt) return false;
            value.EligibilityRetry = false; return true;
        }
        private void ClearBuildProgressSignals()
        {
            buildSupply.Clear(); buildNextSupply.Clear(); buildSupplyInventory = null;
            buildEligible = false;
        }
    }
}
