using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private static bool EmpressMotor(NPC n){return n!=null && n.type==636 && n.aiStyle==120;}
        private static void CaptureEmpressSample(NPC n,ref NpcMotionState state,long session)
        {
            state.EmpressFactsCaptured=true;state.EmpressRelationsComplete=true;
            state.EmpressClock=Main.time;state.EmpressDay=Main.dayTime;state.EmpressDayRate=Main.dayRate;
            state.EmpressRageLatched=NPC.empressRageMode;
            if(!Main.remixWorld || NPC.empressRageMode)return;
            // ShouldEmpressBeEnraged reads the first raw type636, including
            // inactive rows. A stale inactive slot is a scalar anchor; an
            // active first actor requires its actual slot-order motor.
            for(int i=0;i<Main.maxNPCs && i<Main.npc.Length;i++)
            {
                var first=Main.npc[i];if(first==null || first.type!=636)continue;
                state.EmpressFirstIdentity=CombatSelection.Identity(first,session);
                state.EmpressFirstActive=first.active;state.EmpressFirstCenterY=first.Center.Y;break;
            }
        }
        private void CaptureEmpressDependencies(NPC n,long session,ref int queued,ref bool complete)
        {
            if(!Main.remixWorld || NPC.empressRageMode)return;
            for(int i=0;i<Main.maxNPCs && i<Main.npc.Length;i++)
            {
                var first=Main.npc[i];if(first==null || first.type!=636)continue;
                if(first.active && !QueuePlantMotor(i,ref queued))complete=false;
                break;
            }
        }
    }
}
