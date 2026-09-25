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
        private readonly Dictionary<int,string> descriptions=new Dictionary<int,string>();
        internal RecoveryCatalog(HostRecovery host){this.host=host;}
        internal int[] Get(bool healing)
        {
            if(life==null)
            {
                var red=new List<int>();var blue=new List<int>();var seen=new HashSet<int>();
                try
                {
                    for(int type=1;type<ItemID.Count;type++)
                    {var item=new Item();item.SetDefaults(type);if(item.type<=0 || !seen.Add(item.type))continue;if(item.potion && item.healLife>0)red.Add(item.type);if(item.healMana>0)blue.Add(item.type);if(item.healLife>0 || item.healMana>0)descriptions[item.type]=item.type==3001?"恢复70—120生命（选药估算95）":item.healLife>0?"恢复"+item.healLife+"生命":"恢复"+item.healMana+"魔力";}
                    life=red.ToArray();mana=blue.ToArray();
                }
                catch{host.Report("药品目录无法读取，请重新打开药品设置重试。");return new int[0];}
            }
            return healing?life:mana;
        }
        internal void Close(){} // Definition catalog is independent of player inventory.
        internal static bool BuffCandidate(Item item){return item!=null && item.type>0 && item.stack>0 && item.buffType>0 && item.buffTime>0 && !item.summon;}
        internal int[] BuffCandidates()
        {
            var p=host.Player;if(p==null)return new int[0];var result=new HashSet<int>();
            for(int a=0;a<2;a++){if(a==1 && !p.useVoidBag())break;var items=a==0?p.inventory:p.bank4.item;int n=a==0?58:p.bank4.maxItems;for(int i=0;i<n;i++)if(BuffCandidate(items[i]))result.Add(items[i].type);}
            return result.OrderBy(x=>x).ToArray();
        }
        internal string Describe(int type,int list)
        {string name=QuickItems.HostQuickItems.SafeName(type),text;return list==2?name+" · 点击添加或移除":name+" · "+(descriptions.TryGetValue(type,out text)?text:"")+" · 点击切换许可";}
    }
}
