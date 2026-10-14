using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private bool CandidateHandPrepared(PreviewPlan plan)
        {
            var player=combat.Player;var item=player.HeldItem;
            // Book previews advance their animation; native Shoot refreshes
            // the burst/mode separately. They are not this same hand solve.
            return plan?.Prepared==true && plan.PreparedTick==Main.GameUpdateCount && plan.Phase==HostAttackPhase.BeforeNpc && plan.Contact!=null && plan.Ammo!=null && !plan.Controlled && !plan.HadFlight && plan.Flight==null && plan.Delay==0 &&
                HostAttackWindow.Ordinary(item) && !HostSwingAttack.Handles(item) && !HostPhasebladeAttack.Handles(item.shoot) && !HostHarpyAttack.Handles(player,plan.Ammo) && !HostBookStaffAttack.Handles(plan.Ammo.Projectile);
        }
        private bool TryCandidateHand(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,HostAttackPhase phase,int age)
        {
            var plan=candidateHandPlan;candidateHandPlan=null;
            // Candidate membership is not an execution grant. Only this
            // synchronous, completed zero-delay ordinary hand may donate its
            // pure solve; Control/cadence and the native use window ran first.
            if(phase!=HostAttackPhase.BeforeNpc || !CandidateHandPrepared(plan) || !ReferenceEquals(plan.HandOwner,player) || plan.MotionTerrain==null || plan.MotionSource==null ||
                plan.Time!=player.itemTime || plan.Reuse!=player.reuseDelay || !plan.PlayerMotion.SameSample(NpcPredictionSource.ReadPlayer(player)))return false;
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return false;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,true,false);
            if(!PlanInputsCurrent(plan,player,item,timeline.Identity.Slot,phase,age,0,captured,NpcPredictionSource.ReadPlayer(player),receive,Main.npc[timeline.Identity.Slot].immune[player.whoAmI]) || !plan.MotionTerrain.Current)return false;
            NpcTrajectory shared;
            // Both owners must name the same real sample/capture/phase. Compare
            // the entire solved prefix and its assumptions, never just the
            // contact endpoint. A longer tail with changed premises rejects.
            if(!TryCandidatePresentationPrefix(plan,timeline,out shared) || !Equals(plan.Timeline.GravitySources,shared.GravitySources) || !Equals(plan.Timeline.WallSources,shared.WallSources))return false;
            // A Source's epoch is comparable only within that same owner.
            // Separate candidate/selected owners can have different retirement
            // histories. Their fresh same-phase full prefix, future player
            // sources and independent current terrain lease prove this pure
            // hand instead; no trajectory epoch or input permit is rewritten.
            if(ReferenceEquals(plan.MotionSource,observation.Prediction) && plan.Timeline.RelationVersion!=shared.RelationVersion)return false;
            var result=AttackIntercept.Rebind(plan.Contact,shared);if(result==null)return false;
            handTerrain=plan.Terrain.FreezeCells();handMotionTerrain=plan.MotionTerrain;sharedHandOwner=player;
            current=result;environment=plan.Environment;environmentState=plan.EnvironmentState;obstacles=plan.Obstacles;harpyState=plan.HarpyState;
            return true;
        }
    }
}
