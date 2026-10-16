using System.Collections.Generic;
using Microsoft.Xna.Framework;
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
        private readonly HostProjectileBirthSlots origin;
        private sealed class RetainedOrigin {internal Projectile Body;internal Vector2 Position;}
        private Dictionary<int,RetainedOrigin> retainedOrigins;
        private bool poolingCaptured,noPooling;
        private HostProjectileBirthSlots()
        {
            origin=this;
            captured=new bool[1000];occupied=new bool[1000];keys=new ProjectileKey[1000];randomSlots=DebugOptions.Shared_RandomizeProjectileSlots;
            for(int i=0;i<captured.Length;i++){var p=Main.projectile[i];captured[i]=occupied[i]=p!=null && p.active;if(captured[i])keys[i]=p.key;}
        }
        private HostProjectileBirthSlots(HostProjectileBirthSlots original)
        {origin=original.origin;captured=original.captured;keys=original.keys;randomSlots=original.randomSlots;occupied=(bool[])original.occupied.Clone();}
        internal static HostProjectileBirthSlots Capture(){return new HostProjectileBirthSlots();}
        internal HostProjectileBirthSlots Copy(){return new HostProjectileBirthSlots(this);}
        internal bool PoolsInstances
        {get{if(!origin.poolingCaptured){origin.noPooling=Main.NoPooling;origin.poolingCaptured=true;}return !origin.noPooling;}}
        // SetDefaults does not reset Entity.oldPosition on a pooled slot.
        // AI29/521 can Kill before its first oldPosition store, so capture only
        // origins actually consumed by that birth. Candidate copies share the
        // lease; private future releases stay in the family's own motor state.
        internal bool TryRetainedPosition(int slot,out Vector2 position)
        {
            position=Vector2.Zero;if(slot<0 || slot>=captured.Length)return false;
            if(!PoolsInstances)return true;
            if(origin.retainedOrigins==null)origin.retainedOrigins=new Dictionary<int,RetainedOrigin>();
            RetainedOrigin saved;
            if(!origin.retainedOrigins.TryGetValue(slot,out saved))
            {var p=Main.projectile[slot];if(p==null)return false;saved=new RetainedOrigin{Body=p,Position=p.oldPosition};origin.retainedOrigins.Add(slot,saved);}
            position=saved.Position;return true;
        }
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
        {get{return CurrentAfterHeldPrefix(null);}}
        // A world-pass model may have deterministically retired a captured
        // lower slot before the controller reads its prepared input. Only
        // those exact activity transitions change here; all other slots/keys,
        // pooling premises and unknown native births stay strictly captured.
        internal bool CurrentAfterHeldPrefix(Dictionary<int,bool> activity)
        {
                if(randomSlots!=DebugOptions.Shared_RandomizeProjectileSlots)return false;
                if(origin.poolingCaptured && origin.noPooling!=Main.NoPooling)return false;
                if(origin.retainedOrigins!=null)foreach(var pair in origin.retainedOrigins)
                {var p=Main.projectile[pair.Key];if(!ReferenceEquals(p,pair.Value.Body) || p.oldPosition!=pair.Value.Position)return false;}
                for(int i=0;i<captured.Length;i++){var p=Main.projectile[i];bool active=p!=null && p.active,expected=captured[i],projected;if(activity!=null && activity.TryGetValue(i,out projected))expected=projected;if(expected!=active || active && !keys[i].Equals(p.key))return false;}
                return true;
        }
    }
}
