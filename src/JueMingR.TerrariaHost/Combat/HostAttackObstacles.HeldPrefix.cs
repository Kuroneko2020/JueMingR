using System.Collections.Generic;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private Dictionary<HostProjectileFamilyLease,HostProjectileFamilyLease> heldPrefixFamilies;
        private Dictionary<int,bool> heldPrefixActivity;
        private HostHarpyAttack.Arrow[] heldPrefixHarpyArrows;
        private bool[] heldPrefixHarpyOccupied;
        private List<KeyValuePair<NPC,int>> heldPrefixNpcLife;
        private bool heldPrefixCaptured,heldPrefixKnown;

        // The real post-NPC preparation sees the pool BEFORE every projectile.
        // The finite model owns its exact lower-slot motor/receive/Kill prefix;
        // freeze that prefix BEFORE the candidate's new hand batch is born.
        // Ordinary Unchanged/readonly/Draw never borrow this later boundary.
        internal void CaptureHeldControllerPrefix(Projectile[] original,Projectile[] advanced,int parentSlot)
        {
            heldPrefixCaptured=true;heldPrefixKnown=false;heldPrefixFamilies=null;heldPrefixActivity=null;heldPrefixHarpyArrows=null;heldPrefixHarpyOccupied=null;
            if(original==null || advanced==null || original.Length!=advanced.Length)return;
            var bySlot=new Dictionary<int,Projectile>();foreach(var p in advanced)if(p==null || bySlot.ContainsKey(p.whoAmI))return;else bySlot.Add(p.whoAmI,p);
            var changes=new Dictionary<Projectile,Projectile>();var activity=new Dictionary<int,bool>();
            foreach(var p in original)
            {
                Projectile next;if(p==null || !bySlot.TryGetValue(p.whoAmI,out next) || !next.key.Equals(p.key) || next.type!=p.type || next.owner!=p.owner)return;
                if(p.whoAmI>=parentSlot)continue;
                changes.Add(p,next);activity.Add(p.whoAmI,next.active);
            }
            // A controller in the first slot (or with no modeled lower
            // members) still uses the original lease, without allocating a
            // duplicate family snapshot for each direction.
            if(changes.Count==0){heldPrefixCaptured=false;return;}
            // A new actor in the prefix lacks an original identity lease.
            // Keep the complete predicted Contact, but do not lend actual
            // input through an unknown birth. Native NPC hit/life/immune
            // changes are independently rejected by the usual receive lease.
            heldPrefixActivity=activity;heldPrefixFamilies=new Dictionary<HostProjectileFamilyLease,HostProjectileFamilyLease>();
            foreach(var family in familyLeases)heldPrefixFamilies.Add(family,family.AfterHeldPrefix(changes));
            if(heldPrefixNpcLife==null)
            {
                heldPrefixNpcLife=new List<KeyValuePair<NPC,int>>();var selectedNpc=Main.npc[timeline.Identity.Slot];heldPrefixNpcLife.Add(new KeyValuePair<NPC,int>(selectedNpc,selectedNpc.life));
                foreach(var receiver in receivers)heldPrefixNpcLife.Add(new KeyValuePair<NPC,int>(receiver.Npc,receiver.Npc.life));
            }
            if(harpyBatchState!=null)
            {
                var arrows=new List<HostHarpyAttack.Arrow>();
                foreach(var arrow in harpyBatchState.arrows)
                {
                    Projectile expected;if(!changes.TryGetValue(arrow.Shot,out expected)){arrows.Add(arrow);continue;}
                    if(!expected.active)continue;
                    var projected=new HostHarpyAttack.Arrow(expected,arrow.Slot);projected.Shot=arrow.Shot;projected.Key=arrow.Key;arrows.Add(projected);
                }
                heldPrefixHarpyArrows=arrows.ToArray();heldPrefixHarpyOccupied=(bool[])harpyBatchState.occupied.Clone();foreach(var pair in activity)heldPrefixHarpyOccupied[pair.Key]=pair.Value;
            }
            heldPrefixKnown=true;
        }
        private bool HeldPrefixFamilyCurrent(HostProjectileFamilyLease family)
        {HostProjectileFamilyLease expected;return heldPrefixFamilies!=null && heldPrefixFamilies.TryGetValue(family,out expected)?expected.Current:family.Current;}
        internal bool HeldControllerUnchanged
        {get{if(!heldPrefixCaptured)return Unchanged;if(!heldPrefixKnown)return false;foreach(var row in heldPrefixNpcLife)if(row.Key.life!=row.Value)return false;return ReadUnchanged(false,true);}}
        private bool HeldPrefixHarpyMembersCurrent
        {get{return harpyBatchState==null || heldPrefixHarpyArrows!=null && harpyBatchState.MembersAtHeldPrefix(heldPrefixHarpyArrows);}}
        private bool HeldPrefixHarpyDependenciesCurrent
        {get{return harpyBatchState==null || heldPrefixHarpyArrows!=null && harpyBatchState.DependenciesAtHeldPrefix(heldPrefixHarpyArrows,heldPrefixHarpyOccupied);}}
    }
}
