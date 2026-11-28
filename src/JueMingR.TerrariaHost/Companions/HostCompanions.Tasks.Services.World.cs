using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const int NaturalEventReachPixels = 28 * 16;

        private sealed class ServiceSite
        {
            internal TargetTile Target;
            internal ArrivalSearch Arrival;
        }

        private sealed class EventUse
        {
            internal ItemOnce Hand;
            internal CompanionTaskOutcome HandReceipt;
            internal ServiceSite Site;
            internal uint Started;
            internal int InitialQuantity;
        }

        partial void ArmLacewingEventCombat(Actor actor, NPC target, ref TaskCombat intent);

        private sealed class NaturalEventUse
        {
            internal NPC Npc;
            internal int Slot, Generation;
            internal TaskCombat Combat;
            internal ArrivalSearch Arrival;
            internal bool SawReport;
        }

        private static readonly Func<int, int, bool> IsSpawnHalfOfBed = (Func<int, int, bool>)Delegate.CreateDelegate(
            typeof(Func<int, int, bool>), AccessTools.Method(typeof(Player), "IsHoveringOverABottomSideOfABed"));
        private static readonly AccessTools.FieldRef<Stack<Point>> HousingStack =
            AccessTools.StaticFieldRefAccess<Stack<Point>>(AccessTools.Field(typeof(WorldGen), "_roomCheckStack"));
        private static readonly string[] HousingFlagNames =
        {
            "roomTorch", "roomDoor", "roomChair", "roomTable", "roomHasStinkbug", "roomHasEchoStinkbug"
        };
        private static readonly AccessTools.FieldRef<bool>[] HousingFlags = HousingFlagNames.Select(flag =>
            AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(WorldGen), flag))).ToArray();

        partial void RegisterOtherServiceTasks()
        {
            var target = new TaskField("target", min: 1, max: 64);
            RegisterTaskObservation("housing_status",
                "检查 observe_tiles 的同世界室内点或消息发送位置。用原版封闭墙体、空间及家具规则诊断；不改房和居住者。",
                ReadTaskHousing, target);
            RegisterTaskOperation("use_furniture", "走进已观察家具的原版范围，坐下、睡觉、设置该队友的床位或起身。",
                UseTaskFurniture, CancelTravelTask, target, new TaskField("action", required: true, min: 1, max: 16,
                    options: new[] { "sit", "sleep", "set_spawn", "stand" }));
            var naturalEvent = new TaskField("event", min: 1, max: 12, options: new[] { "martian", "empress" });
            RegisterTaskObservation("event_status",
                "读取已观测首领、入侵和当前事件。可指定原版召唤物，或用 event=martian/empress 读取可用的实际 NPC 引用。",
                ObserveTaskEvent, naturalEvent, new TaskField("item_type", "integer", min: 1, max: ItemID.Count - 1));
            RegisterTaskOperation("trigger_event",
                "只在玩家明确要求开启时使用。用队友的原版召唤物，核对真实首领/世界事件；撒旦军队须给出已经观察到的水晶台 target。",
                TriggerTaskEvent, CancelTaskEvent, target, naturalEvent, new TaskField("npc", min: 1, max: 64),
                new TaskField("item_type", "integer", min: 1, max: ItemID.Count - 1),
                new TaskField("slot", "integer", min: 0, max: CompanionBehavior.MainInventorySlots - 1));
        }

        private bool TryServiceTile(TaskExecution execution, bool allowHere, out TargetTile target)
        {
            target = default(TargetTile);
            string reference = execution.Text("target");
            if (reference != null)
                return execution.Actor.TaskTiles != null && execution.Actor.TaskTiles.TryGetValue(reference, out target);
            if (!allowHere || !execution.Anchor.Valid) return false;
            Point point = execution.Anchor.Feet.ToTileCoordinates();
            if (!CompanionNativeContact.Loaded(point.X, point.Y)) return false;
            target = new TargetTile(point, new TileFact(Main.tile[point.X, point.Y]));
            return true;
        }

        private CompanionTaskOutcome ReachServiceTile(Actor actor, ServiceSite site, int snapRange = 0)
        {
            Point point = site.Target.Point;
            var body = actor.Body;
            if (!CompanionNativeContact.Loaded(point.X, point.Y)) return TaskOutcome("waiting", "等该家具所在区段加载。");
            Tile current = Main.tile[point.X, point.Y];
            if (!current.active() || current.inActive() || current.type != site.Target.Fact.Type ||
                current.frameX != site.Target.Fact.FrameX || current.frameY != site.Target.Fact.FrameY)
                return TaskOutcome("failed", "原来的家具状态或位置已变化。请重新观察目标。");
            if (ActorTileReachReady(body) && body.IsInTileInteractionRange(point.X, point.Y, ActorTileReach(body)) &&
                (snapRange == 0 || body.IsWithinSnappngRangeToTile(point.X, point.Y, snapRange))) return null;
            if (site.Arrival == null)
            {
                if (actor.Fishing != null) EndFishingActivity(actor);
                SetTaskTravel(actor, null);
                site.Arrival = new ArrivalSearch
                { X = point.X, Y = point.Y + 1, HorizontalRadius = 3, VerticalRadius = 3, Name = "家具位置" };
            }
            if (actor.TaskTravel == null)
            {
                if (!FindTaskFooting(actor, site.Arrival, out var feet, out bool exhausted))
                    return exhausted ? TaskOutcome("failed", "家具附近没有找到合适的落脚点。") :
                        TaskOutcome("waiting", "正在等家具附近的已知地形。");
                SetTaskTravel(actor, new TaskTravel { Feet = feet, Name = site.Arrival.Name });
            }
            if (actor.TaskTravel.Arrived && !actor.TaskTravel.Temporary && ActorTileReachReady(body))
                return TaskOutcome("failed", "已到目标附近，仍不在家具的原版使用范围。");
            return TaskOutcome("running", "正在前往家具。");
        }

        private CompanionTaskOutcome UseTaskFurniture(TaskExecution execution)
        {
            string action = execution.Text("action");
            var actor = execution.Actor;
            var body = actor.Body;
            if (action == "stand")
            {
                using (CompanionHooks.Enter(actor, actor.Aim))
                { body.sitting.SitUp(body); body.sleeping.StopSleeping(body); }
                SetTaskTravel(actor, null);
                return TaskOutcome(!body.sitting.isSitting && !body.sleeping.isSleeping ? "succeeded" : "unconfirmed",
                    "已核对队友的坐卧状态。", 1);
            }
            if (!(execution.State is ServiceSite site))
            {
                if (!TryServiceTile(execution, false, out var target) || !target.Fact.Active)
                    return TaskOutcome("failed", "请先用地形观察选定对应的实际家具。");
                execution.State = site = new ServiceSite { Target = target };
            }
            Point point = site.Target.Point;
            int x = point.X, y = point.Y;
            var reached = ReachServiceTile(actor, site, action == "sit" ? 40 : action == "sleep" ? 96 : 0);
            if (reached != null) return reached;
            if (!TileObjectData.TryGetTileBounds(x, y, out Rectangle bounds) ||
                !WorldTileKnowledge.RegionKnown(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom))
                return TaskOutcome("waiting", "等待原版家具的完整多格结构资料。");
            if (!FishingHandFree(body)) return TaskOutcome("waiting", "正在等待队友手部空闲。");
            var tile = Main.tile[x,y];
            bool seat = action == "sit";
            if (seat && !TileID.Sets.CanBeSatOnForPlayers[tile.type] ||
                !seat && (tile.type != TileID.Beds || IsSpawnHalfOfBed(x, y) != (action == "set_spawn")))
                return TaskOutcome("failed", seat ? "所选家具不能坐。" : "请选择床的对应一侧。枕头端睡觉，另一端设置出生点。");
            if (action == "set_spawn")
            {
                int spawnX = x - tile.frameX / 18 + (tile.frameX >= 72 ? 5 : 2);
                int spawnY = y - tile.frameY / 18 % 2 + 2;
                if (!HousingFootprintLoaded(spawnX, spawnY - 1))
                    return TaskOutcome("waiting", "床位周围的住房地形尚未完整加载。");
                bool valid = ReadHousing(() =>
                {
                    var floor = Main.tile[spawnX, spawnY - 1];
                    if (floor?.active() != true || floor.type != TileID.Beds) return false;
                    // Match native CheckSpawn's temporary solidity while suppressing its human-facing text.
                    bool solid = Main.tileSolid[TileID.Bubble];
                    Main.tileSolid[TileID.Bubble] = true;
                    try
                    {
                        for (int xx = spawnX - 1; xx <= spawnX + 1; xx++)
                            for (int yy = spawnY - 3; yy < spawnY; yy++)
                            {
                                var cell = Main.tile[xx,yy];
                                if (cell == null || cell.nactive() && Main.tileSolid[cell.type] && !Main.tileSolidTop[cell.type])
                                    return false;
                            }
                        return WorldGen.StartRoomCheck(spawnX, spawnY - 1, NoRoomCheckFeedback.WithoutText);
                    }
                    finally { Main.tileSolid[TileID.Bubble] = solid; }
                });
                if (!valid) return TaskOutcome("failed", "原版床位需要封闭且出生空间没有阻挡。");
                body.ChangeSpawn(spawnX, spawnY);
                actor.Dirty = true;
                return TaskOutcome(body.SpawnX == spawnX && body.SpawnY == spawnY ? "succeeded" : "unconfirmed",
                    "已核对这名队友在当前世界自己的床位。", 1, spawnX + "," + spawnY);
            }

            using (CompanionHooks.Enter(actor, actor.Aim))
            {
                if (seat)
                {
                    if (!PlayerSittingHelper.GetSittingTargetInfo(body, x, y, out _, out var anchor, out _, out _))
                        return TaskOutcome("failed", "原版未找到该家具的就座位置。");
                    if (!body.sitting.isSitting || body.Bottom != anchor) body.sitting.SitDown(body, x, y);
                    if (body.sitting.isSitting && body.Bottom == anchor)
                        return HoldTaskFurniture(execution, "队友已实际坐在指定座位。");
                }
                else
                {
                    if (!PlayerSleepingHelper.GetSleepingTargetInfo(x, y, out _, out var anchor, out _))
                        return TaskOutcome("failed", "原版未找到该床的睡眠位置。");
                    if (!body.sleeping.isSleeping || body.Bottom != anchor) body.sleeping.StartSleeping(body, x, y);
                    if (body.sleeping.isSleeping && body.Bottom == anchor)
                        return HoldTaskFurniture(execution, "队友已实际在指定床上睡下。");
                }
            }
            return TaskOutcome("failed", "原版未能使用家具，可能已有占用或接近姿态不合适。");
        }

        private CompanionTaskOutcome HoldTaskFurniture(TaskExecution execution, string detail)
        {
            var actor = execution.Actor;
            // Native seating moves the body a few pixels. Keep the resulting
            // position through normal task movement ownership so trying to return
            // to the old standby anchor does not immediately generate a stand input.
            actor.TaskControlId = execution.Job.Id;
            SetTaskTravel(actor, new TaskTravel { Feet = CompanionNativeContact.Feet(actor.Body), Arrived = true });
            return TaskOutcome("succeeded", detail, 1);
        }

        private CompanionTaskOutcome ReadTaskHousing(TaskExecution execution)
        {
            if (!TryServiceTile(execution, true, out var target))
                return TaskOutcome("failed", "没有已观察或发送时锁定的室内位置。");
            if (!HousingFootprintLoaded(target.Point.X, target.Point.Y))
                return TaskOutcome("waiting", "室内周围地形还没有完全加载。");
            return ReadHousing(() =>
            {
                if (!WorldGen.StartRoomCheck(target.Point.X, target.Point.Y, NoRoomCheckFeedback.WithoutText))
                    return TaskOutcome("succeeded", "原版房间结构检查未通过：" + HousingFailureText(WorldGen.roomCheckFailureReason.ToString()), value: "false");
                bool ready = WorldGen.RoomNeeds();
                var missing = new[] { "照明", "入口", "座椅", "桌面" }.Where((_, index) => !HousingFlags[index]());
                return TaskOutcome("succeeded", ready ? "原版结构和必要家具检查均通过。未改变房间归属。" :
                    "原版房间结构通过，仍缺：" + String.Join("、", missing) + "。", value: ready ? "true" : "false");
            });
        }

        private static string HousingFailureText(string reason)
        {
            switch (reason)
            {
                case "TooCloseToWorldEdge": return "太靠近世界边界。";
                case "RoomCheckStartedInASolidTile": return "检查点位于实心方块。";
                case "RoomIsTooBig": return "房间太大。";
                case "RoomIsTooSmall": return "房间太小。";
                case "HoleInWallIsTooBig": return "背景墙有过大的缺口。";
                case "TooManyUnsafeWalls": return "缺少足够安全的背景墙。";
                default: return reason;
            }
        }

        private static bool HousingFootprintLoaded(int x, int y)
        {
            int edge = WorldGen.maxRoomSize + 2;
            return x > 10 && y > 10 && x < Main.maxTilesX - 10 && y < Main.maxTilesY - 10 &&
                WorldTileKnowledge.RegionKnown(Math.Max(0, x - edge), Math.Max(0, y - edge),
                    Math.Min(Main.maxTilesX - 1, x + edge), Math.Min(Main.maxTilesY - 1, y + edge));
        }

        private static T ReadHousing<T>(Func<T> read)
        {
            // The native housing query reuses process-wide scratch fields.
            // Borrow fresh scratch on this synchronous game-thread operation.
            var tiles = WorldGen.roomTiles;
            var stack = HousingStack();
            var types = WorldGen.houseTile;
            int x1 = WorldGen.roomX1, x2 = WorldGen.roomX2, y1 = WorldGen.roomY1, y2 = WorldGen.roomY2, count = WorldGen.numRoomTiles;
            var reason = WorldGen.roomCheckFailureReason;
            bool canSpawn = WorldGen.canSpawn;
            var flags = HousingFlags.Select(field => field()).ToArray();
            try
            {
                WorldGen.roomTiles = new BitSet2D();
                HousingStack() = new Stack<Point>();
                WorldGen.houseTile = new bool[TileID.Count];
                return read();
            }
            finally
            {
                WorldGen.roomTiles = tiles;
                HousingStack() = stack;
                WorldGen.houseTile = types;
                WorldGen.roomX1 = x1; WorldGen.roomX2 = x2; WorldGen.roomY1 = y1; WorldGen.roomY2 = y2;
                WorldGen.numRoomTiles = count; WorldGen.roomCheckFailureReason = reason; WorldGen.canSpawn = canSpawn;
                for (int i = 0; i < flags.Length; i++) HousingFlags[i]() = flags[i];
            }
        }

        private static string EventLabel(int item)
        {
            switch (item)
            {
                case ItemID.DD2ElderCrystal: return "撒旦军队";
                case ItemID.PumpkinMoonMedallion: return "南瓜月";
                case ItemID.NaughtyPresent: return "霜月";
                case ItemID.BloodMoonStarter: return "血月";
                case ItemID.GoblinBattleStandard: return "哥布林入侵";
                case ItemID.SnowGlobe: return "雪人军团";
                case ItemID.PirateMap: return "海盗入侵";
                case ItemID.QueenSlimeCrystal: return "史莱姆皇后";
                case ItemID.TruffleWorm: return "猪鲨";
                case ItemID.EmpressButterfly: return "光之女皇";
                case ItemID.SuspiciousLookingEye: return "克苏鲁之眼";
                case ItemID.WormFood: return "世界吞噬怪";
                case ItemID.BloodySpine: return "克苏鲁之脑";
                case ItemID.Abeemination: return "蜂王";
                case ItemID.DeerThing: return "独眼巨鹿";
                case ItemID.SlimeCrown: return "史莱姆王";
                case ItemID.MechanicalWorm: return "毁灭者";
                case ItemID.MechanicalEye: return "双子魔眼";
                case ItemID.MechanicalSkull: return "机械骷髅王";
                case ItemID.CelestialSigil: return "月亮领主";
                default: return null;
            }
        }

        private bool RealEventOngoing(int item)
        {
            switch (item)
            {
                case ItemID.DD2ElderCrystal: return DD2Event.Ongoing || LiveServiceNpc(NPCID.DD2EterniaCrystal);
                case ItemID.PumpkinMoonMedallion: return Main.pumpkinMoon;
                case ItemID.NaughtyPresent: return Main.snowMoon;
                case ItemID.BloodMoonStarter: return Main.bloodMoon;
                case ItemID.GoblinBattleStandard: return Main.invasionType == InvasionID.GoblinArmy;
                case ItemID.SnowGlobe: return Main.invasionType == InvasionID.SnowLegion;
                case ItemID.PirateMap: return Main.invasionType == InvasionID.PirateInvasion;
                case ItemID.QueenSlimeCrystal: return LiveServiceNpc(NPCID.QueenSlimeBoss);
                case ItemID.TruffleWorm: return LiveServiceNpc(NPCID.DukeFishron);
                case ItemID.EmpressButterfly: return LiveServiceNpc(NPCID.HallowBoss);
                case ItemID.SuspiciousLookingEye: return LiveServiceNpc(NPCID.EyeofCthulhu);
                case ItemID.WormFood: return LiveServiceNpc(NPCID.EaterofWorldsHead);
                case ItemID.BloodySpine: return LiveServiceNpc(NPCID.BrainofCthulhu);
                case ItemID.Abeemination: return LiveServiceNpc(NPCID.QueenBee);
                case ItemID.DeerThing: return LiveServiceNpc(NPCID.Deerclops);
                case ItemID.SlimeCrown: return LiveServiceNpc(NPCID.KingSlime);
                case ItemID.MechanicalWorm: return LiveServiceNpc(NPCID.TheDestroyer);
                case ItemID.MechanicalEye: return LiveServiceNpc(NPCID.Retinazer) || LiveServiceNpc(NPCID.Spazmatism);
                case ItemID.MechanicalSkull: return LiveServiceNpc(NPCID.SkeletronPrime);
                case ItemID.CelestialSigil: return LiveServiceNpc(NPCID.MoonLordCore);
                default: return false;
            }
        }

        private bool LiveServiceNpc(int type) => (TaskNpcTable?.Any(npc => npc?.type == type && TaskNpcObserved(npc)) ?? false) ||
            Main.netMode == 1 && Main.npc.Any(npc => npc?.active == true && npc.type == type);

        private CompanionTaskOutcome ObserveTaskEvent(TaskExecution execution)
        {
            int type = execution.Number("item_type");
            string natural = execution.Text("event");
            if (natural != null)
            {
                if (type != 0) return TaskOutcome("rejected", "请仅选择召唤物或自然事件其中一种入口。");
                return ObserveNaturalEvent(execution, natural);
            }
            if (type == 0)
                return TaskOutcome("succeeded", (Main.hardMode ? "困难模式" : "困难前") + " ｜ " + (Main.dayTime ? "白天" : "夜晚") +
                    " ｜史莱姆皇后用明胶水晶；光之女皇需实际七彩草蛉；猪鲨使用 summon_duke 指定实际海域，以队友松露虫为饵；撒旦军队用水晶台和水晶；" +
                    "南瓜月、霜月与血月需其召唤品和夜间。火星事件 " +
                    (Main.invasionType == InvasionID.MartianMadness ? "已发生。" : "尚未发生，需自然出现的火星探测器发现玩家后逃脱。"));
            string name = EventLabel(type);
            if (name == null) return TaskOutcome("failed", "暂未找到该物品对应的原版事件入口。");
            bool active = RealEventOngoing(type);
            if (type == ItemID.CelestialSigil && !active && NPC.MoonLordCountdown > 0)
                return TaskOutcome("succeeded", "月亮领主的原版召唤倒计时已经开始，尚未观察到首领出现。", value: "false");
            return TaskOutcome("succeeded", name + (active ? "已在当前世界发生。" : "未在当前观测内发生。") +
                " 队友主背包对应物品 " + CountTaskInventory(execution.Actor, type) + " 个。" +
                (type == ItemID.TruffleWorm ? " 玩家明确委托时使用 summon_duke 指定实际海域。" : ""), value: active ? "true" : "false");
        }

        private CompanionTaskOutcome TriggerTaskEvent(TaskExecution execution)
        {
            var actor = execution.Actor;
            int type = execution.Number("item_type");
            string natural = execution.Text("event");
            if (natural != null)
            {
                if (type != 0) return TaskOutcome("rejected", "请仅选择召唤物或自然事件其中一种入口。");
                return TriggerNaturalEvent(execution, natural);
            }
            string name = EventLabel(type);
            if (name == null) return TaskOutcome("failed", "此物品不是已核对的原版首领或事件召唤入口。");
            if (type == ItemID.TruffleWorm) return TaskOutcome("blocked", "请在玩家明确委托下使用 summon_duke 指定实际海域，以队友松露虫为饵。");
            if (type == ItemID.EmpressButterfly) return TaskOutcome("blocked", "请先用 event_status 查询七彩草蛉，再指定 event=empress 以及它的实际 NPC 引用。");
            if (!(execution.State is EventUse work))
            {
                if (RealEventOngoing(type)) return TaskOutcome("failed", name + "已发生，本次不重复召唤。");
                execution.State = work = new EventUse();
            }
            if (execution.Issued)
            {
                int used = Math.Max(0, work.InitialQuantity - CountTaskInventory(actor, type));
                if (RealEventOngoing(type) && (used > 0 || work.HandReceipt?.State == "succeeded"))
                    return TaskOutcome("succeeded", "已实际观察到" + name + "生效；对应物品净减少 " + used + " 个。", 1);
                if (work.HandReceipt?.State == "cancelled" && used == 0)
                    return TaskOutcome("failed", work.HandReceipt.Detail);
                // The original Celestial Sigil starts a 720-tick countdown before there is a boss to confirm.
                if (Main.GameUpdateCount - work.Started > (type == ItemID.CelestialSigil ? 1080u : 360u))
                    return TaskOutcome(used > 0 ? "unconfirmed" : "failed", used > 0
                        ? "对应物品已减少，但未核到" + name + "生效，不重发。" : "未见物品消耗或" + name + "生效，请检查原版时间、群系和进度条件。");
                return TaskOutcome("running", "已交给原版动作，正等" + name + "的实际结果。");
            }
            if (work.Hand != null)
                return work.HandReceipt ?? TaskOutcome("running", "召唤物已排队，尚未进入原版用物。");
            if (RealEventOngoing(type) || type == ItemID.CelestialSigil && NPC.MoonLordCountdown > 0)
                return TaskOutcome("failed", name + "已出现或已开始进入世界，本次未另用物品。");
            int slot = FindServiceItem(actor, type, execution.Number("slot", -1), 1);
            if (slot < 0) return TaskOutcome("failed", "队友没有指定的可用召唤资源。");
            if (type == ItemID.DD2ElderCrystal) return StartTaskCrystal(execution, work, slot);
            if (ManualHandBusy(actor)) return TaskOutcome("waiting", "正在等待队友的原生手部动作结束。");
            if (!CanUseItemOnce(actor.Profile.Id, CompanionInventoryArea.Inventory, slot, out string reason) ||
                !TryQueueItemOnce(actor, slot, false, out reason))
                return TaskOutcome("failed", reason ?? "原版未接纳该物品。");
            work.InitialQuantity = CountTaskInventory(actor, type);
            work.Hand = actor.Supplies.Once;
            work.Hand.Task = execution;
            work.Hand.ChatActivation = execution.Job.Identity.Activation;
            work.Hand.ChatDataEpoch = execution.Job.Identity.DataEpoch;
            work.Hand.TaskStarted = () => work.Started = Main.GameUpdateCount;
            work.Hand.ChatComplete = (state, detail) =>
            {
                work.HandReceipt = TaskOutcome(state, detail);
                if (!execution.Issued || ReferenceEquals(execution.Job.Current, execution)) return;
                int used = Math.Max(0, work.InitialQuantity - CountTaskInventory(actor, type));
                execution.Settle(RealEventOngoing(type) && (used > 0 || state == "succeeded")
                    ? TaskOutcome("succeeded", "已实际观察到" + name + "生效；对应物品净减少 " + used + " 个。", 1)
                    : TaskOutcome("unconfirmed", name + "用物已结算，但事件效果未确认。" + detail));
            };
            return TaskOutcome("running", "已受理一次原版召唤用物。");
        }

        private CompanionTaskOutcome StartTaskCrystal(TaskExecution execution, EventUse work, int slot)
        {
            var actor = execution.Actor;
            if (!ShopBuyerReady(actor)) return TaskOutcome("failed", "当前没有队友自己的有效水晶台结算连接。");
            if (work.Site == null)
            {
                if (!TryServiceTile(execution, false, out var target) || !target.Fact.Active || target.Fact.Type != TileID.ElderCrystalStand)
                    return TaskOutcome("failed", "必须指定 observe_tiles 看到的实际永恒水晶座。");
                work.Site = new ServiceSite { Target = target };
            }
            var approach = ReachServiceTile(actor, work.Site);
            if (approach != null) return approach;
            Point point = work.Site.Target.Point;
            int x = point.X, y = point.Y + Math.Max(0, 3 - Main.tile[point.X,point.Y].frameY / 18);
            if (!WorldGen.InWorld(x, y, 8) || !WorldTileKnowledge.RegionKnown(Math.Max(0, x - 126), Math.Max(0, y - 20),
                    Math.Min(Main.maxTilesX - 1, x + 126), Math.Min(Main.maxTilesY - 1, y + 20)))
                return TaskOutcome("waiting", "正在等台座两侧场地资料。");
            if (!Main.tile[x,y].active() || Main.tile[x,y].type != TileID.ElderCrystalStand ||
                Main.pumpkinMoon || Main.snowMoon || DD2Event.WouldFailSpawningHere(x, y))
                return TaskOutcome("failed", "原版水晶台空间不足或已有互斥事件，本次未使用水晶。");
            if (!FishingHandFree(actor.Body)) return TaskOutcome("waiting", "正在等队友双手空闲。");
            execution.MarkSubmitted();
            work.Started = Main.GameUpdateCount;
            work.InitialQuantity = CountTaskInventory(actor, ItemID.DD2ElderCrystal);
            var source = actor.Body.inventory[slot];
            if (--source.stack == 0) actor.Body.inventory[slot] = new Item();
            // The native client spends the crystal before request 113. Only an
            // explicit own-connection queue refusal makes restoring it safe.
            bool accepted;
            using (var scope = CompanionHooks.Enter(actor, actor.Aim))
            {
                try
                {
                    DD2Event.SummonCrystal(x, y, actor.Body.whoAmI);
                    accepted = Main.netMode != 1 || scope.PacketAccepted;
                }
                catch (CompanionConnection.SendQueueFullException) { accepted = false; }
            }
            if (!accepted)
            {
                source.stack++;
                actor.Body.inventory[slot] = source;
                return TaskOutcome("failed", "队友连接未接纳水晶台请求，未消耗水晶。");
            }
            PublishServiceInventory(actor, slot);
            return TaskOutcome("running", "已按原版水晶台入口使用一个队友水晶，等待事件生效。");
        }

        private CompanionTaskOutcome ObserveNaturalEvent(TaskExecution execution, string natural)
        {
            bool martian = natural == "martian";
            if (martian ? Main.invasionType == InvasionID.MartianMadness : LiveServiceNpc(NPCID.HallowBoss))
                return TaskOutcome("succeeded", martian ? "已经观察到火星暴乱。" : "已经观察到光之女皇。", value: "true");
            int type = martian ? NPCID.MartianProbe : NPCID.EmpressButterfly;
            var body = execution.Actor.Body;
            var nearby = (TaskNpcTable ?? Array.Empty<NPC>()).Where(npc => npc?.type == type && TaskNpcObserved(npc) &&
                Vector2.DistanceSquared(body.Center, npc.Center) <= 1600f * 1600f)
                .OrderBy(npc => Vector2.DistanceSquared(body.Center, npc.Center)).Take(4).ToArray();
            if (nearby.Length == 0)
                return TaskOutcome("succeeded", martian ? "队友观测范围没有自然出现的火星探测器。原版需石巨人进度，通常到世界外侧和高空等待。" :
                    "队友观测范围没有七彩草蛉。通常在击败世纪之花后，于神圣之地入夜时自然出现。", value: "false");
            return TaskOutcome("succeeded", (martian ? "可让实际出现的探测器发现队友后自行报告，走近时会停火：" :
                "可在玩家明确委托下处理实际七彩草蛉，白天光之女皇会进入原版强化：" ) +
                String.Join("；", nearby.Select(npc => npc.FullName + " " + NpcTaskReference(npc) +
                    (Vector2.DistanceSquared(body.Center, npc.Center) > NaturalEventReachPixels * NaturalEventReachPixels ? "（须先接近至 28 格内）" : "（在可行动距离内）") +
                    (martian && npc.ai[0] == 2f ? "（正在飞离报告）" : ""))), nearby.Length,
                nearby.Length == 1 ? NpcTaskReference(nearby[0]) : null);
        }

        private CompanionTaskOutcome TriggerNaturalEvent(TaskExecution execution, string natural)
        {
            var actor = execution.Actor;
            bool martian = natural == "martian";
            bool ongoing = martian ? Main.invasionType == InvasionID.MartianMadness : LiveServiceNpc(NPCID.HallowBoss);
            if (!(execution.State is NaturalEventUse work))
            {
                if (ongoing) return TaskOutcome("failed", "世界已有该事件，未再执行触发。");
                var target = ReadTaskNpc(execution.Text("npc"));
                int type = martian ? NPCID.MartianProbe : NPCID.EmpressButterfly;
                if (target?.type != type || !TaskNpcObserved(target) ||
                    Vector2.DistanceSquared(actor.Body.Center, target.Center) > NaturalEventReachPixels * NaturalEventReachPixels)
                    return TaskOutcome("failed", "请先通过事件观察选定附近的具体 NPC，当前目标未出现或代次已变化。");
                if (actor.Fishing != null) EndFishingActivity(actor);
                var combat = martian ? new TaskCombat { HoldFire = true } : null;
                if (martian) actor.TaskCombat = combat;
                else ArmLacewingEventCombat(actor, target, ref combat);
                if (combat == null)
                    return TaskOutcome("failed", "当前没有可用的原生攻击条件，未触碰草蛉。");
                SetTaskTravel(actor, null);
                execution.State = work = new NaturalEventUse
                { Npc = target, Slot = target.whoAmI, Generation = target.generation, Combat = combat };
                execution.MarkSubmitted();
            }
            var npc = work.Npc;
            if (ongoing)
            {
                bool matched = martian ? work.SawReport : npc.life <= 0;
                CancelTaskEvent(execution);
                return TaskOutcome(matched ? "succeeded" : "unconfirmed",
                    matched ? (martian ? "已观察到该探测器飞离报告，真实火星暴乱已开始。" : "已观察到该草蛉被击杀，真实光之女皇已出现。") :
                    "世界已出现此事件，但未确认当前目标促发。", matched ? 1 : 0);
            }
            if (!ReferenceEquals(actor.TaskCombat, work.Combat) || Main.GameUpdateCount - execution.StartedAt > 1800)
            {
                CancelTaskEvent(execution);
                return TaskOutcome(work.SawReport || npc.life <= 0 ? "unconfirmed" : "failed", "本次未能确认事件实际开始。");
            }
            if (!ReferenceEquals(TaskNpcTable?[work.Slot], npc) || npc.generation != work.Generation || !npc.active)
            {
                if (work.SawReport || !martian && npc.life <= 0)
                    return TaskOutcome("waiting", "目标已退场，等待世界或服务器发布实际结果。");
                CancelTaskEvent(execution);
                return TaskOutcome("unconfirmed", "原来观察的目标已失效，无法确认事件发生。");
            }
            if (!TaskNpcObserved(npc)) return TaskOutcome("waiting", "正在等待此目标的真实更新。");
            if (!martian) return TaskOutcome("running", "只攻击玩家选中的这一只七彩草蛉，等待真实事件。");
            if (npc.ai[0] == 2f) work.SawReport = true;
            if (npc.ai[0] == 1f || npc.ai[0] == 2f)
            {
                SetTaskTravel(actor, null);
                return TaskOutcome("waiting", work.SawReport ? "探测器正在按原版飞离报告，队友保持停火。" : "探测器发现了玩家，正在等待它开始飞离。");
            }
            if (Vector2.DistanceSquared(actor.Body.Center, npc.Center) < 352f * 352f && actor.Body.Center.Y > npc.Center.Y)
                return TaskOutcome("waiting", "队友已在探测器的原版下方侦测范围内，保持停火等识别。");
            Point point = npc.Center.ToTileCoordinates();
            if (work.Arrival == null || Math.Abs(point.X - work.Arrival.X) > 5 || Math.Abs(point.Y + 8 - work.Arrival.Y) > 5)
            {
                SetTaskTravel(actor, null);
                work.Arrival = new ArrivalSearch { X = point.X, Y = point.Y + 8, HorizontalRadius = 6, VerticalRadius = 6, Name = "探测器下方" };
            }
            if (actor.TaskTravel == null)
            {
                if (!FindTaskFooting(actor, work.Arrival, out var feet, out var exhausted))
                    return exhausted ? TaskOutcome("blocked", "已知地形没有探测器下方的可站立落脚点。") :
                        TaskOutcome("waiting", "正在寻找探测器下方的真实落脚点。");
                SetTaskTravel(actor, new TaskTravel { Feet = feet, Name = work.Arrival.Name });
            }
            return TaskOutcome("running", "正在走到探测器下方，让原版自然侦测；不会攻击它。");
        }
        private void CancelTaskEvent(TaskExecution execution)
        {
            if (execution.State is EventUse work)
                CancelQueuedTaskItem(execution.Actor, work.Hand, "已撤回尚未开始的事件用物。");
            if (execution.State is NaturalEventUse natural && ReferenceEquals(execution.Actor.TaskCombat, natural.Combat))
            {
                execution.Actor.TaskCombat = null;
                execution.Actor.Combat.Reset();
            }
            CancelTravelTask(execution);
        }
    }
}
