using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private static bool CultistMotor(NPC n)
        {return n!=null && n.aiStyle==84 && (n.type==439 || n.type==440);}

        private void CaptureCultistDependencies(NPC selected,long session,ref int queued,ref bool captured,ref bool complete,ref bool dooms,ref bool doomsComplete)
        {
            captured=true;var owner=selected;
            if(selected.type==440)
            {
                int slot=(int)selected.ai[3];
                if(slot<0 || slot>=Main.maxNPCs || slot>=Main.npc.Length)return;
                owner=Main.npc[slot];
                if(owner==null || !owner.active || owner.type!=439)return;
            }
            if(!QueuePlantMotor(owner.whoAmI,ref queued))complete=false;
            var observed=Read(owner,session);
            var environment=new PredictionEnvironment{Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
            var health=new List<NpcMotionState>{observed};
            if(Main.expertMode && observed.Life>observed.LifeMax/2)for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var contributor=Main.npc[slot];
                if(slot==owner.whoAmI || contributor==null || !contributor.active || contributor.realLife!=owner.whoAmI)continue;
                // Existing shared-life DOT can cross half-health before the
                // owner reaches its count choice. Sample health only, without
                // promoting this otherwise unrelated contributor's motor.
                health.Add(Read(contributor,session));
                if(!QueueCultistHealth(slot,ref queued))doomsComplete=false;
            }
            // A global count has no effect during an ordinary cast. Its actual
            // earliest scheduled consumer, including observed health loss,
            // decides whether any global Doom dependencies are needed at all.
            dooms=NpcMotion.CultistDoomCountAction(observed,environment,Cache.Required,health.ToArray(),health.Count)<=Cache.Required;
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                if(slot>=Main.npc.Length){complete=doomsComplete=false;break;}
                var member=Main.npc[slot];if(member==null || !member.active)continue;
                if(member.type==440 && member.ai[3]==owner.whoAmI)
                {if(!QueuePlantMotor(slot,ref queued))complete=false;}
                if(!dooms)continue;
                if(member.type==523)
                {if(!QueuePlantMotor(slot,ref queued))doomsComplete=false;}
                else if(member.type==439)
                {
                    // A foreign boss supplies health and a bounded possible
                    // count-change deadline, never permission to replay its AI.
                    if(!QueueCultistHealth(slot,ref queued))doomsComplete=false;
                }
                if(member.realLife>=0 && member.realLife<Main.maxNPCs && member.realLife<Main.npc.Length)
                {
                    var shared=Main.npc[member.realLife];
                    if(shared!=null && shared.active && shared.type==439 && !QueueCultistHealth(slot,ref queued))doomsComplete=false;
                }
            }
        }

        private bool QueueCultistHealth(int slot,ref int queued)
        {
            if(slot<0 || slot>=visited.Length)return false;
            if(visited[slot])return true;
            if(queued>=pending.Length)return false;
            visited[slot]=true;pending[queued++]=slot;return true;
        }

        private void FinishCultistDependencies(int count,bool complete,bool dooms,bool doomsComplete)
        {
            var environment=new PredictionEnvironment{Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
            int ownerSlot=-1;
            for(int i=0;i<count;i++)if(motionSlots[states[i].Identity.Slot] && states[i].EffectiveType==439 && states[i].Style==84)
                ownerSlot=states[i].Identity.Slot;
            int foreignBirth=int.MaxValue;
            if(dooms)for(int i=0;i<count;i++)
            {
                var row=states[i];
                if(row.EffectiveType==523)
                {
                    // AI101 reads its real owner's Active flag as well as
                    // health. A foreign health-only boss can retire in its AI;
                    // its future children are not a certified >=10 lower bound.
                    int owner=row.PositionOwner.Slot;
                    if(row.PositionOwner.Token!=null && (owner<0 || owner>=motionSlots.Length || !motionSlots[owner]))doomsComplete=false;
                }
                if(row.EffectiveType!=439 || motionSlots[row.Identity.Slot])continue;
                int action=NpcMotion.CultistDoomBirthAction(row,environment,Cache.Required,states,count);
                if(action!=int.MaxValue && row.Identity.Slot>ownerSlot)action++;
                foreignBirth=Math.Min(foreignBirth,action);
            }
            for(int i=0;i<count;i++)if(motionSlots[states[i].Identity.Slot] && CultistMotor(Main.npc[states[i].Identity.Slot]))
            {
                states[i].CultistRelationsCaptured=true;states[i].CultistRelationsComplete=complete;
                states[i].CultistDoomsCaptured=dooms;states[i].CultistDoomsComplete=doomsComplete;
                states[i].CultistForeignDoomBirthAt=foreignBirth;
            }
        }
    }
}
