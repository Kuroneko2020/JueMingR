using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Projection = JueMingR.TerrariaHost.Blueprint.BlueprintProjectionPlacement;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal Func<HostBlueprint> TaskBlueprint = () => null;

        private sealed class BlueprintWork
        {
            internal Projection Projection;
            internal BlueprintProjectionView View;
            internal int X, Y, Index, Moves, ShapePulses, MaterialType, PageX, PageY;
            internal CaptureCell[] Cells;
            internal IReadOnlyList<CaptureCell> Members;
            internal IReadOnlyList<CaptureObject> Objects;
            internal readonly HashSet<int> MatchedObjects = new HashSet<int>();
            internal WorldUse Work;
            internal Point? Awaiting;
            internal CompanionTaskOutcome Result;
            internal HostBlueprint Owner;
            internal HostBlueprint.TaskProjectionRead Read;
        }

        private void RegisterBlueprintTaskOperations()
        {
            RegisterTaskObservation("observe_blueprints","List confirmed placements and their stable blueprint references in this world.",
                ObserveTaskBlueprints);
            RegisterTaskOperation("build_projection",
                "Build the selected confirmed blueprint projection using this companion's actual supplies and native tile actions.",
                BuildTaskProjection,CancelBlueprintWork,new TaskField("reference","string",true,1,80));
        }

        private static string ProjectionReference(Projection projection) => "blueprint:" + projection.Identity.AssetId.ToString("N") +
            ":" + projection.Identity.ProjectionGeneration;

        private CompanionTaskOutcome ObserveTaskBlueprints(TaskExecution execution)
        {
            var blueprint = TaskBlueprint();
            if (blueprint == null || !blueprint.ControlsEnabled) return TaskOutcome("waiting","当前世界的蓝图资料尚未就绪。");
            var rows = blueprint.ProjectionScene.Where(item => item.Positioned && item.Identity != null).Take(16).ToArray();
            return TaskOutcome("succeeded",rows.Length == 0 ? "当前世界没有已确认摆放的蓝图。" :
                String.Join("；",rows.Select(item => item.Name + " → " + ProjectionReference(item))),rows.Length,
                rows.Length == 1 ? ProjectionReference(rows[0]) : null);
        }

        private CompanionTaskOutcome BuildTaskProjection(TaskExecution execution)
        {
            var actor = execution.Actor;
            var source = TaskBlueprint();
            if (source == null || !source.ControlsEnabled) return TaskOutcome("waiting","当前世界的蓝图资料尚未就绪。");
            if (!(execution.State is BlueprintWork state))
            {
                var projection = source.ProjectionScene.FirstOrDefault(item => item.Positioned &&
                    ProjectionReference(item) == execution.Text("reference"));
                if (projection == null) return TaskOutcome("failed","未找到对应的已确认摆放蓝图。");
                state = new BlueprintWork
                {
                    Projection = projection, View = projection.View, X = projection.OriginX, Y = projection.OriginY,
                    Owner = source, PageY = (projection.View.Height-1)/64*64
                };
                execution.State = state;
            }
            var current = state.Projection;
            bool currentView = current.OriginX == state.X && current.OriginY == state.Y && ReferenceEquals(current.View,state.View);
            if (currentView && current.CompletedByWorldMatch)
            {
                if (state.Work?.Hand != null && state.Result == null)
                    return TaskOutcome("running","已匹配整个蓝图，正在等待本次原版用物收尾。",state.Moves);
                source.ReleaseTaskProjectionRead(state.Read);
                int moves = state.Moves + (state.Result?.State == "succeeded" ? 1 : 0);
                return TaskOutcome("succeeded","原版世界已验证整个蓝图；本队友确认了 " + moves + " 次操作。",moves);
            }
            if (!currentView || !source.ProjectionScene.Contains(current))
                return TaskOutcome(state.Moves > 0 ? "partial" : "failed","蓝图位置或内容已修改，已停止原步骤。",state.Moves);
            if (state.Cells == null)
            {
                if (state.PageY < 0)
                    return TaskOutcome("succeeded","已核对蓝图各区块，完成 " + state.Moves + " 次有效原版操作。",state.Moves);
                var display = new BlueprintProjectionBounds(state.PageX,state.PageY,Math.Min(64,state.View.Width-state.PageX),
                    Math.Min(64,state.View.Height-state.PageY));
                if (state.Read == null)
                    state.Read = source.BeginTaskProjectionRead(current,state.View.SourceBounds(display.Expand(18)));
                if (state.Read == null) return TaskOutcome("waiting","正在排队读取蓝图区块。",state.Moves);
                source.PollTaskProjectionRead(state.Read);
                if (state.Read.Error != null) return TaskOutcome(state.Moves > 0 ? "partial" : "failed",state.Read.Error,state.Moves);
                var geometry = state.Read.Window;
                if (geometry == null) return TaskOutcome("waiting","正在读取蓝图区块。",state.Moves);
                if (!state.View.TryApplyWorkCore(geometry,display,out var effective,out var reason))
                    return reason == "blueprint-mirror-profile-not-ready" ?
                        TaskOutcome("waiting","正在等待原版镜像描述就绪。",state.Moves) :
                        TaskOutcome(state.Moves > 0 ? "partial" : "failed","无法核对当前蓝图区块的实际形态。",state.Moves);
                source.ReleaseTaskProjectionRead(state.Read);
                state.Read = null;
                // Walls first, then physical blocks from the bottom, with
                // paint and wire applied only after their target exists.
                state.Cells = effective.Cells.Where(cell=>display.Contains(cell.X,cell.Y))
                    .OrderByDescending(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
                state.Members = effective.Cells;
                state.Objects = effective.Objects;
                state.MatchedObjects.Clear();
            }
            if (state.Result != null)
            {
                var result = state.Result;
                state.Result = null;
                state.Work = null;
                if (result.State != "succeeded") return TaskOutcome(result.State == "unconfirmed" ? result.State :
                    state.Moves > 0 ? "partial" : result.State,result.Detail,state.Moves);
                state.Moves++;
            }
            if (state.Work != null)
            {
                var progress = AdvanceWorldUse(execution,state.Work,state.MaterialType,0);
                BindBlueprintHand(execution,state);
                return progress;
            }
            if (state.Index >= state.Cells.Length)
            {
                state.Cells = null;
                state.Index = 0;
                state.PageX += 64;
                if (state.PageX >= state.View.Width) { state.PageX = 0; state.PageY -= 64; }
                return TaskOutcome("running","已核对一个蓝图区块。",state.Moves);
            }
            var expected = state.Cells[state.Index];
            var point = new Point(state.X+expected.X,state.Y+expected.Y);
            if (!CompanionNativeContact.Loaded(point.X,point.Y))
            {
                state.Awaiting = point;
                return TaskOutcome("waiting","正在等待蓝图目标地形。",state.Moves);
            }
            state.Awaiting = null;
            var actual = new TileFact(Main.tile[point.X,point.Y]);
            if (state.Work == null)
            {
                if ((expected.Layers & CaptureLayer.Wall) != 0 && actual.Wall != expected.WallType)
                {
                    if (actual.Wall != 0) return TaskOutcome("blocked","目标已有不同墙体，保留其现有状态。",state.Moves);
                    return PrepareBlueprintWork(execution,state,point,actual,"place_wall",
                        item => item.createWall == expected.WallType);
                }
                if ((expected.Layers & CaptureLayer.Tile) != 0 && expected.ObjectId > 0 &&
                    !state.MatchedObjects.Contains(expected.ObjectId))
                {
                    var objectWork = PrepareBlueprintObject(execution,state,expected);
                    if (objectWork != null) return objectWork;
                    state.MatchedObjects.Add(expected.ObjectId);
                }
                if ((expected.Layers & CaptureLayer.Tile) != 0 && (!actual.Active || actual.Type != expected.TileType))
                {
                    if (actual.Active) return TaskOutcome("blocked","蓝图目标已被其他实心方块占用。",state.Moves);
                    if (expected.UnreliableObject)
                        return TaskOutcome(state.Moves > 0 ? "partial" : "failed","这件物体缺少可核实的原版放置形态。",state.Moves);
                    return PrepareBlueprintWork(execution,state,point,actual,"place_tile",
                        item => item.createTile == expected.TileType);
                }
                if ((expected.Layers & CaptureLayer.Tile) != 0 && expected.ObjectId == 0 &&
                    (actual.Slope != expected.Slope || actual.Half != expected.HalfBrick))
                {
                    if (state.ShapePulses++ > 8) return TaskOutcome("partial","原版锤形未达到投影要求。",state.Moves);
                    return PrepareBlueprintWork(execution,state,point,actual,"hammer",item => item.hammer > 0);
                }
                if ((expected.Layers & CaptureLayer.Wire) != 0 && (actual.Wires & expected.Wires) != expected.Wires)
                {
                    int need = expected.Wires & ~actual.Wires;
                    int tool = (need & 1) != 0 ? ItemID.Wrench : (need & 2) != 0 ? ItemID.BlueWrench :
                        (need & 4) != 0 ? ItemID.GreenWrench : ItemID.YellowWrench;
                    return PrepareBlueprintWork(execution,state,point,actual,"place_wire",item => item.type == tool);
                }
                if ((expected.Layers & CaptureLayer.Actuator) != 0 && expected.Actuator && !actual.Actuator)
                    return PrepareBlueprintWork(execution,state,point,actual,"place_actuator",item => item.type == ItemID.Actuator);
                if ((expected.Layers & CaptureLayer.Actuator) != 0 && expected.Inactive != actual.Inactive)
                    return PrepareBlueprintWork(execution,state,point,actual,"actuate",item => item.type == ItemID.ActuationRod);
                var decoration = BlueprintDecoration(execution,state,point,expected,actual);
                if (decoration != null) return decoration;
                if (expected.Payload.HasValue && (!new BlueprintWorldMatchSource(ArchiveIdentityCurrent).TryRead(point.X,point.Y,out var sample) ||
                    expected.Payload.Compare(sample.Cell.Payload) != BlueprintWorldMatchState.Match))
                    return TaskOutcome(state.Moves > 0 ? "partial" : "failed","已核对物件本体，此处保存的内容或物件状态仍需处理。",state.Moves);
                if ((expected.Layers & CaptureLayer.Liquid) != 0 &&
                    (expected.LiquidAmount != actual.Liquid || expected.LiquidKind != actual.LiquidKind))
                    return TaskOutcome(state.Moves > 0 ? "partial" : "failed","该处需要精确液量，暂未由蓝图步骤结算。",state.Moves);
                state.Index++;
                state.ShapePulses = 0;
            }
            return TaskOutcome("running","已检查 " + state.Index + "/" + state.Cells.Length + " 格。",state.Moves);
        }

        private CompanionTaskOutcome BlueprintDecoration(TaskExecution execution, BlueprintWork state,Point point,
            CaptureCell expected,TileFact actual)
        {
            int wallCoat = (expected.FullbrightWall ? 1 : 0) | (expected.InvisibleWall ? 2 : 0);
            int tileCoat = (expected.FullbrightTile ? 1 : 0) | (expected.InvisibleTile ? 2 : 0);
            // Scraper always clears the foreground first. Preserve a present
            // foreground when only the wall needs stripping.
            if ((expected.Layers & CaptureLayer.Wall) != 0 && (expected.WallPaint == 0 && actual.WallPaint != 0 ||
                (actual.WallCoating & ~wallCoat) != 0))
            {
                if (actual.Active && (actual.Paint != 0 || actual.TileCoating != 0 || actual.Type == TileID.LongMoss))
                    return TaskOutcome("blocked","原版刮刀会先改变前景，此处墙面需玩家处理。",state.Moves);
                return PrepareBlueprintWork(execution,state,point,actual,"scrape_wall",item=>ItemID.Sets.IsPaintScraper[item.type]);
            }
            if ((expected.Layers & CaptureLayer.Tile) != 0)
            {
                if (expected.TilePaint == 0 && actual.Paint != 0 || (actual.TileCoating & ~tileCoat) != 0)
                {
                    if (actual.Type == TileID.LongMoss) return TaskOutcome("blocked","原版刮刀会移除这片长苔，已保留原状。",state.Moves);
                    return PrepareBlueprintWork(execution,state,point,actual,"scrape_tile",item=>ItemID.Sets.IsPaintScraper[item.type]);
                }
                if (expected.TilePaint != actual.Paint)
                    return PrepareBlueprintWork(execution,state,point,actual,"paint_tile",IsBrush,paint:expected.TilePaint);
                if ((tileCoat & ~actual.TileCoating) != 0)
                    return PrepareBlueprintWork(execution,state,point,actual,"coat_tile",IsBrush,coating:(tileCoat & ~actual.TileCoating & 1) != 0 ? 1 : 2);
            }
            if ((expected.Layers & CaptureLayer.Wall) != 0)
            {
                if (expected.WallPaint != actual.WallPaint)
                    return PrepareBlueprintWork(execution,state,point,actual,"paint_wall",IsRoller,paint:expected.WallPaint);
                if ((wallCoat & ~actual.WallCoating) != 0)
                    return PrepareBlueprintWork(execution,state,point,actual,"coat_wall",IsRoller,coating:(wallCoat & ~actual.WallCoating & 1) != 0 ? 1 : 2);
            }
            return null;
        }

        private static bool IsBrush(Item item) => item.type == ItemID.Paintbrush || item.type == ItemID.SpectrePaintbrush;
        private static bool IsRoller(Item item) => item.type == ItemID.PaintRoller || item.type == ItemID.SpectrePaintRoller;

        private CompanionTaskOutcome PrepareBlueprintWork(TaskExecution execution, BlueprintWork state, Point point,
            TileFact current,string operation,Func<Item,bool> predicate,int paint=0, int facing=0,
            Func<bool> objectReady=null,Func<bool> objectPlaced=null,int coating=0)
        {
            var actor = execution.Actor;
            int itemType = 0;
            for (int slot=0;slot<CompanionBehavior.MainInventorySlots;slot++)
            {
                var item = actor.Body.inventory[slot];
                if (item == null || item.IsAir || !predicate(item) || !FitsWorldOperation(item,operation,current) ||
                    item.favorited && item.consumable || !SupplyAdmitted(actor,actor.Body.inventory,0,slot,ownsSelected:slot==actor.Body.selectedItem))
                    continue;
                itemType = item.type;
                break;
            }
            if (itemType == 0) return TaskOutcome("waiting","缺少目标格所需材料或工具，补齐后可以继续。",state.Moves);
            state.MaterialType = itemType;
            var work = state.Work = new WorldUse
            {
                Point = point, Before = current, Operation = operation, ExpectedPaint = paint, ExpectedCoating = coating,
                Facing = facing, ObjectReady = objectReady, ObjectPlaced = objectPlaced
            };
            var result = AdvanceWorldUse(execution,work,itemType,0);
            BindBlueprintHand(execution,state);
            return result;
        }

        private void BindBlueprintHand(TaskExecution execution, BlueprintWork state)
        {
            var hand = state.Work.Hand;
            if (hand == null || hand.Completed != null) return;
            hand.Completed = outcome =>
            {
                if (execution.Job.Active && ReferenceEquals(execution.Job.Current,execution)) state.Result = outcome;
                else execution.Settle(TaskOutcome(outcome.State,"蓝图停止后的原版操作回执：" + outcome.Detail,
                    state.Moves + (outcome.State == "succeeded" ? 1 : 0)));
            };
        }

        private CompanionTaskOutcome PrepareBlueprintObject(TaskExecution execution,BlueprintWork state,CaptureCell expected)
        {
            var obj = state.Objects.FirstOrDefault(candidate=>candidate.Id==expected.ObjectId);
            if (!obj.Reliable || obj.Width <= 0 || obj.Height <= 0 || obj.Width * obj.Height > 256)
                return TaskOutcome(state.Moves > 0 ? "partial" : "failed","无法确认这件多格物体的原版形态。",state.Moves);
            var match = new BlueprintWorldMatchSource(ArchiveIdentityCurrent);
            if (!match.TryDescribeTarget(obj,expected,state.X,state.Y,out var target))
                return TaskOutcome("blocked","整件物体的现有原版形态不可确认。",state.Moves);
            int left=state.X+obj.X,top=state.Y+obj.Y;
            var position = new Point(left+obj.OriginX,top+obj.OriginY);
            var members = state.Members.Where(cell=>cell.ObjectId==obj.Id)
                .OrderBy(cell=>cell.Y).ThenBy(cell=>cell.X).ToArray();
            if (members.Length != obj.Width*obj.Height)
                return TaskOutcome("blocked","正在等待完整的物体占位资料。",state.Moves);
            bool Known()
            {
                for (int y=top-2;y<top+obj.Height+2;y++)
                    for (int x=left-2;x<left+obj.Width+2;x++)
                        if (!CompanionNativeContact.Loaded(x,y)) { state.Awaiting = new Point(x,y); return false; }
                return true;
            }
            bool Matches()
            {
                for (int i=0;i<members.Length;i++)
                    if (!match.TryRead(members[i].X+state.X,members[i].Y+state.Y,out var sample) ||
                        sample.ObjectUnknown || !target.SameObservedObject(sample.Object)) return false;
                // Paint, coatings and other appearance are separate real
                // hands after this whole object's native geometry exists.
                return true;
            }
            if (!Known()) return TaskOutcome("waiting","正在等待整个物体及原版支撑格的地形。",state.Moves);
            if (Matches()) return null;
            for(int y=top;y<top+obj.Height;y++)
                for(int x=left;x<left+obj.Width;x++)
                    if(Main.tile[x,y].active()) return TaskOutcome("blocked","已存在物件与蓝图形态不同，保留其现有状态。",state.Moves);
            int facing = obj.Direction==1 ? -1 : obj.Direction==2 ? 1 : execution.Actor.Body.direction;
            bool CanPlace()
            {
                if (!Known() || !BlueprintPlacementQuery.CanPlace(position.X,position.Y,obj.Type,obj.Style,facing,out var choice) ||
                    choice.xCoord!=left || choice.yCoord!=top) return false;
                var data=TileObjectData.GetTileData(choice.type,choice.style,choice.alternate);
                return data!=null && data.Width==obj.Width && data.Height==obj.Height &&
                    data.RandomStyleRange<=1 && data.CalculatePlacementStyle(choice.style,choice.alternate,choice.random)==obj.PlacementStyle &&
                    BlueprintPlacementQuery.PostConditions(choice,position.X,position.Y);
            }
            return PrepareBlueprintWork(execution,state,position,new TileFact(Main.tile[position.X,position.Y]),"place_tile",
                item=>item.createTile==obj.Type && item.placeStyle==obj.Style,facing:facing,objectReady:CanPlace,objectPlaced:Matches);
        }

        private void CancelBlueprintWork(TaskExecution execution)
        {
            if (execution.State is BlueprintWork state) state.Owner.ReleaseTaskProjectionRead(state.Read);
            CancelWorldUse(execution);
        }
    }
}
