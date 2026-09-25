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
        internal bool Matches(HostRecovery host,bool owned=false)
        {
            if(Player==null || !ReferenceEquals(Player,host.Player) || Session!=host.Runtime.Generation ||
                !ReferenceEquals(Items,AccountItems(Player,Account)) || Slot<0 || Slot>=Items.Length || !ReferenceEquals(Item,Items[Slot]) ||
                Item.type!=Type || Item.stack!=Stack || Stack<=0 || Account==4 && !Player.useVoidBag())return false;
            return owned || !host.Protected(Player,Items,Account,Slot);
        }
    }
}
