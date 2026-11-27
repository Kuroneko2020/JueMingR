using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class TaskFish
        {
            internal ArrivalSearch Site;
            internal FishingActivity Activity;
            internal DukeFishing Duke;
            internal int Type, Quantity, Caught;
            internal bool Quest, FinishIfBlocked;
            internal Item Overflow;
            internal uint NextCapacityCheck;
        }

        private CompanionTaskOutcome FishTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (!(execution.State is TaskFish work))
            {
                if (!TryTaskArrival(execution, out var site, out var reason)) return TaskOutcome("failed", reason);
                site.NearWater = site.Side != 0;
                bool quest = execution.Flag("quest_fish");
                int fish = quest ? ActorQuestType(actor) : execution.Number("item_type");
                if (quest && fish <= 0) return TaskOutcome("failed", "队友当前没有可接的当日钓鱼任务。");
                if (CompanionHooks.FishingUnavailable != null) return TaskOutcome("failed", "当前原版钓鱼接口不可用。");
                if (!TryFishingRod(actor, -1, out _)) return TaskOutcome("failed", "队友没有可用鱼竿。");
                bool duke = execution.Operation == "summon_duke";
                DukeFishing request = null;
                if (duke)
                {
                    if (DukePresent()) return TaskOutcome("failed","世界已有猪龙鱼公爵。");
                    if (pendingDuke != null) return TaskOutcome("failed","上一条原版猪鲨召唤仍在等确认。");
                    if (!DukeCoastalColumn(site.X-20) && !DukeCoastalColumn(site.X+20))
                        return TaskOutcome("failed","目标不在猪鲨原版海岸钓线范围内。");
                    if (!TryDukeBait(actor, out int slot)) return TaskOutcome("failed","队友没有可用的松露虫。");
                    request = new DukeFishing { Execution=execution, Slot=slot, Bait=actor.Body.inventory[slot] };
                }
                else
                {
                    FishingNativeBait(actor.Body, out int bait, out _);
                    if (bait <= 0)
                    {
                        if (execution.Flag("notify_no_bait")) NotifyTaskFishing(actor.Profile.OwnerId,actor.Profile.Id,actor.Body.name + "缺少可用鱼饵。");
                        return execution.Flag("finish_if_blocked") ?
                            TaskOutcome("succeeded","队友现无可用鱼饵，已按委托结束钓鱼，本步渔获 0 件。",0,"0") :
                            TaskOutcome("failed", "队友没有可用鱼饵。");
                    }
                }
                work = new TaskFish { Site = site, Type = fish, Quest = quest, Quantity = execution.Number("quantity", quest ? 1 : 0) };
                work.Duke = request;
                work.FinishIfBlocked = execution.Flag("finish_if_blocked");
                execution.State = work;
            }
            if (work.Activity == null)
            {
                if (Main.GameUpdateCount - execution.StartedAt > 216000)
                    return TaskOutcome("failed", "在任务期限内未能核对钓位。");
                FishingAnchor anchor;
                if (execution.Text("destination") == "here" && execution.Anchor.Valid) anchor = execution.Anchor;
                else
                {
                    if (!FindTaskFooting(actor, work.Site, out var point, out bool exhausted))
                        return exhausted ? TaskOutcome("failed", "目标附近未找到可站立的钓位。") :
                            TaskOutcome("waiting", work.Site.Awaiting.HasValue ? "正在等待钓位地形资料。" : "正在核对钓位。");
                    anchor = new FishingAnchor(point, actor.Body.gravDir);
                }
                if (actor.Fishing != null) EndFishingActivity(actor);
                SetTaskTravel(actor, null);
                actor.TaskControlId = execution.Job.Id;
                work.Activity = BeginFishingActivity(actor, anchor);
                work.Activity.OwnerTaskId = execution.Job.Id;
                work.Activity.Duke = work.Duke;
                if (work.Duke != null) dukeFishingActors++;
                if (execution.Flag("notify_no_bait"))
                {
                    var identity = execution.Job.Identity;
                    string name = actor.Body.name;
                    work.Activity.ChatNeedsBait = () => NotifyTaskFishing(identity.OwnerId,identity.ProfileId,name + "缺少可用鱼饵。");
                }
            }
            if (!ReferenceEquals(actor.Fishing, work.Activity))
                return TaskOutcome("cancelled", "原钓鱼活动已结束。", work.Caught);
            if (work.Duke != null) return ReadDukeFishing(execution,work.Duke);
            if (work.Quest && ActorQuestType(actor) != work.Type)
            {
                EndFishingActivity(actor);
                return TaskOutcome(work.Caught > 0 ? "partial" : "failed", "当日任务鱼或交付资格已变化。", work.Caught);
            }
            if (work.Quantity > 0 && work.Caught >= work.Quantity)
            {
                actor.TaskLastFeet = work.Activity.FeetGoal;
                EndFishingActivity(actor);
                SetTaskTravel(actor, new TaskTravel { Feet = actor.TaskLastFeet.Value, Arrived = true, Name = work.Site.Name });
                return TaskOutcome("succeeded", "实际取得所需渔获 " + work.Caught + " 件。", work.Caught,
                    work.Type > 0 ? work.Type.ToString() : null);
            }
            if (work.Quantity == 0 && !work.FinishIfBlocked && work.Activity.CastConfirmed)
            {
                actor.TaskLastFeet = work.Activity.FeetGoal;
                return TaskOutcome("succeeded", "浮漂已进入可用水域，队友会继续钓鱼。", 0, work.Site.Reference);
            }
            if (work.FinishIfBlocked && (work.Overflow != null || work.Activity.GearNotice == 2))
            {
                string blocker = work.Overflow != null ? "主背包无法承接实际渔获" : "鱼饵已用完";
                actor.TaskLastFeet = work.Activity.FeetGoal;
                EndFishingActivity(actor);
                return TaskOutcome("succeeded",blocker + "，已按委托结束钓鱼，实际渔获 " + work.Caught + " 件。",
                    work.Caught,work.Caught.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (work.Overflow != null)
            {
                if (unchecked((int)(Main.GameUpdateCount-work.NextCapacityCheck)) >= 0)
                {
                    work.NextCapacityCheck = Main.GameUpdateCount+30;
                    for(int i=0;i<CompanionBehavior.MainInventorySlots;i++)
                        if (actor.Body.CanItemSlotAcceptPickup(actor.Body.inventory[i],work.Overflow)) { work.Overflow=null; break; }
                }
                if (work.Overflow != null)
                    return TaskOutcome("waiting","已钓到目标鱼，但主背包暂时放不下。清出空间后继续。",work.Caught);
            }
            if (Main.GameUpdateCount - execution.StartedAt > 216000)
            {
                EndFishingActivity(actor);
                return TaskOutcome(work.Caught > 0 ? "partial" : "failed", "在任务期限内未取得足够渔获。", work.Caught);
            }
            return TaskOutcome(work.Activity.Defending || work.Activity.GearNotice != 0 || work.Activity.AwaitingTile.HasValue ?
                "waiting" : "running", (work.Activity.Status ?? "正在前往钓位") +
                (work.Quantity == 0 ? "" : "，渔获 " + work.Caught + "/" + work.Quantity), work.Caught);
        }

        private void CancelFishingTask(TaskExecution execution)
        {
            if (execution.State is TaskFish work && work.Activity != null && ReferenceEquals(execution.Actor.Fishing, work.Activity))
                EndFishingActivity(execution.Actor);
        }

        private static int TaskCatchBefore(Actor actor, int type)
        {
            var work = actor.TaskWork?.Current?.State as TaskFish;
            return work?.Activity != null && ReferenceEquals(work.Activity, actor.Fishing) &&
                (work.Type == 0 || work.Type == type) ? CountTaskInventory(actor, type) : -1;
        }

        private void RecordTaskCatch(Actor actor,int type,int prior,Item overflow)
        {
            if (prior < 0) return;
            var work = actor.TaskWork?.Current?.State as TaskFish;
            if (work?.Activity == null || !ReferenceEquals(work.Activity, actor.Fishing) ||
                work.Type != 0 && work.Type != type) return;
            work.Caught += Math.Max(0, CountTaskInventory(actor,type) - prior);
            if (overflow == null || work.Overflow != null ||
                (work.Quantity == 0 ? !work.FinishIfBlocked : work.Caught >= work.Quantity)) return;
            work.Overflow = overflow;
            work.NextCapacityCheck = Main.GameUpdateCount+30;
            string message = actor.Body.name + "已经钓到目标鱼，主背包暂时放不下。";
            NotifyTaskFishing(actor.Profile.OwnerId,actor.Profile.Id,message);
        }

        private void NotifyTaskFishing(Guid ownerId,Guid profileId,string message)
        {
            Main.NewText(message);
            PublishChatTaskFact(ownerId,profileId,message);
        }

        private static bool TaskFishingCapacityBlocked(Actor actor) => actor.TaskWork?.Current?.State is TaskFish work &&
            ReferenceEquals(work.Activity,actor.Fishing) && work.Overflow != null;

        private CompanionTaskOutcome AttackTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            if (!(execution.State is TaskCombat intent))
            {
                var target = ReadTaskNpc(execution.Text("target"));
                if (target == null || target.friendly || target.townNPC ||
                    NPCID.Sets.IsTownPet[target.type] || NPCID.Sets.CountsAsCritter[target.type])
                    return TaskOutcome("failed", "未找到仍然有效的敌对 NPC。");
                if (actor.Fishing != null) EndFishingActivity(actor);
                float leash = execution.Number("leash_tiles", 24) * 16f;
                if (Vector2.DistanceSquared(body.Center, target.Center) > leash * leash)
                    return TaskOutcome("failed", "指定敌人已超出授权追击范围。");
                intent = new TaskCombat { Target = target, Generation = target.generation, Center = body.Center, Radius = leash };
                actor.TaskCombat = intent;
                actor.Combat.Reset();
                execution.State = intent;
            }
            var npc = intent.Target;
            if (!ReferenceEquals(TaskNpcTable?[npc.whoAmI], npc) || npc.generation != intent.Generation)
            { CancelCombatTask(execution); return TaskOutcome("unconfirmed", "目标实例已更换，无法确认先前敌人的结局。"); }
            if (npc.life <= 0)
            { CancelCombatTask(execution); return TaskOutcome("succeeded", "已观察到指定敌人被击败。", 1); }
            if (!npc.active)
            { CancelCombatTask(execution); return TaskOutcome("unconfirmed", "目标已离开观察范围，未确认击败。"); }
            if (Vector2.DistanceSquared(intent.Center, npc.Center + npc.netOffset) > intent.Radius * intent.Radius ||
                Main.GameUpdateCount - execution.StartedAt > 36000)
            { CancelCombatTask(execution); return TaskOutcome("failed", "目标超出追击范围或本次交战已超时，队友停止追击。"); }
            return !TaskNpcObserved(npc) ? TaskOutcome("waiting", "正在等待目标的有效观察。") :
                CombatSelection.Receives(npc, false) && ObservedNpcCombatReady(npc) ?
                TaskOutcome("running", "正在交战：" + npc.FullName) :
                TaskOutcome("waiting", "正在等待目标进入可受击阶段。");
        }

        private CompanionTaskOutcome GuardTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            bool guardOwner = execution.Text("destination") == "owner";
            Vector2 point;
            if (guardOwner)
            {
                if (owner?.active != true || owner.dead) return TaskOutcome("failed", "当前玩家无法作为保护目标。");
                point = owner.Center;
            }
            else
            {
                if (!(execution.State is ArrivalSearch area))
                {
                    if (!TryTaskArrival(execution,out area,out var error)) return TaskOutcome("failed",error);
                    execution.State = area;
                }
                if (!FindTaskFooting(actor,area,out point,out bool exhausted))
                    return exhausted ? TaskOutcome("failed","未找到可站立的守卫位置。") :
                        TaskOutcome("waiting","正在核对守卫位置的地形。");
            }
            if (actor.Fishing != null) EndFishingActivity(actor);
            actor.TaskControlId = execution.Job.Id;
            actor.TaskCombat = new TaskCombat { GuardOwner = guardOwner, Center = point, Radius = execution.Number("leash_tiles", 18) * 16f };
            actor.Combat.Reset();
            SetTaskTravel(actor, new TaskTravel { Feet = point, FollowOwner = guardOwner, Name = guardOwner ? "保护主人" : "守卫指定地点" });
            return TaskOutcome("succeeded", guardOwner ? "已开始保护主人。" : "已开始向守卫位置移动。");
        }

        private CompanionTaskOutcome HoldFireTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            actor.TaskControlId = execution.Job.Id;
            actor.TaskCombat = new TaskCombat { HoldFire = true };
            actor.Combat.Reset();
            return TaskOutcome("succeeded", "已停止主动出手，正常避险与补给继续。");
        }

        private void CancelCombatTask(TaskExecution execution)
        {
            if (!ReferenceEquals(execution.Actor.TaskCombat, execution.State)) return;
            execution.Actor.TaskCombat = null;
            execution.Actor.Combat.Reset();
        }

        private bool TaskAllowsTarget(Actor actor, NPC npc, int generation)
        {
            var intent = actor.TaskCombat;
            if (intent == null) return true;
            if (intent.HoldFire) return false;
            if (intent.Target != null && (!ReferenceEquals(intent.Target, npc) || intent.Generation != generation)) return false;
            if (intent.GuardOwner && (owner?.active != true || owner.dead)) return false;
            var center = intent.GuardOwner ? owner.Center : intent.Center;
            return Vector2.DistanceSquared(center, npc.Center + npc.netOffset) <= intent.Radius * intent.Radius;
        }

        partial void ArmLacewingEventCombat(Actor actor,NPC target,ref TaskCombat intent)
        {
            if (target?.type != NPCID.EmpressButterfly || !TaskNpcObserved(target) || !SupplyActorReady(actor) ||
                target.life<=0 || target.dontTakeDamage || target.immortal) return;
            intent = new TaskCombat { Target=target,Generation=target.generation,Center=actor.Body.Center,Radius=28f*16f,LacewingEvent=true };
            actor.TaskCombat = intent;
            actor.Combat.Reset();
        }

        private static bool TaskLacewingTarget(Actor actor,NPC target,int generation) => target.type == NPCID.EmpressButterfly &&
            actor.TaskCombat?.LacewingEvent == true && ReferenceEquals(actor.TaskCombat.Target,target) && actor.TaskCombat.Generation==generation &&
            target.active && target.life>0 && !target.dontTakeDamage && !target.immortal;

        private static bool TaskAttackEnabled(Actor actor) => actor.TaskCombat == null ? actor.Preferences.AttackEnabled :
            !actor.TaskCombat.HoldFire;

        private CompanionTaskOutcome RecallTask(TaskExecution execution)
        {
            if (!execution.Beginning) return TaskOutcome("waiting", "正在等待原版召回结果。");
            TryRecall(execution.Actor.Profile.Id, automatic: false, chatResult: (state, message, waitingForNative) =>
            {
                if (state == "running" || state == "succeeded" || state == "unconfirmed") execution.MarkSubmitted();
                execution.Settle(TaskOutcome(state == "running" ? "waiting" : state, message));
            });
            return TaskOutcome("waiting", "已交由原版召回事务处理。");
        }
    }
}
