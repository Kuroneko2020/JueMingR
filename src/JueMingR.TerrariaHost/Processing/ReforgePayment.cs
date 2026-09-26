using System;
using Terraria;

namespace JueMingR.TerrariaHost.Processing
{
    // Reforge owns this snapshot and lease; it never borrows a nurse operation
    // or its bypass. Vanilla normal-currency payment touches coins and change.
    internal sealed class ReforgePayment
    {
        private readonly HostProcessing host;
        private readonly Item[][] arrays=new Item[5][],refs=new Item[5][];
        private readonly int[][] types=new int[5][],stacks=new int[5][];
        private static readonly int[] Ignored={58,57,56,55,54};
        internal readonly ulong[] Slots=new ulong[5];
        internal ReforgePayment(HostProcessing host){this.host=host;for(int a=0;a<5;a++){int n=a==0?54:40;refs[a]=new Item[n];types[a]=new int[n];stacks[a]=new int[n];}}
        private static Item[] Account(Player p,int a){return a==0?p.inventory:a==1?p.bank.item:a==2?p.bank2.item:a==3?p.bank3.item:p.bank4.item;}
        internal bool Balance(Player p,out long total)
        {
            total=0;
            for(int a=0;a<5;a++)
            {
                var array=Account(p,a);if(array==null || array.Length!=(a==0?59:40))return false;
                for(int i=0;i<array.Length;i++)if(array[i]==null || array[i].stack<0)return false;
                bool overflow;long amount=a==0?Utils.CoinsCount(out overflow,array,Ignored):Utils.CoinsCount(out overflow,array);
                if(overflow)return false;total=checked(total+amount);
            }
            return true;
        }
        internal bool Capture(Player p)
        {
            Array.Clear(Slots,0,5);
            for(int a=0;a<5;a++)
            {
                var array=arrays[a]=Account(p,a);
                for(int i=0;i<refs[a].Length;i++)
                {
                    Item item=array[i];refs[a][i]=item;types[a][i]=item.type;stacks[a][i]=item.stack;
                    if(item.IsAir || item.IsACoin){if(host.Items.Ownership.IsProtected(a,i) || host.Items.World.ManualMaterials.Contains(item) || a==0 && (i==host.Items.World.ManualSlot || p.inventoryChestStack[i]))return false;Slots[a]|=1UL<<i;}
                }
            }
            return true;
        }
        internal bool Unchanged(Player p)
        {for(int a=0;a<5;a++){var array=Account(p,a);if(!ReferenceEquals(array,arrays[a]))return false;for(int i=0;i<refs[a].Length;i++)if(!ReferenceEquals(array[i],refs[a][i]) || array[i].type!=types[a][i] || array[i].stack!=stacks[a][i])return false;}return true;}
    }
}
