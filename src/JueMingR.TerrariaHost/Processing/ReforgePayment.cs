using System;
using JueMingR.TerrariaHost.Recovery;
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
        internal readonly ulong[] Slots=new ulong[5];
        internal ReforgePayment(HostProcessing host){this.host=host;for(int a=0;a<5;a++){int n=a==0?54:40;refs[a]=new Item[n];types[a]=new int[n];stacks[a]=new int[n];}}
        internal bool Balance(Player p,out long total) => RecoverySource.TryCoinBalance(p,out total);
        internal bool Capture(Player p)
        {
            Array.Clear(Slots,0,5);
            for(int a=0;a<5;a++)
            {
                var array=arrays[a]=RecoverySource.AccountItems(p,a);
                for(int i=0;i<refs[a].Length;i++)
                {
                    Item item=array[i];refs[a][i]=item;types[a][i]=item.type;stacks[a][i]=item.stack;
                    if(item.IsAir || item.IsACoin){if(host.Items.Ownership.IsProtected(a,i) || host.Items.World.ManualMaterials.Contains(item) || a==0 && (i==host.Items.World.ManualSlot || p.inventoryChestStack[i]))return false;Slots[a]|=1UL<<i;}
                }
            }
            return true;
        }
        internal bool Unchanged(Player p)
        {for(int a=0;a<5;a++){var array=RecoverySource.AccountItems(p,a);if(!ReferenceEquals(array,arrays[a]))return false;for(int i=0;i<refs[a].Length;i++)if(!ReferenceEquals(array[i],refs[a][i]) || array[i].type!=types[a][i] || array[i].stack!=stacks[a][i])return false;}return true;}
    }
}
