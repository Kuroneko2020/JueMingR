using System;
using System.Globalization;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static bool MiningProductMatches(int tileType,int product) =>
            ContentSamples.ItemsByType.TryGetValue(product,out var item) && item.createTile==tileType;

        private sealed class TaskMineArea
        {
            internal TaskExecution Travel;
            internal Point Center;
            internal int Scan, Broken, Obtained;
            internal bool Located, Unknown, MissingTool;
            internal WorldUse Work;
            internal CompanionTaskOutcome Result;
        }

        private void RegisterMiningTaskOperation() => RegisterTaskOperation("mine_area",
            "到当前世界的已记矿点或明确的近处位置，重新观察同种前景并依次原版采矿。collect 要求实际入包；break 只计真实破格。",
            RunMineArea,CancelWorldUse,
            new TaskField("destination","string",true,1,20,"here","companion","selected","memory","reference","marker"),
            new TaskField("reference","string",false,1,120),new TaskField("tile_type","integer",true,0,TileID.Count-1),
            new TaskField("amount","integer",true,1,64),new TaskField("mode","string",true,1,10,"break","collect"),
            new TaskField("product_type","integer",false,1,ItemID.Count-1),
            new TaskField("item_type","integer",false,1,ItemID.Count-1),new TaskField("radius","integer",false,1,12));

        private CompanionTaskOutcome RunMineArea(TaskExecution execution)
        {
            var actor=execution.Actor;
            int quota=execution.Number("amount"), tileType=execution.Number("tile_type"), radius=execution.Number("radius",8);
            bool collect=execution.Text("mode")=="collect";
            int product=execution.Number("product_type");
            if (collect && !MiningProductMatches(tileType,product)) return TaskOutcome("failed","需要与目标矿种匹配的原版产物类型。");
            if (!(execution.State is TaskMineArea work))
            {
                var bound=execution.Anchor;
                if (bound.WorkPoint.HasValue && bound.WorkOreType>=0 &&
                    SharedWorldMemory?.SavedOreTargetCurrent(bound.WorkReference,bound.WorkPoint.Value,tileType)!=true)
                    return TaskOutcome("failed","发话时确认的已保存矿点已变化，请重新确认。");
                execution.State=work=new TaskMineArea();
                if (execution.Text("destination")=="companion")
                {
                    work.Center=bound.WorkPoint ?? CompanionNativeContact.Feet(actor.Body).ToTileCoordinates();
                    work.Located=true;
                }
                else work.Travel=new TaskExecution(null)
                {
                    Actor=actor,Job=execution.Job,Anchor=execution.Anchor,Arguments=execution.Arguments,StartedAt=execution.StartedAt
                };
            }
            if (!work.Located)
            {
                var journey=TravelTask(work.Travel);
                if (journey.State!="succeeded") return journey;
                var location=(ArrivalSearch)work.Travel.State;
                work.Center=new Point(location.X,location.Y);
                work.Located=true;
            }
            if (work.Result!=null)
            {
                var result=work.Result;
                if (work.Work.Hand.EffectConfirmed) work.Broken++;
                if (collect && result.State=="succeeded") work.Obtained+=result.Completed;
                work.Work=null; work.Result=null;
                if (result.State!="succeeded") return MineProgress(work,result.State=="unconfirmed" ? "unconfirmed" :
                    work.Broken>0 ? "partial" : "failed",result.Detail,collect);
            }
            if (work.Broken>=quota || collect && work.Obtained>=quota)
                return MineProgress(work,!collect || work.Obtained>=quota ? "succeeded" : "partial","本次原版采矿已结算。",collect);
            if (work.Work!=null) return AdvanceMineHand(execution,work,collect?product:0);
            int span=radius*2+1,limit=Math.Min(span*span,work.Scan+32);
            for (;work.Scan<limit;work.Scan++)
            {
                int dx=AlternatingOffset(work.Scan%span),dy=AlternatingOffset(work.Scan/span);
                if (dx*dx+dy*dy>radius*radius) continue;
                var point=new Point(work.Center.X+dx,work.Center.Y+dy);
                if (!WorldGen.InWorld(point.X,point.Y,5)) continue;
                if (!CompanionNativeContact.Loaded(point.X,point.Y)) { work.Unknown=true; continue; }
                var fact=new TileFact(Main.tile[point.X,point.Y]);
                if (!fact.Active || fact.Type!=tileType) continue;
                if (WorldToolSlot(actor,"mine",execution.Number("item_type"),fact,point)<0) { work.MissingTool=true; continue; }
                work.Scan++;
                work.Work=new WorldUse { Point=point,Before=fact,Operation="mine" };
                return AdvanceMineHand(execution,work,collect?product:0);
            }
            if (work.Scan<span*span) return MineProgress(work,"running","正在重新观察矿点周围的实地。",collect);
            return MineProgress(work,work.Broken==0 ? "failed" : "partial",work.Unknown ? "还有未同步地形。" :
                work.MissingTool ? "剩余目标没有可用的原版镐或钻头。" : "已知范围内没有更多同类前景。",collect);
        }

        private CompanionTaskOutcome AdvanceMineHand(TaskExecution execution,TaskMineArea work,int product)
        {
            var outcome=AdvanceWorldUse(execution,work.Work,execution.Number("item_type"),product);
            var hand=work.Work.Hand;
            if (hand!=null && hand.Completed==null)
                hand.Completed=actual=>
                {
                    if (ReferenceEquals(execution.Job.Current,execution)) work.Result=actual;
                    else
                    {
                        if (hand.EffectConfirmed) work.Broken++;
                        if (product>0 && actual.State=="succeeded") work.Obtained+=actual.Completed;
                        execution.Settle(MineProgress(work,actual.State,"停止后的原版回执。",product>0));
                    }
                };
            return MineProgress(work,outcome.State,outcome.Detail,product>0);
        }

        private static CompanionTaskOutcome MineProgress(TaskMineArea work,string state,string detail,bool collect) =>
            TaskOutcome(state,"已挖 " + work.Broken + " 格" + (collect ? "，实收 " + work.Obtained + " 件" : "") + "。" + detail,
                collect ? work.Obtained : work.Broken,(collect ? work.Obtained : work.Broken).ToString(CultureInfo.InvariantCulture));
    }
}
