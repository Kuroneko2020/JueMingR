using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Necessary raw chain observations for the bounded AI6 route. This partial
    // owns no native AI/health/global mutation and no parallel world snapshot.
    internal sealed partial class NpcPredictionSource
    {
        private readonly bool[] wormRequired=new bool[NpcPredictionCache.Capacity+1];
        internal static bool SegmentedWormMotor(NPC n)
        {if(n==null || n.aiStyle!=6)return false;int t=n.type;return t>=87 && t<=92 || t>=117 && t<=119 || t==375 || t==402 || t>=454 && t<=459 || t>=510 && t<=515 || t>=621 && t<=623;}
        private static bool SegmentedWormHead(int t){return t==7 || t==10 || t==39 || t==95 || t==98 || t==87 || t==117 || t==375 || t==402 || t==454 || t==510 || t==513 || t==621;}
        private static bool SegmentedWormTail(int t){return t==9 || t==12 || t==41 || t==97 || t==100 || t==92 || t==119 || t==459 || t==512 || t==515 || t==623;}
        private static NPC WormRaw(float value)
        {if(!Finite(value))return null;int slot=(int)value;return slot>=0 && slot<Main.npc.Length?Main.npc[slot]:null;}
        private static void CaptureSegmentedWormSample(NPC n,ref NpcMotionState state,long session)
        {
            if(n.aiStyle!=6)return;
            var parent=WormRaw(n.ai[1]);var child=WormRaw(n.ai[0]);
            // Raw zero/dummy/inactive are real observations. Their full key is
            // separate from positive-only ParentSlot/ChildSlot compatibility.
            if(SegmentedWormMotor(n))state.WormLinkFactsCaptured=parent!=null && child!=null;
            if(parent!=null)state.ParentIdentity=CombatSelection.Identity(parent,session);
            if(child!=null)state.ChildIdentity=CombatSelection.Identity(child,session);
        }
        private bool SegmentedWormRequired(int slot){return slot>=0 && slot<wormRequired.Length && wormRequired[slot];}
        private void CaptureSegmentedWormDependencies(NPC n,long session,int stateIndex,ref int queued,ref bool captured,ref bool complete)
        {
            if(!captured){Array.Clear(wormRequired,0,wormRequired.Length);captured=true;}
            if(n==null || n.whoAmI<0 || n.whoAmI>=wormRequired.Length){complete=false;return;}
            if(n.aiStyle!=6)return; // A foreign geometry motor owns its usual dependency route.
            // Only a foreign worm acquired by this closure gets raw links;
            // ordinary standalone-worm observations keep their existing route.
            if(!SegmentedWormMotor(n))CaptureSegmentedWormSample(n,ref states[stateIndex],session);
            wormRequired[n.whoAmI]=true;
            bool head=SegmentedWormHead(n.type),tail=SegmentedWormTail(n.type),solitary=n.type==375 || n.type==402;
            bool birth=Main.netMode!=1 && n.ai[0]==0 && (head && !solitary || n.type==118);
            if(birth && !SegmentedWormMotor(n))complete=false; // Unobserved foreign birth cannot be a frozen guard.
            if(solitary)
            {
                // Zero means no link. A positive parent constrains geometry
                // regardless of its style or active flag; active rows need
                // their real motor, while inactive geometry remains a value.
                if(n.ai[1]>0 && n.ai[1]<Main.npc.Length && !QueueWormRaw(n.ai[1],ref queued,true))complete=false;
                if(n.ai[0]>0 && !QueueWormRaw(n.ai[0],ref queued))complete=false;
                return;
            }
            // Native fade reads a parent's alpha even on clients. Ordinary
            // guards/constraint and a deep-retirement walk consume raw links.
            if(!head || n.ai[1]>0 && n.ai[1]<Main.npc.Length)
                if(!QueueWormRaw(n.ai[1],ref queued))complete=false;
            if(!birth && (!tail || n.ai[0]>0))
                if(!QueueWormRaw(n.ai[0],ref queued))complete=false;
        }
        private bool QueueWormRaw(float value,ref int queued,bool geometry=false)
        {
            var row=WormRaw(value);if(row==null || row.whoAmI<0 || row.whoAmI>=wormRequired.Length)return false;
            int slot=row.whoAmI;wormRequired[slot]=true;
            // Existing exact ordinary-worm heads/chains can be required foreign
            // motors. A generic Known(style6) fallback cannot certify Solar412,
            // nor freeze its geometry as a substitute.
            int t=row.type;bool exact=row.aiStyle==6 && (SegmentedWormMotor(row) || t>=7 && t<=12 || t>=39 && t<=41 || t>=95 && t<=100);
            if(row.active && (geometry || row.aiStyle==6 && exact))
            {return slot<motionSlots.Length && QueuePlantMotor(slot,ref queued);}
            if(slot<visited.Length && !visited[slot])
            {if(queued>=pending.Length)return false;visited[slot]=true;pending[queued++]=slot;}
            return !row.active || row.aiStyle!=6;
        }
        private void FinishSegmentedWormDependencies(long session,ref int count,bool captured,bool complete)
        {
            if(!captured)return;
            for(int slot=0;slot<wormRequired.Length && slot<Main.npc.Length;slot++)
            {
                if(!wormRequired[slot] || Main.npc[slot]==null)continue;
                bool found=false;for(int i=0;i<count;i++)if(states[i].Identity.Slot==slot){found=true;break;}
                if(found)continue;if(count>=states.Length){complete=false;continue;}
                // Inactive raw guards and dummy200 are values, not native actors.
                states[count++]=Read(Main.npc[slot],session);
            }
            for(int i=0;i<count;i++)if(SegmentedWormRequired(states[i].Identity.Slot) && SegmentedWormMotor(Main.npc[states[i].Identity.Slot]))
            {states[i].WormRelationsCaptured=true;states[i].WormRelationsComplete=complete;}
        }
    }
}
