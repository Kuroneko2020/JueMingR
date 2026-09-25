using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class RecoveryCatalog
    {
        private readonly HostRecovery host;
        private int[] life,mana;
#if DEBUG
        internal long StockReads;
#endif
        internal RecoveryCatalog(HostRecovery host){this.host=host;}
        internal int[] Get(bool healing)
        {
            if(life==null)
            {
                var red=new List<int>();var blue=new List<int>();var seen=new HashSet<int>();
                try
                {
                    for(int type=1;type<ItemID.Count;type++)
                    {var item=new Item();item.SetDefaults(type);if(item.type<=0 || !seen.Add(item.type))continue;if(item.potion && item.healLife>0)red.Add(item.type);if(item.healMana>0)blue.Add(item.type);}
                    life=red.ToArray();mana=blue.ToArray();
                }
                catch{host.Report("药品目录无法读取，请重新打开配置重试。");return new int[0];}
            }
            return healing?life:mana;
        }
        internal void Close(){} // Definition catalog is independent of player inventory.
        // Native pets can have zero definition time; QuickBuff's real effect
        // helper supplies 3600. Other zero-duration items stay outside the list.
        internal static bool BuffCandidate(Item item){return item!=null && item.type>0 && item.stack>0 && item.buffType>0 && item.buffType<Main.lightPet.Length && (item.buffTime>0 || Main.lightPet[item.buffType] || Main.vanityPet[item.buffType]) && !item.summon;}
        internal int[] BuffCandidates()
        {
            var p=host.Player;if(p==null)return new int[0];var result=new HashSet<int>();
            for(int a=0;a<2;a++){if(a==1 && !p.useVoidBag())break;var items=a==0?p.inventory:p.bank4.item;int n=a==0?58:p.bank4.maxItems;for(int i=0;i<n;i++){
#if DEBUG
                StockReads++;
#endif
                if(BuffCandidate(items[i]))result.Add(items[i].type);}}
            return result.OrderBy(x=>x).ToArray();
        }
        internal string Describe(int type,int list)
        {return QuickItems.HostQuickItems.SafeName(type);}
    }
}
