using System;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Captures only currently known immunity. It is not a promise against a
    // different attack writing immunity later, nor a replacement for native
    // damage/terrain qualification. Outer updates, not extraUpdates, count.
    internal readonly struct HostAttackReceive
    {
        private readonly int specificEarliest,ownerEarliest;
        private readonly bool blocked;
        private HostAttackReceive(int ownerEarliest,int specificEarliest,bool blocked=false){this.specificEarliest=specificEarliest;this.ownerEarliest=ownerEarliest;this.blocked=blocked;}
        internal bool Allows(int outerUpdates){return !blocked && outerUpdates>=ownerEarliest && outerUpdates>=specificEarliest;}
        // Static immunity is an absolute native expiry. Its effective wait
        // changes with world time even when the stored expiry does not.
        internal bool Same(HostAttackReceive other){return specificEarliest==other.specificEarliest && ownerEarliest==other.ownerEarliest && blocked==other.blocked;}
        // A gem's actual Kill clears old local immunity before its burst.
        // Owner permission survives that local-only native operation.
        internal HostAttackReceive WithoutLocal(){return new HostAttackReceive(ownerEarliest,0,blocked);}
        // Native hits assign owner immunity, including shorter waits or zero.
        // A replayed assignment replaces ONLY that captured timer; it never
        // grants damage eligibility or clears local/static immunity.
        internal HostAttackReceive WithoutOwner(){return new HostAttackReceive(0,specificEarliest,blocked);}
        internal static HostAttackReceive Capture(Player player,Projectile shot,int npcSlot,bool beforeNpc,bool nextWorld=false)
        {return CaptureCore(player,shot,npcSlot,beforeNpc,nextWorld,false);}
        // Arc families own one private copy of the player's melee history.
        // A future native hand start clears that entire history, so an old
        // captured melee block cannot be folded into permanent immunity.
        internal static HostAttackReceive CaptureIgnoringMelee(Player player,Projectile shot,int npcSlot,bool beforeNpc,bool nextWorld)
        {return CaptureCore(player,shot,npcSlot,beforeNpc,nextWorld,true);}
        private static HostAttackReceive CaptureCore(Player player,Projectile shot,int npcSlot,bool beforeNpc,bool nextWorld,bool ignoreMelee)
        {
            var npc=Main.npc[npcSlot];int owner=0;
            if(!(shot.maxPenetrate==1 && !shot.usesLocalNPCImmunity && !shot.usesIDStaticNPCImmunity))
            {int value=npc.immune[player.whoAmI];owner=value<0?int.MaxValue:value==0?0:value+(beforeNpc || nextWorld?0:1);}
            int specific=0;
            if(shot.usesLocalNPCImmunity || shot.usesIDStaticNPCImmunity)
            {
                int local=int.MaxValue,shared=int.MaxValue;
                if(shot.usesLocalNPCImmunity){int value=shot.localNPCImmunity[npcSlot];local=value<0?int.MaxValue:value;}
                if(shot.usesIDStaticNPCImmunity)
                {uint expiry=Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,npcSlot];long distance=(long)expiry-Main.GameUpdateCount;shared=distance<=0?0:distance>120?int.MaxValue:(int)distance+(nextWorld?0:1);}
                // Native combines local and static branches with OR, then
                // applies the separate owner gate. Negative local is blocked.
                specific=Math.Min(local,shared);
            }
            if(!ignoreMelee && shot.usesOwnerMeleeHitCD && shot.OwnedBySomeone && !player.CanHitNPCWithMeleeHit(npcSlot))return new HostAttackReceive(0,0,true);
            if(npc.trapImmune && shot.trap || npc.immortal && shot.npcProj || !player.CanNPCBeHitByPlayerOrPlayerProjectile(npc))return new HostAttackReceive(0,0,true);
            return new HostAttackReceive(owner,specific);
        }
    }
}
