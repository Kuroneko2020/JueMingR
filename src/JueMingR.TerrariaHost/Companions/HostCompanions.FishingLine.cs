using System;
using System.Collections.Generic;
using JueMingR.Features.Fishing;
using JueMingR.TerrariaHost.Fishing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal enum FishingHand { None, Cast, Pull }

        internal sealed class CompanionFishingLine
        {
            internal readonly List<CompanionBobber> Bobbers = new List<CompanionBobber>(4);
            internal FishingHand Staged, Issued;
            internal FishingActivity CastActivity;
            internal Item Rod;
            internal int RodSlot = -1;
            internal int RodLiquid = -2;
            internal Vector2 Aim;
            internal uint IssuedAt, NextRodScan, RetryAt, NextPull, LastWet;
            internal bool Confirmed, Cutting, Ending, PullReceived, SawWet;
            internal int DryCutSlot = -1;
            internal FishingCatalog Catalog;
            internal readonly CompanionFishingGear Gear = new CompanionFishingGear();
            internal CompanionFishStorage Storage;
            internal SceneMetrics Scene;
            internal Point SceneCenter;
            internal uint NextScene;
            internal bool SceneKnown;
            internal int Quest = -1;
            internal bool QuestFinished;
        }

        internal sealed class CompanionBobber
        {
            internal FishingBobber Body;
            internal bool Qualified, Pullable;
        }

        private readonly FishingCatalog companionFishDirectory = new FishingCatalog();

        internal FishingCandidates CompanionFishingCandidates(Guid profile)
        {
            if (Find(profile) == null || !actors.TryGetValue(profile, out var a) || !FishingActive(a))
                return FishingCandidates.Unknown("当前没有可用钓点。");
            var line = a.FishingLine;
            foreach (var b in line.Bobbers)
                if (line.SceneKnown && b.Qualified && FishingObservation.Live(b.Body, a.Body) && b.Body.InLiquid)
                {
                    if (line.Catalog == null) line.Catalog = new FishingCatalog();
                    return line.Catalog.CurrentForActor(a.Body, b.Body.Projectile, !ActorQuestAvailable(a));
                }
            return FishingCandidates.Unknown("当前没有可用钓点。");
        }

        internal FishKey[] SearchCompanionFishing(string query) => companionFishDirectory.Search(query);

        private static bool NativeActorCanFish(Player body, Item rod, int liquid)
        {
            if (liquid != 1) return true;
            FishingNativeBait(body, out _, out int bait);
            return LavaFishingRod(rod) || ItemID.Sets.IsLavaBait[bait] || body.accLavaFishing;
        }

        private static bool LavaFishingRod(Item item) =>
            item != null && item.type > 0 && item.type < ItemID.Sets.CanFishInLava.Length && ItemID.Sets.CanFishInLava[item.type];

        internal bool TryFishingRod(Actor a, int liquid, out Item rod)
        {
            var p = a.Body;
            var line = a.FishingLine;
            if (line.Bobbers.Count != 0)
            {
                // The live native cast retains its original rod. A better
                // rod found while planning can only authorize a later cast.
                rod = RodCurrent(a) ? line.Rod : null;
                return rod != null;
            }
            if (line.RodLiquid == liquid && Main.GameUpdateCount < line.NextRodScan &&
                (line.Rod == null || RodCurrent(a)))
            {
                rod = line.Rod;
                return rod != null;
            }
            bool lavaReady = liquid != 1 || p.accLavaFishing;
            if (!lavaReady)
            {
                FishingNativeBait(p, out _, out int bait);
                lavaReady = ItemID.Sets.IsLavaBait[bait] || CanPrepareLavaFishing(a);
            }
            int slot = RodCurrent(a) && (lavaReady || LavaFishingRod(line.Rod)) ? line.RodSlot : -1;
            for (int i = 0; i < 50; i++)
            {
                var item = p.inventory[i];
                if (item == null || item.IsAir || item.fishingPole <= 0 || item.shoot <= 0 ||
                    !lavaReady && !LavaFishingRod(item) ||
                    slot >= 0 && p.inventory[slot].fishingPole >= item.fishingPole ||
                    !SupplyAdmitted(a, p.inventory, 0, i, ownsSelected: true)) continue;
                slot = i;
            }
            line.RodSlot = slot;
            line.Rod = rod = slot >= 0 ? p.inventory[slot] : null;
            line.RodLiquid = liquid;
            line.NextRodScan = Main.GameUpdateCount + 24;
            return rod != null;
        }

        private bool RodCurrent(Actor a)
        {
            var line = a.FishingLine;
            var p = a.Body;
            return line.RodSlot >= 0 && ReferenceEquals(p.inventory[line.RodSlot], line.Rod) &&
                line.Rod != null && !line.Rod.IsAir && line.Rod.fishingPole > 0 &&
                SupplyAdmitted(a, p.inventory, 0, line.RodSlot, ownsSelected: true);
        }

        private static bool FishingHandFree(Player p) => FreeHand(p) && !p.selectedItemState.HasBufferedChange &&
            !p.dead && !p.noItems && !p.CCed && !p.cursed && !p.pulley && p.releaseUseItem;

        // The native selected-slot buffer, ItemCheck and projectile birth each
        // acknowledge a different phase. One cast owns a whole group of bobbers.
        private bool TickFishingLine(Actor a, bool ready, bool pause, bool preempt)
        {
            var line = a.FishingLine;
            var p = a.Body;
            line.Staged = FishingHand.None;
            bool active = FishingActive(a);
            if (active && a.Preferences.Fishing.StoreMode == 2 && line.Storage == null)
                line.Storage = new CompanionFishStorage();
            if (line.Storage != null) AdvanceFishStorage(a);
            if (!preempt && line.Gear.ManualLoadout >= 0 && TryManualFishingLoadout(a)) return true;
            if (!active && !line.Ending && line.Bobbers.Count == 0 && !line.Gear.Active) return false;
            if (a.Body.dead || a.Closing) return false;
            ObserveFishingLine(a);
            int liquid = FishingLiquid(a);
            if (!active || preempt || line.Ending)
            {
                UpdateFishingGear(a, false, liquid);
                if (!preempt && line.Bobbers.Count != 0 && !line.PullReceived) return CutFishingLine(a);
                if (line.Bobbers.Count == 0 && line.Issued == FishingHand.None) line.Ending = false;
                return false;
            }
            if (!ready || Main.GameUpdateCount < line.RetryAt) return false;
            if (line.Bobbers.Count != 0)
            {
                if (line.PullReceived || line.Cutting || !FishingHandFree(p)) return false;
                if (!RodCurrent(a)) return CutFishingLine(a);
                if (!UpdateFishingGear(a, true, liquid)) return false;
                if (!line.Confirmed && (line.SawWet && Main.GameUpdateCount - line.IssuedAt >= 90 ||
                    Main.GameUpdateCount - line.IssuedAt >= 240)) return CutFishingLine(a);
                if (line.Confirmed && Main.GameUpdateCount - line.LastWet >= 60) return CutFishingLine(a);
                if (pause) return false;
                bool bite = false, rejected = false;
                foreach (var b in line.Bobbers)
                {
                    if (!b.Body.InLiquid || b.Body.Projectile.ai[1] >= 0) continue;
                    bite = true;
                    var candidate = b.Body.Candidate;
                    var value = a.Preferences.Fishing;
                    rejected |= !b.Qualified || !FishFilter.Keep(value, candidate,
                        candidate.HasValue ? FishingCatalog.Name(candidate.Value) : null, FishingCatalog.Sonar(p),
                        candidate.HasValue && FishingCatalog.Crate(candidate.Value),
                        candidate.HasValue && candidate.Value.Kind == FishKind.Item && ActorQuestType(a) == candidate.Value.Id);
                }
                if (bite && rejected && a.Preferences.Fishing.Cut)
                    return CutFishingLine(a);
                if (!bite || rejected || !line.Confirmed || line.Issued == FishingHand.Pull ||
                    Main.GameUpdateCount < line.NextPull) return false;
                return StageFishingHand(a, FishingHand.Pull);
            }
            if (line.Issued == FishingHand.Cast || line.Cutting) return false;
            bool hasRod = TryFishingRod(a, liquid, out var rod);
            FishingNativeBait(p, out int baitPower, out _);
            FishingNeedsGear(a, !hasRod && liquid != 1, baitPower <= 0);
            if (!hasRod || baitPower <= 0 || !UpdateFishingGear(a, true, liquid) ||
                !NativeActorCanFish(p, rod, liquid) || !FishingSceneReady(a) || !FishingHandFree(p))
                return false;
            if (!TryFishingAim(a, rod, out Vector2 aim)) return false;
            line.Aim = aim;
            return StageFishingHand(a, FishingHand.Cast);
        }

        private bool StageFishingHand(Actor a, FishingHand action)
        {
            if (!RodCurrent(a)) return false;
            var p = a.Body;
            var line = a.FishingLine;
            if (p.selectedItem != line.RodSlot)
            {
                p.selectedItemState.Select(line.RodSlot);
                return true;
            }
            a.Aim = line.Aim;
            if (line.Aim.X != p.Center.X) p.ChangeDir(line.Aim.X > p.Center.X ? 1 : -1);
            p.controlUseItem = true;
            line.Staged = action;
            return true;
        }

        private bool PrepareFishingUse(Actor a)
        {
            var line = a.FishingLine;
            var p = a.Body;
            if (line.Staged == FishingHand.None) return false;
            bool current = FishingActive(a) && !line.Ending && p.controlUseItem && FishingHandFree(p) &&
                RodCurrent(a) && p.selectedItem == line.RodSlot;
            if (current && line.Staged == FishingHand.Cast)
            {
                FishingNativeBait(p, out int baitPower, out _);
                current = baitPower > 0 && NativeActorCanFish(p, line.Rod, FishingLiquid(a)) &&
                    TryFishingAim(a, line.Rod, out line.Aim);
                if (current) a.Aim = line.Aim;
            }
            if (current)
            {
                line.Issued = line.Staged;
                line.IssuedAt = Main.GameUpdateCount;
                if (line.Issued == FishingHand.Cast) line.CastActivity = a.Fishing;
            }
            else p.controlUseItem = false;
            line.Staged = FishingHand.None;
            return true;
        }

        private bool CutFishingLine(Actor a)
        {
            var line = a.FishingLine;
            var p = a.Body;
            if (line.Cutting || !FishingHandFree(p)) return false;
            if (p.HeldItem.fishingPole <= 0) { line.Cutting = true; return false; }
            for (int slot = 0; slot < 50; slot++)
                if (p.inventory[slot].fishingPole <= 0 && SupplyAdmitted(a, p.inventory, 0, slot, ownsSelected: true))
                {
                    line.Cutting = true;
                    line.DryCutSlot = slot;
                    p.selectedItemState.Select(slot);
                    return true;
                }
            return false;
        }

        private void ObserveFishingLine(Actor a)
        {
            var line = a.FishingLine;
            var p = a.Body;
            int had = line.Bobbers.Count;
            for (int i = had - 1; i >= 0; i--)
            {
                var b = line.Bobbers[i];
                if (!FishingObservation.Live(b.Body, p)) { line.Bobbers.RemoveAt(i); continue; }
                if (line.Ending || line.Cutting || line.PullReceived || !ReferenceEquals(line.CastActivity, a.Fishing)) continue;
                if (!b.Body.InLiquid) continue;
                line.SawWet = true;
                // Pool edits or a newly selected target invalidate the last
                // wet observation. Retire through the actual selection edge.
                b.Qualified = (b.Qualified || RefreshFishingZones(a)) && FishingEnteredLiquid(a, b.Body.Projectile);
                if (b.Qualified)
                {
                    line.Confirmed = true;
                    line.LastWet = Main.GameUpdateCount;
                }
            }
            if (line.Cutting && line.DryCutSlot >= 0 &&
                (!p.selectedItemState.HasBufferedChange && p.selectedItem != line.DryCutSlot ||
                 p.inventory[line.DryCutSlot].fishingPole > 0))
            {
                line.Cutting = false;
                line.DryCutSlot = -1;
            }
            if (line.Bobbers.Count != 0) return;
            if (had == 0 && (line.Issued != FishingHand.Cast ||
                !FreeHand(p) || Main.GameUpdateCount - line.IssuedAt < 45)) return;
            if (!line.Confirmed && !line.Ending && !line.PullReceived && FishingActive(a))
            {
                line.RetryAt = Main.GameUpdateCount + 20;
                FishingCastUnconfirmed(a);
            }
            line.Cutting = line.PullReceived = line.Confirmed = line.SawWet = false;
            line.DryCutSlot = -1;
            line.Issued = FishingHand.None;
            line.CastActivity = null;
            line.Catalog?.Clear();
        }

        private void StopFishingLine(Actor a, bool retire = false)
        {
            var line = a.FishingLine;
            line.Ending = true;
            line.Staged = FishingHand.None;
            line.Catalog?.Clear();
            line.SceneKnown = false;
            line.NextScene = 0;
            if (retire || !FishingActive(a)) line.Storage?.ManualSources?.Clear();
            if (!retire) return;
            RestoreFishingGear(a, retiring: true);
            line.Gear.ManualLoadout = -1;
            // Lifecycle retirement kills leased bobbers next. Do not qualify
            // any unfinished cast or pull from that native cleanup.
            line.Bobbers.Clear();
            line.Ending = line.Cutting = line.PullReceived = line.Confirmed = line.SawWet = false;
            line.Issued = FishingHand.None;
            line.CastActivity = null;
            line.DryCutSlot = -1;
            line.Rod = null;
            line.RodSlot = -1;
            line.NextRodScan = 0;
            line.RetryAt = 0;
            line.NextPull = 0;
            line.Storage?.CancelQueued();
            if (a.Body.dead && a.Body.difficulty != 0 && a.Body.difficulty != 3) RetireDroppedFishStorage(a);
        }

        internal void BeforeFishingDeath(Actor a)
        {
            var line = a.FishingLine;
            if (FishingActive(a) || line.Ending || line.Gear.Active)
                StopFishingLine(a, retire: true);
        }

        private void CompanionBobberBorn(Actor a, Projectile b)
        {
            var line = a.FishingLine;
            if (line.Issued != FishingHand.Cast || !ReferenceEquals(line.Rod, a.Body.HeldItem) ||
                (int)b.key == -1 || b.type != CompanionFishingCast.Bobber(a.Body, line.Rod)) return;
            // A native delayed birth during stop/teleport belongs to the
            // departing cast. Keep it in the previous tail for normal cut.
            if (!ReferenceEquals(line.CastActivity, a.Fishing)) line.Ending = true;
            line.Bobbers.Add(new CompanionBobber { Body = new FishingBobber { Projectile = b, Slot = b.whoAmI, Key = (int)b.key } });
        }

        internal bool BeforeCompanionPull(Actor a, Item rod)
        {
            var line = a.FishingLine;
            if (line.Issued != FishingHand.Pull || line.IssuedAt != Main.GameUpdateCount ||
                line.PullReceived || !ReferenceEquals(rod, line.Rod)) return false;
            foreach (var b in line.Bobbers)
                b.Pullable = b.Qualified && FishingObservation.Live(b.Body, a.Body) && b.Body.Projectile.ai[0] == 0;
            return true;
        }

        internal void AfterCompanionPull(Actor a)
        {
            var line = a.FishingLine;
            foreach (var b in line.Bobbers)
                if (b.Pullable && FishingObservation.Live(b.Body, a.Body) && b.Body.Projectile.ai[0] >= 1)
                {
                    line.PullReceived = true;
                    a.Dirty = true;
                    return;
                }
        }

        internal void FinishFishingItemCheck(Actor a)
        {
            var line = a.FishingLine;
            if (line.Issued != FishingHand.Pull || line.IssuedAt != Main.GameUpdateCount || line.PullReceived) return;
            foreach (var b in line.Bobbers)
                if (!FishingObservation.Live(b.Body, a.Body) || b.Body.Projectile.ai[0] != 0)
                    return; // An observed native transition is never replayed.
            // ItemCheck finished normally and every original bobber is still
            // waiting. Retry only on a later released input with live bite
            // admission. An exception never reaches this definitive branch.
            line.Issued = FishingHand.None;
            line.NextPull = Main.GameUpdateCount + 8;
        }
    }
}
