using System;
using System.Collections.Generic;
using System.Linq;

namespace JueMingR.Features.Fishing
{
    public struct FishingGear
    {
        public int Type {get;}
        public int Group {get;}
        public int Score {get;}
        public int ArmorSlot {get;}
        internal FishingGear(int type,int group,int score,int armorSlot=-1){Type=type;Group=group;Score=score;ArmorSlot=armorSlot;}
    }
    public struct FishingGearCandidate
    {
        public int Type {get;}
        // Current armor, social armor, inventory, usable void. These are
        // preference priorities, not permission to access a native container.
        public int Source {get;}
        public int Slot {get;}
        public FishingGearCandidate(int type,int source,int slot){Type=type;Source=source;Slot=slot;}
    }
    public static class FishingEquipmentCatalog
    {
        private static readonly Dictionary<int,FishingGear> entries=Build();
        private static Dictionary<int,FishingGear> Build()
        {
            var values=new Dictionary<int,FishingGear>();
            for(int slot=0;slot<3;slot++)
            {values.Add(2367+slot,new FishingGear(2367+slot,slot,50,slot));values.Add(5591+slot,new FishingGear(5591+slot,slot,70,slot));}
            Add(values,5064,10,10000);Add(values,3721,11,9000);Add(values,2374,12,8000);
            for(int i=0;i<8;i++)Add(values,5139+i,13,7000+i);
            Add(values,2373,14,5200);Add(values,2375,15,5100);Add(values,4881,16,5000);
            Add(values,3035,17,3400);Add(values,3034,17,3200);Add(values,855,17,3000);
            Add(values,5331,18,2900);foreach(int id in new[]{3250,3251,3252})Add(values,id,18,2800);
            foreach(int id in new[]{1250,1251,1252})Add(values,id,18,2700);
            Add(values,396,18,2600);Add(values,158,18,2500);return values;
        }
        private static void Add(Dictionary<int,FishingGear> target,int id,int group,int score){target.Add(id,new FishingGear(id,group,score));}
        public static bool TryGet(int type,bool lava,out FishingGear gear)
        {return entries.TryGetValue(type,out gear) && (type!=4881 || lava);}
        public static int LoadoutScore(IEnumerable<int> effectiveTypes,bool lava)
        {int score=0;foreach(int type in effectiveTypes){FishingGear gear;if(TryGet(type,lava,out gear))score+=gear.ArmorSlot>=0?5:10;}return score;}
        public static FishingGearCandidate? BestArmor(IEnumerable<FishingGearCandidate> candidates,int slot,int currentType)
        {
            if(slot<0 || slot>2)throw new ArgumentOutOfRangeException(nameof(slot));
            FishingGear current;int score=entries.TryGetValue(currentType,out current) && current.ArmorSlot==slot?current.Score:0;
            var eligible=candidates.Where(c=>{FishingGear gear;return entries.TryGetValue(c.Type,out gear) && gear.ArmorSlot==slot && gear.Score>score;});
            foreach(var c in Order(eligible))return c;return null;
        }
        public static IReadOnlyList<FishingGearCandidate> Accessories(IEnumerable<FishingGearCandidate> candidates,bool lava)
        {
            var selected=new List<FishingGearCandidate>();var groups=new HashSet<int>();
            foreach(var c in Order(candidates.Where(c=>{FishingGear gear;return TryGet(c.Type,lava,out gear) && gear.ArmorSlot<0;})))
                if(groups.Add(entries[c.Type].Group))selected.Add(c);
            // Bags' distinct fishing power stacks with each other and earring.
            // Only their line/box/lava capabilities suppress separate items.
            bool lavaBag=selected.Any(x=>x.Type==5064),bag=lavaBag || selected.Any(x=>x.Type==3721);
            selected.RemoveAll(x=>bag && (x.Type==2373 || x.Type==2375) || lavaBag && x.Type==4881);
            return selected.AsReadOnly();
        }
        private static IOrderedEnumerable<FishingGearCandidate> Order(IEnumerable<FishingGearCandidate> candidates)
        {return candidates.OrderByDescending(c=>entries[c.Type].Score).ThenBy(c=>c.Source).ThenBy(c=>c.Slot).ThenBy(c=>c.Type);}
    }
}
