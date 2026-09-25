using System;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Recovery
{
    // Personal-bank writes that bypass ItemSlot. Only the actual container or
    // packed source intersects ownership; unrelated world chests stay usable.
    internal static class RecoveryBankGuards
    {
        private static HostRecovery host;
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Install(HostRecovery value,Harmony harmony)
        {
            host=value;
            Type[] fill={typeof(Item[]),typeof(Item),typeof(GetItemSettings),typeof(Item),typeof(int)};
            Patch(harmony,typeof(Player),"GetItem_FillIntoOccupiedSlot_VoidBag",fill,nameof(Fill));
            Patch(harmony,typeof(Player),"GetItem_FillEmptyInventorySlot_VoidBag",fill,nameof(Fill));
            Patch(harmony,typeof(ChestUI),"MoveCoins",new[]{typeof(Item[]),typeof(Item[]),typeof(int)},nameof(Coins));
            Patch(harmony,typeof(ChestUI),"TryPlacingInChest",new[]{typeof(Item[]),typeof(int),typeof(bool),typeof(int)},nameof(Place));
            Patch(harmony,typeof(ChestUI),"LootAll",Type.EmptyTypes,nameof(Current));
            Patch(harmony,typeof(ChestUI),"DepositAll",Type.EmptyTypes,nameof(Deposit));
            Patch(harmony,typeof(ChestUI),"Restock",Type.EmptyTypes,nameof(Restock));
            Patch(harmony,typeof(ChestUI),"QuickStack",new[]{typeof(bool)},nameof(Stack));
            Patch(harmony,typeof(ItemSorting),"SortInventory",new[]{typeof(Chest),typeof(bool),typeof(bool)},nameof(Sort));
            Patch(harmony,typeof(QuickStacking),"QuickStackToNearbyChests",new[]{typeof(Player),typeof(bool)},nameof(Pack));
            Patch(harmony,typeof(QuickStacking),"QuickStackToNearbyBanks",new[]{typeof(Player)},nameof(Banks));
        }
        private static void Patch(Harmony h,Type type,string name,Type[] args,string prefix)
        {var method=type.GetMethod(name,Flags,null,args,null);if(method==null)throw new MissingMethodException(type.FullName,name);h.Patch(method,new HarmonyMethod(typeof(RecoveryBankGuards).GetMethod(prefix,Flags)));}
        private static bool Active {get{return host!=null && host.Player!=null && !host.Items.World.AutomaticOperation && host.Items.Ownership.AnyProtected;}}
        private static int Account(Item[] array)
        {var p=host.Player;for(int a=0;a<5;a++)if(ReferenceEquals(RecoverySource.AccountItems(p,a),array))return a;return -1;}
        private static bool Owned(Item[] array,int slot)
        {if(!Active)return false;int a=Account(array);return a>=0 && host.Items.Ownership.IsProtected(a,slot);}
        private static bool Any(Item[] array)
        {if(!Active || array==null)return false;int a=Account(array);if(a<0)return false;for(int i=0;i<(a==0?58:40);i++)if(host.Items.Ownership.IsProtected(a,i))return true;return false;}
        private static bool Fill(Item[] __0,int __4,ref bool __result)
        {if(!Owned(__0,__4))return true;__result=false;return false;}
        private static bool Coins(Item[] __0,Item[] __1,int __2,ref long __result)
        {
            if(!Active)return true;
            for(int i=0;i<__0.Length;i++)if(Owned(__0,i) && __0[i].IsACoin && !__0[i].favorited){__result=0;return false;}
            for(int i=0;i<__2;i++)if(Owned(__1,i) && (__1[i].IsAir || __1[i].IsACoin)){__result=0;return false;}
            return true;
        }
        private static bool Place(Item[] __0,int __1,bool __2,ref bool __result)
        {
            if(!Active || __2)return true;
            if(Owned(__0,__1)){__result=false;return false;}
            var chest=Main.LocalPlayer.GetCurrentContainer();if(chest==null)return true;Item source=__0[__1];
            for(int i=0;i<chest.maxItems;i++)if(Owned(chest.item,i) && (chest.item[i].IsAir || chest.item[i].stack<chest.item[i].maxStack && Item.CanStack(source,chest.item[i])))
            {__result=false;return false;}return true;
        }
        private static bool Current(){return !Active || !Any(Main.LocalPlayer.GetCurrentContainer()?.item);}
        private static bool Sources(Item[] array,int first,int count)
        {for(int i=first;i<first+count;i++)if(Owned(array,i) && !array[i].IsAir && !array[i].favorited && !array[i].IsACoin)return true;return false;}
        private static bool Deposit(){return !Active || Current() && !Sources(host.Player.inventory,10,40);}
        private static bool Stack(bool __0){return !Active || Current() && !Sources(__0?host.Player.bank4.item:host.Player.inventory,__0?0:10,40);}
        private static bool Restock()
        {
            if(!Active)return true;if(!Current())return false;
            var chest=host.Player.GetCurrentContainer();if(chest==null)return true;
            for(int i=0;i<58;i++)if(Owned(host.Player.inventory,i))
            {var target=host.Player.inventory[i];if(target.IsAir || target.stack>=target.maxStack)continue;for(int j=0;j<chest.maxItems;j++)if(!chest.item[j].IsAir && Item.CanStack(target,chest.item[j]))return false;}
            return true;
        }
        private static bool Sort(Chest __0){return !Active || __0==null || !Any(__0.item);}
        private static bool Pack(Player __0)
        {
            if(!Active || !ReferenceEquals(__0,host.Player) || !__0.useVoidBag())return true;
            for(int i=0;i<40;i++){Item item=__0.bank4.item[i];if(Owned(__0.bank4.item,i) && !item.IsAir && !item.favorited && !item.IsACoin)return false;}return true;
        }
        private static bool Banks(Player __0)
        {
            if(!Active || !ReferenceEquals(__0,host.Player))return true;
            foreach(var entry in NearbyChests.GetBanksInRangeOf(__0))
            {
                var items=entry.chest.item;
                for(int i=0;i<entry.chest.maxItems;i++)if(Owned(items,i))
                {
                    Item target=items[i];if(target.IsAir || target.IsACoin)return false;
                    for(int s=10;s<50;s++){Item source=__0.inventory[s];if(!source.IsAir && !source.favorited && !source.IsACoin && target.stack<target.maxStack && Item.CanStack(source,target))return false;}
                }
            }
            return true;
        }
    }
}
