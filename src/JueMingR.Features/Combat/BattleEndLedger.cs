using System;
using System.Collections.Generic;

namespace JueMingR.Features.Combat
{
    // Server-applied NPC state, not local predicted life. Slots are bounded;
    // composite bosses finish only after all known server-active members end.
    public sealed class BattleEndLedger
    {
        private struct Entry {internal int Generation,Type,Group;internal bool Active;}
        private readonly Entry[] slots;
        private readonly Dictionary<int,int> groups=new Dictionary<int,int>();
        private readonly HashSet<int> ended=new HashSet<int>();
        private int events;
        private bool eventEnd;
        public BattleEndLedger(int capacity){if(capacity<=0)throw new ArgumentOutOfRangeException(nameof(capacity));slots=new Entry[capacity];}
        public void Observe(int slot,int generation,int type,int group,bool active)
        {
            if(slot<0 || slot>=slots.Length)throw new ArgumentOutOfRangeException(nameof(slot));
            Entry old=slots[slot];bool eligible=active && group>0;
            if(old.Active && eligible && old.Generation==generation && old.Type==type && old.Group==group)return;
            if(old.Active)groups[old.Group]--;
            slots[slot]=new Entry{Generation=generation,Type=type,Group=group,Active=eligible};
            if(eligible){int count;groups.TryGetValue(group,out count);groups[group]=count+1;ended.Remove(group);}
            if(old.Active && groups[old.Group]==0){groups.Remove(old.Group);ended.Add(old.Group);}
        }
        public void Events(int mask,bool baseline=false)
        {if(!baseline && (events & ~mask)!=0)eventEnd=true;events=mask;}
        public bool TakeEnded()
        {bool result=ended.Count!=0 || eventEnd;ended.Clear();eventEnd=false;return result;}
        public void Clear(){Array.Clear(slots,0,slots.Length);groups.Clear();ended.Clear();events=0;eventEnd=false;}
    }
}
