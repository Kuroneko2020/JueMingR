using System;
using Terraria;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class NursePayment
    {
        private readonly HostRecovery host;
        private readonly Item[][] arrays=new Item[5][];
        private readonly Item[][] refs=new Item[5][];
        private readonly int[][] types=new int[5][],stacks=new int[5][];
        private readonly long[] amounts=new long[5];
        private static readonly int[] Ignored={58,57,56,55,54};
        internal readonly ulong[] Slots=new ulong[5];
        internal NursePayment(HostRecovery host){this.host=host;for(int a=0;a<5;a++){int n=a==0?54:40;refs[a]=new Item[n];types[a]=new int[n];stacks[a]=new int[n];}}
        internal bool Balance(Player p,out long total)
        {
            total=0;bool overflow;
            for(int a=0;a<5;a++)
            {
                var array=RecoverySource.AccountItems(p,a);if(array==null || array.Length!=(a==0?59:40))return false;
                for(int i=0;i<array.Length;i++)if(array[i]==null || array[i].stack<0)return false;
                amounts[a]=a==0?Utils.CoinsCount(out overflow,array,Ignored):Utils.CoinsCount(out overflow,array);
            }
            // CoinsCombineStacks clamps affordability at 9,999,999,999. A
            // receipt needs the uncapped sum or a rich player appears unpaid.
            for(int a=0;a<5;a++)total=checked(total+amounts[a]);return true;
        }
        internal bool Capture(Player p)
        {
            Array.Clear(Slots,0,5);
            for(int a=0;a<5;a++)
            {
                var array=arrays[a]=RecoverySource.AccountItems(p,a);int count=a==0?54:40;
                for(int i=0;i<count;i++)
                {
                    Item item=array[i];refs[a][i]=item;types[a][i]=item.type;stacks[a][i]=item.stack;
                    if(item.IsAir || item.IsACoin){if(host.Items.Ownership.IsProtected(a,i) || a==0 && (i==host.Items.World.ManualSlot || p.inventoryChestStack[i]))return false;Slots[a]|=1UL<<i;}
                }
            }
            return true;
        }
        internal bool Unchanged(Player p)
        {
            for(int a=0;a<5;a++){var array=RecoverySource.AccountItems(p,a);if(!ReferenceEquals(array,arrays[a]))return false;for(int i=0;i<refs[a].Length;i++)if(!ReferenceEquals(array[i],refs[a][i]) || array[i].type!=types[a][i] || array[i].stack!=stacks[a][i])return false;}return true;
        }
    }
}
