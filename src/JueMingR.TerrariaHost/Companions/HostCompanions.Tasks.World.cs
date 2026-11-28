using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class TaskWorldHand
        {
            internal TaskExecution Execution;
            internal Action<CompanionTaskOutcome> Completed;
            internal Point Point;
            internal string Operation;
            internal TileFact Before;
            internal Item Source;
            internal Item Auxiliary;
            internal int Slot, Type, Stack, CreatedTile, CreatedWall, Previous = -1, Pulses, ProductType, ProductBefore;
            internal int AuxiliarySlot = -1, AuxiliaryStack, Facing;
            internal ArrivalSearch PickupSite;
            internal bool ApproachingProduct;
            internal bool EffectConfirmed;
            internal NPC Critter;
            internal int CritterGeneration;
            internal uint CritterDeadline;
            internal bool CritterContact;
            internal Func<bool> ObjectReady, ObjectPlaced;
            internal uint NativeAt, RetryAt, NextPickupCheck;
            internal bool Pressed, Submitted, SentToWorld, TileEcho, Cancelled;
        }

        internal readonly struct TileFact
        {
            internal TileFact(Tile tile)
            {
                Active = tile.active(); Type = tile.type; Wall = tile.wall; Slope = tile.slope();
                Paint = tile.color(); WallPaint = tile.wallColor(); Half = tile.halfBrick(); Liquid = tile.liquid;
                TileCoating = (tile.fullbrightBlock() ? 1 : 0) | (tile.invisibleBlock() ? 2 : 0);
                WallCoating = (tile.fullbrightWall() ? 1 : 0) | (tile.invisibleWall() ? 2 : 0);
                LiquidKind = tile.liquidType();
                Wires = (tile.wire() ? 1 : 0) | (tile.wire2() ? 2 : 0) | (tile.wire3() ? 4 : 0) | (tile.wire4() ? 8 : 0);
                FrameX = tile.frameX; FrameY = tile.frameY; Actuator = tile.actuator(); Inactive = tile.inActive();
            }
            internal bool Active { get; }
            internal ushort Type { get; }
            internal ushort Wall { get; }
            internal byte Slope { get; }
            internal byte Paint { get; }
            internal byte WallPaint { get; }
            internal int TileCoating { get; }
            internal int WallCoating { get; }
            internal byte Liquid { get; }
            internal byte LiquidKind { get; }
            internal bool Half { get; }
            internal int Wires { get; }
            internal short FrameX { get; }
            internal short FrameY { get; }
            internal bool Actuator { get; }
            internal bool Inactive { get; }
        }

        private sealed class WorldUse
        {
            internal Point Point;
            internal TileFact Before;
            internal string Operation;
            internal ArrivalSearch Arrival;
            internal TaskWorldHand Hand;
            internal int ExpectedPaint, ExpectedCoating, Facing;
            internal uint? Started;
            internal Func<bool> ObjectReady, ObjectPlaced;
        }

        internal readonly struct TargetTile
        {
            internal TargetTile(Point point, TileFact fact) { Point = point; Fact = fact; }
            internal Point Point { get; }
            internal TileFact Fact { get; }
        }

        private static readonly (string Id,string Label,string Requirements)[] WorldActions =
        {
            ("mine","挖矿、采矿、挖方块","已查看的方块、实际可到达和足够原版镐力的工具。需要掉落物时指定 product_type，入包才算收取。多个同类矿格可用 mine_area。"),
            ("axe","砍树、伐木","已查看的树木格和有斧力的工具。需要木材入包时指定 product_type。"),
            ("hammer","锤半砖、整形、斜坡","已查看、可锤形的方块和有锤力的工具。"),
            ("remove_wall","拆墙、敲墙","已查看、可拆除的墙和有锤力的工具。"),
            ("place_tile","放方块、放家具","已查看的自然落点、队友实际持有的物品及原版支撑、占地和朝向条件。"),
            ("place_wall","放墙、铺墙","已查看的格，队友持有原版墙物品及可连接的落点。"),
            ("paint_tile","刷漆、涂方块","已查看的方块、漆刷及指定颜色的颜料。"),
            ("paint_wall","刷墙、涂墙","已查看的墙、漆滚及指定颜色的颜料。"),
            ("coat_tile","涂层、方块涂层","已查看的方块和原版涂层工具与材料。"),
            ("coat_wall","涂层、墙涂层","已查看的墙和原版涂层工具与材料。"),
            ("scrape_tile","刮漆、刮方块漆","已查看的方块和漆铲。"), ("scrape_wall","刮墙漆","已查看的墙和漆铲。"),
            ("place_wire","布线、接线","已查看的目标、电线与可用布线工具。"),
            ("cut_wire","剪线、拆线路","已查看的线路及可用切线工具。"),
            ("liquid","倒水、舀水、处理液体","已查看的目标格和匹配的实际容器与液体条件。"),
            ("place_actuator","安制动器","已查看的目标、可用制动器及装设工具。"),
            ("actuate","使用制动器","已查看的制动器和原版切换工具。"),
            ("switch","拉开关、拨开关","已查看且实际可操作的原版开关。")
        };

        partial void RegisterWorldTaskOperations()
        {
            RegisterTaskObservation("observe_tiles",
                "Read up to twelve known tiles or liquid cells around the sending position or companion. Returned targets belong to this companion and world.",
                ObserveWorkTiles, new TaskField("around","string",false,1,12,"here","companion"),
                new TaskField("radius","integer",false,1,12), new TaskField("tile_type","integer",false,0,TileID.Count - 1),
                new TaskField("empty","boolean"), new TaskField("liquid","string",false,1,8,"water","lava","honey","shimmer"),
                new TaskField("offset","integer",false,0,625));
            RegisterTaskOperation("world_use",
                "Walk into real reach and use a companion item on an observed tile. Mining can wait for actual actor inventory gain when product_type is set.",
                WorldUseTask, CancelWorldUse, new TaskField("target","string",true,1,64),
                new TaskField("action","string",true,1,24,WorldActions.Select(action=>action.Id).ToArray()),
                new TaskField("item_type","integer",false,1,ItemID.Count - 1),
                new TaskField("product_type","integer",false,1,ItemID.Count - 1),
                new TaskField("paint","integer",false,1,byte.MaxValue), new TaskField("coating","integer",false,1,2));
            RegisterBlueprintTaskOperations();
            RegisterCritterTaskOperation();
            RegisterMiningTaskOperation();
        }

        private CompanionTaskOutcome ObserveWorkTiles(TaskExecution execution)
        {
            var actor = execution.Actor;
            bool here = execution.Text("around") == "here";
            if (here && !execution.Anchor.Valid) return TaskOutcome("rejected", "消息发送时没有有效位置。");
            var center = (here ? execution.Anchor.Feet : CompanionNativeContact.Feet(actor.Body)).ToTileCoordinates();
            int radius = execution.Number("radius", 4), size = radius * 2 + 1, start = execution.Number("offset");
            int type = execution.Number("tile_type", -1), rowCount = 0, unknown = 0, next = -1;
            string liquidFilter = execution.Text("liquid");
            bool empty = execution.Flag("empty",liquidFilter!=null);
            var found = new List<string>();
            string only = null;
            actor.TaskTiles = actor.TaskTiles ?? new Dictionary<string, TargetTile>(StringComparer.Ordinal);
            for (int index = start; index < size * size; index++)
            {
                int x = center.X + index % size - radius, y = center.Y + index / size - radius;
                if (!WorldGen.InWorld(x,y,3)) continue;
                if (!CompanionNativeContact.Loaded(x,y)) { unknown++; continue; }
                var tile = Main.tile[x,y];
                if (tile.active() == empty || type >= 0 && (!tile.active() || tile.type != type)) continue;
                string liquid = tile.liquid == 0 ? null : tile.shimmer() ? "shimmer" : tile.lava() ? "lava" : tile.honey() ? "honey" : "water";
                if (liquidFilter != null && liquidFilter != liquid) continue;
                if (rowCount == 12) { next = index; break; }
                if (actor.TaskTiles.Count >= 128) actor.TaskTiles.Remove(actor.TaskTiles.Keys.First());
                string reference = Guid.NewGuid().ToString("N");
                actor.TaskTiles.Add(reference,new TargetTile(new Point(x,y),new TileFact(tile)));
                only = reference;
                found.Add("(" + x + "," + y + ") · " + (tile.active() ? "方块 " + tile.type : "空格") +
                    (tile.wall > 0 ? " · 墙 " + tile.wall : "") + (liquid == null ? "" : " · " + LiquidLabel(liquid) + " " + tile.liquid) +
                    " · " + reference);
                rowCount++;
            }
            return TaskOutcome(rowCount == 0 && unknown != 0 ? "waiting" : "succeeded",
                (rowCount == 0 ? "已知范围没有匹配格。" : String.Join("；",found)) +
                "；范围中心 ("+center.X+","+center.Y+")，"+(here?"发送时主人":"当前队友")+"，横竖各 ±"+radius+
                " 格，当前世界刻 "+Main.GameUpdateCount+"，本页 offset="+start+"。"+
                (unknown > 0 ? " 本页 "+unknown+" 格地形尚未收到。" : "") + (next >= 0 ? " 下一页 offset=" + next : ""),
                rowCount, rowCount == 1 && next < 0 ? only : null);
        }

        private static string LiquidLabel(string kind)
        {
            switch(kind)
            {
                case "shimmer": return "微光";
                case "lava": return "熔岩";
                case "honey": return "蜂蜜";
                default: return "水";
            }
        }

        private CompanionTaskOutcome WorldUseTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (!(execution.State is WorldUse work))
            {
                if (actor.TaskTiles == null || !actor.TaskTiles.TryGetValue(execution.Text("target"),out var target))
                    return TaskOutcome("failed","目标引用已失效，请先查询该队友可观察的地形。");
                var anchor=execution.Anchor;
                if (anchor.WorkPoint is Point point && (point!=target.Point || anchor.WorkReference?.StartsWith("marker:",StringComparison.Ordinal)==true &&
                    !MarkerWorkSiteCurrent(anchor.WorkReference,point)))
                    return TaskOutcome("failed","发话时指定的位置已变化，请重新确认。");
                string operation = execution.Text("action");
                if (execution.Number("product_type") > 0 && operation != "mine" && operation != "axe")
                    return TaskOutcome("failed","只有开挖或砍伐可以指定要收取的实际产物。");
                if (execution.Number("paint") > 0 && operation != "paint_tile" && operation != "paint_wall" ||
                    execution.Number("coating") > 0 && operation != "coat_tile" && operation != "coat_wall")
                    return TaskOutcome("failed","指定的颜料或涂层必须与实际工具操作一致。");
                if (actor.Fishing != null) EndFishingActivity(actor);
                work = new WorldUse { Point = target.Point, Before = target.Fact, Operation = operation,
                    ExpectedPaint = execution.Number("paint"), ExpectedCoating = execution.Number("coating") };
                execution.State = work;
            }
            return AdvanceWorldUse(execution, work, execution.Number("item_type"),execution.Number("product_type"));
        }

        private CompanionTaskOutcome AdvanceWorldUse(TaskExecution execution, WorldUse work, int itemType, int productType)
        {
            var actor = execution.Actor;
            int x = work.Point.X, y = work.Point.Y;
            if (!WorldGen.InWorld(x,y,4)) return TaskOutcome("failed","目标超出当前世界操作范围。");
            if (!work.Started.HasValue) work.Started = Main.GameUpdateCount;
            if (Main.GameUpdateCount - work.Started.Value > 3600)
                return TaskOutcome(work.Hand?.Submitted == true ? "unconfirmed" : "failed","在允许的期限内未确认该目标。");
            if (!CompanionNativeContact.Loaded(x,y))
            {
                work.Arrival = work.Arrival ?? new ArrivalSearch { X=x,Y=y,HorizontalRadius=7,VerticalRadius=8 };
                work.Arrival.Awaiting = work.Point;
                return TaskOutcome("waiting","目标地形尚未收到。");
            }
            if (work.Arrival != null) work.Arrival.Awaiting = null;
            if (work.Hand != null) return TaskOutcome("running","正在执行原版世界操作。");
            var current = new TileFact(Main.tile[x,y]);
            if (!WorldTargetCurrent(work.Before,current,work.Operation))
                return TaskOutcome("failed","目标已被改变，本步未使用物品。");
            if (actor.TaskWorldHand != null) return TaskOutcome("waiting","队友正在收尾上一件原版用物。");
            if (!ActorTileReachReady(actor.Body) || !actor.Body.IsInTileInteractionRange(x,y,ActorTileReach(actor.Body)))
            {
                if (actor.TaskTravel == null || work.Arrival == null)
                {
                    work.Arrival = work.Arrival ?? new ArrivalSearch { X = x, Y = y, HorizontalRadius = 7, VerticalRadius = 8 };
                    if (!FindTaskFooting(actor,work.Arrival,out var feet,out bool exhausted))
                        return exhausted ? TaskOutcome("failed","目标邻近没有可达落脚点。") :
                            TaskOutcome("waiting","正在核对作业位置。");
                    actor.TaskControlId = execution.Job.Id;
                    SetTaskTravel(actor,new TaskTravel { Feet = feet, Name = "作业地点" });
                }
                if (actor.TaskTravel.Arrived && !actor.TaskTravel.Temporary && ActorTileReachReady(actor.Body))
                    return TaskOutcome("blocked","已到达附近，但原版的互动距离仍未满足。");
                return TaskOutcome("running","正在接近原版作业范围。");
            }
            if (work.ObjectReady?.Invoke() == false) return TaskOutcome("blocked","整件物体的原版占位、朝向或支撑条件尚未满足。");
            bool switchClick = work.Operation == "switch";
            if (switchClick && (!current.Active || current.Inactive || current.Type != TileID.Switches && current.Type != TileID.Lever))
                return TaskOutcome("failed","该目标不是当前可点击的开关或拉杆。");
            int slot = switchClick ? -1 : WorldToolSlot(actor,work.Operation,itemType,current,work.Point);
            if (slot < 0 && !switchClick) return TaskOutcome("failed","队友缺少目标适用且可用的物品。");
            var item = slot < 0 ? null : actor.Body.inventory[slot];
            int auxiliary = WorldAuxiliarySlot(actor,work.Operation,work.ExpectedPaint,work.ExpectedCoating);
            if (auxiliary == -2) return TaskOutcome("waiting","原版会选用的颜料或电线不适用或尚不可用。");
            HoldTaskHandPosition(execution);
            work.Hand = new TaskWorldHand
            {
                Execution = execution, Point = work.Point, Before = current, Operation = work.Operation,
                Slot = slot, Source = item, Type = item?.type ?? 0, Stack = item?.stack ?? 0,
                CreatedTile = item?.createTile ?? 0, CreatedWall = item?.createWall ?? 0,
                AuxiliarySlot = auxiliary, Auxiliary = auxiliary >= 0 ? actor.Body.inventory[auxiliary] : null,
                AuxiliaryStack = auxiliary >= 0 ? actor.Body.inventory[auxiliary].stack : 0,
                Facing = work.Facing, ObjectReady = work.ObjectReady, ObjectPlaced = work.ObjectPlaced,
                ProductType = productType, ProductBefore = productType > 0 ? CountTaskInventory(actor,productType) : 0
            };
            actor.TaskWorldHand = work.Hand;
            return TaskOutcome("running","已进入作业范围，等待队友握持原版工具。");
        }

        private void HoldTaskHandPosition(TaskExecution execution)
        {
            var actor=execution.Actor;
            // Without a job-owned stance the ordinary standby anchor or
            // Follow can carry a waiting hand away from its native target.
            actor.TaskControlId=execution.Job.Id;
            SetTaskTravel(actor,new TaskTravel { Feet=CompanionNativeContact.Feet(actor.Body),Name="作业位置" });
        }

        private static bool WorldTargetCurrent(TileFact old, TileFact current, string operation)
        {
            if (old.Active != current.Active || old.Active && (old.Type != current.Type ||
                !Main.tileSolid[old.Type] && (old.FrameX != current.FrameX || old.FrameY != current.FrameY))) return false;
            switch (operation)
            {
                case "place_wall":
                case "remove_wall": return old.Wall == current.Wall;
                case "scrape_wall": return old.Wall == current.Wall && old.WallPaint == current.WallPaint &&
                    old.WallCoating == current.WallCoating && old.Paint == current.Paint && old.TileCoating == current.TileCoating;
                case "scrape_tile": return old.Paint == current.Paint && old.TileCoating == current.TileCoating;
                case "paint_wall": return old.Wall == current.Wall && old.WallPaint == current.WallPaint;
                case "paint_tile": return old.Paint == current.Paint;
                case "coat_wall": return old.Wall == current.Wall && old.WallCoating == current.WallCoating;
                case "coat_tile": return old.TileCoating == current.TileCoating;
                case "place_wire":
                case "cut_wire": return old.Wires == current.Wires;
                case "liquid": return old.Liquid == current.Liquid && old.LiquidKind == current.LiquidKind;
                case "place_actuator": return old.Actuator == current.Actuator;
                case "actuate": return old.Actuator == current.Actuator && old.Inactive == current.Inactive;
                case "switch": return old.FrameX == current.FrameX && old.FrameY == current.FrameY;
                default: return old.Slope == current.Slope && old.Half == current.Half;
            }
        }

        // Vanilla chooses the first matching supply, independently of the
        // selected tool slot. A protected earlier stack blocks this attempt.
        private int WorldAuxiliarySlot(Actor actor, string operation, int expectedPaint, int expectedCoating)
        {
            var body = actor.Body;
            int slot = -1;
            if (operation == "place_wire")
            {
                for (int i=0;i<58;i++)
                    if (body.inventory[i]?.type == ItemID.Wire && body.inventory[i].stack > 0) { slot=i; break; }
            }
            else if (operation == "paint_tile" || operation == "paint_wall" || operation == "coat_tile" || operation == "coat_wall")
            {
                var chosen = body.FindPaintOrCoating();
                bool coat = operation == "coat_tile" || operation == "coat_wall";
                if (chosen == null || coat && (chosen.paint != 0 || chosen.paintCoating == 0 ||
                    expectedCoating > 0 && chosen.paintCoating != expectedCoating) || !coat &&
                    (chosen.paintCoating != 0 || expectedPaint > 0 && chosen.paint != expectedPaint)) return -2;
                for (int i=0;i<58;i++) if (ReferenceEquals(chosen,body.inventory[i])) { slot=i; break; }
            }
            else return -1;
            return slot >= 0 && !body.inventory[slot].favorited &&
                SupplyAdmitted(actor,body.inventory,0,slot,ownsSelected:slot==body.selectedItem) ? slot : -2;
        }

        private static readonly Func<Player,int,int,int,int,Tile,int> NativePickDamage =
            (Func<Player,int,int,int,int,Tile,int>)Delegate.CreateDelegate(typeof(Func<Player,int,int,int,int,Tile,int>),
                typeof(Player).GetMethod("GetPickaxeDamage",BindingFlags.Instance|BindingFlags.NonPublic));

        private int WorldToolSlot(Actor actor, string operation, int requested, TileFact tile, Point point)
        {
            int best = -1, bestDamage = 0;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
            {
                Item item = actor.Body.inventory[slot];
                if (item == null || item.IsAir || item.favorited && item.consumable ||
                    requested != 0 && item.type != requested ||
                    !SupplyAdmitted(actor,actor.Body.inventory,0,slot,ownsSelected: slot == actor.Body.selectedItem)) continue;
                if (!FitsWorldOperation(item,operation,tile)) continue;
                if (operation != "mine") return slot;
                if (!WorldGen.CanKillTile(point.X,point.Y)) return -1;
                // Ordinary terrain has no object re-anchoring or hit-buffer
                // writes in the native pick-power calculation.
                int damage = Main.tileFrameImportant[tile.Type] ? item.pick :
                    NativePickDamage(actor.Body,point.X,point.Y,item.pick,-1,Main.tile[point.X,point.Y]);
                if (damage>bestDamage) { best=slot; bestDamage=damage; }
            }
            return best;
        }

        private static bool FitsWorldOperation(Item item, string operation, TileFact tile)
        {
            switch (operation)
            {
                case "mine": return tile.Active && item.pick > 0 && (!item.channel || IsMiningDrill(item));
                case "axe": return tile.Active && item.axe > 0 && !item.channel;
                case "hammer": return tile.Active && item.hammer > 0 && !item.channel;
                case "remove_wall": return !tile.Active && tile.Wall > 0 && item.hammer > 0 && !item.channel;
                case "place_tile": return !tile.Active && item.createTile >= 0;
                case "place_wall": return tile.Wall == 0 && item.createWall > 0;
                case "coat_tile":
                case "paint_tile": return tile.Active && (item.type == ItemID.Paintbrush || item.type == ItemID.SpectrePaintbrush);
                case "coat_wall":
                case "paint_wall": return tile.Wall != 0 && (item.type == ItemID.PaintRoller || item.type == ItemID.SpectrePaintRoller);
                case "scrape_tile": return tile.Active && tile.Type != TileID.LongMoss &&
                    (tile.Paint != 0 || tile.TileCoating != 0) && ItemID.Sets.IsPaintScraper[item.type];
                case "scrape_wall": return (!tile.Active || tile.Type != TileID.LongMoss && tile.Paint == 0 && tile.TileCoating == 0) &&
                    tile.Wall != 0 && (tile.WallPaint != 0 || tile.WallCoating != 0) && ItemID.Sets.IsPaintScraper[item.type];
                case "place_wire": return item.type == ItemID.Wrench || item.type == ItemID.BlueWrench ||
                    item.type == ItemID.GreenWrench || item.type == ItemID.YellowWrench;
                case "cut_wire": return !tile.Actuator && tile.Wires > 0 && item.type == ItemID.WireCutter;
                case "place_actuator": return tile.Active && !tile.Actuator && item.type == ItemID.Actuator;
                case "actuate": return tile.Active && tile.Actuator && item.type == ItemID.ActuationRod;
                case "liquid": return item.type == ItemID.EmptyBucket || item.type == ItemID.WaterBucket ||
                    item.type == ItemID.LavaBucket || item.type == ItemID.HoneyBucket;
                case "extract": return tile.Active && (tile.Type == TileID.Extractinator || tile.Type == TileID.ChlorophyteExtractinator) &&
                    ItemID.Sets.ExtractinatorMode[item.type] >= 0;
                default: return false;
            }
        }

        private static bool IsMiningDrill(Item item) => item.channel && item.pick>0 &&
            (item.shoot==ProjectileID.LaserDrill || ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var shot) && shot.aiStyle==20);

        private void CancelWorldUse(TaskExecution execution)
        {
            var actor = execution.Actor;
            var hand = actor.TaskWorldHand;
            if (!ReferenceEquals(hand?.Execution,execution)) return;
            hand.Cancelled = true;
            hand.Pressed = false;
            if (!hand.Submitted) ReleaseWorldHand(actor,hand);
        }

        private void ReleaseWorldHand(Actor actor, TaskWorldHand hand)
        {
            if (!ReferenceEquals(actor.TaskWorldHand,hand)) return;
            actor.TaskWorldHand = null;
            if (hand.Previous >= 0 && actor.Body.selectedItem == hand.Slot)
                actor.Body.selectedItemState.Select(hand.Previous);
        }

        private void RetireWorldHand(Actor actor)
        {
            var hand = actor.TaskWorldHand;
            if (hand == null) return;
            ReleaseWorldHand(actor,hand);
            if (hand.Submitted)
                hand.Execution.Settle(TaskOutcome("unconfirmed","队友身体已退出，本次原版用物结果未确认。"));
        }

        private bool StageWorldTaskHand(Actor actor, bool canPress)
        {
            var hand = actor.TaskWorldHand;
            if (hand == null) return false;
            hand.Pressed = false;
            if (!canPress || hand.Cancelled || actor.TaskWork?.Paused == true) return true;
            if (hand.Operation=="mine" && hand.Submitted && hand.Source!=null && IsMiningDrill(hand.Source) &&
                !hand.SentToWorld && hand.Pulses<24 && actor.Body.channel && actor.Body.selectedItem==hand.Slot && WorldHandCurrent(actor,hand))
            {
                // Hold the ordinary native channel on the same validated tile.
                // Moving, cancellation and actual tile changes release it.
                actor.Body.controlUseItem=hand.Pressed=true;
                return true;
            }
            if (!FreeHand(actor.Body) ||
                !actor.Body.releaseUseItem || unchecked((int)(Main.GameUpdateCount - hand.RetryAt)) < 0) return true;
            if (hand.SentToWorld || hand.CritterContact || hand.Pulses >= 24 || hand.Critter != null &&
                (hand.Pulses >= 8 || unchecked((int)(Main.GameUpdateCount-hand.CritterDeadline)) >= 0)) return true;
            if (!WorldHandCurrent(actor,hand)) return true;
            if (hand.Slot < 0)
            {
                hand.NativeAt = Main.GameUpdateCount;
                hand.RetryAt = Main.GameUpdateCount + 10;
                hand.Pulses++;
                hand.Submitted = true;
                hand.Execution.MarkSubmitted();
                using (CompanionHooks.Enter(actor,new Vector2(hand.Point.X*16+8,hand.Point.Y*16+8)))
                {
                    Wiring.HitSwitch(hand.Point.X,hand.Point.Y);
                    if (Main.netMode == 1) NetMessage.SendData(59,-1,-1,null,hand.Point.X,hand.Point.Y);
                }
                return true;
            }
            if (hand.Previous < 0) hand.Previous = actor.Body.selectedItem;
            if (hand.Facing != 0 && actor.Body.direction != hand.Facing) actor.Body.ChangeDir(hand.Facing);
            actor.Body.selectedItemState.Select(hand.Slot);
            actor.Body.controlUseItem = true;
            hand.Pressed = true;
            return true;
        }

        private bool WorldHandCurrent(Actor actor, TaskWorldHand hand) => SupplyActorReady(actor) &&
            !actor.Body.noItems && !actor.Body.CCed && !actor.Body.cursed &&
            (hand.Slot < 0 || ReferenceEquals(actor.Body.inventory[hand.Slot],hand.Source) && hand.Source.type == hand.Type &&
             hand.Source.stack == hand.Stack) &&
            (hand.Critter != null ? TaskCritterCurrent(hand.Critter,hand.CritterGeneration) &&
                CompanionMotionAction.Near(actor.Body.Center,hand.Critter.Center,36f,32f) : WorldTileHandCurrent(actor,hand));

        private bool WorldTileHandCurrent(Actor actor,TaskWorldHand hand) => ActorTileReachReady(actor.Body) &&
            CompanionNativeContact.Loaded(hand.Point.X,hand.Point.Y) &&
            actor.Body.IsInTileInteractionRange(hand.Point.X,hand.Point.Y,ActorTileReach(actor.Body)) &&
            (hand.AuxiliarySlot < 0 || ReferenceEquals(actor.Body.inventory[hand.AuxiliarySlot],hand.Auxiliary) &&
             hand.Auxiliary.stack == hand.AuxiliaryStack &&
             WorldAuxiliarySlot(actor,hand.Operation,hand.Auxiliary.paint,hand.Auxiliary.paintCoating) == hand.AuxiliarySlot) &&
            WorldTargetCurrent(hand.Before,new TileFact(Main.tile[hand.Point.X,hand.Point.Y]),hand.Operation) &&
            hand.ObjectReady?.Invoke() != false;

        internal bool PrepareWorldTaskItem(Actor actor, out Vector2 point)
        {
            point = Vector2.Zero;
            var hand = actor.TaskWorldHand;
            if (hand?.Pressed != true) return false;
            hand.Pressed = false;
            if (!WorldHandCurrent(actor,hand) || actor.Body.selectedItem != hand.Slot)
            {
                actor.Body.controlUseItem = false;
                return false;
            }
            point = hand.Critter?.Center ?? new Vector2(hand.Point.X * 16f + 8f,hand.Point.Y * 16f + 8f);
            if (hand.Critter != null) hand.Facing = point.X < actor.Body.Center.X ? -1 : 1;
            if (hand.Facing != 0 && actor.Body.direction != hand.Facing) actor.Body.ChangeDir(hand.Facing);
            hand.NativeAt = Main.GameUpdateCount;
            if (unchecked((int)(Main.GameUpdateCount-hand.RetryAt))>=0)
            {
                hand.RetryAt = Main.GameUpdateCount + 10;
                hand.Pulses++;
            }
            hand.Submitted = true;
            hand.Execution.MarkSubmitted();
            return true;
        }

        private bool TryMiningProjectileAim(Actor actor,Projectile projectile,out Vector2 point)
        {
            point=default;
            var hand=actor.TaskWorldHand;
            if (hand?.Source==null || hand.Operation!="mine" || hand.Cancelled || !hand.Submitted ||
                projectile.type!=ProjectileID.LaserDrill || hand.Source.shoot!=projectile.type ||
                actor.Body.selectedItem!=hand.Slot || !WorldHandCurrent(actor,hand)) return false;
            point=new Vector2(hand.Point.X*16+8,hand.Point.Y*16+8);
            return true;
        }

        internal void ObserveTaskWorldTile(int x,int y)
        {
            foreach (var actor in actors.Values)
            {
                var hand = actor.TaskWorldHand;
                if (hand?.Submitted == true && hand.Point.X == x && hand.Point.Y == y && hand.SentToWorld)
                    hand.TileEcho = true;
            }
        }

        internal void ObserveTaskWorldSend(Actor actor, byte[] packet)
        {
            var hand = actor.TaskWorldHand;
            if (hand?.Critter!=null && hand.Submitted && packet.Length>=5 && packet[2]==70 &&
                BitConverter.ToInt16(packet,3)==hand.Critter.whoAmI) { hand.SentToWorld=true; return; }
            if (hand?.Submitted != true || packet.Length < 7 || hand.NativeAt != Main.GameUpdateCount) return;
            int offset;
            switch (packet[2])
            {
                case 17:
                    if (packet.Length >= 10 && (packet[3] == 0 || packet[3] == 2 || packet[3] == 4) &&
                        BitConverter.ToInt16(packet,8) == 1) return; // Native hit feedback, no completed tile edit.
                    offset = 4;
                    break;
                case 48: case 59: case 63: case 64: case 79: offset = 3; break;
                default: return;
            }
            if (BitConverter.ToInt16(packet,offset) == hand.Point.X && BitConverter.ToInt16(packet,offset+2) == hand.Point.Y)
                hand.SentToWorld = true;
        }

        private void AfterWorldTaskHand(Actor actor)
        {
            var hand = actor.TaskWorldHand;
            if (hand == null) return;
            if (hand.Critter != null) { AfterCritterTaskHand(actor,hand); return; }
            bool known = CompanionNativeContact.Loaded(hand.Point.X,hand.Point.Y);
            var actual = known ? new TileFact(Main.tile[hand.Point.X,hand.Point.Y]) : default(TileFact);
            bool changed = known && !WorldTargetCurrent(hand.Before,actual,hand.Operation);
            var remaining = hand.Slot < 0 ? null : actor.Body.inventory[hand.Slot];
            bool debit = hand.Slot >= 0 && (remaining?.type != hand.Type || remaining.stack < hand.Stack) ||
                hand.Auxiliary != null && (hand.Auxiliary.stack < hand.AuxiliaryStack ||
                !ReferenceEquals(actor.Body.inventory[hand.AuxiliarySlot],hand.Auxiliary));
            bool authoritative = Main.netMode != 1 || hand.TileEcho;
            int obtained = hand.ProductType == 0 ? 0 : Math.Max(0,CountTaskInventory(actor,hand.ProductType) - hand.ProductBefore);
            CompanionTaskOutcome outcome = null;
            bool actualEffect = hand.Submitted && changed && WorldEffectMatches(hand,actual) &&
                hand.ObjectPlaced?.Invoke() != false && authoritative;
            hand.EffectConfirmed |= actualEffect;
            if (actualEffect && hand.ProductType > 0 && obtained == 0 && !hand.Cancelled &&
                actor.TaskWork?.Paused != true && actor.TaskTravel?.Temporary != true)
                ApproachTaskProduct(actor,hand);
            if (hand.Submitted && hand.Operation == "extract" && debit && (hand.ProductType == 0 || obtained > 0))
                outcome = TaskOutcome("succeeded","已观察到本次原版提炼消耗。",hand.ProductType == 0 ? 1 : obtained);
            else if (actualEffect && (hand.ProductType == 0 || obtained > 0))
                outcome = TaskOutcome("succeeded", hand.ProductType == 0 ? "已观察到目标世界格变化。" :
                    "已从实际作业取得物品 " + obtained + " 件。", hand.ProductType == 0 ? 1 : obtained,
                    obtained > 0 ? obtained.ToString(CultureInfo.InvariantCulture) : null);
            else if (hand.Submitted && (changed || debit || hand.SentToWorld) && Main.GameUpdateCount - hand.NativeAt >= 150)
                outcome = TaskOutcome("unconfirmed","原版已提交或有可见变化，未能确认完整结果。");
            else if (!hand.Submitted && !WorldHandCurrent(actor,hand))
                outcome = TaskOutcome("failed","开始前目标、范围或原物品已变化。");
            else if (actor.Body.dead || hand.Pulses >= 24 || hand.Cancelled && FreeHand(actor.Body) && !changed && !hand.SentToWorld)
                outcome = TaskOutcome(hand.Submitted ? "unconfirmed" : "cancelled","本次世界操作未能确认完成。");
            if (outcome == null || !FreeHand(actor.Body) && !actor.Body.dead) return;
            ReleaseWorldHand(actor,hand);
            if (hand.Completed != null) hand.Completed(outcome);
            else hand.Execution.Settle(outcome);
        }

        private void ApproachTaskProduct(Actor actor, TaskWorldHand hand)
        {
            // A destroyed tile is not an inventory grant. Take an ordinary
            // movement goal only toward a fresh matching drop near this work.
            if (hand.ApproachingProduct || hand.Operation != "mine" && hand.Operation != "axe") return;
            if (unchecked((int)(Main.GameUpdateCount-hand.NextPickupCheck)) < 0) return;
            hand.NextPickupCheck = Main.GameUpdateCount + 12;
            var center = new Vector2(hand.Point.X*16+8,hand.Point.Y*16+8);
            WorldItem drop = null;
            float closest = 56f * 56f;
            void Consider(WorldItem item)
            {
                if (item == null || !item.active || item.type != hand.ProductType || item.timeSinceItemSpawned > 150 ||
                    item.playerIndexTheItemIsReservedFor != actor.Body.whoAmI && item.playerIndexTheItemIsReservedFor != 255) return;
                float distance = Vector2.DistanceSquared(item.Center,center);
                if (distance < closest) { drop = item; closest = distance; }
            }
            if (Main.netMode == 1) foreach (var item in actor.PersonalItems.Values) Consider(item);
            foreach (var item in Main.item) Consider(item);
            if (drop == null) return;
            var point = drop.Center.ToTileCoordinates();
            hand.PickupSite = hand.PickupSite ?? new ArrivalSearch { X=point.X,Y=point.Y,HorizontalRadius=3,VerticalRadius=5 };
            if (!FindTaskFooting(actor,hand.PickupSite,out var feet,out _)) return;
            hand.ApproachingProduct = true;
            actor.TaskControlId = hand.Execution.Job.Id;
            SetTaskTravel(actor,new TaskTravel { Feet=feet,Name="作业掉落" });
        }

        private static bool WorldEffectMatches(TaskWorldHand hand, TileFact actual)
        {
            switch (hand.Operation)
            {
                case "mine":
                case "axe": return hand.Before.Active && !actual.Active;
                case "hammer": return actual.Active && actual.Type == hand.Before.Type &&
                    (actual.Slope != hand.Before.Slope || actual.Half != hand.Before.Half);
                case "remove_wall": return actual.Wall == 0;
                case "place_tile": return actual.Active && actual.Type == hand.CreatedTile;
                case "place_wall": return actual.Wall == hand.CreatedWall;
                case "paint_tile": return actual.Active && actual.Type == hand.Before.Type && actual.Paint != hand.Before.Paint;
                case "paint_wall": return actual.Wall == hand.Before.Wall && actual.WallPaint != hand.Before.WallPaint;
                case "coat_tile": return actual.Active && actual.Type == hand.Before.Type && actual.TileCoating != hand.Before.TileCoating;
                case "coat_wall": return actual.Wall == hand.Before.Wall && actual.WallCoating != hand.Before.WallCoating;
                case "scrape_tile": return actual.Active && actual.Type == hand.Before.Type &&
                    (actual.Paint != hand.Before.Paint || actual.TileCoating != hand.Before.TileCoating);
                case "scrape_wall": return actual.Wall == hand.Before.Wall &&
                    (actual.WallPaint != hand.Before.WallPaint || actual.WallCoating != hand.Before.WallCoating);
                case "place_wire": return (actual.Wires & ~hand.Before.Wires) != 0;
                case "cut_wire": return (hand.Before.Wires & ~actual.Wires) != 0;
                case "place_actuator": return actual.Actuator;
                case "actuate": return hand.Before.Inactive != actual.Inactive;
                case "switch": return actual.Active && actual.Type == hand.Before.Type &&
                    (hand.Before.FrameX != actual.FrameX || hand.Before.FrameY != actual.FrameY);
                default: return actual.Liquid != hand.Before.Liquid || actual.LiquidKind != hand.Before.LiquidKind;
            }
        }
    }
}
