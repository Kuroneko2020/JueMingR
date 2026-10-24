using JueMingR.Features.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private void RefreshSkyFacing(Player player,Item item)
        {
            // ItemCheck prepares before automatic facing. These sky origins
            // use the final direction, so a pure turn may reuse the SAME NPC
            // sample only after all other original facts have passed. This is
            // one local launch solve, never a new Source request or allowance.
            if(!ReferenceEquals(item,weapon) || !HostSkyAttack.DirectionDependent(item.type) || player.direction==preparedDirection || Main.GameUpdateCount!=prepared ||
                !balancedOrdinary || !currentSolution.HasPoint || !BalancedInputCurrent(true,false))return;
            var timeline=currentSolution.Timeline;
            current=next=null;currentSolution=nextSolution=default(HostAttackSolution);presentation=null;
            handTerrain=handMotionTerrain=null;sharedHandOwner=null;
            try
            {
                using(observation.PredictionWork.Enter(Main.GameUpdateCount))
                {
                    CombatPredictionWork.Check();terrain.Reset();
                    currentSolution=SolveBalancedHand(player,item,ammo,timeline,origin,new HostAttackClock(timeline,HostAttackPhase.BeforeNpc),terrain,out obstacles);
                    current=currentSolution.Contact;
                    preparedDirection=player.direction;
                    cadence.Prepare(player,item,ammo,timeline,HostAttackPhase.BeforeNpc,currentSolution);
                }
            }
            catch(CombatPredictionBudgetExceededException){current=null;currentSolution=default(HostAttackSolution);}
            catch(System.Exception error){FailLocal(error);}
        }
    }
}
