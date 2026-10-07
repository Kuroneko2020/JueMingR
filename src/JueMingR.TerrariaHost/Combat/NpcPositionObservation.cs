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
            else if(n.type==384 && n.aiStyle==72){state.PositionRelation=2;slot=(int)n.ai[0];type=383;}
            else if(n.type==396 && n.aiStyle==79){state.PositionRelation=3;slot=(int)n.ai[3];type=398;}
            else if(n.type==397 && n.aiStyle==78){state.PositionRelation=4;slot=(int)n.ai[3];type=398;state.PositionParameter=n.ai[2]==0?-1:1;}
            else if(n.type==401 && n.aiStyle==82){state.PositionRelation=5;slot=(int)System.Math.Abs(n.ai[0])-1;type=396;}
            else if(n.type==115 && n.aiStyle==29)
            {state.PositionRelation=7;slot=Main.wofNPCIndex;type=113;state.PositionParameter=Main.wofDrawAreaTop+(Main.wofDrawAreaBottom-Main.wofDrawAreaTop)*n.ai[0];if(slot<0)state.ParentSlot=-2;}
            // These controllers use their parent's position/phase to choose
            // velocity; they must never be hard-attached like the four above.
            else if(n.aiStyle==12 && n.type==36){state.PositionRelation=6;slot=(int)n.ai[1];type=35;}
            else if(n.type>=128 && n.type<=131 && n.aiStyle>=33 && n.aiStyle<=36){state.PositionRelation=6;slot=(int)n.ai[1];type=127;}
            if(slot<0 || slot>=Main.maxNPCs)return;var owner=Main.npc[slot];
            if(owner!=null && owner.active && owner.type==type && owner.life>0)
            {
                state.PositionOwner=CombatSelection.Identity(owner,session);
                if(state.PositionRelation==6){state.PositionParameter=owner.ai[1];if(state.Style==12)state.L3=owner.ai[3];}
            }
        }
    }
}
