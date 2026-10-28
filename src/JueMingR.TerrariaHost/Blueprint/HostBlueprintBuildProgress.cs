using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    [Flags]
    internal enum BlueprintBuildWait : byte
    {
        None = 0, Material = 1, Player = 2, Dependencies = 4, Container = 8, Dynamic = 16, Capability = 32
    }

    internal sealed partial class HostBlueprint
    {
        // These are current inventory types, not a history or a target index.
        // At most the existing 50 primary slots contribute. A positive type
        // retains its recovery serial through moves, merges and normal spend;
        // loss followed by a qualified source or the finite restoration
        // threshold below creates another recovery.
        private Dictionary<int, long> buildSupply = new Dictionary<int, long>();
        private Dictionary<int, long> buildNextSupply = new Dictionary<int, long>();
        // Native wall scraping can also clear the selected foreground. Its
        // restoration proof needs at most two of one paint/coating. Remember
        // only 0/1/>=2 across qualified inventory publications: replenishing
        // one to two is a real recovery, while ordinary positive-stack noise
        // and equivalent moves must not restart exhausted source walks.
        private Dictionary<int, int> buildAppearanceSupply = new Dictionary<int, int>();
        private Dictionary<int, int> buildNextAppearanceSupply = new Dictionary<int, int>();
        private BlueprintMaterialInventory buildSupplyInventory;
        private Dictionary<int, int> buildItemReach = new Dictionary<int, int>();
        private Dictionary<int, int> buildNextItemReach = new Dictionary<int, int>();
        private long buildSupplyRevision, buildActivationRevision, buildContainerRevision;
        private float buildPlayerX, buildPlayerY;
        private int buildRange, buildReachX, buildReachY, buildDirection, buildPulleyDirection, buildPlayerWidth, buildPlayerHeight, buildEnvelopeBoost;
        private bool buildEligible, buildPulley;

        private void ObserveBuildSignals()
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.Signals);
#endif
            if (MaterialBuildDemand?.Invoke() != true || projectionScene.Count == 0) return;
            var p = buildTools?.Player;
            // Eligibility describes persistent business permission, not whether
            // this outer callback happened to contain a native execution epoch.
            bool eligible = p != null && CanRetainBuildIntent(p, false);
            // Native reach includes Ceiling(position + body extent). A legal
            // subpixel step can cross that boundary without changing (int)X/Y.
            // This only wakes actual player-gate waits, never world proofs.
            float px = p?.position.X ?? 0f, py = p?.position.Y ?? 0f;
            int reach = p?.blockRange ?? 0, direction = p?.direction ?? 0, pulleyDirection = p?.pulleyDir ?? 0;
            bool pulley = p?.pulley == true;
#if JMR_BLUEPRINT_DIAGNOSTIC
            // The original OR still short-circuits. This records the actual
            // first changed family, not hypothetical later conditions.
            if (BlueprintBuildTrace.Changed(px != buildPlayerX || py != buildPlayerY,BuildTraceReason.SignalPosition) ||
                BlueprintBuildTrace.Changed(reach != buildRange || Player.tileRangeX != buildReachX || Player.tileRangeY != buildReachY,BuildTraceReason.SignalReach) ||
                BlueprintBuildTrace.Changed(direction != buildDirection || pulleyDirection != buildPulleyDirection || pulley != buildPulley,BuildTraceReason.SignalDirection) ||
                BlueprintBuildTrace.Changed(eligible != buildEligible,BuildTraceReason.SignalEligibility) ||
                BlueprintBuildTrace.Changed((p?.width ?? 0) != buildPlayerWidth || (p?.height ?? 0) != buildPlayerHeight,BuildTraceReason.SignalBody))
#else
            if (px != buildPlayerX || py != buildPlayerY || reach != buildRange || Player.tileRangeX != buildReachX || Player.tileRangeY != buildReachY ||
                direction != buildDirection || pulleyDirection != buildPulleyDirection || pulley != buildPulley || eligible != buildEligible ||
                (p?.width ?? 0) != buildPlayerWidth || (p?.height ?? 0) != buildPlayerHeight)
