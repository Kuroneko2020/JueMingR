using System;
using System.Collections.Generic;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private readonly Dictionary<int,int> npcDependencies=new Dictionary<int,int>();
        private int observedWallHead=-1;
        private bool RequiredNpcPacket(byte[] packet,int start,int length)
        {
            // The wall eye carries no root index. A requested native8 supplies
            // its mouth in the same completed snapshot. Admit only that one
            // native netID, including when its geometry lies outside interest.
            if(!RequiredNpcSlot(-113) || length<25 || packet[start]!=23)return false;
            int offset=23;byte flags=packet[start+21];for(int bit=2;bit<6;bit++)if((flags&(1<<bit))!=0)offset+=4;
            return offset+2<=length && BitConverter.ToInt16(packet,start+offset)==113;
        }
        private static int NpcRootFamily(int type)
        {
            // AI_006's shared-health chains; Eater13..15 deliberately owns
            // separate segment health. Destroyer uses the same actual ai3 /
            // previous-child relation in its AI_037 producer.
            if(type>=7 && type<=9)return 7;if(type>=10 && type<=12)return 10;
            if(type>=39 && type<=41)return 39;if(type>=87 && type<=92)return 87;
            if(type>=95 && type<=97)return 95;if(type>=98 && type<=100)return 98;
            if(type>=117 && type<=119)return 117;if(type>=134 && type<=136)return 134;
            if(type>=402 && type<=404)return 402;if(type>=454 && type<=459)return 454;
            if(type>=510 && type<=512)return 510;if(type>=513 && type<=515)return 513;
            if(type>=621 && type<=623)return 621;return -1;
        }
        private bool RequiredNpcSlot(int slot)
        {int count;return npcDependencies.TryGetValue(slot,out count) && count>0 || slot>=0 && slot==observedWallHead && npcDependencies.TryGetValue(-113,out count) && count>0;}
        private void AddNpcDependency(int slot){int count;npcDependencies.TryGetValue(slot,out count);npcDependencies[slot]=count+1;}
        private void RemoveNpcDependency(int slot){int count;if(npcDependencies.TryGetValue(slot,out count)){if(count<=1)npcDependencies.Remove(slot);else npcDependencies[slot]=count-1;}}
        private void ReleaseNpcDependencies(NpcState state)
        {
            if(state.WallDependency){RemoveNpcDependency(-113);state.WallDependency=false;}
            if(state.DependenciesHeld){RemoveNpcDependency(state.RootSlot);RemoveNpcDependency(state.PreviousSlot);state.DependenciesHeld=false;}
            state.Root=state.Previous=null;
        }
        private void ReleaseNpcDependencies(int slot)
        {if(npcStates!=null && npcStates[slot]!=null){ReleaseNpcDependencies(npcStates[slot]);npcStates[slot].RootRefreshIssued=false;npcStates[slot].PhaseReady=npcStates[slot].PhysicsKnown=npcStates[slot].BattleReady=npcStates[slot].ArmedHistory=false;}}
        private static int NpcLink(float value,int self)
        {return value>=0 && value<Main.maxNPCs && value==(int)value && value!=self?(int)value:-1;}
        private bool NpcBaselineCurrent(int slot)
        {WorldLease lease;return npcSources.TryGetValue(slot,out lease) && lease.NpcBaseline && !lease.Waiting && !lease.AwaitBaseline;}
        private bool SameNpcSnapshot(NpcState a,NpcState b)
        {return b!=null && a.SnapshotComplete && b.SnapshotComplete && a.Snapshot!=0 && a.Snapshot==b.Snapshot && ReferenceEquals(a.SnapshotSource,b.SnapshotSource);}
        private bool BoundNpcRoot(NPC npc,NpcState state)
        {
            if(npc.type==114)return state.Root!=null && state.RootSlot>=0 && ReferenceEquals(npcStates[state.RootSlot]?.Body,state.Root) && state.Root.active && state.Root.type==113 && state.Root.generation==state.RootGeneration && NpcBaselineCurrent(state.RootSlot);
            int family=NpcRootFamily(npc.type);
            // During a synchronous hit the array temporarily holds private
            // views. The observation owner's identity records stay canonical;
            // never mistake that legitimate borrow for a new root generation.
            return state.Root!=null && state.Previous!=null && ReferenceEquals(npcStates[state.RootSlot]?.Body,state.Root) && ReferenceEquals(npcStates[state.PreviousSlot]?.Body,state.Previous) &&
                state.Root.generation==state.RootGeneration && state.Previous.generation==state.PreviousGeneration && state.Root.active && state.Root.type==family && state.Previous.active && NpcRootFamily(state.Previous.type)==family &&
                state.Previous.ai[0]==npc.whoAmI && state.Previous.ai[3]==state.RootSlot && NpcBaselineCurrent(state.RootSlot) && NpcBaselineCurrent(state.PreviousSlot);
        }
        private void PrepareNpcRoot(NPC npc,NpcState state,bool decoded)
        {
            if(npc.type==113){observedWallHead=npc.whoAmI;npc.realLife=npc.whoAmI;return;}
            if(npc.type==114){PrepareWallRoot(npc,state);return;}
            // These native linked-health families use ai3. Other ai3
            // values are NOT indices. A source's completed full enumeration
            // establishes the head/previous/body identities together, including
            // root slot0. A later reused head cannot inherit an old binding.
            if(npc.aiStyle!=6 && npc.aiStyle!=37)return;
            int family=NpcRootFamily(npc.type);if(family<0)return;if(npc.type==family){npc.realLife=npc.whoAmI;return;}
            if(!npc.active || npc.life<=0){ReleaseNpcDependencies(state);npc.realLife=-1;return;}
            int root=NpcLink(npc.ai[3],npc.whoAmI),previous=NpcLink(npc.ai[1],npc.whoAmI);
            if(root!=state.RootSlot || previous!=state.PreviousSlot)
            {ReleaseNpcDependencies(state);state.RootSlot=root;state.PreviousSlot=previous;state.RootRefreshIssued=false;}
            if(root<0 || previous<0){npc.realLife=-1;state.BeforeRootBlock=npc.dontTakeDamage;state.RootBlocked=npc.dontTakeDamage=true;return;}
            if(!state.DependenciesHeld && NeededNpc(npc.Center))
            {AddNpcDependency(root);AddNpcDependency(previous);state.DependenciesHeld=true;}
            if(!BoundNpcRoot(npc,state))
            {
                if(state.Root!=null){state.Root=state.Previous=null;state.RootRefreshIssued=false;}
                var head=observedNpcs[root];var before=observedNpcs[previous];var headState=npcStates[root];var beforeState=npcStates[previous];
                if(head.active && head.type==family && before.active && NpcRootFamily(before.type)==family && before.ai[0]==npc.whoAmI && before.ai[3]==root &&
                    NpcBaselineCurrent(root) && NpcBaselineCurrent(previous) && SameNpcSnapshot(state,headState) && SameNpcSnapshot(state,beforeState))
                {state.Root=head;state.RootGeneration=head.generation;state.Previous=before;state.PreviousGeneration=before.generation;}
            }
            bool bound=BoundNpcRoot(npc,state);npc.realLife=bound?root:-1;
            if(!bound){state.BeforeRootBlock=npc.dontTakeDamage;state.RootBlocked=npc.dontTakeDamage=true;}
            if(bound || !state.DependenciesHeld || state.RootRefreshIssued)return;
            var flow=Flow(state.Source);
            // Initial Ready buffers and an in-flight native8 may still contain
            // the other slots. Finish that actual FIFO baseline first. One
            // subsequent coalesced request per binding can repair a head which
            // was filtered before its body's dependency became known. Absence
            // after that request does not cause a per-frame refresh loop.
            if(flow.Snapshot || state.Source!=null && state.Source.InitialNpc.Count>0)return;
            if(!SourceAlive(state.Source))return;state.RootRefreshIssued=true;
            long barrier=flow.Outstanding?flow.Sent:flow.Sent+1;
            foreach(int slot in new[]{npc.whoAmI,root,previous})
            {
                WorldLease lease;if(!npcSources.TryGetValue(slot,out lease)){lease=new WorldLease();npcSources.Add(slot,lease);}
                lease.Pending=state.Source;lease.Waiting=lease.AwaitBaseline=lease.RequiredBaseline=true;lease.Barrier=barrier;
            }
            if(!flow.Outstanding)flow.Requested=true;
        }
        internal bool ObservedNpcCombatReady(NPC npc)
        {
            if(npc.type==482 && observedNpcs!=null)
            {var battle=npcStates?[npc.whoAmI];return battle!=null && battle.BattleReady && battle.Body.active && npc.generation==battle.Generation && npc.netID==battle.NetId && NpcBaselineCurrent(npc.whoAmI);}
            if((LocalNpcPhase(npc) && npc.aiStyle!=126 || npc.aiStyle==19) && observedNpcs!=null)
            {var phase=npcStates?[npc.whoAmI];return phase!=null && (npc.aiStyle==19?phase.PhysicsKnown:phase.PhaseReady) && phase.Body.active && npc.generation==phase.Generation && npc.netID==phase.NetId && NpcBaselineCurrent(npc.whoAmI);}
            if(npc.type==114 && observedNpcs!=null && ReferenceEquals(observedNpcs[npc.whoAmI],npc))return npcStates?[npc.whoAmI]!=null && BoundNpcRoot(npc,npcStates[npc.whoAmI]);
            if(npc.aiStyle!=6 && npc.aiStyle!=37)return true;
            int family=NpcRootFamily(npc.type);
            if(observedNpcs==null || npc.whoAmI<0 || npc.whoAmI>=observedNpcs.Length || !ReferenceEquals(observedNpcs[npc.whoAmI],npc) || family<0 || npc.type==family)return true;
            var state=npcStates?[npc.whoAmI];return state!=null && BoundNpcRoot(npc,state);
        }
        internal bool NpcTilePhysicsKnown(NPC npc)
        {return npc.aiStyle!=126 || observedNpcs==null || npcStates?[npc.whoAmI]!=null && npcStates[npc.whoAmI].PhysicsKnown;}
        private void PrepareWallRoot(NPC npc,NpcState state)
        {
            if(!npc.active || npc.life<=0){ReleaseNpcDependencies(state);npc.realLife=-1;return;}
            int root=observedWallHead>=0 && npcStates[observedWallHead]?.Body.type==113?observedWallHead:-1;
            if(root!=state.RootSlot){ReleaseNpcDependencies(state);state.RootSlot=state.PreviousSlot=root;state.RootRefreshIssued=false;}
            if(NeededNpc(npc.Center) && !state.WallDependency){AddNpcDependency(-113);state.WallDependency=true;}
            if(root>=0 && !state.DependenciesHeld && state.WallDependency){AddNpcDependency(root);AddNpcDependency(root);state.DependenciesHeld=true;}
            if(!BoundNpcRoot(npc,state) && root>=0)
            {
                if(state.Root!=null){state.Root=null;state.RootRefreshIssued=false;}
                var head=npcStates[root];if(head!=null && head.Body.active && NpcBaselineCurrent(root) && SameNpcSnapshot(state,head)){state.Root=head.Body;state.RootGeneration=head.Body.generation;}
            }
            bool bound=BoundNpcRoot(npc,state);npc.realLife=bound?root:-1;
            if(!bound){state.BeforeRootBlock=npc.dontTakeDamage;state.RootBlocked=npc.dontTakeDamage=true;}
            if(bound || !state.WallDependency || state.RootRefreshIssued)return;
            var flow=Flow(state.Source);if(flow.Snapshot || state.Source!=null && state.Source.InitialNpc.Count>0 || !SourceAlive(state.Source))return;
            state.RootRefreshIssued=true;long barrier=flow.Outstanding?flow.Sent:flow.Sent+1;
            // Same bounded source lane as segment roots; no primary global
            // wofNPCIndex is borrowed and no world-wide scan is introduced.
            foreach(int slot in root<0?new[]{npc.whoAmI}:new[]{npc.whoAmI,root})
            {WorldLease lease;if(!npcSources.TryGetValue(slot,out lease)){lease=new WorldLease();npcSources.Add(slot,lease);}lease.Pending=state.Source;lease.Waiting=lease.AwaitBaseline=lease.RequiredBaseline=true;lease.Barrier=barrier;}
            if(!flow.Outstanding)flow.Requested=true;
        }
    }
}
