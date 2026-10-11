using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked Terraria1.4.5.8 AI024 pure observation. The native owl scan needs
    // future identities/geometry, never a frozen CanPerch or town position.
    internal sealed partial class NpcPredictionSource
    {
        private readonly bool[] birdRequired=new bool[NpcPredictionCache.Capacity];
        private readonly bool[] birdGeometryKnown=new bool[NpcPredictionCache.Capacity];
        internal static bool BirdMotor(NPC n)
        {return n!=null && n.aiStyle==24 && (n.type==74 || n.type==297 || n.type==298 || n.type==442 || n.type==611 || n.type>=671 && n.type<=675 || n.type==689);}
        private static bool BirdOwl(NPC n){return BirdMotor(n) && (n.type==611 || n.type==689);}
        private static NPC BirdRaw(int slot)
        {return slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length?Main.npc[slot]:null;}
        private static void CaptureBirdSample(NPC n,ref NpcMotionState state,long session)
        {
            if(!BirdMotor(n))return;
            state.BirdFactsCaptured=true;state.BirdNetMode=Main.netMode;state.BirdReleaseOwner=n.releaseOwner;
            var raw=n.target>=0 && n.target<Main.player.Length?Main.player[n.target]:null;
            state.BirdRawTargetCaptured=raw!=null;state.BirdRawTargetDead=raw!=null && raw.dead;
            // Actual SetDefaults317 samples this multiplier, unlike the
            // caller's689 critter life. No live Transform/SetDefaults here.
            if(n.type==689)state.KnownFormLifeMax=(int)(75f*Terraria.DataStructures.GameDifficultyData.EnemyMaxLifeMultiplier.Sample(Main.Difficulty));
        }
        private void CaptureBirdDependencies(NPC actor,long session,ref int queued,ref bool captured,ref bool complete)
        {
            if(!captured){Array.Clear(birdRequired,0,birdRequired.Length);Array.Clear(birdGeometryKnown,0,birdGeometryKnown.Length);captured=true;}
            if(!BirdMotor(actor))return;
            if(!QueueBirdMotor(actor.whoAmI,ref queued))complete=false;
            // Day/cloud input is a captured world condition. At dry night
            // an owl's ordinary landing/wake path can reach the same scan;
            // do not capture only the currently observedAI0==1 sample.
            if(!BirdOwl(actor) || Main.dayTime || Main.cloudAlpha!=0)return;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var row=Main.npc[slot];
                if(row==null || !row.active || slot==actor.whoAmI || !row.townNPC && row.type!=actor.type)continue;
                if(row.townNPC && !TownCritter(row))
                {
                    // Unknown town geometry is NOT a frozen motor. Keep its
                    // current full identity/health and an explicit unknown
                    // geometry marker. Only the original census consumer may
                    // reject it; owl initialization/landing/wet prefixes must
                    // not fail merely because this row was acquired.
                    birdRequired[slot]=true;birdGeometryKnown[slot]=false;
                    if(!visited[slot]){if(queued>=pending.Length){complete=false;continue;}visited[slot]=true;pending[queued++]=slot;}
                }
                else if(!QueueBirdMotor(slot,ref queued))complete=false;
            }
        }
        private bool QueueBirdMotor(int slot,ref int queued)
        {
            var row=BirdRaw(slot);if(row==null || !row.active || slot>=birdRequired.Length)return false;
            birdRequired[slot]=birdGeometryKnown[slot]=true;return QueuePlantMotor(slot,ref queued);
        }
        private void FinishBirdDependencies(long session,ref int count,bool captured,bool complete)
        {
            if(!captured)return;
            for(int i=0;i<count;i++)
            {
                int slot=states[i].Identity.Slot;
                if(slot<0 || slot>=birdRequired.Length || !birdRequired[slot])continue;
                // On the selected bird this means structural completeness;
                // on an unknown town row it means geometry is unavailable.
                // The kernel checks that row BEFORE reading its Center.
                states[i].BirdRelationsCaptured=true;states[i].BirdRelationsComplete=complete && birdGeometryKnown[slot];
            }
        }
        internal static bool BirdTrackingQuery(NPC n,IPredictionTerrain terrain,out int queryType,out float queryX,out float queryY)
        {
            queryType=n.type;queryX=n.Center.X;queryY=n.Center.Y;
            if(n.type!=689 || n.aiStyle!=24 || Main.netMode==1 || Main.dayTime)return false;
            // FindClosest is a pure native scalar search, not TargetClosest:
            // no aggro adjustment, active&&!dead, ghost included, fallback0.
            int closest=Player.FindClosest(n.position,n.width,n.height);var p=Main.player[closest];
            if(p==null)return false;
            float dx=n.Center.X-p.Center.X,dy=n.Center.Y-p.Center.Y;
            float distance=(float)Math.Sqrt((double)dx*dx+(double)dy*dy);
            if(distance>=80 || terrain==null)return false;
            bool clear;PredictionStop stop;
            if(!terrain.CanHit(new MotionRect(n.position.X,n.position.Y,n.width,n.height),new MotionRect(p.position.X,p.position.Y,p.width,p.height),out clear,out stop) || !clear)return false;
            // Transform keeps X and Bottom. Its TargetClosest and the
            // remaining AI024 initialization both read the NEW317 center.
            queryType=317;queryX=n.position.X+15;queryY=n.position.Y+n.height-16;return true;
        }
    }
}
