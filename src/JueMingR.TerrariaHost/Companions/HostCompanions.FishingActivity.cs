using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private Guid fishingPageOwner, fishingPageId;
        private int fishingActors;
        internal bool WantsFishingBackground => fishingActors != 0;

        internal sealed class FishingActivity
        {
            internal readonly Vector2 RequestedFeet;
            internal Vector2 FeetGoal;
            internal readonly float Gravity;
            internal Point? Seat;
            internal ushort SeatType;
            internal short SeatFrameX, SeatFrameY;
            internal bool Hold, SeatPending, Seated, Reported, AnnouncedSeat, AnnouncedBlocked, AnnouncedWater;
            internal int GearNotice, Candidate, Approach;
            internal uint NextSeat, NextTerrain, NextPondCheck, NextTerrainRequest, NextFreshSearch, BlockedAt, RetryOriginalAt;
            internal Vector2 BlockedFeet;
            internal Point? AwaitingTile;
            internal CompanionFishingWater Water;
            internal bool Scanned;
            internal CompanionFishingWater.Pond Measuring, Pond, RecheckingPond;
            internal readonly List<CompanionFishingWater.Pond> Measured = new List<CompanionFishingWater.Pond>();
            internal CompanionFishingCast Cast;
            internal bool CastConfirmed;
            internal readonly FishingThreats Threats = new FishingThreats();
            internal bool Defending;
            internal uint LastDefenseThreat;
            internal string Status;
            internal readonly Func<int, int, Tile> ReadWaterTile;
            internal FishingActivity(Player at)
            {
                RequestedFeet = FeetGoal = CompanionNativeContact.Feet(at);
                Gravity = at.gravDir;
                ReadWaterTile = ReadTile;
            }

            private Tile ReadTile(int x, int y)
            {
                var tile = ReadFishingTile(x, y);
                if (tile == null) AwaitingTile = new Point(x, y);
                return tile;
            }

            internal void ResetWater(Vector2 center)
            {
                Water = new CompanionFishingWater(center);
                Scanned = false;
                Candidate = 0;
                Measuring = Pond = RecheckingPond = null;
                Measured.Clear();
                Cast = null;
                CastConfirmed = false;
                NextFreshSearch = NextTerrain = NextPondCheck = 0;
                AwaitingTile = null;
            }
        }

        internal bool CanStartFishing(Guid ownerId, Guid id) => CompanionHooks.FishingUnavailable == null &&
            TryFishingActor(ownerId, id, out _) && owner.active && !owner.dead;
        internal bool IsFishing(Guid ownerId, Guid id) => TryFishingActor(ownerId, id, out var actor) && FishingActive(actor);
        private bool TryFishingActor(Guid ownerId, Guid id, out Actor actor) =>
            actors.TryGetValue(id, out actor) && actor.Profile.OwnerId == ownerId && SupplyActorReady(actor);

        internal void SetFishingPage(Guid ownerId, Guid id)
        { fishingPageOwner = ownerId; fishingPageId = id; }

        internal bool SetFishing(Guid ownerId, Guid id, bool enabled)
        {
            if (enabled && CompanionHooks.FishingUnavailable != null)
                return Result(false, "当前原版钓鱼接口不可用。" + CompanionHooks.FishingUnavailable.Message);
            if (!TryFishingActor(ownerId, id, out var actor) || enabled && (!owner.active || owner.dead))
                return Result(false, "当前不能切换钓鱼模式。");
            if (enabled == FishingActive(actor)) return true;
            if (!enabled) return EndFishingActivity(actor, follow: true);
            // Freeze the owner's feet and tile identity in the same input
            // callback. Saving, changing selection, and moving never recapture it.
            var activity = new FishingActivity(owner);
            if (owner.sitting.isSitting)
            {
                Point seat = SeatCell(owner);
                if (ReadFishingTile(seat.X, seat.Y) is Tile tile && owner.sitting.TryGetSittingBlock(owner, out _))
                {
                    activity.Seat = seat;
                    activity.SeatType = tile.type;
                    activity.SeatFrameX = tile.frameX;
                    activity.SeatFrameY = tile.frameY;
                }
            }
            actor.Fishing = activity;
            actor.AutoTeleport?.ResetObservation();
            fishingActors++;
            actor.Behavior.Navigator.IntentChanged(actor.Mobility);
            return Result(true, "");
        }

        private static bool FishingActive(Actor actor) => actor.Fishing != null;
        private static int FishingLiquid(Actor actor) => actor.Fishing?.Pond?.Liquid ?? -1;
        private bool FishingPagePauses(Actor actor) => fishingPageOwner == actor.Profile.OwnerId && fishingPageId == actor.Profile.Id;
        private static bool FishingFollowsOwner(Actor actor) => actor.Fishing == null && actor.Preferences.Follow;

        private bool EndFishingActivity(Actor actor, bool follow = false, bool retire = false)
        {
            if (actor.Fishing == null && !retire) return true;
            var previous = actor.Fishing;
            if (previous != null) { actor.Fishing = null; fishingActors--; }
            if (previous?.Seated == true) StandFromFishingSeat(actor);
            StopFishingLine(actor, retire);
            actor.Behavior.Navigator.IntentChanged(actor.Mobility);
            if (!follow || actor.Preferences.Follow) return true;
            var preferences = actor.Profile.Behavior;
            preferences.Follow = true;
            return SetBehavior(actor.Profile.Id, preferences);
        }

        private void FishingArrivedByWormhole(Actor actor)
        {
            if (actor.Fishing == null) return;
            bool stop = actor.Preferences.TeleportStopsFishing;
            EndFishingActivity(actor, follow: stop);
            if (!stop)
            {
                actor.Fishing = new FishingActivity(actor.Body);
                fishingActors++;
            }
        }

        private static Point SeatCell(Player body) => (body.Bottom - new Vector2(0, 2)).ToTileCoordinates();

        private static bool SameFishingSeat(Player body, Point seat) => body.sitting.isSitting && SeatCell(body) == seat;

        private static void StandFromFishingSeat(Actor actor)
        {
            if (!actor.Body.sitting.isSitting) return;
            using (CompanionHooks.Enter(actor, actor.Body.Center)) actor.Body.sitting.SitUp(actor.Body);
        }

        private void PrepareFishingLocation(Actor actor)
        {
            var activity = actor.Fishing;
            if (activity == null) return;
            var body = actor.Body;
            var feet = CompanionNativeContact.Feet(body);
            activity.SeatPending = false;
            bool hold = false;
            if (activity.Seat is Point seat)
            {
                var tile = ReadFishingTile(seat.X, seat.Y);
                if (tile == null)
                {
                    activity.AwaitingTile = seat;
                    activity.Status = "等待钓位地形";
                }
                else if (!tile.active() || tile.type != activity.SeatType || tile.frameX != activity.SeatFrameX || tile.frameY != activity.SeatFrameY ||
                    !PlayerSittingHelper.GetSittingTargetInfo(body, seat.X, seat.Y, out _, out Vector2 sitting, out _, out _))
                    DropFishingSeat(actor);
                else
                {
                    activity.FeetGoal = new Vector2(sitting.X, activity.RequestedFeet.Y);
                    activity.Seated = SameFishingSeat(body, seat);
                    if (activity.Seated)
                    {
                        hold = true;
                        activity.AnnouncedSeat = false;
                    }
                    else if (body.gravDir > 0 && body.IsWithinSnappngRangeToTile(seat.X, seat.Y, PlayerSittingHelper.ChairSittingMaxDistance) &&
                        body.CanSnapToPosition(sitting - body.Bottom))
                    {
                        hold = true;
                        activity.SeatPending = true;
                        activity.Status = "等待坐下";
                    }
                }
            }

            if (!activity.Seat.HasValue)
            {
                bool horizontal = Math.Abs(feet.X - activity.FeetGoal.X) <= 14f;
                bool elevation = Math.Abs(feet.Y - activity.FeetGoal.Y) <= 16f;
                hold = horizontal && elevation && Math.Abs(body.velocity.X) <= 1.5f &&
                    Math.Abs(body.velocity.Y) <= 1.0f && body.gravDir == activity.Gravity &&
                    !body.pulley && body.grappling[0] < 0 && actor.Behavior.Navigator.Supported;
            }

            if (hold != activity.Hold)
            {
                activity.Hold = hold;
                actor.Behavior.Navigator.IntentChanged(actor.Mobility);
            }
            if (!hold) activity.Status = "正在前往钓位";
            else if (!activity.SeatPending)
            {
                activity.AnnouncedBlocked = false;
                activity.RetryOriginalAt = 0;
                if (activity.Water == null) activity.ResetWater(feet);
            }
        }

        private void DropFishingSeat(Actor actor)
        {
            var activity = actor.Fishing;
            if (activity.Seated) StandFromFishingSeat(actor);
            activity.Seat = null;
            activity.Seated = activity.SeatPending = false;
            activity.FeetGoal = activity.RequestedFeet;
            if (activity.Water != null)
            {
                // This pose no longer owns a usable cast, even when no new
                // pond is found. Retire the old line within the same activity.
                StopFishingLine(actor);
                activity.ResetWater(activity.Water.Center);
            }
            actor.Behavior.Navigator.IntentChanged(actor.Mobility);
        }

        private void ObserveFishingApproach(Actor actor, CompanionMobilityFrame frame)
        {
            var activity = actor.Fishing;
            if (activity == null || activity.Hold || frame.TemporaryGoal) return;
            var navigator = actor.Behavior.Navigator;
            Vector2 feet = CompanionNativeContact.Feet(actor.Body);
            uint now = Main.GameUpdateCount;
            if (activity.RetryOriginalAt != 0 && (unchecked((int)(now - activity.RetryOriginalAt)) >= 0 ||
                Vector2.DistanceSquared(feet, activity.BlockedFeet) > 12 * 12))
            {
                // The short alternatives are approaches to the frozen
                // request. Clearing the obstruction or helping the body
                // must give the original point another normal route search.
                activity.Approach = 0;
                activity.FeetGoal = activity.RequestedFeet;
                activity.RetryOriginalAt = activity.BlockedAt = 0;
                actor.Behavior.Navigator.IntentChanged(actor.Mobility);
                return;
            }
            if (navigator.QueryStatus != NavigationQueryStatus.NoRouteInKnownGraph || navigator.ActionCommitted)
            {
                activity.BlockedAt = 0;
                return;
            }
            if (activity.BlockedAt == 0 || Vector2.DistanceSquared(feet, activity.BlockedFeet) > 12 * 12)
            {
                activity.BlockedAt = now;
                activity.BlockedFeet = feet;
                return;
            }
            if (now - activity.BlockedAt < 90) return;
            // A seated body and a platform section may have different legal
            // contact heights. Try local approaches only after a known dead end.
            if (activity.Seat.HasValue && Vector2.DistanceSquared(feet, activity.RequestedFeet) <= 48 * 48)
            {
                DropFishingSeat(actor);
                activity.BlockedAt = 0;
                return;
            }
            if (!activity.Seat.HasValue && activity.Approach < 4)
            {
                int variant = activity.Approach++;
                activity.FeetGoal = activity.RequestedFeet + (variant < 2 ? new Vector2(variant == 0 ? -20 : 20, 0) :
                    new Vector2(0, (variant == 2 ? 20 : 40) * activity.Gravity));
                activity.BlockedAt = 0;
                actor.Behavior.Navigator.IntentChanged(actor.Mobility);
                return;
            }
            activity.Status = "等待协助前往钓位";
            if (!activity.Seat.HasValue && activity.RetryOriginalAt == 0) activity.RetryOriginalAt = now + 180;
            if (!activity.AnnouncedBlocked && SayFishing(actor, "我过不去，帮我一下")) activity.AnnouncedBlocked = true;
        }

        private void AfterFishingBody(Actor actor)
        {
            var activity = actor.Fishing;
            if (activity == null || !activity.SeatPending || !(activity.Seat is Point seat) || actor.Body.dead) return;
            uint now = Main.GameUpdateCount;
            if (unchecked((int)(now - activity.NextSeat)) < 0) return;
            var body = actor.Body;
            if (body.controlLeft || body.controlRight || body.controlDown || body.controlUp || body.controlJump || body.controlUseItem ||
                body.pulley || body.CCed || body.noItems || !FreeHand(body)) return;
            activity.NextSeat = now + 15;
            bool ownerOccupies = SameFishingSeat(owner, seat);
            if (ownerOccupies || Main.sittingManager == null || Main.sittingManager.GetNextPlayerStackIndexInCoords(seat) >= 2)
            {
                activity.Status = "等座位空出";
                if (!activity.AnnouncedSeat && SayFishing(actor, "椅子让一下，我要坐")) activity.AnnouncedSeat = true;
                return;
            }
            if (ReadFishingTile(seat.X, seat.Y) is Tile tile && tile.active() && tile.type == activity.SeatType &&
                tile.frameX == activity.SeatFrameX && tile.frameY == activity.SeatFrameY &&
                body.IsWithinSnappngRangeToTile(seat.X, seat.Y, PlayerSittingHelper.ChairSittingMaxDistance))
            {
                // The original chair interaction uses this same method and
                // range. Run after UpdateSitting so the native stack records
                // this body only once in the current gameplay step.
                using (CompanionHooks.Enter(actor, seat.ToWorldCoordinates())) body.sitting.SitDown(body, seat.X, seat.Y);
                activity.Seated = SameFishingSeat(body, seat);
            }
        }

        private static Tile ReadFishingTile(int x, int y)
        {
            if (Main.tile == null || x < 1 || y < 1 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1 ||
                Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y))) return null;
            return Main.tile[x, y];
        }

        private void AdvanceFishingSearch(Actor actor)
        {
            var activity = actor.Fishing;
            if (!activity.Hold || activity.SeatPending || activity.Water == null) return;
            var water = activity.Water;
            uint now = Main.GameUpdateCount;
            if (activity.NextFreshSearch != 0)
            {
                if (unchecked((int)(now - activity.NextFreshSearch)) < 0) return;
                activity.ResetWater(water.Center);
                water = activity.Water;
            }
            if (activity.AwaitingTile is Point pending)
            {
                if (ReadFishingTile(pending.X, pending.Y) == null)
                {
                    activity.Status = "等待水域地形";
                    return;
                }
                activity.AwaitingTile = null;
            }

            int budget = CompanionFishingWater.ScanBudget;
            if (activity.Cast?.Found == true)
            {
                if (!RecheckFishingWater(activity, now, ref budget))
                {
                    // Finish the original native line normally before any
                    // other pond earns a new cast within the original circle.
                    StopFishingLine(actor);
                    activity.ResetWater(water.Center);
                    activity.Status = "正在重新寻找可钓水域";
                }
                return;
            }
            FishingNativeBait(actor.Body, out int baitPower, out _);
            if (baitPower <= 0) return;
            activity.Status = "正在寻找可钓水域";
            if (!activity.Scanned)
            {
                if (!TryFishingRod(actor, -1, out _)) return;
                if (water.Advance(ref budget, activity.ReadWaterTile) != CompanionFishingWater.Result.Ready) return;
                activity.Scanned = true;
            }
            while (activity.Candidate < water.Entries.Count && budget > 0)
            {
                var entry = water.Entries[activity.Candidate];
                if (activity.Pond == null)
                {
                    foreach (var known in activity.Measured)
                        if (known.Contains(entry.Cell.X, entry.Cell.Y)) { activity.Pond = known; break; }
                    if (activity.Pond == null)
                    {
                        var measuring = activity.Measuring ?? (activity.Measuring = new CompanionFishingWater.Pond(entry.Cell));
                        if (measuring.Advance(ref budget, activity.ReadWaterTile) != CompanionFishingWater.Result.Ready) return;
                        activity.Measured.Add(measuring);
                        activity.Pond = measuring;
                        activity.Measuring = null;
                    }
                }
                if (activity.Pond.Eligible && TryFishingRod(actor, activity.Pond.Liquid, out var rod))
                {
                    if (activity.Cast == null || !activity.Cast.Matches(actor.Body, rod))
                        activity.Cast = new CompanionFishingCast(actor.Body, rod, entry);
                    if (activity.Cast.Advance(activity.ReadWaterTile) != CompanionFishingWater.Result.Ready) return;
                    if (activity.Cast.Found)
                    {
                        if (activity.Seated && activity.SeatType != 89 && activity.SeatType != 102 && actor.Body.direction != activity.Cast.Facing)
                        {
                            DropFishingSeat(actor);
                            SayFishing(actor, "这张椅子的朝向不好抛竿，我在旁边站着钓。");
                            return;
                        }
                        activity.NextTerrain = now + 90;
                        activity.NextPondCheck = now + 120;
                        activity.Status = "正在钓鱼";
                        return;
                    }
                    // A trajectory slice has its own cap. Do not start a
                    // second candidate's trajectory on the same granted turn.
                    activity.Candidate++;
                    activity.Cast = null;
                    activity.Pond = null;
                    return;
                }
                activity.Candidate++;
                activity.Cast = null;
                activity.Pond = null;
                budget--;
            }
            if (activity.Candidate < water.Entries.Count) return;
            activity.Status = "附近没有可钓水域";
            activity.NextFreshSearch = now + 300;
            if (!activity.AnnouncedWater && SayFishing(actor, "附近20格没有可达的能钓鱼的水域")) activity.AnnouncedWater = true;
        }

        private static bool RecheckFishingWater(FishingActivity activity, uint now, ref int budget)
        {
            if (unchecked((int)(now - activity.NextTerrain)) >= 0)
            {
                activity.NextTerrain = now + 90;
                if (!activity.Cast.TerrainCurrent(activity.ReadWaterTile))
                {
                    if (activity.AwaitingTile.HasValue) activity.NextTerrain = now;
                    else return false;
                    return true;
                }
            }
            if (unchecked((int)(now - activity.NextPondCheck)) < 0) return true;
            var measuring = activity.RecheckingPond ?? (activity.RecheckingPond = new CompanionFishingWater.Pond(activity.Cast.Entry.Cell));
            if (measuring.ValidateMinimum(ref budget, activity.ReadWaterTile) != CompanionFishingWater.Result.Ready) return true;
            activity.RecheckingPond = null;
            activity.NextPondCheck = now + 120;
            return measuring.Eligible;
        }

        private bool RequestFishingTerrain()
        {
            if (fishingActors == 0) return false;
            uint now = Main.GameUpdateCount;
            foreach (var actor in actors.Values)
            {
                var activity = actor.Fishing;
                if (activity?.AwaitingTile is Point tile && unchecked((int)(now - activity.NextTerrainRequest)) >= 0)
                {
                    activity.NextTerrainRequest = now + 20;
                    if (TryRequestTerrain(tile.X, tile.Y, actor.Body.team)) return true;
                }
            }
            return false;
        }

        private bool TryFishingAim(Actor actor, Item rod, out Vector2 aim)
        {
            var activity = actor.Fishing;
            var cast = activity?.Cast;
            aim = default;
            if (activity == null || !activity.Hold || activity.SeatPending || activity.AwaitingTile.HasValue ||
                activity.RecheckingPond != null || cast?.Found != true) return false;
            if (!cast.Matches(actor.Body, rod))
            {
                activity.Cast = null;
                activity.CastConfirmed = false;
                return false;
            }
            aim = cast.Aim;
            activity.CastConfirmed = false;
            return true;
        }

        private void FishingCastUnconfirmed(Actor actor)
        {
            var activity = actor.Fishing;
            if (activity == null || activity.CastConfirmed) return;
            activity.Cast?.Reject();
        }

        private bool FishingEnteredLiquid(Actor actor, Projectile bobber)
        {
            var activity = actor.Fishing;
            if (activity?.Pond == null || activity.Cast?.Found != true || !bobber.wet || bobber.shimmerWet) return false;
            var cell = bobber.Center.ToTileCoordinates();
            int row = activity.Pond.Row;
            if (Math.Abs(cell.Y - row) > 1 || !activity.Pond.Contains(cell.X, row)) return false;
            var tile = ReadFishingTile(cell.X, row);
            if (!CompanionFishingWater.MeasureTile(tile)) return false;
            int wetType = bobber.lavaWet ? 1 : bobber.honeyWet ? 2 : 0;
            if (wetType != activity.Pond.Liquid || !CompanionFishingWater.WithinCircle(activity.Water.Center,
                new Vector2(bobber.Center.X, (row + 1) * 16 - tile.liquid / 16))) return false;
            activity.CastConfirmed = true;
            activity.AnnouncedWater = false;
            if (!activity.Reported && SayFishing(actor, FishingLocationText(actor.Body, wetType))) activity.Reported = true;
            return true;
        }

        private void FishingNeedsGear(Actor actor, bool rod, bool bait)
        {
            var activity = actor.Fishing;
            if (activity == null || !activity.Hold || activity.SeatPending) return;
            int notice = rod ? 1 : bait ? 2 : 0;
            if (notice == 0) { activity.GearNotice = 0; return; }
            activity.Status = rod ? "缺少鱼竿" : "缺少可用鱼饵";
            if (activity.GearNotice != notice && SayFishing(actor, rod ? "我需要一根鱼竿。" : "我需要可用的鱼饵。")) activity.GearNotice = notice;
        }

        private static string FishingLocationText(Player body, int liquid)
        {
            var zones = new List<string>(4);
            if (body.ZoneSnow) zones.Add("雪地");
            if (body.ZoneJungle) zones.Add("丛林");
            if (body.ZoneDesert) zones.Add("沙漠");
            if (body.ZoneHallow) zones.Add("神圣之地");
            if (body.ZoneCorrupt) zones.Add("腐化之地");
            if (body.ZoneCrimson) zones.Add("猩红之地");
            if (body.ZoneGlowshroom) zones.Add("发光蘑菇地");
            if (body.ZoneBeach) zones.Add("海洋");
            if (body.ZoneDungeon) zones.Add("地牢");
            if (zones.Count == 0 && body.ZoneOverworldHeight) zones.Add("森林");
            string depth = body.ZoneUnderworldHeight ? "地狱" : body.ZoneRockLayerHeight ? "洞穴" :
                body.ZoneDirtLayerHeight ? "地下" : body.ZoneSkyHeight ? "太空" : "地表";
            if (zones.Count == 0 || zones.Count > 1 || zones[0] != "森林") zones.Add(depth);
            return "这里是" + String.Join("、", zones) + "，正在" + (liquid == 1 ? "熔岩" : liquid == 2 ? "蜂蜜" : "水") + "中钓鱼。";
        }

        private static bool SayFishing(Actor actor, string message)
        {
            if (Main.netMode == 0)
            {
                ChatHelper.DisplayMessage(NetworkText.FromLiteral(message), Color.White, (byte)actor.Body.whoAmI);
                return true;
            }
            if (Main.netMode != 1 || actor.Connection == null || actor.Connection.Finished) return false;
            // Text modules bypass SendData. The actor's own transport gets an
            // ordinary Say request and the native server labels the sender.
            var packet = NetTextModule.SerializeClientMessage(new ChatMessage(message));
            try
            {
                packet.ShrinkToFit();
                var frame = new byte[packet.Length];
                Buffer.BlockCopy(packet.Buffer.Data, 0, frame, 0, frame.Length);
                return actor.Connection.TrySend(frame);
            }
            catch (IOException) { return false; }
            finally { packet.Recycle(); }
        }
    }
}
