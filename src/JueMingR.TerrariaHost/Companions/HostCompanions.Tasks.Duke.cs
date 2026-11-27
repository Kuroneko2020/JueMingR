using System;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class DukeFishing
        {
            internal TaskExecution Execution;
            internal int Slot;
            internal Item Bait;
            internal uint Deadline;
            internal bool Sent, NativeSummoned;
        }

        private DukeFishing pendingDuke;
        private int dukeFishingActors;
        internal bool HasDukeFishing => dukeFishingActors != 0;

        private static bool DukeCoastalColumn(int x) => x < 380 || x > Main.maxTilesX - 380;

        private bool DukePresent()
        {
            var table = TaskNpcTable;
            if (table != null)
                foreach (var npc in table)
                    if (npc?.type == NPCID.DukeFishron && TaskNpcObserved(npc)) return true;
            return false;
        }

        private bool TryDukeBait(Actor actor,out int slot)
        {
            var bag = actor.Body.inventory;
            for (int pass=0;pass<2;pass++)
                for (int i=pass==0?54:0;i<(pass==0?58:50);i++)
                {
                    var item = bag[i];
                    if (item?.type != ItemID.TruffleWorm || item.stack <= 0) continue;
                    slot = i;
                    return !item.favorited && SupplyAdmitted(actor,bag,0,i,ownsSelected:true);
                }
            slot = -1;
            return false;
        }

        private bool DukeBaitCurrent(Actor actor,DukeFishing request) => request != null && pendingDuke == null &&
            request.Execution.Job.Active && ReferenceEquals(actor.TaskWork,request.Execution.Job) &&
            ReferenceEquals(actor.TaskWork.Current,request.Execution) && !request.Execution.Job.Paused &&
            TryDukeBait(actor,out int slot) && slot == request.Slot && ReferenceEquals(actor.Body.inventory[slot],request.Bait);

        internal int DukeFishingBaitPower(Actor actor,Item item) => actor.Fishing?.Duke is DukeFishing request &&
            ReferenceEquals(request.Bait,item) && !actor.FishingLine.Ending && DukeBaitCurrent(actor,request) ? item.bait : 0;

        private void FishingActivityBait(Actor actor,out int power,out int type)
        {
            var request = actor.Fishing?.Duke;
            if (request == null) { FishingNativeBait(actor.Body,out power,out type); return; }
            power = DukeFishingBaitPower(actor,request.Bait);
            type = power == 0 ? 0 : ItemID.TruffleWorm;
        }

        private bool DukeEligibleBobber(Actor actor,Projectile b) => actor.Fishing?.Pond is CompanionFishingWater.Pond pond &&
            pond.Liquid == 0 && pond.Count > 1000 && !b.lavaWet && !b.honeyWet && !b.shimmerWet &&
            DukeCoastalColumn(b.Center.ToTileCoordinates().X) && DukeBaitCurrent(actor,actor.Fishing.Duke) && !DukePresent();

        internal DukeFishing AdmitDukeDebit(Actor actor,Projectile b) => actor.FishingLine.Issued == FishingHand.Pull &&
            actor.FishingLine.IssuedAt == Main.GameUpdateCount && ReferenceEquals(actor.FishingLine.CastActivity,actor.Fishing) &&
            FishingActivityOwns(actor,b,out var tracked) && tracked.Pullable && DukeEligibleBobber(actor,b) ? actor.Fishing.Duke : null;

        internal void DukeNativeBaitConsumed(DukeFishing request)
        {
            // The native debit has already returned Truffle Worm. Retain its
            // original task through stop/replace and never authorize a retry.
            pendingDuke = request;
            request.Execution.MarkSubmitted();
            request.Deadline = Main.GameUpdateCount + 300;
            request.Execution.Actor.Dirty = true;
        }

        internal void DukeNativePullFinished(Actor actor)
        {
            if (Main.netMode == 0 && ReferenceEquals(pendingDuke?.Execution.Actor,actor))
                pendingDuke.NativeSummoned = DukePresent();
        }

        internal void ObserveDukeSend(Actor actor,byte[] packet)
        {
            if (ReferenceEquals(pendingDuke?.Execution.Actor,actor) && packet.Length >= 7 && packet[2] == 61 &&
                BitConverter.ToInt16(packet,3) == actor.Body.whoAmI && BitConverter.ToInt16(packet,5) == NPCID.DukeFishron)
                pendingDuke.Sent = true;
        }

        private CompanionTaskOutcome ReadDukeFishing(TaskExecution execution,DukeFishing request)
        {
            if (ReferenceEquals(pendingDuke,request)) return TaskOutcome("waiting","已交付原版松露虫，正在确认猪龙鱼公爵。");
            if (DukePresent()) return TaskOutcome("failed","世界已有猪龙鱼公爵，本步骤未消耗松露虫。");
            if (!DukeBaitCurrent(execution.Actor,request)) return TaskOutcome("failed","选定的松露虫或任务身份已变化。");
            if (Main.GameUpdateCount - execution.StartedAt > 216000)
                return TaskOutcome("failed","未在任务期限内取得合格的原版海岸咬钩。");
            return TaskOutcome("running",execution.Actor.Fishing.Status ?? "正在前往海岸钓位。");
        }

        private void PumpDukeFishing(bool retiring=false)
        {
            var request = pendingDuke;
            if (request == null) return;
            var execution = request.Execution;
            bool sameWorld = execution.Job.Identity.WorldSession == session && ReferenceEquals(world,Main.ActiveWorldFileData);
            bool confirmed = request.NativeSummoned || sameWorld && request.Sent && DukePresent();
            if (!retiring && !confirmed && sameWorld && unchecked((int)(Main.GameUpdateCount - request.Deadline)) < 0) return;
            pendingDuke = null;
            if (ReferenceEquals(execution.Actor.Fishing?.Duke,request)) EndFishingActivity(execution.Actor);
            execution.Settle(confirmed ? TaskOutcome("succeeded","已核对原版召唤后的猪龙鱼公爵。",1) :
                TaskOutcome("unconfirmed","已消耗一条松露虫，未确认猪龙鱼公爵出现。"));
        }
    }
}
