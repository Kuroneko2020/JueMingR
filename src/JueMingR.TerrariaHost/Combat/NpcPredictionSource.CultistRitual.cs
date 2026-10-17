using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private readonly bool[] ritualRawRequired=new bool[NpcPredictionCache.Capacity];
        private static bool CultistRitualMotor(NPC n)
        {return n!=null && (n.aiStyle==83 && (n.type==437 || n.type==438) || n.aiStyle==3 && (n.type==379 || n.type==380));}
        private static NPC RitualRaw(int slot)
        {return slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length?Main.npc[slot]:null;}
        private static NpcIdentity RitualIdentity(int slot,long session)
        {var n=RitualRaw(slot);return n==null?default(NpcIdentity):CombatSelection.Identity(n,session);}

        // Pure current facts. A raw AI83 owner has no active/type/life gate;
        // its identity must not inherit the narrower AI84 clone contract.
        private static void CaptureCultistRitualSample(NPC n,ref NpcMotionState state,long session)
        {
            if(!CultistRitualMotor(n))return;
            state.CultistNetMode=Main.netMode;
            state.RitualReleasedCanHarm=!n.friendly && n.defDamage>0;
            if(n.type==437)
            {
                if(n.localAI[3]!=0)
                {
                    state.RitualPart0=RitualIdentity((int)n.ai[0]-1,session);
                    state.RitualPart1=RitualIdentity((int)n.localAI[1]-1,session);
                    state.RitualPart2=RitualIdentity((int)n.localAI[2]-1,session);
                    state.RitualPart3=RitualIdentity((int)n.ai[1]-1,session);
                }
                return;
            }
            if(n.type!=438 && n.ai[3]>=0)return;
            int raw=(int)(0f-n.ai[3]-1f);var owner=RitualRaw(raw);
            state.PositionRelation=13;state.PositionOwnerCaptured=true;state.PositionOwnerValid=owner!=null;
            state.PositionOwner=owner==null?default(NpcIdentity):CombatSelection.Identity(owner,session);
        }

        private void CaptureCultistRitualDependencies(NPC actor,long session,ref int queued,ref bool captured,ref bool complete)
        {
            if(!captured){Array.Clear(ritualRawRequired,0,ritualRawRequired.Length);captured=true;}
            if(!CultistRitualMotor(actor))return;
            if(actor.type==438 || (actor.type==379 || actor.type==380) && actor.ai[3]<0)
            {
                int raw=(int)(0f-actor.ai[3]-1f);
                // The one native438 negative-index retirement consumes no row.
                // Other invalid raw indices cannot masquerade as an owner.
                if(!(actor.type==438 && raw==-1) && !QueueRitualRaw(raw,ref queued))complete=false;
            }
            if(actor.type==437)
            {
                // Client never performs release/all-dead/birth writes. A
                // server's first action must observe its real four new slots
                // before any later registry consumer is allowed to continue.
                if(Main.netMode==1 || actor.localAI[3]==0 || actor.ai[0]==-1 && actor.localAI[0]!=1)return;
                if(!QueueRitualRaw((int)actor.ai[0]-1,ref queued))complete=false;
                if(!QueueRitualRaw((int)actor.localAI[1]-1,ref queued))complete=false;
                if(!QueueRitualRaw((int)actor.localAI[2]-1,ref queued))complete=false;
                if(!QueueRitualRaw((int)actor.ai[1]-1,ref queued))complete=false;
            }
            if(Main.netMode==1 || actor.type!=379 && actor.type!=380)return;
            // Tablet release writes the two raw outer slots without checking
            // their type/active state. A still-pending foreign tablet can thus
            // retarget an actual replacement archer in that slot. Acquire only
            // incoming writers of this necessary motor, not every tablet.
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var tablet=Main.npc[slot];if(tablet==null || !tablet.active || tablet.type!=437 || tablet.aiStyle!=83 || tablet.localAI[3]==0)continue;
                if((int)tablet.ai[0]-1!=actor.whoAmI && (int)tablet.ai[1]-1!=actor.whoAmI)continue;
                if(tablet.localAI[0]!=1 && !(tablet.localAI[0]==0 && RitualWakePending(slot)))continue;
                if(!QueueRitualRaw(slot,ref queued))complete=false;
                if(tablet.localAI[0]==0 && !QueueRitualWakeWitnesses(slot,ref queued))complete=false;
            }
        }

        private static bool RitualWakePending(int tabletSlot)
        {
            // Existing JustHit is already sampled input. Unknown future hits
            // are excluded by NoNewHits and do not justify a world closure.
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var n=Main.npc[slot];if(n==null || !n.active || !n.justHit)continue;
                bool prelude=n.type==438 && n.aiStyle==83 || (n.type==379 || n.type==380) && n.aiStyle==3 && n.ai[3]<0;
                if(prelude && (int)(0f-n.ai[3]-1f)==tabletSlot)return true;
            }
            return false;
        }
        private bool QueueRitualWakeWitnesses(int tabletSlot,ref int queued)
        {
            bool complete=true;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var n=Main.npc[slot];if(n==null || !n.active || !n.justHit)continue;
                bool prelude=n.type==438 && n.aiStyle==83 || (n.type==379 || n.type==380) && n.aiStyle==3 && n.ai[3]<0;
                if(prelude && (int)(0f-n.ai[3]-1f)==tabletSlot && !QueueRitualRaw(slot,ref queued))complete=false;
            }
            return complete;
        }
        private bool QueueRitualRaw(int slot,ref int queued)
        {
            var n=RitualRaw(slot);if(n==null || slot>=ritualRawRequired.Length)return false;
            ritualRawRequired[slot]=true;
            if(n.active)return QueuePlantMotor(slot,ref queued);
            if(visited[slot])return true;
            if(queued>=pending.Length)return false;
            visited[slot]=true;pending[queued++]=slot;return true;
        }
        private void FinishCultistRitualDependencies(long session,ref int count,bool captured,bool complete)
        {
            if(!captured)return;
            // The main pending loop intentionally skips inactive actors. Their
            // raw full identities/geometry/active facts remain native inputs.
            for(int slot=0;slot<ritualRawRequired.Length;slot++)if(ritualRawRequired[slot])
            {
                bool present=false;for(int i=0;i<count;i++)if(states[i].Identity.Slot==slot){present=true;break;}
                if(present)continue;var raw=RitualRaw(slot);
                if(raw==null || count>=states.Length){complete=false;continue;}
                states[count++]=Read(raw,session);visited[slot]=true;
            }
            for(int i=0;i<count;i++)
            {
                int slot=states[i].Identity.Slot;
                if(!motionSlots[slot] || !CultistRitualMotor(RitualRaw(slot)))continue;
                states[i].RitualRelationsCaptured=true;states[i].RitualRelationsComplete=complete;
            }
        }
    }
}
