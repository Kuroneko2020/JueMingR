using System;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Captures only currently known immunity. It is not a promise against a
    // different attack writing immunity later, nor a replacement for native
    // damage/terrain qualification. Outer updates, not extraUpdates, count.
    internal readonly struct HostAttackReceive
    {
        private readonly int earliest;
        private HostAttackReceive(int earliest){this.earliest=earliest;}
        internal bool Allows(int outerUpdates){return outerUpdates>=earliest;}
        internal static HostAttackReceive Capture(Player player,Projectile shot,int npcSlot,bool beforeNpc,bool nextWorld=false)
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
            if(shot.usesOwnerMeleeHitCD && shot.OwnedBySomeone && !player.CanHitNPCWithMeleeHit(npcSlot))return new HostAttackReceive(int.MaxValue);
            if(npc.trapImmune && shot.trap || npc.immortal && shot.npcProj || !player.CanNPCBeHitByPlayerOrPlayerProjectile(npc))return new HostAttackReceive(int.MaxValue);
            return new HostAttackReceive(Math.Max(owner,specific));
        }
    }
}