#endif
            {
                buildPlayerX = px; buildPlayerY = py; buildRange = reach; buildReachX = Player.tileRangeX; buildReachY = Player.tileRangeY;
                buildDirection = direction; buildPulleyDirection = pulleyDirection; buildPulley = pulley; buildEligible = eligible;
                buildPlayerWidth = p?.width ?? 0; buildPlayerHeight = p?.height ?? 0;
                MaterialBuildEligibilityRevision++;
            }
            var inventory = MaterialInventory;
            // Inventory publication intentionally ignores item reach fields.
            // Observe the current exact lease's qualified source when present,
            // otherwise the first eligible source, just as TryPrepareBuild does.
            // The same one inventory walk still owns the broad-phase maximum.
            // This wakes a shorter material even if another item keeps the
            // overall envelope unchanged; stack noise/equivalent moves do not.
            buildNextItemReach.Clear(); int envelopeBoost = 0;
            var hint = CurrentBuildSourceHint(p);
            if (p != null && inventory?.Ready == true) foreach (var slot in inventory.Slots)
            {
#if DEBUG
                BuildReachSourceReads++;
#endif
                if (!slot.Eligible || slot.Stack <= 0 || slot.ItemType <= 0) continue;
                var item = p.inventory[slot.Slot];
                if (!buildTools.BuildInventoryEligible(slot.Slot, item)) continue;
                envelopeBoost = Math.Max(envelopeBoost, item.tileBoost);
                if (!buildNextItemReach.ContainsKey(slot.ItemType) || hint != null && hint.Slot == slot.Slot && hint.Type == slot.ItemType && ReferenceEquals(hint.Item, item))
                    buildNextItemReach[slot.ItemType] = item.tileBoost;
            }
            bool reachChanged = envelopeBoost != buildEnvelopeBoost || buildItemReach.Count != buildNextItemReach.Count;
            if (!reachChanged) foreach (var entry in buildNextItemReach)
            { int old; if (!buildItemReach.TryGetValue(entry.Key, out old) || old != entry.Value) { reachChanged = true; break; } }
            var previousReach = buildItemReach; buildItemReach = buildNextItemReach; buildNextItemReach = previousReach;
            buildEnvelopeBoost = envelopeBoost;
            if (reachChanged) { MaterialBuildEligibilityRevision++; buildNearTickSet = false;
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SignalItemReach);
#endif
            }
            if (ReferenceEquals(buildSupplyInventory, inventory)) return;
            buildSupplyInventory = inventory; buildNextSupply.Clear(); buildNextAppearanceSupply.Clear();
            if (inventory?.Ready == true) foreach (var slot in inventory.Slots)
            {
                if (!slot.Eligible || slot.ItemType <= 0 || slot.Stack <= 0) continue;
                long recovered;
                if (!buildNextSupply.TryGetValue(slot.ItemType, out recovered))
                {
                    if (!buildSupply.TryGetValue(slot.ItemType, out recovered)) recovered = ++buildSupplyRevision;
                    buildNextSupply.Add(slot.ItemType, recovered);
                }
                var item = p?.inventory[slot.Slot];
                if (item == null || item.type != slot.ItemType || !item.PaintOrCoating) continue;
                int amount, previousAmount;
                buildNextAppearanceSupply.TryGetValue(slot.ItemType, out amount);
                int currentAmount = (int)Math.Min(2L, amount + Math.Min(2L, slot.Stack));
                buildNextAppearanceSupply[slot.ItemType] = currentAmount;
                if (amount < 2 && currentAmount == 2 && buildSupply.ContainsKey(slot.ItemType) &&
                    (!buildAppearanceSupply.TryGetValue(slot.ItemType, out previousAmount) || previousAmount < 2))
                    buildNextSupply[slot.ItemType] = ++buildSupplyRevision;
            }
            var previous = buildSupply; buildSupply = buildNextSupply; buildNextSupply = previous;
            buildNextSupply.Clear();
            var previousAppearance = buildAppearanceSupply; buildAppearanceSupply = buildNextAppearanceSupply; buildNextAppearanceSupply = previousAppearance;
            buildNextAppearanceSupply.Clear();
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
            value.BuildEpoch++; value.CandidateCursor = 0; value.Candidate = null;
            value.BuildExhausted = value.BuildRetryPending = false;
            value.BuildWaits = BlueprintBuildWait.None; value.BuildDependencyWaitSlots = 0;
            CaptureBuildWalkSignals(value);
            if (resetNativeRetry) value.EligibilityRetry = false;
        }
        private void WaitForBuild(BlueprintBuildTarget target, BlueprintBuildWait reason, ulong dependencySlots = 0, bool retryPending = false)
        {
            if (target?.Range.Materials == null || !IsBuildTargetCurrent(target) || IsUnconfirmedTarget(target)) return;
            var value = target.Range.Materials;
            if(reason==BlueprintBuildWait.Material && target.Operation!=null)reason|=BlueprintBuildWait.Capability;
            value.BuildWaits |= reason; value.BuildDependencyWaitSlots |= dependencySlots;
            // A prepared source may move to another qualified slot without a
            // type ever becoming unavailable. Its invalid flight gets one local
            // merged retry, while later ordinals still finish this same walk.
            value.BuildRetryPending |= retryPending;
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Count(BuildTraceReason.WaitRegistered);
            var diagnosticReason=reason==BlueprintBuildWait.Material?BuildTraceReason.MissingMaterial:
                reason==BlueprintBuildWait.Container?BuildTraceReason.ContainerWaiting:reason==BlueprintBuildWait.Dependencies?BuildTraceReason.Dependencies:
                reason==BlueprintBuildWait.Dynamic?BuildTraceReason.DynamicWait:BuildTraceReason.PlayerReachOrPulley;
            BlueprintBuildTrace.Count(diagnosticReason);DiagnosticTarget(target,BuildTraceStage.Retry,diagnosticReason);
#endif
        }
        private void WaitForDynamicBuild(BlueprintBuildTarget target)
        {
            WaitForBuild(target, BlueprintBuildWait.Dynamic);
            if (target.Range?.Materials != null) target.Range.Materials.DynamicRetryAt = materialTick + 8;
        }
        private bool BuildDependencyReleased(BlueprintRangeMaterials value)
        {
            var p = player; if (p == null || buildTools == null) return false;
            ulong waiting = value.BuildDependencyWaitSlots;
            for (int slot = 0; slot < 58 && waiting != 0; slot++)
            {
                ulong bit = 1UL << slot; if ((waiting & bit) == 0) continue; waiting &= ~bit;
                var item = p.inventory[slot];
                if (item == null || item.IsAir ||
                    !p.inventoryChestStack[slot] && !buildTools.Items.World.ManualMaterials.Contains(item) && !buildTools.ProtectedAfterYield(item) &&
                    !buildTools.Items.Ownership.IsProtected(slot) && !WiringPendingMaterial(item))
                    return true;
            }
            return false;
        }
        private void TryWakeBuildEligibility(BlueprintProjectionMatchRange range)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.Wake);
