using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Closes incoming native594 writers
    // of one needed controlled slime; it does not acquire all world slimes.
    internal sealed partial class NpcPredictionSource
    {
        private readonly bool[] windyRequired=new bool[NpcPredictionCache.Capacity];
        private static bool WindyBalloonMotor(NPC n)
        {return n!=null && (n.type==594 && n.aiStyle==113 || n.type==1 && n.aiStyle==1 && n.ai[0]==-999);}
        private static NPC WindyRaw(int slot)
        {return slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length?Main.npc[slot]:null;}
        private static void CaptureWindyBalloonSample(NPC n,ref NpcMotionState state,long session)
        {
            if(n==null)return;
            if(n.type==594 && n.aiStyle==113)
            {
                state.WindyNetMode=Main.netMode;state.WindySlaveCaptured=true;
                var raw=WindyRaw((int)n.ai[3]);
                state.WindySlaveIdentity=raw==null?default(NpcIdentity):CombatSelection.Identity(raw,session);
                state.WindySlaveValid=raw!=null && raw.active && raw.type==1 && raw.ai[0]==-999;
            }
            if(n.type==1 && n.aiStyle==1)
            {
                // Capture for ordinary type1 too: native release erases -999,
                // and there is no remaining parent marker on a later sample.
                // This is one row's necessary AI001 profile, not a world scan.
                state.WindyNetMode=Main.netMode;state.WindyPreludeCaptured=true;
                state.WindySlimeContainsItems=NPCID.Sets.SlimeCanContainItems[n.type];
                state.WindySlimePositiveValue=n.value>0;state.WindyBaseLifeMax=n.defLifeMax;
                state.WindySlimeVoiceItem=n.ai[1]>0 && n.ai[1]<ItemID.Count && ItemID.Sets.IsAVoiceChangeItem[(int)n.ai[1]];
            }
        }
        private void CaptureWindyBalloonDependencies(NPC actor,long session,ref int queued,ref bool captured,ref bool complete)
        {
            if(!captured){Array.Clear(windyRequired,0,windyRequired.Length);captured=true;}
            if(!WindyBalloonMotor(actor))return;
            if(!QueueWindyMotor(actor.whoAmI,ref queued))complete=false;
            if(actor.type==594)
            {
                // A pending server birth overwrites the old raw AI3 before
                // any GetSlave call. Stop at the real birth, without pulling
                // an unrelated old raw slot into the deterministic closure.
                if(actor.localAI[0]==0 && Main.netMode!=1)return;
                var slave=WindyRaw((int)actor.ai[3]);
                if(slave!=null && slave.active && slave.type==1 && slave.ai[0]==-999 && !QueueWindyMotor(slave.whoAmI,ref queued))complete=false;
                return;
            }
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var parent=Main.npc[slot];
                if(parent==null || !parent.active || parent.type!=594 || parent.aiStyle!=113 || (int)parent.ai[3]!=actor.whoAmI)continue;
                if(parent.localAI[0]==0 && Main.netMode!=1)continue;
                if(!QueueWindyMotor(slot,ref queued))complete=false;
            }
        }
        private bool QueueWindyMotor(int slot,ref int queued)
        {
            var n=WindyRaw(slot);if(n==null || !n.active || slot>=windyRequired.Length)return false;
            windyRequired[slot]=true;return QueuePlantMotor(slot,ref queued);
        }
        private void FinishWindyBalloonDependencies(long session,ref int count,bool captured,bool complete)
        {
            if(!captured)return;
            for(int i=0;i<count;i++)
            {
                int slot=states[i].Identity.Slot;
                if(slot<0 || slot>=windyRequired.Length || !windyRequired[slot])continue;
                states[i].WindyRelationsCaptured=true;states[i].WindyRelationsComplete=complete;
            }
        }
    }
}
