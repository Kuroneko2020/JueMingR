using System;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class TaskCritter
        {
            internal NPC Target;
            internal int Generation;
            internal ArrivalSearch Site;
        }

        private void RegisterCritterTaskOperation() => RegisterTaskOperation("catch_critter",
            "Walk to and catch the exact observed creature with a real suitable bug net. Count only a native capture received by the companion.",
            CatchTaskCritter,CancelWorldUse,new TaskField("target","string",true,1,64),new TaskField("item_type","integer",false,1,ItemID.Count-1));

        private CompanionTaskOutcome CatchTaskCritter(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            if (!CompanionHooks.TaskCritterAvailable) return TaskOutcome("failed","当前原版捕虫网接口不可用。");
            if (!(execution.State is TaskCritter work))
            {
                var npc=ReadTaskNpc(execution.Text("target"));
                if (!TaskNpcObserved(npc) || !IsCatchable(npc)) return TaskOutcome("failed","目标已离开，或无法按原版规则捕捉。");
                if (actor.Fishing!=null) EndFishingActivity(actor);
                execution.State = work = new TaskCritter { Target=npc,Generation=npc.generation };
            }
            if (ReferenceEquals(actor.TaskWorldHand?.Execution,execution)) return TaskOutcome("running","正在使用队友的原版捕虫网。");
            if (!TaskCritterCurrent(work.Target,work.Generation) || Main.GameUpdateCount-execution.StartedAt>1800)
                return TaskOutcome("failed","未接近原来观察到的活体目标。");
            if (actor.TaskWorldHand != null) return TaskOutcome("waiting","正在等队友结束上一件原版用物。");
            if (!CompanionMotionAction.Near(body.Center,work.Target.Center,36f,32f))
            {
                var tile=work.Target.Bottom.ToTileCoordinates();
                if (work.Site==null || Math.Abs(tile.X-work.Site.X)>2 || Math.Abs(tile.Y-work.Site.Y)>2)
                {
                    work.Site = new ArrivalSearch { X=tile.X,Y=tile.Y,HorizontalRadius=5,VerticalRadius=7 };
                    SetTaskTravel(actor,null);
                }
                if (actor.TaskTravel==null)
                {
                    if (!FindTaskFooting(actor,work.Site,out var feet,out bool exhausted))
                        return TaskOutcome(exhausted?"failed":"waiting",exhausted?"目标附近缺少可站位置。":"正在核对捕捉地形。");
                    actor.TaskControlId=execution.Job.Id;
                    SetTaskTravel(actor,new TaskTravel { Feet=feet, Name="捕捉地点" });
                }
                return TaskOutcome("running","正在接近原来观察到的活体目标。");
            }
            int requested=execution.Number("item_type");
            for(int slot=0;slot<CompanionBehavior.MainInventorySlots;slot++)
            {
                var item=body.inventory[slot];
                if (item==null || !FitsCritterNet(item.type,work.Target.catchItem) || requested!=0 && requested!=item.type ||
                    !SupplyAdmitted(actor,body.inventory,0,slot,ownsSelected:slot==body.selectedItem)) continue;
                HoldTaskHandPosition(execution);
                actor.TaskWorldHand=new TaskWorldHand { Execution=execution,Operation="catch_critter",Slot=slot,Source=item,
                    Type=item.type,Stack=item.stack,Critter=work.Target,CritterGeneration=work.Generation,
                    ProductType=work.Target.catchItem,ProductBefore=CountTaskInventory(actor,work.Target.catchItem),
                    CritterDeadline=Main.GameUpdateCount+600 };
                return TaskOutcome("running","已到达原版捕捉范围。");
            }
            return TaskOutcome("failed","队友没有适用的原版捕虫网。");
        }

        private static bool IsCatchable(NPC npc) => npc?.active==true && npc.catchItem>0 && !npc.SpawnedFromStatue &&
            npc.type!=NPCID.BoundTownSlimeYellow;

        private bool TaskCritterCurrent(NPC npc,int generation) => IsCatchable(npc) && npc.generation==generation &&
            ReferenceEquals(TaskNpcTable?[npc.whoAmI],npc);

        private static bool FitsCritterNet(int tool,int product) => tool==ItemID.GoldenBugNet || tool==ItemID.FireproofBugNet ||
            tool==ItemID.BugNet && !ItemID.Sets.IsLavaBait[product];

        internal bool TaskCritterAllowsNativeTarget(Actor actor,NPC candidate)
        {
            var work=actor?.TaskWorldHand;
            if (!candidate.active || work?.Critter==null) return candidate.active;
            return work.Submitted && !work.Cancelled && !work.CritterContact && ReferenceEquals(work.Critter,candidate) &&
                TaskCritterCurrent(candidate,work.CritterGeneration);
        }

        internal void ObserveTaskCritterCaught(Actor actor,int slot)
        {
            var hand=actor?.TaskWorldHand;
            if (hand?.Critter!=null && slot==hand.Critter.whoAmI && !hand.Critter.active && hand.Submitted)
            {
                hand.CritterContact=true;
                actor.Dirty=true;
            }
        }

        private void AfterCritterTaskHand(Actor actor,TaskWorldHand hand)
        {
            if (!hand.CritterContact && !hand.SentToWorld && !hand.Cancelled && FreeHand(actor.Body) &&
                TaskCritterCurrent(hand.Critter,hand.CritterGeneration) &&
                !CompanionMotionAction.Near(actor.Body.Center,hand.Critter.Center,36f,32f))
            {
                ReleaseWorldHand(actor,hand);
                ((TaskCritter)hand.Execution.State).Site=null;
                SetTaskTravel(actor,null);
                return;
            }
            int gained=CountTaskInventory(actor,hand.ProductType)-hand.ProductBefore;
            var result=hand.CritterContact && gained>0 && (Main.netMode!=1 || hand.SentToWorld) ?
                TaskOutcome("succeeded","原版捕捉到指定活体，已收到物品。",gained,hand.ProductType.ToString()) :
                unchecked((int)(Main.GameUpdateCount-hand.CritterDeadline))>=0 || actor.Body.dead || !hand.CritterContact &&
                (hand.Cancelled || hand.Pulses>=8 || !TaskCritterCurrent(hand.Critter,hand.CritterGeneration)) ?
                TaskOutcome(hand.CritterContact || hand.SentToWorld ? "unconfirmed" : "failed",
                    hand.CritterContact ? "活体已从原版捕捉流程移除，尚未确认队友收到物品。" : "未确认捕捉到原来观察的目标。") : null;
            if (result==null || !FreeHand(actor.Body) && !actor.Body.dead) return;
            ReleaseWorldHand(actor,hand);
            hand.Execution.Settle(result);
        }
    }
}
