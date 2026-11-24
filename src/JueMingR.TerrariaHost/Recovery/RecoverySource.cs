using JueMingR.Features.CoinDeposit;
using Terraria;

namespace JueMingR.TerrariaHost.Recovery
{
    // One real provider, never a copy of its contents. A slot/type alone cannot
    // authorize writing a replacement object after inventory or session change.
    internal struct RecoverySource
    {
        internal Player Player;
        internal Item[] Items;
        internal Item Item;
        internal int Account, Slot, Type, Stack;
        internal long Session, Revision;
        internal bool Found {get{return Item!=null;}}
        internal static Item[] AccountItems(Player p,int account)
        {return account==0?p.inventory:account==1?p.bank.item:account==2?p.bank2.item:account==3?p.bank3.item:p.bank4.item;}
        internal static bool TryCoinBalance(Player player, out long total)
        {
            total = 0;
            // BuyItem counts only the first 54 inventory slots and all four
            // banks. Exact debit receipts use the raw balance in both versions.
            for (int account = 0; account < 5; account++)
            {
                Item[] items = AccountItems(player, account);
                if (items == null || items.Length != (account == 0 ? 59 : 40)) return false;
                int payable = account == 0 ? 54 : 40;
                for (int slot = 0; slot < items.Length; slot++)
                {
                    Item item = items[slot];
                    if (item == null || item.stack < 0) return false;
                    if (slot < payable && CoinRules.TryValue(item.type, item.stack, out long value)) total += value;
                }
            }
            return true;
        }
        internal bool Matches(HostRecovery host,bool owned=false)
        {
            if(Player==null || !ReferenceEquals(Player,host.Player) || Session!=host.Runtime.Generation ||
                !ReferenceEquals(Items,AccountItems(Player,Account)) || Slot<0 || Slot>=Items.Length || !ReferenceEquals(Item,Items[Slot]) ||
                Item.type!=Type || Item.stack!=Stack || Stack<=0 || Account==4 && !Player.useVoidBag())return false;
            return owned || !host.Protected(Player,Items,Account,Slot);
        }
    }
}
