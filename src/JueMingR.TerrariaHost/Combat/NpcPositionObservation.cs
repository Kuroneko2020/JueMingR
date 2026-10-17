using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class NpcPositionObservation
    {
        internal static void Capture(NPC n,ref NpcMotionState state,long session)
        {
            int slot=-1,type=-1;
            if(n.type==114 && n.aiStyle==28)
            {state.PositionRelation=1;slot=Main.wofNPCIndex;type=113;int mid=(Main.wofDrawAreaBottom+Main.wofDrawAreaTop)/2;state.PositionParameter=(mid+(n.ai[0]>0?Main.wofDrawAreaTop:Main.wofDrawAreaBottom))*.5f-n.height/2;}
            else if(n.type==384 && n.aiStyle==72)
            {
                state.PositionRelation=2;slot=(int)n.ai[0];
                if(slot<0 || slot>=Main.maxNPCs || slot>=Main.npc.Length || Main.npc[slot]==null)return;
                var parent=Main.npc[slot];state.PositionOwnerCaptured=true;
                // AI72 tests active/type, not Life. An inactive or changed-type
                // owner is a known native retirement; a replaced generation is
                // still a fresh dependency and must be observed before reuse.
                state.PositionOwnerValid=parent.active && parent.type==383;
                if(state.PositionOwnerValid)state.PositionOwner=CombatSelection.Identity(parent,session);
                return;
            }
            else if(n.type==328 && n.aiStyle==59)
            {
                // AI59 clamps its numeric parent slot, then checks only active
                // and AI58. Life zero remains valid; generations stay distinct.
                state.PositionRelation=10;state.PositionOwnerCaptured=true;
                slot=(int)System.Math.Max(0,System.Math.Min(n.ai[1],Main.maxNPCs-1));
                if(slot<Main.npc.Length)
                {var parent=Main.npc[slot];state.PositionOwnerValid=parent!=null && parent.active && parent.aiStyle==58;if(state.PositionOwnerValid)state.PositionOwner=CombatSelection.Identity(parent,session);}
                return;
            }
            else if(n.type==396 && n.aiStyle==79 || n.type==397 && n.aiStyle==78)
            {
                state.PositionRelation=n.type==396?3:4;slot=(int)n.ai[3];state.PositionOwnerCaptured=true;
                if(n.type==397)state.PositionParameter=n.ai[2]==0?-1:1;
                // Both Moon parts test the raw core's activity and type;
                // its scheduled zero-life retirement has not necessarily run.
                if(slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length)
                {var parent=Main.npc[slot];state.PositionOwnerValid=parent!=null && parent.active && parent.type==398;if(state.PositionOwnerValid)state.PositionOwner=CombatSelection.Identity(parent,session);}
                return;
            }
            else if(n.type==440 && n.aiStyle==84)
            {
                // The clone follows an active type439 owner, including a
                // zero-life row before that owner's own native update.
                state.PositionRelation=12;state.PositionOwnerCaptured=true;slot=(int)n.ai[3];
                if(slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length)
                {var parent=Main.npc[slot];state.PositionOwnerValid=parent!=null && parent.active && parent.type==439;if(state.PositionOwnerValid)state.PositionOwner=CombatSelection.Identity(parent,session);}
                return;
            }
            else if(n.type==401 && n.aiStyle==82){state.PositionRelation=5;slot=(int)System.Math.Abs(n.ai[0])-1;type=396;}
            else if(n.type==438 && n.aiStyle==83 || (n.type==379 || n.type==380) && n.aiStyle==3 && n.ai[3]<0)
            {
                // The ritual prelude reads its encoded raw slot without an
                // active/type/life gate. Invalid indices remain missing input.
                state.PositionRelation=13;state.PositionOwnerCaptured=true;slot=(int)(0f-n.ai[3]-1f);
                if(slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length)
                {var parent=Main.npc[slot];state.PositionOwnerValid=parent!=null;if(parent!=null)state.PositionOwner=CombatSelection.Identity(parent,session);}
                return;
            }
            else if(n.type==115 && n.aiStyle==29)
            {state.PositionRelation=7;slot=Main.wofNPCIndex;type=113;state.PositionParameter=Main.wofDrawAreaTop+(Main.wofDrawAreaBottom-Main.wofDrawAreaTop)*n.ai[0];if(slot<0)state.ParentSlot=-2;}
            // These controllers use their parent's position/phase to choose
            // velocity; they must never be hard-attached like the four above.
            else if(n.aiStyle==12 && n.type==36)
            {
                state.PositionRelation=6;slot=(int)n.ai[1];
                if(slot<0 || slot>=Main.maxNPCs || slot>=Main.npc.Length || Main.npc[slot]==null)return;
                var head=Main.npc[slot];state.PositionOwnerCaptured=true;state.PositionInvalidRetireImmediately=Main.netMode!=2;
                // Native checks activity and AI11, including Dungeon Guardian
                // and an active zero-life row; it does not require type35.
                state.PositionOwnerValid=head.active && head.aiStyle==11;
                if(state.PositionOwnerValid){state.PositionOwner=CombatSelection.Identity(head,session);state.PositionParameter=head.ai[1];state.L3=head.ai[3];}
                return;
            }
            else if(n.type>=128 && n.type<=131 && n.aiStyle>=33 && n.aiStyle<=36){state.PositionRelation=6;slot=(int)n.ai[1];type=127;}
            else if(n.aiStyle==75 && (n.type==390 || n.type==392 || n.type==393 || n.type==394 || n.type==416 || n.type==492))
            {
                state.PositionRelation=11;slot=(int)n.ai[0];type=n.type==390?391:n.type==416?415:n.type==492?491:395;
                state.PositionOwnerCaptured=true;
                if(slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length)
                {var parent=Main.npc[slot];state.PositionOwnerValid=parent!=null && parent.active && parent.type==type;if(state.PositionOwnerValid)state.PositionOwner=CombatSelection.Identity(parent,session);}
                return; // Native owner validity does not test Life>0.
            }
            else if(n.type==111 && n.aiStyle==3 && n.ai[3]<0)
            {
                // A hit always releases before any owner read. Current near
                // geometry alone cannot release the next action: the real
                // player's earlier update may cross the 200px boundary.
                if(state.JustHit)return;
                state.PositionRelation=8;slot=(int)(-n.ai[3]-1);
            }
            if(slot<0 || slot>=Main.maxNPCs)return;var owner=Main.npc[slot];
            if(owner!=null && owner.active && (type<0 || owner.type==type) && owner.life>0)
            {
                state.PositionOwner=CombatSelection.Identity(owner,session);
                if(state.PositionRelation==6){state.PositionParameter=owner.ai[1];if(state.Style==12)state.L3=owner.ai[3];}
            }
        }
    }
}