#endif
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
                value.Candidate = null; value.BuildRetryPending = true;
                DemandProjectionMatchRange(range);
            }
            if ((value.BuildWaits & BlueprintBuildWait.Material) != 0 && value.CatalogRevision == materialPlacementIdentity && value.Desired != null)
                foreach (var supply in buildSupply)
                    if (supply.Value > value.BuildSupplyRevision && value.Desired.ContainsKey(supply.Key)) { value.BuildRetryPending = true; break; }
            if((value.BuildWaits&BlueprintBuildWait.Capability)!=0 && value.BuildSupplyRevision!=buildSupplyRevision)value.BuildRetryPending=true;
            if ((value.BuildWaits & BlueprintBuildWait.Player) != 0 && value.BuildPlayerRevision != MaterialBuildEligibilityRevision ||
                (value.BuildWaits & BlueprintBuildWait.Container) != 0 && value.BuildContainerRevision != buildContainerRevision ||
                (value.BuildWaits & BlueprintBuildWait.Dependencies) != 0 && BuildDependencyReleased(value)) value.BuildRetryPending = true;

            bool nativeDue = value.EligibilityRetry && materialTick >= value.EligibilityRetryAt;
            bool dynamicDue = (value.BuildWaits & BlueprintBuildWait.Dynamic) != 0 && materialTick >= value.DynamicRetryAt;
#if JMR_BLUEPRINT_DIAGNOSTIC
            if(nativeDue)BlueprintBuildTrace.Count(BuildTraceReason.RetryDue);
#endif
            // An outstanding source/sample is already an active walk. Later
            // signals coalesce into one permission and cannot cancel/restart it.
            if (!value.BuildExhausted || value.Candidate != null || range.Demand || range.Reading || value.Progress != null ||
                !value.Current || !range.Current || !value.BuildRetryPending && !nativeDue && !dynamicDue)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                // These are the already-known scalar blockers. No dependency,
                // demand, stamp or eligibility query is repeated to explain it.
                BlueprintBuildTrace.Count(BuildTraceReason.WakeBlocked);
                if(!value.BuildExhausted)BlueprintBuildTrace.Count(BuildTraceReason.WakeNotExhausted);
                if(value.Candidate!=null)BlueprintBuildTrace.Count(BuildTraceReason.WakeCandidate);
                if(range.Demand)BlueprintBuildTrace.Count(BuildTraceReason.WakeDemand);
                if(range.Reading)BlueprintBuildTrace.Count(BuildTraceReason.WakeReading);
                if(value.Progress!=null)BlueprintBuildTrace.Count(BuildTraceReason.WakeProgress);
                if(!value.Current)BlueprintBuildTrace.Count(BuildTraceReason.WakeMaterialNotCurrent);
                if(!range.Current)BlueprintBuildTrace.Count(BuildTraceReason.WakeRangeNotCurrent);
                if(!value.BuildRetryPending && !nativeDue && !dynamicDue)BlueprintBuildTrace.Count(BuildTraceReason.WakeNoPermission);
#endif
                return;
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Semantic(BuildTraceStage.Wake,BuildTraceReason.WakePerformed,value.EligibilityRetryOrdinal,unchecked((long)materialTick),unchecked((long)value.EligibilityRetryAt),nativeDue?1:0);
#endif
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
            if (materialTick < value.EligibilityRetryAt)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                DiagnosticReject(target,BuildTraceStage.Retry,BuildTraceReason.RetryWaiting);
#endif
                return false;
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Count(BuildTraceReason.RetryConsumed);
#endif
            value.EligibilityRetry = false; return true;
        }
        private void ClearBuildProgressSignals()
        {
            buildSupply.Clear(); buildNextSupply.Clear(); buildSupplyInventory = null;
            buildAppearanceSupply.Clear(); buildNextAppearanceSupply.Clear();
            buildItemReach.Clear(); buildNextItemReach.Clear(); buildEnvelopeBoost = 0;
            buildEligible = false;buildNearTickSet=false;
        }
    }
}
