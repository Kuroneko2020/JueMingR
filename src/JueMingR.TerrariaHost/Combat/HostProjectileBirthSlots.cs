using Terraria;
using Terraria.DataStructures;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    // NewProjectileSetup uses the first inactive one of the 1000 native slots.
    // This lease is captured only by a finite spawning family and rechecked
    // at preparation/consumption. It never operates the live projectile pool.
    // Other attacks may change future occupancy; that remains a condition.
    internal sealed class HostProjectileBirthSlots
    {
        private readonly bool[] captured,occupied;
        private readonly ProjectileKey[] keys;
        private readonly bool randomSlots;
        private HostProjectileBirthSlots()
        {
            captured=new bool[1000];occupied=new bool[1000];keys=new ProjectileKey[1000];randomSlots=DebugOptions.Shared_RandomizeProjectileSlots;
            for(int i=0;i<captured.Length;i++){var p=Main.projectile[i];captured[i]=occupied[i]=p!=null && p.active;if(captured[i])keys[i]=p.key;}
        }
        private HostProjectileBirthSlots(HostProjectileBirthSlots original)
        {captured=original.captured;keys=original.keys;randomSlots=original.randomSlots;occupied=(bool[])original.occupied.Clone();}
        internal static HostProjectileBirthSlots Capture(){return new HostProjectileBirthSlots();}
        internal HostProjectileBirthSlots Copy(){return new HostProjectileBirthSlots(this);}
        internal bool TryAllocate(out int slot)
        {
            slot=-1;if(randomSlots)return false;
            for(int i=0;i<occupied.Length;i++)if(!occupied[i]){occupied[i]=true;slot=i;return true;}
            // Full-pool FindOldestProjectile can replace a different live
            // attack. This model cannot invent that attack's future lifetime.
            return false;
        }
        internal void Release(int slot){if(slot>=0 && slot<occupied.Length)occupied[slot]=false;}
        internal bool Current
        {
            get
            {
                if(randomSlots!=DebugOptions.Shared_RandomizeProjectileSlots)return false;
                for(int i=0;i<captured.Length;i++){var p=Main.projectile[i];bool active=p!=null && p.active;if(captured[i]!=active || active && !keys[i].Equals(p.key))return false;}
                return true;
            }
        }
    }
}
