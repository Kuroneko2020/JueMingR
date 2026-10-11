using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private static bool CasterMotor(NPC n)
        {
            if(n==null || n.aiStyle!=8)return false;
            switch(n.type)
            {case 24:case 29:case 32:case 45:case 172:case 281:case 282:case 283:case 284:case 285:case 286:case 533:case 693:return true;default:return false;}
        }

        // These two native queries consume active/type, without a life gate.
        // Query each census once per acquisition, then use the existing MOTOR
        // queue and its Skeleton/Wall closure. A live row is never a frozen
        // presence sample; future actions retain the original NPC slot order.
        private void CaptureCasterDependencies(NPC n,ref int queued,ref bool skeletonQueried,ref bool skeletonComplete,ref bool wallQueried,ref bool wallComplete)
        {
            if(n.type==32 && n.localAI[3]!=1 && !skeletonQueried)
            {skeletonQueried=true;CaptureCasterType(35,ref queued,ref skeletonComplete);}
            if(n.type==24 && Main.getGoodWorld && !wallQueried)
            {wallQueried=true;CaptureCasterType(113,ref queued,ref wallComplete);}
        }
        private void CaptureCasterType(int type,ref int queued,ref bool complete)
        {
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                if(slot>=Main.npc.Length){complete=false;break;}
                var actor=Main.npc[slot];
                if(actor!=null && actor.active && actor.type==type && !QueuePlantMotor(slot,ref queued))complete=false;
            }
        }

        // Called before sorting/Tracking, when motionSlots is the acquisition
        // owner. Health-only foreign caster rows do not gain a motor proof.
        // An empty, completely sampled census is a valid negative result.
        private void FinishCasterDependencies(int count,bool skeletonQueried,bool skeletonComplete,bool wallQueried,bool wallComplete)
        {
            for(int i=0;i<count;i++)
            {
                int slot=states[i].Identity.Slot;
                if(slot<0 || slot>=motionSlots.Length || !motionSlots[slot] || !CasterMotor(Main.npc[slot]))continue;
                var n=Main.npc[slot];bool complete=true;
                if(n.type==32 && n.localAI[3]!=1)complete&=skeletonQueried && skeletonComplete;
                if(n.type==24 && Main.getGoodWorld)complete&=wallQueried && wallComplete;
                states[i].CasterRelationsCaptured=true;states[i].CasterRelationsComplete=complete;
            }
        }
    }
}
