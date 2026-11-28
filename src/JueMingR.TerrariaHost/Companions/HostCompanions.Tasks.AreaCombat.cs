using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class TaskClearArea
        {
            internal Vector2 Center;
            internal float Radius;
            internal bool Arrived, PartialCoverage;
            internal readonly List<(NPC Body,int Generation)> Candidates=new List<(NPC,int)>();
            internal int Cursor, Killed, Lost, Protected, Pending;
            internal uint TargetSince;
            internal TaskCombat Intent;
            internal TaskExecution Travel;
        }

        private void RegisterAreaCombat() => RegisterTaskOperation("clear_area",
            "以玩家发送消息的位置为圆心，按图格半径一次清场。只处理一次现场观察中的实例，持续守卫请用 guard。",
            ClearAreaTask,CancelAreaCombat,new TaskField("radius","integer",true,1,100),new TaskField("include_friendly","boolean"));

        private CompanionTaskOutcome ClearAreaTask(TaskExecution execution)
        {
            var actor=execution.Actor;
            if (!(execution.State is TaskClearArea work))
            {
                if (!execution.Anchor.Valid) return TaskOutcome("failed","发送时没有有效的清场圆心。");
                work=new TaskClearArea { Center=execution.Anchor.BodyCenter,Radius=execution.Number("radius")*16f,PartialCoverage=Main.netMode==1 };
                execution.State=work;
                if (actor.Fishing!=null) EndFishingActivity(actor);
                var table=TaskNpcTable;
                for(int i=0;table!=null && i<table.Length;i++)
                {
                    var target=table[i];
                    if (target?.active!=true || target.type==NPCID.TargetDummy || !WithinArea(work,target)) continue;
                    bool protectedKind=ProtectedAreaKind(target);
                    if (protectedKind && !execution.Flag("include_friendly")) continue;
                    if (!TaskNpcObserved(target)) { work.PartialCoverage=true; continue; }
                    if (protectedKind && !HasFriendlyAreaWeapon(actor,target)) { work.Protected++; continue; }
                    work.Candidates.Add((target,target.generation));
                }
                work.Travel=new TaskExecution(null) { Actor=actor,Job=execution.Job,Anchor=execution.Anchor,
                    StartedAt=execution.StartedAt,Arguments=new Dictionary<string,object>{["destination"]="here"} };
            }
            if (work.Cursor>=work.Candidates.Count) return AreaResult(work);
            if (Main.GameUpdateCount-execution.StartedAt>=1800)
            { work.Pending+=work.Candidates.Count-work.Cursor; return AreaResult(work); }
            if (!work.Arrived)
            {
                var arrival=TravelTask(work.Travel);
                if (arrival.State=="failed") { work.Pending+=work.Candidates.Count-work.Cursor; return AreaResult(work); }
                if (arrival.State!="succeeded") return arrival;
                work.Arrived=true;
            }
            var item=work.Candidates[work.Cursor];
            var targetNow=item.Body;
            bool same=ReferenceEquals(TaskNpcTable?[targetNow.whoAmI],targetNow) && targetNow.generation==item.Generation;
            if (same && !targetNow.active && targetNow.life<=0) work.Killed++;
            else if (!same || !targetNow.active || !WithinArea(work,targetNow) || !TaskNpcObserved(targetNow)) work.Lost++;
            else if (ProtectedAreaKind(targetNow) && !HasFriendlyAreaWeapon(actor,targetNow)) work.Protected++;
            else if (work.Intent!=null && Main.GameUpdateCount-work.TargetSince>=420) work.Pending++;
            else
            {
                if (work.Intent==null)
                {
                    actor.Combat.Reset();
                    actor.TaskCombat=work.Intent=new TaskCombat { Target=targetNow,Generation=item.Generation,Center=work.Center,
                        Radius=work.Radius,AreaFriendly=ProtectedAreaKind(targetNow) };
                    work.TargetSince=Main.GameUpdateCount;
                }
                return TaskOutcome("running","正在处理本次范围内的目标："+targetNow.FullName,work.Killed);
            }
            ClearAreaIntent(execution.Actor,work);
            work.Cursor++;
            return TaskOutcome("running","已处理一个初始目标。",work.Killed);
        }

        private static CompanionTaskOutcome AreaResult(TaskClearArea work) => TaskOutcome(
            work.Lost+work.Pending>0 || work.PartialCoverage ? "partial" : "succeeded",
            "确认击败 " + work.Killed + "，离界或失去观察 " + work.Lost + "，原版保护或无合法武器 " + work.Protected +
            "，未确认 " + work.Pending + (work.PartialCoverage ? "。当前观察覆盖不足。" : "。本次范围处理结束。"),work.Killed);

        private void CancelAreaCombat(TaskExecution execution)
        {
            if (!(execution.State is TaskClearArea work)) return;
            int confirmed=work.Killed;
            if (work.Intent?.Target is NPC target && !target.active && target.life<=0 && target.generation==work.Intent.Generation &&
                ReferenceEquals(TaskNpcTable?[target.whoAmI],target)) confirmed++;
            ClearAreaIntent(execution.Actor,work);
            if (execution.Issued && execution.Job.State=="cancelled") execution.Settle(TaskOutcome("cancelled","已停止后续攻击。已确认击败 "+confirmed+
                " 个。已发出的攻击仍按原版结算。",confirmed));
        }

        private static void ClearAreaIntent(Actor actor,TaskClearArea work)
        {
            if (work.Intent!=null && ReferenceEquals(actor.TaskCombat,work.Intent))
            { actor.TaskCombat=null; actor.Combat.Reset(); }
            work.Intent=null;
        }

        private static void ObserveClearAreaAttack(Actor actor)
        {
            var step=actor.TaskWork?.Current;
            if (!(step?.State is TaskClearArea work) || work.Intent==null || !actor.Body.ItemAnimationJustStarted) return;
            var hand=actor.Combat.Hand;
            if (hand.Weapon!=null && hand.Slot==actor.Body.selectedItem && ReferenceEquals(hand.Target,work.Intent.Target))
                step.MarkSubmitted();
        }

        private static bool WithinArea(TaskClearArea work,NPC npc) =>
            Vector2.DistanceSquared(work.Center,npc.Center+npc.netOffset)<=work.Radius*work.Radius;

        private static bool ProtectedAreaKind(NPC npc) => npc.friendly || npc.townNPC ||
            NPCID.Sets.IsTownPet[npc.type] || NPCID.Sets.CountsAsCritter[npc.type];

        private bool HasFriendlyAreaWeapon(Actor actor,NPC npc)
        {
            if (!npc.active || npc.life<=0 || npc.immortal || npc.dontTakeDamage || !actor.Body.CanNPCBeHitByPlayerOrPlayerProjectile(npc)) return false;
            for(int slot=0;slot<CompanionBehavior.MainInventorySlots;slot++)
                if (FriendlyAreaWeapon(actor.Body,npc,actor.Body.inventory[slot]) && AttackSourceAvailable(actor,slot)) return true;
            return false;
        }

        private static bool FriendlyAreaWeapon(Player player,NPC npc,Item item)
        {
            if (!CompanionItemRules.WeaponEligible(item) || item.favorited && item.consumable) return false;
            if (!npc.friendly || npc.type==NPCID.Guide && player.killGuide || npc.type==NPCID.Clothier && player.killClothier)
                return true;
            // Native PVE permits Rotten Egg's own projectile and native
            // melee permits Flymeal against eligible town NPCs. Native hit
            // eligibility and protection still apply when the item lands.
            return item.type==ItemID.RottenEgg && item.shoot==ProjectileID.RottenEgg ||
                item.type==ItemID.Flymeal && !item.noMelee && npc.isLikeATownNPC;
        }

        private static bool AreaCombatTarget(Actor actor,NPC target,int generation) => actor.TaskCombat?.AreaFriendly==true &&
            ReferenceEquals(actor.TaskCombat.Target,target) && actor.TaskCombat.Generation==generation && target.active &&
            target.life>0 && !target.dontTakeDamage && !target.immortal && actor.Body.CanNPCBeHitByPlayerOrPlayerProjectile(target);

        private static bool AreaCombatWeapon(Actor actor,NPC target,Item item) => actor.TaskCombat?.AreaFriendly!=true || FriendlyAreaWeapon(actor.Body,target,item);
    }
}
