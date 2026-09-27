using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Fishing;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Fishing
{
    // Read-only .8 equipment projection. References represent real containers;
    // a shared favorite can be retained, but is never a physical source.
    internal static class FishingEquipmentPlan
    {
        internal sealed class Move
        {internal Item[] Source;internal int Slot,Target;internal Item Item;}
        private sealed class Candidate
        {internal Item[] Array;internal int Slot,Source;internal Item Item;internal FishingGear Gear;internal FishingGearCandidate Key {get{return new FishingGearCandidate(Item.type,Source,Slot);}}}
        private sealed class Projection
        {
            internal readonly Item[] Armor;
            private readonly Item[][] stores;
            internal Projection(Player p,int loadout)
            {
                int current=p.CurrentLoadoutIndex;Armor=loadout==current?p.armor:p.Loadouts[loadout].Armor;
                stores=new Item[p.Loadouts.Length][];
                for(int i=0;i<stores.Length;i++)stores[i]=loadout==current?p.Loadouts[i].Armor:i==current?p.armor:i==loadout?p.Loadouts[current].Armor:p.Loadouts[i].Armor;
            }
            internal Projection(Player p,Item[] armor){Armor=armor;stores=p.Loadouts.Select(x=>x.Armor).ToArray();}
            internal Item Effective(int slot)
            {
                if(!Armor[slot].IsAir)return Armor[slot];
                foreach(var array in stores)
                {
                    var item=array[slot];if(item.IsAir || !item.favorited)continue;
                    if(!NativeSlot(Armor,item,slot))break;
                    bool compatible=true;
                    for(int previous=slot-1;previous%10>=3 && previous>=slot/10*10+3;previous--)
                    {
                        if(!Armor[previous].IsAir)continue;
                        foreach(var source in stores)
                        {
                            var other=source[previous];if(other.IsAir || !other.favorited)continue;
                            compatible=ItemSlot.CanEquipBothAccessories(item,other,slot>=10);break;
                        }
                        if(!compatible)break;
                    }
                    if(compatible)return item;
                    // Native stops at the first shared favorite, even if its
                    // incompatibility would allow a later group's candidate.
                    break;
                }
                return Armor[slot];
            }
        }
        internal static bool Role(Player p,Item item,int slot)
        {return item!=null && !item.IsAir && item.type>0 && item.type<ItemID.Count && (!item.expertOnly || Main.expertMode) && p.IsItemSlotUnlockedAndUsable(slot) && (slot%10==0?item.headSlot>=0:slot%10==1?item.bodySlot>=0:slot%10==2?item.legSlot>=0:item.accessory);}
        private static bool NativeSlot(Item[] armor,Item item,int slot)
        {
            if(item.IsAir)return true;
            int kind=slot%10;bool functional=slot<10;
            if(kind<3)
            {
                if(kind==0?item.headSlot<0:kind==1?item.bodySlot<0:item.legSlot<0)return false;
                return ItemID.Sets.DualEquipArmor[item.type] || !ItemSlot.HasSameItemInSlot(item,new ArraySegment<Item>(armor,functional?10:0,3));
            }
            return item.accessory && ItemSlot.CanEquipAccessoryInSlot(item,!functional,new ArraySegment<Item>(armor,functional?3:13,7),slot) && !ItemSlot.HasSameItemInSlot(item,new ArraySegment<Item>(armor,functional?13:3,7));
        }
        internal static int Score(Player p,int loadout)
        {
            var projection=new Projection(p,loadout);int score=0;
            for(int slot=0;slot<10;slot++)
            {
                Item item=projection.Effective(slot);FishingGear gear;
                if(Role(p,item,slot) && FishingEquipmentCatalog.TryGet(item.type,false,out gear))score+=gear.ArmorSlot>=0?5:10;
            }
            return score;
        }
        internal static int BestLoadout(Player p)
        {
            int best=p.CurrentLoadoutIndex,score=0;
            for(int i=0;i<p.Loadouts.Length;i++){int value=Score(p,i);if(value>0 && value>=score){score=value;best=i;}}
            return best;
        }
        internal static bool CanSwap(Player p,Item[] source,int sourceSlot,int target,bool planSplit=false)
        {
            if(source==null || sourceSlot<0 || sourceSlot>=source.Length || target<0 || target>=10 || !p.IsItemSlotUnlockedAndUsable(target) || source[sourceSlot]==null || !source[sourceSlot].IsAir && !Role(p,source[sourceSlot],target))return false;
            // Native armor serialization omits stack. Planning may inspect a
            // stacked source, but a physical exchange requires one real item.
            if(source[sourceSlot].stack>1 && (!planSplit || ReferenceEquals(source,p.armor)))return false;
            var armor=(Item[])p.armor.Clone();Item original=armor[target];armor[target]=source[sourceSlot];
            if(ReferenceEquals(source,p.armor))armor[sourceSlot]=original;
            if(!NativeSlot(armor,armor[target],target))return false;
            if(ReferenceEquals(source,p.armor) && (!original.IsAir && !Role(p,original,sourceSlot) || !NativeSlot(armor,original,sourceSlot)))return false;
            var projection=new Projection(p,armor);
            // Native's first collision helper can match the target itself.
            // Confirm every other effective functional slot after the swap.
            if(target>=3 && !armor[target].IsAir)for(int i=3;i<10;i++)if(i!=target && p.IsItemSlotUnlockedAndUsable(i))
            {var other=projection.Effective(i);if(!other.IsAir && !ItemSlot.CanEquipBothAccessories(armor[target],other,false))return false;}
            return true;
        }
        internal static Move Next(Player p,bool lava,Func<Item[],int,bool> allowed,int manualSlots)
        {
            var all=new List<Candidate>();
            for(int slot=0;slot<10;slot++)
            {
                var item=p.GetEffectiveArmor(slot);FishingGear gear;
                if(Role(p,item,slot) && FishingEquipmentCatalog.TryGet(item.type,lava,out gear))all.Add(new Candidate{Array=p.armor,Slot=slot,Source=0,Item=item,Gear=gear});
            }
            Add(p.armor,10,20,1);Add(p.inventory,0,50,2);if(p.useVoidBag())Add(p.bank4.item,0,40,3);
            void Add(Item[] array,int start,int end,int source)
            {for(int slot=start;slot<end;slot++){var item=array[slot];FishingGear gear;if(item!=null && !item.IsAir && allowed(array,slot) && FishingEquipmentCatalog.TryGet(item.type,lava,out gear))all.Add(new Candidate{Array=array,Slot=slot,Source=source,Item=item,Gear=gear});}}
            var ordered=all.OrderByDescending(x=>x.Gear.Score).ThenBy(x=>x.Source).ThenBy(x=>x.Slot).ThenBy(x=>x.Item.type).ToList();
            for(int target=0;target<3;target++)
            {
                if((manualSlots&(1<<target))!=0)continue;FishingGear current;
                int score=FishingEquipmentCatalog.TryGet(p.GetEffectiveArmor(target).type,lava,out current) && current.ArmorSlot==target?current.Score:0;
                foreach(var candidate in ordered)if(candidate.Source!=0 && candidate.Gear.ArmorSlot==target && candidate.Gear.Score>score && CanSwap(p,candidate.Array,candidate.Slot,target,true))return ToMove(candidate,target);
            }
            int capacity=Enumerable.Range(3,7).Count(p.IsItemSlotUnlockedAndUsable);
            // An inaccessible best candidate must not suppress a compatible
            // alternative from the same effect group. Each rejection removes
            // one finite candidate and recomputes the existing catalog order.
            while(ordered.Count!=0)
            {
                var selected=FishingEquipmentCatalog.Accessories(ordered.Select(x=>x.Key),lava).Take(capacity).Select(key=>ordered.First(x=>x.Source==key.Source && x.Slot==key.Slot && x.Item.type==key.Type)).ToArray();
                var wanted=selected.FirstOrDefault(x=>x.Source!=0);if(wanted==null)return null;
                var targets=Enumerable.Range(3,7).Where(slot=>p.IsItemSlotUnlockedAndUsable(slot) && (manualSlots&(1<<slot))==0 && !selected.Any(x=>x.Source==0 && x.Slot==slot)).OrderBy(slot=>Replacement(p.armor[slot],lava)).ThenBy(slot=>GearScore(p.armor[slot],lava)).ThenBy(slot=>slot);
                foreach(int target in targets)if(CanSwap(p,wanted.Array,wanted.Slot,target,true))return ToMove(wanted,target);
                ordered.Remove(wanted);
            }
            return null;
        }
        // A currently unneeded lava hook is still old fishing equipment and is
        // replaced before an empty/nonfishing slot, even in ordinary water.
        private static int GearScore(Item item,bool lava){FishingGear gear;return FishingEquipmentCatalog.TryGet(item.type,true,out gear)?gear.Score:0;}
        private static int Replacement(Item item,bool lava){FishingGear gear;return FishingEquipmentCatalog.TryGet(item.type,true,out gear)?0:item.IsAir?1:2;}
        private static Move ToMove(Candidate item,int target){return new Move{Source=item.Array,Slot=item.Slot,Item=item.Item,Target=target};}
    }
}
