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
            if(!ReferenceEquals(item,weapon) || !HostSkyAttack.DirectionDependent(item.type) || player.direction==preparedDirection || Main.GameUpdateCount!=prepared || current==null || !ValidOrdinaryFacts(true,false))return;
            var timeline=current.Timeline;int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-1;
            current=next=null;presentation=null;
            try
            {
                using(observation.PredictionWork.Enter(Main.GameUpdateCount))
                {
                    CombatPredictionWork.Check();terrain.Reset();
                    current=Solve(player,item,ammo,timeline,origin,age,true);
                    preparedDirection=player.direction;
                    cadence.Prepare(player,item,ammo,timeline,HostAttackPhase.BeforeNpc,current);
                }
            }
            catch(CombatPredictionBudgetExceededException){current=null;}
            catch(System.Exception error){FailLocal(error);}
        }
    }
}
