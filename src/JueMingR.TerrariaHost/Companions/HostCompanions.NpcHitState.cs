using System;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        // Native static immunity is shared by projectile identity, NOT owner.
        // Keep that sharing when both NPC tables describe the same incarnation.
        // A second ABI matrix is needed only because the two streams can hold
        // different generations of one slot. Never copy a world per actor/step.
        private uint[,] primaryNpcStaticHits,observedNpcStaticHits;
        private NPC[] primaryNpcHitTable;
        private byte[] primaryNpcHitGeneration,observedNpcHitGeneration;
        private bool[] primaryNpcHitKnown,observedNpcHitKnown;

        private bool ClientNpcHitOwner {get{return Main.netMode==1 && HasActors;}}

        private bool EnsureNpcHitState(NPC[] primaryTable)
        {
            // A server used to produce a real packet must not initialize or
            // advance this client's caches, even in an in-process fixture.
            if(!ClientNpcHitOwner)return false;
            if(primaryNpcStaticHits!=null)return true;
            if(primaryTable==null || ReferenceEquals(primaryTable,observedNpcs) || primaryTable.Length<Main.maxNPCs)return false;
            primaryNpcHitTable=primaryTable;
            primaryNpcStaticHits=Projectile.perIDStaticNPCImmunity;
            observedNpcStaticHits=new uint[primaryNpcStaticHits.GetLength(0),primaryNpcStaticHits.GetLength(1)];
            int count=primaryNpcStaticHits.GetLength(1);
            primaryNpcHitGeneration=new byte[count];observedNpcHitGeneration=new byte[count];
            primaryNpcHitKnown=new bool[count];observedNpcHitKnown=new bool[count];
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var npc=primaryTable[slot];if(npc==null || npc.generation==0)continue;
                primaryNpcHitGeneration[slot]=npc.generation;primaryNpcHitKnown[slot]=true;
            }
            return true;
        }

        // The NPC-table borrower must capture this token BEFORE entering the
        // observation table. Its finally restores the exact previous matrix,
        // including nested calls, rather than guessing from Main.myPlayer.
        internal uint[,] EnterObservedNpcHitState(NPC[] primaryTable)
        {
            if(!EnsureNpcHitState(primaryTable) || ReferenceEquals(Projectile.perIDStaticNPCImmunity,observedNpcStaticHits))return null;
            var previous=Projectile.perIDStaticNPCImmunity;Projectile.perIDStaticNPCImmunity=observedNpcStaticHits;return previous;
        }
        internal void LeaveObservedNpcHitState(uint[,] previous)
        {if(previous!=null)Projectile.perIDStaticNPCImmunity=previous;}

        private bool SharedNpcHitGeneration(int slot)
        {
            return primaryNpcHitKnown[slot] && observedNpcHitKnown[slot] &&
                primaryNpcHitGeneration[slot]==observedNpcHitGeneration[slot];
        }

        // Called only after a successful observation23 decode, before any
        // actor/shot can inspect the new incarnation's outer eligibility gates.
        // Repeated23, source changes, transforms and interest retirement are
        // not births; retain this slot witness until the world itself ends.
        internal void AcceptObservedNpcHitGeneration(int slot,byte generation,NPC[] primaryTable)
        {
            if(!EnsureNpcHitState(primaryTable) || (uint)slot>=(uint)observedNpcHitGeneration.Length || generation==0)return;
            if(observedNpcHitKnown[slot] && observedNpcHitGeneration[slot]==generation)return;
            observedNpcHitGeneration[slot]=generation;observedNpcHitKnown[slot]=true;
            bool shared=SharedNpcHitGeneration(slot);
            for(int type=0;type<observedNpcStaticHits.GetLength(0);type++)
                observedNpcStaticHits[type,slot]=shared?primaryNpcStaticHits[type,slot]:0u;

            // Local immunity belongs to the exact projectile activation; -1 is
            // permanent until this real NPC incarnation changes. Use the
            // existing lease check, including dragon/head array consumers.
            foreach(var projectile in Main.projectile)
                if(projectile!=null && ProjectileActor(projectile)!=null)projectile.localNPCImmunity[slot]=0;
            // Only actually attached bodies are owners. A loaded but not yet
            // attached profile can still carry whoAmI0 and must never clear it.
            foreach(var player in Main.player)
            {
                if(Owned(player)==null)continue;
                player.meleeNPCHitCooldown[slot]=0;player.TagEffectStack.ResetNPCSlotData(slot);
            }
        }

        private bool PrimaryNpcHitContext()
        {
            return ClientNpcHitOwner && !ReadingObservation && !ReferenceEquals(Main.npc,observedNpcs) &&
                EnsureNpcHitState(Main.npc) && ReferenceEquals(Main.npc,primaryNpcHitTable);
        }

        private bool AdoptObservedNpcHitGeneration(int slot,byte oldGeneration,byte newGeneration,bool replacement)
        {
            return replacement && oldGeneration!=newGeneration && observedNpcHitKnown[slot] && observedNpcHitGeneration[slot]==newGeneration;
        }

        // Prefix replacement for the native Projectile.ResetNPCSlotData leaf.
        // The NewNPCInstanceInSlot prefix supplies its REAL old/new generation:
        // by the time native Reset runs, Main.npc[slot] is already replaced.
        // 'replacement' distinguishes that birth from a direct same-domain
        // reset. Portal/Boss cleanup in NPC.ResetNPCSlotData stays native.
        internal bool ResetPrimaryProjectileNpcHitSlot(int slot,byte oldGeneration,byte newGeneration,bool replacement)
        {
            if(!PrimaryNpcHitContext() || (uint)slot>=(uint)primaryNpcHitGeneration.Length)return false;
            // The primary stream can catch up AFTER AI shots already hit this
            // generation. Adopt those shared timestamps rather than clearing
            // the AI's new state for the human stream's delayed old birth.
            bool adopt=AdoptObservedNpcHitGeneration(slot,oldGeneration,newGeneration,replacement);
            primaryNpcHitGeneration[slot]=newGeneration;primaryNpcHitKnown[slot]=newGeneration!=0;
            bool shared=SharedNpcHitGeneration(slot);
            bool clearObserved=shared && !adopt;
            for(int type=0;type<primaryNpcStaticHits.GetLength(0);type++)
            {
                uint value=adopt?observedNpcStaticHits[type,slot]:0u;
                primaryNpcStaticHits[type,slot]=value;
                if(shared)observedNpcStaticHits[type,slot]=value;
            }
            foreach(var projectile in Main.projectile)
                if(projectile!=null && (clearObserved || ProjectileActor(projectile)==null))projectile.localNPCImmunity[slot]=0;
            return true;
        }

        // Prefix replacement for Player.ResetNPCSlotData. Each attached AI's
        // melee/tag slot belongs to the observation incarnation. A delayed
        // primary birth cannot clear it; a true same-incarnation native reset
        // still clears BOTH domains, just like the shared static timestamps.
        internal bool ResetPrimaryPlayerNpcHitSlot(int slot,byte oldGeneration,byte newGeneration,bool replacement)
        {
            if(!PrimaryNpcHitContext() || (uint)slot>=(uint)primaryNpcHitGeneration.Length)return false;
            bool clearObserved=observedNpcHitKnown[slot] && observedNpcHitGeneration[slot]==newGeneration &&
                !AdoptObservedNpcHitGeneration(slot,oldGeneration,newGeneration,replacement);
            foreach(var player in Main.player)
            {
                if(player==null || !clearObserved && Owned(player)!=null)continue;
                player.meleeNPCHitCooldown[slot]=0;player.TagEffectStack.ResetNPCSlotData(slot);
            }
            return true;
        }

        // The locked native matrix has four Set sites: the slot reset leaf,
        // matching-time clear, and two actual hit writes. Forward the ordinary
        // hit/matching writes here; the birth leaf above handles column adoption.
        // Mirror only one cell, only for the same incarnation, preserving native
        // shared cooldowns/expiry and matching clear across human and ALL AI.
        internal void SetNpcStaticHitImmunity(uint[,] matrix,int type,int slot,uint value)
        {
            matrix[type,slot]=value;
            if(!ClientNpcHitOwner || primaryNpcStaticHits==null)return;
            bool primary=ReferenceEquals(matrix,primaryNpcStaticHits),observed=ReferenceEquals(matrix,observedNpcStaticHits);
            if((primary || observed) && SharedNpcHitGeneration(slot))
            {if(primary)observedNpcStaticHits[type,slot]=value;else primaryNpcStaticHits[type,slot]=value;}
        }

        internal void ClearNpcHitState()
        {
            // World shutdown can occur while borrowed. Restore the real ABI
            // matrix before dropping our references; the borrower's finally
            // still restores its exact entering reference as usual.
            if(observedNpcStaticHits!=null && ReferenceEquals(Projectile.perIDStaticNPCImmunity,observedNpcStaticHits))Projectile.perIDStaticNPCImmunity=primaryNpcStaticHits;
            primaryNpcStaticHits=observedNpcStaticHits=null;primaryNpcHitTable=null;
            primaryNpcHitGeneration=observedNpcHitGeneration=null;primaryNpcHitKnown=observedNpcHitKnown=null;
        }
    }
}
